using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.LibraryCollage.Configuration;
using Jellyfin.Plugin.LibraryCollage.Rendering;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.LibraryCollage.Services;

/// <summary>
/// Picks posters for a library, renders the collage and sets it as the library image.
/// </summary>
public class CollageService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly ManagedImageStore _store;
    private readonly ILogger<CollageService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CollageService"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="providerManager">The provider manager.</param>
    /// <param name="store">The managed image store.</param>
    /// <param name="logger">The logger.</param>
    public CollageService(ILibraryManager libraryManager, IProviderManager providerManager, ManagedImageStore store, ILogger<CollageService> logger)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _store = store;
        _logger = logger;
    }

    private static PluginConfiguration Config => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    /// <summary>
    /// Gets all libraries with their effective settings and image status.
    /// </summary>
    /// <returns>The libraries.</returns>
    public IReadOnlyList<LibraryStatus> GetLibraries()
    {
        var config = Config;
        return GetCollectionFolders()
            .Select(folder =>
            {
                var setting = GetSetting(config, folder);
                return new LibraryStatus
                {
                    Id = folder.Id,
                    Name = folder.Name,
                    CollectionType = folder.CollectionType?.ToString(),
                    Enabled = setting.Enabled,
                    ForceOverwrite = setting.ForceOverwrite,
                    ImageState = GetImageState(folder)
                };
            })
            .OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Generates collages for all enabled libraries.
    /// </summary>
    /// <param name="progress">Progress, 0–100.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task GenerateAllAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Config;
        var folders = GetCollectionFolders().Where(f => GetSetting(config, f).Enabled).ToList();

        for (var i = 0; i < folders.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = folders[i];
            try
            {
                await GenerateAsync(folder, config, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Library Collage: failed for library {Library}", folder.Name);
            }

            progress.Report(100.0 * (i + 1) / folders.Count);
        }
    }

    /// <summary>
    /// Renders a collage for a library with the current settings without saving it.
    /// </summary>
    /// <param name="libraryId">The library id.</param>
    /// <returns>The JPEG, or <c>null</c> if the library is unknown or has too few posters.</returns>
    public byte[]? RenderPreview(Guid libraryId)
    {
        var folder = GetCollectionFolders().FirstOrDefault(f => f.Id == libraryId);
        if (folder is null)
        {
            return null;
        }

        var config = Config;
        var options = ToOptions(config);
        var posters = SelectPosters(folder, config, CollageRenderer.GetCellCount(options));
        return posters.Count == 0 ? null : CollageRenderer.Render(posters, folder.Name, options);
    }

    private async Task GenerateAsync(CollectionFolder folder, PluginConfiguration config, CancellationToken cancellationToken)
    {
        var setting = GetSetting(config, folder);
        var state = GetImageState(folder);
        if (state == ImageState.Custom && !setting.ForceOverwrite)
        {
            _logger.LogInformation("Library Collage: skipping {Library}, it has an image that was not created by this plugin", folder.Name);
            return;
        }

        var options = ToOptions(config);
        var posters = SelectPosters(folder, config, CollageRenderer.GetCellCount(options));
        if (posters.Count < Math.Max(1, config.MinimumItems))
        {
            _logger.LogInformation(
                "Library Collage: skipping {Library}, only {Count} items with poster (minimum {Minimum})",
                folder.Name,
                posters.Count,
                config.MinimumItems);
            return;
        }

        var jpeg = CollageRenderer.Render(posters, folder.Name, options);

        using (var stream = new MemoryStream(jpeg))
        {
            await _providerManager.SaveImage(folder, stream, "image/jpeg", ImageType.Primary, null, cancellationToken).ConfigureAwait(false);
        }

        await folder.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);

        var written = folder.GetImageInfo(ImageType.Primary, 0);
        if (written is not null && written.IsLocalFile)
        {
            _store.Remember(folder.Id, written.Path);
        }

        _logger.LogInformation("Library Collage: updated image for {Library} from {Count} posters", folder.Name, posters.Count);
    }

    private ImageState GetImageState(CollectionFolder folder)
    {
        var current = folder.GetImageInfo(ImageType.Primary, 0);
        if (current is null || !current.IsLocalFile || !File.Exists(current.Path))
        {
            return ImageState.None;
        }

        return _store.IsManaged(folder.Id, current.Path) ? ImageState.Managed : ImageState.Custom;
    }

    private List<string> SelectPosters(CollectionFolder folder, PluginConfiguration config, int count)
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = GetItemTypes(folder.CollectionType),
            Recursive = true,
            IsVirtualItem = false,
            DtoOptions = new DtoOptions(false)
        };

        var items = folder.GetItemList(query)
            .Select(item => (Item: item, Image: item.GetImageInfo(ImageType.Primary, 0)))
            .Where(x => x.Image is not null && x.Image.IsLocalFile && File.Exists(x.Image.Path))
            .Select(x => (x.Item, x.Image!.Path))
            .ToList();

        IEnumerable<(BaseItem Item, string Path)> ordered = config.SelectionMode switch
        {
            SelectionMode.Newest => items.OrderByDescending(x => x.Item.DateCreated),
            SelectionMode.TopRated => items.OrderByDescending(x => x.Item.CommunityRating ?? 0).ThenByDescending(x => x.Item.CriticRating ?? 0),
            SelectionMode.RandomTopRated => Shuffle(items
                .OrderByDescending(x => x.Item.CommunityRating ?? 0)
                .ThenByDescending(x => x.Item.CriticRating ?? 0)
                .Take(Math.Max(count, (int)Math.Ceiling(items.Count * Math.Clamp(config.TopRatedPercent, 1, 100) / 100.0)))
                .ToList()),
            _ => Shuffle(items),
        };

        return ordered
            .Select(x => x.Path)
            .Distinct(StringComparer.Ordinal)
            .Take(count)
            .ToList();
    }

    private static IEnumerable<T> Shuffle<T>(List<T> list)
    {
        var copy = list.ToArray();
        Random.Shared.Shuffle(copy);
        return copy;
    }

    private static BaseItemKind[] GetItemTypes(CollectionType? collectionType) => collectionType switch
    {
        CollectionType.movies => [BaseItemKind.Movie],
        CollectionType.tvshows => [BaseItemKind.Series],
        CollectionType.boxsets => [BaseItemKind.BoxSet],
        CollectionType.music => [BaseItemKind.MusicAlbum],
        CollectionType.musicvideos => [BaseItemKind.MusicVideo],
        CollectionType.homevideos => [BaseItemKind.Video, BaseItemKind.Photo],
        CollectionType.books => [BaseItemKind.Book, BaseItemKind.AudioBook],
        _ => [BaseItemKind.Movie, BaseItemKind.Series],
    };

    private static CollageOptions ToOptions(PluginConfiguration config) => new()
    {
        Layout = config.Layout,
        DimPercent = config.DimPercent,
        DrawText = config.DrawText,
        TextStyle = config.TextStyle
    };

    private static LibrarySetting GetSetting(PluginConfiguration config, CollectionFolder folder)
    {
        return config.Libraries.FirstOrDefault(l => l.LibraryId == folder.Id)
            ?? new LibrarySetting { LibraryId = folder.Id, Enabled = folder.CollectionType == CollectionType.movies };
    }

    private IEnumerable<CollectionFolder> GetCollectionFolders()
    {
        return _libraryManager.GetVirtualFolders()
            .Select(vf => Guid.TryParse(vf.ItemId, out var id) ? _libraryManager.GetItemById(id) : null)
            .OfType<CollectionFolder>();
    }
}

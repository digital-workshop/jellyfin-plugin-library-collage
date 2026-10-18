using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Jellyfin.Plugin.LibraryCollage.Services;

/// <summary>
/// Remembers which library images this plugin wrote (path + hash).
/// Kept outside the plugin configuration so saving the settings page can't overwrite it with stale data.
/// </summary>
public class ManagedImageStore
{
    private readonly object _lock = new();
    private Dictionary<Guid, ManagedImage>? _entries;

    private static string FilePath
    {
        get
        {
            var folder = Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin not initialized.");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "managed-images.json");
        }
    }

    /// <summary>
    /// Returns whether the given file is exactly the image the plugin wrote for this library.
    /// </summary>
    /// <param name="libraryId">The library id.</param>
    /// <param name="imagePath">The library's current primary image path.</param>
    /// <returns><c>true</c> if the plugin wrote this file and it is unchanged.</returns>
    public bool IsManaged(Guid libraryId, string imagePath)
    {
        ManagedImage? entry;
        lock (_lock)
        {
            Load().TryGetValue(libraryId, out entry);
        }

        return entry is not null
            && string.Equals(entry.Path, imagePath, StringComparison.Ordinal)
            && File.Exists(imagePath)
            && string.Equals(entry.Hash, HashFile(imagePath), StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns whether the plugin has ever written an image for this library.
    /// </summary>
    /// <param name="libraryId">The library id.</param>
    /// <returns><c>true</c> if there is an entry.</returns>
    public bool HasEntry(Guid libraryId)
    {
        lock (_lock)
        {
            return Load().ContainsKey(libraryId);
        }
    }

    /// <summary>
    /// Records the image the plugin just wrote.
    /// </summary>
    /// <param name="libraryId">The library id.</param>
    /// <param name="imagePath">The written file.</param>
    public void Remember(Guid libraryId, string imagePath)
    {
        var entry = new ManagedImage { Path = imagePath, Hash = HashFile(imagePath) };
        lock (_lock)
        {
            var entries = Load();
            entries[libraryId] = entry;
            File.WriteAllText(FilePath, JsonSerializer.Serialize(entries));
        }
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private Dictionary<Guid, ManagedImage> Load()
    {
        if (_entries is not null)
        {
            return _entries;
        }

        var path = FilePath;
        _entries = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<Guid, ManagedImage>>(File.ReadAllText(path)) ?? new()
            : new();
        return _entries;
    }

    /// <summary>
    /// An image the plugin has written.
    /// </summary>
    public sealed class ManagedImage
    {
        /// <summary>Gets or sets the image path at the time it was written.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Gets or sets the SHA-256 of the written file.</summary>
        public string Hash { get; set; } = string.Empty;
    }
}

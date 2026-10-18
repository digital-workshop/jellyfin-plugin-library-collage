using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.LibraryCollage.Services;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.LibraryCollage.Tasks;

/// <summary>
/// Scheduled task that regenerates the library collages.
/// </summary>
public class GenerateCollagesTask : IScheduledTask
{
    private readonly CollageService _collageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateCollagesTask"/> class.
    /// </summary>
    /// <param name="collageService">The collage service.</param>
    public GenerateCollagesTask(CollageService collageService)
    {
        _collageService = collageService;
    }

    /// <inheritdoc />
    public string Name => "Bibliotheks-Collagen erzeugen";

    /// <inheritdoc />
    public string Key => "LibraryCollageGenerate";

    /// <inheritdoc />
    public string Description => "Erzeugt für die ausgewählten Bibliotheken eine neue Poster-Collage als Bibliotheksbild.";

    /// <inheritdoc />
    public string Category => "Library Collage";

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        => _collageService.GenerateAllAsync(progress, cancellationToken);

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.WeeklyTrigger,
            DayOfWeek = DayOfWeek.Sunday,
            TimeOfDayTicks = TimeSpan.FromHours(4).Ticks
        };
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.LibraryCollage.Services;
using Jellyfin.Plugin.LibraryCollage.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.LibraryCollage.Api;

/// <summary>
/// Endpoints for the settings page.
/// </summary>
[ApiController]
[Route("LibraryCollage")]
[Authorize(Policy = Policies.RequiresElevation)]
public class LibraryCollageController : ControllerBase
{
    private readonly CollageService _collageService;
    private readonly ITaskManager _taskManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryCollageController"/> class.
    /// </summary>
    /// <param name="collageService">The collage service.</param>
    /// <param name="taskManager">The task manager.</param>
    public LibraryCollageController(CollageService collageService, ITaskManager taskManager)
    {
        _collageService = collageService;
        _taskManager = taskManager;
    }

    /// <summary>
    /// Lists all libraries with their settings and image status.
    /// </summary>
    /// <returns>The libraries.</returns>
    [HttpGet("Libraries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<LibraryStatus>> GetLibraries() => Ok(_collageService.GetLibraries());

    /// <summary>
    /// Renders a preview with the saved settings, without changing the library image.
    /// </summary>
    /// <param name="libraryId">The library id.</param>
    /// <returns>The JPEG.</returns>
    [HttpGet("Preview/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetPreview([FromRoute] Guid libraryId)
    {
        var jpeg = _collageService.RenderPreview(libraryId);
        return jpeg is null ? NotFound() : File(jpeg, "image/jpeg");
    }

    /// <summary>
    /// Generates the collage for one library now.
    /// </summary>
    /// <param name="libraryId">The library id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What happened.</returns>
    [HttpPost("Generate/{libraryId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateOne([FromRoute] Guid libraryId, CancellationToken cancellationToken)
    {
        var result = await _collageService.GenerateOneAsync(libraryId, cancellationToken).ConfigureAwait(false);
        return Ok(new { Result = result });
    }

    /// <summary>
    /// Starts the scheduled task now.
    /// </summary>
    /// <returns>No content.</returns>
    [HttpPost("Generate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Generate()
    {
        _taskManager.QueueScheduledTask<GenerateCollagesTask>();
        return NoContent();
    }
}

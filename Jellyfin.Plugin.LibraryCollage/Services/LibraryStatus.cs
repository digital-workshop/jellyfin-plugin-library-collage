using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.LibraryCollage.Services;

/// <summary>
/// Where a library's current image comes from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ImageState>))]
public enum ImageState
{
    /// <summary>The library has no image.</summary>
    None,

    /// <summary>The image was written by this plugin and is unchanged.</summary>
    Managed,

    /// <summary>The image was set some other way.</summary>
    Custom
}

/// <summary>
/// Outcome of generating one library's collage.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GenerateResult>))]
public enum GenerateResult
{
    /// <summary>The image was written.</summary>
    Generated,

    /// <summary>The library has an image not made by the plugin and overwriting is off.</summary>
    SkippedOwnImage,

    /// <summary>Fewer items with a poster than the configured minimum.</summary>
    TooFewItems,

    /// <summary>No such library.</summary>
    NotFound
}

/// <summary>
/// A library as shown on the settings page.
/// </summary>
public class LibraryStatus
{
    /// <summary>Gets or sets the library id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the library name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the collection type.</summary>
    public string? CollectionType { get; set; }

    /// <summary>Gets or sets a value indicating whether collages are generated.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets a value indicating whether a custom image may be replaced.</summary>
    public bool ForceOverwrite { get; set; }

    /// <summary>Gets or sets the per-library text style, or <c>null</c> for the global style.</summary>
    public string? TextStyle { get; set; }

    /// <summary>Gets or sets the image state.</summary>
    public ImageState ImageState { get; set; }
}

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

    /// <summary>Gets or sets the image state.</summary>
    public ImageState ImageState { get; set; }
}

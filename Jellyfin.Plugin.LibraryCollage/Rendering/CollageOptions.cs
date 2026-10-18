using Jellyfin.Plugin.LibraryCollage.Configuration;

namespace Jellyfin.Plugin.LibraryCollage.Rendering;

/// <summary>
/// Everything the renderer needs to know besides the posters and the title.
/// </summary>
public sealed class CollageOptions
{
    /// <summary>Gets the output width in pixels.</summary>
    public int Width { get; init; } = 1920;

    /// <summary>Gets the output height in pixels.</summary>
    public int Height { get; init; } = 1080;

    /// <summary>Gets the poster layout.</summary>
    public CollageLayout Layout { get; init; } = CollageLayout.Tilted;

    /// <summary>Gets the rotation of the tilted layout in degrees.</summary>
    public float TiltDegrees { get; init; } = 12f;

    /// <summary>Gets the darkening strength, 0–100.</summary>
    public int DimPercent { get; init; } = 50;

    /// <summary>Gets a value indicating whether the title is drawn.</summary>
    public bool DrawText { get; init; } = true;

    /// <summary>Gets the text style.</summary>
    public TextStyle TextStyle { get; init; } = TextStyle.Poster;

    /// <summary>Gets the JPEG quality, 1–100.</summary>
    public int JpegQuality { get; init; } = 90;
}

namespace Jellyfin.Plugin.LibraryCollage.Configuration;

/// <summary>
/// How the posters are arranged.
/// </summary>
public enum CollageLayout
{
    /// <summary>Posters in rows, rotated by a few degrees.</summary>
    Tilted = 0,

    /// <summary>Straight poster grid.</summary>
    Grid = 1
}

/// <summary>
/// How the library name is drawn.
/// </summary>
public enum TextStyle
{
    /// <summary>Centered, bold sans serif.</summary>
    Classic = 0,

    /// <summary>Centered, upper case, condensed display font.</summary>
    Poster = 1,

    /// <summary>Bottom left, smaller, with a gradient towards the bottom.</summary>
    Netflix = 2,

    /// <summary>Centered, dark upper case text on a translucent light box.</summary>
    Label = 3
}

/// <summary>
/// Which items of a library end up in the collage.
/// </summary>
public enum SelectionMode
{
    /// <summary>Random items from the whole library.</summary>
    Random = 0,

    /// <summary>Most recently added items.</summary>
    Newest = 1,

    /// <summary>Highest community rating.</summary>
    TopRated = 2,

    /// <summary>Random items from the top rated share of the library.</summary>
    RandomTopRated = 3
}

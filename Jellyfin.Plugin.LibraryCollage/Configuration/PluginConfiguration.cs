using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.LibraryCollage.Configuration;

/// <summary>
/// Plugin settings, persisted as XML by Jellyfin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the per-library settings. Libraries missing here use the defaults (movie libraries on).</summary>
    public List<LibrarySetting> Libraries { get; set; } = new();

    /// <summary>Gets or sets the poster layout.</summary>
    public CollageLayout Layout { get; set; } = CollageLayout.Tilted;

    /// <summary>Gets or sets the darkening strength in percent.</summary>
    public int DimPercent { get; set; } = 50;

    /// <summary>Gets or sets a value indicating whether the library name is drawn into the image.</summary>
    public bool DrawText { get; set; } = true;

    /// <summary>Gets or sets the text style.</summary>
    public TextStyle TextStyle { get; set; } = TextStyle.Poster;

    /// <summary>Gets or sets a value indicating whether long titles wrap at one font size instead of shrinking.</summary>
    public bool WrapLongTitles { get; set; }

    /// <summary>Gets or sets the text size in percent of the style's default (50–150).</summary>
    public int TextSizePercent { get; set; } = 100;

    /// <summary>Gets or sets the text colour as #rrggbb, or empty for the style's default.</summary>
    public string TextColor { get; set; } = string.Empty;

    /// <summary>Gets or sets the Label style's box colour as #rrggbb, or empty for the default.</summary>
    public string LabelBoxColor { get; set; } = string.Empty;

    /// <summary>Gets or sets how items are picked.</summary>
    public SelectionMode SelectionMode { get; set; } = SelectionMode.RandomTopRated;

    /// <summary>Gets or sets the top rated share (percent) used by <see cref="SelectionMode.RandomTopRated"/>.</summary>
    public int TopRatedPercent { get; set; } = 50;

    /// <summary>Gets or sets the minimum number of items with a poster; smaller libraries are skipped.</summary>
    public int MinimumItems { get; set; } = 4;
}

/// <summary>
/// Settings for one library.
/// </summary>
public class LibrarySetting
{
    /// <summary>Gets or sets the library (collection folder) id.</summary>
    public Guid LibraryId { get; set; }

    /// <summary>Gets or sets a value indicating whether a collage is generated for this library.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets a value indicating whether an image not created by the plugin may be replaced.</summary>
    public bool ForceOverwrite { get; set; }

    /// <summary>Gets or sets the text style for this library; <c>null</c> uses the global style.</summary>
    public TextStyle? TextStyle { get; set; }
}

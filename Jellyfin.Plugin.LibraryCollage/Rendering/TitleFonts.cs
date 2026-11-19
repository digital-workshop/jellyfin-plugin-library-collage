using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Jellyfin.Plugin.LibraryCollage.Configuration;
using SkiaSharp;

namespace Jellyfin.Plugin.LibraryCollage.Rendering;

/// <summary>Resolves title glyphs using only the fonts embedded in the plugin.</summary>
internal sealed class TitleFonts : IDisposable
{
    private static readonly Lazy<SKTypeface>[] Faces =
    [
        Embedded("NotoSans-Bold.ttf"),
        Embedded("IBMPlexSans-Bold.ttf"),
        Embedded("Montserrat-Bold.ttf"),
        Embedded("RobotoCondensed-Bold.ttf"),
        Embedded("BebasNeue-Regular.ttf")
    ];

    private readonly SKFont _primary;
    private readonly SKFont _fallback;
    private readonly Dictionary<string, List<(SKFont Font, string Text)>> _runs = new();

    public TitleFonts(TitleFont selection, float size)
    {
        var index = (int)selection;
        _primary = Create(index >= 0 && index < Faces.Length ? Faces[index].Value : Faces[0].Value);
        _fallback = Create(Faces[0].Value);
        Size = size;
    }

    public float Size
    {
        get => _primary.Size;
        set => _primary.Size = _fallback.Size = value;
    }

    public (float Ascent, float Descent) GetVerticalMetrics(IEnumerable<string> lines)
    {
        var ascent = 0f;
        var descent = 0f;
        foreach (var line in lines)
        {
            foreach (var run in Runs(line))
            {
                ascent = Math.Max(ascent, -run.Font.Metrics.Ascent);
                descent = Math.Max(descent, run.Font.Metrics.Descent);
            }
        }

        return (ascent, descent);
    }

    public float MeasureText(string text)
    {
        var width = 0f;
        foreach (var (font, run) in Runs(text))
        {
            width += font.MeasureText(run);
        }

        return width;
    }

    public void Draw(SKCanvas canvas, string text, float left, float baseline, SKPaint fill, SKPaint? outline)
    {
        foreach (var (font, run) in Runs(text))
        {
            if (outline is not null)
            {
                canvas.DrawText(run, left, baseline, SKTextAlign.Left, font, outline);
            }

            canvas.DrawText(run, left, baseline, SKTextAlign.Left, font, fill);
            left += font.MeasureText(run);
        }
    }

    internal List<(SKFont Font, string Text)> Runs(string text)
    {
        if (_runs.TryGetValue(text, out var cached))
        {
            return cached;
        }

        var runs = new List<(SKFont, string)>();
        var active = _primary;
        var buffer = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            var font = _primary.GetGlyph(rune.Value) != 0 ? _primary : _fallback;
            if (active != font && buffer.Length > 0)
            {
                runs.Add((active, buffer.ToString()));
                buffer.Clear();
            }

            active = font;
            buffer.Append(font.GetGlyph(rune.Value) != 0 ? rune.ToString() : "�");
        }

        if (buffer.Length > 0)
        {
            runs.Add((active, buffer.ToString()));
        }

        return _runs[text] = runs;
    }

    public void Dispose()
    {
        _primary.Dispose();
        _fallback.Dispose();
    }

    private static SKFont Create(SKTypeface face) =>
        new(face) { Edging = SKFontEdging.Antialias, Subpixel = true };

    private static Lazy<SKTypeface> Embedded(string fileName) => new(() =>
    {
        var name = $"{typeof(TitleFonts).Namespace}.Fonts.{fileName}";
        using var stream = typeof(TitleFonts).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded font '{name}' not found.");
        return SKTypeface.FromStream(stream) ?? throw new InvalidOperationException($"Invalid font '{name}'.");
    });
}

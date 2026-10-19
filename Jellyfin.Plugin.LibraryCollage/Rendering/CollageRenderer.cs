using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.LibraryCollage.Configuration;
using SkiaSharp;

namespace Jellyfin.Plugin.LibraryCollage.Rendering;

/// <summary>
/// Draws a darkened poster collage with the library name on top.
/// Has no Jellyfin dependencies so it can be tested on its own.
/// </summary>
public static class CollageRenderer
{
    private const float PosterAspect = 2f / 3f;

    private static readonly Lazy<SKTypeface> DisplayTypeface = new(() => LoadFont("BebasNeue-Regular.ttf"));
    private static readonly Lazy<SKTypeface> SansTypeface = new(() => LoadFont("Montserrat-ExtraBold.ttf"));

    /// <summary>
    /// Gets how many poster cells the layout has, i.e. how many distinct posters are useful at most.
    /// </summary>
    /// <param name="options">The render options.</param>
    /// <returns>The number of cells.</returns>
    public static int GetCellCount(CollageOptions options) => GetCells(options).Count;

    /// <summary>
    /// Renders the collage as JPEG.
    /// </summary>
    /// <param name="posterPaths">Poster image files in order of preference. Repeated if there are fewer than cells.</param>
    /// <param name="title">The text drawn on top.</param>
    /// <param name="options">The render options.</param>
    /// <returns>The encoded JPEG.</returns>
    public static byte[] Render(IReadOnlyList<string> posterPaths, string title, CollageOptions options)
    {
        ArgumentNullException.ThrowIfNull(posterPaths);
        ArgumentNullException.ThrowIfNull(options);

        var images = new Dictionary<string, SKImage?>(StringComparer.Ordinal);
        try
        {
            foreach (var path in posterPaths.Distinct(StringComparer.Ordinal))
            {
                images[path] = TryLoad(path);
            }

            var usable = posterPaths.Where(p => images[p] is not null).Distinct(StringComparer.Ordinal).ToList();

            using var surface = SKSurface.Create(new SKImageInfo(options.Width, options.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;
            canvas.Clear(new SKColor(16, 16, 20));

            DrawPosters(canvas, usable, images, options);
            DrawDim(canvas, options);

            if (options.DrawText && !string.IsNullOrWhiteSpace(title))
            {
                DrawTitle(canvas, title.Trim(), options);
            }

            using var snapshot = surface.Snapshot();
            using var data = snapshot.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(options.JpegQuality, 1, 100));
            return data.ToArray();
        }
        finally
        {
            foreach (var image in images.Values)
            {
                image?.Dispose();
            }
        }
    }

    private static SKImage? TryLoad(string path)
    {
        try
        {
            return File.Exists(path) ? SKImage.FromEncodedData(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void DrawPosters(SKCanvas canvas, List<string> posters, Dictionary<string, SKImage?> images, CollageOptions options)
    {
        var cells = GetCells(options);
        var assignment = AssignPosters(cells, posters.Count);
        var radius = cells.Count > 0 ? cells[0].Rect.Width * 0.035f : 0;

        canvas.Save();
        if (options.Layout == CollageLayout.Tilted)
        {
            canvas.Translate(options.Width / 2f, options.Height / 2f);
            canvas.RotateDegrees(-options.TiltDegrees);
        }

        using var placeholder = new SKPaint { Color = new SKColor(40, 40, 48), IsAntialias = true };
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);

        for (var i = 0; i < cells.Count; i++)
        {
            var rect = cells[i].Rect;
            var roundRect = new SKRoundRect(rect, radius);
            var image = assignment[i] >= 0 ? images[posters[assignment[i]]] : null;

            if (image is null)
            {
                canvas.DrawRoundRect(roundRect, placeholder);
                continue;
            }

            canvas.Save();
            canvas.ClipRoundRect(roundRect, antialias: true);
            canvas.DrawImage(image, CoverSource(image, rect), rect, sampling, null);
            canvas.Restore();
        }

        canvas.Restore();
    }

    /// <summary>
    /// Crops the source so it fills the destination without distortion.
    /// </summary>
    private static SKRect CoverSource(SKImage image, SKRect dest)
    {
        var destAspect = dest.Width / dest.Height;
        var srcAspect = (float)image.Width / image.Height;
        if (srcAspect > destAspect)
        {
            var w = image.Height * destAspect;
            var x = (image.Width - w) / 2f;
            return new SKRect(x, 0, x + w, image.Height);
        }

        var h = image.Width / destAspect;
        var y = (image.Height - h) / 2f;
        return new SKRect(0, y, image.Width, y + h);
    }

    private static List<Cell> GetCells(CollageOptions options)
    {
        float w = options.Width;
        float h = options.Height;
        var cells = new List<Cell>();

        if (options.Layout == CollageLayout.Grid)
        {
            const int Rows = 3;
            var gap = h * 0.006f;
            var ph = (h - (gap * (Rows - 1))) / Rows;
            var pw = ph * PosterAspect;
            var cols = (int)Math.Ceiling((w + gap) / (pw + gap));
            var gridW = (cols * (pw + gap)) - gap;
            var x0 = (w - gridW) / 2f;
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    var x = x0 + (c * (pw + gap));
                    var y = r * (ph + gap);
                    cells.Add(new Cell(r, c, new SKRect(x, y, x + pw, y + ph)));
                }
            }

            return cells;
        }

        // Tilted: lay out a staggered grid around the origin that still covers the canvas after rotation.
        var rad = Math.Abs(options.TiltDegrees) * MathF.PI / 180f;
        var coverW = (w * MathF.Cos(rad)) + (h * MathF.Sin(rad));
        var coverH = (w * MathF.Sin(rad)) + (h * MathF.Cos(rad));

        var posterH = h * 0.56f;
        var posterW = posterH * PosterAspect;
        var gapT = posterH * 0.025f;
        var stepX = posterW + gapT;
        var stepY = posterH + gapT;

        var rowsT = (int)Math.Ceiling((coverH + gapT) / stepY);
        var colsT = (int)Math.Ceiling((coverW + gapT) / stepX) + 1;
        var totalW = (colsT * stepX) - gapT;
        var totalH = (rowsT * stepY) - gapT;

        for (var r = 0; r < rowsT; r++)
        {
            var shift = r % 2 == 1 ? -stepX / 2f : 0f;
            for (var c = 0; c < colsT; c++)
            {
                var x = (-totalW / 2f) + (c * stepX) + shift;
                var y = (-totalH / 2f) + (r * stepY);
                cells.Add(new Cell(r, c, new SKRect(x, y, x + posterW, y + posterH)));
            }
        }

        return cells;
    }

    /// <summary>
    /// Fills the cells in order of preference and, when posters have to repeat,
    /// avoids the same poster sitting right next to itself.
    /// </summary>
    private static int[] AssignPosters(List<Cell> cells, int posterCount)
    {
        var result = new int[cells.Count];
        if (posterCount == 0)
        {
            Array.Fill(result, -1);
            return result;
        }

        var byPosition = new Dictionary<(int Row, int Col), int>();
        var deck = new List<int>();
        var round = 0;

        for (var i = 0; i < cells.Count; i++)
        {
            if (deck.Count == 0)
            {
                // Rotate each refill so repeats don't line up in the same pattern.
                var offset = round++ % posterCount;
                deck.AddRange(Enumerable.Range(0, posterCount).Select(n => (n + offset) % posterCount));
            }

            var cell = cells[i];
            var neighbours = new HashSet<int>();
            foreach (var pos in new[] { (cell.Row, cell.Col - 1), (cell.Row - 1, cell.Col - 1), (cell.Row - 1, cell.Col), (cell.Row - 1, cell.Col + 1) })
            {
                if (byPosition.TryGetValue(pos, out var p))
                {
                    neighbours.Add(p);
                }
            }

            var pick = deck.FindIndex(p => !neighbours.Contains(p));
            if (pick < 0)
            {
                pick = 0;
            }

            result[i] = deck[pick];
            deck.RemoveAt(pick);
            byPosition[(cell.Row, cell.Col)] = result[i];
        }

        return result;
    }

    private static void DrawDim(SKCanvas canvas, CollageOptions options)
    {
        var alpha = (byte)Math.Round(Math.Clamp(options.DimPercent, 0, 100) * 2.55);
        using (var dim = new SKPaint { Color = SKColors.Black.WithAlpha(alpha) })
        {
            canvas.DrawRect(0, 0, options.Width, options.Height, dim);
        }

        if (options.DrawText && options.TextStyle == TextStyle.Netflix)
        {
            var top = options.Height * 0.4f;
            using var gradient = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, top),
                    new SKPoint(0, options.Height),
                    new[] { SKColors.Black.WithAlpha(0), SKColors.Black.WithAlpha(220) },
                    SKShaderTileMode.Clamp)
            };
            canvas.DrawRect(0, top, options.Width, options.Height - top, gradient);
        }
    }

    private static void DrawTitle(SKCanvas canvas, string title, CollageOptions options)
    {
        float w = options.Width;
        float h = options.Height;

        var (typeface, text, targetSize, maxWidth) = options.TextStyle switch
        {
            TextStyle.Poster => (DisplayTypeface.Value, title.ToUpper(CultureInfo.CurrentCulture), h * 0.30f, w * 0.88f),
            TextStyle.Label => (DisplayTypeface.Value, title.ToUpper(CultureInfo.CurrentCulture), h * 0.26f, w * 0.74f),
            TextStyle.Netflix => (SansTypeface.Value, title, h * 0.10f, w * 0.88f),
            _ => (SansTypeface.Value, title, h * 0.19f, w * 0.86f),
        };
        targetSize *= Math.Clamp(options.TextScale, 0.5f, 1.5f);

        using var font = new SKFont(typeface, targetSize) { Edging = SKFontEdging.Antialias, Subpixel = true };
        var lines = options.WrapLongTitles
            ? WrapLines(font, text, targetSize * 0.8f, maxWidth, options.TextStyle == TextStyle.Netflix ? h * 0.5f : h * 0.75f)
            : FitLines(font, text, targetSize, maxWidth);

        using var paint = new SKPaint
        {
            Color = options.TextColor ?? SKColors.White,
            IsAntialias = true,
            ImageFilter = SKImageFilter.CreateDropShadow(0, h * 0.004f, h * 0.008f, h * 0.008f, SKColors.Black.WithAlpha(190))
        };

        var metrics = font.Metrics;
        var capHeight = metrics.CapHeight > 0 ? metrics.CapHeight : -metrics.Ascent * 0.7f;
        var lineHeight = capHeight * 1.35f;
        var blockHeight = capHeight + (lineHeight * (lines.Count - 1));

        if (options.TextStyle == TextStyle.Netflix)
        {
            var x = w * 0.05f;
            var lastBaseline = h * 0.88f;
            for (var i = 0; i < lines.Count; i++)
            {
                var baseline = lastBaseline - (lineHeight * (lines.Count - 1 - i));
                canvas.DrawText(lines[i], x, baseline, SKTextAlign.Left, font, paint);
            }

            return;
        }

        var firstBaseline = ((h - blockHeight) / 2f) + capHeight;

        if (options.TextStyle == TextStyle.Label)
        {
            DrawLabelBox(canvas, font, lines, w / 2f, h / 2f, blockHeight, capHeight, (options.LabelBoxColor ?? new SKColor(235, 235, 240)).WithAlpha((byte)Math.Round(Math.Clamp(options.LabelBoxOpacity, 0, 100) * 2.55)));
            paint.Color = options.TextColor ?? new SKColor(24, 24, 28);
            paint.ImageFilter = null;
        }

        for (var i = 0; i < lines.Count; i++)
        {
            canvas.DrawText(lines[i], w / 2f, firstBaseline + (lineHeight * i), SKTextAlign.Center, font, paint);
        }
    }

    /// <summary>
    /// Draws the translucent light box behind the text of the label style.
    /// </summary>
    private static void DrawLabelBox(SKCanvas canvas, SKFont font, List<string> lines, float centerX, float centerY, float blockHeight, float capHeight, SKColor boxColor)
    {
        var textWidth = lines.Max(l => font.MeasureText(l));
        var padX = capHeight * 0.32f;
        var padY = capHeight * 0.26f;
        var boxWidth = textWidth + (2 * padX);
        var boxHeight = blockHeight + (2 * padY);
        var box = SKRect.Create(centerX - (boxWidth / 2f), centerY - (boxHeight / 2f), boxWidth, boxHeight);
        var radius = capHeight * 0.16f;

        using var shadow = new SKPaint
        {
            Color = SKColors.Black.WithAlpha((byte)(110 * boxColor.Alpha / 255)),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, capHeight * 0.08f)
        };
        canvas.DrawRoundRect(box.Left, box.Top + (capHeight * 0.03f), box.Width, box.Height, radius, radius, shadow);

        using var fill = new SKPaint { Color = boxColor, IsAntialias = true };
        canvas.DrawRoundRect(box, radius, radius, fill);
    }

    /// <summary>
    /// Shrinks the font until the text fits. If one line would get too small, splits into two lines.
    /// </summary>
    private static List<string> FitLines(SKFont font, string text, float targetSize, float maxWidth)
    {
        var lines = new List<string> { text };
        var size = SizeToFit(font, lines, targetSize, maxWidth);

        if (size < targetSize * 0.6f && TrySplit(text, out var first, out var second))
        {
            var twoLines = new List<string> { first, second };
            var twoLineSize = SizeToFit(font, twoLines, targetSize * 0.8f, maxWidth);
            if (twoLineSize > size)
            {
                lines = twoLines;
                size = twoLineSize;
            }
        }

        font.Size = size;
        return lines;
    }

    /// <summary>
    /// Keeps one font size for every title and wraps onto more lines instead.
    /// Only shrinks when a single word is wider than a line or the lines don't fit in height.
    /// </summary>
    private static List<string> WrapLines(SKFont font, string text, float size, float maxWidth, float maxHeight)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string> { text };

        for (var attempt = 0; attempt < 20; attempt++)
        {
            font.Size = size;
            lines = new List<string>();
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 && font.MeasureText(candidate) > maxWidth)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }

            lines.Add(current);

            var widest = lines.Max(l => font.MeasureText(l));
            var capHeight = font.Metrics.CapHeight > 0 ? font.Metrics.CapHeight : -font.Metrics.Ascent * 0.7f;
            var blockHeight = capHeight + (capHeight * 1.35f * (lines.Count - 1));
            var scale = Math.Min(maxWidth / widest, maxHeight / blockHeight);
            if (scale >= 1f)
            {
                break;
            }

            size *= Math.Max(scale, 0.5f) * 0.98f;
        }

        return lines;
    }

    private static float SizeToFit(SKFont font, List<string> lines, float targetSize, float maxWidth)
    {
        font.Size = targetSize;
        var widest = lines.Max(l => font.MeasureText(l));
        return widest <= maxWidth ? targetSize : targetSize * maxWidth / widest;
    }

    private static bool TrySplit(string text, out string first, out string second)
    {
        first = text;
        second = string.Empty;
        var middle = text.Length / 2;
        var best = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == ' ' && (best < 0 || Math.Abs(i - middle) < Math.Abs(best - middle)))
            {
                best = i;
            }
        }

        if (best <= 0)
        {
            return false;
        }

        first = text[..best].Trim();
        second = text[(best + 1)..].Trim();
        return first.Length > 0 && second.Length > 0;
    }

    private static SKTypeface LoadFont(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = $"{typeof(CollageRenderer).Namespace}.Fonts.{fileName}";
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded font '{name}' not found.");
        return SKTypeface.FromStream(stream) ?? SKTypeface.Default;
    }

    private readonly record struct Cell(int Row, int Col, SKRect Rect);
}

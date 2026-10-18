using Jellyfin.Plugin.LibraryCollage.Configuration;
using Jellyfin.Plugin.LibraryCollage.Rendering;
using SkiaSharp;

namespace Jellyfin.Plugin.LibraryCollage.Tests;

public sealed class CollageRendererTests : IDisposable
{
    private static readonly string SamplesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples"));

    private readonly string _posterDir = Path.Combine(Path.GetTempPath(), "lc-tests-" + Guid.NewGuid().ToString("N"));

    public CollageRendererTests()
    {
        Directory.CreateDirectory(_posterDir);
        Directory.CreateDirectory(SamplesDir);
    }

    public void Dispose() => Directory.Delete(_posterDir, true);

    [Theory]
    [InlineData(CollageLayout.Tilted, TextStyle.Poster, "Horror")]
    [InlineData(CollageLayout.Tilted, TextStyle.Classic, "Filme")]
    [InlineData(CollageLayout.Tilted, TextStyle.Netflix, "Action")]
    [InlineData(CollageLayout.Grid, TextStyle.Poster, "Sammlungen")]
    [InlineData(CollageLayout.Tilted, TextStyle.Label, "Fantasy")]
    [InlineData(CollageLayout.Tilted, TextStyle.Label, "Kinderfilme Klassiker der Achtziger")]
    [InlineData(CollageLayout.Tilted, TextStyle.Poster, "Kinderfilme Klassiker der Achtziger")]
    public void Render_ProducesFullHdJpeg(CollageLayout layout, TextStyle style, string title)
    {
        var options = new CollageOptions { Layout = layout, TextStyle = style };
        var posters = CreatePosters(CollageRenderer.GetCellCount(options));

        var jpeg = CollageRenderer.Render(posters, title, options);

        File.WriteAllBytes(Path.Combine(SamplesDir, $"{layout}-{style}-{title.Replace(' ', '_')}.jpg"), jpeg);
        using var decoded = SKBitmap.Decode(jpeg);
        Assert.Equal(1920, decoded.Width);
        Assert.Equal(1080, decoded.Height);
    }

    [Theory]
    [InlineData(CollageLayout.Tilted, 15, 30)]
    [InlineData(CollageLayout.Grid, 15, 30)]
    public void CellCount_IsAroundTwenty(CollageLayout layout, int min, int max)
    {
        var count = CollageRenderer.GetCellCount(new CollageOptions { Layout = layout });
        Assert.InRange(count, min, max);
    }

    [Fact]
    public void Render_Banner_ForReadme()
    {
        var options = new CollageOptions { Width = 1600, Height = 500, DimPercent = 45 };
        var jpeg = CollageRenderer.Render(CreatePosters(CollageRenderer.GetCellCount(options)), "Library Collage", options);
        File.WriteAllBytes(Path.Combine(SamplesDir, "banner.jpg"), jpeg);
        Assert.NotEmpty(jpeg);
    }

    [Fact]
    public void Render_WithFewPosters_RepeatsThem()
    {
        var jpeg = CollageRenderer.Render(CreatePosters(4), "Weihnachten", new CollageOptions());
        File.WriteAllBytes(Path.Combine(SamplesDir, "Tilted-Poster-4_Poster.jpg"), jpeg);
        Assert.NotEmpty(jpeg);
    }

    [Fact]
    public void Render_IgnoresMissingAndBrokenFiles()
    {
        var broken = Path.Combine(_posterDir, "broken.jpg");
        File.WriteAllText(broken, "not an image");
        var posters = new List<string>(CreatePosters(3)) { broken, Path.Combine(_posterDir, "missing.jpg") };

        var jpeg = CollageRenderer.Render(posters, "Test", new CollageOptions());

        Assert.NotEmpty(jpeg);
    }

    [Fact]
    public void Render_WithoutTextAndPosters_DoesNotThrow()
    {
        var jpeg = CollageRenderer.Render([], "Leer", new CollageOptions { DrawText = false });
        Assert.NotEmpty(jpeg);
    }

    /// <summary>
    /// Creates simple stand-in posters: a hue gradient with a big number.
    /// </summary>
    private List<string> CreatePosters(int count)
    {
        var paths = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var hue = i * 360f / count;
            using var surface = SKSurface.Create(new SKImageInfo(400, 600));
            using (var bg = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(400, 600),
                    [SKColor.FromHsl(hue, 70, 55), SKColor.FromHsl((hue + 40) % 360, 60, 20)],
                    SKShaderTileMode.Clamp)
            })
            {
                surface.Canvas.DrawRect(0, 0, 400, 600, bg);
            }

            using var font = new SKFont(SKTypeface.Default, 220);
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha(200), IsAntialias = true };
            surface.Canvas.DrawText((i + 1).ToString(), 200, 380, SKTextAlign.Center, font, paint);

            var path = Path.Combine(_posterDir, $"poster{i}.jpg");
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
            File.WriteAllBytes(path, data.ToArray());
            paths.Add(path);
        }

        return paths;
    }
}

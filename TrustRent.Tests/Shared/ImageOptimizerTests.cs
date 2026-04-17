using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TrustRent.Shared.Services;

namespace TrustRent.Tests.Shared;

public class ImageOptimizerTests
{
    private static Stream CreateTestImage(int width, int height)
    {
        var image = new Image<Rgba32>(width, height);
        var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task OptimizeAsync_SmallImage_ReturnsWebpStream()
    {
        using var input = CreateTestImage(800, 600);

        var result = await ImageOptimizer.OptimizeAsync(input);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        Assert.Equal(0, result.Position); // Position reset to 0
    }

    [Fact]
    public async Task OptimizeAsync_LargeImage_ResizesToMaxWidth()
    {
        using var input = CreateTestImage(3000, 2000);

        var result = await ImageOptimizer.OptimizeAsync(input, maxWidth: 1920);

        // Verify output is valid WebP by loading it
        result.Position = 0;
        using var outputImage = await Image.LoadAsync(result);
        Assert.True(outputImage.Width <= 1920);
    }

    [Fact]
    public async Task OptimizeAsync_CustomMaxWidth_RespectsLimit()
    {
        using var input = CreateTestImage(2000, 1500);

        var result = await ImageOptimizer.OptimizeAsync(input, maxWidth: 1024);

        result.Position = 0;
        using var outputImage = await Image.LoadAsync(result);
        Assert.True(outputImage.Width <= 1024);
    }

    [Fact]
    public async Task OptimizeAsync_ImageBelowMaxWidth_DoesNotResize()
    {
        using var input = CreateTestImage(500, 400);

        var result = await ImageOptimizer.OptimizeAsync(input, maxWidth: 1920);

        result.Position = 0;
        using var outputImage = await Image.LoadAsync(result);
        Assert.Equal(500, outputImage.Width);
        Assert.Equal(400, outputImage.Height);
    }

    [Fact]
    public async Task OptimizeAsync_MaintainsAspectRatio()
    {
        using var input = CreateTestImage(3840, 2160); // 16:9

        var result = await ImageOptimizer.OptimizeAsync(input, maxWidth: 1920);

        result.Position = 0;
        using var outputImage = await Image.LoadAsync(result);
        Assert.Equal(1920, outputImage.Width);
        Assert.Equal(1080, outputImage.Height); // 16:9 maintained
    }
}

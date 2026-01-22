using DepthClockWallpaper.Models;
using SkiaSharp;

namespace DepthClockWallpaper.Core;

/// <summary>
/// The Compositor is the artist. It layers the clock onto the wallpaper,
/// applying the depth mask to create the illusion of the clock existing
/// within the three-dimensional space of the photograph.
/// </summary>
public class Compositor : IDisposable
{
    private SKTypeface? _typeface;
    private bool _disposed;

    private readonly AppConfig _config;

    public Compositor(AppConfig config)
    {
        _config = config;
        Console.WriteLine("Initializing Compositor...");

        // Parse font style
        var fontStyle = ParseFontStyle(_config.Clock.Style.FontStyle);
        _typeface = SKTypeface.FromFamilyName(_config.Clock.Style.FontFamily, fontStyle);

        Console.WriteLine($"✓ Compositor initialized with {_config.Clock.Style.FontFamily} {_config.Clock.Style.FontStyle}");
    }

    /// <summary>
    /// Renders a complete frame with the clock composited into the scene.
    /// </summary>
    /// <param name="original">The original wallpaper image</param>
    /// <param name="depthMask">The foreground mask (white = in front of clock)</param>
    /// <param name="timeText">The time string to render</param>
    /// <param name="debugPath">Optional path to save debug images for troubleshooting</param>
    public SKBitmap RenderFrame(SKBitmap original, SKBitmap depthMask, string timeText, string? debugPath = null)
    {
        Console.WriteLine($"Compositing frame: wallpaper + clock + foreground mask");

        var info = new SKImageInfo(original.Width, original.Height);
        var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        // Step 1: Draw the base wallpaper
        canvas.DrawBitmap(original, 0, 0);
        Console.WriteLine("✓ Drew base wallpaper layer");

        if (debugPath != null)
        {
            SaveDebugImage(original, debugPath, "1_wallpaper_only");
        }

        // Step 2: Create a temporary surface for the clock
        using var clockSurface = SKSurface.Create(info);
        using var clockCanvas = clockSurface.Canvas;

        // Clear clock surface with transparent background
        clockCanvas.Clear(SKColors.Transparent);

        // Draw clock on transparent surface
        DrawClock(clockCanvas, timeText, original.Width, original.Height);
        Console.WriteLine("✓ Drew clock on transparent surface");

        if (debugPath != null)
        {
            SaveDebugImage(clockSurface, debugPath, "2_clock_only");
        }

        // Step 3: Apply clock to main canvas (clock goes under foreground mask)
        canvas.DrawSurface(clockSurface, 0, 0);
        Console.WriteLine("✓ Drew clock on wallpaper (clock visible)");

        if (debugPath != null)
        {
            using var snapshotImage = surface.Snapshot();
            SaveDebugImage(SKBitmap.FromImage(snapshotImage), debugPath, "3_wallpaper_plus_clock");
        }

        // Step 4: Apply foreground mask to hide clock behind objects
        // Check if mask has any foreground pixels (non-transparent)
        bool hasForegroundPixels = HasMaskAnyForegroundPixels(depthMask);
        if (hasForegroundPixels)
        {
            ApplyForegroundMask(canvas, original, depthMask, debugPath);
            Console.WriteLine("✓ Applied foreground mask");
        }
        else
        {
            Console.WriteLine("✓ No foreground detected, clock rendered without masking");
        }

        var result = SKBitmap.FromImage(surface.Snapshot());
        surface.Dispose();

        Console.WriteLine("✓ Frame compositing complete");
        return result;
    }

    private static void SaveDebugImage(SKBitmap bitmap, string basePath, string name)
    {
        try
        {
            Directory.CreateDirectory(basePath);
            var path = Path.Combine(basePath, $"{name}.png");
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
            Console.WriteLine($"[DEBUG] Saved: {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Failed to save {name}: {ex.Message}");
        }
    }

    private static void SaveDebugImage(SKSurface surface, string basePath, string name)
    {
        using var image = surface.Snapshot();
        SaveDebugImage(SKBitmap.FromImage(image), basePath, name);
    }

    /// <summary>
    /// Checks if the mask has any foreground pixels (non-transparent).
    /// Returns true if masking should be applied.
    /// </summary>
    private static bool HasMaskAnyForegroundPixels(SKBitmap mask)
    {
        if (mask == null || mask.IsEmpty)
            return false;

        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                if (mask.GetPixel(x, y).Alpha > 0)
                    return true;
            }
        }
        return false;
    }

    private SKFontStyle ParseFontStyle(string fontStyle)
    {
        return fontStyle.ToLower() switch
        {
            "bold" => SKFontStyle.Bold,
            "italic" => SKFontStyle.Italic,
            "bolditalic" => SKFontStyle.BoldItalic,
            _ => SKFontStyle.Normal
        };
    }

    private SKColor ParseColor(string colorString)
    {
        if (colorString.StartsWith("#"))
        {
            return SKColor.Parse(colorString);
        }

        return colorString.ToLower() switch
        {
            "white" => SKColors.White,
            "black" => SKColors.Black,
            "red" => SKColors.Red,
            "green" => SKColors.Green,
            "blue" => SKColors.Blue,
            _ => SKColors.White
        };
    }

    /// <summary>
    /// Draws the clock text with a subtle shadow for depth.
    /// </summary>
    private void DrawClock(SKCanvas canvas, string timeText, int width, int height)
    {
        using var paint = new SKPaint
        {
            Color = ParseColor(_config.Clock.Style.Color),
            TextSize = CalculateOptimalTextSize(width),
            IsAntialias = true,
            Typeface = _typeface,
            ImageFilter = SKImageFilter.CreateDropShadow(
                0, 6, 12, 12,
                SKColors.Black.WithAlpha(160)
            )
        };

        // Measure text bounds for centering
        var bounds = new SKRect();
        paint.MeasureText(timeText, ref bounds);

        // Position using both horizontal and vertical from config
        float x = width * _config.Clock.Position.Horizontal - bounds.Width / 2 - bounds.Left;

        // Position vertically - verticalPosition is from top (0.0 = top, 1.0 = bottom)
        float y = height * _config.Clock.Position.Vertical;

        Console.WriteLine($"Clock position: X={x:F1}, Y={y:F1}, TextSize={paint.TextSize:F1}");
        Console.WriteLine($"Text bounds: Width={bounds.Width:F1}, Height={bounds.Height:F1}");
        Console.WriteLine($"Time text: '{timeText}'");

        canvas.DrawText(timeText, x, y, paint);
    }

    /// <summary>
    /// Applies the foreground mask to hide clock behind foreground objects.
    /// </summary>
    private void ApplyForegroundMask(SKCanvas canvas, SKBitmap original, SKBitmap depthMask, string? debugPath = null)
    {
        // Resize mask to match original dimensions if needed
        SKBitmap mask = depthMask;
        if (depthMask.Width != original.Width || depthMask.Height != original.Height)
        {
            mask = depthMask.Resize(
                new SKImageInfo(original.Width, original.Height),
                SKFilterQuality.High
            );
        }

        if (debugPath != null)
        {
            SaveDebugImage(mask, debugPath, "4_raw_mask");
        }

        // Apply Gaussian blur for soft edges (the "atmospheric" quality)
        var blurredMask = ApplyGaussianBlur(mask, _config.Depth.MaskBlur);

        if (debugPath != null)
        {
            SaveDebugImage(blurredMask, debugPath, "4a_blurred_mask");
        }

        // Create temporary surface for foreground with premultiplied alpha
        // This is critical for proper alpha blending with DstIn blend mode
        var foregroundInfo = new SKImageInfo(original.Width, original.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var foregroundSurface = SKSurface.Create(foregroundInfo);
        using var foregroundCanvas = foregroundSurface.Canvas;

        // Clear with transparent to ensure clean alpha channel
        foregroundCanvas.Clear(SKColors.Transparent);

        // Draw the original wallpaper onto temporary surface
        foregroundCanvas.DrawBitmap(original, 0, 0);

        if (debugPath != null)
        {
            SaveDebugImage(foregroundSurface, debugPath, "4b_wallpaper_before_mask");
        }

        // Apply mask to the temporary surface (this cuts out the foreground areas)
        // DstIn keeps destination pixels where source (mask) has alpha
        using (var maskPaint = new SKPaint())
        {
            maskPaint.BlendMode = SKBlendMode.DstIn;
            foregroundCanvas.DrawBitmap(blurredMask, 0, 0, maskPaint);
        }

        if (debugPath != null)
        {
            SaveDebugImage(foregroundSurface, debugPath, "4c_masked_foreground");
        }

        // Now draw the masked foreground ON TOP of the clock layer
        // This will hide the clock behind foreground objects
        canvas.DrawSurface(foregroundSurface, 0, 0);

        blurredMask.Dispose();

        if (mask != depthMask)
            mask.Dispose();
    }

    /// <summary>
    /// Applies Gaussian blur to soften mask edges.
    /// </summary>
    private SKBitmap ApplyGaussianBlur(SKBitmap source, float sigma)
    {
        var info = new SKImageInfo(source.Width, source.Height);
        var surface = SKSurface.Create(info);

        using (var paint = new SKPaint())
        {
            paint.ImageFilter = SKImageFilter.CreateBlur(sigma, sigma);
            surface.Canvas.DrawBitmap(source, 0, 0, paint);
        }

        var result = SKBitmap.FromImage(surface.Snapshot());
        surface.Dispose();

        return result;
    }

    /// <summary>
    /// Calculates optimal text size based on screen width.
    /// </summary>
    private float CalculateOptimalTextSize(int screenWidth)
    {
        // Scale text size: 200px for 1920px width (larger and more visible)
        return screenWidth / 9.6f;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _typeface?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

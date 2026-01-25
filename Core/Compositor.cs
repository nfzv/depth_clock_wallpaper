using DepthClockWallpaper.Models;
using Microsoft.Extensions.Options;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DepthClockWallpaper.Core;

/// <summary>
/// The Compositor is the artist. It layers the clock onto the wallpaper,
/// applying the depth mask to create the illusion of the clock existing
/// within the three-dimensional space of the photograph.
/// </summary>
public class Compositor(IOptionsMonitor<AppConfig> config)
{

    /// <summary>
    /// Renders a complete frame with the clock composited into the scene.
    /// </summary>
    /// <param name="original">The original wallpaper image</param>
    /// <param name="depthMask">The foreground mask (white = in front of clock)</param>
    /// <param name="timeText">The time string to render</param>
    /// <param name="debugPath">Optional path to save debug images for troubleshooting</param>
    public SKBitmap RenderFrame(SKBitmap original, SKBitmap depthMask, string timeText)
    {
        Console.WriteLine($"Compositing frame: wallpaper + clock + foreground mask");

        var info = new SKImageInfo(original.Width, original.Height);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        var debugPath = config.CurrentValue.Performance.EnableDebugMode ? config.CurrentValue.Performance.DebugPath : null;

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
        DrawClock(clockCanvas, timeText, original.Width, original.Height, depthMask);
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
            SaveDebugImage(surface, debugPath, "3_wallpaper_plus_clock");
        }

        // Step 4: Apply foreground mask to hide clock behind objects
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
    private void DrawClock(SKCanvas canvas, string timeText, int width, int height, SKBitmap? foregroundMask)
    {
        // Parse font style
        var fontStyle = ParseFontStyle(config.CurrentValue.Clock.Style.FontStyle);
        using var typeface = SKTypeface.FromFamilyName(config.CurrentValue.Clock.Style.FontFamily, fontStyle);
        using var font = new SKFont(typeface, CalculateOptimalTextSize(width));

        using var paint = new SKPaint
        {
            Color = ParseColor(config.CurrentValue.Clock.Style.Color),
            IsAntialias = true,
            ImageFilter = SKImageFilter.CreateDropShadow(
                0, 6, 12, 12,
                SKColors.Black.WithAlpha(160)
            )
        };

        var bounds = new SKRect();
        font.MeasureText(timeText, out bounds);

        float x, y;

        if (config.CurrentValue.Clock.Position.AutoEnabled && foregroundMask != null && !foregroundMask.IsEmpty)
        {
            var (h, v) = CalculateOptimalPosition(
                foregroundMask, width, height, bounds,
                config.CurrentValue.Clock.Position.MaxCoveragePercent);

            x = width * h - bounds.Width / 2 - bounds.Left;
            y = height * v;

            Console.WriteLine($"[Auto] Position: H={h:P0}, V={v:P0}");
        }
        else
        {
            x = width * config.CurrentValue.Clock.Position.Horizontal - bounds.Width / 2 - bounds.Left;
            y = height * config.CurrentValue.Clock.Position.Vertical;
        }

        Console.WriteLine($"Clock position: X={x:F1}, Y={y:F1}, TextSize={font.Size:F1}");
        Console.WriteLine($"Text bounds: Width={bounds.Width:F1}, Height={bounds.Height:F1}");
        Console.WriteLine($"Time text: '{timeText}'");

        canvas.DrawText(timeText, x, y, SKTextAlign.Center, font, paint);
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
                SKSamplingOptions.Default
            );
        }

        if (debugPath != null)
        {
            SaveDebugImage(mask, debugPath, "4_raw_mask");
        }

        // Apply Gaussian blur for soft edges (the "atmospheric" quality)
        var blurredMask = ApplyGaussianBlur(mask, config.CurrentValue.Depth.MaskBlur);

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
    /// Gets the configured text size for the clock.
    /// </summary>
    private float CalculateOptimalTextSize(int screenWidth)
    {
        return screenWidth / config.CurrentValue.Clock.Style.FontSize;
    }


    private (float horizontal, float vertical) CalculateOptimalPosition(
        SKBitmap foregroundMask, int screenWidth, int screenHeight,
        SKRect clockBounds, float maxCoveragePercent)
    {
        var candidates = new[]
        {
            (0.25f, 0.25f), (0.50f, 0.25f), (0.85f, 0.25f),
            (0.25f, 0.50f), (0.50f, 0.50f), (0.85f, 0.50f),
            (0.25f, 0.85f), (0.50f, 0.85f), (0.85f, 0.85f)
        };

        var results = new List<(float h, float v, float coverage)>();

        foreach (var (h, v) in candidates)
        {
            float coverage = CalculateCoverageAt(foregroundMask, h, v, clockBounds, screenWidth, screenHeight);
            results.Add((h, v, coverage));
        }

        return config.CurrentValue.Clock.Position.Strategy switch
        {
            EPositionStrategy.EdgesFirst => FindBestEdgeFirst(results, maxCoveragePercent),
            EPositionStrategy.SmartHybrid => FindSmartHybrid(results, maxCoveragePercent),
            EPositionStrategy.LowestCoverage or _ => (
                results.OrderBy(r => r.coverage).First().h,
                results.OrderBy(r => r.coverage).First().v
            )
        };
    }

    private (float h, float v) FindBestEdgeFirst(
        List<(float h, float v, float coverage)> results, float maxCoveragePercent)
    {
        var edgePositions = new[] { 0, 2, 6, 8 };
        var centerPositions = new[] { 4 };

        foreach (var idx in edgePositions)
        {
            if (results[idx].coverage <= maxCoveragePercent)
                return (results[idx].h, results[idx].v);
        }

        foreach (var idx in centerPositions)
        {
            if (results[idx].coverage <= maxCoveragePercent)
                return (results[idx].h, results[idx].v);
        }

        var best = results.OrderBy(r => r.coverage).First();
        return (best.h, best.v);
    }

    private (float h, float v) FindSmartHybrid(
        List<(float h, float v, float coverage)> results, float maxCoveragePercent)
    {
        var corners = new[] { 0, 2, 6, 8 };
        var edges = new[] { 1, 3, 5, 7 };
        var center = 4;

        foreach (var idx in corners)
        {
            if (results[idx].coverage <= maxCoveragePercent)
                return (results[idx].h, results[idx].v);
        }

        foreach (var idx in edges)
        {
            if (results[idx].coverage <= maxCoveragePercent)
                return (results[idx].h, results[idx].v);
        }

        if (results[center].coverage <= maxCoveragePercent)
            return (results[center].h, results[center].v);

        var best = results.OrderBy(r => r.coverage).First();
        return (best.h, best.v);
    }

    private float CalculateCoverageAt(SKBitmap mask, float horizontal, float vertical,
        SKRect clockBounds, int screenWidth, int screenHeight)
    {
        if (mask.Width == 0 || mask.Height == 0)
            return 0;

        int x = (int)(screenWidth * horizontal - clockBounds.Width / 2 - clockBounds.Left);
        int y = (int)(screenHeight * vertical);

        int marginX = (int)(clockBounds.Width * 0.2);
        int marginY = (int)(clockBounds.Height * 0.2);

        int startX = Math.Max(0, x - marginX);
        int startY = Math.Max(0, (int)(y - clockBounds.Height - marginY));
        int endX = Math.Min(screenWidth, x + (int)clockBounds.Width + marginX);
        int endY = Math.Min(screenHeight, y + marginY);

        int totalPixels = (endX - startX) * (endY - startY);
        if (totalPixels <= 0) return 0;

        int foregroundPixels = 0;
        for (int py = startY; py < endY; py++)
        {
            for (int px = startX; px < endX; px++)
            {
                if (px >= 0 && px < mask.Width && py >= 0 && py < mask.Height)
                {
                    if (mask.GetPixel(px, py).Alpha > 128)
                        foregroundPixels++;
                }
            }
        }

        return (float)foregroundPixels / totalPixels;
    }
}

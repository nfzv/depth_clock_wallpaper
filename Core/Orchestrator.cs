using DepthClockWallpaper.Models;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Timer = System.Timers.Timer;

namespace DepthClockWallpaper.Core;

public class Orchestrator(IOptionsMonitor<AppConfig> configuration, DepthEngine depthEngine, Compositor compositor)
{
    private Timer? _clockTimer;

    public void UpdateWallpaper()
    {
        // Determine source image based on mode
        string sourceImagePath = configuration.CurrentValue.Wallpaper.Mode is EWallpaperMode.Bing
            ? WallpaperPaths.BingWallpaper
            : WallpaperPaths.CustomWallpaper;

        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException($"Source wallpaper not found: {sourceImagePath}");

        Console.WriteLine($"Loading wallpaper in {configuration.CurrentValue.Wallpaper.Mode} mode from: {sourceImagePath}");

        // Load the new wallpaper
        using var newWallpaperOriginal = SKBitmap.Decode(sourceImagePath);
        if (newWallpaperOriginal == null)
        {
            throw new InvalidOperationException($"Failed to decode image: {sourceImagePath}");
        }

        Console.WriteLine($"✓ Wallpaper loaded: {newWallpaperOriginal.Width}x{newWallpaperOriginal.Height}");

        // Extract the depth mask (this is the heavy operation)
        Console.WriteLine("Extracting depth map...");
        using var foregroundMask = depthEngine.ExtractForegroundMask(newWallpaperOriginal);

        // Get a clock
        var timeText = DateTime.Now.ToString(configuration.CurrentValue.Clock.Format);

        // Render new frame with current config
        var debugPath = configuration.CurrentValue.Performance.EnableDebugMode
            ? configuration.CurrentValue.Performance.DebugPath
            : null;
        using var clockedFrame = compositor.RenderFrame(
            newWallpaperOriginal,
            foregroundMask,
            timeText
        );

        // Save the active wallpaper to temp folder
        try
        {
            using var image = SKImage.FromBitmap(clockedFrame);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            using var stream = File.OpenWrite(WallpaperPaths.ActiveWallpaper);
            data.SaveTo(stream);
            Console.WriteLine($"✓ Active wallpaper saved: {WallpaperPaths.ActiveWallpaper}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to save active wallpaper: {ex.Message}");
        }

        WallpaperSetter.SetWallpaper(WallpaperPaths.ActiveWallpaper);
        Console.WriteLine("✓ Depth extraction complete.");

    }

    public void Start()
    {

        Console.WriteLine($"Starting clock with hot-reload support in {configuration.CurrentValue.Wallpaper.Mode} mode...");

        // Calculate delay to next minute boundary
        var now = DateTime.Now;
        var nextMinute = now.AddSeconds(60 - now.Second).AddMilliseconds(-now.Millisecond);
        var delay = (nextMinute - now).TotalMilliseconds;

        Console.WriteLine($"Starting clock sync. Next update in {delay:F0}ms");
        Console.WriteLine($"Source image: {(configuration.CurrentValue.Wallpaper.Mode == EWallpaperMode.Bing ? WallpaperPaths.BingWallpaper : WallpaperPaths.CustomWallpaper)}");
        Console.WriteLine($"Output image: {WallpaperPaths.ActiveWallpaper}");

        // Stop existing timer
        _clockTimer?.Stop();

        // Use a task to handle the initial delay, then start the timer
        Task.Delay((int)delay).ContinueWith(_ =>
        {
            UpdateWallpaper();

            // Now start the timer with configured interval
            _clockTimer = new Timer(configuration.CurrentValue.Performance.UpdateInterval);
            _clockTimer.Elapsed += (s, e) => UpdateWallpaper();
            _clockTimer.AutoReset = true;
            _clockTimer.Start();

            Console.WriteLine($"✓ Clock timer started ({configuration.CurrentValue.Performance.UpdateInterval}ms interval)");
        }).ConfigureAwait(false);
    }
}
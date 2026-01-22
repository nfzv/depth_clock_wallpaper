using DepthClockWallpaper.Models;
using SkiaSharp;
using Timer = System.Timers.Timer;

namespace DepthClockWallpaper.Core;

/// <summary>
/// Hot-reloadable wallpaper orchestrator
/// </summary>
public class HotWallpaperOrchestrator : IDisposable
{
    private DepthEngine? _depthEngine;
    private Compositor? _compositor;
    private Timer? _clockTimer;
    private SKBitmap? _originalWallpaper;
    private SKBitmap? _foregroundMask;
    private SKBitmap? _currentFrame;
    private SKBitmap? _lastRenderedFrame;
    private bool _disposed;
    private readonly object _loadLock = new object();
    private bool _isLoading = false;
    public AppConfig CurrentConfig;

    public event EventHandler<SKBitmap>? FrameUpdated;

    public HotWallpaperOrchestrator()
    {
        CurrentConfig = HotConfigManager.Current;
        HotConfigManager.ConfigChanged += OnConfigurationChanged;

        InitializeComponents();
    }

    private void OnConfigurationChanged(AppConfig newConfig)
    {
        Console.WriteLine("🔄 Configuration changed, hot-reloading...");
        CurrentConfig = newConfig;

        // Reinitialize components that depend on config
        ReinitializeComponents();

        // If we have a wallpaper loaded, re-render with new settings
        if (_originalWallpaper != null)
        {
            // Re-extract depth mask with new depth engine settings
            Console.WriteLine("Re-extracting depth map with new config...");
            float? threshold = CurrentConfig.Depth.Threshold == "" ? null : CurrentConfig.Depth.ThresholdPercentile;
            _foregroundMask?.Dispose();
            _foregroundMask = _depthEngine!.ExtractForegroundMask(_originalWallpaper, threshold);
            Console.WriteLine("✓ Depth re-extraction complete.");

            RenderCurrentFrame();
        }

        Console.WriteLine("✓ Hot-reload complete");
    }

    private void InitializeComponents()
    {
        Console.WriteLine("Initializing orchestrator components...");

        // Initialize depth engine
        _depthEngine?.Dispose();
        _depthEngine = new DepthEngine(CurrentConfig.Model.Path, CurrentConfig);

        // Initialize compositor
        _compositor?.Dispose();
        _compositor = new Compositor(CurrentConfig);

        // Check for debug mode
        if (!string.IsNullOrEmpty(CurrentConfig.Performance.DebugPath))
        {
            Console.WriteLine($"[DEBUG] Debug mode enabled. Saving intermediate images to: {CurrentConfig.Performance.DebugPath}");
        }
        else if (Environment.GetEnvironmentVariable("DEPTHCLOCK_DEBUG_PATH") is { } envDebugPath)
        {
            Console.WriteLine($"[DEBUG] Debug mode enabled (via env var). Saving intermediate images to: {envDebugPath}");
        }

        Console.WriteLine("✓ Components initialized");
    }

    private void ReinitializeComponents()
    {
        Console.WriteLine("Reinitializing for new config...");

        _depthEngine?.Dispose();
        _depthEngine = new DepthEngine(CurrentConfig.Model.Path, CurrentConfig);
        Console.WriteLine($"✓ Depth engine reinitialized: {CurrentConfig.Model.Path}");

        _compositor?.Dispose();
        _compositor = new Compositor(CurrentConfig);
        Console.WriteLine("✓ Compositor reinitialized");
    }

    public void LoadWallpaper()
    {
        lock (_loadLock)
        {
            if (_isLoading)
            {
                Console.WriteLine("LoadWallpaper already in progress, skipping...");
                return;
            }

            _isLoading = true;
        }

        try
        {
            // Determine source image based on mode
            string sourceImagePath = CurrentConfig.Wallpaper.Mode is EWallpaperMode.Bing
                ? WallpaperPaths.BingWallpaper
                : WallpaperPaths.CustomWallpaper;

            if (!File.Exists(sourceImagePath))
                throw new FileNotFoundException($"Source wallpaper not found: {sourceImagePath}");

            Console.WriteLine($"Loading wallpaper in {CurrentConfig.Wallpaper.Mode} mode from: {sourceImagePath}");

            // Dispose previous resources
            _originalWallpaper?.Dispose();
            _foregroundMask?.Dispose();
            _currentFrame?.Dispose();

            // Load the new wallpaper
            _originalWallpaper = SKBitmap.Decode(sourceImagePath);

            if (_originalWallpaper == null)
                throw new InvalidOperationException($"Failed to decode image: {sourceImagePath}");

            Console.WriteLine($"✓ Wallpaper loaded: {_originalWallpaper.Width}x{_originalWallpaper.Height}");

            // Extract the depth mask (this is the heavy operation)
            Console.WriteLine("Extracting depth map...");
            float? threshold = CurrentConfig.Depth.Threshold == "" ? null : CurrentConfig.Depth.ThresholdPercentile;
            _foregroundMask = _depthEngine!.ExtractForegroundMask(_originalWallpaper, threshold);
            Console.WriteLine("✓ Depth extraction complete.");

            // Render the initial frame
            RenderCurrentFrame();
        }
        finally
        {
            lock (_loadLock)
            {
                _isLoading = false;
            }
        }
    }

    public void Start()
    {
        if (_originalWallpaper == null)
            throw new InvalidOperationException("No wallpaper loaded. Call LoadWallpaper() first.");

        Console.WriteLine($"Starting clock with hot-reload support in {CurrentConfig.Wallpaper.Mode} mode...");

        // Calculate delay to next minute boundary
        var now = DateTime.Now;
        var nextMinute = now.AddSeconds(60 - now.Second).AddMilliseconds(-now.Millisecond);
        var delay = (nextMinute - now).TotalMilliseconds;

        Console.WriteLine($"Starting clock sync. Next update in {delay:F0}ms");
        Console.WriteLine($"Source image: {(CurrentConfig.Wallpaper.Mode == EWallpaperMode.Bing ? WallpaperPaths.BingWallpaper : WallpaperPaths.CustomWallpaper)}");
        Console.WriteLine($"Output image: {WallpaperPaths.ActiveWallpaper}");

        // Stop existing timer
        _clockTimer?.Stop();

        // Use a task to handle the initial delay, then start the timer
        Task.Delay((int)delay).ContinueWith(_ =>
        {
            RenderCurrentFrame();

            // Now start the timer with configured interval
            _clockTimer = new System.Timers.Timer(CurrentConfig.Performance.UpdateInterval);
            _clockTimer.Elapsed += (s, e) => RenderCurrentFrame();
            _clockTimer.AutoReset = true;
            _clockTimer.Start();

            Console.WriteLine($"✓ Clock timer started ({CurrentConfig.Performance.UpdateInterval}ms interval)");
        });
    }

    public void Stop()
    {
        Console.WriteLine("Stopping clock timer...");

        _clockTimer?.Stop();
        _clockTimer?.Dispose();
        _clockTimer = null;

        Console.WriteLine("✓ Clock timer stopped.");
    }

    /// <summary>
    /// Renders the current frame with the current time.
    /// Uses hot-reload configuration.
    /// </summary>
    private void RenderCurrentFrame()
    {
        if (_originalWallpaper == null || _foregroundMask == null || _compositor == null)
            return;

        var timeText = DateTime.Now.ToString(CurrentConfig.Clock.Format);
        Console.WriteLine($"Rendering frame: {timeText}");

        // Dispose previous frame
        _currentFrame?.Dispose();

        // Render new frame with current config
        var debugPath = !string.IsNullOrEmpty(CurrentConfig.Performance.DebugPath)
            ? CurrentConfig.Performance.DebugPath
            : null;
        _currentFrame = _compositor.RenderFrame(
            _originalWallpaper,
            _foregroundMask,
            timeText,
            debugPath
        );

        // Save the active wallpaper to temp folder
        try
        {
            using var image = SKImage.FromBitmap(_currentFrame);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            using var stream = File.OpenWrite(WallpaperPaths.ActiveWallpaper);
            data.SaveTo(stream);
            Console.WriteLine($"✓ Active wallpaper saved: {WallpaperPaths.ActiveWallpaper}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to save active wallpaper: {ex.Message}");
        }

        // Check if frame actually changed before updating
        bool frameChanged = !AreFramesEqual(_lastRenderedFrame, _currentFrame);

        if (frameChanged)
        {
            _lastRenderedFrame?.Dispose();
            _lastRenderedFrame = _currentFrame.Copy();

            // Update the desktop wallpaper
            try
            {
                WallpaperSetter.SetWallpaper(WallpaperPaths.ActiveWallpaper);

                Console.WriteLine("✓ Desktop wallpaper updated");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to update desktop wallpaper: {ex.Message}");
            }

            // Notify listeners (for any other subscribers)
            FrameUpdated?.Invoke(this, _currentFrame);
        }
        else
        {
            Console.WriteLine("Frame unchanged, skipping update");
        }
    }

    private bool AreFramesEqual(SKBitmap? frame1, SKBitmap? frame2)
    {
        if (frame1 == null || frame2 == null)
            return false;

        if (frame1.Width != frame2.Width || frame1.Height != frame2.Height)
            return false;

        // Simple pixel comparison (could optimize this)
        for (int y = 0; y < frame1.Height; y++)
        {
            for (int x = 0; x < frame1.Width; x++)
            {
                if (frame1.GetPixel(x, y) != frame2.GetPixel(x, y))
                    return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        Stop();

        _depthEngine?.Dispose();
        _compositor?.Dispose();
        _originalWallpaper?.Dispose();
        _foregroundMask?.Dispose();
        _currentFrame?.Dispose();
        _lastRenderedFrame?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
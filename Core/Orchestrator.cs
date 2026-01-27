using DepthClockWallpaper.Models;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Timer = System.Timers.Timer;

namespace DepthClockWallpaper.Core;

public class Orchestrator(IOptionsMonitor<AppConfig> configuration, DepthEngine depthEngine, Compositor compositor) : IDisposable
{
    private Timer? _clockTimer;
    private Timer? _sessionCleanupTimer;
    private readonly CacheManager _cacheManager = new();
    private bool _disposed;

    /// <summary>
    /// Fired when cache generation progress changes.
    /// </summary>
    public event EventHandler<CacheProgressEventArgs>? CacheGenerationProgress;

    /// <summary>
    /// Raised when cache generation progress updates.
    /// </summary>
    private void OnCacheProgress(string status, int progressPercent, bool isComplete = false)
    {
        CacheGenerationProgress?.Invoke(this, new CacheProgressEventArgs
        {
            Status = status,
            ProgressPercent = progressPercent,
            IsComplete = isComplete
        });
    }

    /// <summary>
    /// Invalidates the cache, forcing a full regeneration on the next update.
    /// Call this when depth-related or rendering settings change.
    /// </summary>
    public void InvalidateCache()
    {
        _cacheManager.InvalidateCache();
        Console.WriteLine("🗑️ Cache invalidated - next update will regenerate all layers");
    }

    /// <summary>
    /// Checks if cache should be invalidated based on config changes.
    /// </summary>
    public static bool ShouldInvalidateCache(AppConfig oldConfig, AppConfig newConfig)
    {
        // Invalidate if depth settings changed
        if (oldConfig.Depth.ThresholdPercentile != newConfig.Depth.ThresholdPercentile ||
            oldConfig.Depth.MaskBlur != newConfig.Depth.MaskBlur ||
            oldConfig.Depth.Threshold != newConfig.Depth.Threshold)
        {
            return true;
        }

        // Invalidate if wallpaper mode or path changed
        if (oldConfig.Wallpaper.Mode != newConfig.Wallpaper.Mode ||
            oldConfig.Wallpaper.Path != newConfig.Wallpaper.Path)
        {
            return true;
        }

        // Invalidate if model settings changed
        if (oldConfig.Model.InputSize != newConfig.Model.InputSize ||
            oldConfig.Model.Path != newConfig.Model.Path)
        {
            return true;
        }

        // Invalidate if auto-positioning settings changed (affects mask generation)
        if (oldConfig.Clock.Position.AutoEnabled != newConfig.Clock.Position.AutoEnabled ||
            oldConfig.Clock.Position.MaxCoveragePercent != newConfig.Clock.Position.MaxCoveragePercent ||
            oldConfig.Clock.Position.Strategy != newConfig.Clock.Position.Strategy)
        {
            return true;
        }

        return false;
    }

    public void UpdateWallpaper()
    {
        var startTime = DateTime.Now;

        // Determine source image based on mode
        string sourceImagePath = configuration.CurrentValue.Wallpaper.Mode is EWallpaperMode.Bing
            ? WallpaperPaths.BingWallpaper
            : WallpaperPaths.CustomWallpaper;

        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException($"Source wallpaper not found: {sourceImagePath}");

        Console.WriteLine($"Loading wallpaper in {configuration.CurrentValue.Wallpaper.Mode} mode from: {sourceImagePath}");

        // Compute config hash for cache validation
        var configHash = CacheManager.ComputeConfigHash(configuration.CurrentValue);

        // Check if cache is valid
        bool cacheValid = configuration.CurrentValue.Performance.CacheDepthMask
            && _cacheManager.IsCacheValid(sourceImagePath, configHash);

        if (cacheValid)
        {
            // ====== FAST PATH: Use cached layers ======
            Console.WriteLine("🚀 [FAST PATH] Using cached layers (no inference needed)");
            UpdateWallpaperFastPath(sourceImagePath);

            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            Console.WriteLine($"✓ Wallpaper update complete in {elapsed:F0}ms (FAST PATH)");
        }
        else
        {
            // ====== SLOW PATH: Run inference and cache results ======
            Console.WriteLine("🐢 [SLOW PATH] Running inference and caching results...");
            UpdateWallpaperSlowPath(sourceImagePath, configHash);

            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            Console.WriteLine($"✓ Wallpaper update complete in {elapsed:F0}ms (SLOW PATH - cached for future)");
        }
    }

    /// <summary>
    /// Fast path: Uses cached depth masks and only renders the clock layer.
    /// Expected time: ~30-50ms (20-60x faster than slow path).
    /// </summary>
    private void UpdateWallpaperFastPath(string sourceImagePath)
    {
        // Load cached layers
        var cachedWallpaper = _cacheManager.GetCachedWallpaper();
        var cachedBlurredMask = _cacheManager.GetCachedBlurredMask();

        if (cachedWallpaper == null || cachedBlurredMask == null)
        {
            Console.WriteLine("⚠️ Cache incomplete, falling back to slow path");
            var configHash = CacheManager.ComputeConfigHash(configuration.CurrentValue);
            UpdateWallpaperSlowPath(sourceImagePath, configHash);
            return;
        }

        try
        {
            // Get current time
            var timeText = DateTime.Now.ToString(configuration.CurrentValue.Clock.Format);

            // Render only the clock layer (very fast)
            using var clockLayer = compositor.RenderClockLayer(
                cachedWallpaper.Width,
                cachedWallpaper.Height,
                timeText,
                cachedBlurredMask);

            // Composite layers together (fast)
            using var finalFrame = compositor.CompositeLayers(cachedWallpaper, clockLayer, cachedBlurredMask);

            // Save and set wallpaper
            SaveAndSetWallpaper(finalFrame);

            // Clean up cached bitmaps
            cachedWallpaper.Dispose();
            cachedBlurredMask.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fast path failed: {ex.Message}");
            cachedWallpaper?.Dispose();
            cachedBlurredMask?.Dispose();

            // Fall back to slow path
            var configHash = CacheManager.ComputeConfigHash(configuration.CurrentValue);
            UpdateWallpaperSlowPath(sourceImagePath, configHash);
        }
    }

    /// <summary>
    /// Slow path: Runs full inference pipeline and caches results for future use.
    /// Expected time: ~1-3 seconds (only runs when wallpaper or config changes).
    /// </summary>
    private void UpdateWallpaperSlowPath(string sourceImagePath, string configHash)
    {
        OnCacheProgress("Loading wallpaper...", 10);

        // Load the new wallpaper
        using var newWallpaperOriginal = SKBitmap.Decode(sourceImagePath);
        if (newWallpaperOriginal == null)
        {
            throw new InvalidOperationException($"Failed to decode image: {sourceImagePath}");
        }

        Console.WriteLine($"✓ Wallpaper loaded: {newWallpaperOriginal.Width}x{newWallpaperOriginal.Height}");
        OnCacheProgress("Running depth inference...", 30);

        // Extract the depth mask (this is the heavy operation)
        Console.WriteLine("Extracting depth map...");
        using var foregroundMask = depthEngine.ExtractForegroundMask(newWallpaperOriginal);
        OnCacheProgress("Creating blurred mask...", 70);

        // Create blurred mask for caching
        Console.WriteLine("Creating blurred mask...");
        using var blurredMask = compositor.CreateBlurredMask(
            foregroundMask,
            newWallpaperOriginal.Width,
            newWallpaperOriginal.Height);
        OnCacheProgress("Saving cache...", 85);

        // Cache the layers for future use
        if (configuration.CurrentValue.Performance.CacheDepthMask)
        {
            Console.WriteLine("Caching layers for future use...");
            _cacheManager.SaveToCache(foregroundMask, blurredMask, newWallpaperOriginal, sourceImagePath, configHash);
        }

        OnCacheProgress("Rendering final frame...", 95);

        // Get current time
        var timeText = DateTime.Now.ToString(configuration.CurrentValue.Clock.Format);

        // Render frame using traditional method
        var debugPath = configuration.CurrentValue.Performance.EnableDebugMode
            ? configuration.CurrentValue.Performance.DebugPath
            : null;
        using var clockedFrame = compositor.RenderFrame(
            newWallpaperOriginal,
            foregroundMask,
            timeText
        );

        // Save and set wallpaper
        SaveAndSetWallpaper(clockedFrame);
        OnCacheProgress("Complete!", 100, isComplete: true);
    }

    /// <summary>
    /// Saves the final frame and sets it as wallpaper.
    /// </summary>
    private void SaveAndSetWallpaper(SKBitmap frame)
    {
        try
        {
            using var image = SKImage.FromBitmap(frame);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            using var stream = File.OpenWrite(WallpaperPaths.ActiveWallpaper);
            data.SaveTo(stream);
            Console.WriteLine($"✓ Active wallpaper saved: {WallpaperPaths.ActiveWallpaper}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to save active wallpaper: {ex.Message}");
            throw;
        }

        WallpaperSetter.SetWallpaper(WallpaperPaths.ActiveWallpaper);
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

            // Start session cleanup timer (checks every 60 seconds)
            StartSessionCleanupTimer();
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts a background timer that periodically checks if the ONNX session should be disposed.
    /// This is key to reducing idle memory usage.
    /// </summary>
    private void StartSessionCleanupTimer()
    {
        var keepAliveMinutes = configuration.CurrentValue.Performance.SessionKeepAliveMinutes;
        
        // Don't start cleanup timer if session should be kept forever
        if (keepAliveMinutes == -1)
        {
            Console.WriteLine("✓ Session cleanup timer disabled (SessionKeepAliveMinutes=-1, kept forever)");
            return;
        }

        // Check every 60 seconds if session should be disposed
        _sessionCleanupTimer = new Timer(60000);
        _sessionCleanupTimer.Elapsed += (s, e) => 
        {
            try
            {
                depthEngine.CleanupExpiredSession();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Session cleanup error: {ex.Message}");
            }
        };
        _sessionCleanupTimer.AutoReset = true;
        _sessionCleanupTimer.Start();

        Console.WriteLine($"✓ Session cleanup timer started (checks every 60s, expires after {keepAliveMinutes} min idle)");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _clockTimer?.Stop();
        _clockTimer?.Dispose();
        _sessionCleanupTimer?.Stop();
        _sessionCleanupTimer?.Dispose();
        _cacheManager?.Dispose();

        Console.WriteLine("✓ Orchestrator disposed");
    }
}

/// <summary>
/// Event arguments for cache generation progress.
/// </summary>
public class CacheProgressEventArgs : EventArgs
{
    public string Status { get; init; } = string.Empty;
    public int ProgressPercent { get; init; }
    public bool IsComplete { get; init; }
}
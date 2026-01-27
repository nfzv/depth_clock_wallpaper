using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaSharp;

namespace DepthClockWallpaper.Core;

/// <summary>
/// Manages caching of depth masks and blurred masks to avoid redundant inference.
/// Implements layer caching strategy for dramatic performance improvement.
/// </summary>
public sealed class CacheManager : IDisposable
{
    private readonly string _cacheDirectory;
    private CacheMetadata? _currentMetadata;
    private bool _disposed;

    public CacheManager(string? customCacheDir = null)
    {
        _cacheDirectory = customCacheDir ?? Path.Combine(Path.GetTempPath(), "DepthClockWallpaper", "cache");
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// Cache directory path
    /// </summary>
    public string CacheDirectory => _cacheDirectory;

    /// <summary>
    /// Checks if the cache is valid for the given wallpaper and config hash.
    /// </summary>
    public bool IsCacheValid(string wallpaperPath, string configHash)
    {
        try
        {
            var metadataPath = GetMetadataPath();
            if (!File.Exists(metadataPath))
                return false;

            var metadata = LoadMetadata();
            if (metadata == null)
                return false;

            // Check if wallpaper hash matches
            var currentWallpaperHash = ComputeWallpaperHash(wallpaperPath);
            if (metadata.WallpaperHash != currentWallpaperHash)
                return false;

            // Check if config hash matches
            if (metadata.ConfigHash != configHash)
                return false;

            // Check if all cache files exist
            if (!File.Exists(GetDepthMaskPath()) || !File.Exists(GetBlurredMaskPath()))
                return false;

            _currentMetadata = metadata;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache] Validation failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the cached depth mask, or null if not available.
    /// </summary>
    public SKBitmap? GetCachedDepthMask()
    {
        try
        {
            var path = GetDepthMaskPath();
            if (!File.Exists(path))
                return null;

            var bitmap = SKBitmap.Decode(path);
            if (bitmap == null)
            {
                // Corrupted file, delete and return null
                Console.WriteLine($"[Cache] Corrupted depth mask, deleting: {path}");
                File.Delete(path);
                return null;
            }

            Console.WriteLine($"[Cache] ✓ Loaded cached depth mask ({bitmap.Width}x{bitmap.Height})");
            return bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache] Failed to load depth mask: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the cached blurred mask, or null if not available.
    /// </summary>
    public SKBitmap? GetCachedBlurredMask()
    {
        try
        {
            var path = GetBlurredMaskPath();
            if (!File.Exists(path))
                return null;

            var bitmap = SKBitmap.Decode(path);
            if (bitmap == null)
            {
                // Corrupted file, delete and return null
                Console.WriteLine($"[Cache] Corrupted blurred mask, deleting: {path}");
                File.Delete(path);
                return null;
            }

            Console.WriteLine($"[Cache] ✓ Loaded cached blurred mask ({bitmap.Width}x{bitmap.Height})");
            return bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache] Failed to load blurred mask: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the cached source wallpaper bitmap, or null if not available.
    /// </summary>
    public SKBitmap? GetCachedWallpaper()
    {
        try
        {
            var path = GetWallpaperCachePath();
            if (!File.Exists(path))
                return null;

            var bitmap = SKBitmap.Decode(path);
            if (bitmap == null)
            {
                Console.WriteLine($"[Cache] Corrupted wallpaper cache, deleting: {path}");
                File.Delete(path);
                return null;
            }

            Console.WriteLine($"[Cache] ✓ Loaded cached wallpaper ({bitmap.Width}x{bitmap.Height})");
            return bitmap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache] Failed to load wallpaper cache: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Saves masks and wallpaper to cache with metadata.
    /// </summary>
    public void SaveToCache(SKBitmap depthMask, SKBitmap blurredMask, SKBitmap wallpaper, 
        string wallpaperPath, string configHash)
    {
        try
        {
            Console.WriteLine($"[Cache] Saving cache to: {_cacheDirectory}");

            // Save depth mask (lossless PNG)
            SaveBitmapAsPng(depthMask, GetDepthMaskPath());

            // Save blurred mask (lossless PNG)
            SaveBitmapAsPng(blurredMask, GetBlurredMaskPath());

            // Save wallpaper (optimized JPEG - 85 quality is visually identical but ~40% smaller)
            SaveBitmapAsJpeg(wallpaper, GetWallpaperCachePath(), 85);

            // Save metadata
            var metadata = new CacheMetadata
            {
                WallpaperHash = ComputeWallpaperHash(wallpaperPath),
                ConfigHash = configHash,
                Timestamp = DateTime.UtcNow,
                WallpaperWidth = wallpaper.Width,
                WallpaperHeight = wallpaper.Height
            };

            SaveMetadata(metadata);
            _currentMetadata = metadata;

            Console.WriteLine($"[Cache] ✓ Cache saved successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache] Failed to save cache: {ex.Message}");
            CrashLogger.Log(ex);
        }
    }

    /// <summary>
    /// Invalidates the cache by deleting all cached files.
    /// </summary>
    public void InvalidateCache()
    {
        try
        {
            Console.WriteLine($"[Cache] Invalidating cache...");

            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, recursive: true);
                Directory.CreateDirectory(_cacheDirectory);
            }

            _currentMetadata = null;
            Console.WriteLine($"[Cache] ✓ Cache invalidated");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache] Failed to invalidate cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Computes a fast hash of the wallpaper file for change detection.
    /// Uses file size, modification time, and content samples for speed.
    /// </summary>
    public string ComputeWallpaperHash(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return string.Empty;

            var info = new FileInfo(filePath);
            using var stream = File.OpenRead(filePath);

            // Read first and last 4KB (or less if file is smaller)
            var headerSize = Math.Min(4096, (int)info.Length);
            var header = new byte[headerSize];
            stream.Read(header, 0, headerSize);

            byte[] footer = Array.Empty<byte>();
            if (info.Length > 4096)
            {
                var footerSize = Math.Min(4096, (int)(info.Length - headerSize));
                footer = new byte[footerSize];
                stream.Seek(-footerSize, SeekOrigin.End);
                stream.Read(footer, 0, footerSize);
            }

            // Combine with metadata for unique hash
            var combined = $"{info.Length}|{info.LastWriteTimeUtc.Ticks}|{Convert.ToBase64String(header)}|{Convert.ToBase64String(footer)}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
            return Convert.ToBase64String(hashBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cache] Failed to compute wallpaper hash: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Computes a hash of relevant config settings that affect rendering.
    /// </summary>
    public static string ComputeConfigHash(Models.AppConfig config)
    {
        // Only hash the settings that affect depth mask generation and rendering
        var relevantSettings = new
        {
            config.Depth.ThresholdPercentile,
            config.Depth.MaskBlur,
            config.Depth.Threshold,
            config.Model.InputSize,
            AutoPosition = config.Clock.Position.AutoEnabled,
            TargetCoverage = config.Clock.Position.TargetCoveragePercent
        };

        var json = JsonSerializer.Serialize(relevantSettings);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(hashBytes);
    }

    private void SaveBitmapAsPng(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    private void SaveBitmapAsJpeg(SKBitmap bitmap, string path, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    private string GetMetadataPath() => Path.Combine(_cacheDirectory, "metadata.json");
    private string GetDepthMaskPath() => Path.Combine(_cacheDirectory, "depth_mask.png");
    private string GetBlurredMaskPath() => Path.Combine(_cacheDirectory, "blurred_mask.png");
    private string GetWallpaperCachePath() => Path.Combine(_cacheDirectory, "wallpaper_cache.jpg");

    private CacheMetadata? LoadMetadata()
    {
        try
        {
            var json = File.ReadAllText(GetMetadataPath());
            return JsonSerializer.Deserialize<CacheMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    private void SaveMetadata(CacheMetadata metadata)
    {
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetMetadataPath(), json);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }

    private class CacheMetadata
    {
        public string WallpaperHash { get; set; } = string.Empty;
        public string ConfigHash { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int WallpaperWidth { get; set; }
        public int WallpaperHeight { get; set; }
    }
}

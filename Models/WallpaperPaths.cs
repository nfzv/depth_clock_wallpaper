using System.IO;

namespace DepthClockWallpaper.Models;

/// <summary>
/// Centralized file paths for temp wallpaper management
/// </summary>
public static class WallpaperPaths
{
    private static readonly string TempFolder = Path.Combine(Path.GetTempPath(), "DepthClockWallpaper");
    
    // Ensure temp folder exists
    static WallpaperPaths()
    {
        Directory.CreateDirectory(TempFolder);
    }
    
    /// <summary>
    /// Temp folder directory
    /// </summary>
    public static string TempDirectory => TempFolder;
    
    /// <summary>
    /// Current active wallpaper with clock (what WallpaperManager uses)
    /// </summary>
    public static string ActiveWallpaper => Path.Combine(TempFolder, "DepthClockWallpaperActive.jpg");
    
    /// <summary>
    /// Bing wallpaper source image
    /// </summary>
    public static string BingWallpaper => Path.Combine(TempFolder, "DepthClockWallpaperBing.jpg");
    
    /// <summary>
    /// Custom wallpaper source image
    /// </summary>
    public static string CustomWallpaper => Path.Combine(TempFolder, "DepthClockWallpaperCustom.jpg");
}
using System;

namespace DepthClockWallpaper.Models;

/// <summary>
/// Configuration settings for DepthClockWallpaper
/// </summary>
public class AppConfig
{
    public ModelConfig Model { get; set; } = new();
    public WallpaperConfig Wallpaper { get; set; } = new();
    public ClockConfig Clock { get; set; } = new();
    public DepthConfig Depth { get; set; } = new();
    public PerformanceConfig Performance { get; set; } = new();
}

public class ModelConfig
{
    public string Path { get; set; } = "depth_anything_v2_small.onnx";
    public int InputSize { get; set; } = 1036;
    public bool UseGPU { get; set; } = true;
}

public class WallpaperConfig
{
    public string Path { get; set; } = "wallpaper.jpg";
    public EWallpaperMode Mode { get; set; } = EWallpaperMode.Bing;
    public bool AutoDetect { get; set; } = false;
    public DateTime? LastBingUpdate { get; set; }
}

public enum EWallpaperMode
{
    Custom,
    Bing
}

public enum EPositionStrategy
{
    LowestCoverage,
    EdgesFirst,
    SmartHybrid
}

public enum EDepthThresholdMode
{
    Manual,
    Auto
}

public class ClockConfig
{
    public string Format { get; set; } = "HH:mm";
    public PositionConfig Position { get; set; } = new();
    public ClockStyleConfig Style { get; set; } = new();
}

public class PositionConfig
{
    public float Vertical { get; set; } = 0.33f;
    public float Horizontal { get; set; } = 0.5f;
    public bool AutoEnabled { get; set; } = false;
    public float MaxCoveragePercent { get; set; } = 0.30f;
    public EPositionStrategy Strategy { get; set; } = EPositionStrategy.LowestCoverage;
}

public class ClockStyleConfig
{
    public string FontFamily { get; set; } = "Segoe UI";
    public string FontStyle { get; set; } = "Bold";
    public float FontSize { get; set; } = 9.6f;
    public string Color { get; set; } = "#FFFFFF";
    public string ShadowColor { get; set; } = "#000000";
    public float ShadowOpacity { get; set; } = 0.6f;
    public float ShadowBlur { get; set; } = 18.0f;
    public ShadowOffsetConfig ShadowOffset { get; set; } = new();
}

public class ShadowOffsetConfig
{
    public float X { get; set; } = 0.0f;
    public float Y { get; set; } = 6.0f;
}

public class DepthConfig
{
    public EDepthThresholdMode Threshold { get; set; } = EDepthThresholdMode.Manual;
    public float ThresholdPercentile { get; set; } = 0.30f;
    public float MaskBlur { get; set; } = 2.0f;
}

public class PerformanceConfig
{
    public int UpdateInterval { get; set; } = 60000;
    public bool CacheDepthMask { get; set; } = true;
    public string ExecutionProvider { get; set; } = "DirectML";
    public string DebugPath { get; set; } = "debug/";
    public bool EnableDebugMode { get; set; } = false;
    
    /// <summary>
    /// Custom cache directory path. If empty, uses default temp location.
    /// </summary>
    public string CacheDirectory { get; set; } = "";
    
    /// <summary>
    /// Whether to preload the ONNX session on startup (improves first-run performance).
    /// </summary>
    public bool PreloadSessionOnStartup { get; set; } = true;
    
    /// <summary>
    /// How long to keep the ONNX session in memory after last use (in minutes).
    /// Set to 0 to dispose immediately after each use (minimum memory).
    /// Set to -1 to keep forever (maximum performance, higher memory).
    /// Default is 5 minutes (balanced).
    /// </summary>
    public int SessionKeepAliveMinutes { get; set; } = 5;
}

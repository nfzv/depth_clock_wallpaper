using System.Text.Json.Serialization;

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
    [JsonPropertyName("path")]
    public string Path { get; set; } = "depth_anything_v2_small.onnx";

    [JsonPropertyName("inputSize")]
    public int InputSize { get; set; } = 1036;

    [JsonPropertyName("useGPU")]
    public bool UseGPU { get; set; } = true;
}

public class WallpaperConfig
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "wallpaper.jpg";

    [JsonPropertyName("mode")]
    public EWallpaperMode Mode { get; set; } = EWallpaperMode.Bing; // "custom" or "bing"

    [JsonPropertyName("autoDetect")]
    public bool AutoDetect { get; set; } = false;

    [JsonPropertyName("lastBingUpdate")]
    public DateTime? LastBingUpdate { get; set; }
}

public enum EWallpaperMode
{
    Custom,
    Bing
}

public class ClockConfig
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = "HH:mm";

    [JsonPropertyName("position")]
    public PositionConfig Position { get; set; } = new();

    [JsonPropertyName("style")]
    public ClockStyleConfig Style { get; set; } = new();
}

public class PositionConfig
{
    [JsonPropertyName("vertical")]
    public float Vertical { get; set; } = 0.33f;

    [JsonPropertyName("horizontal")]
    public float Horizontal { get; set; } = 0.5f;
}

public class ClockStyleConfig
{
    [JsonPropertyName("fontFamily")]
    public string FontFamily { get; set; } = "Segoe UI";

    [JsonPropertyName("fontStyle")]
    public string FontStyle { get; set; } = "Bold";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#FFFFFF";

    [JsonPropertyName("shadowColor")]
    public string ShadowColor { get; set; } = "#000000";

    [JsonPropertyName("shadowOpacity")]
    public float ShadowOpacity { get; set; } = 0.6f;

    [JsonPropertyName("shadowBlur")]
    public float ShadowBlur { get; set; } = 18.0f;

    [JsonPropertyName("shadowOffset")]
    public ShadowOffsetConfig ShadowOffset { get; set; } = new();
}

public class ShadowOffsetConfig
{
    [JsonPropertyName("x")]
    public float X { get; set; } = 0.0f;

    [JsonPropertyName("y")]
    public float Y { get; set; } = 6.0f;
}

public class DepthConfig
{
    [JsonPropertyName("threshold")]
    public string Threshold { get; set; } = "manual";

    [JsonPropertyName("thresholdPercentile")]
    public float ThresholdPercentile { get; set; } = 0.30f;

    [JsonPropertyName("maskBlur")]
    public float MaskBlur { get; set; } = 2.0f;
}

public class PerformanceConfig
{
    [JsonPropertyName("updateInterval")]
    public int UpdateInterval { get; set; } = 60000;

    [JsonPropertyName("cacheDepthMask")]
    public bool CacheDepthMask { get; set; } = true;

    [JsonPropertyName("executionProvider")]
    public string ExecutionProvider { get; set; } = "DirectML";

    [JsonPropertyName("debugPath")]
    public string? DebugPath { get; set; } = null;
}

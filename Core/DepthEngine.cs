using DepthClockWallpaper.Models;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace DepthClockWallpaper.Core;

/// <summary>
/// Runs depth inference using the Depth-Anything-V2 ONNX model
/// and produces foreground masks based on depth separation.
/// </summary>
public sealed class DepthEngine
{
    private static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
    private static readonly float[] Std = { 0.229f, 0.224f, 0.225f };

    private readonly IOptionsMonitor<AppConfig> _config;

    public DepthEngine(IOptionsMonitor<AppConfig> config)
    {
        _config = config;
        var modelPath = config.CurrentValue.Model.Path;
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"ONNX model not found at: {modelPath}");
    }

    /// <summary>
    /// Produces a soft foreground mask from an image.
    /// Caller owns the returned bitmap.
    /// </summary>
    public SKBitmap ExtractForegroundMask(SKBitmap image)
    {
        var depthMap = InferDepth(image);

        try
        {
            // Check if the depth map has significant variation
            if (!HasSignificantDepth(depthMap))
            {
                Console.WriteLine("⚠️ No significant depth detected in image, creating transparent mask (clock will be fully visible)");
                return CreateTransparentMask(image.Width, image.Height);
            }

            float threshold = _config.CurrentValue.Depth.Threshold
                is EDepthThresholdMode.Manual
                ? _config.CurrentValue.Depth.ThresholdPercentile : CalculateOptimalThreshold(depthMap);

            Console.WriteLine($"Depth threshold: {threshold:F4}");

            var mask = CreateForegroundMask(depthMap, threshold);

            // Debug: save depth map visualization if configured
            if (!string.IsNullOrEmpty(ExtractDebugPath()))
            {
                SaveDepthMapDebug(depthMap, threshold);
            }

            return mask;
        }
        finally
        {
            Array.Clear(depthMap, 0, depthMap.Length);
        }
    }

    /// <summary>
    /// Checks if the depth map has significant depth variation.
    /// Returns false if the image is essentially flat (no foreground objects).
    /// </summary>
    private static bool HasSignificantDepth(float[,] depthMap)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        float sum = 0;

        foreach (var value in depthMap)
        {
            if (value < min) min = value;
            if (value > max) max = value;
            sum += value;
        }

        // Check range (max - min) for meaningful depth variation
        float range = max - min;

        // Also check if the depth values are essentially uniform
        float mean = sum / depthMap.Length;
        float varianceSum = 0;
        foreach (var value in depthMap)
        {
            float diff = value - mean;
            varianceSum += diff * diff;
        }
        float stdDev = (float)Math.Sqrt(varianceSum / depthMap.Length);

        // Consider it significant if range > 0.01 or stdDev > 0.005
        bool hasSignificantRange = range > 0.01f;
        bool hasSignificantVariance = stdDev > 0.005f;

        Console.WriteLine($"[Depth Analysis] Range: {range:F6}, StdDev: {stdDev:F6}, HasSignificantDepth: {hasSignificantRange || hasSignificantVariance}");

        return hasSignificantRange || hasSignificantVariance;
    }

    /// <summary>
    /// Creates a fully transparent mask (no foreground objects detected).
    /// </summary>
    private static SKBitmap CreateTransparentMask(int width, int height)
    {
        var mask = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(mask);
        canvas.Clear(SKColors.Transparent);
        return mask;
    }

    private static string? ExtractDebugPath()
    {
        // Try to get debug path from environment variable
        var envPath = Environment.GetEnvironmentVariable("DEPTHCLOCK_DEBUG_PATH");
        return !string.IsNullOrEmpty(envPath) ? envPath : null;
    }

    private static void SaveDepthMapDebug(float[,] depthMap, float threshold)
    {
        var debugPath = ExtractDebugPath();
        if (debugPath == null) return;

        try
        {
            Directory.CreateDirectory(debugPath);

            int h = depthMap.GetLength(0);
            int w = depthMap.GetLength(1);

            // Find min/max for normalization
            float min = float.MaxValue, max = float.MinValue;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (depthMap[y, x] < min) min = depthMap[y, x];
                    if (depthMap[y, x] > max) max = depthMap[y, x];
                }
            }

            Console.WriteLine($"[DEBUG] Depth map range: {min:F4} to {max:F4}, threshold: {threshold:F4}");

            // Save as grayscale image
            var bitmap = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte value = (byte)(255 * (depthMap[y, x] - min) / (max - min));
                    bitmap.SetPixel(x, y, new SKColor(value, value, value, 255));
                }
            }

            var path = Path.Combine(debugPath, "0_depth_map.png");
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
            Console.WriteLine($"[DEBUG] Saved: {path}");
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
            Console.WriteLine($"[DEBUG] Failed to save depth map: {ex.Message}");
        }
    }

    // -------------------------
    // Depth inference
    // -------------------------

    private float[,] InferDepth(SKBitmap source)
    {
        using var resized = ResizeForModel(source);
        var inputTensor = CreateInputTensor(resized);

        // Load session
        var modelPath = _config.CurrentValue.Model.Path;
        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };

        if (_config.CurrentValue.Model.UseGPU)
        {
            options.AppendExecutionProvider_DML(0);
            Console.WriteLine("✓ DirectML GPU acceleration enabled");
        }
        else
        {
            Console.WriteLine("✓ CPU inference enabled");
        }

        using var session = new InferenceSession(modelPath, options);
        using var results = session.Run([
            NamedOnnxValue.CreateFromTensor("input", inputTensor)
        ]);

        var output = results[0].AsEnumerable<float>().ToArray();
        var inputSize = _config.CurrentValue.Model.InputSize;

        var depth518 = new float[inputSize, inputSize];
        for (int y = 0; y < inputSize; y++)
        {
            for (int x = 0; x < inputSize; x++)
            {
                depth518[y, x] = output[y * inputSize + x];
            }
        }

        return ResizeDepthMap(
            depth518,
            inputSize,
            inputSize,
            source.Height,
            source.Width
        );
    }

    private SKBitmap ResizeForModel(SKBitmap image)
    {
        if (image == null || image.IsEmpty)
            throw new InvalidOperationException("Invalid source bitmap");

        var inputSize = _config.CurrentValue.Model.InputSize;
        var resized = image.Resize(
            new SKImageInfo(inputSize, inputSize),
            SKSamplingOptions.Default
        );
        return resized ?? throw new InvalidOperationException("Image resize failed.");
    }

    private DenseTensor<float> CreateInputTensor(SKBitmap image)
    {
        var inputSize = _config.CurrentValue.Model.InputSize;
        var tensor = new DenseTensor<float>(
            [1, 3, inputSize, inputSize]
        );

        for (int y = 0; y < inputSize; y++)
        {
            for (int x = 0; x < inputSize; x++)
            {
                var p = image.GetPixel(x, y);

                tensor[0, 0, y, x] = Normalize(p.Red, Mean[0], Std[0]);
                tensor[0, 1, y, x] = Normalize(p.Green, Mean[1], Std[1]);
                tensor[0, 2, y, x] = Normalize(p.Blue, Mean[2], Std[2]);
            }
        }

        return tensor;
    }

    private static float Normalize(byte value, float mean, float std) =>
        (value / 255f - mean) / std;

    private static float[,] ResizeDepthMap(
        float[,] src,
        int srcH,
        int srcW,
        int dstH,
        int dstW)
    {
        var dst = new float[dstH, dstW];

        float scaleY = (float)srcH / dstH;
        float scaleX = (float)srcW / dstW;

        for (int y = 0; y < dstH; y++)
        {
            int sy = Math.Min((int)(y * scaleY), srcH - 1);
            for (int x = 0; x < dstW; x++)
            {
                int sx = Math.Min((int)(x * scaleX), srcW - 1);
                dst[y, x] = src[sy, sx];
            }
        }

        return dst;
    }

    // -------------------------
    // Depth analysis
    // -------------------------

    /// <summary>
    /// Finds a depth cutoff separating foreground from background
    /// using percentile-based histogram slicing.
    /// </summary>
    private float CalculateOptimalThreshold(float[,] depthMap)
    {
        int h = depthMap.GetLength(0);
        int w = depthMap.GetLength(1);

        var values = new float[h * w];
        int i = 0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                values[i++] = depthMap[y, x];
            }
        }

        Array.Sort(values);

        int index = (int)(values.Length * (1.0f - _config.CurrentValue.Depth.ThresholdPercentile));
        return values[index];
    }

    // -------------------------
    // Mask generation
    // -------------------------

    /// <summary>
    /// Creates a binary foreground mask from a depth map.
    /// Foreground pixels are white, background transparent.
    /// </summary>
    private static SKBitmap CreateForegroundMask(float[,] depthMap, float threshold)
    {
        int h = depthMap.GetLength(0);
        int w = depthMap.GetLength(1);

        var mask = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                byte alpha = depthMap[y, x] >= threshold ? (byte)255 : (byte)0;
                mask.SetPixel(x, y, new SKColor(255, 255, 255, alpha));
            }
        }

        return mask;
    }
}

# AGENTS.md

This document provides guidance for AI assistants and agents working on the DepthClockWallpaper codebase.

## Project Overview

DepthClockWallpaper is a Windows desktop application that renders a live clock on top of the desktop wallpaper with depth-aware masking. The app uses AI-powered depth estimation (Depth-Anything-V2) to create a foreground mask, making the clock appear behind closer objects while remaining visible in the background.

**Tech Stack:**
- .NET 8.0 Windows
- Windows Forms for UI
- Microsoft.ML.OnnxRuntime.DirectML for AI inference
- SkiaSharp for graphics rendering
- Depth-Anything-V2 Small ONNX model

## Codebase Guidelines

### Language & Framework Conventions

- **Target Framework**: .NET 8.0 Windows (`net8.0-windows`)
- **UI Framework**: Windows Forms with `UseWindowsForms=true`
- **C# Version**: Latest stable features available in .NET 8
- **Line Endings**: CRLF (Windows-style)

### Coding Standards

- **Naming Conventions**:
  - Classes: PascalCase (e.g., `DepthEngine`, `WallpaperSetter`)
  - Methods: PascalCase (e.g., `RenderFrame`, `SetWallpaper`)
  - Private fields: camelCase with underscore prefix (e.g., `_config`, `_depthEngine`)
  - Constants: UPPER_SNAKE_CASE (e.g., `DEFAULT_INPUT_SIZE`)

- **File Organization**:
  - One public class per file (filename matches class name)
  - Related classes in same file if tightly coupled (e.g., `Config.cs` contains multiple config classes)

- **XML Documentation**: Required for all public types and members

- **Import Organization**: System namespaces first, then third-party, then project-local

### Critical File Locations

| File | Purpose |
|------|---------|
| `Program.cs` | Application entry point |
| `Core/DepthEngine.cs` | ONNX model loading and inference |
| `Core/Compositor.cs` | Clock compositing with depth masking |
| `Core/HotWallpaperOrchestrator.cs` | Main orchestration with hot-reload |
| `Core/WallpaperSetter.cs` | Windows wallpaper setting |
| `Models/Config.cs` | Configuration schema |
| `UI/SettingsForm.cs` | Settings dialog |

## Development Workflow

### Building the Project

```bash
# Debug build
dotnet build --configuration Debug

# Release build
dotnet build --configuration Release

# Using PowerShell script
.\Scripts\compile.ps1
```

### Running During Development

```bash
dotnet run --configuration Debug
# Or using PowerShell script
.\Scripts\run.ps1
```

### Debug Output

Set the `DEPTHCLOCK_DEBUG_PATH` environment variable to a directory path to output intermediate images (depth masks, composited frames) for debugging.

```powershell
$env:DEPTHCLOCK_DEBUG_PATH = "C:\Temp\DepthClockDebug"
dotnet run
```

### Testing Approach

This project does not currently have a formal test suite. When adding tests:

- Unit tests go in a `Tests/` directory
- Use xUnit as the testing framework
- Mock ONNX runtime for depth engine tests
- Use temporary directories for file operations

## Key Technical Concepts

### ONNX Model Integration

The app uses an embedded ONNX model (`depth_anything_v2_small.onnx`) for depth estimation:

- **Model Source**: Depth-Anything-V2 Small variant
- **Encoder**: ViT-S (Vision Transformer Small)
- **Parameters**: 24.8M
- **Input Size**: Configurable (default 1036x1036)
- **Execution Provider**: DirectML (GPU) or CPU fallback

Model file is embedded as a resource and extracted to a temporary location at runtime.

### Depth Estimation Pipeline

1. Load source image from disk or fetch Bing wallpaper
2. Resize to model's input dimensions
3. Run ONNX inference to get depth map
4. Normalize depth map to 0-1 range
5. Apply threshold to create foreground mask
6. Apply Gaussian blur for soft edges
7. Use mask to hide clock behind foreground objects

### Foreground Mask Creation

```csharp
// Core logic in Compositor.cs
var depthMap = _depthEngine.ProcessImage(wallpaperImage);
var normalizedDepth = NormalizeDepth(depthMap);
var foregroundMask = CreateBinaryMask(normalizedDepth, thresholdPercentile: 0.30);
var blurredMask = ApplyGaussianBlur(foregroundMask, sigma: 2.0);
```

### Windows Wallpaper Setting

The app uses multiple fallback methods (in order):
1. `SystemParametersInfoW` SPI_SETDESKWALLPAPER
2. Registry modification + PowerShell refresh
3. Direct desktop window manipulation via WorkerW

### Configuration Hot-Reload

Changes to `config.json` trigger events via `HotConfigManager`:

1. File watcher detects config change
2. Parse new configuration
3. Fire `ConfigChanged` event
4. Subscribers update their state without restart

## Common Tasks

### Updating Configuration Schema

When adding new settings:

1. **Modify Models/Config.cs**: Add new property to `AppConfig` or related classes
2. **Update Defaults**: Modify `AppConfig.GetDefault()` to include new defaults
3. **Update UI**: Add controls to `UI/SettingsForm.cs`
4. **Update Serialization**: Ensure Newtonsoft.Json attributes are correct
5. **Document**: Update README.md if user-facing

### Adding New Settings

```csharp
// In Models/Config.cs
public class AppConfig {
    public ModelConfig Model { get; set; } = new ModelConfig();
    // Add new section
    public NewFeatureConfig NewFeature { get; set; } = new NewFeatureConfig();
}

public class NewFeatureConfig {
    public bool EnableFeature { get; set; } = true;
    public string SomeSetting { get; set; } = "default";
}
```

### Modifying Clock Rendering

Clock rendering logic is in `Core/Compositor.cs` in the `RenderClock` method. Key parameters:
- Position: `ClockConfig.Position` (normalized 0-1 coordinates)
- Font: `ClockConfig.Style.FontFamily`, `FontSize`, `FontStyle`
- Color: `ClockConfig.Style.Color` (hex string)
- Shadows: `ClockConfig.Style.ShadowBlur`, `ShadowColor`

### Changing Depth Threshold Logic

In `Core/Compositor.cs`, the `CreateForegroundMask` method:

```csharp
// Adjust threshold calculation
var threshold = depthValues.OrderBy(v => v).Skip(
    (int)(depthValues.Length * config.Depth.ThresholdPercentile)
).First();
```

### Adding New Wallpaper Sources

1. Create new service class in `Core/` (e.g., `WallpaperSourceService.cs`)
2. Implement `IWallpaperSource` interface (create if needed)
3. Add source selection to `BingWallpaperService.cs` or `HotWallpaperOrchestrator.cs`
4. Update `Models/Config.cs` with new enum value for wallpaper mode

## Important File Locations

| Path | Description |
|------|-------------|
| `%APPDATA%\DepthClockWallpaper\config.json` | User configuration file |
| `%TEMP%\DepthClockWallpaper\` | Temporary files (model extraction, frame output) |
| `depth_anything_v2_small.onnx` | Embedded ONNX model (build action: EmbeddedResource) |

### Configuration File Format

```json
{
  "Model": {
    "path": "depth_anything_v2_small.onnx",
    "inputSize": 1036,
    "useGPU": true
  },
  "Wallpaper": {
    "mode": "Bing",
    "path": ""
  },
  "Clock": {
    "format": "HH:mm",
    "position": { "horizontal": 0.5, "vertical": 0.33 },
    "style": {
      "fontFamily": "Segoe UI",
      "fontStyle": "Bold",
      "color": "#FFFFFF",
      "fontSize": 72,
      "shadowBlur": 8,
      "shadowColor": "#000000"
    }
  },
  "Depth": {
    "threshold": "auto",
    "thresholdPercentile": 0.30,
    "maskBlur": 2.0
  },
  "Performance": {
    "updateInterval": 60000,
    "cacheDepthMask": true,
    "executionProvider": "DirectML"
  }
}
```

## Build & Release

### Release Build Process

```powershell
# Clean and
dotnet clean --configuration Release
dotnet build release build --configuration Release --no-restore

# Create ZIP distribution (manual step)
# 1. Copy DepthClockWallpaper/bin/Release/net8.0-windows/ contents
# 2. Include depth_anything_v2_small.onnx
# 3. Zip all files
```

### Embedding Resources

The ONNX model is embedded as a resource:

```xml
<!-- In DepthClockWallpaper.csproj -->
<ItemGroup>
  <EmbeddedResource Include="depth_anything_v2_small.onnx" />
</ItemGroup>
```

To add more embedded resources, add `<EmbeddedResource Include="path\to\file" />` to the project file.

### Version Bumping

Version is defined in `DepthClockWallpaper.csproj`:

```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <!-- Update version here before release -->
</PropertyGroup>
```

## Troubleshooting

### Common Errors

**ONNX Runtime Initialization Failed**
- Ensure DirectML is available (Windows 10/11 with compatible GPU)
- Fallback to CPU by setting `"useGPU": false` in config

**Wallpaper Not Updating**
- Check file permissions in temp directory
- Verify desktop window is accessible
- Try running as administrator

**Depth Mask All Black/White**
- Check model file integrity
- Verify image dimensions are valid
- Review debug output with `DEPTHCLOCK_DEBUG_PATH`

### Debug Techniques

1. **Enable Debug Output**:
   ```powershell
   $env:DEPTHCLOCK_DEBUG_PATH = "C:\Temp\DepthClockDebug"
   ```

2. **Check Generated Files**: Look for intermediate images:
   - `wallpaper_original.png`: Source image
   - `depth_map.png`: Raw depth estimation
   - `foreground_mask.png`: Binary mask
   - `final_composite.png`: Rendered result

3. **Config Validation**: Run with invalid config to see validation errors in console output

### GPU/DirectML Issues

If DirectML fails, the app will log a warning and fall back to CPU. To force CPU-only mode:

```json
{
  "Model": {
    "useGPU": false
  }
}
```

Common GPU issues:
- Outdated GPU drivers
- Incompatible DirectX version
- Insufficient GPU memory

## Linting & Type Checking

This project does not currently use formal linting. When adding linting:

- Use `dotnet format` for .NET code style
- Consider `csharpier` or `sonarqube` for deeper analysis
- Add CI checks in GitHub Actions workflow

## Code Review Checklist

When reviewing changes:

- [ ] XML documentation added for new public APIs
- [ ] Configuration schema updated with proper defaults
- [ ] Error handling added for file I/O operations
- [ ] GPU/CPU fallback logic preserved
- [ ] No hardcoded paths (use `WallpaperPaths` class)
- [ ] No secrets or credentials in code
- [ ] Resource cleanup in `Dispose()` methods
- [ ] Thread safety considered for config hot-reload

## Performance Considerations

- **Model Inference**: Most expensive operation; cache results when possible
- **Image Resizing**: Use high-quality interpolation for depth maps
- **Memory**: Dispose bitmaps after use; avoid keeping multiple copies
- **GPU**: Minimize tensor allocations; reuse buffer objects
- **Wallpaper Updates**: Batch changes; avoid rapid successive updates

# AI Coding Agent Guide

This document provides instructions for AI coding assistants working on the DepthClockWallpaper codebase.

## Project Overview

DepthClockWallpaper is a Windows desktop application that renders a clock with a 3D depth effect on wallpapers. It uses AI-powered depth estimation (Depth-Anything-V2 ONNX model) to create the illusion that the clock exists *behind* foreground objects in the wallpaper image.

**Key Concept:** The app splits an image into foreground/background layers using depth estimation, places a clock layer in between, and composites them together.

## Tech Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Runtime | .NET 8.0 (Windows-specific) | net8.0-windows |
| UI Framework | Windows Forms | - |
| Graphics | SkiaSharp | 3.119.1 |
| AI/ML Inference | Microsoft.ML.OnnxRuntime.DirectML | 1.23.0 |
| DI/Config | Microsoft.Extensions.Hosting | 10.0.2 |
| Model | Depth-Anything-V2 Small | ViT-S, 24.8M params |
| Python Tools | uv, torch, onnx | For model export only |

## Architecture

### Core Classes (in order of importance)

| Class | File | Responsibility |
|-------|------|----------------|
| `Orchestrator` | `Core/Orchestrator.cs` | Main coordinator - timers, cache validation, wallpaper update flow |
| `DepthEngine` | `Core/DepthEngine.cs` | ONNX inference, depth map extraction, session management |
| `Compositor` | `Core/Compositor.cs` | SkiaSharp rendering, clock drawing, layer compositing |
| `CacheManager` | `Core/CacheManager.cs` | Depth mask caching for performance |
| `SettingsForm` | `UI/SettingsForm.cs` | Main UI, system tray, settings management |
| `WallpaperSetter` | `Core/WallpaperSetter.cs` | Windows wallpaper setting (multiple fallback methods) |
| `BingWallpaperService` | `Core/BingWallpaperService.cs` | Bing daily image fetching |

### Supporting Classes

| Class | File | Responsibility |
|-------|------|----------------|
| `AppConfig` | `Models/Config.cs` | All configuration model classes |
| `WallpaperPaths` | `Models/WallpaperPaths.cs` | Centralized temp file paths |
| `WritableJsonOptions<T>` | `Models/WritableJsonOptions.cs` | Hot-reload config writer with file locking |
| `CrashLogger` | `Core/CrashLogger.cs` | Centralized crash logging |
| `Win32` | `Core/Win32.cs` | Win32 API interop for wallpaper setting |

### Entry Point

`Program.cs` - Sets up DI container, global exception handling, and runs the application.

## File Structure

```
DepthClockWallpaper/
├── Program.cs                         # Entry point, DI setup
├── DepthClockWallpaper.csproj         # Project file
├── DepthClockWallpaper.sln            # Solution file
├── config.json                        # Runtime config (auto-created)
├── depth_anything_v2_small.onnx       # ONNX model file
├── depth_anything_v2_small.onnx.data  # ONNX model weights
├── icon.ico                           # Application icon
│
├── Core/                              # Core business logic
│   ├── Orchestrator.cs                # Main coordinator
│   ├── DepthEngine.cs                 # ONNX inference
│   ├── Compositor.cs                  # Image compositing
│   ├── CacheManager.cs                # Depth mask caching
│   ├── WallpaperSetter.cs             # Windows wallpaper API
│   ├── BingWallpaperService.cs        # Bing image fetching
│   ├── CrashLogger.cs                 # Error logging
│   └── Win32.cs                       # Win32 interop
│
├── Models/                            # Data models
│   ├── Config.cs                      # Configuration classes
│   ├── WallpaperPaths.cs              # Temp file paths
│   └── WritableJsonOptions.cs         # Config writer
│
├── UI/                                # Windows Forms UI
│   └── SettingsForm.cs                # Settings dialog + system tray
│
├── Scripts/                           # PowerShell scripts
│   ├── run.ps1                        # Quick start (build + run)
│   ├── compile.ps1                    # Full build/publish
│   └── install_depth_anything.ps1     # Model repo setup
│
├── Python/                            # Model export tools
│   ├── export_model.py                # PyTorch to ONNX export
│   ├── pyproject.toml                 # Python dependencies
│   └── uv.lock                        # Lock file
│
└── dist/                              # Build output
```

## Configuration System

### Config File: `config.json`
- Auto-created on first run with defaults
- Hot-reload supported via `IOptionsMonitor<AppConfig>`
- Writable via `WritableJsonOptions<AppConfig>` with file locking

### Config Structure (Models/Config.cs):

```
AppConfig
├── ModelConfig (Path, InputSize=1036, UseGPU=true)
├── WallpaperConfig (Mode=Bing, Path, LastBingUpdate)
├── ClockConfig
│   ├── Format (default: "HH:mm")
│   ├── PositionConfig (Horizontal, Vertical, AutoEnabled, TargetCoveragePercent=0.50)
│   └── ClockStyleConfig (FontFamily, FontStyle, FontSize, Color, Shadow*)
├── DepthConfig (ThresholdMode, ThresholdPercentile, MaskBlur)
└── PerformanceConfig
    ├── UpdateInterval (default: 60000ms)
    ├── CacheDepthMask (default: true)
    ├── SessionKeepAliveMinutes (default: 5)
    └── EnableDebugMode, DebugPath
```

### Key Enums:
- `EWallpaperMode`: Custom, Bing
- `EDepthThresholdMode`: Manual, Auto

## Rendering Pipeline

### Slow Path (cache miss, ~1-3s):
1. Load source wallpaper (Bing or Custom)
2. Run ONNX depth inference → `DepthEngine.ExtractForegroundMask()`
3. Create blurred mask → `Compositor.CreateBlurredMask()`
4. Cache masks for future use
5. Render clock layer → `Compositor.DrawClock()`
6. Composite layers: wallpaper + clock + foreground mask
7. Save and set as Windows wallpaper

### Fast Path (cache hit, ~30-50ms):
1. Load cached wallpaper and blurred mask
2. Render clock layer only
3. Composite and set wallpaper

## Key Patterns

1. **Dependency Injection** - All services registered in `Program.cs`
2. **Options Pattern** - `IOptionsMonitor<AppConfig>` for configuration
3. **IDisposable** - `DepthEngine`, `Orchestrator`, `CacheManager` implement proper disposal
4. **Async/Await** - UI operations use async to prevent blocking
5. **Thread Safety** - `DepthEngine` uses `_sessionLock` for ONNX session access

## Common Development Tasks

### Adding a new configuration option:
1. Add property to appropriate class in `Models/Config.cs`
2. Add UI control in `SettingsForm.InitializeComponent()`
3. Load value in `SettingsForm.LoadSettingsToUI()`
4. Save value in `SettingsForm.ApplySettings()`

### Modifying depth processing:
1. `DepthEngine.ExtractForegroundMask()` - Main entry point
2. `DepthEngine.CalculateOptimalThreshold()` - Threshold calculation
3. `DepthEngine.CreateForegroundMask()` - Mask generation

### Modifying clock rendering:
1. `Compositor.DrawClock()` - Clock text rendering
2. `Compositor.CalculateOptimalPosition()` - Auto-positioning algorithm (finds position closest to target coverage)
3. `Compositor.RenderFrame()` / `CompositeLayers()` - Full compositing

### Adding a new wallpaper source:
1. Add enum value to `EWallpaperMode` in `Models/Config.cs`
2. Create service class similar to `BingWallpaperService`
3. Update `Orchestrator.UpdateWallpaper()` to handle new mode
4. Add UI controls in `SettingsForm`

## Build & Run

### Quick Start:
```powershell
.\Scripts\run.ps1
```

### Manual Build:
```powershell
dotnet build -c Release
dotnet run -c Release
```

### Publish:
```powershell
.\Scripts\compile.ps1 -Configuration Release -Runtime win-x64
```

## Important Notes

### Memory Management
- ONNX session uses 150-500MB RAM
- Controlled via `SessionKeepAliveMinutes` (-1=forever, 0=immediate cleanup, default=5)
- `DepthEngine.CleanupExpiredSession()` called by orchestrator's cleanup timer

### GPU Requirements
- DirectML requires compatible GPU
- Falls back to CPU if GPU unavailable
- Toggle via `ModelConfig.UseGPU`

### File Locking
- Active wallpaper file may be locked by Windows
- `WallpaperSetter` tries 5 different methods for compatibility

### Temp Files (WallpaperPaths.cs)
- Base: `%TEMP%\DepthClockWallpaper\`
- `DepthClockWallpaperActive.jpg` - Current wallpaper (what Windows uses)
- `DepthClockWallpaperBing.jpg` - Downloaded Bing source
- `cache\` - Cached depth masks and metadata

## Debug Mode

Enable to save intermediate images:
1. Set `Performance.EnableDebugMode = true`
2. Set `Performance.DebugPath = "debug/"`

Debug outputs:
- `0_depth_map.png` - Raw depth estimation
- `1_wallpaper_only.png` - Base wallpaper
- `2_clock_only.png` - Clock on transparent background
- `3_wallpaper_plus_clock.png` - Before mask application
- `4_raw_mask.png` - Binary foreground mask
- `4a_blurred_mask.png` - Softened mask with blur

## Testing

No automated tests currently exist. All testing is manual.

Consider adding tests for:
- `DepthEngine` depth calculation logic
- `Compositor` position calculation algorithms
- `CacheManager` cache validation logic
- Configuration serialization/deserialization

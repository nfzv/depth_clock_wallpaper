# DepthClockWallpaper

A Windows desktop application that places your live clock within the three-dimensional depth of your wallpaper. Using AI-powered depth estimation, the app creates a stunning atmospheric effect where the clock appears to exist *behind* foreground objects while remaining visible in the background.

> **Vibe Coded Project**: The code is rather chaotic, sorry about that. You can blame 'Big Pickle' and partially 'MiniMax M2.1 Free' model for that. I just wanted to practice agentic coding using OpenCode 😁


## Examples

<img src="./Assets/Demo/mountain.jpg" width="256">
<img src="./Assets/Demo/palms.jpg" width="256">
<img src="./Assets/Demo/squirrel.jpg" width="256">

## Features

- **Depth-Aware Clock Placement**: The clock renders behind closer objects for a natural 3D effect
- **AI-Powered Depth Estimation**: Using Depth-Anything-V2 model
- **Real-Time Updates**: Live clock that updates every minute (configurable)
- **Customizable Clock**: Fonts, colors, shadows, position, and time format
- **Daily Bing Wallpapers**: Automatically fetches beautiful Bing homepage images
- **System Tray Operation**: Runs quietly in the background with minimal footprint

## How It Works

DepthClockWallpaper uses a depth estimation model Depth-Anything-V2 to split a given image into 2 layers (foreground and background objects). It places an image with clock in between those 2 layers. Saves the layered image in your Temp folder and updates your wallpaper. The frequency of updates and the threshold to determine the depth can be configured per your taste.

## Installation



2. **Setup**
   - Download the latest release
   - Extract the ZIP file to your desired location
   - Run `DepthClockWallpaper.exe`


3. **First Launch**
   - The app will appear in your system tray (bottom-right corner)
   - Right-click the tray icon to access settings
   - Configure your preferences and the app will set your wallpaper
   - It sets the app to always launch on startup (you can disable that in the settings)

## Usage

### System Tray

Right-click the DepthClockWallpaper icon in your system tray to access:
- **Settings**: Open the configuration dialog
- **Exit**: Close the application

### Settings

- **Wallpaper Mode**: Choose between custom images or daily Bing wallpapers
- **Clock Style**: Customize font, color, size, shadows, and position
- **Depth Settings**: Adjust foreground detection threshold and mask blur
- **Performance**: Toggle GPU acceleration and configure update intervals

## For Developers

1. **Prerequisites**
   - .NET 8.0
   - IDE of your choice: Visual Studio / Rider
   - Python (needed to build your onnx model file using `./Python/export_model.py`)
   - uv (for environment and package management)

### Folder Structure

```
DepthClockWallpaper/
├── Core/                          # Core business logic
│   ├── Compositor.cs             # Composites clock onto wallpaper with depth masking
│   ├── DepthEngine.cs            # AI depth estimation using ONNX model
│   ├── HotWallpaperOrchestrator.cs # Main orchestrator with hot-reload support
│   ├── HotConfigManager.cs       # Configuration management with event system
│   ├── WallpaperSetter.cs        # Windows wallpaper setting (multiple methods)
│   ├── BingWallpaperService.cs   # Fetches daily Bing homepage images
│   └── Win32.cs                  # Win32 API interop for WorkerW manipulation
├── Models/                        # Data models
│   ├── Config.cs                 # Configuration classes (AppConfig, ClockConfig, etc.)
│   └── WallpaperPaths.cs         # Centralized temp file paths
├── UI/                            # Windows Forms UI
│   ├── SettingsForm.cs           # Settings dialog with all controls
│   └── WallpaperForm.resx        # UI resources
├── Python/                        # Model export utilities
│   ├── export_model.py           # Exports Depth-Anything-V2 to ONNX format
│   └── pyproject.toml            # Python dependencies
├── Scripts/                       # PowerShell scripts
│   ├── run.ps1                   # Application runner
│   └── compile.ps1               # Build script
├── depth_anything_v2_small.onnx   # Embedded AI model (ONNX format)
├── DepthClockWallpaper.csproj    # .NET 8 project file
├── DepthClockWallpaper.sln       # Solution file
├── Program.cs                    # Entry point
└── icon.ico                      # Application icon
```

### Tech Stack

- **Runtime**: .NET 8.0
- **UI Framework**: Windows Forms
- **AI/ML**: Microsoft.ML.OnnxRuntime.DirectML 1.23.0
- **Graphics**: SkiaSharp 3.119.1
- **Model**: Depth-Anything-V2 Small (ViT-S encoder, 24.8M parameters)

### Building from Source

```bash
# Clone the repository
git clone https://github.com/nfzv/DepthClockWallpaper.git
cd DepthClockWallpaper

# Run the PowerShell script
.\Scripts\run.ps1

# The idea is to build the model from depth_anything_v2 repository and produce .onnx files. You can either run export_model.py directly or run.ps1 that internally calls install_depth_anything.ps1 to do the same.
# Once your model is near your executable or in root folder, you can run your app as usual e.g. dotnet run
```

## Potential Improvements

### 1. Resource Optimization

- Currently it uses ~400MBs of RAM in idle. So there is definitely a room to improvement. Maybe it keeps the model in memory, maybe we should unload it when not used?!

### 2. Alternative Models

- **Depth-Anything-V2** small with input_size set to 1036 by default - consumes a lot of resources. Anything less than that size makes the mask edges really pixelly. Very inefficient. Though the blurring helps a lot, I don't think that's the way to go. Perhaps there are other models that are more efficient that outputs a similar quality?!

- **Meta's Segment Anything Models**. Probably I don't know what I'm talking about or it would be really an overkill. I can imagine a model that could classify and label objects within a wallpaper and if we have a set of predefined object labels in code - we could place the clock in a more aesthetic position.


## FAQ

**Q: Does DepthClockWallpaper support dual monitors?**
A: Currently, the app sets the same wallpaper across all monitors. Multi-monitor support with per-monitor clock positioning is a potential improvement.

**Q: Can I use my own wallpapers instead of Bing images?**
A: Yes. In settings, switch to "Custom" mode and select your preferred image path.

**Q: Does this work with animated wallpapers?**
A: No, DepthClockWallpaper currently only supports static images. Animated wallpaper support is not planned but could be explored as a future enhancement. Depth-Anything-V2 does support video processing, though it's still theoretical. 

## License

MIT License
# DepthClockWallpaper Quick Start Script with UV support
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "DepthClockWallpaper Setup"
Write-Host "========================================"
Write-Host ""

# Check if UV is installed
if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
    Write-Host "UV package manager not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install UV first:"
    Write-Host "  powershell -c ""irm https://astral.sh/uv/install.ps1 | iex"""
    Write-Host ""
    Pause
    exit 1
}

# Check if ONNX model exists
if (-not (Test-Path "depth_anything_v2_small.onnx")) {
    Write-Host "[1/3] ONNX model not found. Exporting..." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Installing Python dependencies with UV..."
    uv sync
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nERROR: Failed to install dependencies." -ForegroundColor Red
        Pause
        exit 1
    }

    Write-Host "`nSetting up Depth-Anything-V2 package..."
    # Run the PowerShell installation script
    & .\Scripts\install_depth_anything.ps1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nERROR: Failed to setup Depth-Anything-V2." -ForegroundColor Red
        Write-Host "Please check DEPTH_ANYTHING_SETUP.md for manual setup instructions."
        Pause
        exit 1
    }

    Write-Host "`nExporting model..."
    uv run python Python/export_model.py
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nERROR: Failed to export model." -ForegroundColor Red
        Pause
        exit 1
    }
} else {
    Write-Host "[1/3] ONNX model found: depth_anything_v2_small.onnx" -ForegroundColor Green
    Write-Host ""
}

# Build the project
Write-Host "[2/3] Building project..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nERROR: Build failed." -ForegroundColor Red
    Pause
    exit 1
}
Write-Host ""


Write-Host "[3/3] Running DepthClockWallpaper..." -ForegroundColor Green
Write-Host ""
Write-Host "========================================"
Write-Host ""

# Run the application
if ($args.Count -eq 0) {
    dotnet run -c Release
} else {
    dotnet run -c Release -- $args[0]
}
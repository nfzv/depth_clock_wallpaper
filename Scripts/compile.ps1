<#
.SYNOPSIS
    Build script for DepthClockWallpaper - Compiles and outputs the executable
.DESCRIPTION
    This script builds the DepthClockWallpaper project and creates a portable release package
.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release
.PARAMETER OutputDir
    Output directory for the build. Default: .\dist
.PARAMETER Package
    Create a portable package with dependencies. Default: $true
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("win-x64", "win-x86")]
    [string]$Runtime = "win-x64",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputDir = ".\dist",
    
    [Parameter(Mandatory=$false)]
    [bool]$Package = $true
)

# Enhanced error handling
$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "DepthClockWallpaper Build Script" -ForegroundColor Cyan
Write-Host "========================================"
Write-Host ""

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path "bin") {
    Remove-Item -Path "bin" -Recurse -Force
}
if (Test-Path "obj") {
    Remove-Item -Path "obj" -Recurse -Force
}
if (Test-Path $OutputDir) {
    Remove-Item -Path $OutputDir -Recurse -Force
}
Write-Host "✓ Cleaned previous builds" -ForegroundColor Green
Write-Host ""

# Restore NuGet packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore --packages .nuget --runtime $Runtime
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ NuGet restore failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Packages restored" -ForegroundColor Green
Write-Host ""

# Build the project
Write-Host "Building project ($Configuration)..." -ForegroundColor Yellow
$buildArgs = @(
    "publish",
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--no-restore",
    "--verbosity", "minimal"
)

Write-Host "Running command: " + $buildArgs
& dotnet $buildArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build completed" -ForegroundColor Green
Write-Host ""

# Find the executable
$exePath = "bin\$($Configuration)\net8.0-windows\$($Runtime)\DepthClockWallpaper.exe"
if (Test-Path $exePath) {
    $exeInfo = Get-Item $exePath
    Write-Host "Executable found: $($exeInfo.FullName)" -ForegroundColor Green
    Write-Host "Size: $([math]::Round($exeInfo.Length / 1MB, 2)) MB" -ForegroundColor Gray
    Write-Host "Created: $($exeInfo.CreationTime)" -ForegroundColor Gray
} else {
    Write-Host "✗ Executable not found!" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Create output directory
Write-Host "Creating output directory..." -ForegroundColor Yellow
New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null
Write-Host "✓ Output directory created: $OutputDir" -ForegroundColor Green
Write-Host ""

# Copy executable and dependencies
if ($Package) {
    Write-Host "Creating portable package..." -ForegroundColor Yellow
    
    # Copy executable
    Copy-Item -Path $exePath -Destination $OutputDir -Force
    
    # Copy required files
    $requiredFiles = @(
        "config.example.json"
    )
    
    foreach ($file in $requiredFiles) {
        if (Test-Path $file) {
            Copy-Item -Path $file -Destination $OutputDir -Force
            Write-Host "✓ Copied: $file" -ForegroundColor Green
        }
    }
    
    # Create a launch script
    $launchScript = @"
# DepthClockWallpaper Launch Script
# This script helps you run the application with different options

Write-Host 'DepthClockWallpaper Launcher' -ForegroundColor Cyan
Write-Host '========================' -ForegroundColor Cyan
Write-Host ''

Write-Host '1. Run with UI (Recommended)'
Write-Host '2. Run in Console Mode'
Write-Host '3. Exit'
Write-Host ''

$choice = Read-Host 'Select option (1-3)'

switch ($choice) {
    '1' {
        Write-Host 'Launching with UI...' -ForegroundColor Green
        Start-Process '.\DepthClockWallpaper.exe' -ArgumentList '--ui'
    }
    '2' {
        Write-Host 'Launching in Console Mode...' -ForegroundColor Green
        $wallpaper = Read-Host 'Enter wallpaper path (or press Enter for default)'
        if ($wallpaper) {
            Start-Process '.\DepthClockWallpaper.exe' -ArgumentList "`"$wallpaper`""
        } else {
            Start-Process '.\DepthClockWallpaper.exe'
        }
    }
    '3' {
        Write-Host 'Exiting...' -ForegroundColor Yellow
        exit
    }
    default {
        Write-Host 'Invalid choice. Exiting...' -ForegroundColor Red
    }
"@
    
    $launchScript | Out-File -FilePath "$OutputDir\launch.ps1" -Encoding UTF8
    Write-Host "✓ Created launch script: launch.ps1" -ForegroundColor Green
    
} else {
    # Just copy the executable
    Copy-Item -Path $exePath -Destination $OutputDir -Force
    Write-Host "✓ Copied executable" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "Output location: $((Resolve-Path $OutputDir))" -ForegroundColor Cyan
Write-Host "Executable: $OutputDir\DepthClockWallpaper.exe" -ForegroundColor Cyan

if ($Package) {
    Write-Host ""
    Write-Host "Package Contents:" -ForegroundColor Gray
    Write-Host "  • DepthClockWallpaper.exe (main executable)" -ForegroundColor White
    Write-Host "  • config.example.json (configuration template)" -ForegroundColor White
    Write-Host "  • launch.ps1 (convenient launcher script)" -ForegroundColor White
    if (Test-Path "$OutputDir\depth_anything_v2_small.onnx") {
        Write-Host "  • depth_anything_v2_small.onnx (ONNX model)" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "To run the application:" -ForegroundColor Yellow
    Write-Host "  • UI Mode: .\DepthClockWallpaper.exe --ui" -ForegroundColor White
    Write-Host "  • Console: .\DepthClockWallpaper.exe wallpaper.jpg" -ForegroundColor White
    Write-Host "  • Launcher: .\launch.ps1" -ForegroundColor White
}

Write-Host "========================================" -ForegroundColor Cyan
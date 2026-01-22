#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Download and setup Depth-Anything-V2 source files.
    This script clones the entire repository from GitHub.
#>

$ErrorActionPreference = "Stop"

# GitHub repository URL
$RepoUrl = "https://github.com/DepthAnything/Depth-Anything-V2.git"
$TargetDir = "depth_anything_v2"

function Clone-Repository {
    <#
    .SYNOPSIS
        Clone the entire repository from GitHub.
    #>
    Write-Host "Cloning repository from $RepoUrl..."
    
    if (Test-Path $TargetDir) {
        Write-Host "$TargetDir directory already exists"
        return $true
    }
    
    try {
        # Clone the repository
        & git clone $RepoUrl $TargetDir
        if ($LASTEXITCODE -ne 0) {
            throw "Git clone failed with exit code $LASTEXITCODE"
        }
        Write-Host "  ✓ Repository cloned successfully" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "  ✗ Failed to clone repository: $_" -ForegroundColor Red
        return $false
    }
}

function Cleanup-Repository {
    <#
    .SYNOPSIS
        Remove unnecessary files from the cloned repository.
    #>
    Write-Host "Cleaning up repository..."
    
    # Files/directories to remove
    $cleanupItems = @(
        ".git",
        ".github", 
        "README.md",
        "LICENSE",
        "requirements.txt",
        "setup.py",
        "examples",
        "tests",
        "demo.py",
        "run.py"
    )
    
    foreach ($item in $cleanupItems) {
        $itemPath = Join-Path $TargetDir $item
        if (Test-Path $itemPath) {
            try {
                Remove-Item -Path $itemPath -Recurse -Force
                Write-Host "  ✓ Removed: $item" -ForegroundColor Green
            }
            catch {
                Write-Host "  ✗ Failed to remove ${item}: $_" -ForegroundColor Yellow
            }
        }
    }
}

function Main {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Depth-Anything-V2 Setup"
    Write-Host "========================================"
    Write-Host ""
    
    # Check if Git is available
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Write-Host "  ✗ Git not found. Please install Git first." -ForegroundColor Red
        Write-Host ""
        Write-Host "Download Git from: https://git-scm.com/download/win"
        return 1
    }
    
    # Clone the entire repository
    if (-not (Clone-Repository)) {
        return 1
    }
    
    Write-Host ""
    
    # Clean up unnecessary files
    Cleanup-Repository
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "✓ Depth-Anything-V2 setup complete!" -ForegroundColor Green
    Write-Host "✓ Entire repository installed in ./$TargetDir/" -ForegroundColor Green
    Write-Host "========================================"
    return 0
}

# Run the main function
exit (Main)
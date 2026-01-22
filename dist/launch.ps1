# DepthClockWallpaper Launch Script
# This script helps you run the application with different options

Write-Host 'DepthClockWallpaper Launcher' -ForegroundColor Cyan
Write-Host '========================' -ForegroundColor Cyan
Write-Host ''

Write-Host '1. Run with UI (Recommended)'
Write-Host '2. Run in Console Mode'
Write-Host '3. Exit'
Write-Host ''

 = Read-Host 'Select option (1-3)'

switch () {
    '1' {
        Write-Host 'Launching with UI...' -ForegroundColor Green
        Start-Process '.\DepthClockWallpaper.exe' -ArgumentList '--ui'
    }
    '2' {
        Write-Host 'Launching in Console Mode...' -ForegroundColor Green
         = Read-Host 'Enter wallpaper path (or press Enter for default)'
        if () {
            Start-Process '.\DepthClockWallpaper.exe' -ArgumentList """"
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

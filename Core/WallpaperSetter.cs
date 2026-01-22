using System.Runtime.InteropServices;
using System.Text;

namespace DepthClockWallpaper.Core;

/// <summary>
/// Robust wallpaper setting with proper string marshaling
/// </summary>
public static class WallpaperSetter
{
    // Import the Unicode version with explicit marshaling
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SystemParametersInfoW(
        int uiAction,
        int uiParam,
        [MarshalAs(UnmanagedType.LPTStr)]
        string pvParam,
        int fWinIni);

    // Alternative: Try Registry method
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(
        int uiAction,
        int uiParam,
        string pvParam,
        int fWinIni);

    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDWININICHANGE = 0x02;

    /// <summary>
    /// Sets desktop wallpaper using multiple methods for maximum compatibility
    /// </summary>
    public static bool SetWallpaper(string imagePath)
    {
        Console.WriteLine($"Setting wallpaper: {imagePath}");
        Console.WriteLine($"File exists: {File.Exists(imagePath)}");

        if (!File.Exists(imagePath))
        {
            Console.WriteLine("❌ Image file does not exist!");
            return false;
        }

        // Get absolute path
        string fullPath = Path.GetFullPath(imagePath);
        Console.WriteLine($"Full path: {fullPath}");

        // Method 1: Unicode SystemParametersInfo with proper marshaling
        bool success = TryUnicodeMethod(fullPath);

        // Method 2: ANSI SystemParametersInfo fallback
        if (!success)
        {
            Console.WriteLine("Unicode method failed, trying ANSI...");
            success = TryANSIMethod(fullPath);
        }

        // Method 3: Registry direct method
        if (!success)
        {
            Console.WriteLine("SystemParametersInfo failed, trying registry...");
            success = TryRegistryMethod(fullPath);
        }

        // Method 4: PowerShell .NET method (most reliable)
        if (!success)
        {
            Console.WriteLine("Registry method failed, trying PowerShell...");
            success = TryPowerShellMethod(fullPath);
        }

        // Method 5: Last resort - Windows API with StringBuilder
        if (!success)
        {
            Console.WriteLine("PowerShell failed, trying StringBuilder method...");
            success = TryStringBuilderMethod(fullPath);
        }

        if (success)
        {
            Console.WriteLine("✅ Wallpaper set successfully");

            // Force desktop refresh
            RefreshDesktop();
        }
        else
        {
            int error = Marshal.GetLastWin32Error();
            Console.WriteLine($"❌ All methods failed. Last error: {error}");
        }

        return success;
    }

    private static bool TryUnicodeMethod(string imagePath)
    {
        try
        {
            Console.WriteLine("🔧 Trying Unicode SystemParametersInfoW...");

            // Ensure the path is properly null-terminated
            // .NET marshaling with [MarshalAs(UnmanagedType.LPTStr)] should handle this
            int result = SystemParametersInfoW(
                SPI_SETDESKWALLPAPER,
                0,
                imagePath,
                SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE
            );

            Console.WriteLine($"Result: {result}");
            return result == 0; // SystemParametersInfo returns 0 on success
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unicode method exception: {ex.Message}");
            return false;
        }
    }

    private static bool TryANSIMethod(string imagePath)
    {
        try
        {
            Console.WriteLine("🔧 Trying ANSI SystemParametersInfo...");

            int result = SystemParametersInfo(
                SPI_SETDESKWALLPAPER,
                0,
                imagePath,
                SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE
            );

            Console.WriteLine($"Result: {result}");
            return result == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ANSI method exception: {ex.Message}");
            return false;
        }
    }

    private static bool TryRegistryMethod(string imagePath)
    {
        try
        {
            Console.WriteLine("🔧 Trying registry method...");

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Control Panel\Desktop", true);

            if (key != null)
            {
                key.SetValue("Wallpaper", imagePath);
                key.Close();

                // Notify system of change
                SystemParametersInfo(
                    SPI_SETDESKWALLPAPER,
                    0,
                    imagePath,
                    SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE
                );

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Registry method exception: {ex.Message}");
            return false;
        }
    }

    private static bool TryPowerShellMethod(string imagePath)
    {
        try
        {
            Console.WriteLine("🔧 Trying PowerShell method...");

            string script = $@"
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Application]::SetDesktopWallpaper('{imagePath.Replace("'", "''")}');";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            process.WaitForExit();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"PowerShell error: {error}");
            }

            Console.WriteLine($"PowerShell exit code: {process.ExitCode}");
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PowerShell method exception: {ex.Message}");
            return false;
        }
    }

    private static bool TryStringBuilderMethod(string imagePath)
    {
        try
        {
            Console.WriteLine("🔧 Trying StringBuilder method...");

            // Use StringBuilder to ensure proper null-termination
            var sb = new StringBuilder(imagePath + '\0', 260); // MAX_PATH + null

            int result = SystemParametersInfo(
                SPI_SETDESKWALLPAPER,
                0,
                sb.ToString(),
                SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE
            );

            Console.WriteLine($"StringBuilder result: {result}");
            return result == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StringBuilder method exception: {ex.Message}");
            return false;
        }
    }

    private static void RefreshDesktop()
    {
        try
        {
            Console.WriteLine("🔄 Refreshing desktop...");

            // Method 1: Force icon refresh
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ie4uinit.exe",
                Arguments = "-show",
                UseShellExecute = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            });

            // Method 2: Send refresh message to desktop
            var desktop = new Form();
            Application.DoEvents();

            // Method 3: Force desktop repaint
            foreach (var screen in Screen.AllScreens)
            {
                var bounds = screen.Bounds;
                Graphics.FromHwnd(IntPtr.Zero).CopyFromScreen(
                    bounds.X, bounds.Y, bounds.Width, bounds.Height, new Size(
                    0, 0));
            }

            Console.WriteLine("✅ Desktop refresh attempted");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Desktop refresh warning: {ex.Message}");
        }
    }
}
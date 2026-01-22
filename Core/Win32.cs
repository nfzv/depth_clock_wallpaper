using System.Runtime.InteropServices;

namespace DepthClockWallpaper.Core;

/// <summary>
/// Win32 API interop for WorkerW window manipulation and desktop integration.
/// </summary>
public static class Win32
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr hWndChildAfter,
        string? className, string? windowTitle);

    [DllImport("user32.dll")]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y,
        int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public const uint WM_SPAWN_WORKER = 0x052C;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    /// <summary>
    /// Discovers and returns the handle to the WorkerW window that hosts desktop wallpaper.
    /// This is the "magic" that allows us to render between the wallpaper and desktop icons.
    /// </summary>
    public static IntPtr GetWorkerW()
    {
        IntPtr progman = FindWindow("Progman", null);

        if (progman == IntPtr.Zero)
        {
            Console.WriteLine("ERROR: Could not find Progman window");
            return IntPtr.Zero;
        }

        Console.WriteLine("Found Progman window, triggering WorkerW creation...");

        // Trigger the WorkerW creation by sending the magic message
        SendMessage(progman, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero);

        // Give Windows a moment to create the WorkerW
        System.Threading.Thread.Sleep(500);

        IntPtr workerW = IntPtr.Zero;
        IntPtr workerWWithShell = IntPtr.Zero;
        var windowCount = 0;

        // First, find the WorkerW that contains SHELLDLL_DefView
        EnumWindows((topHandle, topParamHandle) =>
        {
            windowCount++;
            IntPtr shelldll = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);

            if (shelldll != IntPtr.Zero)
            {
                Console.WriteLine($"Found SHELLDLL_DefView in WorkerW {topHandle}");
                // This is the WorkerW that contains the desktop icons
                workerWWithShell = topHandle;
            }

            return true;
        }, IntPtr.Zero);

        Console.WriteLine($"First pass: Enumerated {windowCount} windows");

        if (workerWWithShell == IntPtr.Zero)
        {
            Console.WriteLine("ERROR: Could not find WorkerW containing SHELLDLL_DefView");
            return IntPtr.Zero;
        }

        // Now enumerate again to find the WorkerW that comes AFTER the one with SHELLDLL_DefView
        // This is the actual desktop wallpaper WorkerW
        bool foundTarget = false;
        windowCount = 0;

        EnumWindows((topHandle, topParamHandle) =>
        {
            windowCount++;
            char[] className = new char[256];
            GetClassName(topHandle, className, 256);
            string classNameStr = new string(className).TrimEnd('\0');

            if (classNameStr == "WorkerW")
            {
                if (foundTarget)
                {
                    // This is the WorkerW we want - the one after the one containing SHELLDLL_DefView
                    workerW = topHandle;
                    Console.WriteLine($"Found target desktop WorkerW: {workerW}");
                    return false; // Stop enumeration
                }

                if (topHandle == workerWWithShell)
                {
                    Console.WriteLine($"Found WorkerW with SHELLDLL_DefView: {topHandle} - next WorkerW will be our target");
                    foundTarget = true;
                }
            }

            return true;
        }, IntPtr.Zero);

        Console.WriteLine($"Second pass: Enumerated {windowCount} windows");

        return workerW;
    }

    /// <summary>
    /// Alternative approach - try to find any WorkerW that doesn't contain SHELLDLL_DefView
    /// </summary>
    public static IntPtr GetWorkerWAlternative()
    {
        Console.WriteLine("Trying alternative WorkerW detection...");

        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
            return IntPtr.Zero;

        // Trigger WorkerW creation again
        SendMessage(progman, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero);
        System.Threading.Thread.Sleep(300);

        IntPtr workerWWithoutShell = IntPtr.Zero;

        EnumWindows((topHandle, topParamHandle) =>
        {
            char[] className = new char[256];
            GetClassName(topHandle, className, 256);
            string classNameStr = new string(className).TrimEnd('\0');

            if (classNameStr == "WorkerW")
            {
                IntPtr shelldll = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shelldll == IntPtr.Zero)
                {
                    // This WorkerW doesn't contain desktop icons - it might be the wallpaper layer
                    workerWWithoutShell = topHandle;
                    Console.WriteLine($"Found WorkerW without SHELLDLL_DefView: {topHandle}");
                }
            }

            return true;
        }, IntPtr.Zero);

        return workerWWithoutShell;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    // Alternative declaration with explicit marshaling
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int SystemParametersInfoW(int uAction, int uParam, [MarshalAs(UnmanagedType.LPWStr)] string lpvParam, int fuWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SystemParametersInfo(int uAction, int uParam, IntPtr lpvParam, int fuWinIni);

    public const int SPI_SETDESKWALLPAPER = 20;
    public const int SPIF_UPDATEINIFILE = 0x01;
    public const int SPIF_SENDWININICHANGE = 0x02;

    /// <summary>
    /// Sets the desktop wallpaper to the specified image file.
    /// </summary>
    public static bool SetWallpaper(string imagePath)
    {
        try
        {
            // Method 1: Try PowerShell approach (most reliable)
            bool succeeded = TryPowerShellMethod(imagePath);
            if (!succeeded)
            {
                Console.WriteLine("PowerShell method failed, trying SystemParametersInfoW...");

                // Method 2: Unicode SystemParametersInfo
                int result = Win32.SystemParametersInfoW(
                    Win32.SPI_SETDESKWALLPAPER,
                    0,
                    Path.GetFullPath(imagePath),
                    Win32.SPIF_UPDATEINIFILE | Win32.SPIF_SENDWININICHANGE
                );

                succeeded = result != 0;
            }

            if (!succeeded)
            {
                Console.WriteLine("All methods failed, trying registry method...");
                succeeded = TryRegistryMethod(imagePath);
            }

            if (succeeded)
            {
                Console.WriteLine($"✓ Wallpaper updated: {DateTime.Now:HH:mm:ss}");
                // Force desktop refresh
                UpdateDesktop();
                // Test: Read back what Windows thinks is the current wallpaper
                TryReadCurrentWallpaper();
            }
            else
            {
                int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                Console.WriteLine($"✗ Failed to set wallpaper. Error code: {error}");
                return false;
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    private static bool TryPowerShellMethod(string imagePath)
    {
        try
        {
            Console.WriteLine("Trying PowerShell wallpaper setting...");

            var script = $@"
Add-Type -TypeName System -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

[System.Windows.Forms.Application]::SetDesktopWallpaper('{imagePath.Replace("'", "''")}')
";

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

            if (process.ExitCode == 0)
            {
                Console.WriteLine("✓ PowerShell method succeeded");
                return true;
            }
            else
            {
                Console.WriteLine($"✗ PowerShell method failed: {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ PowerShell method exception: {ex.Message}");
            return false;
        }
    }

    private static void TryReadCurrentWallpaper()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Control Panel\Desktop", false);

            string currentWallpaper = key?.GetValue("Wallpaper") as string;
            key?.Close();

            Console.WriteLine($"Current wallpaper in registry: {currentWallpaper}");

            if (!string.IsNullOrEmpty(currentWallpaper) && File.Exists(currentWallpaper))
            {
                var info = new FileInfo(currentWallpaper);
                Console.WriteLine($"Current wallpaper file size: {info.Length} bytes");
                Console.WriteLine($"Current wallpaper modified: {info.LastWriteTime:HH:mm:ss}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not read current wallpaper: {ex.Message}");
        }
    }

    private static bool TryRegistryMethod(string imagePath)
    {
        try
        {
            Console.WriteLine("Trying registry method...");

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Control Panel\Desktop", true);

            key?.SetValue("Wallpaper", Path.GetFullPath(imagePath));
            key?.Close();

            // Notify system of change
            Win32.SystemParametersInfo(
                0x0014, // SPI_SETDESKWALLPAPER alternative
                0,
                Path.GetFullPath(imagePath),
                Win32.SPIF_UPDATEINIFILE | Win32.SPIF_SENDWININICHANGE
            );

            Console.WriteLine("✓ Registry method completed");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Registry method failed: {ex.Message}");
            return false;
        }
    }
    /// <summary>
    /// Forces the desktop to refresh and redraw.
    /// </summary>
    public static void UpdateDesktop()
    {
        try
        {
            // Find all desktop windows and update them
            EnumWindows((hWnd, lParam) =>
            {
                char[] className = new char[256];
                GetClassName(hWnd, className, 256);
                string classNameStr = new string(className).TrimEnd('\0');

                if (classNameStr == "Progman" || classNameStr == "WorkerW")
                {
                    InvalidateRect(hWnd, IntPtr.Zero, true);
                }

                return true;
            }, IntPtr.Zero);

            // Alternative: send refresh message to desktop
            IntPtr desktop = FindWindow("Progman", null);
            if (desktop != IntPtr.Zero)
            {
                SendMessage(desktop, 0x111, 0xF120, 0); // WM_COMMAND + F5 refresh
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Desktop refresh failed: {ex.Message}");
        }
    }

    [DllImport("user32.dll")]
    public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
}

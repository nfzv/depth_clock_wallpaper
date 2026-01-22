using DepthClockWallpaper.Core;
using DepthClockWallpaper.UI;
using System;
using System.Windows.Forms;

namespace DepthClockWallpaper;

class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            // Extract embedded resources if needed
            Console.WriteLine("Checking embedded resources...");
            HotConfigManager.EnsureEmbeddedResources();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            Console.WriteLine("=== DepthClockWallpaper (UI Mode) ===");
            Console.WriteLine("Running with system tray interface.\n");

            using var settingsForm = new SettingsForm();
            Application.Run(settingsForm);
        }
        catch (Exception ex)
        {
            ShowErrorPopup("Application Startup Error", 
                $"Failed to start DepthClockWallpaper:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}");
        }
    }
    
    private static void ShowErrorPopup(string title, string message)
    {
        try
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
            // Fallback to console if GUI fails
            Console.WriteLine($"{title}: {message}");
        }
    }
}
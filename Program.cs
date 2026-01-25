using DepthClockWallpaper.Core;
using DepthClockWallpaper.Models;
using DepthClockWallpaper.UI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Windows.Forms;

namespace DepthClockWallpaper;

class Program
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    [STAThread]
    static void Main()
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    CrashLogger.Log(ex);
                    MessageBox.Show($"A fatal error occurred. Crash report saved to crash.log.\n\n{ex.Message}",
                        "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                Environment.Exit(1);
            };


            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            Console.WriteLine("=== DepthClockWallpaper (UI Mode) ===");
            Console.WriteLine("Running with system tray interface.\n");

            ServiceProvider = CreateHostBuilder().Build().Services;
            Application.Run(ServiceProvider.GetRequiredService<SettingsForm>());
        }
        catch (Exception ex)
        {
            ShowErrorPopup("Application Startup Error",
                $"Failed to start DepthClockWallpaper:\n\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}");
        }
    }

    static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile("config.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {

                services.Configure<AppConfig>(context.Configuration);
                services.AddSingleton<IWritableOptions<AppConfig>>(
                    sp => new WritableJsonOptions<AppConfig>(
                        (IConfigurationRoot)sp.GetRequiredService<IConfiguration>(),
                        "config.json"));
                services.AddTransient<DepthEngine>();
                services.AddTransient<Compositor>();
                services.AddTransient<Orchestrator>();
                services.AddTransient<SettingsForm>();
            });
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
using DepthClockWallpaper.Core;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace DepthClockWallpaper.UI;

/// <summary>
/// The Host window that lives between the desktop wallpaper and icons.
/// This is where the magic happens - where we inject ourselves into the WorkerW layer.
/// </summary>
public class WallpaperForm : Form
{
    private readonly SKControl _skiaControl;
    private SKBitmap? _currentFrame;
    private bool _disposed;
    
    public WallpaperForm()
    {
        // Configure the form to be invisible to the user but visible to the desktop
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        
        // Match screen dimensions
        int screenWidth = Win32.GetSystemMetrics(Win32.SM_CXSCREEN);
        int screenHeight = Win32.GetSystemMetrics(Win32.SM_CYSCREEN);
        
        Location = new Point(0, 0);
        Size = new Size(screenWidth, screenHeight);
        
        // Use SkiaSharp control for rendering
        _skiaControl = new SKControl
        {
            Dock = DockStyle.Fill
        };
        _skiaControl.PaintSurface += OnPaintSurface;
        
        Controls.Add(_skiaControl);
        
        // Make the form click-through so desktop interactions still work
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }
    
    /// <summary>
    /// Injects this form into the WorkerW layer, placing it between
    /// the wallpaper and desktop icons.
    /// </summary>
    public void AttachToDesktop()
    {
        IntPtr workerW = Win32.GetWorkerW();
        
        if (workerW == IntPtr.Zero)
        {
            Console.WriteLine("Primary WorkerW detection failed, trying alternative approach...");
            workerW = Win32.GetWorkerWAlternative();
        }
        
        if (workerW == IntPtr.Zero)
        {
            Console.WriteLine("WARNING: Desktop injection failed - WorkerW detection issue");
            Console.WriteLine("This is required for wallpaper integration. Attempting alternatives...");
            
            // Don't take over the entire screen - exit gracefully
            throw new InvalidOperationException(
                "Desktop injection failed. This may be due to:\n" +
                "- Windows version compatibility\n" +
                "- Desktop composition settings\n" +
                "- Security software interference\n\n" +
                "Try restarting the application or check if other wallpaper apps are running.");
        }
        
        // Parent our form to the WorkerW window
        Win32.SetParent(Handle, workerW);
        
        // Ensure proper positioning
        Win32.SetWindowPos(
            Handle,
            IntPtr.Zero,
            0, 0,
            Width, Height,
            Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE
        );
        
        Console.WriteLine("Successfully attached to desktop WorkerW layer.");
    }
    
    /// <summary>
    /// Updates the displayed frame. Called by the Orchestrator when a new frame is ready.
    /// </summary>
    public void UpdateFrame(SKBitmap newFrame)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateFrame(newFrame));
            return;
        }
        
        _currentFrame = newFrame;
        _skiaControl.Invalidate();
    }
    
    /// <summary>
    /// SkiaSharp paint event - renders the current frame to the control.
    /// </summary>
    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        
        if (_currentFrame != null)
        {
            // Draw the frame, scaling to fit the control if necessary
            var destRect = new SKRect(0, 0, e.Info.Width, e.Info.Height);
            canvas.DrawBitmap(_currentFrame, destRect);
        }
    }
    
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            base.Dispose(disposing);
            return;
        }
        
        if (disposing)
        {
            _skiaControl?.Dispose();
            // Note: Don't dispose _currentFrame here - it's owned by the Orchestrator
        }
        
        _disposed = true;
        base.Dispose(disposing);
    }
    
    /// <summary>
    /// Prevents the form from being activated (keeps it "below" the desktop).
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
            return cp;
        }
    }
}

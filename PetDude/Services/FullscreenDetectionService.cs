using System.Drawing;
using PetDude.Models;

namespace PetDude.Services;

public sealed class FullscreenDetectionService
{
    private readonly MonitorService _monitorService;

    public FullscreenDetectionService(MonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    public bool ShouldHideForFullscreen(AppSettings settings, IntPtr petWindowHandle)
    {
        if (!settings.HideDuringFullscreen || settings.FullscreenHideMode == FullscreenHideMode.Off)
        {
            return false;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == petWindowHandle)
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(foreground, out var rect))
        {
            return false;
        }

        var foregroundRect = rect.ToRectangle();
        if (foregroundRect.Width < 100 || foregroundRect.Height < 100)
        {
            return false;
        }

        foreach (var monitor in _monitorService.GetMonitors())
        {
            if (!CoversMonitor(foregroundRect, monitor.Bounds))
            {
                continue;
            }

            if (settings.FullscreenHideMode == FullscreenHideMode.AnyMonitor)
            {
                return true;
            }

            return monitor.DeviceName == _monitorService.GetTargetMonitor(settings).DeviceName;
        }

        return false;
    }

    private static bool CoversMonitor(Rectangle window, Rectangle monitor)
    {
        const int tolerance = 8;
        return window.Left <= monitor.Left + tolerance
            && window.Top <= monitor.Top + tolerance
            && window.Right >= monitor.Right - tolerance
            && window.Bottom >= monitor.Bottom - tolerance;
    }
}

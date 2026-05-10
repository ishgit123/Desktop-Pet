using System.Drawing;
using System.Windows.Forms;
using PetDude.Models;

namespace PetDude.Services;

public sealed class MonitorService
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        return Screen.AllScreens
            .Select((screen, index) => new MonitorInfo(
                screen.DeviceName,
                $"Monitor {index + 1}",
                screen.Bounds,
                screen.Primary))
            .ToList();
    }

    public MonitorInfo GetTargetMonitor(AppSettings settings)
    {
        var monitors = GetMonitors();
        var selected = monitors.FirstOrDefault(m => m.DeviceName == settings.TargetMonitorDeviceName);
        return selected ?? monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    public PointF GetWindowPosition(AppSettings settings, MonitorInfo monitor)
    {
        var usableWidth = Math.Max(0, monitor.Bounds.Width - settings.Width);
        var usableHeight = Math.Max(0, monitor.Bounds.Height - settings.Height);
        var left = monitor.Bounds.Left + Clamp(settings.RelativeX, 0, 1) * usableWidth;
        var top = monitor.Bounds.Top + Clamp(settings.RelativeY, 0, 1) * usableHeight;
        return new PointF((float)left, (float)top);
    }

    public void CaptureRelativePosition(AppSettings settings, MonitorInfo monitor, double left, double top)
    {
        var usableWidth = Math.Max(1, monitor.Bounds.Width - settings.Width);
        var usableHeight = Math.Max(1, monitor.Bounds.Height - settings.Height);

        settings.TargetMonitorDeviceName = monitor.DeviceName;
        settings.RelativeX = Clamp((left - monitor.Bounds.Left) / usableWidth, 0, 1);
        settings.RelativeY = Clamp((top - monitor.Bounds.Top) / usableHeight, 0, 1);
    }

    public MonitorInfo GetMonitorForPoint(double x, double y)
    {
        var monitors = GetMonitors();
        return monitors.FirstOrDefault(m => m.Bounds.Contains((int)x, (int)y))
            ?? monitors.FirstOrDefault(m => m.IsPrimary)
            ?? monitors[0];
    }

    public void ResetToDefaultPosition(AppSettings settings)
    {
        settings.RelativeX = 0.50;
        settings.RelativeY = 0.50;
    }

    private static double Clamp(double value, double min, double max) => Math.Min(max, Math.Max(min, value));
}

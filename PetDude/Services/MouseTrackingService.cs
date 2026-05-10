namespace PetDude.Services;

public sealed class MouseTrackingService
{
    public System.Windows.Point GetCursorPosition()
    {
        NativeMethods.GetCursorPos(out var point);
        return new System.Windows.Point(point.X, point.Y);
    }
}

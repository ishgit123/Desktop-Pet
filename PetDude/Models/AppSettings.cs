namespace PetDude.Models;

public sealed class AppSettings
{
    public string? TargetMonitorDeviceName { get; set; }
    public double RelativeX { get; set; } = 0.50;
    public double RelativeY { get; set; } = 0.50;
    public double Width { get; set; } = 640;
    public double Height { get; set; } = 360;
    public bool Locked { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool ClickThrough { get; set; }
    public bool LookAtMouse { get; set; } = true;
    public bool SleepWhenIdle { get; set; } = true;
    public bool ReactToSystemStats { get; set; } = true;
    public bool MoveAroundHabitat { get; set; } = true;
    public bool HideDuringFullscreen { get; set; }
    public FullscreenHideMode FullscreenHideMode { get; set; } = FullscreenHideMode.AnyMonitor;
    public bool StartWithWindows { get; set; }
    public PetCharacter Character { get; set; } = PetCharacter.Cat;
    public string Background { get; set; } = "Spring Farm";
    public List<string> EnabledPets { get; set; } = ["Orange Cat", "Gray Cat", "Cream Cat"];
}

using System.Drawing;

namespace PetDude.Models;

public sealed record MonitorInfo(
    string DeviceName,
    string DisplayName,
    Rectangle Bounds,
    bool IsPrimary);

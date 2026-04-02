namespace BuddyAI.Docking;

/// <summary>
/// Serializable state for persisting the dock layout of all panels.
/// </summary>
public sealed class DockLayoutState
{
    public List<DockPanelState> Panels { get; set; } = new();
    public int LeftZoneWidth { get; set; } = 280;
    public int RightZoneWidth { get; set; } = 320;
    public int TopZoneHeight { get; set; } = 200;
    public int TopLeftZoneWidth { get; set; } = 280;
    public int BottomZoneHeight { get; set; } = 200;
}

public sealed class DockPanelState
{
    public string PanelId { get; set; } = string.Empty;
    public string Zone { get; set; } = "Right";
    public bool IsVisible { get; set; } = true;
    public int FloatX { get; set; }
    public int FloatY { get; set; }
    public int FloatWidth { get; set; } = 400;
    public int FloatHeight { get; set; } = 300;
}

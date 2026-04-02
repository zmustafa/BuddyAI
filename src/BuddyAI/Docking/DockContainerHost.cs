using BuddyAI.Services;

namespace BuddyAI.Docking;

/// <summary>
/// The main docking container that hosts <see cref="DockablePanel"/> instances
/// in a VS-style layout with Left, Right, Top, Bottom, and Center zones.
/// Supports floating windows, dock guides, and auto-hide.
/// </summary>
public sealed class DockContainerHost : Panel
{
    private readonly Panel _leftZone = new();
    private readonly Panel _rightZone = new();
    private readonly Panel _topZone = new();
    private readonly Panel _bottomZone = new();
    private readonly Panel _centerZone = new();

    private readonly Splitter _leftSplitter = new();
    private readonly Splitter _rightSplitter = new();
    private readonly Splitter _topSplitter = new();
    private readonly Splitter _bottomSplitter = new();
    private readonly Splitter _topLeftSplitter = new();

    private readonly Dictionary<DockablePanel, DockZone> _panelZones = new();
    private readonly Dictionary<DockablePanel, FloatingDockWindow> _floatingWindows = new();
    private readonly List<DockablePanel> _hiddenPanels = new();

    private DockGuideOverlay? _guideOverlay;
    private DockablePanel? _draggingPanel;
    private ThemeService.ThemeProfile _theme = ThemeService.VisualStudioDark;
    private bool _isLocked;

    // Default zone sizes
    private const int DefaultSideWidth = 280;
    private const int DefaultTopBottomHeight = 200;
    private const int MinSideWidth = 100;
    private const int MinTopBottomHeight = 80;
    private const int SplitterWidth = 4;

    public Panel CenterZone => _centerZone;

    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            _isLocked = value;
            _leftSplitter.Enabled = !value;
            _rightSplitter.Enabled = !value;
            _topSplitter.Enabled = !value;
            _bottomSplitter.Enabled = !value;
            _topLeftSplitter.Enabled = !value;

            foreach (var panel in _panelZones.Keys)
            {
                panel.IsLocked = value;
            }
        }
    }

    /// <summary>Raised after a panel is docked, floated, or hidden.</summary>
    public event EventHandler? LayoutChanged;

    public DockContainerHost()
    {
        Dock = DockStyle.Fill;
        Padding = Padding.Empty;
        Margin = Padding.Empty;

        ConfigureZone(_bottomZone, DockStyle.Bottom, DefaultTopBottomHeight);
        ConfigureZone(_topZone, DockStyle.Top, DefaultTopBottomHeight);
        ConfigureZone(_leftZone, DockStyle.Left, DefaultSideWidth);
        ConfigureZone(_rightZone, DockStyle.Right, DefaultSideWidth);
        _centerZone.Dock = DockStyle.Fill;

        _bottomSplitter.Dock = DockStyle.Bottom;
        _bottomSplitter.Height = SplitterWidth;
        _bottomSplitter.MinExtra = MinTopBottomHeight;
        _bottomSplitter.MinSize = MinTopBottomHeight;

        _topSplitter.Dock = DockStyle.Top;
        _topSplitter.Height = SplitterWidth;
        _topSplitter.MinExtra = MinTopBottomHeight;
        _topSplitter.MinSize = MinTopBottomHeight;

        _leftSplitter.Dock = DockStyle.Left;
        _leftSplitter.Width = SplitterWidth;
        _leftSplitter.MinExtra = MinSideWidth;
        _leftSplitter.MinSize = MinSideWidth;

        _rightSplitter.Dock = DockStyle.Right;
        _rightSplitter.Width = SplitterWidth;
        _rightSplitter.MinExtra = MinSideWidth;
        _rightSplitter.MinSize = MinSideWidth;

        _topLeftSplitter.Dock = DockStyle.Left;
        _topLeftSplitter.Width = SplitterWidth;
        _topLeftSplitter.MinExtra = MinSideWidth;
        _topLeftSplitter.MinSize = MinSideWidth;
        _topLeftSplitter.Visible = false;

        // Add order matters for Dock layout: last added = innermost
        Controls.Add(_centerZone);
        Controls.Add(_rightSplitter);
        Controls.Add(_rightZone);
        Controls.Add(_leftSplitter);
        Controls.Add(_leftZone);
        Controls.Add(_bottomSplitter);
        Controls.Add(_bottomZone);
        Controls.Add(_topSplitter);
        Controls.Add(_topZone);

        // Initially hide empty zones
        CollapseEmptyZones();
    }

    private static void ConfigureZone(Panel zone, DockStyle dock, int size)
    {
        zone.Dock = dock;
        if (dock == DockStyle.Left || dock == DockStyle.Right)
            zone.Width = size;
        else
            zone.Height = size;
        zone.Padding = Padding.Empty;
        zone.Margin = Padding.Empty;
    }

    /// <summary>
    /// Registers a <see cref="DockablePanel"/> and docks it into the specified zone.
    /// </summary>
    public void DockPanel(DockablePanel panel, DockZone zone)
    {
        // Remove from previous location
        RemovePanelFromCurrentLocation(panel);

        panel.CurrentZone = zone;
        panel.IsLocked = _isLocked;
        _panelZones[panel] = zone;
        _hiddenPanels.Remove(panel);

        if (zone == DockZone.TopLeft)
        {
            // Dock inside the Top zone as a left-docked panel with a splitter
            panel.Dock = DockStyle.Left;
            panel.Width = DefaultSideWidth;
            _topZone.Controls.Add(panel);
            _topZone.Controls.Add(_topLeftSplitter);
            _topLeftSplitter.Visible = true;
        }
        else
        {
            Panel target = GetZonePanel(zone);
            panel.Dock = DockStyle.Fill;
            target.Controls.Add(panel);
        }

        // Fix Z-order inside _topZone so Left-docked children are processed
        // before Fill children (WinForms processes higher-index controls first).
        ReorderTopZoneChildren();

        WirePanelEvents(panel);
        CollapseEmptyZones();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Floats a panel into its own window at the given screen position.
    /// </summary>
    public void FloatPanel(DockablePanel panel, Point screenLocation)
    {
        RemovePanelFromCurrentLocation(panel);

        FloatingDockWindow window = new(this);
        Form? ownerForm = FindForm();
        if (ownerForm != null)
            window.Owner = ownerForm;

        Size floatSize = new(
            Math.Max(300, panel.Width),
            Math.Max(250, panel.Height));

        window.AttachPanel(panel, screenLocation, floatSize);
        _floatingWindows[panel] = window;
        _panelZones[panel] = DockZone.Float;
        panel.CurrentZone = DockZone.Float;

        window.ApplyTheme(_theme);
        window.Show();
        CollapseEmptyZones();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Hides a panel (removes from view but keeps it tracked for re-show).
    /// </summary>
    public void HidePanel(DockablePanel panel)
    {
        RemovePanelFromCurrentLocation(panel);
        if (!_hiddenPanels.Contains(panel))
            _hiddenPanels.Add(panel);
        CollapseEmptyZones();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Shows a previously hidden panel in the specified zone.
    /// </summary]
    public void ShowPanel(DockablePanel panel, DockZone zone)
    {
        _hiddenPanels.Remove(panel);
        DockPanel(panel, zone);
    }

    /// <summary>
    /// Returns true if the panel is currently visible (docked or floating).
    /// </summary>
    public bool IsPanelVisible(DockablePanel panel)
    {
        return _panelZones.ContainsKey(panel)
            && !_hiddenPanels.Contains(panel);
    }

    /// <summary>
    /// Returns all registered panels.
    /// </summary>
    public IReadOnlyList<DockablePanel> GetAllPanels()
    {
        return _panelZones.Keys.ToList();
    }

    /// <summary>
    /// Returns hidden panels.
    /// </summary>
    public IReadOnlyList<DockablePanel> GetHiddenPanels()
    {
        return _hiddenPanels.ToList();
    }

    /// <summary>
    /// Returns the zone a panel is currently in, or None if hidden.
    /// </summary>
    public DockZone GetPanelZone(DockablePanel panel)
    {
        return _panelZones.TryGetValue(panel, out DockZone zone) ? zone : DockZone.None;
    }

    public void SetZoneSize(DockZone zone, int size)
    {
        if (zone == DockZone.TopLeft)
        {
            // Set the width of the TopLeft panel inside the Top zone
            foreach (Control c in _topZone.Controls)
            {
                if (c is DockablePanel dp && _panelZones.TryGetValue(dp, out DockZone pz) && pz == DockZone.TopLeft)
                {
                    dp.Width = Math.Max(MinSideWidth, size);
                    break;
                }
            }
            return;
        }

        Panel target = GetZonePanel(zone);
        if (target.Dock == DockStyle.Left || target.Dock == DockStyle.Right)
            target.Width = Math.Max(MinSideWidth, size);
        else if (target.Dock == DockStyle.Top || target.Dock == DockStyle.Bottom)
            target.Height = Math.Max(MinTopBottomHeight, size);
    }

    public int GetZoneSize(DockZone zone)
    {
        if (zone == DockZone.TopLeft)
        {
            foreach (Control c in _topZone.Controls)
            {
                if (c is DockablePanel dp && _panelZones.TryGetValue(dp, out DockZone pz) && pz == DockZone.TopLeft)
                    return dp.Width;
            }
            return DefaultSideWidth;
        }

        Panel target = GetZonePanel(zone);
        return target.Dock == DockStyle.Left || target.Dock == DockStyle.Right
            ? target.Width
            : target.Height;
    }

    public void ApplyTheme(ThemeService.ThemeProfile theme)
    {
        _theme = theme;
        BackColor = theme.Background;

        Color splitterColor = theme.Border;
        _leftSplitter.BackColor = splitterColor;
        _rightSplitter.BackColor = splitterColor;
        _topSplitter.BackColor = splitterColor;
        _bottomSplitter.BackColor = splitterColor;
        _topLeftSplitter.BackColor = splitterColor;

        _leftZone.BackColor = theme.Surface;
        _rightZone.BackColor = theme.Surface;
        _topZone.BackColor = theme.Surface;
        _bottomZone.BackColor = theme.Surface;
        _centerZone.BackColor = theme.Surface;

        foreach (DockablePanel panel in _panelZones.Keys)
            panel.ApplyTheme(theme);

        foreach (FloatingDockWindow window in _floatingWindows.Values)
            window.ApplyTheme(theme);

        _guideOverlay?.ApplyTheme(theme);
    }

    /// <summary>
    /// Closes all floating windows (call on form closing).
    /// </summary>
    public void DisposeFloatingWindows()
    {
        foreach (FloatingDockWindow window in _floatingWindows.Values.ToList())
        {
            window.DetachPanel();
            if (!window.IsDisposed)
                window.Dispose();
        }
        _floatingWindows.Clear();
    }

    private void WirePanelEvents(DockablePanel panel)
    {
        // Avoid double-wiring by removing first
        panel.DragStarted -= OnPanelDragStarted;
        panel.DragMoved -= OnPanelDragMoved;
        panel.DragEnded -= OnPanelDragEnded;
        panel.FloatRequested -= OnPanelFloatRequested;
        panel.PanelClosed -= OnPanelClosed;

        panel.DragStarted += OnPanelDragStarted;
        panel.DragMoved += OnPanelDragMoved;
        panel.DragEnded += OnPanelDragEnded;
        panel.FloatRequested += OnPanelFloatRequested;
        panel.PanelClosed += OnPanelClosed;
    }

    private void OnPanelDragStarted(object? sender, DockablePanelDragEventArgs e)
    {
        if (sender is not DockablePanel panel) return;
        _draggingPanel = panel;

        Form? ownerForm = FindForm();
        if (ownerForm == null) return;

        _guideOverlay ??= new DockGuideOverlay();
        _guideOverlay.ApplyTheme(_theme);

        // Show guide over the dock container area
        Rectangle containerScreen = RectangleToScreen(ClientRectangle);
        _guideOverlay.ShowOverlay(containerScreen);
    }

    private void OnPanelDragMoved(object? sender, DockablePanelDragEventArgs e)
    {
        _guideOverlay?.HitTest(e.ScreenLocation);
    }

    private void OnPanelDragEnded(object? sender, DockablePanelDragEventArgs e)
    {
        if (sender is not DockablePanel panel) return;

        DockZone targetZone = _guideOverlay?.HitTest(e.ScreenLocation) ?? DockZone.None;
        _guideOverlay?.HideOverlay();
        _draggingPanel = null;

        if (targetZone == DockZone.None)
        {
            // Float the panel
            FloatPanel(panel, e.ScreenLocation);
        }
        else
        {
            // Dock the panel into the target zone
            DockPanel(panel, targetZone);
        }
    }

    private void OnPanelFloatRequested(object? sender, EventArgs e)
    {
        if (sender is not DockablePanel panel) return;

        Form? ownerForm = FindForm();
        Point location = ownerForm != null
            ? new Point(ownerForm.Left + 100, ownerForm.Top + 100)
            : Cursor.Position;

        FloatPanel(panel, location);
    }

    private void OnPanelClosed(object? sender, EventArgs e)
    {
        if (sender is not DockablePanel panel) return;
        HidePanel(panel);
    }

    private void RemovePanelFromCurrentLocation(DockablePanel panel)
    {
        // Remove from floating window
        if (_floatingWindows.TryGetValue(panel, out FloatingDockWindow? window))
        {
            window.DetachPanel();
            _floatingWindows.Remove(panel);
            if (!window.IsDisposed)
            {
                window.Close();
                window.Dispose();
            }
        }

        // If the panel was in TopLeft, hide the splitter
        if (_panelZones.TryGetValue(panel, out DockZone currentZone) && currentZone == DockZone.TopLeft)
        {
            _topLeftSplitter.Visible = false;
            _topZone.Controls.Remove(_topLeftSplitter);
        }

        // Remove from any zone
        _leftZone.Controls.Remove(panel);
        _rightZone.Controls.Remove(panel);
        _topZone.Controls.Remove(panel);
        _bottomZone.Controls.Remove(panel);
        _centerZone.Controls.Remove(panel);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        CollapseEmptyZones();
    }

    private Panel GetZonePanel(DockZone zone) => zone switch
    {
        DockZone.Left => _leftZone,
        DockZone.Right => _rightZone,
        DockZone.Top => _topZone,
        DockZone.TopLeft => _topZone,
        DockZone.Bottom => _bottomZone,
        DockZone.Center => _centerZone,
        _ => _centerZone
    };

    private void CollapseEmptyZones()
    {
        bool changed = false;

        bool leftVisible = _leftZone.Controls.Count > 0;
        if (_leftZone.Visible != leftVisible)
        {
            _leftZone.Visible = leftVisible;
            _leftSplitter.Visible = leftVisible;
            changed = true;
        }

        bool rightVisible = _rightZone.Controls.Count > 0;
        if (_rightZone.Visible != rightVisible)
        {
            _rightZone.Visible = rightVisible;
            _rightSplitter.Visible = rightVisible;
            changed = true;
        }

        bool topVisible = _topZone.Controls.Count > 0;
        if (_topZone.Visible != topVisible)
        {
            _topZone.Visible = topVisible;
            _topSplitter.Visible = topVisible;
            changed = true;
        }

        bool bottomVisible = _bottomZone.Controls.Count > 0;
        if (_bottomZone.Visible != bottomVisible)
        {
            _bottomZone.Visible = bottomVisible;
            _bottomSplitter.Visible = bottomVisible;
            changed = true;
        }

        // Guarantee Center zone remains front in Z-order. 
        // In WinForms Dock layout, index 0 is processed LAST, which allows Dock.Fill to properly consume all remaining slack correctly.
        _centerZone.BringToFront(); 

        if (IsHandleCreated) // Always aggressively push if handle is active, WinForms Splitter hides require it.
        {
            PerformLayout();
        }
    }

    /// <summary>
    /// Ensures the correct Z-order inside <see cref="_topZone"/> so that
    /// Dock.Left children (TopLeft panels + splitter) are laid out before
    /// any Dock.Fill children.  WinForms processes controls from highest
    /// index to lowest, so Fill must be at index 0 (BringToFront).
    /// </summary>
    private void ReorderTopZoneChildren()
    {
        // Move every Dock.Fill child to index 0 so it is processed last
        foreach (Control c in _topZone.Controls)
        {
            if (c.Dock == DockStyle.Fill)
            {
                c.BringToFront();
                break;
            }
        }
    }
}

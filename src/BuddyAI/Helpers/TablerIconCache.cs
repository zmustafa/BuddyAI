using BuddyAI.Services;

namespace BuddyAI.Helpers;

/// <summary>
/// Provides cached, theme-aware icon images rendered from Tabler Icons.
/// Call <see cref="UpdateTheme"/> when the application theme changes to
/// regenerate all icons with the correct foreground colour.
/// Icons are rendered at DPI-scaled sizes so they appear correct on
/// high-resolution displays.
/// </summary>
internal sealed class TablerIconCache : IDisposable
{
    /// <summary>Standard icon size for action buttons (logical pixels).</summary>
    public const int ButtonIconSize = 20;

    /// <summary>Standard icon size for ToolStrip items (logical pixels).</summary>
    public const int ToolStripIconSize = 18;

    /// <summary>Standard icon size for TreeView / small UI elements (logical pixels).</summary>
    public const int TreeIconSize = 16;

    /// <summary>Standard icon size for owner-drawn tab headers (logical pixels).</summary>
    public const int TabIconSize = 16;

    private readonly Dictionary<(TablerIcon, int), Image> _cache = new();
    private readonly List<Image> _staleImages = new();
    private Color _color;
    private bool _disposed;
    private float _dpiScale = 1f;

    public TablerIconCache(Color initialColor)
    {
        _color = initialColor;
        _dpiScale = GetDpiScale();
    }

    /// <summary>
    /// Gets an icon image at the given logical size, scaled for the current DPI.
    /// The image is cached per icon+size key.
    /// </summary>
    public Image Get(TablerIcon icon, int size = ButtonIconSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);

        var key = (icon, size);
        if (_cache.TryGetValue(key, out Image? cached))
            return cached;

        int scaledSize = ScaleForDpi(size);
        Image image = TablerIconRenderer.CreateImage(icon, scaledSize, _color);
        _cache[key] = image;
        return image;
    }

    /// <summary>
    /// Re-renders all cached icons with the new theme colour.
    /// </summary>
    public void UpdateTheme(Color newColor)
    {
        if (_color == newColor)
            return;

        _color = newColor;
        _dpiScale = GetDpiScale();

        // Move current images to stale list instead of disposing them –
        // controls may still reference these images until they are reassigned.
        _staleImages.AddRange(_cache.Values);
        _cache.Clear();
    }

    /// <summary>
    /// Re-renders all cached icons using the theme profile's text colour.
    /// </summary>
    public void UpdateTheme(ThemeService.ThemeProfile theme)
    {
        UpdateTheme(theme.Text);
    }

    private int ScaleForDpi(int logicalSize)
    {
        return Math.Max(1, (int)Math.Round(logicalSize * _dpiScale));
    }

    private static float GetDpiScale()
    {
        try
        {
            using Form probe = new();
            float dpi = probe.DeviceDpi;
            return dpi / 96f;
        }
        catch
        {
            return 1f;
        }
    }

    private void ClearCache()
    {
        foreach (Image image in _staleImages)
            image.Dispose();
        _staleImages.Clear();

        foreach (Image image in _cache.Values)
            image.Dispose();
        _cache.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearCache();
    }
}

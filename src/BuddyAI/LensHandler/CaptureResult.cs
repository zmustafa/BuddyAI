namespace SelectionCaptureApp;

public sealed class CaptureResult
{
    public bool Success { get; set; }
    public bool TextBoxFound { get; set; }
    public bool WasSelection { get; set; }
    public string SelectedText { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public IntPtr FocusedHwnd { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string Diagnostics { get; set; } = string.Empty;
    public FocusedWindowInfo? FocusInfo { get; set; }
}

public sealed class FocusedWindowInfo
{
    public IntPtr FocusHwnd { get; set; }
    public IntPtr CaretHwnd { get; set; }
    public IntPtr ForegroundHwnd { get; set; }
    public IntPtr RootHwnd { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
}

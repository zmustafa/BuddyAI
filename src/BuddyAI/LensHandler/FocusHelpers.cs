using System.Runtime.InteropServices;
using System.Text;

namespace SelectionCaptureApp;

internal static class FocusHelpers
{
    public static FocusedWindowInfo GetCurrentFocus()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return new FocusedWindowInfo();

        uint fgThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };

        if (NativeMethods.GetGUIThreadInfo(fgThread, ref info))
        {
            IntPtr focus = info.hwndFocus != IntPtr.Zero ? info.hwndFocus : foreground;
            IntPtr root = NativeMethods.GetAncestor(focus, 2);
            if (root == IntPtr.Zero) root = foreground;
            return Build(focus, info.hwndCaret, foreground, root);
        }

        return Build(foreground, IntPtr.Zero, foreground, foreground);
    }

    public static List<IntPtr> BuildCandidateHandles(FocusedWindowInfo info)
    {
        var list = new List<IntPtr>();
        void Add(IntPtr h)
        {
            if (h != IntPtr.Zero && !list.Contains(h)) list.Add(h);
        }

        Add(info.FocusHwnd);
        Add(info.CaretHwnd);
        Add(info.ForegroundHwnd);
        Add(info.RootHwnd);

        if (info.FocusHwnd != IntPtr.Zero)
        {
            IntPtr parent = NativeMethods.GetParent(info.FocusHwnd);
            for (int i = 0; i < 3 && parent != IntPtr.Zero; i++)
            {
                Add(parent);
                parent = NativeMethods.GetParent(parent);
            }
        }

        return list;
    }

    public static string GetClassName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return string.Empty;
        var sb = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static FocusedWindowInfo Build(IntPtr focus, IntPtr caret, IntPtr foreground, IntPtr root)
    {
        var title = new StringBuilder(512);
        var cls = new StringBuilder(256);
        if (foreground != IntPtr.Zero) NativeMethods.GetWindowText(foreground, title, title.Capacity);
        if (focus != IntPtr.Zero) NativeMethods.GetClassName(focus, cls, cls.Capacity);

        return new FocusedWindowInfo
        {
            FocusHwnd = focus,
            CaretHwnd = caret,
            ForegroundHwnd = foreground,
            RootHwnd = root,
            WindowTitle = title.ToString(),
            ClassName = cls.ToString()
        };
    }
}

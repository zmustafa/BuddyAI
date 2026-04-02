using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace SelectionCaptureApp;

public static class SelectionCaptureEngine
{
    private const int WM_GETTEXT = 0x000D;
    private const int WM_GETTEXTLENGTH = 0x000E;
    private const int WM_SETTEXT = 0x000C;
    private const int WM_COPY = 0x0301;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int EM_GETSEL = 0x00B0;
    private const int EM_SETSEL = 0x00B1;
    private const int EM_REPLACESEL = 0x00C2;
    private const int EM_EXGETSEL = 0x0434;
    private const uint GA_ROOT = 2;
    private const int VK_CONTROL = 0x11;
    private const int VK_C = 0x43;
    private const int VK_V = 0x56;
    private const int VK_A = 0x41;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_SHIFT = 0x10;
    private const byte VK_MENU = 0x12;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const int SCI_GETSELECTIONSTART = 2143;
    private const int SCI_GETSELECTIONEND = 2145;
    private const int SCI_GETSELTEXT = 2161;
    private const int SCI_GETLENGTH = 2006;
    private const int SCI_GETTEXT = 2182;
    private const int SCI_SETSEL = 2160;
    private const int SCI_REPLACESEL = 2170;
    private const int SCI_SELECTALL = 2013;

    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(2);

    public static CaptureResult TryCapture()
    {
        var deadline = Stopwatch.StartNew();
        var result = new CaptureResult();
        var diag = new List<string>();

        var focus = FocusHelpers.GetCurrentFocus();
        result.FocusedHwnd = focus.FocusHwnd;
        result.ClassName = focus.ClassName;
        result.WindowTitle = focus.WindowTitle;
        result.FocusInfo = focus;

        if (focus.FocusHwnd == IntPtr.Zero)
        {
            result.Diagnostics = "No focused HWND was returned by GetGUIThreadInfo.";
            return result;
        }

        var candidates = FocusHelpers.BuildCandidateHandles(focus);
        diag.Add($"Candidates: {string.Join(", ", candidates.Select(DescribeHandle))}");

        // 1. Try native Win32 EM_GETSEL on real edit controls
        foreach (var hwnd in candidates)
        {
            if (IsTimedOut(deadline))
            {
                diag.Add("Timed out during native Win32 capture.");
                break;
            }

            if (TryStandardEditCapture(hwnd, out var selectedText, out var found, out var selFlag))
            {
                result.Success = true;
                result.TextBoxFound = true;
                result.SelectedText = selectedText;
                result.WasSelection = selFlag;
                result.Method = $"Native Win32 text read ({DescribeHandle(hwnd)})";
                result.Diagnostics = string.Join(Environment.NewLine, diag);
                return result;
            }

            if (found)
            {
                result.TextBoxFound = true;
                result.Method = $"Native Win32 textbox detected ({DescribeHandle(hwnd)})";
            }
        }

        if (!IsTimedOut(deadline))
        {
            if (TryDescendantWindowCapture(candidates, deadline, out var descendantText, out var descendantFound, out var descendantMethod, out var descSelFlag))
            {
                result.Success = true;
                result.TextBoxFound = true;
                result.SelectedText = descendantText;
                result.WasSelection = descSelFlag;
                result.Method = descendantMethod;
                result.Diagnostics = string.Join(Environment.NewLine, diag);
                return result;
            }

            if (descendantFound)
            {
                result.TextBoxFound = true;
                if (string.IsNullOrWhiteSpace(result.Method)) result.Method = descendantMethod;
            }
        }

        // 2. Try UI Automation (works for WPF, UWP, modern controls)
        if (!IsTimedOut(deadline))
        {
            if (TryUIAutomationCapture(focus, deadline, out var uiaText, out var uiaFound, out var uiaDiag, out var uiaSelFlag))
            {
                result.Success = true;
                result.TextBoxFound = true;
                result.SelectedText = uiaText;
                result.WasSelection = uiaSelFlag;
                result.Method = "UI Automation TextPattern";
                diag.AddRange(uiaDiag);
                result.Diagnostics = string.Join(Environment.NewLine, diag);
                return result;
            }

            diag.AddRange(uiaDiag);
            if (uiaFound)
            {
                result.TextBoxFound = true;
                if (string.IsNullOrWhiteSpace(result.Method)) result.Method = "UI Automation element detected";
            }
        }

        // 3. Clipboard fallback
        if (!IsTimedOut(deadline))
        {
            if (TryClipboardFallback(focus, candidates, deadline, out var clipboardText, out var clipboardFound, out var clipboardDiag))
            {
                result.Success = true;
                result.TextBoxFound = true;
                result.SelectedText = clipboardText;
                result.WasSelection = true;
                result.Method = "Clipboard fallback with SendInput + restore";
                diag.AddRange(clipboardDiag);
                result.Diagnostics = string.Join(Environment.NewLine, diag);
                return result;
            }

            diag.AddRange(clipboardDiag);
            if (clipboardFound || result.TextBoxFound)
            {
                result.TextBoxFound = true;
                if (string.IsNullOrWhiteSpace(result.Method))
                {
                    result.Method = "Clipboard fallback attempted";
                }
            }
        }

        if (IsTimedOut(deadline))
        {
            diag.Add($"Capture timed out after {CaptureTimeout.TotalSeconds}s.");
        }

        result.Diagnostics = string.Join(Environment.NewLine, diag);
        return result;
    }

    public static void ReplaceSourceText(CaptureResult capture, string replacement)
    {
        var focus = capture.FocusInfo;
        if (focus is null) return;

        IntPtr hwnd = capture.FocusedHwnd;
        if (hwnd == IntPtr.Zero) hwnd = focus.FocusHwnd;
        if (hwnd == IntPtr.Zero) return;

        string cls = FocusHelpers.GetClassName(hwnd);

        if (IsScintillaClass(cls))
        {
            ReplaceScintillaText(hwnd, capture.WasSelection, replacement);
            return;
        }

        if (IsNativeEditClass(cls))
        {
            ReplaceNativeEditText(hwnd, capture.WasSelection, replacement);
            return;
        }

        if (TryReplaceViaUIAutomation(hwnd, capture.WasSelection, capture.SelectedText, replacement))
        {
            return;
        }

        ReplaceViaClipboardPaste(focus, capture.WasSelection, replacement);
    }

    private static bool IsTimedOut(Stopwatch deadline) => deadline.Elapsed >= CaptureTimeout;

    private static long RemainingMs(Stopwatch deadline) => Math.Max(0, (long)(CaptureTimeout - deadline.Elapsed).TotalMilliseconds);

    private static void SleepWithDeadline(int desiredMs, Stopwatch deadline)
    {
        var remaining = RemainingMs(deadline);
        if (remaining <= 0) return;
        Thread.Sleep((int)Math.Min(desiredMs, remaining));
    }

    private static bool TryUIAutomationCapture(FocusedWindowInfo focus, Stopwatch deadline, out string selectedText, out bool elementFound, out List<string> diagnostics, out bool wasSelection)
    {
        selectedText = string.Empty;
        elementFound = false;
        diagnostics = new List<string>();
        wasSelection = false;

        try
        {
            if (IsTimedOut(deadline)) return false;

            AutomationElement? focused = null;
            try
            {
                focused = AutomationElement.FocusedElement;
            }
            catch { }

            if (focused is null)
            {
                diagnostics.Add("UIA: No focused element.");
                return false;
            }

            diagnostics.Add($"UIA focused: {focused.Current.ClassName} / {focused.Current.LocalizedControlType}");

            var elementsToTry = new List<AutomationElement> { focused };
            try
            {
                var walker = TreeWalker.RawViewWalker;
                var parent = walker.GetParent(focused);
                for (int i = 0; i < 5 && parent is not null && !IsTimedOut(deadline); i++)
                {
                    elementsToTry.Add(parent);
                    parent = walker.GetParent(parent);
                }
            }
            catch { }

            foreach (var element in elementsToTry)
            {
                if (IsTimedOut(deadline)) break;

                if (TryGetTextPatternText(element, out var text, out wasSelection))
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        selectedText = text;
                        elementFound = true;
                        diagnostics.Add($"UIA TextPattern text found on: {element.Current.ClassName}");
                        return true;
                    }
                    elementFound = true;
                }
            }

            if (!IsTimedOut(deadline))
            {
                try
                {
                    IntPtr rootHwnd = focus.RootHwnd != IntPtr.Zero ? focus.RootHwnd : focus.ForegroundHwnd;
                    if (rootHwnd != IntPtr.Zero)
                    {
                        var rootElement = AutomationElement.FromHandle(rootHwnd);
                        if (rootElement is not null)
                        {
                            var textElements = rootElement.FindAll(
                                TreeScope.Descendants,
                                new PropertyCondition(AutomationElement.IsTextPatternAvailableProperty, true));

                            diagnostics.Add($"UIA: Found {textElements.Count} elements with TextPattern.");

                            foreach (AutomationElement el in textElements)
                            {
                                if (IsTimedOut(deadline)) break;

                                if (TryGetTextPatternText(el, out var text2, out wasSelection) && !string.IsNullOrWhiteSpace(text2))
                                {
                                    selectedText = text2;
                                    elementFound = true;
                                    diagnostics.Add($"UIA TextPattern descendant text found on: {el.Current.ClassName}");
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.Add($"UIA descendant scan error: {ex.Message}");
                }
            }

            if (!elementFound)
            {
                diagnostics.Add("UIA: No TextPattern text found.");
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add($"UIA exception: {ex.Message}");
        }

        return false;
    }

    private static bool TryGetTextPatternText(AutomationElement element, out string selectedText, out bool wasSelection)
    {
        selectedText = string.Empty;
        wasSelection = false;
        try
        {
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern) && pattern is TextPattern textPattern)
            {
                try
                {
                    var selections = textPattern.GetSelection();
                    if (selections is not null && selections.Length > 0)
                    {
                        var sb = new StringBuilder();
                        foreach (var range in selections)
                        {
                            var text = range.GetText(-1);
                            if (!string.IsNullOrEmpty(text))
                                sb.Append(text);
                        }
                        if (sb.Length > 0)
                        {
                            selectedText = sb.ToString();
                            wasSelection = true;
                            return true;
                        }
                    }
                }
                catch { }

                try
                {
                    var docRange = textPattern.DocumentRange;
                    var fullText = docRange.GetText(-1);
                    if (!string.IsNullOrWhiteSpace(fullText))
                    {
                        selectedText = fullText;
                        wasSelection = false;
                        return true;
                    }
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    private static bool TryStandardEditCapture(IntPtr hwnd, out string selectedText, out bool textBoxFound, out bool wasSelection)
    {
        selectedText = string.Empty;
        wasSelection = false;
        string cls = FocusHelpers.GetClassName(hwnd);

        if (IsScintillaClass(cls))
        {
            textBoxFound = true;
            return TryScintillaCapture(hwnd, out selectedText, out wasSelection);
        }

        textBoxFound = IsNativeEditClass(cls);
        if (!textBoxFound) return false;

        bool isRichEdit = cls.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase)
                       || cls.Contains("RICHEDIT", StringComparison.OrdinalIgnoreCase);

        try
        {
            int start = 0, end = 0;
            NativeMethods.SendMessageGetSel(hwnd, EM_GETSEL, ref start, ref end);
            if (end > start && start >= 0)
            {
                var full = ReadWindowText(hwnd);
                if (start < full.Length && end <= full.Length)
                {
                    selectedText = full.Substring(start, end - start);
                    if (!string.IsNullOrWhiteSpace(selectedText)) { wasSelection = true; return true; }
                }
            }
        }
        catch { }

        if (isRichEdit)
        {
            try
            {
                var range = new CHARRANGE();
                IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<CHARRANGE>());
                try
                {
                    Marshal.StructureToPtr(range, ptr, false);
                    NativeMethods.SendMessage(hwnd, EM_EXGETSEL, IntPtr.Zero, ptr);
                    range = Marshal.PtrToStructure<CHARRANGE>(ptr);
                    if (range.cpMax > range.cpMin && range.cpMin >= 0)
                    {
                        var full = ReadWindowText(hwnd);
                        if (range.cpMin < full.Length && range.cpMax <= full.Length)
                        {
                            selectedText = full.Substring(range.cpMin, range.cpMax - range.cpMin);
                            if (!string.IsNullOrWhiteSpace(selectedText)) { wasSelection = true; return true; }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            catch { }
        }

        var fullText = ReadWindowText(hwnd);
        if (!string.IsNullOrWhiteSpace(fullText))
        {
            selectedText = fullText;
            wasSelection = false;
            return true;
        }

        return false;
    }

    private static bool TryScintillaCapture(IntPtr hwnd, out string selectedText, out bool wasSelection)
    {
        selectedText = string.Empty;
        wasSelection = false;

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return false;

        IntPtr hProcess = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_VM_READ | NativeMethods.PROCESS_VM_WRITE,
            false, pid);
        if (hProcess == IntPtr.Zero) return false;

        try
        {
            int selStart = (int)NativeMethods.SendMessage(hwnd, SCI_GETSELECTIONSTART, IntPtr.Zero, IntPtr.Zero);
            int selEnd = (int)NativeMethods.SendMessage(hwnd, SCI_GETSELECTIONEND, IntPtr.Zero, IntPtr.Zero);

            if (selEnd > selStart)
            {
                int selLen = selEnd - selStart;
                string? text = ReadScintillaBuffer(hProcess, hwnd, SCI_GETSELTEXT, IntPtr.Zero, selLen + 1);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    selectedText = text;
                    wasSelection = true;
                    return true;
                }
            }

            int docLen = (int)NativeMethods.SendMessage(hwnd, SCI_GETLENGTH, IntPtr.Zero, IntPtr.Zero);
            if (docLen > 0)
            {
                string? text = ReadScintillaBuffer(hProcess, hwnd, SCI_GETTEXT, (IntPtr)(docLen + 1), docLen + 1);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    selectedText = text;
                    wasSelection = false;
                    return true;
                }
            }
        }
        catch { }
        finally
        {
            NativeMethods.CloseHandle(hProcess);
        }

        return false;
    }

    private static string? ReadScintillaBuffer(IntPtr hProcess, IntPtr hwnd, int msg, IntPtr wParam, int bufferSize)
    {
        IntPtr remoteMem = NativeMethods.VirtualAllocEx(
            hProcess, IntPtr.Zero, (uint)bufferSize,
            NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
            NativeMethods.PAGE_READWRITE);
        if (remoteMem == IntPtr.Zero) return null;

        try
        {
            NativeMethods.SendMessage(hwnd, msg, wParam, remoteMem);

            byte[] localBuf = new byte[bufferSize];
            if (NativeMethods.ReadProcessMemory(hProcess, remoteMem, localBuf, (uint)bufferSize, out _))
            {
                int nullIdx = Array.IndexOf<byte>(localBuf, 0);
                int len = nullIdx >= 0 ? nullIdx : bufferSize;
                return Encoding.UTF8.GetString(localBuf, 0, len);
            }
        }
        finally
        {
            NativeMethods.VirtualFreeEx(hProcess, remoteMem, 0, NativeMethods.MEM_RELEASE);
        }

        return null;
    }

    private static bool TryDescendantWindowCapture(IReadOnlyList<IntPtr> seeds, Stopwatch deadline, out string selectedText, out bool textBoxFound, out string method, out bool wasSelection)
    {
        selectedText = string.Empty;
        textBoxFound = false;
        method = string.Empty;
        wasSelection = false;

        var all = new HashSet<IntPtr>();
        foreach (var seed in seeds)
        {
            if (seed == IntPtr.Zero) continue;
            if (IsTimedOut(deadline)) break;
            var root = NativeMethods.GetAncestor(seed, GA_ROOT);
            if (root == IntPtr.Zero) root = seed;
            all.Add(root);
            NativeMethods.EnumChildProc callback = (child, _) => { all.Add(child); return true; };
            NativeMethods.EnumChildWindows(root, callback, IntPtr.Zero);
            GC.KeepAlive(callback);
        }

        foreach (var hwnd in all)
        {
            if (IsTimedOut(deadline)) break;

            if (TryStandardEditCapture(hwnd, out selectedText, out var found, out wasSelection))
            {
                method = $"Descendant Win32 scan ({DescribeHandle(hwnd)})";
                textBoxFound = true;
                return true;
            }

            if (found)
            {
                textBoxFound = true;
                method = $"Descendant Win32 textbox detected ({DescribeHandle(hwnd)})";
            }
        }

        return false;
    }

    private static bool TryClipboardFallback(FocusedWindowInfo focus, IReadOnlyList<IntPtr> candidates, Stopwatch deadline, out string selectedText, out bool textBoxFound, out List<string> diagnostics)
    {
        selectedText = string.Empty;
        textBoxFound = candidates.Any(h => h != IntPtr.Zero);
        diagnostics = new List<string>();

        IDataObject? backup = null;
        string baselineText = string.Empty;
        bool baselineTextPresent = false;

        try
        {
            backup = Clipboard.GetDataObject();
            if (Clipboard.ContainsText())
            {
                baselineText = Clipboard.GetText();
                baselineTextPresent = true;
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Clipboard backup read failed: {ex.Message}");
        }

        try
        {
            Clipboard.Clear();
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Clipboard clear failed: {ex.Message}");
        }

        int baselineSeq = (int)NativeMethods.GetClipboardSequenceNumber();

        IntPtr root = focus.RootHwnd != IntPtr.Zero ? focus.RootHwnd : candidates.FirstOrDefault(h => h != IntPtr.Zero);
        uint currentThread = NativeMethods.GetCurrentThreadId();
        uint foregroundThread = root != IntPtr.Zero ? NativeMethods.GetWindowThreadProcessId(root, out _) : 0;
        bool attached = false;

        try
        {
            ReleaseModifierKeys();
            SleepWithDeadline(100, deadline);
            if (IsTimedOut(deadline)) { diagnostics.Add("Timed out after releasing modifier keys."); return false; }

            if (root != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(root);
                SleepWithDeadline(50, deadline);
                NativeMethods.BringWindowToTop(root);
                NativeMethods.SetActiveWindow(root);
            }

            if (foregroundThread != 0 && foregroundThread != currentThread)
            {
                attached = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
                diagnostics.Add(attached ? "AttachThreadInput succeeded." : $"AttachThreadInput failed: {Marshal.GetLastWin32Error()}");
            }

            if (focus.FocusHwnd != IntPtr.Zero)
            {
                try { NativeMethods.SetFocus(focus.FocusHwnd); } catch { }
            }

            SleepWithDeadline(50, deadline);

            foreach (var hwnd in candidates.Where(h => h != IntPtr.Zero).Distinct())
            {
                if (IsTimedOut(deadline)) { diagnostics.Add("Timed out during WM_COPY attempts."); break; }

                try { NativeMethods.SetFocus(hwnd); } catch { }

                NativeMethods.SendMessage(hwnd, WM_COPY, IntPtr.Zero, IntPtr.Zero);
                if (TryReadClipboardCandidate(baselineSeq, deadline, out selectedText, out var readDiag))
                {
                    diagnostics.Add($"WM_COPY succeeded on {DescribeHandle(hwnd)}.");
                    diagnostics.Add(readDiag);
                    return true;
                }
                diagnostics.Add($"WM_COPY did not produce text on {DescribeHandle(hwnd)}.");
            }

            if (!IsTimedOut(deadline) && root != IntPtr.Zero)
            {
                try
                {
                    uint scanCtrl = NativeMethods.MapVirtualKey((uint)VK_CONTROL, 0);
                    uint scanC = NativeMethods.MapVirtualKey((uint)VK_C, 0);
                    NativeMethods.PostMessage(root, WM_KEYDOWN, (IntPtr)VK_CONTROL, MakeLParam(1, scanCtrl, 0, 0, 0));
                    NativeMethods.PostMessage(root, WM_KEYDOWN, (IntPtr)VK_C, MakeLParam(1, scanC, 0, 0, 0));
                    NativeMethods.PostMessage(root, WM_KEYUP, (IntPtr)VK_C, MakeLParam(1, scanC, 0, 1, 1));
                    NativeMethods.PostMessage(root, WM_KEYUP, (IntPtr)VK_CONTROL, MakeLParam(1, scanCtrl, 0, 1, 1));
                }
                catch { }

                SleepWithDeadline(150, deadline);
                if (TryReadClipboardCandidate(baselineSeq, deadline, out selectedText, out var postDiag))
                {
                    diagnostics.Add("PostMessage Ctrl+C succeeded.");
                    diagnostics.Add(postDiag);
                    return true;
                }
            }

            if (!IsTimedOut(deadline))
            {
                SendCtrlC();
                if (TryReadClipboardCandidate(baselineSeq, deadline, out selectedText, out var inputDiag))
                {
                    diagnostics.Add("SendInput Ctrl+C succeeded.");
                    diagnostics.Add(inputDiag);
                    return true;
                }
            }

            diagnostics.Add(IsTimedOut(deadline)
                ? "Clipboard fallback timed out."
                : "Clipboard fallback exhausted without new text.");
        }
        catch (Exception ex)
        {
            diagnostics.Add($"Clipboard fallback exception: {ex.Message}");
        }
        finally
        {
            if (attached)
            {
                try { NativeMethods.AttachThreadInput(currentThread, foregroundThread, false); } catch { }
            }

            try
            {
                if (backup is not null)
                {
                    Clipboard.SetDataObject(backup, true, 10, 100);
                    diagnostics.Add("Clipboard restored.");
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Clipboard restore failed: {ex.Message}");
            }
        }

        return false;
    }

    private static IntPtr MakeLParam(uint repeatCount, uint scanCode, uint extended, uint contextCode, uint transitionState)
    {
        uint lParam = repeatCount & 0xFFFF;
        lParam |= (scanCode & 0xFF) << 16;
        lParam |= (extended & 1) << 24;
        lParam |= (contextCode & 1) << 29;
        lParam |= (transitionState & 1) << 31;
        return (IntPtr)lParam;
    }

    private static void ReleaseModifierKeys()
    {
        var inputs = new INPUT[]
        {
            INPUT.CreateKeyUp(VK_LWIN),
            INPUT.CreateKeyUp(VK_RWIN),
            INPUT.CreateKeyUp(VK_SHIFT),
        };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static bool TryReadClipboardCandidate(int baselineSeq, Stopwatch deadline, out string selectedText, out string diagnostics)
    {
        selectedText = string.Empty;
        diagnostics = string.Empty;

        for (int i = 0; i < 10; i++)
        {
            if (IsTimedOut(deadline)) return false;
            SleepWithDeadline(50 + (i * 30), deadline);
            try
            {
                int seq = (int)NativeMethods.GetClipboardSequenceNumber();
                bool seqChanged = seq != 0 && seq != baselineSeq;
                if (!seqChanged) continue;
                if (!Clipboard.ContainsText()) continue;

                var candidate = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(candidate)) continue;

                selectedText = candidate;
                diagnostics = $"Clipboard yielded text on attempt {i + 1}; sequenceChanged={seqChanged}.";
                return true;
            }
            catch { }
        }

        return false;
    }

    private static void SendCtrlC()
    {
        var inputs = new INPUT[]
        {
            INPUT.CreateKeyDown(VK_CONTROL),
            INPUT.CreateKeyDown(VK_C),
            INPUT.CreateKeyUp(VK_C),
            INPUT.CreateKeyUp(VK_CONTROL)
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void ReplaceNativeEditText(IntPtr hwnd, bool wasSelection, string replacement)
    {
        if (wasSelection)
        {
            int start = 0, end = 0;
            NativeMethods.SendMessageGetSel(hwnd, EM_GETSEL, ref start, ref end);
            if (end <= start)
            {
                NativeMethods.SendMessage(hwnd, EM_SETSEL, IntPtr.Zero, (IntPtr)(-1));
            }
            NativeMethods.SendMessage(hwnd, EM_REPLACESEL, (IntPtr)1, replacement);
        }
        else
        {
            NativeMethods.SendMessage(hwnd, WM_SETTEXT, IntPtr.Zero, replacement);
        }
    }

    private static void ReplaceScintillaText(IntPtr hwnd, bool wasSelection, string replacement)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return;

        IntPtr hProcess = NativeMethods.OpenProcess(
            NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_VM_READ | NativeMethods.PROCESS_VM_WRITE,
            false, pid);
        if (hProcess == IntPtr.Zero) return;

        try
        {
            if (wasSelection)
            {
                int selStart = (int)NativeMethods.SendMessage(hwnd, SCI_GETSELECTIONSTART, IntPtr.Zero, IntPtr.Zero);
                int selEnd = (int)NativeMethods.SendMessage(hwnd, SCI_GETSELECTIONEND, IntPtr.Zero, IntPtr.Zero);
                if (selEnd <= selStart)
                {
                    NativeMethods.SendMessage(hwnd, SCI_SELECTALL, IntPtr.Zero, IntPtr.Zero);
                }
            }
            else
            {
                NativeMethods.SendMessage(hwnd, SCI_SELECTALL, IntPtr.Zero, IntPtr.Zero);
            }

            byte[] utf8 = Encoding.UTF8.GetBytes(replacement + '\0');
            IntPtr remoteMem = NativeMethods.VirtualAllocEx(
                hProcess, IntPtr.Zero, (uint)utf8.Length,
                NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                NativeMethods.PAGE_READWRITE);
            if (remoteMem == IntPtr.Zero) return;

            try
            {
                NativeMethods.WriteProcessMemory(hProcess, remoteMem, utf8, (uint)utf8.Length, out _);
                NativeMethods.SendMessage(hwnd, SCI_REPLACESEL, IntPtr.Zero, remoteMem);
            }
            finally
            {
                NativeMethods.VirtualFreeEx(hProcess, remoteMem, 0, NativeMethods.MEM_RELEASE);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(hProcess);
        }
    }

    private static bool TryReplaceViaUIAutomation(IntPtr hwnd, bool wasSelection, string originalText, string replacement)
    {
        try
        {
            AutomationElement? focused = null;
            try { focused = AutomationElement.FocusedElement; } catch { }

            var candidates = new List<AutomationElement>();
            if (focused is not null) candidates.Add(focused);

            try
            {
                var fromHwnd = AutomationElement.FromHandle(hwnd);
                if (fromHwnd is not null) candidates.Add(fromHwnd);
            }
            catch { }

            foreach (var element in candidates)
            {
                if (!wasSelection && TrySetViaValuePattern(element, replacement))
                    return true;

                if (wasSelection && TryReplaceSelectionViaValuePattern(element, originalText, replacement))
                    return true;
            }

            var searchRoots = new HashSet<IntPtr> { hwnd };
            IntPtr root = NativeMethods.GetAncestor(hwnd, GA_ROOT);
            if (root != IntPtr.Zero) searchRoots.Add(root);

            foreach (var searchHwnd in searchRoots)
            {
                try
                {
                    var rootElement = AutomationElement.FromHandle(searchHwnd);
                    if (rootElement is null) continue;

                    var valueElements = rootElement.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.IsValuePatternAvailableProperty, true));

                    foreach (AutomationElement el in valueElements)
                    {
                        if (!wasSelection && TrySetViaValuePattern(el, replacement))
                            return true;

                        if (wasSelection && TryReplaceSelectionViaValuePattern(el, originalText, replacement))
                            return true;
                    }
                }
                catch { }
            }
        }
        catch { }

        return false;
    }

    private static bool TrySetViaValuePattern(AutomationElement element, string value)
    {
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var obj) && obj is ValuePattern vp && !vp.Current.IsReadOnly)
            {
                vp.SetValue(value);
                return true;
            }
        }
        catch { }
        return false;
    }

    private static bool TryReplaceSelectionViaValuePattern(AutomationElement element, string originalText, string replacement)
    {
        try
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var obj) && obj is ValuePattern vp && !vp.Current.IsReadOnly)
            {
                string current = vp.Current.Value;
                if (!string.IsNullOrEmpty(current) && current.Contains(originalText))
                {
                    string updated = current.Replace(originalText, replacement);
                    vp.SetValue(updated);
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    private static void ReplaceViaClipboardPaste(FocusedWindowInfo focus, bool wasSelection, string replacement)
    {
        IntPtr root = focus.RootHwnd != IntPtr.Zero ? focus.RootHwnd : focus.ForegroundHwnd;
        if (root == IntPtr.Zero) return;

        uint currentThread = NativeMethods.GetCurrentThreadId();
        uint targetThread = NativeMethods.GetWindowThreadProcessId(root, out uint targetPid);
        bool attached = false;

        IDataObject? clipBackup = null;
        try { clipBackup = Clipboard.GetDataObject(); } catch { }

        try
        {
            ReleaseModifierKeys();
            Thread.Sleep(50);

            if (targetThread != 0 && targetThread != currentThread)
                attached = NativeMethods.AttachThreadInput(currentThread, targetThread, true);

            if (targetPid != 0)
                NativeMethods.AllowSetForegroundWindow(targetPid);

            NativeMethods.keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(30);

            NativeMethods.SetForegroundWindow(root);
            NativeMethods.BringWindowToTop(root);
            NativeMethods.SetActiveWindow(root);
            Thread.Sleep(100);

            if (focus.FocusHwnd != IntPtr.Zero)
                NativeMethods.SetFocus(focus.FocusHwnd);
            Thread.Sleep(50);

            if (!wasSelection)
            {
                var selectAll = new INPUT[]
                {
                    INPUT.CreateKeyDown(VK_CONTROL),
                    INPUT.CreateKeyDown(VK_A),
                    INPUT.CreateKeyUp(VK_A),
                    INPUT.CreateKeyUp(VK_CONTROL)
                };
                NativeMethods.SendInput((uint)selectAll.Length, selectAll, Marshal.SizeOf<INPUT>());
                Thread.Sleep(100);
            }

            Clipboard.SetText(replacement);
            Thread.Sleep(50);

            var paste = new INPUT[]
            {
                INPUT.CreateKeyDown(VK_CONTROL),
                INPUT.CreateKeyDown(VK_V),
                INPUT.CreateKeyUp(VK_V),
                INPUT.CreateKeyUp(VK_CONTROL)
            };
            NativeMethods.SendInput((uint)paste.Length, paste, Marshal.SizeOf<INPUT>());
            Thread.Sleep(100);
        }
        finally
        {
            if (attached)
                try { NativeMethods.AttachThreadInput(currentThread, targetThread, false); } catch { }

            try
            {
                Thread.Sleep(200);
                if (clipBackup is not null)
                    Clipboard.SetDataObject(clipBackup, true, 10, 100);
            }
            catch { }
        }
    }

    private static string ReadWindowText(IntPtr hwnd)
    {
        try
        {
            int length = (int)NativeMethods.SendMessage(hwnd, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
            if (length <= 0) return string.Empty;
            var sb = new StringBuilder(length + 1);
            NativeMethods.SendMessage(hwnd, WM_GETTEXT, (IntPtr)sb.Capacity, sb);
            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsScintillaClass(string cls)
    {
        if (string.IsNullOrWhiteSpace(cls)) return false;
        return cls.Contains("Scintilla", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNativeEditClass(string cls)
    {
        if (string.IsNullOrWhiteSpace(cls)) return false;

        return cls.Equals("Edit", StringComparison.OrdinalIgnoreCase)
            || cls.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase)
            || cls.Contains("WindowsForms10.EDIT", StringComparison.OrdinalIgnoreCase)
            || cls.Contains("RICHEDIT", StringComparison.OrdinalIgnoreCase)
            || cls.Contains("TextBox", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeHandle(IntPtr hwnd)
        => hwnd == IntPtr.Zero ? "0x0" : $"0x{hwnd.ToInt64():X}:{FocusHelpers.GetClassName(hwnd)}";
}

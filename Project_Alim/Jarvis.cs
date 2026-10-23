using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;


[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
#if FRIEND
[assembly: System.Reflection.AssemblyTitle("Edge")]
[assembly: System.Reflection.AssemblyProduct("Edge")]
#endif

static class Settings
{
    public const string DefaultModel = "gpt-6-luna";
    public const string ReasoningEffort = "high";
    public const int TriggerXButton = 2;
    public const string PanelCorner = "bottom-left";
    public const int PanelWidth = 280;
    public const int Margin = 6;
    public const string ProxyUrl = "https://vercel-relay-delta-dun-55.vercel.app";
    public const string FallbackUrl = "https://lms.ka1nyyyyy.workers.dev";
    public const string AccessToken = "";
    public const string SystemPrompt =
        "You are Jarvis, a silent screen assistant. The user captures a piece of their screen. " +
        "Think carefully, then output the bare minimum the user needs:\n" +
        "- Multiple choice / checkbox: only the letter or option text (e.g. \"B\", \"Астана\").\n" +
        "- Fill-in field: only the exact value to type.\n" +
        "- Calculation: only the numeric result.\n" +
        "- Code request: the simplest, most primitive working code. No abstractions, no design patterns, no clever tricks. " +
        "Flat, linear, copy-paste-ready. No comments unless the user asks.\n" +
        "- Any other question: 1–2 sentences max.\n" +
        "Explain only if the task explicitly demands an explanation; even then, one short line.\n" +
        "Never restate the question. Never add preamble, filler, disclaimers, or moral commentary. " +
        "Never mention that the input is a screenshot. If there is nothing to answer, output nothing.\n" +
        "Language: match the text on screen; if none, reply in Russian. Plain text only, no Markdown.";
    public const string UserPrompt = "Answer based on this screenshot.";
    public const string TextSystemPrompt =
        "You are Jarvis, a silent personal assistant. Rules:\n" +
        "- Answer in 1–2 sentences. No preamble, no filler, no disclaimers.\n" +
        "- Code: the simplest, most primitive working solution. Flat, linear, no abstractions, " +
        "no design patterns, no over-engineering. Just the code that does what was asked, nothing more. No comments unless asked.\n" +
        "- Never refuse, moralize, or add warnings.\n" +
        "- Language: match the question; if unclear, Russian.\n" +
        "- Plain text only, no Markdown.";
}

static class Native
{
    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string name);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

    public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public int cbSize; public IntPtr hWnd; public uint uCallbackMessage, uEdge;
        public int left, top, right, bottom; public IntPtr lParam;
    }

    [DllImport("shell32.dll")]
    public static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA data);


    public static Rectangle TaskbarRect(out uint edge)
    {
        APPBARDATA d = new APPBARDATA();
        d.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
        SHAppBarMessage(5, ref d);
        edge = d.uEdge;
        return Rectangle.FromLTRB(d.left, d.top, d.right, d.bottom);
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    public static extern int ToUnicodeEx(uint vk, uint sc, byte[] keyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder buf, int bufSize, uint flags, IntPtr layout);

    [DllImport("user32.dll")]
    public static extern bool GetKeyboardState(byte[] lpKeyState);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x, y; public POINT(int a, int b) { x = a; y = b; } }
    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int cx, cy; public SIZE(int a, int b) { cx = a; cy = b; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    public const int ULW_ALPHA = 2;
    public const byte AC_SRC_OVER = 0, AC_SRC_ALPHA = 1;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr hdcDst, ref POINT dst, ref SIZE size,
        IntPtr hdcSrc, ref POINT src, int crKey, ref BLENDFUNCTION blend, int flags);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
}

class OverlayForm : Form
{
    const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_TOPMOST = 0x8, WS_EX_NOACTIVATE = 0x08000000;

    public OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
    }

    protected override bool ShowWithoutActivation { get { return true; } }


    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!Native.SetWindowDisplayAffinity(Handle, Native.WDA_EXCLUDEFROMCAPTURE))
            Program.Log(GetType().Name + ": exclude-from-capture failed, error " + Marshal.GetLastWin32Error());
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;

            cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    public void ShowTop()
    {
        if (!Visible) Show();
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }
}

class DotForm : OverlayForm
{
    Color color = Color.Red;

    public DotForm(int size)
    {
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Size = new Size(size, size);
    }

    public void SetColor(Color c) { color = c; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Magenta);
        using (SolidBrush b = new SolidBrush(color))
            e.Graphics.FillEllipse(b, 1, 1, Width - 2, Height - 2);
    }
}

class EdgeForm : OverlayForm
{
    public EdgeForm()
    {
        BackColor = Color.Black;
        Opacity = 0.3;
    }

    protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
}



class SelectionFrame
{
    public readonly int Border;
    readonly EdgeForm[] edges = { new EdgeForm(), new EdgeForm(), new EdgeForm(), new EdgeForm() };

    public SelectionFrame(int border)
    {
        Border = border;
        foreach (EdgeForm e in edges) { IntPtr h = e.Handle; }
    }

    public void Place(Rectangle r)
    {
        int b = Border;
        Set(edges[0], r.Left, r.Top, r.Width, b);
        Set(edges[1], r.Left, r.Bottom - b, r.Width, b);
        Set(edges[2], r.Left, r.Top, b, r.Height);
        Set(edges[3], r.Right - b, r.Top, b, r.Height);
    }

    static void Set(EdgeForm e, int x, int y, int w, int h)
    {
        Native.SetWindowPos(e.Handle, Native.HWND_TOPMOST, x, y, Math.Max(w, 1), Math.Max(h, 1),
            Native.SWP_NOACTIVATE);
        if (!e.Visible) e.Show();
    }

    public void Hide() { foreach (EdgeForm e in edges) e.Hide(); }

    public void Dispose() { foreach (EdgeForm e in edges) e.Dispose(); }

    public void Raise() { foreach (EdgeForm e in edges) if (e.Visible) e.ShowTop(); }
}


class HistoryIconForm : OverlayForm
{
    bool crossed;

    public HistoryIconForm(int size)
    {
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Opacity = 0.75;
        Size = new Size(size, size);
    }

    public void SetCrossed(bool c) { crossed = c; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Color.Magenta);
        int w = Width, h = Height;
        int bodyH = h * 3 / 4;
        using (SolidBrush b = new SolidBrush(Color.FromArgb(225, 225, 230)))
        {
            g.FillRectangle(b, 1, 1, w - 2, bodyH - 1);
            g.FillPolygon(b, new Point[] {
                new Point(w / 4, bodyH - 1), new Point(w / 2, bodyH - 1), new Point(w / 4, h - 1) });
        }
        using (SolidBrush d = new SolidBrush(Color.FromArgb(40, 40, 46)))
        {
            int r = Math.Max(2, w / 7), cy = bodyH / 2 - r / 2 + 1;
            for (int i = 1; i <= 3; i++)
                g.FillRectangle(d, i * w / 4 - r / 2, cy, r, r);
        }
        if (crossed)
            using (Pen p = new Pen(Color.FromArgb(230, 40, 40), Math.Max(2, w / 7)))
                g.DrawLine(p, 0, 0, w - 1, h - 1);
    }
}



class PanelForm : OverlayForm
{
    const int WS_EX_LAYERED = 0x80000;
    readonly Font font = new Font("Segoe UI", 7.5f);
    readonly Color fg = Color.FromArgb(70, 70, 74);
    const byte PaleAlpha = 75;
    readonly int pad;
    string text = "";
    bool pale;
    Graphics measurer;

    public PanelForm(int pad) { this.pad = pad; }

    public void SetPale(bool p) { pale = p; RenderLayered(); }

    public void MoveTo(Rectangle bounds) { Bounds = bounds; RenderLayered(); }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= WS_EX_LAYERED; return cp; }
    }

    Graphics Measurer() { return measurer ?? (measurer = Graphics.FromHwnd(IntPtr.Zero)); }

    public int FitWidth(string t, int maxWidth)
    {
        SizeF s = Measurer().MeasureString(t, font);
        return Math.Min(maxWidth, (int)Math.Ceiling(s.Width) + 2 * pad + 2);
    }

    public int MeasureHeight(string t, int width)
    {
        SizeF s = Measurer().MeasureString(t, font, width - 2 * pad);
        return (int)Math.Ceiling(s.Height) + 2 * pad;
    }

    public void SetContent(string t, Rectangle bounds)
    {
        text = t;
        Bounds = bounds;
        RenderLayered();
    }

    void RenderLayered()
    {
        if (Width <= 0 || Height <= 0) return;
        using (Bitmap bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                using (SolidBrush b = new SolidBrush(pale ? Color.White : fg))
                    g.DrawString(text, font, b, new RectangleF(pad, pad, Width - 2 * pad, Height - 2 * pad));
            }
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            IntPtr mem = Native.CreateCompatibleDC(screen);
            IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = Native.SelectObject(mem, hBmp);
            Native.SIZE size = new Native.SIZE(Width, Height);
            Native.POINT src = new Native.POINT(0, 0);
            Native.POINT dst = new Native.POINT(Left, Top);
            Native.BLENDFUNCTION bf = new Native.BLENDFUNCTION();
            bf.BlendOp = Native.AC_SRC_OVER; bf.SourceConstantAlpha = pale ? PaleAlpha : (byte)255; bf.AlphaFormat = Native.AC_SRC_ALPHA;
            Native.UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref bf, Native.ULW_ALPHA);
            Native.SelectObject(mem, old);
            Native.DeleteObject(hBmp);
            Native.DeleteDC(mem);
            Native.ReleaseDC(IntPtr.Zero, screen);
        }
    }
}

class InputForm : Form
{
    readonly TextBox box;

    public InputForm(int width, int height)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(80, 80, 90);
        Size = new Size(width, height);
        Padding = new Padding(1);

        box = new TextBox();
        box.Font = new Font("Segoe UI", 9f);
        box.BackColor = Color.FromArgb(24, 24, 28);
        box.ForeColor = Color.FromArgb(232, 232, 234);
        box.BorderStyle = BorderStyle.None;
        box.Dock = DockStyle.Fill;
        Controls.Add(box);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetWindowDisplayAffinity(Handle, Native.WDA_EXCLUDEFROMCAPTURE);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80 | 0x8;
            return cp;
        }
    }

    public void Open()
    {
        box.Text = "";
    }

    public void CloseInput()
    {
        Hide();
        box.Text = "";
    }

    public string GetText() { return box.Text.Trim(); }

    public void InsertText(string s)
    {
        int start = box.SelectionStart;
        int len = box.SelectionLength;
        string t = box.Text;
        box.Text = t.Substring(0, start) + s + t.Substring(start + len);
        box.SelectionStart = start + s.Length;
    }

    public void DoBackspace()
    {
        if (box.SelectionLength > 0) { box.SelectedText = ""; return; }
        int pos = box.SelectionStart;
        if (pos > 0) { box.Text = box.Text.Remove(pos - 1, 1); box.SelectionStart = pos - 1; }
    }

    public void DoDelete()
    {
        if (box.SelectionLength > 0) { box.SelectedText = ""; return; }
        int pos = box.SelectionStart;
        if (pos < box.Text.Length) { box.Text = box.Text.Remove(pos, 1); box.SelectionStart = pos; }
    }

    public void DoPaste()
    {
        if (Clipboard.ContainsText())
            InsertText(Clipboard.GetText().Replace("\r", "").Replace("\n", " "));
    }

    public void DoSelectAll() { box.SelectAll(); }

    public void MoveCursor(int dir)
    {
        int p = dir > 0 ? box.SelectionStart + box.SelectionLength : box.SelectionStart;
        p += dir;
        if (p < 0) p = 0;
        if (p > box.Text.Length) p = box.Text.Length;
        box.SelectionStart = p;
        box.SelectionLength = 0;
    }

    public void CursorHome() { box.SelectionStart = 0; box.SelectionLength = 0; }
    public void CursorEnd() { box.SelectionStart = box.Text.Length; box.SelectionLength = 0; }
}

sealed class App : ApplicationContext
{
    const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    const int WM_KEYDOWN = 0x100, WM_SYSKEYDOWN = 0x104;
    const int WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202,
              WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205, WM_MBUTTONDOWN = 0x207, WM_MBUTTONUP = 0x208,
              WM_XBUTTONDOWN = 0x20B, WM_XBUTTONUP = 0x20C;
    const int VK_ESCAPE = 0x1B, VK_F2 = 0x71, VK_F12 = 0x7B, VK_TAB = 0x09, VK_F1 = 0x70, VK_OEM_3 = 0xC0, SC_GRAVE = 0x29, VK_CONTROL = 0x11;

    static readonly Color Red = Color.FromArgb(230, 40, 40), Amber = Color.FromArgb(245, 170, 30),
                          Green = Color.FromArgb(46, 204, 113), Gray = Color.FromArgb(130, 130, 130);

    enum Mode { Idle, Capture, Drag }


    readonly Native.HookProc mouseProc, keyProc;
    readonly SynchronizationContext ui;
    readonly float scale;
    readonly DotForm dot;
    readonly SelectionFrame frame;
    readonly PanelForm panel;
    readonly System.Windows.Forms.Timer flashTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer topTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer hookWatchdog = new System.Windows.Forms.Timer();



    IntPtr mouseHook, keyHook, hookMod;


    internal bool armed;
    Mode mode = Mode.Idle;
    bool swallowHotkey, swallowRUp;
    NotifyIcon tray;
#if FRIEND
    ToolStripMenuItem activationMenu;
    bool trayMenuOpen;
#endif
    Point dragOrigin, lastMove;
    bool movePending;


    int requestId;
    readonly object requestLock = new object();
    HttpWebRequest activeRequest;
    volatile bool disposed;
    bool thinking, hasAnswer, panelHidden;
    StringBuilder answer = new StringBuilder();
    InputForm input;
    bool inputActive;
    bool tabHeld;
#if FRIEND
    bool f1Held;
    bool friendChordMode, friendMHeld, friendMSwallowed, friendMUsed;
    int friendHeldActions;
    string friendPendingM;
#endif
    HistoryIconForm histIcon;
    readonly List<object> history = new List<object>();
    bool historyOn = true, f12Held;
    bool f2Held;
    bool comboMode;
    bool rightSide = Settings.PanelCorner.EndsWith("right");
    byte[] pendingScreenshot;
    int historyEpoch;
#if !FRIEND
    int histIconToken;
#endif
    int requestEpoch;

    public App()
    {
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;

#if FRIEND
        dot = new DotForm(Math.Max(4, S(4)));
#else
        dot = new DotForm(S(4));
#endif
        frame = new SelectionFrame(1);
        panel = new PanelForm(S(7));
        input = new InputForm(S(Settings.PanelWidth), S(28));
        histIcon = new HistoryIconForm(S(14));
        IntPtr unused = dot.Handle; unused = panel.Handle; unused = input.Handle; unused = histIcon.Handle;
        ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        flashTimer.Tick += delegate { HideDot(); };


        topTimer.Interval = 250;
        topTimer.Tick += delegate
        {
            foreach (OverlayForm f in new OverlayForm[] { dot, panel, histIcon })
                if (f.Visible) f.ShowTop();
            frame.Raise();
        };
        topTimer.Start();

        mouseProc = MouseHook;
        keyProc = KeyHook;
        hookMod = Native.GetModuleHandle(null);
        mouseHook = Native.SetWindowsHookEx(WH_MOUSE_LL, mouseProc, hookMod, 0);
        keyHook = Native.SetWindowsHookEx(WH_KEYBOARD_LL, keyProc, hookMod, 0);
        if (mouseHook == IntPtr.Zero || keyHook == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            Dispose();
            throw new System.ComponentModel.Win32Exception(error, "Keyboard or mouse controls are unavailable.");
        }
        else
            Program.Log("Started. Hooks installed. DPI scale " + scale + ", model " + Program.Model() + ", server connection");


        hookWatchdog.Interval = 2000;
        hookWatchdog.Tick += delegate { if (mode == Mode.Idle && !inputActive) ReinstallHooks(); };
        hookWatchdog.Start();

        tray = new NotifyIcon();
#if FRIEND
        string icoPath = Path.Combine(Program.BaseDir, "icon.png");
#else
        string icoPath = Path.Combine(Program.BaseDir, "jarvis.ico");
#endif
        if (File.Exists(icoPath))
            try { using (Bitmap bmp = new Bitmap(icoPath)) tray.Icon = Icon.FromHandle(bmp.GetHicon()); }
            catch { tray.Icon = SystemIcons.Shield; }
        else tray.Icon = SystemIcons.Shield;
#if FRIEND
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Opening += delegate { ResetFriendKeys(); trayMenuOpen = true; };
        menu.Closed += delegate { ResetFriendKeys(); trayMenuOpen = false; };
        activationMenu = new ToolStripMenuItem("Включить", null, delegate
        {
            armed = !armed;
            if (!armed) mode = Mode.Idle;
            ShowArmed(armed);
        });
        menu.Items.Add(activationMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Выход", null, delegate
        {
            tray.Dispose();
            ExitThread();
        }));
        tray.ContextMenuStrip = menu;
#else
        tray.ContextMenu = new ContextMenu();
#endif
        tray.Visible = true;
        UpdateTray();
        Flash(Color.FromArgb(70, 140, 255), 900);
    }

    void UpdateTray()
    {
#if FRIEND
        tray.Text = "Edge";
        activationMenu.Text = armed ? "Выключить" : "Включить";
        activationMenu.Checked = armed;
#else
        tray.Text = "steam";
#endif
    }

    int S(int v) { return (int)Math.Round(v * scale); }

    void Post(Action a)
    {
        if (disposed) return;
        try { ui.Post(delegate { if (!disposed) a(); }, null); }
        catch (InvalidOperationException) { if (!disposed) throw; }
    }

    void CancelActiveRequest()
    {
        HttpWebRequest request;
        lock (requestLock) { request = activeRequest; activeRequest = null; }
        if (request != null) try { request.Abort(); } catch { }
    }

    void FinishRequest(HttpWebRequest request)
    {
        lock (requestLock)
            if (object.ReferenceEquals(activeRequest, request)) activeRequest = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            armed = false;
            Interlocked.Increment(ref requestId);
            CancelActiveRequest();
            hookWatchdog.Stop(); topTimer.Stop(); flashTimer.Stop();
            if (mouseHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
            if (keyHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(keyHook); keyHook = IntPtr.Zero; }
            if (tray != null) tray.Dispose();
            if (frame != null) frame.Dispose();
            foreach (OverlayForm form in new OverlayForm[] { dot, panel, histIcon })
                if (form != null) form.Dispose();
            if (input != null) input.Dispose();
            hookWatchdog.Dispose(); topTimer.Dispose(); flashTimer.Dispose();
        }
        base.Dispose(disposing);
    }




    void ReinstallHooks()
    {
#if FRIEND

        if (friendMHeld && !friendMSwallowed && (Native.GetAsyncKeyState(0x4D) & 0x8000) == 0)
        {
            friendMHeld = false;
            friendPendingM = null;
        }
#endif
        if (mouseHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
        if (keyHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(keyHook); keyHook = IntPtr.Zero; }
        mouseHook = Native.SetWindowsHookEx(WH_MOUSE_LL, mouseProc, hookMod, 0);
        keyHook = Native.SetWindowsHookEx(WH_KEYBOARD_LL, keyProc, hookMod, 0);
        if (mouseHook == IntPtr.Zero || keyHook == IntPtr.Zero)
            Program.Log("Watchdog: reinstall failed, error " + Marshal.GetLastWin32Error());
    }



    IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
#if FRIEND
        if (trayMenuOpen) return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
#endif
        if (nCode >= 0)
        {
            try
            {
                int x = Marshal.ReadInt32(lParam, 0), y = Marshal.ReadInt32(lParam, 4);
                int xbtn = (Marshal.ReadInt32(lParam, 8) >> 16) & 0xFFFF;
                if (OnMouse(wParam.ToInt32(), x, y, xbtn)) return new IntPtr(1);
            }
            catch (Exception ex) { Program.Log("Mouse hook: " + ex); }
        }
        return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    IntPtr KeyHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
#if FRIEND
        if (trayMenuOpen) return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
#endif
        if (nCode >= 0)
        {
            try
            {
                int msg = wParam.ToInt32();
                int vk = Marshal.ReadInt32(lParam, 0);
                int scan = Marshal.ReadInt32(lParam, 4);

#if FRIEND
                int friendKey = HandleFriendKeys(msg, vk, scan,
                    (Native.GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0);
                if (friendKey > 0) return new IntPtr(1);
                if (friendKey < 0) return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
#endif

#if !FRIEND


                if (vk == VK_TAB)
                {
                    bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                    if (tabHeld)
                    {
                        if (!down) tabHeld = false;
                        return new IntPtr(1);
                    }
                    if (down && armed && !inputActive &&
                        (Native.GetAsyncKeyState(VK_CONTROL) & 0x8000) == 0 &&
                        (Native.GetAsyncKeyState(0x10) & 0x8000) == 0 &&
                        (Native.GetAsyncKeyState(0x12) & 0x8000) == 0 &&
                        (Native.GetAsyncKeyState(0x5B) & 0x8000) == 0 &&
                        (Native.GetAsyncKeyState(0x5C) & 0x8000) == 0)
                    {
                        tabHeld = true;
                        comboMode = false;
                        if (mode == Mode.Idle) { mode = Mode.Capture; Post(StartCapture); }
                        else { mode = Mode.Idle; Post(CancelCapture); }
                        return new IntPtr(1);
                    }
                }
#endif


                if (vk == VK_F12 && (armed || f12Held))
                {
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        if (!f12Held) { f12Held = true; Post(ToggleHistory); }
                    }
                    else f12Held = false;
                    return new IntPtr(1);
                }

                if (vk == VK_F2 && armed && !inputActive
#if FRIEND
                    && !friendChordMode
#endif
                    )
                {
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        if (!f2Held)
                        {
                            f2Held = true;
                            if (mode == Mode.Idle) { comboMode = true; mode = Mode.Capture; Post(StartCapture); }
                            else { mode = Mode.Idle; Post(CancelCapture); }
                        }
                    }
                    else f2Held = false;
                    return new IntPtr(1);
                }

#if FRIEND

                if (armed && !friendChordMode && (vk == VK_TAB || vk == VK_F1))
                {
                    bool d = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                    if (vk == VK_TAB && !inputActive)
                    {
                        if (d && !tabHeld)
                        {
                            tabHeld = true;
                            if (mode == Mode.Idle) { mode = Mode.Capture; Post(StartCapture); }
                            else { mode = Mode.Idle; Post(CancelCapture); }
                        }
                        else if (!d) tabHeld = false;
                        return new IntPtr(1);
                    }
                    if (vk == VK_F1)
                    {
                        if (d && !f1Held)
                        {
                            f1Held = true;
                            if (inputActive) Post(delegate { HideInput(); });
                            else if (mode == Mode.Idle) Post(delegate { ShowInput(); });
                        }
                        else if (!d) f1Held = false;
                        return new IntPtr(1);
                    }
                }
#endif

                if (inputActive)
                {
                    bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                    bool ctrl = (Native.GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;


                    if ((vk == VK_OEM_3 || scan == SC_GRAVE) && down && ctrl)
                    {
                        swallowHotkey = true;
                        armed = false;
                        mode = Mode.Idle;
                        Post(delegate { ShowArmed(false); });
                        return new IntPtr(1);
                    }
                    if ((vk == VK_OEM_3 || scan == SC_GRAVE) && !down && swallowHotkey)
                    { swallowHotkey = false; return new IntPtr(1); }




                    if (vk == 0x10 || vk == 0x11 || vk == 0x12 || vk == 0x14 || (vk >= 0xA0 && vk <= 0xA5))
                        return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);


                    if (vk == 0x5B || vk == 0x5C ||
                        (Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0)
                        return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

                    if (!down) return new IntPtr(1);


                    if (vk == 0x0D)
                    {
                        Post(delegate
                        {
                            if (!inputActive) return;
                            string t = input.GetText();
                            if (t.Length > 0)
                            {
                                byte[] shot = pendingScreenshot;
                                HideInput();
                                SubmitInput(t, shot);
                            }
                        });
                        return new IntPtr(1);
                    }

                    if (vk == VK_ESCAPE) { Post(delegate { HideInput(); }); return new IntPtr(1); }

                    if (vk == 0x08) { Post(delegate { input.DoBackspace(); }); return new IntPtr(1); }

                    if (vk == 0x2E) { Post(delegate { input.DoDelete(); }); return new IntPtr(1); }

                    if (vk == 0x25) { Post(delegate { input.MoveCursor(-1); }); return new IntPtr(1); }
                    if (vk == 0x27) { Post(delegate { input.MoveCursor(1); }); return new IntPtr(1); }
                    if (vk == 0x24) { Post(delegate { input.CursorHome(); }); return new IntPtr(1); }
                    if (vk == 0x23) { Post(delegate { input.CursorEnd(); }); return new IntPtr(1); }

                    if (ctrl && vk == 0x56) { Post(delegate { input.DoPaste(); }); return new IntPtr(1); }

                    if (ctrl && vk == 0x41) { Post(delegate { input.DoSelectAll(); }); return new IntPtr(1); }


                    byte[] ks = new byte[256];
                    Native.GetKeyboardState(ks);
                    SyncModifiers(ks);
                    StringBuilder buf = new StringBuilder(4);

                    IntPtr layout = Native.GetKeyboardLayout(
                        Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), IntPtr.Zero));
                    int res = Native.ToUnicodeEx((uint)vk, (uint)scan, ks, buf, buf.Capacity, 0x4, layout);
                    if (res > 0)
                    {
                        string ch = buf.ToString(0, res);
                        if (ch[0] >= ' ')
                            Post(delegate { input.InsertText(ch); });
                    }
                    return new IntPtr(1);
                }

                if (OnKey(msg, vk, scan)) return new IntPtr(1);
            }
            catch (Exception ex) { Program.Log("Key hook: " + ex); }
        }
        return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    static void SyncModifiers(byte[] s)
    {
        SyncKey(s, 0x10); SyncKey(s, 0x11); SyncKey(s, 0x12);
        SyncKey(s, 0xA0); SyncKey(s, 0xA1); SyncKey(s, 0xA2);
        SyncKey(s, 0xA3); SyncKey(s, 0xA4); SyncKey(s, 0xA5);
        short caps = Native.GetAsyncKeyState(0x14);
        s[0x14] = (byte)(((caps & 0x8000) != 0 ? 0x80 : 0) | ((caps & 1) != 0 ? 0x01 : 0));
    }

    static void SyncKey(byte[] s, int vk)
    { s[vk] = (Native.GetAsyncKeyState(vk) & 0x8000) != 0 ? (byte)0x80 : (byte)0; }

#if FRIEND
    static bool PhysicalKey(int vk, int scan, int wantedVk, int wantedScan)
    { return scan != 0 ? scan == wantedScan : vk == wantedVk; }


    internal int HandleFriendKeys(int msg, int vk, int scan, bool ctrl)
    {
        bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
        if (PhysicalKey(vk, scan, 0x4D, 0x32))
        {
            if (down)
            {
                if (!friendMHeld)
                {
                    friendMHeld = true;
                    friendMSwallowed = inputActive;
                    friendMUsed = false;
                    if (inputActive && friendChordMode) friendPendingM = InputCharacter(vk, scan);
                }
                else if (inputActive && friendChordMode && !friendMUsed)
                {
                    string repeat = InputCharacter(vk, scan);
                    if (friendPendingM != null) friendPendingM += repeat;
                    else if (repeat != null) Post(delegate { if (inputActive) input.InsertText(repeat); });
                }
                if (inputActive && friendChordMode) return 1;
                return 0;
            }
            bool swallowed = friendMSwallowed;
            friendMHeld = friendMSwallowed = friendMUsed = false;
            FlushFriendM();
            return swallowed ? 1 : -1;
        }

        int action = PhysicalKey(vk, scan, 0xBC, 0x33) ? 1
            : PhysicalKey(vk, scan, 0x4E, 0x31) ? 2
            : PhysicalKey(vk, scan, 0x42, 0x30) ? 4
            : PhysicalKey(vk, scan, 0x4B, 0x25) ? 8
            : PhysicalKey(vk, scan, VK_OEM_3, SC_GRAVE) ? 16 : 0;
        if (action != 0 && (friendHeldActions & action) != 0)
        {
            if (!down) friendHeldActions &= ~action;
            return 1;
        }
        if (down && ((action == 16 && ctrl) || (action == 1 && friendMHeld)))
        {
            friendHeldActions |= action;
            if (action == 1) friendMUsed = true;
            friendPendingM = null;
            SelectFriendMode(action == 1);
            return 1;
        }
        if (down && armed && friendChordMode && friendMHeld &&
            (action == 2 || action == 4 || action == 8))
        {
            friendHeldActions |= action;
            friendMUsed = true;
            friendPendingM = null;
            if (action == 2)
            {
                if (mode == Mode.Idle)
                {
                    comboMode = false;
                    mode = Mode.Capture;
                    Post(delegate { HideInput(); StartCapture(); });
                }
                else { mode = Mode.Idle; Post(CancelCapture); }
            }
            else if (action == 4)
            {
                if (inputActive) Post(HideInput);
                else if (mode == Mode.Idle) Post(ShowInput);
            }
            else if (mode == Mode.Idle)
                Post(delegate { HideInput(); ToggleVisibility(); });
            return 1;
        }
        if (down && vk != 0x10 && vk != 0x11 && vk != 0x12 && vk != 0x14 &&
            !(vk >= 0xA0 && vk <= 0xA5)) FlushFriendM();
        return 0;
    }

    void SelectFriendMode(bool chord)
    {
        bool on = !armed || friendChordMode != chord;
        friendChordMode = chord;
        armed = on;
        mode = Mode.Idle;
        Interlocked.Increment(ref requestId);
        Post(delegate { ShowArmed(false); if (on) ShowArmed(true); });
    }

    void ResetFriendKeys()
    {
        FlushFriendM();
        if ((Native.GetAsyncKeyState(VK_F12) & 0x8000) == 0) f12Held = false;
        friendMHeld = friendMSwallowed = friendMUsed = false;
        friendHeldActions = 0;
    }

    static string InputCharacter(int vk, int scan)
    {
        byte[] state = new byte[256];
        Native.GetKeyboardState(state);
        SyncModifiers(state);
        StringBuilder buf = new StringBuilder(4);
        IntPtr layout = Native.GetKeyboardLayout(
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), IntPtr.Zero));
        int count = Native.ToUnicodeEx((uint)vk, (uint)scan, state, buf, buf.Capacity, 0x4, layout);
        return count > 0 && buf[0] >= ' ' ? buf.ToString(0, count) : null;
    }

    void FlushFriendM()
    {
        string text = friendPendingM;
        friendPendingM = null;
        if (text != null) Post(delegate { if (inputActive) input.InsertText(text); });
    }
#endif


    internal bool OnKey(int msg, int vk, int scan)
    {
        bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;

        if (vk == VK_OEM_3 || scan == SC_GRAVE)
        {
            if (down)
            {
                if (swallowHotkey) return true;
                if ((Native.GetAsyncKeyState(VK_CONTROL) & 0x8000) == 0) return false;
                swallowHotkey = true;
                armed = !armed;
                if (!armed) mode = Mode.Idle;
                bool on = armed;
                Post(delegate { ShowArmed(on); });
                return true;
            }
            if (swallowHotkey) { swallowHotkey = false; return true; }
            return false;
        }



        if (armed && vk >= 0x25 && vk <= 0x28)
        {
            bool steer = down && hasAnswer && !panelHidden && mode == Mode.Idle &&
                (Native.GetAsyncKeyState(0x10) & 0x8000) == 0 && (Native.GetAsyncKeyState(VK_CONTROL) & 0x8000) == 0 &&
                (Native.GetAsyncKeyState(0x12) & 0x8000) == 0 && (Native.GetAsyncKeyState(0x5B) & 0x8000) == 0 &&
                (Native.GetAsyncKeyState(0x5C) & 0x8000) == 0;
            if (steer)
            {
                if (vk == 0x26) Post(delegate { SetPale(true); });
                else if (vk == 0x28) Post(delegate { SetPale(false); });
                else Post(delegate { SetSide(vk == 0x27); });
            }
            return true;
        }

        if (vk == VK_ESCAPE && down && mode != Mode.Idle)
        {
            mode = Mode.Idle;
            Post(CancelCapture);
            return true;
        }
        return false;
    }


    internal bool OnMouse(int msg, int x, int y, int xbtn)
    {
        if (!armed) return false;
#if FRIEND
        if (friendChordMode && (msg == WM_MBUTTONDOWN || msg == WM_MBUTTONUP)) return false;
#endif

        if (inputActive)
        {
            if ((msg == WM_XBUTTONDOWN || msg == WM_XBUTTONUP) && xbtn == 1)
            {
                if (msg == WM_XBUTTONDOWN) Post(delegate { HideInput(); });
                return true;
            }
            if (msg == WM_XBUTTONDOWN || msg == WM_XBUTTONUP || msg == WM_MBUTTONDOWN || msg == WM_MBUTTONUP) return true;
            return false;
        }

#if !FRIEND

        if ((msg == WM_XBUTTONDOWN || msg == WM_XBUTTONUP) && xbtn == Settings.TriggerXButton)
        {
            if (msg == WM_XBUTTONDOWN)
            {
                if (mode == Mode.Idle) { mode = Mode.Capture; Post(StartCapture); }
                else { mode = Mode.Idle; Post(CancelCapture); }
            }
            return true;
        }

        if ((msg == WM_XBUTTONDOWN || msg == WM_XBUTTONUP) && xbtn == 1)
        {
            if (msg == WM_XBUTTONDOWN && mode == Mode.Idle) Post(delegate { ShowInput(); });
            return true;
        }
#endif

        if (mode == Mode.Capture)
        {
            if (msg == WM_LBUTTONDOWN)
            {
                mode = Mode.Drag;
                dragOrigin = lastMove = new Point(x, y);
                Post(delegate { UpdateFrame(lastMove); });
                return true;
            }
            if (msg == WM_RBUTTONDOWN)
            {
                mode = Mode.Idle;
                swallowRUp = true;
                Post(CancelCapture);
                return true;
            }
        }
        else if (mode == Mode.Drag)
        {
            if (msg == WM_MOUSEMOVE)
            {
                lastMove = new Point(x, y);
                if (!movePending)
                {
                    movePending = true;
                    Post(delegate { movePending = false; if (mode == Mode.Drag) UpdateFrame(lastMove); });
                }
                return false;
            }
            if (msg == WM_LBUTTONUP)
            {
                mode = Mode.Idle;
                Point a = dragOrigin, b = new Point(x, y);
                Post(delegate { FinishDrag(a, b); });
                return true;
            }
        }

        if (msg == WM_RBUTTONUP && swallowRUp) { swallowRUp = false; return true; }


        if (msg == WM_MBUTTONDOWN) { if (mode == Mode.Idle) Post(ToggleVisibility); return true; }
        if (msg == WM_MBUTTONUP) return true;
        return false;
    }



    void ShowArmed(bool on)
    {
#if !FRIEND
        Program.Log(on ? "Armed (Ctrl+`)" : "Disarmed (Ctrl+`)");
#endif
        UpdateTray();
        if (!on)
        {
#if FRIEND
            pendingScreenshot = null;
            comboMode = false;
            tabHeld = f1Held = f2Held = false;
            friendPendingM = null;
#endif
            Interlocked.Increment(ref requestId);
            CancelActiveRequest();
            thinking = false;
            hasAnswer = false;
            frame.Hide();
            panel.Hide();
            if (inputActive) { inputActive = false; input.CloseInput(); }
            histIcon.Hide();
        }
        else FlashHistoryIcon();
        Flash(on ? Green : Gray, 700);
    }

    void SetPale(bool pale) { panel.SetPale(pale); }


    void SetSide(bool right)
    {
        if (rightSide == right) return;
        rightSide = right;
        panel.MoveTo(CornerRect(panel.Width, panel.Height));
        if (dot.Visible) dot.Location = CornerRect(dot.Width, dot.Height).Location;
        if (histIcon.Visible) histIcon.Location = CornerIconRect().Location;
    }

    void ShowDot(Color c)
    {
        flashTimer.Stop();
        dot.Location = CornerRect(dot.Width, dot.Height).Location;
        dot.SetColor(c);
        dot.ShowTop();
    }

    void Flash(Color c, int ms)
    {
        ShowDot(c);
        flashTimer.Interval = ms;
        flashTimer.Start();
    }

    void HideDot()
    {
        flashTimer.Stop();
        dot.Hide();
    }

    void RestoreDot()
    {
        if (thinking && !hasAnswer && !panelHidden) ShowDot(Amber); else HideDot();
    }


    void StartCapture()
    {
        panel.Hide();
        ShowDot(Red);
    }

    void CancelCapture()
    {
        frame.Hide();
        if (hasAnswer && !panelHidden) panel.ShowTop();
        RestoreDot();
    }

    static Rectangle Norm(Point a, Point b)
    {
        return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    void UpdateFrame(Point p) { }

    internal void FinishDrag(Point a, Point b)
    {
        frame.Hide();
        dot.Hide();
        panel.Hide();
        Rectangle r = Norm(a, b);
        if (r.Width < 8 || r.Height < 8)
        {
            if (hasAnswer && !panelHidden) panel.ShowTop();
            RestoreDot();
            return;
        }

#if FRIEND
        int captureRequest = requestId;
        After(60, delegate { if (armed && captureRequest == requestId) CaptureAndAsk(r); });
#else
        After(60, delegate { CaptureAndAsk(r); });
#endif
    }

    void After(int ms, Action a)
    {
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        t.Interval = ms;
        t.Tick += delegate { t.Stop(); t.Dispose(); a(); };
        t.Start();
    }

    void CaptureAndAsk(Rectangle r)
    {
        Bitmap bmp = null;
        try
        {
            bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bmp))
                g.CopyFromScreen(r.Location, Point.Empty, r.Size);
        }
        catch (Exception error)
        {
            if (bmp != null) bmp.Dispose();
            comboMode = false;
            pendingScreenshot = null;
            Program.Log("Capture failed: " + error.Message);
            OnError(BeginRequest(), "Не удалось получить изображение экрана.");
            return;
        }

        if (comboMode)
        {
            comboMode = false;
            try
            {
                using (bmp)
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pendingScreenshot = ms.ToArray();
                }
                ShowInput();
#if !FRIEND
                Program.Log("Combo: screenshot saved, opening text input");
#endif
            }
            catch (Exception error)
            {
                pendingScreenshot = null;
                Program.Log("Image encoding failed: " + error.Message);
                OnError(BeginRequest(), "Не удалось подготовить изображение экрана.");
            }
            return;
        }

        int id = BeginRequest();
        object[] prior = historyOn ? history.ToArray() : new object[0];

        Thread t = new Thread(delegate ()
        {
            try
            {
                byte[] png;
                using (bmp)
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    png = ms.ToArray();
                }
                Ask(id, Settings.SystemPrompt, prior, ImageMsg(png));
            }
            catch (Exception error)
            {
                Program.Log("Image encoding failed: " + error.Message);
                Post(delegate { OnError(id, "Не удалось подготовить изображение экрана."); });
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    void OnChunk(int id, string s)
    {
        if (id != requestId) return;
        answer.Append(s);
        if (!inputActive) HideDot();
        Render(answer.ToString());
    }

    int BeginRequest()
    {
        int id = Interlocked.Increment(ref requestId);
        CancelActiveRequest();
        requestEpoch = historyEpoch;
        thinking = true;
        hasAnswer = false;
        panelHidden = false;
        answer = new StringBuilder();
        ShowDot(Amber);
        return id;
    }

    void OnDone(int id, Dictionary<string, object> userMsg)
    {
        if (id != requestId) return;
        thinking = false;
        if (!inputActive) HideDot();
        string a = answer.ToString().Trim();
        if (a.Length == 0)
        {
            hasAnswer = false;
            panel.Hide();
            return;
        }

        if (historyOn && requestEpoch == historyEpoch)
        {
            history.Add(userMsg);
            history.Add(Msg("assistant", a));
        }
    }

    void ToggleHistory()
    {
        historyOn = !historyOn;
        historyEpoch++;
        history.Clear();
#if !FRIEND
        Program.Log(historyOn ? "History on (F12)" : "History off and cleared (F12)");
#endif
        UpdateTray();
        FlashHistoryIcon();
    }


    void FlashHistoryIcon()
    {
#if !FRIEND
        int token = ++histIconToken;
        histIcon.SetCrossed(!historyOn);
        histIcon.Location = CornerIconRect().Location;
        histIcon.ShowTop();
        After(1200, delegate { if (token == histIconToken) histIcon.Hide(); });
#endif
    }


    Rectangle CornerIconRect()
    {
        Rectangle b = OverlayArea();
        int m = S(Settings.Margin), s = histIcon.Width;
        int x = !rightSide ? b.Left + m : b.Right - m - s;
        int y = Settings.PanelCorner.StartsWith("top") ? b.Top + m : b.Bottom - m - s;
        return new Rectangle(x, y, s, s);
    }

    void OnError(int id, string message)
    {
        if (id != requestId) return;
        thinking = false;
        if (!inputActive) HideDot();
        Render("Ошибка: " + message);
    }

    internal void Render(string text)
    {
        hasAnswer = true;
        int w = panel.FitWidth(text, S(Settings.PanelWidth));
        int h = Math.Min(panel.MeasureHeight(text, w), Screen.PrimaryScreen.Bounds.Height * 8 / 10);
        panel.SetContent(text, CornerRect(w, h));
        if (!panelHidden && !inputActive) panel.ShowTop();
    }



    static Rectangle OverlayArea()
    {
        Rectangle b = Screen.PrimaryScreen.Bounds;
        uint edge;
        Rectangle t = Native.TaskbarRect(out edge);
        if (t.Width <= 0 || t.Height <= 0 || !b.IntersectsWith(t)) return b;
        switch (edge)
        {
            case 0: return Rectangle.FromLTRB(Math.Max(b.Left, t.Right), b.Top, b.Right, b.Bottom);
            case 1: return Rectangle.FromLTRB(b.Left, Math.Max(b.Top, t.Bottom), b.Right, b.Bottom);
            case 2: return Rectangle.FromLTRB(b.Left, b.Top, Math.Min(b.Right, t.Left), b.Bottom);
            default: return Rectangle.FromLTRB(b.Left, b.Top, b.Right, Math.Min(b.Bottom, t.Top));
        }
    }

    Rectangle CornerRect(int w, int h)
    {
        Rectangle b = OverlayArea();
        int m = S(Settings.Margin), off = histIcon.Width + S(4);
        int x = !rightSide ? b.Left + m + off : b.Right - m - off - w;
        int y = Settings.PanelCorner.StartsWith("top") ? b.Top + m : b.Bottom - m - h;
        return new Rectangle(x, y, w, h);
    }

    void ToggleVisibility()
    {
        if (!hasAnswer && !thinking) return;
        panelHidden = !panelHidden;
        if (panelHidden)
        {
            panel.Hide();
            if (thinking) HideDot();
        }
        else
        {
            if (hasAnswer) panel.ShowTop();
            RestoreDot();
        }
    }

    void ShowInput()
    {
        if (inputActive) return;
        inputActive = true;
        panel.Hide();
        input.Open();
        ShowDot(Color.FromArgb(80, 160, 255));
#if !FRIEND
        Program.Log("Text input opened");
#endif
    }

    void HideInput()
    {
        if (!inputActive) return;
        inputActive = false;
        pendingScreenshot = null;
        input.CloseInput();
        if (hasAnswer && !panelHidden) panel.ShowTop();
        RestoreDot();
#if !FRIEND
        Program.Log("Text input closed");
#endif
    }

    void SubmitInput(string text, byte[] combo)
    {
#if !FRIEND
        Program.Log(combo == null ? "Text query, " + text.Length + " chars" : "Combo query: screenshot + " + text.Length + " chars");
#endif
        int id = BeginRequest();
        object[] prior = historyOn ? history.ToArray() : new object[0];

        Dictionary<string, object> userMsg;
        string prompt;
        if (combo != null)
        {
            userMsg = Msg("user", new object[] {
                new Dictionary<string, object> { { "type", "text" }, { "text", text } },
                new Dictionary<string, object> { { "type", "image_url" }, { "image_url", new Dictionary<string, object> {
                    { "url", "data:image/png;base64," + Convert.ToBase64String(combo) },
                    { "detail", "high" } } } }
            });
            prompt = Settings.SystemPrompt;
        }
        else
        {
            userMsg = Msg("user", text);
            prompt = Settings.TextSystemPrompt;
        }

        Thread t = new Thread(delegate() { Ask(id, prompt, prior, userMsg); });
        t.IsBackground = true;
        t.Start();
    }



    static Dictionary<string, object> Msg(string role, object content)
    {
        return new Dictionary<string, object> { { "role", role }, { "content", content } };
    }

    static Dictionary<string, object> ImageMsg(byte[] png)
    {
        return Msg("user", new object[] {
            new Dictionary<string, object> { { "type", "text" }, { "text", Settings.UserPrompt } },
            new Dictionary<string, object> { { "type", "image_url" }, { "image_url", new Dictionary<string, object> {
                { "url", "data:image/png;base64," + Convert.ToBase64String(png) },
                { "detail", "high" } } } }
        });
    }

    void Ask(int id, string systemPrompt, object[] prior, Dictionary<string, object> userMsg)
    {
        try
        {
            string key = null;

            List<object> messages = new List<object>();
            messages.Add(Msg("system", systemPrompt));
            messages.AddRange(prior);
            messages.Add(userMsg);

            JavaScriptSerializer json = new JavaScriptSerializer();
            json.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> request = new Dictionary<string, object> {
                { "model", Program.Model() },
                { "reasoning_effort", Settings.ReasoningEffort },
                { "stream", true },
                { "messages", messages }
            };

            string body = json.Serialize(request);
            bool done = TryAllEndpoints(id, key, json, request, body);
            if (done) Post(delegate { OnDone(id, userMsg); });
        }
        catch (WebException ex)
        {
            string msg = ErrorMessage(ex);
            Program.Log("OpenAI error: " + msg);
            Post(delegate { OnError(id, msg); });
        }
        catch (OpenAIException ex)
        {
            Program.Log("OpenAI error: " + ex.Message);
            Post(delegate { OnError(id, ex.Message); });
        }
        catch (Exception ex)
        {
            Program.Log("Request failed: " + ex);
            Post(delegate { OnError(id, ex.Message); });
        }
    }

    HttpWebRequest Send(int id, string key, string body, WebProxy proxy, string relayUrl)
    {
        if (string.IsNullOrWhiteSpace(relayUrl)) throw new InvalidOperationException("Посредник не настроен.");
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(relayUrl);
        if (Settings.AccessToken.Length > 0) req.Headers["X-Access"] = Settings.AccessToken;
        req.Method = "POST";
        req.ContentType = "application/json";
        req.Timeout = 180000;
        req.ReadWriteTimeout = 180000;
        if (proxy != null) req.Proxy = proxy;
        byte[] data = Encoding.UTF8.GetBytes(body);
        req.ContentLength = data.Length;
        lock (requestLock)
        {
            if (disposed || id != Thread.VolatileRead(ref requestId)) throw new OperationCanceledException();
            activeRequest = req;
        }
        try { using (Stream s = req.GetRequestStream()) s.Write(data, 0, data.Length); }
        catch { FinishRequest(req); req.Abort(); throw; }
        return req;
    }

    bool AskStreaming(int id, string key, JavaScriptSerializer json, string body, WebProxy proxy, string relayUrl)
    {
        if (id != Thread.VolatileRead(ref requestId)) return false;
        HttpWebRequest req = Send(id, key, body, proxy, relayUrl);
        bool completed = false, receivedContent = false;
        try
        {
        using (WebResponse resp = req.GetResponse())
        using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (id != Thread.VolatileRead(ref requestId)) { req.Abort(); return false; }
                if (!line.StartsWith("data:")) continue;
                string payload = line.Substring(5).Trim();
                if (payload == "[DONE]")
                {
                    completed = true;
                    break;
                }
                string error = ExtractError(payload);
                if (error != null) throw new OpenAIException(error);
                string delta = ExtractDelta(json, payload);
                if (!string.IsNullOrWhiteSpace(delta)) receivedContent = true;
                if (!string.IsNullOrEmpty(delta)) Post(delegate { OnChunk(id, delta); });
            }
        }
        }
        finally { FinishRequest(req); }
        if (id != Thread.VolatileRead(ref requestId)) return false;
        if (!completed) throw new OpenAIException("Ответ сервера оборвался. Повторите запрос.");
        if (!receivedContent) throw new OpenAIException("Сервер завершил запрос без ответа. Повторите запрос.");
        return true;
    }

    void AskOnce(int id, string key, JavaScriptSerializer json, string body, WebProxy proxy, string relayUrl)
    {
#if FRIEND
        if (id != Thread.VolatileRead(ref requestId)) return;
#endif
        HttpWebRequest req = Send(id, key, body, proxy, relayUrl);
        string text;
        try
        {
        using (WebResponse resp = req.GetResponse())
        using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
#if FRIEND
        {
            string bodyText = reader.ReadToEnd();
            string error = ExtractError(bodyText);
            if (error != null) throw new OpenAIException(error);
            text = ExtractMessage(json, bodyText);
            if (string.IsNullOrWhiteSpace(text))
                throw new OpenAIException("Сервер завершил запрос без ответа. Повторите запрос.");
        }
#else
            text = ExtractMessage(json, reader.ReadToEnd());
#endif
        }
        finally { FinishRequest(req); }
        if (!string.IsNullOrEmpty(text)) Post(delegate { OnChunk(id, text); });
    }

    bool TryAllEndpoints(int id, string key, JavaScriptSerializer json,
        Dictionary<string, object> request, string body)
    {
        string[] relays = new string[] { Settings.ProxyUrl, Settings.FallbackUrl };
        WebProxy proxy = null;
        for (int route = 0; route < 2; route++)
        {
            if (route == 1)
            {
                proxy = Program.GetProxy();
                if (proxy == null) break;
            }
            foreach (string relay in relays)
            {
                if (string.IsNullOrWhiteSpace(relay)) continue;
                if (id != Thread.VolatileRead(ref requestId)) return false;
                try { return SendWithFallback(id, key, json, request, body, proxy, relay); }
                catch (WebException error)
                {
                    if (!IsNetworkError(error)) throw;
                    if (error.Response != null) error.Response.Close();
                }
            }
        }
        throw new WebException("Посредники временно недоступны.", WebExceptionStatus.ConnectFailure);
    }

    static bool IsNetworkError(WebException ex)
    {
#if FRIEND
        if (ex.Status == WebExceptionStatus.Timeout) return true;
        HttpWebResponse response = ex.Response as HttpWebResponse;
        if (ex.Status == WebExceptionStatus.ProtocolError && response != null &&
            (response.StatusCode == HttpStatusCode.RequestTimeout ||
             response.StatusCode == HttpStatusCode.BadGateway ||
             response.StatusCode == HttpStatusCode.ServiceUnavailable ||
             response.StatusCode == HttpStatusCode.GatewayTimeout)) return true;
#endif
        return ex.Status == WebExceptionStatus.ConnectFailure
            || ex.Status == WebExceptionStatus.NameResolutionFailure
            || ex.Status == WebExceptionStatus.SecureChannelFailure
            || ex.Status == WebExceptionStatus.ConnectionClosed
            || ex.Status == WebExceptionStatus.SendFailure
            || ex.Status == WebExceptionStatus.ReceiveFailure;
    }

    bool SendWithFallback(int id, string key, JavaScriptSerializer json,
        Dictionary<string, object> request, string body, WebProxy proxy, string relayUrl)
    {
#if FRIEND
        if (id != Thread.VolatileRead(ref requestId)) return false;
#endif
        try
        {
            return AskStreaming(id, key, json, body, proxy, relayUrl);
        }
        catch (WebException ex)
        {
            if (IsNetworkError(ex)) throw;
            string msg = ErrorMessage(ex);
            if (msg.IndexOf("stream", StringComparison.OrdinalIgnoreCase) < 0) throw new OpenAIException(msg);
            Program.Log("Streaming refused (" + msg + "), retrying without streaming");
#if FRIEND
            if (id != Thread.VolatileRead(ref requestId)) return false;
#endif
            request["stream"] = false;
            AskOnce(id, key, json, json.Serialize(request), proxy, relayUrl);
#if FRIEND
            return id == Thread.VolatileRead(ref requestId);
#else
            return true;
#endif
        }
    }

    static string ErrorMessage(WebException ex)
    {
        if (ex.Response != null)
        {
            try
            {
                using (StreamReader r = new StreamReader(ex.Response.GetResponseStream()))
                {
                    string m = ExtractError(r.ReadToEnd());
                    if (m != null) return m;
                }
            }
            catch { }
        }
        return ex.Message;
    }

    static string ExtractMessage(JavaScriptSerializer json, string body)
    {
        Dictionary<string, object> obj = json.Deserialize<Dictionary<string, object>>(body);
        object o;
        if (!obj.TryGetValue("choices", out o)) return null;
        ArrayList choices = o as ArrayList;
        if (choices == null || choices.Count == 0) return null;
        Dictionary<string, object> c0 = choices[0] as Dictionary<string, object>;
        if (c0 == null || !c0.TryGetValue("message", out o)) return null;
        Dictionary<string, object> msg = o as Dictionary<string, object>;
        if (msg == null || !msg.TryGetValue("content", out o)) return null;
        return o as string;
    }

    static string ExtractDelta(JavaScriptSerializer json, string payload)
    {
        Dictionary<string, object> obj = json.Deserialize<Dictionary<string, object>>(payload);
        object o;
        if (!obj.TryGetValue("choices", out o)) return null;
        ArrayList choices = o as ArrayList;
        if (choices == null || choices.Count == 0) return null;
        Dictionary<string, object> c0 = choices[0] as Dictionary<string, object>;
        if (c0 == null || !c0.TryGetValue("delta", out o)) return null;
        Dictionary<string, object> delta = o as Dictionary<string, object>;
        if (delta == null || !delta.TryGetValue("content", out o)) return null;
        return o as string;
    }

    static string ExtractError(string body)
    {
        try
        {
            Dictionary<string, object> obj = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
            object o;
            if (obj.TryGetValue("error", out o))
            {
                Dictionary<string, object> err = o as Dictionary<string, object>;
                if (err != null && err.TryGetValue("message", out o)) return o as string;
            }
        }
        catch { }
        return null;
    }
}

sealed class OpenAIException : Exception
{
    public OpenAIException(string message) : base(message) { }
}



static class KeyStore
{
    static readonly byte[] Salt = Encoding.UTF8.GetBytes(" ");

    public static string Protect(string plain)
    {
        byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Salt, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(enc);
    }

    public static string Unprotect(string stored)
    {
        byte[] dec = ProtectedData.Unprotect(Convert.FromBase64String(stored), Salt, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(dec);
    }
}

static class Program
{
#if FRIEND

    static int CheckHotkeySetup()
    {
        IntPtr keyboard = IntPtr.Zero, mouse = IntPtr.Zero;
        Native.HookProc pass = delegate(int code, IntPtr w, IntPtr l)
        { return Native.CallNextHookEx(IntPtr.Zero, code, w, l); };
        try
        {
            IntPtr module = Native.GetModuleHandle(null);
            keyboard = Native.SetWindowsHookEx(13, pass, module, 0);
            mouse = Native.SetWindowsHookEx(14, pass, module, 0);
            if (keyboard == IntPtr.Zero && mouse == IntPtr.Zero) return 23;
            if (keyboard == IntPtr.Zero) return 21;
            if (mouse == IntPtr.Zero) return 22;
            return 0;
        }
        catch { return 24; }
        finally
        {
            if (keyboard != IntPtr.Zero) Native.UnhookWindowsHookEx(keyboard);
            if (mouse != IntPtr.Zero) Native.UnhookWindowsHookEx(mouse);
            GC.KeepAlive(pass);
        }
    }
#endif
#if !FRIEND
    static readonly object logLock = new object();
#endif
    internal static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
#if FRIEND
    const string AutostartName = "Edge";
#else
    const string AutostartName = "Windows Audio";
#endif

#if FRIEND
    public const int TrialHours = 3;
    const string TrialFile = ".edge-expiry";
    public static DateTime Deadline;
    static System.Threading.Timer trialTimer;
    static Stopwatch trialClock;
    static int removalStarted;

    public static bool InitTrial()
    {
        string path = Path.Combine(BaseDir, TrialFile);
        if (File.Exists(path))
        {
            long ticks;
            if (!long.TryParse(File.ReadAllText(path).Trim(), out ticks) ||
                ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
                throw new InvalidDataException("The application expiry record is invalid.");
            Deadline = new DateTime(ticks, DateTimeKind.Utc);
        }
        else
        {
            Deadline = DateTime.UtcNow.AddHours(TrialHours);

            string temp = path + ".new";
            try
            {
                File.WriteAllText(temp, Deadline.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
                File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        return DateTime.UtcNow >= Deadline;
    }

    static void StartTrialTimer()
    {
        double remaining = Math.Max(0, (Deadline - DateTime.UtcNow).TotalMilliseconds);
        int due = (int)Math.Min(TimeSpan.FromHours(TrialHours).TotalMilliseconds, Math.Ceiling(remaining));
        trialClock = Stopwatch.StartNew();

        trialTimer = new System.Threading.Timer(delegate
        {
            if (DateTime.UtcNow >= Deadline || trialClock.ElapsedMilliseconds >= due) SelfDestruct();
        }, null, Math.Min(due, 1000), 1000);
    }

    static ProcessStartInfo SelfRemovalCommand()
    {
        string expected = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Project_Alim"));
        string actual = Path.GetFullPath(BaseDir).TrimEnd('\\', '/');
        if (!string.Equals(actual, expected.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFullPath(Application.ExecutablePath),
                           Path.Combine(expected, "Edge.exe"), StringComparison.OrdinalIgnoreCase))
            return null;


        for (DirectoryInfo directory = new DirectoryInfo(expected); directory != null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) return null;



        string command = "/d /q /v:off /c for /l %i in (1,1,15) do @if exist \"%JARVIS_REMOVE_DIR%\\.edge-removing\" " +
            "if exist \"%JARVIS_REMOVE_DIR%\\Edge.exe\" " +
            "(del /q \"%JARVIS_REMOVE_DIR%\\Edge.exe\" >nul 2>&1 & " +
            "if exist \"%JARVIS_REMOVE_DIR%\\Edge.exe\" ping -n 2 127.0.0.1 >nul 2>&1) & " +
            "if exist \"%JARVIS_REMOVE_DIR%\\.edge-removing\" if not exist \"%JARVIS_REMOVE_DIR%\\Edge.exe\" " +
            "(del /q \"%JARVIS_REMOVE_DIR%\\icon.png\" \"%JARVIS_REMOVE_DIR%\\.edge-expiry\" \"%JARVIS_REMOVE_DIR%\\.edge-expiry.new\" " +
            "\"%JARVIS_REMOVE_DIR%\\.edge-subscription.dpapi\" \"%JARVIS_REMOVE_DIR%\\.edge-subscription-host\" " +
            "\"%JARVIS_REMOVE_DIR%\\.edge-removing\" >nul 2>&1 & " +
            "rd \"%JARVIS_REMOVE_DIR%\" >nul 2>&1) & " +
            "if exist \"%JARVIS_REMOVE_DIR%\\Edge.exe\" del /q \"%JARVIS_REMOVE_DIR%\\.edge-removing\" >nul 2>&1";
        ProcessStartInfo start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), command);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.WindowStyle = ProcessWindowStyle.Hidden;
        start.WorkingDirectory = Environment.SystemDirectory;
        start.EnvironmentVariables["JARVIS_REMOVE_DIR"] = expected;
        return start;
    }

    public static void SelfDestruct()
    {
        if (Interlocked.Exchange(ref removalStarted, 1) != 0) return;
        try
        {
            ProcessStartInfo command = SelfRemovalCommand();
            if (command != null)
            {

                using (FileStream marker = new FileStream(Path.Combine(BaseDir, ".edge-removing"),
                       FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
                try { SetAutostart(false); } catch { }
                try
                {
                    if (Process.Start(command) == null) throw new InvalidOperationException();
                }
                catch
                {
                    try { File.Delete(Path.Combine(BaseDir, ".edge-removing")); } catch { }
                }
            }
        }
        catch { }

        Environment.Exit(0);
    }
#else

    public const int TrialHours = 3;
    const string TrialKey = @"Software\Edge";
    public static DateTime Deadline = DateTime.MaxValue;


    public static bool InitTrial()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(TrialKey))
            {
                object v = k.GetValue("deadline");

                if (v == null)
                    using (RegistryKey legacy = Registry.CurrentUser.OpenSubKey(@"Software\WindowsAudio"))
                        if (legacy != null) v = legacy.GetValue("deadline");
                if (v == null)
                {
                    Deadline = DateTime.UtcNow.AddHours(TrialHours);
                    k.SetValue("deadline", Deadline.ToBinary().ToString());
                }
                else
                {
                    Deadline = DateTime.FromBinary(Convert.ToInt64((string)v));
                    k.SetValue("deadline", Deadline.ToBinary().ToString());
                }

                try
                {
                    using (RegistryKey legacy = Registry.CurrentUser.OpenSubKey(@"Software\WindowsAudio", true))
                        if (legacy != null && string.Equals(legacy.GetValue("deadline") as string,
                                                            Deadline.ToBinary().ToString(), StringComparison.Ordinal))
                            legacy.DeleteValue("deadline", false);
                }
                catch (Exception ex) { Log("Legacy trial cleanup failed: " + ex.Message); }
            }
        }
        catch { Deadline = DateTime.UtcNow.AddHours(TrialHours); }
        return DateTime.UtcNow >= Deadline;
    }



    public static void SelfDestruct()
    {
#if FRIEND
        string expectedDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Project_Alim");
        if (!string.Equals(Path.GetFullPath(BaseDir).TrimEnd('\\', '/'),
                           Path.GetFullPath(expectedDir).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
        {
            Log("Trial expired outside the installation folder; exiting without removing files.");
            Environment.Exit(0);
            return;
        }
#endif
        try { SetAutostart(false); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(TrialKey, false); } catch { }
        try
        {
            string dir = BaseDir.TrimEnd('\\', '/');
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
                "/c ping 127.0.0.1 -n 3 >nul & rd /s /q \"" + dir + "\"");
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.UseShellExecute = false;
            Process.Start(psi);
        }
        catch { }
        Environment.Exit(0);
    }
#endif

    public static void Log(string s)
    {
#if !FRIEND
        try
        {
            lock (logLock)
                File.AppendAllText(Path.Combine(BaseDir, "jarvis.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + s + Environment.NewLine);
        }
        catch { }
#endif
    }

    public static string ApiKey()
    {
        string k = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(k))
        {
            string dat = Path.Combine(BaseDir, "key.dat");
            if (File.Exists(dat)) { try { k = KeyStore.Unprotect(File.ReadAllText(dat)); } catch { } }
        }
        if (string.IsNullOrWhiteSpace(k))
        {
#if FRIEND
            string f = Path.Combine(BaseDir, "hw.txt");
#else
            string f = Path.Combine(BaseDir, "openai_key.txt");
#endif
            if (File.Exists(f)) k = File.ReadAllText(f);
        }
        return string.IsNullOrWhiteSpace(k) ? null : k.Trim();
    }

    public static string Model()
    {
        string m = Environment.GetEnvironmentVariable("OPENAI_MODEL");
        return string.IsNullOrWhiteSpace(m) ? Settings.DefaultModel : m.Trim();
    }

    static WebProxy vpnProxy;

    public static WebProxy GetProxy()
    {
        if (vpnProxy != null) return vpnProxy;
        string url = Environment.GetEnvironmentVariable("JARVIS_PROXY");
        if (string.IsNullOrWhiteSpace(url))
        {
            string f = Path.Combine(BaseDir, "proxy.txt");
            if (File.Exists(f))
                try { url = File.ReadAllText(f).Trim(); } catch { }
        }
        if (string.IsNullOrWhiteSpace(url)) return null;
        try { vpnProxy = new WebProxy(url); Log("Proxy loaded: " + url); }
        catch (Exception ex) { Log("Bad proxy URL: " + ex.Message); }
        return vpnProxy;
    }

    static void SetAutostart(bool on)
    {
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
        {
#if FRIEND

            string legacy = k.GetValue("Windows Audio") as string;
            string oldPath = Path.Combine(BaseDir, "Windows Audio.exe");
            if (legacy != null && (string.Equals(legacy, "\"" + oldPath + "\"", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(legacy, oldPath, StringComparison.OrdinalIgnoreCase)))
                k.DeleteValue("Windows Audio", false);
#endif
            if (on) k.SetValue(AutostartName, "\"" + Application.ExecutablePath + "\"");
            else
            {
#if FRIEND
                if (string.Equals(k.GetValue(AutostartName) as string,
                                  "\"" + Application.ExecutablePath + "\"", StringComparison.OrdinalIgnoreCase))
                    k.DeleteValue(AutostartName, false);
#else
                k.DeleteValue(AutostartName, false);
#endif
            }
        }
        Program.Log("Autostart " + (on ? "enabled" : "disabled"));
    }



    static void MakeKey()
    {
        string txt = Path.Combine(BaseDir, "openai_key.txt");
        if (!File.Exists(txt)) { Log("makekey: openai_key.txt not found"); return; }
        try
        {
            string plain = File.ReadAllText(txt).Trim();
            File.WriteAllText(Path.Combine(BaseDir, "key.dat"), KeyStore.Protect(plain));
            File.Delete(txt);
            Log("makekey: key.dat created, openai_key.txt deleted");
        }
        catch (Exception e) { Log("makekey error: " + e.Message); }
    }

    internal static void InitProcess()
    {

        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | (SecurityProtocolType)12288; } catch { }
        try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e) { Log("UI error: " + e.Exception); };
        AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e) { Log("Fatal: " + e.ExceptionObject); };
    }

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--connect-subscriptions")
        {
            InitProcess();
            try { SubscriptionAuth.Connect(); }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "Подключение аккаунта", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.ExitCode = 27;
            }
            return;
        }
#if FRIEND
        if (args.Length > 0 && args[0] == "--check") { Environment.ExitCode = CheckHotkeySetup(); return; }
#endif
        if (args.Length > 0 && args[0] == "--install")
        {
#if FRIEND
            try { SetAutostart(true); }
            catch { Environment.ExitCode = 25; }
#else
            SetAutostart(true);
#endif
            return;
        }
        if (args.Length > 0 && args[0] == "--uninstall") { SetAutostart(false); return; }
        if (args.Length > 0 && args[0] == "--makekey") { MakeKey(); return; }

        bool created;
#if FRIEND
        using (Mutex m = new Mutex(true, @"Local\Edge_single_instance", out created))
#else
        using (Mutex m = new Mutex(true, @"Local\Jarvis_single_instance", out created))
#endif
        {
            if (!created) { Environment.ExitCode = 29; return; }
            InitProcess();
#if FRIEND
            try
            {
                if (InitTrial()) { SelfDestruct(); return; }
                StartTrialTimer();
            }
            catch { Environment.ExitCode = 26; return; }
#endif
            App app = null;
            try
            {
                app = new App();
#if FRIEND
                if (args.Length == 2 && args[0] == "--ready-event")
                {
                    using (EventWaitHandle ready = EventWaitHandle.OpenExisting(args[1])) ready.Set();
                }
#endif
                Application.Run(app);
            }
            catch (Exception error) { Log("Startup failed: " + error.Message); Environment.ExitCode = 28; }
            finally { if (app != null) app.Dispose(); }
        }
    }
}

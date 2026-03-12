// ClaudeCodeClicker - System Tray Auto-Clicker for Claude Code
// Uses native COM IUIAutomation API with explicit Marshal.ReleaseComObject.
// No System.Windows.Automation — that wrapper leaks native memory on Win11.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// =========================================================================
// COM Interop — IUIAutomation interfaces
//
// vtable slots are counted from IUnknown (slots 0-2: QI, AddRef, Release).
// Each method declaration below occupies one vtable slot starting at slot 3.
// Stubs are never called — they exist only to hold vtable position.
// =========================================================================

[ComImport, Guid("352ffba8-0973-437c-a61f-f64cafd81df9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IUIAutomationCondition { }

[ComImport, Guid("fb377fbe-8ea6-46d5-9c73-6499642d3059")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IUIAutomationInvokePattern
{
    void Invoke(); // slot 3
}

[ComImport, Guid("14314595-b4bc-4055-95f2-58f2e42c9855")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IUIAutomationElementArray
{
    int Length { get; }                          // slot 3 (propget)
    IUIAutomationElement GetElement(int index);  // slot 4
}

// IUIAutomationElement — slots 3..16
// We need: FindAll(6), GetCurrentPropertyValue(10), GetCurrentPattern(16)
[ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IUIAutomationElement
{
    // --- slot 3 ---
    void SetFocus();
    // --- slot 4 ---
    [PreserveSig] int Stub_GetRuntimeId(IntPtr retVal);
    // --- slot 5 ---
    IUIAutomationElement FindFirst(
        int scope, IUIAutomationCondition condition);
    // --- slot 6 ---
    IUIAutomationElementArray FindAll(
        int scope, IUIAutomationCondition condition);
    // --- slot 7: FindFirstBuildCache ---
    [PreserveSig] int Stub7(int a, IntPtr b, IntPtr c, IntPtr d);
    // --- slot 8: FindAllBuildCache ---
    [PreserveSig] int Stub8(int a, IntPtr b, IntPtr c, IntPtr d);
    // --- slot 9: BuildUpdatedCache ---
    [PreserveSig] int Stub9(IntPtr a, IntPtr b);
    // --- slot 10 ---
    void GetCurrentPropertyValue(
        int propertyId,
        [Out, MarshalAs(UnmanagedType.Struct)] out object retVal);
    // --- slot 11: GetCurrentPropertyValueEx ---
    [PreserveSig] int Stub11(int a, int b, IntPtr c);
    // --- slot 12: GetCachedPropertyValue ---
    [PreserveSig] int Stub12(int a, IntPtr b);
    // --- slot 13: GetCachedPropertyValueEx ---
    [PreserveSig] int Stub13(int a, int b, IntPtr c);
    // --- slot 14: GetCurrentPatternAs ---
    [PreserveSig] int Stub14(int a, IntPtr b, IntPtr c);
    // --- slot 15: GetCachedPatternAs ---
    [PreserveSig] int Stub15(int a, IntPtr b, IntPtr c);
    // --- slot 16 ---
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object GetCurrentPattern(int patternId);
}

// IUIAutomation — slots 3..23
// We need: GetRootElement(5), CreatePropertyCondition(23)
[ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IUIAutomation
{
    // --- slot 3: CompareElements ---
    [PreserveSig] int Stub3(IntPtr a, IntPtr b, IntPtr c);
    // --- slot 4: CompareRuntimeIds ---
    [PreserveSig] int Stub4(IntPtr a, IntPtr b, IntPtr c);
    // --- slot 5 ---
    IUIAutomationElement GetRootElement();
    // --- slot 6: ElementFromHandle ---
    [PreserveSig] int Stub6(IntPtr a, IntPtr b);
    // --- slot 7: ElementFromPoint ---
    [PreserveSig] int Stub7(long a, IntPtr b);
    // --- slot 8: GetFocusedElement ---
    [PreserveSig] int Stub8(IntPtr a);
    // --- slot 9: GetRootElementBuildCache ---
    [PreserveSig] int Stub9(IntPtr a, IntPtr b);
    // --- slot 10: ElementFromHandleBuildCache ---
    [PreserveSig] int Stub10(IntPtr a, IntPtr b, IntPtr c);
    // --- slot 11: ElementFromPointBuildCache ---
    [PreserveSig] int Stub11(long a, IntPtr b, IntPtr c);
    // --- slot 12: GetFocusedElementBuildCache ---
    [PreserveSig] int Stub12(IntPtr a, IntPtr b);
    // --- slot 13: CreateTreeWalker ---
    [PreserveSig] int Stub13(IntPtr a, IntPtr b);
    // --- slot 14: get_ControlViewWalker ---
    [PreserveSig] int Stub14(IntPtr a);
    // --- slot 15: get_ContentViewWalker ---
    [PreserveSig] int Stub15(IntPtr a);
    // --- slot 16: get_RawViewWalker ---
    [PreserveSig] int Stub16(IntPtr a);
    // --- slot 17: get_RawViewCondition ---
    [PreserveSig] int Stub17(IntPtr a);
    // --- slot 18: get_ControlViewCondition ---
    [PreserveSig] int Stub18(IntPtr a);
    // --- slot 19: get_ContentViewCondition ---
    [PreserveSig] int Stub19(IntPtr a);
    // --- slot 20: CreateCacheRequest ---
    [PreserveSig] int Stub20(IntPtr a);
    // --- slot 21: CreateTrueCondition ---
    [PreserveSig] int Stub21(IntPtr a);
    // --- slot 22: CreateFalseCondition ---
    [PreserveSig] int Stub22(IntPtr a);
    // --- slot 23 ---
    IUIAutomationCondition CreatePropertyCondition(
        int propertyId,
        [In, MarshalAs(UnmanagedType.Struct)] object value);
}

// =========================================================================
// UIA Constants
// =========================================================================

static class UIA
{
    public const int ControlType_Button = 50000;
    public const int ControlType_Window = 50032;
    public const int Prop_ControlType = 30003;
    public const int Prop_Name = 30005;
    public const int Prop_ClassName = 30012;
    public const int Pattern_Invoke = 10000;
    public const int Scope_Children = 2;
    public const int Scope_Descendants = 4;

    private static readonly Guid CLSID = new Guid("ff48dba4-60ef-4201-aa87-54103eef594e");

    public static IUIAutomation Create()
    {
        return (IUIAutomation)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID));
    }
}

// =========================================================================
// Program
// =========================================================================

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApp());
    }
}

// =========================================================================
// TrayApp
// =========================================================================

class TrayApp : ApplicationContext
{
    private NotifyIcon trayIcon;
    private ContextMenuStrip menu;
    private ToolStripMenuItem pauseItem;
    private Timer pollTimer;
    private ButtonWatcher watcher;
    private bool isPaused;

    public TrayApp()
    {
        watcher = new ButtonWatcher();
        isPaused = false;

        menu = new ContextMenuStrip();
        pauseItem = new ToolStripMenuItem("Pause", null, OnPauseClick);
        menu.Items.Add(pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, OnExitClick);

        trayIcon = new NotifyIcon();
        trayIcon.Icon = SystemIcons.Shield;
        trayIcon.Text = "ClaudeCodeClicker";
        trayIcon.ContextMenuStrip = menu;
        trayIcon.DoubleClick += OnTrayDoubleClick;
        trayIcon.Visible = true;

        pollTimer = new Timer();
        pollTimer.Interval = 1000;
        pollTimer.Tick += OnPollTick;
        pollTimer.Start();
    }

    private void OnPollTick(object sender, EventArgs e)
    {
        watcher.Poll();
        string status = isPaused ? "Paused" : watcher.LastStatus;
        if (status != null && status.Length > 63)
            status = status.Substring(0, 60) + "...";
        trayIcon.Text = status ?? "ClaudeCodeClicker";
    }

    private void OnPauseClick(object sender, EventArgs e)
    {
        isPaused = !isPaused;
        if (isPaused)
        {
            pollTimer.Stop();
            pauseItem.Text = "Resume";
            trayIcon.Icon = SystemIcons.Warning;
        }
        else
        {
            pollTimer.Start();
            pauseItem.Text = "Pause";
            trayIcon.Icon = SystemIcons.Shield;
        }
    }

    private void OnTrayDoubleClick(object sender, EventArgs e)
    {
        string msg = watcher.LastStatus ?? "No status";
        if (watcher.LastClickInfo != null)
            msg += "\nLast: " + watcher.LastClickInfo;
        trayIcon.BalloonTipTitle = "ClaudeCodeClicker";
        trayIcon.BalloonTipText = msg;
        trayIcon.ShowBalloonTip(3000);
    }

    private void OnExitClick(object sender, EventArgs e)
    {
        pollTimer.Stop();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            pollTimer.Stop();
            pollTimer.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            menu.Dispose();
        }
        base.Dispose(disposing);
    }
}

// =========================================================================
// ButtonWatcher — direct COM, explicit ReleaseComObject on every object
// =========================================================================

class ButtonWatcher
{
    private const string TitlePattern = "Claude";
    private const string ClassNameFilter = "Chrome_WidgetWin_1";
    private const string ButtonPattern = "Ctrl Enter";

    private IUIAutomation uia;
    private IUIAutomationCondition buttonCond;
    private IUIAutomationCondition windowCond;
    private IUIAutomationElement cachedWindow;
    private int totalClicks;
    private string lastStatus;
    private string lastClickInfo;

    public string LastStatus { get { return lastStatus; } }
    public string LastClickInfo { get { return lastClickInfo; } }

    public ButtonWatcher()
    {
        uia = UIA.Create();
        buttonCond = uia.CreatePropertyCondition(UIA.Prop_ControlType, UIA.ControlType_Button);
        windowCond = uia.CreatePropertyCondition(UIA.Prop_ControlType, UIA.ControlType_Window);
    }

    public void Poll()
    {
        try
        {
            if (cachedWindow == null || !IsAlive(cachedWindow))
            {
                ReleaseRef(ref cachedWindow);
                cachedWindow = FindWindow();
                if (cachedWindow == null)
                {
                    lastStatus = "Searching for Claude...";
                    return;
                }
            }

            lastStatus = "Watching Claude (clicks: " + totalClicks + ")";
            ScanAndClick();
        }
        catch (Exception)
        {
            ReleaseRef(ref cachedWindow);
            lastStatus = "Error, retrying...";
        }
    }

    private void ScanAndClick()
    {
        IUIAutomationElementArray arr = null;
        try
        {
            arr = cachedWindow.FindAll(UIA.Scope_Descendants, buttonCond);
            if (arr == null) return;

            int count = arr.Length;
            for (int i = 0; i < count; i++)
            {
                IUIAutomationElement btn = null;
                try
                {
                    btn = arr.GetElement(i);
                    string name = GetProp(btn, UIA.Prop_Name);
                    if (name != null
                        && name.IndexOf(ButtonPattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (TryInvoke(btn))
                        {
                            totalClicks++;
                            lastClickInfo = DateTime.Now.ToString("HH:mm:ss") + " \"" + name + "\"";
                            lastStatus = "Clicked! (total: " + totalClicks + ")";
                        }
                        return;
                    }
                }
                finally
                {
                    ReleaseRef(ref btn);
                }
            }
        }
        catch (COMException)
        {
            ReleaseRef(ref cachedWindow);
        }
        finally
        {
            ReleaseRef(ref arr);
        }
    }

    private IUIAutomationElement FindWindow()
    {
        IUIAutomationElement root = null;
        IUIAutomationElementArray children = null;
        try
        {
            root = uia.GetRootElement();
            children = root.FindAll(UIA.Scope_Children, windowCond);
            if (children == null) return null;

            int count = children.Length;
            for (int i = 0; i < count; i++)
            {
                IUIAutomationElement child = null;
                try
                {
                    child = children.GetElement(i);
                    string name = GetProp(child, UIA.Prop_Name);
                    string cls = GetProp(child, UIA.Prop_ClassName);

                    if (name != null && cls != null
                        && name.IndexOf(TitlePattern, StringComparison.OrdinalIgnoreCase) >= 0
                        && cls.Equals(ClassNameFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        IUIAutomationElement result = child;
                        child = null; // don't release — returning it
                        return result;
                    }
                }
                finally
                {
                    ReleaseRef(ref child);
                }
            }
            return null;
        }
        finally
        {
            ReleaseRef(ref children);
            ReleaseRef(ref root);
        }
    }

    private static string GetProp(IUIAutomationElement el, int propId)
    {
        try
        {
            object val;
            el.GetCurrentPropertyValue(propId, out val);
            return val as string;
        }
        catch { return null; }
    }

    private static bool TryInvoke(IUIAutomationElement el)
    {
        object patObj = null;
        try
        {
            patObj = el.GetCurrentPattern(UIA.Pattern_Invoke);
            if (patObj == null) return false;
            IUIAutomationInvokePattern inv = patObj as IUIAutomationInvokePattern;
            if (inv == null) return false;
            inv.Invoke();
            return true;
        }
        catch { return false; }
        finally
        {
            if (patObj != null)
            {
                try { Marshal.ReleaseComObject(patObj); } catch { }
            }
        }
    }

    private static bool IsAlive(IUIAutomationElement el)
    {
        return GetProp(el, UIA.Prop_Name) != null;
    }

    private static void ReleaseRef<T>(ref T obj) where T : class
    {
        if (obj != null)
        {
            try { Marshal.ReleaseComObject(obj); } catch { }
            obj = null;
        }
    }
}

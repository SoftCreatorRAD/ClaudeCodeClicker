// ClaudeCodeClicker - System Tray Auto-Clicker for Claude Code
// Automatically clicks the "Ctrl Enter" permission button in Claude Code.
// Compiled with: csc.exe /target:winexe (see build.bat)

using System;
using System.Drawing;
using System.Windows.Automation;
using System.Windows.Forms;

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
        pollTimer.Interval = 2000;
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
// ButtonWatcher
// =========================================================================

class ButtonWatcher
{
    private const string TitlePattern = "Claude";
    private const string ClassNameFilter = "Chrome_WidgetWin_1";
    private const string ButtonPattern = "Ctrl Enter";

    private static readonly Condition ButtonCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);

    private AutomationElement cachedWindow;
    private int totalClicks;
    private string lastStatus;
    private string lastClickInfo;

    public string LastStatus { get { return lastStatus; } }
    public string LastClickInfo { get { return lastClickInfo; } }

    public void Poll()
    {
        try
        {
            if (cachedWindow == null || !IsAlive(cachedWindow))
            {
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
        catch
        {
            cachedWindow = null;
            lastStatus = "Error, retrying...";
        }
    }

    private void ScanAndClick()
    {
        // Tell GC about the ~1MB of native COM memory FindAll will allocate.
        // This makes GC collect COM wrappers proactively instead of letting them pile up.
        GC.AddMemoryPressure(1024 * 1024);
        try
        {
            AutomationElementCollection buttons = cachedWindow.FindAll(
                TreeScope.Descendants, ButtonCondition);

            foreach (AutomationElement btn in buttons)
            {
                try
                {
                    string name = btn.Current.Name;
                    if (name != null
                        && name.IndexOf(ButtonPattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        object pat;
                        if (btn.TryGetCurrentPattern(InvokePattern.Pattern, out pat))
                        {
                            ((InvokePattern)pat).Invoke();
                            totalClicks++;
                            lastClickInfo = DateTime.Now.ToString("HH:mm:ss") + " \"" + name + "\"";
                            lastStatus = "Clicked! (total: " + totalClicks + ")";
                        }
                        return;
                    }
                }
                catch { }
            }
        }
        finally
        {
            GC.RemoveMemoryPressure(1024 * 1024);
        }
    }

    private static AutomationElement FindWindow()
    {
        try
        {
            AutomationElement root = AutomationElement.RootElement;
            // Walk direct children only — cheap operation
            TreeWalker walker = TreeWalker.ControlViewWalker;
            AutomationElement child = walker.GetFirstChild(root);

            while (child != null)
            {
                try
                {
                    string name = child.Current.Name;
                    string cls = child.Current.ClassName;
                    if (name != null && cls != null
                        && name.IndexOf(TitlePattern, StringComparison.OrdinalIgnoreCase) >= 0
                        && cls.Equals(ClassNameFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        return child;
                    }
                }
                catch { }
                child = walker.GetNextSibling(child);
            }
        }
        catch { }
        return null;
    }

    private static bool IsAlive(AutomationElement el)
    {
        try { return el.Current.Name != null; }
        catch { return false; }
    }
}

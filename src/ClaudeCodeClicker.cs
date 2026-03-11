// ClaudeCodeClicker - System Tray Auto-Clicker for Claude Code
// Automatically clicks the "Ctrl Enter" permission button in Claude Code.
// Compiled with: csc.exe /target:winexe (see build.bat)

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Forms;

// =========================================================================
// Program Entry Point
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
// TrayApp - System Tray Application
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

        // Context menu
        menu = new ContextMenuStrip();
        pauseItem = new ToolStripMenuItem("Pause", null, OnPauseClick);
        menu.Items.Add(pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, OnExitClick);

        // Tray icon
        trayIcon = new NotifyIcon();
        trayIcon.Icon = SystemIcons.Shield;
        trayIcon.Text = "ClaudeCodeClicker - Starting...";
        trayIcon.ContextMenuStrip = menu;
        trayIcon.DoubleClick += OnTrayDoubleClick;
        trayIcon.Visible = true;

        // Poll timer (1 second)
        pollTimer = new Timer();
        pollTimer.Interval = 1000;
        pollTimer.Tick += OnPollTick;
        pollTimer.Start();
    }

    private void OnPollTick(object sender, EventArgs e)
    {
        watcher.Poll();
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        string status = watcher.LastStatus;
        if (isPaused)
        {
            status = "Paused";
        }

        // NotifyIcon.Text has 63 char limit
        if (status != null && status.Length > 63)
        {
            status = status.Substring(0, 60) + "...";
        }

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

        UpdateTooltip();
    }

    private void OnTrayDoubleClick(object sender, EventArgs e)
    {
        string msg = watcher.LastStatus ?? "No status";
        string clicked = watcher.LastClickInfo;
        if (clicked != null)
        {
            msg += "\n\nLast click: " + clicked;
        }

        trayIcon.BalloonTipTitle = "ClaudeCodeClicker";
        trayIcon.BalloonTipText = msg;
        trayIcon.BalloonTipIcon = ToolTipIcon.Info;
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
// ButtonWatcher - UI Automation Monitor & Auto-Clicker
// =========================================================================

class ButtonWatcher
{
    private const string TitlePattern = "Claude";
    private const string ClassNameFilter = "Chrome_WidgetWin_1";
    private const string ButtonPattern = "Ctrl Enter";
    private const int GC_INTERVAL = 10; // Force GC every N polls

    // Cached conditions (COM objects — create once, reuse forever)
    private static readonly PropertyCondition WindowTypeCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
    private static readonly PropertyCondition ButtonTypeCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);

    private AutomationElement cachedWindow;
    private string lastStatus;
    private string lastClickInfo;
    private int totalClicks;
    private int pollCount;

    public string LastStatus { get { return lastStatus; } }
    public string LastClickInfo { get { return lastClickInfo; } }

    public ButtonWatcher()
    {
        cachedWindow = null;
        lastStatus = "Starting...";
        lastClickInfo = null;
        totalClicks = 0;
        pollCount = 0;
    }

    public void Poll()
    {
        pollCount++;

        try
        {
            // Ensure we have a valid window
            if (cachedWindow == null)
            {
                cachedWindow = FindWindow();
            }

            if (cachedWindow == null)
            {
                lastStatus = "Searching for Claude window...";
                return;
            }

            // Verify window is still alive
            if (!IsWindowAlive(cachedWindow))
            {
                cachedWindow = null;
                lastStatus = "Window lost, re-searching...";
                return;
            }

            // Scan for buttons and click
            long memMB = GC.GetTotalMemory(false) / (1024 * 1024);
            lastStatus = "Watching: Claude (clicks: " + totalClicks + ", mem: " + memMB + "MB)";
            FindAndClickButton();
        }
        catch (Exception)
        {
            cachedWindow = null;
            lastStatus = "Error, re-searching...";
        }
        finally
        {
            // Periodic GC to release COM RCWs that hold native memory
            if (pollCount % GC_INTERVAL == 0)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
                GC.WaitForPendingFinalizers();
            }
        }
    }

    private AutomationElement FindWindow()
    {
        try
        {
            AutomationElement root = AutomationElement.RootElement;
            AutomationElementCollection children = root.FindAll(
                TreeScope.Children, WindowTypeCondition);

            foreach (AutomationElement child in children)
            {
                try
                {
                    string name = child.Current.Name;
                    string className = child.Current.ClassName;

                    if (name != null
                        && className != null
                        && name.IndexOf(TitlePattern, StringComparison.OrdinalIgnoreCase) >= 0
                        && className.Equals(ClassNameFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        return child;
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    private void FindAndClickButton()
    {
        try
        {
            // Use TreeWalker to avoid building full collection in memory.
            // Walk descendants one-by-one, stop as soon as target is found.
            TreeWalker walker = new TreeWalker(ButtonTypeCondition);
            AutomationElement btn = walker.GetFirstChild(cachedWindow);

            while (btn != null)
            {
                try
                {
                    string name = btn.Current.Name;
                    if (name != null
                        && name.IndexOf(ButtonPattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (TryInvoke(btn))
                        {
                            totalClicks++;
                            lastClickInfo = DateTime.Now.ToString("HH:mm:ss")
                                + " - \"" + name + "\"";
                            lastStatus = "Clicked: \"" + name + "\" (total: " + totalClicks + ")";
                        }
                        return;
                    }
                }
                catch (Exception)
                {
                    // Skip this element
                }

                btn = walker.GetNextSibling(btn);
            }

            // TreeWalker only walks direct children with the condition.
            // Buttons in Electron are deeply nested — fall back to FindAll
            // but only if TreeWalker found nothing.
            AutomationElementCollection buttons = cachedWindow.FindAll(
                TreeScope.Descendants, ButtonTypeCondition);

            foreach (AutomationElement deepBtn in buttons)
            {
                try
                {
                    string name = deepBtn.Current.Name;
                    if (name != null
                        && name.IndexOf(ButtonPattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (TryInvoke(deepBtn))
                        {
                            totalClicks++;
                            lastClickInfo = DateTime.Now.ToString("HH:mm:ss")
                                + " - \"" + name + "\"";
                            lastStatus = "Clicked: \"" + name + "\" (total: " + totalClicks + ")";
                        }
                        return;
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }
        catch (Exception)
        {
            cachedWindow = null;
        }
    }

    private static bool TryInvoke(AutomationElement element)
    {
        try
        {
            object pattern;
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
            {
                ((InvokePattern)pattern).Invoke();
                return true;
            }
        }
        catch (Exception)
        {
        }
        return false;
    }

    private static bool IsWindowAlive(AutomationElement window)
    {
        try
        {
            string name = window.Current.Name;
            return name != null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

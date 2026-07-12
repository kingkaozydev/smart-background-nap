using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class SmartBackgroundNap
{
    private const string AppName = "Smart Background Nap";
    private const string CreatorLine = "Criado por KaozyKing | Instagram: @oeduardomacedo | GitHub: kingkaozydev";
    private const string AutoTaskName = "SmartBackgroundNap";
    private const string TrayTaskName = "SmartBackgroundNapTray";
    private const string GitHubUrl = "https://github.com/kingkaozydev/smart-background-nap";
    private const string MutexName = "Local\\SmartBackgroundNap.SingleInstance";
    private const string ShowDashboardEventName = "Local\\SmartBackgroundNap.ShowDashboard";
    private const string ResourcePrefix = "SmartBackgroundNap.Resources.";

    private static string appRoot;
    private static string backgroundScriptPath;
    private static string autoManagerPath;
    private static string trayManagerPath;
    private static string configPath;
    private static string readmePath;
    private static string iconPath;
    private static string outputsPath;
    private static string logPath;
    private static Mutex singleInstanceMutex;
    private static EventWaitHandle showDashboardEvent;

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            MainCore(args);
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
            try { Console.Error.WriteLine(ex.ToString()); } catch { }
            Environment.ExitCode = unchecked((int)0xE0434352);
        }
    }

    private static void MainCore(string[] args)
    {
        InitializePaths();

        if (HasArg(args, "--apply"))
        {
            Environment.ExitCode = RunApplyNow().ExitCode;
            return;
        }
        if (HasArg(args, "--restore"))
        {
            Environment.ExitCode = RunRestore().ExitCode;
            return;
        }
        if (HasArg(args, "--install"))
        {
            Environment.ExitCode = InstallComplete().ExitCode;
            return;
        }
        if (HasArg(args, "--uninstall"))
        {
            Environment.ExitCode = UninstallComplete().ExitCode;
            return;
        }
        if (HasArg(args, "--install-auto"))
        {
            Environment.ExitCode = InstallAutomatic().ExitCode;
            return;
        }
        if (HasArg(args, "--uninstall-auto"))
        {
            Environment.ExitCode = UninstallAutomatic().ExitCode;
            return;
        }
        if (HasArg(args, "--install-startup"))
        {
            Environment.ExitCode = InstallStartup().ExitCode;
            return;
        }
        if (HasArg(args, "--uninstall-startup"))
        {
            Environment.ExitCode = UninstallStartup().ExitCode;
            return;
        }

        bool trayOnly = HasArg(args, "--tray");
        bool ownsMutex;
        singleInstanceMutex = new Mutex(true, MutexName, out ownsMutex);
        showDashboardEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowDashboardEventName);
        if (!ownsMutex)
        {
            if (!trayOnly)
            {
                try { showDashboardEvent.Set(); } catch { }
            }
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        SmartNapContext context = new SmartNapContext(trayOnly);
        Application.Run(context);

        try { singleInstanceMutex.ReleaseMutex(); } catch { }
        try { singleInstanceMutex.Dispose(); } catch { }
        try { showDashboardEvent.Dispose(); } catch { }
    }

    private static void WriteCrash(Exception ex)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmartBackgroundNap");
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "crash.log"),
                DateTime.Now.ToString("s") + Environment.NewLine + ex.ToString(),
                Encoding.UTF8);
        }
        catch
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(Path.GetTempPath(), "SmartBackgroundNap-crash.log"),
                    DateTime.Now.ToString("s") + Environment.NewLine + ex.ToString(),
                    Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    private static void InitializePaths()
    {
        string exePath = Application.ExecutablePath;
        string exeDir = Path.GetDirectoryName(exePath);
        string looseRoot;
        if (String.Equals(Path.GetFileName(exeDir), "bin", StringComparison.OrdinalIgnoreCase))
        {
            looseRoot = Path.GetFullPath(Path.Combine(exeDir, ".."));
        }
        else
        {
            looseRoot = exeDir;
        }

        if (File.Exists(Path.Combine(looseRoot, "background-nap.ps1")))
        {
            appRoot = looseRoot;
        }
        else
        {
            appRoot = GetWritableAppRoot();
            string runtimeRoot = Path.Combine(appRoot, "runtime");
            EnsureRuntimeFiles(runtimeRoot);
            looseRoot = runtimeRoot;
        }

        backgroundScriptPath = Path.Combine(looseRoot, "background-nap.ps1");
        autoManagerPath = Path.Combine(looseRoot, "manage-background-nap.ps1");
        trayManagerPath = Path.Combine(looseRoot, "manage-background-nap-tray.ps1");
        configPath = Path.Combine(looseRoot, "game-session.config.json");
        readmePath = Path.Combine(looseRoot, "README.md");
        iconPath = Path.Combine(looseRoot, "assets\\smart-background-nap.ico");
        outputsPath = Path.Combine(appRoot, "outputs");
        logPath = Path.Combine(outputsPath, "background-nap-auto.log");
    }

    private static string GetWritableAppRoot()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] candidates = new string[]
        {
            Path.Combine(local, "SmartBackgroundNap"),
            Path.Combine(local, "Programs", "SmartBackgroundNap"),
            Path.Combine(Path.GetTempPath(), "SmartBackgroundNap")
        };

        Exception last = null;
        foreach (string candidate in candidates)
        {
            try
            {
                Directory.CreateDirectory(candidate);
                string probe = Path.Combine(candidate, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return candidate;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new UnauthorizedAccessException("Could not create a writable Smart Background Nap runtime folder.", last);
    }

    private static void EnsureRuntimeFiles(string runtimeRoot)
    {
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(Path.Combine(runtimeRoot, "assets"));

        ExtractResource("background_nap_ps1", Path.Combine(runtimeRoot, "background-nap.ps1"));
        ExtractResource("browser_nap_ps1", Path.Combine(runtimeRoot, "browser-nap.ps1"));
        ExtractResource("manage_background_nap_ps1", Path.Combine(runtimeRoot, "manage-background-nap.ps1"));
        ExtractResource("manage_background_nap_tray_ps1", Path.Combine(runtimeRoot, "manage-background-nap-tray.ps1"));
        ExtractResource("smart_background_nap_tray_ps1", Path.Combine(runtimeRoot, "smart-background-nap-tray.ps1"));
        ExtractResource("game_session_config_json", Path.Combine(runtimeRoot, "game-session.config.json"));
        ExtractResource("readme_md", Path.Combine(runtimeRoot, "README.md"));
        ExtractResource("icon_ico", Path.Combine(runtimeRoot, "assets\\smart-background-nap.ico"));
    }

    private static void ExtractResource(string resourceName, string targetPath)
    {
        string fullName = ResourcePrefix + resourceName;
        Assembly assembly = Assembly.GetExecutingAssembly();
        using (Stream stream = assembly.GetManifestResourceStream(fullName))
        {
            if (stream == null)
            {
                throw new InvalidOperationException("Missing embedded resource: " + fullName);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            using (MemoryStream memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                byte[] bytes = memory.ToArray();
                if (File.Exists(targetPath))
                {
                    byte[] existing = File.ReadAllBytes(targetPath);
                    if (existing.Length == bytes.Length)
                    {
                        bool same = true;
                        for (int i = 0; i < bytes.Length; i++)
                        {
                            if (existing[i] != bytes[i])
                            {
                                same = false;
                                break;
                            }
                        }
                        if (same) { return; }
                    }
                }
                File.WriteAllBytes(targetPath, bytes);
            }
        }
    }

    private static bool HasArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (String.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static Icon LoadIcon()
    {
        try
        {
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }
        }
        catch
        {
        }

        try
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + "icon_ico"))
            {
                if (stream != null)
                {
                    return new Icon(stream);
                }
            }
        }
        catch
        {
        }

        return SystemIcons.Application;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static RunResult RunHidden(string fileName, string arguments, int timeoutMs)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = fileName;
        psi.Arguments = arguments;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        StringBuilder output = new StringBuilder();
        try
        {
            using (Process process = Process.Start(psi))
            {
                if (process == null)
                {
                    return new RunResult(1, "Could not start " + fileName + ".");
                }

                output.Append(process.StandardOutput.ReadToEnd());
                output.Append(process.StandardError.ReadToEnd());

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { }
                    output.AppendLine("Timed out.");
                    return new RunResult(124, output.ToString().Trim());
                }

                return new RunResult(process.ExitCode, output.ToString().Trim());
            }
        }
        catch (Exception ex)
        {
            return new RunResult(1, ex.Message);
        }
    }

    private static RunResult RunPowerShellScript(string scriptPath, string arguments, int timeoutMs)
    {
        if (!File.Exists(scriptPath))
        {
            return new RunResult(1, "Missing script: " + scriptPath);
        }

        string psArgs = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " + Quote(scriptPath) + " " + arguments;
        return RunHidden("powershell.exe", psArgs, timeoutMs);
    }

    private static RunResult RunApplyNow()
    {
        Directory.CreateDirectory(outputsPath);
        return RunPowerShellScript(backgroundScriptPath, "-Action Apply -StateMode Latest -Quiet -LogPath " + Quote(logPath), 120000);
    }

    private static RunResult RunRestore()
    {
        return RunPowerShellScript(backgroundScriptPath, "-Action Restore -LogPath " + Quote(logPath), 120000);
    }

    private static RunResult InstallAutomatic()
    {
        return RunPowerShellScript(autoManagerPath, "-Action Install -AppExePath " + Quote(Application.ExecutablePath), 60000);
    }

    private static RunResult UninstallAutomatic()
    {
        return RunPowerShellScript(autoManagerPath, "-Action Uninstall", 60000);
    }

    private static RunResult InstallStartup()
    {
        return RunPowerShellScript(trayManagerPath, "-Action Install -AppExePath " + Quote(Application.ExecutablePath), 60000);
    }

    private static RunResult UninstallStartup()
    {
        return RunPowerShellScript(trayManagerPath, "-Action Uninstall", 60000);
    }

    private static RunResult InstallComplete()
    {
        RunResult auto = InstallAutomatic();
        RunResult startup = InstallStartup();
        return RunResult.Combine(auto, startup);
    }

    private static RunResult UninstallComplete()
    {
        RunResult startup = UninstallStartup();
        RunResult auto = UninstallAutomatic();
        return RunResult.Combine(startup, auto);
    }

    private static bool IsTaskInstalled(string taskName)
    {
        RunResult result = RunHidden("schtasks.exe", "/Query /TN " + Quote(taskName), 8000);
        return result.ExitCode == 0;
    }

    private static string ReadLastLogLine()
    {
        try
        {
            if (!File.Exists(logPath))
            {
                return "No log yet.";
            }

            string last = "";
            using (FileStream stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!String.IsNullOrWhiteSpace(line))
                    {
                        last = line;
                    }
                }
            }

            return String.IsNullOrWhiteSpace(last) ? "No log yet." : last;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static string GetLastRunText()
    {
        try
        {
            if (File.Exists(logPath))
            {
                return File.GetLastWriteTime(logPath).ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
        catch
        {
        }

        return "No run yet.";
    }

    private static void OpenFolder()
    {
        OpenExternal(appRoot);
    }

    private static void OpenLog()
    {
        try
        {
            Directory.CreateDirectory(outputsPath);
            if (!File.Exists(logPath))
            {
                using (File.Create(logPath)) { }
            }
            OpenExternal(logPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void OpenConfig()
    {
        OpenExternal(configPath);
    }

    private static void OpenReadme()
    {
        OpenExternal(readmePath);
    }

    private static void OpenGitHub()
    {
        OpenExternal(GitHubUrl);
    }

    private static void OpenExternal(string target)
    {
        try
        {
            if (String.IsNullOrWhiteSpace(target))
            {
                return;
            }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = target;
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private sealed class SmartNapContext : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon;
        private readonly MainWindow mainWindow;
        private bool allowExit;
        private bool listenerStopping;
        private Thread showThread;

        public SmartNapContext(bool trayOnly)
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = LoadIcon();
            notifyIcon.Text = AppName + ": active";
            notifyIcon.Visible = true;
            notifyIcon.ContextMenuStrip = BuildMenu();
            notifyIcon.DoubleClick += delegate { ShowMainWindow(); };

            mainWindow = new MainWindow();
            mainWindow.FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!allowExit && e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    mainWindow.Hide();
                    ShowTrayMessage("Still running in the tray.");
                }
            };

            if (!trayOnly)
            {
                ShowMainWindow();
            }
            else
            {
                ShowTrayMessage("Ready. Automatic mode can be controlled from the tray.");
            }

            StartShowListener();
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem title = new ToolStripMenuItem(AppName);
            title.Enabled = false;
            menu.Items.Add(title);

            ToolStripMenuItem creator = new ToolStripMenuItem(CreatorLine);
            creator.Enabled = false;
            menu.Items.Add(creator);
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem open = new ToolStripMenuItem("Open dashboard");
            open.Click += delegate { ShowMainWindow(); };
            menu.Items.Add(open);

            ToolStripMenuItem apply = new ToolStripMenuItem("Optimize now");
            apply.Click += delegate { RunFromTray("Optimize now", RunApplyNow); };
            menu.Items.Add(apply);

            ToolStripMenuItem restore = new ToolStripMenuItem("Restore last snapshot");
            restore.Click += delegate { RunFromTray("Restore", RunRestore); };
            menu.Items.Add(restore);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem log = new ToolStripMenuItem("Open log");
            log.Click += delegate { OpenLog(); };
            menu.Items.Add(log);

            ToolStripMenuItem folder = new ToolStripMenuItem("Open folder");
            folder.Click += delegate { OpenFolder(); };
            menu.Items.Add(folder);

            ToolStripMenuItem readme = new ToolStripMenuItem("Open README");
            readme.Click += delegate { OpenReadme(); };
            menu.Items.Add(readme);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate
            {
                allowExit = true;
                listenerStopping = true;
                try { showDashboardEvent.Set(); } catch { }
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                mainWindow.Close();
                Application.Exit();
            };
            menu.Items.Add(exit);

            return menu;
        }

        private void StartShowListener()
        {
            showThread = new Thread(new ThreadStart(delegate
            {
                while (!listenerStopping)
                {
                    try
                    {
                        showDashboardEvent.WaitOne();
                        if (listenerStopping) { break; }
                        mainWindow.BeginInvoke(new MethodInvoker(delegate { ShowMainWindow(); }));
                    }
                    catch
                    {
                        break;
                    }
                }
            }));
            showThread.IsBackground = true;
            showThread.Start();
        }

        private void ShowMainWindow()
        {
            mainWindow.RefreshStatus();
            if (!mainWindow.Visible)
            {
                mainWindow.Show();
            }
            if (mainWindow.WindowState == FormWindowState.Minimized)
            {
                mainWindow.WindowState = FormWindowState.Normal;
            }
            mainWindow.Activate();
        }

        private void ShowTrayMessage(string text)
        {
            notifyIcon.BalloonTipTitle = AppName;
            notifyIcon.BalloonTipText = text;
            notifyIcon.ShowBalloonTip(2500);
        }

        private void RunFromTray(string actionName, Func<RunResult> action)
        {
            RunResult result = action();
            mainWindow.RefreshStatus();
            ShowTrayMessage(result.ExitCode == 0 ? actionName + " finished." : actionName + " failed.");
            if (result.ExitCode != 0)
            {
                MessageBox.Show(result.Output, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private sealed class MainWindow : Form
    {
        private Label autoValue;
        private Label startupValue;
        private Label lastRunValue;
        private Label resultValue;
        private Label statusPill;
        private Label actionTitle;
        private Label actionDetail;
        private CheckBox autoCheck;
        private CheckBox startupCheck;
        private Button optimizeButton;
        private Button restoreButton;
        private Button moreButton;
        private ProgressBar actionProgress;
        private bool loading;
        private bool busy;
        private System.Windows.Forms.Timer refreshTimer;

        public MainWindow()
        {
            Text = AppName;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 500);
            Size = new Size(860, 560);
            Icon = LoadIcon();
            BuildLayout();

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 60000;
            refreshTimer.Tick += delegate { if (Visible && !busy) { RefreshStatus(); } };
            refreshTimer.Start();
        }

        private void BuildLayout()
        {
            BackColor = Color.FromArgb(244, 247, 249);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24);
            root.RowCount = 7;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Top;
            header.AutoSize = true;
            header.ColumnCount = 2;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            FlowLayoutPanel headerText = new FlowLayoutPanel();
            headerText.FlowDirection = FlowDirection.TopDown;
            headerText.WrapContents = false;
            headerText.AutoSize = true;

            Label title = new Label();
            title.Text = AppName;
            title.Font = new Font("Segoe UI", 23, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(20, 29, 40);
            title.AutoSize = true;
            headerText.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Background apps stay open, but quieter.";
            subtitle.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            subtitle.ForeColor = Color.FromArgb(88, 101, 115);
            subtitle.AutoSize = true;
            subtitle.Margin = new Padding(0, 2, 0, 0);
            headerText.Controls.Add(subtitle);
            header.Controls.Add(headerText, 0, 0);

            statusPill = new Label();
            statusPill.Text = "Checking";
            statusPill.AutoSize = true;
            statusPill.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            statusPill.ForeColor = Color.White;
            statusPill.BackColor = Color.FromArgb(95, 108, 122);
            statusPill.Padding = new Padding(14, 7, 14, 7);
            statusPill.Margin = new Padding(16, 8, 0, 0);
            header.Controls.Add(statusPill, 1, 0);
            root.Controls.Add(header, 0, 0);

            TableLayoutPanel cards = new TableLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.ColumnCount = 4;
            cards.RowCount = 1;
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            root.Controls.Add(cards, 0, 1);

            autoValue = AddStatusCard(cards, 0, "Auto optimize");
            startupValue = AddStatusCard(cards, 1, "Tray startup");
            lastRunValue = AddStatusCard(cards, 2, "Last pass");
            resultValue = AddStatusCard(cards, 3, "Result");

            FlowLayoutPanel toggles = new FlowLayoutPanel();
            toggles.Dock = DockStyle.Fill;
            toggles.AutoSize = true;
            toggles.Margin = new Padding(0, 18, 0, 0);

            autoCheck = new CheckBox();
            autoCheck.Text = "Run automatically";
            autoCheck.AutoSize = true;
            autoCheck.Font = new Font("Segoe UI", 10);
            autoCheck.Margin = new Padding(0, 10, 34, 0);
            autoCheck.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunUserAction(autoCheck.Checked ? "Enabling automatic mode..." : "Pausing automatic mode...",
                    autoCheck.Checked ? "Automatic mode is on." : "Automatic mode is paused.",
                    autoCheck.Checked ? (Func<RunResult>)InstallAutomatic : UninstallAutomatic);
            };
            toggles.Controls.Add(autoCheck);

            startupCheck = new CheckBox();
            startupCheck.Text = "Start with Windows";
            startupCheck.AutoSize = true;
            startupCheck.Font = new Font("Segoe UI", 10);
            startupCheck.Margin = new Padding(0, 10, 0, 0);
            startupCheck.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunUserAction(startupCheck.Checked ? "Enabling startup..." : "Disabling startup...",
                    startupCheck.Checked ? "The tray will start with Windows." : "Tray startup is off.",
                    startupCheck.Checked ? (Func<RunResult>)InstallStartup : UninstallStartup);
            };
            toggles.Controls.Add(startupCheck);

            root.Controls.Add(toggles, 0, 2);

            TableLayoutPanel actionPanel = new TableLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.BackColor = Color.White;
            actionPanel.Padding = new Padding(18);
            actionPanel.ColumnCount = 2;
            actionPanel.RowCount = 1;
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionPanel.Margin = new Padding(0, 0, 0, 14);
            root.Controls.Add(actionPanel, 0, 3);

            FlowLayoutPanel actionText = new FlowLayoutPanel();
            actionText.Dock = DockStyle.Fill;
            actionText.FlowDirection = FlowDirection.TopDown;
            actionText.WrapContents = false;

            actionTitle = new Label();
            actionTitle.Text = "Ready";
            actionTitle.Font = new Font("Segoe UI", 15, FontStyle.Bold);
            actionTitle.ForeColor = Color.FromArgb(24, 35, 48);
            actionTitle.AutoSize = true;
            actionText.Controls.Add(actionTitle);

            actionDetail = new Label();
            actionDetail.Text = "Waiting for the next automatic pass.";
            actionDetail.Font = new Font("Segoe UI", 9);
            actionDetail.ForeColor = Color.FromArgb(90, 103, 116);
            actionDetail.AutoSize = false;
            actionDetail.Width = 440;
            actionDetail.Height = 38;
            actionText.Controls.Add(actionDetail);

            actionProgress = new ProgressBar();
            actionProgress.Width = 420;
            actionProgress.Height = 8;
            actionProgress.Style = ProgressBarStyle.Continuous;
            actionProgress.MarqueeAnimationSpeed = 0;
            actionProgress.Value = 0;
            actionText.Controls.Add(actionProgress);
            actionPanel.Controls.Add(actionText, 0, 0);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            actions.AutoSize = true;
            actions.Dock = DockStyle.Fill;
            actions.Margin = new Padding(18, 10, 0, 0);

            optimizeButton = CreateButton("Optimize now", delegate
            {
                RunUserAction("Optimizing background apps...", "Optimization pass finished.", RunApplyNow);
            }, true, 148);
            actions.Controls.Add(optimizeButton);

            restoreButton = CreateButton("Restore", delegate
            {
                DialogResult confirm = MessageBox.Show("Restore the latest priority and throttling snapshot for currently running processes?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    RunUserAction("Restoring latest snapshot...", "Restore finished.", RunRestore);
                }
            }, false, 108);
            actions.Controls.Add(restoreButton);

            moreButton = CreateButton("More", delegate { ShowMoreMenu(); }, false, 86);
            actions.Controls.Add(moreButton);
            actionPanel.Controls.Add(actions, 1, 0);

            Label footnote = new Label();
            footnote.Text = "Automatic mode runs short scheduled passes and exits. The tray only keeps this control surface available.";
            footnote.Font = new Font("Segoe UI", 9);
            footnote.ForeColor = Color.FromArgb(94, 106, 120);
            footnote.AutoSize = true;
            footnote.Margin = new Padding(0, 0, 0, 0);
            root.Controls.Add(footnote, 0, 4);

            Panel spacer = new Panel();
            spacer.Dock = DockStyle.Fill;
            root.Controls.Add(spacer, 0, 5);

            Label footer = new Label();
            footer.Text = CreatorLine;
            footer.Font = new Font("Segoe UI", 9);
            footer.ForeColor = Color.FromArgb(92, 104, 116);
            footer.AutoSize = true;
            footer.Margin = new Padding(0, 10, 0, 0);
            root.Controls.Add(footer, 0, 6);
        }

        private void ShowMoreMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open log", null, delegate { OpenLog(); });
            menu.Items.Add("Open config", null, delegate { OpenConfig(); });
            menu.Items.Add("Open folder", null, delegate { OpenFolder(); });
            menu.Items.Add("README", null, delegate { OpenReadme(); });
            menu.Items.Add("GitHub", null, delegate { OpenGitHub(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Disable background tasks", null, delegate
            {
                DialogResult confirm = MessageBox.Show("Disable automatic mode and tray startup?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    RunUserAction("Disabling background tasks...", "Background tasks disabled.", UninstallComplete);
                }
            });
            menu.Show(moreButton, new Point(0, moreButton.Height));
        }

        private Label AddStatusCard(TableLayoutPanel parent, int column, string caption)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(column == 0 ? 0 : 8, 0, column == 3 ? 0 : 8, 0);
            panel.BackColor = Color.White;
            panel.Padding = new Padding(14);

            Label title = new Label();
            title.Text = caption;
            title.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            title.ForeColor = Color.FromArgb(91, 104, 118);
            title.AutoSize = true;
            title.Location = new Point(14, 13);
            panel.Controls.Add(title);

            Label value = new Label();
            value.Text = "...";
            value.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            value.ForeColor = Color.FromArgb(24, 32, 43);
            value.Location = new Point(14, 44);
            value.Size = new Size(160, 52);
            value.AutoEllipsis = true;
            panel.Controls.Add(value);

            parent.Controls.Add(panel, column, 0);
            return value;
        }

        private Button CreateButton(string text, EventHandler handler, bool primary, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Font = new Font("Segoe UI", 10, primary ? FontStyle.Bold : FontStyle.Regular);
            button.Width = width;
            button.Height = 40;
            button.Margin = new Padding(0, 0, 10, 10);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = primary ? Color.FromArgb(35, 112, 83) : Color.FromArgb(196, 205, 214);
            button.BackColor = primary ? Color.FromArgb(35, 112, 83) : Color.White;
            button.ForeColor = primary ? Color.White : Color.FromArgb(35, 45, 58);
            button.Click += handler;
            return button;
        }

        public void RefreshStatus()
        {
            if (busy) { return; }
            loading = true;
            bool autoInstalled = IsTaskInstalled(AutoTaskName);
            bool startupInstalled = IsTaskInstalled(TrayTaskName);

            autoValue.Text = autoInstalled ? "On" : "Off";
            autoValue.ForeColor = autoInstalled ? Color.FromArgb(28, 124, 84) : Color.FromArgb(165, 76, 64);

            startupValue.Text = startupInstalled ? "On" : "Off";
            startupValue.ForeColor = startupInstalled ? Color.FromArgb(28, 124, 84) : Color.FromArgb(165, 76, 64);

            lastRunValue.Text = GetLastRunText();
            resultValue.Text = BuildResultText();

            statusPill.Text = autoInstalled ? "Active" : "Manual";
            statusPill.BackColor = autoInstalled ? Color.FromArgb(28, 124, 84) : Color.FromArgb(95, 108, 122);
            actionTitle.Text = autoInstalled ? "Running quietly" : "Manual mode";
            actionDetail.Text = BuildStatusDetail(autoInstalled, startupInstalled);
            actionProgress.Style = ProgressBarStyle.Continuous;
            actionProgress.MarqueeAnimationSpeed = 0;
            actionProgress.Value = 0;

            autoCheck.Checked = autoInstalled;
            startupCheck.Checked = startupInstalled;
            loading = false;
        }

        private string BuildStatusDetail(bool autoInstalled, bool startupInstalled)
        {
            string line = ReadLastLogLine();
            if (line == "No log yet.")
            {
                return autoInstalled ? "Automatic passes are scheduled. No log yet." : "Turn on automatic mode or run a manual pass.";
            }
            return "Last pass: " + BuildResultText() + (startupInstalled ? "" : " | Tray startup is off.");
        }

        private string BuildResultText()
        {
            string line = ReadLastLogLine();
            if (line == "No log yet.")
            {
                return "No run yet";
            }

            string targets = ExtractLogValue(line, "targets");
            string delta = ExtractLogValue(line, "deltaMB");
            if (!String.IsNullOrWhiteSpace(targets))
            {
                string text = targets + " apps";
                if (!String.IsNullOrWhiteSpace(delta))
                {
                    text += " / " + delta + " MB";
                }
                return text;
            }

            return line.Length > 28 ? line.Substring(0, 28) + "..." : line;
        }

        private string ExtractLogValue(string line, string key)
        {
            string marker = key + "=";
            int start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0) { return ""; }
            start += marker.Length;
            int end = line.IndexOf(' ', start);
            if (end < 0) { end = line.Length; }
            return line.Substring(start, end - start).Trim();
        }

        private void SetBusyState(bool isBusy, string title, string detail)
        {
            busy = isBusy;
            optimizeButton.Enabled = !isBusy;
            restoreButton.Enabled = !isBusy;
            moreButton.Enabled = !isBusy;
            autoCheck.Enabled = !isBusy;
            startupCheck.Enabled = !isBusy;
            actionTitle.Text = title;
            actionDetail.Text = detail;
            actionProgress.Style = isBusy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            actionProgress.MarqueeAnimationSpeed = isBusy ? 24 : 0;
            if (!isBusy) { actionProgress.Value = 0; }
        }

        private void RunUserAction(string activeMessage, string successMessage, Func<RunResult> action)
        {
            if (busy) { return; }

            SetBusyState(true, activeMessage, "Working in the background...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                RunResult result = action();
                BeginInvoke(new MethodInvoker(delegate
                {
                    string title = result.ExitCode == 0 ? successMessage : "Action failed";
                    string detail = result.ExitCode == 0 ? BuildResultText() : ShortError(result.Output);
                    busy = false;
                    RefreshStatus();
                    SetBusyState(false, title, detail);
                    if (result.ExitCode != 0)
                    {
                        MessageBox.Show(ShortError(result.Output), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }));
            });
        }

        private string ShortError(string output)
        {
            if (String.IsNullOrWhiteSpace(output))
            {
                return "No details were returned.";
            }
            output = output.Trim();
            return output.Length > 650 ? output.Substring(0, 650) + Environment.NewLine + "..." : output;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshStatus();
        }
    }

    private sealed class RunResult
    {
        public readonly int ExitCode;
        public readonly string Output;

        public RunResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output == null ? "" : output;
        }

        public static RunResult Combine(RunResult first, RunResult second)
        {
            int exitCode = first.ExitCode != 0 ? first.ExitCode : second.ExitCode;
            string output = (first.Output + Environment.NewLine + second.Output).Trim();
            return new RunResult(exitCode, output);
        }
    }
}

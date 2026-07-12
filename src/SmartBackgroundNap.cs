using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

internal static class SmartBackgroundNap
{
    private const string AppName = "Smart Background Nap";
    private const string CreatorLine = "Criado por KaozyKing | Instagram: @oeduardomacedo | GitHub: kingkaozydev";
    private const string AutoTaskName = "SmartBackgroundNap";
    private const string TrayTaskName = "SmartBackgroundNapTray";
    private const string GitHubUrl = "https://github.com/kingkaozydev/smart-background-nap";

    private static string appRoot;
    private static string backgroundScriptPath;
    private static string autoManagerPath;
    private static string trayManagerPath;
    private static string configPath;
    private static string readmePath;
    private static string iconPath;
    private static string outputsPath;
    private static string logPath;

    [STAThread]
    private static void Main(string[] args)
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

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        bool trayOnly = HasArg(args, "--tray");
        SmartNapContext context = new SmartNapContext(trayOnly);
        Application.Run(context);
    }

    private static void InitializePaths()
    {
        string exePath = Application.ExecutablePath;
        string exeDir = Path.GetDirectoryName(exePath);
        if (String.Equals(Path.GetFileName(exeDir), "bin", StringComparison.OrdinalIgnoreCase))
        {
            appRoot = Path.GetFullPath(Path.Combine(exeDir, ".."));
        }
        else
        {
            appRoot = exeDir;
        }

        backgroundScriptPath = Path.Combine(appRoot, "background-nap.ps1");
        autoManagerPath = Path.Combine(appRoot, "manage-background-nap.ps1");
        trayManagerPath = Path.Combine(appRoot, "manage-background-nap-tray.ps1");
        configPath = Path.Combine(appRoot, "game-session.config.json");
        readmePath = Path.Combine(appRoot, "README.md");
        iconPath = Path.Combine(appRoot, "assets\\smart-background-nap.ico");
        outputsPath = Path.Combine(appRoot, "outputs");
        logPath = Path.Combine(outputsPath, "background-nap-auto.log");
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
        return RunPowerShellScript(backgroundScriptPath, "-Action Apply -StateMode Latest -Quiet", 120000);
    }

    private static RunResult RunRestore()
    {
        return RunPowerShellScript(backgroundScriptPath, "-Action Restore", 120000);
    }

    private static RunResult InstallAutomatic()
    {
        return RunPowerShellScript(autoManagerPath, "-Action Install", 60000);
    }

    private static RunResult UninstallAutomatic()
    {
        return RunPowerShellScript(autoManagerPath, "-Action Uninstall", 60000);
    }

    private static RunResult InstallStartup()
    {
        return RunPowerShellScript(trayManagerPath, "-Action Install", 60000);
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

            ToolStripMenuItem apply = new ToolStripMenuItem("Apply now");
            apply.Click += delegate { RunFromTray("Apply now", RunApplyNow); };
            menu.Items.Add(apply);

            ToolStripMenuItem install = new ToolStripMenuItem("Install / update automatic mode");
            install.Click += delegate { RunFromTray("Install", InstallComplete); };
            menu.Items.Add(install);

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
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                mainWindow.Close();
                Application.Exit();
            };
            menu.Items.Add(exit);

            return menu;
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
        private Label logValue;
        private CheckBox autoCheck;
        private CheckBox startupCheck;
        private bool loading;
        private Timer refreshTimer;

        public MainWindow()
        {
            Text = AppName;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(780, 560);
            Size = new Size(900, 640);
            Icon = LoadIcon();
            BuildLayout();

            refreshTimer = new Timer();
            refreshTimer.Interval = 15000;
            refreshTimer.Tick += delegate { RefreshStatus(); };
            refreshTimer.Start();
        }

        private void BuildLayout()
        {
            BackColor = Color.FromArgb(246, 248, 250);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24);
            root.RowCount = 6;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            Label title = new Label();
            title.Text = AppName;
            title.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(24, 32, 43);
            title.AutoSize = true;
            root.Controls.Add(title, 0, 0);

            Label subtitle = new Label();
            subtitle.Text = "Keep background apps quieter without closing them.";
            subtitle.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            subtitle.ForeColor = Color.FromArgb(84, 96, 110);
            subtitle.AutoSize = true;
            subtitle.Margin = new Padding(0, 0, 0, 18);
            root.Controls.Add(subtitle, 0, 1);

            TableLayoutPanel cards = new TableLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.ColumnCount = 4;
            cards.RowCount = 1;
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            root.Controls.Add(cards, 0, 2);

            autoValue = AddStatusCard(cards, 0, "Automatic mode");
            startupValue = AddStatusCard(cards, 1, "Start with Windows");
            lastRunValue = AddStatusCard(cards, 2, "Last apply");
            logValue = AddStatusCard(cards, 3, "Last log");

            FlowLayoutPanel toggles = new FlowLayoutPanel();
            toggles.Dock = DockStyle.Fill;
            toggles.AutoSize = true;
            toggles.Margin = new Padding(0, 18, 0, 10);

            autoCheck = new CheckBox();
            autoCheck.Text = "Automatic optimization";
            autoCheck.AutoSize = true;
            autoCheck.Font = new Font("Segoe UI", 10);
            autoCheck.Margin = new Padding(0, 0, 28, 0);
            autoCheck.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunUserAction(autoCheck.Checked ? "Automatic optimization enabled." : "Automatic optimization disabled.",
                    autoCheck.Checked ? (Func<RunResult>)InstallAutomatic : UninstallAutomatic);
            };
            toggles.Controls.Add(autoCheck);

            startupCheck = new CheckBox();
            startupCheck.Text = "Start tray with Windows";
            startupCheck.AutoSize = true;
            startupCheck.Font = new Font("Segoe UI", 10);
            startupCheck.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunUserAction(startupCheck.Checked ? "Tray startup enabled." : "Tray startup disabled.",
                    startupCheck.Checked ? (Func<RunResult>)InstallStartup : UninstallStartup);
            };
            toggles.Controls.Add(startupCheck);

            root.Controls.Add(toggles, 0, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.AutoScroll = true;
            actions.WrapContents = true;
            actions.Margin = new Padding(0, 6, 0, 12);
            root.Controls.Add(actions, 0, 4);

            actions.Controls.Add(CreateButton("Install / update", delegate { RunUserAction("Complete setup installed.", InstallComplete); }, true));
            actions.Controls.Add(CreateButton("Apply now", delegate { RunUserAction("Optimization pass finished.", RunApplyNow); }, false));
            actions.Controls.Add(CreateButton("Restore", delegate
            {
                DialogResult confirm = MessageBox.Show("Restore the latest priority and throttling snapshot for currently running processes?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    RunUserAction("Restore finished.", RunRestore);
                }
            }, false));
            actions.Controls.Add(CreateButton("Open log", delegate { OpenLog(); }, false));
            actions.Controls.Add(CreateButton("Open config", delegate { OpenConfig(); }, false));
            actions.Controls.Add(CreateButton("Open folder", delegate { OpenFolder(); }, false));
            actions.Controls.Add(CreateButton("README", delegate { OpenReadme(); }, false));
            actions.Controls.Add(CreateButton("GitHub", delegate { OpenGitHub(); }, false));
            actions.Controls.Add(CreateButton("Uninstall all", delegate
            {
                DialogResult confirm = MessageBox.Show("Disable automatic mode and tray startup?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    RunUserAction("Smart Background Nap was uninstalled from startup tasks.", UninstallComplete);
                }
            }, false));

            Label footer = new Label();
            footer.Text = CreatorLine;
            footer.Font = new Font("Segoe UI", 9);
            footer.ForeColor = Color.FromArgb(92, 104, 116);
            footer.AutoSize = true;
            footer.Margin = new Padding(0, 10, 0, 0);
            root.Controls.Add(footer, 0, 5);
        }

        private Label AddStatusCard(TableLayoutPanel parent, int column, string caption)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(column == 0 ? 0 : 8, 0, column == 3 ? 0 : 8, 0);
            panel.BackColor = Color.White;
            panel.Padding = new Padding(16);

            Label title = new Label();
            title.Text = caption;
            title.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            title.ForeColor = Color.FromArgb(91, 104, 118);
            title.AutoSize = true;
            title.Location = new Point(16, 14);
            panel.Controls.Add(title);

            Label value = new Label();
            value.Text = "...";
            value.Font = new Font("Segoe UI", 13, FontStyle.Bold);
            value.ForeColor = Color.FromArgb(24, 32, 43);
            value.Location = new Point(16, 46);
            value.Size = new Size(170, 58);
            value.AutoEllipsis = true;
            panel.Controls.Add(value);

            parent.Controls.Add(panel, column, 0);
            return value;
        }

        private Button CreateButton(string text, EventHandler handler, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.Font = new Font("Segoe UI", 10, primary ? FontStyle.Bold : FontStyle.Regular);
            button.Width = primary ? 178 : 132;
            button.Height = 42;
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
            loading = true;
            bool autoInstalled = IsTaskInstalled(AutoTaskName);
            bool startupInstalled = IsTaskInstalled(TrayTaskName);

            autoValue.Text = autoInstalled ? "On" : "Off";
            autoValue.ForeColor = autoInstalled ? Color.FromArgb(28, 124, 84) : Color.FromArgb(165, 76, 64);

            startupValue.Text = startupInstalled ? "On" : "Off";
            startupValue.ForeColor = startupInstalled ? Color.FromArgb(28, 124, 84) : Color.FromArgb(165, 76, 64);

            lastRunValue.Text = GetLastRunText();
            logValue.Text = ReadLastLogLine();

            autoCheck.Checked = autoInstalled;
            startupCheck.Checked = startupInstalled;
            loading = false;
        }

        private void RunUserAction(string successMessage, Func<RunResult> action)
        {
            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            RunResult result;
            try
            {
                result = action();
            }
            finally
            {
                Cursor.Current = previous;
            }

            RefreshStatus();
            if (result.ExitCode == 0)
            {
                MessageBox.Show(successMessage, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                string output = result.Output;
                if (output.Length > 3000)
                {
                    output = output.Substring(0, 3000) + Environment.NewLine + "...";
                }
                MessageBox.Show(output, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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

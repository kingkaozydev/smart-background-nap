using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

internal static class SmartBackgroundNapTray
{
    private const string CreatorLine = "Criado por KaozyKing | Instagram: @oeduardomacedo | GitHub: kingkaozydev";
    private static NotifyIcon notifyIcon;
    private static string autoTaskName = "SmartBackgroundNap";
    private static string managerPath = "";
    private static string logPath = "";
    private static string folderPath = "";
    private static string readmePath = "";
    private static string iconPath = "";

    [STAThread]
    private static void Main(string[] args)
    {
        ParseArgs(args);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        notifyIcon = new NotifyIcon();
        notifyIcon.Icon = LoadIcon();
        notifyIcon.Visible = true;
        notifyIcon.Text = "Smart Background Nap: active";
        notifyIcon.ContextMenuStrip = BuildMenu();
        notifyIcon.DoubleClick += delegate { ShowStatus(); };

        notifyIcon.BalloonTipTitle = "Smart Background Nap";
        notifyIcon.BalloonTipText = "Tray indicator active.";
        notifyIcon.ShowBalloonTip(2000);

        Application.Run();

        notifyIcon.Visible = false;
        notifyIcon.Dispose();
    }

    private static void ParseArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            string value = i + 1 < args.Length ? args[i + 1] : "";
            if (!key.StartsWith("--", StringComparison.Ordinal)) { continue; }
            i++;

            switch (key)
            {
                case "--auto-task":
                    autoTaskName = value;
                    break;
                case "--manager":
                    managerPath = value;
                    break;
                case "--log":
                    logPath = value;
                    break;
                case "--folder":
                    folderPath = value;
                    break;
                case "--readme":
                    readmePath = value;
                    break;
                case "--icon":
                    iconPath = value;
                    break;
            }
        }
    }

    private static Icon LoadIcon()
    {
        try
        {
            if (!String.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }
        }
        catch
        {
        }

        return SystemIcons.Application;
    }

    private static ContextMenuStrip BuildMenu()
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem title = new ToolStripMenuItem("Smart Background Nap");
        title.Enabled = false;
        menu.Items.Add(title);
        ToolStripMenuItem creator = new ToolStripMenuItem(CreatorLine);
        creator.Enabled = false;
        menu.Items.Add(creator);
        menu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem status = new ToolStripMenuItem("Status");
        status.Click += delegate { ShowStatus(); };
        menu.Items.Add(status);

        ToolStripMenuItem apply = new ToolStripMenuItem("Optimize now");
        apply.Click += delegate { ApplyNow(); };
        menu.Items.Add(apply);

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

        ToolStripMenuItem exit = new ToolStripMenuItem("Exit tray icon");
        exit.Click += delegate
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            Application.Exit();
        };
        menu.Items.Add(exit);

        return menu;
    }

    private static string RunHidden(string fileName, string arguments, int timeoutMs)
    {
        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = fileName;
        psi.Arguments = arguments;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        using (Process process = Process.Start(psi))
        {
            if (process == null) { return ""; }
            StringBuilder output = new StringBuilder();
            output.Append(process.StandardOutput.ReadToEnd());
            output.Append(process.StandardError.ReadToEnd());
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                output.AppendLine("Timed out.");
            }
            return output.ToString().Trim();
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static void ShowStatus()
    {
        string statusText = "";
        if (!String.IsNullOrWhiteSpace(managerPath) && File.Exists(managerPath))
        {
            statusText = RunHidden(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -File " + Quote(managerPath) + " -Action Status",
                8000);
        }
        else
        {
            statusText = RunHidden(
                "schtasks.exe",
                "/Query /TN " + Quote(autoTaskName) + " /FO LIST /V",
                8000);
        }

        string lastLog = ReadLastLogLine();
        string message = "Smart Background Nap" + Environment.NewLine +
                         CreatorLine + Environment.NewLine + Environment.NewLine +
                         statusText + Environment.NewLine + Environment.NewLine +
                         "Last log:" + Environment.NewLine + lastLog;

        MessageBox.Show(message, "Smart Background Nap", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string ReadLastLogLine()
    {
        try
        {
            if (String.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
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
                    last = line;
                }
            }
            return String.IsNullOrWhiteSpace(last) ? "No log yet." : last;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static void ApplyNow()
    {
        RunHidden("schtasks.exe", "/Run /TN " + Quote(autoTaskName), 8000);
        notifyIcon.BalloonTipTitle = "Smart Background Nap";
        notifyIcon.BalloonTipText = "Apply requested.";
        notifyIcon.ShowBalloonTip(2500);
    }

    private static void OpenLog()
    {
        try
        {
            if (String.IsNullOrWhiteSpace(logPath)) { return; }
            if (!File.Exists(logPath))
            {
                using (File.Create(logPath)) { }
            }
            Process.Start("notepad.exe", Quote(logPath));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Smart Background Nap", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void OpenFolder()
    {
        if (!String.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
        {
            Process.Start("explorer.exe", Quote(folderPath));
        }
    }

    private static void OpenReadme()
    {
        if (!String.IsNullOrWhiteSpace(readmePath) && File.Exists(readmePath))
        {
            Process.Start("notepad.exe", Quote(readmePath));
        }
    }
}

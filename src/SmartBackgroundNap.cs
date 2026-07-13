using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
#if NET9_0_OR_GREATER
using System.Text.Json;
#else
using System.Web.Script.Serialization;
#endif
using System.Threading;
using System.Windows.Forms;
#if NET9_0_OR_GREATER
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
#endif

internal static class SmartBackgroundNap
{
    private const string AppName = "Smart Background Nap";
    private const string AppVersion = "0.3.4";
    private const string CreatorLine = "Criado por KaozyKing | GitHub: kingkaozydev";
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
    private static string securityModelPath;
    private static string iconPath;
    private static string logoPath;
    private static string heroPath;
    private static string uiSettingsPath;
    private static string uiLanguage;
    private static string outputsPath;
    private static string logPath;
    private static string scorePath;
    private static string safetyReportPath;
    private static bool usingLooseRuntime;
    private const uint ProcessSetInformation = 0x0200;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ProcessMemoryPriorityClass = 0;
    private const int ProcessPowerThrottlingClass = 4;
    private const int ProcessIoPriorityClass = 33;
    private const uint ProcessPowerThrottlingCurrentVersion = 1;
    private const uint ProcessPowerThrottlingExecutionSpeed = 0x1;
    private const uint ProcessPowerThrottlingIgnoreTimerResolution = 0x4;

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryPriorityInformation
    {
        public uint MemoryPriority;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }
    private static Mutex singleInstanceMutex;
    private static EventWaitHandle showDashboardEvent;
    private static ScoreWindow scoreWindow;

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
        if (HasArg(args, "--safety-report"))
        {
            WriteSafetyReport();
            Environment.ExitCode = 0;
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
        Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
        {
            WriteCrash(e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                WriteCrash(ex);
            }
        };
        if (SynchronizationContext.Current == null)
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        }

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
            usingLooseRuntime = true;
        }
        else
        {
            appRoot = GetWritableAppRoot();
            string runtimeRoot = Path.Combine(appRoot, "runtime");
            EnsureRuntimeFiles(runtimeRoot);
            looseRoot = runtimeRoot;
            usingLooseRuntime = false;
        }

        backgroundScriptPath = Path.Combine(looseRoot, "background-nap.ps1");
        autoManagerPath = Path.Combine(looseRoot, "manage-background-nap.ps1");
        trayManagerPath = Path.Combine(looseRoot, "manage-background-nap-tray.ps1");
        configPath = Path.Combine(looseRoot, "game-session.config.json");
        readmePath = Path.Combine(looseRoot, "README.md");
        securityModelPath = Path.Combine(looseRoot, "SECURITY_MODEL.md");
        if (!File.Exists(securityModelPath) && File.Exists(Path.Combine(looseRoot, "docs\\SECURITY_MODEL.md")))
        {
            securityModelPath = Path.Combine(looseRoot, "docs\\SECURITY_MODEL.md");
        }
        iconPath = Path.Combine(looseRoot, "assets\\smart-nap-logo.ico");
        logoPath = Path.Combine(looseRoot, "assets\\smart-nap-logo-v2.png");
        heroPath = Path.Combine(looseRoot, "assets\\smart-nap-hero-bg.png");
        uiSettingsPath = Path.Combine(appRoot, "ui-settings.json");
        uiLanguage = LoadUiLanguage();
        outputsPath = Path.Combine(appRoot, "outputs");
        logPath = Path.Combine(outputsPath, "background-nap-auto.log");
        scorePath = Path.Combine(outputsPath, "background-nap-score-latest.json");
        safetyReportPath = Path.Combine(outputsPath, "SmartBackgroundNap-SafetyReport.txt");
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr processHandle, int processInformationClass, IntPtr processInformation, uint processInformationSize);

    [DllImport("ntdll.dll")]
    private static extern int NtSetInformationProcess(IntPtr processHandle, int processInformationClass, ref uint processInformation, uint processInformationLength);

    private static int GetForegroundPid()
    {
        try
        {
            uint pid;
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) { return 0; }
            GetWindowThreadProcessId(hwnd, out pid);
            return (int)pid;
        }
        catch
        {
            return 0;
        }
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

    private static string NormalizeUiLanguage(string value)
    {
        if (String.IsNullOrWhiteSpace(value)) { return ""; }
        string code = value.Trim().Replace('_', '-').ToLowerInvariant();
        if (code.StartsWith("pt")) { return "pt-BR"; }
        if (code.StartsWith("ru")) { return "ru-RU"; }
        if (code.StartsWith("es")) { return "es-ES"; }
        if (code.StartsWith("fr")) { return "fr-FR"; }
        if (code.StartsWith("de")) { return "de-DE"; }
        if (code.StartsWith("en")) { return "en-US"; }
        return "";
    }

    private static string LoadUiLanguage()
    {
        try
        {
            if (String.IsNullOrWhiteSpace(uiSettingsPath) || !File.Exists(uiSettingsPath)) { return ""; }
            IDictionary<string, object> settings = JsonCompat.DeserializeObject(File.ReadAllText(uiSettingsPath, Encoding.UTF8));
            object value;
            return NormalizeUiLanguage(settings != null && settings.TryGetValue("Language", out value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : "");
        }
        catch
        {
            return "";
        }
    }

    private static void SaveUiLanguage(string language)
    {
        string normalized = NormalizeUiLanguage(language);
        if (String.IsNullOrWhiteSpace(normalized)) { return; }
        uiLanguage = normalized;
        try
        {
            Directory.CreateDirectory(appRoot);
            File.WriteAllText(uiSettingsPath, "{ \"Language\": \"" + normalized + "\" }", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
        }
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
        ExtractConfigResource("game_session_config_json", Path.Combine(runtimeRoot, "game-session.config.json"));
        ExtractResource("readme_md", Path.Combine(runtimeRoot, "README.md"));
        ExtractResource("security_model_md", Path.Combine(runtimeRoot, "SECURITY_MODEL.md"));
        ExtractResource("icon_ico", Path.Combine(runtimeRoot, "assets\\smart-nap-logo.ico"));
        ExtractResource("logo_png", Path.Combine(runtimeRoot, "assets\\smart-nap-logo-v2.png"));
        ExtractResource("hero_png", Path.Combine(runtimeRoot, "assets\\smart-nap-hero-bg.png"));
    }

    private static void ExtractConfigResource(string resourceName, string targetPath)
    {
        string defaultJson = ReadEmbeddedText(resourceName);
        if (String.IsNullOrWhiteSpace(defaultJson))
        {
            throw new InvalidOperationException("Missing embedded config: " + ResourcePrefix + resourceName);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
        if (!File.Exists(targetPath))
        {
            File.WriteAllText(targetPath, defaultJson, Encoding.UTF8);
            return;
        }

        try
        {
            IDictionary<string, object> defaults = JsonCompat.DeserializeObject(defaultJson);
            IDictionary<string, object> current = JsonCompat.DeserializeObject(File.ReadAllText(targetPath, Encoding.UTF8));
            if (current == null || defaults == null)
            {
                return;
            }
            if (MergeMissingConfigValues(current, defaults))
            {
                File.WriteAllText(targetPath, JsonCompat.SerializeObject(current), Encoding.UTF8);
            }
        }
        catch
        {
            // Keep the existing user config if it cannot be merged safely.
        }
    }

    private static string ReadEmbeddedText(string resourceName)
    {
        string fullName = ResourcePrefix + resourceName;
        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fullName))
        {
            if (stream == null) { return ""; }
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }

    private static bool MergeMissingConfigValues(IDictionary<string, object> current, IDictionary<string, object> defaults)
    {
        bool changed = false;
        foreach (KeyValuePair<string, object> pair in defaults)
        {
            object existing;
            if (!current.TryGetValue(pair.Key, out existing))
            {
                current[pair.Key] = pair.Value;
                changed = true;
                continue;
            }

            IDictionary<string, object> existingMap = existing as IDictionary<string, object>;
            IDictionary<string, object> defaultMap = pair.Value as IDictionary<string, object>;
            if (existingMap != null && defaultMap != null && MergeMissingConfigValues(existingMap, defaultMap))
            {
                changed = true;
            }
        }
        return changed;
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

    private static Image LoadLogoImage()
    {
        try
        {
            if (File.Exists(logoPath))
            {
                using (Image image = Image.FromFile(logoPath))
                {
                    return new Bitmap(image);
                }
            }
        }
        catch
        {
        }

        try
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + "logo_png"))
            {
                if (stream != null)
                {
                    using (Image image = Image.FromStream(stream))
                    {
                        return new Bitmap(image);
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static RunResult RunHidden(string fileName, string arguments, int timeoutMs)
    {
        return RunHidden(fileName, arguments, timeoutMs, null);
    }

    private static RunResult RunHidden(string fileName, string arguments, int timeoutMs, RunControl control)
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

                if (control != null)
                {
                    control.SetProcess(process);
                    if (control.CancelRequested)
                    {
                        try { process.Kill(); } catch { }
                    }
                }

                DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                bool timedOut = false;
                while (!process.WaitForExit(150))
                {
                    if (control != null && control.CancelRequested)
                    {
                        try { process.Kill(); } catch { }
                        output.AppendLine("Stopped by user.");
                        break;
                    }
                    if (DateTime.UtcNow > deadline)
                    {
                        try { process.Kill(); } catch { }
                        output.AppendLine("Timed out.");
                        timedOut = true;
                        break;
                    }
                }

                try
                {
                    if (!process.HasExited)
                    {
                        process.WaitForExit(3000);
                    }
                }
                catch
                {
                }

                if (!process.HasExited)
                {
                    return new RunResult(1, (output.ToString() + Environment.NewLine + "Process did not exit after stop request.").Trim());
                }

                output.Append(process.StandardOutput.ReadToEnd());
                output.Append(process.StandardError.ReadToEnd());

                if (control != null && control.CancelRequested)
                {
                    return new RunResult(130, output.ToString().Trim());
                }
                if (timedOut)
                {
                    return new RunResult(124, output.ToString().Trim());
                }

                return new RunResult(process.ExitCode, output.ToString().Trim());
            }
        }
        catch (Exception ex)
        {
            return new RunResult(1, ex.Message);
        }
        finally
        {
            if (control != null)
            {
                control.ClearProcess();
            }
        }
    }

    private static RunResult RunPowerShellScript(string scriptPath, string arguments, int timeoutMs)
    {
        return RunPowerShellScript(scriptPath, arguments, timeoutMs, null);
    }

    private static RunResult RunPowerShellScript(string scriptPath, string arguments, int timeoutMs, RunControl control)
    {
        if (!File.Exists(scriptPath))
        {
            return new RunResult(1, "Missing script: " + scriptPath);
        }

        string psArgs = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File " + Quote(scriptPath) + " " + arguments;
        return RunHidden("powershell.exe", psArgs, timeoutMs, control);
    }

    private static void AppendOperationalLog(string text)
    {
        try
        {
            Directory.CreateDirectory(outputsPath);
            File.AppendAllText(logPath, DateTime.Now.ToString("s", CultureInfo.InvariantCulture) + " " + text + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private static RunResult RunApplyNow()
    {
        return RunApplyNow(null);
    }

    private static RunResult RunApplyNow(RunControl control)
    {
        Directory.CreateDirectory(outputsPath);
        return RunPowerShellScript(backgroundScriptPath, "-Action Apply -StateMode Latest -Quiet -LogPath " + Quote(logPath), 120000, control);
    }

    private static RunResult RunElevatedApply()
    {
        try
        {
            Directory.CreateDirectory(outputsPath);
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = GetLaunchExecutablePath();
            start.Arguments = "--apply";
            start.UseShellExecute = true;
            start.Verb = "runas";
            start.WindowStyle = ProcessWindowStyle.Hidden;

            using (Process process = Process.Start(start))
            {
                if (process == null)
                {
                    return new RunResult(1, "Could not start elevated optimizer pass.");
                }
                if (!process.WaitForExit(180000))
                {
                    try { process.Kill(); } catch { }
                    AppendOperationalLog("action=elevated-apply status=timeout");
                    return new RunResult(124, "Elevated optimizer pass timed out.");
                }
                AppendOperationalLog("action=elevated-apply status=done exitCode=" + process.ExitCode.ToString(CultureInfo.InvariantCulture));
                return new RunResult(process.ExitCode, process.ExitCode == 0 ? "Elevated pass finished." : "Elevated pass exited with code " + process.ExitCode.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }
        catch (Win32Exception ex)
        {
            if (ex.NativeErrorCode == 1223)
            {
                AppendOperationalLog("action=elevated-apply status=cancelled");
                return new RunResult(1223, "Administrator permission was cancelled.");
            }
            return new RunResult(1, ex.Message);
        }
        catch (Exception ex)
        {
            return new RunResult(1, ex.Message);
        }
    }

    private static RunResult RunRestore()
    {
        return RunPowerShellScript(backgroundScriptPath, "-Action Restore -LogPath " + Quote(logPath), 120000);
    }

    private static RunResult RunForegroundRestore(int pid)
    {
        if (pid <= 0)
        {
            return new RunResult(0, "No foreground pid.");
        }
        return RunPowerShellScript(backgroundScriptPath, "-Action ForegroundRestore -TargetPid " + pid.ToString() + " -StateMode Latest -Quiet -LogPath " + Quote(logPath), 30000);
    }

    private static RunResult InstallAutomatic()
    {
        return RunPowerShellScript(autoManagerPath, "-Action Install -AppExePath " + Quote(GetLaunchExecutablePath()), 60000);
    }

    private static RunResult UninstallAutomatic()
    {
        return RunPowerShellScript(autoManagerPath, "-Action Uninstall", 60000);
    }

    private static RunResult InstallStartup()
    {
        return RunPowerShellScript(trayManagerPath, "-Action Install -AppExePath " + Quote(GetLaunchExecutablePath()), 60000);
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

    private static bool IsCurrentProcessElevated()
    {
        try
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        catch
        {
            return false;
        }
    }

    private static IDictionary<string, object> LoadConfigRoot()
    {
        if (!File.Exists(configPath)) { return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); }
        IDictionary<string, object> root = JsonCompat.DeserializeObject(File.ReadAllText(configPath, Encoding.UTF8));
        return root ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static IDictionary<string, object> GetOrCreateMap(IDictionary<string, object> root, string key)
    {
        object value;
        IDictionary<string, object> map = null;
        if (root != null && root.TryGetValue(key, out value))
        {
            map = value as IDictionary<string, object>;
        }
        if (map == null)
        {
            map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            root[key] = map;
        }
        return map;
    }

    private static bool IsSmartLearningEnabled()
    {
        try
        {
            IDictionary<string, object> root = LoadConfigRoot();
            object smartObject;
            IDictionary<string, object> smart = root.TryGetValue("SmartMode", out smartObject) ? smartObject as IDictionary<string, object> : null;
            object enabled;
            return smart != null && smart.TryGetValue("LearningEnabled", out enabled) && Convert.ToBoolean(enabled, CultureInfo.InvariantCulture);
        }
        catch
        {
            return false;
        }
    }

    private static RunResult SetSmartLearningEnabled(bool enabled)
    {
        try
        {
            IDictionary<string, object> root = LoadConfigRoot();
            IDictionary<string, object> smart = GetOrCreateMap(root, "SmartMode");
            smart["LearningEnabled"] = enabled;
            File.WriteAllText(configPath, JsonCompat.SerializeObject(root), Encoding.UTF8);
            Directory.CreateDirectory(outputsPath);
            string line = DateTime.Now.ToString("s", CultureInfo.InvariantCulture) + " action=learning enabled=" + enabled.ToString().ToLowerInvariant();
            File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
            return new RunResult(0, enabled ? "Smart Learning enabled." : "Smart Learning disabled.");
        }
        catch (Exception ex)
        {
            return new RunResult(1, ex.Message);
        }
    }

    private static int GetLearningProfileCount()
    {
        try
        {
            string path = Path.Combine(outputsPath, "background-nap-learning-latest.json");
            if (!File.Exists(path)) { return 0; }
            IDictionary<string, object> root = JsonCompat.DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
            object items = null;
            System.Collections.IEnumerable enumerable = root != null && root.TryGetValue("Items", out items) ? items as System.Collections.IEnumerable : null;
            if (enumerable == null || items is string) { return 0; }
            int count = 0;
            foreach (object ignored in enumerable) { count++; }
            return count;
        }
        catch
        {
            return 0;
        }
    }

    private static string GetLaunchExecutablePath()
    {
        if (usingLooseRuntime)
        {
            return Application.ExecutablePath;
        }

        try
        {
            string installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "SmartBackgroundNap");
            Directory.CreateDirectory(installDir);

            string target = Path.Combine(installDir, "SmartBackgroundNap.exe");
            string current = Application.ExecutablePath;
            if (!String.Equals(Path.GetFullPath(current), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(current, target, true);
            }
            return target;
        }
        catch
        {
            return Application.ExecutablePath;
        }
    }


    private static string GetAssemblyVersionText()
    {
        try
        {
            AssemblyInformationalVersionAttribute info =
                (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    Assembly.GetExecutingAssembly(),
                    typeof(AssemblyInformationalVersionAttribute));
            if (info != null && !String.IsNullOrWhiteSpace(info.InformationalVersion))
            {
                return info.InformationalVersion;
            }
        }
        catch
        {
        }

        return AppVersion;
    }

    private static string ComputeFileSha256(string path)
    {
        try
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
        catch (Exception ex)
        {
            return "Unavailable: " + ex.Message;
        }
    }

    private static string IsAdministratorText()
    {
        try
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator) ? "yes" : "no";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string BuildTaskStatusLine(string taskName)
    {
        return taskName + ": " + (IsTaskInstalled(taskName) ? "installed" : "not installed");
    }

    private static string WriteSafetyReport()
    {
        Directory.CreateDirectory(outputsPath);

        StringBuilder report = new StringBuilder();
        report.AppendLine("Smart Background Nap safety report");
        report.AppendLine("Generated: " + DateTime.Now.ToString("s"));
        report.AppendLine("Version: " + GetAssemblyVersionText());
        report.AppendLine("Creator: KaozyKing");
        report.AppendLine();
        report.AppendLine("Local identity");
        report.AppendLine("Executable: " + Application.ExecutablePath);
        report.AppendLine("Executable SHA-256: " + ComputeFileSha256(Application.ExecutablePath));
        report.AppendLine("Runs as administrator: " + IsAdministratorText());
        report.AppendLine("Runtime folder: " + Path.GetDirectoryName(backgroundScriptPath));
        report.AppendLine("Writable data folder: " + appRoot);
        report.AppendLine("Managed startup copy: " + GetLaunchExecutablePath());
        report.AppendLine();
        report.AppendLine("Windows integration");
        report.AppendLine(BuildTaskStatusLine(AutoTaskName));
        report.AppendLine(BuildTaskStatusLine(TrayTaskName));
        report.AppendLine("Startup method: per-user scheduled tasks, least privilege.");
        report.AppendLine("Service installed: no.");
        report.AppendLine("Driver installed: no.");
        report.AppendLine("Startup registry key: no.");
        report.AppendLine();
        report.AppendLine("Data and network posture");
        report.AppendLine("Network access: none by design.");
        report.AppendLine("Telemetry: none.");
        report.AppendLine("Accounts, passwords, cookies, browser profiles, documents, and game files: not read.");
        report.AppendLine("Local files written: config, compact logs, restore snapshots, embedded runtime files, this report.");
        report.AppendLine();
        report.AppendLine("Optimization scope");
        report.AppendLine("Allowed actions: process priority, memory priority, process I/O priority, Windows power throttling/EcoQoS, timer-resolution isolation, foreground wake restore, temporary active-app protection, burst scoring, fullscreen-aware thresholds, optional local Smart Learning profiles, optional working-set trimming.");
        report.AppendLine("Skipped targets: Windows/system processes, session 0 services, foreground app, high-CPU active workloads, configured protected apps, configured protected paths.");
        report.AppendLine("Destructive actions: none. It does not kill apps, delete files, change drivers, change power plans, overclock, undervolt, or disable Windows services.");
        report.AppendLine();
        report.AppendLine("Audit files");
        report.AppendLine("Config: " + configPath);
        report.AppendLine("Log: " + logPath);
        report.AppendLine("Security model: " + securityModelPath);
        report.AppendLine("Source: " + GitHubUrl);

        File.WriteAllText(safetyReportPath, report.ToString(), Encoding.UTF8);
        return safetyReportPath;
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

    private static string ReadLastApplyLogLine()
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
                    if (line.IndexOf("action=apply", StringComparison.OrdinalIgnoreCase) >= 0)
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

    private static void OpenScore()
    {
        try
        {
            Directory.CreateDirectory(outputsPath);
            if (!File.Exists(scorePath))
            {
                File.WriteAllText(scorePath, "{ \"Items\": [] }", Encoding.UTF8);
            }

            if (scoreWindow == null || scoreWindow.IsDisposed)
            {
                scoreWindow = new ScoreWindow(scorePath);
            }

            scoreWindow.RefreshScore();
            if (!scoreWindow.Visible)
            {
                Form owner = Form.ActiveForm;
                if (owner != null && !Object.ReferenceEquals(owner, scoreWindow))
                {
                    scoreWindow.Show(owner);
                }
                else
                {
                    scoreWindow.Show();
                }
            }
            if (scoreWindow.WindowState == FormWindowState.Minimized)
            {
                scoreWindow.WindowState = FormWindowState.Normal;
            }
            scoreWindow.Activate();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void OpenSafetyReport()
    {
        try
        {
            OpenExternal(WriteSafetyReport());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void OpenSecurityModel()
    {
        OpenExternal(securityModelPath);
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
#if NET9_0_OR_GREATER
        private readonly WpfDashboardHost dashboardHost;
#else
        private ModernMainWindow mainWindow;
#endif
        private readonly Form dispatchForm;
        private readonly SynchronizationContext uiContext;
        private bool allowExit;
        private bool listenerStopping;
        private Thread showThread;
        private System.Windows.Forms.Timer foregroundWakeTimer;
        private int lastForegroundPid;
        private bool foregroundRestoreBusy;
        private DateTime lastForegroundRestoreAt = DateTime.MinValue;

        public SmartNapContext(bool trayOnly)
        {
            uiContext = SynchronizationContext.Current;
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = LoadIcon();
            notifyIcon.Text = AppName + ": active";
            notifyIcon.Visible = true;
            notifyIcon.ContextMenuStrip = BuildMenu();
            notifyIcon.DoubleClick += delegate { ShowMainWindow(); };

            dispatchForm = new Form();
            dispatchForm.ShowInTaskbar = false;
            dispatchForm.FormBorderStyle = FormBorderStyle.None;
            dispatchForm.StartPosition = FormStartPosition.Manual;
            dispatchForm.Size = new Size(1, 1);
            dispatchForm.Location = new Point(-32000, -32000);
            dispatchForm.Opacity = 0;
            dispatchForm.Text = "";
            dispatchForm.Show();

#if NET9_0_OR_GREATER
            dashboardHost = new WpfDashboardHost(delegate
            {
                if (uiContext != null)
                {
                    uiContext.Post(delegate { ShowTrayMessage("Still running in the tray."); }, null);
                }
                else
                {
                    ShowTrayMessage("Still running in the tray.");
                }
            });
#else
            mainWindow = CreateMainWindow();
#endif

            if (!trayOnly)
            {
                ShowMainWindow();
            }
            else
            {
                ShowTrayMessage("Ready. Automatic mode can be controlled from the tray.");
            }

            StartShowListener();
            StartForegroundWakeTimer();
        }

        private ModernMainWindow CreateMainWindow()
        {
            ModernMainWindow window = new ModernMainWindow();
            window.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (!allowExit)
                {
                    e.Cancel = true;
                    window.Hide();
                    ShowTrayMessage("Still running in the tray.");
                }
            };
            return window;
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

            ToolStripMenuItem safety = new ToolStripMenuItem("Safety report");
            safety.Click += delegate { OpenSafetyReport(); };
            menu.Items.Add(safety);

            ToolStripMenuItem score = new ToolStripMenuItem("Nap score");
            score.Click += delegate { OpenScore(); };
            menu.Items.Add(score);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate
            {
                allowExit = true;
                listenerStopping = true;
                if (foregroundWakeTimer != null)
                {
                    foregroundWakeTimer.Stop();
                    foregroundWakeTimer.Dispose();
                    foregroundWakeTimer = null;
                }
                try { showDashboardEvent.Set(); } catch { }
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                try { dispatchForm.Close(); } catch { }
                try { dispatchForm.Dispose(); } catch { }
#if NET9_0_OR_GREATER
                dashboardHost.Shutdown();
#else
                mainWindow.Close();
#endif
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
                        bool posted = false;
                        try
                        {
                            dispatchForm.BeginInvoke(new System.Windows.Forms.MethodInvoker(delegate { ShowMainWindow(); }));
                            posted = true;
                        }
                        catch
                        {
                        }

                        if (!posted && uiContext != null)
                        {
                            uiContext.Post(delegate
                            {
                                try { ShowMainWindow(); } catch { }
                            }, null);
                        }
                    }
                    catch
                    {
                        if (listenerStopping) { break; }
                    }
                }
            }));
            showThread.IsBackground = true;
            showThread.Start();
        }

        private void StartForegroundWakeTimer()
        {
            foregroundWakeTimer = new System.Windows.Forms.Timer();
            foregroundWakeTimer.Interval = 180;
            foregroundWakeTimer.Tick += delegate { CheckForegroundWake(); };
            foregroundWakeTimer.Start();
        }

        private void CheckForegroundWake()
        {
            if (foregroundRestoreBusy) { return; }
            int pid = GetForegroundPid();
            if (pid <= 0 || pid == lastForegroundPid) { return; }

            lastForegroundPid = pid;
            if ((DateTime.UtcNow - lastForegroundRestoreAt).TotalMilliseconds < 120) { return; }

            foregroundRestoreBusy = true;
            lastForegroundRestoreAt = DateTime.UtcNow;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    RunFastForegroundRestore(pid);
                }
                finally
                {
                    foregroundRestoreBusy = false;
                }
            });
        }

        private void ShowMainWindow()
        {
#if NET9_0_OR_GREATER
            dashboardHost.Show();
#else
            if (mainWindow == null)
            {
                mainWindow = CreateMainWindow();
            }
            mainWindow.RefreshStatus();
            if (!mainWindow.IsVisible)
            {
                mainWindow.Show();
            }
            if (mainWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                mainWindow.WindowState = System.Windows.WindowState.Normal;
            }
            mainWindow.Activate();
#endif
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
#if NET9_0_OR_GREATER
            dashboardHost.RefreshStatus();
#else
            mainWindow.RefreshStatus();
#endif
            ShowTrayMessage(result.ExitCode == 0 ? actionName + " finished." : actionName + " failed.");
            if (result.ExitCode != 0)
            {
                MessageBox.Show(result.Output, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private static RunResult RunFastForegroundRestore(int pid)
    {
        if (pid <= 0 || pid == Process.GetCurrentProcess().Id)
        {
            return new RunResult(0, "No foreground pid.");
        }

        try
        {
            Process process = Process.GetProcessById(pid);
            if (process.SessionId != Process.GetCurrentProcess().SessionId)
            {
                return new RunResult(0, "Other session.");
            }
            if (IsProtectedForegroundProcess(process.ProcessName))
            {
                return new RunResult(0, "Protected foreground.");
            }

            string priority = "Keep";
            try
            {
                ProcessPriorityClass current = process.PriorityClass;
                if (current == ProcessPriorityClass.Idle || current == ProcessPriorityClass.BelowNormal)
                {
                    process.PriorityClass = ProcessPriorityClass.Normal;
                    priority = "OK";
                }
            }
            catch (Exception ex)
            {
                priority = "Error:" + ex.GetType().Name;
            }

            string memory = TrySetMemoryPriority(pid, 5) ? "OK" : "Skip";
            string io = TrySetIoPriority(pid, 2) ? "OK" : "Skip";
            string power = TryClearPowerThrottling(pid) ? "OK" : "Skip";
            Directory.CreateDirectory(outputsPath);
            string line = String.Format(
                CultureInfo.InvariantCulture,
                "{0} action=foreground-restore mode=fast pid={1} process={2} priority={3} memory={4} io={5} power={6}",
                DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
                pid,
                process.ProcessName,
                priority,
                memory,
                io,
                power);
            File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
            return new RunResult(0, line);
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
            return new RunResult(1, ex.Message);
        }
    }

    private static bool IsProtectedForegroundProcess(string processName)
    {
        string[] protectedNames = new string[]
        {
            "ProcessLasso",
            "ProcessGovernor",
            "bitsumsessionagent",
            "ThrottleStop",
            "MSIAfterburner",
            "RTSS",
            "RTSSHooksLoader64",
            "RivaTunerStatisticsServer",
            "HWiNFO64",
            "HWiNFO32",
            "SmartBackgroundNap",
            "msedgewebview2"
        };
        for (int i = 0; i < protectedNames.Length; i++)
        {
            if (String.Equals(processName, protectedNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TrySetMemoryPriority(int pid, uint memoryPriority)
    {
        IntPtr handle = OpenProcess(ProcessSetInformation | ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) { return false; }
        IntPtr ptr = IntPtr.Zero;
        try
        {
            MemoryPriorityInformation info = new MemoryPriorityInformation();
            info.MemoryPriority = memoryPriority;
            ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MemoryPriorityInformation)));
            Marshal.StructureToPtr(info, ptr, false);
            return SetProcessInformation(handle, ProcessMemoryPriorityClass, ptr, (uint)Marshal.SizeOf(typeof(MemoryPriorityInformation)));
        }
        catch
        {
            return false;
        }
        finally
        {
            if (ptr != IntPtr.Zero) { Marshal.FreeHGlobal(ptr); }
            CloseHandle(handle);
        }
    }

    private static bool TrySetIoPriority(int pid, uint ioPriority)
    {
        IntPtr handle = OpenProcess(ProcessSetInformation | ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) { return false; }
        try
        {
            return NtSetInformationProcess(handle, ProcessIoPriorityClass, ref ioPriority, sizeof(uint)) == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static bool TryClearPowerThrottling(int pid)
    {
        IntPtr handle = OpenProcess(ProcessSetInformation | ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) { return false; }
        IntPtr ptr = IntPtr.Zero;
        try
        {
            ProcessPowerThrottlingState state = new ProcessPowerThrottlingState();
            state.Version = ProcessPowerThrottlingCurrentVersion;
            state.ControlMask = ProcessPowerThrottlingExecutionSpeed | ProcessPowerThrottlingIgnoreTimerResolution;
            state.StateMask = 0;
            ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ProcessPowerThrottlingState)));
            Marshal.StructureToPtr(state, ptr, false);
            return SetProcessInformation(handle, ProcessPowerThrottlingClass, ptr, (uint)Marshal.SizeOf(typeof(ProcessPowerThrottlingState)));
        }
        catch
        {
            return false;
        }
        finally
        {
            if (ptr != IntPtr.Zero) { Marshal.FreeHGlobal(ptr); }
            CloseHandle(handle);
        }
    }


#if NET9_0_OR_GREATER
    private interface IDashboardWindow
    {
        void RefreshStatus();
    }

    private sealed class WpfDashboardHost
    {
        private readonly Thread thread;
        private readonly ManualResetEventSlim ready = new ManualResetEventSlim(false);
        private readonly Action hiddenCallback;
        private System.Windows.Threading.Dispatcher dispatcher;
        private System.Windows.Window window;
        private IDashboardWindow dashboardWindow;
        private Exception startupException;
        private volatile bool allowClose;

        public WpfDashboardHost(Action hiddenCallback)
        {
            this.hiddenCallback = hiddenCallback;
            thread = new Thread(new ThreadStart(Run));
            thread.Name = "SmartBackgroundNap.WpfDashboard";
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public void Show()
        {
            if (!WaitUntilReady())
            {
                return;
            }

            dispatcher.BeginInvoke(new Action(delegate
            {
                EnsureWindow();
                dashboardWindow.RefreshStatus();
                if (!window.IsVisible)
                {
                    window.Show();
                }
                if (window.WindowState == System.Windows.WindowState.Minimized)
                {
                    window.WindowState = System.Windows.WindowState.Normal;
                }
                window.Activate();
            }));
        }

        public void RefreshStatus()
        {
            if (!WaitUntilReady())
            {
                return;
            }

            dispatcher.BeginInvoke(new Action(delegate
            {
                if (window == null)
                {
                    return;
                }
                dashboardWindow.RefreshStatus();
            }));
        }

        public void Shutdown()
        {
            allowClose = true;
            if (ready.IsSet && dispatcher != null)
            {
                dispatcher.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        if (window != null)
                        {
                            window.Close();
                        }
                    }
                    finally
                    {
                        dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);
                    }
                }));
            }

            try
            {
                if (thread.IsAlive)
                {
                    thread.Join(2000);
                }
            }
            catch
            {
            }
        }

        private bool WaitUntilReady()
        {
            ready.Wait(5000);
            if (startupException != null)
            {
                throw new InvalidOperationException("WPF dashboard could not start.", startupException);
            }
            return dispatcher != null;
        }

        private void Run()
        {
            try
            {
                System.Windows.Application app = new System.Windows.Application();
                app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                {
                    WriteCrash(e.Exception);
                    e.Handled = true;
                };
                dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                ready.Set();
                System.Windows.Threading.Dispatcher.Run();
            }
            catch (Exception ex)
            {
                startupException = ex;
                WriteCrash(ex);
                ready.Set();
            }
        }

        private void EnsureWindow()
        {
            if (window != null)
            {
                return;
            }

            try
            {
                dashboardWindow = new WebViewDashboardWindow(delegate(Exception ex)
                {
                    WriteCrash(ex);
                    dispatcher.BeginInvoke(new Action(delegate { ReplaceWithNativeDashboard(); }));
                });
                window = (System.Windows.Window)dashboardWindow;
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
                dashboardWindow = new ModernMainWindow();
                window = (System.Windows.Window)dashboardWindow;
            }
            AttachHideInsteadOfClose(window);
        }

        private void AttachHideInsteadOfClose(System.Windows.Window target)
        {
            target.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (!allowClose)
                {
                    dashboardWindow = null;
                    window = null;
                    NotifyHidden();
                }
            };
            target.StateChanged += delegate
            {
                if (!allowClose && target.WindowState == System.Windows.WindowState.Minimized)
                {
                    dispatcher.BeginInvoke(new Action(delegate { ReleaseDashboardWindow(target); }));
                }
            };
        }

        private void ReleaseDashboardWindow(System.Windows.Window target)
        {
            if (target == null || window != target)
            {
                return;
            }

            try
            {
                allowClose = true;
                target.Close();
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
            }
            finally
            {
                allowClose = false;
                dashboardWindow = null;
                window = null;
                NotifyHidden();
            }
        }

        private void ReplaceWithNativeDashboard()
        {
            try
            {
                bool shouldShow = window != null && window.IsVisible;
                allowClose = true;
                if (window != null)
                {
                    window.Close();
                }
                allowClose = false;

                dashboardWindow = new ModernMainWindow();
                window = (System.Windows.Window)dashboardWindow;
                AttachHideInsteadOfClose(window);
                if (shouldShow)
                {
                    window.Show();
                    window.Activate();
                }
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
            }
        }

        private void NotifyHidden()
        {
            if (hiddenCallback == null)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try { hiddenCallback(); } catch { }
            });
        }
    }

    private sealed class WebViewDashboardWindow : System.Windows.Window, IDashboardWindow
    {
        private readonly Action<Exception> fallbackRequested;
        private readonly WebView2 webView;
        private readonly System.Windows.Threading.DispatcherTimer refreshTimer;
        private readonly System.Windows.Threading.DispatcherTimer liveTimer;
        private readonly System.Windows.Threading.DispatcherTimer actionTimer;
        private RunControl activeRunControl;
        private bool webReady;
        private bool busy;
        private bool activeRunCanStop;
        private DateTime activeRunStartedAt;
        private string activeUiEventLine;
        private string activeTitle = "Control Center";
        private string activeDetail = "Waiting for the next pass.";
        private string runState = "READY";
        private const int WmNcHitTest = 0x0084;
        private const int HtClient = 1;
        private const int HtCaption = 2;
        private const int HtLeft = 10;
        private const int HtRight = 11;
        private const int HtTop = 12;
        private const int HtTopLeft = 13;
        private const int HtTopRight = 14;
        private const int HtBottom = 15;
        private const int HtBottomLeft = 16;
        private const int HtBottomRight = 17;
        private const double ResizeBorderSize = 18.0;
        private const double DragBandHeight = 54.0;
        private const double WindowButtonReserveWidth = 128.0;

        public WebViewDashboardWindow(Action<Exception> fallbackRequested)
        {
            this.fallbackRequested = fallbackRequested;
            Title = AppName;
            Width = 1440;
            Height = 780;
            MinWidth = 900;
            MinHeight = 560;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            WindowStyle = System.Windows.WindowStyle.SingleBorderWindow;
            ResizeMode = System.Windows.ResizeMode.CanResize;
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 9, 15));
            Icon = LoadWebViewWindowIcon(iconPath);
            ApplyResponsiveWindowBounds();
            SourceInitialized += delegate { InstallNativeWindowChrome(); };

            webView = new WebView2();
            Content = webView;

            Loaded += async delegate
            {
                await InitializeAsync();
            };
            StateChanged += delegate
            {
                if (WindowState == System.Windows.WindowState.Minimized)
                {
                    return;
                }
            };
            IsVisibleChanged += delegate
            {
                if (IsVisible)
                {
                    StartDashboardActivity();
                    RefreshStatus();
                }
                else
                {
                    StopDashboardActivity();
                }
            };

            refreshTimer = new System.Windows.Threading.DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(60);
            refreshTimer.Tick += delegate { if (!busy) { RefreshStatus(); } };

            liveTimer = new System.Windows.Threading.DispatcherTimer();
            liveTimer.Interval = TimeSpan.FromSeconds(1);
            liveTimer.Tick += delegate { SendState(); };

            actionTimer = new System.Windows.Threading.DispatcherTimer();
            actionTimer.Interval = TimeSpan.FromMilliseconds(250);
            actionTimer.Tick += delegate { UpdateActiveRunVisuals(); };
        }

        private void ApplyResponsiveWindowBounds()
        {
            try
            {
                System.Windows.Rect workArea = System.Windows.SystemParameters.WorkArea;
                double availableWidth = Math.Max(900.0, workArea.Width - 28.0);
                double availableHeight = Math.Max(560.0, workArea.Height - 72.0);
                MaxWidth = availableWidth;
                MaxHeight = availableHeight;
                Width = Math.Min(1440.0, availableWidth);
                Height = Math.Min(780.0, availableHeight);
                MinWidth = Math.Min(900.0, Width);
                MinHeight = Math.Min(560.0, Height);
            }
            catch
            {
            }
        }

        private void InstallNativeWindowChrome()
        {
            try
            {
                if (WindowStyle == System.Windows.WindowStyle.None)
                {
                    System.Windows.Interop.HwndSource source = System.Windows.PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
                    if (source != null)
                    {
                        source.AddHook(WndProc);
                    }
                }
                ClampWindowToWorkArea();
            }
            catch
            {
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WmNcHitTest)
            {
                return IntPtr.Zero;
            }

            System.Windows.Point point = PointFromScreen(new System.Windows.Point(GetSignedLowWord(lParam), GetSignedHighWord(lParam)));
            double width = ActualWidth > 0 ? ActualWidth : Width;
            double height = ActualHeight > 0 ? ActualHeight : Height;

            if (WindowState == System.Windows.WindowState.Normal && ResizeMode != System.Windows.ResizeMode.NoResize)
            {
                bool left = point.X >= 0 && point.X < ResizeBorderSize;
                bool right = point.X <= width && point.X >= width - ResizeBorderSize;
                bool top = point.Y >= 0 && point.Y < ResizeBorderSize;
                bool bottom = point.Y <= height && point.Y >= height - ResizeBorderSize;

                if (left && top) { handled = true; return new IntPtr(HtTopLeft); }
                if (right && top) { handled = true; return new IntPtr(HtTopRight); }
                if (left && bottom) { handled = true; return new IntPtr(HtBottomLeft); }
                if (right && bottom) { handled = true; return new IntPtr(HtBottomRight); }
                if (left) { handled = true; return new IntPtr(HtLeft); }
                if (right) { handled = true; return new IntPtr(HtRight); }
                if (top) { handled = true; return new IntPtr(HtTop); }
                if (bottom) { handled = true; return new IntPtr(HtBottom); }
            }

            if (point.Y >= 0 && point.Y < DragBandHeight && point.X >= 0 && point.X < width - WindowButtonReserveWidth)
            {
                handled = true;
                return new IntPtr(HtCaption);
            }

            handled = true;
            return new IntPtr(HtClient);
        }

        private static int GetSignedLowWord(IntPtr value)
        {
            return (short)((long)value & 0xffff);
        }

        private static int GetSignedHighWord(IntPtr value)
        {
            return (short)(((long)value >> 16) & 0xffff);
        }

        private void ClampWindowToWorkArea()
        {
            try
            {
                System.Windows.Rect workArea = System.Windows.SystemParameters.WorkArea;
                if (Width > workArea.Width) { Width = Math.Max(MinWidth, workArea.Width - 20.0); }
                if (Height > workArea.Height) { Height = Math.Max(MinHeight, workArea.Height - 20.0); }
                if (Left < workArea.Left + 8.0) { Left = workArea.Left + 8.0; }
                if (Top < workArea.Top + 8.0) { Top = workArea.Top + 8.0; }
                if (Left + Width > workArea.Right - 8.0) { Left = Math.Max(workArea.Left + 8.0, workArea.Right - Width - 8.0); }
                if (Top + Height > workArea.Bottom - 8.0) { Top = Math.Max(workArea.Top + 8.0, workArea.Bottom - Height - 8.0); }
            }
            catch
            {
            }
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            try
            {
                string dataFolder = Path.Combine(appRoot, "WebView2");
                Directory.CreateDirectory(dataFolder);
                CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions(
                    "--disable-features=msWebOOUI,msPdfOOUI --disable-background-networking");
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, dataFolder, options);
                await webView.EnsureCoreWebView2Async(environment);
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                webView.CoreWebView2.NavigationCompleted += delegate
                {
                    webReady = true;
                    SendState();
                };
                webView.NavigateToString(BuildHtml());
            }
            catch (Exception ex)
            {
                if (fallbackRequested != null)
                {
                    fallbackRequested(ex);
                }
                else
                {
                    throw;
                }
            }
        }

        private static System.Windows.Media.ImageSource LoadWebViewWindowIcon(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                System.Windows.Media.Imaging.BitmapImage image = new System.Windows.Media.Imaging.BitmapImage();
                image.BeginInit();
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        public void RefreshStatus()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { RefreshStatus(); }));
                return;
            }
            SendState();
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                IDictionary<string, object> message = JsonCompat.DeserializeObject(e.WebMessageAsJson);
                string action = GetString(message, "action");
                if (String.Equals(action, "ready", StringComparison.OrdinalIgnoreCase))
                {
                    webReady = true;
                    SendState();
                    return;
                }
                if (String.Equals(action, "setLanguage", StringComparison.OrdinalIgnoreCase))
                {
                    SaveUiLanguage(GetString(message, "language"));
                    SendState();
                    return;
                }
                if (String.Equals(action, "drag", StringComparison.OrdinalIgnoreCase))
                {
                    try { DragMove(); } catch { }
                    return;
                }
                if (String.Equals(action, "minimize", StringComparison.OrdinalIgnoreCase))
                {
                    WindowState = System.Windows.WindowState.Minimized;
                    return;
                }
                if (String.Equals(action, "close", StringComparison.OrdinalIgnoreCase))
                {
                    Close();
                    return;
                }
                if (String.Equals(action, "apply", StringComparison.OrdinalIgnoreCase))
                {
                    if (busy && activeRunCanStop) { StopCurrentActionWithFeedback(); } else { RunOptimizeNowActionWithFeedback(); }
                    return;
                }
                if (String.Equals(action, "toggleMotor", StringComparison.OrdinalIgnoreCase))
                {
                    ToggleMotorFromButton();
                    return;
                }
                if (String.Equals(action, "toggleStartup", StringComparison.OrdinalIgnoreCase))
                {
                    bool startupInstalled = IsTaskInstalled(TrayTaskName);
                    RunUserAction(
                        startupInstalled ? "Disabling tray startup..." : "Enabling tray startup...",
                        startupInstalled ? "Tray startup is off." : "The tray will start with Windows.",
                        startupInstalled ? (Func<RunResult>)UninstallStartup : InstallStartup);
                    return;
                }
                if (String.Equals(action, "toggleLearning", StringComparison.OrdinalIgnoreCase))
                {
                    bool learningEnabled = IsSmartLearningEnabled();
                    RunUserAction(
                        learningEnabled ? "Disabling Smart Learning..." : "Enabling Smart Learning...",
                        learningEnabled ? "Smart Learning is off." : "Smart Learning is active.",
                        delegate { return SetSmartLearningEnabled(!learningEnabled); });
                    return;
                }
                if (String.Equals(action, "runElevatedApply", StringComparison.OrdinalIgnoreCase))
                {
                    RunUserAction("Requesting administrator permission...", "Elevated pass finished.", RunElevatedApply);
                    return;
                }
                if (String.Equals(action, "restore", StringComparison.OrdinalIgnoreCase)) { RunUserAction("Restoring latest snapshot...", "Restore finished.", RunRestore); return; }
                if (String.Equals(action, "score", StringComparison.OrdinalIgnoreCase)) { OpenScore(); return; }
                if (String.Equals(action, "log", StringComparison.OrdinalIgnoreCase)) { OpenLog(); return; }
                if (String.Equals(action, "folder", StringComparison.OrdinalIgnoreCase)) { OpenFolder(); return; }
                if (String.Equals(action, "config", StringComparison.OrdinalIgnoreCase)) { OpenConfig(); return; }
                if (String.Equals(action, "safety", StringComparison.OrdinalIgnoreCase)) { OpenSafetyReport(); return; }
                if (String.Equals(action, "github", StringComparison.OrdinalIgnoreCase)) { OpenGitHub(); return; }
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
            }
        }

        private void RunUserAction(string activeMessage, string successMessage, Func<RunResult> action)
        {
            if (busy) { return; }
            busy = true;
            activeRunCanStop = false;
            activeTitle = activeMessage;
            activeDetail = "Working in the background...";
            runState = "WORKING";
            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  NOW  " + CleanEventText(activeMessage);
            SendState();

            ThreadPool.QueueUserWorkItem(delegate
            {
                RunResult result = action();
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    busy = false;
                    activeTitle = result.ExitCode == 0 ? successMessage : "Action failed";
                    activeDetail = result.ExitCode == 0 ? BuildResultText() : ShortError(result.Output);
                    runState = result.ExitCode == 0 ? "DONE" : "ERROR";
                    activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + (result.ExitCode == 0 ? "  OK   " + CleanEventText(successMessage) : "  FAIL " + CleanEventText(ShortError(result.Output)));
                    SendState();
                    if (result.ExitCode != 0)
                    {
                        System.Windows.MessageBox.Show(ShortError(result.Output), AppName, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }));
            });
        }

        private void ToggleMotorFromButton()
        {
            if (busy) { return; }

            bool installed = IsTaskInstalled(AutoTaskName);
            RunUserAction(
                installed ? "Pausing background motor..." : "Starting background motor...",
                installed ? "Background motor paused." : "Background motor active.",
                installed ? (Func<RunResult>)UninstallAutomatic : InstallAutomatic);
        }

        private void RunOptimizeNowActionWithFeedback()
        {
            if (busy) { return; }

            RunControl control = new RunControl();
            activeRunControl = control;
            activeRunStartedAt = DateTime.Now;
            busy = true;
            activeRunCanStop = true;
            activeTitle = "Agindo nos apps agora";
            activeDetail = "Em execucao ha 0s: prioridade, IO, memoria e EcoQoS.";
            runState = "RUNNING";
            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  NOW  passe manual iniciado: prioridade, IO, memoria e EcoQoS";
            if (actionTimer != null) { actionTimer.Start(); }
            SendState();

            ThreadPool.QueueUserWorkItem(delegate
            {
                RunResult result = RunApplyNow(control);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    bool stopped = result.ExitCode == 130;
                    if (actionTimer != null) { actionTimer.Stop(); }
                    activeRunControl = null;
                    busy = false;
                    activeRunCanStop = false;
                    activeTitle = stopped ? "Otimizacao parada" : (result.ExitCode == 0 ? "Otimizacao concluida" : "Action failed");
                    activeDetail = stopped ? "O passe manual foi interrompido." : (result.ExitCode == 0 ? BuildResultText() : ShortError(result.Output));
                    runState = stopped ? "STOPPED" : (result.ExitCode == 0 ? "DONE" : "ERROR");
                    activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + (stopped ? "  STOP passe manual interrompido" : (result.ExitCode == 0 ? "  OK   passe manual aplicado: " + BuildResultText() : "  FAIL passe manual falhou"));
                    SendState();
                    if (result.ExitCode != 0 && !stopped)
                    {
                        System.Windows.MessageBox.Show(ShortError(result.Output), AppName, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }));
            });
        }

        private void StopCurrentActionWithFeedback()
        {
            if (!busy || activeRunControl == null)
            {
                return;
            }

            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  STOP solicitado pelo usuario";
            activeTitle = "Parando otimizacao...";
            activeDetail = "Encerrando o passe manual com seguranca.";
            runState = "STOPPING";
            activeRunControl.Cancel();
            SendState();
        }

        private void UpdateActiveRunVisuals()
        {
            if (!busy || activeRunControl == null)
            {
                return;
            }
            int seconds = Math.Max(0, (int)Math.Round((DateTime.Now - activeRunStartedAt).TotalSeconds));
            if (activeRunControl.CancelRequested)
            {
                activeTitle = "Parando otimizacao...";
                activeDetail = "Parada solicitada ha " + seconds.ToString(CultureInfo.CurrentCulture) + "s.";
                runState = "STOPPING";
            }
            else
            {
                activeTitle = "Agindo nos apps agora";
                activeDetail = "Em execucao ha " + seconds.ToString(CultureInfo.CurrentCulture) + "s: prioridade, IO, memoria e EcoQoS.";
                runState = "RUNNING";
                activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  NOW  passe manual em execucao (" + seconds.ToString(CultureInfo.CurrentCulture) + "s)";
            }
            SendState();
        }

        private void StartDashboardActivity()
        {
            if (refreshTimer != null && !refreshTimer.IsEnabled) { refreshTimer.Start(); }
            if (liveTimer != null && !liveTimer.IsEnabled) { liveTimer.Start(); }
        }

        private void StopDashboardActivity()
        {
            if (refreshTimer != null) { refreshTimer.Stop(); }
            if (liveTimer != null) { liveTimer.Stop(); }
        }

        private void SendState()
        {
            if (!webReady || webView.CoreWebView2 == null)
            {
                return;
            }

            try
            {
                string json = JsonSerializer.Serialize(BuildState());
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
            }
        }

        private WebDashboardState BuildState()
        {
            bool autoInstalled = IsTaskInstalled(AutoTaskName);
            bool startupInstalled = IsTaskInstalled(TrayTaskName);
            bool learningEnabled = IsSmartLearningEnabled();
            List<WebManagerRow> rows = LoadManagerRows();
            ScoreMeta scoreMeta = LoadScoreMeta();
            string line = ReadLastApplyLogLine();
            string targets = line == "No log yet." ? "" : ExtractLogValue(line, "targets");
            string delta = line == "No log yet." ? "" : ExtractLogValue(line, "deltaMB");
            string top = line == "No log yet." ? "" : ExtractLogValue(line, "top");
            string heartbeat = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            string lastEventAge = BuildLastEventAgeText();
            string nextPass = BuildNextPassText(line, autoInstalled);

            WebDashboardState state = new WebDashboardState();
            state.AppVersion = AppVersion;
            state.Creator = CreatorLine;
            state.Language = String.IsNullOrWhiteSpace(uiLanguage) ? "" : uiLanguage;
            state.FirstRun = String.IsNullOrWhiteSpace(uiLanguage);
            state.AutoMode = autoInstalled;
            state.Startup = startupInstalled;
            state.Learning = learningEnabled;
            state.LearningProfiles = learningEnabled ? Math.Max(scoreMeta.LearningProfiles, GetLearningProfileCount()) : 0;
            state.MemoryPressure = String.IsNullOrWhiteSpace(scoreMeta.MemoryPressure) ? ExtractLogValue(line, "pressure") : scoreMeta.MemoryPressure;
            state.FreeMemoryMB = scoreMeta.FreeMemoryMB;
            state.IsElevated = IsCurrentProcessElevated();
            state.PermissionDeniedCount = scoreMeta.PermissionDeniedCount;
            state.PermissionDeniedApps = scoreMeta.PermissionDeniedApps;
            state.Busy = busy;
            state.CanStop = activeRunCanStop;
            state.RunState = busy ? runState : (autoInstalled ? "MOTOR ACTIVE" : "MANUAL");
            state.Title = busy ? activeTitle : (autoInstalled ? "Nap Engine" : "Manual Engine");
            state.Detail = busy ? activeDetail : BuildStatusDetail(autoInstalled, startupInstalled);
            state.LastRun = GetLastEventCardText();
            state.Result = BuildResultText();
            state.Managed = String.IsNullOrWhiteSpace(targets) ? rows.Count.ToString(CultureInfo.CurrentCulture) : targets;
            state.Reclaimed = String.IsNullOrWhiteSpace(delta) ? "0" : delta;
            state.TopApp = String.IsNullOrWhiteSpace(top) ? (rows.Count > 0 ? rows[0].Name : "-") : top;
            state.Wake = autoInstalled ? "Fast wake" : "Manual";
            state.Heartbeat = heartbeat;
            state.LastEventAge = lastEventAge;
            state.NextPass = nextPass;
            state.Rows = rows;
            state.Events = BuildEvents(autoInstalled, heartbeat, lastEventAge, nextPass);
            state.Logo = GetLogoDataUri();
            return state;
        }

        private ScoreMeta LoadScoreMeta()
        {
            ScoreMeta meta = new ScoreMeta();
            meta.PermissionDeniedApps = new List<string>();
            try
            {
                if (!File.Exists(scorePath)) { return meta; }
                string json = File.ReadAllText(scorePath, Encoding.UTF8);
                if (String.IsNullOrWhiteSpace(json)) { return meta; }
                IDictionary<string, object> root = JsonCompat.DeserializeObject(json);
                if (root == null) { return meta; }
                meta.LearningEnabled = GetBool(root, "LearningEnabled");
                meta.LearningProfiles = GetInt(root, "LearningProfiles");
                meta.MemoryPressure = GetString(root, "MemoryPressure");
                meta.FreeMemoryMB = GetDouble(root, "FreeMemoryMB");
                object items;
                if (root.TryGetValue("Items", out items) && items != null)
                {
                    System.Collections.IEnumerable enumerable = items as System.Collections.IEnumerable;
                    if (enumerable != null && !(items is string))
                    {
                        HashSet<string> denied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (object item in enumerable)
                        {
                            IDictionary<string, object> map = item as IDictionary<string, object>;
                            if (map == null || !HasPermissionDeniedStatus(map)) { continue; }
                            string label = BuildProcessLabel(map);
                            if (!String.IsNullOrWhiteSpace(label)) { denied.Add(label); }
                        }
                        meta.PermissionDeniedCount = denied.Count;
                        meta.PermissionDeniedApps = new List<string>(denied);
                        meta.PermissionDeniedApps.Sort(StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
            }
            return meta;
        }

        private static bool HasPermissionDeniedStatus(IDictionary<string, object> map)
        {
            return IsPermissionDeniedStatus(GetString(map, "Priority")) ||
                IsPermissionDeniedStatus(GetString(map, "MemoryPriority")) ||
                IsPermissionDeniedStatus(GetString(map, "IoPriority")) ||
                IsPermissionDeniedStatus(GetString(map, "PowerThrottling")) ||
                IsPermissionDeniedStatus(GetString(map, "TrimWorkingSet"));
        }

        private static bool IsPermissionDeniedStatus(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) { return false; }
            return value.IndexOf("Access denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Acesso negado", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("Win32Error=5", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("0xC0000022", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string BuildStatusDetail(bool autoInstalled, bool startupInstalled)
        {
            string line = ReadLastLogLine();
            if (line == "No log yet.")
            {
                return autoInstalled ? "Foreground wake is armed. Background apps are tuned by tier." : "Paused. Run a manual pass or resume the engine.";
            }
            return BuildResultText() + (startupInstalled ? " | wake guard active" : " | startup off");
        }

        private string GetLastEventCardText()
        {
            try
            {
                if (!File.Exists(logPath)) { return "-"; }
                return File.GetLastWriteTime(logPath).ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            }
            catch
            {
                return "-";
            }
        }

        private string BuildResultText()
        {
            string line = ReadLastApplyLogLine();
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
            return line.Length > 32 ? line.Substring(0, 32) + "..." : line;
        }

        private List<string> BuildEvents(bool autoInstalled, string heartbeat, string lastEventAge, string nextPass)
        {
            List<string> events = new List<string>();
            if (!String.IsNullOrWhiteSpace(activeUiEventLine))
            {
                events.Add(activeUiEventLine);
            }
            events.Add("LIVE " + heartbeat + "  event " + lastEventAge + "  next " + nextPass);
            if (autoInstalled)
            {
                events.Add("WATCH motor automatico ativo; ciclos e foco protegidos");
            }
            List<string> lines = ReadLastLines(logPath, 10);
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                events.Add(FormatActivityLine(lines[i]));
            }
            return events;
        }

        private string BuildLastEventAgeText()
        {
            try
            {
                if (!File.Exists(logPath)) { return "no event"; }
                TimeSpan age = DateTime.Now - File.GetLastWriteTime(logPath);
                if (age.TotalSeconds < 0) { age = TimeSpan.Zero; }
                return FormatCompactAge(age);
            }
            catch
            {
                return "unknown";
            }
        }

        private string BuildNextPassText(string lastApplyLine, bool autoInstalled)
        {
            if (!autoInstalled) { return "paused"; }
            int intervalMinutes = GetAutomationIntervalMinutes();
            DateTime? lastApply = TryReadLogTimestamp(lastApplyLine);
            if (!lastApply.HasValue) { return "waiting"; }
            TimeSpan remaining = lastApply.Value.AddMinutes(intervalMinutes) - DateTime.Now;
            if (remaining.TotalSeconds <= 0) { return "due now"; }
            return FormatCompactCountdown(remaining);
        }

        private int GetAutomationIntervalMinutes()
        {
            const int fallbackIntervalMinutes = 5;
            try
            {
                if (!File.Exists(configPath)) { return fallbackIntervalMinutes; }
                IDictionary<string, object> root = JsonCompat.DeserializeObject(File.ReadAllText(configPath, Encoding.UTF8));
                object automationObject;
                if (root == null || !root.TryGetValue("Automation", out automationObject)) { return fallbackIntervalMinutes; }
                IDictionary<string, object> automation = automationObject as IDictionary<string, object>;
                int interval = GetInt(automation, "IntervalMinutes");
                return interval >= 1 ? interval : fallbackIntervalMinutes;
            }
            catch
            {
                return fallbackIntervalMinutes;
            }
        }

        private static DateTime? TryReadLogTimestamp(string line)
        {
            if (String.IsNullOrWhiteSpace(line) || line.Length < 19) { return null; }
            DateTime parsed;
            if (DateTime.TryParseExact(line.Substring(0, 19), "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed;
            }
            return null;
        }

        private static string FormatCompactCountdown(TimeSpan span)
        {
            if (span.TotalHours >= 1)
            {
                return ((int)span.TotalHours).ToString(CultureInfo.CurrentCulture) + "h " + span.Minutes.ToString("00", CultureInfo.CurrentCulture) + "m";
            }
            return span.Minutes.ToString("00", CultureInfo.CurrentCulture) + ":" + span.Seconds.ToString("00", CultureInfo.CurrentCulture);
        }

        private static string FormatCompactAge(TimeSpan age)
        {
            if (age.TotalSeconds < 2) { return "now"; }
            if (age.TotalMinutes < 1) { return ((int)age.TotalSeconds).ToString(CultureInfo.CurrentCulture) + "s ago"; }
            if (age.TotalHours < 1) { return ((int)age.TotalMinutes).ToString(CultureInfo.CurrentCulture) + "m ago"; }
            return ((int)age.TotalHours).ToString(CultureInfo.CurrentCulture) + "h ago";
        }

        private List<WebManagerRow> LoadManagerRows()
        {
            List<WebManagerRow> rows = new List<WebManagerRow>();
            try
            {
                if (!File.Exists(scorePath)) { return rows; }
                string json = File.ReadAllText(scorePath, Encoding.UTF8);
                if (String.IsNullOrWhiteSpace(json)) { return rows; }
                IDictionary<string, object> root = JsonCompat.DeserializeObject(json);
                if (root == null) { return rows; }
                object items;
                if (!root.TryGetValue("Items", out items) || items == null) { return rows; }
                System.Collections.IEnumerable enumerable = items as System.Collections.IEnumerable;
                if (enumerable == null || items is string) { return rows; }
                foreach (object item in enumerable)
                {
                    IDictionary<string, object> map = item as IDictionary<string, object>;
                    if (map == null) { continue; }
                    WebManagerRow row = new WebManagerRow();
                    row.Name = BuildProcessLabel(map);
                    row.Score = FormatDecimal(GetDouble(map, "Score"));
                    row.Delta = FormatDecimal(GetDouble(map, "DeltaMB")) + " MB";
                    row.Cpu = FormatDecimal(GetDouble(map, "CpuPercent"));
                    row.Bursts = GetInt(map, "BurstCount").ToString(CultureInfo.CurrentCulture);
                    row.Action = BuildActionSummary(map);
                    row.PermissionDenied = HasPermissionDeniedStatus(map);
                    row.RawScore = GetDouble(map, "Score");
                    rows.Add(row);
                }
                rows.Sort(delegate(WebManagerRow left, WebManagerRow right) { return right.RawScore.CompareTo(left.RawScore); });
                if (rows.Count > 12)
                {
                    rows.RemoveRange(12, rows.Count - 12);
                }
            }
            catch
            {
            }
            return rows;
        }

        private string BuildProcessLabel(IDictionary<string, object> map)
        {
            string name = GetString(map, "ProcessName");
            if (String.IsNullOrWhiteSpace(name)) { name = "Unknown"; }
            int id = GetInt(map, "Id");
            return id > 0 ? name + " (" + id.ToString(CultureInfo.CurrentCulture) + ")" : name;
        }

        private string BuildActionSummary(IDictionary<string, object> map)
        {
            string tier = BlankToDash(GetString(map, "NapTier"));
            string priority = BlankToDash(GetString(map, "Priority"));
            string memory = BlankToDash(GetString(map, "MemoryPriority"));
            string io = BlankToDash(GetString(map, "IoPriority"));
            string trim = BlankToDash(GetString(map, "TrimWorkingSet"));
            string power = BlankToDash(GetString(map, "PowerThrottling"));
            string learning = GetString(map, "Learning");
            int observations = GetInt(map, "LearningObservations");
            int wakes = GetInt(map, "LearningWakeCount");
            string summary = "Tier " + tier + " / P " + priority + " / M " + memory + " / IO " + io + " / T " + trim + " / Eco " + power;
            if (!String.IsNullOrWhiteSpace(learning) || observations > 0 || wakes > 0)
            {
                summary += " / Learn " + (String.IsNullOrWhiteSpace(learning) ? observations.ToString(CultureInfo.CurrentCulture) : learning);
                if (wakes > 0) { summary += " / Wake " + wakes.ToString(CultureInfo.CurrentCulture); }
            }
            if (HasPermissionDeniedStatus(map))
            {
                summary += " / Admin needed";
            }
            return summary;
        }

        private List<string> ReadLastLines(string path, int maxLines)
        {
            List<string> result = new List<string>();
            try
            {
                if (!File.Exists(path)) { return result; }
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int start = Math.Max(0, lines.Length - maxLines);
                for (int i = start; i < lines.Length; i++)
                {
                    if (!String.IsNullOrWhiteSpace(lines[i])) { result.Add(lines[i]); }
                }
            }
            catch
            {
            }
            return result;
        }

        private string FormatActivityLine(string line)
        {
            string action = ExtractLogValue(line, "action");
            string time = FormatActivityTime(line);
            if (String.Equals(action, "apply", StringComparison.OrdinalIgnoreCase))
            {
                string targets = ExtractLogValue(line, "targets");
                string delta = ExtractLogValue(line, "deltaMB");
                string light = ExtractLogValue(line, "light");
                string balanced = ExtractLogValue(line, "balanced");
                string deep = ExtractLogValue(line, "deep");
                string trimmed = ExtractLogValue(line, "trimmed");
                string cooldown = ExtractLogValue(line, "cooldown");
                string top = ExtractLogValue(line, "top");
                string learning = ExtractLogValue(line, "learning");
                string pressure = ExtractLogValue(line, "pressure");
                string profiles = ExtractLogValue(line, "profiles");
                string text = time + "  APPLY";
                if (!String.IsNullOrWhiteSpace(targets)) { text += "  " + targets + " apps"; }
                if (!String.IsNullOrWhiteSpace(delta)) { text += "  " + delta + " MB"; }
                if (!String.IsNullOrWhiteSpace(light) || !String.IsNullOrWhiteSpace(balanced) || !String.IsNullOrWhiteSpace(deep)) { text += "  L/B/D " + BlankToZero(light) + "/" + BlankToZero(balanced) + "/" + BlankToZero(deep); }
                if (!String.IsNullOrWhiteSpace(trimmed)) { text += "  T " + trimmed; }
                if (!String.IsNullOrWhiteSpace(cooldown) && cooldown != "0") { text += "  C " + cooldown; }
                if (String.Equals(learning, "on", StringComparison.OrdinalIgnoreCase)) { text += "  LEARN " + BlankToZero(profiles) + " " + BlankToDash(pressure); }
                if (!String.IsNullOrWhiteSpace(top)) { text += "  top " + top; }
                return text;
            }
            if (String.Equals(action, "learning", StringComparison.OrdinalIgnoreCase))
            {
                string enabled = ExtractLogValue(line, "enabled");
                string process = ExtractLogValue(line, "process");
                string wakes = ExtractLogValue(line, "wakes");
                string text = time + "  LEARN";
                if (!String.IsNullOrWhiteSpace(enabled)) { text += "  " + (String.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase) ? "enabled" : "disabled"); }
                if (!String.IsNullOrWhiteSpace(process)) { text += "  " + process; }
                if (!String.IsNullOrWhiteSpace(wakes)) { text += "  wakes " + wakes; }
                return text;
            }
            if (String.Equals(action, "elevated-apply", StringComparison.OrdinalIgnoreCase))
            {
                string status = ExtractLogValue(line, "status");
                string exitCode = ExtractLogValue(line, "exitCode");
                string text = time + "  ADMIN";
                if (!String.IsNullOrWhiteSpace(status)) { text += "  " + status; }
                if (!String.IsNullOrWhiteSpace(exitCode)) { text += "  exit " + exitCode; }
                return text;
            }
            if (String.Equals(action, "foreground-restore", StringComparison.OrdinalIgnoreCase))
            {
                string process = ExtractLogValue(line, "process");
                string pid = ExtractLogValue(line, "pid");
                string text = time + "  WAKE";
                if (!String.IsNullOrWhiteSpace(process)) { text += "  " + process; }
                if (!String.IsNullOrWhiteSpace(pid)) { text += " #" + pid; }
                return text;
            }
            return line.Length > 120 ? line.Substring(0, 120) + "..." : line;
        }

        private string FormatActivityTime(string line)
        {
            if (String.IsNullOrWhiteSpace(line)) { return "--:--:--"; }
            int end = line.IndexOf(' ');
            string raw = end > 0 ? line.Substring(0, end) : line;
            DateTime parsed;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            }
            return raw.Length > 8 ? raw.Substring(raw.Length - 8) : raw;
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

        private string CleanEventText(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) { return "action"; }
            text = text.Replace(Environment.NewLine, " ").Replace("\r", " ").Replace("\n", " ").Trim();
            while (text.EndsWith(".", StringComparison.Ordinal)) { text = text.TrimEnd('.'); }
            return text.Length > 120 ? text.Substring(0, 120) + "..." : text;
        }

        private string ShortError(string output)
        {
            if (String.IsNullOrWhiteSpace(output)) { return "No details were returned."; }
            output = output.Trim();
            return output.Length > 650 ? output.Substring(0, 650) + Environment.NewLine + "..." : output;
        }

        private string GetLogoDataUri()
        {
            try
            {
                if (File.Exists(logoPath))
                {
                    string ext = Path.GetExtension(logoPath);
                    string mime = String.Equals(ext, ".ico", StringComparison.OrdinalIgnoreCase) ? "image/x-icon" : "image/png";
                    return "data:" + mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(logoPath));
                }
            }
            catch
            {
            }
            return "";
        }

        private static string GetString(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) { return ""; }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) { return 0; }
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch
            {
                int parsed;
                return Int32.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
            }
        }

        private static double GetDouble(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) { return 0; }
            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch
            {
                double parsed;
                return Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
            }
        }

        private static bool GetBool(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) { return false; }
            try { return Convert.ToBoolean(value, CultureInfo.InvariantCulture); }
            catch
            {
                bool parsed;
                return Boolean.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) && parsed;
            }
        }

        private static string BlankToDash(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string BlankToZero(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? "0" : value;
        }

        private static string FormatDecimal(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value)) { return "0.0"; }
            return value.ToString("0.0", CultureInfo.CurrentCulture);
        }

        private string LoadDashboardHtml()
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + "dashboard_html"))
                {
                    if (stream == null) { return ""; }
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        private string BuildHtml()
        {
            string dashboardHtml = LoadDashboardHtml();
            if (!String.IsNullOrWhiteSpace(dashboardHtml))
            {
                return dashboardHtml;
            }

            return @"<!doctype html>
<html>
<head>
<meta charset='utf-8'>
<meta http-equiv='Content-Security-Policy' content=""default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline';"">
<style>
:root{color-scheme:dark;--bg:#05090f;--rail:#08101c;--panel:#0d1726;--panel2:#101d30;--line:#263851;--text:#f3f7fb;--soft:#93a5bd;--muted:#607086;--amber:#ffa12b;--green:#28d082;--blue:#4091ff;--red:#eb464e}
*{box-sizing:border-box} body{margin:0;background:#05090f;color:var(--text);font-family:'Segoe UI',system-ui,sans-serif;overflow:hidden} button{font:inherit}
.shell{height:100vh;min-height:0;border:1px solid var(--amber);display:grid;grid-template-rows:minmax(0,1fr);background:linear-gradient(135deg,#05090f,#07101d 55%,#080b12)}
.chrome{display:none}.brand{display:flex;align-items:center;gap:10px;padding-left:20px}.brand img{width:31px;height:31px;object-fit:contain}.brand b{font-size:14px}.brand span{display:block;font-size:10px;color:#6c7e96;font-weight:700;margin-top:1px}.win{display:flex;align-items:center;gap:6px;padding-right:15px}.win button{width:34px;height:28px;border:1px solid transparent;background:#04080e;color:#9fb0c8;border-radius:5px;cursor:pointer}.win button:hover{border-color:#31445f;background:#101b2a;color:#fff}
.body{display:grid;grid-template-columns:86px 1fr;min-height:0}.rail{background:var(--rail);padding-top:18px;display:flex;flex-direction:column;align-items:center;gap:12px;min-height:0}.nav{width:52px;height:48px;border-radius:8px;border:1px solid #17283f;background:#0a1422;color:#9eb0c8;display:grid;place-items:center;cursor:pointer}.nav.active{border-color:#6a4b1b;background:#211b13;color:var(--amber)}.nav:hover{border-color:#3b5679;color:#fff}.nav svg{width:21px;height:21px}.ver{margin-top:auto;margin-bottom:20px;color:#64768e;font-size:10px;font-weight:700}
.main{padding:12px 22px 8px 22px;display:grid;grid-template-rows:54px 220px 88px minmax(0,1fr);gap:0;min-height:0}.top{display:grid;grid-template-columns:1fr auto;align-items:center}.title h1{margin:0;font-size:24px}.title p{margin:4px 0 0;color:var(--soft);font-size:13px}.pills{display:flex;align-items:center;gap:10px}.pill{border:1px solid #263a55;background:#101c2e;color:#aebdd0;border-radius:999px;padding:7px 11px;font-size:12px;font-weight:700}.pill.good{border-color:#1d674b;background:#113928;color:var(--green)}.pill.warn{border-color:#714323;background:#2c1d12;color:var(--amber)}
.hero{position:relative;overflow:hidden;display:grid;grid-template-columns:1fr 420px;gap:18px;border-radius:8px;border:1px solid #253852;background:linear-gradient(135deg,#0d1726,#08111e 58%,#0b0e14);padding:18px 24px}.hero:before{content:'';position:absolute;inset:0;background:linear-gradient(115deg,rgba(255,161,43,.13),transparent 38%),linear-gradient(290deg,rgba(64,145,255,.14),transparent 45%);pointer-events:none}.hero>*{position:relative}.hero h2{margin:0;font-size:31px;line-height:1.06}.hero p{margin:8px 0 16px;color:var(--soft);font-size:14px}.chips{display:flex;gap:8px}.chip{border-radius:6px;background:#1c2a40;color:#dbe6f5;font-weight:800;font-size:11px;padding:7px 11px}.chip:nth-child(2){color:var(--amber)}.chip:nth-child(3){color:var(--green)}.chip:nth-child(4){color:#b28cff}
.control{border:1px solid #324864;background:linear-gradient(160deg,rgba(12,21,35,.96),rgba(8,16,28,.94));border-radius:8px;padding:16px;box-shadow:0 18px 38px rgba(0,0,0,.26);display:grid;grid-template-rows:auto auto 1fr auto;gap:10px}.engineHead{display:flex;align-items:flex-start;justify-content:space-between;gap:14px}.control h3{font-size:22px;margin:0}.state{display:inline-flex;border-radius:999px;background:#123a2a;color:var(--green);font-weight:900;font-size:11px;padding:6px 10px;white-space:nowrap}.detail{color:var(--soft);font-size:12px;line-height:1.35;overflow:hidden}.engineStats{display:grid;grid-template-columns:repeat(4,1fr);gap:8px}.engineStats div{border:1px solid #233650;background:#0b1728;border-radius:7px;padding:8px 9px;min-width:0}.engineStats small{display:block;color:#71839c;font-size:10px;font-weight:800;text-transform:uppercase}.engineStats b{display:block;margin-top:3px;font-size:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.bar{height:5px;border:1px solid #4a5e78;background:#17263a;overflow:hidden}.bar i{display:block;width:0;height:100%;background:var(--amber)}.busy .bar i{width:100%;animation:run 1.2s linear infinite;background:linear-gradient(90deg,var(--amber),var(--green),var(--blue))}@keyframes run{from{transform:translateX(-100%)}to{transform:translateX(100%)}}
.actions{display:grid;grid-template-columns:1.2fr 1fr .72fr;gap:8px}.btn{height:38px;border-radius:6px;border:1px solid #31445f;background:#142238;color:#f4f7fb;font-weight:800;cursor:pointer}.btn.primary{background:var(--amber);border-color:var(--amber);color:#151515}.btn.danger{background:var(--red);border-color:var(--red);color:#fff}.btn:hover{filter:brightness(1.08)}
.cards{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin-top:12px}.card{border:1px solid #263851;border-radius:8px;background:linear-gradient(135deg,#101d30,#0b1422);padding:9px 16px;position:relative;overflow:hidden}.card:before{content:'';position:absolute;left:0;top:0;width:100%;height:3px;background:var(--blue)}.card:nth-child(2):before{background:var(--green)}.card:nth-child(3):before{background:var(--amber)}.card:nth-child(4):before{background:var(--blue)}.card small{display:block;color:var(--soft);font-size:12px}.card b{display:block;margin-top:6px;font-size:19px}
.live{display:grid;grid-template-columns:2.2fr 1fr;gap:14px;margin-top:12px;min-height:0}.panel{border:1px solid #263851;border-radius:8px;background:linear-gradient(135deg,#0f1b2c,#0a1320);padding:14px;min-height:0;overflow:hidden}.panel h3{margin:0 0 10px;font-size:18px}.table{height:calc(100% - 36px);display:grid;grid-template-rows:28px minmax(0,1fr);overflow:hidden;border:1px solid #1c3049;border-radius:7px}.thead,.row{display:grid;grid-template-columns:2fr .58fr .8fr .58fr .58fr 3.15fr;align-items:center}.thead{height:28px;background:#142238;color:#9db0c9;font-size:11px;font-weight:800}.row{min-height:30px;border-top:1px solid #1b2b42;font-size:11px;color:#dbe5f2}.row:nth-child(odd){background:#0b1524}.row span{padding:0 10px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}#rows{min-height:0;overflow:auto;scrollbar-color:#2b405e #09111e;scrollbar-width:thin}.actionsCell{display:flex;align-items:center;gap:3px;padding:0 7px!important;overflow:hidden}.badge{display:inline-flex;align-items:center;height:20px;border-radius:5px;border:1px solid #2b405e;background:#111f33;color:#cfe0f5;padding:0 5px;font-size:9px;font-weight:900;flex:0 0 auto}.badge.ok{border-color:#166447;background:#0e3327;color:var(--green)}.badge.cool{border-color:#655023;background:#2a2113;color:var(--amber)}.badge.skip{border-color:#40536d;color:#8fa1ba}.badge.deep{border-color:#8d5a18;background:#2b1d10;color:var(--amber)}.badge.balanced{border-color:#1a5d86;background:#0d263b;color:#58b9ff}.badge.light{border-color:#355178;background:#111f33;color:#b8c8dc}.goodtxt{color:var(--green);font-weight:800}.amber{color:var(--amber)}.status{display:none}
.feedbox{height:calc(100% - 36px);border:1px solid #1c3049;border-radius:7px;background:#09111e;padding:10px;overflow:auto;font-family:Consolas,monospace;font-size:11px;line-height:1.38;white-space:pre;color:#e5edf7;scrollbar-color:#2b405e #09111e;scrollbar-width:thin}.footer{display:none}
</style>
</head>
<body>
<div class='shell'>
  <div class='chrome' onmousedown=""send('drag')"">
    <div class='brand'><img id='logo'><div><b>SMART NAP</b><span>BACKGROUND CONTROL</span></div></div>
    <div class='win'><button onclick=""send('minimize');event.stopPropagation()"">_</button><button onclick=""send('close');event.stopPropagation()"">X</button></div>
  </div>
  <div class='body'>
    <aside class='rail'>
      <button class='nav active' title='Dashboard'><svg viewBox='0 0 24 24'><path fill='currentColor' d='M3 11.5 12 4l9 7.5v8.5h-6v-5H9v5H3z'/></svg></button>
      <button class='nav' title='Nap Score' onclick=""send('score')""><svg viewBox='0 0 24 24'><path fill='currentColor' d='M4 19h16v2H4zM6 10h3v7H6zm5-5h3v12h-3zm5 8h3v4h-3z'/></svg></button>
      <button class='nav' title='Activity Log' onclick=""send('log')""><svg viewBox='0 0 24 24'><path fill='currentColor' d='M5 4h14v16H5zm3 4v2h8V8zm0 4v2h8v-2zm0 4v2h5v-2z'/></svg></button>
      <button class='nav' title='Local Files' onclick=""send('folder')""><svg viewBox='0 0 24 24'><path fill='currentColor' d='M3 6h7l2 2h9v10H3z'/></svg></button>
      <button class='nav' title='GitHub' onclick=""send('github')""><svg viewBox='0 0 24 24'><path fill='currentColor' d='M12 2a10 10 0 0 0-3 19c.5.1.7-.2.7-.5v-2c-3 .6-3.6-1.2-3.6-1.2-.5-1.1-1.1-1.4-1.1-1.4-.9-.6.1-.6.1-.6 1 .1 1.6 1.1 1.6 1.1.9 1.5 2.4 1.1 3 .8.1-.7.4-1.1.7-1.3-2.4-.3-4.9-1.2-4.9-5A3.9 3.9 0 0 1 6.5 7c-.1-.3-.5-1.4.1-2.8 0 0 .9-.3 3 1.1a10.3 10.3 0 0 1 5.4 0c2.1-1.4 3-1.1 3-1.1.6 1.4.2 2.5.1 2.8a3.9 3.9 0 0 1 1 2.7c0 3.9-2.5 4.8-4.9 5.1.4.3.8 1 .8 2v3c0 .3.2.6.8.5A10 10 0 0 0 12 2z'/></svg></button>
      <div class='ver' id='version'>v0.0.0</div>
    </aside>
    <main class='main'>
      <section class='top'><div class='title'><h1>Dashboard</h1><p>Smart Background Nap</p></div><div class='pills'><span class='pill' id='live'>LIVE</span><span class='pill' id='motor'>MOTOR</span><span class='pill' id='startup'>STARTUP</span></div></section>
      <section class='hero'>
        <div><h2>Background apps under control</h2><p>Open apps stay quiet while your active window keeps priority.</p><div class='chips'><span class='chip'>CPU</span><span class='chip'>RAM</span><span class='chip'>EcoQoS</span><span class='chip'>Wake restore</span></div></div>
        <div class='control' id='control'><div class='engineHead'><h3 id='actionTitle'>Nap Engine</h3><span class='state' id='state'>READY</span></div><div class='detail' id='detail'>Waiting.</div><div class='engineStats'><div><small>Pass</small><b id='enginePass'>-</b></div><div><small>Next</small><b id='engineNext'>-</b></div><div><small>Event</small><b id='engineEvent'>-</b></div><div><small>UI</small><b id='engineBeat'>-</b></div></div><div class='bar'><i></i></div><div class='actions'><button class='btn primary' id='apply' onclick=""send('apply')"">Otimizar agora</button><button class='btn' id='motorBtn' onclick=""send('toggleMotor')"">Pausar motor</button><button class='btn' onclick=""send('restore')"">Restore</button></div></div>
      </section>
      <section class='cards'><div class='card'><small>Auto mode</small><b id='autoCard'>-</b></div><div class='card'><small>Startup</small><b id='startupCard'>-</b></div><div class='card'><small>Last event</small><b id='lastCard'>-</b></div><div class='card'><small>Last result</small><b id='resultCard'>-</b></div></section>
      <section class='live'><div class='panel'><h3>Live Manager</h3><div class='table'><div class='thead'><span>App</span><span>Score</span><span>Delta</span><span>CPU</span><span>Bursts</span><span>Action</span></div><div id='rows'></div></div><div class='status' id='managerStatus'>Waiting for score data.</div></div><div class='panel'><h3>Event Stream</h3><div class='feedbox' id='events'></div><div class='status'><button class='btn' onclick=""send('toggleStartup')"">Startup</button> <button class='btn' onclick=""send('safety')"">Safety</button> <button class='btn' onclick=""send('config')"">Config</button></div></div></section>
      <footer class='footer' id='creator'></footer>
    </main>
  </div>
</div>
<script>
function send(action){ if(window.chrome&&chrome.webview){ chrome.webview.postMessage({action:action}); } }
function txt(id,v){ const e=document.getElementById(id); if(e)e.textContent=v; }
function cls(id,c){ const e=document.getElementById(id); if(e)e.className=c; }
function smartNapUpdate(s){
 document.body.classList.toggle('busy',!!s.Busy);
 document.getElementById('control').classList.toggle('busy',!!s.Busy);
 if(s.Logo){ document.getElementById('logo').src=s.Logo; }
 txt('version','v'+s.AppVersion); txt('creator',s.Creator); txt('actionTitle',s.Title); txt('detail',s.Detail); txt('state',s.RunState);
 txt('enginePass',s.Managed+' apps'); txt('engineNext',s.NextPass); txt('engineEvent',s.LastEventAge); txt('engineBeat',s.Heartbeat);
 txt('autoCard',s.AutoMode?'On':'Off'); txt('startupCard',s.Startup?'On':'Off'); txt('lastCard',s.LastRun); txt('resultCard',s.Result);
 txt('motorBtn',s.AutoMode?'Pausar motor':'Retomar motor'); txt('apply',s.CanStop?'Parar':'Otimizar agora');
 document.getElementById('apply').className=s.CanStop?'btn danger':'btn primary';
 cls('motor',s.AutoMode?'pill good':'pill warn'); txt('motor',s.AutoMode?'MOTOR ACTIVE':'MANUAL');
 cls('startup',s.Startup?'pill good':'pill'); txt('startup',s.Startup?'STARTUP ON':'STARTUP OFF'); cls('live','pill good'); txt('live','LIVE '+s.Heartbeat);
 const rows=document.getElementById('rows'); rows.innerHTML='';
 (s.Rows||[]).forEach(r=>{ const d=document.createElement('div'); d.className='row'; d.innerHTML='<span>'+esc(r.Name)+'</span><span class=""goodtxt"">'+esc(r.Score)+'</span><span class=""amber"">'+esc(r.Delta)+'</span><span>'+esc(r.Cpu)+'</span><span>'+esc(r.Bursts)+'</span><span class=""actionsCell"">'+actionBadges(r.Action)+'</span>'; rows.appendChild(d); });
 if(!s.Rows||s.Rows.length===0){ rows.innerHTML='<div class=""row""><span>No managed entries yet.</span><span></span><span></span><span></span><span></span><span></span></div>'; }
 txt('managerStatus',(s.Rows&&s.Rows.length)?('Tracking latest pass: '+s.Rows.length+' managed entries.'):'Run a pass to populate live entries.');
 txt('events',(s.Events||[]).join('\n'));
}
function esc(v){return String(v==null?'':v).replace(/[&<>""']/g,function(m){if(m==='&')return '&amp;';if(m==='<')return '&lt;';if(m==='>')return '&gt;';if(m==='""')return '&quot;';return '&#39;';});}
function actionBadges(v){return String(v||'').split('/').map(x=>x.trim()).filter(Boolean).map(x=>{const low=x.toLowerCase();let c='badge';if(low.indexOf('tier deep')===0)c+=' deep';else if(low.indexOf('tier balanced')===0)c+=' balanced';else if(low.indexOf('tier light')===0)c+=' light';else if(low.indexOf(' ok')>=0)c+=' ok';else if(low.indexOf('cooldown')>=0)c+=' cool';else if(low.indexOf('skip')>=0||low.indexOf('disabled')>=0)c+=' skip';let label=x.replace('SkippedBelowThreshold','Skip').replace(/^Tier /,'').replace(/^Eco /,'E ');return '<b class=""'+c+'"">'+esc(label)+'</b>';}).join('');}
if(window.chrome&&chrome.webview){ chrome.webview.addEventListener('message',e=>smartNapUpdate(e.data)); }
window.addEventListener('DOMContentLoaded',()=>send('ready'));
</script>
</body>
</html>";
        }

        private sealed class WebDashboardState
        {
            public string AppVersion { get; set; }
            public string Creator { get; set; }
            public string Language { get; set; }
            public bool FirstRun { get; set; }
            public bool AutoMode { get; set; }
            public bool Startup { get; set; }
            public bool Learning { get; set; }
            public int LearningProfiles { get; set; }
            public string MemoryPressure { get; set; }
            public double FreeMemoryMB { get; set; }
            public bool IsElevated { get; set; }
            public int PermissionDeniedCount { get; set; }
            public List<string> PermissionDeniedApps { get; set; }
            public bool Busy { get; set; }
            public bool CanStop { get; set; }
            public string RunState { get; set; }
            public string Title { get; set; }
            public string Detail { get; set; }
            public string LastRun { get; set; }
            public string Result { get; set; }
            public string Managed { get; set; }
            public string Reclaimed { get; set; }
            public string TopApp { get; set; }
            public string Wake { get; set; }
            public string Heartbeat { get; set; }
            public string LastEventAge { get; set; }
            public string NextPass { get; set; }
            public string Logo { get; set; }
            public List<WebManagerRow> Rows { get; set; }
            public List<string> Events { get; set; }
        }

        private sealed class ScoreMeta
        {
            public bool LearningEnabled { get; set; }
            public int LearningProfiles { get; set; }
            public string MemoryPressure { get; set; }
            public double FreeMemoryMB { get; set; }
            public int PermissionDeniedCount { get; set; }
            public List<string> PermissionDeniedApps { get; set; }
        }

        private sealed class WebManagerRow
        {
            public string Name { get; set; }
            public string Score { get; set; }
            public string Delta { get; set; }
            public string Cpu { get; set; }
            public string Bursts { get; set; }
            public string Action { get; set; }
            public bool PermissionDenied { get; set; }
            public double RawScore { get; set; }
        }
    }
#endif

    private sealed class ModernMainWindow : System.Windows.Window
#if NET9_0_OR_GREATER
        , IDashboardWindow
#endif
    {
        private static readonly System.Windows.Media.SolidColorBrush ShellBrush = MakeBrush(5, 9, 15);
        private static readonly System.Windows.Media.SolidColorBrush PanelBrush = MakeBrush(11, 18, 30);
        private static readonly System.Windows.Media.SolidColorBrush PanelSoftBrush = MakeBrush(16, 27, 43);
        private static readonly System.Windows.Media.SolidColorBrush BorderLineBrush = MakeBrush(44, 62, 86);
        private static readonly System.Windows.Media.SolidColorBrush AccentBrush = MakeBrush(255, 161, 43);
        private static readonly System.Windows.Media.SolidColorBrush AccentBlueBrush = MakeBrush(64, 145, 255);
        private static readonly System.Windows.Media.SolidColorBrush GoodBrush = MakeBrush(42, 210, 132);
        private static readonly System.Windows.Media.SolidColorBrush DangerBrush = MakeBrush(235, 70, 78);
        private static readonly System.Windows.Media.SolidColorBrush TextBrush = MakeBrush(241, 246, 252);
        private static readonly System.Windows.Media.SolidColorBrush SoftTextBrush = MakeBrush(150, 165, 185);
        private static readonly System.Windows.Media.FontFamily UiFont = new System.Windows.Media.FontFamily("Segoe UI");

        private System.Windows.Controls.TextBlock autoValue;
        private System.Windows.Controls.TextBlock startupValue;
        private System.Windows.Controls.TextBlock lastRunValue;
        private System.Windows.Controls.TextBlock resultValue;
        private System.Windows.Controls.Border statusPill;
        private System.Windows.Controls.Border livePill;
        private System.Windows.Controls.Border runStatePill;
        private System.Windows.Controls.TextBlock actionTitle;
        private System.Windows.Controls.TextBlock actionDetail;
        private System.Windows.Controls.TextBlock managerStatus;
        private System.Windows.Controls.TextBlock feedStatus;
        private System.Windows.Controls.Button optimizeButton;
        private System.Windows.Controls.Button motorButton;
        private System.Windows.Controls.Button moreButton;
        private System.Windows.Controls.ProgressBar actionProgress;
        private System.Windows.Controls.StackPanel managerRowsPanel;
        private System.Windows.Controls.TextBlock feedText;
        private RunControl activeRunControl;
        private bool activeRunCanStop;
        private DateTime activeRunStartedAt;
        private string activeUiEventLine;
        private bool autoModeActive;
        private bool startupModeActive;
        private bool busy;
        private System.Windows.Threading.DispatcherTimer refreshTimer;
        private System.Windows.Threading.DispatcherTimer liveTimer;
        private System.Windows.Threading.DispatcherTimer actionTimer;

        public ModernMainWindow()
        {
            Title = AppName;
            Width = 1280;
            Height = 760;
            MinWidth = 1140;
            MinHeight = 620;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            WindowStyle = System.Windows.WindowStyle.None;
            ResizeMode = System.Windows.ResizeMode.CanMinimize;
            Background = ShellBrush;
            Icon = LoadWpfImage(iconPath);
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            System.Windows.Media.TextOptions.SetTextFormattingMode(this, System.Windows.Media.TextFormattingMode.Display);
            System.Windows.Media.RenderOptions.SetBitmapScalingMode(this, System.Windows.Media.BitmapScalingMode.HighQuality);

            BuildLayout();

            refreshTimer = new System.Windows.Threading.DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(60);
            refreshTimer.Tick += delegate { if (IsVisible && WindowState != System.Windows.WindowState.Minimized && !busy) { RefreshStatus(); } };

            liveTimer = new System.Windows.Threading.DispatcherTimer();
            liveTimer.Interval = TimeSpan.FromSeconds(2.5);
            liveTimer.Tick += delegate { if (IsVisible && WindowState != System.Windows.WindowState.Minimized) { RefreshLiveManager(); } };

            actionTimer = new System.Windows.Threading.DispatcherTimer();
            actionTimer.Interval = TimeSpan.FromMilliseconds(120);
            actionTimer.Tick += delegate { UpdateActiveRunVisuals(); };

            Loaded += delegate { StartDashboardActivity(); RefreshStatus(); RefreshLiveManager(); };
            IsVisibleChanged += delegate
            {
                if (IsVisible && WindowState != System.Windows.WindowState.Minimized)
                {
                    StartDashboardActivity();
                    RefreshStatus();
                    RefreshLiveManager();
                }
                else
                {
                    StopDashboardActivity();
                }
            };
            StateChanged += delegate
            {
                if (WindowState == System.Windows.WindowState.Minimized)
                {
                    Hide();
                    WindowState = System.Windows.WindowState.Normal;
                }
            };
        }

        private static System.Windows.Media.SolidColorBrush MakeBrush(byte r, byte g, byte b)
        {
            System.Windows.Media.SolidColorBrush brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static System.Windows.Media.SolidColorBrush MakeBrush(byte a, byte r, byte g, byte b)
        {
            System.Windows.Media.SolidColorBrush brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }

        private static System.Windows.Media.ImageSource LoadWpfImage(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                System.Windows.Media.Imaging.BitmapImage image = new System.Windows.Media.Imaging.BitmapImage();
                image.BeginInit();
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private void BuildLayout()
        {
            System.Windows.Controls.Border frame = new System.Windows.Controls.Border();
            frame.BorderBrush = AccentBrush;
            frame.BorderThickness = new System.Windows.Thickness(1);
            frame.Background = ShellBrush;
            frame.CornerRadius = new System.Windows.CornerRadius(8);
            Content = frame;

            System.Windows.Controls.Grid root = new System.Windows.Controls.Grid();
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(54) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            frame.Child = root;

            System.Windows.Controls.Grid chrome = new System.Windows.Controls.Grid();
            chrome.Background = MakeBrush(4, 8, 14);
            chrome.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            chrome.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });
            chrome.MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
            root.Children.Add(chrome);

            System.Windows.Controls.StackPanel brand = new System.Windows.Controls.StackPanel();
            brand.Orientation = System.Windows.Controls.Orientation.Horizontal;
            brand.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            brand.Margin = new System.Windows.Thickness(20, 0, 0, 0);
            chrome.Children.Add(brand);

            System.Windows.Controls.Image logo = new System.Windows.Controls.Image();
            logo.Source = LoadWpfImage(logoPath);
            logo.Width = 30;
            logo.Height = 30;
            logo.Margin = new System.Windows.Thickness(0, 0, 9, 0);
            logo.Stretch = System.Windows.Media.Stretch.Uniform;
            brand.Children.Add(logo);
            System.Windows.Controls.StackPanel brandCopy = new System.Windows.Controls.StackPanel();
            brandCopy.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            brand.Children.Add(brandCopy);
            System.Windows.Controls.TextBlock name = CreateText("SMART NAP", 14, System.Windows.FontWeights.Bold, TextBrush);
            brandCopy.Children.Add(name);
            System.Windows.Controls.TextBlock edition = CreateText("BACKGROUND CONTROL", 10, System.Windows.FontWeights.SemiBold, MakeBrush(106, 122, 145));
            edition.Margin = new System.Windows.Thickness(0, 1, 0, 0);
            brandCopy.Children.Add(edition);

            System.Windows.Controls.StackPanel windowButtons = new System.Windows.Controls.StackPanel();
            windowButtons.Orientation = System.Windows.Controls.Orientation.Horizontal;
            windowButtons.Margin = new System.Windows.Thickness(0, 12, 16, 0);
            System.Windows.Controls.Grid.SetColumn(windowButtons, 1);
            chrome.Children.Add(windowButtons);
            windowButtons.Children.Add(CreateChromeButton("_", delegate { WindowState = System.Windows.WindowState.Minimized; }));
            windowButtons.Children.Add(CreateChromeButton("X", delegate { Close(); }));

            System.Windows.Controls.Grid body = new System.Windows.Controls.Grid();
            body.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(86) });
            body.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            System.Windows.Controls.Grid.SetRow(body, 1);
            root.Children.Add(body);

            System.Windows.Controls.StackPanel rail = new System.Windows.Controls.StackPanel();
            rail.Background = MakeBrush(8, 15, 26);
            rail.Margin = new System.Windows.Thickness(0);
            rail.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            body.Children.Add(rail);
            rail.Children.Add(CreateNavButton("\uE80F", "Dashboard", true, null));
            rail.Children.Add(CreateNavButton("\uE9D9", "Nap Score", false, delegate { OpenScore(); }));
            rail.Children.Add(CreateNavButton("\uE81C", "Activity Log", false, delegate { OpenLog(); }));
            rail.Children.Add(CreateNavButton("\uE8A5", "Local Files", false, delegate { OpenFolder(); }));
            rail.Children.Add(CreateNavButton("\uE8A1", "GitHub", false, delegate { OpenGitHub(); }));

            System.Windows.Controls.TextBlock version = CreateText("v" + AppVersion, 10, System.Windows.FontWeights.Bold, MakeBrush(96, 111, 132));
            version.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            version.Margin = new System.Windows.Thickness(0, 218, 0, 0);
            rail.Children.Add(version);

            System.Windows.Controls.Grid content = new System.Windows.Controls.Grid();
            content.Margin = new System.Windows.Thickness(24, 18, 24, 18);
            content.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(66) });
            content.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(236) });
            content.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(112) });
            content.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            content.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(28) });
            System.Windows.Controls.Grid.SetColumn(content, 1);
            body.Children.Add(content);

            BuildHeader(content);
            BuildHero(content);
            BuildCards(content);
            BuildLiveArea(content);

            System.Windows.Controls.TextBlock footer = CreateText(CreatorLine, 11, System.Windows.FontWeights.Normal, MakeBrush(92, 107, 129));
            footer.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            System.Windows.Controls.Grid.SetRow(footer, 4);
            content.Children.Add(footer);
        }

        private void BuildHeader(System.Windows.Controls.Grid content)
        {
            System.Windows.Controls.Grid header = new System.Windows.Controls.Grid();
            content.Children.Add(header);

            System.Windows.Controls.TextBlock title = CreateText("Overview", 17, System.Windows.FontWeights.Bold, TextBrush);
            title.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            header.Children.Add(title);

            statusPill = null;
            livePill = null;
        }

        private void BuildHero(System.Windows.Controls.Grid content)
        {
            System.Windows.Controls.Border hero = CreateCard(AccentBrush, 12);
            hero.Margin = new System.Windows.Thickness(0, 0, 0, 14);
            hero.Padding = new System.Windows.Thickness(0);
            System.Windows.Controls.Grid.SetRow(hero, 1);
            content.Children.Add(hero);

            System.Windows.Controls.Grid heroRoot = new System.Windows.Controls.Grid();
            heroRoot.ClipToBounds = true;
            hero.Child = heroRoot;

            System.Windows.Controls.Image bg = new System.Windows.Controls.Image();
            bg.Source = LoadWpfImage(heroPath);
            bg.Stretch = System.Windows.Media.Stretch.UniformToFill;
            bg.Opacity = 0.56;
            heroRoot.Children.Add(bg);

            System.Windows.Controls.Border shade = new System.Windows.Controls.Border();
            shade.Background = new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Color.FromArgb(245, 9, 16, 27),
                System.Windows.Media.Color.FromArgb(170, 9, 16, 27),
                new System.Windows.Point(0, 0.2),
                new System.Windows.Point(1, 0.9));
            heroRoot.Children.Add(shade);

            System.Windows.Controls.Grid heroGrid = new System.Windows.Controls.Grid();
            heroGrid.Margin = new System.Windows.Thickness(28, 18, 22, 18);
            heroGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            heroGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(392) });
            heroRoot.Children.Add(heroGrid);

            System.Windows.Controls.StackPanel copy = new System.Windows.Controls.StackPanel();
            copy.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            heroGrid.Children.Add(copy);
            System.Windows.Controls.TextBlock title = CreateText("Background apps under control", 33, System.Windows.FontWeights.Bold, TextBrush);
            title.Margin = new System.Windows.Thickness(0, 0, 0, 8);
            copy.Children.Add(title);
            System.Windows.Controls.TextBlock subtitle = CreateText("Keeps open apps quiet while the foreground stays awake.", 14, System.Windows.FontWeights.Normal, SoftTextBrush);
            subtitle.Margin = new System.Windows.Thickness(0, 0, 0, 20);
            copy.Children.Add(subtitle);

            System.Windows.Controls.StackPanel chips = new System.Windows.Controls.StackPanel();
            chips.Orientation = System.Windows.Controls.Orientation.Horizontal;
            copy.Children.Add(chips);
            chips.Children.Add(CreateChip("CPU", AccentBlueBrush));
            chips.Children.Add(CreateChip("RAM", AccentBrush));
            chips.Children.Add(CreateChip("EcoQoS", GoodBrush));
            chips.Children.Add(CreateChip("Wake restore", MakeBrush(154, 111, 255)));

            System.Windows.Controls.Border command = CreateCard(AccentBlueBrush, 11);
            command.Padding = new System.Windows.Thickness(18, 16, 18, 18);
            System.Windows.Controls.Grid.SetColumn(command, 1);
            heroGrid.Children.Add(command);

            System.Windows.Controls.StackPanel stack = new System.Windows.Controls.StackPanel();
            command.Child = stack;
            actionTitle = CreateText("Control Center", 22, System.Windows.FontWeights.Bold, TextBrush);
            actionTitle.Margin = new System.Windows.Thickness(0, 0, 0, 6);
            stack.Children.Add(actionTitle);
            runStatePill = CreatePill("MOTOR ATIVO", MakeBrush(20, 88, 60), GoodBrush);
            runStatePill.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            runStatePill.Margin = new System.Windows.Thickness(0, 0, 0, 8);
            stack.Children.Add(runStatePill);
            actionDetail = CreateText("Waiting for the next pass.", 12, System.Windows.FontWeights.Normal, SoftTextBrush);
            actionDetail.TextWrapping = System.Windows.TextWrapping.Wrap;
            actionDetail.Height = 34;
            stack.Children.Add(actionDetail);
            actionProgress = new System.Windows.Controls.ProgressBar();
            actionProgress.Height = 5;
            actionProgress.Minimum = 0;
            actionProgress.Maximum = 100;
            actionProgress.Value = 0;
            actionProgress.Foreground = AccentBrush;
            actionProgress.Background = MakeBrush(25, 39, 59);
            actionProgress.BorderBrush = MakeBrush(76, 94, 118);
            actionProgress.Margin = new System.Windows.Thickness(0, 4, 0, 12);
            stack.Children.Add(actionProgress);

            System.Windows.Controls.Grid actionGrid = new System.Windows.Controls.Grid();
            actionGrid.Height = 36;
            actionGrid.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
            actionGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1.35, System.Windows.GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1.2, System.Windows.GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(0.8, System.Windows.GridUnitType.Star) });
            stack.Children.Add(actionGrid);

            optimizeButton = CreateButton("Aplicar agora", true, delegate
            {
                if (busy && activeRunCanStop)
                {
                    StopCurrentActionWithFeedback();
                    return;
                }
                RunOptimizeNowActionWithFeedback();
            });
            actionGrid.Children.Add(optimizeButton);

            motorButton = CreateButton("Pausar motor", false, delegate { ToggleMotorFromButton(); });
            motorButton.Margin = new System.Windows.Thickness(8, 0, 0, 0);
            System.Windows.Controls.Grid.SetColumn(motorButton, 1);
            actionGrid.Children.Add(motorButton);

            moreButton = CreateButton("Mais", false, delegate { ShowMoreMenu(); });
            moreButton.Margin = new System.Windows.Thickness(8, 0, 0, 0);
            System.Windows.Controls.Grid.SetColumn(moreButton, 2);
            actionGrid.Children.Add(moreButton);
        }

        private void BuildCards(System.Windows.Controls.Grid content)
        {
            System.Windows.Controls.Grid cards = new System.Windows.Controls.Grid();
            cards.Margin = new System.Windows.Thickness(0, 0, 0, 14);
            for (int i = 0; i < 4; i++)
            {
                cards.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            }
            System.Windows.Controls.Grid.SetRow(cards, 2);
            content.Children.Add(cards);
            autoValue = AddStatusCard(cards, 0, "Auto mode", AccentBlueBrush);
            startupValue = AddStatusCard(cards, 1, "Startup", GoodBrush);
            lastRunValue = AddStatusCard(cards, 2, "Last pass", AccentBrush);
            resultValue = AddStatusCard(cards, 3, "Last result", AccentBlueBrush);
        }

        private void BuildLiveArea(System.Windows.Controls.Grid content)
        {
            System.Windows.Controls.Grid live = new System.Windows.Controls.Grid();
            live.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(2.05, System.Windows.GridUnitType.Star) });
            live.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            System.Windows.Controls.Grid.SetRow(live, 3);
            content.Children.Add(live);

            System.Windows.Controls.Border managerPanel = CreateCard(AccentBlueBrush, 10);
            managerPanel.Padding = new System.Windows.Thickness(18);
            live.Children.Add(managerPanel);
            System.Windows.Controls.Grid managerGrid = new System.Windows.Controls.Grid();
            managerGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            managerGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            managerGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            managerPanel.Child = managerGrid;
            System.Windows.Controls.TextBlock managerTitle = CreateText("Live Manager", 18, System.Windows.FontWeights.Bold, TextBrush);
            managerTitle.Margin = new System.Windows.Thickness(0, 0, 0, 12);
            managerGrid.Children.Add(managerTitle);
            System.Windows.Controls.Grid managerTable = CreateManagerTable();
            System.Windows.Controls.Grid.SetRow(managerTable, 1);
            managerGrid.Children.Add(managerTable);
            managerStatus = CreateText("Waiting for score data.", 12, System.Windows.FontWeights.Normal, SoftTextBrush);
            managerStatus.Margin = new System.Windows.Thickness(0, 10, 0, 0);
            System.Windows.Controls.Grid.SetRow(managerStatus, 2);
            managerGrid.Children.Add(managerStatus);

            System.Windows.Controls.Border feedPanel = CreateCard(AccentBlueBrush, 10);
            feedPanel.Padding = new System.Windows.Thickness(18);
            feedPanel.Margin = new System.Windows.Thickness(14, 0, 0, 0);
            System.Windows.Controls.Grid.SetColumn(feedPanel, 1);
            live.Children.Add(feedPanel);
            System.Windows.Controls.Grid feedGrid = new System.Windows.Controls.Grid();
            feedGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            feedGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            feedGrid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            feedPanel.Child = feedGrid;
            System.Windows.Controls.TextBlock feedTitle = CreateText("Event Stream", 18, System.Windows.FontWeights.Bold, TextBrush);
            feedTitle.Margin = new System.Windows.Thickness(0, 0, 0, 12);
            feedGrid.Children.Add(feedTitle);
            System.Windows.Controls.Border feedBox = new System.Windows.Controls.Border();
            feedBox.Background = MakeBrush(10, 17, 29);
            feedBox.BorderBrush = MakeBrush(30, 45, 65);
            feedBox.BorderThickness = new System.Windows.Thickness(1);
            feedBox.CornerRadius = new System.Windows.CornerRadius(5);
            System.Windows.Controls.ScrollViewer feedScroll = new System.Windows.Controls.ScrollViewer();
            feedScroll.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;
            feedScroll.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
            feedText = CreateText("", 11, System.Windows.FontWeights.Normal, TextBrush);
            feedText.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            feedText.Margin = new System.Windows.Thickness(10);
            feedText.TextWrapping = System.Windows.TextWrapping.NoWrap;
            feedScroll.Content = feedText;
            feedBox.Child = feedScroll;
            System.Windows.Controls.Grid.SetRow(feedBox, 1);
            feedGrid.Children.Add(feedBox);
            feedStatus = CreateText("No activity yet.", 12, System.Windows.FontWeights.Normal, SoftTextBrush);
            feedStatus.Margin = new System.Windows.Thickness(0, 10, 0, 0);
            System.Windows.Controls.Grid.SetRow(feedStatus, 2);
            feedGrid.Children.Add(feedStatus);
        }

        private System.Windows.Controls.Button CreateChromeButton(string text, Action action)
        {
            System.Windows.Controls.Button button = CreateButton(text, false, delegate { action(); });
            button.Width = 34;
            button.Height = 28;
            button.Margin = new System.Windows.Thickness(4, 0, 0, 0);
            button.FontWeight = System.Windows.FontWeights.Bold;
            button.Background = MakeBrush(4, 8, 14);
            button.BorderBrush = MakeBrush(4, 8, 14);
            button.Foreground = SoftTextBrush;
            return button;
        }

        private System.Windows.Controls.Button CreateNavButton(string glyph, string tooltip, bool active, Action action)
        {
            System.Windows.Controls.Button button = CreateButton(glyph, false, delegate { if (action != null) { action(); } });
            System.Windows.Controls.StackPanel row = new System.Windows.Controls.StackPanel();
            row.Orientation = System.Windows.Controls.Orientation.Horizontal;
            row.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;

            System.Windows.Controls.TextBlock icon = CreateText(glyph, 15, System.Windows.FontWeights.Normal, active ? AccentBrush : SoftTextBrush);
            icon.FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets");
            icon.Width = 24;
            icon.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            row.Children.Add(icon);

            System.Windows.Controls.TextBlock label = CreateText(tooltip, 12, active ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.SemiBold, active ? AccentBrush : SoftTextBrush);
            label.Margin = new System.Windows.Thickness(9, 0, 0, 0);
            label.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            row.Children.Add(label);

            button.Content = row;
            button.Width = 126;
            button.Height = 44;
            button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
            button.Padding = new System.Windows.Thickness(13, 0, 10, 0);
            button.Margin = new System.Windows.Thickness(16, 12, 16, 0);
            button.ToolTip = CreateNavToolTip(tooltip, GetNavDescription(tooltip));
            button.Background = active ? MakeBrush(36, 29, 19) : MakeBrush(8, 15, 26);
            button.BorderBrush = active ? MakeBrush(92, 67, 29) : MakeBrush(20, 31, 48);
            button.Foreground = active ? AccentBrush : SoftTextBrush;
            return button;
        }

        private System.Windows.Controls.ToolTip CreateNavToolTip(string title, string description)
        {
            System.Windows.Controls.ToolTip tip = new System.Windows.Controls.ToolTip();
            tip.Background = MakeBrush(12, 21, 34);
            tip.BorderBrush = BorderLineBrush;
            tip.Foreground = TextBrush;
            tip.Padding = new System.Windows.Thickness(10, 8, 10, 8);
            System.Windows.Controls.StackPanel stack = new System.Windows.Controls.StackPanel();
            stack.Children.Add(CreateText(title, 12, System.Windows.FontWeights.Bold, TextBrush));
            System.Windows.Controls.TextBlock detail = CreateText(description, 11, System.Windows.FontWeights.Normal, SoftTextBrush);
            detail.Margin = new System.Windows.Thickness(0, 3, 0, 0);
            detail.MaxWidth = 220;
            detail.TextWrapping = System.Windows.TextWrapping.Wrap;
            stack.Children.Add(detail);
            tip.Content = stack;
            return tip;
        }

        private string GetNavDescription(string title)
        {
            if (String.Equals(title, "Dashboard", StringComparison.OrdinalIgnoreCase)) { return "Overview, live state, and quick controls."; }
            if (String.Equals(title, "Nap Score", StringComparison.OrdinalIgnoreCase)) { return "Open the scoring table for recently managed apps."; }
            if (String.Equals(title, "Activity Log", StringComparison.OrdinalIgnoreCase)) { return "Open the background optimization log."; }
            if (String.Equals(title, "Local Files", StringComparison.OrdinalIgnoreCase)) { return "Open config, reports, and runtime outputs."; }
            if (String.Equals(title, "GitHub", StringComparison.OrdinalIgnoreCase)) { return "Open the project repository."; }
            return title;
        }

        private System.Windows.Controls.TextBlock CreateTab(string text, bool active)
        {
            System.Windows.Controls.TextBlock label = CreateText(text, 14, active ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal, active ? AccentBrush : SoftTextBrush);
            label.Margin = new System.Windows.Thickness(0, 0, 28, 0);
            return label;
        }

        private System.Windows.Controls.Border CreateChip(string text, System.Windows.Media.Brush accent)
        {
            System.Windows.Controls.Border chip = new System.Windows.Controls.Border();
            chip.CornerRadius = new System.Windows.CornerRadius(3);
            chip.Background = MakeBrush(32, 45, 66);
            chip.Margin = new System.Windows.Thickness(0, 0, 8, 0);
            chip.Padding = new System.Windows.Thickness(12, 6, 12, 6);
            System.Windows.Controls.TextBlock label = CreateText(text, 11, System.Windows.FontWeights.Bold, TextBrush);
            label.Foreground = accent;
            chip.Child = label;
            return chip;
        }

        private System.Windows.Controls.Border CreatePill(string text, System.Windows.Media.Brush background, System.Windows.Media.Brush foreground)
        {
            System.Windows.Controls.Border pill = new System.Windows.Controls.Border();
            pill.CornerRadius = new System.Windows.CornerRadius(4);
            pill.Background = background;
            pill.Padding = new System.Windows.Thickness(11, 5, 11, 5);
            pill.Margin = new System.Windows.Thickness(8, 0, 0, 0);
            pill.Child = CreateText(text, 11, System.Windows.FontWeights.Bold, foreground);
            return pill;
        }

        private void SetPill(System.Windows.Controls.Border pill, string text, System.Windows.Media.Brush background, System.Windows.Media.Brush foreground)
        {
            if (pill == null)
            {
                return;
            }

            pill.Background = background;
            System.Windows.Controls.TextBlock child = pill.Child as System.Windows.Controls.TextBlock;
            if (child != null)
            {
                child.Text = text;
                child.Foreground = foreground;
            }
        }

        private System.Windows.Controls.TextBlock CreateText(string text, double size, System.Windows.FontWeight weight, System.Windows.Media.Brush color)
        {
            System.Windows.Controls.TextBlock block = new System.Windows.Controls.TextBlock();
            block.Text = text;
            block.FontFamily = UiFont;
            block.FontSize = size;
            block.FontWeight = weight;
            block.Foreground = color;
            block.TextTrimming = System.Windows.TextTrimming.CharacterEllipsis;
            return block;
        }

        private System.Windows.Controls.Border CreateCard(System.Windows.Media.Brush accent, double radius)
        {
            System.Windows.Controls.Border card = new System.Windows.Controls.Border();
            card.CornerRadius = new System.Windows.CornerRadius(radius);
            card.BorderThickness = new System.Windows.Thickness(1);
            card.BorderBrush = BorderLineBrush;
            card.Background = new System.Windows.Media.LinearGradientBrush(
                System.Windows.Media.Color.FromRgb(16, 27, 43),
                System.Windows.Media.Color.FromRgb(8, 14, 24),
                new System.Windows.Point(0, 0),
                new System.Windows.Point(1, 1));
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0.18,
                Color = System.Windows.Media.Color.FromRgb(0, 0, 0)
            };
            return card;
        }

        private System.Windows.Controls.TextBlock AddStatusCard(System.Windows.Controls.Grid parent, int column, string title, System.Windows.Media.Brush accent)
        {
            System.Windows.Controls.Border card = CreateCard(accent, 8);
            card.Margin = new System.Windows.Thickness(column == 0 ? 0 : 8, 0, column == 3 ? 0 : 8, 0);
            card.Padding = new System.Windows.Thickness(16, 13, 16, 12);
            System.Windows.Controls.Grid.SetColumn(card, column);
            parent.Children.Add(card);

            System.Windows.Controls.Grid inner = new System.Windows.Controls.Grid();
            inner.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            inner.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            card.Child = inner;

            System.Windows.Controls.Border top = new System.Windows.Controls.Border();
            top.Height = 3;
            top.Background = accent;
            top.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            top.Margin = new System.Windows.Thickness(0, -13, 44, 0);
            inner.Children.Add(top);

            System.Windows.Controls.TextBlock caption = CreateText(title, 12, System.Windows.FontWeights.Normal, SoftTextBrush);
            caption.Margin = new System.Windows.Thickness(0, 0, 0, 14);
            inner.Children.Add(caption);

            System.Windows.Controls.TextBlock value = CreateText("...", 17, System.Windows.FontWeights.Bold, TextBrush);
            value.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            System.Windows.Controls.Grid.SetRow(value, 1);
            inner.Children.Add(value);
            return value;
        }

        private System.Windows.Controls.Button CreateButton(string text, bool primary, System.Windows.RoutedEventHandler handler)
        {
            System.Windows.Controls.Button button = new System.Windows.Controls.Button();
            button.Content = text;
            button.FontFamily = UiFont;
            button.FontSize = 13;
            button.FontWeight = primary ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.SemiBold;
            button.Height = 36;
            button.Padding = new System.Windows.Thickness(14, 0, 14, 0);
            button.Background = primary ? AccentBrush : MakeBrush(21, 34, 53);
            button.BorderBrush = primary ? AccentBrush : BorderLineBrush;
            button.Foreground = primary ? MakeBrush(18, 20, 24) : TextBrush;
            button.Cursor = System.Windows.Input.Cursors.Hand;
            button.Template = CreateButtonTemplate();
            button.Click += handler;
            return button;
        }

        private System.Windows.Controls.ControlTemplate CreateButtonTemplate()
        {
            System.Windows.FrameworkElementFactory border = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.Border));
            border.Name = "Chrome";
            border.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new System.Windows.CornerRadius(3));
            border.SetValue(System.Windows.Controls.Border.BorderThicknessProperty, new System.Windows.TemplateBindingExtension(System.Windows.Controls.Control.BorderThicknessProperty));
            border.SetValue(System.Windows.Controls.Border.BorderBrushProperty, new System.Windows.TemplateBindingExtension(System.Windows.Controls.Control.BorderBrushProperty));
            border.SetValue(System.Windows.Controls.Border.BackgroundProperty, new System.Windows.TemplateBindingExtension(System.Windows.Controls.Control.BackgroundProperty));

            System.Windows.FrameworkElementFactory content = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            content.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            content.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
            content.SetValue(System.Windows.Controls.ContentPresenter.RecognizesAccessKeyProperty, true);
            border.AppendChild(content);

            System.Windows.Controls.ControlTemplate template = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.Button));
            template.VisualTree = border;
            System.Windows.Trigger hover = new System.Windows.Trigger();
            hover.Property = System.Windows.Controls.Button.IsMouseOverProperty;
            hover.Value = true;
            hover.Setters.Add(new System.Windows.Setter(System.Windows.UIElement.OpacityProperty, 0.88, "Chrome"));
            template.Triggers.Add(hover);

            System.Windows.Trigger pressed = new System.Windows.Trigger();
            pressed.Property = System.Windows.Controls.Button.IsPressedProperty;
            pressed.Value = true;
            pressed.Setters.Add(new System.Windows.Setter(System.Windows.UIElement.OpacityProperty, 0.72, "Chrome"));
            template.Triggers.Add(pressed);
            return template;
        }

        private System.Windows.Controls.Grid CreateManagerTable()
        {
            System.Windows.Controls.Grid table = new System.Windows.Controls.Grid();
            table.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            table.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

            System.Windows.Controls.Grid header = CreateManagerRowGrid();
            header.Background = MakeBrush(18, 29, 45);
            AddManagerCell(header, "App", 0, SoftTextBrush, System.Windows.FontWeights.Bold);
            AddManagerCell(header, "Score", 1, SoftTextBrush, System.Windows.FontWeights.Bold);
            AddManagerCell(header, "Delta", 2, SoftTextBrush, System.Windows.FontWeights.Bold);
            AddManagerCell(header, "CPU", 3, SoftTextBrush, System.Windows.FontWeights.Bold);
            AddManagerCell(header, "Bursts", 4, SoftTextBrush, System.Windows.FontWeights.Bold);
            AddManagerCell(header, "Action", 5, SoftTextBrush, System.Windows.FontWeights.Bold);
            table.Children.Add(header);

            System.Windows.Controls.Border rowsBox = new System.Windows.Controls.Border();
            rowsBox.Background = MakeBrush(10, 17, 29);
            rowsBox.BorderBrush = MakeBrush(30, 45, 65);
            rowsBox.BorderThickness = new System.Windows.Thickness(1, 0, 1, 1);
            rowsBox.CornerRadius = new System.Windows.CornerRadius(0, 0, 5, 5);
            System.Windows.Controls.ScrollViewer scroll = new System.Windows.Controls.ScrollViewer();
            scroll.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;
            scroll.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
            managerRowsPanel = new System.Windows.Controls.StackPanel();
            scroll.Content = managerRowsPanel;
            rowsBox.Child = scroll;
            System.Windows.Controls.Grid.SetRow(rowsBox, 1);
            table.Children.Add(rowsBox);
            return table;
        }

        private System.Windows.Controls.Grid CreateManagerRowGrid()
        {
            System.Windows.Controls.Grid row = new System.Windows.Controls.Grid();
            row.Height = 28;
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(2.1, System.Windows.GridUnitType.Star) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(0.8, System.Windows.GridUnitType.Star) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1.0, System.Windows.GridUnitType.Star) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(0.75, System.Windows.GridUnitType.Star) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(0.75, System.Windows.GridUnitType.Star) });
            row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(2.7, System.Windows.GridUnitType.Star) });
            return row;
        }

        private void AddManagerCell(System.Windows.Controls.Grid row, string text, int column, System.Windows.Media.Brush color, System.Windows.FontWeight weight)
        {
            System.Windows.Controls.TextBlock cell = CreateText(text, 11, weight, color);
            cell.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            cell.Margin = new System.Windows.Thickness(10, 0, 6, 0);
            cell.TextTrimming = System.Windows.TextTrimming.CharacterEllipsis;
            System.Windows.Controls.Grid.SetColumn(cell, column);
            row.Children.Add(cell);
        }

        private void AddManagerRow(ManagerRow source, int index)
        {
            if (managerRowsPanel == null)
            {
                return;
            }

            System.Windows.Controls.Border border = new System.Windows.Controls.Border();
            border.Background = index % 2 == 0 ? MakeBrush(12, 21, 34) : MakeBrush(10, 18, 30);
            border.BorderBrush = MakeBrush(24, 39, 59);
            border.BorderThickness = new System.Windows.Thickness(0, 0, 0, 1);
            System.Windows.Controls.Grid row = CreateManagerRowGrid();
            AddManagerCell(row, source.ProcessName, 0, TextBrush, System.Windows.FontWeights.SemiBold);
            AddManagerCell(row, FormatDecimal(source.Score), 1, source.Score >= 100 ? GoodBrush : TextBrush, System.Windows.FontWeights.SemiBold);
            AddManagerCell(row, FormatDecimal(source.DeltaMB) + " MB", 2, AccentBrush, System.Windows.FontWeights.Normal);
            AddManagerCell(row, FormatDecimal(source.CpuPercent), 3, TextBrush, System.Windows.FontWeights.Normal);
            AddManagerCell(row, source.BurstCount.ToString(CultureInfo.CurrentCulture), 4, TextBrush, System.Windows.FontWeights.Normal);
            AddManagerCell(row, source.Action, 5, SoftTextBrush, System.Windows.FontWeights.Normal);
            border.Child = row;
            managerRowsPanel.Children.Add(border);
        }

        public void RefreshStatus()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { RefreshStatus(); }));
                return;
            }
            if (busy) { return; }

            bool autoInstalled = IsTaskInstalled(AutoTaskName);
            bool startupInstalled = IsTaskInstalled(TrayTaskName);
            autoModeActive = autoInstalled;
            startupModeActive = startupInstalled;

            autoValue.Text = autoInstalled ? "On" : "Off";
            autoValue.Foreground = autoInstalled ? GoodBrush : DangerBrush;
            startupValue.Text = startupInstalled ? "On" : "Off";
            startupValue.Foreground = startupInstalled ? GoodBrush : DangerBrush;
            lastRunValue.Text = GetLastRunText();
            resultValue.Text = BuildResultText();

            SetPill(statusPill, autoInstalled ? "Active" : "Manual", autoInstalled ? MakeBrush(20, 88, 60) : MakeBrush(31, 43, 60), autoInstalled ? GoodBrush : SoftTextBrush);
            SetPill(runStatePill, autoInstalled ? "MOTOR ATIVO" : "MOTOR PAUSADO", autoInstalled ? MakeBrush(20, 88, 60) : MakeBrush(78, 36, 35), autoInstalled ? GoodBrush : MakeBrush(255, 178, 170));
            actionTitle.Text = autoInstalled ? "Control Center" : "Manual Control";
            actionDetail.Text = BuildStatusDetail(autoInstalled, startupInstalled);
            actionProgress.IsIndeterminate = false;
            actionProgress.Value = 0;

            if (optimizeButton != null)
            {
                optimizeButton.Content = "Aplicar agora";
                optimizeButton.Background = AccentBrush;
                optimizeButton.BorderBrush = AccentBrush;
                optimizeButton.Foreground = MakeBrush(18, 20, 24);
                optimizeButton.IsEnabled = true;
            }
            if (motorButton != null)
            {
                motorButton.IsEnabled = true;
                motorButton.Content = autoInstalled ? "Pausar motor" : "Retomar motor";
                motorButton.Background = autoInstalled ? MakeBrush(21, 34, 53) : MakeBrush(20, 88, 60);
                motorButton.BorderBrush = autoInstalled ? BorderLineBrush : GoodBrush;
                motorButton.Foreground = autoInstalled ? TextBrush : GoodBrush;
            }
            if (moreButton != null)
            {
                moreButton.IsEnabled = true;
            }

            RefreshLiveManager();
        }

        private string BuildStatusDetail(bool autoInstalled, bool startupInstalled)
        {
            string line = ReadLastLogLine();
            if (line == "No log yet.")
            {
                return autoInstalled ? "Armed for each cycle. Foreground apps stay protected." : "Paused. Resume the motor or run a manual pass.";
            }
            return "Last pass: " + BuildResultText() + (startupInstalled ? " | tray active." : " | tray startup off.");
        }

        private string BuildResultText()
        {
            string line = ReadLastApplyLogLine();
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

            return line.Length > 32 ? line.Substring(0, 32) + "..." : line;
        }

        private void RefreshLiveManager()
        {
            if (managerRowsPanel == null || feedText == null)
            {
                return;
            }

            List<ManagerRow> rows = LoadManagerRows();
            managerRowsPanel.Children.Clear();
            for (int i = 0; i < rows.Count && i < 18; i++)
            {
                AddManagerRow(rows[i], i);
            }
            if (rows.Count == 0)
            {
                System.Windows.Controls.TextBlock empty = CreateText("No managed entries yet.", 12, System.Windows.FontWeights.Normal, SoftTextBrush);
                empty.Margin = new System.Windows.Thickness(10, 10, 0, 0);
                managerRowsPanel.Children.Add(empty);
            }
            managerStatus.Text = rows.Count == 0 ? "No score yet. Run a pass to populate live entries." : "Tracking latest pass: " + rows.Count.ToString(CultureInfo.CurrentCulture) + " managed entries.";
            RefreshEventFeed();
        }

        private void RefreshEventFeed()
        {
            if (feedText == null)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            if (!String.IsNullOrWhiteSpace(activeUiEventLine))
            {
                builder.AppendLine(activeUiEventLine);
            }
            else if (autoModeActive)
            {
                builder.AppendLine("WATCH motor automatico ativo; ciclos e foco protegidos");
            }

            List<string> lines = ReadLastLines(logPath, 12);
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                builder.AppendLine(FormatActivityLine(lines[i]));
            }
            feedText.Text = builder.ToString().TrimEnd();

            if (busy && !String.IsNullOrWhiteSpace(activeUiEventLine))
            {
                feedStatus.Text = "Current event is being tracked live.";
            }
            else if (!String.IsNullOrWhiteSpace(activeUiEventLine))
            {
                feedStatus.Text = "Most recent event is pinned above the log.";
            }
            else if (autoModeActive)
            {
                feedStatus.Text = "Background motor is active; log updates after each pass.";
            }
            else
            {
                feedStatus.Text = lines.Count == 0 ? "No activity yet." : "Latest event: " + FormatActivityTime(lines[lines.Count - 1]);
            }
        }

        private void RunUserAction(string activeMessage, string successMessage, Func<RunResult> action)
        {
            if (busy) { return; }

            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  NOW  " + CleanEventText(activeMessage);
            RefreshEventFeed();
            SetBusyState(true, activeMessage, "Working in the background...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                RunResult result = action();
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    string title = result.ExitCode == 0 ? successMessage : "Action failed";
                    string detail = result.ExitCode == 0 ? BuildResultText() : ShortError(result.Output);
                    activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + (result.ExitCode == 0 ? "  OK   " + CleanEventText(successMessage) : "  FAIL " + CleanEventText(ShortError(result.Output)));
                    busy = false;
                    RefreshStatus();
                    SetBusyState(false, title, detail);
                    RefreshLiveManager();
                    if (result.ExitCode != 0)
                    {
                        System.Windows.MessageBox.Show(ShortError(result.Output), AppName, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }));
            });
        }

        private void ToggleMotorFromButton()
        {
            if (busy) { return; }

            bool installed = IsTaskInstalled(AutoTaskName);
            autoModeActive = installed;
            RunUserAction(
                installed ? "Pausing background motor..." : "Starting background motor...",
                installed ? "Background motor paused." : "Background motor active.",
                installed ? (Func<RunResult>)UninstallAutomatic : InstallAutomatic);
        }

        private void RunOptimizeNowActionWithFeedback()
        {
            if (busy) { return; }

            RunControl control = new RunControl();
            activeRunControl = control;
            activeRunStartedAt = DateTime.Now;
            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  NOW  passe manual iniciado: prioridade, IO, memoria e EcoQoS";
            SetBusyState(true, "Agindo nos apps agora", "Em execucao ha 0s: prioridade, IO, memoria e EcoQoS.", true);
            if (actionTimer != null) { actionTimer.Start(); }
            RefreshEventFeed();

            ThreadPool.QueueUserWorkItem(delegate
            {
                RunResult result = RunApplyNow(control);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    bool stopped = result.ExitCode == 130;
                    string title = stopped ? "Otimizacao parada" : (result.ExitCode == 0 ? "Otimizacao concluida" : "Action failed");
                    string detail = stopped ? "O passe manual foi interrompido." : (result.ExitCode == 0 ? BuildResultText() : ShortError(result.Output));
                    if (actionTimer != null) { actionTimer.Stop(); }
                    activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + (stopped ? "  STOP passe manual interrompido" : (result.ExitCode == 0 ? "  OK   passe manual aplicado: " + BuildResultText() : "  FAIL passe manual falhou"));
                    activeRunControl = null;
                    busy = false;
                    RefreshStatus();
                    SetBusyState(false, title, detail);
                    if (stopped)
                    {
                        SetPill(runStatePill, "PARADO", MakeBrush(78, 36, 35), MakeBrush(255, 178, 170));
                    }
                    else if (result.ExitCode == 0)
                    {
                        SetPill(runStatePill, "ULTIMO PASSE OK", MakeBrush(20, 88, 60), GoodBrush);
                        actionProgress.IsIndeterminate = false;
                        actionProgress.Value = 100;
                    }
                    else
                    {
                        SetPill(runStatePill, "ERRO", MakeBrush(78, 36, 35), DangerBrush);
                    }

                    RefreshLiveManager();
                    if (result.ExitCode != 0 && !stopped)
                    {
                        System.Windows.MessageBox.Show(ShortError(result.Output), AppName, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }));
            });
        }

        private void StopCurrentActionWithFeedback()
        {
            if (!busy || activeRunControl == null)
            {
                return;
            }

            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  STOP solicitado pelo usuario";
            actionTitle.Text = "Parando otimizacao...";
            actionDetail.Text = "Encerrando o passe manual com seguranca.";
            SetPill(runStatePill, "PARANDO", MakeBrush(78, 36, 35), DangerBrush);
            optimizeButton.IsEnabled = false;
            RefreshEventFeed();
            activeRunControl.Cancel();
        }

        private void UpdateActiveRunVisuals()
        {
            if (!busy || activeRunControl == null)
            {
                return;
            }

            int seconds = Math.Max(0, (int)Math.Round((DateTime.Now - activeRunStartedAt).TotalSeconds));
            if (activeRunControl.CancelRequested)
            {
                actionTitle.Text = "Parando otimizacao...";
                actionDetail.Text = "Parada solicitada ha " + seconds.ToString(CultureInfo.CurrentCulture) + "s.";
                SetPill(runStatePill, "PARANDO", MakeBrush(78, 36, 35), DangerBrush);
                return;
            }

            actionTitle.Text = "Agindo nos apps agora";
            actionDetail.Text = "Em execucao ha " + seconds.ToString(CultureInfo.CurrentCulture) + "s: prioridade, IO, memoria e EcoQoS.";
            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  NOW  passe manual em execucao (" + seconds.ToString(CultureInfo.CurrentCulture) + "s)";
            SetPill(runStatePill, "AGINDO AGORA", MakeBrush(84, 54, 13), AccentBrush);
        }

        private void SetBusyState(bool isBusy, string title, string detail)
        {
            SetBusyState(isBusy, title, detail, false);
        }

        private void SetBusyState(bool isBusy, string title, string detail, bool canStop)
        {
            busy = isBusy;
            activeRunCanStop = isBusy && canStop;
            optimizeButton.IsEnabled = !isBusy || activeRunCanStop;
            optimizeButton.Content = activeRunCanStop ? "Parar" : "Aplicar agora";
            optimizeButton.Background = activeRunCanStop ? DangerBrush : AccentBrush;
            optimizeButton.BorderBrush = activeRunCanStop ? DangerBrush : AccentBrush;
            optimizeButton.Foreground = activeRunCanStop ? TextBrush : MakeBrush(18, 20, 24);
            if (motorButton != null) { motorButton.IsEnabled = !isBusy; }
            if (moreButton != null) { moreButton.IsEnabled = !isBusy; }
            actionTitle.Text = title;
            actionDetail.Text = detail;
            actionProgress.IsIndeterminate = isBusy;
            if (!isBusy) { actionProgress.Value = 0; }
            if (activeRunCanStop)
            {
                SetPill(runStatePill, "AGINDO AGORA", MakeBrush(84, 54, 13), AccentBrush);
            }
            else if (isBusy)
            {
                SetPill(runStatePill, "OCUPADO", MakeBrush(23, 37, 56), TextBrush);
            }
            else
            {
                SetPill(runStatePill, autoModeActive ? "MOTOR ATIVO" : "MOTOR PAUSADO", autoModeActive ? MakeBrush(20, 88, 60) : MakeBrush(78, 36, 35), autoModeActive ? GoodBrush : MakeBrush(255, 178, 170));
            }
        }

        private void ShowMoreMenu()
        {
            System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu();
            AddMenuItem(menu, autoModeActive ? "Pause background motor" : "Resume background motor", delegate { ToggleMotorFromButton(); });
            AddMenuItem(menu, startupModeActive ? "Disable tray startup" : "Enable tray startup", delegate
            {
                RunUserAction(
                    startupModeActive ? "Disabling startup..." : "Enabling startup...",
                    startupModeActive ? "Tray startup is off." : "The tray will start with Windows.",
                    startupModeActive ? (Func<RunResult>)UninstallStartup : InstallStartup);
            });
            menu.Items.Add(new System.Windows.Controls.Separator());
            AddMenuItem(menu, "Open log", delegate { OpenLog(); });
            AddMenuItem(menu, "Open config", delegate { OpenConfig(); });
            AddMenuItem(menu, "Open folder", delegate { OpenFolder(); });
            AddMenuItem(menu, "Nap score", delegate { OpenScore(); });
            AddMenuItem(menu, "Restore latest snapshot", delegate
            {
                if (System.Windows.MessageBox.Show("Restore the latest priority and throttling snapshot?", AppName, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes)
                {
                    RunUserAction("Restoring latest snapshot...", "Restore finished.", RunRestore);
                }
            });
            AddMenuItem(menu, "Safety report", delegate { OpenSafetyReport(); });
            AddMenuItem(menu, "Security model", delegate { OpenSecurityModel(); });
            AddMenuItem(menu, "GitHub", delegate { OpenGitHub(); });
            menu.PlacementTarget = moreButton;
            menu.IsOpen = true;
        }

        private void AddMenuItem(System.Windows.Controls.ContextMenu menu, string text, Action action)
        {
            System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem();
            item.Header = text;
            item.Click += delegate { action(); };
            menu.Items.Add(item);
        }

        private void StartDashboardActivity()
        {
            if (refreshTimer == null || liveTimer == null)
            {
                return;
            }
            if (!refreshTimer.IsEnabled) { refreshTimer.Start(); }
            if (!liveTimer.IsEnabled) { liveTimer.Start(); }
            SetPill(livePill, "Live on", MakeBrush(25, 73, 58), GoodBrush);
        }

        private void StopDashboardActivity()
        {
            if (refreshTimer != null) { refreshTimer.Stop(); }
            if (liveTimer != null) { liveTimer.Stop(); }
            SetPill(livePill, "Live paused", MakeBrush(24, 38, 56), SoftTextBrush);
        }

        private string CleanEventText(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                return "action";
            }
            text = text.Replace(Environment.NewLine, " ").Replace("\r", " ").Replace("\n", " ").Trim();
            while (text.EndsWith(".", StringComparison.Ordinal))
            {
                text = text.TrimEnd('.');
            }
            return text.Length > 120 ? text.Substring(0, 120) + "..." : text;
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

        private List<string> ReadLastLines(string path, int maxLines)
        {
            List<string> result = new List<string>();
            try
            {
                if (!File.Exists(path))
                {
                    return result;
                }
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int start = Math.Max(0, lines.Length - maxLines);
                for (int i = start; i < lines.Length; i++)
                {
                    if (!String.IsNullOrWhiteSpace(lines[i]))
                    {
                        result.Add(lines[i]);
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        private string FormatActivityLine(string line)
        {
            string action = ExtractLogValue(line, "action");
            string time = FormatActivityTime(line);
            if (String.Equals(action, "apply", StringComparison.OrdinalIgnoreCase))
            {
                string targets = ExtractLogValue(line, "targets");
                string delta = ExtractLogValue(line, "deltaMB");
                string top = ExtractLogValue(line, "top");
                string text = time + "  APPLY";
                if (!String.IsNullOrWhiteSpace(targets)) { text += "  " + targets + " apps"; }
                if (!String.IsNullOrWhiteSpace(delta)) { text += "  " + delta + " MB"; }
                if (!String.IsNullOrWhiteSpace(top)) { text += "  top " + top; }
                return text;
            }
            if (String.Equals(action, "foreground-restore", StringComparison.OrdinalIgnoreCase))
            {
                string process = ExtractLogValue(line, "process");
                string pid = ExtractLogValue(line, "pid");
                string text = time + "  WAKE";
                if (!String.IsNullOrWhiteSpace(process)) { text += "  " + process; }
                if (!String.IsNullOrWhiteSpace(pid)) { text += " #" + pid; }
                return text;
            }
            return line.Length > 120 ? line.Substring(0, 120) + "..." : line;
        }

        private string FormatActivityTime(string line)
        {
            if (String.IsNullOrWhiteSpace(line))
            {
                return "--:--:--";
            }
            int end = line.IndexOf(' ');
            string raw = end > 0 ? line.Substring(0, end) : line;
            DateTime parsed;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            }
            return raw.Length > 8 ? raw.Substring(raw.Length - 8) : raw;
        }

        private List<ManagerRow> LoadManagerRows()
        {
            List<ManagerRow> rows = new List<ManagerRow>();
            try
            {
                if (!File.Exists(scorePath))
                {
                    return rows;
                }
                string json = File.ReadAllText(scorePath, Encoding.UTF8);
                if (String.IsNullOrWhiteSpace(json))
                {
                    return rows;
                }
                IDictionary<string, object> root = JsonCompat.DeserializeObject(json);
                if (root == null)
                {
                    return rows;
                }
                object items;
                if (!root.TryGetValue("Items", out items) || items == null)
                {
                    return rows;
                }
                System.Collections.IEnumerable enumerable = items as System.Collections.IEnumerable;
                if (enumerable == null || items is string)
                {
                    return rows;
                }
                foreach (object item in enumerable)
                {
                    IDictionary<string, object> map = item as IDictionary<string, object>;
                    if (map == null)
                    {
                        continue;
                    }
                    ManagerRow row = new ManagerRow();
                    row.ProcessName = BuildProcessLabel(map);
                    row.Score = GetDouble(map, "Score");
                    row.DeltaMB = GetDouble(map, "DeltaMB");
                    row.CpuPercent = GetDouble(map, "CpuPercent");
                    row.BurstCount = GetInt(map, "BurstCount");
                    row.Action = BuildActionSummary(map);
                    row.Path = GetString(map, "Path");
                    rows.Add(row);
                }
                rows.Sort(delegate (ManagerRow left, ManagerRow right) { return right.Score.CompareTo(left.Score); });
            }
            catch
            {
            }
            return rows;
        }

        private string BuildProcessLabel(IDictionary<string, object> map)
        {
            string name = GetString(map, "ProcessName");
            if (String.IsNullOrWhiteSpace(name))
            {
                name = "Unknown";
            }
            int id = GetInt(map, "Id");
            return id > 0 ? name + " (" + id.ToString(CultureInfo.CurrentCulture) + ")" : name;
        }

        private string BuildActionSummary(IDictionary<string, object> map)
        {
            string priority = BlankToDash(GetString(map, "Priority"));
            string memory = BlankToDash(GetString(map, "MemoryPriority"));
            string io = BlankToDash(GetString(map, "IoPriority"));
            string trim = BlankToDash(GetString(map, "TrimWorkingSet"));
            string power = BlankToDash(GetString(map, "PowerThrottling"));
            return "P " + priority + " / M " + memory + " / IO " + io + " / T " + trim + " / Eco " + power;
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

        private static string GetString(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return "";
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                int parsed;
                return Int32.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
            }
        }

        private static double GetDouble(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }
            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                double parsed;
                return Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
            }
        }

        private static string BlankToDash(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string FormatDecimal(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
            {
                return "0.0";
            }
            return value.ToString("0.0", CultureInfo.CurrentCulture);
        }

        private sealed class ManagerRow
        {
            public string ProcessName;
            public double Score;
            public double DeltaMB;
            public double CpuPercent;
            public int BurstCount;
            public string Action;
            public string Path;
        }

        private sealed class ManagerDisplayRow
        {
            public string ProcessName { get; set; }
            public string ScoreText { get; set; }
            public string DeltaText { get; set; }
            public string CpuText { get; set; }
            public string BurstsText { get; set; }
            public string Action { get; set; }
        }
    }

    private sealed class MainWindow : Form
    {
        private static readonly Color ShellBack = Color.FromArgb(7, 11, 18);
        private static readonly Color SidebarBack = Color.FromArgb(10, 17, 29);
        private static readonly Color Surface = Color.FromArgb(15, 25, 40);
        private static readonly Color SurfaceSoft = Color.FromArgb(20, 33, 51);
        private static readonly Color SurfaceHot = Color.FromArgb(28, 43, 62);
        private static readonly Color Border = Color.FromArgb(44, 61, 83);
        private static readonly Color Accent = Color.FromArgb(255, 161, 43);
        private static readonly Color AccentBlue = Color.FromArgb(62, 140, 255);
        private static readonly Color Good = Color.FromArgb(38, 205, 126);
        private static readonly Color Warn = Color.FromArgb(255, 92, 92);
        private static readonly Color TextMain = Color.FromArgb(236, 243, 251);
        private static readonly Color TextSoft = Color.FromArgb(151, 165, 184);

        private Label autoValue;
        private Label startupValue;
        private Label lastRunValue;
        private Label resultValue;
        private Label statusPill;
        private Label livePill;
        private Label runStatePill;
        private Label actionTitle;
        private Label actionDetail;
        private Label managerStatus;
        private Label feedStatus;
        private CheckBox autoCheck;
        private CheckBox startupCheck;
        private Button optimizeButton;
        private Button motorButton;
        private Button moreButton;
        private SlimProgressBar actionProgress;
        private DataGridView managerGrid;
        private ListBox eventFeed;
        private RunControl activeRunControl;
        private bool activeRunCanStop;
        private DateTime activeRunStartedAt;
        private string activeUiEventLine;
        private bool autoModeActive;
        private bool startupModeActive;
        private bool loading;
        private bool busy;
        private System.Windows.Forms.Timer refreshTimer;
        private System.Windows.Forms.Timer liveTimer;
        private System.Windows.Forms.Timer actionTimer;

        public MainWindow()
        {
            Text = AppName;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            MinimumSize = new Size(1100, 680);
            Size = new Size(1240, 760);
            Icon = LoadIcon();
            DoubleBuffered = true;
            BuildLayout();

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 60000;
            refreshTimer.Tick += delegate { if (Visible && WindowState != FormWindowState.Minimized && !busy) { RefreshStatus(); } };

            liveTimer = new System.Windows.Forms.Timer();
            liveTimer.Interval = 2500;
            liveTimer.Tick += delegate { if (Visible && WindowState != FormWindowState.Minimized) { RefreshLiveManager(); } };

            actionTimer = new System.Windows.Forms.Timer();
            actionTimer.Interval = 120;
            actionTimer.Tick += delegate { UpdateActiveRunVisuals(); };
        }

        private void BuildLayout()
        {
            BackColor = ShellBack;
            Controls.Clear();
            Image brandLogo = LoadLogoImage();

            GlowPanel glow = new GlowPanel();
            glow.Dock = DockStyle.Fill;
            glow.Padding = new Padding(1);
            glow.BackColor = ShellBack;
            Controls.Add(glow);

            TableLayoutPanel frame = new TableLayoutPanel();
            frame.Dock = DockStyle.Fill;
            frame.RowCount = 2;
            frame.ColumnCount = 1;
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            frame.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            frame.BackColor = ShellBack;
            glow.Controls.Add(frame);

            Panel chrome = new Panel();
            chrome.Dock = DockStyle.Fill;
            chrome.BackColor = Color.FromArgb(5, 9, 15);
            chrome.MouseDown += DragWindow;
            frame.Controls.Add(chrome, 0, 0);

            LogoControl topLogo = new LogoControl();
            topLogo.Compact = false;
            topLogo.LogoImage = brandLogo;
            topLogo.Location = new Point(18, 7);
            topLogo.Size = new Size(178, 28);
            topLogo.MouseDown += DragWindow;
            chrome.Controls.Add(topLogo);

            FlowLayoutPanel windowButtons = new FlowLayoutPanel();
            windowButtons.FlowDirection = FlowDirection.LeftToRight;
            windowButtons.WrapContents = false;
            windowButtons.AutoSize = true;
            windowButtons.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            windowButtons.Location = new Point(Width - 92, 7);
            windowButtons.Resize += delegate { };
            chrome.Controls.Add(windowButtons);
            chrome.Resize += delegate
            {
                windowButtons.Location = new Point(chrome.Width - 88, 7);
            };
            windowButtons.Controls.Add(CreateWindowButton("_", delegate { WindowState = FormWindowState.Minimized; }));
            windowButtons.Controls.Add(CreateWindowButton("X", delegate { Close(); }));

            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.ColumnCount = 2;
            shell.RowCount = 1;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.BackColor = ShellBack;
            frame.Controls.Add(shell, 0, 1);

            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Fill;
            sidebar.BackColor = SidebarBack;
            sidebar.Padding = new Padding(12, 14, 12, 14);
            shell.Controls.Add(sidebar, 0, 0);

            FlowLayoutPanel nav = new FlowLayoutPanel();
            nav.Dock = DockStyle.Fill;
            nav.FlowDirection = FlowDirection.TopDown;
            nav.WrapContents = false;
            nav.BackColor = SidebarBack;
            sidebar.Controls.Add(nav);

            LogoControl mark = new LogoControl();
            mark.Compact = true;
            mark.LogoImage = brandLogo;
            mark.Size = new Size(58, 54);
            mark.Margin = new Padding(0, 0, 0, 16);
            nav.Controls.Add(mark);

            nav.Controls.Add(CreateNavButton("Home", null, true));
            nav.Controls.Add(CreateNavButton("Score", delegate { OpenScore(); }, false));
            nav.Controls.Add(CreateNavButton("Logs", delegate { OpenLog(); }, false));
            nav.Controls.Add(CreateNavButton("Files", delegate { OpenFolder(); }, false));
            nav.Controls.Add(CreateNavButton("Repo", delegate { OpenGitHub(); }, false));

            Label build = new Label();
            build.Text = "v" + AppVersion;
            build.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            build.ForeColor = Color.FromArgb(93, 107, 128);
            build.TextAlign = ContentAlignment.MiddleCenter;
            build.AutoSize = false;
            build.Width = 58;
            build.Height = 28;
            build.Margin = new Padding(0, 220, 0, 0);
            nav.Controls.Add(build);

            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(24, 18, 24, 20);
            content.BackColor = ShellBack;
            content.RowCount = 5;
            content.ColumnCount = 1;
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            shell.Controls.Add(content, 1, 0);

            TableLayoutPanel tabs = new TableLayoutPanel();
            tabs.Dock = DockStyle.Fill;
            tabs.ColumnCount = 2;
            tabs.RowCount = 1;
            tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tabs.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tabs.BackColor = ShellBack;
            content.Controls.Add(tabs, 0, 0);

            FlowLayoutPanel tabList = new FlowLayoutPanel();
            tabList.FlowDirection = FlowDirection.LeftToRight;
            tabList.WrapContents = false;
            tabList.AutoSize = true;
            tabList.Margin = new Padding(0, 3, 0, 0);
            tabList.Controls.Add(CreateTab("General", true));
            tabList.Controls.Add(CreateTab("Live", false));
            tabList.Controls.Add(CreateTab("Safety", false));
            tabs.Controls.Add(tabList, 0, 0);

            FlowLayoutPanel pills = new FlowLayoutPanel();
            pills.FlowDirection = FlowDirection.LeftToRight;
            pills.WrapContents = false;
            pills.AutoSize = true;
            pills.Margin = new Padding(0, 4, 0, 0);
            statusPill = CreatePill("Checking", SurfaceHot, TextMain);
            livePill = CreatePill("Live paused", SurfaceHot, TextSoft);
            pills.Controls.Add(statusPill);
            pills.Controls.Add(livePill);
            tabs.Controls.Add(pills, 1, 0);

            CardPanel hero = new CardPanel();
            hero.Dock = DockStyle.Fill;
            hero.Margin = new Padding(0, 0, 0, 14);
            hero.Padding = new Padding(20);
            hero.AccentColor = Accent;
            hero.Highlight = true;
            content.Controls.Add(hero, 0, 1);

            TableLayoutPanel heroGrid = new TableLayoutPanel();
            heroGrid.Dock = DockStyle.Fill;
            heroGrid.ColumnCount = 2;
            heroGrid.RowCount = 1;
            heroGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            heroGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 410));
            hero.Controls.Add(heroGrid);

            FlowLayoutPanel heroText = new FlowLayoutPanel();
            heroText.Dock = DockStyle.Fill;
            heroText.FlowDirection = FlowDirection.TopDown;
            heroText.WrapContents = false;
            heroGrid.Controls.Add(heroText, 0, 0);

            Label eyebrow = CreateHeroEyebrow("SMART BACKGROUND NAP");
            heroText.Controls.Add(eyebrow);

            Label title = new Label();
            title.Text = "Optimize background load";
            title.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            title.ForeColor = TextMain;
            title.AutoSize = true;
            title.Margin = new Padding(0, 8, 0, 0);
            heroText.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Keep open apps quieter while games and foreground work stay awake.";
            subtitle.Font = new Font("Segoe UI", 10);
            subtitle.ForeColor = TextSoft;
            subtitle.AutoSize = false;
            subtitle.Width = 590;
            subtitle.Height = 28;
            subtitle.Margin = new Padding(0, 5, 0, 14);
            heroText.Controls.Add(subtitle);

            FlowLayoutPanel chips = new FlowLayoutPanel();
            chips.FlowDirection = FlowDirection.LeftToRight;
            chips.WrapContents = false;
            chips.AutoSize = true;
            chips.Margin = new Padding(0, 2, 0, 0);
            chips.Controls.Add(CreateChip("CPU calm", AccentBlue));
            chips.Controls.Add(CreateChip("RAM relief", Accent));
            chips.Controls.Add(CreateChip("Wake restore", Good));
            chips.Controls.Add(CreateChip("Burst guard", Color.FromArgb(154, 111, 255)));
            heroText.Controls.Add(chips);

            CardPanel command = new CardPanel();
            command.Dock = DockStyle.Fill;
            command.Margin = new Padding(18, 0, 0, 0);
            command.Padding = new Padding(14);
            command.AccentColor = AccentBlue;
            command.Highlight = false;
            heroGrid.Controls.Add(command, 1, 0);

            FlowLayoutPanel commandFlow = new FlowLayoutPanel();
            commandFlow.Dock = DockStyle.Fill;
            commandFlow.FlowDirection = FlowDirection.TopDown;
            commandFlow.WrapContents = false;
            command.Controls.Add(commandFlow);

            actionTitle = new Label();
            actionTitle.Text = "Ready";
            actionTitle.Font = new Font("Segoe UI", 15, FontStyle.Bold);
            actionTitle.ForeColor = TextMain;
            actionTitle.AutoSize = true;
            commandFlow.Controls.Add(actionTitle);

            runStatePill = CreatePill("PRONTO", Color.FromArgb(23, 37, 56), TextSoft);
            runStatePill.Margin = new Padding(0, 4, 0, 0);
            commandFlow.Controls.Add(runStatePill);

            actionDetail = new Label();
            actionDetail.Text = "Waiting for the next automatic pass.";
            actionDetail.Font = new Font("Segoe UI", 9);
            actionDetail.ForeColor = TextSoft;
            actionDetail.AutoSize = false;
            actionDetail.Width = 350;
            actionDetail.Height = 40;
            actionDetail.Margin = new Padding(0, 6, 0, 8);
            commandFlow.Controls.Add(actionDetail);

            actionProgress = new SlimProgressBar();
            actionProgress.Width = 350;
            actionProgress.Height = 8;
            actionProgress.Style = ProgressBarStyle.Continuous;
            actionProgress.MarqueeAnimationSpeed = 0;
            actionProgress.Value = 0;
            actionProgress.Margin = new Padding(0, 0, 0, 10);
            commandFlow.Controls.Add(actionProgress);

            FlowLayoutPanel toggles = new FlowLayoutPanel();
            toggles.FlowDirection = FlowDirection.LeftToRight;
            toggles.WrapContents = false;
            toggles.AutoSize = true;
            toggles.Margin = new Padding(0, 8, 0, 0);
            autoCheck = CreateToggle("Automatic");
            autoCheck.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunUserAction(autoCheck.Checked ? "Enabling automatic mode..." : "Pausing automatic mode...",
                    autoCheck.Checked ? "Automatic mode is on." : "Automatic mode is paused.",
                    autoCheck.Checked ? (Func<RunResult>)InstallAutomatic : UninstallAutomatic);
            };
            toggles.Controls.Add(autoCheck);
            startupCheck = CreateToggle("Startup");
            startupCheck.CheckedChanged += delegate
            {
                if (loading) { return; }
                RunUserAction(startupCheck.Checked ? "Enabling startup..." : "Disabling startup...",
                    startupCheck.Checked ? "The tray will start with Windows." : "Tray startup is off.",
                    startupCheck.Checked ? (Func<RunResult>)InstallStartup : UninstallStartup);
            };
            toggles.Controls.Add(startupCheck);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            actions.AutoSize = true;
            actions.Margin = new Padding(0, 0, 0, 8);
            optimizeButton = CreateButton("Aplicar agora", delegate
            {
                if (busy && activeRunCanStop)
                {
                    StopCurrentActionWithFeedback();
                    return;
                }
                RunOptimizeNowActionWithFeedback();
            }, true, 140);
            actions.Controls.Add(optimizeButton);
            motorButton = CreateButton("Pausar motor", delegate { ToggleMotorFromButton(); }, false, 124);
            actions.Controls.Add(motorButton);
            moreButton = CreateButton("Mais", delegate { ShowMoreMenu(); }, false, 76);
            actions.Controls.Add(moreButton);
            commandFlow.Controls.Add(actions);

            TableLayoutPanel cards = new TableLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.ColumnCount = 4;
            cards.RowCount = 1;
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cards.Margin = new Padding(0, 0, 0, 14);
            autoValue = AddStatusCard(cards, 0, "Auto mode", AccentBlue);
            startupValue = AddStatusCard(cards, 1, "Startup", Good);
            lastRunValue = AddStatusCard(cards, 2, "Last pass", Accent);
            resultValue = AddStatusCard(cards, 3, "Last result", AccentBlue);
            content.Controls.Add(cards, 0, 2);

            TableLayoutPanel liveArea = new TableLayoutPanel();
            liveArea.Dock = DockStyle.Fill;
            liveArea.ColumnCount = 2;
            liveArea.RowCount = 1;
            liveArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
            liveArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            liveArea.Margin = new Padding(0, 0, 0, 10);
            content.Controls.Add(liveArea, 0, 3);

            Panel managerPanel = CreateSectionPanel();
            liveArea.Controls.Add(managerPanel, 0, 0);
            BuildManagerPanel(managerPanel);

            Panel feedPanel = CreateSectionPanel();
            feedPanel.Margin = new Padding(14, 0, 0, 0);
            liveArea.Controls.Add(feedPanel, 1, 0);
            BuildFeedPanel(feedPanel);

            Label footer = new Label();
            footer.Text = CreatorLine;
            footer.Font = new Font("Segoe UI", 8);
            footer.ForeColor = Color.FromArgb(92, 107, 129);
            footer.AutoSize = true;
            footer.Margin = new Padding(0, 4, 0, 0);
            content.Controls.Add(footer, 0, 4);
        }

        private void BuildManagerPanel(Panel panel)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.RowCount = 3;
            layout.ColumnCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(layout);

            Label title = new Label();
            title.Text = "Live Manager";
            title.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            title.ForeColor = TextMain;
            title.AutoSize = true;
            title.Margin = new Padding(0, 0, 0, 12);
            layout.Controls.Add(title, 0, 0);

            managerGrid = new DataGridView();
            managerGrid.Dock = DockStyle.Fill;
            managerGrid.BackgroundColor = SurfaceSoft;
            managerGrid.BorderStyle = BorderStyle.None;
            managerGrid.AllowUserToAddRows = false;
            managerGrid.AllowUserToDeleteRows = false;
            managerGrid.AllowUserToResizeRows = false;
            managerGrid.ReadOnly = true;
            managerGrid.MultiSelect = false;
            managerGrid.RowHeadersVisible = false;
            managerGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            managerGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            managerGrid.EnableHeadersVisualStyles = false;
            managerGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(24, 39, 58);
            managerGrid.ColumnHeadersDefaultCellStyle.ForeColor = TextMain;
            managerGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            managerGrid.DefaultCellStyle.BackColor = SurfaceSoft;
            managerGrid.DefaultCellStyle.ForeColor = TextMain;
            managerGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 82, 121);
            managerGrid.DefaultCellStyle.SelectionForeColor = Color.White;
            managerGrid.DefaultCellStyle.Font = new Font("Segoe UI", 9);
            managerGrid.GridColor = Border;
            managerGrid.RowTemplate.Height = 28;
            EnsureManagerColumns();
            layout.Controls.Add(managerGrid, 0, 1);

            managerStatus = new Label();
            managerStatus.Text = "Waiting for score data.";
            managerStatus.Font = new Font("Segoe UI", 9);
            managerStatus.ForeColor = TextSoft;
            managerStatus.AutoSize = true;
            managerStatus.Margin = new Padding(0, 10, 0, 0);
            layout.Controls.Add(managerStatus, 0, 2);
        }

        private void BuildFeedPanel(Panel panel)
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.RowCount = 3;
            layout.ColumnCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(layout);

            Label title = new Label();
            title.Text = "Event Stream";
            title.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            title.ForeColor = TextMain;
            title.AutoSize = true;
            title.Margin = new Padding(0, 0, 0, 12);
            layout.Controls.Add(title, 0, 0);

            eventFeed = new ListBox();
            eventFeed.Dock = DockStyle.Fill;
            eventFeed.BorderStyle = BorderStyle.None;
            eventFeed.BackColor = SurfaceSoft;
            eventFeed.ForeColor = TextMain;
            eventFeed.Font = new Font("Consolas", 9);
            eventFeed.IntegralHeight = false;
            layout.Controls.Add(eventFeed, 0, 1);

            feedStatus = new Label();
            feedStatus.Text = "No activity yet.";
            feedStatus.Font = new Font("Segoe UI", 9);
            feedStatus.ForeColor = TextSoft;
            feedStatus.AutoSize = true;
            feedStatus.Margin = new Padding(0, 10, 0, 0);
            layout.Controls.Add(feedStatus, 0, 2);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, 0xA1, new IntPtr(0x2), IntPtr.Zero);
        }

        private Button CreateWindowButton(string text, EventHandler handler)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 34;
            button.Height = 28;
            button.Margin = new Padding(0, 0, 4, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(5, 9, 15);
            button.ForeColor = TextSoft;
            button.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            button.Click += handler;
            return button;
        }

        private Label CreateHeroEyebrow(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            label.ForeColor = Accent;
            label.Margin = new Padding(0);
            return label;
        }

        private Label CreateTab(string text, bool active)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = new Font("Segoe UI", 10, active ? FontStyle.Bold : FontStyle.Regular);
            label.ForeColor = active ? Accent : TextSoft;
            label.Padding = new Padding(0, 6, 0, 8);
            label.Margin = new Padding(0, 0, 26, 0);
            return label;
        }

        private Label CreateChip(string text, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            label.ForeColor = TextMain;
            label.BackColor = Color.FromArgb(28, 42, 62);
            label.Padding = new Padding(10, 5, 10, 5);
            label.Margin = new Padding(0, 0, 8, 0);
            return label;
        }

        private Panel CreateSectionPanel()
        {
            CardPanel panel = new CardPanel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(16);
            panel.Margin = new Padding(0);
            panel.AccentColor = AccentBlue;
            panel.Highlight = false;
            return panel;
        }

        private Button CreateNavButton(string text, EventHandler handler, bool active)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = 58;
            button.Height = 44;
            button.Margin = new Padding(0, 0, 0, 10);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = active ? Color.FromArgb(89, 67, 31) : Color.FromArgb(20, 31, 48);
            button.BackColor = active ? Color.FromArgb(36, 30, 20) : Color.FromArgb(11, 18, 30);
            button.ForeColor = active ? Accent : TextSoft;
            button.Font = new Font("Segoe UI", 8, active ? FontStyle.Bold : FontStyle.Regular);
            if (handler != null)
            {
                button.Click += handler;
            }
            return button;
        }

        private Label CreatePill(string text, Color backColor, Color foreColor)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            label.ForeColor = foreColor;
            label.BackColor = backColor;
            label.Padding = new Padding(11, 6, 11, 6);
            label.Margin = new Padding(8, 0, 0, 0);
            return label;
        }

        private CheckBox CreateToggle(string text)
        {
            CheckBox check = new CheckBox();
            check.Text = text;
            check.AutoSize = true;
            check.Font = new Font("Segoe UI", 9);
            check.ForeColor = TextMain;
            check.BackColor = Surface;
            check.Margin = new Padding(0, 0, 18, 0);
            return check;
        }

        private void ShowMoreMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(autoModeActive ? "Pause background motor" : "Resume background motor", null, delegate { ToggleMotorFromButton(); });
            menu.Items.Add(startupModeActive ? "Disable tray startup" : "Enable tray startup", null, delegate
            {
                RunUserAction(
                    startupModeActive ? "Disabling startup..." : "Enabling startup...",
                    startupModeActive ? "Tray startup is off." : "The tray will start with Windows.",
                    startupModeActive ? (Func<RunResult>)UninstallStartup : InstallStartup);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Open log", null, delegate { OpenLog(); });
            menu.Items.Add("Open config", null, delegate { OpenConfig(); });
            menu.Items.Add("Open folder", null, delegate { OpenFolder(); });
            menu.Items.Add("README", null, delegate { OpenReadme(); });
            menu.Items.Add("Nap score", null, delegate { OpenScore(); });
            menu.Items.Add("Restore latest snapshot", null, delegate
            {
                DialogResult confirm = MessageBox.Show("Restore the latest priority and throttling snapshot for currently running processes?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes)
                {
                    RunUserAction("Restoring latest snapshot...", "Restore finished.", RunRestore);
                }
            });
            menu.Items.Add("Safety report", null, delegate { OpenSafetyReport(); });
            menu.Items.Add("Security model", null, delegate { OpenSecurityModel(); });
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

        private Label AddStatusCard(TableLayoutPanel parent, int column, string caption, Color accentColor)
        {
            CardPanel panel = new CardPanel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(column == 0 ? 0 : 8, 0, column == 3 ? 0 : 8, 0);
            panel.Padding = new Padding(14);
            panel.AccentColor = accentColor;
            panel.Highlight = false;

            Panel accent = new Panel();
            accent.BackColor = accentColor;
            accent.Dock = DockStyle.Left;
            accent.Width = 4;
            panel.Controls.Add(accent);

            Label title = new Label();
            title.Text = caption;
            title.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            title.ForeColor = TextSoft;
            title.AutoSize = true;
            title.Location = new Point(18, 13);
            panel.Controls.Add(title);

            Label value = new Label();
            value.Text = "...";
            value.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            value.ForeColor = TextMain;
            value.Location = new Point(18, 40);
            value.Size = new Size(230, 46);
            value.AutoEllipsis = true;
            panel.Controls.Add(value);

            parent.Controls.Add(panel, column, 0);
            return value;
        }

        private Button CreateButton(string text, EventHandler handler, bool primary, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Font = new Font("Segoe UI", 9, primary ? FontStyle.Bold : FontStyle.Regular);
            button.Width = width;
            button.Height = 38;
            button.Margin = new Padding(0, 0, 6, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.BackColor = primary ? Accent : Color.FromArgb(23, 37, 56);
            button.ForeColor = primary ? Color.FromArgb(18, 20, 24) : TextMain;
            button.Click += handler;
            return button;
        }

        private void AddManagerColumn(string header, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.HeaderText = header;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            managerGrid.Columns.Add(column);
        }

        private void EnsureManagerColumns()
        {
            if (managerGrid == null || managerGrid.Columns.Count > 0)
            {
                return;
            }

            AddManagerColumn("Process", 145);
            AddManagerColumn("Score", 58);
            AddManagerColumn("Delta MB", 78);
            AddManagerColumn("CPU %", 64);
            AddManagerColumn("Bursts", 58);
            AddManagerColumn("Action", 175);
        }

        public void RefreshStatus()
        {
            if (busy) { return; }
            loading = true;
            bool autoInstalled = IsTaskInstalled(AutoTaskName);
            bool startupInstalled = IsTaskInstalled(TrayTaskName);
            autoModeActive = autoInstalled;
            startupModeActive = startupInstalled;

            autoValue.Text = autoInstalled ? "On" : "Off";
            autoValue.ForeColor = autoInstalled ? Good : Warn;

            startupValue.Text = startupInstalled ? "On" : "Off";
            startupValue.ForeColor = startupInstalled ? Good : Warn;

            lastRunValue.Text = GetLastRunText();
            resultValue.Text = BuildResultText();
            resultValue.ForeColor = TextMain;

            statusPill.Text = autoInstalled ? "Active" : "Manual";
            statusPill.BackColor = autoInstalled ? Color.FromArgb(20, 88, 60) : SurfaceHot;
            statusPill.ForeColor = autoInstalled ? Good : TextSoft;
            actionTitle.Text = autoInstalled ? "Motor active" : "Manual mode";
            actionDetail.Text = BuildStatusDetail(autoInstalled, startupInstalled);
            actionProgress.Style = ProgressBarStyle.Continuous;
            actionProgress.MarqueeAnimationSpeed = 0;
            actionProgress.Value = 0;
            SetRunStatePill(
                autoInstalled ? "MOTOR ATIVO" : "MOTOR PAUSADO",
                autoInstalled ? Color.FromArgb(20, 88, 60) : Color.FromArgb(78, 36, 35),
                autoInstalled ? Good : Color.FromArgb(255, 178, 170));
            if (optimizeButton != null)
            {
                optimizeButton.Text = "Aplicar agora";
                optimizeButton.BackColor = Accent;
                optimizeButton.ForeColor = Color.FromArgb(18, 20, 24);
            }
            if (motorButton != null)
            {
                motorButton.Enabled = true;
                motorButton.Text = autoInstalled ? "Pausar motor" : "Retomar motor";
                motorButton.BackColor = autoInstalled ? Color.FromArgb(23, 37, 56) : Color.FromArgb(20, 88, 60);
                motorButton.ForeColor = autoInstalled ? TextMain : Good;
            }

            autoCheck.Checked = autoInstalled;
            startupCheck.Checked = startupInstalled;
            loading = false;
            RefreshLiveManager();
        }

        private string BuildStatusDetail(bool autoInstalled, bool startupInstalled)
        {
            string line = ReadLastLogLine();
            if (line == "No log yet.")
            {
                return autoInstalled ? "Motor armado: passa a cada ciclo, protege o app em foco e registra tudo no Event Stream." : "Motor pausado. Use Retomar motor ou aplique um passe manual.";
            }
            return "Ultimo passe: " + BuildResultText() + (startupInstalled ? " | tray ativo." : " | tray startup off.");
        }

        private string BuildResultText()
        {
            string line = ReadLastApplyLogLine();
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

        private void RefreshLiveManager()
        {
            List<ManagerRow> rows = LoadManagerRows();
            EnsureManagerColumns();
            managerGrid.Rows.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                ManagerRow row = rows[i];
                int index = managerGrid.Rows.Add(
                    row.ProcessName,
                    FormatDecimal(row.Score),
                    FormatDecimal(row.DeltaMB),
                    FormatDecimal(row.CpuPercent),
                    row.BurstCount.ToString(CultureInfo.CurrentCulture),
                    row.Action);
                DataGridViewRow gridRow = managerGrid.Rows[index];
                if (row.Score >= 100)
                {
                    gridRow.DefaultCellStyle.BackColor = Color.FromArgb(21, 48, 42);
                }
                if (!String.IsNullOrWhiteSpace(row.Path))
                {
                    gridRow.Cells[0].ToolTipText = row.Path;
                }
            }

            managerStatus.Text = rows.Count == 0 ? "No score yet. Run Optimize now to populate the manager." : "Tracking latest pass: " + rows.Count.ToString(CultureInfo.CurrentCulture) + " managed entries.";
            RefreshEventFeed();
        }

        private void RefreshEventFeed()
        {
            eventFeed.BeginUpdate();
            eventFeed.Items.Clear();
            if (!String.IsNullOrWhiteSpace(activeUiEventLine))
            {
                eventFeed.Items.Add(activeUiEventLine);
            }
            else if (autoModeActive)
            {
                eventFeed.Items.Add("WATCH motor automatico ativo; ciclos e foco protegidos");
            }
            List<string> lines = ReadLastLines(logPath, 12);
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                eventFeed.Items.Add(FormatActivityLine(lines[i]));
            }
            eventFeed.EndUpdate();
            if (busy && !String.IsNullOrWhiteSpace(activeUiEventLine))
            {
                feedStatus.Text = "Current event is being tracked live.";
            }
            else if (!String.IsNullOrWhiteSpace(activeUiEventLine))
            {
                feedStatus.Text = "Most recent UI event is pinned above the log.";
            }
            else if (autoModeActive)
            {
                feedStatus.Text = "Background motor is active; log updates after each pass.";
            }
            else
            {
                feedStatus.Text = lines.Count == 0 ? "No activity yet." : "Latest event: " + FormatActivityTime(lines[lines.Count - 1]);
            }
        }

        private List<string> ReadLastLines(string path, int maxLines)
        {
            List<string> result = new List<string>();
            try
            {
                if (!File.Exists(path))
                {
                    return result;
                }

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int start = Math.Max(0, lines.Length - maxLines);
                for (int i = start; i < lines.Length; i++)
                {
                    if (!String.IsNullOrWhiteSpace(lines[i]))
                    {
                        result.Add(lines[i]);
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        private string FormatActivityLine(string line)
        {
            string action = ExtractLogValue(line, "action");
            string time = FormatActivityTime(line);
            if (String.Equals(action, "apply", StringComparison.OrdinalIgnoreCase))
            {
                string targets = ExtractLogValue(line, "targets");
                string delta = ExtractLogValue(line, "deltaMB");
                string top = ExtractLogValue(line, "top");
                string score = ExtractLogValue(line, "score");
                string text = time + "  APPLY";
                if (!String.IsNullOrWhiteSpace(targets)) { text += "  " + targets + " apps"; }
                if (!String.IsNullOrWhiteSpace(delta)) { text += "  " + delta + " MB"; }
                if (!String.IsNullOrWhiteSpace(top)) { text += "  top " + top; }
                if (!String.IsNullOrWhiteSpace(score)) { text += " (" + score + ")"; }
                return text;
            }
            if (String.Equals(action, "foreground-restore", StringComparison.OrdinalIgnoreCase))
            {
                string process = ExtractLogValue(line, "process");
                string pid = ExtractLogValue(line, "pid");
                string priority = ExtractLogValue(line, "priority");
                string io = ExtractLogValue(line, "io");
                string text = time + "  WAKE";
                if (!String.IsNullOrWhiteSpace(process)) { text += "  " + process; }
                if (!String.IsNullOrWhiteSpace(pid)) { text += " #" + pid; }
                if (!String.IsNullOrWhiteSpace(priority)) { text += "  P:" + priority; }
                if (!String.IsNullOrWhiteSpace(io)) { text += "  IO:" + io; }
                return text;
            }
            return line.Length > 120 ? line.Substring(0, 120) + "..." : line;
        }

        private string FormatActivityTime(string line)
        {
            if (String.IsNullOrWhiteSpace(line))
            {
                return "--:--:--";
            }

            int end = line.IndexOf(' ');
            string raw = end > 0 ? line.Substring(0, end) : line;
            DateTime parsed;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            }
            return raw.Length > 8 ? raw.Substring(raw.Length - 8) : raw;
        }

        private List<ManagerRow> LoadManagerRows()
        {
            List<ManagerRow> rows = new List<ManagerRow>();
            try
            {
                if (!File.Exists(scorePath))
                {
                    return rows;
                }

                string json = File.ReadAllText(scorePath, Encoding.UTF8);
                if (String.IsNullOrWhiteSpace(json))
                {
                    return rows;
                }

                IDictionary<string, object> root = JsonCompat.DeserializeObject(json);
                if (root == null)
                {
                    return rows;
                }

                object items;
                if (!root.TryGetValue("Items", out items) || items == null)
                {
                    return rows;
                }

                System.Collections.IEnumerable enumerable = items as System.Collections.IEnumerable;
                if (enumerable == null || items is string)
                {
                    return rows;
                }

                foreach (object item in enumerable)
                {
                    IDictionary<string, object> map = item as IDictionary<string, object>;
                    if (map == null)
                    {
                        continue;
                    }

                    ManagerRow row = new ManagerRow();
                    row.ProcessName = BuildProcessLabel(map);
                    row.Score = GetDouble(map, "Score");
                    row.DeltaMB = GetDouble(map, "DeltaMB");
                    row.CpuPercent = GetDouble(map, "CpuPercent");
                    row.BurstCount = GetInt(map, "BurstCount");
                    row.Action = BuildActionSummary(map);
                    row.Path = GetString(map, "Path");
                    rows.Add(row);
                }

                rows.Sort(delegate (ManagerRow left, ManagerRow right)
                {
                    return right.Score.CompareTo(left.Score);
                });
            }
            catch
            {
            }
            return rows;
        }

        private string BuildProcessLabel(IDictionary<string, object> map)
        {
            string name = GetString(map, "ProcessName");
            if (String.IsNullOrWhiteSpace(name))
            {
                name = "Unknown";
            }

            int id = GetInt(map, "Id");
            return id > 0 ? name + " (" + id.ToString(CultureInfo.CurrentCulture) + ")" : name;
        }

        private string BuildActionSummary(IDictionary<string, object> map)
        {
            string priority = BlankToDash(GetString(map, "Priority"));
            string memory = BlankToDash(GetString(map, "MemoryPriority"));
            string io = BlankToDash(GetString(map, "IoPriority"));
            string trim = BlankToDash(GetString(map, "TrimWorkingSet"));
            string power = BlankToDash(GetString(map, "PowerThrottling"));
            string text = "P " + priority + " / M " + memory + " / IO " + io + " / T " + trim + " / Eco " + power;
            if (GetBool(map, "ForegroundFullscreen"))
            {
                text += " / protected";
            }
            return text;
        }

        private static string GetString(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return "";
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                int parsed;
                return Int32.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
            }
        }

        private static double GetDouble(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                double parsed;
                return Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
            }
        }

        private static bool GetBool(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return false;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            bool parsed;
            return Boolean.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) && parsed;
        }

        private static string BlankToDash(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string FormatDecimal(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
            {
                return "0.0";
            }
            return value.ToString("0.0", CultureInfo.CurrentCulture);
        }

        private void SetBusyState(bool isBusy, string title, string detail)
        {
            SetBusyState(isBusy, title, detail, false);
        }

        private void SetBusyState(bool isBusy, string title, string detail, bool canStop)
        {
            busy = isBusy;
            activeRunCanStop = isBusy && canStop;
            optimizeButton.Enabled = !isBusy || activeRunCanStop;
            optimizeButton.Text = activeRunCanStop ? "Parar" : "Aplicar agora";
            optimizeButton.BackColor = activeRunCanStop ? Warn : Accent;
            optimizeButton.ForeColor = activeRunCanStop ? Color.White : Color.FromArgb(18, 20, 24);
            if (motorButton != null) { motorButton.Enabled = !isBusy; }
            if (moreButton != null) { moreButton.Enabled = !isBusy; }
            if (autoCheck != null) { autoCheck.Enabled = !isBusy; }
            if (startupCheck != null) { startupCheck.Enabled = !isBusy; }
            actionTitle.Text = title;
            actionDetail.Text = detail;
            actionProgress.Style = isBusy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            actionProgress.MarqueeAnimationSpeed = isBusy ? 24 : 0;
            if (!isBusy) { actionProgress.Value = 0; }
            if (runStatePill != null)
            {
                if (activeRunCanStop)
                {
                    SetRunStatePill("AGINDO AGORA", Color.FromArgb(84, 54, 13), Accent);
                }
                else if (isBusy)
                {
                    SetRunStatePill("OCUPADO", Color.FromArgb(23, 37, 56), TextMain);
                }
                else
                {
                    SetRunStatePill("PRONTO", Color.FromArgb(23, 37, 56), TextSoft);
                }
            }
        }

        private void SetRunStatePill(string text, Color backColor, Color foreColor)
        {
            if (runStatePill == null)
            {
                return;
            }

            runStatePill.Text = text;
            runStatePill.BackColor = backColor;
            runStatePill.ForeColor = foreColor;
        }

        private void RunUserAction(string activeMessage, string successMessage, Func<RunResult> action)
        {
            if (busy) { return; }

            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  NOW  " + CleanEventText(activeMessage);
            RefreshEventFeed();
            SetBusyState(true, activeMessage, "Working in the background...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                RunResult result = action();
                BeginInvoke(new System.Windows.Forms.MethodInvoker(delegate
                {
                    string title = result.ExitCode == 0 ? successMessage : "Action failed";
                    string detail = result.ExitCode == 0 ? BuildResultText() : ShortError(result.Output);
                    activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + (result.ExitCode == 0 ? "  OK   " + CleanEventText(successMessage) : "  FAIL " + CleanEventText(ShortError(result.Output)));
                    busy = false;
                    RefreshStatus();
                    SetBusyState(false, title, detail);
                    RefreshLiveManager();
                    if (result.ExitCode != 0)
                    {
                        MessageBox.Show(ShortError(result.Output), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }));
            });
        }

        private string CleanEventText(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                return "action";
            }

            text = text.Replace(Environment.NewLine, " ").Replace("\r", " ").Replace("\n", " ").Trim();
            while (text.EndsWith(".", StringComparison.Ordinal) || text.EndsWith("...", StringComparison.Ordinal))
            {
                text = text.TrimEnd('.');
            }
            return text.Length > 120 ? text.Substring(0, 120) + "..." : text;
        }

        private void ToggleMotorFromButton()
        {
            if (busy)
            {
                return;
            }

            bool installed = IsTaskInstalled(AutoTaskName);
            autoModeActive = installed;
            RunUserAction(
                installed ? "Pausing background motor..." : "Starting background motor...",
                installed ? "Background motor paused." : "Background motor active.",
                installed ? (Func<RunResult>)UninstallAutomatic : InstallAutomatic);
        }

        private void RunOptimizeNowAction()
        {
            if (busy) { return; }

            RunControl control = new RunControl();
            activeRunControl = control;
            SetBusyState(true, "Otimizando agora...", "Aplicando um passe manual nos apps em segundo plano.", true);
            ThreadPool.QueueUserWorkItem(delegate
            {
                RunResult result = RunApplyNow(control);
                BeginInvoke(new System.Windows.Forms.MethodInvoker(delegate
                {
                    bool stopped = result.ExitCode == 130;
                    string title = stopped ? "OtimizaÃ§Ã£o parada" : (result.ExitCode == 0 ? "OtimizaÃ§Ã£o concluÃ­da" : "Action failed");
                    string detail = stopped ? "O passe manual foi interrompido." : (result.ExitCode == 0 ? BuildResultText() : ShortError(result.Output));
                    activeRunControl = null;
                    busy = false;
                    RefreshStatus();
                    SetBusyState(false, title, detail);
                    RefreshLiveManager();
                    if (result.ExitCode != 0 && !stopped)
                    {
                        MessageBox.Show(ShortError(result.Output), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }));
            });
        }

        private void StopCurrentAction()
        {
            if (!busy || activeRunControl == null)
            {
                return;
            }

            actionTitle.Text = "Parando otimizaÃ§Ã£o...";
            actionDetail.Text = "Encerrando o passe manual com seguranÃ§a.";
            optimizeButton.Enabled = false;
            activeRunControl.Cancel();
        }

        private void RunOptimizeNowActionWithFeedback()
        {
            if (busy) { return; }

            RunControl control = new RunControl();
            activeRunControl = control;
            activeRunStartedAt = DateTime.Now;
            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  NOW  passe manual iniciado: prioridade, IO, memoria e EcoQoS";
            SetBusyState(true, "Agindo nos apps agora", "Passe manual iniciado ha 0s. Ajustando apps em segundo plano.", true);
            if (actionTimer != null) { actionTimer.Start(); }
            RefreshEventFeed();

            ThreadPool.QueueUserWorkItem(delegate
            {
                RunResult result = RunApplyNow(control);
                BeginInvoke(new System.Windows.Forms.MethodInvoker(delegate
                {
                    bool stopped = result.ExitCode == 130;
                    string title = stopped ? "Otimizacao parada" : (result.ExitCode == 0 ? "Otimizacao concluida" : "Action failed");
                    string detail = stopped ? "O passe manual foi interrompido." : (result.ExitCode == 0 ? BuildResultText() : ShortError(result.Output));
                    if (actionTimer != null) { actionTimer.Stop(); }
                    activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + (stopped ? "  STOP passe manual interrompido" : (result.ExitCode == 0 ? "  OK   passe manual aplicado: " + BuildResultText() : "  FAIL passe manual falhou"));
                    activeRunControl = null;
                    busy = false;
                    RefreshStatus();
                    SetBusyState(false, title, detail);

                    if (stopped)
                    {
                        SetRunStatePill("PARADO", Color.FromArgb(78, 36, 35), Color.FromArgb(255, 178, 170));
                    }
                    else if (result.ExitCode == 0)
                    {
                        SetRunStatePill("ULTIMO PASSE OK", Color.FromArgb(20, 88, 60), Good);
                        actionProgress.Value = 100;
                    }
                    else
                    {
                        SetRunStatePill("ERRO", Color.FromArgb(78, 36, 35), Warn);
                    }

                    RefreshLiveManager();
                    if (result.ExitCode != 0 && !stopped)
                    {
                        MessageBox.Show(ShortError(result.Output), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }));
            });
        }

        private void StopCurrentActionWithFeedback()
        {
            if (!busy || activeRunControl == null)
            {
                return;
            }

            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  STOP solicitado pelo usuario";
            actionTitle.Text = "Parando otimizacao...";
            actionDetail.Text = "Encerrando o passe manual com seguranca.";
            SetRunStatePill("PARANDO", Color.FromArgb(78, 36, 35), Warn);
            optimizeButton.Enabled = false;
            RefreshEventFeed();
            activeRunControl.Cancel();
        }

        private void UpdateActiveRunVisuals()
        {
            if (!busy || activeRunControl == null)
            {
                return;
            }

            int seconds = Math.Max(0, (int)Math.Round((DateTime.Now - activeRunStartedAt).TotalSeconds));
            if (activeRunControl.CancelRequested)
            {
                actionTitle.Text = "Parando otimizacao...";
                actionDetail.Text = "Parada solicitada ha " + seconds.ToString(CultureInfo.CurrentCulture) + "s.";
                SetRunStatePill("PARANDO", Color.FromArgb(78, 36, 35), Warn);
                return;
            }

            actionTitle.Text = "Agindo nos apps agora";
            actionDetail.Text = "Em execucao ha " + seconds.ToString(CultureInfo.CurrentCulture) + "s: prioridade, IO, memoria e EcoQoS.";
            actionProgress.Value = (int)(((DateTime.Now - activeRunStartedAt).TotalMilliseconds / 24.0) % 100);
            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  NOW  passe manual em execucao (" + seconds.ToString(CultureInfo.CurrentCulture) + "s)";
            SetRunStatePill("AGINDO AGORA", Color.FromArgb(84, 54, 13), Accent);
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

        private void StartDashboardActivity()
        {
            if (refreshTimer == null || liveTimer == null)
            {
                return;
            }
            if (!refreshTimer.Enabled) { refreshTimer.Start(); }
            if (!liveTimer.Enabled) { liveTimer.Start(); }
            livePill.Text = "Live on";
            livePill.BackColor = Color.FromArgb(25, 73, 58);
            livePill.ForeColor = Good;
        }

        private void StopDashboardActivity()
        {
            if (refreshTimer != null) { refreshTimer.Stop(); }
            if (liveTimer != null) { liveTimer.Stop(); }
            if (livePill != null)
            {
                livePill.Text = "Live paused";
                livePill.BackColor = SurfaceHot;
                livePill.ForeColor = TextSoft;
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && WindowState != FormWindowState.Minimized)
            {
                StartDashboardActivity();
                RefreshStatus();
                RefreshLiveManager();
            }
            else
            {
                StopDashboardActivity();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                StopDashboardActivity();
                Hide();
                WindowState = FormWindowState.Normal;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            StartDashboardActivity();
            RefreshStatus();
            RefreshLiveManager();
        }

        private sealed class ManagerRow
        {
            public string ProcessName;
            public double Score;
            public double DeltaMB;
            public double CpuPercent;
            public int BurstCount;
            public string Action;
            public string Path;
        }
    }

    private sealed class GlowPanel : Panel
    {
        public GlowPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using (LinearGradientBrush brush = new LinearGradientBrush(bounds, Color.FromArgb(4, 8, 14), Color.FromArgb(13, 20, 31), LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen hot = new Pen(Color.FromArgb(230, 255, 176, 54), 2))
            using (Pen soft = new Pen(Color.FromArgb(80, 255, 176, 54), 10))
            {
                Rectangle glow = new Rectangle(4, 4, bounds.Width - 9, bounds.Height - 9);
                e.Graphics.DrawRectangle(soft, glow);
                e.Graphics.DrawRectangle(hot, 1, 1, bounds.Width - 3, bounds.Height - 3);
            }
        }
    }

    private sealed class CardPanel : Panel
    {
        public Color AccentColor = Color.FromArgb(255, 161, 43);
        public bool Highlight;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            using (GraphicsPath path = RoundedRect(rect, 10))
            using (LinearGradientBrush fill = new LinearGradientBrush(rect, Color.FromArgb(18, 29, 45), Color.FromArgb(10, 17, 28), LinearGradientMode.ForwardDiagonal))
            using (Pen border = new Pen(Color.FromArgb(43, 61, 83), 1))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }

            if (Highlight)
            {
                Rectangle glowRect = new Rectangle(12, 10, Math.Max(20, Width - 24), Math.Max(20, Height - 20));
                using (GraphicsPath glowPath = RoundedRect(glowRect, 12))
                using (PathGradientBrush glow = new PathGradientBrush(glowPath))
                {
                    glow.CenterColor = Color.FromArgb(70, AccentColor);
                    glow.SurroundColors = new Color[] { Color.FromArgb(0, AccentColor) };
                    e.Graphics.FillPath(glow, glowPath);
                }
            }

            using (Pen accent = new Pen(Color.FromArgb(210, AccentColor), 2))
            {
                e.Graphics.DrawLine(accent, 14, 1, Math.Min(180, Width - 16), 1);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private sealed class LogoControl : Control
    {
        public bool Compact;
        public Image LogoImage;

        public LogoControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (LogoImage != null)
            {
                Rectangle imageRect = Compact
                    ? new Rectangle(0, 0, Width, Height)
                    : new Rectangle(0, 1, 28, 26);
                DrawImageContain(e.Graphics, LogoImage, imageRect);

                if (!Compact)
                {
                    using (Font word = new Font("Segoe UI", 10, FontStyle.Bold))
                    using (Brush main = new SolidBrush(Color.FromArgb(239, 245, 252)))
                    using (Brush accent = new SolidBrush(Color.FromArgb(255, 176, 54)))
                    {
                        e.Graphics.DrawString("SMART", word, main, 36, 2);
                        e.Graphics.DrawString("NAP", word, accent, 91, 2);
                    }
                }
                return;
            }

            Rectangle mark = new Rectangle(2, Compact ? 5 : 2, Compact ? Math.Min(50, Width - 4) : 28, Compact ? Math.Min(44, Height - 8) : 24);
            DrawMark(e.Graphics, mark);

            if (!Compact)
            {
                using (Font word = new Font("Segoe UI", 10, FontStyle.Bold))
                using (Brush main = new SolidBrush(Color.FromArgb(239, 245, 252)))
                using (Brush accent = new SolidBrush(Color.FromArgb(255, 176, 54)))
                {
                    e.Graphics.DrawString("SMART", word, main, 36, 2);
                    e.Graphics.DrawString("NAP", word, accent, 91, 2);
                }
            }
        }

        private static void DrawImageContain(Graphics g, Image image, Rectangle bounds)
        {
            if (image == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            float scale = Math.Min(bounds.Width / (float)image.Width, bounds.Height / (float)image.Height);
            int width = Math.Max(1, (int)Math.Round(image.Width * scale));
            int height = Math.Max(1, (int)Math.Round(image.Height * scale));
            Rectangle dest = new Rectangle(
                bounds.X + (bounds.Width - width) / 2,
                bounds.Y + (bounds.Height - height) / 2,
                width,
                height);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(image, dest);
        }

        private static void DrawMark(Graphics g, Rectangle r)
        {
            PointF[] orange = new PointF[]
            {
                new PointF(r.Left + r.Width * 0.54f, r.Top + r.Height * 0.02f),
                new PointF(r.Left + r.Width * 0.18f, r.Top + r.Height * 0.52f),
                new PointF(r.Left + r.Width * 0.45f, r.Top + r.Height * 0.48f),
                new PointF(r.Left + r.Width * 0.28f, r.Bottom - r.Height * 0.02f),
                new PointF(r.Right - r.Width * 0.08f, r.Top + r.Height * 0.34f),
                new PointF(r.Left + r.Width * 0.60f, r.Top + r.Height * 0.38f)
            };
            PointF[] white = new PointF[]
            {
                new PointF(r.Left + r.Width * 0.15f, r.Top + r.Height * 0.12f),
                new PointF(r.Left + r.Width * 0.40f, r.Top + r.Height * 0.34f),
                new PointF(r.Left + r.Width * 0.31f, r.Top + r.Height * 0.48f),
                new PointF(r.Left + r.Width * 0.04f, r.Top + r.Height * 0.26f)
            };

            using (SolidBrush orangeBrush = new SolidBrush(Color.FromArgb(255, 176, 54)))
            using (SolidBrush whiteBrush = new SolidBrush(Color.White))
            using (Pen glow = new Pen(Color.FromArgb(80, 255, 176, 54), 5))
            {
                g.DrawPolygon(glow, orange);
                g.FillPolygon(whiteBrush, white);
                g.FillPolygon(orangeBrush, orange);
            }
        }
    }

    private sealed class SlimProgressBar : Control
    {
        private ProgressBarStyle style = ProgressBarStyle.Continuous;
        private int marqueeAnimationSpeed;
        private int minimum;
        private int maximum = 100;
        private int value;

        public SlimProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public ProgressBarStyle Style
        {
            get { return style; }
            set { style = value; Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int MarqueeAnimationSpeed
        {
            get { return marqueeAnimationSpeed; }
            set { marqueeAnimationSpeed = value; Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Minimum
        {
            get { return minimum; }
            set { minimum = value; Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Maximum
        {
            get { return maximum; }
            set { maximum = Math.Max(value, minimum + 1); Invalidate(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get { return value; }
            set
            {
                int next = Math.Max(minimum, Math.Min(maximum, value));
                this.value = next;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            using (SolidBrush back = new SolidBrush(Color.FromArgb(25, 39, 59)))
            using (GraphicsPath backPath = RoundedRect(rect, 4))
            {
                e.Graphics.FillPath(back, backPath);
            }

            int fillWidth;
            int fillLeft = 1;
            if (style == ProgressBarStyle.Marquee)
            {
                fillWidth = Math.Max(42, rect.Width / 2);
                int travel = Math.Max(1, rect.Width - fillWidth - 2);
                fillLeft = 1 + (int)Math.Round(travel * (value / (double)Math.Max(1, maximum - minimum)));
            }
            else
            {
                int max = Math.Max(1, maximum - minimum);
                fillWidth = (int)Math.Round((rect.Width - 2) * ((value - minimum) / (double)max));
                if (value <= minimum)
                {
                    fillWidth = Math.Max(44, rect.Width / 5);
                }
            }

            Rectangle fill = new Rectangle(fillLeft, 1, Math.Min(rect.Width - 2, fillWidth), Math.Max(1, rect.Height - 2));
            using (LinearGradientBrush brush = new LinearGradientBrush(fill, Color.FromArgb(255, 176, 54), Color.FromArgb(62, 140, 255), LinearGradientMode.Horizontal))
            using (GraphicsPath fillPath = RoundedRect(fill, 4))
            {
                e.Graphics.FillPath(brush, fillPath);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private sealed class ScoreWindow : Form
    {
        private readonly string sourcePath;
        private Label updatedValue;
        private Label topValue;
        private Label appsValue;
        private Label deltaValue;
        private Label statusLabel;
        private DataGridView grid;
        private Button refreshButton;
        private Button optimizeButton;
        private Button closeButton;
        private bool busy;

        public ScoreWindow(string scoreFilePath)
        {
            sourcePath = scoreFilePath;
            Text = "Nap Score";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(780, 500);
            Size = new Size(920, 620);
            Icon = LoadIcon();
            BuildLayout();
        }

        private void BuildLayout()
        {
            BackColor = Color.FromArgb(244, 247, 249);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24);
            root.RowCount = 5;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            FlowLayoutPanel header = new FlowLayoutPanel();
            header.FlowDirection = FlowDirection.TopDown;
            header.WrapContents = false;
            header.AutoSize = true;
            header.Margin = new Padding(0, 0, 0, 16);

            Label title = new Label();
            title.Text = "Nap Score";
            title.Font = new Font("Segoe UI", 22, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(20, 29, 40);
            title.AutoSize = true;
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Latest background pressure ranking from Smart Background Nap.";
            subtitle.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            subtitle.ForeColor = Color.FromArgb(88, 101, 115);
            subtitle.AutoSize = true;
            subtitle.Margin = new Padding(0, 2, 0, 0);
            header.Controls.Add(subtitle);
            root.Controls.Add(header, 0, 0);

            TableLayoutPanel metrics = new TableLayoutPanel();
            metrics.Dock = DockStyle.Fill;
            metrics.ColumnCount = 4;
            metrics.RowCount = 1;
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            metrics.Margin = new Padding(0, 0, 0, 14);
            updatedValue = AddMetric(metrics, 0, "Last update");
            topValue = AddMetric(metrics, 1, "Top process");
            appsValue = AddMetric(metrics, 2, "Apps scored");
            deltaValue = AddMetric(metrics, 3, "Memory eased");
            root.Controls.Add(metrics, 0, 1);

            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(236, 241, 245);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(45, 58, 72);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 112, 83);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.RowTemplate.Height = 28;
            AddColumn("Process", 150);
            AddColumn("Score", 70);
            AddColumn("Delta MB", 80);
            AddColumn("CPU %", 70);
            AddColumn("Bursts", 70);
            AddColumn("Before MB", 85);
            AddColumn("After MB", 85);
            AddColumn("Actions", 210);
            root.Controls.Add(grid, 0, 2);

            statusLabel = new Label();
            statusLabel.Text = "Ready.";
            statusLabel.AutoSize = true;
            statusLabel.Font = new Font("Segoe UI", 9);
            statusLabel.ForeColor = Color.FromArgb(90, 103, 116);
            statusLabel.Margin = new Padding(0, 10, 0, 10);
            root.Controls.Add(statusLabel, 0, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            actions.AutoSize = true;
            actions.Dock = DockStyle.Right;
            actions.Margin = new Padding(0);

            refreshButton = CreateScoreButton("Refresh", delegate { RefreshScore(); }, false, 110);
            optimizeButton = CreateScoreButton("Optimize now", delegate { OptimizeNow(); }, true, 140);
            closeButton = CreateScoreButton("Close", delegate { Close(); }, false, 92);
            actions.Controls.Add(refreshButton);
            actions.Controls.Add(optimizeButton);
            actions.Controls.Add(closeButton);
            root.Controls.Add(actions, 0, 4);
        }

        private Label AddMetric(TableLayoutPanel parent, int column, string caption)
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
            title.Location = new Point(14, 12);
            panel.Controls.Add(title);

            Label value = new Label();
            value.Text = "...";
            value.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            value.ForeColor = Color.FromArgb(24, 32, 43);
            value.Location = new Point(14, 42);
            value.Size = new Size(170, 40);
            value.AutoEllipsis = true;
            panel.Controls.Add(value);

            parent.Controls.Add(panel, column, 0);
            return value;
        }

        private void AddColumn(string header, int fillWeight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.HeaderText = header;
            column.FillWeight = fillWeight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(column);
        }

        private Button CreateScoreButton(string text, EventHandler handler, bool primary, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Font = new Font("Segoe UI", 10, primary ? FontStyle.Bold : FontStyle.Regular);
            button.Width = width;
            button.Height = 40;
            button.Margin = new Padding(0, 0, 10, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = primary ? Color.FromArgb(35, 112, 83) : Color.FromArgb(196, 205, 214);
            button.BackColor = primary ? Color.FromArgb(35, 112, 83) : Color.White;
            button.ForeColor = primary ? Color.White : Color.FromArgb(35, 45, 58);
            button.Click += handler;
            return button;
        }

        public void RefreshScore()
        {
            try
            {
                string timestampText;
                List<ScoreRow> rows = LoadRows(out timestampText);
                PopulateGrid(rows);

                updatedValue.Text = timestampText;
                appsValue.Text = rows.Count.ToString(CultureInfo.CurrentCulture);
                if (rows.Count > 0)
                {
                    topValue.Text = rows[0].ProcessName;
                }
                else
                {
                    topValue.Text = "None yet";
                }

                double totalDelta = 0;
                for (int i = 0; i < rows.Count; i++)
                {
                    totalDelta += rows[i].DeltaMBValue;
                }
                deltaValue.Text = FormatDecimal(totalDelta) + " MB";
                statusLabel.Text = rows.Count == 0 ? "Run Optimize now once to generate a fresh score." : "Showing the latest score generated by the optimizer.";
            }
            catch (Exception ex)
            {
                grid.Rows.Clear();
                updatedValue.Text = "Unavailable";
                topValue.Text = "Unavailable";
                appsValue.Text = "0";
                deltaValue.Text = "0.0 MB";
                statusLabel.Text = "Could not read Nap Score: " + ShortScoreError(ex.Message);
            }
        }

        private void PopulateGrid(List<ScoreRow> rows)
        {
            grid.Rows.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                ScoreRow row = rows[i];
                int index = grid.Rows.Add(
                    row.ProcessName,
                    FormatDecimal(row.ScoreValue),
                    FormatDecimal(row.DeltaMBValue),
                    FormatDecimal(row.CpuPercentValue),
                    row.BurstCount.ToString(CultureInfo.CurrentCulture),
                    FormatDecimal(row.BeforeMBValue),
                    FormatDecimal(row.AfterMBValue),
                    row.Actions);

                DataGridViewRow gridRow = grid.Rows[index];
                if (row.ScoreValue >= 100)
                {
                    gridRow.DefaultCellStyle.BackColor = Color.FromArgb(238, 250, 244);
                }
                if (!String.IsNullOrWhiteSpace(row.Path))
                {
                    gridRow.Cells[0].ToolTipText = row.Path;
                }
            }
        }

        private List<ScoreRow> LoadRows(out string timestampText)
        {
            timestampText = "No score yet";
            List<ScoreRow> rows = new List<ScoreRow>();
            if (!File.Exists(sourcePath))
            {
                return rows;
            }

            string json = File.ReadAllText(sourcePath, Encoding.UTF8);
            if (String.IsNullOrWhiteSpace(json))
            {
                timestampText = FormatFileTime();
                return rows;
            }

            IDictionary<string, object> root = JsonCompat.DeserializeObject(json);
            if (root == null)
            {
                timestampText = FormatFileTime();
                return rows;
            }

            string timestamp = GetString(root, "Timestamp");
            timestampText = String.IsNullOrWhiteSpace(timestamp) ? FormatFileTime() : FormatTimestamp(timestamp);

            object items;
            if (!root.TryGetValue("Items", out items) || items == null)
            {
                return rows;
            }

            System.Collections.IEnumerable enumerable = items as System.Collections.IEnumerable;
            if (enumerable == null || items is string)
            {
                return rows;
            }

            foreach (object item in enumerable)
            {
                IDictionary<string, object> map = item as IDictionary<string, object>;
                if (map == null)
                {
                    continue;
                }

                ScoreRow row = new ScoreRow();
                row.ProcessName = BuildProcessLabel(map);
                row.ScoreValue = GetDouble(map, "Score");
                row.DeltaMBValue = GetDouble(map, "DeltaMB");
                row.CpuPercentValue = GetDouble(map, "CpuPercent");
                row.BurstCount = GetInt(map, "BurstCount");
                row.BeforeMBValue = GetDouble(map, "WorkingSetBeforeMB");
                row.AfterMBValue = GetDouble(map, "WorkingSetAfterMB");
                row.Actions = BuildActionSummary(map);
                row.Path = GetString(map, "Path");
                rows.Add(row);
            }

            rows.Sort(delegate (ScoreRow left, ScoreRow right)
            {
                return right.ScoreValue.CompareTo(left.ScoreValue);
            });
            return rows;
        }

        private string BuildProcessLabel(IDictionary<string, object> map)
        {
            string name = GetString(map, "ProcessName");
            if (String.IsNullOrWhiteSpace(name))
            {
                name = "Unknown";
            }

            int id = GetInt(map, "Id");
            return id > 0 ? name + " (" + id.ToString(CultureInfo.CurrentCulture) + ")" : name;
        }

        private string BuildActionSummary(IDictionary<string, object> map)
        {
            string priority = BlankToDash(GetString(map, "Priority"));
            string memory = BlankToDash(GetString(map, "MemoryPriority"));
            string io = BlankToDash(GetString(map, "IoPriority"));
            string trim = BlankToDash(GetString(map, "TrimWorkingSet"));
            string power = BlankToDash(GetString(map, "PowerThrottling"));
            string text = "P " + priority + " / Mem " + memory + " / IO " + io + " / T " + trim + " / Eco " + power;
            if (GetBool(map, "ForegroundFullscreen"))
            {
                text += " / fullscreen protected";
            }
            return text;
        }

        private static string GetString(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return "";
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                int parsed;
                return Int32.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
            }
        }

        private static double GetDouble(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                double parsed;
                return Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
            }
        }

        private static bool GetBool(IDictionary<string, object> map, string key)
        {
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return false;
            }

            if (value is bool)
            {
                return (bool)value;
            }

            bool parsed;
            return Boolean.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) && parsed;
        }

        private static string BlankToDash(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private string FormatFileTime()
        {
            try
            {
                return File.GetLastWriteTime(sourcePath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
            }
            catch
            {
                return "No score yet";
            }
        }

        private static string FormatTimestamp(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                return parsed.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
            }
            return value;
        }

        private static string FormatDecimal(double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
            {
                return "0.0";
            }
            return value.ToString("0.0", CultureInfo.CurrentCulture);
        }

        private static string ShortScoreError(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return "unknown error";
            }
            value = value.Trim();
            return value.Length > 180 ? value.Substring(0, 180) + "..." : value;
        }

        private void OptimizeNow()
        {
            if (busy)
            {
                return;
            }

            SetBusy(true);
            statusLabel.Text = "Optimizing background apps...";
            ThreadPool.QueueUserWorkItem(delegate
            {
                RunResult result = RunApplyNow();
                try
                {
                    BeginInvoke(new System.Windows.Forms.MethodInvoker(delegate
                    {
                        SetBusy(false);
                        RefreshScore();
                        if (result.ExitCode == 0)
                        {
                            statusLabel.Text = "Optimization pass finished.";
                        }
                        else
                        {
                            statusLabel.Text = "Optimization failed: " + ShortScoreError(result.Output);
                            MessageBox.Show(ShortScoreError(result.Output), AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }));
                }
                catch
                {
                }
            });
        }

        private void SetBusy(bool isBusy)
        {
            busy = isBusy;
            refreshButton.Enabled = !isBusy;
            optimizeButton.Enabled = !isBusy;
            closeButton.Enabled = !isBusy;
            optimizeButton.Text = isBusy ? "Optimizing..." : "Optimize now";
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshScore();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (Object.ReferenceEquals(scoreWindow, this))
            {
                scoreWindow = null;
            }
        }

        private sealed class ScoreRow
        {
            public string ProcessName;
            public double ScoreValue;
            public double DeltaMBValue;
            public double CpuPercentValue;
            public int BurstCount;
            public double BeforeMBValue;
            public double AfterMBValue;
            public string Actions;
            public string Path;
        }
    }

    private static class JsonCompat
    {
        public static IDictionary<string, object> DeserializeObject(string json)
        {
#if NET9_0_OR_GREATER
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                return ConvertObject(document.RootElement) as IDictionary<string, object>;
            }
#else
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.DeserializeObject(json) as IDictionary<string, object>;
#endif
        }

        public static string SerializeObject(object value)
        {
#if NET9_0_OR_GREATER
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.WriteIndented = true;
            return JsonSerializer.Serialize(value, options);
#else
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.Serialize(value);
#endif
        }

#if NET9_0_OR_GREATER
        private static object ConvertObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    Dictionary<string, object> map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        map[property.Name] = ConvertObject(property.Value);
                    }
                    return map;
                case JsonValueKind.Array:
                    List<object> list = new List<object>();
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        list.Add(ConvertObject(item));
                    }
                    return list;
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    long longValue;
                    if (element.TryGetInt64(out longValue))
                    {
                        return longValue;
                    }
                    double doubleValue;
                    return element.TryGetDouble(out doubleValue) ? (object)doubleValue : 0.0;
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                default:
                    return null;
            }
        }
#endif
    }

    private sealed class RunControl
    {
        private readonly object sync = new object();
        private Process process;
        public volatile bool CancelRequested;

        public void SetProcess(Process value)
        {
            lock (sync)
            {
                process = value;
            }
        }

        public void ClearProcess()
        {
            lock (sync)
            {
                process = null;
            }
        }

        public void Cancel()
        {
            Process toKill = null;
            lock (sync)
            {
                CancelRequested = true;
                toKill = process;
            }

            if (toKill != null)
            {
                try
                {
                    if (!toKill.HasExited)
                    {
                        toKill.Kill();
                    }
                }
                catch
                {
                }
            }
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

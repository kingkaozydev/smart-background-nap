using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
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
    private const string AppVersion = "0.8.0";
    private const string CreatorLine = "Criado por KaozyKing | GitHub: kingkaozydev";
    private const string AutoTaskName = "SmartBackgroundNap";
    private const string TrayTaskName = "SmartBackgroundNapTray";
    private const string DashboardTaskName = "SmartBackgroundNapDashboard";
    private const string SessionAgentTaskName = "SmartBackgroundNapSessionAgent";
    private const string CoreServiceName = "SmartSNAPCoreService";
    private const string CoreServiceDisplayName = "Smart SNAP Core Service";
    private const int CoreServiceLoopSeconds = 25;
    private const int CoreServiceStalePassSeconds = 150;
    private const string MemoryStabilityGuardMode = "Shadow";
    private const string SystemIntegrityGuardMode = "Shadow";
    private const uint ProcessModeBackgroundBegin = 0x00100000;
    private const uint ProcessModeBackgroundEnd = 0x00200000;
    private const int CoreProtocolVersion = 1;
    private const int CoreMinimumSupportedProtocolVersion = 1;
    private const string CorePipeName = "SmartNap.Core.v1";
    private const string CoreContextProviderLegacyBridge = "ScheduledUserSessionTask";
    private const int CorePipeMaxMessageBytes = 65536;
    private const int CorePipeConnectPollMilliseconds = 250;
    private const int CorePipeSubscribeHeartbeatSeconds = 10;
    private const int CorePipeMaxConcurrentConnections = 4;
    private const int SessionAgentLoopMilliseconds = 2000;
    private const int SessionAgentStateMaxAgeSeconds = 12;
    private const string SessionAgentClientType = "sessionAgent";
    private const string GitHubUrl = "https://github.com/kingkaozydev/smart-background-nap";
    private const string GitHubLatestReleaseApi = "https://api.github.com/repos/kingkaozydev/smart-background-nap/releases/latest";
    private const string GitHubLatestDownloadUrl = "https://github.com/kingkaozydev/smart-background-nap/releases/latest/download/SmartBackgroundNap.exe";
    private const string MutexName = "Local\\SmartBackgroundNap.SingleInstance";
    private const string ShowDashboardEventName = "Local\\SmartBackgroundNap.ShowDashboard";
    private const string ResourcePrefix = "SmartBackgroundNap.Resources.";
    private const string SmartNapGamePowerPlanGuid = "7a6f2f9d-88d3-4abf-8b5f-3f8f2f477501";
    private const string SmartNapLivePowerPlanGuid = "79b75b8e-1118-40a6-a9e6-72b72d760457";
    private const string BalancedPowerPlanGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string SmartNapGamePowerPlanName = "Smart Nap MODO JOGO";
    private const string SmartNapLivePowerPlanName = "Smart Nap MODO LIVE";
    private const int EnergyIdleDefaultMinutes = 20;
    private const int EnergyIdleMinMinutes = 5;
    private const int EnergyIdleMaxMinutes = 240;

    private static readonly object powerPlanCacheLock = new object();
    private static PowerPlanSnapshot cachedPowerPlan;
    private static DateTime cachedPowerPlanAtUtc = DateTime.MinValue;
    private static string appRoot;
    private static string backgroundScriptPath;
    private static string autoManagerPath;
    private static string trayManagerPath;
    private static string configPath;
    private static string userConfigPath;
    private static string readmePath;
    private static string securityModelPath;
    private static string iconPath;
    private static string logoPath;
    private static string heroPath;
    private static string powerBasePlanPath;
    private static string uiSettingsPath;
    private static string learningSettingsPath;
    private static string uiLanguage;
    private static string outputsPath;
    private static string logPath;
    private static string scorePath;
    private static string previewPath;
    private static string appPolicyPath;
    private static string radarPath;
    private static string coreServiceStatePath;
    private static string sessionAgentStatePath;
    private static string safetyReportPath;
    private static readonly object corePipeStateLock = new object();
    private static bool corePipeListening;
    private static DateTime corePipeHeartbeatUtc = DateTime.MinValue;
    private static DateTime corePipeLastClientUtc = DateTime.MinValue;
    private static string corePipeLastCommand = "";
    private static string corePipeLastClientUser = "";
    private static string corePipeLastError = "";
    private static long corePipeRequestCount;
    private static long corePipeEventSequence;
    private static int corePipeActiveConnections;
    private static readonly object memoryStabilityLogLock = new object();
    private static string memoryStabilityLastLogSignature = "";
    private static readonly object systemIntegrityLogLock = new object();
    private static string systemIntegrityLastLogSignature = "";
    private static readonly object processBackgroundModeLock = new object();
    private static bool processBackgroundModeEnabled;
    private static bool usingLooseRuntime;
    private static readonly object hardwareLock = new object();
    private static HardwareSnapshot hardwareSnapshotCache;
    private static DateTime hardwareSnapshotAtUtc = DateTime.MinValue;
    private static readonly object cpuClockLock = new object();
    private static CpuClockSnapshot cpuClockCache;
    private static DateTime cpuClockSnapshotAtUtc = DateTime.MinValue;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    private struct MemorySnapshot
    {
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public int MemoryLoad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorPowerInformation
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    private sealed class PowerPlanSnapshot
    {
        public string Guid;
        public string Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out WindowRect rect);
    private struct CpuClockSnapshot
    {
        public int CurrentMhz;
        public int MaxMhz;
        public int LimitMhz;
        public int PerformancePercent;
        public bool ReliableCurrent;
    }

    private sealed class RamModuleInfo
    {
        public string Manufacturer;
        public string PartNumber;
        public int SpeedMhz;
        public ulong CapacityBytes;
    }

    private sealed class RegistryGpuInfo
    {
        public string Name;
        public ulong MemoryBytes;
        public string DriverVersion;
        public string Display;
    }
    private sealed class HardwareSnapshot
    {
        public string Cpu;
        public string CpuDetail;
        public string CpuBaseDetail;
        public string Ram;
        public string RamDetail;
        public string Gpu;
        public string GpuDetail;
        public string Os;
        public string AvailableMemoryText;
        public string SystemDetail;
        public double TotalMemoryMB;
        public double AvailableMemoryMB;
        public double PageFileTotalMB;
        public double PageFileAvailableMB;
        public double VirtualTotalMB;
        public double VirtualAvailableMB;
        public int MemoryLoad;
        public int CpuClockCurrentMhz;
        public int CpuClockMaxMhz;

        public HardwareSnapshot Clone()
        {
            HardwareSnapshot clone = new HardwareSnapshot();
            clone.Cpu = Cpu;
            clone.CpuDetail = CpuDetail;
            clone.CpuBaseDetail = CpuBaseDetail;
            clone.Ram = Ram;
            clone.RamDetail = RamDetail;
            clone.Gpu = Gpu;
            clone.GpuDetail = GpuDetail;
            clone.Os = Os;
            clone.AvailableMemoryText = AvailableMemoryText;
            clone.SystemDetail = SystemDetail;
            clone.TotalMemoryMB = TotalMemoryMB;
            clone.AvailableMemoryMB = AvailableMemoryMB;
            clone.PageFileTotalMB = PageFileTotalMB;
            clone.PageFileAvailableMB = PageFileAvailableMB;
            clone.VirtualTotalMB = VirtualTotalMB;
            clone.VirtualAvailableMB = VirtualAvailableMB;
            clone.MemoryLoad = MemoryLoad;
            clone.CpuClockCurrentMhz = CpuClockCurrentMhz;
            clone.CpuClockMaxMhz = CpuClockMaxMhz;
            return clone;
        }
    }

    private static Mutex singleInstanceMutex;
    private static EventWaitHandle showDashboardEvent;
    #if !NET9_0_OR_GREATER
    private static ScoreWindow scoreWindow;
    #endif
    private static readonly object firstRunDefaultsLock = new object();
    private static bool firstRunDefaultsChecked;

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

        if (HasArg(args, "--complete-update"))
        {
            Environment.ExitCode = CompleteSelfUpdate(args).ExitCode;
            return;
        }

        if (HasArg(args, "--core-service") || HasArg(args, "--service"))
        {
            RunCoreServiceHost(args);
            return;
        }
        if (HasArg(args, "--core-service-once"))
        {
            Environment.ExitCode = RunCoreServicePass("manual").ExitCode;
            return;
        }
        if (HasArg(args, "--install-core-service"))
        {
            Environment.ExitCode = InstallCoreService().ExitCode;
            return;
        }
        if (HasArg(args, "--uninstall-core-service"))
        {
            Environment.ExitCode = UninstallCoreService().ExitCode;
            return;
        }
        if (HasArg(args, "--start-core-service"))
        {
            Environment.ExitCode = StartCoreService().ExitCode;
            return;
        }
        if (HasArg(args, "--stop-core-service"))
        {
            Environment.ExitCode = StopCoreService().ExitCode;
            return;
        }
        if (HasArg(args, "--core-service-status"))
        {
            Environment.ExitCode = WriteCoreServiceStatusToConsole().ExitCode;
            return;
        }
        if (HasArg(args, "--core-pipe-request"))
        {
            Environment.ExitCode = WriteCorePipeRequestToConsole(args).ExitCode;
            return;
        }
        if (HasArg(args, "--session-agent-once"))
        {
            Environment.ExitCode = WriteSessionAgentOnceToConsole(args).ExitCode;
            return;
        }
        if (HasArg(args, "--install-session-agent"))
        {
            Environment.ExitCode = InstallSessionAgent().ExitCode;
            return;
        }
        if (HasArg(args, "--uninstall-session-agent"))
        {
            Environment.ExitCode = UninstallSessionAgent().ExitCode;
            return;
        }
        if (HasArg(args, "--session-agent-status"))
        {
            Environment.ExitCode = WriteSessionAgentStatusToConsole().ExitCode;
            return;
        }
        if (HasArg(args, "--session-agent"))
        {
            Environment.ExitCode = RunSessionAgentHost(args).ExitCode;
            return;
        }

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
            RunResult install = InstallComplete();
            MarkAdminSetupCompletedIfReady("install", install);
            Environment.ExitCode = install.ExitCode;
            return;
        }
        if (HasArg(args, "--repair-install") || HasArg(args, "--setup-elevated"))
        {
            RunResult install = InstallComplete(false);
            MarkAdminSetupCompletedIfReady("repair", install);
            Environment.ExitCode = install.ExitCode;
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
        if (HasArg(args, "--uninstall-dashboard"))
        {
            Environment.ExitCode = UninstallDashboardTask().ExitCode;
            return;
        }
        if (HasArg(args, "--safety-report"))
        {
            WriteSafetyReport();
            Environment.ExitCode = 0;
            return;
        }

        bool trayOnly = HasArg(args, "--tray");
        if (!trayOnly && !IsCurrentProcessElevated())
        {
            EnsureAdminSetupForCurrentVersion();
            if (TryDelegateInteractiveLaunchToElevatedTask())
            {
                return;
            }
        }

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

        EnsureAdminSetupForCurrentVersion();
        EnsureFirstRunDefaults();
        EnsureInstallRepairOnLaunch();

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
        string currentRuntimeRoot = "";
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
            string runtimeRoot = Path.Combine(appRoot, "runtime-" + AppVersion);
            EnsureRuntimeFiles(runtimeRoot);
            looseRoot = runtimeRoot;
            currentRuntimeRoot = runtimeRoot;
            usingLooseRuntime = false;
        }

        backgroundScriptPath = Path.Combine(looseRoot, "background-nap.ps1");
        autoManagerPath = Path.Combine(looseRoot, "manage-background-nap.ps1");
        trayManagerPath = Path.Combine(looseRoot, "manage-background-nap-tray.ps1");
        configPath = Path.Combine(looseRoot, "game-session.config.json");
        userConfigPath = Path.Combine(appRoot, "game-session.user.config.json");
        readmePath = Path.Combine(looseRoot, "README.md");
        securityModelPath = Path.Combine(looseRoot, "SECURITY_MODEL.md");
        if (!File.Exists(securityModelPath) && File.Exists(Path.Combine(looseRoot, "docs\\SECURITY_MODEL.md")))
        {
            securityModelPath = Path.Combine(looseRoot, "docs\\SECURITY_MODEL.md");
        }
        iconPath = Path.Combine(looseRoot, "assets\\smart-nap-logo.ico");
        logoPath = Path.Combine(looseRoot, "assets\\smart-nap-logo-v2.png");
        heroPath = Path.Combine(looseRoot, "assets\\smart-nap-hero-bg.png");
        powerBasePlanPath = Path.Combine(looseRoot, "assets\\power\\smart-nap-hone-base.pow");
        uiSettingsPath = Path.Combine(appRoot, "ui-settings.json");
        learningSettingsPath = Path.Combine(appRoot, "smart-learning.settings.json");
        uiLanguage = LoadUiLanguage();
        outputsPath = Path.Combine(appRoot, "outputs");
        logPath = Path.Combine(outputsPath, "background-nap-auto.log");
        scorePath = Path.Combine(outputsPath, "background-nap-score-latest.json");
        previewPath = Path.Combine(outputsPath, "background-nap-preview-latest.json");
        appPolicyPath = Path.Combine(outputsPath, "background-nap-app-policies.json");
        radarPath = Path.Combine(outputsPath, "background-nap-radar-latest.json");
        coreServiceStatePath = Path.Combine(outputsPath, "smart-snap-core-service-latest.json");
        sessionAgentStatePath = Path.Combine(outputsPath, "smart-snap-session-agent-latest.json");
        safetyReportPath = Path.Combine(outputsPath, "SmartBackgroundNap-SafetyReport.txt");
        MigrateConfigForCurrentRuntime();
        if (!usingLooseRuntime)
        {
            CleanupOldRuntimeFolders(currentRuntimeRoot);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr processHandle, int processInformationClass, IntPtr processInformation, uint processInformationSize);

    [DllImport("ntdll.dll")]
    private static extern int NtSetInformationProcess(IntPtr processHandle, int processInformationClass, ref uint processInformation, uint processInformationLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

    private const int LowMemoryResourceNotification = 0;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateMemoryResourceNotification(int notificationType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryMemoryResourceNotification(IntPtr resourceNotificationHandle, out bool resourceState);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint CallNtPowerInformation(int informationLevel, IntPtr inputBuffer, uint inputBufferSize, IntPtr outputBuffer, uint outputBufferSize);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhOpenQuery(string dataSource, UIntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern int PdhAddEnglishCounter(IntPtr query, string fullCounterPath, UIntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern int PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern int PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint counterType, out PdhFmtCounterValue value);

    [DllImport("pdh.dll")]
    private static extern int PdhCloseQuery(IntPtr query);

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFmtCounterValue
    {
        public uint CStatus;
        public double DoubleValue;
    }

    private const uint PdhFmtDouble = 0x00000200;
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

    private static HardwareSnapshot GetHardwareSnapshot()
    {
        MemorySnapshot memory = GetMemorySnapshot();
        lock (hardwareLock)
        {
            bool expired = hardwareSnapshotCache == null || (DateTime.UtcNow - hardwareSnapshotAtUtc).TotalMinutes >= 10.0;
            if (expired)
            {
                HardwareSnapshot probed = ProbeHardwareSnapshot(memory);
                if (hardwareSnapshotCache == null || IsHardwareSnapshotReliable(probed))
                {
                    hardwareSnapshotCache = probed;
                    hardwareSnapshotAtUtc = DateTime.UtcNow;
                }
                else
                {
                    hardwareSnapshotAtUtc = DateTime.UtcNow.AddMinutes(-9.75);
                    AppendOperationalLog("action=hardware-probe status=kept-last-good");
                }
            }

            HardwareSnapshot snapshot = hardwareSnapshotCache.Clone();
            ApplyMemoryToHardwareSnapshot(snapshot, memory);
            return snapshot;
        }
    }

    private static bool IsHardwareSnapshotReliable(HardwareSnapshot snapshot)
    {
        if (snapshot == null) { return false; }
        bool hasCpu = !String.IsNullOrWhiteSpace(snapshot.Cpu) &&
            snapshot.Cpu.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) < 0;
        bool hasGpu = !String.IsNullOrWhiteSpace(snapshot.Gpu) &&
            snapshot.Gpu.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) < 0;
        return hasCpu && hasGpu;
    }

    private static MemorySnapshot GetMemorySnapshot()
    {
        MemorySnapshot snapshot = new MemorySnapshot();
        try
        {
            MemoryStatusEx status = new MemoryStatusEx();
            if (GlobalMemoryStatusEx(status))
            {
                snapshot.TotalPhysical = status.ullTotalPhys;
                snapshot.AvailablePhysical = status.ullAvailPhys;
                snapshot.TotalPageFile = status.ullTotalPageFile;
                snapshot.AvailablePageFile = status.ullAvailPageFile;
                snapshot.TotalVirtual = status.ullTotalVirtual;
                snapshot.AvailableVirtual = status.ullAvailVirtual;
                snapshot.MemoryLoad = (int)status.dwMemoryLoad;
            }
        }
        catch
        {
        }
        return snapshot;
    }

    private static HardwareSnapshot ProbeHardwareSnapshot(MemorySnapshot memory)
    {
        HardwareSnapshot snapshot = new HardwareSnapshot();
        snapshot.Cpu = CleanHardwareValue(ReadRegistryString(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString"));
        int registryMhz = ReadRegistryInt(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz");
        int cores = 0;
        int threads = Environment.ProcessorCount;
        int maxMhz = registryMhz;
        List<RamModuleInfo> ramModules = new List<RamModuleInfo>();

        try
        {
            string script = "$ErrorActionPreference='SilentlyContinue';" +
                "Get-CimInstance Win32_Processor | Select-Object -First 1 | ForEach-Object { Write-Output ('CPUINFO=' + $_.NumberOfCores + '|' + $_.NumberOfLogicalProcessors + '|' + $_.MaxClockSpeed) };" +
                "Get-CimInstance Win32_PhysicalMemory | ForEach-Object { Write-Output ('RAMDIMM=' + $_.Manufacturer + '|' + $_.PartNumber + '|' + $_.ConfiguredClockSpeed + '|' + $_.Capacity) };" +
                "Get-CimInstance Win32_VideoController | Sort-Object AdapterRAM -Descending | Select-Object -First 1 | ForEach-Object { Write-Output ('GPU=' + $_.Name + '|' + $_.AdapterRAM + '|' + $_.DriverVersion + '|' + $_.CurrentHorizontalResolution + 'x' + $_.CurrentVerticalResolution + '@' + $_.CurrentRefreshRate) };" +
                "Get-CimInstance Win32_OperatingSystem | Select-Object -First 1 | ForEach-Object { Write-Output ('OS=' + $_.Caption + '|' + $_.Version + '|' + $_.OSArchitecture) };";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            RunResult result = RunHidden("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded, 7000);
            string[] lines = result.Output.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith("CPUINFO=", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Substring("CPUINFO=".Length).Split('|');
                    if (parts.Length > 0) { cores = ParseInt(parts[0], cores); }
                    if (parts.Length > 1) { threads = ParseInt(parts[1], threads); }
                    if (parts.Length > 2) { maxMhz = ParseInt(parts[2], maxMhz); }
                }
                else if (line.StartsWith("RAMDIMM=", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Substring("RAMDIMM=".Length).Split('|');
                    RamModuleInfo module = new RamModuleInfo();
                    if (parts.Length > 0) { module.Manufacturer = CleanHardwareValue(parts[0]); }
                    if (parts.Length > 1) { module.PartNumber = CleanHardwareValue(parts[1]); }
                    if (parts.Length > 2) { module.SpeedMhz = ParseInt(parts[2], 0); }
                    if (parts.Length > 3) { module.CapacityBytes = ParseUInt64(parts[3], 0); }
                    ramModules.Add(module);
                }
                else if (line.StartsWith("GPU=", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Substring("GPU=".Length).Split('|');
                    snapshot.Gpu = parts.Length > 0 ? CleanHardwareValue(parts[0]) : "";
                    ulong adapterRam = parts.Length > 1 ? ParseUInt64(parts[1], 0) : 0;
                    string driver = parts.Length > 2 ? CleanHardwareValue(parts[2]) : "";
                    string display = parts.Length > 3 ? CleanHardwareValue(parts[3]) : "";
                    ulong driverMemory = GetVideoMemoryBytes(snapshot.Gpu);
                    if (driverMemory > adapterRam) { adapterRam = driverMemory; }
                    snapshot.GpuDetail = BuildGpuDetail(snapshot.Gpu, adapterRam, driver, display);
                }
                else if (line.StartsWith("OS=", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Substring("OS=".Length).Split('|');
                    string caption = parts.Length > 0 ? CleanHardwareValue(parts[0]) : "";
                    string version = parts.Length > 1 ? CleanHardwareValue(parts[1]) : "";
                    string architecture = parts.Length > 2 ? CleanHardwareValue(parts[2]) : "";
                    snapshot.Os = caption;
                    if (!String.IsNullOrWhiteSpace(version)) { snapshot.Os += " " + version; }
                    if (!String.IsNullOrWhiteSpace(architecture)) { snapshot.Os += " " + architecture; }
                    snapshot.Os = CleanHardwareValue(snapshot.Os);
                }
            }
        }
        catch
        {
        }

        if (String.IsNullOrWhiteSpace(snapshot.Gpu))
        {
            RegistryGpuInfo registryGpu = GetRegistryGpuInfo("");
            if (registryGpu != null && !String.IsNullOrWhiteSpace(registryGpu.Name))
            {
                snapshot.Gpu = registryGpu.Name;
                snapshot.GpuDetail = BuildGpuDetail(snapshot.Gpu, registryGpu.MemoryBytes, registryGpu.DriverVersion, registryGpu.Display);
            }
        }
        else if (String.IsNullOrWhiteSpace(snapshot.GpuDetail) || snapshot.GpuDetail.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            RegistryGpuInfo registryGpu = GetRegistryGpuInfo(snapshot.Gpu);
            if (registryGpu != null && registryGpu.MemoryBytes > 0)
            {
                snapshot.GpuDetail = BuildGpuDetail(snapshot.Gpu, registryGpu.MemoryBytes, FirstNonEmpty(registryGpu.DriverVersion, ""), registryGpu.Display);
            }
        }
        if (String.IsNullOrWhiteSpace(snapshot.Cpu)) { snapshot.Cpu = "CPU unavailable"; }
        if (String.IsNullOrWhiteSpace(snapshot.Gpu)) { snapshot.Gpu = "GPU unavailable"; }
        if (String.IsNullOrWhiteSpace(snapshot.GpuDetail)) { snapshot.GpuDetail = "GPU detail unavailable"; }
        snapshot.CpuBaseDetail = BuildCpuDetail(cores, threads, maxMhz);
        snapshot.CpuDetail = snapshot.CpuBaseDetail;
        snapshot.RamDetail = BuildRamDetail(ramModules);
        if (String.IsNullOrWhiteSpace(snapshot.Os)) { snapshot.Os = CleanHardwareValue(RuntimeInformation.OSDescription); }
        if (Environment.Is64BitOperatingSystem && snapshot.Os.IndexOf("64", StringComparison.OrdinalIgnoreCase) < 0)
        {
            snapshot.Os += " x64";
        }
        return snapshot;
    }

    private static void ApplyMemoryToHardwareSnapshot(HardwareSnapshot snapshot, MemorySnapshot memory)
    {
        if (snapshot == null) { return; }
        CpuClockSnapshot cpuClock = GetCpuClockSnapshot();
        if (cpuClock.CurrentMhz > 0) { snapshot.CpuClockCurrentMhz = cpuClock.CurrentMhz; }
        if (cpuClock.MaxMhz > 0) { snapshot.CpuClockMaxMhz = cpuClock.MaxMhz; }
        snapshot.TotalMemoryMB = memory.TotalPhysical > 0 ? memory.TotalPhysical / 1024.0 / 1024.0 : 0.0;
        snapshot.AvailableMemoryMB = memory.AvailablePhysical > 0 ? memory.AvailablePhysical / 1024.0 / 1024.0 : 0.0;
        snapshot.PageFileTotalMB = memory.TotalPageFile > 0 ? memory.TotalPageFile / 1024.0 / 1024.0 : 0.0;
        snapshot.PageFileAvailableMB = memory.AvailablePageFile > 0 ? memory.AvailablePageFile / 1024.0 / 1024.0 : 0.0;
        snapshot.VirtualTotalMB = memory.TotalVirtual > 0 ? memory.TotalVirtual / 1024.0 / 1024.0 : 0.0;
        snapshot.VirtualAvailableMB = memory.AvailableVirtual > 0 ? memory.AvailableVirtual / 1024.0 / 1024.0 : 0.0;
        snapshot.MemoryLoad = memory.MemoryLoad;
        if (memory.TotalPhysical > 0)
        {
            snapshot.Ram = FormatMemoryBytes(memory.TotalPhysical) + " total / " + FormatMemoryBytes(memory.AvailablePhysical) + " free";
            snapshot.AvailableMemoryText = "RAM " + FormatMemoryBytes(memory.AvailablePhysical) + " free";
            snapshot.SystemDetail = BuildSystemMemoryDetail(memory);
        }
        else
        {
            snapshot.Ram = "RAM unavailable";
            snapshot.AvailableMemoryText = "-";
            snapshot.SystemDetail = "-";
        }
        string baseCpuDetail = String.IsNullOrWhiteSpace(snapshot.CpuBaseDetail) ? snapshot.CpuDetail : snapshot.CpuBaseDetail;
        snapshot.CpuDetail = EnrichCpuDetailWithLiveClock(baseCpuDetail, cpuClock);
    }

    private static string BuildCpuDetail(int cores, int threads, int maxMhz)
    {
        List<string> parts = new List<string>();
        if (cores > 0 && threads > 0)
        {
            parts.Add(cores.ToString(CultureInfo.CurrentCulture) + "C / " + threads.ToString(CultureInfo.CurrentCulture) + "T");
        }
        else if (threads > 0)
        {
            parts.Add(threads.ToString(CultureInfo.CurrentCulture) + " logical threads");
        }
        if (maxMhz > 0)
        {
            parts.Add("base " + FormatMhz(maxMhz));
        }
        return parts.Count > 0 ? String.Join(" | ", parts.ToArray()) : "CPU detail unavailable";
    }

    private static string EnrichCpuDetailWithLiveClock(string baseDetail, CpuClockSnapshot clockSnapshot)
    {
        List<string> parts = new List<string>();
        if (!String.IsNullOrWhiteSpace(baseDetail))
        {
            parts.Add(baseDetail);
        }
        if (clockSnapshot.ReliableCurrent && clockSnapshot.CurrentMhz > 0)
        {
            string clock = "agora " + FormatMhz(clockSnapshot.CurrentMhz);
            if (clockSnapshot.PerformancePercent > 0)
            {
                clock += " (" + clockSnapshot.PerformancePercent.ToString(CultureInfo.CurrentCulture) + "% perf)";
            }
            parts.Add(clock);
        }
        return parts.Count > 0 ? String.Join(" | ", parts.ToArray()) : "CPU detail unavailable";
    }

    private static CpuClockSnapshot GetCpuClockSnapshot()
    {
        lock (cpuClockLock)
        {
            if ((DateTime.UtcNow - cpuClockSnapshotAtUtc).TotalSeconds < 2.0)
            {
                return cpuClockCache;
            }

            CpuClockSnapshot snapshot = GetPdhCpuClockSnapshot();
            if (!snapshot.ReliableCurrent)
            {
                CpuClockSnapshot fallback = GetPowerInfoCpuClockSnapshot();
                snapshot.MaxMhz = fallback.MaxMhz;
                snapshot.LimitMhz = fallback.LimitMhz;
            }
            cpuClockCache = snapshot;
            cpuClockSnapshotAtUtc = DateTime.UtcNow;
            return snapshot;
        }
    }

    private static CpuClockSnapshot GetPdhCpuClockSnapshot()
    {
        CpuClockSnapshot snapshot = new CpuClockSnapshot();
        IntPtr query = IntPtr.Zero;
        IntPtr performanceCounter = IntPtr.Zero;
        IntPtr frequencyCounter = IntPtr.Zero;
        try
        {
            if (PdhOpenQuery(null, UIntPtr.Zero, out query) != 0 || query == IntPtr.Zero) { return snapshot; }
            if (PdhAddEnglishCounter(query, @"\Processor Information(_Total)\% Processor Performance", UIntPtr.Zero, out performanceCounter) != 0) { return snapshot; }
            if (PdhAddEnglishCounter(query, @"\Processor Information(_Total)\Processor Frequency", UIntPtr.Zero, out frequencyCounter) != 0) { return snapshot; }
            PdhCollectQueryData(query);
            Thread.Sleep(80);
            if (PdhCollectQueryData(query) != 0) { return snapshot; }

            double performance = ReadPdhDouble(performanceCounter);
            double baseFrequency = ReadPdhDouble(frequencyCounter);
            if (performance > 0.0 && baseFrequency > 0.0)
            {
                snapshot.PerformancePercent = (int)Math.Round(performance);
                snapshot.MaxMhz = (int)Math.Round(baseFrequency);
                snapshot.CurrentMhz = (int)Math.Round(baseFrequency * performance / 100.0);
                snapshot.ReliableCurrent = snapshot.CurrentMhz > 0;
            }
        }
        catch
        {
        }
        finally
        {
            if (query != IntPtr.Zero) { try { PdhCloseQuery(query); } catch { } }
        }
        return snapshot;
    }

    private static double ReadPdhDouble(IntPtr counter)
    {
        if (counter == IntPtr.Zero) { return 0.0; }
        try
        {
            uint counterType;
            PdhFmtCounterValue value;
            int status = PdhGetFormattedCounterValue(counter, PdhFmtDouble, out counterType, out value);
            return status == 0 && value.CStatus == 0 ? value.DoubleValue : 0.0;
        }
        catch
        {
            return 0.0;
        }
    }

    private static CpuClockSnapshot GetPowerInfoCpuClockSnapshot()
    {
        CpuClockSnapshot snapshot = new CpuClockSnapshot();
        int count = Math.Max(1, Environment.ProcessorCount);
        int itemSize = Marshal.SizeOf(typeof(ProcessorPowerInformation));
        IntPtr buffer = IntPtr.Zero;
        try
        {
            buffer = Marshal.AllocHGlobal(itemSize * count);
            uint status = CallNtPowerInformation(11, IntPtr.Zero, 0, buffer, (uint)(itemSize * count));
            if (status != 0) { return snapshot; }

            int max = 0;
            int limit = 0;
            for (int i = 0; i < count; i++)
            {
                IntPtr ptr = new IntPtr(buffer.ToInt64() + (long)i * itemSize);
                ProcessorPowerInformation info = (ProcessorPowerInformation)Marshal.PtrToStructure(ptr, typeof(ProcessorPowerInformation));
                if (info.MaxMhz > max) { max = (int)info.MaxMhz; }
                if (info.MhzLimit > limit) { limit = (int)info.MhzLimit; }
            }

            snapshot.MaxMhz = max;
            snapshot.LimitMhz = limit;
        }
        catch
        {
        }
        finally
        {
            if (buffer != IntPtr.Zero) { Marshal.FreeHGlobal(buffer); }
        }
        return snapshot;
    }

    private static RegistryGpuInfo GetRegistryGpuInfo(string gpuName)
    {
        RegistryGpuInfo bestAny = null;
        RegistryGpuInfo bestMatch = null;
        try
        {
            using (RegistryKey root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video"))
            {
                if (root == null) { return null; }
                foreach (string adapterKeyName in root.GetSubKeyNames())
                {
                    using (RegistryKey adapterKey = root.OpenSubKey(adapterKeyName))
                    {
                        if (adapterKey == null) { continue; }
                        foreach (string childName in adapterKey.GetSubKeyNames())
                        {
                            if (!String.Equals(childName, "0000", StringComparison.OrdinalIgnoreCase) && !String.Equals(childName, "0001", StringComparison.OrdinalIgnoreCase)) { continue; }
                            using (RegistryKey child = adapterKey.OpenSubKey(childName))
                            {
                                if (child == null) { continue; }
                                ulong bytes = ReadRegistryUInt64(child.GetValue("HardwareInformation.qwMemorySize"));
                                string driver = CleanHardwareValue(Convert.ToString(child.GetValue("DriverDesc"), CultureInfo.InvariantCulture));
                                string adapter = CleanHardwareValue(Convert.ToString(child.GetValue("HardwareInformation.AdapterString"), CultureInfo.InvariantCulture));
                                string chip = CleanHardwareValue(Convert.ToString(child.GetValue("HardwareInformation.ChipType"), CultureInfo.InvariantCulture));
                                string registryVersion = CleanHardwareValue(Convert.ToString(child.GetValue("DriverVersion"), CultureInfo.InvariantCulture));
                                string name = FirstNonEmpty(adapter, FirstNonEmpty(driver, chip));
                                if (String.IsNullOrWhiteSpace(name) && bytes == 0) { continue; }
                                RegistryGpuInfo info = new RegistryGpuInfo();
                                info.Name = name;
                                info.MemoryBytes = bytes;
                                info.DriverVersion = registryVersion;
                                info.Display = "";
                                if (bestAny == null || info.MemoryBytes > bestAny.MemoryBytes) { bestAny = info; }
                                if (!String.IsNullOrWhiteSpace(gpuName) && (NamesLookRelated(gpuName, driver) || NamesLookRelated(gpuName, adapter) || NamesLookRelated(gpuName, chip)))
                                {
                                    if (bestMatch == null || info.MemoryBytes > bestMatch.MemoryBytes) { bestMatch = info; }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
        }
        return bestMatch ?? bestAny;
    }
    private static ulong GetVideoMemoryBytes(string gpuName)
    {
        ulong bestAny = 0;
        ulong bestMatch = 0;
        try
        {
            using (RegistryKey root = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video"))
            {
                if (root == null) { return 0; }
                foreach (string adapterKeyName in root.GetSubKeyNames())
                {
                    using (RegistryKey adapterKey = root.OpenSubKey(adapterKeyName))
                    {
                        if (adapterKey == null) { continue; }
                        foreach (string childName in adapterKey.GetSubKeyNames())
                        {
                            if (!String.Equals(childName, "0000", StringComparison.OrdinalIgnoreCase) && !String.Equals(childName, "0001", StringComparison.OrdinalIgnoreCase)) { continue; }
                            using (RegistryKey child = adapterKey.OpenSubKey(childName))
                            {
                                if (child == null) { continue; }
                                ulong bytes = ReadRegistryUInt64(child.GetValue("HardwareInformation.qwMemorySize"));
                                if (bytes == 0) { continue; }
                                if (bytes > bestAny) { bestAny = bytes; }
                                string driver = Convert.ToString(child.GetValue("DriverDesc"), CultureInfo.InvariantCulture);
                                string adapter = Convert.ToString(child.GetValue("HardwareInformation.AdapterString"), CultureInfo.InvariantCulture);
                                string chip = Convert.ToString(child.GetValue("HardwareInformation.ChipType"), CultureInfo.InvariantCulture);
                                if (NamesLookRelated(gpuName, driver) || NamesLookRelated(gpuName, adapter) || NamesLookRelated(gpuName, chip))
                                {
                                    if (bytes > bestMatch) { bestMatch = bytes; }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
        }
        return bestMatch > 0 ? bestMatch : bestAny;
    }

    private static ulong ReadRegistryUInt64(object value)
    {
        try
        {
            if (value == null) { return 0; }
            if (value is ulong) { return (ulong)value; }
            if (value is long) { return (ulong)Math.Max(0L, (long)value); }
            if (value is uint) { return (uint)value; }
            if (value is int) { return (ulong)Math.Max(0, (int)value); }
            byte[] bytes = value as byte[];
            if (bytes != null)
            {
                if (bytes.Length >= 8) { return BitConverter.ToUInt64(bytes, 0); }
                if (bytes.Length >= 4) { return BitConverter.ToUInt32(bytes, 0); }
            }
            return ParseUInt64(Convert.ToString(value, CultureInfo.InvariantCulture), 0);
        }
        catch
        {
            return 0;
        }
    }

    private static bool NamesLookRelated(string left, string right)
    {
        left = NormalizeHardwareName(left);
        right = NormalizeHardwareName(right);
        if (String.IsNullOrWhiteSpace(left) || String.IsNullOrWhiteSpace(right)) { return false; }
        return left.IndexOf(right, StringComparison.OrdinalIgnoreCase) >= 0 || right.IndexOf(left, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizeHardwareName(string value)
    {
        if (String.IsNullOrWhiteSpace(value)) { return ""; }
        string result = value.ToLowerInvariant();
        string[] noise = new string[] { "(r)", "(tm)", "nvidia", "geforce", "intel", "amd", "radeon", "graphics", "gpu" };
        foreach (string token in noise) { result = result.Replace(token, " "); }
        StringBuilder builder = new StringBuilder();
        foreach (char ch in result)
        {
            if (Char.IsLetterOrDigit(ch)) { builder.Append(ch); }
        }
        return builder.ToString();
    }
    private static string BuildGpuDetail(string gpuName, ulong adapterRam, string driver, string display)
    {
        List<string> parts = new List<string>();
        if (adapterRam > 0)
        {
            parts.Add("VRAM " + FormatMemoryBytes(adapterRam));
        }
        if (!String.IsNullOrWhiteSpace(display) && display.IndexOf("x@", StringComparison.OrdinalIgnoreCase) < 0 && display.IndexOf("@0", StringComparison.OrdinalIgnoreCase) < 0)
        {
            parts.Add(display);
        }

        string driverLabel = BuildGpuDriverLabel(gpuName, driver);
        if (!String.IsNullOrWhiteSpace(driverLabel))
        {
            parts.Add(driverLabel);
        }
        return parts.Count > 0 ? String.Join(" | ", parts.ToArray()) : "GPU detail unavailable";
    }

    private static string BuildGpuDriverLabel(string gpuName, string driverVersion)
    {
        driverVersion = CleanHardwareValue(driverVersion);
        if (String.IsNullOrWhiteSpace(driverVersion)) { return ""; }

        if (HardwareNameContains(gpuName, "nvidia") || HardwareNameContains(gpuName, "geforce") || HardwareNameContains(gpuName, "rtx") || HardwareNameContains(gpuName, "gtx"))
        {
            string nvidiaVersion = TryFormatNvidiaDriverVersion(driverVersion);
            return "driver NVIDIA " + ShortenText(String.IsNullOrWhiteSpace(nvidiaVersion) ? driverVersion : nvidiaVersion, 18);
        }
        if (HardwareNameContains(gpuName, "amd") || HardwareNameContains(gpuName, "radeon"))
        {
            return "driver AMD " + ShortenText(driverVersion, 18);
        }
        if (HardwareNameContains(gpuName, "intel") || HardwareNameContains(gpuName, "arc") || HardwareNameContains(gpuName, "iris") || HardwareNameContains(gpuName, "uhd"))
        {
            return "driver Intel " + ShortenText(driverVersion, 18);
        }
        return "driver " + ShortenText(driverVersion, 18);
    }

    private static bool HardwareNameContains(string value, string token)
    {
        return !String.IsNullOrWhiteSpace(value) && !String.IsNullOrWhiteSpace(token) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string TryFormatNvidiaDriverVersion(string windowsDriverVersion)
    {
        try
        {
            string[] parts = (windowsDriverVersion ?? "").Split('.');
            if (parts.Length < 4) { return ""; }
            int branch;
            int build;
            if (!Int32.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out branch)) { return ""; }
            if (!Int32.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out build)) { return ""; }
            if (build <= 0) { return ""; }

            string packed = Math.Abs(branch % 10).ToString(CultureInfo.InvariantCulture) + build.ToString("D4", CultureInfo.InvariantCulture);
            if (packed.Length < 3) { return ""; }
            return packed.Substring(0, packed.Length - 2) + "." + packed.Substring(packed.Length - 2);
        }
        catch
        {
            return "";
        }
    }

    private static string BuildSystemMemoryDetail(MemorySnapshot memory)
    {
        List<string> parts = new List<string>();
        if (memory.AvailablePhysical > 0) { parts.Add("RAM " + FormatMemoryBytes(memory.AvailablePhysical) + " free"); }
        if (memory.AvailablePageFile > 0 && memory.TotalPageFile > 0)
        {
            parts.Add("pagefile " + FormatMemoryBytes(memory.AvailablePageFile) + " free");
        }
        if (memory.MemoryLoad > 0)
        {
            parts.Add("load " + memory.MemoryLoad.ToString(CultureInfo.CurrentCulture) + "%");
        }
        return parts.Count > 0 ? String.Join(" | ", parts.ToArray()) : "-";
    }

    private static string BuildRamDetail(List<RamModuleInfo> modules)
    {
        if (modules == null || modules.Count == 0)
        {
            return "DIMM details unavailable";
        }

        int count = modules.Count;
        int maxSpeed = 0;
        ulong installedBytes = 0;
        string model = "";
        foreach (RamModuleInfo module in modules)
        {
            if (module == null) { continue; }
            if (module.SpeedMhz > maxSpeed) { maxSpeed = module.SpeedMhz; }
            installedBytes += module.CapacityBytes;
            if (String.IsNullOrWhiteSpace(model))
            {
                model = (module.Manufacturer + " " + module.PartNumber).Trim();
            }
        }

        List<string> parts = new List<string>();
        parts.Add(count.ToString(CultureInfo.CurrentCulture) + (count == 1 ? " module" : " modules"));
        if (installedBytes > 0) { parts.Add(FormatMemoryBytes(installedBytes) + " installed"); }
        if (maxSpeed > 0) { parts.Add(maxSpeed.ToString(CultureInfo.CurrentCulture) + " MHz"); }
        if (!String.IsNullOrWhiteSpace(model)) { parts.Add(ShortenText(model, 42)); }
        return String.Join(" | ", parts.ToArray());
    }

    private static string ReadRegistryString(string keyPath, string valueName)
    {
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                object value = key == null ? null : key.GetValue(valueName);
                return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }
        catch
        {
            return "";
        }
    }

    private static int ReadRegistryInt(string keyPath, string valueName)
    {
        int value;
        return TryReadRegistryIntValue(keyPath, valueName, out value) ? value : 0;
    }

    private static bool TryReadRegistryIntValue(string keyPath, string valueName, out int value)
    {
        value = 0;
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                object raw = key == null ? null : key.GetValue(valueName);
                if (raw == null) { return false; }
                if (raw is int) { value = (int)raw; return true; }
                if (raw is long) { value = unchecked((int)(long)raw); return true; }
                if (raw is uint) { value = unchecked((int)(uint)raw); return true; }
                if (raw is string)
                {
                    int parsed;
                    if (Int32.TryParse((string)raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                    {
                        value = parsed;
                        return true;
                    }
                }
                value = ParseInt(Convert.ToString(raw, CultureInfo.InvariantCulture), 0);
                return true;
            }
        }
        catch
        {
            value = 0;
            return false;
        }
    }

    private static bool RegistrySubKeyExists(string keyPath)
    {
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                return key != null;
            }
        }
        catch
        {
            return false;
        }
    }

    private static string CleanHardwareValue(string value)
    {
        if (String.IsNullOrWhiteSpace(value)) { return ""; }
        string clean = value.Trim();
        while (clean.IndexOf("  ", StringComparison.Ordinal) >= 0)
        {
            clean = clean.Replace("  ", " ");
        }
        return ShortenText(clean, 88);
    }

    private static string ShortenText(string value, int maxLength)
    {
        if (String.IsNullOrWhiteSpace(value)) { return ""; }
        if (maxLength < 4 || value.Length <= maxLength) { return value; }
        return value.Substring(0, maxLength - 3).TrimEnd() + "...";
    }

    private static int ParseInt(string value, int fallback)
    {
        int result;
        return Int32.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : fallback;
    }

    private static ulong ParseUInt64(string value, ulong fallback)
    {
        ulong result;
        return UInt64.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : fallback;
    }

    private static string FormatMhz(int mhz)
    {
        if (mhz >= 1000)
        {
            return (mhz / 1000.0).ToString("0.##", CultureInfo.CurrentCulture) + " GHz";
        }
        return mhz.ToString(CultureInfo.CurrentCulture) + " MHz";
    }

    private static string FormatMemoryBytes(ulong bytes)
    {
        if (bytes <= 0) { return "0 MB"; }
        double gb = bytes / 1024.0 / 1024.0 / 1024.0;
        if (gb >= 0.95)
        {
            return gb.ToString("0.#", CultureInfo.CurrentCulture) + " GB";
        }
        double mb = bytes / 1024.0 / 1024.0;
        return mb.ToString("0", CultureInfo.CurrentCulture) + " MB";
    }

    private static string ExtractLogField(string line, string key)
    {
        if (String.IsNullOrWhiteSpace(line) || String.IsNullOrWhiteSpace(key)) { return ""; }
        string marker = key + "=";
        int start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) { return ""; }
        start += marker.Length;
        int end = line.IndexOf(' ', start);
        if (end < 0) { end = line.Length; }
        return line.Substring(start, end - start).Trim();
    }

    private static string BuildTrayTelemetryText()
    {
        HardwareSnapshot hardware = GetHardwareSnapshot();
        string line = ReadLastApplyLogLine();
        string targets = ExtractLogField(line, "targets");
        string delta = ExtractLogField(line, "deltaMB");
        if (String.IsNullOrWhiteSpace(targets)) { targets = "0"; }
        if (String.IsNullOrWhiteSpace(delta)) { delta = "0"; }
        string free = hardware != null && hardware.AvailableMemoryMB > 0
            ? FormatMemoryBytes((ulong)(hardware.AvailableMemoryMB * 1024.0 * 1024.0))
            : "-";
        return "Smart Nap " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + Environment.NewLine +
            "RAM livre " + free + Environment.NewLine +
            "Apps " + targets + " | Purga " + delta + " MB";
    }

    private static string LimitNotifyText(string text, int maxLength)
    {
        if (String.IsNullOrWhiteSpace(text)) { return AppName; }
        string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        return normalized.Length <= maxLength ? normalized : normalized.Substring(0, Math.Max(1, maxLength - 1));
    }

    private static void ApplyDarkWindowFrame(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) { return; }
        int enabled = 1;
        TryDwmSetWindowAttribute(hwnd, 20, enabled);
        TryDwmSetWindowAttribute(hwnd, 19, enabled);
        TryDwmSetWindowAttribute(hwnd, 35, ColorTranslator.ToWin32(Color.FromArgb(5, 9, 15)));
        TryDwmSetWindowAttribute(hwnd, 36, ColorTranslator.ToWin32(Color.FromArgb(244, 247, 251)));
        TryDwmSetWindowAttribute(hwnd, 34, ColorTranslator.ToWin32(Color.FromArgb(255, 166, 41)));
        AllowWindowCapture(hwnd);
    }

    private static void AllowWindowCapture(IntPtr hwnd)
    {
        try
        {
            const uint WdaNone = 0;
            SetWindowDisplayAffinity(hwnd, WdaNone);
        }
        catch
        {
        }
    }

    private static void TryDwmSetWindowAttribute(IntPtr hwnd, int attribute, int value)
    {
        try
        {
            DwmSetWindowAttribute(hwnd, attribute, ref value, Marshal.SizeOf(typeof(int)));
        }
        catch
        {
        }
    }

    private static string GetWritableAppRoot()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string ownerLocal = GetInstallOwnerLocalAppData();
        List<string> candidates = new List<string>();
        if (!String.IsNullOrWhiteSpace(ownerLocal))
        {
            candidates.Add(Path.Combine(ownerLocal, "SmartBackgroundNap"));
            candidates.Add(Path.Combine(ownerLocal, "Programs", "SmartBackgroundNap"));
        }
        candidates.Add(Path.Combine(local, "SmartBackgroundNap"));
        candidates.Add(Path.Combine(local, "Programs", "SmartBackgroundNap"));
        candidates.Add(Path.Combine(Path.GetTempPath(), "SmartBackgroundNap"));

        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] uniqueCandidates = candidates
            .Where(delegate(string item) { return !String.IsNullOrWhiteSpace(item) && seen.Add(Path.GetFullPath(item)); })
            .ToArray();

        Exception last = null;
        foreach (string candidate in uniqueCandidates)
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

    private static string GetInstallOwnerLocalAppData()
    {
        try
        {
            string exePath = Application.ExecutablePath;
            if (String.IsNullOrWhiteSpace(exePath)) { return ""; }

            string normalized = exePath.Replace('/', '\\');
            const string marker = "\\AppData\\Local\\Programs\\SmartBackgroundNap\\";
            int markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex <= 0) { return ""; }

            string profileRoot = normalized.Substring(0, markerIndex);
            if (String.IsNullOrWhiteSpace(profileRoot)) { return ""; }
            string ownerLocal = Path.Combine(profileRoot, "AppData", "Local");
            return Directory.Exists(ownerLocal) ? ownerLocal : "";
        }
        catch
        {
            return "";
        }
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

    private static bool TryReadJsonMapStrict(string path, out IDictionary<string, object> map)
    {
        map = null;
        try
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) { return false; }
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (String.IsNullOrWhiteSpace(json)) { return false; }
            map = JsonCompat.DeserializeObject(json);
            return map != null;
        }
        catch
        {
            map = null;
            return false;
        }
    }

    private static IDictionary<string, object> LoadJsonMapWithRecovery(string path)
    {
        IDictionary<string, object> map;
        if (TryReadJsonMapStrict(path, out map)) { return map; }

        string lastGood = String.IsNullOrWhiteSpace(path) ? "" : path + ".lastgood";
        if (TryReadJsonMapStrict(lastGood, out map))
        {
            try { AtomicWriteAllText(path, JsonCompat.SerializeObject(map), Encoding.UTF8); } catch { }
            return map;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static void AtomicWriteAllText(string path, string content, Encoding encoding)
    {
        if (String.IsNullOrWhiteSpace(path)) { return; }
        string dir = Path.GetDirectoryName(path);
        if (!String.IsNullOrWhiteSpace(dir)) { Directory.CreateDirectory(dir); }

        string tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string replaceBackup = path + ".replace-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string lastGood = path + ".lastgood";
        bool moved = false;

        try
        {
            DurableWriteAllText(tempPath, content ?? String.Empty, encoding ?? Encoding.UTF8);
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tempPath, path, replaceBackup, true);
                    moved = true;
                }
                catch
                {
                    File.Copy(tempPath, path, true);
                    moved = true;
                }
            }
            else
            {
                File.Move(tempPath, path);
                moved = true;
            }

            if (moved && File.Exists(path))
            {
                try { DurableCopyFile(path, lastGood); } catch { }
            }
        }
        finally
        {
            try { if (!moved && File.Exists(tempPath)) { File.Delete(tempPath); } } catch { }
            try { if (File.Exists(replaceBackup)) { File.Delete(replaceBackup); } } catch { }
        }
    }

    private static void DurableWriteAllText(string path, string content, Encoding encoding)
    {
        Encoding writerEncoding = encoding ?? Encoding.UTF8;
        byte[] preamble = writerEncoding.GetPreamble();
        byte[] payload = writerEncoding.GetBytes(content ?? String.Empty);
        using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 65536, FileOptions.WriteThrough))
        {
            if (preamble != null && preamble.Length > 0)
            {
                stream.Write(preamble, 0, preamble.Length);
            }
            if (payload.Length > 0)
            {
                stream.Write(payload, 0, payload.Length);
            }
            stream.Flush(true);
        }
    }

    private static void DurableCopyFile(string sourcePath, string targetPath)
    {
        string dir = Path.GetDirectoryName(targetPath);
        if (!String.IsNullOrWhiteSpace(dir)) { Directory.CreateDirectory(dir); }
        using (FileStream source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (FileStream target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read, 65536, FileOptions.WriteThrough))
        {
            source.CopyTo(target);
            target.Flush(true);
        }
    }

    private static void AtomicWriteJsonMap(string path, IDictionary<string, object> map)
    {
        AtomicWriteAllText(path, JsonCompat.SerializeObject(map ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)), Encoding.UTF8);
    }

    private static IDictionary<string, object> LoadUiSettings()
    {
        try
        {
            return LoadJsonMapWithRecovery(uiSettingsPath);
        }
        catch
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveUiSettings(IDictionary<string, object> settings)
    {
        try
        {
            AtomicWriteJsonMap(uiSettingsPath, settings ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
        }
    }

    private static string GetUiSettingString(string key)
    {
        IDictionary<string, object> settings = LoadUiSettings();
        object value;
        return settings != null && settings.TryGetValue(key, out value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
    }

    private static string LoadUiLanguage()
    {
        return NormalizeUiLanguage(GetUiSettingString("Language"));
    }

    private static void SaveUiLanguage(string language)
    {
        string normalized = NormalizeUiLanguage(language);
        if (String.IsNullOrWhiteSpace(normalized)) { return; }
        uiLanguage = normalized;
        IDictionary<string, object> settings = LoadUiSettings();
        settings["Language"] = normalized;
        SaveUiSettings(settings);
    }

    private static string LoadDismissedUpdateTag()
    {
        return Convert.ToString(GetUiSettingString("DismissedUpdateTag"), CultureInfo.InvariantCulture).Trim();
    }

    private static void SaveDismissedUpdateTag(string tag)
    {
        if (String.IsNullOrWhiteSpace(tag)) { return; }
        IDictionary<string, object> settings = LoadUiSettings();
        settings["DismissedUpdateTag"] = tag.Trim();
        settings["DismissedUpdateAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        SaveUiSettings(settings);
    }

    private static void SavePendingPostUpdateNotice(string version, string body)
    {
        try
        {
            string normalized = NormalizeVersionLabel(version);
            if (String.IsNullOrWhiteSpace(normalized)) { return; }
            IDictionary<string, object> settings = LoadUiSettings();
            settings["PendingPostUpdateVersion"] = normalized;
            settings["PendingPostUpdateBody"] = String.IsNullOrWhiteSpace(body) ? "" : body.Trim();
            settings["PendingPostUpdateAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            SaveUiSettings(settings);
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
        }
    }

    private static bool ShouldShowPostUpdateNotice()
    {
        string pending = NormalizeVersionLabel(GetUiSettingString("PendingPostUpdateVersion"));
        if (String.IsNullOrWhiteSpace(pending)) { return false; }
        if (!String.Equals(pending, NormalizeVersionLabel(AppVersion), StringComparison.OrdinalIgnoreCase)) { return false; }
        string seen = NormalizeVersionLabel(GetUiSettingString("PostUpdateNoticeSeenVersion"));
        return !String.Equals(seen, pending, StringComparison.OrdinalIgnoreCase);
    }

    private static void MarkPostUpdateNoticeSeen()
    {
        try
        {
            IDictionary<string, object> settings = LoadUiSettings();
            settings["PostUpdateNoticeSeenVersion"] = NormalizeVersionLabel(AppVersion);
            settings["PostUpdateNoticeSeenAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            SaveUiSettings(settings);
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
        }
    }

    private static string GetPostUpdateNoticeBody()
    {
        string body = GetUiSettingString("PendingPostUpdateBody");
        if (String.IsNullOrWhiteSpace(body)) { return ""; }
        string compact = Regex.Replace(body, @"\s+", " ").Trim();
        if (compact.Length > 180) { compact = compact.Substring(0, 180).TrimEnd() + "..."; }
        return compact;
    }

    private static List<string> GetPostUpdateNoticeItems()
    {
        List<string> items = new List<string>();
        string body = GetUiSettingString("PendingPostUpdateBody");
        if (String.IsNullOrWhiteSpace(body)) { return items; }
        string[] lines = body.Replace("\r", "\n").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            line = Regex.Replace(line, @"^[#>*\-\s]+", "").Trim();
            line = Regex.Replace(line, @"`([^`]+)`", "$1");
            line = Regex.Replace(line, @"\[([^\]]+)\]\([^\)]+\)", "$1");
            if (String.IsNullOrWhiteSpace(line)) { continue; }
            if (line.Length > 96) { line = line.Substring(0, 96).TrimEnd() + "..."; }
            items.Add(line);
            if (items.Count >= 5) { break; }
        }
        return items;
    }
    private static bool LoadAutoUpdateChecks()
    {
        string value = GetUiSettingString("AutoUpdateChecks").Trim();
        if (String.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }

    private static void SaveAutoUpdateChecks(bool enabled)
    {
        IDictionary<string, object> settings = LoadUiSettings();
        settings["AutoUpdateChecks"] = enabled ? "true" : "false";
        settings["AutoUpdateChecksChangedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        SaveUiSettings(settings);
    }

    private static int ClampEnergyIdleMinutes(int minutes)
    {
        if (minutes <= 0) { minutes = EnergyIdleDefaultMinutes; }
        return Math.Max(EnergyIdleMinMinutes, Math.Min(EnergyIdleMaxMinutes, minutes));
    }

    private static int LoadEnergyIdleGuardMinutes()
    {
        int parsed;
        return ClampEnergyIdleMinutes(Int32.TryParse(GetUiSettingString("EnergyIdleGuardMinutes"), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : EnergyIdleDefaultMinutes);
    }

    private static bool LoadEnergyIdleGuardEnabled()
    {
        string value = GetUiSettingString("EnergyIdleGuardEnabled").Trim();
        return String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LoadEnergyIdleGuardConfigured()
    {
        return !String.IsNullOrWhiteSpace(GetUiSettingString("EnergyIdleGuardConfigured"));
    }

    private static void SaveEnergyIdleGuard(bool enabled, int minutes)
    {
        IDictionary<string, object> settings = LoadUiSettings();
        settings["EnergyIdleGuardEnabled"] = enabled ? "true" : "false";
        settings["EnergyIdleGuardMinutes"] = ClampEnergyIdleMinutes(minutes).ToString(CultureInfo.InvariantCulture);
        settings["EnergyIdleGuardConfigured"] = "true";
        settings["EnergyIdleGuardChangedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        SaveUiSettings(settings);
    }

    private static bool ReadUiFlag(string key)
    {
        string value = GetUiSettingString(key).Trim();
        return String.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static void SaveUiFlag(string key, bool enabled)
    {
        IDictionary<string, object> settings = LoadUiSettings();
        settings[key] = enabled ? "true" : "false";
        settings[key + "ChangedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        SaveUiSettings(settings);
    }

    private static bool ShouldApplyFirstRunDefaults()
    {
        if (ReadUiFlag("InitialDefaultsApplied")) { return false; }
        try
        {
            if (!String.IsNullOrWhiteSpace(logPath) && File.Exists(logPath) && new FileInfo(logPath).Length > 0) { return false; }
        }
        catch
        {
        }
        return !IsAutomaticEngineEnabled() && !IsStartupInstalled();
    }

    private static void MarkInitialDefaultsApplied(string summary)
    {
        IDictionary<string, object> settings = LoadUiSettings();
        settings["InitialDefaultsApplied"] = "true";
        settings["InitialDefaultsAppliedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        settings["InitialDefaultsSummary"] = summary ?? "";
        SaveUiSettings(settings);
    }

    private static void EnsureFirstRunDefaults()
    {
        lock (firstRunDefaultsLock)
        {
            if (firstRunDefaultsChecked) { return; }
            firstRunDefaultsChecked = true;
        }

        if (!ShouldApplyFirstRunDefaults()) { return; }

        RunResult install = IsAutomaticEngineEnabled() && IsStartupInstalled()
            ? new RunResult(0, "Initial defaults already prepared.")
            : InstallComplete(false);
        EnsureSmartLearningDefaultEnabled();
        SaveAutoUpdateChecks(true);
        string summary = "install=" + install.ExitCode.ToString(CultureInfo.InvariantCulture) + "; auto=" + IsAutomaticEngineEnabled().ToString(CultureInfo.InvariantCulture) + "; startup=" + IsStartupInstalled().ToString(CultureInfo.InvariantCulture) + "; sessionAgent=" + IsTaskInstalled(SessionAgentTaskName).ToString(CultureInfo.InvariantCulture) + "; coreService=" + IsCoreServiceInstalled().ToString(CultureInfo.InvariantCulture) + "; adminSetup=" + IsAdminSetupCurrentForVersion().ToString(CultureInfo.InvariantCulture);
        MarkInitialDefaultsApplied(summary);
        AppendOperationalLog("action=first-run-defaults " + summary);
    }
    private static void EnsureInstallRepairOnLaunch()
    {
        try
        {
            if (IsCurrentProcessElevated()) { return; }
            if (!ReadUiFlag("InitialDefaultsApplied")) { return; }

            bool wantsAuto = IsAutomaticEngineEnabled();
            bool wantsStartup = IsStartupInstalled();
            bool needsRepair = (wantsAuto && !IsTaskInstalled(AutoTaskName)) || (wantsStartup && !IsTaskInstalled(TrayTaskName)) || !IsTaskInstalled(DashboardTaskName) || !IsTaskInstalled(SessionAgentTaskName) || !IsCoreServiceInstalled() || !IsCoreServiceRunning();
            if (!needsRepair) { return; }
            if (WasAdminSetupPromptedForCurrentVersion() && !WasAdminSetupCompletedForCurrentVersion()) { return; }
            if (!ShouldAttemptElevatedSetupRepair()) { return; }

            MarkElevatedSetupRepairAttempt();
            RunResult setup = RunElevatedInstallComplete();
            MarkAdminSetupCompletedIfReady("launch-repair", setup);
            AppendOperationalLog("action=launch-install-repair exitCode=" + setup.ExitCode.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
        }
    }

    private static bool ShouldAttemptElevatedSetupRepair()
    {
        string value = GetUiSettingString("LastElevatedSetupRepairAttemptUtc");
        if (String.IsNullOrWhiteSpace(value)) { return true; }
        DateTime last;
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out last)) { return true; }
        return (DateTime.UtcNow - last) > TimeSpan.FromHours(12);
    }

    private static void MarkElevatedSetupRepairAttempt()
    {
        IDictionary<string, object> settings = LoadUiSettings();
        settings["LastElevatedSetupRepairAttemptUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        SaveUiSettings(settings);
    }
    private static bool WasAdminSetupPromptedForCurrentVersion()
    {
        return String.Equals(GetUiSettingString("AdminSetupPromptedVersion").Trim(), AppVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static bool WasAdminSetupCompletedForCurrentVersion()
    {
        return String.Equals(GetUiSettingString("AdminSetupCompletedVersion").Trim(), AppVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdminSetupCurrentForVersion()
    {
        return WasAdminSetupCompletedForCurrentVersion() && ArePrimaryScheduledTasksInstalled() && IsCoreServiceInstalled() && IsCoreServiceRunning();
    }

    private static bool ShouldRequestAdminSetupForCurrentVersion()
    {
        if (IsCurrentProcessElevated()) { return false; }
        if (IsAdminSetupCurrentForVersion()) { return false; }
        return !WasAdminSetupPromptedForCurrentVersion();
    }

    private static void MarkAdminSetupPromptedForCurrentVersion(string source)
    {
        try
        {
            IDictionary<string, object> settings = LoadUiSettings();
            settings["AdminSetupPromptedVersion"] = AppVersion;
            settings["AdminSetupPromptedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            settings["AdminSetupPromptSource"] = source ?? "setup";
            SaveUiSettings(settings);
        }
        catch
        {
        }
    }

    private static void MarkAdminSetupCompletedForCurrentVersion(string source)
    {
        try
        {
            IDictionary<string, object> settings = LoadUiSettings();
            settings["AdminSetupPromptedVersion"] = AppVersion;
            settings["AdminSetupCompletedVersion"] = AppVersion;
            settings["AdminSetupCompletedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            settings["AdminSetupRunLevel"] = "HighestAvailable+CoreService+SessionAgent";
            settings["AdminSetupSource"] = source ?? "setup";
            SaveUiSettings(settings);
            SaveLocalAutoEngine(false);
            RemoveStartupRegistry();
            AppendOperationalLog("action=admin-setup status=completed source=" + SanitizeLogToken(source));
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
        }
    }

    private static void MarkAdminSetupCompletedIfReady(string source, RunResult result)
    {
        try
        {
            if (result != null && result.ExitCode != 0) { return; }
            if (!ArePrimaryScheduledTasksInstalled()) { return; }
            if (!IsCoreServiceInstalled()) { return; }
            if (!IsCoreServiceRunning()) { return; }
            MarkAdminSetupCompletedForCurrentVersion(source);
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
        }
    }

    private static void EnsureAdminSetupForCurrentVersion()
    {
        try
        {
            if (IsAdminSetupCurrentForVersion()) { return; }

            if (IsCurrentProcessElevated())
            {
                RunResult install = InstallComplete(false);
                MarkAdminSetupCompletedIfReady("elevated-launch", install);
                return;
            }

            if (!ShouldRequestAdminSetupForCurrentVersion()) { return; }

            MarkAdminSetupPromptedForCurrentVersion("first-launch");
            RunResult setup = RunElevatedInstallComplete();
            MarkAdminSetupCompletedIfReady("first-launch", setup);
            AppendOperationalLog("action=admin-setup status=attempted exitCode=" + setup.ExitCode.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
        }
    }
    private static void EnsureSmartLearningDefaultEnabled()
    {
        try
        {
            if (!IsSmartLearningEnabled())
            {
                SetSmartLearningEnabled(true);
            }
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
        ExtractResource("readme_showcase_png", Path.Combine(runtimeRoot, "docs\\images\\smart-nap-showcase.png"));
        ExtractResource("readme_social_preview_png", Path.Combine(runtimeRoot, "docs\\images\\smart-nap-social-preview.png"));
        ExtractResource("readme_about_panel_png", Path.Combine(runtimeRoot, "docs\\images\\smart-nap-about-panel.png"));
        ExtractResource("readme_engine_story_png", Path.Combine(runtimeRoot, "docs\\images\\smart-nap-engine-story.png"));
        ExtractResource("readme_intelligence_png", Path.Combine(runtimeRoot, "docs\\images\\smart-nap-intelligence.png"));
        ExtractResource("icon_ico", Path.Combine(runtimeRoot, "assets\\smart-nap-logo.ico"));
        ExtractResource("logo_png", Path.Combine(runtimeRoot, "assets\\smart-nap-logo-v2.png"));
        ExtractResource("hero_png", Path.Combine(runtimeRoot, "assets\\smart-nap-hero-bg.png"));
        ExtractResource("power_hone_base_pow", Path.Combine(runtimeRoot, "assets\\power\\smart-nap-hone-base.pow"));
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
            AtomicWriteAllText(targetPath, defaultJson, Encoding.UTF8);
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
                AtomicWriteJsonMap(targetPath, current);
            }
        }
        catch
        {
            // Keep the existing user config if it cannot be merged safely.
        }
    }


    private static void MigrateConfigForCurrentRuntime()
    {
        try
        {
            if (String.IsNullOrWhiteSpace(configPath) || String.IsNullOrWhiteSpace(userConfigPath) || !File.Exists(configPath)) { return; }

            IDictionary<string, object> defaults;
            if (!TryReadConfigFile(configPath, out defaults) || defaults == null) { return; }

            IDictionary<string, object> user = null;
            bool hasUserConfig = File.Exists(userConfigPath);
            if (hasUserConfig)
            {
                if (!TryReadConfigFile(userConfigPath, out user) || user == null)
                {
                    BackupBrokenConfigFile(userConfigPath);
                    user = LoadPreviousRuntimeConfig(configPath);
                    if (user == null) { user = defaults; }
                }
            }
            else
            {
                user = LoadPreviousRuntimeConfig(configPath);
            }

            if (user == null) { return; }

            bool changed = MergeMissingConfigValues(user, defaults);
            if (!hasUserConfig || changed)
            {
                string dir = Path.GetDirectoryName(userConfigPath);
                if (!String.IsNullOrWhiteSpace(dir)) { Directory.CreateDirectory(dir); }
                AtomicWriteJsonMap(userConfigPath, user);
            }
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
        }
    }

    private static void CleanupOldRuntimeFolders(string currentRuntimeRoot)
    {
        try
        {
            if (usingLooseRuntime || String.IsNullOrWhiteSpace(appRoot) || !Directory.Exists(appRoot)) { return; }
            string root = SafeFullPath(appRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string current = SafeFullPath(currentRuntimeRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string dir in Directory.GetDirectories(appRoot, "runtime-*"))
            {
                string full = SafeFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (String.IsNullOrWhiteSpace(full) || String.Equals(full, current, StringComparison.OrdinalIgnoreCase)) { continue; }
                if (String.IsNullOrWhiteSpace(root) || !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { continue; }
                try
                {
                    Directory.Delete(full, true);
                    AppendOperationalLog("action=runtime-cleanup status=removed folder=" + SanitizeLogToken(Path.GetFileName(full)));
                }
                catch (Exception ex)
                {
                    AppendOperationalLog("action=runtime-cleanup status=deferred folder=" + SanitizeLogToken(Path.GetFileName(full)) + " detail=" + ShortTaskError(ex.Message));
                }
            }
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
        }
    }

    private static bool TryReadConfigFile(string path, out IDictionary<string, object> config)
    {
        config = null;
        try
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) { return false; }
            config = LoadJsonMapWithRecovery(path);
            return config != null && config.Count > 0;
        }
        catch
        {
            config = null;
            return false;
        }
    }

    private static IDictionary<string, object> LoadPreviousRuntimeConfig(string currentConfigPath)
    {
        try
        {
            if (String.IsNullOrWhiteSpace(appRoot) || !Directory.Exists(appRoot)) { return null; }
            string currentFull = SafeFullPath(currentConfigPath);
            string userFull = SafeFullPath(userConfigPath);
            List<string> candidates = new List<string>();
            string rootConfig = Path.Combine(appRoot, "game-session.config.json");
            if (File.Exists(rootConfig)) { candidates.Add(rootConfig); }

            foreach (string dir in Directory.GetDirectories(appRoot, "runtime-*"))
            {
                string candidate = Path.Combine(dir, "game-session.config.json");
                if (File.Exists(candidate)) { candidates.Add(candidate); }
            }

            candidates.Sort(delegate(string left, string right)
            {
                DateTime r = File.GetLastWriteTimeUtc(right);
                DateTime l = File.GetLastWriteTimeUtc(left);
                return r.CompareTo(l);
            });

            foreach (string candidate in candidates)
            {
                string full = SafeFullPath(candidate);
                if (!String.IsNullOrWhiteSpace(currentFull) && String.Equals(full, currentFull, StringComparison.OrdinalIgnoreCase)) { continue; }
                if (!String.IsNullOrWhiteSpace(userFull) && String.Equals(full, userFull, StringComparison.OrdinalIgnoreCase)) { continue; }
                IDictionary<string, object> previous;
                if (TryReadConfigFile(candidate, out previous)) { return previous; }
            }
        }
        catch
        {
        }
        return null;
    }

    private static void BackupBrokenConfigFile(string path)
    {
        try
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) { return; }
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string backup = Path.Combine(String.IsNullOrWhiteSpace(dir) ? appRoot : dir, name + ".broken-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".json");
            File.Copy(path, backup, true);
        }
        catch
        {
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
                continue;
            }

            System.Collections.IList existingList = existing as System.Collections.IList;
            System.Collections.IEnumerable defaultList = pair.Value as System.Collections.IEnumerable;
            if (existingList != null && defaultList != null && !(pair.Value is string) && MergeMissingScalarListValues(existingList, defaultList))
            {
                changed = true;
            }
        }
        return changed;
    }

    private static bool MergeMissingScalarListValues(System.Collections.IList current, System.Collections.IEnumerable defaults)
    {
        bool changed = false;
        foreach (object item in defaults)
        {
            if (item == null || item is IDictionary<string, object> || (item is System.Collections.IEnumerable && !(item is string))) { continue; }
            string text = Convert.ToString(item, CultureInfo.InvariantCulture);
            if (String.IsNullOrWhiteSpace(text)) { continue; }

            bool exists = false;
            foreach (object existing in current)
            {
                string existingText = Convert.ToString(existing, CultureInfo.InvariantCulture);
                if (String.Equals(existingText, text, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                current.Add(item);
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

    private static string GetArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (String.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1] ?? "";
            }
        }
        return "";
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
        object outputLock = new object();
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

                DataReceivedEventHandler capture = delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) { return; }
                    lock (outputLock)
                    {
                        output.AppendLine(e.Data);
                    }
                };
                process.OutputDataReceived += capture;
                process.ErrorDataReceived += capture;
                try { process.BeginOutputReadLine(); } catch { }
                try { process.BeginErrorReadLine(); } catch { }

                DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                bool timedOut = false;
                while (!process.WaitForExit(150))
                {
                    if (control != null && control.CancelRequested)
                    {
                        try { process.Kill(); } catch { }
                        lock (outputLock) { output.AppendLine("Stopped by user."); }
                        break;
                    }
                    if (DateTime.UtcNow > deadline)
                    {
                        try { process.Kill(); } catch { }
                        lock (outputLock) { output.AppendLine("Timed out."); }
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
                    lock (outputLock)
                    {
                        return new RunResult(1, (output.ToString() + Environment.NewLine + "Process did not exit after stop request.").Trim());
                    }
                }

                try { process.WaitForExit(); } catch { }

                if (control != null && control.CancelRequested)
                {
                    lock (outputLock) { return new RunResult(130, output.ToString().Trim()); }
                }
                if (timedOut)
                {
                    lock (outputLock) { return new RunResult(124, output.ToString().Trim()); }
                }

                lock (outputLock) { return new RunResult(process.ExitCode, output.ToString().Trim()); }
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

    private static string GetPowerCfgPath()
    {
        string systemPowerCfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "powercfg.exe");
        return File.Exists(systemPowerCfg) ? systemPowerCfg : "powercfg.exe";
    }

    private static string NormalizeEnergyChoice(string choice)
    {
        if (String.IsNullOrWhiteSpace(choice)) { return "keep"; }
        string value = choice.Trim();
        if (String.Equals(value, "activate", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "smart", StringComparison.OrdinalIgnoreCase)) { return "activate"; }
        if (String.Equals(value, "restore", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "previous", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "anterior", StringComparison.OrdinalIgnoreCase)) { return "restore"; }
        if (String.Equals(value, "balanced", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "equilibrado", StringComparison.OrdinalIgnoreCase)) { return "balanced"; }
        return "keep";
    }

    private static bool IsSmartNapPowerPlanGuid(string planGuid)
    {
        return String.Equals(planGuid, SmartNapGamePowerPlanGuid, StringComparison.OrdinalIgnoreCase) ||
            String.Equals(planGuid, SmartNapLivePowerPlanGuid, StringComparison.OrdinalIgnoreCase);
    }
    private static string FriendlyPowerPlanName(string planGuid, string parsedName)
    {
        string name = String.IsNullOrWhiteSpace(parsedName) ? "" : parsedName.Trim().Trim('*').Trim();
        if (!String.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        if (String.Equals(planGuid, SmartNapGamePowerPlanGuid, StringComparison.OrdinalIgnoreCase)) { return SmartNapGamePowerPlanName; }
        if (String.Equals(planGuid, SmartNapLivePowerPlanGuid, StringComparison.OrdinalIgnoreCase)) { return SmartNapLivePowerPlanName; }
        if (String.Equals(planGuid, BalancedPowerPlanGuid, StringComparison.OrdinalIgnoreCase)) { return "Equilibrado"; }
        return "";
    }

    private static PowerPlanSnapshot ParsePowerPlanOutput(string output)
    {
        if (String.IsNullOrWhiteSpace(output)) { return null; }
        Match match = Regex.Match(output, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        if (!match.Success) { return null; }
        string name = "";
        int open = output.IndexOf('(', match.Index + match.Length);
        int close = open >= 0 ? output.IndexOf(')', open + 1) : -1;
        if (open >= 0 && close > open) { name = output.Substring(open + 1, close - open - 1).Trim(); }
        if (String.IsNullOrWhiteSpace(name))
        {
            string tail = output.Substring(match.Index + match.Length).Trim();
            tail = tail.Replace("*", "").Trim();
            tail = Regex.Replace(tail, @"^[\s:\-\u2013\u2014]+", "").Trim();
            name = tail.Trim('(', ')', '*', ' ');
        }
        return new PowerPlanSnapshot { Guid = match.Value, Name = FriendlyPowerPlanName(match.Value, name) };
    }

    private static PowerPlanSnapshot ClonePowerPlan(PowerPlanSnapshot snapshot)
    {
        return snapshot == null ? null : new PowerPlanSnapshot { Guid = snapshot.Guid, Name = snapshot.Name };
    }

    private static PowerPlanSnapshot GetCachedPowerPlan(TimeSpan maxAge)
    {
        lock (powerPlanCacheLock)
        {
            if (cachedPowerPlan == null) { return null; }
            if ((DateTime.UtcNow - cachedPowerPlanAtUtc) > maxAge) { return null; }
            return ClonePowerPlan(cachedPowerPlan);
        }
    }

    private static void SaveCachedPowerPlan(PowerPlanSnapshot snapshot)
    {
        if (snapshot == null || String.IsNullOrWhiteSpace(snapshot.Guid)) { return; }
        lock (powerPlanCacheLock)
        {
            cachedPowerPlan = ClonePowerPlan(snapshot);
            cachedPowerPlanAtUtc = DateTime.UtcNow;
        }
    }

    private static PowerPlanSnapshot FindActivePowerPlanFromList()
    {
        RunResult list = RunHidden(GetPowerCfgPath(), "/list", 7000);
        if (list.ExitCode != 0) { list = RunHidden(GetPowerCfgPath(), "/l", 7000); }
        if (list.ExitCode != 0) { return null; }

        foreach (string line in Regex.Split(list.Output ?? "", @"\r?\n"))
        {
            if (line.IndexOf("*", StringComparison.Ordinal) < 0) { continue; }
            PowerPlanSnapshot snapshot = ParsePowerPlanOutput(line);
            if (snapshot != null && !String.IsNullOrWhiteSpace(snapshot.Guid)) { return snapshot; }
        }
        return null;
    }

    private static string ReadPowerPlanFriendlyNameFromRegistry(string guid)
    {
        if (String.IsNullOrWhiteSpace(guid)) { return ""; }
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\" + guid))
            {
                if (key == null) { return ""; }
                return Convert.ToString(key.GetValue("FriendlyName"), CultureInfo.InvariantCulture) ?? "";
            }
        }
        catch { return ""; }
    }

    private static PowerPlanSnapshot FindActivePowerPlanFromRegistry()
    {
        try
        {
            string guid = ReadRegistryString(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActivePowerScheme").Trim();
            if (String.IsNullOrWhiteSpace(guid)) { return null; }
            Match match = Regex.Match(guid, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            if (!match.Success) { return null; }
            string cleanGuid = match.Value;
            string name = ReadPowerPlanFriendlyNameFromRegistry(cleanGuid);
            return new PowerPlanSnapshot { Guid = cleanGuid, Name = FriendlyPowerPlanName(cleanGuid, name) };
        }
        catch { return null; }
    }

    private static PowerPlanSnapshot GetActivePowerPlan()
    {
        PowerPlanSnapshot cached = GetCachedPowerPlan(TimeSpan.FromSeconds(12));
        if (cached != null) { return cached; }

        PowerPlanSnapshot snapshot = null;
        RunResult active = RunHidden(GetPowerCfgPath(), "/getactivescheme", 7000);
        if (active.ExitCode == 0)
        {
            snapshot = ParsePowerPlanOutput(active.Output);
        }

        if (snapshot == null || String.IsNullOrWhiteSpace(snapshot.Guid))
        {
            snapshot = FindActivePowerPlanFromList();
        }
        if (snapshot == null || String.IsNullOrWhiteSpace(snapshot.Guid))
        {
            snapshot = FindActivePowerPlanFromRegistry();
        }
        else if (String.IsNullOrWhiteSpace(snapshot.Name))
        {
            PowerPlanSnapshot fromList = FindActivePowerPlanFromList();
            if (fromList != null && String.Equals(fromList.Guid, snapshot.Guid, StringComparison.OrdinalIgnoreCase) && !String.IsNullOrWhiteSpace(fromList.Name))
            {
                snapshot.Name = fromList.Name;
            }
            else
            {
                PowerPlanSnapshot fromRegistry = FindActivePowerPlanFromRegistry();
                if (fromRegistry != null && String.Equals(fromRegistry.Guid, snapshot.Guid, StringComparison.OrdinalIgnoreCase) && !String.IsNullOrWhiteSpace(fromRegistry.Name))
                {
                    snapshot.Name = fromRegistry.Name;
                }
            }
        }

        if (snapshot != null && !String.IsNullOrWhiteSpace(snapshot.Guid))
        {
            snapshot.Name = FriendlyPowerPlanName(snapshot.Guid, snapshot.Name);
            SaveCachedPowerPlan(snapshot);
            return snapshot;
        }

        return GetCachedPowerPlan(TimeSpan.FromMinutes(10));
    }
    private static PowerPlanSnapshot LoadPreviousPowerPlan()
    {
        try
        {
            string guid = GetUiSettingString("PreviousPowerPlanGuid").Trim();
            string name = GetUiSettingString("PreviousPowerPlanName").Trim();
            if (String.IsNullOrWhiteSpace(guid)) { return null; }
            return new PowerPlanSnapshot { Guid = guid, Name = name };
        }
        catch { return null; }
    }

    private static void SavePreviousPowerPlan(PowerPlanSnapshot snapshot)
    {
        if (snapshot == null || String.IsNullOrWhiteSpace(snapshot.Guid) || IsSmartNapPowerPlanGuid(snapshot.Guid)) { return; }
        IDictionary<string, object> settings = LoadUiSettings();
        settings["PreviousPowerPlanGuid"] = snapshot.Guid;
        settings["PreviousPowerPlanName"] = String.IsNullOrWhiteSpace(snapshot.Name) ? snapshot.Guid : snapshot.Name;
        settings["PreviousPowerPlanSavedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        SaveUiSettings(settings);
    }

    private static void SaveCurrentPowerPlanBeforeSmartActivation()
    {
        PowerPlanSnapshot current = GetActivePowerPlan();
        SavePreviousPowerPlan(current);
    }

    private static bool PowerPlanExists(string planGuid)
    {
        RunResult list = RunHidden(GetPowerCfgPath(), "/list", 7000);
        if (list.ExitCode != 0) { return false; }
        return list.Output.IndexOf(planGuid, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static RunResult RenamePowerPlan(string planGuid, string name, string description)
    {
        return RunHidden(GetPowerCfgPath(), "-changename " + planGuid + " " + Quote(name) + " " + Quote(description), 7000);
    }

    private static RunResult EnsureSmartNapPowerPlan(string planGuid, string name, string description)
    {
        if (String.IsNullOrWhiteSpace(powerBasePlanPath) || !File.Exists(powerBasePlanPath))
        {
            return new RunResult(1, "Missing embedded power plan template.");
        }

        if (!PowerPlanExists(planGuid))
        {
            RunResult import = RunHidden(GetPowerCfgPath(), "-import " + Quote(powerBasePlanPath) + " " + planGuid, 12000);
            if (import.ExitCode != 0) { return new RunResult(import.ExitCode, "Could not import " + name + ": " + import.Output); }
        }

        RunResult rename = RenamePowerPlan(planGuid, name, description);
        if (rename.ExitCode != 0) { return new RunResult(rename.ExitCode, "Power plan exists, but rename failed: " + rename.Output); }
        return new RunResult(0, name + " ready.");
    }

    private static RunResult EnsureSmartNapPowerPlans()
    {
        RunResult game = EnsureSmartNapPowerPlan(SmartNapGamePowerPlanGuid, SmartNapGamePowerPlanName, "Smart Nap gaming power plan based on a high performance Windows profile.");
        if (game.ExitCode != 0) { return game; }
        RunResult live = EnsureSmartNapPowerPlan(SmartNapLivePowerPlanGuid, SmartNapLivePowerPlanName, "Smart Nap live/streaming power plan based on a high performance Windows profile.");
        if (live.ExitCode != 0) { return live; }
        return new RunResult(0, SmartNapGamePowerPlanName + " and " + SmartNapLivePowerPlanName + " ready.");
    }

    private static RunResult ActivatePowerPlan(string planGuid, string name, bool savePrevious)
    {
        if (savePrevious) { SaveCurrentPowerPlanBeforeSmartActivation(); }
        RunResult active = RunHidden(GetPowerCfgPath(), "/setactive " + planGuid, 7000);
        if (active.ExitCode != 0) { return new RunResult(active.ExitCode, "Could not activate " + name + ": " + active.Output); }
        return new RunResult(0, name + " active.");
    }

    private static RunResult RestorePreviousPowerPlan()
    {
        PowerPlanSnapshot previous = LoadPreviousPowerPlan();
        if (previous == null || String.IsNullOrWhiteSpace(previous.Guid))
        {
            return new RunResult(1, "No previous Windows power plan was saved yet.");
        }
        string name = String.IsNullOrWhiteSpace(previous.Name) ? "previous power plan" : previous.Name;
        return ActivatePowerPlan(previous.Guid, name, false);
    }

    private static RunResult ApplyEnergyChoiceForMode(string normalizedMode, string energyChoice)
    {
        string choice = NormalizeEnergyChoice(energyChoice);
        if (String.Equals(normalizedMode, "Gaming", StringComparison.OrdinalIgnoreCase) || String.Equals(normalizedMode, "Competitive", StringComparison.OrdinalIgnoreCase))
        {
            RunResult ensure = EnsureSmartNapPowerPlans();
            if (ensure.ExitCode != 0) { return ensure; }
            if (choice == "activate") { return ActivatePowerPlan(SmartNapGamePowerPlanGuid, SmartNapGamePowerPlanName, true); }
            return new RunResult(0, "Game/competitive power plan is installed. Current Windows plan kept.");
        }
        if (String.Equals(normalizedMode, "Streamer", StringComparison.OrdinalIgnoreCase))
        {
            RunResult ensure = EnsureSmartNapPowerPlans();
            if (ensure.ExitCode != 0) { return ensure; }
            if (choice == "activate") { return ActivatePowerPlan(SmartNapLivePowerPlanGuid, SmartNapLivePowerPlanName, true); }
            return new RunResult(0, "Live power plan is installed. Current Windows plan kept.");
        }
        if (String.Equals(normalizedMode, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            if (choice == "restore") { return RestorePreviousPowerPlan(); }
            if (choice == "balanced") { return ActivatePowerPlan(BalancedPowerPlanGuid, "Balanced", false); }
        }
        return new RunResult(0, "Current Windows power plan kept.");
    }


    private static TimeSpan GetSystemIdleTime()
    {
        try
        {
            LastInputInfo info = new LastInputInfo();
            info.cbSize = (uint)Marshal.SizeOf(typeof(LastInputInfo));
            if (!GetLastInputInfo(ref info)) { return TimeSpan.Zero; }
            uint tick = unchecked((uint)Environment.TickCount);
            uint idleTicks = unchecked(tick - info.dwTime);
            return TimeSpan.FromMilliseconds(idleTicks);
        }
        catch
        {
            return TimeSpan.Zero;
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
        SyncSmartLearningSettingToConfig();
        Directory.CreateDirectory(outputsPath);
        return RunPowerShellScript(backgroundScriptPath, "-ConfigPath " + Quote(GetEffectiveConfigPath()) + " -Action Apply -StateMode Latest -Quiet -LogPath " + Quote(logPath), 180000, control);
    }


    private static RunResult RunPreviewNow()
    {
        SyncSmartLearningSettingToConfig();
        Directory.CreateDirectory(outputsPath);
        return RunPowerShellScript(backgroundScriptPath, "-ConfigPath " + Quote(GetEffectiveConfigPath()) + " -Action Apply -Preview -StateMode None -Quiet -LogPath " + Quote(logPath), 120000);
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

    private static bool ArePrimaryScheduledTasksInstalled()
    {
        return IsTaskInstalled(AutoTaskName) && IsTaskInstalled(TrayTaskName) && IsTaskInstalled(DashboardTaskName) && IsTaskInstalled(SessionAgentTaskName);
    }

    private static bool TryDelegateInteractiveLaunchToElevatedTask()
    {
        try
        {
            if (IsCurrentProcessElevated()) { return false; }
            if (!IsTaskInstalled(DashboardTaskName)) { return false; }
            RunResult result = RunHidden("schtasks.exe", "/Run /TN " + Quote(DashboardTaskName), 12000);
            if (result.ExitCode == 0)
            {
                AppendOperationalLog("action=dashboard-elevated-task status=requested");
                return true;
            }
            AppendOperationalLog("action=dashboard-elevated-task status=failed detail=" + ShortTaskError(result.Output));
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
        }
        return false;
    }
    private static RunResult RunElevatedSetupIfNeeded()
    {
        if (ArePrimaryScheduledTasksInstalled() && IsCoreServiceInstalled() && IsCoreServiceRunning())
        {
            SaveLocalAutoEngine(false);
            RemoveStartupRegistry();
            return new RunResult(0, "Elevated task base is ready.");
        }
        if (IsCurrentProcessElevated())
        {
            return InstallComplete(false);
        }
        return RunElevatedInstallComplete();
    }

    private static RunResult RunElevatedSelfCommand(string arguments, string actionName, int timeoutMs)
    {
        if (String.IsNullOrWhiteSpace(arguments)) { return new RunResult(1, "Missing elevated command."); }
        try
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = GetLaunchExecutablePath();
            start.Arguments = arguments;
            start.UseShellExecute = true;
            start.Verb = "runas";
            start.WindowStyle = ProcessWindowStyle.Hidden;

            using (Process process = Process.Start(start))
            {
                if (process == null)
                {
                    return new RunResult(1, "Nao consegui iniciar a acao elevada do Smart Nap.");
                }
                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { }
                    AppendOperationalLog("action=" + SanitizeLogToken(actionName) + " status=timeout elevated=true");
                    return new RunResult(124, "A acao elevada demorou demais e foi interrompida.");
                }
                AppendOperationalLog("action=" + SanitizeLogToken(actionName) + " status=done elevated=true exitCode=" + process.ExitCode.ToString(CultureInfo.InvariantCulture));
                return new RunResult(process.ExitCode, process.ExitCode == 0 ? "Elevated action completed." : "Elevated action exited with code " + process.ExitCode.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }
        catch (Win32Exception ex)
        {
            if (ex.NativeErrorCode == 1223)
            {
                AppendOperationalLog("action=" + SanitizeLogToken(actionName) + " status=cancelled elevated=true");
                return new RunResult(1223, "Permissao de administrador cancelada.");
            }
            AppendOperationalLog("action=" + SanitizeLogToken(actionName) + " status=failed elevated=true detail=" + ShortTaskError(ex.Message));
            return new RunResult(1, "Nao consegui solicitar permissao de administrador para concluir esta acao.");
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=" + SanitizeLogToken(actionName) + " status=failed elevated=true detail=" + ShortTaskError(ex.Message));
            return new RunResult(1, "Nao consegui concluir a acao elevada.");
        }
    }
    private static RunResult RunElevatedInstallComplete()
    {
        if (IsCurrentProcessElevated())
        {
            return InstallComplete(false);
        }

        try
        {
            MarkAdminSetupPromptedForCurrentVersion("elevated-request");
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = GetLaunchExecutablePath();
            start.Arguments = "--repair-install";
            start.UseShellExecute = true;
            start.Verb = "runas";
            start.WindowStyle = ProcessWindowStyle.Hidden;

            using (Process process = Process.Start(start))
            {
                if (process == null)
                {
                    return new RunResult(1, "Nao consegui iniciar o reparo elevado do Smart Nap.");
                }
                if (!process.WaitForExit(180000))
                {
                    try { process.Kill(); } catch { }
                    AppendOperationalLog("action=elevated-setup status=timeout");
                    return new RunResult(124, "O reparo elevado demorou demais e foi interrompido.");
                }
                AppendOperationalLog("action=elevated-setup status=done exitCode=" + process.ExitCode.ToString(CultureInfo.InvariantCulture));
                RunResult result = new RunResult(process.ExitCode, process.ExitCode == 0 ? "Setup elevated completed." : "Setup elevated exited with code " + process.ExitCode.ToString(CultureInfo.InvariantCulture) + ".");
                MarkAdminSetupCompletedIfReady("elevated-request", result);
                return result;
            }
        }
        catch (Win32Exception ex)
        {
            if (ex.NativeErrorCode == 1223)
            {
                AppendOperationalLog("action=elevated-setup status=cancelled");
                return new RunResult(1223, "Permissao de administrador cancelada.");
            }
            AppendOperationalLog("action=elevated-setup status=failed detail=" + ShortTaskError(ex.Message));
            return new RunResult(1, "Nao consegui solicitar permissao de administrador para concluir a instalacao.");
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=elevated-setup status=failed detail=" + ShortTaskError(ex.Message));
            return new RunResult(1, "Nao consegui concluir o reparo de instalacao.");
        }
    }
    private static RunResult RunRestore()
    {
        return RunPowerShellScript(backgroundScriptPath, "-ConfigPath " + Quote(GetEffectiveConfigPath()) + " -Action Restore -Quiet -LogPath " + Quote(logPath), 180000);
    }

    private static RunResult RunForegroundRestore(int pid)
    {
        if (pid <= 0)
        {
            return new RunResult(0, "No foreground pid.");
        }
        return RunPowerShellScript(backgroundScriptPath, "-ConfigPath " + Quote(GetEffectiveConfigPath()) + " -Action ForegroundRestore -TargetPid " + pid.ToString() + " -StateMode Latest -Quiet -LogPath " + Quote(logPath), 30000);
    }

    private static string BuildInteractiveTaskXml(string taskDescription, string arguments, bool hidden)
    {
        string sid = WindowsIdentity.GetCurrent().User.Value;
        string author = Environment.UserDomainName + "\\" + Environment.UserName;
        string command = GetLaunchExecutablePath();
        string workDir = Path.GetDirectoryName(command);
        if (String.IsNullOrWhiteSpace(workDir)) { workDir = appRoot; }
        string xmlArguments = arguments ?? "";
        string hiddenText = hidden ? "true" : "false";
        return @"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>" + XmlText(author) + @"</Author>
    <Description>" + XmlText(taskDescription) + @"</Description>
  </RegistrationInfo>
  <Principals>
    <Principal id=""Author"">
      <UserId>" + XmlText(sid) + @"</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>" + hiddenText + @"</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession>
    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>" + XmlText(command) + @"</Command>
      <Arguments>" + XmlText(xmlArguments) + @"</Arguments>
      <WorkingDirectory>" + XmlText(workDir) + @"</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";
    }

    private static string BuildSessionAgentTaskXml()
    {
        string sid = WindowsIdentity.GetCurrent().User.Value;
        string author = Environment.UserDomainName + "\\" + Environment.UserName;
        string command = GetLaunchExecutablePath();
        string workDir = Path.GetDirectoryName(command);
        if (String.IsNullOrWhiteSpace(workDir)) { workDir = appRoot; }
        return @"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>" + XmlText(author) + @"</Author>
    <Description>Smart Nap interactive Session Agent. Observes foreground, fullscreen, idle, game, and streaming context for the Core Service.</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>" + XmlText(sid) + @"</UserId>
      <Delay>PT15S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>" + XmlText(sid) + @"</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>true</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession>
    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>" + XmlText(command) + @"</Command>
      <Arguments>--session-agent</Arguments>
      <WorkingDirectory>" + XmlText(workDir) + @"</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";
    }

    private static string XmlText(string value)
    {
        return System.Security.SecurityElement.Escape(value ?? "") ?? "";
    }

    private static RunResult RegisterXmlScheduledTask(string taskName, string xml)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), taskName + "-" + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + ".xml");
        try
        {
            File.WriteAllText(tempPath, xml, Encoding.Unicode);
            return RunHidden("schtasks.exe", "/Create /TN " + Quote(taskName) + " /XML " + Quote(tempPath) + " /F", 30000);
        }
        finally
        {
            try { if (File.Exists(tempPath)) { File.Delete(tempPath); } } catch { }
        }
    }

    private static RunResult InstallDashboardTask()
    {
        if (!IsCurrentProcessElevated())
        {
            return IsTaskInstalled(DashboardTaskName)
                ? new RunResult(0, "Elevated launcher task already installed.")
                : new RunResult(5, "Administrator permission is required to prepare the elevated launcher task.");
        }

        string xml = BuildInteractiveTaskXml("Smart Nap elevated launcher. Opens the dashboard with the same elevated control base used by the engine.", "", false);
        RunResult result = RegisterXmlScheduledTask(DashboardTaskName, xml);
        if (result.ExitCode == 0)
        {
            AppendOperationalLog("action=install-dashboard-task status=OK");
            return new RunResult(0, "Elevated launcher task installed.");
        }
        AppendOperationalLog("action=install-dashboard-task status=FAIL detail=" + ShortTaskError(result.Output));
        return result;
    }

    private static RunResult UninstallDashboardTask()
    {
        if (!IsTaskInstalled(DashboardTaskName))
        {
            return new RunResult(0, "Elevated launcher task was already off.");
        }

        RunResult result = RunHidden("schtasks.exe", "/Delete /TN " + Quote(DashboardTaskName) + " /F", 10000);
        if (result.ExitCode != 0 && !IsCurrentProcessElevated() && LooksLikeAccessDenied(result.Output))
        {
            result = RunElevatedSelfCommand("--uninstall-dashboard", "uninstall-dashboard", 120000);
        }
        if (IsTaskInstalled(DashboardTaskName))
        {
            return new RunResult(result.ExitCode == 0 ? 1 : result.ExitCode, "Nao consegui remover a tarefa elevada do launcher. Aceite a permissao de administrador para concluir.");
        }
        return result.ExitCode == 0 ? new RunResult(0, "Elevated launcher task removed.") : result;
    }

    private static RunResult InstallSessionAgent()
    {
        return InstallSessionAgent(true);
    }

    private static RunResult InstallSessionAgent(bool allowElevatedRepair)
    {
        string xml = BuildSessionAgentTaskXml();
        RunResult result = RegisterXmlScheduledTask(SessionAgentTaskName, xml);
        if (result.ExitCode != 0 && allowElevatedRepair && !IsCurrentProcessElevated() && LooksLikeAccessDenied(result.Output))
        {
            result = RunElevatedSelfCommand("--install-session-agent", "install-session-agent", 120000);
        }

        if (!IsTaskInstalled(SessionAgentTaskName))
        {
            SaveUiFlag("SessionAgentEnabled", false);
            AppendOperationalLog("action=session-agent-install status=FAIL detail=" + ShortTaskError(result.Output));
            return result.ExitCode == 0
                ? new RunResult(1, "Session Agent task was not registered.")
                : result;
        }

        SaveUiFlag("SessionAgentEnabled", true);
        RunResult start = RunHidden("schtasks.exe", "/Run /TN " + Quote(SessionAgentTaskName), 10000);
        AppendOperationalLog("action=session-agent-install status=OK startExitCode=" + start.ExitCode.ToString(CultureInfo.InvariantCulture) + (start.ExitCode == 0 ? "" : " detail=" + ShortTaskError(start.Output)));
        return new RunResult(0, "Session Agent installed for user logon.");
    }

    private static RunResult UninstallSessionAgent()
    {
        return UninstallSessionAgent(true);
    }

    private static RunResult UninstallSessionAgent(bool allowElevatedRepair)
    {
        if (!IsTaskInstalled(SessionAgentTaskName))
        {
            SaveUiFlag("SessionAgentEnabled", false);
            return new RunResult(0, "Session Agent task was already off.");
        }

        RunHidden("schtasks.exe", "/End /TN " + Quote(SessionAgentTaskName), 10000);
        RunResult result = RunHidden("schtasks.exe", "/Delete /TN " + Quote(SessionAgentTaskName) + " /F", 10000);
        if (result.ExitCode != 0 && allowElevatedRepair && !IsCurrentProcessElevated() && LooksLikeAccessDenied(result.Output))
        {
            result = RunElevatedSelfCommand("--uninstall-session-agent", "uninstall-session-agent", 120000);
        }

        SaveUiFlag("SessionAgentEnabled", IsTaskInstalled(SessionAgentTaskName));
        if (IsTaskInstalled(SessionAgentTaskName))
        {
            AppendOperationalLog("action=session-agent-uninstall status=FAIL detail=" + ShortTaskError(result.Output));
            return new RunResult(result.ExitCode == 0 ? 1 : result.ExitCode, "Nao consegui remover a tarefa do Session Agent.");
        }

        AppendOperationalLog("action=session-agent-uninstall status=OK");
        return result.ExitCode == 0 ? new RunResult(0, "Session Agent removed.") : result;
    }

    private static RunResult WriteSessionAgentStatusToConsole()
    {
        SessionAgentSnapshot snapshot = LoadSessionAgentSnapshot();
        Dictionary<string, object> status = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        status["Version"] = AppVersion;
        status["TaskName"] = SessionAgentTaskName;
        status["TaskInstalled"] = IsTaskInstalled(SessionAgentTaskName);
        status["TaskEnabled"] = ReadUiFlag("SessionAgentEnabled");
        status["Snapshot"] = BuildSessionAgentPayload(snapshot);
        status["Context"] = BuildSessionContextPayload(snapshot);
        status["Foreground"] = BuildSessionForegroundPayload(snapshot);
        status["StatePath"] = sessionAgentStatePath;
        Console.WriteLine(JsonCompat.SerializeObject(status));
        return new RunResult(0, JsonCompat.SerializeObject(status));
    }

    private static void RunCoreServiceHost(string[] args)
    {
        try
        {
            SmartSnapCoreService service = new SmartSnapCoreService();
            if (Environment.UserInteractive)
            {
                service.RunConsole(args);
                return;
            }

            ServiceBase.Run(new ServiceBase[] { service });
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=core-service-host status=failed detail=" + ShortTaskError(ex.Message));
            WriteCrash(ex);
            Environment.ExitCode = 1;
        }
    }

    private static bool IsCoreServiceInstalled()
    {
        ServiceControllerStatus status;
        if (TryGetCoreServiceStatus(out status))
        {
            return true;
        }

        RunResult result = RunHidden("sc.exe", "query " + CoreServiceName, 8000);
        return result.ExitCode == 0;
    }

    private static bool IsCoreServiceRunning()
    {
        ServiceControllerStatus status;
        if (TryGetCoreServiceStatus(out status))
        {
            return status == ServiceControllerStatus.Running;
        }

        RunResult result = RunHidden("sc.exe", "query " + CoreServiceName, 8000);
        return result.ExitCode == 0 && result.Output.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetCoreServiceStatusText()
    {
        ServiceControllerStatus status;
        if (TryGetCoreServiceStatus(out status))
        {
            return status.ToString().ToLowerInvariant();
        }

        RunResult result = RunHidden("sc.exe", "query " + CoreServiceName, 8000);
        if (result.ExitCode != 0)
        {
            return "not installed";
        }

        Match match = Regex.Match(result.Output ?? "", @"STATE\s*:\s*\d+\s+([A-Z_]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.ToLowerInvariant().Replace('_', ' ');
        }

        return "installed";
    }

    private static bool TryGetCoreServiceStatus(out ServiceControllerStatus status)
    {
        status = ServiceControllerStatus.Stopped;
        try
        {
            ServiceController[] services = ServiceController.GetServices();
            foreach (ServiceController service in services)
            {
                try
                {
                    if (String.Equals(service.ServiceName, CoreServiceName, StringComparison.OrdinalIgnoreCase))
                    {
                        status = service.Status;
                        return true;
                    }
                }
                finally
                {
                    service.Dispose();
                }
            }
        }
        catch
        {
        }
        return false;
    }

    private static RunResult WriteCoreServiceStatusToConsole()
    {
        RunResult query = RunHidden("sc.exe", "query " + CoreServiceName, 8000);
        ServiceControllerStatus serviceStatus;
        bool installed = TryGetCoreServiceStatus(out serviceStatus) || query.ExitCode == 0;
        bool running = installed && serviceStatus == ServiceControllerStatus.Running;
        CoreServiceSnapshot snapshot = LoadCoreServiceSnapshot();
        int scoreAgeSeconds = snapshot.Available ? snapshot.ScoreAgeSeconds : GetFileAgeSeconds(scorePath);
        bool telemetryStale = snapshot.Available ? snapshot.TelemetryStale : (scoreAgeSeconds < 0 || scoreAgeSeconds > CoreServiceStalePassSeconds);
        string health = snapshot.Available && !String.IsNullOrWhiteSpace(snapshot.Health)
            ? snapshot.Health
            : ClassifyCoreServiceHealth(installed ? (running ? "Running" : "Installed") : "NotInstalled", "Status", query.ExitCode, installed, running, IsTaskInstalled(AutoTaskName), telemetryStale, false);
        IDictionary<string, object> state = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        state["AppVersion"] = AppVersion;
        state["ServiceName"] = CoreServiceName;
        state["DisplayName"] = CoreServiceDisplayName;
        state["ProtocolVersion"] = snapshot.ProtocolVersion <= 0 ? CoreProtocolVersion : snapshot.ProtocolVersion;
        state["MinimumSupportedProtocolVersion"] = snapshot.MinimumSupportedProtocolVersion <= 0 ? CoreMinimumSupportedProtocolVersion : snapshot.MinimumSupportedProtocolVersion;
        state["PipeName"] = String.IsNullOrWhiteSpace(snapshot.PipeName) ? CorePipeName : snapshot.PipeName;
        state["ContextProvider"] = String.IsNullOrWhiteSpace(snapshot.ContextProvider) ? "SessionAgentV1+" + CoreContextProviderLegacyBridge : snapshot.ContextProvider;
        state["Capabilities"] = BuildCoreServiceCapabilities();
        IDictionary<string, object> ipcStatus = BuildCorePipeStatePayload();
        if (snapshot.Available)
        {
            ipcStatus["Listening"] = snapshot.IpcListening;
            ipcStatus["SecureAcl"] = snapshot.IpcSecureAcl;
            ipcStatus["HeartbeatAt"] = snapshot.IpcHeartbeatAt ?? "";
            ipcStatus["LastClientAt"] = snapshot.IpcLastClientAt ?? "";
            ipcStatus["LastCommand"] = snapshot.IpcLastCommand ?? "";
            ipcStatus["LastError"] = snapshot.IpcLastError ?? "";
        }
        state["Ipc"] = ipcStatus;
        state["IpcListening"] = ReadMapBool(ipcStatus, "Listening");
        state["IpcSecureAcl"] = ReadMapBool(ipcStatus, "SecureAcl");
        state["IpcHeartbeatAt"] = ReadMapString(ipcStatus, "HeartbeatAt");
        state["IpcLastClientAt"] = ReadMapString(ipcStatus, "LastClientAt");
        state["IpcLastCommand"] = ReadMapString(ipcStatus, "LastCommand");
        state["IpcLastError"] = ReadMapString(ipcStatus, "LastError");
        state["Installed"] = installed || snapshot.Installed;
        state["Running"] = running || snapshot.Running;
        state["Status"] = snapshot.Available && !String.IsNullOrWhiteSpace(snapshot.Status) ? snapshot.Status : (installed ? GetCoreServiceStatusText() : "not installed");
        state["Action"] = snapshot.Action ?? "Status";
        state["Health"] = health;
        state["Summary"] = snapshot.Available && !String.IsNullOrWhiteSpace(snapshot.Summary) ? snapshot.Summary : BuildCoreServiceSummary(health, "Status", IsTaskInstalled(AutoTaskName), telemetryStale, false, scoreAgeSeconds);
        state["Detail"] = snapshot.Detail ?? "";
        state["NeedsAttention"] = snapshot.Available ? snapshot.NeedsAttention : IsCoreServiceAttentionHealth(health);
        state["AutoTaskInstalled"] = snapshot.Available ? snapshot.AutoTaskInstalled : IsTaskInstalled(AutoTaskName);
        state["SessionAgentTaskInstalled"] = IsTaskInstalled(SessionAgentTaskName);
        state["TelemetryFresh"] = snapshot.Available ? snapshot.TelemetryFresh : !telemetryStale;
        state["TelemetryStale"] = telemetryStale;
        state["ScoreAgeSeconds"] = scoreAgeSeconds;
        state["StateAgeSeconds"] = snapshot.StateAgeSeconds;
        state["UpdatedAt"] = snapshot.UpdatedAt ?? "";
        state["MemoryStability"] = BuildMemoryStabilityPayload(snapshot);
        state["SystemIntegrity"] = BuildSystemIntegrityPayload(snapshot);
        SessionAgentSnapshot sessionAgent = LoadSessionAgentSnapshot();
        state["SessionAgent"] = BuildSessionAgentPayload(sessionAgent);
        state["SessionContext"] = BuildSessionContextPayload(sessionAgent);
        state["SessionForeground"] = BuildSessionForegroundPayload(sessionAgent);
        state["StatePath"] = coreServiceStatePath;
        state["SessionAgentStatePath"] = sessionAgentStatePath;
        state["CheckedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        state["ServiceQueryExitCode"] = query.ExitCode;
        if (query.ExitCode != 0 && !String.IsNullOrWhiteSpace(query.Output))
        {
            state["ServiceQueryDetail"] = ShortTaskError(query.Output);
        }
        Console.WriteLine(JsonCompat.SerializeObject(state));
        return new RunResult(0, JsonCompat.SerializeObject(state));
    }

    private static RunResult InstallCoreService()
    {
        return InstallCoreService(true);
    }

    private static RunResult InstallCoreService(bool allowElevatedRepair)
    {
        if (!IsCurrentProcessElevated())
        {
            if (IsCoreServiceInstalled() && IsCoreServiceRunning())
            {
                SaveUiFlag("CoreServiceEnabled", true);
                return new RunResult(0, "Core service already installed and running.");
            }
            if (allowElevatedRepair)
            {
                return RunElevatedSelfCommand("--install-core-service", "install-core-service", 120000);
            }
            return new RunResult(5, "Administrator permission is required to install the Smart SNAP Core Service.");
        }

        try
        {
            string exePath = GetLaunchExecutablePath();
            string serviceCommand = Quote(exePath) + " --core-service";
            string serviceCommandArgument = Quote(serviceCommand);
            RunResult result = IsCoreServiceInstalled()
                ? RunHidden("sc.exe", "config " + CoreServiceName + " binPath= " + serviceCommandArgument + " DisplayName= " + Quote(CoreServiceDisplayName), 30000)
                : RunHidden("sc.exe", "create " + CoreServiceName + " binPath= " + serviceCommandArgument + " DisplayName= " + Quote(CoreServiceDisplayName) + " start= auto", 30000);

            if (result.ExitCode != 0)
            {
                AppendOperationalLog("action=core-service-install status=FAIL detail=" + ShortTaskError(result.Output));
                WriteCoreServiceState("InstallFailed", "Install", result, IsTaskInstalled(AutoTaskName), false, GetFileAgeSeconds(scorePath), CoreServiceStalePassSeconds);
                return result;
            }

            RunHidden("sc.exe", "description " + CoreServiceName + " " + Quote("Smart SNAP watchdog and privileged broker for Smart Background Nap engine tasks."), 15000);
            RunHidden("sc.exe", "config " + CoreServiceName + " start= delayed-auto", 15000);
            RunHidden("sc.exe", "failure " + CoreServiceName + " reset= 86400 actions= restart/60000/restart/120000/none/0", 15000);

            RunResult start = StartCoreService(false);
            SaveUiFlag("CoreServiceEnabled", true);
            bool running = IsCoreServiceRunning();
            AppendOperationalLog("action=core-service-install status=OK running=" + running.ToString().ToLowerInvariant());
            return start.ExitCode == 0 || running
                ? new RunResult(0, "Core service installed.")
                : new RunResult(0, "Core service installed; start is pending. " + ShortTaskError(start.Output));
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=core-service-install status=failed detail=" + ShortTaskError(ex.Message));
            WriteCrash(ex);
            return new RunResult(1, "Could not install the Smart SNAP Core Service: " + ex.Message);
        }
    }

    private static RunResult UninstallCoreService()
    {
        return UninstallCoreService(true);
    }

    private static RunResult UninstallCoreService(bool allowElevatedRepair)
    {
        if (!IsCoreServiceInstalled())
        {
            SaveUiFlag("CoreServiceEnabled", false);
            return new RunResult(0, "Core service was already off.");
        }

        if (!IsCurrentProcessElevated())
        {
            if (allowElevatedRepair)
            {
                return RunElevatedSelfCommand("--uninstall-core-service", "uninstall-core-service", 120000);
            }
            return new RunResult(5, "Administrator permission is required to remove the Smart SNAP Core Service.");
        }

        try
        {
            RunResult stop = StopCoreService(false);
            RunResult delete = RunHidden("sc.exe", "delete " + CoreServiceName, 30000);
            SaveUiFlag("CoreServiceEnabled", IsCoreServiceInstalled());
            WriteCoreServiceState(IsCoreServiceInstalled() ? "DeletePending" : "Removed", "Uninstall", delete, IsTaskInstalled(AutoTaskName), false, GetFileAgeSeconds(scorePath), CoreServiceStalePassSeconds);
            AppendOperationalLog("action=core-service-uninstall status=" + (delete.ExitCode == 0 ? "OK" : "FAIL") + " detail=" + ShortTaskError(delete.Output));
            if (delete.ExitCode != 0) { return RunResult.Combine(stop, delete); }
            return new RunResult(0, "Core service removed.");
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=core-service-uninstall status=failed detail=" + ShortTaskError(ex.Message));
            WriteCrash(ex);
            return new RunResult(1, "Could not remove the Smart SNAP Core Service: " + ex.Message);
        }
    }

    private static RunResult StartCoreService()
    {
        return StartCoreService(true);
    }

    private static RunResult StartCoreService(bool allowElevatedRepair)
    {
        if (IsCoreServiceRunning())
        {
            return new RunResult(0, "Core service already running.");
        }

        if (!IsCoreServiceInstalled())
        {
            return InstallCoreService(allowElevatedRepair);
        }

        if (!IsCurrentProcessElevated())
        {
            if (allowElevatedRepair)
            {
                return RunElevatedSelfCommand("--start-core-service", "start-core-service", 120000);
            }
            return new RunResult(5, "Administrator permission is required to start the Smart SNAP Core Service.");
        }

        RunResult result = RunHidden("sc.exe", "start " + CoreServiceName, 30000);
        bool running = IsCoreServiceRunning();
        if (result.ExitCode != 0 && !running)
        {
            WriteCoreServiceState("StartPending", "Start", result, IsTaskInstalled(AutoTaskName), false, GetFileAgeSeconds(scorePath), CoreServiceStalePassSeconds);
            return result;
        }
        return new RunResult(0, "Core service started.");
    }

    private static RunResult StopCoreService()
    {
        return StopCoreService(true);
    }

    private static RunResult StopCoreService(bool allowElevatedRepair)
    {
        if (!IsCoreServiceInstalled())
        {
            return new RunResult(0, "Core service was not installed.");
        }

        if (!IsCoreServiceRunning())
        {
            return new RunResult(0, "Core service already stopped.");
        }

        if (!IsCurrentProcessElevated())
        {
            if (allowElevatedRepair)
            {
                return RunElevatedSelfCommand("--stop-core-service", "stop-core-service", 120000);
            }
            return new RunResult(5, "Administrator permission is required to stop the Smart SNAP Core Service.");
        }

        RunResult result = RunHidden("sc.exe", "stop " + CoreServiceName, 30000);
        WriteCoreServiceState(IsCoreServiceRunning() ? "StopPending" : "Stopped", "Stop", result, IsTaskInstalled(AutoTaskName), false, GetFileAgeSeconds(scorePath), CoreServiceStalePassSeconds);
        return result.ExitCode == 0 || !IsCoreServiceRunning() ? new RunResult(0, "Core service stopped.") : result;
    }

    private static int GetFileAgeSeconds(string path)
    {
        try
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) { return -1; }
            double age = (DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalSeconds;
            if (Double.IsNaN(age) || Double.IsInfinity(age)) { return -1; }
            return Math.Max(0, (int)Math.Round(age));
        }
        catch
        {
            return -1;
        }
    }

    private static bool WaitForFileWriteAfter(string path, DateTime cutoffUtc, int timeoutMs)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1000, timeoutMs));
        do
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(path) && File.Exists(path) && File.GetLastWriteTimeUtc(path) >= cutoffUtc)
                {
                    return true;
                }
            }
            catch
            {
            }
            Thread.Sleep(350);
        }
        while (DateTime.UtcNow < deadline);

        try
        {
            return !String.IsNullOrWhiteSpace(path) && File.Exists(path) && File.GetLastWriteTimeUtc(path) >= cutoffUtc;
        }
        catch
        {
            return false;
        }
    }

    private sealed class CoreServiceSnapshot
    {
        public bool Available;
        public bool Installed;
        public bool Running;
        public int ProtocolVersion;
        public int MinimumSupportedProtocolVersion;
        public string PipeName;
        public string ContextProvider;
        public string Status;
        public string Action;
        public string Health;
        public string Summary;
        public string Detail;
        public bool AutoTaskInstalled;
        public bool AutoTaskKicked;
        public bool TelemetryFresh;
        public bool TelemetryStale;
        public bool NeedsAttention;
        public int ScoreAgeSeconds;
        public int StaleThresholdSeconds;
        public int LoopSeconds;
        public int ExitCode;
        public int StateAgeSeconds;
        public string UpdatedAt;
        public bool IpcListening;
        public bool IpcSecureAcl;
        public string IpcHeartbeatAt;
        public string IpcLastClientAt;
        public string IpcLastCommand;
        public string IpcLastError;
        public bool MemoryStabilityAvailable;
        public bool MemoryStabilityRelevant;
        public string MemoryStabilityMode;
        public string MemoryStabilityState;
        public string MemoryStabilitySummary;
        public string MemoryStabilityDetail;
        public int MemoryStabilityMemoryLoad;
        public double MemoryStabilityAvailablePhysicalMB;
        public double MemoryStabilityTotalPhysicalMB;
        public double MemoryStabilityCommitUsedMB;
        public double MemoryStabilityCommitLimitMB;
        public double MemoryStabilityCommitHeadroomMB;
        public int MemoryStabilityCommitHeadroomPercent;
        public string MemoryStabilityPagefileStatus;
        public bool MemoryStabilityPagefileLimited;
        public bool MemoryStabilityLowMemorySignal;
        public bool MemoryStabilityBrowserBurstRecommended;
        public string MemoryStabilityTopProcess;
        public int MemoryStabilityTopProcessPid;
        public double MemoryStabilityTopProcessPrivateMB;
        public double MemoryStabilityTopProcessWorkingSetMB;
        public int MemoryStabilityBrowserProcessCount;
        public double MemoryStabilityBrowserPrivateMB;
        public double MemoryStabilityBrowserWorkingSetMB;
        public string MemoryStabilityBrowserBurstState;
        public int MemoryStabilityHeavyRecentProcessCount;
        public List<string> MemoryStabilitySignals;
        public bool SystemIntegrityAvailable;
        public bool SystemIntegrityRelevant;
        public string SystemIntegrityMode;
        public string SystemIntegrityState;
        public string SystemIntegritySummary;
        public string SystemIntegrityDetail;
        public bool SystemIntegrityBackupAvailable;
        public bool SystemIntegrityMmcssServiceRunning;
        public string SystemIntegrityMmcssServiceStatus;
        public int SystemIntegritySystemResponsiveness;
        public string SystemIntegritySystemResponsivenessState;
        public string SystemIntegritySystemResponsivenessDetail;
        public bool SystemIntegrityHybridCpuDetected;
        public int SystemIntegrityLogicalProcessorCount;
        public string SystemIntegrityHybridSchedulerState;
        public string SystemIntegrityHybridSchedulerDetail;
        public bool SystemIntegritySelfThrottleEligible;
        public string SystemIntegritySelfThrottleState;
        public string SystemIntegritySelfThrottleDetail;
        public int SystemIntegrityIssueCount;
        public int SystemIntegrityRecommendationCount;
        public int SystemIntegritySafeRecommendationCount;
        public int SystemIntegrityOptionalRecommendationCount;
        public int SystemIntegrityExperimentalRecommendationCount;
        public int SystemIntegrityRestartRecommendationCount;
        public int SystemIntegrityApplyBlockedRecommendationCount;
        public List<Dictionary<string, object>> SystemIntegrityRecommendations;
        public List<string> SystemIntegritySignals;
        public List<string> SystemIntegrityIssues;
    }

    private sealed class SessionAgentSnapshot
    {
        public bool Available;
        public int ProtocolVersion;
        public string AgentVersion;
        public string State;
        public string Health;
        public string Source;
        public int SessionId;
        public string UserSid;
        public string UserName;
        public string UpdatedAt;
        public int StateAgeSeconds;
        public int IdleSeconds;
        public string Context;
        public int Confidence;
        public List<string> Evidence;
        public int ForegroundPid;
        public string ForegroundProcessName;
        public string ForegroundStartTime;
        public string ForegroundPath;
        public bool ForegroundHasWindow;
        public bool ForegroundIsGame;
        public bool ForegroundIsStreaming;
        public bool ForegroundIsProtected;
        public bool ForegroundFullscreen;
        public bool StreamingObserved;
        public int StreamingProcessCount;
        public string LastError;
        public string CorePublishedAt;
        public string CorePublishStatus;
    }

    private sealed class MemoryStabilityProcessSample
    {
        public string Name;
        public int Pid;
        public double WorkingSetMB;
        public double PrivateBytesMB;
        public int AgeSeconds;
        public bool Browser;
        public bool Foreground;
    }

    private sealed class MemoryStabilitySnapshot
    {
        public bool Available;
        public bool Relevant;
        public string Mode;
        public string State;
        public string Summary;
        public string Detail;
        public int MemoryLoad;
        public double AvailablePhysicalMB;
        public double TotalPhysicalMB;
        public double CommitUsedMB;
        public double CommitLimitMB;
        public double CommitHeadroomMB;
        public int CommitHeadroomPercent;
        public string PagefileStatus;
        public bool PagefileLimited;
        public bool LowMemorySignal;
        public bool BrowserBurstRecommended;
        public string TopProcess;
        public int TopProcessPid;
        public double TopProcessPrivateMB;
        public double TopProcessWorkingSetMB;
        public int BrowserProcessCount;
        public double BrowserPrivateMB;
        public double BrowserWorkingSetMB;
        public string BrowserBurstState;
        public int HeavyRecentProcessCount;
        public List<string> Signals = new List<string>();
        public List<MemoryStabilityProcessSample> TopConsumers = new List<MemoryStabilityProcessSample>();
    }

    private sealed class SystemIntegritySnapshot
    {
        public bool Available;
        public bool Relevant;
        public string Mode;
        public string State;
        public string Summary;
        public string Detail;
        public bool BackupAvailable;
        public bool MmcssServiceRunning;
        public string MmcssServiceStatus;
        public int SystemResponsiveness = -1;
        public string SystemResponsivenessState;
        public string SystemResponsivenessDetail;
        public bool HybridCpuDetected;
        public int LogicalProcessorCount;
        public string HybridSchedulerState;
        public string HybridSchedulerDetail;
        public bool SelfThrottleEligible;
        public string SelfThrottleState;
        public string SelfThrottleDetail;
        public int IssueCount;
        public int RecommendationCount;
        public int SafeRecommendationCount;
        public int OptionalRecommendationCount;
        public int ExperimentalRecommendationCount;
        public int RestartRecommendationCount;
        public int ApplyBlockedRecommendationCount;
        public List<string> Signals = new List<string>();
        public List<string> Issues = new List<string>();
        public List<SystemOptimizationRecommendation> Recommendations = new List<SystemOptimizationRecommendation>();
    }

    private sealed class SystemOptimizationRecommendation
    {
        public string Id;
        public string Tier;
        public string Title;
        public string Summary;
        public string Category;
        public string Reason;
        public string Compatibility;
        public string Risk;
        public string Impact;
        public string Restart;
        public string Backup;
        public string CurrentValue;
        public string RecommendedValue;
        public string SafetyGate;
        public string ActionKind = "";
        public string Source;
        public string Documentation;
        public bool SelectedByDefault;
        public bool CanApply;
        public bool RequiresAdmin;
        public bool RequiresRestart;
        public bool RequiresSignOut = false;
        public bool RequiresGameClosed = false;
        public bool BackupRequired;
        public bool Reversible;
        public bool Experimental;
        public List<string> Details = new List<string>();
    }

    private static string ReadMapString(IDictionary<string, object> map, string key)
    {
        object value;
        if (map == null || !map.TryGetValue(key, out value) || value == null) { return ""; }
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static int ReadMapInt(IDictionary<string, object> map, string key)
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

    private static double ReadMapDouble(IDictionary<string, object> map, string key)
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

    private static bool ReadMapBool(IDictionary<string, object> map, string key)
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

    private static IDictionary<string, object> ReadMapObject(IDictionary<string, object> map, string key)
    {
        object value;
        if (map == null || !map.TryGetValue(key, out value) || value == null) { return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); }
        IDictionary<string, object> typed = value as IDictionary<string, object>;
        return typed ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> ReadMapStringList(IDictionary<string, object> map, string key)
    {
        List<string> result = new List<string>();
        object value;
        if (map == null || !map.TryGetValue(key, out value) || value == null) { return result; }
        string text = value as string;
        if (text != null)
        {
            if (!String.IsNullOrWhiteSpace(text)) { result.Add(text); }
            return result;
        }
        System.Collections.IEnumerable items = value as System.Collections.IEnumerable;
        if (items == null) { return result; }
        foreach (object item in items)
        {
            string itemText = Convert.ToString(item, CultureInfo.InvariantCulture);
            if (!String.IsNullOrWhiteSpace(itemText)) { result.Add(itemText); }
        }
        return result;
    }

    private static List<Dictionary<string, object>> ReadMapDictionaryList(IDictionary<string, object> map, string key)
    {
        List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
        object value;
        if (map == null || !map.TryGetValue(key, out value) || value == null) { return result; }
        System.Collections.IEnumerable items = value as System.Collections.IEnumerable;
        if (items == null || value is string) { return result; }
        foreach (object item in items)
        {
            IDictionary<string, object> typed = item as IDictionary<string, object>;
            if (typed == null) { continue; }
            Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> pair in typed)
            {
                copy[pair.Key] = pair.Value;
            }
            result.Add(copy);
        }
        return result;
    }

    private static int GetIsoAgeSeconds(string timestamp)
    {
        try
        {
            if (String.IsNullOrWhiteSpace(timestamp)) { return -1; }
            DateTime parsed;
            if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed)) { return -1; }
            double age = (DateTime.UtcNow - parsed.ToUniversalTime()).TotalSeconds;
            if (Double.IsNaN(age) || Double.IsInfinity(age)) { return -1; }
            return Math.Max(0, (int)Math.Round(age));
        }
        catch
        {
            return -1;
        }
    }

    private static string ClassifyCoreServiceHealth(string status, string action, int exitCode, bool installed, bool running, bool autoTaskInstalled, bool telemetryStale, bool kicked)
    {
        string normalizedStatus = (status ?? "").Trim();
        string normalizedAction = (action ?? "").Trim();
        if (normalizedStatus.Equals("Starting", StringComparison.OrdinalIgnoreCase) || normalizedStatus.Equals("StartPending", StringComparison.OrdinalIgnoreCase)) { return "Starting"; }
        if (normalizedStatus.Equals("InstallFailed", StringComparison.OrdinalIgnoreCase) || normalizedStatus.Equals("Error", StringComparison.OrdinalIgnoreCase)) { return "Attention"; }
        if (!installed || normalizedStatus.Equals("NotInstalled", StringComparison.OrdinalIgnoreCase) || normalizedStatus.Equals("Removed", StringComparison.OrdinalIgnoreCase)) { return "NotInstalled"; }
        if (!running || normalizedStatus.Equals("Stopped", StringComparison.OrdinalIgnoreCase) || normalizedStatus.Equals("StopPending", StringComparison.OrdinalIgnoreCase)) { return "Stopped"; }
        if (exitCode != 0) { return "Attention"; }
        if (!autoTaskInstalled || normalizedAction.Equals("NoAutoTask", StringComparison.OrdinalIgnoreCase)) { return "Attention"; }
        if (normalizedAction.Equals("KickAutoTask", StringComparison.OrdinalIgnoreCase) && kicked) { return "Recovering"; }
        if (telemetryStale) { return "Stale"; }
        return "Healthy";
    }

    private static bool IsCoreServiceAttentionHealth(string health)
    {
        return String.Equals(health, "Attention", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(health, "NotInstalled", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(health, "Stopped", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(health, "Stale", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCoreServiceSummary(string health, string action, bool autoTaskInstalled, bool telemetryStale, bool kicked, int scoreAgeSeconds)
    {
        if (String.Equals(health, "Healthy", StringComparison.OrdinalIgnoreCase)) { return "Core service is watching fresh engine telemetry."; }
        if (String.Equals(health, "Recovering", StringComparison.OrdinalIgnoreCase)) { return "Telemetry was stale; service kicked the user-session engine task."; }
        if (String.Equals(health, "Starting", StringComparison.OrdinalIgnoreCase)) { return "Core service is starting."; }
        if (String.Equals(health, "Stopped", StringComparison.OrdinalIgnoreCase)) { return "Core service is installed but stopped."; }
        if (String.Equals(health, "NotInstalled", StringComparison.OrdinalIgnoreCase)) { return "Core service is not installed."; }
        if (!autoTaskInstalled) { return "Automatic engine task is not installed."; }
        if (telemetryStale) { return scoreAgeSeconds < 0 ? "Engine telemetry has not been written yet." : "Engine telemetry is stale."; }
        if (kicked || String.Equals(action, "KickAutoTask", StringComparison.OrdinalIgnoreCase)) { return "Service requested a fresh engine pass."; }
        return "Core service needs attention.";
    }

    private static double MemoryStabilityBytesToMb(ulong bytes)
    {
        return bytes == 0 ? 0 : Math.Round(bytes / 1048576.0, 1);
    }

    private static double MemoryStabilityBytesToMb(long bytes)
    {
        return bytes <= 0 ? 0 : Math.Round(bytes / 1048576.0, 1);
    }

    private static int MemoryStabilityPercent(double part, double total)
    {
        if (total <= 0 || Double.IsNaN(total) || Double.IsInfinity(total)) { return 0; }
        double value = (part / total) * 100.0;
        if (Double.IsNaN(value) || Double.IsInfinity(value)) { return 0; }
        return Math.Max(0, Math.Min(100, (int)Math.Round(value)));
    }

    private static bool TryQueryLowMemoryNotification(out bool lowMemory)
    {
        lowMemory = false;
        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = CreateMemoryResourceNotification(LowMemoryResourceNotification);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1)) { return false; }
            bool resourceState;
            if (!QueryMemoryResourceNotification(handle, out resourceState)) { return false; }
            lowMemory = resourceState;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (handle != IntPtr.Zero && handle != new IntPtr(-1))
            {
                try { CloseHandle(handle); } catch { }
            }
        }
    }

    private static string CleanMemoryStabilityProcessName(string value)
    {
        string name = (value ?? "").Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { name = name.Substring(0, name.Length - 4); }
        return name;
    }

    private static bool IsMemoryStabilitySmartNapProcessName(string processName)
    {
        string name = CleanMemoryStabilityProcessName(processName);
        return StringEqualsAny(name, new string[] { "SmartBackgroundNap", "SmartBackgroundNapTray", "SmartBackgroundNapDashboard", "SmartSNAPCoreService" }) ||
            name.IndexOf("SmartBackgroundNap", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Smart SNAP", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsMemoryStabilityBrowserProcess(string processName)
    {
        return StringEqualsAny(processName, new string[] { "chrome", "msedge", "firefox", "zen", "brave", "opera", "vivaldi", "librewolf", "waterfox", "floorp", "arc", "tor", "msedgewebview2" });
    }

    private static bool IsMemoryStabilityGamingContext(SessionAgentSnapshot sessionAgent)
    {
        if (sessionAgent == null) { return false; }
        if (sessionAgent.ForegroundIsGame) { return true; }
        string context = (sessionAgent.Context ?? "").Trim();
        return context.Equals("Gaming", StringComparison.OrdinalIgnoreCase) ||
            context.Equals("Competitive", StringComparison.OrdinalIgnoreCase) ||
            context.Equals("Game", StringComparison.OrdinalIgnoreCase) ||
            context.Equals("Jogos", StringComparison.OrdinalIgnoreCase) ||
            context.Equals("Jogo", StringComparison.OrdinalIgnoreCase);
    }

    private static List<MemoryStabilityProcessSample> SampleMemoryStabilityProcesses(SessionAgentSnapshot sessionAgent)
    {
        List<MemoryStabilityProcessSample> samples = new List<MemoryStabilityProcessSample>();
        int currentPid = 0;
        try { currentPid = Process.GetCurrentProcess().Id; } catch { }

        Process[] processes;
        try { processes = Process.GetProcesses(); }
        catch { return samples; }

        foreach (Process process in processes)
        {
            try
            {
                int pid = 0;
                try { pid = process.Id; } catch { }
                if (pid <= 0 || pid == currentPid) { continue; }
                string name = CleanMemoryStabilityProcessName(process.ProcessName);
                if (String.IsNullOrWhiteSpace(name) || IsMemoryStabilitySmartNapProcessName(name)) { continue; }

                long workingSetBytes = 0;
                long privateBytes = 0;
                try { workingSetBytes = process.WorkingSet64; } catch { }
                try { privateBytes = process.PrivateMemorySize64; } catch { }
                double workingSetMB = MemoryStabilityBytesToMb(workingSetBytes);
                double privateMB = MemoryStabilityBytesToMb(privateBytes);
                bool browser = IsMemoryStabilityBrowserProcess(name);
                bool foreground = sessionAgent != null && pid == sessionAgent.ForegroundPid;
                double observedMB = Math.Max(workingSetMB, privateMB);
                if (observedMB < 96 && !browser && !foreground) { continue; }

                int ageSeconds = -1;
                try
                {
                    DateTime started = process.StartTime;
                    double age = (DateTime.Now - started).TotalSeconds;
                    if (!Double.IsNaN(age) && !Double.IsInfinity(age)) { ageSeconds = Math.Max(0, (int)Math.Round(age)); }
                }
                catch
                {
                }

                samples.Add(new MemoryStabilityProcessSample
                {
                    Name = name,
                    Pid = pid,
                    WorkingSetMB = workingSetMB,
                    PrivateBytesMB = privateMB,
                    AgeSeconds = ageSeconds,
                    Browser = browser,
                    Foreground = foreground
                });
            }
            catch
            {
            }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }

        return samples
            .OrderByDescending(item => Math.Max(item.PrivateBytesMB, item.WorkingSetMB))
            .Take(8)
            .ToList();
    }

    private static MemoryStabilitySnapshot BuildMemoryStabilitySnapshot(SessionAgentSnapshot sessionAgent, string source)
    {
        MemoryStabilitySnapshot snapshot = new MemoryStabilitySnapshot();
        snapshot.Available = true;
        snapshot.Mode = MemoryStabilityGuardMode;
        snapshot.State = "Normal";
        snapshot.Summary = "Memória estável";
        snapshot.Detail = "Modo shadow: diagnóstico ativo; sem trim global, sem alteração de pagefile.";
        snapshot.PagefileStatus = "Indisponível";
        snapshot.BrowserBurstState = "Normal";

        try
        {
            MemorySnapshot memory = GetMemorySnapshot();
            snapshot.TotalPhysicalMB = MemoryStabilityBytesToMb(memory.TotalPhysical);
            snapshot.AvailablePhysicalMB = MemoryStabilityBytesToMb(memory.AvailablePhysical);
            snapshot.CommitLimitMB = MemoryStabilityBytesToMb(memory.TotalPageFile);
            snapshot.CommitHeadroomMB = MemoryStabilityBytesToMb(memory.AvailablePageFile);
            snapshot.CommitUsedMB = Math.Max(0, Math.Round(snapshot.CommitLimitMB - snapshot.CommitHeadroomMB, 1));
            snapshot.CommitHeadroomPercent = MemoryStabilityPercent(snapshot.CommitHeadroomMB, snapshot.CommitLimitMB);
            snapshot.MemoryLoad = memory.MemoryLoad;

            if (memory.TotalPhysical <= 0 || memory.TotalPageFile <= 0)
            {
                snapshot.Available = false;
                snapshot.Relevant = false;
                snapshot.State = "DiagnosticRequired";
                snapshot.Summary = "Memória não analisada";
                snapshot.Detail = "O Windows não retornou um snapshot completo de memória para o guard.";
                snapshot.Signals.Add("Snapshot de memória indisponível.");
                return snapshot;
            }

            bool lowMemory;
            snapshot.LowMemorySignal = TryQueryLowMemoryNotification(out lowMemory) && lowMemory;

            double configuredPagefileMB = Math.Max(0, snapshot.CommitLimitMB - snapshot.TotalPhysicalMB);
            if (configuredPagefileMB <= 0)
            {
                snapshot.PagefileStatus = "Não identificado";
            }
            else if (configuredPagefileMB < Math.Min(1024, snapshot.TotalPhysicalMB * 0.10))
            {
                snapshot.PagefileStatus = "Limitado";
                snapshot.PagefileLimited = true;
            }
            else if (snapshot.CommitHeadroomPercent > 0 && snapshot.CommitHeadroomPercent <= 12)
            {
                snapshot.PagefileStatus = "Baixo headroom";
            }
            else
            {
                snapshot.PagefileStatus = "Saudável";
            }

            snapshot.TopConsumers = SampleMemoryStabilityProcesses(sessionAgent);
            MemoryStabilityProcessSample top = snapshot.TopConsumers.Count > 0 ? snapshot.TopConsumers[0] : null;
            if (top != null)
            {
                snapshot.TopProcess = top.Name;
                snapshot.TopProcessPid = top.Pid;
                snapshot.TopProcessPrivateMB = top.PrivateBytesMB;
                snapshot.TopProcessWorkingSetMB = top.WorkingSetMB;
            }

            List<MemoryStabilityProcessSample> browsers = snapshot.TopConsumers.Where(item => item.Browser).ToList();
            snapshot.BrowserProcessCount = browsers.Count;
            snapshot.BrowserPrivateMB = Math.Round(browsers.Sum(item => item.PrivateBytesMB), 1);
            snapshot.BrowserWorkingSetMB = Math.Round(browsers.Sum(item => item.WorkingSetMB), 1);
            snapshot.HeavyRecentProcessCount = snapshot.TopConsumers.Count(item => item.AgeSeconds >= 0 && item.AgeSeconds <= 90 && Math.Max(item.PrivateBytesMB, item.WorkingSetMB) >= 384);

            bool gamingContext = IsMemoryStabilityGamingContext(sessionAgent);
            bool browserPressure = snapshot.BrowserProcessCount >= 2 && Math.Max(snapshot.BrowserPrivateMB, snapshot.BrowserWorkingSetMB) >= 1024;
            bool pressureSignal = snapshot.MemoryLoad >= 70 || snapshot.CommitHeadroomPercent <= 20 || snapshot.AvailablePhysicalMB <= 2048;
            snapshot.BrowserBurstRecommended = gamingContext && browserPressure && pressureSignal;
            if (snapshot.BrowserBurstRecommended)
            {
                snapshot.BrowserBurstState = "Browser burst em observação";
            }
            else if (browserPressure)
            {
                snapshot.BrowserBurstState = "Navegador pesado observado";
            }

            int ramHeadroomPercent = MemoryStabilityPercent(snapshot.AvailablePhysicalMB, snapshot.TotalPhysicalMB);
            bool critical = snapshot.LowMemorySignal || snapshot.MemoryLoad >= 92 || ramHeadroomPercent <= 6 || snapshot.CommitHeadroomPercent <= 6 || (snapshot.PagefileLimited && snapshot.CommitHeadroomPercent <= 15);
            bool high = snapshot.MemoryLoad >= 84 || ramHeadroomPercent <= 12 || snapshot.CommitHeadroomPercent <= 12;
            bool observing = snapshot.MemoryLoad >= 72 || ramHeadroomPercent <= 20 || snapshot.CommitHeadroomPercent <= 20 || snapshot.BrowserBurstRecommended || snapshot.HeavyRecentProcessCount >= 3;

            if (critical)
            {
                snapshot.State = "CriticalPressure";
                snapshot.Summary = "Pressão crítica de memória";
            }
            else if (high)
            {
                snapshot.State = "HighPressure";
                snapshot.Summary = "Pressão de memória elevada";
            }
            else if (observing)
            {
                snapshot.State = "Observing";
                snapshot.Summary = "Observando pressão de memória";
            }

            snapshot.Relevant = !String.Equals(snapshot.State, "Normal", StringComparison.OrdinalIgnoreCase) || snapshot.BrowserBurstRecommended || snapshot.PagefileLimited || snapshot.LowMemorySignal;
            snapshot.Signals.Add("RAM disponível " + snapshot.AvailablePhysicalMB.ToString("0.#", CultureInfo.InvariantCulture) + " MB de " + snapshot.TotalPhysicalMB.ToString("0.#", CultureInfo.InvariantCulture) + " MB.");
            snapshot.Signals.Add("Commit livre " + snapshot.CommitHeadroomMB.ToString("0.#", CultureInfo.InvariantCulture) + " MB (" + snapshot.CommitHeadroomPercent.ToString(CultureInfo.InvariantCulture) + "%).");
            snapshot.Signals.Add("Pagefile: " + snapshot.PagefileStatus + ".");
            if (snapshot.LowMemorySignal) { snapshot.Signals.Add("Windows sinalizou baixa memória física."); }
            if (snapshot.BrowserBurstRecommended) { snapshot.Signals.Add("Browser Burst Shield recomendado em modo shadow."); }
            if (top != null) { snapshot.Signals.Add("Maior consumidor observado: " + top.Name + " (" + top.Pid.ToString(CultureInfo.InvariantCulture) + ")."); }
        }
        catch (Exception ex)
        {
            snapshot.Available = false;
            snapshot.Relevant = false;
            snapshot.State = "DiagnosticRequired";
            snapshot.Summary = "Memória não analisada";
            snapshot.Detail = "Falha ao montar diagnóstico de memória: " + ShortTaskError(ex.Message);
            snapshot.Signals.Add("Falha no Memory Stability Guard: " + ShortTaskError(ex.Message));
        }

        MaybeLogMemoryStability(snapshot);
        return snapshot;
    }

    private static List<object> BuildMemoryStabilityProcessPayload(List<MemoryStabilityProcessSample> samples)
    {
        List<object> result = new List<object>();
        if (samples == null) { return result; }
        foreach (MemoryStabilityProcessSample sample in samples.Take(5))
        {
            IDictionary<string, object> item = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            item["Name"] = sample.Name ?? "";
            item["Pid"] = sample.Pid;
            item["WorkingSetMB"] = sample.WorkingSetMB;
            item["PrivateBytesMB"] = sample.PrivateBytesMB;
            item["AgeSeconds"] = sample.AgeSeconds;
            item["Browser"] = sample.Browser;
            item["Foreground"] = sample.Foreground;
            result.Add(item);
        }
        return result;
    }

    private static IDictionary<string, object> BuildMemoryStabilityPayload(MemoryStabilitySnapshot snapshot)
    {
        MemoryStabilitySnapshot value = snapshot ?? new MemoryStabilitySnapshot { Mode = MemoryStabilityGuardMode, State = "DiagnosticRequired", Summary = "Memória não analisada", Detail = "Snapshot indisponível." };
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["Available"] = value.Available;
        payload["Relevant"] = value.Relevant;
        payload["Mode"] = value.Mode ?? MemoryStabilityGuardMode;
        payload["State"] = value.State ?? "DiagnosticRequired";
        payload["Summary"] = value.Summary ?? "";
        payload["Detail"] = value.Detail ?? "";
        payload["MemoryLoad"] = value.MemoryLoad;
        payload["AvailablePhysicalMB"] = value.AvailablePhysicalMB;
        payload["TotalPhysicalMB"] = value.TotalPhysicalMB;
        payload["CommitUsedMB"] = value.CommitUsedMB;
        payload["CommitLimitMB"] = value.CommitLimitMB;
        payload["CommitHeadroomMB"] = value.CommitHeadroomMB;
        payload["CommitHeadroomPercent"] = value.CommitHeadroomPercent;
        payload["PagefileStatus"] = value.PagefileStatus ?? "";
        payload["PagefileLimited"] = value.PagefileLimited;
        payload["LowMemorySignal"] = value.LowMemorySignal;
        payload["BrowserBurstRecommended"] = value.BrowserBurstRecommended;
        payload["TopProcess"] = value.TopProcess ?? "";
        payload["TopProcessPid"] = value.TopProcessPid;
        payload["TopProcessPrivateMB"] = value.TopProcessPrivateMB;
        payload["TopProcessWorkingSetMB"] = value.TopProcessWorkingSetMB;
        payload["BrowserProcessCount"] = value.BrowserProcessCount;
        payload["BrowserPrivateMB"] = value.BrowserPrivateMB;
        payload["BrowserWorkingSetMB"] = value.BrowserWorkingSetMB;
        payload["BrowserBurstState"] = value.BrowserBurstState ?? "";
        payload["HeavyRecentProcessCount"] = value.HeavyRecentProcessCount;
        payload["Signals"] = value.Signals ?? new List<string>();
        payload["TopConsumers"] = BuildMemoryStabilityProcessPayload(value.TopConsumers);
        return payload;
    }

    private static IDictionary<string, object> BuildMemoryStabilityPayload(CoreServiceSnapshot snapshot)
    {
        CoreServiceSnapshot value = snapshot ?? new CoreServiceSnapshot();
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["Available"] = value.MemoryStabilityAvailable;
        payload["Relevant"] = value.MemoryStabilityRelevant;
        payload["Mode"] = String.IsNullOrWhiteSpace(value.MemoryStabilityMode) ? MemoryStabilityGuardMode : value.MemoryStabilityMode;
        payload["State"] = value.MemoryStabilityState ?? "";
        payload["Summary"] = value.MemoryStabilitySummary ?? "";
        payload["Detail"] = value.MemoryStabilityDetail ?? "";
        payload["MemoryLoad"] = value.MemoryStabilityMemoryLoad;
        payload["AvailablePhysicalMB"] = value.MemoryStabilityAvailablePhysicalMB;
        payload["TotalPhysicalMB"] = value.MemoryStabilityTotalPhysicalMB;
        payload["CommitUsedMB"] = value.MemoryStabilityCommitUsedMB;
        payload["CommitLimitMB"] = value.MemoryStabilityCommitLimitMB;
        payload["CommitHeadroomMB"] = value.MemoryStabilityCommitHeadroomMB;
        payload["CommitHeadroomPercent"] = value.MemoryStabilityCommitHeadroomPercent;
        payload["PagefileStatus"] = value.MemoryStabilityPagefileStatus ?? "";
        payload["PagefileLimited"] = value.MemoryStabilityPagefileLimited;
        payload["LowMemorySignal"] = value.MemoryStabilityLowMemorySignal;
        payload["BrowserBurstRecommended"] = value.MemoryStabilityBrowserBurstRecommended;
        payload["TopProcess"] = value.MemoryStabilityTopProcess ?? "";
        payload["TopProcessPid"] = value.MemoryStabilityTopProcessPid;
        payload["TopProcessPrivateMB"] = value.MemoryStabilityTopProcessPrivateMB;
        payload["TopProcessWorkingSetMB"] = value.MemoryStabilityTopProcessWorkingSetMB;
        payload["BrowserProcessCount"] = value.MemoryStabilityBrowserProcessCount;
        payload["BrowserPrivateMB"] = value.MemoryStabilityBrowserPrivateMB;
        payload["BrowserWorkingSetMB"] = value.MemoryStabilityBrowserWorkingSetMB;
        payload["BrowserBurstState"] = value.MemoryStabilityBrowserBurstState ?? "";
        payload["HeavyRecentProcessCount"] = value.MemoryStabilityHeavyRecentProcessCount;
        payload["Signals"] = value.MemoryStabilitySignals ?? new List<string>();
        return payload;
    }

    private static void ApplyMemoryStabilitySnapshot(IDictionary<string, object> target, MemoryStabilitySnapshot snapshot)
    {
        if (target == null || snapshot == null) { return; }
        target["MemoryStability"] = BuildMemoryStabilityPayload(snapshot);
        target["MemoryStabilityAvailable"] = snapshot.Available;
        target["MemoryStabilityRelevant"] = snapshot.Relevant;
        target["MemoryStabilityMode"] = snapshot.Mode ?? MemoryStabilityGuardMode;
        target["MemoryStabilityState"] = snapshot.State ?? "";
        target["MemoryStabilitySummary"] = snapshot.Summary ?? "";
        target["MemoryStabilityDetail"] = snapshot.Detail ?? "";
        target["MemoryStabilityMemoryLoad"] = snapshot.MemoryLoad;
        target["MemoryStabilityAvailablePhysicalMB"] = snapshot.AvailablePhysicalMB;
        target["MemoryStabilityTotalPhysicalMB"] = snapshot.TotalPhysicalMB;
        target["MemoryStabilityCommitUsedMB"] = snapshot.CommitUsedMB;
        target["MemoryStabilityCommitLimitMB"] = snapshot.CommitLimitMB;
        target["MemoryStabilityCommitHeadroomMB"] = snapshot.CommitHeadroomMB;
        target["MemoryStabilityCommitHeadroomPercent"] = snapshot.CommitHeadroomPercent;
        target["MemoryStabilityPagefileStatus"] = snapshot.PagefileStatus ?? "";
        target["MemoryStabilityPagefileLimited"] = snapshot.PagefileLimited;
        target["MemoryStabilityLowMemorySignal"] = snapshot.LowMemorySignal;
        target["MemoryStabilityBrowserBurstRecommended"] = snapshot.BrowserBurstRecommended;
        target["MemoryStabilityTopProcess"] = snapshot.TopProcess ?? "";
        target["MemoryStabilityTopProcessPid"] = snapshot.TopProcessPid;
        target["MemoryStabilityTopProcessPrivateMB"] = snapshot.TopProcessPrivateMB;
        target["MemoryStabilityTopProcessWorkingSetMB"] = snapshot.TopProcessWorkingSetMB;
        target["MemoryStabilityBrowserProcessCount"] = snapshot.BrowserProcessCount;
        target["MemoryStabilityBrowserPrivateMB"] = snapshot.BrowserPrivateMB;
        target["MemoryStabilityBrowserWorkingSetMB"] = snapshot.BrowserWorkingSetMB;
        target["MemoryStabilityBrowserBurstState"] = snapshot.BrowserBurstState ?? "";
        target["MemoryStabilityHeavyRecentProcessCount"] = snapshot.HeavyRecentProcessCount;
        target["MemoryStabilitySignals"] = snapshot.Signals ?? new List<string>();
    }

    private static void MaybeLogMemoryStability(MemoryStabilitySnapshot snapshot)
    {
        if (snapshot == null || !snapshot.Relevant) { return; }
        string signature = String.Join("|", new string[]
        {
            snapshot.State ?? "",
            snapshot.CommitHeadroomPercent.ToString(CultureInfo.InvariantCulture),
            snapshot.PagefileStatus ?? "",
            snapshot.BrowserBurstRecommended ? "browser" : "",
            snapshot.TopProcess ?? ""
        });
        lock (memoryStabilityLogLock)
        {
            if (String.Equals(signature, memoryStabilityLastLogSignature, StringComparison.Ordinal)) { return; }
            memoryStabilityLastLogSignature = signature;
        }
        AppendOperationalLog("action=memory-stability-guard mode=" + SanitizeLogToken(snapshot.Mode) +
            " state=" + SanitizeLogToken(snapshot.State) +
            " ramLoad=" + snapshot.MemoryLoad.ToString(CultureInfo.InvariantCulture) +
            " commitHeadroomPercent=" + snapshot.CommitHeadroomPercent.ToString(CultureInfo.InvariantCulture) +
            " pagefile=" + SanitizeLogToken(snapshot.PagefileStatus) +
            " top=" + SanitizeLogToken(snapshot.TopProcess) +
            " browserPrivateMB=" + snapshot.BrowserPrivateMB.ToString("0.#", CultureInfo.InvariantCulture));
    }

    private static void AddSystemIntegrityIssue(SystemIntegritySnapshot snapshot, string issue)
    {
        if (snapshot == null || String.IsNullOrWhiteSpace(issue)) { return; }
        if (!snapshot.Issues.Contains(issue)) { snapshot.Issues.Add(issue); }
        snapshot.IssueCount = snapshot.Issues.Count;
    }

    private static void AddSystemOptimizationRecommendation(SystemIntegritySnapshot snapshot, SystemOptimizationRecommendation recommendation)
    {
        if (snapshot == null || recommendation == null || String.IsNullOrWhiteSpace(recommendation.Id) || String.IsNullOrWhiteSpace(recommendation.Title)) { return; }
        if (snapshot.Recommendations.Any(item => String.Equals(item.Id, recommendation.Id, StringComparison.OrdinalIgnoreCase))) { return; }
        if (String.IsNullOrWhiteSpace(recommendation.Tier)) { recommendation.Tier = "Optional"; }
        if (String.IsNullOrWhiteSpace(recommendation.Risk)) { recommendation.Risk = "Revisao necessaria"; }
        if (String.IsNullOrWhiteSpace(recommendation.Backup)) { recommendation.Backup = recommendation.BackupRequired ? "Snapshot obrigatorio antes de aplicar." : "Sem alteracao global do Windows."; }
        if (String.IsNullOrWhiteSpace(recommendation.SafetyGate)) { recommendation.SafetyGate = recommendation.CanApply ? "Aplicavel apos confirmacao do usuario." : "Bloqueado ate snapshot, journal e rollback confirmados pelo servico."; }
        recommendation.Experimental = recommendation.Experimental || String.Equals(recommendation.Tier, "Experimental", StringComparison.OrdinalIgnoreCase);
        snapshot.Recommendations.Add(recommendation);
    }

    private static void RefreshSystemOptimizationRecommendationCounts(SystemIntegritySnapshot snapshot)
    {
        if (snapshot == null) { return; }
        snapshot.RecommendationCount = snapshot.Recommendations == null ? 0 : snapshot.Recommendations.Count;
        snapshot.SafeRecommendationCount = snapshot.Recommendations == null ? 0 : snapshot.Recommendations.Count(item => String.Equals(item.Tier, "Safe", StringComparison.OrdinalIgnoreCase));
        snapshot.OptionalRecommendationCount = snapshot.Recommendations == null ? 0 : snapshot.Recommendations.Count(item => String.Equals(item.Tier, "Optional", StringComparison.OrdinalIgnoreCase));
        snapshot.ExperimentalRecommendationCount = snapshot.Recommendations == null ? 0 : snapshot.Recommendations.Count(item => String.Equals(item.Tier, "Experimental", StringComparison.OrdinalIgnoreCase));
        snapshot.RestartRecommendationCount = snapshot.Recommendations == null ? 0 : snapshot.Recommendations.Count(item => item.RequiresRestart || item.RequiresSignOut);
        snapshot.ApplyBlockedRecommendationCount = snapshot.Recommendations == null ? 0 : snapshot.Recommendations.Count(item => !item.CanApply);
    }

    private static Dictionary<string, object> BuildSystemOptimizationRecommendationPayload(SystemOptimizationRecommendation recommendation)
    {
        SystemOptimizationRecommendation value = recommendation ?? new SystemOptimizationRecommendation();
        Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["Id"] = value.Id ?? "";
        payload["Tier"] = value.Tier ?? "Optional";
        payload["Title"] = value.Title ?? "";
        payload["Summary"] = value.Summary ?? "";
        payload["Category"] = value.Category ?? "";
        payload["Reason"] = value.Reason ?? "";
        payload["Compatibility"] = value.Compatibility ?? "";
        payload["Risk"] = value.Risk ?? "";
        payload["Impact"] = value.Impact ?? "";
        payload["Restart"] = value.Restart ?? "";
        payload["Backup"] = value.Backup ?? "";
        payload["CurrentValue"] = value.CurrentValue ?? "";
        payload["RecommendedValue"] = value.RecommendedValue ?? "";
        payload["SafetyGate"] = value.SafetyGate ?? "";
        payload["ActionKind"] = value.ActionKind ?? "";
        payload["Source"] = value.Source ?? "Smart Nap";
        payload["Documentation"] = value.Documentation ?? "";
        payload["SelectedByDefault"] = value.SelectedByDefault;
        payload["CanApply"] = value.CanApply;
        payload["RequiresAdmin"] = value.RequiresAdmin;
        payload["RequiresRestart"] = value.RequiresRestart;
        payload["RequiresSignOut"] = value.RequiresSignOut;
        payload["RequiresGameClosed"] = value.RequiresGameClosed;
        payload["BackupRequired"] = value.BackupRequired;
        payload["Reversible"] = value.Reversible;
        payload["Experimental"] = value.Experimental;
        payload["Details"] = value.Details ?? new List<string>();
        return payload;
    }

    private static List<Dictionary<string, object>> BuildSystemOptimizationRecommendationPayloadList(List<SystemOptimizationRecommendation> recommendations)
    {
        List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
        if (recommendations == null) { return result; }
        foreach (SystemOptimizationRecommendation recommendation in recommendations)
        {
            result.Add(BuildSystemOptimizationRecommendationPayload(recommendation));
        }
        return result;
    }

    private static bool TryGetServiceStatusText(string serviceName, out string statusText, out bool running)
    {
        statusText = "Indisponivel";
        running = false;
        try
        {
            using (ServiceController controller = new ServiceController(serviceName))
            {
                ServiceControllerStatus status = controller.Status;
                statusText = status.ToString();
                running = status == ServiceControllerStatus.Running;
                return true;
            }
        }
        catch
        {
            statusText = "Nao encontrado";
            running = false;
            return false;
        }
    }

    private static bool IsLikelyHybridCpu(HardwareSnapshot hardware)
    {
        string cpu = ((hardware == null ? "" : hardware.Cpu) + " " + (hardware == null ? "" : hardware.CpuDetail)).Trim();
        if (String.IsNullOrWhiteSpace(cpu)) { return false; }
        if (cpu.IndexOf("Core Ultra", StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
        if (cpu.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) < 0) { return false; }
        Match match = Regex.Match(cpu, @"\bi[3579][\s-]?(\d{5})", RegexOptions.IgnoreCase);
        if (!match.Success) { return false; }
        string model = match.Groups[1].Value;
        int generation;
        if (model.Length < 2 || !Int32.TryParse(model.Substring(0, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out generation)) { return false; }
        return generation >= 12;
    }

    private static bool TryQueryDisableDeleteNotify(out int value, out string detail)
    {
        value = -1;
        detail = "";
        try
        {
            RunResult result = RunHidden("fsutil.exe", "behavior query DisableDeleteNotify", 7000);
            string output = result == null ? "" : (result.Output ?? "");
            if (result == null || result.ExitCode != 0 || String.IsNullOrWhiteSpace(output))
            {
                detail = "Consulta TRIM/UNMAP indisponivel.";
                return false;
            }
            Match match = Regex.Match(output, @"NTFS\s+DisableDeleteNotify\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success) { match = Regex.Match(output, @"DisableDeleteNotify\s*=\s*(\d+)", RegexOptions.IgnoreCase); }
            if (!match.Success)
            {
                detail = ShortTaskError(output);
                return false;
            }
            value = ParseInt(match.Groups[1].Value, -1);
            detail = value == 0 ? "TRIM/UNMAP habilitado." : "TRIM/UNMAP pode estar desabilitado.";
            return value >= 0;
        }
        catch (Exception ex)
        {
            detail = ShortTaskError(ex.Message);
            return false;
        }
    }

    private static SystemIntegritySnapshot BuildSystemIntegritySnapshot(SessionAgentSnapshot sessionAgent, string source)
    {
        SystemIntegritySnapshot snapshot = new SystemIntegritySnapshot();
        snapshot.Available = true;
        snapshot.Mode = SystemIntegrityGuardMode;
        snapshot.State = "Healthy";
        snapshot.Summary = "Windows sem alertas";
        snapshot.Detail = "Modo shadow: diagnostico ativo; nenhuma chave global e nenhuma politica de scheduler sao alteradas automaticamente.";
        snapshot.MmcssServiceStatus = "Indisponivel";
        snapshot.SystemResponsivenessState = "Nao analisado";
        snapshot.HybridSchedulerState = "Automático do Windows";
        snapshot.HybridSchedulerDetail = "Sem afinidade rigida; preferencias P/E-core permanecem sob controle do Windows.";
        snapshot.SelfThrottleState = "Normal";
        snapshot.SelfThrottleDetail = "UI em cadencia normal.";
        snapshot.BackupAvailable = false;
        snapshot.LogicalProcessorCount = Environment.ProcessorCount;

        try
        {
            string serviceStatus;
            bool serviceRunning;
            if (TryGetServiceStatusText("MMCSS", out serviceStatus, out serviceRunning))
            {
                snapshot.MmcssServiceStatus = serviceStatus;
                snapshot.MmcssServiceRunning = serviceRunning;
                if (serviceRunning) { snapshot.Signals.Add("MMCSS ativo."); }
                else
                {
                    AddSystemIntegrityIssue(snapshot, "MMCSS nao esta em execucao.");
                    AddSystemOptimizationRecommendation(snapshot, new SystemOptimizationRecommendation
                    {
                        Id = "mmcss-service-state",
                        Tier = "Optional",
                        Title = "Revisar servico MMCSS",
                        Summary = "O agendador multimidia do Windows nao esta em execucao no snapshot atual.",
                        Category = "Responsividade multimidia",
                        Reason = "Status real retornado pelo Windows: " + serviceStatus + ".",
                        Compatibility = "Windows retornou o servico MMCSS, mas ele nao esta ativo.",
                        Risk = "Medio",
                        Impact = "Pode afetar prioridades de audio, captura e jogos que dependem de classes multimidia.",
                        Restart = "Pode exigir reinicio do servico ou do Windows.",
                        Backup = "Sem alteracao automatica; exige diagnostico de servico antes de qualquer reparo.",
                        CurrentValue = serviceStatus,
                        RecommendedValue = "Running",
                        SafetyGate = "Bloqueado: reparo de servico precisa de fluxo dedicado, permissao administrativa e rollback.",
                        RequiresAdmin = true,
                        Reversible = false,
                        Source = "Windows ServiceController + Smart Nap"
                    });
                }
            }
            else
            {
                AddSystemIntegrityIssue(snapshot, "Servico MMCSS nao encontrado.");
                AddSystemOptimizationRecommendation(snapshot, new SystemOptimizationRecommendation
                {
                    Id = "mmcss-service-missing",
                    Tier = "Optional",
                    Title = "Diagnosticar MMCSS ausente",
                    Summary = "O Windows nao retornou o servico MMCSS para a consulta atual.",
                    Category = "Responsividade multimidia",
                    Reason = "A consulta local nao encontrou o servico Multimedia Class Scheduler.",
                    Compatibility = "Informacao insuficiente para alterar o sistema com seguranca.",
                    Risk = "Alto",
                    Impact = "Sem MMCSS validado, jogos, audio e captura podem nao receber a politica multimidia esperada.",
                    Restart = "Indefinido ate o diagnostico do Windows.",
                    Backup = "Nenhuma alteracao automatica sera aplicada.",
                    CurrentValue = "Nao encontrado",
                    RecommendedValue = "Diagnostico manual assistido",
                    SafetyGate = "Bloqueado: o Smart Nap nao recria servicos do Windows automaticamente.",
                    RequiresAdmin = true,
                    Reversible = false,
                    Source = "Windows ServiceController + Smart Nap"
                });
            }

            int responsiveness;
            if (TryReadRegistryIntValue(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", out responsiveness))
            {
                snapshot.SystemResponsiveness = responsiveness;
                if (responsiveness == 100)
                {
                    snapshot.SystemResponsivenessState = "MMCSS desativado";
                    snapshot.SystemResponsivenessDetail = "Valor 100 desativa a reserva do MMCSS.";
                    AddSystemIntegrityIssue(snapshot, "SystemResponsiveness = 100 pode desativar o MMCSS.");
                    AddSystemOptimizationRecommendation(snapshot, new SystemOptimizationRecommendation
                    {
                        Id = "mmcss-systemresponsiveness-100",
                        Tier = "Safe",
                        Title = "Restaurar integridade do MMCSS",
                        Summary = "SystemResponsiveness esta em 100, estado documentado que desativa a reserva do MMCSS.",
                        Category = "Responsividade do sistema",
                        Reason = "Valor atual lido em HKLM\\...\\Multimedia\\SystemProfile: 100.",
                        Compatibility = "Windows compatível; MMCSS deve estar presente antes da aplicacao.",
                        Risk = "Baixo com backup; bloqueado sem snapshot.",
                        Impact = "Pode recuperar o comportamento esperado para tarefas multimidia e jogos responsivos.",
                        Restart = "Reinicializacao ou nova sessao pode ser necessaria.",
                        Backup = "Snapshot do Registro obrigatorio antes de alterar.",
                        CurrentValue = "100",
                        RecommendedValue = "Restaurar valor documentado pelo Windows ou valor anterior confiavel do backup.",
                        SafetyGate = "Bloqueado: exige snapshot, journal de aplicacao e rollback por item.",
                        SelectedByDefault = true,
                        CanApply = false,
                        RequiresAdmin = true,
                        RequiresRestart = true,
                        BackupRequired = true,
                        Reversible = true,
                        Source = "Microsoft MMCSS + Smart Nap Integrity Check",
                        Documentation = "Multimedia Class Scheduler Service"
                    });
                }
                else if (responsiveness < 0 || responsiveness > 100)
                {
                    snapshot.SystemResponsivenessState = "Fora da faixa";
                    snapshot.SystemResponsivenessDetail = "Valor fora da faixa documentada.";
                    AddSystemIntegrityIssue(snapshot, "SystemResponsiveness fora da faixa documentada.");
                    AddSystemOptimizationRecommendation(snapshot, new SystemOptimizationRecommendation
                    {
                        Id = "mmcss-systemresponsiveness-range",
                        Tier = "Safe",
                        Title = "Corrigir valor fora da faixa do MMCSS",
                        Summary = "SystemResponsiveness esta fora da faixa documentada para o Windows.",
                        Category = "Responsividade do sistema",
                        Reason = "Valor atual lido: " + responsiveness.ToString(CultureInfo.InvariantCulture) + ".",
                        Compatibility = "Ajuste so deve ser aplicado apos confirmar MMCSS, chave e backup.",
                        Risk = "Baixo com backup; bloqueado sem snapshot.",
                        Impact = "Remove configuracao inconsistente herdada de tweak externo.",
                        Restart = "Reinicializacao ou nova sessao pode ser necessaria.",
                        Backup = "Snapshot do Registro obrigatorio antes de alterar.",
                        CurrentValue = responsiveness.ToString(CultureInfo.InvariantCulture),
                        RecommendedValue = "Restaurar valor documentado pelo Windows ou valor anterior confiavel do backup.",
                        SafetyGate = "Bloqueado: exige snapshot, journal de aplicacao e rollback por item.",
                        SelectedByDefault = true,
                        CanApply = false,
                        RequiresAdmin = true,
                        RequiresRestart = true,
                        BackupRequired = true,
                        Reversible = true,
                        Source = "Microsoft MMCSS + Smart Nap Integrity Check",
                        Documentation = "Multimedia Class Scheduler Service"
                    });
                }
                else if (responsiveness < 10)
                {
                    snapshot.SystemResponsivenessState = "Alterado por tweak";
                    snapshot.SystemResponsivenessDetail = "O Windows trata valores abaixo de 10 como 20.";
                    AddSystemIntegrityIssue(snapshot, "SystemResponsiveness abaixo de 10 exige revisao; o Windows interpreta como 20.");
                    AddSystemOptimizationRecommendation(snapshot, new SystemOptimizationRecommendation
                    {
                        Id = "mmcss-systemresponsiveness-low",
                        Tier = "Safe",
                        Title = "Revisar SystemResponsiveness alterado",
                        Summary = "O valor abaixo de 10 parece tweak externo; o Windows normaliza esse caso para 20.",
                        Category = "Responsividade do sistema",
                        Reason = "Valor atual lido: " + responsiveness.ToString(CultureInfo.InvariantCulture) + ". A documentacao do Windows trata valores abaixo de 10 como 20.",
                        Compatibility = "Windows compatível; ajuste reversivel com snapshot do Registro.",
                        Risk = "Baixo com backup; bloqueado sem snapshot.",
                        Impact = "Remove uma configuracao enganosa que nao entrega o efeito anunciado por tweaks antigos.",
                        Restart = "Reinicializacao ou nova sessao pode ser necessaria.",
                        Backup = "Snapshot do Registro obrigatorio antes de alterar.",
                        CurrentValue = responsiveness.ToString(CultureInfo.InvariantCulture),
                        RecommendedValue = "Valor documentado/normalizado pelo Windows apos backup.",
                        SafetyGate = "Bloqueado: exige snapshot, journal de aplicacao e rollback por item.",
                        SelectedByDefault = true,
                        CanApply = false,
                        RequiresAdmin = true,
                        RequiresRestart = true,
                        BackupRequired = true,
                        Reversible = true,
                        Source = "Microsoft MMCSS + Smart Nap Integrity Check",
                        Documentation = "Multimedia Class Scheduler Service"
                    });
                }
                else
                {
                    snapshot.SystemResponsivenessState = "Dentro da faixa";
                    snapshot.SystemResponsivenessDetail = "Valor documentado.";
                    snapshot.Signals.Add("SystemResponsiveness = " + responsiveness.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
            else
            {
                snapshot.SystemResponsivenessState = "Nao identificado";
                snapshot.SystemResponsivenessDetail = "Chave MMCSS nao retornou valor.";
                snapshot.Signals.Add("SystemResponsiveness nao identificado.");
            }

            string[] mmcssTasks = new string[] { "Games", "Audio", "Capture", "Playback" };
            List<string> missingTasks = new List<string>();
            foreach (string task in mmcssTasks)
            {
                if (!RegistrySubKeyExists(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\" + task))
                {
                    missingTasks.Add(task);
                }
            }
            if (missingTasks.Count > 0)
            {
                string missingText = String.Join(", ", missingTasks.ToArray());
                AddSystemIntegrityIssue(snapshot, "Tarefas MMCSS ausentes: " + missingText + ".");
                AddSystemOptimizationRecommendation(snapshot, new SystemOptimizationRecommendation
                {
                    Id = "mmcss-tasks-missing",
                    Tier = "Optional",
                    Title = "Revisar tarefas multimidia ausentes",
                    Summary = "Uma ou mais classes MMCSS esperadas nao foram encontradas no Registro.",
                    Category = "Responsividade multimidia",
                    Reason = "Tarefas ausentes: " + missingText + ".",
                    Compatibility = "Aplicacao automatica bloqueada; reparar tarefas MMCSS sem fonte confiavel pode piorar o Windows.",
                    Risk = "Alto",
                    Impact = "Pode afetar Games, Audio, Capture ou Playback dependendo do app.",
                    Restart = "Indefinido ate diagnostico de reparo.",
                    Backup = "Nenhuma alteracao automatica sera aplicada.",
                    CurrentValue = missingText,
                    RecommendedValue = "Reparo validado pelo Windows ou restauracao por backup confiavel.",
                    SafetyGate = "Bloqueado: o Smart Nap nao recria tarefas MMCSS sem origem assinada/validada.",
                    RequiresAdmin = true,
                    BackupRequired = true,
                    Reversible = false,
                    Source = "Microsoft MMCSS + Smart Nap Integrity Check"
                });
            }
            else { snapshot.Signals.Add("Tarefas MMCSS principais presentes."); }

            int ntfsMemory;
            if (TryReadRegistryIntValue(@"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsMemoryUsage", out ntfsMemory))
            {
                if (ntfsMemory >= 2)
                {
                    AddSystemIntegrityIssue(snapshot, "NtfsMemoryUsage ampliado pode reduzir memoria disponivel para jogos.");
                    AddSystemOptimizationRecommendation(snapshot, new SystemOptimizationRecommendation
                    {
                        Id = "ntfs-memory-usage-expanded",
                        Tier = "Optional",
                        Title = "Revisar cache ampliado do NTFS",
                        Summary = "NtfsMemoryUsage esta ampliado e pode competir com RAM usada por jogos quando o sistema fica pressionado.",
                        Category = "Memoria e armazenamento",
                        Reason = "Valor atual lido em HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem: " + ntfsMemory.ToString(CultureInfo.InvariantCulture) + ".",
                        Compatibility = "Windows compatível; recomendacao depende da pressao real de RAM e deve ser reversivel.",
                        Risk = "Baixo com backup; opcional conforme uso.",
                        Impact = "Pode devolver margem de RAM em PCs que ja chegam perto do limite durante jogos.",
                        Restart = "Reinicializacao pode ser necessaria para efeito completo.",
                        Backup = "Snapshot do Registro obrigatorio antes de alterar.",
                        CurrentValue = ntfsMemory.ToString(CultureInfo.InvariantCulture),
                        RecommendedValue = "Padrao do Windows ou valor anterior confiavel do backup.",
                        SafetyGate = "Bloqueado: exige snapshot, journal de aplicacao e rollback por item.",
                        SelectedByDefault = false,
                        CanApply = false,
                        RequiresAdmin = true,
                        RequiresRestart = true,
                        BackupRequired = true,
                        Reversible = true,
                        Source = "Microsoft fsutil/FileSystem + Smart Nap Sanity Scanner",
                        Documentation = "fsutil behavior memoryusage"
                    });
                }
                else { snapshot.Signals.Add("NtfsMemoryUsage dentro do esperado."); }
            }

            int mftZone;
            if (TryReadRegistryIntValue(@"SYSTEM\CurrentControlSet\Control\FileSystem", "MftZoneReservation", out mftZone) && mftZone > 0)
            {
                snapshot.Signals.Add("MftZoneReservation alterado: " + mftZone.ToString(CultureInfo.InvariantCulture) + ".");
            }

            int lastAccess;
            if (TryReadRegistryIntValue(@"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisableLastAccessUpdate", out lastAccess))
            {
                snapshot.Signals.Add("NtfsDisableLastAccessUpdate = " + lastAccess.ToString(CultureInfo.InvariantCulture) + ".");
            }

            int presenceQos;
            if (TryReadRegistryIntValue(@"SYSTEM\CurrentControlSet\Control\Power", "DisableUserPresenceQos", out presenceQos) && presenceQos != 0)
            {
                AddSystemIntegrityIssue(snapshot, "DisableUserPresenceQos esta ativo; esse ajuste e indicado principalmente para benchmarks automatizados.");
                AddSystemOptimizationRecommendation(snapshot, new SystemOptimizationRecommendation
                {
                    Id = "user-presence-qos-disabled",
                    Tier = "Optional",
                    Title = "Revisar User Presence QoS",
                    Summary = "Uma heuristica de presenca do usuario esta desativada; isso costuma fazer sentido para benchmarks, nao como boost gamer universal.",
                    Category = "Politicas de energia",
                    Reason = "DisableUserPresenceQos esta ativo no snapshot atual.",
                    Compatibility = "Ajuste sensivel; recomendacao depende do perfil de uso e precisa de backup.",
                    Risk = "Medio",
                    Impact = "Pode restaurar comportamento normal de responsividade/energia fora de benchmarks automatizados.",
                    Restart = "Reinicializacao ou nova sessao pode ser necessaria.",
                    Backup = "Snapshot do Registro obrigatorio antes de alterar.",
                    CurrentValue = presenceQos.ToString(CultureInfo.InvariantCulture),
                    RecommendedValue = "Padrao do Windows ou valor anterior confiavel do backup.",
                    SafetyGate = "Bloqueado: exige snapshot, journal e confirmacao explicita do usuario.",
                    SelectedByDefault = false,
                    CanApply = false,
                    RequiresAdmin = true,
                    RequiresRestart = true,
                    BackupRequired = true,
                    Reversible = true,
                    Source = "Windows power policy + Smart Nap Sanity Scanner"
                });
            }

            int deleteNotify;
            string trimDetail;
            if (TryQueryDisableDeleteNotify(out deleteNotify, out trimDetail))
            {
                if (deleteNotify == 0) { snapshot.Signals.Add("TRIM/UNMAP habilitado."); }
                else
                {
                    AddSystemIntegrityIssue(snapshot, "DisableDeleteNotify indica TRIM/UNMAP desabilitado.");
                    AddSystemOptimizationRecommendation(snapshot, new SystemOptimizationRecommendation
                    {
                        Id = "storage-trim-disabled",
                        Tier = "Safe",
                        Title = "Validar TRIM/UNMAP do armazenamento",
                        Summary = "O Windows informou DisableDeleteNotify diferente de zero no snapshot atual.",
                        Category = "Armazenamento",
                        Reason = "Consulta fsutil retornou: " + trimDetail + ".",
                        Compatibility = "Aplicar somente se o volume e o dispositivo oferecerem suporte real a TRIM/UNMAP.",
                        Risk = "Baixo quando suportado; bloqueado ate validacao do dispositivo.",
                        Impact = "Pode preservar desempenho e limpeza interna em SSD/NVMe compatível.",
                        Restart = "Normalmente nao exige reinicio, mas a validacao do volume e obrigatoria.",
                        Backup = "Snapshot e verificacao de suporte obrigatorios antes de alterar.",
                        CurrentValue = deleteNotify.ToString(CultureInfo.InvariantCulture),
                        RecommendedValue = "0 somente em dispositivo com suporte confirmado.",
                        SafetyGate = "Bloqueado: exige validacao do tipo de disco/volume e rollback.",
                        SelectedByDefault = false,
                        CanApply = false,
                        RequiresAdmin = true,
                        BackupRequired = true,
                        Reversible = true,
                        Source = "Microsoft fsutil + Smart Nap Sanity Scanner",
                        Documentation = "fsutil behavior DisableDeleteNotify"
                    });
                }
            }
            else if (!String.IsNullOrWhiteSpace(trimDetail))
            {
                snapshot.Signals.Add(trimDetail);
            }

            HardwareSnapshot hardware = GetHardwareSnapshot();
            snapshot.HybridCpuDetected = IsLikelyHybridCpu(hardware);
            if (snapshot.HybridCpuDetected)
            {
                snapshot.HybridSchedulerState = "CPU hibrida detectada";
                snapshot.HybridSchedulerDetail = "Guard em shadow: sem afinidade rigida; perfis futuros devem preferir P-cores/E-cores por politica validada.";
                snapshot.Signals.Add("CPU hibrida provavel; scheduler mantido no automatico do Windows.");
                AddSystemOptimizationRecommendation(snapshot, new SystemOptimizationRecommendation
                {
                    Id = "hybrid-cpu-scheduler-guard",
                    Tier = "Experimental",
                    Title = "Preparar perfil para CPU hibrida",
                    Summary = "O PC parece usar P-cores/E-cores; o Smart Nap deve preferir politicas por contexto, sem afinidade rigida por padrao.",
                    Category = "Agendamento de CPU",
                    Reason = "CPU detectada: " + (hardware == null ? "Nao identificada" : FirstNonEmpty(hardware.Cpu, "Nao identificada")) + ".",
                    Compatibility = "Somente shadow nesta versao; nenhuma politica global de scheduler sera alterada automaticamente.",
                    Risk = "Experimental",
                    Impact = "Base para favorecer desempenho no foreground e eficiencia no fundo sem travar processos em nucleos fixos.",
                    Restart = "Nao aplicavel nesta versao shadow.",
                    Backup = "Sem alteracao global do Windows.",
                    CurrentValue = "Automatico do Windows",
                    RecommendedValue = "Perfil contextual validado por CPU/familia em versao futura.",
                    SafetyGate = "Bloqueado: SCHEDPOLICY/SHORTSCHEDPOLICY globais exigem validacao por familia de CPU e rollback.",
                    SelectedByDefault = false,
                    CanApply = false,
                    BackupRequired = true,
                    Reversible = true,
                    Experimental = true,
                    Source = "Windows heterogeneous scheduling + Smart Nap Hybrid Guard"
                });
            }
            else
            {
                snapshot.Signals.Add("CPU hibrida nao detectada pelo perfil atual.");
            }

            bool gameOrFullscreen = sessionAgent != null && (sessionAgent.ForegroundIsGame || sessionAgent.ForegroundFullscreen || IsMemoryStabilityGamingContext(sessionAgent));
            snapshot.SelfThrottleEligible = gameOrFullscreen;
            if (gameOrFullscreen)
            {
                snapshot.SelfThrottleState = "Modo leve recomendado";
                snapshot.SelfThrottleDetail = "Jogo/tela cheia em foco; o launcher pode reduzir cadencia visual e coleta nao essencial.";
                snapshot.Signals.Add("Low Impact Runtime elegivel nesta sessao.");
                AddSystemOptimizationRecommendation(snapshot, new SystemOptimizationRecommendation
                {
                    Id = "smart-nap-low-impact-runtime",
                    Tier = "Safe",
                    Title = "Manter Smart Nap em baixo impacto durante jogo",
                    Summary = "Quando jogo ou tela cheia esta em foco, o launcher reduz cadencia visual e trabalho nao essencial.",
                    Category = "Peso do proprio Smart Nap",
                    Reason = "Sessao atual indica jogo/tela cheia ou contexto de jogo pelo Session Agent.",
                    Compatibility = "Gerenciado pelo proprio processo; nao altera chaves globais do Windows.",
                    Risk = "Baixo",
                    Impact = "Reduz interferencia do launcher em CPU, I/O, memoria e atualizacoes visuais durante jogo.",
                    Restart = "Nao exige reinicio.",
                    Backup = "Nao modifica Registro nem plano de energia global.",
                    CurrentValue = "Elegivel nesta sessao",
                    RecommendedValue = "Ativar automaticamente quando o jogo estiver em foco ou a janela estiver minimizada.",
                    SafetyGate = "Seguro: rotina interna reversivel e limitada ao processo do Smart Nap.",
                    SelectedByDefault = true,
                    CanApply = false,
                    Reversible = true,
                    Source = "PROCESS_MODE_BACKGROUND_BEGIN + Smart Nap Low Impact Runtime",
                    Documentation = "SetPriorityClass background mode"
                });
            }
            else
            {
                snapshot.SelfThrottleState = "Cadencia normal";
                snapshot.SelfThrottleDetail = "Nenhum jogo ou tela cheia exige reducao extra agora.";
            }

            snapshot.IssueCount = snapshot.Issues.Count;
            RefreshSystemOptimizationRecommendationCounts(snapshot);
            if (snapshot.IssueCount > 0)
            {
                snapshot.State = "Attention";
                snapshot.Summary = snapshot.RecommendationCount > 0
                    ? (snapshot.RecommendationCount.ToString(CultureInfo.InvariantCulture) + " " + (snapshot.RecommendationCount == 1 ? "recomendação" : "recomendações") + " para revisar")
                    : "Tweaks do Windows exigem revisão";
                snapshot.Detail = snapshot.Issues[0];
            }
            else if (snapshot.HybridCpuDetected || snapshot.SelfThrottleEligible)
            {
                snapshot.State = "Observing";
                snapshot.Summary = "Guardas de sistema em shadow";
            }

            RefreshSystemOptimizationRecommendationCounts(snapshot);
            snapshot.Relevant = snapshot.IssueCount > 0 || snapshot.HybridCpuDetected || snapshot.SelfThrottleEligible || snapshot.RecommendationCount > 0;
        }
        catch (Exception ex)
        {
            snapshot.Available = false;
            snapshot.Relevant = false;
            snapshot.State = "DiagnosticRequired";
            snapshot.Summary = "Windows nao analisado";
            snapshot.Detail = "Falha ao montar diagnostico do sistema: " + ShortTaskError(ex.Message);
            snapshot.Signals.Add("Falha no System Integrity Guard: " + ShortTaskError(ex.Message));
        }

        MaybeLogSystemIntegrity(snapshot);
        return snapshot;
    }

    private static IDictionary<string, object> BuildSystemIntegrityPayload(SystemIntegritySnapshot snapshot)
    {
        SystemIntegritySnapshot value = snapshot ?? new SystemIntegritySnapshot { Mode = SystemIntegrityGuardMode, State = "DiagnosticRequired", Summary = "Windows nao analisado", Detail = "Snapshot indisponivel." };
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["Available"] = value.Available;
        payload["Relevant"] = value.Relevant;
        payload["Mode"] = value.Mode ?? SystemIntegrityGuardMode;
        payload["State"] = value.State ?? "DiagnosticRequired";
        payload["Summary"] = value.Summary ?? "";
        payload["Detail"] = value.Detail ?? "";
        payload["BackupAvailable"] = value.BackupAvailable;
        payload["MmcssServiceRunning"] = value.MmcssServiceRunning;
        payload["MmcssServiceStatus"] = value.MmcssServiceStatus ?? "";
        payload["SystemResponsiveness"] = value.SystemResponsiveness;
        payload["SystemResponsivenessState"] = value.SystemResponsivenessState ?? "";
        payload["SystemResponsivenessDetail"] = value.SystemResponsivenessDetail ?? "";
        payload["HybridCpuDetected"] = value.HybridCpuDetected;
        payload["LogicalProcessorCount"] = value.LogicalProcessorCount;
        payload["HybridSchedulerState"] = value.HybridSchedulerState ?? "";
        payload["HybridSchedulerDetail"] = value.HybridSchedulerDetail ?? "";
        payload["SelfThrottleEligible"] = value.SelfThrottleEligible;
        payload["SelfThrottleState"] = value.SelfThrottleState ?? "";
        RefreshSystemOptimizationRecommendationCounts(value);
        payload["SelfThrottleDetail"] = value.SelfThrottleDetail ?? "";
        payload["IssueCount"] = value.IssueCount;
        payload["RecommendationCount"] = value.RecommendationCount;
        payload["SafeRecommendationCount"] = value.SafeRecommendationCount;
        payload["OptionalRecommendationCount"] = value.OptionalRecommendationCount;
        payload["ExperimentalRecommendationCount"] = value.ExperimentalRecommendationCount;
        payload["RestartRecommendationCount"] = value.RestartRecommendationCount;
        payload["ApplyBlockedRecommendationCount"] = value.ApplyBlockedRecommendationCount;
        payload["Recommendations"] = BuildSystemOptimizationRecommendationPayloadList(value.Recommendations);
        payload["Signals"] = value.Signals ?? new List<string>();
        payload["Issues"] = value.Issues ?? new List<string>();
        return payload;
    }

    private static IDictionary<string, object> BuildSystemIntegrityPayload(CoreServiceSnapshot snapshot)
    {
        CoreServiceSnapshot value = snapshot ?? new CoreServiceSnapshot();
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["Available"] = value.SystemIntegrityAvailable;
        payload["Relevant"] = value.SystemIntegrityRelevant;
        payload["Mode"] = String.IsNullOrWhiteSpace(value.SystemIntegrityMode) ? SystemIntegrityGuardMode : value.SystemIntegrityMode;
        payload["State"] = value.SystemIntegrityState ?? "";
        payload["Summary"] = value.SystemIntegritySummary ?? "";
        payload["Detail"] = value.SystemIntegrityDetail ?? "";
        payload["BackupAvailable"] = value.SystemIntegrityBackupAvailable;
        payload["MmcssServiceRunning"] = value.SystemIntegrityMmcssServiceRunning;
        payload["MmcssServiceStatus"] = value.SystemIntegrityMmcssServiceStatus ?? "";
        payload["SystemResponsiveness"] = value.SystemIntegritySystemResponsiveness;
        payload["SystemResponsivenessState"] = value.SystemIntegritySystemResponsivenessState ?? "";
        payload["SystemResponsivenessDetail"] = value.SystemIntegritySystemResponsivenessDetail ?? "";
        payload["HybridCpuDetected"] = value.SystemIntegrityHybridCpuDetected;
        payload["LogicalProcessorCount"] = value.SystemIntegrityLogicalProcessorCount;
        payload["HybridSchedulerState"] = value.SystemIntegrityHybridSchedulerState ?? "";
        payload["HybridSchedulerDetail"] = value.SystemIntegrityHybridSchedulerDetail ?? "";
        payload["SelfThrottleEligible"] = value.SystemIntegritySelfThrottleEligible;
        payload["SelfThrottleState"] = value.SystemIntegritySelfThrottleState ?? "";
        payload["SelfThrottleDetail"] = value.SystemIntegritySelfThrottleDetail ?? "";
        payload["IssueCount"] = value.SystemIntegrityIssueCount;
        payload["RecommendationCount"] = value.SystemIntegrityRecommendationCount;
        payload["SafeRecommendationCount"] = value.SystemIntegritySafeRecommendationCount;
        payload["OptionalRecommendationCount"] = value.SystemIntegrityOptionalRecommendationCount;
        payload["ExperimentalRecommendationCount"] = value.SystemIntegrityExperimentalRecommendationCount;
        payload["RestartRecommendationCount"] = value.SystemIntegrityRestartRecommendationCount;
        payload["ApplyBlockedRecommendationCount"] = value.SystemIntegrityApplyBlockedRecommendationCount;
        payload["Recommendations"] = value.SystemIntegrityRecommendations ?? new List<Dictionary<string, object>>();
        payload["Signals"] = value.SystemIntegritySignals ?? new List<string>();
        payload["Issues"] = value.SystemIntegrityIssues ?? new List<string>();
        return payload;
    }

    private static void ApplySystemIntegritySnapshot(IDictionary<string, object> target, SystemIntegritySnapshot snapshot)
    {
        if (target == null || snapshot == null) { return; }
        target["SystemIntegrity"] = BuildSystemIntegrityPayload(snapshot);
        target["SystemIntegrityAvailable"] = snapshot.Available;
        target["SystemIntegrityRelevant"] = snapshot.Relevant;
        target["SystemIntegrityMode"] = snapshot.Mode ?? SystemIntegrityGuardMode;
        target["SystemIntegrityState"] = snapshot.State ?? "";
        target["SystemIntegritySummary"] = snapshot.Summary ?? "";
        target["SystemIntegrityDetail"] = snapshot.Detail ?? "";
        target["SystemIntegrityBackupAvailable"] = snapshot.BackupAvailable;
        target["SystemIntegrityMmcssServiceRunning"] = snapshot.MmcssServiceRunning;
        target["SystemIntegrityMmcssServiceStatus"] = snapshot.MmcssServiceStatus ?? "";
        target["SystemIntegritySystemResponsiveness"] = snapshot.SystemResponsiveness;
        target["SystemIntegritySystemResponsivenessState"] = snapshot.SystemResponsivenessState ?? "";
        target["SystemIntegritySystemResponsivenessDetail"] = snapshot.SystemResponsivenessDetail ?? "";
        target["SystemIntegrityHybridCpuDetected"] = snapshot.HybridCpuDetected;
        target["SystemIntegrityLogicalProcessorCount"] = snapshot.LogicalProcessorCount;
        target["SystemIntegrityHybridSchedulerState"] = snapshot.HybridSchedulerState ?? "";
        target["SystemIntegrityHybridSchedulerDetail"] = snapshot.HybridSchedulerDetail ?? "";
        target["SystemIntegritySelfThrottleEligible"] = snapshot.SelfThrottleEligible;
        target["SystemIntegritySelfThrottleState"] = snapshot.SelfThrottleState ?? "";
        RefreshSystemOptimizationRecommendationCounts(snapshot);
        target["SystemIntegritySelfThrottleDetail"] = snapshot.SelfThrottleDetail ?? "";
        target["SystemIntegrityIssueCount"] = snapshot.IssueCount;
        target["SystemIntegrityRecommendationCount"] = snapshot.RecommendationCount;
        target["SystemIntegritySafeRecommendationCount"] = snapshot.SafeRecommendationCount;
        target["SystemIntegrityOptionalRecommendationCount"] = snapshot.OptionalRecommendationCount;
        target["SystemIntegrityExperimentalRecommendationCount"] = snapshot.ExperimentalRecommendationCount;
        target["SystemIntegrityRestartRecommendationCount"] = snapshot.RestartRecommendationCount;
        target["SystemIntegrityApplyBlockedRecommendationCount"] = snapshot.ApplyBlockedRecommendationCount;
        target["SystemIntegrityRecommendations"] = BuildSystemOptimizationRecommendationPayloadList(snapshot.Recommendations);
        target["SystemIntegritySignals"] = snapshot.Signals ?? new List<string>();
        target["SystemIntegrityIssues"] = snapshot.Issues ?? new List<string>();
    }

    private static void MaybeLogSystemIntegrity(SystemIntegritySnapshot snapshot)
    {
        if (snapshot == null || !snapshot.Relevant) { return; }
        string signature = String.Join("|", new string[]
        {
            snapshot.State ?? "",
            snapshot.SystemResponsiveness.ToString(CultureInfo.InvariantCulture),
            snapshot.MmcssServiceStatus ?? "",
            snapshot.HybridCpuDetected ? "hybrid" : "",
            snapshot.SelfThrottleEligible ? "self-throttle" : "",
            snapshot.IssueCount.ToString(CultureInfo.InvariantCulture)
        });
        lock (systemIntegrityLogLock)
        {
            if (String.Equals(signature, systemIntegrityLastLogSignature, StringComparison.Ordinal)) { return; }
            systemIntegrityLastLogSignature = signature;
        }
        AppendOperationalLog("action=system-integrity-guard mode=" + SanitizeLogToken(snapshot.Mode) +
            " state=" + SanitizeLogToken(snapshot.State) +
            " mmcss=" + SanitizeLogToken(snapshot.MmcssServiceStatus) +
            " responsiveness=" + snapshot.SystemResponsiveness.ToString(CultureInfo.InvariantCulture) +
            " hybrid=" + snapshot.HybridCpuDetected.ToString().ToLowerInvariant() +
            " selfThrottle=" + snapshot.SelfThrottleEligible.ToString().ToLowerInvariant() +
            " issues=" + snapshot.IssueCount.ToString(CultureInfo.InvariantCulture));
    }

    private static void SetCurrentProcessBackgroundMode(bool enabled, string reason)
    {
        lock (processBackgroundModeLock)
        {
            if (processBackgroundModeEnabled == enabled) { return; }
            bool ok = SetPriorityClass(GetCurrentProcess(), enabled ? ProcessModeBackgroundBegin : ProcessModeBackgroundEnd);
            if (ok)
            {
                processBackgroundModeEnabled = enabled;
                AppendOperationalLog("action=low-impact-runtime state=" + (enabled ? "enabled" : "disabled") + " reason=" + SanitizeLogToken(reason));
            }
            else
            {
                AppendOperationalLog("action=low-impact-runtime status=failed state=" + (enabled ? "enabled" : "disabled") + " reason=" + SanitizeLogToken(reason) + " error=" + Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static List<string> BuildCoreServiceCapabilities()
    {
        return new List<string>
        {
            "hello",
            "getCapabilities",
            "getSnapshot",
            "subscribe",
            "getState",
            "getEvents",
            "getDiagnostics",
            "ping",
            "corePipe.v1",
            "sessionAgent.v1",
            "publishSessionContext",
            "getSessionContext",
            "watchdog",
            "scheduledTaskBridge",
            "memoryStabilityGuard.shadow",
            "commitHeadroomGuard.v1",
            "browserBurstShield.shadow",
            "systemIntegrityGuard.shadow",
            "mmcssIntegrityCheck.shadow",
            "windowsTweakSanityScanner.shadow",
            "systemOptimizationRecommendations.v1",
            "hybridCpuSchedulerGuard.shadow",
            "lowImpactRuntime.v1"
        };
    }

    private static string ToCoreIso(DateTime value)
    {
        if (value <= DateTime.MinValue.AddYears(1)) { return ""; }
        return value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
    }

    private static string GetCurrentUserSid()
    {
        try
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                return identity != null && identity.User != null ? identity.User.Value : "";
            }
        }
        catch
        {
            return "";
        }
    }

    private static string GetCurrentUserName()
    {
        try
        {
            return (Environment.UserDomainName + "\\" + Environment.UserName).Trim('\\');
        }
        catch
        {
            return "";
        }
    }

    private static bool StringEqualsAny(string value, string[] candidates)
    {
        if (String.IsNullOrWhiteSpace(value) || candidates == null) { return false; }
        string normalized = value.Trim();
        if (normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { normalized = normalized.Substring(0, normalized.Length - 4); }
        for (int i = 0; i < candidates.Length; i++)
        {
            if (String.Equals(normalized, candidates[i], StringComparison.OrdinalIgnoreCase)) { return true; }
        }
        return false;
    }

    private static bool ContainsAnyFragment(string value, string[] fragments)
    {
        if (String.IsNullOrWhiteSpace(value) || fragments == null) { return false; }
        for (int i = 0; i < fragments.Length; i++)
        {
            string fragment = fragments[i];
            if (!String.IsNullOrWhiteSpace(fragment) && value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
        }
        return false;
    }

    private static bool IsSessionAgentGameProcessName(string processName)
    {
        if (String.IsNullOrWhiteSpace(processName)) { return false; }
        string name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { name = name.Substring(0, name.Length - 4); }
        if (StringEqualsAny(name, new string[] { "bf6", "bf2042", "bfv", "bf1", "bf4", "bf3", "fc26", "fc25", "fc24", "fifa23", "fifa22", "cs2", "valorant", "valorant-win64-shipping", "r5apex", "apex", "fortniteclient-win64-shipping", "rocketleague", "rainbowsix", "rainbowsix_be", "cod", "cod22", "cod23", "cod24", "modernwarfare", "warzone", "league of legends", "dota2", "overwatch", "destiny2", "thefinals", "pubg", "tslgame", "escape from tarkov", "eldenring", "helldivers2", "gta5", "rdr2" })) { return true; }
        string lower = name.ToLowerInvariant();
        return (lower.StartsWith("bf", StringComparison.Ordinal) && lower.Length <= 7 && lower.Any(Char.IsDigit)) || lower.EndsWith("-win64-shipping", StringComparison.Ordinal);
    }

    private static bool IsSessionAgentGamePath(string path)
    {
        return ContainsAnyFragment(path, new string[] { "\\steamapps\\common\\", "\\XboxGames\\", "\\Epic Games\\", "\\Riot Games\\", "\\Battle.net\\", "\\GOG Galaxy\\Games\\", "\\EA Games\\", "\\Electronic Arts\\Games\\", "\\Electronic Arts\\Battlefield", "\\Electronic Arts\\Apex", "\\Electronic Arts\\FC", "\\Electronic Arts\\EA SPORTS FC", "\\Battlefield 6\\", "\\EA SPORTS FC 26\\" });
    }

    private static bool IsSessionAgentStreamingProcessName(string processName)
    {
        return StringEqualsAny(processName, new string[] { "obs64", "obs32", "Streamlabs Desktop", "Streamlabs", "TikTok LIVE Studio", "TikTokLiveStudio", "TikTokStudio", "PRISMLiveStudio", "XSplit.Core", "XSplitBroadcaster", "vMix64", "vMix", "TwitchStudio", "NVIDIA Broadcast", "ElgatoCameraHub" });
    }

    private static bool IsSessionAgentMediaOrCallProcessName(string processName)
    {
        return StringEqualsAny(processName, new string[] { "Discord", "Teams", "Slack", "Zoom", "Telegram", "WhatsApp", "Spotify", "vlc", "mpv", "chrome", "msedge", "firefox", "zen", "brave", "opera", "vivaldi", "msedgewebview2" });
    }

    private static bool IsSessionAgentWorkProcessName(string processName)
    {
        return StringEqualsAny(processName, new string[] { "Photoshop", "Illustrator", "AfterFX", "Adobe Premiere Pro", "Adobe Media Encoder", "Lightroom", "Resolve", "Fusion", "blender", "UnrealEditor", "Unity", "devenv", "Code", "Code - Insiders", "cursor", "windsurf", "rider64", "idea64", "pycharm64", "webstorm64", "clion64", "datagrip64", "goland64", "phpstorm64", "rustrover64", "sublime_text", "notepad++", "zed", "codex" });
    }

    private static bool IsWindowFullscreenOnMonitor(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero) { return false; }
            WindowRect rect;
            if (!GetWindowRect(hwnd, out rect)) { return false; }
            Rectangle window = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (window.Width <= 0 || window.Height <= 0) { return false; }
            Screen screen = Screen.FromHandle(hwnd);
            Rectangle bounds = screen.Bounds;
            int tolerance = 8;
            return Math.Abs(window.Left - bounds.Left) <= tolerance &&
                Math.Abs(window.Top - bounds.Top) <= tolerance &&
                Math.Abs(window.Right - bounds.Right) <= tolerance &&
                Math.Abs(window.Bottom - bounds.Bottom) <= tolerance;
        }
        catch
        {
            return false;
        }
    }

    private static int CountSessionStreamingProcesses(int sessionId)
    {
        int count = 0;
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (sessionId >= 0 && process.SessionId != sessionId) { continue; }
                if (IsSessionAgentStreamingProcessName(process.ProcessName)) { count++; }
            }
            catch
            {
            }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }
        return count;
    }

    private static string DetermineSessionAgentContext(bool foregroundIsGame, bool foregroundFullscreen, bool foregroundIsStreaming, bool mediaOrCallForeground, bool workForeground, bool streamingObserved, int idleSeconds, List<string> evidence, out int confidence)
    {
        if (evidence == null) { evidence = new List<string>(); }
        if (streamingObserved || foregroundIsStreaming)
        {
            confidence = foregroundIsStreaming ? 88 : 76;
            evidence.Add(foregroundIsStreaming ? "streaming-foreground" : "streaming-process-observed");
            if (foregroundIsGame) { evidence.Add("game-also-observed"); }
            return "Live";
        }
        if (foregroundIsGame)
        {
            confidence = foregroundFullscreen ? 90 : 78;
            evidence.Add(foregroundFullscreen ? "game-fullscreen-foreground" : "game-foreground");
            return "Game";
        }
        if (mediaOrCallForeground)
        {
            confidence = 70;
            evidence.Add("media-or-call-foreground");
            return "MediaOrCall";
        }
        if (workForeground)
        {
            confidence = 68;
            evidence.Add("workload-foreground");
            return "Work";
        }
        if (idleSeconds >= 600)
        {
            confidence = 58;
            evidence.Add("input-idle");
            return "Idle";
        }
        confidence = 45;
        evidence.Add("foreground-observed");
        return "CommonUse";
    }

    private static IDictionary<string, object> BuildSessionAgentObservation()
    {
        IDictionary<string, object> root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        List<string> evidence = new List<string>();
        int currentSessionId;
        try { currentSessionId = Process.GetCurrentProcess().SessionId; }
        catch { currentSessionId = -1; }

        int foregroundPid = 0;
        string foregroundName = "";
        string foregroundPath = "";
        string foregroundStart = "";
        bool foregroundHasWindow = false;
        bool foregroundIsGame = false;
        bool foregroundIsStreaming = false;
        bool foregroundIsProtected = false;
        string foregroundProtectionReason = "";
        bool foregroundFullscreen = false;
        string lastError = "";

        try
        {
            IntPtr hwnd = GetForegroundWindow();
            uint pidValue;
            if (hwnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(hwnd, out pidValue);
                foregroundPid = (int)pidValue;
                foregroundFullscreen = IsWindowFullscreenOnMonitor(hwnd);
                using (Process process = foregroundPid > 0 ? Process.GetProcessById(foregroundPid) : null)
                {
                    if (process != null)
                    {
                        foregroundName = process.ProcessName ?? "";
                        try { foregroundHasWindow = process.MainWindowHandle != IntPtr.Zero; } catch { foregroundHasWindow = hwnd != IntPtr.Zero; }
                        foregroundPath = TryGetProcessPath(process);
                        try { foregroundStart = process.StartTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture); } catch { }
                        foregroundIsGame = IsSessionAgentGameProcessName(foregroundName) || IsSessionAgentGamePath(foregroundPath);
                        foregroundIsStreaming = IsSessionAgentStreamingProcessName(foregroundName);
                        foregroundIsProtected = IsProtectedForegroundProcess(foregroundName);
                        if (foregroundIsProtected) { foregroundProtectionReason = "StaticGuard"; }
                        string runtimeProtectionReason;
                        if (IsSessionForegroundProtectedByRuntime(foregroundPid, foregroundName, foregroundPath, out runtimeProtectionReason))
                        {
                            foregroundIsProtected = true;
                            foregroundProtectionReason = runtimeProtectionReason;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lastError = ShortTaskError(ex.Message);
        }

        TimeSpan idle = GetSystemIdleTime();
        int idleSeconds = Math.Max(0, (int)Math.Round(idle.TotalSeconds));
        int streamingCount = CountSessionStreamingProcesses(currentSessionId);
        bool streamingObserved = streamingCount > 0;
        bool mediaOrCallForeground = IsSessionAgentMediaOrCallProcessName(foregroundName);
        bool workForeground = IsSessionAgentWorkProcessName(foregroundName);
        if (foregroundFullscreen) { evidence.Add("fullscreen-window"); }
        if (foregroundIsProtected) { evidence.Add("protected-foreground"); }
        if (!String.IsNullOrWhiteSpace(foregroundProtectionReason)) { evidence.Add("protection-" + SanitizeEvidenceToken(foregroundProtectionReason)); }

        int confidence;
        string context = DetermineSessionAgentContext(foregroundIsGame, foregroundFullscreen, foregroundIsStreaming, mediaOrCallForeground, workForeground, streamingObserved, idleSeconds, evidence, out confidence);

        IDictionary<string, object> foreground = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreground["ProcessId"] = foregroundPid;
        foreground["ProcessName"] = foregroundName;
        foreground["ProcessStartTime"] = foregroundStart;
        foreground["Path"] = foregroundPath;
        foreground["HasWindow"] = foregroundHasWindow;
        foreground["Fullscreen"] = foregroundFullscreen;
        foreground["LikelyGame"] = foregroundIsGame;
        foreground["StreamingProcess"] = foregroundIsStreaming;
        foreground["Protected"] = foregroundIsProtected;
        foreground["ProtectionReason"] = foregroundProtectionReason;

        root["ProtocolVersion"] = CoreProtocolVersion;
        root["AgentVersion"] = AppVersion;
        root["AgentName"] = "Smart Nap Session Agent";
        root["State"] = "Observing";
        root["Health"] = String.IsNullOrWhiteSpace(lastError) ? "Healthy" : "Degraded";
        root["Source"] = "SessionAgent";
        root["SessionId"] = currentSessionId;
        root["UserSid"] = GetCurrentUserSid();
        root["UserName"] = GetCurrentUserName();
        root["UpdatedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        root["IdleSeconds"] = idleSeconds;
        root["Context"] = context;
        root["Confidence"] = confidence;
        root["Evidence"] = evidence;
        root["Foreground"] = foreground;
        root["ForegroundPid"] = foregroundPid;
        root["ForegroundProcessName"] = foregroundName;
        root["ForegroundStartTime"] = foregroundStart;
        root["ForegroundPath"] = foregroundPath;
        root["ForegroundHasWindow"] = foregroundHasWindow;
        root["ForegroundIsGame"] = foregroundIsGame;
        root["ForegroundIsStreaming"] = foregroundIsStreaming;
        root["ForegroundIsProtected"] = foregroundIsProtected;
        root["ForegroundProtectionReason"] = foregroundProtectionReason;
        root["ForegroundFullscreen"] = foregroundFullscreen;
        root["StreamingObserved"] = streamingObserved;
        root["StreamingProcessCount"] = streamingCount;
        root["LastError"] = lastError;
        return root;
    }

    private static void WriteSessionAgentState(IDictionary<string, object> observation)
    {
        try
        {
            Directory.CreateDirectory(outputsPath);
            AtomicWriteJsonMap(sessionAgentStatePath, observation ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=session-agent status=state-write-failed detail=" + ShortTaskError(ex.Message));
        }
    }

    private static SessionAgentSnapshot LoadSessionAgentSnapshot()
    {
        SessionAgentSnapshot snapshot = new SessionAgentSnapshot();
        snapshot.State = "Unavailable";
        snapshot.Health = "Missing";
        snapshot.Source = "SessionAgent";
        snapshot.Evidence = new List<string>();
        snapshot.StateAgeSeconds = -1;
        try
        {
            if (!File.Exists(sessionAgentStatePath)) { return snapshot; }
            IDictionary<string, object> root = LoadJsonMapWithRecovery(sessionAgentStatePath);
            if (root == null || root.Count == 0) { return snapshot; }
            IDictionary<string, object> foreground = ReadMapObject(root, "Foreground");
            snapshot.Available = true;
            snapshot.ProtocolVersion = ReadMapInt(root, "ProtocolVersion");
            snapshot.AgentVersion = ReadMapString(root, "AgentVersion");
            snapshot.State = ReadMapString(root, "State");
            snapshot.Health = ReadMapString(root, "Health");
            snapshot.Source = ReadMapString(root, "Source");
            snapshot.SessionId = ReadMapInt(root, "SessionId");
            snapshot.UserSid = ReadMapString(root, "UserSid");
            snapshot.UserName = ReadMapString(root, "UserName");
            snapshot.UpdatedAt = ReadMapString(root, "UpdatedAt");
            snapshot.StateAgeSeconds = GetIsoAgeSeconds(snapshot.UpdatedAt);
            snapshot.IdleSeconds = ReadMapInt(root, "IdleSeconds");
            snapshot.Context = ReadMapString(root, "Context");
            snapshot.Confidence = ReadMapInt(root, "Confidence");
            snapshot.Evidence = ReadMapStringList(root, "Evidence");
            snapshot.ForegroundPid = ReadMapInt(root, "ForegroundPid");
            snapshot.ForegroundProcessName = ReadMapString(root, "ForegroundProcessName");
            snapshot.ForegroundStartTime = ReadMapString(root, "ForegroundStartTime");
            snapshot.ForegroundPath = ReadMapString(root, "ForegroundPath");
            snapshot.ForegroundHasWindow = ReadMapBool(root, "ForegroundHasWindow");
            snapshot.ForegroundIsGame = ReadMapBool(root, "ForegroundIsGame");
            snapshot.ForegroundIsStreaming = ReadMapBool(root, "ForegroundIsStreaming");
            snapshot.ForegroundIsProtected = ReadMapBool(root, "ForegroundIsProtected");
            snapshot.ForegroundFullscreen = ReadMapBool(root, "ForegroundFullscreen");
            if (snapshot.ForegroundPid <= 0) { snapshot.ForegroundPid = ReadMapInt(foreground, "ProcessId"); }
            if (String.IsNullOrWhiteSpace(snapshot.ForegroundProcessName)) { snapshot.ForegroundProcessName = ReadMapString(foreground, "ProcessName"); }
            if (String.IsNullOrWhiteSpace(snapshot.ForegroundStartTime)) { snapshot.ForegroundStartTime = ReadMapString(foreground, "ProcessStartTime"); }
            if (String.IsNullOrWhiteSpace(snapshot.ForegroundPath)) { snapshot.ForegroundPath = ReadMapString(foreground, "Path"); }
            snapshot.StreamingObserved = ReadMapBool(root, "StreamingObserved");
            snapshot.StreamingProcessCount = ReadMapInt(root, "StreamingProcessCount");
            snapshot.LastError = ReadMapString(root, "LastError");
            snapshot.CorePublishedAt = ReadMapString(root, "CorePublishedAt");
            snapshot.CorePublishStatus = ReadMapString(root, "CorePublishStatus");
            if (snapshot.ProtocolVersion <= 0) { snapshot.ProtocolVersion = CoreProtocolVersion; }
            if (String.IsNullOrWhiteSpace(snapshot.AgentVersion)) { snapshot.AgentVersion = AppVersion; }
            if (String.IsNullOrWhiteSpace(snapshot.Health)) { snapshot.Health = "Healthy"; }
            if (snapshot.StateAgeSeconds > SessionAgentStateMaxAgeSeconds && String.Equals(snapshot.Health, "Healthy", StringComparison.OrdinalIgnoreCase))
            {
                snapshot.Health = "Stale";
                snapshot.State = "Stale";
            }
        }
        catch
        {
        }
        return snapshot;
    }

    private static IDictionary<string, object> BuildSessionAgentPayload(SessionAgentSnapshot snapshot)
    {
        SessionAgentSnapshot agent = snapshot ?? LoadSessionAgentSnapshot();
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["Available"] = agent.Available;
        payload["ProtocolVersion"] = agent.ProtocolVersion <= 0 ? CoreProtocolVersion : agent.ProtocolVersion;
        payload["AgentVersion"] = String.IsNullOrWhiteSpace(agent.AgentVersion) ? AppVersion : agent.AgentVersion;
        payload["State"] = agent.State ?? "";
        payload["Health"] = agent.Health ?? "";
        payload["Source"] = agent.Source ?? "";
        payload["SessionId"] = agent.SessionId;
        payload["UserSid"] = agent.UserSid ?? "";
        payload["UserName"] = agent.UserName ?? "";
        payload["UpdatedAt"] = agent.UpdatedAt ?? "";
        payload["StateAgeSeconds"] = agent.StateAgeSeconds;
        payload["CorePublishedAt"] = agent.CorePublishedAt ?? "";
        payload["CorePublishStatus"] = agent.CorePublishStatus ?? "";
        payload["LastError"] = agent.LastError ?? "";
        payload["StatePath"] = sessionAgentStatePath;
        return payload;
    }

    private static IDictionary<string, object> BuildSessionContextPayload(SessionAgentSnapshot snapshot)
    {
        SessionAgentSnapshot agent = snapshot ?? LoadSessionAgentSnapshot();
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["Context"] = agent.Context ?? "";
        payload["Confidence"] = agent.Confidence;
        payload["Evidence"] = agent.Evidence ?? new List<string>();
        payload["IdleSeconds"] = agent.IdleSeconds;
        payload["StreamingObserved"] = agent.StreamingObserved;
        payload["StreamingProcessCount"] = agent.StreamingProcessCount;
        payload["UpdatedAt"] = agent.UpdatedAt ?? "";
        payload["StateAgeSeconds"] = agent.StateAgeSeconds;
        return payload;
    }

    private static IDictionary<string, object> BuildSessionForegroundPayload(SessionAgentSnapshot snapshot)
    {
        SessionAgentSnapshot agent = snapshot ?? LoadSessionAgentSnapshot();
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["ProcessId"] = agent.ForegroundPid;
        payload["ProcessName"] = agent.ForegroundProcessName ?? "";
        payload["ProcessStartTime"] = agent.ForegroundStartTime ?? "";
        payload["Path"] = agent.ForegroundPath ?? "";
        payload["HasWindow"] = agent.ForegroundHasWindow;
        payload["Fullscreen"] = agent.ForegroundFullscreen;
        payload["LikelyGame"] = agent.ForegroundIsGame;
        payload["StreamingProcess"] = agent.ForegroundIsStreaming;
        payload["Protected"] = agent.ForegroundIsProtected;
        return payload;
    }

    private static void MarkCorePipeListening(bool listening, string error)
    {
        lock (corePipeStateLock)
        {
            corePipeListening = listening;
            corePipeHeartbeatUtc = DateTime.UtcNow;
            corePipeLastError = error ?? "";
        }
    }

    private static void MarkCorePipeHeartbeat()
    {
        lock (corePipeStateLock)
        {
            corePipeHeartbeatUtc = DateTime.UtcNow;
        }
    }

    private static void MarkCorePipeClient(string command, string clientUser, string error)
    {
        lock (corePipeStateLock)
        {
            corePipeLastClientUtc = DateTime.UtcNow;
            corePipeLastCommand = command ?? "";
            corePipeLastClientUser = clientUser ?? "";
            corePipeLastError = error ?? "";
        }
        Interlocked.Increment(ref corePipeRequestCount);
    }

    private static IDictionary<string, object> BuildCorePipeStatePayload()
    {
        bool listening;
        DateTime heartbeat;
        DateTime clientAt;
        string command;
        string clientUser;
        string error;
        lock (corePipeStateLock)
        {
            listening = corePipeListening;
            heartbeat = corePipeHeartbeatUtc;
            clientAt = corePipeLastClientUtc;
            command = corePipeLastCommand;
            clientUser = corePipeLastClientUser;
            error = corePipeLastError;
        }

        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["Listening"] = listening;
        payload["Transport"] = "NamedPipe";
        payload["PipeName"] = CorePipeName;
        payload["SecureAcl"] = true;
        payload["HeartbeatAt"] = ToCoreIso(heartbeat);
        payload["LastClientAt"] = ToCoreIso(clientAt);
        payload["LastClientUser"] = clientUser ?? "";
        payload["LastCommand"] = command ?? "";
        payload["LastError"] = error ?? "";
        payload["RequestCount"] = Interlocked.Read(ref corePipeRequestCount);
        payload["EventSequence"] = Interlocked.Read(ref corePipeEventSequence);
        payload["MaxMessageBytes"] = CorePipeMaxMessageBytes;
        payload["SubscribeHeartbeatSeconds"] = CorePipeSubscribeHeartbeatSeconds;
        return payload;
    }

    private static IDictionary<string, object> BuildCoreServicePayload(CoreServiceSnapshot snapshot)
    {
        CoreServiceSnapshot service = snapshot ?? LoadCoreServiceSnapshot();
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["AppVersion"] = AppVersion;
        payload["ServiceName"] = CoreServiceName;
        payload["DisplayName"] = CoreServiceDisplayName;
        payload["Installed"] = service.Installed;
        payload["Running"] = service.Running;
        payload["Status"] = service.Status ?? "";
        payload["Action"] = service.Action ?? "";
        payload["Health"] = service.Health ?? "";
        payload["Summary"] = service.Summary ?? "";
        payload["Detail"] = service.Detail ?? "";
        payload["NeedsAttention"] = service.NeedsAttention;
        payload["UpdatedAt"] = service.UpdatedAt ?? "";
        payload["StateAgeSeconds"] = service.StateAgeSeconds;
        payload["MemoryStability"] = BuildMemoryStabilityPayload(service);
        payload["SystemIntegrity"] = BuildSystemIntegrityPayload(service);
        return payload;
    }

    private static IDictionary<string, object> BuildCoreEngineBridgePayload(CoreServiceSnapshot snapshot)
    {
        CoreServiceSnapshot service = snapshot ?? LoadCoreServiceSnapshot();
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["ContextProvider"] = CoreContextProviderLegacyBridge;
        payload["AutoTaskInstalled"] = service.AutoTaskInstalled;
        payload["AutoTaskKicked"] = service.AutoTaskKicked;
        payload["TelemetryFresh"] = service.TelemetryFresh;
        payload["TelemetryStale"] = service.TelemetryStale;
        payload["ScorePath"] = scorePath;
        payload["ScoreAgeSeconds"] = service.ScoreAgeSeconds;
        payload["StaleThresholdSeconds"] = service.StaleThresholdSeconds;
        payload["LoopSeconds"] = service.LoopSeconds;
        payload["LastApplyLine"] = ReadLastApplyLogLine();
        return payload;
    }

    private static IDictionary<string, object> BuildCoreCapabilitiesPayload()
    {
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["ProtocolVersion"] = CoreProtocolVersion;
        payload["MinimumSupportedProtocolVersion"] = CoreMinimumSupportedProtocolVersion;
        payload["PipeName"] = CorePipeName;
        payload["Capabilities"] = BuildCoreServiceCapabilities();
        payload["Transport"] = "NamedPipe";
        payload["SecureAcl"] = true;
        payload["MaxMessageBytes"] = CorePipeMaxMessageBytes;
        payload["ContextProvider"] = "SessionAgentV1+" + CoreContextProviderLegacyBridge;
        return payload;
    }

    private static IDictionary<string, object> BuildCoreSnapshotPayload()
    {
        CoreServiceSnapshot service = LoadCoreServiceSnapshot();
        SessionAgentSnapshot sessionAgent = LoadSessionAgentSnapshot();
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["SnapshotVersion"] = 1;
        payload["GeneratedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        payload["Protocol"] = BuildCoreCapabilitiesPayload();
        payload["Service"] = BuildCoreServicePayload(service);
        payload["Ipc"] = BuildCorePipeStatePayload();
        payload["Engine"] = BuildCoreEngineBridgePayload(service);
        payload["Session"] = BuildSessionAgentPayload(sessionAgent);
        payload["Context"] = BuildSessionContextPayload(sessionAgent);
        payload["Foreground"] = BuildSessionForegroundPayload(sessionAgent);
        payload["MemoryStability"] = BuildMemoryStabilityPayload(service);
        payload["SystemIntegrity"] = BuildSystemIntegrityPayload(service);
        payload["Events"] = BuildCoreEventsPayload(20);
        return payload;
    }

    private static IDictionary<string, object> BuildCoreEventsPayload(int maxLines)
    {
        int limit = Math.Max(1, Math.Min(100, maxLines <= 0 ? 20 : maxLines));
        List<string> lines = ReadCoreLogLines(logPath, limit);
        List<object> events = new List<object>();
        long baseSequence = Interlocked.Read(ref corePipeEventSequence);
        for (int i = 0; i < lines.Count; i++)
        {
            IDictionary<string, object> entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            entry["Sequence"] = Math.Max(0, baseSequence - lines.Count + i + 1);
            entry["Text"] = lines[i];
            events.Add(entry);
        }

        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["Cursor"] = File.Exists(logPath) ? File.GetLastWriteTimeUtc(logPath).Ticks.ToString(CultureInfo.InvariantCulture) : "0";
        payload["Count"] = events.Count;
        payload["Events"] = events;
        return payload;
    }

    private static List<string> ReadCoreLogLines(string path, int maxLines)
    {
        List<string> result = new List<string>();
        try
        {
            if (!File.Exists(path)) { return result; }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            int start = Math.Max(0, lines.Length - Math.Max(1, maxLines));
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

    private static IDictionary<string, object> BuildCoreDiagnosticsPayload()
    {
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["Service"] = BuildCoreServicePayload(null);
        payload["Ipc"] = BuildCorePipeStatePayload();
        payload["Capabilities"] = BuildCoreCapabilitiesPayload();
        payload["Session"] = BuildSessionAgentPayload(null);
        payload["Context"] = BuildSessionContextPayload(null);
        payload["Foreground"] = BuildSessionForegroundPayload(null);
        IDictionary<string, object> tasks = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        tasks["Auto"] = BuildTaskStatusLine(AutoTaskName);
        tasks["Tray"] = BuildTaskStatusLine(TrayTaskName);
        tasks["Dashboard"] = BuildTaskStatusLine(DashboardTaskName);
        tasks["SessionAgent"] = BuildTaskStatusLine(SessionAgentTaskName);
        payload["Tasks"] = tasks;
        IDictionary<string, object> paths = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        paths["AppRoot"] = appRoot;
        paths["Outputs"] = outputsPath;
        paths["Log"] = logPath;
        paths["Score"] = scorePath;
        paths["CoreState"] = coreServiceStatePath;
        paths["SessionAgentState"] = sessionAgentStatePath;
        payload["Paths"] = paths;
        return payload;
    }

    private static PipeSecurity CreateCorePipeSecurity()
    {
        PipeSecurity security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        AddCorePipeAccessRule(security, WellKnownSidType.LocalSystemSid, PipeAccessRights.FullControl);
        AddCorePipeAccessRule(security, WellKnownSidType.BuiltinAdministratorsSid, PipeAccessRights.FullControl);
        AddCorePipeAccessRule(security, WellKnownSidType.InteractiveSid, PipeAccessRights.ReadWrite);
        return security;
    }

    private static void AddCorePipeAccessRule(PipeSecurity security, WellKnownSidType sidType, PipeAccessRights rights)
    {
        SecurityIdentifier sid = new SecurityIdentifier(sidType, null);
        security.AddAccessRule(new PipeAccessRule(sid, rights, AccessControlType.Allow));
    }

    private static NamedPipeServerStream CreateCorePipeServerStream()
    {
        return NamedPipeServerStreamAcl.Create(
            CorePipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous,
            CorePipeMaxMessageBytes,
            CorePipeMaxMessageBytes,
            CreateCorePipeSecurity());
    }

    private static string TryGetCorePipeClientUser(NamedPipeServerStream pipe)
    {
        try { return pipe == null ? "" : pipe.GetImpersonationUserName(); }
        catch { return ""; }
    }

    private static string ReadCorePipeMessage(PipeStream pipe)
    {
        if (pipe == null || !pipe.IsConnected) { return ""; }
        byte[] buffer = new byte[4096];
        using (MemoryStream output = new MemoryStream())
        {
            do
            {
                int read = pipe.Read(buffer, 0, buffer.Length);
                if (read <= 0) { break; }
                if (output.Length + read > CorePipeMaxMessageBytes)
                {
                    throw new InvalidOperationException("Core pipe message exceeded " + CorePipeMaxMessageBytes.ToString(CultureInfo.InvariantCulture) + " bytes.");
                }
                output.Write(buffer, 0, read);
            }
            while (!pipe.IsMessageComplete);

            return Encoding.UTF8.GetString(output.ToArray()).Trim();
        }
    }

    private static void WriteCorePipeMessage(PipeStream pipe, object message)
    {
        if (pipe == null || !pipe.IsConnected) { return; }
        string json = JsonCompat.SerializeObject(message);
        byte[] payload = Encoding.UTF8.GetBytes(json);
        if (payload.Length > CorePipeMaxMessageBytes)
        {
            throw new InvalidOperationException("Core pipe response exceeded " + CorePipeMaxMessageBytes.ToString(CultureInfo.InvariantCulture) + " bytes.");
        }
        pipe.Write(payload, 0, payload.Length);
        pipe.Flush();
    }

    private static IDictionary<string, object> AcceptSessionAgentObservation(IDictionary<string, object> request, IDictionary<string, object> observation, string clientUser)
    {
        IDictionary<string, object> accepted = observation == null
            ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(observation, StringComparer.OrdinalIgnoreCase);

        int requestSessionId = ReadMapInt(request, "sessionId");
        if (ReadMapInt(accepted, "SessionId") <= 0 && requestSessionId > 0) { accepted["SessionId"] = requestSessionId; }
        if (String.IsNullOrWhiteSpace(ReadMapString(accepted, "AgentVersion"))) { accepted["AgentVersion"] = ReadMapString(request, "clientVersion"); }
        if (String.IsNullOrWhiteSpace(ReadMapString(accepted, "UserSid"))) { accepted["UserSid"] = ReadMapString(request, "userSid"); }
        accepted["ProtocolVersion"] = CoreProtocolVersion;
        accepted["Source"] = "SessionAgent";
        accepted["CorePublishedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        accepted["CorePublishStatus"] = "accepted";
        accepted["PipeClientUser"] = clientUser ?? "";

        WriteSessionAgentState(accepted);
        SessionAgentSnapshot snapshot = LoadSessionAgentSnapshot();
        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        payload["accepted"] = true;
        payload["session"] = BuildSessionAgentPayload(snapshot);
        payload["context"] = BuildSessionContextPayload(snapshot);
        payload["foreground"] = BuildSessionForegroundPayload(snapshot);
        return payload;
    }

    private static string NormalizeCorePipeCommand(string command)
    {
        string value = String.IsNullOrWhiteSpace(command) ? "hello" : command.Trim();
        string lower = value.Replace("-", "").Replace("_", "").ToLowerInvariant();
        if (lower == "capabilities" || lower == "getcapabilities") { return "getCapabilities"; }
        if (lower == "snapshot" || lower == "getsnapshot") { return "getSnapshot"; }
        if (lower == "state" || lower == "getstate") { return "getState"; }
        if (lower == "events" || lower == "getevents") { return "getEvents"; }
        if (lower == "diagnostics" || lower == "getdiagnostics" || lower == "doctor") { return "getDiagnostics"; }
        if (lower == "sessioncontext" || lower == "getsessioncontext") { return "getSessionContext"; }
        if (lower == "publishsessioncontext" || lower == "sessionsnapshot" || lower == "publishsessionagent") { return "publishSessionContext"; }
        if (lower == "subscribe") { return "subscribe"; }
        if (lower == "ping") { return "ping"; }
        if (lower == "hello") { return "hello"; }
        return value;
    }

    private static IDictionary<string, object> BuildCorePipeResponse(IDictionary<string, object> request, string clientUser)
    {
        IDictionary<string, object> payloadMap = ReadMapObject(request, "payload");
        string command = NormalizeCorePipeCommand(ReadMapString(request, "command"));
        string requestMessageId = ReadMapString(request, "messageId");
        string correlationId = ReadMapString(request, "correlationId");
        if (String.IsNullOrWhiteSpace(correlationId)) { correlationId = requestMessageId; }
        if (String.IsNullOrWhiteSpace(correlationId)) { correlationId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture); }

        int protocolVersion = ReadMapInt(request, "protocolVersion");
        bool protocolOk = protocolVersion >= CoreMinimumSupportedProtocolVersion && protocolVersion <= CoreProtocolVersion;
        IDictionary<string, object> response = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        response["protocolVersion"] = CoreProtocolVersion;
        response["messageId"] = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        response["correlationId"] = correlationId;
        response["command"] = command;
        response["serviceVersion"] = AppVersion;
        response["serviceTime"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        response["sequence"] = Interlocked.Increment(ref corePipeEventSequence);

        if (!protocolOk)
        {
            response["accepted"] = false;
            response["status"] = "rejected";
            response["errorCode"] = "protocol_unsupported";
            response["errorMessage"] = "Unsupported protocol version.";
            response["payload"] = BuildCoreCapabilitiesPayload();
            return response;
        }

        IDictionary<string, object> payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        response["accepted"] = true;
        response["status"] = "ok";
        response["errorCode"] = "";
        response["errorMessage"] = "";

        if (String.Equals(command, "hello", StringComparison.OrdinalIgnoreCase))
        {
            payload["service"] = BuildCoreServicePayload(null);
            payload["capabilities"] = BuildCoreCapabilitiesPayload();
            payload["ipc"] = BuildCorePipeStatePayload();
            IDictionary<string, object> client = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            client["clientType"] = ReadMapString(request, "clientType");
            client["clientVersion"] = ReadMapString(request, "clientVersion");
            client["sessionId"] = ReadMapInt(request, "sessionId");
            client["user"] = clientUser ?? "";
            payload["client"] = client;
        }
        else if (String.Equals(command, "getCapabilities", StringComparison.OrdinalIgnoreCase))
        {
            payload = BuildCoreCapabilitiesPayload();
        }
        else if (String.Equals(command, "getSnapshot", StringComparison.OrdinalIgnoreCase))
        {
            payload = BuildCoreSnapshotPayload();
        }
        else if (String.Equals(command, "getState", StringComparison.OrdinalIgnoreCase))
        {
            payload["service"] = BuildCoreServicePayload(null);
            payload["engine"] = BuildCoreEngineBridgePayload(null);
            payload["ipc"] = BuildCorePipeStatePayload();
        }
        else if (String.Equals(command, "getEvents", StringComparison.OrdinalIgnoreCase))
        {
            int maxEvents = ReadMapInt(request, "maxEvents");
            if (maxEvents <= 0) { maxEvents = ReadMapInt(payloadMap, "maxEvents"); }
            payload = BuildCoreEventsPayload(maxEvents <= 0 ? 50 : maxEvents);
        }
        else if (String.Equals(command, "getDiagnostics", StringComparison.OrdinalIgnoreCase))
        {
            payload = BuildCoreDiagnosticsPayload();
        }
        else if (String.Equals(command, "getSessionContext", StringComparison.OrdinalIgnoreCase))
        {
            SessionAgentSnapshot agent = LoadSessionAgentSnapshot();
            payload["session"] = BuildSessionAgentPayload(agent);
            payload["context"] = BuildSessionContextPayload(agent);
            payload["foreground"] = BuildSessionForegroundPayload(agent);
        }
        else if (String.Equals(command, "publishSessionContext", StringComparison.OrdinalIgnoreCase))
        {
            string clientType = ReadMapString(request, "clientType");
            if (!String.Equals(clientType, SessionAgentClientType, StringComparison.OrdinalIgnoreCase))
            {
                response["accepted"] = false;
                response["status"] = "rejected";
                response["errorCode"] = "client_type_not_allowed";
                response["errorMessage"] = "Only the Smart Nap Session Agent can publish session context.";
            }
            else
            {
                payload = AcceptSessionAgentObservation(request, payloadMap, clientUser);
            }
        }
        else if (String.Equals(command, "subscribe", StringComparison.OrdinalIgnoreCase))
        {
            payload = BuildCoreSnapshotPayload();
            payload["SubscriptionMode"] = "heartbeat-stream";
            payload["HeartbeatSeconds"] = CorePipeSubscribeHeartbeatSeconds;
        }
        else if (String.Equals(command, "ping", StringComparison.OrdinalIgnoreCase))
        {
            payload["pong"] = true;
            payload["ipc"] = BuildCorePipeStatePayload();
        }
        else
        {
            response["accepted"] = false;
            response["status"] = "rejected";
            response["errorCode"] = "command_unsupported";
            response["errorMessage"] = "Unsupported Core Service v1 command.";
            payload["capabilities"] = BuildCoreServiceCapabilities();
        }

        response["payload"] = payload;
        return response;
    }

    private static bool IsAcceptedCorePipeResponse(IDictionary<string, object> response)
    {
        return response != null && ReadMapBool(response, "accepted");
    }

    private static void RunCorePipeSubscription(PipeStream pipe, ManualResetEvent stopSignal, string correlationId)
    {
        while (pipe != null && pipe.IsConnected && !stopSignal.WaitOne(TimeSpan.FromSeconds(CorePipeSubscribeHeartbeatSeconds)))
        {
            MarkCorePipeHeartbeat();
            IDictionary<string, object> heartbeat = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            heartbeat["protocolVersion"] = CoreProtocolVersion;
            heartbeat["messageId"] = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            heartbeat["correlationId"] = correlationId ?? "";
            heartbeat["eventType"] = "core.heartbeat";
            heartbeat["sequence"] = Interlocked.Increment(ref corePipeEventSequence);
            heartbeat["serviceVersion"] = AppVersion;
            heartbeat["timestamp"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            heartbeat["payload"] = BuildCorePipeStatePayload();
            WriteCorePipeMessage(pipe, heartbeat);
        }
    }

    private static void HandleCorePipeConnection(NamedPipeServerStream pipe, ManualResetEvent stopSignal)
    {
        string command = "unknown";
        string clientUser = TryGetCorePipeClientUser(pipe);
        try
        {
            string text = ReadCorePipeMessage(pipe);
            if (String.IsNullOrWhiteSpace(text)) { return; }
            IDictionary<string, object> request = JsonCompat.DeserializeObject(text);
            if (request == null) { throw new InvalidDataException("Invalid JSON envelope."); }
            command = NormalizeCorePipeCommand(ReadMapString(request, "command"));
            MarkCorePipeClient(command, clientUser, "");
            IDictionary<string, object> response = BuildCorePipeResponse(request, clientUser);
            WriteCorePipeMessage(pipe, response);
            if (IsAcceptedCorePipeResponse(response) && String.Equals(command, "subscribe", StringComparison.OrdinalIgnoreCase))
            {
                RunCorePipeSubscription(pipe, stopSignal, ReadMapString(response, "correlationId"));
            }
        }
        catch (Exception ex)
        {
            MarkCorePipeClient(command, clientUser, ex.Message);
            try
            {
                IDictionary<string, object> response = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                response["protocolVersion"] = CoreProtocolVersion;
                response["messageId"] = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                response["correlationId"] = "";
                response["accepted"] = false;
                response["status"] = "failed";
                response["errorCode"] = "ipc_error";
                response["errorMessage"] = ShortTaskError(ex.Message);
                response["serviceVersion"] = AppVersion;
                response["serviceTime"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                WriteCorePipeMessage(pipe, response);
            }
            catch
            {
            }
        }
    }

    private static void RunCorePipeServerLoop(ManualResetEvent stopSignal)
    {
        MarkCorePipeListening(true, "");
        AppendOperationalLog("action=core-ipc status=listening pipe=" + CorePipeName);
        try
        {
            while (!stopSignal.WaitOne(0))
            {
                MarkCorePipeHeartbeat();
                NamedPipeServerStream pipe = null;
                try
                {
                    pipe = CreateCorePipeServerStream();
                    System.Threading.Tasks.Task wait = pipe.WaitForConnectionAsync();
                    while (!wait.Wait(CorePipeConnectPollMilliseconds))
                    {
                        if (stopSignal.WaitOne(0)) { return; }
                        MarkCorePipeHeartbeat();
                    }
                    if (wait.IsFaulted && wait.Exception != null) { throw wait.Exception; }
                    if (!pipe.IsConnected) { pipe.Dispose(); pipe = null; continue; }
                    if (Volatile.Read(ref corePipeActiveConnections) >= CorePipeMaxConcurrentConnections)
                    {
                        IDictionary<string, object> busy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        busy["accepted"] = false;
                        busy["protocolVersion"] = CoreProtocolVersion;
                        busy["errorCode"] = "ipc_busy";
                        busy["errorMessage"] = "Core IPC is busy. Try again.";
                        try { WriteCorePipeMessage(pipe, busy); } catch { }
                        pipe.Dispose();
                        pipe = null;
                        continue;
                    }

                    NamedPipeServerStream connectedPipe = pipe;
                    pipe = null;
                    Interlocked.Increment(ref corePipeActiveConnections);
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            using (connectedPipe)
                            {
                                HandleCorePipeConnection(connectedPipe, stopSignal);
                            }
                        }
                        catch (Exception workerEx)
                        {
                            MarkCorePipeListening(true, workerEx.Message);
                            AppendOperationalLog("action=core-ipc-connection status=failed detail=" + ShortTaskError(workerEx.Message));
                        }
                        finally
                        {
                            Interlocked.Decrement(ref corePipeActiveConnections);
                        }
                    });
                }
                catch (Exception ex)
                {
                    MarkCorePipeListening(true, ex.Message);
                    AppendOperationalLog("action=core-ipc status=failed detail=" + ShortTaskError(ex.Message));
                    stopSignal.WaitOne(TimeSpan.FromSeconds(2));
                }
                finally
                {
                    if (pipe != null) { try { pipe.Dispose(); } catch { } }
                }
            }
        }
        finally
        {
            MarkCorePipeListening(false, "");
            AppendOperationalLog("action=core-ipc status=stopped pipe=" + CorePipeName);
        }
    }

    private static RunResult WriteCorePipeRequestToConsole(string[] args)
    {
        string requestText = GetArgValue(args, "--core-pipe-request");
        if (String.IsNullOrWhiteSpace(requestText) || !requestText.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            string command = String.IsNullOrWhiteSpace(requestText) ? "hello" : NormalizeCorePipeCommand(requestText);
            IDictionary<string, object> request = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            request["protocolVersion"] = CoreProtocolVersion;
            request["messageId"] = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            request["correlationId"] = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            request["clientType"] = "diagnostic";
            request["clientVersion"] = AppVersion;
            request["sessionId"] = Process.GetCurrentProcess().SessionId;
            request["command"] = command;
            request["createdAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            requestText = JsonCompat.SerializeObject(request);
        }

        try
        {
            using (NamedPipeClientStream pipe = new NamedPipeClientStream(".", CorePipeName, PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.Impersonation))
            {
                pipe.Connect(5000);
                pipe.ReadMode = PipeTransmissionMode.Message;
                byte[] payload = Encoding.UTF8.GetBytes(requestText);
                if (payload.Length > CorePipeMaxMessageBytes) { return new RunResult(1, "Request is too large."); }
                pipe.Write(payload, 0, payload.Length);
                pipe.Flush();
                string response = ReadCorePipeMessage(pipe);
                Console.WriteLine(response);
                return new RunResult(0, response);
            }
        }
        catch (Exception ex)
        {
            string message = "Core pipe request failed: " + ShortTaskError(ex.Message);
            Console.WriteLine(message);
            return new RunResult(1, message);
        }
    }

    private static RunResult PublishSessionAgentObservationToCore(IDictionary<string, object> observation)
    {
        if (observation == null) { return new RunResult(1, "No session observation to publish."); }

        IDictionary<string, object> request = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        request["protocolVersion"] = CoreProtocolVersion;
        request["messageId"] = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        request["correlationId"] = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        request["clientType"] = SessionAgentClientType;
        request["clientVersion"] = AppVersion;
        request["sessionId"] = ReadMapInt(observation, "SessionId");
        request["userSid"] = ReadMapString(observation, "UserSid");
        request["command"] = "publishSessionContext";
        request["payload"] = observation;
        request["createdAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        try
        {
            using (NamedPipeClientStream pipe = new NamedPipeClientStream(".", CorePipeName, PipeDirection.InOut, PipeOptions.None, TokenImpersonationLevel.Impersonation))
            {
                pipe.Connect(1500);
                pipe.ReadMode = PipeTransmissionMode.Message;
                string requestText = JsonCompat.SerializeObject(request);
                byte[] payload = Encoding.UTF8.GetBytes(requestText);
                if (payload.Length > CorePipeMaxMessageBytes) { return new RunResult(1, "Session observation is too large."); }
                pipe.Write(payload, 0, payload.Length);
                pipe.Flush();
                string responseText = ReadCorePipeMessage(pipe);
                IDictionary<string, object> response = JsonCompat.DeserializeObject(responseText);
                bool accepted = response != null && ReadMapBool(response, "accepted");
                return accepted ? new RunResult(0, responseText) : new RunResult(1, String.IsNullOrWhiteSpace(responseText) ? "Session observation was rejected." : responseText);
            }
        }
        catch (Exception ex)
        {
            return new RunResult(2, "Core pipe unavailable for Session Agent: " + ShortTaskError(ex.Message));
        }
    }

    private static void MarkSessionAgentPublishStatus(IDictionary<string, object> observation, RunResult publish)
    {
        if (observation == null) { return; }
        bool ok = publish != null && publish.ExitCode == 0;
        observation["CorePublishStatus"] = ok ? "published" : "local-only";
        observation["CorePublishedAt"] = ok ? DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) : "";
        observation["CorePublishDetail"] = publish == null ? "" : ShortTaskError(publish.Output);
    }

    private static RunResult WriteSessionAgentOnceToConsole(string[] args)
    {
        IDictionary<string, object> observation = BuildSessionAgentObservation();
        RunResult publish = PublishSessionAgentObservationToCore(observation);
        MarkSessionAgentPublishStatus(observation, publish);
        WriteSessionAgentState(observation);
        Console.WriteLine(JsonCompat.SerializeObject(observation));
        return new RunResult(0, JsonCompat.SerializeObject(observation));
    }

    private static RunResult RunSessionAgentHost(string[] args)
    {
        ManualResetEvent stopSignal = new ManualResetEvent(false);
        ConsoleCancelEventHandler cancelHandler = delegate(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            stopSignal.Set();
        };

        try { Console.CancelKeyPress += cancelHandler; } catch { }
        string lastSignature = "";
        try
        {
            AppendOperationalLog("action=session-agent event=started session=" + Process.GetCurrentProcess().SessionId.ToString(CultureInfo.InvariantCulture));
            while (!stopSignal.WaitOne(0))
            {
                IDictionary<string, object> observation = BuildSessionAgentObservation();
                RunResult publish = PublishSessionAgentObservationToCore(observation);
                MarkSessionAgentPublishStatus(observation, publish);
                WriteSessionAgentState(observation);

                string signature = ReadMapString(observation, "Context") + "|" +
                    ReadMapInt(observation, "ForegroundPid").ToString(CultureInfo.InvariantCulture) + "|" +
                    ReadMapBool(observation, "ForegroundFullscreen").ToString(CultureInfo.InvariantCulture) + "|" +
                    ReadMapBool(observation, "StreamingObserved").ToString(CultureInfo.InvariantCulture);
                if (!String.Equals(signature, lastSignature, StringComparison.OrdinalIgnoreCase))
                {
                    lastSignature = signature;
                    AppendOperationalLog("action=session-agent context=" + SanitizeLogToken(ReadMapString(observation, "Context")) +
                        " confidence=" + ReadMapInt(observation, "Confidence").ToString(CultureInfo.InvariantCulture) +
                        " foreground=" + SanitizeLogToken(ReadMapString(observation, "ForegroundProcessName")) +
                        " pid=" + ReadMapInt(observation, "ForegroundPid").ToString(CultureInfo.InvariantCulture) +
                        " fullscreen=" + ReadMapBool(observation, "ForegroundFullscreen").ToString().ToLowerInvariant() +
                        " core=" + SanitizeLogToken(ReadMapString(observation, "CorePublishStatus")));
                }

                stopSignal.WaitOne(SessionAgentLoopMilliseconds);
            }
            AppendOperationalLog("action=session-agent event=stopped");
            return new RunResult(0, "Session Agent stopped.");
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=session-agent status=failed detail=" + ShortTaskError(ex.Message));
            WriteCrash(ex);
            return new RunResult(1, "Session Agent failed: " + ex.Message);
        }
        finally
        {
            try { Console.CancelKeyPress -= cancelHandler; } catch { }
            try { stopSignal.Dispose(); } catch { }
        }
    }

    private static CoreServiceSnapshot LoadCoreServiceSnapshot()
    {
        CoreServiceSnapshot snapshot = new CoreServiceSnapshot();
        snapshot.Status = "unknown";
        snapshot.Action = "Unknown";
        snapshot.Health = "Unknown";
        snapshot.Summary = "";
        snapshot.Detail = "";
        snapshot.ScoreAgeSeconds = GetFileAgeSeconds(scorePath);
        snapshot.StaleThresholdSeconds = CoreServiceStalePassSeconds;
        snapshot.LoopSeconds = CoreServiceLoopSeconds;
        snapshot.ProtocolVersion = CoreProtocolVersion;
        snapshot.MinimumSupportedProtocolVersion = CoreMinimumSupportedProtocolVersion;
        snapshot.PipeName = CorePipeName;
        snapshot.ContextProvider = "SessionAgentV1+" + CoreContextProviderLegacyBridge;
        snapshot.StateAgeSeconds = -1;

        try
        {
            if (!File.Exists(coreServiceStatePath)) { return snapshot; }
            string json = File.ReadAllText(coreServiceStatePath, Encoding.UTF8);
            if (String.IsNullOrWhiteSpace(json)) { return snapshot; }
            IDictionary<string, object> root = JsonCompat.DeserializeObject(json);
            if (root == null) { return snapshot; }

            snapshot.Available = true;
            snapshot.Installed = ReadMapBool(root, "Installed");
            snapshot.Running = ReadMapBool(root, "Running");
            snapshot.ProtocolVersion = ReadMapInt(root, "ProtocolVersion");
            snapshot.MinimumSupportedProtocolVersion = ReadMapInt(root, "MinimumSupportedProtocolVersion");
            snapshot.PipeName = ReadMapString(root, "PipeName");
            snapshot.ContextProvider = ReadMapString(root, "ContextProvider");
            snapshot.Status = ReadMapString(root, "Status");
            snapshot.Action = ReadMapString(root, "Action");
            snapshot.Health = ReadMapString(root, "Health");
            snapshot.Summary = ReadMapString(root, "Summary");
            snapshot.Detail = ReadMapString(root, "Detail");
            snapshot.AutoTaskInstalled = ReadMapBool(root, "AutoTaskInstalled");
            snapshot.AutoTaskKicked = ReadMapBool(root, "AutoTaskKicked");
            snapshot.TelemetryFresh = ReadMapBool(root, "TelemetryFresh");
            snapshot.TelemetryStale = ReadMapBool(root, "TelemetryStale");
            snapshot.ScoreAgeSeconds = ReadMapInt(root, "ScoreAgeSeconds");
            snapshot.StaleThresholdSeconds = ReadMapInt(root, "StaleThresholdSeconds");
            snapshot.LoopSeconds = ReadMapInt(root, "LoopSeconds");
            snapshot.ExitCode = ReadMapInt(root, "ExitCode");
            snapshot.UpdatedAt = ReadMapString(root, "UpdatedAt");
            snapshot.IpcListening = ReadMapBool(root, "IpcListening");
            snapshot.IpcSecureAcl = ReadMapBool(root, "IpcSecureAcl");
            snapshot.IpcHeartbeatAt = ReadMapString(root, "IpcHeartbeatAt");
            snapshot.IpcLastClientAt = ReadMapString(root, "IpcLastClientAt");
            snapshot.IpcLastCommand = ReadMapString(root, "IpcLastCommand");
            snapshot.IpcLastError = ReadMapString(root, "IpcLastError");
            IDictionary<string, object> memoryStability = ReadMapObject(root, "MemoryStability");
            snapshot.MemoryStabilityAvailable = ReadMapBool(memoryStability, "Available") || ReadMapBool(root, "MemoryStabilityAvailable");
            snapshot.MemoryStabilityRelevant = ReadMapBool(memoryStability, "Relevant") || ReadMapBool(root, "MemoryStabilityRelevant");
            snapshot.MemoryStabilityMode = ReadMapString(memoryStability, "Mode");
            if (String.IsNullOrWhiteSpace(snapshot.MemoryStabilityMode)) { snapshot.MemoryStabilityMode = ReadMapString(root, "MemoryStabilityMode"); }
            if (String.IsNullOrWhiteSpace(snapshot.MemoryStabilityMode)) { snapshot.MemoryStabilityMode = MemoryStabilityGuardMode; }
            snapshot.MemoryStabilityState = ReadMapString(memoryStability, "State");
            if (String.IsNullOrWhiteSpace(snapshot.MemoryStabilityState)) { snapshot.MemoryStabilityState = ReadMapString(root, "MemoryStabilityState"); }
            snapshot.MemoryStabilitySummary = ReadMapString(memoryStability, "Summary");
            if (String.IsNullOrWhiteSpace(snapshot.MemoryStabilitySummary)) { snapshot.MemoryStabilitySummary = ReadMapString(root, "MemoryStabilitySummary"); }
            snapshot.MemoryStabilityDetail = ReadMapString(memoryStability, "Detail");
            if (String.IsNullOrWhiteSpace(snapshot.MemoryStabilityDetail)) { snapshot.MemoryStabilityDetail = ReadMapString(root, "MemoryStabilityDetail"); }
            snapshot.MemoryStabilityMemoryLoad = ReadMapInt(memoryStability, "MemoryLoad");
            if (snapshot.MemoryStabilityMemoryLoad <= 0) { snapshot.MemoryStabilityMemoryLoad = ReadMapInt(root, "MemoryStabilityMemoryLoad"); }
            snapshot.MemoryStabilityAvailablePhysicalMB = ReadMapDouble(memoryStability, "AvailablePhysicalMB");
            if (snapshot.MemoryStabilityAvailablePhysicalMB <= 0) { snapshot.MemoryStabilityAvailablePhysicalMB = ReadMapDouble(root, "MemoryStabilityAvailablePhysicalMB"); }
            snapshot.MemoryStabilityTotalPhysicalMB = ReadMapDouble(memoryStability, "TotalPhysicalMB");
            if (snapshot.MemoryStabilityTotalPhysicalMB <= 0) { snapshot.MemoryStabilityTotalPhysicalMB = ReadMapDouble(root, "MemoryStabilityTotalPhysicalMB"); }
            snapshot.MemoryStabilityCommitUsedMB = ReadMapDouble(memoryStability, "CommitUsedMB");
            if (snapshot.MemoryStabilityCommitUsedMB <= 0) { snapshot.MemoryStabilityCommitUsedMB = ReadMapDouble(root, "MemoryStabilityCommitUsedMB"); }
            snapshot.MemoryStabilityCommitLimitMB = ReadMapDouble(memoryStability, "CommitLimitMB");
            if (snapshot.MemoryStabilityCommitLimitMB <= 0) { snapshot.MemoryStabilityCommitLimitMB = ReadMapDouble(root, "MemoryStabilityCommitLimitMB"); }
            snapshot.MemoryStabilityCommitHeadroomMB = ReadMapDouble(memoryStability, "CommitHeadroomMB");
            if (snapshot.MemoryStabilityCommitHeadroomMB <= 0) { snapshot.MemoryStabilityCommitHeadroomMB = ReadMapDouble(root, "MemoryStabilityCommitHeadroomMB"); }
            snapshot.MemoryStabilityCommitHeadroomPercent = ReadMapInt(memoryStability, "CommitHeadroomPercent");
            if (snapshot.MemoryStabilityCommitHeadroomPercent <= 0) { snapshot.MemoryStabilityCommitHeadroomPercent = ReadMapInt(root, "MemoryStabilityCommitHeadroomPercent"); }
            snapshot.MemoryStabilityPagefileStatus = ReadMapString(memoryStability, "PagefileStatus");
            if (String.IsNullOrWhiteSpace(snapshot.MemoryStabilityPagefileStatus)) { snapshot.MemoryStabilityPagefileStatus = ReadMapString(root, "MemoryStabilityPagefileStatus"); }
            snapshot.MemoryStabilityPagefileLimited = ReadMapBool(memoryStability, "PagefileLimited") || ReadMapBool(root, "MemoryStabilityPagefileLimited");
            snapshot.MemoryStabilityLowMemorySignal = ReadMapBool(memoryStability, "LowMemorySignal") || ReadMapBool(root, "MemoryStabilityLowMemorySignal");
            snapshot.MemoryStabilityBrowserBurstRecommended = ReadMapBool(memoryStability, "BrowserBurstRecommended") || ReadMapBool(root, "MemoryStabilityBrowserBurstRecommended");
            snapshot.MemoryStabilityTopProcess = ReadMapString(memoryStability, "TopProcess");
            if (String.IsNullOrWhiteSpace(snapshot.MemoryStabilityTopProcess)) { snapshot.MemoryStabilityTopProcess = ReadMapString(root, "MemoryStabilityTopProcess"); }
            snapshot.MemoryStabilityTopProcessPid = ReadMapInt(memoryStability, "TopProcessPid");
            if (snapshot.MemoryStabilityTopProcessPid <= 0) { snapshot.MemoryStabilityTopProcessPid = ReadMapInt(root, "MemoryStabilityTopProcessPid"); }
            snapshot.MemoryStabilityTopProcessPrivateMB = ReadMapDouble(memoryStability, "TopProcessPrivateMB");
            if (snapshot.MemoryStabilityTopProcessPrivateMB <= 0) { snapshot.MemoryStabilityTopProcessPrivateMB = ReadMapDouble(root, "MemoryStabilityTopProcessPrivateMB"); }
            snapshot.MemoryStabilityTopProcessWorkingSetMB = ReadMapDouble(memoryStability, "TopProcessWorkingSetMB");
            if (snapshot.MemoryStabilityTopProcessWorkingSetMB <= 0) { snapshot.MemoryStabilityTopProcessWorkingSetMB = ReadMapDouble(root, "MemoryStabilityTopProcessWorkingSetMB"); }
            snapshot.MemoryStabilityBrowserProcessCount = ReadMapInt(memoryStability, "BrowserProcessCount");
            if (snapshot.MemoryStabilityBrowserProcessCount <= 0) { snapshot.MemoryStabilityBrowserProcessCount = ReadMapInt(root, "MemoryStabilityBrowserProcessCount"); }
            snapshot.MemoryStabilityBrowserPrivateMB = ReadMapDouble(memoryStability, "BrowserPrivateMB");
            if (snapshot.MemoryStabilityBrowserPrivateMB <= 0) { snapshot.MemoryStabilityBrowserPrivateMB = ReadMapDouble(root, "MemoryStabilityBrowserPrivateMB"); }
            snapshot.MemoryStabilityBrowserWorkingSetMB = ReadMapDouble(memoryStability, "BrowserWorkingSetMB");
            if (snapshot.MemoryStabilityBrowserWorkingSetMB <= 0) { snapshot.MemoryStabilityBrowserWorkingSetMB = ReadMapDouble(root, "MemoryStabilityBrowserWorkingSetMB"); }
            snapshot.MemoryStabilityBrowserBurstState = ReadMapString(memoryStability, "BrowserBurstState");
            if (String.IsNullOrWhiteSpace(snapshot.MemoryStabilityBrowserBurstState)) { snapshot.MemoryStabilityBrowserBurstState = ReadMapString(root, "MemoryStabilityBrowserBurstState"); }
            snapshot.MemoryStabilityHeavyRecentProcessCount = ReadMapInt(memoryStability, "HeavyRecentProcessCount");
            if (snapshot.MemoryStabilityHeavyRecentProcessCount <= 0) { snapshot.MemoryStabilityHeavyRecentProcessCount = ReadMapInt(root, "MemoryStabilityHeavyRecentProcessCount"); }
            snapshot.MemoryStabilitySignals = ReadMapStringList(memoryStability, "Signals");
            if (snapshot.MemoryStabilitySignals.Count <= 0) { snapshot.MemoryStabilitySignals = ReadMapStringList(root, "MemoryStabilitySignals"); }
            IDictionary<string, object> systemIntegrity = ReadMapObject(root, "SystemIntegrity");
            snapshot.SystemIntegrityAvailable = ReadMapBool(systemIntegrity, "Available") || ReadMapBool(root, "SystemIntegrityAvailable");
            snapshot.SystemIntegrityRelevant = ReadMapBool(systemIntegrity, "Relevant") || ReadMapBool(root, "SystemIntegrityRelevant");
            snapshot.SystemIntegrityMode = ReadMapString(systemIntegrity, "Mode");
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegrityMode)) { snapshot.SystemIntegrityMode = ReadMapString(root, "SystemIntegrityMode"); }
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegrityMode)) { snapshot.SystemIntegrityMode = SystemIntegrityGuardMode; }
            snapshot.SystemIntegrityState = ReadMapString(systemIntegrity, "State");
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegrityState)) { snapshot.SystemIntegrityState = ReadMapString(root, "SystemIntegrityState"); }
            snapshot.SystemIntegritySummary = ReadMapString(systemIntegrity, "Summary");
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegritySummary)) { snapshot.SystemIntegritySummary = ReadMapString(root, "SystemIntegritySummary"); }
            snapshot.SystemIntegrityDetail = ReadMapString(systemIntegrity, "Detail");
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegrityDetail)) { snapshot.SystemIntegrityDetail = ReadMapString(root, "SystemIntegrityDetail"); }
            snapshot.SystemIntegrityBackupAvailable = ReadMapBool(systemIntegrity, "BackupAvailable") || ReadMapBool(root, "SystemIntegrityBackupAvailable");
            snapshot.SystemIntegrityMmcssServiceRunning = ReadMapBool(systemIntegrity, "MmcssServiceRunning") || ReadMapBool(root, "SystemIntegrityMmcssServiceRunning");
            snapshot.SystemIntegrityMmcssServiceStatus = ReadMapString(systemIntegrity, "MmcssServiceStatus");
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegrityMmcssServiceStatus)) { snapshot.SystemIntegrityMmcssServiceStatus = ReadMapString(root, "SystemIntegrityMmcssServiceStatus"); }
            snapshot.SystemIntegritySystemResponsiveness = ReadMapInt(systemIntegrity, "SystemResponsiveness");
            if (snapshot.SystemIntegritySystemResponsiveness == 0 && ReadMapInt(root, "SystemIntegritySystemResponsiveness") != 0) { snapshot.SystemIntegritySystemResponsiveness = ReadMapInt(root, "SystemIntegritySystemResponsiveness"); }
            snapshot.SystemIntegritySystemResponsivenessState = ReadMapString(systemIntegrity, "SystemResponsivenessState");
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegritySystemResponsivenessState)) { snapshot.SystemIntegritySystemResponsivenessState = ReadMapString(root, "SystemIntegritySystemResponsivenessState"); }
            snapshot.SystemIntegritySystemResponsivenessDetail = ReadMapString(systemIntegrity, "SystemResponsivenessDetail");
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegritySystemResponsivenessDetail)) { snapshot.SystemIntegritySystemResponsivenessDetail = ReadMapString(root, "SystemIntegritySystemResponsivenessDetail"); }
            snapshot.SystemIntegrityHybridCpuDetected = ReadMapBool(systemIntegrity, "HybridCpuDetected") || ReadMapBool(root, "SystemIntegrityHybridCpuDetected");
            snapshot.SystemIntegrityLogicalProcessorCount = ReadMapInt(systemIntegrity, "LogicalProcessorCount");
            if (snapshot.SystemIntegrityLogicalProcessorCount <= 0) { snapshot.SystemIntegrityLogicalProcessorCount = ReadMapInt(root, "SystemIntegrityLogicalProcessorCount"); }
            snapshot.SystemIntegrityHybridSchedulerState = ReadMapString(systemIntegrity, "HybridSchedulerState");
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegrityHybridSchedulerState)) { snapshot.SystemIntegrityHybridSchedulerState = ReadMapString(root, "SystemIntegrityHybridSchedulerState"); }
            snapshot.SystemIntegrityHybridSchedulerDetail = ReadMapString(systemIntegrity, "HybridSchedulerDetail");
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegrityHybridSchedulerDetail)) { snapshot.SystemIntegrityHybridSchedulerDetail = ReadMapString(root, "SystemIntegrityHybridSchedulerDetail"); }
            snapshot.SystemIntegritySelfThrottleEligible = ReadMapBool(systemIntegrity, "SelfThrottleEligible") || ReadMapBool(root, "SystemIntegritySelfThrottleEligible");
            snapshot.SystemIntegritySelfThrottleState = ReadMapString(systemIntegrity, "SelfThrottleState");
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegritySelfThrottleState)) { snapshot.SystemIntegritySelfThrottleState = ReadMapString(root, "SystemIntegritySelfThrottleState"); }
            snapshot.SystemIntegritySelfThrottleDetail = ReadMapString(systemIntegrity, "SelfThrottleDetail");
            if (String.IsNullOrWhiteSpace(snapshot.SystemIntegritySelfThrottleDetail)) { snapshot.SystemIntegritySelfThrottleDetail = ReadMapString(root, "SystemIntegritySelfThrottleDetail"); }
            snapshot.SystemIntegrityIssueCount = ReadMapInt(systemIntegrity, "IssueCount");
            if (snapshot.SystemIntegrityIssueCount <= 0) { snapshot.SystemIntegrityIssueCount = ReadMapInt(root, "SystemIntegrityIssueCount"); }
            snapshot.SystemIntegrityRecommendationCount = ReadMapInt(systemIntegrity, "RecommendationCount");
            if (snapshot.SystemIntegrityRecommendationCount <= 0) { snapshot.SystemIntegrityRecommendationCount = ReadMapInt(root, "SystemIntegrityRecommendationCount"); }
            snapshot.SystemIntegritySafeRecommendationCount = ReadMapInt(systemIntegrity, "SafeRecommendationCount");
            if (snapshot.SystemIntegritySafeRecommendationCount <= 0) { snapshot.SystemIntegritySafeRecommendationCount = ReadMapInt(root, "SystemIntegritySafeRecommendationCount"); }
            snapshot.SystemIntegrityOptionalRecommendationCount = ReadMapInt(systemIntegrity, "OptionalRecommendationCount");
            if (snapshot.SystemIntegrityOptionalRecommendationCount <= 0) { snapshot.SystemIntegrityOptionalRecommendationCount = ReadMapInt(root, "SystemIntegrityOptionalRecommendationCount"); }
            snapshot.SystemIntegrityExperimentalRecommendationCount = ReadMapInt(systemIntegrity, "ExperimentalRecommendationCount");
            if (snapshot.SystemIntegrityExperimentalRecommendationCount <= 0) { snapshot.SystemIntegrityExperimentalRecommendationCount = ReadMapInt(root, "SystemIntegrityExperimentalRecommendationCount"); }
            snapshot.SystemIntegrityRestartRecommendationCount = ReadMapInt(systemIntegrity, "RestartRecommendationCount");
            if (snapshot.SystemIntegrityRestartRecommendationCount <= 0) { snapshot.SystemIntegrityRestartRecommendationCount = ReadMapInt(root, "SystemIntegrityRestartRecommendationCount"); }
            snapshot.SystemIntegrityApplyBlockedRecommendationCount = ReadMapInt(systemIntegrity, "ApplyBlockedRecommendationCount");
            if (snapshot.SystemIntegrityApplyBlockedRecommendationCount <= 0) { snapshot.SystemIntegrityApplyBlockedRecommendationCount = ReadMapInt(root, "SystemIntegrityApplyBlockedRecommendationCount"); }
            snapshot.SystemIntegrityRecommendations = ReadMapDictionaryList(systemIntegrity, "Recommendations");
            if (snapshot.SystemIntegrityRecommendations.Count <= 0) { snapshot.SystemIntegrityRecommendations = ReadMapDictionaryList(root, "SystemIntegrityRecommendations"); }
            snapshot.SystemIntegritySignals = ReadMapStringList(systemIntegrity, "Signals");
            if (snapshot.SystemIntegritySignals.Count <= 0) { snapshot.SystemIntegritySignals = ReadMapStringList(root, "SystemIntegritySignals"); }
            snapshot.SystemIntegrityIssues = ReadMapStringList(systemIntegrity, "Issues");
            if (snapshot.SystemIntegrityIssues.Count <= 0) { snapshot.SystemIntegrityIssues = ReadMapStringList(root, "SystemIntegrityIssues"); }
            snapshot.StateAgeSeconds = GetIsoAgeSeconds(snapshot.UpdatedAt);
            if (snapshot.LoopSeconds <= 0) { snapshot.LoopSeconds = CoreServiceLoopSeconds; }
            if (snapshot.ProtocolVersion <= 0) { snapshot.ProtocolVersion = CoreProtocolVersion; }
            if (snapshot.MinimumSupportedProtocolVersion <= 0) { snapshot.MinimumSupportedProtocolVersion = CoreMinimumSupportedProtocolVersion; }
            if (String.IsNullOrWhiteSpace(snapshot.PipeName)) { snapshot.PipeName = CorePipeName; }
            if (String.IsNullOrWhiteSpace(snapshot.ContextProvider)) { snapshot.ContextProvider = "SessionAgentV1+" + CoreContextProviderLegacyBridge; }
            if (snapshot.StaleThresholdSeconds <= 0) { snapshot.StaleThresholdSeconds = CoreServiceStalePassSeconds; }
            if (String.IsNullOrWhiteSpace(snapshot.Health))
            {
                snapshot.Health = ClassifyCoreServiceHealth(snapshot.Status, snapshot.Action, snapshot.ExitCode, snapshot.Installed, snapshot.Running, snapshot.AutoTaskInstalled, snapshot.TelemetryStale, snapshot.AutoTaskKicked);
            }
            if (snapshot.StateAgeSeconds > Math.Max(90, snapshot.LoopSeconds * 4) && String.Equals(snapshot.Health, "Healthy", StringComparison.OrdinalIgnoreCase))
            {
                snapshot.Health = "Stale";
                snapshot.Summary = "Core service state has not refreshed recently.";
            }
            if (String.IsNullOrWhiteSpace(snapshot.Summary))
            {
                snapshot.Summary = BuildCoreServiceSummary(snapshot.Health, snapshot.Action, snapshot.AutoTaskInstalled, snapshot.TelemetryStale, snapshot.AutoTaskKicked, snapshot.ScoreAgeSeconds);
            }
            snapshot.NeedsAttention = ReadMapBool(root, "NeedsAttention") || IsCoreServiceAttentionHealth(snapshot.Health);
        }
        catch
        {
        }

        return snapshot;
    }

    private static void WriteCoreServiceState(string status, string action, RunResult result, bool autoTaskInstalled, bool kicked, int scoreAgeSeconds, int staleThresholdSeconds)
    {
        WriteCoreServiceState(status, action, result, autoTaskInstalled, kicked, scoreAgeSeconds, staleThresholdSeconds, "");
    }

    private static void WriteCoreServiceState(string status, string action, RunResult result, bool autoTaskInstalled, bool kicked, int scoreAgeSeconds, int staleThresholdSeconds, string source)
    {
        try
        {
            Directory.CreateDirectory(outputsPath);
            bool installed = IsCoreServiceInstalled();
            bool running = installed && IsCoreServiceRunning();
            int exitCode = result == null ? 0 : result.ExitCode;
            bool telemetryStale = scoreAgeSeconds < 0 || scoreAgeSeconds > staleThresholdSeconds;
            string health = ClassifyCoreServiceHealth(status, action, exitCode, installed, running, autoTaskInstalled, telemetryStale, kicked);
            IDictionary<string, object> state = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            state["AppVersion"] = AppVersion;
            state["ServiceName"] = CoreServiceName;
            state["DisplayName"] = CoreServiceDisplayName;
            state["ProtocolVersion"] = CoreProtocolVersion;
            state["MinimumSupportedProtocolVersion"] = CoreMinimumSupportedProtocolVersion;
            state["PipeName"] = CorePipeName;
            state["ContextProvider"] = "SessionAgentV1+" + CoreContextProviderLegacyBridge;
            state["Capabilities"] = BuildCoreServiceCapabilities();
            IDictionary<string, object> ipcState = BuildCorePipeStatePayload();
            state["Ipc"] = ipcState;
            state["IpcListening"] = ReadMapBool(ipcState, "Listening");
            state["IpcSecureAcl"] = ReadMapBool(ipcState, "SecureAcl");
            state["IpcHeartbeatAt"] = ReadMapString(ipcState, "HeartbeatAt");
            state["IpcLastClientAt"] = ReadMapString(ipcState, "LastClientAt");
            state["IpcLastCommand"] = ReadMapString(ipcState, "LastCommand");
            state["IpcLastError"] = ReadMapString(ipcState, "LastError");
            state["Installed"] = installed;
            state["Running"] = running;
            state["Status"] = status ?? "Unknown";
            state["Action"] = action ?? "Observe";
            state["Health"] = health;
            state["Summary"] = BuildCoreServiceSummary(health, action, autoTaskInstalled, telemetryStale, kicked, scoreAgeSeconds);
            state["NeedsAttention"] = IsCoreServiceAttentionHealth(health);
            state["AutoTaskInstalled"] = autoTaskInstalled;
            state["SessionAgentTaskInstalled"] = IsTaskInstalled(SessionAgentTaskName);
            state["AutoTaskKicked"] = kicked;
            state["TelemetryFresh"] = !telemetryStale;
            state["TelemetryStale"] = telemetryStale;
            state["ScorePath"] = scorePath;
            state["ScoreAgeSeconds"] = scoreAgeSeconds;
            state["StaleThresholdSeconds"] = staleThresholdSeconds;
            state["LoopSeconds"] = CoreServiceLoopSeconds;
            state["ExitCode"] = exitCode;
            state["Detail"] = result == null ? "" : ShortTaskError(result.Output);
            if (!String.IsNullOrWhiteSpace(source)) { state["Source"] = source; }
            SessionAgentSnapshot sessionAgent = LoadSessionAgentSnapshot();
            MemoryStabilitySnapshot memoryStability = BuildMemoryStabilitySnapshot(sessionAgent, source);
            ApplyMemoryStabilitySnapshot(state, memoryStability);
            SystemIntegritySnapshot systemIntegrity = BuildSystemIntegritySnapshot(sessionAgent, source);
            ApplySystemIntegritySnapshot(state, systemIntegrity);
            state["SessionAgent"] = BuildSessionAgentPayload(sessionAgent);
            state["SessionContext"] = BuildSessionContextPayload(sessionAgent);
            state["SessionForeground"] = BuildSessionForegroundPayload(sessionAgent);
            state["UpdatedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            AtomicWriteJsonMap(coreServiceStatePath, state);
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=core-service-state status=failed detail=" + ShortTaskError(ex.Message));
        }
    }

    private static RunResult RunCoreServicePass(string source)
    {
        bool autoTaskInstalled = IsTaskInstalled(AutoTaskName);
        int scoreAgeSeconds = GetFileAgeSeconds(scorePath);
        int intervalSeconds = Math.Max(60, LoadAutomationIntervalMinutes() * 60);
        int staleThresholdSeconds = Math.Max(CoreServiceStalePassSeconds, intervalSeconds * 2);
        bool stale = scoreAgeSeconds < 0 || scoreAgeSeconds > staleThresholdSeconds;
        string action = "Observe";
        bool kicked = false;
        RunResult result = new RunResult(0, "Engine telemetry is fresh.");

        if (!autoTaskInstalled)
        {
            action = "NoAutoTask";
            result = new RunResult(0, "Automatic engine task is not installed.");
        }
        else if (stale)
        {
            action = "KickAutoTask";
            result = RunHidden("schtasks.exe", "/Run /TN " + Quote(AutoTaskName), 15000);
            kicked = result.ExitCode == 0;
            AppendOperationalLog("action=core-service-watchdog source=" + SanitizeLogToken(source) + " status=" + (kicked ? "kicked" : "failed") + " scoreAgeSeconds=" + scoreAgeSeconds.ToString(CultureInfo.InvariantCulture) + " detail=" + ShortTaskError(result.Output));
        }

        WriteCoreServiceState("Running", action, result, autoTaskInstalled, kicked, scoreAgeSeconds, staleThresholdSeconds, source);
        return result.ExitCode == 0 ? new RunResult(0, action) : result;
    }

    private static RunResult InstallAutomatic()
    {
        return InstallAutomatic(true);
    }

    private static RunResult InstallAutomatic(bool allowElevatedRepair)
    {
        if (allowElevatedRepair && !IsCurrentProcessElevated() && !IsTaskInstalled(AutoTaskName))
        {
            RunResult elevated = RunElevatedInstallComplete();
            if (IsTaskInstalled(AutoTaskName))
            {
                SaveLocalAutoEngine(false);
                return new RunResult(0, "Automatic engine enabled through elevated setup.");
            }
            AppendOperationalLog("action=install-auto-elevated-fallback exitCode=" + elevated.ExitCode.ToString(CultureInfo.InvariantCulture));
        }

        RunResult result = RunPowerShellScript(autoManagerPath, "-Action Install -AppExePath " + Quote(GetLaunchExecutablePath()), 60000);
        if (IsTaskInstalled(AutoTaskName))
        {
            SaveLocalAutoEngine(false);
            return result.ExitCode == 0 ? result : new RunResult(0, "Automatic engine enabled through Task Scheduler.");
        }

        SaveLocalAutoEngine(true);
        string reason = result.ExitCode == 0 ? "Task Scheduler unavailable" : ShortTaskError(result.Output);
        return new RunResult(0, "Automatic engine enabled through local tray fallback. " + reason);
    }
    private static RunResult UninstallAutomatic()
    {
        RunResult result = new RunResult(0, "Automatic engine was already using local control.");
        if (IsTaskInstalled(AutoTaskName))
        {
            result = RunPowerShellScript(autoManagerPath, "-Action Uninstall", 60000);
            if (result.ExitCode != 0 && !IsCurrentProcessElevated() && LooksLikeAccessDenied(result.Output))
            {
                result = RunElevatedSelfCommand("--uninstall-auto", "uninstall-auto", 120000);
            }
        }
        SaveLocalAutoEngine(false);
        if (IsTaskInstalled(AutoTaskName))
        {
            return new RunResult(result.ExitCode == 0 ? 1 : result.ExitCode, "Nao consegui desativar a tarefa do motor. Aceite a permissao de administrador para concluir a alteracao.");
        }
        if (result.ExitCode != 0) { return result; }
        return new RunResult(0, "Automatic engine disabled.");
    }

    private static bool IsLocalAutoEngineEnabled()
    {
        return ReadUiFlag("LocalAutoEngineEnabled");
    }

    private static void SaveLocalAutoEngine(bool enabled)
    {
        SaveUiFlag("LocalAutoEngineEnabled", enabled);
    }

    private static bool IsAutomaticEngineEnabled()
    {
        return IsTaskInstalled(AutoTaskName) || IsLocalAutoEngineEnabled();
    }

    private static int LoadAutomationIntervalMinutes()
    {
        const int fallbackIntervalMinutes = 5;
        try
        {
            IDictionary<string, object> root = LoadConfigRoot();
            object automationObject;
            if (root == null || !root.TryGetValue("Automation", out automationObject)) { return fallbackIntervalMinutes; }
            IDictionary<string, object> automation = automationObject as IDictionary<string, object>;
            if (automation == null) { return fallbackIntervalMinutes; }
            object intervalObject;
            if (!automation.TryGetValue("IntervalMinutes", out intervalObject) || intervalObject == null) { return fallbackIntervalMinutes; }
            int interval = Convert.ToInt32(intervalObject, CultureInfo.InvariantCulture);
            return Math.Max(1, Math.Min(60, interval));
        }
        catch
        {
            return fallbackIntervalMinutes;
        }
    }

    private static string GetStartupRegistryCommand()
    {
        return Quote(GetLaunchExecutablePath()) + " --tray";
    }

    private static bool IsStartupRegistryInstalled()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
            {
                if (key == null) { return false; }
                string value = Convert.ToString(key.GetValue(AppName), CultureInfo.InvariantCulture);
                return !String.IsNullOrWhiteSpace(value) && value.IndexOf("SmartBackgroundNap", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
        catch
        {
            return false;
        }
    }

    private static RunResult EnableStartupRegistry()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (key == null) { return new RunResult(1, "Could not open current-user startup registry key."); }
                key.SetValue(AppName, GetStartupRegistryCommand(), RegistryValueKind.String);
            }
            return new RunResult(0, "Startup enabled for the current user.");
        }
        catch (Exception ex)
        {
            return new RunResult(1, "Could not enable current-user startup: " + ex.Message);
        }
    }

    private static RunResult RemoveStartupRegistry()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (key != null && key.GetValue(AppName) != null)
                {
                    key.DeleteValue(AppName, false);
                }
            }
            return new RunResult(0, "Current-user startup removed.");
        }
        catch (Exception ex)
        {
            return new RunResult(1, "Could not remove current-user startup: " + ex.Message);
        }
    }

    private static RunResult InstallStartup()
    {
        return InstallStartup(true);
    }

    private static RunResult InstallStartup(bool allowElevatedRepair)
    {
        if (allowElevatedRepair && !IsCurrentProcessElevated() && !IsTaskInstalled(TrayTaskName))
        {
            RunResult elevated = RunElevatedInstallComplete();
            if (IsTaskInstalled(TrayTaskName))
            {
                RemoveStartupRegistry();
                return new RunResult(0, "Tray startup enabled through elevated setup.");
            }
            AppendOperationalLog("action=install-startup-elevated-fallback exitCode=" + elevated.ExitCode.ToString(CultureInfo.InvariantCulture));
        }

        RunResult result = RunPowerShellScript(trayManagerPath, "-Action Install -AppExePath " + Quote(GetLaunchExecutablePath()), 60000);
        if (IsTaskInstalled(TrayTaskName))
        {
            RemoveStartupRegistry();
            return result.ExitCode == 0 ? result : new RunResult(0, "Tray startup enabled through Task Scheduler.");
        }

        RunResult fallback = EnableStartupRegistry();
        if (fallback.ExitCode == 0)
        {
            return new RunResult(0, "Tray startup enabled for the current user. " + (result.ExitCode == 0 ? "Task Scheduler unavailable." : ShortTaskError(result.Output)));
        }
        return new RunResult(1, "Nao consegui configurar a inicializacao automatica neste usuario. Abra o Smart Nap como administrador e tente novamente.");
    }
    private static RunResult UninstallStartup()
    {
        RunResult taskResult = new RunResult(0, "Tray startup task was already off.");
        if (IsTaskInstalled(TrayTaskName))
        {
            taskResult = RunHidden("schtasks.exe", "/Delete /TN " + Quote(TrayTaskName) + " /F", 10000);
            if (taskResult.ExitCode != 0 && !IsCurrentProcessElevated() && LooksLikeAccessDenied(taskResult.Output))
            {
                taskResult = RunElevatedSelfCommand("--uninstall-startup", "uninstall-startup", 120000);
            }
        }
        RunResult registryResult = RemoveStartupRegistry();
        if (IsTaskInstalled(TrayTaskName))
        {
            return new RunResult(taskResult.ExitCode == 0 ? 1 : taskResult.ExitCode, "Nao consegui desativar a inicializacao automatica. Aceite a permissao de administrador para remover a tarefa elevada.");
        }
        if (taskResult.ExitCode != 0) { return taskResult; }
        if (registryResult.ExitCode != 0) { return registryResult; }
        return new RunResult(0, "Tray startup disabled. Current Smart Nap session kept running.");
    }

    private static bool IsStartupInstalled()
    {
        return IsTaskInstalled(TrayTaskName) || IsStartupRegistryInstalled();
    }

    private static RunResult InstallComplete()
    {
        return InstallComplete(true);
    }

    private static RunResult InstallComplete(bool allowElevatedRepair)
    {
        if (allowElevatedRepair && !IsCurrentProcessElevated() && (!ArePrimaryScheduledTasksInstalled() || !IsCoreServiceInstalled() || !IsCoreServiceRunning()))
        {
            RunResult elevated = RunElevatedInstallComplete();
            if (ArePrimaryScheduledTasksInstalled() && IsCoreServiceInstalled())
            {
                return elevated.ExitCode == 0 ? elevated : new RunResult(0, "Elevated task base repaired.");
            }
            AppendOperationalLog("action=install-complete-elevated-fallback exitCode=" + elevated.ExitCode.ToString(CultureInfo.InvariantCulture));
        }

        RunResult dashboard = InstallDashboardTask();
        RunResult auto = InstallAutomatic(false);
        RunResult startup = InstallStartup(false);
        RunResult sessionAgent = InstallSessionAgent(false);
        RunResult coreService = InstallCoreService(false);
        if (coreService.ExitCode != 0)
        {
            AppendOperationalLog("action=install-core-service status=deferred detail=" + ShortTaskError(coreService.Output));
            coreService = new RunResult(0, "Core service deferred: " + ShortTaskError(coreService.Output));
        }
        RunResult power = EnsureSmartNapPowerPlans();
        if (power.ExitCode != 0)
        {
            AppendOperationalLog("action=install-power-plans status=deferred detail=" + ShortTaskError(power.Output));
        }
        EnsureSmartLearningDefaultEnabled();
        RunResult shortcuts = InstallStartMenuShortcuts();
        if (shortcuts.ExitCode != 0)
        {
            AppendOperationalLog("action=install-start-menu-shortcut status=deferred detail=" + ShortTaskError(shortcuts.Output));
        }
        RunResult verify = EnsureInstalledRuntimeReady(false);
        return RunResult.Combine(RunResult.Combine(RunResult.Combine(RunResult.Combine(RunResult.Combine(dashboard, auto), startup), sessionAgent), coreService), verify);
    }

    private static RunResult RestartAutomaticEngineTaskForInstallVerify(DateTime cutoffUtc)
    {
        if (!IsTaskInstalled(AutoTaskName))
        {
            return new RunResult(1, "Automatic engine task is not installed.");
        }

        RunResult end = RunHidden("schtasks.exe", "/End /TN " + Quote(AutoTaskName), 10000);
        Thread.Sleep(750);
        RunResult start = RunHidden("schtasks.exe", "/Run /TN " + Quote(AutoTaskName), 15000);
        bool freshScore = WaitForFileWriteAfter(scorePath, cutoffUtc, 45000);
        string detail = "end=" + end.ExitCode.ToString(CultureInfo.InvariantCulture) +
            "; start=" + start.ExitCode.ToString(CultureInfo.InvariantCulture) +
            "; freshScore=" + freshScore.ToString(CultureInfo.InvariantCulture);
        AppendOperationalLog("action=install-verify-auto-task-refresh " + SanitizeLogToken(detail));

        if (start.ExitCode != 0)
        {
            return new RunResult(start.ExitCode, "Automatic engine task did not start after setup. " + ShortTaskError(start.Output));
        }
        if (!freshScore)
        {
            return new RunResult(1, "Automatic engine did not publish fresh telemetry after setup.");
        }
        return new RunResult(0, "Automatic engine refreshed after setup.");
    }

    private static RunResult EnsureInstalledRuntimeReady(bool allowElevatedRepair)
    {
        if (!IsCurrentProcessElevated())
        {
            if (allowElevatedRepair)
            {
                return RunElevatedSelfCommand("--repair-install", "install-runtime-ready", 120000);
            }
            return new RunResult(5, "Administrator permission is required to verify and start Smart Nap services.");
        }

        bool tasksReady = ArePrimaryScheduledTasksInstalled();
        bool sessionAgentReady = IsTaskInstalled(SessionAgentTaskName);
        bool serviceInstalled = IsCoreServiceInstalled();
        if (!tasksReady || !sessionAgentReady || !serviceInstalled)
        {
            string detail = "tasks=" + tasksReady.ToString(CultureInfo.InvariantCulture) +
                "; sessionAgent=" + sessionAgentReady.ToString(CultureInfo.InvariantCulture) +
                "; coreService=" + serviceInstalled.ToString(CultureInfo.InvariantCulture);
            AppendOperationalLog("action=install-verify status=FAIL detail=" + SanitizeLogToken(detail));
            WriteCoreServiceState("InstallIncomplete", "InstallVerify", new RunResult(1, detail), IsTaskInstalled(AutoTaskName), false, GetFileAgeSeconds(scorePath), CoreServiceStalePassSeconds, "install-verify");
            return new RunResult(1, "Setup incomplete: " + detail);
        }

        DateTime verifyStartedUtc = DateTime.UtcNow.AddSeconds(-1);
        RunResult lastStart = new RunResult(0, "Core service already running.");
        for (int attempt = 0; attempt < 3 && !IsCoreServiceRunning(); attempt++)
        {
            lastStart = StartCoreService(false);
            if (WaitForCoreServiceRunningState(true, 15000)) { break; }
            Thread.Sleep(750);
        }

        RunResult agentStart = RunHidden("schtasks.exe", "/Run /TN " + Quote(SessionAgentTaskName), 10000);
        RunResult engineRefresh = RestartAutomaticEngineTaskForInstallVerify(verifyStartedUtc);
        bool running = IsCoreServiceRunning();
        if (running && engineRefresh.ExitCode == 0)
        {
            AppendOperationalLog("action=install-verify status=OK sessionAgentStart=" + agentStart.ExitCode.ToString(CultureInfo.InvariantCulture));
            return new RunResult(0, "Smart Nap services are installed and running.");
        }

        string output = running
            ? ("Automatic engine did not refresh after setup. " + ShortTaskError(engineRefresh.Output))
            : ("Core service did not reach Running after setup. " + ShortTaskError(lastStart.Output));
        WriteCoreServiceState("StartFailed", "InstallVerify", new RunResult(1, output), IsTaskInstalled(AutoTaskName), false, GetFileAgeSeconds(scorePath), CoreServiceStalePassSeconds, "install-verify");
        AppendOperationalLog("action=install-verify status=FAIL detail=" + ShortTaskError(output));
        return new RunResult(1, output);
    }

    private static RunResult UninstallComplete()
    {
        RunResult sessionAgent = UninstallSessionAgent();
        RunResult startup = UninstallStartup();
        RunResult auto = UninstallAutomatic();
        RunResult dashboard = UninstallDashboardTask();
        RunResult coreService = UninstallCoreService(true);
        RunResult shortcuts = RemoveStartMenuShortcuts();
        if (shortcuts.ExitCode != 0)
        {
            AppendOperationalLog("action=uninstall-start-menu-shortcut status=deferred detail=" + ShortTaskError(shortcuts.Output));
        }
        return RunResult.Combine(RunResult.Combine(RunResult.Combine(RunResult.Combine(sessionAgent, startup), auto), dashboard), coreService);
    }

    private static bool IsTaskInstalled(string taskName)
    {
        RunResult result = RunHidden("schtasks.exe", "/Query /TN " + Quote(taskName), 8000);
        return result.ExitCode == 0;
    }

    private static bool LooksLikeAccessDenied(string output)
    {
        if (String.IsNullOrWhiteSpace(output)) { return false; }
        string compact = Regex.Replace(output, @"\s+", " ").Trim();
        return compact.IndexOf("0x80070005", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("Acesso negado", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("Access is denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("PermissionDenied", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("UnauthorizedAccess", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    private static string ShortTaskError(string output)
    {
        if (String.IsNullOrWhiteSpace(output)) { return "Task Scheduler unavailable."; }
        string compact = Regex.Replace(output, @"\s+", " ").Trim();
        if (compact.Length > 180) { compact = compact.Substring(0, 180).TrimEnd() + "..."; }
        return compact;
    }
    private static string FriendlyUiError(string output)
    {
        if (String.IsNullOrWhiteSpace(output)) { return "Nenhum detalhe foi retornado."; }

        if (LooksLikeTaskSetupWarning(output))
        {
            return "O Windows bloqueou o registro da tarefa elevada. Abra o Smart Nap como administrador uma vez para concluir a instalacao completa; o motor local continua ativo.";
        }

        if (LooksLikeRecoverableRunTimeout(output))
        {
            return "A acao demorou mais que o esperado, mas o Smart Nap evitou travar o painel e continua pronto para o proximo ciclo.";
        }

        string compact = Regex.Replace(output, @"\s+", " ").Trim();
        if (compact.IndexOf("Permissao de administrador cancelada", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("Administrator permission was cancelled", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Permissao de administrador cancelada. O Smart Nap continua aberto, mas a instalacao completa sera concluida quando a permissao for aceita.";
        }

        if (LooksLikeAccessDenied(output))
        {
            return "O Windows negou permissao para uma etapa avancada. O Smart Nap manteve o controle local e pulou apenas o ajuste bloqueado.";
        }

        if (compact.Length > 320) { compact = compact.Substring(0, 320).TrimEnd() + "..."; }
        return compact;
    }
    private static bool LooksLikeTaskSetupWarning(string output)
    {
        if (String.IsNullOrWhiteSpace(output)) { return false; }
        string compact = Regex.Replace(output, @"\s+", " ").Trim();
        bool taskRelated = compact.IndexOf("Register-ScheduledTask", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("ScheduledTask", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("Task Scheduler", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("SmartBackgroundNapTray", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("SmartBackgroundNapDashboard", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("schtasks", StringComparison.OrdinalIgnoreCase) >= 0;
        bool denied = compact.IndexOf("0x80070005", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("Acesso negado", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("Access is denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("PermissionDenied", StringComparison.OrdinalIgnoreCase) >= 0;
        return taskRelated && denied;
    }
    private static bool LooksLikeRecoverableRunTimeout(string output)
    {
        if (String.IsNullOrWhiteSpace(output)) { return false; }
        string compact = Regex.Replace(output, @"\s+", " ").Trim();
        bool timedOut = compact.StartsWith("Timed out.", StringComparison.OrdinalIgnoreCase) ||
            compact.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!timedOut) { return false; }
        return compact.IndexOf("PriorityRestore", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("MemoryPriority", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("IoPriority", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("PowerThrottling", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("CpuAffinity", StringComparison.OrdinalIgnoreCase) >= 0 ||
            compact.IndexOf("background-nap-state-latest.json", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ShouldSuppressRunModal(string output)
    {
        return LooksLikeTaskSetupWarning(output) || LooksLikeRecoverableRunTimeout(output);
    }

    private static string BuildDeferredRunDetail(string output)
    {
        if (LooksLikeRecoverableRunTimeout(output))
        {
            return "Passe pesado finalizado sem travar o launcher. O motor continua ajustando no proximo ciclo.";
        }
        return "Passe manual finalizado. A inicializacao automatica elevada ficou pendente de permissao.";
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
        string sourcePath = GetEffectiveConfigPath();
        if (!File.Exists(sourcePath)) { return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); }
        return LoadJsonMapWithRecovery(sourcePath);
    }

    private static string GetEffectiveConfigPath()
    {
        try
        {
            if (!String.IsNullOrWhiteSpace(userConfigPath) && File.Exists(userConfigPath))
            {
                return userConfigPath;
            }
        }
        catch
        {
        }
        return configPath;
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
            bool enabled;
            if (TryReadSmartLearningSetting(out enabled))
            {
                EnsureSmartLearningConfigValue(enabled);
                return enabled;
            }

            if (TryReadSmartLearningFromConfig(out enabled))
            {
                SaveSmartLearningSetting(enabled);
                return enabled;
            }

            if (TryReadSmartLearningFromLog(out enabled))
            {
                SaveSmartLearningSetting(enabled);
                WriteSmartLearningToConfig(enabled);
                return enabled;
            }
        }
        catch
        {
        }
        return false;
    }

    private static RunResult SetSmartLearningEnabled(bool enabled)
    {
        try
        {
            SaveSmartLearningSetting(enabled);
            WriteSmartLearningToConfig(enabled);
            AppendOperationalLog("action=learning enabled=" + enabled.ToString().ToLowerInvariant());
            return new RunResult(0, enabled ? "Smart Learning enabled." : "Smart Learning disabled.");
        }
        catch (Exception ex)
        {
            return new RunResult(1, ex.Message);
        }
    }

    private static void SyncSmartLearningSettingToConfig()
    {
        try
        {
            bool enabled;
            if (TryReadSmartLearningSetting(out enabled))
            {
                EnsureSmartLearningConfigValue(enabled);
                return;
            }

            if (TryReadSmartLearningFromLog(out enabled))
            {
                SaveSmartLearningSetting(enabled);
                WriteSmartLearningToConfig(enabled);
            }
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=learning-sync status=failed error=" + ex.GetType().Name);
        }
    }

    private static bool TryReadSmartLearningSetting(out bool enabled)
    {
        enabled = false;
        try
        {
            if (string.IsNullOrEmpty(learningSettingsPath) || !File.Exists(learningSettingsPath)) { return false; }
            IDictionary<string, object> root = LoadJsonMapWithRecovery(learningSettingsPath);
            if (root == null || !root.ContainsKey("LearningEnabled")) { return false; }
            enabled = GetMapBool(root, "LearningEnabled", false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadSmartLearningFromConfig(out bool enabled)
    {
        enabled = false;
        try
        {
            IDictionary<string, object> root = LoadConfigRoot();
            object smartObject;
            IDictionary<string, object> smart = root.TryGetValue("SmartMode", out smartObject) ? smartObject as IDictionary<string, object> : null;
            if (smart == null || !smart.ContainsKey("LearningEnabled")) { return false; }
            enabled = GetMapBool(smart, "LearningEnabled", false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadSmartLearningFromLog(out bool enabled)
    {
        enabled = false;
        try
        {
            if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath)) { return false; }
            string[] lines = File.ReadAllLines(logPath, Encoding.UTF8);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].IndexOf("action=learning", StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                string raw = ExtractLogToken(lines[i], "enabled");
                bool parsed;
                if (Boolean.TryParse(raw, out parsed))
                {
                    enabled = parsed;
                    return true;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    private static string ExtractLogToken(string line, string key)
    {
        if (String.IsNullOrEmpty(line) || String.IsNullOrEmpty(key)) { return String.Empty; }
        string marker = key + "=";
        int start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) { return String.Empty; }
        start += marker.Length;
        int end = line.IndexOf(' ', start);
        if (end < 0) { end = line.Length; }
        return line.Substring(start, end - start).Trim();
    }

    private static void SaveSmartLearningSetting(bool enabled)
    {
        Directory.CreateDirectory(appRoot);
        IDictionary<string, object> root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        root["LearningEnabled"] = enabled;
        root["UpdatedAt"] = DateTime.Now.ToString("s", CultureInfo.InvariantCulture);
        AtomicWriteJsonMap(learningSettingsPath, root);
    }

    private static void EnsureSmartLearningConfigValue(bool enabled)
    {
        bool current;
        if (!TryReadSmartLearningFromConfig(out current) || current != enabled)
        {
            WriteSmartLearningToConfig(enabled);
        }
    }

    private static void WriteSmartLearningToConfig(bool enabled)
    {
        IDictionary<string, object> root = LoadConfigRoot();
        IDictionary<string, object> smart = GetOrCreateMap(root, "SmartMode");
        smart["LearningEnabled"] = enabled;
        Directory.CreateDirectory(appRoot);
        AtomicWriteJsonMap(userConfigPath, root);
    }


    private static string GetMapString(IDictionary<string, object> map, string key)
    {
        object value;
        if (map == null || !map.TryGetValue(key, out value) || value == null) { return String.Empty; }
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static List<string> GetMapStringList(IDictionary<string, object> map, string key)
    {
        List<string> output = new List<string>();
        object value;
        if (map == null || !map.TryGetValue(key, out value) || value == null) { return output; }

        string textValue = value as string;
        if (!String.IsNullOrWhiteSpace(textValue))
        {
            output.Add(textValue.Trim());
            return output;
        }

        System.Collections.IEnumerable enumerable = value as System.Collections.IEnumerable;
        if (enumerable == null) { return output; }
        foreach (object item in enumerable)
        {
            string itemText = Convert.ToString(item, CultureInfo.InvariantCulture);
            if (!String.IsNullOrWhiteSpace(itemText)) { output.Add(itemText.Trim()); }
        }
        return output;
    }

    private static bool GetMapBool(IDictionary<string, object> map, string key, bool fallback)
    {
        object value;
        if (map == null || !map.TryGetValue(key, out value) || value == null) { return fallback; }
        try
        {
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            bool parsed;
            if (Boolean.TryParse(text, out parsed)) { return parsed; }
            int numeric;
            if (Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)) { return numeric != 0; }
            return fallback;
        }
    }

    private static string NormalizePolicyNameForConfig(string policy)
    {
        if (String.IsNullOrWhiteSpace(policy)) { return String.Empty; }
        if (String.Equals(policy, "Auto", StringComparison.OrdinalIgnoreCase)) { return "Auto"; }
        if (String.Equals(policy, "Protect", StringComparison.OrdinalIgnoreCase)) { return "Protect"; }
        if (String.Equals(policy, "Light", StringComparison.OrdinalIgnoreCase)) { return "Light"; }
        if (String.Equals(policy, "Balanced", StringComparison.OrdinalIgnoreCase)) { return "Balanced"; }
        if (String.Equals(policy, "Deep", StringComparison.OrdinalIgnoreCase)) { return "Deep"; }
        return String.Empty;
    }
    private static string NormalizeSessionMode(string mode)
    {
        string value = String.IsNullOrWhiteSpace(mode) ? "Auto" : mode.Trim();
        if (String.Equals(value, "Gaming", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Game", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Jogos", StringComparison.OrdinalIgnoreCase)) { return "Gaming"; }
        if (String.Equals(value, "Competitive", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Competitivo", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Ranked", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Rankeado", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "PvP", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Versus", StringComparison.OrdinalIgnoreCase)) { return "Competitive"; }
        if (String.Equals(value, "Work", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Trabalho", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Creator", StringComparison.OrdinalIgnoreCase)) { return "Work"; }
        if (String.Equals(value, "Focus", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Foco", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "DeepFocus", StringComparison.OrdinalIgnoreCase)) { return "Focus"; }
        if (String.Equals(value, "Streamer", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Stream", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Live", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Transmissao", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "Transmiss\u00e3o", StringComparison.OrdinalIgnoreCase)) { return "Streamer"; }
        return "Auto";
    }

    private static IDictionary<string, object> LoadSmartModeMap()
    {
        IDictionary<string, object> root = LoadConfigRoot();
        object smartObject;
        IDictionary<string, object> smart = root.TryGetValue("SmartMode", out smartObject) ? smartObject as IDictionary<string, object> : null;
        return smart ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetSessionMode()
    {
        try
        {
            IDictionary<string, object> smart = LoadSmartModeMap();
            return NormalizeSessionMode(GetMapString(smart, "SessionMode"));
        }
        catch
        {
            return "Auto";
        }
    }

    private static PowerPlanSnapshot GetRecommendedPowerPlanForSessionMode(string mode)
    {
        string normalized = NormalizeSessionMode(mode);
        if (String.Equals(normalized, "Gaming", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(normalized, "Competitive", StringComparison.OrdinalIgnoreCase))
        {
            return new PowerPlanSnapshot { Guid = SmartNapGamePowerPlanGuid, Name = SmartNapGamePowerPlanName };
        }
        if (String.Equals(normalized, "Streamer", StringComparison.OrdinalIgnoreCase))
        {
            return new PowerPlanSnapshot { Guid = SmartNapLivePowerPlanGuid, Name = SmartNapLivePowerPlanName };
        }
        return null;
    }

    private static bool IsAdaptiveExclusionsEnabled()
    {
        try
        {
            IDictionary<string, object> smart = LoadSmartModeMap();
            return GetMapBool(smart, "AdaptiveExclusionsEnabled", true);
        }
        catch
        {
            return true;
        }
    }

    private static RunResult WriteSmartModeValue(string key, object value)
    {
        try
        {
            IDictionary<string, object> root = LoadConfigRoot();
            IDictionary<string, object> smart = GetOrCreateMap(root, "SmartMode");
            smart[key] = value;
            Directory.CreateDirectory(appRoot);
            AtomicWriteJsonMap(userConfigPath, root);
            return new RunResult(0, "Saved.");
        }
        catch (Exception ex)
        {
            return new RunResult(1, ex.Message);
        }
    }

    private static RunResult SetSessionMode(string mode)
    {
        return SetSessionMode(mode, "keep");
    }

    private static RunResult SetSessionMode(string mode, string energyChoice)
    {
        string normalized = NormalizeSessionMode(mode);
        RunResult result = WriteSmartModeValue("SessionMode", normalized);
        if (result.ExitCode != 0) { return result; }

        RunResult energy = ApplyEnergyChoiceForMode(normalized, energyChoice);
        string choice = NormalizeEnergyChoice(energyChoice);
        AppendOperationalLog("action=session-mode mode=" + normalized + " energy=" + choice + " energyResult=" + (energy.ExitCode == 0 ? "OK" : "FAIL"));
        if (energy.ExitCode != 0) { return energy; }
        string detail = String.IsNullOrWhiteSpace(energy.Output) ? "" : " " + energy.Output;
        return new RunResult(0, "Session mode: " + normalized + "." + detail);
    }

    private static RunResult SetAdaptiveExclusionsEnabled(bool enabled)
    {
        RunResult result = WriteSmartModeValue("AdaptiveExclusionsEnabled", enabled);
        if (result.ExitCode == 0) { AppendOperationalLog("action=adaptive-exclusions enabled=" + enabled.ToString().ToLowerInvariant()); }
        return result.ExitCode == 0 ? new RunResult(0, enabled ? "Adaptive exclusions enabled." : "Adaptive exclusions disabled.") : result;
    }

    private static bool IsNetworkUdpGuardEnabled()
    {
        try
        {
            IDictionary<string, object> smart = LoadSmartModeMap();
            object value;
            if (smart != null && smart.TryGetValue("NetworkUdpGuardEnabled", out value))
            {
                return GetMapBool(smart, "NetworkUdpGuardEnabled", false);
            }
            return ReadUiFlag("NetworkUdpGuardEnabledMirror");
        }
        catch
        {
            return ReadUiFlag("NetworkUdpGuardEnabledMirror");
        }
    }

    private static RunResult SetNetworkUdpGuardEnabled(bool enabled)
    {
        RunResult result = WriteSmartModeValue("NetworkUdpGuardEnabled", enabled);
        if (result.ExitCode != 0) { return result; }
        SaveUiFlag("NetworkUdpGuardEnabledMirror", enabled);

        AppendOperationalLog("action=udp-guard enabled=" + enabled.ToString().ToLowerInvariant() + " apply=queued");
        RunResult apply = RunApplyNow();
        if (apply.ExitCode != 0)
        {
            AppendOperationalLog("action=udp-guard enabled=" + enabled.ToString().ToLowerInvariant() + " apply=failed exitCode=" + apply.ExitCode.ToString(CultureInfo.InvariantCulture));
            return apply;
        }

        AppendOperationalLog("action=udp-guard enabled=" + enabled.ToString().ToLowerInvariant() + " apply=OK");
        return new RunResult(0, enabled ? "Zero Ping enabled and applied." : "Zero Ping disabled and cleaned.");
    }

    private static int CountManualPolicies()
    {
        try
        {
            if (String.IsNullOrWhiteSpace(appPolicyPath) || !File.Exists(appPolicyPath)) { return 0; }
            IDictionary<string, object> root = LoadJsonMapWithRecovery(appPolicyPath);
            object existingItems = null;
            System.Collections.IEnumerable enumerable = root != null && root.TryGetValue("Items", out existingItems) ? existingItems as System.Collections.IEnumerable : null;
            if (enumerable == null || existingItems is string) { return 0; }
            HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object item in enumerable)
            {
                IDictionary<string, object> map = item as IDictionary<string, object>;
                if (map == null) { continue; }
                string policy = NormalizePolicyNameForConfig(GetMapString(map, "Policy"));
                if (String.IsNullOrWhiteSpace(policy) || String.Equals(policy, "Auto", StringComparison.OrdinalIgnoreCase)) { continue; }
                string label = GetMapString(map, "Path");
                if (String.IsNullOrWhiteSpace(label)) { label = GetMapString(map, "ProcessName"); }
                if (String.IsNullOrWhiteSpace(label)) { label = GetMapString(map, "Key"); }
                if (!String.IsNullOrWhiteSpace(label)) { unique.Add(label.Trim().ToLowerInvariant()); }
            }
            return unique.Count;
        }
        catch
        {
            return 0;
        }
    }

    private static RunResult ClearAppPolicies()
    {
        try
        {
            Directory.CreateDirectory(outputsPath);
            Dictionary<string, object> output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            output["Timestamp"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            output["Items"] = new List<Dictionary<string, object>>();
            AtomicWriteJsonMap(appPolicyPath, output);
            AppendOperationalLog("action=policy-clear status=done");
            return new RunResult(0, "Policies cleared.");
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

    private static bool IsBehaviorEngineEnabled()
    {
        try
        {
            IDictionary<string, object> root = LoadConfigRoot();
            object smartObject;
            IDictionary<string, object> smart = root.TryGetValue("SmartMode", out smartObject) ? smartObject as IDictionary<string, object> : null;
            object value;
            if (smart == null || !smart.TryGetValue("BehaviorEngine", out value)) { return true; }
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return true;
        }
    }

    private static int GetBehaviorProfileCount()
    {
        try
        {
            string path = Path.Combine(outputsPath, "background-nap-behavior-latest.json");
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

    private static string GetManagedExecutableTargetPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "SmartBackgroundNap",
            "SmartBackgroundNap.exe");
    }

    private static string GetStartMenuProgramsPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
    }

    private static string GetPrimaryStartMenuShortcutPath()
    {
        return Path.Combine(GetStartMenuProgramsPath(), "Smart Nap.lnk");
    }

    private static string[] GetStartMenuShortcutCandidates()
    {
        string root = GetStartMenuProgramsPath();
        return new string[]
        {
            Path.Combine(root, "Smart Nap.lnk"),
            Path.Combine(root, "Smart Background Nap.lnk"),
            Path.Combine(root, "SmartBackgroundNap.lnk")
        };
    }

    private static void NotifyShellIconChanged()
    {
        try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); }
        catch { }
    }

    private static RunResult InstallStartMenuShortcuts()
    {
        try
        {
            string target = GetLaunchExecutablePath();
            string icon = File.Exists(iconPath) ? iconPath : target;
            string primary = GetPrimaryStartMenuShortcutPath();
            CreateStartMenuShortcut(primary, target, icon);

            foreach (string candidate in GetStartMenuShortcutCandidates())
            {
                try
                {
                    if (!String.Equals(candidate, primary, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
                    {
                        File.Delete(candidate);
                    }
                }
                catch (Exception ex)
                {
                    AppendOperationalLog("action=start-menu-shortcut-legacy-remove status=deferred file=" + SanitizeLogToken(Path.GetFileName(candidate)) + " detail=" + ShortTaskError(ex.Message));
                }
            }
            NotifyShellIconChanged();
            return new RunResult(0, "Start menu shortcut updated.");
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=start-menu-shortcut status=failed detail=" + ShortTaskError(ex.Message));
            return new RunResult(1, "Could not update Start menu shortcut: " + ex.Message);
        }
    }

    private static RunResult RemoveStartMenuShortcuts()
    {
        try
        {
            foreach (string shortcutPath in GetStartMenuShortcutCandidates())
            {
                try
                {
                    if (File.Exists(shortcutPath)) { File.Delete(shortcutPath); }
                }
                catch (Exception ex)
                {
                    AppendOperationalLog("action=start-menu-shortcut-remove status=deferred file=" + SanitizeLogToken(Path.GetFileName(shortcutPath)) + " detail=" + ShortTaskError(ex.Message));
                }
            }
            NotifyShellIconChanged();
            return new RunResult(0, "Start menu shortcut removed.");
        }
        catch (Exception ex)
        {
            return new RunResult(1, "Could not remove Start menu shortcut: " + ex.Message);
        }
    }

    private static void AddUniquePath(List<string> paths, string path)
    {
        if (String.IsNullOrWhiteSpace(path)) { return; }
        foreach (string existing in paths)
        {
            if (String.Equals(existing, path, StringComparison.OrdinalIgnoreCase)) { return; }
        }
        paths.Add(path);
    }

    private static void CreateStartMenuShortcut(string shortcutPath, string target, string icon)
    {
        string dir = Path.GetDirectoryName(shortcutPath);
        if (!String.IsNullOrWhiteSpace(dir)) { Directory.CreateDirectory(dir); }

        object shell = null;
        object shortcut = null;
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) { throw new InvalidOperationException("WScript.Shell is unavailable."); }
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { "" });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(target) ?? "" });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { icon + ",0" });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Smart Nap background optimizer" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            try { if (shortcut != null && Marshal.IsComObject(shortcut)) { Marshal.FinalReleaseComObject(shortcut); } } catch { }
            try { if (shell != null && Marshal.IsComObject(shell)) { Marshal.FinalReleaseComObject(shell); } } catch { }
        }
    }

    private static string GetLaunchExecutablePath()
    {
        if (usingLooseRuntime)
        {
            return Application.ExecutablePath;
        }

        string target = GetManagedExecutableTargetPath();
        try
        {
            string installDir = Path.GetDirectoryName(target);
            if (!String.IsNullOrWhiteSpace(installDir)) { Directory.CreateDirectory(installDir); }

            string current = Application.ExecutablePath;
            if (!String.Equals(Path.GetFullPath(current), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                if (FilesAlreadyMatch(current, target))
                {
                    return target;
                }

                bool restartCoreService = IsCoreServiceRunning();
                if (restartCoreService)
                {
                    RunResult stopService = StopCoreService(false);
                    if (stopService.ExitCode != 0)
                    {
                        AppendOperationalLog("action=managed-copy-core-service-stop status=deferred detail=" + ShortTaskError(stopService.Output));
                    }
                    WaitForCoreServiceRunningState(false, 12000);
                }

                StopSmartNapProcessesForUpdate(target, Process.GetCurrentProcess().Id);
                CopyFileWithRetries(current, target, 20, 250);
                if (restartCoreService)
                {
                    RunResult startService = StartCoreService(false);
                    AppendOperationalLog("action=managed-copy-core-service-restart exitCode=" + startService.ExitCode.ToString(CultureInfo.InvariantCulture) + " detail=" + ShortTaskError(startService.Output));
                }
            }
            return target;
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=managed-copy status=failed detail=" + ShortTaskError(ex.Message));
            try
            {
                if (File.Exists(target)) { return target; }
            }
            catch
            {
            }
            return Application.ExecutablePath;
        }
    }

    private static bool WaitForCoreServiceRunningState(bool running, int timeoutMs)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(1000, timeoutMs));
        do
        {
            if (IsCoreServiceRunning() == running)
            {
                return true;
            }
            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);
        return IsCoreServiceRunning() == running;
    }

    private static bool FilesAlreadyMatch(string left, string right)
    {
        try
        {
            if (String.IsNullOrWhiteSpace(left) || String.IsNullOrWhiteSpace(right)) { return false; }
            FileInfo leftInfo = new FileInfo(left);
            FileInfo rightInfo = new FileInfo(right);
            if (!leftInfo.Exists || !rightInfo.Exists) { return false; }
            if (leftInfo.Length != rightInfo.Length) { return false; }
            using (FileStream leftStream = File.Open(left, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (FileStream rightStream = File.Open(right, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] leftHash = sha.ComputeHash(leftStream);
                byte[] rightHash = sha.ComputeHash(rightStream);
                if (leftHash.Length != rightHash.Length) { return false; }
                for (int i = 0; i < leftHash.Length; i++)
                {
                    if (leftHash[i] != rightHash[i]) { return false; }
                }
                return true;
            }
        }
        catch
        {
            return false;
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
        report.AppendLine(BuildTaskStatusLine(DashboardTaskName));
        report.AppendLine(BuildTaskStatusLine(SessionAgentTaskName));
        report.AppendLine("Startup method: per-user scheduled tasks after user-approved setup; Session Agent uses InteractiveToken + LeastPrivilege; HKCU Run fallback is used only if tray startup task setup is blocked.");
        report.AppendLine("Core service: " + GetCoreServiceStatusText() + ".");
        report.AppendLine("Core service state file: " + coreServiceStatePath);
        report.AppendLine("Session Agent state file: " + sessionAgentStatePath);
        report.AppendLine("Driver installed: no.");
        report.AppendLine("Startup registry key: no.");
        report.AppendLine();
        report.AppendLine("Data and network posture");
        report.AppendLine("Network access: optional official GitHub Releases check for update notifications; no telemetry or user data upload.");
        report.AppendLine("Telemetry: none.");
        report.AppendLine("Accounts, passwords, cookies, browser profiles, documents, and game files: not read.");
        report.AppendLine("Local files written: config, compact logs, restore snapshots, embedded runtime files, this report.");
        report.AppendLine();
        report.AppendLine("Optimization scope");
        report.AppendLine("Allowed actions: process priority, memory priority, process I/O priority, Windows power throttling/EcoQoS, timer-resolution isolation, foreground wake restore, temporary active-app protection, burst scoring, fullscreen-aware thresholds, optional local Smart Learning profiles, optional working-set trimming.");
        report.AppendLine("Skipped targets: Windows/system processes, session 0 services, foreground app, high-CPU active workloads, configured protected apps, configured protected paths.");
        report.AppendLine("Destructive actions: none. It does not kill apps, delete files, change drivers, force power-plan changes, overclock, undervolt, or disable Windows services. Optional Smart Nap power profiles require user confirmation.");
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
            OpenExternal(scorePath);
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

    private static void OpenLatestRelease()
    {
        OpenExternal(GitHubUrl + "/releases/latest");
    }

    private static void OpenLatestDownload()
    {
        RunResult result = StartSelfUpdate(GitHubLatestDownloadUrl, "", "");
        if (result.ExitCode != 0)
        {
            MessageBox.Show(result.Output, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static RunResult StartSelfUpdate(string downloadUrl, string latestTag, string releaseBody)
    {
        try
        {
            if (String.IsNullOrWhiteSpace(downloadUrl)) { downloadUrl = GitHubLatestDownloadUrl; }
            Uri uri;
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out uri) || !String.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                return new RunResult(1, "O link da atualizacao nao parece ser oficial. Abra o GitHub oficial e baixe manualmente.");
            }
            if (!IsOfficialUpdateHost(uri.Host))
            {
                return new RunResult(1, "A atualizacao precisa vir do GitHub oficial do Smart Nap.");
            }

            string updateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartBackgroundNap", "updates");
            Directory.CreateDirectory(updateDir);
            string label = SanitizeFileNameSegment(String.IsNullOrWhiteSpace(latestTag) ? "latest" : latestTag);
            string downloadedPath = Path.Combine(updateDir, "SmartBackgroundNap-" + label + ".exe");

            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(3);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SmartBackgroundNap/" + AppVersion);
                byte[] bytes = client.GetByteArrayAsync(uri).GetAwaiter().GetResult();
                if (bytes == null || bytes.Length < 256 * 1024)
                {
                    return new RunResult(1, "O download da atualizacao veio incompleto. Tente novamente em alguns segundos.");
                }
                File.WriteAllBytes(downloadedPath, bytes);
            }

            RunResult validation = ValidateDownloadedUpdateExecutable(downloadedPath, latestTag);
            if (validation.ExitCode != 0)
            {
                try { File.Delete(downloadedPath); } catch { }
                return validation;
            }

            string targetPath = GetLaunchExecutablePath();
            if (String.IsNullOrWhiteSpace(targetPath) || !File.Exists(downloadedPath))
            {
                return new RunResult(1, "Nao consegui preparar o arquivo de atualizacao.");
            }

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = downloadedPath;
            start.Arguments = "--complete-update --update-source " + Quote(downloadedPath) + " --update-target " + Quote(targetPath) + " --wait-pid " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) + " --launch-after";
            start.UseShellExecute = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(start);
            AppendOperationalLog("action=self-update status=helper-started tag=" + SanitizeLogToken(latestTag));
            ScheduleProcessExitForUpdate();
            return new RunResult(0, "Atualizador iniciado. O Smart Nap vai reiniciar automaticamente.");
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
            return new RunResult(1, "Nao consegui baixar ou preparar a atualizacao agora.");
        }
    }

    private static RunResult ValidateDownloadedUpdateExecutable(string path, string expectedTag)
    {
        try
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new RunResult(1, "O arquivo de atualizacao nao foi encontrado.");
            }
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Length < 256 * 1024) { return new RunResult(1, "O arquivo de atualizacao parece incompleto."); }
                if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z') { return new RunResult(1, "O arquivo baixado nao parece ser um executavel do Windows."); }
                stream.Seek(0x3c, SeekOrigin.Begin);
                byte[] offsetBytes = new byte[4];
                if (stream.Read(offsetBytes, 0, offsetBytes.Length) != offsetBytes.Length) { return new RunResult(1, "Nao consegui validar o cabecalho da atualizacao."); }
                int peOffset = BitConverter.ToInt32(offsetBytes, 0);
                if (peOffset <= 0 || peOffset > stream.Length - 4) { return new RunResult(1, "O executavel da atualizacao esta com cabecalho invalido."); }
                stream.Seek(peOffset, SeekOrigin.Begin);
                if (stream.ReadByte() != 'P' || stream.ReadByte() != 'E' || stream.ReadByte() != 0 || stream.ReadByte() != 0)
                {
                    return new RunResult(1, "O arquivo baixado nao passou na validacao PE.");
                }
            }

            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(path);
            string fileVersion = FirstNonEmpty(versionInfo.ProductVersion, versionInfo.FileVersion);
            if (String.IsNullOrWhiteSpace(fileVersion))
            {
                return new RunResult(1, "Nao consegui validar a versao do executavel baixado.");
            }

            if (!String.IsNullOrWhiteSpace(expectedTag) && !String.Equals(NormalizeVersionLabel(expectedTag), NormalizeVersionLabel(fileVersion), StringComparison.OrdinalIgnoreCase))
            {
                AppendOperationalLog("action=self-update-validation status=tag-version-mismatch tag=" + SanitizeLogToken(expectedTag) + " fileVersion=" + SanitizeLogToken(fileVersion));
            }

            if (!IsRemoteVersionNewer(fileVersion, AppVersion) && !String.Equals(NormalizeVersionLabel(fileVersion), NormalizeVersionLabel(AppVersion), StringComparison.OrdinalIgnoreCase))
            {
                return new RunResult(1, "A atualizacao baixada nao parece ser mais nova que a versao instalada.");
            }

            AppendOperationalLog("action=self-update-validation status=ok version=" + SanitizeLogToken(fileVersion) + " sha256=" + ComputeFileSha256(path));
            return new RunResult(0, "Update executable validated.");
        }
        catch (Exception ex)
        {
            AppendOperationalLog("action=self-update-validation status=failed detail=" + ShortTaskError(ex.Message));
            return new RunResult(1, "Nao consegui validar a atualizacao baixada.");
        }
    }
    private static bool IsOfficialUpdateHost(string host)
    {
        if (String.IsNullOrWhiteSpace(host)) { return false; }
        host = host.Trim().ToLowerInvariant();
        return String.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
            String.Equals(host, "objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileNameSegment(string value)
    {
        if (String.IsNullOrWhiteSpace(value)) { return "latest"; }
        string safe = Regex.Replace(value.Trim(), @"[^A-Za-z0-9._-]+", "-").Trim('-');
        return String.IsNullOrWhiteSpace(safe) ? "latest" : safe;
    }

    private static string SanitizeLogToken(string value)
    {
        if (String.IsNullOrWhiteSpace(value)) { return "unknown"; }
        return Regex.Replace(value.Trim(), @"\s+", "_");
    }

    private static void ScheduleProcessExitForUpdate()
    {
        ThreadPool.QueueUserWorkItem(delegate
        {
            Thread.Sleep(900);
            try { Application.Exit(); } catch { }
            Thread.Sleep(1800);
            try { Environment.Exit(0); } catch { }
        });
    }

    private static RunResult CompleteSelfUpdate(string[] args)
    {
        string source = GetArgValue(args, "--update-source");
        string target = GetArgValue(args, "--update-target");
        int waitPid;
        Int32.TryParse(GetArgValue(args, "--wait-pid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out waitPid);

        if (String.IsNullOrWhiteSpace(source) || String.IsNullOrWhiteSpace(target))
        {
            return new RunResult(1, "Missing update source or target.");
        }

        try
        {
            WaitForProcessExit(waitPid, 70000);
            StopSmartNapProcessesForUpdate(target, Process.GetCurrentProcess().Id);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            CopyFileWithRetries(source, target, 20, 450);

            RunHidden(target, "--install", 120000);
            if (HasArg(args, "--launch-after"))
            {
                ProcessStartInfo launch = new ProcessStartInfo();
                launch.FileName = target;
                launch.UseShellExecute = true;
                Process.Start(launch);
            }
            AppendOperationalLog("action=self-update status=completed target=" + SanitizeLogToken(target));
            return new RunResult(0, "Update completed.");
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
            return new RunResult(1, ex.Message);
        }
    }

    private static void WaitForProcessExit(int pid, int timeoutMs)
    {
        if (pid <= 0) { return; }
        try
        {
            using (Process process = Process.GetProcessById(pid))
            {
                process.WaitForExit(timeoutMs);
            }
        }
        catch
        {
        }
    }

    private static void StopSmartNapProcessesForUpdate(string targetPath, int currentPid)
    {
        string target = SafeFullPath(targetPath);
        foreach (Process process in Process.GetProcessesByName("SmartBackgroundNap"))
        {
            try
            {
                if (process.Id == currentPid) { continue; }
                string processPath = SafeFullPath(TryGetProcessPath(process));
                if (!String.IsNullOrWhiteSpace(target) && String.IsNullOrWhiteSpace(processPath)) { continue; }
                if (!String.IsNullOrWhiteSpace(target) && !String.IsNullOrWhiteSpace(processPath) && !String.Equals(target, processPath, StringComparison.OrdinalIgnoreCase)) { continue; }
                process.CloseMainWindow();
                if (!process.WaitForExit(2500)) { process.Kill(); }
            }
            catch
            {
            }
            finally
            {
                try { process.Dispose(); } catch { }
            }
        }
    }

    private static string TryGetProcessPath(Process process)
    {
        try { return process.MainModule == null ? "" : process.MainModule.FileName; }
        catch { return ""; }
    }

    private static string SafeFullPath(string path)
    {
        if (String.IsNullOrWhiteSpace(path)) { return ""; }
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }

    private static void CopyFileWithRetries(string source, string target, int attempts, int delayMs)
    {
        Exception last = null;
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                File.Copy(source, target, true);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                Thread.Sleep(delayMs);
            }
        }
        throw last ?? new IOException("Could not replace Smart Nap executable.");
    }
    private static ReleaseUpdateInfo CheckForOfficialUpdate()
    {
        ReleaseUpdateInfo info = new ReleaseUpdateInfo();
        info.Checked = true;
        info.LatestTag = "";
        info.LatestVersion = "";
        info.ReleaseUrl = GitHubUrl + "/releases/latest";
        info.DownloadUrl = GitHubLatestDownloadUrl;

        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(7);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SmartBackgroundNap/" + AppVersion);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                string json = client.GetStringAsync(GitHubLatestReleaseApi).GetAwaiter().GetResult();
                IDictionary<string, object> root = JsonCompat.DeserializeObject(json);
                if (root == null) { throw new InvalidOperationException("Invalid GitHub release response."); }

                info.LatestTag = ReadMapString(root, "tag_name");
                info.LatestVersion = NormalizeVersionLabel(info.LatestTag);
                info.ReleaseName = ReadMapString(root, "name");
                info.ReleaseUrl = FirstNonEmpty(ReadMapString(root, "html_url"), GitHubUrl + "/releases/latest");
                info.PublishedAt = ReadMapString(root, "published_at");
                info.ReleaseBody = ReadMapString(root, "body");
                info.DownloadUrl = FirstNonEmpty(FindReleaseAssetUrl(root, "SmartBackgroundNap.exe"), GitHubLatestDownloadUrl);

                bool newer = IsRemoteVersionNewer(info.LatestTag, AppVersion);
                bool dismissed = String.Equals(LoadDismissedUpdateTag(), info.LatestTag, StringComparison.OrdinalIgnoreCase);
                info.Ignored = newer && dismissed;
                info.Available = newer && !dismissed;
            }
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
            info.Available = false;
        }

        return info;
    }

    private static string FindReleaseAssetUrl(IDictionary<string, object> root, string assetName)
    {
        object assetsObject;
        if (root == null || !root.TryGetValue("assets", out assetsObject)) { return ""; }
        System.Collections.IEnumerable assets = assetsObject as System.Collections.IEnumerable;
        if (assets == null || assetsObject is string) { return ""; }
        foreach (object item in assets)
        {
            IDictionary<string, object> map = item as IDictionary<string, object>;
            if (map == null) { continue; }
            if (String.Equals(ReadMapString(map, "name"), assetName, StringComparison.OrdinalIgnoreCase))
            {
                return ReadMapString(map, "browser_download_url");
            }
        }
        return "";
    }

    private static string FirstNonEmpty(string first, string fallback)
    {
        return String.IsNullOrWhiteSpace(first) ? fallback : first;
    }

    private static string NormalizeVersionLabel(string value)
    {
        if (String.IsNullOrWhiteSpace(value)) { return ""; }
        return value.Trim().TrimStart('v', 'V');
    }

    private static bool IsRemoteVersionNewer(string remoteTag, string currentVersion)
    {
        int[] remote = ParseVersionParts(remoteTag);
        int[] current = ParseVersionParts(currentVersion);
        for (int i = 0; i < 3; i++)
        {
            if (remote[i] > current[i]) { return true; }
            if (remote[i] < current[i]) { return false; }
        }
        return false;
    }

    private static int[] ParseVersionParts(string value)
    {
        int[] parts = new int[] { 0, 0, 0 };
        if (String.IsNullOrWhiteSpace(value)) { return parts; }
        string clean = NormalizeVersionLabel(value);
        string[] tokens = clean.Split(new char[] { '.', '-', '+', '_' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < Math.Min(3, tokens.Length); i++)
        {
            int parsed;
            parts[i] = Int32.TryParse(tokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
        }
        return parts;
    }

    private sealed class ReleaseUpdateInfo
    {
        public bool Checked { get; set; }
        public bool Checking { get; set; }
        public bool Available { get; set; }
        public bool Ignored { get; set; }
        public string LatestTag { get; set; }
        public string LatestVersion { get; set; }
        public string ReleaseName { get; set; }
        public string ReleaseUrl { get; set; }
        public string DownloadUrl { get; set; }
        public string PublishedAt { get; set; }
        public string ReleaseBody { get; set; }
        public string Error { get; set; }

        public static ReleaseUpdateInfo Idle()
        {
            ReleaseUpdateInfo info = new ReleaseUpdateInfo();
            info.ReleaseUrl = GitHubUrl + "/releases/latest";
            info.DownloadUrl = GitHubLatestDownloadUrl;
            return info;
        }

        public static ReleaseUpdateInfo InProgress()
        {
            ReleaseUpdateInfo info = Idle();
            info.Checking = true;
            return info;
        }
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
        #if !NET9_0_OR_GREATER
        private bool allowExit;
        #endif
        private bool listenerStopping;
        private Thread showThread;
        private System.Windows.Forms.Timer foregroundWakeTimer;
        private System.Windows.Forms.Timer processStartRadarTimer;
        private System.Windows.Forms.Timer trayTelemetryTimer;
        private System.Windows.Forms.Timer energyIdleGuardTimer;
        private System.Windows.Forms.Timer localAutoEngineTimer;
        private bool localAutoEngineBusy;
        private DateTime lastLocalAutoEngineRunAt = DateTime.MinValue;
        private DateTime lastTrayTelemetryRefreshAt = DateTime.MinValue;
        private string lastTrayTelemetryText = "";
        private int lastForegroundPid;
        private bool foregroundRestoreBusy;
        private bool processStartRadarBusy;
        private readonly HashSet<int> processStartRadarSeenPids = new HashSet<int>();
        private DateTime lastForegroundRestoreAt = DateTime.MinValue;
        private bool reactiveApplyBusy;
        private DateTime lastReactiveApplyAt = DateTime.MinValue;
        private DateTime lastGameRadarSweepAt = DateTime.MinValue;
        private int lastReactiveApplyPid;
        private string lastReactiveApplyReason = "";

        public SmartNapContext(bool trayOnly)
        {
            uiContext = SynchronizationContext.Current;
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = LoadIcon();
            notifyIcon.Text = AppName + ": active";
            notifyIcon.Visible = true;
            ContextMenuStrip trayMenu = BuildMenu();
            trayMenu.Opening += delegate { UpdateTrayTelemetryText(true); };
            notifyIcon.ContextMenuStrip = trayMenu;
            notifyIcon.DoubleClick += delegate { ShowMainWindow(); };
            notifyIcon.MouseMove += delegate { RefreshTrayTelemetryFromHover(); };
            notifyIcon.MouseClick += delegate { UpdateTrayTelemetryText(true); };

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
                    uiContext.Post(delegate {  }, null);
                }
            });
#else
            mainWindow = CreateMainWindow();
#endif

            if (!trayOnly)
            {
                ShowMainWindow();
            }

            StartShowListener();
            PrimeProcessStartRadar();
            StartForegroundWakeTimer();
            StartProcessStartRadarTimer();
            StartTrayTelemetryTimer();
            StartEnergyIdleGuardTimer();
            StartLocalAutoEngineTimer();
        }

        #if !NET9_0_OR_GREATER
        private ModernMainWindow CreateMainWindow()
        {
            ModernMainWindow window = new ModernMainWindow();
            window.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (!allowExit)
                {
                    e.Cancel = true;
                    window.Hide();
                }
            };
            return window;
        }
        #endif

        private const int TrayMenuWidth = 430;
        private const int TrayItemWidth = 392;
        private const int TraySubItemWidth = 326;
        private const string TrayActiveTag = "smartnap-tray-active";

        private static Color TrayBg { get { return Color.FromArgb(9, 9, 11); } }
        private static Color TrayPanel { get { return Color.FromArgb(12, 20, 33); } }
        private static Color TrayPanelAlt { get { return Color.FromArgb(15, 27, 45); } }
        private static Color TrayText { get { return Color.FromArgb(238, 244, 252); } }
        private static Color TrayMuted { get { return Color.FromArgb(148, 164, 188); } }
        private static Color AccentOrange { get { return Color.FromArgb(255, 173, 47); } }
        private static Color AccentGreen { get { return Color.FromArgb(53, 232, 154); } }
        private static Color AccentBlue { get { return Color.FromArgb(84, 162, 255); } }
        private static Color AccentCyan { get { return Color.FromArgb(61, 220, 255); } }
        private static Color AccentViolet { get { return Color.FromArgb(167, 139, 250); } }
        private static Color AccentRose { get { return Color.FromArgb(255, 93, 120); } }
        private static Color AccentNeutral { get { return Color.FromArgb(137, 156, 184); } }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.AutoClose = true;
            menu.AutoSize = true;
            menu.Padding = new Padding(4, 5, 4, 5);
            menu.Margin = Padding.Empty;
            menu.BackColor = TrayBg;
            menu.ForeColor = TrayText;
            menu.Renderer = new SmartNapCompactTrayRenderer();
            PopulateTrayMenu(menu);
            menu.Opening += delegate
            {
                PopulateTrayMenu(menu);
                UpdateTrayTelemetryText(true);
            };
            return menu;
        }

        private void PopulateTrayMenu(ContextMenuStrip menu)
        {
            menu.SuspendLayout();
            menu.Items.Clear();
            string mode = GetSessionMode();
            bool motorOn = IsAutomaticEngineEnabled();
            bool startupOn = IsStartupInstalled();
            bool learningOn = IsSmartLearningEnabled();
            bool zeroPingOn = IsNetworkUdpGuardEnabled();
            bool updateAutoOn = LoadAutoUpdateChecks();
            PowerPlanSnapshot plan = GetActivePowerPlan();
            string planName = plan == null || String.IsNullOrWhiteSpace(plan.Name) ? "Plano nao identificado" : plan.Name;

            AddPlainTrayLabel(menu, "Smart Nap", true);
            AddPlainTrayLabel(menu, (motorOn ? "Motor ativo" : "Motor pausado") + " | " + DisplaySessionMode(mode), false);
            AddPlainTrayLabel(menu, BuildCompactTrayTelemetryText(), false);
            AddPlainTrayLabel(menu, "Energia: " + TrimTrayText(planName, 42), false);
            menu.Items.Add(new ToolStripSeparator());

            AddPlainTrayItem(menu, "Abrir painel", false, delegate { ShowMainWindow(); });
            AddPlainTrayItem(menu, "Otimizar agora", false, delegate { RunFromTray("Otimizar agora", RunApplyNow); });
            AddPlainTrayItem(menu, motorOn ? "Pausar motor" : "Retomar motor", motorOn, delegate { RunFromTray(motorOn ? "Motor pausado" : "Motor iniciado", motorOn ? (Func<RunResult>)UninstallAutomatic : InstallAutomatic); });
            menu.Items.Add(new ToolStripSeparator());

            AddPlainTrayLabel(menu, "Modo do motor", false);
            AddPlainModeTrayItem(menu, "Auto", "Auto", mode, "keep");
            AddPlainModeTrayItem(menu, "Jogos", "Gaming", mode, "keep");
            AddPlainModeTrayItem(menu, "Competitivo", "Competitive", mode, "keep");
            AddPlainModeTrayItem(menu, "Live / Streamer", "Streamer", mode, "keep");
            AddPlainModeTrayItem(menu, "Trabalho", "Work", mode, "keep");
            AddPlainModeTrayItem(menu, "Foco", "Focus", mode, "keep");
            menu.Items.Add(new ToolStripSeparator());

            AddPlainTrayLabel(menu, "Recursos", false);
            AddPlainTrayItem(menu, zeroPingOn ? "Zero Ping ligado" : "Zero Ping desligado", zeroPingOn, delegate { RunFromTray(zeroPingOn ? "Zero Ping desligado" : "Zero Ping ligado", delegate { return SetNetworkUdpGuardEnabled(!zeroPingOn); }); });
            AddPlainTrayItem(menu, learningOn ? "Smart Learning ligado" : "Smart Learning desligado", learningOn, delegate { RunFromTray(learningOn ? "Smart Learning desligado" : "Smart Learning ligado", delegate { return SetSmartLearningEnabled(!learningOn); }); });
            AddPlainTrayItem(menu, startupOn ? "Inicia com Windows" : "Não inicia com Windows", startupOn, delegate { RunFromTray(startupOn ? "Inicialização desligada" : "Inicialização ligada", startupOn ? (Func<RunResult>)UninstallStartup : InstallStartup); });
            AddPlainTrayItem(menu, updateAutoOn ? "Updates automáticos ligados" : "Updates automáticos desligados", updateAutoOn, delegate { SaveAutoUpdateChecks(!updateAutoOn); UpdateTrayTelemetryText(true); });
            AddPlainTrayItem(menu, "Baixar atualização oficial", false, delegate { OpenLatestDownload(); });
            menu.Items.Add(new ToolStripSeparator());

            AddPlainTrayItem(menu, "Sair do Smart Nap", false, delegate { ExitFromTray(); });
            menu.ResumeLayout();
        }

        private void AddPlainTrayLabel(ContextMenuStrip menu, string text, bool title)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Enabled = false;
            item.AutoSize = false;
            item.Size = new Size(310, title ? 30 : 24);
            item.Margin = Padding.Empty;
            item.Padding = new Padding(8, 0, 8, 0);
            item.BackColor = TrayBg;
            item.ForeColor = title ? Color.White : TrayMuted;
            item.Font = new Font("Segoe UI Variable Text", title ? 10.4f : 8.4f, title ? FontStyle.Bold : FontStyle.Regular);
            menu.Items.Add(item);
        }

        private ToolStripMenuItem AddPlainTrayItem(ContextMenuStrip menu, string text, bool active, Action action)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Checked = active;
            item.AutoSize = false;
            item.Size = new Size(310, 30);
            item.Margin = Padding.Empty;
            item.Padding = new Padding(8, 0, 8, 0);
            item.BackColor = TrayBg;
            item.ForeColor = active ? Color.White : TrayText;
            item.Font = new Font("Segoe UI Variable Text", 9.1f, active ? FontStyle.Bold : FontStyle.Regular);
            item.Click += delegate { action(); };
            menu.Items.Add(item);
            return item;
        }

        private void AddPlainModeTrayItem(ContextMenuStrip menu, string label, string mode, string currentMode, string energyChoice)
        {
            bool active = String.Equals(NormalizeSessionMode(currentMode), NormalizeSessionMode(mode), StringComparison.OrdinalIgnoreCase);
            AddPlainTrayItem(menu, label, active, delegate
            {
                RunFromTray("Modo " + label, delegate { return SetSessionMode(mode, energyChoice); });
            });
        }

        private void AddTrayHeader(ContextMenuStrip menu, string title, string subtitle, Icon icon)
        {
            TrayHeaderControl control = new TrayHeaderControl(title, subtitle, icon);
            ToolStripControlHost host = new ToolStripControlHost(control);
            host.Margin = new Padding(5, 2, 5, 8);
            host.Padding = Padding.Empty;
            host.AutoSize = false;
            host.Size = control.Size;
            menu.Items.Add(host);
        }

        private void AddTrayInfo(ContextMenuStrip menu, string label, string value, Color accent)
        {
            TrayInfoControl control = new TrayInfoControl(label, value, accent);
            ToolStripControlHost host = new ToolStripControlHost(control);
            host.Margin = new Padding(5, 2, 5, 5);
            host.Padding = Padding.Empty;
            host.AutoSize = false;
            host.Size = control.Size;
            menu.Items.Add(host);
        }

        private void AddTraySeparator(ContextMenuStrip menu)
        {
            ToolStripSeparator separator = new ToolStripSeparator();
            separator.Margin = new Padding(6, 6, 6, 6);
            menu.Items.Add(separator);
        }

        private void AddTraySection(ContextMenuStrip menu, string text)
        {
            TraySectionControl control = new TraySectionControl(text);
            ToolStripControlHost host = new ToolStripControlHost(control);
            host.Margin = new Padding(5, 2, 5, 1);
            host.Padding = Padding.Empty;
            host.AutoSize = false;
            host.Size = control.Size;
            menu.Items.Add(host);
        }

        private ToolStripMenuItem AddTraySubmenu(ContextMenuStrip menu, string text, string description, Color accent, string glyph)
        {
            return AddTraySubmenu(menu.Items, text, description, accent, glyph);
        }

        private ToolStripMenuItem AddTraySubmenu(ToolStripItemCollection items, string text, string description, Color accent, string glyph)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.ToolTipText = description;
            item.Image = CreateTrayGlyph(accent, glyph);
            item.ImageScaling = ToolStripItemImageScaling.None;
            StyleTrayItem(item, false, false, accent, false);
            item.DropDown.BackColor = TrayBg;
            item.DropDown.AutoClose = true;
            PrepareTrayDropDown(item.DropDown);
            item.DropDown.Renderer = new SmartNapTrayRenderer();
            item.DropDownOpening += delegate
            {
                item.DropDown.BackColor = TrayBg;
                item.DropDown.AutoClose = true;
                PrepareTrayDropDown(item.DropDown);
                item.DropDown.Renderer = new SmartNapTrayRenderer();
            };
            items.Add(item);
            return item;
        }

        private static void PrepareTrayDropDown(ToolStripDropDown dropDown)
        {
            if (dropDown == null) { return; }
            dropDown.Padding = new Padding(8, 8, 8, 8);
            dropDown.AutoSize = true;
            dropDown.MinimumSize = new Size(TraySubItemWidth + 56, 0);
            dropDown.MaximumSize = new Size(TraySubItemWidth + 72, 0);
            dropDown.BackColor = TrayBg;
            dropDown.ForeColor = TrayText;
            ToolStripDropDownMenu menu = dropDown as ToolStripDropDownMenu;
            if (menu != null)
            {
                menu.ShowImageMargin = true;
                menu.ShowCheckMargin = false;
                menu.ImageScalingSize = new Size(22, 22);
            }
        }

        private ToolStripMenuItem AddTrayItem(ContextMenuStrip menu, string text, string description, Action action, bool bold, bool isChecked, Color accent, string glyph)
        {
            return AddTrayItem(menu.Items, text, description, action, bold, isChecked, accent, glyph, false);
        }

        private ToolStripMenuItem AddTrayItem(ToolStripMenuItem parent, string text, string description, Action action, bool bold, bool isChecked, Color accent, string glyph)
        {
            return AddTrayItem(parent.DropDownItems, text, description, action, bold, isChecked, accent, glyph, true);
        }

        private ToolStripMenuItem AddTrayCompactItem(ContextMenuStrip menu, string text, string description, Action action, bool isChecked, Color accent, string glyph)
        {
            return AddTrayItem(menu.Items, text, description, action, false, isChecked, accent, glyph, true);
        }

        private ToolStripMenuItem AddTrayItem(ToolStripItemCollection items, string text, string description, Action action, bool bold, bool isChecked, Color accent, string glyph, bool compact)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Checked = false;
            item.Tag = isChecked ? TrayActiveTag : null;
            item.ToolTipText = description;
            item.Image = CreateTrayGlyph(accent, glyph);
            item.ImageScaling = ToolStripItemImageScaling.None;
            item.Font = new Font("Segoe UI Variable Text", compact ? 8.9f : 9.4f, bold ? FontStyle.Bold : FontStyle.Regular);
            item.AutoToolTip = true;
            item.RightToLeft = RightToLeft.No;
            StyleTrayItem(item, bold, isChecked, accent, compact);
            item.Click += delegate { action(); };
            items.Add(item);
            return item;
        }

        private void AddModeTrayItem(ToolStripMenuItem parent, string label, string mode, string currentMode, string tooltip, string energyChoice, Color accent, string glyph)
        {
            ToolStripMenuItem item = AddTrayItem(parent, label, tooltip, delegate
            {
                RunFromTray("Modo " + label, delegate { return SetSessionMode(mode, energyChoice); });
            }, false, String.Equals(NormalizeSessionMode(currentMode), NormalizeSessionMode(mode), StringComparison.OrdinalIgnoreCase), accent, glyph);
            item.ToolTipText = tooltip;
        }

        private void AddModeTrayItem(ContextMenuStrip menu, string label, string mode, string currentMode, string tooltip, string energyChoice, Color accent, string glyph)
        {
            ToolStripMenuItem item = AddTrayCompactItem(menu, label, tooltip, delegate
            {
                RunFromTray("Modo " + label, delegate { return SetSessionMode(mode, energyChoice); });
            }, String.Equals(NormalizeSessionMode(currentMode), NormalizeSessionMode(mode), StringComparison.OrdinalIgnoreCase), accent, glyph);
            item.ToolTipText = tooltip;
        }

        private static bool IsTrayItemActive(ToolStripItem item)
        {
            return item != null && Object.Equals(item.Tag as string, TrayActiveTag);
        }

        private void StyleTrayItem(ToolStripMenuItem item, bool accentText, bool active, Color accent, bool compact)
        {
            item.AutoSize = false;
            item.Size = new Size(TrayItemWidth, compact ? 34 : 42);
            item.Margin = new Padding(0, compact ? 2 : 3, 0, compact ? 2 : 3);
            item.Padding = new Padding(8, 0, 14, 0);
            item.BackColor = TrayBg;
            item.ForeColor = active ? Color.FromArgb(255, 255, 255) : (accentText ? AccentOrange : TrayText);
            item.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            item.TextImageRelation = TextImageRelation.ImageBeforeText;
            item.ImageAlign = ContentAlignment.MiddleLeft;
            item.TextAlign = ContentAlignment.MiddleLeft;
        }

        private static Bitmap CreateTrayGlyph(Color accent, string label)
        {
            int size = 28;
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF rect = new RectangleF(2, 2, size - 4, size - 4);
                using (GraphicsPath path = RoundedRect(rect, 9f))
                using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, size, size), Color.FromArgb(42, accent), Color.FromArgb(16, 24, 38), 135f))
                using (Pen border = new Pen(Color.FromArgb(150, accent), 1.15f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(border, path);
                }

                string key = String.IsNullOrWhiteSpace(label) ? "" : label.Trim().ToUpperInvariant();
                using (Pen line = new Pen(Color.FromArgb(238, 246, 255), 1.9f))
                using (Pen soft = new Pen(Color.FromArgb(220, accent), 2.0f))
                using (Pen thin = new Pen(Color.FromArgb(178, 200, 226), 1.35f))
                using (SolidBrush mark = new SolidBrush(Color.FromArgb(242, 248, 255)))
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(238, accent)))
                {
                    line.StartCap = LineCap.Round;
                    line.EndCap = LineCap.Round;
                    soft.StartCap = LineCap.Round;
                    soft.EndCap = LineCap.Round;
                    thin.StartCap = LineCap.Round;
                    thin.EndCap = LineCap.Round;

                    if (key == "P")
                    {
                        using (GraphicsPath card = RoundedRect(new RectangleF(8, 8, 13, 12), 3f)) { g.DrawPath(line, card); }
                        g.DrawLine(soft, 10, 17, 19, 17);
                    }
                    else if (key == "GO" || key == "J")
                    {
                        PointF[] bolt = new PointF[] { new PointF(15, 5), new PointF(8, 16), new PointF(14, 16), new PointF(11, 23), new PointF(21, 11), new PointF(15, 12) };
                        g.FillPolygon(glow, bolt);
                    }
                    else if (key == "ON")
                    {
                        g.DrawLine(line, 10, 8, 10, 20);
                        g.DrawLine(line, 18, 8, 18, 20);
                    }
                    else if (key == "UP")
                    {
                        g.FillPolygon(mark, new PointF[] { new PointF(11, 8), new PointF(21, 14), new PointF(11, 20) });
                    }
                    else if (key == "MD" || key == "A")
                    {
                        g.DrawArc(soft, 8, 8, 12, 12, 35, 285);
                        g.FillEllipse(glow, 17, 7, 4, 4);
                        g.DrawLine(thin, 9, 14, 14, 14);
                    }
                    else if (key == "FX" || key == "AI")
                    {
                        g.DrawLine(line, 14, 6, 14, 22);
                        g.DrawLine(line, 6, 14, 22, 14);
                        g.DrawLine(soft, 9, 9, 19, 19);
                        g.DrawLine(soft, 19, 9, 9, 19);
                    }
                    else if (key == "PW" || key == "ST")
                    {
                        g.DrawArc(line, 8, 9, 12, 12, 35, 290);
                        g.DrawLine(soft, 14, 6, 14, 14);
                    }
                    else if (key == "TL")
                    {
                        g.DrawLine(line, 8, 10, 20, 10);
                        g.DrawLine(line, 8, 15, 20, 15);
                        g.DrawLine(line, 8, 20, 20, 20);
                        g.FillEllipse(glow, 11, 8, 4, 4);
                        g.FillEllipse(glow, 16, 13, 4, 4);
                    }
                    else if (key == "X")
                    {
                        g.DrawLine(line, 10, 10, 18, 18);
                        g.DrawLine(line, 18, 10, 10, 18);
                    }
                    else if (key == "ZP" || key == "C")
                    {
                        g.DrawEllipse(line, 8, 8, 12, 12);
                        g.DrawLine(soft, 14, 5, 14, 10);
                        g.DrawLine(soft, 14, 18, 14, 23);
                        g.DrawLine(soft, 5, 14, 10, 14);
                        g.DrawLine(soft, 18, 14, 23, 14);
                    }
                    else if (key == "DL")
                    {
                        g.DrawLine(line, 14, 7, 14, 17);
                        g.DrawLine(line, 10, 13, 14, 17);
                        g.DrawLine(line, 18, 13, 14, 17);
                        g.DrawLine(soft, 9, 21, 19, 21);
                    }
                    else if (key == "SC")
                    {
                        g.DrawLine(thin, 9, 20, 9, 15);
                        g.DrawLine(soft, 14, 20, 14, 10);
                        g.DrawLine(line, 19, 20, 19, 13);
                    }
                    else if (key == "LG")
                    {
                        g.DrawLine(line, 9, 9, 19, 9);
                        g.DrawLine(thin, 9, 14, 19, 14);
                        g.DrawLine(thin, 9, 19, 16, 19);
                    }
                    else if (key == "FD")
                    {
                        using (GraphicsPath folder = RoundedRect(new RectangleF(7, 10, 15, 10), 3f)) { g.DrawPath(line, folder); }
                        g.DrawLine(soft, 8, 10, 12, 7);
                        g.DrawLine(soft, 12, 7, 16, 10);
                    }
                    else if (key == "OK")
                    {
                        g.DrawLine(line, 9, 14, 13, 18);
                        g.DrawLine(line, 13, 18, 20, 9);
                    }
                    else if (key == "GH")
                    {
                        g.DrawEllipse(line, 8, 8, 12, 12);
                        g.DrawLine(thin, 12, 18, 12, 22);
                        g.DrawLine(thin, 16, 18, 16, 22);
                    }
                    else if (key == "L")
                    {
                        g.DrawEllipse(soft, 9, 9, 10, 10);
                        g.DrawLine(line, 7, 14, 4, 11);
                        g.DrawLine(line, 21, 14, 24, 11);
                    }
                    else if (key == "T")
                    {
                        using (GraphicsPath bag = RoundedRect(new RectangleF(8, 10, 12, 10), 3f)) { g.DrawPath(line, bag); }
                        g.DrawLine(soft, 11, 10, 11, 8);
                        g.DrawLine(soft, 17, 10, 17, 8);
                    }
                    else if (key == "F")
                    {
                        g.DrawEllipse(line, 8, 8, 12, 12);
                        g.DrawEllipse(soft, 12, 12, 4, 4);
                    }
                    else
                    {
                        g.FillEllipse(glow, 11, 11, 6, 6);
                    }
                }
            }
            return bitmap;
        }

        private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            float d = radius * 2f;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void ConfirmPowerModeFromTray(string mode, string energyChoice, string planName)
        {
            DialogResult confirm = MessageBox.Show(
                "Ativar " + planName + " agora?\n\nEsse perfil mantem o PC em alto desempenho enquanto o modo estiver ativo. Use Auto depois para restaurar ou trocar o plano.",
                AppName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) { return; }
            RunFromTray("Modo " + DisplaySessionMode(mode), delegate { return SetSessionMode(mode, energyChoice); });
        }

        private void ExitFromTray()
        {
#if !NET9_0_OR_GREATER
            allowExit = true;
#endif
            listenerStopping = true;
            if (foregroundWakeTimer != null)
            {
                foregroundWakeTimer.Stop();
                foregroundWakeTimer.Dispose();
                foregroundWakeTimer = null;
            }
            if (processStartRadarTimer != null)
            {
                processStartRadarTimer.Stop();
                processStartRadarTimer.Dispose();
                processStartRadarTimer = null;
            }
            if (trayTelemetryTimer != null)
            {
                trayTelemetryTimer.Stop();
                trayTelemetryTimer.Dispose();
                trayTelemetryTimer = null;
            }
            if (energyIdleGuardTimer != null)
            {
                energyIdleGuardTimer.Stop();
                energyIdleGuardTimer.Dispose();
                energyIdleGuardTimer = null;
            }
            if (localAutoEngineTimer != null)
            {
                localAutoEngineTimer.Stop();
                localAutoEngineTimer.Dispose();
                localAutoEngineTimer = null;
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
        }

        private string DisplaySessionMode(string mode)
        {
            string normalized = NormalizeSessionMode(mode);
            if (String.Equals(normalized, "Gaming", StringComparison.OrdinalIgnoreCase)) { return "Jogos"; }
            if (String.Equals(normalized, "Competitive", StringComparison.OrdinalIgnoreCase)) { return "Competitivo"; }
            if (String.Equals(normalized, "Streamer", StringComparison.OrdinalIgnoreCase)) { return "Live"; }
            if (String.Equals(normalized, "Work", StringComparison.OrdinalIgnoreCase)) { return "Trabalho"; }
            if (String.Equals(normalized, "Focus", StringComparison.OrdinalIgnoreCase)) { return "Foco"; }
            return "Auto";
        }

        private string BuildCompactTrayTelemetryText()
        {
            HardwareSnapshot hardware = GetHardwareSnapshot();
            string line = ReadLastApplyLogLine();
            string targets = ExtractLogField(line, "targets");
            string delta = ExtractLogField(line, "deltaMB");
            if (String.IsNullOrWhiteSpace(targets)) { targets = "0"; }
            if (String.IsNullOrWhiteSpace(delta)) { delta = "0"; }
            string free = hardware != null && hardware.AvailableMemoryMB > 0 ? FormatMemoryBytes((ulong)(hardware.AvailableMemoryMB * 1024.0 * 1024.0)) : "-";
            return "RAM livre " + free + " | " + targets + " apps | " + delta + " MB aliviados";
        }

        private static string TrimTrayText(string text, int maxLength)
        {
            if (String.IsNullOrWhiteSpace(text)) { return "-"; }
            string value = text.Trim();
            return value.Length <= maxLength ? value : value.Substring(0, Math.Max(1, maxLength - 1)) + "...";
        }

        private sealed class TrayHeaderControl : Control
        {
            private readonly string title;
            private readonly string subtitle;
            private readonly Icon icon;

            public TrayHeaderControl(string title, string subtitle, Icon icon)
            {
                this.title = title;
                this.subtitle = subtitle;
                this.icon = icon;
                Size = new Size(TrayItemWidth, 70);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF rect = new RectangleF(1, 1, Width - 2, Height - 2);
                using (GraphicsPath path = RoundedRect(rect, 15f))
                using (LinearGradientBrush fill = new LinearGradientBrush(ClientRectangle, Color.FromArgb(28, 38, 58), Color.FromArgb(8, 14, 24), 135f))
                using (Pen border = new Pen(Color.FromArgb(70, 91, 130), 1f))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }
                using (GraphicsPath glow = RoundedRect(new RectangleF(Width - 118, -50, 142, 142), 71f))
                using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(18, 167, 139, 250)))
                {
                    e.Graphics.FillPath(glowBrush, glow);
                }
                if (icon != null)
                {
                    e.Graphics.DrawIcon(icon, new Rectangle(14, 13, 42, 42));
                }
                using (Font titleFont = new Font("Segoe UI Variable Display", 12.4f, FontStyle.Bold))
                using (Font subFont = new Font("Segoe UI Variable Text", 8.3f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(e.Graphics, title, titleFont, new Rectangle(66, 13, Width - 86, 25), TrayText, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(e.Graphics, subtitle, subFont, new Rectangle(66, 39, Width - 86, 18), AccentGreen, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                }
            }
        }

        private sealed class TrayInfoControl : Control
        {
            private readonly string label;
            private readonly string value;
            private readonly Color accent;

            public TrayInfoControl(string label, string value, Color accent)
            {
                this.label = label;
                this.value = value;
                this.accent = accent;
                Size = new Size(TrayItemWidth, 50);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF rect = new RectangleF(1, 1, Width - 2, Height - 2);
                using (GraphicsPath path = RoundedRect(rect, 12f))
                using (LinearGradientBrush fill = new LinearGradientBrush(ClientRectangle, Color.FromArgb(19, 31, 50), Color.FromArgb(8, 14, 24), 135f))
                using (Pen border = new Pen(Color.FromArgb(56, 75, 108), 1f))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }
                using (GraphicsPath rail = RoundedRect(new RectangleF(8, 11, 4, Height - 22), 2f))
                using (SolidBrush railBrush = new SolidBrush(accent))
                {
                    e.Graphics.FillPath(railBrush, rail);
                }
                using (Font labelFont = new Font("Segoe UI Variable Text", 7.5f, FontStyle.Bold))
                using (Font valueFont = new Font("Segoe UI Variable Text", 9.2f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(e.Graphics, label.ToUpperInvariant(), labelFont, new Rectangle(22, 9, Width - 34, 15), TrayMuted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(e.Graphics, value, valueFont, new Rectangle(22, 27, Width - 34, 18), TrayText, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                }
            }
        }

        private sealed class TraySectionControl : Control
        {
            private readonly string text;

            public TraySectionControl(string text)
            {
                this.text = String.IsNullOrWhiteSpace(text) ? "" : text.Trim();
                Size = new Size(TrayItemWidth, 24);
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle lineRect = new Rectangle(104, Height / 2, Math.Max(24, Width - 118), 1);
                using (LinearGradientBrush line = new LinearGradientBrush(lineRect, Color.FromArgb(0, 84, 162, 255), Color.FromArgb(74, 84, 162, 255), 0f))
                using (Font font = new Font("Segoe UI Variable Text", 7.2f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(e.Graphics, text.ToUpperInvariant(), font, new Rectangle(8, 4, 94, 16), TrayMuted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                    e.Graphics.FillRectangle(line, lineRect);
                }
            }
        }

        private sealed class SmartNapCompactTrayRenderer : ToolStripProfessionalRenderer
        {
            public SmartNapCompactTrayRenderer() : base(new SmartNapTrayColors()) { }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using (SolidBrush fill = new SolidBrush(TrayBg))
                {
                    e.Graphics.FillRectangle(fill, new Rectangle(Point.Empty, e.ToolStrip.Size));
                }
                using (Pen border = new Pen(Color.FromArgb(58, 78, 112), 1f))
                {
                    Rectangle rect = new Rectangle(Point.Empty, e.ToolStrip.Size);
                    rect.Width -= 1;
                    rect.Height -= 1;
                    e.Graphics.DrawRectangle(border, rect);
                }
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                ToolStripMenuItem item = e.Item as ToolStripMenuItem;
                if (item == null || !item.Enabled) { return; }
                if (!item.Selected && !item.Pressed) { return; }
                Rectangle rect = new Rectangle(4, 2, Math.Max(20, e.Item.Width - 8), e.Item.Height - 4);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = RoundedRect(rect, 7f))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(28, 44, 70)))
                using (Pen border = new Pen(Color.FromArgb(92, 121, 170), 1f))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }
            }

            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                Rectangle rect = new Rectangle(e.ImageRectangle.X + 5, e.ImageRectangle.Y + 7, 12, 12);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = RoundedRect(rect, 6f))
                using (SolidBrush fill = new SolidBrush(AccentGreen))
                {
                    e.Graphics.FillPath(fill, path);
                }
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                Rectangle rect = new Rectangle(8, e.Item.Height / 2, Math.Max(16, e.Item.Width - 16), 1);
                using (SolidBrush line = new SolidBrush(Color.FromArgb(46, 64, 92)))
                {
                    e.Graphics.FillRectangle(line, rect);
                }
            }
        }

        private sealed class SmartNapTrayRenderer : ToolStripProfessionalRenderer
        {
            public SmartNapTrayRenderer() : base(new SmartNapTrayColors()) { }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle rect = new Rectangle(Point.Empty, e.ToolStrip.Size);
                using (LinearGradientBrush fill = new LinearGradientBrush(rect, TrayBg, Color.FromArgb(5, 11, 20), 90f))
                {
                    e.Graphics.FillRectangle(fill, rect);
                }
                using (Pen border = new Pen(Color.FromArgb(54, 75, 112), 1f))
                {
                    Rectangle borderRect = rect;
                    borderRect.Width -= 1;
                    borderRect.Height -= 1;
                    e.Graphics.DrawRectangle(border, borderRect);
                }
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                ToolStripMenuItem item = e.Item as ToolStripMenuItem;
                if (item == null) { base.OnRenderMenuItemBackground(e); return; }
                int width = Math.Max(e.Item.Width, e.ToolStrip == null ? e.Item.Width : e.ToolStrip.ClientSize.Width);
                Rectangle rect = new Rectangle(7, 3, Math.Max(24, width - 14), e.Item.Height - 6);
                bool hot = item.Selected || item.Pressed;
                bool active = IsTrayItemActive(item);
                if (!hot && !active) { return; }
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color accent = active ? AccentGreen : AccentBlue;
                using (GraphicsPath path = RoundedRect(rect, 12f))
                using (LinearGradientBrush fill = new LinearGradientBrush(rect, active ? Color.FromArgb(24, 53, 232, 154) : Color.FromArgb(28, 84, 162, 255), Color.FromArgb(13, 24, 40), 0f))
                using (Pen border = new Pen(Color.FromArgb(hot ? 165 : 95, accent), 1f))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }
            }

            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
            {
                Rectangle rect = new Rectangle(e.ImageRectangle.X + 2, e.ImageRectangle.Y + 3, 18, 18);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = RoundedRect(rect, 7f))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(45, 53, 232, 154)))
                using (Pen border = new Pen(AccentGreen, 1.2f))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }
                using (Pen check = new Pen(Color.White, 1.8f))
                {
                    check.StartCap = LineCap.Round;
                    check.EndCap = LineCap.Round;
                    e.Graphics.DrawLines(check, new Point[] { new Point(rect.X + 5, rect.Y + 10), new Point(rect.X + 8, rect.Y + 13), new Point(rect.X + 14, rect.Y + 6) });
                }
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                Rectangle rect = new Rectangle(12, e.Item.Height / 2, Math.Max(12, e.Item.Width - 24), 1);
                using (LinearGradientBrush line = new LinearGradientBrush(rect, Color.FromArgb(0, 84, 162, 255), Color.FromArgb(88, 84, 162, 255), 0f))
                {
                    e.Graphics.FillRectangle(line, rect);
                }
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Color.FromArgb(198, 213, 235);
                base.OnRenderArrow(e);
            }
        }

        private sealed class SmartNapTrayColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return TrayBg; } }
            public override Color ImageMarginGradientBegin { get { return TrayBg; } }
            public override Color ImageMarginGradientMiddle { get { return TrayBg; } }
            public override Color ImageMarginGradientEnd { get { return TrayBg; } }
            public override Color MenuBorder { get { return Color.FromArgb(54, 75, 112); } }
            public override Color MenuItemBorder { get { return AccentOrange; } }
            public override Color MenuItemSelected { get { return Color.FromArgb(24, 36, 58); } }
            public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(28, 44, 70); } }
            public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(12, 22, 36); } }
            public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(28, 44, 70); } }
            public override Color MenuItemPressedGradientEnd { get { return TrayBg; } }
            public override Color SeparatorDark { get { return Color.FromArgb(48, 62, 86); } }
            public override Color SeparatorLight { get { return Color.FromArgb(14, 18, 28); } }
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

        private void PrimeProcessStartRadar()
        {
            try
            {
                processStartRadarSeenPids.Clear();
                foreach (Process process in Process.GetProcesses())
                {
                    try { processStartRadarSeenPids.Add(process.Id); }
                    catch { }
                    finally { process.Dispose(); }
                }
            }
            catch { }
        }

        private void StartProcessStartRadarTimer()
        {
            processStartRadarTimer = new System.Windows.Forms.Timer();
            processStartRadarTimer.Interval = 900;
            processStartRadarTimer.Tick += delegate { CheckProcessStartRadar(); };
            processStartRadarTimer.Start();
        }

        private void CheckProcessStartRadar()
        {
            if (processStartRadarBusy || (!IsLocalAutoEngineEnabled() && !IsNetworkUdpGuardEnabled())) { return; }

            processStartRadarBusy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<Process> candidates = new List<Process>();
                try
                {
                    HashSet<int> alive = new HashSet<int>();
                    foreach (Process process in Process.GetProcesses())
                    {
                        bool keepForCandidate = false;
                        try
                        {
                            alive.Add(process.Id);
                            if (!processStartRadarSeenPids.Contains(process.Id))
                            {
                                processStartRadarSeenPids.Add(process.Id);
                                candidates.Add(process);
                                keepForCandidate = true;
                            }
                        }
                        catch { }
                        finally
                        {
                            if (!keepForCandidate) { process.Dispose(); }
                        }
                    }

                    if (processStartRadarSeenPids.Count > alive.Count + 512)
                    {
                        processStartRadarSeenPids.Clear();
                        foreach (int id in alive) { processStartRadarSeenPids.Add(id); }
                    }

                    foreach (Process candidate in candidates)
                    {
                        try
                        {
                            RequestReactiveApplyIfNeeded(candidate.Id);
                            if (reactiveApplyBusy) { break; }
                        }
                        catch { }
                    }

                    if (IsNetworkUdpGuardEnabled() && !reactiveApplyBusy)
                    {
                        TryRequestRunningGameReactiveApply();
                    }
                }
                catch (Exception ex)
                {
                    WriteCrash(ex);
                }
                finally
                {
                    foreach (Process candidate in candidates)
                    {
                        try { candidate.Dispose(); } catch { }
                    }
                    processStartRadarBusy = false;
                }
            });
        }
        private void TryRequestRunningGameReactiveApply()
        {
            try
            {
                DateTime now = DateTime.UtcNow;
                if ((now - lastGameRadarSweepAt).TotalMilliseconds < 2500.0) { return; }
                if ((now - lastReactiveApplyAt).TotalSeconds < 2.0) { return; }
                lastGameRadarSweepAt = now;

                int currentPid = Process.GetCurrentProcess().Id;
                int bestPid = 0;
                double bestScore = -1.0;
                foreach (Process process in Process.GetProcesses())
                {
                    try
                    {
                        if (process.Id <= 0 || process.Id == currentPid) { continue; }
                        string reason;
                        if (!ShouldTriggerReactiveApply(process, out reason)) { continue; }
                        if (!String.Equals(reason, "game", StringComparison.OrdinalIgnoreCase)) { continue; }

                        string name = process.ProcessName ?? "";
                        string path = TryGetProcessPath(process);
                        double score = ScoreGameCandidateForReactive(name, path, process);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestPid = process.Id;
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        try { process.Dispose(); } catch { }
                    }
                }

                if (bestPid > 0)
                {
                    RequestReactiveApplyIfNeeded(bestPid);
                }
            }
            catch
            {
            }
        }

        private static bool IsSmartNapProcessOrPath(string processName, string path)
        {
            string name = processName == null ? "" : processName.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { name = name.Substring(0, name.Length - 4); }
            if (NameInList(name, new string[] { "SmartBackgroundNap", "SmartBackgroundNapTray", "SmartBackgroundNapDashboard", "Smart Nap", "smartnap" })) { return true; }
            if (String.IsNullOrWhiteSpace(path)) { return false; }
            return path.IndexOf("SmartBackgroundNap", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("Smart Background Nap", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("\\SmartNap\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private static double ScoreGameCandidateForReactive(string processName, string path, Process process)
        {
            if (IsSmartNapProcessOrPath(processName, path)) { return -10000.0; }
            double score = 0.0;
            if (IsLikelyGameProcessNameForReactive(processName)) { score += 1000.0; }
            if (PathContainsAny(path, new string[] { "\\steamapps\\common\\", "\\XboxGames\\", "\\Epic Games\\", "\\Riot Games\\", "\\Battle.net\\", "\\GOG Galaxy\\Games\\", "\\EA Games\\", "\\Electronic Arts\\Games\\", "\\Electronic Arts\\Battlefield", "\\Electronic Arts\\FC", "\\Electronic Arts\\EA SPORTS FC", "\\Battlefield 6\\", "\\EA SPORTS FC 26\\" })) { score += 500.0; }
            try { if (process != null && process.MainWindowHandle != IntPtr.Zero) { score += 100.0; } } catch { }
            try { score += Math.Min(100.0, Math.Max(0.0, process == null ? 0.0 : process.WorkingSet64 / 1048576.0 / 64.0)); } catch { }
            return score;
        }
        private void StartEnergyIdleGuardTimer()
        {
            energyIdleGuardTimer = new System.Windows.Forms.Timer();
            energyIdleGuardTimer.Interval = 30000;
            energyIdleGuardTimer.Tick += delegate { CheckEnergyIdleGuard(); };
            energyIdleGuardTimer.Start();
        }

        private void StartLocalAutoEngineTimer()
        {
            localAutoEngineTimer = new System.Windows.Forms.Timer();
            localAutoEngineTimer.Interval = 15000;
            localAutoEngineTimer.Tick += delegate { CheckLocalAutoEngine(); };
            localAutoEngineTimer.Start();
            lastLocalAutoEngineRunAt = DateTime.UtcNow.AddMinutes(-LoadAutomationIntervalMinutes());
            CheckLocalAutoEngine();
        }

        private void CheckLocalAutoEngine()
        {
            try
            {
                if (!IsLocalAutoEngineEnabled() || localAutoEngineBusy) { return; }
                int intervalMinutes = LoadAutomationIntervalMinutes();
                if ((DateTime.UtcNow - lastLocalAutoEngineRunAt).TotalMinutes < intervalMinutes) { return; }

                localAutoEngineBusy = true;
                lastLocalAutoEngineRunAt = DateTime.UtcNow;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        RunResult result = RunApplyNow();
                        AppendOperationalLog("action=local-auto status=" + (result.ExitCode == 0 ? "OK" : "FAIL") + " exitCode=" + result.ExitCode.ToString(CultureInfo.InvariantCulture));
                    }
                    catch (Exception ex)
                    {
                        AppendOperationalLog("action=local-auto status=failed error=" + ex.GetType().Name);
                    }
                    finally
                    {
                        localAutoEngineBusy = false;
                        try
                        {
                            if (dispatchForm != null && !dispatchForm.IsDisposed)
                            {
                                dispatchForm.BeginInvoke(new System.Windows.Forms.MethodInvoker(delegate { UpdateTrayTelemetryText(true); }));
                            }
                        }
                        catch
                        {
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
            }
        }
        private void CheckEnergyIdleGuard()
        {
            try
            {
                if (!LoadEnergyIdleGuardEnabled()) { return; }
                PowerPlanSnapshot active = GetActivePowerPlan();
                if (active == null || !String.Equals(active.Guid, SmartNapGamePowerPlanGuid, StringComparison.OrdinalIgnoreCase)) { return; }
                TimeSpan idle = GetSystemIdleTime();
                int minutes = LoadEnergyIdleGuardMinutes();
                if (idle.TotalMinutes < minutes) { return; }
                RunResult result = ActivatePowerPlan(BalancedPowerPlanGuid, "Balanced", false);
                AppendOperationalLog("action=energy-idle-guard idleMinutes=" + ((int)Math.Floor(idle.TotalMinutes)).ToString(CultureInfo.InvariantCulture) + " threshold=" + minutes.ToString(CultureInfo.InvariantCulture) + " result=" + (result.ExitCode == 0 ? "OK" : "FAIL"));
                if (result.ExitCode == 0) { UpdateTrayTelemetryText(true); }
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
            }
        }
        private void StartTrayTelemetryTimer()
        {
            trayTelemetryTimer = new System.Windows.Forms.Timer();
            trayTelemetryTimer.Interval = 5000;
            trayTelemetryTimer.Tick += delegate { UpdateTrayTelemetryText(false); };
            trayTelemetryTimer.Start();
            UpdateTrayTelemetryText(true);
        }

        private void RefreshTrayTelemetryFromHover()
        {
            if ((DateTime.UtcNow - lastTrayTelemetryRefreshAt).TotalMilliseconds < 850.0)
            {
                return;
            }
            UpdateTrayTelemetryText(true);
        }

        private void UpdateTrayTelemetryText()
        {
            UpdateTrayTelemetryText(false);
        }

        private void UpdateTrayTelemetryText(bool force)
        {
            try
            {
                if (!force && (DateTime.UtcNow - lastTrayTelemetryRefreshAt).TotalMilliseconds < 1800.0)
                {
                    return;
                }

                string tooltipText = LimitNotifyText(BuildTrayTelemetryText(), 63);
                if (force || !String.Equals(tooltipText, lastTrayTelemetryText, StringComparison.Ordinal))
                {
                    notifyIcon.Text = tooltipText;
                    lastTrayTelemetryText = tooltipText;
                }
                lastTrayTelemetryRefreshAt = DateTime.UtcNow;
            }
            catch
            {
                try { notifyIcon.Text = AppName; } catch { }
            }
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
                    RequestReactiveApplyIfNeeded(pid);
                }
                finally
                {
                    foregroundRestoreBusy = false;
                }
            });
        }

        private void RequestReactiveApplyIfNeeded(int pid)
        {
            try
            {
                if (pid <= 0 || pid == Process.GetCurrentProcess().Id || reactiveApplyBusy || localAutoEngineBusy) { return; }
                if (!IsLocalAutoEngineEnabled() && !IsNetworkUdpGuardEnabled()) { return; }

                string reason;
                string processName;
                using (Process process = Process.GetProcessById(pid))
                {
                    if (!ShouldTriggerReactiveApply(process, out reason)) { return; }
                    processName = process.ProcessName ?? "unknown";
                }

                DateTime now = DateTime.UtcNow;
                double cooldownSeconds = reason == "game" || reason == "streaming" ? 3.0 : 5.0;
                if (pid == lastReactiveApplyPid && (now - lastReactiveApplyAt).TotalSeconds < 8.0) { return; }
                if ((now - lastReactiveApplyAt).TotalSeconds < cooldownSeconds) { return; }

                reactiveApplyBusy = true;
                lastReactiveApplyAt = now;
                lastReactiveApplyPid = pid;
                lastReactiveApplyReason = reason;
                AppendOperationalLog("action=reactive-boost phase=0 status=queued trigger=" + reason + " pid=" + pid + " process=" + processName);
                RefreshDashboardFromContext();

                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        RunReactiveApplyPhase(reason, pid, processName, 1);
                        bool shockwave = reason == "game" || reason == "streaming" || reason == "workload";
                        if (shockwave)
                        {
                            Thread.Sleep(2200);
                            RunReactiveApplyPhase(reason, pid, processName, 2);
                        }
                        if (reason == "game" || reason == "streaming")
                        {
                            Thread.Sleep(11000);
                            RunReactiveApplyPhase(reason, pid, processName, 3);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendOperationalLog("action=reactive-boost status=FAIL trigger=" + reason + " pid=" + pid + " process=" + processName + " error=" + ShortTaskError(ex.Message));
                        WriteCrash(ex);
                    }
                    finally
                    {
                        reactiveApplyBusy = false;
                        RefreshDashboardFromContext();
                    }
                });
            }
            catch
            {
                reactiveApplyBusy = false;
            }
        }

        private void RunReactiveApplyPhase(string reason, int pid, string processName, int phase)
        {
            try
            {
                RunResult result = RunApplyNow();
                string status = result.ExitCode == 0 ? "OK" : "FAIL";
                AppendOperationalLog("action=reactive-boost phase=" + phase + " status=" + status + " trigger=" + reason + " pid=" + pid + " process=" + processName + " exitCode=" + result.ExitCode);
                RefreshDashboardFromContext();
            }
            catch (Exception ex)
            {
                AppendOperationalLog("action=reactive-boost phase=" + phase + " status=FAIL trigger=" + reason + " pid=" + pid + " process=" + processName + " error=" + ShortTaskError(ex.Message));
                WriteCrash(ex);
            }
        }
        private void RefreshDashboardFromContext()
        {
            try
            {
                if (dispatchForm != null && !dispatchForm.IsDisposed)
                {
                    dispatchForm.BeginInvoke(new System.Windows.Forms.MethodInvoker(delegate
                    {
                        UpdateTrayTelemetryText(true);
#if NET9_0_OR_GREATER
                        dashboardHost.RefreshStatus();
#else
                        if (mainWindow != null) { mainWindow.RefreshStatus(); }
#endif
                    }));
                }
            }
            catch
            {
            }
        }

        private bool ShouldTriggerReactiveApply(Process process, out string reason)
        {
            reason = "";
            if (process == null) { return false; }
            string name = process.ProcessName ?? "";
            if (String.IsNullOrWhiteSpace(name)) { return false; }
            if (IsSmartNapProcessOrPath(name, "")) { return false; }
            if (IsProtectedForegroundProcess(name) || IsSystemForegroundProcess(name) || IsKnownLauncherProcessForReactive(name) || IsBrowserProcessForReactive(name)) { return false; }

            string path = TryGetProcessPath(process);
            if (IsSmartNapProcessOrPath(name, path)) { return false; }
            if (PathContainsAny(path, new string[] { "\\Windows\\", "\\Program Files\\WindowsApps\\" })) { return false; }
            if (IsKnownLauncherPathForReactive(path)) { return false; }

            if (IsStreamingProcessForReactive(name))
            {
                reason = "streaming";
                return true;
            }

            if (IsLikelyGameProcessNameForReactive(name))
            {
                reason = "game";
                return true;
            }

            if (PathContainsAny(path, new string[] { "\\steamapps\\common\\", "\\XboxGames\\", "\\Epic Games\\", "\\Riot Games\\", "\\Battle.net\\", "\\GOG Galaxy\\Games\\", "\\EA Games\\", "\\Electronic Arts\\Games\\", "\\Electronic Arts\\Battlefield", "\\Electronic Arts\\Apex", "\\Electronic Arts\\The Sims", "\\Electronic Arts\\FC", "\\Electronic Arts\\EA SPORTS FC", "\\Battlefield 6\\", "\\EA SPORTS FC 26\\" }))
            {
                reason = "game";
                return true;
            }

            if (IsProfessionalOrDeveloperProcessForReactive(name))
            {
                reason = "workload";
                return true;
            }

            if (IsNetworkUdpGuardEnabled())
            {
                bool hasWindow = false;
                try { hasWindow = process.MainWindowHandle != IntPtr.Zero; } catch { }
                if (hasWindow)
                {
                    reason = "udp-watch";
                    return true;
                }
            }
            return false;
        }

        private static bool IsSystemForegroundProcess(string processName)
        {
            return NameInList(processName, new string[] { "explorer", "ApplicationFrameHost", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "SearchApp", "RuntimeBroker", "TextInputHost", "ctfmon", "sihost", "taskhostw", "dwm", "SystemSettings", "SecurityHealthSystray" });
        }

        private static bool IsKnownLauncherProcessForReactive(string processName)
        {
            return NameInList(processName, new string[] { "steam", "steamwebhelper", "EpicGamesLauncher", "EpicWebHelper", "Battle.net", "EADesktop", "EABackgroundService", "EACefSubProcess", "EALauncher", "EASteamLauncher", "EAConnect", "RiotClientServices", "RiotClientUx", "UbisoftConnect", "upc", "GalaxyClient", "GOG Galaxy", "XboxPcApp" });
        }

        private static bool IsBrowserProcessForReactive(string processName)
        {
            return NameInList(processName, new string[] { "chrome", "msedge", "firefox", "zen", "brave", "opera", "vivaldi", "msedgewebview2" });
        }

        private static bool IsKnownLauncherPathForReactive(string path)
        {
            return PathContainsAny(path, new string[] { "\\Steam\\", "\\Epic Games\\Launcher\\", "\\Electronic Arts\\EA Desktop\\", "\\Electronic Arts\\EA app\\", "\\EA Desktop\\", "\\EA Games\\Launcher\\", "\\Riot Client\\", "\\Ubisoft\\", "\\Battle.net\\", "\\GOG Galaxy\\", "\\Microsoft\\Xbox\\" });
        }
        private static bool IsLikelyGameProcessNameForReactive(string processName)
        {
            if (String.IsNullOrWhiteSpace(processName)) { return false; }
            string name = processName.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { name = name.Substring(0, name.Length - 4); }
            if (NameInList(name, new string[] { "bf6", "bf2042", "bfv", "bf1", "bf4", "bf3", "fc26", "fc25", "fc24", "fifa23", "fifa22", "cs2", "valorant", "valorant-win64-shipping", "r5apex", "apex", "fortniteclient-win64-shipping", "rocketleague", "rainbowsix", "rainbowsix_be", "cod", "cod22", "cod23", "cod24", "modernwarfare", "warzone", "league of legends", "dota2", "overwatch", "destiny2", "thefinals", "pubg", "tslgame", "escape from tarkov", "eldenring", "helldivers2", "gta5", "rdr2" })) { return true; }
            string lower = name.ToLowerInvariant();
            if ((lower.StartsWith("bf", StringComparison.Ordinal) && lower.Length <= 7 && ContainsDigit(lower)) || lower.EndsWith("-win64-shipping", StringComparison.Ordinal)) { return true; }
            return false;
        }

        private static bool ContainsDigit(string value)
        {
            if (String.IsNullOrEmpty(value)) { return false; }
            for (int i = 0; i < value.Length; i++)
            {
                if (Char.IsDigit(value[i])) { return true; }
            }
            return false;
        }
        private static bool IsStreamingProcessForReactive(string processName)
        {
            return NameInList(processName, new string[] { "obs64", "obs32", "Streamlabs Desktop", "Streamlabs", "TikTok LIVE Studio", "TikTokLiveStudio", "TikTokStudio", "PRISMLiveStudio", "XSplit.Core", "XSplitBroadcaster", "vMix64", "vMix", "TwitchStudio", "NVIDIA Broadcast", "ElgatoCameraHub" });
        }

        private static bool IsProfessionalOrDeveloperProcessForReactive(string processName)
        {
            return NameInList(processName, new string[] { "Photoshop", "Illustrator", "AfterFX", "Adobe Premiere Pro", "Adobe Media Encoder", "Lightroom", "Resolve", "Fusion", "vegas170", "vegas180", "vegas190", "vegas200", "vegas210", "vegas220", "blender", "UnrealEditor", "Unity", "acad", "Revit", "SketchUp", "Rhino", "3dsmax", "Maya", "Cinema 4D", "D5Render", "Twinmotion", "devenv", "Code", "Code - Insiders", "cursor", "windsurf", "rider64", "idea64", "pycharm64", "webstorm64", "clion64", "datagrip64", "goland64", "phpstorm64", "rustrover64", "sublime_text", "notepad++", "zed", "codex" });
        }

        private static bool NameInList(string processName, string[] names)
        {
            if (String.IsNullOrWhiteSpace(processName) || names == null) { return false; }
            for (int i = 0; i < names.Length; i++)
            {
                if (String.Equals(processName, names[i], StringComparison.OrdinalIgnoreCase)) { return true; }
            }
            return false;
        }

        private static bool PathContainsAny(string path, string[] fragments)
        {
            if (String.IsNullOrWhiteSpace(path) || fragments == null) { return false; }
            for (int i = 0; i < fragments.Length; i++)
            {
                string fragment = fragments[i];
                if (!String.IsNullOrWhiteSpace(fragment) && path.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            }
            return false;
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
            // Silent by design: the tray tooltip/menu already exposes live status.
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
            int groupWake = TryRestoreForegroundProcessGroup(process);
            Directory.CreateDirectory(outputsPath);
            string line = String.Format(
                CultureInfo.InvariantCulture,
                "{0} action=foreground-restore mode=fast pid={1} process={2} priority={3} memory={4} io={5} power={6} groupWake={7}",
                DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
                pid,
                process.ProcessName,
                priority,
                memory,
                io,
                power,
                groupWake);
            File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
            return new RunResult(0, line);
        }
        catch (Exception ex)
        {
            WriteCrash(ex);
            return new RunResult(1, ex.Message);
        }
    }

    private static int TryRestoreForegroundProcessGroup(Process foregroundProcess)
    {
        if (foregroundProcess == null) { return 0; }
        string name = foregroundProcess.ProcessName;
        if (!ShouldWakeProcessGroup(name)) { return 0; }

        int currentSession;
        try { currentSession = Process.GetCurrentProcess().SessionId; }
        catch { currentSession = -1; }

        int restored = 0;
        Process[] peers;
        try { peers = Process.GetProcessesByName(name); }
        catch { return 0; }

        for (int i = 0; i < peers.Length && restored < 64; i++)
        {
            Process peer = peers[i];
            try
            {
                if (peer.Id == foregroundProcess.Id || peer.Id == Process.GetCurrentProcess().Id) { continue; }
                if (currentSession >= 0 && peer.SessionId != currentSession) { continue; }
                if (IsProtectedForegroundProcess(peer.ProcessName)) { continue; }

                bool touched = false;
                try
                {
                    ProcessPriorityClass current = peer.PriorityClass;
                    if (current == ProcessPriorityClass.Idle || current == ProcessPriorityClass.BelowNormal)
                    {
                        peer.PriorityClass = ProcessPriorityClass.Normal;
                        touched = true;
                    }
                }
                catch { }

                if (TrySetMemoryPriority(peer.Id, 5)) { touched = true; }
                if (TrySetIoPriority(peer.Id, 2)) { touched = true; }
                if (TryClearPowerThrottling(peer.Id)) { touched = true; }
                if (touched) { restored++; }
            }
            catch
            {
            }
            finally
            {
                try { peer.Dispose(); } catch { }
            }
        }

        return restored;
    }

    private static bool ShouldWakeProcessGroup(string processName)
    {
        if (String.IsNullOrWhiteSpace(processName)) { return false; }
        string[] names = new string[]
        {
            "zen", "chrome", "msedge", "firefox", "brave", "opera", "vivaldi",
            "Discord", "Teams", "Slack", "Zoom", "Telegram", "WhatsApp",
            "Spotify", "vlc", "mpv",
            "steam", "steamwebhelper", "EpicGamesLauncher", "EpicWebHelper", "Battle.net",
            "EADesktop", "EABackgroundService", "RiotClientServices", "RiotClientUx",
            "UbisoftConnect", "upc", "GalaxyClient", "GOG Galaxy", "XboxPcApp"
        };
        for (int i = 0; i < names.Length; i++)
        {
            if (String.Equals(processName, names[i], StringComparison.OrdinalIgnoreCase)) { return true; }
        }
        return false;
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

    private static bool IsSessionForegroundProtectedByRuntime(int pid, string processName, string path, out string reason)
    {
        reason = "";
        if (pid <= 0 && String.IsNullOrWhiteSpace(processName) && String.IsNullOrWhiteSpace(path)) { return false; }
        if (IsForegroundProtectedByScoreSnapshot(pid, processName, out reason)) { return true; }
        if (IsForegroundProtectedByTemporaryMap(processName, path, out reason)) { return true; }
        return false;
    }

    private static bool IsForegroundProtectedByScoreSnapshot(int pid, string processName, out string reason)
    {
        reason = "";
        try
        {
            if (String.IsNullOrWhiteSpace(scorePath) || !File.Exists(scorePath)) { return false; }
            IDictionary<string, object> root = LoadJsonMapWithRecovery(scorePath);
            if (root == null || root.Count == 0) { return false; }

            bool udpActive = ReadMapBool(root, "NetworkUdpGuardActive");
            int protectedCount = ReadMapInt(root, "NetworkUdpGuardProtectedCount");
            int udpGamePid = ReadMapInt(root, "NetworkUdpGuardGamePid");
            string udpGame = ReadMapString(root, "NetworkUdpGuardGame");
            if (udpActive && protectedCount > 0)
            {
                if (pid > 0 && udpGamePid == pid)
                {
                    reason = "NetcodeShield";
                    return true;
                }
                if (NamesMatchForProtection(udpGame, processName))
                {
                    reason = "NetcodeShield";
                    return true;
                }
            }

            bool cpuBoundActive = ReadMapBool(root, "CpuBoundAssistActive");
            int cpuBoundPid = ReadMapInt(root, "CpuBoundAssistGamePid");
            string cpuBoundGame = ReadMapString(root, "CpuBoundAssistGame");
            if (cpuBoundActive)
            {
                if (pid > 0 && cpuBoundPid == pid)
                {
                    reason = "CpuBoundAssist";
                    return true;
                }
                if (NamesMatchForProtection(cpuBoundGame, processName))
                {
                    reason = "CpuBoundAssist";
                    return true;
                }
            }
        }
        catch
        {
        }
        reason = "";
        return false;
    }

    private static bool IsForegroundProtectedByTemporaryMap(string processName, string path, out string reason)
    {
        reason = "";
        try
        {
            string protectPath = Path.Combine(outputsPath, "background-nap-protect-latest.json");
            if (String.IsNullOrWhiteSpace(protectPath) || !File.Exists(protectPath)) { return false; }
            IDictionary<string, object> root = LoadJsonMapWithRecovery(protectPath);
            object itemsObject;
            if (root == null || !root.TryGetValue("Items", out itemsObject) || itemsObject == null) { return false; }
            System.Collections.IEnumerable items = itemsObject as System.Collections.IEnumerable;
            if (items == null || itemsObject is string) { return false; }

            string normalizedName = NormalizeProtectionToken(processName);
            string normalizedPath = NormalizeProtectionPath(path);
            foreach (object item in items)
            {
                IDictionary<string, object> map = item as IDictionary<string, object>;
                if (map == null) { continue; }
                if (IsProtectionEntryExpired(ReadMapString(map, "ExpiresAt"))) { continue; }

                string key = NormalizeProtectionKey(ReadMapString(map, "Key"));
                string itemName = NormalizeProtectionToken(ReadMapString(map, "ProcessName"));
                string itemPath = NormalizeProtectionPath(ReadMapString(map, "Path"));
                if (!String.IsNullOrWhiteSpace(normalizedName) && (String.Equals(key, "name:" + normalizedName, StringComparison.OrdinalIgnoreCase) || String.Equals(itemName, normalizedName, StringComparison.OrdinalIgnoreCase)))
                {
                    reason = FirstNonEmpty(ReadMapString(map, "Reason"), "TemporaryProtection");
                    return true;
                }
                if (!String.IsNullOrWhiteSpace(normalizedPath) && (String.Equals(key, "path:" + normalizedPath, StringComparison.OrdinalIgnoreCase) || String.Equals(itemPath, normalizedPath, StringComparison.OrdinalIgnoreCase)))
                {
                    reason = FirstNonEmpty(ReadMapString(map, "Reason"), "TemporaryProtection");
                    return true;
                }
            }
        }
        catch
        {
        }
        reason = "";
        return false;
    }

    private static bool IsProtectionEntryExpired(string expiresAt)
    {
        if (String.IsNullOrWhiteSpace(expiresAt)) { return false; }
        DateTime parsed;
        if (!DateTime.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out parsed)) { return false; }
        return parsed.ToUniversalTime() < DateTime.UtcNow;
    }

    private static bool NamesMatchForProtection(string left, string right)
    {
        return !String.IsNullOrWhiteSpace(left) &&
               !String.IsNullOrWhiteSpace(right) &&
               String.Equals(NormalizeProtectionToken(left), NormalizeProtectionToken(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProtectionToken(string value)
    {
        if (String.IsNullOrWhiteSpace(value)) { return ""; }
        string text = value.Trim();
        if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { text = text.Substring(0, text.Length - 4); }
        return text.ToLowerInvariant();
    }

    private static string NormalizeProtectionPath(string value)
    {
        return String.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeProtectionKey(string value)
    {
        return String.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }

    private static string SanitizeEvidenceToken(string value)
    {
        if (String.IsNullOrWhiteSpace(value)) { return "runtime"; }
        string cleaned = Regex.Replace(value.Trim(), "[^A-Za-z0-9]+", "-").Trim('-').ToLowerInvariant();
        return String.IsNullOrWhiteSpace(cleaned) ? "runtime" : cleaned;
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
    private sealed class GamePresetDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Tier { get; set; }
        public string Genre { get; set; }
        public string Accent { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string ExpectedGain { get; set; }
        public string[] SafeOptions { get; set; }
        public string[] ExperimentalOptions { get; set; }
        public string[] ProcessNames { get; set; }
        public string[] InstallKeywords { get; set; }
        public string[] SafeOptimizations { get; set; }
        public string[] ExperimentalOptimizations { get; set; }
        public string[] DetectProcessNames { get; set; }
        public string[] DetectRoots { get; set; }
    }

    private sealed class WebGamePreset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ShortName { get; set; }
        public string Tier { get; set; }
        public string Genre { get; set; }
        public string Accent { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string ExpectedGain { get; set; }
        public bool Installed { get; set; }
        public bool Running { get; set; }
        public string ProcessName { get; set; }
        public int ProcessId { get; set; }
        public string Path { get; set; }
        public string Status { get; set; }
        public string DetectedPath { get; set; }
        public string DetectionSource { get; set; }
        public string Platform { get; set; }
        public bool PresetApplied { get; set; }
        public string PresetStatus { get; set; }
        public string LastAppliedAt { get; set; }
        public int BackupFiles { get; set; }
        public bool Restored { get; set; }
        public int SelectedSafeCount { get; set; }
        public int SelectedExperimentalCount { get; set; }
        public List<string> SafeOptions { get; set; }
        public List<string> ExperimentalOptions { get; set; }
        public List<string> SafeOptimizations { get; set; }
        public List<string> ExperimentalOptimizations { get; set; }
        public string CoverDataUrl { get; set; }
    }

    private static readonly Dictionary<string, string> GameCoverDataUrlCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly object GameCoverDataUrlLock = new object();

    private static string GetGameCoverDataUrl(string gameId)
    {
        string id = (gameId ?? "").Trim().ToLowerInvariant();
        string resourceName = "";
        switch (id)
        {
            case "bf6": resourceName = "game_bf6_cover_jpg"; break;
            case "eafc26": resourceName = "game_eafc26_cover_jpg"; break;
            case "cs2": resourceName = "game_cs2_cover_jpg"; break;
            case "valorant": resourceName = "game_valorant_cover_jpg"; break;
        }
        if (String.IsNullOrWhiteSpace(resourceName)) { return ""; }

        lock (GameCoverDataUrlLock)
        {
            string cached;
            if (GameCoverDataUrlCache.TryGetValue(id, out cached)) { return cached; }
        }

        try
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourcePrefix + resourceName))
            {
                if (stream == null) { return ""; }
                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    string dataUrl = "data:image/jpeg;base64," + Convert.ToBase64String(memory.ToArray());
                    lock (GameCoverDataUrlLock) { GameCoverDataUrlCache[id] = dataUrl; }
                    return dataUrl;
                }
            }
        }
        catch { return ""; }
    }

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
                });
                window = (System.Windows.Window)dashboardWindow;
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
                throw new InvalidOperationException("WebView2 dashboard could not start in this WebView-only build.", ex);
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
            WriteCrash(new InvalidOperationException("Native launcher fallback is disabled in the WebView-only build."));
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
        private DateTime lastDashboardStateSentUtc = DateTime.MinValue;
        private bool lowImpactRuntimeActive;
        private string lowImpactRuntimeReason = "";
        private int lowImpactRuntimeCadenceSeconds = 1;
        private RunControl activeRunControl;
        private bool webReady;
        private bool busy;
        private bool activeRunCanStop;
        private DateTime activeRunStartedAt;
        private string activeUiEventLine;
        private string activeTitle = "Control Center";
        private string activeDetail = "Waiting for the next pass.";
        private string runState = "READY";
        private ReleaseUpdateInfo updateInfo = ReleaseUpdateInfo.Idle();
        private bool updateCheckRunning;
        private DateTime updateCheckedAtUtc = DateTime.MinValue;
        private bool manualMaximized;
        private bool initialMaximizeApplied;
        private System.Windows.Rect restoreWindowRect;
        private bool webDragActive;
        private double webDragStartX;
        private double webDragStartY;
        private double webDragStartLeft;
        private double webDragStartTop;
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
        private const double ResponsiveMinWindowWidth = 560.0;
        private const double ResponsiveMinWindowHeight = 460.0;
        [DllImport("user32.dll", EntryPoint = "ReleaseCapture")]
        private static extern bool ReleaseCaptureForDrag();

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessageForDrag(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public WebViewDashboardWindow(Action<Exception> fallbackRequested)
        {
            this.fallbackRequested = fallbackRequested;
            Title = AppName;
            Width = 1440;
            Height = 780;
            MinWidth = ResponsiveMinWindowWidth;
            MinHeight = ResponsiveMinWindowHeight;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            WindowStyle = System.Windows.WindowStyle.None;
            ResizeMode = System.Windows.ResizeMode.CanResize;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 9, 15));
            Icon = LoadWebViewWindowIcon(iconPath);
            ApplyResponsiveWindowBounds();
            SourceInitialized += delegate { InstallNativeWindowChrome(); };

            webView = new WebView2();
            System.Windows.Controls.Grid host = new System.Windows.Controls.Grid();
            host.Children.Add(webView);
            host.Children.Add(CreateDragOverlay());
            Content = host;

            Loaded += async delegate
            {
                ApplyInitialMaximizedWindow();
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
                    BeginUpdateCheck(false);
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
                double availableWidth = Math.Max(ResponsiveMinWindowWidth, workArea.Width - 28.0);
                double availableHeight = Math.Max(ResponsiveMinWindowHeight, workArea.Height - 72.0);

                // Do not cap MaxWidth/MaxHeight: WPF applies those caps when the native maximize button is clicked.
                // Keeping them infinite lets Windows fill the full work area while preserving a sane first-open size.
                MaxWidth = Double.PositiveInfinity;
                MaxHeight = Double.PositiveInfinity;
                Width = Math.Min(1440.0, availableWidth);
                Height = Math.Min(820.0, availableHeight);
                MinWidth = Math.Min(ResponsiveMinWindowWidth, Width);
                MinHeight = Math.Min(ResponsiveMinWindowHeight, Height);
            }
            catch
            {
            }
        }

        private void InstallNativeWindowChrome()
        {
            try
            {
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(this);
                ApplyDarkWindowFrame(helper.Handle);
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

        private void ApplyInitialMaximizedWindow()
        {
            if (initialMaximizeApplied)
            {
                return;
            }

            initialMaximizeApplied = true;
            if (WindowState == System.Windows.WindowState.Minimized || manualMaximized)
            {
                return;
            }

            ToggleManualMaximize();
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

            if (!manualMaximized && WindowState == System.Windows.WindowState.Normal && ResizeMode != System.Windows.ResizeMode.NoResize)
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

        private static System.Windows.Rect GetVirtualDragBounds()
        {
            try
            {
                return new System.Windows.Rect(
                    System.Windows.SystemParameters.VirtualScreenLeft,
                    System.Windows.SystemParameters.VirtualScreenTop,
                    System.Windows.SystemParameters.VirtualScreenWidth,
                    System.Windows.SystemParameters.VirtualScreenHeight);
            }
            catch
            {
                return System.Windows.SystemParameters.WorkArea;
            }
        }

        private System.Windows.Rect GetCurrentWindowWorkArea()
        {
            try
            {
                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(this);
                Rectangle area = Screen.FromHandle(helper.Handle).WorkingArea;
                return new System.Windows.Rect(area.Left, area.Top, area.Width, area.Height);
            }
            catch
            {
                return System.Windows.SystemParameters.WorkArea;
            }
        }
        private void ClampWindowToWorkArea()
        {
            try
            {
                System.Windows.Rect workArea = GetCurrentWindowWorkArea();
                double safeWidth = Math.Max(360.0, workArea.Width - 20.0);
                double safeHeight = Math.Max(360.0, workArea.Height - 20.0);
                if (MinWidth > safeWidth) { MinWidth = safeWidth; }
                if (MinHeight > safeHeight) { MinHeight = safeHeight; }
                if (Width > workArea.Width) { Width = Math.Max(MinWidth, safeWidth); }
                if (Height > workArea.Height) { Height = Math.Max(MinHeight, safeHeight); }
                if (Left < workArea.Left + 8.0) { Left = workArea.Left + 8.0; }
                if (Top < workArea.Top + 8.0) { Top = workArea.Top + 8.0; }
                if (Left + Width > workArea.Right - 8.0) { Left = Math.Max(workArea.Left + 8.0, workArea.Right - Width - 8.0); }
                if (Top + Height > workArea.Bottom - 8.0) { Top = Math.Max(workArea.Top + 8.0, workArea.Bottom - Height - 8.0); }
            }
            catch
            {
            }
        }

        private System.Windows.FrameworkElement CreateDragOverlay()
        {
            System.Windows.Controls.Border overlay = new System.Windows.Controls.Border();
            overlay.Background = System.Windows.Media.Brushes.Transparent;
            overlay.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            overlay.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            overlay.Margin = new System.Windows.Thickness(78.0, 0.0, WindowButtonReserveWidth, 0.0);
            overlay.Height = DragBandHeight;
            overlay.Cursor = System.Windows.Input.Cursors.SizeAll;
            overlay.MouseLeftButtonDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                e.Handled = true;
                BeginNativeDrag();
            };
            return overlay;
        }
        private void BeginWebDrag(IDictionary<string, object> message)
        {
            try
            {
                if (WindowState == System.Windows.WindowState.Minimized)
                {
                    WindowState = System.Windows.WindowState.Normal;
                }
                if (manualMaximized)
                {
                    ToggleManualMaximize();
                }

                webDragActive = true;
                webDragStartX = GetDouble(message, "x");
                webDragStartY = GetDouble(message, "y");
                webDragStartLeft = Left;
                webDragStartTop = Top;
            }
            catch
            {
                webDragActive = false;
            }
        }

        private void MoveWebDrag(IDictionary<string, object> message)
        {
            if (!webDragActive)
            {
                return;
            }

            try
            {
                double x = GetDouble(message, "x");
                double y = GetDouble(message, "y");
                double nextLeft = webDragStartLeft + (x - webDragStartX);
                double nextTop = webDragStartTop + (y - webDragStartY);
                System.Windows.Rect workArea = GetVirtualDragBounds();
                double minLeft = workArea.Left - Math.Max(0.0, Width - 96.0);
                double maxLeft = workArea.Right - 96.0;
                double minTop = workArea.Top;
                double maxTop = workArea.Bottom - 48.0;
                Left = Math.Max(minLeft, Math.Min(maxLeft, nextLeft));
                Top = Math.Max(minTop, Math.Min(maxTop, nextTop));
            }
            catch
            {
                webDragActive = false;
            }
        }
        private void BeginNativeDrag()
        {
            try
            {
                if (manualMaximized)
                {
                    ToggleManualMaximize();
                }

                System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(this);
                ReleaseCaptureForDrag();
                SendMessageForDrag(helper.Handle, 0xA1, new IntPtr(HtCaption), IntPtr.Zero);
            }
            catch
            {
                try { DragMove(); } catch { }
            }
        }
        private void ToggleManualMaximize()
        {
            try
            {
                if (WindowState == System.Windows.WindowState.Minimized)
                {
                    WindowState = System.Windows.WindowState.Normal;
                }

                if (manualMaximized)
                {
                    manualMaximized = false;
                    Left = restoreWindowRect.Left;
                    Top = restoreWindowRect.Top;
                    Width = Math.Max(MinWidth, restoreWindowRect.Width);
                    Height = Math.Max(MinHeight, restoreWindowRect.Height);
                    return;
                }

                System.Windows.Rect workArea = GetCurrentWindowWorkArea();
                double currentWidth = ActualWidth > 0.0 ? ActualWidth : Width;
                double currentHeight = ActualHeight > 0.0 ? ActualHeight : Height;
                double currentLeft = Double.IsNaN(Left) ? workArea.Left + Math.Max(0.0, (workArea.Width - currentWidth) / 2.0) : Left;
                double currentTop = Double.IsNaN(Top) ? workArea.Top + Math.Max(0.0, (workArea.Height - currentHeight) / 2.0) : Top;
                restoreWindowRect = new System.Windows.Rect(currentLeft, currentTop, currentWidth, currentHeight);
                manualMaximized = true;
                WindowState = System.Windows.WindowState.Normal;
                Left = workArea.Left;
                Top = workArea.Top;
                Width = workArea.Width;
                Height = workArea.Height;
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
                    BeginUpdateCheck(false);
                    SendState();
                    return;
                }
                if (String.Equals(action, "setLanguage", StringComparison.OrdinalIgnoreCase))
                {
                    SaveUiLanguage(GetString(message, "language"));
                    SendState();
                    return;
                }
                if (String.Equals(action, "dragStart", StringComparison.OrdinalIgnoreCase))
                {
                    BeginWebDrag(message);
                    return;
                }
                if (String.Equals(action, "dragMove", StringComparison.OrdinalIgnoreCase))
                {
                    MoveWebDrag(message);
                    return;
                }
                if (String.Equals(action, "dragEnd", StringComparison.OrdinalIgnoreCase))
                {
                    webDragActive = false;
                    return;
                }                if (String.Equals(action, "drag", StringComparison.OrdinalIgnoreCase))
                {
                    BeginNativeDrag();
                    return;
                }
                if (String.Equals(action, "minimize", StringComparison.OrdinalIgnoreCase))
                {
                    WindowState = System.Windows.WindowState.Minimized;
                    return;
                }
                if (String.Equals(action, "maximize", StringComparison.OrdinalIgnoreCase))
                {
                    ToggleManualMaximize();
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
                    bool startupInstalled = IsStartupInstalled();
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
                if (String.Equals(action, "toggleUdpGuard", StringComparison.OrdinalIgnoreCase))
                {
                    bool udpEnabled = IsNetworkUdpGuardEnabled();
                    RunUserAction(
                        udpEnabled ? "Disabling Zero Ping..." : "Enabling Zero Ping...",
                        udpEnabled ? "Zero Ping is off." : "Zero Ping is ready.",
                        delegate { return SetNetworkUdpGuardEnabled(!udpEnabled); });
                    return;
                }
                if (String.Equals(action, "setAppPolicy", StringComparison.OrdinalIgnoreCase))
                {
                    SetAppPolicyFromMessage(message);
                    SendState();
                    return;
                }
                if (String.Equals(action, "refreshGames", StringComparison.OrdinalIgnoreCase))
                {
                    RunUserAction("Procurando jogos instalados...", "Busca de jogos concluída.", delegate { int found = RefreshGameDiscoveryCache(); return new RunResult(0, "Busca concluída: " + found.ToString(CultureInfo.InvariantCulture) + " jogo(s) com caminho salvo. Se um jogo não aparecer, use Escolher pasta no card dele."); });
                    return;
                }
                if (String.Equals(action, "selectGameFolder", StringComparison.OrdinalIgnoreCase))
                {
                    SelectGameFolderFromMessage(message);
                    return;
                }
                if (String.Equals(action, "applyGamePreset", StringComparison.OrdinalIgnoreCase))
                {
                    RunUserAction("Aplicando preset de jogo...", "Preset de jogo aplicado.", delegate { return ApplyGamePresetFromMessage(message); });
                    return;
                }
                if (String.Equals(action, "restoreGamePreset", StringComparison.OrdinalIgnoreCase))
                {
                    RunUserAction("Restaurando preset de jogo...", "Preset de jogo restaurado.", delegate { return RestoreGamePresetFromMessage(message); });
                    return;
                }
                if (String.Equals(action, "ackGameBetaWelcome", StringComparison.OrdinalIgnoreCase))
                {
                    MarkGameBetaWelcomeSeen();
                    SendState();
                    return;
                }
                if (String.Equals(action, "setSessionMode", StringComparison.OrdinalIgnoreCase))
                {
                    string mode = GetMapString(message, "mode");
                    string energy = GetMapString(message, "energy");
                    if (message.ContainsKey("idleEnabled") || message.ContainsKey("idleMinutes"))
                    {
                        SaveEnergyIdleGuard(GetBool(message, "idleEnabled"), GetInt(message, "idleMinutes"));
                    }
                    RunUserAction("Changing session mode...", "Session mode updated.", delegate { return SetSessionMode(mode, energy); });
                    return;
                }
                if (String.Equals(action, "toggleAdaptiveExclusions", StringComparison.OrdinalIgnoreCase))
                {
                    bool enabled = !IsAdaptiveExclusionsEnabled();
                    RunUserAction(enabled ? "Enabling adaptive exclusions..." : "Disabling adaptive exclusions...", enabled ? "Adaptive exclusions enabled." : "Adaptive exclusions disabled.", delegate { return SetAdaptiveExclusionsEnabled(enabled); });
                    return;
                }
                if (String.Equals(action, "preview", StringComparison.OrdinalIgnoreCase))
                {
                    RunPreviewActionWithFeedback();
                    return;
                }
                if (String.Equals(action, "clearPolicies", StringComparison.OrdinalIgnoreCase))
                {
                    RunUserAction("Clearing manual policies...", "Manual policies cleared.", ClearAppPolicies);
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
                if (String.Equals(action, "setUpdateAuto", StringComparison.OrdinalIgnoreCase))
                {
                    SaveAutoUpdateChecks(GetBool(message, "enabled"));
                    SendState();
                    return;
                }
                if (String.Equals(action, "setEnergyIdleGuard", StringComparison.OrdinalIgnoreCase))
                {
                    SaveEnergyIdleGuard(GetBool(message, "enabled"), GetInt(message, "minutes"));
                    SendState();
                    return;
                }
                if (String.Equals(action, "checkUpdate", StringComparison.OrdinalIgnoreCase)) { BeginUpdateCheck(true); return; }
                if (String.Equals(action, "openUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    string downloadUrl = updateInfo == null || String.IsNullOrWhiteSpace(updateInfo.DownloadUrl) ? GitHubLatestDownloadUrl : updateInfo.DownloadUrl;
                    string latestTag = updateInfo == null ? "" : updateInfo.LatestTag;
                    RunUserAction("Baixando e instalando pelo Smart Nap...", "Atualizacao interna iniciada.", delegate { return StartSelfUpdate(downloadUrl, latestTag, updateInfo == null ? "" : updateInfo.ReleaseBody); });
                    return;
                }
                if (String.Equals(action, "dismissUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    string tag = updateInfo == null ? "" : updateInfo.LatestTag;
                    if (!String.IsNullOrWhiteSpace(tag)) { SaveDismissedUpdateTag(tag); }
                    if (updateInfo != null) { updateInfo.Available = false; updateInfo.Ignored = true; }
                    SendState();
                    return;
                }
                if (String.Equals(action, "ackPostUpdate", StringComparison.OrdinalIgnoreCase))
                {
                    MarkPostUpdateNoticeSeen();
                    SendState();
                    return;
                }
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
            }
        }

        private void BeginUpdateCheck(bool force)
        {
            if (updateCheckRunning) { return; }
            if (!force && !LoadAutoUpdateChecks()) { return; }
            if (!force && updateInfo != null && updateInfo.Checked && (DateTime.UtcNow - updateCheckedAtUtc) < TimeSpan.FromHours(6)) { return; }

            updateCheckRunning = true;
            updateInfo = ReleaseUpdateInfo.InProgress();
            SendState();

            ThreadPool.QueueUserWorkItem(delegate
            {
                ReleaseUpdateInfo result = CheckForOfficialUpdate();
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    updateInfo = result ?? ReleaseUpdateInfo.Idle();
                    updateCheckedAtUtc = DateTime.UtcNow;
                    updateCheckRunning = false;
                    if (updateInfo.Available)
                    {
                        activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  UPDATE  " + updateInfo.LatestTag + " disponivel";
                    }
                    SendState();
                }));
            });
        }
        private static bool IsKnownAppPolicy(string policy, bool includeAuto)
        {
            if (String.IsNullOrWhiteSpace(policy)) { return false; }
            if (includeAuto && String.Equals(policy, "Auto", StringComparison.OrdinalIgnoreCase)) { return true; }
            return String.Equals(policy, "Protect", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(policy, "Light", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(policy, "Balanced", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(policy, "Deep", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAppPolicyName(string policy)
        {
            if (String.IsNullOrWhiteSpace(policy)) { return ""; }
            if (String.Equals(policy, "Auto", StringComparison.OrdinalIgnoreCase)) { return "Auto"; }
            if (String.Equals(policy, "Protect", StringComparison.OrdinalIgnoreCase)) { return "Protect"; }
            if (String.Equals(policy, "Light", StringComparison.OrdinalIgnoreCase)) { return "Light"; }
            if (String.Equals(policy, "Balanced", StringComparison.OrdinalIgnoreCase)) { return "Balanced"; }
            if (String.Equals(policy, "Deep", StringComparison.OrdinalIgnoreCase)) { return "Deep"; }
            return "";
        }

        private static string NormalizeAppPolicyKey(string key)
        {
            return String.IsNullOrWhiteSpace(key) ? "" : key.Trim().ToLowerInvariant();
        }

        private static void AddAppPolicyKey(List<string> keys, string key)
        {
            key = NormalizeAppPolicyKey(key);
            if (String.IsNullOrWhiteSpace(key)) { return; }
            foreach (string existing in keys)
            {
                if (String.Equals(existing, key, StringComparison.OrdinalIgnoreCase)) { return; }
            }
            keys.Add(key);
        }

        private static List<string> BuildAppPolicyKeys(string key, string processName, string path)
        {
            List<string> keys = new List<string>();
            AddAppPolicyKey(keys, key);
            if (!String.IsNullOrWhiteSpace(path)) { AddAppPolicyKey(keys, "path:" + path.Trim().ToLowerInvariant()); }
            if (!String.IsNullOrWhiteSpace(processName)) { AddAppPolicyKey(keys, "name:" + processName.Trim().ToLowerInvariant()); }
            return keys;
        }

        private static Dictionary<string, string> LoadAppPolicyMapForUi()
        {
            Dictionary<string, string> policies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (String.IsNullOrWhiteSpace(appPolicyPath) || !File.Exists(appPolicyPath)) { return policies; }
                IDictionary<string, object> root = LoadJsonMapWithRecovery(appPolicyPath);
                object existingItems = null;
                System.Collections.IEnumerable enumerable = root != null && root.TryGetValue("Items", out existingItems) ? existingItems as System.Collections.IEnumerable : null;
                if (enumerable == null || existingItems is string) { return policies; }
                foreach (object item in enumerable)
                {
                    IDictionary<string, object> map = item as IDictionary<string, object>;
                    if (map == null) { continue; }
                    string policy = NormalizePolicyNameForConfig(GetMapString(map, "Policy"));
                    if (!IsKnownAppPolicy(policy, true)) { continue; }
                    foreach (string policyKey in BuildAppPolicyKeys(GetMapString(map, "Key"), GetMapString(map, "ProcessName"), GetMapString(map, "Path")))
                    {
                        policies[policyKey] = policy;
                    }
                }
            }
            catch
            {
            }
            return policies;
        }

        private static void ApplyManualPolicyOverlay(WebManagerRow row, IDictionary<string, string> manualPolicies)
        {
            if (row == null || manualPolicies == null || manualPolicies.Count == 0) { return; }
            foreach (string policyKey in BuildAppPolicyKeys(row.Key, row.ProcessName, row.Path))
            {
                string policy;
                if (manualPolicies.TryGetValue(policyKey, out policy) && IsKnownAppPolicy(policy, true))
                {
                    row.Policy = policy;
                    row.PolicySource = String.Equals(policy, "Auto", StringComparison.OrdinalIgnoreCase) ? "" : "user";
                    return;
                }
            }
        }
        private void SetAppPolicyFromMessage(IDictionary<string, object> message)
        {
            string policy = NormalizeAppPolicyName(GetString(message, "policy"));
            if (!IsKnownAppPolicy(policy, true)) { return; }

            string key = GetString(message, "key");
            string processName = GetString(message, "processName");
            string path = GetString(message, "path");
            List<string> targetKeys = BuildAppPolicyKeys(key, processName, path);
            if (targetKeys.Count == 0) { return; }
            HashSet<string> targetSet = new HashSet<string>(targetKeys, StringComparer.OrdinalIgnoreCase);

            Directory.CreateDirectory(outputsPath);
            List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();
            try
            {
                if (File.Exists(appPolicyPath))
                {
                    IDictionary<string, object> root = LoadJsonMapWithRecovery(appPolicyPath);
                    object existingItems = null;
                    System.Collections.IEnumerable enumerable = root != null && root.TryGetValue("Items", out existingItems) ? existingItems as System.Collections.IEnumerable : null;
                    if (enumerable != null && !(existingItems is string))
                    {
                        foreach (object item in enumerable)
                        {
                            IDictionary<string, object> map = item as IDictionary<string, object>;
                            if (map == null) { continue; }
                            string existingKey = GetMapString(map, "Key");
                            string existingPolicy = NormalizePolicyNameForConfig(GetMapString(map, "Policy"));
                            if (String.IsNullOrWhiteSpace(existingKey) || !IsKnownAppPolicy(existingPolicy, true)) { continue; }
                            bool sameTarget = false;
                            foreach (string existingAlias in BuildAppPolicyKeys(existingKey, GetMapString(map, "ProcessName"), GetMapString(map, "Path")))
                            {
                                if (targetSet.Contains(existingAlias)) { sameTarget = true; break; }
                            }
                            if (sameTarget) { continue; }
                            Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            copy["Key"] = NormalizeAppPolicyKey(existingKey);
                            copy["ProcessName"] = GetMapString(map, "ProcessName");
                            copy["Path"] = GetMapString(map, "Path");
                            copy["Policy"] = existingPolicy;
                            copy["UpdatedAt"] = GetString(map, "UpdatedAt");
                            items.Add(copy);
                        }
                    }
                }
            }
            catch
            {
            }

            foreach (string targetKey in targetKeys)
            {
                Dictionary<string, object> entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                entry["Key"] = targetKey;
                entry["ProcessName"] = processName;
                entry["Path"] = path;
                entry["Policy"] = policy;
                entry["UpdatedAt"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
                items.Add(entry);
            }

            Dictionary<string, object> output = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            output["Timestamp"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            output["Items"] = items;
            AtomicWriteJsonMap(appPolicyPath, output);

            string label = String.IsNullOrWhiteSpace(processName) ? targetKeys[0] : processName;
            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  POLICY  " + label + " -> " + policy;
            try
            {
                string safeLabel = label.Replace(' ', '_');
                File.AppendAllText(logPath, DateTime.Now.ToString("s", CultureInfo.InvariantCulture) + " action=policy process=" + safeLabel + " policy=" + policy + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
            }
        }
        private static bool NameInList(string processName, string[] names)
        {
            if (String.IsNullOrWhiteSpace(processName) || names == null) { return false; }
            foreach (string name in names)
            {
                if (String.IsNullOrWhiteSpace(name)) { continue; }
                if (String.Equals(processName, name, StringComparison.OrdinalIgnoreCase)) { return true; }
            }
            return false;
        }

        private static bool IsKnownLauncherProcessForReactive(string processName)
        {
            return NameInList(processName, new string[] { "steam", "steamwebhelper", "EpicGamesLauncher", "EpicWebHelper", "Battle.net", "EADesktop", "EABackgroundService", "EACefSubProcess", "EALauncher", "EASteamLauncher", "EAConnect", "RiotClientServices", "RiotClientUx", "UbisoftConnect", "upc", "GalaxyClient", "GOG Galaxy", "XboxPcApp" });
        }

        private static List<GamePresetDefinition> GetGamePresetDefinitions()
        {
            return new List<GamePresetDefinition>
            {
                new GamePresetDefinition
                {
                    Id = "bf6",
                    Name = "Battlefield 6",
                    ShortName = "BF6",
                    Tier = "Competitive FPS",
                    Accent = "orange",
                    Description = "Biblioteca beta com ajustes de comunidade para cache/shader, frame pacing, CPU-bound e stutter em partidas grandes.",
                    ProcessNames = new[] { "bf6", "Battlefield6", "Battlefield" },
                    InstallKeywords = new[] { "Battlefield 6", "Battlefield6", "Battlefield" },
                    SafeOptions = new[] { "Pipeline de shader/cache: backup e reconstrução guiada após update", "Frame pacing competitivo: cap estável por Hz e anti-stutter", "Config de engine segura: streaming e orçamento de CPU mais leve", "DX12/driver cache hygiene: remover cache antigo sem apagar saves", "Overlay/download guard: EA, Steam, Discord e capturas em modo leve", "Preset CPU-bound: reduzir pós-processamento pesado sem mudar controles" },
                    ExperimentalOptions = new[] { "user.cfg avançado: thread budget e render queue em teste A/B", "Cache rebuild agressivo: DX/NVIDIA/AMD shader cache com aviso de stutter inicial", "Ultra low CPU fallback: streaming, efeitos e partículas em orçamento mínimo", "Overlay hard-off test: EA/Steam por config reversível com backup", "Frame cap lab: 90/120/144/165 com medição de pacing" }
                },
                new GamePresetDefinition
                {
                    Id = "eafc26",
                    Name = "EA SPORTS FC 26",
                    ShortName = "FC26",
                    Tier = "Sports online",
                    Accent = "cyan",
                    Description = "Receitas de comunidade para reduzir stutter, estabilizar frame pacing e controlar EA/Steam sem alterar gameplay.",
                    ProcessNames = new[] { "FC26", "FC25", "FC24" },
                    InstallKeywords = new[] { "EA SPORTS FC 26", "EA Sports FC 26", "FC26", "EA SPORTS FC" },
                    SafeOptions = new[] { "FC setup sanity: detectar arquivo de settings e fazer backup", "Stutter guard: cap de FPS por Hz e estabilidade de cutscenes", "EA/Steam overlay guard: reduzir overlays e downloads durante partida", "Shader/cache refresh guiado após update de driver ou patch", "CPU/GPU balance: crowd, hair e cloth em perfil de desempenho", "Fullscreen e Hz corretos sem mexer em câmera, controle ou gameplay" },
                    ExperimentalOptions = new[] { "FC microstutter lab: caps 60/90/120/144 com rollback", "Stadium heavy preset: reduzir crowd, cloth e hair para PC fraco", "EA overlay hard-off via user_*.ini com backup", "Cache rebuild agressivo em Documents/AppData quando há travadas", "Steam input/overlay isolation quando FC abre via Steam" }
                },
                new GamePresetDefinition
                {
                    Id = "cs2",
                    Name = "Counter-Strike 2",
                    ShortName = "CS2",
                    Tier = "Competitive shooter",
                    Accent = "blue",
                    Description = "Receitas competitivas de comunidade para launch options limpas, autoexec, shader cache, Reflex e frame pacing.",
                    ProcessNames = new[] { "cs2" },
                    InstallKeywords = new[] { "Counter-Strike Global Offensive", "Counter-Strike 2", "csgo", "cs2" },
                    SafeOptions = new[] { "Launch options auditor: remover comandos antigos ou prejudiciais", "Autoexec performance pack: telemetria, pacing e cvars seguras", "NVIDIA Reflex check: orientar ON quando suportado", "Shader prewarm/cache hygiene após update do jogo ou driver", "Frame cap estável por Hz para reduzir variação de frametime", "Steam overlay/download guard durante partida competitiva" },
                    ExperimentalOptions = new[] { "-vulkan A/B test com reversão automática", "fps_max lab: 0, refresh+buffer ou cap competitivo", "DX shader cache rebuild agressivo", "Low-end cfg: partículas, decals e streaming budget reduzidos", "Workshop/custom cfg quarantine para caçar stutter" }
                },
                new GamePresetDefinition
                {
                    Id = "valorant",
                    Name = "VALORANT",
                    ShortName = "VALORANT",
                    Tier = "Tactical FPS",
                    Accent = "violet",
                    Description = "Receitas seguras de comunidade para FPS alto, baixa latência, cache limpo e estabilidade sem tocar no Vanguard.",
                    ProcessNames = new[] { "VALORANT-Win64-Shipping", "VALORANT" },
                    InstallKeywords = new[] { "VALORANT", "Riot Games" },
                    SafeOptions = new[] { "Config backup Riot e validação de GameUserSettings", "Multithreaded Rendering check quando a CPU suporta", "NVIDIA Reflex/low latency check quando suportado", "FPS cap por menu/background para aliviar stutter térmico", "Fullscreen e Hz sanity sem tocar em sensibilidade ou mira", "Overlay/download guard sem tocar no Vanguard" },
                    ExperimentalOptions = new[] { "FPS cap lab por cenário: menu, background e in-game", "Low-end GPU profile: material, detail e UI em modo performance", "Cache/config reset guiado com backup", "Overlay hard isolation sem mexer no Vanguard", "Frame pacing stress test por monitor" }
                }
            };
        }

        private static List<WebGamePreset> BuildGamePresetsForUi()
        {
            List<WebGamePreset> output = new List<WebGamePreset>();
            List<Process> processes = new List<Process>();
            IDictionary<string, object> presetState = LoadJsonMapWithRecovery(Path.Combine(outputsPath, "game-presets.state.json"));
            string appliedGameId = GetMapString(presetState, "LastGameId");
            string appliedAt = GetMapString(presetState, "Timestamp");
            bool restored = GetBool(presetState, "Restored");
            int backupFiles = GetInt(presetState, "BackupFiles");
            List<string> appliedSafeOptions = GetMapStringList(presetState, "SafeOptions");
            List<string> appliedExperimentalOptions = GetMapStringList(presetState, "ExperimentalOptions");
            try { processes.AddRange(Process.GetProcesses()); } catch { }
            try
            {
                foreach (GamePresetDefinition definition in GetGamePresetDefinitions())
                {
                    Process running = null;
                    string runningPath = "";
                    foreach (Process process in processes)
                    {
                        string processName = "";
                        try { processName = process.ProcessName ?? ""; } catch { }
                        if (!NameInList(processName, definition.ProcessNames)) { continue; }
                        if (IsKnownLauncherProcessForReactive(processName)) { continue; }
                        running = process;
                        runningPath = TryGetProcessPath(process);
                        break;
                    }

                    string manualPath = FindManualGameInstallPath(definition);
                    string installedPath = !String.IsNullOrWhiteSpace(runningPath) ? runningPath : manualPath;
                    string detectionSource = running != null ? "Rodando" : (!String.IsNullOrWhiteSpace(manualPath) ? "Pasta salva" : (!String.IsNullOrWhiteSpace(installedPath) ? "Biblioteca detectada" : "Não encontrado"));
                    bool presetApplied = !restored && String.Equals(appliedGameId, definition.Id, StringComparison.OrdinalIgnoreCase);
                    output.Add(new WebGamePreset
                    {
                        Id = definition.Id,
                        Name = definition.Name,
                        ShortName = definition.ShortName,
                        Tier = definition.Tier,
                        Genre = definition.Tier,
                        Accent = definition.Accent,
                        Summary = definition.Description,
                        Description = definition.Description,
                        ExpectedGain = definition.Tier,
                        CoverDataUrl = GetGameCoverDataUrl(definition.Id),
                        Installed = !String.IsNullOrWhiteSpace(installedPath),
                        Running = running != null,
                        ProcessName = running == null ? "" : running.ProcessName,
                        ProcessId = running == null ? 0 : running.Id,
                        Path = installedPath,
                        DetectedPath = installedPath,
                        Status = running != null ? "Running" : (!String.IsNullOrWhiteSpace(installedPath) ? "Installed" : "Not found"),
                        DetectionSource = detectionSource,
                        Platform = DetectGamePlatform(definition, installedPath),
                        PresetApplied = presetApplied,
                        PresetStatus = presetApplied ? "Applied" : (restored && String.Equals(appliedGameId, definition.Id, StringComparison.OrdinalIgnoreCase) ? "Restored" : "Not applied"),
                        LastAppliedAt = presetApplied ? appliedAt : "",
                        BackupFiles = presetApplied ? backupFiles : 0,
                        Restored = restored && String.Equals(appliedGameId, definition.Id, StringComparison.OrdinalIgnoreCase),
                        SelectedSafeCount = presetApplied && appliedSafeOptions.Count > 0 ? appliedSafeOptions.Count : 0,
                        SelectedExperimentalCount = presetApplied ? appliedExperimentalOptions.Count : 0,
                        SafeOptions = new List<string>(definition.SafeOptions ?? new string[0]),
                        ExperimentalOptions = new List<string>(definition.ExperimentalOptions ?? new string[0]),
                        SafeOptimizations = new List<string>(definition.SafeOptions ?? new string[0]),
                        ExperimentalOptimizations = new List<string>(definition.ExperimentalOptions ?? new string[0])
                    });
                }
            }
            finally
            {
                foreach (Process process in processes) { try { process.Dispose(); } catch { } }
            }
            return output;
        }

        private static string DetectGamePlatform(GamePresetDefinition definition, string installPath)
        {
            string id = (definition == null ? "" : definition.Id ?? "").Trim().ToLowerInvariant();
            string path = (installPath ?? "").Trim().ToLowerInvariant();
            if (path.IndexOf("steamapps", StringComparison.OrdinalIgnoreCase) >= 0) { return "Steam"; }
            if (path.IndexOf("epic", StringComparison.OrdinalIgnoreCase) >= 0) { return "Epic Games"; }
            if (path.IndexOf("riot games", StringComparison.OrdinalIgnoreCase) >= 0) { return "Riot Client"; }
            if (path.IndexOf("battle.net", StringComparison.OrdinalIgnoreCase) >= 0) { return "Battle.net"; }
            if (path.IndexOf("ubisoft", StringComparison.OrdinalIgnoreCase) >= 0) { return "Ubisoft Connect"; }
            if (id == "valorant") { return "Riot Client"; }
            if (id == "cs2") { return "Steam"; }
            if (id == "eafc26") { return "EA App"; }
            if (id == "bf6") { return "EA App"; }
            return "";
        }

        private static int RefreshGameDiscoveryCache()
        {
            int found = 0;
            foreach (GamePresetDefinition definition in GetGamePresetDefinitions())
            {
                try
                {
                    string path = FindGameInstallPath(definition);
                    if (String.IsNullOrWhiteSpace(path)) { continue; }
                    SaveManualGameInstallPath(definition, path);
                    found++;
                }
                catch { }
            }
            return found;
        }
        private static string FindGameInstallPath(GamePresetDefinition definition)
        {
            if (definition == null) { return ""; }
            string running = FindGameInstallPathFromRunningProcess(definition);
            if (!String.IsNullOrWhiteSpace(running)) { return running; }
            string manual = FindManualGameInstallPath(definition);
            if (!String.IsNullOrWhiteSpace(manual)) { return manual; }

            foreach (string candidate in BuildGameInstallCandidates(definition))
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(candidate)) { continue; }
                    string normalized = candidate.Trim().Trim('"');
                    if (File.Exists(normalized)) { return normalized; }
                    if (Directory.Exists(normalized)) { return normalized; }
                }
                catch { }
            }
            return "";
        }

        private static string FindGameInstallPathFromRunningProcess(GamePresetDefinition definition)
        {
            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    using (process)
                    {
                        string name = "";
                        try { name = process.ProcessName ?? ""; } catch { }
                        if (!NameInList(name, definition.ProcessNames)) { continue; }
                        if (IsKnownLauncherProcessForReactive(name)) { continue; }
                        string path = TryGetProcessPath(process);
                        if (!String.IsNullOrWhiteSpace(path)) { return path; }
                    }
                }
            }
            catch { }
            return "";
        }

        private static List<string> BuildGameInstallCandidates(GamePresetDefinition definition)
        {
            List<string> candidates = new List<string>();
            Action<string> add = delegate(string value)
            {
                if (!String.IsNullOrWhiteSpace(value) && !candidates.Contains(value, StringComparer.OrdinalIgnoreCase)) { candidates.Add(value); }
            };

            foreach (string root in BuildGameInstallRoots())
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) { continue; }
                    if (MatchesGamePath(root, definition)) { add(root); }
                    foreach (string dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (MatchesGamePath(dir, definition) || ContainsGameExecutable(dir, definition)) { add(dir); }
                    }
                }
                catch { }
            }

            AddShortcutGameCandidates(definition, add);
            return candidates;
        }

        private static bool MatchesGamePath(string value, GamePresetDefinition definition)
        {
            string text = value ?? "";
            foreach (string keyword in definition.InstallKeywords ?? new string[0])
            {
                if (!String.IsNullOrWhiteSpace(keyword) && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            }
            foreach (string process in definition.ProcessNames ?? new string[0])
            {
                if (!String.IsNullOrWhiteSpace(process) && text.IndexOf(process, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            }
            return false;
        }

        private static IEnumerable<string> EnumerateFilesLimited(string root, string pattern, int maxDepth, int maxFiles)
        {
            if (String.IsNullOrWhiteSpace(root)) { yield break; }
            try { if (!Directory.Exists(root)) { yield break; } } catch { yield break; }
            Queue<Tuple<string, int>> queue = new Queue<Tuple<string, int>>();
            queue.Enqueue(Tuple.Create(root, 0));
            int emitted = 0;
            while (queue.Count > 0 && emitted < maxFiles)
            {
                Tuple<string, int> item = queue.Dequeue();
                string dir = item.Item1;
                int depth = item.Item2;
                string[] files = new string[0];
                try { files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly); } catch { }
                foreach (string file in files)
                {
                    yield return file;
                    emitted++;
                    if (emitted >= maxFiles) { yield break; }
                }
                if (depth >= maxDepth) { continue; }
                string[] dirs = new string[0];
                try { dirs = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly); } catch { }
                foreach (string child in dirs) { queue.Enqueue(Tuple.Create(child, depth + 1)); }
            }
        }
        private static bool ContainsGameExecutable(string directory, GamePresetDefinition definition)
        {
            try
            {
                foreach (string process in definition.ProcessNames ?? new string[0])
                {
                    if (String.IsNullOrWhiteSpace(process)) { continue; }
                    string exe = process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? process : process + ".exe";
                    if (File.Exists(Path.Combine(directory, exe))) { return true; }
                    foreach (string file in EnumerateFilesLimited(directory, exe, 4, 700))
                    {
                        if (file.IndexOf("launcher", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static List<string> BuildGameInstallRoots()
        {
            List<string> roots = new List<string>();
            Action<string> add = delegate(string value)
            {
                if (!String.IsNullOrWhiteSpace(value) && !roots.Contains(value, StringComparer.OrdinalIgnoreCase)) { roots.Add(value); }
            };
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            add(Path.Combine(pfx86, "Steam", "steamapps", "common"));
            add(Path.Combine(pf, "Steam", "steamapps", "common"));
            AddSteamLibraryRoots(add);
            add(Path.Combine(pf, "EA Games"));
            add(Path.Combine(pfx86, "EA Games"));
            add(Path.Combine(pf, "Electronic Arts", "Games"));
            add(Path.Combine(pfx86, "Origin Games"));
            add(Path.Combine(pf, "Epic Games"));
            AddEpicInstallRoots(add);
            add(Path.Combine(pf, "Riot Games"));
            add(Path.Combine(pfx86, "Riot Games"));
            AddRiotInstallRoots(add);
            AddEaInstallRoots(add);
            AddDriveLibraryRoots(add);
            add(Path.Combine(pd, "Battle.net"));
            return roots;
        }

        private static void AddSteamLibraryRoots(Action<string> add)
        {
            foreach (string steamRoot in GetSteamRoots())
            {
                string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                try
                {
                    if (File.Exists(vdf))
                    {
                        string text = File.ReadAllText(vdf);
                        foreach (Match match in Regex.Matches(text, "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\""))
                        {
                            string path = Regex.Unescape(match.Groups[1].Value.Replace("\\\\", "\\"));
                            add(Path.Combine(path, "steamapps", "common"));
                        }
                    }
                }
                catch { }
                add(Path.Combine(steamRoot, "steamapps", "common"));
            }
        }

        private static IEnumerable<string> GetSteamRoots()
        {
            List<string> roots = new List<string>();
            Action<string> add = delegate(string value)
            {
                if (!String.IsNullOrWhiteSpace(value) && !roots.Contains(value, StringComparer.OrdinalIgnoreCase)) { roots.Add(value); }
            };
            try { using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) { add(Convert.ToString(key == null ? null : key.GetValue("SteamPath"))); } } catch { }
            try { using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")) { add(Convert.ToString(key == null ? null : key.GetValue("InstallPath"))); } } catch { }
            add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
            return roots;
        }
        private static void AddEaInstallRoots(Action<string> add)
        {
            string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] roots = new string[]
            {
                Path.Combine(pd, "EA Desktop"),
                Path.Combine(pd, "Electronic Arts"),
                Path.Combine(local, "Electronic Arts"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EA Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Origin Games")
            };
            foreach (string root in roots)
            {
                try
                {
                    if (!Directory.Exists(root)) { continue; }
                    add(root);
                    foreach (string file in EnumerateFilesLimited(root, "*.*", 4, 900))
                    {
                        string ext = Path.GetExtension(file);
                        if (!String.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase) && !String.Equals(ext, ".mfst", StringComparison.OrdinalIgnoreCase) && !String.Equals(ext, ".xml", StringComparison.OrdinalIgnoreCase)) { continue; }
                        FileInfo info = new FileInfo(file);
                        if (info.Length > 1024 * 1024 * 2) { continue; }
                        string text = File.ReadAllText(file, Encoding.UTF8);
                        foreach (Match match in Regex.Matches(text, "[A-Za-z]:\\\\(?:[^\\\"\\r\\n])+"))
                        {
                            string path = match.Value.Replace("\\\\", "\\").Trim();
                            if (File.Exists(path)) { add(Path.GetDirectoryName(path)); }
                            else if (Directory.Exists(path)) { add(path); }
                        }
                    }
                }
                catch { }
            }
        }

        private static void AddDriveLibraryRoots(Action<string> add)
        {
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) { continue; }
                    string root = drive.RootDirectory.FullName;
                    add(Path.Combine(root, "SteamLibrary", "steamapps", "common"));
                    add(Path.Combine(root, "Steam", "steamapps", "common"));
                    add(Path.Combine(root, "EA Games"));
                    add(Path.Combine(root, "Epic Games"));
                    add(Path.Combine(root, "Riot Games"));
                    add(Path.Combine(root, "XboxGames"));
                    add(Path.Combine(root, "Games"));
                }
            }
            catch { }
        }

        private static void AddEpicInstallRoots(Action<string> add)
        {
            string manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
            try
            {
                if (!Directory.Exists(manifests)) { return; }
                foreach (string file in Directory.EnumerateFiles(manifests, "*.item", SearchOption.TopDirectoryOnly))
                {
                    string text = File.ReadAllText(file);
                    Match m = Regex.Match(text, "\\\"InstallLocation\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
                    if (m.Success) { add(Regex.Unescape(m.Groups[1].Value)); }
                }
            }
            catch { }
        }

        private static void AddRiotInstallRoots(Action<string> add)
        {
            string json = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "RiotClientInstalls.json");
            try
            {
                if (File.Exists(json))
                {
                    foreach (Match match in Regex.Matches(File.ReadAllText(json), "[A-Za-z]:\\\\(?:[^\\\"\\r\\n])+"))
                    {
                        string path = match.Value.Replace("\\\\", "\\");
                        string dir = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                        if (!String.IsNullOrWhiteSpace(dir)) { add(dir); }
                    }
                }
            }
            catch { }
        }

        private static void AddShortcutGameCandidates(GamePresetDefinition definition, Action<string> add)
        {
            foreach (string root in GetShortcutSearchRoots())
            {
                try
                {
                    if (!Directory.Exists(root)) { continue; }
                    foreach (string shortcut in EnumerateFilesLimited(root, "*.lnk", 3, 600))
                    {
                        if (!MatchesGamePath(shortcut, definition)) { continue; }
                        string target = TryGetShortcutTarget(shortcut);
                        if (String.IsNullOrWhiteSpace(target)) { continue; }
                        if (File.Exists(target)) { add(target); add(Path.GetDirectoryName(target)); }
                        else if (Directory.Exists(target)) { add(target); }
                    }
                }
                catch { }
            }
        }

        private static IEnumerable<string> GetShortcutSearchRoots()
        {
            yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "Start Menu", "Programs");
        }

        private static string TryGetShortcutTarget(string shortcutPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) { return ""; }
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                string target = Convert.ToString(shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null));
                try { Marshal.FinalReleaseComObject(shortcut); } catch { }
                try { Marshal.FinalReleaseComObject(shell); } catch { }
                return target ?? "";
            }
            catch { return ""; }
        }

        private void SelectGameFolderFromMessage(IDictionary<string, object> message)
        {
            string gameId = GetMapString(message, "gameId");
            GamePresetDefinition definition = GetGamePresetDefinitions().Find(delegate(GamePresetDefinition item) { return String.Equals(item.Id, gameId, StringComparison.OrdinalIgnoreCase); });
            if (definition == null) { AppendOperationalLog("action=game-folder status=unknown"); SendState(); return; }

            string selectedPath = "";
            Action choose = delegate
            {
                using (System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = "Escolha a pasta onde " + definition.Name + " está instalado";
                    dialog.ShowNewFolderButton = false;
                    string current = FindManualGameInstallPath(definition);
                    if (String.IsNullOrWhiteSpace(current)) { current = FindManualGameInstallPath(definition); }
                    if (!String.IsNullOrWhiteSpace(current))
                    {
                        string folder = File.Exists(current) ? Path.GetDirectoryName(current) : current;
                        if (!String.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)) { dialog.SelectedPath = folder; }
                    }
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) { selectedPath = dialog.SelectedPath; }
                }
            };
            try
            {
                if (Dispatcher.CheckAccess()) { choose(); } else { Dispatcher.Invoke(choose); }
            }
            catch (Exception ex)
            {
                AppendOperationalLog("action=game-folder game=" + definition.ShortName.Replace(' ', '_') + " status=failed error=" + SanitizeLogToken(ex.Message));
                SendState();
                return;
            }

            if (!String.IsNullOrWhiteSpace(selectedPath) && !IsValidGameInstallSelection(selectedPath, definition))
            {
                AppendOperationalLog("action=game-folder game=" + definition.ShortName.Replace(' ', '_') + " status=invalid path=" + SanitizeLogToken(selectedPath));
                selectedPath = "";
            }

            if (!String.IsNullOrWhiteSpace(selectedPath))
            {
                SaveManualGameInstallPath(definition, selectedPath);
                AppendOperationalLog("action=game-folder game=" + definition.ShortName.Replace(' ', '_') + " status=saved path=" + SanitizeLogToken(selectedPath));
            }
            else
            {
                AppendOperationalLog("action=game-folder game=" + definition.ShortName.Replace(' ', '_') + " status=cancelled");
            }
            SendState();
        }

        private static bool IsValidGameInstallSelection(string selectedPath, GamePresetDefinition definition)
        {
            if (definition == null || String.IsNullOrWhiteSpace(selectedPath)) { return false; }
            string path = selectedPath.Trim().Trim('"');
            try
            {
                if (File.Exists(path))
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    return NameInList(name, definition.ProcessNames) || MatchesGamePath(path, definition);
                }
                if (!Directory.Exists(path)) { return false; }
                return MatchesGamePath(path, definition) || ContainsGameExecutable(path, definition);
            }
            catch { return false; }
        }

        private static string GetManualGameInstallPathFile()
        {
            return Path.Combine(outputsPath, "game-paths.user.json");
        }

        private static Dictionary<string, string> LoadManualGameInstallPaths()
        {
            Dictionary<string, string> output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in new[] { GetManualGameInstallPathFile(), GetManualGameInstallPathFile() + ".lastgood" })
            {
                try
                {
                    if (!File.Exists(path)) { continue; }
                    IDictionary<string, object> root = JsonCompat.DeserializeObject(File.ReadAllText(path, Encoding.UTF8));
                    if (root == null) { continue; }
                    foreach (KeyValuePair<string, object> pair in root)
                    {
                        string value = Convert.ToString(pair.Value, CultureInfo.InvariantCulture);
                        if (!String.IsNullOrWhiteSpace(pair.Key) && !String.IsNullOrWhiteSpace(value)) { output[pair.Key] = value; }
                    }
                    if (output.Count > 0) { break; }
                }
                catch { }
            }
            return output;
        }

        private static string FindManualGameInstallPath(GamePresetDefinition definition)
        {
            if (definition == null || String.IsNullOrWhiteSpace(definition.Id)) { return ""; }
            Dictionary<string, string> paths = LoadManualGameInstallPaths();
            string value = "";
            if (!paths.TryGetValue(definition.Id, out value) || String.IsNullOrWhiteSpace(value)) { return ""; }
            value = value.Trim().Trim('"');
            try { return Directory.Exists(value) || File.Exists(value) ? value : ""; } catch { return ""; }
        }

        private static void SaveManualGameInstallPath(GamePresetDefinition definition, string selectedPath)
        {
            if (definition == null || String.IsNullOrWhiteSpace(definition.Id) || String.IsNullOrWhiteSpace(selectedPath)) { return; }
            Dictionary<string, string> paths = LoadManualGameInstallPaths();
            paths[definition.Id] = selectedPath.Trim();
            Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in paths) { root[pair.Key] = pair.Value; }
            string path = GetManualGameInstallPathFile();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AtomicWriteJsonMap(path, root);
            try { File.Copy(path, path + ".lastgood", true); } catch { }
        }

        private RunResult ApplyGamePresetFromMessage(IDictionary<string, object> message)
        {
            string gameId = GetMapString(message, "gameId");
            GamePresetDefinition definition = GetGamePresetDefinitions().Find(delegate(GamePresetDefinition item) { return String.Equals(item.Id, gameId, StringComparison.OrdinalIgnoreCase); });
            if (definition == null) { return new RunResult(1, "Preset de jogo desconhecido."); }
            string installPath = FindGameInstallPath(definition);
            if (String.IsNullOrWhiteSpace(installPath))
            {
                return new RunResult(2, "Localize a pasta de " + definition.Name + " antes de aplicar o preset. Assim o Smart Nap só altera arquivos do jogo correto.");
            }

            List<string> selectedSafeOptions = GetMapStringList(message, "safeOptions");
            List<string> selectedExperimentalOptions = GetMapStringList(message, "experimentalOptions");
            if (selectedSafeOptions.Count == 0) { selectedSafeOptions = new List<string>(definition.SafeOptions ?? new string[0]); }
            bool experimental = selectedExperimentalOptions.Count > 0 || GetBool(message, "experimental");
            int backupFiles = EnsureGamePresetFileBackups(definition);

            SaveGamePresetState(definition, experimental, selectedSafeOptions, selectedExperimentalOptions, backupFiles);
            AppendOperationalLog("action=game-preset game=" + definition.ShortName.Replace(' ', '_') + " safe=" + selectedSafeOptions.Count.ToString(CultureInfo.InvariantCulture) + " experimental=" + selectedExperimentalOptions.Count.ToString(CultureInfo.InvariantCulture) + " backups=" + backupFiles.ToString(CultureInfo.InvariantCulture) + " session=unchanged");
            return new RunResult(0, "Preset de jogo salvo: " + definition.Name + ". O modo atual do motor foi mantido.");
        }

        private void SaveGamePolicy(string processName, string policy)
        {
            Dictionary<string, object> message = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            message["policy"] = policy;
            message["processName"] = processName;
            message["key"] = "name:" + (processName ?? "").Trim().ToLowerInvariant();
            message["path"] = "";
            SetAppPolicyFromMessage(message);
        }

        private static void SaveGamePresetState(GamePresetDefinition definition, bool experimental, List<string> safeOptions, List<string> experimentalOptions, int backupFiles)
        {
            Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            root["Timestamp"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            root["LastGameId"] = definition.Id;
            root["LastGameName"] = definition.Name;
            root["Experimental"] = experimental;
            root["SafeOptions"] = safeOptions == null || safeOptions.Count == 0 ? new List<string>(definition.SafeOptions ?? new string[0]) : new List<string>(safeOptions);
            root["ExperimentalOptions"] = experimentalOptions == null ? new List<string>() : new List<string>(experimentalOptions);
            root["BackupFiles"] = backupFiles;
            root["Restored"] = false;
            AtomicWriteJsonMap(Path.Combine(outputsPath, "game-presets.state.json"), root);
        }

        private static string GetGamePresetBackupRoot()
        {
            return Path.Combine(outputsPath, "game-preset-backups");
        }

        private static string HashGamePresetTarget(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes((value ?? "").Trim().ToLowerInvariant()));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < bytes.Length && i < 16; i++) { sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture)); }
                return sb.ToString();
            }
        }

        private static void AddExistingFile(List<string> files, string path)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(path) && File.Exists(path) && !files.Contains(path, StringComparer.OrdinalIgnoreCase)) { files.Add(path); }
            }
            catch { }
        }

        private static List<string> BuildGamePresetBackupCandidates(GamePresetDefinition definition)
        {
            List<string> files = new List<string>();
            string id = (definition == null ? "" : definition.Id ?? "").Trim().ToLowerInvariant();
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string install = definition == null ? "" : FindGameInstallPath(definition);

            if (id == "bf6")
            {
                foreach (string folder in new[] { "Battlefield 6", "Battlefield6", "Battlefield 2042" })
                {
                    string settings = Path.Combine(docs, folder, "settings");
                    AddExistingFile(files, Path.Combine(settings, "PROFSAVE_profile"));
                    AddExistingFile(files, Path.Combine(settings, "PROFSAVE"));
                    AddExistingFile(files, Path.Combine(settings, "PROFSAVE_tmp"));
                }
                AddExistingFile(files, Path.Combine(install, "user.cfg"));
            }
            else if (id == "eafc26")
            {
                foreach (string folder in new[] { "FC 26", "EA SPORTS FC 26", "FC 25", "EA SPORTS FC 25", "FC 24", "EA SPORTS FC 24" })
                {
                    string root = Path.Combine(docs, folder);
                    AddExistingFile(files, Path.Combine(root, "fcsetup.ini"));
                    AddExistingFile(files, Path.Combine(root, "fifasetup.ini"));
                    AddExistingFile(files, Path.Combine(root, "settings.ini"));
                    AddExistingFile(files, Path.Combine(root, "buttonDataSetup.ini"));
                }
            }
            else if (id == "cs2")
            {
                foreach (string root in BuildGameInstallRoots())
                {
                    string steam = root;
                    if (steam.EndsWith(Path.Combine("steamapps", "common"), StringComparison.OrdinalIgnoreCase))
                    {
                        DirectoryInfo steamApps = Directory.GetParent(steam);
                        DirectoryInfo steamRoot = steamApps == null ? null : steamApps.Parent;
                        string userData = steamRoot == null ? "" : Path.Combine(steamRoot.FullName, "userdata");
                        try
                        {
                            if (Directory.Exists(userData))
                            {
                                foreach (string cfg in Directory.EnumerateFiles(userData, "*.cfg", SearchOption.AllDirectories))
                                {
                                    if (cfg.IndexOf(Path.Combine("730", "local", "cfg"), StringComparison.OrdinalIgnoreCase) >= 0) { AddExistingFile(files, cfg); }
                                }
                                foreach (string txt in Directory.EnumerateFiles(userData, "*.txt", SearchOption.AllDirectories))
                                {
                                    if (txt.IndexOf(Path.Combine("730", "local", "cfg"), StringComparison.OrdinalIgnoreCase) >= 0) { AddExistingFile(files, txt); }
                                }
                            }
                        }
                        catch { }
                    }
                }
                AddExistingFile(files, Path.Combine(install, "game", "csgo", "cfg", "autoexec.cfg"));
            }
            else if (id == "valorant")
            {
                string configRoot = Path.Combine(local, "VALORANT", "Saved", "Config");
                try
                {
                    if (Directory.Exists(configRoot))
                    {
                        foreach (string file in Directory.EnumerateFiles(configRoot, "GameUserSettings.ini", SearchOption.AllDirectories)) { AddExistingFile(files, file); }
                    }
                }
                catch { }
            }

            return files;
        }

        private static int EnsureGamePresetFileBackups(GamePresetDefinition definition)
        {
            List<string> files = BuildGamePresetBackupCandidates(definition);
            int ready = 0;
            foreach (string target in files)
            {
                try
                {
                    string id = (definition == null ? "unknown" : definition.Id ?? "unknown").Trim().ToLowerInvariant();
                    string itemDir = Path.Combine(GetGamePresetBackupRoot(), id, HashGamePresetTarget(target));
                    string backupPath = Path.Combine(itemDir, "original.bin");
                    string targetPath = Path.Combine(itemDir, "target.txt");
                    Directory.CreateDirectory(itemDir);
                    if (!File.Exists(backupPath)) { File.Copy(target, backupPath, false); }
                    if (!File.Exists(targetPath)) { AtomicWriteAllText(targetPath, target, Encoding.UTF8); }
                    ready++;
                }
                catch { }
            }
            return ready;
        }

        private static int RestoreGamePresetFileBackups(string gameId)
        {
            string root = GetGamePresetBackupRoot();
            if (!Directory.Exists(root)) { return 0; }
            int restored = 0;
            string normalized = (gameId ?? "").Trim().ToLowerInvariant();
            try
            {
                foreach (string gameDir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                {
                    string currentId = Path.GetFileName(gameDir) ?? "";
                    if (!String.IsNullOrWhiteSpace(normalized) && !String.Equals(currentId, normalized, StringComparison.OrdinalIgnoreCase)) { continue; }
                    foreach (string itemDir in Directory.EnumerateDirectories(gameDir, "*", SearchOption.TopDirectoryOnly))
                    {
                        string backupPath = Path.Combine(itemDir, "original.bin");
                        string targetPathFile = Path.Combine(itemDir, "target.txt");
                        if (!File.Exists(backupPath) || !File.Exists(targetPathFile)) { continue; }
                        string target = File.ReadAllText(targetPathFile, Encoding.UTF8).Trim();
                        if (String.IsNullOrWhiteSpace(target)) { continue; }
                        string dir = Path.GetDirectoryName(target);
                        if (!String.IsNullOrWhiteSpace(dir)) { Directory.CreateDirectory(dir); }
                        File.Copy(backupPath, target, true);
                        restored++;
                    }
                }
            }
            catch { }
            return restored;
        }

        private RunResult RestoreGamePresetFromMessage(IDictionary<string, object> message)
        {
            string gameId = GetMapString(message, "gameId");
            if (!String.IsNullOrWhiteSpace(gameId))
            {
                GamePresetDefinition selected = GetGamePresetDefinitions().Find(delegate(GamePresetDefinition item) { return String.Equals(item.Id, gameId, StringComparison.OrdinalIgnoreCase); });
                if (selected == null) { return new RunResult(1, "Preset de jogo desconhecido."); }
            }

            int restored = RestoreGamePresetFileBackups(gameId);
            SaveGamePresetRestoreState(String.IsNullOrWhiteSpace(gameId) ? "all" : gameId, restored);
            AppendOperationalLog("action=game-preset-restore target=" + (String.IsNullOrWhiteSpace(gameId) ? "all" : gameId) + " files=" + restored.ToString(CultureInfo.InvariantCulture) + " session=unchanged");
            if (restored <= 0) { return new RunResult(0, "Nenhum arquivo alterado pela aba Jogos para restaurar."); }
            return new RunResult(0, "Arquivos do preset restaurados: " + restored.ToString(CultureInfo.InvariantCulture) + ".");
        }

        private static void SaveGamePresetRestoreState(string target, int files)
        {
            Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            root["Timestamp"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            root["LastGameId"] = target;
            root["LastGameName"] = target;
            root["Experimental"] = false;
            root["SafeOptions"] = new List<string>();
            root["ExperimentalOptions"] = new List<string>();
            root["Restored"] = true;
            root["RestoredFiles"] = files;
            AtomicWriteJsonMap(Path.Combine(outputsPath, "game-presets.state.json"), root);
        }

        private static bool ShouldShowGameBetaWelcome()
        {
            return !ReadUiFlag("GameBetaWelcomeSeen");
        }

        private static void MarkGameBetaWelcomeSeen()
        {
            SaveUiFlag("GameBetaWelcomeSeen", true);
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
                    activeDetail = result.ExitCode == 0 ? (String.IsNullOrWhiteSpace(result.Output) ? BuildResultText() : ShortError(result.Output)) : ShortError(result.Output);
                    runState = result.ExitCode == 0 ? "DONE" : "ERROR";
                    activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + (result.ExitCode == 0 ? "  OK   " + CleanEventText(successMessage) : "  FAIL " + CleanEventText(ShortError(result.Output)));
                    SendState();
                    if (result.ExitCode != 0 && !ShouldSuppressRunModal(result.Output))
                    {
                        System.Windows.MessageBox.Show(ShortError(result.Output), AppName, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }));
            });
        }

        private void ToggleMotorFromButton()
        {
            if (busy) { return; }

            bool installed = IsAutomaticEngineEnabled();
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
                    bool setupDeferred = result.ExitCode != 0 && !stopped && ShouldSuppressRunModal(result.Output);
                    if (actionTimer != null) { actionTimer.Stop(); }
                    activeRunControl = null;
                    busy = false;
                    activeRunCanStop = false;
                    activeTitle = stopped ? "Otimizacao parada" : ((result.ExitCode == 0 || setupDeferred) ? "Otimizacao concluida" : "Action failed");
                    activeDetail = stopped ? "O passe manual foi interrompido." : (result.ExitCode == 0 ? BuildResultText() : (setupDeferred ? BuildDeferredRunDetail(result.Output) : ShortError(result.Output)));
                    runState = stopped ? "STOPPED" : ((result.ExitCode == 0 || setupDeferred) ? "DONE" : "ERROR");
                    activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + (stopped ? "  STOP passe manual interrompido" : ((result.ExitCode == 0 || setupDeferred) ? "  OK   passe manual aplicado: " + BuildResultText() : "  FAIL passe manual falhou"));
                    SendState();
                    if (result.ExitCode != 0 && !stopped && !ShouldSuppressRunModal(result.Output))
                    {
                        System.Windows.MessageBox.Show(ShortError(result.Output), AppName, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }));
            });
        }

        private void RunPreviewActionWithFeedback()
        {
            if (busy) { return; }
            busy = true;
            activeRunCanStop = false;
            activeTitle = "Simulando passe seguro";
            activeDetail = "Calculando o que mudaria sem alterar prioridade, memoria, IO ou EcoQoS.";
            runState = "PREVIEW";
            activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "  PREVIEW  simulacao iniciada";
            SendState();

            ThreadPool.QueueUserWorkItem(delegate
            {
                RunResult result = RunPreviewNow();
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    busy = false;
                    PreviewSummary preview = LoadPreviewSummary();
                    activeTitle = result.ExitCode == 0 ? "Preview pronto" : "Preview falhou";
                    activeDetail = result.ExitCode == 0 ? preview.Detail : ShortError(result.Output);
                    runState = result.ExitCode == 0 ? "DONE" : "ERROR";
                    activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + (result.ExitCode == 0 ? "  OK   preview: " + preview.ShortText : "  FAIL preview falhou");
                    SendState();
                    if (result.ExitCode != 0 && !ShouldSuppressRunModal(result.Output))
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
            SetCurrentProcessBackgroundMode(false, "dashboard-visible");
            if (refreshTimer != null && !refreshTimer.IsEnabled) { refreshTimer.Start(); }
            if (liveTimer != null && !liveTimer.IsEnabled) { liveTimer.Start(); }
        }

        private void StopDashboardActivity()
        {
            if (refreshTimer != null) { refreshTimer.Stop(); }
            if (liveTimer != null) { liveTimer.Stop(); }
            SetCurrentProcessBackgroundMode(true, "dashboard-hidden");
        }

        private bool ShouldDeferLowImpactState()
        {
            bool hiddenOrMinimized = !IsVisible || WindowState == System.Windows.WindowState.Minimized;
            bool interactive = IsVisible && WindowState != System.Windows.WindowState.Minimized && IsActive;
            if (busy || interactive)
            {
                lowImpactRuntimeActive = false;
                lowImpactRuntimeReason = interactive ? "dashboard-active" : "operation-active";
                lowImpactRuntimeCadenceSeconds = 1;
                SetCurrentProcessBackgroundMode(false, lowImpactRuntimeReason);
                return false;
            }

            SessionAgentSnapshot agent = LoadSessionAgentSnapshot();
            bool gameOrFullscreen = agent != null && agent.Available && (agent.ForegroundIsGame || agent.ForegroundFullscreen || IsMemoryStabilityGamingContext(agent));
            bool active = hiddenOrMinimized || gameOrFullscreen;
            lowImpactRuntimeActive = active;
            lowImpactRuntimeReason = !active ? "normal" : (hiddenOrMinimized ? "dashboard-hidden" : "game-foreground");
            lowImpactRuntimeCadenceSeconds = !active ? 1 : (hiddenOrMinimized ? 8 : 4);
            SetCurrentProcessBackgroundMode(active, lowImpactRuntimeReason);
            if (!active) { return false; }
            return (DateTime.UtcNow - lastDashboardStateSentUtc).TotalSeconds < lowImpactRuntimeCadenceSeconds;
        }

        private void SendState()
        {
            if (!webReady || webView.CoreWebView2 == null)
            {
                return;
            }
            if (ShouldDeferLowImpactState())
            {
                return;
            }

            try
            {
                WebDashboardState state = BuildState();
                state.LowImpactRuntimeAvailable = true;
                state.LowImpactRuntimeActive = lowImpactRuntimeActive;
                state.LowImpactRuntimeReason = lowImpactRuntimeReason ?? "";
                state.LowImpactRuntimeCadenceSeconds = lowImpactRuntimeCadenceSeconds;
                lastDashboardStateSentUtc = DateTime.UtcNow;
                string json = JsonSerializer.Serialize(state);
                webView.CoreWebView2.PostWebMessageAsJson(json);
                string script = "try{ if(window.smartNapUpdate){ window.smartNapUpdate(" + json + "); } }catch(e){ console.error('Smart Nap direct state failed', e); }";
                _ = webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
            }
        }

        private WebDashboardState BuildState()
        {
            bool autoInstalled = IsAutomaticEngineEnabled();
            bool startupInstalled = IsStartupInstalled();
            bool learningEnabled = IsSmartLearningEnabled();
            bool behaviorEnabled = IsBehaviorEngineEnabled();
            bool networkUdpGuardEnabled = IsNetworkUdpGuardEnabled();
            List<WebManagerRow> rows = LoadManagerRows();
            ScoreMeta scoreMeta = LoadScoreMeta();
            CoreServiceSnapshot coreService = LoadCoreServiceSnapshot();
            SessionAgentSnapshot sessionAgent = LoadSessionAgentSnapshot();
            ReconcileNetworkUdpGuardMeta(scoreMeta, rows, networkUdpGuardEnabled);
            string line = ReadLastApplyLogLine();
            string targets = line == "No log yet." ? "" : ExtractLogValue(line, "targets");
            string delta = line == "No log yet." ? "" : ExtractLogValue(line, "deltaMB");
            string top = line == "No log yet." ? "" : ExtractLogValue(line, "top");
            string heartbeat = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            string lastEventAge = BuildLastEventAgeText();
            string nextPass = BuildNextPassText(line, autoInstalled);
            HardwareSnapshot hardware = GetHardwareSnapshot();

            WebDashboardState state = new WebDashboardState();
            state.AppVersion = AppVersion;
            state.Creator = CreatorLine;
            state.Language = String.IsNullOrWhiteSpace(uiLanguage) ? "" : uiLanguage;
            state.FirstRun = String.IsNullOrWhiteSpace(uiLanguage);
            state.LowImpactRuntimeAvailable = true;
            state.LowImpactRuntimeActive = lowImpactRuntimeActive;
            state.LowImpactRuntimeReason = lowImpactRuntimeReason ?? "";
            state.LowImpactRuntimeCadenceSeconds = lowImpactRuntimeCadenceSeconds;
            state.AutoMode = autoInstalled;
            state.Startup = startupInstalled;
            state.SessionMode = GetSessionMode();
            PowerPlanSnapshot activePowerPlan = GetActivePowerPlan();
            PowerPlanSnapshot previousPowerPlan = LoadPreviousPowerPlan();
            PowerPlanSnapshot recommendedPowerPlan = GetRecommendedPowerPlanForSessionMode(state.SessionMode);
            state.PowerPlanName = activePowerPlan == null ? "" : activePowerPlan.Name;
            state.PowerPlanGuid = activePowerPlan == null ? "" : activePowerPlan.Guid;
            state.powerPlanName = state.PowerPlanName;
            state.powerPlanGuid = state.PowerPlanGuid;
            state.RecommendedPowerPlanName = recommendedPowerPlan == null ? "" : recommendedPowerPlan.Name;
            state.RecommendedPowerPlanGuid = recommendedPowerPlan == null ? "" : recommendedPowerPlan.Guid;
            state.recommendedPowerPlanName = state.RecommendedPowerPlanName;
            state.recommendedPowerPlanGuid = state.RecommendedPowerPlanGuid;
            state.GamePowerPlanName = SmartNapGamePowerPlanName;
            state.GamePowerPlanGuid = SmartNapGamePowerPlanGuid;
            state.LivePowerPlanName = SmartNapLivePowerPlanName;
            state.LivePowerPlanGuid = SmartNapLivePowerPlanGuid;
            state.PreviousPowerPlanName = previousPowerPlan == null ? "" : previousPowerPlan.Name;
            state.PreviousPowerPlanGuid = previousPowerPlan == null ? "" : previousPowerPlan.Guid;
            state.EnergyIdleGuardEnabled = LoadEnergyIdleGuardEnabled();
            state.EnergyIdleGuardConfigured = LoadEnergyIdleGuardConfigured();
            state.EnergyIdleGuardMinutes = LoadEnergyIdleGuardMinutes();
            state.AdaptiveExclusions = IsAdaptiveExclusionsEnabled();
            state.NetworkUdpGuard = networkUdpGuardEnabled;
            state.NetworkUdpGuardActive = networkUdpGuardEnabled && scoreMeta.NetworkUdpGuardActive;
            state.NetworkUdpGuardMode = networkUdpGuardEnabled
                ? (String.IsNullOrWhiteSpace(scoreMeta.NetworkUdpGuardMode) ? "Armed" : scoreMeta.NetworkUdpGuardMode)
                : "Off";
            state.NetworkUdpGuardGame = networkUdpGuardEnabled ? scoreMeta.NetworkUdpGuardGame : String.Empty;
            state.NetworkUdpGuardEndpoints = networkUdpGuardEnabled ? scoreMeta.NetworkUdpGuardEndpoints : 0;
            state.NetworkUdpGuardProcessCount = networkUdpGuardEnabled ? scoreMeta.NetworkUdpGuardProcessCount : 0;
            state.NetworkUdpGuardNoStackTweaks = networkUdpGuardEnabled && (scoreMeta.NetworkUdpGuardNoStackTweaks || networkUdpGuardEnabled);
            state.NetworkUdpGuardConfidence = networkUdpGuardEnabled ? scoreMeta.NetworkUdpGuardConfidence : 0;
            state.NetworkUdpGuardConfidenceLabel = networkUdpGuardEnabled ? scoreMeta.NetworkUdpGuardConfidenceLabel : "None";
            state.NetworkUdpGuardReason = networkUdpGuardEnabled ? scoreMeta.NetworkUdpGuardReason : String.Empty;
            state.NetworkUdpGuardShieldMode = networkUdpGuardEnabled ? scoreMeta.NetworkUdpGuardShieldMode : "Off";
            state.NetworkUdpGuardProtectedCount = networkUdpGuardEnabled ? scoreMeta.NetworkUdpGuardProtectedCount : 0;
            state.NetworkUdpGuardQosStatus = networkUdpGuardEnabled ? scoreMeta.NetworkUdpGuardQosStatus : "Off";
            state.NetworkUdpGuardSignals = networkUdpGuardEnabled ? scoreMeta.NetworkUdpGuardSignals : new List<string>();
            state.StreamGuardActive = scoreMeta.StreamGuardActive;
            state.StreamHelperCount = scoreMeta.StreamHelperCount;
            state.StreamGameProtectedCount = scoreMeta.StreamGameProtectedCount;
            state.GpuPressureAvailable = scoreMeta.GpuPressureAvailable;
            state.GpuPressureProvider = scoreMeta.GpuPressureProvider;
            state.GpuPressureLevel = scoreMeta.GpuPressureLevel;
            state.GpuPressureDxgiAvailable = scoreMeta.GpuPressureDxgiAvailable;
            state.GpuPressureAdapterName = scoreMeta.GpuPressureAdapterName;
            state.GpuAdapterDedicatedVideoMemoryMB = scoreMeta.GpuAdapterDedicatedVideoMemoryMB;
            state.GpuAdapterSharedSystemMemoryMB = scoreMeta.GpuAdapterSharedSystemMemoryMB;
            state.GpuAdapterLocalBudgetMB = scoreMeta.GpuAdapterLocalBudgetMB;
            state.GpuAdapterLocalUsageMB = scoreMeta.GpuAdapterLocalUsageMB;
            state.GpuAdapterLocalAvailableMB = scoreMeta.GpuAdapterLocalAvailableMB;
            state.GpuAdapterLocalUsagePercent = scoreMeta.GpuAdapterLocalUsagePercent;
            state.GpuAdapterNonLocalBudgetMB = scoreMeta.GpuAdapterNonLocalBudgetMB;
            state.GpuAdapterNonLocalUsageMB = scoreMeta.GpuAdapterNonLocalUsageMB;
            state.GpuAdapterNonLocalAvailableMB = scoreMeta.GpuAdapterNonLocalAvailableMB;
            state.GpuAdapterDedicatedUsageMB = scoreMeta.GpuAdapterDedicatedUsageMB;
            state.GpuAdapterSharedUsageMB = scoreMeta.GpuAdapterSharedUsageMB;
            state.GpuTotalUtilPercent = scoreMeta.GpuTotalUtilPercent;
            state.GpuTopProcess = scoreMeta.GpuTopProcess;
            state.GpuTopProcessPid = scoreMeta.GpuTopProcessPid;
            state.GpuTopProcessPercent = scoreMeta.GpuTopProcessPercent;
            state.GpuTopProcessDedicatedMB = scoreMeta.GpuTopProcessDedicatedMB;
            state.ShaderBoostEnabled = scoreMeta.ShaderBoostEnabled;
            state.ShaderBoostObserveOnly = scoreMeta.ShaderBoostObserveOnly;
            state.ShaderBoostState = scoreMeta.ShaderBoostState;
            state.ShaderBoostSharedState = scoreMeta.ShaderBoostSharedState;
            state.ShaderBoostReadiness = scoreMeta.ShaderBoostReadiness;
            state.ShaderBoostRecommendation = scoreMeta.ShaderBoostRecommendation;
            state.ShaderBoostGame = scoreMeta.ShaderBoostGame;
            state.ShaderBoostGamePid = scoreMeta.ShaderBoostGamePid;
            state.ShaderBoostGameRoot = scoreMeta.ShaderBoostGameRoot;
            state.ShaderBoostApi = scoreMeta.ShaderBoostApi;
            state.ShaderBoostApiConfidence = scoreMeta.ShaderBoostApiConfidence;
            state.ShaderBoostGpu = scoreMeta.ShaderBoostGpu;
            state.ShaderBoostVendor = scoreMeta.ShaderBoostVendor;
            state.ShaderBoostDriverVersion = scoreMeta.ShaderBoostDriverVersion;
            state.ShaderBoostCacheState = scoreMeta.ShaderBoostCacheState;
            state.ShaderBoostCacheLocatedCount = scoreMeta.ShaderBoostCacheLocatedCount;
            state.ShaderBoostCacheTotalSizeMB = scoreMeta.ShaderBoostCacheTotalSizeMB;
            state.ShaderBoostCacheManager = scoreMeta.ShaderBoostCacheManager;
            state.ShaderBoostCompilationState = scoreMeta.ShaderBoostCompilationState;
            state.ShaderBoostCompilationPossible = scoreMeta.ShaderBoostCompilationPossible;
            state.ShaderBoostPreparationMethod = scoreMeta.ShaderBoostPreparationMethod;
            state.ShaderBoostWarmupState = scoreMeta.ShaderBoostWarmupState;
            state.ShaderBoostSignals = scoreMeta.ShaderBoostSignals;
            state.ShaderBoostDetails = scoreMeta.ShaderBoostDetails;
            state.CpuBoundAssistActive = scoreMeta.CpuBoundAssistActive;
            state.CpuBoundAssistGame = scoreMeta.CpuBoundAssistGame;
            state.CpuBoundAssistGamePid = scoreMeta.CpuBoundAssistGamePid;
            state.CpuBoundAssistConfidence = scoreMeta.CpuBoundAssistConfidence;
            state.CpuBoundAssistReason = scoreMeta.CpuBoundAssistReason;
            state.EngineHealthStatus = scoreMeta.EngineHealthStatus;
            state.EngineHealthSummary = scoreMeta.EngineHealthSummary;
            state.CoreProtocolVersion = coreService.ProtocolVersion;
            state.CoreMinimumSupportedProtocolVersion = coreService.MinimumSupportedProtocolVersion;
            state.CorePipeName = coreService.PipeName;
            state.CoreContextProvider = coreService.ContextProvider;
            state.CoreServiceAvailable = coreService.Available;
            state.CoreServiceInstalled = coreService.Installed;
            state.CoreServiceRunning = coreService.Running;
            state.CoreServiceStatus = coreService.Status;
            state.CoreServiceAction = coreService.Action;
            state.CoreServiceHealth = coreService.Health;
            state.CoreServiceSummary = coreService.Summary;
            state.CoreServiceDetail = coreService.Detail;
            state.CoreServiceAutoTaskInstalled = coreService.AutoTaskInstalled;
            state.CoreServiceAutoTaskKicked = coreService.AutoTaskKicked;
            state.CoreServiceTelemetryFresh = coreService.TelemetryFresh;
            state.CoreServiceTelemetryStale = coreService.TelemetryStale;
            state.CoreServiceNeedsAttention = coreService.NeedsAttention;
            state.CoreServiceScoreAgeSeconds = coreService.ScoreAgeSeconds;
            state.CoreServiceStaleThresholdSeconds = coreService.StaleThresholdSeconds;
            state.CoreServiceLoopSeconds = coreService.LoopSeconds;
            state.CoreServiceStateAgeSeconds = coreService.StateAgeSeconds;
            state.CoreServiceUpdatedAt = coreService.UpdatedAt;
            state.CoreIpcListening = coreService.IpcListening;
            state.CoreIpcSecureAcl = coreService.IpcSecureAcl;
            state.CoreIpcHeartbeatAt = coreService.IpcHeartbeatAt;
            state.CoreIpcLastClientAt = coreService.IpcLastClientAt;
            state.CoreIpcLastCommand = coreService.IpcLastCommand;
            state.CoreIpcLastError = coreService.IpcLastError;
            state.MemoryStabilityAvailable = coreService.MemoryStabilityAvailable;
            state.MemoryStabilityRelevant = coreService.MemoryStabilityRelevant;
            state.MemoryStabilityMode = coreService.MemoryStabilityMode;
            state.MemoryStabilityState = coreService.MemoryStabilityState;
            state.MemoryStabilitySummary = coreService.MemoryStabilitySummary;
            state.MemoryStabilityDetail = coreService.MemoryStabilityDetail;
            state.MemoryStabilityMemoryLoad = coreService.MemoryStabilityMemoryLoad;
            state.MemoryStabilityAvailablePhysicalMB = coreService.MemoryStabilityAvailablePhysicalMB;
            state.MemoryStabilityTotalPhysicalMB = coreService.MemoryStabilityTotalPhysicalMB;
            state.MemoryStabilityCommitUsedMB = coreService.MemoryStabilityCommitUsedMB;
            state.MemoryStabilityCommitLimitMB = coreService.MemoryStabilityCommitLimitMB;
            state.MemoryStabilityCommitHeadroomMB = coreService.MemoryStabilityCommitHeadroomMB;
            state.MemoryStabilityCommitHeadroomPercent = coreService.MemoryStabilityCommitHeadroomPercent;
            state.MemoryStabilityPagefileStatus = coreService.MemoryStabilityPagefileStatus;
            state.MemoryStabilityPagefileLimited = coreService.MemoryStabilityPagefileLimited;
            state.MemoryStabilityLowMemorySignal = coreService.MemoryStabilityLowMemorySignal;
            state.MemoryStabilityBrowserBurstRecommended = coreService.MemoryStabilityBrowserBurstRecommended;
            state.MemoryStabilityTopProcess = coreService.MemoryStabilityTopProcess;
            state.MemoryStabilityTopProcessPid = coreService.MemoryStabilityTopProcessPid;
            state.MemoryStabilityTopProcessPrivateMB = coreService.MemoryStabilityTopProcessPrivateMB;
            state.MemoryStabilityTopProcessWorkingSetMB = coreService.MemoryStabilityTopProcessWorkingSetMB;
            state.MemoryStabilityBrowserProcessCount = coreService.MemoryStabilityBrowserProcessCount;
            state.MemoryStabilityBrowserPrivateMB = coreService.MemoryStabilityBrowserPrivateMB;
            state.MemoryStabilityBrowserWorkingSetMB = coreService.MemoryStabilityBrowserWorkingSetMB;
            state.MemoryStabilityBrowserBurstState = coreService.MemoryStabilityBrowserBurstState;
            state.MemoryStabilityHeavyRecentProcessCount = coreService.MemoryStabilityHeavyRecentProcessCount;
            state.MemoryStabilitySignals = coreService.MemoryStabilitySignals ?? new List<string>();
            state.SystemIntegrityAvailable = coreService.SystemIntegrityAvailable;
            state.SystemIntegrityRelevant = coreService.SystemIntegrityRelevant;
            state.SystemIntegrityMode = coreService.SystemIntegrityMode;
            state.SystemIntegrityState = coreService.SystemIntegrityState;
            state.SystemIntegritySummary = coreService.SystemIntegritySummary;
            state.SystemIntegrityDetail = coreService.SystemIntegrityDetail;
            state.SystemIntegrityBackupAvailable = coreService.SystemIntegrityBackupAvailable;
            state.SystemIntegrityMmcssServiceRunning = coreService.SystemIntegrityMmcssServiceRunning;
            state.SystemIntegrityMmcssServiceStatus = coreService.SystemIntegrityMmcssServiceStatus;
            state.SystemIntegritySystemResponsiveness = coreService.SystemIntegritySystemResponsiveness;
            state.SystemIntegritySystemResponsivenessState = coreService.SystemIntegritySystemResponsivenessState;
            state.SystemIntegritySystemResponsivenessDetail = coreService.SystemIntegritySystemResponsivenessDetail;
            state.SystemIntegrityHybridCpuDetected = coreService.SystemIntegrityHybridCpuDetected;
            state.SystemIntegrityLogicalProcessorCount = coreService.SystemIntegrityLogicalProcessorCount;
            state.SystemIntegrityHybridSchedulerState = coreService.SystemIntegrityHybridSchedulerState;
            state.SystemIntegrityHybridSchedulerDetail = coreService.SystemIntegrityHybridSchedulerDetail;
            state.SystemIntegritySelfThrottleEligible = coreService.SystemIntegritySelfThrottleEligible;
            state.SystemIntegritySelfThrottleState = coreService.SystemIntegritySelfThrottleState;
            state.SystemIntegritySelfThrottleDetail = coreService.SystemIntegritySelfThrottleDetail;
            state.SystemIntegrityIssueCount = coreService.SystemIntegrityIssueCount;
            state.SystemIntegrityRecommendationCount = coreService.SystemIntegrityRecommendationCount;
            state.SystemIntegritySafeRecommendationCount = coreService.SystemIntegritySafeRecommendationCount;
            state.SystemIntegrityOptionalRecommendationCount = coreService.SystemIntegrityOptionalRecommendationCount;
            state.SystemIntegrityExperimentalRecommendationCount = coreService.SystemIntegrityExperimentalRecommendationCount;
            state.SystemIntegrityRestartRecommendationCount = coreService.SystemIntegrityRestartRecommendationCount;
            state.SystemIntegrityApplyBlockedRecommendationCount = coreService.SystemIntegrityApplyBlockedRecommendationCount;
            state.SystemIntegrityRecommendations = coreService.SystemIntegrityRecommendations ?? new List<Dictionary<string, object>>();
            state.SystemIntegritySignals = coreService.SystemIntegritySignals ?? new List<string>();
            state.SystemIntegrityIssues = coreService.SystemIntegrityIssues ?? new List<string>();
            state.SessionAgentAvailable = sessionAgent.Available;
            state.SessionAgentHealth = sessionAgent.Health;
            state.SessionAgentState = sessionAgent.State;
            state.SessionAgentUpdatedAt = sessionAgent.UpdatedAt;
            state.SessionAgentStateAgeSeconds = sessionAgent.StateAgeSeconds;
            state.SessionAgentContext = sessionAgent.Context;
            state.SessionAgentConfidence = sessionAgent.Confidence;
            state.SessionAgentForegroundPid = sessionAgent.ForegroundPid;
            state.SessionAgentForegroundProcessName = sessionAgent.ForegroundProcessName;
            state.SessionAgentForegroundFullscreen = sessionAgent.ForegroundFullscreen;
            state.SessionAgentStreamingObserved = sessionAgent.StreamingObserved;
            state.RollbackAuditEnabled = scoreMeta.RollbackAuditEnabled;
            int manualPolicyCount = CountManualPolicies();
            state.PolicyCount = manualPolicyCount;
            state.ManualPolicyCount = manualPolicyCount;
            state.AppCount = scoreMeta.AppCount > 0 ? scoreMeta.AppCount : rows.Count;
            state.ProcessCount = scoreMeta.ProcessCount > 0 ? scoreMeta.ProcessCount : rows.Count;
            PreviewSummary preview = LoadPreviewSummary();
            state.PreviewTargets = preview.Targets;
            state.PreviewWouldTrim = preview.WouldTrim;
            state.PreviewTop = preview.TopApp;
            state.PreviewAt = preview.TimestampText;
            state.PreviewResult = preview.ShortText;
            state.PreviewDetail = preview.Detail;
            state.Learning = learningEnabled;
            state.LearningProfiles = learningEnabled ? Math.Max(scoreMeta.LearningProfiles, GetLearningProfileCount()) : 0;
            state.Behavior = behaviorEnabled || scoreMeta.BehaviorEnabled;
            state.BehaviorProfiles = state.Behavior ? Math.Max(scoreMeta.BehaviorProfiles, GetBehaviorProfileCount()) : 0;
            double freeMemoryFallback = scoreMeta.FreeMemoryMB > 0 ? scoreMeta.FreeMemoryMB : (hardware.AvailableMemoryMB > 0 ? hardware.AvailableMemoryMB : 0);
            string logPressure = ExtractLogValue(line, "pressure");
            state.MemoryPressure = !String.IsNullOrWhiteSpace(scoreMeta.MemoryPressure) ? scoreMeta.MemoryPressure : (!String.IsNullOrWhiteSpace(logPressure) ? logPressure : ClassifyMemoryPressure(freeMemoryFallback));
            state.FreeMemoryMB = freeMemoryFallback;
            string logIntent = ExtractLogValue(line, "intent");
            string intentCandidate = !String.IsNullOrWhiteSpace(scoreMeta.IntentName) ? scoreMeta.IntentName : (!String.IsNullOrWhiteSpace(top) ? top : String.Empty);
            if (IsBlockedNetworkUdpGameName(intentCandidate)) { intentCandidate = String.Empty; }
            state.IntentKind = !String.IsNullOrWhiteSpace(intentCandidate) && IsDashboardGameProcessName(intentCandidate) ? "Game" : (!String.IsNullOrWhiteSpace(logIntent) ? logIntent : "Desktop");
            state.IntentName = intentCandidate;
            state.IntentConfidence = scoreMeta.IntentConfidence > 0 && !String.IsNullOrWhiteSpace(state.IntentName) ? scoreMeta.IntentConfidence : (!String.IsNullOrWhiteSpace(state.IntentName) ? 50 : 0);
            state.IntentSignals = scoreMeta.IntentSignals;
            string radarTopCandidate = !String.IsNullOrWhiteSpace(scoreMeta.RadarTop) ? scoreMeta.RadarTop : (rows.Count > 0 ? rows[0].Name : String.Empty);
            state.RadarTop = IsBlockedNetworkUdpGameName(radarTopCandidate) ? String.Empty : radarTopCandidate;
            state.RadarCount = scoreMeta.RadarCount > 0 ? scoreMeta.RadarCount : rows.Count;
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
            string topAppCandidate = String.IsNullOrWhiteSpace(top) ? (rows.Count > 0 ? rows[0].Name : "-") : top;
            state.TopApp = IsBlockedNetworkUdpGameName(topAppCandidate) ? "-" : topAppCandidate;
            state.Wake = autoInstalled ? "Fast wake" : "Manual";
            state.Heartbeat = heartbeat;
            state.LastEventAge = lastEventAge;
            state.NextPass = nextPass;
            state.HardwareCpu = hardware.Cpu;
            state.HardwareCpuDetail = hardware.CpuDetail;
            state.HardwareRam = hardware.Ram;
            state.HardwareRamDetail = hardware.RamDetail;
            state.HardwareGpu = hardware.Gpu;
            state.HardwareGpuDetail = hardware.GpuDetail;
            state.HardwareOs = hardware.Os;
            state.AvailableMemoryText = hardware.AvailableMemoryText;
            state.HardwareSystemDetail = hardware.SystemDetail;
            state.HardwareRamTotalMB = hardware.TotalMemoryMB;
            state.HardwareRamFreeMB = hardware.AvailableMemoryMB;
            state.HardwarePageFileTotalMB = hardware.PageFileTotalMB;
            state.HardwarePageFileFreeMB = hardware.PageFileAvailableMB;
            state.HardwareVirtualTotalMB = hardware.VirtualTotalMB;
            state.HardwareVirtualFreeMB = hardware.VirtualAvailableMB;
            state.HardwareMemoryLoad = hardware.MemoryLoad;
            state.HardwareCpuClockMhz = hardware.CpuClockCurrentMhz;
            state.HardwareCpuMaxMhz = hardware.CpuClockMaxMhz;
            state.AppTimelines = BuildAppTimelines(rows);
            state.Rows = rows;
            state.Events = BuildEvents(autoInstalled, heartbeat, lastEventAge, nextPass, coreService);
            try
            {
                state.GamePresets = BuildGamePresetsForUi();
            }
            catch (Exception ex)
            {
                WriteCrash(ex);
                state.GamePresets = new List<WebGamePreset>();
            }
            state.GameBetaWelcome = ShouldShowGameBetaWelcome();
            ReleaseUpdateInfo currentUpdate = updateInfo ?? ReleaseUpdateInfo.Idle();
            state.UpdateAutoChecks = LoadAutoUpdateChecks();
            state.UpdateChecking = updateCheckRunning || currentUpdate.Checking;
            state.UpdateAvailable = currentUpdate.Available;
            state.UpdateIgnored = currentUpdate.Ignored;
            state.UpdateLatestTag = currentUpdate.LatestTag;
            state.UpdateLatestVersion = currentUpdate.LatestVersion;
            state.UpdateReleaseName = currentUpdate.ReleaseName;
            state.UpdateReleaseUrl = currentUpdate.ReleaseUrl;
            state.UpdateDownloadUrl = currentUpdate.DownloadUrl;
            state.UpdatePublishedAt = currentUpdate.PublishedAt;
            state.UpdateError = currentUpdate.Error;
            state.PostUpdateNotice = ShouldShowPostUpdateNotice();
            state.PostUpdateBody = GetPostUpdateNoticeBody();
            state.PostUpdateItems = GetPostUpdateNoticeItems();
            state.Logo = GetLogoDataUri();
            return state;
        }

        private void ReconcileNetworkUdpGuardMeta(ScoreMeta meta, List<WebManagerRow> rows, bool enabled)
        {
            if (!enabled || meta == null) { return; }

            WebManagerRow gameRow = FindNetworkUdpGuardGameRow(rows);
            string gameName = gameRow == null ? "" : CleanGameDisplayName(String.IsNullOrWhiteSpace(gameRow.ProcessName) ? gameRow.Name : gameRow.ProcessName);
            if (String.IsNullOrWhiteSpace(gameName) && IsDashboardGameProcessName(meta.CpuBoundAssistGame)) { gameName = CleanGameDisplayName(meta.CpuBoundAssistGame); }
            if (String.IsNullOrWhiteSpace(gameName) && IsDashboardGameProcessName(meta.RadarTop)) { gameName = CleanGameDisplayName(meta.RadarTop); }
            if (String.IsNullOrWhiteSpace(gameName)) { return; }

            string currentGame = CleanGameDisplayName(meta.NetworkUdpGuardGame);
            bool currentLooksWrong = String.IsNullOrWhiteSpace(currentGame) || IsBlockedNetworkUdpGameName(currentGame) || !IsDashboardGameProcessName(currentGame);
            bool currentConfirmed = meta.NetworkUdpGuardEndpoints > 0;
            if (meta.NetworkUdpGuardActive && !currentLooksWrong && currentConfirmed) { return; }

            int endpoints = Math.Max(meta.NetworkUdpGuardEndpoints, gameRow == null ? 0 : gameRow.UdpEndpoints);
            bool rowConfirmed = endpoints > 0;
            int processCount = Math.Max(meta.NetworkUdpGuardProcessCount, endpoints > 0 ? 1 : 0);
            int confidence = Math.Max(meta.NetworkUdpGuardConfidence, endpoints > 0 ? 74 : 45);
            string confidenceLabel = endpoints > 0 ? "Medium" : "Low";
            if (gameRow != null && (gameRow.UdpGameProtected || gameRow.UdpGuardActive || gameRow.UdpConfidence >= 85))
            {
                confidence = Math.Max(confidence, Math.Max(86, gameRow.UdpConfidence));
                confidenceLabel = "High";
            }
            else if (gameRow != null && gameRow.UdpConfidence > confidence)
            {
                confidence = gameRow.UdpConfidence;
                confidenceLabel = String.IsNullOrWhiteSpace(gameRow.UdpConfidenceLabel) ? confidenceLabel : gameRow.UdpConfidenceLabel;
            }

            if (!rowConfirmed)
            {
                meta.NetworkUdpGuardActive = false;
                meta.NetworkUdpGuardMode = "Armed";
                meta.NetworkUdpGuardGame = gameName;
                meta.NetworkUdpGuardEndpoints = 0;
                meta.NetworkUdpGuardProcessCount = processCount;
                meta.NetworkUdpGuardConfidence = confidence;
                meta.NetworkUdpGuardConfidenceLabel = "Low";
                meta.NetworkUdpGuardReason = "Jogo detectado; aguardando UDP confirmado antes de aplicar o Zero Ping.";
                meta.NetworkUdpGuardShieldMode = "Observe";
                if (String.IsNullOrWhiteSpace(meta.NetworkUdpGuardQosStatus) || String.Equals(meta.NetworkUdpGuardQosStatus, "Off", StringComparison.OrdinalIgnoreCase)) { meta.NetworkUdpGuardQosStatus = "Ready"; }
                if (meta.NetworkUdpGuardSignals == null) { meta.NetworkUdpGuardSignals = new List<string>(); }
                string candidateSignal = "Game candidate: " + gameName;
                if (!meta.NetworkUdpGuardSignals.Contains(candidateSignal)) { meta.NetworkUdpGuardSignals.Add(candidateSignal); }
                return;
            }

            meta.NetworkUdpGuardActive = true;
            meta.NetworkUdpGuardMode = "Protecting";
            meta.NetworkUdpGuardGame = gameName;
            meta.NetworkUdpGuardEndpoints = endpoints;
            meta.NetworkUdpGuardProcessCount = processCount;
            meta.NetworkUdpGuardProtectedCount = Math.Max(1, meta.NetworkUdpGuardProtectedCount);
            meta.NetworkUdpGuardConfidence = confidence;
            meta.NetworkUdpGuardConfidenceLabel = confidenceLabel;
            meta.NetworkUdpGuardReason = "UDP confirmado no jogo ou na arvore relacionada. Zero Ping ativo.";
            if (String.IsNullOrWhiteSpace(meta.NetworkUdpGuardShieldMode) || String.Equals(meta.NetworkUdpGuardShieldMode, "Off", StringComparison.OrdinalIgnoreCase)) { meta.NetworkUdpGuardShieldMode = "Netcode Shield"; }
            if (String.IsNullOrWhiteSpace(meta.NetworkUdpGuardQosStatus) || String.Equals(meta.NetworkUdpGuardQosStatus, "Off", StringComparison.OrdinalIgnoreCase)) { meta.NetworkUdpGuardQosStatus = "Ready"; }
            if (meta.NetworkUdpGuardSignals == null) { meta.NetworkUdpGuardSignals = new List<string>(); }
            meta.NetworkUdpGuardSignals.Add("Game lock: " + gameName);
        }

        private static WebManagerRow FindNetworkUdpGuardGameRow(List<WebManagerRow> rows)
        {
            if (rows == null || rows.Count == 0) { return null; }
            WebManagerRow best = null;
            double bestScore = -1.0;
            foreach (WebManagerRow row in rows)
            {
                if (row == null || !IsDashboardGameRow(row)) { continue; }
                double score = row.RawScore + (row.UdpEndpoints * 100.0) + (row.UdpGameProtected ? 500.0 : 0.0) + (row.UdpGuardActive ? 250.0 : 0.0) + row.UdpConfidence;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = row;
                }
            }
            return best;
        }

        private static bool IsDashboardGameRow(WebManagerRow row)
        {
            string name = CleanGameDisplayName(row == null ? "" : (String.IsNullOrWhiteSpace(row.ProcessName) ? row.Name : row.ProcessName));
            string role = row == null ? "" : row.Role;
            if (IsSmartNapProcessOrPath(name, row == null ? "" : row.Path)) { return false; }
            if (IsBlockedNetworkUdpGameName(name)) { return false; }
            if (NameInList(role, new string[] { "Browser", "Communication", "Media", "Streaming", "StreamHelper", "Launcher", "LauncherHelper", "Professional", "Development" })) { return false; }
            if (IsDashboardGameProcessName(name)) { return true; }
            string path = row == null ? "" : row.Path;
            return PathHasDashboardGameFragment(path);
        }

        private static bool IsDashboardGameProcessName(string processName)
        {
            string name = CleanGameDisplayName(processName);
            if (String.IsNullOrWhiteSpace(name) || IsBlockedNetworkUdpGameName(name)) { return false; }
            if (NameInList(name, new string[] { "bf6", "bf2042", "bfv", "bf1", "bf4", "bf3", "fc26", "fc25", "fc24", "fifa23", "fifa22", "cs2", "valorant", "valorant-win64-shipping", "r5apex", "apex", "fortniteclient-win64-shipping", "rocketleague", "rainbowsix", "rainbowsix_be", "cod", "cod22", "cod23", "cod24", "modernwarfare", "warzone", "league of legends", "dota2", "overwatch", "destiny2", "thefinals", "pubg", "tslgame", "escape from tarkov", "eldenring", "helldivers2", "gta5", "rdr2" })) { return true; }
            string lower = name.ToLowerInvariant();
            return (lower.StartsWith("bf", StringComparison.Ordinal) && lower.Length <= 7 && ContainsDashboardDigit(lower)) || lower.EndsWith("-win64-shipping", StringComparison.Ordinal);
        }

        private static bool IsBlockedNetworkUdpGameName(string processName)
        {
            string name = CleanGameDisplayName(processName);
            if (String.IsNullOrWhiteSpace(name)) { return true; }
            if (IsSmartNapProcessOrPath(name, "")) { return true; }
            if (NameInList(name, new string[] { "chrome", "msedge", "firefox", "zen", "brave", "opera", "vivaldi", "librewolf", "waterfox", "floorp", "arc", "tor", "msedgewebview2", "Lightshot", "ShareX", "Greenshot", "SnippingTool", "ScreenClippingHost", "GameBar", "GameBarFTServer", "XboxGameBar", "NVIDIA Share" })) { return true; }
            return IsKnownLauncherProcessForReactive(name) || NameInList(name, new string[] { "explorer", "smartbackgroundnap", "smartbackgroundnaptray", "smartbackgroundnapdashboard", "smart nap", "smartnap", "codex", "powershell", "pwsh", "cmd", "conhost", "notepad", "taskmgr" });
        }

        private static bool IsSmartNapProcessOrPath(string processName, string path)
        {
            string name = CleanGameDisplayName(processName);
            if (NameInList(name, new string[] { "SmartBackgroundNap", "SmartBackgroundNapTray", "SmartBackgroundNapDashboard", "Smart Nap", "smartnap" })) { return true; }
            if (String.IsNullOrWhiteSpace(path)) { return false; }
            return path.IndexOf("SmartBackgroundNap", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("Smart Background Nap", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("\\SmartNap\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CleanGameDisplayName(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) { return ""; }
            string text = value.Trim();
            int paren = text.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0) { text = text.Substring(0, paren).Trim(); }
            int xIndex = text.LastIndexOf(" x", StringComparison.OrdinalIgnoreCase);
            if (xIndex > 0 && xIndex + 2 < text.Length && Char.IsDigit(text[xIndex + 2])) { text = text.Substring(0, xIndex).Trim(); }
            if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { text = text.Substring(0, text.Length - 4); }
            return text.Trim();
        }

        private static bool PathHasDashboardGameFragment(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || IsSmartNapProcessOrPath("", path)) { return false; }
            string[] fragments = new string[] { "\\steamapps\\common\\", "\\XboxGames\\", "\\Epic Games\\", "\\Riot Games\\", "\\Battle.net\\", "\\GOG Galaxy\\Games\\", "\\EA Games\\", "\\Electronic Arts\\Games\\", "\\Electronic Arts\\Battlefield", "\\Electronic Arts\\FC", "\\Electronic Arts\\EA SPORTS FC", "\\Battlefield 6\\", "\\EA SPORTS FC 26\\" };
            for (int i = 0; i < fragments.Length; i++)
            {
                if (path.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            }
            return false;
        }
        private static bool ContainsDashboardDigit(string value)
        {
            if (String.IsNullOrEmpty(value)) { return false; }
            for (int i = 0; i < value.Length; i++)
            {
                if (Char.IsDigit(value[i])) { return true; }
            }
            return false;
        }

        private static string ClassifyMemoryPressure(double freeMemoryMB)
        {
            if (freeMemoryMB <= 0) { return "Unknown"; }
            if (freeMemoryMB <= 3072) { return "Critical"; }
            if (freeMemoryMB <= 6144) { return "Elevated"; }
            if (freeMemoryMB <= 8192) { return "Moderate"; }
            return "Normal";
        }

        private ScoreMeta LoadScoreMeta()
        {
            ScoreMeta meta = new ScoreMeta();
            meta.PermissionDeniedApps = new List<string>();
            try
            {
                if (!File.Exists(scorePath)) { LoadRadarMeta(meta); return meta; }
                string json = File.ReadAllText(scorePath, Encoding.UTF8);
                if (String.IsNullOrWhiteSpace(json)) { LoadRadarMeta(meta); return meta; }
                IDictionary<string, object> root = JsonCompat.DeserializeObject(json);
                if (root == null) { LoadRadarMeta(meta); return meta; }
                meta.AppCount = GetInt(root, "AppCount");
                meta.ProcessCount = GetInt(root, "ProcessCount");
                meta.LearningEnabled = GetBool(root, "LearningEnabled");
                meta.LearningProfiles = GetInt(root, "LearningProfiles");
                meta.BehaviorEnabled = GetBool(root, "BehaviorEnabled");
                meta.BehaviorProfiles = GetInt(root, "BehaviorProfiles");
                meta.MemoryPressure = GetString(root, "MemoryPressure");
                meta.FreeMemoryMB = GetDouble(root, "FreeMemoryMB");
                meta.IntentKind = GetString(root, "IntentKind");
                meta.IntentName = GetString(root, "IntentName");
                meta.IntentConfidence = GetInt(root, "IntentConfidence");
                meta.IntentSignals = ReadStringList(root, "IntentSignals");
                meta.NetworkUdpGuardEnabled = GetBool(root, "NetworkUdpGuardEnabled");
                meta.NetworkUdpGuardActive = GetBool(root, "NetworkUdpGuardActive");
                meta.NetworkUdpGuardMode = GetString(root, "NetworkUdpGuardMode");
                meta.NetworkUdpGuardGame = GetString(root, "NetworkUdpGuardGame");
                meta.NetworkUdpGuardEndpoints = GetInt(root, "NetworkUdpGuardEndpoints");
                meta.NetworkUdpGuardProcessCount = GetInt(root, "NetworkUdpGuardProcessCount");
                meta.NetworkUdpGuardNoStackTweaks = GetBool(root, "NetworkUdpGuardNoStackTweaks");
                meta.NetworkUdpGuardConfidence = GetInt(root, "NetworkUdpGuardConfidence");
                meta.NetworkUdpGuardConfidenceLabel = GetString(root, "NetworkUdpGuardConfidenceLabel");
                meta.NetworkUdpGuardReason = GetString(root, "NetworkUdpGuardReason");
                meta.NetworkUdpGuardShieldMode = GetString(root, "NetworkUdpGuardShieldMode");
                meta.NetworkUdpGuardProtectedCount = GetInt(root, "NetworkUdpGuardProtectedCount");
                meta.NetworkUdpGuardQosStatus = GetString(root, "NetworkUdpGuardQosStatus");
                meta.NetworkUdpGuardSignals = ReadStringList(root, "NetworkUdpGuardSignals");
                meta.GpuPressureAvailable = GetBool(root, "GpuPressureAvailable");
                meta.GpuPressureProvider = GetString(root, "GpuPressureProvider");
                meta.GpuPressureLevel = GetString(root, "GpuPressureLevel");
                meta.GpuPressureDxgiAvailable = GetBool(root, "GpuPressureDxgiAvailable");
                meta.GpuPressureAdapterName = GetString(root, "GpuPressureAdapterName");
                meta.GpuAdapterDedicatedVideoMemoryMB = GetDouble(root, "GpuAdapterDedicatedVideoMemoryMB");
                meta.GpuAdapterSharedSystemMemoryMB = GetDouble(root, "GpuAdapterSharedSystemMemoryMB");
                meta.GpuAdapterLocalBudgetMB = GetDouble(root, "GpuAdapterLocalBudgetMB");
                meta.GpuAdapterLocalUsageMB = GetDouble(root, "GpuAdapterLocalUsageMB");
                meta.GpuAdapterLocalAvailableMB = GetDouble(root, "GpuAdapterLocalAvailableMB");
                meta.GpuAdapterLocalUsagePercent = GetDouble(root, "GpuAdapterLocalUsagePercent");
                meta.GpuAdapterNonLocalBudgetMB = GetDouble(root, "GpuAdapterNonLocalBudgetMB");
                meta.GpuAdapterNonLocalUsageMB = GetDouble(root, "GpuAdapterNonLocalUsageMB");
                meta.GpuAdapterNonLocalAvailableMB = GetDouble(root, "GpuAdapterNonLocalAvailableMB");
                meta.GpuAdapterDedicatedUsageMB = GetDouble(root, "GpuAdapterDedicatedUsageMB");
                meta.GpuAdapterSharedUsageMB = GetDouble(root, "GpuAdapterSharedUsageMB");
                meta.GpuTotalUtilPercent = GetDouble(root, "GpuTotalUtilPercent");
                meta.GpuTopProcess = GetString(root, "GpuTopProcess");
                meta.GpuTopProcessPid = GetInt(root, "GpuTopProcessPid");
                meta.GpuTopProcessPercent = GetDouble(root, "GpuTopProcessPercent");
                meta.GpuTopProcessDedicatedMB = GetDouble(root, "GpuTopProcessDedicatedMB");
                meta.ShaderBoostEnabled = GetBool(root, "ShaderBoostEnabled");
                meta.ShaderBoostObserveOnly = GetBool(root, "ShaderBoostObserveOnly");
                meta.ShaderBoostState = GetString(root, "ShaderBoostState");
                meta.ShaderBoostSharedState = GetString(root, "ShaderBoostSharedState");
                meta.ShaderBoostReadiness = GetInt(root, "ShaderBoostReadiness");
                meta.ShaderBoostRecommendation = GetString(root, "ShaderBoostRecommendation");
                meta.ShaderBoostGame = GetString(root, "ShaderBoostGame");
                meta.ShaderBoostGamePid = GetInt(root, "ShaderBoostGamePid");
                meta.ShaderBoostGameRoot = GetString(root, "ShaderBoostGameRoot");
                meta.ShaderBoostApi = GetString(root, "ShaderBoostApi");
                meta.ShaderBoostApiConfidence = GetInt(root, "ShaderBoostApiConfidence");
                meta.ShaderBoostGpu = GetString(root, "ShaderBoostGpu");
                meta.ShaderBoostVendor = GetString(root, "ShaderBoostVendor");
                meta.ShaderBoostDriverVersion = GetString(root, "ShaderBoostDriverVersion");
                meta.ShaderBoostCacheState = GetString(root, "ShaderBoostCacheState");
                meta.ShaderBoostCacheLocatedCount = GetInt(root, "ShaderBoostCacheLocatedCount");
                meta.ShaderBoostCacheTotalSizeMB = GetDouble(root, "ShaderBoostCacheTotalSizeMB");
                meta.ShaderBoostCacheManager = GetString(root, "ShaderBoostCacheManager");
                meta.ShaderBoostCompilationState = GetString(root, "ShaderBoostCompilationState");
                meta.ShaderBoostCompilationPossible = GetBool(root, "ShaderBoostCompilationPossible");
                meta.ShaderBoostPreparationMethod = GetString(root, "ShaderBoostPreparationMethod");
                meta.ShaderBoostWarmupState = GetString(root, "ShaderBoostWarmupState");
                meta.ShaderBoostSignals = ReadStringList(root, "ShaderBoostSignals");
                meta.ShaderBoostDetails = ReadStringList(root, "ShaderBoostDetails");
                meta.CpuBoundAssistActive = GetBool(root, "CpuBoundAssistActive");
                meta.CpuBoundAssistGame = GetString(root, "CpuBoundAssistGame");
                meta.CpuBoundAssistGamePid = GetInt(root, "CpuBoundAssistGamePid");
                meta.CpuBoundAssistConfidence = GetInt(root, "CpuBoundAssistConfidence");
                meta.CpuBoundAssistReason = GetString(root, "CpuBoundAssistReason");
                meta.EngineHealthStatus = GetString(root, "EngineHealthStatus");
                meta.EngineHealthSummary = GetString(root, "EngineHealthSummary");
                meta.RollbackAuditEnabled = GetBool(root, "RollbackAuditEnabled");
                meta.StreamGuardActive = GetBool(root, "StreamGuardActive");
                meta.StreamHelperCount = GetInt(root, "StreamHelperCount");
                meta.StreamGameProtectedCount = GetInt(root, "StreamGameProtectedCount");
                object items = null;
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
            LoadRadarMeta(meta);
            return meta;
        }

        private void LoadRadarMeta(ScoreMeta meta)
        {
            try
            {
                if (meta == null || !File.Exists(radarPath)) { return; }
                IDictionary<string, object> root = JsonCompat.DeserializeObject(File.ReadAllText(radarPath, Encoding.UTF8));
                object items = null;
                System.Collections.IEnumerable enumerable = root != null && root.TryGetValue("Items", out items) ? items as System.Collections.IEnumerable : null;
                if (enumerable == null || items is string) { return; }
                int count = 0;
                string top = "";
                double topSeverity = -1.0;
                foreach (object item in enumerable)
                {
                    IDictionary<string, object> map = item as IDictionary<string, object>;
                    if (map == null) { continue; }
                    string itemName = BuildProcessLabel(map);
                    if (IsBlockedNetworkUdpGameName(itemName)) { continue; }
                    count++;
                    double severity = GetDouble(map, "Severity");
                    if (severity > topSeverity)
                    {
                        topSeverity = severity;
                        top = itemName;
                    }
                }
                meta.RadarCount = count;
                meta.RadarTop = top;
            }
            catch
            {
            }
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

        private PreviewSummary LoadPreviewSummary()
        {
            PreviewSummary summary = new PreviewSummary();
            summary.ShortText = "No preview yet";
            summary.Detail = "Run Preview to see the next pass without touching processes.";
            summary.TimestampText = "-";
            summary.TopApp = "-";
            try
            {
                if (String.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath)) { return summary; }
                IDictionary<string, object> root = JsonCompat.DeserializeObject(File.ReadAllText(previewPath, Encoding.UTF8));
                if (root == null) { return summary; }
                object items;
                System.Collections.IEnumerable enumerable = root.TryGetValue("Items", out items) ? items as System.Collections.IEnumerable : null;
                if (enumerable == null || items is string) { return summary; }
                int targets = 0;
                int wouldTrim = 0;
                int light = 0;
                int balanced = 0;
                int deep = 0;
                double topScore = -1.0;
                string topName = "-";
                foreach (object item in enumerable)
                {
                    IDictionary<string, object> map = item as IDictionary<string, object>;
                    if (map == null) { continue; }
                    targets++;
                    string trim = GetMapString(map, "TrimWorkingSet");
                    if (String.Equals(trim, "WouldTrim", StringComparison.OrdinalIgnoreCase)) { wouldTrim++; }
                    string tier = GetMapString(map, "NapTier");
                    if (String.Equals(tier, "Light", StringComparison.OrdinalIgnoreCase)) { light++; }
                    else if (String.Equals(tier, "Balanced", StringComparison.OrdinalIgnoreCase)) { balanced++; }
                    else if (String.Equals(tier, "Deep", StringComparison.OrdinalIgnoreCase)) { deep++; }
                    double score = GetDouble(map, "Score");
                    if (score > topScore)
                    {
                        topScore = score;
                        topName = BuildProcessLabel(map);
                    }
                }
                summary.Targets = targets;
                summary.WouldTrim = wouldTrim;
                summary.TopApp = topName;
                summary.ShortText = targets.ToString(CultureInfo.CurrentCulture) + " apps / " + wouldTrim.ToString(CultureInfo.CurrentCulture) + " trims";
                summary.Detail = "Preview: " + targets.ToString(CultureInfo.CurrentCulture) + " apps, " + wouldTrim.ToString(CultureInfo.CurrentCulture) + " would trim, L/B/D " + light.ToString(CultureInfo.CurrentCulture) + "/" + balanced.ToString(CultureInfo.CurrentCulture) + "/" + deep.ToString(CultureInfo.CurrentCulture) + ", top " + topName + ".";
                summary.TimestampText = File.GetLastWriteTime(previewPath).ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            }
            catch
            {
            }
            return summary;
        }

        private Dictionary<string, List<string>> BuildAppTimelines(List<WebManagerRow> rows)
        {
            Dictionary<string, List<string>> timelines = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            List<string> logLines = ReadLastLines(logPath, 80);
            foreach (WebManagerRow row in rows)
            {
                if (row == null) { continue; }
                string key = String.IsNullOrWhiteSpace(row.Key) ? row.ProcessName : row.Key;
                if (String.IsNullOrWhiteSpace(key)) { key = row.Name; }
                if (String.IsNullOrWhiteSpace(key) || timelines.ContainsKey(key)) { continue; }
                List<string> items = new List<string>();
                items.Add("App: " + row.Name);
                items.Add("Policy: " + (String.IsNullOrWhiteSpace(row.Policy) ? "Auto" : row.Policy) + " | Confidence: " + row.BehaviorConfidence.ToString(CultureInfo.CurrentCulture) + " | Tier: " + (String.IsNullOrWhiteSpace(row.BehaviorTier) ? "Auto" : row.BehaviorTier));
                items.Add("Instances: " + row.InstanceCount.ToString(CultureInfo.CurrentCulture) + " | CPU: " + row.Cpu + " | Delta: " + row.Delta);
                if (!String.IsNullOrWhiteSpace(row.BehaviorReason)) { items.Add("Reason: " + row.BehaviorReason); }
                string process = row.ProcessName ?? String.Empty;
                for (int i = logLines.Count - 1; i >= 0 && items.Count < 9; i--)
                {
                    string line = logLines[i];
                    if (String.IsNullOrWhiteSpace(process) || line.IndexOf(process, StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("action=apply", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("action=preview", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        items.Add(FormatActivityLine(line));
                    }
                }
                timelines[key] = items;
            }
            return timelines;
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

        private List<string> BuildEvents(bool autoInstalled, string heartbeat, string lastEventAge, string nextPass, CoreServiceSnapshot coreService)
        {
            List<string> events = new List<string>();
            if (!String.IsNullOrWhiteSpace(activeUiEventLine))
            {
                events.Add(activeUiEventLine);
            }
            events.Add("LIVE " + heartbeat + "  event " + lastEventAge + "  next " + nextPass);
            if (coreService != null && (coreService.NeedsAttention || coreService.AutoTaskKicked || String.Equals(coreService.Health, "Recovering", StringComparison.OrdinalIgnoreCase)))
            {
                events.Add(DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) +
                    " action=core-service health=" + SanitizeLogToken(coreService.Health) +
                    " status=" + SanitizeLogToken(coreService.Status) +
                    " operation=" + SanitizeLogToken(coreService.Action) +
                    " scoreAgeSeconds=" + coreService.ScoreAgeSeconds.ToString(CultureInfo.InvariantCulture));
            }
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
                Dictionary<string, string> manualPolicies = LoadAppPolicyMapForUi();
                Dictionary<string, WebManagerRow> grouped = new Dictionary<string, WebManagerRow>(StringComparer.OrdinalIgnoreCase);
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
                    row.Key = GetString(map, "AppKey");
                    row.ProcessName = GetMapString(map, "ProcessName");
                    row.Path = GetMapString(map, "Path");
                    row.Role = GetString(map, "Role");
                    row.Policy = String.IsNullOrWhiteSpace(GetString(map, "AppPolicy")) ? "Auto" : GetString(map, "AppPolicy");
                    row.PolicySource = GetString(map, "PolicySource");
                    row.Guard = GetString(map, "GuardReason");
                    row.Intent = GetString(map, "IntentKind");
                    row.IntentConfidence = GetInt(map, "IntentConfidence");
                    row.SwitchFastWake = GetBool(map, "SwitchFastWake");
                    row.UdpEndpoints = GetInt(map, "UdpEndpoints");
                    row.UdpGameProtected = GetBool(map, "UdpGameProtected");
                    row.UdpGuardActive = GetBool(map, "UdpGuardActive");
                    row.UdpConfidence = GetInt(map, "UdpConfidence");
                    row.UdpConfidenceLabel = GetString(map, "UdpConfidenceLabel");
                    row.UdpConfidenceReason = GetString(map, "UdpConfidenceReason");
                    row.UdpShieldMode = GetString(map, "UdpShieldMode");
                    row.UdpQosStatus = GetString(map, "UdpQosStatus");
                    row.GpuPercent = GetDouble(map, "GpuPercent");
                    row.GpuDedicatedMB = GetDouble(map, "GpuDedicatedMB");
                    row.GpuSharedMB = GetDouble(map, "GpuSharedMB");
                    row.GpuHelperPressure = GetBool(map, "GpuHelperPressure");
                    row.VramPressureActive = GetBool(map, "VramPressureActive");
                    row.CpuBoundAssist = GetBool(map, "CpuBoundAssist");
                    row.BehaviorWakeCount = GetInt(map, "BehaviorWakeCount");
                    row.BehaviorConfidence = GetInt(map, "BehaviorConfidence");
                    row.BehaviorBias = GetInt(map, "BehaviorBias");
                    row.BehaviorTier = GetString(map, "BehaviorPreferredTier");
                    row.BehaviorReason = GetString(map, "BehaviorReason");
                    ApplyManualPolicyOverlay(row, manualPolicies);
                    row.RawScore = GetDouble(map, "Score");
                    row.RepresentativeScore = row.RawScore;
                    row.RawDelta = GetDouble(map, "DeltaMB");
                    row.RawCpu = GetDouble(map, "CpuPercent");
                    row.RawBursts = GetInt(map, "BurstCount");
                    row.InstanceCount = Math.Max(1, GetInt(map, "InstanceCount"));
                    string groupKey = BuildManagerGroupKey(map, row);
                    WebManagerRow existing;
                    if (grouped.TryGetValue(groupKey, out existing))
                    {
                        MergeManagerRow(existing, row);
                    }
                    else
                    {
                        grouped[groupKey] = row;
                    }
                }
                rows.AddRange(grouped.Values);
                foreach (WebManagerRow row in rows)
                {
                    ApplyManualPolicyOverlay(row, manualPolicies);
                    row.Name = BuildManagerDisplayName(row);
                    row.Score = FormatDecimal(row.RawScore);
                    row.Delta = FormatDecimal(row.RawDelta) + " MB";
                    row.Cpu = FormatDecimal(row.RawCpu);
                    row.Bursts = row.RawBursts.ToString(CultureInfo.CurrentCulture);
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

        private static string BuildManagerGroupKey(IDictionary<string, object> map, WebManagerRow row)
        {
            string key = row == null ? "" : row.Key;
            if (String.IsNullOrWhiteSpace(key))
            {
                key = GetString(map, "AppKey");
            }
            if (!String.IsNullOrWhiteSpace(key))
            {
                return key.Trim().ToLowerInvariant();
            }
            string path = row == null ? "" : row.Path;
            if (String.IsNullOrWhiteSpace(path))
            {
                path = GetMapString(map, "Path");
            }
            if (!String.IsNullOrWhiteSpace(path))
            {
                return "path:" + path.Trim().ToLowerInvariant();
            }
            string name = row == null ? "" : row.ProcessName;
            if (String.IsNullOrWhiteSpace(name))
            {
                name = GetMapString(map, "ProcessName");
            }
            return "name:" + (String.IsNullOrWhiteSpace(name) ? "unknown" : name.Trim().ToLowerInvariant());
        }

        private static void MergeManagerRow(WebManagerRow target, WebManagerRow source)
        {
            if (target == null || source == null) { return; }
            target.InstanceCount += Math.Max(1, source.InstanceCount);
            target.RawScore += source.RawScore;
            target.RawDelta += source.RawDelta;
            target.RawCpu += source.RawCpu;
            target.RawBursts += source.RawBursts;
            target.PermissionDenied = target.PermissionDenied || source.PermissionDenied;
            target.SwitchFastWake = target.SwitchFastWake || source.SwitchFastWake;
            target.UdpEndpoints += source.UdpEndpoints;
            target.UdpGameProtected = target.UdpGameProtected || source.UdpGameProtected;
            target.UdpGuardActive = target.UdpGuardActive || source.UdpGuardActive;
            if (source.UdpConfidence > target.UdpConfidence)
            {
                target.UdpConfidence = source.UdpConfidence;
                target.UdpConfidenceLabel = source.UdpConfidenceLabel;
                target.UdpConfidenceReason = source.UdpConfidenceReason;
                target.UdpShieldMode = source.UdpShieldMode;
            }
            if (String.IsNullOrWhiteSpace(target.UdpQosStatus) || String.Equals(target.UdpQosStatus, "Off", StringComparison.OrdinalIgnoreCase)) { target.UdpQosStatus = source.UdpQosStatus; }
            target.GpuPercent += source.GpuPercent;
            target.GpuDedicatedMB += source.GpuDedicatedMB;
            target.GpuSharedMB += source.GpuSharedMB;
            target.GpuHelperPressure = target.GpuHelperPressure || source.GpuHelperPressure;
            target.VramPressureActive = target.VramPressureActive || source.VramPressureActive;
            target.CpuBoundAssist = target.CpuBoundAssist || source.CpuBoundAssist;
            if (source.IntentConfidence > target.IntentConfidence)
            {
                target.IntentConfidence = source.IntentConfidence;
                if (!String.IsNullOrWhiteSpace(source.Intent)) { target.Intent = source.Intent; }
            }
            if (source.BehaviorConfidence > target.BehaviorConfidence)
            {
                target.BehaviorConfidence = source.BehaviorConfidence;
                target.BehaviorBias = source.BehaviorBias;
                if (source.BehaviorWakeCount > target.BehaviorWakeCount) { target.BehaviorWakeCount = source.BehaviorWakeCount; }
                if (!String.IsNullOrWhiteSpace(source.BehaviorTier)) { target.BehaviorTier = source.BehaviorTier; }
                if (!String.IsNullOrWhiteSpace(source.BehaviorReason)) { target.BehaviorReason = source.BehaviorReason; }
            }
            if (String.IsNullOrWhiteSpace(target.Guard) && !String.IsNullOrWhiteSpace(source.Guard)) { target.Guard = source.Guard; }
            if ((String.IsNullOrWhiteSpace(target.Role) || String.Equals(target.Role, "App", StringComparison.OrdinalIgnoreCase)) && !String.IsNullOrWhiteSpace(source.Role)) { target.Role = source.Role; }
            if ((String.IsNullOrWhiteSpace(target.Policy) || String.Equals(target.Policy, "Auto", StringComparison.OrdinalIgnoreCase)) && !String.IsNullOrWhiteSpace(source.Policy)) { target.Policy = source.Policy; }
            if (source.RepresentativeScore > target.RepresentativeScore)
            {
                target.RepresentativeScore = source.RepresentativeScore;
                target.Action = source.Action;
                target.Key = String.IsNullOrWhiteSpace(source.Key) ? target.Key : source.Key;
                target.ProcessName = String.IsNullOrWhiteSpace(source.ProcessName) ? target.ProcessName : source.ProcessName;
                target.Path = String.IsNullOrWhiteSpace(source.Path) ? target.Path : source.Path;
            }
        }

        private static string BuildManagerDisplayName(WebManagerRow row)
        {
            string name = row == null ? "" : row.ProcessName;
            if (String.IsNullOrWhiteSpace(name) && row != null) { name = row.Name; }
            if (String.IsNullOrWhiteSpace(name)) { name = "Unknown"; }
            int instances = row == null ? 0 : row.InstanceCount;
            return instances > 1 ? name + " x" + instances.ToString(CultureInfo.CurrentCulture) : name;
        }

        private string BuildProcessLabel(IDictionary<string, object> map)
        {
            string name = GetMapString(map, "ProcessName");
            if (String.IsNullOrWhiteSpace(name)) { name = "Unknown"; }
            int instances = GetInt(map, "InstanceCount");
            if (instances > 1) { return name + " x" + instances.ToString(CultureInfo.CurrentCulture); }
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
            string affinity = BlankToDash(GetString(map, "CpuAffinity"));
            string learning = GetString(map, "Learning");
            int observations = GetInt(map, "LearningObservations");
            int wakes = GetInt(map, "LearningWakeCount");
            string policy = GetString(map, "AppPolicy");
            string source = GetString(map, "PolicySource");
            string role = GetString(map, "Role");
            string guard = GetString(map, "GuardReason");
            string intent = GetString(map, "IntentKind");
            int intentConfidence = GetInt(map, "IntentConfidence");
            int udpEndpoints = GetInt(map, "UdpEndpoints");
            bool udpProtected = GetBool(map, "UdpGameProtected");
            int udpConfidence = GetInt(map, "UdpConfidence");
            string udpConfidenceLabel = GetString(map, "UdpConfidenceLabel");
            string udpQosStatus = GetString(map, "UdpQosStatus");
            bool gpuHelperPressure = GetBool(map, "GpuHelperPressure");
            bool vramPressureActive = GetBool(map, "VramPressureActive");
            bool cpuBoundAssist = GetBool(map, "CpuBoundAssist");
            int behaviorConfidence = GetInt(map, "BehaviorConfidence");
            int behaviorBias = GetInt(map, "BehaviorBias");
            int behaviorWakes = GetInt(map, "BehaviorWakeCount");
            string behaviorTier = GetString(map, "BehaviorPreferredTier");
            string summary = "Tier " + tier + " / P " + priority + " / M " + memory + " / IO " + io + " / T " + trim + " / Eco " + power + " / Affinity " + affinity;
            if (!String.IsNullOrWhiteSpace(policy) && !String.Equals(policy, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                summary += " / Policy " + policy;
            }
            else if (!String.IsNullOrWhiteSpace(source) && !String.Equals(source, "auto", StringComparison.OrdinalIgnoreCase))
            {
                summary += " / " + source;
            }
            if (!String.IsNullOrWhiteSpace(role) && !String.Equals(role, "App", StringComparison.OrdinalIgnoreCase))
            {
                summary += " / " + role;
            }
            if (!String.IsNullOrWhiteSpace(guard))
            {
                summary += " / Guard " + guard;
            }
            if (!String.IsNullOrWhiteSpace(intent) && !String.Equals(intent, "Desktop", StringComparison.OrdinalIgnoreCase))
            {
                summary += " / Intent " + intent + (intentConfidence > 0 ? " " + intentConfidence.ToString(CultureInfo.CurrentCulture) : "");
            }
            if (udpEndpoints > 0)
            {
                summary += " / Zero Ping UDP " + udpEndpoints.ToString(CultureInfo.CurrentCulture) + (udpProtected ? " protected" : "");
            }
            if (udpConfidence > 0)
            {
                summary += " / UDP confidence " + (String.IsNullOrWhiteSpace(udpConfidenceLabel) ? udpConfidence.ToString(CultureInfo.CurrentCulture) + "%" : udpConfidenceLabel + " " + udpConfidence.ToString(CultureInfo.CurrentCulture) + "%");
            }
            if (!String.IsNullOrWhiteSpace(udpQosStatus) && !String.Equals(udpQosStatus, "Off", StringComparison.OrdinalIgnoreCase)) { summary += " / QoS " + udpQosStatus; }
            if (gpuHelperPressure) { summary += " / GPU helper"; }
            if (vramPressureActive) { summary += " / VRAM pressure"; }
            if (cpuBoundAssist) { summary += " / CPU-bound assist"; }
            if (behaviorConfidence > 0)
            {
                string behaviorLabel = behaviorBias < 0 ? "Guard" : (String.IsNullOrWhiteSpace(behaviorTier) ? "Auto" : behaviorTier);
                summary += " / Behavior " + behaviorConfidence.ToString(CultureInfo.CurrentCulture) + " " + behaviorLabel;
                if (behaviorWakes > 0) { summary += " / BWake " + behaviorWakes.ToString(CultureInfo.CurrentCulture); }
            }
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
                string behavior = ExtractLogValue(line, "behavior");
                string behaviorProfiles = ExtractLogValue(line, "behaviorProfiles");
                string intent = ExtractLogValue(line, "intent");
                string confidence = ExtractLogValue(line, "confidence");
                string text = time + "  APPLY";
                if (!String.IsNullOrWhiteSpace(targets)) { text += "  " + targets + " apps"; }
                if (!String.IsNullOrWhiteSpace(delta)) { text += "  " + delta + " MB"; }
                if (!String.IsNullOrWhiteSpace(light) || !String.IsNullOrWhiteSpace(balanced) || !String.IsNullOrWhiteSpace(deep)) { text += "  L/B/D " + BlankToZero(light) + "/" + BlankToZero(balanced) + "/" + BlankToZero(deep); }
                if (!String.IsNullOrWhiteSpace(trimmed)) { text += "  T " + trimmed; }
                if (!String.IsNullOrWhiteSpace(cooldown) && cooldown != "0") { text += "  C " + cooldown; }
                if (String.Equals(learning, "on", StringComparison.OrdinalIgnoreCase)) { text += "  LEARN " + BlankToZero(profiles) + " " + BlankToDash(pressure); }
                if (String.Equals(behavior, "on", StringComparison.OrdinalIgnoreCase)) { text += "  BEHAVIOR " + BlankToZero(behaviorProfiles); }
                if (!String.IsNullOrWhiteSpace(intent)) { text += "  INTENT " + intent + (String.IsNullOrWhiteSpace(confidence) ? "" : " " + confidence); }
                if (!String.IsNullOrWhiteSpace(top)) { text += "  top " + top; }
                return text;
            }
            if (String.Equals(action, "policy", StringComparison.OrdinalIgnoreCase))
            {
                string process = ExtractLogValue(line, "process");
                string policy = ExtractLogValue(line, "policy");
                string text = time + "  POLICY";
                if (!String.IsNullOrWhiteSpace(process)) { text += "  " + process; }
                if (!String.IsNullOrWhiteSpace(policy)) { text += " -> " + policy; }
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
            if (String.Equals(action, "behavior", StringComparison.OrdinalIgnoreCase))
            {
                string process = ExtractLogValue(line, "process");
                string wakes = ExtractLogValue(line, "wakes");
                string confidence = ExtractLogValue(line, "confidence");
                string tier = ExtractLogValue(line, "tier");
                string text = time + "  BEHAVIOR";
                if (!String.IsNullOrWhiteSpace(process)) { text += "  " + process; }
                if (!String.IsNullOrWhiteSpace(wakes)) { text += "  wakes " + wakes; }
                if (!String.IsNullOrWhiteSpace(confidence)) { text += "  conf " + confidence; }
                if (!String.IsNullOrWhiteSpace(tier)) { text += "  " + tier; }
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
            return FriendlyUiError(output);
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

        private static List<string> ReadStringList(IDictionary<string, object> map, string key)
        {
            List<string> result = new List<string>();
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) { return result; }
            System.Collections.IEnumerable enumerable = value as System.Collections.IEnumerable;
            if (enumerable == null || value is string)
            {
                string single = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (!String.IsNullOrWhiteSpace(single)) { result.Add(single); }
                return result;
            }
            foreach (object item in enumerable)
            {
                string text = Convert.ToString(item, CultureInfo.InvariantCulture);
                if (!String.IsNullOrWhiteSpace(text)) { result.Add(text); }
            }
            return result;
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
      <section class='cards'><div class='card'><small>Auto mode</small><b id='autoCard'>-</b></div><div class='card'><small>Managed apps</small><b id='managedCard'>-</b></div><div class='card'><small>Smart Learning</small><b id='learningCard'>-</b></div><div class='card'><small>Last event</small><b id='lastCard'>-</b></div><div class='card'><small>Last result</small><b id='resultCard'>-</b></div></section>
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
 txt('autoCard',s.AutoMode?'On':'Off'); txt('managedCard',s.Managed+' apps'); txt('learningCard',s.Learning?'On':'Off'); txt('lastCard',s.LastRun); txt('resultCard',s.Result);
 txt('motorBtn',s.AutoMode?'Pausar motor':'Retomar motor'); txt('apply',s.CanStop?'Cancelar passe':'Otimizar agora');
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
            public string SessionMode { get; set; }
            public string PowerPlanName { get; set; }
            public string PowerPlanGuid { get; set; }
            public string powerPlanName { get; set; }
            public string powerPlanGuid { get; set; }
            public string RecommendedPowerPlanName { get; set; }
            public string RecommendedPowerPlanGuid { get; set; }
            public string recommendedPowerPlanName { get; set; }
            public string recommendedPowerPlanGuid { get; set; }
            public string GamePowerPlanName { get; set; }
            public string GamePowerPlanGuid { get; set; }
            public string LivePowerPlanName { get; set; }
            public string LivePowerPlanGuid { get; set; }
            public string PreviousPowerPlanName { get; set; }
            public string PreviousPowerPlanGuid { get; set; }
            public bool EnergyIdleGuardEnabled { get; set; }
            public bool EnergyIdleGuardConfigured { get; set; }
            public int EnergyIdleGuardMinutes { get; set; }
            public bool AdaptiveExclusions { get; set; }
            public bool NetworkUdpGuard { get; set; }
            public bool NetworkUdpGuardActive { get; set; }
            public string NetworkUdpGuardMode { get; set; }
            public string NetworkUdpGuardGame { get; set; }
            public int NetworkUdpGuardEndpoints { get; set; }
            public int NetworkUdpGuardProcessCount { get; set; }
            public bool NetworkUdpGuardNoStackTweaks { get; set; }
            public int NetworkUdpGuardConfidence { get; set; }
            public string NetworkUdpGuardConfidenceLabel { get; set; }
            public string NetworkUdpGuardReason { get; set; }
            public string NetworkUdpGuardShieldMode { get; set; }
            public int NetworkUdpGuardProtectedCount { get; set; }
            public string NetworkUdpGuardQosStatus { get; set; }
            public List<string> NetworkUdpGuardSignals { get; set; }
            public bool GpuPressureAvailable { get; set; }
            public string GpuPressureProvider { get; set; }
            public string GpuPressureLevel { get; set; }
            public bool GpuPressureDxgiAvailable { get; set; }
            public string GpuPressureAdapterName { get; set; }
            public double GpuAdapterDedicatedVideoMemoryMB { get; set; }
            public double GpuAdapterSharedSystemMemoryMB { get; set; }
            public double GpuAdapterLocalBudgetMB { get; set; }
            public double GpuAdapterLocalUsageMB { get; set; }
            public double GpuAdapterLocalAvailableMB { get; set; }
            public double GpuAdapterLocalUsagePercent { get; set; }
            public double GpuAdapterNonLocalBudgetMB { get; set; }
            public double GpuAdapterNonLocalUsageMB { get; set; }
            public double GpuAdapterNonLocalAvailableMB { get; set; }
            public double GpuAdapterDedicatedUsageMB { get; set; }
            public double GpuAdapterSharedUsageMB { get; set; }
            public double GpuTotalUtilPercent { get; set; }
            public string GpuTopProcess { get; set; }
            public int GpuTopProcessPid { get; set; }
            public double GpuTopProcessPercent { get; set; }
            public double GpuTopProcessDedicatedMB { get; set; }
            public bool ShaderBoostEnabled { get; set; }
            public bool ShaderBoostObserveOnly { get; set; }
            public string ShaderBoostState { get; set; }
            public string ShaderBoostSharedState { get; set; }
            public int ShaderBoostReadiness { get; set; }
            public string ShaderBoostRecommendation { get; set; }
            public string ShaderBoostGame { get; set; }
            public int ShaderBoostGamePid { get; set; }
            public string ShaderBoostGameRoot { get; set; }
            public string ShaderBoostApi { get; set; }
            public int ShaderBoostApiConfidence { get; set; }
            public string ShaderBoostGpu { get; set; }
            public string ShaderBoostVendor { get; set; }
            public string ShaderBoostDriverVersion { get; set; }
            public string ShaderBoostCacheState { get; set; }
            public int ShaderBoostCacheLocatedCount { get; set; }
            public double ShaderBoostCacheTotalSizeMB { get; set; }
            public string ShaderBoostCacheManager { get; set; }
            public string ShaderBoostCompilationState { get; set; }
            public bool ShaderBoostCompilationPossible { get; set; }
            public string ShaderBoostPreparationMethod { get; set; }
            public string ShaderBoostWarmupState { get; set; }
            public List<string> ShaderBoostSignals { get; set; }
            public List<string> ShaderBoostDetails { get; set; }
            public bool CpuBoundAssistActive { get; set; }
            public string CpuBoundAssistGame { get; set; }
            public int CpuBoundAssistGamePid { get; set; }
            public int CpuBoundAssistConfidence { get; set; }
            public string CpuBoundAssistReason { get; set; }
            public string EngineHealthStatus { get; set; }
            public string EngineHealthSummary { get; set; }
            public int CoreProtocolVersion { get; set; }
            public int CoreMinimumSupportedProtocolVersion { get; set; }
            public string CorePipeName { get; set; }
            public string CoreContextProvider { get; set; }
            public bool CoreServiceAvailable { get; set; }
            public bool CoreServiceInstalled { get; set; }
            public bool CoreServiceRunning { get; set; }
            public string CoreServiceStatus { get; set; }
            public string CoreServiceAction { get; set; }
            public string CoreServiceHealth { get; set; }
            public string CoreServiceSummary { get; set; }
            public string CoreServiceDetail { get; set; }
            public bool CoreServiceAutoTaskInstalled { get; set; }
            public bool CoreServiceAutoTaskKicked { get; set; }
            public bool CoreServiceTelemetryFresh { get; set; }
            public bool CoreServiceTelemetryStale { get; set; }
            public bool CoreServiceNeedsAttention { get; set; }
            public int CoreServiceScoreAgeSeconds { get; set; }
            public int CoreServiceStaleThresholdSeconds { get; set; }
            public int CoreServiceLoopSeconds { get; set; }
            public int CoreServiceStateAgeSeconds { get; set; }
            public string CoreServiceUpdatedAt { get; set; }
            public bool CoreIpcListening { get; set; }
            public bool CoreIpcSecureAcl { get; set; }
            public string CoreIpcHeartbeatAt { get; set; }
            public string CoreIpcLastClientAt { get; set; }
            public string CoreIpcLastCommand { get; set; }
            public string CoreIpcLastError { get; set; }
            public bool MemoryStabilityAvailable { get; set; }
            public bool MemoryStabilityRelevant { get; set; }
            public string MemoryStabilityMode { get; set; }
            public string MemoryStabilityState { get; set; }
            public string MemoryStabilitySummary { get; set; }
            public string MemoryStabilityDetail { get; set; }
            public int MemoryStabilityMemoryLoad { get; set; }
            public double MemoryStabilityAvailablePhysicalMB { get; set; }
            public double MemoryStabilityTotalPhysicalMB { get; set; }
            public double MemoryStabilityCommitUsedMB { get; set; }
            public double MemoryStabilityCommitLimitMB { get; set; }
            public double MemoryStabilityCommitHeadroomMB { get; set; }
            public int MemoryStabilityCommitHeadroomPercent { get; set; }
            public string MemoryStabilityPagefileStatus { get; set; }
            public bool MemoryStabilityPagefileLimited { get; set; }
            public bool MemoryStabilityLowMemorySignal { get; set; }
            public bool MemoryStabilityBrowserBurstRecommended { get; set; }
            public string MemoryStabilityTopProcess { get; set; }
            public int MemoryStabilityTopProcessPid { get; set; }
            public double MemoryStabilityTopProcessPrivateMB { get; set; }
            public double MemoryStabilityTopProcessWorkingSetMB { get; set; }
            public int MemoryStabilityBrowserProcessCount { get; set; }
            public double MemoryStabilityBrowserPrivateMB { get; set; }
            public double MemoryStabilityBrowserWorkingSetMB { get; set; }
            public string MemoryStabilityBrowserBurstState { get; set; }
            public int MemoryStabilityHeavyRecentProcessCount { get; set; }
            public List<string> MemoryStabilitySignals { get; set; }
            public bool SystemIntegrityAvailable { get; set; }
            public bool SystemIntegrityRelevant { get; set; }
            public string SystemIntegrityMode { get; set; }
            public string SystemIntegrityState { get; set; }
            public string SystemIntegritySummary { get; set; }
            public string SystemIntegrityDetail { get; set; }
            public bool SystemIntegrityBackupAvailable { get; set; }
            public bool SystemIntegrityMmcssServiceRunning { get; set; }
            public string SystemIntegrityMmcssServiceStatus { get; set; }
            public int SystemIntegritySystemResponsiveness { get; set; }
            public string SystemIntegritySystemResponsivenessState { get; set; }
            public string SystemIntegritySystemResponsivenessDetail { get; set; }
            public bool SystemIntegrityHybridCpuDetected { get; set; }
            public int SystemIntegrityLogicalProcessorCount { get; set; }
            public string SystemIntegrityHybridSchedulerState { get; set; }
            public string SystemIntegrityHybridSchedulerDetail { get; set; }
            public bool SystemIntegritySelfThrottleEligible { get; set; }
            public string SystemIntegritySelfThrottleState { get; set; }
            public string SystemIntegritySelfThrottleDetail { get; set; }
            public int SystemIntegrityIssueCount { get; set; }
            public int SystemIntegrityRecommendationCount { get; set; }
            public int SystemIntegritySafeRecommendationCount { get; set; }
            public int SystemIntegrityOptionalRecommendationCount { get; set; }
            public int SystemIntegrityExperimentalRecommendationCount { get; set; }
            public int SystemIntegrityRestartRecommendationCount { get; set; }
            public int SystemIntegrityApplyBlockedRecommendationCount { get; set; }
            public List<Dictionary<string, object>> SystemIntegrityRecommendations { get; set; }
            public List<string> SystemIntegritySignals { get; set; }
            public List<string> SystemIntegrityIssues { get; set; }
            public bool LowImpactRuntimeAvailable { get; set; }
            public bool LowImpactRuntimeActive { get; set; }
            public string LowImpactRuntimeReason { get; set; }
            public int LowImpactRuntimeCadenceSeconds { get; set; }
            public bool SessionAgentAvailable { get; set; }
            public string SessionAgentHealth { get; set; }
            public string SessionAgentState { get; set; }
            public string SessionAgentUpdatedAt { get; set; }
            public int SessionAgentStateAgeSeconds { get; set; }
            public string SessionAgentContext { get; set; }
            public int SessionAgentConfidence { get; set; }
            public int SessionAgentForegroundPid { get; set; }
            public string SessionAgentForegroundProcessName { get; set; }
            public bool SessionAgentForegroundFullscreen { get; set; }
            public bool SessionAgentStreamingObserved { get; set; }
            public bool RollbackAuditEnabled { get; set; }
            public bool StreamGuardActive { get; set; }
            public int StreamHelperCount { get; set; }
            public int StreamGameProtectedCount { get; set; }
            public int PolicyCount { get; set; }
            public int ManualPolicyCount { get; set; }
            public int AppCount { get; set; }
            public int ProcessCount { get; set; }
            public int PreviewTargets { get; set; }
            public int PreviewWouldTrim { get; set; }
            public string PreviewTop { get; set; }
            public string PreviewAt { get; set; }
            public string PreviewResult { get; set; }
            public string PreviewDetail { get; set; }
            public bool Learning { get; set; }
            public int LearningProfiles { get; set; }
            public bool Behavior { get; set; }
            public int BehaviorProfiles { get; set; }
            public string MemoryPressure { get; set; }
            public double FreeMemoryMB { get; set; }
            public string IntentKind { get; set; }
            public string IntentName { get; set; }
            public int IntentConfidence { get; set; }
            public List<string> IntentSignals { get; set; }
            public string RadarTop { get; set; }
            public int RadarCount { get; set; }
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
            public string HardwareCpu { get; set; }
            public string HardwareCpuDetail { get; set; }
            public string HardwareRam { get; set; }
            public string HardwareRamDetail { get; set; }
            public string HardwareGpu { get; set; }
            public string HardwareGpuDetail { get; set; }
            public string HardwareOs { get; set; }
            public string AvailableMemoryText { get; set; }
            public string HardwareSystemDetail { get; set; }
            public double HardwareRamTotalMB { get; set; }
            public double HardwareRamFreeMB { get; set; }
            public double HardwarePageFileTotalMB { get; set; }
            public double HardwarePageFileFreeMB { get; set; }
            public double HardwareVirtualTotalMB { get; set; }
            public double HardwareVirtualFreeMB { get; set; }
            public int HardwareMemoryLoad { get; set; }
            public int HardwareCpuClockMhz { get; set; }
            public int HardwareCpuMaxMhz { get; set; }
            public List<WebManagerRow> Rows { get; set; }
            public Dictionary<string, List<string>> AppTimelines { get; set; }
            public List<string> Events { get; set; }
            public List<WebGamePreset> GamePresets { get; set; }
            public bool GameBetaWelcome { get; set; }
            public bool UpdateAutoChecks { get; set; }
            public bool UpdateChecking { get; set; }
            public bool UpdateAvailable { get; set; }
            public bool UpdateIgnored { get; set; }
            public string UpdateLatestTag { get; set; }
            public string UpdateLatestVersion { get; set; }
            public string UpdateReleaseName { get; set; }
            public string UpdateReleaseUrl { get; set; }
            public string UpdateDownloadUrl { get; set; }
            public string UpdatePublishedAt { get; set; }
            public string UpdateError { get; set; }
            public bool PostUpdateNotice { get; set; }
            public string PostUpdateBody { get; set; }
            public List<string> PostUpdateItems { get; set; }
        }

        private sealed class PreviewSummary
        {
            public int Targets { get; set; }
            public int WouldTrim { get; set; }
            public string TopApp { get; set; }
            public string TimestampText { get; set; }
            public string ShortText { get; set; }
            public string Detail { get; set; }
        }
        private sealed class ScoreMeta
        {
            public int AppCount { get; set; }
            public int ProcessCount { get; set; }
            public bool LearningEnabled { get; set; }
            public int LearningProfiles { get; set; }
            public bool BehaviorEnabled { get; set; }
            public int BehaviorProfiles { get; set; }
            public string MemoryPressure { get; set; }
            public double FreeMemoryMB { get; set; }
            public string IntentKind { get; set; }
            public string IntentName { get; set; }
            public int IntentConfidence { get; set; }
            public List<string> IntentSignals { get; set; }
            public bool NetworkUdpGuardEnabled { get; set; }
            public bool NetworkUdpGuardActive { get; set; }
            public string NetworkUdpGuardMode { get; set; }
            public string NetworkUdpGuardGame { get; set; }
            public int NetworkUdpGuardEndpoints { get; set; }
            public int NetworkUdpGuardProcessCount { get; set; }
            public bool NetworkUdpGuardNoStackTweaks { get; set; }
            public int NetworkUdpGuardConfidence { get; set; }
            public string NetworkUdpGuardConfidenceLabel { get; set; }
            public string NetworkUdpGuardReason { get; set; }
            public string NetworkUdpGuardShieldMode { get; set; }
            public int NetworkUdpGuardProtectedCount { get; set; }
            public string NetworkUdpGuardQosStatus { get; set; }
            public List<string> NetworkUdpGuardSignals { get; set; }
            public bool GpuPressureAvailable { get; set; }
            public string GpuPressureProvider { get; set; }
            public string GpuPressureLevel { get; set; }
            public bool GpuPressureDxgiAvailable { get; set; }
            public string GpuPressureAdapterName { get; set; }
            public double GpuAdapterDedicatedVideoMemoryMB { get; set; }
            public double GpuAdapterSharedSystemMemoryMB { get; set; }
            public double GpuAdapterLocalBudgetMB { get; set; }
            public double GpuAdapterLocalUsageMB { get; set; }
            public double GpuAdapterLocalAvailableMB { get; set; }
            public double GpuAdapterLocalUsagePercent { get; set; }
            public double GpuAdapterNonLocalBudgetMB { get; set; }
            public double GpuAdapterNonLocalUsageMB { get; set; }
            public double GpuAdapterNonLocalAvailableMB { get; set; }
            public double GpuAdapterDedicatedUsageMB { get; set; }
            public double GpuAdapterSharedUsageMB { get; set; }
            public double GpuTotalUtilPercent { get; set; }
            public string GpuTopProcess { get; set; }
            public int GpuTopProcessPid { get; set; }
            public double GpuTopProcessPercent { get; set; }
            public double GpuTopProcessDedicatedMB { get; set; }
            public bool ShaderBoostEnabled { get; set; }
            public bool ShaderBoostObserveOnly { get; set; }
            public string ShaderBoostState { get; set; }
            public string ShaderBoostSharedState { get; set; }
            public int ShaderBoostReadiness { get; set; }
            public string ShaderBoostRecommendation { get; set; }
            public string ShaderBoostGame { get; set; }
            public int ShaderBoostGamePid { get; set; }
            public string ShaderBoostGameRoot { get; set; }
            public string ShaderBoostApi { get; set; }
            public int ShaderBoostApiConfidence { get; set; }
            public string ShaderBoostGpu { get; set; }
            public string ShaderBoostVendor { get; set; }
            public string ShaderBoostDriverVersion { get; set; }
            public string ShaderBoostCacheState { get; set; }
            public int ShaderBoostCacheLocatedCount { get; set; }
            public double ShaderBoostCacheTotalSizeMB { get; set; }
            public string ShaderBoostCacheManager { get; set; }
            public string ShaderBoostCompilationState { get; set; }
            public bool ShaderBoostCompilationPossible { get; set; }
            public string ShaderBoostPreparationMethod { get; set; }
            public string ShaderBoostWarmupState { get; set; }
            public List<string> ShaderBoostSignals { get; set; }
            public List<string> ShaderBoostDetails { get; set; }
            public bool CpuBoundAssistActive { get; set; }
            public string CpuBoundAssistGame { get; set; }
            public int CpuBoundAssistGamePid { get; set; }
            public int CpuBoundAssistConfidence { get; set; }
            public string CpuBoundAssistReason { get; set; }
            public string EngineHealthStatus { get; set; }
            public string EngineHealthSummary { get; set; }
            public bool RollbackAuditEnabled { get; set; }
            public bool StreamGuardActive { get; set; }
            public int StreamHelperCount { get; set; }
            public int StreamGameProtectedCount { get; set; }
            public string RadarTop { get; set; }
            public int RadarCount { get; set; }
            public int PermissionDeniedCount { get; set; }
            public List<string> PermissionDeniedApps { get; set; }
        }

        private sealed class WebManagerRow
        {
            public string Name { get; set; }
            public string ProcessName { get; set; }
            public string Key { get; set; }
            public string Path { get; set; }
            public string Score { get; set; }
            public string Delta { get; set; }
            public string Cpu { get; set; }
            public string Bursts { get; set; }
            public string Action { get; set; }
            public string Role { get; set; }
            public string Policy { get; set; }
            public string PolicySource { get; set; }
            public string Guard { get; set; }
            public string Intent { get; set; }
            public int IntentConfidence { get; set; }
            public int UdpEndpoints { get; set; }
            public bool UdpGameProtected { get; set; }
            public bool UdpGuardActive { get; set; }
            public int UdpConfidence { get; set; }
            public string UdpConfidenceLabel { get; set; }
            public string UdpConfidenceReason { get; set; }
            public string UdpShieldMode { get; set; }
            public string UdpQosStatus { get; set; }
            public double GpuPercent { get; set; }
            public double GpuDedicatedMB { get; set; }
            public double GpuSharedMB { get; set; }
            public bool GpuHelperPressure { get; set; }
            public bool VramPressureActive { get; set; }
            public bool CpuBoundAssist { get; set; }
            public int BehaviorConfidence { get; set; }
            public int BehaviorWakeCount { get; set; }
            public int BehaviorBias { get; set; }
            public string BehaviorTier { get; set; }
            public string BehaviorReason { get; set; }
            public bool SwitchFastWake { get; set; }
            public bool PermissionDenied { get; set; }
            public double RawScore { get; set; }
            public double RepresentativeScore { get; set; }
            public double RawDelta { get; set; }
            public double RawCpu { get; set; }
            public int RawBursts { get; set; }
            public int InstanceCount { get; set; }
        }
    }
#endif

    #if !NET9_0_OR_GREATER
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

            bool autoInstalled = IsAutomaticEngineEnabled();
            bool startupInstalled = IsStartupInstalled();
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

        private static List<GamePresetDefinition> GetGamePresetDefinitions()
        {
            return new List<GamePresetDefinition>
            {
                new GamePresetDefinition
                {
                    Id = "bf6",
                    Name = "Battlefield 6",
                    ShortName = "BF6",
                    Tier = "Competitive FPS",
                    Accent = "orange",
                    Description = "Biblioteca beta com ajustes de comunidade para cache/shader, frame pacing, CPU-bound e stutter em partidas grandes.",
                    ProcessNames = new[] { "bf6", "Battlefield6", "Battlefield" },
                    InstallKeywords = new[] { "Battlefield 6", "Battlefield6", "Battlefield" },
                    SafeOptions = new[] { "Pipeline de shader/cache: backup e reconstrução guiada após update", "Frame pacing competitivo: cap estável por Hz e anti-stutter", "Config de engine segura: streaming e orçamento de CPU mais leve", "DX12/driver cache hygiene: remover cache antigo sem apagar saves", "Overlay/download guard: EA, Steam, Discord e capturas em modo leve", "Preset CPU-bound: reduzir pós-processamento pesado sem mudar controles" },
                    ExperimentalOptions = new[] { "user.cfg avançado: thread budget e render queue em teste A/B", "Cache rebuild agressivo: DX/NVIDIA/AMD shader cache com aviso de stutter inicial", "Ultra low CPU fallback: streaming, efeitos e partículas em orçamento mínimo", "Overlay hard-off test: EA/Steam por config reversível com backup", "Frame cap lab: 90/120/144/165 com medição de pacing" }
                },
                new GamePresetDefinition
                {
                    Id = "eafc26",
                    Name = "EA SPORTS FC 26",
                    ShortName = "FC26",
                    Tier = "Sports online",
                    Accent = "cyan",
                    Description = "Receitas de comunidade para reduzir stutter, estabilizar frame pacing e controlar EA/Steam sem alterar gameplay.",
                    ProcessNames = new[] { "FC26", "FC25", "FC24" },
                    InstallKeywords = new[] { "EA SPORTS FC 26", "EA Sports FC 26", "FC26", "EA SPORTS FC" },
                    SafeOptions = new[] { "FC setup sanity: detectar arquivo de settings e fazer backup", "Stutter guard: cap de FPS por Hz e estabilidade de cutscenes", "EA/Steam overlay guard: reduzir overlays e downloads durante partida", "Shader/cache refresh guiado após update de driver ou patch", "CPU/GPU balance: crowd, hair e cloth em perfil de desempenho", "Fullscreen e Hz corretos sem mexer em câmera, controle ou gameplay" },
                    ExperimentalOptions = new[] { "FC microstutter lab: caps 60/90/120/144 com rollback", "Stadium heavy preset: reduzir crowd, cloth e hair para PC fraco", "EA overlay hard-off via user_*.ini com backup", "Cache rebuild agressivo em Documents/AppData quando há travadas", "Steam input/overlay isolation quando FC abre via Steam" }
                },
                new GamePresetDefinition
                {
                    Id = "cs2",
                    Name = "Counter-Strike 2",
                    ShortName = "CS2",
                    Tier = "Competitive shooter",
                    Accent = "blue",
                    Description = "Receitas competitivas de comunidade para launch options limpas, autoexec, shader cache, Reflex e frame pacing.",
                    ProcessNames = new[] { "cs2" },
                    InstallKeywords = new[] { "Counter-Strike Global Offensive", "Counter-Strike 2", "csgo", "cs2" },
                    SafeOptions = new[] { "Launch options auditor: remover comandos antigos ou prejudiciais", "Autoexec performance pack: telemetria, pacing e cvars seguras", "NVIDIA Reflex check: orientar ON quando suportado", "Shader prewarm/cache hygiene após update do jogo ou driver", "Frame cap estável por Hz para reduzir variação de frametime", "Steam overlay/download guard durante partida competitiva" },
                    ExperimentalOptions = new[] { "-vulkan A/B test com reversão automática", "fps_max lab: 0, refresh+buffer ou cap competitivo", "DX shader cache rebuild agressivo", "Low-end cfg: partículas, decals e streaming budget reduzidos", "Workshop/custom cfg quarantine para caçar stutter" }
                },
                new GamePresetDefinition
                {
                    Id = "valorant",
                    Name = "VALORANT",
                    ShortName = "VALORANT",
                    Tier = "Tactical FPS",
                    Accent = "violet",
                    Description = "Receitas seguras de comunidade para FPS alto, baixa latência, cache limpo e estabilidade sem tocar no Vanguard.",
                    ProcessNames = new[] { "VALORANT-Win64-Shipping", "VALORANT" },
                    InstallKeywords = new[] { "VALORANT", "Riot Games" },
                    SafeOptions = new[] { "Config backup Riot e validação de GameUserSettings", "Multithreaded Rendering check quando a CPU suporta", "NVIDIA Reflex/low latency check quando suportado", "FPS cap por menu/background para aliviar stutter térmico", "Fullscreen e Hz sanity sem tocar em sensibilidade ou mira", "Overlay/download guard sem tocar no Vanguard" },
                    ExperimentalOptions = new[] { "FPS cap lab por cenário: menu, background e in-game", "Low-end GPU profile: material, detail e UI em modo performance", "Cache/config reset guiado com backup", "Overlay hard isolation sem mexer no Vanguard", "Frame pacing stress test por monitor" }
                }
            };
        }

        private static List<WebGamePreset> BuildGamePresetsForUi()
        {
            List<WebGamePreset> output = new List<WebGamePreset>();
            List<Process> processes = new List<Process>();
            try { processes.AddRange(Process.GetProcesses()); } catch { }
            try
            {
                foreach (GamePresetDefinition definition in GetGamePresetDefinitions())
                {
                    Process running = null;
                    string runningPath = "";
                    foreach (Process process in processes)
                    {
                        string processName = "";
                        try { processName = process.ProcessName ?? ""; } catch { }
                        if (!NameInList(processName, definition.ProcessNames)) { continue; }
                        if (IsKnownLauncherProcessForReactive(processName)) { continue; }
                        running = process;
                        runningPath = TryGetProcessPath(process);
                        break;
                    }

                    string installedPath = !String.IsNullOrWhiteSpace(runningPath) ? runningPath : FindGameInstallPath(definition);
                    output.Add(new WebGamePreset
                    {
                        Id = definition.Id,
                        Name = definition.Name,
                        ShortName = definition.ShortName,
                        Tier = definition.Tier,
                        Genre = definition.Tier,
                        Accent = definition.Accent,
                        Summary = definition.Description,
                        Description = definition.Description,
                        ExpectedGain = definition.Tier,
                        CoverDataUrl = GetGameCoverDataUrl(definition.Id),
                        Installed = !String.IsNullOrWhiteSpace(installedPath),
                        Running = running != null,
                        ProcessName = running == null ? "" : running.ProcessName,
                        ProcessId = running == null ? 0 : running.Id,
                        Path = installedPath,
                        DetectedPath = installedPath,
                        Status = running != null ? "Running" : (!String.IsNullOrWhiteSpace(installedPath) ? "Installed" : "Not found"),
                        SafeOptions = new List<string>(definition.SafeOptions ?? new string[0]),
                        ExperimentalOptions = new List<string>(definition.ExperimentalOptions ?? new string[0]),
                        SafeOptimizations = new List<string>(definition.SafeOptions ?? new string[0]),
                        ExperimentalOptimizations = new List<string>(definition.ExperimentalOptions ?? new string[0])
                    });
                }
            }
            finally
            {
                foreach (Process process in processes) { try { process.Dispose(); } catch { } }
            }
            return output;
        }

        private static int RefreshGameDiscoveryCache()
        {
            int found = 0;
            foreach (GamePresetDefinition definition in GetGamePresetDefinitions())
            {
                try
                {
                    string path = FindGameInstallPath(definition);
                    if (String.IsNullOrWhiteSpace(path)) { continue; }
                    SaveManualGameInstallPath(definition, path);
                    found++;
                }
                catch { }
            }
            return found;
        }
        private static string FindGameInstallPath(GamePresetDefinition definition)
        {
            if (definition == null) { return ""; }
            string running = FindGameInstallPathFromRunningProcess(definition);
            if (!String.IsNullOrWhiteSpace(running)) { return running; }

            foreach (string candidate in BuildGameInstallCandidates(definition))
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(candidate)) { continue; }
                    string normalized = candidate.Trim().Trim('"');
                    if (File.Exists(normalized)) { return normalized; }
                    if (Directory.Exists(normalized)) { return normalized; }
                }
                catch { }
            }
            return "";
        }

        private static string FindGameInstallPathFromRunningProcess(GamePresetDefinition definition)
        {
            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    using (process)
                    {
                        string name = "";
                        try { name = process.ProcessName ?? ""; } catch { }
                        if (!NameInList(name, definition.ProcessNames)) { continue; }
                        if (IsKnownLauncherProcessForReactive(name)) { continue; }
                        string path = TryGetProcessPath(process);
                        if (!String.IsNullOrWhiteSpace(path)) { return path; }
                    }
                }
            }
            catch { }
            return "";
        }

        private static List<string> BuildGameInstallCandidates(GamePresetDefinition definition)
        {
            List<string> candidates = new List<string>();
            Action<string> add = delegate(string value)
            {
                if (!String.IsNullOrWhiteSpace(value) && !candidates.Contains(value, StringComparer.OrdinalIgnoreCase)) { candidates.Add(value); }
            };

            foreach (string root in BuildGameInstallRoots())
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) { continue; }
                    if (MatchesGamePath(root, definition)) { add(root); }
                    foreach (string dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (MatchesGamePath(dir, definition) || ContainsGameExecutable(dir, definition)) { add(dir); }
                    }
                }
                catch { }
            }

            AddShortcutGameCandidates(definition, add);
            return candidates;
        }

        private static bool MatchesGamePath(string value, GamePresetDefinition definition)
        {
            string text = value ?? "";
            foreach (string keyword in definition.InstallKeywords ?? new string[0])
            {
                if (!String.IsNullOrWhiteSpace(keyword) && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            }
            foreach (string process in definition.ProcessNames ?? new string[0])
            {
                if (!String.IsNullOrWhiteSpace(process) && text.IndexOf(process, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            }
            return false;
        }

        private static IEnumerable<string> EnumerateFilesLimited(string root, string pattern, int maxDepth, int maxFiles)
        {
            if (String.IsNullOrWhiteSpace(root)) { yield break; }
            try { if (!Directory.Exists(root)) { yield break; } } catch { yield break; }
            Queue<Tuple<string, int>> queue = new Queue<Tuple<string, int>>();
            queue.Enqueue(Tuple.Create(root, 0));
            int emitted = 0;
            while (queue.Count > 0 && emitted < maxFiles)
            {
                Tuple<string, int> item = queue.Dequeue();
                string dir = item.Item1;
                int depth = item.Item2;
                string[] files = new string[0];
                try { files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly); } catch { }
                foreach (string file in files)
                {
                    yield return file;
                    emitted++;
                    if (emitted >= maxFiles) { yield break; }
                }
                if (depth >= maxDepth) { continue; }
                string[] dirs = new string[0];
                try { dirs = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly); } catch { }
                foreach (string child in dirs) { queue.Enqueue(Tuple.Create(child, depth + 1)); }
            }
        }
        private static bool ContainsGameExecutable(string directory, GamePresetDefinition definition)
        {
            try
            {
                foreach (string process in definition.ProcessNames ?? new string[0])
                {
                    if (String.IsNullOrWhiteSpace(process)) { continue; }
                    string exe = process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? process : process + ".exe";
                    if (File.Exists(Path.Combine(directory, exe))) { return true; }
                    foreach (string file in EnumerateFilesLimited(directory, exe, 4, 700))
                    {
                        if (file.IndexOf("launcher", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static List<string> BuildGameInstallRoots()
        {
            List<string> roots = new List<string>();
            Action<string> add = delegate(string value)
            {
                if (!String.IsNullOrWhiteSpace(value) && !roots.Contains(value, StringComparer.OrdinalIgnoreCase)) { roots.Add(value); }
            };
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            add(Path.Combine(pfx86, "Steam", "steamapps", "common"));
            add(Path.Combine(pf, "Steam", "steamapps", "common"));
            AddSteamLibraryRoots(add);
            add(Path.Combine(pf, "EA Games"));
            add(Path.Combine(pfx86, "EA Games"));
            add(Path.Combine(pf, "Electronic Arts", "Games"));
            add(Path.Combine(pfx86, "Origin Games"));
            add(Path.Combine(pf, "Epic Games"));
            AddEpicInstallRoots(add);
            add(Path.Combine(pf, "Riot Games"));
            add(Path.Combine(pfx86, "Riot Games"));
            AddRiotInstallRoots(add);
            add(Path.Combine(pd, "Battle.net"));
            return roots;
        }

        private static void AddSteamLibraryRoots(Action<string> add)
        {
            foreach (string steamRoot in GetSteamRoots())
            {
                string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                try
                {
                    if (File.Exists(vdf))
                    {
                        string text = File.ReadAllText(vdf);
                        foreach (Match match in Regex.Matches(text, "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\""))
                        {
                            string path = Regex.Unescape(match.Groups[1].Value.Replace("\\\\", "\\"));
                            add(Path.Combine(path, "steamapps", "common"));
                        }
                    }
                }
                catch { }
                add(Path.Combine(steamRoot, "steamapps", "common"));
            }
        }

        private static IEnumerable<string> GetSteamRoots()
        {
            List<string> roots = new List<string>();
            Action<string> add = delegate(string value)
            {
                if (!String.IsNullOrWhiteSpace(value) && !roots.Contains(value, StringComparer.OrdinalIgnoreCase)) { roots.Add(value); }
            };
            try { using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) { add(Convert.ToString(key == null ? null : key.GetValue("SteamPath"))); } } catch { }
            try { using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")) { add(Convert.ToString(key == null ? null : key.GetValue("InstallPath"))); } } catch { }
            add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
            return roots;
        }

        private static void AddEpicInstallRoots(Action<string> add)
        {
            string manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
            try
            {
                if (!Directory.Exists(manifests)) { return; }
                foreach (string file in Directory.EnumerateFiles(manifests, "*.item", SearchOption.TopDirectoryOnly))
                {
                    string text = File.ReadAllText(file);
                    Match m = Regex.Match(text, "\\\"InstallLocation\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
                    if (m.Success) { add(Regex.Unescape(m.Groups[1].Value)); }
                }
            }
            catch { }
        }

        private static void AddRiotInstallRoots(Action<string> add)
        {
            string json = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "RiotClientInstalls.json");
            try
            {
                if (File.Exists(json))
                {
                    foreach (Match match in Regex.Matches(File.ReadAllText(json), "[A-Za-z]:\\\\(?:[^\\\"\\r\\n])+"))
                    {
                        string path = match.Value.Replace("\\\\", "\\");
                        string dir = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                        if (!String.IsNullOrWhiteSpace(dir)) { add(dir); }
                    }
                }
            }
            catch { }
        }

        private static void AddShortcutGameCandidates(GamePresetDefinition definition, Action<string> add)
        {
            foreach (string root in GetShortcutSearchRoots())
            {
                try
                {
                    if (!Directory.Exists(root)) { continue; }
                    foreach (string shortcut in EnumerateFilesLimited(root, "*.lnk", 3, 600))
                    {
                        if (!MatchesGamePath(shortcut, definition)) { continue; }
                        string target = TryGetShortcutTarget(shortcut);
                        if (String.IsNullOrWhiteSpace(target)) { continue; }
                        if (File.Exists(target)) { add(target); add(Path.GetDirectoryName(target)); }
                        else if (Directory.Exists(target)) { add(target); }
                    }
                }
                catch { }
            }
        }

        private static IEnumerable<string> GetShortcutSearchRoots()
        {
            yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "Start Menu", "Programs");
        }

        private static string TryGetShortcutTarget(string shortcutPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) { return ""; }
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                string target = Convert.ToString(shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null));
                try { Marshal.FinalReleaseComObject(shortcut); } catch { }
                try { Marshal.FinalReleaseComObject(shell); } catch { }
                return target ?? "";
            }
            catch { return ""; }
        }

        private RunResult ApplyGamePresetFromMessage(IDictionary<string, object> message)
        {
            string gameId = GetMapString(message, "gameId");
            GamePresetDefinition definition = GetGamePresetDefinitions().Find(delegate(GamePresetDefinition item) { return String.Equals(item.Id, gameId, StringComparison.OrdinalIgnoreCase); });
            if (definition == null) { return new RunResult(1, "Preset de jogo desconhecido."); }

            List<string> selectedSafeOptions = GetMapStringList(message, "safeOptions");
            List<string> selectedExperimentalOptions = GetMapStringList(message, "experimentalOptions");
            if (selectedSafeOptions.Count == 0) { selectedSafeOptions = new List<string>(definition.SafeOptions ?? new string[0]); }
            bool experimental = selectedExperimentalOptions.Count > 0 || GetBool(message, "experimental");
            int backupFiles = EnsureGamePresetFileBackups(definition);

            SaveGamePresetState(definition, experimental, selectedSafeOptions, selectedExperimentalOptions, backupFiles);
            AppendOperationalLog("action=game-preset game=" + definition.ShortName.Replace(' ', '_') + " safe=" + selectedSafeOptions.Count.ToString(CultureInfo.InvariantCulture) + " experimental=" + selectedExperimentalOptions.Count.ToString(CultureInfo.InvariantCulture) + " backups=" + backupFiles.ToString(CultureInfo.InvariantCulture) + " session=unchanged");
            return new RunResult(0, "Preset de jogo salvo: " + definition.Name + ". O modo atual do motor foi mantido.");
        }

        private void SaveGamePolicy(string processName, string policy)
        {
            Dictionary<string, object> message = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            message["policy"] = policy;
            message["processName"] = processName;
            message["key"] = "name:" + (processName ?? "").Trim().ToLowerInvariant();
            message["path"] = "";
            SetAppPolicyFromMessage(message);
        }

        private static void SaveGamePresetState(GamePresetDefinition definition, bool experimental, List<string> safeOptions, List<string> experimentalOptions, int backupFiles)
        {
            Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            root["Timestamp"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            root["LastGameId"] = definition.Id;
            root["LastGameName"] = definition.Name;
            root["Experimental"] = experimental;
            root["SafeOptions"] = safeOptions == null || safeOptions.Count == 0 ? new List<string>(definition.SafeOptions ?? new string[0]) : new List<string>(safeOptions);
            root["ExperimentalOptions"] = experimentalOptions == null ? new List<string>() : new List<string>(experimentalOptions);
            root["BackupFiles"] = backupFiles;
            root["Restored"] = false;
            AtomicWriteJsonMap(Path.Combine(outputsPath, "game-presets.state.json"), root);
        }

        private static string GetGamePresetBackupRoot()
        {
            return Path.Combine(outputsPath, "game-preset-backups");
        }

        private static string HashGamePresetTarget(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes((value ?? "").Trim().ToLowerInvariant()));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < bytes.Length && i < 16; i++) { sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture)); }
                return sb.ToString();
            }
        }

        private static void AddExistingFile(List<string> files, string path)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(path) && File.Exists(path) && !files.Contains(path, StringComparer.OrdinalIgnoreCase)) { files.Add(path); }
            }
            catch { }
        }

        private static List<string> BuildGamePresetBackupCandidates(GamePresetDefinition definition)
        {
            List<string> files = new List<string>();
            string id = (definition == null ? "" : definition.Id ?? "").Trim().ToLowerInvariant();
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string install = definition == null ? "" : FindGameInstallPath(definition);

            if (id == "bf6")
            {
                foreach (string folder in new[] { "Battlefield 6", "Battlefield6", "Battlefield 2042" })
                {
                    string settings = Path.Combine(docs, folder, "settings");
                    AddExistingFile(files, Path.Combine(settings, "PROFSAVE_profile"));
                    AddExistingFile(files, Path.Combine(settings, "PROFSAVE"));
                    AddExistingFile(files, Path.Combine(settings, "PROFSAVE_tmp"));
                }
                AddExistingFile(files, Path.Combine(install, "user.cfg"));
            }
            else if (id == "eafc26")
            {
                foreach (string folder in new[] { "FC 26", "EA SPORTS FC 26", "FC 25", "EA SPORTS FC 25", "FC 24", "EA SPORTS FC 24" })
                {
                    string root = Path.Combine(docs, folder);
                    AddExistingFile(files, Path.Combine(root, "fcsetup.ini"));
                    AddExistingFile(files, Path.Combine(root, "fifasetup.ini"));
                    AddExistingFile(files, Path.Combine(root, "settings.ini"));
                    AddExistingFile(files, Path.Combine(root, "buttonDataSetup.ini"));
                }
            }
            else if (id == "cs2")
            {
                foreach (string root in BuildGameInstallRoots())
                {
                    string steam = root;
                    if (steam.EndsWith(Path.Combine("steamapps", "common"), StringComparison.OrdinalIgnoreCase))
                    {
                        DirectoryInfo steamApps = Directory.GetParent(steam);
                        DirectoryInfo steamRoot = steamApps == null ? null : steamApps.Parent;
                        string userData = steamRoot == null ? "" : Path.Combine(steamRoot.FullName, "userdata");
                        try
                        {
                            if (Directory.Exists(userData))
                            {
                                foreach (string cfg in Directory.EnumerateFiles(userData, "*.cfg", SearchOption.AllDirectories))
                                {
                                    if (cfg.IndexOf(Path.Combine("730", "local", "cfg"), StringComparison.OrdinalIgnoreCase) >= 0) { AddExistingFile(files, cfg); }
                                }
                                foreach (string txt in Directory.EnumerateFiles(userData, "*.txt", SearchOption.AllDirectories))
                                {
                                    if (txt.IndexOf(Path.Combine("730", "local", "cfg"), StringComparison.OrdinalIgnoreCase) >= 0) { AddExistingFile(files, txt); }
                                }
                            }
                        }
                        catch { }
                    }
                }
                AddExistingFile(files, Path.Combine(install, "game", "csgo", "cfg", "autoexec.cfg"));
            }
            else if (id == "valorant")
            {
                string configRoot = Path.Combine(local, "VALORANT", "Saved", "Config");
                try
                {
                    if (Directory.Exists(configRoot))
                    {
                        foreach (string file in Directory.EnumerateFiles(configRoot, "GameUserSettings.ini", SearchOption.AllDirectories)) { AddExistingFile(files, file); }
                    }
                }
                catch { }
            }

            return files;
        }

        private static int EnsureGamePresetFileBackups(GamePresetDefinition definition)
        {
            List<string> files = BuildGamePresetBackupCandidates(definition);
            int ready = 0;
            foreach (string target in files)
            {
                try
                {
                    string id = (definition == null ? "unknown" : definition.Id ?? "unknown").Trim().ToLowerInvariant();
                    string itemDir = Path.Combine(GetGamePresetBackupRoot(), id, HashGamePresetTarget(target));
                    string backupPath = Path.Combine(itemDir, "original.bin");
                    string targetPath = Path.Combine(itemDir, "target.txt");
                    Directory.CreateDirectory(itemDir);
                    if (!File.Exists(backupPath)) { File.Copy(target, backupPath, false); }
                    if (!File.Exists(targetPath)) { AtomicWriteAllText(targetPath, target, Encoding.UTF8); }
                    ready++;
                }
                catch { }
            }
            return ready;
        }

        private static int RestoreGamePresetFileBackups(string gameId)
        {
            string root = GetGamePresetBackupRoot();
            if (!Directory.Exists(root)) { return 0; }
            int restored = 0;
            string normalized = (gameId ?? "").Trim().ToLowerInvariant();
            try
            {
                foreach (string gameDir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                {
                    string currentId = Path.GetFileName(gameDir) ?? "";
                    if (!String.IsNullOrWhiteSpace(normalized) && !String.Equals(currentId, normalized, StringComparison.OrdinalIgnoreCase)) { continue; }
                    foreach (string itemDir in Directory.EnumerateDirectories(gameDir, "*", SearchOption.TopDirectoryOnly))
                    {
                        string backupPath = Path.Combine(itemDir, "original.bin");
                        string targetPathFile = Path.Combine(itemDir, "target.txt");
                        if (!File.Exists(backupPath) || !File.Exists(targetPathFile)) { continue; }
                        string target = File.ReadAllText(targetPathFile, Encoding.UTF8).Trim();
                        if (String.IsNullOrWhiteSpace(target)) { continue; }
                        string dir = Path.GetDirectoryName(target);
                        if (!String.IsNullOrWhiteSpace(dir)) { Directory.CreateDirectory(dir); }
                        File.Copy(backupPath, target, true);
                        restored++;
                    }
                }
            }
            catch { }
            return restored;
        }

        private RunResult RestoreGamePresetFromMessage(IDictionary<string, object> message)
        {
            string gameId = GetMapString(message, "gameId");
            if (!String.IsNullOrWhiteSpace(gameId))
            {
                GamePresetDefinition selected = GetGamePresetDefinitions().Find(delegate(GamePresetDefinition item) { return String.Equals(item.Id, gameId, StringComparison.OrdinalIgnoreCase); });
                if (selected == null) { return new RunResult(1, "Preset de jogo desconhecido."); }
            }

            int restored = RestoreGamePresetFileBackups(gameId);
            SaveGamePresetRestoreState(String.IsNullOrWhiteSpace(gameId) ? "all" : gameId, restored);
            AppendOperationalLog("action=game-preset-restore target=" + (String.IsNullOrWhiteSpace(gameId) ? "all" : gameId) + " files=" + restored.ToString(CultureInfo.InvariantCulture) + " session=unchanged");
            if (restored <= 0) { return new RunResult(0, "Nenhum arquivo alterado pela aba Jogos para restaurar."); }
            return new RunResult(0, "Arquivos do preset restaurados: " + restored.ToString(CultureInfo.InvariantCulture) + ".");
        }

        private static void SaveGamePresetRestoreState(string target, int files)
        {
            Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            root["Timestamp"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            root["LastGameId"] = target;
            root["LastGameName"] = target;
            root["Experimental"] = false;
            root["SafeOptions"] = new List<string>();
            root["ExperimentalOptions"] = new List<string>();
            root["Restored"] = true;
            root["RestoredFiles"] = files;
            AtomicWriteJsonMap(Path.Combine(outputsPath, "game-presets.state.json"), root);
        }

        private static bool ShouldShowGameBetaWelcome()
        {
            return !ReadUiFlag("GameBetaWelcomeSeen");
        }

        private static void MarkGameBetaWelcomeSeen()
        {
            SaveUiFlag("GameBetaWelcomeSeen", true);
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
                    if (result.ExitCode != 0 && !ShouldSuppressRunModal(result.Output))
                    {
                        System.Windows.MessageBox.Show(ShortError(result.Output), AppName, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                }));
            });
        }

        private void ToggleMotorFromButton()
        {
            if (busy) { return; }

            bool installed = IsAutomaticEngineEnabled();
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
                    bool setupDeferred = result.ExitCode != 0 && !stopped && ShouldSuppressRunModal(result.Output);
                    string title = stopped ? "Otimizacao parada" : ((result.ExitCode == 0 || setupDeferred) ? "Otimizacao concluida" : "Action failed");
                    string detail = stopped ? "O passe manual foi interrompido." : (result.ExitCode == 0 ? BuildResultText() : (setupDeferred ? BuildDeferredRunDetail(result.Output) : ShortError(result.Output)));
                    if (actionTimer != null) { actionTimer.Stop(); }
                    activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + (stopped ? "  STOP passe manual interrompido" : ((result.ExitCode == 0 || setupDeferred) ? "  OK   passe manual aplicado: " + BuildResultText() : "  FAIL passe manual falhou"));
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
                    if (result.ExitCode != 0 && !stopped && !ShouldSuppressRunModal(result.Output))
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
            optimizeButton.Content = activeRunCanStop ? "Cancelar passe" : "Aplicar agora";
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
            return FriendlyUiError(output);
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
                    row.Path = GetMapString(map, "Path");
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
            string name = GetMapString(map, "ProcessName");
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

        private static List<string> ReadStringList(IDictionary<string, object> map, string key)
        {
            List<string> result = new List<string>();
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) { return result; }
            System.Collections.IEnumerable enumerable = value as System.Collections.IEnumerable;
            if (enumerable == null || value is string)
            {
                string single = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (!String.IsNullOrWhiteSpace(single)) { result.Add(single); }
                return result;
            }
            foreach (object item in enumerable)
            {
                string text = Convert.ToString(item, CultureInfo.InvariantCulture);
                if (!String.IsNullOrWhiteSpace(text)) { result.Add(text); }
            }
            return result;
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
            bool autoInstalled = IsAutomaticEngineEnabled();
            bool startupInstalled = IsStartupInstalled();
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
                    row.Path = GetMapString(map, "Path");
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
            string name = GetMapString(map, "ProcessName");
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

        private static List<string> ReadStringList(IDictionary<string, object> map, string key)
        {
            List<string> result = new List<string>();
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) { return result; }
            System.Collections.IEnumerable enumerable = value as System.Collections.IEnumerable;
            if (enumerable == null || value is string)
            {
                string single = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (!String.IsNullOrWhiteSpace(single)) { result.Add(single); }
                return result;
            }
            foreach (object item in enumerable)
            {
                string text = Convert.ToString(item, CultureInfo.InvariantCulture);
                if (!String.IsNullOrWhiteSpace(text)) { result.Add(text); }
            }
            return result;
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
            optimizeButton.Text = activeRunCanStop ? "Cancelar passe" : "Aplicar agora";
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

        private static List<GamePresetDefinition> GetGamePresetDefinitions()
        {
            return new List<GamePresetDefinition>
            {
                new GamePresetDefinition
                {
                    Id = "bf6",
                    Name = "Battlefield 6",
                    ShortName = "BF6",
                    Tier = "Competitive FPS",
                    Accent = "orange",
                    Description = "Biblioteca beta com ajustes de comunidade para cache/shader, frame pacing, CPU-bound e stutter em partidas grandes.",
                    ProcessNames = new[] { "bf6", "Battlefield6", "Battlefield" },
                    InstallKeywords = new[] { "Battlefield 6", "Battlefield6", "Battlefield" },
                    SafeOptions = new[] { "Pipeline de shader/cache: backup e reconstrução guiada após update", "Frame pacing competitivo: cap estável por Hz e anti-stutter", "Config de engine segura: streaming e orçamento de CPU mais leve", "DX12/driver cache hygiene: remover cache antigo sem apagar saves", "Overlay/download guard: EA, Steam, Discord e capturas em modo leve", "Preset CPU-bound: reduzir pós-processamento pesado sem mudar controles" },
                    ExperimentalOptions = new[] { "user.cfg avançado: thread budget e render queue em teste A/B", "Cache rebuild agressivo: DX/NVIDIA/AMD shader cache com aviso de stutter inicial", "Ultra low CPU fallback: streaming, efeitos e partículas em orçamento mínimo", "Overlay hard-off test: EA/Steam por config reversível com backup", "Frame cap lab: 90/120/144/165 com medição de pacing" }
                },
                new GamePresetDefinition
                {
                    Id = "eafc26",
                    Name = "EA SPORTS FC 26",
                    ShortName = "FC26",
                    Tier = "Sports online",
                    Accent = "cyan",
                    Description = "Receitas de comunidade para reduzir stutter, estabilizar frame pacing e controlar EA/Steam sem alterar gameplay.",
                    ProcessNames = new[] { "FC26", "FC25", "FC24" },
                    InstallKeywords = new[] { "EA SPORTS FC 26", "EA Sports FC 26", "FC26", "EA SPORTS FC" },
                    SafeOptions = new[] { "FC setup sanity: detectar arquivo de settings e fazer backup", "Stutter guard: cap de FPS por Hz e estabilidade de cutscenes", "EA/Steam overlay guard: reduzir overlays e downloads durante partida", "Shader/cache refresh guiado após update de driver ou patch", "CPU/GPU balance: crowd, hair e cloth em perfil de desempenho", "Fullscreen e Hz corretos sem mexer em câmera, controle ou gameplay" },
                    ExperimentalOptions = new[] { "FC microstutter lab: caps 60/90/120/144 com rollback", "Stadium heavy preset: reduzir crowd, cloth e hair para PC fraco", "EA overlay hard-off via user_*.ini com backup", "Cache rebuild agressivo em Documents/AppData quando há travadas", "Steam input/overlay isolation quando FC abre via Steam" }
                },
                new GamePresetDefinition
                {
                    Id = "cs2",
                    Name = "Counter-Strike 2",
                    ShortName = "CS2",
                    Tier = "Competitive shooter",
                    Accent = "blue",
                    Description = "Receitas competitivas de comunidade para launch options limpas, autoexec, shader cache, Reflex e frame pacing.",
                    ProcessNames = new[] { "cs2" },
                    InstallKeywords = new[] { "Counter-Strike Global Offensive", "Counter-Strike 2", "csgo", "cs2" },
                    SafeOptions = new[] { "Launch options auditor: remover comandos antigos ou prejudiciais", "Autoexec performance pack: telemetria, pacing e cvars seguras", "NVIDIA Reflex check: orientar ON quando suportado", "Shader prewarm/cache hygiene após update do jogo ou driver", "Frame cap estável por Hz para reduzir variação de frametime", "Steam overlay/download guard durante partida competitiva" },
                    ExperimentalOptions = new[] { "-vulkan A/B test com reversão automática", "fps_max lab: 0, refresh+buffer ou cap competitivo", "DX shader cache rebuild agressivo", "Low-end cfg: partículas, decals e streaming budget reduzidos", "Workshop/custom cfg quarantine para caçar stutter" }
                },
                new GamePresetDefinition
                {
                    Id = "valorant",
                    Name = "VALORANT",
                    ShortName = "VALORANT",
                    Tier = "Tactical FPS",
                    Accent = "violet",
                    Description = "Receitas seguras de comunidade para FPS alto, baixa latência, cache limpo e estabilidade sem tocar no Vanguard.",
                    ProcessNames = new[] { "VALORANT-Win64-Shipping", "VALORANT" },
                    InstallKeywords = new[] { "VALORANT", "Riot Games" },
                    SafeOptions = new[] { "Config backup Riot e validação de GameUserSettings", "Multithreaded Rendering check quando a CPU suporta", "NVIDIA Reflex/low latency check quando suportado", "FPS cap por menu/background para aliviar stutter térmico", "Fullscreen e Hz sanity sem tocar em sensibilidade ou mira", "Overlay/download guard sem tocar no Vanguard" },
                    ExperimentalOptions = new[] { "FPS cap lab por cenário: menu, background e in-game", "Low-end GPU profile: material, detail e UI em modo performance", "Cache/config reset guiado com backup", "Overlay hard isolation sem mexer no Vanguard", "Frame pacing stress test por monitor" }
                }
            };
        }

        private static List<WebGamePreset> BuildGamePresetsForUi()
        {
            List<WebGamePreset> output = new List<WebGamePreset>();
            List<Process> processes = new List<Process>();
            try { processes.AddRange(Process.GetProcesses()); } catch { }
            try
            {
                foreach (GamePresetDefinition definition in GetGamePresetDefinitions())
                {
                    Process running = null;
                    string runningPath = "";
                    foreach (Process process in processes)
                    {
                        string processName = "";
                        try { processName = process.ProcessName ?? ""; } catch { }
                        if (!NameInList(processName, definition.ProcessNames)) { continue; }
                        if (IsKnownLauncherProcessForReactive(processName)) { continue; }
                        running = process;
                        runningPath = TryGetProcessPath(process);
                        break;
                    }

                    string installedPath = !String.IsNullOrWhiteSpace(runningPath) ? runningPath : FindGameInstallPath(definition);
                    output.Add(new WebGamePreset
                    {
                        Id = definition.Id,
                        Name = definition.Name,
                        ShortName = definition.ShortName,
                        Tier = definition.Tier,
                        Genre = definition.Tier,
                        Accent = definition.Accent,
                        Summary = definition.Description,
                        Description = definition.Description,
                        ExpectedGain = definition.Tier,
                        CoverDataUrl = GetGameCoverDataUrl(definition.Id),
                        Installed = !String.IsNullOrWhiteSpace(installedPath),
                        Running = running != null,
                        ProcessName = running == null ? "" : running.ProcessName,
                        ProcessId = running == null ? 0 : running.Id,
                        Path = installedPath,
                        DetectedPath = installedPath,
                        Status = running != null ? "Running" : (!String.IsNullOrWhiteSpace(installedPath) ? "Installed" : "Not found"),
                        SafeOptions = new List<string>(definition.SafeOptions ?? new string[0]),
                        ExperimentalOptions = new List<string>(definition.ExperimentalOptions ?? new string[0]),
                        SafeOptimizations = new List<string>(definition.SafeOptions ?? new string[0]),
                        ExperimentalOptimizations = new List<string>(definition.ExperimentalOptions ?? new string[0])
                    });
                }
            }
            finally
            {
                foreach (Process process in processes) { try { process.Dispose(); } catch { } }
            }
            return output;
        }

        private static int RefreshGameDiscoveryCache()
        {
            int found = 0;
            foreach (GamePresetDefinition definition in GetGamePresetDefinitions())
            {
                try
                {
                    string path = FindGameInstallPath(definition);
                    if (String.IsNullOrWhiteSpace(path)) { continue; }
                    SaveManualGameInstallPath(definition, path);
                    found++;
                }
                catch { }
            }
            return found;
        }
        private static string FindGameInstallPath(GamePresetDefinition definition)
        {
            if (definition == null) { return ""; }
            string running = FindGameInstallPathFromRunningProcess(definition);
            if (!String.IsNullOrWhiteSpace(running)) { return running; }

            foreach (string candidate in BuildGameInstallCandidates(definition))
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(candidate)) { continue; }
                    string normalized = candidate.Trim().Trim('"');
                    if (File.Exists(normalized)) { return normalized; }
                    if (Directory.Exists(normalized)) { return normalized; }
                }
                catch { }
            }
            return "";
        }

        private static string FindGameInstallPathFromRunningProcess(GamePresetDefinition definition)
        {
            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    using (process)
                    {
                        string name = "";
                        try { name = process.ProcessName ?? ""; } catch { }
                        if (!NameInList(name, definition.ProcessNames)) { continue; }
                        if (IsKnownLauncherProcessForReactive(name)) { continue; }
                        string path = TryGetProcessPath(process);
                        if (!String.IsNullOrWhiteSpace(path)) { return path; }
                    }
                }
            }
            catch { }
            return "";
        }

        private static List<string> BuildGameInstallCandidates(GamePresetDefinition definition)
        {
            List<string> candidates = new List<string>();
            Action<string> add = delegate(string value)
            {
                if (!String.IsNullOrWhiteSpace(value) && !candidates.Contains(value, StringComparer.OrdinalIgnoreCase)) { candidates.Add(value); }
            };

            foreach (string root in BuildGameInstallRoots())
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) { continue; }
                    if (MatchesGamePath(root, definition)) { add(root); }
                    foreach (string dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (MatchesGamePath(dir, definition) || ContainsGameExecutable(dir, definition)) { add(dir); }
                    }
                }
                catch { }
            }

            AddShortcutGameCandidates(definition, add);
            return candidates;
        }

        private static bool MatchesGamePath(string value, GamePresetDefinition definition)
        {
            string text = value ?? "";
            foreach (string keyword in definition.InstallKeywords ?? new string[0])
            {
                if (!String.IsNullOrWhiteSpace(keyword) && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            }
            foreach (string process in definition.ProcessNames ?? new string[0])
            {
                if (!String.IsNullOrWhiteSpace(process) && text.IndexOf(process, StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            }
            return false;
        }

        private static IEnumerable<string> EnumerateFilesLimited(string root, string pattern, int maxDepth, int maxFiles)
        {
            if (String.IsNullOrWhiteSpace(root)) { yield break; }
            try { if (!Directory.Exists(root)) { yield break; } } catch { yield break; }
            Queue<Tuple<string, int>> queue = new Queue<Tuple<string, int>>();
            queue.Enqueue(Tuple.Create(root, 0));
            int emitted = 0;
            while (queue.Count > 0 && emitted < maxFiles)
            {
                Tuple<string, int> item = queue.Dequeue();
                string dir = item.Item1;
                int depth = item.Item2;
                string[] files = new string[0];
                try { files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly); } catch { }
                foreach (string file in files)
                {
                    yield return file;
                    emitted++;
                    if (emitted >= maxFiles) { yield break; }
                }
                if (depth >= maxDepth) { continue; }
                string[] dirs = new string[0];
                try { dirs = Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly); } catch { }
                foreach (string child in dirs) { queue.Enqueue(Tuple.Create(child, depth + 1)); }
            }
        }
        private static bool ContainsGameExecutable(string directory, GamePresetDefinition definition)
        {
            try
            {
                foreach (string process in definition.ProcessNames ?? new string[0])
                {
                    if (String.IsNullOrWhiteSpace(process)) { continue; }
                    string exe = process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? process : process + ".exe";
                    if (File.Exists(Path.Combine(directory, exe))) { return true; }
                    foreach (string file in EnumerateFilesLimited(directory, exe, 4, 700))
                    {
                        if (file.IndexOf("launcher", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static List<string> BuildGameInstallRoots()
        {
            List<string> roots = new List<string>();
            Action<string> add = delegate(string value)
            {
                if (!String.IsNullOrWhiteSpace(value) && !roots.Contains(value, StringComparer.OrdinalIgnoreCase)) { roots.Add(value); }
            };
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            add(Path.Combine(pfx86, "Steam", "steamapps", "common"));
            add(Path.Combine(pf, "Steam", "steamapps", "common"));
            AddSteamLibraryRoots(add);
            add(Path.Combine(pf, "EA Games"));
            add(Path.Combine(pfx86, "EA Games"));
            add(Path.Combine(pf, "Electronic Arts", "Games"));
            add(Path.Combine(pfx86, "Origin Games"));
            add(Path.Combine(pf, "Epic Games"));
            AddEpicInstallRoots(add);
            add(Path.Combine(pf, "Riot Games"));
            add(Path.Combine(pfx86, "Riot Games"));
            AddRiotInstallRoots(add);
            add(Path.Combine(pd, "Battle.net"));
            return roots;
        }

        private static void AddSteamLibraryRoots(Action<string> add)
        {
            foreach (string steamRoot in GetSteamRoots())
            {
                string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                try
                {
                    if (File.Exists(vdf))
                    {
                        string text = File.ReadAllText(vdf);
                        foreach (Match match in Regex.Matches(text, "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\""))
                        {
                            string path = Regex.Unescape(match.Groups[1].Value.Replace("\\\\", "\\"));
                            add(Path.Combine(path, "steamapps", "common"));
                        }
                    }
                }
                catch { }
                add(Path.Combine(steamRoot, "steamapps", "common"));
            }
        }

        private static IEnumerable<string> GetSteamRoots()
        {
            List<string> roots = new List<string>();
            Action<string> add = delegate(string value)
            {
                if (!String.IsNullOrWhiteSpace(value) && !roots.Contains(value, StringComparer.OrdinalIgnoreCase)) { roots.Add(value); }
            };
            try { using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) { add(Convert.ToString(key == null ? null : key.GetValue("SteamPath"))); } } catch { }
            try { using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")) { add(Convert.ToString(key == null ? null : key.GetValue("InstallPath"))); } } catch { }
            add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
            return roots;
        }

        private static void AddEpicInstallRoots(Action<string> add)
        {
            string manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
            try
            {
                if (!Directory.Exists(manifests)) { return; }
                foreach (string file in Directory.EnumerateFiles(manifests, "*.item", SearchOption.TopDirectoryOnly))
                {
                    string text = File.ReadAllText(file);
                    Match m = Regex.Match(text, "\\\"InstallLocation\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
                    if (m.Success) { add(Regex.Unescape(m.Groups[1].Value)); }
                }
            }
            catch { }
        }

        private static void AddRiotInstallRoots(Action<string> add)
        {
            string json = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "RiotClientInstalls.json");
            try
            {
                if (File.Exists(json))
                {
                    foreach (Match match in Regex.Matches(File.ReadAllText(json), "[A-Za-z]:\\\\(?:[^\\\"\\r\\n])+"))
                    {
                        string path = match.Value.Replace("\\\\", "\\");
                        string dir = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                        if (!String.IsNullOrWhiteSpace(dir)) { add(dir); }
                    }
                }
            }
            catch { }
        }

        private static void AddShortcutGameCandidates(GamePresetDefinition definition, Action<string> add)
        {
            foreach (string root in GetShortcutSearchRoots())
            {
                try
                {
                    if (!Directory.Exists(root)) { continue; }
                    foreach (string shortcut in EnumerateFilesLimited(root, "*.lnk", 3, 600))
                    {
                        if (!MatchesGamePath(shortcut, definition)) { continue; }
                        string target = TryGetShortcutTarget(shortcut);
                        if (String.IsNullOrWhiteSpace(target)) { continue; }
                        if (File.Exists(target)) { add(target); add(Path.GetDirectoryName(target)); }
                        else if (Directory.Exists(target)) { add(target); }
                    }
                }
                catch { }
            }
        }

        private static IEnumerable<string> GetShortcutSearchRoots()
        {
            yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "Start Menu", "Programs");
        }

        private static string TryGetShortcutTarget(string shortcutPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) { return ""; }
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                string target = Convert.ToString(shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null));
                try { Marshal.FinalReleaseComObject(shortcut); } catch { }
                try { Marshal.FinalReleaseComObject(shell); } catch { }
                return target ?? "";
            }
            catch { return ""; }
        }

        private RunResult ApplyGamePresetFromMessage(IDictionary<string, object> message)
        {
            string gameId = GetMapString(message, "gameId");
            GamePresetDefinition definition = GetGamePresetDefinitions().Find(delegate(GamePresetDefinition item) { return String.Equals(item.Id, gameId, StringComparison.OrdinalIgnoreCase); });
            if (definition == null) { return new RunResult(1, "Preset de jogo desconhecido."); }

            List<string> selectedSafeOptions = GetMapStringList(message, "safeOptions");
            List<string> selectedExperimentalOptions = GetMapStringList(message, "experimentalOptions");
            if (selectedSafeOptions.Count == 0) { selectedSafeOptions = new List<string>(definition.SafeOptions ?? new string[0]); }
            bool experimental = selectedExperimentalOptions.Count > 0 || GetBool(message, "experimental");
            int backupFiles = EnsureGamePresetFileBackups(definition);

            SaveGamePresetState(definition, experimental, selectedSafeOptions, selectedExperimentalOptions, backupFiles);
            AppendOperationalLog("action=game-preset game=" + definition.ShortName.Replace(' ', '_') + " safe=" + selectedSafeOptions.Count.ToString(CultureInfo.InvariantCulture) + " experimental=" + selectedExperimentalOptions.Count.ToString(CultureInfo.InvariantCulture) + " backups=" + backupFiles.ToString(CultureInfo.InvariantCulture) + " session=unchanged");
            return new RunResult(0, "Preset de jogo salvo: " + definition.Name + ". O modo atual do motor foi mantido.");
        }

        private void SaveGamePolicy(string processName, string policy)
        {
            Dictionary<string, object> message = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            message["policy"] = policy;
            message["processName"] = processName;
            message["key"] = "name:" + (processName ?? "").Trim().ToLowerInvariant();
            message["path"] = "";
            SetAppPolicyFromMessage(message);
        }

        private static void SaveGamePresetState(GamePresetDefinition definition, bool experimental, List<string> safeOptions, List<string> experimentalOptions, int backupFiles)
        {
            Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            root["Timestamp"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            root["LastGameId"] = definition.Id;
            root["LastGameName"] = definition.Name;
            root["Experimental"] = experimental;
            root["SafeOptions"] = safeOptions == null || safeOptions.Count == 0 ? new List<string>(definition.SafeOptions ?? new string[0]) : new List<string>(safeOptions);
            root["ExperimentalOptions"] = experimentalOptions == null ? new List<string>() : new List<string>(experimentalOptions);
            root["BackupFiles"] = backupFiles;
            root["Restored"] = false;
            AtomicWriteJsonMap(Path.Combine(outputsPath, "game-presets.state.json"), root);
        }

        private static string GetGamePresetBackupRoot()
        {
            return Path.Combine(outputsPath, "game-preset-backups");
        }

        private static string HashGamePresetTarget(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes((value ?? "").Trim().ToLowerInvariant()));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < bytes.Length && i < 16; i++) { sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture)); }
                return sb.ToString();
            }
        }

        private static void AddExistingFile(List<string> files, string path)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(path) && File.Exists(path) && !files.Contains(path, StringComparer.OrdinalIgnoreCase)) { files.Add(path); }
            }
            catch { }
        }

        private static List<string> BuildGamePresetBackupCandidates(GamePresetDefinition definition)
        {
            List<string> files = new List<string>();
            string id = (definition == null ? "" : definition.Id ?? "").Trim().ToLowerInvariant();
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string install = definition == null ? "" : FindGameInstallPath(definition);

            if (id == "bf6")
            {
                foreach (string folder in new[] { "Battlefield 6", "Battlefield6", "Battlefield 2042" })
                {
                    string settings = Path.Combine(docs, folder, "settings");
                    AddExistingFile(files, Path.Combine(settings, "PROFSAVE_profile"));
                    AddExistingFile(files, Path.Combine(settings, "PROFSAVE"));
                    AddExistingFile(files, Path.Combine(settings, "PROFSAVE_tmp"));
                }
                AddExistingFile(files, Path.Combine(install, "user.cfg"));
            }
            else if (id == "eafc26")
            {
                foreach (string folder in new[] { "FC 26", "EA SPORTS FC 26", "FC 25", "EA SPORTS FC 25", "FC 24", "EA SPORTS FC 24" })
                {
                    string root = Path.Combine(docs, folder);
                    AddExistingFile(files, Path.Combine(root, "fcsetup.ini"));
                    AddExistingFile(files, Path.Combine(root, "fifasetup.ini"));
                    AddExistingFile(files, Path.Combine(root, "settings.ini"));
                    AddExistingFile(files, Path.Combine(root, "buttonDataSetup.ini"));
                }
            }
            else if (id == "cs2")
            {
                foreach (string root in BuildGameInstallRoots())
                {
                    string steam = root;
                    if (steam.EndsWith(Path.Combine("steamapps", "common"), StringComparison.OrdinalIgnoreCase))
                    {
                        DirectoryInfo steamApps = Directory.GetParent(steam);
                        DirectoryInfo steamRoot = steamApps == null ? null : steamApps.Parent;
                        string userData = steamRoot == null ? "" : Path.Combine(steamRoot.FullName, "userdata");
                        try
                        {
                            if (Directory.Exists(userData))
                            {
                                foreach (string cfg in Directory.EnumerateFiles(userData, "*.cfg", SearchOption.AllDirectories))
                                {
                                    if (cfg.IndexOf(Path.Combine("730", "local", "cfg"), StringComparison.OrdinalIgnoreCase) >= 0) { AddExistingFile(files, cfg); }
                                }
                                foreach (string txt in Directory.EnumerateFiles(userData, "*.txt", SearchOption.AllDirectories))
                                {
                                    if (txt.IndexOf(Path.Combine("730", "local", "cfg"), StringComparison.OrdinalIgnoreCase) >= 0) { AddExistingFile(files, txt); }
                                }
                            }
                        }
                        catch { }
                    }
                }
                AddExistingFile(files, Path.Combine(install, "game", "csgo", "cfg", "autoexec.cfg"));
            }
            else if (id == "valorant")
            {
                string configRoot = Path.Combine(local, "VALORANT", "Saved", "Config");
                try
                {
                    if (Directory.Exists(configRoot))
                    {
                        foreach (string file in Directory.EnumerateFiles(configRoot, "GameUserSettings.ini", SearchOption.AllDirectories)) { AddExistingFile(files, file); }
                    }
                }
                catch { }
            }

            return files;
        }

        private static int EnsureGamePresetFileBackups(GamePresetDefinition definition)
        {
            List<string> files = BuildGamePresetBackupCandidates(definition);
            int ready = 0;
            foreach (string target in files)
            {
                try
                {
                    string id = (definition == null ? "unknown" : definition.Id ?? "unknown").Trim().ToLowerInvariant();
                    string itemDir = Path.Combine(GetGamePresetBackupRoot(), id, HashGamePresetTarget(target));
                    string backupPath = Path.Combine(itemDir, "original.bin");
                    string targetPath = Path.Combine(itemDir, "target.txt");
                    Directory.CreateDirectory(itemDir);
                    if (!File.Exists(backupPath)) { File.Copy(target, backupPath, false); }
                    if (!File.Exists(targetPath)) { AtomicWriteAllText(targetPath, target, Encoding.UTF8); }
                    ready++;
                }
                catch { }
            }
            return ready;
        }

        private static int RestoreGamePresetFileBackups(string gameId)
        {
            string root = GetGamePresetBackupRoot();
            if (!Directory.Exists(root)) { return 0; }
            int restored = 0;
            string normalized = (gameId ?? "").Trim().ToLowerInvariant();
            try
            {
                foreach (string gameDir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                {
                    string currentId = Path.GetFileName(gameDir) ?? "";
                    if (!String.IsNullOrWhiteSpace(normalized) && !String.Equals(currentId, normalized, StringComparison.OrdinalIgnoreCase)) { continue; }
                    foreach (string itemDir in Directory.EnumerateDirectories(gameDir, "*", SearchOption.TopDirectoryOnly))
                    {
                        string backupPath = Path.Combine(itemDir, "original.bin");
                        string targetPathFile = Path.Combine(itemDir, "target.txt");
                        if (!File.Exists(backupPath) || !File.Exists(targetPathFile)) { continue; }
                        string target = File.ReadAllText(targetPathFile, Encoding.UTF8).Trim();
                        if (String.IsNullOrWhiteSpace(target)) { continue; }
                        string dir = Path.GetDirectoryName(target);
                        if (!String.IsNullOrWhiteSpace(dir)) { Directory.CreateDirectory(dir); }
                        File.Copy(backupPath, target, true);
                        restored++;
                    }
                }
            }
            catch { }
            return restored;
        }

        private RunResult RestoreGamePresetFromMessage(IDictionary<string, object> message)
        {
            string gameId = GetMapString(message, "gameId");
            if (!String.IsNullOrWhiteSpace(gameId))
            {
                GamePresetDefinition selected = GetGamePresetDefinitions().Find(delegate(GamePresetDefinition item) { return String.Equals(item.Id, gameId, StringComparison.OrdinalIgnoreCase); });
                if (selected == null) { return new RunResult(1, "Preset de jogo desconhecido."); }
            }

            int restored = RestoreGamePresetFileBackups(gameId);
            SaveGamePresetRestoreState(String.IsNullOrWhiteSpace(gameId) ? "all" : gameId, restored);
            AppendOperationalLog("action=game-preset-restore target=" + (String.IsNullOrWhiteSpace(gameId) ? "all" : gameId) + " files=" + restored.ToString(CultureInfo.InvariantCulture) + " session=unchanged");
            if (restored <= 0) { return new RunResult(0, "Nenhum arquivo alterado pela aba Jogos para restaurar."); }
            return new RunResult(0, "Arquivos do preset restaurados: " + restored.ToString(CultureInfo.InvariantCulture) + ".");
        }

        private static void SaveGamePresetRestoreState(string target, int files)
        {
            Dictionary<string, object> root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            root["Timestamp"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            root["LastGameId"] = target;
            root["LastGameName"] = target;
            root["Experimental"] = false;
            root["SafeOptions"] = new List<string>();
            root["ExperimentalOptions"] = new List<string>();
            root["Restored"] = true;
            root["RestoredFiles"] = files;
            AtomicWriteJsonMap(Path.Combine(outputsPath, "game-presets.state.json"), root);
        }

        private static bool ShouldShowGameBetaWelcome()
        {
            return !ReadUiFlag("GameBetaWelcomeSeen");
        }

        private static void MarkGameBetaWelcomeSeen()
        {
            SaveUiFlag("GameBetaWelcomeSeen", true);
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
                    if (result.ExitCode != 0 && !ShouldSuppressRunModal(result.Output))
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

            bool installed = IsAutomaticEngineEnabled();
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
                    bool setupDeferred = result.ExitCode != 0 && !stopped && ShouldSuppressRunModal(result.Output);
                    string title = stopped ? "Otimizacao parada" : ((result.ExitCode == 0 || setupDeferred) ? "Otimizacao concluida" : "Action failed");
                    string detail = stopped ? "O passe manual foi interrompido." : (result.ExitCode == 0 ? BuildResultText() : (setupDeferred ? BuildDeferredRunDetail(result.Output) : ShortError(result.Output)));
                    activeRunControl = null;
                    busy = false;
                    RefreshStatus();
                    SetBusyState(false, title, detail);
                    RefreshLiveManager();
                    if (result.ExitCode != 0 && !stopped && !ShouldSuppressRunModal(result.Output))
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

            actionTitle.Text = "Parando otimizacao...";
            actionDetail.Text = "Encerrando o passe manual com seguranca.";
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
                    bool setupDeferred = result.ExitCode != 0 && !stopped && ShouldSuppressRunModal(result.Output);
                    string title = stopped ? "Otimizacao parada" : ((result.ExitCode == 0 || setupDeferred) ? "Otimizacao concluida" : "Action failed");
                    string detail = stopped ? "O passe manual foi interrompido." : (result.ExitCode == 0 ? BuildResultText() : (setupDeferred ? BuildDeferredRunDetail(result.Output) : ShortError(result.Output)));
                    if (actionTimer != null) { actionTimer.Stop(); }
                    activeUiEventLine = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + (stopped ? "  STOP passe manual interrompido" : ((result.ExitCode == 0 || setupDeferred) ? "  OK   passe manual aplicado: " + BuildResultText() : "  FAIL passe manual falhou"));
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
                    if (result.ExitCode != 0 && !stopped && !ShouldSuppressRunModal(result.Output))
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
            return FriendlyUiError(output);
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
                row.Path = GetMapString(map, "Path");
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
            string name = GetMapString(map, "ProcessName");
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

    #endif

    private sealed class SmartSnapCoreService : ServiceBase
    {
        private readonly ManualResetEvent stopSignal = new ManualResetEvent(false);
        private Thread worker;
        private Thread pipeWorker;

        public SmartSnapCoreService()
        {
            ServiceName = CoreServiceName;
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            TryRequestExtraTime(10000);
            WriteCoreServiceState("Starting", "Start", new RunResult(0, "Starting."), IsTaskInstalled(AutoTaskName), false, GetFileAgeSeconds(scorePath), CoreServiceStalePassSeconds);
            worker = new Thread(WorkerLoop);
            worker.IsBackground = true;
            worker.Name = "Smart SNAP Core Service";
            worker.Start();
            pipeWorker = new Thread(PipeLoop);
            pipeWorker.IsBackground = true;
            pipeWorker.Name = "Smart SNAP Core Pipe";
            pipeWorker.Start();
        }

        protected override void OnStop()
        {
            TryRequestExtraTime(10000);
            stopSignal.Set();
            if (worker != null && worker.IsAlive)
            {
                try { worker.Join(TimeSpan.FromSeconds(8)); } catch { }
            }
            if (pipeWorker != null && pipeWorker.IsAlive)
            {
                try { pipeWorker.Join(TimeSpan.FromSeconds(8)); } catch { }
            }
            WriteCoreServiceState("Stopped", "Stop", new RunResult(0, "Stopped."), IsTaskInstalled(AutoTaskName), false, GetFileAgeSeconds(scorePath), CoreServiceStalePassSeconds);
        }

        public void RunConsole(string[] args)
        {
            OnStart(args);
            try
            {
                Console.WriteLine("Smart SNAP Core Service console mode. Press Enter to stop.");
                Console.ReadLine();
            }
            catch
            {
                try { Thread.Sleep(Timeout.Infinite); } catch { }
            }
            finally
            {
                OnStop();
            }
        }

        private void WorkerLoop()
        {
            AppendOperationalLog("action=core-service event=started");
            try
            {
                RunCoreServicePass("service-start");
            }
            catch (Exception ex)
            {
                AppendOperationalLog("action=core-service status=failed detail=" + ShortTaskError(ex.Message));
                WriteCoreServiceState("Error", "ServiceStart", new RunResult(1, ex.Message), IsTaskInstalled(AutoTaskName), false, GetFileAgeSeconds(scorePath), CoreServiceStalePassSeconds);
            }

            while (!stopSignal.WaitOne(TimeSpan.FromSeconds(CoreServiceLoopSeconds)))
            {
                try
                {
                    RunCoreServicePass("watchdog");
                }
                catch (Exception ex)
                {
                    AppendOperationalLog("action=core-service status=failed detail=" + ShortTaskError(ex.Message));
                    WriteCoreServiceState("Error", "Watchdog", new RunResult(1, ex.Message), IsTaskInstalled(AutoTaskName), false, GetFileAgeSeconds(scorePath), CoreServiceStalePassSeconds);
                }
            }

            AppendOperationalLog("action=core-service event=stopped");
        }

        private void PipeLoop()
        {
            RunCorePipeServerLoop(stopSignal);
        }

        private void TryRequestExtraTime(int milliseconds)
        {
            try { RequestAdditionalTime(milliseconds); } catch { }
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

    private sealed class PreviewSummary
    {
        public int Targets { get; set; }
        public int WouldTrim { get; set; }
        public string TopApp { get; set; }
        public string TimestampText { get; set; }
        public string ShortText { get; set; }
        public string Detail { get; set; }
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





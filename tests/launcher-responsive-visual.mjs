import { spawn } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const dashboardPath = process.argv[2] ? path.resolve(process.argv[2]) : path.join(repoRoot, "src", "launcher", "dashboard.html");
const chromePath = process.env.CHROME_PATH || "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const outputRoot = process.env.SMARTNAP_RESPONSIVE_OUT || (existsSync("D:\\") ? "D:\\CodexBuildData\\SmartBackgroundNap\\responsive-screenshots" : path.join(repoRoot, "build", "responsive-screenshots"));

if (!existsSync(dashboardPath)) {
  throw new Error(`dashboard.html not found: ${dashboardPath}`);
}
if (!existsSync(chromePath)) {
  throw new Error(`Chrome not found. Set CHROME_PATH. Tried: ${chromePath}`);
}
mkdirSync(outputRoot, { recursive: true });
const logoPath = path.join(repoRoot, "assets", "smart-nap-logo-v2.png");
const logoDataUrl = existsSync(logoPath) ? "data:image/png;base64," + readFileSync(logoPath).toString("base64") : "";

const scenarios = [
  { name: "dashboard-1280x720", width: 1280, height: 720, dpr: 1, view: "dashboard" },
  { name: "dashboard-1366x768", width: 1366, height: 768, dpr: 1, view: "dashboard" },
  { name: "dashboard-1366x768-dpi125", width: 1366, height: 768, dpr: 1.25, view: "dashboard" },
  { name: "dashboard-timeline-1366x768", width: 1366, height: 768, dpr: 1, view: "dashboard", scrollTop: 2700 },
  { name: "dashboard-1600x900", width: 1600, height: 900, dpr: 1, view: "dashboard" },
  { name: "dashboard-1920x1080", width: 1920, height: 1080, dpr: 1, view: "dashboard" },
  {
    name: "dashboard-activity-resources-idle-1920x1080",
    width: 1920,
    height: 1080,
    dpr: 1,
    view: "dashboard",
    statePatch: {
      IntentKind: "Desktop",
      SessionMode: "Auto",
      Rows: [],
      Managed: 0,
      NetworkUdpGuard: false,
      NetworkUdpGuardActive: false,
      NetworkUdpGuardGame: "",
      NetworkUdpGuardProcessCount: 0,
      NetworkUdpGuardEndpoints: 0,
      ShaderBoostEnabled: false,
      ShaderBoostActive: false,
      ShaderBoostGameName: "",
      CpuBoundAssistActive: false,
      CpuBoundAssistGame: "",
      CpuBoundAssistGamePid: 0,
      CpuBoundAssistConfidence: 0,
      StreamGuardActive: false,
      StreamGuardProfile: "Off",
      StreamGuardAppCount: 0,
      StreamGuardHelperCount: 0,
      StreamGameProtectedCount: 0
    }
  },
  {
    name: "dashboard-activity-resources-game-no-live-1920x1080",
    width: 1920,
    height: 1080,
    dpr: 1,
    view: "dashboard",
    statePatch: {
      IntentKind: "Gaming",
      SessionMode: "Gaming",
      Rows: [
        row("Battlefield 6 Open Beta Long Process Name.exe", "bf6.exe", "C:\\Games\\Battlefield 6 Open Beta\\bf6.exe", "185", "812 MB", "22%", "76", "Protect", true),
        row("Discord Voice Chat", "Discord.exe", "C:\\Users\\eduar\\AppData\\Local\\Discord\\Discord.exe", "72", "120 MB", "4%", "8", "Light")
      ],
      StreamGuardActive: false,
      StreamGuardProfile: "Off",
      StreamGuardAppCount: 0,
      StreamGuardHelperCount: 0,
      StreamGameProtectedCount: 0
    }
  },
  {
    name: "dashboard-activity-resources-live-only-1920x1080",
    width: 1920,
    height: 1080,
    dpr: 1,
    view: "dashboard",
    statePatch: {
      IntentKind: "Streaming",
      SessionMode: "Streamer",
      Rows: [
        row("OBS Studio Encoder Session", "obs64.exe", "C:\\Program Files\\obs-studio\\bin\\64bit\\obs64.exe", "112", "340 MB", "11%", "28", "Protect", false, true)
      ],
      NetworkUdpGuard: false,
      NetworkUdpGuardActive: false,
      NetworkUdpGuardGame: "",
      NetworkUdpGuardProcessCount: 0,
      NetworkUdpGuardEndpoints: 0,
      ShaderBoostEnabled: false,
      ShaderBoostActive: false,
      ShaderBoostGameName: "",
      CpuBoundAssistActive: false,
      CpuBoundAssistGame: "",
      CpuBoundAssistGamePid: 0,
      CpuBoundAssistConfidence: 0,
      StreamGuardActive: true,
      StreamGuardProfile: "Live",
      StreamGuardAppCount: 1,
      StreamGuardHelperCount: 1,
      StreamGameProtectedCount: 0
    }
  },
  { name: "dashboard-activity-memory-expanded-1920x1080", width: 1920, height: 1080, dpr: 1, view: "dashboard", activityCard: "memory" },
  { name: "dashboard-activity-profile-expanded-1920x1080", width: 1920, height: 1080, dpr: 1, view: "dashboard", activityCard: "profile" },
  { name: "dashboard-activity-pass-expanded-1920x1080", width: 1920, height: 1080, dpr: 1, view: "dashboard", activityCard: "adjustment" },
  { name: "dashboard-activity-pressure-expanded-1920x1080", width: 1920, height: 1080, dpr: 1, view: "dashboard", activityCard: "pressure" },
  { name: "dashboard-activity-gpu-expanded-1920x1080", width: 1920, height: 1080, dpr: 1, view: "dashboard", activityCard: "gpu" },
  { name: "dashboard-activity-zero-expanded-1920x1080", width: 1920, height: 1080, dpr: 1, view: "dashboard", activityCard: "zero" },
  { name: "dashboard-activity-shader-expanded-1920x1080", width: 1920, height: 1080, dpr: 1, view: "dashboard", activityCard: "shader" },
  { name: "dashboard-activity-context-expanded-1920x1080", width: 1920, height: 1080, dpr: 1, view: "dashboard", activityCard: "context" },
  { name: "dashboard-mode-1920x1080", width: 1920, height: 1080, dpr: 1, view: "dashboard", scrollTop: 900 },
  { name: "dashboard-2560x1440", width: 2560, height: 1440, dpr: 1, view: "dashboard" },
  { name: "dashboard-3440x1440", width: 3440, height: 1440, dpr: 1, view: "dashboard" },
  { name: "dashboard-900x1440-dpi150", width: 900, height: 1440, dpr: 1.5, view: "dashboard" },
  { name: "dashboard-1080x1920", width: 1080, height: 1920, dpr: 1, view: "dashboard" },
  { name: "dashboard-mode-1080x1920", width: 1080, height: 1920, dpr: 1, view: "dashboard", scrollTop: 1480 },
  { name: "dashboard-1200x1920-dpi175", width: 1200, height: 1920, dpr: 1.75, view: "dashboard" },
  { name: "dashboard-1440x2560", width: 1440, height: 2560, dpr: 1, view: "dashboard" },
  { name: "dashboard-768x1024-dpi200", width: 768, height: 1024, dpr: 2, view: "dashboard" },
  { name: "games-1080x1920", width: 1080, height: 1920, dpr: 1, view: "games" },
  { name: "games-768x1024", width: 768, height: 1024, dpr: 1, view: "games" },
  { name: "games-scrolled-1366x768", width: 1366, height: 768, dpr: 1, view: "games", scrollTop: 96 },
  { name: "game-preset-modal-768x1024", width: 768, height: 1024, dpr: 1, view: "games", modal: "gamePreset" },
  { name: "game-preset-modal-tech-1920x1080", width: 1920, height: 1080, dpr: 1, view: "games", modal: "gamePreset", technicalDetails: true },
  { name: "game-preset-modal-tech-1366x768", width: 1366, height: 768, dpr: 1, view: "games", modal: "gamePreset", technicalDetails: true },
  { name: "game-preset-modal-tech-1080x1920", width: 1080, height: 1920, dpr: 1, view: "games", modal: "gamePreset", technicalDetails: true },
  { name: "game-preset-modal-tech-768x1366", width: 768, height: 1366, dpr: 1, view: "games", modal: "gamePreset", technicalDetails: true },
  { name: "game-preset-modal-900x1440-dpi150", width: 900, height: 1440, dpr: 1.5, view: "games", modal: "gamePreset" },
  { name: "game-preset-modal-applied-768x1366", width: 768, height: 1366, dpr: 1, view: "games", modal: "gamePreset", appliedPreset: true },
  { name: "game-preset-modal-1440x2560", width: 1440, height: 2560, dpr: 1, view: "games", modal: "gamePreset" },
  { name: "energy-mode-popup-1366x768", width: 1366, height: 768, dpr: 1, view: "dashboard", modal: "energyPrompt" },
  { name: "update-modal-1366x768", width: 1366, height: 768, dpr: 1, view: "dashboard", modal: "updateOverlay" }
];

const mockState = {
  Language: "pt-BR",
  AppVersion: "0.7.0",
  Creator: "KaozyKing",
  Logo: logoDataUrl,
  Title: "Controle em tempo real",
  Detail: "Motor ativo com jogo detectado, Zero Ping pronto e ShaderBoost monitorando cache grafico.",
  RunState: "MOTOR ACTIVE",
  AutoMode: true,
  Busy: false,
  CanStop: false,
  Startup: true,
  Managed: 12,
  Rows: [
    row("Battlefield 6 Open Beta Long Process Name.exe", "bf6.exe", "C:\\Games\\Battlefield 6 Open Beta\\Battlefield 6 Open Beta Long Folder\\bf6.exe", "185", "812 MB", "22%", "76", "Protect", true),
    row("OBS Studio Encoder Session", "obs64.exe", "C:\\Program Files\\obs-studio\\bin\\64bit\\obs64.exe", "112", "340 MB", "11%", "28", "Protect", false, true),
    row("Discord Voice Chat With Long Server Name", "Discord.exe", "C:\\Users\\eduar\\AppData\\Local\\Discord\\app\\Discord.exe", "72", "120 MB", "4%", "8", "Light"),
    row("Chrome Background Tabs x18", "chrome.exe", "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", "96", "640 MB", "8%", "13", "Balanced"),
    row("Steam Client WebHelper x9", "steamwebhelper.exe", "C:\\Program Files (x86)\\Steam\\bin\\cef\\steamwebhelper.exe", "88", "430 MB", "5%", "11", "Balanced"),
    row("EA App Launcher", "EADesktop.exe", "C:\\Program Files\\Electronic Arts\\EA Desktop\\EA Desktop\\EADesktop.exe", "64", "180 MB", "3%", "5", "Protect"),
    row("Very Long Background Utility Name Used To Test Wrapping", "long-helper.exe", "D:\\Tools\\Very Long Folder Name With Spaces\\long-helper.exe", "44", "80 MB", "2%", "2", "Deep")
  ],
  Events: [
    "13:51:25 action=reactive-boost phase=process-protection process=Battlefield_6_Open_Beta_With_A_Very_Long_Process_Name.exe trigger=foreground-game-detected status=OK detail=long-event-used-by-responsive-guard-to-ensure-activity-cards-grow-instead-of-clipping-text",
    "LIVE event now next 1s",
    "APPLY 12 apps 812 MB L/B/D 3/6/3 top bf6.exe INTENT Gaming 94",
    "OK Zero Ping is on",
    "action=session-mode mode=Competitive energy=keep status=OK"
  ],
  GamePresets: [
    game("longgame1", "Battlefield 6 Open Beta With A Very Long Edition Name", "FPS competitivo com foco em estabilidade de frametime, UDP e ShaderBoost.", true, true),
    game("cyber", "Cyberpunk 2077 Ultimate Ray Tracing Path Tracing Edition", "Perfil pesado para jogos DX12 com muitos assets e cache grafico.", true, false),
    game("valorant", "VALORANT", "Protecao para jogo competitivo com anti-cheat e prioridade de resposta.", true, true),
    game("cs2", "Counter-Strike 2", "Preset competitivo generico para FPS e netcode UDP.", true, false),
    game("elden", "Elden Ring Nightreign", "Perfil para jogo pesado com estabilidade de CPU e I/O.", false, false),
    game("minecraft", "Minecraft Java With Modpack Extremely Long Name", "Perfil de Java, mods e carga de disco.", true, false)
  ],
  NetworkUdpGuard: true,
  NetworkUdpGuardActive: true,
  NetworkUdpGuardGame: "Battlefield 6 Open Beta With A Very Long Edition Name",
  NetworkUdpGuardEndpoints: 12,
  NetworkUdpGuardProcessCount: 12,
  NetworkUdpGuardQosStatus: "Ready",
  NetworkUdpGuardMode: "Armed",
  NetworkUdpGuardNoStackTweaks: true,
  ShaderBoostEnabled: true,
  ShaderBoostState: "Cache saudavel",
  ShaderBoostGameName: "Battlefield 6 Open Beta With A Very Long Edition Name",
  ShaderBoostReadiness: 85,
  ShaderBoostGpu: "NVIDIA GeForce RTX 4070 SUPER",
  ShaderBoostDriverVersion: "32.0.15.9579",
  ShaderBoostCacheLocatedCount: 3,
  ShaderBoostCacheTotalSizeMB: 2500,
  ShaderBoostCacheManager: "NvidiaShaderAdapter, WindowsManaged",
  ShaderBoostPreparationMethod: "MonitoringOnly",
  ShaderBoostSharedState: "Waiting",
  ShaderBoostObserveOnly: true,
  ShaderBoostRecommendation: "Open a game or run a pass to analyze shader cache state",
  GpuPressureAvailable: true,
  GpuPressureLevel: "Normal",
  GpuTotalUtilPercent: 17,
  GpuAdapterDedicatedUsageMB: 2100,
  GpuAdapterLocalUsageMB: 2100,
  GpuAdapterLocalBudgetMB: 8192,
  GpuAdapterLocalAvailableMB: 4096,
  GpuAdapterNonLocalUsageMB: 724,
  GpuAdapterNonLocalBudgetMB: 16384,
  GpuPressureProvider: "DXGI Video memory budget + Windows GPU counters",
  GpuPressureDxgiAvailable: true,
  GpuPressureAdapterName: "NVIDIA GeForce RTX 4070 SUPER",
  GpuTopProcess: "msedgewebview2",
  GpuTopProcessPid: 18560,
  GpuTopProcessPercent: 8,
  GpuTopProcessDedicatedMB: 183,
  HardwareCpu: "AMD Ryzen 7 7800X3D",
  HardwareCpuDetail: "8 cores / 16 threads",
  HardwareRam: "32 GB",
  HardwareRamTotalMB: 32768,
  HardwareGpu: "NVIDIA GeForce RTX 4070 SUPER",
  HardwareGpuDetail: "driver NVIDIA 560.xx",
  HardwareOs: "Windows 11 Pro",
  FreeMemoryMB: 18432,
  MemoryPressure: "Low",
  BehaviorProfiles: 7,
  LearningProfiles: 7,
  Learning: true,
  SessionMode: "Competitive",
  PowerPlanName: "Smart Nap MODO JOGO",
  PowerPlanGuid: "7a6f2f9d-88d3-4abf-8b5f-3f8f2f477501",
  RecommendedPowerPlanName: "Smart Nap MODO JOGO",
  RecommendedPowerPlanGuid: "7a6f2f9d-88d3-4abf-8b5f-3f8f2f477501",
  GamePowerPlanName: "Smart Nap MODO JOGO",
  GamePowerPlanGuid: "7a6f2f9d-88d3-4abf-8b5f-3f8f2f477501",
  LivePowerPlanName: "Smart Nap MODO LIVE",
  LivePowerPlanGuid: "4f27e32a-369a-4c37-8a76-a6f79a3d86fa",
  PolicyCount: 2,
  ManualPolicyCount: 2,
  IntentKind: "Gaming",
  IntentConfidence: 94,
  RadarCount: 7,
  RadarTop: "Discord (7432)",
  RowsUpdatedAt: new Date().toISOString()
};

function row(name, processName, filePath, score, delta, cpu, bursts, policy, udp = false, stream = false) {
  return {
    Name: name,
    ProcessName: processName,
    Path: filePath,
    Key: processName + "|" + filePath,
    Score: score,
    Delta: delta,
    Cpu: cpu,
    Bursts: bursts,
    Policy: policy,
    BehaviorTier: policy,
    BehaviorConfidence: 88,
    BehaviorWakeCount: 3,
    IntentKind: udp ? "Gaming" : (stream ? "Streaming" : "Desktop"),
    Intent: udp ? "Gaming 96" : (stream ? "Streaming 92" : "Desktop"),
    UdpEndpoints: udp ? 12 : 0,
    UdpGameProtected: udp,
    UdpGuardActive: udp,
    UdpConfidence: udp ? 96 : 0,
    UdpConfidenceLabel: udp ? "High" : "",
    StreamingGuard: stream,
    ProtectedGuard: policy === "Protect",
    Action: "P OK/M OK/IO OK/E OK/Tier " + policy
  };
}

function game(id, name, summary, installed, running) {
  return {
    Id: id,
    Name: name,
    ShortName: name.split(" ")[0],
    Summary: summary,
    Description: summary,
    Tier: "Best performance",
    Genre: "Game",
    ExpectedGain: "Mais estabilidade",
    Installed: installed,
    Running: running,
    DetectedPath: installed ? `D:\\Games\\${name}\\${id}.exe` : "",
    SafeOptimizations: [
      "Proteger processo do jogo e renderizador principal.",
      "Evitar EcoQoS em subprocessos criticos durante a partida.",
      "Preservar cache grafico saudavel e monitorar recompilacoes."
    ],
    ExperimentalOptimizations: [
      "Reduzir concorrencia de I/O quando o jogo estiver compilando shaders.",
      "Aplicar perfil assistido de warmup quando houver suporte validado."
    ]
  };
}

class DevToolsClient {
  constructor(ws) {
    this.ws = ws;
    this.nextId = 1;
    this.pending = new Map();
    this.listeners = new Map();
    ws.addEventListener("message", event => {
      const message = JSON.parse(event.data);
      if (message.id && this.pending.has(message.id)) {
        const { resolve, reject } = this.pending.get(message.id);
        this.pending.delete(message.id);
        if (message.error) reject(new Error(message.error.message || JSON.stringify(message.error)));
        else resolve(message.result || {});
        return;
      }
      if (message.method && this.listeners.has(message.method)) {
        const listeners = this.listeners.get(message.method);
        this.listeners.set(message.method, []);
        listeners.forEach(listener => listener(message.params || {}));
      }
    });
  }
  send(method, params = {}) {
    const id = this.nextId++;
    this.ws.send(JSON.stringify({ id, method, params }));
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      setTimeout(() => {
        if (!this.pending.has(id)) return;
        this.pending.delete(id);
        reject(new Error(`DevTools timeout: ${method}`));
      }, 10000);
    });
  }
  once(method, timeoutMs = 10000) {
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error(`DevTools event timeout: ${method}`)), timeoutMs);
      const listeners = this.listeners.get(method) || [];
      listeners.push(params => {
        clearTimeout(timer);
        resolve(params);
      });
      this.listeners.set(method, listeners);
    });
  }
}

async function fetchJson(url, tries = 60) {
  let lastError;
  for (let i = 0; i < tries; i++) {
    try {
      const response = await fetch(url);
      if (response.ok) return await response.json();
      lastError = new Error(`HTTP ${response.status}`);
    } catch (error) {
      lastError = error;
    }
    await sleep(120);
  }
  throw lastError || new Error(`Could not fetch ${url}`);
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function main() {
  const port = 9300 + Math.floor(Math.random() * 400);
  const userDataDir = path.join(os.tmpdir(), "sbn-responsive-" + Date.now());
  const chrome = spawn(chromePath, [
    "--headless=new",
    "--disable-gpu",
    "--disable-extensions",
    "--allow-file-access-from-files",
    `--remote-debugging-port=${port}`,
    `--user-data-dir=${userDataDir}`,
    "about:blank"
  ], { stdio: "ignore" });

  try {
    await fetchJson(`http://127.0.0.1:${port}/json/version`);
    const targets = await fetchJson(`http://127.0.0.1:${port}/json/list`);
    const page = targets.find(target => target.type === "page");
    if (!page || !page.webSocketDebuggerUrl) throw new Error("No Chrome page target was exposed.");
    const ws = new WebSocket(page.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => {
      ws.addEventListener("open", resolve, { once: true });
      ws.addEventListener("error", reject, { once: true });
    });
    const client = new DevToolsClient(ws);
    await client.send("Page.enable");
    await client.send("Runtime.enable");

    const failures = [];
    for (const scenario of scenarios) {
      const result = await runScenario(client, scenario);
      if (!result.ok) failures.push(result);
    }

    try { ws.close(); } catch {}
    if (failures.length) {
      const details = failures.map(f => `${f.name}: ${f.errors.join("; ")}${f.offenders && f.offenders.length ? " | offenders: " + f.offenders.join(" > ") : ""}`).join("\n");
      throw new Error("Responsive visual guard failed:\n" + details);
    }
    console.log(`launcher responsive visual ok (${scenarios.length} scenarios, screenshots: ${outputRoot})`);
  } finally {
    try { chrome.kill(); } catch {}
    await sleep(250);
    try { rmSync(userDataDir, { recursive: true, force: true }); } catch {}
  }
}

async function runScenario(client, scenario) {
  await client.send("Emulation.setDeviceMetricsOverride", {
    width: scenario.width,
    height: scenario.height,
    deviceScaleFactor: scenario.dpr || 1,
    mobile: false,
    screenWidth: scenario.width,
    screenHeight: scenario.height
  });
  const loaded = client.once("Page.loadEventFired");
  await client.send("Page.navigate", { url: pathToFileURL(dashboardPath).href });
  await loaded;
  await sleep(160);

  const script = `
(async () => {
  const state = Object.assign({}, ${JSON.stringify(mockState)}, ${JSON.stringify(scenario.statePatch || {})});
  if (${JSON.stringify(!!scenario.appliedPreset)}) {
    const applied = (state.GamePresets || []).find(game => game.Id === "longgame1");
    if (applied) {
      applied.PresetApplied = true;
      applied.PresetStatus = "applied";
      applied.SelectedSafeCount = Array.isArray(applied.SafeOptimizations) ? applied.SafeOptimizations.length : 0;
      applied.SelectedExperimentalCount = 0;
      applied.BackupFiles = 5;
      applied.LastAppliedAt = new Date().toISOString();
    }
  }
  window.__scenarioName = ${JSON.stringify(scenario.name)};
  smartNapUpdate(state);
  showView(${JSON.stringify(scenario.view || "dashboard")});
  window.__expectEnergyPrompt = ${JSON.stringify(scenario.modal || "")} === "energyPrompt";
  if (${JSON.stringify(scenario.modal || "")} === "gamePreset") {
    openGamePreset("longgame1");
    if (${JSON.stringify(!!scenario.technicalDetails)}) {
      const toggle = document.querySelector("#gamePresetTechnicalToggle");
      if (toggle && !toggle.checked) toggle.click();
    }
  }
  else if (${JSON.stringify(scenario.modal || "")} === "energyPrompt") setSessionMode("Gaming");
  else if (${JSON.stringify(scenario.modal || "")}) openOverlay(${JSON.stringify(scenario.modal || "")});
  const scenarioScrollTop = ${JSON.stringify(scenario.scrollTop || 0)};
  if (scenarioScrollTop) {
    const workspace = document.querySelector(".workspace");
    if (workspace) {
      workspace.scrollTop = scenarioScrollTop;
      updateChromeCompact(true);
    }
  }
  const activityCard = ${JSON.stringify(scenario.activityCard || "")};
  window.__activityCard = activityCard;
  if (activityCard) {
    const card = document.querySelector('.activityPanel [data-activity-card="' + activityCard + '"]');
    if (card && typeof toggleActivityCard === "function") toggleActivityCard(card);
    await new Promise(resolve => setTimeout(resolve, 280));
  }
  return validateResponsivePage();
})()
`;

  await client.send("Runtime.evaluate", {
    expression: `
window.validateResponsivePage = function(){
  const vw = document.documentElement.clientWidth;
  const vh = document.documentElement.clientHeight;
  const workspace = document.querySelector(".workspace");
  const errors = [];
  const offenderList = [];
  const workspaceOverflow = workspace ? Math.ceil(workspace.scrollWidth - workspace.clientWidth) : 0;
  const docOverflow = Math.ceil(document.documentElement.scrollWidth - vw);
  if (workspaceOverflow > 4) errors.push("workspace horizontal overflow " + workspaceOverflow + "px");
  if (docOverflow > 4) errors.push("document horizontal overflow " + docOverflow + "px");
  const visible = el => {
    const style = getComputedStyle(el);
    const rect = el.getBoundingClientRect();
    return style.display !== "none" && style.visibility !== "hidden" && rect.width > 0 && rect.height > 0;
  };
  const rootStyle = getComputedStyle(document.documentElement);
  const motionToken = rootStyle.getPropertyValue("--motion-standard").trim();
  if (!motionToken) errors.push("motion tokens missing");
  const bodyAmbient = getComputedStyle(document.body, "::before").animationName;
  if (bodyAmbient && bodyAmbient !== "none") errors.push("ambient body animation re-enabled: " + bodyAmbient);
  const streamerMode = document.querySelector("#modeStreamer");
  if (streamerMode && getComputedStyle(streamerMode).animationName !== "none") errors.push("streamer mode has continuous decorative animation");
  const modeDeck = document.querySelector("#modeDeck");
  if (window.__expectEnergyPrompt && !document.querySelector("#energyOverlay.open")) errors.push("energy mode popup did not open from mode card selection");
  if (modeDeck && visible(modeDeck)) {
    const modeButtons = Array.from(modeDeck.querySelectorAll(".modeBtn")).filter(visible);
    if (modeButtons.length !== 6) errors.push("engine mode selector lost modes: " + modeButtons.length);
    if (!modeDeck.querySelector("#modeSelectedValue") || !modeDeck.querySelector("#modeAppliedValue")) errors.push("engine mode selected/applied state missing");
    if (!modeDeck.querySelector("#currentPowerPlanValue") || !modeDeck.querySelector("#recommendedPowerPlanValue")) errors.push("engine mode power-plan hierarchy missing");
    if (!modeDeck.querySelector(".selectedMode") || !modeDeck.querySelector(".modeFacts") || !modeDeck.querySelector("#modeControlState")) errors.push("engine mode lower summary/status blocks missing");
    if (!modeDeck.querySelector("#modeDetailTitle")) errors.push("engine mode lower panel lost details title");
    if (modeDeck.querySelector("#selectedModeIcon") || modeDeck.querySelector("#modeSelectionState")) errors.push("engine mode lower panel reintroduced duplicated selected/applied markers");
    const visibleHiddenState = Array.from(modeDeck.querySelectorAll("#modeSelectedValue,#modeAppliedValue")).some(visible);
    if (visibleHiddenState) errors.push("engine mode lower panel is visibly repeating selected/applied state");
    if (!modeDeck.querySelector(".modePlanNote")) errors.push("engine mode selected summary lost plan note");
    const priorityTags = Array.from(modeDeck.querySelectorAll("#modePriorityTags span")).filter(visible);
    if (priorityTags.length < 3) errors.push("engine mode priorities are not rendered");
    const selected = modeDeck.querySelectorAll(".modeBtn.selected").length;
    const applied = modeDeck.querySelectorAll(".modeBtn.applied").length;
    if (applied !== 1) errors.push("engine mode applied marker count " + applied);
    if (selected > 1) errors.push("engine mode selected marker count " + selected);
    modeButtons.forEach(button => {
      const rect = button.getBoundingClientRect();
      const descriptionVisible = Array.from(button.querySelectorAll(".modeCopy span")).some(visible);
      const chipVisible = Array.from(button.querySelectorAll("strong")).some(visible);
      if (rect.height < 86) errors.push("engine mode card collapsed into tab: " + Math.round(rect.height));
      if (!descriptionVisible || !chipVisible) errors.push("engine mode card lost description/chip");
      Array.from(button.querySelectorAll(".modeIcon,.modeCopy,.modeState,strong")).filter(visible).forEach(child => {
        const childRect = child.getBoundingClientRect();
        if (childRect.left < rect.left - 2 || childRect.right > rect.right + 2 || childRect.top < rect.top - 2 || childRect.bottom > rect.bottom + 2) {
          errors.push("engine mode child outside card: " + (button.id || button.textContent.trim()));
        }
      });
    });
  }
  const primaryButton = document.querySelector(".btn.primary");
  if (primaryButton && visible(primaryButton)) {
    const durationText = getComputedStyle(primaryButton).transitionDuration || "";
    const maxDuration = Math.max(...durationText.split(",").map(v => {
      v = v.trim();
      if (v.endsWith("ms")) return parseFloat(v) / 1000;
      if (v.endsWith("s")) return parseFloat(v);
      return 0;
    }), 0);
    if (maxDuration > 0.4) errors.push("primary button transition too slow " + maxDuration + "s");
  }
  const toastHost = document.querySelector("#toastHost.toastHost");
  if (!toastHost) errors.push("toast host missing");
  const smallButtons = Array.from(document.querySelectorAll("button:not(.chromeBtn):not(.policyBtn)"))
    .filter(visible)
    .filter(el => {
      const rect = el.getBoundingClientRect();
      return rect.width < 42 || rect.height < 30;
    })
    .map(el => (el.textContent || el.title || el.className || "button").trim().slice(0, 60));
  if (smallButtons.length) errors.push("tiny buttons: " + smallButtons.slice(0, 5).join(", "));
  const clipped = Array.from(document.querySelectorAll(".card b,.focusLine b,.systemTile b,.gameBody h3,.btn,.gameMiniBtn,.gamePresetChip,.managerTitle #managerStatus"))
    .filter(visible)
    .filter(el => {
      const style = getComputedStyle(el);
      return style.overflowX === "hidden" && el.scrollWidth > el.clientWidth + 3;
    })
    .map(el => (el.textContent || el.className || "text").trim().slice(0, 60));
  if (clipped.length) errors.push("clipped text: " + clipped.slice(0, 5).join(", "));
  const clippedTimelineText = Array.from(document.querySelectorAll(".eventLine .eventHead,.eventLine span,.eventLine i,.eventLine b,.eventLine em"))
    .filter(visible)
    .filter(el => {
      const style = getComputedStyle(el);
      const hidesX = style.overflowX === "hidden" || style.overflowX === "clip";
      const hidesY = style.overflowY === "hidden" || style.overflowY === "clip";
      return (hidesX && el.scrollWidth > el.clientWidth + 3) || (hidesY && el.scrollHeight > el.clientHeight + 3);
    })
    .map(el => (el.textContent || el.className || "timeline text").trim().slice(0, 60));
  if (clippedTimelineText.length) errors.push("timeline clipped text: " + clippedTimelineText.slice(0, 5).join(", "));
  const timelineRows = Array.from(document.querySelectorAll(".timeline .eventLine")).filter(visible);
  for (let i = 1; i < timelineRows.length; i++) {
    const previous = timelineRows[i - 1].getBoundingClientRect();
    const current = timelineRows[i].getBoundingClientRect();
    if (current.top < previous.bottom - 2) {
      errors.push("timeline rows overlap");
      break;
    }
  }
  const timelineChildOverflow = [];
  timelineRows.forEach(row => {
    const rowRect = row.getBoundingClientRect();
    Array.from(row.querySelectorAll("time,i,b,em,span")).filter(visible).forEach(child => {
      const childRect = child.getBoundingClientRect();
      if (childRect.left < rowRect.left - 2 || childRect.right > rowRect.right + 2 || childRect.top < rowRect.top - 2 || childRect.bottom > rowRect.bottom + 2) {
        timelineChildOverflow.push((child.textContent || child.className || "timeline child").trim().slice(0, 60));
      }
    });
  });
  if (timelineChildOverflow.length) errors.push("timeline child outside card: " + timelineChildOverflow.slice(0, 4).join(", "));
  const topbar = document.querySelector(".topbar");
  const dashboard = document.querySelector("#dashboardView");
  if (topbar && dashboard && visible(topbar) && visible(dashboard) && vh > vw) {
    const topbarRect = topbar.getBoundingClientRect();
    const dashboardRect = dashboard.getBoundingClientRect();
    const gap = Math.round(dashboardRect.top - topbarRect.bottom);
    if (topbarRect.height > 190) errors.push("portrait topbar too tall " + Math.round(topbarRect.height) + "px");
    if (gap > 42) errors.push("portrait dashboard starts too far below topbar " + gap + "px");
  }
  const networkPanel = document.querySelector(".commandHero .networkPanel");
  const chipRow = document.querySelector(".commandHero .chipRow");
  if (networkPanel && chipRow && visible(networkPanel) && visible(chipRow)) {
    const panelRect = networkPanel.getBoundingClientRect();
    const chipRect = chipRow.getBoundingClientRect();
    const gap = Math.round(chipRect.top - panelRect.bottom);
    if (gap > 72) errors.push("command hero chip row gap too large " + gap + "px");
  }
  const commandHero = document.querySelector(".commandHero");
  const quickCards = document.querySelector(".commandMainFlow > .cards");
  const engineCard = document.querySelector(".command .engineCard, .leftCol > .engineCard, #control");
  const syncPill = document.querySelector("#live");
  if (syncPill && visible(syncPill) && /Painel sincronizado|Panel synced/i.test(syncPill.textContent || "")) {
    errors.push("normal panel-synced pill is visible in the topbar");
  }
  if (quickCards && visible(quickCards)) {
    const quickLabels = Array.from(quickCards.querySelectorAll(".card small")).map(el => (el.textContent || "").trim()).join(" | ");
    if (/Ações no passe|Pass actions|Zero Ping|ShaderBoost/i.test(quickLabels)) {
      errors.push("dashboard quick cards are mirroring module/status labels: " + quickLabels);
    }
    ["Última intervenção", "Picos recentes", "Última detecção UDP", "Saúde do cache gráfico"].forEach(label => {
      if (!quickLabels.includes(label)) errors.push("dashboard quick cards missing unique label: " + label);
    });
  }
  if (engineCard && visible(engineCard)) {
    const sessionMain = engineCard.querySelector("#engineSessionMain");
    const ringCaption = engineCard.querySelector("#engineRingCaption");
    const ringDetail = engineCard.querySelector("#engineRingDetail");
    const processBlock = engineCard.querySelector(".engineProcessBlock");
    const rhythm = engineCard.querySelector(".engineRhythm");
    const cycleLine = engineCard.querySelector(".engineCycleLine");
    const passCard = engineCard.querySelector("#enginePulseCard");
    if (!sessionMain || !visible(sessionMain)) errors.push("realtime control missing protected-apps headline");
    if (!ringCaption || !ringDetail || !visible(ringCaption) || !visible(ringDetail)) errors.push("realtime control pulse caption/detail missing");
    if (!processBlock || !visible(processBlock)) errors.push("realtime control processing block missing");
    if (!rhythm || !visible(rhythm) || !cycleLine || !visible(cycleLine)) errors.push("realtime control engine rhythm/cycle line missing");
    const detailText = (engineCard.querySelector("#detail")?.textContent || "").trim();
    const headlineText = (sessionMain?.textContent || "").trim();
    const headlineApps = headlineText.match(/^(\d+)\s+apps/i);
    if (headlineApps && new RegExp("^" + headlineApps[1] + "\\s+apps", "i").test(detailText)) errors.push("realtime control header detail repeats protected-app count");
    const ringCaptionText = (ringCaption?.textContent || "").trim();
    if (/apps protegidos|protected apps/i.test(ringCaptionText)) errors.push("realtime ring caption duplicates protected-app headline");
    if (/apps agrupados|grouped apps/i.test(ringCaptionText)) errors.push("realtime ring caption repeats grouped-app copy instead of complementary process context");
    if (/^0(?:[,.]0+)?\s*(?:MB|GB)\b/i.test(detailText)) errors.push("realtime header detail exposes raw zero-memory copy");
    const actionSummary = engineCard.querySelector("#engineActionsSummary");
    if (actionSummary && /CPU\s+\d+\s*•\s*RAM/i.test(actionSummary.textContent || "")) errors.push("realtime applied actions still render as a textual split instead of mini-grid");
    if (actionSummary && /CPU|RAM|I\/O|EcoQoS/i.test(actionSummary.textContent || "") && !actionSummary.querySelector(".engineActionGrid")) errors.push("realtime applied actions missing engineActionGrid");
    const actionGrid = actionSummary?.querySelector(".engineActionGrid");
    if (actionGrid) {
      const actionMetrics = Array.from(actionGrid.querySelectorAll(".engineActionMetric"));
      if (actionMetrics.length !== 4) errors.push("realtime applied actions grid must expose exactly four mini-metrics");
      const actionStyle = getComputedStyle(actionGrid);
      if (!/^repeat\(2,/.test(actionStyle.gridTemplateColumns) && actionStyle.gridTemplateColumns.split(" ").length !== 2) errors.push("realtime applied actions grid is not two columns");
      const stackedDirection = actionMetrics.some(el => getComputedStyle(el).flexDirection === "column");
      if (stackedDirection) errors.push("realtime applied actions metrics stacked vertically instead of compact horizontal label/value pairs");
      if (actionGrid.getBoundingClientRect().height > 66) errors.push("realtime applied actions grid is too tall and reintroduces processing-block void");
    }
    const firstSummaryCells = Array.from(engineCard.querySelectorAll(".engineSummary > span")).slice(0, 2);
    if (firstSummaryCells.length === 2) {
      const maxCellHeight = Math.max(...firstSummaryCells.map(el => el.getBoundingClientRect().height));
      if (maxCellHeight > 110) errors.push("realtime processing first row is artificially tall");
    }
    if (/^\d{1,2}:\d{2}$/.test((engineCard.querySelector("#engineNext")?.textContent || "").trim())) errors.push("realtime cycle-state metric duplicates next-cycle timer");
    const sessionTimeText = (engineCard.querySelector("#engineBeat")?.textContent || "").trim();
    const lastPassTimeText = (engineCard.querySelector("#enginePulseStamp")?.textContent || "").trim();
    if (/^\d{1,3}:\d{2}:\d{2}$/.test(sessionTimeText)) errors.push("realtime session duration still looks like an absolute clock timestamp");
    if (sessionTimeText && lastPassTimeText && sessionTimeText === lastPassTimeText) errors.push("realtime session duration duplicates last-pass timestamp");
    if (/^(?:h[áa]\s+\d+|\d+[smh]\s+atr|\d{1,2}:\d{2}(?::\d{2})?)$/i.test(lastPassTimeText)) errors.push("last-pass compact stamp repeats live temporal counters instead of a completion state");
    const applyText = (engineCard.querySelector("#apply")?.textContent || "").trim();
    if (/^Parar$/i.test(applyText)) errors.push("realtime apply button exposes ambiguous Parar state");
    if ((engineCard.querySelector("#engineIntentSummary")?.textContent || "").trim() === "Work") errors.push("realtime context is not translated from Work");
    const applyButton = engineCard.querySelector("#apply");
    if (applyButton) {
      const restAfter = getComputedStyle(applyButton, "::after").content;
      if (restAfter && restAfter !== "none" && restAfter !== "normal" && !applyButton.classList.contains("motion-pending")) errors.push("realtime apply button shows a decorative pseudo-element at rest");
    }
    if (passCard && visible(passCard)) {
      if (!passCard.querySelector(".enginePulseChevron")) errors.push("last-pass pulse card missing chevron");
      if (!passCard.querySelector("#enginePulseDetails")) errors.push("last-pass pulse details missing");
      if (passCard.scrollWidth > passCard.clientWidth + 3) errors.push("last-pass pulse card has horizontal overflow");
    } else {
      errors.push("last-pass pulse card missing");
    }
  }
  const activityPanel = document.querySelector(".activityPanel");
  if (activityPanel && visible(activityPanel)) {
    const groupCount = activityPanel.querySelectorAll(".activityGroupLabel").length;
    if (groupCount < 4) errors.push("activity panel is missing hierarchy group labels");
    const insightButton = activityPanel.querySelector("#enginePass");
    if (!insightButton || insightButton.tagName !== "BUTTON") errors.push("activity insights control must be a real button");
    const activityText = activityPanel.textContent || "";
    if (/Efeito de mem|Memory effect/i.test(activityText)) errors.push("activity panel still shows duplicated memory-effect copy");
    if (activityPanel.querySelector(".metric-pressure")) errors.push("activity panel still renders latest relief as an orphan metric card");
    const reclaimedText = (activityPanel.querySelector("#reclaimedValue")?.textContent || "").trim();
    if (/aliviad|relieved/i.test(reclaimedText)) errors.push("reclaimed memory value should be the value only, not duplicate the label: " + reclaimedText);
    const reclaimedInline = activityPanel.querySelector("#reclaimedInline");
    if (!reclaimedInline) errors.push("activity panel memory card is missing inline latest-relief detail");
    const expandableCards = Array.from(activityPanel.querySelectorAll(".focusLine.activity-expandable")).filter(visible);
    const expandableCount = expandableCards.length;
    if (expandableCount < 4) errors.push("activity panel should expose inline expansion for detail-rich cards");
    const missingChevron = expandableCards
      .filter(card => !card.querySelector(".activityChevron"))
      .map(card => card.dataset.activityCard || card.textContent.trim().slice(0, 40));
    if (missingChevron.length) errors.push("expandable activity cards missing chevron: " + missingChevron.join(", "));
    const scenarioName = window.__scenarioName || "";
    const resourceLabel = activityPanel.querySelector(".activityResourcesLabel");
    const resourceKeys = ["zero", "shader", "cpu", "live"];
    const resourceCard = key => activityPanel.querySelector('[data-activity-card="' + key + '"]');
    const resourceVisible = key => {
      const card = resourceCard(key);
      return !!card && visible(card);
    };
    const visibleResourceCards = resourceKeys.map(resourceCard).filter(card => card && visible(card));
    if (scenarioName.includes("activity-resources-idle")) {
      resourceKeys.forEach(key => {
        if (resourceVisible(key)) errors.push("idle session should hide resource card: " + key);
      });
      if (resourceLabel && visible(resourceLabel)) errors.push("idle session should hide the active resources section");
    }
    if (scenarioName.includes("activity-resources-game-no-live")) {
      ["zero", "shader", "cpu"].forEach(key => {
        if (!resourceVisible(key)) errors.push("game session should show resource card: " + key);
      });
      if (resourceVisible("live")) errors.push("game without live should hide Live Guard");
    }
    if (scenarioName.includes("activity-resources-live-only")) {
      if (!resourceVisible("live")) errors.push("live context should show Live Guard");
      ["zero", "shader", "cpu"].forEach(key => {
        if (resourceVisible(key)) errors.push("live-only session should hide unrelated resource card: " + key);
      });
    }
    if (!visibleResourceCards.length && resourceLabel && visible(resourceLabel)) errors.push("active resources label visible without cards");
    if (visibleResourceCards.length && resourceLabel && !visible(resourceLabel)) errors.push("active resources label hidden while cards are visible");
    if (vw > 760 && visibleResourceCards.length % 2 === 1) {
      const focusBoxRect = activityPanel.querySelector(".focusBox")?.getBoundingClientRect();
      const lastResource = visibleResourceCards[visibleResourceCards.length - 1];
      if (focusBoxRect && lastResource.getBoundingClientRect().width < focusBoxRect.width - 12) {
        errors.push("odd active resource card does not span full width: " + (lastResource.dataset.activityCard || "unknown"));
      }
    }
    const focusBox = activityPanel.querySelector(".focusBox");
    if (focusBox && vw > 760) {
      const focusBoxRect = focusBox.getBoundingClientRect();
      const compactCards = Array.from(focusBox.querySelectorAll(".focusLine:not(.expanded)"))
        .filter(visible)
        .filter(card => !card.classList.contains("metric-top") && !card.classList.contains("metric-gpu") && !card.classList.contains("metric-intent"))
        .filter(card => card.getBoundingClientRect().width < focusBoxRect.width * 0.75);
      const rows = [];
      compactCards
        .map(card => ({ card, rect: card.getBoundingClientRect() }))
        .sort((a, b) => a.rect.top - b.rect.top || a.rect.left - b.rect.left)
        .forEach(item => {
          let row = rows.find(candidate => Math.abs(candidate.top - item.rect.top) <= 4);
          if (!row) {
            row = { top: item.rect.top, items: [] };
            rows.push(row);
          }
          row.items.push(item);
        });
      rows.filter(row => row.items.length >= 2).forEach(row => {
        const bottoms = row.items.map(item => item.rect.bottom);
        const bottomSpread = Math.max(...bottoms) - Math.min(...bottoms);
        if (bottomSpread > 3) {
          errors.push("closed activity cards in the same row have uneven bottoms: " + row.items.map(item => item.card.dataset.activityCard).join(", "));
        }
        const chevronTops = row.items
          .map(item => item.card.querySelector(".activityChevron")?.getBoundingClientRect().top)
          .filter(value => Number.isFinite(value));
        if (chevronTops.length >= 2 && Math.max(...chevronTops) - Math.min(...chevronTops) > 2) {
          errors.push("activity chevrons are not aligned in compact row: " + row.items.map(item => item.card.dataset.activityCard).join(", "));
        }
      });
      const overTallValues = compactCards.filter(card => {
        const value = card.querySelector("b");
        if (!value) return false;
        const style = getComputedStyle(value);
        const lineHeight = parseFloat(style.lineHeight) || 16;
        return value.getBoundingClientRect().height > lineHeight * 2.35;
      }).map(card => card.dataset.activityCard || card.textContent.trim().slice(0, 40));
      if (overTallValues.length) errors.push("closed activity values exceed two-line compact limit: " + overTallValues.join(", "));
      const bloatedSimpleCards = compactCards.filter(card => !card.classList.contains("activity-has-summary"))
        .filter(card => card.getBoundingClientRect().height > 92)
        .map(card => card.dataset.activityCard || card.textContent.trim().slice(0, 40));
      if (bloatedSimpleCards.length) errors.push("simple closed activity cards are too tall: " + bloatedSimpleCards.join(", "));
      const bloatedSummaryCards = compactCards.filter(card => card.classList.contains("activity-has-summary"))
        .filter(card => card.getBoundingClientRect().height > 106)
        .map(card => card.dataset.activityCard || card.textContent.trim().slice(0, 40));
      if (bloatedSummaryCards.length) errors.push("summary closed activity cards are too tall: " + bloatedSummaryCards.join(", "));
    }
    if (/QoS Ready|Modo:\s*Armed|Mode:\s*Armed|Stack preservada|Stack preserved|Coleta:|MonitoringOnly|Open a game or run a pass/i.test(activityText)) {
      errors.push("activity panel exposes raw diagnostic copy");
    }
    const topImpactValue = (activityPanel.querySelector("#engineTop")?.textContent || "").trim();
    if (topImpactValue === "-") errors.push("activity panel top-impact empty state still renders only '-'");
    const shaderCompact = (activityPanel.querySelector("#shaderBoostState")?.textContent || "").trim();
    if (/Battlefield|Open Beta|DirectX|Vulkan|DX12|DX11/i.test(shaderCompact)) {
      errors.push("activity ShaderBoost compact state is too verbose: " + shaderCompact);
    }
    if (/•|processos UDP|UDP processes/i.test((activityPanel.querySelector("#udpTelemetry")?.textContent || "").trim())) {
      errors.push("Zero Ping compact value should be module state only");
    }
    if (/•|\d+%/.test(shaderCompact)) {
      errors.push("ShaderBoost compact value should be module state only");
    }
    if (/^Pronto$/i.test((activityPanel.querySelector("#cpuBoundState")?.textContent || "").trim())) {
      errors.push("CPU-Bound Assist compact state is too generic");
    }
    if (/^Pronto$/i.test((activityPanel.querySelector("#streamTelemetry")?.textContent || "").trim())) {
      errors.push("Live Guard compact state is too generic");
    }
    const expandedKey = window.__activityCard || "";
    if (expandedKey) {
      const target = activityPanel.querySelector('[data-activity-card="' + expandedKey + '"]');
      if (!target) errors.push("requested expanded activity card missing: " + expandedKey);
      else {
        if (!target.classList.contains("expanded")) errors.push("requested activity card did not expand: " + expandedKey);
        if (!target.querySelector(".activityChevron")) errors.push("expanded activity card missing chevron: " + expandedKey);
        const detailRows = target.querySelectorAll(".activityDetailRow,.activityDetailText").length;
        if (detailRows < 1) errors.push("expanded activity card has no structured details: " + expandedKey);
        if (target.scrollHeight > target.clientHeight + 3) errors.push("expanded activity card clips its own content: " + expandedKey);
        if (target.scrollWidth > target.clientWidth + 3) errors.push("expanded activity card has horizontal overflow: " + expandedKey);
        const targetRect = target.getBoundingClientRect();
        const focusBoxRect = activityPanel.querySelector(".focusBox")?.getBoundingClientRect();
        if (focusBoxRect && targetRect.width < focusBoxRect.width - 12) {
          errors.push("expanded activity card does not span full grid width: " + expandedKey);
        }
        const clippedDetails = Array.from(target.querySelectorAll(".activityDetailsInner,.activityDetailsInner *")).filter(visible).filter(child => {
          const childRect = child.getBoundingClientRect();
          return childRect.bottom > targetRect.bottom + 2;
        });
        if (clippedDetails.length) errors.push("expanded activity detail escapes card: " + expandedKey);
        const groupFor = key => (["zero","shader","cpu","live"].includes(key) ? "resources" : (key === "context" ? "recent" : (key === "impact" ? "impact" : "results")));
        const group = groupFor(expandedKey);
        const expandedInGroup = Array.from(activityPanel.querySelectorAll(".focusLine.expanded"))
          .filter(card => groupFor(card.dataset.activityCard || "") === group).length;
        if (expandedInGroup !== 1) errors.push("activity accordion group has " + expandedInGroup + " expanded cards for " + group);
      }
    }
  }
  if (vw >= 900 && commandHero && visible(commandHero)) {
    const heroRect = commandHero.getBoundingClientRect();
    const expectedMin = Math.min(520, Math.round(vw * 0.38));
    if (heroRect.width < expectedMin) errors.push("command hero squeezed " + Math.round(heroRect.width) + "px");
  }
  if (vw >= 900 && engineCard && visible(engineCard)) {
    const engineRect = engineCard.getBoundingClientRect();
    const expectedMin = Math.min(360, Math.round(vw * 0.26));
    if (engineRect.width < expectedMin) errors.push("engine card squeezed " + Math.round(engineRect.width) + "px");
  }
  if (vw >= 1800 && commandHero && quickCards && engineCard && visible(commandHero) && visible(quickCards) && visible(engineCard)) {
    const heroRect = commandHero.getBoundingClientRect();
    const cardsRect = quickCards.getBoundingClientRect();
    const engineRect = engineCard.getBoundingClientRect();
    const mainFlowGap = Math.round(cardsRect.top - heroRect.bottom);
    if (mainFlowGap > 34) errors.push("dashboard overview-to-cards gap too large " + mainFlowGap + "px");
    const cardsUnderHero = Math.abs(cardsRect.left - heroRect.left) <= 6 && cardsRect.right <= heroRect.right + 8;
    if (cardsUnderHero && engineRect.bottom > heroRect.bottom + 80) {
      const gap = Math.round(cardsRect.top - heroRect.bottom);
      if (gap > 34) errors.push("dashboard quick cards leave unused command gap " + gap + "px");
    }
    const modeDeck = document.querySelector("#modeDeck");
    if (modeDeck && visible(modeDeck)) {
      const modeGap = Math.round(modeDeck.getBoundingClientRect().top - cardsRect.bottom);
      if (modeGap > 44) errors.push("dashboard quick cards-to-mode gap too large " + modeGap + "px");
    }
    const commandPanel = document.querySelector(".leftCol > .command.panel");
    if (commandPanel && commandPanel.contains(engineCard)) errors.push("realtime control is still nested inside the left command panel");
    const primaryRegion = document.querySelector(".primaryRegion");
    const primaryTopGrid = document.querySelector(".primaryTopGrid");
    const activityRail = document.querySelector(".activityRail");
    const activityPanelForLayout = document.querySelector(".activityPanel");
    if (!primaryRegion || !visible(primaryRegion)) errors.push("dashboard primary region missing");
    if (!primaryTopGrid || !visible(primaryTopGrid)) errors.push("dashboard primary top grid missing");
    if (!activityRail || !visible(activityRail)) errors.push("dashboard activity rail missing");
    if (primaryTopGrid && !primaryTopGrid.contains(engineCard)) errors.push("realtime control is not inside the primary top grid");
    if (activityRail && activityPanelForLayout && !activityRail.contains(activityPanelForLayout)) errors.push("activity panel is not inside the activity rail");
    if (primaryRegion && activityPanelForLayout && primaryRegion.contains(activityPanelForLayout)) errors.push("activity panel is still part of the primary flow");
    const lowerSections = [
      ["mode", document.querySelector("#modeDeck")],
      ["system", document.querySelector(".systemPanel")],
      ["manager", document.querySelector(".manager")]
    ].filter(([, section]) => section && visible(section));
    const controlRect = engineCard.getBoundingClientRect();
    const activityRect = activityPanelForLayout?.getBoundingClientRect();
    const lowerTargetWidth = Math.round(controlRect.right - heroRect.left);
    lowerSections.forEach(([name, section]) => {
      const sectionRect = section.getBoundingClientRect();
      if (!primaryRegion || !primaryRegion.contains(section)) errors.push("dashboard lower section escaped primary region: " + name);
      if (Math.abs(sectionRect.left - heroRect.left) > 6) {
        errors.push("dashboard lower section left edge drifted: " + name + " " + Math.round(sectionRect.left) + "/" + Math.round(heroRect.left));
      }
      if (sectionRect.width < lowerTargetWidth - 12 || sectionRect.width > lowerTargetWidth + 28) {
        errors.push("dashboard lower section width drifted from main flow: " + name + " " + Math.round(sectionRect.width) + "/" + lowerTargetWidth);
      }
      if (activityRect && sectionRect.right > activityRect.left - 6) {
        errors.push("dashboard lower section runs under activity column: " + name + " right=" + Math.round(sectionRect.right) + " activityLeft=" + Math.round(activityRect.left));
      }
    });
    if (modeDeck && primaryTopGrid && visible(modeDeck)) {
      const primaryTopRect = primaryTopGrid.getBoundingClientRect();
      const modeRect = modeDeck.getBoundingClientRect();
      const quickToModeGap = Math.round(modeRect.top - cardsRect.bottom);
      const topToModeGap = Math.round(modeRect.top - primaryTopRect.bottom);
      if (topToModeGap > 34) errors.push("dashboard primary top-to-mode gap too large " + topToModeGap + "px");
      if (quickToModeGap > 110) errors.push("dashboard quick cards still wait for unrelated rail height " + quickToModeGap + "px");
      if (activityRect && Math.abs(modeRect.top - activityRect.bottom) <= 8) {
        errors.push("dashboard mode is still locked to the activity panel bottom");
      }
    }
    if (activityPanelForLayout && visible(activityPanelForLayout)) {
      const timeline = activityPanelForLayout.querySelector(".timeline");
      const panelRect = activityPanelForLayout.getBoundingClientRect();
      const timelineRect = timeline?.getBoundingClientRect();
      if (timeline && visible(timeline) && panelRect.height > 720 && timelineRect.height < 240) {
        errors.push("activity timeline is not using available rail height");
      }
      const profilePanel = document.querySelector(".primaryRegion>.systemPanel");
      if (profilePanel && visible(profilePanel)) {
        const profileGap = Math.round(profilePanel.getBoundingClientRect().top - panelRect.bottom);
        if (profileGap > 34) errors.push("activity rail log ends before PC profile starts " + profileGap + "px");
      }
    }
  }
  const engineActions = document.querySelector(".engineCard .actions");
  const restoreButton = document.querySelector("#restoreBtn");
  if (engineActions && restoreButton && visible(engineActions) && visible(restoreButton) && engineActions.getBoundingClientRect().width > 360) {
    const actionRect = engineActions.getBoundingClientRect();
    const restoreRect = restoreButton.getBoundingClientRect();
    const leftGap = Math.abs(restoreRect.left - actionRect.left);
    const rightGap = Math.abs(restoreRect.right - actionRect.right);
    if (leftGap > 4 || rightGap > 4) errors.push("engine restore action is not full width");
  }
  const command = document.querySelector(".command");
  const commandChildren = Array.from(document.querySelectorAll(".commandHero,.command > .engineCard")).filter(visible);
  if (command && visible(command) && commandChildren.length) {
    const commandRect = command.getBoundingClientRect();
    const childBottom = Math.max(...commandChildren.map(el => el.getBoundingClientRect().bottom));
    const bottomGap = Math.round(commandRect.bottom - childBottom);
    const style = getComputedStyle(command);
    const hasVisibleSurface = style.backgroundColor !== "rgba(0, 0, 0, 0)" || style.borderTopWidth !== "0px";
    if (hasVisibleSurface && bottomGap > 56) errors.push("command panel has excessive empty bottom " + bottomGap + "px");
  }
  if (document.body.classList.contains("view-games")) {
    const logo = document.querySelector(".wordmarkLogo");
    if (logo && visible(logo)) {
      const animationName = getComputedStyle(logo).animationName;
      if (animationName && animationName !== "none") errors.push("games logo still animated: " + animationName);
    }
    const controls = document.querySelector(".windowControls");
    if (controls && visible(controls)) {
      const transform = getComputedStyle(controls).transform;
      if (transform && transform !== "none") {
        try {
          const matrix = new DOMMatrixReadOnly(transform);
          if (Math.abs(matrix.a - 1) > 0.02 || Math.abs(matrix.d - 1) > 0.02) {
            errors.push("games window controls use scaling transform");
          }
        } catch {}
      }
    }
  }
  const modal = document.querySelector(".open .languagePanel,.open .infoPanel,.open .energyPanel,.open .gamePresetPanel,.open .gameModePanel");
  if (modal) {
    const rect = modal.getBoundingClientRect();
    if (rect.left < -2 || rect.right > vw + 2) errors.push("modal horizontal outside viewport left=" + Math.round(rect.left) + " right=" + Math.round(rect.right) + " vw=" + vw);
    if (rect.top < -2 || rect.bottom > vh + 2) errors.push("modal vertical outside viewport top=" + Math.round(rect.top) + " bottom=" + Math.round(rect.bottom) + " vh=" + vh + " height=" + Math.round(rect.height));
    if (modal.classList.contains("gamePresetPanel")) {
      const isPortraitPreset = vh > vw && vw <= 1080;
      const scenarioName = window.__scenarioName || "";
      const actions = modal.querySelector(".gameActions");
      if (!actions || !visible(actions)) {
        errors.push("game preset actions are not visible");
      } else {
        const actionRect = actions.getBoundingClientRect();
        const bottomGap = Math.round(rect.bottom - actionRect.bottom);
        if (bottomGap > 64) errors.push("game preset modal excessive bottom gap " + bottomGap + "px");
      }
      const footer = modal.querySelector(".gamePresetFootSummary");
      if (!footer || !visible(footer)) errors.push("game preset decision footer is not visible");
      const techToggle = modal.querySelector(".gamePresetTechnical");
      if (!techToggle || !visible(techToggle)) errors.push("game preset technical toggle is not visible");
      if (techToggle && visible(techToggle)) {
        const toggleRect = techToggle.getBoundingClientRect();
        const toggleStyle = getComputedStyle(techToggle);
        const portraitLineControl = isPortraitPreset && parseFloat(toggleStyle.borderTopWidth || "0") === 0 && /rgba?\(0,\s*0,\s*0,\s*0\)|transparent/i.test(toggleStyle.backgroundColor || "transparent");
        if (!portraitLineControl && toggleRect.width > Math.min(260, rect.width * 0.45)) {
          errors.push("game preset technical toggle is stretched width=" + Math.round(toggleRect.width));
        }
      }
      const review = modal.querySelector(".gamePresetReview");
      if (review && visible(review)) {
        const style = getComputedStyle(review);
        if (review.scrollHeight > review.clientHeight + 4 && !/(auto|scroll)/.test(style.overflowY)) {
          errors.push("game preset review does not own overflow when content grows");
        }
        const firstCard = modal.querySelector(".gameOptionCard");
        const techChecked = !!modal.querySelector("#gamePresetTechnicalToggle")?.checked;
        if (isPortraitPreset && firstCard && visible(firstCard) && !techChecked) {
          const cardHeight = firstCard.getBoundingClientRect().height;
          if (cardHeight > 150) errors.push("game preset portrait compact card is too tall without technical details: " + Math.round(cardHeight) + "px");
        }
        const details = modal.querySelector(".gameOptionDetails");
        if (isPortraitPreset && details && visible(details) && techChecked) {
          const columns = getComputedStyle(details).gridTemplateColumns.split(" ").filter(Boolean).length;
          if (columns > 1) errors.push("game preset portrait technical details still use cramped multi-column grid");
        }
      }
      const lead = modal.querySelector(".gamePresetLead");
      const main = modal.querySelector(".gamePresetMain");
      if (lead && main && visible(lead) && visible(main) && isPortraitPreset) {
        const leadRect = lead.getBoundingClientRect();
        const mainRect = main.getBoundingClientRect();
        if (Math.abs(mainRect.top - leadRect.top) < 48 && mainRect.left > leadRect.left + leadRect.width - 4) {
          errors.push("game preset portrait layout still uses squeezed side-by-side columns");
        }
        const infoDisclosure = modal.querySelector(".gamePresetCompactInfo");
        if (!infoDisclosure || !visible(infoDisclosure)) {
          errors.push("game preset portrait layout is missing compact game info disclosure");
        }
      }
      if (isPortraitPreset && scenarioName.includes("applied")) {
        const primary = modal.querySelector("#gamePresetApplyButton");
        const appliedNote = modal.querySelector("#gamePresetAppliedNote");
        if (primary && visible(primary)) errors.push("game preset clean applied portrait state still shows a giant disabled primary CTA");
        if (!appliedNote || !visible(appliedNote)) errors.push("game preset clean applied portrait state is missing the applied-state note");
      }
      const close = modal.querySelector(".gamePresetClose");
      if (close && visible(close)) {
        const closeRect = close.getBoundingClientRect();
        const topGap = Math.round(closeRect.top - rect.top);
        const rightGap = Math.round(rect.right - closeRect.right);
        if (topGap > 28 || rightGap > 28) errors.push("game preset close is not in modal header");
      }
      const options = modal.querySelector(".gameOptions");
      if (options && visible(options) && getComputedStyle(options).alignItems !== "start") {
        errors.push("game preset optimization groups do not align independently");
      }
    }
  }
  const managerRows = document.querySelectorAll(".manager .row");
  if (vw <= 900 && managerRows.length) {
    const first = managerRows[0].getBoundingClientRect();
    if (first.width > (workspace ? workspace.clientWidth + 4 : vw + 4)) errors.push("manager card wider than viewport");
  }
  const policyGroups = Array.from(document.querySelectorAll(".policyButtons")).filter(visible);
  policyGroups.forEach(group => {
    const buttons = Array.from(group.querySelectorAll(".policyBtn")).filter(visible);
    if (buttons.length >= 5 && group.getBoundingClientRect().width >= 330) {
      const rects = buttons.map(button => button.getBoundingClientRect());
      const topSpread = Math.max(...rects.map(rect => rect.top)) - Math.min(...rects.map(rect => rect.top));
      const heightSpread = Math.max(...rects.map(rect => rect.height)) - Math.min(...rects.map(rect => rect.height));
      if (topSpread > 2 || heightSpread > 2) errors.push("policy buttons misaligned top=" + Math.round(topSpread) + " height=" + Math.round(heightSpread));
    }
  });
  if (workspaceOverflow > 4 && workspace) {
    Array.from(workspace.querySelectorAll("*"))
      .filter(visible)
      .forEach(el => {
        const rect = el.getBoundingClientRect();
        const extra = Math.ceil(rect.right - workspace.getBoundingClientRect().right);
        const scrollExtra = Math.ceil(el.scrollWidth - el.clientWidth);
        if (extra > 4 || scrollExtra > 24) {
          const label = (el.id ? "#" + el.id : "") + (el.className ? "." + String(el.className).trim().replace(/\\s+/g,".") : "") + "[" + Math.round(rect.width) + "w/" + el.scrollWidth + "sw]";
          offenderList.push(label);
        }
      });
  }
  return { errors, workspaceOverflow, docOverflow, width: vw, height: vh, offenders: offenderList.slice(0, 8) };
};
`,
    awaitPromise: true,
    returnByValue: true
  });

  const evaluation = await client.send("Runtime.evaluate", {
    expression: script,
    awaitPromise: true,
    returnByValue: true
  });
  const value = evaluation.result?.value || {};
  const screenshot = await client.send("Page.captureScreenshot", { format: "png", fromSurface: true });
  writeFileSync(path.join(outputRoot, scenario.name + ".png"), Buffer.from(screenshot.data, "base64"));
  return { name: scenario.name, ok: !value.errors || value.errors.length === 0, errors: value.errors || [], offenders: value.offenders || [] };
}

main().catch(error => {
  console.error(error.stack || error.message || String(error));
  process.exit(1);
});



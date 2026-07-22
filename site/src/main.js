const repo = "kingkaozydev/smart-background-nap";
const latestReleaseApi = `https://api.github.com/repos/${repo}/releases/latest`;
const commitsApi = `https://api.github.com/repos/${repo}/commits?per_page=5`;
const fallbackDownload = `https://github.com/${repo}/releases/latest/download/SmartBackgroundNap.exe`;

const $ = (id) => document.getElementById(id);
const setText = (id, value) => {
  const el = $(id);
  if (el) el.textContent = value;
};
const setHref = (id, value) => {
  const el = $(id);
  if (el && value) el.href = value;
};

function formatDate(value) {
  if (!value) return "";
  try {
    return new Intl.DateTimeFormat("pt-BR", {
      day: "2-digit",
      month: "short",
      year: "numeric",
    }).format(new Date(value));
  } catch {
    return "";
  }
}

function shortMarkdown(text) {
  return String(text || "")
    .replace(/```[\s\S]*?```/g, "")
    .replace(/[#>*_`\[\]()]/g, "")
    .replace(/\r/g, "")
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .slice(0, 4)
    .join(" · ");
}

function assetDownload(release) {
  const asset = Array.isArray(release.assets)
    ? release.assets.find((item) => item && item.name === "SmartBackgroundNap.exe")
    : null;
  return asset?.browser_download_url || fallbackDownload;
}

async function loadRelease() {
  try {
    const response = await fetch(latestReleaseApi, {
      headers: { Accept: "application/vnd.github+json" },
    });
    if (!response.ok) throw new Error(`GitHub respondeu ${response.status}`);
    const release = await response.json();
    const version = release.tag_name || release.name || "latest";
    const download = assetDownload(release);
    const date = formatDate(release.published_at || release.created_at);
    setText("releaseVersion", version);
    setText("releaseMeta", date ? `publicada em ${date}` : "release oficial do GitHub");
    setText("releaseBody", shortMarkdown(release.body) || "Download oficial publicado no GitHub Releases.");
    ["navDownload", "heroDownload", "releaseDownload", "finalDownload"].forEach((id) =>
      setHref(id, download)
    );
  } catch {
    setText("releaseVersion", "release/latest");
    setText("releaseMeta", "usando link oficial do GitHub");
    setText(
      "releaseBody",
      "Não foi possível carregar as notas agora, mas o botão continua apontando para a última release oficial do GitHub."
    );
  }
}

async function loadCommits() {
  const list = $("commitList");
  if (!list) return;
  try {
    const response = await fetch(commitsApi, {
      headers: { Accept: "application/vnd.github+json" },
    });
    if (!response.ok) throw new Error(`GitHub respondeu ${response.status}`);
    const commits = await response.json();
    list.innerHTML = "";
    commits.slice(0, 5).forEach((entry) => {
      const li = document.createElement("li");
      const msg = entry?.commit?.message?.split("\n")[0] || "Atualização no repositório";
      const date = formatDate(entry?.commit?.committer?.date);
      li.innerHTML = `${escapeHtml(msg)}${date ? `<small>${date}</small>` : ""}`;
      list.appendChild(li);
    });
  } catch {
    list.innerHTML = "<li>Não foi possível carregar os commits agora. O GitHub continua sendo a fonte oficial.</li>";
  }
}

function escapeHtml(value) {
  return String(value).replace(
    /[&<>'"]/g,
    (char) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" }[char])
  );
}

function animateHeroCounter() {
  const el = $("heroApps");
  if (!el) return;
  let value = 18;
  setInterval(() => {
    value = value >= 42 ? 18 : value + Math.floor(Math.random() * 4) + 1;
    el.textContent = String(value);
  }, 1800);
}

loadRelease();
loadCommits();
animateHeroCounter();

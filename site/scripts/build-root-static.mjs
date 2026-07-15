import { mkdir, rm, writeFile } from "node:fs/promises";
import { readFileSync, existsSync } from "node:fs";
import { basename, extname } from "node:path";

const text = (path) => readFileSync(path, "utf8");
const b64 = (path) => readFileSync(path).toString("base64");

await rm("dist", { recursive: true, force: true });
await mkdir("dist/server", { recursive: true });
await mkdir("dist/_appgen_meta", { recursive: true });
await writeFile("dist/_appgen_meta/appgarden.json", JSON.stringify({ runtime: "worker", entrypoint: "server/index.js" }, null, 2), "utf8");

const html = text("site/index.html");
const css = text("site/src/styles.css");
const js = text("site/src/main.js");
const imageFiles = [
  "site/public/smart-nap-logo.png",
  "site/public/smart-nap-social-preview.png",
  "site/public/smart-nap-about-panel.png"
].filter(existsSync);

const images = Object.fromEntries(imageFiles.map((path) => {
  const ext = extname(path).toLowerCase();
  const type = ext === ".png" ? "image/png" : "application/octet-stream";
  return ["/" + basename(path), { type, data: b64(path) }];
}));

const server = `const HTML = ${JSON.stringify(html)};
const CSS = ${JSON.stringify(css)};
const JS = ${JSON.stringify(js)};
const IMAGES = ${JSON.stringify(images)};

function response(body, type, status = 200) {
  return new Response(body, {
    status,
    headers: {
      "content-type": type,
      "cache-control": type.startsWith("image/") ? "public, max-age=86400" : "public, max-age=300"
    }
  });
}

function decodeBase64(value) {
  const binary = atob(value);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}

export default {
  async fetch(request) {
    const url = new URL(request.url);
    const path = url.pathname === "/" ? "/index.html" : url.pathname;
    if (path === "/index.html") return response(HTML, "text/html; charset=utf-8");
    if (path === "/src/styles.css") return response(CSS, "text/css; charset=utf-8");
    if (path === "/src/main.js") return response(JS, "application/javascript; charset=utf-8");
    if (IMAGES[path]) return response(decodeBase64(IMAGES[path].data), IMAGES[path].type);
    return response(HTML, "text/html; charset=utf-8", 200);
  }
};
`;

await writeFile("dist/server/index.js", server, "utf8");
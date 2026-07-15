import { cp, rm, mkdir } from "node:fs/promises";
import { existsSync } from "node:fs";

await rm("dist", { recursive: true, force: true });
await mkdir("dist", { recursive: true });
await cp("site/index.html", "dist/index.html");
await cp("site/src", "dist/src", { recursive: true });
if (existsSync("site/public")) await cp("site/public", "dist", { recursive: true });
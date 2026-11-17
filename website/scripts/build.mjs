import { cp, mkdir, readdir, rm, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { runChecks } from "./check.mjs";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const dist = path.join(root, "dist");

const checks = await runChecks();
await rm(dist, { recursive: true, force: true });
await mkdir(dist, { recursive: true });

for (const entry of ["index.html", "mod.json", "src", "assets"]) {
  await cp(path.join(root, entry), path.join(dist, entry), { recursive: true });
}
await writeFile(path.join(dist, ".nojekyll"), "", "utf8");

const files = [];
async function collect(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const absolute = path.join(directory, entry.name);
    if (entry.isDirectory()) await collect(absolute);
    else files.push({ path: path.relative(dist, absolute), bytes: (await stat(absolute)).size });
  }
}
await collect(dist);

console.log(JSON.stringify({
  status: "PASS",
  output: dist,
  checks,
  fileCount: files.length,
  totalBytes: files.reduce((total, file) => total + file.bytes, 0),
  files
}, null, 2));

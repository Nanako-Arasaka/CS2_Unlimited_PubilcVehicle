import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { copy } from "../src/copy.js";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

function structure(value) {
  if (Array.isArray(value)) {
    return `[${value.length}:${value.map(structure).join("|")}]`;
  }
  if (value && typeof value === "object") {
    return `{${Object.keys(value).sort().map((key) => `${key}:${structure(value[key])}`).join("|")}}`;
  }
  return typeof value;
}

export async function runChecks() {
  const data = JSON.parse(await readFile(path.join(root, "mod.json"), "utf8"));
  const main = await readFile(path.join(root, "src/main.js"), "utf8");
  const css = await readFile(path.join(root, "src/styles.css"), "utf8");
  const font = await readFile(path.join(root, "assets/source-sans-3-latin.woff2"));

  assert.deepEqual(Object.keys(copy).sort(), ["en", "zh"], "Only Chinese and English copy tables are expected");
  assert.equal(structure(copy.zh), structure(copy.en), "Chinese and English copy must have identical structure");
  assert.ok(["preview", "beta", "stable"].includes(data.status), "Unknown mod status");
  assert.match(data.distribution.github, /^https:\/\/github\.com\//, "A valid source repository is required");
  if (data.distribution.paradoxMods) {
    assert.match(data.distribution.paradoxMods, /^https:\/\/mods\.paradoxplaza\.com\//, "The Paradox Mods URL must use the official host");
  }

  if (data.status === "stable") {
    assert.ok(data.validation.items.every((item) => item.state === "done"), "Stable status requires every validation item to be done");
  }

  for (const item of data.validation.items) {
    assert.ok(["done", "pending"].includes(item.state), `Unknown validation state: ${item.state}`);
    assert.equal(typeof copy.zh.validation.items[item.id], "string", `Missing Chinese validation copy: ${item.id}`);
    assert.equal(typeof copy.en.validation.items[item.id], "string", `Missing English validation copy: ${item.id}`);
  }

  for (const setting of data.settings) {
    assert.ok(copy.zh.settings.items[setting.key], `Missing Chinese setting copy: ${setting.key}`);
    assert.ok(copy.en.settings.items[setting.key], `Missing English setting copy: ${setting.key}`);
  }

  for (const transport of data.supports) {
    assert.ok(copy.zh.transport[transport], `Missing Chinese transport copy: ${transport}`);
    assert.ok(copy.en.transport[transport], `Missing English transport copy: ${transport}`);
  }

  assert.equal((main.match(/btn btn--primary/g) || []).length, 1, "The page must contain exactly one primary action");
  assert.ok(!/(box-shadow|backdrop-filter|linear-gradient|radial-gradient)\s*:/.test(css), "Forbidden visual effects found");

  const cssWithoutTokens = css.replace(/:root\s*\{[\s\S]*?\}/, "");
  assert.ok(!/#[0-9a-fA-F]{3,8}\b/.test(cssWithoutTokens), "Raw hex colors must remain inside the token layer");
  assert.ok(font.length > 10000, "The self-hosted font is missing or truncated");

  return {
    languages: Object.keys(copy).length,
    settings: data.settings.length,
    validationItems: data.validation.items.length,
    transportTypes: data.supports.length,
    fontBytes: font.length
  };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = await runChecks();
  console.log(JSON.stringify({ status: "PASS", ...result }, null, 2));
}

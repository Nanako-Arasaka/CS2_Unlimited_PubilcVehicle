import { spawn } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const output = path.join(root, ".validation");
const chromePath = process.env.CHROME_PATH || "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const profile = path.join(output, "chrome-cdp");
await mkdir(output, { recursive: true });

const chrome = spawn(chromePath, [
  "--headless=new",
  "--disable-gpu",
  "--no-first-run",
  "--remote-debugging-port=0",
  `--user-data-dir=${profile}`,
  "about:blank"
], { stdio: ["ignore", "ignore", "pipe"] });

let socket;
try {
  const browserWs = await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error("Chrome DevTools startup timed out")), 15000);
    chrome.stderr.on("data", (chunk) => {
      const match = chunk.toString().match(/DevTools listening on (ws:\/\/\S+)/);
      if (match) {
        clearTimeout(timer);
        resolve(match[1]);
      }
    });
    chrome.once("error", reject);
  });

  socket = new WebSocket(browserWs);
  await new Promise((resolve, reject) => {
    socket.addEventListener("open", resolve, { once: true });
    socket.addEventListener("error", reject, { once: true });
  });

  let sequence = 0;
  const pending = new Map();
  socket.addEventListener("message", (event) => {
    const message = JSON.parse(event.data);
    if (!message.id || !pending.has(message.id)) return;
    const { resolve, reject } = pending.get(message.id);
    pending.delete(message.id);
    if (message.error) reject(new Error(JSON.stringify(message.error)));
    else resolve(message.result);
  });

  function send(method, params = {}, sessionId) {
    const id = ++sequence;
    return new Promise((resolve, reject) => {
      pending.set(id, { resolve, reject });
      socket.send(JSON.stringify({ id, method, params, ...(sessionId ? { sessionId } : {}) }));
    });
  }

  const { targetId } = await send("Target.createTarget", { url: "about:blank" });
  const { sessionId } = await send("Target.attachToTarget", { targetId, flatten: true });
  await send("Page.enable", {}, sessionId);
  await send("Runtime.enable", {}, sessionId);

  async function evaluate(expression) {
    const result = await send("Runtime.evaluate", {
      expression,
      awaitPromise: true,
      returnByValue: true
    }, sessionId);
    if (result.exceptionDetails) throw new Error(JSON.stringify(result.exceptionDetails));
    return result.result.value;
  }

  async function setViewport(width, height) {
    await send("Emulation.setDeviceMetricsOverride", {
      width,
      height,
      deviceScaleFactor: 1,
      mobile: width < 640
    }, sessionId);
  }

  async function capture(name) {
    const screenshot = await send("Page.captureScreenshot", {
      format: "png",
      fromSurface: true,
      captureBeyondViewport: false
    }, sessionId);
    await writeFile(path.join(output, name), Buffer.from(screenshot.data, "base64"));
  }

  const measureExpression = `(() => {
    const overflowing = [...document.querySelectorAll("body *")].filter((element) => {
      const style = getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      return style.display !== "none" && rect.width > 0 && (rect.right > innerWidth + 1 || rect.left < -1);
    }).map((element) => ({
      tag: element.tagName,
      className: typeof element.className === "string" ? element.className : "svg",
      left: Math.round(element.getBoundingClientRect().left),
      right: Math.round(element.getBoundingClientRect().right)
    })).slice(0, 20);
    const primary = document.querySelector(".btn--primary");
    const specValue = document.querySelector(".spec__val");
    return {
      language: document.documentElement.lang,
      title: document.querySelector("h1")?.textContent,
      innerWidth,
      clientWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
      bodyScrollWidth: document.body.scrollWidth,
      primaryButtons: document.querySelectorAll(".btn--primary").length,
      primaryHeight: primary?.getBoundingClientRect().height,
      primaryRadius: primary ? getComputedStyle(primary).borderRadius : null,
      specValueAlign: specValue ? getComputedStyle(specValue).textAlign : null,
      specValueSize: specValue ? getComputedStyle(specValue).fontSize : null,
      validationItems: document.querySelectorAll(".validation-item").length,
      settingsRows: document.querySelectorAll(".setting-row:not(.setting-row--head)").length,
      overflowing
    };
  })()`;

  await setViewport(1440, 1000);
  await send("Page.navigate", { url: "http://127.0.0.1:4173/" }, sessionId);
  await evaluate(`new Promise((resolve) => {
    if (document.querySelector(".detail")) { resolve(true); return; }
    const observer = new MutationObserver(() => {
      if (document.querySelector(".detail")) { observer.disconnect(); resolve(true); }
    });
    observer.observe(document.documentElement, { childList: true, subtree: true });
  })`);

  const desktop = await evaluate(measureExpression);
  await capture("desktop-cdp.png");

  const english = await evaluate(`(() => {
    document.querySelector('[data-language="en"]').click();
    document.querySelector(".details-list summary").click();
    return {
      language: document.documentElement.lang,
      title: document.querySelector("h1").textContent,
      faqOpen: document.querySelector(".details-list details").open,
      faqQuestion: document.querySelector(".details-list summary span").textContent,
      primaryLabel: document.querySelector(".btn--primary span").textContent
    };
  })()`);

  const copyRect = await evaluate(`(async () => {
    const button = document.querySelector(".copy-command");
    button.scrollIntoView({ block: "center", behavior: "instant" });
    await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
    const rect = button.getBoundingClientRect();
    return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
  })()`);
  for (const type of ["mouseMoved", "mousePressed", "mouseReleased"]) {
    await send("Input.dispatchMouseEvent", {
      type,
      x: copyRect.x,
      y: copyRect.y,
      button: type === "mouseMoved" ? "none" : "left",
      clickCount: type === "mouseMoved" ? 0 : 1
    }, sessionId);
  }
  const copyInteraction = await evaluate(`(async () => {
    await new Promise((resolve) => setTimeout(resolve, 100));
    return {
      feedback: document.querySelector(".copy-feedback").textContent,
      hasCommand: document.querySelector(".command-block code").textContent.includes("build.ps1")
    };
  })()`);

  await send("Emulation.setEmulatedMedia", {
    features: [{ name: "prefers-reduced-motion", value: "reduce" }]
  }, sessionId);
  const reducedMotion = await evaluate(`({
    scrollBehavior: getComputedStyle(document.documentElement).scrollBehavior,
    transitionDuration: getComputedStyle(document.querySelector(".btn--primary")).transitionDuration
  })`);
  await send("Emulation.setEmulatedMedia", { features: [] }, sessionId);

  await evaluate(`document.querySelector('[data-language="zh"]').click(); true`);
  await setViewport(375, 1000);
  const mobile = await evaluate(measureExpression);
  await capture("mobile-cdp.png");

  const report = { status: "PASS", desktop, english, copyInteraction, reducedMotion, mobile };
  const checks = {
    desktopOverflow: desktop.scrollWidth <= desktop.innerWidth,
    mobileOverflow: mobile.scrollWidth <= mobile.innerWidth && mobile.overflowing.length === 0,
    singlePrimary: desktop.primaryButtons === 1 && mobile.primaryButtons === 1,
    specValues: desktop.specValueAlign === "left" && mobile.specValueSize === "18px",
    englishSwitch: english.language === "en" && english.primaryLabel === "View source",
    copyCommand: copyInteraction.feedback === "Copied" && copyInteraction.hasCommand,
    reducedMotion: reducedMotion.scrollBehavior === "auto" && reducedMotion.transitionDuration === "0s"
  };
  report.checks = checks;
  if (Object.values(checks).some((value) => !value)) report.status = "FAIL";
  await writeFile(path.join(output, "browser-report.json"), JSON.stringify(report, null, 2), "utf8");
  console.log(JSON.stringify(report, null, 2));
  await send("Browser.close");
} finally {
  if (socket) socket.close();
  if (!chrome.killed) chrome.kill();
}

"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { spawn } = require("node:child_process");
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
class CDP {
  constructor(url) {
    this.ws = new WebSocket(url); this.sequence = 0; this.pending = new Map(); this.exceptions = [];
    this.ready = new Promise((resolve, reject) => { this.ws.addEventListener("open", resolve, { once: true }); this.ws.addEventListener("error", reject, { once: true }); });
    this.ws.addEventListener("message", event => {
      const data = JSON.parse(event.data);
      if (data.method === "Runtime.exceptionThrown") this.exceptions.push(data.params.exceptionDetails.text + ": " + (data.params.exceptionDetails.exception?.description || ""));
      if (data.id && this.pending.has(data.id)) {
        const job = this.pending.get(data.id); this.pending.delete(data.id); clearTimeout(job.timer);
        data.error ? job.reject(Error(data.error.message)) : job.resolve(data.result);
      }
    });
  }
  async send(method, params = {}) {
    await this.ready;
    const id = ++this.sequence;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => { this.pending.delete(id); reject(Error("CDP timeout " + method)); }, 15000);
      this.pending.set(id, { resolve, reject, timer });
      this.ws.send(JSON.stringify({ id, method, params }));
    });
  }
  async eval(expression) {
    const result = await this.send("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true, userGesture: true });
    if (result.exceptionDetails) throw Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
    return result.result.value;
  }
  async wait(expression, label) {
    for (let i = 0; i < 100; i++) {
      try { if (await this.eval(expression)) return; } catch { /* navigation replaces context */ }
      await pause(100);
    }
    throw Error("Browser condition not reached: " + label);
  }
  async size(width) { await this.send("Emulation.setDeviceMetricsOverride", { width, height: 900, deviceScaleFactor: 1, mobile: false }); }
  async shot(file) {
    const data = await this.send("Page.captureScreenshot", { format: "png", captureBeyondViewport: false });
    fs.writeFileSync(file, Buffer.from(data.data, "base64"));
  }
  async navigate(url) { await this.send("Page.navigate", { url }); }
}
async function launch(report) {
  const candidates = [process.env.EDGE_EXECUTABLE,
    path.join(process.env["ProgramFiles(x86)"] || "C:\\Program Files (x86)", "Microsoft/Edge/Application/msedge.exe"),
    path.join(process.env.ProgramFiles || "C:\\Program Files", "Microsoft/Edge/Application/msedge.exe")];
  const executable = candidates.find(file => file && fs.existsSync(file));
  assert.ok(executable, "Microsoft Edge executable not found");
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), "stackmeet-edge-"));
  const child = spawn(executable, ["--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check", "--disable-background-networking", "--disable-extensions", "--disable-component-update", "--disable-sync", "--remote-debugging-port=0", "--user-data-dir=" + profile, "about:blank"], { stdio: "ignore", windowsHide: true });
  let cdp;
  const close = () => {
    if (cdp) cdp.ws.close();
    child.kill();
    try { fs.rmSync(profile, { recursive: true, force: true, maxRetries: 3, retryDelay: 200 }); }
    catch { report.findings.push({ type: "profile-cleanup", detail: "Disposable profile remained locked" }); }
  };
  try {
    let launchError;
    child.on("error", error => { launchError = error; });
    const portFile = path.join(profile, "DevToolsActivePort");
    for (let i = 0; i < 150 && !fs.existsSync(portFile); i++) {
      if (launchError) throw launchError;
      await pause(100);
    }
    assert.ok(fs.existsSync(portFile), "Edge did not expose its isolated DevTools port");
    const port = fs.readFileSync(portFile, "utf8").split(/\r?\n/)[0];
    const endpoint = "http://127.0.0.1:" + port;
    report.browser = await (await fetch(endpoint + "/json/version")).json();
    delete report.browser.webSocketDebuggerUrl;
    const tabs = await (await fetch(endpoint + "/json/list")).json();
    cdp = new CDP(tabs.find(tab => tab.type === "page").webSocketDebuggerUrl);
    await cdp.send("Page.enable"); await cdp.send("Runtime.enable"); await cdp.send("Network.enable");
    await cdp.send("Network.setBlockedURLs", { urls: ["https://*"] });
    return { cdp, close };
  } catch (error) { close(); throw error; }
}
module.exports = { launch, pause };

// 管理画面をヘッドレス Chrome で操作して画面を撮る
//
//   node capture_server.mjs <steps.json> [width] [height]
//
// steps: [{ "go": url, "waitMs"?: n }, { "wait": ms }, { "click": text, "selector"?: css, "nth"?: n }, { "type": text },
//         { "key": "Enter" }, { "eval": js }, { "shot": file }]
// Chrome の場所は環境変数 CHROME_PATH、DevTools のポートは CDP_PORT (既定 9334) で変えられる
import { spawn } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const [stepsFile, widthArg, heightArg] = process.argv.slice(2);
if (!stepsFile) {
  console.error("usage: node capture_server.mjs <steps.json> [width] [height]");
  process.exit(2);
}

const steps = JSON.parse(readFileSync(stepsFile, "utf8"));
const width = Number(widthArg ?? 1400);
const height = Number(heightArg ?? 900);
const port = Number(process.env.CDP_PORT ?? 9334);
const chromePath = process.env.CHROME_PATH ?? {
  win32: "C:/Program Files/Google/Chrome/Application/chrome.exe",
  darwin: "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
}[process.platform] ?? "google-chrome";

// 前回のログインの Cookie を残さないように、毎回新しいプロファイルで起動する
const profile = mkdtempSync(join(tmpdir(), "pos-readme-chrome-"));
const chrome = spawn(chromePath, [
  "--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run", "--no-default-browser-check",
  `--remote-debugging-port=${port}`, `--window-size=${width},${height}`, `--user-data-dir=${profile}`,
  "about:blank",
], { stdio: "ignore" });

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function main() {
  let targets = [];
  for (let i = 0; i < 50 && targets.length === 0; i++) {
    try {
      targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
    } catch {
      // 起動を待つ
    }
    if (targets.length === 0) {
      await sleep(200);
    }
  }

  const page = targets.find((t) => t.type === "page");
  if (!page) {
    throw new Error(`Chrome に接続できません (${chromePath}、ポート ${port})`);
  }

  const ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    ws.onopen = resolve;
    ws.onerror = reject;
  });
  let id = 0;
  const pending = new Map();
  ws.onmessage = (event) => {
    const message = JSON.parse(event.data);
    if (message.id && pending.has(message.id)) {
      pending.get(message.id)(message);
      pending.delete(message.id);
    }
  };
  const send = (method, params = {}) => new Promise((resolve) => {
    const current = ++id;
    pending.set(current, resolve);
    ws.send(JSON.stringify({ id: current, method, params }));
  });
  const evaluate = async (expression) => {
    const response = await send("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
    if (response.result?.exceptionDetails) {
      throw new Error(JSON.stringify(response.result.exceptionDetails));
    }
    return response.result?.result?.value;
  };

  await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: false });
  await send("Page.enable");
  await send("Runtime.enable");

  for (const step of steps) {
    if (step.go) {
      await send("Page.navigate", { url: step.go });
      await sleep(step.waitMs ?? 3000);
    } else if (step.wait) {
      await sleep(step.wait);
    } else if (step.click !== undefined) {
      // 表示中の要素から文言で探し、中心を実際のマウスで押す (Blazor のイベントを通すため)
      const selector = step.selector ?? "button, a, [role=button], li, td, .mud-list-item, .mud-nav-link, input, label";
      const rect = await evaluate(`(() => {
        const text = ${JSON.stringify(step.click)};
        const all = [...document.querySelectorAll(${JSON.stringify(selector)})]
          .filter(e => { const r = e.getBoundingClientRect(); return r.width > 0 && r.height > 0; })
          .filter(e => text === "" || (e.innerText ?? "").trim().includes(text) || (e.getAttribute("aria-label") ?? "").includes(text) || (e.getAttribute("placeholder") ?? "").includes(text));
        const e = all[${step.nth ?? 0}];
        if (!e) return null;
        e.scrollIntoView({ block: "center" });
        const r = e.getBoundingClientRect();
        return { x: r.x + r.width / 2, y: r.y + r.height / 2 };
      })()`);
      if (!rect) {
        throw new Error(`見つかりません: ${step.click} (${selector})`);
      }
      for (const type of ["mouseMoved", "mousePressed", "mouseReleased"]) {
        await send("Input.dispatchMouseEvent", { type, x: rect.x, y: rect.y, button: "left", clickCount: 1 });
      }
      await sleep(step.waitMs ?? 800);
    } else if (step.type !== undefined) {
      await send("Input.insertText", { text: step.type });
      await sleep(step.waitMs ?? 600);
    } else if (step.key) {
      const code = { Enter: 13, Tab: 9, Escape: 27, ArrowDown: 40, Backspace: 8 }[step.key] ?? 0;
      await send("Input.dispatchKeyEvent", { type: "keyDown", key: step.key, windowsVirtualKeyCode: code, code: step.key });
      await send("Input.dispatchKeyEvent", { type: "keyUp", key: step.key, windowsVirtualKeyCode: code, code: step.key });
      await sleep(step.waitMs ?? 500);
    } else if (step.eval) {
      console.log(JSON.stringify(await evaluate(step.eval)));
    } else if (step.shot) {
      const shot = await send("Page.captureScreenshot", { format: "png" });
      writeFileSync(step.shot, Buffer.from(shot.result.data, "base64"));
      console.log(`saved ${step.shot}`);
    }
  }

  ws.close();
}

try {
  await main();
} catch (e) {
  console.error(e.message);
  process.exitCode = 1;
} finally {
  chrome.kill();
  await sleep(500);
  try {
    rmSync(profile, { recursive: true, force: true });
  } catch {
    // Chrome が掴んでいる間は消せないことがある (一時フォルダなので残してよい)
  }
}

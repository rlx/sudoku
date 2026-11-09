// Takes screenshots of the web game at phone/desktop sizes using headless Chrome.
// Usage: node tests/web/screenshots.mjs <base-url> <out-dir>
import { spawn } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';

const [base = 'http://127.0.0.1:8765/', out = '/tmp/sudoku-shots'] = process.argv.slice(2);
mkdirSync(out, { recursive: true });

const CHROME = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';
const PORT = 9333;
const chrome = spawn(CHROME, ['--headless=new', `--remote-debugging-port=${PORT}`,
  `--user-data-dir=/tmp/sudoku-chrome-${Date.now()}`, '--no-first-run', 'about:blank'], { stdio: 'ignore' });

const sleep = ms => new Promise(r => setTimeout(r, ms));
const UA = {
  iphone: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1',
  android: 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Mobile Safari/537.36',
  desktop: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36',
};

async function connect() {
  for (let i = 0; i < 50; i++) {
    try {
      const res = await fetch(`http://127.0.0.1:${PORT}/json/new?about:blank`, { method: 'PUT' });
      return (await res.json()).webSocketDebuggerUrl;
    } catch { await sleep(200); }
  }
  throw new Error('Chrome did not start');
}

let ws, nextId = 1;
const pending = new Map();
function send(method, params = {}) {
  const id = nextId++;
  ws.send(JSON.stringify({ id, method, params }));
  return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
}
async function evaluate(expr) {
  const r = await send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description || 'eval failed');
  return r.result.value;
}
async function shot(name) {
  await sleep(150);
  const { data } = await send('Page.captureScreenshot', { format: 'png' });
  writeFileSync(`${out}/${name}.png`, Buffer.from(data, 'base64'));
  console.log('saved', name);
}
async function open(device, width, height, mobile, fresh = true) {
  await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: mobile ? 2 : 1, mobile });
  await send('Emulation.setUserAgentOverride', { userAgent: UA[device], platform: device === 'iphone' ? 'iPhone' : '' });
  await send('Emulation.setTouchEmulationEnabled', { enabled: mobile, maxTouchPoints: mobile ? 5 : 0 });
  if (fresh) {
    await send('Page.navigate', { url: base });
    await sleep(500);
    await evaluate('localStorage.clear()');
  }
  await send('Page.navigate', { url: base });
  await sleep(600);
}
const click = sel => evaluate(`document.querySelector(${JSON.stringify(sel)}).click()`);
const errors = [];

ws = new WebSocket(await connect());
await new Promise(r => ws.addEventListener('open', r));
ws.addEventListener('message', ev => {
  const msg = JSON.parse(ev.data);
  if (msg.id && pending.has(msg.id)) { pending.get(msg.id).resolve(msg.result || msg); pending.delete(msg.id); }
  if (msg.method === 'Runtime.exceptionThrown') errors.push(msg.params.exceptionDetails.exception?.description);
});
await send('Page.enable');
await send('Runtime.enable');

try {
  // iPhone: first visit shows install help, then play.
  await open('iphone', 393, 852, true);
  await shot('1-iphone-install');
  await click('#install-done');
  await shot('2-iphone-menu');
  await click('[data-level="easy"]');
  // pick the first empty square, enter a wrong number, then select a given
  await evaluate(`(() => {
    const s = JSON.parse(localStorage.getItem('bigSudoku.save'));
    const i = s.givens.indexOf(0);
    document.querySelectorAll('.cell')[i].click();
    document.querySelector('[data-num="' + (s.solution[i] % 9 + 1) + '"]').click();
    const j = s.givens.indexOf(0, i + 1);
    document.querySelectorAll('.cell')[j].click();
    document.querySelector('[data-num="' + s.solution[j] + '"]').click();
  })()`);
  await shot('3-iphone-game');

  // Reload keeps the game
  await send('Page.navigate', { url: base });
  await sleep(600);
  const resumed = await evaluate(`!document.querySelector('#game').hidden`);
  console.log('resumed after reload:', resumed);
  if (!resumed) errors.push('game not resumed after reload');

  // Solve it
  await evaluate(`(() => {
    const s = JSON.parse(localStorage.getItem('bigSudoku.save'));
    const cells = document.querySelectorAll('.cell');
    s.givens.forEach((g, i) => {
      if (g) return;
      cells[i].click();
      document.querySelector('[data-num="' + s.solution[i] + '"]').click();
    });
  })()`);
  await shot('4-iphone-solved');
  const cleared = await evaluate(`localStorage.getItem('bigSudoku.save') === null`);
  if (!cleared) errors.push('save not cleared after solving');

  // Small phone
  await open('iphone', 320, 568, true, false);
  await click('[data-level="medium"]');
  await shot('5-small-phone-game');

  // Landscape phone
  await open('iphone', 852, 393, true, false);
  await click('#continue');
  await shot('6-phone-landscape');

  // Android first visit
  await open('android', 412, 915, true);
  await shot('7-android-install');
  await send('Page.navigate', { url: base + '?app=android' });
  await sleep(600);
  await shot('8-android-app-menu');
  await click('[data-level="hard"]');
  await shot('9-android-app-game');

  // Desktop browser
  await open('desktop', 1366, 768, false);
  await shot('10-desktop-menu');
  await click('[data-level="easy"]');
  await shot('11-desktop-game');
} finally {
  console.log(errors.length ? 'PAGE ERRORS:\n' + errors.join('\n') : 'no page errors');
  ws.close();
  chrome.kill();
  process.exit(errors.length ? 1 : 0);
}

import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { createReadStream } from 'node:fs';
import { readFile, realpath, stat, writeFile } from 'node:fs/promises';
import { extname, join, relative, resolve, sep } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

const [browserPath, sitePath, evidence, tag, commit, archiveSha256] = process.argv.slice(2);
const site = await realpath(sitePath);
const contentTypes = {
    '.css': 'text/css', '.html': 'text/html; charset=utf-8', '.js': 'text/javascript',
    '.json': 'application/json', '.png': 'image/png', '.svg': 'image/svg+xml',
    '.woff': 'font/woff', '.woff2': 'font/woff2'
};
const responses = new Map();
const server = createServer(async (request, response) => {
    try {
        const url = new URL(request.url, origin);
        const pathname = decodeURIComponent(url.pathname);
        if (request.method !== 'GET' || url.origin !== origin) {
            response.writeHead(405).end();
            return;
        }
        const path = await realpath(resolve(site, '.' + (pathname === '/' ? '/index.html' : pathname)));
        const within = relative(site, path);
        if (!within || within === '..' || within.startsWith('..' + sep) || resolve(site, within) !== path)
            throw new Error('Outside the documentation bundle');
        const file = await stat(path);
        if (!file.isFile() || file.size > 32 * 1024 * 1024) throw new Error('Invalid asset');
        responses.set(pathname, 200);
        response.writeHead(200, {
            'Content-Type': contentTypes[extname(path)] ?? 'application/octet-stream',
            'Content-Length': file.size, 'Cache-Control': 'no-store'
        });
        createReadStream(path).on('error', () => response.destroy()).pipe(response);
    } catch {
        response.writeHead(404).end();
    }
});
await new Promise((accept, reject) => server.once('error', reject).listen(0, '127.0.0.1', accept));
const origin = 'http://127.0.0.1:' + server.address().port;
const profile = join(evidence, 'browser-profile');
const browser = spawn(browserPath, [
    '--headless', '--no-first-run', '--no-default-browser-check', '--disable-background-networking',
    '--disable-component-update', '--disable-extensions', '--remote-debugging-port=0',
    '--remote-debugging-address=127.0.0.1', '--user-data-dir=' + profile, 'about:blank'
], { windowsHide: true, stdio: 'ignore' });

let browserFailure;
browser.once('error', error => { browserFailure = error; });
let socket;
let sequence = 0;
const pending = new Map();
const pages = [];

async function waitFor(read, reason, seconds = 15) {
    const expires = Date.now() + seconds * 1000;
    while (Date.now() < expires) {
        if (browserFailure) throw browserFailure;
        if (browser.exitCode !== null) throw new Error('Headless browser exited early');
        const value = await read();
        if (value) return value;
        await delay(100);
    }
    throw new Error('Timed out waiting for ' + reason);
}

function send(method, params = {}) {
    return new Promise((accept, reject) => {
        const id = ++sequence;
        const timeout = setTimeout(() => {
            pending.delete(id);
            reject(new Error('Browser command timed out: ' + method));
        }, 10000);
        pending.set(id, message => {
            clearTimeout(timeout);
            if (message.error) reject(new Error(method + ': ' + message.error.message));
            else accept(message.result);
        });
        socket.send(JSON.stringify({ id, method, params }));
    });
}

async function evaluate(expression) {
    const response = await send('Runtime.evaluate', { expression, returnByValue: true });
    if (response.exceptionDetails) throw new Error('Browser evaluation failed');
    return response.result.value;
}

async function capture(page, heading) {
    const article = await waitFor(() => evaluate(
        '(() => { const h = document.querySelector("article h1"); const b = h?.getBoundingClientRect(); ' +
        'return document.readyState === "complete" && h && b.width > 0 && b.height > 0 && ' +
        'location.pathname === ' + JSON.stringify(page) + ' ? h.textContent.trim() : null; })()'
    ), page);
    if (!heading.test(article)) throw new Error('Unexpected article at ' + page);
    const assetsReady = await evaluate(
        'document.querySelector("#logo")?.naturalWidth > 0 && [...document.styleSheets]' +
        '.some(s => s.href?.endsWith("/public/docfx.min.css") && s.cssRules.length > 0)'
    );
    if (!assetsReady) throw new Error('Stylesheet or logo did not render at ' + page);

    const { data } = await send('Page.captureScreenshot', { format: 'png' });
    const png = Buffer.from(data, 'base64');
    if (png.length < 4096 || png.length > 8 * 1024 * 1024) throw new Error('Unexpected screenshot at ' + page);
    const name = 'page-' + (pages.length + 1) + '.png';
    await writeFile(join(evidence, name), png, { flag: 'wx' });
    pages.push({ path: page, heading: article, screenshot: name });
}

async function follow(href) {
    if (!await evaluate('(() => { const a = [...document.querySelectorAll("a")].find(x => x.getAttribute("href") === ' +
        JSON.stringify(href) + '); if (!a) return false; a.click(); return true; })()'))
        throw new Error('Missing rendered navigation link ' + href);
}

try {
    const port = await waitFor(async () => {
        try { return Number((await readFile(join(profile, 'DevToolsActivePort'), 'utf8')).split('\n')[0]) || null; }
        catch { return null; }
    }, 'the isolated browser endpoint');
    const targets = await (await fetch('http://127.0.0.1:' + port + '/json/list', { signal: AbortSignal.timeout(5000) })).json();
    const target = targets.find(value => value.type === 'page' && value.url === 'about:blank');
    if (!target) throw new Error('Browser did not start an isolated page');
    socket = new WebSocket('ws://127.0.0.1:' + port + '/devtools/page/' + encodeURIComponent(target.id));
    await new Promise((accept, reject) => {
        const timer = setTimeout(() => reject(new Error('Browser connection timed out')), 5000);
        socket.addEventListener('open', () => { clearTimeout(timer); accept(); }, { once: true });
        socket.addEventListener('error', () => { clearTimeout(timer); reject(new Error('Browser connection failed')); }, { once: true });
    });
    socket.addEventListener('message', event => {
        const message = JSON.parse(event.data);
        if (pending.has(message.id)) {
            const complete = pending.get(message.id);
            pending.delete(message.id);
            complete(message);
        }
        if (message.method === 'Fetch.requestPaused') {
            const { requestId, request } = message.params;
            const allowed = new URL(request.url).origin === origin;
            send(allowed ? 'Fetch.continueRequest' : 'Fetch.failRequest',
                allowed ? { requestId } : { requestId, errorReason: 'BlockedByClient' })
                .catch(error => { browserFailure = error; });
        }
    });
    await send('Fetch.enable');
    const { product } = await send('Browser.getVersion');
    await send('Page.navigate', { url: origin + '/index.html' });
    await capture('/index.html', /ModernFormsNext documentation/i);
    await follow('content/docs/getting-started.html');
    await capture('/content/docs/getting-started.html', /Getting Started with ModernFormsNext/i);
    await follow('../../index.html');
    await capture('/index.html', /ModernFormsNext documentation/i);
    await follow('api/index.html');
    await capture('/api/index.html', /API reference/i);
    await send('Page.navigate', { url: origin + '/api/ModernFormsNext.NavigationPage.html' });
    await capture('/api/ModernFormsNext.NavigationPage.html', /NavigationPage/i);
    for (const path of ['/public/docfx.min.css', '/public/docfx.min.js', '/logo.svg']) {
        if (responses.get(path) !== 200) throw new Error('Missing loaded asset ' + path);
    }
    await writeFile(join(evidence, 'browser.json'),
        JSON.stringify({ schema: 1, status: 'passed', tag, commit, archiveSha256, browser: product, pages }, null, 2),
        { flag: 'wx' });
} finally {
    socket?.close();
    browser.kill();
    server.closeAllConnections();
    server.close();
}

"use strict";

const $ = (id) => document.getElementById(id);
const form = $("form");
const power = $("power");
const hint = $("hint");
const message = $("message");

const TOKEN_KEY = "shutdown-token";
const INSTALL_KEY = "shutdown-install-dismissed";
const LIVE_EVERY_MS = 5000;

let token = null;
let paired = false;
let scheduledAt = null;
let armTimer = null;
let liveTimer = null;
let lastPc = null;
let stopInstallGuide = () => {};

// --- Storage --------------------------------------------------------------
// The token lives in the address (#k=...) so a home screen shortcut carries it along.
// iPhone home screen apps don't share storage with Safari, so localStorage alone would lose it.

function storageGet(key) {
  try { return localStorage.getItem(key); } catch { return null; }
}

function storageSet(key, value) {
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch { /* private mode: the address still has the token */ }
}

function setToken(value) {
  token = value;
  storageSet(TOKEN_KEY, value);
  history.replaceState(null, "", location.pathname + location.search + (value ? `#k=${value}` : ""));
}

// --- Helpers --------------------------------------------------------------

function say(text, kind = "") {
  message.textContent = text;
  message.className = "message " + kind;
}

async function call(path, body) {
  const headers = {};
  if (token) headers["X-Device-Token"] = token;
  if (body !== undefined) headers["Content-Type"] = "application/json";

  const response = await fetch(path, {
    method: body === undefined ? "GET" : "POST",
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(data.error || `Request failed (${response.status})`);
    error.needsPairing = data.needsPairing === true;
    throw error;
  }
  return data;
}

function deviceName() {
  const ua = navigator.userAgent;
  if (/iPhone/.test(ua)) return "iPhone";
  if (/iPad/.test(ua)) return "iPad";
  const model = ua.match(/Android[^;)]*;\s*([^;)]+?)(?:\s+Build|\))/);
  if (model && model[1] && model[1].trim() !== "K") return model[1].trim();
  if (/Android/.test(ua)) return "Android phone";
  return "Browser";
}

function isOnHomeScreen() {
  return navigator.standalone === true || matchMedia("(display-mode: standalone)").matches;
}

function el(tag, text, className) {
  const node = document.createElement(tag);
  if (text !== undefined) node.textContent = text;
  if (className) node.className = className;
  return node;
}

const fmt = {
  rate(bytes) {
    if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB/s`;
    return `${Math.round(bytes / 1024)} KB/s`;
  },
  duration(seconds) {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    if (h >= 24) return `${Math.floor(h / 24)} d ${h % 24} h`;
    return h > 0 ? `${h} h ${m} min` : `${m} min`;
  },
  // Fits a small stat tile: "12h 2m", "3d 4h", "45m".
  short(seconds) {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    if (h >= 24) return `${Math.floor(h / 24)}d ${h % 24}h`;
    return h > 0 ? `${h}h ${m}m` : `${m}m`;
  },
  clock(seconds) {
    const pad = (n) => String(n).padStart(2, "0");
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
  },
};

// --- Install guide --------------------------------------------------------

// Shown to paired phones until the page runs from the home screen or the user says it's done.
// "?demo=ios&step=2" forces the guide (and freezes a step), to check how it looks on a computer.
function showInstallTip() {
  if (!$("install").hidden) return;
  const query = new URLSearchParams(location.search);
  const demo = query.get("demo");
  if (!demo && (isOnHomeScreen() || storageGet(INSTALL_KEY))) return;

  const step = query.has("step") ? Number(query.get("step")) : undefined;
  stopInstallGuide = InstallGuide.render($("install"), { platform: demo || undefined, step });
  $("install").hidden = false;
}

// --- Status ---------------------------------------------------------------

function render(status) {
  $("machine").textContent = status.machineName;
  paired = status.paired;
  const canUse = status.paired || status.pinAvailable;
  form.hidden = !canUse;
  $("unpaired").hidden = canUse;
  $("pin-row").hidden = status.paired || !status.pinAvailable;
  for (const id of ["live", "watch", "shortcuts"]) $(id).hidden = !status.paired;
  setScheduled(status.scheduledShutdownAt);
}

function setScheduled(value) {
  scheduledAt = value ? new Date(value) : null;
  $("pending").hidden = !scheduledAt;
  tick();
}

function tick() {
  if (!scheduledAt) return;
  $("countdown").textContent = fmt.clock(Math.max(0, Math.round((scheduledAt - Date.now()) / 1000)));
}

async function refresh() {
  try {
    const status = await call("/api/status");
    if (token && !status.paired) {
      // The phone was removed on the PC.
      setToken(null);
      say("This phone was unpaired on the PC.", "error");
    }
    render(status);
    if (status.paired) refreshLive();
    return status;
  } catch {
    say("Can't reach the PC. Is it on, and is Shutdown Server running?", "error");
    return null;
  }
}

// --- Live view ------------------------------------------------------------

async function refreshLive() {
  if (!paired || document.hidden) return;
  try {
    const pc = await call("/api/pc");
    lastPc = pc;
    renderLive(pc);
  } catch (error) {
    if (error.needsPairing) refresh();
  }
}

function renderLive(pc) {
  $("cpu").textContent = `${pc.cpuPercent}%`;
  $("memory").textContent = `${pc.memoryPercent}%`;
  $("down").textContent = fmt.rate(pc.downloadBytesPerSecond);
  $("uptime").textContent = fmt.short(pc.uptimeSeconds);
  $("presence").textContent = pc.idleSeconds < 60
    ? "Someone is using it right now"
    : `Nobody has touched it for ${fmt.duration(pc.idleSeconds)}`;

  // Unsaved work
  const warn = $("unsaved-live");
  warn.hidden = pc.unsaved.length === 0;
  warn.textContent = pc.unsaved.length === 1
    ? `${pc.unsaved[0].name} may have unsaved work.`
    : `${pc.unsaved.length} apps may have unsaved work.`;

  // Open apps
  $("apps-summary").textContent = `${pc.apps.length} app${pc.apps.length === 1 ? "" : "s"} open`;
  const unsavedTitles = new Set(pc.unsaved.map((u) => u.title));
  $("apps-list").replaceChildren(...pc.apps.map((app) => {
    const li = el("li");
    li.append(el("b", app.name), el("span", app.title));
    if (unsavedTitles.has(app.title)) li.className = "unsaved";
    return li;
  }));

  $("screen-box").hidden = !pc.screenPreview;

  renderWatch(pc.watch, pc.apps);
  renderRecipes(pc);
}

$("screen-btn").addEventListener("click", async () => {
  const button = $("screen-btn");
  button.disabled = true;
  try {
    const response = await fetch("/api/screenshot", { headers: { "X-Device-Token": token } });
    if (!response.ok) throw new Error((await response.json().catch(() => ({}))).error || "Couldn't load the screen.");
    const img = $("screen");
    if (img.src) URL.revokeObjectURL(img.src);
    img.src = URL.createObjectURL(await response.blob());
    img.hidden = false;
    button.textContent = "Refresh screen";
  } catch (error) {
    say(error.message, "error");
  } finally {
    button.disabled = false;
  }
});

$("screen").addEventListener("click", () => $("screen").classList.toggle("zoomed"));

// --- Shut down when done --------------------------------------------------

const WATCH_EXPLAIN = {
  idle: "When nobody uses the PC and it has nothing left to do (renders, exports, updates).",
  network: "When nobody uses the PC and downloads have stopped.",
  app: "When the app you pick closes, like a game or an installer.",
};

function watchKind() {
  return document.querySelector('input[name="watch-kind"]:checked').value;
}

function updateWatchSetup() {
  const kind = watchKind();
  $("watch-explain").textContent = WATCH_EXPLAIN[kind];
  $("watch-minutes").hidden = kind === "app";
  $("watch-app-row").hidden = kind !== "app";
}

function renderWatch(watch, apps) {
  $("watch-setup").hidden = !!watch;
  $("watch-active").hidden = !watch;

  if (watch) {
    $("watch-desc").textContent = `Shuts down ${watch.description}.`;
    $("watch-progress").textContent = watch.kind === "app"
      ? `${watch.process} is still open`
      : watch.quietSince
        ? `quiet for ${fmt.clock(Math.round((Date.now() - new Date(watch.quietSince)) / 1000))}`
        : "still busy";
    return;
  }

  // Keep the app list up to date without losing the current choice.
  const select = $("watch-app");
  const chosen = select.value;
  const names = [...new Set(apps.map((a) => a.name))].sort((a, b) => a.localeCompare(b));
  select.replaceChildren(...names.map((name) => {
    const option = el("option", name);
    option.value = name;
    return option;
  }));
  if (names.includes(chosen)) select.value = chosen;
}

document.querySelectorAll('input[name="watch-kind"]').forEach((input) => input.addEventListener("change", updateWatchSetup));

$("watch-start").addEventListener("click", async () => {
  const kind = watchKind();
  const minutes = Number(document.querySelector('input[name="watch-min"]:checked').value);
  try {
    await call("/api/watch", { kind, minutes, process: kind === "app" ? $("watch-app").value : null });
    say("Waiting. The PC turns off by itself when it's done.", "ok");
    refreshLive();
  } catch (error) {
    say(error.message, "error");
  }
});

$("watch-stop").addEventListener("click", async () => {
  try {
    await call("/api/watch/cancel", {});
    say("Stopped waiting.", "ok");
    refreshLive();
  } catch (error) {
    say(error.message, "error");
  }
});

// --- Automations ----------------------------------------------------------

function renderRecipes(pc) {
  $("r1-url").textContent = `${location.origin}/api/shutdown`;
  $("r1-token").dataset.value = token || "";
}

document.querySelectorAll(".copy").forEach((button) => button.addEventListener("click", async () => {
  const source = button.dataset.copyFrom ? $(button.dataset.copyFrom) : null;
  const text = button.dataset.copy ?? source?.dataset.value ?? source?.textContent ?? "";
  try {
    await navigator.clipboard.writeText(text);
  } catch {
    // Clipboard API needs HTTPS on some browsers: fall back to a hidden text field.
    const area = el("textarea");
    area.value = text;
    document.body.append(area);
    area.select();
    document.execCommand("copy");
    area.remove();
  }
  const label = button.textContent;
  button.textContent = "Copied";
  setTimeout(() => (button.textContent = label), 1500);
}));

// --- Shut down ------------------------------------------------------------

function disarm() {
  clearTimeout(armTimer);
  armTimer = null;
  power.classList.remove("armed");
  hint.textContent = "Tap to shut down";
}

async function shutDown() {
  $("unsaved-confirm").hidden = true;
  power.disabled = true;
  const delayMinutes = Number(form.elements.delay.value);
  try {
    const result = await call("/api/shutdown", { pin: $("pin").value, delayMinutes });
    $("pin").value = "";
    // "now" can still have a short delay when the PC has one set in its advanced settings.
    const secondsLeft = (new Date(result.scheduledShutdownAt) - Date.now()) / 1000;
    if (secondsLeft <= 2) {
      say("Shutting down now. Bye!", "ok");
    } else {
      setScheduled(result.scheduledShutdownAt);
      say("Shutdown scheduled.", "ok");
    }
  } catch (error) {
    say(error.message, "error");
    if (error.needsPairing) refresh();
  } finally {
    power.disabled = false;
  }
}

// Two taps are needed, so a pocket tap does not turn the PC off.
form.addEventListener("submit", async (event) => {
  event.preventDefault();

  if (!armTimer) {
    power.classList.add("armed");
    hint.textContent = "Tap again to confirm";
    armTimer = setTimeout(disarm, 4000);
    return;
  }
  disarm();

  // Check for unsaved work right before shutting down (paired phones can see it).
  if (paired) {
    try {
      const pc = await call("/api/pc");
      if (pc.unsaved.length > 0) {
        $("unsaved-confirm-list").replaceChildren(...pc.unsaved.map((u) => el("li", `${u.name}: ${u.title}`)));
        $("unsaved-confirm").hidden = false;
        $("unsaved-confirm").scrollIntoView({ behavior: "smooth", block: "center" });
        return;
      }
    } catch { /* if the check fails, don't block the shutdown */ }
  }
  shutDown();
});

$("unsaved-go").addEventListener("click", shutDown);
$("unsaved-cancel").addEventListener("click", () => {
  $("unsaved-confirm").hidden = true;
  say("The PC stays on.", "");
});

$("cancel").addEventListener("click", async () => {
  try {
    await call("/api/cancel", {});
    setScheduled(null);
    say("Shutdown cancelled.", "ok");
  } catch (error) {
    say(error.message, "error");
  }
});

$("install-done").addEventListener("click", () => {
  storageSet(INSTALL_KEY, "1");
  stopInstallGuide();
  $("install").hidden = true;
});

// --- Start ----------------------------------------------------------------

async function start() {
  const params = new URLSearchParams(location.hash.slice(1));
  const pairCode = params.get("pair");
  token = params.get("k") || storageGet(TOKEN_KEY);

  if (pairCode) {
    history.replaceState(null, "", location.pathname); // don't keep the one-time code around
    try {
      const result = await call("/api/pair", { code: pairCode, name: deviceName() });
      setToken(result.token);
      say("This phone is paired.", "ok");
    } catch (error) {
      say(error.message, "error");
    }
  } else if (token) {
    setToken(token); // make sure the address holds it before the user adds a shortcut
  }

  updateWatchSetup();
  const status = await refresh();
  if (status?.paired || new URLSearchParams(location.search).has("demo")) showInstallTip();
  liveTimer = setInterval(refreshLive, LIVE_EVERY_MS);
}

setInterval(tick, 1000);
document.addEventListener("visibilitychange", () => {
  if (!document.hidden) refresh();
});
start();

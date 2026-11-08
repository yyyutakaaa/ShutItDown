"use strict";

// Shows how to put the page on the home screen. Websites can't trigger that themselves on iPhone,
// so for iPhone this plays a short looping animation of Safari:
// hold the address bar → Share → View More → Add to Home Screen → Add.
window.InstallGuide = (() => {
  const ICONS = {
    share: '<svg viewBox="0 0 24 24"><path d="M12 15V3M8 7l4-4 4 4M6 11H5v10h14V11h-1"/></svg>',
    copy: '<svg viewBox="0 0 24 24"><path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/></svg>',
    mic: '<svg viewBox="0 0 24 24"><rect x="9" y="3" width="6" height="11" rx="3"/><path d="M6 11a6 6 0 0 0 12 0M12 17v4"/></svg>',
    tabs: '<svg viewBox="0 0 24 24"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 4H6a2 2 0 0 0-2 2v10"/></svg>',
    close: '<svg viewBox="0 0 24 24"><path d="M6 6l12 12M18 6L6 18"/></svg>',
    add: '<svg viewBox="0 0 24 24"><rect x="4" y="4" width="16" height="16" rx="4"/><path d="M12 8v8M8 12h8"/></svg>',
    book: '<svg viewBox="0 0 24 24"><path d="M4 5h6a2 2 0 0 1 2 2v13a2 2 0 0 0-2-2H4zM20 5h-6a2 2 0 0 0-2 2v13a2 2 0 0 1 2-2h6z"/></svg>',
    star: '<svg viewBox="0 0 24 24"><path d="M12 4l2.5 5 5.5.8-4 3.9.9 5.5-4.9-2.6-4.9 2.6.9-5.5-4-3.9 5.5-.8z"/></svg>',
    glasses: '<svg viewBox="0 0 24 24"><circle cx="7" cy="15" r="3"/><circle cx="17" cy="15" r="3"/><path d="M10 15h4M4 15l2-8M20 15l-2-8"/></svg>',
    search: '<svg viewBox="0 0 24 24"><circle cx="11" cy="11" r="6"/><path d="M20 20l-4.5-4.5"/></svg>',
    menu: '<svg viewBox="0 0 24 24"><path d="M5 7h14M5 12h14M5 17h9"/></svg>',
    reload: '<svg viewBox="0 0 24 24"><path d="M19 12a7 7 0 1 1-2-5M19 4v4h-4"/></svg>',
    app: '<svg viewBox="0 0 256 256" class="app-icon"><rect width="256" height="256" rx="56" fill="#FF5A1F"/><g fill="none" stroke="#141414" stroke-width="26" stroke-linecap="round"><path d="M169.1 89A64 64 0 1 1 86.9 89"/><path d="M128 52v76"/></g></svg>',
  };

  // Each step: what the phone shows, where the finger goes (px on the 196 x 400 screen),
  // how it presses ("hold" or "tap"), which caption is lit, and how long the step lasts.
  const IOS_STEPS = [
    { scene: "browser", finger: null, caption: null, ms: 1000 },
    { scene: "browser", finger: [98, 374], press: "hold", caption: 0, ms: 1900 },
    { scene: "menu", finger: [98, 201], press: "tap", caption: 1, ms: 1400 },
    { scene: "sheet", finger: [98, 341], press: "tap", caption: 2, ms: 1500 },
    { scene: "more", finger: [98, 243], press: "tap", caption: 3, ms: 1600 },
    { scene: "dialog", finger: [176, 20], press: "tap", caption: 4, ms: 1500 },
    { scene: "home", finger: null, caption: 5, ms: 2600 },
  ];

  const IOS_CAPTIONS = [
    "Press and hold the address bar at the bottom.",
    "Tap Share.",
    "Tap View More.",
    "Tap Add to Home Screen.",
    "Tap Add.",
    "Done. Open Shutdown from your home screen.",
  ];

  const ANDROID_CAPTIONS = [
    "Tap the ⋮ menu at the top right of Chrome.",
    "Tap Add to Home screen.",
    "Tap Add.",
    "Done. Open Shutdown from your home screen.",
  ];

  function isIos() {
    const ua = navigator.userAgent;
    return /iPhone|iPad|iPod/.test(ua) || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1);
  }

  const row = (icon, text, extra = "") => `<span class="row ${extra}">${ICONS[icon]}<span>${text}</span></span>`;

  function buildIosDemo(host) {
    const homeIcons = Array.from({ length: 15 }, (_, i) =>
      i === 5 ? `<span class="hs-app new">${ICONS.app}<i>Shutdown</i></span>` : `<span class="hs-app"><b></b><i></i></span>`
    ).join("");
    const contacts = Array.from({ length: 4 }, () => "<b></b>").join("");

    const phone = document.createElement("div");
    phone.className = "demo-phone";
    phone.innerHTML = `
      <div class="demo-screen">
        <div class="scene scene-browser">
          <div class="mini-page">
            <span class="mini-line short"></span>
            <span class="mini-line"></span>
            <span class="mini-power"></span>
          </div>
          <span class="round back">&#8249;</span>
          <span class="address">${ICONS.menu}<span><em>Not Secure —</em> ${host}</span>${ICONS.reload}</span>
          <span class="round tabs">${ICONS.tabs}</span>

          <div class="menu">
            ${row("share", "Share", "target")}
            ${row("copy", "Copy")}
            ${row("mic", "Voice Search")}
            <span class="sep"></span>
            ${row("tabs", "Switch Tab Groups")}
            ${row("close", "Close Tab", "red")}
          </div>

          <div class="sheet sheet-compact">
            <div class="sheet-head">${ICONS.app}<span><b>Shutdown</b><i>${host}</i></span><span class="x">${ICONS.close}</span></div>
            <div class="contacts">${contacts}</div>
            <div class="group">
              ${row("copy", "Copy")}
              ${row("book", "Add Bookmark")}
            </div>
            <span class="view-more">View More</span>
          </div>

          <div class="sheet sheet-full">
            <div class="sheet-head">${ICONS.app}<span><b>Shutdown</b><i>${host}</i></span><span class="x">${ICONS.close}</span></div>
            <div class="group">
              ${row("copy", "Copy")}
              ${row("book", "Add Bookmark")}
              ${row("star", "Add to Favorites")}
              ${row("glasses", "Add to Reading List")}
              ${row("add", "Add to Home Screen", "target")}
              ${row("search", "Find on Page")}
            </div>
          </div>
        </div>

        <div class="scene scene-dialog">
          <div class="dlg-nav"><span>Cancel</span><b>Add to Home Screen</b><span class="add">Add</span></div>
          <div class="dlg-body">${ICONS.app}<span class="dlg-name">Shutdown<i>${host}</i></span></div>
          <div class="dlg-toggle"><span>Open as Web App</span><span class="switch"></span></div>
        </div>

        <div class="scene scene-home"><div class="hs-grid">${homeIcons}</div></div>
        <span class="finger"></span>
      </div>`;
    return phone;
  }

  function placeFinger(phone, step, animate) {
    const finger = phone.querySelector(".finger");
    if (!step.finger) {
      finger.classList.remove("visible");
      return finger;
    }
    const [x, y] = step.finger;
    if (!animate) finger.style.transition = "none";
    finger.classList.add("visible");
    finger.style.transform = `translate(${x - 13}px, ${y - 13}px)`;
    return finger;
  }

  function play(phone, list) {
    const items = list.querySelectorAll("li");
    let index = 0;
    let timer = null;
    let stopped = false;

    function apply(step) {
      phone.dataset.scene = step.scene;
      items.forEach((li, i) => li.classList.toggle("current", i === step.caption));

      const finger = placeFinger(phone, step, true);
      if (!step.finger) return;

      // Press after the finger arrives; a hold lasts long enough for the menu to make sense.
      const pressFor = step.press === "hold" ? 900 : 300;
      const pressAt = step.ms - pressFor - 150;
      setTimeout(() => finger.classList.add(step.press), pressAt);
      setTimeout(() => finger.classList.remove(step.press), pressAt + pressFor);
    }

    function next() {
      const step = IOS_STEPS[index];
      apply(step);
      index = (index + 1) % IOS_STEPS.length;
      timer = setTimeout(next, step.ms);
    }

    function pause() {
      clearTimeout(timer);
      timer = null;
    }

    // Only run while someone can see it.
    document.addEventListener("visibilitychange", () => {
      if (document.hidden) pause();
      else if (!timer && !stopped) next();
    });

    next();
    return () => {
      stopped = true;
      pause();
    };
  }

  // For a still picture (and for people who turned off animations): show one step without moving.
  function freeze(phone, list, stepIndex) {
    const step = IOS_STEPS[Math.max(0, Math.min(stepIndex, IOS_STEPS.length - 1))];
    phone.dataset.scene = step.scene;
    list.querySelectorAll("li").forEach((li, i) => li.classList.toggle("current", i === step.caption));
    placeFinger(phone, step, false);
  }

  /**
   * Fills the install box. Options: platform ("ios" | "android", detected when left out)
   * and step (freeze the iPhone demo on one step). Returns a function that stops the animation.
   */
  function render(container, options = {}) {
    const platform = options.platform || (isIos() ? "ios" : "android");
    const video = container.querySelector(".install-video");
    const list = container.querySelector(".install-steps");
    const captions = platform === "ios" ? IOS_CAPTIONS : ANDROID_CAPTIONS;

    list.innerHTML = captions.map((text) => `<li>${text}</li>`).join("");
    video.innerHTML = "";
    container.querySelector(".install-note").textContent = platform === "ios"
      ? "On older iPhones, tap the Share button in the bottom bar instead, then Add to Home Screen."
      : "";

    const none = () => {};
    if (platform !== "ios") return none;

    const phone = buildIosDemo(location.hostname);
    video.appendChild(phone);

    const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (options.step !== undefined) {
      freeze(phone, list, options.step);
      return none;
    }
    if (reduceMotion) {
      freeze(phone, list, 4);
      return none;
    }
    return play(phone, list);
  }

  return { render, isIos };
})();

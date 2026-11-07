// ============================================================
// HtmlWidgetPanel.js — AutoFPS widget (Fluent redesign)
//
// One script for both front-ends:
//   * the in-game MSFS toolbar panel (HtmlWidgetPanel.html, inside <ingame-ui>)
//   * the browser page served by the app at http://localhost:54321
// It builds the whole UI into #mainSection and talks to the AutoFPS app API:
//   GET  /api/status            live values + overview controls (polled)
//   GET  /api/page/{tuning|settings}  description of that app page (polled while shown)
//   POST /api/control           {"name","value"} change any Tuning/Settings control
//   POST /api/... (legacy)      reset, flighttype, targetmode, target, targetfps, toggle*
// Written in plain ES5-style JS for the sim's Coherent engine.
// ============================================================
var API = "http://localhost:54321";
var POLL_STATUS_MS = 500;
var POLL_PAGE_MS = 1000;

// Target FPS stepper
var FPS_MIN = 10;
var FPS_MAX = 500;
var FPS_HOLD_DELAY_MS = 400;
var FPS_REPEAT_MS = 125;
var FPS_STEP5_AFTER_MS = 2000;
var FPS_STEP10_AFTER_MS = 4000;
var FPS_SEND_DEBOUNCE_MS = 450;

// Width-based scaling (VR friendly)
var BASE_WIDTH = 440;
var MIN_SCALE = 0.6;
var MAX_SCALE_INGAME = 3.0;
var MAX_SCALE_WEB = 1.25;

var SUPPRESS_MS = 1500;

(function () {
	var SVG_CHEV = '<svg class="chev" viewBox="0 0 10 10"><polyline points="2,3.5 5,6.5 8,3.5" fill="none" stroke="currentColor" stroke-width="1.3"/></svg>';
	var SVG_CHECK = '<svg viewBox="0 0 10 10"><polyline points="1.8,5.2 4.1,7.4 8.4,2.6" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>';
	var SVG_MINUS = '<svg viewBox="0 0 10 10"><rect x="1" y="4.3" width="8" height="1.4" fill="currentColor"/></svg>';
	var SVG_PLUS = '<svg viewBox="0 0 10 10"><rect x="1" y="4.3" width="8" height="1.4" fill="currentColor"/><rect x="4.3" y="1" width="1.4" height="8" fill="currentColor"/></svg>';
	var SVG_CLOSE = '<svg viewBox="0 0 10 10"><path d="M1.5 1.5 L8.5 8.5 M8.5 1.5 L1.5 8.5" stroke="currentColor" stroke-width="1.3"/></svg>';
	var SVG_INFO = '<svg viewBox="0 0 16 16"><circle cx="8" cy="8" r="6.6" fill="none" stroke="currentColor" stroke-width="1.2"/><rect x="7.35" y="7" width="1.3" height="4.6" fill="currentColor"/><circle cx="8" cy="4.9" r="0.85" fill="currentColor"/></svg>';

	// Status text glyphs the app uses, drawn as SVG so they render in the sim
	var ICONS = {
		"▲": '<svg viewBox="0 0 10 10"><polygon points="5,1 9,9 1,9" fill="currentColor"/></svg>',
		"▼": '<svg viewBox="0 0 10 10"><polygon points="1,1 9,1 5,9" fill="currentColor"/></svg>',
		"⊥": '<svg viewBox="0 0 10 10"><line x1="5" y1="1" x2="5" y2="7" stroke="currentColor" stroke-width="1.4"/><line x1="1" y1="9" x2="9" y2="9" stroke="currentColor" stroke-width="1.4"/></svg>',
		"⊤": '<svg viewBox="0 0 10 10"><line x1="5" y1="3" x2="5" y2="9" stroke="currentColor" stroke-width="1.4"/><line x1="1" y1="1" x2="9" y2="1" stroke="currentColor" stroke-width="1.4"/></svg>',
		"Δ": '<svg viewBox="0 0 10 10"><polygon points="5,1 9,9 1,9" fill="none" stroke="currentColor" stroke-width="1"/></svg>',
		"⚡": '<svg viewBox="0 0 10 10"><polygon points="6,0 1,6 4.5,6 3.5,10 9,4 5.5,4" fill="currentColor"/></svg>',
		"⏸": '<svg viewBox="0 0 10 10"><rect x="1" y="1" width="2.5" height="8" fill="currentColor"/><rect x="6.5" y="1" width="2.5" height="8" fill="currentColor"/></svg>',
		"❄️": '<svg viewBox="0 0 10 10"><g stroke="currentColor" stroke-width="1" stroke-linecap="round"><line x1="5" y1="0.5" x2="5" y2="9.5"/><line x1="0.5" y1="5" x2="9.5" y2="5"/><line x1="1.9" y1="1.9" x2="8.1" y2="8.1"/><line x1="8.1" y1="1.9" x2="1.9" y2="8.1"/></g></svg>',
		"🔒": '<svg viewBox="0 0 10 10"><rect x="2" y="4.5" width="6" height="5" rx="0.6" fill="currentColor"/><path d="M3.3 4.5V3.2a1.7 1.7 0 0 1 3.4 0V4.5" fill="none" stroke="currentColor" stroke-width="1"/></svg>'
	};
	var ICON_KEYS = Object.keys(ICONS).sort(function (a, b) { return b.length - a.length; });

	// ------------------------------------------------------------------ helpers
	function el(tag, cls, html) {
		var e = document.createElement(tag);
		if (cls) e.className = cls;
		if (html !== undefined && html !== null) e.innerHTML = html;
		return e;
	}

	function text(tag, cls, t) {
		var e = el(tag, cls);
		e.textContent = t === undefined || t === null ? "" : String(t);
		return e;
	}

	function post(path, body) {
		return fetch(API + path, { method: "POST", cache: "no-store", body: body === undefined ? "" : body }).catch(function () { });
	}

	function now() { return Date.now(); }

	// Colours from the app are only applied when they carry meaning (green/red/orange...), not neutral greys.
	function meaningfulColor(hex) {
		if (!hex || typeof hex !== "string" || hex.charAt(0) !== "#" || hex.length < 7) return null;
		var r = parseInt(hex.substr(1, 2), 16), g = parseInt(hex.substr(3, 2), 16), b = parseInt(hex.substr(5, 2), 16);
		if (!isFinite(r) || !isFinite(g) || !isFinite(b)) return null;
		var mx = Math.max(r, g, b), mn = Math.min(r, g, b);
		return (mx - mn) < 24 ? null : hex;
	}

	function setTextWithIcons(node, t) {
		node.textContent = "";
		if (!t) return;
		var i = 0;
		while (i < t.length) {
			var matched = null;
			for (var k = 0; k < ICON_KEYS.length; k++) {
				if (t.indexOf(ICON_KEYS[k], i) === i) { matched = ICON_KEYS[k]; break; }
			}
			if (matched) {
				var span = el("span", "icon-glyph", ICONS[matched]);
				node.appendChild(span);
				i += matched.length;
				continue;
			}
			var start = i;
			i++;
			while (i < t.length) {
				var iconStart = false;
				for (var k2 = 0; k2 < ICON_KEYS.length; k2++) {
					if (t.indexOf(ICON_KEYS[k2], i) === i) { iconStart = true; break; }
				}
				if (iconStart) break;
				i++;
			}
			node.appendChild(document.createTextNode(t.slice(start, i)));
		}
	}

	function applyField(node, item, extraColorTarget) {
		if (!node || !item) return;
		setTextWithIcons(node, item.text === undefined || item.text === null ? "" : String(item.text));
		var c = meaningfulColor(item.color);
		node.style.color = c || "";
		if (extraColorTarget) extraColorTarget.style.background = c || "";
	}

	// ------------------------------------------------------------------ components
	var openSelects = [];

	function closeAllSelects() {
		for (var i = 0; i < openSelects.length; i++) openSelects[i].list.classList.add("hidden");
	}

	/** Custom dropdown: {root, setItems(items), setIndex(i), setWarn(bool)} — onPick(index) on user choice. */
	function makeSelect(onPick, extraClass) {
		var root = el("div", "select" + (extraClass ? " " + extraClass : ""));
		var btn = el("button", "select-btn");
		btn.type = "button";
		var label = el("span");
		btn.appendChild(label);
		btn.insertAdjacentHTML("beforeend", SVG_CHEV);
		var list = el("div", "select-list hidden");
		root.appendChild(btn);
		root.appendChild(list);
		var api = { root: root, list: list, items: [], index: -1, key: null };
		openSelects.push(api);

		btn.addEventListener("click", function (e) {
			e.stopPropagation();
			var wasHidden = list.classList.contains("hidden");
			closeAllSelects();
			if (wasHidden && api.items.length) list.classList.remove("hidden");
		});

		api.setItems = function (items) {
			var key = JSON.stringify(items || []);
			if (key === api.key) return;
			api.key = key;
			api.items = items || [];
			list.textContent = "";
			for (var i = 0; i < api.items.length; i++) {
				(function (idx) {
					var it = text("div", "select-item", api.items[idx]);
					it.addEventListener("click", function (e) {
						e.stopPropagation();
						closeAllSelects();
						onPick(idx);
					});
					list.appendChild(it);
				})(i);
			}
			api.setIndex(api.index, true);
		};

		api.setIndex = function (idx, force) {
			if (idx === api.index && !force) return;
			api.index = idx;
			label.textContent = (idx >= 0 && idx < api.items.length) ? api.items[idx] : "-";
			var ch = list.children;
			for (var i = 0; i < ch.length; i++) ch[i].classList.toggle("active", i === idx);
		};

		api.setWarn = function (on) { btn.classList.toggle("afps-warn", !!on); };
		api.setEnabled = function (on) { btn.disabled = !on; };
		return api;
	}

	/** Checkbox: {root, set(checked)} — onToggle(newValue) on click. */
	function makeCheck(caption, onToggle) {
		var root = el("label", "check");
		root.appendChild(el("span", "box", SVG_CHECK));
		var cap = text("span", "", caption);
		root.appendChild(cap);
		var api = { root: root, checked: false, enabled: true };
		root.addEventListener("click", function (e) {
			e.preventDefault();
			e.stopPropagation();
			if (!api.enabled) return;
			api.set(!api.checked);
			onToggle(api.checked);
		});
		api.set = function (v) { api.checked = !!v; root.classList.toggle("on", api.checked); };
		api.setText = function (t) { cap.textContent = t; };
		api.setEnabled = function (on) { api.enabled = !!on; root.classList.toggle("disabled", !api.enabled); };
		return api;
	}

	// ------------------------------------------------------------------ build UI
	function init() {
		var main = document.getElementById("mainSection");
		if (!main) { setTimeout(init, 100); return; }
		var ingameUi = document.getElementById("HtmlWidgetPanel");
		var IS_INGAME = !!ingameUi;
		var MAX_SCALE = IS_INGAME ? MAX_SCALE_INGAME : MAX_SCALE_WEB;

		// Top bar
		var topbar = el("div", "topbar");
		var logo = el("img", "logo");
		logo.src = "img/AutoFPS.png";
		logo.alt = "";
		topbar.appendChild(logo);
		topbar.appendChild(text("span", "app-name", "AutoFPS"));
		var tabs = el("div", "tabs");
		var tabNames = [["overview", "Overview"], ["tuning", "Tuning"], ["settings", "Settings"]];
		var tabButtons = {};
		for (var ti = 0; ti < tabNames.length; ti++) {
			var tb = text("button", "tab", tabNames[ti][1]);
			tb.type = "button";
			tb.setAttribute("data-tab", tabNames[ti][0]);
			tabs.appendChild(tb);
			tabButtons[tabNames[ti][0]] = tb;
		}
		topbar.appendChild(tabs);
		topbar.appendChild(el("span", "spacer"));
		var closeBtn = null;
		if (IS_INGAME) {
			closeBtn = el("button", "icon-btn", SVG_CLOSE);
			closeBtn.type = "button";
			closeBtn.title = "Close";
			topbar.appendChild(closeBtn);
		}
		main.appendChild(topbar);

		var disconnected = text("div", "card disconnected muted", "Not connected. Start AutoFPS on this PC.");
		main.appendChild(disconnected);

		// ---- Overview page
		var pageOverview = el("div", "page hidden");
		main.appendChild(pageOverview);

		var chips = null;
		var chipEls = {};
		if (!IS_INGAME) {
			var connCard = el("div", "card");
			connCard.appendChild(text("div", "card-title", "Connection status"));
			chips = el("div", "chips");
			var chipIds = ["connMSFS", "connSimConnect", "connSession"];
			for (var ci = 0; ci < chipIds.length; ci++) {
				var chip = el("span", "chip");
				var dot = el("span", "dot");
				var lab = text("span", "", "-");
				chip.appendChild(dot);
				chip.appendChild(lab);
				chips.appendChild(chip);
				chipEls[chipIds[ci]] = { dot: dot, label: lab };
			}
			connCard.appendChild(chips);
			pageOverview.appendChild(connCard);
		}

		var simCard = el("div", "card");
		var simTitle = el("div", "card-title");
		var imgSimHeaderFS = el("img", "hidden");
		imgSimHeaderFS.src = "img/fps_msfs.png";
		imgSimHeaderFS.title = "Default MSFS settings";
		var imgSimHeaderApp = el("img", "hidden");
		imgSimHeaderApp.src = "img/AutoFPS.png";
		imgSimHeaderApp.title = "AutoFPS-controlled MSFS settings";
		simTitle.appendChild(imgSimHeaderFS);
		simTitle.appendChild(imgSimHeaderApp);
		simTitle.appendChild(text("span", "", "Sim values"));
		simCard.appendChild(simTitle);
		var tilesEl = el("div", "tiles");
		simCard.appendChild(tilesEl);
		pageOverview.appendChild(simCard);

		var tileDefs = [["lblFps", "valFps"], ["lblTlod", "valTlod"], ["lblOlod", "valOlod"], ["lblAgl", "valAgl"], ["lblFpm", "valFpm"], ["lblClouds", "valClouds"]];
		var fieldEls = {};
		var fpsIcon = el("img", "fps-source-icon hidden");
		for (var tdi = 0; tdi < tileDefs.length; tdi++) {
			var tile = el("div", "tile");
			var inner = el("div", "tile-inner");
			var cap = text("div", "tile-cap muted", "");
			var valWrap = el("div", "tile-val");
			var val = text("span", "", "n/a");
			valWrap.appendChild(val);
			if (tdi === 0) valWrap.appendChild(fpsIcon);
			inner.appendChild(cap);
			inner.appendChild(valWrap);
			tile.appendChild(inner);
			tilesEl.appendChild(tile);
			fieldEls[tileDefs[tdi][0]] = cap;
			fieldEls[tileDefs[tdi][1]] = val;
		}

		var flightCard = el("div", "card");
		flightCard.appendChild(text("div", "card-title", "Flight"));
		var row1 = el("div", "row");
		var row2 = el("div", "row");
		flightCard.appendChild(row1);
		flightCard.appendChild(row2);
		pageOverview.appendChild(flightCard);

		var statusLine = el("div", "card status-line", SVG_INFO);
		var statusText = el("span");
		statusLine.appendChild(statusText);
		pageOverview.appendChild(statusLine);

		// ---- Tuning & Settings pages (rendered from the app's page description)
		var pageTuning = el("div", "page hidden");
		var tuningDyn = el("div");
		pageTuning.appendChild(tuningDyn);
		main.appendChild(pageTuning);

		var pageSettings = el("div", "page hidden");
		var settingsDyn = el("div");
		pageSettings.appendChild(settingsDyn);
		main.appendChild(pageSettings);

		var pages = { overview: pageOverview, tuning: pageTuning, settings: pageSettings };
		var activeTab = "overview";
		try { activeTab = localStorage.getItem("afpsTab") || "overview"; } catch (e) { }
		if (!pages[activeTab]) activeTab = "overview";

		// Widget-only settings (in-game): camera auto-hide
		var autoHideItems = ["Off", "Chase", "All Ext"];
		var autoHideIndex = 0;
		var suppressAutoHideUntil = 0;
		var autoHideSel = null;
		if (IS_INGAME) {
			var widgetCard = el("div", "card");
			widgetCard.appendChild(text("div", "card-title", "In-game widget"));
			var whRow = el("div", "row");
			whRow.appendChild(text("span", "lbl", "Auto-hide in external views"));
			autoHideSel = makeSelect(function (idx) {
				autoHideIndex = idx;
				suppressAutoHideUntil = now() + SUPPRESS_MS;
				autoHideSel.setIndex(idx);
				post("/api/widgetautohide", String(idx));
			});
			autoHideSel.setItems(autoHideItems);
			autoHideSel.setIndex(0);
			whRow.appendChild(autoHideSel.root);
			widgetCard.appendChild(whRow);
			pageSettings.appendChild(widgetCard);
		}

		// ---------------------------------------------------------------- Overview controls
		var modeSel, targetSel, profileSel, expertChk, resetBtn;
		var suppressModeUntil = 0, suppressTargetUntil = 0, suppressProfileUntil = 0, suppressExpertUntil = 0;
		var targetListKind = "auto", targetSwapIndex = -1;

		modeSel = makeSelect(function (idx) {
			flushFpsSend();
			suppressModeUntil = now() + SUPPRESS_MS;
			modeSel.setIndex(idx);
			post("/api/targetmode", String(idx));
		});
		row1.appendChild(modeSel.root);
		row1.appendChild(text("span", "lbl muted", "Target"));
		targetSel = makeSelect(function (idx) {
			flushFpsSend();
			if (idx !== targetSwapIndex) {
				suppressTargetUntil = now() + SUPPRESS_MS;
				targetSel.setIndex(idx);
			}
			post("/api/target", targetListKind + ":" + idx);
		});
		row1.appendChild(targetSel.root);
		row1.appendChild(text("span", "lbl muted", "FPS"));

		var stepper = el("div", "stepper");
		var fpsDecBtn = el("button", "stepper-btn", SVG_MINUS);
		fpsDecBtn.type = "button";
		fpsDecBtn.title = "Decrease target FPS (hold to speed up)";
		var fpsValEl = text("span", "stepper-val", "-");
		var fpsIncBtn = el("button", "stepper-btn", SVG_PLUS);
		fpsIncBtn.type = "button";
		fpsIncBtn.title = "Increase target FPS (hold to speed up)";
		stepper.appendChild(fpsDecBtn);
		stepper.appendChild(fpsValEl);
		stepper.appendChild(fpsIncBtn);
		row1.appendChild(stepper);

		row2.appendChild(text("span", "lbl muted", "Flight type"));
		profileSel = makeSelect(function (idx) {
			suppressProfileUntil = now() + SUPPRESS_MS;
			profileSel.setIndex(idx);
			post("/api/flighttype", String(idx));
		});
		row2.appendChild(profileSel.root);
		expertChk = makeCheck("Expert", function () {
			suppressExpertUntil = now() + SUPPRESS_MS;
			post("/api/toggleexpert");
		});
		row2.appendChild(expertChk.root);
		resetBtn = text("button", "btn hidden", "Reset");
		resetBtn.type = "button";
		resetBtn.title = "Reset TLOD, clouds, Auto Target FPS and graphics mode detection to their initial state";
		resetBtn.addEventListener("click", function (e) {
			e.stopPropagation();
			resetBtn.disabled = true;
			post("/api/reset").then(function () { setTimeout(function () { resetBtn.disabled = false; }, 1000); });
		});
		row2.appendChild(resetBtn);

		// ---- Target FPS stepper with hold-to-repeat
		var fpsValue = null, fpsDirty = false, fpsSendTimer = null, suppressFpsUntil = 0;
		var holdDir = 0, holdStart = 0, holdDelayTimer = null, holdRepeatTimer = null;

		function formatFps(v) { return (v === null || !isFinite(v)) ? "-" : String(Math.round(v * 10) / 10); }
		function showFps() { fpsValEl.textContent = formatFps(fpsValue); }
		function flushFpsSend() {
			if (fpsSendTimer) { clearTimeout(fpsSendTimer); fpsSendTimer = null; }
			if (!fpsDirty || fpsValue === null) return;
			fpsDirty = false;
			suppressFpsUntil = now() + SUPPRESS_MS;
			post("/api/targetfps", String(fpsValue));
		}
		function queueFpsSend() {
			fpsDirty = true;
			if (fpsSendTimer) clearTimeout(fpsSendTimer);
			fpsSendTimer = setTimeout(flushFpsSend, FPS_SEND_DEBOUNCE_MS);
		}
		function stepFps(dir, step) {
			if (fpsValue === null) return;
			var v = fpsValue, nv;
			if (step <= 1) nv = v + dir;
			else if (dir > 0) nv = (Math.floor(v / step) + 1) * step;
			else nv = (Math.ceil(v / step) - 1) * step;
			if (nv < FPS_MIN) nv = FPS_MIN;
			if (nv > FPS_MAX) nv = FPS_MAX;
			nv = Math.round(nv * 2) / 2;
			if (nv === v) return;
			fpsValue = nv;
			showFps();
			suppressFpsUntil = now() + 2500;
			queueFpsSend();
		}
		function stopHold() {
			if (holdDelayTimer) { clearTimeout(holdDelayTimer); holdDelayTimer = null; }
			if (holdRepeatTimer) { clearInterval(holdRepeatTimer); holdRepeatTimer = null; }
			holdDir = 0;
		}
		function startHold(dir) {
			stopHold();
			holdDir = dir;
			holdStart = now();
			stepFps(dir, 1);
			holdDelayTimer = setTimeout(function () {
				holdDelayTimer = null;
				holdRepeatTimer = setInterval(function () {
					var held = now() - holdStart;
					stepFps(holdDir, held >= FPS_STEP10_AFTER_MS ? 10 : (held >= FPS_STEP5_AFTER_MS ? 5 : 1));
				}, FPS_REPEAT_MS);
			}, FPS_HOLD_DELAY_MS);
		}
		function wireStepper(btn, dir) {
			btn.addEventListener("mousedown", function (e) {
				if (e.button !== 0) return;
				e.stopPropagation();
				startHold(dir);
			});
			btn.addEventListener("mouseleave", stopHold);
		}
		wireStepper(fpsDecBtn, -1);
		wireStepper(fpsIncBtn, 1);
		window.addEventListener("mouseup", stopHold, true);
		window.addEventListener("blur", stopHold);

		fpsIcon.addEventListener("click", function (e) {
			if (fpsIcon.getAttribute("data-clickable") !== "true") return;
			e.stopPropagation();
			post("/api/togglefpssource");
		});

		// ---------------------------------------------------------------- tabs
		// Switching tabs must not flash the sim panel: the target page is rendered before it is shown
		// (Tuning/Settings are prefetched in the background), and the panel height is adjusted in the
		// same step as the swap instead of a debounced resize afterwards.
		var tabRequest = 0;
		function showTab(name) {
			activeTab = name;
			try { localStorage.setItem("afpsTab", name); } catch (e) { }
			for (var k in tabButtons) tabButtons[k].classList.toggle("active", k === name);
			if (!connected) return;
			var req = ++tabRequest;
			if (name !== "overview" && pageState[name].key === null) {
				// Not loaded yet (first open): fetch, then swap once its content exists.
				fetchPage(name, function () { if (req === tabRequest) applyTab(name); });
				return;
			}
			applyTab(name);
		}
		function applyTab(name) {
			for (var p in pages) pages[p].classList.toggle("hidden", p !== name);
			snapNow();
		}
		for (var tk in tabButtons) {
			(function (name) {
				tabButtons[name].addEventListener("click", function (e) { e.stopPropagation(); showTab(name); });
			})(tk);
		}

		if (closeBtn) {
			closeBtn.addEventListener("click", function () {
				if (window.Coherent && typeof Coherent.call === "function") {
					Coherent.call("TOOLBAR_BUTTON_TOGGLE", "HTML_WIDGET_PANEL", false);
				}
			});
		}

		document.addEventListener("click", function (e) {
			if (e.target && e.target.closest && e.target.closest(".select")) return;
			closeAllSelects();
		});

		// ---------------------------------------------------------------- theme & connection
		var connected = false;
		var isDark = true;

		function applyTheme(dark) {
			if (dark === isDark) return;
			isDark = dark;
			main.classList.toggle("dark", dark);
			main.classList.toggle("light", !dark);
			document.body.classList.toggle("light", !dark);
		}

		function setConnected(on) {
			if (on === connected) return;
			connected = on;
			disconnected.classList.toggle("hidden", on);
			for (var p in pages) pages[p].classList.toggle("hidden", !on || p !== activeTab);
			lastSnapHeight = -1;
			scheduleSnap();
			if (on) {
				pollPage();
				prefetchPages();
			}
		}

		// ---------------------------------------------------------------- status polling (overview)
		function applyStatus(data) {
			setConnected(true);
			if (data.theme && typeof data.theme.isDark === "boolean") applyTheme(data.theme.isDark);

			for (var id in fieldEls) {
				if (data[id]) applyField(fieldEls[id], data[id]);
			}
			if (data.statusMessage) {
				applyField(statusText, data.statusMessage);
				var first = String(data.statusMessage.text || "").split("\n")[0];
				statusLine.classList.toggle("compact", first.length > 60);
			}
			if (chips) {
				for (var cid in chipEls) {
					if (!data[cid]) continue;
					chipEls[cid].label.textContent = data[cid].text || "";
					chipEls[cid].dot.style.background = meaningfulColor(data[cid].color) || "#E05050";
				}
			}

			var iconMap = { 1: "img/fps_msfs.png", 2: "img/fps_rtss.png", 3: "img/fps_ofxr.png", 4: "img/fps_vxr.png" };
			var src = iconMap[data.fpsIcon];
			if (src) {
				if (fpsIcon.getAttribute("src") !== src) fpsIcon.src = src;
				var clickable = (data.fpsIcon === 1 || data.fpsIcon === 2);
				fpsIcon.setAttribute("data-clickable", clickable ? "true" : "false");
				fpsIcon.style.cursor = clickable ? "pointer" : "default";
				fpsIcon.title = clickable ? "FPS source — click to toggle MSFS / RTSS" : "FPS source";
				fpsIcon.classList.remove("hidden");
			} else {
				fpsIcon.classList.add("hidden");
			}
			imgSimHeaderFS.classList.toggle("hidden", !data.simHeaderFS);
			imgSimHeaderApp.classList.toggle("hidden", !data.simHeaderDog);

			if (Array.isArray(data.profiles)) {
				profileSel.setItems(data.profiles.map(function (n) { return String(n).replace(/↔/g, "<->"); }));
				if (now() >= suppressProfileUntil && typeof data.activeProfileIndex === "number") profileSel.setIndex(data.activeProfileIndex);
			}
			if (typeof data.resetVisible === "boolean") resetBtn.classList.toggle("hidden", !data.resetVisible);
			if (typeof data.expertVisible === "boolean") expertChk.root.classList.toggle("hidden", !data.expertVisible);

			if (data.widgetHotkey !== undefined) syncHotkey(data.widgetHotkey);
			if (typeof data.hideTitleBar === "boolean") {
				document.body.classList.toggle("hide-titlebar", data.hideTitleBar);
			}

			applyControls(data.autoFpsControls);
		}

		function applyControls(c) {
			if (!c) return;
			if (Array.isArray(c.modeItems)) {
				modeSel.setItems(c.modeItems);
				if (now() >= suppressModeUntil && typeof c.mode === "number") modeSel.setIndex(c.mode);
				modeSel.setWarn(!!c.modeOrange);
			}
			if (Array.isArray(c.targetItems)) {
				var kind = String(c.targetList);
				if (kind !== targetListKind) { targetListKind = kind; suppressTargetUntil = 0; }
				targetSel.setItems(c.targetItems);
				targetSwapIndex = typeof c.targetSwapIndex === "number" ? c.targetSwapIndex : -1;
				if (now() >= suppressTargetUntil && typeof c.targetIndex === "number") targetSel.setIndex(c.targetIndex);
				targetSel.setWarn(!!c.targetOrange);
			}

			var editable = !!c.fpsEditable;
			fpsDecBtn.style.visibility = editable ? "visible" : "hidden";
			fpsIncBtn.style.visibility = editable ? "visible" : "hidden";
			if (!editable) {
				stopHold();
				fpsDirty = false;
				if (fpsSendTimer) { clearTimeout(fpsSendTimer); fpsSendTimer = null; }
				fpsValue = null;
				fpsValEl.textContent = (c.fpsText === undefined || c.fpsText === null || c.fpsText === "") ? "-" : String(c.fpsText);
			} else if (holdDir === 0 && (fpsValue === null || now() >= suppressFpsUntil) && c.fps !== undefined && c.fps !== null && c.fps !== "") {
				var f = parseFloat(String(c.fps).replace(",", "."));
				if (isFinite(f)) { fpsValue = f; showFps(); }
			}
			fpsValEl.style.color = c.fpsOrange ? "#F59E0B" : "";

			if (typeof c.expert === "boolean" && now() >= suppressExpertUntil) expertChk.set(c.expert);

			if (autoHideSel && typeof c.autoHide === "number" && now() >= suppressAutoHideUntil) {
				autoHideIndex = c.autoHide;
				autoHideSel.setIndex(autoHideIndex);
			}
		}

		function pollStatus() {
			fetch(API + "/api/status", { cache: "no-store" })
				.then(function (res) { if (!res.ok) throw new Error("HTTP " + res.status); return res.json(); })
				.then(applyStatus)
				.catch(function () { setConnected(false); });
		}

		// ---------------------------------------------------------------- Tuning / Settings pages
		var pageState = { tuning: { key: null, controls: {}, labels: [] }, settings: { key: null, controls: {}, labels: [] } };
		var pendingUntil = {};   // control name -> time until which polled values are ignored

		function structureKey(cards) {
			return JSON.stringify(cards, function (k, v) {
				if (k === "value" || k === "checked" || k === "index" || k === "warn" || k === "enabled" || k === "color" || k === "tip") return undefined;
				return v;
			});
		}

		function sendControl(name, value) {
			pendingUntil[name] = now() + SUPPRESS_MS;
			fetch(API + "/api/control", { method: "POST", cache: "no-store", body: JSON.stringify({ name: name, value: value }) })
				.catch(function () { })
				.then(function () { setTimeout(pollPage, 250); });
		}

		function buildControl(item, controls) {
			var node = null;
			switch (item.t) {
				case "label":
					node = el("span", "lbl");
					setTextWithIcons(node, item.text);
					controls.__labels.push({ kind: "label", node: node });
					break;
				case "note":
					node = text("div", "note muted", item.text);
					break;
				case "check": {
					var chk = makeCheck(item.text, function (v) { sendControl(item.name, v); });
					node = chk.root;
					controls[item.name] = { kind: "check", api: chk };
					break;
				}
				case "combo": {
					var sel = makeSelect(function (idx) { sel.setIndex(idx); sendControl(item.name, idx); });
					sel.setItems(item.items || []);
					node = sel.root;
					controls[item.name] = { kind: "combo", api: sel };
					break;
				}
				case "text": {
					var input = el("input", "input");
					input.type = "text";
					var commit = function () {
						var v = input.value.trim();
						if (v !== "" && v !== input.getAttribute("data-server")) sendControl(item.name, v);
					};
					input.addEventListener("keydown", function (e) {
						e.stopPropagation();
						if (e.keyCode === 13) { commit(); input.blur(); }
						if (e.keyCode === 27) { input.value = input.getAttribute("data-server") || ""; input.blur(); }
					});
					input.addEventListener("blur", commit);
					input.addEventListener("mousedown", function (e) { e.stopPropagation(); });
					node = input;
					controls[item.name] = { kind: "text", node: input };
					break;
				}
				case "button": {
					var b = text("button", "btn", item.text);
					b.type = "button";
					b.addEventListener("click", function (e) { e.stopPropagation(); sendControl(item.name, null); });
					node = b;
					controls[item.name] = { kind: "button", node: b };
					break;
				}
			}
			if (node && item.tip) node.title = item.tip;
			return node;
		}

		function updateControl(item, ctl) {
			if (!ctl) return;
			var pending = item.name && pendingUntil[item.name] && now() < pendingUntil[item.name];
			var enabled = item.enabled !== false;
			switch (ctl.kind) {
				case "label":
					setTextWithIcons(ctl.node, item.text);
					ctl.node.style.color = meaningfulColor(item.color) || "";
					break;
				case "check":
					if (!pending) ctl.api.set(item.checked);
					ctl.api.setText(item.text);
					ctl.api.setEnabled(enabled);
					break;
				case "combo":
					ctl.api.setItems(item.items || []);
					if (!pending) ctl.api.setIndex(item.index);
					ctl.api.setWarn(!!item.warn);
					ctl.api.setEnabled(enabled);
					break;
				case "text":
					ctl.node.setAttribute("data-server", item.value || "");
					if (!pending && document.activeElement !== ctl.node) ctl.node.value = item.value || "";
					ctl.node.classList.toggle("afps-warn", !!item.warn);
					ctl.node.disabled = !enabled;
					ctl.node.classList.toggle("wide", String(item.value || "").length > 4);
					break;
				case "button":
					ctl.node.textContent = item.text;
					ctl.node.disabled = !enabled;
					break;
			}
			if (item.tip) (ctl.node || (ctl.api && ctl.api.root)).title = item.tip;
		}

		function renderPage(name, data) {
			var st = pageState[name];
			var container = name === "tuning" ? tuningDyn : settingsDyn;
			var cards = data.cards || [];
			var key = structureKey(cards);
			if (key !== st.key) {
				// Structure changed (sections shown/hidden by the app): rebuild, unless the user is typing in it.
				if (document.activeElement && container.contains(document.activeElement)) return;
				st.key = key;
				st.controls = { __labels: [] };
				container.textContent = "";
				for (var i = 0; i < cards.length; i++) {
					var card = el("div", "card");
					var rows = cards[i].rows || [];
					for (var r = 0; r < rows.length; r++) {
						var row = rows[r];
						if (!Array.isArray(row)) {
							if (row && row.t === "title") card.appendChild(text("div", "card-title", row.text));
							continue;
						}
						var rowEl = el("div", "row");
						for (var j = 0; j < row.length; j++) {
							var node = buildControl(row[j], st.controls);
							if (node) rowEl.appendChild(node);
						}
						card.appendChild(rowEl);
					}
					container.appendChild(card);
				}
				scheduleSnap();
			}
			// Values
			var lblIndex = 0;
			for (var ci2 = 0; ci2 < cards.length; ci2++) {
				var rws = cards[ci2].rows || [];
				for (var ri = 0; ri < rws.length; ri++) {
					if (!Array.isArray(rws[ri])) continue;
					for (var ii = 0; ii < rws[ri].length; ii++) {
						var it = rws[ri][ii];
						if (it.t === "label") { updateControl(it, st.controls.__labels ? st.controls.__labels[lblIndex++] : null); continue; }
						if (it.name) updateControl(it, st.controls[it.name]);
					}
				}
			}
		}

		var pageBusy = { tuning: false, settings: false };
		function fetchPage(name, done) {
			if (!connected || !pageState[name] || pageBusy[name]) return;
			pageBusy[name] = true;
			fetch(API + "/api/page/" + name, { cache: "no-store" })
				.then(function (res) { if (!res.ok) throw new Error("HTTP " + res.status); return res.json(); })
				.then(function (data) {
					pageBusy[name] = false;
					renderPage(name, data);
					if (done) done();
				})
				.catch(function () { pageBusy[name] = false; if (done) done(); });
		}
		/** Active page refreshes every second. */
		function pollPage() {
			if (activeTab !== "overview") fetchPage(activeTab);
		}
		/** Hidden pages refresh slowly so they are ready (and the right size) before they are opened. */
		function prefetchPages() {
			for (var n in pageState) if (n !== activeTab) fetchPage(n);
		}

		// ---------------------------------------------------------------- show/hide hotkey (configured in the app config)
		var hotkey = null, lastHotkeyStr = null;
		function syncHotkey(raw) {
			var str = typeof raw === "string" ? raw.trim() : JSON.stringify(raw || "");
			if (str === lastHotkeyStr) return;
			lastHotkeyStr = str;
			if (!str || str === "None" || str === "null" || str === "{}" || str === '""') { hotkey = null; return; }
			try { hotkey = typeof raw === "object" ? raw : JSON.parse(str); } catch (e) { hotkey = null; }
		}

		var isHotkeyHidden = false, isCameraHidden = false;
		function updateWidgetVisibility() {
			var hide = isHotkeyHidden || isCameraHidden;
			document.body.classList.toggle("widget-hidden", hide);
			if (ingameUi) ingameUi.classList.toggle("widget-hidden", hide);
		}

		window.addEventListener("keydown", function (e) {
			if (!hotkey || e.repeat) return;
			if (e.keyCode !== hotkey.keyCode) return;
			if (!!e.altKey !== !!hotkey.alt || !!e.ctrlKey !== !!hotkey.ctrl || !!e.shiftKey !== !!hotkey.shift || !!e.metaKey !== !!hotkey.meta) return;
			e.preventDefault();
			e.stopPropagation();
			isHotkeyHidden = !isHotkeyHidden;
			updateWidgetVisibility();
		}, true);

		// ---------------------------------------------------------------- camera auto-hide (in-game)
		function checkCameraAutoHide() {
			if (!autoHideIndex) {
				if (isCameraHidden) { isCameraHidden = false; updateWidgetVisibility(); }
				return;
			}
			var camera = null;
			try {
				if (window.SimVar && typeof SimVar.GetSimVarValue === "function" && (typeof SimVar.IsReady !== "function" || SimVar.IsReady())) {
					camera = SimVar.GetSimVarValue("CAMERA STATE", "Enum");
				}
			} catch (e) { }
			var hide = false;
			if (typeof camera === "number") {
				if (autoHideIndex === 1) hide = (camera === 3);
				else if (autoHideIndex === 2) hide = (camera >= 3 && camera <= 9);
			}
			if (hide !== isCameraHidden) { isCameraHidden = hide; updateWidgetVisibility(); }
		}

		// ---------------------------------------------------------------- in-game window behaviour
		function isInteractive(e) {
			if (!e.target || !e.target.closest) return false;
			if (e.target.closest("button, input, .select, .check, .stepper, .tab")) return true;
			var icon = e.target.closest(".fps-source-icon");
			return !!(icon && icon.getAttribute("data-clickable") === "true");
		}

		if (IS_INGAME) {
			// Double-click empty space: toggle the title bar (shared with the app setting)
			var lastClick = 0, lastX = 0, lastY = 0;
			var blockDbl = function (e) {
				if (isInteractive(e)) return;
				e.preventDefault();
				e.stopPropagation();
			};
			window.addEventListener("dblclick", blockDbl, true);
			document.addEventListener("dblclick", blockDbl, true);
			document.addEventListener("click", function (e) {
				if (isInteractive(e)) { lastClick = 0; return; }
				var t = now(), dx = e.clientX - lastX, dy = e.clientY - lastY;
				if (lastClick && t - lastClick <= 500 && dx * dx + dy * dy <= 1600) {
					lastClick = 0;
					post("/api/toggletitlebar");
					return;
				}
				lastClick = t; lastX = e.clientX; lastY = e.clientY;
			}, true);

			// Drag the panel from any empty area by forwarding presses to the sim's header
			var header = function () { return document.querySelector("ingame-ui-header"); };
			var forward = function (type, e, bubbles) {
				var h = header();
				if (!h) return;
				try {
					h.dispatchEvent(new MouseEvent(type, {
						bubbles: bubbles, cancelable: true, view: window,
						clientX: e.clientX, clientY: e.clientY, screenX: e.screenX, screenY: e.screenY, button: 0, buttons: 1
					}));
				} catch (err) { }
			};
			var hoverFwd = false;
			var setHover = function (on, e) {
				if (on === hoverFwd) return;
				hoverFwd = on;
				forward(on ? "mouseover" : "mouseout", e, true);
				forward(on ? "mouseenter" : "mouseleave", e, false);
			};
			document.addEventListener("mousedown", function (e) {
				if (e.button !== 0 || isInteractive(e)) return;
				if (e.target && e.target.closest && e.target.closest("ingame-ui-header")) return;
				forward("mousedown", e, true);
			}, true);
			document.addEventListener("mousemove", function (e) {
				if (e.target && e.target.closest && e.target.closest("ingame-ui-header")) { hoverFwd = false; return; }
				setHover(!isInteractive(e), e);
			}, true);
			document.addEventListener("mouseout", function (e) {
				if (!e.relatedTarget) setHover(false, e);
			}, true);
		}

		// ---------------------------------------------------------------- height snap & scaling
		var lastSnapHeight = -1, snapTimer = null;
		function snapHeight() {
			snapTimer = null;
			if (!ingameUi || typeof ingameUi.sendRect !== "function") return;
			var frame = ingameUi.getBoundingClientRect();
			var last = null;
			var kids = main.children;
			for (var i = kids.length - 1; i >= 0; i--) {
				if (!kids[i].classList.contains("hidden")) { last = kids[i]; break; }
			}
			if (!last || !frame.height) return;
			var rect = last.getBoundingClientRect();
			var needed = Math.round(rect.bottom - frame.top + 10);
			if (needed < 48 || !isFinite(needed) || needed > 4000) return;
			if (lastSnapHeight >= 0 && Math.abs(needed - lastSnapHeight) <= 3) return;
			lastSnapHeight = needed;
			ingameUi.style.height = needed + "px";
			ingameUi.sendRect();
		}
		function scheduleSnap() {
			if (snapTimer) clearTimeout(snapTimer);
			snapTimer = setTimeout(snapHeight, 200);
		}
		function snapNow() {
			if (snapTimer) { clearTimeout(snapTimer); snapTimer = null; }
			snapHeight();
		}

		var lastWidth = -1;
		function applyScale() {
			var w = IS_INGAME ? (document.documentElement.clientWidth || main.clientWidth) : Math.min(document.documentElement.clientWidth || 440, 560);
			if (!w || w === lastWidth) { scheduleSnap(); return; }
			lastWidth = w;
			var scale = w / BASE_WIDTH;
			if (scale < MIN_SCALE) scale = MIN_SCALE;
			if (scale > MAX_SCALE) scale = MAX_SCALE;
			document.documentElement.style.fontSize = (14 * scale) + "px";
			scheduleSnap();
		}
		window.addEventListener("resize", applyScale);
		if (ingameUi) ingameUi.addEventListener("resize", applyScale);
		setInterval(applyScale, 250);

		// ---------------------------------------------------------------- start
		for (var k in tabButtons) tabButtons[k].classList.toggle("active", k === activeTab);
		applyScale();
		pollStatus();
		setInterval(pollStatus, POLL_STATUS_MS);
		setInterval(pollPage, POLL_PAGE_MS);
		setInterval(prefetchPages, POLL_PAGE_MS * 4);
		setInterval(checkCameraAutoHide, 150);
	}

	init();
})();

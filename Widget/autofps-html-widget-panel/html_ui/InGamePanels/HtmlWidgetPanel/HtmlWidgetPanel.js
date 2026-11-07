// ============================================================
// HtmlWidgetPanel.js
// ============================================================
const STATUS_URL = "http://localhost:54321/api/status";
const RESET_URL = "http://localhost:54321/api/reset";
const FLIGHT_TYPE_URL = "http://localhost:54321/api/flighttype";
const TOGGLE_TITLEBAR_URL = "http://localhost:54321/api/toggletitlebar";
const TOGGLE_DARKMODE_URL = "http://localhost:54321/api/toggledarkmode";
const TOGGLE_EXPERT_URL = "http://localhost:54321/api/toggleexpert";
const TOGGLE_LOGPLUS_URL = "http://localhost:54321/api/togglelogplus";
const HOTKEY_URL = "http://localhost:54321/api/hotkey";
const TARGET_MODE_URL = "http://localhost:54321/api/targetmode";
const TARGET_URL = "http://localhost:54321/api/target";
const TARGET_FPS_URL = "http://localhost:54321/api/targetfps";
const GENERAL_COLLAPSED_URL = "http://localhost:54321/api/generalcollapsed";
const AUTO_HIDE_URL = "http://localhost:54321/api/widgetautohide";
const POLL_INTERVAL_MS = 500;

// EXPERIMENTAL: drag the window from any empty area, not just the title bar.
const DRAG_ANYWHERE_ENABLED = true;

// Target FPS stepper
const FPS_MIN = 10;
const FPS_MAX = 500;
const FPS_HOLD_DELAY_MS = 400;     // hold this long before repeating starts
const FPS_REPEAT_MS = 125;         // repeat interval while held
const FPS_STEP5_AFTER_MS = 2000;   // held this long: step by 5
const FPS_STEP10_AFTER_MS = 4000;  // held this long: step by 10
const FPS_SEND_DEBOUNCE_MS = 450;

// Base width for scaling
const BASE_WIDTH = 420;
const MIN_SCALE = 0.6;
const MAX_SCALE = 3.0;

const DISCONNECTED_BG = "#121418";
const DISCONNECTED_TEXT = "#F0F0F0";

(function () {
	function init() {
		const statusEl = document.getElementById("widget-status");
		const mainSection = document.getElementById("mainSection");
		const contentEl = document.getElementById("widget-content");
		const closeBtn = document.getElementById("widget-close-btn");
		const groupBoxes = document.querySelectorAll(".group-box");
		const groupHeaders = document.querySelectorAll(".group-header");
		const resetBtn = document.getElementById("resetBtn");
		const flightTypeDropdown = document.getElementById("flightTypeDropdown");
		const flightTypeBtn = document.getElementById("flightTypeBtn");
		const flightTypeList = document.getElementById("flightTypeList");
		const darkModeChk = document.getElementById("darkModeChk");
		const expertChk = document.getElementById("expertChk");
		const logPlusChk = document.getElementById("logPlusChk");
		const logPlusLabel = document.getElementById("logPlusLabel");
		const customCheckboxes = document.querySelectorAll(".custom-checkbox");
		const titleEl = document.getElementById("widget-title");
		const hotkeyBtn = document.getElementById("widget-hotkey-btn");
		const autoHideGroup = document.getElementById("autoHideGroup");
		const autoHideBtn = document.getElementById("autoHideBtn");
		const autoHideList = document.getElementById("autoHideList");
		const modeDropdown = document.getElementById("modeDropdown");
		const modeBtn = document.getElementById("modeBtn");
		const modeList = document.getElementById("modeList");
		const targetDropdown = document.getElementById("targetDropdown");
		const targetBtn = document.getElementById("targetBtn");
		const targetList = document.getElementById("targetList");
		const fpsDecBtn = document.getElementById("fpsDecBtn");
		const fpsIncBtn = document.getElementById("fpsIncBtn");
		const fpsValEl = document.getElementById("fpsVal");
		const generalHeader = document.getElementById("generalHeader");
		const generalChevron = document.getElementById("generalChevron");
		const generalControls = document.getElementById("generalControls");

		// ---- Collapsible General Controls ----
		var isControlsCollapsed = false;
		var suppressGeneralCollapsedSyncUntil = 0;
		try {
			isControlsCollapsed = localStorage.getItem("generalControlsCollapsed") === "true";
		} catch (e) { }

		var SVG_DOWN = '<svg viewBox="0 0 10 10"><polygon points="1,1 9,1 5,9" fill="currentColor"/></svg>';
		var SVG_RIGHT = '<svg viewBox="0 0 10 10"><polygon points="1,1 1,9 9,5" fill="currentColor"/></svg>';

		function updateGeneralControlsVisibility() {
			if (!generalControls) return;
			generalControls.classList.toggle("hidden", isControlsCollapsed);
			if (generalChevron) {
				generalChevron.innerHTML = isControlsCollapsed ? SVG_RIGHT : SVG_DOWN;
			}
			scheduleSnap();
		}

		if (generalHeader) {
			generalHeader.addEventListener("click", function (e) {
				e.stopPropagation();
				suppressGeneralCollapsedSyncUntil = Date.now() + 1500;
				isControlsCollapsed = !isControlsCollapsed;
				updateGeneralControlsVisibility();

				// Post updated state to C# backend
				fetch(GENERAL_COLLAPSED_URL, {
					method: "POST",
					cache: "no-store",
					body: isControlsCollapsed ? "true" : "false"
				}).catch(function () { });
			});
		}

		updateGeneralControlsVisibility();
		if (!statusEl || !mainSection || !contentEl) {
			setTimeout(init, 100);
			return;
		}

		if (darkModeChk) {
			darkModeChk.addEventListener("click", function (e) {
				e.stopPropagation();
				fetch(TOGGLE_DARKMODE_URL, { method: "POST", cache: "no-store" }).catch(function () { });
			});
		}

		if (expertChk) {
			expertChk.addEventListener("click", function (e) {
				e.stopPropagation();
				fetch(TOGGLE_EXPERT_URL, { method: "POST", cache: "no-store" }).catch(function () { });
			});
		}

		if (logPlusChk) {
			logPlusChk.addEventListener("click", function (e) {
				e.stopPropagation();
				fetch(TOGGLE_LOGPLUS_URL, { method: "POST", cache: "no-store" }).catch(function () { });
			});
		}

		var lastProfilesKey = null;
		var currentProfiles = [];
		var suppressSyncUntil = 0;
		var currentActiveIndex = -1;

		if (closeBtn) {
			closeBtn.addEventListener("click", function () {
				if (window.Coherent && typeof Coherent.call === "function") {
					Coherent.call("TOOLBAR_BUTTON_TOGGLE", "HTML_WIDGET_PANEL", false);
				}
			});
		}

		// ---- show/hide hotkey ----
		var hotkey = null;
		var lastServerHotkeyStr = "";
		var suppressHotkeySyncUntil = 0;
		var listening = false;
		var listeningArmed = false;

		var MODIFIER_KEYCODES = { 16: 1, 17: 1, 18: 1, 91: 1, 92: 1, 93: 1, 224: 1 };
		var KEYCODE_LABELS = {
			8: "Backspace", 9: "Tab", 13: "Enter", 19: "Pause", 20: "CapsLock",
			27: "Esc", 32: "Space", 33: "PgUp", 34: "PgDn", 35: "End", 36: "Home",
			37: "Left", 38: "Up", 39: "Right", 40: "Down", 45: "Insert", 46: "Delete",
			106: "Num*", 107: "Num+", 109: "Num-", 110: "Num.", 111: "Num/",
			186: ";", 187: "=", 188: ",", 189: "-", 190: ".", 191: "/",
			192: "`", 219: "[", 220: "\\", 221: "]", 222: "'"
		};

		function labelForKeyCode(kc) {
			if (KEYCODE_LABELS[kc]) return KEYCODE_LABELS[kc];
			if (kc >= 48 && kc <= 57) return String.fromCharCode(kc);
			if (kc >= 65 && kc <= 90) return String.fromCharCode(kc);
			if (kc >= 96 && kc <= 105) return "Num" + (kc - 96);
			if (kc >= 112 && kc <= 123) return "F" + (kc - 111);
			return "Key" + kc;
		}

		function labelForHotkey(hk) {
			if (!hk) return "No Hotkey";
			var parts = [];
			if (hk.ctrl) parts.push("Ctrl");
			if (hk.alt) parts.push("Alt");
			if (hk.shift) parts.push("Shift");
			if (hk.meta) parts.push("Win");
			parts.push(hk.label || labelForKeyCode(hk.keyCode));
			return parts.join("+");
		}

		function updateHotkeyDisplay() {
			if (!hotkeyBtn) return;
			var labelEl = document.querySelector(".hotkey-label");
			if (!hotkey) {
				if (labelEl) labelEl.style.display = "none";
				hotkeyBtn.textContent = "Show/Hide";
				hotkeyBtn.title = "Click to configure a Show/Hide hotkey";
			} else {
				if (labelEl) labelEl.style.display = "";
				hotkeyBtn.textContent = labelForHotkey(hotkey);
				hotkeyBtn.title = "Click to clear this hotkey";
			}
		}

		function saveHotkey(hk) {
			hotkey = hk;
			var val = hk ? JSON.stringify(hk) : "";
			lastServerHotkeyStr = val;
			suppressHotkeySyncUntil = Date.now() + 2000;
			updateHotkeyDisplay();
			fetch(HOTKEY_URL, {
				method: "POST",
				cache: "no-store",
				body: val
			}).catch(function () { });
		}

		function syncHotkeyFromServer(raw) {
			if (Date.now() < suppressHotkeySyncUntil || listening) return;
			var str = (raw || "").trim();
			if (str === lastServerHotkeyStr) return;
			lastServerHotkeyStr = str;

			if (!str || str === "None" || str === "null" || str === "{}") {
				hotkey = null;
			} else {
				try {
					hotkey = typeof raw === "object" ? raw : JSON.parse(str);
				} catch (e) {
					hotkey = null;
				}
			}
			updateHotkeyDisplay();
		}

		function startListening() {
			listening = true;
			listeningArmed = false;
			if (hotkeyBtn) {
				hotkeyBtn.classList.add("listening");
				hotkeyBtn.style.backgroundColor = "#c0392b";
				hotkeyBtn.style.color = "#ffffff";
				hotkeyBtn.textContent = "Press a key...";
			}
			setTimeout(function () { listeningArmed = true; }, 50);
		}

		function stopListening() {
			listening = false;
			if (hotkeyBtn) {
				hotkeyBtn.classList.remove("listening");
				hotkeyBtn.style.backgroundColor = "";
				hotkeyBtn.style.color = "";
			}
			updateHotkeyDisplay();
		}

		if (hotkeyBtn) {
			hotkeyBtn.addEventListener("click", function (e) {
				e.stopPropagation();
				if (listening) {
					saveHotkey(null);
					stopListening();
					return;
				}
				if (hotkey) {
					saveHotkey(null);
					hotkeyBtn.textContent = "Set Hotkey";
					return;
				}
				startListening();
			});

			hotkeyBtn.addEventListener("mouseenter", function () {
				if (listening) return;
				hotkeyBtn.textContent = hotkey ? "Clear Hotkey" : "Set Hotkey";
			});

			hotkeyBtn.addEventListener("mouseleave", function () {
				if (listening) return;
				updateHotkeyDisplay();
			});
		}

		updateHotkeyDisplay();

		window.addEventListener("keydown", function (e) {
			if (!listening || !listeningArmed) return;
			if (MODIFIER_KEYCODES[e.keyCode]) return;
			e.preventDefault();
			e.stopPropagation();
			if (typeof e.stopImmediatePropagation === "function") e.stopImmediatePropagation();

			if (e.keyCode === 27 || e.keyCode === 8 || e.keyCode === 46) {
				saveHotkey(null);
				stopListening();
				return;
			}

			saveHotkey({
				keyCode: e.keyCode,
				label: labelForKeyCode(e.keyCode),
				alt: e.altKey, ctrl: e.ctrlKey, shift: e.shiftKey, meta: e.metaKey
			});
			stopListening();
		}, true);

		var isHidden = false;
		var isCameraHidden = false;

		function updateWidgetVisibility() {
			var shouldHide = isHidden || isCameraHidden;
			document.body.classList.toggle("widget-hidden", shouldHide);
			var ingameUi = document.getElementById("HtmlWidgetPanel");
			if (ingameUi) {
				ingameUi.classList.toggle("widget-hidden", shouldHide);
			}
		}

		window.addEventListener("keydown", function (e) {
			if (listening || !hotkey || e.repeat) return;
			if (e.keyCode !== hotkey.keyCode) return;
			if (!!e.altKey !== !!hotkey.alt) return;
			if (!!e.ctrlKey !== !!hotkey.ctrl) return;
			if (!!e.shiftKey !== !!hotkey.shift) return;
			if (!!e.metaKey !== !!hotkey.meta) return;
			e.preventDefault();
			e.stopPropagation();
			if (typeof e.stopImmediatePropagation === "function") e.stopImmediatePropagation();

			isHidden = !isHidden;
			updateWidgetVisibility();
		}, true);

		if (resetBtn) {
			resetBtn.addEventListener("click", function () {
				resetBtn.disabled = true;
				fetch(RESET_URL, { method: "POST", cache: "no-store" })
					.catch(function () { })
					.then(function () {
						setTimeout(function () { resetBtn.disabled = false; }, 1000);
					});
			});
		}

		// Title bar toggle on double click
		var DOUBLE_CLICK_MS = 500;
		var DOUBLE_CLICK_PX = 40;
		var lastClickTime = 0;
		var lastClickX = 0;
		var lastClickY = 0;

		function isInteractiveTarget(e) {
			if (e.target && e.target.closest) {
				if (e.target.closest("button, .custom-select, input, .chk-label, .stepper, .collapsible-header")) return true;
				var fpsIcon = e.target.closest(".fps-source-icon");
				if (fpsIcon && fpsIcon.dataset.clickable === "true") return true;
			}
			return false;
		}
		function blockNativeDblClick(e) {
			if (isInteractiveTarget(e)) return;
			e.preventDefault();
			e.stopPropagation();
			if (typeof e.stopImmediatePropagation === "function") e.stopImmediatePropagation();
		}

		function onClickForDoubleClick(e) {
			if (isInteractiveTarget(e)) {
				lastClickTime = 0;
				return;
			}

			var now = Date.now();
			var dx = e.clientX - lastClickX;
			var dy = e.clientY - lastClickY;
			var closeEnough = (dx * dx + dy * dy) <= (DOUBLE_CLICK_PX * DOUBLE_CLICK_PX);

			if (lastClickTime && (now - lastClickTime) <= DOUBLE_CLICK_MS && closeEnough) {
				lastClickTime = 0;
				fetch(TOGGLE_TITLEBAR_URL, { method: "POST", cache: "no-store" }).catch(function () { });
				return;
			}

			lastClickTime = now;
			lastClickX = e.clientX;
			lastClickY = e.clientY;
		}

		window.addEventListener("dblclick", blockNativeDblClick, true);
		document.addEventListener("dblclick", blockNativeDblClick, true);
		document.addEventListener("click", onClickForDoubleClick, true);

		// Drag window anywhere
		function forwardPressToHeader(e) {
			if (!DRAG_ANYWHERE_ENABLED || e.button !== 0 || isInteractiveTarget(e)) return;
			if (e.target && e.target.closest && e.target.closest("ingame-ui-header")) return;

			var headerEl = document.querySelector("ingame-ui-header");
			if (!headerEl) return;

			try {
				headerEl.dispatchEvent(new MouseEvent("mousedown", {
					bubbles: true, cancelable: true, view: window,
					clientX: e.clientX, clientY: e.clientY, screenX: e.screenX, screenY: e.screenY,
					button: 0, buttons: 1
				}));
			} catch (err) { }
		}
		document.addEventListener("mousedown", forwardPressToHeader, true);

		var headerHoverForwarded = false;
		function dispatchToHeader(type, bubbles, e) {
			var headerEl = document.querySelector("ingame-ui-header");
			if (!headerEl) return;
			try {
				headerEl.dispatchEvent(new MouseEvent(type, {
					bubbles: bubbles, cancelable: false, view: window,
					clientX: e.clientX, clientY: e.clientY, screenX: e.screenX, screenY: e.screenY
				}));
			} catch (err) { }
		}

		function setHeaderHover(on, e) {
			if (on === headerHoverForwarded) return;
			headerHoverForwarded = on;
			if (on) {
				dispatchToHeader("mouseover", true, e);
				dispatchToHeader("mouseenter", false, e);
			} else {
				dispatchToHeader("mouseout", true, e);
				dispatchToHeader("mouseleave", false, e);
			}
		}

		var lastDeadSpacePos = null;
		document.addEventListener("mousemove", function (e) {
			if (!DRAG_ANYWHERE_ENABLED) return;
			var t = e.target;
			var overHeader = !!(t && t.closest && t.closest("ingame-ui-header"));
			if (overHeader) {
				headerHoverForwarded = false;
				lastDeadSpacePos = null;
				return;
			}
			var dead = !isInteractiveTarget(e);
			lastDeadSpacePos = dead ? { clientX: e.clientX, clientY: e.clientY, screenX: e.screenX, screenY: e.screenY } : null;
			setHeaderHover(dead, e);
		}, true);

		document.addEventListener("mouseout", function (e) {
			if (!DRAG_ANYWHERE_ENABLED) return;
			if (!e.relatedTarget) {
				lastDeadSpacePos = null;
				setHeaderHover(false, e);
			}
		}, true);

		document.addEventListener("mouseup", function (e) {
			if (!DRAG_ANYWHERE_ENABLED || e.button !== 0) return;
			setTimeout(function () {
				if (lastDeadSpacePos) {
					headerHoverForwarded = false;
					setHeaderHover(true, lastDeadSpacePos);
				}
			}, 30);
		}, true);

		// Flight Type dropdown
		function displayProfileName(name) {
			return String(name).replace(/\u2194/g, "<->");
		}

		function closeFlightTypeList() {
			if (flightTypeList) flightTypeList.classList.add("hidden");
		}

		function selectFlightType(idx) {
			suppressSyncUntil = Date.now() + 1500;
			currentActiveIndex = idx;
			if (flightTypeBtn && currentProfiles[idx] !== undefined) {
				flightTypeBtn.textContent = displayProfileName(currentProfiles[idx]);
			}
			closeFlightTypeList();
			fetch(FLIGHT_TYPE_URL, {
				method: "POST",
				cache: "no-store",
				body: String(idx)
			}).catch(function (err) {
				statusEl.textContent = "Flight Type change failed";
			});
		}

		function renderFlightTypeList(profiles) {
			currentProfiles = profiles;
			flightTypeList.textContent = "";
			profiles.forEach(function (name, i) {
				var item = document.createElement("div");
				item.className = "custom-select-item";
				item.textContent = displayProfileName(name);
				item.dataset.index = String(i);
				item.addEventListener("click", function () {
					selectFlightType(i);
				});
				flightTypeList.appendChild(item);
			});
		}

		function markActiveItem(idx) {
			if (!flightTypeList) return;
			var children = flightTypeList.children;
			for (var i = 0; i < children.length; i++) {
				children[i].classList.toggle("active", i === idx);
			}
		}

		function closeAllLists() {
			[flightTypeList, modeList, targetList, autoHideList].forEach(function (l) {
				if (l) l.classList.add("hidden");
			});
		}

		// Auto-Hide dropdown
		var autoHideItems = ["Off", "Chase", "All Ext"];
		var currentAutoHideIndex = 0;
		var suppressAutoHideSyncUntil = 0;

		function selectAutoHide(idx) {
			suppressAutoHideSyncUntil = Date.now() + 1500;
			currentAutoHideIndex = idx;
			if (autoHideBtn && autoHideItems[idx] !== undefined) {
				autoHideBtn.textContent = autoHideItems[idx];
			}
			markActive(autoHideList, idx);
			closeAllLists();
			fetch(AUTO_HIDE_URL, {
				method: "POST",
				cache: "no-store",
				body: String(idx)
			}).catch(function () { });
		}

		if (autoHideList) {
			renderList(autoHideList, autoHideItems, selectAutoHide);
		}

		if (autoHideBtn && autoHideList) {
			autoHideBtn.addEventListener("click", function (e) {
				e.stopPropagation();
				var wasHidden = autoHideList.classList.contains("hidden");
				closeAllLists();
				if (wasHidden) autoHideList.classList.remove("hidden");
			});
		}

		if (flightTypeBtn && flightTypeList) {
			flightTypeBtn.addEventListener("click", function (e) {
				e.stopPropagation();
				var wasHidden = flightTypeList.classList.contains("hidden");
				closeAllLists();
				if (wasHidden) flightTypeList.classList.remove("hidden");
			});
		}
		document.addEventListener("click", function (e) {
			if (e.target && e.target.closest && e.target.closest(".custom-select")) return;
			closeAllLists();
		});

		// Mode dropdown
		var lastModeKey = null;
		var modeItems = [];
		var currentModeIndex = -1;
		var suppressModeSyncUntil = 0;

		function pickMode(idx) {
			flushFpsSend();
			suppressModeSyncUntil = Date.now() + 1500;
			currentModeIndex = idx;
			if (modeBtn && modeItems[idx] !== undefined) modeBtn.textContent = modeItems[idx];
			markActive(modeList, idx);
			closeAllLists();
			fetch(TARGET_MODE_URL, { method: "POST", cache: "no-store", body: String(idx) }).catch(function () { });
		}

		// Target dropdown
		var lastTargetKey = null;
		var targetListKind = "auto";
		var targetItems = [];
		var targetSwapIndex = -1;
		var currentTargetIndex = -1;
		var suppressTargetSyncUntil = 0;

		function pickTarget(idx) {
			flushFpsSend();
			closeAllLists();
			if (idx !== targetSwapIndex) {
				suppressTargetSyncUntil = Date.now() + 1500;
				currentTargetIndex = idx;
				if (targetBtn && targetItems[idx] !== undefined) targetBtn.textContent = targetItems[idx];
				markActive(targetList, idx);
			}
			fetch(TARGET_URL, { method: "POST", cache: "no-store", body: targetListKind + ":" + idx }).catch(function () { });
		}

		function renderList(listEl, items, onPick) {
			listEl.textContent = "";
			items.forEach(function (name, i) {
				var item = document.createElement("div");
				item.className = "custom-select-item";
				item.textContent = String(name);
				item.dataset.index = String(i);
				item.addEventListener("click", function () {
					onPick(i);
				});
				listEl.appendChild(item);
			});
		}

		function markActive(listEl, idx) {
			if (!listEl) return;
			var children = listEl.children;
			for (var i = 0; i < children.length; i++) {
				children[i].classList.toggle("active", i === idx);
			}
		}

		if (modeBtn && modeList) {
			modeBtn.addEventListener("click", function (e) {
				e.stopPropagation();
				var wasHidden = modeList.classList.contains("hidden");
				closeAllLists();
				if (wasHidden) modeList.classList.remove("hidden");
			});
		}
		if (targetBtn && targetList) {
			targetBtn.addEventListener("click", function (e) {
				e.stopPropagation();
				var wasHidden = targetList.classList.contains("hidden");
				closeAllLists();
				if (wasHidden) targetList.classList.remove("hidden");
			});
		}

		// Target FPS stepper
		var fpsValue = null;
		var suppressFpsSyncUntil = 0;
		var fpsDirty = false;
		var fpsSendTimer = null;
		var holdDir = 0;
		var holdStart = 0;
		var holdDelayTimer = null;
		var holdRepeatTimer = null;

		function formatFps(v) {
			if (v === null || !isFinite(v)) return "-";
			return String(Math.round(v * 10) / 10);
		}

		function showFps() {
			if (fpsValEl) fpsValEl.textContent = formatFps(fpsValue);
		}

		function flushFpsSend() {
			if (fpsSendTimer) { clearTimeout(fpsSendTimer); fpsSendTimer = null; }
			if (!fpsDirty || fpsValue === null) return;
			fpsDirty = false;
			suppressFpsSyncUntil = Date.now() + 1500;
			fetch(TARGET_FPS_URL, { method: "POST", cache: "no-store", body: String(fpsValue) }).catch(function () { });
		}

		function queueFpsSend() {
			fpsDirty = true;
			if (fpsSendTimer) clearTimeout(fpsSendTimer);
			fpsSendTimer = setTimeout(flushFpsSend, FPS_SEND_DEBOUNCE_MS);
		}

		function stepFps(dir, step) {
			if (fpsValue === null) return;
			var v = fpsValue;
			var nv;
			if (step <= 1) nv = v + dir;
			else if (dir > 0) nv = (Math.floor(v / step) + 1) * step;
			else nv = (Math.ceil(v / step) - 1) * step;
			if (nv < FPS_MIN) nv = FPS_MIN;
			if (nv > FPS_MAX) nv = FPS_MAX;
			nv = Math.round(nv * 2) / 2;
			if (nv === v) return;
			fpsValue = nv;
			showFps();
			suppressFpsSyncUntil = Date.now() + 2500;
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
			holdStart = Date.now();
			stepFps(dir, 1);
			holdDelayTimer = setTimeout(function () {
				holdDelayTimer = null;
				holdRepeatTimer = setInterval(function () {
					var held = Date.now() - holdStart;
					var step = held >= FPS_STEP10_AFTER_MS ? 10 : (held >= FPS_STEP5_AFTER_MS ? 5 : 1);
					stepFps(holdDir, step);
				}, FPS_REPEAT_MS);
			}, FPS_HOLD_DELAY_MS);
		}

		function wireStepperButton(btn, dir) {
			if (!btn) return;
			btn.addEventListener("mousedown", function (e) {
				if (e.button !== 0) return;
				e.stopPropagation();
				startHold(dir);
			});
			btn.addEventListener("mouseleave", stopHold);
		}
		wireStepperButton(fpsDecBtn, -1);
		wireStepperButton(fpsIncBtn, 1);
		window.addEventListener("mouseup", stopHold, true);
		window.addEventListener("blur", stopHold);

		function applyControls(c) {
			if (!c) return;

			if (Date.now() >= suppressGeneralCollapsedSyncUntil && typeof c.generalCollapsed === "boolean") {
				if (isControlsCollapsed !== c.generalCollapsed) {
					isControlsCollapsed = c.generalCollapsed;
					updateGeneralControlsVisibility();
				}
			}

			// Mode
			if (modeBtn && modeList && Array.isArray(c.modeItems)) {
				var mk = JSON.stringify(c.modeItems);
				if (mk !== lastModeKey) {
					lastModeKey = mk;
					modeItems = c.modeItems;
					renderList(modeList, modeItems, pickMode);
				}
				if (Date.now() >= suppressModeSyncUntil && typeof c.mode === "number") {
					currentModeIndex = c.mode;
					if (modeItems[currentModeIndex] !== undefined) modeBtn.textContent = modeItems[currentModeIndex];
				}
				markActive(modeList, currentModeIndex);

				if (c.modeOrange) {
					modeBtn.style.backgroundColor = "#FFA500";
					modeBtn.style.color = "#000000";
				}
			}

			// Target
			if (targetBtn && targetList && Array.isArray(c.targetItems)) {
				var tk = String(c.targetList) + "|" + JSON.stringify(c.targetItems);
				if (tk !== lastTargetKey) {
					lastTargetKey = tk;
					targetListKind = String(c.targetList);
					targetItems = c.targetItems;
					renderList(targetList, targetItems, pickTarget);
					suppressTargetSyncUntil = 0;
				}
				targetSwapIndex = (typeof c.targetSwapIndex === "number") ? c.targetSwapIndex : -1;
				if (Date.now() >= suppressTargetSyncUntil && typeof c.targetIndex === "number") {
					currentTargetIndex = c.targetIndex;
					if (targetItems[currentTargetIndex] !== undefined) targetBtn.textContent = targetItems[currentTargetIndex];
				}
				markActive(targetList, currentTargetIndex);

				if (c.targetOrange) {
					targetBtn.style.backgroundColor = "#FFA500";
					targetBtn.style.color = "#000000";
				}
			}

			// FPS
			var editable = !!c.fpsEditable;
			var btnVis = editable ? "visible" : "hidden";
			if (fpsDecBtn) fpsDecBtn.style.visibility = btnVis;
			if (fpsIncBtn) fpsIncBtn.style.visibility = btnVis;

			if (!editable) {
				stopHold();
				fpsDirty = false;
				if (fpsSendTimer) { clearTimeout(fpsSendTimer); fpsSendTimer = null; }
				fpsValue = null;
				if (fpsValEl) fpsValEl.textContent = (c.fpsText === undefined || c.fpsText === null || c.fpsText === "") ? "-" : String(c.fpsText);
			} else if (holdDir === 0 && (fpsValue === null || Date.now() >= suppressFpsSyncUntil) && c.fps !== undefined && c.fps !== null && c.fps !== "") {
				var f = parseFloat(String(c.fps).replace(",", "."));
				if (isFinite(f)) {
					fpsValue = f;
					showFps();
				}
			}

			if (c.fpsOrange && fpsValEl) fpsValEl.style.color = "#FFA500";

			// Expert and Log+
			if (expertChk && typeof c.expert === "boolean") {
				expertChk.checked = c.expert;
			}
			if (logPlusChk && typeof c.logPlus === "boolean") {
				logPlusChk.checked = c.logPlus;
			}
			if (logPlusLabel && typeof c.logPlusVisible === "boolean") {
				logPlusLabel.style.display = c.logPlusVisible ? "inline-flex" : "none";
			}

			// Auto-Hide setting sync
			if (Date.now() >= suppressAutoHideSyncUntil && typeof c.autoHide === "number") {
				currentAutoHideIndex = c.autoHide;
				if (autoHideBtn && autoHideItems[currentAutoHideIndex] !== undefined) {
					autoHideBtn.textContent = autoHideItems[currentAutoHideIndex];
				}
				markActive(autoHideList, currentAutoHideIndex);
			}
		}

		// Vertical-only auto height
		var AUTO_HEIGHT_ENABLED = true;
		var BOTTOM_MARGIN_ADJUST_PX = 0;
		var MIN_PANEL_HEIGHT_PX = 48;
		var SNAP_DEBOUNCE_MS = 200;
		var SNAP_TOLERANCE_PX = 3;
		var ingameUiEl = document.getElementById("HtmlWidgetPanel");
		var lastSnapWidth = -1;
		var lastSnapHeight = -1;
		var snapTimer = null;

		function snapHeight() {
			snapTimer = null;
			if (!AUTO_HEIGHT_ENABLED || !ingameUiEl || typeof ingameUiEl.sendRect !== "function") return;
			if (contentEl.classList.contains("hidden")) return;

			var w = document.documentElement.clientWidth;
			var lastEl = groupBoxes[groupBoxes.length - 1];
			if (!lastEl) return;

			var frameRect = ingameUiEl.getBoundingClientRect();
			var lastRect = lastEl.getBoundingClientRect();
			if (!frameRect.height || !lastRect.height) return;

			var sideMargin = Math.max(4, Math.round(lastRect.left - frameRect.left));
			var needed = Math.round(lastRect.bottom - frameRect.top + sideMargin + BOTTOM_MARGIN_ADJUST_PX);
			if (needed < MIN_PANEL_HEIGHT_PX) needed = MIN_PANEL_HEIGHT_PX;
			if (!isFinite(needed) || needed > 4000) return;

			if (lastSnapHeight >= 0 && Math.abs(needed - lastSnapHeight) <= SNAP_TOLERANCE_PX) return;

			lastSnapWidth = w;
			lastSnapHeight = needed;
			ingameUiEl.style.height = needed + "px";
			ingameUiEl.sendRect();
		}

		function scheduleSnap() {
			if (!AUTO_HEIGHT_ENABLED) return;
			if (snapTimer) clearTimeout(snapTimer);
			snapTimer = setTimeout(snapHeight, SNAP_DEBOUNCE_MS);
		}

		// Scale
		function applyScale() {
			const w = document.documentElement.clientWidth || mainSection.clientWidth;
			if (!w) return;
			var scale = w / BASE_WIDTH;
			if (scale < MIN_SCALE) scale = MIN_SCALE;
			if (scale > MAX_SCALE) scale = MAX_SCALE;
			document.documentElement.style.fontSize = (14 * scale) + "px";
			scheduleSnap();
		}
		applyScale();
		window.addEventListener("resize", applyScale);
		if (ingameUiEl) ingameUiEl.addEventListener("resize", applyScale);
		setInterval(applyScale, 250);
		if (typeof requestAnimationFrame === "function") {
			var watchedWidth = -1;
			var watchWidth = function () {
				var cw = document.documentElement.clientWidth;
				if (cw !== watchedWidth) {
					watchedWidth = cw;
					applyScale();
				}
				requestAnimationFrame(watchWidth);
			};
			requestAnimationFrame(watchWidth);
		}

		// SVG icon glyph substitutions
		var ICONS = {
			"\u25B2": '<svg viewBox="0 0 10 10"><polygon points="5,1 9,9 1,9" fill="currentColor"/></svg>',
			"\u25BC": '<svg viewBox="0 0 10 10"><polygon points="1,1 9,1 5,9" fill="currentColor"/></svg>',
			"\u22A5": '<svg viewBox="0 0 10 10"><line x1="5" y1="1" x2="5" y2="7" stroke="currentColor" stroke-width="1.4"/><line x1="1" y1="9" x2="9" y2="9" stroke="currentColor" stroke-width="1.4"/></svg>',
			"\u22A4": '<svg viewBox="0 0 10 10"><line x1="5" y1="3" x2="5" y2="9" stroke="currentColor" stroke-width="1.4"/><line x1="1" y1="1" x2="9" y2="1" stroke="currentColor" stroke-width="1.4"/></svg>',
			"\u0394": '<svg viewBox="0 0 10 10"><polygon points="5,1 9,9 1,9" fill="none" stroke="currentColor" stroke-width="1"/></svg>',
			"\u26A1": '<svg viewBox="0 0 10 10"><polygon points="6,0 1,6 4.5,6 3.5,10 9,4 5.5,4" fill="currentColor"/></svg>',
			"\u23F8": '<svg viewBox="0 0 10 10"><rect x="1" y="1" width="2.5" height="8" fill="currentColor"/><rect x="6.5" y="1" width="2.5" height="8" fill="currentColor"/></svg>',
			"\u2744\uFE0F": '<svg viewBox="0 0 10 10"><g stroke="currentColor" stroke-width="1" stroke-linecap="round"><line x1="5" y1="0.5" x2="5" y2="9.5"/><line x1="0.5" y1="5" x2="9.5" y2="5"/><line x1="1.9" y1="1.9" x2="8.1" y2="8.1"/><line x1="8.1" y1="1.9" x2="1.9" y2="8.1"/></g></svg>',
			"\uD83D\uDD12": '<svg viewBox="0 0 10 10"><rect x="2" y="4.5" width="6" height="5" rx="0.6" fill="currentColor"/><path d="M3.3 4.5V3.2a1.7 1.7 0 0 1 3.4 0V4.5" fill="none" stroke="currentColor" stroke-width="1"/></svg>'
		};
		var ICON_KEYS = Object.keys(ICONS).sort(function (a, b) { return b.length - a.length; });

		function setTextWithIcons(el, text) {
			el.textContent = "";
			if (!text) return;
			var i = 0;
			while (i < text.length) {
				var matched = null;
				for (var k = 0; k < ICON_KEYS.length; k++) {
					if (text.indexOf(ICON_KEYS[k], i) === i) { matched = ICON_KEYS[k]; break; }
				}
				if (matched) {
					var span = document.createElement("span");
					span.className = "icon-glyph";
					span.innerHTML = ICONS[matched];
					el.appendChild(span);
					i += matched.length;
				} else {
					var start = i;
					i++;
					while (i < text.length) {
						var isIconStart = false;
						for (var k2 = 0; k2 < ICON_KEYS.length; k2++) {
							if (text.indexOf(ICON_KEYS[k2], i) === i) { isIconStart = true; break; }
						}
						if (isIconStart) break;
						i++;
					}
					el.appendChild(document.createTextNode(text.slice(start, i)));
				}
			}
		}

		function setDisconnected() {
			contentEl.classList.add("hidden");
			statusEl.textContent = "Not connected. Please start AutoFPS.";
			if (titleEl) titleEl.classList.add("hidden");
			if (hotkeyBtn) hotkeyBtn.parentElement.classList.add("hidden");
			if (autoHideGroup) autoHideGroup.classList.add("hidden");
			var fpsIconElem = document.getElementById("imgFpsSource");
			if (fpsIconElem) fpsIconElem.style.display = "none";
			var imgSimHeaderFS = document.getElementById("imgSimHeaderFS");
			if (imgSimHeaderFS) imgSimHeaderFS.style.display = "none";
			var imgSimHeaderDog = document.getElementById("imgSimHeaderDog");
			if (imgSimHeaderDog) imgSimHeaderDog.style.display = "none";
			mainSection.style.backgroundColor = DISCONNECTED_BG;
			mainSection.style.color = DISCONNECTED_TEXT;
		}

		function applyData(data) {
			var justConnected = contentEl.classList.contains("hidden");
			contentEl.classList.remove("hidden");
			if (justConnected) {
				lastSnapWidth = -1;
				lastSnapHeight = -1;
				scheduleSnap();
			}
			statusEl.textContent = "";
			if (hotkeyBtn) hotkeyBtn.parentElement.classList.remove("hidden");
			if (autoHideGroup) autoHideGroup.classList.remove("hidden");

			if (data.theme) {
				var theme = data.theme;
				var inputBg = theme.inputBg || theme.bg;
				var btnBg = theme.btnBg || inputBg;

				mainSection.style.backgroundColor = theme.bg || mainSection.style.backgroundColor;
				mainSection.style.color = theme.text || mainSection.style.color;
				groupBoxes.forEach(function (el) {
					if (theme.border) el.style.borderColor = theme.border;
				});
				groupHeaders.forEach(function (el) {
					if (theme.text) el.style.color = theme.text;
				});
				document.querySelectorAll(".controls-row .label, .chk-label").forEach(function (el) {
					if (theme.text) el.style.color = theme.text;
				});
				[modeBtn, targetBtn, autoHideBtn].forEach(function (b) {
					if (!b) return;
					if (inputBg) b.style.backgroundColor = inputBg;
					if (theme.text) b.style.color = theme.text;
					if (theme.border) b.style.borderColor = theme.border;
				});
				[modeList, targetList, autoHideList].forEach(function (l) {
					if (!l) return;
					if (inputBg) l.style.backgroundColor = inputBg;
					if (theme.text) l.style.color = theme.text;
					if (theme.border) l.style.borderColor = theme.border;
				});
				[fpsDecBtn, fpsIncBtn].forEach(function (b) {
					if (!b) return;
					if (btnBg) b.style.backgroundColor = btnBg;
					if (theme.text) b.style.color = theme.text;
					if (theme.border) b.style.borderColor = theme.border;
				});
				if (fpsValEl && theme.text) fpsValEl.style.color = theme.text;

				if (flightTypeBtn) {
					if (inputBg) flightTypeBtn.style.backgroundColor = inputBg;
					if (theme.text) flightTypeBtn.style.color = theme.text;
					if (theme.border) flightTypeBtn.style.borderColor = theme.border;
				}
				if (flightTypeList) {
					if (inputBg) flightTypeList.style.backgroundColor = inputBg;
					if (theme.text) flightTypeList.style.color = theme.text;
					if (theme.border) flightTypeList.style.borderColor = theme.border;
				}
				if (resetBtn) {
					if (btnBg) resetBtn.style.backgroundColor = btnBg;
					if (theme.text) resetBtn.style.color = theme.text;
					if (theme.border) resetBtn.style.borderColor = theme.border;
				}
				if (hotkeyBtn && !hotkeyBtn.classList.contains("listening")) {
					if (btnBg) hotkeyBtn.style.backgroundColor = btnBg;
					if (theme.text) hotkeyBtn.style.color = theme.text;
					if (theme.border) hotkeyBtn.style.borderColor = theme.border;
				}

				customCheckboxes.forEach(function (cb) {
					if (inputBg) cb.style.backgroundColor = inputBg;
					if (theme.border) cb.style.borderColor = theme.border;
					if (theme.text) cb.style.color = theme.text;
				});

				if (titleEl && theme.text) titleEl.style.color = theme.text;
			}

			for (var id in data) {
				if (id === "theme" || id === "widgetTitle" || id === "widgetHotkey" || id === "autoFpsControls" || id === "fpsIcon" || id === "simHeaderFS" || id === "simHeaderDog") continue;
				if (!Object.prototype.hasOwnProperty.call(data, id)) continue;
				var item = data[id];
				var el = document.getElementById(id);
				if (el && item) {
					setTextWithIcons(el, (item.text === undefined || item.text === null) ? "" : item.text);
					var fallbackColor = (data.theme && data.theme.text) ? data.theme.text : "#FFFFFF";
					el.style.color = item.color || fallbackColor;

					if (id === "statusMessage") {
						var txt = (item.text === undefined || item.text === null) ? "" : String(item.text);
						var firstLine = txt.split("\n")[0] || "";
						if (firstLine.length > 60) {
							el.classList.add("status-compact");
						} else {
							el.classList.remove("status-compact");
						}
					}
				}
			}

			var fpsIconElem = document.getElementById("imgFpsSource");
			if (fpsIconElem) {
				var fpsIconMap = {
					1: "img/fps_msfs.png",
					2: "img/fps_rtss.png",
					3: "img/fps_ofxr.png",
					4: "img/fps_vxr.png"
				};
				var iconSrc = fpsIconMap[data.fpsIcon];
				var isClickable = (data.fpsIcon === 1 || data.fpsIcon === 2);

				if (iconSrc) {
					fpsIconElem.src = iconSrc;
					fpsIconElem.style.display = "inline-block";
					fpsIconElem.style.cursor = isClickable ? "pointer" : "default";
					fpsIconElem.title = isClickable ? "Click to toggle FPS source" : "";
					fpsIconElem.dataset.clickable = isClickable ? "true" : "false";
				} else {
					fpsIconElem.style.display = "none";
				}

				if (!fpsIconElem.hasClickWire) {
					fpsIconElem.hasClickWire = true;
					fpsIconElem.addEventListener("click", function (e) {
						if (fpsIconElem.dataset.clickable !== "true") return;
						e.stopPropagation();
						fetch("http://localhost:54321/api/togglefpssource", { method: "POST", cache: "no-store" }).catch(function () { });
					});
				}
			}

			var imgSimHeaderFS = document.getElementById("imgSimHeaderFS");
			if (imgSimHeaderFS) {
				imgSimHeaderFS.style.display = data.simHeaderFS ? "inline-block" : "none";
			}

			var imgSimHeaderDog = document.getElementById("imgSimHeaderDog");
			if (imgSimHeaderDog) {
				imgSimHeaderDog.style.display = data.simHeaderDog ? "inline-block" : "none";
			}

			if (titleEl) {
				titleEl.classList.remove("hidden");
				if (data.widgetTitle) titleEl.textContent = data.widgetTitle;
			}

			if (darkModeChk && data.theme && typeof data.theme.isDark === "boolean") {
				darkModeChk.checked = data.theme.isDark;
			}

			if (data.widgetHotkey !== undefined) {
				syncHotkeyFromServer(data.widgetHotkey);
			}

			if (typeof data.hideTitleBar === "boolean") {
				document.body.classList.toggle("hide-titlebar", data.hideTitleBar);
				var headerEl = document.querySelector("header, .ingameUiHeader, ingame-ui-header");
				if (headerEl) {
					headerEl.style.display = data.hideTitleBar ? "none" : "";
				}
			}

			if (flightTypeBtn && flightTypeList && Array.isArray(data.profiles)) {
				var profilesKey = JSON.stringify(data.profiles);
				if (profilesKey !== lastProfilesKey) {
					lastProfilesKey = profilesKey;
					renderFlightTypeList(data.profiles);
				}

				if (Date.now() >= suppressSyncUntil && typeof data.activeProfileIndex === "number") {
					if (currentActiveIndex !== data.activeProfileIndex) {
						currentActiveIndex = data.activeProfileIndex;
						if (currentProfiles[currentActiveIndex] !== undefined) {
							flightTypeBtn.textContent = displayProfileName(currentProfiles[currentActiveIndex]);
						}
					}
				}
				markActiveItem(currentActiveIndex);
			}

			try {
				applyControls(data.autoFpsControls);
			} catch (err) { }
		}

		// Internal fast camera check (150ms)
		function checkCameraAutoHide() {
			if (!currentAutoHideIndex) {
				if (isCameraHidden) {
					isCameraHidden = false;
					updateWidgetVisibility();
				}
				return;
			}

			var camera = null;
			try {
				if (window.SimVar && typeof SimVar.GetSimVarValue === "function" &&
					(typeof SimVar.IsReady !== "function" || SimVar.IsReady())) {
					camera = SimVar.GetSimVarValue("CAMERA STATE", "Enum");
				}
			} catch (e) { }

			var hide = false;
			if (typeof camera === "number") {
				if (currentAutoHideIndex === 1) {
					hide = (camera === 3);
				} else if (currentAutoHideIndex === 2) {
					hide = (camera >= 3 && camera <= 9);
				}
			}

			if (hide !== isCameraHidden) {
				isCameraHidden = hide;
				updateWidgetVisibility();
			}
		}

		setInterval(checkCameraAutoHide, 150);
		checkCameraAutoHide();

		function poll() {
			fetch(STATUS_URL, { cache: "no-store" })
				.then(function (res) {
					if (!res.ok) throw new Error("HTTP " + res.status);
					return res.json();
				})
				.then(applyData)
				.catch(function () {
					setDisconnected();
				});
		}

		poll();
		setInterval(poll, POLL_INTERVAL_MS);
	}

	init();
})();
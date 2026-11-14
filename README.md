# MSFS_AutoFPS v0.5.5.1

## Notice
My future development efforts on this app are mainly limited to maintenance, resilience improvements and streamlining of existing functionality only. I do add new functionality at times, mainly from my existing wishlist. I occasionally accept user requests for new functionality, however these will only be accepted if I judge it to be a great idea and it is technically achievable, useful to the majority of users, consistent with AutoFPS's existing design philosophy, with negligible, or preferably no, UI impact, and if I have the available time to do it.

## Licence
- Versions released before 8 November 2026, up to and including v0.5.5.0, were released under the MIT Licence (with an exception for the app icon image and associated files, and a requirement to attribute previous authors) and remain under it.
- Versions released on or after 8 November 2026 are released under the MSFS_AutoFPS Licence in [LICENSE.txt](LICENSE.txt). In short: free personal use, no redistribution of the app or derivative works without my written permission, no decompiling or reverse engineering, credit to the original author where permission is given, and the app name and icon are not licensed. This is source-available, not open source. Please read the full terms.
- This app builds on earlier work by other authors, credited in the Summary below. Their original licences continue to apply to their parts.
- The only official source of this app is [github.com/ResetXPDR/MSFS_AutoFPS](https://github.com/ResetXPDR/MSFS_AutoFPS). Copies, forks or rebuilds published elsewhere, including any rebuilt from decompiled binaries, are not made, endorsed or supported by me. Please download only from the official [Releases](https://github.com/ResetXPDR/MSFS_AutoFPS/releases/latest) page.

## Important Simulator Notes
- **Official Forums Warning:** Do not mention or discuss this app on the official MSFS forums, even in private messages. Forum moderation treats active memory access as a Terms of Service violation regardless of its harmless operation. If you post about it there, your account may be flagged or banned.
- **SimConnect Wishlist:** There is an official MSFS wishlist item requesting SimConnect variable access to graphics settings, which would allow legitimate settings management. Please vote for it [here](https://forums.flightsimulator.com/t/expose-tlod-olod-clouds-etc-via-simconnect-l-vars/634075).
- **Memory Access & Safety:** AutoFPS reads and adjusts active simulator graphics settings in memory at a maximum rate of once per second per setting. On startup, memory offsets are validated; if an unsupported simulator build is detected and updated offsets cannot be validated, the app restricts itself to read-only mode to prevent unintended changes. AutoFPS is provided as-is and used at your own risk.

---

## Table of Contents
This readme goes from the basics to the full detail. Sections 1 to 3 get you installed and running, sections 4 to 7 explain how to use and read the app, sections 8 and 9 cover Expert mode and the companion apps, section 10 helps when something goes wrong, and section 11 holds the complete technical detail.

1. [Summary](#summary)
2. [Installation & Updates](#installation--updates)
3. [Quick Start & Non-Expert Guide](#quick-start--non-expert-guide)
4. [Desktop App Window & Controls](#desktop-app-window--controls)
5. [Reading the Display: Colours and Symbols](#reading-the-display-colours-and-symbols)
6. [In-Game Toolbar Widget & Web Interface](#in-game-toolbar-widget--web-interface)
7. [Graphics Modes & Frame Generation](#graphics-modes--frame-generation)
8. [Expert Mode Reference Guide](#expert-mode-reference-guide)
9. [Companion Apps & Advanced Features](#companion-apps--advanced-features)
10. [Troubleshooting & FAQ](#troubleshooting--faq)
11. [Detailed Reference](#detailed-reference)

---

## Summary
Based on muumimorko's idea and code in MSFS_AdaptiveLOD, as further developed by Fragtality in DynamicLOD, myself in DynamicLOD_ResetEdition and MSFS2020_AutoFPS and with inspiration from changes suggested by kayjay1c6b from his MSFS2024_AutoFPS.

Now fully compatible with MSFS 2020 and 2024 in a single application, AutoFPS improves simulator smoothness and framerate consistency by automatically changing key graphics settings on the fly. Key features include:

- Automatic TLOD adjustment to achieve and maintain a target FPS or follow an altitude schedule.
- Advanced FPS smoothing (FPS+) to filter out brief rendering spikes and dips.
- In-Game Toolbar Widget and local web interface (`http://localhost:54321`) with real-time telemetry, camera-based auto-hiding, and cockpit controls.
- Dynamic graphics mode architecture supporting native rendering, VR, native Frame Generation, and external mods (MSM, VectorXR, OFXR, Lossless Scaling) with dual-layer VR conflict protection.
- Simplified Non-Expert mode with automated guardrails suited to most user setups.
- Deep Expert Mode customisation covering sensitivity models, altitude bands, night optimisations with configurable day/night switching, multi-profile pairing (VR, sim rate and Day/Night), and CPU thread optimisation.
- A **Monitor only** flight type that stops AutoFPS changing any simulator setting and simply monitors performance.
- Native Dark Mode theme, resizable fluid layout, and automatic restoration of simulator settings upon exit, enhanced to withstand MSFS CTDs.
- Auto future MSFS version compatibility, provided MSFS memory changes are minor, plus detection of and protection from known similar apps already running or incompatibilities with newer MSFS versions.
- Intelligent auto-update system that verifies each downloaded installer (SHA-256), defers widget updates until MSFS exits, and supports custom Community folder and Addons Linker management that persists across updates.
- Optional MSFS Performance Optimiser, which selects the best CPU core affinity, process priority and power plan for MSFS.

---

## Installation & Updates

Basically: run the installer to install, update or uninstall the app. If anything goes wrong, see [Troubleshooting & FAQ](#troubleshooting--faq).

### Requirements
- Microsoft Flight Simulator 2020 or 2024
- Windows 10 / 11 (64-bit)
- .NET 8 Desktop Runtime (x64)
- Visual C++ 2015–2022 Redistributable (x64)
- MobiFlight WASM Module (managed by the installer)

### Installing
1. Download the latest installer (`MSFS_AutoFPS-installer-vX.X.X.X.exe`, about 3 MB) from [GitHub Releases](https://github.com/ResetXPDR/MSFS_AutoFPS/releases/latest).
2. Make sure MSFS_AutoFPS and/or MSFS2020_AutoFPS are not running. If the MobiFlight module is missing or outdated, MSFS must be closed too.
3. Run the installer, choose your options and install. Do not run it as Admin unless it will not install because of a permissions issue.
4. Start AutoFPS, ideally just after MSFS has loaded to the main menu, then follow the [Quick Start](#quick-start--non-expert-guide).

### Installer Options
- **Desktop Icon:** Creates a desktop icon for the app.
- **MSFS Widget:** Deploys the AutoFPS In-Game Toolbar Widget to the Community folder of any installed MSFS versions.
- **Close to Tray:** The app closes to the system tray.
- **Reset Configs:** Resets the app Common, MSFS 2020 and MSFS 2024 config files. When upgrading from 0.5.5.0 or earlier, the config files are first backed up to a `ConfigBackup_<version>` folder.
- **Repair:** Reinstalls core files, including the latest Visual C++ Redistributable and .NET 8 runtime, MobiFlight and the In-Game Widget (if enabled), keeping your app config intact.
- **Auto Start Options:** Remove, Retain, FSUIPC (via `FSUIPC.ini`) or MSFS (via `EXE.xml`).
- **Custom Community Folders:** Use `2020 Folder...` or `2024 Folder...` to point to custom Community paths (such as Addons Linker locations). Custom paths persist across updates in `Install.log`. Right-click either button to reset to the auto-detected simulator defaults.

### Updating, Upgrading and Uninstalling
- **Updating:** Just run the installer and select update. Do **not** uninstall first, because that deletes all app files including your config.
- **Upgrading from MSFS2020_AutoFPS:** No need to uninstall it first. The installer removes it and preserves its MSFS 2020 config for use in this app.
- **Uninstalling:** Exit the app completely (not hiding in the system tray), run the installer and select **Remove** on the first window. This removes the desktop icon, any MSFS or FSUIPC autostart entries, and the entire app folder including your config file and the in-game widget.

### Update Modes & Channels
- **Silent Updates:** Installs updates automatically without prompts, displaying release notes in Notepad afterwards.
- **Prompted Updates (Default):** Prompts for confirmation before installing optional updates.
- **Show Updates:** Displays notifications when updates are available without downloading.
- **Mandatory Updates Only:** Notifies and installs only mandatory builds.
- **Update Channels (Expert mode):**
  - **Release:** Standard stable release baseline.
  - **RC+ (Release Candidate), Test+, Exp+ (Experimental):** Accesses pre-release builds. Returning from a test channel to Release offers a one-time automatic rollback to the latest formal release. Test versions require all app update notifications to remain active to maintain a consistent test baseline.

### Safe Updates
- **Verified:** Before launching a downloaded installer, auto-update checks its SHA-256 against the digest GitHub publishes for the release. On a mismatch it downloads the installer again, and if no digest is available the update is refused.
- **Deferred:** When an auto-update contains an in-game widget update while MSFS is running, the installer defers installation until MSFS closes, preventing flight interruptions and locked-file errors.

### File Locations
The installation location is fixed to `%appdata%\MSFS_AutoFPS`.
- Binaries: `%appdata%\MSFS_AutoFPS\bin`
- Logs: `%appdata%\MSFS_AutoFPS\log`
- Installation Log: `%appdata%\MSFS_AutoFPS\Install.log`
- Configs: `%appdata%\MSFS_AutoFPS\MSFS_AutoFPS.config` (common), `MSFS2020_AutoFPS.config` and `MSFS2024_AutoFPS.config`

---

## Quick Start & Non-Expert Guide

If you are new to the application or prefer an automated set-and-forget experience, Non-Expert mode provides reliable performance management without complex tuning.

### Quick Start Steps
1. Configure your general graphics settings inside MSFS as you normally would.
2. Launch AutoFPS before loading into a flight.
3. Ensure **Expert** (Use Expert Options) is left unchecked.
4. Select your **Flight Type**:
   - **VFR:** Tuned for lower altitudes, GA aircraft, and higher visual expectations.
   - **IFR:** Tuned for airliners, higher climb rates, and complex airport performance.
   - **Monitor only:** Choose this to watch performance without AutoFPS changing any settings (see [Monitor Only](#monitor-only)).
5. Set your **Target FPS**:
   - **Auto:** Recommended if you want the app to establish a realistic target based on your system headroom.
   - **Manual:** Set to a framerate your hardware can comfortably sustain in moderate conditions.
   - **Fixed:** Set to match your lock if using a driver or monitor refresh lock.
6. Click back into MSFS, allow any initial FPS settling or seeking events to complete (up to 60 seconds), and fly.

### Non-Expert Automated Guardrails
In Non-Expert mode, AutoFPS manages LOD and visual settings using conservative, pre-tuned rules to prevent system overloads:
- **Automation Method:** Uses FPS Sensitivity at 5% for smooth, progressive adjustments.
- **Base Min TLOD:** 50% of your simulator setting for VFR, 25% for IFR (doubled if Auto Target FPS is selected), clamped to a maximum ceiling of 150.
- **Base TLOD Altitude:** 100 ft AGL for VFR, 1,000 ft AGL for IFR.
- **Top Max TLOD:** 250% of baseline for VFR (capped at 400), 200% for IFR (capped at 400).
- **Ground OLOD:** Capped at 200 (VFR runs at 150% of baseline up to 200; IFR runs at 100%).
- **Night Optimisation:** Automatically uses `TLOD÷2` at night (capped at 200) to shed distant scenery workload in the dark. By default, night starts at about the end of civil twilight.
- **MSFS 2020 Specifics:** Decreases cloud quality if performance cannot be recovered at Base Min TLOD or if GPU load exceeds 98% (when GPU-Z is running).
- **MSFS 2024 Specifics:** Engages automated settings reduction under marginal conditions and supports VRAM+ overflow mitigation when GPU-Z is active.

*Note: Do not manually change graphics settings in the MSFS menu while in an active flight with AutoFPS running, as the app will overwrite them or restore original values when the session ends.*

---

## Desktop App Window & Controls

- **General Operation:**
  - Can be started anytime, but preferably just after MSFS loads to the main menu. The app exits automatically when MSFS closes.
  - Set `ExitAppAfterFlightSession = true` in `MSFS_AutoFPS.config` if you want the app to exit automatically at the end of each flight.
  - Running as Administrator is not typically required, but AutoFPS must run at the same elevation level as MSFS. When running elevated, `Admin` appears in the title bar.
- **Window Positioning & Memory:**
  - Window position and minimised/maximised state are remembered across sessions (except movements made in VR).
  - Disable position saving by setting `RememberWindowPos = false` in the configuration file.
  - The window automatically resets to its default position `(50, 50)` if restarted within 15 seconds of closing. Disable this by setting `AllowWindowPosReset = false`.
- **Title Bar Hiding & Whole-Window Dragging:**
  - The title bar can be hidden or restored by double-clicking the title bar, or via the right-click context menu (**Hide Title Bar**).
  - Left-click and drag anywhere on empty body or panel space to move the window.
- **Progressive UI Collapsing:**
  - Double-clicking the title bar area progressively collapses the desktop interface:
    - First double-click: Hides the Expert settings section (if active).
    - Second double-click: Hides the General settings section.
    - Third double-click: Restores all hidden sections.
- **Right-Click Context Menu:**
  - Right-clicking anywhere on the window provides instant access to **Hide/Show Title Bar**, **Minimise**, **Close to Tray** (if selected during installation), and **Exit** (exits immediately without prompts).
- **Streamlined Close Confirmation:**
  - The confirmation prompt only triggers when clicking the window close `[X]` button. Deliberate multi-step exits (`Alt + F4`, right-clicking the Taskbar icon and selecting *Close window*, or using the context menu) exit immediately.
- **Main Controls:**
  - **Use Expert Options (Expert):** Unchecked (default) uses the built-in guardrails. Checked reveals the additional settings. If you do not understand these settings and their impact on MSFS performance and graphics quality, it is strongly recommended that you leave this unchecked.
  - **Flight Type:** VFR or IFR in any mode, plus six additional user profiles in Expert mode, and **Monitor only** in any mode, which stops AutoFPS changing any sim settings (see [Monitor Only](#monitor-only)).
  - **Target FPS:** The most important setting in the app (range 10–500, fractional values of 0.5 supported). Set it to the FPS you want after any frame generation has been applied.
  - **Target FPS option (Manual, Auto, Fixed):** How the Target FPS is applied (see [Graphics Modes & Frame Generation](#graphics-modes--frame-generation)). The choice is saved separately for each graphics mode in each profile.
  - **On Top:** Lets the app overlay your MSFS session, with MSFS keeping the focus. Useful for adjusting settings and watching the result as the flight progresses, and for single monitor users with frame generation, who then see the true FG FPS the app is reading.
  - **Dark Mode:** Toggles between the default Light theme and a native Dark theme.
  - **Reset button:** Resets TLOD, Clouds, Auto Target FPS and graphics mode detection to their initial state, which is useful to restart the TLOD Extra seek if conditions change a lot. In Expert mode, outside a flight session, it restores the active flight profile to defaults after confirmation. Press **ALT-R** while the app has the focus, so it can be assigned as a VR-friendly voice command with an app like VoiceAttack.
  - **20>24 / 24>20 button:** If the Sim Version shows in red and is not the MSFS version you want to configure before starting it, click this button to switch to that version.

---

## Reading the Display: Colours and Symbols

**Connection Status**
- **Red** means not connected and **green** means connected. The Sim Version shows **royal blue** when the MSFS Performance Optimiser is enabled and controlling MSFS.
- The **Widget** label (shown if the In-Game Widget is installed) is green when the toolbar widget or the browser page (`http://localhost:54321/`) is open and receiving telemetry, and red when closed or inactive.
- Sim Values only show valid values when all three connections are green (or royal blue for the Sim Version with the Optimiser). `n/a` means not available right now.

**Sim Values colours**
- **Green:** the value meets or exceeds the target.
- **Red:** the value is at its minimum or below target.
- **Orange with arrows (TLOD or OLOD):** the value is auto-adjusting.
- **Orange (FPS and TLOD):** adjustment is limited because FG is inactive and TLOD increases are disabled.
- **Black:** none of the above.
- Other symbols: 🔒 value locked, ▲ increasing, ▼ decreasing, ⊤ upper limit reached (also shown beside TLOD while VRAM+ is limiting it, with an explanatory tooltip), ⊥ lower limit reached.
- The Sim Values header shows an **MSFS icon** when default MSFS values are being displayed (not in a flight session), and an **AutoFPS icon** when the values are being actively controlled by AutoFPS in a flight. Both icons show when hybrid dynamic settings is active.
- Mouse over the **Reduce** value to see the additional reduction settings when auto reduction or VRAM+ is at Level 1 or greater.
- The **Reduce** label shows `DynSet XX` or `Inactive` for only 15 seconds after a flight starts, a full reset, or MSFS dynamic settings being switched on or off (Expert), then returns to the normal cloud state unless settings reduction is active.

**FPS display**
- **FPS** shows the unfiltered raw FPS when a Fixed FPS is set or detected. **FPS+** shows the average FPS, filtered for spikes and dips, so brief changes (view changes, panning, scenery loading) do not cause unwanted setting changes.
- The FPS value shows in **two alternating purple shades** while detailed FPS logging is running (Log+ mode). Click it again to cancel.
- The FPS source icon shows RTSS, MSFS, OFXR or VXR. Click it to switch between MSFS and RTSS when RTSS is running.

**Target FPS dropdown**
- The dropdown background turns **orange** when the Target FPS type selected is not the one for the current graphics mode, and also when Target FPS is higher than a detected FPS cap, with a tooltip explaining how to fix it.

**Status line**
- The sim rate shows at the start of the line for any value except 1X.
- Shows the detected graphics mode, app pause, FPS settle, Mtns+, app priority and the TLOD range as applicable. A **+** next to TLOD means TLOD Extra is active, so expect a higher TLOD Base Min.
- **Day**, **Night** or **Δ** shows the day/night state, with Δ while transitioning between day and night. The same state selects the Night profile in a Day/Night profile pair.
- A 🔒 next to the TLOD range means the delayed base TLOD reduction (`DelayLG`) is active and waiting for the landing gear.
- A **lightning symbol** next to the FPS reading means periodic frame time spikes have been detected, and next to the TLOD range means spike protection is currently reducing TLOD.
- When VRAM+ is reducing settings, the **Reduce** label changes to **VRAM+** in red, and the status line appends **HLD** (hold) or **RED** (reduction).
- A **D** prefix on the TLOD and OLOD labels means MSFS dynamic settings is active and controls the LOD reductions.

---

## In-Game Toolbar Widget & Web Interface

The companion In-Game Toolbar Widget and local web server allow in-cockpit monitoring and adjustments without switching out of the simulator.

### Installation & Access
- Select the **MSFS Widget** option during the installer setup to deploy the widget to your Community folder.
- In-cockpit: Open the MSFS top toolbar and click the **AutoFPS** icon.
- Web browser: Navigate to `http://localhost:54321/` in a browser on the PC running AutoFPS while it is running. The web server only accepts requests addressed to localhost / 127.0.0.1.

### Key Widget Features
- **Live Telemetry & Balanced Layout:** Mirrors the desktop application across three clean columns displaying real-time FPS, TLOD, OLOD, AGL, vertical speed, reduction states, and connection status with matching glyphs.
- **Interactive Target FPS Controls:** Adjust Target FPS directly in the cockpit using responsive `–` and `+` stepper buttons (with accelerating hold), toggle modes (Manual, Auto, Fixed), and switch target graphics profiles.
- **Camera-Based Auto-Hide:** Automatically conceals the in-game toolbar panel when leaving cockpit view to keep external views and screenshots uncluttered:
  - **Off:** Widget remains visible in all camera views.
  - **Chase:** Automatically hides only in standard chase exterior view.
  - **All Ext:** Automatically hides across all external views (chase, drone, showcase, and exterior fixed cameras) with sub-150 ms SimVar response.
- **Click-to-Toggle FPS Source:** Click the FPS source badge on the panel to toggle capture between MSFS and RTSS (read-only during active OFXR or VectorXR sessions).
- **Show/Hide Hotkey:** The previous on-screen hotkey button has been replaced by the Auto-Hide dropdown. Any previously configured hotkeys continue to work in the background. To set, change, or clear a manual toggle hotkey, edit the `WidgetHotkey` key in `MSFS2020_AutoFPS.config` or `MSFS2024_AutoFPS.config` located in `%appdata%\MSFS_AutoFPS` (set to `"None"` to disable).
- **Collapsible General Controls:** Click the General section header to collapse controls during flight, synchronised two-way with the desktop application.
- **Title Bar Hiding & Dragging:** Double-click empty space on the widget header to toggle the title bar. Move the widget by dragging anywhere on its background. Text and elements dynamically scale when resized horizontally to prevent clipping in VR.
- **Monitor Only Layout:** When the Flight Type is **Monitor only**, the widget (and web interface) hides Target FPS, Expert and Reset, moves Dark to the Flight Type line, and shows Log+ locked on and dimmed. Target FPS, mode, Target, Expert and Log+ changes made from the widget are ignored while in Monitor only.
- **Flight Type List:** The list is rebuilt each time it is opened with the selected item scrolled into view, scrolls with the mouse wheel and has a wide scrollbar that can also be clicked or dragged.

---

## Graphics Modes & Frame Generation

AutoFPS incorporates a dynamic graphics mode architecture. Standard native modes are permanently available, while external frame generation mods populate automatically only when their background processes or API layers are detected.

### Target FPS Guidelines (Fixed vs NTE vs Manual)
- **Fixed FPS (True Lock):**
  - Use when your framerate is rigidly locked to an exact number (e.g. monitor refresh divisor, driver limiter, or Reflex lock).
  - Select **Fixed Target FPS** and match your lock.
  - Target FPS can be set between 0% and 5% below a detected cap without warning to accommodate standard G-Sync, FreeSync, and Reflex frame-rate limits. If set more than 5% below the cap, the app advises switching to Manual mode or raising the target to match.
- **NTE FPS (Not-To-Exceed Cap):**
  - Use when a ceiling cap exists but framerate still fluctuates below it.
  - Select **Manual Target FPS**.
  - Target FPS must be set at least 5% below the cap, ensuring settings reduction has sufficient headroom to recover.
- **Auto Target FPS:**
  - Use when you don't know what target to choose, or your flights are so varied that one target is not always right.
  - It calculates a target after the initial FPS settling, when stationary on the ground or any time you are in the air, and recalculates if performance becomes too low, after arriving at a new destination, when changing graphics mode, pressing Reset, or toggling the option off and on.
  - It detects a fixed FPS (stable with minimal variation) and, when found, sets that as the target and switches to Fixed FPS automation.
  - Otherwise, with IFR or an Expert profile, the target ranges from 95% of the current average FPS on the ground to 85% at or above 3000 ft, leaving headroom for Max TLOD. VFR reduces each percentage by 5%.
  - Auto settings reduction, TLOD Base Extra and Free are mutually exclusive with Auto Target FPS, so Fixed TLOD must be used.
- **Warning System:** Setting Target FPS strictly higher than a detected cap triggers an orange mismatch highlight with corrective tooltips (buffered by 0.5 FPS to avoid timing jitter alarms).

### Supported Graphics Modes
- **NFR (Native Frame Rate):** Standard 2D rendering without frame generation.
- **VR:** Native OpenXR virtual reality rendering inside MSFS.
- **nVidia FG (DLSS 3 / FG Mod / MFG):** Native frame generation. Requires MSFS window focus; disengages when the simulator loses focus.
- **FSR FG:** AMD frame generation in MSFS 2024 (2× multiplier), operating regardless of window focus.
- **MSM (MFG Super Motion):** Autodetected when the mod process is running. Parses dynamic multipliers (2X–6X, 1X Adaptive) with independent IFR/VFR profiles and tracks window focus (`MSM Inact` at base framerate when unfocused).
- **Lossless Scaling FG (LSFG):** Autodetected when Lossless Scaling is running. Adaptive mode operates as LSFG using the pre-LSFG multiplier.
- **OFXR (Optical Flow OpenXR):** Dedicated VR graphics mode for the OFXR Bridge Tray app across community builds. Features sub-50 ms controller loop synchronisation, auto-derived multipliers, and pacing desync settle recovery.
- **VXR (VectorXR):** Dedicated VR mode integrating directly with VectorXR via Status API v1. Reads live compositor telemetry, reports fractional multipliers (e.g. `VXRDyn 2.4X`), and handles standby states.
- **Dual-Layer VR Conflict Protection:** If both OFXR and VectorXR OpenXR layers are detected executing concurrently, AutoFPS warns the user, reverts to standard VR mode (`VR Conflict!`), and pauses LOD automation to avert frame pacing contention and VRAM spikes.
- **Man FG (Manual Frame Generation):** Forces manual multipliers (2X to 8X or DynFG) for unsupported or external frame generation utilities.

### Using the Graphics Modes
- Core modes (NFR, VR, FG, FSR, Man FG) are always present. External mods (OFXR, VXR, LSFG, MSM) only appear when detected, and AutoFPS falls back to NFR if they close.
- The app starts in the last-used mode and switches to the detected mode when a flight loads. **Man FG** is a forced mode: it stays selected across restarts until you choose Auto to return to detection.
- Each graphics mode and flight type has its own Target FPS and its own Manual, Auto or Fixed option. You can edit an inactive mode's target and option in the dropdowns, which is handy for VR because you can set it in 2D before putting the headset on.
- Dynamic or adaptive FG set outside MSFS should be configured under **Man FG** (DynFG). With MSFS as the FPS source, Target FPS is the NFR (1×) value. With RTSS as the FPS source, Target FPS is a post-FG value, the active multiplier is shown in the status line, and TLOD automation is disabled because pre-FG frametimes cannot be derived reliably.
- **nVidia FG** only works while MSFS has the focus (driver behaviour), so use the app's **On Top** option to keep MSFS focused.
- **Lossless Scaling** needs v2.13.2 or later, is detected whenever LS is running, and uses a profile named `MSFS2020` or `MSFS2024`, falling back to the first profile. Press Reset after changing LS settings mid-flight, and use only one FG type at a time.
- See the Detailed Reference at the end for the full behaviour of each mode.

---

## Expert Mode Reference Guide

Enable **Expert** (Use Expert Options) to reveal deep customisation controls across automation algorithms, altitude transitions, and scenery offsets.

### TLOD Automation Methods
- **FPS Sensitivity (Default & Recommended):** Adjusts TLOD in smaller, more frequent increments proportional to the variance between current and target FPS. Lower sensitivity values (e.g. 2–3) produce smaller steps, ideal for resolving visual stutters. Step size equals double the sensitivity value.
- **Auto TLOD:** Disregards FPS targets and automates TLOD strictly according to aircraft altitude (AGL), interpolating between Base Min and Top Max.
- **Retired methods:** FPS Tolerance and FPS Cap were retired in 0.5.5.1. Profiles using them are converted automatically, with a message: FPS Tolerance profiles become FPS Sensitivity, and FPS Cap profiles become FPS Sensitivity with Fixed Target FPS for every graphics mode. Use FPS Sensitivity with Fixed Target FPS for locked framerates.

### TLOD Altitude & Range Settings
- **TLOD Base Min:** Minimum TLOD applied at or below Alt TLOD Base (Range: 10 to Top Max - 10).
- **Alt TLOD Base:** Altitude (AGL) at or below which TLOD remains at Base Min (Range: 100 ft to 100,000 ft).
- **TLOD Top Max / Min:** Ceiling TLOD applied at or above Alt TLOD Top (Range: Base Min + 10 to 1,000).
- **Alt TLOD Top:** Altitude (AGL) at or above which TLOD can reach Top Max. Tooltips calculate recommended climb/descent rates (defaulting to 1,350 fpm for VFR and 2,000 fpm for IFR).
- **TLOD Base Extra:**
  - **Fixed:** Disables extra ground seeking.
  - **Extra:** Progressively seeks higher base TLOD when parked or taxiing (≤ 5 knots) under favourable headroom, freezing or reducing during takeoff rolls and low flight.
  - **Free:** Allows free-ranging TLOD between Base Min and Top Max at all altitudes based on real-time headroom (recommended only for rural VFR).
  - **Start Max:** Begins flight sessions with maximum extra TLOD rather than ramping up from base. It is skipped when VRAM is already above the VRAM+ recovery threshold at session start, and cancelled if VRAM reaches the VRAM+ reduce threshold during the FPS settle.
- **Taxiing and Takeoff:** TLOD changes are allowed while taxiing at up to 20 knots, but not during a takeoff roll (above 20 knots, or above 5 knots while accelerating). The same rule gates cloud quality increases and settings reduction recovery.
- **Mountains+ (Mtns):** Expands Top Max TLOD by a set offset when terrain elevation exceeds a defined threshold (e.g. 1,000 ft AGL).
- **Night Mode (Per-Profile):** A dropdown in each profile:
  - **Off:** Maintains standard daytime settings around the clock.
  - **TLOD÷2:** Halves Top Max TLOD at night to eliminate distant rendering that cannot be seen in the dark.
  - **Clds-X:** Steps down volumetric clouds by the Night cloud reduction levels (never below Low), keeping full draw distance for night city lights and runways while reducing cloud load.
  - **Both:** Applies both TLOD halving and cloud reduction simultaneously.
  - *Night cloud reduction levels:* One global setting per sim config file (`NightCloudReduceLevelsGlobal`, 1 to 3, default 2) used by all profiles, Non-Expert included. VR has its own value (`NightCloudReduceLevelsGlobalVR`, 1 to 3, default 1), because VR cloud quality is usually lower than 2D on the same system. The Clds-X label shows the value for the graphics mode selected in Target. Night cloud reduction and its daytime restore apply straight away, not after the FPS settle.
  - *Switching:* Day or night is detected from the sim's automatic brightness, so timing follows the actual light level including latitude, season and cloud cover. `DayNightSwitchLevel` (0 to 10, default 5) in `MSFS2020_AutoFPS.config` / `MSFS2024_AutoFPS.config` moves the switch earlier or later: 0 is about sunrise / sunset, 5 is about the end of civil twilight and 10 is near total darkness. Set it to 0 for the pre-0.5.5.1 timing.
  - *Hold:* After each flip, further flips are held for 5 minutes of sim time (`DayNightSwitchHoldMinutes`, 1 to 30), so flying in and out of broken cloud does not flip night settings on and off. The hold ends early if brightness moves `DayNightSwitchHoldLevels` levels (0 to 2, default 2) past the switch level. A manual sim time change ends any hold and day/night is not decided until 3 seconds after the last change.
  - *Behaviour:* Automatically clears any active daytime cloud boost (`IncCloudQ`) upon entering night mode and restores it at sunrise. Active (or disabled) settings reduction takes priority over night cloud reduction in the Clouds / Reduce display, with the night reduction shown in the tooltip.

### Auto OLOD
- Adjusts Objects LOD based on an altitude band independent of FPS to avoid automation contention.
- Interpolates between **OLOD @ Base** (at or below Alt OLOD Base) and **OLOD @ Top** (at or above Alt OLOD Top). Allows higher OLOD on the ground for airport detail and lower OLOD at cruise, or vice versa.

### Flight Profiles & Command Line Arguments
- Supports up to 12 fully customisable profiles. Adjust the available total by editing `ProfileCount` (range 4–12, default 8: VFR, IFR, User 1–6) in `MSFS_AutoFPS.config`.
- **Profile Renaming:** Double-click the profile dropdown to rename. Press Enter or click away to save.
- **VR Auto-Switching:** Append `" VR"` to a profile name matching a 2D profile (e.g. `IFR` and `IFR VR`). AutoFPS switches profiles automatically when entering or exiting VR (`↔` symbol displayed).
- **Sim Rate Switching:** Append `" xX"` (e.g. `IFR 1X` and `IFR 4X`) to trigger automatic profile switching when acceleration rates change.
- **Day/Night Pairing:** Name two profiles the same, with one ending in `" Night"` (e.g. `IFR` and `IFR Night`), and AutoFPS uses the Night profile at night and the other during the day (Day or Night shows on the status line). See the Detailed Reference for how the two profiles interact.
- **Name Matching:** VR, Night and sim rate pairing ignores capitalisation and unusual spacing (e.g. `IFR night` or `IFR  2x` still pair). Profile names are cleaned on load and when renamed.
- **Landing Gear Delay (`DelayLG`):** Include `"DelayLG"` in a profile name to lock TLOD at Top Max during descent until the landing gear is lowered.
- **Command Line Switches:** Launch the application with predefined profiles:
  - `-ifr` and `-vfr`: Launches AutoFPS with the default IFR or VFR profile (even if renamed).
  - `-user1` through `-user6`: Launches directly into the corresponding User profile (Expert mode).
  - `-profile "<profile name>"`: Loads the exact specified profile name (Expert mode).

### MSFS Performance Optimiser
Enabled via the `+` checkbox beside the Sim Version label. Directs thread affinity and scheduling to improve simulator frame pacing:
- **CPU Affinity:** Enforces physical-core-only execution (excluding SMT/Hyperthreads). On Intel hybrid architectures, restricts MSFS to performance cores (P-cores). Dual-CCD AMD processors can enable first-CCD affinity.
- **Process Priority:** Automatically elevates MSFS process priority from Normal to High.
- **Power Plan:** Overrides Windows Balanced plan to High Performance or Ultimate Performance when the sim is active.
- *Safety:* Stands down if external utilities (Process Lasso, VR Auto Optimiser) have already adjusted simulator affinities.
- **Config File Parameters (`MSFS_AutoFPS.config`):**
  - `AffinityPhysicalCoreThreshold` (default `6`): SMT-core threshold at which physical-core-only affinity is applied. Setting to `32` disables affinity changes on most CPUs.
  - `AMDUseFirstCCDOnly` (default `true`): Activates first-CCD affinity on dual-CCD AMD CPUs (`CCD+` shown in tooltip).
  - `MSFSProcessPriority` (default `High`): Target MSFS priority level (`Normal`, `AboveNormal`, `High`).
  - `PowerPlanEnabled` (default `true`): Toggles automatic power-plan switching.

### Simulator-Specific Features
- **MSFS 2020 Cloud Quality Reduction:** Drops volumetric clouds by one level if target FPS cannot be achieved at Base Min TLOD or if GPU load exceeds 98% (with GPU-Z running).
- **MSFS 2024 Auto Settings Reduction:** Progressively steps down non-LOD settings (shadows, trees, displacement maps, ray tracing) during sustained performance deficits when TLOD is already at its minimum, restoring them once headroom recovers.
- **MSFS 2024 Auto Increase Clouds:** Boosts cloud quality by one level if TLOD Base Extra is active and substantial performance margin remains.
- **MSFS 2024 Hybrid Dynamic Settings:** Allows AutoFPS to manage baseline LOD ranges while letting MSFS's internal dynamic settings engine handle micro-adjustments using native internal metrics.

---

## Companion Apps & Advanced Features

### GPU-Z Companion App & VRAM+ Protection
MSFS 2024 and MSFS 2020 in DX12 mode are very demanding on VRAM. When VRAM overflows into much slower system RAM, performance can drop to single digit FPS and MSFS becomes more prone to CTDs. VRAM+ monitors usage via GPU-Z shared memory and proactively holds or sheds settings to prevent the overflow.

#### Setup
1. Download and install [GPU-Z](https://www.techpowerup.com/download/techpowerup-gpu-z/).
2. In GPU-Z settings: enable *Minimise on close*, *Load at Windows startup (minimised)*, *Minimise to system tray*, and *Refresh sensors while in background*.
3. Make sure the card selected in the GPU-Z GPU dropdown is the GPU driving MSFS. GPU-Z shares the sensors of its selected card and AutoFPS cannot choose a different one.
4. VRAM+ is on by default, including in Non-Expert mode, and only works while GPU-Z is running.

#### VRAM+ Option (Expert mode)
- **MSFS 2020:** Enabling uses the LODs-only setting, providing up to a 50% reduction.
- **MSFS 2024:** **Off** disables the feature. **Max** (default) uses the full settings reduction suite minus clouds. **Set** uses the shared auto settings reduction options, whether or not auto settings reduction is enabled.
- Hover over the VRAM+ dropdown for a tooltip built from your live hold, reduce and recovery thresholds.

#### How it behaves
- **Hold** (default 96% VRAM in use): TLOD is capped at its current value, even if favourable performance conditions exist. The limit symbol (⊤) shows beside TLOD in Sim Values, with a tooltip.
- **Reduce** (default 98%): settings are progressively reduced (LODs by up to 50%, other settings down by up to 2 levels) until VRAM falls back below the Hold threshold. The **Reduce** label changes to **VRAM+** in red, and the status line appends **HLD** or **RED**.
- **Recovery** (default 93%, at least 5% below Reduce): settings progressively recover once VRAM has enough headroom, potentially back to their defaults.
- **Start Max:** is skipped when VRAM at session start is already above the recovery threshold, and cancelled if VRAM reaches the reduce threshold during the FPS settle. VRAM is monitored throughout the settle.
- If VRAM+ keeps activating, reduce your TLOD settings and/or other MSFS graphics settings.

#### Automation Thresholds
The three thresholds are changed in `MSFS2020_AutoFPS.config` / `MSFS2024_AutoFPS.config` (in `%appdata%\MSFS_AutoFPS`) after running the app once following an update:
- `VRAMOverflowHoldTLOD` (default `96`%): caps TLOD to its current value, preventing further upward seeking.
- `VRAMOverflowReduceTLOD` (default `98`%): progressively invokes settings reduction (or reduces LODs by up to 50%) until memory pressure stabilises.
- `VRAMOverflowSettingsRecovery` (default `93`%): must be at least 5% below the reduction threshold before settings are permitted to recover.

See *VRAM+ in full* in the Detailed Reference for every behaviour.

### RTSS Companion App & Periodic Spike Protection
RivaTuner Statistics Server ([RTSS](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/)) provides external frame timing metrics and enables detection of cyclic frame time stutter often encountered over dense photogrammetry.

- Spike Protection requires RTSS to be installed and running.
- It detects periodic frame time spikes (cadence of 0.3–1.8 seconds) matching photogrammetry reload stutters.
- When protection is active, AutoFPS applies temporary TLOD reductions, smoothly restoring values over time and distance once the spike train subsides.
- Logging is streamlined to record events only when 4 or more spikes occur outside a 1.8-second cadence window, minimising log file growth.
- `SpikeThresholdMultiplier` in `MSFS_AutoFPS.config` (default `2.0`) controls the multiple of the frame time median at which a spike is counted.

### Monitor Only
Choose **Monitor only** at the end of the Flight Type list (Expert or Non-Expert mode) to run AutoFPS purely as a performance monitor and logger:
- AutoFPS stops changing any sim settings (the status line shows Monitor Mode). On selecting it, the sim is first returned to your own default TLOD, OLOD, cloud quality and key settings.
- It is remembered between sessions until you choose another flight type, which reloads that profile and does a full reset. It is not a profile, and your profiles are untouched.
- The layout is simplified across the desktop app, widget and web interface. Log+ is forced on and locked while you are in Monitor only.
- You can change TLOD, OLOD and cloud quality in the MSFS options at any time, in or out of a flight. The readings update immediately once you save, and nothing is restored or reapplied.
- VR, sim rate and Night profile auto-switching are suspended.

### Log+ Diagnostic Mode & Performance Logging
Enable **Log+** in Expert mode to access advanced diagnostics:
- **Detailed FPS Logging:** Click the live FPS readout during flight to record raw and averaged FPS to the log file every second for a defined duration (indicated by alternating purple text). Default duration is 30 seconds, changeable via `logFPSDetailsCountMax` in `MSFS_AutoFPS.config`.
- **Camera State Logging:** Logs simulator camera transitions to help diagnose view-change stutters.
- **System Metrics Logging:** Logs total CPU load, dominant core usage, MSFS process usage, GPU load, and VRAM pressure using exponentially weighted moving averages (EWMA).
- **Config File Parameters (`MSFS_AutoFPS.config`):**
  - `perfLogTopCoreCount` (default `2`): Number of busiest cores tracked.
  - `PerfLogBufferSize` (default `15`): Pre-breach and post-recovery sample buffer.
  - `PerfLoggingAlways` (default `false`): Forces continuous logging when Log+ is active.
  - `EwmaAlpha` (default `0.1`): Smoothing coefficient for scheduler jitter.
  - `MonitorMode`: no longer enables monitoring. It only stores the Monitor only Flight Type selection, so choose **Monitor only** in the Flight Type list instead (see [Monitor Only](#monitor-only)).

---

## Troubleshooting & FAQ

### Start here
- Installer will not download or run, or is flagged as unsafe: see [Installer Problems](#installer-problems).
- SimConnect stays red, TLOD and OLOD show zero, or a WASM exception is in the log: see [SimConnect stays red, values show zero or a WASM exception](#simconnect-stays-red-values-show-zero-or-a-wasm-exception).
- Need a clean MSFS reinstall: see [MSFS Clean Reinstallation Instructions](#msfs-clean-reinstallation-instructions).
- "Unable to attach MSFS" or "MSFS compatibility test failed": see [App Messages](#app-messages).
- Stutters, changing settings or VRAM+ questions: see the [FAQ](#frequently-asked-questions).

### SimConnect stays red, values show zero or a WASM exception
This covers SimConnect staying red, TLOD and OLOD showing zero, "Critical Exception occurred: MSFS_AutoFPS - Unable to load DLL 'GpuzShMem.x64.dll' or one of its dependencies" in the log file, and a SimConnect / WASM exception such as Exception 31 or Exception 25. Work through these in order and stop as soon as it works:
1. **Repair.** Close MSFS, run the **MSFS_AutoFPS installer** (the one downloaded from the Releases page, not an MSFS installer), choose **Repair** and reboot. If a redistributable fails to install, install or repair these yourself:
   - Microsoft Visual C++ 2015 - 2022 Redistributable: [x86](https://aka.ms/vs/17/release/vc_redist.x86.exe) and [x64](https://aka.ms/vs/17/release/vc_redist.x64.exe).
   - The .NET Desktop Runtime x64 from the [Microsoft .NET 8.0 download page](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
2. **Remove duplicate MobiFlight modules.** Duplicates in both your Official and Community folders cause Sim Values to read zero. Remove the older copies, rerun the installer and start the app again.
3. **Delete the MSFS WASM folder** (when the log shows an exception such as 31 or 25, the WASM installation is most likely corrupt). It is located under the Microsoft Flight Simulator directory in either `%appdata%` (Steam) or `%localappdata%` (MS Store) and MSFS rebuilds it next time it runs. Rebooting is also recommended.
4. **Clean reinstall MSFS.** If that does not fix it, a full clean reinstall of MSFS is required: see [MSFS Clean Reinstallation Instructions](#msfs-clean-reinstallation-instructions).

### MSFS Clean Reinstallation Instructions
*A clean reinstallation takes under 15 minutes in MSFS 2024, but several hours in MSFS 2020. Only undertake this if troubleshooting persistent WASM or core simulator crashes.*

1. Back up or relocate your Community folder.
2. Follow the official Zendesk clean install instructions exactly:
   https://flightsimulator.zendesk.com/hc/en-us/articles/17335196046108-How-to-clean-install-the-simulator-on-PC
3. **Crucial:** Manually delete leftover simulator AppData/LocalState directories as specified in the guide. Controller profiles, logbooks, and cloud saves are retained on Xbox Live servers.
4. Reinstall the simulator, launch once to the main menu, and then restore your Community folder.

### Installer Problems
- **Installer did not download or is zero size:** It should be about 3 MB. Try another browser, or temporarily disable your AV.
- **Installer will not run, or is flagged as untrusted:** Windows SmartScreen may block it because the app is new. Right-click the installer, choose Properties, tick **Unblock**, then Apply and OK, and try again. Otherwise create an exception for the installer in your AV program, or disable the AV temporarily while running it.
- **Cannot run the installer at all:** Follow the manual installation steps below.
- **Previously removed MSFS2020_AutoFPS without using its installer:** You may have problems installing MSFS_AutoFPS. Run the MSFS_AutoFPS installer and select remove, install [MSFS2020_AutoFPS 0.4.3.1](https://github.com/ResetXPDR/MSFS2020_AutoFPS/releases/download/v0.4.3.1/MSFS_AutoFPS-installer-v0.4.3.1.exe) with no autostart options, then rerun the MSFS_AutoFPS installer and choose your autostart option.
- **Bypass MobiFlight check:** If the installer fails during the MobiFlight check despite the module being current, create a shortcut to the installer, append `-bypassmobiflight` to the Target field, and run the shortcut.
- **Unable to locate Community folder:** Often caused by leftover simulator userdata following an MSFS uninstall. AutoFPS checks for `UserCfg.opt` to verify simulator presence, so removing orphan userdata folders resolves the false detection. Alternatively use the `2020 Folder...` or `2024 Folder...` button in the installer.
- **XML Exception during autostart:** "XML Exception: Unexpected XML declaration" indicates corruption in your `EXE.xml` file. Paste the file content into an XML validator or AI tool to correct the structure, save, and rerun the installer.
- **Installer error:** An unexpected installer error now shows the reason and next steps instead of leaving the installer stuck.

### Manual Installation (Without Installer)
If the installer cannot be run:
1. Download the [MobiFlight WASM Module](https://github.com/MobiFlight/MobiFlight-WASM-Module/releases) and extract it into your MSFS Community folder (without MSFS running).
2. Install the Visual C++ Redistributable ([x86](https://aka.ms/vs/17/release/vc_redist.x86.exe) and [x64](https://aka.ms/vs/17/release/vc_redist.x64.exe)).
3. Download and install the latest [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
4. Create the folder `%appdata%\MSFS_AutoFPS\bin`.
5. Download [MSFS_AutoFPS.zip](https://github.com/ResetXPDR/MSFS_AutoFPS/blob/main/MSFS_AutoFPS.zip) and extract its contents into the newly created `bin` folder.
6. Create a shortcut to `MSFS_AutoFPS.exe` on your desktop.

### App Messages
- **"Unable to attach MSFS - app disabled":** The most likely causes are MSFS loading very slowly so the attachment times out, MSFS and AutoFPS running at different permission levels (e.g. MSFS running as Admin while AutoFPS is not), or your anti-virus blocking the app. Restart AutoFPS after MSFS has loaded to the main menu, check that the elevation levels match, set an anti-virus exclusion for the app, and as a last resort run AutoFPS as administrator.
- **"MSFS compatibility test failed - app disabled":**
  - You started MSFS, changed MSFS settings and then started AutoFPS. Exit AutoFPS, toggle any simple setting (e.g. VSync) in the MSFS settings menu, save, and restart AutoFPS. If that does not work, exit both completely, start AutoFPS, then start MSFS.
  - `UserCfg.opt` contains values beyond what the MSFS menu allows. Reset the graphics settings to default (F12) in the MSFS main menu settings for both PC (MSFS 2020) or General (MSFS 2024) and VR, then make all changes within the MSFS settings menu.
  - A new MSFS version may have moved the memory map. AutoFPS will first auto-install an explicitly compatible update if one is available, otherwise it offers the update most likely to be compatible, which may be a test build. If none is found or it does not help, I am likely already aware and working on it. If you may be among the first to hit it (for example on an MSFS beta), please raise a new issue on GitHub or add to an existing one.
  - There may be a permissions issue and you may need to run the app as Administrator.

### Frequently Asked Questions

#### How does AutoFPS compare to the in-built Dynamic Setting feature in MSFS 2024?
- **Independent Adjustment:** MSFS Dynamic Settings adjusts TLOD first and then OLOD if performance remains poor. AutoFPS manages TLOD and OLOD independently, with OLOD adjusting inversely proportional to altitude by default. AutoFPS can also manage a wider suite of visual settings when Settings Reduction and VRAM+ are active.
- **Adjustment Range:** MSFS Dynamic Settings only reduces down to 50% of your configured setting. AutoFPS in Non-Expert mode uses defaults as a midpoint, and in Expert mode can be set to any range, including beyond the in-game menu maximum of 400.
- **Response Speed:** MSFS Dynamic Settings responds faster due to internal metric access, but tends to overreact to brief stutters. AutoFPS filters short-term spikes to maintain visual stability.
- **Frame Generation Awareness:** MSFS Dynamic Settings bases its target on native framerate rather than delivered framerate when frame generation is active. AutoFPS accommodates all mainstream frame generation technologies with post-FG targets.
- **Hybrid Automation:** In Expert Mode, AutoFPS can work alongside MSFS Dynamic Settings: MSFS handles rapid micro-adjustments while AutoFPS manages base LOD ranges and external settings reductions.

#### Why does my antivirus or browser flag the installer as untrusted?
AutoFPS is a hobbyist project. Code signing certificates carry prohibitive costs, so new releases may occasionally trigger false positives (Windows SmartScreen, Kaspersky, or browser download warnings) until community reputation builds. If blocked, right-click the installer, select **Properties**, check **Unblock**, and click **Apply**. Downloaded updates are also verified by AutoFPS against the SHA-256 digest published by GitHub before they are launched.

#### Why are my simulator settings restoring to unexpected values?
AutoFPS records your default settings when a flight begins and restores them when returning to the main menu or exiting the app. If you alter graphics settings in the MSFS menu while in an active flight with AutoFPS running, those changes will be overwritten. To change baseline settings, do so from the MSFS main menu or with AutoFPS closed, or choose **Monitor only**, where AutoFPS does not change or restore anything.

#### Can you tell me the best settings for me to use?
- The best settings depend on your system, the other addons and apps you use, and what you mean by "best": higher FPS, better visuals or a smoother experience. Some of these are mutually exclusive.
- There are now thousands of users of this app and I don't have the time to give individual settings advice.
- Start with Non-Expert mode to see what the app does and when. When you are comfortable, enable Expert and read the tooltip and README for each setting.
- Make small changes to only a few settings at a time so you can see the effect of each.

#### How do I eliminate visual or audio stutters when using the app?
1. Revert to the default configuration using the installer's **Reset Configs** option.
2. Begin in Non-Expert mode with the IFR profile and Auto Target FPS.
3. If the MSFS Performance Optimiser is enabled, try disabling it, as core affinity changes can cause audio crackling on certain hardware.
4. In Expert mode, lower **FPS Sensitivity** to 2 or 3 to enforce smaller, gradual TLOD adjustments.

#### What does VRAM+ do and why does it show the wrong GPU or not work?
VRAM+ holds or reduces key settings when VRAM is about to overflow (see [GPU-Z Companion App & VRAM+ Protection](#gpu-z-companion-app--vram-protection)). It only works while GPU-Z is running. GPU-Z shares the sensors of the card selected in its own GPU dropdown, and AutoFPS cannot choose a different card, so on a system with more than one GPU select the card that drives MSFS in GPU-Z.

#### Why does RTSS framerate telemetry freeze?
Switching between 2D Frame Generation and VR can latch the swapchain in shared memory. Clicking the FPS source icon on the app or widget toggles capture back to MSFS. If RTSS remains frozen, restarting RTSS or MSFS clears the shared memory handle.

---

## Detailed Reference

The sections below hold the full technical detail from the original README. Click a heading to expand it.

<details>
<summary><strong>Credits and licence history</strong></summary>

AutoFPS is based on muumimorko's idea and code in MSFS_AdaptiveLOD, as further developed by Fragtality in DynamicLOD, myself (ResetXPDR) in DynamicLOD_ResetEdition and MSFS2020_AutoFPS, and with inspiration from changes suggested by kayjay1c6b from his MSFS2024_AutoFPS. My thanks to all of them.

Versions up to and including v0.5.5.0 were released under the MIT Licence, which requires all previous authors' efforts to be appropriately attributed in the documentation and website descriptions of any derivative software. Later versions use the MSFS_AutoFPS Licence in [LICENSE.txt](LICENSE.txt), which keeps that requirement. Parts built on the earlier authors' work remain under their original licences.

</details>

<details>
<summary><strong>Graphics modes: full behaviour of each mode</strong></summary>

### What are these various graphics modes shown in the dropdown list for Target FPS and on the status line and how do I use them?
- **How to use**
  - The app uses dynamic graphics mode management. Core modes (NFR, VR, FG, FSR, Man FG) are always present, while external mods (OFXR, VXR, LSFG, MSM) populate only when detected on your system, falling back to NFR if closed.
  - App defaults to last-used mode on startup.
  - Auto-switches to detected mode on flight load (excludes Man FG, which is manually selected and locked).
  - Target FPS dropdown allows editing inactive mode’s setting.
  - Dropdown background turns orange when target FPS differs from current mode.
  - VR-friendly: users can adjust target FPS in 2D before adorning and activating VR headset (pre-flight Sim Values automatically reflect VR defaults when VR, OFXR, or VXR is selected).
  - Dynamic/adaptive FG of any type set external to MSFS should be configured under Man FG, otherwise it should auto-detect.
    - When using **MSFS as the FPS source**, Target FPS is set using the NFR (1×) value, and FPS displays as NFR because DynFG’s dynamic behaviour prevents reliable detection of its actual FG multiplier at any specific point in time.
    - When using **RTSS as the FPS source**, Target FPS is set as a post-FG target value. AutoFPS calculates and displays the active dynamic FG multiplier in real time on the status line. TLOD automation is disabled in this specific configuration because pre-FG base frametimes cannot be reliably derived.
- **NFR (Native Frame Rate)**
  - Default non-VR, non-FG graphics mode (formerly “PC”).
  - Automatically selected when MSFS graphics settings do not enable FG.
  - Autodetected by AutoFPS.
  - No frame multiplication applied.
- **VR**
  - Activated within MSFS.
  - Autodetected by AutoFPS.
  - No notable limitations.
- **OFXR (Optical Flow OpenXR)**
  - Dedicated VR graphics mode using the external OFXR Bridge Tray app.
  - Autodetected when entering VR with supported community builds running and armed.
  - Automatically reverts to standard VR mode if the bridge is disarmed.
  - Sub-50 ms controller loop synchronisation with live compositor telemetry.
  - Automatically derives effective multipliers in Auto mode (e.g. OFXR Auto 2X).
  - Recovers from pacing desyncs using an FPS settle event rather than locking LOD adjustments indefinitely.
  - Pausing via hotkey (`OFXR ⏸`) reverts to native sim FPS and freezes LOD adjustments for steady visual comparisons.
  - Fixed FPS mismatch warnings are disabled in this mode.
- **VXR (VectorXR)**
  - Dedicated VR graphics mode integrating directly with VectorXR via its Status API.
  - Reads real-time OpenXR compositor telemetry, reports fractional delivered multipliers, and supports paused and standby states.
  - Fixed FPS mismatch warnings are disabled in this mode.
- **Dual-Layer VR Conflict Protection**
  - If both OFXR and VectorXR OpenXR layers run simultaneously, AutoFPS warns the user, falls back to standard VR mode, and pauses LOD automation to prevent pacing contention.
- **nVidia FG (native, FG mod or MFG)**
  - Activated via MSFS graphics settings or FG mod.
  - MFG is auto-detected if DLSS FG 3X or greater is set within MSFS settings.
  - Dynamic FG is detected when set within MSFS settings.
  - Autodetected by AutoFPS but only active when MSFS is the focused window.
  - FG becomes inactive when MSFS loses focus (driver-level behaviour).
  - Use app’s “On Top” option to ensure MSFS maintains focus.
- **MSM (MFG Super Motion)**
  - Autodetected when the MFG Super Motion mod process is running.
  - Resolves dynamic multipliers with independent IFR and VFR Target FPS settings.
  - Tracks MSFS window focus matching native FG: displays MSM Inact at base framerate when MSFS loses focus.
- **Lossless Scaling FG (LSFG)**
  - Activated through LSFG app (v2.13.2+ required).
  - Auto detected when LS is running, regardless of actual FG activation.
  - AutoFPS checks for profiles named `MSFS2020` or `MSFS2024`.
    - Falls back to first config profile (usually `Default`) if none found.
  - Adaptive mode operates as LSFG using the pre-LSFG multiplier (e.g. 1X).
  - After changing LS settings mid-flight, press Reset to redetect.
  - Only one FG type should be active—mixing LSFG with native FG or FG mod causes incorrect FPS readings.
- **FSR FG**
  - Available in MSFS 2024, which supports FSR 3 and 4.
  - Activated via MSFS graphics settings.
  - Autodetected by AutoFPS.
  - Uses fixed 2× multiplier.
  - Remains active regardless of window focus.
- **Man FG (Manual FG)**
  - Allows future-proofing for externally set or unsupported FG types via manual tuning.
  - Manual FG selection from 2X to 8X.
  - Use DynFG for dynamic/adaptive FG setups set external to MSFS.
  - Functions as a **forced mode**, remaining active across app restarts until manually changed to a different graphics mode.
  - Select Auto to return to auto detection of all other modes.

</details>

<details>
<summary><strong>Connection status, Sim Values, FPS source, updates, VRAM+, hybrid dynamic settings and status messages</strong></summary>

- Connection Status
  - Red values indicate not connected, green is connected, or royal blue for the Sim Version if the MSFS Performance Optimiser is enabled.
  - Automatically identifies which MSFS version is in use as either MSFS2020 or MSFS2024 and the version number. 
  - If the sim version is showing in red and is not the MSFS version you wish to configure before starting that MSFS version, click the 20>24 or 24>20 button, as applicable, and it will change to that.
  - **Widget Status Indicator:**
    - A `Widget` connection status label appears in this panel if the In-Game Widget is installed for the active simulator.
    - It shows green when either the in-game toolbar widget or the external web browser widget (`http://localhost:54321/`) is open and actively receiving telemetry, and red when closed or inactive.
  - MSFS Performance Optimiser - enabled via the "+" checkbox to the left of the Sim Version label:
    - When first enabled, a one‑off warning dialog advises users of potential performance, stuttering, audio, or unexpected MSFS behaviour when using this feature.
    - The Sim Values panel reflects optimiser‑controlled states such as CPU affinity, process priority, and power‑plan selection, updating immediately when these values are applied or restored.
    - Designed to change states only when they are at their default levels and have not already been modified by other tools (e.g., VR Auto Optimiser, Process Lasso), ensuring no conflict with external managers.
    - The Sim Version text changes to royal blue to indicate the optimiser is active and controlling MSFS.
    - Provides UI controls for Physical Cores, MSFS process priority, and Best Windows Power Plan, shown in Expert Mode when hovering over the optimiser checkbox or MSFS label.
    - Can be fine‑tuned with four user‑configurable options in the common config file.
    - CPU Affinity:
      - Uses a universal physical‑core rule based on SMT that gives consistent behaviour across AMD, Intel hybrid, and SMT‑off systems.
      - On Intel hybrid CPUs, MSFS runs on the P‑cores only, since E‑cores don’t support SMT and aren’t counted as physical cores in this rule.
      - The `AffinityPhysicalCoreThreshold` key, defaulting to 6, controls the SMT‑core threshold at which physical‑core‑only affinity is applied; setting this value to 32 effectively disables the feature on most CPUs.
      - The `AMDUseFirstCCDOnly` key, defaulting to true, activates first‑CCD affinity mode on dual‑CCD AMD CPUs. The tooltip appends "CCD+" to the physical‑core affinity line when this mode is active.
      - When enabled, sets CPU affinity to physical cores only, excluding SMT threads, and provides warnings or CCD‑specific options where applicable.
    - MSFS Process Priority:
      - Changes priority only when MSFS is running at the default Normal level, switching it to High when available and leaving any user‑set AboveNormal or RealTime priority untouched.
      - The `MSFSProcessPriority` key, defaulting to High, controls the target MSFS process priority (Normal / AboveNormal / High). RealTime is intentionally excluded because using it would require AutoFPS itself to run elevated.
      - Priority level is selectable via a dropdown UI control.
    - Windows Power Plan:
      - Overrides the power plan only when starting from the default Balanced plan, choosing Ultimate Performance or High Performance depending on availability.
      - The `PowerPlanEnabled` key, defaulting to true, explicitly enables or disables automatic power‑plan switching.
      - Power‑plan selection is available as a UI toggle.
    - Applies delayed second writes for both CPU affinity and process priority to ensure settings reliably stick on systems that ignore the initial set.
    - The optimiser tooltip dynamically rebuilds on load to show the active configuration, including the selected power plan, physical‑core affinity threshold, MSFS process priority, and the current UI‑controlled states.
- Sim Values
  - Will not show valid values unless all three connections are green, or royal blue for the Sim Version if the MSFS Performance Optimiser is enabled. n/a means not available right now.
  - When MSFS is detected and **NOT** in a flight session:
    - Default **TLOD**, **OLOD**, and **Cloud Quality** values are displayed and refresh within one second of changes made in the MSFS settings menu.
    - **VR‑specific defaults** show automatically when in VR, OFXR, or VXR mode or when VR, OFXR, or VXR is selected in the Target FPS dropdown.
    - The Sim Values header shows an **MSFS icon** to indicate default values are being displayed.
  - When in a flight session, the Sim Values header shows an **AutoFPS icon** to indicate values are being actively controlled by AutoFPS.
  - Green means the sim value meets or exceeds the target. Red means it is at its minimum or below target. Orange with arrows for TLOD or OLOD means the value is auto‑adjusting. Orange for FPS and TLOD means adjustment is limited because FG is inactive and TLOD increases are disabled. Black is shown otherwise.
  - Other symbols may be shown when applicable, such as value locked 🔒, increasing ▲ and decreasing ▼, upper limit ⊤ and lower limit ⊥.
  - Additional reduction settings values can be made visible when auto reduction or VRAM+ is active at Level 1 or greater by the user mousing over the Reduce value.
  - FPS display
    - FPS (raw) - shows the unfiltered raw FPS when a Fixed FPS is set or automatically detected.
      - Used for both on-screen reporting ("FPS") and all performance-management logic in fixed-FPS conditions.
      - Raw FPS is shown because the fixed FPS value is the most relevant for automation and performance decisions.
    - FPS+ (averaged) - shows the average FPS, filtered for spikes and dips, for the current graphics mode.
      - Smooths out any transient FPS spikes or dips experienced - such as those caused by sudden changes in view, panning, scenery loading or other transient events - so that undesired automated MSFS setting changes are minimised.
      - FPS values within 15% (FPS Sensitivity automation) or 10% (Auto TLOD automation) of the current average are averaged over a 5 second rolling window of FPS values.
      - FPS values outside of this range are considered outliers and are not included in the average until a sustained change over 3 seconds in the same direction is detected.
      - The average will recover more quickly if the very recent trend is detected to have minimal variance.
      - Averaging period is 5 seconds.
- FPS source icon - RTSS (RivaTuner Statistics Server), MSFS, OFXR, or VXR.
    - **[RTSS](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/)** is a well-established tool for FPS monitoring, widely used in the gaming community and fully compatible with MSFS.
    - RTSS is the default FPS source and will automatically revert to MSFS as the FPS source if RTSS is not installed and running, if Dynamic FG is the graphics mode (with MSFS FPS source) or if RTSS data is detected as frozen.
    - In VR sessions using OFXR or VectorXR, the icon displays the dedicated VR compositor source (**OFXR** or **VXR**) and is read-only.
    - In standard 2D and VR modes, clicking the FPS source icon on either the desktop app or the toolbar widget switches between MSFS and RTSS capture sources, provided RTSS is running.
    - The last used FPS source will be saved and restored upon the next app launch, when a flight session begins, or when the Reset button is pressed during a flight session.
    - RTSS data can freeze (swap-chain latched) when switching from 2D Frame Generation (FG) to VR or when MSFS recreates its graphics swap-chain.
      - FPS/frame time values stop updating and remain constant.
      - AutoFPS detects this as frozen RTSS data.
    - Restarting RTSS (manually or via AutoFPS running as Admin) clears the frozen shared‑memory state.
      - Effective after the RTSS injection delay passes, nominally 15 seconds.
      - Potential side‑effect: restarting RTSS while FG is active can trigger a graphics‑driver‑level MSFS CTD on some systems.
    - Restarting MSFS fully resets the graphics pipeline and RTSS integration.
      - Always resolves the frozen state.
      - Heavy‑handed and time‑consuming.
    - Disabling the MSFS RTSS compatibility profile prevents freezes when switching between 2D FG and VR.
      - MSFS must remain single‑window only (no pop‑outs).
      - Side‑effects:
        - FPS data becomes corrupted if any pop‑out windows are used.
        - MSFS can CTD when corrupted FPS data is fed back into RTSS.
    - AutoFPS displays a warning when unresolvable frozen RTSS data is detected.
      - Users can choose to suppress this warning in future runs.
- General
  - Update Management
    - Visible whenever not in a flight session.
    - **Silent Updates** install updates automatically without prompts, except for non-explicit compatibility updates and reversion from test versions, which are always prompted.
      - The installer window appears briefly during the update process and release notes are shown afterwards, with no user interaction required.
      - A one‑time migration prompt is provided for existing Prompted Updates users to switch to Silent Updates.
      - Older versions of the app that don’t recognise Silent Updates will continue to treat this setting as Prompted Updates, ensuring full backward compatibility.
    - **Prompted Updates** (default) automatically install updates but seeks user confirmation for updates and displays release notes.
      - Mandatory updates will auto install regardless of user settings.
      - Optional updates will seek user confirmation and switch to **Show Updates** if declined.
      - Installer runs automatically, showing Release Notes in Notepad and auto-starting the new version.
    - **Show Updates** displays available updates and download links.
    - **Mandatory Updates Only** displays and installs only mandatory updates.
    - **Update Channel** sets the minimum maturity level for updates (Release, RC+, Test+, Exp+), replacing the previous binary test‑update model.
      - The Update Channel dropdown only appears in non‑expert mode when set to a test channel (RC+, Test+, or Exp+); Release‑channel users must enable Expert Options to view or change it.
      - Test‑channel users who return to **Release** will be offered a one-time automatic rollback to the latest formal release.
      - **Mandatory Updates Only** remains unavailable while on any test channel.
      - Test‑channel updates run a shorter process than release updates, assuming all core components are already current.
      - Users on the **Exp+** channel can auto-update directly from release versions to experimental builds.
    - **Compatibility Updates** may be auto‑installed if the app fails its compatibility check and a matching update is available, which may be a test build if no stable version exists.
    - **Smart Deferred Updates:** If an update contains an In-Game Widget update while MSFS is running, the update is deferred automatically until MSFS closes. A status message indicates when an update is waiting.
    - **Verified Updates:** Before launching a downloaded installer, auto-update compares its SHA-256 with the digest GitHub publishes for the release asset (case ignored). An installer already in your Downloads folder is verified in place. On a mismatch it is deleted, downloaded again and verified again; if the release has no digest the update is refused and the file is left alone. Silent updates only write a failure to the log, otherwise an "Update Verification Failed" message is shown. Update downloads now time out instead of hanging.
    - App startup sequence ensures update check is completed before connecting to MSFS.
  - VRAM+ - protects against VRAM overflow using GPU-Z. See the [GPU-Z & VRAM+](#gpu-z-companion-app--vram-protection) section and *VRAM+ in full* below.
  - Hybrid Dynamic Settings/AutoFPS-controlled Automation in Expert mode with MSFS 2024:
    - Uses MSFS dynamic settings, if enabled in MSFS, for auto-LOD reduction; otherwise it reverts to AutoFPS-controlled LOD reduction if active.
    - A one-time message will appear when the app is first launched with MSFS 2024, explaining how to enable and disable the feature.
    - Displays `DynSet` and the associated default MSFS target frame rate in the Sim Values panel’s Reduce status, with a descriptive tooltip, when in a flight session and not at a reduction level greater than LODs-only.
    - TLOD and OLOD labels will show with a `"D"` prefix and tooltip when `DynSet` is active — even if AutoFPS reduction is disabled — as MSFS dynamic settings remain active.
    - An MSFS icon will also be shown before the AutoFPS icon in the Sim Values panel title when dynamic settings is enabled, to indicate hybrid automation is active.
    - `DynSet` and the MSFS target frame rate can be changed at any time, including mid-flight, with changes reflected in the Reduce status display.
    - DynSet enable/disable and increase/decrease buttons are available when Log+ is enabled to allow instant changes to these settings without needing to open MSFS settings. 
    - Unlike AutoFPS-controlled LOD reduction, actual reduction amounts are not reflected in the TLOD and OLOD display values, as these are internal to MSFS and currently only visible via Developer Mode FPS view.
    - Works best when using MSFS-set FPS cap, with Max and Dynamic Settings Target Frame Rates in MSFS settings set to the native frame rate equivalent of the AutoFPS target FPS, and a Fixed Target FPS in AutoFPS.
    - If not using an FPS cap, it is recommended to set the MSFS dynamic settings target frame rate to at least 10 FPS lower than the native frame rate equivalent of the AutoFPS target FPS for best results.
    - Since Dynamic Settings Target Frame Rate is only configurable in 5 FPS increments, choose the next lowest value if your desired setting is not a direct multiple of 5.
  - Status Message - Displays key system messages, such as:
    - Before loading a flight - whether a newer version of the app is available to download and install, or whether an update is deferred and waiting for MSFS to close.
    - When an auto-update completes - displays a temporary confirmation that the app was updated, which clears once the first flight session begins.
    - Loading in to a flight - whether MSFS memory integrity tests have failed, and
    - Flight is loaded
      - Shows current sim rate with a range of 0.125X to 16X, which will display at the start of the app status line for any value except 1X.
      - Displays a centred two-line status message showing detected Graphics Mode (NFR, FG, LSFG, MFG, MSM, FSR, OFXR, VXR or VR) and DX version (MSFS 2020 only), app pause, FPS settle, Mtns+, app priority mode and/or TLOD range as applicable.
      - The FPS settle timer runs for up to 30 seconds to allow FPS to settle between pausing/unpausing, auto target FPS calibration, TLOD Extra transitions and VR/OFXR/NFR/FG/LSFG mode transitions. This allows the FPS to stabilise before engaging automatic functions and should lead to much smaller TLOD changes when seeking the target FPS on such transitions.
      - App priority shows whether FPS or TLOD are the current automation priority. A + next to TLOD indicates that TLOD Extra has been activated and that a higher TLOD Base Min should be expected. 
      - Bonus GPU load display if the optional [GPU-Z](https://www.techpowerup.com/download/techpowerup-gpu-z/) companion app is installed and detected running when starting any flight session. Note, the GPU-Z companion app is required to be running if the Decrease Cloud Quality option is selected in conjunction with the GPU Load activation method, as GPU-Z provides the necessary GPU load information to the app for this method to function.
      - Auto pause will activate if in flight and either MSFS is in active pause or the MSFS settings menu is being accessed.

</details>

<details>
<summary><strong>VRAM+ in full</strong></summary>

- What it does
  - MSFS 2024 and MSFS 2020 in DX12 mode are very demanding on VRAM use, which can result in situations where its VRAM usage completely fills all available VRAM on your GPU and overflows into much slower system RAM.
  - VRAM overflow situations often lead to significant reductions in performance, in some cases down to single digit FPS, and make MSFS much more prone to CTD.
  - While MS/Asobo have reduced VRAM usage in MSFS 2024 SU2, many users with GPUs having less than 16GB VRAM still regularly encounter VRAM overflow situations, even with modest settings.
  - VRAM+ mitigates these potential overflow events by detecting an impending VRAM overflow and either holding or, if close enough, reducing key settings known to free up VRAM.
  - Settings reduce by up to 50% for LODs (no further for MSFS 2020) and down by 2 quality levels for other settings, but only as far as required to avert the VRAM overflow. When VRAM usage reduces to an acceptable level, these settings automatically increase again, potentially back to their default values if sufficient VRAM headroom exists.
- Requirements
  - Requires the [GPU-Z](https://www.techpowerup.com/download/techpowerup-gpu-z/) companion app to be installed and running. The feature is disabled if GPU-Z is not found running. If it is not running, a message suggesting you start it is displayed during the initial 30 second settle timer of each session.
  - Recommended GPU-Z settings are to minimise on close, load at Windows startup (minimised), and minimise to the system tray on the General tab, and refresh sensors while GPU-Z is in the background on the Sensors tab.
  - Ensure the GPU shown in the GPU-Z GPU dropdown list is the same as the GPU being used for MSFS. GPU-Z shares the sensors of its selected card and AutoFPS cannot choose another.
- Thresholds
  - Three thresholds for VRAM usage, all changeable in `MSFS2020_AutoFPS.config` / `MSFS2024_AutoFPS.config` after running the app once after updating:
    - `VRAMOverflowHoldTLOD`, defaults to >= 96% VRAM in use and caps TLOD to its current value, even if favourable performance conditions exist.
    - `VRAMOverflowReduceTLOD`, defaults to >= 98% VRAM in use and progressively activates settings reduction until the Hold threshold is achieved.
    - `VRAMOverflowSettingsRecovery`, must be at least 5% less than `VRAMOverflowReduceTLOD` (i.e. 93% by default) and allows settings to progressively increase with favourable performance conditions until the feature disengages.
- VRAM+ option (Expert mode only; enabled by default in either mode)
  - MSFS 2020: enabling uses the LODs-only setting, providing up to a 50% reduction.
  - MSFS 2024:
    - Off: disables the feature completely.
    - Max: (default) uses the full settings reduction suite minus clouds (to minimise impact on user experience), max levels 2, floor lowest and recovery altitude ground.
    - Set: uses the shared auto settings reduction options that will now be shown, regardless of whether auto settings reduction is enabled or not.
  - The tooltip on the VRAM+ dropdown is built from your live hold, reduce and recovery thresholds, and explains Max and Set, including that Auto Reduce Settings need not be ticked.
- Reduction behaviour
  - Settings reduction commences if the VRAM reduction threshold is exceeded, and continues until it is no longer exceeded, regardless of whether auto settings reduction is enabled or not.
  - With MSFS 2024, user auto reduce settings are overridden while VRAM reduction is active to the maximum possible reduction, namely max reduction steps the greater of 2 or the current setting, reduction settings floor off and reduction settings suite to full.
  - VRAM LOD reductions occur proportionally based on how far VRAM usage exceeds the reduce threshold until TLOD Min is reached, after which reductions occur at twice the normal rate to more quickly address VRAM overflow.
  - VRAM LOD recovery rate is half the normal LOD recovery rate to more gently recover from VRAM overflow.
  - VRAM protection limits LOD reductions to 50% max, aligning it with normal settings reduction.
  - VRAM+ triggering requires two consecutive threshold breaches before activating, in order to reduce the likelihood of false triggering.
  - Recovery is allowed at any altitude, including on the ground, due to the conservative 5% minimum reduction in VRAM use below the VRAM reduce threshold being required before recovery is allowed. Each VRAM+ recovery increases the recovery altitude setting by one, up to Alt TLOD Top, to reduce instances of VRAM+ cycling too often.
  - TLOD Base Extra seeking is cancelled while VRAM+ limiting is active and may fully reset if severe.
- Display
  - The "Reduce" sim value label changes to "VRAM+" and shows in red when VRAM settings reduction is active. The app status line appends HLD or RED to VRAM+ to indicate hold and reduction events respectively.
  - The VRAM+ padlock beside TLOD in Sim Values has been replaced by the limit symbol (⊤) already used when the sim's maximum TLOD is reached, as VRAM+ limits TLOD rather than locking it. The TLOD value has a tooltip explaining this while the symbol is shown.
- Start Max
  - Start Max is skipped when VRAM at session start is above the VRAM+ recovery threshold (VRAM+ users with GPU-Z running), so the profile starts at Base Min TLOD with the normal FPS settle. There is no change when VRAM+ is off or no GPU-Z reading is available.
  - Start Max is cancelled automatically if VRAM reaches the VRAM+ reduce threshold while the FPS settle after it is still running. The Start Max extra is cleared and TLOD is dropped straight to Base Min.
  - VRAM is now monitored throughout the settle, where previously VRAM+ did not run during it and an overflow at the start could continue unchecked.
- If you are continually experiencing VRAM+ activating, consider reducing your app TLOD settings and/or reducing other MSFS graphics settings.

</details>

<details>
<summary><strong>Target FPS options, profiles, Non-Expert defaults and Expert options overview</strong></summary>

- Target FPS — the most important setting in the app (range 10–500, fractional values of 0.5 supported)
    - Set this to the FPS you want the app to target after any frame‑generation technology has been applied.
    - Target FPS option (FPS Sensitivity mode only), saved separately for each graphics mode in each profile
      - Manual
        - Automates proportionally above and below the user‑set value in smaller, more frequent adjustments.
        - Requires FPS headroom above the target. If FPS rarely exceeds the target, TLOD increases may be slow or may not occur.
        - When an FPS cap is in place:
          - Target FPS must be set at least 5% below the cap, ensuring settings reduction has sufficient headroom to recover.
          - Setting Target FPS strictly higher than a detected cap triggers an orange mismatch warning (buffered by 0.5 FPS to prevent frame jitter false alarms).
        - If you are using a true fixed FPS (FPS holding the same value with minimal variation) or VSync for that purpose, use the Fixed option instead of Manual.
      - Auto
        - Automatically calculates a target FPS after initial FPS settling, when stationary on the ground or any time you are in the air.
        - Recalculates automatically if performance becomes too low for the current target, after arriving at a new destination, when changing graphics mode, pressing Reset, or toggling the option off/on for a quick recalibration.
        - Detects a fixed FPS condition (FPS stabilised with minimal variation). When detected, it sets the target FPS to that value, switches to Fixed FPS automation, and continues monitoring to ensure the classification remains valid.
        - Otherwise, with IFR or any Expert‑mode user profile, the target ranges from 95% of current average FPS on the ground to 85% at or above 3000 ft, providing headroom for Max TLOD.
        - With VFR, each percentage is reduced by 5% to better suit higher performance expectations.
        - Auto settings reduction and TLOD Base Extra and Free are mutually exclusive with Auto Target FPS, hence are not selectable. Fixed TLOD must be used.
      - Fixed
        - Designed for true locked framerates (e.g. driver locks, monitor refresh divisors, or Reflex caps). Maintains a fixed FPS responsively and enables the FreeTLOD option.
        - Set Target FPS to match your locked rate, or between 0% and 5% below a detected cap without warning to accommodate standard G-Sync, FreeSync, and Reflex frame-rate caps.
        - Setting Target FPS more than 5% below a detected lock advises switching to Manual mode or raising the target to match the cap.
        - Setting Target FPS higher than a detected cap triggers an orange mismatch warning with recommendations to resolve.
        - Gently raises TLOD when FPS is close to or at the Fixed Target FPS, and applies proportionally larger reductions when FPS drops, maintaining the fixed FPS with maximum possible TLOD.
        - Requires two consecutive raw FPS drops or a 5‑second downward trend before reducing TLOD, avoiding reactions to brief dips.
        - Recovers at twice the normal increase rate when below the last stable TLOD peak, improving responsiveness within previously safe ranges while avoiding overshoot above them.
        - Uses fixed timing guards (5 s since last decrease, 10 s since last increase) to minimise oscillation.
        - Halves maximum TLOD reduction in non‑cockpit camera states to account for larger transient FPS drops typical of these views.
        - Profiles that previously used FPS Cap are converted automatically to FPS Sensitivity with Fixed Target FPS for every graphics mode, with a message. The old one-off conversion prompt has been removed.
      - Saving the option per graphics mode
        - The Manual, Auto or Fixed option is saved for each graphics mode in each profile. Existing settings are migrated from the profile's previous Auto and Fixed settings, which are kept in step so older versions still work if you roll back.
        - The option dropdown applies to the mode shown in the Target FPS type dropdown. For a mode that is not active it is saved for later.
        - Changing graphics mode outside a flight, or Man FG in a flight, loads the option for the new mode, with a reset when in a flight. Resetting a profile to defaults sets every mode to Manual.
    - Target FPS graphics mode
      - A separate Target FPS setting exists for each graphics mode (NFR, FG, LSFG, MSM, VXR, Man FG, FSR, OFXR and VR) and each flight type (VFR, IFR, and six user profiles in Expert mode).
      - On startup, the app defaults to the graphics mode last used.
      - Automatically switches to the detected graphics mode when loading into a flight session (except in Man FG).
      - Users can adjust Target FPS for non‑active graphics modes using the drop‑down list.
      - The drop‑down background turns orange when the selected Target FPS type does not match the current graphics mode.
      - Changes to Target FPS for non‑active modes are saved immediately without reloading the UI.
      - Any other UI change reloads the Target FPS type for the current graphics mode.
      - Particularly useful for VR users, who previously had to enter VR and remove the headset to change this setting.
  - On Top
    - Allows the app to overlay your MSFS session if desired, with MSFS having the focus.
    - Mainly useful for adjusting settings and seeing the outcome over the top of your flight as it progresses.
    - Should also satisfy single monitor users utilising the FG capability of MSFS as they now see the true FG FPS the app is reading when MSFS has the focus.
  - Dark Mode
    - Allows toggling between the default Light theme and a native Dark theme.
  - Reset button
    - Resets TLOD, Clouds, Auto Target FPS and graphics mode detection to initial state.
    - In Expert Mode (outside a flight session), restores the active flight profile to defaults following user confirmation.
    - Useful to reinitialise and recommence the seek process for TLOD Extra should conditions change significantly from what they were on initial start-up.
    - Can be activated by pressing ALT-R while the app has the focus, making it suitable to be assigned as a VR-friendly voice command with an app like VoiceAttack.
  - Flight type - VFR or IFR (any mode), six additional user profiles (Expert mode), and Monitor only (any mode)
    - Monitor only (any mode):
      - The last entry in the Flight Type list. AutoFPS stops changing any sim settings and only monitors (the status line shows Monitor Mode). On selecting it, the sim is first returned to your own default TLOD, OLOD, cloud quality and key settings.
      - It is a global state remembered between sessions until another flight type is chosen, which reloads that profile and does a full reset. It is not a profile and cannot be renamed, and the active profile and its settings are left untouched underneath.
      - VR, sim rate and Night profile auto-switching are suspended while in Monitor only.
      - The Expert panel is hidden and the General panel becomes a single line: On top, Dark, Log+ and Flight Type. Target FPS controls and the Expert checkbox are hidden. Reset is hidden too, except when no sim is running, where it still switches the MSFS 2020 / 2024 view.
      - Log+ is forced on and locked (ticked and greyed out) while in Monitor only, and your saved Log+ setting is restored when you leave.
      - Changes to TLOD, OLOD (PC and VR) and cloud quality made in the MSFS options update the stored defaults and the readings in the app, widget and web interface immediately once saved, in or out of a flight session. Nothing is logged and nothing is restored or reapplied.
      - Clouds show green while they match the sim default (white if they ever differ), as TLOD and OLOD do. The Reduce readout is not shown.
      - The previous `MonitorMode` config file method of enabling it has been removed.
    - Non-Expert mode:
      - VFR will use higher minimum and maximum TLODs and a lower TLOD base altitude than IFR.
      - Accounts for the greater performance expectation that GA flights in rural areas will have.
    - Expert mode:
      - Extends Non-Expert mode IFR and VFR profiles by adding 6 user profiles by default.
        - Can be altered to provide between 4 and 12 total profiles by editing the ProfileCount key in the common config file.
      - Defaults to similar settings differences as Non-Expert mode for VFR and IFR.
      - User profiles are initially based on the IFR profile.
      - Settings are fully customisable and saved to/restored from the respective profile.
      - Profiles can be reset to default by clicking the Reset button after MSFS has been detected while not in a flight session.
      - Profile names can be edited, including VFR and IFR:
        - Double clicking on the profile combo box text area toggles edit-ability, with non-editable being the initial state on startup.
        - Press Enter, press Tab or click on another control on the app UI for the changed text to be accepted.
      - VR/Non-VR pairing for automatic flight type profile switching:
        - Activates when entering or exiting VR based on matching profile names with and without " VR" suffix.
        - A "↔", and a pair count if more than one pair, will be appended to VR/Non-VR paired profile names in the profile selection dropdown to indicate auto-switching pairing(s).
        - A warning message will show when an edited profile name matches a VR/non-VR counterpart, indicating it will auto-switch between them; prompts user to rename if unintended.
      - Sim Rate pairing for automatic flight type profile switching:
        - Activates when the sim rate changes based on matching profile names with " xX" suffixes, where x is the new sim rate. eg. IFR 1X and IFR 4X.
      - Day/Night pairing for automatic flight type profile switching (Expert):
        - Name two profiles the same, with one ending in " Night" (e.g. IFR and IFR Night). AutoFPS uses the Night profile at night and the other profile during the day, checked every tick in a flight, including at flight start, after a manual profile change and after a reset.
        - Outside a flight either profile can be selected to edit its settings. In a flight, selecting the inactive profile of a pair switches straight back, so exit the flight first or temporarily rename the Night profile (e.g. IFR Nite) to edit it.
        - A profile can have only one auto-switch partner: if either profile is also part of a VR pair, the VR pair takes priority and there is no Night pairing.
        - Night mode is not used on the day profile of a pair (shown greyed out), and the Night profile can only use its cloud reduction (Clds), as TLOD halving is not needed on a dedicated night profile. Saved Night mode settings are kept and return if the profiles are unpaired.
        - The switch carries current settings over to the new profile (no reset) when both profiles use the same auto method and Manual / Auto / Fixed option, and either it is turning to night, no settings reduction is active, or the new profile can recover any active reductions itself. Otherwise a full reset is done, as for VR and sim rate profile switching.
        - Paired profiles show the ↔ marker in the Flight Type list, renaming a profile into a pair shows an auto-switch warning, and the status line and log show Day or Night. The switch follows the same hold as day/night settings switching.
      - Matching of VR, Night and sim rate profile names ignores capitalisation and unusual spacing (e.g. IFR night, IFR vr, IFR 2x and IFR  Night all pair).
      - Delayed base TLOD reduction until landing gear-down:
        - Enabled when "DelayLG" appears anywhere in a flight profile name.
        - Activation requires the aircraft to first ascend above Alt TLOD Base.
        - Once active, a lock symbol 🔒 appears next to the TLOD range in the app status line.
        - TLOD reduction below Max/Top is then delayed until gear-down, at which point TLOD drops to Min/Base as usual.
        - If gear is lowered late, this may trigger a one-off, large TLOD drop, potentially causing a stutter as the graphics engine unloads.
        - TLOD Base Extra will still reduce below Alt TLOD Base as normal, but since this is additive to Top Min/Max, the reduction will be much smaller than usual.
    - Command line argument support for flight type profiles is as follows:
      - -ifr and -vfr continue launching the app with the default IFR and VFR profiles, even if renamed in Expert mode.
      - -user1 to -user6 now open their corresponding user profiles, restricted to Expert mode.
      - -profile "`<profile name>`" loads the specified profile in Expert mode, requiring an exact match.
  - Use Expert Options (shortened to **Expert**)
    - Non-Expert Mode (unchecked and default)
      - Allows the app to use default settings in conjunction with your chosen target FPS that should produce good automated FPS tracking, provided you have set reasonable MSFS TLOD, OLOD and Cloud settings and a realistic FPS target within your system's performance capability. Built-in guardrails cap daytime TLOD at 400, night TLOD at 200, and ground OLOD at 200.
      - The following settings will be used:
        - Target FPS option, graphics mode and value - user selectable. Enabling Auto automatically disables Extra due to automation control ambiguity
        - VFR or IFR flight type - user selectable
        - TLOD Automation Method - FPS Sensitivity
        - FPS Sensitivity - 5%
        - TLOD Base Min - VFR 50% of your current MSFS TLOD setting, IFR 25%, doubled if using Auto Target FPS (clamped to a maximum of 150)
        - TLOD Base Extra - enabled with an x4 multiplier, unless Auto Target FPS is enabled, then disabled
        - Alt TLOD Base - VFR 100 ft, IFR 1000 ft
        - TLOD Top Max - VFR 250% of your current MSFS TLOD setting (capped at 400), IFR 200% (capped at 400)
        - Alt TLOD Top - internally calculated to achieve a maximum recommended descent rate of 1350 fpm for VFR and 2000 fpm for IFR.
        - Mtns - disabled
        - Night Mode - TLOD÷2 (capped at 200)
        - Auto OLOD - enabled and VFR 150% of your current MSFS OLOD setting (capped at 200), IFR 100% 
        - VRAM+ - enabled,
          - Only functional if GPU-Z is running.
          - Imposes different auto settings reduction settings, namely Floor: off and Recovery: Ground with 5% VRAM reduction achieved. 
          - Can be disabled by temporarily checking Expert Options, selecting Off in the VRAM+ dropdown control, then unchecking Expert Options.
        - MSFS 2020 only 
          - Decrease Cloud Quality
            - enabled by default and uses the GPU load activation method if GPU-Z is found to be running, otherwise the TLOD activation method is used.
            - can be disabled by setting DecCloudQNonExpert to false in the app config file located in the app's root, NOT bin, directory.
            - GPU load activation method decreases cloud quality with greater than 98% GPU load and recovers with less than 80% GPU load.
            - TLOD activation method uses a Cloud Recovery TLOD 2/5 between TLOD Base Min and TLOD Top Max or + 50 over TLOD Base Min, whichever is lower. If excessive changing of cloud quality levels are detected, the app will automatically increase its calculated cloud recovery TLOD.
        - MSFS 2024 only 
          - Auto Settings Reduction - enabled with Max Levels: 2, Floor: Lowest, and Recovery: Ground. Reduction Settings Suite:
            - IFR: Full reduction suite minus clouds (to minimise impact on user experience). OLOD may reduce by up to 50%, but TLOD will not reduce below TLOD Base Min when not using Auto Target FPS, as it is already very low for this flight‑type profile.
            - VFR: Flora (Trees, Plants, Grass), Ray Traced and Terrain Shadows and Displacement Mapping 
          - Auto Increase Clouds - enabled
    - Expert Mode (checked)
      - The UI expands to show additional MSFS settings to adjust.
      - If you do not understand these settings and their impact on MSFS performance and graphics quality, it is strongly recommended that you do not use these expert options and you should uncheck this option.

</details>

<details>
<summary><strong>Expert settings in full (automation methods, TLOD, Night Mode, Auto OLOD, cloud quality, settings reduction, VRAM+)</strong></summary>

- Expert Settings
  - Auto Method – FPS Sensitivity generally gives the best results for most users, including when using a true fixed FPS, and hence is the default. Use Auto TLOD if you want a DynamicLOD‑like experience. FPS Tolerance and FPS Cap were retired in 0.5.5.1 (see below).
    - FPS Sensitivity - smaller changes more often.
      - Determines how sensitive the app will be to the variance between your current and target FPS.
      - Also determines the largest TLOD step size you will see, being double the FPS sensitivity number.
      - The lower the setting the smaller the changes will be, which is useful if you are experiencing stuttering with the default value of 5. Vice versa for higher settings. (1 - 20 allowable)
    - Auto TLOD – operates like Auto OLOD by using an altitude‑based schedule, and is the mode most closely aligned with DynamicLOD_ResetEdition behaviour.
      - TLOD will adjust based on an altitude band with a base and top level and with TLOD values defined for each of these altitudes.
      - The app will set TLOD Base at or below the Alt TLOD Base (AGL), set the TLOD Top at or above Alt TLOD Top (AGL) and interpolate in between.
      - The nominal LOD Step Size can be set to allow users experiencing stuttering issues to try different LOD step sizes to help resolve the issue. The default value is 5. (1 - 20 allowable)
      - Alt TLOD Top in this mode was reduced from 1000 to 100 ft in 0.5.5.1, as 1000 was unnecessarily restrictive.
      - This method completely ignores FPS hence all FPS-related settings are removed from the UI.
    - FPS Tolerance and FPS Cap (retired in 0.5.5.1)
      - The Auto Method dropdown lists only FPS Sensitivity and Auto TLOD.
      - Profiles on FPS Tolerance are switched to FPS Sensitivity automatically, with a message. Profiles on FPS Cap are converted automatically to FPS Sensitivity, also setting Fixed Target FPS for every graphics mode of the profile, with a message.
      - The FPS Cap TLOD Extra seek process, and the `TLOD+ Seek` and `FPSCap` status text, no longer exist. For locked frame rates use FPS Sensitivity with Fixed Target FPS.
  - Periodic Spike Detection/Protection:
    - Requires RTSS to be running and configured to monitor frame time data. 
    - Detection is auto enabled in Expert mode and disabled in Non-Expert mode.
    - Detects periodic MSFS frame time spikes (4+ fresh spikes of same 0.3-1.8s cadence), which can occur with high TLOD and photogrammetry conditions and can cause stutters.
    - Protection checkbox, enabled by default, is shown only in Expert mode and when RTSS is detected as running; otherwise the controls are removed entirely.
    - Automatic TLOD reduction when protection is enabled and periodic frame time spikes are detected, helping reduce stutters in affected areas.
    - Automatic TLOD restoration when periodic frame time spikes are no longer detected, using a time‑delayed, continuously‑scaled recovery model driven by distance travelled, altitude above ground, and vertical trend for smooth, scenery‑aware restoration without abrupt jumps.
    - Lightning symbols show with the FPS reading and TLOD range in the status line when periodic frame time spikes are detected and when resultant TLOD reduction is active respectively.
    - A spike-protection UI panel (multiplier textbox, frame-time record button, and spike status) will show when both Spike Protection and Log+ are enabled.
    - A "SpikeThresholdMultiplier" common config file option, defaulting to 2.0, determines the multiple of the frame time median at which a spike will be counted.
    - Log entries show spike detection and protection settings, spike TLOD reduction amount when greater than zero, and when associated TLOD reduction and recovery commence.
    - Additional diagnostic logging occurs when Log+ is enabled.
  - TLOD Base Min - Sets the minimum TLOD the automation algorithm will use at or below the Base altitude. (Range: 10 - TLOD Max-10)
  - Alt TLOD Base – Altitude (AGL) at or below which TLOD will be at TLOD Base Min. (Range: 100 ft to 100,000 ft; N/A in Free TLOD mode)
  - TLOD Base Extra option in FPS Sensitivity mode in Manual or Fixed Target FPS:
    - Fixed:
      - No TLOD Base Extra is used.
      - Forced for Auto Target FPS due to mutual exclusivity with this mode.
    - Extra:
      - Allows TLOD Extra below Alt TLOD Top under favourable performance conditions.
      - Multiplier configurable between 1.5 and 4 (default 2); auto‑limited so TLOD Base Min + Base Extra ≤ TLOD Top Max.
      - Multiplier tooltip shows TLOD Min including Extra.
      - On the ground at ≤ 5 knots, Base Extra increases progressively (larger initial steps) until a higher TLOD Base Min is reached, up to the maximum allowable.
      - During critical flight phases (ground speed above 20 knots, above 5 knots while accelerating, or below Alt TLOD Base in the air), Base Extra is limited to reductions only to minimise stutters.
      - Avoid rapid view changes or fast panning, especially while scenery loads, as this may trigger unnecessary reductions.
      - If VRAM+ limiting is active, Base Extra seeking is cancelled and may fully reset if severe.
    - Free:
      - As per Extra but free‑ranging, allowing TLOD to vary between TLOD Base Min and TLOD Top Max at any altitude and any time based on performance.
      - May cause stutters around complex scenery or aircraft, especially near complex airports; recommended only for VFR and non‑complex scenarios.
  - Start Max option for TLOD Extra
    - Available for all automation modes supporting TLOD Extra, defaulting to disabled.
    - When enabled, starts with maximum TLOD Extra instead of beginning at minimum and ramping upward.
    - Replaces the initial 30-second FPS timer with a normal settle event, reducing startup settle time at the cost of potentially incomplete initial stabilisation.
    - Due to the aggressive nature of this feature, initial performance may be sub-optimal and the final achievable TLOD Extra may be lower than when the feature is disabled.
    - Start Max is skipped when VRAM at session start is above the VRAM+ recovery threshold (VRAM+ users with GPU-Z running), so the profile starts at Base Min TLOD with the normal FPS settle. A log line records it, and there is no change when VRAM+ is off or no GPU-Z reading is available.
    - Start Max is cancelled automatically if VRAM reaches the VRAM+ reduce threshold while the FPS settle after it is still running. The Start Max extra is cleared and TLOD is dropped straight to Base Min, with a log line. VRAM is now monitored throughout the settle.
  - TLOD Top
    - FPS Sensitivity mode:
      - TLOD Top Max – Maximum TLOD the automation algorithm may use. (Range: TLOD Base Min+10 to 1000)
      - Alt TLOD Top – Altitude (AGL) at or above which TLOD may increase up to TLOD Top Max. (Range: Alt TLOD Base+1 to 100,000 ft; N/A in Free TLOD mode)
    - Auto TLOD mode:
      - TLOD Top Max – Fixed maximum TLOD the automation algorithm will use. (Range: TLOD Base Min+10 to 1000)
      - Alt TLOD Top – Altitude (AGL) at or above which TLOD will be fixed at TLOD Top Max. (Range: Alt TLOD Base+1 to 100,000 ft)
    - All modes, except when Free TLOD is enabled:
      - Default Alt TLOD Top values are lower in FPS Sensitivity mode to account for free‑ranging TLOD Max.
        - IFR: Base 1000 ft, Top (Sensitivity) 2000 ft, Top (Auto TLOD) 3333 ft – aligns with recommended max 2000 fpm descent rate.
        - VFR: Base 100 ft, Top (Sensitivity) 1000 ft, Top (Auto TLOD) 2350 ft – aligns with recommended max 1350 fpm descent rate.
      - TLOD Top Min/Max Tooltip – Displays recommended maximum average descent rate for current settings.
      - Alt TLOD Top Tooltip – Displays altitude to set for recommended IFR/VFR descent rates.
        - Profiles with "VFR" in their name (case‑insensitive) automatically show VFR info.
        - Default fpm constants are stored in the associated MSFS version config file for easier adjustment.
  - Mtns - additional TLOD Max in high elevation areas for all modes.
    - When enabled, extends TLOD Max in areas where the terrain is higher than Mtn Alt Min (Range: 100 ft - 100,000 ft) by the TLOD Mtn Amt amount (Range: 10 - 1000), progressively increasing by the TLOD step size per second until completely activated.
    - When Free TLOD mode is active in FPS Sensitivity mode, Mountains+ applies at all altitudes, including on the ground, allowing TLOD to reach up to TLOD Top Max + Mountains Extra everywhere based on performance.
    - If terrain drops below Mtn Alt Min, Mtns will remain fixed for 5 minutes then progressively reduce by the TLOD step size per second until completely deactivated.
  - Night Mode - per-profile night-time performance optimisation (see the Night Mode entry in the Expert Mode Reference Guide for the Day/Night switch level, hold and cloud reduction settings)
    - Per-profile dropdown with four modes: Off, TLOD÷2, Clds-X, and Both.
    - Decouples night-time cloud reduction from TLOD halving, allowing users to reduce cloud overhead while preserving full draw distance for city lights and runways.
    - Steps down volumetric cloud quality by the Night cloud reduction levels (never below Low) at night and cleanly restores daytime settings at sunrise.
    - Automatically clears any active daytime cloud boost (IncCloudQ) upon entering night mode and restores it at sunrise.
    - Works with all automation methods: FPS Sensitivity and Auto TLOD.
    - Settings adjust progressively when transitioning between day and night.
    - The status line displays Day, Night, or Δ while transitioning between them.
    - Fixed in 0.5.5.1: night cloud reduction not reapplying after a night, day, night sequence (Increase Cloud Quality could run before the night restore and block the next reduction), and settings reduction recovery raising clouds one level above the night level after a reduction cycle at night.
    - Night and day transitions no longer compound TLOD Min, Mountains and spike reductions during the transition, and the mid altitude reset TLOD uses the halved Max TLOD only at night.
  - Auto OLOD
    - When enabled, four user definable parameters relating to this feature will be revealed on the UI.
    - Rather than the automation being FPS based, which would cause contention with TLOD changes at the same time, OLOD will adjust based on an altitude band with a base (1000ft minimum and less than top) and top level (2000ft minimum, 100000ft maximum and greater than base) and with OLOD values defined for each of these altitudes (10 - 1000 allowable).
    - The app will set OLOD @ Base at or below the Alt OLOD Base (AGL), set the OLOD @ Top at or above Alt OLOD Top (AGL) and interpolate in between. Note that OLOD @ Base can be higher, lower or the same value as the OLOD @ Top, depending on whether you want OLOD to decrease, increase or stay the same respectively as you ascend.
    - Integrates with auto settings reduction (MSFS 2024 only) and VRAM+.
  - Hide OLOD settings - allows the user to collapse the Auto OLOD settings panel when not needed, to make the app window more compact.
  - MSFS 2020 only
    - Decrease Cloud Quality - When enabled, will reduce/restore cloud quality by one level if the activation condition is met.
      - Activation Methods
        - TLOD is the original method and is most suitable for systems where TLOD has the largest impact on desired MSFS performance.
        - GPU Load is the new method that allows cloud quality changes to occur independently of TLOD
          - Most suitable for systems where cloud quality has a similar or larger impact on desired MSFS performance than TLOD does.
          - Reduction occurs only when FPS falls below target by at least the margin specified by the current TLOD automation method.
        - All flight type profiles will use the same cloud reduction method.
      - TLOD (FPS Sensitivity TLOD Automation Method)
        - Decreases when TLOD has already auto reduced to TLOD Base Min and FPS is still below target FPS by more than the FPS tolerance.
        - Cloud Recovery TLOD with optional + (resultant TLOD must fall within TLOD Base Min+5 and TLOD Max-5)
          - The TLOD level required to cancel an active cloud quality reduction state and restore cloud quality back to its initial higher quality level.
          - Ideally set to 50 TLOD or more above TLOD Base Min to provide a TLOD buffer to minimise the chance that cloud quality will constantly change down and up.
          - When + is checked, Cloud Recovery TLOD becomes relative to TLOD Base Min instead of absolute.
      - GPU Load (All TLOD Automation Methods)
        - Requires the [GPU-Z](https://www.techpowerup.com/download/techpowerup-gpu-z/) companion app to be installed and running for this method to work. If GPU-Z is not running, the user will be alerted to start it on the app status line in the General section.
        - Decreases when the GPU load, as measured by the GPU-Z companion app, is higher than the user-defined Decrease GPU Load percentage. (50% - 100% allowable)
        - Cloud Recovery GPU load (5% - 90% and at least 10% less than Decrease GPU Load allowable)
          - Recovers when the GPU load is lower than the user-defined Recover GPU Load percentage.
          - Ideally set to at least 15% lower than the Decrease GPU Load percentage to provide a GPU load buffer to minimise the chance that cloud quality will constantly change down and up.
  - MSFS 2024 only
    - Automatic Settings Reduction
      - Enabled for Non-Expert mode and disabled by default for Expert mode. 
      - Activated under marginal performance conditions to help improve FPS and reduce VRAM usage.
      - Only applicable to MSFS 2024, as existing MSFS 2020 functionality is considered acceptable and MSFS 2024 tends to experience VRAM overflow, where such setting reductions are intended to help alleviate, much more frequently.
      - Not applicable to AutoTLOD mode as it has no TLOD or FPS recovery mechanisms. 
      - Default settings will be saved on flight session commencement and restored on completion.
      - Settings reduction will only function and show when in a flight session and the secondary compatibility test passed.
      - Automatically reduces settings if the current FPS falls below the target FPS and TLOD is already at a minimum.
      - Activation and recovery are automatically paused during critical flight phases (takeoff, initial climb, final approach, landing).
      - When enabled the default values of the applicable settings are the same as for Non-Expert except Reduction Settings Suite: LODs, Clouds, Trees, and RT Shadows (which covers the settings most likely to improve FPS when they are reduced).
        - OLOD reductions are proportional to TLOD reductions.
        - AutoFPS-controlled LOD reductions are replaced with MSFS dynamic settings control when enabled. Resulting reductions are internal to MSFS and not reflected in external LOD values.
        - Settings reduction activation cancels the TLOD Extra seek process if active.
      - Automatically restores settings if the current FPS rises above the target FPS by the default tolerance or if the current FPS matches the target FPS and the TLOD has automatically increased by an acceptable margin.
        - Settings recovery will commence recovery immediately in any mode if the current FPS is above the target FPS by the FPS tolerance amount.
        - Displacement Maps will not restore to enabled until above 100 ft AGL, as it is known to cause texture corruption at low altitudes.
      - TLOD Top Max and OLOD will be progressively reduced at the user-defined LOD step rate by up to 50%.
      - Max Levels setting determines how many levels the settings other than LODs will be reduced by in subsequent reduction cycles. 
      - Floor setting determines the minimum setting quality level that settings will be reduced to, including off if an allowable state for a setting.
      - Recover setting determines at which altitude (ground, Alt TLOD Base or Alt TLOD Top / equivalent) settings reduction recovery can occur as enabling it at lower altitudes may not be acceptable for some users. The default is Alt TLOD Base, being the middle option.
      - Reduction Settings Suite setting determines which settings set will be reduced. 
        - The presets are based on research that identified the settings most likely to improve FPS and/or reduce VRAM consumption when they are reduced, in decreasing order of effectiveness.
        - Custom Reduction Settings Suite allow the user to select which individual settings to reduce rather than the preset sets, the individual settings for which will appear when the user mouses over the reduction settings suite label or drop down list and Custom is currently selected. 
      - The Reset button will restore all settings to their default values.
    - VRAM+: see *VRAM+ in full* above.
    - Auto Increase Clouds
      - Auto increase cloud quality option with TLOD Base Extra enabled.
      - Increases cloud quality by one level if not already at ultra and sufficient TLOD or FPS performance margin exists at the conclusion of the seek process.
        - Enhanced activation/deactivation based on GPU load is available when the companion app GPU-Z is running.
      - Removed when performance conditions have degraded sufficiently since activation.

</details>

<details>
<summary><strong>Log+ mode and performance logging</strong></summary>

- Log+ Mode - Advanced testing and logging features:
  - Additional logging and features are available when the **Log+** option is checked in Expert mode.
  - Detailed FPS logging:
    - Activated by clicking the FPS value during flight sessions.
    - Default log period is 30 seconds, changeable with the **logFPSDetailsCountMax** key in MSFS_AutoFPS.config in the app root directory.
    - Logs the raw FPS and average FPS to one decimal place every second.
    - FPS value is displayed in two alternating purple shades when logging FPS details.
    - Clicking the FPS display while logging is active will cancel the current logging event.
  - Camera state logging:
    - Logs all MSFS camera state changes to help diagnose issues related to camera state changes.
  - Performance monitoring and logging:
    - Shown as an extra line on the app status line during flight sessions.
      - Total CPU – overall system CPU usage across all logical processors.
      - Dominant Core – instantaneous load of the core selected by 10‑second averaging.
      - TopXAvg – instantaneous load of the busiest X (configurable) cores.
      - MSFS CPU – MSFS CPU usage based on the applied affinity mask, showing core count when set.
    - Extra performance data logging to provide clearer context when performance limits are hit.
      - Logs performance data every second during flight when enabled (logs grow rapidly).
      - Ignores the first performance data sample, which is often an outlier due to startup conditions.
      - Uses EWMA smoothing for Dominant, Max and MSFS CPU metrics, logging both raw and smoothed values to stabilise scheduler jitter and show clearer CPU‑pressure trends.
      - Logged fields: FPS, ΔFPS, TLOD, AGL, Total CPU, Dominant Core, MSFS CPU, TopXAvg, GPU and VRAM (when detected). Where two values are logged, the first is raw and the second is EWMA‑smoothed.
      - Configurable parameters in the common config file:
        - perfLogTopCoreCount – X in TopXAvg, default 2.
        - PerfLogBufferSize – number of pre‑breach and post‑recovery samples, default 15.
        - PerfLoggingAlways – always enabled, regardless of what it is set to in the config file.
        - EwmaAlpha – EWMA smoothing coefficient, default 0.1.
        - MonitorMode – no longer enables monitoring. The config key now only stores the Monitor only Flight Type selection (see Flight type - Monitor only).
- Virtual screen coordinates and window position logging on app startup.
  - Detailed settings initialisation, reduction and recovery event logging, to aid troubleshooting and performance monitoring.
  - Verbose compatibility test results in log file.

</details>

<details>
<summary><strong>General operation and app window notes</strong></summary>

- General
  - Can be started anytime, but preferably just after MSFS has loaded in to the main menu to minimise sudden MSFS settings changes when the app is initialising. The app will exit itself when MSFS closes.
  - If you wish to have the app exit at the conclusion of a flight session, change 'ExitAppAfterFlightSession' in the common config file to true.
  - With the default install option, the app's icon more intuitively resides on the task bar when the app is running, not in the system tray overflow where it has been located in previous versions. Exit by clicking on the app window's close button and providing user confirmation when prompted.
  - If installed with the close app to system tray option, the app will remain running in the system tray until the user right clicks and selects Exit or the app auto exits when MSFS closes.
  - Running as Admin NOT usually required (BUT: It is required to be run under the same User/Elevation as MSFS). 'Admin' will be shown on the app title bar when the app is running as Admin.
  - Do NOT change MSFS graphics settings manually while in a flight with this app running as it will conflict with what the app is managing and they will automatically restore to what they originally were when you exit your flight. If you wish to change the defaults for these MSFS settings, you must do so either without this app running or, if it is, only while you are in the MSFS main menu (ie not in a flight).
- App Window
  - Position and minimised/maximised state will be remembered between sessions, except movements to it made while in VR due to window restoration issues.
    - Manually permanently disable this feature in the config file by setting the RememberWindowPos line to be false.
  - Will automatically reset to default position (50,50) if the app is restarted within 15 seconds of last closing, except if disabled by setting the AllowWindowPosReset key to false in the common config file.
  - **Window Title Bar Hiding:** The title bar can be hidden or restored by double-clicking the title bar, or via the right-click context menu (**Hide Title Bar**). When hidden, an app title indicator is shown.
  - **Whole-Window Dragging:** You can left-click and drag the window from anywhere on its body or panels, not just the title bar.
  - **Streamlined Close Confirmation:** The *"Are you sure you want to close?"* confirmation prompt only appears when physically clicking the window close `[X]` button (when not in system tray). Deliberate multi-step exits—such as right-clicking the Taskbar icon and selecting *Close window*, pressing `Alt + F4`, or using the context menu—exit immediately without prompting.
  - **Right-Click Context Menu:** Right-clicking anywhere on the window provides quick access to:
    - **Hide/Show Title Bar:** Toggles the window title bar.
    - **Minimise:** Minimises the app window to the taskbar.
    - **Close to Tray:** Sends the app window to the system tray. Only shown if the "Close to Tray" option was chosen during installation.
    - **Exit:** Exits the application immediately without requiring confirmation dialogs.
  - **UI Layout & Sizing:**
    - The user can progressively hide parts of the UI when double-clicking the title bar area. The first double click hides the Expert settings section (if applicable), the second hides the general settings section, and a third double click restores all hidden settings sections.
    - The window dynamically scales its dimensions to accommodate Windows text scaling and custom DPI settings without clipping group box borders or overlapping panels.

</details>

<details>
<summary><strong>Installation, update and uninstall notes in full</strong></summary>

Basically: Just run the Installer to either install, update or uninstall the app.

Some Notes:
- Install Options:
  - Desktop Icon: Create a desktop icon for the app.
  - MSFS Widget: Deploys the AutoFPS In-Game Toolbar Widget to the Community folder of any installed MSFS versions.
  - Close to Tray: App Closes to the System Tray.
  - Reset Configs: Resets the app Common, MSFS 2020 and MSFS 2024 config files. When upgrading from 0.5.5.0 or earlier, the config files are first backed up to a `ConfigBackup_<version>` folder (an existing backup folder is left alone, and a backup failure is logged and does not stop the installation).
  - Repair: Reinstalls core files, including latest Visual C++ Redistributable and .NET 8 runtime versions, MobiFlight, and In-Game Widget (if enabled), keeping your app config intact.
  - Auto Start Options: Remove, Retain, FSUIPC (via FSUIPC.ini) or MSFS (via EXE.xml).
  - Custom Community Folders: Allows choosing custom Community folder locations for MSFS 2020 and MSFS 2024 via the `2020 Folder...` and `2024 Folder...` buttons. Ideal for Addons Linker or custom drive configurations. Right-click on either button to quickly reset back to auto-detected simulator defaults.
- Custom Community Folders & Addons Linker:
  - The installer automatically detects symbolic links and directory junctions in the default Community folder, only deleting physical folder leftovers when custom paths are selected, ensuring Addons Linker links are preserved.
  - The installer automatically records and remembers custom Community paths across all future auto-updates in `Install.log`.
- Smart Deferred Auto-Updates:
  - When an update contains an in-game widget change and MSFS is currently running, AutoFPS automatically defers the installation until MSFS has closed, avoiding flight interruptions and file-lock errors.
  - A status line message on the main app window indicates when an update is deferred and waiting for MSFS to exit.
  - If an exit is initiated while an update is waiting (via the system tray menu, window title bar, or post-flight auto-close), a confirmation dialog gives you the choice:
    - Click 'No' to keep AutoFPS running so the update installs automatically when MSFS exits. If post-flight auto-close is enabled, it is suspended for that session so you are not repeatedly prompted.
    - Click 'Yes' to close AutoFPS immediately and postpone the update until the next launch with MSFS closed.
  - Minimizing to the system tray is unaffected and leaves the update queued for when MSFS exits.
  - Updates that do not contain widget changes continue to install immediately while MSFS is running.
- If the installer did not download or is zero size, try redownloading again with a different browser and possibly with AV temporarily disabled if required. It should be about 3 MB in size. 
- If the installer will not run at all:
  - Windows SmartScreen is potentially blocking it because the app is so new. The solution to try is:
    - Right-click on the Installer and select properties
    - Check the option "Unblock"
    - Click on Apply and Ok to save the change
    - Then try to install it again
  - Try creating an exception for the installer in your AV program, or just disabled the AV program temporarily while running the installer.
  - If all else fails, the app can be manually installed as follows:
    - Download the [Mobiflight Module](https://github.com/MobiFlight/MobiFlight-WASM-Module/releases) and extract it to your Community folder (without MSFS running).
    - Install [this](https://aka.ms/vs/17/release/vc_redist.x86.exe) and [this](https://aka.ms/vs/17/release/vc_redist.x64.exe) and go to the Microsoft .NET 8.0 download page [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) and download and install the latest .NET Desktop Runtime X64 version.
    - Create an %appdata%\MSFS_AutoFPS folder, then a sub folder called bin underneath that.
    - Download [MSFS_AutoFPS.zip](https://github.com/ResetXPDR/MSFS_AutoFPS/blob/main/MSFS_AutoFPS.zip) and extract to the newly-created bin folder.
    - Create a shortcut to MSFS_AutoFPS.exe and move it to a place of your choice eg. desktop.
    - AutoFPS should now work when you click on the shortcut.
- MSFS_AutoFPS and/or MSFS2020_AutoFPS must not be running before installing/updating/upgrading.
- Do not run the Installer as Admin unless it will not install due to a permissions issue. If the installer is run as Admin, an advisory message will be shown explaining inherited elevation.
- There is no need to uninstall MSFS2020_AutoFPS before upgrading to MSFS_AutoFPS. The installer uninstalls MSFS2020_AutoFPS if currently installed but preserves its MSFS 2020 config for use in this new app beforehand if desired or applicable.
- If you have previously removed MSFS2020_FPS without using the installer to remove it properly, you may experience issues installing MSFS_AutoFPS. If this happens, do the following:
  - Run the installer for the MSFS_AutoFPS and select remove to uninstall it completely.
  - Download the installer for MSFS2020_AutoFPS 0.4.3.1 [here](https://github.com/ResetXPDR/MSFS2020_AutoFPS/releases/download/v0.4.3.1/MSFS_AutoFPS-installer-v0.4.3.1.exe) and reinstall it with no autostart options.
  - Rerun the installer for MSFS_AutoFPS and reinstall with whatever autostart option you desire. 
- Mobiflight Module:
  - If the installer can't locate your Community folder to install this module, perhaps because of a Custom MSFS install location, use the `2020 Folder...` or `2024 Folder...` button in the installer or manually extract to your Community folder.
  - If you see an ‘Unable to locate Community folder’ message after uninstalling MSFS, it’s caused by leftover MSFS userdata that the official uninstaller doesn’t remove.
    - AutoFPS detects MSFS by checking for the remaining UserCfg.opt file, so a partial uninstall can look like an active install—especially if a custom Community folder has been deleted.
    - Removing the leftover MSFS userdata resolves the issue, and future silent updates will suppress this message while still logging it internally.
  - If the MobiFlight Module is not installed or outdated, MSFS also has to be stopped.
  - If you have duplicate MobiFlight Modules installed, in either your official or community folders, the app may display 0 value Sim Values and otherwise not function. Remove the duplicate versions, rerun the app installer and it should now work.
  - If the installer fails when checking/updating Mobiflight, despite the latest version being correctly installed in your MSFS Community folder, create a shortcut for the installer, add the command line option "-bypassmobiflight" to the target text box, then run the shortcut to be able to bypass this installation step.
- The app will automatically check for updates on startup and will notify you accordingly on the app status line.
  - If you wish to only be notified of mandatory updates, select the Mandatory Updates options.
  - In test versions, notification of all app updates will be enabled as a minimum by default, regardless of what you have previously chosen for update notification, in order to encourage maintenance of a current test baseline.
  - Auto-update verifies the downloaded installer's SHA-256 against the digest published by GitHub before launching it, re-downloading on a mismatch and refusing the update if no digest is available.
- If you wish to retain your settings for an update version, do NOT uninstall first, as that deletes all app files, including the config file. Just run the installer, select update and your settings will be retained.
- The app may be blocked by Windows Security or your AV-Scanner, if so try to unblock or set an exception (for the whole Folder).
- The Installation-Location is fixed to %appdata%\MSFS_AutoFPS (your Users AppData\Roaming Folder) and can't be changed.
  - Binary in %appdata%\MSFS_AutoFPS\bin
  - Logs in %appdata%\MSFS_AutoFPS\log
  - Installation log in %appdata%\MSFS_AutoFPS\Install.log
  - Config: %appdata%\MSFS_AutoFPS\MSFS_AutoFPS.config (common), MSFS2020_AutoFPS.config and MSFS2024_AutoFPS.config
- If after installing and running the app your simconnect always stays red, your TLOD and OLOD values show as zero or you see "Critical Exception occurred: MSFS_AutoFPS - Unable to load DLL 'GpuzShMem.x64.dll' or one of its dependencies" in the log file:
  - Try reinstalling the app with the Repair option selected, then reboot. If any of the redistributables fail to install during this process, try downloading and installing/repairing (as applicable):
    - A Microsoft official version of “Microsoft Visual C++ 2015 - 2022 Redistributable”, which may be missing from your Windows installation. Try installing [this](https://aka.ms/vs/17/release/vc_redist.x86.exe) and [this](https://aka.ms/vs/17/release/vc_redist.x64.exe).
    - The NET desktop runtime from [here](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.15/windowsdesktop-runtime-8.0.15-win-x64.exe) if still available. Alternatively, go to the Microsoft .NET 8.0 download page [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) and download and install the latest .NET Desktop Runtime X64 version.
  - If still not resolved and the log shows a SimConnect / WASM exception such as Exception 31 or Exception 25, you most likely have a corrupt MSFS WASM installation.
    - First, try deleting the MSFS WASM folder, located under the Microsoft Flight Simulator directory in either %appdata% or %localappdata% for Steam and MS Store install directories respectively, which will rebuild when you next run MSFS. Rebooting is also recommended.
    - If that doesn't fix it, a full clean reinstall of MSFS will be required per the explicit instructions in [MSFS Clean Reinstallation Instructions](#msfs-clean-reinstallation-instructions).
- If you get an "Unable to attach MSFS - app disabled." message, the most likely causes are that MSFS is loading in very slowly and the attachment process is timing out, MSFS and this app are running at different permission privilege levels, or your anti-virus/malware app is blocking this app. To resolve, try the following:
  - Restart this app after MSFS has loaded in to the main menu.
  - Check that MSFS is not running as administrator.
  - Set an exclusion for this app in your anti-virus/malware app.
  - If all else fails, try running this app as administrator.
- If you get an "MSFS compatibility test failed - app disabled." message there are numerous possible causes:
  - You have started MSFS, made changes to MSFS settings and then started this app. To rectify:
    - First, try exiting this app, go to the MSFS settings menu, toggle any simple setting eg. vsync, save changes then restart this app.
    - If that doesn't work, try exiting this app and MSFS completely, start this app then start MSFS.
  - There is an issue with permissions and you may need to run the app as Administrator. 
  - You may have changed MSFS settings in your UserCfg.opt file beyond what is possible to set in the MSFS settings menu. To rectify, go into MSFS settings at the main menu and reset to default (F12) the graphics settings for both PC in MSFS 2020 or General in MSFS 2024 and VR for either, then make all changes to MSFS within the MSFS settings menu.
  - A new version of MSFS may introduce a different memory map than expected, preventing the app from auto‑adjusting to the new settings location.
    - In this case, the app will auto-install an explicitly compatible update in the first instance if one is available, otherwise it will attempt to offer an auto‑update to the version most likely to be compatible. This may be a test build if no stable release is available.
    - Release‑channel users temporarily moved to a test build for compatibility will revert to release‑only updates with the next formal version (manual opt‑in to test updates remains available).
    - If no suitable auto‑update is found, or if the update does not achieve compatibility, I am likely already aware and working on a solution. However, if you may be among the first to encounter the issue (e.g. on an MSFS beta), please raise a new issue on GitHub or contribute to an existing one.
- If you get an error message saying "XML Exception: Unexpected XML declaration" or "Exception: 'System.Xml.XMlException' during AutostartExe" when trying to install with the auto-start option for MSFS, it usually means your EXE.xml file has a corrupted data structure. To resolve, copy the content of your EXE.xml into MS Copilot and ask it to check and correct it for you. Paste the fixed structure back into your EXE.xml file, save it, then try reinstalling again.
- To uninstall
  - Ensure you have completely exited the app (ie. it is not hiding still running in your SysTray),
  - Run the installer and select Remove on the first window.
  - This will remove all traces of the app, including the desktop icon, MSFS or FSUIPC autostart entries if you used them, and the entire app folder, including your configuration file and in-game widget (if installed).

</details>

<details>
<summary><strong>FAQ answers in full</strong></summary>

### How does AutoFPS compare to the in-built Dynamic Setting feature that MS included in MSFS 2024?
- Dynamic Setting adjusts both TLOD and OLOD, the former initially then the latter if still significantly under target FPS. AutoFPS adjusts these settings independently, with OLOD changing inversely proportional to altitude by default, and can also change a lot more settings when auto settings reduction and VRAM+ are active.
- Dynamic Setting only ever adjusts down to as low as 50% of your currently set TLOD and/or OLOD, whereas AutoFPS in non-expert mode uses the default values as a centre point for adjustments and in expert mode is configurable to whatever range you want, including above the max of 400 settable within the MSFS settings menu.
- Dynamic Settings is much quicker than AutoFPS at responding to FPS reductions, due to it having internal access to MSFS performance metrics that AutoFPS does not, which also means it tends to overreact to short term stutters which are quite common now.
- Dynamic Setting uses a native frame rate target FPS rather than actual FPS when frame generation is in use, so you need to be mindful of this when using it with FG. ie. You can be achieving 150 FPS with DLSS FG with a target FPS of 100 and it will still reduce OLOD and TLOD to 50%. AutoFPS accommodates most mainstream FG types and allows an applicable target FPS to be set for each FG type.
- Hybrid dynamic settings / AutoFPS automation is now available in Expert Mode and leverages the strengths of both MSFS dynamic settings and AutoFPS:
  - MSFS’s built-in dynamic settings engine is highly responsive, using internal performance metrics not accessible externally. This allows it to adjust LODs more precisely and rapidly than AutoFPS alone.
  - AutoFPS complements this by adjusting the base LOD values that MSFS dynamic settings operate within. It responds to longer-term performance trends, especially increasing LODs in favourable conditions — extending the usable range beyond the default 50–100% window imposed by MSFS dynamic settings.
  - AutoFPS also manages a broader set of performance-critical graphics settings — including Cloud Quality and ten others — offering a more comprehensive performance management regime than dynamic LODs alone.
  - Together, MSFS dynamic settings and AutoFPS now work in tandem to manage both performance and visual quality, each addressing different layers of the optimisation stack.
  - See the Usage / Configuration section for details on how to optimally configure this feature.

### You developed this app and must therefore know how to best configure it, so surely you can tell me the best settings for me to use?
- The best settings for you to use are highly dependent on your system's capabilities, what other addons and supporting apps you use, and what your expectation of "best" actually is - higher FPS, improved visuals, smoother experience - some of which are mutually exclusive (i.e you can have one but only at the expense of another).
- Additionally, there are now thousands of users of this app and I simply don't have the time to provide individual settings advice to everyone that asks.
- As such, your best course of action is to start off with non-expert mode to see what the app actually does and when.
- Once you are comfortable with how the app operates, enable expert settings and start reading the tooltip and readme on what each setting does.
- When making changes, it is best to make small changes to only a few settings so that you can see the effect of them and adjust as desired.

### I am getting major stuttering (visual or sound), freezes or CTDs in MSFS using this app. What can I do to stop them?
- By far the most common reason is users have enabled Expert settings and have modified the default settings to be way beyond what their system is capable of, even without running the app.
- As such, the first step to resolve is to restore the app's default settings, which you can do by using the installer and selecting the Reset Configs options, which will recreate your config files.
- Rerun the app and try Non-Expert mode with IFR flight type and Auto Target FPS checked.
- If you have enabled the MSFS Performance Optimiser, which can cause stutters on some systems (especially with sound), try disabling it.
- If this doesn't resolve it, try enabling Expert options and reducing the FPS Sensitivity setting to 2, to allow smaller TLOD changes.

### My default MSFS graphics settings are messed up and each time I try to change them back they get messed up again. How do I fix this?
- You are likely trying to change these default MSFS settings while the app is still running and you are in an active flight, where the app will override any such changes you try to make.
- Either exit the app completely or be in the MSFS main menu (ie. NOT in a flight), then you can go to the MSFS settings screen and change your default MSFS settings to what you want and the app will restore these at the conclusion of a flight session or upon exiting.

### Why am I getting a dangerous/unsafe/virus/trojan/malware warning when trying to download or install?
- This app is unsigned because I am a hobbyist and the cost of obtaining certification is prohibitive to me, so you may get a warning message of a potentially dangerous app when you download it in a web browser like Chrome or from your antivirus program, especially Kaspersky which is known to flag false positives with this installer and the app.
- You can either trust this download, based on feedback you can easily find on Avsim and Youtube, make an exception in your browser and/or antivirus program for the download then run a virus scan and malware scan before you install just be sure, or just not install and use this app.

</details>

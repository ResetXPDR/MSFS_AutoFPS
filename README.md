# MSFS_AutoFPS v0.5.5.0

## Notice
My future development efforts on this app are mainly limited to maintenance, resilience improvements and streamlining of existing functionality only. I do add new functionality at times, mainly from my existing wishlist. I occasionally accept user requests for new functionality, however these will only be accepted if I judge it to be a great idea and it is technically achievable, useful to the majority of users, consistent with AutoFPS's existing design philosophy, with negligible, or preferably no, UI impact, and if I have the available time to do it.

## Important Simulator Notes
- **Official Forums Warning:** Do not mention or discuss this app on the official MSFS forums, even in private messages. Forum moderation treats active memory access as a Terms of Service violation regardless of its harmless operation. If you post about it there, your account may be flagged or banned.
- **SimConnect Wishlist:** There is an official MSFS wishlist item requesting SimConnect variable access to graphics settings, which would allow legitimate settings management. Please vote for it [here](https://forums.flightsimulator.com/t/expose-tlod-olod-clouds-etc-via-simconnect-l-vars/634075).
- **Memory Access & Safety:** AutoFPS reads and adjusts active simulator graphics settings in memory at a maximum rate of once per second per setting. On startup, memory offsets are validated; if an unsupported simulator build is detected and updated offsets cannot be validated, the app restricts itself to read-only mode to prevent unintended changes. AutoFPS is provided as-is and used at your own risk.

---

## Table of Contents
1. [Summary](#summary)
2. [Quick Start & Non-Expert Guide](#quick-start--non-expert-guide)
3. [In-Game Toolbar Widget & Web Interface](#in-game-toolbar-widget--web-interface)
4. [Desktop App Window & Controls](#desktop-app-window--controls)
5. [Graphics Modes & Frame Generation](#graphics-modes--frame-generation)
6. [Expert Mode Reference Guide](#expert-mode-reference-guide)
7. [Companion Apps & Advanced Features](#companion-apps--advanced-features)
8. [Installation, Updates & Maintenance](#installation-updates--maintenance)
9. [Frequently Asked Questions & Troubleshooting](#frequently-asked-questions--troubleshooting)

---

## Summary
Now fully compatible with MSFS 2020 and 2024 in a single application, AutoFPS improves simulator smoothness and framerate consistency by automatically changing key graphics settings on the fly. Key features include:

- Automatic TLOD adjustment to achieve and maintain a target FPS or follow an altitude schedule.
- Advanced FPS smoothing (FPS+) to filter out brief rendering spikes and dips.
- In-Game Toolbar Widget and local web interface (`http://localhost:54321`) with real-time telemetry, camera-based auto-hiding, and cockpit controls.
- Dynamic graphics mode architecture supporting native rendering, VR, native Frame Generation, and external mods (MSM, VectorXR, OFXR, Lossless Scaling) with dual-layer VR conflict protection.
- Simplified Non-Expert mode with automated guardrails suited to most user setups.
- Deep Expert Mode customisation covering sensitivity models, altitude bands, night optimisations, multi-profile pairing, and CPU thread optimisation.
- Native Dark Mode theme, resizable fluid layout, and automatic restoration of simulator settings upon exit.

---

## Quick Start & Non-Expert Guide

If you are new to the application or prefer an automated set-and-forget experience, Non-Expert mode provides reliable performance management without complex tuning.

### 5-Step Quick Start
1. Configure your general graphics settings inside MSFS as you normally would.
2. Launch AutoFPS before loading into a flight.
3. Ensure **Expert** (Use Expert Options) is left unchecked.
4. Select your **Flight Type**:
   - **VFR:** Tuned for lower altitudes, GA aircraft, and higher visual expectations.
   - **IFR:** Tuned for airliners, higher climb rates, and complex airport performance.
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
- **Night Optimisation:** Automatically uses `TLOD÷2` at night (capped at 200) to shed distant scenery workload in the dark.
- **MSFS 2020 Specifics:** Decreases cloud quality if performance cannot be recovered at Base Min TLOD or if GPU load exceeds 98% (when GPU-Z is running).
- **MSFS 2024 Specifics:** Engages automated settings reduction under marginal conditions and supports VRAM+ overflow mitigation when GPU-Z is active.

*Note: Do not manually change graphics settings in the MSFS menu while in an active flight with AutoFPS running, as the app will overwrite them or restore original values when the session ends.*

---

## In-Game Toolbar Widget & Web Interface

The companion In-Game Toolbar Widget and local web server allow in-cockpit monitoring and adjustments without switching out of the simulator.

### Installation & Access
- Select the **MSFS Widget** option during the installer setup to deploy the widget to your Community folder.
- In-cockpit: Open the MSFS top toolbar and click the **AutoFPS** icon.
- Web browser / tablet: Navigate to `http://localhost:54321/` on any device on your local network while AutoFPS is running.

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
  - Target FPS must be set at least 5% below the cap when Settings Reduction is disabled, or at least 5% plus your profile FPS tolerance percentage below the cap when Settings Reduction is enabled, ensuring reduced settings have sufficient headroom to recover.
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

---

## Expert Mode Reference Guide

Enable **Expert** (Use Expert Options) to reveal deep customisation controls across automation algorithms, altitude transitions, and scenery offsets.

### TLOD Automation Methods
- **FPS Sensitivity (Default & Recommended):** Adjusts TLOD in smaller, more frequent increments proportional to the variance between current and target FPS. Lower sensitivity values (e.g. 2–3) produce smaller steps, ideal for resolving visual stutters. Step size equals double the sensitivity value.
- **FPS Tolerance (Legacy):** Requires FPS to deviate beyond a set percentage tolerance before adjusting settings in larger, less frequent steps.
- **Auto TLOD:** Disregards FPS targets and automates TLOD strictly according to aircraft altitude (AGL), interpolating between Base Min and Top Max.
- **FPS Cap (Legacy):** Specialised configuration of Auto TLOD designed for locked framerates with TLOD Extra headroom seeking. Superseded by FPS Sensitivity with Fixed Target FPS.

### TLOD Altitude & Range Settings
- **TLOD Base Min:** Minimum TLOD applied at or below Alt TLOD Base (Range: 10 to Top Max - 10).
- **Alt TLOD Base:** Altitude (AGL) at or below which TLOD remains at Base Min (Range: 100 ft to 100,000 ft).
- **TLOD Top Max / Min:** Ceiling TLOD applied at or above Alt TLOD Top (Range: Base Min + 10 to 1,000).
- **Alt TLOD Top:** Altitude (AGL) at or above which TLOD can reach Top Max. Tooltips calculate recommended climb/descent rates (defaulting to 1,350 fpm for VFR and 2,000 fpm for IFR).
- **TLOD Base Extra:**
  - **Fixed:** Disables extra ground seeking.
  - **Extra:** Progressively seeks higher base TLOD when parked or taxiing (≤ 5 knots) under favourable headroom, freezing or reducing during takeoff rolls and low flight.
  - **Free:** Allows free-ranging TLOD between Base Min and Top Max at all altitudes based on real-time headroom (recommended only for rural VFR).
  - **Start Max:** Begins flight sessions with maximum extra TLOD rather than ramping up from base.
- **Mountains+ (Mtns):** Expands Top Max TLOD by a set offset when terrain elevation exceeds a defined threshold (e.g. 1,000 ft AGL).
- **Night Mode (Unified Per-Profile):** Replaces binary switches with a flexible dropdown:
  - **Off:** Maintains standard daytime settings around the clock.
  - **TLOD÷2:** Halves Top Max TLOD at night to eliminate distant rendering that cannot be seen in the dark.
  - **Clds-X:** Steps down volumetric clouds by a configured number of levels (default 2, floor Low), keeping full draw distance for night city lights and runways while reducing cloud load.
  - **Both:** Applies both TLOD halving and cloud reduction simultaneously.
  - *Behaviour:* Automatically clears active daytime cloud boost (`IncCloudQ`) upon entering night mode and restores it at sunrise. Transitions occur progressively based on solar position.

### Auto OLOD
- Adjusts Objects LOD based on an altitude band independent of FPS to avoid automation contention.
- Interpolates between **OLOD @ Base** (at or below Alt OLOD Base) and **OLOD @ Top** (at or above Alt OLOD Top). Allows higher OLOD on the ground for airport detail and lower OLOD at cruise, or vice versa.

### Flight Profiles & Command Line Arguments
- Supports up to 12 fully customisable profiles. Adjust the available total by editing `ProfileCount` (range 4–12, default 8: VFR, IFR, User 1–6) in `MSFS_AutoFPS.config`.
- **Profile Renaming:** Double-click the profile dropdown to rename. Press Enter or click away to save.
- **VR Auto-Switching:** Append `" VR"` to a profile name matching a 2D profile (e.g. `IFR` and `IFR VR`). AutoFPS switches profiles automatically when entering or exiting VR (`↔` symbol displayed).
- **Sim Rate Switching:** Append `" xX"` (e.g. `IFR 1X` and `IFR 4X`) to trigger automatic profile switching when acceleration rates change.
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

<details>
<summary><strong>GPU-Z Companion App & VRAM+ Protection</strong></summary>

### Overview
MSFS 2024 and DX12 MSFS 2020 are prone to exhausting GPU memory, causing severe framerate degradation when VRAM overflows into system RAM. VRAM+ monitors usage via GPU-Z shared memory and proactively sheds settings to prevent overflow.

### Setup
1. Download and install [GPU-Z](https://www.techpowerup.com/download/techpowerup-gpu-z/).
2. In GPU-Z settings: enable *Minimise on close*, *Load at Windows startup (minimised)*, *Minimise to system tray*, and *Refresh sensors while in background*.
3. Verify that the GPU displayed in GPU-Z matches the GPU driving MSFS.

### Automation Thresholds (`MSFS_AutoFPS.config`)
- `VRAMOverflowHoldTLOD` (Default `96`%): Caps TLOD to its current value, preventing further upward seeking.
- `VRAMOverflowReduceTLOD` (Default `98`%): Progressively invokes settings reduction (or reduces LODs by up to 50%) until memory pressure stabilises.
- `VRAMOverflowSettingsRecovery` (Default `93`%): Requires at least a 5% safety margin below the reduction threshold before settings are permitted to recover.
</details>

<details>
<summary><strong>RTSS Companion App & Periodic Spike Protection</strong></summary>

### Overview
RivaTuner Statistics Server ([RTSS](https://www.guru3d.com/download/rtss-rivatuner-statistics-server-download/)) provides external frame timing metrics and enables detection of cyclic frame time stutter often encountered over dense photogrammetry.

### Spike Protection
- Requires RTSS to be installed and running.
- Detects periodic frame time spikes (cadence of 0.3–1.8 seconds) matching photogrammetry reload stutters.
- When protection is active, AutoFPS applies temporary TLOD reductions, smoothly restoring values over time and distance once the spike train subsides.
- Logging is streamlined to record events only when 4 or more spikes occur outside a 1.8-second cadence window, minimising log file growth.
- `SpikeThresholdMultiplier` in `MSFS_AutoFPS.config` (default `2.0`) controls the multiple of the frame time median at which a spike is counted.
</details>

<details>
<summary><strong>Log+ Diagnostic Mode & Performance Logging</strong></summary>

### Features & Configuration
Enable **Log+** in Expert mode to access advanced diagnostics:
- **Detailed FPS Logging:** Click the live FPS readout during flight to record raw and averaged FPS to the log file every second for a defined duration (indicated by alternating purple text). Default duration is 30 seconds, changeable via `logFPSDetailsCountMax` in `MSFS_AutoFPS.config`.
- **Camera State Logging:** Logs simulator camera transitions to help diagnose view-change stutters.
- **System Metrics Logging:** Logs total CPU load, dominant core usage, MSFS process usage, GPU load, and VRAM pressure using exponentially weighted moving averages (EWMA).
- **Config File Parameters (`MSFS_AutoFPS.config`):**
  - `perfLogTopCoreCount` (default `2`): Number of busiest cores tracked.
  - `PerfLogBufferSize` (default `15`): Pre-breach and post-recovery sample buffer.
  - `PerfLoggingAlways` (default `false`): Forces continuous logging when Log+ is active.
  - `EwmaAlpha` (default `0.1`): Smoothing coefficient for scheduler jitter.
  - `MonitorMode` (default `false`): Disables all graphics adjustments, running AutoFPS purely as a background performance logger.
</details>

---

## Installation, Updates & Maintenance

### Requirements
- Microsoft Flight Simulator 2020 or 2024
- Windows 10 / 11 (64-bit)
- .NET 8 Desktop Runtime (x64)
- Visual C++ 2015–2022 Redistributable (x64)
- MobiFlight WASM Module (managed by the installer)

### Installation Options
- Download the latest installer (`MSFS_AutoFPS-Installer-vXYZ.exe`) from [GitHub Releases](https://github.com/ResetXPDR/MSFS_AutoFPS/releases/latest).
- **MSFS Widget:** Installs the toolbar companion to your Community folder.
- **Repair:** Reinstalls runtime libraries, MobiFlight modules, and the widget without altering custom configurations.
- **Custom Community Folders:** Use `2020 Folder...` or `2024 Folder...` to point to custom Community paths (such as Addons Linker locations). Custom paths persist across updates in `Install.log`. Right-click either button to reset to auto-detected simulator defaults.
- **Smart Deferred Updates:** When an auto-update contains an in-game widget update while MSFS is running, the installer defers installation until MSFS closes, preventing flight interruptions and locked-file errors.
- **File Locations:**
  - Binaries: `%appdata%\MSFS_AutoFPS\bin`
  - Logs: `%appdata%\MSFS_AutoFPS\log`
  - Installation Log: `%appdata%\MSFS_AutoFPS\Install.log`
  - Configs: `%appdata%\MSFS_AutoFPS\MSFS_AutoFPS.config`, `MSFS2020_AutoFPS.config`, `MSFS2024_AutoFPS.config`

### Update Channels & Management
- **Silent Updates:** Installs updates automatically without prompts, displaying release notes in Notepad afterwards.
- **Prompted Updates (Default):** Prompts for user confirmation before installing optional updates.
- **Show Updates:** Displays notifications when updates are available without downloading.
- **Mandatory Updates Only:** Notifies and installs only mandatory builds.
- **Update Channels:** Configurable in Expert mode:
  - **Release:** Standard stable release baseline.
  - **RC+ (Release Candidate), Test+, Exp+ (Experimental):** Accesses pre-release builds. Returning from a test channel to Release offers a one-time automatic rollback to the latest formal release. Test versions require all app update notifications to remain active to maintain a consistent test baseline.

<details>
<summary><strong>Manual Installation Instructions (Without Installer)</strong></summary>

If the executable installer cannot be run:
1. Download the [MobiFlight WASM Module](https://github.com/MobiFlight/MobiFlight-WASM-Module/releases) and extract it into your MSFS Community folder.
2. Install the Visual C++ Redistributable ([x86](https://aka.ms/vs/17/release/vc_redist.x86.exe) and [x64](https://aka.ms/vs/17/release/vc_redist.x64.exe)).
3. Download and install the latest [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
4. Create the folder `%appdata%\MSFS_AutoFPS\bin`.
5. Download [MSFS_AutoFPS.zip](https://github.com/ResetXPDR/MSFS_AutoFPS/blob/main/MSFS_AutoFPS.zip) and extract its contents into the newly created `bin` folder.
6. Create a shortcut to `MSFS_AutoFPS.exe` on your desktop.
</details>

<details>
<summary><strong>Advanced Installation Troubleshooting & Error Codes</strong></summary>

- **Bypass MobiFlight Check:** If the installer fails during the MobiFlight check despite the module being current, create a shortcut to the installer, append `-bypassmobiflight` to the Target field, and run the shortcut.
- **Unable to Locate Community Folder:** Often caused by leftover simulator userdata following an uninstall. AutoFPS checks for `UserCfg.opt` to verify simulator presence. Removing orphan userdata folders resolves the false detection.
- **Duplicate MobiFlight Modules:** Having duplicate modules in both Official and Community folders causes Sim Values to read zero. Remove older copies.
- **XML Exception During Autostart:** "XML Exception: Unexpected XML declaration" indicates corruption in your `EXE.xml` file. Paste the file content into an XML validator or AI tool to correct structural syntax, save, and rerun the installer.
- **Unable to Attach MSFS - App Disabled:** Caused by attachment timeouts during slow simulator loading, elevation mismatches (e.g. MSFS running as Admin while AutoFPS is not), or antivirus blocking. Restart AutoFPS after reaching the main menu, verify elevation parity, or set antivirus exceptions.
- **MSFS Compatibility Test Failed - App Disabled:**
  - Triggered if simulator settings were modified while the sim was running before AutoFPS launched. Rectify by toggling a simple setting (like VSync) in the MSFS menu, saving, and restarting AutoFPS.
  - Triggered if `UserCfg.opt` contains values beyond menu boundaries. Reset graphics settings to default (F12) in the MSFS menu to restore valid configurations.
  - Triggered by new simulator versions introducing relocated memory maps. AutoFPS will attempt to download a compatible update automatically.
</details>

<details>
<summary><strong>MSFS Clean Reinstallation Instructions</strong></summary>

*A clean reinstallation takes under 15 minutes in MSFS 2024, but several hours in MSFS 2020. Only undertake this if troubleshooting persistent WASM or core simulator crashes.*

1. Back up or relocate your Community folder.
2. Follow the official Zendesk clean install instructions exactly:  
   https://flightsimulator.zendesk.com/hc/en-us/articles/17335196046108-How-to-clean-install-the-simulator-on-PC
3. **Crucial:** Manually delete leftover simulator AppData/LocalState directories as specified in the guide. Controller profiles, logbooks, and cloud saves are retained on Xbox Live servers.
4. Reinstall the simulator, launch once to the main menu, and then restore your Community folder.
</details>

---

## Frequently Asked Questions & Troubleshooting

### How does AutoFPS compare to the in-built Dynamic Setting feature in MSFS 2024?
- **Independent Adjustment:** MSFS Dynamic Settings adjusts TLOD first and then OLOD if performance remains poor. AutoFPS manages TLOD and OLOD independently, with OLOD adjusting inversely proportional to altitude by default. AutoFPS can also manage a wider suite of visual settings when Settings Reduction and VRAM+ are active.
- **Adjustment Range:** MSFS Dynamic Settings only reduces down to 50% of your configured setting. AutoFPS in Non-Expert mode uses defaults as a midpoint, and in Expert mode can be set to any range, including beyond the in-game menu maximum of 400.
- **Response Speed:** MSFS Dynamic Settings responds faster due to internal metric access, but tends to overreact to brief stutters. AutoFPS filters short-term spikes to maintain visual stability.
- **Frame Generation Awareness:** MSFS Dynamic Settings bases its target on native framerate rather than delivered framerate when frame generation is active. AutoFPS accommodates all mainstream frame generation technologies with post-FG targets.
- **Hybrid Automation:** In Expert Mode, AutoFPS can work alongside MSFS Dynamic Settings: MSFS handles rapid micro-adjustments while AutoFPS manages base LOD ranges and external settings reductions.

### Why does my antivirus or browser flag the installer as untrusted?
AutoFPS is an open-source hobbyist project. Code signing certificates carry prohibitive costs, so new releases may occasionally trigger false positives (Windows SmartScreen, Kaspersky, or browser download warnings) until community reputation builds. If blocked, right-click the installer, select **Properties**, check **Unblock**, and click **Apply**.

### Why are my simulator settings restoring to unexpected values?
AutoFPS records your default settings when a flight begins and restores them when returning to the main menu or exiting the app. If you alter graphics settings in the MSFS menu while in an active flight with AutoFPS running, those changes will be overwritten. To change baseline settings, do so from the MSFS main menu or with AutoFPS closed.

### How do I eliminate visual or audio stutters when using the app?
1. Revert to the default configuration using the installer's **Reset Configs** option.
2. Begin in Non-Expert mode with the IFR profile and Auto Target FPS.
3. If the MSFS Performance Optimiser is enabled, try disabling it, as core affinity changes can cause audio crackling on certain hardware.
4. In Expert mode, lower **FPS Sensitivity** to 2 or 3 to enforce smaller, gradual TLOD adjustments.

### What should I do if SimConnect stays red or values show zero?
- Rerun the installer with the **Repair** option to ensure .NET 8, VC++ runtimes, and MobiFlight are properly installed.
- Check for duplicate MobiFlight WASM modules in both your Official and Community folders and remove older copies.
- If the log indicates WASM Exception 31, your simulator WASM cache may be corrupted. Delete the `WASM` cache folder located in your MSFS AppData directory and restart the simulator.

### Why does RTSS framerate telemetry freeze?
Switching between 2D Frame Generation and VR can latch the swapchain in shared memory. Clicking the FPS source icon on the app or widget toggles capture back to MSFS. If RTSS remains frozen, restarting RTSS or MSFS clears the shared memory handle.

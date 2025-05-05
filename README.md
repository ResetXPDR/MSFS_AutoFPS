# MSFS_AutoFPS by ResetXPDR (Updated for 0.4.4.9)

## Notice
My future development efforts on this app are mainly limited to maintenance, resilience improvements and streamlining of existing functionality only. I do add new functionality at times, mainly from my existing wishlist. I occasionally accept user requests for new functionality, however these will only be accepted if it is a great idea, technically achievable, useful to the majority of users, consistent with AutoFPS's existing design philosophy, with neglible, or preferably no, UI impact, and if I have the available time to do it.

## Summary
Based on muumimorko's idea and code in MSFS_AdaptiveLOD, as further developed by Fragtality in DynamicLOD, myself in DynamicLOD_ResetEdition and MSFS2020_AutoFPS and with inspiration from changes suggested by kayjay1c6b from his MSFS2024_AutoFPS.<br/><br/>

Now fully compatible with MSFS 2020 and 2024 in the one app, this app aims to improve the MSFS user experience by automatically changing key MSFS settings that impact MSFS performance and smoothness the most. It has an easy to use UI and provides features such as:<br/>
- Automatic TLOD adjustment when in the air to either achieve and maintain a target FPS or to an altitude schedule,
- A choice between VFR (GA) and IFR (Airliner) flight types, which defaults to settings suitable to each flight type and in Expert mode is fully customisable with four additional profiles available.
- Auto target FPS option, which is useful if you don't know what target FPS to choose or if your flight types are so varied that a single target FPS value is not always appropriate,
- A greatly simplified Non-Expert default UI option that uses pre-defined settings for an automated experience suited to most user scenarios,
- An Expert mode Option, which allows user customisation of the following:
  - A choice of TLOD automation method, each suitable for different specific uses of the app, namely FPS Sensitivity, FPS Tolerance, Auto TLOD and FPS Cap,
  - Auto raising and lowering of the minimum or base TLOD option, depending on low altitude performance being either very favourable or poor respectively,
  - Auto lowering of the maximum or top TLOD at night option, reducing system workload by not having to draw distant scenery that can't be seen in the dark anyway,
  - Automatic OLOD adjustment option based on an automatic or user-definable OLOD range and altitude band (AGL),
  - MSFS 2020 only 
    - Cloud quality decrease option for when either FPS can't be achieved at the lowest desired TLOD or when the GPU load is too high,
  - MSFS 2024 only 
    - Auto settings reduction option for MSFS 2024, activated under marginal performance conditions to help improve FPS and reduce VRAM usage.
    - Auto cloud increase option with TLOD Min/Base + enabled and sufficient performance margin exists.
- Simultaneous PC, FG (native nVidia, MFG, FG mod, FSR3 or Lossless Scaling), and VR mode compatibility, including correct FG FPS display, and separate FPS targets for each mode,
- Auto detection and protection from known similar apps already running or incompatibilities with newer MSFS versions, 
- VRAM+ overflow protection option for MSFS 2024, when running the [GPU-Z](https://www.techpowerup.com/download/techpowerup-gpu-z/) companion app.
- Auto installation of app updates (optional except for mandatory updates),
- Auto disabling of Dynamic Settings in MSFS 2024 while this app is active, to prevent settings contention, and
- Auto restoration of original MSFS settings changed by the app during a flight session, enhanced to withstand MSFS CTDs.<br><br>

**Really, really important:**
- Do not even mention, let alone try to discuss, this app on the MSFS official forums, even in personal messages, as they have taken the view that this app modifies licenced data, regardless of how harmless the way in which the app does it, and is therefore a violation of their Terms of Service and Code of Conduct for that website. If you do so, your post/personal message will be flagged by moderators and you may get banned from the MSFS official forums. You have been warned!
- Notwithstanding, there is a new MSFS wishlist item requesting simconnect variables access to MSFS settings, which would allow me to make this app legitimate in MS/Abobo's eyes and expand the range of possibilities of what this app could do in future. Please vote for it [here](https://forums.flightsimulator.com/t/expose-tlod-olod-clouds-etc-via-simconnect-l-vars/634075). 

Important:<br/> 
- This app directly accesses active MSFS memory locations while MSFS is running to read and set MSFS graphics settings on the fly at a maximum rate of one read and change per setting per second, normally much less. The app will first verify that the MSFS memory locations being used are still valid and if not, likely because of an MSFS version change, will attempt to find where they have been relocated. If it does find the new memory locations and they pass validation tests, the app will update itself automatically and will function as normal. If it can't find or validate MSFS memory locations at any time when starting up, the app will self-restrict to read only mode to prevent the app making changes to unknown MSFS memory locations.
- As such, I believe the app to be robust in its interaction with validated MSFS memory locations and to be responsible in disabling itself if it can't guarantee that. Nonetheless, this app is offered as is and no responsibility will be taken for unintended negative side effects. Use at your own risk!<br/><br/>


## FAQ

I am new to this app/MSFS, or I don't care for all this technical jargon. What is the simplest way to use this app to make my MSFS experience better?
- Leave your relevant MSFS graphics settings set as you normally would without this app, 
- Start the app before you load your flight,
- Leave Use Expert Settings unchecked,
- Pick what type of flight you are doing via the drop down list ie. either VFR (GA aircraft) or IFR (airliners),
- If using an FPS cap, enter that as your target FPS otherwise enter a target FPS your system can usually easily achieve or click on auto target FPS for the app to pick it for you,
- Click back on MSFS and wait until any FPS settle or TLOD seek events have finished (60 seconds max), then
- Go fly!
- Due to potential settings conflict, don't change any MSFS graphics settings that could be adjusted by AutoFPS while in a flight with AutoFPS already running.
- If MSFS 2024 performance still drops significantly in complex scenarios or you receive memory capacity warnings, see the VRAM+ FAQ entry.

What does this VRAM+ feature for MSFS 2024 do? How do I use it?
- MSFS 2024 is very demanding on VRAM use which can result in situations where its VRAM usage can completely fill all available VRAM on your GPU and overflow into much slower system RAM.
- VRAM overflow situations often lead to significant reductions in performance, in some cases down to single digit FPS, and makes MSFS much more prone to CTD.
- While MS/Asobo have reduced VRAM usage in MSFS 2024 SU2, many users with GPUs having less than 16GB VRAM are still regularly encountering VRAM overflow situations, even with modest settings.
- VRAM+ mitigates these potential overflow events by detecting an impending VRAM overflow and either hold or, if close enough, commence reducing key settings known to free up VRAM.
- Settings will automatically reduce, up to 50% for LODs and down by 2 quality levels for other settings, but only as far as is required to avert the VRAM overflow.
- When VRAM usage reduces to an acceptable level, these setting will automatically increase again, potentially back to their default values if sufficient VRAM headroom now exists.
- In order to use VRAM+ automatically, you will need to have VRAM+ enabled (default) and also run the [GPU-Z](https://www.techpowerup.com/download/techpowerup-gpu-z/) companion app as it is needed to provide current VRAM usage to the app.
  - Recommended GPU-Z settings are to minimise on close, load at windows startup (minimised), and to minimise to the system tray on the general tab, and refresh sensors while GPU-Z is in the background on the sensors tab.
  - Ensure the GPU shown on the GPU-Z GPU dropdown list is the same as the GPU being used for MSFS.

I am getting major stuttering, freezes or CTDs in MSFS using this app. What can I do to stop them?
- By far the most common reason is users have enabled Expert settings and have modified the default settings to be way beyond what their system is capable of, even without running the app.
- As such, the first step to resolve is to restore the app's default settings, which you can do by using the installer to uninstall (remove option) and reinstall, which will recreate your config file.
- Rerun the app and try Non-Expert mode with IFR flight type and Auto Target FPS checked.
- If this doesn't resolve it, try enabling Expert options and reducing the FPS Sensitivity setting to 2, to allow smaller TLOD changes.
- If still not resolved, try the FPS Tolerance mode, which was the automation method in the original release version that had larger TLOD changes but they occurred less often, with a setting of 5.

My default MSFS graphics settings are messed up and each time I try to change them back they get messed up again. How do I fix this?
- You are likely trying to change these default MSFS settings while the app is still running and you are in an active flight, where the app will override any such changes you try to make.
- Either exit the app completely or be in the MSFS main menu (ie. NOT in a flight), then you can go to the MSFS settings screen and change your default MSFS settings to what you want and the app will restore these at the conclusion of a flight session or upon exiting.

How does this app work for Frame Generation (FG) users?
- The app does detect correct FG FPS when FG (native nVidia or FG mod) is enabled in MSFS, however FG is only active when MSFS is the focused window and becomes inactive when not, through your graphics driver not this app.
- To see correct MSFS FG FPS, use the app's "On Top" option to overlay this app over MSFS and give MSFS the focus.
- If MSFS FG is being incorrectly reported as enabled by the app, the likely reason is that either the FG mod had been installed and removed or you have disabled Hardware Accelerated Graphics Scheduling under Windows settings and the now the now greyed out MSFS FG setting may show that it is off but it is still set to on internally to MSFS. To fix, change the DLSSG line in your UserCfg.opt file to be DLSSG 0.
- Lossless Scaling (LS) FG, including the scaling multiplier used, is also detected and the correct LSFG multiplied FPS is displayed.
  - Make sure your LSFG app is updated to the latest version that supports LSFG 3.0 (2.13.2 or later).
  - The app will first try to use an LS profile with the specific name MSFS2020 or MSFS2024, depending on which MSFS version is currently in use, to obtain these settings.
  - If such an MSFS2020 or MSFS2024 profile does not exist then the settings in the first profile found in the config file, usually named Default, will be used.
  - When adaptive frame generation is detected, a base FPS will be used for the target FPS because the frame generation multiplier is variable and is not currently detectable.
  - If you make changes to your LS settings after starting a flight, press AutoFPS's Reset button so that AutoFPS can redetect them correctly.
- FSR3 FG is now supported for MSFS 2024 as of SU2.
  - Although FSR3 FG can be implemented with an adaptive multiplier, MSFS currently implements it with a fixed 2X multiplier that is active regardless of whether MSFS has the focus or not.
- Multi Frame Generation, available only for users with 5000 series nVidia GPUs, is unable to be auto detected by the app at this time due to the privileged access need to read this setting.
  - In the interim, a manual MFG multiplier and target MFG FPS selection will be presented on the UI when a 5000 series nVidia GPU is detected.
  - Match the app's MFG multiplier with what you have set for MFG with MSFS in nVidia settings.
  - Set to MFG Off if not using MFG or using an alternative FG method.
  - Feature can be removed by the user setting MfgModeMultEnabled to false in MSFS_AutoFPS.config in the app root directory.
- Detection of all FG types other than LSFG is automatic within 5 seconds of making the change. Detection of LSFG is normally only performed upon starting a flight. If LSFG is started after this detection is normally performed, press the Reset button for it to be detected.
- Only one type of FG can be active at a time for the app to show FPS correctly. In particular, using native nVidia or the FG mod AND LSFG will cause incorrect FPS calculations in the app because they function differently when MSFS loses focus. Choose one or the other if you want to use them with this app.

Why am I getting a dangerous/unsafe/virus/trojan/malware warning when trying to download or install?
- This app is unsigned because I am a hobbyist and the cost of obtaining certification is prohibitive to me, so you may get a warning message of a potentially dangerous app when you download it in a web browser like Chrome or from your antivirus program, especially Kaspersky which is known to flag false positives with this installer and the app.
- You can either trust this download, based on feedback you can easily find on Avsim and Youtube, make an exception in your browser and/or antivirus program for the download then run a virus scan and malware scan before you install just be sure, or just not install and use this app.<br/><br/>

## Requirements

The Installer will install the following Software:
- .NET 8 Desktop Runtime (x64)
- Visual C++ Redistributable (x64)
- MobiFlight Event/WASM Module

<br/>

[Download here](https://github.com/ResetXPDR/MSFS_AutoFPS/releases/latest)

(Under Assests, the MSFS_AutoFPS-Installer-vXYZ.exe File)

<br/><br/>

## Installation / Update / Uninstall
Basically: Just run the Installer to either install, update or uninstall the app.<br/>

Some Notes:
- If the installer did not download or is zero size, try redownloading again with a different browser and possibly with AV temporarily disabled if required. It should be about 1.5Mb in size. 
- If the installer will not run at all:
  - Windows SmartScreen is potentially blocking it because the app is so new. The solution to try is:
    - Right-click on the Installer and select properties
    - Check the option "Unblock"
    - Click on Apply and Ok to save the change
    - Then try to install it again
  - Try creating an exception for the installer in your AV program, or just disabled the AV program temporarily while running the installer
- MSFS_AutoFPS and/or MSFS2020_AutoFPS must not be running before installing/updating/upgrading.
- Do not run the Installer as Admin unless it will not install due to a permissions issue.
- There is no need to uninstall MSFS2020_AutoFPS before upgrading to MSFS_AutoFPS. The installer uninstalls MSFS2020_AutoFPS if currently installed but preserves its MSFS 2020 config for use in this new app beforehand if desired or applicable.
- If you have previously removed MSFS2020_FPS without using the installer to remove it properly, you may experience issues installing MSFS_AutoFPS. If this happens, do the following:
  - Run the installer for the MSFS_AutoFPS and select remove to uninstall it completely.
  - Download the installer for MSFS2020_AutoFPS 0.4.3.1 [here](https://github.com/ResetXPDR/MSFS2020_AutoFPS/releases/download/v0.4.3.1/MSFS_AutoFPS-installer-v0.4.3.1.exe) and reinstall it with no autostart options.
  - Rerun the installer for MSFS_AutoFPS and reinstall with whatever autostart option you desire. 
- Mobiflight Module:
  - If the installer can't locate your Community folder to install this module, perhaps because of a Custom MSFS install location, download the latest module version from [here](https://github.com/MobiFlight/MobiFlight-WASM-Module/releases) and manually extract to your Community folder.
  - If the MobiFlight Module is not installed or outdated, MSFS also has to be stopped.
  - If you have duplicate MobiFlight Modules installed, in either your official or community folders, the app may display 0 value Sim Values and otherwise not function. Remove the duplicate versions, rerun the app installer and it should now work.
  - If the installer fails when checking/updating Mobiflight, despite the latest version being correctly installed in your MSFS Community folder, create a shortcut for the installer, add the command line option "-bypassmobiflight" to the target text box, then run the shortcut to be able to bypass this installation step.
- The app will automatically check for updates on startup by default and will notify you accordingly on the app status line.
  - If you wish to only be notified of mandatory updates, uncheck the Check For Updates checkbox.
  - In test versions, all app updates will be enabled by default regardless of what you have previously chosen for update notification in order to maintain a current test baseline.
- If you wish to retain your settings for an update version, do NOT uninstall first, as that deletes all app files, including the config file. Just run the installer, select update and your settings will be retained.
- The "Clean Install" option will recreate new configuration files without having to remove the app first.
- The "Install Latest Redistributables" option will silently (other than UAC prompts) update your system with the latest Visual C++ Redistributable and .NET 8 runtime versions.
  - Mandatory for initial installations, optional for manual updates, and disabled for quick updates to avoid UAC prompts.
- For Auto-Start either your FSUIPC7.ini or EXE.xml (MSFS) is modified. The Installer does not create a Backup.
- If you wish to remove an Auto-Start option from a previous installation, rerun the installer and select Remove Auto-Start and the click Update.
- The app may be blocked by Windows Security or your AV-Scanner, if so try to unblock or set an exception (for the whole Folder)
- The Installation-Location is fixed to %appdata%\MSFS_AutoFPS (your Users AppData\Roaming Folder) and can't be changed.
  - Binary in %appdata%\MSFS_AutoFPS\bin
  - Logs in %appdata%\MSFS_AutoFPS\log
  - Config: %appdata%\MSFS_AutoFPS\MSFS_AutoFPS.config (common), MSFS2020_AutoFPS.config and MSFS2024_AutoFPS.config
- If after installing and running the app your simconnect always stays red, your TLOD and OLOD values show as zero or you see "Critical Exception occurred: MSFS_AutoFPS - Unable to load DLL 'GpuzShMem.x64.dll' or one of its dependencies" in the log file:
  - Try reinstalling the app with the "Clean Install" and "Install Latest Redistributables" options selected.
  - Try downloading and installing/repairing a Microsoft official version of “Microsoft Visual C++ 2015 - 2022 Redistributable”, which may be missing from your Windows installation. Try installing [this](https://aka.ms/vs/17/release/vc_redist.x86.exe) and [this](https://aka.ms/vs/17/release/vc_redist.x64.exe).
  - Try downloading and installing/repairing the NET desktop runtime from [here](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.15/windowsdesktop-runtime-8.0.15-win-x64.exe) if still available. Alternatively, go to the Micrsoft .NET 8.0 download page [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) and download and install the latest .NET Desktop Runtime X64 version.
  - If still not resolved and the error code in your AutoFPS log file is Exception 31, you most likely have a corrupt MSFS installation so you can choose to either not run this app or to reinstall MSFS completely.
  - If reinstalling MSFS 2024, you need to do a clean install, as outlined for your MSFS version [here](https://flightsimulator.zendesk.com/hc/en-us/articles/17335196046108-How-to-clean-install-the-simulator-on-PC).
    - **Ensure you backup/relocate your Community folder BEFORE you do this, then put it back when the resintallation is complete.**
    - Only takes around 15 minutes to complete and all your settings, controller assignments, career progression and your pilot profile are retained.
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
  - You may have changed MSFS settings in your UserCfg.opt file beyond what is possible to set in the MSFS settings menu. To rectify, go into MSFS settings at the main menu and reset to default (F12) the graphics settings for both PC and VR mode, then make all changes to MSFS within the MSFS settings menu.
  - A new version of MSFS has come out that has a different memory map to what the app expects and the app can't auto adjust to the new memory location for MSFS settings. If so, I will likely be already aware of it and working on a solution, but if you may be one of the first to encounter it (eg. on an MSFS beta) then please raise an issue on github or contribute to an existing one if it has already been raised.
- If you get an error message saying "XML Exception: Unexpected XML declaration" or "Exception: 'System.Xml.XMlException' during AutostartExe" when trying to install with the auto-start option for MSFS, it usually means your EXE.xml file has a corrupted data structure. To resolve, copy the content of your EXE.xml into MS Copilot and ask it to check and correct it for you. Paste the fixed structure back into your EXE.xml file, save it, then try reinstalling again.
- To uninstall
  - Ensure you have completely exited the app (ie. it is not hiding still running in your SysTray),
  - Run the installer and select Remove on the first window.
  - This will remove all traces of the app, including the desktop icon, MSFS or FSUIPC autostart entries if you used them, and the entire app folder, including your configuration file.

<br/><br/>

## Usage / Configuration

- General
  - Can be started anytime, but preferably just after MSFS has loaded in to the main menu to minimise sudden MSFS settings changes when the app is initialising. The app will exit itself when MSFS closes. 
  - With the default install option, the app's icon more intuitively resides on the task bar when the app is running, not in the system tray overflow where it has been located in previous versions. Exit by clicking on the app window's close button and providing user confirmation when prompted.
  - If installed with the close app to system tray option, the app will remain running in the system tray until the user right clicks and selects Exit or the app auto exits when MSFS closes.
  - The app window's minimised/maximised state will be remembered between sessions and will be restored on the next startup.
  - The apps window's position will be remembered between sessions, except movements to it made while in VR due to window restoration issues. If there are issues with the window not displaying correctly on start-up, as can happen when auto-starting the app through MSFS or FSUIPC, either don't use auto-start, restart the app within 10 seconds of last closing it to auto reset the window position, or manually permanently disable this feature in the config file by setting the RememberWindowPos line to be false.
  - The user can progressively hide parts of the UI when the app window is double clicked anywhere that is not a control. The first double click will hide the Expert settings section (if applicable), the second will hide the general settings section and a third double click will restore all hidden settings sections. The last state in use will be restored when next starting the app. 
  - Running as Admin NOT usually required (BUT: It is required to be run under the same User/Elevation as MSFS).
  - Do NOT change MSFS graphics settings manually while in a flight with this app running as it will conflict with what the app is managing and they will automatically restore to what they originally were when you exit your flight. If you wish to change the defaults for these MSFS settings, you must do so either without this app running or, if it is, only while you are in the MSFS main menu (ie not in a flight).
- Connection Status
  - Red values indicate not connected, green is connected.
  - Automatically identifies which MSFS version is in use as either MSFS2020 or MSFS2024 and the version number. 
  - If the sim version is showing in red and is not the MSFS version you wish to configure before starting that MSFS version, click the 20>24 or 24>20 button, as applicable, and it will change to that.
- Sim Values
  - Will not show valid values unless all three connections are green. n/a means not available right now.
  - Green means the sim value is at or better than target value being sought, red means at lowest level or worse than target value being sought, orange means TLOD or OLOD is auto adjusting, black is shown otherwise.
  - Additional reduction settings values can be made visible when auto reduction or VRAM+ is active at Level 1 or greater by the user mousing over the Reduce value.
  - FPS shows the FPS for the current graphics mode averaged over 5 seconds which will smooth out any transient FPS spikes experienced when panning or loading new scenery or objects so that automated MSFS setting changes are minimised.
- General
  - Update Management
    - **Auto Updates** (default) will auto-install updates.
      - Mandatory updates will auto install regardless of user settings.
      - Optional updates will seek user confirmation and switch to **Show Updates** if declined.
      - Installer runs automatically, showing Release Notes in Notepad and auto-starting the new version.
    - **Show Updates** displays available updates and download links.
    - **Mandatory Updates Only** displays and installs only mandatory updates.
    - **+ Test** opts users into test version updates.
      - Test version users will have **+ Test** force enabled and greyed out.
      - **Mandatory Updates Only** will be unavailable until the app updates to a release version.
    - App startup sequence ensures update check is completed before connecting to MSFS.
  - VRAM+ - detects an impending VRAM overflow and either hold or, if close enough, commence reducing key settings known to free up VRAM.
    - **Requires the [GPU-Z](https://www.techpowerup.com/download/techpowerup-gpu-z/) companion app to be installed and running to work**.
      - Recommended GPU-Z settings are to minimise on close, load at windows startup (minimised), and to minimise to the system tray on the general tab, and refresh sensors while GPU-Z is in the background on the sensors tab.
      - Ensure the GPU shown on the GPU-Z GPU dropdown list is the same as the GPU being used for MSFS.
      - The feature will be disabled if GPU-Z is not found running.
    - Uses three thresholds for VRAM usage, all changeable in the app root directory config file (not the one in the bin subdirectory) after running the app once after updating:
      - VRAMOverflowHoldTLOD threshold, defaults to >= 96% VRAM in use and will cap TLOD to its current value, even if favourable performance conditions exist.
      - VRAMOverflowReduceTLOD threshold, defaults to >= 98% VRAM in use and will progressively activate auto settings reduction until the Hold threshold is achieved.
      - VRAMOverflowSettingsRecovery threshold, must be at least 5% less than VRAMOverflowReduceTLOD (ie. 93% by default) and will allow settings to progressively increase with favourable performance conditions until the feature disengages. 
    - VRAM+ setting:
      - Non-Expert mode:
        - Checkbox only visible if GPU-Z is running
        - Enabling uses the Max setting described for Expert mode.
      - Expert mode:
        - Off: disables the feature completely.
        - Max: (default) uses the full settings reduction suite minus clouds (to minimise impact on user experience), max levels 2, floor lowest and recovery altitude ground.
        - Set: uses the shared auto settings reduction options that will now be shown, regardless of whether auto settings reduction is enabled or not.
    - If you are continually experiencing VRAM+ activating, consider reducing your app TLOD settings and/or reducing other MSFS graphics settings.
  - Status Message - Displays key system messages, such as:
    - Before loading a flight - whether a newer version of the app is available to download and install,
    - Loading in to a flight  - whether MSFS memory integrity test have failed, and
    - Flight is loaded
      - Shows current sim rate with a range of 0.125X to 16X, which will display at the start of the app status line for any value except 1X.
      - Shows detected Graphics Mode (PC, FG, LSFG, MFG, FSR3 or VR) and DX version (MSFS 2020 only), app pause, FPS settle, TLOD+ seek, Mtn+, app priority mode and/or TLOD range as applicable.
      - The FPS settle timer runs for up to 30 seconds to allow FPS to settle between pausing/unpausing, auto target FPS calibration, TLOD Min + transitions and VR/PC/FG/LSFG mode transitions. This allows the FPS to stabilise before engaging automatic functions and should lead to much smaller TLOD changes when seeking the target FPS on such transitions.
      - App priority shows whether FPS or TLOD are the current automation priority. A + next to TLOD indicates that TLOD Min + has been activated and that a higher TLOD Min should be expected. Similarly, a + next to ATLOD or FPSCap indicates that TLOD Base + has been activated and that a higher TLOD offset across the entire altitude schedule should be expected. 
      - Bonus GPU load display if the optional [GPU-Z](https://www.techpowerup.com/download/techpowerup-gpu-z/) companion app is installed and detected running when starting any flight session. Note, the GPU-Z companion app is required to be running if the Decrease Cloud Quality option is selected in conjunction with the GPU Load activation method, as GPU-Z provides the necessary GPU load information to the app for this method to function.
      - Auto pause will activate if in flight and either MSFS is in active pause or the MSFS settings menu is being accessed.
  - Target FPS - The most important setting in this app. (10 - 200 allowable)
    - Set it to what FPS you want the app to target while running, noting that this value should be at the mid to lower end of what your system is capable of otherwise the app will be unlikely to achieve it.
    - There is a setting for each graphics mode (PC, FG, LSFG, MFG, FSR3 and VR) and each flight type (VFR, IFR and, if Expert mode, 4 user profiles). Automatically defaults to the currently detected graphics mode, however the user can change the target FPS for graphics modes other than what is currently active.
      - Selectable by using the drop down list of target FPS types in the app window.
      - Target FPS type drop down list background will change to orange when the target FPS type is different to the current graphics mode to clearly indicate this difference to the user.
      - Changes to target FPS for a type different to the current graphics mode will be saved to the config file without reloading the UI as it does not affect any other displayed settings.
      - Changes to any other setting on the UI will reload the target FPS type for the current graphics mode.
      - Particularly useful for VR users who have previously had to be in VR and remove their headset to change this setting.
    - If using MSFS FG, the target FPS you set is your desired FG Active FPS, not the FG Inactive FPS you see when this app has the focus instead of MSFS. 
    - If using an FPS cap, or Vsync for the same purpose, it is strongly recommended you use the FPS Cap automation method, available in Expert mode, with an FPS target matching your FPS cap and works well in such instances.
    - If using such an FPS cap with either FPS Sensitivity or Tolerance automation methods you will need to set your target FPS to be at least 5% lower than that cap to allow the automation logic to function correctly. This potentially introduces screen tearing, or breaks motion reprojection in VR, hence why Auto TLOD is preferred.
  - Auto Target FPS
    - Cannot be enabled at the same time as TLOD Min + due to automation control ambiguity. Selecting both will result in the most recent selection being enabled and the other disabled, with a dialog box to advise this.
    - When checked, a target FPS will automatically be calculated, following any initial FPS settling, when stationary on the ground or any time you are in the air.
    - Automatically recalulated if performance conditions are too low for the calculated target FPS, on the ground after arriving at a new destination, if you change graphics mode or if you uncheck then check the option again for a quick recalibration.
    - With IFR, or any of the user profiles in Expert mode, it will range from 95% of your current average FPS on the ground to 85% at or above 3000 ft, the latter being lower to give head room for Max TLOD.
    - With VFR it will be 5% less than each of the IFR percentages respectively to better suit the greater performance expectation with VFR flights.
  - On Top
    - Allows the app to overlay your MSFS session if desired, with MSFS having the focus.
    - Mainly useful for adjusting settings and seeing the outcome over the top of your flight as it progresses.
    - Should also satisfy single monitor users utilising the FG capability of MSFS as they now see the true FG FPS the app is reading when MSFS has the focus.
  - Reset button
    - Resets TLOD, Clouds, Auto Target FPS and graphics mode detection to initial state.
    - Useful to reinitialise and recommence the seek process for TLOD Min/Top + should conditions change significantly from what they were on initial start-up.
    - Can be activated by pressing ALT-R while the app has the focus, making it suitable to be assigned as a VR-friendly voice command with an app like VoiceAttack.
  - Flight type - VFR or IFR (any mode), and four additional user profiles (Expert mode)
    - Non-Expert mode:
      - VFR will use higher minimum and maximum TLODs and a lower TLOD base altitude than IFR.
      - Accounts for the greater performance expectation that GA flights in rural areas will have.
    - Expert mode:
      - Defaults to similar settings differences as Non-Expert mode for VFR and IFR.
      - user1 through user4 profiles are initially based on the IFR profile.
      - Settings are fully customisable and saved to/restored from the respective profile.
      - Profile names can be edited, including VFR and IFR:
        - Double clicking on the profile combo box text area toggles edit-ability, with non-editable being the initial state on startup.
        - Press Enter, press Tab or click on another control on the app UI for the changed text to be accepted.
    - Command line argument support for flight type profiles is as follows:
      - -ifr and -vfr continue launching the app with the default IFR and VFR profiles, even if renamed in Expert mode.
      - -user1 to -user4 now open their corresponding user profiles, restricted to Expert mode.
      - -profile "`<profile name>`" loads the specified profile in Expert mode, requiring an exact match.
  - Use Expert Options
    - Non-Expert Mode (unchecked and default)
      - Allows the app to use default settings in conjunction with your chosen target FPS that should produce good automated FPS tracking, provided you have set reasonable MSFS TLOD, OLOD and Cloud settings and a realistic FPS target within your system's performance capability.
      - The app will first attempt to automatically detect if an FPS cap is use by checking to see if the FPS matches the target FPS over a 10 second period at the end of the initial 30 second settling period.
      - If an FPS cap is detected, the FPS Cap TLOD automation method will be used with the following settings:
        - Auto Target FPS - disabled and hidden as a user-specified FPS cap is in use
        - LOD Step - 5
        - Alt TLOD Top - VFR 3000 ft, IFR 5000 ft
        - TLOD Base - VFR 50% of your current MSFS TLOD setting, IFR 25%
        - TLOD Top - VFR 150% of your current MSFS TLOD setting, IFR 100%
        - TLOD Base + - enabled
        - TLOD Base - - disabled
        - TLOD Top + - disabled
        - TLOD Top - - enabled
      - Otherwise, FPS Sensitivity will be used with the following settings:
        - Auto Target FPS - user selectable. Enabling automatically disables TLOD Min + due to automation control ambiguity
        - FPS Sensitivity - 5%
        - Avg Descent Rate - VFR 1000 fpm, IFR 2000 fpm
        - TLOD Minimum - VFR 100% of your current MSFS TLOD setting, IFR 50%
        - TLOD Maximum - VFR 300% of your current MSFS TLOD setting, IFR 200%
        - TLOD Min + - enabled, unless Auto Target FPS is enabled then disabled
        - TLOD Max + - disabled
        - TLOD Max - - enabled
      - Common to both automation methods:
        - VFR or IFR flight type - user selectable
        - Alt TLOD Base - VFR 100 ft, IFR 1000 ft
        - Auto OLOD - enabled and VFR 150% of your current MSFS OLOD setting, IFR 100% 
        - MSFS 2020 only
          - Decrease Cloud Quality
            - enabled by default and uses the GPU load activation method if GPU-Z is found to be running, otherwise the TLOD activation method is used.
            - can be disabled by setting DecCloudQNonExpert to false in the app config file located in the app's root, NOT bin, directory.
            - GPU load activation method decreases cloud quality with greater than 98% GPU load and recovers with less than 80% GPU load.
            - TLOD activation activation method uses a Cloud Recovery TLOD 2/5 between TLOD Minimum and TLOD Maximum or + 50 over TLOD Min, whichever is lower. If excessive changing of cloud quality levels are detected, the app will automatically increase its calculated cloud recovery TLOD.
        - MSFS 2024 only
          - Auto Settings Reduction - enabled with Max Levels: 2, Floor: Lowest, and Recovery: Alt TLOD Base. Reduction Settings Suite:
            - IFR: Full reduction suite minus clouds (to minimise impact on user experience)
            - VFR: Flora (Trees, Plants, Grass), Ray Traced and Terrain Shadows and Displacement Mapping 
          - Auto Increase Clouds - enabled
          - VRAM+ - enabled,
            - Only functional if GPU-Z is running.
            - Imposes different auto settings reduction settings, namely Floor: off and Recovery: Ground with 5% VRAM reduction achieved. 
            - Can be disabled by switching to Expert mode, unchecking VRAM+, then switching back to Non-Expert mode.
    - Expert Mode (checked)
      - The UI expands to show additional MSFS settings to adjust.
      - If you do not understand these settings and their impact on MSFS performance and graphics quality, it is strongly recommended that you do not use these expert options and you should uncheck this option. 
- Expert Settings
  - Auto Method - FPS Sensitivity generally gives the best results for most users and hence is the default. Use FPS Tolerance if you experience stuttering issues. Use Auto TLOD if you want a DynamicLOD-like experience. Use FPS Cap if you use an FPS cap on your system.
    - FPS Sensitivity - smaller changes more often.
      - Determines how sensitive the app will be to the variance between your current and target FPS.
      - Also determines the largest TLOD step size you will see, being double the FPS sensitivity number.
      - The lower the setting the smaller the changes will be, which is useful if you are experiencing stuttering with the default value of 5. Vice versa for higher settings. (1 - 20 allowable)
    - FPS Tolerance - larger changes less often.
      - Determines how much variance from your target FPS must occur before the app will adjust MSFS settings to achieve the target FPS and what nominal magnitude those changes will be.
      - The lower the setting, the more reactive the app will be, the more MSFS settings changes will occur and the changes will be smaller. (1% - 20% allowable)
    - Auto TLOD - functions similar to Auto OLOD by using an altitude schedule.
      - TLOD will adjust based on an altitude band with a base and top level and with TLOD values defined for each of these altitudes.
      - The app will set TLOD Base at or below the Alt TLOD Base (AGL), set the TLOD Top at or above Alt TLOD Top (AGL) and interpolate in between.
      - The nominal LOD Step Size can be set to allow users experiencing stuttering issues to try different LOD step sizes to help resolve the issue. The default value is 5. (1 - 20 allowable)
      - When TLOD Base + is unchecked, this method completely ignores FPS hence all FPS-related settings are removed from the UI.
      - TLOD Base + - additional TLOD with favourable performance conditions.
        - When enabled, set the target FPS to your FPS cap if you use one, or slightly below your usual FPS if not, for the logic to function properly.
        - Applicable to FPS Cap mode, Auto TLOD mode with TLOD Base + enabled and Non-Expert mode when an FPS cap is auto-detected.
        - On the ground and below Alt TLOD Top:
          - TLOD Base - checked, only available in FPS Cap mode, fixes total TLOD to TLOD Base.
          - Otherwise, in all other applicable modes, allows total TLOD up to 2 times TLOD Base.
        - Above Alt TLOD Top allows total TLOD up to 2 times TLOD Top.
        - TLOD Base + seek process will automatically start:
          - When commencing a flight, irrespective of aircraft position, and at the end when on the ground and stopped.
          - When Climbing through Alt TLOD Top.
          - Periodically, every 5 minutes nominally above Alt TLOD Top, if not already at 2 times TLOD Top.
        - The seek process can be restarted manually via the Reset button if flight conditions render the original TLOD Base invalid.
        - When seeking there will be:
          - Large steps of the lesser of TLOD Base and 50 on the ground, prioritising timeliness of completion.
          - Small steps of the TLOD Step Size in the air, prioritising stutter minimisation.
          - Intentional overshoot then reduction by a headroom amount to create headroom.
        - FPS cap breaches trigger an immediate reduction of the headroom amount, proportional to the magnitude of the FPS breach in non-seek occurrences.
        - Avoid quickly panning external views, especially during initial scenery loading, to prevent temporary FPS drops and unnecessary TLOD reductions.   
        - If FPS temporarily drops below the target during takeoff and TLOD decreases, it will progressively restore once conditions improve after passing Alt TLOD Top.
        - The calculated TLOD Base + will be applied as an offset that increases the entire TLOD altitude schedule by that amount.
        - TLOD Top + cannot be enabled simultaneously due to conflicting controls. The most recent selection will activate, disabling the other, with a dialog box notification.
        - If VRAM+ is active and VRAM limiting applies, TLOD Base + will be frozen or fully reset if the limitation is severe enough.
        - Config file settings can be changed for headroom amount, re-seek enabled, re-seek interval (seconds), and TLOD Base multiplier limits for IFR and VFR.
      - TLOD Top + - additional TLOD Top in high elevation areas.
        - Operates the same as TLOD Max + except that it cannot be enabled with TLOD Base + due to conflicting control over TLOD Top. Selecting both will result in the most recent selection being enabled and the other disabled, with a dialog box to advise this.
      - TLOD Top - reduced TLOD Top at night. Operates the same as TLOD Max -.
    - FPS Cap - a specific configuration of Auto TLOD optimised for when a system FPS cap is in use.
      - TLOD Base + and TLOD Top + are automatically enabled and disabled respectively, and their associated checkboxes are removed from the UI.
      - A TLOD Base - checkbox is provided in Expert mode which excludes TLOD Base + occurring below Alt TLOD Top when checked.
      - The following guidelines should be observed to get the best result from this mode:
        - Set TLOD Base and TLOD Top values within your FPS cap for worst-case performance during your flight type profile. TLOD Base + may increase with better performance but won't drop below the set values.
        - TLOD Base + is applied across the entire altitude schedule, potentially allowing a doubling of TLOD Top if performance conditions are favorable, so be particularly conservative when setting TLOD Top.
        - On the ground and stopped with TLOD Base - unchecked:
           - The initial seek process may temporarily destabilize FPS while identifying performance limits, but it typically stabilizes within 60 seconds once the ideal TLOD is determined.
           - Post-seek, panning may cause stuttering due to how MSFS handles high TLOD scenery loading, irrespective of whether you or this app has set them that high.
           - If stuttering persists, either uncheck TLOD Base - or use AutoTLOD with TLOD Base + disabled for the lowest possible TLOD on the ground.
  - TLOD Min - Sets the minimum TLOD the automation algorithm will use. (10 - TLOD Max-10 allowable)
  - TLOD Min + - additional TLOD Min with favourable performance conditions.
    - Requires at least 15% FPS headroom above target FPS to work at all. If you use an FPS cap, set your target FPS to at least 15% below it, preferably more.
    - When enabled, the TLOD Min + seek process will automatically start when commencing a flight, regardless of your aircraft's position, and at the conclusion of a flight when on the ground and stopped.
    - This seeking process can be manually restarted by pressing the Reset button, should flight conditions change such that the original TLOD Min + is no longer valid.
    - When seeking on the ground, TLOD Min + will progressively increase, in larger steps at first, until a higher TLOD Min with less than 15% FPS headroom is available or a maximum of nominally 2 times TLOD Min, changeable in the config file.
    - On climb out, TLOD Min + will remain set until your aircraft passes the calculated altitude threshold for the app priority mode to transition from TLOD to FPS priority.
    - While in FPS priority mode, TLOD Min + will calculate to be 50% (IFR or user profiles) or 25% (VFR) of the lower of either whatever TLOD you are currently getting or TLOD Max without TLOD Mtn Amt, but no lower than TLOD Min.
    - On descent through the calculated TLOD priority mode transition altitude, TLOD Min + will lock until landed to give the app time to reduce TLOD to Min at a moderate rate.
    - If at any time conditions deteriorate after TLOD Min + is set, there is an automatic 20% reduction of TLOD Min + in order to maintain target FPS. 
    - Avoid rapidly changing views or panning your external view too quickly, especially initially as un-cached scenery loads in, as you will induce temporary FPS drops that may trigger an unnecessary TLOD Min + reduction.    
    - Cannot be enabled at the same time as Auto Target FPS due to automation control ambiguity. Selecting both will result in the most recent selection being enabled and the other disabled, with a dialog box to advise this.
    - If VRAM+ is active and VRAM limiting is in effect, TLOD Min + seeking will be cancelled, and potentially completely reset if severe enough.
  - TLOD Base - - Excludes TLOD Base + occurring below Alt TLOD Top in FPS Cap mode only.
  - TLOD Max - Sets the maximum TLOD the automation algorithm will use. (TLOD Min+10 - 1000 allowable)
  - TLOD Max + - additional TLOD Max in high elevation areas. 
    - When enabled, extends TLOD Max in areas where the terrain is higher than Mtn Alt Min (100ft - 100000ft allowable) by the TLOD Mtn Amt amount (10 - 1000 allowable), progressively increasing by the TLOD step size per second until completely activated.
    - If terrain drops below Mtn Alt Min, TLOD Max + will remain fixed for 5 minutes then progressively reduce by the TLOD step size per second until completely deactivated.
  - TLOD Max - - reduced TLOD Max at night
    - Halves TLOD Max/Top at night to reduce system workload by not drawing scenery out to distances that can't be seen in the dark anyway.
    - Works with all automation methods: FPS Sensitivity, FPS Tolerance and Auto TLOD.
    - Defaults to enabled in Non-Expert mode. Enabled in Expert mode by checking the - box to the right of the TLOD Max/Top textbox.
    - When your flight transitions from day to night time, based on your location and the local time, TLOD Max/Top will progressively reduce to half its normal value, including the progressive removal of any TLOD Min/Base + and TLOD Max/Top + in use.
    - When your flight transitions from night to day time, based on your location and the local time, TLOD Max/Top will first progressively increase to its normal value then, providing you are either stopped on the ground or are in the air above Alt TLOD Min/Base, will activate the seeking process if TLOD Min/Base + is enabled and reactivate TLOD Max/Top + if enabled.
    - The status line will show either Day or Night when activated and Δ while transitioning between them.
  - Alt TLOD Base - Altitude (AGL) at or below which TLOD will be at TLOD Min. (100ft - 100000ft allowable)
  - Avg Descent Rate- Used in combination with FPS sensitivity to determine the altitude band in which TLOD will be interpolated between TLOD Min at the Alt TLOD base starting point and the lower of TLOD Max and the maximum TLOD your system can achieve while achieving at least your desired FPS target at a calculated top altitude. (200fpm - 10000fpm allowable)
    - This band ensures that, if you descend at your set Avg Descent Rate or less, that the app can decrement TLOD from TLOD Max to TLOD Min by the Alt TLOD Base without exceeding the LOD Step rate associated with the FPS sensitivity level you have set.
  - Auto OLOD
    - When enabled, four user definable parameters relating to this feature will be revealed on the UI when the user mouses over the Auto OLOD checkbox.
    - Rather than the automation being FPS based, which would cause contention with TLOD changes at the same time, OLOD will adjust based on an altitude band with a base (1000ft minimum and less than top) and top level (2000ft minimum, 100000ft maximum and greater than base) and with OLOD values defined for each of these altitudes (10 - 1000 allowable).
    -  The app will set OLOD @ Base at or below the Alt OLOD Base (AGL), set the OLOD @ Top at or above Alt OLOD Top (AGL) and interpolate in between. Note that OLOD @ Base can be higher, lower or the same value as the OLOD @ Top, depending on whether you want OLOD to decrease, increase or stay the same respectively as you ascend.
    -  In MSFS2024, integrates with auto settings reduction and VRAM+. 
  - MSFS 2020 only
    - Decrease Cloud Quality - When enabled, will reduce/restore cloud quality by one level if the activation condition is met.
      - Activation Methods
        - TLOD is the original method and is most suitable for systems where TLOD has the largest impact on desired MSFS performance.
        - GPU Load is the new method that allows cloud quality changes to occur independently of TLOD
          - Most suitable for systems where cloud quality has a similar or larger impact on desired MSFS performance than TLOD does.
          - Reduction occurs only when FPS falls below target by at least the margin specified by the current TLOD automation method.
        - All flight type profiles will use the same cloud reduction method.
      - TLOD (FPS Sensitivity and FPS Tolerance TLOD Automation Methods)
        - Decreases when TLOD has already auto reduced to TLOD Min and FPS is still below target FPS by more than the FPS tolerance.
        - Cloud Recovery TLOD with optional + (resultant TLOD must fall within TLOD Min+5 and TLOD Max-5)
          - The TLOD level required to cancel an active cloud quality reduction state and restore cloud quality back to its initial higher quality level.
          - Ideally set to 50 TLOD or more above TLOD Min to provide a TLOD buffer to minimise the chance that cloud quality will constantly change down and up.
          - When + is checked, Cloud Recovery TLOD becomes relative to TLOD Min instead of absolute.
      - GPU Load (All TLOD Automation Methods)
        - Requires the [GPU-Z](https://www.techpowerup.com/download/techpowerup-gpu-z/) companion app to be installed and running for this method to work. If GPU-Z is not running, the user will be alerted to start it in on the app status line in the General section.
        - Decreases when the GPU load, as measured by the GPU-Z companion app, is higher than the user-defined Decrease GPU Load percentage. (50% - 100% allowable)
        - Cloud Recovery GPU load (5% - 90% and at least 10% less than Decrease GPU Load allowable)
          - Recovers when the GPU load is lower than the user-defined Recover GPU Load percentage.
          - Ideally set to at least 15% lower than the Decrease GPU Load percentage to provide a GPU load buffer to minimise the chance that cloud quality will constantly change down and up.
  - MSFS 2024 only
    - Automatic Settings Reduction
      - Optional and activated under marginal performance conditions to help improve FPS and reduce VRAM usage.
      - Only applicable to MSFS 2024, as existing MSFS 2020 functionality is considered acceptable and MSFS 2024 tends to experience VRAM overflow, where such setting reductions are intended to help alleviate, much more frequently.
      - Not applicable to AutoTLOD mode with TLOD Base + disabled as it has no TLOD or FPS recovery mechanisms. 
      - Default settings will be saved on flight session commencement and restored on completion.
      - Settings reduction will only function and show when in a flight session and the secondary compatibility test passed.
      - Automatically reduces settings if the current FPS falls below the target FPS and TLOD is already at a minimum.
      - Disabled by default for Expert mode. When enabled the default values of the applicable settings are the same as for Non-Expert except Reduction Settings Suite: LODs, Clouds, Trees, and RT Shadows (which covers the settings most likely to improve FPS when they are reduced).
        - TLOD reductions with FPS Cap or AutoTLOD with TLOD Base+ enabled reduce normally calculated TLOD applicable to your aircraft's current altitude above ground rather than simply adjusting TLOD Base like the other two modes.
        - OLOD reductions are proportional to TLOD reductions.
        - Settings reduction activation cancels the TLOD Min/Base + seek process if active.
      - Automatically restores settings if the current FPS rises above the target FPS by the default tolerance or if the current FPS matches the target FPS and the TLOD has automatically increased by an acceptable margin.
        - Settings recovery will commence recovery immediately in any mode if the current FPS is above the target FPS by the FPS tolerance amount.
        - In AutoTLOD with TLOD Base + and FPS Cap modes, the reseek process will be triggered at least 60 seconds after the last time settings reduction level was reduced and the target FPS is being achieved once again.
        - Displacement Maps will not restore to enabled until above 100 ft AGL, as it is known to cause texture corruption at low altitudes.
      - TLOD Min/Base and OLOD will be progressively reduced at the user-defined LOD step rate by up to 50%.
        - TLOD Top gets reduced by the same proportional amount that TLOD Base gets reduced for FPS Cap mode and Auto TLOD with TLOD Base + enabled mode.
      - Max Levels setting determines how many levels the settings other than LODs will be reduced by in subsequent reduction cycles. 
      - Floor setting determines the minimum setting quality level that settings will be reduced to, including off if an allowable state for a setting.
      - Recover setting determines at which altitude (ground, Alt TLOD Base or Alt TLOD Top / equivalent) settings reduction recovery can occur as enabling it at lower altitudes may not be acceptable for some users. The default is Alt TLOD Base, being the middle option.
      - Reduction Settings Suite setting determines which settings set will be reduced. 
        - The presets are based on research that identified the settings most likely to improve FPS and/or reduce VRAM consumption when they are reduced, in decreasing order of effectiveness.
        - Custom Reduction Settings Suite allow the user to select which individual settings to reduce rather than the preset sets, the individual settings for which will appear when the user mouses over the reduction settings suite label or drop down list and Custom is currently selected. 
      - The Reset button will restore all settings to their default values.
    - VRAM+
      - Optional VRAM overflow protection that invokes auto settings reduction if VRAM usage exceeds a reduction threshold, nominally 98% and changeable in the config file.
      - Enabled by default and applies globally, including Non-Expert mode.
      - Requires the [GPU-Z](https://www.techpowerup.com/download/techpowerup-gpu-z/) companion app to be installed and running. See the FAQ section VRAM+ entry for details on how to configure correctly.
        - If the required companion app GPU-Z in not running, a message suggesting to the user to start it will be displayed during the initial 30 second settle timer of each session.
      - Settings reduction commences if the VRAM reduction threshold is exceeded, and continues until it is not longer exceeded, regardless of whether auto settings reduction is enabled or not. 
      - VRAM settings recovery threshold, nominally 5% below the VRAM limit reduction theshold, allows adequate VRAM usage headroom before settings recovery is activated.
      - User auto reduce settings are overriden while VRAM reduction is active to the maximum possible reduction, namely max reductions steps the greater of 2 or the current setting, reduction settings floor off and reduction settings suite to full.
      - VRAM LOD reductions occur at twice the rate of normal LOD reductions to more quickly address VRAM overflow. VRAM LOD recovery rate is half the normal LOD recovery rate to more gently recover from VRAM overflow.
      - VRAM protection will limit LOD reductions to 50% max, aligning it with normal settings reduction. 
      - VRAM+ triggering requires two consecutive threshold breaches before activating, in order to reduce the likelihood of false triggering.
      - Recovery is allowed at any altitude, including on the ground, due to the conservative 5% minimum reduction in VRAM use below the VRAM reduce threshold being required before recovery is allowed.
        - Each VRAM+ recovery will increase the recovery altitude setting by one, up to Alt TLOD Top, to reduce instances of VRAM+ cycling too often.
      - "Reduce" sim value label changes to "VRAM+" and shows in red when VRAM settings reduction is active, indicating that the app is actively reducing settings to manage VRAM usage.
    - Auto Increase Clouds
      - Auto increase cloud quality option with TLOD Min/Base + enabled.
      - Increases cloud quality by one level if not already at ultra and sufficient TLOD or FPS performance margin exists at the conclusion of the seek process. 
      - Removed immediately if the FPS cap is breached.
<br/><br/>

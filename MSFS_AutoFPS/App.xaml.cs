using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using H.NotifyIcon;
using Serilog;
using Serilog.Events;

namespace MSFS_AutoFPS;

public partial class App : Application
{
	private ServiceModel Model;

	private ServiceController Controller;

	protected int Interval = 1000;

	private TaskbarIcon notifyIcon;

	public static readonly string appDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MSFS_AutoFPS";

	public static readonly string binDir = appDir + "\\bin";

	public static readonly string ConfigFileCommon = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MSFS_AutoFPS\\MSFS_AutoFPS.config";

	public static readonly string ConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MSFS_AutoFPS\\MSFS2020_AutoFPS.config";

	public static readonly string ConfigFile2024 = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MSFS_AutoFPS\\MSFS2024_AutoFPS.config";

	public static readonly string UseSysTrayIcon = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\MSFS_AutoFPS\\SysTrayIcon.enabled";

	public static readonly string msConfigStore2020 = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Packages\\Microsoft.FlightSimulator_8wekyb3d8bbwe\\LocalCache\\UserCfg.opt";

	public static readonly string msConfigSteam2020 = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Microsoft Flight Simulator\\UserCfg.opt";

	public static readonly string msConfigStore2024 = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Packages\\Microsoft.Limitless_8wekyb3d8bbwe\\LocalCache\\UserCfg.opt";

	public static readonly string msConfigSteam2024 = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Microsoft Flight Simulator 2024\\UserCfg.opt";

	public new static App Current => Application.Current as App;

	protected override void OnStartup(StartupEventArgs e)
	{
		//IL_08fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0902: Unknown result type (might be due to invalid IL or missing references)
		//IL_0916: Unknown result type (might be due to invalid IL or missing references)
		base.OnStartup(e);
		string[] array = new string[7] { "MSFS_AutoFPS", "MSFS2020_AutoFPS", "MSFS2024_AutoFPS", "DynamicLOD_ResetEdition", "DynamicLOD", "SmoothFlight", "AeroTune" };
		foreach (string text in array)
		{
			int num = Process.GetProcessesByName(text).Length;
			if (text.Equals("MSFS_AutoFPS", StringComparison.OrdinalIgnoreCase) ? (num > 1) : (num > 0))
			{
				MessageBox.Show(text.Equals("MSFS_AutoFPS", StringComparison.OrdinalIgnoreCase) ? "MSFS_AutoFPS is already running. Aborting." : $"A conflicting app, {text}, is already running. {"MSFS_AutoFPS"} aborting.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Hand);
				Application.Current.Shutdown();
				return;
			}
		}
		Directory.SetCurrentDirectory(binDir);
		if (!File.Exists(ConfigFileCommon))
		{
			string text2 = Directory.GetCurrentDirectory() + "\\MSFS_AutoFPS.config";
			if (!File.Exists(text2))
			{
				MessageBox.Show("No MSFS Common Configuration File found! Closing ...", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Hand);
				Application.Current.Shutdown();
				return;
			}
			File.Copy(text2, ConfigFileCommon);
		}
		if (!File.Exists(ConfigFile))
		{
			string text3 = Directory.GetCurrentDirectory() + "\\MSFS2020_AutoFPS.config";
			if (!File.Exists(text3))
			{
				MessageBox.Show("No MSFS 2020 Configuration File found! Closing ...", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Hand);
				Application.Current.Shutdown();
				return;
			}
			File.Copy(text3, ConfigFile);
		}
		if (!File.Exists(ConfigFile2024))
		{
			string text4 = Directory.GetCurrentDirectory() + "\\MSFS2024_AutoFPS.config";
			if (!File.Exists(text4))
			{
				MessageBox.Show("No MSFS 2024 Configuration File found! Closing ...", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Hand);
				Application.Current.Shutdown();
				return;
			}
			File.Copy(text4, ConfigFile2024);
		}
		InitLog();
		Model = new ServiceModel();
		if (Model.DevTesting)
		{
			Logger.Log(LogLevel.Information, "App:OnStartup", "DevTesting active");
		}
		else
		{
			List<string> list = new List<string>();
			if (Model.LogPlus)
			{
				list.Add("Log+");
			}
			if (Model.ExperimentalFeatures)
			{
				list.Add("Experimental Features");
			}
			if (Model.MonitorMode)
			{
				list.Add("Monitor Mode");
			}
			if (list.Count > 0)
			{
				string message;
				if (list.Count == 1)
				{
					message = list[0] + " active";
				}
				else
				{
					message = ((list.Count != 2) ? (list[0] + ", " + list[1] + " and " + list[2] + " active") : (list[0] + " and " + list[1] + " active"));
				}
				Logger.Log(LogLevel.Information, "App:OnStartup", message);
			}
		}
		if (IsRunningAsAdmin())
		{
			Logger.Log(LogLevel.Information, "App:OnStartup", "App running as Admin");
		}
		string communityPathFromUserCfg = MSFS_AutoFPS.MainWindow.GetCommunityPathFromUserCfg(isMSFS2024: false);
		string communityPathFromUserCfg2 = MSFS_AutoFPS.MainWindow.GetCommunityPathFromUserCfg(isMSFS2024: true);
		string communityPathFromInstallLog = MSFS_AutoFPS.MainWindow.GetCommunityPathFromInstallLog(isMSFS2024: false);
		string communityPathFromInstallLog2 = MSFS_AutoFPS.MainWindow.GetCommunityPathFromInstallLog(isMSFS2024: true);
		string installedWidgetVersion = MSFS_AutoFPS.MainWindow.GetInstalledWidgetVersion(communityPathFromUserCfg ?? communityPathFromInstallLog);
		string installedWidgetVersion2 = MSFS_AutoFPS.MainWindow.GetInstalledWidgetVersion(communityPathFromUserCfg2 ?? communityPathFromInstallLog2);
		Model.WidgetInstalled2020 = installedWidgetVersion != null;
		Model.WidgetInstalled2024 = installedWidgetVersion2 != null;
		if (installedWidgetVersion != null || installedWidgetVersion2 != null)
		{
			Logger.Log(LogLevel.Information, "App:OnStartup", (installedWidgetVersion != null && installedWidgetVersion2 != null) ? $"In-Game Widget v{installedWidgetVersion} for MSFS 2020 and v{installedWidgetVersion2} for MSFS 2024 detected" : ("In-Game Widget v" + (installedWidgetVersion ?? installedWidgetVersion2) + " detected for " + ((installedWidgetVersion != null) ? "MSFS 2020" : "MSFS 2024")));
		}
		bool flag = installedWidgetVersion != null && !string.Equals(installedWidgetVersion, "1.6.0", StringComparison.OrdinalIgnoreCase);
		bool flag2 = installedWidgetVersion2 != null && !string.Equals(installedWidgetVersion2, "1.6.0", StringComparison.OrdinalIgnoreCase);
		if (flag | flag2)
		{
			bool num2 = flag && communityPathFromUserCfg != null && communityPathFromInstallLog != null && !string.Equals(NormalizePath(communityPathFromUserCfg), NormalizePath(communityPathFromInstallLog), StringComparison.OrdinalIgnoreCase);
			bool flag3 = flag2 && communityPathFromUserCfg2 != null && communityPathFromInstallLog2 != null && !string.Equals(NormalizePath(communityPathFromUserCfg2), NormalizePath(communityPathFromInstallLog2), StringComparison.OrdinalIgnoreCase);
			string value = (num2 ? $" (installer wrote to \"{communityPathFromInstallLog}\", but MSFS 2020 is actually configured to use \"{communityPathFromUserCfg}\")" : "");
			string value2 = (flag3 ? $" (installer wrote to \"{communityPathFromInstallLog2}\", but MSFS 2024 is actually configured to use \"{communityPathFromUserCfg2}\")" : "");
			Logger.Log(LogLevel.Warning, "App:OnStartup", $"In-Game Widget version mismatch detected in {((flag & flag2) ? "MSFS 2020 and MSFS 2024" : (flag ? "MSFS 2020" : "MSFS 2024"))} (Expected: v{"1.6.0"}). Aborting startup.{value}{value2}");
			string text5 = "";
			if (num2)
			{
				text5 += $"\n\nFor MSFS 2020, this app installed the widget to:\n\"{communityPathFromInstallLog}\"\n\nbut MSFS 2020 is actually configured to load add-ons from:\n\"{communityPathFromUserCfg}\"";
			}
			if (flag3)
			{
				text5 += $"\n\nFor MSFS 2024, this app installed the widget to:\n\"{communityPathFromInstallLog2}\"\n\nbut MSFS 2024 is actually configured to load add-ons from:\n\"{communityPathFromUserCfg2}\"";
			}
			if (text5 != "")
			{
				text5 += "\n\nIf you use a custom Community folder (e.g. with Addons Linker), make sure the widget is actually enabled/linked there.";
			}
			MessageBox.Show($"An outdated version of the AutoFPS In-Game Widget was detected in your {((flag & flag2) ? "MSFS 2020 and MSFS 2024" : (flag ? "MSFS 2020" : "MSFS 2024"))} Community folder.\n\n• Required Widget: v{"1.6.0"}\n\nThis can occur if an earlier test update was installed while MSFS was running, or if your configured Community folder differs from the one MSFS is actually using." + text5 + "\n\nTo update the widget:\n1. Close MSFS.\n2. Run the AutoFPS 0.5.5.0 installer and click 'Update!'.\n\nAutoFPS will now close.", "In-Game Widget Version Mismatch", MessageBoxButton.OK, MessageBoxImage.Hand);
			Application.Current.Shutdown();
			return;
		}
		Model.AutoUpdateJustCompleted = e.Args.Any((string a) => a.Equals("-postquickupdate", StringComparison.OrdinalIgnoreCase));
		string path = Path.Combine(appDir, "Install.log");
		bool flag4 = !File.Exists(path) || !File.ReadAllText(path).Contains("Upgrading from MSFS_AutoFPS 0.5.4");
		if (("0.5.5.0".StartsWith("0.5.4") && installedWidgetVersion == null && installedWidgetVersion2 == null && Model.AutoUpdateJustCompleted) & flag4)
		{
			string text6 = Path.Combine(MSFS_AutoFPS.MainWindow.KnownFolders.GetPath(MSFS_AutoFPS.MainWindow.KnownFolder.Downloads), "MSFS_AutoFPS-installer-v0.5.5.0.exe");
			if (File.Exists(text6))
			{
				bool flag5 = false;
				while (true)
				{
					MessageBoxResult messageBoxResult = MessageBoxResult.None;
					messageBoxResult = ((!flag5) ? MessageBox.Show("MSFS_AutoFPS has been updated to v0.5.5.0!\n\nThis release includes a new In-Game Toolbar Widget that lets you view telemetry, change flight profiles, and reset LODs directly from your cockpit toolbar.\n\nWould you like the auto updater to add the widget to your Community folder now?" + (IPCManager.IsSimRunning() ? "\n\nNote: Please close MSFS first before clicking 'Yes'." : ""), "New Feature: MSFS In-Game Widget", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) : MessageBox.Show("Please close MSFS before clicking 'Yes' to auto-install the In-Game Widget to your Community folder. Clicking 'No' will skip the installation.", "MSFS is still running", MessageBoxButton.YesNo, MessageBoxImage.Exclamation));
					if (messageBoxResult != MessageBoxResult.Yes)
					{
						break;
					}
					if (IPCManager.IsSimRunning())
					{
						flag5 = true;
						continue;
					}
					try
					{
						Process process = new Process();
						process.StartInfo.FileName = text6;
						process.StartInfo.Arguments = "-quickupdate -enablewidget";
						process.StartInfo.UseShellExecute = true;
						process.StartInfo.WorkingDirectory = MSFS_AutoFPS.MainWindow.KnownFolders.GetPath(MSFS_AutoFPS.MainWindow.KnownFolder.Downloads);
						process.Start();
						Application.Current.Shutdown();
						return;
					}
					catch (Exception ex)
					{
						Logger.Log(LogLevel.Error, "App:OnStartup", "Failed to launch installer for widget setup: " + ex.Message);
					}
				}
			}
		}
		InitSystray();
		Controller = new ServiceController(Model);
		Task.Run((Action)Controller.Run);
		DispatcherTimer val = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		val.Tick += OnTick;
		val.Start();
		MainWindow = new MainWindow(Model.UseSysTrayIcon ? (notifyIcon.DataContext as NotifyIconViewModel) : null, Model);
		Application.Current.MainWindow = MainWindow;
		if (!Model.UseSysTrayIcon || (Model.OpenWindow && Model.windowIsVisible))
		{
			MainWindow.Show();
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (Model != null)
		{
			Model.ConfigurationFileCommon.SaveConfiguration();
			int num = 5;
			Model.CancellationRequested = true;
			if (Model.PerfOptEnabled)
			{
				Model.RemovePerfOpt();
			}
			while (Model.MemoryAccess != null && --num > 0)
			{
				Thread.Sleep(Interval);
			}
		}
		notifyIcon?.Dispose();
		base.OnExit(e);
		Logger.Log(LogLevel.Information, "App:OnExit", "MSFS_AutoFPS exiting ...");
	}

	protected void OnTick(object sender, EventArgs e)
	{
		if (!Model.AppEnabled)
		{
			if (MainWindow.Visibility != Visibility.Visible)
			{
				MainWindow.Show();
			}
			MainWindow.WindowState = WindowState.Normal;
			MainWindow.Activate();
		}
		if (!Model.AutoUpdateInProgress && Model.UseSysTrayIcon && ((MainWindow.WindowState != WindowState.Minimized && MainWindow.Visibility == Visibility.Visible && !Model.windowIsVisible) || (MainWindow.Visibility == Visibility.Hidden && Model.windowIsVisible)))
		{
			Model.windowIsVisible = !Model.windowIsVisible;
			Model.ConfigurationFileCommon.SetSetting("windowIsVisible", Model.windowIsVisible ? "true" : "false");
		}
		if (Model.ServiceExited)
		{
			Current.Shutdown();
		}
	}

	protected void InitLog()
	{
		string text = "..\\log\\MSFS_AutoFPS.log";
		string text2 = "Debug";
		LoggerConfiguration loggerConfiguration = new LoggerConfiguration().WriteTo.File(text, LogEventLevel.Verbose, "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message} {NewLine}{Exception}", null, retainedFileCountLimit: 3, fileSizeLimitBytes: 1073741824L, levelSwitch: null, buffered: false, shared: false, flushToDiskInterval: null, rollingInterval: RollingInterval.Day);
		switch (text2)
		{
		case "Warning":
			loggerConfiguration.MinimumLevel.Warning();
			break;
		case "Debug":
			loggerConfiguration.MinimumLevel.Debug();
			break;
		case "Verbose":
			loggerConfiguration.MinimumLevel.Verbose();
			break;
		default:
			loggerConfiguration.MinimumLevel.Information();
			break;
		}
		Log.Logger = loggerConfiguration.CreateLogger();
		Log.Information("-----------------------------------------------------------------------");
		Logger.Log(LogLevel.Information, "App:InitLog", "MSFS_AutoFPS v0.5.5.0 started! Log Level: Debug Log File: ..\\log\\MSFS_AutoFPS.log");
	}

	protected void InitSystray()
	{
		if (File.Exists(UseSysTrayIcon))
		{
			Model.UseSysTrayIcon = true;
			Logger.Log(LogLevel.Information, "App:InitSystray", "Creating SysTray Icon ...");
			notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
			notifyIcon.Icon = GetIcon("icon.ico");
			notifyIcon.ForceCreate(enablesEfficiencyMode: false);
		}
	}

	public static Icon GetIcon(string filename)
	{
		using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MSFS_AutoFPS." + filename);
		return new Icon(stream);
	}

	public static string GetNewestConfigFile(bool isMSFS2024 = false)
	{
		string text = (isMSFS2024 ? msConfigStore2024 : msConfigStore2020);
		string text2 = (isMSFS2024 ? msConfigSteam2024 : msConfigSteam2020);
		bool flag = File.Exists(text);
		bool flag2 = File.Exists(text2);
		if (!flag && !flag2)
		{
			return null;
		}
		if (flag && !flag2)
		{
			return text;
		}
		if (!flag & flag2)
		{
			return text2;
		}
		DateTime lastWriteTime = File.GetLastWriteTime(text);
		DateTime lastWriteTime2 = File.GetLastWriteTime(text2);
		Logger.Log(LogLevel.Warning, "ServiceModel:ServiceModel", "MSFS " + (isMSFS2024 ? "2024" : "2020") + " Steam and MS Store installations detected. Defaulting to most recently modified version, namely " + ((lastWriteTime > lastWriteTime2) ? "MS Store" : "Steam") + " version");
		if (!(lastWriteTime > lastWriteTime2))
		{
			return text2;
		}
		return text;
	}

	public static bool IsRunningAsAdmin()
	{
		using WindowsIdentity ntIdentity = WindowsIdentity.GetCurrent();
		return new WindowsPrincipal(ntIdentity).IsInRole(WindowsBuiltInRole.Administrator);
	}

	private static string NormalizePath(string path)
	{
		try
		{
			return Path.GetFullPath(path).TrimEnd('\\', '/');
		}
		catch
		{
			return path?.TrimEnd('\\', '/');
		}
	}
}

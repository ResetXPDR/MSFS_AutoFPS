using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Threading;
using CpuHeadroom;
using MSFS_AutoFPS.Utilities;
using Newtonsoft.Json.Linq;

namespace MSFS_AutoFPS;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow, IComponentConnector
{
	public enum KnownFolder
	{
		Contacts,
		Downloads,
		Favorites,
		Links,
		SavedGames,
		SavedSearches
	}

	public static class KnownFolders
	{
		private static readonly Dictionary<KnownFolder, Guid> _guids = new Dictionary<KnownFolder, Guid>
		{
			[KnownFolder.Contacts] = new Guid("56784854-C6CB-462B-8169-88E350ACB882"),
			[KnownFolder.Downloads] = new Guid("374DE290-123F-4565-9164-39C4925E467B"),
			[KnownFolder.Favorites] = new Guid("1777F761-68AD-4D8A-87BD-30B759FA33DD"),
			[KnownFolder.Links] = new Guid("BFB9D5E0-C6A9-404C-B2B2-AE6DB6AF4968"),
			[KnownFolder.SavedGames] = new Guid("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4"),
			[KnownFolder.SavedSearches] = new Guid("7D1D3A04-DEBB-4115-95CF-2F29DA2920DA")
		};

		public static string GetPath(KnownFolder knownFolder)
		{
			return SHGetKnownFolderPath(_guids[knownFolder], 0u);
		}

		[DllImport("shell32", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
		private static extern string SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, nint hToken = 0);
	}

	public enum CustomMessageBoxResult
	{
		Action,
		Close
	}

	public class CustomMessageBox
	{
		public static CustomMessageBoxResult Show(string message, string link, string title, string actionButtonText)
		{
			Window owner = Application.Current?.MainWindow;
			Window window = new Window
			{
				Title = title,
				Width = 400.0,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				ResizeMode = ResizeMode.CanResize,
				SizeToContent = SizeToContent.Height,
				Owner = owner,
				ShowInTaskbar = false
			};
			StackPanel stackPanel = new StackPanel
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Width = 400.0
			};
			TextBlock element = new TextBlock
			{
				Text = message,
				Margin = new Thickness(10.0),
				TextWrapping = TextWrapping.Wrap,
				TextAlignment = TextAlignment.Left,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Width = 380.0
			};
			stackPanel.Children.Add(element);
			CustomMessageBoxResult result = CustomMessageBoxResult.Close;
			StackPanel stackPanel2 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Center,
				Margin = new Thickness(10.0)
			};
			Button button = new Button
			{
				Content = actionButtonText,
				Width = 120.0,
				Margin = new Thickness(5.0)
			};
			button.Click += (object sender, RoutedEventArgs e) =>
			{
				result = CustomMessageBoxResult.Action;
				Process.Start(new ProcessStartInfo(link)
				{
					UseShellExecute = true
				});
				window.Close();
			};
			Button button2 = new Button
			{
				Content = "Close",
				Width = 75.0,
				Margin = new Thickness(5.0)
			};
			button2.Click += (object sender, RoutedEventArgs e) =>
			{
				result = CustomMessageBoxResult.Close;
				window.Close();
			};
			stackPanel2.Children.Add(button);
			stackPanel2.Children.Add(button2);
			stackPanel.Children.Add(stackPanel2);
			window.Content = stackPanel;
			window.ShowDialog();
			return result;
		}
	}

	protected NotifyIconViewModel notifyModel;

	protected ServiceModel serviceModel;

	protected DispatcherTimer timer;

	private AutoFpsWebServer _webServer;

	private bool updateAvailable;

	private bool sessionRunning;

	private bool firstSessionRan;

	private int _msfsPid = -1;

	private int fixedFPSWarningCountdown;

	protected string assemblyVersion = "0.5.5.0";

	private string TargetFPSType = "NFR";

	private float tlodLast;

	private float olodLast;

	private int reductionSettingsDepthLast;

	private int selectedIndex = -1;

	private bool ShowAutoReducePanel;

	private bool ShowReduceSimValues;

	private int ShowAutoReducePanelState;

	private static string appName = typeof(MainWindow).Namespace;

	private UserCfgQuickCheck lodChecker;

	private MemoryManager.SettingsMaskBuilder settingsMaskBuilder = new MemoryManager.SettingsMaskBuilder(768);

	private bool AttemptedExplicitCompatUpdate;

	private CpuHeadroomProbe? _cpuProbe;

	private Action<CpuSnapshot>? _cpuProbeHandler;

	private bool _isTitleBarCloseClick;

	private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
	{
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	private int activeFpsIcon;

	private bool _isExplicitExit;

	private bool _isPromptOpen;

	private Brush ThemeTextBrush => (Brush)FindResource("TextForegroundBrush");

	private Brush ThemeInputBgBrush => (Brush)FindResource("InputBackgroundBrush");

	private Brush ThemeGreenBrush
	{
		get
		{
			if (!serviceModel.isDarkMode)
			{
				return Brushes.Green;
			}
			return new SolidColorBrush(Color.FromRgb(74, 222, 128));
		}
	}

	private Brush ThemeRedBrush
	{
		get
		{
			if (!serviceModel.isDarkMode)
			{
				return Brushes.Red;
			}
			return new SolidColorBrush(Color.FromRgb(byte.MaxValue, 107, 107));
		}
	}

	private Brush ThemeOrangeBrush
	{
		get
		{
			if (!serviceModel.isDarkMode)
			{
				return Brushes.DarkOrange;
			}
			return new SolidColorBrush(Color.FromRgb(byte.MaxValue, 167, 38));
		}
	}

	private Brush ThemeBlueBrush
	{
		get
		{
			if (!serviceModel.isDarkMode)
			{
				return Brushes.MediumBlue;
			}
			return new SolidColorBrush(Color.FromRgb(96, 165, 250));
		}
	}

	private Brush ThemeIndianRedBrush => Brushes.IndianRed;

	private Brush ThemeSalmonBrush => Brushes.Salmon;

	public MainWindow(NotifyIconViewModel notifyModel, ServiceModel serviceModel)
	{
		//IL_02a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c4: Expected Obj, but got Unknown
		//IL_02df: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		MainWindow mainWindow = this;
		InitializeComponent();
		mnuCloseToTray.Visibility = ((!serviceModel.UseSysTrayIcon) ? Visibility.Collapsed : Visibility.Visible);
		_webServer = new AutoFpsWebServer(54321, GetTelemetryJson, () =>
		{
			((DispatcherObject)mainWindow).Dispatcher.Invoke((Action)(() =>
			{
				if (serviceModel.IsSimRunning && serviceModel.IsSessionRunning)
				{
					serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
				}
			}));
		}, (int idx) =>
		{
			((DispatcherObject)mainWindow).Dispatcher.Invoke((Action)(() =>
			{
				if (idx >= 0 && idx < mainWindow.cbActiveProfile.Items.Count && mainWindow.cbActiveProfile.SelectedIndex != idx)
				{
					mainWindow.cbActiveProfile.SelectedIndex = idx;
				}
			}));
		}, () =>
		{
			((DispatcherObject)mainWindow).Dispatcher.Invoke((Action)(() =>
			{
				mainWindow.ToggleTitleBar();
			}));
		}, () =>
		{
			((DispatcherObject)mainWindow).Dispatcher.Invoke((Action)(() =>
			{
				mainWindow.chkDarkMode.IsChecked = !mainWindow.chkDarkMode.IsChecked;
				mainWindow.chkDarkMode_Click(null, null);
			}));
		}, (string hkJson) =>
		{
			((DispatcherObject)mainWindow).Dispatcher.Invoke((Action)(() =>
			{
				serviceModel.WidgetHotkey = hkJson;
				string text = (string.IsNullOrWhiteSpace(hkJson) ? "None" : hkJson);
				serviceModel.ConfigurationFile.SetSetting("WidgetHotkey", text);
				Logger.Log(LogLevel.Information, "MainWindow:MainWindow", "WidgetHotkey set to " + text);
			}));
		}, BuildWidgetControlsJson, WidgetSetTargetMode, WidgetSetTarget, WidgetSetTargetFps, WidgetToggleExpert, WidgetToggleLogPlus, (bool collapsed) =>
		{
			((DispatcherObject)mainWindow).Dispatcher.Invoke((Action)(() =>
			{
				serviceModel.WidgetGeneralCollapsed = collapsed;
				serviceModel.ConfigurationFile.SetSetting("WidgetGeneralCollapsed", collapsed ? "true" : "false");
				Logger.Log(LogLevel.Information, "MainWindow:MainWindow", $"WidgetGeneralCollapsed set to {collapsed}");
			}));
		}, (int mode) =>
		{
			((DispatcherObject)mainWindow).Dispatcher.Invoke((Action)(() =>
			{
				serviceModel.WidgetAutoHide = mode;
				serviceModel.ConfigurationFile.SetSetting("WidgetAutoHide", mode.ToString());
				Logger.Log(LogLevel.Information, "MainWindow:MainWindow", $"WidgetAutoHide set to {mode}");
			}));
		}, () =>
		{
			((DispatcherObject)mainWindow).Dispatcher.Invoke((Action)(() =>
			{
				if (!serviceModel.VrModeActive || ((!serviceModel.OfxrModeEnabled || !serviceModel.OfxrStatusJsonActive || !(serviceModel.OfxrOutputFps > 0f)) && (!serviceModel.VxrModeEnabled || !serviceModel.VxrStatusJsonActive || !(serviceModel.VxrOutputFps > 0f))))
				{
					ServiceModel serviceModel2 = serviceModel;
					bool fpsSourceRTSS = (serviceModel.fpsSourceRTSSDefault = !serviceModel.fpsSourceRTSS);
					serviceModel2.fpsSourceRTSS = fpsSourceRTSS;
					serviceModel.ConfigurationFileCommon.SetSetting("fpsSourceRTSS", serviceModel.fpsSourceRTSS.ToString().ToLower());
					Logger.Log(LogLevel.Information, "MainWindow:imgSimFPS_Click", "FPS source changed to " + (serviceModel.fpsSourceRTSS ? "RTSS" : "MSFS"));
					serviceModel.fpsAverageReset = true;
				}
			}));
		});
		ConfigureFluentWebApi();
		_webServer.Start();
		if (serviceModel.UseSysTrayIcon)
		{
			this.notifyModel = notifyModel;
		}
		this.serviceModel = serviceModel;
		SetWindowTitle();
		if (serviceModel.UseExpertOptions)
		{
			stkpnlExpertSettings.Visibility = Visibility.Visible;
		}
		else
		{
			stkpnlExpertSettings.Visibility = Visibility.Collapsed;
		}
		if (serviceModel.LogPlus)
		{
			Logger.Log(LogLevel.Information, "MainWindow:MainWindow", $"Virtual screen coordinates: ({serviceModel.virtualScreenMinX},{serviceModel.virtualScreenMinY}) ({serviceModel.virtualScreenMaxX},{serviceModel.virtualScreenMaxY})");
			Logger.Log(LogLevel.Information, "MainWindow:MainWindow", $"Initial window position set to ({serviceModel.windowLeft},{serviceModel.windowTop})");
		}
		timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		timer.Tick += OnTick;
		Window_IsVisibleChanged(this, default);
		lodChecker = new UserCfgQuickCheck(serviceModel, serviceModel.isMSFS2024 ? serviceModel.DetectedMSFSConfig2024 : serviceModel.DetectedMSFSConfig2020, serviceModel.DefaultTLOD, serviceModel.DefaultTLOD_VR, serviceModel.DefaultOLOD, serviceModel.DefaultOLOD_VR, serviceModel.DefaultCloudQ, serviceModel.DefaultCloudQ_VR);
		lodChecker.ValuesChanged += (float tlodPC, float tlodVR, float olodPC, float olodVR, int cloudQ_PC, int cloudQ_VR) =>
		{
			if (serviceModel.IsSimRunning && IPCManager.IsSimRunning() && !serviceModel.MonitorMode)
			{
				List<string> list = new List<string>();
				if (serviceModel.MemoryAccess == null)
				{
					if (tlodPC != serviceModel.DefaultTLOD)
					{
						list.Add($"TLOD {serviceModel.DefaultTLOD} → {tlodPC}");
						serviceModel.DefaultTLOD = tlodPC;
					}
					if (tlodVR != serviceModel.DefaultTLOD_VR)
					{
						list.Add($"TLOD VR {serviceModel.DefaultTLOD_VR} → {tlodVR}");
						serviceModel.DefaultTLOD_VR = tlodVR;
					}
					if (olodPC != serviceModel.DefaultOLOD)
					{
						list.Add($"OLOD {serviceModel.DefaultOLOD} → {olodPC}");
						serviceModel.DefaultOLOD = olodPC;
					}
					if (olodVR != serviceModel.DefaultOLOD_VR)
					{
						list.Add($"OLOD VR {serviceModel.DefaultOLOD_VR} → {olodVR}");
						serviceModel.DefaultOLOD_VR = olodVR;
					}
					if (cloudQ_PC != serviceModel.DefaultCloudQ)
					{
						string text = ServiceModel.SettingQualityText(serviceModel.DefaultCloudQ);
						string text2 = ServiceModel.SettingQualityText(cloudQ_PC);
						list.Add("CloudQ " + text + " → " + text2);
						serviceModel.DefaultCloudQ = cloudQ_PC;
					}
					if (cloudQ_VR != serviceModel.DefaultCloudQ_VR)
					{
						string text3 = ServiceModel.SettingQualityText(serviceModel.DefaultCloudQ_VR);
						string text4 = ServiceModel.SettingQualityText(cloudQ_VR);
						list.Add("CloudQ VR " + text3 + " → " + text4);
						serviceModel.DefaultCloudQ_VR = cloudQ_VR;
					}
					if (list.Count > 0)
					{
						Logger.Log(LogLevel.Information, "MainWindow:UserCfgQuickCheck", "MSFS default setting changed by user: " + string.Join(", ", list));
					}
				}
				else
				{
					if (serviceModel.LODControllerBusy || serviceModel.ResetInProgress)
					{
						Thread.Sleep(100);
					}
					if (!serviceModel.LODControllerBusy && !serviceModel.ResetInProgress && serviceModel.MemoryAccess != null && serviceModel.tlod >= serviceModel.SimMinLOD)
					{
						string text5 = serviceModel.MemoryAccess.CompareSettingsWithModel(correctSettings: true);
						if (!string.IsNullOrEmpty(text5))
						{
							bool flag = text5.Contains(",");
							Logger.Log(LogLevel.Warning, "MainWindow:UserCfgQuickCheck", "MSFS config file changed during flight session; AutoFPS restored managed setting" + (flag ? "s" : "") + ": " + text5);
						}
					}
				}
			}
		};
		Resources["CustomLabelColor"] = new SolidColorBrush(Colors.Aqua);
		WindowState = ((!serviceModel.windowIsVisible) ? WindowState.Minimized : WindowState.Normal);
		cbActiveProfile.Items.Add(serviceModel.ProfileName[0]);
		cbActiveProfile.Items.Add(serviceModel.ProfileName[1]);
		serviceModel.UpdateAffinityTooltip = () =>
		{
			((DispatcherObject)mainWindow).Dispatcher.Invoke((Action)mainWindow.SetAffinityTooltip);
		};
		serviceModel.UpdatePriorityTooltip = () =>
		{
			((DispatcherObject)mainWindow).Dispatcher.Invoke((Action)mainWindow.SetPriorityTooltip);
		};
		serviceModel.UpdatePowerPlanTooltip = () =>
		{
			((DispatcherObject)mainWindow).Dispatcher.Invoke((Action)mainWindow.SetPowerPlanTooltip);
		};
		stkpnlExpertSettings.Visibility = Visibility.Collapsed;
		LoadSettings();
		DownloadLatestReleaseJson();
		CheckForUpdates();
	}

	private string BuildWidgetControlsJson()
	{
		try
		{
			return ((DispatcherObject)this).Dispatcher.Invoke<string>((Func<string>)BuildWidgetControlsJsonOnUi, (DispatcherPriority)9, CancellationToken.None, TimeSpan.FromMilliseconds(300.0));
		}
		catch
		{
			return null;
		}
	}

	private static string WidgetJsonString(string s)
	{
		if (s == null)
		{
			return "\"\"";
		}
		StringBuilder stringBuilder = new StringBuilder("\"");
		foreach (char c in s)
		{
			switch (c)
			{
			case '"':
				stringBuilder.Append("\\\"");
				continue;
			case '\\':
				stringBuilder.Append("\\\\");
				continue;
			case '\n':
				stringBuilder.Append("\\n");
				continue;
			case '\r':
				stringBuilder.Append("\\r");
				continue;
			case '\t':
				stringBuilder.Append("\\t");
				continue;
			}
			if (c < ' ')
			{
				int num = c;
				stringBuilder.Append("\\u" + num.ToString("x4"));
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.Append('"').ToString();
	}

	private static string WidgetComboItemsJson(ComboBox combo)
	{
		StringBuilder stringBuilder = new StringBuilder("[");
		for (int i = 0; i < combo.Items.Count; i++)
		{
			object obj = combo.Items[i];
			string s = ((obj is ComboBoxItem comboBoxItem) ? Convert.ToString(comboBoxItem.Content, CultureInfo.InvariantCulture) : Convert.ToString(obj, CultureInfo.InvariantCulture));
			if (i > 0)
			{
				stringBuilder.Append(',');
			}
			stringBuilder.Append(WidgetJsonString(s));
		}
		return stringBuilder.Append(']').ToString();
	}

	private string BuildWidgetControlsJsonOnUi()
	{
		if (serviceModel == null)
		{
			return null;
		}
		bool flag = serviceModel.ActiveGraphicsMode == "Man FG";
		ComboBox comboBox = (flag ? cbManFgMultiplier : cbTargetFPSType);
		int num = (serviceModel.AutoTargetFPS ? 1 : (serviceModel.FixedTargetFPS ? 2 : 0));
		string s = "";
		string text = txtTargetFPS.Text;
		if (!string.IsNullOrWhiteSpace(text) && float.TryParse(text, new RealInvariantFormat(text), out var result))
		{
			s = result.ToString("0.##", CultureInfo.InvariantCulture);
		}
		bool flag2 = cbTargetFPSOption.Background is SolidColorBrush solidColorBrush && solidColorBrush.Color == Colors.Orange;
		bool flag3 = comboBox.Background is SolidColorBrush solidColorBrush2 && solidColorBrush2.Color == Colors.Orange;
		bool flag4 = txtTargetFPS.Background is SolidColorBrush solidColorBrush3 && solidColorBrush3.Color == Colors.Orange;
		bool valueOrDefault = chkUseExpertOptions.IsChecked == true;
		bool valueOrDefault2 = chkLOGPlus.IsChecked == true;
		bool flag5 = chkLOGPlus.Visibility == Visibility.Visible;
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append('{');
		stringBuilder.Append("\"mode\":").Append(num.ToString(CultureInfo.InvariantCulture));
		stringBuilder.Append(",\"modeItems\":").Append(WidgetComboItemsJson(cbTargetFPSOption));
		stringBuilder.Append(",\"modeOrange\":").Append(flag2 ? "true" : "false");
		stringBuilder.Append(",\"targetList\":").Append(flag ? "\"man\"" : "\"auto\"");
		stringBuilder.Append(",\"targetItems\":").Append(WidgetComboItemsJson(comboBox));
		stringBuilder.Append(",\"targetIndex\":").Append(comboBox.SelectedIndex.ToString(CultureInfo.InvariantCulture));
		stringBuilder.Append(",\"targetOrange\":").Append(flag3 ? "true" : "false");
		stringBuilder.Append(",\"targetSwapIndex\":").Append(flag ? "0" : "7");
		stringBuilder.Append(",\"fps\":").Append(WidgetJsonString(s));
		stringBuilder.Append(",\"fpsText\":").Append(WidgetJsonString(text ?? ""));
		stringBuilder.Append(",\"fpsOrange\":").Append(flag4 ? "true" : "false");
		stringBuilder.Append(",\"fpsEditable\":").Append((num != 1) ? "true" : "false");
		stringBuilder.Append(",\"expert\":").Append(valueOrDefault ? "true" : "false");
		stringBuilder.Append(",\"logPlus\":").Append(valueOrDefault2 ? "true" : "false");
		stringBuilder.Append(",\"logPlusVisible\":").Append(flag5 ? "true" : "false");
		stringBuilder.Append(",\"generalCollapsed\":").Append(serviceModel.WidgetGeneralCollapsed ? "true" : "false");
		stringBuilder.Append(",\"autoHide\":").Append(serviceModel.WidgetAutoHide.ToString(CultureInfo.InvariantCulture));
		stringBuilder.Append('}');
		return stringBuilder.ToString();
	}

	private void WidgetToggleExpert()
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)(() =>
		{
			chkUseExpertOptions.IsChecked = !chkUseExpertOptions.IsChecked;
			chkUseExpertOptions_Click(null, null);
		}), Array.Empty<object>());
	}

	private void WidgetToggleLogPlus()
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)(() =>
		{
			chkLOGPlus.IsChecked = !chkLOGPlus.IsChecked;
			chkLOGPlus_Click(null, null);
		}), Array.Empty<object>());
	}

	private void WidgetSetTargetMode(int index)
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)(() =>
		{
			if (serviceModel != null && index >= 0 && index < cbTargetFPSOption.Items.Count)
			{
				cbTargetFPSOption.SelectedIndex = index;
			}
		}), Array.Empty<object>());
	}

	private void WidgetSetTarget(string request)
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)(() =>
		{
			if (serviceModel != null && !string.IsNullOrWhiteSpace(request))
			{
				string[] array = request.Split(':');
				if (array.Length == 2 && int.TryParse(array[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
				{
					bool flag = serviceModel.ActiveGraphicsMode == "Man FG";
					if (array[0] == "man" == flag)
					{
						ComboBox comboBox = (flag ? cbManFgMultiplier : cbTargetFPSType);
						if (result >= 0 && result < comboBox.Items.Count)
						{
							comboBox.SelectedIndex = result;
						}
					}
				}
			}
		}), Array.Empty<object>());
	}

	private void WidgetSetTargetFps(string text)
	{
		((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)(Action)(() =>
		{
			if (serviceModel != null && !serviceModel.AutoTargetFPS && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				if (result < 10f)
				{
					result = 10f;
				}
				if (result > 500f)
				{
					result = 500f;
				}
				result = (float)Math.Round(result * 2f) / 2f;
				txtTargetFPS.Text = Convert.ToString(result, CultureInfo.CurrentUICulture);
				TextBox_SetSetting(txtTargetFPS);
			}
		}), Array.Empty<object>());
	}

	private string GetLabelHexColor(Label lbl, string defaultHex = "#FFFFFF")
	{
		if (lbl?.Foreground is SolidColorBrush solidColorBrush)
		{
			return $"#{solidColorBrush.Color.R:X2}{solidColorBrush.Color.G:X2}{solidColorBrush.Color.B:X2}";
		}
		return defaultHex;
	}

	private string SanitizeText(object content)
	{
		if (content == null)
		{
			return "";
		}
		string text = content.ToString();
		if (string.IsNullOrWhiteSpace(text))
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		string text2 = text;
		foreach (char c in text2)
		{
			if (c == '\n')
			{
				stringBuilder.Append(c);
			}
			else if (c >= ' ')
			{
				switch (c)
				{
				case '\u007f':
				case '\u0080':
				case '\u0081':
				case '\u0082':
				case '\u0083':
				case '\u0084':
				case '\u0085':
				case '\u0086':
				case '\u0087':
				case '\u0088':
				case '\u0089':
				case '\u008a':
				case '\u008b':
				case '\u008c':
				case '\u008d':
				case '\u008e':
				case '\u008f':
				case '\u0090':
				case '\u0091':
				case '\u0092':
				case '\u0093':
				case '\u0094':
				case '\u0095':
				case '\u0096':
				case '\u0097':
				case '\u0098':
				case '\u0099':
				case '\u009a':
				case '\u009b':
				case '\u009c':
				case '\u009d':
				case '\u009e':
				case '\u009f':
					continue;
				}
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString().Trim();
	}

	private object Field(string text, string color)
	{
		return new
		{
			text = SanitizeText(text),
			color = color
		};
	}

	private string GetTelemetryJson()
	{
		return ((DispatcherObject)this).Dispatcher.Invoke<string>((Func<string>)(() =>
		{
			bool valueOrDefault = chkDarkMode.IsChecked == true;
			string bg = (valueOrDefault ? "#18191B" : "#CECECE");
			string inputBg = (valueOrDefault ? "#2B2D33" : "#E0E0E0");
			string btnBg = (valueOrDefault ? "#2B2D33" : "#C4C6CA");
			string text = (valueOrDefault ? "#F0F0F0" : "#000000");
			string border = (valueOrDefault ? "#50555E" : "#8E929A");
			return JsonSerializer.Serialize(new
			{
				theme = new
				{
					bg = bg,
					inputBg = inputBg,
					btnBg = btnBg,
					text = text,
					border = border,
					isDark = valueOrDefault
				},
				widgetTitle = "AutoFPS Widget v1.6.0",
				widgetHotkey = serviceModel.WidgetHotkey,
				hideTitleBar = serviceModel.hideTitleBar,
				connMSFS = Field(lblConnStatMSFS.Content?.ToString(), GetLabelHexColor(lblConnStatMSFS, text)),
				connSimConnect = Field(lblConnStatSimConnect.Content?.ToString(), GetLabelHexColor(lblConnStatSimConnect, text)),
				connSession = Field(lblConnStatSession.Content?.ToString(), GetLabelHexColor(lblConnStatSession, text)),
				lblFps = Field(lblSimFPSType.Content?.ToString(), GetLabelHexColor(lblSimFPSType, text)),
				valFps = Field(lblSimFPS.Content?.ToString(), GetLabelHexColor(lblSimFPS, text)),
				fpsIcon = activeFpsIcon,
				simHeaderFS = (imgSimHeaderIcon.Visibility == Visibility.Visible),
				simHeaderDog = (imgSimHeaderIcon2.Visibility == Visibility.Visible),
				lblTlod = Field(lblSimTLODlbl.Content?.ToString(), GetLabelHexColor(lblSimTLODlbl, text)),
				valTlod = Field(lblSimTLOD.Content?.ToString(), GetLabelHexColor(lblSimTLOD, text)),
				lblOlod = Field(lblSimOLODlbl.Content?.ToString(), GetLabelHexColor(lblSimOLODlbl, text)),
				valOlod = Field(lblSimOLOD.Content?.ToString(), GetLabelHexColor(lblSimOLOD, text)),
				lblAgl = Field("AGL:", text),
				valAgl = Field(lblPlaneAGL.Content?.ToString(), GetLabelHexColor(lblPlaneAGL, text)),
				lblFpm = Field("FPM:", text),
				valFpm = Field(lblPlaneVS.Content?.ToString(), GetLabelHexColor(lblPlaneVS, text)),
				lblClouds = Field(lblSimReduce.Content?.ToString(), GetLabelHexColor(lblSimReduce, text)),
				valClouds = Field(lblSimReduce2.Content?.ToString(), GetLabelHexColor(lblSimReduce2, text)),
				statusMessage = Field(lblStatusMessage.Content?.ToString(), GetLabelHexColor(lblStatusMessage, text)),
				profiles = (from object i in cbActiveProfile.Items
					select (!(i is ComboBoxItem { Content: var content })) ? i?.ToString() : content?.ToString()).ToArray(),
				activeProfileIndex = cbActiveProfile.SelectedIndex,
				resetVisible = (btnReset.Visibility == Visibility.Visible),
				expertVisible = (chkUseExpertOptions.Visibility == Visibility.Visible)
			}, JsonOpts);
		}));
	}

	private void SetWindowTitle()
	{
		// Fork: short title; version and credits are on Settings > About. Only states worth noticing are appended.
		Title = "AutoFPS" + (serviceModel.MonitorMode ? " · Monitor Mode" : "") + (App.IsRunningAsAdmin() ? " · Admin" : "");
	}

	public void CheckForUpdates()
	{
		bool flag = false;
		bool flag2 = false;
		string text = FindReleaseDownloadUrl("releases.json", wantPrerelease: false);
		string text2 = FindReleaseDownloadUrl("releases.json", wantPrerelease: true);
		updateAvailable = false;
		lblStatusMessage.Content = "";
		if (serviceModel.UpdateOption != 2 && (serviceModel.UseExpertOptions || serviceModel.UpdateChannel > 0))
		{
			cbUpdateChannel.Visibility = Visibility.Visible;
		}
		else
		{
			cbUpdateChannel.Visibility = Visibility.Collapsed;
		}
		cbUpdateOption.SelectedIndex = ((serviceModel.UpdateOption != 0 || !serviceModel.SilentUpdates) ? (serviceModel.UpdateOption + 1) : 0);
		if (serviceModel.TestUpdates && text2 != null && !text2.Contains("0.5.5.0"))
		{
			string value = Regex.Match(text2, "v([0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+-(test|RC|exp)(\\d+))").Groups[1].Value;
			string uriString = global::AppConfig.Config.RepoUrl + "/releases/tag/v" +Regex.Match(text2, "v([0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+-(test|RC|exp))").Groups[1].Value;
			if (lblappUrlTestVersion.Inlines.FirstInline is Hyperlink hyperlink && Uri.TryCreate(uriString, UriKind.Absolute, out Uri result))
			{
				hyperlink.NavigateUri = result;
			}
			int num;
			if (value.Contains("-exp"))
			{
				num = 3;
			}
			else
			{
				num = (value.Contains("-test") ? 2 : (value.Contains("-RC") ? 1 : 0));
			}
			flag2 = num <= serviceModel.UpdateChannel;
			string text3 = $"Latest test version available: {value}. Build channel: {num switch
			{
				0 => "Release", 
				1 => "RC", 
				2 => "Test", 
				3 => "Experimental", 
				_ => "Unknown", 
			}}. {(flag2 ? "Allowed" : "Not allowed")} under user update channel ";
			string text4 = serviceModel.UpdateChannel switch
			{
				0 => "Release only", 
				1 => "RC+", 
				2 => "Test+", 
				3 => "Exp+", 
				_ => "Unknown", 
			};
			string text5;
			if (serviceModel.UpdateOption != 0)
			{
				text5 = ((serviceModel.UpdateOption == 1) ? "Show Updates" : "Mandatory Only");
			}
			else
			{
				text5 = (serviceModel.SilentUpdates ? "Silent Updates" : "Prompted Updates");
			}
			string text6 = serviceModel.UpdateChannel switch
			{
				0 => "Release only", 
				1 => "RC+", 
				2 => "Test+", 
				3 => "Exp+", 
				_ => "Unknown", 
			};
			Logger.Log(LogLevel.Information, "MainWindow:CheckForUpdates", text3 + text4 + ". Update option: " + text5 + " + Update Channel: " + text6);
			if (flag2)
			{
				if (text2.Contains("m-"))
				{
					lblStatusMessage.Content = "New mandatory test version available";
					lblStatusMessage.Foreground = new SolidColorBrush(Colors.Red);
					AutoUpdate(Regex.Match(text2, "v([0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+-(test|RC|exp)(\\d+))").Groups[1].Value, Mandatory: true);
				}
				else if (serviceModel.UpdateOption == 0)
				{
					lblStatusMessage.Content = "New " + value + " available";
					lblStatusMessage.Foreground = new SolidColorBrush(Colors.Green);
					AutoUpdate(value);
				}
				else if (serviceModel.UpdateOption == 1)
				{
					updateAvailable = true;
					lblStatusMessage.Content = "New " + value + " available";
					lblStatusMessage.Foreground = new SolidColorBrush(Colors.Green);
					lblappUrl.Visibility = Visibility.Collapsed;
					lblappUrlTestVersion.Visibility = Visibility.Visible;
				}
				else
				{
					lblStatusMessage.Content = "Waiting for flight session to start";
				}
			}
			else
			{
				text6 = value;
				text5 = serviceModel.UpdateChannel switch
				{
					0 => "Release only", 
					1 => "RC+", 
					2 => "Test+", 
					3 => "Exp+", 
					_ => "Unknown", 
				};
				Logger.Log(LogLevel.Information, "MainWindow:CheckForUpdates", "Test update " + text6 + " skipped as it is below user update channel " + text5 + ".");
			}
		}
		string text7;
		string text9;
		if (!flag2 && int.TryParse(assemblyVersion.Replace(".", ""), CultureInfo.InvariantCulture, out var result2) && text != null && text.Contains("/releases/"))
		{
			if (text.Contains("m-"))
			{
				flag = true;
			}
			if (result2 < 1000)
			{
				result2 = result2 / 10 * 100 + result2 % 10;
			}
			text7 = Regex.Match(text, "v([^/]+)")?.Groups[1].Value;
			string text5 = text7;
			string text6;
			if (serviceModel.UpdateOption != 0)
			{
				text6 = ((serviceModel.UpdateOption == 1) ? "Show Updates" : "Mandatory Only");
			}
			else
			{
				text6 = (serviceModel.SilentUpdates ? "Silent Updates" : "Prompted Updates");
			}
			string text4 = serviceModel.UpdateChannel switch
			{
				0 => "Release only", 
				1 => "RC+", 
				2 => "Test+", 
				3 => "Exp+", 
				_ => "Unknown", 
			};
			Logger.Log(LogLevel.Information, "MainWindow:CheckForUpdates", "Latest release version available: " + text5 + ". Update option: " + text6 + " + Update Channel: " + text4);
			if (!string.IsNullOrEmpty(text7) && serviceModel.UpdateOption != 2)
			{
				int num2 = text7.LastIndexOf('.');
				if (num2 >= 0)
				{
					text4 = text7;
					int num3 = num2 + 1;
					if (text4.Substring(num3, text4.Length - num3).Length == 1)
					{
						text4 = text7;
						string text8 = text4.Substring(0, text4.Length - 1);
						text4 = text7;
						num3 = num2 + 1;
						text9 = text8 + "0" + text4.Substring(num3, text4.Length - num3);
						goto IL_0714;
					}
				}
				text9 = text7;
				goto IL_0714;
			}
			lblStatusMessage.Content = "";
		}
		goto IL_08b5;
		IL_08b5:
		serviceModel.InitialCheckForUpdatesComplete = true;
		return;
		IL_0714:
		if (int.TryParse(text9.Replace(".", ""), CultureInfo.InvariantCulture, out var result3))
		{
			if (result3 < result2)
			{
				lblStatusMessage.Content = "Preview release version installed";
				lblStatusMessage.Foreground = new SolidColorBrush(Colors.Blue);
			}
			else if (result3 > result2 || serviceModel.AutoRevertToReleaseVersion)
			{
				if (flag)
				{
					lblStatusMessage.Content = "New mandatory version available";
					lblStatusMessage.Foreground = new SolidColorBrush(Colors.Red);
					AutoUpdate(text7, Mandatory: true);
				}
				else if (serviceModel.UpdateOption == 0 || serviceModel.AutoRevertToReleaseVersion)
				{
					lblStatusMessage.Content = (serviceModel.AutoRevertToReleaseVersion ? "Release " : "New ") + "version " + text7 + " available";
					lblStatusMessage.Foreground = new SolidColorBrush(Colors.Green);
					AutoUpdate(text7);
				}
				else if (serviceModel.UpdateOption == 1)
				{
					updateAvailable = true;
					lblStatusMessage.Content = (serviceModel.AutoRevertToReleaseVersion ? "Release " : "New ") + "version " + text7 + " available";
					lblStatusMessage.Foreground = new SolidColorBrush(Colors.Green);
					lblappUrl.Visibility = Visibility.Visible;
				}
				else
				{
					lblStatusMessage.Content = "Waiting for flight session to start";
				}
			}
		}
		goto IL_08b5;
	}

	private static bool DownloadLatestReleaseJson()
	{
		string url = global::AppConfig.Config.ReleasesApiUrl;
		string text = "releases.json";
		if (File.Exists(text))
		{
			DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(text);
			double totalSeconds = (DateTime.UtcNow - lastWriteTimeUtc).TotalSeconds;
			if (totalSeconds < 60.0)
			{
				Logger.Log(LogLevel.Information, "MainWindow:DownloadLatestReleaseJson", $"Using cached release data to check for updates (age: {totalSeconds:F1}s)");
				return true;
			}
		}
		if (DownloadJsonFile(url, text))
		{
			Logger.Log(LogLevel.Information, "MainWindow:DownloadLatestReleaseJson", "Downloaded fresh release data to check for updates.");
			return true;
		}
		Logger.Log(LogLevel.Warning, "MainWindow:DownloadLatestReleaseJson", "Failed to download release data.");
		return false;
	}

	private static bool DownloadJsonFile(string url, string filePath)
	{
		try
		{
			using HttpClient httpClient = new HttpClient();
			httpClient.DefaultRequestHeaders.Add("User-Agent", "C# App");
			HttpResponseMessage result = httpClient.GetAsync(url).GetAwaiter().GetResult();
			result.EnsureSuccessStatusCode();
			string result2 = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			File.WriteAllText(filePath, result2);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MainWindow:DownloadJsonFile", $"Exception {ex}: {ex.Message}");
			return false;
		}
	}

	private static string FindReleaseDownloadUrl(string filePath, bool wantPrerelease)
	{
		if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0L)
		{
			return null;
		}
		foreach (JObject item in JArray.Parse(File.ReadAllText(filePath)))
		{
			if ((bool)item["prerelease"] == wantPrerelease)
			{
				JArray jArray = (JArray)item["assets"];
				if (jArray.Count > 0)
				{
					return (string?)jArray[0]["browser_download_url"];
				}
			}
		}
		return null;
	}

	private string GetVersionLabel(string version)
	{
		if (version.Contains("test"))
		{
			return "test ";
		}
		if (version.Contains("RC"))
		{
			return "release candidate ";
		}
		if (version.Contains("exp"))
		{
			return "experimental ";
		}
		return "release ";
	}

	public static string GetWidgetVersionFromReleasesJson(string appVersionStr)
	{
		try
		{
			string path = "releases.json";
			if (!File.Exists(path))
			{
				path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "releases.json");
			}
			if (!File.Exists(path))
			{
				return null;
			}
			string b = appName + "-installer-v" + appVersionStr + ".exe";
			string b2 = "v" + appVersionStr;
			using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(path));
			foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
			{
				string a = (item.TryGetProperty("tag_name", out var value) ? value.GetString() : "");
				string input = (item.TryGetProperty("body", out var value2) ? value2.GetString() : "");
				bool flag = false;
				if (item.TryGetProperty("assets", out var value3))
				{
					foreach (JsonElement item2 in value3.EnumerateArray())
					{
						if (string.Equals(item2.TryGetProperty("name", out var value4) ? value4.GetString() : "", b, StringComparison.OrdinalIgnoreCase))
						{
							flag = true;
							break;
						}
					}
				}
				if (!flag && (string.Equals(a, b2, StringComparison.OrdinalIgnoreCase) || string.Equals(a, appVersionStr, StringComparison.OrdinalIgnoreCase)))
				{
					flag = true;
				}
				if (flag)
				{
					Match match = Regex.Match(input, "MSFS\\s+Widget\\s+Version:\\s*([0-9]+\\.[0-9]+(\\.[0-9]+)?)", RegexOptions.IgnoreCase);
					if (match.Success && match.Groups.Count >= 2)
					{
						return match.Groups[1].Value.Trim();
					}
					break;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Debug, "MainWindow:GetWidgetVersion", "Exception reading releases.json: " + ex.Message);
		}
		return null;
	}

	public static bool IsWidgetUpdateRequiredForSim(string targetWidgetVersion, bool isMSFS2024)
	{
		string installedWidgetVersion = GetInstalledWidgetVersion(isMSFS2024);
		if (string.IsNullOrEmpty(installedWidgetVersion))
		{
			return false;
		}
		return !string.Equals(installedWidgetVersion, targetWidgetVersion.Trim(), StringComparison.OrdinalIgnoreCase);
	}

	public static string GetCommunityPathFromInstallLog(bool isMSFS2024)
	{
		try
		{
			string path = Path.Combine(App.appDir, "Install.log");
			if (File.Exists(path))
			{
				string text = (isMSFS2024 ? "MSFS 2024 Community: " : "MSFS 2020 Community: ");
				string[] array = File.ReadAllLines(path);
				foreach (string text2 in array)
				{
					if (text2.StartsWith(text, StringComparison.OrdinalIgnoreCase))
					{
						string text3 = text2.Substring(text.Length).Trim();
						if (Directory.Exists(text3))
						{
							return text3;
						}
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static string GetInstalledWidgetVersion(string communityPath)
	{
		try
		{
			if (string.IsNullOrEmpty(communityPath) || !Directory.Exists(communityPath))
			{
				return null;
			}
			string path = Path.Combine(communityPath, "autofps-html-widget-panel", "manifest.json");
			if (!File.Exists(path))
			{
				return null;
			}
			Match match = Regex.Match(File.ReadAllText(path), "\\\"package_version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
			if (match.Success && match.Groups.Count >= 2)
			{
				return match.Groups[1].Value.Trim();
			}
			return "unknown";
		}
		catch
		{
			return null;
		}
	}

	public static string GetInstalledWidgetVersion(bool isMSFS2024)
	{
		return GetInstalledWidgetVersion(GetCommunityPathFromUserCfg(isMSFS2024) ?? GetCommunityPathFromInstallLog(isMSFS2024));
	}

	public static string GetCommunityPathFromUserCfg(bool isMSFS2024)
	{
		try
		{
			string text = (isMSFS2024 ? App.msConfigStore2024 : App.msConfigStore2020);
			string text2 = (isMSFS2024 ? App.msConfigSteam2024 : App.msConfigSteam2020);
			string text3;
			if (File.Exists(text))
			{
				text3 = text;
			}
			else
			{
				text3 = (File.Exists(text2) ? text2 : null);
			}
			if (text3 == null)
			{
				return null;
			}
			string[] array = File.ReadAllLines(text3);
			foreach (string text4 in array)
			{
				if (text4.StartsWith("InstalledPackagesPath ", StringComparison.OrdinalIgnoreCase))
				{
					string text5 = text4.Replace("\"", "").Substring("InstalledPackagesPath ".Length) + "\\Community";
					return Directory.Exists(text5) ? text5 : null;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	// Fork: a "mandatory" update the user declined is not offered again during this session.
	private static string declinedMandatoryUpdateVersion;

	public void AutoUpdate(string AppVersionStr, bool Mandatory = false, bool UpdateForCompat = false, bool isExplicit = false)
	{
		AppVersionStr = AppVersionStr.TrimStart('v', 'V');
		if (Mandatory && declinedMandatoryUpdateVersion == AppVersionStr)
		{
			return;
		}
		if (!serviceModel.deferredUpdate | Mandatory | UpdateForCompat)
		{
			WindowState = WindowState.Normal;
			if (serviceModel.RememberWindowPos)
			{
				Top = serviceModel.windowTop;
				Left = serviceModel.windowLeft;
			}
			Show();
			serviceModel.AutoUpdateInProgress = true;
			if (serviceModel.AutoRevertToReleaseVersion)
			{
				Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "Auto revert to release version enabled.");
			}
			else
			{
				Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "New app version available for auto update.");
			}
			string versionLabel = GetVersionLabel(AppVersionStr);
			if (Mandatory)
			{
				// Fork: mandatory updates are never installed without explicit user consent.
				if (MessageBox.Show(this, "New mandatory " + appName + " " + versionLabel + "version " + AppVersionStr + " is available.\n\nDownload and install it now?", "New mandatory " + appName + " " + versionLabel + "version", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
				{
					declinedMandatoryUpdateVersion = AppVersionStr;
					Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "User declined mandatory " + appName + " " + versionLabel + "version " + AppVersionStr + ". It will not be offered again this session.");
					if (!serviceModel.windowIsVisible)
					{
						if (serviceModel.UseSysTrayIcon)
						{
							Hide();
						}
						else
						{
							WindowState = WindowState.Minimized;
						}
					}
					serviceModel.AutoUpdateInProgress = false;
					return;
				}
				Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "User agreed to install mandatory " + appName + " " + versionLabel + "version " + AppVersionStr + ".");
			}
			else
			{
				string text = "Auto " + (serviceModel.AutoRevertToReleaseVersion ? "revert to " : "install new ") + appName + " " + versionLabel + "version " + AppVersionStr;
				string caption = (UpdateForCompat ? "MSFS compatibility test failed - app disabled." : ((serviceModel.AutoRevertToReleaseVersion ? "Reversionary " : "New ") + appName + " " + versionLabel + "version available"));
				int num = -1;
				if (UpdateForCompat)
				{
					num = ParseMsfsMajorMinor(serviceModel.DetectedSimVersion).Minor;
				}
				string text2;
				if (isExplicit)
				{
					text2 = "confirmed";
				}
				else
				{
					text2 = ((UpdateForCompat && num >= 0 && num < 10) ? "possibly" : "likely");
				}
				bool flag = !string.IsNullOrEmpty(versionLabel);
				bool flag2 = !serviceModel.TestUpdates;
				string text3;
				if (UpdateForCompat)
				{
					text3 = (isExplicit ? "confirmed" : text2);
				}
				else
				{
					text3 = "";
				}
				string text4 = (((UpdateForCompat && !isExplicit) & flag2 & flag) ? "; prerelease, may be less stable" : "");
				if (UpdateForCompat)
				{
					text = text + ", which is " + text3 + " compatible with MSFS version " + serviceModel.DetectedSimVersion + text4;
				}
				text += "?";
				// Fork: only the user's own "Silent Updates" choice skips the prompt; compatibility
				// updates (even explicitly compatible ones) always ask first.
				if (serviceModel.SilentUpdates && !UpdateForCompat && !serviceModel.AutoRevertToReleaseVersion)
				{
					Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "User silently agreed to auto update app to " + AppVersionStr + ".");
					serviceModel.AutoRevertToReleaseVersion = false;
				}
				else
				{
					if (MessageBox.Show(this, text, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
					{
						if (UpdateForCompat)
						{
							Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "User declined auto updating of app to " + AppVersionStr + ", which is " + text3 + " compatible with MSFS version " + serviceModel.DetectedSimVersion + text4);
						}
						else if (serviceModel.AutoRevertToReleaseVersion)
						{
							Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "User declined reversionary auto updating of app to " + AppVersionStr + ".");
							serviceModel.AutoRevertToReleaseVersion = false;
						}
						else
						{
							Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "User declined auto updating of app to " + AppVersionStr + ". Show Updates option automatically set.");
							serviceModel.UpdateOption = 1;
							cbUpdateOption.SelectedIndex = serviceModel.UpdateOption + 1;
							serviceModel.ConfigurationFileCommon.SetSetting("UpdateOption", 1.ToString());
							Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", 1.ToString());
						}
						if (!serviceModel.windowIsVisible)
						{
							if (serviceModel.UseSysTrayIcon)
							{
								Hide();
							}
							else
							{
								WindowState = WindowState.Minimized;
							}
						}
						serviceModel.AutoUpdateInProgress = false;
						CheckForUpdates();
						return;
					}
					if (UpdateForCompat)
					{
						Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "User agreed to auto update app to " + AppVersionStr + ", which is " + text3 + " compatible with MSFS version " + serviceModel.DetectedSimVersion + text4);
					}
					else
					{
						Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "User agreed to auto update app to " + AppVersionStr + ".");
					}
					serviceModel.AutoRevertToReleaseVersion = false;
				}
			}
			string widgetVersionFromReleasesJson = GetWidgetVersionFromReleasesJson(AppVersionStr);
			if (!string.IsNullOrEmpty(widgetVersionFromReleasesJson) && IPCManager.IsSimRunning() && (IsWidgetUpdateRequiredForSim(widgetVersionFromReleasesJson, isMSFS2024: false) || IsWidgetUpdateRequiredForSim(widgetVersionFromReleasesJson, isMSFS2024: true)))
			{
				serviceModel.deferredUpdateVersion = AppVersionStr;
				if (!(Mandatory | UpdateForCompat))
				{
					serviceModel.deferredUpdate = true;
					Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", $"In-Game Widget update to v{widgetVersionFromReleasesJson} detected while MSFS is running. Auto update to v{AppVersionStr} deferred until MSFS exits.");
					if (!serviceModel.SilentUpdates)
					{
						MessageBox.Show(this, $"This update includes an In-Game Widget change (v{widgetVersionFromReleasesJson}), which cannot be installed while MSFS is running.\n\nThe update to {appName} ({AppVersionStr}) will be deferred and will install automatically once MSFS has closed.", appName + " Update Deferred", MessageBoxButton.OK, MessageBoxImage.Asterisk);
					}
					if (!serviceModel.windowIsVisible)
					{
						if (serviceModel.UseSysTrayIcon)
						{
							Hide();
						}
						else
						{
							WindowState = WindowState.Minimized;
						}
					}
					serviceModel.AutoUpdateInProgress = false;
					return;
				}
				serviceModel.deferredUpdate = false;
				string value = (Mandatory ? "mandatory" : "compatibility");
				Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", $"In-Game Widget update (v{widgetVersionFromReleasesJson}) required for {appName} v{AppVersionStr} ({value}), but MSFS is running. Prompting user to close MSFS.");
				while (IPCManager.IsSimRunning())
				{
					if (MessageBox.Show(this, $"The {appName} ({AppVersionStr}) {value} update includes an In-Game Widget update (v{widgetVersionFromReleasesJson}), which cannot be installed while MSFS is running.\n\n" + "To install the update now, please exit MSFS and click 'Yes'.\n\nClicking 'No' will cancel the update and close " + appName + ".", appName + " Update - MSFS Must Be Closed", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
					{
						serviceModel.ServiceExited = true;
						Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", $"User declined to close MSFS. {appName} v{AppVersionStr} ({value}) update cancelled and application exiting.");
						_webServer?.Stop();
						Application.Current.Shutdown();
						return;
					}
				}
			}
		}
		else
		{
			serviceModel.deferredUpdate = false;
			Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "Executing deferred auto update to " + AppVersionStr + ".");
		}
		string installerName = appName + "-installer-v" + AppVersionStr + ".exe";
		string text5 = Path.Combine(KnownFolders.GetPath(KnownFolder.Downloads), installerName);
		string url = global::AppConfig.Config.RepoUrl + "/releases/download/" + (Mandatory ? "m-" : "") + "v" + (AppVersionStr.Contains("test") ? AppVersionStr.Substring(0, AppVersionStr.IndexOf("test") + 4) : (AppVersionStr.Contains("RC") ? AppVersionStr.Substring(0, AppVersionStr.IndexOf("RC") + 2) : (AppVersionStr.Contains("exp") ? AppVersionStr.Substring(0, AppVersionStr.IndexOf("exp") + 3) : AppVersionStr))) + "/" + installerName;
		// Fork: never run an installer that is already lying in Downloads; always fetch a fresh copy
		// from the configured repository and verify it against the release asset's SHA-256 digest.
		bool installerReady = false;
		try
		{
			if (File.Exists(text5))
			{
				File.Delete(text5);
			}
			installerReady = DownloadFile(url, text5) && VerifyInstallerDigest(text5, installerName);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MainWindow:AutoUpdate", $"Exception preparing installer {installerName}: {ex.Message}");
		}
		if (installerReady)
		{
			Process process = new Process();
			process.StartInfo.FileName = text5;
			process.StartInfo.UseShellExecute = true;
			process.StartInfo.Arguments = "-quickupdate";
			process.StartInfo.WorkingDirectory = KnownFolders.GetPath(KnownFolder.Downloads);
			try
			{
				if (!serviceModel.SilentUpdates)
				{
					serviceModel.ConfigurationFileCommon.SetSetting("windowIsVisible", "true");
				}
				process.Start();
				Logger.Log(LogLevel.Information, "MainWindow:AutoUpdate", "Auto update in progress.");
				serviceModel.ServiceExited = true;
				Application.Current.Shutdown();
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error, "MainWindow:AutoUpdate", $"Exception {ex}: {ex.Message}");
			}
		}
		else
		{
			Logger.Log(LogLevel.Error, "MainWindow:AutoUpdate", "Unable to download app update.");
		}
		if (!serviceModel.deferredUpdate && !serviceModel.windowIsVisible)
		{
			if (serviceModel.UseSysTrayIcon)
			{
				Hide();
			}
			else
			{
				WindowState = WindowState.Minimized;
			}
		}
		if (!serviceModel.ServiceExited)
		{
			serviceModel.AutoUpdateInProgress = false;
		}
	}

	private int MapTypeToChannel(string type)
	{
		return type switch
		{
			"exp" => 3, 
			"test" => 2, 
			"RC" => 1, 
			_ => 0, 
		};
	}

	private static Version ParseMsfsMajorMinor(string msfsVersion)
	{
		if (string.IsNullOrWhiteSpace(msfsVersion))
		{
			return new Version(0, 0);
		}
		string[] array = msfsVersion.Split('.');
		int major = 0;
		int minor = 0;
		if (array.Length != 0 && int.TryParse(array[0], out var result))
		{
			major = result;
		}
		if (array.Length > 1 && int.TryParse(array[1], out var result2))
		{
			minor = result2;
		}
		return new Version(major, minor);
	}

	private string FindCompatibleReleaseUrl(string filePath, string msfsVersion, out bool isExplicitMatch)
	{
		isExplicitMatch = false;
		string source = "MemoryManager:FindCompatibleReleaseUrl";
		JArray jArray = JArray.Parse(File.ReadAllText(filePath));
		List<(Version, int, string, bool, bool, HashSet<Version>)> list = new List<(Version, int, string, bool, bool, HashSet<Version>)>();
		foreach (JObject item6 in jArray)
		{
			JArray jArray2 = (JArray)item6["assets"];
			if (jArray2 == null || jArray2.Count == 0)
			{
				continue;
			}
			string? input = (string?)jArray2[0]["name"];
			string item = (string?)jArray2[0]["browser_download_url"];
			var (version, a, item2) = ParseVersion(input);
			if (version.Major == 0 && version.Minor == 0)
			{
				continue;
			}
			bool item3 = item6.Value<bool?>("prerelease") == true || string.Equals(a, "rc", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "test", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "exp", StringComparison.OrdinalIgnoreCase);
			bool item4 = false;
			HashSet<Version> hashSet = new HashSet<Version>();
			using (StringReader stringReader = new StringReader(((string?)item6["body"]) ?? string.Empty))
			{
				string text;
				while ((text = stringReader.ReadLine()) != null)
				{
					if (!text.Contains("MSFS Compatibility:", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					foreach (string item7 in ExtractCompatVersions(text))
					{
						Version item5 = ParseMsfsMajorMinor(item7);
						hashSet.Add(item5);
						if (string.Equals(item7, msfsVersion, StringComparison.OrdinalIgnoreCase))
						{
							item4 = true;
						}
					}
				}
			}
			list.Add((version, item2, item, item3, item4, hashSet));
		}
		List<(Version, int, string, bool, bool, HashSet<Version>)> list2 = list.Where(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.isCompatible).ToList();
		Logger.Log(LogLevel.Information, source, $"Found {list2.Count} app release(s) explicitly compatible with MSFS version {msfsVersion}");
		string text2;
		if (!serviceModel.TestUpdates)
		{
			text2 = PickBest(list2.Where(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => !c.isPre), "Step 1 (compatible: stable-first for release users without test updates)", preferPre: false) ?? PickBest(list2, "Step 1 (compatible: allow pre if no stable exists)", preferPre: true);
		}
		else
		{
			bool testUpdates = serviceModel.TestUpdates;
			text2 = PickBest(list2, "Step 1 (compatible: channel-preference for test/opted-in users)", testUpdates);
		}
		if (text2 != null)
		{
			isExplicitMatch = true;
			return text2;
		}
		Version msfsMajorMinor = ParseMsfsMajorMinor(msfsVersion);
		if (!serviceModel.TestUpdates)
		{
			List<(Version, int, string, bool, bool, HashSet<Version>)> list3 = list.Where(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.isPre).ToList().Where(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.declaredCompatMM.Any((Version mm) => mm.Major == msfsMajorMinor.Major && mm.Minor == msfsMajorMinor.Minor))
				.OrderByDescending(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.ver)
				.ThenByDescending(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.suffixNum)
				.ToList();
			if (list3.Count == 0)
			{
				Logger.Log(LogLevel.Information, source, "No explicitly compatible release and no prerelease declares matching major.minor; no fallback for release user without test updates.");
				return null;
			}
			(Version, int, string, bool, bool, HashSet<Version>) tuple2 = list3.First();
			if (!tuple2.Item6.Any((Version mm) => mm.Major == msfsMajorMinor.Major && mm.Minor >= 10))
			{
				Logger.Log(LogLevel.Information, source, $"Newest prerelease matching major.minor is not late-beta (minor < 10); skipping fallback for conservative release user. Candidate ver={tuple2.Item1}");
				return null;
			}
			string text3 = PickBest(list3, "Step 2 (conservative fallback: newest prerelease with declared late-beta major.minor)", preferPre: true);
			if (text3 != null)
			{
				isExplicitMatch = false;
				return text3;
			}
			Logger.Log(LogLevel.Information, source, "Conservative prerelease fallback found no selectable URL.");
			return null;
		}
		List<(Version, int, string, bool, bool, HashSet<Version>)> list4 = list.Where(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.isPre).ToList().Where(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.declaredCompatMM.Any((Version mm) => mm.Major == msfsMajorMinor.Major && mm.Minor == msfsMajorMinor.Minor))
			.ToList();
		if (list4.Count > 0)
		{
			string text4 = PickBest(list4, "Step 3 (fallback: test/rc/exp with declared major.minor match)", preferPre: true);
			if (text4 != null)
			{
				isExplicitMatch = false;
				return text4;
			}
		}
		List<(Version, int, string, bool, bool, HashSet<Version>)> list5 = list.Where(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => !c.isPre).ToList().Where(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.declaredCompatMM.Any((Version mm) => mm.Major == msfsMajorMinor.Major && mm.Minor == msfsMajorMinor.Minor))
			.ToList();
		if (list5.Count > 0)
		{
			string text5 = PickBest(list5, "Step 4 (fallback: stable with declared major.minor match)", preferPre: false);
			if (text5 != null)
			{
				isExplicitMatch = false;
				return text5;
			}
		}
		Logger.Log(LogLevel.Information, source, "No compatible or major.minor-matching fallback found; staying on current version.");
		return null;
		string PickBest(IEnumerable<(Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM)> set, string context, bool preferPre)
		{
			List<(Version, int, string, bool, bool, HashSet<Version>)> list6 = set.Where(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.isPre).ToList();
			List<(Version, int, string, bool, bool, HashSet<Version>)> list7 = set.Where(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => !c.isPre).ToList();
			(Version, int, string, bool, bool, HashSet<Version>)? tuple3 = null;
			if (preferPre && list6.Count > 0)
			{
				tuple3 = list6.OrderByDescending(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.ver).ThenByDescending(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.suffixNum).First();
			}
			else if (!preferPre && list7.Count > 0)
			{
				tuple3 = list7.OrderByDescending(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.ver).First();
			}
			else if (list6.Count > 0)
			{
				tuple3 = list6.OrderByDescending(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.ver).ThenByDescending(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.suffixNum).First();
			}
			else if (list7.Count > 0)
			{
				tuple3 = list7.OrderByDescending(((Version ver, int suffixNum, string downloadUrl, bool isPre, bool isCompatible, HashSet<Version> declaredCompatMM) c) => c.ver).First();
			}
			if (tuple3.HasValue)
			{
				Logger.Log(LogLevel.Information, source, $"Auto-update version selected from {context}: {tuple3.Value.Item1} {(tuple3.Value.Item4 ? "(prerelease)" : "(stable)")} suffix={tuple3.Value.Item2}");
			}
			return tuple3?.Item3;
		}
	}

	private static IEnumerable<string> ExtractCompatVersions(string line)
	{
		MatchCollection matchCollection = Regex.Matches(line, "\\d+\\.\\d+\\.\\d+\\.\\d+");
		foreach (Match item in matchCollection)
		{
			yield return item.Value;
		}
	}

	private static (Version baseVer, string suffix, int suffixNum) ParseVersion(string input)
	{
		Match match = Regex.Match(input, "v?(?<base>\\d+(\\.\\d+){2,3})(?:-(?<suffix>RC|test|exp)(?<num>\\d+))?", RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			return (baseVer: new Version(0, 0), suffix: string.Empty, suffixNum: 0);
		}
		Version item = new Version(match.Groups["base"].Value);
		string item2 = match.Groups["suffix"].Value.ToLowerInvariant();
		int item3 = (match.Groups["num"].Success ? int.Parse(match.Groups["num"].Value) : 0);
		return (baseVer: item, suffix: item2, suffixNum: item3);
	}

	private static int CompareAppVersions(string v1, string v2)
	{
		(Version baseVer, string suffix, int suffixNum) tuple = ParseVersion(v1);
		Version item = tuple.baseVer;
		string item2 = tuple.suffix;
		int item3 = tuple.suffixNum;
		(Version baseVer, string suffix, int suffixNum) tuple2 = ParseVersion(v2);
		Version item4 = tuple2.baseVer;
		string item5 = tuple2.suffix;
		int item6 = tuple2.suffixNum;
		int num = item.CompareTo(item4);
		if (num != 0)
		{
			return num;
		}
		int suffixRank = GetSuffixRank(item2);
		int suffixRank2 = GetSuffixRank(item5);
		if (suffixRank != suffixRank2)
		{
			return suffixRank.CompareTo(suffixRank2);
		}
		return item3.CompareTo(item6);
	}

	private static int GetSuffixRank(string suffix)
	{
		if (suffix == null || suffix.Length != 0)
		{
			return suffix switch
			{
				"rc" => 1, 
				"test" => 1, 
				"exp" => 1, 
				_ => 2, 
			};
		}
		return 0;
	}

	public static bool DownloadFile(string url, string file)
	{
		bool result = false;
		try
		{
			using HttpClient httpClient = new HttpClient();
			HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url);
			if (httpClient.Send(request).IsSuccessStatusCode)
			{
				byte[] result2 = httpClient.GetByteArrayAsync(url).Result;
				File.WriteAllBytes(file, result2);
				result = File.Exists(file);
			}
			else
			{
				MessageBox.Show("File does not exist at the given URL.", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
		}
		catch (HttpRequestException ex)
		{
			MessageBox.Show($"HttpRequestException '{ex.GetType()}' during DownloadFile", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		catch (Exception ex2)
		{
			MessageBox.Show($"Exception '{ex2.GetType()}' during DownloadFile", "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		return result;
	}

	// Fork: compares the downloaded installer with the "digest" (sha256:...) that the GitHub
	// releases API reports for the matching asset in releases.json. Deletes the file on failure.
	private static bool VerifyInstallerDigest(string file, string assetName)
	{
		string expected = null;
		try
		{
			if (File.Exists("releases.json"))
			{
				foreach (JObject release in JArray.Parse(File.ReadAllText("releases.json")))
				{
					if (release["assets"] is not JArray assets)
					{
						continue;
					}
					foreach (JToken asset in assets)
					{
						if (string.Equals((string?)asset["name"], assetName, StringComparison.OrdinalIgnoreCase))
						{
							string digest = (string?)asset["digest"];
							if (!string.IsNullOrEmpty(digest) && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
							{
								expected = digest.Substring(7);
							}
							break;
						}
					}
					if (expected != null)
					{
						break;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MainWindow:VerifyInstallerDigest", "Exception reading releases.json: " + ex.Message);
		}
		string actual;
		using (FileStream stream = File.OpenRead(file))
		{
			actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
		}
		if (expected != null && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
		{
			Logger.Log(LogLevel.Information, "MainWindow:VerifyInstallerDigest", $"Installer {assetName} SHA-256 verified: {actual}");
			return true;
		}
		string reason = (expected == null) ? "no SHA-256 digest is published for this release asset" : $"SHA-256 mismatch (expected {expected}, got {actual})";
		Logger.Log(LogLevel.Error, "MainWindow:VerifyInstallerDigest", $"Installer {assetName} rejected: {reason}.");
		try
		{
			File.Delete(file);
		}
		catch
		{
		}
		MessageBox.Show($"The downloaded update {assetName} could not be verified and was not installed:\n\n{reason}.", "Update Verification Failed", MessageBoxButton.OK, MessageBoxImage.Hand);
		return false;
	}

	public static string GetFinalRedirect(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return url;
		}
		int num = 8;
		string text = url;
		do
		{
			HttpWebResponse httpWebResponse = null;
			try
			{
				HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
				httpWebRequest.Method = "HEAD";
				httpWebRequest.AllowAutoRedirect = false;
				httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
				switch (httpWebResponse.StatusCode)
				{
				case HttpStatusCode.OK:
					return text;
				case HttpStatusCode.MovedPermanently:
				case HttpStatusCode.Found:
				case HttpStatusCode.SeeOther:
				case HttpStatusCode.TemporaryRedirect:
					text = httpWebResponse.Headers["Location"];
					if (text == null)
					{
						return url;
					}
					if (text.IndexOf("://", StringComparison.Ordinal) == -1)
					{
						text = new Uri(new Uri(url), text).ToString();
					}
					url = text;
					break;
				default:
					return text;
				}
			}
			catch (WebException)
			{
				return text;
			}
			catch (Exception ex2)
			{
				Logger.Log(LogLevel.Error, "MainWindow:GetFinalRedirect", $"Exception {ex2}: {ex2.Message}");
				return null;
			}
			finally
			{
				httpWebResponse?.Close();
			}
		}
		while (num-- > 0);
		return text;
	}

	public void SetTheme()
	{
		Uri source = (serviceModel.isDarkMode ? new Uri("Themes/ThemeDark.xaml", UriKind.Relative) : new Uri("Themes/ThemeLight.xaml", UriKind.Relative));
		ResourceDictionary item = new ResourceDictionary
		{
			Source = source
		};
		Resources.MergedDictionaries.Clear();
		Resources.MergedDictionaries.Add(item);
		ApplyFluentTheme();
	}

	protected void LoadSettings(bool noLogging = false)
	{
		SetTheme();
		chkDarkMode.IsChecked = serviceModel.isDarkMode;
		if ((serviceModel.WidgetInstalled2020 && !serviceModel.isMSFS2024) || (serviceModel.WidgetInstalled2024 && serviceModel.isMSFS2024))
		{
			lblConnStatWidget.Visibility = Visibility.Visible;
		}
		else
		{
			lblConnStatWidget.Visibility = Visibility.Collapsed;
		}
		if (serviceModel.UseExpertOptions || serviceModel.LogPlus)
		{
			chkUseExpertOptions.Content = "Expert";
			chkLOGPlus.Visibility = Visibility.Visible;
			chkLOGPlus.IsChecked = serviceModel.LogPlus;
		}
		else
		{
			chkUseExpertOptions.Content = "Expert Settings";
			chkLOGPlus.Visibility = Visibility.Collapsed;
		}
		if (serviceModel.SmtCores < serviceModel.AffinityPhysicalCoreThreshold)
		{
			chkPerfOptAffinity.ToolTip = "Not enough SMT cores for Physical Cores mode\n" + $"• Detected: {serviceModel.SmtCores} SMT cores, Required: ≥{serviceModel.AffinityPhysicalCoreThreshold}\n" + "• To stop the associated warning message from showing when enabling this feature, lower \n  'AffinityPhysicalCoreThreshold' in the common config file in the app's root directory\n";
		}
		else
		{
			SetAffinityTooltip();
		}
		SetPriorityTooltip();
		SetPowerPlanTooltip();
		CheckBox checkBox = chkVRAMPlus;
		bool? isChecked = (chkVRAMPlus2.IsChecked = serviceModel.VRAMPlusEnabled);
		checkBox.IsChecked = isChecked;
		chkVRAMPlus.Visibility = Visibility.Collapsed;
		if (serviceModel.isMSFS2024)
		{
			cbVRAMPlus.SelectedIndex = serviceModel.VRAMPlusSuite;
			stkpnlCloudQualityOptions.Visibility = Visibility.Collapsed;
			stkpnlCloudQualityOptions2.Visibility = Visibility.Collapsed;
			stkpnlCloudQualityOptions3.Visibility = Visibility.Collapsed;
			chkAutoReduceSettings.IsChecked = serviceModel.AutoReduceSettings[serviceModel.activeProfile];
			if (serviceModel.AutoTargetFPS)
			{
				chkAutoReduceSettings.IsEnabled = false;
			}
			else
			{
				chkAutoReduceSettings.IsEnabled = true;
			}
			if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 2 && !serviceModel.MinTLODExtra[serviceModel.activeProfile])
			{
				stkpnlAutoReduceSettings.Visibility = Visibility.Collapsed;
				stkpnlAutoReduceSettings2.Visibility = Visibility.Collapsed;
			}
			else if (serviceModel.AutoTargetFPS && serviceModel.VRAMPlusSuite < 2)
			{
				stkpnlAutoReduceSettings2.Visibility = Visibility.Collapsed;
			}
			else
			{
				stkpnlAutoReduceSettings.Visibility = Visibility.Visible;
				if (!serviceModel.AutoReduceSettings[serviceModel.activeProfile] && serviceModel.VRAMPlusSuite < 2)
				{
					stkpnlAutoReduceSettings2.Visibility = Visibility.Collapsed;
				}
				else
				{
					stkpnlAutoReduceSettings2.Visibility = Visibility.Visible;
				}
			}
			if (serviceModel.AutoReduceSettings[serviceModel.activeProfile] || serviceModel.VRAMPlusSuite >= 1)
			{
				chkReduceCloudQ.IsChecked = serviceModel.ReduceCloudQ[serviceModel.activeProfile];
				chkReduceTrees.IsChecked = serviceModel.ReduceTrees[serviceModel.activeProfile];
				chkReduceRTShadows.IsChecked = serviceModel.ReduceRTShadows[serviceModel.activeProfile];
				chkReduceTShadows.IsChecked = serviceModel.ReduceTShadows[serviceModel.activeProfile];
				chkReduceBldings.IsChecked = serviceModel.ReduceBldings[serviceModel.activeProfile];
				chkReduceDispMap.IsChecked = serviceModel.ReduceDispMap[serviceModel.activeProfile];
				chkReduceOSTPCache.IsChecked = serviceModel.ReduceOSTPCache[serviceModel.activeProfile];
				chkReducePlants.IsChecked = serviceModel.ReducePlants[serviceModel.activeProfile];
				chkReduceGrass.IsChecked = serviceModel.ReduceGrass[serviceModel.activeProfile];
				chkReducePkdAcQ.IsChecked = serviceModel.ReducePkdAcQ[serviceModel.activeProfile];
				chkReduceAcTrafQ.IsChecked = serviceModel.ReduceAcTrafQ[serviceModel.activeProfile];
				if (serviceModel.ReductionSettingsSuite[serviceModel.activeProfile] < 3 || (!serviceModel.AutoReduceSettings[serviceModel.activeProfile] && serviceModel.VRAMPlusSuite < 2))
				{
					chkReduceCloudQ.IsEnabled = false;
					chkReduceTrees.IsEnabled = false;
					chkReduceRTShadows.IsEnabled = false;
					chkReduceTShadows.IsEnabled = false;
					chkReduceBldings.IsEnabled = false;
					chkReduceDispMap.IsEnabled = false;
					chkReduceOSTPCache.IsEnabled = false;
					chkReducePlants.IsEnabled = false;
					chkReduceGrass.IsEnabled = false;
					chkReducePkdAcQ.IsEnabled = false;
					chkReduceAcTrafQ.IsEnabled = false;
				}
				else
				{
					chkReduceCloudQ.IsEnabled = true;
					chkReduceTrees.IsEnabled = true;
					chkReduceRTShadows.IsEnabled = true;
					chkReduceTShadows.IsEnabled = true;
					chkReduceBldings.IsEnabled = true;
					chkReduceDispMap.IsEnabled = true;
					chkReduceOSTPCache.IsEnabled = true;
					chkReducePlants.IsEnabled = true;
					chkReduceGrass.IsEnabled = true;
					chkReducePkdAcQ.IsEnabled = true;
					chkReduceAcTrafQ.IsEnabled = true;
				}
				cbReductionSettingsMaxLevels.SelectedIndex = serviceModel.ReductionSettingsMaxLevels[serviceModel.activeProfile] - 1;
				cbReductionSettingsFloor.SelectedIndex = serviceModel.ReductionSettingsFloor[serviceModel.activeProfile];
				cbReductionSettingsSuite.SelectedIndex = serviceModel.ReductionSettingsSuite[serviceModel.activeProfile];
				cbReductionSettingsRecoveryAlt.SelectedIndex = serviceModel.ReductionSettingsRecoveryAlt[serviceModel.activeProfile];
			}
			else
			{
				stkpnlAutoReduceSettings2.Visibility = Visibility.Collapsed;
				stkpnlSettingsReduction.Visibility = Visibility.Collapsed;
			}
		}
		else
		{
			stkpnlCloudQualityOptions.Visibility = Visibility.Visible;
			stkpnlCloudQualityOptions2.Visibility = Visibility.Visible;
			stkpnlCloudQualityOptions3.Visibility = Visibility.Visible;
			stkpnlAutoReduceSettings.Visibility = Visibility.Collapsed;
		}
		if (serviceModel.MinTLODExtra[serviceModel.activeProfile])
		{
			chkIncCloudQ.Visibility = Visibility.Visible;
			chkIncCloudQ.IsChecked = serviceModel.IncCloudQ[serviceModel.activeProfile];
		}
		else
		{
			chkIncCloudQ.Visibility = Visibility.Collapsed;
		}
		cbUpdateChannel.SelectedIndex = serviceModel.UpdateChannel;
		ComboBox comboBox = cbManFgMultiplier;
		int num;
		if (cbManFgMultiplier.Items.Count <= 0)
		{
			num = -1;
		}
		else
		{
			num = ((serviceModel.ManFgMultiplier == 1) ? 8 : Math.Min(Math.Max(serviceModel.ManFgMultiplier - 1, 0), cbManFgMultiplier.Items.Count - 1));
		}
		comboBox.SelectedIndex = num;
		if (serviceModel.RememberWindowPos)
		{
			Top = serviceModel.windowTop;
			Left = serviceModel.windowLeft;
		}
		// Fluent UI: pages replace the old double-click panel collapsing (windowPanelState), the custom
		// title bar replaces WindowStyle toggling, and the window keeps its own size (no SizeToContent).
		stkpnlGeneral.Visibility = Visibility.Visible;
		stkpnlExpertSettings.Visibility = ((!serviceModel.UseExpertOptions || serviceModel.MonitorMode) ? Visibility.Collapsed : Visibility.Visible);
		// The app's own title bar (with minimise/maximise/close) is always shown; hideTitleBar now only
		// applies to the in-game widget panel header.
		titleBar.Visibility = Visibility.Visible;
		if (mnuToggleTitleBar != null)
		{
			mnuToggleTitleBar.Header = (serviceModel.hideTitleBar ? "Show Widget Title Bar" : "Hide Widget Title Bar");
		}
		gpboxConnectionStatus.Header = "Connection status";
		if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3)
		{
			chkTLODExtraMtns.Visibility = Visibility.Collapsed;
			lblTLODExtraMtns.Visibility = Visibility.Collapsed;
			lblMaxTLODExtra.Visibility = Visibility.Visible;
			chkTLODBaseMinus.Visibility = Visibility.Visible;
			chkTLODBaseMinus.IsChecked = !serviceModel.TLODBaseMinus[serviceModel.activeProfile];
			chkMaxTLODExtra.Visibility = Visibility.Visible;
			chkMaxTLODExtra.IsChecked = serviceModel.MaxTLODExtra[serviceModel.activeProfile];
			if (serviceModel.MaxTLODExtra[serviceModel.activeProfile])
			{
				cbMaxTLODExtraCapMult.Visibility = Visibility.Visible;
			}
			else
			{
				cbMaxTLODExtraCapMult.Visibility = Visibility.Collapsed;
			}
			cbMaxTLODExtraCapMult.ToolTip = "Max with Extra: " + ((serviceModel.MaxTLOD[serviceModel.activeProfile] * serviceModel.MaxTLODExtraCapMult[serviceModel.activeProfile] > serviceModel.SimMaxLOD) ? (serviceModel.SimMaxLOD + " ⊤") : (serviceModel.MaxTLOD[serviceModel.activeProfile] * serviceModel.MaxTLODExtraCapMult[serviceModel.activeProfile]).ToString());
			cbMaxTLODExtraCapMult.SelectedIndex = (int)Math.Round(2.0 * ((double)serviceModel.MaxTLODExtraCapMult[serviceModel.activeProfile] - 1.5));
		}
		else
		{
			chkTLODExtraMtns.Visibility = Visibility.Visible;
			lblTLODExtraMtns.Visibility = Visibility.Visible;
			cbMaxTLODExtraCapMult.Visibility = Visibility.Collapsed;
			lblMaxTLODExtra.Visibility = Visibility.Collapsed;
			chkTLODBaseMinus.Visibility = Visibility.Collapsed;
			chkMaxTLODExtra.Visibility = Visibility.Collapsed;
		}
		if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2)
		{
			if (serviceModel.AutoTargetFPS)
			{
				cbTLODBaseOption.Visibility = Visibility.Collapsed;
			}
			else
			{
				cbTLODBaseOption.Visibility = Visibility.Visible;
			}
			cbTLODBaseOption.SelectedIndex = (serviceModel.FreeTLOD[serviceModel.activeProfile] ? 2 : (serviceModel.MinTLODExtra[serviceModel.activeProfile] ? 1 : 0));
			cbTargetFPSOption.Visibility = Visibility.Visible;
			cbTargetFPSOption.SelectedIndex = (serviceModel.AutoTargetFPS ? 1 : (serviceModel.FixedTargetFPS ? 2 : 0));
		}
		else
		{
			cbTargetFPSOption.Visibility = Visibility.Collapsed;
			cbTLODBaseOption.Visibility = Visibility.Collapsed;
		}
		if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 2 || serviceModel.AutoTargetFPS)
		{
			cbMinTLODExtraCapMult.Visibility = Visibility.Collapsed;
		}
		else
		{
			if (serviceModel.MinTLODExtra[serviceModel.activeProfile] && (serviceModel.TLODAutoMethod[serviceModel.activeProfile] != 3 || !serviceModel.TLODBaseMinus[serviceModel.activeProfile]) && !serviceModel.FreeTLOD[serviceModel.activeProfile])
			{
				cbMinTLODExtraCapMult.Visibility = Visibility.Visible;
			}
			else
			{
				cbMinTLODExtraCapMult.Visibility = Visibility.Collapsed;
			}
			cbMinTLODExtraCapMult.ToolTip = "Min with Extra: " + ((serviceModel.MinTLOD[serviceModel.activeProfile] * serviceModel.MinTLODExtraCapMult[serviceModel.activeProfile] > serviceModel.MaxTLOD[serviceModel.activeProfile] * ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3 && serviceModel.MaxTLODExtra[serviceModel.activeProfile]) ? serviceModel.MaxTLODExtraCapMult[serviceModel.activeProfile] : 1f)) ? (serviceModel.MaxTLOD[serviceModel.activeProfile] * ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3 && serviceModel.MaxTLODExtra[serviceModel.activeProfile]) ? serviceModel.MaxTLODExtraCapMult[serviceModel.activeProfile] : 1f) + " ⊤") : (serviceModel.MinTLOD[serviceModel.activeProfile] * serviceModel.MinTLODExtraCapMult[serviceModel.activeProfile]).ToString());
			cbMinTLODExtraCapMult.SelectedIndex = (int)Math.Round(2.0 * ((double)serviceModel.MinTLODExtraCapMult[serviceModel.activeProfile] - 1.5));
		}
		chkStartTLODExtraAtMax.Visibility = (((serviceModel.TLODAutoMethod[serviceModel.activeProfile] != 3 || serviceModel.TLODBaseMinus[serviceModel.activeProfile]) && (serviceModel.TLODAutoMethod[serviceModel.activeProfile] >= 2 || !serviceModel.MinTLODExtra[serviceModel.activeProfile])) ? Visibility.Collapsed : Visibility.Visible);
		chkStartTLODExtraAtMax.IsChecked = serviceModel.StartTLODExtraAtMax[serviceModel.activeProfile];
		if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] >= 2)
		{
			lblTLODAuto1.Content = "LOD Step";
			lblTLODMin.Content = "TLOD Base Min";
			lblTLODMax.Content = ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3 && serviceModel.MaxTLODExtra[serviceModel.activeProfile]) ? "TLOD Top Min" : "TLOD Top Max");
			chkTLODExtraMtns.ToolTip = "Extra TLOD Top in high terrain areas";
			txtAltTLODTop.ToolTip = "TLOD Top Min set above this altitude";
			if (serviceModel.MinTLODExtra[serviceModel.activeProfile] || serviceModel.DecCloudQ[serviceModel.activeProfile])
			{
				stkTargetFPS.Visibility = Visibility.Visible;
				txtTargetFPS.Visibility = Visibility.Visible;
			}
			else
			{
				stkTargetFPS.Visibility = Visibility.Hidden;
				txtTargetFPS.Visibility = Visibility.Hidden;
			}
			lblCloudMethod.Visibility = Visibility.Hidden;
			cbCloudMethod.Visibility = Visibility.Hidden;
			chkDecCloudQ.IsChecked = serviceModel.DecCloudQ[serviceModel.activeProfile];
			stkpnlCloudQualityOptions2.Visibility = Visibility.Collapsed;
			if (serviceModel.DecCloudQ[serviceModel.activeProfile])
			{
				stkpnlCloudQualityOptions3.Visibility = Visibility.Visible;
			}
			else
			{
				stkpnlCloudQualityOptions3.Visibility = Visibility.Collapsed;
			}
		}
		else
		{
			if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 0)
			{
				lblTLODAuto1.Content = "      Sens";
			}
			else
			{
				lblTLODAuto1.Content = "     Var %";
			}
			chkTLODExtraMtns.ToolTip = "Extra TLOD Top in high terrain areas";
			txtAltTLODTop.ToolTip = "TLOD Top Max allowable above this altitude";
			lblTLODMin.Content = "TLOD Base Min";
			lblTLODMax.Content = "TLOD Top Max";
			chkDecCloudQ.IsChecked = serviceModel.DecCloudQ[serviceModel.activeProfile];
			if (serviceModel.DecCloudQ[serviceModel.activeProfile])
			{
				lblCloudMethod.Visibility = Visibility.Visible;
				cbCloudMethod.Visibility = Visibility.Visible;
				cbCloudMethod.SelectedIndex = serviceModel.DecCloudQMethod[serviceModel.activeProfile];
				if (serviceModel.DecCloudQMethod[serviceModel.activeProfile] == 0)
				{
					stkpnlCloudQualityOptions2.Visibility = Visibility.Visible;
					stkpnlCloudQualityOptions3.Visibility = Visibility.Collapsed;
				}
				else
				{
					stkpnlCloudQualityOptions2.Visibility = Visibility.Collapsed;
					stkpnlCloudQualityOptions3.Visibility = Visibility.Visible;
				}
			}
			else
			{
				lblCloudMethod.Visibility = Visibility.Hidden;
				cbCloudMethod.Visibility = Visibility.Hidden;
				stkpnlCloudQualityOptions2.Visibility = Visibility.Collapsed;
				stkpnlCloudQualityOptions3.Visibility = Visibility.Collapsed;
			}
			stkTargetFPS.Visibility = Visibility.Visible;
			txtTargetFPS.Visibility = Visibility.Visible;
			if (!serviceModel.isMSFS2024)
			{
				if (serviceModel.UseExpertOptions && serviceModel.CloudRecoveryPlus[serviceModel.activeProfile] && serviceModel.MinTLOD[serviceModel.activeProfile] + serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile] > serviceModel.MaxTLOD[serviceModel.activeProfile])
				{
					serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("CloudRecoveryTLOD" + serviceModel.profileSuffix) : "CloudRecoveryTLOD_VFR", Convert.ToString((int)(2f * (serviceModel.MaxTLOD[serviceModel.activeProfile] - serviceModel.MinTLOD[serviceModel.activeProfile]) / 5f), CultureInfo.InvariantCulture));
				}
				else if (serviceModel.UseExpertOptions && !serviceModel.CloudRecoveryPlus[serviceModel.activeProfile] && (serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile] <= serviceModel.MinTLOD[serviceModel.activeProfile] || serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile] >= serviceModel.MaxTLOD[serviceModel.activeProfile]))
				{
					serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("CloudRecoveryTLOD" + serviceModel.profileSuffix) : "CloudRecoveryTLOD_VFR", Convert.ToString((int)(serviceModel.MinTLOD[serviceModel.activeProfile] + 2f * (serviceModel.MaxTLOD[serviceModel.activeProfile] - serviceModel.MinTLOD[serviceModel.activeProfile]) / 5f), CultureInfo.InvariantCulture));
				}
				txtCloudRecoveryTLOD.Text = Convert.ToString(serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile], CultureInfo.CurrentUICulture);
			}
		}
		txtMaxTLod.ToolTip = ((serviceModel.AltTLODTop[serviceModel.activeProfile] - serviceModel.AltTLODBase[serviceModel.activeProfile]) * 60f * (float)serviceModel.FPSTolerance[serviceModel.activeProfile] / (serviceModel.MaxTLOD[serviceModel.activeProfile] * ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3) ? serviceModel.MaxTLODExtraCapMult[serviceModel.activeProfile] : 1f) - serviceModel.MinTLOD[serviceModel.activeProfile])).ToString("F0") + " fpm max avg descent rate recommended";
		txtAltTLODTop.ToolTip = Math.Round(serviceModel.AltTLODBase[serviceModel.activeProfile] + (serviceModel.UseExpertOptions ? (serviceModel.ProfileName[serviceModel.FlightTypeExpert].ToUpperInvariant().Contains("VFR") ? serviceModel.defaultAvgDesRate_VFR : serviceModel.defaultAvgDesRate_IFR) : (serviceModel.FlightTypeIFR ? serviceModel.defaultAvgDesRate_IFR : serviceModel.defaultAvgDesRate_VFR)) * (serviceModel.MaxTLOD[serviceModel.activeProfile] * ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3) ? serviceModel.MaxTLODExtraCapMult[serviceModel.activeProfile] : 1f) - serviceModel.MinTLOD[serviceModel.activeProfile]) / (60f * (float)serviceModel.FPSTolerance[serviceModel.activeProfile])).ToString("F0") + " ft for recommended max " + (serviceModel.UseExpertOptions ? (serviceModel.ProfileName[serviceModel.FlightTypeExpert].ToUpperInvariant().Contains("VFR") ? serviceModel.defaultAvgDesRate_VFR : serviceModel.defaultAvgDesRate_IFR) : (serviceModel.FlightTypeIFR ? serviceModel.defaultAvgDesRate_IFR : serviceModel.defaultAvgDesRate_VFR)).ToString("F0") + " fpm descent";
		chkTLODExtraMtns.IsChecked = serviceModel.TLODExtraMtns[serviceModel.activeProfile];
		if (cbNightMode.Items.Count > 2 && cbNightMode.Items[2] is ComboBoxItem comboBoxItem)
		{
			comboBoxItem.Content = $"Clds-{serviceModel.NightCloudReduceLevels[serviceModel.activeProfile]}";
		}
		cbNightMode.SelectedIndex = serviceModel.NightMode[serviceModel.activeProfile];
		if (serviceModel.TLODExtraMtns[serviceModel.activeProfile])
		{
			stkpnlTLODExtraMtns.Visibility = Visibility.Visible;
			txtTLODExtraMtnsTriggerAlt.Text = Convert.ToString(serviceModel.TLODExtraMtnsTriggerAlt[serviceModel.activeProfile], CultureInfo.CurrentUICulture);
			txtTLODExtraMtnsAmount.Text = Convert.ToString(serviceModel.TLODExtraMtnsAmount[serviceModel.activeProfile], CultureInfo.CurrentUICulture);
		}
		else
		{
			stkpnlTLODExtraMtns.Visibility = Visibility.Collapsed;
			serviceModel.TLODExtraMtnsAmountResidual = 0f;
		}
		txtCloudDecreaseGPUPct.Text = Convert.ToString(serviceModel.CloudDecreaseGPUPct, CultureInfo.CurrentUICulture);
		txtCloudRecoverGPUPct.Text = Convert.ToString(serviceModel.CloudRecoverGPUPct, CultureInfo.CurrentUICulture);
		chkUseExpertOptions.IsChecked = serviceModel.UseExpertOptions;
		if (serviceModel.UseExpertOptions)
		{
			cbActiveProfile.Items.Clear();
			for (int i = 0; i < serviceModel.ProfileCount; i++)
			{
				cbActiveProfile.Items.Add(serviceModel.ProfileName[i]);
			}
			string[] profileName = serviceModel.ProfileName;
			List<(string, string)> list = new List<(string, string)>();
			foreach (string text in profileName)
			{
				string text2 = text + " VR";
				string text3 = (text.EndsWith(" VR") ? text.Substring(0, text.Length - 3) : null);
				if (!text.EndsWith(" VR") && Enumerable.Contains(profileName, text2))
				{
					list.Add((text, text2));
				}
				else if (text.EndsWith(" VR") && Enumerable.Contains(profileName, text3))
				{
					list.Add((text3, text));
				}
			}
			bool flag2 = list.Distinct().Count() > 1;
			int num2 = 1;
			foreach (var item3 in list.Distinct())
			{
				string item = item3.Item1;
				string item2 = item3.Item2;
				string text4 = (flag2 ? $" ↔{num2}" : " ↔");
				string[] array = new string[2] { item, item2 };
				foreach (string text5 in array)
				{
					for (int l = 0; l < cbActiveProfile.Items.Count; l++)
					{
						string text6 = cbActiveProfile.Items[l]?.ToString();
						string text7 = text6;
						int num3 = text6.LastIndexOf(" ↔");
						if (num3 != -1)
						{
							text7 = text6.Substring(0, num3);
						}
						if (text7 == text5)
						{
							cbActiveProfile.Items[l] = text7 + text4;
							break;
						}
					}
				}
				num2++;
			}
			for (int m = 0; m < cbActiveProfile.Items.Count; m++)
			{
				string text8 = cbActiveProfile.Items[m]?.ToString();
				if (text8.Contains(" ↔"))
				{
					string cleanText = text8.Substring(0, text8.LastIndexOf(" ↔"));
					if (!list.Any(((string, string) p) => p.Item1 == cleanText || p.Item2 == cleanText))
					{
						cbActiveProfile.Items[m] = cleanText;
					}
				}
			}
			cbActiveProfile.SelectedIndex = serviceModel.FlightTypeExpert;
			serviceModel.TLODReduceAfterGearDown = serviceModel.ProfileName[serviceModel.FlightTypeExpert].Contains("DelayLG");
			if (serviceModel.ExperimentalFeatures)
			{
				chkLOGPlus.IsEnabled = false;
			}
			else
			{
				chkLOGPlus.IsEnabled = true;
			}
			lblPerfOptEnabled.Visibility = Visibility.Visible;
			chkPerfOptEnabled.Visibility = Visibility.Visible;
			chkPerfOptEnabled.IsChecked = serviceModel.PerfOptEnabled;
			chkPerfOptAffinity.IsChecked = serviceModel.PerfOptAffinityEnabled;
			chkPerfOptPriority.IsChecked = serviceModel.PerfOptPriorityEnabled;
			chkPerfOptPowerPlan.IsChecked = serviceModel.PerfOptPowerPlanEnabled;
			cbPerfOptPriority.SelectedIndex = ((serviceModel.MSFSProcessPriority == "High") ? 1 : 0);
		}
		else
		{
			cbActiveProfile.Items.Clear();
			cbActiveProfile.Items.Add("IFR");
			cbActiveProfile.Items.Add("VFR");
			cbActiveProfile.SelectedIndex = ((!serviceModel.FlightTypeIFR) ? 1 : 0);
			if (!serviceModel.PerfOptEnabled)
			{
				lblPerfOptEnabled.Visibility = Visibility.Collapsed;
				chkPerfOptEnabled.Visibility = Visibility.Collapsed;
			}
		}
		if (serviceModel.AutoTargetFPS || (serviceModel.TLODAutoMethod[serviceModel.activeProfile] >= 2 && !serviceModel.MinTLODExtra[serviceModel.activeProfile] && !serviceModel.DecCloudQ[serviceModel.activeProfile]))
		{
			txtTargetFPS.IsEnabled = false;
		}
		else
		{
			txtTargetFPS.IsEnabled = true;
		}
		if (serviceModel.OnTop)
		{
			AutoFPS.Topmost = true;
		}
		else
		{
			AutoFPS.Topmost = false;
		}
		chkOnTop.IsChecked = serviceModel.OnTop;
		if (!serviceModel.UseExpertOptions)
		{
			serviceModel.activeProfile = 0;
		}
		else if (serviceModel.FlightTypeIFR)
		{
			serviceModel.activeProfile = 1;
		}
		else
		{
			serviceModel.activeProfile = 2;
		}
		if (serviceModel.AutoTargetFPS)
		{
			txtTargetFPS.Text = (serviceModel.ForceAutoFPSCal ? "auto" : Convert.ToString(serviceModel.TargetFPS, CultureInfo.CurrentUICulture));
		}
		else if (serviceModel.ActiveGraphicsMode == "VR")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_VR : serviceModel.TargetFPS_VR_VFR, CultureInfo.CurrentUICulture);
		}
		else if (serviceModel.ActiveGraphicsMode == "OFXR")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_OFXR : serviceModel.TargetFPS_OFXR_VFR, CultureInfo.CurrentUICulture);
		}
		else if (serviceModel.ActiveGraphicsMode == "VXR")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_VXR : serviceModel.TargetFPS_VXR_VFR, CultureInfo.CurrentUICulture);
		}
		else if (serviceModel.ActiveGraphicsMode == "FG")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_FG : serviceModel.TargetFPS_FG_VFR, CultureInfo.CurrentUICulture);
		}
		else if (serviceModel.ActiveGraphicsMode == "LSFG")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_LS : serviceModel.TargetFPS_LS_VFR, CultureInfo.CurrentUICulture);
		}
		else if (serviceModel.ActiveGraphicsMode == "FSR")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_FSR : serviceModel.TargetFPS_FSR_VFR, CultureInfo.CurrentUICulture);
		}
		else if (serviceModel.ActiveGraphicsMode == "MSM")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_MSM : serviceModel.TargetFPS_MSM_VFR, CultureInfo.CurrentUICulture);
		}
		else if (serviceModel.ActiveGraphicsMode == "Man FG")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_ManFG : serviceModel.TargetFPS_ManFG_VFR, CultureInfo.CurrentUICulture);
		}
		else
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_PC : serviceModel.TargetFPS_PC_VFR, CultureInfo.CurrentUICulture);
		}
		List<string> list2 = new List<string> { "NFR", "VR" };
		if (serviceModel.IsOFXRInstalled())
		{
			list2.Add("OFXR");
		}
		if (serviceModel.IsVXRInstalled())
		{
			list2.Add("VXR");
		}
		list2.Add("FG");
		if (serviceModel.IsLSFGInstalled())
		{
			list2.Add("LSFG");
		}
		list2.Add("FSR");
		if (serviceModel.IsMSMInstalled())
		{
			list2.Add("MSM");
		}
		list2.Add("Man FG");
		if (!list2.SequenceEqual(cbTargetFPSType.Items.Cast<string>()))
		{
			cbTargetFPSType.Items.Clear();
			foreach (string item4 in list2)
			{
				cbTargetFPSType.Items.Add(item4);
			}
		}
		if (!cbTargetFPSType.Items.Contains(serviceModel.ActiveGraphicsMode))
		{
			Logger.Log(LogLevel.Information, "MainWindow:LoadSettings", "ActiveGraphicsMode '" + serviceModel.ActiveGraphicsMode + "' is not available. Defaulting to NFR.");
			serviceModel.ActiveGraphicsMode = "NFR";
			serviceModel.ConfigurationFile.SetSetting("ActiveGraphicsMode", "NFR");
		}
		cbTargetFPSType.SelectedItem = serviceModel.ActiveGraphicsMode;
		if (serviceModel.ActiveGraphicsMode == "Man FG")
		{
			cbTargetFPSType.Visibility = Visibility.Collapsed;
			cbManFgMultiplier.Visibility = Visibility.Visible;
		}
		else
		{
			cbTargetFPSType.Visibility = Visibility.Visible;
			cbManFgMultiplier.Visibility = Visibility.Collapsed;
		}
		cbTargetFPSType.Background = ThemeInputBgBrush;
		serviceModel.ActiveGraphicsModeChanged = false;
		serviceModel.ReloadAppWindowSettings = false;
		txtFPSTolerance.Text = Convert.ToString(serviceModel.FPSTolerance[serviceModel.activeProfile], CultureInfo.CurrentUICulture);
		txtSpikeThresholdMultiplier.Text = serviceModel.SpikeThresholdMultiplier.ToString("0.0", CultureInfo.CurrentUICulture);
		cbTLODAutoMethod.SelectedIndex = serviceModel.TLODAutoMethod[serviceModel.activeProfile];
		txtMinTLod.Text = Convert.ToString(serviceModel.MinTLOD[serviceModel.activeProfile], CultureInfo.CurrentUICulture);
		txtMaxTLod.Text = Convert.ToString(serviceModel.MaxTLOD[serviceModel.activeProfile], CultureInfo.CurrentUICulture);
		chkCustomAutoOLOD.IsChecked = serviceModel.CustomAutoOLOD[serviceModel.activeProfile];
		chkOLODPanelHide.IsChecked = serviceModel.OLODPanelHide;
		if (serviceModel.OLODPanelHide)
		{
			stkpnlCustomAutoOLOD.Visibility = Visibility.Collapsed;
		}
		else
		{
			stkpnlCustomAutoOLOD.Visibility = Visibility.Visible;
		}
		txtOLODAtBase.Text = Convert.ToString(serviceModel.OLODAtBase[serviceModel.activeProfile], CultureInfo.CurrentUICulture);
		txtOLODAtTop.Text = Convert.ToString(serviceModel.OLODAtTop[serviceModel.activeProfile], CultureInfo.CurrentUICulture);
		txtAltOLODBase.Text = Convert.ToString(serviceModel.AltOLODBase[serviceModel.activeProfile], CultureInfo.CurrentUICulture);
		txtAltOLODTop.Text = Convert.ToString(serviceModel.AltOLODTop[serviceModel.activeProfile], CultureInfo.CurrentUICulture);
		txtAltTLODBase.Text = (serviceModel.FreeTLOD[serviceModel.activeProfile] ? "n/a" : Convert.ToString(serviceModel.AltTLODBase[serviceModel.activeProfile], CultureInfo.CurrentUICulture));
		txtAltTLODTop.Text = (serviceModel.FreeTLOD[serviceModel.activeProfile] ? "n/a" : Convert.ToString(serviceModel.AltTLODTop[serviceModel.activeProfile], CultureInfo.CurrentUICulture));
		if (serviceModel.FreeTLOD[serviceModel.activeProfile] && serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2)
		{
			txtAltTLODBase.IsEnabled = false;
			txtAltTLODTop.IsEnabled = false;
		}
		else
		{
			txtAltTLODBase.IsEnabled = true;
			txtAltTLODTop.IsEnabled = true;
		}
		chkCloudRecoveryPlus.IsChecked = serviceModel.CloudRecoveryPlus[serviceModel.activeProfile];
		if (!noLogging)
		{
			_ = serviceModel.LogPlus;
			serviceModel.SetActiveOLODs();
			if (serviceModel.isMSFS2024)
			{
				Logger.Log(LogLevel.Information, "MainWindow:LoadSettings", "MSFS:" + (serviceModel.isMSFS2024 ? "24" : "20") + " " + (serviceModel.UseExpertOptions ? "" : "Non-") + "Expert GfxMode:" + GetGfxModeName() + ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2) ? (" TgtFPSOpt:" + (serviceModel.AutoTargetFPS ? "Auto" : (serviceModel.FixedTargetFPS ? "Fixed" : "Manual"))) : "") + $" FltType:{serviceModel.ProfileName[serviceModel.FlightTypeExpert]} TgtFPS:{txtTargetFPS.Text} TLODMethod:{GetTLODMethodName()} Tol:{serviceModel.FPSTolerance[serviceModel.activeProfile]}" + ((serviceModel.UseExpertOptions && serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 0 && serviceModel.SpikeDetectEnabled) ? (" SDet SMult:" + serviceModel.SpikeThresholdMultiplier.ToString("0.0") + (serviceModel.SpikeTLODReductionEnabled ? " SProt" : "")) : "") + $" TMin:{serviceModel.MinTLOD[serviceModel.activeProfile]}" + (serviceModel.MinTLODExtra[serviceModel.activeProfile] ? $" TBXtra TBXtraMult:{Math.Round(serviceModel.MinTLODExtraCapMult[serviceModel.activeProfile], 1)}" : "") + (((serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3 && !serviceModel.TLODBaseMinus[serviceModel.activeProfile]) || (serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2 && serviceModel.MinTLODExtra[serviceModel.activeProfile])) ? ((serviceModel.StartTLODExtraAtMax[serviceModel.activeProfile] ? " TXtraStMax" : "") + (serviceModel.FreeTLOD[serviceModel.activeProfile] ? " FreeTLOD" : "")) : "") + ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3) ? ((serviceModel.TLODBaseMinus[serviceModel.activeProfile] ? " TBXtraFPSCap" : "") + (serviceModel.MaxTLODExtra[serviceModel.activeProfile] ? $" TTopXtra TTopXtraMult:{serviceModel.MaxTLODExtraCapMult[serviceModel.activeProfile]}" : "")) : "") + $" TMax:{serviceModel.MaxTLOD[serviceModel.activeProfile]}" + (serviceModel.TLODExtraMtns[serviceModel.activeProfile] ? $" TMtns TMtnsAltMin:{serviceModel.TLODExtraMtnsTriggerAlt[serviceModel.activeProfile]} TMtnsAmt:{serviceModel.TLODExtraMtnsAmount[serviceModel.activeProfile]}" : "") + ((serviceModel.NightMode[serviceModel.activeProfile] == 1) ? " TNight:Div2" : ((serviceModel.NightMode[serviceModel.activeProfile] == 2) ? $" TNight:Clds-{serviceModel.NightCloudReduceLevels[serviceModel.activeProfile]}" : ((serviceModel.NightMode[serviceModel.activeProfile] == 3) ? $" TNight:Both-{serviceModel.NightCloudReduceLevels[serviceModel.activeProfile]}" : ""))) + $" TLODBAlt:{serviceModel.AltTLODBase[serviceModel.activeProfile]} TLODTAlt:{serviceModel.AltTLODTop[serviceModel.activeProfile]}" + (serviceModel.CustomAutoOLOD[serviceModel.activeProfile] ? $" CustomOLOD OLODB:{serviceModel.activeOLODAtBase} OLODT:{serviceModel.activeOLODAtTop} OLODBAlt:{serviceModel.AltOLODBase[serviceModel.activeProfile]} OLODTAlt:{serviceModel.AltOLODTop[serviceModel.activeProfile]}" : "") + (serviceModel.AutoReduceSettings[serviceModel.activeProfile] ? $" RSet RLvls:{serviceModel.ReductionSettingsMaxLevels[serviceModel.activeProfile]} RFloor:{serviceModel.ReductionSettingsFloor[serviceModel.activeProfile]} RRecov:{serviceModel.ReductionSettingsRecoveryAlt[serviceModel.activeProfile]} RSuite:{serviceModel.ReductionSettingsSuite[serviceModel.activeProfile]}" : "") + ((serviceModel.MinTLODExtra[serviceModel.activeProfile] && serviceModel.IncCloudQ[serviceModel.activeProfile]) ? " IncCld" : "") + (serviceModel.VRAMPlusEnabled ? $" VRAM+:{((serviceModel.VRAMPlusSuite == 1) ? "Max" : "Set")} Vrec:{serviceModel.VRAMOverflowSettingsRecovery}% Vhld:{serviceModel.VRAMOverflowHoldTLOD}% Vred:{serviceModel.VRAMOverflowReduceTLOD}%" : "") + ((serviceModel.SimMaxLOD != 1000f) ? $" MaxLOD:{serviceModel.SimMaxLOD}" : ""));
			}
			else
			{
				Logger.Log(LogLevel.Information, "MainWindow:LoadSettings", "MSFS:" + (serviceModel.isMSFS2024 ? "24" : "20") + (serviceModel.UseExpertOptions ? " Expert" : "") + " GfxMode:" + GetGfxModeName() + ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2) ? (" TgtFPSOpt:" + (serviceModel.AutoTargetFPS ? "Auto" : (serviceModel.FixedTargetFPS ? "Fixed" : "Manual"))) : "") + $" FltType:{serviceModel.ProfileName[serviceModel.FlightTypeExpert]} TgtFPS:{txtTargetFPS.Text} TLODMethod:{GetTLODMethodName()} Tol:{serviceModel.FPSTolerance[serviceModel.activeProfile]}" + ((serviceModel.UseExpertOptions && serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 0 && serviceModel.SpikeDetectEnabled) ? (" SDet SMult:" + serviceModel.SpikeThresholdMultiplier.ToString("0.0") + (serviceModel.SpikeTLODReductionEnabled ? " SProt" : "")) : "") + $" TMin:{serviceModel.MinTLOD[serviceModel.activeProfile]}" + (serviceModel.MinTLODExtra[serviceModel.activeProfile] ? $" TBXtra TBXtraMult:{Math.Round(serviceModel.MinTLODExtraCapMult[serviceModel.activeProfile], 1)}" : "") + (((serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3 && !serviceModel.TLODBaseMinus[serviceModel.activeProfile]) || (serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2 && serviceModel.MinTLODExtra[serviceModel.activeProfile])) ? ((serviceModel.StartTLODExtraAtMax[serviceModel.activeProfile] ? " TXtraStMax" : "") + (serviceModel.FreeTLOD[serviceModel.activeProfile] ? " FreeTLOD" : "")) : "") + ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3) ? ((serviceModel.TLODBaseMinus[serviceModel.activeProfile] ? " TBXtraFPSCap" : "") + (serviceModel.MaxTLODExtra[serviceModel.activeProfile] ? $" TTopXtra TTopXtraMult:{serviceModel.MaxTLODExtraCapMult[serviceModel.activeProfile]}" : "")) : "") + $" TMax:{serviceModel.MaxTLOD[serviceModel.activeProfile]}" + (serviceModel.TLODExtraMtns[serviceModel.activeProfile] ? $" TMtns TMtnsAltMin:{serviceModel.TLODExtraMtnsTriggerAlt[serviceModel.activeProfile]} TMtnsAmt:{serviceModel.TLODExtraMtnsAmount[serviceModel.activeProfile]}" : "") + ((serviceModel.NightMode[serviceModel.activeProfile] == 1) ? " TNight:Div2" : ((serviceModel.NightMode[serviceModel.activeProfile] == 2) ? $" TNight:Clds-{serviceModel.NightCloudReduceLevels[serviceModel.activeProfile]}" : ((serviceModel.NightMode[serviceModel.activeProfile] == 3) ? $" TNight:Both-{serviceModel.NightCloudReduceLevels[serviceModel.activeProfile]}" : ""))) + $" TLODBAlt:{serviceModel.AltTLODBase[serviceModel.activeProfile]} TLODTAlt:{serviceModel.AltTLODTop[serviceModel.activeProfile]}" + (serviceModel.CustomAutoOLOD[serviceModel.activeProfile] ? $" CustomOLOD OLODB:{serviceModel.activeOLODAtBase} OLODT:{serviceModel.activeOLODAtTop} OLODBAlt:{serviceModel.AltOLODBase[serviceModel.activeProfile]} OLODTAlt:{serviceModel.AltOLODTop[serviceModel.activeProfile]} " : "") + (serviceModel.DecCloudQ[serviceModel.activeProfile] ? ("CloudQ CloudQMethod:" + ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] >= 2) ? "GPU" : GetTCloudQMethodName()) + " " + ((serviceModel.DecCloudQMethod[serviceModel.activeProfile] == 0 && serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2) ? $"CRecovT:{serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile]} CRecov+:{serviceModel.CloudRecoveryPlus[serviceModel.activeProfile]}" : $"CDecGPUPct:{serviceModel.CloudDecreaseGPUPct} CRecGPUPct:{serviceModel.CloudRecoverGPUPct}")) : "") + (serviceModel.VRAMPlusEnabled ? $" VRAM+:LODs Vrec:{serviceModel.VRAMOverflowSettingsRecovery}% Vhld:{serviceModel.VRAMOverflowHoldTLOD}% Vred:{serviceModel.VRAMOverflowReduceTLOD}%" : "") + ((serviceModel.SimMaxLOD != 1000f) ? $" MaxLOD:{serviceModel.SimMaxLOD}" : ""));
			}
		}
		if (serviceModel.UseExpertOptions && serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3 && !serviceModel.FPSCapMigrationOffered)
		{
			Show();
			serviceModel.SetSetting("FPSCapMigrationOffered" + serviceModel.FlightTypeExpert, "true", noLoad: true);
			serviceModel.FPSCapMigrationOffered = true;
			if (MessageBox.Show(this, "FPS Sensitivity + Fixed Target FPS is now the recommended mode rather than FPS Cap mode.\n\nIt maintains a Fixed FPS more responsively, achieves higher TLOD on average, and makes the new FreeTLOD option available.\n\nCurrent FPS Cap settings for this profile can be automatically migrated to the recommended mode with equivalent tuning. This notice is only shown once per profile.\n\nWould you like to proceed?", "Recommended Settings Change", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
			{
				serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("TLODAutoMethod" + serviceModel.profileSuffix) : "TLODAutoMethod_VFR", "0", noLoad: true);
				serviceModel.SetSetting("AutoTargetFPS" + (serviceModel.FlightTypeIFR ? serviceModel.profileSuffix : "_VFR"), "false", noLoad: true);
				serviceModel.SetSetting("FixedTargetFPS" + (serviceModel.FlightTypeIFR ? serviceModel.profileSuffix : "_VFR"), "true", noLoad: true);
				serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("FreeTLOD" + serviceModel.profileSuffix) : "FreeTLOD_VFR", "false", noLoad: true);
				serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("MinTLODExtra" + serviceModel.profileSuffix) : "MinTLODExtra_VFR", "true", noLoad: true);
				float num4 = Math.Min(serviceModel.MaxTLOD[serviceModel.activeProfile] * serviceModel.MaxTLODExtraCapMult[serviceModel.activeProfile], serviceModel.SimMaxLOD);
				serviceModel.SetSetting("maxTLod" + ((serviceModel.activeProfile == 1) ? serviceModel.profileSuffix : "_VFR"), Convert.ToString(num4, CultureInfo.InvariantCulture), noLoad: true);
				float num5 = (serviceModel.ProfileName[serviceModel.FlightTypeExpert].ToUpperInvariant().Contains("VFR") ? serviceModel.defaultAvgDesRate_VFR : serviceModel.defaultAvgDesRate_IFR);
				int num6 = (int)Math.Round(serviceModel.AltTLODBase[serviceModel.activeProfile] + num5 * (num4 - serviceModel.MinTLOD[serviceModel.activeProfile]) / (60f * (float)serviceModel.FPSTolerance[serviceModel.activeProfile]));
				serviceModel.SetSetting("AltTLODTop_SensTol" + ((serviceModel.activeProfile == 1) ? serviceModel.profileSuffix : "_VFR"), Convert.ToString(num6, CultureInfo.InvariantCulture));
				Logger.Log(LogLevel.Information, "MainWindow:LoadSettings", $"User agreed to auto FPS Cap migration for current profile: TLODAutoMethod set to Sensitivity, AutoTargetFPS set to false, FixedTargetFPS set to true, FreeTLOD set to false, MinTLODExtra set to true, MaxTLOD increased to {num4} and AltTLODTop_SensTol set to {num6} ft");
				LoadSettings();
				serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
			}
			else
			{
				Logger.Log(LogLevel.Information, "MainWindow:LoadSettings", "User declined auto FPS Cap migration for current profile: No changes made to TLODAutoMethod, AutoTargetFPS, FixedTargetFPS, FreeTLOD, MinTLODExtra, MaxTLOD or AltTLODTop_SensTol");
			}
		}
	}

	protected string GetTLODMethodName()
	{
		if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 0)
		{
			return "Sens";
		}
		if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 1)
		{
			return "Tol";
		}
		if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 2)
		{
			return "ATLOD";
		}
		return "FPSCap";
	}

	protected string GetTCloudQMethodName()
	{
		if (serviceModel.DecCloudQMethod[serviceModel.activeProfile] == 0)
		{
			return "TLOD";
		}
		return "GPU";
	}

	protected string GetGfxModeName()
	{
		string activeGraphicsMode = serviceModel.ActiveGraphicsMode;
		if (!(activeGraphicsMode == "FG"))
		{
			if (activeGraphicsMode == "Man FG")
			{
				if (serviceModel.ManFgMultiplier == 1)
				{
					return "Man DynFG";
				}
				return "Man FG " + serviceModel.ManFgMultiplier + "X";
			}
			return serviceModel.ActiveGraphicsMode;
		}
		if (serviceModel.FgModeMultiplier == 1)
		{
			return "DynFG";
		}
		return "FG " + serviceModel.FgModeMultiplier + "X";
	}

	protected void UpdateStatus()
	{
		if (serviceModel.IsSimRunning)
		{
			if (serviceModel.OffsetSearchingActive)
			{
				lblConnStatMSFS.Foreground = ThemeOrangeBrush;
			}
			else if (serviceModel.PerfOptEnabled)
			{
				lblConnStatMSFS.Foreground = new SolidColorBrush(Colors.RoyalBlue);
			}
			else
			{
				lblConnStatMSFS.Foreground = ThemeGreenBrush;
			}
		}
		else
		{
			lblConnStatMSFS.Foreground = ThemeRedBrush;
		}
		int num = ((serviceModel.DetectedSimVersion != null) ? (int.Parse(serviceModel.DetectedSimVersion.Split('.')[1]) - 2) : 0);
		lblConnStatMSFS.Content = (serviceModel.isMSFS2024 ? "MSFS2024 " : "MSFS2020 ") + ((serviceModel.DetectedSimVersion == null || serviceModel.UseExpertOptions || !serviceModel.isMSFS2024) ? serviceModel.DetectedSimVersion : ("SU" + num));
		int num2;
		if (IPCManager.SimConnect != null)
		{
			num2 = (IPCManager.SimConnect.IsReady ? 1 : 0);
			if (num2 != 0)
			{
				lblConnStatSimConnect.Foreground = ThemeGreenBrush;
				goto IL_014b;
			}
		}
		else
		{
			num2 = 0;
		}
		lblConnStatSimConnect.Foreground = ThemeRedBrush;
		goto IL_014b;
		IL_014b:
		if (num2 != 0 && serviceModel.IsSessionRunning && !serviceModel.OffsetSearchingActive)
		{
			lblConnStatSession.Foreground = ThemeGreenBrush;
		}
		else
		{
			lblConnStatSession.Foreground = ThemeRedBrush;
		}
		if (_webServer != null && _webServer.IsWidgetConnected)
		{
			lblConnStatWidget.Foreground = ThemeGreenBrush;
		}
		else
		{
			lblConnStatWidget.Foreground = ThemeRedBrush;
		}
	}

	protected float GetFPS()
	{
		if (serviceModel.MemoryAccess != null)
		{
			float num = ((serviceModel.fixedFPSDetectedAutoTargetFPS || serviceModel.FixedTargetFPS) ? serviceModel.fpsRaw : serviceModel.fpsAverage);
			if (serviceModel.fpsSourceRTSS)
			{
				if (serviceModel.LsModeEnabled)
				{
					return (float)Math.Round(num * (float)serviceModel.LsModeMultiplier);
				}
				if (serviceModel.MsmModeEnabled && serviceModel.ActiveWindowMSFS)
				{
					return (float)Math.Round(num * (float)serviceModel.MsmMultiplier);
				}
				if (serviceModel.VrModeActive)
				{
					if (serviceModel.OfxrModeEnabled)
					{
						if (serviceModel.OfxrStatusJsonActive && serviceModel.OfxrOutputFps > 0f)
						{
							return (float)Math.Round(num);
						}
						return (float)Math.Round(num * (float)serviceModel.OfxrModeMultiplier);
					}
					if (serviceModel.VxrModeEnabled)
					{
						if (serviceModel.VxrStatusJsonActive && serviceModel.VxrOutputFps > 0f)
						{
							return (float)Math.Round(num);
						}
						return (float)Math.Round(num * (float)serviceModel.VxrModeMultiplier);
					}
					return (float)Math.Round(num);
				}
				return (float)Math.Round(num);
			}
			if (serviceModel.VrModeActive)
			{
				if (serviceModel.OfxrModeEnabled)
				{
					if (serviceModel.OfxrStatusJsonActive && serviceModel.OfxrOutputFps > 0f)
					{
						return (float)Math.Round(num);
					}
					return (float)Math.Round(num * (float)serviceModel.OfxrModeMultiplier);
				}
				if (serviceModel.VxrModeEnabled)
				{
					if (serviceModel.VxrStatusJsonActive && serviceModel.VxrOutputFps > 0f)
					{
						return (float)Math.Round(num);
					}
					return (float)Math.Round(num * (float)serviceModel.VxrModeMultiplier);
				}
				return (float)Math.Round(num);
			}
			if (serviceModel.ActiveGraphicsMode == "Man FG" && serviceModel.ActiveWindowMSFS)
			{
				return (float)Math.Round(num * (float)serviceModel.ManFgMultiplier);
			}
			if (serviceModel.FgModeEnabled && serviceModel.ActiveWindowMSFS)
			{
				return (float)Math.Round(num * (float)serviceModel.FgModeMultiplier);
			}
			if (serviceModel.FgModeFSREnabled)
			{
				return (float)Math.Round(num * 2f);
			}
			if (serviceModel.MsmModeEnabled && serviceModel.ActiveWindowMSFS)
			{
				return (float)Math.Round(num * (float)serviceModel.MsmMultiplier);
			}
			if (serviceModel.LsModeEnabled)
			{
				return (float)Math.Round(num * (float)serviceModel.LsModeMultiplier);
			}
			return (float)Math.Round(num);
		}
		return 0f;
	}

	protected void UpdateLiveValues()
	{
		if (IPCManager.SimConnect != null && IPCManager.SimConnect.IsConnected)
		{
			if (GetFPS() > 0f)
			{
				lblSimFPS.Content = GetFPS().ToString("F0") + ((serviceModel.SpikeDetected || serviceModel.SpikeVerificationActive) ? "⚡" : "");
				Thickness margin = ((serviceModel.SpikeDetected || serviceModel.SpikeVerificationActive) ? new Thickness(-3.0, 0.0, 0.0, 0.0) : new Thickness(5.0, 0.0, 0.0, 0.0));
				if (serviceModel.VrModeActive && serviceModel.OfxrModeEnabled && serviceModel.OfxrStatusJsonActive && serviceModel.OfxrOutputFps > 0f)
				{
					imgSimFPSOFXR.Visibility = Visibility.Visible;
					if (imgSimFPSVXR != null)
					{
						imgSimFPSVXR.Visibility = Visibility.Collapsed;
					}
					imgSimFPSRTSS.Visibility = Visibility.Collapsed;
					imgSimFPSMSFS.Visibility = Visibility.Collapsed;
					imgSimFPSOFXR.Margin = margin;
					activeFpsIcon = 3;
				}
				else if (serviceModel.VrModeActive && serviceModel.VxrModeEnabled && serviceModel.VxrStatusJsonActive && serviceModel.VxrOutputFps > 0f)
				{
					if (imgSimFPSVXR != null)
					{
						imgSimFPSVXR.Visibility = Visibility.Visible;
						imgSimFPSVXR.Margin = margin;
					}
					imgSimFPSOFXR.Visibility = Visibility.Collapsed;
					imgSimFPSRTSS.Visibility = Visibility.Collapsed;
					imgSimFPSMSFS.Visibility = Visibility.Collapsed;
					activeFpsIcon = 4;
				}
				else if (serviceModel.fpsSourceRTSS)
				{
					imgSimFPSRTSS.Visibility = Visibility.Visible;
					if (imgSimFPSVXR != null)
					{
						imgSimFPSVXR.Visibility = Visibility.Collapsed;
					}
					imgSimFPSMSFS.Visibility = Visibility.Collapsed;
					imgSimFPSOFXR.Visibility = Visibility.Collapsed;
					imgSimFPSRTSS.Margin = margin;
					activeFpsIcon = 2;
				}
				else
				{
					imgSimFPSMSFS.Visibility = Visibility.Visible;
					if (imgSimFPSVXR != null)
					{
						imgSimFPSVXR.Visibility = Visibility.Collapsed;
					}
					imgSimFPSRTSS.Visibility = Visibility.Collapsed;
					imgSimFPSOFXR.Visibility = Visibility.Collapsed;
					imgSimFPSMSFS.Margin = margin;
					activeFpsIcon = 1;
				}
				if (serviceModel.FixedTargetFPS || serviceModel.fixedFPSDetectedAutoTargetFPS)
				{
					lblSimFPSType.Content = "FPS:";
					lblSimFPSType.ToolTip = "Unfiltered FPS shown when a fixed FPS is detected or\nset, as the system-set FPS value is most relevant for\nperformance management in this case";
				}
				else
				{
					lblSimFPSType.Content = "FPS+:";
					lblSimFPSType.ToolTip = $"Average of {5} recent FPS values, ignoring brief drops or spikes";
				}
			}
			else
			{
				lblSimFPS.Content = "n/a";
				imgSimFPSMSFS.Visibility = Visibility.Collapsed;
				imgSimFPSRTSS.Visibility = Visibility.Collapsed;
			}
		}
		else
		{
			lblSimFPS.Content = "n/a";
			imgSimFPSMSFS.Visibility = Visibility.Collapsed;
			imgSimFPSRTSS.Visibility = Visibility.Collapsed;
			imgSimFPSOFXR.Visibility = Visibility.Collapsed;
			if (imgSimFPSVXR != null)
			{
				imgSimFPSVXR.Visibility = Visibility.Collapsed;
			}
			activeFpsIcon = 0;
		}
		if (!serviceModel.IsSimRunning)
		{
			// Fork: the offline "24>20 / 20>24" config switch was removed from the UI; Reset only exists while MSFS runs.
			btnReset.Visibility = Visibility.Collapsed;
		}
		else
		{
			btnReset.Content = "_Reset";
			if (serviceModel.IsSessionRunning)
			{
				btnReset.ToolTip = "Reset TLOD, Clouds, Auto Target FPS and graphics mode detection to initial state.";
				btnReset.Visibility = Visibility.Visible;
			}
			else
			{
				btnReset.ToolTip = "Reset current profile to default values";
				if (serviceModel.UseExpertOptions)
				{
					btnReset.Visibility = Visibility.Visible;
				}
				else
				{
					btnReset.Visibility = Visibility.Collapsed;
				}
			}
		}
		if (serviceModel.SpikeDetectEnabled && serviceModel.UseExpertOptions && serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 0 && serviceModel.GetRTSSFrameTimes(record: false, 10f, checkOnly: true))
		{
			chkSpikeProtect.IsChecked = serviceModel.SpikeTLODReductionEnabled;
			if (serviceModel.SpikeTLODReductionEnabled && serviceModel.LogPlus)
			{
				stkpnlSpikeDetect.Visibility = Visibility.Visible;
				if (!serviceModel.IsSessionRunning)
				{
					Label label = lblSpikeDetectProtStatus;
					string content = (serviceModel.SpikeDetectProtStatus = "Awaiting flight session FT data");
					label.Content = content;
				}
			}
			else
			{
				stkpnlSpikeDetect.Visibility = Visibility.Collapsed;
			}
			chkSpikeProtect.Visibility = Visibility.Visible;
			lblSpikeProtect.Visibility = Visibility.Visible;
		}
		else
		{
			chkSpikeProtect.Visibility = Visibility.Collapsed;
			lblSpikeProtect.Visibility = Visibility.Collapsed;
			stkpnlSpikeDetect.Visibility = Visibility.Collapsed;
		}
		bool flag = IPCManager.IsProcessRunning("GPU-Z", startsWith: true) || serviceModel.gpuUsage > 0f;
		chkVRAMPlus.Visibility = Visibility.Collapsed;
		chkVRAMPlus2.Visibility = ((!flag || !serviceModel.UseExpertOptions || (serviceModel.isMSFS2024 && serviceModel.TLODAutoMethod[serviceModel.activeProfile] != 2)) ? Visibility.Collapsed : Visibility.Visible);
		lblVRAMPlus.Visibility = ((!(serviceModel.isMSFS2024 & flag) || !serviceModel.UseExpertOptions) ? Visibility.Collapsed : Visibility.Visible);
		cbVRAMPlus.Visibility = ((!(serviceModel.isMSFS2024 & flag) || !serviceModel.UseExpertOptions) ? Visibility.Collapsed : Visibility.Visible);
		var (flag2, flag3) = GetFixedFPSConfigMismatch();
		cbTargetFPSOption.Background = (((serviceModel.FixedFPSConfigWarning && fixedFPSWarningCountdown > 0) & flag2) ? new SolidColorBrush(Colors.Orange) : ThemeInputBgBrush);
		txtTargetFPS.Background = (((serviceModel.FixedFPSConfigWarning && fixedFPSWarningCountdown > 0) & flag3) ? new SolidColorBrush(Colors.Orange) : ThemeInputBgBrush);
		if (fixedFPSWarningCountdown > 0)
		{
			fixedFPSWarningCountdown--;
		}
		string text2 = "Target FPS option\nManual: User choice (default)\nAuto: AutoFPS chooses automatically\nFixed: FPS stays at the system-set\nfixed value; requires matching it\n\n";
		if (serviceModel.FixedFPSConfigWarning & flag2)
		{
			if (!serviceModel.FixedTargetFPS)
			{
				string text3 = (serviceModel.SettingsReduceEnabled ? "Target FPS is too close to cap for Manual mode\n(Settings Reduction requires extra recovery headroom).\n" : "Target FPS is too close to cap for Manual mode.\n");
				text2 = text2 + "WARNING:\n" + $"Detected fixed FPS: {serviceModel.fixedFPSDetected:F1}.\n" + text3 + "If you regularly use a fixed FPS:\nChange to Fixed Target FPS.";
			}
			else
			{
				text2 = text2 + "WARNING:\n" + $"Detected fixed FPS: {serviceModel.fixedFPSDetected:F1}.\n" + "Target FPS is well below cap for Fixed mode.\nChange to Manual Target FPS (or match cap).";
			}
		}
		else
		{
			text2 += "Orange background means config issue";
		}
		cbTargetFPSOption.ToolTip = text2;
		text2 = "Enter a target FPS achievable by your system\nfor the selected graphics mode.\n\nIf using frame generation, this is the target for\nthe rendered FPS AFTER frame generation is applied,\nEXCEPT when using DynFG with MSFS as the\nFPS source, which targets the pre-FG FPS.\n\n";
		if (serviceModel.FixedFPSConfigWarning & flag3)
		{
			string text4 = ((serviceModel.TargetFPS > serviceModel.fixedFPSDetected) ? $"Set Target FPS to {serviceModel.fixedFPSDetected:F0} or lower." : $"Set Target FPS to {serviceModel.fixedFPSDetected:F0} or switch to Manual.");
			text2 = text2 + "WARNING:\n" + ((serviceModel.TargetFPS > serviceModel.fixedFPSDetected) ? $"Target FPS ({serviceModel.TargetFPS}) > detected Fixed FPS ({serviceModel.fixedFPSDetected:F0}).\n" : $"Target FPS ({serviceModel.TargetFPS}) is well below Fixed FPS ({serviceModel.fixedFPSDetected:F0}).\n") + text4;
		}
		else
		{
			text2 += "Orange background means config issue";
		}
		txtTargetFPS.ToolTip = text2;
		if (serviceModel.MemoryAccess != null && serviceModel.DevTesting)
		{
			string content2 = serviceModel.MemoryAccess.ScanMemory();
			cbUpdateChannel.Visibility = Visibility.Collapsed;
			lblappUrlTestVersion.Visibility = Visibility.Collapsed;
			cbUpdateOption.Visibility = Visibility.Collapsed;
			lblappUrl.Visibility = Visibility.Collapsed;
			lblappUrlTestVersion.Visibility = Visibility.Collapsed;
			lblStatusMessage.Content = content2;
		}
		else if (serviceModel.MemoryAccess != null && serviceModel.initialLODsSet)
		{
			if (serviceModel.isMSFS2024 && serviceModel.SettingsReduceEnabled)
			{
				Grid grid = gpboxSimValues.Content as Grid;
				if (ShowReduceSimValues && (serviceModel.ReductionSettingsSuite[serviceModel.activeProfile] > 0 || serviceModel.VRAMSettingsReduceActive) && serviceModel.ReductionSettingsDepth > 0)
				{
					stkpnlSettingsReduction.Visibility = Visibility.Visible;
					if (ShowAutoReducePanelState == 0 && grid != null)
					{
						foreach (FrameworkElement item in grid.Children.OfType<FrameworkElement>())
						{
							if (Grid.GetRow(item) == 2)
							{
								item.Visibility = Visibility.Visible;
							}
						}
					}
					if (ShowAutoReducePanelState != 2 && (serviceModel.ReductionSettingsSuite[serviceModel.activeProfile] > 1 || serviceModel.VRAMSettingsReduceActive))
					{
						if (grid != null)
						{
							foreach (FrameworkElement item2 in grid.Children.OfType<FrameworkElement>())
							{
								int row = Grid.GetRow(item2);
								if (row >= 3 && row <= 5)
								{
									item2.Visibility = Visibility.Visible;
								}
							}
							ShowAutoReducePanelState = 2;
						}
					}
					else if (ShowAutoReducePanelState != 1 && serviceModel.ReductionSettingsSuite[serviceModel.activeProfile] <= 1 && !serviceModel.VRAMSettingsReduceActive && grid != null)
					{
						foreach (FrameworkElement item3 in grid.Children.OfType<FrameworkElement>())
						{
							int row2 = Grid.GetRow(item3);
							if (row2 >= 3 && row2 <= 5)
							{
								item3.Visibility = Visibility.Collapsed;
							}
						}
						ShowAutoReducePanelState = 1;
					}
				}
				else if (ShowAutoReducePanelState != 0)
				{
					stkpnlSettingsReduction.Visibility = Visibility.Collapsed;
					if (grid != null)
					{
						foreach (FrameworkElement item4 in grid.Children.OfType<FrameworkElement>())
						{
							int row3 = Grid.GetRow(item4);
							if (row3 >= 2 && row3 <= 5)
							{
								item4.Visibility = Visibility.Collapsed;
							}
						}
						ShowAutoReducePanelState = 0;
					}
				}
				lblReduceCloudQ.Content = (serviceModel.VrModeActive ? ServiceModel.SettingQualityText(serviceModel.cloudQ_VR) : ServiceModel.SettingQualityText(serviceModel.cloudQ));
				lblReduceTrees.Content = (serviceModel.VrModeActive ? ServiceModel.SettingQualityText(serviceModel.Trees_VR) : ServiceModel.SettingQualityText(serviceModel.Trees));
				lblReduceRTShadows.Content = (serviceModel.VrModeActive ? serviceModel.RTShadows_VR : serviceModel.RTShadows);
				Label label2 = lblReduceTShadows;
				object content3;
				if (serviceModel.VrModeActive)
				{
					content3 = ((!serviceModel.TShadowsEnabled_VR) ? "Off" : ((object)serviceModel.TShadows_VR));
				}
				else
				{
					content3 = ((!serviceModel.TShadowsEnabled) ? "Off" : ((object)serviceModel.TShadows));
				}
				label2.Content = content3;
				lblReduceBldings.Content = (serviceModel.VrModeActive ? ServiceModel.SettingQualityText(serviceModel.Bldings_VR) : ServiceModel.SettingQualityText(serviceModel.Bldings));
				lblReduceDispMap.Content = (serviceModel.VrModeActive ? serviceModel.DispMap_VR : serviceModel.DispMap);
				lblReduceOSTPCache.Content = (serviceModel.VrModeActive ? ServiceModel.SettingQualityText(serviceModel.OSTPCache_VR) : ServiceModel.SettingQualityText(serviceModel.OSTPCache));
				lblReducePlants.Content = (serviceModel.VrModeActive ? ServiceModel.SettingQualityText(serviceModel.Plants_VR) : ServiceModel.SettingQualityText(serviceModel.Plants));
				lblReduceGrass.Content = (serviceModel.VrModeActive ? ServiceModel.SettingQualityText(serviceModel.Grass_VR) : ServiceModel.SettingQualityText(serviceModel.Grass));
				lblReducePkdAcQ.Content = (serviceModel.VrModeActive ? ServiceModel.SettingQualityText(serviceModel.PkdAcQ_VR) : ServiceModel.SettingQualityText(serviceModel.PkdAcQ));
				lblReduceAcTrafQ.Content = (serviceModel.VrModeActive ? ServiceModel.SettingQualityText(serviceModel.AcTrafQ_VR) : ServiceModel.SettingQualityText(serviceModel.AcTrafQ));
				Label label3 = lblReduceCloudQ;
				Brush foreground;
				if (serviceModel.VrModeActive)
				{
					if (serviceModel.cloudQ_VR == serviceModel.DefaultCloudQ_VR)
					{
						foreground = ThemeTextBrush;
					}
					else
					{
						foreground = (serviceModel.IncCloudQActive ? ThemeGreenBrush : ThemeRedBrush);
					}
				}
				else if (serviceModel.cloudQ == serviceModel.DefaultCloudQ)
				{
					foreground = ThemeTextBrush;
				}
				else
				{
					foreground = (serviceModel.IncCloudQActive ? ThemeGreenBrush : ThemeRedBrush);
				}
				label3.Foreground = foreground;
				Label label4 = lblReduceTrees;
				Brush foreground2;
				if (serviceModel.VrModeActive)
				{
					foreground2 = ((serviceModel.Trees_VR == serviceModel.DefaultTrees_VR) ? ThemeTextBrush : ThemeRedBrush);
				}
				else
				{
					foreground2 = ((serviceModel.Trees == serviceModel.DefaultTrees) ? ThemeTextBrush : ThemeRedBrush);
				}
				label4.Foreground = foreground2;
				Label label5 = lblReduceRTShadows;
				Brush foreground3;
				if (serviceModel.VrModeActive)
				{
					foreground3 = ((serviceModel.RTShadows_VR == serviceModel.DefaultRTShadows_VR) ? ThemeTextBrush : ThemeRedBrush);
				}
				else
				{
					foreground3 = ((serviceModel.RTShadows == serviceModel.DefaultRTShadows) ? ThemeTextBrush : ThemeRedBrush);
				}
				label5.Foreground = foreground3;
				Label label6 = lblReduceTShadows;
				Brush foreground4;
				if (serviceModel.VrModeActive)
				{
					foreground4 = ((serviceModel.TShadows_VR == serviceModel.DefaultTShadows_VR) ? ThemeTextBrush : ThemeRedBrush);
				}
				else
				{
					foreground4 = ((serviceModel.TShadows == serviceModel.DefaultTShadows) ? ThemeTextBrush : ThemeRedBrush);
				}
				label6.Foreground = foreground4;
				Label label7 = lblReduceBldings;
				Brush foreground5;
				if (serviceModel.VrModeActive)
				{
					foreground5 = ((serviceModel.Bldings_VR == serviceModel.DefaultBldings_VR) ? ThemeTextBrush : ThemeRedBrush);
				}
				else
				{
					foreground5 = ((serviceModel.Bldings == serviceModel.DefaultBldings) ? ThemeTextBrush : ThemeRedBrush);
				}
				label7.Foreground = foreground5;
				Label label8 = lblReduceDispMap;
				Brush foreground6;
				if (serviceModel.VrModeActive)
				{
					foreground6 = ((serviceModel.DispMap_VR == serviceModel.DefaultDispMap_VR) ? ThemeTextBrush : ThemeRedBrush);
				}
				else
				{
					foreground6 = ((serviceModel.DispMap == serviceModel.DefaultDispMap) ? ThemeTextBrush : ThemeRedBrush);
				}
				label8.Foreground = foreground6;
				Label label9 = lblReduceOSTPCache;
				Brush foreground7;
				if (serviceModel.VrModeActive)
				{
					foreground7 = ((serviceModel.OSTPCache_VR == serviceModel.DefaultOSTPCache_VR) ? ThemeTextBrush : ThemeRedBrush);
				}
				else
				{
					foreground7 = ((serviceModel.OSTPCache == serviceModel.DefaultOSTPCache) ? ThemeTextBrush : ThemeRedBrush);
				}
				label9.Foreground = foreground7;
				Label label10 = lblReducePlants;
				Brush foreground8;
				if (serviceModel.VrModeActive)
				{
					foreground8 = ((serviceModel.Plants_VR == serviceModel.DefaultPlants_VR) ? ThemeTextBrush : ThemeRedBrush);
				}
				else
				{
					foreground8 = ((serviceModel.Plants == serviceModel.DefaultPlants) ? ThemeTextBrush : ThemeRedBrush);
				}
				label10.Foreground = foreground8;
				Label label11 = lblReduceGrass;
				Brush foreground9;
				if (serviceModel.VrModeActive)
				{
					foreground9 = ((serviceModel.Grass_VR == serviceModel.DefaultGrass_VR) ? ThemeTextBrush : ThemeRedBrush);
				}
				else
				{
					foreground9 = ((serviceModel.Grass == serviceModel.DefaultGrass) ? ThemeTextBrush : ThemeRedBrush);
				}
				label11.Foreground = foreground9;
				Label label12 = lblReducePkdAcQ;
				Brush foreground10;
				if (serviceModel.VrModeActive)
				{
					foreground10 = ((serviceModel.PkdAcQ_VR == serviceModel.DefaultPkdAcQ_VR) ? ThemeTextBrush : ThemeRedBrush);
				}
				else
				{
					foreground10 = ((serviceModel.PkdAcQ == serviceModel.DefaultPkdAcQ) ? ThemeTextBrush : ThemeRedBrush);
				}
				label12.Foreground = foreground10;
				Label label13 = lblReduceAcTrafQ;
				Brush foreground11;
				if (serviceModel.VrModeActive)
				{
					foreground11 = ((serviceModel.AcTrafQ_VR == serviceModel.DefaultAcTrafQ_VR) ? ThemeTextBrush : ThemeRedBrush);
				}
				else
				{
					foreground11 = ((serviceModel.AcTrafQ == serviceModel.DefaultAcTrafQ) ? ThemeTextBrush : ThemeRedBrush);
				}
				label13.Foreground = foreground11;
				if (ShowAutoReducePanel && (serviceModel.AutoReduceSettings[serviceModel.activeProfile] || serviceModel.VRAMPlusSuite == 2) && serviceModel.ReductionSettingsSuite[serviceModel.activeProfile] == 3)
				{
					stkpnlSettingsReduction.Visibility = Visibility.Visible;
				}
				else
				{
					stkpnlSettingsReduction.Visibility = Visibility.Collapsed;
				}
			}
			else
			{
				stkpnlSettingsReduction.Visibility = Visibility.Collapsed;
			}
			if (!sessionRunning)
			{
				tlodLast = serviceModel.tlod;
				olodLast = serviceModel.olod;
				sessionRunning = true;
			}
			firstSessionRan = true;
			if (_msfsPid == -1)
			{
				_msfsPid = Process.GetProcessesByName("FlightSimulator" + (serviceModel.isMSFS2024 ? "2024" : "")).FirstOrDefault()?.Id ?? (-1);
				if (_msfsPid != -1)
				{
					try
					{
						_cpuProbe?.Dispose();
					}
					catch
					{
					}
					PdhCoreSampler sampler = new PdhCoreSampler();
					ProcessCpuSampler processSampler = new ProcessCpuSampler();
					_cpuProbe = new CpuHeadroomProbe(sampler, processSampler, _msfsPid, 1000, 10, 10, 5.0, serviceModel.perfLogTopCoreCount);
					_cpuProbeHandler = (CpuSnapshot snap) =>
					{
						serviceModel._latestSnap = snap;
					};
					_cpuProbe.OnSnapshot += _cpuProbeHandler;
				}
			}
			cbUpdateChannel.Visibility = Visibility.Collapsed;
			lblappUrlTestVersion.Visibility = Visibility.Collapsed;
			cbUpdateOption.Visibility = Visibility.Collapsed;
			lblappUrl.Visibility = Visibility.Collapsed;
			lblappUrlTestVersion.Visibility = Visibility.Collapsed;
			imgSimHeaderIcon2.Visibility = Visibility.Visible;
			btnDynSet.Content = "DynSet" + ((!serviceModel.VrModeActive) ? (serviceModel.DynSet ? ((object)serviceModel.DynSetTargetFPS) : "X") : (serviceModel.DynSet_VR ? ((object)serviceModel.DynSetTargetFPS_VR) : "X"));
			lblStatusMessage.Foreground = ThemeTextBrush;
			stkStatusRow.HorizontalAlignment = HorizontalAlignment.Center;
			lblStatusMessage.HorizontalContentAlignment = HorizontalAlignment.Center;
			((DependencyObject)lblStatusMessage).SetValue(TextBlock.TextAlignmentProperty, (object)TextAlignment.Center);
			if (serviceModel.VrModeActive ? serviceModel.DynSet_VR : serviceModel.DynSet)
			{
				lblSimTLODlbl.Content = "DTLOD:";
				lblSimTLODlbl.ToolTip = "Dynamic TLOD " + (serviceModel.UseExpertOptions ? "" : "auto-") + "enabled with " + (serviceModel.VrModeActive ? serviceModel.DynSetTargetFPS_VR : serviceModel.DynSetTargetFPS) + " native target frame rate";
				lblSimOLODlbl.Content = "DOLOD:";
				lblSimOLODlbl.ToolTip = "Dynamic OLOD " + (serviceModel.UseExpertOptions ? "" : "auto-") + "enabled with " + (serviceModel.VrModeActive ? serviceModel.DynSetTargetFPS_VR : serviceModel.DynSetTargetFPS) + " native target frame rate";
				imgSimHeaderIcon.Visibility = Visibility.Visible;
			}
			else
			{
				lblSimTLODlbl.Content = "TLOD:";
				lblSimTLODlbl.ToolTip = "Terrain Level of Detail";
				lblSimOLODlbl.Content = "OLOD:";
				lblSimOLODlbl.ToolTip = "Object Level of Detail";
				imgSimHeaderIcon.Visibility = Visibility.Collapsed;
			}
			Label label14 = lblSimTLOD;
			string text5 = serviceModel.tlod.ToString("F0");
			string text6 = ((serviceModel.VRAMPlusEnabled && (serviceModel.VRAMHoldActive || serviceModel.VRAMReduceActive)) ? " \ud83d\udd12" : "");
			string text7;
			if (serviceModel.tlod > tlodLast)
			{
				text7 = " ▲";
			}
			else
			{
				text7 = ((serviceModel.tlod < tlodLast) ? " ▼" : "");
			}
			label14.Content = text5 + text6 + text7 + ((serviceModel.tlod <= serviceModel.SimMinLOD) ? " ⊥" : "");
			Label label15 = lblSimOLOD;
			string text8 = serviceModel.olod.ToString("F0");
			string text9;
			if (serviceModel.olod > olodLast)
			{
				text9 = " ▲";
			}
			else
			{
				text9 = ((serviceModel.olod < olodLast) ? " ▼" : "");
			}
			Label label16 = label15;
			string text10 = text8;
			string text11 = text9;
			string text12;
			if (serviceModel.olod <= serviceModel.SimMinLOD)
			{
				text12 = " ⊥";
			}
			else
			{
				text12 = ((serviceModel.olod >= serviceModel.SimMaxLOD) ? " ⊤" : "");
			}
			label16.Content = text10 + text11 + text12;
			if (serviceModel.simRate != 1f)
			{
				lblStatusMessage.Content = serviceModel.simRate.ToString("0.###") + "X ";
			}
			else
			{
				lblStatusMessage.Content = "";
			}
			if ((serviceModel.AutoReduceSettings[serviceModel.activeProfile] || serviceModel.VRAMSettingsReduceActive) && !serviceModel.IncCloudQActive && !serviceModel.NightCloudReduceLevelsActive)
			{
				if (serviceModel.VRAMSettingsReduceActive)
				{
					lblSimReduce.Content = "VRAM+:";
					lblSimReduce.Foreground = ThemeRedBrush;
				}
				else
				{
					lblSimReduce.Content = "Reduce:";
					lblSimReduce.Foreground = ThemeTextBrush;
				}
				lblSimReduce2.ToolTip = null;
				if (!serviceModel.SettingsReduceEnabled)
				{
					lblSimReduce2.Content = "Disabled";
					lblSimReduce2.Foreground = ThemeOrangeBrush;
					lblSimReduce2.ToolTip = "Settings reduction disabled due to UserCfg.opt and MSFS setting(s) mismatch or incompatible new MSFS version";
				}
				else if (serviceModel.ReductionSettingsDepth == 0 && serviceModel.MinTLODReductionAmount == 0f)
				{
					if (serviceModel.VrModeActive ? serviceModel.DynSet_VR : serviceModel.DynSet)
					{
						lblSimReduce2.Content = "DynSet" + (((serviceModel.VrModeActive ? serviceModel.DynSetTargetFPS_VR : serviceModel.DynSetTargetFPS) >= 100) ? "" : " ") + (serviceModel.VrModeActive ? serviceModel.DynSetTargetFPS_VR : serviceModel.DynSetTargetFPS);
						lblSimReduce2.ToolTip = "Dynamic Settings " + (serviceModel.UseExpertOptions ? "" : "auto-") + "enabled with " + (serviceModel.VrModeActive ? serviceModel.DynSetTargetFPS_VR : serviceModel.DynSetTargetFPS) + " native target frame rate";
					}
					else
					{
						lblSimReduce2.Content = "Inactive";
						lblSimReduce2.ToolTip = null;
					}
					lblSimReduce2.Foreground = ThemeTextBrush;
				}
				else
				{
					if ((serviceModel.ReductionSettingsDepth == 0 && serviceModel.MinTLODReductionAmount > 0f) || (serviceModel.ReductionSettingsDepth > 0 && serviceModel.ReductionSettingsSuite[serviceModel.activeProfile] == 0 && (!serviceModel.isMSFS2024 || !serviceModel.VRAMSettingsReduceActive)))
					{
						lblSimReduce2.Content = "LODs " + Math.Round((serviceModel.MinTLOD[serviceModel.activeProfile] - serviceModel.MinTLODReductionAmount) / serviceModel.MinTLOD[serviceModel.activeProfile] * 100f) + "%";
					}
					else
					{
						Label label17 = lblSimReduce2;
						string text13 = serviceModel.ReductionSettingsDepth.ToString();
						string text14;
						if (serviceModel.ReductionSettingsDepth < reductionSettingsDepthLast)
						{
							text14 = " ▲";
						}
						else
						{
							text14 = ((serviceModel.ReductionSettingsDepth > reductionSettingsDepthLast) ? " ▼" : "");
						}
						label17.Content = "Lvl " + text13 + text14;
					}
					lblSimReduce2.Foreground = ThemeRedBrush;
				}
				reductionSettingsDepthLast = serviceModel.ReductionSettingsDepth;
			}
			else
			{
				lblSimReduce.Foreground = ThemeTextBrush;
				lblSimReduce.Content = "Clouds:";
				if (serviceModel.VrModeActive)
				{
					lblSimReduce2.Content = ServiceModel.SettingQualityText(serviceModel.cloudQ_VR);
				}
				else
				{
					lblSimReduce2.Content = ServiceModel.SettingQualityText(serviceModel.cloudQ);
				}
				if (serviceModel.NightCloudReduceLevelsActive)
				{
					lblSimReduce2.Foreground = ThemeOrangeBrush;
					lblSimReduce2.ToolTip = $"Clouds reduced by {serviceModel.NightCloudReduceLevels[serviceModel.activeProfile]} level" + ((serviceModel.NightCloudReduceLevels[serviceModel.activeProfile] > 1) ? "s" : "") + " at night";
				}
				else if (serviceModel.DecCloudQ[serviceModel.activeProfile] && serviceModel.DecCloudQActive)
				{
					lblSimReduce2.Foreground = ThemeRedBrush;
				}
				else if (serviceModel.IncCloudQActive)
				{
					lblSimReduce2.Foreground = ThemeGreenBrush;
				}
				else
				{
					lblSimReduce2.Foreground = ThemeTextBrush;
				}
			}
			if (serviceModel.VrFgConflictPromptRequired && !serviceModel.VrFgConflictPromptActive)
			{
				serviceModel.VrFgConflictPromptRequired = false;
				serviceModel.VrFgConflictPromptActive = true;
				serviceModel.AppPaused = true;
				MessageBox.Show(this, "Both OFXR and VectorXR Frame Generation layers are currently active in MSFS!\n\nRunning both layers simultaneously causes severe stuttering, pacing breakdowns, and extreme VRAM usage.\n\nAutoFPS has disabled VR Frame Generation compensation and fallen back to standard VR mode.\n\nPlease disable or disarm one of these tools before continuing.", "AutoFPS - VR Frame Generation Conflict", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				serviceModel.AppPaused = false;
				serviceModel.fpsAverageReset = true;
			}
			if (serviceModel.VrModeActive)
			{
				if (serviceModel.VrFgConflictDetected)
				{
					Label label18 = lblStatusMessage;
					label18.Content = label18.Content?.ToString() + "VR Conflict!";
				}
				else if (serviceModel.OfxrModeEnabled)
				{
					if (serviceModel.IsOFXRPaused())
					{
						Label label19 = lblStatusMessage;
						label19.Content = label19.Content?.ToString() + "OFXR ⏸";
					}
					else if (serviceModel.OfxrDesynced)
					{
						Label label20 = lblStatusMessage;
						label20.Content = label20.Content?.ToString() + "OFXR Dsync";
					}
					else if (serviceModel.OfxrModeAuto)
					{
						if (serviceModel.OfxrStatusJsonActive && serviceModel.OfxrAutoMultiplier > 1)
						{
							Label label21 = lblStatusMessage;
							label21.Content = label21.Content?.ToString() + $"OFXR Auto {serviceModel.OfxrAutoMultiplier}X";
						}
						else
						{
							Label label22 = lblStatusMessage;
							label22.Content = label22.Content?.ToString() + "OFXR Auto 1X";
						}
					}
					else
					{
						Label label23 = lblStatusMessage;
						label23.Content = label23.Content?.ToString() + "OFXR " + serviceModel.OfxrModeMultiplier + "X";
					}
				}
				else if (serviceModel.VxrModeEnabled)
				{
					if (serviceModel.IsVXRPaused())
					{
						Label label24 = lblStatusMessage;
						label24.Content = label24.Content?.ToString() + "VXR ⏸";
					}
					else if (serviceModel.IsVXRStandby())
					{
						Label label25 = lblStatusMessage;
						label25.Content = label25.Content?.ToString() + "VXR Stby";
					}
					else
					{
						Label label26 = lblStatusMessage;
						label26.Content = label26.Content?.ToString() + ((!string.IsNullOrEmpty(serviceModel.VxrMode)) ? serviceModel.VxrMode : $"VXR {serviceModel.VxrModeMultiplier}X");
					}
				}
				else
				{
					Label label27 = lblStatusMessage;
					label27.Content = label27.Content?.ToString() + "VR";
				}
			}
			else if (serviceModel.ActiveGraphicsMode == "Man FG" && serviceModel.ManFgMultiplier == 1)
			{
				Label label28 = lblStatusMessage;
				label28.Content = label28.Content?.ToString() + "DynFG " + (serviceModel.ActiveWindowMSFS ? $"{serviceModel.DynFgCurrentMultiplier}X" : "Inactive");
			}
			else if (serviceModel.ActiveGraphicsMode == "Man FG")
			{
				Label label29 = lblStatusMessage;
				label29.Content = label29.Content?.ToString() + (serviceModel.ActiveWindowMSFS ? ("Man FG " + serviceModel.ManFgMultiplier + "X") : "FG Inact");
			}
			else if (serviceModel.FgModeFSREnabled)
			{
				Label label30 = lblStatusMessage;
				label30.Content = label30.Content?.ToString() + "FSR FG";
			}
			else if (serviceModel.LsModeAdaptive)
			{
				Label label31 = lblStatusMessage;
				label31.Content = label31.Content?.ToString() + "LSFG Adapt 1X";
			}
			else if (serviceModel.LsModeEnabled)
			{
				Label label32 = lblStatusMessage;
				label32.Content = label32.Content?.ToString() + "LSFG " + serviceModel.LsModeMultiplier.ToString("F0") + "X";
			}
			else if (serviceModel.FgModeEnabled)
			{
				Label label33 = lblStatusMessage;
				Label label34 = label33;
				string? text15 = label33.Content?.ToString();
				string? text16;
				if (serviceModel.FgModeMultiplier == 1)
				{
					text16 = $"DynFG {serviceModel.DynFgCurrentMultiplier}X";
				}
				else if (serviceModel.ActiveWindowMSFS)
				{
					text16 = ((serviceModel.FgModeMultiplier > 2) ? ("MFG " + serviceModel.FgModeMultiplier + "X") : "FG Act");
				}
				else
				{
					text16 = ((serviceModel.FgModeMultiplier > 2) ? "M" : "") + "FG Inact";
				}
				label34.Content = text15 + text16;
			}
			else if (serviceModel.MsmModeAdaptive)
			{
				Label label35 = lblStatusMessage;
				label35.Content = label35.Content?.ToString() + (serviceModel.ActiveWindowMSFS ? "MSM Adapt 1X" : "MSM Inact");
			}
			else if (serviceModel.MsmModeEnabled)
			{
				Label label36 = lblStatusMessage;
				label36.Content = label36.Content?.ToString() + (serviceModel.ActiveWindowMSFS ? ("MSM " + serviceModel.MsmMultiplier.ToString("F0") + "X") : "MSM Inact");
			}
			else
			{
				Label label37 = lblStatusMessage;
				label37.Content = label37.Content?.ToString() + "NFR";
			}
			Label label38 = lblStatusMessage;
			Label label39 = label38;
			string? text17 = label38.Content?.ToString();
			string text18;
			if (serviceModel.MemoryAccess.IsDX12())
			{
				text18 = (serviceModel.isMSFS2024 ? "" : " DX12");
			}
			else
			{
				text18 = " DX11";
			}
			label39.Content = text17 + text18;
			lblSpikeDetectProtStatus.Content = serviceModel.SpikeDetectProtStatus;
			if (serviceModel.IsSessionRunning)
			{
				float num;
				if (serviceModel.ActiveGraphicsMode == "Man FG")
				{
					num = ((serviceModel.ManFgMultiplier != 1 || !serviceModel.fpsSourceRTSS) ? (serviceModel.TargetFPS / (float)serviceModel.ManFgMultiplier) : (serviceModel.TargetFPS / (float)serviceModel.DynFgCurrentMultiplier));
				}
				else if (serviceModel.FgModeEnabled)
				{
					num = ((serviceModel.FgModeMultiplier != 1 || !serviceModel.fpsSourceRTSS) ? (serviceModel.TargetFPS / (float)serviceModel.FgModeMultiplier) : (serviceModel.TargetFPS / (float)serviceModel.DynFgCurrentMultiplier));
				}
				else
				{
					num = serviceModel.TargetFPS;
				}
				float num2 = ((!serviceModel.ActiveWindowMSFS) ? num : serviceModel.TargetFPS);
				if (serviceModel.MonitorMode)
				{
					Label label40 = lblStatusMessage;
					label40.Content = label40.Content?.ToString() + " | Monitor Mode";
				}
				else
				{
					if (serviceModel.MinTLODExtraSeeking)
					{
						Label label41 = lblStatusMessage;
						label41.Content = label41.Content?.ToString() + " | TLOD+ Seek";
					}
					else if ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2 || serviceModel.MinTLODExtra[serviceModel.activeProfile] || serviceModel.VRAMPlusEnabled) && serviceModel.FPSSettleActive)
					{
						Label label42 = lblStatusMessage;
						label42.Content = label42.Content?.ToString() + " | FPS Settle" + (serviceModel.FPSSettleInitial ? (" " + serviceModel.FPSSettleCount + "s") : "");
					}
					else
					{
						if (serviceModel.AutoTargetFPS && serviceModel.fixedFPSDetectedAutoTargetFPS)
						{
							Label label43 = lblStatusMessage;
							label43.Content = label43.Content?.ToString() + " | AFixedFPS";
						}
						if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] >= 2)
						{
							Label label44 = lblStatusMessage;
							label44.Content = label44.Content?.ToString() + ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 2) ? " | ATLOD" : " | FPSCap") + (serviceModel.MinTLODExtraActive ? "+" : "");
						}
						else
						{
							Label label45 = lblStatusMessage;
							label45.Content = label45.Content?.ToString() + (serviceModel.FixedTargetFPS ? " | FixedFPS" : "") + (serviceModel.FreeTLOD[serviceModel.activeProfile] ? "" : ((serviceModel.IsAppPriorityFPS ? " | FPS" : " | TLOD") + (serviceModel.MinTLODExtraActive ? "+" : "") + " Pri"));
						}
						Label label46 = lblStatusMessage;
						Label label47 = label46;
						string? text19 = label46.Content?.ToString();
						string text20;
						if (serviceModel.NightMode[serviceModel.activeProfile] <= 0)
						{
							text20 = "";
						}
						else
						{
							text20 = (serviceModel.NightTime ? " Night" : " Day");
						}
						label47.Content = text19 + text20 + (serviceModel.NightTimeTransiton ? " Δ" : "");
						Label label48 = lblSimTLOD;
						label48.Content = label48.Content?.ToString() + ((serviceModel.tlod >= serviceModel.SimMaxLOD) ? " ⊤" : "");
					}
					Label label49 = lblStatusMessage;
					label49.Content = label49.Content?.ToString() + ((((serviceModel.IsAppPriorityFPS && serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2) || serviceModel.FPSSettleActive || serviceModel.MinTLODExtraSeeking) && !serviceModel.FreeTLOD[serviceModel.activeProfile]) ? " | TLOD " : " ") + (serviceModel.FreeTLOD[serviceModel.activeProfile] ? ("| FreeTLOD " + serviceModel.MinTLOD[serviceModel.activeProfile].ToString("F0") + ((serviceModel.MinTLOD[serviceModel.activeProfile] != serviceModel.rangeMaxTLOD) ? ("-" + serviceModel.rangeMaxTLOD.ToString("F0")) : "")) : ((serviceModel.rangeMinTLOD == serviceModel.rangeMaxTLOD) ? ((object)serviceModel.rangeMinTLOD) : (serviceModel.rangeMinTLOD.ToString("F0") + "-" + serviceModel.rangeMaxTLOD.ToString("F0"))))?.ToString() + ((serviceModel.SpikeTLODReductionAmount > 0f) ? "⚡" : "") + ((serviceModel.TLODReduceAfterGearDown && serviceModel.LockTLODMax) ? "\ud83d\udd12" : "") + ((serviceModel.TLODExtraMtnsAmountResidual > 0f) ? ("M" + ((serviceModel.TLODExtraMtnsAmountResidual == (float)serviceModel.TLODExtraMtnsAmount[serviceModel.activeProfile]) ? "+" : "Δ")) : "");
				}
				object content4 = lblStatusMessage.Content;
				object obj2;
				if (content4 == null)
				{
					obj2 = null;
				}
				else
				{
					string? text21 = content4.ToString();
					obj2 = ((text21 != null) ? text21.Split('\n')[0] : null);
				}
				if (obj2 == null)
				{
					obj2 = "";
				}
				string text22 = (string)obj2;
				if (serviceModel.gpuUsage > 0f)
				{
					Label label50 = lblStatusMessage;
					label50.Content = label50.Content?.ToString() + ((serviceModel.VRAMProtInactive || text22.Length <= 43) ? (" | GPU " + serviceModel.gpuUsage.ToString("F0") + "%") : "") + ((serviceModel.gpuMemoryUsage > 0) ? (" | VRAM" + ((!serviceModel.VRAMPlusEnabled) ? " " : (serviceModel.VRAMHoldActive ? " HLD " : (serviceModel.VRAMReduceActive ? " RED " : "+ "))) + serviceModel.gpuMemoryUsage.ToString("F0") + "%") : "");
				}
				else if ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] >= 2 || serviceModel.DecCloudQMethod[serviceModel.activeProfile] == 1) && serviceModel.DecCloudQ[serviceModel.activeProfile])
				{
					Label label51 = lblStatusMessage;
					label51.Content = label51.Content?.ToString() + " | Start GPU-Z for CloudQ";
				}
				else if (serviceModel.gpuMemoryUsage == 0 && serviceModel.FPSSettleInitial && serviceModel.FPSSettleCount <= 18 && serviceModel.VRAMPlusEnabled)
				{
					Label label52 = lblStatusMessage;
					label52.Content = label52.Content?.ToString() + " | Start GPU-Z for VRAM+";
				}
				if (serviceModel.RTSSDataFrozen)
				{
					Label label53 = lblStatusMessage;
					label53.Content = label53.Content?.ToString() + " | RTSS❄\ufe0f";
					if ((!serviceModel.RTSSDataFrozen2DWarningSuppressed && serviceModel.ActiveGraphicsMode != "VR" && serviceModel.ActiveGraphicsMode != "OFXR") || serviceModel.ActiveGraphicsMode == "VXR" || (!serviceModel.RTSSDataFrozenVRWarningSuppressed && (serviceModel.ActiveGraphicsMode == "VR" || serviceModel.ActiveGraphicsMode == "OFXR" || serviceModel.ActiveGraphicsMode == "VXR")))
					{
						string text23 = "";
						string text24 = "";
						string text25 = "";
						if (serviceModel.fpsSourceRTSSDefault)
						{
							text23 = "the FPS source being automatically switched from RTSS back to MSFS";
						}
						if (serviceModel.SpikeTLODReductionEnabled)
						{
							text24 = "the Spike Protection feature being disabled due to RTSS data freezing";
						}
						text25 = text23 + ((text23 != "" && text24 != "") ? " and " : "") + text24;
						if (!serviceModel.RTSSDataFrozenVRWarningSuppressed && (serviceModel.ActiveGraphicsMode == "VR" || serviceModel.ActiveGraphicsMode == "OFXR" || serviceModel.ActiveGraphicsMode == "VXR"))
						{
							serviceModel.RTSSDataFrozenVRWarningSuppressed = true;
							if (MessageBox.Show("RTSS data froze when you switched to VR from a 2D FG graphics mode, resulting in " + text25 + ".\n\nThis is easiest avoided by setting frame generation to None in MSFS, since restarting RTSS each time you switch between 2D FG and VR is impractical.\n\nSee the readme for alternative, albeit conditional, solutions.\n\nDo you want to see this warning again in future runs of the app?", "RTSS Data Frozen", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
							{
								serviceModel.SetSetting("RTSSDataFrozenVRWarningSuppressed", "true");
							}
						}
						else if (!serviceModel.RTSSDataFrozen2DWarningSuppressed && serviceModel.ActiveGraphicsMode != "VR" && serviceModel.ActiveGraphicsMode != "OFXR" && serviceModel.ActiveGraphicsMode != "VXR")
						{
							serviceModel.RTSSDataFrozen2DWarningSuppressed = true;
							if (MessageBox.Show("RTSS data is frozen, resulting in " + text25 + ".\n\nThis usually occurs when changing FG type in MSFS without restarting the sim.\n\nThis can be resolved by restarting RTSS, or will be resolved the next time you run MSFS.\n\nSee the readme for alternative, albeit conditional, solutions.\n\nDo you want to see this warning again in future runs of the app?", "RTSS Data Frozen", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
							{
								serviceModel.SetSetting("RTSSDataFrozen2DWarningSuppressed", "true");
							}
						}
					}
				}
				if (serviceModel.AppPaused)
				{
					Label label54 = lblStatusMessage;
					label54.Content = label54.Content?.ToString() + " | ⏸";
				}
				if (serviceModel.LogPlus)
				{
					CpuSnapshot latestSnap = serviceModel._latestSnap;
					if ((object)latestSnap != null)
					{
						Label label49 = lblStatusMessage;
						label49.Content = label49.Content?.ToString() + $"\nCPU: {latestSnap.TotalCpuPercent:F0}% | " + ((latestSnap.StableCoreIndex is int) ? $"Dom {latestSnap.StableInstantPercent:F0}% | " : "") + $"Top{serviceModel.perfLogTopCoreCount}: {latestSnap.TopXMetric:F0}% | " + $"MSFS {latestSnap.MsfsCpuPercent:F0}%" + ((serviceModel.LastAppliedAffinityCount > 0) ? $" ({serviceModel.LastAppliedAffinityCount} Cores)" : "");
					}
				}
				if (serviceModel.ActiveGraphicsModeChanged)
				{
					LoadSettings();
				}
				float num3 = num2 * (float)serviceModel.FPSTolerance[serviceModel.activeProfile] / 100f;
				if ((serviceModel.FgModeEnabled || serviceModel.MsmModeEnabled) && !serviceModel.ActiveWindowMSFS)
				{
					lblSimFPS.Foreground = ThemeOrangeBrush;
				}
				else if ((serviceModel.fixedFPSDetectedAutoTargetFPS ? serviceModel.TargetFPS : serviceModel.fixedFPSDetected) == GetFPS())
				{
					lblSimFPS.Foreground = ThemeGreenBrush;
				}
				else if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] >= 2)
				{
					lblSimFPS.Foreground = ThemeTextBrush;
				}
				else if (GetFPS() < num2 - ((serviceModel.FixedTargetFPS && (serviceModel.tlod < tlodLast || serviceModel.tlod == serviceModel.MinTLOD[serviceModel.activeProfile])) ? 0f : num3))
				{
					lblSimFPS.Foreground = ThemeRedBrush;
				}
				else if ((!serviceModel.AutoTargetFPS || serviceModel.fixedFPSDetectedAutoTargetFPS) && GetFPS() > num2 + num3)
				{
					lblSimFPS.Foreground = ThemeGreenBrush;
				}
				else
				{
					lblSimFPS.Foreground = ThemeTextBrush;
				}
				var (flag4, flag5) = GetFixedFPSConfigMismatch();
				if ((flag4 | flag5) && serviceModel.fixedFPSDetectHistoryIndex < serviceModel.fixedFPSDetectHistory.Length)
				{
					serviceModel.fixedFPSDetectHistory[serviceModel.fixedFPSDetectHistoryIndex % serviceModel.fixedFPSDetectHistory.Length] = serviceModel.fixedFPSDetected;
					if (serviceModel.fixedFPSDetectHistoryIndex == serviceModel.fixedFPSDetectHistory.Length - 1 && serviceModel.fixedFPSDetectHistory.All((float x) => x == serviceModel.fixedFPSDetectHistory[0]))
					{
						Logger.Log(LogLevel.Warning, "MainWindow:UpdateLiveValues", $"Detected system Fixed FPS ({serviceModel.fixedFPSDetected}) {((serviceModel.fixedFPSDetected < serviceModel.TargetFPS) ? "lower than" : ((serviceModel.fixedFPSDetected > serviceModel.TargetFPS) ? "higher than" : "matching"))} the configured Target FPS ({serviceModel.TargetFPS}), preventing TLOD automation functioning correctly.");
						serviceModel.FixedFPSConfigWarning = true;
						fixedFPSWarningCountdown = 30;
					}
					else if (serviceModel.fixedFPSDetectHistoryIndex > 0 && !serviceModel.fixedFPSDetectHistory.Take(serviceModel.fixedFPSDetectHistoryIndex + 1).All((float x) => x == serviceModel.fixedFPSDetectHistory[0]))
					{
						Logger.Log(LogLevel.Warning, "MainWindow:UpdateLiveValues", "Potential Target FPS and detected Fixed FPS mismatch detected. History contains differing values: disabling further cap-detection correction.");
						serviceModel.fixedFPSDetectHistoryIndex = serviceModel.fixedFPSDetectHistory.Length;
					}
					else
					{
						serviceModel.fpsRawTrend.Reset();
						serviceModel.fixedFPSDetected = -1f;
						serviceModel.fixedFPSDetectAttempts = 0;
						int num4 = serviceModel.fixedFPSDetectHistoryIndex + 1;
						Logger.Log(LogLevel.Warning, "MainWindow:UpdateLiveValues", $"Potential Target FPS and detected Fixed FPS issue detected. Detection reset after {num4} consecutive {((num4 == 1) ? "match" : "matches")}.");
					}
					serviceModel.fixedFPSDetectHistoryIndex++;
				}
				if (serviceModel.MonitorMode)
				{
					lblSimTLOD.Foreground = ThemeBlueBrush;
				}
				else if (serviceModel.VRAMReduceActive)
				{
					lblSimTLOD.Foreground = ThemeIndianRedBrush;
				}
				else if (serviceModel.VRAMHoldActive)
				{
					lblSimTLOD.Foreground = ThemeSalmonBrush;
				}
				else if (serviceModel.MinTLODReductionAmount > 0f)
				{
					lblSimTLOD.Foreground = ThemeRedBrush;
				}
				else if ((serviceModel.FgModeEnabled || serviceModel.MsmModeEnabled) && !serviceModel.ActiveWindowMSFS)
				{
					lblSimTLOD.Foreground = ThemeOrangeBrush;
				}
				else if (((!serviceModel.FreeTLOD[serviceModel.activeProfile] && serviceModel.tlod == serviceModel.activeMinTLOD) || serviceModel.tlod == serviceModel.activeMaxTLOD) && (GetFPS() >= num2 || serviceModel.TLODAutoMethod[serviceModel.activeProfile] >= 2))
				{
					lblSimTLOD.Foreground = ThemeGreenBrush;
				}
				else if (serviceModel.tlod == serviceModel.MinTLOD[serviceModel.activeProfile] && GetFPS() < num2)
				{
					lblSimTLOD.Foreground = ThemeRedBrush;
				}
				else if (serviceModel.tlod_step && !serviceModel.AppPaused)
				{
					lblSimTLOD.Foreground = ThemeOrangeBrush;
				}
				else
				{
					lblSimTLOD.Foreground = ThemeTextBrush;
				}
				tlodLast = serviceModel.tlod;
				olodLast = serviceModel.olod;
				if (serviceModel.MonitorMode)
				{
					lblSimOLOD.Foreground = ThemeBlueBrush;
				}
				else if (serviceModel.VRAMReduceActive)
				{
					lblSimOLOD.Foreground = ThemeIndianRedBrush;
				}
				else if (serviceModel.VRAMHoldActive)
				{
					lblSimOLOD.Foreground = ThemeSalmonBrush;
				}
				else if (serviceModel.MinTLODReductionAmount > 0f)
				{
					lblSimOLOD.Foreground = ThemeRedBrush;
				}
				else if (serviceModel.CustomAutoOLOD[serviceModel.activeProfile] && (serviceModel.olod == serviceModel.activeOLODAtBase || serviceModel.olod == serviceModel.activeOLODAtTop))
				{
					lblSimOLOD.Foreground = ThemeGreenBrush;
				}
				else if (serviceModel.olod_step && !serviceModel.AppPaused)
				{
					lblSimOLOD.Foreground = ThemeOrangeBrush;
				}
				else
				{
					lblSimOLOD.Foreground = ThemeTextBrush;
				}
				if (serviceModel.AutoTargetFPS)
				{
					if (serviceModel.UpdateTargetFPS)
					{
						txtTargetFPS.Text = Convert.ToString(serviceModel.TargetFPS, CultureInfo.CurrentUICulture);
						serviceModel.UpdateTargetFPS = false;
					}
					else if (serviceModel.ForceAutoFPSCal)
					{
						txtTargetFPS.Text = "auto";
					}
				}
			}
			else
			{
				lblSimFPS.Foreground = ThemeTextBrush;
			}
		}
		else
		{
			if (serviceModel.ExitAppAfterFlightSession && firstSessionRan)
			{
				Force_WindowClosing();
			}
			stkDynSet.Visibility = Visibility.Collapsed;
			if (serviceModel.isMSFS2024 && ShowAutoReducePanel && (serviceModel.AutoReduceSettings[serviceModel.activeProfile] || serviceModel.VRAMPlusSuite == 2) && serviceModel.ReductionSettingsSuite[serviceModel.activeProfile] == 3)
			{
				stkpnlSettingsReduction.Visibility = Visibility.Visible;
			}
			else
			{
				stkpnlSettingsReduction.Visibility = Visibility.Collapsed;
			}
			if (serviceModel.MemoryCompatTestComplete)
			{
				bool flag6 = serviceModel.VrModeActive;
				if (!flag6)
				{
					bool flag7;
					switch (TargetFPSType)
					{
					case "VR":
					case "OFXR":
					case "VXR":
						flag7 = true;
						break;
					default:
						flag7 = false;
						break;
					}
					flag6 = flag7;
				}
				bool flag8 = flag6;
				lblSimTLOD.Content = (flag8 ? serviceModel.DefaultTLOD_VR : serviceModel.DefaultTLOD);
				lblSimTLOD.Foreground = ThemeGreenBrush;
				lblSimOLOD.Content = (flag8 ? serviceModel.DefaultOLOD_VR : serviceModel.DefaultOLOD);
				lblSimOLOD.Foreground = ThemeGreenBrush;
				lblSimReduce2.Content = ServiceModel.SettingQualityText(flag8 ? serviceModel.DefaultCloudQ_VR : serviceModel.DefaultCloudQ);
				lblSimReduce2.Foreground = ThemeGreenBrush;
				lblSimReduce.Content = "Clouds:";
				imgSimHeaderIcon.Visibility = Visibility.Visible;
				imgSimHeaderIcon2.Visibility = Visibility.Collapsed;
			}
			else
			{
				lblSimTLOD.Content = "n/a";
				lblSimTLOD.Foreground = ThemeTextBrush;
				lblSimOLOD.Content = "n/a";
				lblSimOLOD.Foreground = ThemeTextBrush;
				lblSimReduce2.Content = "n/a";
				lblSimReduce2.Foreground = ThemeTextBrush;
			}
			lblSimFPS.Foreground = ThemeTextBrush;
			if (sessionRunning && !serviceModel.deferredUpdate)
			{
				CheckForUpdates();
			}
			sessionRunning = false;
			cbUpdateOption.Visibility = Visibility.Visible;
			if (serviceModel.UpdateOption != 2 && (serviceModel.UseExpertOptions || serviceModel.UpdateChannel > 0))
			{
				cbUpdateChannel.Visibility = Visibility.Visible;
			}
			else
			{
				cbUpdateChannel.Visibility = Visibility.Collapsed;
			}
			stkStatusRow.HorizontalAlignment = HorizontalAlignment.Left;
			lblStatusMessage.HorizontalContentAlignment = HorizontalAlignment.Left;
			((DependencyObject)lblStatusMessage).SetValue(TextBlock.TextAlignmentProperty, (object)TextAlignment.Left);
			if (serviceModel.AutoUpdateJustCompleted)
			{
				lblStatusMessage.Foreground = ThemeGreenBrush;
				lblStatusMessage.Content = "Auto-update to 0.5.5.0 completed";
				if (firstSessionRan)
				{
					serviceModel.AutoUpdateJustCompleted = false;
				}
			}
			else if (serviceModel.deferredUpdate)
			{
				lblStatusMessage.Foreground = ThemeGreenBrush;
				lblStatusMessage.Content = serviceModel.deferredUpdateVersion + " installs when MSFS exits";
			}
			else if (!updateAvailable)
			{
				lblStatusMessage.Foreground = ThemeGreenBrush;
				lblStatusMessage.Content = "Waiting for flight session to start";
			}
		}
		UpdateStatusMessageFontSize();
		if (serviceModel.ReloadAppWindowSettings)
		{
			LoadSettings();
		}
	}

	protected void UpdateStatusMessageFontSize()
	{
		bool flag = serviceModel.isMSFS2024 && serviceModel.LogPlus && serviceModel.ShowDynSetButtonsInTestMode && serviceModel.UseExpertOptions;
		stkDynSet.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		int num = (lblStatusMessage.Content as string)?.Split('\n').Max((string line) => line.Trim().Length) ?? 0;
		double num2 = ((FontSize > 0.0) ? FontSize : 12.0);
		if (flag)
		{
			if (num > 51)
			{
				lblStatusMessage.FontSize = num2 * 0.875;
			}
			else if (num > 49)
			{
				lblStatusMessage.FontSize = num2 * (11.0 / 12.0);
			}
			else if (num > 47)
			{
				lblStatusMessage.FontSize = num2 * (23.0 / 24.0);
			}
			else
			{
				((DependencyObject)lblStatusMessage).ClearValue(Control.FontSizeProperty);
			}
		}
		else if (num > 60)
		{
			lblStatusMessage.FontSize = num2 * (11.0 / 12.0);
		}
		else if (num > 57)
		{
			lblStatusMessage.FontSize = num2 * (23.0 / 24.0);
		}
		else
		{
			((DependencyObject)lblStatusMessage).ClearValue(Control.FontSizeProperty);
		}
	}

	private (bool modeWarning, bool targetWarning) GetFixedFPSConfigMismatch()
	{
		if (serviceModel == null || serviceModel.fixedFPSDetected <= 0f || serviceModel.AutoTargetFPS)
		{
			return (modeWarning: false, targetWarning: false);
		}
		if (serviceModel.ActiveGraphicsMode == "OFXR" || serviceModel.ActiveGraphicsMode == "VXR")
		{
			return (modeWarning: false, targetWarning: false);
		}
		if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 2)
		{
			return (modeWarning: false, targetWarning: false);
		}
		float fixedFPSDetected = serviceModel.fixedFPSDetected;
		float targetFPS = serviceModel.TargetFPS;
		float num = fixedFPSDetected + 0.5f;
		if (targetFPS > num)
		{
			return (modeWarning: false, targetWarning: true);
		}
		float num2 = fixedFPSDetected * 0.95f - 0.5f;
		if (serviceModel.FixedTargetFPS)
		{
			if (targetFPS < num2)
			{
				return (modeWarning: true, targetWarning: true);
			}
			return (modeWarning: false, targetWarning: false);
		}
		float num3 = 5f + (serviceModel.SettingsReduceEnabled ? ((float)serviceModel.FPSTolerance[serviceModel.activeProfile]) : 0f);
		float num4 = fixedFPSDetected * (1f - num3 / 100f);
		if (targetFPS > num4)
		{
			return (modeWarning: true, targetWarning: false);
		}
		return (modeWarning: false, targetWarning: false);
	}

	protected void UpdateAircraftValues()
	{
		if (IPCManager.SimConnect != null && IPCManager.SimConnect.IsConnected)
		{
			MobiSimConnect simConnect = IPCManager.SimConnect;
			lblPlaneAGL.Content = simConnect.ReadSimVar("PLANE ALT ABOVE GROUND", "feet").ToString("F0");
			serviceModel.verticalSpeed = (float)Math.Round(simConnect.ReadSimVar("VERTICAL SPEED", "feet per second") * 60f);
			if (serviceModel.verticalSpeed == 0f)
			{
				serviceModel.verticalSpeed = 0f;
			}
			lblPlaneVS.Content = serviceModel.verticalSpeed.ToString("F0");
		}
		else
		{
			lblPlaneAGL.Content = "n/a";
			lblPlaneVS.Content = "n/a";
		}
	}

	protected void OnTick(object sender, EventArgs e)
	{
		lodChecker.Tick();
		if (serviceModel.RememberWindowPos && !serviceModel.VrModeActive && WindowState != WindowState.Minimized)
		{
			if ((int)Left >= serviceModel.virtualScreenMinX && (int)Left < serviceModel.virtualScreenMaxX - 10 && (int)Left != serviceModel.windowLeft)
			{
				serviceModel.windowLeft = (int)Left;
				serviceModel.ConfigurationFileCommon.SetSetting("windowLeft", serviceModel.windowLeft.ToString().ToLower());
			}
			if ((int)Top >= serviceModel.virtualScreenMinY && (int)Top < serviceModel.virtualScreenMaxY - 10 && (int)Top != serviceModel.windowTop)
			{
				serviceModel.windowTop = (int)Top;
				serviceModel.ConfigurationFileCommon.SetSetting("windowTop", serviceModel.windowTop.ToString().ToLower());
			}
		}
		UpdateStatus();
		if (serviceModel.UnableToAttachMSFS || !serviceModel.AppEnabled)
		{
			WindowState = WindowState.Normal;
			if (serviceModel.RememberWindowPos)
			{
				Top = serviceModel.windowTop;
				Left = serviceModel.windowLeft;
			}
			Show();
			lblStatusMessage.Foreground = new SolidColorBrush(Colors.Red);
			lblSimTLOD.Content = "n/a";
			lblSimTLOD.Foreground = new SolidColorBrush(Colors.Red);
			lblSimOLOD.Content = "n/a";
			lblSimOLOD.Foreground = new SolidColorBrush(Colors.Red);
			lblSimReduce2.Content = "n/a";
			lblSimReduce2.Foreground = new SolidColorBrush(Colors.Red);
			lblSimFPS.Content = "n/a";
			lblSimFPS.Foreground = new SolidColorBrush(Colors.Red);
			lblPlaneAGL.Content = "n/a";
			lblPlaneAGL.Foreground = new SolidColorBrush(Colors.Red);
			lblPlaneVS.Content = "n/a";
			lblPlaneVS.Foreground = new SolidColorBrush(Colors.Red);
		}
		if (serviceModel.UnableToAttachMSFS)
		{
			lblStatusMessage.Content = "Unable to attach MSFS - app disabled.";
			MessageBox.Show(this, "The most likely causes are that MSFS is loading in very slowly and the attachment process is timing out, MSFS is running at a higher privilege level, or your anti-virus/malware app is blocking this app.\n\nTo resolve, try the following:\n1. Restart this app after MSFS has loaded in to the main menu.\n2. Check that MSFS is not running as administrator.\n3. Set an exclusion for this app in your anti-virus/malware app.\n4. If all else fails, try running this app as administrator. ", "Unable to attach MSFS - app disabled.", MessageBoxButton.OK, MessageBoxImage.Hand);
			serviceModel.ServiceExited = true;
		}
		else if (!serviceModel.AppEnabled || (serviceModel.MemoryCompatTestComplete && !serviceModel.SettingsReduceEnabled && !AttemptedExplicitCompatUpdate))
		{
			string filePath = "releases.json";
			string text = FindCompatibleReleaseUrl(filePath, serviceModel.DetectedSimVersion, out var isExplicit);
			string appVersionStr = null;
			bool flag = false;
			if (text != null)
			{
				MatchCollection matchCollection = Regex.Matches(text, "[0-9]+\\.[0-9]+\\.[0-9]+(?:\\.[0-9]+)?(?:-(test|RC|exp)\\d+)?", RegexOptions.IgnoreCase);
				Match match = ((matchCollection.Count > 0) ? matchCollection[matchCollection.Count - 1] : null);
				if (match != null && match.Success)
				{
					appVersionStr = match.Value;
					if (CompareAppVersions(appVersionStr, "0.5.5.0") > 0)
					{
						flag = true;
						Logger.Log(LogLevel.Information, "MainWindow:OnTick", $"Found newer compatible release {appVersionStr} (current {"0.5.5.0"}) from URL: {text}");
						if (!serviceModel.TestUpdates && match.Groups[1].Success)
						{
							serviceModel.ConfigurationFileCommon.SetSetting("CompatTestUpgrader", "true");
							Logger.Log(LogLevel.Information, "MainWindow:OnTick", "Set CompatTestUpgrader in common config file due to compatibility fallback to test version");
						}
					}
					else
					{
						Logger.Log(LogLevel.Information, "MainWindow:OnTick", $"Compatible release {appVersionStr} is not newer than current {"0.5.5.0"}, skipping AutoUpdate.");
					}
				}
				else
				{
					Logger.Log(LogLevel.Warning, "MainWindow:OnTick", "Compatible release URL found but version string could not be parsed: " + text);
				}
			}
			else
			{
				Logger.Log(LogLevel.Information, "MainWindow:OnTick", "No compatible release URL found for MSFS version " + serviceModel.DetectedSimVersion + ".");
			}
			if (!serviceModel.SettingsReduceEnabled & isExplicit & flag)
			{
				((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)(() =>
				{
					((MainWindow)Application.Current.MainWindow).AutoUpdate(appVersionStr, Mandatory: false, UpdateForCompat: true, isExplicit);
				}));
			}
			else if (!serviceModel.AppEnabled)
			{
				if (flag)
				{
					((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)(() =>
					{
						((MainWindow)Application.Current.MainWindow).AutoUpdate(appVersionStr, Mandatory: false, UpdateForCompat: true, isExplicit);
					}));
				}
				else
				{
					if (App.IsRunningAsAdmin())
					{
						MessageBox.Show("The MSFS compatibility test failed.\n\n" + appName + " is currently running with Administrator privileges. Running " + appName + " elevated can prevent proper communication with MSFS and is a known cause of compatibility test failures.\n\nPlease restart " + appName + " without Administrator privileges.", "Compatibility Test Failed", MessageBoxButton.OK, MessageBoxImage.Hand);
						Logger.Log(LogLevel.Information, "MainWindow:OnTick", appName + " running as Admin when compatibility test failed. User advised to restart without Admin privileges.");
					}
					else
					{
						lblStatusMessage.Content = "MSFS compatibility test failed - app disabled.";
						string text2 = ((!((text != null && appVersionStr != null && CompareAppVersions(appVersionStr, "0.5.5.0") <= 0) & isExplicit)) ? "This app version is not explicitly compatible with the current MSFS version, and no newer potentially compatible version was found. Compatibility is therefore indeterminate.\n\n" : "This app version is explicitly compatible with the current MSFS version, so the compatibility test should pass. Please try the following after closing this app.\n\n");
						switch (CustomMessageBox.Show("MSFS compatibility test failed - app disabled.\n\n" + text2 + "1. Go to the MSFS settings menu, toggle any simple setting (e.g. VSync), save changes, then restart this app.\n\n2. Exit MSFS completely, start MSFS, then start this app when in the MSFS main menu.\n\n3. Reboot your system and try again.\n\n4. If still unresolved, click Report to raise an issue if one doesn't already exist.\n\n5. More solutions can be found in the readme.", global::AppConfig.Config.RepoUrl + "/issues", "Critical", "Report Issue"))
						{
						case CustomMessageBoxResult.Action:
							Logger.Log(LogLevel.Information, "MainWindow:OnTick", "User selected 'Report Issue' after compatibility test failure.");
							break;
						case CustomMessageBoxResult.Close:
							Logger.Log(LogLevel.Information, "MainWindow:OnTick", "User dismissed compatibility failure message without action.");
							break;
						}
					}
					serviceModel.ServiceExited = true;
				}
			}
			AttemptedExplicitCompatUpdate = true;
		}
		else
		{
			UpdateLiveValues();
		}
		UpdateAircraftValues();
	}

	private void chkDarkMode_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.ConfigurationFileCommon.SetSetting("DarkMode", (serviceModel.isDarkMode = chkDarkMode.IsChecked.Value).ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:chkDarkMode_Click", chkDarkMode.IsChecked.ToString().ToLower());
		LoadSettings(noLogging: true);
	}

	private void chkOnTop_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting("OnTop", chkOnTop.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:chkOnTop_Click", chkOnTop.IsChecked.ToString().ToLower());
		LoadSettings(noLogging: true);
	}

	private void btnDynSet_Click(object sender, RoutedEventArgs e)
	{
		if (this.serviceModel.MemoryAccess != null)
		{
			if (this.serviceModel.VrModeActive)
			{
				MemoryManager memoryAccess = this.serviceModel.MemoryAccess;
				bool dynSet_VR = (this.serviceModel.DynSet_VR = !this.serviceModel.DynSet_VR);
				memoryAccess.SetDynSet_VR(dynSet_VR);
			}
			else
			{
				MemoryManager memoryAccess2 = this.serviceModel.MemoryAccess;
				bool dynSet_VR = (this.serviceModel.DynSet = !this.serviceModel.DynSet);
				memoryAccess2.SetDynSet_PC(dynSet_VR);
			}
			ServiceModel serviceModel = this.serviceModel;
			int defaultDynSetFrameRateTarget = (this.serviceModel.DynSetTargetFPS = this.serviceModel.MemoryAccess.GetDynSetFrameRateTarget());
			serviceModel.DefaultDynSetFrameRateTarget = defaultDynSetFrameRateTarget;
			ServiceModel serviceModel2 = this.serviceModel;
			defaultDynSetFrameRateTarget = (this.serviceModel.DynSetTargetFPS_VR = this.serviceModel.MemoryAccess.GetDynSetFrameRateTarget(VR: true));
			serviceModel2.DefaultDynSetFrameRateTarget_VR = defaultDynSetFrameRateTarget;
			stkDynSetChange.Visibility = ((!(this.serviceModel.VrModeActive ? this.serviceModel.DynSet_VR : this.serviceModel.DynSet)) ? Visibility.Collapsed : Visibility.Visible);
			Logger.Log(LogLevel.Information, "MainWindow:btnDynSet_Click", (this.serviceModel.VrModeActive ? this.serviceModel.DynSet_VR : this.serviceModel.DynSet) ? ("On " + (this.serviceModel.VrModeActive ? this.serviceModel.DynSetTargetFPS_VR : this.serviceModel.DynSetTargetFPS)) : "Off");
			this.serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void btnDynSetPlus_Click(object sender, RoutedEventArgs e)
	{
		if (this.serviceModel.MemoryAccess != null)
		{
			if (this.serviceModel.VrModeActive)
			{
				MemoryManager memoryAccess = this.serviceModel.MemoryAccess;
				ServiceModel serviceModel = this.serviceModel;
				int num = (this.serviceModel.DynSetTargetFPS_VR = Math.Min(120, Math.Max(20, this.serviceModel.DynSetTargetFPS_VR + 5)));
				int value = (serviceModel.DefaultDynSetFrameRateTarget_VR = num);
				memoryAccess.SetDynSetFrameRateTargetVR(value);
			}
			else
			{
				MemoryManager memoryAccess2 = this.serviceModel.MemoryAccess;
				ServiceModel serviceModel2 = this.serviceModel;
				int num = (this.serviceModel.DynSetTargetFPS = Math.Min(120, Math.Max(20, this.serviceModel.DynSetTargetFPS + 5)));
				int value = (serviceModel2.DefaultDynSetFrameRateTarget = num);
				memoryAccess2.SetDynSetFrameRateTarget(value);
			}
			Logger.Log(LogLevel.Information, "MainWindow:btnDynSetPlus_Click", "+ 5 to " + (this.serviceModel.VrModeActive ? this.serviceModel.DynSetTargetFPS_VR : this.serviceModel.DynSetTargetFPS));
		}
	}

	private void btnDynSetMinus_Click(object sender, RoutedEventArgs e)
	{
		if (this.serviceModel.MemoryAccess != null)
		{
			if (this.serviceModel.VrModeActive)
			{
				MemoryManager memoryAccess = this.serviceModel.MemoryAccess;
				ServiceModel serviceModel = this.serviceModel;
				int num = (this.serviceModel.DynSetTargetFPS_VR = Math.Min(120, Math.Max(20, this.serviceModel.DynSetTargetFPS_VR - 5)));
				int value = (serviceModel.DefaultDynSetFrameRateTarget_VR = num);
				memoryAccess.SetDynSetFrameRateTargetVR(value);
			}
			else
			{
				MemoryManager memoryAccess2 = this.serviceModel.MemoryAccess;
				ServiceModel serviceModel2 = this.serviceModel;
				int num = (this.serviceModel.DynSetTargetFPS = Math.Min(120, Math.Max(20, this.serviceModel.DynSetTargetFPS - 5)));
				int value = (serviceModel2.DefaultDynSetFrameRateTarget = num);
				memoryAccess2.SetDynSetFrameRateTarget(value);
			}
			Logger.Log(LogLevel.Information, "MainWindow:btnDynSetMinus_Click", "- 5 to " + (this.serviceModel.VrModeActive ? this.serviceModel.DynSetTargetFPS_VR : this.serviceModel.DynSetTargetFPS));
		}
	}

	private void cbUpdateOption_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel != null && cbUpdateOption.SelectedIndex != ((serviceModel.UpdateOption != 0 || !serviceModel.SilentUpdates) ? (serviceModel.UpdateOption + 1) : 0))
		{
			serviceModel.ConfigurationFileCommon.SetSetting("UpdateOption", Math.Max(cbUpdateOption.SelectedIndex - 1, 0).ToString());
			if (cbUpdateOption.SelectedIndex == 0)
			{
				serviceModel.ConfigurationFileCommon.SetSetting("SilentUpdates", "true");
				serviceModel.SilentUpdates = true;
			}
			else
			{
				serviceModel.ConfigurationFileCommon.SetSetting("SilentUpdates", "false");
				serviceModel.SilentUpdates = false;
			}
			serviceModel.UpdateOption = Math.Max(cbUpdateOption.SelectedIndex - 1, 0);
			serviceModel.deferredUpdate = false;
			CheckForUpdates();
		}
	}

	private void cbTargetFPSOption_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel != null && cbTargetFPSOption.SelectedIndex != (serviceModel.AutoTargetFPS ? 1 : (serviceModel.FixedTargetFPS ? 2 : 0)))
		{
			if (cbTargetFPSOption.SelectedIndex == 0)
			{
				serviceModel.AutoTargetFPS = false;
				serviceModel.FixedTargetFPS = false;
			}
			else if (cbTargetFPSOption.SelectedIndex == 1)
			{
				serviceModel.AutoTargetFPS = true;
				serviceModel.FixedTargetFPS = false;
			}
			else if (cbTargetFPSOption.SelectedIndex == 2)
			{
				serviceModel.AutoTargetFPS = false;
				serviceModel.FixedTargetFPS = true;
			}
			serviceModel.ConfigurationFile.SetSetting("AutoTargetFPS" + (serviceModel.FlightTypeIFR ? serviceModel.profileSuffix : "_VFR"), serviceModel.AutoTargetFPS.ToString().ToLower());
			serviceModel.ConfigurationFile.SetSetting("FixedTargetFPS" + (serviceModel.FlightTypeIFR ? serviceModel.profileSuffix : "_VFR"), serviceModel.FixedTargetFPS.ToString().ToLower());
			string text;
			if (serviceModel.AutoTargetFPS)
			{
				text = "Auto";
			}
			else
			{
				text = (serviceModel.FixedTargetFPS ? "Fixed" : "Manual");
			}
			Logger.Log(LogLevel.Information, "MainWindow:cbTargetFPSOption_SelectionChange", text.ToString());
			serviceModel.LoadConfiguration();
			LoadSettings();
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void cbTargetFPSType_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel == null || cbTargetFPSType.SelectedItem == null)
		{
			return;
		}
		TargetFPSType = cbTargetFPSType.SelectedItem.ToString();
		if (TargetFPSType == "VR")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_VR : serviceModel.TargetFPS_VR_VFR, CultureInfo.CurrentUICulture);
		}
		else if (TargetFPSType == "OFXR")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_OFXR : serviceModel.TargetFPS_OFXR_VFR, CultureInfo.CurrentUICulture);
		}
		else if (TargetFPSType == "VXR")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_VXR : serviceModel.TargetFPS_VXR_VFR, CultureInfo.CurrentUICulture);
		}
		else if (TargetFPSType == "FG")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_FG : serviceModel.TargetFPS_FG_VFR, CultureInfo.CurrentUICulture);
		}
		else if (TargetFPSType == "LSFG")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_LS : serviceModel.TargetFPS_LS_VFR, CultureInfo.CurrentUICulture);
		}
		else if (TargetFPSType == "FSR")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_FSR : serviceModel.TargetFPS_FSR_VFR, CultureInfo.CurrentUICulture);
		}
		else if (TargetFPSType == "MSM")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_MSM : serviceModel.TargetFPS_MSM_VFR, CultureInfo.CurrentUICulture);
		}
		else if (TargetFPSType == "Man FG")
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_ManFG : serviceModel.TargetFPS_ManFG_VFR, CultureInfo.CurrentUICulture);
		}
		else
		{
			txtTargetFPS.Text = Convert.ToString(serviceModel.FlightTypeIFR ? serviceModel.TargetFPS_PC : serviceModel.TargetFPS_PC_VFR, CultureInfo.CurrentUICulture);
		}
		if (serviceModel.MemoryAccess == null || TargetFPSType == "Man FG" || serviceModel.ActiveGraphicsMode == "Man FG")
		{
			string activeGraphicsMode = serviceModel.ActiveGraphicsMode;
			serviceModel.ActiveGraphicsMode = TargetFPSType;
			serviceModel.ConfigurationFile.SetSetting("ActiveGraphicsMode", serviceModel.ActiveGraphicsMode);
			Logger.Log(LogLevel.Information, "MainWindow:cbTargetFPSType_SelectionChange", "Default graphics mode " + ((activeGraphicsMode != serviceModel.ActiveGraphicsMode) ? "changed to" : "is") + " " + serviceModel.ActiveGraphicsMode);
			cbTargetFPSType.Background = ThemeInputBgBrush;
			if (serviceModel.ActiveGraphicsMode == "Man FG")
			{
				LoadSettings();
			}
		}
		else if (TargetFPSType != serviceModel.ActiveGraphicsMode)
		{
			Logger.Log(LogLevel.Information, "MainWindow:cbTargetFPSType_SelectionChange", "Non-active graphics mode changed to " + TargetFPSType);
			cbTargetFPSType.Background = new SolidColorBrush(Colors.Orange);
		}
		else
		{
			cbTargetFPSType.Background = ThemeInputBgBrush;
		}
	}

	private void cbManFgMultiplier_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel == null)
		{
			return;
		}
		int num = cbManFgMultiplier.SelectedIndex;
		if (num >= 0 && num < cbManFgMultiplier.Items.Count)
		{
			int num2;
			switch (num)
			{
			case 0:
				serviceModel.ActiveGraphicsMode = "NFR";
				serviceModel.ConfigurationFile.SetSetting("ActiveGraphicsMode", serviceModel.ActiveGraphicsMode);
				Logger.Log(LogLevel.Information, "MainWindow:cbManFgMultiplier_SelectionChange", "Auto → NFR");
				LoadSettings();
				serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
				return;
			default:
				num2 = num + 1;
				break;
			case 8:
				num2 = 1;
				break;
			}
			int num3 = num2;
			if (serviceModel.ManFgMultiplier != num3)
			{
				serviceModel.ManFgMultiplier = num3;
				serviceModel.SetSetting("ManFgMultiplier", num3.ToString(CultureInfo.InvariantCulture));
				Logger.Log(LogLevel.Information, "MainWindow:cbManFgMultiplier_SelectionChange", num3.ToString(CultureInfo.InvariantCulture));
				LoadSettings();
				serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
			}
		}
	}

	private void cbUpdateChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (serviceModel != null && cbUpdateChannel.SelectedIndex != serviceModel.UpdateChannel)
		{
			serviceModel.UpdateChannel = cbUpdateChannel.SelectedIndex;
			serviceModel.ConfigurationFileCommon.SetSetting("UpdateChannel", serviceModel.UpdateChannel.ToString());
			serviceModel.TestUpdates = serviceModel.UpdateChannel > 0;
			serviceModel.ConfigurationFileCommon.SetSetting("TestUpdates", serviceModel.TestUpdates.ToString().ToLower());
			_ = serviceModel.TestUpdates;
			Logger.Log(LogLevel.Information, "MainWindow:cbUpdateChannel_SelectionChanged", "Update channel: " + serviceModel.UpdateChannel switch
			{
				0 => "Release only", 
				1 => "RC+", 
				2 => "Test+", 
				3 => "Exp+", 
				_ => "Unknown", 
			});
			LoadSettings();
			serviceModel.deferredUpdate = false;
			CheckForUpdates();
		}
	}

	private void cbActiveProfile_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel == null || cbActiveProfile.SelectedIndex < 0 || serviceModel.FlightTypeExpert == cbActiveProfile.SelectedIndex)
		{
			return;
		}
		serviceModel.FlightTypeExpert = cbActiveProfile.SelectedIndex;
		if (serviceModel.UseExpertOptions)
		{
			serviceModel.SetSetting("FlightTypeExpertExtended", cbActiveProfile.SelectedIndex.ToString());
		}
		else
		{
			serviceModel.SetSetting("FlightTypeIFR", (cbActiveProfile.SelectedIndex != 1).ToString());
		}
		Logger.Log(LogLevel.Information, "MainWindow:cbActiveProfile_SelectionChange", cbActiveProfile.SelectedIndex.ToString());
		if (!serviceModel.UseExpertOptions)
		{
			if (serviceModel.AutoTargetFPS)
			{
				serviceModel.MinTLODExtra[0] = false;
			}
			else
			{
				serviceModel.MinTLODExtra[0] = true;
			}
		}
		else if (serviceModel.MemoryAccess == null && serviceModel.ProfileName[cbActiveProfile.SelectedIndex].EndsWith(" VR"))
		{
			cbTargetFPSType.SelectedIndex = 1;
		}
		serviceModel.LoadConfiguration();
		LoadSettings();
		serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
	}

	private void cbActiveProfile_Loaded(object sender, RoutedEventArgs e)
	{
		((DispatcherObject)cbActiveProfile).Dispatcher.InvokeAsync((Action)(() =>
		{
			if (cbActiveProfile.Template.FindName("PART_EditableTextBox", cbActiveProfile) is TextBox textBox)
			{
				textBox.LostFocus -= TextBox_TextChangedOrLostFocusOrEnter;
				textBox.LostFocus += TextBox_TextChangedOrLostFocusOrEnter;
				textBox.KeyDown -= TextBox_TextChangedOrLostFocusOrEnter;
				textBox.KeyDown += TextBox_TextChangedOrLostFocusOrEnter;
				if (cbActiveProfile.SelectedItem is string text)
				{
					int num = text.LastIndexOf(" ↔");
					textBox.Text = ((num != -1) ? text.Substring(0, num) : text);
					textBox.CaretIndex = textBox.Text.Length;
				}
			}
		}), (DispatcherPriority)6);
	}

	private void TextBox_TextChangedOrLostFocusOrEnter(object sender, RoutedEventArgs e)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		TextBox textBox = sender as TextBox;
		if (e is KeyEventArgs e2 && (int)e2.Key != 6)
		{
			return;
		}
		selectedIndex = cbActiveProfile.SelectedIndex;
		if (selectedIndex >= 0 && textBox.Text != "" && (string)cbActiveProfile.Items[selectedIndex] != textBox.Text)
		{
			cbActiveProfile.Items[selectedIndex] = textBox.Text;
			string text;
			if (selectedIndex != 0)
			{
				text = ((selectedIndex == 1) ? "VFR" : (selectedIndex - 1).ToString());
			}
			else
			{
				text = "IFR";
			}
			serviceModel.SetSetting("ProfileName" + text, (string)cbActiveProfile.Items[selectedIndex]);
			cbActiveProfile.SelectedIndex = selectedIndex;
			if ((serviceModel.ProfileName[serviceModel.FlightTypeExpert].EndsWith(" VR") && Enumerable.Contains(serviceModel.ProfileName, serviceModel.ProfileName[serviceModel.FlightTypeExpert].Substring(0, serviceModel.ProfileName[serviceModel.FlightTypeExpert].Length - 3))) || Enumerable.Contains(serviceModel.ProfileName, serviceModel.ProfileName[serviceModel.FlightTypeExpert] + " VR"))
			{
				MessageBox.Show(this, "This profile will auto-switch with its VR/non-VR counterpart; rename it if unintended.", "Profile Auto-Switching Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			Logger.Log(LogLevel.Information, "MainWindow:TextBox_ActiveProfile", "Index:" + selectedIndex + " Text:" + (string)cbActiveProfile.Items[selectedIndex]);
			LoadSettings();
		}
	}

	private void cbActiveProfile_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (serviceModel.UseExpertOptions)
		{
			cbActiveProfile.IsEditable = !cbActiveProfile.IsEditable;
		}
		cbActiveProfile_Loaded(sender, null);
	}

	private void chkUseExpertOptions_Click(object sender, RoutedEventArgs e)
	{
		Logger.Log(LogLevel.Information, "MainWindow:chkUseExpertOptions_Click", chkUseExpertOptions.IsChecked.ToString().ToLower());
		if (serviceModel.MemoryAccess != null && !serviceModel.UseExpertOptions && chkUseExpertOptions.IsChecked.Value)
		{
			serviceModel.ResetDynamicSettingsState();
		}
		else if (serviceModel.MemoryAccess != null && serviceModel.UseExpertOptions && !chkUseExpertOptions.IsChecked.Value)
		{
			serviceModel.SetDynamicSettingsState();
		}
		serviceModel.SetSetting("useExpertOptions", chkUseExpertOptions.IsChecked.ToString().ToLower());
		serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		LoadSettings();
		if (serviceModel.UseExpertOptions && !serviceModel.MonitorMode)
		{
			stkpnlExpertSettings.Visibility = Visibility.Visible;
			serviceModel.windowPanelState = 0;
			serviceModel.SetSetting("windowPanelState", serviceModel.windowPanelState.ToString().ToLower(), noLoad: true);
		}
		else
		{
			stkpnlExpertSettings.Visibility = Visibility.Collapsed;
		}
		if (serviceModel.MemoryAccess != null)
		{
			if (serviceModel.CustomAutoOLOD[serviceModel.activeProfile] && serviceModel.UseExpertOptions)
			{
				serviceModel.olod = serviceModel.MemoryAccess.SetOLOD(serviceModel.OLODAtBase[serviceModel.activeProfile]);
			}
			else if (serviceModel.VrModeActive)
			{
				serviceModel.olod = serviceModel.MemoryAccess.SetOLOD(serviceModel.DefaultOLOD_VR);
			}
			else
			{
				serviceModel.olod = serviceModel.MemoryAccess.SetOLOD(serviceModel.DefaultOLOD);
			}
		}
	}

	private void btnReset_Click(object sender, RoutedEventArgs e)
	{
		Logger.Log(LogLevel.Information, "MainWindow:btnReset_Click", "User");
		if (!serviceModel.IsSimRunning)
		{
			serviceModel.isMSFS2024 = (serviceModel.isMSFS2024_last = !serviceModel.isMSFS2024);
			if (cbActiveProfile.Items.Count > 2)
			{
				for (int num = cbActiveProfile.Items.Count - 1; num >= 2; num--)
				{
					cbActiveProfile.Items.RemoveAt(num);
				}
			}
			serviceModel.LoadConfiguration();
			serviceModel.ReloadAppWindowSettings = true;
		}
		else if (serviceModel.IsSessionRunning)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
		else if (MessageBox.Show("Reset current flight type profile to defaults?", "Reset Profile", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
		{
			serviceModel.LoadConfiguration(resetDefaults: true);
			serviceModel.ReloadAppWindowSettings = true;
		}
	}

	private void cbTLODAutoMethod_SelectionChanged(object sender, EventArgs e)
	{
		if (serviceModel != null && serviceModel.TLODAutoMethod[serviceModel.activeProfile] != cbTLODAutoMethod.SelectedIndex)
		{
			serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("TLODAutoMethod" + serviceModel.profileSuffix) : "TLODAutoMethod_VFR", cbTLODAutoMethod.SelectedIndex.ToString());
			serviceModel.TLODAutoMethod[serviceModel.activeProfile] = cbTLODAutoMethod.SelectedIndex;
			Logger.Log(LogLevel.Information, "MainWindow:cbTLODAutoMethod_SelectionChange", cbTLODAutoMethod.SelectedIndex.ToString().ToLower());
			serviceModel.LoadConfiguration();
			LoadSettings();
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkSpikeProtect_Click(object sender, RoutedEventArgs e)
	{
		if (serviceModel != null && serviceModel.SpikeTLODReductionEnabled != chkSpikeProtect.IsChecked)
		{
			serviceModel.SpikeTLODReductionEnabled = chkSpikeProtect.IsChecked == true;
			serviceModel.ConfigurationFileCommon.SetSetting("SpikeTLODReductionEnabled", serviceModel.SpikeTLODReductionEnabled.ToString().ToLower());
			Logger.Log(LogLevel.Information, "MainWindow:chkSpikeProtect_Click", chkSpikeProtect.IsChecked.ToString().ToLower());
			if (!serviceModel.SpikeTLODReductionEnabled)
			{
				serviceModel.SpikeTLODReductionAmount = 0f;
				serviceModel.SpikeTLODReductionTickMark = 0L;
				serviceModel.SpikeDetected = false;
			}
			LoadSettings();
		}
	}

	private void btnSpikeRec_Click(object sender, RoutedEventArgs e)
	{
		if (serviceModel != null && serviceModel.MemoryAccess != null)
		{
			Logger.Log(LogLevel.Information, "MainWindow:btnSpikeRec_Click", "User initiated recording of last 10s of frame times");
			serviceModel.GetRTSSFrameTimes(record: true);
		}
	}

	private void chkCustomAutoOLOD_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("customAutoOLOD" + serviceModel.profileSuffix) : "customAutoOLOD_VFR", chkCustomAutoOLOD.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:chkCustomAutoOLOD_Click", chkCustomAutoOLOD.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null && serviceModel.CustomAutoOLOD[serviceModel.activeProfile])
		{
			MemoryManager memoryAccess = serviceModel.MemoryAccess;
			float oLOD_PC = (serviceModel.olod = serviceModel.DefaultOLOD);
			memoryAccess.SetOLOD_PC(oLOD_PC);
			MemoryManager memoryAccess2 = serviceModel.MemoryAccess;
			oLOD_PC = (serviceModel.olod = serviceModel.DefaultOLOD_VR);
			memoryAccess2.SetOLOD_VR(oLOD_PC);
		}
	}

	private void chkOLODPanelHide_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting("OLODPanelHide", chkOLODPanelHide.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:chkOLODPanelHide_Click", chkOLODPanelHide.IsChecked.ToString().ToLower());
		LoadSettings(noLogging: true);
	}

	private void cbCloudMethod_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel != null && serviceModel.DecCloudQMethod[serviceModel.activeProfile] != cbCloudMethod.SelectedIndex)
		{
			serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("DecCloudQMethod" + serviceModel.profileSuffix) : "DecCloudQMethod_VFR", cbCloudMethod.SelectedIndex.ToString());
			Logger.Log(LogLevel.Information, "MainWindow:cbCloudMethod_SelectionChange", cbCloudMethod.SelectedIndex.ToString().ToLower());
			serviceModel.ResetCloudsLODs();
			LoadSettings();
		}
	}

	private void chkCloudRecoveryPlus_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("CloudRecoveryPlus" + serviceModel.profileSuffix) : "CloudRecoveryPlus_VFR", chkCloudRecoveryPlus.IsChecked.ToString().ToLower());
		if ((chkCloudRecoveryPlus.IsChecked.Value && (serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile] < 5f || serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile] > serviceModel.MaxTLOD[serviceModel.activeProfile] - serviceModel.MinTLOD[serviceModel.activeProfile] - 5f)) || (!chkCloudRecoveryPlus.IsChecked.Value && (serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile] < serviceModel.MinTLOD[serviceModel.activeProfile] + 5f || serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile] > serviceModel.MaxTLOD[serviceModel.activeProfile] - 5f)))
		{
			serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("CloudRecoveryTLOD" + serviceModel.profileSuffix) : "CloudRecoveryTLOD_VFR", Convert.ToString(Math.Round((chkCloudRecoveryPlus.IsChecked.Value ? 0f : serviceModel.MinTLOD[serviceModel.activeProfile]) + 2f * (serviceModel.MaxTLOD[serviceModel.activeProfile] - serviceModel.MinTLOD[serviceModel.activeProfile]) / 5f), CultureInfo.InvariantCulture));
		}
		Logger.Log(LogLevel.Information, "MainWindow:chkCloudRecoveryPlus_Click", chkCloudRecoveryPlus.IsChecked.ToString().ToLower());
		LoadSettings();
		serviceModel.ResetCloudsLODs();
	}

	private void chkLOGPlus_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.LogPlus = chkLOGPlus.IsChecked.Value;
		serviceModel.ConfigurationFileCommon.SetSetting("LogPlus", serviceModel.LogPlus.ToString().ToLower());
		SetWindowTitle();
		Logger.Log(LogLevel.Information, "MainWindow:chkLOGPlus_Click :", "Log+ " + (serviceModel.LogPlus ? "Enabled" : "Disabled"));
		UpdateLiveValues();
		UpdateLayout();
		if (serviceModel.UseExpertOptions || serviceModel.LogPlus)
		{
			chkUseExpertOptions.Content = "Expert";
			chkLOGPlus.Visibility = Visibility.Visible;
		}
		else
		{
			chkUseExpertOptions.Content = "Expert Settings";
			chkLOGPlus.Visibility = Visibility.Collapsed;
		}
	}

	private void chkMinTLODExtra_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("MinTLODExtra" + serviceModel.profileSuffix) : "MinTLODExtra_VFR", chkMinTLODExtra.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:chkMinTLODExtra_Click", chkMinTLODExtra.IsChecked.ToString().ToLower());
		LoadSettings();
		serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
	}

	private void cbTLODBaseOption_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel != null && cbTLODBaseOption.SelectedIndex != (serviceModel.FreeTLOD[serviceModel.activeProfile] ? 2 : (serviceModel.MinTLODExtra[serviceModel.activeProfile] ? 1 : 0)))
		{
			if (cbTLODBaseOption.SelectedIndex == 0)
			{
				serviceModel.FreeTLOD[serviceModel.activeProfile] = false;
				serviceModel.MinTLODExtra[serviceModel.activeProfile] = false;
			}
			else if (cbTLODBaseOption.SelectedIndex == 1)
			{
				serviceModel.FreeTLOD[serviceModel.activeProfile] = false;
				serviceModel.MinTLODExtra[serviceModel.activeProfile] = true;
			}
			else if (cbTLODBaseOption.SelectedIndex == 2)
			{
				serviceModel.FreeTLOD[serviceModel.activeProfile] = true;
				serviceModel.MinTLODExtra[serviceModel.activeProfile] = true;
			}
			serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("FreeTLOD" + serviceModel.profileSuffix) : "FreeTLOD_VFR", serviceModel.FreeTLOD[serviceModel.activeProfile].ToString().ToLower(), noLoad: true);
			serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("MinTLODExtra" + serviceModel.profileSuffix) : "MinTLODExtra_VFR", serviceModel.MinTLODExtra[serviceModel.activeProfile].ToString().ToLower());
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
			string text;
			if (serviceModel.AutoTargetFPS)
			{
				text = "Fixed";
			}
			else
			{
				text = (serviceModel.FixedTargetFPS ? "Extra" : "Free");
			}
			Logger.Log(LogLevel.Information, "MainWindow:cbTLODBaseOption_SelectionChange", text.ToString());
			LoadSettings();
		}
	}

	private void chkMaxTLODExtra_Click(object sender, RoutedEventArgs e)
	{
		if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3 && serviceModel.TLODBaseMinus[serviceModel.activeProfile] && !chkMaxTLODExtra.IsChecked.Value)
		{
			chkMaxTLODExtra.IsChecked = !chkMaxTLODExtra.IsChecked;
			MessageBox.Show(this, "FPS Cap mode requires at least one TLOD Extra to be enabled. If you prefer not to use any TLOD Extra, switch to Auto TLOD mode instead.", "Advice", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("MaxTLODExtra" + serviceModel.profileSuffix) : "MaxTLODExtra_VFR", chkMaxTLODExtra.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:chkMaxTLODExtra_Click", chkMaxTLODExtra.IsChecked.ToString().ToLower());
		LoadSettings();
		serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
	}

	private void cbMinTLODExtraCapMult_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel != null && (double)serviceModel.MinTLODExtraCapMult[serviceModel.activeProfile] != 1.5 + 0.5 * (double)cbMinTLODExtraCapMult.SelectedIndex)
		{
			float num = 1.5f + 0.5f * (float)cbMinTLODExtraCapMult.SelectedIndex;
			serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("MinTLODExtraCapMult" + serviceModel.profileSuffix) : "MinTLODExtraCapMult_VFR", num.ToString("0.0", CultureInfo.InvariantCulture));
			Logger.Log(LogLevel.Information, "MainWindow:cbMinTLODExtraCapMult_SelectionChange", num.ToString("0.0", CultureInfo.InvariantCulture));
			LoadSettings();
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void cbMaxTLODExtraCapMult_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel != null && (double)serviceModel.MaxTLODExtraCapMult[serviceModel.activeProfile] != 1.5 + 0.5 * (double)cbMaxTLODExtraCapMult.SelectedIndex)
		{
			float num = 1.5f + 0.5f * (float)cbMaxTLODExtraCapMult.SelectedIndex;
			serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("MaxTLODExtraCapMult" + serviceModel.profileSuffix) : "MaxTLODExtraCapMult_VFR", num.ToString("0.0", CultureInfo.InvariantCulture));
			Logger.Log(LogLevel.Information, "MainWindow:cbMaxTLODExtraCapMult_SelectionChange", num.ToString("0.0", CultureInfo.InvariantCulture));
			LoadSettings();
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkTLODBaseMinus_Click(object sender, RoutedEventArgs e)
	{
		bool flag = chkTLODBaseMinus.IsChecked != true;
		if ((serviceModel.TLODAutoMethod[serviceModel.activeProfile] == 3 && !serviceModel.MaxTLODExtra[serviceModel.activeProfile]) & flag)
		{
			chkTLODBaseMinus.IsChecked = !chkTLODBaseMinus.IsChecked;
			MessageBox.Show(this, "FPS Cap mode requires at least one TLOD Extra to be enabled. If you prefer not to use any TLOD Extra, switch to Auto TLOD mode instead.", "Advice", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("TLODBaseMinus" + serviceModel.profileSuffix) : "TLODBaseMinus_VFR", flag.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:TLODBaseMinus_Click", flag.ToString().ToLower());
		LoadSettings();
		serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
	}

	private void chkStartTLODExtraAtMax_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("StartTLODExtraAtMax" + serviceModel.profileSuffix) : "StartTLODExtraAtMax_VFR", chkStartTLODExtraAtMax.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:chkStartTLODExtraAtMax_Click", chkStartTLODExtraAtMax.IsChecked.ToString().ToLower());
		LoadSettings();
		serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
	}

	private void chkTLODExtraMtns_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("TLODExtraMtns" + serviceModel.profileSuffix) : "TLODExtraMtns_VFR", chkTLODExtraMtns.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:chkTLODExtraMtns_Click", chkTLODExtraMtns.IsChecked.ToString().ToLower());
		LoadSettings();
		serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
	}

	private void chkDecCloudQ_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("DecCloudQ" + serviceModel.profileSuffix) : "DecCloudQ_VFR", chkDecCloudQ.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:chkDecCloudQ_Click", chkDecCloudQ.IsChecked.ToString().ToLower());
		LoadSettings();
		serviceModel.ResetCloudsLODs();
	}

	private void chkIncCloudQ_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("IncCloudQ" + serviceModel.profileSuffix) : "IncCloudQ_VFR", chkIncCloudQ.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:chkIncCloudQ_Click", chkIncCloudQ.IsChecked.ToString().ToLower());
		LoadSettings();
		serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
	}

	private void chkVRAMPlus_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting("VRAMPlusEnabled", chkVRAMPlus.IsChecked.ToString().ToLower());
		serviceModel.SetSetting("VRAMPlusSuite", chkVRAMPlus.IsChecked.Value ? "1" : "0");
		Logger.Log(LogLevel.Information, "MainWindow:chkVRAMPlus_Click", chkVRAMPlus.IsChecked.ToString().ToLower());
		LoadSettings();
		serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
	}

	private void chkVRAMPlus2_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting("VRAMPlusEnabled", chkVRAMPlus2.IsChecked.ToString().ToLower());
		serviceModel.SetSetting("VRAMPlusSuite", chkVRAMPlus2.IsChecked.Value ? "1" : "0");
		Logger.Log(LogLevel.Information, "MainWindow:chkVRAMPlus_Click", chkVRAMPlus2.IsChecked.ToString().ToLower());
		LoadSettings();
		serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
	}

	private void cbVRAMPlus_SelectionChange(object sender, RoutedEventArgs e)
	{
		if (serviceModel != null && serviceModel.VRAMPlusSuite != cbVRAMPlus.SelectedIndex)
		{
			serviceModel.SetSetting("VRAMPlusEnabled", (cbVRAMPlus.SelectedIndex >= 1).ToString().ToLower(), noLoad: true);
			serviceModel.SetSetting("VRAMPlusSuite", cbVRAMPlus.SelectedIndex.ToString().ToLower());
			Logger.Log(LogLevel.Information, "MainWindow:cbVRAMPlus_SelectionChange", cbVRAMPlus.SelectedIndex.ToString().ToLower());
			LoadSettings();
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkAutoReduceSettings_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("AutoReduceSettings" + serviceModel.profileSuffix) : "AutoReduceSettings_VFR", chkAutoReduceSettings.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:chkAutoReduceSettings_Click", chkAutoReduceSettings.IsChecked.ToString().ToLower());
		LoadSettings();
		serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
	}

	private void cbReductionSettingsMaxLevels_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel != null && serviceModel.ReductionSettingsMaxLevels[serviceModel.activeProfile] - 1 != cbReductionSettingsMaxLevels.SelectedIndex)
		{
			serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReductionSettingsMaxLevels" + serviceModel.profileSuffix) : "ReductionSettingsMaxLevels_VFR", (cbReductionSettingsMaxLevels.SelectedIndex + 1).ToString());
			serviceModel.ReductionSettingsMaxLevels[serviceModel.activeProfile] = cbReductionSettingsMaxLevels.SelectedIndex + 1;
			Logger.Log(LogLevel.Information, "MainWindow:cbReductionSettingsMaxLevels_SelectionChange", (cbReductionSettingsMaxLevels.SelectedIndex + 1).ToString().ToLower());
			LoadSettings();
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void cbReductionSettingsFloor_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel != null && serviceModel.ReductionSettingsFloor[serviceModel.activeProfile] != cbReductionSettingsFloor.SelectedIndex)
		{
			serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReductionSettingsFloor" + serviceModel.profileSuffix) : "ReductionSettingsFloor_VFR", cbReductionSettingsFloor.SelectedIndex.ToString());
			serviceModel.ReductionSettingsFloor[serviceModel.activeProfile] = cbReductionSettingsFloor.SelectedIndex;
			Logger.Log(LogLevel.Information, "MainWindow:cbReductionSettingsFloor_SelectionChange", cbReductionSettingsFloor.SelectedIndex.ToString().ToLower());
			LoadSettings();
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void cbReductionSettingsSuite_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel != null && serviceModel.ReductionSettingsSuite[serviceModel.activeProfile] != cbReductionSettingsSuite.SelectedIndex)
		{
			serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReductionSettingsSuite" + serviceModel.profileSuffix) : "ReductionSettingsSuite_VFR", cbReductionSettingsSuite.SelectedIndex.ToString());
			serviceModel.ReductionSettingsSuite[serviceModel.activeProfile] = cbReductionSettingsSuite.SelectedIndex;
			Logger.Log(LogLevel.Information, "MainWindow:cbReductionSettingsSuite_SelectionChange", cbReductionSettingsSuite.SelectedIndex.ToString().ToLower());
			LoadSettings();
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void cbReductionSettingsRecoveryAlt_SelectionChange(object sender, EventArgs e)
	{
		if (serviceModel != null && serviceModel.ReductionSettingsRecoveryAlt[serviceModel.activeProfile] != cbReductionSettingsRecoveryAlt.SelectedIndex)
		{
			serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReductionSettingsRecoveryAlt" + serviceModel.profileSuffix) : "ReductionSettingsRecoveryAlt_VFR", cbReductionSettingsRecoveryAlt.SelectedIndex.ToString());
			serviceModel.ReductionSettingsRecoveryAlt[serviceModel.activeProfile] = cbReductionSettingsRecoveryAlt.SelectedIndex;
			Logger.Log(LogLevel.Information, "MainWindow:cbReductionSettingsRecoveryAlt_SelectionChange", cbReductionSettingsRecoveryAlt.SelectedIndex.ToString().ToLower());
			LoadSettings();
		}
	}

	private void cbReductionSettingsSuite_MouseEnter(object sender, MouseEventArgs e)
	{
		ShowAutoReducePanel = true;
	}

	private void cbReductionSettingsSuite_MouseLeave(object sender, MouseEventArgs e)
	{
		ShowAutoReducePanel = false;
	}

	private void lblSimReduce_MouseEnter(object sender, MouseEventArgs e)
	{
		if (serviceModel.UseExpertOptions)
		{
			ShowReduceSimValues = true;
		}
	}

	private void imgSimFPS_Click(object sender, MouseButtonEventArgs e)
	{
		if (this.serviceModel != null && (!this.serviceModel.VrModeActive || ((!this.serviceModel.OfxrModeEnabled || !this.serviceModel.OfxrStatusJsonActive || !(this.serviceModel.OfxrOutputFps > 0f)) && (!this.serviceModel.VxrModeEnabled || !this.serviceModel.VxrStatusJsonActive || !(this.serviceModel.VxrOutputFps > 0f)))))
		{
			ServiceModel serviceModel = this.serviceModel;
			bool fpsSourceRTSS = (this.serviceModel.fpsSourceRTSSDefault = !this.serviceModel.fpsSourceRTSS);
			serviceModel.fpsSourceRTSS = fpsSourceRTSS;
			this.serviceModel.ConfigurationFileCommon.SetSetting("fpsSourceRTSS", this.serviceModel.fpsSourceRTSS.ToString().ToLower());
			Logger.Log(LogLevel.Information, "MainWindow:imgSimFPS_Click", "FPS source changed to " + (this.serviceModel.fpsSourceRTSS ? "RTSS" : "MSFS"));
			this.serviceModel.fpsAverageReset = true;
		}
	}

	private void chkPerfOptEnabled_Click(object sender, RoutedEventArgs e)
	{
		if (serviceModel == null)
		{
			return;
		}
		serviceModel.PerfOptEnabled = !serviceModel.PerfOptEnabled;
		serviceModel.ConfigurationFileCommon.SetSetting("PerfOptEnabled", serviceModel.PerfOptEnabled.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:PerfOptToggle", "Perf opt " + (serviceModel.PerfOptEnabled ? "enabled" : "disabled"));
		if (serviceModel.PerfOptEnabled && !serviceModel.ConfigurationFileCommon.SettingExists("PerfOptWarningShown"))
		{
			MessageBox.Show("Enabling MSFS Performance Optimisations applies changes to CPU core affinity, MSFS process priority, and the Windows power plan.\n\nThese adjustments can improve performance on most systems, but may also reduce performance, introduce stuttering, cause audio issues, or lead to unexpected MSFS behaviour depending on your hardware and aircraft.\n\nMonitor your system after enabling this option.\n\nThis message will not be shown again.", "MSFS Performance Optimisations – Important Notice", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			serviceModel.ConfigurationFileCommon.SetSetting("PerfOptWarningShown", "true");
		}
		if (!serviceModel.PerfOptEnabled && serviceModel.SmtCores < serviceModel.AffinityPhysicalCoreThreshold)
		{
			serviceModel.PerfOptAffinityEnabled = false;
			serviceModel.ConfigurationFileCommon.SetSetting("PerfOptAffinityEnabled", "false");
			Logger.Log(LogLevel.Information, "MainWindow:PerfOptToggle", "PerfOptAffinity forced disabled due to low SMT core count on disabling Perf Opt");
			chkPerfOptAffinity.IsChecked = false;
		}
		if (serviceModel.PerfOptEnabled && serviceModel.PerfOptAffinityEnabled && serviceModel.AMDDualCCDValue)
		{
			ApplyAmdFirstCcdPreference();
		}
		if (serviceModel.MemoryCompatTestComplete && serviceModel.AppEnabled)
		{
			if (serviceModel.PerfOptEnabled)
			{
				serviceModel.ApplyPerfOpt();
			}
			else
			{
				serviceModel.RemovePerfOpt();
			}
		}
		chkPerfOptEnabled.IsChecked = serviceModel.PerfOptEnabled;
		if (serviceModel.UseExpertOptions && serviceModel.PerfOptEnabled)
		{
			stkpnlPerfOptDetails.Visibility = Visibility.Visible;
		}
		else
		{
			stkpnlPerfOptDetails.Visibility = Visibility.Collapsed;
		}
		LoadSettings();
	}

	private void PerfOpt_MouseEnter(object sender, MouseEventArgs e)
	{
		if (serviceModel.UseExpertOptions && serviceModel.PerfOptEnabled)
		{
			stkpnlPerfOptDetails.Visibility = Visibility.Visible;
		}
	}

	private void PerfOpt_MouseLeave(object sender, MouseEventArgs e)
	{
		stkpnlPerfOptDetails.Visibility = Visibility.Collapsed;
	}

	private void chkPerfOptAffinity_Click(object sender, RoutedEventArgs e)
	{
		if (serviceModel != null)
		{
			serviceModel.PerfOptAffinityEnabled = !serviceModel.PerfOptAffinityEnabled;
			if (serviceModel.PerfOptAffinityEnabled && serviceModel.SmtCores < serviceModel.AffinityPhysicalCoreThreshold)
			{
				MessageBox.Show($"Your CPU has only {serviceModel.SmtCores} SMT cores, which is below the configured threshold of {serviceModel.AffinityPhysicalCoreThreshold}.\n\n" + "Enabling Physical Cores mode may cause reduced performance or stuttering.", "Low Core Count Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			serviceModel.ConfigurationFileCommon.SetSetting("PerfOptAffinityEnabled", serviceModel.PerfOptAffinityEnabled.ToString().ToLower());
			Logger.Log(LogLevel.Information, "MainWindow:chkPerfOptAffinity_Click", "CPU Core Affinity " + (serviceModel.PerfOptAffinityEnabled ? "enabled" : "disabled"));
			if (serviceModel.PerfOptAffinityEnabled && serviceModel.AMDDualCCDValue)
			{
				ApplyAmdFirstCcdPreference();
			}
			if (serviceModel.MemoryCompatTestComplete && serviceModel.AppEnabled && serviceModel.PerfOptAffinityEnabled)
			{
				serviceModel.RestorePerfOptAffinity(log: false);
				serviceModel.ApplyPerfOptAffinity();
			}
			else if (!serviceModel.PerfOptAffinityEnabled)
			{
				serviceModel.RestorePerfOptAffinity();
			}
			chkPerfOptAffinity.IsChecked = serviceModel.PerfOptAffinityEnabled;
		}
	}

	private void ApplyAmdFirstCcdPreference()
	{
		if (serviceModel.UseExpertOptions)
		{
			MessageBoxResult messageBoxResult = MessageBox.Show("Your AMD CPU has multiple CCDs.\n\nFirst‑CCD mode can improve consistency by restricting MSFS to the first CCD.\n\nWould you like to enable First‑CCD mode?", "AMD Dual‑CCD Optimization", MessageBoxButton.YesNo, MessageBoxImage.Question);
			serviceModel.AMDUseFirstCCDOnly = messageBoxResult == MessageBoxResult.Yes;
			serviceModel.ConfigurationFileCommon.SetSetting("AMDUseFirstCCDOnly", serviceModel.AMDUseFirstCCDOnly.ToString().ToLower());
			SetAffinityTooltip();
			Logger.Log(LogLevel.Information, "MainWindow:ApplyAmdFirstCcdPreference", "AMD First‑CCD mode " + (serviceModel.AMDUseFirstCCDOnly ? "enabled" : "disabled") + " by user choice");
		}
		else
		{
			serviceModel.AMDUseFirstCCDOnly = true;
			serviceModel.ConfigurationFileCommon.SetSetting("AMDUseFirstCCDOnly", "true");
			SetAffinityTooltip();
			Logger.Log(LogLevel.Information, "MainWindow:ApplyAmdFirstCcdPreference", "AMD First‑CCD mode enabled automatically (non‑expert mode)");
		}
	}

	public void SetAffinityTooltip()
	{
		string text = $"Use physical CPU cores only\n• {serviceModel.SmtCores} SMT cores detected";
		if (serviceModel.AMDUseFirstCCDOnlyValue)
		{
			text += "\n• First CCD only (AMD CCD+ enabled)";
		}
		else if (serviceModel.AMDDualCCDValue)
		{
			text += "\n• Both CCDs (AMD CCD+ disabled)";
		}
		if (serviceModel.LastAppliedAffinityText != null)
		{
			text = text + "\n• " + serviceModel.LastAppliedAffinityText;
			if (serviceModel.SingleLpCores.Count > 0)
			{
				text = ((!serviceModel.cpuNameValue.Contains("Intel", StringComparison.OrdinalIgnoreCase) || serviceModel.DualLpCores.Count <= 0) ? (text + $"\n• {serviceModel.SingleLpCores.Count} single LP cores excluded") : (text + $"\n• {serviceModel.SingleLpCores.Count} E-cores excluded"));
			}
		}
		chkPerfOptAffinity.ToolTip = text;
	}

	public void SetPriorityTooltip()
	{
		string text = "Sets a higher MSFS process priority";
		if (serviceModel.LastAppliedPriorityText != null)
		{
			text = text + "\n• " + serviceModel.LastAppliedPriorityText;
		}
		chkPerfOptPriority.ToolTip = text;
	}

	public void SetPowerPlanTooltip()
	{
		string text = "Switches to the best available Windows power plan for MSFS";
		if (!serviceModel.PerfOptPowerPlanEnabled)
		{
			text += "\n• Only overrides when starting from a default Windows power plan";
			text += "\n• Chooses Ultimate Performance, High Performance, or Balanced depending on availability";
		}
		else if (serviceModel.LastAppliedPowerPlanText != null)
		{
			text = text + "\n" + serviceModel.LastAppliedPowerPlanText;
		}
		chkPerfOptPowerPlan.ToolTip = text;
	}

	private void chkPerfOptPriority_Click(object sender, RoutedEventArgs e)
	{
		if (serviceModel != null)
		{
			serviceModel.PerfOptPriorityEnabled = !serviceModel.PerfOptPriorityEnabled;
			serviceModel.ConfigurationFileCommon.SetSetting("PerfOptPriorityEnabled", serviceModel.PerfOptPriorityEnabled.ToString().ToLower());
			Logger.Log(LogLevel.Information, "MainWindow:chkPerfOptPriority_Click", "MSFS Process Priority " + (serviceModel.PerfOptPriorityEnabled ? "enabled" : "disabled"));
			if (serviceModel.MemoryCompatTestComplete && serviceModel.AppEnabled && serviceModel.PerfOptPriorityEnabled)
			{
				serviceModel.RestorePerfOptPriority(log: false);
				serviceModel.ApplyPerfOptPriority();
			}
			else if (!serviceModel.PerfOptPriorityEnabled)
			{
				serviceModel.RestorePerfOptPriority();
			}
			chkPerfOptPriority.IsChecked = serviceModel.PerfOptPriorityEnabled;
			SetPriorityTooltip();
		}
	}

	private void cbPerfOptPriority_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (serviceModel != null && ((serviceModel.MSFSProcessPriority == "High") ? 1 : 0) != cbPerfOptPriority.SelectedIndex)
		{
			serviceModel.ConfigurationFileCommon.SetSetting("MSFSProcessPriority", serviceModel.MSFSProcessPriority = ((cbPerfOptPriority.SelectedIndex == 1) ? "High" : "AboveNormal"));
			Logger.Log(LogLevel.Information, "MainWindow:cbPerfOptPriority_SelectionChanged", "MSFS Process Priority set to " + serviceModel.MSFSProcessPriority);
			if (serviceModel.MemoryCompatTestComplete && serviceModel.AppEnabled && serviceModel.PerfOptPriorityEnabled)
			{
				serviceModel.RestorePerfOptPriority(log: false);
				serviceModel.ApplyPerfOptPriority();
			}
			else if (!serviceModel.PerfOptPriorityEnabled)
			{
				serviceModel.RestorePerfOptPriority();
			}
		}
	}

	private void chkPerfOptPowerPlan_Click(object sender, RoutedEventArgs e)
	{
		if (serviceModel != null)
		{
			serviceModel.PerfOptPowerPlanEnabled = !serviceModel.PerfOptPowerPlanEnabled;
			serviceModel.ConfigurationFileCommon.SetSetting("PerfOptPowerPlanEnabled", serviceModel.PerfOptPowerPlanEnabled.ToString().ToLower());
			Logger.Log(LogLevel.Information, "MainWindow:chkPerfOptPowerPlan_Click", "Best Power Plan " + (serviceModel.PerfOptPowerPlanEnabled ? "enabled" : "disabled"));
			if (serviceModel.MemoryCompatTestComplete && serviceModel.AppEnabled && serviceModel.PerfOptPowerPlanEnabled)
			{
				serviceModel.RestorePerfOptPowerPlan(log: false);
				serviceModel.ApplyPerfOptPowerPlan();
			}
			else if (!serviceModel.PerfOptPowerPlanEnabled)
			{
				serviceModel.RestorePerfOptPowerPlan();
			}
			chkPerfOptPowerPlan.IsChecked = serviceModel.PerfOptPowerPlanEnabled;
			SetPowerPlanTooltip();
		}
	}

	private void lblSimReduce2_MouseEnter(object sender, MouseEventArgs e)
	{
		ShowReduceSimValues = true;
	}

	private void lblSimReduce_MouseLeave(object sender, MouseEventArgs e)
	{
		ShowReduceSimValues = false;
	}

	private void cbNightMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (serviceModel != null && serviceModel.NightMode[serviceModel.activeProfile] != cbNightMode.SelectedIndex && CheckNightMode())
		{
			string text = ((serviceModel.activeProfile == 1) ? serviceModel.profileSuffix : "_VFR");
			int num = cbNightMode.SelectedIndex;
			bool flag = num == 1 || num == 3;
			serviceModel.SetSetting("NightMode" + text, num.ToString());
			serviceModel.SetSetting("MaxTLODHalvedNight" + text, flag.ToString().ToLower());
			serviceModel.NightMode[serviceModel.activeProfile] = num;
			serviceModel.MaxTLODHalvedNight[serviceModel.activeProfile] = flag;
			Logger.Log(LogLevel.Information, "MainWindow:cbNightMode_SelectionChanged", $"NightMode:{num} MaxTLODHalvedNight:{flag.ToString().ToLower()}");
			LoadSettings();
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private bool CheckNightMode()
	{
		if (serviceModel.UseExpertOptions && (cbNightMode.SelectedIndex == 1 || cbNightMode.SelectedIndex == 3) && serviceModel.MaxTLOD[serviceModel.activeProfile] < 2f * serviceModel.MinTLOD[serviceModel.activeProfile])
		{
			MessageBox.Show(this, "TLOD Halved at Night cannot be selected as your TLOD Max/Top is not at least 2 times your TLOD Min/Base. Change either one of these to meet this requirement before selecting this option.", "Advice", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			int num = ((cbNightMode.SelectedIndex == 3) ? 2 : 0);
			cbNightMode.SelectedIndex = num;
			string text = ((serviceModel.activeProfile == 1) ? serviceModel.profileSuffix : "_VFR");
			serviceModel.SetSetting("NightMode" + text, num.ToString());
			serviceModel.SetSetting("MaxTLODHalvedNight" + text, "false");
			Logger.Log(LogLevel.Information, "MainWindow:CheckNightMode", "NightMode reverted because TLOD Max/Top is not at least 2 times TLOD Min/Base.");
			LoadSettings();
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
			return false;
		}
		return true;
	}

	private void TextBox_LostFocus(object sender, RoutedEventArgs e)
	{
		TextBox_SetSetting(sender as TextBox);
	}

	private void TextBox_KeyUp(object sender, KeyEventArgs e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Invalid comparison between Unknown and I4
		if ((int)e.Key == 6 && (int)e.Key == 6)
		{
			TextBox_SetSetting(sender as TextBox);
		}
	}

	private void TextBox_SetSetting(TextBox sender)
	{
		if (sender == null || string.IsNullOrWhiteSpace(sender.Text))
		{
			return;
		}
		bool flag = false;
		bool flag2 = true;
		bool flag3 = false;
		bool resetTLOD = false;
		string text;
		switch (sender.Name)
		{
		case "txtTargetFPS":
			text = "targetFps";
			break;
		case "txtFPSTolerance":
			text = "FpsTolerance";
			flag = true;
			break;
		case "txtSpikeThresholdMultiplier":
			text = "SpikeThresholdMultiplier";
			break;
		case "txtCloudRecoveryTLOD":
			text = "CloudRecoveryTLOD";
			break;
		case "txtCloudDecreaseGPUPct":
			text = "CloudDecreaseGPUPct";
			break;
		case "txtCloudRecoverGPUPct":
			text = "CloudRecoverGPUPct";
			break;
		case "txtMinTLod":
			text = "minTLod";
			resetTLOD = true;
			break;
		case "txtMaxTLod":
			text = "maxTLod";
			resetTLOD = true;
			break;
		case "txtTLODExtraMtnsTriggerAlt":
			text = "TLODExtraMtnsTriggerAlt";
			break;
		case "txtTLODExtraMtnsAmount":
			text = "TLODExtraMtnsAmount";
			break;
		case "txtOLODAtBase":
			text = "OLODAtBase";
			break;
		case "txtOLODAtTop":
			text = "OLODAtTop";
			break;
		case "txtAltOLODBase":
			text = "AltOLODBase";
			break;
		case "txtAltOLODTop":
			text = "AltOLODTop";
			break;
		case "txtAltTLODBase":
			text = "AltTLODBase";
			resetTLOD = true;
			break;
		case "txtAltTLODTop":
			text = "AltTLODTop";
			resetTLOD = true;
			break;
		default:
			text = "";
			break;
		}
		if (text == "")
		{
			return;
		}
		if (flag && int.TryParse(sender.Text, CultureInfo.InvariantCulture, out var result) && ((result != 0) | flag3))
		{
			if (flag2)
			{
				result = Math.Abs(result);
			}
			if (text == "FpsTolerance")
			{
				if (result > 25)
				{
					result = serviceModel.FPSTolerance[serviceModel.activeProfile];
				}
				if (serviceModel.activeProfile != 1)
				{
					text += "_VFR";
				}
			}
			if (serviceModel.activeProfile == 1)
			{
				text += serviceModel.profileSuffix;
			}
			serviceModel.SetSetting(text, Convert.ToString(result, CultureInfo.InvariantCulture));
			Logger.Log(LogLevel.Information, "MainWindow:TextBox_SetSetting", text + " changed to " + result);
		}
		if (!flag && float.TryParse(sender.Text, new RealInvariantFormat(sender.Text), out var result2))
		{
			if (flag2)
			{
				result2 = Math.Abs(result2);
			}
			switch (text)
			{
			case "targetFps":
				if (result2 < 10f || result2 > 500f)
				{
					result2 = serviceModel.TargetFPS;
				}
				result2 = (float)Math.Round(result2 * 2f) / 2f;
				if (TargetFPSType == "VR")
				{
					text = (serviceModel.FlightTypeIFR ? "targetFpsVR" : "targetFpsVRVFR");
				}
				else if (TargetFPSType == "OFXR")
				{
					text = (serviceModel.FlightTypeIFR ? "targetFpsOFXR" : "targetFpsOFXRVFR");
				}
				else if (TargetFPSType == "VXR")
				{
					text = (serviceModel.FlightTypeIFR ? "targetFpsVXR" : "targetFpsVXRVFR");
				}
				else if (TargetFPSType == "FG")
				{
					text = (serviceModel.FlightTypeIFR ? "targetFpsFG" : "targetFpsFGVFR");
				}
				else if (TargetFPSType == "LSFG")
				{
					text = (serviceModel.FlightTypeIFR ? "targetFpsLS" : "targetFpsLSVFR");
				}
				else if (TargetFPSType == "FSR")
				{
					text = (serviceModel.FlightTypeIFR ? "targetFpsFSRX" : "targetFpsFSRXVFR");
				}
				else if (TargetFPSType == "MSM")
				{
					text = (serviceModel.FlightTypeIFR ? "targetFpsMSM" : "targetFpsMSMVFR");
				}
				else if (TargetFPSType == "Man FG")
				{
					text = (serviceModel.FlightTypeIFR ? "targetFpsManFG" : "targetFpsManFGVFR");
				}
				else
				{
					text = (serviceModel.FlightTypeIFR ? "targetFpsPC" : "targetFpsPCVFR");
				}
				txtTargetFPS.Text = Convert.ToString(result2, CultureInfo.CurrentUICulture);
				break;
			case "SpikeThresholdMultiplier":
				if ((double)result2 < 1.1)
				{
					result2 = 1.1f;
				}
				else
				{
					result2 = ((!(result2 > 10f)) ? ((float)Math.Round(result2 * 10f) / 10f) : 10f);
				}
				txtSpikeThresholdMultiplier.Text = result2.ToString("0.0");
				break;
			case "minTLod":
				if (result2 < serviceModel.SimMinLOD || result2 > serviceModel.MaxTLOD[serviceModel.activeProfile] - 10f)
				{
					result2 = serviceModel.MinTLOD[serviceModel.activeProfile];
				}
				if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2 && (serviceModel.CloudRecoveryPlus[serviceModel.activeProfile] ? (serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile] + result2 > serviceModel.MaxTLOD[serviceModel.activeProfile] - 10f) : (serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile] < result2 + 10f)))
				{
					serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("CloudRecoveryTLOD" + serviceModel.profileSuffix) : "CloudRecoveryTLOD_VFR", Convert.ToString(Math.Round((serviceModel.CloudRecoveryPlus[serviceModel.activeProfile] ? 0f : serviceModel.MinTLOD[serviceModel.activeProfile]) + 2f * (serviceModel.MaxTLOD[serviceModel.activeProfile] - result2) / 5f), CultureInfo.InvariantCulture));
				}
				break;
			case "maxTLod":
				if (result2 < serviceModel.MinTLOD[serviceModel.activeProfile] + 10f || result2 > serviceModel.SimMaxLOD)
				{
					result2 = serviceModel.MaxTLOD[serviceModel.activeProfile];
				}
				if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2 && (serviceModel.CloudRecoveryPlus[serviceModel.activeProfile] ? serviceModel.MinTLOD[serviceModel.activeProfile] : 0f) + serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile] > result2 - 10f)
				{
					serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("CloudRecoveryTLOD" + serviceModel.profileSuffix) : "CloudRecoveryTLOD_VFR", Convert.ToString(Math.Round((serviceModel.CloudRecoveryPlus[serviceModel.activeProfile] ? 0f : serviceModel.MinTLOD[serviceModel.activeProfile]) + 2f * (result2 - serviceModel.MinTLOD[serviceModel.activeProfile]) / 5f), CultureInfo.InvariantCulture));
				}
				break;
			case "TLODExtraMtnsAmount":
				if (result2 < serviceModel.SimMinLOD || result2 > serviceModel.SimMaxLOD)
				{
					result2 = serviceModel.TLODExtraMtnsAmount[serviceModel.activeProfile];
				}
				break;
			case "TLODExtraMtnsTriggerAlt":
				if (result2 < 100f || result2 > 100000f)
				{
					result2 = serviceModel.TLODExtraMtnsTriggerAlt[serviceModel.activeProfile];
				}
				break;
			case "CloudRecoveryTLOD":
				if (serviceModel.CloudRecoveryPlus[serviceModel.activeProfile] && (result2 < 5f || result2 > serviceModel.MaxTLOD[serviceModel.activeProfile] - serviceModel.MinTLOD[serviceModel.activeProfile] - 5f))
				{
					result2 = serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile];
				}
				else if (!serviceModel.CloudRecoveryPlus[serviceModel.activeProfile] && (result2 < serviceModel.MinTLOD[serviceModel.activeProfile] + 5f || result2 > serviceModel.MaxTLOD[serviceModel.activeProfile] - 5f))
				{
					result2 = serviceModel.CloudRecoveryTLOD[serviceModel.activeProfile];
				}
				break;
			case "CloudDecreaseGPUPct":
				if (result2 < 50f || result2 > 100f || result2 < serviceModel.CloudRecoverGPUPct + 10f)
				{
					result2 = serviceModel.CloudDecreaseGPUPct;
				}
				break;
			case "CloudRecoverGPUPct":
				if (result2 < 5f || result2 > 90f || result2 > serviceModel.CloudDecreaseGPUPct - 10f)
				{
					result2 = serviceModel.CloudRecoverGPUPct;
				}
				break;
			case "OLODAtBase":
				if (result2 < serviceModel.SimMinLOD || result2 > serviceModel.SimMaxLOD)
				{
					result2 = serviceModel.OLODAtBase[serviceModel.activeProfile];
				}
				break;
			case "OLODAtTop":
				if (result2 < serviceModel.SimMinLOD || result2 > serviceModel.SimMaxLOD)
				{
					result2 = serviceModel.OLODAtTop[serviceModel.activeProfile];
				}
				break;
			case "AltOLODBase":
				if (result2 < 10f || result2 >= 100000f || result2 > serviceModel.AltOLODTop[serviceModel.activeProfile] - 1f)
				{
					result2 = serviceModel.AltOLODBase[serviceModel.activeProfile];
				}
				break;
			case "AltOLODTop":
				if (result2 < 2000f || result2 > 100000f || result2 < serviceModel.AltOLODBase[serviceModel.activeProfile] + 1f)
				{
					result2 = serviceModel.AltOLODTop[serviceModel.activeProfile];
				}
				break;
			case "AltTLODBase":
				if (result2 < 10f || result2 >= 100000f || result2 > serviceModel.AltTLODTop[serviceModel.activeProfile] - 1f)
				{
					result2 = serviceModel.AltTLODBase[serviceModel.activeProfile];
				}
				break;
			case "AltTLODTop":
				if (serviceModel.TLODAutoMethod[serviceModel.activeProfile] < 2)
				{
					text += "_SensTol";
				}
				if (result2 < 1000f || result2 > 100000f || result2 < serviceModel.AltTLODBase[serviceModel.activeProfile] + 1f)
				{
					result2 = serviceModel.AltTLODTop[serviceModel.activeProfile];
				}
				break;
			}
			Logger.Log(LogLevel.Information, "MainWindow:TextBox_SetSetting", text + " changed to " + result2.ToString("F0"));
			if (text == "SpikeThresholdMultiplier")
			{
				serviceModel.ConfigurationFileCommon.SetSetting(text, Convert.ToString(result2, CultureInfo.InvariantCulture));
				serviceModel.LoadConfiguration();
				LoadSettings();
			}
			else
			{
				if (serviceModel.activeProfile == 1)
				{
					text += serviceModel.profileSuffix;
				}
				else if (serviceModel.activeProfile != 1 && text != "CloudDecreaseGPUPct" && text != "CloudRecoverGPUPct" && !text.StartsWith("targetFps"))
				{
					text += "_VFR";
				}
				serviceModel.SetSetting(text, Convert.ToString(result2, CultureInfo.InvariantCulture));
			}
		}
		if (text != "SpikeThresholdMultiplier" && (!text.StartsWith("targetFps") || !(TargetFPSType != serviceModel.ActiveGraphicsMode)))
		{
			LoadSettings();
			serviceModel.ResetCloudsLODs(resetTLOD);
			CheckNightMode();
		}
	}

	private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
	{
		try
		{
			Process process = new Process();
			process.StartInfo.UseShellExecute = true;
			process.StartInfo.FileName = e.Uri.ToString();
			process.Start();
			e.Handled = true;
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MainWindow:Hyperlink_RequestNavigate", $"Exception {ex}: {ex.Message}");
		}
	}

	private void chkReduceCloudQ_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReduceCloudQ" + serviceModel.profileSuffix) : "ReduceCloudQ_VFR", chkReduceCloudQ.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:ReduceCloudQ_Click", chkReduceCloudQ.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkReduceTrees_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReduceTrees" + serviceModel.profileSuffix) : "ReduceTrees_VFR", chkReduceTrees.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:ReduceTrees_Click", chkReduceTrees.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkReduceRTShadows_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReduceRTShadows" + serviceModel.profileSuffix) : "ReduceRTShadows_VFR", chkReduceRTShadows.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:ReduceRTShadows_Click", chkReduceRTShadows.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkReduceTShadows_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReduceTShadows" + serviceModel.profileSuffix) : "ReduceTShadows_VFR", chkReduceTShadows.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:ReduceTShadows_Click", chkReduceTShadows.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkReduceBldings_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReduceBldings" + serviceModel.profileSuffix) : "ReduceBldings_VFR", chkReduceBldings.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:ReduceBldings_Click", chkReduceBldings.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkReduceDispMap_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReduceDispMap" + serviceModel.profileSuffix) : "ReduceDispMap_VFR", chkReduceDispMap.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:ReduceDispMap_Click", chkReduceDispMap.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkReduceOSTPCache_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReduceOSTPCache" + serviceModel.profileSuffix) : "ReduceOSTPCache_VFR", chkReduceOSTPCache.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:ReduceOSTPCache_Click", chkReduceOSTPCache.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkReducePlants_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReducePlants" + serviceModel.profileSuffix) : "ReducePlants_VFR", chkReducePlants.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:ReducePlants_Click", chkReducePlants.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkReduceGrass_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReduceGrass" + serviceModel.profileSuffix) : "ReduceGrass_VFR", chkReduceGrass.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:ReduceGrass_Click", chkReduceGrass.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkReducePkdAcQ_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReducePkdAcQ" + serviceModel.profileSuffix) : "ReducePkdAcQ_VFR", chkReducePkdAcQ.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:ReducePkdAcQ_Click", chkReducePkdAcQ.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkReduceAcTrafQ_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.SetSetting((serviceModel.activeProfile == 1) ? ("ReduceAcTrafQ" + serviceModel.profileSuffix) : "ReduceAcTrafQ_VFR", chkReduceAcTrafQ.IsChecked.ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:ReduceAcTrafQ_Click", chkReduceAcTrafQ.IsChecked.ToString().ToLower());
		LoadSettings();
		if (serviceModel.MemoryAccess != null)
		{
			serviceModel.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
		}
	}

	private void chkCloudRecoveryPlus_Checked(object sender, RoutedEventArgs e)
	{
	}

	private void AutoFPS_DoubleClick(object sender, MouseEventArgs e)
	{
		object originalSource = e.OriginalSource;
		DependencyObject val = (DependencyObject)((originalSource is DependencyObject) ? originalSource : null);
		while (val != null && !(val is TextBox) && !(val is CheckBox) && !(val is ComboBox) && !(val is Button) && !(val is RadioButton))
		{
			val = ((!(val is Visual) && !(val is Visual3D)) ? LogicalTreeHelper.GetParent(val) : VisualTreeHelper.GetParent(val));
		}
		if (val is TextBox || val is CheckBox || val is ComboBox || val is Button || val is RadioButton)
		{
			e.Handled = true;
			return;
		}
		if (serviceModel.windowPanelState == 0 && !serviceModel.UseExpertOptions)
		{
			serviceModel.windowPanelState = 1;
		}
		else if (serviceModel.windowPanelState < 2)
		{
			serviceModel.windowPanelState++;
		}
		else
		{
			serviceModel.windowPanelState = ((!serviceModel.UseExpertOptions || serviceModel.MonitorMode) ? 1 : 0);
		}
		serviceModel.SetSetting("windowPanelState", serviceModel.windowPanelState.ToString().ToLower(), noLoad: true);
		LoadSettings(noLogging: true);
	}

	private void mnuExit_Click(object sender, RoutedEventArgs e)
	{
		serviceModel.ServiceExited = true;
		Close();
	}

	private void mnuMinimise_Click(object sender, RoutedEventArgs e)
	{
		WindowState = WindowState.Minimized;
	}

	private void mnuCloseToTray_Click(object sender, RoutedEventArgs e)
	{
		Hide();
	}

	private void ToggleTitleBar()
	{
		// Toggles the in-game widget title bar setting (the app window keeps its title bar).
		serviceModel.ConfigurationFileCommon.SetSetting("hideTitleBar", (serviceModel.hideTitleBar = !serviceModel.hideTitleBar).ToString().ToLower());
		Logger.Log(LogLevel.Information, "MainWindow:mnuToggleTitleBar_Click", "Title Bar " + (serviceModel.hideTitleBar ? "hidden" : "shown"));
		LoadSettings(noLogging: true);
	}

	private void mnuToggleTitleBar_Click(object sender, RoutedEventArgs e)
	{
		ToggleTitleBar();
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		(PresentationSource.FromVisual(this) as HwndSource)?.AddHook(WndProc);
	}

	private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		// Fork: caption double-click no longer hides the title bar (Fluent title bar handles maximise).
		if (msg == 274 && ((int)wParam & 0xFFF0) == 61536)
		{
			_isTitleBarCloseClick = lParam != IntPtr.Zero;
		}
		return IntPtr.Zero;
	}

	private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		DependencyObject val = (DependencyObject)((originalSource is DependencyObject) ? originalSource : null);
		while (val != null && !(val is TextBox) && !(val is CheckBox) && !(val is ComboBox) && !(val is ComboBoxItem) && !(val is Button) && !(val is RadioButton) && !(val is Hyperlink) && (object)val != imgSimFPSRTSS && (object)val != imgSimFPSMSFS)
		{
			DependencyObject val2 = null;
			if (val is Visual || val is Visual3D)
			{
				val2 = VisualTreeHelper.GetParent(val);
			}
			if (val2 == null)
			{
				val2 = LogicalTreeHelper.GetParent(val);
			}
			if (val2 == null && val is FrameworkContentElement frameworkContentElement)
			{
				val2 = frameworkContentElement.Parent;
			}
			val = val2;
		}
		if (val == null && e.LeftButton == MouseButtonState.Pressed && e.ClickCount == 1)
		{
			DragMove();
		}
	}

	private void Window_StateChanged(object sender, EventArgs e)
	{
		bool flag = WindowState == WindowState.Minimized;
		serviceModel.ConfigurationFileCommon.SetSetting("windowIsVisible", flag ? "false" : "true");
		serviceModel.windowIsVisible = !flag;
		if (!IsLoaded || flag)
		{
			return;
		}
		((DispatcherObject)this).Dispatcher.Invoke((Action)(() =>
		{
			Visibility = Visibility.Hidden;
			if (serviceModel.RememberWindowPos)
			{
				Top = serviceModel.windowTop;
				Left = serviceModel.windowLeft;
			}
			InvalidateMeasure();
			InvalidateVisual();
			UpdateLayout();
			Visibility = Visibility.Visible;
		}), (DispatcherPriority)7);
	}

	protected void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		bool flag = (serviceModel.isMSFS2024 ? serviceModel.WidgetInstalled2024 : serviceModel.WidgetInstalled2020);
		if (!IsVisible)
		{
			if (serviceModel.UseSysTrayIcon)
			{
				notifyModel.CanExecuteHideWindow = false;
				notifyModel.CanExecuteShowWindow = true;
			}
			if (!flag)
			{
				timer.Stop();
			}
			else
			{
				timer.Start();
			}
		}
		else
		{
			if (serviceModel.RememberWindowPos)
			{
				Top = serviceModel.windowTop;
				Left = serviceModel.windowLeft;
			}
			timer.Start();
		}
	}

	public void Force_WindowClosing()
	{
		_isExplicitExit = true;
		Window_Closing(this, new CancelEventArgs());
	}

	private void Window_Closing(object sender, CancelEventArgs e)
	{
		// Fork: the title bar X only hides the window (to the tray, or minimised when the tray icon is off);
		// exiting is done with Exit in the window or tray menu.
		if (_isTitleBarCloseClick && !serviceModel.ServiceExited && !_isExplicitExit)
		{
			_isTitleBarCloseClick = false;
			e.Cancel = true;
			if (serviceModel.UseSysTrayIcon)
			{
				Hide();
			}
			else
			{
				WindowState = WindowState.Minimized;
			}
			return;
		}
		if (serviceModel.ServiceExited || _isExplicitExit || (!serviceModel.UseSysTrayIcon && WindowState == WindowState.Minimized))
		{
			if (ShouldCancelExitForPendingUpdate())
			{
				e.Cancel = true;
				_isExplicitExit = false;
				serviceModel.ServiceExited = false;
				serviceModel.ExitAppAfterFlightSession = false;
			}
			else
			{
				_isExplicitExit = false;
				serviceModel.ServiceExited = true;
				HandleDeferredUpdateOnExit();
				_webServer?.Stop();
				Application.Current?.Shutdown();
			}
			return;
		}
		if (serviceModel.UseSysTrayIcon)
		{
			e.Cancel = true;
			Hide();
			return;
		}
		if (ShouldCancelExitForPendingUpdate())
		{
			e.Cancel = true;
			return;
		}
		if (_isTitleBarCloseClick)
		{
			_isTitleBarCloseClick = false;
			if (MessageBox.Show(this, "Are you sure you want to close " + appName + "?", "Confirm Close", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
			{
				e.Cancel = true;
				return;
			}
		}
		HandleDeferredUpdateOnExit();
		_webServer?.Stop();
		Application.Current?.Shutdown();
	}

	private bool ShouldCancelExitForPendingUpdate()
	{
		if (!serviceModel.deferredUpdate || string.IsNullOrEmpty(serviceModel.deferredUpdateVersion) || !IPCManager.IsSimRunning())
		{
			return false;
		}
		if (_isPromptOpen)
		{
			return true;
		}
		_isPromptOpen = true;
		try
		{
			string messageBoxText = $"A scheduled update to {appName} ({serviceModel.deferredUpdateVersion}) is waiting for MSFS to close.\n\n• Click 'No' to keep {appName} running so the update can install automatically when MSFS exits.\n• Click 'Yes' to close {appName} now and postpone the update until next launch.\n\nDo you want to close {appName} anyway?";
			string caption = "Pending Update: MSFS Still Running";
			MessageBoxResult messageBoxResult;
			if (IsVisible)
			{
				try
				{
					messageBoxResult = MessageBox.Show(this, messageBoxText, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
				}
				catch (InvalidOperationException)
				{
					messageBoxResult = MessageBox.Show(messageBoxText, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
				}
			}
			else
			{
				messageBoxResult = MessageBox.Show(messageBoxText, caption, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
			}
			if (messageBoxResult == MessageBoxResult.No)
			{
				serviceModel.ServiceExited = false;
				serviceModel.ExitAppAfterFlightSession = false;
				_isTitleBarCloseClick = false;
				return true;
			}
			Logger.Log(LogLevel.Information, "MainWindow:Window_Closing", "User closed app while MSFS was running. Deferred update to v" + serviceModel.deferredUpdateVersion + " postponed to next launch.");
			serviceModel.deferredUpdate = false;
			serviceModel.deferredUpdateVersion = null;
			_isTitleBarCloseClick = false;
			return false;
		}
		finally
		{
			_isPromptOpen = false;
		}
	}

	private void HandleDeferredUpdateOnExit()
	{
		if (serviceModel.deferredUpdate && !string.IsNullOrEmpty(serviceModel.deferredUpdateVersion))
		{
			if (!IPCManager.IsSimRunning())
			{
				Logger.Log(LogLevel.Information, "MainWindow:Window_Closing", "MSFS closed. Launching deferred auto-update to v" + serviceModel.deferredUpdateVersion + ".");
				AutoUpdate(serviceModel.deferredUpdateVersion);
				serviceModel.deferredUpdateVersion = null;
			}
			else
			{
				Logger.Log(LogLevel.Information, "MainWindow:Window_Closing", "MSFS is still running. Deferred update to v" + serviceModel.deferredUpdateVersion + " will apply on next launch.");
				serviceModel.deferredUpdateVersion = null;
			}
		}
	}
}

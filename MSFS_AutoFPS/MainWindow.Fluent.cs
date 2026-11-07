using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Ui.Appearance;


namespace MSFS_AutoFPS;

// Fork: Fluent (WPF-UI) shell for the main window — page navigation and theme application.
// Kept separate from the (decompiled) MainWindow.xaml.cs so the UI shell stays easy to follow.
public partial class MainWindow
{
	private void NavItem_Checked(object sender, RoutedEventArgs e)
	{
		if (sender is not FrameworkElement { Tag: string pageName })
		{
			return;
		}
		// Fires during InitializeComponent before the pages exist; the XAML defaults already show Overview.
		if (pageOverview == null || pageTuning == null || pageSettings == null)
		{
			return;
		}
		pageOverview.Visibility = pageName == nameof(pageOverview) ? Visibility.Visible : Visibility.Collapsed;
		pageTuning.Visibility = pageName == nameof(pageTuning) ? Visibility.Visible : Visibility.Collapsed;
		pageSettings.Visibility = pageName == nameof(pageSettings) ? Visibility.Visible : Visibility.Collapsed;
		if (txtAboutVersion != null)
		{
			txtAboutVersion.Text = $"Version {AppConfig.Config.AppVersionFull}";
		}
	}

	// ------------------------------------------------------------------------------------------------
	// Widget web API: the in-game / browser widget renders the Tuning and Settings pages from a JSON
	// description generated from the live WPF controls, and changes go back through those same controls,
	// so all existing visibility rules, validation and handlers apply unchanged.
	// ------------------------------------------------------------------------------------------------

	/// <summary>One-time Fluent shell setup (called from the constructor before the web server starts):
	/// title bar close handling, About text and the widget page/control endpoints.</summary>
	private void ConfigureFluentWebApi()
	{
		// The title bar's X keeps the original "[X] asks for confirmation / closes to tray" behaviour, which keys
		// on _isTitleBarCloseClick (previously set from WM_SYSCOMMAND of the native caption button).
		// CloseClicked is raised before the TitleBar closes the window.
		titleBar.CloseClicked += (_, _) => _isTitleBarCloseClick = true;
		if (txtAboutVersion != null)
		{
			txtAboutVersion.Text = $"Version {AppConfig.Config.AppVersionFull}";
		}
		if (_webServer == null)
		{
			return;
		}
		_webServer.GetPageJson = page => Dispatcher.Invoke(() => BuildPageJson(page));
		_webServer.SetControl = body => Dispatcher.Invoke(() => ApplyWidgetControl(body));
	}

	private FrameworkElement PageRoot(string page) => page switch
	{
		"tuning" => pageTuning,
		"settings" => pageSettings,
		_ => null,
	};

	private string BuildPageJson(string page)
	{
		FrameworkElement root = PageRoot(page);
		if (root == null)
		{
			return null;
		}
		var cardStyle = TryFindResource("Card") as Style;
		var titleStyle = TryFindResource("CardTitle") as Style;
		var pageTitleStyle = TryFindResource("PageTitle") as Style;

		using var ms = new System.IO.MemoryStream();
		using (var w = new System.Text.Json.Utf8JsonWriter(ms))
		{
			w.WriteStartObject();
			w.WriteString("page", page);
			w.WriteStartArray("cards");
			var ctx = new PageWriter(w, cardStyle, titleStyle, pageTitleStyle);
			ctx.WalkChildren(root); // the page itself is Collapsed while not shown in the app; walk its content
			ctx.CloseCard();
			w.WriteEndArray();
			w.WriteEndObject();
		}
		return Encoding.UTF8.GetString(ms.ToArray());
	}

	/// <summary>Walks a page's logical tree and writes cards → rows → controls, skipping anything not Visible.</summary>
	private sealed class PageWriter
	{
		private readonly System.Text.Json.Utf8JsonWriter w;
		private readonly Style cardStyle, titleStyle, pageTitleStyle;
		private bool cardOpen, rowOpen;

		public PageWriter(System.Text.Json.Utf8JsonWriter writer, Style card, Style title, Style pageTitle)
		{
			w = writer;
			cardStyle = card;
			titleStyle = title;
			pageTitleStyle = pageTitle;
		}

		public void Walk(object node)
		{
			if (node is not DependencyObject d)
			{
				return;
			}
			if (d is UIElement ui && ui.Visibility != Visibility.Visible)
			{
				return;
			}
			if (d is Border b && cardStyle != null && b.Style == cardStyle)
			{
				CloseCard();
				w.WriteStartObject();
				w.WriteStartArray("rows");
				cardOpen = true;
				WalkChildren(b);
				CloseCard();
				return;
			}
			if (d is TextBlock tb && tb.Style != null && tb.Style == pageTitleStyle)
			{
				return;
			}
			if (d is TextBlock title && titleStyle != null && title.Style == titleStyle)
			{
				EnsureCard();
				CloseRow();
				w.WriteStartObject();
				w.WriteString("t", "title");
				w.WriteString("text", title.Text);
				w.WriteEndObject();
				return;
			}
			if (IsRowPanel(d))
			{
				EnsureCard();
				CloseRow();
				OpenRow();
				WalkRowItems(d);
				CloseRow();
				return;
			}
			if (IsLeaf(d))
			{
				EnsureCard();
				CloseRow();
				OpenRow();
				WriteLeaf((FrameworkElement)d);
				CloseRow();
				return;
			}
			WalkChildren(d);
		}

		public void WalkChildren(DependencyObject d)
		{
			foreach (object child in LogicalTreeHelper.GetChildren(d))
			{
				Walk(child);
			}
		}

		/// <summary>Inside a row: flatten nested containers, emit visible leaves in order.</summary>
		private void WalkRowItems(DependencyObject d)
		{
			foreach (object child in LogicalTreeHelper.GetChildren(d))
			{
				if (child is UIElement ui && ui.Visibility != Visibility.Visible)
				{
					continue;
				}
				if (child is DependencyObject cd && IsLeaf(cd))
				{
					WriteLeaf((FrameworkElement)cd);
				}
				else if (child is DependencyObject nested)
				{
					WalkRowItems(nested);
				}
			}
		}

		private static bool IsRowPanel(DependencyObject d) =>
			d is WrapPanel || d is System.Windows.Controls.Primitives.UniformGrid || (d is StackPanel sp && sp.Orientation == Orientation.Horizontal);

		private static bool IsLeaf(DependencyObject d) =>
			d is Label || d is TextBox || d is ComboBox || d is CheckBox || d is Button || d is TextBlock;

		private void WriteLeaf(FrameworkElement e)
		{
			w.WriteStartObject();
			switch (e)
			{
				case CheckBox c:
					w.WriteString("t", "check");
					w.WriteString("name", c.Name);
					w.WriteString("text", ContentText(c.Content));
					w.WriteBoolean("checked", c.IsChecked == true);
					break;
				case Button btn:
					w.WriteString("t", "button");
					w.WriteString("name", btn.Name);
					w.WriteString("text", ContentText(btn.Content).Replace("_", ""));
					break;
				case ComboBox cb:
					w.WriteString("t", "combo");
					w.WriteString("name", cb.Name);
					w.WriteNumber("index", cb.SelectedIndex);
					w.WriteStartArray("items");
					foreach (object item in cb.Items)
					{
						w.WriteStringValue(ContentText(item is ComboBoxItem cbi ? cbi.Content : item).Trim());
					}
					w.WriteEndArray();
					WriteWarn(cb.Background);
					break;
				case TextBox t:
					w.WriteString("t", "text");
					w.WriteString("name", t.Name);
					w.WriteString("value", t.Text);
					WriteWarn(t.Background);
					break;
				case Label l:
					w.WriteString("t", "label");
					w.WriteString("text", ContentText(l.Content));
					WriteColor(l.Foreground);
					break;
				case TextBlock tb:
					w.WriteString("t", "note");
					w.WriteString("text", tb.Text);
					break;
			}
			if (e is Control)
			{
				w.WriteBoolean("enabled", e.IsEnabled);
			}
			string tip = TooltipText(e.ToolTip);
			if (!string.IsNullOrWhiteSpace(tip))
			{
				w.WriteString("tip", tip.Trim());
			}
			w.WriteEndObject();
		}

		private void WriteColor(Brush brush)
		{
			if (brush is SolidColorBrush s && !IsNeutral(s.Color))
			{
				w.WriteString("color", $"#{s.Color.R:X2}{s.Color.G:X2}{s.Color.B:X2}");
			}
		}

		private void WriteWarn(Brush background)
		{
			if (background is SolidColorBrush s && s.Color.A > 0x80 && !IsNeutral(s.Color))
			{
				w.WriteString("warn", $"#{s.Color.R:X2}{s.Color.G:X2}{s.Color.B:X2}");
			}
		}

		private static bool IsNeutral(Color c) =>
			Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B)) < 24;

		private void EnsureCard()
		{
			if (cardOpen)
			{
				return;
			}
			w.WriteStartObject();
			w.WriteStartArray("rows");
			cardOpen = true;
		}

		public void CloseCard()
		{
			CloseRow();
			if (!cardOpen)
			{
				return;
			}
			w.WriteEndArray();
			w.WriteEndObject();
			cardOpen = false;
		}

		private void OpenRow()
		{
			w.WriteStartArray();
			rowOpen = true;
		}

		private void CloseRow()
		{
			if (!rowOpen)
			{
				return;
			}
			w.WriteEndArray();
			rowOpen = false;
		}
	}

	private static string ContentText(object content) => content switch
	{
		null => "",
		string s => s,
		TextBlock tb => InlineText(tb),
		_ => content.ToString(),
	};

	private static string TooltipText(object tip) => tip switch
	{
		null => null,
		string s => s,
		TextBlock tb => InlineText(tb),
		ToolTip tt => TooltipText(tt.Content),
		_ => null,
	};

	private static string InlineText(TextBlock tb)
	{
		if (tb.Inlines.Count == 0)
		{
			return tb.Text;
		}
		var sb = new StringBuilder();
		foreach (Inline inline in tb.Inlines)
		{
			if (inline is Run r)
			{
				sb.Append(r.Text);
			}
			else if (inline is LineBreak)
			{
				sb.Append('\n');
			}
		}
		return sb.ToString();
	}

	/// <summary>Applies {"name":..,"value":..} through the named control on the Tuning or Settings page.</summary>
	private string ApplyWidgetControl(string body)
	{
		string name;
		System.Text.Json.JsonElement value;
		try
		{
			using var doc = System.Text.Json.JsonDocument.Parse(body);
			name = doc.RootElement.GetProperty("name").GetString();
			value = doc.RootElement.TryGetProperty("value", out var v) ? v.Clone() : default;
		}
		catch
		{
			return "{\"ok\":false,\"error\":\"bad request\"}";
		}
		if (string.IsNullOrEmpty(name) || FindName(name) is not FrameworkElement el || !IsOnWidgetPage(el))
		{
			return "{\"ok\":false,\"error\":\"unknown control\"}";
		}
		if (!el.IsEnabled)
		{
			return "{\"ok\":false,\"error\":\"disabled\"}";
		}
		switch (el)
		{
			case CheckBox chk:
			{
				bool want = value.ValueKind == System.Text.Json.JsonValueKind.True
					|| (value.ValueKind == System.Text.Json.JsonValueKind.String && value.GetString() == "true");
				if (chk.IsChecked != want)
				{
					chk.IsChecked = want;
					chk.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, chk));
				}
				break;
			}
			case ComboBox cb:
			{
				int idx = value.ValueKind == System.Text.Json.JsonValueKind.Number ? value.GetInt32()
					: int.TryParse(value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : "", out int p) ? p : -1;
				if (idx < 0 || idx >= cb.Items.Count)
				{
					return "{\"ok\":false,\"error\":\"index out of range\"}";
				}
				cb.SelectedIndex = idx;
				break;
			}
			case TextBox tb:
			{
				string text = value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString()
					: value.ValueKind == System.Text.Json.JsonValueKind.Number ? value.GetRawText() : null;
				if (string.IsNullOrWhiteSpace(text) || text.Length > 16)
				{
					return "{\"ok\":false,\"error\":\"bad value\"}";
				}
				tb.Text = text.Trim();
				TextBox_SetSetting(tb);
				break;
			}
			case Button btn:
				btn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, btn));
				break;
			default:
				return "{\"ok\":false,\"error\":\"not editable\"}";
		}
		return "{\"ok\":true}";
	}

	/// <summary>Only controls that live on the Tuning or Settings page may be changed remotely.</summary>
	private bool IsOnWidgetPage(DependencyObject el)
	{
		for (DependencyObject cur = el; cur != null; cur = LogicalTreeHelper.GetParent(cur))
		{
			if (ReferenceEquals(cur, pageTuning) || ReferenceEquals(cur, pageSettings))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Switches the WPF-UI theme (and Mica backdrop) to match the app's Dark setting.</summary>
	private void ApplyFluentTheme()
	{
		ApplicationThemeManager.Apply(serviceModel.isDarkMode ? ApplicationTheme.Dark : ApplicationTheme.Light, Wpf.Ui.Controls.WindowBackdropType.Mica, updateAccent: true);
	}
}

/// <summary>Small XAML helpers for the Fluent shell.</summary>
public static class FluentUi
{
	/// <summary>Visible ⇄ Collapsed, e.g. to show a hint while a section is hidden.</summary>
	public static readonly IValueConverter InverseVisibility = new InverseVisibilityConverter();

	/// <summary>Visible if any bound Visibility is Visible — hides a card whose rows code-behind has all collapsed.</summary>
	public static readonly IMultiValueConverter AnyVisible = new AnyVisibleConverter();

	private sealed class AnyVisibleConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
			=> Array.Exists(values, v => v is Visibility vis && vis == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();
	}

	private sealed class InverseVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			=> value is Visibility v && v == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			=> throw new NotSupportedException();
	}
}

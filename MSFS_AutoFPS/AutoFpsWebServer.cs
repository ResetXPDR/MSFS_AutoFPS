using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MSFS_AutoFPS;

public partial class AutoFpsWebServer : IDisposable
{
	private TcpListener _tcpListener;

	private CancellationTokenSource _cts;

	private readonly Func<string> _getTelemetryJson;

	private readonly Action _onReset;

	private readonly Action<int> _onSetFlightType;

	private readonly int _port;

	private bool _isRunning;

	private readonly Action _onToggleTitleBar;

	private readonly Action _onToggleDarkMode;

	private readonly Action<string> _onSetHotkey;

	private readonly Func<string> _getControlsJson;

	private readonly Action<int> _onSetTargetMode;

	private readonly Action<string> _onSetTarget;

	private readonly Action<string> _onSetTargetFps;

	private readonly Action _onToggleExpert;

	private readonly Action _onToggleLogPlus;

	private readonly Action<bool> _onSetGeneralCollapsed;

	private readonly Action<int> _onSetWidgetAutoHide;

	private readonly Action _onToggleFpsSource;

	private long _lastRequestTicks;

	public bool IsWidgetConnected
	{
		get
		{
			if (_lastRequestTicks > 0)
			{
				return DateTime.UtcNow.Ticks - Interlocked.Read(in _lastRequestTicks) < TimeSpan.FromSeconds(2.0).Ticks;
			}
			return false;
		}
	}

	public AutoFpsWebServer(int port, Func<string> getTelemetryJson, Action onReset, Action<int> onSetFlightType, Action onToggleTitleBar = null, Action onToggleDarkMode = null, Action<string> onSetHotkey = null, Func<string> getControlsJson = null, Action<int> onSetTargetMode = null, Action<string> onSetTarget = null, Action<string> onSetTargetFps = null, Action onToggleExpert = null, Action onToggleLogPlus = null, Action<bool> onSetGeneralCollapsed = null, Action<int> onSetWidgetAutoHide = null, Action onToggleFpsSource = null)
	{
		_port = port;
		_getTelemetryJson = getTelemetryJson;
		_onReset = onReset;
		_onSetFlightType = onSetFlightType;
		_onToggleTitleBar = onToggleTitleBar;
		_onToggleDarkMode = onToggleDarkMode;
		_onSetHotkey = onSetHotkey;
		_getControlsJson = getControlsJson;
		_onSetTargetMode = onSetTargetMode;
		_onSetTarget = onSetTarget;
		_onSetTargetFps = onSetTargetFps;
		_onToggleExpert = onToggleExpert;
		_onToggleLogPlus = onToggleLogPlus;
		_onSetGeneralCollapsed = onSetGeneralCollapsed;
		_onSetWidgetAutoHide = onSetWidgetAutoHide;
		_onToggleFpsSource = onToggleFpsSource;
	}

	public void Start()
	{
		if (_isRunning)
		{
			return;
		}
		try
		{
			_cts = new CancellationTokenSource();
			_tcpListener = new TcpListener(IPAddress.Loopback, _port);
			_tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
			_tcpListener.Start();
			_isRunning = true;
			Logger.Log(LogLevel.Information, "WebServer", $"Web server started reliably on port {_port}");
			Task.Run(() => AcceptClientsAsync(_cts.Token));
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "WebServer", "Failed to start web server: " + ex.Message);
		}
	}

	public void Stop()
	{
		_isRunning = false;
		try
		{
			_cts?.Cancel();
			_tcpListener?.Stop();
			_tcpListener = null;
		}
		catch
		{
		}
	}

	public void Dispose()
	{
		Stop();
	}

	private async Task AcceptClientsAsync(CancellationToken token)
	{
		while (!token.IsCancellationRequested && _isRunning)
		{
			try
			{
				TcpClient client = await _tcpListener.AcceptTcpClientAsync();
				Task.Run(() => HandleClientAsync(client), token);
			}
			catch
			{
				if (token.IsCancellationRequested || !_isRunning)
				{
					break;
				}
			}
		}
	}

	private async Task HandleClientAsync(TcpClient client)
	{
		using (client)
		{
			client.ReceiveTimeout = 2000;
			client.SendTimeout = 2000;
			client.NoDelay = true;
			try
			{
				using NetworkStream stream = client.GetStream();
				// Fork: read the full request (headers + Content-Length body); clients may send the body
				// in a separate packet (e.g. after "Expect: 100-continue"), which a single read missed.
				string text = await ReadRequestAsync(stream);
				if (text == null)
				{
					return;
				}
				string[] array = text.Split(new string[2] { "\r\n", "\n" }, StringSplitOptions.None)[0].Split(' ');
				string text2 = ((array.Length != 0) ? array[0].ToUpper() : "");
				string text3 = ((array.Length > 1) ? array[1].ToLower() : "/");
				int num2 = text3.IndexOf('?');
				if (num2 >= 0)
				{
					text3 = text3.Substring(0, num2);
				}
				string text4 = "";
				int num3 = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
				if (num3 >= 0 && num3 + 4 < text.Length)
				{
					text4 = text.Substring(num3 + 4);
				}
				if (text2 == "OPTIONS")
				{
					string s = "HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
					byte[] bytes = Encoding.UTF8.GetBytes(s);
					await stream.WriteAsync(bytes, 0, bytes.Length);
					return;
				}
				int value = 200;
				string value2 = "OK";
				byte[] bodyBytes;
				string text5;
				if (text3 == "/api/status")
				{
					Interlocked.Exchange(ref _lastRequestTicks, DateTime.UtcNow.Ticks);
					string json;
					try
					{
						json = _getTelemetryJson() ?? "{}";
					}
					catch
					{
						json = "{\"fps\":\"n/a\",\"tlod\":\"n/a\",\"olod\":\"n/a\",\"agl\":\"n/a\",\"fpm\":\"n/a\",\"clouds\":\"n/a\"}";
					}
					json = AddControlsToJson(json);
					bodyBytes = Encoding.UTF8.GetBytes(json);
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/reset")
				{
					try
					{
						_onReset?.Invoke();
					}
					catch
					{
					}
					bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/toggletitlebar")
				{
					try
					{
						_onToggleTitleBar?.Invoke();
					}
					catch
					{
					}
					bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/toggledarkmode")
				{
					try
					{
						_onToggleDarkMode?.Invoke();
					}
					catch
					{
					}
					bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/toggleexpert")
				{
					try
					{
						_onToggleExpert?.Invoke();
					}
					catch
					{
					}
					bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/togglelogplus")
				{
					try
					{
						_onToggleLogPlus?.Invoke();
					}
					catch
					{
					}
					bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/hotkey")
				{
					try
					{
						_onSetHotkey?.Invoke(text4.Trim());
					}
					catch
					{
					}
					bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/flighttype")
				{
					if (int.TryParse(text4.Trim(), out var result) && result >= 0)
					{
						try
						{
							_onSetFlightType?.Invoke(result);
						}
						catch
						{
						}
						bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					}
					else
					{
						bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"missing index\"}");
					}
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/targetmode")
				{
					if (int.TryParse(text4.Trim(), out var result2) && result2 >= 0)
					{
						try
						{
							_onSetTargetMode?.Invoke(result2);
						}
						catch
						{
						}
						bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					}
					else
					{
						bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"missing index\"}");
					}
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/target")
				{
					string text6 = text4.Trim();
					if (text6.Length > 0 && text6.Length <= 16)
					{
						try
						{
							_onSetTarget?.Invoke(text6);
						}
						catch
						{
						}
						bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					}
					else
					{
						bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"bad request\"}");
					}
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/targetfps")
				{
					string text7 = text4.Trim();
					if (text7.Length > 0 && text7.Length <= 16)
					{
						try
						{
							_onSetTargetFps?.Invoke(text7);
						}
						catch
						{
						}
						bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					}
					else
					{
						bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"bad request\"}");
					}
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/generalcollapsed")
				{
					bool obj12 = text4.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
					try
					{
						_onSetGeneralCollapsed?.Invoke(obj12);
					}
					catch
					{
					}
					bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/widgetautohide")
				{
					if (int.TryParse(text4.Trim(), out var result3) && result3 >= 0 && result3 <= 2)
					{
						try
						{
							_onSetWidgetAutoHide?.Invoke(result3);
						}
						catch
						{
						}
						bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					}
					else
					{
						bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"missing or invalid index\"}");
					}
					text5 = "application/json";
				}
				else if (text2 == "POST" && text3 == "/api/togglefpssource")
				{
					try
					{
						_onToggleFpsSource?.Invoke();
					}
					catch
					{
					}
					bodyBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
					text5 = "application/json";
				}
				else if (TryHandleFluentRequest(text2, text3, text4, out bodyBytes, out text5, out value, out value2))
				{
					// Fork: shared Fluent widget files, /api/page/* and /api/control (AutoFpsWebServer.Fluent.cs)
				}
				else if (text3 == "/" || text3 == "/index.html")
				{
					bodyBytes = Encoding.UTF8.GetBytes(GetWidgetHtml());
					text5 = "text/html; charset=utf-8";
				}
				else
				{
					bodyBytes = Encoding.UTF8.GetBytes("404 Not Found");
					text5 = "text/plain";
					value = 404;
					value2 = "Not Found";
				}
				string s2 = $"HTTP/1.1 {value} {value2}\r\n" + "Access-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Content-Type\r\nContent-Type: " + text5 + "\r\n" + $"Content-Length: {bodyBytes.Length}\r\n" + "Connection: close\r\n\r\n";
				byte[] bytes2 = Encoding.UTF8.GetBytes(s2);
				await stream.WriteAsync(bytes2, 0, bytes2.Length);
				await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
				await stream.FlushAsync();
			}
			catch
			{
			}
		}
	}

	private string AddControlsToJson(string json)
	{
		if (_getControlsJson == null || string.IsNullOrWhiteSpace(json))
		{
			return json;
		}
		string text;
		try
		{
			text = _getControlsJson();
		}
		catch
		{
			return json;
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			return json;
		}
		int num = json.LastIndexOf('}');
		if (num < 0)
		{
			return json;
		}
		string text2 = json.Substring(0, num).TrimEnd();
		string text3 = (text2.EndsWith("{") ? "" : ",");
		return text2 + text3 + "\"autoFpsControls\":" + text + "}";
	}

	private string GetWidgetHtml()
	{
		return "<!DOCTYPE html>\r\n<html lang='en'>\r\n<head>\r\n    <meta charset='UTF-8'>\r\n    <meta name='viewport' content='width=device-width, initial-scale=1.0'>\r\n    <meta name='color-scheme' content='light'>\r\n    <meta name='supported-color-schemes' content='light'>\r\n    <title>AutoFPS Live Widget</title>\r\n    <style>\r\n        * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; user-select: none; }\r\n        html, body { \r\n            padding: 10px; \r\n            width: 420px; \r\n            color-scheme: light; \r\n            transition: background-color 0.2s ease, color 0.2s ease; \r\n            background-color: #121418;\r\n            color: #F0F0F0;\r\n        }\r\n        #widget-toolbar {\r\n            display: flex;\r\n            align-items: center;\r\n            justify-content: space-between;\r\n            padding-bottom: 8px;\r\n        }\r\n        #toolbar-left {\r\n            display: flex;\r\n            align-items: center;\r\n            gap: 6px;\r\n        }\r\n        #widget-title {\r\n            font-size: 13px;\r\n            font-weight: 700;\r\n            white-space: nowrap;\r\n        }\r\n        #widget-status {\r\n            color: #FFD166;\r\n            font-weight: 600;\r\n            font-size: 13px;\r\n        }\r\n        .auto-hide-group {\r\n            display: inline-flex;\r\n            align-items: center;\r\n            margin-right: 2px;\r\n        }\r\n        .auto-hide-label {\r\n            font-size: 11.5px;\r\n            font-weight: 600;\r\n            white-space: nowrap;\r\n            margin-right: 6px;\r\n        }\r\n        #autoHideDropdown {\r\n            width: 74px;\r\n            flex: 0 0 74px;\r\n            margin-right: 0;\r\n        }\r\n        .group-box { border: 1px solid #50555E; border-radius: 4px; padding: 7px 9px; margin-bottom: 7px; transition: border-color 0.2s ease; }\r\n        .group-header { font-size: 13px; font-weight: 600; padding: 0 4px; }\r\n        .collapsible-header { cursor: pointer; user-select: none; }\r\n        .collapsible-header:hover { filter: brightness(1.25); }\r\n        .chevron { display: inline-block; width: 0.85em; height: 0.85em; margin-left: 0.35em; vertical-align: -0.08em; }\r\n        .chevron svg { width: 100%; height: 100%; display: block; }\r\n        .conn-row { display: flex; align-items: center; justify-content: space-around; font-size: 13px; font-weight: 600; padding: 3px 0; }\r\n        \r\n        .status-msg {\r\n            font-size: 12px;\r\n            font-weight: 500;\r\n            text-align: center;\r\n            width: 100%;\r\n            padding: 4px 2px 0 2px;\r\n            margin-top: 4px;\r\n            min-height: 16px;\r\n            word-break: break-word;\r\n            white-space: pre-line;\r\n            color: #FFF;\r\n        }\r\n        .status-msg.status-compact {\r\n            font-size: 10.5px;\r\n        }\r\n\r\n        .controls-row {\r\n            display: flex;\r\n            align-items: center;\r\n            justify-content: space-between;\r\n            gap: 6px;\r\n            padding: 2px 0;\r\n            font-size: 12px;\r\n        }\r\n        .controls-row .label {\r\n            flex: 0 0 auto;\r\n            white-space: nowrap;\r\n            font-weight: 500;\r\n        }\r\n        .custom-select {\r\n            position: relative;\r\n            flex: 1 1 auto;\r\n        }\r\n        .custom-select-btn {\r\n            width: 100%;\r\n            text-align: left;\r\n            background: #1b1e22;\r\n            color: inherit;\r\n            border: 1px solid #50555E;\r\n            border-radius: 3px;\r\n            padding: 3px 6px;\r\n            font-size: 11.5px;\r\n            cursor: pointer;\r\n            display: flex;\r\n            align-items: center;\r\n            justify-content: space-between;\r\n        }\r\n        .custom-select-btn::after {\r\n            content: '';\r\n            border-left: 4px solid transparent;\r\n            border-right: 4px solid transparent;\r\n            border-top: 5px solid currentColor;\r\n            margin-left: 4px;\r\n        }\r\n        .custom-select-list {\r\n            position: absolute;\r\n            top: 100%;\r\n            left: 0;\r\n            right: 0;\r\n            z-index: 50;\r\n            margin-top: 2px;\r\n            max-height: 120px;\r\n            overflow-y: auto;\r\n            background: #1b1e22;\r\n            border: 1px solid #50555E;\r\n            border-radius: 3px;\r\n        }\r\n        .custom-select-list.hidden { display: none; }\r\n        .custom-select-item {\r\n            padding: 4px 8px;\r\n            font-size: 11.5px;\r\n            color: inherit;\r\n            cursor: pointer;\r\n        }\r\n        .custom-select-item:hover {\r\n            background: rgba(128, 128, 128, 0.25);\r\n        }\r\n        .custom-select-item.active {\r\n            background: #3a4a5a;\r\n            color: #FFF !important;\r\n            font-weight: 600;\r\n        }\r\n\r\n        .custom-select-btn:hover,\r\n        .stepper-btn:hover,\r\n        #resetBtn:hover,\r\n        .chk-label:hover .custom-checkbox {\r\n            border-color: #60A5FA !important;\r\n        }\r\n\r\n        #resetBtn {\r\n            flex: 0 0 auto;\r\n            background: #2a2a2a;\r\n            color: #ddd;\r\n            border: 1px solid #555;\r\n            border-radius: 3px;\r\n            cursor: pointer;\r\n            padding: 0 9px;\r\n            height: 22px;\r\n            font-size: 11.5px;\r\n            font-weight: 600;\r\n            display: inline-flex;\r\n            align-items: center;\r\n            box-sizing: border-box;\r\n        }\r\n        #resetBtn:hover { opacity: 0.85; }\r\n        #resetBtn:disabled { opacity: 0.5; cursor: default; }\r\n\r\n        .chk-label {\r\n            display: inline-flex;\r\n            align-items: center;\r\n            font-size: 12px;\r\n            cursor: pointer;\r\n            user-select: none;\r\n            white-space: nowrap;\r\n        }\r\n        .chk-label input[type='checkbox'].chk-input-native {\r\n            position: absolute;\r\n            width: 1px;\r\n            height: 1px;\r\n            margin: -1px;\r\n            opacity: 0;\r\n        }\r\n        .custom-checkbox {\r\n            flex: 0 0 auto;\r\n            position: relative;\r\n            box-sizing: border-box;\r\n            width: 14px;\r\n            height: 14px;\r\n            margin-right: 5px;\r\n            border: 1px solid #50555E;\r\n            border-radius: 3px;\r\n            background: #1b1e22;\r\n        }\r\n        .custom-checkbox::after {\r\n            content: '';\r\n            position: absolute;\r\n            display: none;\r\n            left: 4px;\r\n            top: 1px;\r\n            width: 4px;\r\n            height: 8px;\r\n            border: solid currentColor;\r\n            border-width: 0 2px 2px 0;\r\n            transform: rotate(45deg);\r\n            color: #F0F0F0;\r\n        }\r\n        .chk-input-native:checked ~ .custom-checkbox::after {\r\n            display: block;\r\n        }\r\n\r\n        .controls-row.target-row {\r\n            position: relative;\r\n            justify-content: flex-start;\r\n            gap: 5px;\r\n            margin-top: 2px;\r\n        }\r\n        .target-row .custom-select { flex: 0 0 74px; width: 74px; min-width: 0; }\r\n        .target-row #targetDropdown { position: static; }\r\n        .target-row .custom-select-btn { white-space: nowrap; overflow: hidden; }\r\n        .custom-select-list.target-list {\r\n            display: grid;\r\n            grid-template-columns: repeat(4, 1fr);\r\n            left: 0;\r\n            right: 0;\r\n            max-height: none;\r\n            overflow: visible;\r\n        }\r\n        .custom-select-list.target-list.hidden { display: none; }\r\n        .target-list .custom-select-item { text-align: center; white-space: nowrap; padding: 5px 2px; }\r\n        .fps-group { display: flex; align-items: center; gap: 5px; flex: 0 0 auto; margin-left: auto; }\r\n        .stepper { display: flex; align-items: center; gap: 3px; }\r\n        .stepper-btn {\r\n            display: flex;\r\n            align-items: center;\r\n            justify-content: center;\r\n            width: 24px;\r\n            height: 22px;\r\n            padding: 0;\r\n            background: #2a2a2a;\r\n            color: #ddd;\r\n            border: 1px solid #555;\r\n            border-radius: 3px;\r\n            cursor: pointer;\r\n        }\r\n        .stepper-btn:hover { opacity: 0.85; }\r\n        .stepper-btn:active { opacity: 0.6; }\r\n        .stepper-btn svg { display: block; width: 11px; height: 11px; }\r\n        .stepper-val { min-width: 30px; text-align: center; font-weight: 600; white-space: nowrap; font-size: 12px; }\r\n\r\n        .controls-row.general-row-2 {\r\n            margin-top: 7px;\r\n            justify-content: flex-start;\r\n            gap: 10px;\r\n        }\r\n        .flight-type-group {\r\n            display: flex;\r\n            align-items: center;\r\n            gap: 5px;\r\n            flex: 0 0 auto;\r\n            margin-left: auto;\r\n        }\r\n        .flight-type-group .custom-select {\r\n            width: 116px;\r\n            flex: 0 0 116px;\r\n        }\r\n        #flightTypeList {\r\n            top: auto;\r\n            bottom: 100%;\r\n            margin-top: 0;\r\n            margin-bottom: 2px;\r\n        }\r\n\r\n        .header-icon-svg {\r\n            display: inline-flex;\r\n            align-items: center;\r\n            justify-content: center;\r\n            margin-right: 5px;\r\n            vertical-align: -2px;\r\n        }\r\n        .header-icon-svg svg {\r\n            width: 14px;\r\n            height: 14px;\r\n            display: block;\r\n        }\r\n\r\n        .sim-grid { \r\n            display: grid; \r\n            grid-template-columns: 60px 1fr 60px 1fr 60px 1fr; \r\n            row-gap: 7px; \r\n            column-gap: 0; \r\n            align-items: center; \r\n            font-size: 12px; \r\n            padding: 3px 8px; \r\n        }\r\n        .sim-grid .label { font-weight: 500; text-align: left; white-space: nowrap; }\r\n        .sim-grid .val { font-weight: 600; text-align: left; padding-left: 3px; box-sizing: border-box; white-space: nowrap; }\r\n        .sim-grid > :nth-child(n+7) { margin-top: 4px; }\r\n\r\n        .fps-val-container {\r\n            display: inline-flex;\r\n            align-items: center;\r\n            justify-content: flex-start;\r\n        }\r\n\r\n        .fps-source-icon {\r\n            display: inline-flex;\r\n            align-items: center;\r\n            justify-content: center;\r\n            margin-left: 9px;\r\n            vertical-align: middle;\r\n            cursor: pointer !important;\r\n        }\r\n        .fps-source-icon svg {\r\n            width: 15px;\r\n            height: 15px;\r\n            display: block;\r\n        }\r\n\r\n        .hidden { display: none !important; }\r\n\r\n        .icon-glyph {\r\n            display: inline-block;\r\n            width: 0.75em;\r\n            height: 0.75em;\r\n            vertical-align: -0.05em;\r\n        }\r\n        .icon-glyph svg {\r\n            display: block;\r\n            width: 100%;\r\n            height: 100%;\r\n        }\r\n    </style>\r\n</head>\r\n<body>\r\n    <div id='widget-toolbar'>\r\n        <div id='toolbar-left'>\r\n            <span id='widget-title'>AutoFPS Web Widget</span>\r\n            <span id='widget-status'></span>\r\n        </div>\r\n        <div class='auto-hide-group' id='autoHideGroup'>\r\n            <span class='auto-hide-label' id='autoHideLabel'>Auto-Hide</span>\r\n            <div id='autoHideDropdown' class='custom-select'>\r\n                <button type='button' id='autoHideBtn' class='custom-select-btn'>-</button>\r\n                <div id='autoHideList' class='custom-select-list hidden'></div>\r\n            </div>\r\n        </div>\r\n    </div>\r\n\r\n    <!-- 1. Connection Status -->\r\n    <fieldset class='group-box'>\r\n        <legend class='group-header'>Connection Status</legend>\r\n        <div class='conn-row'>\r\n            <span id='connMSFS'>MSFS</span>\r\n            <span id='connSimConnect'>SimConnect</span>\r\n            <span id='connSession'>Session</span>\r\n        </div>\r\n    </fieldset>\r\n\r\n    <!-- 2. Sim Values -->\r\n    <fieldset class='group-box'>\r\n        <legend class='group-header'>\r\n            <span id='hdrSimFS' class='header-icon-svg' style='display: none;'><svg viewBox='0 0 16 16'><text x='1' y='13' font-family='sans-serif' font-weight='900' font-style='italic' font-size='13' fill='#38BDF8'>F</text><text x='8' y='13' font-family='sans-serif' font-weight='900' font-style='italic' font-size='13' fill='#94A3B8'>S</text></svg></span>\r\n            <span id='hdrSimDog' class='header-icon-svg' style='display: none;'><svg viewBox='0 0 16 16'><path d='M3 5c-1 0-2 2-1 4l2 1z M13 5c1 0 2 2 1 4l-2 1z' fill='#C28A53'/><path d='M3 6c0-3 10-3 10 0v5c0 3-2 5-5 5s-5-2-5-5z' fill='#E8B882'/><circle cx='6' cy='8' r='1' fill='#222'/><circle cx='10' cy='8' r='1' fill='#222'/><ellipse cx='8' cy='11' rx='1.6' ry='1' fill='#442B15'/></svg></span>\r\n            <span>Sim Values</span>\r\n        </legend>\r\n        <div class='sim-grid'>\r\n            <span id='lblFps' class='label'>FPS+:</span>\r\n            <span class='val fps-val-container'><span id='valFps'>n/a</span><span id='imgFpsSource' class='fps-source-icon' style='display: none;'></span></span>\r\n            <span id='lblTlod' class='label'>TLOD:</span>\r\n            <span id='valTlod' class='val'>n/a</span>\r\n            <span id='lblOlod' class='label'>OLOD:</span>\r\n            <span id='valOlod' class='val'>n/a</span>\r\n\r\n            <span id='lblAgl' class='label'>AGL:</span>\r\n            <span id='valAgl' class='val'>n/a</span>\r\n            <span id='lblFpm' class='label'>FPM:</span>\r\n            <span id='valFpm' class='val'>n/a</span>\r\n            <span id='lblClouds' class='label'>Clouds:</span>\r\n            <span id='valClouds' class='val'>n/a</span>\r\n        </div>\r\n    </fieldset>\r\n\r\n    <!-- 3. General (Controls) -->\r\n    <fieldset class='group-box'>\r\n        <legend id='generalHeader' class='group-header collapsible-header' title='Click to show/hide controls'>\r\n            General <span id='generalChevron' class='chevron'><svg viewBox='0 0 10 10'><polygon points='1,1 9,1 5,9' fill='currentColor'/></svg></span>\r\n        </legend>\r\n        <div id='generalControls'>\r\n            <!-- Row 1: Mode / Target / FPS / Dark -->\r\n            <div class='controls-row target-row'>\r\n                <div id='modeDropdown' class='custom-select'>\r\n                    <button type='button' id='modeBtn' class='custom-select-btn'>-</button>\r\n                    <div id='modeList' class='custom-select-list hidden'></div>\r\n                </div>\r\n                <span class='label'>Target</span>\r\n                <div id='targetDropdown' class='custom-select'>\r\n                    <button type='button' id='targetBtn' class='custom-select-btn'>-</button>\r\n                    <div id='targetList' class='custom-select-list target-list hidden'></div>\r\n                </div>\r\n                <div id='fpsGroup' class='fps-group'>\r\n                    <span class='label'>FPS</span>\r\n                    <div class='stepper'>\r\n                        <button type='button' id='fpsDecBtn' class='stepper-btn' title='Decrease target FPS (hold to speed up)'><svg viewBox='0 0 10 10'><rect x='1' y='4.2' width='8' height='1.6' fill='currentColor'/></svg></button>\r\n                        <span id='fpsVal' class='stepper-val'>-</span>\r\n                        <button type='button' id='fpsIncBtn' class='stepper-btn' title='Increase target FPS (hold to speed up)'><svg viewBox='0 0 10 10'><rect x='1' y='4.2' width='8' height='1.6' fill='currentColor'/><rect x='4.2' y='1' width='1.6' height='8' fill='currentColor'/></svg></button>\r\n                    </div>\r\n                </div>\r\n                <label id='darkModeLabel' class='chk-label'>\r\n                    <input type='checkbox' id='darkModeChk' class='chk-input-native' />\r\n                    <span class='custom-checkbox' aria-hidden='true'></span>\r\n                    <span>Dark</span>\r\n                </label>\r\n            </div>\r\n\r\n            <!-- Row 2: Expert / Log+ / Reset / Flight Type -->\r\n            <div class='controls-row general-row-2'>\r\n                <label id='expertLabel' class='chk-label'>\r\n                    <input type='checkbox' id='expertChk' class='chk-input-native' />\r\n                    <span class='custom-checkbox' aria-hidden='true'></span>\r\n                    <span>Expert</span>\r\n                </label>\r\n                <label id='logPlusLabel' class='chk-label'>\r\n                    <input type='checkbox' id='logPlusChk' class='chk-input-native' />\r\n                    <span class='custom-checkbox' aria-hidden='true'></span>\r\n                    <span>Log+</span>\r\n                </label>\r\n                <button type='button' id='resetBtn'>Reset</button>\r\n                <div class='flight-type-group'>\r\n                    <span class='label'>Flight Type</span>\r\n                    <div id='flightTypeDropdown' class='custom-select'>\r\n                        <button type='button' id='flightTypeBtn' class='custom-select-btn'>-</button>\r\n                        <div id='flightTypeList' class='custom-select-list hidden'></div>\r\n                    </div>\r\n                </div>\r\n            </div>\r\n        </div>\r\n        <div id='statusMessage' class='status-msg'>n/a</div>\r\n    </fieldset>\r\n\r\n    <script>\r\n        const flightTypeBtn = document.getElementById('flightTypeBtn');\r\n        const flightTypeList = document.getElementById('flightTypeList');\r\n        const resetBtn = document.getElementById('resetBtn');\r\n        const darkModeChk = document.getElementById('darkModeChk');\r\n        const expertChk = document.getElementById('expertChk');\r\n        const logPlusChk = document.getElementById('logPlusChk');\r\n        const logPlusLabel = document.getElementById('logPlusLabel');\r\n        const titleEl = document.getElementById('widget-title');\r\n        const autoHideBtn = document.getElementById('autoHideBtn');\r\n        const autoHideList = document.getElementById('autoHideList');\r\n        const modeBtn = document.getElementById('modeBtn');\r\n        const modeList = document.getElementById('modeList');\r\n        const targetBtn = document.getElementById('targetBtn');\r\n        const targetList = document.getElementById('targetList');\r\n        const fpsDecBtn = document.getElementById('fpsDecBtn');\r\n        const fpsIncBtn = document.getElementById('fpsIncBtn');\r\n        const fpsValEl = document.getElementById('fpsVal');\r\n        const generalHeader = document.getElementById('generalHeader');\r\n        const generalChevron = document.getElementById('generalChevron');\r\n        const generalControls = document.getElementById('generalControls');\r\n        const customCheckboxes = document.querySelectorAll('.custom-checkbox');\r\n\r\n        // Target FPS stepper settings\r\n        const FPS_MIN = 10;\r\n        const FPS_MAX = 500;\r\n        const FPS_HOLD_DELAY_MS = 400;\r\n        const FPS_REPEAT_MS = 125;\r\n        const FPS_STEP5_AFTER_MS = 2000;\r\n        const FPS_STEP10_AFTER_MS = 4000;\r\n        const FPS_SEND_DEBOUNCE_MS = 450;\r\n\r\n        let lastProfilesKey = null;\r\n        let currentProfiles = [];\r\n        let suppressSyncUntil = 0;\r\n        let currentActiveIndex = -1;\r\n\r\n        // Collapsible General controls\r\n        let isControlsCollapsed = false;\r\n        let suppressGeneralCollapsedSyncUntil = 0;\r\n        const SVG_DOWN = '<svg viewBox=\\'0 0 10 10\\'><polygon points=\\'1,1 9,1 5,9\\' fill=\\'currentColor\\'/></svg>';\r\n        const SVG_RIGHT = '<svg viewBox=\\'0 0 10 10\\'><polygon points=\\'1,1 1,9 9,5\\' fill=\\'currentColor\\'/></svg>';\r\n\r\n        function updateGeneralControlsVisibility() {\r\n            if (!generalControls) return;\r\n            generalControls.classList.toggle('hidden', isControlsCollapsed);\r\n            if (generalChevron) generalChevron.innerHTML = isControlsCollapsed ? SVG_RIGHT : SVG_DOWN;\r\n        }\r\n\r\n        if (generalHeader) {\r\n            generalHeader.addEventListener('click', (e) => {\r\n                e.stopPropagation();\r\n                suppressGeneralCollapsedSyncUntil = Date.now() + 1500;\r\n                isControlsCollapsed = !isControlsCollapsed;\r\n                updateGeneralControlsVisibility();\r\n                fetch('/api/generalcollapsed', {\r\n                    method: 'POST',\r\n                    cache: 'no-store',\r\n                    body: isControlsCollapsed ? 'true' : 'false'\r\n                }).catch(() => {});\r\n            });\r\n        }\r\n\r\n        // Auto-Hide dropdown\r\n        const autoHideItems = ['Off', 'Chase', 'All Ext'];\r\n        let currentAutoHideIndex = 0;\r\n        let suppressAutoHideSyncUntil = 0;\r\n\r\n        function selectAutoHide(idx) {\r\n            suppressAutoHideSyncUntil = Date.now() + 1500;\r\n            currentAutoHideIndex = idx;\r\n            if (autoHideBtn && autoHideItems[idx] !== undefined) autoHideBtn.textContent = autoHideItems[idx];\r\n            markActive(autoHideList, idx);\r\n            closeAllLists();\r\n            fetch('/api/widgetautohide', { method: 'POST', cache: 'no-store', body: String(idx) }).catch(() => {});\r\n        }\r\n\r\n        // SVG icon glyph substitutions\r\n        const ICONS = {\r\n            '\\u25B2': '<svg viewBox=\\'0 0 10 10\\'><polygon points=\\'5,1 9,9 1,9\\' fill=\\'currentColor\\'/></svg>',\r\n            '\\u25BC': '<svg viewBox=\\'0 0 10 10\\'><polygon points=\\'1,1 9,1 5,9\\' fill=\\'currentColor\\'/></svg>',\r\n            '\\u22A5': '<svg viewBox=\\'0 0 10 10\\'><line x1=\\'5\\' y1=\\'1\\' x2=\\'5\\' y2=\\'7\\' stroke=\\'currentColor\\' stroke-width=\\'1.4\\'/><line x1=\\'1\\' y1=\\'9\\' x2=\\'9\\' y2=\\'9\\' stroke=\\'currentColor\\' stroke-width=\\'1.4\\'/></svg>',\r\n            '\\u22A4': '<svg viewBox=\\'0 0 10 10\\'><line x1=\\'5\\' y1=\\'3\\' x2=\\'5\\' y2=\\'9\\' stroke=\\'currentColor\\' stroke-width=\\'1.4\\'/><line x1=\\'1\\' y1=\\'1\\' x2=\\'9\\' y2=\\'1\\' stroke=\\'currentColor\\' stroke-width=\\'1.4\\'/></svg>',\r\n            '\\u0394': '<svg viewBox=\\'0 0 10 10\\'><polygon points=\\'5,1 9,9 1,9\\' fill=\\'none\\' stroke=\\'currentColor\\' stroke-width=\\'1\\'/></svg>',\r\n            '\\u26A1': '<svg viewBox=\\'0 0 10 10\\'><polygon points=\\'6,0 1,6 4.5,6 3.5,10 9,4 5.5,4\\' fill=\\'currentColor\\'/></svg>',\r\n            '\\u23F8': '<svg viewBox=\\'0 0 10 10\\'><rect x=\\'1\\' y=\\'1\\' width=\\'2.5\\' height=\\'8\\' fill=\\'currentColor\\'/><rect x=\\'6.5\\' y=\\'1\\' width=\\'2.5\\' height=\\'8\\' fill=\\'currentColor\\'/></svg>',\r\n            '\\u2744\\uFE0F': '<svg viewBox=\\'0 0 10 10\\'><g stroke=\\'currentColor\\' stroke-width=\\'1\\' stroke-linecap=\\'round\\'><line x1=\\'5\\' y1=\\'0.5\\' x2=\\'5\\' y2=\\'9.5\\'/><line x1=\\'0.5\\' y1=\\'5\\' x2=\\'9.5\\' y2=\\'5\\'/><line x1=\\'1.9\\' y1=\\'1.9\\' x2=\\'8.1\\' y2=\\'8.1\\'/><line x1=\\'8.1\\' y1=\\'1.9\\' x2=\\'1.9\\' y2=\\'8.1\\'/></g></svg>',\r\n            '\\uD83D\\uDD12': '<svg viewBox=\\'0 0 10 10\\'><rect x=\\'2\\' y=\\'4.5\\' width=\\'6\\' height=\\'5\\' rx=\\'0.6\\' fill=\\'currentColor\\'/><path d=\\'M3.3 4.5V3.2a1.7 1.7 0 0 1 3.4 0V4.5\\' fill=\\'none\\' stroke=\\'currentColor\\' stroke-width=\\'1\\'/></svg>'\r\n        };\r\n        const ICON_KEYS = Object.keys(ICONS).sort((a, b) => b.length - a.length);\r\n\r\n        function setTextWithIcons(el, text) {\r\n            el.textContent = '';\r\n            if (!text) return;\r\n            let i = 0;\r\n            while (i < text.length) {\r\n                let matched = null;\r\n                for (let k = 0; k < ICON_KEYS.length; k++) {\r\n                    if (text.indexOf(ICON_KEYS[k], i) === i) { matched = ICON_KEYS[k]; break; }\r\n                }\r\n                if (matched) {\r\n                    const span = document.createElement('span');\r\n                    span.className = 'icon-glyph';\r\n                    span.innerHTML = ICONS[matched];\r\n                    el.appendChild(span);\r\n                    i += matched.length;\r\n                } else {\r\n                    const start = i;\r\n                    i++;\r\n                    while (i < text.length) {\r\n                        let isIconStart = false;\r\n                        for (let k2 = 0; k2 < ICON_KEYS.length; k2++) {\r\n                            if (text.indexOf(ICON_KEYS[k2], i) === i) { isIconStart = true; break; }\r\n                        }\r\n                        if (isIconStart) break;\r\n                        i++;\r\n                    }\r\n                    el.appendChild(document.createTextNode(text.slice(start, i)));\r\n                }\r\n            }\r\n        }\r\n\r\n        function displayProfileName(name) {\r\n            return String(name).replace(/\\u2194/g, '<->');\r\n        }\r\n\r\n        function closeFlightTypeList() {\r\n            if (flightTypeList) flightTypeList.classList.add('hidden');\r\n        }\r\n\r\n        function selectFlightType(idx) {\r\n            suppressSyncUntil = Date.now() + 1500;\r\n            currentActiveIndex = idx;\r\n            if (flightTypeBtn && currentProfiles[idx] !== undefined) {\r\n                flightTypeBtn.textContent = displayProfileName(currentProfiles[idx]);\r\n            }\r\n            closeFlightTypeList();\r\n            fetch('/api/flighttype', { method: 'POST', cache: 'no-store', body: String(idx) }).catch(() => {});\r\n        }\r\n\r\n        function renderFlightTypeList(profiles) {\r\n            currentProfiles = profiles;\r\n            flightTypeList.textContent = '';\r\n            profiles.forEach((name, i) => {\r\n                const item = document.createElement('div');\r\n                item.className = 'custom-select-item';\r\n                item.textContent = displayProfileName(name);\r\n                item.addEventListener('click', (e) => {\r\n                    e.stopPropagation();\r\n                    selectFlightType(i);\r\n                });\r\n                flightTypeList.appendChild(item);\r\n            });\r\n        }\r\n\r\n        function markActiveItem(idx) {\r\n            if (!flightTypeList) return;\r\n            Array.from(flightTypeList.children).forEach((child, i) => {\r\n                child.classList.toggle('active', i === idx);\r\n            });\r\n        }\r\n\r\n        function closeAllLists() {\r\n            [flightTypeList, modeList, targetList, autoHideList].forEach(l => { if (l) l.classList.add('hidden'); });\r\n        }\r\n\r\n        function wireListButton(btn, list) {\r\n            if (!btn || !list) return;\r\n            btn.addEventListener('click', (e) => {\r\n                e.stopPropagation();\r\n                const wasHidden = list.classList.contains('hidden');\r\n                closeAllLists();\r\n                if (wasHidden) list.classList.remove('hidden');\r\n            });\r\n        }\r\n        wireListButton(flightTypeBtn, flightTypeList);\r\n        wireListButton(modeBtn, modeList);\r\n        wireListButton(targetBtn, targetList);\r\n        wireListButton(autoHideBtn, autoHideList);\r\n\r\n        if (autoHideList) renderList(autoHideList, autoHideItems, selectAutoHide);\r\n\r\n        document.addEventListener('click', (e) => {\r\n            if (e.target && e.target.closest && e.target.closest('.custom-select')) return;\r\n            closeAllLists();\r\n        });\r\n\r\n        function renderList(listEl, items, onPick) {\r\n            listEl.textContent = '';\r\n            items.forEach((name, i) => {\r\n                const item = document.createElement('div');\r\n                item.className = 'custom-select-item';\r\n                item.textContent = String(name);\r\n                item.addEventListener('click', (e) => {\r\n                    e.stopPropagation();\r\n                    onPick(i);\r\n                });\r\n                listEl.appendChild(item);\r\n            });\r\n        }\r\n\r\n        function markActive(listEl, idx) {\r\n            if (!listEl) return;\r\n            Array.from(listEl.children).forEach((child, i) => child.classList.toggle('active', i === idx));\r\n        }\r\n\r\n        // Mode (Manual / Auto / Fixed)\r\n        let lastModeKey = null;\r\n        let modeItems = [];\r\n        let currentModeIndex = -1;\r\n        let suppressModeSyncUntil = 0;\r\n\r\n        function pickMode(idx) {\r\n            flushFpsSend();\r\n            suppressModeSyncUntil = Date.now() + 1500;\r\n            currentModeIndex = idx;\r\n            if (modeItems[idx] !== undefined) modeBtn.textContent = modeItems[idx];\r\n            markActive(modeList, idx);\r\n            closeAllLists();\r\n            fetch('/api/targetmode', { method: 'POST', cache: 'no-store', body: String(idx) }).catch(() => {});\r\n        }\r\n\r\n        // Target (graphics mode or manual FG multipliers)\r\n        let lastTargetKey = null;\r\n        let targetListKind = 'auto';\r\n        let targetItems = [];\r\n        let targetSwapIndex = -1;\r\n        let currentTargetIndex = -1;\r\n        let suppressTargetSyncUntil = 0;\r\n\r\n        function pickTarget(idx) {\r\n            flushFpsSend();\r\n            closeAllLists();\r\n            if (idx !== targetSwapIndex) {\r\n                suppressTargetSyncUntil = Date.now() + 1500;\r\n                currentTargetIndex = idx;\r\n                if (targetItems[idx] !== undefined) targetBtn.textContent = targetItems[idx];\r\n                markActive(targetList, idx);\r\n            }\r\n            fetch('/api/target', { method: 'POST', cache: 'no-store', body: targetListKind + ':' + idx }).catch(() => {});\r\n        }\r\n\r\n        // Target FPS stepper\r\n        let fpsValue = null;\r\n        let suppressFpsSyncUntil = 0;\r\n        let fpsDirty = false;\r\n        let fpsSendTimer = null;\r\n        let holdDir = 0;\r\n        let holdStart = 0;\r\n        let holdDelayTimer = null;\r\n        let holdRepeatTimer = null;\r\n\r\n        function formatFps(v) {\r\n            if (v === null || !isFinite(v)) return '-';\r\n            return String(Math.round(v * 10) / 10);\r\n        }\r\n\r\n        function showFps() {\r\n            if (fpsValEl) fpsValEl.textContent = formatFps(fpsValue);\r\n        }\r\n\r\n        function flushFpsSend() {\r\n            if (fpsSendTimer) { clearTimeout(fpsSendTimer); fpsSendTimer = null; }\r\n            if (!fpsDirty || fpsValue === null) return;\r\n            fpsDirty = false;\r\n            suppressFpsSyncUntil = Date.now() + 1500;\r\n            fetch('/api/targetfps', { method: 'POST', cache: 'no-store', body: String(fpsValue) }).catch(() => {});\r\n        }\r\n\r\n        function queueFpsSend() {\r\n            fpsDirty = true;\r\n            if (fpsSendTimer) clearTimeout(fpsSendTimer);\r\n            fpsSendTimer = setTimeout(flushFpsSend, FPS_SEND_DEBOUNCE_MS);\r\n        }\r\n\r\n        function stepFps(dir, step) {\r\n            if (fpsValue === null) return;\r\n            const v = fpsValue;\r\n            let nv;\r\n            if (step <= 1) nv = v + dir;\r\n            else if (dir > 0) nv = (Math.floor(v / step) + 1) * step;\r\n            else nv = (Math.ceil(v / step) - 1) * step;\r\n            if (nv < FPS_MIN) nv = FPS_MIN;\r\n            if (nv > FPS_MAX) nv = FPS_MAX;\r\n            nv = Math.round(nv * 2) / 2;\r\n            if (nv === v) return;\r\n            fpsValue = nv;\r\n            showFps();\r\n            suppressFpsSyncUntil = Date.now() + 2500;\r\n            queueFpsSend();\r\n        }\r\n\r\n        function stopHold() {\r\n            if (holdDelayTimer) { clearTimeout(holdDelayTimer); holdDelayTimer = null; }\r\n            if (holdRepeatTimer) { clearInterval(holdRepeatTimer); holdRepeatTimer = null; }\r\n            holdDir = 0;\r\n        }\r\n\r\n        function startHold(dir) {\r\n            stopHold();\r\n            holdDir = dir;\r\n            holdStart = Date.now();\r\n            stepFps(dir, 1);\r\n            holdDelayTimer = setTimeout(() => {\r\n                holdDelayTimer = null;\r\n                holdRepeatTimer = setInterval(() => {\r\n                    const held = Date.now() - holdStart;\r\n                    const step = held >= FPS_STEP10_AFTER_MS ? 10 : (held >= FPS_STEP5_AFTER_MS ? 5 : 1);\r\n                    stepFps(holdDir, step);\r\n                }, FPS_REPEAT_MS);\r\n            }, FPS_HOLD_DELAY_MS);\r\n        }\r\n\r\n        function wireStepperButton(btn, dir) {\r\n            if (!btn) return;\r\n            btn.addEventListener('mousedown', (e) => {\r\n                if (e.button !== 0) return;\r\n                e.stopPropagation();\r\n                startHold(dir);\r\n            });\r\n            btn.addEventListener('mouseleave', stopHold);\r\n        }\r\n        wireStepperButton(fpsDecBtn, -1);\r\n        wireStepperButton(fpsIncBtn, 1);\r\n        window.addEventListener('mouseup', stopHold, true);\r\n        window.addEventListener('blur', stopHold);\r\n\r\n        function applyControls(c) {\r\n            if (!c) return;\r\n\r\n            if (Date.now() >= suppressGeneralCollapsedSyncUntil && typeof c.generalCollapsed === 'boolean') {\r\n                if (isControlsCollapsed !== c.generalCollapsed) {\r\n                    isControlsCollapsed = c.generalCollapsed;\r\n                    updateGeneralControlsVisibility();\r\n                }\r\n            }\r\n\r\n            if (modeBtn && modeList && Array.isArray(c.modeItems)) {\r\n                const mk = JSON.stringify(c.modeItems);\r\n                if (mk !== lastModeKey) {\r\n                    lastModeKey = mk;\r\n                    modeItems = c.modeItems;\r\n                    renderList(modeList, modeItems, pickMode);\r\n                }\r\n                if (Date.now() >= suppressModeSyncUntil && typeof c.mode === 'number') {\r\n                    currentModeIndex = c.mode;\r\n                    if (modeItems[currentModeIndex] !== undefined) modeBtn.textContent = modeItems[currentModeIndex];\r\n                }\r\n                markActive(modeList, currentModeIndex);\r\n\r\n                if (c.modeOrange) {\r\n                    modeBtn.style.backgroundColor = '#FFA500';\r\n                    modeBtn.style.color = '#000000';\r\n                }\r\n            }\r\n\r\n            if (targetBtn && targetList && Array.isArray(c.targetItems)) {\r\n                const tk = String(c.targetList) + '|' + JSON.stringify(c.targetItems);\r\n                if (tk !== lastTargetKey) {\r\n                    lastTargetKey = tk;\r\n                    targetListKind = String(c.targetList);\r\n                    targetItems = c.targetItems;\r\n                    renderList(targetList, targetItems, pickTarget);\r\n                    suppressTargetSyncUntil = 0;\r\n                }\r\n                targetSwapIndex = (typeof c.targetSwapIndex === 'number') ? c.targetSwapIndex : -1;\r\n                if (Date.now() >= suppressTargetSyncUntil && typeof c.targetIndex === 'number') {\r\n                    currentTargetIndex = c.targetIndex;\r\n                    if (targetItems[currentTargetIndex] !== undefined) targetBtn.textContent = targetItems[currentTargetIndex];\r\n                }\r\n                markActive(targetList, currentTargetIndex);\r\n\r\n                if (c.targetOrange) {\r\n                    targetBtn.style.backgroundColor = '#FFA500';\r\n                    targetBtn.style.color = '#000000';\r\n                }\r\n            }\r\n\r\n            const editable = !!c.fpsEditable;\r\n            const btnVis = editable ? 'visible' : 'hidden';\r\n            if (fpsDecBtn) fpsDecBtn.style.visibility = btnVis;\r\n            if (fpsIncBtn) fpsIncBtn.style.visibility = btnVis;\r\n\r\n            if (!editable) {\r\n                stopHold();\r\n                fpsDirty = false;\r\n                if (fpsSendTimer) { clearTimeout(fpsSendTimer); fpsSendTimer = null; }\r\n                fpsValue = null;\r\n                if (fpsValEl) fpsValEl.textContent = (c.fpsText === undefined || c.fpsText === null || c.fpsText === '') ? '-' : String(c.fpsText);\r\n            } else if (holdDir === 0 && (fpsValue === null || Date.now() >= suppressFpsSyncUntil) && c.fps !== undefined && c.fps !== null && c.fps !== '') {\r\n                const f = parseFloat(String(c.fps).replace(',', '.'));\r\n                if (isFinite(f)) {\r\n                    fpsValue = f;\r\n                    showFps();\r\n                }\r\n            }\r\n\r\n            if (c.fpsOrange && fpsValEl) fpsValEl.style.color = '#FFA500';\r\n\r\n            if (expertChk && typeof c.expert === 'boolean') expertChk.checked = c.expert;\r\n            if (logPlusChk && typeof c.logPlus === 'boolean') logPlusChk.checked = c.logPlus;\r\n            if (logPlusLabel && typeof c.logPlusVisible === 'boolean') logPlusLabel.style.display = c.logPlusVisible ? 'inline-flex' : 'none';\r\n\r\n            if (Date.now() >= suppressAutoHideSyncUntil && typeof c.autoHide === 'number') {\r\n                currentAutoHideIndex = c.autoHide;\r\n                if (autoHideBtn && autoHideItems[currentAutoHideIndex] !== undefined) {\r\n                    autoHideBtn.textContent = autoHideItems[currentAutoHideIndex];\r\n                }\r\n                markActive(autoHideList, currentAutoHideIndex);\r\n            }\r\n        }\r\n\r\n        if (resetBtn) {\r\n            resetBtn.addEventListener('click', () => {\r\n                resetBtn.disabled = true;\r\n                fetch('/api/reset', { method: 'POST', cache: 'no-store' })\r\n                    .catch(() => {})\r\n                    .then(() => setTimeout(() => { resetBtn.disabled = false; }, 1000));\r\n            });\r\n        }\r\n\r\n        if (darkModeChk) {\r\n            darkModeChk.addEventListener('click', (e) => {\r\n                e.stopPropagation();\r\n                fetch('/api/toggledarkmode', { method: 'POST', cache: 'no-store' }).catch(() => {});\r\n            });\r\n        }\r\n\r\n        if (expertChk) {\r\n            expertChk.addEventListener('click', (e) => {\r\n                e.stopPropagation();\r\n                fetch('/api/toggleexpert', { method: 'POST', cache: 'no-store' }).catch(() => {});\r\n            });\r\n        }\r\n\r\n        if (logPlusChk) {\r\n            logPlusChk.addEventListener('click', (e) => {\r\n                e.stopPropagation();\r\n                fetch('/api/togglelogplus', { method: 'POST', cache: 'no-store' }).catch(() => {});\r\n            });\r\n        }\r\n\r\n        async function updateStatus() {\r\n            try {\r\n                const res = await fetch('/api/status', { cache: 'no-store' });\r\n                if (!res.ok) throw new Error();\r\n                const data = await res.json();\r\n\r\n                if (data.theme) {\r\n                    const theme = data.theme;\r\n                    const inputBg = theme.inputBg || theme.bg;\r\n                    const btnBg = theme.btnBg || inputBg;\r\n\r\n                    document.documentElement.style.backgroundColor = theme.bg;\r\n                    document.body.style.backgroundColor = theme.bg;\r\n                    document.body.style.color = theme.text;\r\n                    document.querySelectorAll('.group-box').forEach(el => el.style.borderColor = theme.border);\r\n                    document.querySelectorAll('.group-header').forEach(el => el.style.color = theme.text);\r\n                    document.querySelectorAll('.controls-row .label, .chk-label').forEach(el => el.style.color = theme.text);\r\n\r\n                    if (flightTypeBtn) {\r\n                        flightTypeBtn.style.backgroundColor = inputBg;\r\n                        flightTypeBtn.style.color = theme.text;\r\n                        flightTypeBtn.style.borderColor = theme.border;\r\n                    }\r\n                    if (flightTypeList) {\r\n                        flightTypeList.style.backgroundColor = inputBg;\r\n                        flightTypeList.style.color = theme.text;\r\n                        flightTypeList.style.borderColor = theme.border;\r\n                    }\r\n                    if (resetBtn) {\r\n                        resetBtn.style.backgroundColor = btnBg;\r\n                        resetBtn.style.color = theme.text;\r\n                        resetBtn.style.borderColor = theme.border;\r\n                    }\r\n                    [modeBtn, targetBtn, autoHideBtn].forEach(el => {\r\n                        if (!el) return;\r\n                        el.style.backgroundColor = inputBg;\r\n                        el.style.color = theme.text;\r\n                        el.style.borderColor = theme.border;\r\n                    });\r\n                    [modeList, targetList, autoHideList].forEach(el => {\r\n                        if (!el) return;\r\n                        el.style.backgroundColor = inputBg;\r\n                        el.style.color = theme.text;\r\n                        el.style.borderColor = theme.border;\r\n                    });\r\n                    [fpsDecBtn, fpsIncBtn].forEach(el => {\r\n                        if (!el) return;\r\n                        el.style.backgroundColor = btnBg;\r\n                        el.style.color = theme.text;\r\n                        el.style.borderColor = theme.border;\r\n                    });\r\n                    if (fpsValEl) fpsValEl.style.color = theme.text;\r\n                    if (titleEl) titleEl.style.color = theme.text;\r\n\r\n                    customCheckboxes.forEach(cb => {\r\n                        cb.style.backgroundColor = inputBg;\r\n                        cb.style.borderColor = theme.border;\r\n                        cb.style.color = theme.text;\r\n                    });\r\n\r\n                    if (darkModeChk && typeof theme.isDark === 'boolean') {\r\n                        darkModeChk.checked = theme.isDark;\r\n                    }\r\n                }\r\n\r\n                if (titleEl && data.widgetTitle) titleEl.textContent = data.widgetTitle;\r\n\r\n                for (const [id, item] of Object.entries(data)) {\r\n                    if (id === 'theme' || id === 'widgetTitle' || id === 'widgetHotkey' || id === 'autoFpsControls' || id === 'fpsIcon' || id === 'simHeaderFS' || id === 'simHeaderDog') continue;\r\n                    const el = document.getElementById(id);\r\n                    if (el && item) {\r\n                        setTextWithIcons(el, item.text ?? '');\r\n                        el.style.color = item.color ?? (data.theme ? data.theme.text : '#FFFFFF');\r\n\r\n                        if (id === 'statusMessage') {\r\n                            const txt = (item.text === undefined || item.text === null) ? '' : String(item.text);\r\n                            const firstLine = txt.split('\\n')[0] || '';\r\n                            if (firstLine.length > 60) el.classList.add('status-compact');\r\n                            else el.classList.remove('status-compact');\r\n                        }\r\n                    }\r\n                }\r\n\r\n                const fpsIconElem = document.getElementById('imgFpsSource');\r\n                if (fpsIconElem) {\r\n                    const fpsIconSvg = {\r\n                        1: '<svg viewBox=\\'0 0 16 16\\'><text x=\\'1\\' y=\\'13\\' font-family=\\'sans-serif\\' font-weight=\\'900\\' font-style=\\'italic\\' font-size=\\'13\\' fill=\\'#38BDF8\\'>F</text><text x=\\'8\\' y=\\'13\\' font-family=\\'sans-serif\\' font-weight=\\'900\\' font-style=\\'italic\\' font-size=\\'13\\' fill=\\'#94A3B8\\'>S</text></svg>',\r\n                        2: '<svg viewBox=\\'0 0 16 16\\'><rect x=\\'1\\' y=\\'1\\' width=\\'14\\' height=\\'10\\' rx=\\'1\\' fill=\\'#3B82F6\\' stroke=\\'#CBD5E1\\' stroke-width=\\'0.8\\'/><rect x=\\'2\\' y=\\'2\\' width=\\'6\\' height=\\'5\\' fill=\\'#000\\'/><text x=\\'2.5\\' y=\\'6\\' font-family=\\'monospace\\' font-weight=\\'bold\\' font-size=\\'4.5\\' fill=\\'#E879F9\\'>60</text><path d=\\'M6 11v2h4v-2\\' stroke=\\'#94A3B8\\' stroke-width=\\'1\\' fill=\\'none\\'/><line x1=\\'5\\' y1=\\'13\\' x2=\\'11\\' y2=\\'13\\' stroke=\\'#94A3B8\\' stroke-width=\\'1.2\\'/></svg>',\r\n                        3: '<svg viewBox=\\'0 0 16 16\\'><circle cx=\\'8\\' cy=\\'8\\' r=\\'7.5\\' fill=\\'#000\\' stroke=\\'#22C55E\\' stroke-width=\\'1.2\\'/><path d=\\'M3.5 8h9c.5 0 .8.4.8.8v2.4c0 .5-.4.8-.8.8h-3c-.5 0-.8-.3-.9-.6L8 9.5l-.6 1.9c-.1.3-.4.6-.9.6h-3c-.5 0-.8-.3-.8-.8V8.8c0-.4.3-.8.8-.8z\\' fill=\\'none\\' stroke=\\'#FFF\\' stroke-width=\\'0.9\\'/><circle cx=\\'5.5\\' cy=\\'9.8\\' r=\\'1.2\\' fill=\\'#FFF\\'/><circle cx=\\'10.5\\' cy=\\'9.8\\' r=\\'1.2\\' fill=\\'#FFF\\'/></svg>',\r\n                        4: '<svg viewBox=\\'0 0 16 16\\'><rect width=\\'16\\' height=\\'16\\' rx=\\'3\\' fill=\\'#1A382B\\'/><text x=\\'1.5\\' y=\\'12\\' font-family=\\'sans-serif\\' font-weight=\\'900\\' font-size=\\'10\\' fill=\\'#F5EBE1\\'>XR</text><polygon points=\\'7,11 7,6 10,6 13,3 14,4 11,7 11,10 9,10 9,11\\' fill=\\'#F97316\\'/></svg>'\r\n                    };\r\n                    const svg = fpsIconSvg[data.fpsIcon];\r\n                    const isClickable = (data.fpsIcon === 1 || data.fpsIcon === 2);\r\n\r\n                    if (svg) {\r\n                        fpsIconElem.innerHTML = svg;\r\n                        fpsIconElem.style.display = 'inline-flex';\r\n                        fpsIconElem.style.cursor = isClickable ? 'pointer' : 'default';\r\n                        fpsIconElem.title = isClickable ? 'Click to toggle FPS source' : '';\r\n                        fpsIconElem.dataset.clickable = isClickable ? 'true' : 'false';\r\n                    } else {\r\n                        fpsIconElem.style.display = 'none';\r\n                    }\r\n\r\n                    if (!fpsIconElem.hasClickWire) {\r\n                        fpsIconElem.hasClickWire = true;\r\n                        fpsIconElem.addEventListener('click', (e) => {\r\n                            if (fpsIconElem.dataset.clickable !== 'true') return;\r\n                            e.stopPropagation();\r\n                            fetch('/api/togglefpssource', { method: 'POST', cache: 'no-store' }).catch(() => {});\r\n                        });\r\n                    }\r\n                }\r\n\r\n                const elHdrFS = document.getElementById('hdrSimFS');\r\n                if (elHdrFS) elHdrFS.style.display = data.simHeaderFS ? 'inline-flex' : 'none';\r\n\r\n                const elHdrDog = document.getElementById('hdrSimDog');\r\n                if (elHdrDog) elHdrDog.style.display = data.simHeaderDog ? 'inline-flex' : 'none';\r\n\r\n                if (flightTypeBtn && flightTypeList && Array.isArray(data.profiles)) {\r\n                    const profilesKey = JSON.stringify(data.profiles);\r\n                    if (profilesKey !== lastProfilesKey) {\r\n                        lastProfilesKey = profilesKey;\r\n                        renderFlightTypeList(data.profiles);\r\n                    }\r\n                    if (Date.now() >= suppressSyncUntil && typeof data.activeProfileIndex === 'number') {\r\n                        if (currentActiveIndex !== data.activeProfileIndex) {\r\n                            currentActiveIndex = data.activeProfileIndex;\r\n                            if (currentProfiles[currentActiveIndex] !== undefined) {\r\n                                flightTypeBtn.textContent = displayProfileName(currentProfiles[currentActiveIndex]);\r\n                            }\r\n                        }\r\n                    }\r\n                    markActiveItem(currentActiveIndex);\r\n                }\r\n\r\n                try { applyControls(data.autoFpsControls); } catch (e) {}\r\n            } catch (err) {\r\n                ['connMSFS', 'connSimConnect', 'connSession'].forEach(id => {\r\n                    const el = document.getElementById(id);\r\n                    if (el) el.style.color = '#EF4444';\r\n                });\r\n                ['valFps', 'valTlod', 'valOlod', 'valAgl', 'valFpm', 'valClouds'].forEach(id => {\r\n                    const el = document.getElementById(id);\r\n                    if (el) el.innerText = 'n/a';\r\n                });\r\n                const elFpsIcon = document.getElementById('imgFpsSource');\r\n                if (elFpsIcon) elFpsIcon.style.display = 'none';\r\n                const elHdrFS = document.getElementById('hdrSimFS');\r\n                if (elHdrFS) elHdrFS.style.display = 'none';\r\n                const elHdrDog = document.getElementById('hdrSimDog');\r\n                if (elHdrDog) elHdrDog.style.display = 'none';\r\n                const elStatus = document.getElementById('statusMessage');\r\n                if (elStatus) elStatus.innerText = 'Disconnected';\r\n            }\r\n        }\r\n        setInterval(updateStatus, 500);\r\n        updateStatus();\r\n    </script>\r\n</body>\r\n</html>";
	}
}

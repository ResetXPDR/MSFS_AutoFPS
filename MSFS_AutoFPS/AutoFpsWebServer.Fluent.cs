using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MSFS_AutoFPS;

// Fork: additions to the widget web server.
//  * Serves the shared Fluent widget UI (the same HTML/CSS/JS as the in-game MSFS panel) from embedded "web/..." resources.
//  * GET  /api/page/{tuning|settings}  -> JSON description of that app page (cards, rows, controls, values).
//  * POST /api/control {"name":..,"value":..} -> applies a value through the real WPF control and its handlers.
public partial class AutoFpsWebServer
{
	/// <summary>Returns the JSON description of an app page ("tuning" or "settings"), or null if unknown.</summary>
	public Func<string, string> GetPageJson { get; set; }

	/// <summary>Applies a control change from the widget; receives the raw JSON body and returns a JSON result.</summary>
	public Func<string, string> SetControl { get; set; }

	private static readonly Dictionary<string, (byte[] Data, string ContentType)> StaticFiles = LoadStaticFiles();

	private static Dictionary<string, (byte[] Data, string ContentType)> LoadStaticFiles()
	{
		var files = new Dictionary<string, (byte[] Data, string ContentType)>(StringComparer.OrdinalIgnoreCase);
		try
		{
			Assembly asm = typeof(AutoFpsWebServer).Assembly;
			foreach (string name in asm.GetManifestResourceNames())
			{
				string key = name.Replace('\\', '/');
				if (!key.StartsWith("web/", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				using Stream stream = asm.GetManifestResourceStream(name);
				if (stream == null)
				{
					continue;
				}
				using var ms = new MemoryStream();
				stream.CopyTo(ms);
				files["/" + key.Substring(4).ToLowerInvariant()] = (ms.ToArray(), ContentTypeFor(key));
			}
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "WebServer", "Failed to load embedded widget files: " + ex.Message);
		}
		return files;
	}

	private static string ContentTypeFor(string path)
	{
		switch (Path.GetExtension(path).ToLowerInvariant())
		{
			case ".html": return "text/html; charset=utf-8";
			case ".css": return "text/css; charset=utf-8";
			case ".js": return "application/javascript; charset=utf-8";
			case ".png": return "image/png";
			case ".jpg": return "image/jpeg";
			case ".svg": return "image/svg+xml";
			case ".json": return "application/json";
			default: return "application/octet-stream";
		}
	}

	private const int MaxRequestBytes = 64 * 1024;

	/// <summary>Reads one HTTP request: until the header terminator, then until Content-Length body bytes
	/// have arrived (answering "Expect: 100-continue"). Returns null if the client sent nothing.</summary>
	private static async System.Threading.Tasks.Task<string> ReadRequestAsync(System.Net.Sockets.NetworkStream stream)
	{
		var data = new List<byte>(1024);
		var buffer = new byte[4096];
		int headerEnd = -1;
		int contentLength = 0;
		bool continueSent = false;
		while (data.Count < MaxRequestBytes)
		{
			if (headerEnd >= 0 && data.Count - (headerEnd + 4) >= contentLength)
			{
				break;
			}
			int n = await stream.ReadAsync(buffer, 0, buffer.Length);
			if (n <= 0)
			{
				break;
			}
			data.AddRange(new ArraySegment<byte>(buffer, 0, n));
			if (headerEnd < 0)
			{
				headerEnd = IndexOfHeaderEnd(data);
				if (headerEnd >= 0)
				{
					string headers = Encoding.ASCII.GetString(data.GetRange(0, headerEnd).ToArray());
					foreach (string line in headers.Split(new[] { "\r\n" }, StringSplitOptions.None))
					{
						int colon = line.IndexOf(':');
						if (colon <= 0)
						{
							continue;
						}
						string name = line.Substring(0, colon).Trim();
						string val = line.Substring(colon + 1).Trim();
						if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int len))
						{
							contentLength = Math.Max(0, Math.Min(len, MaxRequestBytes));
						}
						else if (name.Equals("Expect", StringComparison.OrdinalIgnoreCase) && val.Equals("100-continue", StringComparison.OrdinalIgnoreCase) && !continueSent)
						{
							continueSent = true;
							byte[] cont = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
							await stream.WriteAsync(cont, 0, cont.Length);
						}
					}
				}
			}
		}
		return data.Count == 0 ? null : Encoding.UTF8.GetString(data.ToArray());
	}

	private static int IndexOfHeaderEnd(List<byte> data)
	{
		for (int i = 0; i + 3 < data.Count; i++)
		{
			if (data[i] == '\r' && data[i + 1] == '\n' && data[i + 2] == '\r' && data[i + 3] == '\n')
			{
				return i;
			}
		}
		return -1;
	}

	/// <param name="method">Upper-case HTTP method.</param>
	/// <param name="path">Lower-case path without query string.</param>
	private bool TryHandleFluentRequest(string method, string path, string body, out byte[] bodyBytes, out string contentType, out int status, out string statusText)
	{
		bodyBytes = null;
		contentType = null;
		status = 200;
		statusText = "OK";

		if (method == "GET" && path.StartsWith("/api/page/", StringComparison.Ordinal))
		{
			string json = null;
			try
			{
				json = GetPageJson?.Invoke(path.Substring("/api/page/".Length));
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error, "WebServer", "Page JSON failed: " + ex.Message);
			}
			if (json == null)
			{
				status = 404;
				statusText = "Not Found";
				json = "{\"ok\":false,\"error\":\"unknown page\"}";
			}
			bodyBytes = Encoding.UTF8.GetBytes(json);
			contentType = "application/json";
			return true;
		}

		if (method == "POST" && path == "/api/control")
		{
			string json;
			try
			{
				json = SetControl?.Invoke(body ?? "") ?? "{\"ok\":false}";
			}
			catch (Exception ex)
			{
				json = "{\"ok\":false,\"error\":\"" + ex.GetType().Name + "\"}";
			}
			bodyBytes = Encoding.UTF8.GetBytes(json);
			contentType = "application/json";
			return true;
		}

		if (method == "GET")
		{
			string key = (path == "/" ? "/index.html" : path);
			if (StaticFiles.TryGetValue(key, out var file))
			{
				bodyBytes = file.Data;
				contentType = file.ContentType;
				return true;
			}
		}
		return false;
	}
}

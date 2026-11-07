using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace MSFS_AutoFPS;

public static class IPCManager
{
	public static readonly int waitDuration = 10000;

	private static bool _simVarsSubscribed = false;

	private static float _camStateLast = 0f;

	private static float _camStatePending = 0f;

	private static float _camStateLastRaw = 0f;

	public static MobiSimConnect SimConnect { get; set; } = null;

	public static bool WaitForSimulator(ServiceModel model)
	{
		bool flag = IsSimRunning();
		bool flag2 = false;
		int num = 5;
		while (!model.InitialCheckForUpdatesComplete && num-- > 0)
		{
			Thread.Sleep(1000);
		}
		if (model.IsSimRunning && !flag)
		{
			Logger.Log(LogLevel.Warning, "IPCManager:WaitForSimulator", "Simulator exited unexpectedly – aborting");
			return false;
		}
		if (model.AutoUpdateInProgress || (!flag && model.WaitForConnect))
		{
			do
			{
				if (!flag2 && !model.AutoUpdateInProgress)
				{
					Logger.Log(LogLevel.Information, "IPCManager:WaitForSimulator", $"Simulator not started - waiting {waitDuration / 1000}s between Retries");
					flag2 = true;
				}
				Thread.Sleep(waitDuration);
			}
			while (model.AutoUpdateInProgress || (!IsSimRunning() && !model.CancellationRequested && !model.IsSimRunning));
			Thread.Sleep(waitDuration);
			return true;
		}
		if (flag)
		{
			if (!model.IsSimRunning)
			{
				Logger.Log(LogLevel.Information, "IPCManager:WaitForSimulator", "Simulator started");
			}
			return true;
		}
		Logger.Log(LogLevel.Error, "IPCManager:WaitForSimulator", "Simulator not started - aborting");
		return false;
	}

	public static bool IsProcessRunning(string name, bool startsWith = false)
	{
		name = Normalize(name);
		if (!startsWith)
		{
			Process process = Process.GetProcessesByName(name).FirstOrDefault();
			if (process != null)
			{
				return Normalize(process.ProcessName) == name;
			}
			return false;
		}
		return Process.GetProcesses().Any((Process p) => Normalize(p.ProcessName).StartsWith(name, StringComparison.OrdinalIgnoreCase));
		static string Normalize(string s)
		{
			return s.Replace("‑", "-").Replace("–", "-").Replace("—", "-")
				.Replace("−", "-")
				.Replace("﹘", "-");
		}
	}

	public static bool IsSim2024()
	{
		return IsProcessRunning("FlightSimulator2024");
	}

	public static bool IsSimRunning()
	{
		if (!IsProcessRunning("FlightSimulator"))
		{
			return IsProcessRunning("FlightSimulator2024");
		}
		return true;
	}

	public static bool WaitForConnection(ServiceModel model)
	{
		if (!IsSimRunning())
		{
			return false;
		}
		if (SimConnect != null && SimConnect.IsConnected)
		{
			return true;
		}
		_simVarsSubscribed = false;
		SimConnect = new MobiSimConnect(model);
		bool flag = SimConnect.Connect();
		bool flag2 = false;
		if (!SimConnect.IsConnected)
		{
			do
			{
				if (!flag2)
				{
					Logger.Log(LogLevel.Information, "IPCManager:WaitForConnection", $"Connection not established - waiting {waitDuration / 2 / 1000}s between Retries");
					flag2 = true;
				}
				Thread.Sleep(waitDuration / 2);
				if (!flag)
				{
					flag = SimConnect.Connect();
				}
			}
			while (!SimConnect.IsConnected && IsSimRunning() && !model.CancellationRequested);
			return SimConnect.IsConnected;
		}
		Logger.Log(LogLevel.Information, "IPCManager:WaitForConnection", "SimConnect is opened");
		return true;
	}

	public static bool WaitForSessionReady(ServiceModel model)
	{
		int millisecondsTimeout = 1000;
		if (!_simVarsSubscribed)
		{
			SimConnect.SubscribeSimVar("CAMERA STATE", "Enum");
			SimConnect.SubscribeSimVar("PLANE IN PARKING STATE", "Bool");
			SimConnect.SubscribeSimVar("ANIMATION DELTA TIME", "Seconds");
			SimConnect.SubscribeSimVar("VERTICAL SPEED", "feet per second");
			SimConnect.SubscribeSimVar("PLANE ALT ABOVE GROUND", "feet");
			SimConnect.SubscribeSimVar("PLANE ALT ABOVE GROUND MINUS CG", "feet");
			SimConnect.SubscribeSimVar("SIM ON GROUND", "Bool");
			SimConnect.SubscribeSimVar("GROUND VELOCITY", "knots");
			SimConnect.SubscribeSimVar("PLANE ALTITUDE", "feet");
			SimConnect.SubscribeSimVar("GLASSCOCKPIT AUTOMATIC BRIGHTNESS", "percent");
			SimConnect.SubscribeSimVar("GEAR POSITION", "percent");
			SimConnect.SubscribeSimVar("PLANE LATITUDE", "degrees");
			SimConnect.SubscribeSimVar("PLANE LONGITUDE", "degrees");
			_simVarsSubscribed = true;
		}
		RefreshCamState(model);
		bool flag = IsSimActive(model);
		bool flag2 = false;
		while (IsSimRunning() && !flag && !model.CancellationRequested)
		{
			if (!flag2)
			{
				Logger.Log(LogLevel.Information, "IPCManager:WaitForSessionReady", "Waiting for flight session connection ...");
				flag2 = true;
			}
			Thread.Sleep(millisecondsTimeout);
			RefreshCamState(model);
			flag = IsSimActive(model);
		}
		if (!flag)
		{
			Logger.Log(LogLevel.Information, "IPCManager:WaitForSessionReady", "SimConnect or Simulator not available - aborting");
			return false;
		}
		Logger.Log(LogLevel.Information, "IPCManager:WaitForSessionReady", "Flight session connection established");
		return true;
	}

	private static void RefreshCamState(ServiceModel model)
	{
		float num = SimConnect.ReadSimVar("CAMERA STATE", "Enum");
		float camStateLast = _camStateLast;
		float camStatePending = _camStatePending;
		if (num != 0f && num != _camStateLast)
		{
			if (num == camStatePending)
			{
				_camStateLast = num;
				if (model.LogPlus)
				{
					Logger.Log(LogLevel.Information, "IPCManager:RefreshCamState", $"Cam state {num} (confirmed)");
				}
			}
			else if (model.LogPlus && camStatePending != camStateLast)
			{
				Logger.Log(LogLevel.Information, "IPCManager:RefreshCamState", $"Cam state {camStatePending} (transient)");
			}
			_camStatePending = num;
		}
		else if (num == _camStateLast)
		{
			if (model.LogPlus && camStatePending != camStateLast)
			{
				Logger.Log(LogLevel.Information, "IPCManager:RefreshCamState", $"Cam state {camStatePending} (transient)");
			}
			_camStatePending = num;
		}
		else if (model.LogPlus && num != camStateLast)
		{
			Logger.Log(LogLevel.Information, "IPCManager:RefreshCamState", "Cam state 0 (ignored)");
		}
	}

	public static bool IsCamReady(ServiceModel model)
	{
		RefreshCamState(model);
		bool flag = false;
		float camStateLast = _camStateLast;
		if ((camStateLast >= 2f && camStateLast <= 10f) || (IsSim2024() && (camStateLast == 16f || camStateLast == 27f || camStateLast == 29f || camStateLast == 30f || camStateLast == 31f || camStateLast == 33f || camStateLast == 34f || camStateLast == 35f)))
		{
			return !flag;
		}
		return false;
	}

	public static bool IsSimActive(ServiceModel model)
	{
		float camStateLast = _camStateLast;
		if ((camStateLast >= 2f && camStateLast <= 9f) || (IsSim2024() && (camStateLast == 30f || camStateLast == 31f)))
		{
			return SimConnect.ReadSimVar("ANIMATION DELTA TIME", "Seconds") > 0f;
		}
		return false;
	}

	public static void CloseSafe()
	{
		try
		{
			if (SimConnect != null)
			{
				SimConnect.Disconnect();
				SimConnect = null;
			}
		}
		catch
		{
		}
	}
}

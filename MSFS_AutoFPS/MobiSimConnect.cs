using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.FlightSimulator.SimConnect;

namespace MSFS_AutoFPS;

public class MobiSimConnect : IDisposable
{
	public const string MOBIFLIGHT_CLIENT_DATA_NAME_COMMAND = "MobiFlight.Command";

	public const string MOBIFLIGHT_CLIENT_DATA_NAME_RESPONSE = "MobiFlight.Response";

	public const uint MOBIFLIGHT_MESSAGE_SIZE = 1024u;

	public const uint WM_PILOTSDECK_SIMCONNECT = 6537u;

	public const string CLIENT_NAME = "MSFS_AutoFPS";

	public const string PILOTSDECK_CLIENT_DATA_NAME_SIMVAR = "MSFS_AutoFPS.LVars";

	public const string PILOTSDECK_CLIENT_DATA_NAME_COMMAND = "MSFS_AutoFPS.Command";

	public const string PILOTSDECK_CLIENT_DATA_NAME_RESPONSE = "MSFS_AutoFPS.Response";

	protected SimConnect simConnect;

	protected nint simConnectHandle = IntPtr.Zero;

	protected Thread simConnectThread;

	private static bool cancelThread;

	protected bool isSimConnected;

	protected bool isMobiConnected;

	protected bool isReceiveRunning;

	private const int fpsLen = 5;

	private float[] fpsStatistic;

	private int fpsIndex;

	private float simRate = 1f;

	private int exception31Count;

	private readonly Dictionary<uint, string> requestMap = new Dictionary<uint, string>();

	private DateTime lastSubError;

	private bool subErrorActive;

	private DateTime lastRecvError;

	private bool recvErrorActive;

	protected uint nextID = 1u;

	protected const int reorderTreshold = 150;

	protected Dictionary<string, uint> addressToIndex = new Dictionary<string, uint>();

	protected Dictionary<uint, float> simVars = new Dictionary<uint, float>();

	private ServiceModel model;

	public bool IsConnected
	{
		get
		{
			if (isSimConnected)
			{
				return isMobiConnected;
			}
			return false;
		}
	}

	public bool IsReady
	{
		get
		{
			if (IsConnected)
			{
				return isReceiveRunning;
			}
			return false;
		}
	}

	public bool SimIsPaused { get; private set; }

	public bool SimIsRunning { get; private set; }

	public MobiSimConnect(ServiceModel Model)
	{
		model = Model;
		fpsIndex = -1;
		fpsStatistic = new float[5];
	}

	public float GetSimRate()
	{
		return simRate;
	}

	public float GetAverageFPS()
	{
		if (fpsIndex == -1)
		{
			return 0f;
		}
		return fpsStatistic.Sum() / 5f;
	}

	public bool Connect()
	{
		try
		{
			if (isSimConnected)
			{
				return true;
			}
			if (!IPCManager.IsSimRunning())
			{
				Logger.Log(LogLevel.Warning, "MobiSimConnect:Connect", "Simulator not running, skipping SimConnect connection attempt");
				return false;
			}
			simConnect = new SimConnect("MSFS_AutoFPS", IntPtr.Zero, 6537u, null, 0u);
			simConnect.OnRecvOpen += SimConnect_OnOpen;
			simConnect.OnRecvQuit += SimConnect_OnQuit;
			simConnect.OnRecvException += SimConnect_OnException;
			cancelThread = false;
			simConnectThread = new Thread(SimConnect_ReceiveThread)
			{
				IsBackground = true
			};
			simConnectThread.Start();
			Logger.Log(LogLevel.Information, "MobiSimConnect:Connect", "SimConnect Connection open");
			return true;
		}
		catch (COMException ex)
		{
			simConnectThread = null;
			cancelThread = true;
			simConnect = null;
			simConnectHandle = IntPtr.Zero;
			uint errorCode = (uint)ex.ErrorCode;
			if (errorCode != 2147500037u)
			{
				Logger.Log(LogLevel.Error, "MobiSimConnect:Connect", $"COMException while opening SimConnect! HRESULT=0x{errorCode:X}, {ex.Message}");
			}
		}
		catch (Exception ex2)
		{
			simConnectThread = null;
			cancelThread = true;
			simConnect = null;
			simConnectHandle = IntPtr.Zero;
			Logger.Log(LogLevel.Error, "MobiSimConnect:Connect", $"Exception while opening SimConnect! (Exception: {ex2.GetType()} {ex2.Message})");
		}
		return false;
	}

	protected void SimConnect_OnOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
	{
		try
		{
			isSimConnected = true;
			simConnect.OnRecvClientData += SimConnect_OnClientData;
			simConnect.OnRecvEvent += SimConnect_OnReceiveEvent;
			simConnect.OnRecvEventFrame += Simconnect_OnRecvEventFrame;
			CreateDataAreaDefaultChannel();
			SendMobiWasmCmd("MF.Ping");
			Logger.Log(LogLevel.Information, "MobiSimConnect:SimConnect_OnOpen", "SimConnect OnOpen received");
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MobiSimConnect:SimConnect_OnOpen", $"Exception during SimConnect OnOpen! (Exception: {ex.GetType()} {ex.Message})");
		}
	}

	protected void SimConnect_ReceiveThread()
	{
		ulong num = 0uL;
		int num2 = 100;
		int num3 = 5000 / num2;
		isReceiveRunning = true;
		while (!cancelThread && simConnect != null && isReceiveRunning)
		{
			try
			{
				simConnect.ReceiveMessage();
				recvErrorActive = false;
				if (isSimConnected && !isMobiConnected && num % (ulong)num3 == 0L)
				{
					Logger.Log(LogLevel.Debug, "MobiSimConnect:SimConnect_ReceiveThread", "Sending Ping to MobiFlight WASM Module");
					SendMobiWasmCmd("MF.DummyCmd");
					SendMobiWasmCmd("MF.Ping");
				}
			}
			catch (Exception)
			{
				if (!model.IsSessionRunning)
				{
					continue;
				}
				DateTime utcNow = DateTime.UtcNow;
				if (!recvErrorActive)
				{
					recvErrorActive = true;
					lastRecvError = utcNow;
				}
				else if (!((utcNow - lastRecvError).TotalSeconds < 5.0))
				{
					if (!IPCManager.IsSimRunning())
					{
						Logger.Log(LogLevel.Information, "MobiSimConnect:SimConnect_ReceiveThread", "SimConnect receive thread stopping because simulator is no longer running");
					}
					else
					{
						Logger.Log(LogLevel.Warning, "MobiSimConnect:SimConnect_ReceiveThread", "SimConnect stopped responding while simulator is still running");
					}
					isSimConnected = false;
					isReceiveRunning = false;
					return;
				}
				continue;
			}
			Thread.Sleep(num2);
			num++;
		}
		isReceiveRunning = false;
	}

	protected void CreateEventSubscription()
	{
		simConnect.SubscribeToSystemEvent(SIM_EVENTS.SIM, "sim");
		RegisterRequest(0u, "SubscribeToSystemEvent(SIM)");
		simConnect.SubscribeToSystemEvent(SIM_EVENTS.PAUSE, "pause");
		RegisterRequest(1u, "SubscribeToSystemEvent(PAUSE)");
		simConnect.SubscribeToSystemEvent(SIM_EVENTS.FRAME, "frame");
		RegisterRequest(2u, "SubscribeToSystemEvent(FRAME)");
	}

	protected void CreateDataAreaDefaultChannel()
	{
		simConnect.MapClientDataNameToID("MobiFlight.Command", MOBIFLIGHT_CLIENT_DATA_ID.MOBIFLIGHT_CMD);
		simConnect.MapClientDataNameToID("MobiFlight.Response", MOBIFLIGHT_CLIENT_DATA_ID.MOBIFLIGHT_RESPONSE);
		simConnect.AddToClientDataDefinition(SIMCONNECT_DEFINE_ID.Dummy, 0u, 1024u, 0f, 0u);
		simConnect.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, ResponseString>(SIMCONNECT_DEFINE_ID.Dummy);
		simConnect.RequestClientData(MOBIFLIGHT_CLIENT_DATA_ID.MOBIFLIGHT_RESPONSE, SIMCONNECT_REQUEST_ID.Dummy, SIMCONNECT_DEFINE_ID.Dummy, SIMCONNECT_CLIENT_DATA_PERIOD.ON_SET, SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.CHANGED, 0u, 0u, 0u);
		RegisterRequest(0u, "RequestClientData(DefaultChannel)");
	}

	protected void CreateDataAreaClientChannel()
	{
		simConnect.MapClientDataNameToID("MSFS_AutoFPS.Command", PILOTSDECK_CLIENT_DATA_ID.MOBIFLIGHT_CMD);
		simConnect.MapClientDataNameToID("MSFS_AutoFPS.Response", PILOTSDECK_CLIENT_DATA_ID.MOBIFLIGHT_RESPONSE);
		simConnect.MapClientDataNameToID("MSFS_AutoFPS.LVars", PILOTSDECK_CLIENT_DATA_ID.MOBIFLIGHT_LVARS);
		simConnect.AddToClientDataDefinition(SIMCONNECT_DEFINE_ID.Dummy, 0u, 1024u, 0f, 0u);
		simConnect.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, ResponseString>(SIMCONNECT_DEFINE_ID.Dummy);
		simConnect.RequestClientData(PILOTSDECK_CLIENT_DATA_ID.MOBIFLIGHT_RESPONSE, SIMCONNECT_REQUEST_ID.Dummy, SIMCONNECT_DEFINE_ID.Dummy, SIMCONNECT_CLIENT_DATA_PERIOD.ON_SET, SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.CHANGED, 0u, 0u, 0u);
		RegisterRequest(0u, "RequestClientData(ClientChannel)");
	}

	protected void SimConnect_OnClientData(SimConnect sender, SIMCONNECT_RECV_CLIENT_DATA data)
	{
		try
		{
			if (data.dwRequestID == 0)
			{
				ResponseString responseString = (ResponseString)data.dwData[0];
				if (responseString.Data == "MF.Pong" && !isMobiConnected)
				{
					Logger.Log(LogLevel.Information, "MobiSimConnect:SimConnect_OnClientData", "MobiFlight WASM Ping acknowledged - opening Client Connection");
					SendMobiWasmCmd("MF.Clients.Add.MSFS_AutoFPS");
				}
				if (responseString.Data == "MF.Clients.Add.MSFS_AutoFPS.Finished" && !isMobiConnected)
				{
					CreateDataAreaClientChannel();
					CreateEventSubscription();
					isMobiConnected = true;
					SendClientWasmCmd("MF.SimVars.Clear");
					SendClientWasmCmd($"MF.Config.MAX_VARS_PER_FRAME.Set.{ServiceModel.MfLvarsPerFrame}");
					Logger.Log(LogLevel.Information, "MobiSimConnect:SimConnect_OnClientData", "MobiFlight WASM Client Connection opened");
				}
			}
			else
			{
				ClientDataValue clientDataValue = (ClientDataValue)data.dwData[0];
				if (simVars.ContainsKey(data.dwRequestID))
				{
					simVars[data.dwRequestID] = clientDataValue.data;
					return;
				}
				Logger.Log(LogLevel.Warning, "MobiSimConnect:SimConnect_OnClientData", $"The received ID '{data.dwRequestID}' is not subscribed! (Data: {data})");
			}
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MobiSimConnect:SimConnect_OnClientData", $"Exception during SimConnect OnClientData! (Exception: {ex.GetType()}) (Data: {data})");
		}
	}

	protected void SimConnect_OnQuit(SimConnect sender, SIMCONNECT_RECV data)
	{
		Disconnect();
	}

	public void Disconnect()
	{
		try
		{
			cancelThread = true;
			if (simConnect != null)
			{
				simConnect.Dispose();
				simConnect = null;
				simConnectHandle = IntPtr.Zero;
			}
			isSimConnected = false;
			isMobiConnected = false;
			nextID = 1u;
			simVars.Clear();
			addressToIndex.Clear();
			Logger.Log(LogLevel.Information, "MobiSimConnect:Disconnect", "SimConnect Connection closed");
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MobiSimConnect:Disconnect", $"Exception during disconnecting from SimConnect! (Exception: {ex.GetType()} {ex.Message})");
		}
	}

	private void Simconnect_OnRecvEventFrame(SimConnect sender, SIMCONNECT_RECV_EVENT_FRAME recEvent)
	{
		if (!SimIsRunning || SimIsPaused || recEvent == null)
		{
			return;
		}
		if (fpsIndex == -1)
		{
			fpsIndex = 1;
			for (int i = 0; i < fpsStatistic.Length; i++)
			{
				fpsStatistic[i] = recEvent.fFrameRate;
			}
		}
		else
		{
			fpsStatistic[fpsIndex] = recEvent.fFrameRate;
			fpsIndex++;
			if (fpsIndex >= fpsStatistic.Length)
			{
				fpsIndex = 0;
			}
		}
		simRate = recEvent.fSimSpeed;
	}

	private void SimConnect_OnReceiveEvent(SimConnect sender, SIMCONNECT_RECV_EVENT recEvent)
	{
		if (recEvent == null)
		{
			return;
		}
		if (recEvent.uEventID == 1)
		{
			if (recEvent.dwData == 1)
			{
				SimIsPaused = true;
			}
			else
			{
				SimIsPaused = false;
			}
		}
		else if (recEvent.uEventID == 0)
		{
			if (recEvent.dwData == 1)
			{
				SimIsRunning = true;
			}
			else
			{
				SimIsRunning = false;
			}
		}
	}

	public void Dispose()
	{
		Disconnect();
		GC.SuppressFinalize(this);
	}

	private void SendClientWasmCmd(string command)
	{
		SendWasmCmd(PILOTSDECK_CLIENT_DATA_ID.MOBIFLIGHT_CMD, MOBIFLIGHT_CLIENT_DATA_ID.MOBIFLIGHT_LVARS, command);
	}

	private void SendClientWasmDummyCmd()
	{
		SendWasmCmd(PILOTSDECK_CLIENT_DATA_ID.MOBIFLIGHT_CMD, MOBIFLIGHT_CLIENT_DATA_ID.MOBIFLIGHT_LVARS, "MF.DummyCmd");
	}

	private void SendMobiWasmCmd(string command)
	{
		SendWasmCmd(MOBIFLIGHT_CLIENT_DATA_ID.MOBIFLIGHT_CMD, MOBIFLIGHT_CLIENT_DATA_ID.MOBIFLIGHT_LVARS, command);
	}

	private void SendWasmCmd(Enum cmdChannelId, Enum cmdId, string command)
	{
		simConnect.SetClientData(cmdChannelId, cmdId, SIMCONNECT_CLIENT_DATA_SET_FLAG.DEFAULT, 0u, new ClientDataString(command));
	}

	private void RegisterRequest(uint id, string name)
	{
		if (requestMap.TryGetValue(id, out var value))
		{
			requestMap[id] = value + "; " + name;
		}
		else
		{
			requestMap[id] = name;
		}
	}

	protected void SimConnect_OnException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
	{
		if (data.dwException != 3 && data.dwException != 29)
		{
			try
			{
				int dwException = (int)data.dwException;
				string text;
				if (!Enum.IsDefined(typeof(SIMCONNECT_EXCEPTION), dwException))
				{
					text = "UNKNOWN_EXCEPTION";
				}
				else
				{
					SIMCONNECT_EXCEPTION sIMCONNECT_EXCEPTION = (SIMCONNECT_EXCEPTION)dwException;
					text = sIMCONNECT_EXCEPTION.ToString();
				}
				string value = text;
				Logger.Log(LogLevel.Error, "MobiSimConnect:SimConnect_OnException", $"SimConnect Exception Event: Exception Code: {data.dwException} ({value}), SendID: {data.dwSendID}, Index: {data.dwIndex}, Thread: {Environment.CurrentManagedThreadId}, IsSimRunning: {model.IsSimRunning}, IPCAlive: {IPCManager.IsSimRunning()}, IsConnected: {IsConnected}");
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error, "MobiSimConnect:SimConnect_OnException", $"Exception inside diagnostic logger: {ex.GetType()} {ex.Message}");
			}
			try
			{
				if (requestMap.TryGetValue(data.dwSendID, out var value2))
				{
					Logger.Log(LogLevel.Error, "MobiSimConnect:SimConnect_OnException", "TriggeredBy: " + value2);
				}
				else if (data.dwSendID != 0)
				{
					Logger.Log(LogLevel.Warning, "MobiSimConnect:SimConnect_OnException", $"TriggeredBy: <unmapped SendID {data.dwSendID}>");
				}
			}
			catch
			{
			}
			Logger.Log(LogLevel.Error, "MobiSimConnect:SimConnect_OnException", $"Exception received: (Exception: {data.dwException})");
		}
		if (data.dwException != 31)
		{
			return;
		}
		exception31Count++;
		Logger.Log(LogLevel.Warning, "MobiSimConnect:SimConnect_OnException", $"Exception 31 detected (count={exception31Count})");
		if (exception31Count <= 3)
		{
			try
			{
				Task.Run(async () =>
				{
					Disconnect();
					await Task.Delay(3000);
					Connect();
				});
				return;
			}
			catch (Exception ex2)
			{
				Logger.Log(LogLevel.Error, "MobiSimConnect:SimConnect_OnException", $"Exception while attempting reconnect: {ex2.GetType()} {ex2.Message}");
				return;
			}
		}
		Application app = Application.Current;
		Application application = app;
		if (application == null)
		{
			return;
		}
		((DispatcherObject)application).Dispatcher.Invoke((Action)(() =>
		{
			if (!MobiflightFolderExistsOrInaccessible())
			{
				MessageBox.Show(Application.Current.MainWindow, "MobiFlight WASM Module not found in your Community folder.\n\nPlease ensure the MobiFlight Module is installed and enabled in your Community folder. The simplest way to do this is, with MSFS not running, to run the AutoFPS installer and click Update.", "MobiFlight Module Error (Exception 31)", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			else if (model.SimVersionChanged)
			{
				MessageBox.Show(Application.Current.MainWindow, "MSFS has updated but its SimConnect connection is incomplete. Please restart MSFS to restore communication with this app.", "MSFS Version Change Detected", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
			else
			{
				((DispatcherObject)Application.Current).Dispatcher.Invoke((Action)(() =>
				{
					string text2 = ((!model.isMSFS2024) ? (File.Exists(App.msConfigSteam2020) ? "%appdata%\\Microsoft Flight Simulator\\WASM" : "%localappdata%\\Packages\\Microsoft.FlightSimulator_8wekyb3d8bbwe\\LocalState\\WASM") : (File.Exists(App.msConfigSteam2024) ? "%appdata%\\Microsoft Flight Simulator 2024\\WASM" : "%localappdata%\\Packages\\Microsoft.Limitless_8wekyb3d8bbwe\\LocalState\\WASM"));
					MainWindow.CustomMessageBox.Show("There is a problem with the SimConnect interface between MSFS and the app.\n\nPlease follow ALL steps below exactly and in order. In most cases, this issue occurs because one of these steps was incomplete or skipped.\n\n1. Update to the latest version of AutoFPS.\n\n2. Run the installer again, choose Repair, then reboot your PC.\n\n3. If the problem continues, close MSFS and delete the MSFS WASM folder located at:\n" + text2 + "\nThen reboot your PC.\n\n4. If the issue persists, click the button below for instructions on performing a CLEAN reinstall of MSFS.\n\nThese are the only known solutions for this error.", "https://github.com/ResetXPDR/MSFS_AutoFPS?tab=readme-ov-file#msfs-clean-reinstallation-instructions", "WASM Module Error (Exception 31)", "Reinstall Guide");
				}));
			}
			model.ServiceExited = true;
			app.Shutdown();
		}));
	}

	public bool MobiflightFolderExistsOrInaccessible()
	{
		try
		{
			string text = MainWindow.GetCommunityPathFromUserCfg(model.isMSFS2024);
			if (string.IsNullOrWhiteSpace(text))
			{
				if (string.IsNullOrWhiteSpace(model.InstalledPackagesPath))
				{
					return true;
				}
				text = Path.Combine(model.InstalledPackagesPath, "Community");
			}
			return Directory.Exists(Path.Combine(text, "mobiflight-event-module"));
		}
		catch
		{
			return true;
		}
	}

	public void SubscribeLvar(string address)
	{
		SubscribeVariable("(L:" + address + ")");
	}

	public void SubscribeSimVar(string name, string unit)
	{
		SubscribeVariable($"(A:{name}, {unit})");
	}

	protected void SubscribeVariable(string address)
	{
		try
		{
			if (!addressToIndex.ContainsKey(address))
			{
				RegisterVariable(nextID, address);
				simVars.Add(nextID, 0f);
				addressToIndex.Add(address, nextID);
				nextID++;
			}
			else
			{
				Logger.Log(LogLevel.Warning, "MobiSimConnect:SubscribeVariable", "The Address '" + address + "' is already subscribed");
			}
		}
		catch (Exception ex)
		{
			DateTime utcNow = DateTime.UtcNow;
			if (!subErrorActive)
			{
				subErrorActive = true;
				lastSubError = utcNow;
			}
			else if (!((utcNow - lastSubError).TotalSeconds < 5.0))
			{
				Logger.Log(LogLevel.Error, "MobiSimConnect:SubscribeVariable", $"Persistent SimConnect error while subscribing '{address}'! (Exception: {ex.GetType()}) (Message: {ex.Message})");
				subErrorActive = false;
			}
		}
	}

	protected void RegisterVariable(uint ID, string address)
	{
		simConnect.AddToClientDataDefinition((SIMCONNECT_DEFINE_ID)ID, (ID - 1) * 4, 4u, 0f, 0u);
		simConnect?.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, ClientDataValue>((SIMCONNECT_DEFINE_ID)ID);
		simConnect?.RequestClientData(PILOTSDECK_CLIENT_DATA_ID.MOBIFLIGHT_LVARS, (SIMCONNECT_REQUEST_ID)ID, (SIMCONNECT_DEFINE_ID)ID, SIMCONNECT_CLIENT_DATA_PERIOD.ON_SET, SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.CHANGED, 0u, 0u, 0u);
		RegisterRequest(ID, "RequestClientData(" + address + ")");
		SendClientWasmCmd("MF.SimVars.Add." + address);
	}

	public void UnsubscribeAll()
	{
		try
		{
			SendClientWasmCmd("MF.SimVars.Clear");
			nextID = 1u;
			simVars.Clear();
			addressToIndex.Clear();
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MobiSimConnect:UnsubscribeAll", $"Exception while unsubscribing SimVars! (Exception: {ex.GetType()}) (Message: {ex.Message})");
		}
	}

	public float ReadLvar(string address)
	{
		if (addressToIndex.TryGetValue("(L:" + address + ")", out var value) && simVars.TryGetValue(value, out var value2))
		{
			return value2;
		}
		return 0f;
	}

	public float ReadSimVar(string name, string unit)
	{
		string key = $"(A:{name}, {unit})";
		if (addressToIndex.TryGetValue(key, out var value) && simVars.TryGetValue(value, out var value2))
		{
			return value2;
		}
		return 0f;
	}

	public void WriteLvar(string address, float value)
	{
		SendClientWasmCmd($"MF.SimVars.Set.{string.Format(new CultureInfo("en-US").NumberFormat, "{0:G}", value)} (>L:{address})");
		SendClientWasmDummyCmd();
	}

	public void ExecuteCode(string code)
	{
		SendClientWasmCmd("MF.SimVars.Set." + code);
		SendClientWasmDummyCmd();
	}

	public int AltAboveGround(bool onGround)
	{
		int num = (int)ReadSimVar("PLANE ALT ABOVE GROUND", "feet");
		if ((num == 0) & onGround)
		{
			num = (int)ReadSimVar("PLANE ALT ABOVE GROUND MINUS CG", "feet");
		}
		return num;
	}
}

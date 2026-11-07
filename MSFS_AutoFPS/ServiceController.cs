using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using RTSSSharedMemoryNET;

namespace MSFS_AutoFPS;

public class ServiceController
{
	protected ServiceModel Model;

	protected int Interval = 1000;

	private int fpsAverageIndex4 = -1;

	private float[] fpsAverage4;

	private int fpsAverageIndex4Last = -1;

	private float[] fpsAverage4Last;

	private bool fpsSourceLast;

	private int RTSSDetailedLogCount = 10;

	private double[] RTSSInstFrameTimeArray = new double[10];

	private float confirmAvg4 = -1f;

	private float[] candidateBuffer4;

	private int candidateIndex4;

	private bool candidateActive4;

	private int deviationStreak4;

	private int deviationStreak4Last;

	private bool trendingDown4;

	private bool trendingDown4Last;

	public ServiceController(ServiceModel model)
	{
		Model = model;
		fpsAverage4 = new float[5];
		fpsAverage4Last = new float[5];
		candidateBuffer4 = new float[5];
	}

	public void Run()
	{
		try
		{
			Logger.Log(LogLevel.Information, "ServiceController:Run", "Service starting ...");
			while (!Model.CancellationRequested && Model.AppEnabled)
			{
				if (Wait())
				{
					ServiceLoop();
					continue;
				}
				if (!IPCManager.IsSimRunning())
				{
					Model.CancellationRequested = true;
					Model.ServiceExited = true;
					Logger.Log(LogLevel.Information, "ServiceController:Run", "Simulator closed – AutoFPS shutting down.");
					return;
				}
				Reset();
				Logger.Log(LogLevel.Information, "ServiceController:Run", "Session aborted, Retry possible - Waiting for new Session");
			}
			if (!Model.AppEnabled)
			{
				Logger.Log(LogLevel.Critical, "ServiceController:Run", "MSFS compatibility test failed - app disabled.");
			}
			IPCManager.CloseSafe();
		}
		catch (Exception value)
		{
			Logger.Log(LogLevel.Critical, "ServiceController:Run", $"Critical Exception occurred: {value}");
		}
	}

	protected bool Wait()
	{
		if (!IPCManager.WaitForSimulator(Model))
		{
			Model.IsSimRunning = false;
			return false;
		}
		Model.IsSimRunning = true;
		if (IPCManager.IsSim2024() != Model.isMSFS2024_last)
		{
			Model.isMSFS2024_last = IPCManager.IsSim2024();
			if (Model.LoadConfiguration())
			{
				Model.ReloadAppWindowSettings = true;
			}
			Model.MemoryCompatTestComplete = false;
		}
		if (!Model.MemoryCompatTestComplete)
		{
			new MemoryManager(Model);
			while (!Model.MemoryCompatTestComplete)
			{
				Thread.Sleep(Interval);
			}
		}
		if (Model.DevTesting)
		{
			Model.MemoryAccess = new MemoryManager(Model);
			while (Model.DevTesting)
			{
				Thread.Sleep(Interval);
			}
		}
		if (!IPCManager.WaitForConnection(Model))
		{
			return false;
		}
		if (!IPCManager.WaitForSessionReady(Model))
		{
			Model.IsSessionRunning = false;
			return false;
		}
		Model.IsSessionRunning = true;
		return true;
	}

	protected void Reset()
	{
		try
		{
			IPCManager.SimConnect?.Disconnect();
			IPCManager.SimConnect = null;
			Model.IsSessionRunning = false;
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Critical, "ServiceController:Reset", "Exception during Reset: " + ex.Source + " - " + ex.Message);
		}
	}

	protected void ServiceLoop()
	{
		try
		{
			if (!Model.AppEnabled)
			{
				return;
			}
			Model.MemoryAccess = new MemoryManager(Model);
			LODController lODController = new LODController(Model);
			Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Starting Service Loop");
			Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", $"Initial LODs PC {Model.DefaultTLOD} / {Model.DefaultOLOD} and VR {Model.DefaultTLOD_VR} / {Model.DefaultOLOD_VR}");
			Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Cloud Quality PC " + ServiceModel.SettingQualityText(Model.DefaultCloudQ) + " / VR " + ServiceModel.SettingQualityText(Model.DefaultCloudQ_VR));
			Model.MemoryAccess.IsFgModeEnabled();
			if (Model.isMSFS2024)
			{
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Off Screen Terrain Pre-Caching PC " + ServiceModel.SettingQualityText(Model.DefaultOSTPCache) + " / VR " + ServiceModel.SettingQualityText(Model.DefaultOSTPCache_VR));
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Terrain Shadows Enabled PC " + (Model.DefaultTShadowsEnabled ? "Enabled" : "Disabled") + " / VR " + (Model.DefaultTShadowsEnabled_VR ? "Enabled" : "Disabled"));
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", $"Initial Terrain Shadows PC {Model.DefaultTShadows} / VR {Model.DefaultTShadows_VR}");
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Raytraced Shadows PC " + (Model.DefaultRTShadows ? "Enabled" : "Disabled") + " / VR " + (Model.DefaultRTShadows_VR ? "Enabled" : "Disabled"));
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Displacement Mapping PC " + (Model.DefaultDispMap ? "Enabled" : "Disabled") + " / VR " + (Model.DefaultDispMap_VR ? "Enabled" : "Disabled"));
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Buildings PC " + ServiceModel.SettingQualityText(Model.DefaultBldings) + " / VR " + ServiceModel.SettingQualityText(Model.DefaultBldings_VR));
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Trees PC " + ServiceModel.SettingQualityText(Model.DefaultTrees) + " / VR " + ServiceModel.SettingQualityText(Model.DefaultTrees_VR));
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Plants PC " + ServiceModel.SettingQualityText(Model.DefaultPlants) + " / VR " + ServiceModel.SettingQualityText(Model.DefaultPlants_VR));
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Grass PC " + ServiceModel.SettingQualityText(Model.DefaultGrass) + " / VR " + ServiceModel.SettingQualityText(Model.DefaultGrass_VR));
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Parked Aircraft Quantity PC " + ServiceModel.SettingQualityText(Model.DefaultPkdAcQ) + " / VR " + ServiceModel.SettingQualityText(Model.DefaultPkdAcQ_VR));
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Aircraft Traffic Quantity PC " + ServiceModel.SettingQualityText(Model.DefaultAcTrafQ) + " / VR " + ServiceModel.SettingQualityText(Model.DefaultAcTrafQ_VR));
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "Initial Dynamic Settings PC " + (Model.DefaultDynSet ? ("Enabled @ " + Model.DefaultDynSetFrameRateTarget) : "Disabled") + " / VR " + (Model.DefaultDynSetVR ? ("Enabled @ " + Model.DefaultDynSetFrameRateTarget_VR) : "Disabled"));
			}
			if (Model.TLODAutoMethod[Model.activeProfile] != 2)
			{
				Model.FPSSettleInitial = true;
			}
			Model.DefaultSettingsRead = true;
			lODController.GetAltAboveGroundDelayed();
			if (!Model.UseExpertOptions)
			{
				Model.SetDynamicSettingsState();
			}
			Model.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
			if (!Model.MonitorMode)
			{
				Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", $"Setting Initial LODs @ {Model.altAboveGnd:F0} ft AGL to {Model.tlod} / {Model.olod}");
			}
			Model.initialLODsSet = true;
			Model.rangeMinTLOD = Model.MinTLOD[Model.activeProfile];
			Model.rangeMaxTLOD = (Model.FreeTLOD[Model.activeProfile] ? Model.MaxTLOD[Model.activeProfile] : Math.Min(Model.tlod, Model.MaxTLOD[Model.activeProfile]));
			Stopwatch stopwatch = Stopwatch.StartNew();
			long num = stopwatch.ElapsedMilliseconds;
			while (!Model.CancellationRequested && IPCManager.IsSimRunning() && IPCManager.IsCamReady(Model))
			{
				double totalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
				long num2 = 0L;
				num += Interval;
				UpdateAverageFPS_Master();
				long num3 = stopwatch.ElapsedMilliseconds - (num - Interval);
				if (num3 > 1100)
				{
					num = stopwatch.ElapsedMilliseconds + Interval;
					if (Model.LogPlus)
					{
						Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", $"Scheduler reset after loop overrun ({num3} ms)");
					}
				}
				if (Model.VrModeActive && Model.OfxrModeEnabled && Model.OfxrStatusJsonActive && Model.OfxrOutputFps > 0f && Model.OfxrStatusJsonLatency > 75f)
				{
					num2 = (long)(Model.OfxrStatusJsonLatency - 50f);
					num -= num2;
					if (Model.LogPlus)
					{
						Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", $"Applied OFXR phase correction of {Model.OfxrStatusJsonLatency - 50f:F0} ms");
					}
				}
				try
				{
					if (IPCManager.IsSimActive(Model))
					{
						if (Model.AppPaused && Model.TLODAutoMethod[Model.activeProfile] != 2)
						{
							Model.FPSSettleActive = true;
						}
						lODController.RunTick();
					}
					else
					{
						if (!Model.AppPaused)
						{
							Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "App paused while MSFS is paused");
						}
						Model.AppPaused = true;
					}
				}
				catch (Exception value)
				{
					Logger.Log(LogLevel.Critical, "ServiceController:ServiceLoop", $"Critical Exception during ServiceLoop tick:\n{value}");
				}
				long num4 = num - stopwatch.ElapsedMilliseconds;
				if (num4 > 0)
				{
					Thread.Sleep((int)num4);
				}
				Model.LastLODControllerLoopTimeMs = stopwatch.Elapsed.TotalMilliseconds - totalMilliseconds;
			}
			if (!Model.UseExpertOptions)
			{
				Model.ResetDynamicSettingsState();
			}
			Logger.Log(LogLevel.Information, "ServiceController:ServiceLoop", "ServiceLoop ended");
			if (!Model.MonitorMode)
			{
				RestoreDefaultMSFSSettings("ServiceController:ServiceLoop");
			}
			Model.TLODAutoMethod[0] = 0;
			fpsAverageIndex4 = -1;
			fpsSourceLast = false;
			Model.initialLODsSet = false;
			Model.SpikeDetected = false;
			Model.MemoryAccess = null;
		}
		catch (Exception value2)
		{
			Logger.Log(LogLevel.Critical, "ServiceController:ServiceLoop", $"Fatal Exception in ServiceLoop:\n{value2}");
		}
		finally
		{
			Model.IsSessionRunning = false;
		}
	}

	private float GetRTSSFPS()
	{
		AppEntry[] appEntries = OSD.GetAppEntries();
		if (appEntries == null)
		{
			return -1f;
		}
		AppEntry[] array = appEntries;
		foreach (AppEntry appEntry in array)
		{
			if (!appEntry.Name.Contains("FlightSimulator"))
			{
				continue;
			}
			if (RTSSDetailedLogCount > 0)
			{
				RTSSDetailedLogCount--;
				RTSSInstFrameTimeArray[RTSSDetailedLogCount] = appEntry.InstantaneousFrameTime.TotalMilliseconds;
				if (RTSSDetailedLogCount == 0 && RTSSInstFrameTimeArray.All((double x) => x == RTSSInstFrameTimeArray[0]))
				{
					if (Model.LogPlus)
					{
						Logger.Log(LogLevel.Information, "ServiceController:GetRTSSFPS", "RTSS Detailed Log Data Frozen: InstFrameTimeArray=" + string.Join(", ", RTSSInstFrameTimeArray.Select((double x) => x.ToString("F2"))));
					}
					if (App.IsRunningAsAdmin())
					{
						string text = null;
						string environmentVariable = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
						if (!string.IsNullOrEmpty(environmentVariable))
						{
							string text2 = Path.Combine(environmentVariable, "RivaTuner Statistics Server", "RTSS.exe");
							if (File.Exists(text2))
							{
								text = text2;
							}
						}
						if (text != null)
						{
							Process.Start(text, "-u");
							if (Model.LogPlus)
							{
								Logger.Log(LogLevel.Information, "ServiceController:GetRTSSFPS", "RTSS.exe re-launched to unfreeze data");
							}
						}
						else
						{
							Logger.Log(LogLevel.Warning, "ServiceController:GetRTSSFPS", "RTSS.exe not found in Program Files (x86) - unable to unfreeze data");
						}
					}
					else
					{
						Model.RTSSDataFrozen = true;
					}
				}
			}
			if (appEntry.StatFrameTimeBufFramerate > 0f)
			{
				return appEntry.StatFrameTimeBufFramerate;
			}
			return appEntry.InstantaneousFrameRate;
		}
		return -1f;
	}

	private void ResetAverageFPS(bool suppressLog = true)
	{
		fpsAverageIndex4 = -1;
		RTSSDetailedLogCount = 10;
		Array.Clear(RTSSInstFrameTimeArray, 0, RTSSInstFrameTimeArray.Length);
		Model.RTSSDataFrozen = false;
		Model.RTSSDataFrozenVRWarningSuppressed = Model.GetSetting("RTSSDataFrozenVRWarningSuppressed", "false") == "true";
		Model.RTSSDataFrozen2DWarningSuppressed = Model.GetSetting("RTSSDataFrozen2DWarningSuppressed", "false") == "true";
		if (!suppressLog)
		{
			Logger.Log(LogLevel.Information, "ServiceController:ResetAverageFPS", "Average FPS reset to initial state");
		}
		Model.fpsAverageReset = false;
		Model.fpsRawTrend.Reset();
	}

	private void UpdateAverageFPS_Master()
	{
		float num = (Model.fpsRawMSFS = IPCManager.SimConnect.GetAverageFPS());
		float num2 = num;
		float rTSSFPS;
		if ((rTSSFPS = GetRTSSFPS()) == -1f)
		{
			if (Model.fpsSourceRTSS)
			{
				Logger.Log(LogLevel.Warning, "ServiceController:UpdateAverageFPS", "RTSS FPS source not available, using MSFS FPS instead");
			}
			Model.fpsSourceRTSS = false;
			if (Model.RTSSDataFrozen)
			{
				RTSSDetailedLogCount = 10;
				Array.Clear(RTSSInstFrameTimeArray, 0, RTSSInstFrameTimeArray.Length);
				Model.RTSSDataFrozen = false;
			}
		}
		else if (Model.RTSSDataFrozen)
		{
			if (Model.fpsSourceRTSS)
			{
				Logger.Log(LogLevel.Warning, "ServiceController:UpdateAverageFPS", "RTSS FPS data frozen, using MSFS FPS instead");
			}
			Model.fpsSourceRTSS = false;
		}
		else
		{
			if (!Model.fpsSourceRTSS && Model.fpsSourceRTSSDefault)
			{
				Logger.Log(LogLevel.Information, "ServiceController:UpdateAverageFPS", "RTSS FPS source available, using RTSS FPS instead");
				Model.fpsSourceRTSS = true;
			}
			if (Model.fpsSourceRTSS)
			{
				num2 = rTSSFPS;
			}
		}
		if (Model.VrModeActive)
		{
			if (Model.OfxrModeEnabled && Model.OfxrStatusJsonActive && Model.OfxrOutputFps > 0f)
			{
				Model.ReadOFXRStatusJson(vrActive: true);
				num2 = Model.OfxrOutputFps;
			}
			else if (Model.VxrModeEnabled && Model.VxrStatusJsonActive && Model.VxrOutputFps > 0f)
			{
				Model.ReadVXRStatusJson(vrActive: true);
				num2 = Model.VxrOutputFps;
			}
		}
		if (Model.fpsAverageReset)
		{
			ResetAverageFPS();
		}
		Model.fpsRawTrend.Add(num2);
		Model.fpsRaw = num2;
		Model.fpsAverage = ComputeAverageFPS4(num2);
		fpsSourceLast = Model.fpsSourceRTSS;
	}

	private float ComputeAverageFPS4(float currentFPS, bool useSimpleAvg = false)
	{
		if (fpsAverageIndex4 == -1 || Model.fpsSourceRTSS != fpsSourceLast)
		{
			fpsAverageIndex4 = 1;
			confirmAvg4 = currentFPS;
			deviationStreak4 = 0;
			trendingDown4 = (trendingDown4Last = false);
			for (int i = 0; i < fpsAverage4.Length; i++)
			{
				fpsAverage4[i] = currentFPS;
			}
			candidateActive4 = false;
			Array.Copy(fpsAverage4, fpsAverage4Last, fpsAverage4.Length);
			fpsAverageIndex4Last = fpsAverageIndex4;
			return confirmAvg4;
		}
		if (deviationStreak4 == 0)
		{
			fpsAverageIndex4Last = fpsAverageIndex4;
		}
		if (++fpsAverageIndex4 >= fpsAverage4.Length)
		{
			fpsAverageIndex4 = 0;
		}
		if (useSimpleAvg)
		{
			fpsAverage4[fpsAverageIndex4] = currentFPS;
			confirmAvg4 = fpsAverage4.Average();
			return confirmAvg4;
		}
		float num = confirmAvg4 * ((Model.TLODAutoMethod[Model.activeProfile] >= 2) ? 0.1f : 0.15f);
		float num2 = confirmAvg4 - num;
		float num3 = confirmAvg4 + num;
		if (currentFPS < confirmAvg4)
		{
			trendingDown4 = true;
		}
		else
		{
			trendingDown4 = false;
		}
		if (currentFPS >= num2 && currentFPS <= num3)
		{
			if (deviationStreak4 > 0)
			{
				Array.Copy(fpsAverage4Last, fpsAverage4, fpsAverage4.Length);
				fpsAverageIndex4 = fpsAverageIndex4Last;
				deviationStreak4 = 0;
				candidateActive4 = false;
			}
			else
			{
				Array.Copy(fpsAverage4, fpsAverage4Last, fpsAverage4.Length);
				fpsAverage4[fpsAverageIndex4] = currentFPS;
			}
			confirmAvg4 = fpsAverage4.Average();
			deviationStreak4Last = 0;
		}
		else
		{
			if (candidateActive4 && trendingDown4 != trendingDown4Last)
			{
				deviationStreak4 = 0;
			}
			if (!candidateActive4)
			{
				Array.Copy(fpsAverage4, candidateBuffer4, fpsAverage4.Length);
				candidateIndex4 = fpsAverageIndex4;
				deviationStreak4 = 0;
				candidateActive4 = true;
				if (deviationStreak4Last == 0)
				{
					confirmAvg4 = fpsAverage4Last.Average();
				}
			}
			else if (++candidateIndex4 >= candidateBuffer4.Length)
			{
				candidateIndex4 = 0;
			}
			candidateBuffer4[candidateIndex4] = currentFPS;
			deviationStreak4++;
			deviationStreak4Last = deviationStreak4;
			if (deviationStreak4 >= 3)
			{
				int num4 = candidateBuffer4.Length;
				int num5 = Math.Max(2, 2);
				List<float> list = new List<float>();
				for (int j = 0; j < num5; j++)
				{
					int num6 = (candidateIndex4 - j + num4) % num4;
					list.Add(candidateBuffer4[num6]);
				}
				float num7 = list.Average();
				float num8 = 0f;
				foreach (float item in list)
				{
					float num9 = item - num7;
					num8 += num9 * num9;
				}
				int num10 = num5 - 1;
				if (num8 / (float)num10 < 1f)
				{
					int num11 = (candidateIndex4 - deviationStreak4 + 1 + num4) % num4;
					int num12 = (candidateIndex4 - num5 + 1 + num4) % num4;
					for (int k = 0; k < num4; k++)
					{
						if (num11 > candidateIndex4)
						{
							if (k >= num11)
							{
								_ = 1;
							}
							else
								_ = k <= candidateIndex4;
						}
						else if (k >= num11)
						{
							_ = k <= candidateIndex4;
						}
						else
							_ = 0;
						if (!((num12 > candidateIndex4) ? (k >= num12 || k <= candidateIndex4) : (k >= num12 && k <= candidateIndex4)))
						{
							candidateBuffer4[k] = num7;
						}
					}
				}
				float num13 = candidateBuffer4.Average();
				Array.Copy(candidateBuffer4, fpsAverage4, fpsAverage4.Length);
				fpsAverageIndex4 = candidateIndex4;
				confirmAvg4 = num13;
				num = confirmAvg4 * ((Model.TLODAutoMethod[Model.activeProfile] >= 2) ? 0.1f : 0.15f);
				num2 = confirmAvg4 - num;
				num3 = confirmAvg4 + num;
				if (currentFPS >= num2 && currentFPS <= num3)
				{
					candidateActive4 = false;
					deviationStreak4 = 0;
				}
				else
				{
					deviationStreak4 = 1;
				}
			}
		}
		trendingDown4Last = trendingDown4;
		return confirmAvg4;
	}

	public void RestoreDefaultMSFSSettings(string callingFunction)
	{
		if (IPCManager.IsSimRunning())
		{
			if (Model.ResetInProgress)
			{
				Thread.Sleep(100);
			}
			Logger.Log(LogLevel.Information, callingFunction, $"Resetting LODs to PC {Model.DefaultTLOD} / {Model.DefaultOLOD} and VR {Model.DefaultTLOD_VR} / {Model.DefaultOLOD_VR}");
			Model.MemoryAccess.SetTLOD_PC(Model.DefaultTLOD);
			Model.MemoryAccess.SetTLOD_VR(Model.DefaultTLOD_VR);
			Model.MemoryAccess.SetOLOD_PC(Model.DefaultOLOD);
			Model.MemoryAccess.SetOLOD_VR(Model.DefaultOLOD_VR);
			Logger.Log(LogLevel.Information, callingFunction, "Resetting Cloud Quality to PC " + ServiceModel.SettingQualityText(Model.DefaultCloudQ) + " / VR " + ServiceModel.SettingQualityText(Model.DefaultCloudQ_VR));
			Model.MemoryAccess.SetCloudQ(Model.DefaultCloudQ);
			Model.MemoryAccess.SetCloudQ(Model.DefaultCloudQ_VR, VR: true);
			if (Model.isMSFS2024)
			{
				Logger.Log(LogLevel.Information, callingFunction, "Resetting Reduction Settings to saved MSFS settings");
				Model.ResetReductionSettings();
				Model.ResetReductionSettings(VR: true);
			}
		}
		Model.MemoryAccess.RestoreUserCfgOptSettings();
	}
}

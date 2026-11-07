using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using CpuHeadroom;
using SharpDX.DXGI;

namespace MSFS_AutoFPS;

public class LODController
{
	private enum TrendState
	{
		Down,
		Steady,
		Up
	}

	private MobiSimConnect SimConnect;

	private ServiceModel Model;

	private int[] verticalStats = new int[5];

	private float[] verticalStatsVS = new float[5];

	private int verticalIndex;

	private float vs;

	private float[] gpuUsage = new float[5];

	private int gpuIndex;

	private const float CloudRecoveryExtraTolerance = 0.15f;

	private const float MinTLODExtraActiveTolerance = 0.1f;

	private const float TLODBasePlusSeekHeadroomTolerance = 0.1f;

	private double LastSpikeReductionLat;

	private double LastSpikeReductionLon;

	private bool SpikeRecoveryActive;

	private long spikeVerificationStartTick;

	private bool spikeVerificationLastDetected;

	private int RTSSBufferRecordCount = 10;

	private bool RTSSUnavailableMessageShown;

	private bool AutoFPSInFlightPhase;

	private float MinTLODExtraMaxSeekAchieved;

	private long MinTLODExtraSeekLastTickMark;

	private bool MinTLODExtraReSeek;

	private bool ReduceSettingsRecoveryEnabled;

	private int CloudRecoveryExtraNonExpert;

	private long CloudRecoveryTickMark;

	private long CloudHysteresisDecayTick;

	private long TotalTicks;

	private float altAboveGndLast;

	private float groundSpeedLast;

	private bool criticalFlightPhaseActive;

	private bool NightTimeLast;

	private int DecCloudQActiveLast;

	private float TLODStep;

	private bool cameraStateCockpit;

	private bool FPSToleranceExceeded;

	private bool FGInactive;

	private TrendState TLODTrendLast;

	private float averageFPSRounded;

	private float averageFPSUnrounded;

	private float averageFPSUnroundedRaw;

	private float[] deltaFPSTrend = new float[30];

	private int deltaFPSTrendIndex;

	private float[] deltaFPSTrendShort = new float[5];

	private int deltaFPSTrendShortIndex;

	private float deltaFPSLowest;

	private float deltaFPSRange;

	private float deltaFPSRangeLowest;

	private float deltaFPSHighest;

	private float deltaFPSTrendMin;

	private float deltaFPSRawlast;

	private float newTLOD;

	private float MinTLODNoExtra;

	private int TLODExtraMtnsTimer;

	private int logTimer = 1;

	private int logTimerInterval = 9;

	private float logAltAboveGndLast = -1f;

	private float logTlodLast = -1f;

	private float logOlodLast = -1f;

	private float logSpikeTLODReductionAmountLast = -1f;

	private bool logDecCloudQActiveLast = true;

	private bool logIsAppPriorityFPSLast;

	private float logRangeMinTLODLast = -1f;

	private bool logNightTimeLast;

	private bool logVRAMProtInactiveLast;

	private int gpuMemoryUsageLast;

	private int cameraDelayCount;

	private float simRateLast = 1f;

	private int LsModeMultiplierlast = -1;

	private bool lastOfxrPaused;

	private int lastOfxrMultiplier;

	private bool lastOfxrOverSpeed;

	private bool lastOfxrDesynced;

	private int ofxrDesyncCount;

	private int lastOfxrAutoMultiplier;

	private bool lastVxrPaused;

	private bool lastVxrStandby;

	private int lastVxrMultiplier;

	private string lastVxrMode = "";

	private bool lastVxrUnevenPacing;

	private bool lastVrFgConflict;

	private GpuzWrapper gpuz = new GpuzWrapper();

	private int gpuzGPULoadSensorIndex = -1;

	private int gpuzGPUMemoryUsedSensorIndex = -1;

	private bool gpuModelDetected;

	private int gpuTotalMemory;

	private string[] _buffer;

	private DateTime[] _bufferTs;

	private int _currentIndex;

	private DateTime _lastOutputTime = DateTime.MinValue;

	private int _lastOutputIndex = -1;

	private int _perfTriggerCount;

	private int _bufferSizeCached;

	private double _domSmooth = double.NaN;

	private double _maxSmooth = double.NaN;

	private double _msfsSmooth = double.NaN;

	private double _topXSmooth = double.NaN;

	private double _topXDeltaSmooth = double.NaN;

	private bool _seenFirstPerfSample;

	private int _triggerPendingInt;

	private DateTime _lastTriggerTimeRounded = DateTime.MinValue;

	public LODController(ServiceModel model)
	{
		Model = model;
		SimConnect = IPCManager.SimConnect;
		Model.VRAMSettingsReduceAlt = ((Model.VRAMPlusSuite >= 2) ? Model.ReductionSettingsRecoveryAlt[Model.activeProfile] : 0);
		Model.UpdateDynamicSettingsState();
		GetMSFSState();
		if (!gpuzRunning())
		{
			Logger.Log(LogLevel.Information, "LODController:LODController", "GPU-Z companion app not running");
			gpuzGPULoadSensorIndex = -1;
		}
		SetActiveLODs();
		if (SimConnect.ReadSimVar("GLASSCOCKPIT AUTOMATIC BRIGHTNESS", "percent") > 25f)
		{
			Model.NightTime = false;
		}
		else
		{
			Model.NightTime = true;
		}
	}

	public void GetAltAboveGroundDelayed()
	{
		Thread.Sleep(1000);
		Model.altAboveGnd = SimConnect.ReadSimVar("PLANE ALT ABOVE GROUND", "feet");
	}

	public static double DistanceNM(double lat1, double lon1, double lat2, double lon2)
	{
		double num = (lat2 - lat1) * Math.PI / 180.0;
		double num2 = (lon2 - lon1) * Math.PI / 180.0;
		double num3 = Math.Sin(num / 2.0) * Math.Sin(num / 2.0) + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Sin(num2 / 2.0) * Math.Sin(num2 / 2.0);
		double num4 = 2.0 * Math.Atan2(Math.Sqrt(num3), Math.Sqrt(1.0 - num3));
		return 3440.06479 * num4;
	}

	private string GetGfxModeName()
	{
		switch (Model.ActiveGraphicsMode)
		{
		case "FG":
			if (Model.FgModeMultiplier == 1)
			{
				return $"DynFG {Model.DynFgCurrentMultiplier}X";
			}
			return "FG " + (Model.ActiveWindowMSFS ? (Model.FgModeMultiplier + "X") : "InAct");
		case "Man FG":
			if (Model.ManFgMultiplier == 1)
			{
				return $"Man DynFG {Model.DynFgCurrentMultiplier}X";
			}
			return "Man FG " + (Model.ActiveWindowMSFS ? (Model.ManFgMultiplier + "X") : "InAct");
		case "OFXR":
			return $"OFXR {Model.OfxrModeMultiplier}X";
		case "VXR":
			if (string.IsNullOrEmpty(Model.VxrMode))
			{
				return $"VXR {Model.VxrModeMultiplier}X";
			}
			return Model.VxrMode;
		default:
			return Model.ActiveGraphicsMode;
		}
	}

	private void UpdateVariables()
	{
		vs = SimConnect.ReadSimVar("VERTICAL SPEED", "feet per second");
		Model.OnGround = SimConnect.ReadSimVar("SIM ON GROUND", "Bool") == 1f;
		verticalStatsVS[verticalIndex] = vs;
		if (vs >= 8f)
		{
			verticalStats[verticalIndex] = 1;
		}
		else if (vs <= -2f)
		{
			verticalStats[verticalIndex] = -1;
		}
		else
		{
			verticalStats[verticalIndex] = 0;
		}
		if (++verticalIndex >= verticalStats.Length || verticalIndex >= verticalStatsVS.Length)
		{
			verticalIndex = 0;
		}
		Model.VerticalTrend = VerticalSum();
		Model.altAboveGnd = SimConnect.ReadSimVar("PLANE ALT ABOVE GROUND", "feet");
		if (Model.altAboveGnd == 0f && !Model.OnGround)
		{
			Model.altAboveGnd = SimConnect.ReadSimVar("PLANE ALT ABOVE GROUND MINUS CG", "feet");
		}
		Model.groundSpeed = SimConnect.ReadSimVar("GROUND VELOCITY", "knots");
		Model.altitude = SimConnect.ReadSimVar("PLANE ALTITUDE", "feet");
		criticalFlightPhaseActive = (Model.OnGround && Model.groundSpeed > 5f) || (!Model.OnGround && Model.altAboveGnd < Model.AltTLODBase[Model.activeProfile]);
		if (SimConnect.ReadSimVar("GLASSCOCKPIT AUTOMATIC BRIGHTNESS", "percent") > 25f)
		{
			Model.NightTime = false;
		}
		else
		{
			Model.NightTime = true;
		}
		Model.simRate = SimConnect.GetSimRate();
		if (Model.simRate != simRateLast)
		{
			string value = Regex.Replace(Model.ProfileName[Model.FlightTypeExpert], "\\s\\d+(\\.\\d+)?X$", "");
			string value2 = $"{value} {Model.simRate:0.#}X";
			if (Enumerable.Contains(Model.ProfileName, value2))
			{
				int num = Array.IndexOf(Model.ProfileName, value2);
				Model.SetSetting("FlightTypeExpertExtended", num.ToString());
				Logger.Log(LogLevel.Information, "LODController:GetMSFSState", "Auto-switching to " + Model.ProfileName[num] + " flight type profile");
				Model.ActiveGraphicsModeChanged = true;
				Model.fpsAverageReset = true;
				Model.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
			}
		}
		simRateLast = Model.simRate;
		averageFPSRounded = GetAverageFPS();
		averageFPSUnrounded = GetAverageFPS(unrounded: true);
		averageFPSUnroundedRaw = GetAverageFPS(unrounded: true, useRaw: true);
		GetMSFSState();
		FGInactive = (Model.FgModeEnabled || Model.MsmModeEnabled) && !Model.ActiveWindowMSFS;
		if (gpuzRunning())
		{
			gpuUsage[gpuIndex++] = (float)gpuz.SensorValue(gpuzGPULoadSensorIndex);
			if (gpuIndex >= gpuUsage.Length)
			{
				gpuIndex = 0;
			}
			Model.gpuUsage = gpuUsage.Average();
			if (gpuModelDetected)
			{
				Model.gpuMemoryUsage = Math.Max((int)(gpuz.SensorValue(gpuzGPUMemoryUsedSensorIndex) / (double)gpuTotalMemory * 100.0), 0);
			}
		}
		else
		{
			Model.gpuUsage = -1f;
			Model.gpuMemoryUsage = 0;
			Model.VRAMProtInactive = true;
		}
		if (Model.groundSpeed > 1f && Model.altAboveGnd == altAboveGndLast && Model.groundSpeed == groundSpeedLast)
		{
			if (!Model.AppPaused)
			{
				Logger.Log(LogLevel.Information, "LODController:UpdateVariables", "App paused while MSFS is paused");
			}
			Model.AppPaused = true;
		}
		else
		{
			if (Model.AppPaused)
			{
				if (Model.UseExpertOptions && Model.UpdateDynamicSettingsState())
				{
					Model.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
				}
				Model.FPSSettleActive = true;
				Logger.Log(LogLevel.Information, "LODController:UpdateVariables", "App resumed after MSFS resumed");
			}
			Model.AppPaused = false;
		}
		deltaFPSTrendMin = (0f - Model.TargetFPS) * 0.025f;
		if (!(cameraStateCockpit = SimConnect.ReadSimVar("CAMERA STATE", "Enum") == 2f))
		{
			cameraDelayCount = 5;
		}
		else
		{
			cameraDelayCount--;
		}
		if (cameraDelayCount > 0)
		{
			deltaFPSTrendMin *= 2f;
		}
		else
		{
			cameraDelayCount = 0;
		}
		SetActiveLODs();
		_ = Model.LogPlus;
		string[] array;
		string text;
		if (logTimer == 0 && (Model.LogPlus || ((!Model.FPSSettleActive || Model.TLODAutoMethod[Model.activeProfile] >= 2) && (Math.Round(Model.altAboveGnd) != Math.Round(logAltAboveGndLast) || Math.Round(Model.tlod) != Math.Round(logTlodLast) || Math.Round(Model.SpikeTLODReductionAmount) != Math.Round(logSpikeTLODReductionAmountLast) || Math.Round(Model.olod) != Math.Round(logOlodLast) || logDecCloudQActiveLast != Model.DecCloudQActive || logNightTimeLast != Model.NightTime || logIsAppPriorityFPSLast != Model.IsAppPriorityFPS || Math.Round(Model.rangeMinTLOD) != Math.Round(logRangeMinTLODLast) || Model.VRAMProtInactive != logVRAMProtInactiveLast))))
		{
			array = new string[17]
			{
				"Mode:",
				GetGfxModeName(),
				$" FPS:{Math.Round(averageFPSRounded / (float)((Model.FgModeEnabled && !Model.ActiveWindowMSFS) ? Model.FgModeMultiplier : ((!Model.MsmModeEnabled) ? 1 : Model.MsmMultiplier)))}",
				(Model.simRate != 1f) ? $" SR:{Model.simRate}X" : "",
				(Model.NightMode[Model.activeProfile] <= 0) ? "" : (Model.NightTime ? " Night" : " Day"),
				" Pri:",
				(Model.TLODAutoMethod[Model.activeProfile] == 2) ? "ATLOD" : ((Model.TLODAutoMethod[Model.activeProfile] == 3) ? "FPSCap" : (Model.IsAppPriorityFPS ? "FPS" : "TLOD")),
				Model.MinTLODExtraActive ? ("+ " + Model.MinTLODExtraAmount) : "",
				(Model.SpikeTLODReductionAmount > 0f) ? $" SRed:{Model.SpikeTLODReductionAmount}" : "",
				$" TLOD:{Math.Round(Model.tlod)} TLODRng:",
				(Model.FreeTLOD[Model.activeProfile] ? (Model.MinTLOD[Model.activeProfile] + ((Model.MaxTLODHalvedNight[Model.activeProfile] && Model.NightTime && Model.MinTLOD[Model.activeProfile] == Model.MaxTLOD[Model.activeProfile] / 2f) ? "" : ("-" + Model.MaxTLOD[Model.activeProfile] / (float)((!Model.MaxTLODHalvedNight[Model.activeProfile] || !Model.NightTime) ? 1 : 2)))) : ((Model.rangeMinTLOD == Model.rangeMaxTLOD) ? ((object)Model.rangeMinTLOD) : (Model.rangeMinTLOD.ToString("F0") + "-" + Model.rangeMaxTLOD.ToString("F0"))))?.ToString(),
				Model.TLODExtraMtns[Model.activeProfile] ? $" Mtns+:{Model.TLODExtraMtnsAmountResidual}" : "",
				$" OLOD:{Math.Round(Model.olod)} AGL:{Math.Round(Model.altAboveGnd)} FPM:{Math.Round(vs * 60f)}",
				null,
				null,
				null,
				null
			};
			object obj;
			if (!Model.isMSFS2024)
			{
				obj = " Clouds:" + (Model.VrModeActive ? ServiceModel.SettingQualityText(Model.cloudQ_VR) : ServiceModel.SettingQualityText(Model.cloudQ)) + (Model.NightCloudReduceLevelsActive ? $"(-{(Model.VrModeActive ? (Model.DefaultCloudQ_VR - Model.cloudQ_VR) : (Model.DefaultCloudQ - Model.cloudQ))})" : "");
			}
			else if ((!Model.AutoReduceSettings[Model.activeProfile] && !Model.VRAMPlusEnabled) || (!(Model.MinTLODReductionAmount > 0f) && !Model.DynamicSettingsActive))
			{
				if (Model.NightCloudReduceLevelsActive)
				{
					obj = $" Clds:{ServiceModel.SettingQualityText(Model.VrModeActive ? Model.cloudQ_VR : Model.cloudQ)}(-{(Model.VrModeActive ? (Model.DefaultCloudQ_VR - Model.cloudQ_VR) : (Model.DefaultCloudQ - Model.cloudQ))})";
				}
				else
				{
					obj = ((Model.IncCloudQ[Model.activeProfile] && Model.IncCloudQActive) ? " Clouds+" : "");
				}
			}
			else
			{
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(6, 1);
				defaultInterpolatedStringHandler.AppendLiteral(" RLvl:");
				defaultInterpolatedStringHandler.AppendFormatted((Model.ReductionSettingsDepth > 0) ? ((object)Model.ReductionSettingsDepth) : ("LODs" + Math.Round((Model.MinTLOD[Model.activeProfile] - Model.MinTLODReductionAmount) / Model.MinTLOD[Model.activeProfile] * 100f) + "%"));
				obj = defaultInterpolatedStringHandler.ToStringAndClear();
			}
			array[13] = (string)obj;
			array[14] = ((Model.gpuUsage > 0f) ? $" GPU:{Math.Round(Model.gpuUsage)}%" : "");
			array[15] = ((Model.gpuMemoryUsage > 0) ? ($" VRAM:{Model.gpuMemoryUsage}%" + (Model.VRAMReduceActive ? " RED" : (Model.VRAMHoldActive ? " HLD" : ""))) : "");
			if (Model.LogPlus)
			{
				CpuSnapshot latestSnap = Model._latestSnap;
				if ((object)latestSnap != null)
				{
					text = $" CPU:{latestSnap.TotalCpuPercent:F0}% Dom:{((latestSnap.StableCoreIndex is int) ? (_domSmooth.ToString("F0") + "%(#" + latestSnap.StableCoreIndex + ")") : "-")} Top" + Model.perfLogTopCoreCount + $"Avg:{_topXSmooth:F0}% MSFS:{_msfsSmooth:F0}%{((Model.LastAppliedAffinityCount > 0) ? (" (" + Model.LastAppliedAffinityCount + " Cores)") : "")}";
					goto IL_109f;
				}
			}
			text = "";
			goto IL_109f;
		}
		if (--logTimer < 0)
		{
			logTimer = 0;
		}
		goto IL_116c;
		IL_116c:
		if (Model.LogPlus)
		{
			if (Model.PerfLogResetPending)
			{
				ResetPerfLogState();
				Model.PerfLogResetPending = false;
			}
			AddEntryPerfLog();
			OutputPerfLog(Model.PerfLoggingAlways);
		}
		return;
		IL_109f:
		array[16] = text;
		Logger.Log(LogLevel.Information, "LODController:UpdateVariables", string.Concat(array));
		logTimer = logTimerInterval;
		logAltAboveGndLast = Model.altAboveGnd;
		logTlodLast = Model.tlod;
		logSpikeTLODReductionAmountLast = Model.SpikeTLODReductionAmount;
		logOlodLast = Model.olod;
		logDecCloudQActiveLast = Model.DecCloudQActive;
		logIsAppPriorityFPSLast = Model.IsAppPriorityFPS;
		logRangeMinTLODLast = Model.rangeMinTLOD;
		logNightTimeLast = Model.NightTime;
		logVRAMProtInactiveLast = Model.VRAMProtInactive;
		goto IL_116c;
	}

	private void EnsurePerfBuffer()
	{
		int num = Math.Max(1, Model.PerfLogBufferSize);
		if (num == _bufferSizeCached)
		{
			return;
		}
		string[] array = new string[num];
		DateTime[] array2 = new DateTime[num];
		if (_buffer != null && _bufferSizeCached > 0)
		{
			int num2 = Math.Min(_bufferSizeCached, num);
			for (int i = 0; i < num2; i++)
			{
				int num3 = (_currentIndex - num2 + i + _bufferSizeCached) % _bufferSizeCached;
				int num4 = (num - num2 + i) % num;
				array[num4] = _buffer[num3];
				array2[num4] = _bufferTs[num3];
			}
			_currentIndex = num2 % num;
		}
		else
		{
			_currentIndex = 0;
		}
		_buffer = array;
		_bufferTs = array2;
		_bufferSizeCached = num;
		if (_lastOutputIndex >= _bufferSizeCached)
		{
			_lastOutputIndex = -1;
		}
	}

	private void AddEntryPerfLog()
	{
		if (!Model.LogPlus)
		{
			return;
		}
		CpuSnapshot latestSnap = Model._latestSnap;
		if ((object)latestSnap == null)
		{
			return;
		}
		if (!_seenFirstPerfSample)
		{
			_seenFirstPerfSample = true;
			return;
		}
		EnsurePerfBuffer();
		DateTime dateTime = DateTime.Now.AddMilliseconds(500.0);
		dateTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second);
		double num = 0.0;
		double num2 = averageFPSUnrounded / (float)((Model.FgModeEnabled && !Model.ActiveWindowMSFS) ? Model.FgModeMultiplier : ((!Model.MsmModeEnabled) ? 1 : Model.MsmMultiplier));
		try
		{
			if (deltaFPSTrendShort != null)
			{
				num = num2 - (double)Model.TargetFPS;
			}
		}
		catch
		{
			num = 0.0;
		}
		float value = (float)num;
		try
		{
			int? stableCoreIndex = latestSnap.StableCoreIndex;
			if (stableCoreIndex.HasValue)
			{
				int valueOrDefault = stableCoreIndex.GetValueOrDefault();
				_ = $"{latestSnap.StableInstantPercent:F0}%(#{valueOrDefault})";
			}
		}
		catch
		{
		}
		string text;
		try
		{
			double totalCpuPercent = latestSnap.TotalCpuPercent;
			double maxCorePercent = latestSnap.MaxCorePercent;
			_ = latestSnap.MaxCoreIndex;
			double msfsCpuPercent = latestSnap.MsfsCpuPercent;
			double num3 = Math.Round(Model.gpuUsage);
			int gpuMemoryUsage = Model.gpuMemoryUsage;
			_domSmooth = Ewma(_domSmooth, latestSnap.StableInstantPercent, Model.PerfLogEwmaAlpha);
			_maxSmooth = Ewma(_maxSmooth, maxCorePercent, Model.PerfLogEwmaAlpha);
			_msfsSmooth = Ewma(_msfsSmooth, msfsCpuPercent, Model.PerfLogEwmaAlpha);
			_topXSmooth = Ewma(_topXSmooth, latestSnap.TopXMetric, Model.PerfLogEwmaAlpha * 2.0);
			_topXDeltaSmooth = Ewma(_topXDeltaSmooth, latestSnap.TopXDelta, Model.PerfLogEwmaAlpha * 2.0);
			text = $"CPU:{totalCpuPercent:F0}% Dom:{_domSmooth:F0}/{latestSnap.StableInstantPercent:F0}%(#{latestSnap.StableCoreIndex}) MSFS:{_msfsSmooth:F0}/{msfsCpuPercent:F0}% Top" + Model.perfLogTopCoreCount + $"Avg:{_topXSmooth:F0}/{latestSnap.TopXMetric:F0}% " + ((num3 > 0.0) ? $"GPU:{num3}% VRAM:{gpuMemoryUsage}%" : "");
		}
		catch
		{
			text = "CPU:??% Dom:- Max:??% MSFS:??% TopX:??% GPU:??% VRAM:??%";
		}
		string text2 = $"FPS:{num2:F1}/{Model.fpsRaw:F1}" + ((((Model.ActiveGraphicsMode == "FG" && Model.FgModeMultiplier == 1) || (Model.ActiveGraphicsMode == "Man FG" && Model.ManFgMultiplier == 1)) && Model.fpsSourceRTSS) ? $"/{Model.fpsRawMSFS:F1}/{Model.DynFgCurrentMultiplier}X" : "") + (((Model.ActiveGraphicsMode == "Man FG" || Model.FgModeEnabled) && !Model.ActiveWindowMSFS) ? " " : $"/Δ{value:+0.0;-0.0;0.0} ") + $"TLOD:{Model.tlod}/{Model.recentTLODPeak:F0} " + $"AGL:{Math.Round(Model.altAboveGnd)} " + text;
		int num4 = (_currentIndex - 1 + _buffer.Length) % _buffer.Length;
		if (_bufferTs[num4] == dateTime)
		{
			_buffer[num4] = text2;
			_bufferTs[num4] = dateTime;
			_lastOutputIndex = ((_lastOutputIndex == -1) ? num4 : _lastOutputIndex);
			return;
		}
		_buffer[_currentIndex] = text2;
		_bufferTs[_currentIndex] = dateTime;
		int currentIndex = _currentIndex;
		_currentIndex = (_currentIndex + 1) % _buffer.Length;
		if (_lastOutputIndex == -1)
		{
			_lastOutputIndex = currentIndex;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double Ewma(double prev, double current, double alpha)
	{
		if (!double.IsNaN(prev))
		{
			return alpha * current + (1.0 - alpha) * prev;
		}
		return current;
	}

	private void OutputPerfLog(bool trigger)
	{
		EnsurePerfBuffer();
		if (trigger)
		{
			_perfTriggerCount = -_bufferSizeCached;
		}
		if (_perfTriggerCount == 0)
		{
			return;
		}
		List<(DateTime, int)> list = new List<(DateTime, int)>(_buffer.Length);
		for (int i = 0; i < _buffer.Length; i++)
		{
			DateTime dateTime = _bufferTs[i];
			if (!(dateTime == DateTime.MinValue) && !(dateTime <= _lastOutputTime))
			{
				list.Add((dateTime, i));
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		list.Sort(((DateTime ts, int idx) a, (DateTime ts, int idx) b) => a.ts.CompareTo(b.ts));
		bool flag = _perfTriggerCount < 0;
		IEnumerable<(DateTime, int)> source = list;
		if (flag)
		{
			int num = Math.Min(_bufferSizeCached, list.Count);
			source = list.Skip(list.Count - num);
		}
		List<(DateTime, int)> list2 = source.ToList();
		if (flag)
		{
			for (int num2 = 0; num2 < list2.Count; num2++)
			{
				(DateTime, int) tuple = list2[num2];
				string text = _buffer[tuple.Item2];
				if (!string.IsNullOrEmpty(text))
				{
					if (!Model.PerfLoggingAlways && num2 == list2.Count - 1)
					{
						text += " (T)";
					}
					else if (!Model.PerfLoggingAlways || num2 - list2.Count + 1 != 0)
					{
						text += $" (T{num2 - list2.Count + 1})";
					}
					Logger.Log(LogLevel.Debug, "LODController:OutputPerfLog", text);
					(_lastOutputTime, _lastOutputIndex) = tuple;
				}
			}
			_perfTriggerCount = _bufferSizeCached;
			return;
		}
		for (int num3 = 0; num3 < list2.Count; num3++)
		{
			if (_perfTriggerCount <= 0)
			{
				break;
			}
			(DateTime, int) tuple3 = list2[num3];
			string text2 = _buffer[tuple3.Item2];
			if (!string.IsNullOrEmpty(text2))
			{
				Logger.Log(LogLevel.Debug, "LODController:OutputPerfLog", text2);
				_lastOutputTime = tuple3.Item1;
				_lastOutputIndex = tuple3.Item2;
				_perfTriggerCount = Math.Max(0, _perfTriggerCount - 1);
			}
		}
	}

	public void ResetPerfLogState()
	{
		_buffer = null;
		_bufferTs = null;
		_currentIndex = 0;
		_lastOutputTime = DateTime.MinValue;
		_lastOutputIndex = -1;
		_perfTriggerCount = 0;
		_bufferSizeCached = 0;
		_domSmooth = double.NaN;
		_maxSmooth = double.NaN;
		_msfsSmooth = double.NaN;
		_topXSmooth = double.NaN;
		_topXDeltaSmooth = double.NaN;
		_seenFirstPerfSample = false;
		_triggerPendingInt = 0;
		_lastTriggerTimeRounded = DateTime.MinValue;
		Model.perfLogForFirstReseekDone = false;
		Model.perfLogForInitialSeekDone = false;
	}

	private void DetectFixedFPS(int clusterThreshold = 10, int maxOutliersAbove = 1)
	{
		float[] filledArray = Model.fpsRawTrend.GetFilledArray();
		if (filledArray == null || filledArray.Length < clusterThreshold)
		{
			Logger.Log(LogLevel.Warning, "LODController:DetectFixedFPS", $"Insufficient data: fpsSamples.Length = {filledArray?.Length ?? 0}, required = {clusterThreshold}");
			return;
		}
		Model.fixedFPSDetectAttempts++;
		string text = string.Join(", ", filledArray.Select((float f) => f.ToString("F1")));
		if (Model.LogPlus)
		{
			Logger.Log(LogLevel.Debug, "LODController:DetectFixedFPS", "RawFPS: " + text);
		}
		float binSize = Math.Clamp(filledArray.Average() * 0.01f, 0.1f, 1f);
		float minFps = filledArray.Min();
		int num = (int)Math.Ceiling((filledArray.Max() - minFps) / binSize) + 1;
		List<float>[] array = new List<float>[num];
		for (int num2 = 0; num2 < num; num2++)
		{
			array[num2] = new List<float>();
		}
		float[] array2 = filledArray;
		foreach (float num4 in array2)
		{
			int num5 = (int)((num4 - minFps) / binSize);
			array[num5].Add(num4);
		}
		var anon = (from b in array.Select((List<float> samples, int index) => new
			{
				BinCenter = minFps + ((float)index + 0.5f) * binSize,
				Samples = samples,
				Count = samples.Count
			})
			where b.Count >= 2
			orderby b.Count descending
			select b).FirstOrDefault();
		if (anon == null)
		{
			Logger.Log(LogLevel.Information, "LODController:DetectFixedFPS", $"Atmpt:{Model.fixedFPSDetectAttempts}. No DomFPS detected.");
			return;
		}
		float candidate = anon.BinCenter;
		float toleranceFinal = Math.Max(0.5f, candidate * 0.01f);
		float[] array3 = filledArray.Where((float f) => Math.Abs(f - candidate) <= toleranceFinal).ToArray();
		if (array3.Length != 0)
		{
			candidate = array3.Average();
		}
		float initialMean = ((array3.Length != 0) ? array3.Average() : candidate);
		float[] array4 = array3.Where((float f) => f >= initialMean).ToArray();
		float biasedMean = ((array4.Length != 0) ? array4.Average() : initialMean);
		float num6 = ((array4.Length != 0) ? ((float)Math.Sqrt(array4.Select((float f) => Math.Pow(f - biasedMean, 2.0)).Average())) : 0f);
		float dominantFPS = (float)Math.Round(candidate * 2f) / 2f;
		float num7 = toleranceFinal * 0.25f;
		int num8 = array3.Length;
		int num9 = filledArray.Count((float f) => f > dominantFPS + toleranceFinal);
		bool flag = num8 >= clusterThreshold && num9 <= maxOutliersAbove && num6 <= num7;
		int num10;
		if (Model.fpsSourceRTSS)
		{
			if (Model.LsModeEnabled)
			{
				num10 = Model.LsModeMultiplier;
			}
			else if (Model.MsmModeEnabled && Model.ActiveWindowMSFS)
			{
				num10 = Model.MsmMultiplier;
			}
			else
			{
				num10 = ((Model.VrModeActive && Model.OfxrModeEnabled && (!Model.OfxrStatusJsonActive || !(Model.OfxrOutputFps > 0f))) ? Model.OfxrModeMultiplier : ((!Model.VrModeActive || !Model.VxrModeEnabled || (Model.VxrStatusJsonActive && Model.VxrOutputFps > 0f)) ? 1 : Model.VxrModeMultiplier));
			}
		}
		else if (Model.VrModeActive)
		{
			num10 = ((Model.OfxrModeEnabled && (!Model.OfxrStatusJsonActive || !(Model.OfxrOutputFps > 0f))) ? Model.OfxrModeMultiplier : ((!Model.VxrModeEnabled || (Model.VxrStatusJsonActive && Model.VxrOutputFps > 0f)) ? 1 : Model.VxrModeMultiplier));
		}
		else if (Model.ActiveGraphicsMode == "Man FG")
		{
			num10 = Model.ManFgMultiplier;
		}
		else if (Model.LsModeEnabled)
		{
			num10 = Model.LsModeMultiplier;
		}
		else if (Model.FgModeEnabled && Model.ActiveWindowMSFS)
		{
			num10 = Model.FgModeMultiplier;
		}
		else
		{
			num10 = (Model.FgModeFSREnabled ? 2 : ((!Model.MsmModeEnabled || !Model.ActiveWindowMSFS) ? 1 : Model.MsmMultiplier));
		}
		dominantFPS *= num10;
		if (Model.LogPlus)
		{
			Logger.Log(LogLevel.Debug, "LODController:DetectFixedFPS", $"Atmpt:{Model.fixedFPSDetectAttempts} DomFPS:{dominantFPS} Tol:±{toleranceFinal:F2} SD+:{num6:F3} SDMax:{num7:F3} In:{num8} Out+:{num9} FixedFPS:" + (flag ? "Y" : "N"));
		}
		if (flag)
		{
			Logger.Log(LogLevel.Debug, "LODController:DetectFixedFPS", $"Fixed FPS detected at {dominantFPS} FPS");
		}
		else if (Model.fixedFPSDetectAttempts >= 5)
		{
			Logger.Log(LogLevel.Information, "LODController:DetectFixedFPS", $"Fixed FPS detection failed after {5} attempts.");
		}
		Model.fixedFPSDetected = ((flag && dominantFPS >= 10f) ? dominantFPS : (-1f));
		if (!Model.AutoTargetFPS)
		{
			return;
		}
		if (Model.fixedFPSDetected > 0f && Model.fixedFPSDetectHistoryIndex < Model.fixedFPSDetectHistory.Length)
		{
			Model.fixedFPSDetectedAutoTargetFPS = true;
			Model.fixedFPSDetectHistory[Model.fixedFPSDetectHistoryIndex] = Model.fixedFPSDetected;
			if (Model.fixedFPSDetectHistoryIndex > 0 && !Model.fixedFPSDetectHistory.Take(Model.fixedFPSDetectHistoryIndex + 1).All((float x) => x == Model.fixedFPSDetectHistory[0]))
			{
				Model.fixedFPSDetectHistoryIndex = Model.fixedFPSDetectHistory.Length;
				Model.ForceAutoFPSCal = true;
				Model.fixedFPSDetectedAutoTargetFPS = false;
				ResetFixedFPSDetection();
				Logger.Log(LogLevel.Information, "LODController:DetectFixedFPS", "Auto Target Fixed FPS history inconsistent — reverting to normal Auto Target FPS logic.");
				return;
			}
			if (Model.fixedFPSDetectHistoryIndex == 0 && Model.fixedFPSDetected != Model.TargetFPS)
			{
				Model.TargetFPS = Model.fixedFPSDetected;
				Model.UpdateTargetFPS = true;
				Model.ForceAutoFPSCal = false;
				Logger.Log(LogLevel.Information, "LODController:DetectFixedFPS", $"Auto Target Fixed FPS detected at {Model.TargetFPS} FPS (override applied)");
			}
			Model.fixedFPSDetectHistoryIndex++;
			if (Model.fixedFPSDetectHistoryIndex < Model.fixedFPSDetectHistory.Length)
			{
				Logger.Log(LogLevel.Debug, "LODController:DetectFixedFPS", $"Auto Target Fixed FPS detection {Model.fixedFPSDetectHistoryIndex}/{Model.fixedFPSDetectHistory.Length}: confirmed consistent at {Model.fixedFPSDetected} FPS");
				ResetFixedFPSDetection();
			}
			else
			{
				Logger.Log(LogLevel.Information, "LODController:DetectFixedFPS", $"Auto Target Fixed FPS confirmed stable after {Model.fixedFPSDetectHistoryIndex} identical detections.");
			}
		}
		else if (Model.fixedFPSDetected <= 0f && Model.fixedFPSDetectAttempts >= 5 && Model.fixedFPSDetectHistoryIndex < Model.fixedFPSDetectHistory.Length)
		{
			Model.fixedFPSDetectHistoryIndex = Model.fixedFPSDetectHistory.Length;
			Model.ForceAutoFPSCal = true;
			Model.fixedFPSDetectedAutoTargetFPS = false;
			ResetFixedFPSDetection();
			Logger.Log(LogLevel.Information, "LODController:DetectFixedFPS", "Auto Target Fixed FPS no longer detected — reverting to normal Auto Target FPS logic.");
		}
	}

	private void ResetFixedFPSDetection()
	{
		Model.fpsRawTrend.Reset();
		Model.fixedFPSDetected = -1f;
		Model.fixedFPSDetectAttempts = 0;
	}

	public void RunTick()
	{
		UpdateVariables();
		if (Model.ResetInProgress || Model.MonitorMode)
		{
			return;
		}
		float num = (deltaFPSTrendShort[deltaFPSTrendShortIndex] = (float)Math.Round(averageFPSUnrounded - Model.TargetFPS + (FGInactive ? (Model.TargetFPS * 0.025f) : 0f), 1));
		float num2 = (float)Math.Round(averageFPSUnroundedRaw - Model.TargetFPS + (FGInactive ? (Model.TargetFPS * 0.025f) : 0f), 1);
		if (++deltaFPSTrendShortIndex >= deltaFPSTrendShort.Length)
		{
			deltaFPSTrendShortIndex = 0;
		}
		float num3 = Model.TargetFPS * (float)Model.FPSTolerance[Model.activeProfile] / 100f;
		if (Model.AutoTargetFPS && !Model.fixedFPSDetectedAutoTargetFPS)
		{
			if (Model.OnGround && Model.groundSpeed < 1f && AutoFPSInFlightPhase)
			{
				AutoFPSInFlightPhase = false;
				Model.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
				Logger.Log(LogLevel.Information, "LODController:RunTick", "Recalibrating Auto Target FPS on arrival at new location");
			}
			else if (Model.altAboveGnd >= Model.AltTLODBase[Model.activeProfile])
			{
				AutoFPSInFlightPhase = true;
			}
		}
		ReduceSettingsRecoveryEnabled = (Model.AutoReduceSettings[Model.activeProfile] || Model.VRAMSettingsReduceActive) && (Model.MinTLODReductionAmount > 0f || Model.DynamicSettingsActive) && (Model.ReductionSettingsRecoveryAlt[Model.activeProfile] == 0 || (Model.VRAMSettingsReduceActive && Model.VRAMSettingsReduceAlt == 0) || ((Model.ReductionSettingsRecoveryAlt[Model.activeProfile] == 1 || (Model.VRAMSettingsReduceActive && Model.VRAMSettingsReduceAlt == 1)) && Model.altAboveGnd > Model.AltTLODBase[Model.activeProfile]) || ((Model.ReductionSettingsRecoveryAlt[Model.activeProfile] == 2 || (Model.VRAMSettingsReduceActive && Model.VRAMSettingsReduceAlt == 2)) && Model.altAboveGnd > Model.AltTLODTop[Model.activeProfile]));
		if (Model.MinTLODExtra[Model.activeProfile] && (!Model.MaxTLODHalvedNight[Model.activeProfile] || (!Model.NightTime && !Model.NightTimeTransiton)))
		{
			if (Model.TLODAutoMethod[Model.activeProfile] == 3)
			{
				if (Model.altAboveGnd > Model.AltTLODTop[Model.activeProfile] + 100f && !Model.AboveAltTLODTopPlusTolerance)
				{
					MinTLODExtraReSeek = true;
					Model.MinTLODExtraSeekingInitial = true;
					Model.MinTLODExtraIterations = 50 / Model.FPSTolerance[Model.activeProfile] * 8;
					Logger.Log(LogLevel.Information, "LODController:RunTick", "Activating TLOD Base Extra " + (Model.TLODBaseMinus[Model.activeProfile] ? "" : "re-") + "seek process on climbing through Alt TLOD Top");
					Model.AboveAltTLODTopPlusTolerance = true;
				}
				else if (Model.altAboveGnd < Model.AltTLODTop[Model.activeProfile] - 100f && Model.AboveAltTLODTopPlusTolerance)
				{
					Logger.Log(LogLevel.Information, "LODController:RunTick", (Model.TLODBaseMinus[Model.activeProfile] ? "Deactivating" : "Reducing") + " TLOD Base Extra on descent through Alt TLOD Top");
					Model.AboveAltTLODTopPlusTolerance = false;
					Model.MinTLODExtraSeekingInitial = false;
					MinTLODExtraReSeek = false;
					Model.MinTLODExtraIterations = 1;
					MinTLODExtraSeekLastTickMark = TotalTicks;
				}
			}
			if (Model.TLODAutoMethod[Model.activeProfile] == 3)
			{
				if (Model.altAboveGnd >= Model.AltTLODBase[Model.activeProfile])
				{
					Model.AboveAltTLODBase = true;
				}
				else if (Model.OnGround && Model.groundSpeed < 1f && Model.AboveAltTLODBase)
				{
					Model.AboveAltTLODBase = false;
					Model.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
					Logger.Log(LogLevel.Information, "LODController:RunTick", "Recalibrating TLOD Base Extra on arrival at new location");
				}
			}
		}
		if (Model.altAboveGnd > Model.AltTLODTop[Model.activeProfile])
		{
			Model.LockTLODMax = true;
		}
		else if (Model.OnGround || SimConnect.ReadSimVar("GEAR POSITION", "percent") == 100f)
		{
			Model.LockTLODMax = false;
		}
		if (Model.TLODAutoMethod[Model.activeProfile] == 2)
		{
			Model.FPSSettleActive = false;
		}
		else if (Model.FPSSettleActive && !Model.AppPaused)
		{
			if (!Model.FPSSettleActiveLast)
			{
				deltaFPSTrendIndex = 0;
				deltaFPSLowest = 100f;
				deltaFPSHighest = -100f;
				deltaFPSRangeLowest = 100f;
			}
			deltaFPSTrend[deltaFPSTrendIndex] = num;
			if (Model.MinTLODExtraSeeking && !FGInactive)
			{
				if (Model.TLODAutoMethod[Model.activeProfile] >= 2 && Model.MinTLODExtra[Model.activeProfile])
				{
					if (deltaFPSTrendIndex >= (Model.VrModeActive ? 9 : 4) && deltaFPSTrend.Skip(deltaFPSTrendIndex - 4).Take(5).Min() > deltaFPSTrendMin)
					{
						Model.MinTLODExtraSeeking = false;
					}
					else if (deltaFPSTrendIndex >= ((Model.MinTLODExtraIterations > 1) ? 14 : 9))
					{
						if (deltaFPSTrend.Skip(deltaFPSTrendIndex - 4).Take(5).Min() <= deltaFPSTrendMin && Model.MinTLODExtraIterations > 1)
						{
							Model.MinTLODExtraIterations = 1;
						}
						Model.MinTLODExtraSeeking = false;
					}
				}
				else if (deltaFPSTrendIndex >= 1)
				{
					if (deltaFPSTrendIndex >= 4 && deltaFPSTrend.Skip(deltaFPSTrendIndex - 4).Take(5).Max() - deltaFPSTrend.Skip(deltaFPSTrendIndex - 4).Take(5).Min() <= 1f)
					{
						Model.FPSSettleActive = false;
					}
					else if (deltaFPSLowest > num)
					{
						deltaFPSLowest = num;
					}
					else if ((double)deltaFPSHighest < (double)num + 0.5)
					{
						deltaFPSHighest = num;
					}
					else
					{
						Model.FPSSettleActive = false;
					}
				}
			}
			else if (!Model.FPSSettleInitial && deltaFPSTrendIndex >= 4 + ((Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite == 1) ? 4 : ((Model.MinTLODReductionAmount > 0f || Model.ReductionSettingsDepth > 0) ? 2 : 0)))
			{
				deltaFPSRange = deltaFPSTrend.Skip(deltaFPSTrendIndex - 4).Take(5).Max() - deltaFPSTrend.Skip(deltaFPSTrendIndex - 4).Take(5).Min();
				if (deltaFPSRangeLowest > deltaFPSRange)
				{
					deltaFPSRangeLowest = deltaFPSRange;
				}
				else if (((double)deltaFPSRange < 0.05 * (double)Model.TargetFPS || Model.ForceAutoFPSCal) && (!Model.OfxrModeEnabled || !Model.OfxrDesynced))
				{
					Model.FPSSettleActive = false;
				}
			}
			if (Model.FPSSettleInitial)
			{
				Model.FPSSettleCount = 30 - deltaFPSTrendIndex - 1;
			}
			if (++deltaFPSTrendIndex >= deltaFPSTrend.Length)
			{
				deltaFPSTrendIndex = 0;
				Model.FPSSettleActive = false;
				Model.FPSSettleCount = 30;
				Model.MinTLODExtraSeeking = false;
				Model.FPSSettleInitial = false;
			}
		}
		if (Model.FgModeEnabled && Model.ActiveWindowMSFS && !Model.FPSSettleActive && Model.FPSSettleActiveLast)
		{
			Model.fpsRawTrend.Reset();
		}
		if (Model.OfxrModeEnabled && !Model.FPSSettleActive && Model.FPSSettleActiveLast)
		{
			Model.fpsRawTrend.Reset();
			Model.fpsAverageReset = true;
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Debug, "LODController:RunTick", "OFXR mode active, resetting FPS trend");
			}
		}
		Model.FPSSettleActiveLast = Model.FPSSettleActive;
		if (!Model.AppPaused && !Model.FPSSettleInitial && !Model.FPSSettleActive && !FGInactive && ((Model.OnGround && Model.groundSpeed < 1f) || Model.altAboveGnd > Model.AltTLODBase[Model.activeProfile]) && (TotalTicks % Math.Min(20, 30) == 0L || Model.fixedFPSDetectAttempts == 0) && Model.fpsRawTrend.IsFull && Model.fixedFPSDetected == -1f && Model.fixedFPSDetectAttempts < 5)
		{
			DetectFixedFPS();
		}
		Model.FrameTimeNoiseLevel = 0f;
		if (Model.SpikeDetectEnabled && Model.TLODAutoMethod[Model.activeProfile] == 0)
		{
			if (Model.FPSSettleActive)
			{
				Model.SpikeDetectProtStatus = "Awaiting FPS settling";
			}
			else
			{
				Model.GetRTSSFrameTimes();
				if (Model.RTSSFrameTimes.Length != 0)
				{
					RTSSUnavailableMessageShown = false;
					Model.DetectPeriodicSpikes();
					if (Model.SpikeTLODReductionEnabled && Model.MinTLODReductionAmount == 0f && Model.ReductionSettingsDepth == 0)
					{
						int num4 = ((!Model.tlod_increased) ? 1 : 2);
						if (num4 == 0)
						{
							spikeVerificationLastDetected = Model.SpikeDetected;
						}
						else if (Model.SpikeVerificationActive)
						{
							if (TotalTicks - spikeVerificationStartTick < num4)
							{
								spikeVerificationLastDetected = Model.SpikeDetected;
								Model.SpikeDetected = false;
							}
							else
							{
								Model.SpikeVerificationActive = false;
								if (!Model.SpikeDetected)
								{
									spikeVerificationLastDetected = false;
								}
							}
						}
						else if (Model.SpikeDetected && !spikeVerificationLastDetected)
						{
							Model.SpikeVerificationActive = true;
							spikeVerificationStartTick = TotalTicks;
							spikeVerificationLastDetected = true;
							Model.SpikeDetected = false;
						}
						else
						{
							spikeVerificationLastDetected = Model.SpikeDetected;
						}
						if (Model.SpikeDetected && Model.SpikeTLODReductionAmount < Model.MaxTLOD[Model.activeProfile] - Model.MinTLOD[Model.activeProfile])
						{
							if (Model.ActiveWindowMSFS)
							{
								float spikeTLODReductionAmount = Model.SpikeTLODReductionAmount;
								if (Model.tlod < Model.MaxTLOD[Model.activeProfile] && Model.SpikeTLODReductionAmount == 0f)
								{
									Model.SpikeTLODReductionAmount = Model.MaxTLOD[Model.activeProfile] - Model.tlod;
								}
								Model.MinTLODExtraAmount = Math.Max(0f, Model.MinTLODExtraAmount - (float)(4 * Model.FPSTolerance[Model.activeProfile]));
								Model.SpikeTLODReductionAmount = Math.Min(Model.SpikeTLODReductionAmount + (float)(4 * Model.FPSTolerance[Model.activeProfile]), Model.MaxTLOD[Model.activeProfile] - Model.MinTLOD[Model.activeProfile]);
								if (spikeTLODReductionAmount == 0f || SpikeRecoveryActive)
								{
									Logger.Log(LogLevel.Information, "LODController:RunTick", $"Automatic periodic spike TLOD reduction activated @ TLOD {Model.tlod}");
								}
								Model.SpikeTLODReductionTickMark = TotalTicks;
								LastSpikeReductionLat = SimConnect.ReadSimVar("PLANE LATITUDE", "degrees");
								LastSpikeReductionLon = SimConnect.ReadSimVar("PLANE LONGITUDE", "degrees");
								SpikeRecoveryActive = false;
							}
							else
							{
								Model.SpikeDetectProtStatus += " ⏸";
							}
						}
						else if (Model.SpikeTLODReductionAmount > 0f)
						{
							if (TotalTicks - Model.SpikeTLODReductionTickMark >= Model.SpikeTLODReductionRecoveryDelay)
							{
								double num5 = DistanceNM(LastSpikeReductionLat, LastSpikeReductionLon, SimConnect.ReadSimVar("PLANE LATITUDE", "degrees"), SimConnect.ReadSimVar("PLANE LONGITUDE", "degrees"));
								float num6 = Model.FPSTolerance[Model.activeProfile];
								float num7 = (float)Math.Min(1.0, Math.Max(0.0, (num5 - 1.5) / 3.5));
								float num8 = (Model.FreeTLOD[Model.activeProfile] ? 500f : Model.AltTLODBase[Model.activeProfile]);
								float num9 = (Model.FreeTLOD[Model.activeProfile] ? 3000f : Model.AltTLODTop[Model.activeProfile]);
								float num10 = Math.Min(1f, Math.Max(0f, (Model.altAboveGnd - num8) / (num9 - num8)));
								float num11 = Math.Min(1f, Math.Max(0f, (float)(Model.VerticalTrend + 1) / 6f));
								float num12 = Math.Min(1f, Math.Max(0f, (Model.altAboveGnd - num8) / (num9 - num8)));
								float num13 = num11 * (1f - num12) + 1f * num12;
								float num14 = num7 * num10 * num13;
								num6 = (float)Math.Round(num6 * num14);
								if (Model.LogPlus)
								{
									Logger.Log(LogLevel.Information, "LODController:RunTick", $"SpikeRecoveryCalc distNM={num5:F2} distFactor={num7:F3} altFactor={num10:F3} trendFactorRaw={num11:F3} altitudeBlend={num12:F3} trendFactor={num13:F3} stepMult={num14:F3} step={num6}");
								}
								if (num6 > 0f)
								{
									if (!SpikeRecoveryActive)
									{
										Logger.Log(LogLevel.Information, "LODController:RunTick", "Automatic periodic spike TLOD reduction recovery activated ");
									}
									SpikeRecoveryActive = true;
									Model.SpikeDetectProtStatus = $"Spike recovery TLOD +{num6}";
									if (Model.LogPlus)
									{
										Logger.Log(LogLevel.Information, "LODController:RunTick", Model.SpikeDetectProtStatus);
									}
									Model.SpikeTLODReductionAmount = Math.Max(0f, (float)Math.Round(Model.SpikeTLODReductionAmount - num6));
								}
							}
							else if (Model.LogPlus && (Model.SpikeTLODReductionRecoveryDelay - (TotalTicks - Model.SpikeTLODReductionTickMark)) % 5 == 0L)
							{
								Logger.Log(LogLevel.Information, "LODController:RunTick", $"SpikeRecoveryPending secondsRemaining={Model.SpikeTLODReductionRecoveryDelay - (TotalTicks - Model.SpikeTLODReductionTickMark)}");
							}
						}
					}
				}
				else
				{
					if (Model.GetRTSSFrameTimes(record: false, 10f, checkOnly: true))
					{
						Model.SpikeDetectProtStatus = "RTSS FT unavailable";
						if (!RTSSUnavailableMessageShown)
						{
							Logger.Log(LogLevel.Warning, "LODController:RunTick", "RTSS frame time data unavailable for periodic spike detection");
						}
					}
					else
					{
						Model.SpikeDetectProtStatus = "RTSS not running";
						if (!RTSSUnavailableMessageShown)
						{
							Logger.Log(LogLevel.Warning, "LODController:RunTick", "RTSS not running, so periodic spike detection is unavailable.");
						}
					}
					RTSSUnavailableMessageShown = true;
				}
			}
		}
		bool flag3;
		float num26;
		if (!Model.AppPaused && (!Model.OfxrModeEnabled || !Model.IsOFXRPaused()) && (!Model.FPSSettleActive || (Model.TLODAutoMethod[Model.activeProfile] >= 2 && !Model.MinTLODExtra[Model.activeProfile] && !Model.VRAMPlusEnabled)))
		{
			FPSToleranceExceeded = Math.Abs(num) >= num3;
			if (Model.AutoTargetFPS)
			{
				float mean = deltaFPSTrendShort.Average();
				float num15 = deltaFPSTrendShort.Select((float val) => (val - mean) * (val - mean)).Average();
				if (!FGInactive && num15 < 5f && (Model.ForceAutoFPSCal || (!FGInactive && deltaFPSTrendShort.Average() <= (0f - num3) * 2f && (!Model.DecCloudQ[Model.activeProfile] || Model.DecCloudQActive) && Model.tlod == Model.activeMinTLOD)))
				{
					if (!Model.ForceAutoFPSCal && Model.MinTLODExtra[Model.activeProfile] && Model.MinTLODExtraActive)
					{
						Model.MinTLODExtraActive = false;
						Model.MinTLODExtraAmount = 0f;
					}
					else
					{
						Model.TargetFPS = (int)(averageFPSRounded * ((Model.FlightTypeIFR ? 0.95f : 0.9f) - Math.Min(0.1f, Model.altAboveGnd / 30000f)));
						Model.UpdateTargetFPS = true;
						Model.ForceAutoFPSCal = false;
						if (deltaFPSTrendShort.Average() <= (0f - num3) * 2f && num15 < 5f && (!Model.DecCloudQ[Model.activeProfile] || Model.DecCloudQActive) && Model.tlod == Model.activeMinTLOD)
						{
							Logger.Log(LogLevel.Information, "LODController:RunTick", "FPS too " + ((Model.tlod == Model.activeMinTLOD) ? "low" : "high") + " for current auto target FPS. " + (Model.fixedFPSDetectedAutoTargetFPS ? "Fixed FPS no longer valid. " : "") + "Auto Target FPS updated to " + $"{Model.TargetFPS}");
						}
						else
						{
							Logger.Log(LogLevel.Information, "LODController:RunTick", "Auto Target FPS set to " + $"{Model.TargetFPS}");
						}
						Model.fixedFPSDetectedAutoTargetFPS = false;
						Array.Fill(deltaFPSTrendShort, num);
						num3 = Model.TargetFPS * (float)Model.FPSTolerance[Model.activeProfile] / 100f;
					}
				}
			}
			if (Model.MinTLODExtra[Model.activeProfile] && Model.gpuMemoryUsage >= Model.VRAMOverflowHoldTLOD)
			{
				if (Model.TLODAutoMethod[Model.activeProfile] >= 2)
				{
					if (Model.MinTLODExtraIterations > 1)
					{
						Model.MinTLODExtraSeekingInitial = false;
						Model.MinTLODExtraIterations = 0;
					}
					MinTLODExtraReSeek = false;
					Model.MinTLODExtraSeeking = false;
					MinTLODExtraSeekLastTickMark = TotalTicks;
				}
				else
				{
					Model.MinTLODExtraIterations = 1;
				}
			}
			if (Model.TLODAutoMethod[Model.activeProfile] >= 2)
			{
				bool flag = deltaFPSTrendShort.Average() <= deltaFPSTrendMin || (MinTLODExtraReSeek && num < deltaFPSTrendMin / 5f);
				if (Model.MinTLODExtra[Model.activeProfile] && !FGInactive && (Model.DecCloudQMethod[Model.activeProfile] == 1 || !Model.DecCloudQActive) && (!Model.MaxTLODHalvedNight[Model.activeProfile] || !Model.NightTime || Model.MinTLODReductionAmount > 0f || Model.DynamicSettingsActive))
				{
					float num16 = (float)((Model.TLODBaseMinus[Model.activeProfile] || Model.AboveAltTLODTopPlusTolerance || !Model.OnGround || !(Model.groundSpeed < 1f)) ? 1 : 2) * Model.TLODBasePlusSeekStepSize;
					if (Model.OnGround && Model.groundSpeed < 1f && !Model.TLODBaseMinus[Model.activeProfile])
					{
						float num17 = Model.MinTLOD[Model.activeProfile];
						float num18 = Model.MinTLODExtraCapMult[Model.activeProfile];
						float minTLODExtraAmount = Model.MinTLODExtraAmount;
						float num19 = num17 * (num18 - 1f);
						float num20 = Model.TLODBasePlusSeekMinHeadroom * (float)(int)Math.Round((double)num17 * ((double)num18 / 2.0) / (double)Model.TLODBasePlusSeekMinHeadroom);
						if (minTLODExtraAmount < num19)
						{
							float num21 = num19 - minTLODExtraAmount;
							num16 = ((num21 >= num20) ? num20 : num21);
						}
						else
						{
							num16 = Model.TLODBasePlusSeekMinHeadroom;
						}
					}
					float num22;
					if (Model.AboveAltTLODTopPlusTolerance)
					{
						if (Model.MaxTLODExtra[Model.activeProfile])
						{
							num22 = ((Model.MaxTLOD[Model.activeProfile] * Model.MaxTLODExtraCapMult[Model.activeProfile] <= Model.SimMaxLOD) ? (Model.MaxTLOD[Model.activeProfile] * (Model.MaxTLODExtraCapMult[Model.activeProfile] - 1f)) : (Model.SimMaxLOD - Model.MaxTLOD[Model.activeProfile]));
						}
						else
						{
							num22 = Model.MinTLOD[Model.activeProfile] * (Model.MinTLODExtraCapMult[Model.activeProfile] - 1f);
						}
					}
					else
					{
						num22 = ((Model.TLODBaseMinus[Model.activeProfile] && !ReduceSettingsRecoveryEnabled) ? 0f : Math.Min(Model.MinTLOD[Model.activeProfile] * (Model.MinTLODExtraCapMult[Model.activeProfile] - 1f), Model.SimMaxLOD - Model.MinTLOD[Model.activeProfile]));
					}
					Model.TLODBasePlusSeekHeadroom = Math.Max(Model.TLODBasePlusSeekMinHeadroom, Math.Min(Model.AboveAltTLODTopPlusTolerance ? (Model.MaxTLOD[Model.activeProfile] * Model.MaxTLODExtraCapMult[Model.activeProfile]) : (Model.MinTLOD[Model.activeProfile] * Model.MinTLODExtraCapMult[Model.activeProfile]), Model.SimMaxLOD) * 0.1f);
					if (Model.IncCloudQ[Model.activeProfile] && Model.gpuUsage > 0f)
					{
						if (!Model.IncCloudQActive && !flag && (Model.VrModeActive ? Model.cloudQ_VR : Model.cloudQ) < 3 && Model.gpuUsage <= Model.CloudIncreaseGPUMaxLoad - 10f && Model.ReductionSettingsDepth == 0 && Model.MinTLODReductionAmount == 0f && !Model.VRAMSettingsReduceActive)
						{
							AutoIncreaseCloudQuality();
						}
						else if ((Model.IncCloudQActive & flag) && Model.gpuUsage > Model.CloudIncreaseGPUMaxLoad)
						{
							AutoDecreaseCloudQuality();
						}
					}
					if (Model.MinTLODExtraIterations == 1 || ((flag || (Model.TLODBaseMinus[Model.activeProfile] && Model.TLODAutoMethod[Model.activeProfile] == 3 && !Model.AboveAltTLODTopPlusTolerance && Model.MinTLODReductionAmount == 0f && !Model.DynamicSettingsActive)) && Model.MinTLODExtraAmount > 0f))
					{
						if (Model.IncCloudQ[Model.activeProfile] && !Model.IncCloudQActive && Model.MinTLODExtraIterations == 1 && !flag && (Model.VrModeActive ? Model.cloudQ_VR : Model.cloudQ) < 3 && Model.MinTLODExtraAmount >= num22 + ((!Model.AboveAltTLODTopPlusTolerance) ? Model.TLODBasePlusSeekHeadroom : ((Model.MinTLODExtraSeekingInitial || !MinTLODExtraReSeek) ? Math.Max(Model.TLODBasePlusSeekHeadroom, Model.MaxTLOD[Model.activeProfile] / 4f) : ((float)Model.FPSTolerance[Model.activeProfile]))) - (float)Model.FPSTolerance[Model.activeProfile])
						{
							AutoIncreaseCloudQuality();
						}
						else if ((Model.IncCloudQActive & flag) && Model.gpuUsage <= 0f)
						{
							AutoDecreaseCloudQuality();
						}
						else
						{
							float minTLODExtraAmount2 = Model.MinTLODExtraAmount;
							bool flag2 = false;
							MinTLODExtraMaxSeekAchieved = Math.Max(MinTLODExtraMaxSeekAchieved, Model.MinTLODExtraAmount);
							if (flag)
							{
								Model.MinTLODExtraAmount -= (float)Math.Round((double)Model.TLODBasePlusSeekHeadroom * (MinTLODExtraReSeek ? 0.2 : ((double)(Math.Abs(deltaFPSTrendShort.Average() / deltaFPSTrendMin) / 5f)))) * 5f;
								if (Model.MinTLODExtraAmount > num22)
								{
									Model.MinTLODExtraAmount = num22;
								}
							}
							else if (Model.AboveAltTLODTopPlusTolerance || Model.MinTLODExtraSeekingInitial)
							{
								Model.MinTLODExtraAmount -= num16;
							}
							else
							{
								float num23 = MinTLODExtraMaxSeekAchieved - num22;
								float num24 = ((Model.altAboveGnd < Model.AltTLODBase[Model.activeProfile]) ? 0f : ((float)Math.Pow((Model.altAboveGnd - Model.AltTLODBase[Model.activeProfile]) / (Model.AltTLODTop[Model.activeProfile] - Model.AltTLODBase[Model.activeProfile]), Model.LOSCorrection[Model.activeProfile] ? 0.5f : 1f)));
								float num25 = (float)Math.Round(num23 * num24 / (float)Model.FPSTolerance[Model.activeProfile]) * (float)Model.FPSTolerance[Model.activeProfile] + num22;
								if (Model.MinTLODExtraAmount - num16 > num25)
								{
									Model.MinTLODExtraAmount = num25;
								}
								else
								{
									Model.MinTLODExtraAmount -= num16;
								}
							}
							if (Model.MinTLODExtraAmount > 0f)
							{
								if ((num16 > (float)Model.FPSTolerance[Model.activeProfile]) | flag)
								{
									Model.FPSSettleActive = true;
								}
								if (Model.MinTLODExtraIterations != 1)
								{
									Model.MinTLODExtraSeeking = true;
								}
							}
							else
							{
								Model.MinTLODExtraAmount = 0f;
							}
							if ((!MinTLODExtraReSeek && !Model.MinTLODExtraSeekingInitial && minTLODExtraAmount2 > 0f) & flag)
							{
								flag2 = true;
								Logger.Log(LogLevel.Information, "LODController:RunTick", $"TLOD Base Extra auto-reduced for FPS Cap breach at {Model.TargetFPS + deltaFPSTrendShort.Average():F1} FPS (5s avg) to {Model.MinTLODExtraAmount}");
							}
							if (Model.MinTLODExtraAmount == 0f || Model.MinTLODExtraAmount <= num22)
							{
								if (MinTLODExtraReSeek)
								{
									Logger.Log(LogLevel.Information, "LODController:RunTick", "TLOD Base Extra set to " + $"{Model.MinTLODExtraAmount} after periodic re-seek");
									if (Model.LogPlus && !Model.perfLogForFirstReseekDone)
									{
										OutputPerfLog(trigger: true);
									}
									Model.perfLogForFirstReseekDone = true;
								}
								else if (Model.MinTLODExtraSeekingInitial)
								{
									Logger.Log(LogLevel.Information, "LODController:RunTick", "TLOD Base Extra set to " + $"{Model.MinTLODExtraAmount} after initial seek");
									if (Model.LogPlus && !Model.perfLogForInitialSeekDone)
									{
										OutputPerfLog(trigger: true);
									}
									Model.perfLogForInitialSeekDone = true;
								}
								else if (minTLODExtraAmount2 == 0f)
								{
									Logger.Log(LogLevel.Information, "LODController:RunTick", "TLOD Base Extra already at 0, so no reduction required");
								}
								else if (Model.MinTLODExtraAmount <= num22 && !flag2)
								{
									Logger.Log(LogLevel.Information, "LODController:RunTick", "TLOD Base Extra auto-reduced below Alt TLOD Top to " + $"{Model.MinTLODExtraAmount}");
								}
								MinTLODExtraMaxSeekAchieved = 0f;
								Model.MinTLODExtraIterations = 0;
								Model.MinTLODExtraSeekingInitial = false;
								MinTLODExtraReSeek = false;
								Model.MinTLODExtraSeeking = false;
							}
							MinTLODExtraSeekLastTickMark = TotalTicks;
							SetActiveLODs();
						}
					}
					else if ((!criticalFlightPhaseActive && MinTLODExtraReSeek) || (!Model.MinTLODExtraActive && Model.MinTLODExtraIterations > 1 && deltaFPSTrendShort.Average() >= deltaFPSTrendMin / 5f && num >= deltaFPSTrendMin / 5f))
					{
						if (Model.MinTLODExtraAmount + (MinTLODExtraReSeek ? ((float)Model.FPSTolerance[Model.activeProfile]) : num16) > num22 + ((!Model.AboveAltTLODTopPlusTolerance) ? ((!Model.TLODBaseMinus[Model.activeProfile] && Model.MinTLODExtraAmount >= Model.MinTLOD[Model.activeProfile] * (Model.MinTLODExtraCapMult[Model.activeProfile] - 1f)) ? Model.TLODBasePlusSeekHeadroom : Model.MinTLOD[Model.activeProfile]) : ((Model.IncCloudQ[Model.activeProfile] && (Model.VrModeActive ? Model.cloudQ_VR : Model.cloudQ) < 3 && !Model.IncCloudQActive) ? Math.Max(Model.TLODBasePlusSeekHeadroom, Model.MaxTLOD[Model.activeProfile] / 4f) : ((Model.MinTLODExtraSeekingInitial || !MinTLODExtraReSeek) ? Model.TLODBasePlusSeekHeadroom : ((float)Model.FPSTolerance[Model.activeProfile])))))
						{
							Model.MinTLODExtraIterations = 1;
						}
						else
						{
							if (MinTLODExtraReSeek)
							{
								if ((int)(num22 - Model.MinTLODExtraAmount) % Model.FPSTolerance[Model.activeProfile] == 0)
								{
									Model.MinTLODExtraAmount += Model.FPSTolerance[Model.activeProfile] * ((!(Model.MinTLODExtraAmount < num22) || !Model.MinTLODExtraSeekingInitial) ? 1 : 2);
								}
								else
								{
									Model.MinTLODExtraAmount += (int)(num22 - Model.MinTLODExtraAmount) % Model.FPSTolerance[Model.activeProfile];
								}
							}
							else
							{
								Model.MinTLODExtraAmount += num16;
							}
							Model.MinTLODExtraSeeking = true;
							if (!MinTLODExtraReSeek && num16 > (float)Model.FPSTolerance[Model.activeProfile])
							{
								Model.FPSSettleActive = true;
							}
							SetActiveLODs();
							if (Model.LogPlus && (!MinTLODExtraReSeek || (Model.MinTLODExtraAmount - (float)Model.FPSTolerance[Model.activeProfile]) % Model.TLODBasePlusSeekMinHeadroom == 0f) && ((Model.MinTLODExtraSeekingInitial && !Model.perfLogForInitialSeekDone) || (MinTLODExtraReSeek && !Model.perfLogForFirstReseekDone)))
							{
								OutputPerfLog(trigger: true);
							}
						}
					}
					else if (num < deltaFPSTrendMin / 5f && !flag && !MinTLODExtraReSeek && Model.MinTLODExtraAmount > 0f && !Model.FPSSettleActive && TotalTicks >= 20)
					{
						Model.MinTLODExtraAmount -= Math.Min((float)Math.Pow(num / Model.TargetFPS / deltaFPSTrendMin * 100f, 1.75600004196167) * TLODStep / 5f, TLODStep * 2f);
						if (Model.MinTLODExtraAmount < 0f)
						{
							Model.MinTLODExtraAmount = 0f;
						}
						if (Model.IncCloudQActive && ((double)Model.MinTLODExtraAmount < 0.5 * (double)num22 || Model.gpuUsage > Model.CloudIncreaseGPUMaxLoad))
						{
							AutoDecreaseCloudQuality();
						}
						MinTLODExtraSeekLastTickMark = TotalTicks;
						SetActiveLODs();
					}
					else if (!FGInactive)
					{
						if ((Model.MinTLODExtraSeekingInitial || MinTLODExtraReSeek) && Model.MinTLODExtraIterations > 1)
						{
							Model.MinTLODExtraIterations = 1;
						}
						else
						{
							Model.MinTLODExtraIterations = 0;
						}
					}
					if (!criticalFlightPhaseActive && deltaFPSTrendShort.Average() >= deltaFPSTrendMin / 5f && num >= deltaFPSTrendMin / 5f && Model.TLODBasePlusReSeekEnabled && !MinTLODExtraReSeek && Math.Max(Model.gpuMemoryUsage, gpuMemoryUsageLast) < (Model.VRAMSettingsReduceActive ? (Model.VRAMOverflowSettingsRecovery + 1) : Model.VRAMOverflowHoldTLOD) && (!Model.MinTLODExtraSeekingInitial || Model.MinTLODReductionAmount > 0f || Model.DynamicSettingsActive) && (ReduceSettingsRecoveryEnabled || Model.AboveAltTLODTopPlusTolerance) && (TotalTicks - MinTLODExtraSeekLastTickMark > Model.TLODBasePlusReSeekInterval || ((Model.MinTLODReductionAmount > 0f || Model.DynamicSettingsActive) && TotalTicks - MinTLODExtraSeekLastTickMark > 30)) && (Model.MinTLODExtraAmount < num22 || (Model.IncCloudQ[Model.activeProfile] && !Model.IncCloudQActive && (Model.VrModeActive ? Model.cloudQ_VR : Model.cloudQ) < 3)))
					{
						MinTLODExtraReSeek = true;
						if (Model.MinTLODReductionAmount > 0f || Model.DynamicSettingsActive)
						{
							Logger.Log(LogLevel.Information, "LODController:RunTick", "Settings recovery periodic re-seek process initiated");
						}
						else
						{
							Logger.Log(LogLevel.Information, "LODController:RunTick", "TLOD Base Extra periodic re-seek process initiated");
						}
					}
				}
				if (Model.MinTLODExtra[Model.activeProfile] && Model.MinTLODExtraAmount > 0f && !Model.MinTLODExtraSeeking)
				{
					Model.MinTLODExtraActive = true;
				}
				else
				{
					Model.MinTLODExtraActive = false;
				}
				if (Model.TLODReduceAfterGearDown && Model.LockTLODMax)
				{
					newTLOD = Math.Clamp(Model.tlod + newTLOD, Model.activeMinTLOD, Model.activeMaxTLOD);
				}
				else if (Model.altAboveGnd < Model.AltTLODBase[Model.activeProfile])
				{
					newTLOD = Model.activeMinTLOD;
				}
				else if (Model.altAboveGnd > Model.AltTLODTop[Model.activeProfile])
				{
					newTLOD = Model.activeMaxTLOD;
				}
				else
				{
					newTLOD = CalculateTLOD();
					if (Model.MinTLODExtraSeeking)
					{
						newTLOD = (float)Math.Round(newTLOD / (float)Model.FPSTolerance[Model.activeProfile]) * (float)Model.FPSTolerance[Model.activeProfile];
					}
					else if (Math.Abs(newTLOD - Model.tlod) < (float)Model.FPSTolerance[Model.activeProfile])
					{
						newTLOD = Model.tlod;
					}
					else if (Math.Abs(newTLOD - Model.tlod) > (float)(Model.FPSTolerance[Model.activeProfile] * 2))
					{
						newTLOD = Model.tlod + (float)(Math.Sign(newTLOD - Model.tlod) * Model.FPSTolerance[Model.activeProfile] * 2);
					}
					else
					{
						newTLOD = Model.tlod + (float)(Math.Sign(newTLOD - Model.tlod) * Model.FPSTolerance[Model.activeProfile]);
					}
				}
				if (newTLOD > Model.SimMaxLOD + ((MinTLODExtraReSeek || (!Model.MinTLODExtraActive && Model.MinTLODExtraIterations > 1 && deltaFPSTrendShort.Average() >= deltaFPSTrendMin / 5f && num >= deltaFPSTrendMin / 5f)) ? Model.TLODBasePlusSeekHeadroom : 0f))
				{
					newTLOD = Model.SimMaxLOD + ((MinTLODExtraReSeek || (!Model.MinTLODExtraActive && Model.MinTLODExtraIterations > 1 && deltaFPSTrendShort.Average() >= deltaFPSTrendMin / 5f && num >= deltaFPSTrendMin / 5f)) ? Model.TLODBasePlusSeekHeadroom : 0f);
					Model.MinTLODExtraIterations = 1;
				}
			}
			else
			{
				newTLOD = 0f;
				if (Model.VRAMProtInactive && Model.ReductionSettingsDepth == 0 && Model.MinTLODReductionAmount == 0f)
				{
					flag3 = Model.FixedTargetFPS || Model.fixedFPSDetectedAutoTargetFPS;
					num26 = (Model.MinTLODExtra[Model.activeProfile] ? (Math.Min(Model.activeMaxTLOD, Model.MinTLOD[Model.activeProfile] * Model.MinTLODExtraCapMult[Model.activeProfile]) - MinTLODNoExtra) : 0f);
					if (Model.IncCloudQActive && Model.MinTLODExtraAmount <= num26)
					{
						bool num27;
						if (!flag3)
						{
							num27 = num <= 0f - num3;
						}
						else
						{
							if (Model.gpuUsage <= 0f && deltaFPSTrendShort.Average() <= deltaFPSTrendMin && num2 <= deltaFPSTrendMin)
							{
								goto IL_2f3f;
							}
							num27 = Model.gpuUsage > Model.CloudIncreaseGPUMaxLoad;
						}
						if (num27)
						{
							goto IL_2f3f;
						}
					}
					goto IL_2f45;
				}
				newTLOD = Model.tlod;
			}
			goto IL_37ff;
		}
		goto IL_6833;
		IL_2f3f:
		AutoDecreaseCloudQuality();
		goto IL_2f45;
		IL_37ff:
		Model.MinTLODExtraAmount = (float)Math.Round(Model.MinTLODExtraAmount);
		CalculateTLODRange();
		newTLOD = (float)Math.Round(Math.Clamp(newTLOD, Model.rangeMinTLOD, Model.rangeMaxTLOD));
		if (Model.VRAMPlusEnabled && Model.gpuMemoryUsage > 0 && gpuMemoryUsageLast > 0)
		{
			if (Math.Min(Model.gpuMemoryUsage, gpuMemoryUsageLast) >= Model.VRAMOverflowHoldTLOD && Math.Max(Model.gpuMemoryUsage, gpuMemoryUsageLast) < Model.VRAMOverflowReduceTLOD)
			{
				Model.VRAMReduceActive = false;
				Model.VRAMProtInactive = false;
				if (newTLOD > Model.tlod)
				{
					newTLOD = Model.tlod;
				}
				if (!Model.VRAMHoldActive)
				{
					Logger.Log(LogLevel.Information, "LODController:RunTick", "VRAM Limited Hold Activated @ " + Model.gpuMemoryUsage + "%");
				}
				Model.VRAMHoldActive = true;
			}
			else if (Math.Min(Model.gpuMemoryUsage, gpuMemoryUsageLast) >= Model.VRAMOverflowReduceTLOD)
			{
				Model.VRAMHoldActive = false;
				Model.VRAMProtInactive = false;
				if (Model.TLODAutoMethod[Model.activeProfile] == 3)
				{
					if (!Model.VRAMReduceActive)
					{
						Logger.Log(LogLevel.Information, "LODController:RunTick", "VRAM Limited Reduce Activated @ " + Model.gpuMemoryUsage + "%");
						Model.MinTLODExtraAmount = 0f;
						Model.MinTLODExtraActive = false;
					}
					Model.VRAMSettingsReduceActive = true;
				}
				else
				{
					if (Model.tlod > Model.MinTLOD[Model.activeProfile])
					{
						float num28 = TLODStep * (float)(Model.gpuMemoryUsage - Model.VRAMOverflowReduceTLOD + 1);
						newTLOD = Math.Max(Model.MinTLOD[Model.activeProfile], Model.tlod - num28);
						if (Model.recentTLODPeak > newTLOD)
						{
							Model.recentTLODPeak = newTLOD;
						}
						if (Model.MinTLODExtraAmount > 0f && Model.tlod <= Model.MinTLOD[Model.activeProfile] * Model.MinTLODExtraCapMult[Model.activeProfile])
						{
							Model.MinTLODExtraAmount = Math.Max(0f, Model.MinTLODExtraAmount - num28);
						}
						SetActiveLODs();
					}
					else
					{
						newTLOD = Model.activeMinTLOD;
					}
					if (!Model.VRAMReduceActive)
					{
						Logger.Log(LogLevel.Information, "LODController:RunTick", "VRAM Limited Reduce Activated @ " + Model.gpuMemoryUsage + "%");
					}
					if (Model.tlod == Model.MinTLOD[Model.activeProfile])
					{
						Model.VRAMSettingsReduceActive = true;
						Model.MinTLODExtraActive = false;
					}
				}
				Model.VRAMReduceActive = true;
			}
			else if (Math.Max(Model.gpuMemoryUsage, gpuMemoryUsageLast) < (Model.VRAMSettingsReduceActive ? (Model.VRAMOverflowSettingsRecovery + 1) : Model.VRAMOverflowHoldTLOD))
			{
				Model.VRAMHoldActive = false;
				Model.VRAMReduceActive = false;
				if (Model.MinTLODReductionAmount == 0f)
				{
					if (!Model.VRAMProtInactive)
					{
						Logger.Log(LogLevel.Information, "LODController:RunTick", "VRAM Limiting Inactive");
					}
					Model.VRAMProtInactive = true;
				}
			}
		}
		gpuMemoryUsageLast = Model.gpuMemoryUsage;
		Model.LODControllerBusy = true;
		if (!Model.SpikeVerificationActive && Math.Abs(Model.tlod - newTLOD) >= 1f)
		{
			Model.tlod = Model.MemoryAccess.SetTLOD(newTLOD);
			Model.tlod_step = true;
			Model.tlod_increased = Model.tlod > newTLOD;
		}
		else
		{
			Model.tlod_step = false;
			Model.tlod_increased = false;
		}
		if (!Model.FreeTLOD[Model.activeProfile] && Model.MaxTLODHalvedNight[Model.activeProfile] && Model.NightTime && !Model.NightTimeTransiton && Model.IncCloudQ[Model.activeProfile] && !Model.NightCloudReduceLevelsActive)
		{
			if (!Model.IncCloudQActive && ((Model.FixedTargetFPS || Model.TLODAutoMethod[Model.activeProfile] == 3) ? (deltaFPSTrendShort.Average() >= deltaFPSTrendMin / 5f) : (num >= Model.TargetFPS * 0.1f)) && ((Model.TLODAutoMethod[Model.activeProfile] != 3 && !Model.FixedTargetFPS) || Model.gpuUsage <= 0f || Model.gpuUsage <= Model.CloudIncreaseGPUMaxLoad) && TotalTicks - MinTLODExtraSeekLastTickMark > Model.TLODBasePlusReSeekInterval && (Model.VrModeActive ? Model.cloudQ_VR : Model.cloudQ) < 3)
			{
				AutoIncreaseCloudQuality();
			}
			else if (Model.IncCloudQActive && ((Model.FixedTargetFPS || Model.TLODAutoMethod[Model.activeProfile] == 3) ? (deltaFPSTrendShort.Average() <= deltaFPSTrendMin) : (num <= 0f - num3)))
			{
				AutoDecreaseCloudQuality();
			}
		}
		if (!Model.FPSSettleActive)
		{
			float num29;
			if (Model.altAboveGnd >= Model.AltTLODTop[Model.activeProfile])
			{
				num29 = Model.activeMinTLOD / Model.activeMaxTLOD;
			}
			else
			{
				num29 = ((Model.altAboveGnd <= Model.AltTLODBase[Model.activeProfile]) ? 1f : (1f + (Model.altAboveGnd - Model.AltTLODBase[Model.activeProfile]) * (Model.activeMinTLOD / Model.activeMaxTLOD - 1f) / (Model.AltTLODTop[Model.activeProfile] - Model.AltTLODBase[Model.activeProfile])));
			}
			if (Model.SettingsReduceEnabled && ((Model.VRAMReduceActive && Model.VRAMSettingsReduceActive && Math.Min(Model.gpuMemoryUsage, gpuMemoryUsageLast) >= Model.VRAMOverflowReduceTLOD && Model.ReductionSettingsDepth < ((!Model.isMSFS2024) ? 1 : ((Model.VRAMPlusSuite < 2) ? 2 : Model.ReductionSettingsMaxLevels[Model.activeProfile]))) || (Model.AutoReduceSettings[Model.activeProfile] && !FGInactive && Model.MinTLODExtraAmount == 0f && Model.ReductionSettingsDepth < Model.ReductionSettingsMaxLevels[Model.activeProfile] && !Model.MinTLODExtraSeeking && deltaFPSTrendShort.Average() <= 0f - num3 && (Model.TLODAutoMethod[Model.activeProfile] >= 2 || Model.tlod == Model.activeMinTLOD))))
			{
				if (Model.LogPlus)
				{
					OutputPerfLog(trigger: true);
				}
				if (Model.IncCloudQActive)
				{
					AutoDecreaseCloudQuality();
				}
				if (Model.TLODAutoMethod[Model.activeProfile] < 2)
				{
					Model.MinTLODExtraIterations = 1;
				}
				if (Model.ReductionSettingsDepth == 0 && Model.MinTLODReductionAmount < Model.MinTLOD[Model.activeProfile] / 2f && !Model.DynamicSettingsActive)
				{
					if (!Model.VRAMSettingsReduceActive && (Model.VrModeActive ? Model.DynSet_VR : Model.DynSet))
					{
						Logger.Log(LogLevel.Information, "LODController:RunTick", "Auto settings reduction dynamic settings activated");
						Model.MinTLODReductionAmount = 0f;
						Model.DynamicSettingsActive = true;
					}
					else
					{
						if (Model.MinTLODReductionAmount == 0f)
						{
							if (Model.VRAMSettingsReduceActive)
							{
								Logger.Log(LogLevel.Information, "LODController:RunTick", "VRAM protection auto settings reduction activated");
								if (Model.IncCloudQActive)
								{
									MemoryManager memoryAccess = Model.MemoryAccess;
									int value;
									if (!Model.VrModeActive)
									{
										int num30 = (Model.cloudQ -= 1);
										value = num30;
									}
									else
									{
										int num30 = (Model.cloudQ_VR -= 1);
										value = num30;
									}
									memoryAccess.SetCloudQ(value, Model.VrModeActive);
									Model.IncCloudQActive = false;
									Logger.Log(LogLevel.Information, "LODController:RunTick", "Increase Cloud Quality deactivated: " + ServiceModel.SettingQualityText(Model.VrModeActive ? (Model.cloudQ_VR + 1) : (Model.cloudQ + 1)) + " → " + ServiceModel.SettingQualityText(Model.VrModeActive ? Model.cloudQ_VR : Model.cloudQ));
								}
							}
							else
							{
								Logger.Log(LogLevel.Information, "LODController:RunTick", "Auto settings LODs reduction initiated");
							}
						}
						Model.MinTLODReductionAmount += (Model.VRAMReduceActive ? Math.Max(Model.MinTLOD[Model.activeProfile] / 20f, Model.FPSTolerance[Model.activeProfile]) : (((Model.TLODAutoMethod[Model.activeProfile] >= 2 && Model.MinTLODExtra[Model.activeProfile]) ? num29 : 1f) * (float)Model.FPSTolerance[Model.activeProfile]));
						if (Model.MinTLODReductionAmount >= Model.MinTLOD[Model.activeProfile] / 2f)
						{
							Model.MinTLODReductionAmount = Model.MinTLOD[Model.activeProfile] / 2f;
							Logger.Log(LogLevel.Information, "LODController:RunTick", "Auto settings reduction to LODs-only activated");
						}
					}
				}
				else if (!Model.isMSFS2024 && Model.ReductionSettingsSuite[Model.activeProfile] == 0 && Model.VRAMSettingsReduceActive)
				{
					Model.ReductionSettingsDepth = 1;
				}
				else if (Model.ReductionSettingsSuite[Model.activeProfile] == 0 && (!Model.VRAMSettingsReduceActive || Model.VRAMPlusSuite >= 2))
				{
					Model.ReductionSettingsDepth = Model.ReductionSettingsMaxLevels[Model.activeProfile];
				}
				else
				{
					int num33 = ((!Model.VRAMSettingsReduceActive || Model.VRAMPlusSuite >= 2) ? Model.ReductionSettingsFloor[Model.activeProfile] : 0);
					if (Model.VrModeActive)
					{
						if (Model.ReduceCloudQ[Model.activeProfile] && (!Model.VRAMSettingsReduceActive || Model.VRAMPlusSuite >= 2) && Model.cloudQ_VR > Math.Max(num33 - 1, 0))
						{
							MemoryManager memoryAccess2 = Model.MemoryAccess;
							int num30 = (Model.cloudQ_VR = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultCloudQ_VR - 1) : (Model.cloudQ_VR - 1)));
							memoryAccess2.SetCloudQ(num30, VR: true);
						}
						if ((Model.ReduceTrees[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.Trees_VR > Math.Max(num33 - 1, 0))
						{
							MemoryManager memoryAccess3 = Model.MemoryAccess;
							int num30 = (Model.Trees_VR = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultTrees_VR - 1) : (Model.Trees_VR - 1)));
							memoryAccess3.SetTrees(num30, VR: true);
						}
						if ((Model.ReduceRTShadows[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.RTShadows_VR)
						{
							MemoryManager memoryAccess4 = Model.MemoryAccess;
							bool value2 = (Model.RTShadows_VR = false);
							memoryAccess4.SetRTShadows(value2, VR: true);
						}
						if ((Model.ReduceTShadows[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.TShadowsEnabled_VR && Model.TShadows_VR == 128 && num33 == 0)
						{
							MemoryManager memoryAccess5 = Model.MemoryAccess;
							bool value2 = (Model.TShadowsEnabled_VR = false);
							memoryAccess5.SetTShadowsEnabled(value2, VR: true);
						}
						if ((Model.ReduceTShadows[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.TShadowsEnabled_VR && (double)Model.TShadows_VR > Math.Pow(2.0, num33 + 6))
						{
							Model.MemoryAccess.SetTerrainShadows(Model.TShadows_VR /= 2, VR: true);
						}
						if ((Model.ReduceBldings[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.Bldings_VR > Math.Max(num33 - 1, 0))
						{
							MemoryManager memoryAccess6 = Model.MemoryAccess;
							int num30 = (Model.Bldings_VR = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultBldings_VR - 1) : (Model.Bldings_VR - 1)));
							memoryAccess6.SetBuildings(num30, VR: true);
						}
						if ((Model.ReduceDispMap[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.DispMap_VR)
						{
							MemoryManager memoryAccess7 = Model.MemoryAccess;
							bool value2 = (Model.DispMap_VR = false);
							memoryAccess7.SetDispMap(value2, VR: true);
						}
						if ((Model.ReduceOSTPCache[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.OSTPCache_VR > Math.Max(num33 - 1, 0))
						{
							MemoryManager memoryAccess8 = Model.MemoryAccess;
							int num30 = (Model.OSTPCache_VR = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultOSTPCache_VR - 1) : (Model.OSTPCache_VR - 1)));
							memoryAccess8.SetOSTPreCaching(num30, VR: true);
						}
						if ((Model.ReducePlants[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.Plants_VR > num33 - 1)
						{
							MemoryManager memoryAccess9 = Model.MemoryAccess;
							int num30 = (Model.Plants_VR = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultPlants_VR - 1) : (Model.Plants_VR - 1)));
							memoryAccess9.SetPlants(num30, VR: true);
						}
						if ((Model.ReduceGrass[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.Grass_VR > num33 - 1)
						{
							MemoryManager memoryAccess10 = Model.MemoryAccess;
							int num30 = (Model.Grass_VR = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultGrass_VR - 1) : (Model.Grass_VR - 1)));
							memoryAccess10.SetGrass(num30, VR: true);
						}
						if ((Model.ReducePkdAcQ[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.PkdAcQ_VR > num33 - 1)
						{
							MemoryManager memoryAccess11 = Model.MemoryAccess;
							int num30 = (Model.PkdAcQ_VR = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultPkdAcQ_VR - 1) : (Model.PkdAcQ_VR - 1)));
							memoryAccess11.SetPkdAcQ(num30, VR: true);
						}
						if ((Model.ReduceAcTrafQ[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.AcTrafQ_VR > num33 - 1)
						{
							MemoryManager memoryAccess12 = Model.MemoryAccess;
							int num30 = (Model.AcTrafQ_VR = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultAcTrafQ_VR - 1) : (Model.AcTrafQ_VR - 1)));
							memoryAccess12.SetAcTrafQ(num30, VR: true);
						}
					}
					else
					{
						if (Model.ReduceCloudQ[Model.activeProfile] && (!Model.VRAMSettingsReduceActive || Model.VRAMPlusSuite >= 2) && Model.cloudQ > Math.Max(num33 - 1, 0))
						{
							MemoryManager memoryAccess13 = Model.MemoryAccess;
							int num30 = (Model.cloudQ = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultCloudQ - 1) : (Model.cloudQ - 1)));
							memoryAccess13.SetCloudQ(num30);
						}
						if ((Model.ReduceTrees[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.Trees > Math.Max(num33 - 1, 0))
						{
							MemoryManager memoryAccess14 = Model.MemoryAccess;
							int num30 = (Model.Trees = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultTrees - 1) : (Model.Trees - 1)));
							memoryAccess14.SetTrees(num30);
						}
						if ((Model.ReduceRTShadows[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.RTShadows)
						{
							MemoryManager memoryAccess15 = Model.MemoryAccess;
							bool value2 = (Model.RTShadows = false);
							memoryAccess15.SetRTShadows(value2);
						}
						if ((Model.ReduceTShadows[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.TShadowsEnabled && Model.TShadows == 128 && num33 == 0)
						{
							MemoryManager memoryAccess16 = Model.MemoryAccess;
							bool value2 = (Model.TShadowsEnabled = false);
							memoryAccess16.SetTShadowsEnabled(value2);
						}
						if ((Model.ReduceTShadows[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.TShadowsEnabled && (double)Model.TShadows > Math.Pow(2.0, num33 + 6))
						{
							Model.MemoryAccess.SetTerrainShadows(Model.TShadows /= 2);
						}
						if ((Model.ReduceBldings[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.Bldings > Math.Max(num33 - 1, 0))
						{
							MemoryManager memoryAccess17 = Model.MemoryAccess;
							int num30 = (Model.Bldings = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultBldings - 1) : (Model.Bldings - 1)));
							memoryAccess17.SetBuildings(num30);
						}
						if ((Model.ReduceDispMap[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.DispMap)
						{
							MemoryManager memoryAccess18 = Model.MemoryAccess;
							bool value2 = (Model.DispMap = false);
							memoryAccess18.SetDispMap(value2);
						}
						if ((Model.ReduceOSTPCache[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.OSTPCache > Math.Max(num33 - 1, 0))
						{
							MemoryManager memoryAccess19 = Model.MemoryAccess;
							int num30 = (Model.OSTPCache = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultOSTPCache - 1) : (Model.OSTPCache - 1)));
							memoryAccess19.SetOSTPreCaching(num30);
						}
						if ((Model.ReducePlants[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.Plants > num33 - 1)
						{
							MemoryManager memoryAccess20 = Model.MemoryAccess;
							int num30 = (Model.Plants = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultPlants - 1) : (Model.Plants - 1)));
							memoryAccess20.SetPlants(num30);
						}
						if ((Model.ReduceGrass[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.Grass > num33 - 1)
						{
							MemoryManager memoryAccess21 = Model.MemoryAccess;
							int num30 = (Model.Grass = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultGrass - 1) : (Model.Grass - 1)));
							memoryAccess21.SetGrass(num30);
						}
						if ((Model.ReducePkdAcQ[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.PkdAcQ > num33 - 1)
						{
							MemoryManager memoryAccess22 = Model.MemoryAccess;
							int num30 = (Model.PkdAcQ = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultPkdAcQ - 1) : (Model.PkdAcQ - 1)));
							memoryAccess22.SetPkdAcQ(num30);
						}
						if ((Model.ReduceAcTrafQ[Model.activeProfile] || (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2)) && Model.AcTrafQ > num33 - 1)
						{
							MemoryManager memoryAccess23 = Model.MemoryAccess;
							int num30 = (Model.AcTrafQ = ((Model.ReductionSettingsDepth == 0) ? (Model.DefaultAcTrafQ - 1) : (Model.AcTrafQ - 1)));
							memoryAccess23.SetAcTrafQ(num30);
						}
					}
					Model.ReductionSettingsDepth++;
					Model.ReductionSettingsDepthAchieved = Math.Max(Model.ReductionSettingsDepthAchieved, Model.ReductionSettingsDepth);
					Model.ReductionSettingsSuiteAchieved = Math.Max(Model.ReductionSettingsSuiteAchieved, (Model.VRAMSettingsReduceActive && Model.VRAMPlusSuite < 2) ? 2 : Model.ReductionSettingsSuite[Model.activeProfile]);
					Model.FPSSettleActive = true;
					Logger.Log(LogLevel.Information, "LODController:RunTick", "Auto settings reduction increased to level " + Model.ReductionSettingsDepth);
				}
				MinTLODExtraSeekLastTickMark = TotalTicks;
			}
			else if ((!criticalFlightPhaseActive || Model.FreeTLOD[Model.activeProfile]) && (Model.AutoReduceSettings[Model.activeProfile] || (Math.Max(Model.gpuMemoryUsage, gpuMemoryUsageLast) <= Model.VRAMOverflowSettingsRecovery && Model.VRAMSettingsReduceActive)) && ReduceSettingsRecoveryEnabled && ((!Model.VRAMSettingsReduceActive && !FGInactive && (deltaFPSTrendShort.Average() >= num3 || ((Model.MinTLODExtraAmount >= Model.MinTLOD[Model.activeProfile] / 2f || Model.FixedTargetFPS) && deltaFPSTrendShort.Average() >= deltaFPSTrendMin / 5f && num >= deltaFPSTrendMin / 5f))) || (Math.Max(Model.gpuMemoryUsage, gpuMemoryUsageLast) <= Model.VRAMOverflowSettingsRecovery && Model.VRAMSettingsReduceActive)))
			{
				if (Model.LogPlus)
				{
					OutputPerfLog(trigger: true);
				}
				if (Model.ReductionSettingsSuite[Model.activeProfile] == 0 && (!Model.VRAMSettingsReduceActive || Model.VRAMPlusSuite >= 2))
				{
					Model.ReductionSettingsDepth = -1;
				}
				else
				{
					Model.ReductionSettingsDepth--;
				}
				if (Model.ReductionSettingsDepth == -1 && Model.MinTLODReductionAmount > 0f)
				{
					Model.MinTLODReductionAmount -= ((Model.TLODAutoMethod[Model.activeProfile] >= 2 && Model.MinTLODExtra[Model.activeProfile]) ? num29 : 1f) * (float)Model.FPSTolerance[Model.activeProfile];
					Model.MinTLODExtraAmount = Math.Max(0f, Model.MinTLODExtraAmount - (float)Model.FPSTolerance[Model.activeProfile]);
					if (Model.MinTLODReductionAmount <= 0f)
					{
						Model.MinTLODReductionAmount = 0f;
					}
					Model.ReductionSettingsDepth = 0;
					if (Model.MinTLODReductionAmount == 0f)
					{
						if (Model.VRAMSettingsReduceActive)
						{
							Logger.Log(LogLevel.Information, "LODController:RunTick", "VRAM protection auto settings reduction deactivated");
							Model.VRAMSettingsReduceActive = false;
							if (++Model.VRAMSettingsReduceAlt > 2)
							{
								Model.VRAMSettingsReduceAlt = 2;
							}
						}
						else
						{
							Logger.Log(LogLevel.Information, "LODController:RunTick", "Auto settings LODs reduction deactivated");
						}
					}
				}
				else
				{
					if (Model.TLODAutoMethod[Model.activeProfile] < 2)
					{
						Model.MinTLODExtraAmount = 0f;
					}
					else if (Model.MinTLODExtraAmount >= Model.MinTLOD[Model.activeProfile] / 2f)
					{
						Model.MinTLODExtraAmount -= Model.MinTLOD[Model.activeProfile] / 2f;
					}
					if (Model.ReductionSettingsDepth >= 0)
					{
						if (Model.VrModeActive)
						{
							if (Model.cloudQ_VR < Model.DefaultCloudQ_VR - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess24 = Model.MemoryAccess;
								int num30 = (Model.cloudQ_VR += 1);
								memoryAccess24.SetCloudQ(num30, VR: true);
							}
							if (Model.Trees_VR < Model.DefaultTrees_VR - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess25 = Model.MemoryAccess;
								int num30 = (Model.Trees_VR += 1);
								memoryAccess25.SetTrees(num30, VR: true);
							}
							if (!Model.RTShadows_VR && Model.DefaultRTShadows_VR && Model.ReductionSettingsDepth == 0)
							{
								MemoryManager memoryAccess26 = Model.MemoryAccess;
								bool value2 = (Model.RTShadows_VR = true);
								memoryAccess26.SetRTShadows(value2, VR: true);
							}
							if (Model.TShadowsEnabled_VR && Model.TShadows_VR < Model.DefaultTShadows_VR)
							{
								Model.MemoryAccess.SetTerrainShadows(Model.TShadows_VR *= 2, VR: true);
							}
							if (!Model.TShadowsEnabled_VR && Model.DefaultTShadowsEnabled_VR)
							{
								MemoryManager memoryAccess27 = Model.MemoryAccess;
								bool value2 = (Model.TShadowsEnabled_VR = true);
								memoryAccess27.SetTShadowsEnabled(value2, VR: true);
							}
							if (Model.Bldings_VR < Model.DefaultBldings_VR - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess28 = Model.MemoryAccess;
								int num30 = (Model.Bldings_VR += 1);
								memoryAccess28.SetBuildings(num30, VR: true);
							}
							if (Model.OSTPCache_VR < Model.DefaultOSTPCache_VR - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess29 = Model.MemoryAccess;
								int num30 = (Model.OSTPCache_VR += 1);
								memoryAccess29.SetOSTPreCaching(num30, VR: true);
							}
							if (Model.Plants_VR < Model.DefaultPlants_VR - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess30 = Model.MemoryAccess;
								int num30 = (Model.Plants_VR += 1);
								memoryAccess30.SetPlants(num30, VR: true);
							}
							if (Model.Grass_VR < Model.DefaultGrass_VR - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess31 = Model.MemoryAccess;
								int num30 = (Model.Grass_VR += 1);
								memoryAccess31.SetGrass(num30, VR: true);
							}
							if (Model.PkdAcQ_VR < Model.DefaultPkdAcQ_VR - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess32 = Model.MemoryAccess;
								int num30 = (Model.PkdAcQ_VR += 1);
								memoryAccess32.SetPkdAcQ(num30, VR: true);
							}
							if (Model.AcTrafQ_VR < Model.DefaultAcTrafQ_VR - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess33 = Model.MemoryAccess;
								int num30 = (Model.AcTrafQ_VR += 1);
								memoryAccess33.SetAcTrafQ(num30, VR: true);
							}
						}
						else
						{
							if (Model.cloudQ < Model.DefaultCloudQ - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess34 = Model.MemoryAccess;
								int num30 = (Model.cloudQ += 1);
								memoryAccess34.SetCloudQ(num30);
							}
							if (Model.Trees < Model.DefaultTrees - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess35 = Model.MemoryAccess;
								int num30 = (Model.Trees += 1);
								memoryAccess35.SetTrees(num30);
							}
							if (!Model.RTShadows && Model.DefaultRTShadows && Model.ReductionSettingsDepth == 0)
							{
								MemoryManager memoryAccess36 = Model.MemoryAccess;
								bool value2 = (Model.RTShadows = true);
								memoryAccess36.SetRTShadows(value2);
							}
							if (Model.TShadowsEnabled && Model.TShadows < Model.DefaultTShadows)
							{
								Model.MemoryAccess.SetTerrainShadows(Model.TShadows *= 2);
							}
							if (!Model.TShadowsEnabled && Model.DefaultTShadowsEnabled)
							{
								MemoryManager memoryAccess37 = Model.MemoryAccess;
								bool value2 = (Model.TShadowsEnabled = true);
								memoryAccess37.SetTShadowsEnabled(value2);
							}
							if (Model.Bldings < Model.DefaultBldings - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess38 = Model.MemoryAccess;
								int num30 = (Model.Bldings += 1);
								memoryAccess38.SetBuildings(num30);
							}
							if (Model.OSTPCache < Model.DefaultOSTPCache - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess39 = Model.MemoryAccess;
								int num30 = (Model.OSTPCache += 1);
								memoryAccess39.SetOSTPreCaching(num30);
							}
							if (Model.Plants < Model.DefaultPlants - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess40 = Model.MemoryAccess;
								int num30 = (Model.Plants += 1);
								memoryAccess40.SetPlants(num30);
							}
							if (Model.Grass < Model.DefaultGrass - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess41 = Model.MemoryAccess;
								int num30 = (Model.Grass += 1);
								memoryAccess41.SetGrass(num30);
							}
							if (Model.PkdAcQ < Model.DefaultPkdAcQ - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess42 = Model.MemoryAccess;
								int num30 = (Model.PkdAcQ += 1);
								memoryAccess42.SetPkdAcQ(num30);
							}
							if (Model.AcTrafQ < Model.DefaultAcTrafQ - Model.ReductionSettingsDepth)
							{
								MemoryManager memoryAccess43 = Model.MemoryAccess;
								int num30 = (Model.AcTrafQ += 1);
								memoryAccess43.SetAcTrafQ(num30);
							}
						}
					}
					Model.FPSSettleActive = true;
					if (Model.ReductionSettingsDepth <= 0)
					{
						if (Model.DynamicSettingsActive)
						{
							Model.DynamicSettingsActive = false;
							Logger.Log(LogLevel.Information, "LODController:RunTick", "Dynamic settings inactive");
						}
						else
						{
							Logger.Log(LogLevel.Information, "LODController:RunTick", "Auto settings reduction decreased to LODs only");
						}
						Model.ReductionSettingsDepth = 0;
					}
					else
					{
						Logger.Log(LogLevel.Information, "LODController:RunTick", "Auto settings reduction decreased to level " + Model.ReductionSettingsDepth);
					}
				}
			}
			else if (Model.LogPlus)
			{
				OutputPerfLog(trigger: false);
			}
			if (Model.ReductionSettingsDepth == 0 && !Model.DispMap && Model.DefaultDispMap && Model.altAboveGnd >= 100f)
			{
				MemoryManager memoryAccess44 = Model.MemoryAccess;
				bool value2 = (Model.DispMap = true);
				memoryAccess44.SetDispMap(value2);
			}
		}
		if (!Model.isMSFS2024)
		{
			if (Model.DecCloudQ[Model.activeProfile] && !Model.DecCloudQActive && !Model.FPSSettleActive && !Model.MinTLODExtraSeeking && ((Model.TLODAutoMethod[Model.activeProfile] < 2 && Model.DecCloudQMethod[Model.activeProfile] != 1 && Model.tlod == Model.activeMinTLOD && num <= 0f - num3) || (((Model.TLODAutoMethod[Model.activeProfile] == 2 && num <= 0f - num3) || (Model.TLODAutoMethod[Model.activeProfile] == 3 && deltaFPSTrendShort.Average() <= deltaFPSTrendMin) || Model.DecCloudQMethod[Model.activeProfile] == 1) && Model.gpuUsage > Model.CloudDecreaseGPUPct)))
			{
				if (Model.VrModeActive && Model.DefaultCloudQ_VR >= 1)
				{
					MemoryManager memoryAccess45 = Model.MemoryAccess;
					int num30 = (Model.cloudQ_VR = Model.DefaultCloudQ_VR - 1);
					memoryAccess45.SetCloudQ(num30, VR: true);
					Model.DecCloudQActive = true;
				}
				if (!Model.VrModeActive && Model.DefaultCloudQ >= 1)
				{
					MemoryManager memoryAccess46 = Model.MemoryAccess;
					int num30 = (Model.cloudQ = Model.DefaultCloudQ - 1);
					memoryAccess46.SetCloudQ(num30);
					Model.DecCloudQActive = true;
				}
				CloudRecoveryTickMark = TotalTicks;
			}
			if (Model.DecCloudQ[Model.activeProfile] && Model.DecCloudQActive && ((Model.TLODAutoMethod[Model.activeProfile] < 2 && Model.DecCloudQMethod[Model.activeProfile] != 1 && (num >= Model.TargetFPS * 0.15f || (newTLOD >= ((Model.UseExpertOptions && Model.CloudRecoveryPlus[Model.activeProfile]) ? (Model.activeMinTLOD + Model.CloudRecoveryTLOD[Model.activeProfile]) : Model.CloudRecoveryTLOD[Model.activeProfile]) && num >= 0f))) || ((Model.TLODAutoMethod[Model.activeProfile] >= 2 || Model.DecCloudQMethod[Model.activeProfile] == 1) && Model.gpuUsage <= Model.CloudRecoverGPUPct)))
			{
				Model.LODControllerBusy = false;
				Model.ResetCloudsLODs(ResetTLOD: false);
				Model.LODControllerBusy = true;
				if (Model.MinTLODExtra[Model.activeProfile] && !Model.MinTLODExtraActive && Model.OnGround)
				{
					Model.FPSSettleActive = true;
				}
				if (!Model.UseExpertOptions && Model.altAboveGnd > Model.AltTLODBase[Model.activeProfile] && TotalTicks < CloudRecoveryTickMark + 30)
				{
					CloudRecoveryExtraNonExpert += 20;
				}
			}
		}
		if ((Model.NightMode[Model.activeProfile] == 2 || Model.NightMode[Model.activeProfile] == 3) && Model.NightCloudReduceLevels[Model.activeProfile] > 0 && Model.ReductionSettingsDepth == 0 && !Model.DecCloudQActive)
		{
			if (Model.NightTime && !Model.NightCloudReduceLevelsActive)
			{
				if (Model.VrModeActive && Model.cloudQ_VR == Model.DefaultCloudQ_VR + (Model.IncCloudQActive ? 1 : 0))
				{
					MemoryManager memoryAccess47 = Model.MemoryAccess;
					int num30 = (Model.cloudQ_VR = Math.Max(0, Model.DefaultCloudQ_VR - Model.NightCloudReduceLevels[Model.activeProfile]));
					memoryAccess47.SetCloudQ(num30, VR: true);
					Model.NightCloudReduceLevelsActive = true;
					Model.IncCloudQActive = false;
				}
				if (!Model.VrModeActive && Model.cloudQ == Model.DefaultCloudQ + (Model.IncCloudQActive ? 1 : 0))
				{
					MemoryManager memoryAccess48 = Model.MemoryAccess;
					int num30 = (Model.cloudQ = Math.Max(0, Model.DefaultCloudQ - Model.NightCloudReduceLevels[Model.activeProfile]));
					memoryAccess48.SetCloudQ(num30);
					Model.NightCloudReduceLevelsActive = true;
					Model.IncCloudQActive = false;
				}
			}
			else if (!Model.NightTime && Model.NightCloudReduceLevelsActive)
			{
				if (Model.VrModeActive && Model.cloudQ_VR < Model.DefaultCloudQ_VR)
				{
					MemoryManager memoryAccess49 = Model.MemoryAccess;
					int num30 = (Model.cloudQ_VR = Model.DefaultCloudQ_VR);
					memoryAccess49.SetCloudQ(num30, VR: true);
					Model.NightCloudReduceLevelsActive = false;
				}
				if (!Model.VrModeActive && Model.cloudQ < Model.DefaultCloudQ)
				{
					MemoryManager memoryAccess50 = Model.MemoryAccess;
					int num30 = (Model.cloudQ = Model.DefaultCloudQ);
					memoryAccess50.SetCloudQ(num30);
					Model.NightCloudReduceLevelsActive = false;
				}
			}
		}
		float num70;
		if (!Model.CustomAutoOLOD[Model.activeProfile])
		{
			if (Model.VrModeActive && Model.olod != Model.DefaultOLOD_VR)
			{
				num70 = Model.DefaultOLOD_VR;
			}
			else
			{
				num70 = ((Model.VrModeActive || Model.olod == Model.DefaultOLOD) ? Model.olod : Model.DefaultOLOD);
			}
		}
		else if (Model.altAboveGnd < Model.AltOLODBase[Model.activeProfile])
		{
			num70 = Model.activeOLODAtBase;
		}
		else if (Model.altAboveGnd > Model.AltOLODTop[Model.activeProfile])
		{
			num70 = Model.activeOLODAtTop;
		}
		else
		{
			num70 = Model.activeOLODAtBase + (Model.activeOLODAtTop - Model.activeOLODAtBase) * (Model.altAboveGnd - Model.AltOLODBase[Model.activeProfile]) / (Model.AltOLODTop[Model.activeProfile] - Model.AltOLODBase[Model.activeProfile]);
			if (Model.MinTLODReductionAmount == 0f)
			{
				if (Math.Abs(num70 - Model.olod) < (float)Model.FPSTolerance[Model.activeProfile])
				{
					num70 = Model.olod;
				}
				else
				{
					num70 = ((!(Math.Abs(num70 - Model.olod) > (float)(Model.FPSTolerance[Model.activeProfile] * 2))) ? (Model.olod + (float)(Math.Sign(num70 - Model.olod) * Model.FPSTolerance[Model.activeProfile])) : (Model.olod + (float)(Math.Sign(num70 - Model.olod) * Model.FPSTolerance[Model.activeProfile] * 2)));
				}
			}
		}
		num70 = (float)Math.Round((Model.MinTLOD[Model.activeProfile] - Model.MinTLODReductionAmount) / Model.MinTLOD[Model.activeProfile] * num70);
		if (num70 != Model.olod && Math.Abs(num70 - Model.olod) >= 1f)
		{
			Model.olod = Model.MemoryAccess.SetOLOD(num70);
			Model.olod_step = true;
		}
		else
		{
			Model.olod_step = false;
		}
		goto IL_6833;
		IL_3130:
		if (Model.MinTLODExtraAmount == 0f)
		{
			Model.MinTLODExtraActive = false;
		}
		else
		{
			Model.MinTLODExtraActive = true;
		}
		if (flag3)
		{
			if (Model.tlod < CalculateTLOD(rawResult: true) && deltaFPSTrendShort.Average() >= deltaFPSTrendMin / 5f && num2 >= deltaFPSTrendMin / 5f && deltaFPSRawlast >= deltaFPSTrendMin / 5f && num >= deltaFPSTrendMin / 5f && (!criticalFlightPhaseActive || Model.FreeTLOD[Model.activeProfile]))
			{
				newTLOD = ((Model.tlod < Model.recentTLODPeak) ? Math.Min(TLODStep * 4f, Model.recentTLODPeak - Model.tlod) : Math.Min(TLODStep, ++Model.fixedTargetFPSIncreaseStep));
			}
			else if (Model.tlod > Model.MinTLOD[Model.activeProfile] && num2 <= deltaFPSTrendMin * 2f / 5f && (num2 >= deltaFPSTrendMin * 2f || deltaFPSRawlast <= deltaFPSTrendMin * 2f / 5f || deltaFPSTrendShort.Average() < deltaFPSTrendMin / 2f))
			{
				float num71 = 1f;
				float num72 = 4f;
				newTLOD = 0f - Math.Min((num2 + deltaFPSRawlast) / 2f / deltaFPSTrendMin * 5f * num71 + num72, TLODStep * 8f / (float)(cameraStateCockpit ? 1 : 2));
			}
			else
			{
				Model.fixedTargetFPSIncreaseStep = 0L;
			}
			if (TLODTrendLast == TrendState.Up && newTLOD <= 0f)
			{
				Model.recentTLODPeak = Math.Min(Model.tlod, Model.MaxTLOD[Model.activeProfile] - TLODStep);
			}
			else if (Model.recentTLODPeak < Model.MaxTLOD[Model.activeProfile] && Model.tlod < Model.recentTLODPeak)
			{
				Model.recentTLODPeak = (float)Math.Floor(Ewma(Model.recentTLODPeak, Model.tlod, (VerticalSum() < -2) ? 0.1f : 0.05f));
			}
			if (Math.Round(newTLOD) > 0.0)
			{
				TLODTrendLast = TrendState.Up;
			}
			else if (Math.Round(newTLOD) < 0.0)
			{
				TLODTrendLast = TrendState.Down;
			}
			else
			{
				TLODTrendLast = TrendState.Steady;
			}
			deltaFPSRawlast = num2;
		}
		else if (Model.TLODAutoMethod[Model.activeProfile] == 0)
		{
			newTLOD = (float)Math.Sign(num) * Math.Min((float)Math.Pow(Math.Abs(num) / ((averageFPSRounded > 1f) ? averageFPSRounded : 1f) * TLODStep * 10f, 1.399999976158142), TLODStep * 2f);
		}
		else if (FPSToleranceExceeded)
		{
			newTLOD = (float)Math.Sign(num) * TLODStep * (float)((!(Math.Abs(num) >= num3 * 2f)) ? 1 : 2);
		}
		if ((((Model.FgModeEnabled || Model.ActiveGraphicsMode == "Man FG") && !Model.ActiveWindowMSFS) || Model.SpikeDetected) && newTLOD > 0f)
		{
			newTLOD = 0f;
		}
		if (((Model.ActiveGraphicsMode == "FG" && Model.FgModeMultiplier == 1) || (Model.ActiveGraphicsMode == "Man FG" && Model.ManFgMultiplier == 1)) && Model.fpsSourceRTSS && !Model.ActiveWindowMSFS)
		{
			newTLOD = 0f;
		}
		if (Model.MinTLODExtra[Model.activeProfile] && !Model.NightTimeTransiton)
		{
			if (Model.MinTLODExtraAmount > 0f && Math.Sign(newTLOD) == -1 && Model.tlod <= Model.MinTLOD[Model.activeProfile] * Model.MinTLODExtraCapMult[Model.activeProfile])
			{
				Model.MinTLODExtraAmount = Math.Max(Model.MinTLODExtraAmount + newTLOD, 0f);
			}
			if (!Model.VRAMSettingsReduceActive && !Model.VRAMReduceActive && Model.ReductionSettingsDepth == 0 && Model.MinTLODReductionAmount == 0f && Model.MinTLODExtraAmount < num26 && Math.Sign(newTLOD) == 1 && (Model.FreeTLOD[Model.activeProfile] || !criticalFlightPhaseActive))
			{
				Model.MinTLODExtraAmount = Math.Min(Model.MinTLODExtraAmount + newTLOD, num26);
				newTLOD = 0f;
			}
			SetActiveLODs();
		}
		if (Model.TLODReduceAfterGearDown && Model.LockTLODMax)
		{
			newTLOD = Math.Clamp(Model.tlod + newTLOD, Model.activeMinTLOD, Model.activeMaxTLOD);
		}
		else if (Model.altAboveGnd <= Model.AltTLODBase[Model.activeProfile])
		{
			newTLOD = Model.activeMinTLOD;
		}
		else
		{
			newTLOD = Math.Min(CalculateTLOD(), Model.tlod + newTLOD);
		}
		goto IL_37ff;
		IL_6833:
		TotalTicks++;
		if (Model.gpuUsage > 0f && Model.CloudIncreaseHysteresis > 10 && TotalTicks - CloudHysteresisDecayTick >= (Model.OnGround ? 1200 : 480))
		{
			Model.CloudIncreaseHysteresis = ((Model.CloudIncreaseHysteresis - 1 < 10) ? 10 : (Model.CloudIncreaseHysteresis - 1));
			CloudHysteresisDecayTick = TotalTicks;
		}
		altAboveGndLast = Model.altAboveGnd;
		groundSpeedLast = Model.groundSpeed;
		NightTimeLast = Model.NightTime;
		Model.LODControllerBusy = false;
		return;
		IL_2f45:
		if (!Model.VRAMSettingsReduceActive && Model.ReductionSettingsDepth == 0 && !FGInactive && (!criticalFlightPhaseActive || Model.FreeTLOD[Model.activeProfile]) && Model.IncCloudQ[Model.activeProfile] && !Model.IncCloudQActive && (Model.VrModeActive ? Model.cloudQ_VR : Model.cloudQ) < 3 && ((Model.gpuUsage > 0f && Model.gpuUsage <= Model.CloudIncreaseGPUMaxLoad - (float)Model.CloudIncreaseHysteresis) || (Model.gpuUsage <= 0f && Model.MinTLODExtraAmount == num26)))
		{
			if (!(Model.gpuUsage > 0f) || !(Model.gpuUsage <= Model.CloudIncreaseGPUMaxLoad - (float)Model.CloudIncreaseHysteresis) || !(deltaFPSTrendShort.Average() >= deltaFPSTrendMin / 5f))
			{
				bool num73;
				if (!flag3)
				{
					num73 = num >= Model.TargetFPS * 0.1f;
				}
				else
				{
					if (!Model.fpsRawTrend.IsFull || !(Model.fpsRawTrend.GetFilledArray().Average() >= Model.TargetFPS + deltaFPSTrendMin / 5f) || !(deltaFPSTrendShort.Average() >= deltaFPSTrendMin / 5f))
					{
						goto IL_3130;
					}
					num73 = num2 >= deltaFPSTrendMin / 5f;
				}
				if (!num73)
				{
					goto IL_3130;
				}
			}
			AutoIncreaseCloudQuality();
		}
		goto IL_3130;
	}

	public void CalculateTLODRange()
	{
		Model.rangeMinTLOD = Model.activeMinTLOD;
		if (Model.TLODReduceAfterGearDown && Model.LockTLODMax)
		{
			Model.rangeMaxTLOD = Model.activeMaxTLOD;
			return;
		}
		if (Model.FreeTLOD[Model.activeProfile] || Model.altAboveGnd >= Model.AltTLODTop[Model.activeProfile] || Model.TLODAutoMethod[Model.activeProfile] >= 2)
		{
			Model.IsAppPriorityFPS = true;
			Model.rangeMaxTLOD = Model.activeMaxTLOD;
			return;
		}
		Model.IsAppPriorityFPS = false;
		if (Model.altAboveGnd >= Model.AltTLODBase[Model.activeProfile])
		{
			Model.rangeMaxTLOD = (float)Math.Round(Math.Min(CalculateTLOD(), Model.activeMaxTLOD));
		}
		else
		{
			Model.rangeMaxTLOD = Model.activeMinTLOD;
		}
	}

	public void AutoIncreaseCloudQuality()
	{
		if (!Model.NightTime || (Model.NightMode[Model.activeProfile] != 2 && Model.NightMode[Model.activeProfile] != 3) || Model.NightCloudReduceLevels[Model.activeProfile] <= 0)
		{
			int num = (Model.VrModeActive ? Model.cloudQ_VR : Model.cloudQ);
			int num2 = Math.Clamp(num + 1, 0, 3);
			if (Model.VrModeActive)
			{
				Model.cloudQ_VR = num2;
			}
			else
			{
				Model.cloudQ = num2;
			}
			Model.MemoryAccess.SetCloudQ(num2, Model.VrModeActive);
			Model.IncCloudQActive = true;
			Model.FPSSettleActive = true;
			Logger.Log(LogLevel.Information, "LODController:RunTick", "Increase Cloud Quality auto activated: " + ServiceModel.SettingQualityText(num) + " → " + ServiceModel.SettingQualityText(num2) + ((Model.gpuUsage > 0f) ? $" (hysteresis {Model.CloudIncreaseHysteresis}%)" : ""));
		}
	}

	public void AutoDecreaseCloudQuality()
	{
		int num = (Model.VrModeActive ? Model.cloudQ_VR : Model.cloudQ);
		int num2 = Math.Clamp(num - 1, 0, 3);
		if (Model.VrModeActive)
		{
			Model.cloudQ_VR = num2;
		}
		else
		{
			Model.cloudQ = num2;
		}
		Model.MemoryAccess.SetCloudQ(num2, Model.VrModeActive);
		Model.IncCloudQActive = false;
		Model.FPSSettleActive = true;
		MinTLODExtraSeekLastTickMark = TotalTicks;
		string value;
		if (Model.FixedTargetFPS)
		{
			value = ((Model.gpuUsage > Model.CloudIncreaseGPUMaxLoad) ? $"GPU overload ({Model.gpuUsage}%)" : (((Model.TLODAutoMethod[Model.activeProfile] == 3) ? "FPS cap" : "Fixed FPS") + " breach"));
		}
		else
		{
			value = "FPS drop";
		}
		if (Model.gpuUsage > 0f)
		{
			Model.CloudIncreaseHysteresis = Math.Min(25, Model.CloudIncreaseHysteresis + 4);
		}
		Logger.Log(LogLevel.Information, "LODController:RunTick", $"Increase Cloud Quality deactivated due to {value}: {ServiceModel.SettingQualityText(num)} → {ServiceModel.SettingQualityText(num2)}" + ((Model.gpuUsage > 0f) ? $" (hysteresis {Model.CloudIncreaseHysteresis}%)" : ""));
	}

	public int VerticalSum()
	{
		return verticalStats.Sum();
	}

	public float VSAverage()
	{
		return verticalStatsVS.Average();
	}

	public bool gpuzRunning()
	{
		if (gpuzGPULoadSensorIndex >= 0)
		{
			return true;
		}
		if (gpuzGPULoadSensorIndex == -1 && gpuz.Open())
		{
			Logger.Log(LogLevel.Information, "LODController:gpuzRunning", "GPU-Z companion app running");
			gpuzGPULoadSensorIndex = 0;
			while (gpuz.SensorName(gpuzGPULoadSensorIndex) != string.Empty && gpuz.SensorName(gpuzGPULoadSensorIndex) != "GPU Load")
			{
				gpuzGPULoadSensorIndex++;
			}
			if (gpuz.SensorName(gpuzGPULoadSensorIndex) != "GPU Load")
			{
				Logger.Log(LogLevel.Information, "LODController:gpuzRunning", "GPU-Z GPU Load sensor not found");
				gpuzGPULoadSensorIndex = -1;
			}
			else
			{
				Logger.Log(LogLevel.Information, "LODController:gpuzRunning", "GPU-Z GPU Load sensor found");
			}
			gpuzGPUMemoryUsedSensorIndex = 0;
			while (gpuz.SensorName(gpuzGPUMemoryUsedSensorIndex) != string.Empty && gpuz.SensorName(gpuzGPUMemoryUsedSensorIndex) != "Memory Used" && gpuz.SensorName(gpuzGPUMemoryUsedSensorIndex) != "Memory Used (Dedicated)")
			{
				gpuzGPUMemoryUsedSensorIndex++;
			}
			if (gpuz.SensorName(gpuzGPUMemoryUsedSensorIndex) != "Memory Used" && gpuz.SensorName(gpuzGPUMemoryUsedSensorIndex) != "Memory Used (Dedicated)")
			{
				Logger.Log(LogLevel.Information, "LODController:gpuzRunning", "GPU-Z GPU Memory Used sensor not found");
				if (Model.LogPlus)
				{
					gpuzGPUMemoryUsedSensorIndex = 0;
					while (gpuz.SensorName(gpuzGPUMemoryUsedSensorIndex) != string.Empty)
					{
						Logger.Log(LogLevel.Information, "LODController:gpuzRunning", $"Sensor # {gpuzGPUMemoryUsedSensorIndex}, Name : {gpuz.SensorName(gpuzGPUMemoryUsedSensorIndex)}");
						gpuzGPUMemoryUsedSensorIndex++;
					}
				}
				gpuzGPUMemoryUsedSensorIndex = -1;
			}
			else
			{
				Logger.Log(LogLevel.Information, "LODController:gpuzRunning", "GPU-Z GPU Memory Used sensor found");
				Logger.Log(LogLevel.Information, "LODController:gpuzRunning", $"GPU-Z Initial Memory Used: {(int)gpuz.SensorValue(gpuzGPUMemoryUsedSensorIndex)} MB");
				try
				{
					using Factory1 factory = new Factory1();
					Adapter1[] adapters = factory.Adapters1;
					foreach (Adapter1 adapter in adapters)
					{
						if (adapter.Description.Description.Replace(" ", "") == Model.gpuModel.Replace(" ", ""))
						{
							gpuTotalMemory = adapter.Description.DedicatedVideoMemory / 1048576;
							if (gpuTotalMemory >= 10)
							{
								gpuModelDetected = true;
							}
							break;
						}
					}
				}
				catch (Exception value)
				{
					Logger.Log(LogLevel.Error, "LODController:gpuzRunning", $"Exception {value} encountered");
				}
				if (gpuModelDetected)
				{
					Logger.Log(LogLevel.Information, "LODController:gpuzRunning", $"System Reported Total GPU Memory: {gpuTotalMemory} MB");
					Logger.Log(LogLevel.Information, "LODController:gpuzRunning", $"GPU Initial Memory Usage: {(int)(gpuz.SensorValue(gpuzGPUMemoryUsedSensorIndex) / (double)gpuTotalMemory * 100.0)}%");
				}
				else
				{
					Logger.Log(LogLevel.Information, "LODController:gpuzRunning", "GPU model matching UserCfg.opt not found. VRAM usage will not be available.");
				}
			}
			return true;
		}
		return false;
	}

	public void SetActiveLODs()
	{
		float num = (Model.VrModeActive ? Model.DefaultTLOD_VR : Model.DefaultTLOD);
		if (!Model.VrModeActive)
		{
			_ = Model.DefaultOLOD;
		}
		else
		{
			_ = Model.DefaultOLOD_VR;
		}
		float num2 = (Model.UseExpertOptions ? Model.MaxTLOD[Model.activeProfile] : (Math.Min(400f, num * (Model.FlightTypeIFR ? 2f : 2.5f)) * ((Model.FPSSettleInitial || Model.TLODAutoMethod[Model.activeProfile] == 3) ? 0.5f : 1f)));
		float num3 = (Model.UseExpertOptions ? (Model.MaxTLOD[Model.activeProfile] / 2f) : Math.Min(200f, num * ((Model.FlightTypeIFR || Model.FPSSettleInitial || Model.TLODAutoMethod[Model.activeProfile] == 3) ? 1f : 1.25f)));
		TLODStep = Math.Max(2f, Model.FPSTolerance[Model.activeProfile]);
		Model.SetActiveOLODs();
		if (Model.UseExpertOptions)
		{
			MinTLODNoExtra = Model.MinTLOD[Model.activeProfile];
		}
		else
		{
			MinTLODNoExtra = (Model.MinTLOD[0] = Math.Clamp(num * (Model.FlightTypeIFR ? 0.5f : 1f) * (Model.AutoTargetFPS ? 1f : 0.5f), 10f, 150f));
			Model.AltTLODBase[0] = (Model.FlightTypeIFR ? 1000f : 100f);
			Model.MaxTLOD[0] = num2;
			Model.AltTLODTop[0] = (float)Math.Round(Model.AltTLODBase[0] + (Model.FlightTypeIFR ? Model.defaultAvgDesRate_IFR : Model.defaultAvgDesRate_VFR) * Math.Max(10f, Model.MaxTLOD[0] * ((Model.TLODAutoMethod[0] == 3) ? Model.MaxTLODExtraCapMult[0] : 1f) - Model.MinTLOD[0]) / (60f * (float)Model.FPSTolerance[0]));
			if (Model.gpuUsage > 0f)
			{
				Model.DecCloudQMethod[0] = 1;
				Model.CloudDecreaseGPUPct = 98f;
				Model.CloudRecoverGPUPct = 80f;
			}
			else
			{
				Model.DecCloudQMethod[0] = 0;
				Model.CloudRecoveryTLOD[Model.activeProfile] = (float)Math.Min(Math.Round(2f * (Model.activeMinTLOD + Model.activeMaxTLOD) / 5f), Model.activeMinTLOD + 50f) + (float)CloudRecoveryExtraNonExpert;
			}
			if (Model.DecCloudQMethod[0] != DecCloudQActiveLast)
			{
				Model.ResetCloudsLODs();
			}
			DecCloudQActiveLast = Model.DecCloudQMethod[0];
		}
		if (Model.MaxTLODHalvedNight[Model.activeProfile] && Model.NightTime != NightTimeLast)
		{
			Model.NightTimeTransiton = true;
			Model.MinTLODExtraIterations = 0;
		}
		if (Model.NightTimeTransiton)
		{
			if (Model.TLODAutoMethod[Model.activeProfile] < 2)
			{
				if (Model.NightTime)
				{
					if (Model.activeMaxTLOD > num3)
					{
						Model.activeMaxTLOD = (Model.activeMaxTLOD -= TLODStep);
					}
					if (Model.activeMaxTLOD <= num3 || Model.tlod <= num3)
					{
						Model.activeMaxTLOD = num3;
						Model.NightTimeTransiton = false;
					}
					if (Model.MinTLODExtraAmount > Model.activeMaxTLOD - MinTLODNoExtra)
					{
						Model.MinTLODExtraAmount = Model.activeMaxTLOD - MinTLODNoExtra;
					}
				}
				else
				{
					Model.activeMaxTLOD = num2;
					Model.NightTimeTransiton = false;
				}
			}
			else if (Model.NightTime)
			{
				if (Model.activeMinTLOD <= MinTLODNoExtra && (Model.activeMaxTLOD <= Math.Max(num3, MinTLODNoExtra) || Model.altAboveGnd <= Model.AltTLODBase[Model.activeProfile]))
				{
					Model.NightTimeTransiton = false;
				}
				if (Model.activeMinTLOD > MinTLODNoExtra)
				{
					Model.activeMinTLOD -= TLODStep;
				}
				if (Model.activeMaxTLOD > Math.Max(num3, MinTLODNoExtra))
				{
					Model.activeMaxTLOD = (Model.activeMaxTLOD -= TLODStep);
				}
				if (Model.activeMinTLOD < MinTLODNoExtra)
				{
					Model.activeMinTLOD = MinTLODNoExtra;
				}
				if (Model.activeMaxTLOD < Math.Max(num3, MinTLODNoExtra))
				{
					Model.activeMaxTLOD = Math.Max(num3, MinTLODNoExtra);
				}
			}
			else
			{
				Model.activeMinTLOD = MinTLODNoExtra;
				if (Model.MinTLODReductionAmount == 0f)
				{
					Model.MinTLODExtraAmount = 0f;
				}
				else
				{
					Model.activeMinTLOD += Model.MinTLODExtraAmount;
				}
				if (Model.activeMaxTLOD >= (float)Math.Round((Model.activeMinTLOD - Model.MinTLODReductionAmount) / Model.activeMinTLOD * num2 / 5f) * 5f || Model.altAboveGnd <= Model.AltTLODBase[Model.activeProfile])
				{
					Logger.Log(LogLevel.Information, "LODController:SetActiveLODs", "Recalibrating TLOD Min/Base + on transition to day time");
					if (Model.OnGround && Model.groundSpeed <= 5f)
					{
						if (Model.TLODAutoMethod[Model.activeProfile] >= 2)
						{
							Model.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
							MinTLODExtraReSeek = false;
							MinTLODExtraSeekLastTickMark = TotalTicks;
						}
						else
						{
							Model.MinTLODExtraIterations = 8;
							Model.FPSSettleActive = true;
						}
					}
					Model.activeMaxTLOD = (float)Math.Round(Model.activeMinTLOD / (Model.activeMinTLOD - Model.MinTLODReductionAmount) * num2 / 5f) * 5f;
					Model.NightTimeTransiton = false;
				}
				if (Model.activeMaxTLOD < num2)
				{
					Model.activeMaxTLOD += TLODStep;
				}
				if (Model.activeMaxTLOD > num2)
				{
					Model.activeMaxTLOD = num2;
				}
			}
		}
		else if (Model.MaxTLODHalvedNight[Model.activeProfile] && Model.NightTime)
		{
			if (Model.TLODAutoMethod[Model.activeProfile] < 2)
			{
				Model.activeMinTLOD = MinTLODNoExtra + Model.MinTLODExtraAmount;
			}
			else
			{
				Model.activeMinTLOD = MinTLODNoExtra;
			}
			Model.activeMaxTLOD = num3;
			Model.MinTLODExtraActive = false;
			Model.MinTLODExtraSeeking = false;
			Model.MinTLODExtraIterations = 0;
			Model.TLODExtraMtnsActive = false;
			Model.TLODExtraMtnsAmountResidual = 0f;
		}
		else
		{
			Model.activeMinTLOD = MinTLODNoExtra + Model.MinTLODExtraAmount;
			if (Model.UseExpertOptions)
			{
				Model.activeMaxTLOD = Model.MaxTLOD[Model.activeProfile];
				if (Model.TLODExtraMtns[Model.activeProfile] && Model.altitude - Model.altAboveGnd >= (float)Model.TLODExtraMtnsTriggerAlt[Model.activeProfile])
				{
					Model.TLODExtraMtnsAmountResidual = Math.Min(Model.TLODExtraMtnsAmountResidual + Math.Max(TLODStep, 5f), Model.TLODExtraMtnsAmount[Model.activeProfile]);
					Model.TLODExtraMtnsActive = true;
					TLODExtraMtnsTimer = 300;
				}
				else if (Model.TLODExtraMtnsActive && --TLODExtraMtnsTimer < 0)
				{
					Model.TLODExtraMtnsActive = false;
				}
				else if (!Model.TLODExtraMtnsActive && Model.TLODExtraMtnsAmountResidual > 0f)
				{
					Model.TLODExtraMtnsAmountResidual -= Math.Max(TLODStep, 5f);
					if (Model.TLODExtraMtnsAmountResidual < 0f)
					{
						Model.TLODExtraMtnsAmountResidual = 0f;
					}
				}
				if (Model.TLODAutoMethod[Model.activeProfile] >= 2 && Model.MinTLODExtra[Model.activeProfile])
				{
					Model.activeMaxTLOD += Model.MinTLODExtraAmount;
				}
				else
				{
					Model.activeMaxTLOD += Model.TLODExtraMtnsAmountResidual;
				}
			}
			else
			{
				Model.activeMaxTLOD = num2;
				if (Model.TLODAutoMethod[Model.activeProfile] >= 2 && Model.MinTLODExtra[Model.activeProfile])
				{
					Model.activeMaxTLOD += Model.MinTLODExtraAmount;
				}
			}
		}
		if (Model.TLODAutoMethod[Model.activeProfile] >= 2 && Model.MinTLODExtra[Model.activeProfile] && !Model.NightTimeTransiton)
		{
			Model.activeMaxTLOD = (float)Math.Round((Model.activeMinTLOD - Model.MinTLODReductionAmount) / Model.activeMinTLOD * Model.activeMaxTLOD);
		}
		Model.activeMinTLOD = (float)Math.Round(Model.activeMinTLOD - ((!Model.UseExpertOptions && !Model.AutoTargetFPS && Model.FlightTypeIFR) ? 0f : Model.MinTLODReductionAmount) + (Model.FreeTLOD[Model.activeProfile] ? Model.TLODExtraMtnsAmountResidual : 0f));
		if (Model.TLODAutoMethod[Model.activeProfile] < 2 && (Model.MinTLODReductionAmount > 0f || Model.ReductionSettingsDepth > 0))
		{
			ServiceModel model = Model;
			float recentTLODPeak = (Model.activeMaxTLOD = Model.activeMinTLOD);
			model.recentTLODPeak = recentTLODPeak;
		}
		Model.activeMinTLOD = Math.Clamp(Model.activeMinTLOD, Model.SimMinLOD, Model.SimMaxLOD + ((Model.TLODAutoMethod[Model.activeProfile] >= 2 && Model.MinTLODExtraIterations > 0) ? Model.TLODBasePlusSeekHeadroom : 0f));
		Model.activeMaxTLOD = Math.Clamp(Model.activeMaxTLOD - Model.SpikeTLODReductionAmount, Model.SimMinLOD, Model.SimMaxLOD + ((Model.TLODAutoMethod[Model.activeProfile] >= 2 && Model.MinTLODExtraIterations > 0) ? Model.TLODBasePlusSeekHeadroom : 0f));
		if (Model.activeMinTLOD > Model.activeMaxTLOD)
		{
			Model.activeMinTLOD = Model.activeMaxTLOD;
		}
	}

	private float CalculateTLOD(bool rawResult = false)
	{
		if (rawResult)
		{
			if (Model.FreeTLOD[Model.activeProfile] || Model.altAboveGnd >= Model.AltTLODTop[Model.activeProfile])
			{
				return Model.activeMaxTLOD;
			}
			float num = Model.MinTLOD[Model.activeProfile] * (Model.MinTLODExtra[Model.activeProfile] ? Model.MinTLODExtraCapMult[Model.activeProfile] : 1f);
			if (Model.altAboveGnd <= Model.AltTLODBase[Model.activeProfile])
			{
				return num;
			}
			return num + (Model.activeMaxTLOD - num) * (float)Math.Pow((Model.altAboveGnd - Model.AltTLODBase[Model.activeProfile]) / (Model.AltTLODTop[Model.activeProfile] - Model.AltTLODBase[Model.activeProfile]), Model.LOSCorrection[Model.activeProfile] ? 0.5f : 1f);
		}
		return Model.activeMinTLOD + (Model.activeMaxTLOD - Model.activeMinTLOD) * (float)Math.Pow((Model.altAboveGnd - Model.AltTLODBase[Model.activeProfile]) / (Model.AltTLODTop[Model.activeProfile] - Model.AltTLODBase[Model.activeProfile]), Model.LOSCorrection[Model.activeProfile] ? 0.5f : 1f);
	}

	public float GetAverageFPS(bool unrounded = false, bool useRaw = false)
	{
		float num = (useRaw ? Model.fpsRaw : Model.fpsAverage);
		if (Model.fpsSourceRTSS)
		{
			if (Model.LsModeEnabled)
			{
				num *= (float)Model.LsModeMultiplier;
			}
			else if (Model.VrModeActive && Model.OfxrModeEnabled)
			{
				if (!Model.OfxrStatusJsonActive || Model.OfxrOutputFps <= 0f)
				{
					num *= (float)Model.OfxrModeMultiplier;
				}
			}
			else if (Model.VrModeActive && Model.VxrModeEnabled)
			{
				if (!Model.VxrStatusJsonActive || Model.VxrOutputFps <= 0f)
				{
					num *= (float)Model.VxrModeMultiplier;
				}
			}
			else if (Model.ActiveGraphicsMode == "Man FG")
			{
				if (!Model.ActiveWindowMSFS)
				{
					num *= (float)Model.ManFgMultiplier;
				}
				if (Model.ManFgMultiplier == 1)
				{
					Model.DynFgCurrentMultiplier = ((!(Model.fpsRawMSFS > 0f)) ? 1 : Math.Clamp((int)Math.Round(Model.fpsRaw / Model.fpsRawMSFS), 1, 6));
				}
			}
			else if (Model.FgModeEnabled)
			{
				if (!Model.ActiveWindowMSFS)
				{
					num *= (float)Model.FgModeMultiplier;
				}
				if (Model.FgModeMultiplier == 1)
				{
					Model.DynFgCurrentMultiplier = ((!(Model.fpsRawMSFS > 0f)) ? 1 : Math.Clamp((int)Math.Round(Model.fpsRaw / Model.fpsRawMSFS), 1, 6));
				}
			}
			else if (Model.MsmModeEnabled)
			{
				num *= (float)Model.MsmMultiplier;
			}
		}
		else if (Model.VrModeActive && Model.OfxrModeEnabled)
		{
			if (!Model.OfxrStatusJsonActive || Model.OfxrOutputFps <= 0f)
			{
				num *= (float)Model.OfxrModeMultiplier;
			}
		}
		else if (Model.VrModeActive && Model.VxrModeEnabled)
		{
			if (!Model.VxrStatusJsonActive || Model.VxrOutputFps <= 0f)
			{
				num *= (float)Model.VxrModeMultiplier;
			}
		}
		else if (Model.ActiveGraphicsMode == "Man FG")
		{
			num *= (float)Model.ManFgMultiplier;
		}
		else if (Model.LsModeEnabled)
		{
			num *= (float)Model.LsModeMultiplier;
		}
		else if (Model.FgModeEnabled)
		{
			num *= (float)Model.FgModeMultiplier;
		}
		else if (Model.FgModeFSREnabled)
		{
			num *= 2f;
		}
		else if (Model.MsmModeEnabled)
		{
			num *= (float)Model.MsmMultiplier;
		}
		if (!unrounded)
		{
			return (float)Math.Round(num);
		}
		return num;
	}

	private void ResetOFXRStates()
	{
		Model.OfxrModeEnabled = false;
		Model.OfxrDesynced = false;
		Model.OfxrAutoMultiplier = 1;
		Model.OfxrOverSpeed = false;
		lastOfxrPaused = false;
		lastOfxrMultiplier = 0;
		lastOfxrOverSpeed = false;
		lastOfxrDesynced = false;
		ofxrDesyncCount = 0;
		lastOfxrAutoMultiplier = 0;
	}

	private void ResetVXRStates()
	{
		Model.VxrModeEnabled = false;
		Model.VxrMode = "";
		Model.VxrModeMultiplier = 0;
		Model.VxrEffectiveMultiplier = 1f;
		Model.VxrUnevenPacing = false;
		Model.VxrStatusJsonActive = false;
		lastVxrPaused = false;
		lastVxrStandby = false;
		lastVxrMultiplier = 0;
		lastVxrMode = "";
		lastVxrUnevenPacing = false;
	}

	private void ResetVRModeStates()
	{
		ResetOFXRStates();
		ResetVXRStates();
		lastVrFgConflict = false;
		Model.VrFgConflictDetected = false;
		Model.VrFgConflictPromptRequired = false;
		Model.VrFgConflictPromptActive = false;
	}

	private void GetMSFSState()
	{
		if (Model.VrModeActive = Model.MemoryAccess.IsVrModeActive())
		{
			Model.LsModeEnabled = false;
			Model.MsmModeEnabled = false;
			Model.FgModeEnabled = false;
			Model.FgModeFSREnabled = false;
			bool forceDetect = Model.ActiveGraphicsMode != "VR" && Model.ActiveGraphicsMode != "OFXR" && !Model.ActiveGraphicsMode.StartsWith("VXR");
			bool flag2 = Process.GetProcessesByName("OFXRBridgeTray").Length != 0 && Model.IsOFXRArmed(Model.VrModeActive, forceDetect);
			bool flag3 = Model.ReadVXRStatusJson(Model.VrModeActive, forceDetect);
			if (flag2 & flag3)
			{
				ResetOFXRStates();
				ResetVXRStates();
				Model.VrFgConflictDetected = true;
				if (!lastVrFgConflict)
				{
					lastVrFgConflict = true;
					Model.VrFgConflictPromptRequired = true;
					Logger.Log(LogLevel.Error, "LODController:GetMSFSState", "CRITICAL CONFLICT: Both OFXR and VectorXR Frame Generation layers are active simultaneously in MSFS! Dropping to standard VR mode.");
				}
			}
			else if (flag2)
			{
				lastVrFgConflict = false;
				Model.VrFgConflictDetected = false;
				Model.VrFgConflictPromptRequired = false;
				Model.VrFgConflictPromptActive = false;
				ResetVXRStates();
				Model.OfxrModeEnabled = true;
				bool flag4 = Model.IsOFXRPaused();
				if (flag4 != lastOfxrPaused)
				{
					lastOfxrPaused = flag4;
					Logger.Log(LogLevel.Information, "LODController:GetMSFSState", flag4 ? "OFXR paused" : "OFXR unpaused");
				}
				int num = Model.GetOFXRModeMultiplier(forceDetect);
				if (num != lastOfxrMultiplier)
				{
					if (lastOfxrMultiplier != 0)
					{
						Logger.Log(LogLevel.Information, "LODController:GetMSFSState", $"OFXR multiplier changed to {num}X");
						Model.fpsAverageReset = true;
					}
					lastOfxrMultiplier = num;
				}
				if (Model.LogPlus && Model.OfxrStatusJsonActive && Model.OfxrAppFps > 0f && Model.OfxrOutputFps > 0f)
				{
					Logger.Log(LogLevel.Debug, "LODController:GetMSFSState", $"OFXR status.json: AppFps={Model.OfxrAppFps:F0}, OutputFps={Model.OfxrOutputFps:F0}, calcMult={Math.Round(Model.OfxrOutputFps / Model.OfxrAppFps, 1):F1}");
				}
				int num2 = ((num > 0) ? (Model.OfxrMaxFPS / num) : Model.OfxrMaxFPS);
				float num3 = (lastOfxrOverSpeed ? ((float)num2) : ((float)num2 + 2f));
				float num4 = ((Model.OfxrStatusJsonActive && Model.OfxrAppFps > 0f) ? Model.OfxrAppFps : Model.fpsAverage);
				bool flag5 = !flag4 && num > 1 && num4 > num3;
				bool flag6 = false;
				if (!flag4 && Model.OfxrStatusJsonActive && Model.OfxrAppFps > 0f && Model.OfxrOutputFps > 0f)
				{
					float num5 = Model.OfxrOutputFps / Model.OfxrAppFps;
					int num6 = (int)Math.Round(num5);
					if (Model.OfxrModeAuto)
					{
						if (num6 >= 2 && num6 <= 4 && Math.Abs(num5 - (float)num6) <= 0.2f)
						{
							if (num6 != lastOfxrAutoMultiplier)
							{
								Logger.Log(LogLevel.Information, "LODController:GetMSFSState", $"OFXR Auto multiplier detected as {num6}X (App: {Model.OfxrAppFps:F1}, Output: {Model.OfxrOutputFps:F1} FPS)");
								lastOfxrAutoMultiplier = num6;
							}
							Model.OfxrAutoMultiplier = num6;
							num = num6;
						}
						else
						{
							flag6 = true;
						}
					}
					else
					{
						float val;
						if (Model.OfxrDisplayHz > 0f)
						{
							val = Model.OfxrDisplayHz;
						}
						else
						{
							val = ((num > 0) ? ((float)Model.OfxrMaxFPS) : 90f);
						}
						float num7 = Math.Min(Model.OfxrAppFps * (float)num, val);
						if (flag5 || (num7 > 0f && Model.OfxrOutputFps < num7 * 0.8f) || Math.Abs(num5 - (float)num) > 0.5f)
						{
							flag6 = true;
						}
					}
				}
				else if (!flag4 & flag5)
				{
					flag6 = true;
				}
				if (flag6)
				{
					if (ofxrDesyncCount < 2)
					{
						ofxrDesyncCount++;
					}
				}
				else
				{
					ofxrDesyncCount = 0;
				}
				bool flag7 = ofxrDesyncCount >= 2;
				if (flag7 != lastOfxrDesynced)
				{
					lastOfxrDesynced = flag7;
					if (flag7)
					{
						if (Model.OfxrStatusJsonActive && Model.OfxrAppFps > 0f)
						{
							Logger.Log(LogLevel.Information, "LODController:GetMSFSState", $"OFXR desync detected: App FPS ({Model.OfxrAppFps:F1}), Output FPS ({Model.OfxrOutputFps:F1}), expected {(Model.OfxrModeAuto ? "Auto integer" : $"{num}X ({Model.OfxrAppFps * (float)num:F1} FPS)")}. FPS settle event activated.");
						}
						else
						{
							Logger.Log(LogLevel.Information, "LODController:GetMSFSState", $"OFXR desync detected: Base FPS ({Model.fpsAverage:F0}) exceeds {num}X limit ({num2} FPS). Multiplier reduced to 1X. FPS settle event activated.");
						}
						Model.FPSSettleActive = true;
					}
					else
					{
						Logger.Log(LogLevel.Information, "LODController:GetMSFSState", $"OFXR desync recovered: Output FPS and pacing restored to {num}X and FPS trend data reset.");
						Model.fpsRawTrend.Reset();
						Model.fpsAverageReset = true;
					}
				}
				Model.OfxrDesynced = flag7;
				Model.OfxrOverSpeed = flag5;
				if (flag4)
				{
					Model.OfxrModeMultiplier = 1;
				}
				else if (flag7 && (!Model.OfxrStatusJsonActive || Model.OfxrModeAuto))
				{
					Model.OfxrModeMultiplier = 1;
				}
				else
				{
					Model.OfxrModeMultiplier = num;
				}
			}
			else if (flag3)
			{
				lastVrFgConflict = false;
				Model.VrFgConflictDetected = false;
				Model.VrFgConflictPromptRequired = false;
				Model.VrFgConflictPromptActive = false;
				ResetOFXRStates();
				Model.VxrModeEnabled = true;
				bool flag8 = Model.IsVXRPaused();
				if (flag8 != lastVxrPaused)
				{
					lastVxrPaused = flag8;
					Logger.Log(LogLevel.Information, "LODController:GetMSFSState", flag8 ? "VectorXR paused" : "VectorXR unpaused");
				}
				bool flag9 = Model.IsVXRStandby();
				if (flag9 != lastVxrStandby)
				{
					lastVxrStandby = flag9;
					if (flag9)
					{
						Logger.Log(LogLevel.Information, "LODController:GetMSFSState", "VectorXR standby (game holding refresh rate natively)");
					}
				}
				int vXRModeMultiplier = Model.GetVXRModeMultiplier(forceDetect);
				float num8 = vXRModeMultiplier;
				bool flag10 = false;
				if (!flag8 && !flag9 && Model.VxrGenerating && Model.VxrAppFps > 0f && Model.VxrDistinctFps > 0f)
				{
					num8 = Model.VxrDistinctFps / Model.VxrAppFps;
					if (!Model.VxrModeDynamic && num8 > (float)vXRModeMultiplier + 0.25f)
					{
						flag10 = true;
					}
				}
				Model.VxrEffectiveMultiplier = num8;
				Model.VxrUnevenPacing = flag10;
				if (flag8)
				{
					Model.VxrMode = "VXR ⏸";
					Model.VxrModeMultiplier = 1;
				}
				else if (flag9)
				{
					Model.VxrMode = "VXR Stby";
					Model.VxrModeMultiplier = 1;
				}
				else if (Model.VxrModeDynamic)
				{
					string text = ((num8 % 1f == 0f) ? $"{num8:F0}" : $"{num8:F1}");
					Model.VxrMode = "VXRDyn " + text + "X";
					Model.VxrModeMultiplier = (int)Math.Round(num8);
				}
				else if (Model.VxrModeAuto)
				{
					Model.VxrMode = (flag10 ? $"VXRAuto {num8:F1}X*" : $"VXRAuto {vXRModeMultiplier}X");
					Model.VxrModeMultiplier = vXRModeMultiplier;
				}
				else
				{
					Model.VxrMode = (flag10 ? $"VXR {num8:F1}X*" : $"VXR {vXRModeMultiplier}X");
					Model.VxrModeMultiplier = vXRModeMultiplier;
				}
				if (vXRModeMultiplier != lastVxrMultiplier || Model.VxrMode != lastVxrMode)
				{
					if (lastVxrMultiplier != 0)
					{
						Logger.Log(LogLevel.Information, "LODController:GetMSFSState", $"VectorXR active: {Model.VxrMode} (Target: {vXRModeMultiplier}X, Effective: {num8:F1}X, App: {Model.VxrAppFps:F1}, Distinct: {Model.VxrDistinctFps:F1} FPS)");
						Model.fpsAverageReset = true;
					}
					lastVxrMultiplier = vXRModeMultiplier;
					lastVxrMode = Model.VxrMode;
				}
				if (flag10 != lastVxrUnevenPacing)
				{
					lastVxrUnevenPacing = flag10;
					if (flag10)
					{
						Logger.Log(LogLevel.Information, "LODController:GetMSFSState", $"VectorXR uneven pacing active (*): App FPS ({Model.VxrAppFps:F1}) below target ({Model.VxrHoldFps:F1} FPS). Effective ratio: {num8:F1}X");
					}
					else
					{
						Logger.Log(LogLevel.Information, "LODController:GetMSFSState", $"VectorXR pacing restored: App FPS ({Model.VxrAppFps:F1}) holding target ({Model.VxrHoldFps:F1} FPS).");
					}
				}
				if (Model.LogPlus && Model.VxrStatusJsonActive && Model.VxrAppFps > 0f && Model.VxrOutputFps > 0f)
				{
					Logger.Log(LogLevel.Debug, "LODController:GetMSFSState", $"VXR relay: AppFps={Model.VxrAppFps:F1}, OutputFps={Model.VxrOutputFps:F1}, DistinctFps={Model.VxrDistinctFps:F1}, HoldFps={Model.VxrHoldFps:F1}, EffectiveMult={num8:F1}X");
				}
			}
			else
			{
				ResetVRModeStates();
			}
		}
		else
		{
			ResetVRModeStates();
			if (Process.GetProcessesByName("LosslessScaling").Length != 0)
			{
				Model.LsModeEnabled = true;
				Model.LsModeMultiplier = Model.GetLSModeMultiplier();
				Model.MsmModeEnabled = false;
				Model.FgModeEnabled = false;
				Model.FgModeFSREnabled = false;
			}
			else
			{
				Model.LsModeEnabled = false;
				Model.MemoryAccess.IsFgModeEnabled();
				if (Process.GetProcessesByName("Mfg Super Motion").Length != 0)
				{
					Model.MsmModeEnabled = true;
					Model.MsmMultiplier = Model.GetMSMMultiplier();
				}
				else
				{
					Model.MsmModeEnabled = false;
				}
			}
		}
		bool activeWindowMSFS = Model.ActiveWindowMSFS;
		string activeGraphicsMode = Model.ActiveGraphicsMode;
		Model.ActiveWindowMSFS = Model.MemoryAccess.IsActiveWindowMSFS();
		if (Model.ActiveWindowMSFS != activeWindowMSFS)
		{
			Logger.Log(LogLevel.Information, "LODController:GetMSFSState", Model.ActiveWindowMSFS ? "Active window changed to MSFS" : "Active window no longer MSFS");
		}
		if (Model.VrModeActive)
		{
			if (Model.OfxrModeEnabled)
			{
				Model.ActiveGraphicsMode = "OFXR";
				if (!Model.AutoTargetFPS)
				{
					Model.TargetFPS = (Model.FlightTypeIFR ? Model.TargetFPS_OFXR : Model.TargetFPS_OFXR_VFR);
				}
			}
			else if (Model.VxrModeEnabled)
			{
				Model.ActiveGraphicsMode = "VXR";
				if (!Model.AutoTargetFPS)
				{
					Model.TargetFPS = (Model.FlightTypeIFR ? Model.TargetFPS_VXR : Model.TargetFPS_VXR_VFR);
				}
			}
			else
			{
				Model.ActiveGraphicsMode = "VR";
				if (!Model.AutoTargetFPS)
				{
					Model.TargetFPS = (Model.FlightTypeIFR ? Model.TargetFPS_VR : Model.TargetFPS_VR_VFR);
				}
			}
		}
		else if (Model.ActiveGraphicsMode == "Man FG")
		{
			if (!Model.AutoTargetFPS)
			{
				Model.TargetFPS = (Model.FlightTypeIFR ? Model.TargetFPS_ManFG : Model.TargetFPS_ManFG_VFR);
			}
		}
		else if (Model.LsModeEnabled)
		{
			Model.ActiveGraphicsMode = "LSFG";
			if (!Model.AutoTargetFPS)
			{
				Model.TargetFPS = (Model.FlightTypeIFR ? Model.TargetFPS_LS : Model.TargetFPS_LS_VFR);
			}
		}
		else if (Model.FgModeEnabled)
		{
			Model.ActiveGraphicsMode = "FG";
			if (!Model.AutoTargetFPS)
			{
				Model.TargetFPS = (Model.FlightTypeIFR ? Model.TargetFPS_FG : Model.TargetFPS_FG_VFR);
			}
		}
		else if (Model.FgModeFSREnabled)
		{
			Model.ActiveGraphicsMode = "FSR";
			if (!Model.AutoTargetFPS)
			{
				Model.TargetFPS = (Model.FlightTypeIFR ? Model.TargetFPS_FSR : Model.TargetFPS_FSR_VFR);
			}
		}
		else if (Model.MsmModeEnabled)
		{
			Model.ActiveGraphicsMode = "MSM";
			if (!Model.AutoTargetFPS)
			{
				Model.TargetFPS = (Model.FlightTypeIFR ? Model.TargetFPS_MSM : Model.TargetFPS_MSM_VFR);
			}
		}
		else
		{
			Model.ActiveGraphicsMode = "NFR";
			if (!Model.AutoTargetFPS)
			{
				Model.TargetFPS = (Model.FlightTypeIFR ? Model.TargetFPS_PC : Model.TargetFPS_PC_VFR);
			}
		}
		if (LsModeMultiplierlast == -1)
		{
			Logger.Log(LogLevel.Information, "LODController:GetMSFSState", "Initial graphics mode " + Model.ActiveGraphicsMode);
		}
		LsModeMultiplierlast = Model.LsModeMultiplier;
		if (!activeWindowMSFS && Model.ActiveWindowMSFS && ((Model.ActiveGraphicsMode == "FG" && (Model.FgModeMultiplier > 1 || Model.fpsSourceRTSS)) || (Model.ActiveGraphicsMode == "Man FG" && (Model.ManFgMultiplier > 1 || Model.fpsSourceRTSS))))
		{
			Array.Fill(deltaFPSTrendShort, 0f);
			Model.fpsAverageReset = true;
			Model.FPSSettleActive = true;
		}
		if (!(Model.ActiveGraphicsMode != activeGraphicsMode))
		{
			return;
		}
		Model.ConfigurationFile.SetSetting("ActiveGraphicsMode", Model.ActiveGraphicsMode);
		Logger.Log(LogLevel.Information, "LODController:GetMSFSState", "Graphics mode changed from " + activeGraphicsMode + " to " + Model.ActiveGraphicsMode);
		if (Model.ActiveGraphicsMode == "VR" || activeGraphicsMode == "VR" || Model.ActiveGraphicsMode == "OFXR" || activeGraphicsMode == "OFXR" || Model.ActiveGraphicsMode == "VXR" || activeGraphicsMode == "VXR")
		{
			switch (activeGraphicsMode)
			{
			case "VR":
			case "OFXR":
			case "VXR":
				Model.MemoryAccess.IsFgModeEnabled(forceCheck: true);
				break;
			}
			RTSSBufferRecordCount = 10;
			if (Model.UseExpertOptions)
			{
				Model.UpdateDynamicSettingsState();
			}
			if (Model.UseExpertOptions)
			{
				if ((Model.ActiveGraphicsMode == "VR" || Model.ActiveGraphicsMode == "OFXR" || Model.ActiveGraphicsMode == "VXR") && Enumerable.Contains(Model.ProfileName, Model.ProfileName[Model.FlightTypeExpert] + " VR"))
				{
					Model.SetSetting("FlightTypeExpertExtended", Array.IndexOf(Model.ProfileName, Model.ProfileName[Model.FlightTypeExpert] + " VR").ToString());
					Logger.Log(LogLevel.Information, "LODController:GetMSFSState", "Auto-switching to " + Model.ProfileName[Model.FlightTypeExpert] + " flight type profile");
				}
				else
				{
					switch (activeGraphicsMode)
					{
					case "VR":
					case "OFXR":
					case "VXR":
						if (Model.ProfileName[Model.FlightTypeExpert].Length > 3 && Model.ProfileName[Model.FlightTypeExpert].EndsWith(" VR") && Enumerable.Contains(Model.ProfileName, Model.ProfileName[Model.FlightTypeExpert].Substring(0, Model.ProfileName[Model.FlightTypeExpert].Length - 3)))
						{
							Model.SetSetting("FlightTypeExpertExtended", Array.IndexOf(Model.ProfileName, Model.ProfileName[Model.FlightTypeExpert].Substring(0, Model.ProfileName[Model.FlightTypeExpert].Length - 3)).ToString());
							Logger.Log(LogLevel.Information, "LODController:GetMSFSState", "Auto-switching to " + Model.ProfileName[Model.FlightTypeExpert] + " flight type profile");
						}
						break;
					}
				}
			}
			if (Model.TLODAutoMethod[Model.activeProfile] != 2)
			{
				Model.FPSSettleInitial = true;
			}
		}
		Model.ActiveGraphicsModeChanged = true;
		Model.ResetCloudsLODs(ResetTLOD: true, ResetAll: true);
	}
}

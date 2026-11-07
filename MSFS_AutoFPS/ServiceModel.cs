using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using CpuHeadroom;
using Microsoft.Win32;
using RTSSSharedMemoryNET;

namespace MSFS_AutoFPS;

public class ServiceModel
{
	public class CoreInfo
	{
		public int CoreIndex;

		public int EfficiencyClass;

		public uint Flags;

		public List<int> LogicalProcessors = new List<int>();
	}

	private enum LOGICAL_PROCESSOR_RELATIONSHIP
	{
		RelationProcessorCore
	}

	private struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX
	{
		public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;

		public int Size;

		public PROCESSOR_RELATIONSHIP Processor;
	}

	private struct PROCESSOR_RELATIONSHIP
	{
		public byte Flags;

		public byte EfficiencyClass;

		public byte Reserved1;

		public byte Reserved2;

		public int GroupCount;
	}

	private struct GROUP_AFFINITY
	{
		public ulong Mask;

		public ushort Group;

		public ushort Reserved1;

		public ushort Reserved2;

		public ushort Reserved3;
	}

	public enum updateOptions
	{
		PromptedUpdates,
		ShowUpdates,
		MandatoryOnly
	}

	public enum appProfiles
	{
		NonExpert,
		IFR_Expert,
		VFR_Expert
	}

	public class FPSRawTrendBuffer
	{
		private readonly float[] buffer;

		private int index;

		private bool filled;

		public int Count
		{
			get
			{
				if (!filled)
				{
					return index;
				}
				return buffer.Length;
			}
		}

		public bool IsFull => filled;

		public FPSRawTrendBuffer(int size)
		{
			buffer = new float[size];
		}

		public void Add(float value)
		{
			buffer[index++] = value;
			if (index >= buffer.Length)
			{
				index = 0;
				filled = true;
			}
		}

		public void Reset()
		{
			index = 0;
			filled = false;
			Array.Clear(buffer, 0, buffer.Length);
		}

		public float[] GetFilledArray()
		{
			if (!filled)
			{
				return buffer.Take(index).ToArray();
			}
			return buffer.ToArray();
		}
	}

	private struct IntervalInfo
	{
		public long intervalUs;

		public int spikeIndexA;

		public int spikeIndexB;
	}

	public List<CoreInfo> CpuCores = new List<CoreInfo>();

	private string cpuName = Registry.GetValue("HKEY_LOCAL_MACHINE\\HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0", "ProcessorNameString", null)?.ToString() ?? "null";

	private int CPULogicalProcessors = Environment.ProcessorCount;

	private int AMDDualCCDThreshold = 24;

	public readonly List<CoreInfo> Cores = new List<CoreInfo>();

	public List<CoreInfo> DualLpCores = new List<CoreInfo>();

	public List<CoreInfo> SingleLpCores = new List<CoreInfo>();

	public int SmtCores;

	public string MSFSProcessPriority;

	private ProcessPriorityClass OriginalPriority;

	public bool AMDUseFirstCCDOnly;

	public int AffinityPhysicalCoreThreshold;

	private nint OriginalAffinity;

	private bool OriginalAffinityCaptured;

	private bool OriginalPriorityCaptured;

	private bool InvalidPriorityWarned;

	private bool InvalidAffinityThresholdWarned;

	public bool PerfOptAffinityEnabled = true;

	public bool PerfOptPriorityEnabled = true;

	public bool PerfOptPowerPlanEnabled = true;

	private string originalPowerPlanGuid;

	private bool powerPlanChanged;

	public int LastAppliedAffinityCount = -1;

	public const int fixedFPSMaxRetries = 5;

	public const int fpsAverageSeconds = 5;

	public const int fpsConfirmationDuration = 3;

	public FPSRawTrendBuffer fpsRawTrend = new FPSRawTrendBuffer(Math.Min(20, 30));

	private DateTime lastOFXRLogModify;

	private DateTime lastOFXRSetModify;

	private int _cachedOfxrMultiplier = 2;

	private bool isOfxrPaused;

	private bool isOfxrDisarmed;

	private bool ofxrDisarmedInCurrentSession;

	private int lastOfxrBuild;

	private DateTime lastOFXRStatusJsonModify;

	private bool hasLoggedStaleStatusJson;

	private int _cachedVxrMultiplier;

	private string _cachedVxrFrameRate = "";

	private bool isVxrPaused;

	private bool isVxrStandby;

	private bool hasLoggedStaleVxrStatusJson;

	private DateTime lastVXRStatusJsonModify = DateTime.MinValue;

	public const int FPSSettleCountMax = 30;

	public const bool LogSimValues = true;

	public const float defaultAltTLODBase_IFR = 1000f;

	public const float defaultAltTLODBase_VFR = 100f;

	public const float defaultAltTLODTop_SensTol_IFR = 2000f;

	public const float defaultAltTLODTop_IFR = 3333f;

	public const float defaultAltTLODTop_SensTol_VFR = 1000f;

	public const float defaultAltTLODTop_VFR = 2350f;

	public const int MinTLODExtraIterationsMax = 8;

	public const float MinTLODExtraAltTLODTopTol = 100f;

	private bool resetWindowPosition;

	private string[] args = Environment.GetCommandLineArgs();

	private bool initialConfigLoaded;

	private DateTime lastRTSSStatTimeEnd;

	private DateTime lastLSFGSetModify;

	private DateTime lastMSMSetModify;

	public bool ShowDynSetButtonsInTestMode;

	public bool DevTesting;

	public int DevCandidateIndex = 1;

	public bool isMSFS2024;

	public bool isMSFS2024_last;

	public ConfigurationFile ConfigurationFile = new ConfigurationFile();

	public ConfigurationFile ConfigurationFileCommon = new ConfigurationFile();

	public string profileSuffix = "";

	public int ConfigVersion { get; set; }

	public string ConfigAppVersion { get; set; }

	public string DetectedSimVersion { get; set; }

	public string DetectedMSFSConfig2020 { get; set; }

	public string DetectedMSFSConfig2024 { get; set; }

	public bool SimVersionChanged { get; set; }

	public bool ServiceExited { get; set; }

	public bool MemoryCompatTestComplete { get; set; }

	public string InstalledPackagesPath { get; set; }

	public bool LODControllerBusy { get; set; }

	public bool ResetInProgress { get; set; }

	public bool CancellationRequested { get; set; }

	public bool AppEnabled { get; set; } = true;

	public bool ExitAppAfterFlightSession { get; set; }

	public bool SettingsReduceEnabled { get; set; }

	public bool UnableToAttachMSFS { get; set; }

	public bool ReloadAppWindowSettings { get; set; }

	public bool UseSysTrayIcon { get; set; }

	public bool WidgetInstalled2020 { get; set; }

	public bool WidgetInstalled2024 { get; set; }

	public string WidgetHotkey { get; set; }

	public bool WidgetGeneralCollapsed { get; set; }

	public int WidgetAutoHide { get; set; }

	public bool Force2024MSStore { get; set; }

	public bool fpsSourceRTSS { get; set; }

	public bool fpsSourceRTSSDefault { get; set; }

	public bool ExperimentalFeatures { get; set; }

	public bool PerfOptEnabled { get; set; }

	public bool AMDUseFirstCCDOnlyValue
	{
		get
		{
			if (AMDUseFirstCCDOnly && cpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase))
			{
				return CPULogicalProcessors >= AMDDualCCDThreshold;
			}
			return false;
		}
	}

	public bool AMDDualCCDValue
	{
		get
		{
			if (cpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase))
			{
				return CPULogicalProcessors >= AMDDualCCDThreshold;
			}
			return false;
		}
	}

	public string cpuNameValue => cpuName;

	public string LastAppliedAffinityText { get; private set; }

	public Action UpdateAffinityTooltip { get; set; }

	public string LastAppliedPriorityText { get; private set; }

	public Action UpdatePriorityTooltip { get; set; }

	public string LastAppliedPowerPlanText { get; private set; }

	public Action UpdatePowerPlanTooltip { get; set; }

	public CpuSnapshot? _latestSnap { get; set; }

	public int PerfLogBufferSize { get; set; } = 20;

	public bool PerfLoggingAlways { get; set; }

	public double PerfLogEwmaAlpha { get; set; } = 0.1;

	public bool MonitorMode { get; set; }

	public bool perfLogForInitialSeekDone { get; set; }

	public bool perfLogForFirstReseekDone { get; set; }

	public int perfLogTopCoreCount { get; set; } = 2;

	public bool PerfLogResetPending { get; set; }

	public int UpdateOption { get; set; }

	public bool AutoUpdateInProgress { get; set; }

	public bool AutoUpdateJustCompleted { get; set; }

	public bool TestUpdates { get; set; }

	public int UpdateChannel { get; set; }

	public bool SilentUpdates { get; set; }

	public bool AutoRevertToReleaseVersion { get; set; }

	public bool CompatTestUpgrader { get; set; }

	public bool InitialCheckForUpdatesComplete { get; set; }

	public string deferredUpdateVersion { get; set; }

	public bool deferredUpdate { get; set; }

	public bool IsSimRunning { get; set; }

	public bool IsSessionRunning { get; set; }

	public bool NightTime { get; set; }

	public bool NightTimeTransiton { get; set; }

	public float simRate { get; set; } = 1f;

	public float fpsAverage { get; set; }

	public float fpsRawMSFS { get; set; }

	public float fpsRaw { get; set; }

	public float fixedFPSDetected { get; set; } = -1f;

	public int fixedFPSDetectAttempts { get; set; }

	public int fixedFPSDetectHistoryIndex { get; set; }

	public bool fixedFPSDetectedAutoTargetFPS { get; set; }

	public float[] fixedFPSDetectHistory { get; set; } = new float[5];

	public bool FixedFPSConfigWarning { get; set; }

	public bool fpsAverageReset { get; set; }

	public MemoryManager MemoryAccess { get; set; }

	public int VerticalTrend { get; set; }

	public bool OnGround { get; set; } = true;

	public float groundSpeed { get; set; }

	public float tlod { get; set; }

	public float olod { get; set; }

	public bool initialLODsSet { get; set; }

	public float altAboveGnd { get; set; }

	public float verticalSpeed { get; set; }

	public float altitude { get; set; }

	public int cloudQ { get; set; }

	public int cloudQ_VR { get; set; }

	public int Trees { get; set; }

	public int Trees_VR { get; set; }

	public bool RTShadows { get; set; }

	public bool RTShadows_VR { get; set; }

	public bool TShadowsEnabled { get; set; }

	public bool TShadowsEnabled_VR { get; set; }

	public int TShadows { get; set; }

	public int TShadows_VR { get; set; }

	public int Bldings { get; set; }

	public int Bldings_VR { get; set; }

	public bool DispMap { get; set; }

	public bool DispMap_VR { get; set; }

	public int OSTPCache { get; set; }

	public int OSTPCache_VR { get; set; }

	public int Plants { get; set; }

	public int Plants_VR { get; set; }

	public int Grass { get; set; }

	public int Grass_VR { get; set; }

	public int PkdAcQ { get; set; }

	public int PkdAcQ_VR { get; set; }

	public int AcTrafQ { get; set; }

	public int AcTrafQ_VR { get; set; }

	public bool DynSet { get; set; }

	public bool DynSet_VR { get; set; }

	public int DynSetTargetFPS { get; set; }

	public int DynSetTargetFPS_VR { get; set; }

	public bool DynamicSettingsActive { get; set; }

	public bool VrModeActive { get; set; }

	public int OfxrModeMultiplier { get; set; } = 2;

	public bool OfxrModeEnabled { get; set; }

	public int OfxrMaxFPS { get; set; } = 120;

	public bool OfxrOverSpeed { get; set; }

	public bool OfxrModeAuto { get; set; }

	public bool OfxrStatusJsonActive { get; set; }

	public float OfxrStatusJsonLatency { get; set; }

	public bool OfxrDesynced { get; set; }

	public int OfxrAutoMultiplier { get; set; } = 1;

	public float OfxrOutputFps { get; set; }

	public float OfxrAppFps { get; set; }

	public float OfxrDisplayHz { get; set; }

	public int OfxrBuild { get; set; }

	public bool OfxrGenerating { get; set; }

	public bool VxrModeEnabled { get; set; }

	public string VxrMode { get; set; } = "";

	public int VxrModeMultiplier { get; set; }

	public float VxrEffectiveMultiplier { get; set; } = 1f;

	public bool VxrUnevenPacing { get; set; }

	public bool VxrModeAuto { get; set; }

	public bool VxrModeDynamic { get; set; }

	public bool VxrStatusJsonActive { get; set; }

	public bool VxrGenerating { get; set; }

	public float VxrAppFps { get; set; }

	public float VxrOutputFps { get; set; }

	public float VxrDistinctFps { get; set; }

	public float VxrHoldFps { get; set; }

	public float VxrDisplayHz { get; set; }

	public bool VrFgConflictDetected { get; set; }

	public bool VrFgConflictPromptRequired { get; set; }

	public bool VrFgConflictPromptActive { get; set; }

	public bool ActiveWindowMSFS { get; set; }

	public bool FPSSettleInitial { get; set; }

	public bool FPSSettleActive { get; set; }

	public bool FPSSettleActiveLast { get; set; }

	public int FPSSettleCount { get; set; }

	public bool AutoTargetFPS { get; set; }

	public bool FixedTargetFPS { get; set; }

	public bool UpdateTargetFPS { get; set; }

	public bool ForceAutoFPSCal { get; set; } = true;

	public string ActiveGraphicsMode { get; set; }

	public bool ActiveGraphicsModeChanged { get; set; }

	public bool FgModeEnabled { get; set; }

	public int FgModeMultiplier { get; set; } = 2;

	public int DynFgCurrentMultiplier { get; set; } = 1;

	public bool FgModeFSREnabled { get; set; }

	public bool LsModeEnabled { get; set; }

	public bool LsModeAdaptive { get; set; }

	public int LsModeMultiplier { get; set; }

	public bool MsmModeEnabled { get; set; }

	public bool MsmModeAdaptive { get; set; }

	public int MsmMultiplier { get; set; } = 1;

	public int ManFgMultiplier { get; set; }

	public bool UseExpertOptions { get; set; }

	public bool FlightTypeIFR { get; set; }

	public int FlightTypeExpert { get; set; }

	public bool IsAppPriorityFPS { get; set; } = true;

	public float TargetFPS { get; set; }

	public float TargetFPS_PC { get; set; }

	public float TargetFPS_VR { get; set; }

	public float TargetFPS_OFXR { get; set; }

	public float TargetFPS_VXR { get; set; }

	public float TargetFPS_FG { get; set; }

	public float TargetFPS_LS { get; set; }

	public float TargetFPS_MSM { get; set; }

	public float TargetFPS_FSR { get; set; }

	public float TargetFPS_ManFG { get; set; }

	public float TargetFPS_PC_VFR { get; set; }

	public float TargetFPS_VR_VFR { get; set; }

	public float TargetFPS_OFXR_VFR { get; set; }

	public float TargetFPS_VXR_VFR { get; set; }

	public float TargetFPS_FG_VFR { get; set; }

	public float TargetFPS_LS_VFR { get; set; }

	public float TargetFPS_MSM_VFR { get; set; }

	public float TargetFPS_FSR_VFR { get; set; }

	public float TargetFPS_ManFG_VFR { get; set; }

	public int[] TLODAutoMethod { get; set; } = new int[3];

	public int[] FPSTolerance { get; set; } = new int[3];

	public bool[] LOSCorrection { get; set; } = new bool[3];

	public float[] MinTLOD { get; set; } = new float[3];

	public bool[] MinTLODExtra { get; set; } = new bool[3];

	public bool[] StartTLODExtraAtMax { get; set; } = new bool[3];

	public bool[] FreeTLOD { get; set; } = new bool[3];

	public float MinTLODExtraAmount { get; set; }

	public bool[] MaxTLODExtra { get; set; } = new bool[3];

	public bool TLODReduceAfterGearDown { get; set; }

	public float[] MaxTLOD { get; set; } = new float[3];

	public bool[] MaxTLODHalvedNight { get; set; } = new bool[3];

	public int[] NightMode { get; set; } = new int[3];

	public int[] NightCloudReduceLevels { get; set; } = new int[3];

	public bool NightCloudReduceLevelsActive { get; set; }

	public float[] AltTLODBase { get; set; } = new float[3];

	public float[] AltTLODTop { get; set; } = new float[3];

	public float defaultAvgDesRate_IFR { get; set; } = 2000f;

	public float defaultAvgDesRate_VFR { get; set; } = 1350f;

	public bool[] DecCloudQ { get; set; } = new bool[3];

	public bool[] IncCloudQ { get; set; } = new bool[3];

	public bool[] AutoReduceSettings { get; set; } = new bool[3];

	public int[] ReductionSettingsMaxLevels { get; set; } = new int[3];

	public int[] ReductionSettingsFloor { get; set; } = new int[3];

	public int[] ReductionSettingsSuite { get; set; } = new int[3];

	public int[] ReductionSettingsRecoveryAlt { get; set; } = new int[3];

	public int ReductionSettingsDepth { get; set; }

	public int ReductionSettingsDepthAchieved { get; set; }

	public int ReductionSettingsSuiteAchieved { get; set; }

	public int ReductionSettingsDepthAchieved_VR { get; set; }

	public int ReductionSettingsSuiteAchieved_VR { get; set; }

	public float MinTLODReductionAmount { get; set; }

	public int[] RTSSFrameTimes { get; set; }

	public bool RTSSDataFrozen { get; set; }

	public bool RTSSDataFrozen2DWarningSuppressed { get; set; }

	public bool RTSSDataFrozenVRWarningSuppressed { get; set; }

	public bool SpikeDetectEnabled { get; set; } = true;

	public bool SpikeTLODReductionEnabled { get; set; } = true;

	public float SpikeTLODReductionAmount { get; set; }

	public long SpikeTLODReductionTickMark { get; set; }

	public long SpikeTLODReductionRecoveryDelay { get; set; } = 30L;

	public bool SpikeDetected { get; set; }

	public bool SpikeVerificationActive { get; set; }

	public string SpikeDetectProtStatus { get; set; } = "Awaiting MSFS connection";

	public double SpikeThresholdMultiplier { get; set; } = 3.0;

	public double LastLODControllerLoopTimeMs { get; set; }

	public float FrameTimeNoiseLevel { get; set; }

	public bool IncCloudQActive { get; set; }

	public bool[] ReduceCloudQ { get; set; } = new bool[3];

	public bool[] ReduceTrees { get; set; } = new bool[3];

	public bool[] ReduceRTShadows { get; set; } = new bool[3];

	public bool[] ReduceTShadows { get; set; } = new bool[3];

	public bool[] ReduceBldings { get; set; } = new bool[3];

	public bool[] ReduceDispMap { get; set; } = new bool[3];

	public bool[] ReduceOSTPCache { get; set; } = new bool[3];

	public bool[] ReducePlants { get; set; } = new bool[3];

	public bool[] ReduceGrass { get; set; } = new bool[3];

	public bool[] ReducePkdAcQ { get; set; } = new bool[3];

	public bool[] ReduceAcTrafQ { get; set; } = new bool[3];

	public bool[] ReduceAcTrafQ_VR { get; set; } = new bool[3];

	public int[] DecCloudQMethod { get; set; } = new int[3];

	public float CloudDecreaseGPUPct { get; set; }

	public float CloudRecoverGPUPct { get; set; }

	public float[] CloudRecoveryTLOD { get; set; } = new float[3];

	public bool[] CloudRecoveryPlus { get; set; } = new bool[3];

	public bool[] CustomAutoOLOD { get; set; } = new bool[3];

	public bool OLODPanelHide { get; set; }

	public float[] OLODAtBase { get; set; } = new float[3];

	public float[] AltOLODBase { get; set; } = new float[3];

	public float[] OLODAtTop { get; set; } = new float[3];

	public float[] AltOLODTop { get; set; } = new float[3];

	public int activeProfile { get; set; }

	public int ProfileCount { get; set; } = 6;

	public string[] ProfileName { get; set; }

	public bool FPSCapMigrationOffered { get; set; }

	public bool TLODExtraMtnsActive { get; set; }

	public bool[] TLODExtraMtns { get; set; } = new bool[3];

	public int[] TLODExtraMtnsTriggerAlt { get; set; } = new int[3];

	public int[] TLODExtraMtnsAmount { get; set; } = new int[3];

	public float TLODExtraMtnsAmountResidual { get; set; }

	public float SimMinLOD { get; set; }

	public float SimMaxLOD { get; set; }

	public float DefaultTLOD { get; set; } = 100f;

	public float DefaultTLOD_VR { get; set; } = 100f;

	public float DefaultOLOD { get; set; } = 100f;

	public float DefaultOLOD_VR { get; set; } = 100f;

	public int DefaultCloudQ { get; set; } = 2;

	public int DefaultCloudQ_VR { get; set; } = 2;

	public bool DefaultDynSet { get; set; }

	public bool DefaultDynSetVR { get; set; }

	public int DefaultDynSetFrameRateTarget { get; set; }

	public int DefaultDynSetFrameRateTarget_VR { get; set; }

	public int DefaultTrees { get; set; }

	public int DefaultTrees_VR { get; set; }

	public bool DefaultRTShadows { get; set; }

	public bool DefaultRTShadows_VR { get; set; }

	public bool DefaultTShadowsEnabled { get; set; }

	public bool DefaultTShadowsEnabled_VR { get; set; }

	public int DefaultTShadows { get; set; }

	public int DefaultTShadows_VR { get; set; }

	public int DefaultBldings { get; set; }

	public int DefaultBldings_VR { get; set; }

	public bool DefaultDispMap { get; set; }

	public bool DefaultDispMap_VR { get; set; }

	public int DefaultOSTPCache { get; set; }

	public int DefaultOSTPCache_VR { get; set; }

	public int DefaultPlants { get; set; }

	public int DefaultPlants_VR { get; set; }

	public int DefaultGrass { get; set; }

	public int DefaultGrass_VR { get; set; }

	public int DefaultPkdAcQ { get; set; }

	public int DefaultPkdAcQ_VR { get; set; }

	public int DefaultAcTrafQ { get; set; }

	public int DefaultAcTrafQ_VR { get; set; }

	public bool DefaultSettingsRead { get; set; }

	public bool tlod_step { get; set; }

	public bool tlod_increased { get; set; }

	public bool olod_step { get; set; }

	public bool DecCloudQActive { get; set; }

	public bool MinTLODExtraActive { get; set; }

	public bool MinTLODExtraSeeking { get; set; }

	public bool AboveAltTLODTopPlusTolerance { get; set; }

	public bool AboveAltTLODBase { get; set; }

	public int MinTLODExtraIterations { get; set; }

	public bool MinTLODExtraSeekingInitial { get; set; } = true;

	public float[] MinTLODExtraCapMult { get; set; } = new float[3];

	public float[] MaxTLODExtraCapMult { get; set; } = new float[3];

	public bool[] TLODBaseMinus { get; set; } = new bool[3];

	public bool TLODBasePlusReSeekEnabled { get; set; }

	public int TLODBasePlusReSeekInterval { get; set; }

	public float TLODBasePlusSeekStepSize { get; set; }

	public float TLODBasePlusSeekMinHeadroom { get; set; }

	public float TLODBasePlusSeekHeadroom { get; set; }

	public float activeMinTLOD { get; set; }

	public float activeMaxTLOD { get; set; }

	public bool LockTLODMax { get; set; }

	public float rangeMinTLOD { get; set; }

	public float rangeMaxTLOD { get; set; }

	public float activeOLODAtBase { get; set; }

	public float activeOLODAtTop { get; set; }

	public static int MfLvarsPerFrame { get; set; }

	public bool WaitForConnect { get; set; }

	public bool OpenWindow { get; set; }

	public int windowTop { get; set; }

	public int windowLeft { get; set; }

	public int virtualScreenMinX { get; set; }

	public int virtualScreenMaxX { get; set; }

	public int virtualScreenMinY { get; set; }

	public int virtualScreenMaxY { get; set; }

	public bool windowIsVisible { get; set; }

	public bool hideTitleBar { get; set; }

	public bool isDarkMode { get; set; }

	public bool RememberWindowPos { get; set; }

	public bool AllowWindowPosReset { get; set; } = true;

	public int windowPanelState { get; set; }

	public bool OnTop { get; set; }

	public string SimModule { get; set; }

	public long OffsetModuleBase { get; set; }

	public long moduleBaseAddress { get; set; } = -1L;

	public long OffsetModuleBaseDefault { get; set; }

	public bool OffsetSearchingActive { get; set; }

	public bool AppPaused { get; set; }

	public float gpuUsage { get; set; } = -1f;

	public string gpuModel { get; set; } = "";

	public int gpuMemoryUsage { get; set; }

	public bool VRAMHoldActive { get; set; }

	public bool VRAMReduceActive { get; set; }

	public bool VRAMProtInactive { get; set; } = true;

	public int VRAMOverflowHoldTLOD { get; set; }

	public int VRAMOverflowReduceTLOD { get; set; }

	public bool VRAMSettingsReduceActive { get; set; }

	public int VRAMOverflowSettingsRecovery { get; set; }

	public bool VRAMPlusEnabled { get; set; } = true;

	public int VRAMPlusSuite { get; set; } = 1;

	public int VRAMSettingsReduceAlt { get; set; }

	public bool LogPlus { get; set; }

	public long fixedTargetFPSIncreaseStep { get; set; }

	public float recentTLODPeak { get; set; } = 1000f;

	public float TLODIncreaseCap { get; set; }

	public int CloudIncreaseHysteresis { get; set; } = 10;

	public float CloudIncreaseGPUMaxLoad { get; set; } = 90f;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP relationshipType, nint buffer, ref int returnedLength);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int nIndex);

	public ServiceModel()
	{
		LoadCpuTopology();
		TLODAutoMethod[0] = 0;
		FPSTolerance[0] = 5;
		MinTLODExtra[0] = true;
		FreeTLOD[0] = false;
		MaxTLODExtra[0] = true;
		StartTLODExtraAtMax[0] = false;
		TLODBaseMinus[0] = false;
		TLODExtraMtns[0] = false;
		MaxTLODHalvedNight[0] = true;
		NightCloudReduceLevels[0] = 2;
		NightMode[0] = 1;
		LOSCorrection[0] = false;
		MinTLOD[0] = 50f;
		MaxTLOD[0] = 200f;
		virtualScreenMinX = GetSystemMetrics(76);
		virtualScreenMinY = GetSystemMetrics(77);
		virtualScreenMaxX = virtualScreenMinX + GetSystemMetrics(78);
		virtualScreenMaxY = virtualScreenMinY + GetSystemMetrics(79);
		ConfigurationFileCommon.LoadConfiguration(isMSFS2024, Common: true);
		windowTop = Convert.ToInt32(ConfigurationFileCommon.GetSetting("windowTop", "50"));
		windowLeft = Convert.ToInt32(ConfigurationFileCommon.GetSetting("windowLeft", "50"));
		ConfigAppVersion = Convert.ToString(ConfigurationFileCommon.GetSetting("ConfigAppVersion", "0.5.5.0"));
		if (windowLeft < virtualScreenMinX || windowLeft > virtualScreenMaxX - 10 || windowTop < virtualScreenMinY || windowTop > virtualScreenMaxY - 10 || (ConfigAppVersion == "0.5.5.0" && File.GetLastWriteTime(App.ConfigFileCommon) > DateTime.Now.AddSeconds(-15.0)))
		{
			resetWindowPosition = true;
		}
		DetectedMSFSConfig2020 = App.GetNewestConfigFile();
		DetectedMSFSConfig2024 = App.GetNewestConfigFile(isMSFS2024: true);
		if (DetectedMSFSConfig2020 == null && DetectedMSFSConfig2024 == null)
		{
			MessageBox.Show("No Microsoft Flight Simulator installation was detected. This application requires at least one MSFS install to function.", "MSFS_AutoFPS: MSFS Config Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", "No MSFS installation detected (Neither MSFS 2020 nor 2024 configs were found). Exiting application.");
			Environment.Exit(1);
		}
		if (DetectedMSFSConfig2020 != null && DetectedMSFSConfig2024 == null)
		{
			isMSFS2024 = (isMSFS2024_last = false);
		}
		else if (DetectedMSFSConfig2020 == null && DetectedMSFSConfig2024 != null)
		{
			isMSFS2024 = (isMSFS2024_last = true);
		}
		else if (new FileInfo(DetectedMSFSConfig2024).LastWriteTime > new FileInfo(DetectedMSFSConfig2020).LastWriteTime)
		{
			isMSFS2024 = (isMSFS2024_last = true);
		}
		LoadConfiguration();
		ConfigurationFileCommon.SetSetting("ConfigAppVersion", "0.5.5.0");
	}

	public bool LoadConfiguration(bool resetDefaults = false)
	{
		bool flag = false;
		if (IsSimRunning)
		{
			isMSFS2024 = (isMSFS2024_last = IPCManager.IsSim2024());
		}
		flag = ConfigurationFile.LoadConfiguration(isMSFS2024);
		Window windowOwner = ((DispatcherObject)Application.Current).Dispatcher.Invoke<Window>((Func<Window>)(() => Application.Current.MainWindow));
		ConfigurationFileCommon.LoadConfiguration(isMSFS2024, Common: true);
		ConfigAppVersion = Convert.ToString(ConfigurationFileCommon.GetSetting("ConfigAppVersion", "0.5.5.0"));
		MonitorMode = ParseBoolSetting(ConfigurationFileCommon, "MonitorMode", "false");
		if (!ConfigurationFileCommon.SettingExists("LogPlus") && ConfigurationFileCommon.SettingExists("TestMode"))
		{
			ConfigurationFileCommon.AddSetting("LogPlus", ParseBoolSetting(ConfigurationFileCommon, "TestMode", "false").ToString().ToLower());
		}
		LogPlus = MonitorMode || ParseBoolSetting(ConfigurationFileCommon, "LogPlus", "false");
		ShowDynSetButtonsInTestMode = ParseBoolSetting(ConfigurationFileCommon, "ShowDynSetButtonsInTestMode", "false");
		RememberWindowPos = ParseBoolSetting(ConfigurationFileCommon, "RememberWindowPos", "true");
		AllowWindowPosReset = ParseBoolSetting(ConfigurationFileCommon, "AllowWindowPosReset", "true");
		if (resetWindowPosition && AllowWindowPosReset)
		{
			ConfigurationFileCommon.SetSetting("windowTop", "50");
			ConfigurationFileCommon.SetSetting("windowLeft", "50");
			ConfigurationFileCommon.SetSetting("openWindow", "true");
			ConfigurationFileCommon.SetSetting("windowIsVisible", "true");
			resetWindowPosition = false;
		}
		windowTop = ParseIntSetting(ConfigurationFileCommon, "windowTop", "50");
		windowLeft = ParseIntSetting(ConfigurationFileCommon, "windowLeft", "50");
		OpenWindow = ParseBoolSetting(ConfigurationFileCommon, "openWindow", "true");
		windowIsVisible = ParseBoolSetting(ConfigurationFileCommon, "windowIsVisible", "true");
		hideTitleBar = ParseBoolSetting(ConfigurationFileCommon, "hideTitleBar", "false");
		isDarkMode = ParseBoolSetting(ConfigurationFileCommon, "DarkMode", "false");
		if (!ConfigurationFileCommon.SettingExists("UpdateOption"))
		{
			ConfigurationFileCommon.AddSetting("UpdateOption", "0");
			ConfigurationFileCommon.SetSetting("SilentUpdates", "false");
		}
		UpdateOption = ParseIntSetting(ConfigurationFileCommon, "UpdateOption", "0");
		CompatTestUpgrader = ParseBoolSetting(ConfigurationFileCommon, "CompatTestUpgrader", "false");
		if (CompatTestUpgrader)
		{
			ConfigurationFileCommon.SetSetting("CompatTestUpgrader", "false");
			Logger.Log(LogLevel.Information, "ServiceModel:LoadConfiguration", "Cleared CompatTestUpgrader after returning to stable version");
		}
		if (ConfigurationFileCommon.SettingExists("UpdateChannel"))
		{
			UpdateChannel = ParseIntSetting(ConfigurationFileCommon, "UpdateChannel", "0");
			TestUpdates = UpdateChannel > 0;
			ConfigurationFileCommon.SetSetting("TestUpdates", TestUpdates.ToString().ToLower());
		}
		else
		{
			TestUpdates = ParseBoolSetting(ConfigurationFileCommon, "TestUpdates", "false");
			UpdateChannel = (TestUpdates ? 2 : 0);
			ConfigurationFileCommon.SetSetting("UpdateChannel", UpdateChannel.ToString());
			ConfigurationFileCommon.SetSetting("TestUpdates", TestUpdates.ToString().ToLower());
		}
		if (!ConfigurationFileCommon.SettingExists("SilentUpdates"))
		{
			if (UpdateOption == 0)
			{
				string silentUpdatesMessage = "A new Silent Updates option is available that will automatically install app updates without prompts or release notes. The installer window will still appear briefly, but no interaction is required.\n\nYou are currently using Prompted (previously Auto) Updates. Would you like to switch to Silent Updates?";
				if (((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => (windowOwner == null) ? MessageBox.Show(silentUpdatesMessage, "MSFS_AutoFPS: New Silent Updates Option", MessageBoxButton.YesNo, MessageBoxImage.Question) : MessageBox.Show(windowOwner, silentUpdatesMessage, "MSFS_AutoFPS: New Silent Updates Option", MessageBoxButton.YesNo, MessageBoxImage.Question))) == MessageBoxResult.Yes)
				{
					SilentUpdates = true;
					ConfigurationFileCommon.SetSetting("SilentUpdates", "true");
					Logger.Log(LogLevel.Information, "ServiceModel:LoadConfiguration", "User migrated from Prompted Updates to Silent Updates.");
				}
				else
				{
					SilentUpdates = false;
					ConfigurationFileCommon.SetSetting("SilentUpdates", "false");
					Logger.Log(LogLevel.Information, "ServiceModel:LoadConfiguration", "User declined migration to Silent Updates, staying on Prompted Updates.");
				}
			}
			else
			{
				SilentUpdates = false;
				ConfigurationFileCommon.SetSetting("SilentUpdates", "false");
			}
		}
		else
		{
			SilentUpdates = ParseBoolSetting(ConfigurationFileCommon, "SilentUpdates", "false");
		}
		if (UpdateOption > 0 && SilentUpdates)
		{
			ConfigurationFileCommon.SetSetting("SilentUpdates", "false");
			SilentUpdates = false;
		}
		if (!initialConfigLoaded)
		{
			ConfigurationFileCommon.SetSetting("ExperimentalFeatures", "false");
		}
		ExperimentalFeatures = false;
		if (ExperimentalFeatures)
		{
			LogPlus = true;
		}
		if (Process.GetProcessesByName("RTSS").Length == 0)
		{
			fpsSourceRTSS = false;
		}
		else
		{
			bool flag2 = (fpsSourceRTSSDefault = ParseBoolSetting(ConfigurationFileCommon, "fpsSourceRTSS", "true"));
			fpsSourceRTSS = flag2;
		}
		PerfOptEnabled = ParseBoolSetting(ConfigurationFileCommon, "PerfOptEnabled", "false");
		MSFSProcessPriority = ParseStringSetting(ConfigurationFileCommon, "MSFSProcessPriority", "High");
		if (MSFSProcessPriority != "AboveNormal" && MSFSProcessPriority != "High")
		{
			if (!InvalidPriorityWarned)
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:LoadConfiguration", "Invalid MSFSProcessPriority value in config: '" + MSFSProcessPriority + "'. Using default: High");
				InvalidPriorityWarned = true;
			}
			MSFSProcessPriority = "High";
		}
		AMDUseFirstCCDOnly = ParseBoolSetting(ConfigurationFileCommon, "AMDUseFirstCCDOnly", "true");
		AffinityPhysicalCoreThreshold = ParseIntSetting(ConfigurationFileCommon, "AffinityPhysicalCoreThreshold", "6");
		if (AffinityPhysicalCoreThreshold < 4 || AffinityPhysicalCoreThreshold > 32)
		{
			if (!InvalidAffinityThresholdWarned)
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:LoadConfiguration", $"MSFSAffinityThreshold out of range ({AffinityPhysicalCoreThreshold}). Capping to 4–32 range.");
				InvalidAffinityThresholdWarned = true;
			}
			AffinityPhysicalCoreThreshold = Math.Clamp(AffinityPhysicalCoreThreshold, 4, 32);
		}
		PerfOptPriorityEnabled = ParseBoolSetting(ConfigurationFileCommon, "PerfOptPriorityEnabled", "true");
		if (!ConfigurationFileCommon.SettingExists("PerfOptAffinityEnabled") && SmtCores < AffinityPhysicalCoreThreshold)
		{
			ConfigurationFileCommon.SetSetting("PerfOptAffinityEnabled", "false");
		}
		else
		{
			PerfOptAffinityEnabled = ParseBoolSetting(ConfigurationFileCommon, "PerfOptAffinityEnabled", "true");
		}
		if (ConfigurationFileCommon.SettingExists("PowerPlanEnabled"))
		{
			PerfOptPowerPlanEnabled = ParseBoolSetting(ConfigurationFileCommon, "PowerPlanEnabled", "true");
			ConfigurationFileCommon.SetSetting("PerfOptPowerPlanEnabled", PerfOptPowerPlanEnabled.ToString().ToLower());
			ConfigurationFileCommon.RemoveSetting("PowerPlanEnabled");
		}
		else
		{
			PerfOptPowerPlanEnabled = ParseBoolSetting(ConfigurationFileCommon, "PerfOptPowerPlanEnabled", "true");
		}
		PerfLogBufferSize = ParseIntSetting(ConfigurationFileCommon, "PerfLogBufferSize", "15");
		PerfLoggingAlways = true;
		PerfLogEwmaAlpha = ParseFloatSetting(ConfigurationFileCommon, "PerfLogEwmaAlpha", "0.1");
		perfLogTopCoreCount = Math.Max(1, ParseIntSetting(ConfigurationFileCommon, "perfLogTopCoreCount", "2"));
		if (ConfigurationFileCommon.SettingExists("SpikeDetectEnabled"))
		{
			ConfigurationFileCommon.RemoveSetting("SpikeDetectEnabled");
		}
		SpikeTLODReductionEnabled = ParseBoolSetting(ConfigurationFileCommon, "SpikeTLODReductionEnabled", "true");
		SpikeThresholdMultiplier = ParseFloatSetting(ConfigurationFileCommon, "SpikeThresholdMultiplier", "2.0");
		ExitAppAfterFlightSession = ParseBoolSetting(ConfigurationFileCommon, "ExitAppAfterFlightSession", "false");
		MfLvarsPerFrame = ParseIntSetting(ConfigurationFile, "mfLvarPerFrame", "15");
		ConfigVersion = ParseIntSetting(ConfigurationFile, "ConfigVersion", "1");
		if (isMSFS2024)
		{
			Force2024MSStore = ParseBoolSetting(ConfigurationFile, "Force2024MSStore", "false");
		}
		UseExpertOptions = ParseBoolSetting(ConfigurationFile, "useExpertOptions", "false");
		windowPanelState = ParseIntSetting(ConfigurationFile, "windowPanelState", "0");
		WaitForConnect = ParseBoolSetting(ConfigurationFile, "waitForConnect", "true");
		RTSSDataFrozenVRWarningSuppressed = ParseBoolSetting(ConfigurationFile, "RTSSDataFrozenVRWarningSuppressed", "false");
		RTSSDataFrozen2DWarningSuppressed = ParseBoolSetting(ConfigurationFile, "RTSSDataFrozen2DWarningSuppressed", "false");
		string text = ParseStringSetting(ConfigurationFile, "WidgetHotkey", "None");
		WidgetHotkey = ((text == "None") ? "" : text);
		WidgetGeneralCollapsed = ParseBoolSetting(ConfigurationFile, "WidgetGeneralCollapsed", "false");
		WidgetAutoHide = ParseIntSetting(ConfigurationFile, "WidgetAutoHide", "0");
		SpikeDetectEnabled = UseExpertOptions;
		if (UseExpertOptions)
		{
			ProfileCount = ParseIntSetting(ConfigurationFileCommon, "ProfileCount", "8");
			if (ProfileCount < 4 || ProfileCount > 12 || (ConfigAppVersion == "0.5.2.0-test23" && ProfileCount == 6))
			{
				if (ProfileCount < 4)
				{
					ProfileCount = 4;
				}
				else if (ProfileCount > 12)
				{
					ProfileCount = 12;
				}
				else
				{
					ProfileCount = 8;
				}
				ConfigurationFileCommon.SetSetting("ProfileCount", ProfileCount.ToString());
			}
			ProfileName = new string[ProfileCount];
			ProfileName[0] = ParseStringSetting(ConfigurationFile, "ProfileNameIFR", "IFR");
			ProfileName[1] = ParseStringSetting(ConfigurationFile, "ProfileNameVFR", "VFR");
			for (int num = 2; num < ProfileCount; num++)
			{
				ProfileName[num] = ParseStringSetting(ConfigurationFile, $"ProfileName{num - 1}", $"User{num - 1}");
			}
			if (!initialConfigLoaded)
			{
				if (args.Length == 2)
				{
					if (args[1] == "-ifr")
					{
						ConfigurationFile.SetSetting("FlightTypeExpertExtended", "0");
					}
					else if (args[1] == "-vfr")
					{
						ConfigurationFile.SetSetting("FlightTypeExpertExtended", "1");
					}
					else
					{
						for (int num2 = 2; num2 < ProfileCount; num2++)
						{
							if (args[1] == $"-user{num2 - 1}")
							{
								ConfigurationFile.SetSetting("FlightTypeExpertExtended", num2.ToString());
								break;
							}
						}
					}
				}
				else if (args.Length == 3 && args[1] == "-profile" && Array.IndexOf(ProfileName, args[2]) >= 0)
				{
					ConfigurationFile.SetSetting("FlightTypeExpertExtended", Array.IndexOf(ProfileName, args[2]).ToString());
				}
			}
			if (ConfigurationFile.SettingExists("FlightTypeExpertExtended"))
			{
				int num3 = (FlightTypeExpert = ParseIntSetting(ConfigurationFile, "FlightTypeExpertExtended", "0"));
				if (num3 > ProfileCount - 1)
				{
					ConfigurationFile.SetSetting("FlightTypeExpertExtended", (FlightTypeExpert = 0).ToString());
				}
			}
			else
			{
				FlightTypeExpert = ParseIntSetting(ConfigurationFile, "FlightTypeExpert", "0");
				ConfigurationFile.SetSetting("FlightTypeExpertExtended", FlightTypeExpert.ToString());
			}
			FlightTypeIFR = FlightTypeExpert != 1;
			FPSCapMigrationOffered = ParseBoolSetting(ConfigurationFile, "FPSCapMigrationOffered" + FlightTypeExpert, "false");
		}
		else
		{
			if (!initialConfigLoaded && args.Length == 2)
			{
				if (args[1] == "-ifr")
				{
					ConfigurationFile.SetSetting("FlightTypeIFR", "true");
				}
				else if (args[1] == "-vfr")
				{
					ConfigurationFile.SetSetting("FlightTypeIFR", "false");
				}
			}
			FlightTypeIFR = ParseBoolSetting(ConfigurationFile, "FlightTypeIFR", "true");
			FlightTypeExpert = ((!FlightTypeIFR) ? 1 : 0);
			ProfileName = new string[2];
			ProfileName[0] = "IFR";
			ProfileName[1] = "VFR";
		}
		profileSuffix = ((FlightTypeExpert >= 2) ? (FlightTypeExpert - 1).ToString() : "");
		SimModule = ParseStringSetting(ConfigurationFile, "simModule", "WwiseLibPCx64P.dll");
		if (!UseExpertOptions)
		{
			activeProfile = 0;
		}
		else if (FlightTypeIFR)
		{
			activeProfile = 1;
		}
		else
		{
			activeProfile = 2;
		}
		TLODAutoMethod[1] = ParseIntSetting(ConfigurationFile, "TLODAutoMethod" + profileSuffix, "0", resetDefaults);
		TLODAutoMethod[2] = ParseIntSetting(ConfigurationFile, "TLODAutoMethod_VFR", TLODAutoMethod[1].ToString(), resetDefaults);
		AutoTargetFPS = TLODAutoMethod[activeProfile] < 2 && ParseBoolSetting(ConfigurationFile, "AutoTargetFPS" + (FlightTypeIFR ? profileSuffix : "_VFR"), "false", resetDefaults);
		if (!initialConfigLoaded)
		{
			if (ConfigurationFile.SettingExists("FixedTargetFPS" + (FlightTypeIFR ? profileSuffix : "_VFR")) && !ConfigurationFile.SettingExists("FixedTargetFPS" + (FlightTypeIFR ? profileSuffix : "_VFR")))
			{
				ConfigurationFile.SetSetting("FixedTargetFPS" + (FlightTypeIFR ? profileSuffix : "_VFR"), ParseBoolSetting(ConfigurationFile, "FixedTargetFPS" + (FlightTypeIFR ? profileSuffix : "_VFR"), "false").ToString().ToLower());
				ConfigurationFile.RemoveSetting("FixedTargetFPS" + (FlightTypeIFR ? profileSuffix : "_VFR"));
			}
			if (isMSFS2024)
			{
				if (ConfigurationFile.SettingExists("CloudRecoveryTLOD"))
				{
					ConfigurationFile.RemoveSetting("CloudRecoveryTLOD");
				}
				if (ConfigurationFile.SettingExists("CloudRecoveryTLOD_VFR"))
				{
					ConfigurationFile.RemoveSetting("CloudRecoveryTLOD_VFR");
				}
				for (int num6 = 1; num6 <= 10; num6++)
				{
					string key = "CloudRecoveryTLOD" + num6;
					if (ConfigurationFile.SettingExists(key))
					{
						ConfigurationFile.RemoveSetting(key);
					}
				}
			}
		}
		FixedTargetFPS = TLODAutoMethod[activeProfile] < 2 && !AutoTargetFPS && ParseBoolSetting(ConfigurationFile, "FixedTargetFPS" + (FlightTypeIFR ? profileSuffix : "_VFR"), "false", resetDefaults);
		VRAMPlusEnabled = ParseBoolSetting(ConfigurationFile, "VRAMPlusEnabled", "true");
		OLODPanelHide = ParseBoolSetting(ConfigurationFile, "OLODPanelHide", "false");
		VRAMPlusSuite = (isMSFS2024 ? ((!UseExpertOptions) ? (VRAMPlusEnabled ? 1 : 0) : ParseIntSetting(ConfigurationFile, "VRAMPlusSuite", "1")) : 0);
		VRAMOverflowHoldTLOD = ParseIntSetting(ConfigurationFile, "VRAMOverflowHoldTLOD", "96");
		VRAMOverflowReduceTLOD = ParseIntSetting(ConfigurationFile, "VRAMOverflowReduceTLOD", "98");
		VRAMOverflowSettingsRecovery = ParseIntSetting(ConfigurationFile, "VRAMOverflowSettingsRecovery", (VRAMOverflowReduceTLOD - 5).ToString());
		if (VRAMOverflowReduceTLOD > 100)
		{
			VRAMOverflowReduceTLOD = 100;
			ConfigurationFile.SetSetting("VRAMOverflowReduceTLOD", "100");
		}
		if (VRAMOverflowHoldTLOD >= VRAMOverflowReduceTLOD)
		{
			VRAMOverflowHoldTLOD = VRAMOverflowReduceTLOD - 1;
			ConfigurationFile.SetSetting("VRAMOverflowHoldTLOD", VRAMOverflowHoldTLOD.ToString());
		}
		if (VRAMOverflowSettingsRecovery >= VRAMOverflowHoldTLOD)
		{
			VRAMOverflowSettingsRecovery = VRAMOverflowReduceTLOD - 5;
			ConfigurationFile.SetSetting("VRAMOverflowSettingsRecovery", VRAMOverflowSettingsRecovery.ToString());
		}
		if (AutoTargetFPS)
		{
			MinTLODExtra[1] = false;
			MinTLODExtra[2] = false;
			MinTLODExtra[0] = false;
			FreeTLOD[1] = false;
			FreeTLOD[2] = false;
			MaxTLODExtra[1] = false;
			MaxTLODExtra[2] = false;
			MaxTLODExtra[0] = false;
			StartTLODExtraAtMax[1] = false;
			StartTLODExtraAtMax[2] = false;
		}
		else
		{
			MinTLODExtra[1] = ParseBoolSetting(ConfigurationFile, "MinTLODExtra" + profileSuffix, "false", resetDefaults);
			MinTLODExtra[2] = ParseBoolSetting(ConfigurationFile, "MinTLODExtra_VFR", MinTLODExtra[1].ToString(), resetDefaults);
			MinTLODExtra[0] = true;
			FreeTLOD[1] = TLODAutoMethod[1] < 2 && ParseBoolSetting(ConfigurationFile, "FreeTLOD" + profileSuffix, "false", resetDefaults);
			FreeTLOD[2] = TLODAutoMethod[2] < 2 && ParseBoolSetting(ConfigurationFile, "FreeTLOD_VFR", FreeTLOD[1].ToString(), resetDefaults);
			MaxTLODExtra[1] = true;
			MaxTLODExtra[2] = true;
			MaxTLODExtra[0] = true;
			StartTLODExtraAtMax[1] = ParseBoolSetting(ConfigurationFile, "StartTLODExtraAtMax" + profileSuffix, "false", resetDefaults);
			StartTLODExtraAtMax[2] = ParseBoolSetting(ConfigurationFile, "StartTLODExtraAtMax_VFR", StartTLODExtraAtMax[1].ToString(), resetDefaults);
		}
		defaultAvgDesRate_IFR = ParseFloatSetting(ConfigurationFile, "defaultAvgDesRate_IFR", "2000");
		defaultAvgDesRate_VFR = ParseFloatSetting(ConfigurationFile, "defaultAvgDesRate_VFR", "1350");
		OnTop = ParseBoolSetting(ConfigurationFile, "OnTop", "false");
		if (isMSFS2024)
		{
			DecCloudQ[0] = false;
			IncCloudQ[0] = ParseBoolSetting(ConfigurationFile, "IncCloudQNonExpert", "true", resetDefaults);
			AutoReduceSettings[0] = !AutoTargetFPS;
			ReductionSettingsMaxLevels[0] = 2;
			ReductionSettingsFloor[0] = 1;
			ReductionSettingsSuite[0] = 3;
			ReductionSettingsRecoveryAlt[0] = 0;
			if (FlightTypeIFR)
			{
				ReduceCloudQ[0] = false;
				ReduceTrees[0] = true;
				ReduceRTShadows[0] = true;
				ReduceTShadows[0] = true;
				ReduceBldings[0] = true;
				ReduceDispMap[0] = true;
				ReduceOSTPCache[0] = true;
				ReducePlants[0] = true;
				ReduceGrass[0] = true;
				ReducePkdAcQ[0] = true;
				ReduceAcTrafQ[0] = true;
			}
			else
			{
				ReduceCloudQ[0] = false;
				ReduceTrees[0] = true;
				ReduceRTShadows[0] = true;
				ReduceTShadows[0] = true;
				ReduceBldings[0] = false;
				ReduceDispMap[0] = true;
				ReduceOSTPCache[0] = false;
				ReducePlants[0] = true;
				ReduceGrass[0] = true;
				ReducePkdAcQ[0] = false;
				ReduceAcTrafQ[0] = false;
			}
		}
		else
		{
			AutoReduceSettings[0] = false;
			DecCloudQ[0] = ParseBoolSetting(ConfigurationFile, "DecCloudQNonExpert", "true", resetDefaults);
			IncCloudQ[0] = false;
			DecCloudQMethod[0] = 0;
			CloudRecoveryTLOD[0] = 100f;
			CloudRecoveryPlus[0] = false;
		}
		CustomAutoOLOD[0] = true;
		AltOLODBase[0] = 2000f;
		AltOLODTop[0] = 10000f;
		TLODBasePlusReSeekEnabled = ParseBoolSetting(ConfigurationFile, "TLODBasePlusReSeekEnabled", "true");
		TLODBasePlusReSeekInterval = 60;
		float tLODBasePlusSeekHeadroom = (TLODBasePlusSeekMinHeadroom = ParseFloatSetting(ConfigurationFile, "TLODBasePlusSeekMinHeadroom", "25"));
		TLODBasePlusSeekHeadroom = tLODBasePlusSeekHeadroom;
		if (isMSFS2024)
		{
			OfxrMaxFPS = ParseIntSetting(ConfigurationFile, "ofxrMaxFPS", "120");
		}
		TargetFPS_PC = ParseFloatSetting(ConfigurationFile, "targetFpsPC" + profileSuffix, "40", resetDefaults);
		TargetFPS_VR = ParseFloatSetting(ConfigurationFile, "targetFpsVR" + profileSuffix, "40", resetDefaults);
		TargetFPS_OFXR = ParseFloatSetting(ConfigurationFile, "targetFpsOFXR" + profileSuffix, "40", resetDefaults);
		TargetFPS_VXR = ParseFloatSetting(ConfigurationFile, "targetFpsVXR" + profileSuffix, "40", resetDefaults);
		TargetFPS_FG = ParseFloatSetting(ConfigurationFile, "targetFpsFG" + profileSuffix, "40", resetDefaults);
		TargetFPS_LS = ParseFloatSetting(ConfigurationFile, "targetFpsLS" + profileSuffix, "40", resetDefaults);
		TargetFPS_MSM = ParseFloatSetting(ConfigurationFile, "targetFpsMSM" + profileSuffix, "40", resetDefaults);
		if (ConfigurationFile.SettingExists("targetFpsFSRX" + profileSuffix) || !ConfigurationFile.SettingExists("targetFpsFSR3" + profileSuffix))
		{
			TargetFPS_FSR = ParseFloatSetting(ConfigurationFile, "targetFpsFSRX" + profileSuffix, "40", resetDefaults);
		}
		else
		{
			TargetFPS_FSR = ParseFloatSetting(ConfigurationFile, "targetFpsFSR3" + profileSuffix, "40", resetDefaults);
			ConfigurationFile.SetSetting("targetFpsFSRX" + profileSuffix, TargetFPS_FSR.ToString(CultureInfo.InvariantCulture));
		}
		TargetFPS_PC_VFR = ParseFloatSetting(ConfigurationFile, "targetFpsPCVFR", TargetFPS_PC.ToString(CultureInfo.InvariantCulture), resetDefaults);
		TargetFPS_VR_VFR = ParseFloatSetting(ConfigurationFile, "targetFpsVRVFR", TargetFPS_VR.ToString(CultureInfo.InvariantCulture), resetDefaults);
		TargetFPS_OFXR_VFR = ParseFloatSetting(ConfigurationFile, "targetFpsOFXRVFR", TargetFPS_OFXR.ToString(CultureInfo.InvariantCulture), resetDefaults);
		TargetFPS_VXR_VFR = ParseFloatSetting(ConfigurationFile, "targetFpsVXRVFR", TargetFPS_VXR.ToString(CultureInfo.InvariantCulture), resetDefaults);
		TargetFPS_FG_VFR = ParseFloatSetting(ConfigurationFile, "targetFpsFGVFR", TargetFPS_FG.ToString(CultureInfo.InvariantCulture), resetDefaults);
		TargetFPS_LS_VFR = ParseFloatSetting(ConfigurationFile, "targetFpsLSVFR", TargetFPS_LS.ToString(CultureInfo.InvariantCulture), resetDefaults);
		TargetFPS_MSM_VFR = ParseFloatSetting(ConfigurationFile, "targetFpsMSMVFR", TargetFPS_MSM.ToString(CultureInfo.InvariantCulture), resetDefaults);
		if (ConfigurationFile.SettingExists("targetFpsFSRXVFR") || !ConfigurationFile.SettingExists("targetFpsFSR3VFR"))
		{
			TargetFPS_FSR_VFR = ParseFloatSetting(ConfigurationFile, "targetFpsFSRXVFR", TargetFPS_FSR.ToString(CultureInfo.InvariantCulture), resetDefaults);
		}
		else
		{
			TargetFPS_FSR_VFR = ParseFloatSetting(ConfigurationFile, "targetFpsFSR3VFR", TargetFPS_FSR.ToString(CultureInfo.InvariantCulture), resetDefaults);
			ConfigurationFile.SetSetting("targetFpsFSRXVFR", TargetFPS_FSR_VFR.ToString(CultureInfo.InvariantCulture));
		}
		OffsetModuleBaseDefault = ((!isMSFS2024) ? 4924264 : ((File.Exists(App.msConfigStore2024) || Force2024MSStore) ? 176205860 : 179105780));
		OffsetModuleBase = ParseLongSetting(ConfigurationFile, "offsetModuleBase", $"0x{OffsetModuleBaseDefault:X8}", NumberStyles.HexNumber);
		SimMinLOD = ParseFloatSetting(ConfigurationFile, "simMinLod", "10");
		SimMaxLOD = ParseFloatSetting(ConfigurationFile, "simMaxLod", "1000");
		ActiveGraphicsMode = ParseStringSetting(ConfigurationFile, "ActiveGraphicsMode", "NFR");
		if (ActiveGraphicsMode == "FSR3")
		{
			ActiveGraphicsMode = "FSR";
			ConfigurationFile.SetSetting("ActiveGraphicsMode", "FSR");
		}
		ManFgMultiplier = ParseIntSetting(ConfigurationFile, "ManFgMultiplier", "2");
		TargetFPS_ManFG = ParseFloatSetting(ConfigurationFile, "targetFpsManFG" + profileSuffix, "40", resetDefaults);
		TargetFPS_ManFG_VFR = ParseFloatSetting(ConfigurationFile, "targetFpsManFGVFR", TargetFPS_ManFG.ToString(CultureInfo.InvariantCulture), resetDefaults);
		if (!ParseBoolSetting(ConfigurationFile, "HasMigratedFGModes", "false"))
		{
			string migrationMessage = null;
			string activeGraphicsMode = ActiveGraphicsMode;
			float targetFPS_ManFG = ParseFloatSetting(ConfigurationFile, "targetFpsMFG" + profileSuffix, "40");
			float targetFPS_ManFG_VFR = ParseFloatSetting(ConfigurationFile, "targetFpsMFGVFR", targetFPS_ManFG.ToString(CultureInfo.InvariantCulture));
			float targetFPS_ManFG2 = ParseFloatSetting(ConfigurationFile, "targetFpsOFG" + profileSuffix, "40");
			float targetFPS_ManFG_VFR2 = ParseFloatSetting(ConfigurationFile, "targetFpsOFGVFR", targetFPS_ManFG2.ToString(CultureInfo.InvariantCulture));
			if (activeGraphicsMode == "MFG")
			{
				int num8 = ParseIntSetting(ConfigurationFile, "MfgModeMultiplier", "0");
				if (num8 == 0)
				{
					ActiveGraphicsMode = "NFR";
					migrationMessage = "The old MFG mode was configured with FG disabled.\nYour settings have been migrated to NFR, which is the equivalent behaviour.";
				}
				else
				{
					ActiveGraphicsMode = "Man FG";
					ManFgMultiplier = num8 + 1;
					TargetFPS_ManFG = targetFPS_ManFG;
					TargetFPS_ManFG_VFR = targetFPS_ManFG_VFR;
					migrationMessage = "The old MFG mode has been replaced with the unified FG mode.\nYour settings have been migrated automatically.";
				}
			}
			else if (activeGraphicsMode == "OFG")
			{
				ActiveGraphicsMode = "Man FG";
				ManFgMultiplier = 2;
				TargetFPS_ManFG = targetFPS_ManFG2;
				TargetFPS_ManFG_VFR = targetFPS_ManFG_VFR2;
				migrationMessage = "The old OFG mode has been replaced with the unified FG mode.\nYour settings have been migrated automatically and set to the equivalent 2× configuration.";
			}
			if (migrationMessage != null)
			{
				((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => (windowOwner == null) ? MessageBox.Show(migrationMessage, "MSFS_AutoFPS", MessageBoxButton.OK, MessageBoxImage.Asterisk) : MessageBox.Show(windowOwner, migrationMessage, "MSFS_AutoFPS", MessageBoxButton.OK, MessageBoxImage.Asterisk)));
			}
			ConfigurationFile.SetSetting("ManFgMultiplier", ManFgMultiplier.ToString(CultureInfo.InvariantCulture));
			ConfigurationFile.SetSetting("ActiveGraphicsMode", ActiveGraphicsMode);
			ConfigurationFile.SetSetting("targetFpsManFG" + profileSuffix, TargetFPS_ManFG.ToString(CultureInfo.InvariantCulture));
			ConfigurationFile.SetSetting("targetFpsManFGVFR", TargetFPS_ManFG_VFR.ToString(CultureInfo.InvariantCulture));
			ConfigurationFile.SetSetting("HasMigratedFGModes", "true");
		}
		if (TLODAutoMethod[1] == 2)
		{
			MinTLODExtra[1] = false;
		}
		else if (TLODAutoMethod[1] == 3)
		{
			MinTLODExtra[1] = true;
			TLODExtraMtns[1] = false;
		}
		if (TLODAutoMethod[1] != 3)
		{
			TLODExtraMtns[1] = ParseBoolSetting(ConfigurationFile, "TLODExtraMtns" + profileSuffix, "false", resetDefaults);
		}
		TLODExtraMtnsTriggerAlt[1] = ParseIntSetting(ConfigurationFile, "TLODExtraMtnsTriggerAlt" + profileSuffix, "5000", resetDefaults);
		TLODExtraMtnsAmount[1] = ParseIntSetting(ConfigurationFile, "TLODExtraMtnsAmount" + profileSuffix, "200", resetDefaults);
		FPSTolerance[1] = ParseIntSetting(ConfigurationFile, "FpsTolerance" + profileSuffix, "5", resetDefaults);
		LOSCorrection[1] = false;
		MinTLOD[1] = ParseFloatSetting(ConfigurationFile, "minTLod" + profileSuffix, "50", resetDefaults);
		if (MinTLOD[1] < SimMinLOD)
		{
			ConfigurationFile.SetSetting("minTLod" + profileSuffix, SimMinLOD.ToString());
			MinTLOD[1] = SimMinLOD;
		}
		MaxTLOD[1] = ParseFloatSetting(ConfigurationFile, "maxTLod" + profileSuffix, "200", resetDefaults);
		if (MaxTLOD[1] > SimMaxLOD)
		{
			ConfigurationFile.SetSetting("maxTLod" + profileSuffix, SimMaxLOD.ToString());
			MaxTLOD[1] = SimMaxLOD;
		}
		if (ParseFloatSetting(ConfigurationFile, "MinTLODExtraCapMult" + profileSuffix, MinTLODExtraCapMult[1].ToString(CultureInfo.InvariantCulture)) < 1.5f)
		{
			ConfigurationFile.SetSetting("MinTLODExtraCapMult" + profileSuffix, 1.5f.ToString(CultureInfo.InvariantCulture));
		}
		MinTLODExtraCapMult[1] = (FreeTLOD[1] ? (MaxTLOD[1] / MinTLOD[1]) : ParseFloatSetting(ConfigurationFile, "MinTLODExtraCapMult" + profileSuffix, "2", resetDefaults));
		MinTLODExtraCapMult[0] = 4f;
		MaxTLODExtraCapMult[1] = ParseFloatSetting(ConfigurationFile, "MaxTLODExtraCapMult" + profileSuffix, "2", resetDefaults);
		MaxTLODExtraCapMult[2] = ParseFloatSetting(ConfigurationFile, "MaxTLODExtraCapMult_VFR", MaxTLODExtraCapMult[1].ToString(CultureInfo.InvariantCulture), resetDefaults);
		MaxTLODExtraCapMult[0] = 2f;
		if (!ConfigurationFile.SettingExists("NightMode" + profileSuffix) && ConfigurationFile.SettingExists("MaxTLODHalvedNight" + profileSuffix))
		{
			bool flag4 = ParseBoolSetting(ConfigurationFile, "MaxTLODHalvedNight" + profileSuffix, "false");
			ConfigurationFile.AddSetting("NightMode" + profileSuffix, flag4 ? "1" : "0");
		}
		NightMode[1] = ParseIntSetting(ConfigurationFile, "NightMode" + profileSuffix, "0", resetDefaults);
		MaxTLODHalvedNight[1] = NightMode[1] == 1 || NightMode[1] == 3;
		NightCloudReduceLevels[1] = ParseIntSetting(ConfigurationFile, "NightCloudReduceLevels" + profileSuffix, "2", resetDefaults);
		AltTLODBase[1] = ParseFloatSetting(ConfigurationFile, "AltTLODBase" + profileSuffix, 1000f.ToString("F0"), resetDefaults);
		if (AltTLODBase[1] < 10f)
		{
			ConfigurationFile.SetSetting("AltTLODBase" + profileSuffix, "10");
		}
		AltTLODTop[1] = ParseFloatSetting(ConfigurationFile, "AltTLODTop" + ((TLODAutoMethod[activeProfile] < 2) ? "_SensTol" : "") + profileSuffix, (TLODAutoMethod[activeProfile] < 2) ? 2000f.ToString("F0") : 3333f.ToString("F0"), resetDefaults);
		if (isMSFS2024)
		{
			if (TLODAutoMethod[1] == 2)
			{
				AutoReduceSettings[1] = false;
				VRAMPlusSuite = 1;
			}
			else if (AutoTargetFPS)
			{
				AutoReduceSettings[1] = false;
			}
			else
			{
				AutoReduceSettings[1] = ParseBoolSetting(ConfigurationFile, "AutoReduceSettings" + profileSuffix, "false", resetDefaults);
			}
			ReductionSettingsMaxLevels[1] = ParseIntSetting(ConfigurationFile, "ReductionSettingsMaxLevels" + profileSuffix, "2", resetDefaults);
			ReductionSettingsFloor[1] = ParseIntSetting(ConfigurationFile, "ReductionSettingsFloor" + profileSuffix, "1", resetDefaults);
			ReductionSettingsSuite[1] = ParseIntSetting(ConfigurationFile, "ReductionSettingsSuite" + profileSuffix, "1", resetDefaults);
			ReductionSettingsRecoveryAlt[1] = ParseIntSetting(ConfigurationFile, "ReductionSettingsRecoveryAlt" + profileSuffix, "1", resetDefaults);
			ReduceCloudQ[1] = false;
			ReduceTrees[1] = false;
			ReduceRTShadows[1] = false;
			ReduceTShadows[1] = false;
			ReduceBldings[1] = false;
			ReduceDispMap[1] = false;
			ReduceOSTPCache[1] = false;
			ReducePlants[1] = false;
			ReduceGrass[1] = false;
			ReducePkdAcQ[1] = false;
			ReduceAcTrafQ[1] = false;
			if (ReductionSettingsSuite[1] > 0)
			{
				ReduceCloudQ[1] = true;
				ReduceTrees[1] = true;
				ReduceRTShadows[1] = true;
			}
			if (ReductionSettingsSuite[1] > 1)
			{
				ReduceTShadows[1] = true;
				ReduceBldings[1] = true;
				ReduceDispMap[1] = true;
				ReduceOSTPCache[1] = true;
				ReducePlants[1] = true;
				ReduceGrass[1] = true;
				ReducePkdAcQ[1] = true;
				ReduceAcTrafQ[1] = true;
			}
			if (ReductionSettingsSuite[1] == 3)
			{
				ReduceCloudQ[1] = ParseBoolSetting(ConfigurationFile, "ReduceCloudQ" + profileSuffix, "true", resetDefaults);
				ReduceTrees[1] = ParseBoolSetting(ConfigurationFile, "ReduceTrees" + profileSuffix, "true", resetDefaults);
				ReduceRTShadows[1] = ParseBoolSetting(ConfigurationFile, "ReduceRTShadows" + profileSuffix, "true", resetDefaults);
				ReduceTShadows[1] = ParseBoolSetting(ConfigurationFile, "ReduceTShadows" + profileSuffix, "true", resetDefaults);
				ReduceBldings[1] = ParseBoolSetting(ConfigurationFile, "ReduceBldings" + profileSuffix, "true", resetDefaults);
				ReduceDispMap[1] = ParseBoolSetting(ConfigurationFile, "ReduceDispMap" + profileSuffix, "true", resetDefaults);
				ReduceOSTPCache[1] = ParseBoolSetting(ConfigurationFile, "ReduceOSTPCache" + profileSuffix, "true", resetDefaults);
				ReducePlants[1] = ParseBoolSetting(ConfigurationFile, "ReducePlants" + profileSuffix, "true", resetDefaults);
				ReduceGrass[1] = ParseBoolSetting(ConfigurationFile, "ReduceGrass" + profileSuffix, "true", resetDefaults);
				ReducePkdAcQ[1] = ParseBoolSetting(ConfigurationFile, "ReducePkdAcQ" + profileSuffix, "true", resetDefaults);
				ReduceAcTrafQ[1] = ParseBoolSetting(ConfigurationFile, "ReduceAcTrafQ" + profileSuffix, "true", resetDefaults);
			}
			DecCloudQ[1] = false;
			DecCloudQMethod[1] = 0;
			if (MinTLODExtra[1])
			{
				IncCloudQ[1] = ParseBoolSetting(ConfigurationFile, "IncCloudQ" + profileSuffix, "false", resetDefaults);
			}
			else
			{
				IncCloudQ[1] = false;
			}
		}
		else
		{
			AutoReduceSettings[1] = false;
			DecCloudQ[1] = ParseBoolSetting(ConfigurationFile, "DecCloudQ" + profileSuffix, "true", resetDefaults);
			DecCloudQMethod[1] = ((TLODAutoMethod[activeProfile] >= 2) ? 1 : ParseIntSetting(ConfigurationFile, "DecCloudQMethod" + profileSuffix, "0", resetDefaults));
			CloudRecoveryTLOD[1] = ParseFloatSetting(ConfigurationFile, "CloudRecoveryTLOD" + profileSuffix, "100", resetDefaults);
			CloudRecoveryPlus[1] = ParseBoolSetting(ConfigurationFile, "CloudRecoveryPlus" + profileSuffix, "false", resetDefaults);
			if (activeProfile == 1)
			{
				CloudDecreaseGPUPct = ParseFloatSetting(ConfigurationFile, "CloudDecreaseGPUPct" + profileSuffix, "98", resetDefaults);
				CloudRecoverGPUPct = ParseFloatSetting(ConfigurationFile, "CloudRecoverGPUPct" + profileSuffix, "80", resetDefaults);
			}
			IncCloudQ[1] = false;
		}
		CustomAutoOLOD[1] = ParseBoolSetting(ConfigurationFile, "customAutoOLOD" + profileSuffix, "true", resetDefaults);
		OLODAtBase[1] = ParseFloatSetting(ConfigurationFile, "OLODAtBase" + profileSuffix, "100", resetDefaults);
		if (OLODAtBase[1] > SimMaxLOD)
		{
			ConfigurationFile.SetSetting("OLODAtBase" + profileSuffix, SimMaxLOD.ToString());
			OLODAtBase[1] = SimMaxLOD;
		}
		OLODAtTop[1] = ParseFloatSetting(ConfigurationFile, "OLODAtTop" + profileSuffix, "20", resetDefaults);
		if (OLODAtTop[1] < SimMinLOD)
		{
			ConfigurationFile.SetSetting("OLODAtTop" + profileSuffix, SimMinLOD.ToString());
			OLODAtTop[1] = SimMinLOD;
		}
		AltOLODBase[1] = ParseFloatSetting(ConfigurationFile, "AltOLODBase" + profileSuffix, "2000", resetDefaults);
		AltOLODTop[1] = ParseFloatSetting(ConfigurationFile, "AltOLODTop" + profileSuffix, "10000", resetDefaults);
		if (TLODAutoMethod[1] == 2)
		{
			TLODBaseMinus[1] = false;
		}
		else
		{
			TLODBaseMinus[1] = ParseBoolSetting(ConfigurationFile, "TLODBaseMinus" + profileSuffix, "true", resetDefaults);
		}
		if (TLODAutoMethod[2] == 2)
		{
			MinTLODExtra[2] = false;
		}
		else if (TLODAutoMethod[2] == 3)
		{
			MinTLODExtra[2] = true;
			TLODExtraMtns[2] = false;
		}
		if (TLODAutoMethod[2] != 3)
		{
			TLODExtraMtns[2] = ParseBoolSetting(ConfigurationFile, "TLODExtraMtns_VFR", "false", resetDefaults);
		}
		TLODExtraMtnsTriggerAlt[2] = ParseIntSetting(ConfigurationFile, "TLODExtraMtnsTriggerAlt_VFR", "5000", resetDefaults);
		TLODExtraMtnsAmount[2] = ParseIntSetting(ConfigurationFile, "TLODExtraMtnsAmount_VFR", "200", resetDefaults);
		FPSTolerance[2] = ParseIntSetting(ConfigurationFile, "FpsTolerance_VFR", FPSTolerance[1].ToString(), resetDefaults);
		LOSCorrection[2] = false;
		MinTLOD[2] = ParseFloatSetting(ConfigurationFile, "minTLod_VFR", (MinTLOD[1] * 2f).ToString(), resetDefaults);
		if (MinTLOD[2] < SimMinLOD)
		{
			ConfigurationFile.SetSetting("minTLod_VFR", SimMinLOD.ToString());
			MinTLOD[2] = SimMinLOD;
		}
		MaxTLOD[2] = ParseFloatSetting(ConfigurationFile, "maxTLod_VFR", Math.Round((double)MaxTLOD[1] * 1.5).ToString(), resetDefaults);
		if (MaxTLOD[2] > SimMaxLOD)
		{
			ConfigurationFile.SetSetting("maxTLod_VFR", SimMaxLOD.ToString());
			MaxTLOD[2] = SimMaxLOD;
		}
		if (ParseFloatSetting(ConfigurationFile, "MinTLODExtraCapMult_VFR", MinTLODExtraCapMult[1].ToString(CultureInfo.InvariantCulture)) < 1.5f)
		{
			ConfigurationFile.SetSetting("MinTLODExtraCapMult_VFR", 1.5f.ToString(CultureInfo.InvariantCulture));
		}
		MinTLODExtraCapMult[2] = (FreeTLOD[2] ? (MaxTLOD[2] / MinTLOD[2]) : ParseFloatSetting(ConfigurationFile, "MinTLODExtraCapMult_VFR", MinTLODExtraCapMult[1].ToString(CultureInfo.InvariantCulture), resetDefaults));
		if (!ConfigurationFile.SettingExists("NightMode_VFR") && ConfigurationFile.SettingExists("MaxTLODHalvedNight_VFR"))
		{
			bool flag5 = ParseBoolSetting(ConfigurationFile, "MaxTLODHalvedNight_VFR", "false");
			ConfigurationFile.AddSetting("NightMode_VFR", flag5 ? "1" : "0");
		}
		NightMode[2] = ParseIntSetting(ConfigurationFile, "NightMode_VFR", "0", resetDefaults);
		MaxTLODHalvedNight[2] = NightMode[2] == 1 || NightMode[2] == 3;
		NightCloudReduceLevels[2] = ParseIntSetting(ConfigurationFile, "NightCloudReduceLevels_VFR", "2", resetDefaults);
		AltTLODBase[2] = ParseFloatSetting(ConfigurationFile, "AltTLODBase_VFR", 100f.ToString("F0"), resetDefaults);
		if (AltTLODBase[2] < 10f)
		{
			SetSetting("AltTLODBase_VFR", "10");
		}
		AltTLODTop[2] = ParseFloatSetting(ConfigurationFile, "AltTLODTop" + ((TLODAutoMethod[activeProfile] < 2) ? "_SensTol" : "") + "_VFR" + profileSuffix, (TLODAutoMethod[activeProfile] < 2) ? 1000f.ToString("F0") : 2350f.ToString("F0"), resetDefaults);
		if (isMSFS2024)
		{
			if (TLODAutoMethod[2] == 2)
			{
				AutoReduceSettings[2] = false;
				VRAMPlusSuite = 1;
			}
			else if (AutoTargetFPS)
			{
				AutoReduceSettings[2] = false;
			}
			else
			{
				AutoReduceSettings[2] = ParseBoolSetting(ConfigurationFile, "AutoReduceSettings_VFR", "false", resetDefaults);
			}
			ReductionSettingsMaxLevels[2] = ParseIntSetting(ConfigurationFile, "ReductionSettingsMaxLevels_VFR", "2", resetDefaults);
			ReductionSettingsFloor[2] = ParseIntSetting(ConfigurationFile, "ReductionSettingsFloor_VFR", "1", resetDefaults);
			ReductionSettingsSuite[2] = ParseIntSetting(ConfigurationFile, "ReductionSettingsSuite_VFR", "1", resetDefaults);
			ReductionSettingsRecoveryAlt[2] = ParseIntSetting(ConfigurationFile, "ReductionSettingsRecoveryAlt_VFR", "1", resetDefaults);
			ReduceCloudQ[2] = false;
			ReduceTrees[2] = false;
			ReduceRTShadows[2] = false;
			ReduceTShadows[2] = false;
			ReduceBldings[2] = false;
			ReduceDispMap[2] = false;
			ReduceOSTPCache[2] = false;
			ReducePlants[2] = false;
			ReduceGrass[2] = false;
			ReducePkdAcQ[2] = false;
			ReduceAcTrafQ[2] = false;
			if (ReductionSettingsSuite[2] > 0)
			{
				ReduceCloudQ[2] = true;
				ReduceTrees[2] = true;
				ReduceRTShadows[2] = true;
			}
			if (ReductionSettingsSuite[2] > 1)
			{
				ReduceTShadows[2] = true;
				ReduceBldings[2] = true;
				ReduceDispMap[2] = true;
				ReduceOSTPCache[2] = true;
				ReducePlants[2] = true;
				ReduceGrass[2] = true;
				ReducePkdAcQ[2] = true;
				ReduceAcTrafQ[2] = true;
			}
			if (ReductionSettingsSuite[2] == 3)
			{
				ReduceCloudQ[2] = ParseBoolSetting(ConfigurationFile, "ReduceCloudQ_VFR", "false", resetDefaults);
				ReduceTrees[2] = ParseBoolSetting(ConfigurationFile, "ReduceTrees_VFR", "false", resetDefaults);
				ReduceRTShadows[2] = ParseBoolSetting(ConfigurationFile, "ReduceRTShadows_VFR", "false", resetDefaults);
				ReduceTShadows[2] = ParseBoolSetting(ConfigurationFile, "ReduceTShadows_VFR", "false", resetDefaults);
				ReduceBldings[2] = ParseBoolSetting(ConfigurationFile, "ReduceBldings_VFR", "false", resetDefaults);
				ReduceDispMap[2] = ParseBoolSetting(ConfigurationFile, "ReduceDispMap_VFR", "false", resetDefaults);
				ReduceOSTPCache[2] = ParseBoolSetting(ConfigurationFile, "ReduceOSTPCache_VFR", "false", resetDefaults);
				ReducePlants[2] = ParseBoolSetting(ConfigurationFile, "ReducePlants_VFR", "false", resetDefaults);
				ReduceGrass[2] = ParseBoolSetting(ConfigurationFile, "ReduceGrass_VFR", "false", resetDefaults);
				ReducePkdAcQ[2] = ParseBoolSetting(ConfigurationFile, "ReducePkdAcQ_VFR", "false", resetDefaults);
				ReduceAcTrafQ[2] = ParseBoolSetting(ConfigurationFile, "ReduceAcTrafQ_VFR", "false", resetDefaults);
			}
			DecCloudQ[2] = false;
			DecCloudQMethod[2] = 0;
			if (MinTLODExtra[2])
			{
				IncCloudQ[2] = ParseBoolSetting(ConfigurationFile, "IncCloudQ_VFR", "false", resetDefaults);
			}
			else
			{
				IncCloudQ[2] = false;
			}
		}
		else
		{
			AutoReduceSettings[2] = false;
			DecCloudQ[2] = ParseBoolSetting(ConfigurationFile, "DecCloudQ_VFR", DecCloudQ[1].ToString(), resetDefaults);
			DecCloudQMethod[2] = ((TLODAutoMethod[activeProfile] >= 2) ? 1 : ParseIntSetting(ConfigurationFile, "DecCloudQMethod_VFR", DecCloudQMethod[1].ToString(), resetDefaults));
			CloudRecoveryTLOD[2] = ParseFloatSetting(ConfigurationFile, "CloudRecoveryTLOD_VFR", CloudRecoveryTLOD[1].ToString(), resetDefaults);
			CloudRecoveryPlus[2] = ParseBoolSetting(ConfigurationFile, "CloudRecoveryPlus_VFR", CloudRecoveryPlus[1].ToString(), resetDefaults);
			if (activeProfile == 2)
			{
				CloudDecreaseGPUPct = ParseFloatSetting(ConfigurationFile, "CloudDecreaseGPUPct_VFR", "98", resetDefaults);
				CloudRecoverGPUPct = ParseFloatSetting(ConfigurationFile, "CloudRecoverGPUPct_VFR" + profileSuffix, "80", resetDefaults);
			}
			IncCloudQ[2] = false;
		}
		CustomAutoOLOD[2] = ParseBoolSetting(ConfigurationFile, "customAutoOLOD_VFR", CustomAutoOLOD[1].ToString(), resetDefaults);
		OLODAtBase[2] = ParseFloatSetting(ConfigurationFile, "OLODAtBase_VFR", Math.Round((double)OLODAtBase[1] * 1.5).ToString(), resetDefaults);
		if (OLODAtBase[2] > SimMaxLOD)
		{
			ConfigurationFile.SetSetting("OLODAtBase_VFR", SimMaxLOD.ToString());
			OLODAtBase[2] = SimMaxLOD;
		}
		OLODAtTop[2] = ParseFloatSetting(ConfigurationFile, "OLODAtTop_VFR", OLODAtTop[1].ToString(), resetDefaults);
		if (OLODAtTop[2] < SimMinLOD)
		{
			ConfigurationFile.SetSetting("OLODAtTop_VFR", SimMinLOD.ToString());
			OLODAtTop[2] = SimMinLOD;
		}
		AltOLODBase[2] = ParseFloatSetting(ConfigurationFile, "AltOLODBase_VFR", AltOLODBase[1].ToString(), resetDefaults);
		AltOLODTop[2] = ParseFloatSetting(ConfigurationFile, "AltOLODTop_VFR", AltOLODTop[1].ToString(), resetDefaults);
		if (TLODAutoMethod[2] == 2)
		{
			TLODBaseMinus[2] = false;
		}
		else
		{
			TLODBaseMinus[2] = ParseBoolSetting(ConfigurationFile, "TLODBaseMinus_VFR", "true", resetDefaults);
		}
		TLODBasePlusSeekStepSize = FPSTolerance[activeProfile];
		if (isMSFS2024)
		{
			CloudIncreaseGPUMaxLoad = ParseFloatSetting(ConfigurationFile, "CloudIncreaseGPUMaxLoad" + profileSuffix, "90", resetDefaults);
		}
		if (UseExpertOptions && MinTLODExtra[activeProfile] && TLODAutoMethod[activeProfile] < 2 && MinTLOD[activeProfile] * MinTLODExtraCapMult[activeProfile] > MaxTLOD[activeProfile])
		{
			string messageBoxText = "TLOD Base Min × Extra multiplier cannot exceed TLOD Top Max. ";
			float num9 = (float)(Math.Floor(MaxTLOD[activeProfile] / MinTLOD[activeProfile] * 2f) / 2.0);
			float num10 = Math.Max(1.5f, Math.Min(MinTLODExtraCapMult[activeProfile], num9));
			if (MinTLOD[activeProfile] * num10 <= MaxTLOD[activeProfile] && (double)num9 >= 1.5)
			{
				SetSetting((activeProfile == 1) ? ("MinTLODExtraCapMult" + profileSuffix) : "MinTLODExtraCapMult_VFR", num10.ToString("0.0", CultureInfo.InvariantCulture));
				messageBoxText += $"TLOD Base Extra Multiplier has been reduced to {num10:0.0} to fit.";
			}
			else
			{
				SetSetting((activeProfile == 1) ? ("MinTLODExtra" + profileSuffix) : "MinTLODExtra_VFR", "false");
				messageBoxText += "Unable to reduce TLOD Base Extra Multiplier to fit. TLOD Extra has been disabled.";
			}
			((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => (windowOwner == null) ? MessageBox.Show(messageBoxText, "Advice", MessageBoxButton.OK, MessageBoxImage.Asterisk) : MessageBox.Show(windowOwner, messageBoxText, "Advice", MessageBoxButton.OK, MessageBoxImage.Asterisk)));
		}
		if (UseExpertOptions && MinTLODExtra[activeProfile] && TLODAutoMethod[activeProfile] == 3 && !TLODBaseMinus[activeProfile])
		{
			float num11 = MinTLOD[activeProfile] * (MinTLODExtraCapMult[activeProfile] - 1f);
			float num12 = MaxTLOD[activeProfile] * (MaxTLODExtraCapMult[activeProfile] - 1f);
			if (num11 > num12)
			{
				string messageBoxText2 = "TLOD Base Extra cannot exceed TLOD Top Extra. ";
				float num13 = (float)(Math.Floor((num12 / MinTLOD[activeProfile] + 1f) * 2f) / 2.0);
				float num14 = Math.Max(1.5f, Math.Min(MinTLODExtraCapMult[activeProfile], num13));
				if (MinTLOD[activeProfile] * (num14 - 1f) <= num12 && (double)num13 >= 1.5)
				{
					SetSetting((activeProfile == 1) ? ("MinTLODExtraCapMult" + profileSuffix) : "MinTLODExtraCapMult_VFR", num14.ToString("0.0", CultureInfo.InvariantCulture));
					messageBoxText2 = messageBoxText2 + "TLOD Base Extra Multiplier has been reduced to " + num14.ToString("0.0") + " to fit.";
				}
				else
				{
					SetSetting((activeProfile == 1) ? ("TLODBaseMinus" + profileSuffix) : "TLODBaseMinus_VFR", "true");
					messageBoxText2 += "Unable to reduce TLOD Base Extra Multiplier to fit. TLOD Base Extra below Alt TLOD Base has been disabled.";
				}
				((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => (windowOwner == null) ? MessageBox.Show(messageBoxText2, "Advice", MessageBoxButton.OK, MessageBoxImage.Asterisk) : MessageBox.Show(windowOwner, messageBoxText2, "Advice", MessageBoxButton.OK, MessageBoxImage.Asterisk)));
			}
		}
		initialConfigLoaded = true;
		return flag;
	}

	public string GetSetting(string key, string defaultValue = "")
	{
		return ConfigurationFile[key] ?? defaultValue;
	}

	public void SetSetting(string key, string value, bool noLoad = false)
	{
		ConfigurationFile[key] = value;
		if (!noLoad)
		{
			LoadConfiguration();
		}
	}

	private static bool ParseBoolSetting(ConfigurationFile config, string key, string defaultValue, bool resetValue = false)
	{
		string setting = config.GetSetting(key, defaultValue);
		if (!bool.TryParse(setting, out var result) | resetValue)
		{
			if (!resetValue)
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:ParseBoolSetting", $"Invalid boolean value for '{key}': '{setting}'. Using default: {defaultValue}");
			}
			config.SetSetting(key, defaultValue);
			return bool.Parse(defaultValue);
		}
		return result;
	}

	private static int ParseIntSetting(ConfigurationFile config, string key, string defaultValue, bool resetValue = false)
	{
		string setting = config.GetSetting(key, defaultValue);
		if (!int.TryParse(setting, out var result) | resetValue)
		{
			if (!resetValue)
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:ParseIntSetting", $"Invalid integer value for '{key}': '{setting}'. Using default: {defaultValue}");
			}
			config.SetSetting(key, defaultValue);
			return int.Parse(defaultValue);
		}
		return result;
	}

	private static string ParseStringSetting(ConfigurationFile config, string key, string defaultValue, bool resetValue = false)
	{
		string setting = config.GetSetting(key, defaultValue);
		if (string.IsNullOrWhiteSpace(setting) | resetValue)
		{
			if (!resetValue)
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:ParseStringSetting", $"Empty or missing string for '{key}'. Using default: '{defaultValue}'");
			}
			config.SetSetting(key, defaultValue);
			return defaultValue;
		}
		return setting;
	}

	private static float ParseFloatSetting(ConfigurationFile config, string key, string defaultValue, bool resetValue = false)
	{
		string setting = config.GetSetting(key, defaultValue);
		if (!float.TryParse(setting, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) | resetValue)
		{
			if (!resetValue)
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:ParseFloatSetting", $"Invalid float value for '{key}': '{setting}'. Using default: {defaultValue}");
			}
			config.SetSetting(key, defaultValue);
			return float.Parse(defaultValue, CultureInfo.InvariantCulture);
		}
		return result;
	}

	public static long ParseLongSetting(ConfigurationFile config, string key, string defaultValue, NumberStyles style = NumberStyles.Integer, bool resetValue = false)
	{
		string text = config.GetSetting(key, defaultValue);
		if ((style & NumberStyles.HexNumber) != 0)
		{
			if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				text = text.Substring(2);
			}
			if (defaultValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				defaultValue = defaultValue.Substring(2);
			}
		}
		if (!long.TryParse(text, style, CultureInfo.InvariantCulture, out var result) | resetValue)
		{
			if (!resetValue)
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:ParseLongSetting", $"Invalid long value for '{key}': '{text}'. Using default: {defaultValue}");
			}
			config.SetSetting(key, ((style & NumberStyles.HexNumber) != 0) ? ("0x" + defaultValue) : defaultValue);
			return long.Parse(defaultValue, style, CultureInfo.InvariantCulture);
		}
		return result;
	}

	public static string GetLSFGSettingsPath()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lossless Scaling", "Settings.xml");
	}

	public static string GetMSMSettingsPath()
	{
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MFG SUPER MOTION", "preferences.json");
	}

	public bool IsLSFGInstalled()
	{
		return Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lossless Scaling"));
	}

	public bool IsMSMInstalled()
	{
		return Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MFG SUPER MOTION"));
	}

	public bool IsOFXRInstalled()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		if (!Directory.Exists(Path.Combine(folderPath, "ofxr-bridge")))
		{
			return Directory.Exists(Path.Combine(folderPath, "OFXR Bridge"));
		}
		return true;
	}

	public int GetLSModeMultiplier()
	{
		string lSFGSettingsPath = GetLSFGSettingsPath();
		if (File.GetLastWriteTime(lSFGSettingsPath) != lastLSFGSetModify)
		{
			lastLSFGSetModify = File.GetLastWriteTime(lSFGSettingsPath);
			LsModeAdaptive = false;
			XDocument xDocument = XDocument.Load(lSFGSettingsPath);
			XElement xElement = xDocument.Descendants("Profile").FirstOrDefault((XElement p) => p.Element("Title")?.Value == (isMSFS2024 ? "MSFS2024" : "MSFS2020"));
			if (xElement == null)
			{
				xElement = xDocument.Descendants("Profile").FirstOrDefault();
			}
			if (xElement != null)
			{
				XElement frameGenerationElement = xElement.Elements("FrameGeneration").FirstOrDefault();
				if (frameGenerationElement != null)
				{
					if (frameGenerationElement.Value == "LSFG1")
					{
						return 2;
					}
					if (frameGenerationElement.Value != "Off")
					{
						XElement xElement2 = (from e in xElement.Elements()
							where e.Name.LocalName.StartsWith(frameGenerationElement.Value + "Mode")
							select e).FirstOrDefault();
						if (xElement2 != null)
						{
							if (!(xElement2.Value == "ADAPTIVE"))
							{
								if (xElement2.Value == "CUSTOM" || xElement2.Value == "FIXED")
								{
									xElement2 = xElement.Elements("LSFG3Multiplier").FirstOrDefault();
									xElement2.Value = "X" + xElement2.Value;
								}
								if (!int.TryParse(xElement2.Value.Substring(1), out var result))
								{
									return 1;
								}
								return result;
							}
							LsModeAdaptive = true;
						}
					}
				}
			}
			return 1;
		}
		return LsModeMultiplier;
	}

	public int GetMSMMultiplier()
	{
		string mSMSettingsPath = GetMSMSettingsPath();
		if (!File.Exists(mSMSettingsPath))
		{
			return 1;
		}
		if (File.GetLastWriteTime(mSMSettingsPath) != lastMSMSetModify)
		{
			lastMSMSetModify = File.GetLastWriteTime(mSMSettingsPath);
			MsmModeAdaptive = false;
			try
			{
				using JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(mSMSettingsPath));
				JsonElement rootElement = jsonDocument.RootElement;
				string text = (rootElement.TryGetProperty("SelectedProfile", out var value) ? value.GetString() : "default");
				if (rootElement.TryGetProperty("Profiles", out var value2) && value2.ValueKind == JsonValueKind.Array)
				{
					JsonElement jsonElement = default;
					bool flag = false;
					string b = (isMSFS2024 ? "MSFS2024" : "MSFS2020");
					foreach (JsonElement item in value2.EnumerateArray())
					{
						if (item.TryGetProperty("Name", out var value3) && string.Equals(value3.GetString(), b, StringComparison.OrdinalIgnoreCase))
						{
							jsonElement = item;
							flag = true;
							break;
						}
					}
					if (!flag && !string.IsNullOrEmpty(text))
					{
						foreach (JsonElement item2 in value2.EnumerateArray())
						{
							if ((item2.TryGetProperty("Id", out var value4) && string.Equals(value4.GetString(), text, StringComparison.OrdinalIgnoreCase)) || (item2.TryGetProperty("Name", out var value5) && string.Equals(value5.GetString(), text, StringComparison.OrdinalIgnoreCase)))
							{
								jsonElement = item2;
								flag = true;
								break;
							}
						}
					}
					if (!flag)
					{
						JsonElement.ArrayEnumerator arrayEnumerator2 = value2.EnumerateArray();
						if (arrayEnumerator2.MoveNext())
						{
							jsonElement = arrayEnumerator2.Current;
							flag = true;
						}
					}
					if (flag && jsonElement.TryGetProperty("Options", out var value6))
					{
						if (!value6.TryGetProperty("FrameGeneration", out var value7) || !value7.GetBoolean())
						{
							return 1;
						}
						if (value6.TryGetProperty("Adaptive", out var value8) && value8.GetBoolean())
						{
							MsmModeAdaptive = true;
							return 1;
						}
						JsonElement value9;
						return Math.Clamp((value6.TryGetProperty("Mode", out value9) ? value9.GetInt32() : 0) + 2, 2, 6);
					}
				}
			}
			catch
			{
				return MsmMultiplier;
			}
			return 1;
		}
		return MsmMultiplier;
	}

	public (string logPath, string iniPath, string statusPath) GetActiveOFXRPaths()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string path = Path.Combine(folderPath, "OFXR Bridge");
		string text = Path.Combine(path, "tray-lifecycle.log");
		string text2 = Path.Combine(path, "tray.ini");
		string text3 = Path.Combine(path, "status.json");
		string path2 = Path.Combine(folderPath, "ofxr-bridge");
		string text4 = Path.Combine(path2, "tray-lifecycle.log");
		string text5 = Path.Combine(path2, "status.json");
		string path3 = (isMSFS2024 ? "FlightSimulator2024.exe.ini" : "FlightSimulator.exe.ini");
		string text6 = Path.Combine(path2, "Profiles", path3);
		string text7 = (File.Exists(text6) ? text6 : Path.Combine(path2, "ofxr-bridge.ini"));
		DateTime dateTime = DateTime.MinValue;
		if (File.Exists(text3))
		{
			dateTime = File.GetLastWriteTime(text3);
		}
		if (File.Exists(text))
		{
			DateTime lastWriteTime = File.GetLastWriteTime(text);
			if (lastWriteTime > dateTime)
			{
				dateTime = lastWriteTime;
			}
		}
		if (File.Exists(text2))
		{
			DateTime lastWriteTime2 = File.GetLastWriteTime(text2);
			if (lastWriteTime2 > dateTime)
			{
				dateTime = lastWriteTime2;
			}
		}
		DateTime dateTime2 = DateTime.MinValue;
		if (File.Exists(text5))
		{
			dateTime2 = File.GetLastWriteTime(text5);
		}
		if (File.Exists(text4))
		{
			DateTime lastWriteTime3 = File.GetLastWriteTime(text4);
			if (lastWriteTime3 > dateTime2)
			{
				dateTime2 = lastWriteTime3;
			}
		}
		if (File.Exists(text7))
		{
			DateTime lastWriteTime4 = File.GetLastWriteTime(text7);
			if (lastWriteTime4 > dateTime2)
			{
				dateTime2 = lastWriteTime4;
			}
		}
		if (dateTime2 > dateTime && dateTime2 != DateTime.MinValue)
		{
			return (logPath: text4, iniPath: text7, statusPath: File.Exists(text5) ? text5 : null);
		}
		if (dateTime != DateTime.MinValue)
		{
			return (logPath: text, iniPath: text2, statusPath: File.Exists(text3) ? text3 : null);
		}
		return (logPath: null, iniPath: null, statusPath: null);
	}

	public int GetOFXRModeMultiplier(bool forceDetect = false)
	{
		if (OfxrStatusJsonActive)
		{
			return _cachedOfxrMultiplier;
		}
		string item = GetActiveOFXRPaths().iniPath;
		if (string.IsNullOrEmpty(item) || !File.Exists(item))
		{
			OfxrModeAuto = false;
			_cachedOfxrMultiplier = 1;
			return _cachedOfxrMultiplier;
		}
		DateTime lastWriteTime = File.GetLastWriteTime(item);
		if (forceDetect || lastWriteTime != lastOFXRSetModify)
		{
			lastOFXRSetModify = lastWriteTime;
			try
			{
				bool flag = false;
				bool flag2 = false;
				using (FileStream stream = new FileStream(item, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					using StreamReader streamReader = new StreamReader(stream);
					string text;
					while ((text = streamReader.ReadLine()) != null)
					{
						string text2 = text.Trim();
						if (text2.StartsWith("triple_frame_gen", StringComparison.OrdinalIgnoreCase))
						{
							int num = text2.IndexOf('=');
							if (num > 0 && text2.Substring(num + 1).Trim() == "1")
							{
								flag = true;
							}
						}
						else if (text2.StartsWith("frames_auto", StringComparison.OrdinalIgnoreCase))
						{
							int num2 = text2.IndexOf('=');
							if (num2 > 0 && text2.Substring(num2 + 1).Trim() == "1")
							{
								flag2 = true;
							}
						}
					}
				}
				if (flag2)
				{
					OfxrModeAuto = true;
					_cachedOfxrMultiplier = 1;
				}
				else if (flag)
				{
					OfxrModeAuto = false;
					_cachedOfxrMultiplier = 3;
				}
				else
				{
					OfxrModeAuto = false;
					_cachedOfxrMultiplier = 2;
				}
			}
			catch
			{
				OfxrModeAuto = false;
				_cachedOfxrMultiplier = 1;
			}
		}
		return _cachedOfxrMultiplier;
	}

	public bool IsOFXRArmed(bool vrActive, bool forceDetect = false)
	{
		if (ReadOFXRStatusJson(vrActive, forceDetect))
		{
			return true;
		}
		if (OfxrStatusJsonActive && isOfxrDisarmed)
		{
			return false;
		}
		string item = GetActiveOFXRPaths().logPath;
		if (string.IsNullOrEmpty(item) || !File.Exists(item))
		{
			isOfxrDisarmed = true;
			return false;
		}
		try
		{
			DateTime lastWriteTime = File.GetLastWriteTime(item);
			if (forceDetect || lastWriteTime != lastOFXRLogModify)
			{
				lastOFXRLogModify = lastWriteTime;
				using FileStream fileStream = new FileStream(item, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				if (fileStream.Length == 0L)
				{
					return false;
				}
				long offset = Math.Max(0L, fileStream.Length - 2048);
				fileStream.Seek(offset, SeekOrigin.Begin);
				using StreamReader streamReader = new StreamReader(fileStream);
				string[] array = streamReader.ReadToEnd().Split(new string[2] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
				bool flag = false;
				bool flag2 = false;
				for (int num = array.Length - 1; num >= 0; num--)
				{
					string text = array[num].ToLowerInvariant();
					if (!flag)
					{
						if (text.Contains("disarm") || text.Contains("disable"))
						{
							isOfxrDisarmed = true;
							flag = true;
						}
						else if (text.Contains("arm") || text.Contains("enable"))
						{
							isOfxrDisarmed = false;
							flag = true;
						}
					}
					if (!flag2)
					{
						if (text.Contains("pause"))
						{
							isOfxrPaused = true;
							flag2 = true;
						}
						else if (text.Contains("resume") || text.Contains("startup") || text.Contains("disarm") || text.Contains("disable"))
						{
							isOfxrPaused = false;
							flag2 = true;
						}
					}
					if (flag & flag2)
					{
						break;
					}
				}
				if (!flag2)
				{
					isOfxrPaused = false;
				}
			}
		}
		catch
		{
			return false;
		}
		return !isOfxrDisarmed;
	}

	public bool IsOFXRPaused()
	{
		return isOfxrPaused;
	}

	public bool ReadOFXRStatusJson(bool vrActive, bool forceDetect = false)
	{
		string item = GetActiveOFXRPaths().statusPath;
		if (string.IsNullOrEmpty(item) || !File.Exists(item))
		{
			OfxrStatusJsonActive = false;
			OfxrOutputFps = 0f;
			OfxrBuild = 0;
			lastOfxrBuild = 0;
			hasLoggedStaleStatusJson = false;
			return false;
		}
		try
		{
			DateTime lastWriteTime = File.GetLastWriteTime(item);
			if (forceDetect || lastWriteTime != lastOFXRStatusJsonModify)
			{
				lastOFXRStatusJsonModify = lastWriteTime;
				string json;
				using (FileStream stream = new FileStream(item, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					using StreamReader streamReader = new StreamReader(stream);
					json = streamReader.ReadToEnd();
				}
				using JsonDocument jsonDocument = JsonDocument.Parse(json);
				JsonElement rootElement = jsonDocument.RootElement;
				if (rootElement.TryGetProperty("updated", out var value) && DateTime.TryParse(value.GetString(), null, DateTimeStyles.AdjustToUniversal, out var result))
				{
					OfxrStatusJsonLatency = (float)(DateTime.UtcNow - result).TotalMilliseconds;
					if ((DateTime.UtcNow - result).TotalSeconds > 4.0)
					{
						if (!hasLoggedStaleStatusJson)
						{
							Logger.Log(LogLevel.Information, "ServiceModel:ReadOFXRStatusJson", "OFXR status.json is stale (>4s old). Falling back to legacy log/ini detection.");
							hasLoggedStaleStatusJson = true;
						}
						OfxrStatusJsonActive = false;
						OfxrOutputFps = 0f;
						OfxrBuild = 0;
						lastOfxrBuild = 0;
						return false;
					}
				}
				hasLoggedStaleStatusJson = false;
				bool flag = rootElement.TryGetProperty("armed", out var value2) && value2.GetBoolean();
				isOfxrDisarmed = !flag;
				bool flag2 = rootElement.TryGetProperty("paused", out var value3) && value3.GetBoolean();
				isOfxrPaused = flag2;
				if (rootElement.TryGetProperty("multiplier", out var value4) && value4.TryGetInt32(out var value5))
				{
					_cachedOfxrMultiplier = value5;
				}
				bool flag3 = rootElement.TryGetProperty("session", out var value6) && value6.ValueKind == JsonValueKind.Object;
				OfxrGenerating = rootElement.TryGetProperty("generating", out var value7) && value7.ValueKind == JsonValueKind.True;
				if (rootElement.TryGetProperty("app_fps", out var value8) && value8.ValueKind == JsonValueKind.Number)
				{
					OfxrAppFps = (float)value8.GetDouble();
				}
				else
				{
					OfxrAppFps = 0f;
				}
				if (rootElement.TryGetProperty("output_fps", out var value9) && value9.ValueKind == JsonValueKind.Number)
				{
					OfxrOutputFps = (float)value9.GetDouble();
				}
				else
				{
					OfxrOutputFps = 0f;
				}
				if (rootElement.TryGetProperty("display_hz", out var value10) && value10.ValueKind == JsonValueKind.Number)
				{
					OfxrDisplayHz = (float)value10.GetDouble();
				}
				if (rootElement.TryGetProperty("ofxr", out var value11) && value11.ValueKind == JsonValueKind.Object && value11.TryGetProperty("build", out var value12) && value12.TryGetInt32(out var value13))
				{
					OfxrBuild = value13;
					if (OfxrBuild != lastOfxrBuild)
					{
						Logger.Log(LogLevel.Information, "ServiceModel:ReadOFXRStatusJson", $"OFXR Bridge build {OfxrBuild} detected");
						lastOfxrBuild = OfxrBuild;
					}
				}
				OfxrStatusJsonActive = true;
				return flag && (!vrActive | flag3);
			}
			return OfxrStatusJsonActive && !isOfxrDisarmed;
		}
		catch
		{
			OfxrStatusJsonActive = false;
			return false;
		}
	}

	public bool IsVXRInstalled()
	{
		if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VectorXR")))
		{
			return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VECTORXR_RUNTIME_RELAY_PATH"));
		}
		return true;
	}

	public bool ReadVXRStatusJson(bool vrActive, bool forceDetect = false)
	{
		try
		{
			FileInfo fileInfo = null;
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string text = Path.Combine(folderPath, "VectorXR", "api", "status.json");
			if (File.Exists(text))
			{
				fileInfo = new FileInfo(text);
			}
			else
			{
				string text2 = Environment.GetEnvironmentVariable("VECTORXR_RUNTIME_RELAY_PATH");
				if (string.IsNullOrEmpty(text2))
				{
					text2 = Path.Combine(folderPath, "VectorXR", "runtime");
				}
				string path = Path.Combine(text2, "status");
				if (Directory.Exists(path))
				{
					FileInfo[] files = new DirectoryInfo(path).GetFiles("*.json");
					DateTime dateTime = DateTime.MinValue;
					FileInfo[] array = files;
					foreach (FileInfo fileInfo2 in array)
					{
						if (fileInfo2.LastWriteTimeUtc > dateTime)
						{
							dateTime = fileInfo2.LastWriteTimeUtc;
							fileInfo = fileInfo2;
						}
					}
				}
			}
			if (fileInfo == null)
			{
				VxrStatusJsonActive = false;
				return false;
			}
			if (forceDetect || fileInfo.LastWriteTimeUtc != lastVXRStatusJsonModify)
			{
				lastVXRStatusJsonModify = fileInfo.LastWriteTimeUtc;
				string json;
				using (FileStream stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
				{
					using StreamReader streamReader = new StreamReader(stream);
					json = streamReader.ReadToEnd();
				}
				using JsonDocument jsonDocument = JsonDocument.Parse(json);
				JsonElement rootElement = jsonDocument.RootElement;
				if (rootElement.TryGetProperty("application", out var value))
				{
					string? obj = value.GetString() ?? "";
					string value2 = (isMSFS2024 ? "FlightSimulator2024.exe" : "FlightSimulator.exe");
					if (!obj.Equals(value2, StringComparison.OrdinalIgnoreCase))
					{
						VxrStatusJsonActive = false;
						return false;
					}
				}
				if (rootElement.TryGetProperty("updatedAtUnixMilliseconds", out var value3) && value3.TryGetInt64(out var value4) && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - value4 > 4000)
				{
					if (!hasLoggedStaleVxrStatusJson)
					{
						Logger.Log(LogLevel.Information, "ServiceModel:ReadVXRStatusJson", "VectorXR relay status is stale (>4s old).");
						hasLoggedStaleVxrStatusJson = true;
					}
					VxrStatusJsonActive = false;
					return false;
				}
				hasLoggedStaleVxrStatusJson = false;
				bool flag = rootElement.TryGetProperty("framegen", out var value5);
				string text3 = "";
				JsonElement value7;
				JsonElement value8;
				if (flag && value5.TryGetProperty("state", out var value6))
				{
					text3 = value6.GetString()?.ToLowerInvariant() ?? "";
				}
				else if (rootElement.TryGetProperty("state", out value7) && value7.TryGetProperty("framegenState", out value8))
				{
					text3 = value8.GetString()?.ToLowerInvariant() ?? "";
				}
				isVxrPaused = text3 == "paused";
				isVxrStandby = text3 == "standby";
				bool flag2 = text3 == "waiting";
				if (text3 == "unavailable" || text3 == "off" || string.IsNullOrEmpty(text3))
				{
					VxrStatusJsonActive = false;
					return false;
				}
				if (flag)
				{
					VxrGenerating = value5.TryGetProperty("generating", out var value9) && value9.GetBoolean();
					VxrAppFps = ((value5.TryGetProperty("appFps", out var value10) && value10.TryGetSingle(out var value11)) ? value11 : 0f);
					VxrOutputFps = ((value5.TryGetProperty("outputFps", out var value12) && value12.TryGetSingle(out var value13)) ? value13 : 0f);
					VxrDistinctFps = ((value5.TryGetProperty("distinctFps", out var value14) && value14.TryGetSingle(out var value15)) ? value15 : 0f);
					JsonElement value18;
					float value19;
					if (value5.TryGetProperty("targetAppFps", out var value16) && value16.TryGetSingle(out var value17))
					{
						VxrHoldFps = value17;
					}
					else if (value5.TryGetProperty("holdFps", out value18) && value18.TryGetSingle(out value19))
					{
						VxrHoldFps = value19;
					}
					else
					{
						VxrHoldFps = 0f;
					}
					VxrDisplayHz = ((value5.TryGetProperty("refreshHz", out var value20) && value20.TryGetSingle(out var value21)) ? value21 : 0f);
					_cachedVxrFrameRate = ((!value5.TryGetProperty("frameRate", out var value22)) ? "" : (value22.GetString()?.ToLowerInvariant() ?? ""));
					_cachedVxrMultiplier = ((!value5.TryGetProperty("multiplier", out var value23) || !value23.TryGetInt32(out var value24)) ? 1 : value24);
					VxrModeAuto = _cachedVxrFrameRate == "auto";
					VxrModeDynamic = _cachedVxrFrameRate == "dynamic";
					if (isVxrStandby | flag2)
					{
						_cachedVxrMultiplier = 1;
					}
				}
				VxrStatusJsonActive = true;
				return true;
			}
			return VxrStatusJsonActive;
		}
		catch
		{
			VxrStatusJsonActive = false;
			return false;
		}
	}

	public int GetVXRModeMultiplier(bool forceDetect = false)
	{
		if (ReadVXRStatusJson(vrActive: true, forceDetect))
		{
			return _cachedVxrMultiplier;
		}
		VxrMode = "";
		_cachedVxrMultiplier = 0;
		return 0;
	}

	public bool IsVXRPaused()
	{
		return isVxrPaused;
	}

	public bool IsVXRStandby()
	{
		return isVxrStandby;
	}

	public bool UpdateDynamicSettingsState()
	{
		if (MemoryAccess != null && isMSFS2024)
		{
			bool dynSet = DynSet;
			bool dynSet_VR = DynSet_VR;
			bool dynSet2 = (DefaultDynSet = MemoryAccess.GetDynSet());
			DynSet = dynSet2;
			dynSet2 = (DefaultDynSetVR = MemoryAccess.GetDynSet(VR: true));
			DynSet_VR = dynSet2;
			if (!UseExpertOptions)
			{
				DefaultDynSetFrameRateTarget = MemoryAccess.GetDynSetFrameRateTarget();
				DefaultDynSetFrameRateTarget_VR = MemoryAccess.GetDynSetFrameRateTarget(VR: true);
				Logger.Log(LogLevel.Information, "ServiceModel:UpdateDynamicSettingsState", "Dynamic Settings defaults saved");
			}
			else if (VrModeActive ? DefaultDynSetVR : DefaultDynSet)
			{
				int dynSetTargetFPS = (DefaultDynSetFrameRateTarget = MemoryAccess.GetDynSetFrameRateTarget());
				DynSetTargetFPS = dynSetTargetFPS;
				dynSetTargetFPS = (DefaultDynSetFrameRateTarget_VR = MemoryAccess.GetDynSetFrameRateTarget(VR: true));
				DynSetTargetFPS_VR = dynSetTargetFPS;
				Logger.Log(LogLevel.Information, "ServiceModel:UpdateDynamicSettingsState", $"Dynamic Settings enabled at frame rate target {(VrModeActive ? DefaultDynSetFrameRateTarget_VR : DefaultDynSetFrameRateTarget)} FPS");
			}
			else
			{
				Logger.Log(LogLevel.Information, "ServiceModel:UpdateDynamicSettingsState", "Dynamic Settings disabled");
			}
			if (VrModeActive && DynSet_VR != dynSet_VR)
			{
				return true;
			}
			if (!VrModeActive && DynSet != dynSet)
			{
				return true;
			}
		}
		return false;
	}

	public void SetDynamicSettingsState()
	{
		if (MemoryAccess != null && isMSFS2024)
		{
			MemoryAccess.SetDynSet_VR(value: false);
			DynSet_VR = MemoryAccess.GetDynSet(VR: true);
			MemoryAccess.SetDynSet_PC(value: false);
			DynSet = MemoryAccess.GetDynSet();
			Logger.Log(LogLevel.Information, "ServiceModel:SetDynamicSettingsState", "Dynamic Settings auto-disabled");
		}
	}

	public void ResetDynamicSettingsState()
	{
		if (MemoryAccess != null && isMSFS2024)
		{
			MemoryManager memoryAccess = MemoryAccess;
			bool dynSet_PC = (DynSet = DefaultDynSet);
			memoryAccess.SetDynSet_PC(dynSet_PC);
			MemoryManager memoryAccess2 = MemoryAccess;
			dynSet_PC = (DynSet_VR = DefaultDynSetVR);
			memoryAccess2.SetDynSet_VR(dynSet_PC);
			MemoryManager memoryAccess3 = MemoryAccess;
			int value = (DynSetTargetFPS = DefaultDynSetFrameRateTarget);
			memoryAccess3.SetDynSetFrameRateTarget(value);
			MemoryManager memoryAccess4 = MemoryAccess;
			value = (DynSetTargetFPS_VR = DefaultDynSetFrameRateTarget_VR);
			memoryAccess4.SetDynSetFrameRateTargetVR(value);
			Logger.Log(LogLevel.Information, "ServiceModel:ResetDynamicSettingsState", "Dynamic Settings defaults restored");
		}
	}

	public void ResetReductionSettings(bool VR = false)
	{
		if (MemoryAccess != null)
		{
			MemoryManager memoryAccess = MemoryAccess;
			int value;
			if (!VR)
			{
				int num = (Trees = DefaultTrees);
				value = num;
			}
			else
			{
				int num = (Trees_VR = DefaultTrees_VR);
				value = num;
			}
			memoryAccess.SetTrees(value, VR);
			MemoryManager memoryAccess2 = MemoryAccess;
			bool value2;
			if (!VR)
			{
				bool flag = (RTShadows = DefaultRTShadows);
				value2 = flag;
			}
			else
			{
				bool flag = (RTShadows_VR = DefaultRTShadows_VR);
				value2 = flag;
			}
			memoryAccess2.SetRTShadows(value2, VR);
			MemoryManager memoryAccess3 = MemoryAccess;
			bool value3;
			if (!VR)
			{
				bool flag = (TShadowsEnabled = DefaultTShadowsEnabled);
				value3 = flag;
			}
			else
			{
				bool flag = (TShadowsEnabled_VR = DefaultTShadowsEnabled_VR);
				value3 = flag;
			}
			memoryAccess3.SetTShadowsEnabled(value3, VR);
			MemoryManager memoryAccess4 = MemoryAccess;
			int value4;
			if (!VR)
			{
				int num = (TShadows = DefaultTShadows);
				value4 = num;
			}
			else
			{
				int num = (TShadows_VR = DefaultTShadows_VR);
				value4 = num;
			}
			memoryAccess4.SetTerrainShadows(value4, VR);
			MemoryManager memoryAccess5 = MemoryAccess;
			int value5;
			if (!VR)
			{
				int num = (Bldings = DefaultBldings);
				value5 = num;
			}
			else
			{
				int num = (Bldings_VR = DefaultBldings_VR);
				value5 = num;
			}
			memoryAccess5.SetBuildings(value5, VR);
			MemoryManager memoryAccess6 = MemoryAccess;
			bool value6;
			if (!VR)
			{
				bool flag = (DispMap = DefaultDispMap);
				value6 = flag;
			}
			else
			{
				bool flag = (DispMap_VR = DefaultDispMap_VR);
				value6 = flag;
			}
			memoryAccess6.SetDispMap(value6, VR);
			MemoryManager memoryAccess7 = MemoryAccess;
			int value7;
			if (!VR)
			{
				int num = (OSTPCache = DefaultOSTPCache);
				value7 = num;
			}
			else
			{
				int num = (OSTPCache_VR = DefaultOSTPCache_VR);
				value7 = num;
			}
			memoryAccess7.SetOSTPreCaching(value7, VR);
			MemoryManager memoryAccess8 = MemoryAccess;
			int value8;
			if (!VR)
			{
				int num = (Plants = DefaultPlants);
				value8 = num;
			}
			else
			{
				int num = (Plants_VR = DefaultPlants_VR);
				value8 = num;
			}
			memoryAccess8.SetPlants(value8, VR);
			MemoryManager memoryAccess9 = MemoryAccess;
			int value9;
			if (!VR)
			{
				int num = (Grass = DefaultGrass);
				value9 = num;
			}
			else
			{
				int num = (Grass_VR = DefaultGrass_VR);
				value9 = num;
			}
			memoryAccess9.SetGrass(value9, VR);
			MemoryManager memoryAccess10 = MemoryAccess;
			int value10;
			if (!VR)
			{
				int num = (PkdAcQ = DefaultPkdAcQ);
				value10 = num;
			}
			else
			{
				int num = (PkdAcQ_VR = DefaultPkdAcQ_VR);
				value10 = num;
			}
			memoryAccess10.SetPkdAcQ(value10, VR);
			MemoryManager memoryAccess11 = MemoryAccess;
			int value11;
			if (!VR)
			{
				int num = (AcTrafQ = DefaultAcTrafQ);
				value11 = num;
			}
			else
			{
				int num = (AcTrafQ_VR = DefaultAcTrafQ_VR);
				value11 = num;
			}
			memoryAccess11.SetAcTrafQ(value11, VR);
			if (VR)
			{
				MemoryAccess.SetOLOD_VR(DefaultOLOD_VR);
			}
			else
			{
				MemoryAccess.SetOLOD_PC(DefaultOLOD);
			}
			olod = (VrModeActive ? DefaultOLOD_VR : DefaultOLOD);
			MinTLODReductionAmount = 0f;
			ReductionSettingsDepth = 0;
			VRAMSettingsReduceActive = false;
			VRAMHoldActive = false;
			VRAMReduceActive = false;
			VRAMProtInactive = true;
			VRAMSettingsReduceActive = false;
		}
	}

	public void ResetCloudsLODs(bool ResetTLOD = true, bool ResetAll = false)
	{
		if (MonitorMode)
		{
			if (VrModeActive)
			{
				tlod = DefaultTLOD_VR;
				olod = DefaultOLOD_VR;
			}
			else
			{
				tlod = DefaultTLOD;
				olod = DefaultOLOD;
			}
		}
		else
		{
			if (MemoryAccess == null || !DefaultSettingsRead)
			{
				return;
			}
			int num = 5;
			while (LODControllerBusy && num-- > 0)
			{
				Thread.Sleep(100);
			}
			LODControllerBusy = false;
			ResetInProgress = true;
			if (Process.GetProcessesByName("RTSS").Length == 0)
			{
				fpsSourceRTSS = false;
			}
			else
			{
				fpsSourceRTSS = Convert.ToBoolean(ConfigurationFileCommon.GetSetting("fpsSourceRTSS", "true"));
			}
			Logger.Log(LogLevel.Information, "ServiceModel:ResetCloudsTLOD", "FPS source set to " + (fpsSourceRTSS ? "RTSS" : "MSFS"));
			if (ResetAll)
			{
				MemoryAccess.IsFgModeEnabled();
				if (Process.GetProcessesByName("LosslessScaling").Length != 0)
				{
					LsModeEnabled = true;
					LsModeMultiplier = GetLSModeMultiplier();
				}
				else
				{
					LsModeEnabled = false;
				}
				if (Process.GetProcessesByName("Mfg Super Motion").Length != 0)
				{
					MsmModeEnabled = true;
					MsmMultiplier = GetMSMMultiplier();
				}
				else
				{
					MsmModeEnabled = false;
				}
				Logger.Log(LogLevel.Information, "ServiceModel:ResetCloudsTLOD", "Full reset of LODs, clouds, graphics mode and Auto Target FPS states activated");
				ActiveGraphicsModeChanged = true;
			}
			if (DecCloudQActive | ResetAll)
			{
				MemoryManager memoryAccess = MemoryAccess;
				int value = (cloudQ_VR = DefaultCloudQ_VR);
				memoryAccess.SetCloudQ(value, VR: true);
				MemoryManager memoryAccess2 = MemoryAccess;
				value = (cloudQ = DefaultCloudQ);
				memoryAccess2.SetCloudQ(value);
				DecCloudQActive = false;
			}
			if (AutoTargetFPS)
			{
				ForceAutoFPSCal = true;
				FPSSettleActive = true;
				TargetFPS = 0f;
			}
			else
			{
				ForceAutoFPSCal = false;
			}
			if (isMSFS2024 & ResetAll)
			{
				ResetReductionSettings(VR: true);
				ResetReductionSettings();
				IncCloudQActive = false;
				DynamicSettingsActive = false;
			}
			if (LogPlus)
			{
				PerfLogResetPending = true;
			}
			VRAMSettingsReduceAlt = ((VRAMPlusSuite >= 2) ? ReductionSettingsRecoveryAlt[activeProfile] : 0);
			if (TLODExtraMtns[activeProfile])
			{
				TLODExtraMtnsAmountResidual = 0f;
			}
			if ((ResetTLOD && OnGround) | ResetAll)
			{
				LockTLODMax = false;
				if (StartTLODExtraAtMax[activeProfile] && ((TLODAutoMethod[activeProfile] == 3 && !TLODBaseMinus[activeProfile]) || (TLODAutoMethod[activeProfile] < 2 && MinTLODExtra[activeProfile])))
				{
					MinTLODExtraActive = true;
					if (TLODAutoMethod[activeProfile] == 3 && altAboveGnd > AltTLODTop[activeProfile] + 100f)
					{
						MinTLODExtraAmount = MaxTLOD[activeProfile] * (MaxTLODExtraCapMult[activeProfile] - 1f);
						AboveAltTLODTopPlusTolerance = true;
						AboveAltTLODBase = true;
					}
					else
					{
						MinTLODExtraAmount = MinTLOD[activeProfile] * (MinTLODExtraCapMult[activeProfile] - 1f);
					}
					if (MaxTLODHalvedNight[activeProfile] && NightTime)
					{
						MinTLODExtraAmount = Math.Min(MinTLODExtraAmount, MaxTLOD[activeProfile] / 2f - MinTLOD[activeProfile]);
					}
					FPSSettleInitial = false;
					MinTLODExtraSeeking = false;
					MinTLODExtraSeekingInitial = false;
				}
				else if ((altAboveGnd < AltTLODBase[activeProfile] || (TLODAutoMethod[activeProfile] >= 2 && MinTLODExtra[activeProfile])) | ResetAll)
				{
					MinTLODExtraActive = false;
					MinTLODExtraSeeking = false;
					MinTLODExtraAmount = 0f;
					MinTLODExtraIterations = 8;
					NightTimeTransiton = false;
					AboveAltTLODTopPlusTolerance = false;
					AboveAltTLODBase = false;
				}
				TLODBasePlusSeekHeadroom = TLODBasePlusSeekMinHeadroom;
				float num2;
				if (TLODAutoMethod[activeProfile] < 2)
				{
					num2 = ((!UseExpertOptions) ? Math.Max((VrModeActive ? DefaultTLOD_VR : DefaultTLOD) * (FlightTypeIFR ? 0.5f : 1f) * (AutoTargetFPS ? 1f : 0.5f), 10f) : MinTLOD[activeProfile]);
				}
				else
				{
					if (StartTLODExtraAtMax[activeProfile])
					{
						MinTLODExtraSeekingInitial = false;
					}
					else if (TLODBaseMinus[activeProfile])
					{
						if (altAboveGnd < AltTLODTop[activeProfile])
						{
							MinTLODExtraIterations = 0;
							MinTLODExtraSeekingInitial = false;
						}
						else
						{
							MinTLODExtraSeekingInitial = true;
						}
					}
					else
					{
						MinTLODExtraSeekingInitial = true;
					}
					if (!TLODBaseMinus[activeProfile] && altAboveGnd < AltTLODTop[activeProfile] + 100f)
					{
						Logger.Log(LogLevel.Information, "ServiceModel:ResetCloudsTLOD", "Activating TLOD Base Extra seek process on TLOD reset");
					}
					if (altAboveGnd < AltTLODBase[activeProfile])
					{
						num2 = MinTLOD[activeProfile];
					}
					else if (altAboveGnd > AltTLODTop[activeProfile])
					{
						num2 = ((MaxTLODHalvedNight[activeProfile] && NightTime) ? (MaxTLOD[activeProfile] / 2f) : MaxTLOD[activeProfile]);
					}
					else
					{
						num2 = MinTLOD[activeProfile] + ((MaxTLODHalvedNight[activeProfile] ? (MaxTLOD[activeProfile] / 2f) : MaxTLOD[activeProfile]) - MinTLOD[activeProfile]) * (float)Math.Pow((altAboveGnd - AltTLODBase[activeProfile]) / (AltTLODTop[activeProfile] - AltTLODBase[activeProfile]), LOSCorrection[activeProfile] ? 0.5f : 1f);
					}
					num2 += TLODExtraMtnsAmountResidual;
				}
				fpsRawTrend.Reset();
				fixedFPSDetectAttempts = 0;
				fixedFPSDetected = -1f;
				fixedFPSDetectHistoryIndex = 0;
				fixedFPSDetectedAutoTargetFPS = false;
				fixedTargetFPSIncreaseStep = 0L;
				FixedFPSConfigWarning = false;
				recentTLODPeak = MaxTLOD[activeProfile];
				TLODIncreaseCap = 0f;
				CloudIncreaseHysteresis = 10;
				SpikeTLODReductionAmount = 0f;
				SpikeTLODReductionTickMark = 0L;
				SpikeDetected = false;
				SpikeVerificationActive = false;
				fpsAverageReset = true;
				RTSSDataFrozen = false;
				Array.Clear(fixedFPSDetectHistory, 0, fixedFPSDetectHistory.Length);
				FPSSettleActiveLast = false;
				NightCloudReduceLevelsActive = false;
				if (TLODAutoMethod[activeProfile] != 2)
				{
					FPSSettleActive = true;
				}
				if (StartTLODExtraAtMax[activeProfile] && ((TLODAutoMethod[activeProfile] == 3 && !TLODBaseMinus[activeProfile]) || (TLODAutoMethod[activeProfile] < 2 && MinTLODExtra[activeProfile])))
				{
					num2 += MinTLODExtraAmount;
				}
				tlod = MemoryAccess.SetTLOD(num2);
				if (VrModeActive)
				{
					MemoryAccess.SetTLOD_PC(DefaultTLOD);
				}
				else
				{
					MemoryAccess.SetTLOD_VR(DefaultTLOD_VR);
				}
			}
			float value2;
			if (CustomAutoOLOD[activeProfile])
			{
				SetActiveOLODs();
				if (altAboveGnd < AltOLODBase[activeProfile])
				{
					value2 = activeOLODAtBase;
				}
				else
				{
					value2 = ((!(altAboveGnd > AltOLODTop[activeProfile])) ? ((float)Math.Round((activeOLODAtBase + (activeOLODAtTop - activeOLODAtBase) * (altAboveGnd - AltOLODBase[activeProfile]) / (AltOLODTop[activeProfile] - AltOLODBase[activeProfile])) / (float)FPSTolerance[activeProfile]) * (float)FPSTolerance[activeProfile]) : activeOLODAtTop);
				}
			}
			else
			{
				value2 = (VrModeActive ? DefaultOLOD_VR : DefaultOLOD);
			}
			olod = MemoryAccess.SetOLOD(value2);
			if (VrModeActive)
			{
				MemoryAccess.SetOLOD_PC(DefaultOLOD);
			}
			else
			{
				MemoryAccess.SetOLOD_VR(DefaultOLOD_VR);
			}
			ResetInProgress = false;
		}
	}

	public void SetActiveOLODs()
	{
		if (UseExpertOptions)
		{
			activeOLODAtBase = OLODAtBase[activeProfile];
			activeOLODAtTop = OLODAtTop[activeProfile];
		}
		else
		{
			float num = (VrModeActive ? DefaultOLOD_VR : DefaultOLOD);
			activeOLODAtBase = Math.Min(200f, FlightTypeIFR ? num : (num * 1.5f));
			activeOLODAtTop = Math.Max(activeOLODAtBase * 0.1f, 10f);
		}
	}

	public static string SettingQualityText(int SettingQuality)
	{
		return SettingQuality switch
		{
			0 => "Low", 
			1 => "Med", 
			2 => "High", 
			3 => "Ultra", 
			-1 => "Off", 
			_ => "n/a", 
		};
	}

	public void ApplyPerfOpt()
	{
		if (PerfOptPriorityEnabled)
		{
			ApplyPerfOptPriority();
		}
		if (PerfOptAffinityEnabled)
		{
			ApplyPerfOptAffinity();
		}
		if (PerfOptPowerPlanEnabled)
		{
			ApplyPerfOptPowerPlan();
		}
	}

	public void RemovePerfOpt()
	{
		if (PerfOptPriorityEnabled)
		{
			RestorePerfOptPriority();
		}
		if (PerfOptAffinityEnabled)
		{
			RestorePerfOptAffinity();
		}
		if (PerfOptPowerPlanEnabled)
		{
			RestorePerfOptPowerPlan();
		}
	}

	public void ApplyPerfOptPriority()
	{
		try
		{
			Process p = Process.GetProcessesByName(isMSFS2024 ? "FlightSimulator2024" : "FlightSimulator").FirstOrDefault();
			if (p == null)
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:ApplyPerfOptPriority", "MSFS process not found for priority change.");
			}
			else if (p.PriorityClass == ProcessPriorityClass.Normal)
			{
				if (!OriginalPriorityCaptured)
				{
					OriginalPriority = p.PriorityClass;
					OriginalPriorityCaptured = true;
				}
				ProcessPriorityClass targetPriority = ProcessPriorityClass.High;
				switch (MSFSProcessPriority)
				{
				case "Normal":
					targetPriority = ProcessPriorityClass.Normal;
					break;
				case "AboveNormal":
					targetPriority = ProcessPriorityClass.AboveNormal;
					break;
				case "High":
					targetPriority = ProcessPriorityClass.High;
					break;
				}
				Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptPriority", $"Priority decision: MSFS running at Normal priority. Applying {targetPriority} priority.");
				LastAppliedPriorityText = "Priority set to " + MSFSProcessPriority;
				UpdatePriorityTooltip?.Invoke();
				p.PriorityClass = targetPriority;
				Task.Delay(500).ContinueWith((Task _) =>
				{
					try
					{
						p.PriorityClass = targetPriority;
					}
					catch
					{
					}
				});
			}
			else
			{
				Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptPriority", $"Priority decision: MSFS priority already elevated ({p.PriorityClass}). Skipping priority change.");
			}
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "ServiceModel:ApplyPerfOptPriority", "Priority apply failed: " + ex.Message);
		}
	}

	public void ApplyPerfOptAffinity()
	{
		try
		{
			Process p = Process.GetProcessesByName(isMSFS2024 ? "FlightSimulator2024" : "FlightSimulator").FirstOrDefault();
			if (p == null)
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:ApplyPerfOptAffinity", "MSFS process not found for affinity change.");
				return;
			}
			long num = (1L << CPULogicalProcessors) - 1;
			if (p.ProcessorAffinity != num)
			{
				Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptAffinity", "Skipping affinity: MSFS already has a custom affinity mask.");
				return;
			}
			if (!OriginalAffinityCaptured)
			{
				OriginalAffinity = p.ProcessorAffinity;
				OriginalAffinityCaptured = true;
				Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptAffinity", $"Captured original affinity=0x{((IntPtr)OriginalAffinity).ToInt64():X}");
			}
			long mask = 0L;
			foreach (CoreInfo core in Cores)
			{
				if (core.LogicalProcessors.Count == 2)
				{
					mask |= 1L << core.LogicalProcessors[0];
				}
			}
			Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptAffinity", $"Universal physical‑cores‑only mask built. Mask=0x{mask:X}");
			if (AMDUseFirstCCDOnlyValue)
			{
				long num2 = 0L;
				for (int i = 0; i < SmtCores / 2; i++)
				{
					num2 |= 1L << DualLpCores[i].LogicalProcessors[0];
				}
				Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptAffinity", $"AMD first‑CCD filter applied. Using {SmtCores / 2} physical cores. Mask=0x{num2:X}");
				mask = num2;
			}
			LastAppliedAffinityCount = BitOperations.PopCount((ulong)mask);
			LastAppliedAffinityText = $"{LastAppliedAffinityCount} physical cores allocated";
			UpdateAffinityTooltip?.Invoke();
			p.ProcessorAffinity = new IntPtr(mask);
			Task.Delay(500).ContinueWith((Task _) =>
			{
				try
				{
					p.ProcessorAffinity = new IntPtr(mask);
				}
				catch
				{
				}
			});
			Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptAffinity", "Affinity applied to MSFS. " + LastAppliedAffinityText);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "ServiceModel:ApplyPerfOptAffinity", "Affinity apply failed: " + ex.Message);
		}
	}

	public void RestorePerfOptPriority(bool log = true)
	{
		try
		{
			if (!OriginalPriorityCaptured)
			{
				if (log)
				{
					Logger.Log(LogLevel.Information, "ServiceModel:RestorePerfOptPriority", "Priority restore skipped — no priority change was applied.");
				}
				return;
			}
			Process p = Process.GetProcessesByName(isMSFS2024 ? "FlightSimulator2024" : "FlightSimulator").FirstOrDefault();
			if (p == null || p.HasExited)
			{
				if (log)
				{
					Logger.Log(LogLevel.Information, "ServiceModel:RestorePerfOptPriority", "Priority restore skipped — simulator no longer running.");
				}
				return;
			}
			p.PriorityClass = OriginalPriority;
			Task.Delay(500).ContinueWith((Task _) =>
			{
				try
				{
					p.PriorityClass = OriginalPriority;
				}
				catch
				{
				}
			});
			LastAppliedPriorityText = null;
			UpdatePriorityTooltip?.Invoke();
			if (log)
			{
				Logger.Log(LogLevel.Information, "ServiceModel:RestorePerfOptPriority", $"Priority restored to original value: {OriginalPriority}");
			}
		}
		catch (Exception ex)
		{
			if (log)
			{
				Logger.Log(LogLevel.Error, "ServiceModel:RestorePerfOptPriority", "Priority restore failed: " + ex.Message);
			}
		}
	}

	public void RestorePerfOptAffinity(bool log = true)
	{
		try
		{
			if (!OriginalAffinityCaptured)
			{
				if (log)
				{
					Logger.Log(LogLevel.Information, "ServiceModel:RestorePerfOptAffinity", "Affinity restore skipped — no affinity change was applied.");
				}
				return;
			}
			Process p = Process.GetProcessesByName(isMSFS2024 ? "FlightSimulator2024" : "FlightSimulator").FirstOrDefault();
			if (p == null || p.HasExited)
			{
				if (log)
				{
					Logger.Log(LogLevel.Information, "ServiceModel:RestorePerfOptAffinity", "Affinity restore skipped — simulator no longer running.");
				}
				return;
			}
			p.ProcessorAffinity = OriginalAffinity;
			Task.Delay(500).ContinueWith((Task _) =>
			{
				try
				{
					p.ProcessorAffinity = OriginalAffinity;
				}
				catch
				{
				}
			});
			LastAppliedAffinityCount = -1;
			LastAppliedAffinityText = null;
			UpdateAffinityTooltip?.Invoke();
			if (log)
			{
				Logger.Log(LogLevel.Information, "ServiceModel:RestorePerfOptAffinity", $"Affinity restored to original value: 0x{((IntPtr)OriginalAffinity).ToInt64():X}");
			}
		}
		catch (Exception ex)
		{
			if (log)
			{
				Logger.Log(LogLevel.Error, "ServiceModel:RestorePerfOptAffinity", "Affinity restore failed: " + ex.Message);
			}
		}
	}

	private string RunPowerCfg(string args)
	{
		try
		{
			Process process = new Process();
			process.StartInfo.FileName = "powercfg";
			process.StartInfo.Arguments = args;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.Start();
			string result = process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			return result;
		}
		catch
		{
			return null;
		}
	}

	public void ApplyPerfOptPowerPlan()
	{
		try
		{
			string text = null;
			string text2 = null;
			string text3 = null;
			text3 = RunPowerCfg("/getactivescheme");
			if (!string.IsNullOrWhiteSpace(text3))
			{
				text = text3.Split(' ').FirstOrDefault((string x) => Guid.TryParse(x, out var _));
				text2 = text3.Split('(', ')')[1].Trim();
				if (!powerPlanChanged)
				{
					LastAppliedPowerPlanText = $"• Original power plan: {text2}\n• Current power plan: {text2}";
					UpdatePowerPlanTooltip?.Invoke();
				}
			}
			if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text2))
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:ApplyPerfOptPowerPlan", "Unable to resolve the active power plan (GUID and/or name missing).");
				return;
			}
			Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptPowerPlan", $"Current plan: {text2} ({text})");
			string text4 = RunPowerCfg("/list");
			if (string.IsNullOrEmpty(text4))
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:ApplyPerfOptPowerPlan", "Unable to read power plan list.");
				return;
			}
			if (!text.Equals("381b4222-f694-41f0-9685-ff5bb260df2e", StringComparison.OrdinalIgnoreCase) && !text.Equals("a1841308-3541-4fab-bc81-f71556f20b4a", StringComparison.OrdinalIgnoreCase) && !text.Equals("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase))
			{
				Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptPowerPlan", "Not a valid starting power plan. No change applied.");
				return;
			}
			string text5 = null;
			string text6 = null;
			string text7 = null;
			string[] array = text4.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			for (int num = 0; num < array.Length; num++)
			{
				string text8 = array[num].Trim();
				if (text8.Length == 0)
				{
					continue;
				}
				string text9 = text8.Split(' ').FirstOrDefault((string x) => Guid.TryParse(x, out var _));
				if (text9 != null)
				{
					if (text8.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
					{
						text5 = text9;
					}
					if (text9.Equals("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase))
					{
						text6 = text9;
					}
					if (text9.Equals("381b4222-f694-41f0-9685-ff5bb260df2e", StringComparison.OrdinalIgnoreCase))
					{
						text7 = text9;
					}
				}
			}
			string text10 = text5 ?? text6 ?? text7;
			if (string.IsNullOrEmpty(text10))
			{
				Logger.Log(LogLevel.Warning, "ServiceModel:ApplyPerfOptPowerPlan", "No suitable target plan found.");
				return;
			}
			if (text.Equals(text10, StringComparison.OrdinalIgnoreCase))
			{
				Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptPowerPlan", "Already on best available plan.");
				return;
			}
			string value = text2;
			originalPowerPlanGuid = text;
			RunPowerCfg("/setactive " + text10);
			powerPlanChanged = true;
			text3 = RunPowerCfg("/getactivescheme");
			if (!string.IsNullOrWhiteSpace(text3))
			{
				string value2 = text3.Split(' ').FirstOrDefault((string x) => Guid.TryParse(x, out var _));
				string value3 = text3.Split('(', ')')[1].Trim();
				if (!string.IsNullOrEmpty(value2) && !string.IsNullOrEmpty(value3))
				{
					Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptPowerPlan", $"Switched to: {value3} ({value2})");
					LastAppliedPowerPlanText = $"• Original power plan: {value}\n• Current power plan: {value3}";
				}
				else
				{
					Logger.Log(LogLevel.Information, "ServiceModel:ApplyPerfOptPowerPlan", "Switched to: " + text10);
					LastAppliedPowerPlanText = $"• Original power plan: {value}\n• Current power plan: {text10}";
				}
				UpdatePowerPlanTooltip?.Invoke();
			}
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "ServiceModel:ApplyPerfOptPowerPlan", "ApplyPerfOptPowerPlan failed: " + ex.Message);
		}
	}

	public void RestorePerfOptPowerPlan(bool log = true)
	{
		try
		{
			if (!powerPlanChanged || string.IsNullOrEmpty(originalPowerPlanGuid))
			{
				if (log)
				{
					Logger.Log(LogLevel.Information, "ServiceModel:RestorePerfOptPowerPlan", "No power plan change to restore.");
				}
				return;
			}
			RunPowerCfg("/setactive " + originalPowerPlanGuid);
			string text = RunPowerCfg("/getactivescheme");
			if (!string.IsNullOrWhiteSpace(text))
			{
				string value = text.Split(' ').FirstOrDefault((string x) => Guid.TryParse(x, out var _));
				string value2 = text.Split('(', ')')[1].Trim();
				if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(value2))
				{
					if (log)
					{
						Logger.Log(LogLevel.Information, "ServiceModel:RestorePerfOptPowerPlan", $"Restored original power plan: {value2} ({value})");
					}
				}
				else if (log)
				{
					Logger.Log(LogLevel.Information, "ServiceModel:RestorePerfOptPowerPlan", "Restored original power plan: " + originalPowerPlanGuid);
				}
			}
			powerPlanChanged = false;
		}
		catch (Exception ex)
		{
			if (log)
			{
				Logger.Log(LogLevel.Error, "ServiceModel:PowerPlan", "RestorePerfOptPowerPlan failed: " + ex.Message);
			}
		}
	}

	public void LoadCpuTopology()
	{
		Cores.Clear();
		int returnedLength = 0;
		GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, IntPtr.Zero, ref returnedLength);
		if (returnedLength <= 0)
		{
			return;
		}
		nint num = Marshal.AllocHGlobal(returnedLength);
		try
		{
			if (!GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP.RelationProcessorCore, num, ref returnedLength))
			{
				return;
			}
			nint num2 = num;
			nint num3 = IntPtr.Add(num, returnedLength);
			int num4 = 0;
			while (((IntPtr)num2).ToInt64() < ((IntPtr)num3).ToInt64())
			{
				int num5 = Marshal.ReadInt32(num2);
				int offset = Marshal.ReadInt32(IntPtr.Add(num2, 4));
				if (num5 == 0)
				{
					nint num6 = IntPtr.Add(num2, 8);
					Marshal.ReadByte(num6);
					byte efficiencyClass = Marshal.ReadByte(IntPtr.Add(num6, 1));
					ushort num7 = (ushort)Marshal.ReadInt16(IntPtr.Add(num6, 22));
					CoreInfo coreInfo = new CoreInfo
					{
						CoreIndex = num4++,
						EfficiencyClass = efficiencyClass
					};
					nint num8 = IntPtr.Add(num6, 24);
					for (int i = 0; i < num7; i++)
					{
						ulong mask = Marshal.PtrToStructure<GROUP_AFFINITY>(num8).Mask;
						for (int j = 0; j < 64; j++)
						{
							if ((mask & (ulong)(1L << j)) != 0L)
							{
								coreInfo.LogicalProcessors.Add(j);
							}
						}
						num8 = IntPtr.Add(num8, Marshal.SizeOf<GROUP_AFFINITY>());
					}
					Cores.Add(coreInfo);
				}
				num2 = IntPtr.Add(num2, offset);
			}
		}
		finally
		{
			Marshal.FreeHGlobal(num);
		}
		DualLpCores = Cores.Where((CoreInfo c) => c.LogicalProcessors.Count == 2).ToList();
		SingleLpCores = Cores.Where((CoreInfo c) => c.LogicalProcessors.Count == 1).ToList();
		SmtCores = DualLpCores.Count;
	}

	public bool GetRTSSFrameTimes(bool record = false, float seconds = 10f, bool checkOnly = false)
	{
		AppEntry[] appEntries = OSD.GetAppEntries();
		if (checkOnly)
		{
			if (appEntries == null)
			{
				return false;
			}
			return true;
		}
		if (appEntries == null)
		{
			RTSSFrameTimes = Array.Empty<int>();
			return false;
		}
		AppEntry[] array = appEntries;
		foreach (AppEntry appEntry in array)
		{
			if (!appEntry.Name.Contains("FlightSimulator"))
			{
				continue;
			}
			if (appEntry.StatFrameTimeBuf == null)
			{
				RTSSFrameTimes = Array.Empty<int>();
				return false;
			}
			int num = appEntry.StatFrameTimeBuf.Length;
			int num2 = (int)(appEntry.StatFrameTimeBufPos - 1);
			if (num2 < 0)
			{
				num2 += num;
			}
			List<int> list = new List<int>(num);
			float num3 = seconds * 1000f;
			float num4 = 0f;
			for (int j = 0; j < num; j++)
			{
				int num5 = num2 - j;
				if (num5 < 0)
				{
					num5 += num;
				}
				int num6 = (int)appEntry.StatFrameTimeBuf[num5];
				if (num6 != 0)
				{
					float num7 = (float)num6 / 1000f;
					list.Add(num6);
					num4 += num7;
					if (num4 >= num3)
					{
						break;
					}
				}
			}
			RTSSFrameTimes = list.ToArray();
			if (record)
			{
				string value = string.Join(",", RTSSFrameTimes);
				Logger.Log(LogLevel.Debug, "ServiceController:GetRTSSFrameTimes", $"BufPos:{appEntry.StatFrameTimeBufPos} FrameTimesCSV:{value}");
			}
			return true;
		}
		RTSSFrameTimes = Array.Empty<int>();
		return false;
	}

	public double GetRealFrameMedian()
	{
		if (RTSSFrameTimes == null || RTSSFrameTimes.Length == 0)
		{
			return 0.0;
		}
		if (RTSSFrameTimes.Length == 1)
		{
			return RTSSFrameTimes[0];
		}
		int num = RTSSFrameTimes.Length;
		int[] array = (int[])RTSSFrameTimes.Clone();
		Array.Sort(array);
		double[] array2 = new double[num];
		double num2 = 0.0;
		for (int i = 0; i < num; i++)
		{
			array2[i] = Math.Log(Math.Max(1, array[i]));
			num2 += array2[i];
		}
		double num3 = num2 / (double)num;
		double num4 = 0.0;
		for (int j = 0; j < num; j++)
		{
			double num5 = array2[j] - num3;
			num4 += num5 * num5;
		}
		int num6 = Math.Max(1, (int)Math.Ceiling((double)num * 0.4));
		int num7 = Math.Min(num - 1, (int)Math.Floor((double)num * 0.9));
		double num8 = 0.0;
		for (int k = 0; k < num6; k++)
		{
			num8 += array2[k];
		}
		double num9 = -1.0;
		int num10 = num6;
		double num11 = 0.0;
		double num12 = 0.0;
		for (int l = num6; l <= num7; l++)
		{
			if (l > num6)
			{
				num8 += array2[l - 1];
			}
			double num13 = l;
			double num14 = num - l;
			double num15 = num2 - num8;
			double num16 = num8 / num13;
			double num17 = num15 / num14;
			double num18 = num16 - num17;
			double num19 = num13 * num14 / (double)num * num18 * num18;
			if (num19 > num9)
			{
				num9 = num19;
				num10 = l;
				num11 = num16;
				num12 = num17;
			}
		}
		double num20 = ((num4 > 0.0) ? (num9 / num4) : 0.0);
		double num21 = Math.Exp(num12 - num11);
		int num22 = num - num10;
		double num23 = (double)num22 / (double)num;
		bool flag = num < 4 || num22 >= 2;
		int num24 = (((num20 > 0.6 && num21 >= 1.75 && num23 >= 0.1 && num23 <= 0.6) & flag) ? num10 : 0);
		int num25 = num - num24;
		int num26 = num24 + num25 / 2;
		if (num25 % 2 != 0)
		{
			return array[num26];
		}
		return (double)(array[num26 - 1] + array[num26]) / 2.0;
	}

	public void DetectPeriodicSpikes()
	{
		SpikeDetected = false;
		if (RTSSFrameTimes == null)
		{
			return;
		}
		if (RTSSDataFrozen)
		{
			if (LogPlus && SpikeDetectProtStatus != "RTSS buffer stale")
			{
				Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", "RTSS buffer stale → periodic detection skipped");
			}
			SpikeDetectProtStatus = "RTSS buffer stale";
			return;
		}
		List<(int, long)> list = new List<(int, long)>();
		long num = 0L;
		for (int i = 0; i < RTSSFrameTimes.Length; i++)
		{
			int num2 = RTSSFrameTimes[i];
			num += num2;
			list.Add((num2, num));
		}
		if (num < 5000000 && RTSSFrameTimes.Length < 922)
		{
			if (LogPlus && SpikeDetectProtStatus != "Data set < 5s")
			{
				Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", $"Elapsed time too short ({(float)num / 1000000f:F2}s) → periodic detection skipped");
			}
			SpikeDetectProtStatus = "Data set < 5s";
			return;
		}
		double realFrameMedian = GetRealFrameMedian();
		double num3 = RTSSFrameTimes.Average();
		int value = RTSSFrameTimes.Min();
		int value2 = RTSSFrameTimes.Max();
		int value3 = RTSSFrameTimes.Length;
		double val = 0.0;
		double num4 = Math.Max(realFrameMedian * SpikeThresholdMultiplier, val);
		long num5 = 300000L;
		long num6 = 1800000L;
		List<(int, int, long)> list2 = new List<(int, int, long)>();
		int num7 = -1;
		int num8 = -1;
		long item = 0L;
		int num9 = -1;
		bool flag = false;
		long num10 = -1L;
		for (int j = 0; j < list.Count; j++)
		{
			if (!((double)list[j].Item1 > num4))
			{
				continue;
			}
			if (num7 == -1 && list[j].Item2 > num6)
			{
				num10 = list[j].Item2;
				flag = true;
			}
			if (num9 == -1 || j != num9 + 1)
			{
				if (num7 != -1)
				{
					list2.Add((num7, num8, item));
				}
				num7 = j;
				num8 = list[j].Item1;
				item = list[j].Item2;
			}
			else if (list[j].Item1 > num8)
			{
				num7 = j;
				num8 = list[j].Item1;
				item = list[j].Item2;
			}
			num9 = j;
		}
		if (num7 != -1)
		{
			list2.Add((num7, num8, item));
		}
		if (list2.Count < 4)
		{
			SpikeDetectProtStatus = $"Spikes but < 4 ({list2.Count})";
			return;
		}
		if (flag)
		{
			SpikeDetectProtStatus = "First spike stale";
			return;
		}
		if (LogPlus && (list2.Count > 0 || ExperimentalFeatures))
		{
			Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", $"PeriodicDetect medianUs={realFrameMedian} averageUs={(int)num3} minUs={value} maxUs={value2} sampleCount={value3} multiplier={SpikeThresholdMultiplier:F1} thresholdUs={num4} spikeCount={list2.Count}");
		}
		List<int> list3 = list2.OrderByDescending(((int idx, int rawUs, long elapsedUs) s) => s.rawUs).Take(10).OrderBy(((int idx, int rawUs, long elapsedUs) s) => s.idx)
			.Select(((int idx, int rawUs, long elapsedUs) s) => s.idx)
			.ToList();
		List<long> list4 = new List<long>();
		for (int num11 = 1; num11 < list3.Count; num11++)
		{
			long item2 = list[list3[num11 - 1]].Item2;
			long item3 = list[list3[num11]].Item2 - item2;
			list4.Add(item3);
		}
		List<IntervalInfo> list5 = new List<IntervalInfo>();
		for (int num12 = 0; num12 < list3.Count; num12++)
		{
			for (int num13 = num12 + 1; num13 < list3.Count; num13++)
			{
				int num14 = num13 - num12;
				if (num14 <= 2)
				{
					long item4 = list[list3[num12]].Item2;
					long intervalUs = list[list3[num13]].Item2 - item4;
					list5.Add(new IntervalInfo
					{
						intervalUs = intervalUs,
						spikeIndexA = list3[num12],
						spikeIndexB = list3[num13]
					});
				}
			}
		}
		List<IntervalInfo> list6 = list5.OrderBy((IntervalInfo x) => x.intervalUs).ToList();
		if (list6.Count == 0)
		{
			if (LogPlus)
			{
				Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", "NoIntervals: no spikes → periodic detection skipped");
			}
			SpikeDetectProtStatus = "Spikes not detected";
			return;
		}
		if (LogPlus)
		{
			Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", $"SortedIntervals count={list6.Count} min={list6.First().intervalUs} max={list6.Last().intervalUs}");
		}
		List<List<IntervalInfo>> list7 = new List<List<IntervalInfo>>();
		long num15 = 50000L;
		for (int num16 = 0; num16 < list6.Count; num16++)
		{
			IntervalInfo item5 = list6[num16];
			List<IntervalInfo> list8 = new List<IntervalInfo> { item5 };
			for (int num17 = num16 + 1; num17 < list6.Count; num17++)
			{
				IntervalInfo item6 = list6[num17];
				long num18 = Math.Abs(item6.intervalUs - item5.intervalUs);
				if (num18 > num15)
				{
					break;
				}
				list8.Add(item6);
			}
			list7.Add(list8);
		}
		List<IntervalInfo> list9 = list7.OrderByDescending((List<IntervalInfo> c) => c.Count).First();
		long intervalUs2 = list9.OrderBy((IntervalInfo x) => x.intervalUs).ElementAt(list9.Count / 2).intervalUs;
		if (intervalUs2 < num5 || intervalUs2 > num6)
		{
			if (LogPlus)
			{
				Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", $"Reject: periodicityUs={intervalUs2} outside allowable range [{num5}..{num6}]");
			}
			if (intervalUs2 < num5)
			{
				SpikeDetectProtStatus = "Spike interval < 0.3s";
			}
			else
			{
				SpikeDetectProtStatus = "Spike interval > 1.8s";
			}
			return;
		}
		if (LogPlus)
		{
			Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", $"LargestCluster size={list9.Count} first={list9.First().intervalUs} last={list9.Last().intervalUs} periodicityUs={intervalUs2}");
		}
		if (list9.Count < 3)
		{
			if (LogPlus)
			{
				Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", $"Reject: largestCluster.Count={list9.Count} < 3 (need ≥3 intervals)");
			}
			SpikeDetectProtStatus = "Spikes but < 3 periodic";
			return;
		}
		List<int> list10 = new List<int>();
		foreach (IntervalInfo item8 in list9)
		{
			list10.Add(item8.spikeIndexA);
			list10.Add(item8.spikeIndexB);
		}
		int num19 = list10.Min();
		long item7 = list[num19].Item2;
		long intervalUs3 = list9.Last().intervalUs;
		if (LogPlus)
		{
			Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", $"PeriodicCheck earliestIdx={num19} earliestElapsedUs={item7} clusterPeriodUs={intervalUs2} clusterMaxIntervalUs={intervalUs3}");
		}
		if (item7 > intervalUs3)
		{
			if (LogPlus)
			{
				Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", $"Reject: earliestPeriodicSpikeElapsedUs={item7} > largestClusterMaxIntervalUs={intervalUs3}");
			}
			SpikeDetectProtStatus = "Periodic spikes but stale";
			return;
		}
		string text = string.Join(",", RTSSFrameTimes);
		if (LogPlus)
		{
			Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", "FrameTimesCSV: " + text);
		}
		SpikeDetected = true;
		SpikeDetectProtStatus = $"{list9.Count} periodic spikes, {(float)intervalUs2 / 1000000f:F1}s apart";
		Logger.Log(LogLevel.Debug, "ServiceController:DetectPeriodicSpikes", SpikeDetectProtStatus);
	}
}

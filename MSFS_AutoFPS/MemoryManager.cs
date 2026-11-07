using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MSFS_AutoFPS.Utilities;

namespace MSFS_AutoFPS;

public class MemoryManager
{
	public class BoundaryFieldResult
	{
		public string Name { get; set; }

		public long Address { get; set; }

		public long Offset { get; set; }

		public bool Passed { get; set; }

		public bool IsFloat { get; set; }

		public int Expected { get; set; }

		public int Actual { get; set; }

		public float ExpectedFloat { get; set; }

		public float ActualFloat { get; set; }
	}

	public class BoundaryTest4Result
	{
		public float Confidence;

		public List<BoundaryFieldResult> Fields;
	}

	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	public class SettingsMaskBuilder
	{
		private readonly HashSet<int> activeBytes = new HashSet<int>();

		private readonly int dumpSize;

		private readonly int dumpBaseOffset;

		public SettingsMaskBuilder(int dumpSize, int dumpBaseOffset = 768)
		{
			this.dumpSize = dumpSize;
			this.dumpBaseOffset = dumpBaseOffset;
		}

		public void AddScanOutput(string scanOutput)
		{
			if (string.IsNullOrWhiteSpace(scanOutput))
			{
				return;
			}
			string[] array = scanOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (text.Length == 0)
				{
					continue;
				}
				if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
				{
					text = text.Substring(2);
				}
				if (int.TryParse(text, NumberStyles.HexNumber, null, out var result))
				{
					int num = result - dumpBaseOffset;
					if (num >= 0 && num < dumpSize)
					{
						activeBytes.Add(num);
					}
				}
			}
		}

		public string FinalizeAndSaveMask(string dumpsDir, string DetectedSimVersion)
		{
			if (!Directory.Exists(dumpsDir))
			{
				Directory.CreateDirectory(dumpsDir);
			}
			int num = dumpSize;
			byte[] array = new byte[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = (byte)(activeBytes.Contains(i) ? byte.MaxValue : 0);
			}
			int value = int.Parse(DetectedSimVersion.Split('.')[1]) - 2;
			string path = $"SettingsMask_SU{value}.bin";
			string text = Path.Combine(dumpsDir, path);
			File.WriteAllBytes(text, array);
			return text;
		}
	}

	private ServiceModel Model;

	private long OffsetPointerMain;

	private long OffsetPointerTlod;

	private long OffsetPointerTlodVr;

	private long OffsetPointerOlod;

	private long OffsetPointerOlodVr;

	private long OffsetPointerCloudQ;

	private long OffsetPointerCloudQVr;

	private long OffsetPointerVrMode;

	private long OffsetPointerAnsio;

	private long OffsetPointerDynSet;

	private long OffsetPointerDynSetVr;

	private long OffsetPointerDynSetFrameRateTarget;

	private long OffsetPointerDynSetFrameRateTargetVr;

	private long OffsetPointerCubeMap;

	private long OffsetPointerWaterWaves;

	private long OffsetPointerOSTPreCaching;

	private long OffsetPointerTerrainShadows;

	private long OffsetPointerRTShadows;

	private long OffsetPointerTShadowsEnabled;

	private long OffsetPointerDispMap;

	private long OffsetPointerBuildings;

	private long OffsetPointerTrees;

	private long OffsetPointerPlants;

	private long OffsetPointerGrass;

	private long OffsetPointerPkdAcQ;

	private long OffsetPointerAcTrafQ;

	private long OffsetVR;

	private bool is2024ReleaseSU;

	private long OffsetShift;

	private bool OffsetShiftChanged;

	private long OffsetShiftPointer;

	private bool OffsetShiftPointerChanged;

	private long OffsetPointerFoveatedRenderingScale;

	private long OffsetPointerSharpAmt;

	private long OffsetPointerSharpAmtVR;

	private long OffsetPointerExposureBias;

	private long OffsetPointerExposureBiasVR;

	private long addrTLOD;

	private long addrOLOD;

	private long addrTLOD_VR;

	private long addrOLOD_VR;

	private long addrCloudQ;

	private long addrCloudQ_VR;

	private long addrVrMode;

	private long addrAnsio;

	private long addrDynSet;

	private long addrDynSetVR;

	private long addrDynSetFrameRateTarget;

	private long addrDynSetFrameRateTargetVR;

	private long addrCubeMap;

	private long addrWaterWaves;

	private long addrOSTPreCaching;

	private long addrTerrainShadows;

	private long addrRTShadows;

	private long addrTShadowsEnabled;

	private long addrDispMap;

	private long addrBuildings;

	private long addrTrees;

	private long addrPlants;

	private long addrGrass;

	private long addrPkdAcQ;

	private long addrAcTrafQ;

	private long addrFoveatedRenderingScale;

	private long addrSharpAmt;

	private long addrSharpAmtVR;

	private long addrExposureBias;

	private long addrExposureBiasVR;

	private bool allowMemoryWrites;

	private bool isDX12;

	private float configTLODPC;

	private float configTLODVR;

	private float configOLODPC;

	private float configOLODVR;

	private int configAniso;

	private int configCubeMap;

	private int configWaterWaves;

	private string DefaultTLODStr;

	private string DefaultTLOD_VRStr;

	private string DefaultOLODStr;

	private string DefaultOLOD_VRStr;

	private string DefaultCloudQStr;

	private string DefaultCloudQ_VRStr;

	private string DefaultOSTPCacheStr;

	private string DefaultTShadowsEnabledStr;

	private string DefaultTShadowsStr;

	private string DefaultRTShadowsStr;

	private string DefaultDispMapStr;

	private string DefaultBldingsStr;

	private string DefaultTreesStr;

	private string DefaultPlantsStr;

	private string DefaultGrassStr;

	private string DefaultPkdAcQStr;

	private string DefaultAcTrafQStr;

	private string DefaultOSTPCache_VRStr;

	private string DefaultTShadowsEnabled_VRStr;

	private string DefaultTShadows_VRStr;

	private string DefaultRTShadows_VRStr;

	private string DefaultDispMap_VRStr;

	private string DefaultBldings_VRStr;

	private string DefaultTrees_VRStr;

	private string DefaultPlants_VRStr;

	private string DefaultGrass_VRStr;

	private string DefaultPkdAcQ_VRStr;

	private string DefaultAcTrafQ_VRStr;

	private ConfigParser OptSetParser = new ConfigParser();

	private string OptSetPath;

	private string OptSetPathApp;

	private DateTime lastOptSetModify;

	private bool isfirstSessionRan = true;

	private byte[] memoryMap = new byte[1503];

	public MemoryManager(ServiceModel model)
	{
		Model = model;
		Model.ReductionSettingsDepthAchieved = 0;
		Model.ReductionSettingsSuiteAchieved = 0;
		Model.ReductionSettingsDepthAchieved_VR = 0;
		Model.ReductionSettingsSuiteAchieved_VR = 0;
		OptSetPath = (Model.isMSFS2024 ? Model.DetectedMSFSConfig2024 : Model.DetectedMSFSConfig2020);
		OptSetPathApp = OptSetPath + ".AutoFPS";
		try
		{
			if (!Model.MemoryCompatTestComplete)
			{
				int num = 0;
				bool flag = false;
				string MSFSVersion = null;
				Thread.Sleep(2000);
				do
				{
					if (num > 0)
					{
						Thread.Sleep(6000);
					}
					if (flag = MemoryInterface.Attach("FlightSimulator" + (Model.isMSFS2024 ? "2024" : ""), out MSFSVersion))
					{
						Model.moduleBaseAddress = MemoryInterface.GetModuleAddress(Model.isMSFS2024 ? "FlightSimulator2024.exe" : Model.SimModule);
					}
				}
				while (++num < 5 && (!flag || Model.moduleBaseAddress <= 0 || MSFSVersion == null));
				if (MSFSVersion == null)
				{
					Logger.Log(LogLevel.Information, "MemoryManager:MemoryManager", "Attempting Attach2 fallback to extract MSFS version ...");
					if (flag = MemoryInterface.Attach2("FlightSimulator" + (Model.isMSFS2024 ? "2024" : ""), out MSFSVersion))
					{
						Model.moduleBaseAddress = MemoryInterface.GetModuleAddress(Model.isMSFS2024 ? "FlightSimulator2024.exe" : Model.SimModule);
					}
					Logger.Log(LogLevel.Information, "MemoryManager:MemoryManager", $"Attach2 result={flag}; version='{MSFSVersion}'; moduleBaseAddress={Model.moduleBaseAddress}");
					if (flag && !string.IsNullOrEmpty(MSFSVersion))
					{
						Logger.Log(LogLevel.Information, "MemoryManager:MemoryManager", "Attach2 succeeded, MSFS version " + MSFSVersion + " obtained.");
					}
					else
					{
						Logger.Log(LogLevel.Warning, "MemoryManager:MemoryManager", "Attach2 did not obtain a valid MSFS version.");
					}
				}
				if (MSFSVersion == null)
				{
					Logger.Log(LogLevel.Information, "MemoryManager:MemoryManager", "Attempting Attach3 diagnostic attempt to extract MSFS version ...");
					if (flag = MemoryInterface.Attach3("FlightSimulator" + (Model.isMSFS2024 ? "2024" : ""), out MSFSVersion))
					{
						Model.moduleBaseAddress = MemoryInterface.GetModuleAddress(Model.isMSFS2024 ? "FlightSimulator2024.exe" : Model.SimModule);
					}
					Logger.Log(LogLevel.Information, "MemoryManager:MemoryManager", $"Attach3 result={flag}; version='{MSFSVersion}'; moduleBaseAddress={Model.moduleBaseAddress}");
					if (flag && !string.IsNullOrEmpty(MSFSVersion))
					{
						Logger.Log(LogLevel.Information, "MemoryManager:MemoryManager", "Attach3 succeeded, MSFS version " + MSFSVersion + " obtained.");
					}
					else
					{
						Logger.Log(LogLevel.Warning, "MemoryManager:MemoryManager", "Attach3 did not obtain a valid MSFS version.");
					}
				}
				if (!flag || Model.moduleBaseAddress <= 0 || MSFSVersion == null)
				{
					if (!flag)
					{
						Logger.Log(LogLevel.Error, "MemoryManager:MemoryManager", $"Unable to find and attach to MSFS process after {num} attempts - memory writes disabled.");
					}
					else if (Model.moduleBaseAddress <= 0)
					{
						Logger.Log(LogLevel.Error, "MemoryManager:MemoryManager", $"Unable to determine module base address after {num} attempts - memory writes disabled.");
					}
					else if (MSFSVersion == null)
					{
						Logger.Log(LogLevel.Error, "MemoryManager:MemoryManager", $"Unable to determine MSFS version number after {num} attempts - memory writes disabled.");
					}
					Model.UnableToAttachMSFS = true;
					Model.AppEnabled = false;
					Model.MemoryCompatTestComplete = true;
					return;
				}
				Logger.Log(LogLevel.Information, "MemoryManager:MemoryManager", $"MSFS successfully attached after {num} attempt" + ((num > 1) ? "s" : ""));
				Model.DetectedSimVersion = MSFSVersion;
				GetOptSettings();
				if (Model.PerfOptEnabled)
				{
					Model.ApplyPerfOpt();
				}
			}
			string text = Model.DetectedSimVersion.Substring(2, 1);
			is2024ReleaseSU = Model.isMSFS2024 && text == "9";
			if (model.isMSFS2024)
			{
				if (int.Parse(Model.ConfigurationFile.GetSetting("shiftPointerSetVersion", "1")) != 1)
				{
					Model.ConfigurationFile.RemoveSetting("offsetShift");
					Model.ConfigurationFile.RemoveSetting("offsetShiftPointer");
					Model.ConfigurationFile.SetSetting("shiftPointerSetVersion", 1.ToString());
				}
				OffsetShift = (is2024ReleaseSU ? 0 : (Model.ConfigurationFile.SettingExists("offsetShift") ? ServiceModel.ParseLongSetting(Model.ConfigurationFile, "offsetShift", "0x0", NumberStyles.HexNumber) : 0));
				OffsetShiftPointer = (is2024ReleaseSU ? 4095 : ServiceModel.ParseLongSetting(Model.ConfigurationFile, "offsetShiftPointer", "0x0FFF", NumberStyles.HexNumber));
				Calc2024PointerOffsets();
			}
			else
			{
				OffsetPointerTlod = 12L;
				OffsetPointerTlodVr = 276L;
				OffsetPointerOlod = 12L;
				OffsetPointerCloudQ = 68L;
				OffsetPointerCloudQVr = 264L;
				OffsetPointerVrMode = 28L;
				OffsetPointerAnsio = -24L;
				OffsetPointerWaterWaves = 60L;
				OffsetPointerMain = 976L;
			}
			GetActiveDXVersion();
			if (!Model.MemoryCompatTestComplete)
			{
				if (IsCompatTestRequired())
				{
					Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "New MSFS/app/offset combo detected");
					model.SimVersionChanged = HasSimVersionChanged();
					if (Model.isMSFS2024 && Model.ConfigurationFile.SettingExists("offsetModuleBase " + Model.DetectedSimVersion))
					{
						model.OffsetModuleBase = Convert.ToInt64(Model.ConfigurationFile.GetSetting("offsetModuleBase " + Model.DetectedSimVersion), 16);
						Model.SetSetting("offsetModuleBase", "0x" + model.OffsetModuleBase.ToString("X8"));
						Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Trying previously saved offsetModuleBase for " + model.DetectedSimVersion + ": 0x" + model.OffsetModuleBase.ToString("X8"));
					}
					else
					{
						Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Trying offsetModuleBase: 0x" + model.OffsetModuleBase.ToString("X8"));
					}
					if (Model.isMSFS2024)
					{
						GetAddresses2024(Model.OffsetModuleBase);
					}
					else
					{
						GetAddresses2020();
					}
					if (Model.AppEnabled)
					{
						Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Running primary compatibility test for new MSFS/app/offset combo");
						BoundaryTest(logResult: true);
						if (!allowMemoryWrites)
						{
							Logger.Log(LogLevel.Error, "MemoryManager:MemoryManager", "Primary compatibility test failed - possible MSFS memory map change");
							if (model.isMSFS2024)
							{
								OffsetSearch2024();
							}
							else
							{
								OffsetSearch();
							}
							if (Model.isMSFS2024 && !allowMemoryWrites && OffsetShiftChanged)
							{
								Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Re-running primary compatibility test after resolving VR offset change");
								Calc2024PointerOffsets();
								GetAddresses2024(Model.OffsetModuleBase);
								BoundaryTest(logResult: true);
								if (allowMemoryWrites)
								{
									Model.ConfigurationFile.SetSetting("offsetShift", $"0x{OffsetShift:X}");
									Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Primary compatibility test passed after resolving VR offset change - memory writes enabled");
									Model.SetSetting("offsetModuleBase", "0x" + Model.OffsetModuleBase.ToString("X8"));
									Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "New offsetModuleBase found and saved: 0x" + Model.OffsetModuleBase.ToString("X8"));
								}
								else
								{
									Logger.Log(LogLevel.Error, "MemoryManager:MemoryManager", "Primary compatibility test still failed after resolving VR offset change");
								}
							}
						}
						else
						{
							Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Primary compatibility test passed - memory writes enabled");
						}
						if (!allowMemoryWrites)
						{
							Logger.Log(LogLevel.Error, "MemoryManager:MemoryManager", "Module offset search failed to find a valid offset - memory writes disabled");
							if (Model.isMSFS2024)
							{
								GetAddresses2024(Model.OffsetModuleBase);
							}
							else
							{
								GetAddresses2020();
							}
							BoundaryTest(logResult: true);
							if (!Model.DevTesting)
							{
								Model.AppEnabled = false;
							}
						}
						else
						{
							if (model.isMSFS2024)
							{
								GetAddresses2024(Model.OffsetModuleBase);
							}
							else
							{
								GetAddresses2020();
							}
							Model.ConfigurationFile.GetSetting("LastCompatTestPassConfig");
							Model.ConfigurationFile.SetSetting("LastCompatTestPassConfig", "0.5.5.0 " + Model.DetectedSimVersion);
							Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Primary compatibility test pass for MSFS/app/offset combo saved to config file");
							if (model.isMSFS2024)
							{
								Model.ConfigurationFile.SetSetting("offsetModuleBase " + Model.DetectedSimVersion, "0x" + Model.OffsetModuleBase.ToString("X8"));
								Model.ConfigurationFile.CleanOffsetModuleBaseKeys();
							}
						}
					}
				}
				else
				{
					Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Primary compatibility test bypassed due to previous saved pass found for current MSFS/app/offset combo.");
					Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Using offsetModuleBase: 0x" + Model.OffsetModuleBase.ToString("X8"));
					allowMemoryWrites = true;
				}
				if (allowMemoryWrites)
				{
					if (model.isMSFS2024)
					{
						if (IsCompatTestRequired("LastCompatTest2PassConfig"))
						{
							Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Running secondary compatibility test for new MSFS/app/offset combo");
							GetAddresses2024(Model.OffsetModuleBase);
							BoundaryTestSecondary(logResult: true);
							if (model.SettingsReduceEnabled)
							{
								Model.ConfigurationFile.GetSetting("LastCompatTest2PassConfig");
								Model.ConfigurationFile.SetSetting("LastCompatTest2PassConfig", "0.5.5.0 " + Model.DetectedSimVersion);
								Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Secondary compatibility test pass for MSFS/app/offset combo saved to config file");
							}
							else if (Model.isMSFS2024)
							{
								Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Secondary compatibility test failed - attempting extended offset shift analysis to rectify");
								RunExtendedOffsetShiftAnalysis();
								if (OffsetShiftPointerChanged)
								{
									Calc2024PointerOffsets();
									GetAddresses2024(Model.OffsetModuleBase);
									BoundaryTestSecondary(logResult: true);
									if (model.SettingsReduceEnabled)
									{
										Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", $"Extended offset shift analysis pointer shift succeeded - saving new offset shift pointer 0x{OffsetShiftPointer:X}");
										Model.ConfigurationFile.SetSetting("offsetShiftPointer", $"0x{OffsetShiftPointer:X}");
										Model.ConfigurationFile.GetSetting("LastCompatTest2PassConfig");
										Model.ConfigurationFile.SetSetting("LastCompatTest2PassConfig", "0.5.5.0 " + Model.DetectedSimVersion);
										Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Secondary compatibility test pass for MSFS/app/offset combo saved to config file");
									}
									else
									{
										Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Extended offset shift analysis pointer shift failed - reduction setting memory writes remain disabled");
									}
								}
								else
								{
									Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Extended offset shift analysis did not detect a pointer shift - reduction setting memory writes remain disabled");
								}
							}
							else
							{
								Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Secondary compatibility test failed - reduction setting memory writes disabled!");
							}
						}
						else
						{
							Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", "Secondary compatibility test bypassed due to previous saved pass found for current MSFS/app/offset combo");
							model.SettingsReduceEnabled = true;
						}
					}
					else
					{
						model.SettingsReduceEnabled = true;
					}
				}
				if (model.isMSFS2024)
				{
					GetAddresses2024(Model.OffsetModuleBase);
				}
				else
				{
					GetAddresses2020();
				}
				if (!TLODReadWritability())
				{
					Model.AppEnabled = false;
				}
				Model.MemoryCompatTestComplete = true;
			}
			else
			{
				GetOptSettings();
				if (model.isMSFS2024)
				{
					GetAddresses2024(Model.OffsetModuleBase);
				}
				else
				{
					GetAddresses2020();
				}
				if (Model.LogPlus)
				{
					VerifyAddresses();
				}
				allowMemoryWrites = true;
			}
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:MemoryManager", $"Exception {ex}: {ex.Message}");
		}
	}

	public List<(long offset, long size)> ComputeStructuralShifts(byte[] suRelease, byte[] suBeta, byte[] suReleaseMask)
	{
		List<(long, long)> list = new List<(long, long)>();
		int num = Math.Min((int)(OffsetPointerAcTrafQ + OffsetVR - 768), Math.Min(suBeta.Length - 4, suRelease.Length - 4));
		int num2 = 0;
		for (int i = 0; i <= num; i++)
		{
			int num3 = i + num2;
			if (num3 >= suBeta.Length)
			{
				return list;
			}
			if (suReleaseMask[i] != byte.MaxValue || suRelease[i] == suBeta[num3])
			{
				continue;
			}
			bool flag = false;
			for (int j = 1; j <= 16; j++)
			{
				if (flag)
				{
					break;
				}
				int num4 = num3 + j;
				if (num4 + 3 >= suBeta.Length)
				{
					break;
				}
				bool flag2 = true;
				for (int k = 0; k < 4; k++)
				{
					int num5 = i + k;
					int num6 = num4 + k;
					if (num5 >= suRelease.Length || num6 >= suBeta.Length)
					{
						flag2 = false;
						break;
					}
					if (suReleaseMask[num5] == byte.MaxValue && suRelease[num5] != suBeta[num6])
					{
						flag2 = false;
						break;
					}
				}
				if (flag2)
				{
					flag = true;
					list.Add((i, j));
					num2 += j;
				}
			}
			if (!flag)
			{
				break;
			}
		}
		return list;
	}

	private long ApplyPointerShift(long baseOffset)
	{
		return baseOffset + ((baseOffset >= OffsetShiftPointer) ? OffsetShift : 0);
	}

	private void Calc2024PointerOffsets()
	{
		OffsetVR = 328 + OffsetShift;
		OffsetPointerFoveatedRenderingScale = (is2024ReleaseSU ? 768 : ApplyPointerShift(768L));
		OffsetPointerSharpAmt = (is2024ReleaseSU ? 796 : ApplyPointerShift(796L));
		OffsetPointerSharpAmtVR = (is2024ReleaseSU ? 800 : ApplyPointerShift(800L));
		OffsetPointerExposureBias = (is2024ReleaseSU ? 804 : ApplyPointerShift(804L));
		OffsetPointerExposureBiasVR = (is2024ReleaseSU ? 808 : ApplyPointerShift(808L));
		OffsetPointerDynSet = (is2024ReleaseSU ? 812 : ApplyPointerShift(812L));
		OffsetPointerDynSetVr = (is2024ReleaseSU ? 813 : ApplyPointerShift(813L));
		OffsetPointerDynSetFrameRateTarget = (is2024ReleaseSU ? 816 : ApplyPointerShift(816L));
		OffsetPointerDynSetFrameRateTargetVr = (is2024ReleaseSU ? 820 : ApplyPointerShift(820L));
		OffsetPointerVrMode = (is2024ReleaseSU ? 824 : ApplyPointerShift(824L));
		OffsetPointerAnsio = (is2024ReleaseSU ? 828 : ApplyPointerShift(828L));
		OffsetPointerTlod = (is2024ReleaseSU ? 856 : ApplyPointerShift(856L));
		OffsetPointerOSTPreCaching = (is2024ReleaseSU ? 872 : ApplyPointerShift(872L));
		OffsetPointerOlod = (is2024ReleaseSU ? 876 : ApplyPointerShift(876L));
		OffsetPointerTShadowsEnabled = (is2024ReleaseSU ? 908 : ApplyPointerShift(908L));
		OffsetPointerTerrainShadows = (is2024ReleaseSU ? 912 : ApplyPointerShift(912L));
		OffsetPointerWaterWaves = (is2024ReleaseSU ? 924 : ApplyPointerShift(924L));
		OffsetPointerCloudQ = (is2024ReleaseSU ? 932 : ApplyPointerShift(932L));
		OffsetPointerRTShadows = (is2024ReleaseSU ? 952 : ApplyPointerShift(952L));
		OffsetPointerCubeMap = (is2024ReleaseSU ? 964 : ApplyPointerShift(964L));
		OffsetPointerDispMap = (is2024ReleaseSU ? 997 : ApplyPointerShift(997L));
		if (false)
		{
			OffsetPointerBuildings = (is2024ReleaseSU ? 1044 : ApplyPointerShift(1044L));
			OffsetPointerTrees = (is2024ReleaseSU ? 1048 : ApplyPointerShift(1048L));
			OffsetPointerPlants = (is2024ReleaseSU ? 1052 : ApplyPointerShift(1052L));
			OffsetPointerGrass = (is2024ReleaseSU ? 1060 : ApplyPointerShift(1060L));
			OffsetPointerPkdAcQ = (is2024ReleaseSU ? 1076 : ApplyPointerShift(1076L));
			OffsetPointerAcTrafQ = (is2024ReleaseSU ? 1092 : ApplyPointerShift(1092L));
		}
		else
		{
			OffsetPointerBuildings = (is2024ReleaseSU ? 1044 : ApplyPointerShift(1044L));
			OffsetPointerTrees = (is2024ReleaseSU ? 1048 : ApplyPointerShift(1048L));
			OffsetPointerPlants = (is2024ReleaseSU ? 1052 : ApplyPointerShift(1052L));
			OffsetPointerGrass = (is2024ReleaseSU ? 1060 : ApplyPointerShift(1060L));
			OffsetPointerPkdAcQ = (is2024ReleaseSU ? 1076 : ApplyPointerShift(1076L));
			OffsetPointerAcTrafQ = (is2024ReleaseSU ? 1092 : ApplyPointerShift(1092L));
		}
	}

	private bool IsCompatTestRequired(string settingsKey = "LastCompatTestPassConfig")
	{
		if (!Model.ConfigurationFile.SettingExists(settingsKey))
		{
			return true;
		}
		string setting = Model.ConfigurationFile.GetSetting(settingsKey);
		if (setting == "0.5.5.0 " + Model.DetectedSimVersion)
		{
			return false;
		}
		return true;
	}

	private bool HasSimVersionChanged(string settingsKey = "LastCompatTestPassConfig")
	{
		if (Model.ConfigurationFile.SettingExists(settingsKey))
		{
			return Model.ConfigurationFile.GetSetting(settingsKey).Split(' ')[1] != Model.DetectedSimVersion;
		}
		return false;
	}

	private void GetOptSettings()
	{
		List<string> missingKeys = new List<string>();
		if (!File.Exists(OptSetPath) || IsMissing("LoDFactor", 1) || IsMissing("LoDFactor", 2) || IsMissing("LoDFactor", 3) || IsMissing("LoDFactor", 4) || IsMissing("MaxAnisotropy", 1) || IsMissing("Size", 1, "ReflectionProbe") || IsMissing("FFTSize", 1))
		{
			if (!File.Exists(OptSetPath))
			{
				Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", "Unable to locate " + OptSetPath);
			}
			else if (missingKeys.Count > 0)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", "Unable to find required settings in UserOpt.cfg: " + string.Join(", ", missingKeys));
			}
			else
			{
				Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", "Unable to find required settings in UserOpt.cfg.");
			}
			Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", "Unable to run memory compatibility test - memory writes disabled");
			Model.AppEnabled = false;
			return;
		}
		if (Model.MemoryCompatTestComplete && !Model.DevTesting && !Model.MonitorMode)
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:GetOptSettings", "Backing up pre flight session state UserCfg.opt file");
			try
			{
				if ((File.GetAttributes(OptSetPath) & FileAttributes.ReadOnly) != 0)
				{
					Logger.Log(LogLevel.Warning, "MemoryManager:GetOptSettings", "UserCfg.opt is read-only; skipping backup creation.");
				}
				else
				{
					File.Copy(OptSetPath, OptSetPathApp, overwrite: true);
					if (File.Exists(OptSetPathApp) && File.Exists(OptSetPath))
					{
						Func<string, string> func = (string file) =>
						{
							using FileStream inputStream = File.OpenRead(file);
							using SHA256 sHA = SHA256.Create();
							return BitConverter.ToString(sHA.ComputeHash(inputStream)).Replace("-", "").ToLower();
						};
						if (func(OptSetPath) != func(OptSetPathApp))
						{
							File.Delete(OptSetPathApp);
							Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", "Backed up UserCfg.opt file does not match original. Deleting back up file  ...");
						}
						else
						{
							Logger.Log(LogLevel.Debug, "MemoryManager:GetOptSettings", "Backed up UserCfg.opt file exact match of original.  Keeping backed up file ...");
						}
					}
				}
			}
			catch (IOException value)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", $"IO exception {value} copying UserCfg.opt file");
			}
			catch (UnauthorizedAccessException value2)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", $"Unauthorised access exception {value2} copying UserCfg.opt file");
			}
			catch (Exception value3)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", $"Exception {value3} copying UserCfg.opt file");
			}
		}
		else if (File.Exists(OptSetPathApp))
		{
			FileAttributes attributes = File.GetAttributes(OptSetPathApp);
			if ((attributes & FileAttributes.ReadOnly) != 0)
			{
				Logger.Log(LogLevel.Warning, "MemoryManager:GetOptSettings", "Remnant backup UserCfg.opt file is read-only; clearing read-only attribute.");
				File.SetAttributes(OptSetPathApp, attributes & ~FileAttributes.ReadOnly);
			}
			string path = OptSetPathApp + ".temp";
			if (File.Exists(path))
			{
				FileAttributes attributes2 = File.GetAttributes(path);
				if ((attributes2 & FileAttributes.ReadOnly) != 0)
				{
					Logger.Log(LogLevel.Warning, "MemoryManager:GetOptSettings", "Remnant temp UserCfg.opt file is read-only; clearing read-only attribute.");
					File.SetAttributes(path, attributes2 & ~FileAttributes.ReadOnly);
				}
				try
				{
					File.Delete(path);
					Logger.Log(LogLevel.Debug, "MemoryManager:GetOptSettings", "Deleted remnant temp UserCfg.opt file.");
				}
				catch (Exception value4)
				{
					Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", $"Exception {value4} deleting remnant temp UserCfg.opt file");
				}
			}
			if (((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show(Application.Current.MainWindow, "A backup of UserCfg.opt was found, suggesting that this app did not shut down properly last time. If this was due to an MSFS crash or MSFS exit without first returning to the main menu, it's recommended to use the backup instead, as the existing file may have incorrect settings. Would you like to restore the backup?", "Backed up UserCfg.Opt file found", MessageBoxButton.YesNo, MessageBoxImage.Exclamation))) == MessageBoxResult.Yes)
			{
				RestoreUserCfgOptFile(CTDRecovery: true);
				((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show(Application.Current.MainWindow, "It is strongly recommended that you exit MSFS immediately, without changing any settings first, and restart it so that MSFS loads the restored UserCfg.opt file correctly.", "UserCfg.opt successfully restored", MessageBoxButton.OK, MessageBoxImage.Exclamation)));
			}
			else
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:GetOptSettings", "Backed up UserCfg.opt file found but not used.  Deleting back up file ...");
				try
				{
					File.Delete(OptSetPathApp);
				}
				catch (IOException value5)
				{
					Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", $"IO exception {value5} deleting UserCfg.opt file");
				}
				catch (UnauthorizedAccessException value6)
				{
					Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", $"Unauthorised access exception {value6} deleting UserCfg.opt file");
				}
				catch (Exception value7)
				{
					Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", $"Exception {value7} deleting UserCfg.opt file");
				}
			}
		}
		if (OptSetParser.GetOrModifyItem(OptSetPath, "Adapter", 1) != null)
		{
			Model.gpuModel = Convert.ToString(OptSetParser.GetOrModifyItem(OptSetPath, "Adapter", 1));
		}
		if (OptSetParser.GetOrModifyItem(OptSetPath, "InstalledPackagesPath", 1) != null)
		{
			Model.InstalledPackagesPath = Convert.ToString(OptSetParser.GetOrModifyItem(OptSetPath, "InstalledPackagesPath", 1));
		}
		Model.DefaultTLOD = (configTLODPC = (float)Math.Round(Convert.ToSingle((DefaultTLODStr = OptSetParser.GetOrModifyItem(OptSetPath, "LoDFactor", 1)).Replace(',', '.'), CultureInfo.InvariantCulture) * 100f));
		Model.DefaultTLOD_VR = (configTLODVR = (float)Math.Round(Convert.ToSingle((DefaultTLOD_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "LoDFactor", 3)).Replace(',', '.'), CultureInfo.InvariantCulture) * 100f));
		Model.DefaultOLOD = (configOLODPC = (float)Math.Round(Convert.ToSingle((DefaultOLODStr = OptSetParser.GetOrModifyItem(OptSetPath, "LoDFactor", 2)).Replace(',', '.'), CultureInfo.InvariantCulture) * 100f));
		Model.DefaultOLOD_VR = (configOLODVR = (float)Math.Round(Convert.ToSingle((DefaultOLOD_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "LoDFactor", 4)).Replace(',', '.'), CultureInfo.InvariantCulture) * 100f));
		configAniso = Convert.ToInt32(OptSetParser.GetOrModifyItem(OptSetPath, "MaxAnisotropy", 1));
		configCubeMap = Convert.ToInt32(OptSetParser.GetOrModifyItem(OptSetPath, "Size", 1, "ReflectionProbe"));
		configWaterWaves = Convert.ToInt32(OptSetParser.GetOrModifyItem(OptSetPath, "FFTSize", 1));
		Model.DefaultCloudQ = Convert.ToInt32(DefaultCloudQStr = OptSetParser.GetOrModifyItem(OptSetPath, "Quality", 1, "VolumetricClouds"));
		Model.DefaultCloudQ_VR = Convert.ToInt32(DefaultCloudQ_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "Quality", 1, "VolumetricClouds", 2));
		if (Model.isMSFS2024)
		{
			Model.DefaultOSTPCache = Convert.ToInt32(DefaultOSTPCacheStr = OptSetParser.GetOrModifyItem(OptSetPath, "Quality", 1, "OffscreenTerrainPreCaching"));
			Model.DefaultTShadowsEnabled = Convert.ToInt32(DefaultTShadowsEnabledStr = OptSetParser.GetOrModifyItem(OptSetPath, "Enabled", 1, "HeightFieldShadows")) == 1;
			Model.DefaultTShadows = Convert.ToInt32(DefaultTShadowsStr = OptSetParser.GetOrModifyItem(OptSetPath, "Size", 1, "HeightFieldShadows"));
			Model.DefaultRTShadows = Convert.ToInt32(DefaultRTShadowsStr = OptSetParser.GetOrModifyItem(OptSetPath, "Enabled", 1, "RaytracedShadows")) == 1;
			Model.DefaultDispMap = Convert.ToInt32(DefaultDispMapStr = OptSetParser.GetOrModifyItem(OptSetPath, "Enabled", 1, "DisplacementMapping")) == 1;
			Model.DefaultBldings = Convert.ToInt32(DefaultBldingsStr = OptSetParser.GetOrModifyItem(OptSetPath, "Quality", 1, "Buildings"));
			Model.DefaultTrees = Convert.ToInt32(DefaultTreesStr = OptSetParser.GetOrModifyItem(OptSetPath, "TreesQuality", 1, "Procedural"));
			Model.DefaultPlants = Convert.ToInt32(DefaultPlantsStr = OptSetParser.GetOrModifyItem(OptSetPath, "PlantsQuality", 1, "Procedural"));
			Model.DefaultGrass = Convert.ToInt32(DefaultGrassStr = OptSetParser.GetOrModifyItem(OptSetPath, "GrassQuality", 1, "Procedural"));
			Model.DefaultPkdAcQ = Convert.ToInt32(DefaultPkdAcQStr = OptSetParser.GetOrModifyItem(OptSetPath, "ParkedAircraftQuantity", 1, "Traffic"));
			Model.DefaultAcTrafQ = Convert.ToInt32(DefaultAcTrafQStr = OptSetParser.GetOrModifyItem(OptSetPath, "AircraftTrafficQuantity", 1, "Traffic"));
			Model.DefaultOSTPCache_VR = Convert.ToInt32(DefaultOSTPCache_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "Quality", 1, "OffscreenTerrainPreCaching", 2));
			Model.DefaultTShadowsEnabled_VR = Convert.ToInt32(DefaultTShadowsEnabled_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "Enabled", 1, "HeightFieldShadows", 2)) == 1;
			Model.DefaultTShadows_VR = Convert.ToInt32(DefaultTShadows_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "Size", 1, "HeightFieldShadows", 2));
			Model.DefaultRTShadows_VR = Convert.ToInt32(DefaultRTShadows_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "Enabled", 1, "RaytracedShadows", 2)) == 1;
			Model.DefaultDispMap_VR = Convert.ToInt32(DefaultDispMap_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "Enabled", 1, "DisplacementMapping", 2)) == 1;
			Model.DefaultBldings_VR = Convert.ToInt32(DefaultBldings_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "Quality", 1, "Buildings", 2));
			Model.DefaultTrees_VR = Convert.ToInt32(DefaultTrees_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "TreesQuality", 1, "Procedural", 2));
			Model.DefaultPlants_VR = Convert.ToInt32(DefaultPlants_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "PlantsQuality", 1, "Procedural", 2));
			Model.DefaultGrass_VR = Convert.ToInt32(DefaultGrass_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "GrassQuality", 1, "Procedural", 2));
			Model.DefaultPkdAcQ_VR = Convert.ToInt32(DefaultPkdAcQ_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "ParkedAircraftQuantity", 1, "Traffic", 4));
			Model.DefaultAcTrafQ_VR = Convert.ToInt32(DefaultAcTrafQ_VRStr = OptSetParser.GetOrModifyItem(OptSetPath, "AircraftTrafficQuantity", 1, "Traffic", 4));
			Model.DefaultDynSet = Convert.ToInt32(OptSetParser.GetOrModifyItem(OptSetPath, "DynamicSettings", 1)) == 1;
			Model.DefaultDynSetVR = Convert.ToInt32(OptSetParser.GetOrModifyItem(OptSetPath, "DynamicSettingsVR", 1)) == 1;
			Model.DefaultDynSetFrameRateTarget = Convert.ToInt32(OptSetParser.GetOrModifyItem(OptSetPath, "TargetFrameRate", 1));
			Model.DefaultDynSetFrameRateTarget_VR = Convert.ToInt32(OptSetParser.GetOrModifyItem(OptSetPath, "TargetFrameRateVR", 1));
		}
		if (!Model.MemoryCompatTestComplete)
		{
			Logger.Log(LogLevel.Information, "MemoryManager:GetOptSettings", "GPU: " + Model.gpuModel);
			Logger.Log(LogLevel.Information, "MemoryManager:GetOptSettings", "CPU: " + Model.cpuNameValue);
			LogCpuSummary();
			if (GetPhysicallyInstalledSystemMemory(out var totalKb) && totalKb > 0)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:GetOptSettings", "RAM: " + totalKb / 1024 / 1024 + " GB");
			}
		}
		if (Model.LogPlus && Model.MemoryCompatTestComplete)
		{
			Logger.Log(LogLevel.Information, "MemoryManager:GetOptSettings", "configTLODPC " + configTLODPC);
			Logger.Log(LogLevel.Information, "MemoryManager:GetOptSettings", "configTLODVR " + configTLODVR);
			Logger.Log(LogLevel.Information, "MemoryManager:GetOptSettings", "configOLODPC " + configOLODPC);
			Logger.Log(LogLevel.Information, "MemoryManager:GetOptSettings", "configOLODVR " + configOLODVR);
			Logger.Log(LogLevel.Information, "MemoryManager:GetOptSettings", "configAniso " + configAniso);
			Logger.Log(LogLevel.Information, "MemoryManager:GetOptSettings", "configCubeMap " + configCubeMap);
			Logger.Log(LogLevel.Information, "MemoryManager:GetOptSettings", "configWaterWaves " + configWaterWaves);
		}
		if (Model.MemoryCompatTestComplete)
		{
			Logger.Log(LogLevel.Information, "MemoryManager:GetOptSettings", "Successfully read required settings from UserOpt.cfg");
		}
		[DllImport("kernel32.dll", EntryPoint = "GetPhysicallyInstalledSystemMemory")]
		static extern bool GetPhysicallyInstalledSystemMemory(out long totalKb);
		bool IsMissing(string key, int index = -1, string section = null)
		{
			if (((section == null) ? OptSetParser.GetOrModifyItem(OptSetPath, key, index) : OptSetParser.GetOrModifyItem(OptSetPath, key, index, section)) == null)
			{
				missingKeys.Add((section != null) ? $"{section}.{key}[{index}]" : $"{key}[{index}]");
				return true;
			}
			return false;
		}
	}

	public void LogCpuSummary()
	{
		if (Model.LogPlus)
		{
			foreach (ServiceModel.CoreInfo core in Model.Cores)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:LogCpuSummary", $"Core {core.CoreIndex}: CoreType={((core.LogicalProcessors.Count == 2) ? "SMT Pair" : "Single")}, LogicalProcessors=[{string.Join(",", core.LogicalProcessors)}], EfficiencyClass={core.EfficiencyClass}");
			}
		}
		Logger.Log(LogLevel.Information, "MemoryManager:LogCpuSummary", $"CPU Core Summary: Physical={Model.Cores.Count}, Logical={Model.Cores.Sum((ServiceModel.CoreInfo c) => c.LogicalProcessors.Count)}, SMT Cores={Model.SmtCores}, Single Cores={Model.SingleLpCores.Count}");
	}

	public void RestoreUserCfgOptSettings()
	{
		if ((File.GetAttributes(OptSetPath) & FileAttributes.ReadOnly) != 0)
		{
			Logger.Log(LogLevel.Warning, "MemoryManager:RestoreUserCfgOptSettings", "UserCfg.opt is read-only; skipping restore and deleting backup file.");
			try
			{
				if (File.Exists(OptSetPathApp))
				{
					File.Delete(OptSetPathApp);
				}
				return;
			}
			catch
			{
				return;
			}
		}
		Logger.Log(LogLevel.Information, "MemoryManager:RestoreUserCfgOptSettings", "Reverting UserCfg.opt individual settings modified during this session to original");
		try
		{
			string text = OptSetPathApp + ".temp";
			File.Copy(OptSetPath, text, overwrite: true);
			OptSetParser.GetOrModifyItem(text, "LoDFactor", 1, null, 1, DefaultTLODStr);
			OptSetParser.GetOrModifyItem(text, "LoDFactor", 3, null, 1, DefaultTLOD_VRStr);
			OptSetParser.GetOrModifyItem(text, "LoDFactor", 2, null, 1, DefaultOLODStr);
			OptSetParser.GetOrModifyItem(text, "LoDFactor", 4, null, 1, DefaultOLOD_VRStr);
			OptSetParser.GetOrModifyItem(text, "Quality", 1, "VolumetricClouds", 1, DefaultCloudQStr);
			OptSetParser.GetOrModifyItem(text, "Quality", 1, "VolumetricClouds", 2, DefaultCloudQ_VRStr);
			if (Model.isMSFS2024)
			{
				OptSetParser.GetOrModifyItem(text, "DynamicSettings", 1, null, 1, Model.DefaultDynSet ? "1" : "0");
				OptSetParser.GetOrModifyItem(text, "DynamicSettingsVR", 1, null, 1, Model.DefaultDynSetVR ? "1" : "0");
				OptSetParser.GetOrModifyItem(text, "TargetFrameRate", 1, null, 1, Model.DefaultDynSetFrameRateTarget.ToString());
				OptSetParser.GetOrModifyItem(text, "TargetFrameRateVR", 1, null, 1, Model.DefaultDynSetFrameRateTarget_VR.ToString());
				OptSetParser.GetOrModifyItem(text, "TreesQuality", 1, "Procedural", 1, DefaultTreesStr);
				OptSetParser.GetOrModifyItem(text, "Enabled", 1, "RaytracedShadows", 1, DefaultRTShadowsStr);
				OptSetParser.GetOrModifyItem(text, "TreesQuality", 1, "Procedural", 2, DefaultTrees_VRStr);
				OptSetParser.GetOrModifyItem(text, "Enabled", 1, "RaytracedShadows", 2, DefaultRTShadows_VRStr);
				OptSetParser.GetOrModifyItem(text, "Enabled", 1, "HeightFieldShadows", 1, DefaultTShadowsEnabledStr);
				OptSetParser.GetOrModifyItem(text, "Size", 1, "HeightFieldShadows", 1, DefaultTShadowsStr);
				OptSetParser.GetOrModifyItem(text, "Quality", 1, "Buildings", 1, DefaultBldingsStr);
				OptSetParser.GetOrModifyItem(text, "Enabled", 1, "DisplacementMapping", 1, DefaultDispMapStr);
				OptSetParser.GetOrModifyItem(text, "Quality", 1, "OffscreenTerrainPreCaching", 1, DefaultOSTPCacheStr);
				OptSetParser.GetOrModifyItem(text, "PlantsQuality", 1, "Procedural", 1, DefaultPlantsStr);
				OptSetParser.GetOrModifyItem(text, "GrassQuality", 1, "Procedural", 1, DefaultGrassStr);
				OptSetParser.GetOrModifyItem(text, "ParkedAircraftQuantity", 1, "Traffic", 1, DefaultPkdAcQStr);
				OptSetParser.GetOrModifyItem(text, "AircraftTrafficQuantity", 1, "Traffic", 1, DefaultAcTrafQStr);
				OptSetParser.GetOrModifyItem(text, "Enabled", 1, "HeightFieldShadows", 2, DefaultTShadowsEnabled_VRStr);
				OptSetParser.GetOrModifyItem(text, "Size", 1, "HeightFieldShadows", 2, DefaultTShadows_VRStr);
				OptSetParser.GetOrModifyItem(text, "Quality", 1, "Buildings", 2, DefaultBldings_VRStr);
				OptSetParser.GetOrModifyItem(text, "Enabled", 1, "DisplacementMapping", 2, DefaultDispMap_VRStr);
				OptSetParser.GetOrModifyItem(text, "Quality", 1, "OffscreenTerrainPreCaching", 2, DefaultOSTPCache_VRStr);
				OptSetParser.GetOrModifyItem(text, "PlantsQuality", 1, "Procedural", 2, DefaultPlants_VRStr);
				OptSetParser.GetOrModifyItem(text, "GrassQuality", 1, "Procedural", 2, DefaultGrass_VRStr);
				OptSetParser.GetOrModifyItem(text, "ParkedAircraftQuantity", 1, "Traffic", 4, DefaultPkdAcQ_VRStr);
				OptSetParser.GetOrModifyItem(text, "AircraftTrafficQuantity", 1, "Traffic", 4, DefaultAcTrafQ_VRStr);
			}
			if (ValidateConfigFormat(text))
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:RestoreUserCfgOptSettings", "UserCfg.opt backup file refreshed with individual changes successfully");
				File.Copy(text, OptSetPathApp, overwrite: true);
				File.Delete(text);
				RestoreUserCfgOptFile(CTDRecovery: false, IndividualChanges: true);
			}
			return;
		}
		catch (IOException value)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:RestoreUserCfgOptSettings", $"IO exception {value} restoring UserCfg.opt file");
		}
		catch (UnauthorizedAccessException value2)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:RestoreUserCfgOptSettings", $"Unauthorised access exception {value2} restoring UserCfg.opt file");
		}
		catch (Exception value3)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:RestoreUserCfgOptSettings", $"Exception {value3} restoring UserCfg.opt file");
		}
		Logger.Log(LogLevel.Error, "MemoryManager:RestoreUserCfgOptSettings", "Unable to restore UserCfg.opt file with individual changes successfully. Restoring back up file ...");
		RestoreUserCfgOptFile();
	}

	public static bool ValidateConfigFormat(string filePath)
	{
		string[] array = File.ReadAllLines(filePath);
		int num = 0;
		Regex regex = new Regex("^\\s*(\\w+(\\.\\w+)*)\\s+\"?([^\"]+)\"?$");
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			string text = array2[i].Trim();
			if (text.StartsWith("{"))
			{
				num++;
			}
			else if (text.StartsWith("}"))
			{
				num--;
			}
			if (!text.StartsWith("{") && !text.StartsWith("}") && text.Length > 0 && !regex.IsMatch(text))
			{
				return false;
			}
		}
		string contents = string.Join("\n", array);
		UTF8Encoding encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
		File.WriteAllText(filePath, contents, encoding);
		return num == 0;
	}

	public void RestoreUserCfgOptFile(bool CTDRecovery = false, bool IndividualChanges = false)
	{
		if ((File.GetAttributes(OptSetPath) & FileAttributes.ReadOnly) != 0)
		{
			Logger.Log(LogLevel.Warning, "MemoryManager:RestoreUserCfgOptFile", "UserCfg.opt is read-only; skipping restore and deleting backup file.");
			try
			{
				if (File.Exists(OptSetPathApp))
				{
					File.Delete(OptSetPathApp);
				}
				return;
			}
			catch (Exception value)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:RestoreUserCfgOptFile", $"Exception {value} deleting backup file while skipping restore");
				return;
			}
		}
		if (File.Exists(OptSetPathApp))
		{
			if (!IndividualChanges)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:RestoreUserCfgOptFile", "Restoring UserCfg.opt file to " + (CTDRecovery ? "existing backup version due to unclean exit last session" : "pre flight session state"));
			}
			try
			{
				for (int i = 0; i < 10; i++)
				{
					try
					{
						File.Copy(OptSetPathApp, OptSetPath, overwrite: true);
					}
					catch (IOException)
					{
						if (i == 9)
						{
							throw new IOException($"Unable to write to {OptSetPath} after {10} attempts.");
						}
						Thread.Sleep(50);
						continue;
					}
					break;
				}
				if (!File.Exists(OptSetPathApp) || !File.Exists(OptSetPath))
				{
					return;
				}
				Func<string, string> func = (string file) =>
				{
					using FileStream inputStream = File.OpenRead(file);
					using SHA256 sHA = SHA256.Create();
					return BitConverter.ToString(sHA.ComputeHash(inputStream)).Replace("-", "").ToLower();
				};
				if (func(OptSetPath) == func(OptSetPathApp))
				{
					File.Delete(OptSetPathApp);
					Logger.Log(LogLevel.Debug, "MemoryManager:RestoreUserCfgOptFile", "Restored UserCfg.opt file exact match of back up. Deleting back up file ...");
					return;
				}
				Logger.Log(LogLevel.Error, "MemoryManager:RestoreUserCfgOptFile", "Restored UserCfg.opt file does not match backup.  Keeping backed up file ...");
				((DispatcherObject)Application.Current).Dispatcher.Invoke<MessageBoxResult>((Func<MessageBoxResult>)(() => MessageBox.Show(Application.Current.MainWindow, "The restored UserCfg.opt file doesn't match the backup and may be corrupt. The verified backup, identical to the original when created, has been retained in your MSFS root directory. After reviewing its contents, it's strongly recommended to manually replace the original file with the backup.", "Restoration of UserCfg.Opt Error", MessageBoxButton.OK, MessageBoxImage.Exclamation)));
				return;
			}
			catch (IOException value2)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", $"IO exception {value2} deleting UserCfg.opt file");
				return;
			}
			catch (UnauthorizedAccessException value3)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", $"Unauthorised access exception {value3} deleting UserCfg.opt file");
				return;
			}
			catch (Exception value4)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:GetOptSettings", $"Exception {value4} deleting UserCfg.opt file");
				return;
			}
		}
		Logger.Log(LogLevel.Debug, "MemoryManager:RestoreUserCfgOptFile", "Back up UserCfg.opt not found");
	}

	private void OffsetSearch()
	{
		long num = 4194304L;
		bool flag = false;
		long num2 = 0L;
		long moduleAddress = MemoryInterface.GetModuleAddress(Model.SimModule);
		Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch", "OffsetModuleBase search started");
		while (num2 < 1048576 && !flag)
		{
			addrTLOD = MemoryInterface.ReadMemory<long>(moduleAddress + num + num2) + OffsetPointerMain;
			if (addrTLOD > 0)
			{
				addrTLOD_VR = MemoryInterface.ReadMemory<long>(addrTLOD) + OffsetPointerTlodVr;
				addrTLOD = MemoryInterface.ReadMemory<long>(addrTLOD) + OffsetPointerTlod;
				addrOLOD_VR = addrTLOD_VR + OffsetPointerOlod;
				addrOLOD = addrTLOD + OffsetPointerOlod;
				addrCloudQ = addrTLOD + OffsetPointerCloudQ;
				addrCloudQ_VR = addrCloudQ + OffsetPointerCloudQVr;
				addrVrMode = addrTLOD - OffsetPointerVrMode;
				BoundaryTest();
			}
			if (allowMemoryWrites)
			{
				flag = true;
			}
			else
			{
				num2++;
			}
		}
		if (flag)
		{
			Model.SetSetting("offsetModuleBase", "0x" + (num + num2).ToString("X8"));
			Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch", "New offsetModuleBase found and saved: 0x" + (num + num2).ToString("X8"));
			Model.OffsetModuleBase = num + num2;
		}
		else
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch", $"OffsetModuleBase not found after {num2} iterations");
		}
	}

	private void OffsetSearch2024(bool fullTest = false)
	{
		Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch2024", "Starting module offset search");
		if (Model.moduleBaseAddress == 0L)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:OffsetSearch2024", "Failed to get module base address");
			return;
		}
		long offsetModuleBase = Model.OffsetModuleBase;
		long num = 33554432L;
		long num2 = offsetModuleBase + num;
		long num3 = 4L;
		int num4 = 0;
		int num5 = 0;
		Model.OffsetSearchingActive = true;
		long num6 = offsetModuleBase;
		long num7 = num3;
		bool flag = true;
		bool flag2 = true;
		List<(float, long)> list = new List<(float, long)>();
		float num8 = 0f;
		while (num6 < num2)
		{
			num8 = ((list.Count > 0) ? list[0].Item1 : 0f);
			if (flag2)
			{
				num6 = offsetModuleBase;
				flag2 = false;
			}
			else
			{
				if (flag)
				{
					num6 = offsetModuleBase + num7;
					num4++;
				}
				else
				{
					num6 = offsetModuleBase - num7;
					num7 += num3;
				}
				flag = !flag;
			}
			try
			{
				GetAddresses2024(num6);
				float num9 = 0.1f;
				if (addrTLOD > 0 && Math.Abs(GetTLOD_PC() - configTLODPC) < num9 && Math.Abs(GetOLOD_PC() - configOLODPC) < num9)
				{
					Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch2024", $"Preliminary match found at offset: 0x{num6:X8}");
					if (Model.LogPlus)
					{
						BoundaryTest(logResult: true);
					}
					else
					{
						BoundaryTest();
					}
					if (Model.isMSFS2024)
					{
						float num10 = ScoreOffsetCandidate(Model.LogPlus);
						Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch2024", $"Confidence: {num10:P2}");
						list.Add((num10, num6));
						list = list.OrderByDescending(((float confidence, long offset) r) => r.confidence).Take(5).ToList();
						num8 = list[0].Item1;
						float num11 = 1f - Math.Min(num8, 0.95f);
						num = (long)(33554432f * num11);
						num2 = offsetModuleBase + num;
					}
				}
				if (allowMemoryWrites)
				{
					Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch2024", $"Found valid module offset: 0x{num6:X8} after {num4} attempts");
					if (!fullTest)
					{
						Model.SetSetting("offsetModuleBase", "0x" + num6.ToString("X8"));
						Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch2024", "New offsetModuleBase found and saved: 0x" + num6.ToString("X8"));
						Model.OffsetModuleBase = num6;
						Model.OffsetSearchingActive = false;
					}
					BoundaryTest(logResult: true);
					if (!fullTest)
					{
						return;
					}
					allowMemoryWrites = false;
					num5++;
				}
			}
			catch
			{
			}
			if (!flag && num4 % 262144 == 0)
			{
				long value = Math.Abs(num6 - offsetModuleBase) / 1024 / 1024;
				long value2 = num / 1048576;
				if (num8 > 0f)
				{
					Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch2024", $"Searched ±{value} MB | Confidence {num8:P1} | Search radius {value2} MB");
				}
				else
				{
					Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch2024", $"Searched ±{value} MB");
				}
			}
		}
		if (fullTest)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:OffsetSearch2024", $"Found {num5} valid module offset" + ((num5 != 1) ? "s" : "") + $" after {num4} attempts. " + ((num5 == 1) ? "Desired result!" : "Bad Result!"));
		}
		else
		{
			Logger.Log(LogLevel.Error, "MemoryManager:OffsetSearch2024", $"Failed to find a valid module offset after {num4} attempts");
		}
		if (Model.DevTesting || Model.LogPlus)
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch2024", "Top confidence results:");
			foreach (var item2 in list)
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch2024", $"  {item2.Item1:P2} at offset 0x{item2.Item2:X8}");
			}
		}
		if (list.Count > 0 && !allowMemoryWrites)
		{
			int num12 = Model.DevCandidateIndex - 1;
			if (num12 < 0)
			{
				num12 = 0;
			}
			if (num12 >= list.Count)
			{
				num12 = list.Count - 1;
			}
			long item = list[num12].Item2;
			Logger.Log(LogLevel.Debug, "MemoryManager:OffsetSearch2024", $"Selected candidate #{Model.DevCandidateIndex} -> offset 0x{item:X8}");
			Model.OffsetModuleBase = item;
			GetAddresses2024(Model.OffsetModuleBase);
			DetectVrOffsetDrift();
		}
		Model.OffsetSearchingActive = false;
	}

	private void DetectVrOffsetDrift()
	{
		float eps = 0.1f;
		float num = Convert.ToSingle(OptSetParser.GetOrModifyItem(OptSetPath, "SharpenAmount", 1).Replace(',', '.'), CultureInfo.InvariantCulture);
		float num2 = Convert.ToSingle(OptSetParser.GetOrModifyItem(OptSetPath, "SharpenAmountVR", 1).Replace(',', '.'), CultureInfo.InvariantCulture);
		float num3 = Convert.ToSingle(OptSetParser.GetOrModifyItem(OptSetPath, "ExposureBias", 1).Replace(',', '.'), CultureInfo.InvariantCulture);
		float num4 = Convert.ToSingle(OptSetParser.GetOrModifyItem(OptSetPath, "ExposureBiasVR", 1).Replace(',', '.'), CultureInfo.InvariantCulture);
		float num5 = Convert.ToSingle(OptSetParser.GetOrModifyItem(OptSetPath, "FoveatedRenderingScale", 1).Replace(',', '.'), CultureInfo.InvariantCulture);
		if (Math.Abs(GetFloatSetting(addrSharpAmt) - num) < eps && Math.Abs(GetFloatSetting(addrSharpAmtVR) - num2) < eps && Math.Abs(GetFloatSetting(addrExposureBias) - num3) < eps && Math.Abs(GetFloatSetting(addrExposureBiasVR) - num4) < eps && Math.Abs(GetFloatSetting(addrFoveatedRenderingScale) - num5) < eps && Math.Abs(GetTLOD_VR() - configTLODVR) < eps && Math.Abs(GetOLOD_VR() - configOLODVR) < eps)
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", $"All seven settings match within eps={eps:F3}; SharpenAmount={num} (addr 0x{addrSharpAmt:X}), SharpenAmountVR={num2} (addr 0x{addrSharpAmtVR:X}), ExposureBias={num3} (addr 0x{addrExposureBias:X}), ExposureBiasVR={num4} (addr 0x{addrExposureBiasVR:X}), FoveatedRenderingScale={num5} (addr 0x{addrFoveatedRenderingScale:X}), TLOD_VR={GetTLOD_VR():F3} (addr 0x{addrTLOD_VR:X}), OLOD_VR={GetOLOD_VR():F3} (addr 0x{addrOLOD_VR:X})");
			return;
		}
		List<(string Name, long Address, float ExpectedRaw, float? ExpectedConfig, bool IsVR)> failingFields = new List<(string, long, float, float?, bool)>();
		AddIfFailRaw("SharpenAmount", addrSharpAmt, num, GetFloatSetting(addrSharpAmt));
		AddIfFailRaw("SharpenAmount_VR", addrSharpAmtVR, num2, GetFloatSetting(addrSharpAmtVR));
		AddIfFailRaw("ExposureBias", addrExposureBias, num3, GetFloatSetting(addrExposureBias));
		AddIfFailRaw("ExposureBias_VR", addrExposureBiasVR, num4, GetFloatSetting(addrExposureBiasVR));
		AddIfFailRaw("FoveatedRenderingScale", addrFoveatedRenderingScale, num5, GetFloatSetting(addrFoveatedRenderingScale));
		float expectedRaw = configTLODVR / 100f;
		float expectedRaw2 = configOLODVR / 100f;
		float actualRaw;
		try
		{
			actualRaw = GetTLOD_VR() / 100f;
		}
		catch
		{
			actualRaw = MemoryInterface.ReadMemory<float>(addrTLOD_VR);
		}
		float actualRaw2;
		try
		{
			actualRaw2 = GetOLOD_VR() / 100f;
		}
		catch
		{
			actualRaw2 = MemoryInterface.ReadMemory<float>(addrOLOD_VR);
		}
		AddIfFailWithConfig("TLOD_VR", addrTLOD_VR, expectedRaw, configTLODVR, actualRaw);
		AddIfFailWithConfig("OLOD_VR", addrOLOD_VR, expectedRaw2, configOLODVR, actualRaw2);
		if (failingFields.Count == 0)
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", "No failing fields found after initial check.");
			return;
		}
		if (false)
		{
			try
			{
				(string, long, float, float?, bool) tuple = failingFields.FirstOrDefault(((string Name, long Address, float ExpectedRaw, float? ExpectedConfig, bool IsVR) f) => f.Name == "OLOD_VR");
				(string, long, float, float?, bool) tuple2 = tuple;
				if (tuple2.Item1 != null || tuple2.Item2 != 0L || tuple2.Item3 != 0f || tuple2.Item4.HasValue || tuple2.Item5)
				{
					long item = tuple.Item2;
					int num6 = 32;
					Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", $"OLOD neighborhood dump around 0x{item:X} +/-{num6} bytes:");
					for (int num7 = -num6; num7 <= num6; num7 += 4)
					{
						long num8 = item + num7;
						try
						{
							float value = MemoryInterface.ReadMemory<float>(num8);
							Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", $"{num7,3:+#;-#;0} (0x{num8:X}) float={value:F8}");
						}
						catch (Exception ex)
						{
							Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", $"{num7,3:+#;-#;0} (0x{num8:X}) read failed: {ex.Message}");
						}
					}
				}
				(string, long, float, float?, bool) tuple3 = failingFields.FirstOrDefault(((string Name, long Address, float ExpectedRaw, float? ExpectedConfig, bool IsVR) f) => f.Name == "TLOD_VR");
				tuple2 = tuple3;
				if (tuple2.Item1 != null || tuple2.Item2 != 0L || tuple2.Item3 != 0f || tuple2.Item4.HasValue || tuple2.Item5)
				{
					long item2 = tuple3.Item2;
					int num9 = 32;
					Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", $"TLOD neighborhood dump around 0x{item2:X} +/-{num9} bytes:");
					for (int num10 = -num9; num10 <= num9; num10 += 4)
					{
						long num11 = item2 + num10;
						try
						{
							float value2 = MemoryInterface.ReadMemory<float>(num11);
							Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", $"{num10,3:+#;-#;0} (0x{num11:X}) float={value2:F8}");
						}
						catch (Exception ex2)
						{
							Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", $"{num10,3:+#;-#;0} (0x{num11:X}) read failed: {ex2.Message}");
						}
					}
				}
			}
			catch (Exception ex3)
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", "Neighborhood dump failed: " + ex3.Message);
			}
		}
		Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", $"{failingFields.Count} failing fields detected, scanning +/-40 bytes in 4-byte steps");
		List<(int, bool, string)> list = new List<(int, bool, string)>();
		foreach (var item4 in failingFields)
		{
			int num12 = 0;
			int num13 = int.MaxValue;
			float value3 = 0f;
			long value4 = 0L;
			if (item4.Name == "OLOD_VR" || item4.Name == "TLOD_VR")
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", $"Scanning {item4.Name} around base 0x{item4.Address:X}");
			}
			for (int num14 = -40; num14 <= 40; num14 += 4)
			{
				if (num14 == 0)
				{
					continue;
				}
				long num15 = item4.Address + num14;
				bool flag = false;
				float num16 = 0f;
				try
				{
					num16 = MemoryInterface.ReadMemory<float>(num15);
					if (Math.Abs(num16 - item4.ExpectedRaw) < eps)
					{
						flag = true;
					}
					else
					{
						if (item4.ExpectedConfig.HasValue)
						{
							float num17 = item4.ExpectedConfig.Value / 100f;
							if (Math.Abs(num16 - num17) < eps)
							{
								flag = true;
							}
						}
						if (!flag)
						{
							try
							{
								float floatSetting = GetFloatSetting(num15);
								if (Math.Abs(floatSetting - item4.ExpectedRaw) < eps || (item4.ExpectedConfig.HasValue && Math.Abs(floatSetting - item4.ExpectedConfig.Value / 100f) < eps))
								{
									flag = true;
								}
							}
							catch
							{
							}
						}
						if (!flag)
						{
							try
							{
								float num18 = MemoryInterface.ReadMemory<float>(MemoryInterface.ReadMemory<long>(num15));
								if (Math.Abs(num18 - item4.ExpectedRaw) < eps || (item4.ExpectedConfig.HasValue && Math.Abs(num18 - item4.ExpectedConfig.Value / 100f) < eps))
								{
									flag = true;
									num16 = num18;
								}
							}
							catch
							{
							}
						}
						if (!flag)
						{
							try
							{
								float num19 = (float)MemoryInterface.ReadMemory<double>(num15);
								if (Math.Abs(num19 - item4.ExpectedRaw) < eps || (item4.ExpectedConfig.HasValue && Math.Abs(num19 - item4.ExpectedConfig.Value / 100f) < eps))
								{
									flag = true;
									num16 = num19;
								}
							}
							catch
							{
							}
						}
					}
				}
				catch
				{
					flag = false;
				}
				if (flag)
				{
					int num20 = Math.Abs(num14);
					if (num20 < num13 || (num20 == num13 && num14 > num12))
					{
						num13 = num20;
						num12 = num14;
						value3 = num16;
						value4 = num15;
					}
				}
			}
			if (num12 != 0)
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", $"Field {item4.Name} realigns at {num12:+#;-#;0} bytes (expected raw {item4.ExpectedRaw}, config {(item4.ExpectedConfig.HasValue ? item4.ExpectedConfig.Value.ToString("F3") : "N/A")})");
				if (item4.Name == "OLOD_VR" || item4.Name == "TLOD_VR")
				{
					Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", $"Field {item4.Name} matched at testAddress=0x{value4:X} shift={num12:+#;-#;0} rawValue={value3:F8}");
				}
				list.Add((num12, item4.IsVR, item4.Name));
			}
			else
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:DetectVrOffsetDrift", "Field " + item4.Name + " did not realign within +/-40 bytes");
			}
		}
		if (list.Count == 0)
		{
			Logger.Log(LogLevel.Warning, "MemoryManager:DetectVrOffsetDrift", "No realignment detected — drift pattern is random. Offset likely incorrect.");
			return;
		}
		(int, bool, string) tuple4 = list.FirstOrDefault(((int DeltaSigned, bool IsVR, string Name) d) => d.Name == "TLOD_VR");
		(int, bool, string) tuple5 = list.FirstOrDefault(((int DeltaSigned, bool IsVR, string Name) d) => d.Name == "OLOD_VR");
		(int, bool, string) tuple6 = tuple4;
		if (tuple6.Item1 != 0 || tuple6.Item2 || tuple6.Item3 != null)
		{
			tuple6 = tuple5;
			if ((tuple6.Item1 != 0 || tuple6.Item2 || tuple6.Item3 != null) && tuple4.Item1 == tuple5.Item1)
			{
				int item3 = tuple4.Item1;
				long value5 = OffsetVR + item3;
				Logger.Log(LogLevel.Warning, "MemoryManager:DetectVrOffsetDrift", $"TLOD_VR and OLOD_VR both realign by {item3:+#;-#;0} bytes — this strongly suggests the VR offset has shifted by {item3:+#;-#;0} bytes. Current OffsetVR={OffsetVR} => Suggested OffsetVR={value5} (0x{value5:X}).");
				if (item3 != 0)
				{
					OffsetShift += item3;
					OffsetShiftChanged = true;
				}
			}
		}
		List<IGrouping<int, (int, bool)>> list2 = (from d in list.Select(((int DeltaSigned, bool IsVR, string Name) d) => (Delta: Math.Abs(d.DeltaSigned), IsVR: d.IsVR))
			group d by d.Delta into g
			orderby g.Key
			select g).ToList();
		if (list2.Count == 1)
		{
			IGrouping<int, (int, bool)> grouping = list2[0];
			if (grouping.Count() >= 2)
			{
				int key = grouping.Key;
				Logger.Log(LogLevel.Warning, "MemoryManager:DetectVrOffsetDrift", $"Consistent single-cluster drift detected (+{key} bytes) with {grouping.Count()} supporting fields.");
			}
			else
			{
				Logger.Log(LogLevel.Warning, "MemoryManager:DetectVrOffsetDrift", "Single cluster has insufficient support. Offset likely incorrect.");
			}
		}
		else if (list2.Count == 2)
		{
			IGrouping<int, (int, bool)> grouping2 = list2[0];
			IGrouping<int, (int, bool)> grouping3 = list2[1];
			int key2 = grouping2.Key;
			int key3 = grouping3.Key;
			if (grouping2.Count() < 2 || grouping3.Count() < 2)
			{
				Logger.Log(LogLevel.Warning, "MemoryManager:DetectVrOffsetDrift", "Double cluster detected but insufficient support. Offset likely incorrect.");
				return;
			}
			if (grouping2.Any(((int Delta, bool IsVR) x) => x.IsVR))
			{
				Logger.Log(LogLevel.Warning, "MemoryManager:DetectVrOffsetDrift", "Lower-delta cluster contains VR fields — inconsistent double split.");
				return;
			}
			if (grouping3.Any(((int Delta, bool IsVR) x) => !x.IsVR))
			{
				Logger.Log(LogLevel.Warning, "MemoryManager:DetectVrOffsetDrift", "Higher-delta cluster contains PC fields — inconsistent double split.");
				return;
			}
			Logger.Log(LogLevel.Warning, "MemoryManager:DetectVrOffsetDrift", $"Valid double-split drift detected (PC +{key2}, VR +{key3}).");
		}
		else
		{
			Logger.Log(LogLevel.Warning, "MemoryManager:DetectVrOffsetDrift", "More than two drift clusters detected — inconsistent drift. Offset likely incorrect.");
		}
		void AddIfFailRaw(string name, long addr, float num22, float num21)
		{
			if (Math.Abs(num21 - num22) >= eps)
			{
				failingFields.Add((name, addr, num22, null, name.EndsWith("_VR")));
			}
		}
		void AddIfFailWithConfig(string name, long addr, float num22, float expectedConfig, float num21)
		{
			if (Math.Abs(num21 - num22) >= eps)
			{
				failingFields.Add((name, addr, num22, expectedConfig, name.EndsWith("_VR")));
			}
		}
	}

	private float ScoreOffsetCandidate(bool logResult = false)
	{
		float num = 0.1f;
		int passed = 0;
		int failed = 0;
		float num2 = Convert.ToSingle(OptSetParser.GetOrModifyItem(OptSetPath, "SharpenAmount", 1).Replace(',', '.'), CultureInfo.InvariantCulture);
		float num3 = Convert.ToSingle(OptSetParser.GetOrModifyItem(OptSetPath, "SharpenAmountVR", 1).Replace(',', '.'), CultureInfo.InvariantCulture);
		float num4 = Convert.ToSingle(OptSetParser.GetOrModifyItem(OptSetPath, "ExposureBias", 1).Replace(',', '.'), CultureInfo.InvariantCulture);
		float num5 = Convert.ToSingle(OptSetParser.GetOrModifyItem(OptSetPath, "ExposureBiasVR", 1).Replace(',', '.'), CultureInfo.InvariantCulture);
		float num6 = Convert.ToSingle(OptSetParser.GetOrModifyItem(OptSetPath, "FoveatedRenderingScale", 1).Replace(',', '.'), CultureInfo.InvariantCulture);
		Check("TLOD_PC", Math.Abs(GetTLOD_PC() - configTLODPC) < num);
		Check("TLOD_VR", Math.Abs(GetTLOD_VR() - configTLODVR) < num);
		Check("OLOD_PC", Math.Abs(GetOLOD_PC() - configOLODPC) < num);
		Check("OLOD_VR", Math.Abs(GetOLOD_VR() - configOLODVR) < num);
		Check("Aniso", MemoryInterface.ReadMemory<int>(addrAnsio) == configAniso);
		Check("CubeMap", MemoryInterface.ReadMemory<int>(addrCubeMap) == configCubeMap);
		Check("WaterWaves", MemoryInterface.ReadMemory<int>(addrWaterWaves) == configWaterWaves);
		Check("OSTPreCaching_PC", GetIntSetting(addrOSTPreCaching) == Model.DefaultOSTPCache);
		Check("TShadowsEnabled_PC", GetBoolSetting(addrTShadowsEnabled) == Model.DefaultTShadowsEnabled);
		Check("RTShadows_PC", GetBoolSetting(addrRTShadows) == Model.DefaultRTShadows);
		Check("DispMap_PC", GetBoolSetting(addrDispMap) == Model.DefaultDispMap);
		Check("TerrainShadows_PC", GetIntSetting(addrTerrainShadows) == Model.DefaultTShadows);
		Check("Buildings_PC", GetIntSetting(addrBuildings) == Model.DefaultBldings);
		Check("Trees_PC", GetIntSetting(addrTrees) == Model.DefaultTrees);
		Check("Plants_PC", GetIntSetting(addrPlants) == Model.DefaultPlants);
		Check("Grass_PC", GetIntSetting(addrGrass) == Model.DefaultGrass);
		Check("PkdAcQ_PC", GetIntSetting(addrPkdAcQ) == Model.DefaultPkdAcQ);
		Check("AcTrafQ_PC", GetIntSetting(addrAcTrafQ) == Model.DefaultAcTrafQ);
		Check("OSTPreCaching_VR", GetIntSetting(addrOSTPreCaching + OffsetVR) == Model.DefaultOSTPCache_VR);
		Check("TShadowsEnabled_VR", GetBoolSetting(addrTShadowsEnabled + OffsetVR) == Model.DefaultTShadowsEnabled_VR);
		Check("RTShadows_VR", GetBoolSetting(addrRTShadows + OffsetVR) == Model.DefaultRTShadows_VR);
		Check("DispMap_VR", GetBoolSetting(addrDispMap + OffsetVR) == Model.DefaultDispMap_VR);
		Check("TerrainShadows_VR", GetIntSetting(addrTerrainShadows + OffsetVR) == Model.DefaultTShadows_VR);
		Check("Buildings_VR", GetIntSetting(addrBuildings + OffsetVR) == Model.DefaultBldings_VR);
		Check("Trees_VR", GetIntSetting(addrTrees + OffsetVR) == Model.DefaultTrees_VR);
		Check("Plants_VR", GetIntSetting(addrPlants + OffsetVR) == Model.DefaultPlants_VR);
		Check("Grass_VR", GetIntSetting(addrGrass + OffsetVR) == Model.DefaultGrass_VR);
		Check("PkdAcQ_VR", GetIntSetting(addrPkdAcQ + OffsetVR) == Model.DefaultPkdAcQ_VR);
		Check("AcTrafQ_VR", GetIntSetting(addrAcTrafQ + OffsetVR) == Model.DefaultAcTrafQ_VR);
		Check("SharpenAmount", GetFloatSetting(addrSharpAmt) == num2, doubleCount: true);
		Check("SharpenAmount_VR", GetFloatSetting(addrSharpAmtVR) == num3, doubleCount: true);
		Check("ExposureBias", GetFloatSetting(addrExposureBias) == num4, doubleCount: true);
		Check("ExposureBias_VR", GetFloatSetting(addrExposureBiasVR) == num5, doubleCount: true);
		Check("FoveatedRenderingScale", GetFloatSetting(addrFoveatedRenderingScale) == num6, doubleCount: true);
		int num7 = passed + failed;
		float num8 = ((num7 == 0) ? 0f : ((float)passed / (float)num7));
		if (logResult)
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:ScoreOffsetCandidate", $"Summary: {passed}/{num7} passed ({num8:P2})");
		}
		return num8;
		void Check(string name, bool condition, bool doubleCount = false)
		{
			if (condition)
			{
				passed++;
				if (doubleCount)
				{
					passed++;
				}
				if (logResult)
				{
					Logger.Log(LogLevel.Debug, "MemoryManager:ScoreOffsetCandidate", "PASS: " + name + (doubleCount ? "(X2)" : ""));
				}
			}
			else
			{
				failed++;
				if (doubleCount)
				{
					failed++;
				}
				if (logResult)
				{
					Logger.Log(LogLevel.Debug, "MemoryManager:ScoreOffsetCandidate", "FAIL: " + name + (doubleCount ? "(X2)" : ""));
				}
			}
		}
	}

	private BoundaryTest4Result BoundaryTest4()
	{
		List<BoundaryFieldResult> fields = new List<BoundaryFieldResult>();
		float eps = 0.1f;
		CheckFloat("TLOD_PC", addrTLOD, configTLODPC, () => GetTLOD_PC(), OffsetPointerTlod);
		CheckFloat("TLOD_VR", addrTLOD_VR, configTLODVR, () => GetTLOD_VR(), OffsetPointerTlod + OffsetVR);
		CheckFloat("OLOD_PC", addrOLOD, configOLODPC, () => GetOLOD_PC(), OffsetPointerOlod);
		CheckFloat("OLOD_VR", addrOLOD_VR, configOLODVR, () => GetOLOD_VR(), OffsetPointerOlod + OffsetVR);
		CheckInt("Aniso", addrAnsio, configAniso, OffsetPointerAnsio);
		CheckInt("CubeMap", addrCubeMap, configCubeMap, OffsetPointerCubeMap);
		CheckInt("WaterWaves", addrWaterWaves, configWaterWaves, OffsetPointerWaterWaves);
		CheckInt("OSTPreCaching_PC", addrOSTPreCaching, Model.DefaultOSTPCache, OffsetPointerOSTPreCaching);
		CheckBool("TShadowsEnabled_PC", addrTShadowsEnabled, Model.DefaultTShadowsEnabled, OffsetPointerTShadowsEnabled);
		CheckBool("RTShadows_PC", addrRTShadows, Model.DefaultRTShadows, OffsetPointerRTShadows);
		CheckBool("DispMap_PC", addrDispMap, Model.DefaultDispMap, OffsetPointerDispMap);
		CheckInt("TerrainShadows_PC", addrTerrainShadows, Model.DefaultTShadows, OffsetPointerTerrainShadows);
		CheckInt("Buildings_PC", addrBuildings, Model.DefaultBldings, OffsetPointerBuildings);
		CheckInt("Trees_PC", addrTrees, Model.DefaultTrees, OffsetPointerTrees);
		CheckInt("Plants_PC", addrPlants, Model.DefaultPlants, OffsetPointerPlants);
		CheckInt("Grass_PC", addrGrass, Model.DefaultGrass, OffsetPointerGrass);
		CheckInt("PkdAcQ_PC", addrPkdAcQ, Model.DefaultPkdAcQ, OffsetPointerPkdAcQ);
		CheckInt("AcTrafQ_PC", addrAcTrafQ, Model.DefaultAcTrafQ, OffsetPointerAcTrafQ);
		CheckInt("OSTPreCaching_VR", addrOSTPreCaching + OffsetVR, Model.DefaultOSTPCache_VR, OffsetPointerOSTPreCaching + OffsetVR);
		CheckBool("TShadowsEnabled_VR", addrTShadowsEnabled + OffsetVR, Model.DefaultTShadowsEnabled_VR, OffsetPointerTShadowsEnabled + OffsetVR);
		CheckBool("RTShadows_VR", addrRTShadows + OffsetVR, Model.DefaultRTShadows_VR, OffsetPointerRTShadows + OffsetVR);
		CheckBool("DispMap_VR", addrDispMap + OffsetVR, Model.DefaultDispMap_VR, OffsetPointerDispMap + OffsetVR);
		CheckInt("TerrainShadows_VR", addrTerrainShadows + OffsetVR, Model.DefaultTShadows_VR, OffsetPointerTerrainShadows + OffsetVR);
		CheckInt("Buildings_VR", addrBuildings + OffsetVR, Model.DefaultBldings_VR, OffsetPointerBuildings + OffsetVR);
		CheckInt("Trees_VR", addrTrees + OffsetVR, Model.DefaultTrees_VR, OffsetPointerTrees + OffsetVR);
		CheckInt("Plants_VR", addrPlants + OffsetVR, Model.DefaultPlants_VR, OffsetPointerPlants + OffsetVR);
		CheckInt("Grass_VR", addrGrass + OffsetVR, Model.DefaultGrass_VR, OffsetPointerGrass + OffsetVR);
		CheckInt("PkdAcQ_VR", addrPkdAcQ + OffsetVR, Model.DefaultPkdAcQ_VR, OffsetPointerPkdAcQ + OffsetVR);
		CheckInt("AcTrafQ_VR", addrAcTrafQ + OffsetVR, Model.DefaultAcTrafQ_VR, OffsetPointerAcTrafQ + OffsetVR);
		int num = Enumerable.Count(fields, (BoundaryFieldResult f) => f.Passed);
		int count = fields.Count;
		float confidence = ((count == 0) ? 0f : ((float)num / (float)count));
		return new BoundaryTest4Result
		{
			Confidence = confidence,
			Fields = fields
		};
		void CheckBool(string name, long address, bool expected, long offset)
		{
			bool flag = false;
			try
			{
				flag = GetBoolSetting(address);
			}
			catch
			{
			}
			fields.Add(new BoundaryFieldResult
			{
				Name = name,
				Address = address,
				Offset = offset,
				IsFloat = false,
				Passed = (expected == flag),
				Expected = (expected ? 1 : 0),
				Actual = (flag ? 1 : 0)
			});
		}
		void CheckFloat(string name, long address, float configValue, Func<float> getterFallback, long offset)
		{
			float num2 = float.NaN;
			try
			{
				num2 = MemoryInterface.ReadMemory<float>(address);
			}
			catch
			{
				num2 = float.NaN;
			}
			float num3 = float.NaN;
			if (getterFallback != null)
			{
				try
				{
					num3 = getterFallback();
				}
				catch
				{
					num3 = float.NaN;
				}
			}
			float num4 = configValue / 100f;
			float num5 = float.NaN;
			if (!float.IsNaN(num3))
			{
				num5 = ((!(Math.Abs(num3 - configValue) < Math.Max(1f, Math.Abs(configValue) * 0.001f))) ? num3 : (num3 / 100f));
			}
			float num6 = configValue;
			if (float.IsNaN(num2))
			{
				num6 = ((!name.Contains("LOD") && !name.EndsWith("_VR")) ? configValue : num4);
			}
			else if (Math.Abs(num2 - configValue) < eps)
			{
				num6 = configValue;
			}
			else if (Math.Abs(num2 - num4) < eps)
			{
				num6 = num4;
			}
			else if (!float.IsNaN(num5) && Math.Abs(num2 - num5) < eps)
			{
				num6 = num5;
			}
			else
			{
				float num7 = Math.Abs(num2 - configValue);
				float num8 = Math.Abs(num2 - num4);
				float num9 = (float.IsNaN(num5) ? float.MaxValue : Math.Abs(num2 - num5));
				if (num8 <= num7 && num8 <= num9)
				{
					num6 = num4;
				}
				else
				{
					num6 = ((!(num9 <= num7) || !(num9 <= num8)) ? configValue : num5);
				}
			}
			bool passed = !float.IsNaN(num2) && Math.Abs(num2 - num6) < eps;
			fields.Add(new BoundaryFieldResult
			{
				Name = name,
				Address = address,
				Offset = offset,
				IsFloat = true,
				Passed = passed,
				ExpectedFloat = num6,
				ActualFloat = num2
			});
		}
		void CheckInt(string name, long address, int expected, long offset)
		{
			int num2 = 0;
			try
			{
				num2 = MemoryInterface.ReadMemory<int>(address);
			}
			catch
			{
			}
			fields.Add(new BoundaryFieldResult
			{
				Name = name,
				Address = address,
				Offset = offset,
				IsFloat = false,
				Passed = (expected == num2),
				Expected = expected,
				Actual = num2
			});
		}
	}

	private void RunExtendedOffsetShiftAnalysis()
	{
		Logger.Log(LogLevel.Debug, "MemoryManager:RunExtendedOffsetShiftAnalysis", "Starting per-field drift analysis");
		BoundaryTest4Result boundaryTest4Result = BoundaryTest4();
		if (boundaryTest4Result == null || boundaryTest4Result.Fields == null || boundaryTest4Result.Fields.Count == 0)
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:RunExtendedOffsetShiftAnalysis", "Per-field drift analysis returned no fields");
			return;
		}
		List<BoundaryFieldResult> list = boundaryTest4Result.Fields.Where((BoundaryFieldResult boundaryFieldResult2) => !boundaryFieldResult2.Passed).ToList();
		if (list.Count == 0)
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:RunExtendedOffsetShiftAnalysis", "No failing fields to analyse");
			return;
		}
		Logger.Log(LogLevel.Debug, "MemoryManager:RunExtendedOffsetShiftAnalysis", $"{list.Count} failing fields detected");
		float num = 0.1f;
		List<(int Delta, bool IsVR, string Name)> driftPairs = new List<(int, bool, string)>();
		foreach (BoundaryFieldResult item in list)
		{
			int num2 = 0;
			int num3 = int.MaxValue;
			float f = float.NaN;
			int num4 = int.MinValue;
			long value = 0L;
			for (int num5 = -40; num5 <= 40; num5 += 4)
			{
				if (num5 == 0)
				{
					continue;
				}
				long num6 = item.Address + num5;
				try
				{
					if (item.IsFloat)
					{
						float num7 = MemoryInterface.ReadMemory<float>(num6);
						if (!float.IsNaN(item.ExpectedFloat) && Math.Abs(num7 - item.ExpectedFloat) < num)
						{
							int num8 = Math.Abs(num5);
							if (num8 < num3 || (num8 == num3 && num5 > num2))
							{
								num3 = num8;
								num2 = num5;
								f = num7;
								value = num6;
							}
						}
						continue;
					}
					int num9 = MemoryInterface.ReadMemory<int>(num6);
					if (num9 == item.Expected)
					{
						int num10 = Math.Abs(num5);
						if (num10 < num3 || (num10 == num3 && num5 > num2))
						{
							num3 = num10;
							num2 = num5;
							num4 = num9;
							value = num6;
						}
					}
				}
				catch
				{
				}
			}
			if (num2 != 0)
			{
				string value2;
				if (item.IsFloat)
				{
					value2 = (float.IsNaN(f) ? item.ActualFloat.ToString("F6") : f.ToString("F6"));
				}
				else
				{
					value2 = ((num4 == int.MinValue) ? "N/A" : num4.ToString());
				}
				long offset = item.Offset;
				long value3 = offset + num2;
				Logger.Log(LogLevel.Debug, "MemoryManager:RunExtendedOffsetShiftAnalysis", $"Field {item.Name} realigns at {num2:+#;-#;0} bytes (bakedOffset=0x{offset:X}, driftedOffset=0x{value3:X}, matchedAddr=0x{value:X}, matchedValue={value2})");
				driftPairs.Add((num2, item.Name.EndsWith("_VR"), item.Name));
			}
			else
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:RunExtendedOffsetShiftAnalysis", "Field " + item.Name + " does not realign within ±40 bytes");
			}
		}
		if (driftPairs.Count == 0)
		{
			Logger.Log(LogLevel.Warning, "MemoryManager:RunExtendedOffsetShiftAnalysis", "No realignment detected — drift pattern is random. Offset incorrect.");
			return;
		}
		BoundaryFieldResult boundaryFieldResult = (from boundaryFieldResult2 in list
			where driftPairs.Any(((int Delta, bool IsVR, string Name) dp) => dp.Name == boundaryFieldResult2.Name)
			orderby boundaryFieldResult2.Address
			select boundaryFieldResult2).FirstOrDefault();
		if (boundaryFieldResult != null)
		{
			Logger.Log(LogLevel.Warning, "MemoryManager:RunExtendedOffsetShiftAnalysis", "PC drift block begins at: " + boundaryFieldResult.Name);
		}
		List<IGrouping<int, (int, bool, string)>> list2 = (from d in driftPairs
			group d by d.Delta into g
			orderby g.Key
			select g).ToList();
		if (list2.Count == 1)
		{
			IGrouping<int, (int, bool, string)> grouping = list2[0];
			if (grouping.Count() >= 2)
			{
				int key = grouping.Key;
				Logger.Log(LogLevel.Warning, "MemoryManager:RunExtendedOffsetShiftAnalysis", $"Consistent single-cluster drift detected (+{key} bytes) with {grouping.Count()} supporting fields.");
				if (boundaryFieldResult != null && key == OffsetShift)
				{
					OffsetShiftPointer = boundaryFieldResult.Offset;
					OffsetShiftPointerChanged = true;
					Logger.Log(LogLevel.Warning, "MemoryManager:RunExtendedOffsetShiftAnalysis", $"OffsetShiftPointer set to 0x{OffsetShiftPointer:X} from anchor field {boundaryFieldResult.Name} (delta matches OffsetShift={OffsetShift:+#;-#;0}).");
				}
				else
				{
					Logger.Log(LogLevel.Warning, "MemoryManager:RunExtendedOffsetShiftAnalysis", $"Drift delta {key:+#;-#;0} does not match OffsetShift ({OffsetShift:+#;-#;0}) or no valid anchor field; no pointer shift applied.");
				}
			}
			else
			{
				Logger.Log(LogLevel.Warning, "MemoryManager:RunExtendedOffsetShiftAnalysis", "Single cluster has insufficient support. Offset incorrect.");
			}
		}
		else if (list2.Count == 2)
		{
			IGrouping<int, (int, bool, string)> grouping2 = list2[0];
			IGrouping<int, (int, bool, string)> grouping3 = list2[1];
			int key2 = grouping2.Key;
			int key3 = grouping3.Key;
			if (grouping2.Count() < 2 || grouping3.Count() < 2)
			{
				Logger.Log(LogLevel.Warning, "MemoryManager:RunExtendedOffsetShiftAnalysis", "Double cluster detected but insufficient support. Offset incorrect.");
				return;
			}
			if (grouping2.Any(((int Delta, bool IsVR, string Name) x) => x.IsVR))
			{
				Logger.Log(LogLevel.Warning, "MemoryManager:RunExtendedOffsetShiftAnalysis", "Lower-delta cluster contains VR fields — inconsistent double split.");
				return;
			}
			if (grouping3.Any(((int Delta, bool IsVR, string Name) x) => !x.IsVR))
			{
				Logger.Log(LogLevel.Warning, "MemoryManager:RunExtendedOffsetShiftAnalysis", "Higher-delta cluster contains PC fields — inconsistent double split.");
				return;
			}
			Logger.Log(LogLevel.Warning, "MemoryManager:RunExtendedOffsetShiftAnalysis", $"Valid double-split drift detected (PC +{key2}, VR +{key3}).");
		}
		else
		{
			Logger.Log(LogLevel.Warning, "MemoryManager:RunExtendedOffsetShiftAnalysis", "More than two drift clusters detected — inconsistent drift. Offset incorrect.");
		}
	}

	private void BoundaryTest(bool logResult = false)
	{
		if (Model.isMSFS2024)
		{
			BoundaryTest2024(logResult);
		}
		else
		{
			BoundaryTest2020(logResult);
		}
	}

	private void BoundaryTest2020(bool logResult = false)
	{
		float num = 0.1f;
		if (addrTLOD > 0 && Math.Abs(GetTLOD_PC() - configTLODPC) < num && Math.Abs(GetTLOD_VR() - configTLODVR) < num && Math.Abs(GetOLOD_PC() - configOLODPC) < num && Math.Abs(GetOLOD_VR() - configOLODVR) < num && MemoryInterface.ReadMemory<int>(addrTLOD + OffsetPointerAnsio) == configAniso && MemoryInterface.ReadMemory<int>(addrTLOD + OffsetPointerWaterWaves) == configWaterWaves)
		{
			allowMemoryWrites = true;
		}
		if (logResult && (Model.LogPlus || !allowMemoryWrites))
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Address TLOD: 0x{addrTLOD:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Address OLOD: 0x{addrOLOD:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Address TLOD VR: 0x{addrTLOD_VR:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Address OLOD VR: 0x{addrOLOD_VR:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Address Aniso Filter: 0x{addrTLOD + OffsetPointerAnsio:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Address Water Waves: 0x{addrTLOD + OffsetPointerWaterWaves:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"TLOD PC: {GetTLOD_PC()}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Config TLOD: {configTLODPC}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"TLOD VR: {GetTLOD_VR()}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Config TLOD VR: {configTLODVR}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"OLOD PC: {GetOLOD_PC()}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Config OLOD: {configOLODPC}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"OLOD VR: {GetOLOD_VR()}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Config OLOD VR: {configOLODVR}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Aniso Filter: {MemoryInterface.ReadMemory<int>(addrTLOD + OffsetPointerAnsio)}X");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Config Aniso: {configAniso}X");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Water Waves: {MemoryInterface.ReadMemory<int>(addrTLOD + OffsetPointerWaterWaves)}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2020", $"Config Water Waves: {configWaterWaves}");
		}
	}

	private void BoundaryTest2024(bool logResult = false)
	{
		float num = 0.1f;
		if (addrTLOD > 0 && Math.Abs(GetTLOD_PC() - configTLODPC) < num && Math.Abs(GetTLOD_VR() - configTLODVR) < num && Math.Abs(GetOLOD_PC() - configOLODPC) < num && Math.Abs(GetOLOD_VR() - configOLODVR) < num && MemoryInterface.ReadMemory<int>(addrAnsio) == configAniso && MemoryInterface.ReadMemory<int>(addrCubeMap) == configCubeMap && MemoryInterface.ReadMemory<int>(addrWaterWaves) == configWaterWaves)
		{
			allowMemoryWrites = true;
		}
		if (logResult && (Model.LogPlus || !allowMemoryWrites))
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Address TLOD: 0x{addrTLOD:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Address OLOD: 0x{addrOLOD:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Address TLOD VR: 0x{addrTLOD_VR:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Address OLOD VR: 0x{addrOLOD_VR:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Address Aniso Filter: 0x{addrAnsio:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Address Cubemap Reflections: 0x{addrCubeMap:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Address Water Waves: 0x{addrWaterWaves:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"TLOD PC: {GetTLOD_PC()}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Config TLOD: {configTLODPC}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"TLOD VR: {GetTLOD_VR()}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Config TLOD VR: {configTLODVR}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"OLOD PC: {GetOLOD_PC()}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Config OLOD: {configOLODPC}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"OLOD VR: {GetOLOD_VR()}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Config OLOD VR: {configOLODVR}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Aniso Filter: {MemoryInterface.ReadMemory<int>(addrAnsio)}X");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Config Aniso: {configAniso}X");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Cubemap Reflections: {MemoryInterface.ReadMemory<int>(addrCubeMap)}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Config Cubemap: {configCubeMap}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Water Waves: {MemoryInterface.ReadMemory<int>(addrWaterWaves)}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTest2024", $"Config Water Waves: {configWaterWaves}");
		}
	}

	private void BoundaryTestSecondary(bool logResult = false)
	{
		if (addrTLOD > 0 && GetIntSetting(addrOSTPreCaching) == Model.DefaultOSTPCache && GetBoolSetting(addrTShadowsEnabled) == Model.DefaultTShadowsEnabled && GetBoolSetting(addrRTShadows) == Model.DefaultRTShadows && GetBoolSetting(addrDispMap) == Model.DefaultDispMap && GetIntSetting(addrTerrainShadows) == Model.DefaultTShadows && GetIntSetting(addrBuildings) == Model.DefaultBldings && GetIntSetting(addrTrees) == Model.DefaultTrees && GetIntSetting(addrPlants) == Model.DefaultPlants && GetIntSetting(addrGrass) == Model.DefaultGrass && GetIntSetting(addrPkdAcQ) == Model.DefaultPkdAcQ && GetIntSetting(addrAcTrafQ) == Model.DefaultAcTrafQ && GetIntSetting(addrOSTPreCaching + OffsetVR) == Model.DefaultOSTPCache_VR && GetBoolSetting(addrTShadowsEnabled + OffsetVR) == Model.DefaultTShadowsEnabled_VR && GetBoolSetting(addrRTShadows + OffsetVR) == Model.DefaultRTShadows_VR && GetBoolSetting(addrDispMap + OffsetVR) == Model.DefaultDispMap_VR && GetIntSetting(addrTerrainShadows + OffsetVR) == Model.DefaultTShadows_VR && GetIntSetting(addrBuildings + OffsetVR) == Model.DefaultBldings_VR && GetIntSetting(addrTrees + OffsetVR) == Model.DefaultTrees_VR && GetIntSetting(addrPlants + OffsetVR) == Model.DefaultPlants_VR && GetIntSetting(addrGrass + OffsetVR) == Model.DefaultGrass_VR && GetIntSetting(addrPkdAcQ + OffsetVR) == Model.DefaultPkdAcQ_VR && GetIntSetting(addrAcTrafQ + OffsetVR) == Model.DefaultAcTrafQ_VR)
		{
			Model.SettingsReduceEnabled = true;
		}
		if (logResult && (Model.LogPlus || !Model.SettingsReduceEnabled))
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Address OSTPreCaching: 0x{addrOSTPreCaching:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Address TShadowsEnabled: 0x{addrTShadowsEnabled:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Address RTShadows: 0x{addrRTShadows:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Address DispMap: 0x{addrDispMap:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Address TerrainShadows: 0x{addrTerrainShadows:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Address Buildings: 0x{addrBuildings:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Address Trees: 0x{addrTrees:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Address Plants: 0x{addrPlants:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Address Grass: 0x{addrGrass:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Address PkdAcQ: 0x{addrPkdAcQ:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Address AcTrafQ: 0x{addrAcTrafQ:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"Offset VR: 0x{OffsetVR:X}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "Setting: MSFS / Config");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "OSTPreCaching: " + ServiceModel.SettingQualityText(GetIntSetting(addrOSTPreCaching)) + " / " + ServiceModel.SettingQualityText(Model.DefaultOSTPCache));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"TShadowsEnabled: {GetBoolSetting(addrTShadowsEnabled)} / {Model.DefaultTShadowsEnabled}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"RTShadows: {GetBoolSetting(addrRTShadows)} / {Model.DefaultRTShadows}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"DispMap: {GetBoolSetting(addrDispMap)} / {Model.DefaultDispMap}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"TerrainShadows: {GetIntSetting(addrTerrainShadows)} / {Model.DefaultTShadows}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "Buildings: " + ServiceModel.SettingQualityText(GetIntSetting(addrBuildings)) + " / " + ServiceModel.SettingQualityText(Model.DefaultBldings));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "Trees: " + ServiceModel.SettingQualityText(GetIntSetting(addrTrees)) + " / " + ServiceModel.SettingQualityText(Model.DefaultTrees));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "Plants: " + ServiceModel.SettingQualityText(GetIntSetting(addrPlants)) + " / " + ServiceModel.SettingQualityText(Model.DefaultPlants));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "Grass: " + ServiceModel.SettingQualityText(GetIntSetting(addrGrass)) + " / " + ServiceModel.SettingQualityText(Model.DefaultGrass));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "PkdAcQ: " + ServiceModel.SettingQualityText(GetIntSetting(addrPkdAcQ)) + " / " + ServiceModel.SettingQualityText(Model.DefaultPkdAcQ));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "AcTrafQ: " + ServiceModel.SettingQualityText(GetIntSetting(addrAcTrafQ)) + " / " + ServiceModel.SettingQualityText(Model.DefaultAcTrafQ));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "OSTPreCaching VR: " + ServiceModel.SettingQualityText(GetIntSetting(addrOSTPreCaching + OffsetVR)) + " / " + ServiceModel.SettingQualityText(Model.DefaultOSTPCache_VR));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"TShadowsEnabled VR: {GetBoolSetting(addrTShadowsEnabled + OffsetVR)} / {Model.DefaultTShadowsEnabled_VR}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"RTShadows VR: {GetBoolSetting(addrRTShadows + OffsetVR)} / {Model.DefaultRTShadows_VR}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"DispMap VR: {GetBoolSetting(addrDispMap + OffsetVR)} / {Model.DefaultDispMap_VR}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", $"TerrainShadows VR: {GetIntSetting(addrTerrainShadows + OffsetVR)} / {Model.DefaultTShadows_VR}");
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "Buildings VR: " + ServiceModel.SettingQualityText(GetIntSetting(addrBuildings + OffsetVR)) + " / " + ServiceModel.SettingQualityText(Model.DefaultBldings_VR));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "Trees VR: " + ServiceModel.SettingQualityText(GetIntSetting(addrTrees + OffsetVR)) + " / " + ServiceModel.SettingQualityText(Model.DefaultTrees_VR));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "Plants VR: " + ServiceModel.SettingQualityText(GetIntSetting(addrPlants + OffsetVR)) + " / " + ServiceModel.SettingQualityText(Model.DefaultPlants_VR));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "Grass VR: " + ServiceModel.SettingQualityText(GetIntSetting(addrGrass + OffsetVR)) + " / " + ServiceModel.SettingQualityText(Model.DefaultGrass_VR));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "PkdAcQ VR: " + ServiceModel.SettingQualityText(GetIntSetting(addrPkdAcQ + OffsetVR)) + " / " + ServiceModel.SettingQualityText(Model.DefaultPkdAcQ_VR));
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "AcTrafQ VR: " + ServiceModel.SettingQualityText(GetIntSetting(addrAcTrafQ + OffsetVR)) + " / " + ServiceModel.SettingQualityText(Model.DefaultAcTrafQ_VR));
		}
		if (Model.SettingsReduceEnabled)
		{
			Logger.Log(LogLevel.Debug, "MemoryManager:BoundaryTestSecondary", "Reduction setting boundary tests passed - reduction setting memory writes enabled");
		}
	}

	private void GetAddresses2020()
	{
		addrTLOD = MemoryInterface.ReadMemory<long>(Model.moduleBaseAddress + Model.OffsetModuleBase) + OffsetPointerMain;
		if (addrTLOD > 0)
		{
			addrTLOD_VR = MemoryInterface.ReadMemory<long>(addrTLOD) + OffsetPointerTlodVr;
			addrTLOD = MemoryInterface.ReadMemory<long>(addrTLOD) + OffsetPointerTlod;
			addrOLOD_VR = addrTLOD_VR + OffsetPointerOlod;
			addrOLOD = addrTLOD + OffsetPointerOlod;
			addrCloudQ = addrTLOD + OffsetPointerCloudQ;
			addrCloudQ_VR = addrCloudQ + OffsetPointerCloudQVr;
			addrVrMode = addrTLOD - OffsetPointerVrMode;
			if (allowMemoryWrites)
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2020", $"Address TLOD: 0x{addrTLOD:X} / {addrTLOD}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2020", $"Address OLOD: 0x{addrOLOD:X} / {addrOLOD}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2020", $"Address CloudQ: 0x{addrCloudQ:X} / {addrCloudQ}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2020", $"Address TLOD VR: 0x{addrTLOD_VR:X} / {addrTLOD_VR}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2020", $"Address OLOD VR: 0x{addrOLOD_VR:X} / {addrOLOD_VR}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2020", $"Address CloudQ VR: 0x{addrCloudQ_VR:X} / {addrCloudQ_VR}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2020", $"Address VrMode: 0x{addrVrMode:X} / {addrVrMode}");
			}
		}
	}

	private bool GetAddresses2024(long BaseOffset)
	{
		try
		{
			long num = Model.moduleBaseAddress + BaseOffset;
			addrTLOD = num + OffsetPointerTlod;
			addrOLOD = num + OffsetPointerOlod;
			addrTLOD_VR = addrTLOD + OffsetVR;
			addrOLOD_VR = addrOLOD + OffsetVR;
			addrVrMode = num + OffsetPointerVrMode;
			addrCloudQ = num + OffsetPointerCloudQ;
			addrCloudQ_VR = addrCloudQ + OffsetVR;
			addrDynSet = num + OffsetPointerDynSet;
			addrDynSetVR = num + OffsetPointerDynSetVr;
			addrDynSetFrameRateTarget = num + OffsetPointerDynSetFrameRateTarget;
			addrDynSetFrameRateTargetVR = num + OffsetPointerDynSetFrameRateTargetVr;
			addrOSTPreCaching = num + OffsetPointerOSTPreCaching;
			addrTerrainShadows = num + OffsetPointerTerrainShadows;
			addrRTShadows = num + OffsetPointerRTShadows;
			addrTShadowsEnabled = num + OffsetPointerTShadowsEnabled;
			addrDispMap = num + OffsetPointerDispMap;
			addrBuildings = num + OffsetPointerBuildings;
			addrTrees = num + OffsetPointerTrees;
			addrPlants = num + OffsetPointerPlants;
			addrGrass = num + OffsetPointerGrass;
			addrPkdAcQ = num + OffsetPointerPkdAcQ;
			addrAcTrafQ = num + OffsetPointerAcTrafQ;
			addrAnsio = num + OffsetPointerAnsio;
			addrCubeMap = num + OffsetPointerCubeMap;
			addrWaterWaves = num + OffsetPointerWaterWaves;
			addrSharpAmt = num + OffsetPointerSharpAmt;
			addrSharpAmtVR = num + OffsetPointerSharpAmtVR;
			addrExposureBias = num + OffsetPointerExposureBias;
			addrExposureBiasVR = num + OffsetPointerExposureBiasVR;
			addrFoveatedRenderingScale = num + OffsetPointerFoveatedRenderingScale;
			if (Model.LogPlus && allowMemoryWrites && Model.MemoryCompatTestComplete)
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2024", $"Address TLOD: 0x{addrTLOD:X}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2024", $"Address OLOD: 0x{addrOLOD:X}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2024", $"Address CloudQ: 0x{addrCloudQ:X}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2024", $"Address TLOD VR: 0x{addrTLOD_VR:X}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2024", $"Address OLOD VR: 0x{addrOLOD_VR:X}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2024", $"Address CloudQ VR: 0x{addrCloudQ_VR:X}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2024", $"Address VrMode: 0x{addrVrMode:X}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2024", $"Address DynSet: 0x{addrDynSet:X}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2024", $"Address DynSetVR: 0x{addrDynSetVR:X}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2024", $"Address DynSetFrameRateTarget: 0x{addrDynSetFrameRateTarget:X}");
				Logger.Log(LogLevel.Debug, "MemoryManager:GetAddresses2024", $"Address DynSetFrameRateTargetVR: 0x{addrDynSetFrameRateTargetVR:X}");
			}
			return true;
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "GetAddresses2024", "Error: " + ex.Message);
			return false;
		}
	}

	private bool TLODReadWritability()
	{
		try
		{
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:TLODReadWritability", $"Verifying read/write access for TLOD at address 0x{addrTLOD:X}");
			}
			if (GetTLOD_PC() != configTLODPC)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:TLODReadWritability", $"TLOD value mismatch with config file value. Expected: {configTLODPC}, Actual: {GetTLOD_PC()}. App disabled and saved memory offsets removed and reset");
				if (Model.ConfigurationFile.SettingExists("offsetModuleBase"))
				{
					Model.ConfigurationFile.RemoveSetting("offsetModuleBase");
				}
				if (Model.ConfigurationFile.SettingExists("offsetModuleBase " + Model.DetectedSimVersion))
				{
					Model.ConfigurationFile.RemoveSetting("offsetModuleBase " + Model.DetectedSimVersion);
				}
				if (Model.ConfigurationFile.SettingExists("LastCompatTestPassConfig"))
				{
					Model.ConfigurationFile.RemoveSetting("LastCompatTestPassConfig");
				}
				if (Model.ConfigurationFile.SettingExists("LastCompatTest2PassConfig"))
				{
					Model.ConfigurationFile.RemoveSetting("LastCompatTest2PassConfig");
				}
				allowMemoryWrites = false;
				return false;
			}
			SetTLOD_PC(configTLODPC + 1f);
			if (GetTLOD_PC() != configTLODPC + 1f)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:TLODReadWritability", $"TLOD value mismatch after verify write. Expected: {configTLODPC + 1f}, Actual: {GetTLOD_PC()}. App disabled");
				allowMemoryWrites = false;
				return false;
			}
			SetTLOD_PC(configTLODPC);
			Logger.Log(LogLevel.Debug, "MemoryManager:TLODReadWritability", "Read/write verification for TLOD successful");
			return true;
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:TLODReadWritability", "Exception during read/write verification: " + ex.Message);
			allowMemoryWrites = false;
			return false;
		}
	}

	private void VerifyAddresses()
	{
		VerifyAddress(addrTLOD, "TLOD", "float");
		VerifyAddress(addrOLOD, "OLOD", "float");
		VerifyAddress(addrTLOD_VR, "TLOD VR", "float");
		VerifyAddress(addrOLOD_VR, "OLOD VR", "float");
		Logger.Log(LogLevel.Debug, "Address Verification", "CloudQ Value: " + ServiceModel.SettingQualityText(GetCloudQ_PC()));
		Logger.Log(LogLevel.Debug, "Address Verification", "CloudQ VR Value: " + ServiceModel.SettingQualityText(GetCloudQ_VR()));
		VerifyAddress(addrVrMode, "VR Mode", "bool");
		if (Model.isMSFS2024)
		{
			VerifyAddress(addrDynSet, "Dynamic Setting", "bool");
			VerifyAddress(addrDynSetVR, "Dynamic Setting VR", "bool");
			VerifyAddress(addrDynSetFrameRateTarget, "Dynamic Setting Frame Rate Target", "int");
			VerifyAddress(addrDynSetFrameRateTargetVR, "Dynamic Setting Frame Rate Target VR", "int");
		}
	}

	private void VerifyAddress(long address, string propertyName, string dataType)
	{
		try
		{
			switch (dataType)
			{
			case "float":
			{
				float value3 = (float)Math.Round(MemoryInterface.ReadMemory<float>(address) * 100f);
				Logger.Log(LogLevel.Debug, "Address Verification", $"{propertyName} Value: {value3}");
				break;
			}
			case "int":
			{
				int value2 = MemoryInterface.ReadMemory<int>(address);
				Logger.Log(LogLevel.Debug, "Address Verification", $"{propertyName} Value: {value2}");
				break;
			}
			case "bool":
			{
				bool value = MemoryInterface.ReadMemory<byte>(address) == 1;
				Logger.Log(LogLevel.Debug, "Address Verification", $"{propertyName} Value: {value}");
				break;
			}
			default:
				Logger.Log(LogLevel.Debug, "Address Verification", propertyName + " Unknown data type");
				break;
			}
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "Address Verification", "Failed to read " + propertyName + ": " + ex.Message);
		}
	}

	private void GetActiveDXVersion()
	{
		if (Model.isMSFS2024)
		{
			isDX12 = true;
			if (!Model.MemoryCompatTestComplete)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:GetActiveDXVersion", (Model.DetectedMSFSConfig2024.Contains("8wekyb3d8bbwe") ? "MS Store" : "Steam") + " MSFS2024 version " + Model.DetectedSimVersion + " detected - DX12");
			}
			return;
		}
		StreamReader streamReader = new StreamReader(Model.DetectedMSFSConfig2020);
		if (streamReader.ReadToEnd().Contains("PreferD3D12 1"))
		{
			isDX12 = true;
		}
		streamReader.Close();
		if (!Model.MemoryCompatTestComplete)
		{
			Logger.Log(LogLevel.Information, "MemoryManager:GetActiveDXVersion", (Model.DetectedMSFSConfig2020.Contains("8wekyb3d8bbwe") ? "MS Store" : "Steam") + " " + Model.DetectedSimVersion + "  MSFS2020 version detected - " + (isDX12 ? "DX12" : "DX11"));
		}
	}

	public bool IsVrModeActive()
	{
		try
		{
			return MemoryInterface.ReadMemory<int>(addrVrMode) == 1;
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:IsVrModeActive", $"Exception {ex}: {ex.Message}");
		}
		return false;
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	public bool IsActiveWindowMSFS()
	{
		StringBuilder stringBuilder = new StringBuilder(256);
		if (GetWindowText(GetForegroundWindow(), stringBuilder, 256) > 0)
		{
			string text = stringBuilder.ToString();
			if (text.Length > 26 && text.Substring(0, 26) == "Microsoft Flight Simulator")
			{
				return true;
			}
		}
		return false;
	}

	public bool IsDX12()
	{
		return isDX12;
	}

	public void IsFgModeEnabled(bool forceCheck = false)
	{
		if (!forceCheck && !(File.GetLastWriteTime(OptSetPath) != lastOptSetModify))
		{
			return;
		}
		if (Model.isMSFS2024)
		{
			string text = Convert.ToString(OptSetParser.GetOrModifyItem(OptSetPath, "FrameGeneration", 1));
			if (Model.FgModeEnabled = text == "DLSSG")
			{
				Model.FgModeMultiplier = Convert.ToInt32(OptSetParser.GetOrModifyItem(OptSetPath, "NBFramesToGenerate", 1)) + 1;
			}
			Model.FgModeFSREnabled = text == "FSRFG";
		}
		else
		{
			Model.FgModeEnabled = Convert.ToInt32(OptSetParser.GetOrModifyItem(OptSetPath, "DLSSG", 1)) == 1;
		}
		lastOptSetModify = File.GetLastWriteTime(OptSetPath);
	}

	public string ScanMemory()
	{
		string text = null;
		if (isfirstSessionRan)
		{
			for (int i = 0; i < memoryMap.Length; i++)
			{
				memoryMap[i] = GetByteSetting(addrDynSet + i - 90);
			}
		}
		else
		{
			for (int j = 0; j < memoryMap.Length; j++)
			{
				byte byteSetting = GetByteSetting(addrDynSet + j - 90);
				if (memoryMap[j] != byteSetting)
				{
					text += $"0x{j + 812 - 90:X} ";
					memoryMap[j] = byteSetting;
				}
			}
		}
		isfirstSessionRan = false;
		return text;
	}

	public byte GetByteSetting(long address)
	{
		try
		{
			return MemoryInterface.ReadMemory<byte>(address);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetByteSetting", $"Exception {ex}: {ex.Message}");
		}
		return 0;
	}

	public bool GetBoolSetting(long address)
	{
		try
		{
			return MemoryInterface.ReadMemory<byte>(address) == 1;
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetBoolSetting", $"Exception {ex}: {ex.Message}");
		}
		return false;
	}

	public float GetFloatSetting(long address)
	{
		try
		{
			return MemoryInterface.ReadMemory<float>(address);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetFloatSetting", $"Exception {ex}: {ex.Message}");
		}
		return 0f;
	}

	public int GetIntSetting(long address)
	{
		try
		{
			return MemoryInterface.ReadMemory<int>(address);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetIntSetting", $"Exception {ex}: {ex.Message}");
		}
		return -2;
	}

	public int GetCloudQ(bool VR = false)
	{
		MemoryManager memoryManager = this;
		long address;
		if (Model.isMSFS2024)
		{
			address = addrCloudQ + (VR ? OffsetVR : 0);
		}
		else
		{
			address = (VR ? addrCloudQ_VR : addrCloudQ);
		}
		return memoryManager.GetIntSetting(address);
	}

	public int GetOSTPreCaching(bool VR = false)
	{
		return GetIntSetting(addrOSTPreCaching + (VR ? OffsetVR : 0));
	}

	public int GetTerrainShadows(bool VR = false)
	{
		return GetIntSetting(addrTerrainShadows + (VR ? OffsetVR : 0));
	}

	public bool GetRTShadows(bool VR = false)
	{
		return GetBoolSetting(addrRTShadows + (VR ? OffsetVR : 0));
	}

	public bool GetTShadowsEnabled(bool VR = false)
	{
		return GetBoolSetting(addrTShadowsEnabled + (VR ? OffsetVR : 0));
	}

	public bool GetDispMap(bool VR = false)
	{
		return GetBoolSetting(addrDispMap + (VR ? OffsetVR : 0));
	}

	public int GetBuildings(bool VR = false)
	{
		return GetIntSetting(addrBuildings + (VR ? OffsetVR : 0));
	}

	public int GetTrees(bool VR = false)
	{
		return GetIntSetting(addrTrees + (VR ? OffsetVR : 0));
	}

	public int GetPlants(bool VR = false)
	{
		return GetIntSetting(addrPlants + (VR ? OffsetVR : 0));
	}

	public int GetGrass(bool VR = false)
	{
		return GetIntSetting(addrGrass + (VR ? OffsetVR : 0));
	}

	public int GetPkdAcQ(bool VR = false)
	{
		return GetIntSetting(addrPkdAcQ + (VR ? OffsetVR : 0));
	}

	public int GetAcTrafQ(bool VR = false)
	{
		return GetIntSetting(addrAcTrafQ + (VR ? OffsetVR : 0));
	}

	public string CompareSettingsWithModel(bool correctSettings = false)
	{
		List<string> logItems = new List<string>();
		List<string> userItems = new List<string>();
		string text = (Model.VrModeActive ? " VR" : " PC");
		float num = (Model.VrModeActive ? Model.MemoryAccess.GetTLOD_VR() : Model.MemoryAccess.GetTLOD_PC());
		float num2 = (Model.VrModeActive ? Model.MemoryAccess.GetTLOD_PC() : Model.MemoryAccess.GetTLOD_VR());
		float num3 = (Model.VrModeActive ? Model.DefaultTLOD : Model.DefaultTLOD_VR);
		if (Model.tlod != num)
		{
			logItems.Add($"TLOD{text} mismatch: AutoFPS={Model.tlod}, MSFS={num}");
			if (correctSettings)
			{
				userItems.Add($"TLOD{text} {num} → {Model.tlod}");
				if (Model.VrModeActive)
				{
					Model.MemoryAccess.SetTLOD_VR(Model.tlod);
				}
				else
				{
					Model.MemoryAccess.SetTLOD_PC(Model.tlod);
				}
			}
			else
			{
				userItems.Add("TLOD" + text);
			}
		}
		if (num2 != num3)
		{
			string text2 = (Model.VrModeActive ? " PC" : " VR");
			logItems.Add($"TLOD{text2} mismatch: AutoFPS={num3}, MSFS={num2}");
			if (correctSettings)
			{
				userItems.Add($"TLOD{text2} {num2} → {num3}");
				if (Model.VrModeActive)
				{
					Model.MemoryAccess.SetTLOD_PC(Model.DefaultTLOD);
				}
				else
				{
					Model.MemoryAccess.SetTLOD_VR(Model.DefaultTLOD_VR);
				}
			}
			else
			{
				userItems.Add("TLOD" + text2);
			}
		}
		float num4 = (Model.VrModeActive ? Model.MemoryAccess.GetOLOD_VR() : Model.MemoryAccess.GetOLOD_PC());
		float num5 = (Model.VrModeActive ? Model.MemoryAccess.GetOLOD_PC() : Model.MemoryAccess.GetOLOD_VR());
		float num6 = (Model.VrModeActive ? Model.DefaultOLOD : Model.DefaultOLOD_VR);
		if (Model.olod != num4)
		{
			logItems.Add($"OLOD{text} mismatch: AutoFPS={Model.olod}, MSFS={num4}");
			if (correctSettings)
			{
				userItems.Add($"OLOD{text} {num4} → {Model.olod}");
				if (Model.VrModeActive)
				{
					Model.MemoryAccess.SetOLOD_VR(Model.olod);
				}
				else
				{
					Model.MemoryAccess.SetOLOD_PC(Model.olod);
				}
			}
			else
			{
				userItems.Add("OLOD" + text);
			}
		}
		if (num5 != num6)
		{
			string text3 = (Model.VrModeActive ? " PC" : " VR");
			logItems.Add($"OLOD{text3} mismatch: AutoFPS={num6}, MSFS={num5}");
			if (correctSettings)
			{
				userItems.Add($"OLOD{text3} {num5} → {num6}");
				if (Model.VrModeActive)
				{
					Model.MemoryAccess.SetOLOD_PC(Model.DefaultOLOD);
				}
				else
				{
					Model.MemoryAccess.SetOLOD_VR(Model.DefaultOLOD_VR);
				}
			}
			else
			{
				userItems.Add("OLOD" + text3);
			}
		}
		CheckQuality("Cloud Quality", Model.cloudQ, Model.MemoryAccess.GetCloudQ, Model.MemoryAccess.SetCloudQ, vr: false);
		CheckQuality("Cloud Quality", Model.cloudQ_VR, Model.MemoryAccess.GetCloudQ, Model.MemoryAccess.SetCloudQ, vr: true);
		if (Model.isMSFS2024)
		{
			CheckQuality("Trees", Model.Trees, Model.MemoryAccess.GetTrees, Model.MemoryAccess.SetTrees, vr: false);
			CheckQuality("Trees", Model.Trees_VR, Model.MemoryAccess.GetTrees, Model.MemoryAccess.SetTrees, vr: true);
			CheckBool("Raytraced Shadows", Model.RTShadows, Model.MemoryAccess.GetRTShadows, Model.MemoryAccess.SetRTShadows, vr: false);
			CheckBool("Raytraced Shadows", Model.RTShadows_VR, Model.MemoryAccess.GetRTShadows, Model.MemoryAccess.SetRTShadows, vr: true);
			CheckBool("Terrain Shadows Enabled", Model.TShadowsEnabled, Model.MemoryAccess.GetTShadowsEnabled, Model.MemoryAccess.SetTShadowsEnabled, vr: false);
			CheckBool("Terrain Shadows Enabled", Model.TShadowsEnabled_VR, Model.MemoryAccess.GetTShadowsEnabled, Model.MemoryAccess.SetTShadowsEnabled, vr: true);
			CheckTerrainShadows("Terrain Shadows", Model.TShadows, Model.MemoryAccess.GetTerrainShadows, Model.MemoryAccess.SetTerrainShadows, vr: false);
			CheckTerrainShadows("Terrain Shadows", Model.TShadows_VR, Model.MemoryAccess.GetTerrainShadows, Model.MemoryAccess.SetTerrainShadows, vr: true);
			CheckQuality("Buildings", Model.Bldings, Model.MemoryAccess.GetBuildings, Model.MemoryAccess.SetBuildings, vr: false);
			CheckQuality("Buildings", Model.Bldings_VR, Model.MemoryAccess.GetBuildings, Model.MemoryAccess.SetBuildings, vr: true);
			CheckBool("Displacement Mapping", Model.DispMap, Model.MemoryAccess.GetDispMap, Model.MemoryAccess.SetDispMap, vr: false);
			CheckBool("Displacement Mapping", Model.DispMap_VR, Model.MemoryAccess.GetDispMap, Model.MemoryAccess.SetDispMap, vr: true);
			CheckQuality("Off Screen Terrain Pre-Caching", Model.OSTPCache, Model.MemoryAccess.GetOSTPreCaching, Model.MemoryAccess.SetOSTPreCaching, vr: false);
			CheckQuality("Off Screen Terrain Pre-Caching", Model.OSTPCache_VR, Model.MemoryAccess.GetOSTPreCaching, Model.MemoryAccess.SetOSTPreCaching, vr: true);
			CheckQuality("Plants", Model.Plants, Model.MemoryAccess.GetPlants, Model.MemoryAccess.SetPlants, vr: false);
			CheckQuality("Plants", Model.Plants_VR, Model.MemoryAccess.GetPlants, Model.MemoryAccess.SetPlants, vr: true);
			CheckQuality("Grass", Model.Grass, Model.MemoryAccess.GetGrass, Model.MemoryAccess.SetGrass, vr: false);
			CheckQuality("Grass", Model.Grass_VR, Model.MemoryAccess.GetGrass, Model.MemoryAccess.SetGrass, vr: true);
			CheckQuality("Parked Aircraft Quantity", Model.PkdAcQ, Model.MemoryAccess.GetPkdAcQ, Model.MemoryAccess.SetPkdAcQ, vr: false);
			CheckQuality("Parked Aircraft Quantity", Model.PkdAcQ_VR, Model.MemoryAccess.GetPkdAcQ, Model.MemoryAccess.SetPkdAcQ, vr: true);
			CheckQuality("Aircraft Traffic Quantity", Model.AcTrafQ, Model.MemoryAccess.GetAcTrafQ, Model.MemoryAccess.SetAcTrafQ, vr: false);
			CheckQuality("Aircraft Traffic Quantity", Model.AcTrafQ_VR, Model.MemoryAccess.GetAcTrafQ, Model.MemoryAccess.SetAcTrafQ, vr: true);
			if (!Model.UseExpertOptions)
			{
				CheckBool("Dynamic Setting", Model.DynSet, Model.MemoryAccess.GetDynSet, Model.MemoryAccess.SetDynSet, vr: false);
				CheckBool("Dynamic Setting", Model.DynSet_VR, Model.MemoryAccess.GetDynSet, Model.MemoryAccess.SetDynSet, vr: true);
			}
		}
		if (logItems.Count > 0)
		{
			string message = string.Join(", ", logItems);
			string result = string.Join(", ", userItems);
			Logger.Log(LogLevel.Warning, "MemoryManager:CompareSettings", message);
			return result;
		}
		return null;
		void CheckBool(string name, bool modelValue, Func<bool, bool> getter, Action<bool, bool> setter, bool vr)
		{
			bool flag = getter(vr);
			string value = (vr ? " VR" : " PC");
			string value2 = (modelValue ? "Enabled" : "Disabled");
			string value3 = (flag ? "Enabled" : "Disabled");
			if (modelValue != flag)
			{
				logItems.Add($"{name}{value} mismatch: AutoFPS={value2}, MSFS={value3}");
				if (correctSettings)
				{
					userItems.Add($"{name}{value} {value3} → {value2}");
					setter(modelValue, vr);
				}
				else
				{
					userItems.Add($"{name}{value} (MSFS={value3}, AutoFPS={value2})");
				}
			}
		}
		void CheckQuality(string name, int modelValue, Func<bool, int> getter, Action<int, bool> setter, bool vr)
		{
			int num7 = getter(vr);
			string text4 = (vr ? " VR" : " PC");
			if (modelValue != num7)
			{
				logItems.Add($"{name}{text4} mismatch: AutoFPS={ServiceModel.SettingQualityText(modelValue)}, MSFS={ServiceModel.SettingQualityText(num7)}");
				if (correctSettings)
				{
					userItems.Add($"{name}{text4} {ServiceModel.SettingQualityText(num7)} → {ServiceModel.SettingQualityText(modelValue)}");
					setter(modelValue, vr);
				}
				else
				{
					userItems.Add(name + text4);
				}
			}
		}
		void CheckTerrainShadows(string name, int modelValue, Func<bool, int> getter, Action<int, bool> setter, bool vr)
		{
			int num7 = getter(vr);
			string text4 = (vr ? " VR" : " PC");
			if (modelValue != num7)
			{
				logItems.Add($"{name}{text4} mismatch: AutoFPS={modelValue}, MSFS={num7}");
				if (correctSettings)
				{
					userItems.Add($"{name}{text4} {num7} → {modelValue}");
					setter(modelValue, vr);
				}
				else
				{
					userItems.Add(name + text4);
				}
			}
		}
	}

	public void SetCloudQ(int value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			long address;
			if (Model.isMSFS2024)
			{
				address = addrCloudQ + (VR ? OffsetVR : 0);
			}
			else
			{
				address = (VR ? addrCloudQ_VR : addrCloudQ);
			}
			SetIntSetting(address, value);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetCloudQ", ServiceModel.SettingQualityText(value) + (VR ? " VR" : ""));
			}
		}
	}

	public void SetOSTPreCaching(int value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			SetIntSetting(addrOSTPreCaching + (VR ? OffsetVR : 0), value);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetOSTPreCaching", ServiceModel.SettingQualityText(value) + (VR ? " VR" : ""));
			}
		}
	}

	public void SetTerrainShadows(int value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			SetIntSetting(addrTerrainShadows + (VR ? OffsetVR : 0), value, 128, 2048, 128);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetTerrainShadows", value + (VR ? " VR" : ""));
			}
		}
	}

	public void SetRTShadows(bool value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			SetBoolSetting(addrRTShadows + (VR ? OffsetVR : 0), value);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetRTShadows", value + (VR ? " VR" : ""));
			}
		}
	}

	public void SetTShadowsEnabled(bool value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			SetBoolSetting(addrTShadowsEnabled + (VR ? OffsetVR : 0), value);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetTShadowsEnabled", value + (VR ? " VR" : ""));
			}
		}
	}

	public void SetDispMap(bool value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			SetBoolSetting(addrDispMap + (VR ? OffsetVR : 0), value);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetDispMap", value + (VR ? " VR" : ""));
			}
		}
	}

	public void SetBuildings(int value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			SetIntSetting(addrBuildings + (VR ? OffsetVR : 0), value);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetBuildings", ServiceModel.SettingQualityText(value) + (VR ? " VR" : ""));
			}
		}
	}

	public void SetTrees(int value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			SetIntSetting(addrTrees + (VR ? OffsetVR : 0), value);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetTrees", ServiceModel.SettingQualityText(value) + (VR ? " VR" : ""));
			}
		}
	}

	public void SetPlants(int value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			SetIntSetting(addrPlants + (VR ? OffsetVR : 0), value, -1);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetPlants", ServiceModel.SettingQualityText(value) + (VR ? " VR" : ""));
			}
		}
	}

	public void SetGrass(int value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			SetIntSetting(addrGrass + (VR ? OffsetVR : 0), value, -1);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetGrass", ServiceModel.SettingQualityText(value) + (VR ? " VR" : ""));
			}
		}
	}

	public void SetPkdAcQ(int value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			SetIntSetting(addrPkdAcQ + (VR ? OffsetVR : 0), value, -1);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetPkdAcQ", ServiceModel.SettingQualityText(value) + (VR ? " VR" : ""));
			}
		}
	}

	public void SetAcTrafQ(int value, bool VR = false)
	{
		if (Model.SettingsReduceEnabled)
		{
			SetIntSetting(addrAcTrafQ + (VR ? OffsetVR : 0), value, -1);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetAcTrafQ", ServiceModel.SettingQualityText(value) + (VR ? " VR" : ""));
			}
		}
	}

	public void SetDynSet(bool value, bool VR = false)
	{
		if (VR)
		{
			SetDynSet_VR(value);
		}
		else
		{
			SetDynSet_PC(value);
		}
		if (Model.LogPlus)
		{
			Logger.Log(LogLevel.Information, "MemoryManager:SetDynSet", value + (VR ? " VR" : ""));
		}
	}

	public void SetDynSet_PC(bool value)
	{
		if (allowMemoryWrites)
		{
			try
			{
				byte b = (value ? ((byte)1) : ((byte)0));
				MemoryInterface.WriteMemory<byte>(addrDynSet, b);
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:SetDynSet", $"Exception {ex}: {ex.Message}");
			}
		}
	}

	public void SetDynSet_VR(bool value)
	{
		if (allowMemoryWrites)
		{
			try
			{
				byte b = (value ? ((byte)1) : ((byte)0));
				MemoryInterface.WriteMemory<byte>(addrDynSetVR, b);
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:SetDynSet_VR", $"Exception {ex}: {ex.Message}");
			}
		}
	}

	public void SetDynSetFrameRateTarget(int value, bool VR = false)
	{
		if (Model.isMSFS2024 && allowMemoryWrites)
		{
			SetIntSetting(addrDynSetFrameRateTarget + (VR ? OffsetVR : 0), value, 20, 120, 5);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetDynSetFrameRateTarget", value + (VR ? " VR" : ""));
			}
		}
	}

	public void SetDynSetFrameRateTargetVR(int value, bool VR = false)
	{
		if (Model.isMSFS2024 && allowMemoryWrites)
		{
			SetIntSetting(addrDynSetFrameRateTargetVR + (VR ? OffsetVR : 0), value, 20, 120, 5);
			if (Model.LogPlus)
			{
				Logger.Log(LogLevel.Information, "MemoryManager:SetDynSetFrameRateTargetVR", value + (VR ? " VR" : ""));
			}
		}
	}

	private bool SetIntSetting(long address, int value, int lowerBound = 0, int upperBound = 3, int divisor = 1)
	{
		if (address > 0 && allowMemoryWrites && value >= lowerBound && value <= upperBound && value % divisor == 0)
		{
			try
			{
				MemoryInterface.WriteMemory<int>(address, value);
				return true;
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:SetIntSetting", $"Exception {ex}: {ex.Message}");
			}
		}
		return false;
	}

	private bool SetBoolSetting(long address, bool value)
	{
		if (address > 0 && allowMemoryWrites)
		{
			try
			{
				byte b = (value ? ((byte)1) : ((byte)0));
				MemoryInterface.WriteMemory<byte>(address, b);
				return true;
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Error, "MemoryManager:SetBoolSetting", $"Exception {ex}: {ex.Message}");
			}
		}
		return false;
	}

	public float GetTLOD_PC()
	{
		try
		{
			return (float)Math.Round(MemoryInterface.ReadMemory<float>(addrTLOD) * 100f);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetTLOD", $"Exception {ex}: {ex.Message}");
		}
		return 0f;
	}

	public float GetTLOD_VR()
	{
		try
		{
			return (float)Math.Round(MemoryInterface.ReadMemory<float>(addrTLOD_VR) * 100f);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetTLOD_VR", $"Exception {ex}: {ex.Message}");
		}
		return 0f;
	}

	public float GetOLOD_PC()
	{
		try
		{
			return (float)Math.Round(MemoryInterface.ReadMemory<float>(addrOLOD) * 100f);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetOLOD", $"Exception {ex}: {ex.Message}");
		}
		return 0f;
	}

	public float GetOLOD_VR()
	{
		try
		{
			return (float)Math.Round(MemoryInterface.ReadMemory<float>(addrOLOD_VR) * 100f);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetOLOD_VR", $"Exception {ex}: {ex.Message}");
		}
		return 0f;
	}

	public int GetCloudQ_PC()
	{
		try
		{
			return MemoryInterface.ReadMemory<int>(addrCloudQ);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetCloudQ", $"Exception {ex}: {ex.Message}");
		}
		return -1;
	}

	public int GetCloudQ_VR()
	{
		try
		{
			return MemoryInterface.ReadMemory<int>(addrCloudQ_VR);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetCloudQ VR", $"Exception {ex}: {ex.Message}");
		}
		return -1;
	}

	public bool GetDynSet(bool VR = false)
	{
		try
		{
			return MemoryInterface.ReadMemory<byte>(VR ? addrDynSetVR : addrDynSet) == 1;
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetDynSet", $"Exception {ex}: {ex.Message}");
		}
		return false;
	}

	public int GetDynSetFrameRateTarget(bool VR = false)
	{
		try
		{
			return MemoryInterface.ReadMemory<int>(VR ? addrDynSetFrameRateTargetVR : addrDynSetFrameRateTarget);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:GetDynSetFrameRateTarget", $"Exception {ex}: {ex.Message}");
		}
		return -1;
	}

	public float SetTLOD(float value, bool verify = false)
	{
		value = Math.Clamp((float)Math.Round(value), Model.SimMinLOD, Model.SimMaxLOD + Model.TLODBasePlusSeekHeadroom);
		if (allowMemoryWrites)
		{
			if (Model.VrModeActive)
			{
				SetTLOD_VR(value);
			}
			else
			{
				SetTLOD_PC(value);
			}
			if (verify && value != (Model.VrModeActive ? GetTLOD_VR() : GetTLOD_PC()))
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:SetTLOD", $"TLOD value mismatch: {value} != {(Model.VrModeActive ? GetTLOD_VR() : GetTLOD_PC())}, app disabled");
				allowMemoryWrites = false;
				Model.AppEnabled = false;
				return 0f;
			}
			return value;
		}
		return 0f;
	}

	public void SetTLOD_PC(float value)
	{
		try
		{
			MemoryInterface.WriteMemory<float>(addrTLOD, value / 100f);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:SetTLOD", $"Exception {ex}: {ex.Message}");
		}
	}

	public void SetTLOD_VR(float value)
	{
		try
		{
			MemoryInterface.WriteMemory<float>(addrTLOD_VR, value / 100f);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:SetTLOD VR", $"Exception {ex}: {ex.Message}");
		}
	}

	public float SetOLOD(float value, bool verify = false)
	{
		bool flag = false;
		value = Math.Clamp((float)Math.Round(value), Model.SimMinLOD, Model.SimMaxLOD);
		if (allowMemoryWrites)
		{
			if (Model.VrModeActive)
			{
				SetOLOD_VR(value);
			}
			else
			{
				SetOLOD_PC(value);
			}
			if (flag)
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:SetOLOD", $"OLOD value applied: {value} → {(Model.VrModeActive ? "VR" : "PC")} mode (VR mode {(Model.VrModeActive ? "active" : "inactive")})");
			}
			if ((verify | flag) && value != (Model.VrModeActive ? GetOLOD_VR() : GetOLOD_PC()))
			{
				Logger.Log(LogLevel.Debug, "MemoryManager:SetOLOD", $"OLOD value mismatch: {value} != {(Model.VrModeActive ? GetOLOD_VR() : GetOLOD_PC())}, app disabled");
				allowMemoryWrites = false;
				Model.AppEnabled = false;
				return 0f;
			}
			return value;
		}
		return 0f;
	}

	public void SetOLOD_PC(float value)
	{
		try
		{
			MemoryInterface.WriteMemory<float>(addrOLOD, value / 100f);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:SetOLOD", $"Exception {ex}: {ex.Message}");
		}
	}

	public void SetOLOD_VR(float value)
	{
		try
		{
			MemoryInterface.WriteMemory<float>(addrOLOD_VR, value / 100f);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryManager:SetOLOD VR", $"Exception {ex}: {ex.Message}");
		}
	}
}

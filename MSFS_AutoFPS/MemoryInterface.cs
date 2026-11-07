using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace MSFS_AutoFPS;

public static class MemoryInterface
{
	private enum TOKEN_INFORMATION_CLASS
	{
		TokenElevation = 20
	}

	private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

	public const int PROCESS_VM_OPERATION = 8;

	public const int PROCESS_VM_READ = 16;

	public const int PROCESS_VM_WRITE = 32;

	private static Process proc;

	private static nint procHandle;

	[DllImport("advapi32.dll", SetLastError = true)]
	private static extern bool OpenProcessToken(nint ProcessHandle, uint DesiredAccess, out nint TokenHandle);

	[DllImport("advapi32.dll", SetLastError = true)]
	private static extern bool GetTokenInformation(nint TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, nint TokenInformation, uint TokenInformationLength, out uint ReturnLength);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(nint hObject);

	public static long GetModuleAddress(string Name)
	{
		try
		{
			if (proc != null)
			{
				foreach (ProcessModule module in proc.Modules)
				{
					if (Name == module.ModuleName)
					{
						return module.BaseAddress;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryInterface:GetModuleAddress", $"Exception {ex}: {ex.Message}");
		}
		return -1L;
	}

	public static bool Attach(string name, out string MSFSVersion, bool returnTitleAndVersions = false)
	{
		MSFSVersion = null;
		try
		{
			Process[] processesByName = Process.GetProcessesByName(name);
			if (processesByName.Length != 0)
			{
				proc = processesByName[0];
				nint bestHwnd = IntPtr.Zero;
				EnumWindows((nint hWnd, nint lParam) =>
				{
					GetWindowThreadProcessId(hWnd, out var lpdwProcessId);
					if (lpdwProcessId == proc.Id && Regex.IsMatch(GetWindowTitle(hWnd) ?? "", "\\d+\\.\\d+\\.\\d+\\.\\d+"))
					{
						bestHwnd = hWnd;
					}
					return true;
				}, IntPtr.Zero);
				if (bestHwnd != IntPtr.Zero)
				{
					procHandle = NativeMethods.OpenProcess(56, bInheritHandle: false, proc.Id);
					string windowTitle = GetWindowTitle(bestHwnd);
					if (returnTitleAndVersions)
					{
						string value = proc.MainModule?.FileVersionInfo?.FileVersion?.Replace(',', '.') ?? "<unknown>";
						string value2 = proc.MainModule?.FileVersionInfo?.ProductVersion?.Replace(',', '.') ?? "<unknown>";
						MSFSVersion = $"Window Title: {windowTitle} | File Version: {value} | Product Version: {value2}";
						return true;
					}
					MSFSVersion = proc.MainModule?.FileVersionInfo?.FileVersion?.Replace(',', '.');
					if (MSFSVersion == null || !Regex.IsMatch(MSFSVersion ?? "", "^\\d+\\.\\d+\\.\\d+\\.\\d+$"))
					{
						MSFSVersion = proc.MainModule?.FileVersionInfo?.ProductVersion?.Replace(',', '.');
					}
					if (MSFSVersion == null || !Regex.IsMatch(MSFSVersion ?? "", "^\\d+\\.\\d+\\.\\d+\\.\\d+$"))
					{
						Match match = new Regex("Microsoft Flight Simulator(?: 2024)? - (\\d+\\.\\d+\\.\\d+\\.\\d+)").Match(windowTitle);
						if (match.Success)
						{
							MSFSVersion = match.Groups[1].Value;
						}
					}
					return true;
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryInterface:Attach", $"Exception {ex}: {ex.Message}");
		}
		return false;
	}

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern int GetWindowTextLength(nint hWnd);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	private static string GetWindowTitle(nint hWnd)
	{
		int windowTextLength = GetWindowTextLength(hWnd);
		if (windowTextLength == 0)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder(windowTextLength + 1);
		GetWindowText(hWnd, stringBuilder, stringBuilder.Capacity);
		return stringBuilder.ToString();
	}

	public static bool Attach2(string name, out string MSFSVersion, bool returnTitleAndVersions = false)
	{
		MSFSVersion = null;
		try
		{
			Process[] processesByName = Process.GetProcessesByName(name);
			if (processesByName.Length != 0)
			{
				proc = processesByName[0];
				procHandle = NativeMethods.OpenProcess(56, bInheritHandle: false, proc.Id);
				if (returnTitleAndVersions)
				{
					string value = proc.MainWindowTitle ?? "<unknown>";
					string value2 = proc.MainModule?.FileVersionInfo?.FileVersion?.Replace(',', '.') ?? "<unknown>";
					string value3 = proc.MainModule?.FileVersionInfo?.ProductVersion?.Replace(',', '.') ?? "<unknown>";
					MSFSVersion = $"Window Title: {value} | File Version: {value2} | Product Version: {value3}";
					return true;
				}
				MSFSVersion = proc.MainModule?.FileVersionInfo?.FileVersion?.Replace(',', '.');
				if (MSFSVersion == null || !Regex.IsMatch(MSFSVersion ?? "", "^\\d+\\.\\d+\\.\\d+\\.\\d+$"))
				{
					MSFSVersion = proc.MainModule?.FileVersionInfo?.ProductVersion?.Replace(',', '.');
				}
				if (MSFSVersion == null || !Regex.IsMatch(MSFSVersion ?? "", "^\\d+\\.\\d+\\.\\d+\\.\\d+$"))
				{
					Match match = new Regex("Microsoft Flight Simulator(?: 2024)? - (\\d+\\.\\d+\\.\\d+\\.\\d+)").Match(proc.MainWindowTitle);
					if (match.Success)
					{
						MSFSVersion = match.Groups[1].Value;
					}
				}
				return true;
			}
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryInterface:Attach2", $"Exception {ex}: {ex.Message}");
		}
		return false;
	}

	public static bool Attach3(string name, out string MSFSVersion, bool returnFullWindowTitle = false)
	{
		MSFSVersion = null;
		try
		{
			Process[] processesByName = Process.GetProcessesByName(name);
			if (processesByName.Length == 0)
			{
				return false;
			}
			Process process = processesByName[0];
			for (int i = 0; i < processesByName.Length; i++)
			{
				try
				{
					if (!string.IsNullOrEmpty(processesByName[i].MainWindowTitle))
					{
						process = processesByName[i];
						break;
					}
				}
				catch
				{
				}
			}
			proc = process;
			procHandle = NativeMethods.OpenProcess(56, bInheritHandle: false, proc.Id);
			if (returnFullWindowTitle)
			{
				MSFSVersion = SafeGetString(() => proc.MainWindowTitle) ?? string.Empty;
				return true;
			}
			try
			{
				MSFSVersion = SafeGetString(() => proc.MainModule?.FileVersionInfo?.FileVersion)?.Replace(',', '.');
			}
			catch
			{
				MSFSVersion = null;
			}
			if (MSFSVersion == null || !Regex.IsMatch(MSFSVersion ?? "", "^\\d+\\.\\d+\\.\\d+\\.\\d+$"))
			{
				try
				{
					MSFSVersion = SafeGetString(() => proc.MainModule?.FileVersionInfo?.ProductVersion)?.Replace(',', '.');
				}
				catch
				{
					MSFSVersion = null;
				}
			}
			if (MSFSVersion == null || !Regex.IsMatch(MSFSVersion ?? "", "^\\d+\\.\\d+\\.\\d+\\.\\d+$"))
			{
				try
				{
					string text = SafeGetString(() => proc.MainWindowTitle) ?? string.Empty;
					text = text.Replace('\u00a0', ' ').Trim();
					Match match = new Regex("Microsoft Flight Simulator(?: 2024)? - (\\d+\\.\\d+\\.\\d+\\.\\d+)").Match(text);
					if (match.Success)
					{
						MSFSVersion = match.Groups[1].Value;
					}
				}
				catch
				{
				}
			}
			string empty = string.Empty;
			string value = string.Empty;
			string value2 = string.Empty;
			long value3 = -1L;
			int num = -1;
			int num2 = -1;
			int num3 = -1;
			long num4 = -1L;
			long num5 = -1L;
			bool flag = false;
			DateTime? dateTime = null;
			TimeSpan? timeSpan = null;
			ProcessPriorityClass? processPriorityClass = null;
			int num6 = 0;
			List<string> list = null;
			try
			{
				empty = SafeGetString(() => proc.MainWindowTitle) ?? string.Empty;
			}
			catch
			{
				empty = string.Empty;
			}
			try
			{
				ProcessModule mm = proc.MainModule;
				if (mm != null)
				{
					value = SafeGetString(() => mm.ModuleName) ?? string.Empty;
					value2 = SafeGetString(() => mm.FileName) ?? string.Empty;
					value3 = mm.BaseAddress;
				}
			}
			catch
			{
				value = string.Empty;
				value2 = string.Empty;
				value3 = -1L;
			}
			try
			{
				list = new List<string>();
				int num7 = 0;
				foreach (ProcessModule module in proc.Modules)
				{
					if (num7++ < 10)
					{
						try
						{
							list.Add(module.ModuleName ?? module.FileName ?? "<unknown>");
						}
						catch
						{
							list.Add("<inaccessible>");
						}
						continue;
					}
					break;
				}
				num = proc.Modules?.Count ?? (-1);
			}
			catch
			{
				list = null;
				num = -1;
			}
			try
			{
				num2 = proc.HandleCount;
				num3 = proc.Threads?.Count ?? (-1);
				num4 = proc.WorkingSet64;
				num5 = proc.VirtualMemorySize64;
				flag = proc.Responding;
			}
			catch
			{
				num2 = -1;
				num3 = -1;
				num4 = -1L;
				num5 = -1L;
				flag = false;
			}
			try
			{
				dateTime = SafeGetDateTime(() => proc.StartTime);
				if (dateTime.HasValue)
				{
					timeSpan = DateTime.UtcNow - dateTime.Value.ToUniversalTime();
				}
			}
			catch
			{
				dateTime = null;
				timeSpan = null;
			}
			try
			{
				processPriorityClass = SafeGetPriority(() => proc.PriorityClass);
				num6 = SafeGetInt(() => proc.BasePriority);
			}
			catch
			{
				processPriorityClass = null;
				num6 = 0;
			}
			try
			{
				string value4 = ((list != null && list.Count > 0) ? string.Join(";", list) : "<none-or-inaccessible>");
				Logger.Log(LogLevel.Information, "MemoryInterface:Attach3", $"Attach3 => PID={proc.Id}; SessionId={proc.SessionId}; StartTime={(dateTime.HasValue ? dateTime.Value.ToString("o") : "unknown")}; Uptime={(timeSpan.HasValue ? timeSpan.Value.ToString("d\\.hh\\:mm\\:ss") : "unknown")}; Title=\"{empty}\"; MainModule=\"{value}\"; MainModuleFile=\"{value2}\"; ModuleBase=0x{value3:X}; ModuleCount={num}; ModulesSample=\"{value4}\"; Handles={num2}; Threads={num3}; WorkingSet={num4}; VirtualMem={num5}; Responding={flag}; Priority={(processPriorityClass.HasValue ? processPriorityClass.Value.ToString() : "unknown")}; BasePriority={num6}; ProcIs64Bit={(Environment.Is64BitOperatingSystem ? "likely" : "no")}; VersionResolved={MSFSVersion ?? "null"}");
			}
			catch
			{
			}
			return true;
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryInterface:Attach3", "Exception " + ex.GetType().Name + ": " + ex.Message);
		}
		return false;
		static DateTime? SafeGetDateTime(Func<DateTime> getter)
		{
			try
			{
				return getter();
			}
			catch
			{
				return null;
			}
		}
		static int SafeGetInt(Func<int> getter)
		{
			try
			{
				return getter();
			}
			catch
			{
				return -1;
			}
		}
		static ProcessPriorityClass? SafeGetPriority(Func<ProcessPriorityClass> getter)
		{
			try
			{
				return getter();
			}
			catch
			{
				return null;
			}
		}
		static string SafeGetString(Func<string> getter)
		{
			try
			{
				return getter();
			}
			catch
			{
				return null;
			}
		}
	}

	public static void WriteMemory<T>(long Address, object Value)
	{
		try
		{
			byte[] array = StructureToByteArray(Value);
			NativeMethods.NtWriteVirtualMemory(checked((int)procHandle), Address, array, array.Length, out var _);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryInterface:WriteMemory", $"Exception {ex}: {ex.Message}");
		}
	}

	public static T ReadMemory<T>(long address) where T : struct
	{
		try
		{
			byte[] array = new byte[Marshal.SizeOf(typeof(T))];
			NativeMethods.NtReadVirtualMemory(checked((int)procHandle), address, array, array.Length, out var _);
			return ByteArrayToStructure<T>(array);
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryInterface:ReadMemory", $"Exception {ex}: {ex.Message}");
		}
		return default;
	}

	public static byte[] ReadBytes(long address, int length)
	{
		try
		{
			byte[] array = new byte[length];
			NativeMethods.NtReadVirtualMemory(checked((int)procHandle), address, array, array.Length, out var _);
			return array;
		}
		catch (Exception ex)
		{
			Logger.Log(LogLevel.Error, "MemoryInterface:ReadBytes", $"Exception {ex}: {ex.Message}");
			return null;
		}
	}

	private static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
	{
		GCHandle gCHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
		try
		{
			return (T)Marshal.PtrToStructure(gCHandle.AddrOfPinnedObject(), typeof(T));
		}
		finally
		{
			gCHandle.Free();
		}
	}

	private static byte[] StructureToByteArray(object obj)
	{
		int num = Marshal.SizeOf(obj);
		byte[] array = new byte[num];
		nint num2 = Marshal.AllocHGlobal(num);
		Marshal.StructureToPtr(obj, num2, fDeleteOld: true);
		Marshal.Copy(num2, array, 0, num);
		Marshal.FreeHGlobal(num2);
		return array;
	}
}

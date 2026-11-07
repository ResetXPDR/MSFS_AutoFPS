using System.Runtime.InteropServices;

namespace MSFS_AutoFPS;

internal static class NativeMethods
{
	[DllImport("kernel32.dll")]
	internal static extern nint OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

	[DllImport("ntdll.dll")]
	internal static extern nint NtWriteVirtualMemory(int ProcessHandle, long BaseAddress, byte[] Buffer, int NumberOfBytesToWrite, out int NumberOfBytesWritten);

	[DllImport("ntdll.dll")]
	internal static extern bool NtReadVirtualMemory(int ProcessHandle, long BaseAddress, byte[] Buffer, int NumberOfBytesToRead, out int NumberOfBytesRead);
}

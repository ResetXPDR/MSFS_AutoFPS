using System.Runtime.InteropServices;

namespace MSFS_AutoFPS;

public struct ResponseString
{
	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
	public string Data;
}

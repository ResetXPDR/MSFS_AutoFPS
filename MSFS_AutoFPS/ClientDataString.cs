using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MSFS_AutoFPS;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ClientDataString
{
	[MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
	public byte[] data;

	public ClientDataString(string strData)
	{
		byte[] bytes = Encoding.ASCII.GetBytes(strData);
		byte[] destinationArray = new byte[1024];
		Array.Copy(bytes, destinationArray, bytes.Length);
		data = destinationArray;
	}
}

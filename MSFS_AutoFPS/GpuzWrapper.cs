using System.Runtime.InteropServices;

namespace MSFS_AutoFPS;

internal class GpuzWrapper
{
	[DllImport("GpuzShMem.x64.dll", SetLastError = true)]
	public static extern int InitGpuzShMem();

	[DllImport("GpuzShMem.x64.dll", SetLastError = true)]
	public static extern int RemGpuzShMem();

	[DllImport("GpuzShMem.x64.dll", SetLastError = true)]
	public static extern nint GetSensorName(int index);

	[DllImport("GpuzShMem.x64.dll", SetLastError = true)]
	public static extern double GetSensorValue(int index);

	[DllImport("GpuzShMem.x64.dll", SetLastError = true)]
	public static extern nint GetSensorUnit(int index);

	[DllImport("GpuzShMem.x64.dll", SetLastError = true)]
	public static extern nint GetDataKey(int index);

	[DllImport("GpuzShMem.x64.dll", SetLastError = true)]
	public static extern nint GetDataValue(int index);

	public bool Open()
	{
		if (InitGpuzShMem() != 0)
		{
			return false;
		}
		return true;
	}

	public void Close()
	{
		RemGpuzShMem();
	}

	public string SensorName(int index)
	{
		return Marshal.PtrToStringUni(GetSensorName(index));
	}

	public double SensorValue(int index)
	{
		return GetSensorValue(index);
	}

	public string SensorUnit(int index)
	{
		return Marshal.PtrToStringUni(GetSensorUnit(index));
	}

	public string DataKey(int index)
	{
		return Marshal.PtrToStringUni(GetDataKey(index));
	}

	public string DataValue(int index)
	{
		return Marshal.PtrToStringUni(GetDataValue(index));
	}

	public string ListAllSensors()
	{
		string text = string.Empty;
		int num = 0;
		string text2;
		while ((text2 = SensorName(num)) != string.Empty)
		{
			text = text + "[" + num + "]" + text2 + ": " + SensorValue(num) + " " + SensorUnit(num) + "\n";
			num++;
		}
		return text;
	}

	public string ListAllData()
	{
		string text = string.Empty;
		int num = 0;
		string text2;
		while ((text2 = DataKey(num)) != string.Empty)
		{
			text = text + "[" + num + "]" + text2 + ": " + DataValue(num) + "\n";
			num++;
		}
		return text;
	}
}

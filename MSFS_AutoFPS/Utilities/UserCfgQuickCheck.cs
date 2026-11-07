using System;
using System.Globalization;
using System.IO;

namespace MSFS_AutoFPS.Utilities;

public class UserCfgQuickCheck
{
	private readonly ServiceModel model;

	private readonly string cfgPath;

	private readonly ConfigParser parser = new ConfigParser();

	private DateTime lastWriteTime;

	private float lastTLOD_PC;

	private float lastTLOD_VR;

	private float lastOLOD_PC;

	private float lastOLOD_VR;

	private int lastCloudQ_PC;

	private int lastCloudQ_VR;

	public event Action<float, float, float, float, int, int> ValuesChanged;

	public UserCfgQuickCheck(ServiceModel model, string cfgPath, float initialTLOD_PC, float initialTLOD_VR, float initialOLOD_PC, float initialOLOD_VR, int initialCloudQ_PC, int initialCloudQ_VR)
	{
		this.model = model;
		this.cfgPath = cfgPath;
		lastWriteTime = File.GetLastWriteTime(cfgPath);
		lastTLOD_PC = initialTLOD_PC;
		lastTLOD_VR = initialTLOD_VR;
		lastOLOD_PC = initialOLOD_PC;
		lastOLOD_VR = initialOLOD_VR;
		lastCloudQ_PC = initialCloudQ_PC;
		lastCloudQ_VR = initialCloudQ_VR;
	}

	public void Tick()
	{
		DateTime dateTime = File.GetLastWriteTime(cfgPath);
		if (!(dateTime > lastWriteTime))
		{
			return;
		}
		lastWriteTime = dateTime;
		if (model.MemoryAccess == null)
		{
			float num = ReadLOD(1);
			float num2 = ReadLOD(2);
			float num3 = ReadLOD(3);
			float num4 = ReadLOD(4);
			int num5 = ReadCloudQuality(1);
			int num6 = ReadCloudQuality(2);
			bool num7 = num != lastTLOD_PC || num3 != lastTLOD_VR || num2 != lastOLOD_PC || num4 != lastOLOD_VR;
			bool flag = num5 != lastCloudQ_PC || num6 != lastCloudQ_VR;
			if (num7 | flag)
			{
				lastTLOD_PC = num;
				lastTLOD_VR = num3;
				lastOLOD_PC = num2;
				lastOLOD_VR = num4;
				lastCloudQ_PC = num5;
				lastCloudQ_VR = num6;
				ValuesChanged?.Invoke(lastTLOD_PC, lastTLOD_VR, lastOLOD_PC, lastOLOD_VR, lastCloudQ_PC, lastCloudQ_VR);
			}
		}
		else
		{
			ValuesChanged?.Invoke(lastTLOD_PC, lastTLOD_VR, lastOLOD_PC, lastOLOD_VR, lastCloudQ_PC, lastCloudQ_VR);
		}
	}

	private float ReadLOD(int occurrence)
	{
		return (float)Math.Round(Convert.ToSingle(parser.GetOrModifyItem(cfgPath, "LoDFactor", occurrence).Replace(',', '.'), CultureInfo.InvariantCulture) * 100f);
	}

	private int ReadCloudQuality(int occurrence)
	{
		return Convert.ToInt32(parser.GetOrModifyItem(cfgPath, "Quality", 1, "VolumetricClouds", occurrence));
	}
}

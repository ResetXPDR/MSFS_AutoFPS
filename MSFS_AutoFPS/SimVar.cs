namespace MSFS_AutoFPS;

public class SimVar
{
	public uint ID { get; set; }

	public string Name { get; set; }

	public float Data { get; set; }

	public SimVar(uint iD, float data = 0f)
	{
		ID = iD;
		Data = data;
	}
}

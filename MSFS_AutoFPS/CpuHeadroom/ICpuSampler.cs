using System;

namespace CpuHeadroom;

public interface ICpuSampler : IDisposable
{
	CoreUsage[] Sample();
}

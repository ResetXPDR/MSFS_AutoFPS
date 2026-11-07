using System;

namespace CpuHeadroom;

public interface IProcessSampler : IDisposable
{
	double SampleProcessPercent(int pid);
}

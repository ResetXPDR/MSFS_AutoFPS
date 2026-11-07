using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;

namespace CpuHeadroom;

public sealed class ProcessCpuSampler : IProcessSampler, IDisposable
{
	private readonly Dictionary<int, TimeSpan> _lastCpu = new Dictionary<int, TimeSpan>();

	private readonly Dictionary<int, DateTime> _lastTime = new Dictionary<int, DateTime>();

	public double SampleProcessPercent(int pid)
	{
		Process processById = Process.GetProcessById(pid);
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan totalProcessorTime = processById.TotalProcessorTime;
		if (!_lastCpu.TryGetValue(pid, out var value))
		{
			_lastCpu[pid] = totalProcessorTime;
			_lastTime[pid] = utcNow;
			return 0.0;
		}
		double totalMilliseconds = (totalProcessorTime - value).TotalMilliseconds;
		double totalMilliseconds2 = (utcNow - _lastTime[pid]).TotalMilliseconds;
		_lastCpu[pid] = totalProcessorTime;
		_lastTime[pid] = utcNow;
		if (totalMilliseconds2 <= 0.0)
		{
			return 0.0;
		}
		int num = 0;
		try
		{
			num = BitOperations.PopCount((uint)processById.ProcessorAffinity);
		}
		catch
		{
			num = Environment.ProcessorCount;
		}
		if (num <= 0)
		{
			num = Environment.ProcessorCount;
		}
		double val = totalMilliseconds / totalMilliseconds2 * 100.0 / (double)num;
		return Math.Max(0.0, Math.Min(100.0, val));
	}

	public void Dispose()
	{
	}
}

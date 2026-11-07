using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using MSFS_AutoFPS;

namespace CpuHeadroom;

public sealed class PdhCoreSampler : ICpuSampler, IDisposable
{
	private enum PdhFormat : uint
	{
		PDH_FMT_DOUBLE = 0x200u
	}

	private struct PDH_FMT_COUNTERVALUE
	{
		public uint CStatus;

		public double doubleValue;
	}

	private readonly nint _query;

	private readonly nint[] _counters;

	private readonly int _coreCount;

	private bool _disposed;

	private readonly bool _available;

	private const string PDH = "pdh.dll";

	public PdhCoreSampler()
	{
		_coreCount = Environment.ProcessorCount;
		_counters = new nint[_coreCount];
		if (PdhOpenQuery(IntPtr.Zero, IntPtr.Zero, out _query) != 0)
		{
			Logger.Log(LogLevel.Debug, "CpuHeadRoom:PdhCoreSampler", "PdhOpenQuery failed. CPU monitoring disabled.");
			_available = false;
			return;
		}
		bool flag = false;
		for (int i = 0; i < _coreCount; i++)
		{
			string counterPath = $"\\Processor({i})\\% Processor Time";
			if (PdhAddCounter(_query, counterPath, IntPtr.Zero, out _counters[i]) != 0)
			{
				_counters[i] = IntPtr.Zero;
			}
			else
			{
				flag = true;
			}
		}
		if (!flag)
		{
			Logger.Log(LogLevel.Debug, "CpuHeadRoom:PdhCoreSampler", "No counters could be added. CPU monitoring disabled.");
			_available = false;
			return;
		}
		PdhCollectQueryData(_query);
		Thread.Sleep(1000);
		PdhCollectQueryData(_query);
		_available = true;
	}

	public CoreUsage[] Sample()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException("PdhCoreSampler");
		}
		DateTime utcNow = DateTime.UtcNow;
		if (!_available)
		{
			CoreUsage[] array = new CoreUsage[_coreCount];
			for (int i = 0; i < _coreCount; i++)
			{
				array[i] = new CoreUsage(i, 0.0, utcNow);
			}
			return array;
		}
		PdhCollectQueryData(_query);
		List<CoreUsage> list = new List<CoreUsage>(_coreCount);
		for (int j = 0; j < _coreCount; j++)
		{
			uint type;
			PDH_FMT_COUNTERVALUE value;
			if (_counters[j] == IntPtr.Zero)
			{
				list.Add(new CoreUsage(j, 0.0, utcNow));
			}
			else if (PdhGetFormattedCounterValue(_counters[j], PdhFormat.PDH_FMT_DOUBLE, out type, out value) == 0)
			{
				double percent = Math.Max(0.0, Math.Min(100.0, value.doubleValue));
				list.Add(new CoreUsage(j, percent, utcNow));
			}
			else
			{
				list.Add(new CoreUsage(j, 0.0, utcNow));
			}
		}
		return list.ToArray();
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			PdhCloseQuery(_query);
			_disposed = true;
		}
	}

	[DllImport("pdh.dll", CharSet = CharSet.Unicode)]
	private static extern int PdhOpenQuery(nint dataSource, nint userData, out nint query);

	[DllImport("pdh.dll", CharSet = CharSet.Unicode)]
	private static extern int PdhAddCounter(nint query, string counterPath, nint userData, out nint counter);

	[DllImport("pdh.dll")]
	private static extern int PdhCollectQueryData(nint query);

	[DllImport("pdh.dll")]
	private static extern int PdhCloseQuery(nint query);

	[DllImport("pdh.dll")]
	private static extern int PdhGetFormattedCounterValue(nint counter, PdhFormat format, out uint type, out PDH_FMT_COUNTERVALUE value);
}

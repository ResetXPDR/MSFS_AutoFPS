using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CpuHeadroom;

public sealed class CpuHeadroomProbe : IDisposable
{
	private readonly ICpuSampler _sampler;

	private readonly IProcessSampler _processSampler;

	private readonly int _msfsPid;

	private readonly CancellationTokenSource _cts = new CancellationTokenSource();

	private readonly Task _loop;

	private readonly int _intervalMs;

	private readonly int _windowSize;

	private readonly Queue<double[]> _perCoreHistory = new Queue<double[]>();

	private int? _currentChosenCore;

	private int _candidateCore;

	private int _candidateCount;

	private readonly int _requiredStableCount;

	private readonly double _switchMarginPercent;

	private int[]? _msfsAllowedCores;

	private double _prevTopXMetric;

	private int _topX = 4;

	private bool _topXUseAverage = true;

	public event Action<CpuSnapshot>? OnSnapshot;

	public CpuHeadroomProbe(ICpuSampler sampler, IProcessSampler processSampler, int msfsPid, int intervalMs = 1000, int windowSeconds = 10, int requiredStableSeconds = 10, double switchMarginPercent = 5.0, int topX = 4, bool topXUseAverage = true)
	{
		_sampler = sampler;
		_processSampler = processSampler;
		_msfsPid = msfsPid;
		_intervalMs = intervalMs;
		_windowSize = Math.Max(1, windowSeconds * 1000 / Math.Max(1, intervalMs));
		_requiredStableCount = Math.Max(1, requiredStableSeconds * 1000 / Math.Max(1, intervalMs));
		_switchMarginPercent = Math.Max(0.0, switchMarginPercent);
		_topX = Math.Max(1, topX);
		_topXUseAverage = topXUseAverage;
		try
		{
			Process processById = Process.GetProcessById(_msfsPid);
			_msfsAllowedCores = GetAllowedCoreIndices(processById);
		}
		catch
		{
			_msfsAllowedCores = null;
		}
		_loop = Task.Run((Func<Task?>)RunLoop);
	}

	private int[] GetAllowedCoreIndices(Process p)
	{
		long num = p.ProcessorAffinity;
		List<int> list = new List<int>();
		for (int i = 0; i < Environment.ProcessorCount; i++)
		{
			if (((num >> i) & 1) != 0L)
			{
				list.Add(i);
			}
		}
		return list.ToArray();
	}

	private async Task RunLoop()
	{
		try
		{
			while (!_cts.IsCancellationRequested)
			{
				CoreUsage[] perCore = _sampler.Sample();
				int num = perCore.Length;
				int maxCoreIndex = 0;
				double num2 = 0.0;
				for (int i = 0; i < num; i++)
				{
					if (perCore[i].Percent > num2)
					{
						num2 = perCore[i].Percent;
						maxCoreIndex = i;
					}
				}
				double headroomPercent = Math.Max(0.0, 100.0 - num2);
				double totalCpuPercent = ((num > 0) ? perCore.Average((CoreUsage c) => c.Percent) : 0.0);
				double msfsCpuPercent = ((_msfsPid > 0) ? _processSampler.SampleProcessPercent(_msfsPid) : 0.0);
				double[] item = perCore.Select((CoreUsage c) => c.Percent).ToArray();
				_perCoreHistory.Enqueue(item);
				if (_perCoreHistory.Count > _windowSize)
				{
					_perCoreHistory.Dequeue();
				}
				int? stableCoreIndex = null;
				double stableInstantPercent = 0.0;
				double num3 = 0.0;
				double topXDelta = 0.0;
				if (_perCoreHistory.Count >= 1)
				{
					int count = _perCoreHistory.Count;
					double[] array = new double[num];
					foreach (double[] item2 in _perCoreHistory)
					{
						for (int num4 = 0; num4 < num; num4++)
						{
							array[num4] += item2[num4];
						}
					}
					double[] array2 = new double[num];
					for (int num5 = 0; num5 < num; num5++)
					{
						array2[num5] = array[num5] / (double)count;
					}
					double[] array3 = (from num12 in (_msfsAllowedCores != null && _msfsAllowedCores.Length != 0) ? _msfsAllowedCores : Enumerable.Range(0, num).ToArray()
						select perCore[num12].Percent into x
						orderby x descending
						select x).ToArray();
					int count2 = Math.Clamp(_topX, 1, array3.Length);
					double[] array4 = array3.Take(count2).ToArray();
					double num6 = array4.Sum();
					if (_topXUseAverage)
					{
						num3 = ((array4.Length != 0) ? (num6 / (double)array4.Length) : 0.0);
					}
					else
					{
						num3 = num6;
					}
					topXDelta = num3 - _prevTopXMetric;
					_prevTopXMetric = num3;
					int num7 = 0;
					double num8 = array2[0];
					for (int num9 = 1; num9 < array2.Length; num9++)
					{
						if (array2[num9] > num8)
						{
							num8 = array2[num9];
							num7 = num9;
						}
					}
					if (!_currentChosenCore.HasValue)
					{
						if (_candidateCore == num7)
						{
							_candidateCount++;
						}
						else
						{
							_candidateCore = num7;
							_candidateCount = 1;
						}
						if (_candidateCount >= _requiredStableCount)
						{
							_currentChosenCore = num7;
							_candidateCount = 0;
						}
					}
					else
					{
						int value = _currentChosenCore.Value;
						double num10 = array2[value];
						double num11 = num8;
						if (num7 == value)
						{
							_candidateCore = num7;
							_candidateCount = 0;
						}
						else if (num11 >= num10 + _switchMarginPercent)
						{
							if (_candidateCore == num7)
							{
								_candidateCount++;
							}
							else
							{
								_candidateCore = num7;
								_candidateCount = 1;
							}
							if (_candidateCount >= _requiredStableCount)
							{
								_currentChosenCore = num7;
								_candidateCount = 0;
							}
						}
						else
						{
							_candidateCount = 0;
							_candidateCore = -1;
						}
					}
					stableCoreIndex = _currentChosenCore;
					if (stableCoreIndex.HasValue)
					{
						int valueOrDefault = stableCoreIndex.GetValueOrDefault();
						if (valueOrDefault >= 0 && valueOrDefault < num)
						{
							stableInstantPercent = perCore[valueOrDefault].Percent;
						}
					}
				}
				CpuSnapshot obj = new CpuSnapshot(DateTime.UtcNow, headroomPercent, num2, maxCoreIndex, totalCpuPercent, msfsCpuPercent, stableCoreIndex, stableInstantPercent, num3, topXDelta, perCore);
				OnSnapshot?.Invoke(obj);
				await Task.Delay(_intervalMs, _cts.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (TaskCanceledException)
		{
		}
	}

	public void Dispose()
	{
		_cts.Cancel();
		try
		{
			_loop.Wait(1000);
		}
		catch
		{
		}
		_sampler.Dispose();
		_processSampler.Dispose();
		_cts.Dispose();
	}
}

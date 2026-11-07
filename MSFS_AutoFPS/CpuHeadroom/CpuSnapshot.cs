using System;

namespace CpuHeadroom;

public record CpuSnapshot(DateTime Timestamp, double HeadroomPercent, double MaxCorePercent, int MaxCoreIndex, double TotalCpuPercent, double MsfsCpuPercent, int? StableCoreIndex, double StableInstantPercent, double TopXMetric, double TopXDelta, CoreUsage[] PerCore);

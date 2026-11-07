using System;

namespace CpuHeadroom;

public record CoreUsage(int CoreIndex, double Percent, DateTime Timestamp);

using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class PerformanceMetric
{
    public int MetricId { get; set; }

    public string MetricType { get; set; } = null!;

    public string MetricName { get; set; } = null!;

    public string? Category { get; set; }

    public decimal? MinValue { get; set; }

    public decimal? MaxValue { get; set; }

    public string? Unit { get; set; }
}

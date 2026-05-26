using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Eoopercentage
{
    public int EoopercentageId { get; set; }

    public string MetricName { get; set; } = null!;

    public string Category { get; set; } = null!;

    public decimal MinValue { get; set; }

    public decimal MaxValue { get; set; }
}

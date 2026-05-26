using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Shift
{
    public int ShiftNumber { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public virtual ICollection<ProductionLine> ProductionLines { get; set; } = new List<ProductionLine>();
}

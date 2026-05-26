using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Break
{
    public int BreakId { get; set; }

    public int ProductionLinesId { get; set; }

    public TimeOnly? BreakStart { get; set; }

    public TimeOnly? BreakEnd { get; set; }

    public virtual ProductionLine ProductionLines { get; set; } = null!;
}

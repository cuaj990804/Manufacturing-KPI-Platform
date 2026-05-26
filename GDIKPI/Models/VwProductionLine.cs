using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class VwProductionLine
{
    public string CustomerName { get; set; } = null!;

    public string AreaName { get; set; } = null!;

    public int? LineNumber { get; set; }

    public int DailyGoal { get; set; }

    public int PersonalQuantity { get; set; }

    public string? SupervisorName { get; set; }

    public string? Descansos { get; set; }
}

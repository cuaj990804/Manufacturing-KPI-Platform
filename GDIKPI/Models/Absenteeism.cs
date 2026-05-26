using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Absenteeism
{
    public int AbsenteeismId { get; set; }

    public string AbsenteeismCategory { get; set; } = null!;

    public int AbsenteeismQuantity { get; set; }

    public DateOnly AbsenteeismDate { get; set; }

    public string EmployeeNumber { get; set; } = null!;

    public int ProductionLinesId { get; set; }

    public virtual User EmployeeNumberNavigation { get; set; } = null!;

    public virtual ProductionLine ProductionLines { get; set; } = null!;
}

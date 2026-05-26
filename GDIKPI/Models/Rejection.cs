using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Rejection
{
    public int RejectionId { get; set; }

    public int ProductionLinesId { get; set; }

    public string Category { get; set; } = null!;

    public string EmployeeNumber { get; set; } = null!;

    public int? DefectQuantity { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int ProgramId { get; set; }

    public string? ProgramDescription { get; set; }

    public virtual ICollection<DefectsDatum> DefectsData { get; set; } = new List<DefectsDatum>();

    public virtual ProductionLine ProductionLines { get; set; } = null!;
}

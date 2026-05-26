using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class DefectsDatum
{
    public int DefectsDataId { get; set; }

    public int? RejectionId { get; set; }

    public int ProductionLinesId { get; set; }

    public string? Category { get; set; }

    public string EmployeeNumber { get; set; } = null!;

    public int DefectId { get; set; }

    public int? DefectQuantity { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? ScannerValue { get; set; }

    public virtual Defect Defect { get; set; } = null!;

    public virtual ProductionLine ProductionLines { get; set; } = null!;

    public virtual Rejection? Rejection { get; set; }
}

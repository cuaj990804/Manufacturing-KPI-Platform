using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class VwOee
{
    public int ProductionLinesId { get; set; }

    public int? LineNumber { get; set; }

    public string Area { get; set; } = null!;

    public DateOnly? ReportDate { get; set; }

    public string? Type { get; set; }

    public string? Category { get; set; }

    public string? Status { get; set; }

    public int? Total { get; set; }
}

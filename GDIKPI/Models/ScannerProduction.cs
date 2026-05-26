using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class ScannerProduction
{
    public long ScannerProductionId { get; set; }

    public DateTime ScannerProductionDateTime { get; set; }

    public int LineId { get; set; }

    public string? PartNumber { get; set; }

    public string ScannerValue { get; set; } = null!;
}

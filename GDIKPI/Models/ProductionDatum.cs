using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class ProductionDatum
{
    public int ProductionId { get; set; }

    public int ProductionLinesId { get; set; }

    public DateOnly ProductionDate { get; set; }

    public TimeOnly StartHour { get; set; }

    public TimeOnly EndHour { get; set; }

    public int? ProducedPieces { get; set; }

    public int? RejectedPieces { get; set; }

    public int ProgramId { get; set; }

    public string? ProgramDescription { get; set; }

    public virtual ProductionLine ProductionLines { get; set; } = null!;
}

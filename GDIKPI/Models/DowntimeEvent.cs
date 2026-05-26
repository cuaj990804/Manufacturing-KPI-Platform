using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class DowntimeEvent
{
    public int DowntimeId { get; set; }

    public int ProductionLinesId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string? Reason { get; set; }

    public string? Status { get; set; }

    public string? ClosedBy { get; set; }

    public string? OpenedBy { get; set; }

    public string? DowntimeCategory { get; set; }

    public virtual ProductionLine ProductionLines { get; set; } = null!;
}

using Microsoft.EntityFrameworkCore;

namespace GDIKPI.DTO;

// Result contract for GetDailyProduction; the area summary has a different shape.
[Keyless]
public class DailyProductionIntervalDTO
{
    public int LineNumber { get; set; }
    public DateTime ProductionDate { get; set; }
    public string HourInterval { get; set; } = string.Empty;
    public int GoalPieces { get; set; }
    public int ProducedPieces { get; set; }
    public int RejectedPieces { get; set; }
    public int AccumulatedRejections { get; set; }
    public int HourlyBalance { get; set; }
    public int AccumulatedBalance { get; set; }
    public string BreakInfo { get; set; } = string.Empty;
}

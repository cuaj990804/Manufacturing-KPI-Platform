namespace GDIKPI.DTO
{
    public class DailyProductionDTO
    {
        public string AreaCustomerName { get; set; } = null!;
        public int LineNumber { get; set; }
        public DateTime ProductionDate { get; set; }
        public string HourInterval { get; set; } = null!;
        public int GoalPieces { get; set; }
        public int ProducedPieces { get; set; }
        public int RejectedPieces { get; set; }
        public int AccumulatedRejections { get; set; }
        public int DowntimeMinutes { get; set; }
        public int AccumulatedDowntime { get; set; }
        public decimal AccumulatedBalance { get; set; }
        public int EstimatedGoalPieces { get; set; }
        public int RequirementGoalPieces { get; set; }
        public decimal RequirementBalance { get; set; }
        public decimal QualityPercentage { get; set; }

    }
}

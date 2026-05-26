namespace GDIKPI.DTO
{
    public class LineOEEDTO
    {
        public int LineNumber { get; set; }
        public string AreaCustomerName { get; set; } = null!;
        public int RequirementGoalPieces { get; set; }
        public int EstimatedRequirement { get; set; }
        public int ProducedPieces { get; set; }
        public int DowntimeMinutes { get; set; }
        public int RejectedPieces { get; set; }
        public decimal AvailabilityPercentage { get; set; }
        public decimal PerformancePercentage { get; set; }
        public decimal QualityPercentage { get; set; }
        public decimal OeePercentage { get; set; }
        public int ProductionLinesId { get; set; }
        public int PlannedMinutes { get; set; }
        public int OperatingMinutes { get; set; }
    }
}



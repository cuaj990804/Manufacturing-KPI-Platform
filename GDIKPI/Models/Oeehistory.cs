using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Oeehistory
{
    public int Id { get; set; }

    public DateTime? RegistrationDate { get; set; }

    public DateOnly? DateData { get; set; }

    public TimeOnly? TimeData { get; set; }

    public int? ProductionLinesId { get; set; }

    public string? LineNumber { get; set; }

    public string? AreaCustomerName { get; set; }

    public int? RequirementGoalPieces { get; set; }

    public int? ProducedPieces { get; set; }

    public int? DowntimeMinutes { get; set; }

    public int? RejectedPieces { get; set; }

    public decimal? AvailabilityPercentage { get; set; }

    public decimal? PerformancePercentage { get; set; }

    public decimal? QualityPercentage { get; set; }

    public decimal? OeePercentage { get; set; }

    public int? PlannedMinutes { get; set; }

    public int? OperatingMinutes { get; set; }
}

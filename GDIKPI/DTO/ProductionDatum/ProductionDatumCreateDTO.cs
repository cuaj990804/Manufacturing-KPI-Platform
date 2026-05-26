public class ProductionDatumCreateDTO
{
    public int ProductionLinesId { get; set; }
    public string ProductionDate { get; set; } = null!; // "yyyy-MM-dd"
    public string StartHour { get; set; } = null!; // "HH:mm"
    public string EndHour { get; set; } = null!;
    public int? ProducedPieces { get; set; }
    public int? RejectedPieces { get; set; }
    public int ProgramId { get; set; }
    public string? ProgramDescription { get; set; }
}

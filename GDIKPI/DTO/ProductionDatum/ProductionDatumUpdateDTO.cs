namespace GDIKPI.DTO.ProductionDatum
{
    public class ProductionDatumUpdateDTO
    {
        public int ProductionId { get; set; }
        public int ProductionLinesId { get; set; }
        public string ProductionDate { get; set; } = null!; // Cambiar a string
        public string StartHour { get; set; } = null!;      // Cambiar a string
        public string EndHour { get; set; } = null!;        // Cambiar a string
        public int? ProducedPieces { get; set; }
        public int ProgramId { get; set; }
        public string? ProgramDescription { get; set; }

    }
}
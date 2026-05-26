namespace GDIKPI.DTO.ProductionDatum
{
    public class DowntimeCreateDTO
    {
        public int ProductionLinesId { get; set; }
        public DateTime StartTime { get; set; }
        public string? Category { get; set; }

        public string? Reason { get; set; }

    }
}

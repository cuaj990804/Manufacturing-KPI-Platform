namespace GDIKPI.DTO.ProductionDatum
{
    public class DowntimeUpdateDTO
    {
        public int ProductionLinesId { get; set; }

        public DateTime EndTime { get; set; }
        public string ClosedBy { get; set; }
    }
}




namespace GDIKPI.DTO.Area
{
    public class ProductionLineUpdateDTO
    {
        public int AreaId { get; set; }
        public int OriginalLineNumber { get; set; }
        public int LineNumber { get; set; }
        public string? LineName { get; set; }
        public int DailyGoal { get; set; }
        public int PersonalQuantity { get; set; }
        public decimal? StandardTime { get; set; }
        public string Break1Start { get; set; }
        public string Break1End { get; set; }
        public string Break2Start { get; set; }
        public string Break2End { get; set; }
        public string SupervisorEmployeeNumber { get; set; }
        public List<int> Permissions { get; set; } = new List<int>();
        public bool IsActive { get; set; } = true;

    }
}

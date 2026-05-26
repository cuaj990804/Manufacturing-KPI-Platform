namespace GDIKPI.DTO.Defetcs
{
    public class DefectRegistrationDTO
    {
        public int ProductionLineId { get; set; }
        public int ProgramId { get; set; }
        public string? ProgramDescription { get; set; }
        public string TimeInterval { get; set; } = null!;
        public int TotalVolantes { get; set; }
        public List<DefectDetailDTO> Defects { get; set; } = null!;
    }
}

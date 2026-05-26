namespace GDIKPI.DTO.Defetcs
{
    public class UpdateDefectsDTO
    {
        public int ProductionLineId { get; set; }
        public int ProgramId { get; set; }
        public string TimeInterval { get; set; }
        public int TotalVolantes { get; set; }
        public List<DefectDetailDTO> Defects { get; set; }
        public bool IsUpdate { get; set; }
        public int? ExistingRecordId { get; set; }
    }
}

namespace GDIKPI.DTO.ProductionDatum
{
    public class AbsenteeismDTO
    {
        public int ProductionLineId { get; set; }
        public int EmployeeNumber { get; set; }
        public List<AbsenteeismCategoryDTO> Categories { get; set; } = new List<AbsenteeismCategoryDTO>();
    }
}

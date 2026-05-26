namespace GDIKPI.DTO.DefectsQr
{
    public class DefectsQrPageViewModel
    {
        public int SelectedAreaId { get; set; }
        public string SelectedAreaName { get; set; } = string.Empty;
        public List<AreaOptionDto> Areas { get; set; } = new();
        public List<DefectQrItemDto> Defects { get; set; } = new();
        public List<CommandQrItemDto> Commands { get; set; } = new();
    }

    public class AreaOptionDto
    {
        public int AreaId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class DefectQrItemDto
    {
        public int DefectId { get; set; }
        public string DefectName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string QrValue { get; set; } = string.Empty;
    }

    public class CommandQrItemDto
    {
        public string CommandText { get; set; } = string.Empty;
        public string ActionKey { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string QrValue { get; set; } = string.Empty;
    }
}

using System;

namespace GDIKPI.Models;

public partial class DashboardProductionCardSetting
{
    public int DashboardProductionCardSettingId { get; set; }

    public string CardKey { get; set; } = string.Empty;

    public bool IsVisible { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime UpdatedAt { get; set; }
}

using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class ProductionLine
{
    public int ProductionLinesId { get; set; }

    public int? LineNumber { get; set; }

    public int DailyGoal { get; set; }

    public int PersonalQuantity { get; set; }

    public int ShiftNumber { get; set; }

    public int? AreaId { get; set; }

    public bool IsActive { get; set; }

    public string? LineName { get; set; }

    public decimal? StandardTime { get; set; }

    public virtual ICollection<Absenteeism> Absenteeisms { get; set; } = new List<Absenteeism>();

    public virtual Area? Area { get; set; }

    public virtual ICollection<Break> Breaks { get; set; } = new List<Break>();

    public virtual ICollection<DefectsDatum> DefectsData { get; set; } = new List<DefectsDatum>();

    public virtual ICollection<DowntimeEvent> DowntimeEvents { get; set; } = new List<DowntimeEvent>();

    public virtual ICollection<Efficiency> Efficiencies { get; set; } = new List<Efficiency>();

    public virtual ICollection<ProductionDatum> ProductionData { get; set; } = new List<ProductionDatum>();

    public virtual ICollection<Rejection> Rejections { get; set; } = new List<Rejection>();

    public virtual Shift ShiftNumberNavigation { get; set; } = null!;

    public virtual ICollection<UserLinePermission> UserLinePermissions { get; set; } = new List<UserLinePermission>();
}

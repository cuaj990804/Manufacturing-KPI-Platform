using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Defect
{
    public int DefectId { get; set; }

    public string DefectName { get; set; } = null!;

    public int AreaId { get; set; }

    public int? DefectCategoryId { get; set; }

    public virtual Area Area { get; set; } = null!;

    public virtual DefectsCategory? DefectCategory { get; set; }

    public virtual ICollection<DefectsDatum> DefectsData { get; set; } = new List<DefectsDatum>();
}

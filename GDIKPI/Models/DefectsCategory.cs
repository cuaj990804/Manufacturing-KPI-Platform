using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class DefectsCategory
{
    public int DefectCategoryId { get; set; }

    public string DefectCategoryName { get; set; } = null!;

    public virtual ICollection<Defect> Defects { get; set; } = new List<Defect>();
}

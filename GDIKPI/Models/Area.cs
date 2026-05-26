using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Area
{
    public int AreaId { get; set; }

    public string AreaName { get; set; } = null!;

    public string CustomerName { get; set; } = null!;

    public virtual ICollection<Defect> Defects { get; set; } = new List<Defect>();

    public virtual ICollection<ProductionLine> ProductionLines { get; set; } = new List<ProductionLine>();

    public virtual ICollection<ProductionOperator> ProductionOperators { get; set; } = new List<ProductionOperator>();

    public virtual ICollection<UserAreaPermission> UserAreaPermissions { get; set; } = new List<UserAreaPermission>();
}

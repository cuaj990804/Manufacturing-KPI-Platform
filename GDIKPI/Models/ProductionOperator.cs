using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class ProductionOperator
{
    public int OperatorId { get; set; }

    public int EmployeeNumber { get; set; }

    public string? NameOperator { get; set; }

    public string? LastnameOperator { get; set; }

    public int? AreaId { get; set; }

    public string? Operation { get; set; }

    public int? Goal { get; set; }

    public bool? Active { get; set; }

    public virtual Area? Area { get; set; }

    public virtual ICollection<ProductionOperatorsScan> ProductionOperatorsScans { get; set; } = new List<ProductionOperatorsScan>();
}

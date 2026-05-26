using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class ProductionOperatorsScan
{
    public int Id { get; set; }

    public int OperatorId { get; set; }

    public string? Code { get; set; }

    public DateTime? ScannedAt { get; set; }

    public virtual ProductionOperator Operator { get; set; } = null!;
}

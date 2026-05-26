using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class UserLinePermission
{
    public int UserLinePermissionId { get; set; }

    public string EmployeeNumber { get; set; } = null!;

    public int ProductionLinesId { get; set; }

    public int PermissionId { get; set; }

    public virtual User EmployeeNumberNavigation { get; set; } = null!;

    public virtual Permission Permission { get; set; } = null!;

    public virtual ProductionLine ProductionLines { get; set; } = null!;
}

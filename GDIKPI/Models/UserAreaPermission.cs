using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class UserAreaPermission
{
    public int UserAreaPermissionId { get; set; }

    public string EmployeeNumber { get; set; } = null!;

    public int AreaId { get; set; }

    public int PermissionId { get; set; }

    public virtual Area Area { get; set; } = null!;

    public virtual User EmployeeNumberNavigation { get; set; } = null!;

    public virtual Permission Permission { get; set; } = null!;
}

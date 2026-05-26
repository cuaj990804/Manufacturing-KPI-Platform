using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class UserModulePermission
{
    public int UserModulePermissionId { get; set; }

    public string EmployeeNumber { get; set; } = null!;

    public int ModuleId { get; set; }

    public int PermissionId { get; set; }

    public virtual User EmployeeNumberNavigation { get; set; } = null!;

    public virtual Module Module { get; set; } = null!;

    public virtual Permission Permission { get; set; } = null!;
}

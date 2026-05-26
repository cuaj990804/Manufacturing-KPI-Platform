using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class User
{
    public string EmployeeNumber { get; set; } = null!;

    public string? Name { get; set; }

    public string? Role { get; set; }

    public string? Area { get; set; }

    public virtual ICollection<Absenteeism> Absenteeisms { get; set; } = new List<Absenteeism>();

    public virtual ICollection<UserAreaPermission> UserAreaPermissions { get; set; } = new List<UserAreaPermission>();

    public virtual ICollection<UserLinePermission> UserLinePermissions { get; set; } = new List<UserLinePermission>();

    public virtual ICollection<UserModulePermission> UserModulePermissions { get; set; } = new List<UserModulePermission>();
}

using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Permission
{
    public int PermissionId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<UserAreaPermission> UserAreaPermissions { get; set; } = new List<UserAreaPermission>();

    public virtual ICollection<UserLinePermission> UserLinePermissions { get; set; } = new List<UserLinePermission>();

    public virtual ICollection<UserModulePermission> UserModulePermissions { get; set; } = new List<UserModulePermission>();
}

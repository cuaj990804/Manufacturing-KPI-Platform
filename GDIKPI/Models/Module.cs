using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Module
{
    public int ModuleId { get; set; }

    public string ModuleName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public string? Route { get; set; }

    public virtual ICollection<UserModulePermission> UserModulePermissions { get; set; } = new List<UserModulePermission>();
}

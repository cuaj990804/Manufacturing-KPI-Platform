using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class VwUserPermission
{
    public int EmployeeNumber { get; set; }

    public string? Name { get; set; }

    public string Area { get; set; } = null!;

    public int? LineNumber { get; set; }

    public string? Permissions { get; set; }

    public int AreaId { get; set; }

    public string AccessType { get; set; } = null!;
}

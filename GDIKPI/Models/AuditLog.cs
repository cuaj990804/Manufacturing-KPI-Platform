using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class AuditLog
{
    public int AuditLogId { get; set; }

    public string? EmployeeNumber { get; set; }

    public string? ActionType { get; set; }

    public string? EntityName { get; set; }

    public int? EntityId { get; set; }

    public DateTime? Timestamp { get; set; }

    public string? Details { get; set; }
}

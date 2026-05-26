using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Command
{
    public int CommandId { get; set; }

    public string CommandText { get; set; } = null!;

    public string ActionKey { get; set; } = null!;

    public string Scope { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

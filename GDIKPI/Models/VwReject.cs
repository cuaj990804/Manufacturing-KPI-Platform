using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class VwReject
{
    public DateOnly? Fecha { get; set; }

    public int? NumeroSemana { get; set; }

    public string Category { get; set; } = null!;

    public int? Linea { get; set; }

    public string Area { get; set; } = null!;

    public string DefectName { get; set; } = null!;

    public int ProgramId { get; set; }

    public string? ProgramDescription { get; set; }

    public int? Cantidad { get; set; }
}

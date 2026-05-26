using System;
using System.Collections.Generic;

namespace GDIKPI.Models;

public partial class Efficiency
{
    public int Id { get; set; }

    public DateTime RegistrationDate { get; set; }

    public DateOnly DateData { get; set; }

    public TimeOnly TimeData { get; set; }

    public int ProductionLinesId { get; set; }

    public string LineNumber { get; set; } = null!;

    public string? AreaCustomerName { get; set; }

    public decimal EfficiencyPercentage { get; set; }

    public int PeopleQuantity { get; set; }

    public decimal TiempoEstandar { get; set; }

    public decimal? HorasUtilizadas { get; set; }

    public decimal? HorasGanadas { get; set; }

    public int ProducedPieces { get; set; }

    public virtual ProductionLine ProductionLines { get; set; } = null!;
}

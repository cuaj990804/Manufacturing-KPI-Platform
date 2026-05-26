using GDIKPI.Data;
using GDIKPI.Models;

namespace GDIKPI.Services
{
    public class OEEService
    {
        private readonly KpisContext _context;

        public OEEService(KpisContext context)
        {
            _context = context;
        }

        public (int? LineId, int? DailyGoal) GetLineData(string area, int lineNumber)
        {
            var splitString = area.Split(" - ");
            if (splitString.Length != 2) return (null, null);

            var customer = splitString[0].Trim();
            var areaName = splitString[1].Trim();

            // Buscar en la tabla Areas
            var areaRecord = _context.Areas
                .FirstOrDefault(x => x.AreaName == areaName && x.CustomerName == customer);

            if (areaRecord == null) return (null, null);

            // Buscar en ProductionLines usando AreaId
            var line = _context.ProductionLines
                .FirstOrDefault(l => l.AreaId == areaRecord.AreaId && l.LineNumber == lineNumber);

            return (line?.ProductionLinesId, line?.DailyGoal);
        }

        public List<(int LineId, int DailyGoal)> GetProductionLinesByArea(string area)
        {
            var splitString = area.Split(" - ");
            if (splitString.Length != 2) return new List<(int, int)>();

            var customer = splitString[0].Trim();
            var areaName = splitString[1].Trim();

            // Buscar en la tabla Areas
            var areaRecord = _context.Areas
                .FirstOrDefault(x => x.AreaName == areaName && x.CustomerName == customer);

            if (areaRecord == null) return new List<(int, int)>();

            // Buscar en ProductionLines usando AreaId y proyectar LineId + DailyGoal
            var lines = _context.ProductionLines
                .Where(l => l.AreaId == areaRecord.AreaId && l.IsActive)
                .Select(l => new { l.ProductionLinesId, l.DailyGoal })
                .ToList();

            // Convertir a lista de tuplas
            return lines
                .Select(l => (l.ProductionLinesId, l.DailyGoal))
                .ToList();
        }

        public int GetProductionQuantity(int? lineId, DateOnly dateStart, DateOnly? dateEnd)
        {
            if (lineId == null || dateEnd == null)
                return 0;

            var totalProduction = _context.ProductionData
                .Where(p => p.ProductionLinesId == lineId.Value
                            && p.ProductionDate >= dateStart
                            && p.ProductionDate <= dateEnd.Value)
                .Sum(p => (int?)(p.ProducedPieces)) ?? 0;

            return totalProduction;
        }

        public int GetRejectsQuantity(int? lineId, DateOnly dateStart, DateOnly? dateEnd)
        {
            if (lineId == null)
                return 0;

            // Convertir DateOnly a DateTime para comparaciones
            DateTime startDateTime = dateStart.ToDateTime(TimeOnly.MinValue);
            DateTime endDateTime = dateEnd.HasValue ? dateEnd.Value.ToDateTime(TimeOnly.MaxValue) : DateTime.MaxValue;

            // Sumar los defectos (DefectQuantity) dentro del rango de fechas
            var totalRejected = _context.Rejections
                .Where(r => r.ProductionLinesId == lineId.Value
                            && r.StartTime >= startDateTime
                            && r.EndTime <= endDateTime)
                .Sum(r => (int?)r.DefectQuantity) ?? 0;

            return totalRejected;
        }

        // NUEVO: Método para calcular el número de días en el rango
        private int GetDaysInRange(DateOnly dateStart, DateOnly dateEnd)
        {
            int workingDays = 0;
            var currentDate = dateStart;

            while (currentDate <= dateEnd)
            {
                // Obtener día de la semana (0=Domingo, 1=Lunes, ..., 6=Sábado)
                var dayOfWeek = currentDate.DayOfWeek;

                // Solo contar días laborables (Lunes a Viernes)
                if (dayOfWeek != DayOfWeek.Saturday && dayOfWeek != DayOfWeek.Sunday)
                {
                    workingDays++;
                }

                currentDate = currentDate.AddDays(1);
            }

            return workingDays;
        }

        // MODIFICADO: Ahora considera múltiples días
        public int? PlannedTime(int? lineId, DateOnly dateStart, DateOnly dateEnd)
        {
            if (!lineId.HasValue) return null;

            // Calcular número de días en el rango
            int daysInRange = GetDaysInRange(dateStart, dateEnd);

            // Obtener el número de turno de la línea
            var shiftNumber = _context.ProductionLines
                .Where(l => l.ProductionLinesId == lineId.Value)
                .Select(l => l.ShiftNumber)
                .FirstOrDefault();

            // Obtener el turno
            var shift = _context.Shifts
                .Where(s => s.ShiftNumber == shiftNumber)
                .Select(s => new { s.StartTime, s.EndTime }) // TimeOnly?
                .FirstOrDefault();

            if (shift == null || !shift.StartTime.HasValue || !shift.EndTime.HasValue)
                return null;

            // Calcular duración total del turno en minutos por día
            var duration = shift.EndTime.Value.ToTimeSpan() - shift.StartTime.Value.ToTimeSpan();
            if (duration.TotalMinutes < 0)
                duration = duration.Add(TimeSpan.FromHours(24));

            int dailyMinutes = (int)duration.TotalMinutes;

            // Obtener los breaks del turno por día
            var breaks = _context.Breaks
                .Where(b => b.ProductionLinesId == lineId.Value)
                .Select(b => new { b.BreakStart, b.BreakEnd }) // TimeOnly?
                .ToList();

            int dailyBreakMinutes = 0;
            foreach (var b in breaks)
            {
                // Ignorar breaks nulos
                if (!b.BreakStart.HasValue || !b.BreakEnd.HasValue)
                    continue;

                var breakDuration = b.BreakEnd.Value.ToTimeSpan() - b.BreakStart.Value.ToTimeSpan();
                if (breakDuration.TotalMinutes < 0)
                    breakDuration = breakDuration.Add(TimeSpan.FromHours(24));

                dailyBreakMinutes += (int)breakDuration.TotalMinutes;
            }

            // Restar los minutos de breaks al turno diario
            int netDailyMinutes = dailyMinutes - dailyBreakMinutes;

            // Multiplicar por el número de días
            int totalMinutes = (netDailyMinutes > 0 ? netDailyMinutes : 0) * daysInRange;

            return totalMinutes;
        }

        public int? OperatingTime(int? lineId, DateOnly? dateStart, DateOnly? dateEnd)
        {
            if (!lineId.HasValue) return null;

            // Convertir DateOnly a DateTime para comparaciones
            DateTime startDateTime = dateStart.HasValue ? dateStart.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue;
            DateTime endDateTime = dateEnd.HasValue ? dateEnd.Value.ToDateTime(TimeOnly.MaxValue) : DateTime.MaxValue;

            // Obtener downtimes (cerrados o abiertos)
            var downtimes = _context.DowntimeEvents
                .Where(d => d.ProductionLinesId == lineId.Value
                            && d.StartTime <= endDateTime) // empezó antes del fin del rango
                .Select(d => new { d.StartTime, d.EndTime, d.Status })
                .ToList();

            int totalDowntimeMinutes = 0;

            foreach (var dt in downtimes)
            {
                if (dt.StartTime == null) continue;

                DateTime effectiveEnd;

                if (dt.EndTime.HasValue)
                {
                    // Evento cerrado
                    effectiveEnd = dt.EndTime.Value;
                }
                else if (dt.Status == "Open")
                {
                    // Evento abierto → tomar hasta la fecha de corte (o hasta ahora si es menor)
                    effectiveEnd = endDateTime < DateTime.Now ? endDateTime : DateTime.Now;
                }
                else
                {
                    continue;
                }

                // Validar solapamiento con rango consultado
                if (effectiveEnd < startDateTime) continue;

                DateTime realStart = dt.StartTime < startDateTime ? startDateTime : dt.StartTime;
                DateTime realEnd = effectiveEnd > endDateTime ? endDateTime : effectiveEnd;

                if (realEnd > realStart)
                {
                    var duration = realEnd - realStart;
                    totalDowntimeMinutes += (int)duration.TotalMinutes;
                }
            }

            return totalDowntimeMinutes > 0 ? totalDowntimeMinutes : 0;
        }

        // Método principal que calcula OEE de una línea - CORREGIDO PARA MÚLTIPLES DÍAS
        public (double disponibilidad, double rendimiento, double calidad, double oee, int operacionTime, int totalPieces, int totalRejects, int goal, int downtime)? GetOEESingleLine(string area, int lineNumber, DateOnly dateStart, DateOnly dateEnd)
        {
            var (lineId, dailyGoal) = GetLineData(area, lineNumber);
            if (lineId == null || !dailyGoal.HasValue) return null;

            var line = _context.ProductionLines.FirstOrDefault(l => l.ProductionLinesId == lineId);
            if (line == null) return null;

            // Calcular días en el rango
            int daysInRange = GetDaysInRange(dateStart, dateEnd);

            // Calcular meta total para el período
            int totalGoalForPeriod = dailyGoal.Value * daysInRange;

            int plannedMinutes = PlannedTime(lineId, dateStart, dateEnd) ?? 0;
            int downtime = OperatingTime(lineId, dateStart, dateEnd) ?? 0;
            int operatingMinutes = plannedMinutes - downtime;
            int totalPieces = GetProductionQuantity(lineId, dateStart, dateEnd);
            int totalRejects = GetRejectsQuantity(lineId, dateStart, dateEnd);

            // Calcular piezas por minuto basado en la meta total del período
            double piecesPerMinute = plannedMinutes > 0 ? (double)totalGoalForPeriod / plannedMinutes : 0;

            // Validaciones para evitar división por cero
            if (plannedMinutes == 0 || operatingMinutes <= 0 || totalPieces == 0)
                return (0, 0, 0, 0, 0, 0, 0, totalGoalForPeriod, downtime);

            double availability = (double)operatingMinutes / plannedMinutes;
            double performance = piecesPerMinute > 0 ? (double)totalPieces / (operatingMinutes * piecesPerMinute) : 0;
            double quality = (double)(totalPieces - totalRejects) / totalPieces;
            double oee = availability * performance * quality;

            return (availability, performance, quality, oee, operatingMinutes, totalPieces, totalRejects, totalGoalForPeriod, downtime);
        }

        // Método principal que calcula OEE por Area - CORREGIDO PARA MÚLTIPLES DÍAS
        public (double disponibilidad, double rendimiento, double calidad, double oee, int totalOperatingTime, int totalPieces, int totalRejects, int totalGoal, int totalDowntime)? GetOEEArea(string area, DateOnly dateStart, DateOnly dateEnd)
        {
            var lines = GetProductionLinesByArea(area);
            if (lines == null || !lines.Any()) return null;

            // Calcular días en el rango
            int daysInRange = GetDaysInRange(dateStart, dateEnd);

            double totalAvailability = 0, totalPerformance = 0, totalQuality = 0;
            int count = 0;
            int totalOperatingMinutes = 0;
            int totalPiecesProduced = 0;
            int totalRejectedPieces = 0;
            int totalGoalSum = 0;
            int totalDowntimeSum = 0;

            foreach (var (lineId, dailyGoal) in lines)
            {
                // Calcular meta total para el período por línea
                int lineGoalForPeriod = dailyGoal * daysInRange;

                int plannedMinutes = PlannedTime(lineId, dateStart, dateEnd) ?? 0;
                int downtime = OperatingTime(lineId, dateStart, dateEnd) ?? 0;
                int operatingMinutes = plannedMinutes - downtime;
                int totalPieces = GetProductionQuantity(lineId, dateStart, dateEnd);
                int totalRejects = GetRejectsQuantity(lineId, dateStart, dateEnd);

                // Siempre sumar goal y downtime, incluso si la línea no está operativa
                totalGoalSum += lineGoalForPeriod;
                totalDowntimeSum += downtime;

                if (plannedMinutes == 0 || operatingMinutes <= 0 || totalPieces == 0)
                    continue;

                double piecesPerMinute = (double)lineGoalForPeriod / plannedMinutes;

                double availability = (double)operatingMinutes / plannedMinutes;
                double performance = (double)totalPieces / (operatingMinutes * piecesPerMinute);
                double quality = (double)(totalPieces - totalRejects) / totalPieces;

                totalAvailability += availability;
                totalPerformance += performance;
                totalQuality += quality;

                // Acumular valores totales
                totalOperatingMinutes += operatingMinutes;
                totalPiecesProduced += totalPieces;
                totalRejectedPieces += totalRejects;

                count++;
            }

            if (count == 0) return (0, 0, 0, 0, 0, 0, 0, totalGoalSum, totalDowntimeSum);

            double avgAvailability = totalAvailability / count;
            double avgPerformance = totalPerformance / count;
            double avgQuality = totalQuality / count;
            double oee = avgAvailability * avgPerformance * avgQuality;

            return (avgAvailability, avgPerformance, avgQuality, oee, totalOperatingMinutes, totalPiecesProduced, totalRejectedPieces, totalGoalSum, totalDowntimeSum);
        }

        // Método principal que calcula OEE general - CORREGIDO PARA MÚLTIPLES DÍAS
        public (double disponibilidad, double rendimiento, double calidad, double oee, int totalOperatingTime, int totalPieces, int totalRejects, int totalGoal, int totalDowntime)? GetOEEOverall(DateOnly dateStart, DateOnly dateEnd)
        {
            var lines = _context.ProductionLines
                .Where(l => l.IsActive)
                .Select(l => new { l.ProductionLinesId, l.DailyGoal })
                .ToList()
                .Select(l => (l.ProductionLinesId, l.DailyGoal))
                .ToList();
            if (lines == null || !lines.Any()) return null;

            // Calcular días en el rango
            int daysInRange = GetDaysInRange(dateStart, dateEnd);

            double totalAvailability = 0, totalPerformance = 0, totalQuality = 0;
            int count = 0;
            int totalOperatingMinutes = 0;
            int totalPiecesProduced = 0;
            int totalRejectedPieces = 0;
            int totalGoalSum = 0;
            int totalDowntimeSum = 0;

            foreach (var (lineId, dailyGoal) in lines)
            {
                // Calcular meta total para el período por línea
                int lineGoalForPeriod = dailyGoal * daysInRange;

                int plannedMinutes = PlannedTime(lineId, dateStart, dateEnd) ?? 0;
                int downtime = OperatingTime(lineId, dateStart, dateEnd) ?? 0;
                int operatingMinutes = plannedMinutes - downtime;
                int totalPieces = GetProductionQuantity(lineId, dateStart, dateEnd);
                int totalRejects = GetRejectsQuantity(lineId, dateStart, dateEnd);

                // Siempre sumar goal y downtime, incluso si la línea no está operativa
                totalGoalSum += lineGoalForPeriod;
                totalDowntimeSum += downtime;

                if (plannedMinutes == 0 || operatingMinutes <= 0 || totalPieces == 0)
                    continue;

                double piecesPerMinute = (double)lineGoalForPeriod / plannedMinutes;

                double availability = (double)operatingMinutes / plannedMinutes;
                double performance = (double)totalPieces / (operatingMinutes * piecesPerMinute);
                double quality = (double)(totalPieces - totalRejects) / totalPieces;

                totalAvailability += availability;
                totalPerformance += performance;
                totalQuality += quality;

                // Acumular valores totales
                totalOperatingMinutes += operatingMinutes;
                totalPiecesProduced += totalPieces;
                totalRejectedPieces += totalRejects;
                count++;
            }

            if (count == 0) return (0, 0, 0, 0, 0, 0, 0, totalGoalSum, totalDowntimeSum);

            double avgAvailability = totalAvailability / count;
            double avgPerformance = totalPerformance / count;
            double avgQuality = totalQuality / count;
            double oee = avgAvailability * avgPerformance * avgQuality;

            return (avgAvailability, avgPerformance, avgQuality, oee, totalOperatingMinutes, totalPiecesProduced, totalRejectedPieces, totalGoalSum, totalDowntimeSum);
        }

        // Método dinámico - CORREGIDO
        public (double disponibilidad, double rendimiento, double calidad, double oee, int totalOperatingTime, int totalPieces, int totalRejects, int goal, int downtime)? GetDynamicOEE(
            string? areaFilter,
            int? lineFilter,
            DateOnly dateStart,
            DateOnly? dateEnd = null)
        {
            var endDate = dateEnd ?? dateStart;

            // Caso 1: Área y línea específicas (OEE de línea individual)
            if (lineFilter.HasValue && lineFilter > 0)
            {
                return GetOEESingleLine(areaFilter, lineFilter.Value, dateStart, endDate);
            }
            // Caso 2: Solo área (OEE de toda el área)
            else if (!string.IsNullOrEmpty(areaFilter))
            {
                return GetOEEArea(areaFilter, dateStart, endDate);
            }
            // Caso 3: Sin filtros de área ni línea (OEE de toda la planta)
            else
            {
                return GetOEEOverall(dateStart, endDate);
            }
        }

        public List<(string Label, double OeeValue)> GetHistoricalOEEData(
            string? areaFilter,
            int? lineFilter,
            DateOnly startDate,
            DateOnly endDate,
            int periodCount = 6)
        {
            var result = new List<(string Label, double OeeValue)>();

            // Calcular el rango total en días
            int totalDays = (endDate.DayNumber - startDate.DayNumber) + 1;

            // Si el rango es muy pequeño, usar días individuales
            if (totalDays <= periodCount)
            {
                for (int i = 0; i < totalDays; i++)
                {
                    var currentDate = startDate.AddDays(i);
                    // Usar GetOEEWithHistory para obtener datos del historial primero
                    var oeeData = GetOEEWithHistory(areaFilter, lineFilter, currentDate, currentDate);

                    string label = currentDate.ToString("MMM dd");
                    double oeeValue = oeeData?.oee ?? 0;

                    result.Add((label, oeeValue * 100)); // Convertir a porcentaje
                }
            }
            else
            {
                // Para rangos más grandes, dividir en períodos
                int daysPerPeriod = totalDays / periodCount;
                if (daysPerPeriod == 0) daysPerPeriod = 1;

                for (int i = 0; i < periodCount; i++)
                {
                    var periodStart = startDate.AddDays(i * daysPerPeriod);
                    var periodEnd = (i == periodCount - 1)
                        ? endDate
                        : startDate.AddDays((i + 1) * daysPerPeriod - 1);

                    // Evitar que el período exceda la fecha final
                    if (periodEnd > endDate) periodEnd = endDate;

                    // Usar GetOEEWithHistory para obtener datos del historial primero
                    var oeeData = GetOEEWithHistory(areaFilter, lineFilter, periodStart, periodEnd);

                    string label;
                    if (daysPerPeriod == 1)
                    {
                        label = periodStart.ToString("MMM dd");
                    }
                    else if (daysPerPeriod <= 7)
                    {
                        label = $"{periodStart:MMM dd} - {periodEnd:MMM dd}";
                    }
                    else if (daysPerPeriod <= 31)
                    {
                        label = periodStart.ToString("MMM yyyy");
                    }
                    else
                    {
                        label = $"{periodStart:MMM} - {periodEnd:MMM yyyy}";
                    }

                    double oeeValue = oeeData?.oee ?? 0;
                    result.Add((label, oeeValue * 100)); // Convertir a porcentaje
                }
            }

            return result;
        }


        public List<(string Label, double OeeValue)> GetMonthlyOEEData(
            string? areaFilter,
            int? lineFilter,
            DateOnly startDate,
            DateOnly endDate)
        {
            var result = new List<(string Label, double OeeValue)>();

            var currentDate = new DateOnly(startDate.Year, startDate.Month, 1); // Primer día del mes
            var finalDate = new DateOnly(endDate.Year, endDate.Month, DateTime.DaysInMonth(endDate.Year, endDate.Month));

            while (currentDate <= finalDate)
            {
                var monthStart = currentDate;
                var monthEnd = new DateOnly(currentDate.Year, currentDate.Month,
                    DateTime.DaysInMonth(currentDate.Year, currentDate.Month));

                // Ajustar al rango solicitado
                if (monthStart < startDate) monthStart = startDate;
                if (monthEnd > endDate) monthEnd = endDate;

                // Usar GetOEEWithHistory para obtener datos del historial primero
                var oeeData = GetOEEWithHistory(areaFilter, lineFilter, monthStart, monthEnd);

                string label = currentDate.ToString("MMM yyyy");
                double oeeValue = oeeData?.oee ?? 0;

                result.Add((label, oeeValue * 100));

                // Avanzar al siguiente mes
                currentDate = currentDate.AddMonths(1);
            }

            return result;
        }

        // Método para obtener OEE desde el historial para un día específico
        public (double disponibilidad, double rendimiento, double calidad, double oee, int operatingTime, int totalPieces, int totalRejects, int goal, int downtime)? GetOEEFromHistory(
            string? areaFilter,
            int? lineFilter,
            DateOnly date)
        {
            IQueryable<Oeehistory> query = _context.Oeehistories.Where(h => h.DateData == date);

            // Buscar ProductionLinesId basado en área y número de línea
            int? productionLineId = null;
            int? areaId = null;

            if (!string.IsNullOrEmpty(areaFilter))
            {
                var parts = areaFilter.Split(" - ");
                var customer = parts[0].Trim();
                var areaName = parts.Length > 1 ? parts[1].Trim() : "";

                var areaRecord = _context.Areas
                    .FirstOrDefault(a => a.CustomerName == customer && a.AreaName == areaName);

                if (areaRecord != null)
                {
                    areaId = areaRecord.AreaId;

                    if (lineFilter.HasValue && lineFilter > 0)
                    {
                        // Buscar ProductionLinesId específico
                        productionLineId = _context.ProductionLines
                            .Where(l => l.AreaId == areaId && l.LineNumber == lineFilter.Value)
                            .Select(l => l.ProductionLinesId)
                            .FirstOrDefault();
                    }
                }
            }

            // Aplicar filtros por ID
            if (productionLineId.HasValue && productionLineId > 0)
            {
                // Filtro por línea específica usando ProductionLinesId
                query = query.Where(h => h.ProductionLinesId == productionLineId);
            }
            else if (areaId.HasValue)
            {
                // Filtro por área usando AreaCustomerName
                query = query.Where(h => h.AreaCustomerName == areaFilter);
            }
            else if (lineFilter.HasValue && lineFilter > 0)
            {
                // Filtro solo por LineNumber si no se encontró el área
                query = query.Where(h => h.LineNumber == lineFilter.Value.ToString());
            }

            var historyRecords = query.ToList();

            if (!historyRecords.Any())
                return null;

            // Filtrar solo registros con producción válida (piezas producidas > 0)
            var validRecords = historyRecords.Where(h => (h.ProducedPieces ?? 0) > 0).ToList();

            if (!validRecords.Any())
                return null;

            // Calcular promedios de los registros válidos del historial
            double avgAvailability = validRecords.Average(h => (double)(h.AvailabilityPercentage ?? 0));
            double avgPerformance = validRecords.Average(h => (double)(h.PerformancePercentage ?? 0));
            double avgQuality = validRecords.Average(h => (double)(h.QualityPercentage ?? 0));
            double avgOee = validRecords.Average(h => (double)(h.OeePercentage ?? 0));

            int totalOperatingTime = validRecords.Sum(h => h.OperatingMinutes ?? 0);
            int totalPieces = validRecords.Sum(h => h.ProducedPieces ?? 0);
            int totalRejects = validRecords.Sum(h => h.RejectedPieces ?? 0);
            int totalGoal = validRecords.Sum(h => h.RequirementGoalPieces ?? 0);
            int totalDowntime = validRecords.Sum(h => h.DowntimeMinutes ?? 0);

            // Convertir porcentajes de decimal a double (0-1)
            return (
                avgAvailability / 100,
                avgPerformance / 100,
                avgQuality / 100,
                avgOee / 100,
                totalOperatingTime,
                totalPieces,
                totalRejects,
                totalGoal,
                totalDowntime
            );
        }

        // Método mejorado que usa historial para días pasados y cálculo en tiempo real para el día actual
        public (double disponibilidad, double rendimiento, double calidad, double oee, int totalOperatingTime, int totalPieces, int totalRejects, int goal, int downtime)? GetOEEWithHistory(
            string? areaFilter,
            int? lineFilter,
            DateOnly dateStart,
            DateOnly dateEnd)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            // Si el rango es solo el día actual, calcular en tiempo real
            if (dateStart == today && dateEnd == today)
            {
                return GetDynamicOEE(areaFilter, lineFilter, dateStart, dateEnd);
            }

            // Si el rango es un solo día anterior, intentar obtener del historial
            if (dateStart == dateEnd && dateStart < today)
            {
                var historyData = GetOEEFromHistory(areaFilter, lineFilter, dateStart);
                if (historyData.HasValue)
                {
                    return historyData;
                }
                // Si no hay en historial, calcular
                return GetDynamicOEE(areaFilter, lineFilter, dateStart, dateEnd);
            }

            // Para rangos de múltiples días, combinar datos del historial y cálculos en tiempo real
            var currentDate = dateStart;
            var combinedData = new List<(double disponibilidad, double rendimiento, double calidad, double oee, int operatingTime, int totalPieces, int totalRejects, int goal, int downtime)>();

            while (currentDate <= dateEnd)
            {
                // Solo procesar días laborales (lunes a viernes)
                var dayOfWeek = currentDate.DayOfWeek;
                if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                {
                    currentDate = currentDate.AddDays(1);
                    continue;
                }

                (double disponibilidad, double rendimiento, double calidad, double oee, int operatingTime, int totalPieces, int totalRejects, int goal, int downtime)? dayData;

                if (currentDate < today)
                {
                    // Intentar obtener del historial primero
                    dayData = GetOEEFromHistory(areaFilter, lineFilter, currentDate);
                    // Si no hay en historial, NO calcular con GetDynamicOEE para evitar datos incorrectos
                }
                else
                {
                    // Para el día actual, siempre calcular en tiempo real
                    dayData = GetDynamicOEE(areaFilter, lineFilter, currentDate, currentDate);
                }

                // Solo agregar si hay datos válidos (tiene piezas producidas o OEE > 0)
                if (dayData.HasValue && (dayData.Value.totalPieces > 0 || dayData.Value.oee > 0))
                {
                    combinedData.Add(dayData.Value);
                }

                currentDate = currentDate.AddDays(1);
            }

            if (!combinedData.Any())
                return null;

            // Calcular promedios ponderados
            double avgAvailability = combinedData.Average(d => d.disponibilidad);
            double avgPerformance = combinedData.Average(d => d.rendimiento);
            double avgQuality = combinedData.Average(d => d.calidad);
            double avgOee = combinedData.Average(d => d.oee);

            int totalOperatingTime = combinedData.Sum(d => d.operatingTime);
            int totalPieces = combinedData.Sum(d => d.totalPieces);
            int totalRejects = combinedData.Sum(d => d.totalRejects);
            int totalGoal = combinedData.Sum(d => d.goal);
            int totalDowntime = combinedData.Sum(d => d.downtime);

            return (avgAvailability, avgPerformance, avgQuality, avgOee, totalOperatingTime, totalPieces, totalRejects, totalGoal, totalDowntime);
        }
    }
}
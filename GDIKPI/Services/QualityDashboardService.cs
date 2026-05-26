using GDIKPI.Data;
using GDIKPI.Models;

namespace GDIKPI.Services
{
    public class QualityDashboardService
    {
        private readonly KpisContext _context;

        public QualityDashboardService(KpisContext context)
        {
            _context = context;
        }

        // Método corregido para GetRejectsQuantity - agregado filtro por programa
        public int GetRejectsQuantity(
            int? lineId,
            DateOnly dateStart,
            DateOnly? dateEnd,
            string? defectFilter = null,
            string? categoryFilter = null,
            string? programFilter = null,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            // Convertir DateOnly a DateTime para comparaciones
            DateTime startDateTime = dateStart.ToDateTime(TimeOnly.MinValue);
            DateTime endDateTime = dateEnd.HasValue ? dateEnd.Value.ToDateTime(TimeOnly.MaxValue) : DateTime.MaxValue;

            var query = _context.Rejections
                .Where(r => r.StartTime >= startDateTime && r.StartTime <= endDateTime);

            if (startTimeFilter.HasValue)
            {
                var startTimeSpan = startTimeFilter.Value.ToTimeSpan();
                query = query.Where(r => r.StartTime.TimeOfDay >= startTimeSpan);
            }

            if (endTimeFilter.HasValue)
            {
                var endTimeSpan = endTimeFilter.Value.ToTimeSpan();
                query = query.Where(r => r.StartTime.TimeOfDay <= endTimeSpan);
            }

            // Solo filtrar por línea si se especifica una línea
            if (lineId.HasValue && lineId.Value > 0)
            {
                query = query.Where(r => r.ProductionLinesId == lineId.Value);
            }

            // Aplicar filtro por defecto si se especifica
            if (!string.IsNullOrEmpty(defectFilter) && defectFilter != "Todos los defectos")
            {
                var defect = _context.Defects
                    .FirstOrDefault(d => d.DefectName.ToLower().Contains(defectFilter.ToLower()));

                if (defect != null)
                {
                    query = query.Where(r => r.DefectsData.Any(d => d.DefectId == defect.DefectId));
                }
            }

            // Aplicar filtro por categoría si se especifica
            if (!string.IsNullOrEmpty(categoryFilter) && categoryFilter != "Todos los procesos")
            {
                query = query.Where(r => r.Category != null &&
                                        r.Category.ToLower().Contains(categoryFilter.ToLower()));
            }

            // Aplicar filtro por programas múltiples si se especifica
            if (programFilter != null && !string.IsNullOrEmpty(programFilter) && programFilter != "Todos los programas")
            {
                if (int.TryParse(programFilter, out int programId))
                {
                    query = query.Where(r => r.ProgramId == programId);
                }
            }

            var totalRejected = query.Sum(r => (int?)r.DefectQuantity) ?? 0;
            return totalRejected;
        }

        // Método corregido para GetScrapQuantity - agregado filtro por programa
        public int GetScrapQuantity(
            int? lineId,
            DateOnly dateStart,
            DateOnly? dateEnd,
            string? defectFilter = null,
            string? categoryFilter = null,
            string? programFilter = null,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            // Convertir DateOnly a DateTime para comparaciones
            DateTime startDateTime = dateStart.ToDateTime(TimeOnly.MinValue);
            DateTime endDateTime = dateEnd.HasValue ? dateEnd.Value.ToDateTime(TimeOnly.MaxValue) : DateTime.MaxValue;

            var query = _context.Rejections
                .Where(r => r.StartTime >= startDateTime
                            && r.StartTime <= endDateTime
                            && r.Category == "Scrap");

            if (startTimeFilter.HasValue)
            {
                var startTimeSpan = startTimeFilter.Value.ToTimeSpan();
                query = query.Where(r => r.StartTime.TimeOfDay >= startTimeSpan);
            }

            if (endTimeFilter.HasValue)
            {
                var endTimeSpan = endTimeFilter.Value.ToTimeSpan();
                query = query.Where(r => r.StartTime.TimeOfDay <= endTimeSpan);
            }

            // Solo filtrar por línea si se especifica una línea
            if (lineId.HasValue && lineId.Value > 0)
            {
                query = query.Where(r => r.ProductionLinesId == lineId.Value);
            }

            // Aplicar filtro por defecto si se especifica
            if (!string.IsNullOrEmpty(defectFilter) && defectFilter != "Todos los defectos")
            {
                var defect = _context.Defects
                    .FirstOrDefault(d => d.DefectName.ToLower().Contains(defectFilter.ToLower()));

                if (defect != null)
                {
                    query = query.Where(r => r.DefectsData.Any(d => d.DefectId == defect.DefectId));
                }
            }

            // Aplicar filtro por programa si se especifica
            if (!string.IsNullOrEmpty(programFilter) && programFilter != "Todos los programas")
            {


                if (int.TryParse(programFilter, out int programId))
                {
                    query = query.Where(r => r.ProgramId == programId);
                }
            }

            var totalScrap = query.Sum(r => (int?)r.DefectQuantity) ?? 0;
            return totalScrap;
        }

        // Método corregido para GetProductionQuantity - agregado filtro por programa
        public int GetProductionQuantity(
            int? lineId,
            DateOnly dateStart,
            DateOnly? dateEnd,
            string? defectFilter = null,
            string? categoryFilter = null,
            string? programFilter = null,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            if (dateEnd == null)
                return 0;

            var query = _context.ProductionData
                .Where(p => p.ProductionDate >= dateStart && p.ProductionDate <= dateEnd.Value);

            if (startTimeFilter.HasValue)
            {
                query = query.Where(p => p.StartHour >= startTimeFilter.Value);
            }

            if (endTimeFilter.HasValue)
            {
                query = query.Where(p => p.StartHour <= endTimeFilter.Value);
            }

            // Solo filtrar por línea si se especifica una línea
            if (lineId.HasValue && lineId.Value > 0)
            {
                query = query.Where(p => p.ProductionLinesId == lineId.Value);
            }

            // Si quieres filtrar por proceso/categoría en la producción:
            if (!string.IsNullOrEmpty(categoryFilter) && categoryFilter != "Todos los procesos")
            {
                // Asumiendo que tienes un campo ProcessType en ProductionData
                // query = query.Where(p => p.ProcessType != null && 
                //                         p.ProcessType.ToLower().Contains(categoryFilter.ToLower()));
            }

            if (!string.IsNullOrEmpty(programFilter) && programFilter != "Todos los programas")
            {
                if (int.TryParse(programFilter, out int programId))
                {
                    query = query.Where(p => p.ProgramId == programId);
                }
            }





            var totalProduction = query.Sum(p => (int?)(p.ProducedPieces)) ?? 0;
            return totalProduction;
        }

        public int GetLineData(string area, int lineNumber)
        {
            if (string.IsNullOrEmpty(area))
                return 0;

            var splitString = area.Split(" - ");

            // Validar que el formato sea correcto
            if (splitString.Length < 2)
                return 0;

            var customer = splitString[0].Trim();
            var areaName = splitString[1].Trim();

            // Buscar en la tabla Areas
            var areaRecord = _context.Areas
                .FirstOrDefault(x => x.AreaName == areaName && x.CustomerName == customer);

            if (areaRecord == null)
                return 0;

            // Buscar en ProductionLines usando AreaId
            var line = _context.ProductionLines
                .FirstOrDefault(l => l.AreaId == areaRecord.AreaId && l.LineNumber == lineNumber);

            return line?.ProductionLinesId ?? 0; // Devuelve 0 si ProductionLinesId es null
        }

        public (int rejectsQty, int productionQty, int scrapQty, double scrapPercentage, double qualityPercentage, double reWorkPercentage)? GetQualityKpisSingleLine(
        string area,
        int lineNumber,
        DateOnly dateStart,
        DateOnly dateEnd,
        string? defectFilter = null,
        string? categoryFilter = null,
        List<string>? programFilters = null,
        TimeOnly? startTimeFilter = null,
        TimeOnly? endTimeFilter = null)
        {
            var lineId = GetLineData(area, lineNumber);

            var line = _context.ProductionLines.FirstOrDefault(l => l.ProductionLinesId == lineId);
            if (line == null) return null;

            int rejectsQty = 0;
            int productionQty = 0;
            int scrapQty = 0;

            // Si hay filtros de programas, sumar para cada uno
            if (programFilters != null && programFilters.Any())
            {
                foreach (var programFilter in programFilters)
                {
                    rejectsQty += GetRejectsQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);
                    productionQty += GetProductionQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);
                    scrapQty += GetScrapQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);
                }
            }
            else
            {
                // Sin filtro de programas
                rejectsQty = GetRejectsQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);
                productionQty = GetProductionQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);
                scrapQty = GetScrapQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);
            }

            double scrapPercentage = 0.0;
            double reWorkPercentage = 0.0;
            double qualityPercentage = 0.0;

            // Evitar división por cero y convertir a double para obtener decimales
            if (productionQty > 0)
            {
                scrapPercentage = ((double)scrapQty / productionQty);

                reWorkPercentage = ((double)rejectsQty / productionQty);
                qualityPercentage = ((double)(productionQty - scrapQty - rejectsQty) / productionQty);
            }

            return (rejectsQty, productionQty, scrapQty, scrapPercentage, qualityPercentage, reWorkPercentage);
        }

        // Método para obtener KPIs de múltiples líneas específicas
        public (int rejectsQty, int productionQty, int totalScrapQty, double scrapPercentage, double qualityPercentage, double reWorkPercentage)? GetQualityKpisMultipleLines(
            string area,
            List<int> lineNumbers,
            DateOnly dateStart,
            DateOnly dateEnd,
            string? defectFilter = null,
            string? categoryFilter = null,
            List<string>? programFilters = null,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            if (string.IsNullOrEmpty(area))
                return null;

            var splitString = area.Split(" - ");

            // Validar que el formato sea correcto
            if (splitString.Length < 2)
                return null;

            var customer = splitString[0].Trim();
            var areaName = splitString[1].Trim();

            // Buscar en la tabla Areas
            var areaRecord = _context.Areas
                .FirstOrDefault(x => x.AreaName == areaName && x.CustomerName == customer);

            if (areaRecord == null) return null;

            // Obtener solo las líneas especificadas del área
            var lineIds = _context.ProductionLines
                .Where(l => l.AreaId == areaRecord.AreaId && l.IsActive && lineNumbers.Contains(l.LineNumber ?? 0))
                .Select(l => l.ProductionLinesId)
                .ToList();

            if (!lineIds.Any()) return null;

            int totalProductionQty = 0;
            int totalRejectsQty = 0;
            int totalScrapQty = 0;

            // Si hay múltiples programas, iterar sobre cada uno
            if (programFilters != null && programFilters.Any())
            {
                foreach (var programFilter in programFilters)
                {
                    foreach (var lineId in lineIds)
                    {
                        int production = GetProductionQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);
                        int rejects = GetRejectsQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);
                        int scrap = GetScrapQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);

                        totalProductionQty += production;
                        totalRejectsQty += rejects;
                        totalScrapQty += scrap;
                    }
                }
            }
            else
            {
                // Si no hay programas seleccionados, sumar todos los datos sin filtro de programa
                foreach (var lineId in lineIds)
                {
                    int production = GetProductionQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);
                    int rejects = GetRejectsQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);
                    int scrap = GetScrapQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);

                    totalProductionQty += production;
                    totalRejectsQty += rejects;
                    totalScrapQty += scrap;
                }
            }

            // Calcular métricas
            double scrapPercentage = 0.0;
            double reWorkPercentage = 0.0;
            double qualityPercentage = 0.0;

            // Evitar división por cero
            if (totalProductionQty > 0)
            {
                scrapPercentage = ((double)totalScrapQty / totalProductionQty);
                reWorkPercentage = ((double)totalRejectsQty / totalProductionQty);
                qualityPercentage = ((double)(totalProductionQty - totalScrapQty - totalRejectsQty) / totalProductionQty);
            }

            return (totalRejectsQty, totalProductionQty, totalScrapQty, scrapPercentage, qualityPercentage, reWorkPercentage);
        }

        public (int rejectsQty, int productionQty, int totalScrapQty, double scrapPercentage, double qualityPercentage, double reWorkPercentage)? GetQualityKpisArea(
            string area,
            DateOnly dateStart,
            DateOnly dateEnd,
            string? defectFilter = null,
            string? categoryFilter = null,
            List<string>? programFilters = null,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            if (string.IsNullOrEmpty(area))
                return null;

            var splitString = area.Split(" - ");

            // Validar que el formato sea correcto
            if (splitString.Length < 2)
                return null;

            var customer = splitString[0].Trim();
            var areaName = splitString[1].Trim();

            // Buscar en la tabla Areas
            var areaRecord = _context.Areas
                .FirstOrDefault(x => x.AreaName == areaName && x.CustomerName == customer);

            if (areaRecord == null) return null;

            // Obtener todas las líneas del área
            var lineIds = _context.ProductionLines
                .Where(l => l.AreaId == areaRecord.AreaId && l.IsActive)
                .Select(l => l.ProductionLinesId)
                .ToList();

            if (!lineIds.Any()) return null;

            int totalProductionQty = 0;
            int totalRejectsQty = 0;
            int totalScrapQty = 0;

            // Si hay múltiples programas, iterar sobre cada uno
            if (programFilters != null && programFilters.Any())
            {
                foreach (var programFilter in programFilters)
                {
                    foreach (var lineId in lineIds)
                    {
                        int production = GetProductionQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);
                        int rejects = GetRejectsQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);
                        int scrap = GetScrapQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);

                        totalProductionQty += production;
                        totalRejectsQty += rejects;
                        totalScrapQty += scrap;
                    }
                }
            }
            else
            {
                // Si no hay programas seleccionados, sumar todos los datos sin filtro de programa
                foreach (var lineId in lineIds)
                {
                    int production = GetProductionQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);
                    int rejects = GetRejectsQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);
                    int scrap = GetScrapQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);

                    totalProductionQty += production;
                    totalRejectsQty += rejects;
                    totalScrapQty += scrap;
                }
            }

            // Calcular métricas
            double scrapPercentage = 0.0;
            double reWorkPercentage = 0.0;
            double qualityPercentage = 0.0;

            // Evitar división por cero
            if (totalProductionQty > 0)
            {
                // Calcular scrap percentage correctamente
                scrapPercentage = ((double)totalScrapQty / totalProductionQty);
                reWorkPercentage = ((double)totalRejectsQty / totalProductionQty);
                qualityPercentage = ((double)(totalProductionQty - totalScrapQty - totalRejectsQty) / totalProductionQty);
            }

            return (totalRejectsQty, totalProductionQty, totalScrapQty, scrapPercentage, qualityPercentage, reWorkPercentage);
        }

        public (int rejectsQty, int productionQty, int totalScrapQty, double scrapPercentage, double qualityPercentage, double reWorkPercentage)? GetQualityKpisOverall(
            DateOnly dateStart,
            DateOnly dateEnd,
            string? defectFilter = null,
            string? categoryFilter = null,
            List<string>? programFilters = null,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            // Obtener todas las líneas de la planta
            var lineIds = _context.ProductionLines
                .Where(l => l.IsActive)
                .Select(l => l.ProductionLinesId)
                .ToList();

            if (!lineIds.Any()) return null;

            int totalProductionQty = 0;
            int totalRejectsQty = 0;
            int totalScrapQty = 0;

            // Si hay múltiples programas, iterar sobre cada uno
            if (programFilters != null && programFilters.Any())
            {
                foreach (var programFilter in programFilters)
                {
                    foreach (var lineId in lineIds)
                    {
                        int production = GetProductionQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);
                        int rejects = GetRejectsQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);
                        int scrap = GetScrapQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, programFilter, startTimeFilter, endTimeFilter);

                        totalProductionQty += production;
                        totalRejectsQty += rejects;
                        totalScrapQty += scrap;
                    }
                }
            }
            else
            {
                // Si no hay programas seleccionados, sumar todos los datos sin filtro de programa
                foreach (var lineId in lineIds)
                {
                    int production = GetProductionQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);
                    int rejects = GetRejectsQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);
                    int scrap = GetScrapQuantity(lineId, dateStart, dateEnd, defectFilter, categoryFilter, null, startTimeFilter, endTimeFilter);

                    totalProductionQty += production;
                    totalRejectsQty += rejects;
                    totalScrapQty += scrap;
                }
            }

            // Calcular métricas
            double scrapPercentage = 0.0;
            double reWorkPercentage = 0.0;
            double qualityPercentage = 0.0;

            // Evitar división por cero
            if (totalProductionQty > 0)
            {
                // Calcular scrap percentage correctamente
                scrapPercentage = ((double)totalScrapQty / totalProductionQty);
                reWorkPercentage = ((double)totalRejectsQty / totalProductionQty);
                qualityPercentage = ((double)(totalProductionQty - totalScrapQty - totalRejectsQty) / totalProductionQty);
            }

            return (totalRejectsQty, totalProductionQty, totalScrapQty, scrapPercentage, qualityPercentage, reWorkPercentage);
        }


        public (int rejectsQty, int productionQty, int scrapQty, double scrapPercentage, double qualityPercentage, double reWorkPercentage)? GetDynamicQualityKPIs(
            string? areaFilter,
            List<int>? lineFilters,
            string? defectFilter,
            string? categoryFilter,
            List<string>? programFilters,
            DateOnly dateStart,
            DateOnly? dateEnd = null,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            var endDate = dateEnd ?? dateStart;

            // Caso 1: Área y líneas específicas (múltiples líneas)
            if (lineFilters != null && lineFilters.Any() && !string.IsNullOrEmpty(areaFilter))
            {
                return GetQualityKpisMultipleLines(areaFilter, lineFilters, dateStart, endDate, defectFilter, categoryFilter, programFilters, startTimeFilter, endTimeFilter);
            }
            // Caso 2: Solo área (KPIs de toda el área)
            else if (!string.IsNullOrEmpty(areaFilter))
            {
                return GetQualityKpisArea(areaFilter, dateStart, endDate, defectFilter, categoryFilter, programFilters, startTimeFilter, endTimeFilter);
            }
            // Caso 3: Sin filtros de área ni línea (KPIs de toda la planta)
            else
            {
                return GetQualityKpisOverall(dateStart, endDate, defectFilter, categoryFilter, programFilters, startTimeFilter, endTimeFilter);
            }
        }

        public List<(string Label, double QualityValue)> GetHistoricalQualityData(
            string? areaFilter,
            List<int>? lineFilters,
            string? defectFilter,
            List<string>? programFilters,
            DateOnly startDate,
            DateOnly endDate,
            int periodCount = 6,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            var result = new List<(string Label, double QualityValue)>();

            int totalDays = (endDate.DayNumber - startDate.DayNumber) + 1;

            if (totalDays <= periodCount)
            {
                for (int i = 0; i < totalDays; i++)
                {
                    var currentDate = startDate.AddDays(i);
                    var qualityData = GetDynamicQualityKPIs(areaFilter, lineFilters, defectFilter, null, programFilters, currentDate, currentDate, startTimeFilter, endTimeFilter);

                    string label = currentDate.ToString("MMM dd");
                    double qualityValue = qualityData?.qualityPercentage ?? 0;

                    result.Add((label, qualityValue * 100));
                }
            }
            else
            {
                int daysPerPeriod = totalDays / periodCount;
                if (daysPerPeriod == 0) daysPerPeriod = 1;

                for (int i = 0; i < periodCount; i++)
                {
                    var periodStart = startDate.AddDays(i * daysPerPeriod);
                    var periodEnd = (i == periodCount - 1)
                        ? endDate
                        : startDate.AddDays((i + 1) * daysPerPeriod - 1);

                    if (periodEnd > endDate) periodEnd = endDate;

                    var qualityData = GetDynamicQualityKPIs(areaFilter, lineFilters, null, null, programFilters, periodStart, periodEnd, startTimeFilter, endTimeFilter);

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

                    double qualityValue = qualityData?.qualityPercentage ?? 0;
                    result.Add((label, qualityValue * 100));
                }
            }

            return result;
        }


        public List<(string Label, double QualityValue)> GetMonthlyQualityData(
            string? areaFilter,
            List<int>? lineFilters,
            string? defectFilter,
            List<string>? programFilters,
            DateOnly startDate,
            DateOnly endDate,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            var result = new List<(string Label, double QualityValue)>();

            var currentDate = new DateOnly(startDate.Year, startDate.Month, 1);
            var finalDate = new DateOnly(endDate.Year, endDate.Month, DateTime.DaysInMonth(endDate.Year, endDate.Month));

            while (currentDate <= finalDate)
            {
                var monthStart = currentDate;
                var monthEnd = new DateOnly(currentDate.Year, currentDate.Month,
                    DateTime.DaysInMonth(currentDate.Year, currentDate.Month));

                if (monthStart < startDate) monthStart = startDate;
                if (monthEnd > endDate) monthEnd = endDate;

                var qualityData = GetDynamicQualityKPIs(areaFilter, lineFilters, defectFilter, null, programFilters, monthStart, monthEnd, startTimeFilter, endTimeFilter);

                string label = currentDate.ToString("MMM yyyy");
                double qualityValue = qualityData?.qualityPercentage ?? 0;

                result.Add((label, qualityValue * 100));

                currentDate = currentDate.AddMonths(1);
            }

            return result;
        }


        public List<(string DefectName, int Quantity, double QualityImpactPercentage)> GetTop5DefectsByQualityImpact(
            string? areaFilter,
            List<int>? lineFilters,
            string? defectFilter,
            string? defectCategoryFilter,
            string? categoryFilter,
            List<string>? programFilters,
            DateOnly dateStart,
            DateOnly? dateEnd = null,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            var endDate = dateEnd ?? dateStart;

            // Primero obtener la producción total para calcular porcentajes
            var totalProductionData = GetDynamicQualityKPIs(areaFilter, lineFilters, defectFilter, defectCategoryFilter, programFilters, dateStart, endDate, startTimeFilter, endTimeFilter);
            int totalProduction = totalProductionData?.productionQty ?? 0;

            if (totalProduction == 0)
            {
                return new List<(string DefectName, int Quantity, double QualityImpactPercentage)>();
            }

            // Convertir DateOnly a DateTime para comparaciones
            DateTime startDateTime = dateStart.ToDateTime(TimeOnly.MinValue);
            DateTime endDateTime = endDate.ToDateTime(TimeOnly.MaxValue);

            var query = _context.Rejections
                .Where(r => r.StartTime >= startDateTime && r.StartTime <= endDateTime);

            if (startTimeFilter.HasValue)
            {
                var startTimeSpan = startTimeFilter.Value.ToTimeSpan();
                query = query.Where(r => r.StartTime.TimeOfDay >= startTimeSpan);
            }

            if (endTimeFilter.HasValue)
            {
                var endTimeSpan = endTimeFilter.Value.ToTimeSpan();
                query = query.Where(r => r.StartTime.TimeOfDay <= endTimeSpan);
            }

            // Aplicar filtro de múltiples líneas específicas
            if (lineFilters != null && lineFilters.Any())
            {
                var lineIds = _context.ProductionLines
                    .Where(l => lineFilters.Contains(l.LineNumber ?? 0))
                    .Select(l => l.ProductionLinesId)
                    .ToList();
                query = query.Where(r => lineIds.Contains(r.ProductionLinesId));
            }
            // Si no hay líneas específicas pero sí área, filtrar por área
            else if (!string.IsNullOrEmpty(areaFilter) && areaFilter != "Todas las areas")
            {
                var splitString = areaFilter.Split(" - ");
                if (splitString.Length >= 2)
                {
                    var customer = splitString[0].Trim();
                    var areaName = splitString[1].Trim();

                    var areaRecord = _context.Areas
                        .FirstOrDefault(x => x.AreaName == areaName && x.CustomerName == customer);

                    if (areaRecord != null)
                    {
                        var areaLineIds = _context.ProductionLines
                            .Where(l => l.AreaId == areaRecord.AreaId && l.IsActive)
                            .Select(l => l.ProductionLinesId)
                            .ToList();

                        if (areaLineIds.Any())
                        {
                            query = query.Where(r => areaLineIds.Contains(r.ProductionLinesId));
                        }
                    }
                }
            }

            // Aplicar filtro por defecto específico si se especifica
            if (!string.IsNullOrEmpty(defectFilter) && defectFilter != "Todos los defectos")
            {
                var defect = _context.Defects
                    .FirstOrDefault(d => d.DefectName.ToLower().Contains(defectFilter.ToLower()));

                if (defect != null)
                {
                    query = query.Where(r => r.DefectsData.Any(d => d.DefectId == defect.DefectId));
                }
            }

            // Aplicar filtro por categoría si se especifica
            if (!string.IsNullOrEmpty(categoryFilter) && categoryFilter != "Todos los procesos")
            {
                query = query.Where(r => r.Category != null &&
                                        r.Category.ToLower().Contains(categoryFilter.ToLower()));
            }

            // Aplicar filtro por múltiples programas si se especifica
            if (programFilters != null && programFilters.Any())
            {
                var programIds = programFilters
                    .Where(p => int.TryParse(p, out _))
                    .Select(p => int.Parse(p))
                    .ToList();

                if (programIds.Any())
                {
                    query = query.Where(r => programIds.Contains(r.ProgramId));
                }
            }

            // Calcular impacto en calidad para cada defecto
            var defectDataQuery = query.SelectMany(r => r.DefectsData.Select(dd => new {
                DefectId = dd.DefectId,
                Quantity = dd.DefectQuantity ?? 0 // Usar la cantidad específica del defecto individual
            }))
            .Where(x => x.Quantity > 0); // Filtrar registros con cantidad 0

            // Si hay filtro de defecto específico, aplicar también aquí
            if (!string.IsNullOrEmpty(defectFilter) && defectFilter != "Todos los defectos")
            {
                var defect = _context.Defects
                    .FirstOrDefault(d => d.DefectName.ToLower().Contains(defectFilter.ToLower()));

                if (defect != null)
                {
                    defectDataQuery = defectDataQuery.Where(x => x.DefectId == defect.DefectId);
                }
            }

            // Si hay filtro de categoría de defecto, aplicar también aquí
            if (!string.IsNullOrEmpty(defectCategoryFilter) && defectCategoryFilter != "Todas las categorias")
            {
                // Primero obtener el ID de la categoría, luego filtrar defectos
                var categoryIds = _context.DefectsCategories
                    .Where(dc => dc.DefectCategoryName.ToLower().Contains(defectCategoryFilter.ToLower()))
                    .Select(dc => dc.DefectCategoryId)
                    .ToList();

                if (categoryIds.Any())
                {
                    var defectIds = _context.Defects
                        .Where(d => d.DefectCategoryId.HasValue && categoryIds.Contains(d.DefectCategoryId.Value))
                        .Select(d => d.DefectId)
                        .ToList();

                    if (defectIds.Any())
                    {
                        defectDataQuery = defectDataQuery.Where(x => defectIds.Contains(x.DefectId));
                    }
                }
            }

            var defectStats = defectDataQuery
                .GroupBy(x => x.DefectId)
                .Select(g => new {
                    DefectId = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    QualityImpactPercentage = ((double)g.Sum(x => x.Quantity) / totalProduction) * 100
                })
                .Join(_context.Defects,
                      stat => stat.DefectId,
                      defect => defect.DefectId,
                      (stat, defect) => new {
                          DefectName = defect.DefectName ?? "Defecto Desconocido",
                          TotalQuantity = stat.TotalQuantity,
                          QualityImpactPercentage = stat.QualityImpactPercentage
                      })
                .OrderByDescending(x => x.QualityImpactPercentage) // Ordenar por porcentaje de impacto en calidad
                .Take(5) // Solo los 5 con mayor impacto
                .ToList();

            return defectStats.Select(d => (
                d.DefectName,
                (int)d.TotalQuantity,
                Math.Round(d.QualityImpactPercentage, 2)
            )).ToList();
        }

        // Método para obtener el total de defectos según filtros
        public int GetTotalDefectCount(
            string? areaFilter,
            List<int>? lineFilters,
            string? defectFilter,
            string? defectCategoryFilter,
            List<string>? programFilters,
            DateOnly dateStart,
            DateOnly? dateEnd = null,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            var endDate = dateEnd ?? dateStart;

            // Convertir DateOnly a DateTime para comparaciones
            DateTime startDateTime = dateStart.ToDateTime(TimeOnly.MinValue);
            DateTime endDateTime = endDate.ToDateTime(TimeOnly.MaxValue);

            var query = _context.Rejections
                .Where(r => r.StartTime >= startDateTime && r.StartTime <= endDateTime);

            if (startTimeFilter.HasValue)
            {
                var startTimeSpan = startTimeFilter.Value.ToTimeSpan();
                query = query.Where(r => r.StartTime.TimeOfDay >= startTimeSpan);
            }

            if (endTimeFilter.HasValue)
            {
                var endTimeSpan = endTimeFilter.Value.ToTimeSpan();
                query = query.Where(r => r.StartTime.TimeOfDay <= endTimeSpan);
            }

            // Aplicar filtro de múltiples líneas específicas
            if (lineFilters != null && lineFilters.Any())
            {
                var lineIds = _context.ProductionLines
                    .Where(l => lineFilters.Contains(l.LineNumber ?? 0))
                    .Select(l => l.ProductionLinesId)
                    .ToList();
                query = query.Where(r => lineIds.Contains(r.ProductionLinesId));
            }
            // Si no hay líneas específicas pero sí área, filtrar por área
            else if (!string.IsNullOrEmpty(areaFilter) && areaFilter != "Todas las areas")
            {
                var splitString = areaFilter.Split(" - ");
                if (splitString.Length >= 2)
                {
                    var customer = splitString[0].Trim();
                    var areaName = splitString[1].Trim();

                    var areaRecord = _context.Areas
                        .FirstOrDefault(x => x.AreaName == areaName && x.CustomerName == customer);

                    if (areaRecord != null)
                    {
                        var areaLineIds = _context.ProductionLines
                            .Where(l => l.AreaId == areaRecord.AreaId && l.IsActive)
                            .Select(l => l.ProductionLinesId)
                            .ToList();

                        if (areaLineIds.Any())
                        {
                            query = query.Where(r => areaLineIds.Contains(r.ProductionLinesId));
                        }
                    }
                }
            }

            // Aplicar filtro por defecto específico si se especifica
            if (!string.IsNullOrEmpty(defectFilter) && defectFilter != "Todos los defectos")
            {
                var defect = _context.Defects
                    .FirstOrDefault(d => d.DefectName.ToLower().Contains(defectFilter.ToLower()));

                if (defect != null)
                {
                    query = query.Where(r => r.DefectsData.Any(d => d.DefectId == defect.DefectId));
                }
            }

            // Aplicar filtro por múltiples programas si se especifica
            if (programFilters != null && programFilters.Any())
            {
                var programIds = programFilters
                    .Where(p => int.TryParse(p, out _))
                    .Select(p => int.Parse(p))
                    .ToList();

                if (programIds.Any())
                {
                    query = query.Where(r => programIds.Contains(r.ProgramId));
                }
            }

            // Calcular total de defectos
            var defectDataQuery = query.SelectMany(r => r.DefectsData.Select(dd => new {
                DefectId = dd.DefectId,
                Quantity = dd.DefectQuantity ?? 0
            }))
            .Where(x => x.Quantity > 0);

            // Si hay filtro de defecto específico, aplicar también aquí
            if (!string.IsNullOrEmpty(defectFilter) && defectFilter != "Todos los defectos")
            {
                var defect = _context.Defects
                    .FirstOrDefault(d => d.DefectName.ToLower().Contains(defectFilter.ToLower()));

                if (defect != null)
                {
                    defectDataQuery = defectDataQuery.Where(x => x.DefectId == defect.DefectId);
                }
            }

            // Si hay filtro de categoría de defecto, aplicar también aquí
            if (!string.IsNullOrEmpty(defectCategoryFilter) && defectCategoryFilter != "Todas las categorias")
            {
                // Primero obtener el ID de la categoría, luego filtrar defectos
                var categoryIds = _context.DefectsCategories
                    .Where(dc => dc.DefectCategoryName.ToLower().Contains(defectCategoryFilter.ToLower()))
                    .Select(dc => dc.DefectCategoryId)
                    .ToList();

                if (categoryIds.Any())
                {
                    var defectIds = _context.Defects
                        .Where(d => d.DefectCategoryId.HasValue && categoryIds.Contains(d.DefectCategoryId.Value))
                        .Select(d => d.DefectId)
                        .ToList();

                    if (defectIds.Any())
                    {
                        defectDataQuery = defectDataQuery.Where(x => defectIds.Contains(x.DefectId));
                    }
                }
            }

            return defectDataQuery.Sum(x => x.Quantity);
        }

        // También actualizar el método histórico para consistencia
        public List<(string Label, List<(string DefectName, int Quantity, double QualityImpact)> DefectData)> GetHistoricalDefectsByQualityImpact(
            string? areaFilter,
            List<int>? lineFilters,
            string? defectFilter,
            string? defectCategoryFilter,
            List<string>? programFilters,
            DateOnly startDate,
            DateOnly endDate,
            int periodCount = 6,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            var result = new List<(string Label, List<(string DefectName, int Quantity, double QualityImpact)> DefectData)>();

            int totalDays = (endDate.DayNumber - startDate.DayNumber) + 1;

            if (totalDays <= periodCount)
            {
                for (int i = 0; i < totalDays; i++)
                {
                    var currentDate = startDate.AddDays(i);
                    var defectsData = GetTop5DefectsByQualityImpact(areaFilter, lineFilters, defectFilter, defectCategoryFilter, null, programFilters, currentDate, currentDate, startTimeFilter, endTimeFilter);

                    string label = currentDate.ToString("MMM dd");
                    result.Add((label, defectsData));
                }
            }
            else
            {
                int daysPerPeriod = totalDays / periodCount;
                if (daysPerPeriod == 0) daysPerPeriod = 1;

                for (int i = 0; i < periodCount; i++)
                {
                    var periodStart = startDate.AddDays(i * daysPerPeriod);
                    var periodEnd = (i == periodCount - 1)
                        ? endDate
                        : startDate.AddDays((i + 1) * daysPerPeriod - 1);

                    if (periodEnd > endDate) periodEnd = endDate;

                    var defectsData = GetTop5DefectsByQualityImpact(areaFilter, lineFilters, defectFilter, defectCategoryFilter, null, programFilters, periodStart, periodEnd, startTimeFilter, endTimeFilter);

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

                    result.Add((label, defectsData));
                }
            }

            return result;
        }

        // Agregar este método a tu clase QualityDashboardService
        public List<(string LineLabel, double QualityPercentage)> GetQualityByLines(
            string? areaFilter,
            DateOnly dateStart,
            DateOnly dateEnd,
            string? defectFilter = null,
            string? categoryFilter = null,
            List<string>? programFilters = null,
            TimeOnly? startTimeFilter = null,
            TimeOnly? endTimeFilter = null)
        {
            var result = new List<(string LineLabel, double QualityPercentage)>();

            // Obtener todas las líneas, opcionalmente filtradas por área
            IQueryable<ProductionLine> linesQuery = _context.ProductionLines.Where(l => l.IsActive);

            if (!string.IsNullOrEmpty(areaFilter) && areaFilter != "Todas las areas")
            {
                var splitString = areaFilter.Split(" - ");
                if (splitString.Length >= 2)
                {
                    var customer = splitString[0].Trim();
                    var areaName = splitString[1].Trim();

                    var areaRecord = _context.Areas
                        .FirstOrDefault(x => x.AreaName == areaName && x.CustomerName == customer);

                    if (areaRecord != null)
                    {
                        linesQuery = linesQuery.Where(l => l.AreaId == areaRecord.AreaId);
                    }
                }
                else
                {
                    // Si el formato no es válido, retornar lista vacía
                    return result;
                }
            }

            var lines = linesQuery
                .OrderBy(l => l.AreaId)
                .ThenBy(l => l.LineNumber)
                .ToList();

            // Calcular calidad para cada línea
            foreach (var line in lines)
            {
                var qualityData = GetQualityKpisSingleLine(
                    // Reconstruir el string del área para la línea
                    $"{line.Area?.CustomerName} - {line.Area?.AreaName}",
                    line.LineNumber??0,
                    dateStart,
                    dateEnd,
                    defectFilter,
                    categoryFilter,
                    programFilters,
                    startTimeFilter,
                    endTimeFilter
                );

                // Usar LineName si existe, sino usar "Línea {número}"
                string lineLabel = !string.IsNullOrWhiteSpace(line.LineName)
                    ? line.LineName
                    : $"Línea {line.LineNumber}";

                // Si hay área específica, incluir solo el número de línea o LineName
                // Si son todas las áreas, incluir área en el label
                if (string.IsNullOrEmpty(areaFilter) || areaFilter == "Todas las areas")
                {
                    lineLabel = !string.IsNullOrWhiteSpace(line.LineName)
                        ? $"{line.Area?.AreaName} - {line.LineName}"
                        : $"{line.Area?.AreaName} - L{line.LineNumber}";
                }

                double qualityPercentage = qualityData?.qualityPercentage ?? 0;

                // Solo agregar líneas que tienen datos de producción
                if (qualityData?.productionQty > 0)
                {
                    result.Add((lineLabel, qualityPercentage * 100)); // Convertir a porcentaje
                }
            }

            // Ordenar por porcentaje de calidad descendente
            return result.OrderByDescending(r => r.QualityPercentage).ToList();
        }





    }
}

using ClosedXML.Excel;
using GDIKPI.Data;
using GDIKPI.Models;
using GDIKPI.Services;
using GDIKPI.Services.ReportServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QualityDashboardApiController : Controller
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;
        private readonly QualityDashboardService _qualityDashboardService;
        private readonly QualityDashboardReportService _qualityReportService;

        public QualityDashboardApiController(KpisContext context, PermissionService permissionService, QualityDashboardService qualityDashboardService, QualityDashboardReportService qualityReportDashboardService)
        {
            _context = context;
            _permissionService = permissionService;
            _qualityDashboardService = qualityDashboardService;
            _qualityReportService = qualityReportDashboardService;
        }
        private IQueryable<VwReject> GetFilter(
            string areaFilter,
            List<int> lineFilters,
            string categoryFilter,
            string defectFilter,
            DateOnly? startDateFilter,
            DateOnly? endDateFilter,
            List<int> programIdFilters)
        {
            var query = _context.VwRejects.AsNoTracking().AsQueryable();

            // Filtro por área (más flexible)
            if (!string.IsNullOrEmpty(areaFilter) && areaFilter != "Todas las areas")
            {
                query = query.Where(p => p.Area != null && p.Area.ToLower().Contains(areaFilter.ToLower()));
            }

            // Filtro por líneas múltiples
            if (lineFilters != null && lineFilters.Any())
            {
                query = query.Where(p => lineFilters.Contains(p.Linea ?? 0));
            }

            // Filtro por descripción/categoría (más flexible)
            if (!string.IsNullOrEmpty(categoryFilter) && categoryFilter != "Todos los procesos")
            {
                query = query.Where(p => p.Category != null && p.Category.ToLower().Contains(categoryFilter.ToLower()));
            }

            // Filtro por estado (más flexible)
            if (!string.IsNullOrEmpty(defectFilter) && defectFilter != "Todos los defectos")
            {
                query = query.Where(p => p.DefectName != null && p.DefectName.ToLower().Contains(defectFilter.ToLower()));
            }

            // Filtro por rango de fechas
            if (startDateFilter.HasValue)
            {
                query = query.Where(p => p.Fecha >= startDateFilter.Value);
            }

            if (endDateFilter.HasValue)
            {
                query = query.Where(p => p.Fecha <= endDateFilter.Value);
            }

            // Filtro por múltiples programas
            if (programIdFilters != null && programIdFilters.Any())
            {
                query = query.Where(p => programIdFilters.Contains(p.ProgramId));
            }

            return query;
        }

        private IQueryable<DefectsDatum> GetTimeFilteredDefectsDataQuery(
            string? areaFilter,
            List<int> lineFilters,
            string? defectFilter,
            DateOnly? startDateFilter,
            DateOnly? endDateFilter,
            List<int> programIdFilters,
            TimeOnly? startTimeFilter,
            TimeOnly? endTimeFilter)
        {
            var query = _context.DefectsData
                .AsNoTracking()
                .Include(dd => dd.Defect)
                .Include(dd => dd.ProductionLines)
                    .ThenInclude(pl => pl.Area)
                .Include(dd => dd.Rejection)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(areaFilter) && areaFilter != "Todas las areas")
            {
                query = query.Where(dd =>
                    dd.ProductionLines.Area != null &&
                    ((dd.ProductionLines.Area.CustomerName ?? "") + " - " + (dd.ProductionLines.Area.AreaName ?? ""))
                        .ToLower()
                        .Contains(areaFilter.ToLower()));
            }

            if (lineFilters.Any())
            {
                query = query.Where(dd => dd.ProductionLines.LineNumber.HasValue && lineFilters.Contains(dd.ProductionLines.LineNumber.Value));
            }

            if (!string.IsNullOrWhiteSpace(defectFilter) && defectFilter != "Todos los defectos")
            {
                query = query.Where(dd => dd.Defect.DefectName != null && dd.Defect.DefectName.ToLower().Contains(defectFilter.ToLower()));
            }

            if (startDateFilter.HasValue)
            {
                var startDateTime = startDateFilter.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(dd => dd.StartTime >= startDateTime);
            }

            if (endDateFilter.HasValue)
            {
                var endDateTime = endDateFilter.Value.ToDateTime(TimeOnly.MaxValue);
                query = query.Where(dd => dd.StartTime <= endDateTime);
            }

            if (startTimeFilter.HasValue)
            {
                var startTimeSpan = startTimeFilter.Value.ToTimeSpan();
                query = query.Where(dd => dd.StartTime.TimeOfDay >= startTimeSpan);
            }

            if (endTimeFilter.HasValue)
            {
                var endTimeSpan = endTimeFilter.Value.ToTimeSpan();
                query = query.Where(dd => dd.StartTime.TimeOfDay <= endTimeSpan);
            }

            if (programIdFilters.Any())
            {
                query = query.Where(dd => dd.Rejection != null && programIdFilters.Contains(dd.Rejection.ProgramId));
            }

            return query;
        }

        private object GetTable(IQueryable<VwReject> query, int draw, int start, int length, string orderColumn, string orderDirection)
        {
            int totalRecords = query.Count();

            // Orden dinámico según las columnas del DataTable
            // Nuevo orden: Fecha, Semana, Category, Area, Linea, ProgramDescription, DefectName, Cantidad
            query = (orderColumn, orderDirection) switch
            {
                ("0", "asc") => query.OrderBy(p => p.Fecha),
                ("0", "desc") => query.OrderByDescending(p => p.Fecha),

                ("1", "asc") => query.OrderBy(p => p.NumeroSemana),
                ("1", "desc") => query.OrderByDescending(p => p.NumeroSemana),

                ("2", "asc") => query.OrderBy(p => p.Category),
                ("2", "desc") => query.OrderByDescending(p => p.Category),

                ("3", "asc") => query.OrderBy(p => p.Area),
                ("3", "desc") => query.OrderByDescending(p => p.Area),

                ("4", "asc") => query.OrderBy(p => p.Linea),
                ("4", "desc") => query.OrderByDescending(p => p.Linea),

                ("5", "asc") => query.OrderBy(p => p.ProgramDescription),
                ("5", "desc") => query.OrderByDescending(p => p.ProgramDescription),

                ("6", "asc") => query.OrderBy(p => p.DefectName),
                ("6", "desc") => query.OrderByDescending(p => p.DefectName),

                ("7", "asc") => query.OrderBy(p => p.Cantidad),
                ("7", "desc") => query.OrderByDescending(p => p.Cantidad),

                _ => query.OrderBy(p => p.Fecha).ThenBy(p => p.Area) // fallback
            };

            // Paginación + selección de columnas
            var data = query
                .Skip(start)
                .Take(length)
                .Select(p => new
                {
                    fecha = p.Fecha.HasValue ? p.Fecha.Value.ToString("yyyy-MM-dd") : "N/A",
                    semana = p.NumeroSemana ?? 0,
                    category = p.Category ?? "N/A",
                    area = p.Area ?? "",
                    linea = p.Linea,
                    defectName = p.DefectName ?? "N/A",
                    cantidad = p.Cantidad,
                    programDescription = p.ProgramDescription ?? "N/A"
                })
                .ToList();


            return new
            {
                draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data
            };
        }

        private object GetTableFromDefectsData(
            IQueryable<DefectsDatum> query,
            int draw,
            int start,
            int length,
            string orderColumn,
            string orderDirection)
        {
            var projectedQuery = query.Select(dd => new
            {
                fecha = dd.StartTime.Date,
                tiempo = dd.StartTime,
                area = ((dd.ProductionLines.Area!.CustomerName ?? "") + " - " + (dd.ProductionLines.Area!.AreaName ?? "")),
                linea = !string.IsNullOrWhiteSpace(dd.ProductionLines.LineName)
                    ? dd.ProductionLines.LineName
                    : (dd.ProductionLines.LineNumber.HasValue ? dd.ProductionLines.LineNumber.Value.ToString() : ""),
                programDescription = dd.Rejection != null ? (dd.Rejection.ProgramDescription ?? "N/A") : "N/A",
                defectName = dd.Defect.DefectName ?? "N/A",
                cantidad = dd.DefectQuantity ?? 0
            });

            projectedQuery = (orderColumn, orderDirection) switch
            {
                ("0", "asc") => projectedQuery.OrderBy(p => p.fecha),
                ("0", "desc") => projectedQuery.OrderByDescending(p => p.fecha),
                ("1", "asc") => projectedQuery.OrderBy(p => p.tiempo),
                ("1", "desc") => projectedQuery.OrderByDescending(p => p.tiempo),
                ("2", "asc") => projectedQuery.OrderBy(p => p.area),
                ("2", "desc") => projectedQuery.OrderByDescending(p => p.area),
                ("3", "asc") => projectedQuery.OrderBy(p => p.linea),
                ("3", "desc") => projectedQuery.OrderByDescending(p => p.linea),
                ("4", "asc") => projectedQuery.OrderBy(p => p.programDescription),
                ("4", "desc") => projectedQuery.OrderByDescending(p => p.programDescription),
                ("5", "asc") => projectedQuery.OrderBy(p => p.defectName),
                ("5", "desc") => projectedQuery.OrderByDescending(p => p.defectName),
                ("6", "asc") => projectedQuery.OrderBy(p => p.cantidad),
                ("6", "desc") => projectedQuery.OrderByDescending(p => p.cantidad),
                _ => projectedQuery.OrderByDescending(p => p.fecha).ThenByDescending(p => p.tiempo)
            };

            var totalRecords = projectedQuery.Count();
            var data = projectedQuery
                .Skip(start)
                .Take(length)
                .ToList()
                .Select(p => new
                {
                    fecha = p.fecha.ToString("yyyy-MM-dd"),
                    tiempo = p.tiempo.ToString("HH:mm:ss"),
                    area = p.area,
                    linea = p.linea,
                    programDescription = p.programDescription,
                    defectName = p.defectName,
                    cantidad = p.cantidad
                })
                .ToList();

            return new
            {
                draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data
            };
        }


        [HttpPost("SetTable")]
        public IActionResult SetTable()
        {
            try
            {
                int.TryParse(Request.Form["draw"], out int draw);
                int.TryParse(Request.Form["start"], out int start);
                int.TryParse(Request.Form["length"], out int length);
                string orderColumn = Request.Form["order[0][column]"].FirstOrDefault() ?? "2";
                string orderDirection = Request.Form["order[0][dir]"].FirstOrDefault() ?? "asc";

                string areaFilter = Request.Form["areaFilter"].FirstOrDefault();
                string defectFilter = Request.Form["defectFilter"].FirstOrDefault();     // defecto
                string? startTimeFilterRaw = Request.Form["startTimeFilter"].FirstOrDefault();
                string? endTimeFilterRaw = Request.Form["endTimeFilter"].FirstOrDefault();

                // Procesar múltiples líneas seleccionadas
                List<int> lineFilters = new List<int>();
                var lineFiltersRaw = Request.Form["lineFilters[]"];
                if (lineFiltersRaw.Any())
                {
                    foreach (var line in lineFiltersRaw)
                    {
                        if (int.TryParse(line, out int parsedLine))
                        {
                            lineFilters.Add(parsedLine);
                        }
                    }
                }

                // Procesar múltiples programas seleccionados
                List<int> programFilters = new List<int>();
                var programFiltersRaw = Request.Form["programFilters[]"];
                if (programFiltersRaw.Any())
                {
                    foreach (var prog in programFiltersRaw)
                    {
                        if (int.TryParse(prog, out int parsedProg))
                        {
                            programFilters.Add(parsedProg);
                        }
                    }
                }

                var today = DateOnly.FromDateTime(DateTime.Now);

                DateOnly? startDateFilter = today;
                DateOnly? endDateFilter = today;

                if (DateOnly.TryParse(Request.Form["startDateFilter"].FirstOrDefault(), out DateOnly sdf))
                    startDateFilter = sdf;

                if (DateOnly.TryParse(Request.Form["endDateFilter"].FirstOrDefault(), out DateOnly edf))
                    endDateFilter = edf;

                TimeOnly? startTimeFilter = null;
                TimeOnly? endTimeFilter = null;

                if (TimeOnly.TryParse(startTimeFilterRaw, out var parsedStartTime))
                    startTimeFilter = parsedStartTime;

                if (TimeOnly.TryParse(endTimeFilterRaw, out var parsedEndTime))
                    endTimeFilter = parsedEndTime;

                // Debug logging
                Console.WriteLine($"Filtros aplicados - Área: {areaFilter}, Líneas: {string.Join(",", lineFilters)}, Programas: {string.Join(",", programFilters)}, Defecto: {defectFilter}, FechaInicio: {startDateFilter}, FechaFin: {endDateFilter}, HoraInicio: {startTimeFilter}, HoraFin: {endTimeFilter}");

                var queryFiltrada = GetTimeFilteredDefectsDataQuery(areaFilter, lineFilters, defectFilter, startDateFilter, endDateFilter, programFilters, startTimeFilter, endTimeFilter);
                var resultado = GetTableFromDefectsData(queryFiltrada, draw, start, length, orderColumn, orderDirection);

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en SetTable: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost("GetQualityKpis")]
        public async Task<IActionResult> GetQualityKpis([FromBody] Dictionary<string, object> data)
        {
            try
            {
                // Obtener valores desde el JSON
                // Obtener valores desde el JSON string para Quality Dashboard
                string? areaFilter = data.ContainsKey("areaFilter") ? data["areaFilter"]?.ToString() : null;
                string? defectCategoryFilter = data.ContainsKey("defectCategoryFilter") ? data["defectCategoryFilter"]?.ToString() : null;
                string? defectFilter = data.ContainsKey("defectFilter") ? data["defectFilter"]?.ToString() : null;
                string? startDateFilter = data.ContainsKey("startDateFilter") ? data["startDateFilter"]?.ToString() : null;
                string? endDateFilter = data.ContainsKey("endDateFilter") ? data["endDateFilter"]?.ToString() : null;
                string? startTimeFilterRaw = data.ContainsKey("startTimeFilter") ? data["startTimeFilter"]?.ToString() : null;
                string? endTimeFilterRaw = data.ContainsKey("endTimeFilter") ? data["endTimeFilter"]?.ToString() : null;

                // Parse lineFilters a List<int>
                List<int> lineFilters = new List<int>();
                if (data.ContainsKey("lineFilters") && data["lineFilters"] != null)
                {
                    var lineFiltersArray = System.Text.Json.JsonSerializer.Deserialize<List<int>>(data["lineFilters"].ToString());
                    if (lineFiltersArray != null)
                    {
                        lineFilters = lineFiltersArray;
                    }
                }

                // Parse programFilters a List<string> (los IDs de programas son strings)
                List<string> programFilters = new List<string>();
                if (data.ContainsKey("programFilters") && data["programFilters"] != null)
                {
                    var programFiltersArray = System.Text.Json.JsonSerializer.Deserialize<List<string>>(data["programFilters"].ToString());
                    if (programFiltersArray != null)
                    {
                        programFilters = programFiltersArray;
                    }
                }

                // Para compatibilidad, usar el primer programa si existe
                string? programFilter = programFilters.Any() ? programFilters.First() : null;

                // Manejo de fechas
                var today = DateOnly.FromDateTime(DateTime.Now);
                DateOnly start = today;
                DateOnly end = today;

                if (!string.IsNullOrEmpty(startDateFilter) && DateOnly.TryParseExact(startDateFilter, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateOnly s))
                    start = s;

                if (!string.IsNullOrEmpty(endDateFilter) && DateOnly.TryParseExact(endDateFilter, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateOnly e))
                    end = e;

                TimeOnly? startTimeFilter = null;
                TimeOnly? endTimeFilter = null;

                if (!string.IsNullOrEmpty(startTimeFilterRaw) && TimeOnly.TryParse(startTimeFilterRaw, out var st))
                    startTimeFilter = st;

                if (!string.IsNullOrEmpty(endTimeFilterRaw) && TimeOnly.TryParse(endTimeFilterRaw, out var et))
                    endTimeFilter = et;

                // Variables para Quality KPIs (no OEE)
                int rejectsQty = 0, productionQty = 0, scrapQty = 0;
                double scrapPercentage = 0, qualityPercentage = 0, reWorkPercentage = 0;

                // Adjusting the call to match the expected signature of GetDynamicQualityKPIs
                var qualityResult = _qualityDashboardService.GetDynamicQualityKPIs(
                    areaFilter,
                    lineFilters,
                    defectFilter,
                    null,
                    programFilters,
                    start,
                    end,
                    startTimeFilter,
                    endTimeFilter
                );

                if (qualityResult.HasValue)
                {
                    rejectsQty = qualityResult.Value.rejectsQty;
                    productionQty = qualityResult.Value.productionQty;
                    scrapPercentage = qualityResult.Value.scrapPercentage;
                    qualityPercentage = qualityResult.Value.qualityPercentage;
                    reWorkPercentage = qualityResult.Value.reWorkPercentage;
                    scrapQty = qualityResult.Value.scrapQty;

                }

                // Obtener total de defectos según filtros
                var totalDefects = _qualityDashboardService.GetTotalDefectCount(
                    areaFilter,
                    lineFilters,
                    defectFilter,
                    defectCategoryFilter, // Usar defectCategoryFilter
                    programFilters,
                    start,
                    end,
                    startTimeFilter,
                    endTimeFilter
                );

                var result = new
                {
                    // Métricas principales de calidad
                    qualityPercentage = Math.Round(qualityPercentage * 100, 2),
                    scrapPercentage = Math.Round(scrapPercentage * 100, 2),
                    reWorkPercentage = Math.Round(reWorkPercentage * 100, 2),

                    // Cantidades absolutas
                    totalProduction = productionQty,
                    totalRejects = rejectsQty,
                    totalScrap = scrapQty,
                    totalDefects = totalDefects


                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener datos de Quality KPIs", error = ex.Message });
            }
        }

        [HttpGet("QualityPercentage")]
        public IActionResult LoadParameters(string metricName, string category)
        {
            if (string.IsNullOrEmpty(metricName) || string.IsNullOrEmpty(category))
                return BadRequest("Faltan parámetros");

            var parametro = _context.PerformanceMetrics
                .FirstOrDefault(p => p.MetricName == metricName && p.Category == category);

            if (parametro == null)
                return NotFound();

            return Ok(new
            {
                minValue = parametro.MinValue,
                maxValue = parametro.MaxValue
            });
        }

        // ACTUALIZADO: Cambiar de EOOPercentage a PerformanceMetrics
        [HttpGet("GetCategory")]
        public IActionResult GetOEEPercentage(string metricName, string metricType = null)
        {
            if (string.IsNullOrEmpty(metricName))
                return BadRequest("Falta el parámetro metricName");

            var query = _context.PerformanceMetrics.Where(p => p.MetricName == metricName);

            // Si se proporciona metricType, filtrar también por él
            if (!string.IsNullOrEmpty(metricType))
            {
                query = query.Where(p => p.MetricType == metricType);
            }

            var parametros = query
                .Select(p => new
                {
                    category = p.Category,
                    minValue = p.MinValue,
                    maxValue = p.MaxValue,
                    metricType = p.MetricType
                })
                .ToList();

            if (!parametros.Any())
                return NotFound("No se encontraron parámetros para este KPI");

            return Ok(parametros);
        }

        // ACTUALIZADO: Cambiar de EOOPercentage a PerformanceMetrics
        [HttpPost("UpdateParameters")]
        public IActionResult UpdateParameters([FromForm] string MetricName,
                                      [FromForm] string Category,
                                      [FromForm] decimal MinValue,
                                      [FromForm] decimal MaxValue)
        {
            if (string.IsNullOrEmpty(MetricName) || string.IsNullOrEmpty(Category))
                return BadRequest("Datos inválidos");

            var parametro = _context.PerformanceMetrics
                .FirstOrDefault(p => p.MetricName == MetricName && p.Category == Category);

            if (parametro == null)
            {
                // Si no existe, crear un nuevo registro
                parametro = new PerformanceMetric
                {
                    MetricType = "Quality", // Valor por defecto
                    MetricName = MetricName,
                    Category = Category,
                    MinValue = MinValue,
                    MaxValue = MaxValue,
                    Unit = "%" // Valor por defecto
                };
                _context.PerformanceMetrics.Add(parametro);
            }
            else
            {
                // Si existe, actualizar
                parametro.MinValue = MinValue;
                parametro.MaxValue = MaxValue;
            }

            _context.SaveChanges();

            return Ok(new { message = "Parámetro actualizado correctamente" });
        }

        [HttpPost("GetParetoByDefect")]
        public async Task<IActionResult> GetParetoByDefect([FromBody] Dictionary<string, object> data)
        {
            try
            {
                // Obtener filtros desde el JSON
                string? areaFilter = data.ContainsKey("areaFilter") ? data["areaFilter"]?.ToString() : null;
                string? defectCategoryFilter = data.ContainsKey("defectCategoryFilter") ? data["defectCategoryFilter"]?.ToString() : null;
                string? defectFilter = data.ContainsKey("defectFilter") ? data["defectFilter"]?.ToString() : null;
                string? startDateFilter = data.ContainsKey("startDateFilter") ? data["startDateFilter"]?.ToString() : null;
                string? endDateFilter = data.ContainsKey("endDateFilter") ? data["endDateFilter"]?.ToString() : null;
                string? startTimeFilterRaw = data.ContainsKey("startTimeFilter") ? data["startTimeFilter"]?.ToString() : null;
                string? endTimeFilterRaw = data.ContainsKey("endTimeFilter") ? data["endTimeFilter"]?.ToString() : null;

                // Parse lineFilters a List<int>
                List<int> lineFilters = new List<int>();
                if (data.ContainsKey("lineFilters") && data["lineFilters"] != null)
                {
                    var lineFiltersArray = System.Text.Json.JsonSerializer.Deserialize<List<int>>(data["lineFilters"].ToString());
                    if (lineFiltersArray != null)
                    {
                        lineFilters = lineFiltersArray;
                    }
                }

                // Parse programFilters a List<string>
                List<string> programFilters = new List<string>();
                if (data.ContainsKey("programFilters") && data["programFilters"] != null)
                {
                    var programFiltersArray = System.Text.Json.JsonSerializer.Deserialize<List<string>>(data["programFilters"].ToString());
                    if (programFiltersArray != null)
                    {
                        programFilters = programFiltersArray;
                    }
                }

                // Manejo de fechas
                var today = DateOnly.FromDateTime(DateTime.Now);
                DateOnly start = today;
                DateOnly end = today;

                if (!string.IsNullOrEmpty(startDateFilter) && DateOnly.TryParseExact(startDateFilter, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateOnly s))
                    start = s;

                if (!string.IsNullOrEmpty(endDateFilter) && DateOnly.TryParseExact(endDateFilter, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateOnly e))
                    end = e;

                TimeOnly? startTimeFilter = null;
                TimeOnly? endTimeFilter = null;

                if (!string.IsNullOrEmpty(startTimeFilterRaw) && TimeOnly.TryParse(startTimeFilterRaw, out var st))
                    startTimeFilter = st;

                if (!string.IsNullOrEmpty(endTimeFilterRaw) && TimeOnly.TryParse(endTimeFilterRaw, out var et))
                    endTimeFilter = et;

                // Obtener los datos de Pareto (Top 5 defectos por impacto en calidad)
                var paretoData = _qualityDashboardService.GetTop5DefectsByQualityImpact(
                    areaFilter,
                    lineFilters,
                    defectFilter,
                    defectCategoryFilter,
                    null,
                    programFilters,
                    start,
                    end,
                    startTimeFilter,
                    endTimeFilter
                );

                // Usar el QualityImpactPercentage que ya viene calculado
                // Este porcentaje representa: (Cantidad del defecto / Producción total) * 100
                var paretoResult = paretoData.Select(d => new
                {
                    defectName = d.DefectName,
                    quantity = d.Quantity,
                    percentage = d.QualityImpactPercentage
                }).ToList();

                return Ok(paretoResult);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // Agregar este método al QualityDashboardApiController


        [HttpGet("ExportQualityToExcel")]
        public IActionResult QualityReport(
                string? areaFilter,
                string? lineFilters,
                string? defectCategoryFilter,
                string? defectFilter,
                string? categoryFilter,
                DateOnly? startDateFilter,
                DateOnly? endDateFilter,
                double? qualityPercentage,
                double? scrapPercentage,
                double? reWorkPercentage,
                int? totalProduction,
                int? totalRejects,
                int? totalScrap,
                string? programFilters,
                string? programNamesFilter,
                string? programTypesFilter

        )
        {
            try
            {
                // Parsear líneas desde string separado por comas
                List<int> lineFiltersList = new List<int>();
                if (!string.IsNullOrEmpty(lineFilters))
                {
                    var lineArray = lineFilters.Split(',');
                    foreach (var lineStr in lineArray)
                    {
                        if (int.TryParse(lineStr.Trim(), out int parsedLine))
                        {
                            lineFiltersList.Add(parsedLine);
                        }
                    }
                }

                // Parsear programas desde string separado por comas
                List<int> programFiltersList = new List<int>();
                if (!string.IsNullOrEmpty(programFilters))
                {
                    var programArray = programFilters.Split(',');
                    foreach (var programStr in programArray)
                    {
                        if (int.TryParse(programStr.Trim(), out int parsedProgram))
                        {
                            programFiltersList.Add(parsedProgram);
                        }
                    }
                }

                // Ruta de la plantilla (usa .xlsx)
                var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "reports", "QualityReport.xlsx");

                if (!System.IO.File.Exists(templatePath))
                {
                    return StatusCode(500, $"La plantilla no se encontró en {templatePath}");
                }

                // Reutilizar tu función de filtrado adaptada para Quality
                var queryFiltrada = GetFilter(
                    areaFilter,
                    lineFiltersList,
                    categoryFilter,
                    defectFilter,
                    startDateFilter,
                    endDateFilter,
                    programFiltersList
                );
                decimal? targetdecimal = null;

                if (!string.IsNullOrEmpty(areaFilter))
                {
                    if (areaFilter.Contains("Forrado"))
                    {
                        targetdecimal = _context.PerformanceMetrics
                            .Where(l => l.MetricType == "Quality" && l.MetricName == "Overall" && l.Category == "Steering wheel")
                            .Select(l => l.MinValue) // solo el valor
                            .FirstOrDefault();
                    }
                    else if (areaFilter.Contains("Back-covers"))
                    {
                        targetdecimal = _context.PerformanceMetrics
                            .Where(l => l.MetricType == "Quality" && l.MetricName == "Overall" && l.Category == "Back-Cover")
                            .Select(l => l.MinValue)
                            .FirstOrDefault();
                    }
                }
                double target = (double)targetdecimal;

                string area = areaFilter;
                string line = lineFiltersList.Any() ? string.Join(", ", lineFiltersList) : "All Lines";
                string program = programNamesFilter ?? "Todos los programas";

                string range = $"{startDateFilter} - {endDateFilter}";

                string subtitle = $"{(string.IsNullOrEmpty(area) ? "" : "Area: " + area)} "+
                                  $"{(string.IsNullOrEmpty(line) || line == "All Lines" ? "" : "Line: " + line)} " +
                                  $"{(string.IsNullOrEmpty(program) || program == "Todos los programas" ? "" : "Program: " + program)} "+
                                  $"{(string.IsNullOrEmpty(range) ? "" : "Range: " + range)}";

                // Límite máximo de registros para evitar problemas de rendimiento
                const int MAX_REGISTROS_EXCEL = 50000;

                // Optimización: Tomar directamente los registros con límite
                // Esto es más rápido que contar primero y luego tomar
                var registros = queryFiltrada
                    .Take(MAX_REGISTROS_EXCEL)
                    .ToList();

                // Si no hay registros, continuar con el reporte vacío (no retornar NoContent)
                // Esto permite generar el reporte con los KPIs y gráficas aunque no haya detalles de defectos

                using var workbook = new XLWorkbook(templatePath);
                var worksheet = workbook.Worksheet(1);

                // Encabezado del reporte
                worksheet.Cell(12, 2).Value = startDateFilter.HasValue && endDateFilter.HasValue
                   ? $"{startDateFilter:yyyy/MM/dd} - {endDateFilter:yyyy/MM/dd}"
                   : DateTime.Now.ToString("yyyy/MM/dd");
                worksheet.Cell(12, 7).Value = line;
                worksheet.Cell(12, 9).Value = areaFilter ?? "All Areas";

                if (string.IsNullOrEmpty(programNamesFilter) || programNamesFilter == "Todos los programas")
                {
                    worksheet.Cell(12, 13).Value = "All programs";
                }
                else
                {
                    worksheet.Cell(12, 13).Value = programNamesFilter;
                }


                // Métricas de calidad principales
                worksheet.Cell(22, 3).Value = (qualityPercentage ?? 0).ToString("F2") + "%";
                worksheet.Cell(22, 7).Value = (reWorkPercentage ?? 0).ToString("F2") + "%";
                worksheet.Cell(22, 11).Value = (scrapPercentage ?? 0).ToString("F2") + "%";

                // Totales de producción y rechazos
                worksheet.Cell(37, 3).Value = totalProduction ?? 0;
                worksheet.Cell(37, 7).Value = totalRejects ?? 0;
                worksheet.Cell(37, 11).Value = totalScrap ?? 0;

                var startDate = startDateFilter ?? DateOnly.FromDateTime(DateTime.Now);
                var endDate = endDateFilter ?? DateOnly.FromDateTime(DateTime.Now);

                // Obtener datos históricos de calidad
                List<(string Label, double QualityValue)> historicalData;

                // Decidir qué tipo de datos mostrar basado en el rango de fechas
                var daysDifference = (endDate.DayNumber - startDate.DayNumber) + 1;

                // Convertir programFiltersList a lista de strings para el servicio
                List<string>? programFiltersForService = programFiltersList.Any()
                    ? programFiltersList.Select(p => p.ToString()).ToList()
                    : null;

                if (daysDifference <= 31) // Un mes o menos, usar datos por períodos
                {
                    historicalData = _qualityDashboardService.GetHistoricalQualityData(areaFilter, lineFiltersList, defectFilter, programFiltersForService, startDate, endDate, 6);
                }
                else // Más de un mes, usar datos mensuales
                {
                    historicalData = _qualityDashboardService.GetMonthlyQualityData(areaFilter, lineFiltersList, defectFilter, programFiltersForService, startDate, endDate);

                    // Si hay demasiados meses, tomar solo los últimos 6
                    if (historicalData.Count > 6)
                    {
                        historicalData = historicalData.TakeLast(6).ToList();
                    }
                }

                // Extraer labels y valores
                var labels = historicalData.Select(h => h.Label).ToList();
                var values = historicalData.Select(h => h.QualityValue).ToList();

                // Si no hay datos históricos, usar datos por defecto
                if (!labels.Any())
                {
                    labels = new List<string> { startDate.ToString("MMM yyyy") };
                    values = new List<double> { qualityPercentage ?? 0 };
                }

                // CORRECCIÓN: Obtener los TOP 5 defectos directamente (no históricos)
                var top5Defects = _qualityDashboardService.GetTop5DefectsByQualityImpact(
                    areaFilter,
                    lineFiltersList,
                    defectFilter,
                    defectCategoryFilter,
                    categoryFilter,
                    programFiltersForService,
                    startDate,
                    endDate
                );

                var defectLabels = new List<string>();
                var defectValues = new List<double>();

                if (top5Defects.Any())
                {
                    defectLabels = top5Defects.Select(d => $"{d.DefectName} ({d.Quantity})").ToList();
                    defectValues = top5Defects.Select(d => d.QualityImpactPercentage).ToList(); // Usar el porcentaje de impacto
                }
                else
                {
                    // Datos por defecto si no hay defectos
                    defectLabels = new List<string> { "Sin defectos" };
                    defectValues = new List<double> { 0 };
                }
                // Si hay filtro de líneas específicas, calcular para esas líneas
                List<(string LineLabel, double QualityPercentage)> qualityByLinesData;

                if (lineFiltersList != null && lineFiltersList.Any())
                {
                    // Calcular calidad para cada línea seleccionada
                    qualityByLinesData = new List<(string, double)>();

                    foreach (var lineNumber in lineFiltersList)
                    {
                        var singleLineQuality = _qualityDashboardService.GetQualityKpisSingleLine(
                            areaFilter,
                            lineNumber,
                            startDate,
                            endDate,
                            defectFilter,
                            categoryFilter,
                            programFiltersForService
                        );

                        if (singleLineQuality.HasValue)
                        {
                            qualityByLinesData.Add(($"Línea {lineNumber}", singleLineQuality.Value.qualityPercentage * 100));
                        }
                    }
                }
                else
                {
                    // Obtener calidad de todas las líneas del área
                    qualityByLinesData = _qualityDashboardService.GetQualityByLines(
                        areaFilter,
                        startDate,
                        endDate,
                        defectFilter,
                        categoryFilter,
                        programFiltersForService
                    );
                }

                // Extraer labels y valores para la gráfica
                var lineLabels = new List<string>();
                var lineQualityValues = new List<double>();

                if (qualityByLinesData.Any())
                {
                    lineLabels = qualityByLinesData.Select(d => d.LineLabel).ToList();
                    lineQualityValues = qualityByLinesData.Select(d => d.QualityPercentage).ToList();
                }
                else
                {
                    // Datos por defecto si no hay líneas
                    lineLabels = new List<string> { "Sin datos" };
                    lineQualityValues = new List<double> { 0 };
                }
                // Crear la gráfica con datos dinámicos de calidad
                _qualityReportService.CreateAreaChartQualityOverall(worksheet, 47, 2, labels, values, target, "FTQ TIME LINE",subtitle);
                _qualityReportService.CreateBarChartQualityOverall(worksheet, 87, 2, defectLabels, defectValues, "PARETO BY DEFECTS", subtitle);
                // Siempre generar el gráfico FTQ BY LINE, incluso para una sola línea
                _qualityReportService.CreateBarChartQualityByLine(
                    worksheet,
                    130,
                    2,
                    lineLabels,
                    lineQualityValues,
                    "PARETO FTQ BY LINE",
                    subtitle,
                    target
                );





                // Detalle de rechazos
                int row = 174;
                foreach (var r in registros)
                {
                    // Week No
                    var weekCell = worksheet.Cell(row, 2);
                    weekCell.Value = r.NumeroSemana?.ToString() ?? "";
                    weekCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    weekCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    weekCell.Style.Font.FontSize = 12;

                    // Date (merge 2 columnas)
                    var dateRange = worksheet.Range(row, 3, row, 4);
                    dateRange.Merge();
                    dateRange.Value = r.Fecha?.ToString("MM/dd/yyyy") ?? "";
                    dateRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    dateRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    dateRange.Style.Font.FontSize = 12;

                    // Process (merge 2 columnas)
                    var processRange = worksheet.Range(row, 5, row, 6);
                    processRange.Merge();
                    processRange.Value = r.Category ?? "";
                    processRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    processRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    processRange.Style.Font.FontSize = 12;

                    // Area (merge 2 columnas)
                    var areaRange = worksheet.Range(row, 7, row, 8);
                    areaRange.Merge();
                    areaRange.Value = r.Area ?? "";
                    areaRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    areaRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    areaRange.Style.Font.FontSize = 12;

                    // Line
                    var lineCell = worksheet.Cell(row, 9);
                    lineCell.Value = r.Linea?.ToString() ?? "";
                    lineCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    lineCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    lineCell.Style.Font.FontSize = 12;

                    // Program
                    var programCell = worksheet.Cell(row, 10);
                    programCell.Value = r.ProgramDescription;
                    programCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    programCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    programCell.Style.Font.FontSize = 12;

                    // Defect Name (merge 3 columnas)
                    var defectRange = worksheet.Range(row, 11, row, 14);
                    defectRange.Merge();
                    defectRange.Value = r.DefectName ?? "N/A";
                    defectRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    defectRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    defectRange.Style.Font.FontSize = 12;

                    // Defect Quantity
                    var cantidadCell = worksheet.Cell(row, 15);
                    cantidadCell.Value = r.Cantidad;
                    cantidadCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cantidadCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    cantidadCell.Style.Font.FontSize = 12;

                    // Avanza 1 fila
                    row++;
                }






                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                var fileName = $"ReporteCalidad_{(startDateFilter?.ToString("yyyyMMdd") ?? "NA")}_{(endDateFilter?.ToString("yyyyMMdd") ?? "NA")}.xlsx";

                return File(content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al exportar Excel: {ex.Message}");
                return StatusCode(500, $"Error al generar Excel: {ex.Message}");
            }
        }
    }
}

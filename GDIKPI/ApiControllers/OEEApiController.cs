using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using GDIKPI.Data;
using GDIKPI.Models;
using GDIKPI.Services;
using GDIKPI.Services.ReportServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OEEApiController : ControllerBase
    {
        private readonly KpisContext _context;
        private readonly OEEService _oeeService;
        private readonly OEEReportService _oeeReportService;

        public OEEApiController(KpisContext context, OEEService oeeService, OEEReportService oeeReportService)
        {
            _context = context;
            _oeeService = oeeService;
            _oeeReportService = oeeReportService;
        }

        private IQueryable<VwOee> GetFilter(
            string downTimeFilter,
            string areaFilter,
            int? lineFilter,
            string descriptionFilter,
            string statusFilter,
            DateOnly? startDateFilter,
            DateOnly? endDateFilter)
        {
            var query = _context.VwOees.AsNoTracking().AsQueryable();

            // Filtro por tipo (más flexible para incluir "Sin Incidencias")
            if (!string.IsNullOrEmpty(downTimeFilter) && downTimeFilter != "Todos los Tipos")
            {
                // Mapear los valores del frontend a los valores de la vista
                switch (downTimeFilter.ToLower())
                {
                    case "ausentismo":
                        query = query.Where(p => p.Type.ToLower().Contains("ausentismo"));
                        break;
                    case "tiempo muerto":
                        query = query.Where(p => p.Type.ToLower().Contains("tiempo muerto") || p.Type.ToLower().Contains("downtime"));
                        break;
                    case "sin incidencias":
                        query = query.Where(p => p.Type.ToLower().Contains("sin incidencias"));
                        break;
                    default:
                        query = query.Where(p => p.Type.ToLower().Contains(downTimeFilter.ToLower()));
                        break;
                }
            }

            // Filtro por área (más flexible)
            if (!string.IsNullOrEmpty(areaFilter) && areaFilter != "Todas las areas")
            {
                query = query.Where(p => p.Area != null && p.Area.ToLower().Contains(areaFilter.ToLower()));
            }

            // Filtro por línea (asegurar que incluya las líneas sin incidencias)
            if (lineFilter.HasValue && lineFilter > 0)
            {
                query = query.Where(p => p.LineNumber == lineFilter.Value);
            }

            // Filtro por descripción/categoría (más flexible)
            if (!string.IsNullOrEmpty(descriptionFilter) && descriptionFilter != "Todas las descripciones")
            {
                query = query.Where(p => p.Category != null && p.Category.ToLower().Contains(descriptionFilter.ToLower()));
            }

            // Filtro por estado (más flexible)
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "Todos los estados")
            {
                // Mapear estados del frontend
                switch (statusFilter.ToLower())
                {
                    case "activo":
                        query = query.Where(p => p.Status != null && (p.Status.ToLower().Contains("activo") || p.Status.ToLower().Contains("active")));
                        break;
                    case "abierto":
                        query = query.Where(p => p.Status != null && (p.Status.ToLower().Contains("abierto") || p.Status.ToLower().Contains("open")));
                        break;
                    case "cerrado":
                        query = query.Where(p => p.Status != null && (p.Status.ToLower().Contains("cerrado") || p.Status.ToLower().Contains("close")));
                        break;
                    case "n/a":
                        query = query.Where(p => p.Status == null || p.Status.ToLower().Contains("n/a"));
                        break;
                    default:
                        query = query.Where(p => p.Status != null && p.Status.ToLower().Contains(statusFilter.ToLower()));
                        break;
                }
            }

            // Filtro por rango de fechas
            if (startDateFilter.HasValue)
            {
                query = query.Where(p => p.ReportDate >= startDateFilter.Value);
            }

            if (endDateFilter.HasValue)
            {
                query = query.Where(p => p.ReportDate <= endDateFilter.Value);
            }

            return query;
        }

        private object GetTable(IQueryable<VwOee> query, int draw, int start, int length, string orderColumn, string orderDirection, string? areaFilter = null, int? lineFilter = null, DateOnly? startDateFilter = null)
        {
            int totalRecords = query.Count();

            // Si no hay registros, generar "Sin Incidencias" por línea
            if (totalRecords == 0)
            {
                var lineasSinIncidencias = new List<VwOee>();

                // Obtener las líneas de producción según los filtros
                var queryLineas = _context.ProductionLines.Where(l => l.IsActive);

                // Aplicar filtro de área si existe
                if (!string.IsNullOrEmpty(areaFilter))
                {
                    var parts = areaFilter.Split(" - ");
                    var customerName = parts[0];
                    var areaName = parts.Length > 1 ? parts[1] : "";

                    var areaId = _context.Areas
                        .Where(a => a.CustomerName == customerName && a.AreaName == areaName)
                        .Select(a => (int?)a.AreaId)
                        .FirstOrDefault();

                    if (areaId.HasValue)
                    {
                        queryLineas = queryLineas.Where(l => l.AreaId == areaId.Value);
                    }
                }

                // Aplicar filtro de línea si existe
                if (lineFilter.HasValue && lineFilter > 0)
                {
                    queryLineas = queryLineas.Where(l => l.LineNumber == lineFilter.Value);
                }

                var lineas = queryLineas.ToList();

                // Generar registros "Sin Incidencias" para cada línea
                foreach (var linea in lineas)
                {
                    var area = _context.Areas.FirstOrDefault(a => a.AreaId == linea.AreaId);
                    var areaDisplay = area != null ? $"{area.CustomerName} - {area.AreaName}" : "N/A";

                    lineasSinIncidencias.Add(new VwOee
                    {
                        ProductionLinesId = linea.ProductionLinesId,
                        LineNumber = linea.LineNumber,
                        Area = areaDisplay,
                        ReportDate = startDateFilter ?? DateOnly.FromDateTime(DateTime.Now),
                        Type = "Sin Incidencias",
                        Category = "N/A",
                        Status = "N/A",
                        Total = 0
                    });
                }

                // Convertir a IQueryable para reutilizar la lógica de ordenamiento
                query = lineasSinIncidencias.AsQueryable();
                totalRecords = query.Count();
            }

            // Debug: Log para verificar qué datos estamos obteniendo
            var allData = query.Select(p => new { p.LineNumber, p.Type, p.Area }).ToList();


            // Orden dinámico
            query = (orderColumn, orderDirection) switch
            {
                ("0", "asc") => query.OrderBy(p => p.Type),
                ("0", "desc") => query.OrderByDescending(p => p.Type),
                ("1", "asc") => query.OrderBy(p => p.Area),
                ("1", "desc") => query.OrderByDescending(p => p.Area),
                ("2", "asc") => query.OrderBy(p => p.LineNumber),
                ("2", "desc") => query.OrderByDescending(p => p.LineNumber),
                ("3", "asc") => query.OrderBy(p => p.ReportDate),
                ("3", "desc") => query.OrderByDescending(p => p.ReportDate),
                ("4", "asc") => query.OrderBy(p => p.Category),
                ("4", "desc") => query.OrderByDescending(p => p.Category),
                ("5", "asc") => query.OrderBy(p => p.Status),
                ("5", "desc") => query.OrderByDescending(p => p.Status),
                ("6", "asc") => query.OrderBy(p => p.Total),
                ("6", "desc") => query.OrderByDescending(p => p.Total),
                _ => query.OrderBy(p => p.LineNumber).ThenByDescending(p => p.ReportDate) // Ordenar por línea primero
            };

            // Paginación + selección de columnas
            var data = query
                .Skip(start)
                .Take(length)
                .Select(p => new
                {
                    type = p.Type ?? "Sin Incidencias",
                    area = p.Area ?? "",
                    lineNumber = p.LineNumber,
                    reportDate = p.ReportDate.HasValue ? p.ReportDate.Value.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd"),
                    category = p.Category ?? "N/A",
                    status = p.Status ?? "N/A",
                    total = p.Total
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

                string downTimeFilter = Request.Form["downTimeFilter"].FirstOrDefault();
                string areaFilter = Request.Form["areaFilter"].FirstOrDefault();
                string descriptionFilter = Request.Form["descriptionFilter"].FirstOrDefault();
                string statusFilter = Request.Form["statusFilter"].FirstOrDefault();

                int? lineFilter = null;
                if (int.TryParse(Request.Form["lineFilter"].FirstOrDefault(), out int lf))
                    lineFilter = lf;

                var today = DateOnly.FromDateTime(DateTime.Now);

                DateOnly startDateFilter = today;
                DateOnly endDateFilter = today;

                if (DateOnly.TryParse(Request.Form["startDateFilter"].FirstOrDefault(), out DateOnly sdf))
                    startDateFilter = sdf;

                if (DateOnly.TryParse(Request.Form["endDateFilter"].FirstOrDefault(), out DateOnly edf))
                    endDateFilter = edf;

                // Debug logging
                Console.WriteLine($"Filtros aplicados - Tipo: {downTimeFilter}, Área: {areaFilter}, Línea: {lineFilter}, Estado: {statusFilter}");

                var queryFiltrada = GetFilter(downTimeFilter, areaFilter, lineFilter, descriptionFilter, statusFilter, startDateFilter, endDateFilter);
                var resultado = GetTable(queryFiltrada, draw, start, length, orderColumn, orderDirection, areaFilter, lineFilter, startDateFilter);

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en SetTable: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [HttpPost("CalculateOEE")]
        public async Task<IActionResult> GetKPIData([FromBody] Dictionary<string, object> data)
        {
            try
            {
                // Obtener valores desde el JSON
                string? downTimeFilter = data.ContainsKey("downTimeFilter") ? data["downTimeFilter"]?.ToString() : null;
                string? areaFilter = data.ContainsKey("areaFilter") ? data["areaFilter"]?.ToString() : null;
                string? descriptionFilter = data.ContainsKey("descriptionFilter") ? data["descriptionFilter"]?.ToString() : null;
                string? statusFilter = data.ContainsKey("statusFilter") ? data["statusFilter"]?.ToString() : null;
                string? startDateFilter = data.ContainsKey("startDateFilter") ? data["startDateFilter"]?.ToString() : null;
                string? endDateFilter = data.ContainsKey("endDateFilter") ? data["endDateFilter"]?.ToString() : null;

                // Parse lineFilter a int?
                int? lineFilter = null;
                if (data.ContainsKey("lineFilter") && int.TryParse(data["lineFilter"]?.ToString(), out int lf))
                {
                    lineFilter = lf;
                }

                var today = DateOnly.FromDateTime(DateTime.Now);

                DateOnly start = today;
                DateOnly end = today;

                if (!string.IsNullOrEmpty(startDateFilter) && DateOnly.TryParseExact(startDateFilter, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateOnly s))
                    start = s;

                if (!string.IsNullOrEmpty(endDateFilter) && DateOnly.TryParseExact(endDateFilter, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateOnly e))
                    end = e;

                double disponibilidad = 0, rendimiento = 0, calidad = 0, oeeTotal = 0, totalDowntime = 0;
                int totalOperatingTime = 0, totalPieces = 0, totalRejects = 0, totalGoal = 0;

                // Usar GetOEEWithHistory para consultar historial primero en días anteriores
                var oeeResult = _oeeService.GetOEEWithHistory(areaFilter, lineFilter, start, end);
                if (oeeResult.HasValue)
                {
                    disponibilidad = oeeResult.Value.disponibilidad;
                    rendimiento = oeeResult.Value.rendimiento;
                    calidad = oeeResult.Value.calidad;
                    oeeTotal = oeeResult.Value.oee;
                    totalOperatingTime = oeeResult.Value.totalOperatingTime;
                    totalPieces = oeeResult.Value.totalPieces;
                    totalRejects = oeeResult.Value.totalRejects;
                    totalGoal = oeeResult.Value.goal;
                    totalDowntime = oeeResult.Value.downtime;
                }

                var result = new
                {
                    oeeTotal = Math.Round(oeeTotal * 100, 2),
                    availability = Math.Round(disponibilidad * 100, 2),
                    performance = Math.Round(rendimiento * 100, 2),
                    quality = Math.Round(calidad * 100, 2),
                    totalOperatingTime,
                    totalPieces,
                    totalRejects,
                    totalGoal,
                    totalDowntime

                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error al obtener datos de KPIs", error = ex.Message });
            }
        }

        // ACTUALIZADO: Cambiar de EOOPercentage a PerformanceMetrics
        [HttpGet("OEEPercentage")]
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
        public IActionResult GetOEEPercentage(string metricName)
        {
            if (string.IsNullOrEmpty(metricName))
                return BadRequest("Falta el parámetro metricName");

            var parametros = _context.PerformanceMetrics
                .Where(p => p.MetricName == metricName)
                .Select(p => new
                {
                    category = p.Category,
                    minValue = p.MinValue,
                    maxValue = p.MaxValue
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
                    MetricType = "OEE", // Valor por defecto
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

        [HttpGet("ExportOEEToExcel")]
        public IActionResult ExportOEEToExcel(
            string? downTimeFilter,
            string? areaFilter,
            int? lineFilter,
            string? descriptionFilter,
            string? statusFilter,
            DateOnly? startDateFilter,
            DateOnly? endDateFilter,
            double? oeeTotal,
            double? availability,
            double? performance,
            double? quality,
            double? totalOperatingTime,
            int? totalPieces,
            int? totalRejects,
            int? totalGoal,
            double? totalDowntime)
        {
            try
            {
                // Ruta de la plantilla (usa .xlsx)
                var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "assets", "reports", "OeeReport.xlsx");

                if (!System.IO.File.Exists(templatePath))
                {
                    return StatusCode(500, $"La plantilla no se encontró en {templatePath}");
                }

                // Reutilizar tu función de filtrado
                var queryFiltrada = GetFilter(
                    downTimeFilter,
                    areaFilter,
                    lineFilter,
                    descriptionFilter,
                    statusFilter,
                    startDateFilter,
                    endDateFilter
                );
                var registros = queryFiltrada.ToList();

                // Si no hay incidencias, generar registros "Sin Incidencias" por línea
                if (!registros.Any())
                {
                    var lineasSinIncidencias = new List<VwOee>();

                    // Obtener las líneas de producción según los filtros
                    var queryLineas = _context.ProductionLines.Where(l => l.IsActive);

                    // Aplicar filtro de área si existe
                    if (!string.IsNullOrEmpty(areaFilter))
                    {
                        var parts = areaFilter.Split(" - ");
                        var customerName = parts[0];
                        var areaName = parts.Length > 1 ? parts[1] : "";

                        var areaId = _context.Areas
                            .Where(a => a.CustomerName == customerName && a.AreaName == areaName)
                            .Select(a => (int?)a.AreaId)
                            .FirstOrDefault();

                        if (areaId.HasValue)
                        {
                            queryLineas = queryLineas.Where(l => l.AreaId == areaId.Value);
                        }
                    }

                    // Aplicar filtro de línea si existe
                    if (lineFilter.HasValue && lineFilter > 0)
                    {
                        queryLineas = queryLineas.Where(l => l.LineNumber == lineFilter.Value);
                    }

                    var lineas = queryLineas.ToList();

                    // Generar registros "Sin Incidencias" para cada línea
                    foreach (var linea in lineas)
                    {
                        var area = _context.Areas.FirstOrDefault(a => a.AreaId == linea.AreaId);
                        var areaDisplay = area != null ? $"{area.CustomerName} - {area.AreaName}" : "N/A";

                        lineasSinIncidencias.Add(new VwOee
                        {
                            ProductionLinesId = linea.ProductionLinesId,
                            LineNumber = linea.LineNumber,
                            Area = areaDisplay,
                            ReportDate = startDateFilter ?? DateOnly.FromDateTime(DateTime.Now),
                            Type = "Sin Incidencias",
                            Category = "N/A",
                            Status = "N/A",
                            Total = 0
                        });
                    }

                    registros = lineasSinIncidencias;

                    // Si después de generar "Sin Incidencias" aún no hay registros
                    if (!registros.Any())
                    {
                        return BadRequest(new {
                            success = false,
                            message = "No hay líneas de producción activas para el área seleccionada."
                        });
                    }
                }

                using var workbook = new XLWorkbook(templatePath);
                var worksheet = workbook.Worksheet(1);
                // progresbarr 
                _oeeReportService.CreateImageProgressBar(worksheet, 28, 8, oeeTotal ?? 0, 100, 315, 50);
                _oeeReportService.CreateImageProgressBar(worksheet, 28, 17, performance ?? 0, 100, 235, 50);
                _oeeReportService.CreateImageProgressBar(worksheet, 28, 21, quality ?? 0, 100, 235, 50);
                _oeeReportService.CreateImageProgressBar(worksheet, 28, 13, availability ?? 0, 100, 235, 50);


                // Encabezado
                worksheet.Cell(12, 8).Value = areaFilter ?? "All Areas";
                worksheet.Cell(12, 13).Value = lineFilter?.ToString() ?? "All Lines";
                worksheet.Cell(12, 16).Value = startDateFilter.HasValue && endDateFilter.HasValue
                    ? $"{startDateFilter:yyyy/MM/dd} - {endDateFilter:yyyy/MM/dd}"
                    : DateTime.Now.ToString("yyyy/MM/dd");

                // Métricas
                worksheet.Cell(21, 8).Value = oeeTotal.ToString() + "%";
                worksheet.Cell(21, 21).Value = quality.ToString() + "%";
                worksheet.Cell(21, 17).Value = performance.ToString() + "%";
                worksheet.Cell(21, 13).Value = availability.ToString() + "%";

                // Totales
                worksheet.Cell(38, 2).Value = totalPieces ?? 0;
                worksheet.Cell(46, 2).Value = totalGoal ?? 0;
                worksheet.Cell(54, 2).Value = totalRejects ?? 0;
                worksheet.Cell(16, 2).Value = Math.Round((totalOperatingTime ?? 0) / 60, 1);
                worksheet.Cell(25, 2).Value = Math.Round((totalDowntime ?? 0) / 60, 1);


                var startDate = startDateFilter ?? DateOnly.FromDateTime(DateTime.Now);
                var endDate = endDateFilter ?? DateOnly.FromDateTime(DateTime.Now);

                // Obtener datos históricos dinámicos
                List<(string Label, double OeeValue)> historicalData;

                // Decidir qué tipo de datos mostrar basado en el rango de fechas
                var daysDifference = (endDate.DayNumber - startDate.DayNumber) + 1;

                if (daysDifference <= 31) // Un mes o menos, usar datos por períodos
                {
                    historicalData = _oeeService.GetHistoricalOEEData(areaFilter, lineFilter, startDate, endDate, 6);
                }
                else // Más de un mes, usar datos mensuales
                {
                    historicalData = _oeeService.GetMonthlyOEEData(areaFilter, lineFilter, startDate, endDate);

                    // Si hay demasiados meses, tomar solo los últimos 6
                    if (historicalData.Count > 6)
                    {
                        historicalData = historicalData.TakeLast(6).ToList();
                    }
                }

                // Extraer labels y valores
                var labels = historicalData.Select(h => h.Label).ToList();
                var values = historicalData.Select(h => h.OeeValue).ToList();

                // Si no hay datos históricos, usar datos por defecto
                if (!labels.Any())
                {
                    labels = new List<string> { startDate.ToString("MMM yyyy") };
                    values = new List<double> { oeeTotal ?? 0 };
                }

                // Crear la gráfica con datos dinámicos
                _oeeReportService.CreateAreaChart(worksheet, 33, 8, labels, values, "OEE TIME LINE");

                // Detalle
                int row = 68;
                foreach (var r in registros)
                {
                    var typeRange = worksheet.Range(row, 2, row + 1, 4);
                    typeRange.Merge();
                    typeRange.Value = r.Type;
                    typeRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    typeRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    typeRange.Style.Font.FontSize = 18;

                    var areaRange = worksheet.Range(row, 5, row + 1, 7);
                    areaRange.Merge();
                    areaRange.Value = r.Area ?? "";
                    areaRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    areaRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    areaRange.Style.Font.FontSize = 18;

                    var lineRange = worksheet.Range(row, 8, row + 1, 10);
                    lineRange.Merge();
                    lineRange.Value = r.LineNumber?.ToString() ?? "";
                    lineRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    lineRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    lineRange.Style.Font.FontSize = 18;

                    var dateRange = worksheet.Range(row, 11, row + 1, 13);
                    dateRange.Merge();
                    dateRange.Value = r.ReportDate?.ToString("MM/dd/yyyy") ?? "";
                    dateRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    dateRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    dateRange.Style.Font.FontSize = 18;

                    var categoryRange = worksheet.Range(row, 14, row + 1, 16);
                    categoryRange.Merge();
                    categoryRange.Value = r.Category ?? "N/A";
                    categoryRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    categoryRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    categoryRange.Style.Font.FontSize = 18;

                    var statusRange = worksheet.Range(row, 17, row + 1, 19);
                    statusRange.Merge();
                    statusRange.Value = r.Status ?? "N/A";
                    statusRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    statusRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    statusRange.Style.Font.FontSize = 18;

                    var totalRange = worksheet.Range(row, 20, row + 1, 22);
                    totalRange.Merge();
                    totalRange.Value = r.Total;
                    totalRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    totalRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    totalRange.Style.Font.FontSize = 18;

                    // Avanza 2 filas para el siguiente registro
                    row += 2;
                }

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                var fileName = $"ReporteOEE_{(startDateFilter?.ToString("yyyyMMdd") ?? "NA")}_{(endDateFilter?.ToString("yyyyMMdd") ?? "NA")}.xlsx";

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
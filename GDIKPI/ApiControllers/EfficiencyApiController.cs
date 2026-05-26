using GDIKPI.Data;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EfficiencyApiController : ControllerBase
    {
        private readonly KpisContext _context;

        public EfficiencyApiController(KpisContext context)
        {
            _context = context;
        }

        private IQueryable<Models.Efficiency> BuildEfficiencyQuery(
            string? areaFilter,
            List<int>? lineFilters,
            DateOnly startDate,
            DateOnly endDate)
        {
            var query = _context.Efficiencies
                .AsNoTracking()
                .Include(e => e.ProductionLines)
                .Where(e => e.DateData >= startDate && e.DateData <= endDate);

            if (!string.IsNullOrWhiteSpace(areaFilter))
            {
                query = query.Where(e => e.AreaCustomerName == areaFilter);
            }

            if (lineFilters != null && lineFilters.Any())
            {
                query = query.Where(e =>
                    e.ProductionLines.LineNumber.HasValue &&
                    lineFilters.Contains(e.ProductionLines.LineNumber.Value));
            }

            return query;
        }

        private static List<int> ParseLineFilters(string? rawLineFilters)
        {
            var lineFilters = new List<int>();

            if (string.IsNullOrWhiteSpace(rawLineFilters))
            {
                return lineFilters;
            }

            foreach (var line in rawLineFilters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(line, out var parsedLine))
                {
                    lineFilters.Add(parsedLine);
                }
            }

            return lineFilters;
        }

        [HttpPost("chart")]
        public async Task<IActionResult> GetChartData([FromBody] Dictionary<string, object> data)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var startDate = today;
            var endDate = today;

            var areaFilter = data.ContainsKey("areaFilter") ? data["areaFilter"]?.ToString() : null;
            var startDateFilter = data.ContainsKey("startDateFilter") ? data["startDateFilter"]?.ToString() : null;
            var endDateFilter = data.ContainsKey("endDateFilter") ? data["endDateFilter"]?.ToString() : null;

            if (!string.IsNullOrWhiteSpace(startDateFilter) && DateOnly.TryParse(startDateFilter, out var parsedStartDate))
            {
                startDate = parsedStartDate;
            }

            if (!string.IsNullOrWhiteSpace(endDateFilter) && DateOnly.TryParse(endDateFilter, out var parsedEndDate))
            {
                endDate = parsedEndDate;
            }

            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var lineFilters = new List<int>();
            if (data.ContainsKey("lineFilters") && data["lineFilters"] != null)
            {
                var parsedLines = System.Text.Json.JsonSerializer.Deserialize<List<int>>(data["lineFilters"].ToString()!);
                if (parsedLines != null)
                {
                    lineFilters = parsedLines;
                }
            }

            var query = BuildEfficiencyQuery(areaFilter, lineFilters, startDate, endDate);

            var chartData = await query
                .GroupBy(e => e.DateData)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    label = g.Key.ToString("dd/MM"),
                    horasGanadas = Math.Round(g.Sum(x => x.HorasGanadas ?? 0), 2),
                    horasUtilizadas = Math.Round(g.Sum(x => x.HorasUtilizadas ?? 0), 2),
                    efficiencyPercentage = Math.Round(g.Average(x => x.EfficiencyPercentage), 1)
                })
                .ToListAsync();

            return Ok(new
            {
                labels = chartData.Select(x => x.label),
                horasGanadas = chartData.Select(x => x.horasGanadas),
                horasUtilizadas = chartData.Select(x => x.horasUtilizadas),
                efficiencyPercentages = chartData.Select(x => x.efficiencyPercentage)
            });
        }

        [HttpPost("table")]
        public IActionResult GetTable()
        {
            try
            {
                int.TryParse(Request.Form["draw"], out int draw);
                int.TryParse(Request.Form["start"], out int start);
                int.TryParse(Request.Form["length"], out int length);
                string orderColumn = Request.Form["order[0][column]"].FirstOrDefault() ?? "0";
                string orderDirection = Request.Form["order[0][dir]"].FirstOrDefault() ?? "desc";

                string areaFilter = Request.Form["areaFilter"].FirstOrDefault() ?? string.Empty;

                List<int> lineFilters = new();
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

                var today = DateOnly.FromDateTime(DateTime.Today);
                DateOnly startDateFilter = today;
                DateOnly endDateFilter = today;

                if (DateOnly.TryParse(Request.Form["startDateFilter"].FirstOrDefault(), out DateOnly parsedStartDate))
                {
                    startDateFilter = parsedStartDate;
                }

                if (DateOnly.TryParse(Request.Form["endDateFilter"].FirstOrDefault(), out DateOnly parsedEndDate))
                {
                    endDateFilter = parsedEndDate;
                }

                if (endDateFilter < startDateFilter)
                {
                    (startDateFilter, endDateFilter) = (endDateFilter, startDateFilter);
                }

                var query = BuildEfficiencyQuery(areaFilter, lineFilters, startDateFilter, endDateFilter);

                query = (orderColumn, orderDirection) switch
                {
                    ("0", "asc") => query.OrderBy(e => e.DateData),
                    ("0", "desc") => query.OrderByDescending(e => e.DateData),
                    ("2", "asc") => query.OrderBy(e => e.AreaCustomerName),
                    ("2", "desc") => query.OrderByDescending(e => e.AreaCustomerName),
                    ("3", "asc") => query.OrderBy(e => e.ProductionLines.LineNumber),
                    ("3", "desc") => query.OrderByDescending(e => e.ProductionLines.LineNumber),
                    ("4", "asc") => query.OrderBy(e => e.PeopleQuantity),
                    ("4", "desc") => query.OrderByDescending(e => e.PeopleQuantity),
                    ("5", "asc") => query.OrderBy(e => e.ProducedPieces),
                    ("5", "desc") => query.OrderByDescending(e => e.ProducedPieces),
                    ("6", "asc") => query.OrderBy(e => e.HorasGanadas),
                    ("6", "desc") => query.OrderByDescending(e => e.HorasGanadas),
                    ("7", "asc") => query.OrderBy(e => e.HorasUtilizadas),
                    ("7", "desc") => query.OrderByDescending(e => e.HorasUtilizadas),
                    ("8", "asc") => query.OrderBy(e => e.EfficiencyPercentage),
                    ("8", "desc") => query.OrderByDescending(e => e.EfficiencyPercentage),
                    _ => query.OrderByDescending(e => e.DateData).ThenBy(e => e.ProductionLines.LineNumber)
                };

                int totalRecords = query.Count();

                var data = query
                    .Skip(start)
                    .Take(length)
                    .ToList()
                    .Select(e => new
                    {
                        fecha = e.DateData.ToString("yyyy-MM-dd"),
                        semana = ISOWeek.GetWeekOfYear(e.DateData.ToDateTime(TimeOnly.MinValue)),
                        area = e.AreaCustomerName ?? string.Empty,
                        linea = !string.IsNullOrWhiteSpace(e.ProductionLines.LineName)
                            ? e.ProductionLines.LineName
                            : (e.ProductionLines.LineNumber?.ToString() ?? string.Empty),
                        peopleQuantity = e.PeopleQuantity,
                        producedPieces = e.ProducedPieces,
                        horasGanadas = Math.Round(e.HorasGanadas ?? 0, 2),
                        horasUtilizadas = Math.Round(e.HorasUtilizadas ?? 0, 2),
                        efficiencyPercentage = Math.Round(e.EfficiencyPercentage, 2),
                        tiempoEstandar= e.TiempoEstandar

                    })
                    .ToList();

                return Ok(new
                {
                    draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = totalRecords,
                    data
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("export")]
        public async Task<IActionResult> ExportEfficiencyToExcel([FromBody] EfficiencyExportRequest request)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var startDate = request.StartDateFilter ?? today;
                var endDate = request.EndDateFilter ?? today;

                if (endDate < startDate)
                {
                    (startDate, endDate) = (endDate, startDate);
                }

                var lineFilters = ParseLineFilters(request.LineFilters);
                var query = BuildEfficiencyQuery(request.AreaFilter, lineFilters, startDate, endDate);
                var records = await query
                    .OrderByDescending(e => e.DateData)
                    .ThenBy(e => e.ProductionLines.LineNumber)
                    .Select(e => new
                    {
                        Fecha = e.DateData,
                        Area = e.AreaCustomerName ?? string.Empty,
                        Linea = !string.IsNullOrWhiteSpace(e.ProductionLines.LineName)
                            ? e.ProductionLines.LineName
                            : (e.ProductionLines.LineNumber.HasValue ? e.ProductionLines.LineNumber.Value.ToString() : string.Empty),
                        Personas = e.PeopleQuantity ,
                        PiezasProducidas = e.ProducedPieces ,
                        TiempoEstandar = e.TiempoEstandar ,
                        HorasGanadas = Math.Round(e.HorasGanadas ?? 0, 2),
                        HorasUtilizadas = Math.Round(e.HorasUtilizadas ?? 0, 2),
                        Eficiencia = Math.Round(e.EfficiencyPercentage, 2)
                    })
                    .ToListAsync();

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Eficiencia");

                worksheet.Cell("A1").Value = "Reporte de Eficiencia";
                worksheet.Cell("A1").Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Font.FontSize = 18;

                worksheet.Cell("A3").Value = "Area";
                worksheet.Cell("B3").Value = string.IsNullOrWhiteSpace(request.AreaFilter) ? "Todas" : request.AreaFilter;
                worksheet.Cell("D3").Value = "Lineas";
                worksheet.Cell("E3").Value = lineFilters.Any() ? string.Join(", ", lineFilters) : "Todas";
                worksheet.Cell("G3").Value = "Rango";
                worksheet.Cell("H3").Value = $"{startDate:yyyy-MM-dd} a {endDate:yyyy-MM-dd}";

                worksheet.Cell("A5").Value = "Grafica filtrada";
                worksheet.Cell("A5").Style.Font.Bold = true;

                if (!string.IsNullOrWhiteSpace(request.ChartImageBase64))
                {
                    var base64Payload = request.ChartImageBase64;
                    var commaIndex = base64Payload.IndexOf(',');
                    if (commaIndex >= 0)
                    {
                        base64Payload = base64Payload[(commaIndex + 1)..];
                    }

                    var imageBytes = Convert.FromBase64String(base64Payload);
                    using var imageStream = new MemoryStream(imageBytes);
                    worksheet.AddPicture(imageStream)
                        .MoveTo(worksheet.Cell("A6"))
                        .WithSize(900, 380);
                }
                else
                {
                    worksheet.Cell("A6").Value = "No se recibio imagen de la grafica.";
                }

                var tableStartRow = 28;
                var headers = new[]
                {
                    "Fecha", "Area", "Linea", "Personas", "Piezas Producidas",
                    "Tiempo Estandar", "Horas Ganadas", "Horas Utilizadas", "Eficiencia %"
                };

                for (var columnIndex = 0; columnIndex < headers.Length; columnIndex++)
                {
                    var cell = worksheet.Cell(tableStartRow, columnIndex + 1);
                    cell.Value = headers[columnIndex];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
                }

                var currentRow = tableStartRow + 1;
                foreach (var record in records)
                {
                    worksheet.Cell(currentRow, 1).Value = record.Fecha.ToString("yyyy-MM-dd");
                    worksheet.Cell(currentRow, 2).Value = record.Area;
                    worksheet.Cell(currentRow, 3).Value = record.Linea;
                    worksheet.Cell(currentRow, 4).Value = record.Personas;
                    worksheet.Cell(currentRow, 5).Value = record.PiezasProducidas;
                    worksheet.Cell(currentRow, 6).Value = record.TiempoEstandar;
                    worksheet.Cell(currentRow, 7).Value = record.HorasGanadas;
                    worksheet.Cell(currentRow, 8).Value = record.HorasUtilizadas;
                    worksheet.Cell(currentRow, 9).Value = record.Eficiencia / 100;
                    worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "0.00%";
                    currentRow++;
                }

                if (!records.Any())
                {
                    worksheet.Cell(currentRow, 1).Value = "No hay datos para los filtros seleccionados.";
                }

                worksheet.Columns("A:I").AdjustToContents();
                worksheet.SheetView.FreezeRows(tableStartRow);

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"ReporteEficiencia_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class EfficiencyExportRequest
        {
            public string? AreaFilter { get; set; }
            public string? LineFilters { get; set; }
            public DateOnly? StartDateFilter { get; set; }
            public DateOnly? EndDateFilter { get; set; }
            public string? ChartImageBase64 { get; set; }
        }
    }
}

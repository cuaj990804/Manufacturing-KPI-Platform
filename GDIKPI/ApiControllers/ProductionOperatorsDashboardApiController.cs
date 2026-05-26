using ClosedXML.Excel;
using GDIKPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductionOperatorsDashboardApiController : ControllerBase
    {
        private readonly KpisContext _context;

        public ProductionOperatorsDashboardApiController(KpisContext context)
        {
            _context = context;
        }

        private IQueryable<Models.ProductionOperatorsScan> BuildScansQuery(
            string? operationFilter,
            string? employeeFilter,
            DateTime startDate,
            DateTime endDate,
            string? startTimeFilter,
            string? endTimeFilter)
        {
            var endExclusive = endDate.Date.AddDays(1);

            var query = _context.ProductionOperatorsScans
                .AsNoTracking()
                .Where(scanItem =>
                    scanItem.ScannedAt >= startDate.Date &&
                    scanItem.ScannedAt < endExclusive);

            // FILTRO HORA INICIO
            if (TimeSpan.TryParse(startTimeFilter, out var startTime))
            {
                query = query.Where(scanItem =>
                    scanItem.ScannedAt.HasValue &&
                    scanItem.ScannedAt.Value.TimeOfDay >= startTime);
            }

            // FILTRO HORA FIN
            if (TimeSpan.TryParse(endTimeFilter, out var endTime))
            {
                query = query.Where(scanItem =>
                    scanItem.ScannedAt.HasValue &&
                    scanItem.ScannedAt.Value.TimeOfDay <= endTime);
            }

            // FILTRO OPERACION
            if (!string.IsNullOrWhiteSpace(operationFilter))
            {
                query = query.Where(scanItem =>
                    scanItem.Operator.Operation == operationFilter);
            }

            // FILTRO EMPLEADO
            if (!string.IsNullOrWhiteSpace(employeeFilter))
            {
                if (TryParseEmployeeNumber(employeeFilter, out var employeeNumber))
                {
                    query = query.Where(scanItem =>
                        scanItem.Operator.EmployeeNumber == employeeNumber);
                }
                else
                {
                    var normalizedFilter = employeeFilter.Trim();

                    query = query.Where(scanItem =>
                        EF.Functions.Like(
                            (scanItem.Operator.NameOperator ?? "") + " " +
                            (scanItem.Operator.LastnameOperator ?? ""),
                            $"%{normalizedFilter}%")

                        ||

                        EF.Functions.Like(
                            (scanItem.Operator.LastnameOperator ?? "") + " " +
                            (scanItem.Operator.NameOperator ?? ""),
                            $"%{normalizedFilter}%")

                        ||

                        EF.Functions.Like(
                            scanItem.Operator.NameOperator ?? "",
                            $"%{normalizedFilter}%")

                        ||

                        EF.Functions.Like(
                            scanItem.Operator.LastnameOperator ?? "",
                            $"%{normalizedFilter}%")

                        ||

                        EF.Functions.Like(
                            scanItem.Operator.EmployeeNumber.ToString(),
                            $"%{normalizedFilter}%"));
                }
            }

            return query;
        }

        private static bool TryParseEmployeeNumber(string rawValue, out int employeeNumber)
        {
            employeeNumber = default;

            if (int.TryParse(rawValue, out employeeNumber))
            {
                return true;
            }

            var match = Regex.Match(rawValue.Trim(), @"^(\d+)");
            return match.Success && int.TryParse(match.Groups[1].Value, out employeeNumber);
        }

        private static (DateTime StartDate, DateTime EndDate) ParseDateRange(
            string? startDateRaw,
            string? endDateRaw)
        {
            var today = DateTime.Today;
            var startDate = today;
            var endDate = today;

            if (!string.IsNullOrWhiteSpace(startDateRaw) &&
                DateTime.TryParse(startDateRaw, out var parsedStartDate))
            {
                startDate = parsedStartDate.Date;
            }

            if (!string.IsNullOrWhiteSpace(endDateRaw) &&
                DateTime.TryParse(endDateRaw, out var parsedEndDate))
            {
                endDate = parsedEndDate.Date;
            }

            if (endDate < startDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            return (startDate, endDate);
        }

        [HttpPost("summary")]
        public async Task<IActionResult> GetSummary([FromBody] DashboardFilters filters)
        {
            var (startDate, endDate) = ParseDateRange(
                filters.StartDateFilter,
                filters.EndDateFilter);

            var query = BuildScansQuery(
                filters.OperationFilter,
                filters.EmployeeFilter,
                startDate,
                endDate,
                filters.StartTimeFilter,
                filters.EndTimeFilter);

            var totalScans = await query.CountAsync();

            var uniqueCodes = await query
                .Where(scanItem => scanItem.Code != null)
                .Select(scanItem => scanItem.Code!)
                .Distinct()
                .CountAsync();

            var uniqueOperators = await query
                .Select(scanItem => scanItem.OperatorId)
                .Distinct()
                .CountAsync();

            var operations = await query
                .Where(scanItem => scanItem.Operator.Operation != null)
                .Select(scanItem => scanItem.Operator.Operation!)
                .Distinct()
                .CountAsync();

            return Ok(new
            {
                totalScans,
                uniqueCodes,
                uniqueOperators,
                operations
            });
        }



        [HttpPost("chart")]
        public async Task<IActionResult> GetChartData([FromBody] DashboardFilters filters)
        {
            try
            {
                var (startDate, endDate) = ParseDateRange(
                    filters.StartDateFilter,
                    filters.EndDateFilter);

                var query = BuildScansQuery(
                    filters.OperationFilter,
                    filters.EmployeeFilter,
                    startDate,
                    endDate,
                    filters.StartTimeFilter,
                    filters.EndTimeFilter);

                var chartData = await query
                    .GroupBy(scanItem => new
                    {
                        scanItem.Operator.EmployeeNumber,
                        FullName =
                            (scanItem.Operator.NameOperator ?? "") + " " +
                            (scanItem.Operator.LastnameOperator ?? "")
                    })
                    .Select(group => new
                    {
                        employeeNumber = group.Key.EmployeeNumber,
                        fullName = group.Key.FullName.Trim(),
                        total = group.Count()
                    })
                    .OrderByDescending(item => item.total)
                    .ThenBy(item => item.fullName)
                    .ToListAsync();

                return Ok(new
                {
                    labels = chartData.Select(item => $"{item.employeeNumber} - {item.fullName}"),
                    totals = chartData.Select(item => item.total),
                    chartTitle = "Produccion por operador"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("chartHistory")]
        public async Task<IActionResult> GetChartHistory([FromBody] DashboardFilters filters)
        {
            try
            {
                var (startDate, endDate) = ParseDateRange(
                    filters.StartDateFilter,
                    filters.EndDateFilter);

                var query = BuildScansQuery(
                    filters.OperationFilter,
                    filters.EmployeeFilter,
                    startDate,
                    endDate,
                    filters.StartTimeFilter,
                    filters.EndTimeFilter);

                var isSingleDay = startDate == endDate;

                if (isSingleDay)
                {
                    var data = await query
                        .Where(s => s.ScannedAt != null)
                        .GroupBy(s => s.ScannedAt!.Value.Hour)
                        .Select(g => new { period = g.Key, total = g.Count() })
                        .OrderBy(g => g.period)
                        .ToListAsync();

                    return Ok(new
                    {
                        labels = data.Select(d => $"{d.period:D2}:00"),
                        totals = data.Select(d => d.total),
                        chartTitle = "Escaneos por hora"
                    });
                }
                else
                {
                    var data = await query
                        .Where(s => s.ScannedAt != null)
                        .GroupBy(s => s.ScannedAt!.Value.Date)
                        .Select(g => new { period = g.Key, total = g.Count() })
                        .OrderBy(g => g.period)
                        .ToListAsync();

                    return Ok(new
                    {
                        labels = data.Select(d => d.period.ToString("yyyy-MM-dd")),
                        totals = data.Select(d => d.total),
                        chartTitle = "Historial de escaneos"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("areas")]
        public async Task<IActionResult> GetAreas()
        {
            var areas = await _context.Areas
                .AsNoTracking()
                .OrderBy(a => a.AreaName)
                .Select(a => new { areaId = a.AreaId, areaName = a.AreaName, customerName = a.CustomerName })
                .ToListAsync();

            return Ok(areas);
        }

        [HttpGet("operations")]
        public async Task<IActionResult> GetOperations()
        {
            var operations = await _context.ProductionOperators
                .AsNoTracking()
                .Where(o => o.Operation != null && o.Operation != "")
                .Select(o => o.Operation!)
                .Distinct()
                .OrderBy(o => o)
                .ToListAsync();

            return Ok(operations);
        }

        [HttpGet("operator/{employeeNumber}")]
        public async Task<IActionResult> GetOperator(int employeeNumber)
        {
            var op = await _context.ProductionOperators
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.EmployeeNumber == employeeNumber);

            if (op == null)
                return NotFound(new { message = "Operador no encontrado" });

            return Ok(new
            {
                operatorId = op.OperatorId,
                employeeNumber = op.EmployeeNumber,
                nameOperator = op.NameOperator,
                lastnameOperator = op.LastnameOperator,
                areaId = op.AreaId,
                operation = op.Operation,
                goal = op.Goal,
                active = op.Active
            });
        }

        [HttpGet("operators/list")]
        public async Task<IActionResult> GetOperatorsList()
        {
            var operators = await _context.ProductionOperators
                .AsNoTracking()
                .OrderBy(o => o.Operation).ThenBy(o => o.EmployeeNumber)
                .Select(o => new
                {
                    operatorId = o.OperatorId,
                    employeeNumber = o.EmployeeNumber,
                    nameOperator = o.NameOperator,
                    lastnameOperator = o.LastnameOperator,
                    fullName = (o.NameOperator ?? "") + " " + (o.LastnameOperator ?? ""),
                    operation = o.Operation,
                    active = o.Active
                })
                .ToListAsync();

            return Ok(operators);
        }

        [HttpDelete("operator/{employeeNumber}")]
        public async Task<IActionResult> DeleteOperator(int employeeNumber)
        {
            var op = await _context.ProductionOperators
                .FirstOrDefaultAsync(o => o.EmployeeNumber == employeeNumber);

            if (op == null)
                return NotFound(new { message = "Operador no encontrado" });

            op.Active = false;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Operador desactivado correctamente" });
        }

        [HttpPost("saveOperator")]
        public async Task<IActionResult> SaveOperator([FromBody] SaveOperatorRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Datos invalidos" });

            if (string.IsNullOrWhiteSpace(request.NameOperator))
                return BadRequest(new { message = "El nombre es requerido" });

            if (string.IsNullOrWhiteSpace(request.LastnameOperator))
                return BadRequest(new { message = "El apellido es requerido" });

            var existingOperator = await _context.ProductionOperators
                .FirstOrDefaultAsync(o => o.EmployeeNumber == request.EmployeeNumber);

            if (existingOperator != null)
            {
                existingOperator.NameOperator = request.NameOperator.Trim();
                existingOperator.LastnameOperator = request.LastnameOperator.Trim();
                existingOperator.AreaId = request.AreaId;
                existingOperator.Operation = request.Operation?.Trim();
                existingOperator.Goal = request.Goal;
                existingOperator.Active = request.Active;
            }
            else
            {
                var newOperator = new Models.ProductionOperator
                {
                    EmployeeNumber = request.EmployeeNumber,
                    NameOperator = request.NameOperator.Trim(),
                    LastnameOperator = request.LastnameOperator.Trim(),
                    AreaId = request.AreaId,
                    Operation = request.Operation?.Trim(),
                    Goal = request.Goal,
                    Active = request.Active
                };
                _context.ProductionOperators.Add(newOperator);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Operador guardado correctamente" });
        }

        [HttpGet("operators")]
        public async Task<IActionResult> GetOperators([FromQuery] string? term = null)
        {
            var query = _context.ProductionOperators
                .AsNoTracking()
                .Where(operatorItem => operatorItem.Active != false);

            if (!string.IsNullOrWhiteSpace(term))
            {
                var normalizedTerm = term.Trim();

                query = query.Where(operatorItem =>
                    EF.Functions.Like(
                        operatorItem.EmployeeNumber.ToString(),
                        $"%{normalizedTerm}%")

                    ||

                    EF.Functions.Like(
                        operatorItem.NameOperator ?? "",
                        $"%{normalizedTerm}%")

                    ||

                    EF.Functions.Like(
                        operatorItem.LastnameOperator ?? "",
                        $"%{normalizedTerm}%")

                    ||

                    EF.Functions.Like(
                        (operatorItem.NameOperator ?? "") + " " +
                        (operatorItem.LastnameOperator ?? ""),
                        $"%{normalizedTerm}%")

                    ||

                    EF.Functions.Like(
                        (operatorItem.LastnameOperator ?? "") + " " +
                        (operatorItem.NameOperator ?? ""),
                        $"%{normalizedTerm}%"));
            }

            var operators = await query
                .OrderBy(operatorItem => operatorItem.EmployeeNumber)
                .Take(15)
                .Select(operatorItem => new
                {
                    employeeNumber = operatorItem.EmployeeNumber,

                    fullName =
                        (operatorItem.NameOperator ?? "") + " " +
                        (operatorItem.LastnameOperator ?? ""),

                    label =
                        operatorItem.EmployeeNumber + " - " +
                        (
                            (operatorItem.NameOperator ?? "") + " " +
                            (operatorItem.LastnameOperator ?? "")
                        ).Trim()
                })
                .ToListAsync();

            return Ok(operators);
        }

        [HttpPost("table")]
        public async Task<IActionResult> GetTable()
        {
            try
            {
                int.TryParse(Request.Form["draw"], out var draw);
                int.TryParse(Request.Form["start"], out var start);
                int.TryParse(Request.Form["length"], out var length);

                var orderColumn = Request.Form["order[0][column]"].FirstOrDefault() ?? "0";
                var orderDirection = Request.Form["order[0][dir]"].FirstOrDefault() ?? "desc";

                var operationFilter = Request.Form["operationFilter"].FirstOrDefault();
                var employeeFilter = Request.Form["employeeFilter"].FirstOrDefault();

                var startDateRaw = Request.Form["startDateFilter"].FirstOrDefault();
                var endDateRaw = Request.Form["endDateFilter"].FirstOrDefault();

                var startTimeFilter = Request.Form["startTimeFilter"].FirstOrDefault();
                var endTimeFilter = Request.Form["endTimeFilter"].FirstOrDefault();

                var (startDateFilter, endDateFilter) =
                    ParseDateRange(startDateRaw, endDateRaw);

                var query = BuildScansQuery(
                    operationFilter,
                    employeeFilter,
                    startDateFilter,
                    endDateFilter,
                    startTimeFilter,
                    endTimeFilter);

                query = (orderColumn, orderDirection) switch
                {
                    ("0", "asc") => query.OrderBy(scanItem => scanItem.ScannedAt),
                    ("0", "desc") => query.OrderByDescending(scanItem => scanItem.ScannedAt),

                    ("1", "asc") => query.OrderBy(scanItem => scanItem.ScannedAt),
                    ("1", "desc") => query.OrderByDescending(scanItem => scanItem.ScannedAt),

                    ("2", "asc") => query.OrderBy(scanItem => scanItem.Operator.EmployeeNumber),
                    ("2", "desc") => query.OrderByDescending(scanItem => scanItem.Operator.EmployeeNumber),

                    ("3", "asc") => query.OrderBy(scanItem => scanItem.Operator.NameOperator),
                    ("3", "desc") => query.OrderByDescending(scanItem => scanItem.Operator.NameOperator),

                    ("4", "asc") => query.OrderBy(scanItem => scanItem.Operator.Operation),
                    ("4", "desc") => query.OrderByDescending(scanItem => scanItem.Operator.Operation),

                    ("5", "asc") => query.OrderBy(scanItem => scanItem.Code),
                    ("5", "desc") => query.OrderByDescending(scanItem => scanItem.Code),

                    _ => query.OrderByDescending(scanItem => scanItem.ScannedAt)
                };

                var totalRecords = await query.CountAsync();

                var data = await query
                    .Skip(start)
                    .Take(length)
                    .Select(scanItem => new
                    {
                        id = scanItem.Id,
                        scannedAt = scanItem.ScannedAt,
                        employeeNumber = scanItem.Operator.EmployeeNumber,
                        fullName =
                            (scanItem.Operator.NameOperator ?? "") + " " +
                            (scanItem.Operator.LastnameOperator ?? ""),
                        operation = scanItem.Operator.Operation ?? "",
                        code = scanItem.Code ?? ""
                    })
                    .ToListAsync();

                return Ok(new
                {
                    draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = totalRecords,
                    data = data.Select(scanItem => new
                    {
                        id = scanItem.id,
                        scannedAt = scanItem.scannedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                        scanDate = scanItem.scannedAt?.ToString("yyyy-MM-dd") ?? "",
                        scanTime = scanItem.scannedAt?.ToString("HH:mm:ss") ?? "",
                        scanItem.employeeNumber,
                        scanItem.fullName,
                        scanItem.operation,
                        scanItem.code
                    })
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost("export")]
        public async Task<IActionResult> ExportToExcel([FromBody] DashboardExportRequest request)
        {
            try
            {
                var (startDate, endDate) = ParseDateRange(
                    request.StartDateFilter,
                    request.EndDateFilter);

                var query = BuildScansQuery(
                    request.OperationFilter,
                    request.EmployeeFilter,
                    startDate,
                    endDate,
                    request.StartTimeFilter,
                    request.EndTimeFilter);

                var records = await query
                    .OrderByDescending(scanItem => scanItem.ScannedAt)
                    .Select(scanItem => new
                    {
                        scannedAt = scanItem.ScannedAt,
                        employeeNumber = scanItem.Operator.EmployeeNumber,
                        fullName = (scanItem.Operator.NameOperator ?? "") + " " +
                                   (scanItem.Operator.LastnameOperator ?? ""),
                        operation = scanItem.Operator.Operation ?? "",
                        code = scanItem.Code ?? ""
                    })
                    .ToListAsync();

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Escaneos");

                worksheet.Cell("A1").Value = "Reporte de Escaneos de Operadores";
                worksheet.Cell("A1").Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Font.FontSize = 18;

                worksheet.Cell("A3").Value = "Operacion";
                worksheet.Cell("B3").Value = string.IsNullOrWhiteSpace(request.OperationFilter) ? "Todas" : request.OperationFilter;
                worksheet.Cell("D3").Value = "Empleado";
                worksheet.Cell("E3").Value = string.IsNullOrWhiteSpace(request.EmployeeFilter) ? "Todos" : request.EmployeeFilter;
                worksheet.Cell("G3").Value = "Rango";
                worksheet.Cell("H3").Value = $"{startDate:yyyy-MM-dd} a {endDate:yyyy-MM-dd}";

                worksheet.Cell("A5").Value = "Grafica de escaneos por operador (agrupado por operacion)";
                worksheet.Cell("A5").Style.Font.Bold = true;

                Color OpColor(int idx)
                {
                    var palette = new[]
                    {
                        Color.FromArgb(37, 99, 235),
                        Color.FromArgb(34, 197, 94),
                        Color.FromArgb(234, 179, 8),
                        Color.FromArgb(239, 68, 68),
                        Color.FromArgb(168, 85, 247),
                        Color.FromArgb(251, 146, 60),
                        Color.FromArgb(20, 184, 166),
                        Color.FromArgb(236, 72, 153),
                        Color.FromArgb(14, 165, 233),
                        Color.FromArgb(132, 204, 22),
                        Color.FromArgb(100, 100, 100)
                    };
                    return palette[idx % palette.Length];
                }

                // ── Query raw data grouped by (operator, operation) ──
                var rawOpData = await query
                    .GroupBy(scanItem => new
                    {
                        scanItem.Operator.EmployeeNumber,
                        FullName =
                            (scanItem.Operator.NameOperator ?? "") + " " +
                            (scanItem.Operator.LastnameOperator ?? ""),
                        Operation = scanItem.Operator.Operation ?? "Sin operacion"
                    })
                    .Select(group => new
                    {
                        group.Key.EmployeeNumber,
                        group.Key.FullName,
                        group.Key.Operation,
                        Total = group.Count()
                    })
                    .ToListAsync();

                // ── Chart 1: Top 20 operators, horizontal stacked bars by operation ──
                var topOperators = rawOpData
                    .GroupBy(o => new { o.EmployeeNumber, FullName = o.FullName.Trim() })
                    .Select(g => new
                    {
                        g.Key.EmployeeNumber,
                        g.Key.FullName,
                        Total = g.Sum(x => x.Total),
                        Ops = g.ToDictionary(x => x.Operation, x => x.Total)
                    })
                    .OrderByDescending(o => o.Total)
                    .Take(20)
                    .ToList();

                var allOps = topOperators
                    .SelectMany(o => o.Ops.Keys)
                    .Distinct()
                    .OrderBy(op => op)
                    .ToList();

                var c1Labels = topOperators.Select(o => $"{o.EmployeeNumber} - {o.FullName}").ToList();
                var c1MaxVal = topOperators.Any() ? topOperators.Max(o => o.Total) : 1;

                int c1ml = 200, c1mr = 60, c1mt = 50, c1mb = 70;
                int c1bh = 28, c1bs = 12;
                int c1rows = c1Labels.Count;
                int c1barsH = c1rows > 0 ? c1rows * (c1bh + c1bs) - c1bs : 0;
                int c1h = c1mt + c1mb + c1barsH;
                if (c1h < 250) c1h = 250;
                int c1w = 950;
                int c1pw = c1w - c1ml - c1mr;

                using (var bmp = new Bitmap(c1w, c1h))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.White);

                    using (var tf = new Font("Arial", 14, FontStyle.Bold))
                    using (var tb = new SolidBrush(Color.Black))
                    {
                        var txt = "Escaneos por operador";
                        var sz = g.MeasureString(txt, tf);
                        g.DrawString(txt, tf, tb, (c1w - sz.Width) / 2, 10);
                    }

                    if (c1Labels.Count > 0)
                    {
                        using (var ap = new Pen(Color.FromArgb(200, 200, 200), 1))
                        {
                            g.DrawLine(ap, c1ml, c1mt, c1ml + c1pw, c1mt);
                            g.DrawLine(ap, c1ml, c1mt, c1ml, c1mt + c1barsH);
                        }

                        using (var gp = new Pen(Color.FromArgb(230, 230, 230), 1))
                            for (int i = 1; i <= 5; i++)
                                g.DrawLine(gp, c1ml + (float)(i * c1pw / 5), c1mt, c1ml + (float)(i * c1pw / 5), c1mt + c1barsH);

                        using (var lf = new Font("Arial", 9, FontStyle.Regular))
                        using (var lb = new SolidBrush(Color.Black))
                        using (var vf = new Font("Arial", 9, FontStyle.Bold))
                        using (var vb = new SolidBrush(Color.FromArgb(37, 99, 235)))
                        {
                            for (int i = 0; i < c1Labels.Count; i++)
                            {
                                int y = c1mt + i * (c1bh + c1bs);
                                var op = topOperators[i];

                                var label = c1Labels[i];
                                var lsz = g.MeasureString(label, lf);
                                if (lsz.Width > c1ml - 10)
                                {
                                    while (g.MeasureString(label + "...", lf).Width > c1ml - 10 && label.Length > 1)
                                        label = label[..^1];
                                    label += "...";
                                    lsz = g.MeasureString(label, lf);
                                }
                                g.DrawString(label, lf, lb, c1ml - lsz.Width - 8, y + (c1bh - lsz.Height) / 2);

                                float curX = c1ml;
                                for (int oi = 0; oi < allOps.Count; oi++)
                                {
                                    var opName = allOps[oi];
                                    if (op.Ops.TryGetValue(opName, out var opTotal))
                                    {
                                        float segW = (float)opTotal / c1MaxVal * c1pw;
                                        if (segW < 1 && opTotal > 0) segW = 1;
                                        using (var sb = new SolidBrush(OpColor(oi)))
                                            g.FillRectangle(sb, curX, y, segW, c1bh);
                                        curX += segW;
                                    }
                                }

                                using (var bp = new Pen(Color.FromArgb(200, 200, 200), 1))
                                    g.DrawRectangle(bp, c1ml, y, curX - c1ml, c1bh);

                                var vt = op.Total.ToString();
                                var vs = g.MeasureString(vt, vf);
                                g.DrawString(vt, vf, vb, curX + 6, y + (c1bh - vs.Height) / 2);
                            }

                            using (var xf = new Font("Arial", 8, FontStyle.Regular))
                            using (var xb = new SolidBrush(Color.FromArgb(100, 100, 100)))
                                for (int i = 0; i <= 5; i++)
                                {
                                    var x = c1ml + (float)(i * c1pw / 5);
                                    var vt = ((int)(i * c1MaxVal / 5)).ToString();
                                    var vs = g.MeasureString(vt, xf);
                                    g.DrawString(vt, xf, xb, x - vs.Width / 2, c1mt + c1barsH + 4);
                                }

                            using (var lgf = new Font("Arial", 8, FontStyle.Regular))
                            {
                                int legY = c1mt + c1barsH + 20;
                                int legX = c1ml;
                                int sw = 12;
                                int gap = 8;
                                for (int oi = 0; oi < allOps.Count; oi++)
                                {
                                    if (legX + sw + 4 + g.MeasureString(allOps[oi], lgf).Width > c1w - 10 && legX != c1ml)
                                    {
                                        legX = c1ml;
                                        legY += sw + gap;
                                    }
                                    using (var sb = new SolidBrush(OpColor(oi)))
                                        g.FillRectangle(sb, legX, legY, sw, sw);
                                    g.DrawRectangle(Pens.Gray, legX, legY, sw, sw);
                                    legX += sw + 4;
                                    var ol = allOps[oi];
                                    var osz = g.MeasureString(ol, lgf);
                                    g.DrawString(ol, lgf, lb, legX, legY + (sw - osz.Height) / 2);
                                    legX += (int)osz.Width + gap;
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var nf = new Font("Arial", 14, FontStyle.Regular))
                        using (var nb = new SolidBrush(Color.Gray))
                        {
                            var txt = "Sin datos disponibles";
                            var sz = g.MeasureString(txt, nf);
                            g.DrawString(txt, nf, nb, (c1w - sz.Width) / 2, (c1h - sz.Height) / 2);
                        }
                    }

                    using var c1s = new MemoryStream();
                    bmp.Save(c1s, ImageFormat.Png);
                    c1s.Position = 0;
                    worksheet.AddPicture(c1s)
                        .MoveTo(worksheet.Cell("A6"))
                        .WithSize(c1w, c1h);
                }

                // ── Chart 2: Total scans per operation ──
                var opTotals = rawOpData
                    .GroupBy(o => o.Operation)
                    .Select(g => new { operation = g.Key, total = g.Sum(x => x.Total) })
                    .OrderByDescending(o => o.total)
                    .ToList();

                var c2Labels = opTotals.Select(d => d.operation).ToList();
                var c2Values = opTotals.Select(d => d.total).ToList();
                var c2MaxVal = c2Values.Any() ? c2Values.Max() : 1;

                int c2ml = 120, c2mr = 60, c2mt = 50, c2mb = 30;
                int c2bh = 28, c2bs = 12;
                int c2rows = c2Labels.Count;
                int c2barsH = c2rows > 0 ? c2rows * (c2bh + c2bs) - c2bs : 0;
                int c2h = c2mt + c2mb + c2barsH;
                if (c2h < 200) c2h = 200;
                int c2w = 900;
                int c2pw = c2w - c2ml - c2mr;

                int chart2HeaderRow = 6 + (int)Math.Ceiling(c1h / 20.0) + 2;
                worksheet.Cell($"A{chart2HeaderRow}").Value = "Grafica de escaneos por operacion";
                worksheet.Cell($"A{chart2HeaderRow}").Style.Font.Bold = true;

                int chart2Row = chart2HeaderRow + 1;

                using (var bmp = new Bitmap(c2w, c2h))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.White);

                    using (var tf = new Font("Arial", 14, FontStyle.Bold))
                    using (var tb = new SolidBrush(Color.Black))
                    {
                        var txt = "Escaneos por operacion";
                        var sz = g.MeasureString(txt, tf);
                        g.DrawString(txt, tf, tb, (c2w - sz.Width) / 2, 10);
                    }

                    if (c2Labels.Count > 0)
                    {
                        using (var ap = new Pen(Color.FromArgb(200, 200, 200), 1))
                        {
                            g.DrawLine(ap, c2ml, c2mt, c2ml + c2pw, c2mt);
                            g.DrawLine(ap, c2ml, c2mt, c2ml, c2mt + c2barsH);
                        }

                        using (var gp = new Pen(Color.FromArgb(230, 230, 230), 1))
                            for (int i = 1; i <= 5; i++)
                                g.DrawLine(gp, c2ml + (float)(i * c2pw / 5), c2mt, c2ml + (float)(i * c2pw / 5), c2mt + c2barsH);

                        using (var bbrush = new SolidBrush(Color.FromArgb(37, 99, 235)))
                        using (var bpen = new Pen(Color.FromArgb(37, 99, 235), 1))
                        using (var lf = new Font("Arial", 9, FontStyle.Regular))
                        using (var vf = new Font("Arial", 9, FontStyle.Bold))
                        using (var lb = new SolidBrush(Color.Black))
                        using (var vb = new SolidBrush(Color.FromArgb(37, 99, 235)))
                        {
                            for (int i = 0; i < c2Labels.Count; i++)
                            {
                                float bw = (float)c2Values[i] / c2MaxVal * c2pw;
                                if (bw < 1) bw = 1;
                                int y = c2mt + i * (c2bh + c2bs);

                                var label = c2Labels[i];
                                var lsz = g.MeasureString(label, lf);
                                if (lsz.Width > c2ml - 10)
                                {
                                    while (g.MeasureString(label + "...", lf).Width > c2ml - 10 && label.Length > 1)
                                        label = label[..^1];
                                    label += "...";
                                    lsz = g.MeasureString(label, lf);
                                }
                                g.DrawString(label, lf, lb, c2ml - lsz.Width - 8, y + (c2bh - lsz.Height) / 2);

                                g.FillRectangle(bbrush, c2ml, y, bw, c2bh);
                                g.DrawRectangle(bpen, c2ml, y, bw, c2bh);

                                var vt = c2Values[i].ToString();
                                var vs = g.MeasureString(vt, vf);
                                g.DrawString(vt, vf, vb, c2ml + bw + 6, y + (c2bh - vs.Height) / 2);
                            }
                        }

                        using (var xf = new Font("Arial", 8, FontStyle.Regular))
                        using (var xb = new SolidBrush(Color.FromArgb(100, 100, 100)))
                            for (int i = 0; i <= 5; i++)
                            {
                                var x = c2ml + (float)(i * c2pw / 5);
                                var vt = ((int)(i * c2MaxVal / 5)).ToString();
                                var vs = g.MeasureString(vt, xf);
                                g.DrawString(vt, xf, xb, x - vs.Width / 2, c2mt + c2barsH + 4);
                            }
                    }
                    else
                    {
                        using (var nf = new Font("Arial", 14, FontStyle.Regular))
                        using (var nb = new SolidBrush(Color.Gray))
                        {
                            var txt = "Sin datos disponibles";
                            var sz = g.MeasureString(txt, nf);
                            g.DrawString(txt, nf, nb, (c2w - sz.Width) / 2, (c2h - sz.Height) / 2);
                        }
                    }

                    using var c2s = new MemoryStream();
                    bmp.Save(c2s, ImageFormat.Png);
                    c2s.Position = 0;
                    worksheet.AddPicture(c2s)
                        .MoveTo(worksheet.Cell($"A{chart2Row}"))
                        .WithSize(c2w, c2h);
                }

                // ── Chart 3: History chart (hourly / daily trend) ──
                var isSingleDay = startDate == endDate;
                var c3Title = isSingleDay ? "Escaneos por hora" : "Historial de escaneos";

                string[] c3Labels;
                int[] c3Values;
                int c3MaxVal;

                if (isSingleDay)
                {
                    var hourlyData = await query
                        .Where(s => s.ScannedAt != null)
                        .GroupBy(s => s.ScannedAt!.Value.Hour)
                        .Select(g => new { period = g.Key, total = g.Count() })
                        .OrderBy(g => g.period)
                        .ToListAsync();
                    c3Labels = hourlyData.Select(d => $"{d.period:D2}:00").ToArray();
                    c3Values = hourlyData.Select(d => d.total).ToArray();
                }
                else
                {
                    var dailyData = await query
                        .Where(s => s.ScannedAt != null)
                        .GroupBy(s => s.ScannedAt!.Value.Date)
                        .Select(g => new { period = g.Key, total = g.Count() })
                        .OrderBy(g => g.period)
                        .ToListAsync();
                    c3Labels = dailyData.Select(d => d.period.ToString("yyyy-MM-dd")).ToArray();
                    c3Values = dailyData.Select(d => d.total).ToArray();
                }

                c3MaxVal = c3Values.Length > 0 ? c3Values.Max() : 1;

                int c3ml = 130, c3mr = 60, c3mt = 50, c3mb = 40;
                int c3bh = 26, c3bs = 8;
                int c3rows = c3Labels.Length;
                int c3barsH = c3rows > 0 ? c3rows * (c3bh + c3bs) - c3bs : 0;
                int c3h = c3mt + c3mb + c3barsH;
                if (c3h < 250) c3h = 250;
                if (c3h > 500) c3h = 500;
                int c3w = 950;
                int c3pw = c3w - c3ml - c3mr;

                int chart3HeaderRow = chart2Row + (int)Math.Ceiling(c2h / 20.0) + 2;
                worksheet.Cell($"A{chart3HeaderRow}").Value = c3Title;
                worksheet.Cell($"A{chart3HeaderRow}").Style.Font.Bold = true;

                int chart3Row = chart3HeaderRow + 1;

                using (var bmp = new Bitmap(c3w, c3h))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.White);

                    using (var tf = new Font("Arial", 14, FontStyle.Bold))
                    using (var tb = new SolidBrush(Color.Black))
                    {
                        var txt = c3Title;
                        var sz = g.MeasureString(txt, tf);
                        g.DrawString(txt, tf, tb, (c3w - sz.Width) / 2, 10);
                    }

                    if (c3Labels.Length > 0)
                    {
                        using (var ap = new Pen(Color.FromArgb(200, 200, 200), 1))
                        {
                            g.DrawLine(ap, c3ml, c3mt, c3ml + c3pw, c3mt);
                            g.DrawLine(ap, c3ml, c3mt, c3ml, c3mt + c3barsH);
                        }

                        using (var gp = new Pen(Color.FromArgb(230, 230, 230), 1))
                            for (int i = 1; i <= 5; i++)
                                g.DrawLine(gp, c3ml + (float)(i * c3pw / 5), c3mt, c3ml + (float)(i * c3pw / 5), c3mt + c3barsH);

                        using (var bbrush = new SolidBrush(Color.FromArgb(20, 184, 166)))
                        using (var bpen = new Pen(Color.FromArgb(20, 184, 166), 1))
                        using (var lf = new Font("Arial", 9, FontStyle.Regular))
                        using (var vf = new Font("Arial", 9, FontStyle.Bold))
                        using (var lb = new SolidBrush(Color.Black))
                        using (var vb = new SolidBrush(Color.FromArgb(20, 184, 166)))
                        {
                            for (int i = 0; i < c3Labels.Length; i++)
                            {
                                float bw = (float)c3Values[i] / c3MaxVal * c3pw;
                                if (bw < 1 && c3Values[i] > 0) bw = 1;
                                int y = c3mt + i * (c3bh + c3bs);

                                var label = c3Labels[i];
                                var lsz = g.MeasureString(label, lf);
                                if (lsz.Width > c3ml - 10)
                                {
                                    while (g.MeasureString(label + "...", lf).Width > c3ml - 10 && label.Length > 1)
                                        label = label[..^1];
                                    label += "...";
                                }
                                g.DrawString(label, lf, lb, c3ml - lsz.Width - 8, y + (c3bh - lsz.Height) / 2);

                                g.FillRectangle(bbrush, c3ml, y, bw, c3bh);
                                g.DrawRectangle(bpen, c3ml, y, bw, c3bh);

                                var vt = c3Values[i].ToString();
                                var vs = g.MeasureString(vt, vf);
                                g.DrawString(vt, vf, vb, c3ml + bw + 6, y + (c3bh - vs.Height) / 2);
                            }
                        }

                        using (var xf = new Font("Arial", 8, FontStyle.Regular))
                        using (var xb = new SolidBrush(Color.FromArgb(100, 100, 100)))
                            for (int i = 0; i <= 5; i++)
                            {
                                var x = c3ml + (float)(i * c3pw / 5);
                                var vt = ((int)(i * c3MaxVal / 5)).ToString();
                                var vs = g.MeasureString(vt, xf);
                                g.DrawString(vt, xf, xb, x - vs.Width / 2, c3mt + c3barsH + 4);
                            }
                    }
                    else
                    {
                        using (var nf = new Font("Arial", 14, FontStyle.Regular))
                        using (var nb = new SolidBrush(Color.Gray))
                        {
                            var txt = "Sin datos disponibles";
                            var sz = g.MeasureString(txt, nf);
                            g.DrawString(txt, nf, nb, (c3w - sz.Width) / 2, (c3h - sz.Height) / 2);
                        }
                    }

                    using var c3s = new MemoryStream();
                    bmp.Save(c3s, ImageFormat.Png);
                    c3s.Position = 0;
                    worksheet.AddPicture(c3s)
                        .MoveTo(worksheet.Cell($"A{chart3Row}"))
                        .WithSize(c3w, c3h);
                }

                var tableStartRow = chart3Row + (int)Math.Ceiling(c3h / 20.0) + 2;
                var headers = new[] { "Fecha", "Hora", "Empleado", "Nombre", "Operacion", "Codigo" };

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
                    worksheet.Cell(currentRow, 1).Value = record.scannedAt?.ToString("yyyy-MM-dd") ?? "";
                    worksheet.Cell(currentRow, 2).Value = record.scannedAt?.ToString("HH:mm:ss") ?? "";
                    worksheet.Cell(currentRow, 3).Value = record.employeeNumber;
                    worksheet.Cell(currentRow, 4).Value = record.fullName;
                    worksheet.Cell(currentRow, 5).Value = record.operation;
                    worksheet.Cell(currentRow, 6).Value = record.code;
                    currentRow++;
                }

                if (!records.Any())
                {
                    worksheet.Cell(currentRow, 1).Value = "No hay datos para los filtros seleccionados.";
                }

                worksheet.Columns("A:F").AdjustToContents();
                // Fila congelada eliminada por solicitud del usuario

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"ReporteEscaneosOperadores_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("scan/{id:int}")]
        public async Task<IActionResult> GetScan(int id)
        {
            var scan = await _context.ProductionOperatorsScans
                .AsNoTracking()
                .Include(s => s.Operator)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (scan == null)
                return NotFound(new { message = "Escaneo no encontrado" });

            return Ok(new
            {
                scan.Id,
                scan.Code,
                scannedAt = scan.ScannedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                scan.OperatorId,
                employeeNumber = scan.Operator.EmployeeNumber,
                fullName = (scan.Operator.NameOperator ?? "") + " " + (scan.Operator.LastnameOperator ?? ""),
                operation = scan.Operator.Operation ?? ""
            });
        }

        [HttpPut("scan/{id:int}")]
        public async Task<IActionResult> UpdateScan(int id, [FromBody] UpdateScanRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Datos invalidos" });

            var scan = await _context.ProductionOperatorsScans
                .FirstOrDefaultAsync(s => s.Id == id);

            if (scan == null)
                return NotFound(new { message = "Escaneo no encontrado" });

            if (!string.IsNullOrWhiteSpace(request.Code))
                scan.Code = request.Code.Trim();

            if (request.ScannedAt.HasValue)
                scan.ScannedAt = request.ScannedAt.Value;

            if (request.EmployeeNumber.HasValue)
            {
                var operatorEntity = await _context.ProductionOperators
                    .FirstOrDefaultAsync(o => o.EmployeeNumber == request.EmployeeNumber.Value);

                if (operatorEntity == null)
                    return NotFound(new { message = "Empleado no encontrado" });

                scan.OperatorId = operatorEntity.OperatorId;
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Escaneo actualizado correctamente" });
        }

        public class UpdateScanRequest
        {
            public string? Code { get; set; }
            public DateTime? ScannedAt { get; set; }
            public int? EmployeeNumber { get; set; }
        }

        public class DashboardFilters
        {
            public string? OperationFilter { get; set; }

            public string? EmployeeFilter { get; set; }

            public string? StartDateFilter { get; set; }

            public string? EndDateFilter { get; set; }

            public string? StartTimeFilter { get; set; }

            public string? EndTimeFilter { get; set; }
        }

        public class DashboardExportRequest : DashboardFilters
        {
        }

        public class SaveOperatorRequest
        {
            public int EmployeeNumber { get; set; }
            public string? NameOperator { get; set; }
            public string? LastnameOperator { get; set; }
            public int? AreaId { get; set; }
            public string? Operation { get; set; }
            public int? Goal { get; set; }
            public bool Active { get; set; } = true;
        }
    }
}
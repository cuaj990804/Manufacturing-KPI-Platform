using ClosedXML.Excel;
using GDIKPI.Data;
using GDIKPI.Hubs;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Drawing;
using System.Drawing.Imaging;
using System.Data;
using System.Text.RegularExpressions;

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductionOperatorsDashboardApiController : ControllerBase
    {
        private static readonly TimeOnly DefaultOperatorWorkdayStart = new(7, 0);
        private static readonly TimeOnly DefaultOperatorWorkdayEnd = new(17, 0);
        private readonly KpisContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly AuditService _auditService;

        public ProductionOperatorsDashboardApiController(
            KpisContext context,
            IHubContext<DashboardHub> hubContext,
            AuditService auditService)
        {
            _context = context;
            _hubContext = hubContext;
            _auditService = auditService;
        }

        private sealed class DashboardScanRecord
        {
            public long Id { get; init; }
            public string Source { get; init; } = string.Empty;
            public DateTime ScannedAt { get; init; }
            public int ReferenceNumber { get; init; }
            public string FullName { get; init; } = string.Empty;
            public string Operation { get; init; } = string.Empty;
            public string Code { get; init; } = string.Empty;
            public int? AreaId { get; init; }
            public int? ProductionLinesId { get; init; }
            public bool CanEdit { get; init; }
        }

        private sealed class ProductionLineStatsRecord
        {
            public int ProductionLinesId { get; init; }
            public int? LineNumber { get; init; }
            public string LineName { get; init; } = string.Empty;
            public int DailyGoal { get; init; }
            public int? AreaId { get; init; }
            public string AreaName { get; init; } = string.Empty;
            public string CustomerName { get; init; } = string.Empty;
            public int ProgramId { get; init; }
            public string ProgramDescription { get; init; } = string.Empty;
            public int ProducedPieces { get; init; }
        }

        private sealed class DashboardChartRecord
        {
            public string Source { get; init; } = string.Empty;
            public DateTime ScannedAt { get; init; }
            public int ReferenceNumber { get; init; }
            public string FullName { get; init; } = string.Empty;
            public string Operation { get; init; } = string.Empty;
            public int? AreaId { get; init; }
            public int? ProductionLinesId { get; init; }
            public int Quantity { get; init; } = 1;
        }

        private async Task<List<DashboardScanRecord>> BuildScansAsync(
            string? operationFilter,
            string? employeeFilter,
            DateTime startDate,
            DateTime endDate,
            string? startTimeFilter,
            string? endTimeFilter,
            int? areaId,
            int? productionLinesId)
        {
            var endExclusive = endDate.Date.AddDays(1);

            var operatorScans = await _context.ProductionOperatorsScans
                .AsNoTracking()
                .Where(scanItem =>
                    scanItem.ScannedAt.HasValue &&
                    scanItem.ScannedAt >= startDate.Date &&
                    scanItem.ScannedAt < endExclusive)
                .Select(scanItem => new DashboardScanRecord
                {
                    Id = scanItem.Id,
                    Source = "OPERATOR",
                    ScannedAt = scanItem.ScannedAt!.Value,
                    ReferenceNumber = scanItem.Operator.EmployeeNumber,
                    FullName = (scanItem.Operator.NameOperator ?? "") + " " +
                               (scanItem.Operator.LastnameOperator ?? ""),
                    Operation = scanItem.Operator.Operation ?? "Sin operacion",
                    Code = scanItem.Code ?? "",
                    AreaId = scanItem.Operator.AreaId,
                    ProductionLinesId = scanItem.Operator.ProductionLinesId,
                    CanEdit = true
                })
                .ToListAsync();

            var lineScans = await (
                from scanItem in _context.ScannerProductions.AsNoTracking()
                join line in _context.ProductionLines.AsNoTracking()
                    on scanItem.LineId equals line.ProductionLinesId
                where scanItem.ScannerProductionDateTime >= startDate.Date &&
                      scanItem.ScannerProductionDateTime < endExclusive
                select new DashboardScanRecord
                {
                    Id = scanItem.ScannerProductionId,
                    Source = "LINE",
                    ScannedAt = scanItem.ScannerProductionDateTime,
                    ReferenceNumber = line.LineNumber ?? line.ProductionLinesId,
                    FullName = "LINEA " + (line.LineNumber ?? line.ProductionLinesId),
                    Operation = "VOLANTES",
                    Code = scanItem.ScannerValue,
                    AreaId = line.AreaId,
                    ProductionLinesId = line.ProductionLinesId,
                    CanEdit = false
                }).ToListAsync();

            IEnumerable<DashboardScanRecord> query = operatorScans.Concat(lineScans);

            if (areaId.HasValue)
            {
                query = query.Where(scanItem => scanItem.AreaId == areaId.Value);
            }

            if (productionLinesId.HasValue)
            {
                query = query.Where(scanItem => scanItem.ProductionLinesId == productionLinesId.Value);
            }

            // FILTRO HORA INICIO
            if (TimeSpan.TryParse(startTimeFilter, out var startTime))
            {
                query = query.Where(scanItem =>
                    scanItem.ScannedAt.TimeOfDay >= startTime);
            }

            // FILTRO HORA FIN
            if (TimeSpan.TryParse(endTimeFilter, out var endTime))
            {
                query = query.Where(scanItem =>
                    scanItem.ScannedAt.TimeOfDay <= endTime);
            }

            // FILTRO OPERACION
            if (!string.IsNullOrWhiteSpace(operationFilter))
            {
                query = query.Where(scanItem =>
                    scanItem.Operation == operationFilter);
            }

            // FILTRO EMPLEADO
            if (!string.IsNullOrWhiteSpace(employeeFilter))
            {
                if (TryParseEmployeeNumber(employeeFilter, out var employeeNumber))
                {
                    query = query.Where(scanItem =>
                        scanItem.ReferenceNumber == employeeNumber);
                }
                else
                {
                    var normalizedFilter = employeeFilter.Trim();

                    query = query.Where(scanItem =>
                        scanItem.FullName.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
                        scanItem.ReferenceNumber.ToString().Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase));
                }
            }

            return query.ToList();
        }

        private async Task<List<DashboardChartRecord>> BuildChartRecordsAsync(
            string? operationFilter,
            string? employeeFilter,
            DateTime startDate,
            DateTime endDate,
            string? startTimeFilter,
            string? endTimeFilter,
            int? areaId,
            int? productionLinesId)
        {
            var endExclusive = endDate.Date.AddDays(1);
            var startProductionDate = DateOnly.FromDateTime(startDate.Date);
            var endProductionDate = DateOnly.FromDateTime(endDate.Date);

            var operatorRecords = await _context.ProductionOperatorsScans
                .AsNoTracking()
                .Where(scanItem =>
                    scanItem.ScannedAt.HasValue &&
                    scanItem.ScannedAt >= startDate.Date &&
                    scanItem.ScannedAt < endExclusive)
                .Select(scanItem => new DashboardChartRecord
                {
                    Source = "OPERATOR",
                    ScannedAt = scanItem.ScannedAt!.Value,
                    ReferenceNumber = scanItem.Operator.EmployeeNumber,
                    FullName = (scanItem.Operator.NameOperator ?? "") + " " +
                               (scanItem.Operator.LastnameOperator ?? ""),
                    Operation = scanItem.Operator.Operation ?? "Sin operacion",
                    AreaId = scanItem.Operator.AreaId,
                    ProductionLinesId = scanItem.Operator.ProductionLinesId,
                    Quantity = 1
                })
                .ToListAsync();

            var lineProductionQuery = _context.ProductionData
                .AsNoTracking()
                .Where(production =>
                    production.ProductionDate >= startProductionDate &&
                    production.ProductionDate <= endProductionDate &&
                    (production.ProducedPieces ?? 0) > 0 &&
                    production.ProductionLines.IsActive &&
                    (production.ProductionLines.LineName == null ||
                     !production.ProductionLines.LineName.ToUpper().Contains("OVERALL")));

            if (areaId.HasValue)
            {
                lineProductionQuery = lineProductionQuery
                    .Where(production => production.ProductionLines.AreaId == areaId.Value);
            }

            if (productionLinesId.HasValue)
            {
                lineProductionQuery = lineProductionQuery
                    .Where(production => production.ProductionLinesId == productionLinesId.Value);
            }

            var lineProduction = await lineProductionQuery
                .Select(production => new
                {
                    production.ProductionDate,
                    production.StartHour,
                    production.ProducedPieces,
                    production.ProductionLinesId,
                    production.ProductionLines.LineNumber,
                    production.ProductionLines.AreaId
                })
                .ToListAsync();

            var lineRecords = lineProduction.Select(production => new DashboardChartRecord
            {
                Source = "LINE",
                ScannedAt = production.ProductionDate.ToDateTime(production.StartHour),
                ReferenceNumber = production.LineNumber ?? production.ProductionLinesId,
                FullName = "LINEA " + (production.LineNumber ?? production.ProductionLinesId),
                Operation = "VOLANTES",
                AreaId = production.AreaId,
                ProductionLinesId = production.ProductionLinesId,
                Quantity = production.ProducedPieces ?? 0
            });

            IEnumerable<DashboardChartRecord> query = operatorRecords.Concat(lineRecords);

            if (areaId.HasValue)
                query = query.Where(item => item.AreaId == areaId.Value);

            if (productionLinesId.HasValue)
                query = query.Where(item => item.ProductionLinesId == productionLinesId.Value);

            if (TimeSpan.TryParse(startTimeFilter, out var startTime))
                query = query.Where(item => item.ScannedAt.TimeOfDay >= startTime);

            if (TimeSpan.TryParse(endTimeFilter, out var endTime))
                query = query.Where(item => item.ScannedAt.TimeOfDay <= endTime);

            if (!string.IsNullOrWhiteSpace(operationFilter))
                query = query.Where(item => item.Operation == operationFilter);

            if (!string.IsNullOrWhiteSpace(employeeFilter))
            {
                if (TryParseEmployeeNumber(employeeFilter, out var employeeNumber))
                {
                    query = query.Where(item => item.ReferenceNumber == employeeNumber);
                }
                else
                {
                    var normalizedFilter = employeeFilter.Trim();
                    query = query.Where(item =>
                        item.FullName.Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
                        item.ReferenceNumber.ToString().Contains(normalizedFilter, StringComparison.OrdinalIgnoreCase));
                }
            }

            return query.ToList();
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

        private static string DescribeOperator(Models.ProductionOperator operatorEntity)
        {
            var fullName = ((operatorEntity.NameOperator ?? "") + " " + (operatorEntity.LastnameOperator ?? "")).Trim();

            return $"OperatorId: {operatorEntity.OperatorId}, Empleado: {operatorEntity.EmployeeNumber}, Nombre: {fullName}, AreaId: {operatorEntity.AreaId}, LineaId: {operatorEntity.ProductionLinesId}, Operacion: {operatorEntity.Operation}, Meta: {operatorEntity.Goal}, Activo: {operatorEntity.Active}";
        }

        private static string DescribeProductionLine(ProductionLine line)
        {
            return $"ProductionLinesId: {line.ProductionLinesId}, AreaId: {line.AreaId}, Numero: {line.LineNumber}, Nombre: {line.LineName}, Meta: {line.DailyGoal}, Personal: {line.PersonalQuantity}, TiempoEstandar: {line.StandardTime}, Activa: {line.IsActive}";
        }

        private static string DescribeScan(Models.ProductionOperatorsScan scan)
        {
            return $"ScanId: {scan.Id}, Codigo: {scan.Code}, Fecha: {scan.ScannedAt?.ToString("yyyy-MM-dd HH:mm:ss")}, Empleado: {scan.Operator.EmployeeNumber}, Operacion: {scan.Operator.Operation}, OperatorId: {scan.OperatorId}";
        }

        private static string ExtractAuditDetailValue(string? details, params string[] labels)
        {
            if (string.IsNullOrWhiteSpace(details))
            {
                return "";
            }

            foreach (var label in labels)
            {
                var match = Regex.Match(
                    details,
                    $"{Regex.Escape(label)}:\\s*([^,.]+)",
                    RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            return "";
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

            var query = await BuildScansAsync(
                filters.OperationFilter,
                filters.EmployeeFilter,
                startDate,
                endDate,
                filters.StartTimeFilter,
                filters.EndTimeFilter,
                filters.AreaId,
                filters.ProductionLinesId);

            var totalScans = query.Count;

            var uniqueCodes = query
                .Where(scanItem => scanItem.Code != "")
                .Select(scanItem => scanItem.Code)
                .Distinct()
                .Count();

            var uniqueOperators = query
                .Select(scanItem => new { scanItem.Source, scanItem.ReferenceNumber })
                .Distinct()
                .Count();

            var operations = query
                .Where(scanItem => scanItem.Operation != "")
                .Select(scanItem => scanItem.Operation)
                .Distinct()
                .Count();

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

                var query = await BuildChartRecordsAsync(
                    filters.OperationFilter,
                    filters.EmployeeFilter,
                    startDate,
                    endDate,
                    filters.StartTimeFilter,
                    filters.EndTimeFilter,
                    filters.AreaId,
                    filters.ProductionLinesId);

                var chartData = query
                    .GroupBy(scanItem => new
                    {
                        scanItem.Source,
                        scanItem.ReferenceNumber,
                        scanItem.FullName
                    })
                    .Select(group => new
                    {
                        referenceNumber = group.Key.ReferenceNumber,
                        fullName = group.Key.FullName.Trim(),
                        total = group.Sum(item => item.Quantity)
                    })
                    .OrderByDescending(item => item.total)
                    .ThenBy(item => item.fullName)
                    .ToList();

                return Ok(new
                {
                    labels = chartData.Select(item => item.fullName),
                    totals = chartData.Select(item => item.total),
                    chartTitle = "Produccion por operador / linea"
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

                var query = await BuildChartRecordsAsync(
                    filters.OperationFilter,
                    filters.EmployeeFilter,
                    startDate,
                    endDate,
                    filters.StartTimeFilter,
                    filters.EndTimeFilter,
                    filters.AreaId,
                    filters.ProductionLinesId);

                var isSingleDay = startDate == endDate;

                if (isSingleDay)
                {
                    var data = query
                        .GroupBy(s => s.ScannedAt.Hour)
                        .Select(g => new { period = g.Key, total = g.Sum(item => item.Quantity) })
                        .OrderBy(g => g.period)
                        .ToList();

                    return Ok(new
                    {
                        labels = data.Select(d => $"{d.period:D2}:00"),
                        totals = data.Select(d => d.total),
                        chartTitle = "Produccion por hora"
                    });
                }
                else
                {
                    var data = query
                        .GroupBy(s => s.ScannedAt.Date)
                        .Select(g => new { period = g.Key, total = g.Sum(item => item.Quantity) })
                        .OrderBy(g => g.period)
                        .ToList();

                    return Ok(new
                    {
                        labels = data.Select(d => d.period.ToString("yyyy-MM-dd")),
                        totals = data.Select(d => d.total),
                        chartTitle = "Historial de produccion"
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

        [HttpGet("production-lines")]
        public async Task<IActionResult> GetProductionLines([FromQuery] int? areaId = null)
        {
            var query = _context.ProductionLines
                .AsNoTracking()
                .Where(line => line.IsActive);

            if (areaId.HasValue)
            {
                query = query.Where(line => line.AreaId == areaId.Value);
            }

            var lines = await query
                .OrderBy(line => line.Area!.CustomerName)
                .ThenBy(line => line.Area!.AreaName)
                .ThenBy(line => line.LineNumber)
                .Select(line => new
                {
                    productionLinesId = line.ProductionLinesId,
                    lineNumber = line.LineNumber,
                    lineName = line.LineName,
                    areaId = line.AreaId,
                    areaName = line.Area != null ? line.Area.AreaName : "",
                    customerName = line.Area != null ? line.Area.CustomerName : ""
                })
                .ToListAsync();

            return Ok(lines);
        }

        [HttpGet("production-lines/manage")]
        public async Task<IActionResult> GetProductionLinesForManagement([FromQuery] int? areaId = null)
        {
            var query = _context.ProductionLines.AsNoTracking().AsQueryable();

            if (areaId.HasValue)
            {
                query = query.Where(line => line.AreaId == areaId.Value);
            }

            var lines = await query
                .OrderByDescending(line => line.IsActive)
                .ThenBy(line => line.Area!.CustomerName)
                .ThenBy(line => line.Area!.AreaName)
                .ThenBy(line => line.LineNumber)
                .Select(line => new
                {
                    productionLinesId = line.ProductionLinesId,
                    lineNumber = line.LineNumber,
                    lineName = line.LineName,
                    dailyGoal = line.DailyGoal,
                    personalQuantity = line.PersonalQuantity,
                    standardTime = line.StandardTime,
                    areaId = line.AreaId,
                    areaName = line.Area != null ? line.Area.AreaName : "",
                    customerName = line.Area != null ? line.Area.CustomerName : "",
                    isActive = line.IsActive
                })
                .ToListAsync();

            return Ok(lines);
        }

        [HttpPost("save-production-line")]
        public async Task<IActionResult> SaveProductionLine([FromBody] SaveProductionLineRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Datos invalidos" });

            if (request.AreaId <= 0 || !await _context.Areas.AsNoTracking().AnyAsync(area => area.AreaId == request.AreaId))
                return BadRequest(new { message = "Seleccione un area valida" });

            if (request.LineNumber <= 0)
                return BadRequest(new { message = "El numero de linea debe ser mayor que cero" });

            if (request.DailyGoal <= 0)
                return BadRequest(new { message = "La meta diaria debe ser mayor que cero" });

            if (request.PersonalQuantity <= 0)
                return BadRequest(new { message = "La cantidad de personal debe ser mayor que cero" });

            if (request.StandardTime.HasValue && request.StandardTime.Value < 0)
                return BadRequest(new { message = "El tiempo estandar no puede ser negativo" });

            var lineName = request.LineName?.Trim();
            if (lineName?.Length > 100)
                return BadRequest(new { message = "El nombre de linea no puede exceder 100 caracteres" });

            var duplicate = await _context.ProductionLines.AsNoTracking().AnyAsync(line =>
                line.AreaId == request.AreaId &&
                line.LineNumber == request.LineNumber &&
                line.ProductionLinesId != request.ProductionLinesId);

            if (duplicate)
                return Conflict(new { message = "Ya existe una linea con ese numero en el area seleccionada" });

            ProductionLine line;
            string action;
            string details;

            if (request.ProductionLinesId.HasValue && request.ProductionLinesId.Value > 0)
            {
                line = await _context.ProductionLines
                    .FirstOrDefaultAsync(item => item.ProductionLinesId == request.ProductionLinesId.Value);

                if (line == null)
                    return NotFound(new { message = "La linea no existe" });

                if (line.AreaId != request.AreaId)
                    return BadRequest(new { message = "El area de una linea existente no se puede cambiar desde este dashboard" });

                var previousDetails = DescribeProductionLine(line);
                line.LineNumber = request.LineNumber;
                line.LineName = lineName;
                line.DailyGoal = request.DailyGoal;
                line.PersonalQuantity = request.PersonalQuantity;
                line.StandardTime = request.StandardTime;
                line.IsActive = request.IsActive;
                action = "UPDATE";
                details = $"Linea actualizada. Antes: {previousDetails}. Despues: {DescribeProductionLine(line)}";
            }
            else
            {
                var shiftNumber = await _context.Shifts.AsNoTracking()
                    .OrderBy(shift => shift.ShiftNumber)
                    .Select(shift => (int?)shift.ShiftNumber)
                    .FirstOrDefaultAsync();

                if (!shiftNumber.HasValue)
                    return BadRequest(new { message = "No hay un turno registrado para crear la linea" });

                line = new ProductionLine
                {
                    AreaId = request.AreaId,
                    LineNumber = request.LineNumber,
                    LineName = lineName,
                    DailyGoal = request.DailyGoal,
                    PersonalQuantity = request.PersonalQuantity,
                    StandardTime = request.StandardTime,
                    ShiftNumber = shiftNumber.Value,
                    IsActive = request.IsActive
                };
                _context.ProductionLines.Add(line);
                action = "CREATE";
                details = "";
            }

            await _context.SaveChangesAsync();

            if (action == "CREATE")
            {
                details = $"Linea creada. Datos: {DescribeProductionLine(line)}";
            }

            await _auditService.LogProductionLineAction(action, line.ProductionLinesId, details);
            await NotifyOperatorStatsUpdatedAsync("VOLANTES");

            return Ok(new
            {
                success = true,
                productionLinesId = line.ProductionLinesId,
                message = "Linea guardada correctamente"
            });
        }

        [HttpGet("operations")]
        public async Task<IActionResult> GetOperations([FromQuery] int? areaId = null, [FromQuery] int? productionLinesId = null)
        {
            var query = _context.ProductionOperators
                .AsNoTracking()
                .Where(o => o.Operation != null && o.Operation != "");

            if (areaId.HasValue)
            {
                query = query.Where(o => o.AreaId == areaId.Value);
            }

            if (productionLinesId.HasValue)
            {
                query = query.Where(o => o.ProductionLinesId == productionLinesId.Value);
            }

            var operations = await query
                .Select(o => o.Operation!)
                .Distinct()
                .OrderBy(o => o)
                .ToListAsync();

            return Ok(operations);
        }

        [HttpGet("operator/{employeeNumber}")]
        public async Task<IActionResult> GetOperator(int employeeNumber, [FromQuery] int? areaId = null, [FromQuery] int? productionLinesId = null)
        {
            var query = _context.ProductionOperators
                .AsNoTracking()
                .Where(o => o.EmployeeNumber == employeeNumber);

            if (areaId.HasValue)
            {
                query = query.Where(o => o.AreaId == areaId.Value);
            }

            if (productionLinesId.HasValue)
            {
                query = query.Where(o => o.ProductionLinesId == productionLinesId.Value);
            }

            var op = await query.FirstOrDefaultAsync();

            if (op == null)
                return NotFound(new { message = "Operador no encontrado" });

            return Ok(new
            {
                operatorId = op.OperatorId,
                employeeNumber = op.EmployeeNumber,
                nameOperator = op.NameOperator,
                lastnameOperator = op.LastnameOperator,
                areaId = op.AreaId,
                productionLinesId = op.ProductionLinesId,
                operation = op.Operation,
                goal = op.Goal,
                active = op.Active
            });
        }

        [HttpGet("operators/list")]
        public async Task<IActionResult> GetOperatorsList([FromQuery] int? areaId = null, [FromQuery] int? productionLinesId = null)
        {
            var query = _context.ProductionOperators
                .AsNoTracking();

            if (areaId.HasValue)
            {
                query = query.Where(o => o.AreaId == areaId.Value);
            }

            if (productionLinesId.HasValue)
            {
                query = query.Where(o => o.ProductionLinesId == productionLinesId.Value);
            }

            var operators = await query
                .OrderBy(o => o.Operation).ThenBy(o => o.EmployeeNumber)
                .Select(o => new
                {
                    operatorId = o.OperatorId,
                    employeeNumber = o.EmployeeNumber,
                    nameOperator = o.NameOperator,
                    lastnameOperator = o.LastnameOperator,
                    fullName = (o.NameOperator ?? "") + " " + (o.LastnameOperator ?? ""),
                    areaName = o.Area != null ? o.Area.AreaName : "",
                    customerName = o.Area != null ? o.Area.CustomerName : "",
                    productionLinesId = o.ProductionLinesId,
                    lineNumber = o.ProductionLines != null ? o.ProductionLines.LineNumber : null,
                    lineName = o.ProductionLines != null ? o.ProductionLines.LineName : "",
                    operation = o.Operation,
                    active = o.Active
                })
                .ToListAsync();

            return Ok(operators);
        }

        [HttpGet("operator-stats")]
        public async Task<IActionResult> GetOperatorStats()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var now = DateTime.Now;

            var stats = await _context.ProductionOperators
                .AsNoTracking()
                .Where(o => o.Active != false)
                .Select(o => new
                {
                    o.EmployeeNumber,
                    o.AreaId,
                    o.ProductionLinesId,
                    FullName = ((o.NameOperator ?? "") + " " + (o.LastnameOperator ?? "")).Trim(),
                    Operation = string.IsNullOrWhiteSpace(o.Operation) ? "Sin operacion" : o.Operation!,
                    AreaName = o.Area != null ? o.Area.AreaName : "",
                    CustomerName = o.Area != null ? o.Area.CustomerName : "",
                    LineName = o.ProductionLines != null ? o.ProductionLines.LineName : "",
                    Goal = o.Goal ?? 1,
                    ScanCount = o.ProductionOperatorsScans
                        .Count(s => s.ScannedAt >= today && s.ScannedAt < tomorrow)
                })
                .OrderBy(o => o.Operation)
                .ThenByDescending(o => o.ScanCount)
                .ThenBy(o => o.FullName)
                .ToListAsync();

            var areaIds = stats
                .Where(o => o.AreaId.HasValue)
                .Select(o => o.AreaId!.Value)
                .Distinct()
                .ToList();

            var productionLineIds = stats
                .Where(o => o.ProductionLinesId.HasValue)
                .Select(o => o.ProductionLinesId!.Value)
                .Distinct()
                .ToList();

            var areaSchedules = (await _context.ProductionLines
                .AsNoTracking()
                .Include(line => line.ShiftNumberNavigation)
                .Include(line => line.Breaks)
                .Where(line => line.AreaId.HasValue && areaIds.Contains(line.AreaId.Value) && line.IsActive)
                .ToListAsync())
                .Where(line => line.ShiftNumberNavigation.StartTime.HasValue)
                .GroupBy(line => line.AreaId)
                .ToDictionary(
                    group => group.Key!.Value,
                    group => ToOperatorWorkSchedule(
                        group.OrderBy(line => line.ShiftNumberNavigation.StartTime).First()));

            var lineSchedules = (await _context.ProductionLines
                .AsNoTracking()
                .Include(line => line.ShiftNumberNavigation)
                .Include(line => line.Breaks)
                .Where(line => productionLineIds.Contains(line.ProductionLinesId) && line.ShiftNumberNavigation.StartTime.HasValue)
                .ToListAsync())
                .ToDictionary(
                    line => line.ProductionLinesId,
                    ToOperatorWorkSchedule);

            var grouped = stats
                .GroupBy(o => new
                {
                    o.Operation,
                    o.AreaId,
                    o.ProductionLinesId,
                    o.AreaName,
                    o.CustomerName,
                    o.LineName
                })
                .Select(g => new
                {
                    operation = g.Key.Operation,
                    areaId = g.Key.AreaId,
                    productionLinesId = g.Key.ProductionLinesId,
                    areaName = g.Key.AreaName,
                    customerName = g.Key.CustomerName,
                    lineName = g.Key.LineName,
                    operators = g
                        .Select(o =>
                        {
                            var workSchedule = GetWorkSchedule(o.AreaId, o.ProductionLinesId, areaSchedules, lineSchedules);

                            return new
                            {
                                o.EmployeeNumber,
                                o.FullName,
                                o.Goal,
                                o.ScanCount,
                                percentage = GetRealTimePercentage(o.ScanCount, o.Goal, today, workSchedule, now),
                                finalPercentage = o.Goal > 0 ? o.ScanCount * 100.0 / o.Goal : 0,
                                expectedPiecesNow = GetExpectedPiecesNow(o.Goal, today, workSchedule, now)
                            };
                        })
                        .ToList()
                })
                .ToList();

            return Ok(grouped);
        }

        [HttpGet("production-line-stats")]
        public async Task<IActionResult> GetProductionLineStats([FromQuery] int? areaId = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var now = DateTime.Now;

            var linesQuery = _context.ProductionLines
                .AsNoTracking()
                .Include(line => line.Area)
                .Include(line => line.ShiftNumberNavigation)
                .Include(line => line.Breaks)
                .Where(line => line.IsActive);

            if (areaId.HasValue)
            {
                linesQuery = linesQuery.Where(line => line.AreaId == areaId.Value);
            }

            var activeLines = await linesQuery
                .OrderBy(line => line.Area!.CustomerName)
                .ThenBy(line => line.Area!.AreaName)
                .ThenBy(line => line.LineNumber)
                .ToListAsync();

            var lineIds = activeLines
                .Select(line => line.ProductionLinesId)
                .ToList();

            var production = await _context.ProductionData
                .AsNoTracking()
                .Where(item => item.ProductionDate == today && lineIds.Contains(item.ProductionLinesId))
                .Select(item => new
                {
                    item.ProductionLinesId,
                    item.ProgramId,
                    item.ProgramDescription,
                    ProducedPieces = item.ProducedPieces ?? 0
                })
                .ToListAsync();

            var productionByLine = production
                .GroupBy(item => item.ProductionLinesId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var latestPrograms = await _context.ProductionLines
                .AsNoTracking()
                .Where(line => lineIds.Contains(line.ProductionLinesId))
                .Select(line => new
                {
                    line.ProductionLinesId,
                    Program = line.ProductionData
                        .OrderByDescending(item => item.ProductionDate)
                        .ThenByDescending(item => item.EndHour)
                        .ThenByDescending(item => item.ProductionId)
                        .Select(item => new
                        {
                            item.ProgramId,
                            item.ProgramDescription,
                            item.ProductionDate,
                            item.EndHour,
                            item.ProductionId
                        })
                        .FirstOrDefault()
                })
                .ToDictionaryAsync(item => item.ProductionLinesId, item => item.Program);

            var latestProgramsByArea = activeLines
                .Where(line => line.AreaId.HasValue)
                .Select(line => new
                {
                    AreaId = line.AreaId!.Value,
                    Program = latestPrograms.GetValueOrDefault(line.ProductionLinesId)
                })
                .Where(item =>
                    item.Program != null &&
                    (item.Program.ProgramId > 0 ||
                     (!string.IsNullOrWhiteSpace(item.Program.ProgramDescription) &&
                      item.Program.ProgramDescription.Replace("_", " ").Trim().ToUpperInvariant() != "SIN PROGRAMA")))
                .GroupBy(item => item.AreaId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(item => item.Program!.ProductionDate)
                        .ThenByDescending(item => item.Program!.EndHour)
                        .ThenByDescending(item => item.Program!.ProductionId)
                        .First()
                        .Program);

            var stats = new List<ProductionLineStatsRecord>();

            foreach (var line in activeLines)
            {
                if (!productionByLine.TryGetValue(line.ProductionLinesId, out var lineProduction) || lineProduction.Count == 0)
                {
                    latestPrograms.TryGetValue(line.ProductionLinesId, out var latestProgram);
                    var lineHasKnownProgram = latestProgram != null &&
                        (latestProgram.ProgramId > 0 ||
                         (!string.IsNullOrWhiteSpace(latestProgram.ProgramDescription) &&
                          latestProgram.ProgramDescription.Replace("_", " ").Trim().ToUpperInvariant() != "SIN PROGRAMA"));

                    if (!lineHasKnownProgram && line.AreaId.HasValue)
                    {
                        latestProgramsByArea.TryGetValue(line.AreaId.Value, out latestProgram);
                    }

                    var programId = latestProgram?.ProgramId ?? 0;
                    var programDescription = string.IsNullOrWhiteSpace(latestProgram?.ProgramDescription)
                        ? programId > 0 ? $"PROGRAMA {programId}" : "SIN PROGRAMA"
                        : latestProgram!.ProgramDescription!.Trim();

                    stats.Add(new ProductionLineStatsRecord
                    {
                        ProductionLinesId = line.ProductionLinesId,
                        LineNumber = line.LineNumber,
                        LineName = line.LineName ?? "",
                        DailyGoal = line.DailyGoal,
                        AreaId = line.AreaId,
                        AreaName = line.Area?.AreaName ?? "",
                        CustomerName = line.Area?.CustomerName ?? "",
                        ProgramId = programId,
                        ProgramDescription = programDescription,
                        ProducedPieces = 0
                    });
                    continue;
                }

                foreach (var programGroup in lineProduction.GroupBy(item => new
                {
                    item.ProgramId,
                    ProgramDescription = string.IsNullOrWhiteSpace(item.ProgramDescription)
                        ? item.ProgramId > 0 ? $"PROGRAMA {item.ProgramId}" : "SIN PROGRAMA"
                        : item.ProgramDescription!.Trim()
                }))
                {
                    var programId = programGroup.Key.ProgramId;
                    var programDescription = programGroup.Key.ProgramDescription;
                    var hasKnownProgram = programId > 0 ||
                        programDescription.Replace("_", " ").Trim().ToUpperInvariant() != "SIN PROGRAMA";

                    if (!hasKnownProgram &&
                        line.AreaId.HasValue &&
                        latestProgramsByArea.TryGetValue(line.AreaId.Value, out var areaProgram) &&
                        areaProgram != null)
                    {
                        programId = areaProgram.ProgramId;
                        programDescription = string.IsNullOrWhiteSpace(areaProgram.ProgramDescription)
                            ? programId > 0 ? $"PROGRAMA {programId}" : "SIN PROGRAMA"
                            : areaProgram.ProgramDescription.Trim();
                    }

                    stats.Add(new ProductionLineStatsRecord
                    {
                        ProductionLinesId = line.ProductionLinesId,
                        LineNumber = line.LineNumber,
                        LineName = line.LineName ?? "",
                        DailyGoal = line.DailyGoal,
                        AreaId = line.AreaId,
                        AreaName = line.Area?.AreaName ?? "",
                        CustomerName = line.Area?.CustomerName ?? "",
                        ProgramId = programId,
                        ProgramDescription = programDescription,
                        ProducedPieces = programGroup.Sum(item => item.ProducedPieces)
                    });
                }
            }

            var lineSchedules = activeLines.ToDictionary(
                line => line.ProductionLinesId,
                ToOperatorWorkSchedule);

            var groups = stats
                .GroupBy(item => new
                {
                    item.AreaId,
                    item.AreaName,
                    item.CustomerName,
                    item.ProgramId,
                    item.ProgramDescription
                })
                .OrderBy(group => group.Key.CustomerName)
                .ThenBy(group => group.Key.AreaName)
                .ThenBy(group => group.Key.ProgramDescription)
                .Select(group => new
                {
                    areaId = group.Key.AreaId,
                    areaName = group.Key.AreaName,
                    customerName = group.Key.CustomerName,
                    programId = group.Key.ProgramId,
                    programDescription = group.Key.ProgramDescription,
                    lines = group
                        .GroupBy(item => new
                        {
                            item.ProductionLinesId,
                            item.LineNumber,
                            item.LineName,
                            item.DailyGoal
                        })
                        .OrderBy(lineGroup => lineGroup.Key.LineNumber)
                        .Select(lineGroup =>
                        {
                            var producedPieces = lineGroup.Sum(item => item.ProducedPieces);
                            var schedule = lineSchedules.TryGetValue(lineGroup.Key.ProductionLinesId, out var configuredSchedule)
                                ? configuredSchedule
                                : new OperatorWorkSchedule(DefaultOperatorWorkdayStart, DefaultOperatorWorkdayEnd, lineGroup.Key.DailyGoal, Array.Empty<OperatorBreak>());

                            return new
                            {
                                productionLinesId = lineGroup.Key.ProductionLinesId,
                                lineNumber = lineGroup.Key.LineNumber,
                                lineName = lineGroup.Key.LineName,
                                goal = lineGroup.Key.DailyGoal,
                                producedPieces,
                                percentage = GetRealTimePercentage(producedPieces, lineGroup.Key.DailyGoal, DateTime.Today, schedule, now),
                                finalPercentage = lineGroup.Key.DailyGoal > 0
                                    ? producedPieces * 100.0 / lineGroup.Key.DailyGoal
                                    : 0,
                                expectedPiecesNow = GetExpectedPiecesNow(lineGroup.Key.DailyGoal, DateTime.Today, schedule, now)
                            };
                        })
                        .ToList()
                })
                .ToList();

            return Ok(groups);
        }

        private static OperatorWorkSchedule GetWorkSchedule(
            int? areaId,
            int? productionLinesId,
            IReadOnlyDictionary<int, OperatorWorkSchedule> areaSchedules,
            IReadOnlyDictionary<int, OperatorWorkSchedule> lineSchedules)
        {
            if (productionLinesId.HasValue && lineSchedules.TryGetValue(productionLinesId.Value, out var lineSchedule))
            {
                return lineSchedule;
            }

            if (areaId.HasValue && areaSchedules.TryGetValue(areaId.Value, out var areaSchedule))
            {
                return areaSchedule;
            }

            return new OperatorWorkSchedule(DefaultOperatorWorkdayStart, DefaultOperatorWorkdayEnd, 0, Array.Empty<OperatorBreak>());
        }

        private static double GetWorkdayProgress(DateTime today, OperatorWorkSchedule workSchedule, DateTime now)
        {
            var workdayStart = today.Add(workSchedule.StartTime.ToTimeSpan());
            var workdayEnd = today.Add(workSchedule.EndTime.ToTimeSpan());

            var totalWorkMinutes = GetWorkingMinutes(workdayStart, workdayEnd, workSchedule.Breaks);
            if (totalWorkMinutes <= 0)
            {
                return 0;
            }

            var elapsedEnd = now < workdayStart
                ? workdayStart
                : now > workdayEnd
                    ? workdayEnd
                    : now;

            var elapsedWorkMinutes = GetWorkingMinutes(workdayStart, elapsedEnd, workSchedule.Breaks);
            return Math.Clamp(elapsedWorkMinutes / totalWorkMinutes, 0, 1);
        }

        private static double GetExpectedPiecesNow(int goal, DateTime today, OperatorWorkSchedule workSchedule, DateTime now)
        {
            return Math.Round(goal * GetLineGoalProgress(today, workSchedule, now), 1);
        }

        private static double GetRealTimePercentage(int scanCount, int goal, DateTime today, OperatorWorkSchedule workSchedule, DateTime now)
        {
            var expectedPiecesNow = GetExpectedPiecesNow(goal, today, workSchedule, now);

            if (expectedPiecesNow <= 0)
            {
                return scanCount > 0 ? 100 : 0;
            }

            return scanCount * 100.0 / expectedPiecesNow;
        }

        private static OperatorWorkSchedule ToOperatorWorkSchedule(ProductionLine line)
        {
            var breaks = line.Breaks
                .Where(lineBreak => lineBreak.BreakStart.HasValue && lineBreak.BreakEnd.HasValue)
                .Select(lineBreak => new OperatorBreak(lineBreak.BreakStart!.Value, lineBreak.BreakEnd!.Value))
                .ToArray();

            return new OperatorWorkSchedule(
                line.ShiftNumberNavigation.StartTime ?? DefaultOperatorWorkdayStart,
                line.ShiftNumberNavigation.EndTime ?? DefaultOperatorWorkdayEnd,
                line.DailyGoal,
                breaks);
        }

        private static double GetLineGoalProgress(DateTime today, OperatorWorkSchedule workSchedule, DateTime now)
        {
            if (workSchedule.DailyGoal <= 0)
            {
                return GetWorkdayProgress(today, workSchedule, now);
            }

            var estimatedLineGoalPieces = GetEstimatedLineGoalPieces(today, workSchedule, now);
            return Math.Clamp(estimatedLineGoalPieces / workSchedule.DailyGoal, 0, 1);
        }

        private static double GetEstimatedLineGoalPieces(DateTime today, OperatorWorkSchedule workSchedule, DateTime now)
        {
            var lineProgress = GetWorkdayProgress(today, workSchedule, now);
            return Math.Round(workSchedule.DailyGoal * lineProgress, MidpointRounding.AwayFromZero);
        }

        private static double GetWorkingMinutes(DateTime rangeStart, DateTime rangeEnd, IReadOnlyCollection<OperatorBreak> breaks)
        {
            if (rangeEnd <= rangeStart)
            {
                return 0;
            }

            var minutes = (rangeEnd - rangeStart).TotalMinutes;

            foreach (var lineBreak in breaks)
            {
                var breakStart = rangeStart.Date.Add(lineBreak.StartTime.ToTimeSpan());
                var breakEnd = rangeStart.Date.Add(lineBreak.EndTime.ToTimeSpan());

                var overlapStart = breakStart > rangeStart ? breakStart : rangeStart;
                var overlapEnd = breakEnd < rangeEnd ? breakEnd : rangeEnd;

                if (overlapEnd > overlapStart)
                {
                    minutes -= (overlapEnd - overlapStart).TotalMinutes;
                }
            }

            return Math.Max(0, minutes);
        }

        private sealed class OperatorWorkSchedule
        {
            public OperatorWorkSchedule(TimeOnly startTime, TimeOnly endTime, int dailyGoal, IReadOnlyCollection<OperatorBreak> breaks)
            {
                StartTime = startTime;
                EndTime = endTime;
                DailyGoal = dailyGoal;
                Breaks = breaks;
            }

            public TimeOnly StartTime { get; }

            public TimeOnly EndTime { get; }

            public int DailyGoal { get; }

            public IReadOnlyCollection<OperatorBreak> Breaks { get; }
        }

        private sealed class OperatorBreak
        {
            public OperatorBreak(TimeOnly startTime, TimeOnly endTime)
            {
                StartTime = startTime;
                EndTime = endTime;
            }

            public TimeOnly StartTime { get; }

            public TimeOnly EndTime { get; }
        }

        [HttpDelete("operator/{employeeNumber}")]
        public async Task<IActionResult> DeleteOperator(int employeeNumber)
        {
            var op = await _context.ProductionOperators
                .FirstOrDefaultAsync(o => o.EmployeeNumber == employeeNumber);

            if (op == null)
                return NotFound(new { message = "Operador no encontrado" });

            var operation = op.Operation;
            var operatorDetails = DescribeOperator(op);
            op.Active = false;
            await _context.SaveChangesAsync();

            await _auditService.LogProductionOperatorAction(
                "DEACTIVATE",
                op.OperatorId,
                $"Operador desactivado. Antes: {operatorDetails}");

            await NotifyOperatorStatsUpdatedAsync(operation);

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

            if (request.ProductionLinesId.HasValue)
            {
                var line = await _context.ProductionLines
                    .AsNoTracking()
                    .FirstOrDefaultAsync(line =>
                        line.ProductionLinesId == request.ProductionLinesId.Value &&
                        line.IsActive);

                if (line == null)
                    return BadRequest(new { message = "La linea seleccionada no existe o esta inactiva" });

                if (request.AreaId.HasValue && line.AreaId != request.AreaId.Value)
                    return BadRequest(new { message = "La linea seleccionada no pertenece al area indicada" });

                request.AreaId ??= line.AreaId;
            }

            var existingOperator = await _context.ProductionOperators
                .FirstOrDefaultAsync(o => o.EmployeeNumber == request.EmployeeNumber);

            if (existingOperator != null)
            {
                var previousDetails = DescribeOperator(existingOperator);

                existingOperator.NameOperator = request.NameOperator.Trim();
                existingOperator.LastnameOperator = request.LastnameOperator.Trim();
                existingOperator.AreaId = request.AreaId;
                existingOperator.ProductionLinesId = request.ProductionLinesId;
                existingOperator.Operation = request.Operation?.Trim();
                existingOperator.Goal = request.Goal;
                existingOperator.Active = request.Active;

                await _context.SaveChangesAsync();

                await _auditService.LogProductionOperatorAction(
                    "UPDATE",
                    existingOperator.OperatorId,
                    $"Operador actualizado. Antes: {previousDetails}. Despues: {DescribeOperator(existingOperator)}");
            }
            else
            {
                var newOperator = new Models.ProductionOperator
                {
                    EmployeeNumber = request.EmployeeNumber,
                    NameOperator = request.NameOperator.Trim(),
                    LastnameOperator = request.LastnameOperator.Trim(),
                    AreaId = request.AreaId,
                    ProductionLinesId = request.ProductionLinesId,
                    Operation = request.Operation?.Trim(),
                    Goal = request.Goal,
                    Active = request.Active
                };
                _context.ProductionOperators.Add(newOperator);

                await _context.SaveChangesAsync();

                await _auditService.LogProductionOperatorAction(
                    "CREATE",
                    newOperator.OperatorId,
                    $"Operador creado. Datos: {DescribeOperator(newOperator)}");
            }

            await NotifyOperatorStatsUpdatedAsync(request.Operation);
            return Ok(new { success = true, message = "Operador guardado correctamente" });
        }

        [HttpGet("operators")]
        public async Task<IActionResult> GetOperators([FromQuery] string? term = null, [FromQuery] int? areaId = null, [FromQuery] int? productionLinesId = null)
        {
            var query = _context.ProductionOperators
                .AsNoTracking()
                .Where(operatorItem => operatorItem.Active != false);

            if (areaId.HasValue)
            {
                query = query.Where(operatorItem => operatorItem.AreaId == areaId.Value);
            }

            if (productionLinesId.HasValue)
            {
                query = query.Where(operatorItem => operatorItem.ProductionLinesId == productionLinesId.Value);
            }

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
                int? areaId = null;

                if (int.TryParse(Request.Form["areaId"].FirstOrDefault(), out var parsedAreaId) &&
                    parsedAreaId > 0)
                {
                    areaId = parsedAreaId;
                }

                int? productionLinesId = null;

                if (int.TryParse(Request.Form["productionLinesId"].FirstOrDefault(), out var parsedProductionLinesId) &&
                    parsedProductionLinesId > 0)
                {
                    productionLinesId = parsedProductionLinesId;
                }

                var (startDateFilter, endDateFilter) =
                    ParseDateRange(startDateRaw, endDateRaw);

                var query = await BuildScansAsync(
                    operationFilter,
                    employeeFilter,
                    startDateFilter,
                    endDateFilter,
                    startTimeFilter,
                    endTimeFilter,
                    areaId,
                    productionLinesId);

                var orderedScans = (orderColumn, orderDirection) switch
                {
                    ("0", "asc") => query.OrderBy(scanItem => scanItem.ScannedAt),
                    ("0", "desc") => query.OrderByDescending(scanItem => scanItem.ScannedAt),

                    ("1", "asc") => query.OrderBy(scanItem => scanItem.ScannedAt),
                    ("1", "desc") => query.OrderByDescending(scanItem => scanItem.ScannedAt),

                    ("2", "asc") => query.OrderBy(scanItem => scanItem.ReferenceNumber),
                    ("2", "desc") => query.OrderByDescending(scanItem => scanItem.ReferenceNumber),

                    ("3", "asc") => query.OrderBy(scanItem => scanItem.FullName),
                    ("3", "desc") => query.OrderByDescending(scanItem => scanItem.FullName),

                    ("4", "asc") => query.OrderBy(scanItem => scanItem.Operation),
                    ("4", "desc") => query.OrderByDescending(scanItem => scanItem.Operation),

                    ("5", "asc") => query.OrderBy(scanItem => scanItem.Code),
                    ("5", "desc") => query.OrderByDescending(scanItem => scanItem.Code),

                    _ => query.OrderByDescending(scanItem => scanItem.ScannedAt)
                };

                var totalRecords = query.Count;

                var data = orderedScans
                    .Skip(start)
                    .Take(length)
                    .Select(scanItem => new
                    {
                        id = scanItem.Id,
                        scannedAt = scanItem.ScannedAt,
                        employeeNumber = scanItem.ReferenceNumber,
                        fullName = scanItem.FullName,
                        operation = scanItem.Operation,
                        code = scanItem.Code,
                        source = scanItem.Source,
                        canEdit = scanItem.CanEdit
                    })
                    .ToList();

                return Ok(new
                {
                    draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = totalRecords,
                    data = data.Select(scanItem => new
                    {
                        id = scanItem.id,
                        scannedAt = scanItem.scannedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                        scanDate = scanItem.scannedAt.ToString("yyyy-MM-dd"),
                        scanTime = scanItem.scannedAt.ToString("HH:mm:ss"),
                        scanItem.employeeNumber,
                        scanItem.fullName,
                        scanItem.operation,
                        scanItem.code,
                        scanItem.source,
                        scanItem.canEdit
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

        [HttpPost("audit-logs")]
        public async Task<IActionResult> GetAuditLogs()
        {
            try
            {
                int.TryParse(Request.Form["draw"], out var draw);
                int.TryParse(Request.Form["start"], out var start);
                int.TryParse(Request.Form["length"], out var length);

                var orderColumn = Request.Form["order[0][column]"].FirstOrDefault() ?? "0";
                var orderDirection = Request.Form["order[0][dir]"].FirstOrDefault() ?? "desc";
                var userFilter = Request.Form["userFilter"].FirstOrDefault();
                var employeeFilter = Request.Form["employeeFilter"].FirstOrDefault();
                var codeFilter = Request.Form["codeFilter"].FirstOrDefault();
                var actionFilter = Request.Form["actionFilter"].FirstOrDefault();
                var detailFilter = Request.Form["detailFilter"].FirstOrDefault();
                var startDateRaw = Request.Form["startDateFilter"].FirstOrDefault();
                var endDateRaw = Request.Form["endDateFilter"].FirstOrDefault();
                var (startDateFilter, endDateFilter) = ParseDateRange(startDateRaw, endDateRaw);
                var endExclusive = endDateFilter.Date.AddDays(1);

                var query = _context.AuditLogs
                    .AsNoTracking()
                    .Where(log =>
                        (log.EntityName == "ProductionOperator" ||
                            log.EntityName == "ProductionOperatorsScan" ||
                            log.EntityName == "ProductionOperatorsScanAttempt") &&
                        log.Timestamp >= startDateFilter.Date &&
                        log.Timestamp < endExclusive);

                if (!string.IsNullOrWhiteSpace(userFilter))
                {
                    var normalizedUserFilter = userFilter.Trim();

                    query = query.Where(log =>
                        EF.Functions.Like(log.EmployeeNumber ?? "", $"%{normalizedUserFilter}%"));
                }

                if (!string.IsNullOrWhiteSpace(employeeFilter))
                {
                    var normalizedEmployeeFilter = employeeFilter.Trim();

                    query = query.Where(log =>
                        EF.Functions.Like(log.Details ?? "", $"%Empleado: {normalizedEmployeeFilter}%") ||
                        EF.Functions.Like(log.Details ?? "", $"%Empleado capturado: {normalizedEmployeeFilter}%") ||
                        EF.Functions.Like(log.Details ?? "", $"%Empleado: %{normalizedEmployeeFilter}%") ||
                        EF.Functions.Like(log.Details ?? "", $"%Empleado capturado: %{normalizedEmployeeFilter}%"));
                }

                if (!string.IsNullOrWhiteSpace(codeFilter))
                {
                    var normalizedCodeFilter = codeFilter.Trim();

                    query = query.Where(log =>
                        EF.Functions.Like(log.Details ?? "", $"%Codigo: {normalizedCodeFilter}%") ||
                        EF.Functions.Like(log.Details ?? "", $"%Codigo base: {normalizedCodeFilter}%") ||
                        EF.Functions.Like(log.Details ?? "", $"%Codigo: %{normalizedCodeFilter}%") ||
                        EF.Functions.Like(log.Details ?? "", $"%Codigo base: %{normalizedCodeFilter}%"));
                }

                if (!string.IsNullOrWhiteSpace(actionFilter))
                {
                    var normalizedActionFilter = actionFilter.Trim();
                    query = query.Where(log => log.ActionType == normalizedActionFilter);
                }

                if (!string.IsNullOrWhiteSpace(detailFilter))
                {
                    var normalizedDetailFilter = detailFilter.Trim();
                    query = query.Where(log =>
                        EF.Functions.Like(log.Details ?? "", $"%{normalizedDetailFilter}%") ||
                        EF.Functions.Like(log.EntityName ?? "", $"%{normalizedDetailFilter}%") ||
                        EF.Functions.Like(log.ActionType ?? "", $"%{normalizedDetailFilter}%"));
                }

                var orderedQuery = (orderColumn, orderDirection) switch
                {
                    ("0", "asc") => query.OrderBy(log => log.Timestamp),
                    ("0", "desc") => query.OrderByDescending(log => log.Timestamp),
                    ("1", "asc") => query.OrderBy(log => log.Timestamp),
                    ("1", "desc") => query.OrderByDescending(log => log.Timestamp),
                    ("2", "asc") => query.OrderBy(log => log.EmployeeNumber),
                    ("2", "desc") => query.OrderByDescending(log => log.EmployeeNumber),
                    ("3", "asc") => query.OrderBy(log => log.Details),
                    ("3", "desc") => query.OrderByDescending(log => log.Details),
                    ("4", "asc") => query.OrderBy(log => log.Details),
                    ("4", "desc") => query.OrderByDescending(log => log.Details),
                    ("5", "asc") => query.OrderBy(log => log.ActionType),
                    ("5", "desc") => query.OrderByDescending(log => log.ActionType),
                    ("6", "asc") => query.OrderBy(log => log.EntityName),
                    ("6", "desc") => query.OrderByDescending(log => log.EntityName),
                    ("7", "asc") => query.OrderBy(log => log.EntityId),
                    ("7", "desc") => query.OrderByDescending(log => log.EntityId),
                    _ => query.OrderByDescending(log => log.Timestamp)
                };

                var totalRecords = await query.CountAsync();

                var data = await orderedQuery
                    .Skip(start)
                    .Take(length)
                    .Select(log => new
                    {
                        log.AuditLogId,
                        log.EmployeeNumber,
                        log.ActionType,
                        log.EntityName,
                        log.EntityId,
                        log.Timestamp,
                        log.Details
                    })
                    .ToListAsync();

                return Ok(new
                {
                    draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = totalRecords,
                    data = data.Select(log => new
                    {
                        id = log.AuditLogId,
                        logDate = log.Timestamp?.ToString("yyyy-MM-dd") ?? "",
                        logTime = log.Timestamp?.ToString("HH:mm:ss") ?? "",
                        employeeNumber = log.EmployeeNumber ?? "",
                        affectedEmployee = ExtractAuditDetailValue(log.Details, "Empleado", "Empleado capturado"),
                        pieceCode = ExtractAuditDetailValue(log.Details, "Codigo", "Codigo base"),
                        actionType = log.ActionType ?? "",
                        entityName = log.EntityName ?? "",
                        entityId = log.EntityId?.ToString() ?? "",
                        details = log.Details ?? ""
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

                var query = await BuildScansAsync(
                    request.OperationFilter,
                    request.EmployeeFilter,
                    startDate,
                    endDate,
                    request.StartTimeFilter,
                    request.EndTimeFilter,
                    request.AreaId,
                    request.ProductionLinesId);

                var records = query
                    .OrderByDescending(scanItem => scanItem.ScannedAt)
                    .Select(scanItem => new
                    {
                        scannedAt = scanItem.ScannedAt,
                        employeeNumber = scanItem.ReferenceNumber,
                        fullName = scanItem.FullName,
                        operation = scanItem.Operation,
                        code = scanItem.Code
                    })
                    .ToList();

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Escaneos");

                worksheet.Cell("A1").Value = "Reporte de Escaneos de Operadores y Lineas";
                worksheet.Cell("A1").Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Font.FontSize = 18;

                worksheet.Cell("A3").Value = "Operacion";
                worksheet.Cell("B3").Value = string.IsNullOrWhiteSpace(request.OperationFilter) ? "Todas" : request.OperationFilter;
                worksheet.Cell("D3").Value = "Empleado";
                worksheet.Cell("E3").Value = string.IsNullOrWhiteSpace(request.EmployeeFilter) ? "Todos" : request.EmployeeFilter;
                worksheet.Cell("G3").Value = "Rango";
                worksheet.Cell("H3").Value = $"{startDate:yyyy-MM-dd} a {endDate:yyyy-MM-dd}";

                worksheet.Cell("A5").Value = "Grafica de escaneos por operador / linea (agrupado por operacion)";
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
                var rawOpData = query
                    .GroupBy(scanItem => new
                    {
                        scanItem.Source,
                        scanItem.ReferenceNumber,
                        scanItem.FullName,
                        scanItem.Operation
                    })
                    .Select(group => new
                    {
                        EmployeeNumber = group.Key.ReferenceNumber,
                        group.Key.FullName,
                        group.Key.Operation,
                        Total = group.Count()
                    })
                    .ToList();

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
                    var hourlyData = query
                        .GroupBy(s => s.ScannedAt.Hour)
                        .Select(g => new { period = g.Key, total = g.Count() })
                        .OrderBy(g => g.period)
                    .ToList();
                    c3Labels = hourlyData.Select(d => $"{d.period:D2}:00").ToArray();
                    c3Values = hourlyData.Select(d => d.total).ToArray();
                }
                else
                {
                    var dailyData = query
                        .GroupBy(s => s.ScannedAt.Date)
                        .Select(g => new { period = g.Key, total = g.Count() })
                        .OrderBy(g => g.period)
                        .ToList();
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
                    worksheet.Cell(currentRow, 1).Value = record.scannedAt.ToString("yyyy-MM-dd");
                    worksheet.Cell(currentRow, 2).Value = record.scannedAt.ToString("HH:mm:ss");
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
                .Include(s => s.Operator)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (scan == null)
                return NotFound(new { message = "Escaneo no encontrado" });

            var previousOperation = scan.Operator.Operation;
            var previousDetails = DescribeScan(scan);
            Models.ProductionOperator? newOperator = null;

            if (!string.IsNullOrWhiteSpace(request.Code))
                scan.Code = request.Code.Trim();

            if (request.ScannedAt.HasValue)
                scan.ScannedAt = request.ScannedAt.Value;

            if (request.EmployeeNumber.HasValue)
            {
                var operatorQuery = _context.ProductionOperators
                    .Where(o => o.EmployeeNumber == request.EmployeeNumber.Value);

                if (request.AreaId.HasValue)
                {
                    operatorQuery = operatorQuery.Where(o => o.AreaId == request.AreaId.Value);
                }

                if (request.ProductionLinesId.HasValue)
                {
                    operatorQuery = operatorQuery.Where(o => o.ProductionLinesId == request.ProductionLinesId.Value);
                }

                var operatorEntity = await operatorQuery.FirstOrDefaultAsync();

                if (operatorEntity == null)
                    return NotFound(new { message = "Empleado no encontrado" });

                scan.OperatorId = operatorEntity.OperatorId;
                newOperator = operatorEntity;
            }

            await _context.SaveChangesAsync();

            await _auditService.LogProductionOperatorScanAction(
                "UPDATE",
                scan.Id,
                $"Escaneo actualizado. Antes: {previousDetails}. Despues: Codigo: {scan.Code}, Fecha: {scan.ScannedAt?.ToString("yyyy-MM-dd HH:mm:ss")}, Empleado: {(newOperator?.EmployeeNumber ?? scan.Operator.EmployeeNumber)}, Operacion: {(newOperator?.Operation ?? scan.Operator.Operation)}");

            await NotifyOperatorStatsUpdatedAsync(previousOperation);

            if (request.EmployeeNumber.HasValue)
            {
                var currentOperation = await _context.ProductionOperatorsScans
                    .AsNoTracking()
                    .Where(s => s.Id == id)
                    .Select(s => s.Operator.Operation)
                    .FirstOrDefaultAsync();

                if (!string.Equals(previousOperation, currentOperation, StringComparison.OrdinalIgnoreCase))
                {
                    await NotifyOperatorStatsUpdatedAsync(currentOperation);
                }
            }

            return Ok(new { success = true, message = "Escaneo actualizado correctamente" });
        }

        [HttpDelete("scan/{id:long}")]
        public async Task<IActionResult> DeleteScan(long id, [FromQuery] string? source = "OPERATOR")
        {
            if (string.Equals(source, "LINE", StringComparison.OrdinalIgnoreCase))
            {
                var lineScan = await _context.ScannerProductions
                    .FirstOrDefaultAsync(scanItem => scanItem.ScannerProductionId == id);

                if (lineScan == null)
                    return NotFound(new { message = "Escaneo de linea no encontrado" });

                var productionLine = await _context.ProductionLines
                    .AsNoTracking()
                    .FirstOrDefaultAsync(line => line.ProductionLinesId == lineScan.LineId);

                var scanDate = DateOnly.FromDateTime(lineScan.ScannerProductionDateTime);
                var startHour = new TimeOnly(lineScan.ScannerProductionDateTime.Hour, 0, 0);
                var productionData = await _context.ProductionData
                    .Where(item =>
                        item.ProductionLinesId == lineScan.LineId &&
                        item.ProductionDate == scanDate &&
                        item.StartHour == startHour &&
                        item.ProducedPieces > 0)
                    .OrderByDescending(item => item.ProductionId)
                    .FirstOrDefaultAsync();

                if (productionData != null)
                {
                    productionData.ProducedPieces = Math.Max(
                        (productionData.ProducedPieces ?? 0) - 1,
                        0);
                }

                var scannerValue = lineScan.ScannerValue;
                _context.ScannerProductions.Remove(lineScan);
                await _context.SaveChangesAsync();

                await _auditService.LogProductionLineAction(
                    "DELETE_LINE_SCAN",
                    lineScan.LineId,
                    $"Escaneo de linea eliminado. ScannerProductionId: {id}, Codigo: {scannerValue}, Fecha: {lineScan.ScannerProductionDateTime:yyyy-MM-dd HH:mm:ss}, ProductionId actualizado: {productionData?.ProductionId}");

                await _hubContext.Clients.All.SendAsync("ProductionDataUpdated", new
                {
                    lineId = lineScan.LineId,
                    areaId = productionLine?.AreaId,
                    updatedAt = DateTime.Now
                });

                return Ok(new
                {
                    success = true,
                    message = "Escaneo de linea eliminado correctamente",
                    scanId = id,
                    producedPieces = productionData?.ProducedPieces ?? 0
                });
            }

            if (id > int.MaxValue)
                return BadRequest(new { message = "Identificador de escaneo invalido" });

            var operatorScanId = (int)id;
            var scan = await _context.ProductionOperatorsScans
                .Include(scanItem => scanItem.Operator)
                .FirstOrDefaultAsync(scanItem => scanItem.Id == operatorScanId);

            if (scan == null)
                return NotFound(new { message = "Escaneo no encontrado" });

            var operation = scan.Operator.Operation;
            var scanDetails = DescribeScan(scan);
            var employeeNumber = scan.Operator.EmployeeNumber;

            _context.ProductionOperatorsScans.Remove(scan);
            await _context.SaveChangesAsync();

            await _auditService.LogProductionOperatorScanAction(
                "DELETE",
                operatorScanId,
                $"Escaneo eliminado. Empleado: {employeeNumber}, Operacion: {operation}, Datos: {scanDetails}");

            await NotifyOperatorStatsUpdatedAsync(operation);

            return Ok(new
            {
                success = true,
                message = "Escaneo eliminado correctamente",
                scanId = operatorScanId
            });
        }

        [HttpPost("manual-pieces")]
        public async Task<IActionResult> AddManualPieces([FromBody] AddManualPiecesRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Datos invalidos" });

            if (request.EmployeeNumber <= 0)
                return BadRequest(new { message = "Empleado invalido" });

            if (request.Quantity < 1 || request.Quantity > 500)
                return BadRequest(new { message = "La cantidad debe estar entre 1 y 500" });

            if (!request.ScannedAt.HasValue)
                return BadRequest(new { message = "La fecha es requerida" });

            var operatorQuery = _context.ProductionOperators
                .Where(o =>
                    o.EmployeeNumber == request.EmployeeNumber &&
                    o.Active != false);

            if (request.AreaId.HasValue)
            {
                operatorQuery = operatorQuery.Where(o => o.AreaId == request.AreaId.Value);
            }

            if (request.ProductionLinesId.HasValue)
            {
                operatorQuery = operatorQuery.Where(o => o.ProductionLinesId == request.ProductionLinesId.Value);
            }

            var operatorEntity = await operatorQuery.FirstOrDefaultAsync();

            if (operatorEntity == null)
                return NotFound(new { message = "Empleado no encontrado o inactivo" });

            var scannedAt = request.ScannedAt.Value;
            var codeBase = string.IsNullOrWhiteSpace(request.Code)
                ? $"MANUAL-{request.EmployeeNumber}-{scannedAt:yyyyMMddHHmmss}"
                : request.Code.Trim();

            if (codeBase.Length > 40)
                return BadRequest(new { message = "El codigo base no puede exceder 40 caracteres" });

            var scans = Enumerable.Range(1, request.Quantity)
                .Select(index => new Models.ProductionOperatorsScan
                {
                    OperatorId = operatorEntity.OperatorId,
                    Code = request.Quantity == 1
                        ? codeBase
                        : $"{codeBase}-{index:D3}",
                    ScannedAt = scannedAt.AddSeconds(index - 1)
                })
                .ToList();

            _context.ProductionOperatorsScans.AddRange(scans);
            await _context.SaveChangesAsync();

            await _auditService.LogProductionOperatorAction(
                "ADD_MANUAL_PIECES",
                operatorEntity.OperatorId,
                $"Piezas manuales agregadas. Empleado: {operatorEntity.EmployeeNumber}, Operador: {((operatorEntity.NameOperator ?? "") + " " + (operatorEntity.LastnameOperator ?? "")).Trim()}, Operacion: {operatorEntity.Operation}, AreaId: {operatorEntity.AreaId}, LineaId: {operatorEntity.ProductionLinesId}, Cantidad: {scans.Count}, Codigo base: {codeBase}, Fecha inicial: {scannedAt:yyyy-MM-dd HH:mm:ss}, ScanIds: {string.Join(", ", scans.Select(scanItem => scanItem.Id))}");

            await NotifyOperatorStatsUpdatedAsync(operatorEntity.Operation);

            return Ok(new
            {
                success = true,
                message = "Piezas agregadas correctamente",
                quantity = scans.Count
            });
        }

        [HttpPost("manual-line-production")]
        public async Task<IActionResult> AddManualLineProduction([FromBody] AddManualLineProductionRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Datos invalidos" });

            if (request.ProductionLinesId <= 0)
                return BadRequest(new { message = "Seleccione una linea valida" });

            if (request.Quantity < 1 || request.Quantity > 5000)
                return BadRequest(new { message = "La cantidad debe estar entre 1 y 5000" });

            if (!request.ProducedAt.HasValue)
                return BadRequest(new { message = "La fecha y hora son requeridas" });

            var productionLine = await _context.ProductionLines
                .AsNoTracking()
                .FirstOrDefaultAsync(line =>
                    line.ProductionLinesId == request.ProductionLinesId &&
                    line.IsActive);

            if (productionLine == null)
                return NotFound(new { message = "La linea no existe o esta inactiva" });

            if (request.AreaId.HasValue && productionLine.AreaId != request.AreaId.Value)
                return BadRequest(new { message = "La linea no pertenece al area seleccionada" });

            await using var transaction = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable);

            var producedAt = request.ProducedAt.Value;
            var productionDate = DateOnly.FromDateTime(producedAt);
            var productionTime = TimeOnly.FromDateTime(producedAt);
            var startHour = new TimeOnly(producedAt.Hour, 0, 0);
            var endHour = producedAt.Hour == 23
                ? TimeOnly.MaxValue
                : new TimeOnly(producedAt.Hour + 1, 0, 0);

            var programContext = await _context.ProductionData
                .AsNoTracking()
                .Where(item =>
                    item.ProductionLinesId == productionLine.ProductionLinesId &&
                    item.ProductionDate == productionDate &&
                    item.StartHour <= productionTime &&
                    (item.EndHour > productionTime ||
                     (startHour.Hour == 23 && item.StartHour == startHour)))
                .OrderByDescending(item => item.ProductionId)
                .Select(item => new { item.ProgramId, item.ProgramDescription })
                .FirstOrDefaultAsync();

            programContext ??= await _context.ProductionData
                .AsNoTracking()
                .Where(item => item.ProductionLinesId == productionLine.ProductionLinesId)
                .OrderByDescending(item => item.ProductionDate)
                .ThenByDescending(item => item.StartHour)
                .ThenByDescending(item => item.ProductionId)
                .Select(item => new { item.ProgramId, item.ProgramDescription })
                .FirstOrDefaultAsync();

            var programId = programContext?.ProgramId ?? 0;
            var programDescription = string.IsNullOrWhiteSpace(programContext?.ProgramDescription)
                ? programId > 0 ? $"PROGRAMA {programId}" : "SIN_PROGRAMA"
                : programContext.ProgramDescription;

            var productionData = await _context.ProductionData
                .FirstOrDefaultAsync(item =>
                    item.ProductionLinesId == productionLine.ProductionLinesId &&
                    item.ProductionDate == productionDate &&
                    item.ProgramId == programId &&
                    item.StartHour == startHour);

            if (productionData == null)
            {
                productionData = new ProductionDatum
                {
                    ProductionLinesId = productionLine.ProductionLinesId,
                    ProductionDate = productionDate,
                    StartHour = startHour,
                    EndHour = endHour,
                    ProducedPieces = request.Quantity,
                    RejectedPieces = 0,
                    ProgramId = programId,
                    ProgramDescription = programDescription
                };
                _context.ProductionData.Add(productionData);
            }
            else
            {
                productionData.ProducedPieces = (productionData.ProducedPieces ?? 0) + request.Quantity;
                if (string.IsNullOrWhiteSpace(productionData.ProgramDescription))
                {
                    productionData.ProgramDescription = programDescription;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _auditService.LogProductionLineAction(
                "ADD_MANUAL_PRODUCTION",
                productionLine.ProductionLinesId,
                $"Produccion manual agregada. Cantidad: {request.Quantity}, Fecha: {producedAt:yyyy-MM-dd HH:mm:ss}, ProgramaId: {programId}, Intervalo: {startHour:HH:mm}-{endHour:HH:mm}, ProductionId: {productionData.ProductionId}");

            await _hubContext.Clients.All.SendAsync("ProductionDataUpdated", new
            {
                lineId = productionLine.ProductionLinesId,
                areaId = productionLine.AreaId,
                quantity = request.Quantity,
                updatedAt = DateTime.Now
            });

            return Ok(new
            {
                success = true,
                message = $"Se agregaron {request.Quantity} piezas a la linea",
                productionId = productionData.ProductionId,
                producedPieces = productionData.ProducedPieces
            });
        }

        public class UpdateScanRequest
        {
            public string? Code { get; set; }
            public DateTime? ScannedAt { get; set; }
            public int? EmployeeNumber { get; set; }
            public int? AreaId { get; set; }
            public int? ProductionLinesId { get; set; }
        }

        public class AddManualPiecesRequest
        {
            public int EmployeeNumber { get; set; }
            public string? Code { get; set; }
            public int Quantity { get; set; }
            public DateTime? ScannedAt { get; set; }
            public int? AreaId { get; set; }
            public int? ProductionLinesId { get; set; }
        }

        public class AddManualLineProductionRequest
        {
            public int ProductionLinesId { get; set; }
            public int Quantity { get; set; }
            public DateTime? ProducedAt { get; set; }
            public int? AreaId { get; set; }
        }

        private Task NotifyOperatorStatsUpdatedAsync(string? operation)
        {
            return _hubContext.Clients.All.SendAsync("OperatorStatsUpdated", new
            {
                operation = string.IsNullOrWhiteSpace(operation) ? "Sin operacion" : operation,
                updatedAt = DateTime.Now
            });
        }

        public class DashboardFilters
        {
            public string? OperationFilter { get; set; }

            public string? EmployeeFilter { get; set; }

            public string? StartDateFilter { get; set; }

            public string? EndDateFilter { get; set; }

            public string? StartTimeFilter { get; set; }

            public string? EndTimeFilter { get; set; }

            public int? AreaId { get; set; }

            public int? ProductionLinesId { get; set; }
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
            public int? ProductionLinesId { get; set; }
            public string? Operation { get; set; }
            public int? Goal { get; set; }
            public bool Active { get; set; } = true;
        }

        public class SaveProductionLineRequest
        {
            public int? ProductionLinesId { get; set; }
            public int AreaId { get; set; }
            public int LineNumber { get; set; }
            public string? LineName { get; set; }
            public int DailyGoal { get; set; }
            public int PersonalQuantity { get; set; }
            public decimal? StandardTime { get; set; }
            public bool IsActive { get; set; } = true;
        }
    }
}

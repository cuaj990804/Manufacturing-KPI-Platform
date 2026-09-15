using GDIKPI.Data;
using GDIKPI.Hubs;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.RegularExpressions;

namespace GDIKPI.Controllers
{
    public class ProductionOperatorsController : Controller
    {
        private const int ImmediateDuplicateGraceSeconds = 10;

        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly AuditService _auditService;
        private readonly IConfiguration _configuration;

        public ProductionOperatorsController(
            KpisContext context,
            PermissionService permissionService,
            IHubContext<DashboardHub> hubContext,
            AuditService auditService,
            IConfiguration configuration)
        {
            _context = context;
            _permissionService = permissionService;
            _hubContext = hubContext;
            _auditService = auditService;
            _configuration = configuration;
        }

        public async Task<ActionResult> Index(int? areaId = null)
        {
            if (areaId.HasValue)
            {
                var area = await _context.Areas
                    .AsNoTracking()
                    .Where(areaItem => areaItem.AreaId == areaId.Value)
                    .Select(areaItem => new
                    {
                        areaItem.AreaId,
                        areaItem.AreaName,
                        areaItem.CustomerName
                    })
                    .FirstOrDefaultAsync();

                if (area is null)
                {
                    ViewBag.ErrorMessage = "Area no encontrada.";
                    return View();
                }

                ViewBag.AreaId = area.AreaId;
                ViewBag.AreaName = area.AreaName;
                ViewBag.CustomerName = area.CustomerName;
            }

            ViewBag.ScannerValidation = new
            {
                ZF = new
                {
                    enabled = _configuration.GetValue<bool>("ScannerValidation:ZF:Enabled"),
                    allowedPrefixes = _configuration.GetSection("ScannerValidation:ZF:AllowedPrefixes").Get<string[]>() ?? Array.Empty<string>(),
                    allowedSuffixes = _configuration.GetSection("ScannerValidation:ZF:AllowedSuffixes").Get<string[]>() ?? Array.Empty<string>()
                }
            };

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ValidateEmployee(int employeeNumber, int? areaId = null)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var query = _context.ProductionOperators
                .AsNoTracking()
                .Where(operatorItem =>
                    operatorItem.EmployeeNumber == employeeNumber &&
                    operatorItem.Active == true);

            if (areaId.HasValue)
            {
                query = query.Where(operatorItem => operatorItem.AreaId == areaId.Value);
            }

            var productionOperator = await query
                .Select(operatorItem => new
                {
                    operatorItem.OperatorId,
                    operatorItem.EmployeeNumber,
                    FullName = (operatorItem.NameOperator ?? "") + " " + (operatorItem.LastnameOperator ?? ""),
                    operatorItem.Operation,
                    operatorItem.Goal,
                    operatorItem.AreaId,
                    CurrentQuantity = operatorItem.ProductionOperatorsScans.Count(scanItem =>
                        scanItem.ScannedAt >= today &&
                        scanItem.ScannedAt < tomorrow)
                })
                .FirstOrDefaultAsync();

            if (productionOperator is null)
            {
                return Json(new
                {
                    success = false,
                    message = areaId.HasValue
                        ? "Empleado invalido, inactivo o no pertenece a esta area."
                        : "Empleado invalido o inactivo."
                });
            }

            return Json(new
            {
                success = true,
                operatorData = new
                {
                    productionOperator.OperatorId,
                    productionOperator.EmployeeNumber,
                    productionOperator.FullName,
                    productionOperator.Operation,
                    productionOperator.Goal,
                    productionOperator.AreaId,
                    productionOperator.CurrentQuantity,
                    goalReached = productionOperator.Goal.HasValue &&
                        productionOperator.Goal.Value > 0 &&
                        productionOperator.CurrentQuantity >= productionOperator.Goal.Value
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentScans(int limit = 10, int? areaId = null)
        {
            limit = Math.Clamp(limit, 1, 5);
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var query = _context.ProductionOperatorsScans
                .AsNoTracking()
                .Where(scanItem =>
                    scanItem.ScannedAt >= today &&
                    scanItem.ScannedAt < tomorrow);

            if (areaId.HasValue)
            {
                query = query.Where(scanItem => scanItem.Operator.AreaId == areaId.Value);
            }

            var recentScans = await query
                .OrderByDescending(scanItem => scanItem.ScannedAt)
                .ThenByDescending(scanItem => scanItem.Id)
                .Take(limit)
                .Select(scanItem => new
                {
                    scanItem.Id,
                    scanItem.Code,
                    scanItem.ScannedAt,
                    scanItem.Operator.EmployeeNumber,
                    FullName = (scanItem.Operator.NameOperator ?? "") + " " + (scanItem.Operator.LastnameOperator ?? ""),
                    scanItem.Operator.Operation,
                    scanItem.Operator.AreaId,
                    Quantity = _context.ProductionOperatorsScans.Count(countItem =>
                        countItem.OperatorId == scanItem.OperatorId &&
                        countItem.ScannedAt >= today &&
                        countItem.ScannedAt < tomorrow &&
                        (countItem.ScannedAt < scanItem.ScannedAt ||
                            (countItem.ScannedAt == scanItem.ScannedAt && countItem.Id <= scanItem.Id)) &&
                        (!areaId.HasValue || countItem.Operator.AreaId == areaId.Value))
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                scans = recentScans
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetScansSummary(int? areaId = null)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var query = _context.ProductionOperatorsScans
                .AsNoTracking()
                .Where(scanItem =>
                    scanItem.ScannedAt >= today &&
                    scanItem.ScannedAt < tomorrow);

            if (areaId.HasValue)
            {
                query = query.Where(scanItem => scanItem.Operator.AreaId == areaId.Value);
            }

            var summary = await query
                .GroupBy(scanItem => new
                {
                    scanItem.Operator.EmployeeNumber,
                    NameOperator = scanItem.Operator.NameOperator ?? "",
                    LastnameOperator = scanItem.Operator.LastnameOperator ?? "",
                    Operation = scanItem.Operator.Operation ?? "",
                    scanItem.Operator.AreaId
                })
                .Select(group => new
                {
                    group.Key.EmployeeNumber,
                    FullName = ((group.Key.NameOperator ?? "") + " " + (group.Key.LastnameOperator ?? "")).Trim(),
                    group.Key.Operation,
                    group.Key.AreaId,
                    Quantity = group.Count(),
                    LastScannedAt = group.Max(scanItem => scanItem.ScannedAt)
                })
                .OrderByDescending(item => item.Quantity)
                .ThenBy(item => item.FullName)
                .ToListAsync();

            return Json(new
            {
                success = true,
                scans = summary
            });
        }

        [HttpPost]
        public async Task<IActionResult> SaveScan([FromBody] SaveProductionOperatorScanRequest request)
        {
            if (request is null)
            {
                await LogScanAttemptRejectedAsync(null, null, null, "Solicitud invalida.");
                return BadRequest(new { success = false, message = "Solicitud invalida." });
            }

            if (request.EmployeeNumber <= 0)
            {
                await LogScanAttemptRejectedAsync(request.EmployeeNumber, request.Code, request.AreaId, "Numero de empleado invalido.");
                return BadRequest(new { success = false, message = "Numero de empleado invalido." });
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                await LogScanAttemptRejectedAsync(request.EmployeeNumber, request.Code, request.AreaId, "El codigo es requerido.");
                return BadRequest(new { success = false, message = "El codigo es requerido." });
            }

            var code = request.Code.Trim();

            if (code.Length > 50)
            {
                await LogScanAttemptRejectedAsync(request.EmployeeNumber, code, request.AreaId, "El codigo no puede exceder 50 caracteres.");
                return BadRequest(new { success = false, message = "El codigo no puede exceder 50 caracteres." });
            }

            if (await IsEmployeeNumberCodeAsync(code, request.AreaId))
            {
                await LogScanAttemptRejectedAsync(request.EmployeeNumber, code, request.AreaId, "Numero de empleado capturado en campo volante.");
                return BadRequest(new { success = false, message = "Escanee el volante, no el numero de empleado." });
            }

            var productionOperator = await _context.ProductionOperators
                .AsNoTracking()
                .Where(operatorItem =>
                    operatorItem.EmployeeNumber == request.EmployeeNumber &&
                    operatorItem.Active == true &&
                    (!request.AreaId.HasValue || operatorItem.AreaId == request.AreaId.Value))
                .Select(operatorItem => new
                {
                    operatorItem.OperatorId,
                    operatorItem.EmployeeNumber,
                    operatorItem.NameOperator,
                    operatorItem.LastnameOperator,
                    Operation = operatorItem.Operation ?? "",
                    operatorItem.Goal,
                    operatorItem.AreaId,
                    AreaName = operatorItem.Area != null ? operatorItem.Area.AreaName : null,
                    CustomerName = operatorItem.Area != null ? operatorItem.Area.CustomerName : null
                })
                .FirstOrDefaultAsync();

            if (productionOperator is null)
            {
                await LogScanAttemptRejectedAsync(
                    request.EmployeeNumber,
                    code,
                    request.AreaId,
                    request.AreaId.HasValue
                        ? "Empleado invalido, inactivo o no pertenece a esta area."
                        : "Empleado invalido o inactivo.");

                return NotFound(new
                {
                    success = false,
                    message = request.AreaId.HasValue
                        ? "Empleado invalido, inactivo o no pertenece a esta area."
                        : "Empleado invalido o inactivo."
                });
            }

            // Codigos especiales que pueden repetirse (empiezan con "UNREADABLE").
            var isOverrideCode = code.StartsWith("UNREADABLE", StringComparison.OrdinalIgnoreCase);

            if (!isOverrideCode && !ValidateConfiguredCustomerScan(productionOperator.CustomerName, productionOperator.AreaName, code, out var validationError))
            {
                await LogScanAttemptRejectedAsync(
                    request.EmployeeNumber,
                    code,
                    request.AreaId,
                    validationError);

                return BadRequest(new
                {
                    success = false,
                    message = validationError
                });
            }

            var operation = productionOperator.Operation.Trim();
            var normalizedCode = NormalizeVolanteCode(code);

            var codeAlreadyScannedInOperation = false;
            ProductionOperatorsScan? scan = null;
            int operatorQuantity = 0;
            var goalReached = false;
            var auditDetails = string.Empty;

            await using (var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
            {
                var lockResult = await AcquireScanKeyLockAsync(normalizedCode, operation, productionOperator.AreaId);

                if (lockResult < 0)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(409, new
                    {
                        success = false,
                        message = "El codigo esta siendo procesado, intente nuevamente."
                    });
                }

                var operatorGoalLockResult = await AcquireScanKeyLockAsync(
                    $"OPERATOR-{productionOperator.OperatorId}",
                    "DAILY-GOAL",
                    productionOperator.AreaId);

                if (operatorGoalLockResult < 0)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(409, new
                    {
                        success = false,
                        message = "El operador esta siendo procesado, intente nuevamente."
                    });
                }

                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                operatorQuantity = await _context.ProductionOperatorsScans
                    .AsNoTracking()
                    .CountAsync(scanItem =>
                        scanItem.OperatorId == productionOperator.OperatorId &&
                        scanItem.ScannedAt >= today &&
                        scanItem.ScannedAt < tomorrow);

                if (productionOperator.Goal.HasValue &&
                    productionOperator.Goal.Value > 0 &&
                    operatorQuantity >= productionOperator.Goal.Value)
                {
                    goalReached = true;
                    await transaction.RollbackAsync();
                }

                if (!goalReached && !isOverrideCode)
                {
                    codeAlreadyScannedInOperation = await HasCodeAlreadyScannedInOperationAsync(
                        code,
                        normalizedCode,
                        operation,
                        productionOperator.AreaId);

                    if (codeAlreadyScannedInOperation)
                    {
                        await transaction.RollbackAsync();
                    }
                }

                if (!goalReached && !codeAlreadyScannedInOperation)
                {
                    scan = new ProductionOperatorsScan
                    {
                        OperatorId = productionOperator.OperatorId,
                        Code = code,
                        ScannedAt = DateTime.Now
                    };

                    _context.ProductionOperatorsScans.Add(scan);
                    await _context.SaveChangesAsync();

                    auditDetails = $"Escaneo registrado desde ProductionOperators. Empleado: {productionOperator.EmployeeNumber}, Operador: {((productionOperator.NameOperator ?? "") + " " + (productionOperator.LastnameOperator ?? "")).Trim()}, Operacion: {productionOperator.Operation}, AreaId: {productionOperator.AreaId}, Codigo: {scan.Code}, Longitud: {scan.Code?.Length ?? 0}, Huella: {normalizedCode}, Fecha: {scan.ScannedAt?.ToString("yyyy-MM-dd HH:mm:ss")}";

                    operatorQuantity = await _context.ProductionOperatorsScans
                        .AsNoTracking()
                        .CountAsync(scanItem =>
                            scanItem.OperatorId == productionOperator.OperatorId &&
                            scanItem.ScannedAt >= today &&
                            scanItem.ScannedAt < tomorrow &&
                            (!request.AreaId.HasValue || scanItem.Operator.AreaId == request.AreaId.Value));

                    await transaction.CommitAsync();
                }
            }

            if (goalReached)
            {
                var goalMessage = $"Meta diaria cumplida ({operatorQuantity}/{productionOperator.Goal}). No se permiten mas escaneos.";
                await LogScanAttemptRejectedAsync(
                    request.EmployeeNumber,
                    code,
                    request.AreaId,
                    goalMessage);

                return Conflict(new
                {
                    success = false,
                    goalReached = true,
                    currentQuantity = operatorQuantity,
                    goal = productionOperator.Goal,
                    message = goalMessage
                });
            }

            if (codeAlreadyScannedInOperation)
            {
                var recentDuplicateScan = await FindRecentDuplicateScanForSameOperatorAsync(
                    productionOperator.OperatorId,
                    normalizedCode);

                if (recentDuplicateScan is not null)
                {
                    return Json(new
                    {
                        success = true,
                        ignored = true,
                        message = "Escaneo duplicado inmediato ignorado.",
                        scanId = recentDuplicateScan.Id,
                        scannedAt = recentDuplicateScan.ScannedAt,
                        scan = (object?)null
                    });
                }

                await LogScanAttemptRejectedAsync(
                    request.EmployeeNumber,
                    code,
                    request.AreaId,
                    $"Codigo duplicado o equivalente para la misma operacion. Operacion: {operation}, AreaId: {productionOperator.AreaId}, Huella: {normalizedCode}");

                return Conflict(new
                {
                    success = false,
                    message = "Este codigo ya fue escaneado por un operador de la misma operacion."
                });
            }

            if (scan is null)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "No se pudo confirmar el escaneo."
                });
            }

            await _auditService.LogProductionOperatorScanAction(
                "CREATE",
                scan.Id,
                auditDetails);

            await NotifyOperatorStatsUpdatedAsync(productionOperator.Operation);

            return Json(new
            {
                success = true,
                message = "Escaneo guardado.",
                goal = productionOperator.Goal,
                currentQuantity = operatorQuantity,
                goalReached = productionOperator.Goal.HasValue &&
                    productionOperator.Goal.Value > 0 &&
                    operatorQuantity >= productionOperator.Goal.Value,
                scanId = scan.Id,
                scannedAt = scan.ScannedAt,
                scan = new
                {
                    scan.Id,
                    scan.Code,
                    scan.ScannedAt,
                    productionOperator.EmployeeNumber,
                    FullName = (productionOperator.NameOperator ?? "") + " " + (productionOperator.LastnameOperator ?? ""),
                    productionOperator.Operation,
                    productionOperator.AreaId,
                    Quantity = operatorQuantity
                }
            });
        }

        public class SaveProductionOperatorScanRequest
        {
            public int EmployeeNumber { get; set; }

            public string? Code { get; set; }

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

        private Task LogScanAttemptRejectedAsync(int? employeeNumber, string? code, int? areaId, string reason)
        {
            var auditEmployeeNumber = employeeNumber.GetValueOrDefault();

            return _auditService.LogProductionOperatorScanAttemptAction(
                "REJECTED",
                auditEmployeeNumber,
                $"Intento de escaneo rechazado. Empleado capturado: {employeeNumber?.ToString() ?? "N/A"}, AreaId: {areaId?.ToString() ?? "N/A"}, Codigo: {(string.IsNullOrWhiteSpace(code) ? "N/A" : code.Trim())}, Longitud: {(string.IsNullOrWhiteSpace(code) ? 0 : code.Trim().Length)}, Huella: {(string.IsNullOrWhiteSpace(code) ? "N/A" : NormalizeVolanteCode(code))}, Motivo: {reason}, Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        private async Task<bool> HasCodeAlreadyScannedInOperationAsync(
            string code,
            string normalizedCode,
            string operation,
            int? areaId)
        {
            var today = DateTime.Today;
            var startDate = today.AddDays(-30);

            var candidateCodes = await _context.ProductionOperatorsScans
                .AsNoTracking()
                .Where(scanItem =>
                    scanItem.ScannedAt >= startDate &&
                    scanItem.Operator.Operation == operation &&
                    scanItem.Operator.AreaId == areaId &&
                    scanItem.Code != null &&
                    (scanItem.Code == code ||
                        scanItem.Code.Contains("B") ||
                        scanItem.Code.Contains("MX")))
                .OrderByDescending(scanItem => scanItem.ScannedAt)
                .Select(scanItem => scanItem.Code!)
                .Take(1000)
                .ToListAsync();

            return candidateCodes.Any(candidateCode =>
                string.Equals(candidateCode, code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeVolanteCode(candidateCode), normalizedCode, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<ProductionOperatorsScan?> FindRecentDuplicateScanForSameOperatorAsync(
            int operatorId,
            string normalizedCode)
        {
            var cutoff = DateTime.Now.AddSeconds(-ImmediateDuplicateGraceSeconds);

            var recentScans = await _context.ProductionOperatorsScans
                .AsNoTracking()
                .Where(scanItem =>
                    scanItem.OperatorId == operatorId &&
                    scanItem.ScannedAt >= cutoff &&
                    scanItem.Code != null)
                .OrderByDescending(scanItem => scanItem.ScannedAt)
                .Take(10)
                .ToListAsync();

            return recentScans.FirstOrDefault(scanItem =>
                string.Equals(NormalizeVolanteCode(scanItem.Code ?? string.Empty), normalizedCode, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> IsEmployeeNumberCodeAsync(string code, int? areaId)
        {
            if (!int.TryParse(code.Trim(), out var employeeNumber))
            {
                return false;
            }

            return await _context.ProductionOperators
                .AsNoTracking()
                .AnyAsync(operatorItem =>
                    operatorItem.EmployeeNumber == employeeNumber &&
                    operatorItem.Active == true &&
                    (!areaId.HasValue || operatorItem.AreaId == areaId.Value));
        }

        private bool ValidateConfiguredCustomerScan(string? customerName, string? areaName, string scannerValue, out string error)
        {
            error = string.Empty;

            var customerText = $"{customerName} {areaName}";
            if (!customerText.Contains("zf", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var zfSection = _configuration.GetSection("ScannerValidation:ZF");
            if (!zfSection.GetValue<bool>("Enabled"))
            {
                return true;
            }

            var normalizedScan = scannerValue.Trim().ToUpperInvariant();
            var allowedPrefixes = zfSection.GetSection("AllowedPrefixes").Get<string[]>() ?? Array.Empty<string>();
            var allowedSuffixes = zfSection.GetSection("AllowedSuffixes").Get<string[]>() ?? Array.Empty<string>();

            var normalizedPrefixes = allowedPrefixes
                .Select(prefix => (prefix ?? string.Empty).Trim().ToUpperInvariant())
                .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
                .ToArray();

            var normalizedSuffixes = allowedSuffixes
                .Select(suffix => (suffix ?? string.Empty).Trim().ToUpperInvariant())
                .Where(suffix => !string.IsNullOrWhiteSpace(suffix))
                .ToArray();

            var hasValidPrefix = normalizedPrefixes.Length == 0
                || normalizedPrefixes.Any(prefix => normalizedScan.StartsWith(prefix));

            var hasValidSuffix = normalizedSuffixes.Length == 0
                || normalizedSuffixes.Any(suffix => normalizedScan.EndsWith(suffix));

            if (hasValidPrefix && hasValidSuffix)
            {
                return true;
            }

            error = "Lectura alterada. Reescanea la pieza.";
            return false;
        }

        private static string NormalizeVolanteCode(string code)
        {
            var normalized = Regex.Replace(code.Trim().ToUpperInvariant(), @"\s+", "");
            var bIndex = normalized.IndexOf('B');
            var mxIndex = normalized.IndexOf("MX", StringComparison.Ordinal);

            if (bIndex >= 0 && mxIndex > bIndex)
            {
                return normalized[bIndex..mxIndex] + "MX";
            }

            return normalized;
        }

        private async Task<int> AcquireScanKeyLockAsync(string code, string operation, int? areaId)
        {
            var lockName = $"ProductionOperatorsScan:{areaId?.ToString() ?? "NA"}:{operation}:{code}";

            if (lockName.Length > 255)
            {
                lockName = lockName[..255];
            }

            var resultParameter = new SqlParameter("@Result", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC @Result = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000",
                resultParameter,
                new SqlParameter("@Resource", lockName));

            return (int)(resultParameter.Value ?? -999);
        }
    }
}

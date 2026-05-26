using GDIKPI.Data;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Controllers
{
    public class ProductionOperatorsController : Controller
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;

        public ProductionOperatorsController(KpisContext context, PermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ValidateEmployee(int employeeNumber)
        {
            var productionOperator = await _context.ProductionOperators
                .AsNoTracking()
                .Where(operatorItem =>
                    operatorItem.EmployeeNumber == employeeNumber &&
                    operatorItem.Active == true)
                .Select(operatorItem => new
                {
                    operatorItem.OperatorId,
                    operatorItem.EmployeeNumber,
                    FullName = (operatorItem.NameOperator ?? "") + " " + (operatorItem.LastnameOperator ?? ""),
                    operatorItem.Operation,
                    operatorItem.Goal
                })
                .FirstOrDefaultAsync();

            if (productionOperator is null)
            {
                return Json(new
                {
                    success = false,
                    message = "Empleado invalido o inactivo."
                });
            }

            return Json(new
            {
                success = true,
                operatorData = productionOperator
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentScans(int limit = 10)
        {
            limit = Math.Clamp(limit, 1, 5);
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var recentScans = await _context.ProductionOperatorsScans
                .AsNoTracking()
                .Where(scanItem =>
                    scanItem.ScannedAt >= today &&
                    scanItem.ScannedAt < tomorrow)
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
                    scanItem.Operator.Operation
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                scans = recentScans
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetScansSummary()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var summary = await _context.ProductionOperatorsScans
                .AsNoTracking()
                .Where(scanItem =>
                    scanItem.ScannedAt >= today &&
                    scanItem.ScannedAt < tomorrow)
                .GroupBy(scanItem => new
                {
                    scanItem.Operator.EmployeeNumber,
                    NameOperator = scanItem.Operator.NameOperator ?? "",
                    LastnameOperator = scanItem.Operator.LastnameOperator ?? "",
                    Operation = scanItem.Operator.Operation ?? ""
                })
                .Select(group => new
                {
                    group.Key.EmployeeNumber,
                    FullName = ((group.Key.NameOperator ?? "") + " " + (group.Key.LastnameOperator ?? "")).Trim(),
                    group.Key.Operation,
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
                return BadRequest(new { success = false, message = "Solicitud invalida." });
            }

            if (request.EmployeeNumber <= 0)
            {
                return BadRequest(new { success = false, message = "Numero de empleado invalido." });
            }

            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { success = false, message = "El codigo es requerido." });
            }

            var code = request.Code.Trim();

            if (code.Length > 50)
            {
                return BadRequest(new { success = false, message = "El codigo no puede exceder 50 caracteres." });
            }

            var productionOperator = await _context.ProductionOperators
                .AsNoTracking()
                .Where(operatorItem =>
                    operatorItem.EmployeeNumber == request.EmployeeNumber &&
                    operatorItem.Active == true)
                .Select(operatorItem => new
                {
                    operatorItem.OperatorId,
                    operatorItem.EmployeeNumber,
                    operatorItem.NameOperator,
                    operatorItem.LastnameOperator,
                    Operation = operatorItem.Operation ?? ""
                })
                .FirstOrDefaultAsync();

            if (productionOperator is null)
            {
                return NotFound(new { success = false, message = "Empleado invalido o inactivo." });
            }

            var operation = productionOperator.Operation.Trim();

            // Códigos especiales que pueden repetirse (empiezan con "OVERRIDE-")
            var isOverrideCode = code.StartsWith("UNREADABLE", StringComparison.OrdinalIgnoreCase);

            if (!isOverrideCode)
            {
                var codeAlreadyScannedInOperation = await _context.ProductionOperatorsScans
                    .AsNoTracking()
                    .AnyAsync(scanItem =>
                        scanItem.Code == code &&
                        scanItem.Operator.Operation == operation);

                if (codeAlreadyScannedInOperation)
                {
                    return Conflict(new
                    {
                        success = false,
                        message = "Este codigo ya fue escaneado por un operador de la misma operacion."
                    });
                }
            }

            var scan = new ProductionOperatorsScan
            {
                OperatorId = productionOperator.OperatorId,
                Code = code,
                ScannedAt = DateTime.Now
            };

            _context.ProductionOperatorsScans.Add(scan);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Escaneo guardado.",
                scanId = scan.Id,
                scannedAt = scan.ScannedAt,
                scan = new
                {
                    scan.Id,
                    scan.Code,
                    scan.ScannedAt,
                    productionOperator.EmployeeNumber,
                    FullName = (productionOperator.NameOperator ?? "") + " " + (productionOperator.LastnameOperator ?? ""),
                    productionOperator.Operation
                }
            });
        }

        public class SaveProductionOperatorScanRequest
        {
            public int EmployeeNumber { get; set; }

            public string? Code { get; set; }
        }
    }
}

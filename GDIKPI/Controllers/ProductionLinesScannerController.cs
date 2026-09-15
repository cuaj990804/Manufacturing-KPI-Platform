using GDIKPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Controllers
{
    public class ProductionLinesScannerController : Controller
    {
        private readonly KpisContext _context;
        private readonly IConfiguration _configuration;

        public ProductionLinesScannerController(KpisContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index(int? areaId = null)
        {
            if (areaId.HasValue)
            {
                var area = await _context.Areas
                    .AsNoTracking()
                    .Where(item => item.AreaId == areaId.Value)
                    .Select(item => new
                    {
                        item.AreaId,
                        item.AreaName,
                        item.CustomerName
                    })
                    .FirstOrDefaultAsync();

                if (area is null)
                {
                    ViewBag.ErrorMessage = "Area no encontrada.";
                }
                else
                {
                    ViewBag.AreaId = area.AreaId;
                    ViewBag.AreaName = area.AreaName;
                    ViewBag.CustomerName = area.CustomerName;
                }
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

            var batchInactivitySeconds = _configuration.GetValue<int?>(
                "ProductionLineScanner:BatchInactivitySeconds") ?? 60;
            ViewBag.BatchInactivitySeconds = batchInactivitySeconds > 0
                ? Math.Min(batchInactivitySeconds, 86400)
                : 60;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ValidateLine(int lineNumber, int? areaId = null)
        {
            if (lineNumber <= 0)
            {
                return BadRequest(new { success = false, message = "Numero de linea invalido." });
            }

            var query = _context.ProductionLines
                .AsNoTracking()
                .Where(line => line.LineNumber == lineNumber && line.IsActive);

            if (areaId.HasValue)
            {
                query = query.Where(line => line.AreaId == areaId.Value);
            }

            var matches = await query
                .OrderBy(line => line.ProductionLinesId)
                .Take(2)
                .Select(line => new
                {
                    line.ProductionLinesId,
                    line.LineNumber,
                    line.LineName,
                    line.AreaId,
                    AreaName = line.Area != null ? line.Area.AreaName : null,
                    CustomerName = line.Area != null ? line.Area.CustomerName : null
                })
                .ToListAsync();

            if (matches.Count == 0)
            {
                return NotFound(new
                {
                    success = false,
                    message = areaId.HasValue
                        ? "Linea invalida, inactiva o no pertenece a esta area."
                        : "Linea invalida o inactiva."
                });
            }

            if (!areaId.HasValue && matches.Count > 1)
            {
                return Conflict(new
                {
                    success = false,
                    message = "El numero de linea existe en mas de un area. Abra el modulo desde un area especifica."
                });
            }

            var selectedLine = matches[0];

            return Json(new { success = true, lineData = selectedLine });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentScans(int limit = 5, int? areaId = null)
        {
            limit = Math.Clamp(limit, 1, 5);
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var query =
                from scan in _context.ScannerProductions.AsNoTracking()
                join line in _context.ProductionLines.AsNoTracking()
                    on scan.LineId equals line.ProductionLinesId
                where scan.ScannerProductionDateTime >= today
                    && scan.ScannerProductionDateTime < tomorrow
                    && (!areaId.HasValue || line.AreaId == areaId.Value)
                orderby scan.ScannerProductionDateTime descending, scan.ScannerProductionId descending
                select new
                {
                    scan.ScannerProductionId,
                    scan.ScannerValue,
                    scan.ScannerProductionDateTime,
                    line.ProductionLinesId,
                    line.LineNumber,
                    line.AreaId
                };

            return Json(new
            {
                success = true,
                scans = await query.Take(limit).ToListAsync()
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetScansSummary(int? areaId = null)
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var productionDate = DateOnly.FromDateTime(today);

            var productionTotals = await (
                from production in _context.ProductionData.AsNoTracking()
                join line in _context.ProductionLines.AsNoTracking()
                    on production.ProductionLinesId equals line.ProductionLinesId
                where production.ProductionDate == productionDate
                    && (production.ProducedPieces ?? 0) > 0
                    && (!areaId.HasValue || line.AreaId == areaId.Value)
                group production by new
                {
                    line.ProductionLinesId,
                    line.LineNumber,
                    line.LineName,
                    line.AreaId
                }
                into groupItem
                select new
                {
                    groupItem.Key.ProductionLinesId,
                    groupItem.Key.LineNumber,
                    groupItem.Key.LineName,
                    groupItem.Key.AreaId,
                    Quantity = groupItem.Sum(item => item.ProducedPieces ?? 0)
                }).ToListAsync();

            var productionLineIds = productionTotals
                .Select(item => item.ProductionLinesId)
                .ToList();

            var lastPhysicalScans = await _context.ScannerProductions
                .AsNoTracking()
                .Where(scan =>
                    productionLineIds.Contains(scan.LineId) &&
                    scan.ScannerProductionDateTime >= today &&
                    scan.ScannerProductionDateTime < tomorrow)
                .GroupBy(scan => scan.LineId)
                .Select(groupItem => new
                {
                    ProductionLinesId = groupItem.Key,
                    LastScannedAt = groupItem.Max(item => item.ScannerProductionDateTime)
                })
                .ToDictionaryAsync(item => item.ProductionLinesId, item => item.LastScannedAt);

            var lastQuantityEntries = await _context.AuditLogs
                .AsNoTracking()
                .Where(audit =>
                    audit.EntityName == "ProductionLine" &&
                    audit.ActionType == "ADD_MANUAL_PRODUCTION" &&
                    audit.EntityId.HasValue &&
                    productionLineIds.Contains(audit.EntityId.Value) &&
                    audit.Timestamp >= today &&
                    audit.Timestamp < tomorrow)
                .GroupBy(audit => audit.EntityId!.Value)
                .Select(groupItem => new
                {
                    ProductionLinesId = groupItem.Key,
                    LastScannedAt = groupItem.Max(item => item.Timestamp)
                })
                .ToDictionaryAsync(item => item.ProductionLinesId, item => item.LastScannedAt);

            var scans = productionTotals
                .Select(item =>
                {
                    lastPhysicalScans.TryGetValue(item.ProductionLinesId, out var physicalScanAt);
                    lastQuantityEntries.TryGetValue(item.ProductionLinesId, out var quantityEntryAt);

                    DateTime? lastScannedAt = null;
                    if (physicalScanAt != default)
                        lastScannedAt = physicalScanAt;
                    if (quantityEntryAt != default && (!lastScannedAt.HasValue || quantityEntryAt > lastScannedAt.Value))
                        lastScannedAt = quantityEntryAt;

                    return new
                    {
                        item.ProductionLinesId,
                        item.LineNumber,
                        item.LineName,
                        item.AreaId,
                        item.Quantity,
                        LastScannedAt = lastScannedAt
                    };
                })
                .Where(item => item.LastScannedAt.HasValue)
                .OrderByDescending(item => item.Quantity)
                .ThenBy(item => item.LineNumber)
                .ToList();

            return Json(new
            {
                success = true,
                scans
            });
        }
    }
}

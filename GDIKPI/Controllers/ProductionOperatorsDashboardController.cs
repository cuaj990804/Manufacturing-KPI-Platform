using GDIKPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Controllers
{
    [Authorize]
    public class ProductionOperatorsDashboardController : Controller
    {
        private readonly KpisContext _context;

        public ProductionOperatorsDashboardController(KpisContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? areaId = null)
        {
            var areas = await _context.Areas
                .AsNoTracking()
                .OrderBy(areaItem => areaItem.CustomerName)
                .ThenBy(areaItem => areaItem.AreaName)
                .Select(areaItem => new
                {
                    areaItem.AreaId,
                    areaItem.AreaName,
                    areaItem.CustomerName
                })
                .ToListAsync();

            ViewBag.Areas = areas;

            if (areaId.HasValue)
            {
                var area = areas.FirstOrDefault(areaItem => areaItem.AreaId == areaId.Value);

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

            var operatorsQuery = _context.ProductionOperators
                .AsNoTracking()
                .Where(operatorItem => !string.IsNullOrWhiteSpace(operatorItem.Operation));

            if (areaId.HasValue)
            {
                operatorsQuery = operatorsQuery.Where(operatorItem => operatorItem.AreaId == areaId.Value);
            }

            var operations = await operatorsQuery
                .Select(operatorItem => operatorItem.Operation!)
                .Distinct()
                .OrderBy(operation => operation)
                .ToListAsync();

            var hasProductionLines = await _context.ProductionLines
                .AsNoTracking()
                .AnyAsync(line =>
                    line.IsActive &&
                    (!areaId.HasValue || line.AreaId == areaId.Value));

            if (hasProductionLines && !operations.Contains("VOLANTES"))
            {
                operations.Add("VOLANTES");
                operations.Sort(StringComparer.OrdinalIgnoreCase);
            }

            ViewBag.OperationsList = new SelectList(operations);

            return View();
        }
        public async Task<IActionResult> OperatorStats()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var stats = await _context.ProductionOperators
                .AsNoTracking()
                .Where(o => o.Active != false)
                .Select(o => new
                {
                    o.EmployeeNumber,
                    FullName = (o.NameOperator ?? "") + " " + (o.LastnameOperator ?? ""),
                    Operation = o.Operation ?? "Sin operacion",
                    o.Goal,
                    ScanCount = o.ProductionOperatorsScans
                        .Count(s => s.ScannedAt >= today && s.ScannedAt < tomorrow)
                })
                .OrderBy(o => o.Operation)
                .ThenByDescending(o => o.ScanCount)
                .ThenBy(o => o.FullName)
                .ToListAsync();

            var lineStats = await _context.ProductionLines
                .AsNoTracking()
                .Where(line => line.IsActive)
                .Select(line => new
                {
                    EmployeeNumber = line.LineNumber ?? line.ProductionLinesId,
                    FullName = "LINEA " + (line.LineNumber ?? line.ProductionLinesId),
                    Operation = "VOLANTES",
                    Goal = (int?)line.DailyGoal,
                    ScanCount = _context.ScannerProductions.Count(scan =>
                        scan.LineId == line.ProductionLinesId &&
                        scan.ScannerProductionDateTime >= today &&
                        scan.ScannerProductionDateTime < tomorrow)
                })
                .OrderByDescending(line => line.ScanCount)
                .ThenBy(line => line.FullName)
                .ToListAsync();

            stats.AddRange(lineStats);

            var grouped = stats
                .GroupBy(o => o.Operation)
                .Select(g => new { Operation = g.Key, Operators = g.ToList() })
                .ToList();

            ViewBag.Groups = grouped;
            return View();
        }
    }
}

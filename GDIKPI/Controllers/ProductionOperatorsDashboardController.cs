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

        public async Task<IActionResult> Index()
        {
            var operations = await _context.ProductionOperators
                .AsNoTracking()
                .Where(operatorItem => !string.IsNullOrWhiteSpace(operatorItem.Operation))
                .Select(operatorItem => operatorItem.Operation!)
                .Distinct()
                .OrderBy(operation => operation)
                .ToListAsync();

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

            var grouped = stats
                .GroupBy(o => o.Operation)
                .Select(g => new { Operation = g.Key, Operators = g.ToList() })
                .ToList();

            ViewBag.Groups = grouped;
            return View();
        }
    }
}

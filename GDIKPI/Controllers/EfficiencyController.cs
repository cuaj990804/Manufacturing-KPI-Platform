using GDIKPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GDIKPI.Controllers
{
    [Authorize]
    public class EfficiencyController : Controller
    {
        private readonly KpisContext _context;

        public EfficiencyController(KpisContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var areas = _context.Efficiencies
                .Where(e => !string.IsNullOrWhiteSpace(e.AreaCustomerName))
                .Select(e => e.AreaCustomerName!)
                .Distinct()
                .OrderBy(area => area)
                .ToList()
                .Select(area => new
                {
                    DisplayName = area
                })
                .ToList();

            ViewBag.Areaslist = new SelectList(areas, "DisplayName", "DisplayName");

            return View();
        }
    }
}

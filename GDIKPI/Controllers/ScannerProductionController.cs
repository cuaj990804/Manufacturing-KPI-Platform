using GDIKPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Controllers
{

    public class ScannerProductionController : Controller
    {
        private readonly KpisContext _context;

        public ScannerProductionController(KpisContext context)
        {
            _context = context;
        }

        // GET: ScannerProductionController
        public async Task<ActionResult> Index(int? productionLineId)
        {
            if (productionLineId == null)
            {
                return BadRequest("No se especificó una línea de producción.");
            }

            var productionLine = await LoadProductionLineIntoViewBag(productionLineId.Value);

            if (productionLine == null)
            {
                return NotFound($"La línea de producción con ID {productionLineId} no fue encontrada.");
            }

            return View();
        }

        public async Task<ActionResult> Reject(int? productionLineId, string? employeeNumber)
        {
            if (productionLineId == null)
            {
                return BadRequest("No se especificó una línea de producción.");
            }

            var productionLine = await LoadProductionLineIntoViewBag(productionLineId.Value);

            if (productionLine == null)
            {
                return NotFound($"La línea de producción con ID {productionLineId} no fue encontrada.");
            }

            ViewBag.EmployeeNumber = employeeNumber;
            return View();
        }

        // GET: ScannerProductionController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: ScannerProductionController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: ScannerProductionController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: ScannerProductionController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: ScannerProductionController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: ScannerProductionController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: ScannerProductionController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: Obtener todas las áreas (customers)
        public async Task<JsonResult> GetAreas()
        {
            try
            {
                var areas = await _context.Areas
                    .Select(a => new
                    {
                        areaId = a.AreaId,
                        areaName = a.AreaName,
                        customerName = a.CustomerName
                    })
                    .ToListAsync();

                return Json(areas);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // GET: Obtener programas por area (AreaName + CustomerName, ej: "Forrado ZF")
        public async Task<JsonResult> GetProgramsByCustomer(string customerName)
        {
            try
            {
                if (string.IsNullOrEmpty(customerName))
                {
                    return Json(new List<object>());
                }

                using var client = new HttpClient();
                client.BaseAddress = new Uri("http://192.168.1.1:9091");
                client.Timeout = TimeSpan.FromSeconds(30);

                var response = await client.GetAsync($"/GetProgram?area={Uri.EscapeDataString(customerName)}");

                if (response.IsSuccessStatusCode)
                {
                    var programs = await response.Content.ReadFromJsonAsync<List<dynamic>>();

                    if (programs != null && programs.Any())
                    {
                        var result = programs.Select(p => new
                        {
                            id = p.GetProperty("id").ToString(),
                            program = p.GetProperty("program").ToString(),
                            type = p.GetProperty("type").GetString(),
                            partnumber = p.GetProperty("partnumber").GetString()
                        }).ToList();

                        return Json(result);
                    }
                }

                return Json(new List<object>());
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        private async Task<Models.ProductionLine?> LoadProductionLineIntoViewBag(int productionLineId)
        {
            var productionLine = await _context.ProductionLines
                .Include(pl => pl.Area)
                .FirstOrDefaultAsync(pl => pl.ProductionLinesId == productionLineId);

            if (productionLine == null)
            {
                return null;
            }

            ViewBag.ProductionLineId = productionLine.ProductionLinesId;
            ViewBag.LineNumber = productionLine.LineNumber;
            ViewBag.LineName = productionLine.LineName;
            ViewBag.DailyGoal = productionLine.DailyGoal;
            ViewBag.AreaId = productionLine.AreaId;
            ViewBag.AreaName = productionLine.Area?.AreaName;
            ViewBag.CustomerName = productionLine.Area?.CustomerName;
            ViewBag.PersonalQuantity = productionLine.PersonalQuantity;
            ViewBag.StandardTime = productionLine.StandardTime ?? 0m;

            return productionLine;
        }
    }
}

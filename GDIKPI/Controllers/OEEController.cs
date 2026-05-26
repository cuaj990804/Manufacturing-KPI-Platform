using GDIKPI.Data;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Controllers
{
    [Authorize]
    public class OEEController : Controller
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;
        public OEEController(KpisContext context, PermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }
        // GET: OEEController
        public ActionResult Index()
        {
            var areas = _context.Areas
                .Select(a => new
                {
                    DisplayName = a.CustomerName + " - " +  a.AreaName
                })
                .ToList();
            var LineNumber = new List<object>();

            var Description = _context.VwOees
                .Select(a => new
                {
                    Description = a.Category
                })
                .Distinct()
                .ToList();
            ViewBag.Areaslist = new SelectList(areas, "DisplayName", "DisplayName");
            ViewBag.LinesNumberList = new SelectList(LineNumber, "Line", "Line");
            ViewBag.DescriptionList = new SelectList(Description, "Description", "Description");



            return View();
        }

        public JsonResult GetLinesByArea(string area)
        {
            if (string.IsNullOrEmpty(area))
                return Json(new List<object>()); // No hay área seleccionada

            var parts = area.Split(" - ");
            var customerName = parts[0];
            var areaName = parts.Length > 1 ? parts[1] : "";

            // Obtener el AreaID del área seleccionada
            int? areaId = _context.Areas
                .Where(a => a.CustomerName == customerName && a.AreaName == areaName)
                .Select(a => (int?)a.AreaId)
                .FirstOrDefault();

            // Obtener las líneas de esa área con sus nombres
            var lines = _context.ProductionLines
                .Where(l => l.AreaId == areaId && l.IsActive)
                .Select(l => new
                {
                    l.LineNumber,
                    l.LineName
                })
                .Distinct()
                .OrderBy(l => l.LineNumber)
                .ToList();

            // Agregar manualmente la opción "Todas las lineas"
            var result = new List<object>
            {
                new { Value = "", Text = "Todas las lineas" }
            };

            // Usar LineName si existe, sino usar "Línea {número}"
            result.AddRange(lines.Select(l => new
            {
                Value = l.LineNumber,
                Text = !string.IsNullOrWhiteSpace(l.LineName) ? l.LineName : $"Línea {l.LineNumber}"
            }));

            return Json(result);
        }



        public IActionResult AddOEEParamModal()
        {
            return PartialView("Parameters"); 
        }


        // GET: OEEController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: OEEController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: OEEController/Create
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

        // GET: OEEController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: OEEController/Edit/5
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

        // GET: OEEController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: OEEController/Delete/5
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
    }
}

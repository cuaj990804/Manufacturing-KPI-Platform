using GDIKPI.Data;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GDIKPI.Controllers
{
    public class ProductionDataController : Controller
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;

        public ProductionDataController(KpisContext context, PermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }

        // GET: ProductionData
        public async Task<IActionResult> Index(int? areaId)
        {
            var area = _context.Areas
             .Where(a => a.AreaId == areaId)
             .Select(a => new
             {
                 a.AreaId,
                 DisplayName = a.CustomerName + " - " + a.AreaName
             })
             .FirstOrDefault();

            var employeeNumberClaim = HttpContext.User.FindFirst("EmployeeNumber")?.Value;
            if (string.IsNullOrEmpty(employeeNumberClaim))
            {
                return Unauthorized();
            }

            bool isAdmin = await _permissionService.IsUserAdmin(employeeNumberClaim);
            bool canCreate = await _permissionService.HasAccessibleLines(employeeNumberClaim, "Create");

            ViewBag.CanCreateOrEdit = isAdmin || canCreate;
            ViewBag.AreaId = areaId ?? 0;
            ViewBag.areaName = area != null ? area.DisplayName : "All Areas";

            return View();
        }
        public async Task<JsonResult> GetProgramsByArea(int areaId)
        {
            try
            {
                if (areaId <= 0)
                    return Json(new List<object>());

                var areaName = await _context.Areas
                    .Where(a => a.AreaId == areaId)
                    .Select(a => a.AreaName + " " + a.CustomerName)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(areaName))
                    return Json(new List<object>());

                using var client = new HttpClient();
                client.BaseAddress = new Uri("http://192.168.1.1:9091");
                client.Timeout = TimeSpan.FromSeconds(30);

                var response = await client.GetAsync($"/GetProgram?area={Uri.EscapeDataString(areaName)}");

                if (response.IsSuccessStatusCode)
                {
                    var programs = await response.Content.ReadFromJsonAsync<List<dynamic>>();

                    if (programs != null && programs.Any())
                    {
                        var result = programs.Select(p => new
                        {
                            id = p.GetProperty("id").ToString(),
                            program = p.GetProperty("program").ToString()
                        }).ToList();

                        return Json(result);
                    }

                    return Json(new List<object>
            {
                new { id = "", program = "No hay programas disponibles" }
            });
                }
                else
                {
                    return Json(new List<object>
            {
                new { id = "", program = "Error al cargar programas" }
            });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener programas: {ex.Message}");
                return Json(new List<object>
        {
            new { id = "", program = "Error al cargar programas" }
                });
            }
        }

        public async Task<IActionResult> LoadCreateForm(int areaId)
        {
            var now = DateTime.Now;

            var employeeNumberClaim = HttpContext.User.FindFirst("EmployeeNumber")?.Value;
            if (string.IsNullOrEmpty(employeeNumberClaim))
                return Unauthorized();

            bool isAdmin = await _permissionService.IsUserAdmin(employeeNumberClaim);

            List<ProductionLine> productionLines;

            if (isAdmin)
            {
                // Admin ve todas las líneas del área
                productionLines = _context.ProductionLines
                    .Where(pl => pl.AreaId == areaId && pl.IsActive)
                    .ToList();
            }
            else
            {
                // Obtener las líneas a las que tiene acceso con permiso "Create"
                var accessibleLines = await _permissionService.GetAllAccessibleLines(employeeNumberClaim, "Create");

                // Debug logging
                Console.WriteLine($"DEBUG - User {employeeNumberClaim}:");
                Console.WriteLine($"  Accessible lines: [{string.Join(", ", accessibleLines)}]");
                Console.WriteLine($"  Filtering by areaId: {areaId}");

                // Obtener todas las líneas accesibles sin filtro de área para debug
                var allAccessibleLines = _context.ProductionLines
                    .Where(pl => accessibleLines.Contains(pl.ProductionLinesId) && pl.IsActive)
                    .Select(pl => new { pl.ProductionLinesId, pl.AreaId, pl.LineNumber })
                    .ToList();

                Console.WriteLine($"  All accessible lines: [{string.Join(", ", allAccessibleLines.Select(l => $"Line{l.ProductionLinesId}(Area{l.AreaId})"))}]");

                productionLines = _context.ProductionLines
                    .Where(pl => pl.AreaId == areaId && accessibleLines.Contains(pl.ProductionLinesId) && pl.IsActive)
                    .ToList();

                Console.WriteLine($"  Filtered lines for area {areaId}: [{string.Join(", ", productionLines.Select(pl => $"Line{pl.ProductionLinesId}"))}]");
            }

            // Crear lista con display name (alias si existe, sino número de línea)
            var productionLinesDisplay = productionLines.Select(pl => new
            {
                pl.ProductionLinesId,
                DisplayName = !string.IsNullOrEmpty(pl.LineName) ? pl.LineName : $"Línea {pl.LineNumber}"
            }).ToList();

            ViewBag.ProductionLines = new SelectList(productionLinesDisplay, "ProductionLinesId", "DisplayName");

            // Generar intervalos horarios desde las 07:00 hasta las 24:00
            var intervals = new List<string>();
            for (int i = 7; i < 24; i++)
            {
                int nextHour = i + 1;
                intervals.Add($"{i:00}:00-{nextHour:00}:00");
            }

            ViewBag.Intervals = intervals;

            // Ajustar SelectedInterval para que no quede fuera del rango 7-24
            int currentHour = now.Hour < 7 ? 7 : (now.Hour >= 24 ? 23 : now.Hour);
            int nextCurrentHour = currentHour + 1;
            ViewBag.SelectedInterval = $"{currentHour:00}:00-{nextCurrentHour:00}:00";

            ViewBag.AreaId = areaId;

            var model = new ProductionDatum
            {
                ProductionDate = DateOnly.FromDateTime(DateTime.Today)
            };

            return PartialView("Create", model);
        }

        [HttpGet]
        public IActionResult GetRegisteredIntervalsForLine(int lineId, string date)
        {
            try
            {
                if (!DateOnly.TryParse(date, out var parsedDate))
                {
                    return BadRequest(new { message = "Fecha inválida" });
                }

                var registeredIntervals = _context.ProductionData
                    .Where(pd => pd.ProductionLinesId == lineId && pd.ProductionDate == parsedDate)
                    .Select(pd => pd.StartHour.ToString("HH\\:mm") + "-" + pd.EndHour.ToString("HH\\:mm"))
                    .Distinct()
                    .ToList();

                return Json(registeredIntervals);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error: " + ex.Message });
            }
        }

        public IActionResult LoadEditForm(int id)
        {
            var model = _context.ProductionData.FirstOrDefault(p => p.ProductionId == id);
            if (model == null)
                return NotFound();

            var productionLines = _context.ProductionLines
                .Where(pl => pl.AreaId == 1 && pl.IsActive)
                .ToList();

            ViewBag.ProductionLines = new SelectList(productionLines, "ProductionLinesId", "LineNumber", model.ProductionLinesId);

            // Crear intervalos de hora
            var intervals = new List<string>();
            for (int i = 0; i < 24; i++)
            {
                int nextHour = (i + 1) % 24;
                intervals.Add($"{i:D2}:00-{nextHour:D2}:00");
            }
            ViewBag.Intervals = intervals;


            string selectedInterval = $"{model.StartHour:HH\\:mm}-{model.EndHour:HH\\:mm}";
            ViewBag.SelectedInterval = selectedInterval;

            return PartialView("Edit", model);
        }

   

        public async Task<IActionResult> LoadDownTime(int areaId)
        {
            var now = DateTime.Now;

            // Obtener número de empleado del claim
            var employeeNumberClaim = HttpContext.User.FindFirst("EmployeeNumber")?.Value;
            if (string.IsNullOrEmpty(employeeNumberClaim))
                return Unauthorized();

            // Verificar si es admin
            bool isAdmin = await _permissionService.IsUserAdmin(employeeNumberClaim);

            List<ProductionLine> productionLines;

            if (isAdmin)
            {
                // Admin ve todas las líneas del área
                productionLines = _context.ProductionLines
                    .Where(pl => pl.AreaId == areaId && pl.IsActive)
                    .ToList();
            }
            else
            {
                // Obtener las líneas a las que tiene acceso con permiso "Create"
                var accessibleLines = await _permissionService.GetAllAccessibleLines(employeeNumberClaim, "Create");

                productionLines = _context.ProductionLines
                    .Where(pl => pl.AreaId == areaId && accessibleLines.Contains(pl.ProductionLinesId) && pl.IsActive)
                    .ToList();
            }

            ViewBag.ProductionLines = new SelectList(productionLines, "ProductionLinesId", "LineNumber");
            ViewBag.AreaId = areaId;

            var model = new ProductionDatum
            {
                ProductionDate = DateOnly.FromDateTime(DateTime.Today)
            };

            return PartialView("DownTime", model);
        }
        public async Task<IActionResult> LoadAbsenteeism(int areaId)
        {
            var now = DateTime.Now;

            // Obtener número de empleado del claim
            var employeeNumberClaim = HttpContext.User.FindFirst("EmployeeNumber")?.Value;
            if (string.IsNullOrEmpty(employeeNumberClaim))
                return Unauthorized();

            // Verificar si es admin
            bool isAdmin = await _permissionService.IsUserAdmin(employeeNumberClaim);

            List<ProductionLine> productionLines;

            if (isAdmin)
            {
                // Admin ve todas las líneas del área
                productionLines = _context.ProductionLines
                    .Where(pl => pl.AreaId == areaId && pl.IsActive)
                    .ToList();
            }
            else
            {
                // Obtener las líneas a las que tiene acceso con permiso "Create"
                var accessibleLines = await _permissionService.GetAllAccessibleLines(employeeNumberClaim, "Create");

                productionLines = _context.ProductionLines
                    .Where(pl => pl.AreaId == areaId && accessibleLines.Contains(pl.ProductionLinesId) && pl.IsActive)
                    .ToList();
            }

            ViewBag.ProductionLines = new SelectList(productionLines, "ProductionLinesId", "LineNumber");
            ViewBag.AreaId = areaId;

            var model = new ProductionDatum
            {
                ProductionDate = DateOnly.FromDateTime(DateTime.Today)
            };

            return PartialView("Absenteeism", model);
        }


        public async Task<IActionResult> LoadViewData(int areaId)
        {
            var area = _context.Areas
                .Where(a => a.AreaId == areaId)
                .Select(a => new
                {
                    a.AreaId,
                    DisplayName = a.CustomerName + " - " + a.AreaName
                })
                .FirstOrDefault();

            var employeeNumberClaim = HttpContext.User.FindFirst("EmployeeNumber")?.Value;
            if (string.IsNullOrEmpty(employeeNumberClaim))
            {
                return Unauthorized();
            }

            bool isAdmin = await _permissionService.IsUserAdmin(employeeNumberClaim);
            bool canView = await _permissionService.HasAccessibleLines(employeeNumberClaim, "View");

            if (!isAdmin && !canView)
            {
                return StatusCode(403, "No tienes permisos para ver estos datos.");
            }

            ViewBag.AreaId = areaId;
            ViewBag.AreaName = area != null ? area.DisplayName : "Área no encontrada";

            // Obtener líneas de producción del área para el filtro
            var productionLines = await _context.ProductionLines
                .Where(pl => pl.AreaId == areaId && pl.IsActive == true)
                .OrderBy(pl => pl.LineNumber)
                .Select(pl => new SelectListItem
                {
                    Value = pl.ProductionLinesId.ToString(),
                    Text = $"Línea {pl.LineNumber}"
                })
                .ToListAsync();

            ViewBag.ProductionLines = productionLines;

            return PartialView("ViewData");
        }

        private bool ProductionDatumExists(int id)
        {
            return _context.ProductionData.Any(e => e.ProductionId == id);
        }
    }
}

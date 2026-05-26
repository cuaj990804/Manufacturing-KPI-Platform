using GDIKPI.Data;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.Elfie.Serialization;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Controllers
{
    public class DefectsController : Controller
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;
        public DefectsController(KpisContext context, PermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }
        public IActionResult Index()
        {
            return View();
        }
        public async Task<IActionResult> RegisterDefects(int areaId)
        {
            var employeeNumberClaim = User.FindFirst("EmployeeNumber")?.Value;

            if (string.IsNullOrEmpty(employeeNumberClaim))
            {
                return RedirectToAction("Login", "Account");
            }

            bool isAdmin = await _permissionService.IsUserAdmin(employeeNumberClaim);

            List<ProductionLine> productionLines;

            if (areaId > 0)
            {
                // Filtrar líneas de producción por el areaId recibido
                productionLines = _context.ProductionLines
                    .Where(pl => pl.IsActive && pl.AreaId == areaId)
                    .Include(pl => pl.Area)
                    .ToList();
            }
            else
            {
                // Si no hay areaId, mostrar todas las líneas activas
                productionLines = _context.ProductionLines
                    .Where(pl => pl.IsActive)
                    .Include(pl => pl.Area)
                    .ToList();
            }

            // Crear lista con LineName si existe, sino LineNumber
            var productionLinesDisplay = productionLines.Select(pl => new
            {
                pl.ProductionLinesId,
                DisplayName = !string.IsNullOrWhiteSpace(pl.LineName) ? pl.LineName : $"Línea {pl.LineNumber}"
            }).ToList();

            ViewBag.ProductionLines = new SelectList(productionLinesDisplay, "ProductionLinesId", "DisplayName");

            var now = DateTime.Now;

            var intervals = new List<string>();
            for (int i = 7; i < 24; i++)
            {
                int nextHour = (i + 1) % 24;
                intervals.Add($"{i:00}:00-{nextHour:00}:00");
            }

            int currentHour = now.Hour;
            int nextCurrentHour = (currentHour + 1) % 24;
            ViewBag.Intervals = intervals;
            ViewBag.SelectedInterval = $"{currentHour:00}:00-{nextCurrentHour:00}:00";

            ViewBag.AreaId = areaId;

            return View();
        }
        public async Task<JsonResult> GetProgramsByArea(int areaId)
        {
            try
            {
                if (areaId <= 0)
                    return Json(new List<object>());

                // Obtener solo el área específica según areaId
                var area = await _context.Areas
                    .Where(a => a.AreaId == areaId)
                    .FirstOrDefaultAsync();

                if (area == null)
                {
                    return Json(new List<object>
                    {
                        new { id = "", program = "Área no encontrada" }
                    });
                }

                using var client = new HttpClient();
                client.BaseAddress = new Uri("http://192.168.1.1:9091");
                client.Timeout = TimeSpan.FromSeconds(30);

                var areaFullName = area.AreaName + " " + area.CustomerName;
                var response = await client.GetAsync($"/GetProgram?area={Uri.EscapeDataString(areaFullName)}");

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
                }

                return Json(new List<object>
                {
                    new { id = "", program = "No hay programas disponibles" }
                });
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

        [HttpGet]
        public IActionResult GetRegisteredDefectIntervalsForLine(int lineId, string date)
        {
            try
            {
                if (!DateOnly.TryParse(date, out var parsedDate))
                {
                    return BadRequest(new { message = "Fecha inválida" });
                }

                var registeredIntervals = _context.Rejections
                    .Where(r => r.ProductionLinesId == lineId && r.StartTime.Date == parsedDate.ToDateTime(TimeOnly.MinValue))
                    .Select(r => r.StartTime.ToString("HH:mm") + "-" + r.EndTime.ToString("HH:mm"))
                    .Distinct()
                    .ToList();

                return Json(registeredIntervals);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDefectCategories()
        {
            try
            {
                var categories = await _context.DefectsCategories
                    .Select(c => new
                    {
                        c.DefectCategoryId,
                        c.DefectCategoryName
                    })
                    .ToListAsync();

                return Json(categories);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error al cargar categorías: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateDefect([FromBody] CreateDefectRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.DefectName))
                {
                    return BadRequest(new { message = "El nombre del defecto es requerido" });
                }

                if (request.AreaId <= 0)
                {
                    return BadRequest(new { message = "El área es requerida" });
                }

                int categoryId = request.DefectCategoryId;

                // Si se proporcionó un nombre de categoría personalizado (cuando se selecciona "Otros")
                if (!string.IsNullOrWhiteSpace(request.CustomCategoryName))
                {
                    // Verificar si ya existe una categoría con ese nombre
                    var existingCategory = await _context.DefectsCategories
                        .FirstOrDefaultAsync(c => c.DefectCategoryName.ToLower() == request.CustomCategoryName.ToLower());

                    if (existingCategory != null)
                    {
                        // Si ya existe, usar esa categoría
                        categoryId = existingCategory.DefectCategoryId;
                    }
                    else
                    {
                        // Si no existe, crear la nueva categoría
                        var newCategory = new DefectsCategory
                        {
                            DefectCategoryName = request.CustomCategoryName
                        };

                        _context.DefectsCategories.Add(newCategory);
                        await _context.SaveChangesAsync();

                        categoryId = newCategory.DefectCategoryId;
                    }
                }

                // Verificar si ya existe un defecto con el mismo nombre en la misma área
                var existingDefect = await _context.Defects
                    .FirstOrDefaultAsync(d => d.DefectName == request.DefectName && d.AreaId == request.AreaId);

                if (existingDefect != null)
                {
                    return BadRequest(new { message = "Ya existe un defecto con ese nombre en esta área" });
                }

                // Crear el nuevo defecto
                var defect = new Defect
                {
                    DefectName = request.DefectName,
                    AreaId = request.AreaId,
                    DefectCategoryId = categoryId
                };

                _context.Defects.Add(defect);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Defecto creado exitosamente",
                    defectId = defect.DefectId
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error al crear el defecto: " + ex.Message });
            }
        }

    }

    // DTO para recibir datos del formulario
    public class CreateDefectRequest
    {
        public string DefectName { get; set; } = string.Empty;
        public int AreaId { get; set; }
        public int DefectCategoryId { get; set; }
        public string? CustomCategoryName { get; set; }
    }
}
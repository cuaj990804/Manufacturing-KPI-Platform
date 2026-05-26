using GDIKPI.Data;
using GDIKPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GDIKPI.Controllers
{
    [Authorize]

    public class QualityDashboardController : Controller
    {
    
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;
        public QualityDashboardController(KpisContext context, PermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }
        // GET: QualityDashboardController
        public ActionResult Index()
        {
            var areas = _context.Areas
                .Select(a => new
                {
                    DisplayName = a.CustomerName + " - " + a.AreaName
                })
                .ToList();
            var LineNumber = new List<object>();
            var defects = new List<object>();

            var Process = _context.VwRejects
                .Select(a => new
                {
                    category = a.Category
                })
                .Distinct()
                .ToList();

            var DefectCategory = _context.DefectsCategories
               .Select(a => new
               {
                   defectCategoryName = a.DefectCategoryName
               })
               .Distinct()
               .ToList();
            ViewBag.Areaslist = new SelectList(areas, "DisplayName", "DisplayName");
            ViewBag.LinesNumberList = new SelectList(LineNumber, "Line", "Line");
            ViewBag.DefectCategoryList = new SelectList(DefectCategory, "defectCategoryName", "defectCategoryName");
            ViewBag.CategoryList = new SelectList(Process, "category", "category");



            return View();
        }

        public JsonResult GetLinesByArea(string area)
        {
            if (string.IsNullOrEmpty(area))
                return Json(new List<object>()); // No hay área seleccionada

            // Formato: "CustomerName - AreaName" - separar por " - "
            var splitString = area.Split(" - ");
            if (splitString.Length < 2)
                return Json(new List<object>()); // Formato incorrecto

            var customerName = splitString[0].Trim();
            var areaName = splitString[1].Trim();

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

        // Método  para obtener defectos por área y categoría
        public JsonResult GetDefectByCategory(string area, string category)
        {
            if (string.IsNullOrEmpty(area))
                return Json(new List<object>()); // No hay área seleccionada

            // Formato: "CustomerName - AreaName" - separar por " - "
            var splitString = area.Split(" - ");
            if (splitString.Length < 2)
                return Json(new List<object>()); // Formato incorrecto

            var customerName = splitString[0].Trim();
            var areaName = splitString[1].Trim();

            // Obtener el AreaID del área seleccionada
            int? areaId = _context.Areas
                .Where(a => a.CustomerName == customerName && a.AreaName == areaName)
                .Select(a => (int?)a.AreaId)
                .FirstOrDefault();

            if (!areaId.HasValue)
                return Json(new List<object>());

            var query = _context.Defects.Where(d => d.AreaId == areaId);

            // Si se especifica una categoría, filtrar por ella
            if (!string.IsNullOrEmpty(category))
            {
                // Obtener el DefectCategoryID de la categoría seleccionada
                var defectCategoryId = _context.DefectsCategories
                    .Where(dc => dc.DefectCategoryName == category)
                    .Select(dc => (int?)dc.DefectCategoryId)
                    .FirstOrDefault();

                if (defectCategoryId.HasValue)
                {
                    query = query.Where(d => d.DefectCategoryId == defectCategoryId);
                }
            }

            // Obtener los defectos
            var defects = query
                .Select(d => d.DefectName)
                .Distinct()
                .ToList();

            // Agregar manualmente la opción "Todos los defectos"
            var result = new List<object>
            {
                new { Value = "", Text = "Todos los defectos" }
            };

            result.AddRange(defects.Select(d => new { Value = d, Text = d }));

            return Json(result);
        }

        public IActionResult AddQualityParamModal()
        {
            return PartialView("Parameters");
        }
        public async Task<JsonResult> GetProgramsByArea(string area)
        {
            try
            {
                if (string.IsNullOrEmpty(area))
                    return Json(new List<object>()); // No hay área seleccionada

                using var client = new HttpClient();
                client.BaseAddress = new Uri("http://192.168.1.1:9091"); // Ajusta el puerto según tu configuración
                client.Timeout = TimeSpan.FromSeconds(30); // Timeout de 30 segundos

                // Llamada al endpoint externo
                var response = await client.GetAsync($"/GetProgram?area={Uri.EscapeDataString(area)}");

                if (response.IsSuccessStatusCode)
                {
                    // Deserializamos la respuesta
                    var programs = await response.Content.ReadFromJsonAsync<List<dynamic>>();

                    if (programs != null && programs.Any())
                    {
                        // Crear lista con opción "Todos los programas" al inicio
                        var result = new List<object>
                {
                    new { id = "", program = "Todos los programas" }
                };

                        // Agregar los programas obtenidos de la API
                        result.AddRange(programs.Select(p => new
                        {
                            id = p.GetProperty("id").ToString(),
                            program = p.GetProperty("program").ToString(),
                            Type = p.GetProperty("type").ToString()
                        }));

                        return Json(result);
                    }
                    else
                    {
                        // No se encontraron programas para el área
                        return Json(new List<object>
                {
                    new { id = "", program = "No hay programas disponibles" }
                });
                    }
                }
                else
                {
                    // Error en la respuesta de la API
                    Console.WriteLine($"Error al obtener programas: {response.StatusCode} - {response.ReasonPhrase}");
                    return Json(new List<object>
            {
                new { id = "", program = "Error al cargar programas" }
            });
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Error de conexión HTTP
                Console.WriteLine($"Error de conexión al obtener programas: {httpEx.Message}");
                return Json(new List<object>
        {
            new { id = "", program = "Error de conexión" }
        });
            }
            catch (Exception ex)
            {
                // Error general
                Console.WriteLine($"Error general al obtener programas: {ex.Message}");
                return Json(new List<object>
        {
            new { id = "", program = "Error al cargar programas" }
        });
            }
        }



    }
}
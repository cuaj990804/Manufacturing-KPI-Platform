using GDIKPI.Data;
using GDIKPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Controllers
{
    public class DashboardProductionController : Controller
    {
        private readonly KpisContext _context; // Usa tu DbContext existente

        public DashboardProductionController(KpisContext context)
        {
            _context = context;
        }
        public ActionResult Index(int? areaId)
        {
            // Si se proporciona areaId, verificar que el usuario tenga acceso a esa área
            if (areaId.HasValue)
            {
                var area = _context.Areas.FirstOrDefault(a => a.AreaId == areaId.Value);
                if (area != null)
                {
                    ViewBag.AreaId = areaId.Value;
                    ViewBag.AreaName = $"{area.CustomerName} - {area.AreaName}";
                    ViewBag.CustomerName = area.CustomerName;
                    ViewBag.AreaNameOnly = area.AreaName;
                }
                else
                {
                    // Si el área no existe, redirigir a error o mostrar mensaje
                    ViewBag.ErrorMessage = "Área no encontrada";
                }
            }
            else
            {
                
                var areas = _context.Areas
                    .Select(a => new {
                        a.AreaId,
                        a.AreaName,
                        a.CustomerName
                    })
                    .ToList();

                ViewBag.Areas = areas; 
                ViewBag.AreaName = "Todas las áreas";
            }

            return View();
        }
   
        public ActionResult Test()
        {
            return View();
        }

      




    }
}

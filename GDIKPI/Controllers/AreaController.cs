using GDIKPI.Data;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Controllers
{
    public class AreaController : Controller
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;

        public AreaController(KpisContext context, PermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }
        public IActionResult Index()
        {
            var areas = _context.Areas
                .Select(a => new
                {
                    a.AreaId,
                    DisplayName = a.AreaName + " - " + a.CustomerName
                })
                .ToList();
            var areasFilter = _context.Areas
                .Select(a => new { AreaName = a.AreaName })
                .Distinct()
                .ToList();


            var customersFilter = _context.Areas
                .Select(a => new
                {
                    CustomerName = a.CustomerName
                })
                .Distinct()
                .ToList();

            // Usar el mismo formato que en LoadEditForm para consistencia
            var users = _context.Users
            .Where(u => u.Role == "supervisor" && u.Area == "production")
            .Select(u => new
            {
                u.EmployeeNumber,
                DisplayName = u.Name 
            })
            .ToList();


            ViewBag.Areaslist = new SelectList(areas, "AreaId", "DisplayName");
            ViewBag.SupervisorsList = new SelectList(users, "EmployeeNumber", "DisplayName");
            ViewBag.CustomersListFilter = new SelectList(customersFilter, "CustomerName", "CustomerName");
            ViewBag.AreaListFilter = new SelectList(areasFilter, "AreaName", "AreaName");

            return View();
        }

        public IActionResult LoadEditForm(int lineNumber, string area, string customer)
        {
            // Corregido: obtener el AreaId del área cuyo CustomerName coincide
            var areaId = _context.Areas
                .Where(a => a.AreaName == area && a.CustomerName == customer)
                .Select(a => a.AreaId)
                .FirstOrDefault();

            // Buscar la línea por número
            var line = _context.ProductionLines.Where(p => p.AreaId == areaId && p.LineNumber == lineNumber)
                .FirstOrDefault(p => p.LineNumber == lineNumber);

            // Validar si no existe la línea
            if (line == null)
            {
                return BadRequest($"No se encontró una línea con número {lineNumber}.");
            }

            // Obtener descansos ordenados
            var breaks = _context.Breaks
                .Where(b => b.ProductionLinesId == line.ProductionLinesId)
                .OrderBy(b => b.BreakStart)
                .ToList();

            // Asignar descansos al ViewBag
            ViewBag.Break1Start = breaks.ElementAtOrDefault(0)?.BreakStart?.ToString("HH:mm");
            ViewBag.Break1End = breaks.ElementAtOrDefault(0)?.BreakEnd?.ToString("HH:mm");
            ViewBag.Break2Start = breaks.ElementAtOrDefault(1)?.BreakStart?.ToString("HH:mm");
            ViewBag.Break2End = breaks.ElementAtOrDefault(1)?.BreakEnd?.ToString("HH:mm");

            // Obtener supervisor y permisos asignados a esta línea
            var supervisorPermissions = _context.UserLinePermissions
                .Where(ulp => ulp.ProductionLinesId == line.ProductionLinesId)
                .ToList();

            // Obtener el supervisor (asumiendo que todos los registros tienen el mismo EmployeeNumber para una línea)
            var supervisor = supervisorPermissions.FirstOrDefault()?.EmployeeNumber;

            // Obtener los IDs de permisos asignados
            var assignedPermissions = supervisorPermissions.Select(ulp => ulp.PermissionId).ToList();

            // Asignar supervisor y permisos al ViewBag
            ViewBag.Supervisor = supervisor ?? "0";
            ViewBag.AssignedPermissions = assignedPermissions;

            // Cargar lista de usuarios para el select del supervisor

            var users = _context.Users
                .Where(u => u.Role == "supervisor" && u.Area == "production")
                .Select(u => new
                {
                    u.EmployeeNumber,
                    DisplayName = u.Name
                })
                .ToList();
            ViewBag.UsersList = new SelectList(users, "EmployeeNumber", "DisplayName", supervisor);

            // Cargar áreas para el select
            var areas = _context.Areas
                .Select(a => new
                {
                    a.AreaId,
                    DisplayName = a.AreaName + " - " + a.CustomerName
                })
                .ToList();
            ViewBag.Areaslist = new SelectList(areas, "AreaId", "DisplayName");

            // Retornar vista parcial con el modelo
            return PartialView("EditProductionLine", line);
        }
        [HttpGet]
        public IActionResult LoadEditAreaForm()
        {
            var areas = _context.Areas
                .Select(a => new
                {
                    a.AreaId,
                    DisplayName = a.AreaName + " - " + a.CustomerName
                })
                .ToList();
            ViewBag.Areaslist = new SelectList(areas, "AreaId", "DisplayName");
            // Retornar vista parcial con el modelo
            return PartialView("EditAreas", new Area());
        }



        public IActionResult Areas()
        {
            return View();
        }
    }
}

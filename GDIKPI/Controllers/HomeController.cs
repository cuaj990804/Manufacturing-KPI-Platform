using GDIKPI.Data;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace GDIKPI.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;
        private readonly ModuleCardService _moduleCardService;

        public HomeController(KpisContext context, ILogger<HomeController> logger, PermissionService permissionService, ModuleCardService moduleCardService)
        {
            _logger = logger;
            _context = context;
            _permissionService = permissionService;
            _moduleCardService = moduleCardService;
        }

        public async Task<IActionResult> Index()
        {
            var employeeNumberClaim = User.FindFirst("EmployeeNumber")?.Value;
            var areaClaim = User.FindFirst("Area")?.Value;
            if (!int.TryParse(employeeNumberClaim, out int employeeNumber))
            {
                // No est� autenticado correctamente o falta claim, redirigir a login
                return RedirectToAction("Login", "Account");
            }

            // Obtener cards de módulos según permisos
            var moduleCards = await _moduleCardService.GetUserModuleCards(employeeNumber);

            return View(moduleCards);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

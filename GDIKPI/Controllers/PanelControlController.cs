using GDIKPI.Data;
using GDIKPI.Services;
using GDIKPI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

namespace GDIKPI.Controllers
{
    [Authorize(Roles = "admin")]
    public class PanelControlController : Controller
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;
        private readonly AuthService _authService;

        public PanelControlController(KpisContext context, PermissionService permissionService, AuthService authService)
        {
            _context = context;
            _permissionService = permissionService;
            _authService = authService;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Obtener el número de empleado actual desde los claims
            var employeeNumber = _authService.GetCurrentEmployeeNumber(User);

            if (employeeNumber == null)
                return Unauthorized();

            // 2. Obtener todos los usuarios
            var users = await _context.Users
                .OrderBy(u => u.EmployeeNumber)
                .ToListAsync();

            var viewModel = new PanelControlViewModel
            {
                Users = users
            };

            return View(viewModel);
        }
    }
}

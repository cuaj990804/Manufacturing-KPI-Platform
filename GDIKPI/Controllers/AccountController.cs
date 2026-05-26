using GDIKPI.Data;
using GDIKPI.DTO.Login;
using GDIKPI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GDIKPI.Controllers
{

    public class AccountController : Controller
    {
        private readonly AuthService _authService;
        private readonly KpisContext _context;

        public AccountController(AuthService authService, KpisContext context)
        {
            _authService = authService;
            _context = context;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDTO model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Verificar PRIMERO si es un usuario de línea de producción (formato L + número)
            if (model.EmployeeNumber.StartsWith("L", StringComparison.OrdinalIgnoreCase))
            {
                // Extraer el ProductionLineId del formato L+ID (ej: L123 -> 123)
                string lineIdString = model.EmployeeNumber.Substring(1);

                if (int.TryParse(lineIdString, out int productionLineId))
                {
                    // Validar que la línea de producción existe y está activa
                    var productionLine = await _context.ProductionLines
                        .Include(pl => pl.Area)
                        .FirstOrDefaultAsync(pl => pl.ProductionLinesId == productionLineId && pl.IsActive);

                    if (productionLine != null)
                    {
                        // Crear claims para la línea de producción
                        var lineClaims = new List<Claim>
                        {
                            new Claim("EmployeeNumber", model.EmployeeNumber),
                            new Claim(ClaimTypes.Name, $"Línea {productionLine.LineNumber}"),
                            new Claim(ClaimTypes.Role, "ProductionLine"),
                            new Claim("ProductionLineId", productionLineId.ToString()),
                            new Claim("LineNumber", productionLine.LineNumber?.ToString() ?? ""),
                            new Claim("Area", productionLine.Area?.AreaName ?? "")
                        };

                        var lineIdentity = new ClaimsIdentity(lineClaims, "Cookies");
                        var linePrincipal = new ClaimsPrincipal(lineIdentity);

                        await HttpContext.SignInAsync("Cookies", linePrincipal);

                        // Redirigir a ScannerProduction con el ID de la línea
                        return RedirectToAction("Index", "ScannerProduction", new { productionLineId = productionLineId });
                    }
                    else
                    {
                        ModelState.AddModelError("", $"La línea de producción L{productionLineId} no existe o no está activa.");
                        return View(model);
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Formato de línea inválido. Use L seguido del número de línea (ej: L123).");
                    return View(model);
                }
            }

            // Si NO es una línea, autenticar como usuario normal
            var user = await _authService.AuthenticateUser(model.EmployeeNumber);

            if (user == null)
            {
                ModelState.AddModelError("", "Número de empleado no válido.");
                return View(model);
            }

            var claims = await _authService.GetUserClaims(model.EmployeeNumber);
            var identity = new ClaimsIdentity(claims, "Cookies");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("Cookies", principal);

            // Verificar si es un usuario TV O si pertenece al área ZF-Forrado
            bool shouldRedirectToDashboard = user.Role?.ToLower() == "tv" ||
                (user.Area != null && user.Area.ToLower().Contains("zf") && user.Area.ToLower().Contains("forrado"));

            if (shouldRedirectToDashboard)
            {
                // Buscar el área ZF-Forrado en la base de datos
                var area = await _context.Areas
                    .Where(a => a.CustomerName.ToLower().Contains("zf") &&
                               a.AreaName.ToLower().Contains("forrado"))
                    .FirstOrDefaultAsync();

                if (area != null)
                {
                    return RedirectToAction("Index", "DashboardProduction", new { areaId = area.AreaId });
                }
                else
                {
                    // Si no encuentra el área ZF-Forrado específica, buscar por el área del usuario
                    var userArea = await _context.Areas
                        .Where(a => user.Area != null &&
                                   (a.CustomerName + " " + a.AreaName).ToLower().Contains(user.Area.ToLower()))
                        .FirstOrDefaultAsync();

                    if (userArea != null)
                    {
                        return RedirectToAction("Index", "DashboardProduction", new { areaId = userArea.AreaId });
                    }
                }
            }

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Logout()
        {
            var employeeNumber = _authService.GetCurrentEmployeeNumber(User);
            if (!string.IsNullOrEmpty(employeeNumber))
            {
                await _authService.LogoutUser(employeeNumber);
            }

            await HttpContext.SignOutAsync("Cookies");

            return RedirectToAction("Login");
        }
    }
}

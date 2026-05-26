using GDIKPI.Data;
using GDIKPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GDIKPI.Services
{
    public class AuthService
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;

        public AuthService(KpisContext context, PermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }

        // Autenticar usuario (por ejemplo, con número de empleado o ID de línea)
        public async Task<User?> AuthenticateUser(string employeeNumber)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.EmployeeNumber == employeeNumber);

            // Auditoría de login removida - solo se registrarán cambios en datos de producción

            return user;
        }

        // Crear claims para el usuario
        public async Task<List<Claim>> GetUserClaims(string employeeNumber)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.EmployeeNumber == employeeNumber);

            if (user == null)
                return new List<Claim>();

            var claims = new List<Claim>
            {
                new Claim("EmployeeNumber", employeeNumber),
                new Claim(ClaimTypes.Name, user.Name ?? ""),
                new Claim(ClaimTypes.Role, user.Role ?? ""),
                new Claim("Area", user.Area ?? "")

            };

            // Agregar permisos como claims
            var permissions = await _permissionService.GetUserPermissions(employeeNumber);
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("Permission", permission));
            }

            // Agregar líneas accesibles
            var accessibleLines = await _permissionService.GetAllAccessibleLines(employeeNumber, "View");
            foreach (var lineId in accessibleLines)
            {
                claims.Add(new Claim("AccessibleLine", lineId.ToString()));
            }

            return claims;
        }

        // Obtener información del usuario actual desde el contexto HTTP
        public string? GetCurrentEmployeeNumber(ClaimsPrincipal user)
        {
            var employeeNumberClaim = user.FindFirst("EmployeeNumber")?.Value;
            return employeeNumberClaim;
        }

        // Verificar si el usuario actual tiene un permiso específico
        public bool HasPermission(ClaimsPrincipal user, string permission)
        {
            return user.HasClaim("Permission", permission) ||
                   user.HasClaim("Role", "Admin") ||
                   user.HasClaim("Role", "Administrator");
        }

        // Obtener líneas accesibles del usuario actual
        public List<int> GetAccessibleLines(ClaimsPrincipal user)
        {
            return user.FindAll("AccessibleLine")
                .Select(c => int.Parse(c.Value))
                .ToList();
        }

        // Método para logout
        public async Task LogoutUser(string employeeNumber)
        {
            // Auditoría de logout removida - solo se registrarán cambios en datos de producción
            await Task.CompletedTask;
        }
    }
}
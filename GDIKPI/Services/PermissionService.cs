using GDIKPI.Data;
using GDIKPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace GDIKPI.Services
{
    public class PermissionService
    {
        private readonly KpisContext _context;

        public PermissionService(KpisContext context)
        {
            _context = context;
        }

        // Métodos para permisos de líneas de producción
        public async Task<bool> CanUserAccessLine(string employeeNumber, int productionLineId, string permission)
        {
            return await _context.UserLinePermissions
                .Include(ulp => ulp.Permission)
                .AnyAsync(ulp => ulp.EmployeeNumber == employeeNumber &&
                                 ulp.ProductionLinesId == productionLineId &&
                                 ulp.Permission.Name == permission);
        }

        public async Task<List<int>> GetAccessibleLines(string employeeNumber, string permission)
        {
            return await _context.UserLinePermissions
                .Include(ulp => ulp.Permission)
                .Where(ulp => ulp.EmployeeNumber == employeeNumber && ulp.Permission.Name == permission)
                .Select(ulp => ulp.ProductionLinesId)
                .ToListAsync();
        }
        public async Task<bool> HasAccessibleLines(string employeeNumber, string permission)
        {
            return await _context.UserLinePermissions
                .Include(ulp => ulp.Permission)
                .AnyAsync(ulp => ulp.EmployeeNumber == employeeNumber && ulp.Permission.Name == permission);
        }


        // Métodos para permisos de áreas
        public async Task<bool> CanUserAccessArea(string employeeNumber, int areaId, string permission)
        {
            return await _context.UserAreaPermissions
                .Include(uap => uap.Permission)
                .AnyAsync(uap => uap.EmployeeNumber == employeeNumber &&
                                 uap.AreaId == areaId &&
                                 uap.Permission.Name == permission);
        }

        public async Task<List<int>> GetAccessibleAreas(string employeeNumber, string permission)
        {
            return await _context.UserAreaPermissions
                .Include(uap => uap.Permission)
                .Where(uap => uap.EmployeeNumber == employeeNumber && uap.Permission.Name == permission)
                .Select(uap => uap.AreaId)
                .ToListAsync();
        }

        public async Task<List<int>> GetAccessibleAreasIncludingAdmin(string employeeNumber, string permission)
        {
            if (await IsUserAdmin(employeeNumber))
            {
                return await _context.Areas.Select(a => a.AreaId).ToListAsync();
            }

            return await GetAccessibleAreas(employeeNumber, permission);
        }

        // Verificar si puede acceder a una línea a través del área
        public async Task<bool> CanUserAccessLineViaArea(string employeeNumber, int productionLineId, string permission)
        {
            // Obtener el área de la línea de producción
            var line = await _context.ProductionLines
                .FirstOrDefaultAsync(pl => pl.ProductionLinesId == productionLineId);

            if (line?.AreaId == null) return false;

            // Verificar permisos en el área
            return await CanUserAccessArea(employeeNumber, line.AreaId.Value, permission);
        }

        // Método combinado: verificar acceso directo a línea O a través del área
        public async Task<bool> HasAccessToLine(string employeeNumber, int productionLineId, string permission)
        {
            // Verificar acceso directo a la línea
            var directAccess = await CanUserAccessLine(employeeNumber, productionLineId, permission);
            if (directAccess) return true;

            // Verificar acceso a través del área
            return await CanUserAccessLineViaArea(employeeNumber, productionLineId, permission);
        }

        // Obtener todas las líneas accesibles (solo directas, NO por área)
        public async Task<List<int>> GetAllAccessibleLines(string employeeNumber, string permission)
        {
            // Solo devolver líneas con acceso directo (UserLinePermissions)
            // NO incluir líneas por área para permitir control granular
            var directLines = await GetAccessibleLines(employeeNumber, permission);
            return directLines;
        }

        // Obtener todos los permisos de un usuario
        public async Task<List<string>> GetUserPermissions(string employeeNumber)
        {
            var linePermissions = await _context.UserLinePermissions
                .Include(ulp => ulp.Permission)
                .Where(ulp => ulp.EmployeeNumber == employeeNumber)
                .Select(ulp => ulp.Permission.Name)
                .ToListAsync();

            var areaPermissions = await _context.UserAreaPermissions
                .Include(uap => uap.Permission)
                .Where(uap => uap.EmployeeNumber == employeeNumber)
                .Select(uap => uap.Permission.Name)
                .ToListAsync();

            return linePermissions.Union(areaPermissions).Distinct().ToList();
        }

        // Verificar si es administrador (tiene todos los permisos)
        public async Task<bool> IsUserAdmin(string employeeNumber)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeNumber == employeeNumber);
            return user?.Role?.ToLower() == "admin" || user?.Role?.ToLower() == "administrator";
        }

        // Método para logging de auditoría
        public async Task LogUserAction(string employeeNumber, string actionType, string entityName, int entityId, string details = null)
        {
            var auditLog = new AuditLog
            {
                EmployeeNumber = employeeNumber,
                ActionType = actionType,
                EntityName = entityName,
                EntityId = entityId,
                Timestamp = DateTime.Now,
                Details = details
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
    }
}
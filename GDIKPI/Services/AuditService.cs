using GDIKPI.Data;
using GDIKPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GDIKPI.Services
{
    public class AuditService
    {
        private readonly KpisContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(KpisContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetCurrentEmployeeNumber()
        {
            var employeeNumberClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("EmployeeNumber")?.Value;
            return employeeNumberClaim ?? "0";
        }

        public async Task LogProductionDataAction(string actionType, int productionDataId, string details = null)
        {
            await LogAction(actionType, "ProductionData", productionDataId, details);
        }

        public async Task LogDefectAction(string actionType, int defectDataId, string details = null)
        {
            await LogAction(actionType, "DefectsData", defectDataId, details);
        }

        public async Task LogDowntimeAction(string actionType, int downtimeId, string details = null)
        {
            await LogAction(actionType, "DowntimeEvent", downtimeId, details);
        }

        public async Task LogAbsenteeismAction(string actionType, int absenteeismId, string details = null)
        {
            await LogAction(actionType, "Absenteeism", absenteeismId, details);
        }

        public async Task LogRejectionAction(string actionType, int rejectionId, string details = null)
        {
            await LogAction(actionType, "Rejection", rejectionId, details);
        }

        public async Task LogProductionLineAction(string actionType, int productionLineId, string details = null)
        {
            await LogAction(actionType, "ProductionLine", productionLineId, details);
        }

        private async Task LogAction(string actionType, string entityName, int entityId, string details = null)
        {
            var employeeNumber = GetCurrentEmployeeNumber();

            if (employeeNumber == "0")
                return; // No hay usuario autenticado

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

        // Método para obtener registros de auditoría por entidad
        public async Task<List<AuditLog>> GetAuditTrail(string entityName, int entityId)
        {
            return await _context.AuditLogs
                .Where(al => al.EntityName == entityName && al.EntityId == entityId)
                .OrderByDescending(al => al.Timestamp)
                .ToListAsync();
        }

        // Método para obtener auditoría por empleado
        public async Task<List<AuditLog>> GetEmployeeAuditTrail(string employeeNumber, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.AuditLogs
                .Where(al => al.EmployeeNumber == employeeNumber);

            if (fromDate.HasValue)
                query = query.Where(al => al.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(al => al.Timestamp <= toDate.Value);

            return await query
                .OrderByDescending(al => al.Timestamp)
                .ToListAsync();
        }
    }
}
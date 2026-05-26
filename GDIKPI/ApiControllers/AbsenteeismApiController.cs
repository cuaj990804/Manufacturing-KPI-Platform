using GDIKPI.Data;
using GDIKPI.DTO.ProductionDatum;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AbsenteeismApiController : ControllerBase
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;
        private readonly AuditService _auditService;

        public AbsenteeismApiController(KpisContext context, PermissionService permissionService, AuditService auditService)
        {
            _context = context;
            _permissionService = permissionService;
            _auditService = auditService;
        }
        [HttpGet]
        public async Task<IActionResult> GetStatus(int lineid)
        {
            try
            {
                // Validar que lineid sea válido
                if (lineid <= 0)
                {
                    return BadRequest(new { error = "Line ID debe ser mayor que 0" });
                }

                // Verificar que la línea de producción existe
                var lineExists = await _context.ProductionLines
                    .AnyAsync(pl => pl.ProductionLinesId == lineid);

                if (!lineExists)
                {
                    return NotFound(new { error = "Línea de producción no encontrada" });
                }

                // Obtener todos los registros de ausentismo para la línea de producción de hoy
                var today = DateOnly.FromDateTime(DateTime.Now);
                var absenteeismRecords = await _context.Absenteeisms
                    .Where(a => a.ProductionLinesId == lineid && a.AbsenteeismDate == today)
                    .ToListAsync();

                if (!absenteeismRecords.Any())
                {
                    // No existen registros - permitir crear nuevos
                    return Ok(new
                    {
                        hasRecords = false,
                        isNewRecord = true,
                        absenteeismData = new Dictionary<string, int>()
                    });
                }

                // Existen registros - procesar y devolver los datos existentes
                var absenteeismData = absenteeismRecords.ToDictionary(
                    a => a.AbsenteeismCategory, // o el campo que represente el tipo de ausentismo
                    a => a.AbsenteeismQuantity // o el campo que represente la cantidad
                );

                return Ok(new
                {
                    hasRecords = true,
                    isNewRecord = false,
                    absenteeismData = absenteeismData
                });
            }
            catch (Exception ex)
            {
                // Log del error (considera usar ILogger)
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        [HttpPost("AbsenteeismCreate")]
        public async Task<IActionResult> AbsenteeismCreate([FromBody] AbsenteeismDTO request)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);

                // Obtener el número de línea para la auditoría
                var lineNumber = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == request.ProductionLineId)
                    .Select(pl => pl.LineNumber)
                    .FirstOrDefaultAsync();

                // Eliminar registros existentes para esta línea y fecha
                var existingRecords = _context.Absenteeisms
                    .Where(a => a.ProductionLinesId == request.ProductionLineId && a.AbsenteeismDate == today)
                    .ToList();

                bool hasExistingRecords = existingRecords.Any();
                var existingSummary = string.Join(", ", existingRecords.Select(r => $"{r.AbsenteeismCategory}: {r.AbsenteeismQuantity}"));

                _context.Absenteeisms.RemoveRange(existingRecords);

                // Crear nuevos registros solo para las categorías con cantidad > 0
                var newRecords = new List<Absenteeism>();
                foreach (var category in request.Categories.Where(c => c.CategoryQuantity > 0))
                {
                    var newRecord = new Absenteeism
                    {
                        AbsenteeismCategory = category.Category,
                        AbsenteeismQuantity = category.CategoryQuantity,
                        AbsenteeismDate = today,
                        EmployeeNumber = request.EmployeeNumber.ToString(),
                        ProductionLinesId = request.ProductionLineId
                    };
                    _context.Absenteeisms.Add(newRecord);
                    newRecords.Add(newRecord);
                }

                await _context.SaveChangesAsync();

                // Preparar detalles para auditoría
                var newSummary = string.Join(", ", newRecords.Select(r => $"{r.AbsenteeismCategory}: {r.AbsenteeismQuantity}"));
                var actionType = hasExistingRecords ? "UPDATE" : "CREATE";
                var details = hasExistingRecords
                    ? $"Línea {lineNumber}: Actualización de ausentismo [{existingSummary}] → [{newSummary}]"
                    : $"Línea {lineNumber}: Registro de ausentismo [{newSummary}]";

                // Registrar auditoría consolidada (usar el ID del primer registro o 0 si no hay registros)
                var entityId = newRecords.FirstOrDefault()?.AbsenteeismId ?? 0;
                await _auditService.LogAbsenteeismAction(actionType, entityId, details);

                return Ok(new { success = true, message = "Registros guardados correctamente" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }



    }
}


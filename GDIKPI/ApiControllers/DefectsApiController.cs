using GDIKPI.Data;
using GDIKPI.DTO.Defetcs;
using GDIKPI.Hubs;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DefectsApiController : ControllerBase
    {
        private readonly KpisContext _context;
        private readonly AuditService _auditService;
        private readonly IHubContext<DashboardHub> _hubContext;

        public DefectsApiController(KpisContext context, AuditService auditService, IHubContext<DashboardHub> hubContext)
        {
            _context = context;
            _auditService = auditService;
            _hubContext = hubContext;
        }

        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            var categories = _context.DefectsCategories
                .Select(c => new
                {
                    Id = c.DefectCategoryId,
                    Label = c.DefectCategoryName,
                    Icon = "🔴"
                })
                .OrderBy(c => c.Label)
                .ToList();

            return Ok(categories);
        }

        [HttpGet("defects")]
        public IActionResult GetDefects(int lineId)
        {
            var areaId = _context.ProductionLines
                .Where(d => d.ProductionLinesId == lineId)
                .Select(p => p.AreaId)
                .FirstOrDefault();

            var defects = _context.Defects
                .Where(d => d.AreaId == areaId)
                .Select(d => new
                {
                    id = d.DefectId,
                    Name = d.DefectName,
                    CategoryId = d.DefectCategoryId
                })
                .OrderBy(d => d.Name)
                .ToList();

            return Ok(defects);
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterDefects([FromBody] DefectRegistrationDTO model)
        {
            // Validar que el ProgramId esté presente
            if (model.ProgramId <= 0)
            {
                return BadRequest("El ProgramId es requerido y debe ser mayor a 0.");
            }

            var employeeNumberClaim = User.FindFirst("EmployeeNumber")?.Value;
            if (string.IsNullOrEmpty(employeeNumberClaim))
                return Unauthorized("Usuario no autenticado correctamente.");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var intervalParts = model.TimeInterval.Split('-');
                if (intervalParts.Length != 2 ||
                    !DateTime.TryParse(intervalParts[0], out var startTime) ||
                    !DateTime.TryParse(intervalParts[1], out var endTime))
                {
                    return BadRequest("El formato de TimeInterval es inválido. Debe ser 'yyyy-MM-dd HH:mm:ss-yyyy-MM-dd HH:mm:ss'.");
                }

                var rejection = new Rejection
                {
                    ProductionLinesId = model.ProductionLineId,
                    ProgramId = model.ProgramId, // NUEVO CAMPO AGREGADO
                    ProgramDescription = model.ProgramDescription,
                    Category = "Producción",
                    EmployeeNumber = employeeNumberClaim,
                    DefectQuantity = model.TotalVolantes,
                    StartTime = startTime,
                    EndTime = endTime
                };

                _context.Rejections.Add(rejection);
                await _context.SaveChangesAsync();

                var defectsDataList = model.Defects.Select(defect => new DefectsDatum
                {
                    RejectionId = rejection.RejectionId,
                    ProductionLinesId = model.ProductionLineId,

                    Category = "Quality",
                    EmployeeNumber = employeeNumberClaim,
                    DefectId = defect.DefectId,
                    DefectQuantity = defect.Quantity,
                    StartTime = rejection.StartTime,
                    EndTime = rejection.EndTime
                }).ToList();

                _context.DefectsData.AddRange(defectsDataList);
                await _context.SaveChangesAsync();

                // Obtener la información de la línea para la auditoría
                var lineInfo = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == model.ProductionLineId)
                    .Select(pl => new { pl.LineNumber, pl.LineName })
                    .FirstOrDefaultAsync();

                var lineDisplay = !string.IsNullOrWhiteSpace(lineInfo?.LineName)
                    ? lineInfo.LineName
                    : $"Línea {lineInfo?.LineNumber}";

                var lineNumber = lineInfo?.LineNumber;

                // Crear resumen de defectos
                var defectsSummary = string.Join(", ", model.Defects.Select(d =>
                {
                    var defectName = _context.Defects
                        .Where(def => def.DefectId == d.DefectId)
                        .Select(def => def.DefectName)
                        .FirstOrDefault();
                    return $"{defectName}: {d.Quantity}";
                }));

                // Registrar auditoría
                await _auditService.LogDefectAction("CREATE", rejection.RejectionId,
                    $"{lineDisplay}: Registro de defectos - Total volantes: {model.TotalVolantes}, Defectos: [{defectsSummary}], Programa: {model.ProgramId}");

                // Obtener el área de la línea de producción para notificar al dashboard
                var areaId = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == model.ProductionLineId)
                    .Select(pl => pl.AreaId)
                    .FirstOrDefaultAsync();

                // Enviar notificación SignalR al dashboard del área específica
                if (areaId.HasValue)
                {
                    await _hubContext.Clients.Group($"Dashboard_{areaId.Value}")
                        .SendAsync("RejectDataUpdated", new {
                            areaId = areaId.Value,
                            type = "CREATE",
                            lineNumber = lineNumber,
                            totalRejects = model.TotalVolantes,
                            defectsCount = model.Defects.Count
                        });
                }

                await transaction.CommitAsync();

                return Ok("Registro completado.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // Captura la cadena completa de excepciones
                var errorMessage = ex.Message;
                var innerMessage = ex.InnerException?.Message ?? "Sin inner exception";
                var innerInnerMessage = ex.InnerException?.InnerException?.Message ?? "Sin inner-inner exception";

                return BadRequest($"Error: {errorMessage} | Inner: {innerMessage} | Inner2: {innerInnerMessage}");
            }
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateDefects([FromBody] DefectRegistrationDTO model)
        {
            // Validar que el ProgramId esté presente
            if (model.ProgramId <= 0)
            {
                return BadRequest("El ProgramId es requerido y debe ser mayor a 0.");
            }

            var employeeNumberClaim = User.FindFirst("EmployeeNumber")?.Value;
            if (string.IsNullOrEmpty(employeeNumberClaim))
                return Unauthorized("Usuario no autenticado correctamente.");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var intervalParts = model.TimeInterval.Split('-');
                if (intervalParts.Length != 2 ||
                    !DateTime.TryParse(intervalParts[0], out var startTime) ||
                    !DateTime.TryParse(intervalParts[1], out var endTime))
                {
                    return BadRequest("El formato de TimeInterval es inválido. Debe ser 'yyyy-MM-dd HH:mm:ss-yyyy-MM-dd HH:mm:ss'.");
                }

                // Buscar el registro existente con ProgramId incluido
                var existingRejection = await _context.Rejections
                    .FirstOrDefaultAsync(r => r.ProductionLinesId == model.ProductionLineId
                                           && r.StartTime >= startTime
                                           && r.EndTime <= endTime);

                if (existingRejection == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound("No se encontró un registro de rechazo existente para actualizar.");
                }

                // Obtener la información de la línea y los valores anteriores para la auditoría
                var lineInfo = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == model.ProductionLineId)
                    .Select(pl => new { pl.LineNumber, pl.LineName })
                    .FirstOrDefaultAsync();

                var lineDisplay = !string.IsNullOrWhiteSpace(lineInfo?.LineName)
                    ? lineInfo.LineName
                    : $"Línea {lineInfo?.LineNumber}";

                var lineNumber = lineInfo?.LineNumber;

                var oldDefectsData = await _context.DefectsData
                    .Where(dd => dd.RejectionId == existingRejection.RejectionId)
                    .Join(_context.Defects,
                          dd => dd.DefectId,
                          d => d.DefectId,
                          (dd, d) => new { DefectName = d.DefectName, Quantity = dd.DefectQuantity })
                    .ToListAsync();

                var oldDefectsSummary = string.Join(", ", oldDefectsData.Select(d => $"{d.DefectName}: {d.Quantity}"));
                var oldTotalVolantes = existingRejection.DefectQuantity;

                // Actualizar el registro de rechazo
                existingRejection.DefectQuantity = model.TotalVolantes;
                existingRejection.EmployeeNumber = employeeNumberClaim;
                existingRejection.ProgramId = model.ProgramId;
                existingRejection.ProgramDescription = model.ProgramDescription; 

                _context.Rejections.Update(existingRejection);

                // Eliminar los registros de defectos existentes
                var existingDefectsData = await _context.DefectsData
                    .Where(dd => dd.RejectionId == existingRejection.RejectionId)
                    .ToListAsync();

                _context.DefectsData.RemoveRange(existingDefectsData);

                // Crear los nuevos registros de defectos
                var newDefectsDataList = model.Defects.Select(defect => new DefectsDatum
                {
                    RejectionId = existingRejection.RejectionId,
                    ProductionLinesId = model.ProductionLineId,
                    // ProgramId NO existe en DefectsData - se quita esta línea
                    Category = "Quality",
                    EmployeeNumber = employeeNumberClaim,
                    DefectId = defect.DefectId,
                    DefectQuantity = defect.Quantity,
                    StartTime = existingRejection.StartTime,
                    EndTime = existingRejection.EndTime
                }).ToList();

                _context.DefectsData.AddRange(newDefectsDataList);
                await _context.SaveChangesAsync();

                // Crear resumen de nuevos defectos para la auditoría
                var newDefectsSummary = string.Join(", ", model.Defects.Select(d =>
                {
                    var defectName = _context.Defects
                        .Where(def => def.DefectId == d.DefectId)
                        .Select(def => def.DefectName)
                        .FirstOrDefault();
                    return $"{defectName}: {d.Quantity}";
                }));

                // Registrar auditoría
                await _auditService.LogDefectAction("UPDATE", existingRejection.RejectionId,
                    $"{lineDisplay}: Actualización de defectos - Anterior [Volantes: {oldTotalVolantes}, Defectos: {oldDefectsSummary}] → Nuevo [Volantes: {model.TotalVolantes}, Defectos: {newDefectsSummary}], Programa: {model.ProgramId}");

                // Obtener el área de la línea de producción para notificar al dashboard
                var areaId = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == model.ProductionLineId)
                    .Select(pl => pl.AreaId)
                    .FirstOrDefaultAsync();

                // Enviar notificación SignalR al dashboard del área específica
                if (areaId.HasValue)
                {
                    await _hubContext.Clients.Group($"Dashboard_{areaId.Value}")
                        .SendAsync("RejectDataUpdated", new {
                            areaId = areaId.Value,
                            type = "UPDATE",
                            lineNumber = lineNumber,
                            totalRejects = model.TotalVolantes,
                            defectsCount = model.Defects.Count
                        });
                }

                await transaction.CommitAsync();

                return Ok("Registro actualizado correctamente.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest($"Error al actualizar: {ex.Message}");
            }
        }

        [HttpGet("GetDefectsData")]
        public IActionResult GetDefectsData(int lineid, DateTime startTime, DateTime endTime, int programId)
        {
            try
            {
                // Buscar registro con ProgramId incluido
                var rejection = _context.Rejections
                    .Where(r => r.ProductionLinesId == lineid
                               && r.ProgramId == programId
                               && r.StartTime >= startTime
                               && r.EndTime <= endTime)
                    .FirstOrDefault();


                if (rejection == null)
                {
                    return Ok(new { hasRecords = false, defectsData = (object)null, totalVolantes = 0 });
                }

                var defectsData = _context.DefectsData
                    .Where(dd => dd.RejectionId == rejection.RejectionId)
                    .Join(_context.Defects,
                          dd => dd.DefectId,
                          d => d.DefectId,
                          (dd, d) => new
                          {
                              defectId = dd.DefectId,
                              defectName = d.DefectName,
                              quantity = dd.DefectQuantity,
                              categoryId = d.DefectCategoryId
                          })
                    .ToList();

                return Ok(new
                {
                    hasRecords = true,
                    defectsData = defectsData,
                    totalVolantes = rejection.DefectQuantity,
                    programId = rejection.ProgramId,
                    programDescription = rejection.ProgramDescription
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
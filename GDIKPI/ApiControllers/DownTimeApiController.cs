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
    public class DownTimeApiController : ControllerBase
    {
        private readonly KpisContext _context;
        private readonly PermissionService _permissionService;
        private readonly AuditService _auditService;

        public DownTimeApiController(KpisContext context, PermissionService permissionService, AuditService auditService)
        {
            _context = context;
            _permissionService = permissionService;
            _auditService = auditService;
        }
        [HttpGet]
        public IActionResult GetStatus(int lineid)
        {
            var downtime = _context.DowntimeEvents
         .Where(dt => dt.ProductionLinesId == lineid && dt.Status == "Open")
         .OrderByDescending(dt => dt.StartTime)
         .FirstOrDefault();

            if (downtime == null)
            {
                return Ok(new { hasOpen = false });
            }

            return Ok(new
            {
                hasOpen = true,
                startTime = downtime.StartTime.ToString("yyyy-MM-ddTHH:mm"), // formato compatible con input datetime-local
                openedBy = downtime.OpenedBy,
                category=downtime.DowntimeCategory,
                reason = downtime.Reason
            });
        }
        [HttpPost("register")]
        public async Task<IActionResult> RegisterDowntime([FromBody] DowntimeCreateDTO dto)
        {
            try
            {
                Console.WriteLine($"📥 DTO recibido: ProductionLinesId={dto?.ProductionLinesId}, StartTime={dto?.StartTime}, Category={dto?.Category}, Reason={dto?.Reason}");

                var employeeNumber = HttpContext.User.FindFirst("EmployeeNumber")?.Value;
                Console.WriteLine($"👤 Employee Number: {employeeNumber}");

                if (string.IsNullOrEmpty(employeeNumber))
                    return Unauthorized("Empleado no autenticado.");

                if (!ModelState.IsValid)
                {
                    Console.WriteLine("❌ ModelState no válido");
                    foreach (var error in ModelState)
                    {
                        Console.WriteLine($"Campo: {error.Key}, Errores: {string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage))}");
                    }

                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) });
                    return BadRequest(new { message = "Datos inválidos", errors });
                }

                // Validación específica de fecha
                if (dto.StartTime == default(DateTime))
                    return BadRequest("La fecha de inicio es requerida.");

                // Convertir a hora local si viene en UTC (opcional, dependiendo de tu zona horaria)
                var startTime = dto.StartTime;

                // Validar rango de fecha más realista
                var minDate = new DateTime(2020, 1, 1);
                var maxDate = DateTime.Now.AddYears(1);

                


                var downtime = new DowntimeEvent
                {
                    ProductionLinesId = dto.ProductionLinesId,
                    StartTime = startTime,
                    Reason = dto.Reason?.Trim(),
                    DowntimeCategory = dto.Category?.Trim(),
                    OpenedBy = employeeNumber,
                    Status = "Open",
             

                };

                _context.DowntimeEvents.Add(downtime);
                await _context.SaveChangesAsync();

                // Obtener el número de línea para la auditoría
                var lineNumber = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == dto.ProductionLinesId)
                    .Select(pl => pl.LineNumber)
                    .FirstOrDefaultAsync();

                // Registrar auditoría
                var reasonText = !string.IsNullOrEmpty(dto.Reason) ? $", Razón: {dto.Reason}" : "";
                await _auditService.LogDowntimeAction("CREATE", downtime.DowntimeId,
                    $"Línea {lineNumber}: Registro de tiempo muerto - Categoría: {dto.Category}{reasonText}, Inicio: {startTime:yyyy-MM-dd HH:mm:ss}");

                return Ok(new
                {
                    success = true,
                    message = "Tiempo muerto registrado correctamente.",
                    startTime = downtime.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (DbUpdateException dbEx)
            {
                return StatusCode(500, $"Error de base de datos: {dbEx.InnerException?.Message ?? dbEx.Message}");
            }
            catch (Exception ex)
            {
                // Log el error completo para debugging
                Console.WriteLine($"Error en RegisterDowntime: {ex}");
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }
        [HttpPost("close")]
        public async Task<IActionResult> CloseDowntime([FromBody] DowntimeUpdateDTO dto)
        {
            try
            {
              

                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) });
                    return BadRequest(new { message = "Datos inválidos", errors });
                }

                

                // Buscar el registro de downtime abierto para esta línea de producción
                var existingDowntime = await _context.DowntimeEvents
                    .FirstOrDefaultAsync(d => d.ProductionLinesId == dto.ProductionLinesId &&
                                            d.Status == "Open");


                // Convertir a hora local si viene en UTC (opcional, dependiendo de tu zona horaria)
                var endTime = dto.EndTime;

                // Actualizar el registro existente
                existingDowntime.EndTime = endTime;
                existingDowntime.ClosedBy = dto.ClosedBy;
                existingDowntime.Status = "Close";

                // Marcar la entidad como modificada
                _context.DowntimeEvents.Update(existingDowntime);
                await _context.SaveChangesAsync();

                // Obtener el número de línea para la auditoría
                var lineNumber = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == dto.ProductionLinesId)
                    .Select(pl => pl.LineNumber)
                    .FirstOrDefaultAsync();

                var duration = existingDowntime.EndTime.HasValue ?
                    (existingDowntime.EndTime.Value - existingDowntime.StartTime).TotalMinutes : 0;

                // Registrar auditoría
                await _auditService.LogDowntimeAction("CLOSE", existingDowntime.DowntimeId,
                    $"Línea {lineNumber}: Cierre de tiempo muerto - Categoría: {existingDowntime.DowntimeCategory}, Duración: {duration:F1} minutos, Cerrado: {endTime:yyyy-MM-dd HH:mm:ss}");

                return Ok(new
                {
                    success = true,
                    message = "Tiempo muerto cerrado correctamente.",
                    startTime = existingDowntime.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    endTime = existingDowntime.EndTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                    duration = duration
                });
            }
            catch (DbUpdateException dbEx)
            {
                return StatusCode(500, $"Error de base de datos: {dbEx.InnerException?.Message ?? dbEx.Message}");
            }
            catch (Exception ex)
            {
                // Log el error completo para debugging
                Console.WriteLine($"Error en CloseDowntime: {ex}");
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }



    }
}

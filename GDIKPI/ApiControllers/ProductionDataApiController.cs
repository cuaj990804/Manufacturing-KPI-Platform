using GDIKPI.Data;
using GDIKPI.DTO.ProductionDatum;
using GDIKPI.Hubs;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductionDataApiController : ControllerBase
    {
        private readonly KpisContext _context; // Usa tu DbContext existente
        private readonly AuditService _auditService;
        private readonly IHubContext<DashboardHub> _hubContext;

        // Cache para prevenir duplicados recientes (en memoria)
        private static readonly Dictionary<string, DateTime> _recentCreations = new();
        private static readonly object _cacheLock = new object();

        public ProductionDataApiController(KpisContext context, AuditService auditService, IHubContext<DashboardHub> hubContext)
        {
            _context = context;
            _auditService = auditService;
            _hubContext = hubContext;
        }
        [HttpPost("SetTable")]
        public IActionResult SetTable()
        {
            try
            {
                int draw = Convert.ToInt32(Request.Form["draw"]);
                int start = Convert.ToInt32(Request.Form["start"]);
                int length = Convert.ToInt32(Request.Form["length"]);

                // Leer areaId que envía el cliente
                int areaId = 0;
                var areaIdStr = Request.Form["areaId"].FirstOrDefault();
                if (!string.IsNullOrEmpty(areaIdStr))
                    int.TryParse(areaIdStr, out areaId);

                // Query base con Include para ProductionLines
                var query = _context.ProductionData.Include(p => p.ProductionLines).AsQueryable();

                // Filtrar por areaId si se envió
                if (areaId > 0)
                    query = query.Where(p => p.ProductionLines.AreaId == areaId);

                int totalRecords = query.Count();

                var dataQuery = query
                    .OrderByDescending(p => p.ProductionDate)
                    .Skip(start)
                    .Take(length);

                var data = dataQuery
                    .Select(p => new
                    {
                        productionDate = p.ProductionDate.ToString("yyyy-MM-dd"),
                        startHour = p.StartHour.ToString(@"hh\:mm"),
                        endHour = p.EndHour.ToString(@"hh\:mm"),
                        producedPieces = p.ProducedPieces,
                        line = p.ProductionLines.LineNumber,
                        productionId = p.ProductionId
                    }).ToList();

                return Ok(new
                {
                    draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = totalRecords,
                    data
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("GetDataByDate")]
        public async Task<IActionResult> GetDataByDate()
        {
            try
            {
                int draw = Convert.ToInt32(Request.Form["draw"]);
                int start = Convert.ToInt32(Request.Form["start"]);
                int length = Convert.ToInt32(Request.Form["length"]);

                // Leer areaId que envía el cliente
                int areaId = 0;
                var areaIdStr = Request.Form["areaId"].FirstOrDefault();
                if (!string.IsNullOrEmpty(areaIdStr))
                    int.TryParse(areaIdStr, out areaId);

                // Leer fecha seleccionada
                DateOnly? selectedDate = null;
                var dateStr = Request.Form["selectedDate"].FirstOrDefault();
                if (!string.IsNullOrEmpty(dateStr) && DateOnly.TryParse(dateStr, out var date))
                    selectedDate = date;

                // Leer filtro de línea
                var lineFilterStr = Request.Form["lineFilter"].FirstOrDefault();
                int? lineFilter = null;
                if (!string.IsNullOrEmpty(lineFilterStr))
                {
                    // Intentar parsear directamente como número
                    if (int.TryParse(lineFilterStr, out var lineNumber))
                    {
                        // Buscar la línea por número
                        var lineId = await _context.ProductionLines
                            .Where(pl => pl.LineNumber == lineNumber && pl.AreaId == areaId)
                            .Select(pl => pl.ProductionLinesId)
                            .FirstOrDefaultAsync();

                        if (lineId > 0)
                            lineFilter = lineId;
                    }
                }

                // Query base con Include para ProductionLines
                var query = _context.ProductionData.Include(p => p.ProductionLines).AsQueryable();

                // Filtrar por areaId si se envió
                if (areaId > 0)
                    query = query.Where(p => p.ProductionLines.AreaId == areaId);

                // Filtrar por fecha si se envió
                if (selectedDate.HasValue)
                    query = query.Where(p => p.ProductionDate == selectedDate.Value);

                // Filtrar por línea si se envió
                if (lineFilter.HasValue)
                    query = query.Where(p => p.ProductionLinesId == lineFilter.Value);

                int totalRecords = query.Count();

                var dataQuery = query
                    .OrderByDescending(p => p.ProductionDate)
                    .ThenByDescending(p => p.StartHour)
                    .Skip(start)
                    .Take(length);

                var data = dataQuery
                    .Select(p => new
                    {
                        productionDate = p.ProductionDate.ToString("yyyy-MM-dd"),
                        startHour = p.StartHour.ToString(@"hh\:mm"),
                        endHour = p.EndHour.ToString(@"hh\:mm"),
                        producedPieces = p.ProducedPieces,
                        rejectedPieces = p.RejectedPieces,
                        line = p.ProductionLines.LineNumber,
                        programId = p.ProgramId,
                        programDescription = p.ProgramDescription,
                        productionId = p.ProductionId
                    }).ToList();

                return Ok(new
                {
                    draw,
                    recordsTotal = totalRecords,
                    recordsFiltered = totalRecords,
                    data
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductionDatumCreateDTO dto)
        {
            // Crear clave única para el cache de duplicados recientes (SIN ProducedPieces)
            string cacheKey = $"{dto.ProductionLinesId}_{dto.ProductionDate}_{dto.StartHour}_{dto.EndHour}_{dto.ProgramId}";

            // Verificar cache de duplicados recientes (protección adicional)
            lock (_cacheLock)
            {
                // Limpiar entradas antiguas del cache (más de 10 segundos)
                var cutoffTime = DateTime.Now.AddSeconds(-10);
                var keysToRemove = _recentCreations.Where(kvp => kvp.Value < cutoffTime).Select(kvp => kvp.Key).ToList();
                foreach (var key in keysToRemove)
                {
                    _recentCreations.Remove(key);
                }

                // Verificar si este registro fue creado recientemente
                if (_recentCreations.ContainsKey(cacheKey))
                {
                    return Conflict(new { message = "Registro duplicado detectado. Intento muy reciente." });
                }

                // Agregar al cache
                _recentCreations[cacheKey] = DateTime.Now;
            }

            // Usar transacción para garantizar atomicidad
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var productionDate = DateOnly.Parse(dto.ProductionDate);
                var startHour = TimeOnly.Parse(dto.StartHour);
                var endHour = TimeOnly.Parse(dto.EndHour);

                // VALIDACIÓN ANTI-DUPLICADOS con LOCK para evitar condiciones de carrera
                var existingRecord = await _context.ProductionData
                    .FromSqlRaw(@"
                        SELECT * FROM ProductionData WITH (UPDLOCK, HOLDLOCK)
                        WHERE ProductionLinesId = {0}
                        AND ProductionDate = {1}
                        AND StartHour = {2}
                        AND EndHour = {3}
                        AND ProgramId = {4}",
                        dto.ProductionLinesId, productionDate, startHour, endHour, dto.ProgramId)
                    .FirstOrDefaultAsync();

                if (existingRecord != null)
                {
                    await transaction.RollbackAsync();
                    return Conflict(new {
                        message = "Ya existe un registro de producción con estos mismos datos.",
                        existingId = existingRecord.ProductionId,
                        existingPieces = existingRecord.ProducedPieces
                    });
                }

                var entity = new ProductionDatum
                {
                    ProductionLinesId = dto.ProductionLinesId,
                    ProductionDate = productionDate,
                    StartHour = startHour,
                    EndHour = endHour,
                    ProducedPieces = dto.ProducedPieces ?? 0,
                    RejectedPieces = dto.RejectedPieces ?? 0,
                    ProgramId = dto.ProgramId,
                    ProgramDescription = dto.ProgramDescription
                };

                _context.ProductionData.Add(entity);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Obtener el número de línea para la auditoría
                var lineNumber = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == dto.ProductionLinesId)
                    .Select(pl => pl.LineNumber)
                    .FirstOrDefaultAsync();

                // Registrar auditoría
                await _auditService.LogProductionDataAction("CREATE", entity.ProductionId,
                    $"Nuevo registro de producción: Línea {lineNumber}, Fecha {dto.ProductionDate}, Piezas producidas: {dto.ProducedPieces}");

                // Obtener el área de la línea de producción para notificar al dashboard
                var areaId = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == dto.ProductionLinesId)
                    .Select(pl => pl.AreaId)
                    .FirstOrDefaultAsync();

                // Enviar notificación SignalR al dashboard del área específica
                if (areaId.HasValue)
                {
                    // Calcular el nuevo requerimiento usando el SP
                    var today = DateTime.Today;
                    var currentTime = TimeOnly.FromDateTime(DateTime.Now);

                    var dailyResults = await _context.DailyProduction
                        .FromSqlRaw("EXEC [dbo].[GetDailyProductionByArea] @AreaID = {0}, @TargetDate = {1}, @TargetTime = {2}",
                            areaId.Value, today, currentTime)
                        .ToListAsync();

                    var productionLine = await _context.ProductionLines
                        .Where(pl => pl.ProductionLinesId == dto.ProductionLinesId)
                        .FirstOrDefaultAsync();

                    var lineData = dailyResults.FirstOrDefault(r => r.LineNumber == productionLine.LineNumber);

                    await _hubContext.Clients.Group($"Dashboard_{areaId.Value}")
                        .SendAsync("RequirementUpdated", new {
                            areaId = areaId.Value,
                            lineId = dto.ProductionLinesId,
                            requirementGoalPieces = lineData?.EstimatedGoalPieces ?? 0
                        });
                }

                return Ok(new { message = "Registro insertado correctamente." });
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627) // Violación de constraint único
            {
                await transaction.RollbackAsync();
                return Conflict(new { message = "Ya existe un registro de producción con estos mismos datos." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error interno.", error = ex.Message });
            }
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductionDatumUpdateDTO dto)
        {
            if (id != dto.ProductionId)
                return BadRequest(new { message = "El ID no coincide con el objeto enviado." });

            var existing = await _context.ProductionData.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Registro no encontrado." });

            try
            {
                // Obtener números de línea para la auditoría
                var oldLineNumber = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == existing.ProductionLinesId)
                    .Select(pl => pl.LineNumber)
                    .FirstOrDefaultAsync();

                var newLineNumber = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == dto.ProductionLinesId)
                    .Select(pl => pl.LineNumber)
                    .FirstOrDefaultAsync();

                // Capturar valores anteriores para auditoría
                var oldValues = $"Línea: {oldLineNumber}, Fecha: {existing.ProductionDate}, Piezas: {existing.ProducedPieces}";

                // Convertir los strings a los tipos correctos
                if (!DateOnly.TryParse(dto.ProductionDate, out var productionDate))
                    return BadRequest(new { message = "Fecha inválida." });

                if (!TimeOnly.TryParse(dto.StartHour, out var startHour))
                    return BadRequest(new { message = "Hora de inicio inválida." });

                if (!TimeOnly.TryParse(dto.EndHour, out var endHour))
                    return BadRequest(new { message = "Hora de fin inválida." });

                // Verificar si realmente hay cambios antes de proceder
                bool hasChanges = existing.ProductionLinesId != dto.ProductionLinesId ||
                                existing.ProductionDate != productionDate ||
                                existing.StartHour != startHour ||
                                existing.EndHour != endHour ||
                                existing.ProducedPieces != dto.ProducedPieces ||
                                existing.ProgramId != dto.ProgramId ||
                                existing.ProgramDescription != dto.ProgramDescription;

                if (!hasChanges)
                {
                    return Ok(new { message = "No hay cambios para actualizar." });
                }

                // Actualizar los campos
                existing.ProductionLinesId = dto.ProductionLinesId;
                existing.ProductionDate = productionDate;
                existing.StartHour = startHour;
                existing.EndHour = endHour;
                existing.ProducedPieces = dto.ProducedPieces;
                existing.ProgramId = dto.ProgramId;
                existing.ProgramDescription = dto.ProgramDescription;

                await _context.SaveChangesAsync();

                // Registrar auditoría solo cuando realmente hay cambios
                var newValues = $"Línea: {newLineNumber}, Fecha: {dto.ProductionDate}, Piezas: {dto.ProducedPieces}";
                await _auditService.LogProductionDataAction("UPDATE", id,
                    $"Actualización: Anterior [{oldValues}] → Nuevo [{newValues}]");

                // Obtener el área de la línea de producción para notificar al dashboard
                var areaId = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == dto.ProductionLinesId)
                    .Select(pl => pl.AreaId)
                    .FirstOrDefaultAsync();

                // Enviar notificación SignalR al dashboard del área específica
                if (areaId.HasValue)
                {
                    // Calcular el nuevo requerimiento usando el SP
                    var today = DateTime.Today;
                    var currentTime = TimeOnly.FromDateTime(DateTime.Now);

                    var dailyResults = await _context.DailyProduction
                        .FromSqlRaw("EXEC [dbo].[GetDailyProductionByArea] @AreaID = {0}, @TargetDate = {1}, @TargetTime = {2}",
                            areaId.Value, today, currentTime)
                        .ToListAsync();

                    var productionLine = await _context.ProductionLines
                        .Where(pl => pl.ProductionLinesId == dto.ProductionLinesId)
                        .FirstOrDefaultAsync();

                    var lineData = dailyResults.FirstOrDefault(r => r.LineNumber == productionLine.LineNumber);

                    await _hubContext.Clients.Group($"Dashboard_{areaId.Value}")
                        .SendAsync("RequirementUpdated", new {
                            areaId = areaId.Value,
                            lineId = dto.ProductionLinesId,
                            requirementGoalPieces = lineData?.EstimatedGoalPieces ?? 0
                        });
                }

                return Ok(new { message = "Registro actualizado correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno.", error = ex.Message });
            }
        }

        [HttpPost("UpdateProducedPieces")]
        public async Task<IActionResult> UpdateProducedPieces([FromForm] int id, [FromForm] int producedPieces)
        {
            var record = await _context.ProductionData.FindAsync(id);
            if (record == null)
                return NotFound("Registro no encontrado");

            var oldProducedPieces = record.ProducedPieces;

            // Solo proceder si realmente hay un cambio en las piezas producidas
            if (oldProducedPieces == producedPieces)
            {
                return Ok(new { message = "No hay cambios en la cantidad de piezas producidas." });
            }

            record.ProducedPieces = producedPieces;

            try
            {
                await _context.SaveChangesAsync();

                // Obtener el número de línea para la auditoría
                var lineNumber = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == record.ProductionLinesId)
                    .Select(pl => pl.LineNumber)
                    .FirstOrDefaultAsync();

                // Registrar auditoría solo cuando realmente hay un cambio
                await _auditService.LogProductionDataAction("UPDATE_PIECES", id,
                    $"Línea {lineNumber}: Actualización piezas producidas: {oldProducedPieces} → {producedPieces}");

                // Obtener el área de la línea de producción para notificar al dashboard
                var areaId = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == record.ProductionLinesId)
                    .Select(pl => pl.AreaId)
                    .FirstOrDefaultAsync();

                // Enviar notificación SignalR al dashboard del área específica
                if (areaId.HasValue)
                {
                    // Obtener el lineId para actualizar el requerimiento
                    var lineId = record.ProductionLinesId;

                    // Calcular el nuevo requerimiento usando el SP
                    var productionLineId = record.ProductionLinesId;
                    var today = DateTime.Today;
                    var currentTime = TimeOnly.FromDateTime(DateTime.Now);

                    var dailyResults = await _context.DailyProduction
                        .FromSqlRaw("EXEC [dbo].[GetDailyProductionByArea] @AreaID = {0}, @TargetDate = {1}, @TargetTime = {2}",
                            areaId.Value, today, currentTime)
                        .ToListAsync();

                    var productionLine = await _context.ProductionLines
                        .Where(pl => pl.ProductionLinesId == productionLineId)
                        .FirstOrDefaultAsync();

                    var lineData = dailyResults.FirstOrDefault(r => r.LineNumber == productionLine.LineNumber);

                    await _hubContext.Clients.Group($"Dashboard_{areaId.Value}")
                        .SendAsync("RequirementUpdated", new {
                            areaId = areaId.Value,
                            lineId = lineId,
                            requirementGoalPieces = lineData?.RequirementGoalPieces ?? 0
                        });
                }

                return Ok(new { message = "Cantidad de piezas producidas actualizada." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Error al actualizar: " + ex.Message);
            }
        }





        //Controller para actualizar las piezas en el createform
        [HttpGet("CheckIfProductionExists")]
        public async Task<IActionResult> CheckIfProductionExistsClientEval(
            int lineId,
            DateTime date,
            string startHour,
            string endHour,
            int programId)
        {
            try
            {
                if (!TimeOnly.TryParse(startHour, out var start) ||
                    !TimeOnly.TryParse(endHour, out var end))
                    return BadRequest("Formato de hora inválido");

                if (start >= end)
                    return BadRequest("Hora de inicio debe ser menor que hora de fin");

                var productionDate = DateOnly.FromDateTime(date);

                // Buscar el primer registro que se solape
                var record = await _context.ProductionData
                    .Where(p => p.ProductionLinesId == lineId && p.ProductionDate == productionDate)
                    .Where(p => start < p.EndHour && end > p.StartHour)
                    .Where(p => p.ProgramId == programId)
                    .Select(p => new { p.ProducedPieces, p.ProductionId, p.ProgramDescription })
                    .FirstOrDefaultAsync();

                if (record != null)
                {
                    // Si existe, devolvemos la cantidad producida
                    return Ok(new { exists = true, producedPieces = record.ProducedPieces, id = record.ProductionId, programDescription = record.ProgramDescription });
                }

                // Si no existe, devolvemos exists = false
                return Ok(new { exists = false, producedPieces = 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }












        [HttpGet("GetLinesByArea")]
        public async Task<IActionResult> GetLinesByArea(int areaId)
        {
            try
            {
                var lines = await _context.ProductionLines
                    .Where(pl => pl.AreaId == areaId && pl.IsActive == true)
                    .OrderBy(pl => pl.LineNumber)
                    .Select(pl => new
                    {
                        id = pl.ProductionLinesId,
                        lineNumber = pl.LineNumber,
                        lineName = pl.LineName,
                        description = !string.IsNullOrEmpty(pl.LineName) ? pl.LineName : $"Línea {pl.LineNumber}"
                    })
                    .ToListAsync();

                return Ok(lines);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

    }
}
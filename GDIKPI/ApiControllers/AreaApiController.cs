using GDIKPI.Data;
using GDIKPI.DTO.Area;
using GDIKPI.Models;
using GDIKPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GDIKPI.ApiControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AreaApiController : ControllerBase
    {
        private readonly KpisContext _context;
        private readonly AuditService _auditService;

        public AreaApiController(KpisContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }
        [HttpPost("CreateArea")]
        public IActionResult CreateArea([FromForm] string areaName, [FromForm] string customerName)

        {
            if (string.IsNullOrWhiteSpace(areaName) || string.IsNullOrWhiteSpace(customerName))
                return BadRequest("Datos inválidos.");

            var newArea = new Area
            {
                AreaName = areaName,
                CustomerName = customerName
            };

            _context.Areas.Add(newArea);
            _context.SaveChanges();

            // Devuelve el nuevo AreaID y DisplayName combinado
            return Ok(new
            {
                areaId = newArea.AreaId,
                displayName = $"{newArea.AreaName} - {newArea.CustomerName}"
            });
        }

        private IQueryable<ProductionLine> GetFilter(IFormCollection form)
        {
            var query = _context.ProductionLines
                .AsNoTracking()
                .Include(p => p.Area)
                .AsQueryable();

            // Filtro por AreaId (que viene del select con nombre "areaId")
            if (!string.IsNullOrEmpty(form["areaNameFilter"]))
            {
                string areaNameFilter = form["areaNameFilter"].ToString().Trim();
                query = query.Where(p => p.Area != null && p.Area.AreaName.Contains(areaNameFilter));
            }
            

            // Filtro por CustomerName
            if (!string.IsNullOrEmpty(form["customerNameFilter"]))
            {
                string customerName = form["customerNameFilter"].ToString().Trim();
                query = query.Where(p => p.Area != null && p.Area.CustomerName.Contains(customerName));
            }

            // Filtro por LineNumber (numérico)
            if (int.TryParse(form["lineNumber"], out int lineNumber) && lineNumber > 0)
            {
                query = query.Where(p => p.LineNumber == lineNumber);
            }

            // Filtro por DailyGoal (numérico)
            if (int.TryParse(form["dailyGoal"], out int dailyGoal) && dailyGoal > 0)
            {
                query = query.Where(p => p.DailyGoal == dailyGoal);
            }

            // Filtro por PersonalQuantity (staffCount) (numérico)
            if (int.TryParse(form["staffCount"], out int staffCount) && staffCount > 0)
            {
                query = query.Where(p => p.PersonalQuantity == staffCount);
            }



            // Filtro por SupervisorName
            if (!string.IsNullOrEmpty(form["supervisorFilter"]))
            {
                string supervisorStr = form["supervisorFilter"].ToString().Trim();

                // Buscar el nombre del supervisor según su número de empleado
                string supervisorName = _context.Users
                    .Where(u => u.EmployeeNumber == supervisorStr)
                    .Select(u => u.Name)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(supervisorName))
                {
                    query = query.Where(p => _context.UserLinePermissions
                        .Any(ulp => ulp.ProductionLinesId == p.ProductionLinesId &&
                                    ulp.EmployeeNumberNavigation.Name == supervisorName));
                }
                else
                {
                    // No se encontró el supervisor con ese número
                    query = query.Where(p => false);
                }
            }




            return query;
        }


        private object GetTable(IQueryable<ProductionLine> query, int draw, int start, int length, string orderColumn, string orderDirection)
        {
            int totalRecords = query.Count();

            // Orden dinámico
            var orderedQuery = (orderColumn, orderDirection) switch
            {
                ("0", "asc") => query.OrderByDescending(p => p.IsActive).ThenBy(p => p.Area!.CustomerName),
                ("0", "desc") => query.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.Area!.CustomerName),
                ("1", "asc") => query.OrderByDescending(p => p.IsActive).ThenBy(p => p.Area!.AreaName),
                ("1", "desc") => query.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.Area!.AreaName),
                ("2", "asc") => query.OrderByDescending(p => p.IsActive).ThenBy(p => p.LineNumber),
                ("2", "desc") => query.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.LineNumber),
                ("3", "asc") => query.OrderByDescending(p => p.IsActive).ThenBy(p => p.DailyGoal),
                ("3", "desc") => query.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.DailyGoal),
                ("4", "asc") => query.OrderByDescending(p => p.IsActive).ThenBy(p => p.PersonalQuantity),
                ("4", "desc") => query.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.PersonalQuantity),
                _ => query.OrderByDescending(p => p.IsActive).ThenBy(p => p.Area!.CustomerName).ThenBy(p => p.Area!.AreaName).ThenBy(p => p.LineNumber)
            };

            // Paginación
            var pageItems = orderedQuery
                .Skip(start)
                .Take(length)
                .ToList();

            var productionLineIds = pageItems.Select(p => p.ProductionLinesId).ToList();

            var breaksByLine = _context.Breaks
                .Where(b => productionLineIds.Contains(b.ProductionLinesId))
                .AsEnumerable()
                .GroupBy(b => b.ProductionLinesId)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join(", ", g
                        .OrderBy(b => b.BreakStart)
                        .Select(b => b.BreakStart.HasValue && b.BreakEnd.HasValue
                            ? b.BreakStart.Value.ToString("HH:mm") + " - " + b.BreakEnd.Value.ToString("HH:mm")
                            : string.Empty)
                        .Where(x => !string.IsNullOrWhiteSpace(x))));

            var supervisorsByLine = _context.UserLinePermissions
                .Where(ulp => productionLineIds.Contains(ulp.ProductionLinesId))
                .Select(ulp => new
                {
                    ulp.ProductionLinesId,
                    SupervisorName = ulp.EmployeeNumberNavigation.Name
                })
                .AsEnumerable()
                .GroupBy(x => x.ProductionLinesId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.SupervisorName).FirstOrDefault());

            var data = pageItems
                .Select(p => new
                {
                    productionLineId = p.ProductionLinesId,
                    isActive = p.IsActive,
                    customerName = p.Area != null ? p.Area.CustomerName : string.Empty,
                    areaName = p.Area != null ? p.Area.AreaName : string.Empty,
                    lineNumber = p.LineNumber,
                    lineName = p.LineName,
                    dailyGoal = p.DailyGoal,
                    personalQuantity = p.PersonalQuantity,
                    descansos = breaksByLine.TryGetValue(p.ProductionLinesId, out var descansos) ? descansos : string.Empty,
                    supervisor = supervisorsByLine.TryGetValue(p.ProductionLinesId, out var supervisor) ? supervisor : string.Empty
                })
                .ToList();

            return new
            {
                draw,
                recordsTotal = totalRecords,
                recordsFiltered = totalRecords,
                data
            };
        }

        [HttpPost("SetTable")]
        public IActionResult SetTable()
        {
            try
            {
                int.TryParse(Request.Form["draw"], out int draw);
                int.TryParse(Request.Form["start"], out int start);
                int.TryParse(Request.Form["length"], out int length);
                string orderColumn = Request.Form["order[0][column]"].FirstOrDefault();
                string orderDirection = Request.Form["order[0][dir]"].FirstOrDefault();

                var queryFiltrada = GetFilter(Request.Form);
                var resultado = GetTable(queryFiltrada, draw, start, length, orderColumn, orderDirection);

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("CreateProductionLine")]
        public IActionResult CreateProductionLine([FromForm] int areaId,
                          [FromForm] int lineNumber,
                          [FromForm] string lineName,
                          [FromForm] int dailyGoal,
                          [FromForm] int personalQuantity,
                          [FromForm] string break1Start,
                          [FromForm] string break1End,
                          [FromForm] string break2Start,
                          [FromForm] string break2End,
                          [FromForm] string supervisor,
                          [FromForm] int[] permissions)
        {
            if (areaId == 0 || lineNumber <= 0 || dailyGoal <= 0 || personalQuantity <= 0 || string.IsNullOrEmpty(supervisor))
                return BadRequest("Datos incompletos o inválidos.");

            var allPermissions = new List<int>();

            if (permissions != null && permissions.Length > 0)
            {
                var validPermissions = new[] { 2, 3, 4 };

                if (!permissions.All(p => validPermissions.Contains(p)))
                    return BadRequest("Los permisos seleccionados no son válidos.");

                allPermissions.AddRange(permissions.Distinct());
            }

            var exists = _context.ProductionLines
                .Any(p => p.AreaId == areaId && p.LineNumber == lineNumber);
            if (exists)
                return Conflict("La línea ya existe en esta área.");

            // Usar una transacción para asegurar consistencia de datos
            using var transaction = _context.Database.BeginTransaction();

            try
            {
                // Crear la línea de producción
                var newLine = new ProductionLine
                {
                    AreaId = areaId,
                    LineNumber = lineNumber,
                    LineName = lineName,
                    DailyGoal = dailyGoal,
                    PersonalQuantity = personalQuantity,
                    ShiftNumber = 1 // O establece según corresponda
                };

                _context.ProductionLines.Add(newLine);
                _context.SaveChanges();

                // Update the ParseTimeOnly method to handle the conversion from TimeSpan to TimeOnly
                TimeOnly? ParseTimeOnly(string value)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        return null;
                    if (TimeSpan.TryParse(value, out TimeSpan timeSpan))
                    {
                        return TimeOnly.FromTimeSpan(timeSpan); // Convert TimeSpan to TimeOnly
                    }
                    throw new FormatException($"El valor '{value}' no se pudo convertir a TimeOnly.");
                }

                // Crear los descansos
                var breaks = new List<Break>
        {
            new Break
            {
                ProductionLinesId = newLine.ProductionLinesId,
                BreakStart = ParseTimeOnly(break1Start),
                BreakEnd = ParseTimeOnly(break1End)
            },
            new Break
            {
                ProductionLinesId = newLine.ProductionLinesId,
                BreakStart = ParseTimeOnly(break2Start),
                BreakEnd = ParseTimeOnly(break2End)
            }
        };

                _context.Breaks.AddRange(breaks);
                _context.SaveChanges();

                // Crear los permisos del supervisor para esta línea (incluyendo el permiso 1 por defecto)
                var userLinePermissions = new List<UserLinePermission>();
                foreach (var permissionId in allPermissions)
                {
                    userLinePermissions.Add(new UserLinePermission
                    {
                        EmployeeNumber = supervisor,
                        ProductionLinesId = newLine.ProductionLinesId,
                        PermissionId = permissionId
                    });
                }

                _context.UserLinePermissions.AddRange(userLinePermissions);
                _context.SaveChanges();
                bool alreadyHasAreaPermission = _context.UserAreaPermissions
            .Any(p => p.EmployeeNumber == supervisor && p.AreaId == areaId && p.PermissionId == 1);

                if (!alreadyHasAreaPermission)
                {
                    var areaPermission = new UserAreaPermission
                    {
                        EmployeeNumber = supervisor,
                        AreaId = areaId,
                        PermissionId = 1
                    };

                    _context.UserAreaPermissions.Add(areaPermission);
                    _context.SaveChanges();
                }

                // Confirmar la transacción
                transaction.Commit();

                return Ok(new
                {
                    success = true,
                    productionLineId = newLine.ProductionLinesId,
                    supervisorId = supervisor,
                    permissionsAssigned = allPermissions.Count,
                    permissions = allPermissions, // Mostrar todos los permisos asignados incluyendo el 1
                    message = $"Línea de producción creada exitosamente con {allPermissions.Count} permisos asignados al supervisor (incluyendo permiso por defecto)."
                });
            }
            catch (Exception ex)
            {
                // Revertir la transacción en caso de error
                transaction.Rollback();

                // Log del error (opcional)
                // _logger.LogError(ex, "Error al crear la línea de producción");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor al crear la línea de producción.",
                    error = ex.Message
                });
            }
        }

        // Agregar estos métodos a tu AreaApiController

        [HttpGet("GetProductionLineDetails")]
        public async Task<IActionResult> GetProductionLineDetails(string customerName, string areaName, int lineNumber)
        {
            try
            {
                // Buscar la línea específica
                var productionLine = await _context.ProductionLines
                    .Include(pl => pl.Area)
                    .FirstOrDefaultAsync(pl =>
                        pl.Area.CustomerName == customerName &&
                        pl.Area.AreaName == areaName &&
                        pl.LineNumber == lineNumber);

                if (productionLine == null)
                {
                    return NotFound("Línea de producción no encontrada");
                }

                var result = new
                {
                    areaId = productionLine.AreaId,
                    lineNumber = productionLine.LineNumber,
                    dailyGoal = productionLine.DailyGoal,
                    personalQuantity = productionLine.PersonalQuantity,
                    descansos = productionLine.Breaks,
                    customerName = productionLine.Area.CustomerName,
                    areaName = productionLine.Area.AreaName
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener los detalles: {ex.Message}");
            }
        }

        [HttpGet("GetAreaDetails")]
        public async Task<IActionResult> GetAreaDetails(int areaid)
        {
            try
            {
                var area = await _context.Areas
                    .FirstOrDefaultAsync(a =>
                        a.AreaId == areaid 
                       );

                if (area == null)
                {
                    return NotFound("Área no encontrada");
                }

                var result = new
                {
                    areaId = area.AreaId,
                    areaName = area.AreaName,
                    customerName = area.CustomerName
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener los detalles del área: {ex.Message}");
            }
        }
        [HttpPost("UpdateArea")]
        public async Task<IActionResult> UpdateArea([FromForm] Area updatedArea)
        {
            var area = await _context.Areas.FirstOrDefaultAsync(a => a.AreaId == updatedArea.AreaId);

            if (area == null)
                return NotFound(new { message = "Área no encontrada para actualizar" });

            area.AreaName = updatedArea.AreaName;
            area.CustomerName = updatedArea.CustomerName;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Área actualizada correctamente" });
        }





        [HttpGet("GetAllAreas")]
        public async Task<IActionResult> GetAllAreas()
        {
            try
            {
                var areas = await _context.Areas
                    .Select(a => new
                    {
                        areaId = a.AreaId,
                        displayName = $"{a.CustomerName} - {a.AreaName}"
                    })
                    .OrderBy(a => a.displayName)
                    .ToListAsync();

                return Ok(areas);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al obtener las áreas: {ex.Message}");
            }
        }

        [HttpPut("UpdateProductionLine")]
        public async Task<IActionResult> UpdateProductionLine([FromBody] ProductionLineUpdateDTO dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingLine = await _context.ProductionLines
                    .FirstOrDefaultAsync(p => p.AreaId == dto.AreaId && p.LineNumber == dto.OriginalLineNumber);
                if (existingLine == null)
                {
                    return Ok(new { success = false, message = "La línea de producción no existe." });
                }

                if (dto.LineNumber != dto.OriginalLineNumber)
                {
                    var duplicateLine = await _context.ProductionLines
                        .AnyAsync(p => p.LineNumber == dto.LineNumber &&
                                       p.AreaId == dto.AreaId &&
                                       p.ProductionLinesId != existingLine.ProductionLinesId);
                    if (duplicateLine)
                    {
                        return Ok(new { success = false, message = "Ya existe una línea con ese número en el área seleccionada." });
                    }
                }

                if (dto.StandardTime.HasValue && dto.StandardTime.Value < 0)
                {
                    return Ok(new { success = false, message = "El tiempo estándar no puede ser negativo." });
                }

                // Validar supervisor
                string supervisorEmployeeNumber = null;
                if (!string.IsNullOrWhiteSpace(dto.SupervisorEmployeeNumber))
                {
                    supervisorEmployeeNumber = dto.SupervisorEmployeeNumber;
                }

                // Capturar valores anteriores para auditoría
                int oldDailyGoal = existingLine.DailyGoal;
                var area = await _context.Areas.FirstOrDefaultAsync(a => a.AreaId == dto.AreaId);
                string areaInfo = area != null ? $"{area.CustomerName} - {area.AreaName}" : $"Area ID: {dto.AreaId}";

                // Actualizar datos de la línea
                existingLine.LineNumber = dto.LineNumber;
                existingLine.LineName = dto.LineName;
                existingLine.AreaId = dto.AreaId;
                existingLine.DailyGoal = dto.DailyGoal;
                existingLine.PersonalQuantity = dto.PersonalQuantity;
                existingLine.StandardTime = dto.StandardTime;
                existingLine.IsActive = dto.IsActive;

                // Actualizar descansos
                var existingBreaks = await _context.Breaks
                    .Where(b => b.ProductionLinesId == existingLine.ProductionLinesId)
                    .OrderBy(b => b.BreakStart)
                    .ToListAsync();
                ProcessBreak(dto.Break1Start, dto.Break1End, existingBreaks, 0, existingLine.ProductionLinesId);
                ProcessBreak(dto.Break2Start, dto.Break2End, existingBreaks, 1, existingLine.ProductionLinesId);

                // Actualizar TODOS los permisos del supervisor
                await UpdateAllUserPermissions(existingLine.ProductionLinesId, supervisorEmployeeNumber, dto.Permissions);

                await _context.SaveChangesAsync();

                // Registrar en el audit log si cambió la meta diaria
                if (oldDailyGoal != dto.DailyGoal)
                {
                    string auditDetails = $"Meta diaria actualizada en {areaInfo}, Línea {dto.LineNumber}: {oldDailyGoal} → {dto.DailyGoal}";
                    await _auditService.LogProductionLineAction("UPDATE", existingLine.ProductionLinesId, auditDetails);
                }

                await transaction.CommitAsync();
                return Ok(new { success = true, message = "Línea de producción actualizada correctamente." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error interno del servidor. Por favor, intente nuevamente.",
                    details = ex.Message // Solo para debugging, remover en producción
                });
            }
        }

        private async Task UpdateAllUserPermissions(int productionLineId, string supervisorEmployeeNumber, List<int> selectedPermissions)
        {
            // Obtener todos los permisos existentes para esta línea
            var currentPermissions = await _context.UserLinePermissions
                .Where(p => p.ProductionLinesId == productionLineId)
                .ToListAsync();

            // Si no hay supervisor, eliminar todos los permisos
            if (string.IsNullOrEmpty(supervisorEmployeeNumber))
            {
                _context.UserLinePermissions.RemoveRange(currentPermissions);
                return;
            }

            // Lista completa de permisos a asignar (siempre incluye supervisor = 1)
            var permissionsToAssign = new List<int> { 1 }; // Supervisor siempre
            if (selectedPermissions != null && selectedPermissions.Any())
            {
                permissionsToAssign.AddRange(selectedPermissions.Where(p => p > 1)); // Solo agregar permisos > 1
            }

            // Eliminar permisos que ya no están en la lista
            var permissionsToRemove = currentPermissions
                .Where(cp => cp.EmployeeNumber == supervisorEmployeeNumber &&
                             !permissionsToAssign.Contains(cp.PermissionId))
                .ToList();

            if (permissionsToRemove.Any())
            {
                _context.UserLinePermissions.RemoveRange(permissionsToRemove);
            }

            // Agregar nuevos permisos
            foreach (var permissionId in permissionsToAssign)
            {
                var existingPermission = currentPermissions
                    .FirstOrDefault(cp => cp.EmployeeNumber == supervisorEmployeeNumber &&
                                         cp.PermissionId == permissionId);

                if (existingPermission == null)
                {
                    var newPermission = new UserLinePermission
                    {
                        EmployeeNumber = supervisorEmployeeNumber,
                        ProductionLinesId = productionLineId,
                        PermissionId = permissionId
                    };
                    _context.UserLinePermissions.Add(newPermission);
                }
                // Si ya existe, actualizar el empleado (por si cambió el supervisor)
                else if (existingPermission.EmployeeNumber != supervisorEmployeeNumber)
                {
                    existingPermission.EmployeeNumber = supervisorEmployeeNumber;
                }
            }

            // Eliminar permisos de otros empleados (si se cambió el supervisor)
            var permissionsFromOtherEmployees = currentPermissions
                .Where(cp => cp.EmployeeNumber != supervisorEmployeeNumber)
                .ToList();

            if (permissionsFromOtherEmployees.Any())
            {
                _context.UserLinePermissions.RemoveRange(permissionsFromOtherEmployees);
            }
        }



        // Update the ProcessBreak method to use TimeOnly instead of TimeSpan
        private void ProcessBreak(string startTime, string endTime, List<Break> existingBreaks, int breakIndex, int productionLineId)
        {
            var existingBreak = existingBreaks.ElementAtOrDefault(breakIndex);

            // Si se proporcionaron ambos horarios del descanso
            if (!string.IsNullOrEmpty(startTime) && !string.IsNullOrEmpty(endTime))
            {
                if (TimeOnly.TryParse(startTime, out TimeOnly start) && TimeOnly.TryParse(endTime, out TimeOnly end))
                {
                    if (existingBreak == null)
                    {
                        // Crear nuevo descanso
                        var newBreak = new Break
                        {
                            ProductionLinesId = productionLineId,
                            BreakStart = start,
                            BreakEnd = end
                        };
                        _context.Breaks.Add(newBreak);
                    }
                    else
                    {
                        // Actualizar descanso existente
                        existingBreak.BreakStart = start;
                        existingBreak.BreakEnd = end;
                    }
                }
            }
            else
            {
                // Si no se proporcionaron horarios y existe un descanso, eliminarlo
                if (existingBreak != null)
                {
                    _context.Breaks.Remove(existingBreak);
                }
            }
        }





    }
}

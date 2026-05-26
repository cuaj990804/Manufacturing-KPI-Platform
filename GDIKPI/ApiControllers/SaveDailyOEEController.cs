
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using GDIKPI.Data;
using GDIKPI.DTO;

namespace GDIKPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SaveDailyOEEController : ControllerBase
    {
        private readonly KpisContext _context; 

        public SaveDailyOEEController(KpisContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> GetActiveLines()
        {
            var result = await _context.ProductionLines
                .Where(pl => pl.IsActive)
                .ToListAsync();
            return Ok(result);
        }



        [HttpGet("dailyproductionbyarea")]
        public async Task<IActionResult> GetDailyProductionByArea(
        [FromQuery] int areaId,
        [FromQuery] DateTime? targetDate = null,
        [FromQuery] TimeOnly? targetTime = null)
        {
            try
            {
                var dateParam = targetDate?.Date ?? DateTime.Today;
                var timeParam = targetTime?.ToTimeSpan() ?? DateTime.Now.TimeOfDay;

                var results = await _context.DailyProduction
                    .FromSqlRaw("EXEC [dbo].[GetDailyProductionByArea] @AreaID = {0}, @TargetDate = {1}, @TargetTime = {2}",
                        areaId, dateParam, timeParam)
                    .ToListAsync();

                // Filtrar solo líneas activas
                var activeLineNumbers = await _context.ProductionLines
                    .Where(pl => pl.AreaId == areaId && pl.IsActive)
                    .Select(pl => pl.LineNumber)
                    .ToListAsync();

                var filteredResults = results.Where(r => activeLineNumbers.Contains(r.LineNumber)).ToList();

                // Crear lista de respuesta con OEE
                var response = new List<DailyProductionWithOEEDTO>();

                // Obtener OEE para cada línea
                foreach (var result in filteredResults)
                {
                    var productionLineId = await _context.ProductionLines
                        .Where(pl => pl.LineNumber == result.LineNumber && pl.AreaId == areaId)
                        .Select(pl => pl.ProductionLinesId)
                        .FirstOrDefaultAsync();

                    decimal oeePercentage = 0;
                    if (productionLineId > 0)
                    {
                        var oeeResults = await _context.LineOEE
                            .FromSqlRaw("EXEC [dbo].[GetLineOEE] @ProductionLineId = {0}, @TargetDate = {1}, @TargetTime = {2}",
                                productionLineId, dateParam, timeParam)
                            .ToListAsync();

                        oeePercentage = oeeResults.FirstOrDefault()?.OeePercentage ?? 0;
                    }

                    response.Add(new DailyProductionWithOEEDTO
                    {
                        AreaCustomerName = result.AreaCustomerName,
                        LineNumber = result.LineNumber,
                        ProductionDate = result.ProductionDate,
                        HourInterval = result.HourInterval,
                        GoalPieces = result.GoalPieces,
                        ProducedPieces = result.ProducedPieces,
                        RejectedPieces = result.RejectedPieces,
                        AccumulatedRejections = result.AccumulatedRejections,
                        DowntimeMinutes = result.DowntimeMinutes,
                        AccumulatedDowntime = result.AccumulatedDowntime,
                        AccumulatedBalance = result.AccumulatedBalance,
                        EstimatedGoalPieces = result.EstimatedGoalPieces,
                        RequirementGoalPieces = result.RequirementGoalPieces,
                        RequirementBalance = result.RequirementBalance,
                        QualityPercentage = result.QualityPercentage,
                        OEEPercentage = oeePercentage
                    });
                }

                return Ok(response);
            }
            catch (SqlException sqlEx)
            {
                return BadRequest(new { error = "Error de base de datos", message = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error interno", message = ex.Message });
            }
        }

        [HttpGet("line-oee")]
        public async Task<IActionResult> GetLineOEE(
            [FromQuery] int productionLineId,
            [FromQuery] DateTime? targetDate = null,
            [FromQuery] TimeOnly? targetTime = null)
        {
            try
            {
                var dateParam = targetDate?.Date ?? DateTime.Today;
                var timeParam = targetTime?.ToTimeSpan() ?? DateTime.Now.TimeOfDay;

                // Verificar que la línea esté activa
                var isLineActive = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == productionLineId && pl.IsActive)
                    .AnyAsync();

                if (!isLineActive)
                {
                    return NotFound(new { error = "Línea de producción no encontrada o inactiva" });
                }

                var results = await _context.LineOEE
                    .FromSqlRaw("EXEC [dbo].[GetLineOEETEST] @ProductionLineId = {0}, @TargetDate = {1}, @TargetTime = {2}",
                        productionLineId, dateParam, timeParam)
                    .ToListAsync();

                var result = results.FirstOrDefault();

                if (result == null)
                {
                    return NotFound(new { error = "No se encontraron datos para la línea especificada" });
                }

                return Ok(result);
            }
            catch (SqlException sqlEx)
            {
                return BadRequest(new { error = "Error de base de datos", message = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error interno", message = ex.Message });
            }
        }

        [HttpGet("save-daily-efficiency")]
        public async Task<IActionResult> SaveDailyEfficiency(
            [FromQuery] DateTime? targetDate = null,
            [FromQuery] TimeOnly? targetTime = null)
        {
            try
            {
                var dateParam = targetDate?.Date ?? DateTime.Today;
                var timeParam = targetTime?.ToTimeSpan() ?? DateTime.Now.TimeOfDay;

                // Obtener todas las líneas activas
                var activeLines = await _context.ProductionLines
                    .Where(pl => pl.IsActive)
                    .ToListAsync();

                if (!activeLines.Any())
                {
                    return Ok(new { message = "No hay líneas activas para procesar", recordsSaved = 0 });
                }

                var savedRecords = 0;
                var errors = new List<string>();

                // Procesar cada línea activa
                foreach (var line in activeLines)
                {
                    try
                    {
                        // Ejecutar el SP GetLineOEE para obtener los datos de producción
                        var oeeResults = await _context.LineOEE
                            .FromSqlRaw("EXEC [dbo].[GetLineOEE] @ProductionLineId = {0}, @TargetDate = {1}, @TargetTime = {2}",
                                line.ProductionLinesId, dateParam, timeParam)
                            .ToListAsync();

                        var oeeData = oeeResults.FirstOrDefault();

                        if (oeeData != null)
                        {
                            // Calcular eficiencia usando la fórmula:
                            // Eficiencia = (Piezas Producidas × Tiempo Estándar) / (Personal × Horas Efectivas) × 100
                            var producidas = oeeData.ProducedPieces;
                            var tiempoEstandar = line.StandardTime ?? 0;
                            var personal = line.PersonalQuantity;
                            var tiempoEfectivoHoras = oeeData.OperatingMinutes / 60.0;

                            var horasGanadas = Math.Round(producidas * tiempoEstandar, 4);
                            var horasUtilizadas = Math.Round((decimal)(personal * tiempoEfectivoHoras), 4);

                            decimal eficienciaPercentage = 0;
                            if (personal > 0 && tiempoEfectivoHoras > 0)
                            {
                                eficienciaPercentage = Math.Round((decimal)((producidas * (double)tiempoEstandar) / (personal * tiempoEfectivoHoras) * 100), 4);
                            }

                            // Crear el registro para Efficiency
                            var efficiency = new Models.Efficiency
                            {
                                RegistrationDate = DateTime.Now,
                                DateData = DateOnly.FromDateTime(dateParam),
                                TimeData = TimeOnly.FromTimeSpan(timeParam),
                                ProductionLinesId = oeeData.ProductionLinesId,
                                LineNumber = oeeData.LineNumber.ToString(),
                                AreaCustomerName = oeeData.AreaCustomerName,
                                EfficiencyPercentage = eficienciaPercentage,
                                PeopleQuantity = line.PersonalQuantity,
                                TiempoEstandar = line.StandardTime ?? 0,
                                HorasGanadas = horasGanadas,
                                HorasUtilizadas = horasUtilizadas,
                                ProducedPieces = producidas
                            };

                            // Insertar en la base de datos
                            _context.Efficiencies.Add(efficiency);
                            savedRecords++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error procesando línea {line.LineNumber}: {ex.Message}");
                    }
                }

                // Guardar todos los cambios
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Proceso completado",
                    recordsSaved = savedRecords,
                    totalActiveLines = activeLines.Count,
                    errors = errors.Any() ? errors : null
                });
            }
            catch (Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                return BadRequest(new { error = "Error de base de datos", message = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error interno", message = ex.Message });
            }
        }

        [HttpGet("save-daily-oee")]
        public async Task<IActionResult> SaveDailyOEE(
            [FromQuery] DateTime? targetDate = null,
            [FromQuery] TimeOnly? targetTime = null)
        {
            try
            {
                var dateParam = targetDate?.Date ?? DateTime.Today;
                var timeParam = targetTime?.ToTimeSpan() ?? DateTime.Now.TimeOfDay;

                // Obtener todas las líneas activas
                var activeLines = await _context.ProductionLines
                    .Where(pl => pl.IsActive)
                    .ToListAsync();

                if (!activeLines.Any())
                {
                    return Ok(new { message = "No hay líneas activas para procesar", recordsSaved = 0 });
                }

                var savedRecords = 0;
                var errors = new List<string>();

                // Procesar cada línea activa
                foreach (var line in activeLines)
                {
                    try
                    {
                        // Ejecutar el SP GetLineOEE para cada línea
                        var oeeResults = await _context.LineOEE
                            .FromSqlRaw("EXEC [dbo].[GetLineOEE] @ProductionLineId = {0}, @TargetDate = {1}, @TargetTime = {2}",
                                line.ProductionLinesId, dateParam, timeParam)
                            .ToListAsync();

                        var oeeData = oeeResults.FirstOrDefault();

                        if (oeeData != null)
                        {
                            // Crear el registro para OEEHistory
                            var oeeHistory = new Models.Oeehistory
                            {
                                RegistrationDate = DateTime.Now,
                                DateData = DateOnly.FromDateTime(dateParam),
                                TimeData = TimeOnly.FromTimeSpan(timeParam),
                                ProductionLinesId = oeeData.ProductionLinesId,
                                LineNumber = oeeData.LineNumber.ToString(),
                                AreaCustomerName = oeeData.AreaCustomerName,
                                RequirementGoalPieces = oeeData.RequirementGoalPieces,
                                ProducedPieces = oeeData.ProducedPieces,
                                DowntimeMinutes = oeeData.DowntimeMinutes,
                                RejectedPieces = oeeData.RejectedPieces,
                                AvailabilityPercentage = oeeData.AvailabilityPercentage,
                                PerformancePercentage = oeeData.PerformancePercentage,
                                QualityPercentage = oeeData.QualityPercentage,
                                OeePercentage = oeeData.OeePercentage,
                                PlannedMinutes = oeeData.PlannedMinutes,
                                OperatingMinutes = oeeData.OperatingMinutes
                            };

                            // Insertar en la base de datos
                            _context.Oeehistories.Add(oeeHistory);
                            savedRecords++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error procesando línea {line.LineNumber}: {ex.Message}");
                    }
                }

                // Guardar todos los cambios
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Proceso completado",
                    recordsSaved = savedRecords,
                    totalActiveLines = activeLines.Count,
                    errors = errors.Any() ? errors : null
                });
            }
            catch (SqlException sqlEx)
            {
                return BadRequest(new { error = "Error de base de datos", message = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Error interno", message = ex.Message });
            }
        }


    }
}

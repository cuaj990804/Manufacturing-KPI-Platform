
using GDIKPI.Data;
using GDIKPI.DTO;
using GDIKPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductionController : ControllerBase
    {
        private readonly KpisContext _context; // Usa tu DbContext existente

        public ProductionController(KpisContext context)
        {
            _context = context;
        }




        // En tu controller
        [HttpGet("daily-production")]
        public async Task<IActionResult> GetDailyProduction(
            [FromQuery] int productionLinesId,
            [FromQuery] DateTime? targetDate = null)
        {
            try
            {
                var dateParam = targetDate?.Date ?? DateTime.Today;

                // Verificar que la línea esté activa antes de ejecutar el SP
                var isLineActive = await _context.ProductionLines
                    .Where(pl => pl.ProductionLinesId == productionLinesId && pl.IsActive)
                    .AnyAsync();

                if (!isLineActive)
                {
                    return Ok(new List<object>());
                }

                var results = await _context.DailyProductionIntervals
                    .FromSqlRaw("EXEC [dbo].[GetDailyProduction] @ProductionLinesID = {0}, @TargetDate = {1}",
                        productionLinesId, dateParam)
                    .ToListAsync();

                return Ok(results);
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

        [HttpGet("dailyproductionbyarea")]
        public async Task<IActionResult> GetDailyProductionByArea(
        [FromQuery] int? areaId,
        [FromQuery] DateTime? targetDate = null,
        [FromQuery] TimeOnly? targetTime = null)
        {
            try
            {
                var dateParam = targetDate?.Date ?? DateTime.Today;
                var timeParam = targetTime?.ToTimeSpan() ?? DateTime.Now.TimeOfDay;



                if (areaId.HasValue)
                {


                    var results = await _context.DailyProduction
                        .FromSqlRaw("EXEC [dbo].[GetDailyProductionByArea] @AreaID = {0}, @TargetDate = {1}, @TargetTime = {2}",
                            areaId, dateParam, timeParam)
                        .ToListAsync();

                    // Incluir siempre todas las lineas activas, aunque el procedimiento
                    // todavia no genere un registro para el intervalo actual.
                    var activeLines = await _context.ProductionLines
                        .Where(pl => pl.AreaId == areaId && pl.IsActive)
                        .Select(pl => new
                        {
                            pl.ProductionLinesId,
                            pl.LineNumber,
                            pl.LineName,
                            AreaName = pl.Area != null ? pl.Area.AreaName : "",
                            CustomerName = pl.Area != null ? pl.Area.CustomerName : ""
                        })
                        .ToListAsync();

                    // Crear lista de respuesta con OEE
                    var response = new List<DailyProductionWithOEEDTO>();

                    // Obtener OEE para cada línea
                    foreach (var productionLine in activeLines)
                    {
                        var result = results.FirstOrDefault(item => item.LineNumber == productionLine.LineNumber);
                        var productionLineId = productionLine.ProductionLinesId;

                        decimal oeePercentage = 0;
                        var oeeResults = await _context.LineOEE
                            .FromSqlRaw("EXEC [dbo].[GetLineOEE] @ProductionLineId = {0}, @TargetDate = {1}, @TargetTime = {2}",
                                productionLineId, dateParam, timeParam)
                            .ToListAsync();

                        oeePercentage = oeeResults.FirstOrDefault()?.OeePercentage ?? 0;

                        response.Add(new DailyProductionWithOEEDTO
                        {
                            LineId = productionLineId,
                            AreaCustomerName = result?.AreaCustomerName
                                ?? productionLine.CustomerName
                                ?? productionLine.AreaName
                                ?? "",
                            LineNumber = productionLine.LineNumber ?? 0,
                            LineName = productionLine.LineName,
                            ProductionDate = result?.ProductionDate ?? dateParam,
                            HourInterval = result?.HourInterval ?? "",
                            GoalPieces = result?.GoalPieces ?? 0,
                            ProducedPieces = result?.ProducedPieces ?? 0,
                            RejectedPieces = result?.RejectedPieces ?? 0,
                            AccumulatedRejections = result?.AccumulatedRejections ?? 0,
                            DowntimeMinutes = result?.DowntimeMinutes ?? 0,
                            AccumulatedDowntime = result?.AccumulatedDowntime ?? 0,
                            AccumulatedBalance = result?.AccumulatedBalance ?? 0,
                            EstimatedGoalPieces = result?.EstimatedGoalPieces ?? 0,
                            RequirementGoalPieces = result?.RequirementGoalPieces ?? 0,
                            RequirementBalance = result?.RequirementBalance ?? 0,
                            QualityPercentage = result?.QualityPercentage ?? 0,
                            OEEPercentage = oeePercentage
                        });

                    }
                    return Ok(response);
                }
                else
                {
                   
                    var activeLines = await _context.ProductionLines
                        .Where(pl => pl.IsActive)
                        .Select(pl => new
                        {
                            pl.AreaId,
                            pl.LineNumber,
                            pl.ProductionLinesId,
                            pl.LineName,
                            AreaName = pl.Area != null ? pl.Area.AreaName : "",
                            CustomerName = pl.Area != null ? pl.Area.CustomerName : ""
                        })
                        .ToListAsync();

                    var response = new List<DailyProductionWithOEEDTO>();

                    foreach (var line in activeLines)
                    {
                        var results = await _context.DailyProduction
                            .FromSqlRaw("EXEC [dbo].[GetDailyProductionByArea] @AreaID = {0}, @TargetDate = {1}, @TargetTime = {2}",
                                line.AreaId, dateParam, timeParam)
                            .ToListAsync();

                        var match = results.FirstOrDefault(r => r.LineNumber == line.LineNumber);

                        decimal oee = 0;

                        var oeeResult = await _context.LineOEE
                            .FromSqlRaw("EXEC [dbo].[GetLineOEE] @ProductionLineId = {0}, @TargetDate = {1}, @TargetTime = {2}",
                                line.ProductionLinesId, dateParam, timeParam)
                            .ToListAsync();

                        oee = oeeResult.FirstOrDefault()?.OeePercentage ?? 0;

                        response.Add(new DailyProductionWithOEEDTO
                        {
                            LineId = line.ProductionLinesId,
                            AreaCustomerName = match?.AreaCustomerName
                                ?? line.CustomerName
                                ?? line.AreaName
                                ?? "",
                            LineNumber = line.LineNumber ?? 0,
                            LineName = line.LineName,
                            ProductionDate = match?.ProductionDate ?? dateParam,
                            HourInterval = match?.HourInterval ?? "",
                            GoalPieces = match?.GoalPieces ?? 0,
                            ProducedPieces = match?.ProducedPieces ?? 0,
                            RejectedPieces = match?.RejectedPieces ?? 0,
                            AccumulatedRejections = match?.AccumulatedRejections ?? 0,
                            DowntimeMinutes = match?.DowntimeMinutes ?? 0,
                            AccumulatedDowntime = match?.AccumulatedDowntime ?? 0,
                            AccumulatedBalance = match?.AccumulatedBalance ?? 0,
                            EstimatedGoalPieces = match?.EstimatedGoalPieces ?? 0,
                            RequirementGoalPieces = match?.RequirementGoalPieces ?? 0,
                            RequirementBalance = match?.RequirementBalance ?? 0,
                            QualityPercentage = match?.QualityPercentage ?? 0,
                            OEEPercentage = oee
                        });
                    }

                    return Ok(response);
                }
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


    }
}

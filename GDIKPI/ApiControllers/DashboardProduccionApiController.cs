
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

                var results = await _context.DailyProduction
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
                        var productionLine = await _context.ProductionLines
                            .Where(pl => pl.LineNumber == result.LineNumber && pl.AreaId == areaId)
                            .Select(pl => new { pl.ProductionLinesId, pl.LineName })
                            .FirstOrDefaultAsync();

                        var productionLineId = productionLine?.ProductionLinesId ?? 0;
                        var lineName = productionLine?.LineName;

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
                            LineId = productionLineId,
                            AreaCustomerName = result.AreaCustomerName,
                            LineNumber = result.LineNumber,
                            LineName = lineName,
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
                else
                {
                   
                    var activeLines = await _context.ProductionLines
                        .Where(pl => pl.IsActive)
                        .Select(pl => new { pl.AreaId, pl.LineNumber, pl.ProductionLinesId, pl.LineName })
                        .ToListAsync();

                    var response = new List<DailyProductionWithOEEDTO>();

                    foreach (var line in activeLines)
                    {
                        var results = await _context.DailyProduction
                            .FromSqlRaw("EXEC [dbo].[GetDailyProductionByArea] @AreaID = {0}, @TargetDate = {1}, @TargetTime = {2}",
                                line.AreaId, dateParam, timeParam)
                            .ToListAsync();

                        var match = results.FirstOrDefault(r => r.LineNumber == line.LineNumber);

                        if (match == null) continue;

                        decimal oee = 0;

                        var oeeResult = await _context.LineOEE
                            .FromSqlRaw("EXEC [dbo].[GetLineOEE] @ProductionLineId = {0}, @TargetDate = {1}, @TargetTime = {2}",
                                line.ProductionLinesId, dateParam, timeParam)
                            .ToListAsync();

                        oee = oeeResult.FirstOrDefault()?.OeePercentage ?? 0;

                        response.Add(new DailyProductionWithOEEDTO
                        {
                            LineId = line.ProductionLinesId,
                            AreaCustomerName = match.AreaCustomerName,
                            LineNumber = match.LineNumber,
                            LineName = line.LineName,
                            ProducedPieces = match.ProducedPieces,
                            RequirementGoalPieces = match.RequirementGoalPieces,
                            RequirementBalance = match.RequirementBalance,
                            RejectedPieces = match.RejectedPieces,
                            DowntimeMinutes = match.DowntimeMinutes,
                            QualityPercentage = match.QualityPercentage,
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using GDIKPI.Data;
using GDIKPI.Models;
using GDIKPI.Hubs;
using GDIKPI.DTO;

namespace GDIKPI.ApiControllers
{
    /// <summary>
    /// API Controller para Scanner Production
    /// Maneja las peticiones relacionadas con el escaneo de producción y consultas a API externa
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ScannerProductionApiController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly KpisContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly string _externalApiBaseUrl;

        public ScannerProductionApiController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            KpisContext context,
            IHubContext<DashboardHub> hubContext)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _context = context;
            _hubContext = hubContext;
            // Leer la URL del API externa desde configuración (para mejor mantenibilidad)
            _externalApiBaseUrl = _configuration["ExternalApi:BaseUrl"] ?? "http://192.168.1.1:9091";
        }

        /// <summary>
        /// Obtiene información de un número de parte desde el API externa
        /// </summary>
        /// <param name="partNumber">Número de parte a buscar</param>
        /// <returns>Información del part number</returns>
        [HttpGet("GetPartNumber")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPartNumber([FromQuery] string partNumber)
        {
            try
            {
                // Validación de entrada
                if (string.IsNullOrWhiteSpace(partNumber))
                {
                    return BadRequest(new { error = "El parámetro partNumber es requerido" });
                }

                // URL del API externo
                var externalApiUrl = $"{_externalApiBaseUrl}/GetPartNumber?partNumber={Uri.EscapeDataString(partNumber)}";

                // Crear cliente HTTP
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Realizar petición
                var response = await httpClient.GetAsync(externalApiUrl);

                // Manejo de respuestas
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound(new
                        {
                            error = $"No se encontró el número de parte: {partNumber}",
                            partNumber = partNumber
                        });
                    }
                    return StatusCode((int)response.StatusCode, new
                    {
                        error = "Error al consultar el API externo",
                        statusCode = (int)response.StatusCode
                    });
                }

                var data = await response.Content.ReadAsStringAsync();
                return Content(data, "application/json");
            }
            catch (TaskCanceledException ex)
            {
                return StatusCode(408, new
                {
                    error = "Tiempo de espera agotado al consultar el API externo",
                    details = ex.Message
                });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(503, new
                {
                    error = "No se pudo conectar con el API externo",
                    details = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Error interno al consultar el API externo",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene programas filtrados por área/customer desde el API externa
        /// </summary>
        /// <param name="area">Nombre del área o customer</param>
        /// <returns>Lista de programas</returns>
        [HttpGet("GetPrograms")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPrograms([FromQuery] string area)
        {
            try
            {
                // Si no se proporciona área, devolver mensaje
                if (string.IsNullOrWhiteSpace(area))
                {
                    return BadRequest(new { error = "El parámetro area es requerido" });
                }

                // URL del API externo
                var externalApiUrl = $"{_externalApiBaseUrl}/GetProgram?area={Uri.EscapeDataString(area)}";

                // Crear cliente HTTP
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Realizar petición
                var response = await httpClient.GetAsync(externalApiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        error = "Error al consultar programas del API externo",
                        statusCode = (int)response.StatusCode
                    });
                }

                var data = await response.Content.ReadAsStringAsync();
                return Content(data, "application/json");
            }
            catch (TaskCanceledException ex)
            {
                return StatusCode(408, new
                {
                    error = "Tiempo de espera agotado al consultar programas",
                    details = ex.Message
                });
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(503, new
                {
                    error = "No se pudo conectar con el API externo",
                    details = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Error interno al consultar programas",
                    details = ex.Message
                });
            }
        }

        [HttpGet("ValidateEmployeeNumber")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateEmployeeNumber([FromQuery] string employeeNumber)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(employeeNumber))
                {
                    return BadRequest(new { error = "El parametro employeeNumber es requerido" });
                }

                var normalizedEmployeeNumber = employeeNumber.Trim();
                var exists = await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.EmployeeNumber == normalizedEmployeeNumber);

                return Ok(new
                {
                    exists,
                    employeeNumber = normalizedEmployeeNumber
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Error interno al validar el numero de empleado",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Health check del API externa
        /// </summary>
        /// <returns>Estado del API externa</returns>
        [HttpGet("HealthCheck")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> HealthCheck()
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                var response = await httpClient.GetAsync(_externalApiBaseUrl);

                if (response.IsSuccessStatusCode)
                {
                    return Ok(new
                    {
                        status = "healthy",
                        externalApiUrl = _externalApiBaseUrl,
                        timestamp = DateTime.Now
                    });
                }

                return StatusCode(503, new
                {
                    status = "unhealthy",
                    externalApiUrl = _externalApiBaseUrl,
                    statusCode = (int)response.StatusCode,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new
                {
                    status = "unhealthy",
                    externalApiUrl = _externalApiBaseUrl,
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// Guarda un escaneo en la base de datos
        /// </summary>
        /// <param name="request">Datos del escaneo</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("SaveScan")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SaveScan([FromBody] SaveScanRequest request)
        {
            try
            {
                // Validación de entrada
                if (request == null)
                {
                    return BadRequest(new { error = "Los datos del escaneo son requeridos" });
                }

                if (request.LineId <= 0)
                {
                    return BadRequest(new { error = "LineId es requerido y debe ser mayor a 0" });
                }

                if (string.IsNullOrWhiteSpace(request.ScannerValue))
                {
                    return BadRequest(new { error = "ScannerValue es requerido" });
                }

                // Actualizar o insertar en ProductionData
                var now = DateTime.Now;
                var currentDate = DateOnly.FromDateTime(now);
                var currentTime = TimeOnly.FromDateTime(now);
                var startHour = new TimeOnly(now.Hour, 0, 0);
                var endHour = startHour.AddHours(1);

                // Resolver ProgramId: usar el enviado; si no viene, intentar obtenerlo del intervalo actual
                int effectiveProgramId = request.ProgramId ?? 0;
                string? effectiveProgramDescription = request.ProgramDescription;

                if (effectiveProgramId <= 0)
                {
                    var activeProductionData = await _context.ProductionData
                        .Where(pd => pd.ProductionLinesId == request.LineId
                                  && pd.ProductionDate == currentDate
                                  && pd.StartHour <= currentTime
                                  && pd.EndHour > currentTime)
                        .OrderByDescending(pd => pd.ProductionId)
                        .FirstOrDefaultAsync();

                    if (activeProductionData != null)
                    {
                        effectiveProgramId = activeProductionData.ProgramId;
                        if (string.IsNullOrWhiteSpace(effectiveProgramDescription))
                        {
                            effectiveProgramDescription = activeProductionData.ProgramDescription;
                        }
                    }
                }

                if (effectiveProgramId <= 0)
                {
                    var latestLineProductionData = await _context.ProductionData
                        .Where(pd => pd.ProductionLinesId == request.LineId)
                        .OrderByDescending(pd => pd.ProductionDate)
                        .ThenByDescending(pd => pd.EndHour)
                        .FirstOrDefaultAsync();

                    if (latestLineProductionData != null)
                    {
                        effectiveProgramId = latestLineProductionData.ProgramId;
                        if (string.IsNullOrWhiteSpace(effectiveProgramDescription))
                        {
                            effectiveProgramDescription = latestLineProductionData.ProgramDescription;
                        }
                    }
                }

                if (effectiveProgramId <= 0)
                {
                    // Fallback definitivo cuando no hay contexto de programa en la línea
                    effectiveProgramId = 0;
                    if (string.IsNullOrWhiteSpace(effectiveProgramDescription))
                    {
                        effectiveProgramDescription = "SIN_PROGRAMA";
                    }
                }

                // PartNumber es opcional desde UI, pero en DB es requerido.
                var effectivePartNumber = string.IsNullOrWhiteSpace(request.PartNumber)
                    ? "SIN_PARTE"
                    : request.PartNumber.Trim();

                // Crear el registro de escaneo
                var scannerProduction = new ScannerProduction
                {
                    ScannerProductionDateTime = now,
                    LineId = request.LineId,
                    PartNumber = effectivePartNumber,
                    ScannerValue = request.ScannerValue
                };

                _context.ScannerProductions.Add(scannerProduction);

                // Buscar el registro de ProductionData correspondiente al intervalo de hora actual
                var productionData = await _context.ProductionData
                    .Where(pd => pd.ProductionLinesId == request.LineId
                              && pd.ProductionDate == currentDate
                              && pd.ProgramId == effectiveProgramId
                              && pd.StartHour <= currentTime
                              && pd.EndHour > currentTime)
                    .FirstOrDefaultAsync();

                if (productionData != null)
                {
                    // Si existe el registro, incrementar ProducedPieces
                    productionData.ProducedPieces = (productionData.ProducedPieces ?? 0) + 1;

                    // Actualizar ProgramDescription si se proporcionó y no existe
                    if (!string.IsNullOrWhiteSpace(effectiveProgramDescription) &&
                        string.IsNullOrWhiteSpace(productionData.ProgramDescription))
                    {
                        productionData.ProgramDescription = effectiveProgramDescription;
                    }
                }
                else
                {
                    productionData = new ProductionDatum
                    {
                        ProductionLinesId = request.LineId,
                        ProductionDate = currentDate,
                        StartHour = startHour,
                        EndHour = endHour,
                        ProducedPieces = 1,
                        RejectedPieces = 0,
                        ProgramId = effectiveProgramId,
                        ProgramDescription = effectiveProgramDescription
                    };

                    _context.ProductionData.Add(productionData);
                }

                await _context.SaveChangesAsync();

                // 🔥 BROADCAST DUAL: Dashboard de área + Scanner de línea
                await BroadcastProductionUpdate(
                    lineId: request.LineId,
                    areaId: null, // Se obtendrá automáticamente
                    eventType: "SCANNER_SCAN",
                    additionalData: new
                    {
                        scannerValue = request.ScannerValue,
                        partNumber = effectivePartNumber,
                        programDescription = effectiveProgramDescription
                    }
                );

                return Ok(new
                {
                    success = true,
                    message = "Escaneo guardado exitosamente",
                    data = new
                    {
                        scannerProductionId = scannerProduction.ScannerProductionId,
                        scannerProductionDateTime = scannerProduction.ScannerProductionDateTime,
                        lineId = scannerProduction.LineId,
                        partNumber = scannerProduction.PartNumber,
                        scannerValue = scannerProduction.ScannerValue,
                        productionData = new
                        {
                            productionId = productionData.ProductionId,
                            producedPieces = productionData.ProducedPieces,
                            startHour = productionData.StartHour.ToString("HH:mm"),
                            endHour = productionData.EndHour.ToString("HH:mm")
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error al guardar el escaneo",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Elimina el último escaneo de una línea de producción específica
        /// </summary>
        /// <param name="lineId">ID de la línea de producción</param>
        /// <returns>Resultado de la operación</returns>
        [HttpDelete("DeleteLastScan")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteLastScan([FromQuery] int lineId)
        {
            try
            {
                if (lineId <= 0)
                {
                    return BadRequest(new { error = "LineId es requerido y debe ser mayor a 0" });
                }

                // Buscar el último escaneo de la línea
                var lastScan = await _context.ScannerProductions
                    .Where(s => s.LineId == lineId)
                    .OrderByDescending(s => s.ScannerProductionDateTime)
                    .FirstOrDefaultAsync();

                if (lastScan == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        error = "No se encontró ningún escaneo para eliminar"
                    });
                }

                _context.ScannerProductions.Remove(lastScan);

                // Actualizar ProductionData (decrementar ProducedPieces)
                var scanDateTime = lastScan.ScannerProductionDateTime;
                var scanDate = DateOnly.FromDateTime(scanDateTime);
                var scanTime = TimeOnly.FromDateTime(scanDateTime);

                var productionDataToUpdate = await _context.ProductionData
                    .Where(pd => pd.ProductionLinesId == lineId
                              && pd.ProductionDate == scanDate
                              && pd.StartHour <= scanTime
                              && pd.EndHour > scanTime)
                    .FirstOrDefaultAsync();

                if (productionDataToUpdate != null && productionDataToUpdate.ProducedPieces > 0)
                {
                    productionDataToUpdate.ProducedPieces--;
                }

                await _context.SaveChangesAsync();

                // 🔥 BROADCAST DUAL: Dashboard de área + Scanner de línea
                await BroadcastProductionUpdate(
                    lineId: lineId,
                    areaId: null,
                    eventType: "SCANNER_DELETE",
                    additionalData: new
                    {
                        producedPieces = productionDataToUpdate?.ProducedPieces ?? 0
                    }
                );

                return Ok(new
                {
                    success = true,
                    message = "Escaneo eliminado exitosamente",
                    data = new
                    {
                        scannerProductionId = lastScan.ScannerProductionId,
                        scannerValue = lastScan.ScannerValue
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error al eliminar el escaneo",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene el conteo de escaneos del día actual para una línea de producción
        /// </summary>
        /// <param name="lineId">ID de la línea de producción</param>
        /// <param name="partNumber">Número de parte (opcional)</param>
        /// <returns>Conteo de escaneos</returns>
        [HttpGet("GetTodayScans")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTodayScans([FromQuery] int lineId, [FromQuery] string? partNumber = null)
        {
            try
            {
                if (lineId <= 0)
                {
                    return BadRequest(new { error = "LineId es requerido y debe ser mayor a 0" });
                }

                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                var query = _context.ScannerProductions
                    .Where(s => s.LineId == lineId && s.ScannerProductionDateTime >= today && s.ScannerProductionDateTime < tomorrow);

                // Filtrar por número de parte si se proporciona
                if (!string.IsNullOrWhiteSpace(partNumber))
                {
                    query = query.Where(s => s.PartNumber == partNumber);
                }

                var totalScans = await query.CountAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        lineId = lineId,
                        partNumber = partNumber ?? "Todos",
                        date = today,
                        totalScans = totalScans
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error al consultar escaneos",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Valida si un código ya fue escaneado previamente en la tabla ScannerProduction
        /// </summary>
        /// <param name="scannerValue">Valor del código a validar</param>
        /// <param name="lineId">ID de la línea de producción (opcional)</param>
        /// <returns>Indica si el código ya fue escaneado y los detalles del escaneo previo</returns>
        [HttpGet("ValidateScannerValue")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ValidateScannerValue([FromQuery] string scannerValue, [FromQuery] int? lineId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(scannerValue))
                {
                    return BadRequest(new { error = "ScannerValue es requerido" });
                }

                // Buscar si el valor ya fue escaneado
                var query = _context.ScannerProductions
                    .Where(s => s.ScannerValue == scannerValue);

                // Si se proporciona lineId, filtrar por línea
                if (lineId.HasValue && lineId.Value > 0)
                {
                    query = query.Where(s => s.LineId == lineId.Value);
                }

                var existingScan = await query
                    .OrderByDescending(s => s.ScannerProductionDateTime)
                    .FirstOrDefaultAsync();

                if (existingScan != null)
                {
                    var lineNumber = await _context.ProductionLines
                        .Where(pl => pl.ProductionLinesId == existingScan.LineId)
                        .Select(pl => pl.LineNumber)
                        .FirstOrDefaultAsync();

                    // El código ya fue escaneado
                    return Ok(new
                    {
                        isValid = false,
                        alreadyScanned = true,
                        message = "Este código ya fue escaneado previamente",
                        scanDetails = new
                        {
                            scannerProductionId = existingScan.ScannerProductionId,
                            scannerValue = existingScan.ScannerValue,
                            partNumber = existingScan.PartNumber,
                            lineId = existingScan.LineId,
                            lineNumber = lineNumber?.ToString() ?? "N/A",
                            scanDateTime = existingScan.ScannerProductionDateTime
                        }
                    });
                }

                // El código no ha sido escaneado, es válido
                return Ok(new
                {
                    isValid = true,
                    alreadyScanned = false,
                    message = "El código es válido y no ha sido escaneado previamente"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error al validar el código escaneado",
                    details = ex.Message
                });
            }

        }

        /// <summary>
        /// Obtiene las métricas de producción para una línea específica usando GetLineOEE
        /// (requerimiento, eficiencia, rechazos, piezas producidas)
        /// </summary>
        /// <param name="lineId">ID de la línea de producción</param>
        /// <param name="targetDate">Fecha objetivo (opcional, por defecto hoy)</param>
        /// <param name="targetTime">Hora objetivo (opcional, por defecto hora actual)</param>
        /// <returns>Métricas de producción con OEE</returns>
        [HttpGet("GetLineMetrics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetLineMetrics(
            [FromQuery] int lineId,
            [FromQuery] DateTime? targetDate = null,
            [FromQuery] TimeOnly? targetTime = null)
        {
            try
            {
                if (lineId <= 0)
                {
                    return BadRequest(new { error = "LineId es requerido y debe ser mayor a 0" });
                }

                // Verificar que la línea esté activa
                var productionLine = await _context.ProductionLines
                    .Include(pl => pl.Area)
                    .FirstOrDefaultAsync(pl => pl.ProductionLinesId == lineId && pl.IsActive);

                if (productionLine == null)
                {
                    return NotFound(new { error = "Línea de producción no encontrada o inactiva" });
                }

                var dateParam = targetDate?.Date ?? DateTime.Today;
                var timeParam = targetTime?.ToTimeSpan() ?? DateTime.Now.TimeOfDay;

                // Ejecutar SP GetLineOEE para obtener todas las métricas
                var oeeResults = await _context.LineOEE
                    .FromSqlRaw("EXEC [dbo].[GetLineOEE] @ProductionLineId = {0}, @TargetDate = {1}, @TargetTime = {2}",
                        lineId, dateParam, timeParam)
                    .ToListAsync();

                var oeeData = oeeResults.FirstOrDefault();

                if (oeeData == null)
                {
                    return Ok(new
                    {
                        lineId = lineId,
                        lineNumber = productionLine.LineNumber,
                        areaCustomerName = productionLine.Area?.CustomerName ?? "N/A",
                        standardTime = productionLine.StandardTime ?? 0m,
                        personalQuantity = productionLine.PersonalQuantity,
                        availableMinutes = 0,
                        requirementGoalPieces = 0,
                        estimatedRequirement = 0,
                        producedPieces = 0,
                        rejectedPieces = 0,
                        downtimeMinutes = 0,
                        availabilityPercentage = 0m,
                        performancePercentage = 0m,
                        qualityPercentage = 0m,
                        oeePercentage = 0m,
                        plannedMinutes = 0,
                        operatingMinutes = 0
                    });
                }

                return Ok(new
                {
                    lineId = lineId,
                    lineNumber = oeeData.LineNumber,
                    areaCustomerName = oeeData.AreaCustomerName,
                    standardTime = productionLine.StandardTime ?? 0m,
                    personalQuantity = productionLine.PersonalQuantity,
                    availableMinutes = oeeData.PlannedMinutes,
                    requirementGoalPieces = oeeData.RequirementGoalPieces,
                    estimatedRequirement = oeeData.EstimatedRequirement,
                    producedPieces = oeeData.ProducedPieces,
                    rejectedPieces = oeeData.RejectedPieces,
                    downtimeMinutes = oeeData.DowntimeMinutes,
                    availabilityPercentage = oeeData.AvailabilityPercentage,
                    performancePercentage = oeeData.PerformancePercentage,
                    qualityPercentage = oeeData.QualityPercentage,
                    oeePercentage = oeeData.OeePercentage,
                    plannedMinutes = oeeData.PlannedMinutes,
                    operatingMinutes = oeeData.OperatingMinutes
                });
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error de base de datos al obtener métricas",
                    details = sqlEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error al obtener métricas de la línea",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Valida un código de defecto
        /// </summary>
        /// <param name="code">Código del defecto a validar</param>
        /// <param name="areaId">ID del área para filtrar defectos</param>
        /// <returns>Información del defecto si es válido</returns>
        [HttpGet("ValidateDefectCode")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ValidateDefectCode([FromQuery] string code, [FromQuery] int areaId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    return BadRequest(new { error = "El código de defecto es requerido" });
                }

                if (areaId <= 0)
                {
                    return BadRequest(new { error = "AreaId es requerido y debe ser mayor a 0" });
                }

                // Buscar el defecto por código (asumiendo que el código corresponde al DefectId)
                // Si el código es alfanumérico, ajustar según tu lógica de negocio
                int defectId;
                if (!int.TryParse(code, out defectId))
                {
                    return Ok(new
                    {
                        isValid = false,
                        message = "El código de defecto debe ser numérico"
                    });
                }

                var defect = await _context.Defects
                    .Include(d => d.DefectCategory)
                    .FirstOrDefaultAsync(d => d.DefectId == defectId && d.AreaId == areaId);

                if (defect == null)
                {
                    return Ok(new
                    {
                        isValid = false,
                        message = "Código de defecto no encontrado para esta área"
                    });
                }

                return Ok(new
                {
                    isValid = true,
                    defectId = defect.DefectId,
                    defectName = defect.DefectName,
                    areaId = defect.AreaId,
                    category = defect.DefectCategory != null ? defect.DefectCategory.DefectCategoryName : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error al validar el código de defecto",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene el historial de rechazos del día actual para una línea de producción
        /// </summary>
        /// <param name="lineId">ID de la línea de producción</param>
        /// <returns>Historial de rechazos con sus defectos</returns>
        /// <summary>
        /// Obtiene el catÃ¡logo de defectos disponibles para un Ã¡rea
        /// </summary>
        /// <param name="areaId">ID del Ã¡rea</param>
        /// <returns>Lista de defectos ordenada por nombre</returns>
        [HttpGet("GetDefectsByArea")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDefectsByArea([FromQuery] int areaId)
        {
            try
            {
                if (areaId <= 0)
                {
                    return BadRequest(new { error = "AreaId es requerido y debe ser mayor a 0" });
                }

                var defects = await _context.Defects
                    .Include(d => d.DefectCategory)
                    .Where(d => d.AreaId == areaId)
                    .OrderBy(d => d.DefectName)
                    .Select(d => new
                    {
                        defectId = d.DefectId,
                        defectName = d.DefectName,
                        areaId = d.AreaId,
                        category = d.DefectCategory != null ? d.DefectCategory.DefectCategoryName : null
                    })
                    .ToListAsync();

                return Ok(defects);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error al obtener los defectos del Ã¡rea",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene el historial de rechazos del dÃ­a actual para una lÃ­nea de producciÃ³n
        /// </summary>
        /// <param name="lineId">ID de la lÃ­nea de producciÃ³n</param>
        /// <returns>Historial de rechazos con sus defectos</returns>
        [HttpGet("GetRejectionsHistory")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetRejectionsHistory([FromQuery] int lineId)
        {
            try
            {
                if (lineId <= 0)
                {
                    return BadRequest(new { error = "LineId es requerido y debe ser mayor a 0" });
                }

                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);

                // Obtener rechazos del día agrupados por ScannerValue
                var rejections = await _context.DefectsData
                    .Include(dd => dd.Defect)
                    .Where(dd => dd.ProductionLinesId == lineId
                              && dd.StartTime >= today
                              && dd.StartTime < tomorrow
                              && !string.IsNullOrEmpty(dd.ScannerValue))
                    .OrderByDescending(dd => dd.StartTime)
                    .Select(dd => new
                    {
                        defectsDataId = dd.DefectsDataId,
                        scannerValue = dd.ScannerValue,
                        defectId = dd.DefectId,
                        defectName = dd.Defect.DefectName,
                        category = dd.Category,
                        defectQuantity = dd.DefectQuantity,
                        startTime = dd.StartTime,
                        endTime = dd.EndTime,
                        employeeNumber = dd.EmployeeNumber
                    })
                    .ToListAsync();

                // Agrupar por ScannerValue para mostrar rechazos únicos
                var groupedRejections = rejections
                    .GroupBy(r => r.scannerValue)
                    .Select(g => new
                    {
                        scannerValue = g.Key,
                        firstRejectionTime = g.Min(r => r.startTime),
                        defects = g.Select(r => new
                        {
                            defectsDataId = r.defectsDataId,
                            defectId = r.defectId,
                            defectName = r.defectName,
                            category = r.category,
                            defectQuantity = r.defectQuantity,
                            startTime = r.startTime,
                            endTime = r.endTime
                        }).ToList(),
                        totalDefects = g.Count()
                    })
                    .OrderByDescending(g => g.firstRejectionTime)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    lineId = lineId,
                    date = today,
                    totalRejectedPieces = groupedRejections.Count,
                    rejections = groupedRejections
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error al obtener el historial de rechazos",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Elimina un defecto específico del historial de rechazos
        /// </summary>
        /// <param name="defectsDataId">ID del registro en DefectsData</param>
        /// <returns>Resultado de la operación</returns>
        [HttpDelete("DeleteDefectFromRejection")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteDefectFromRejection([FromQuery] int defectsDataId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (defectsDataId <= 0)
                {
                    return BadRequest(new { error = "DefectsDataId es requerido y debe ser mayor a 0" });
                }

                // Buscar el registro de DefectsData
                var defectData = await _context.DefectsData
                    .Include(dd => dd.Rejection)
                    .FirstOrDefaultAsync(dd => dd.DefectsDataId == defectsDataId);

                if (defectData == null)
                {
                    return NotFound(new { error = "Registro de defecto no encontrado" });
                }

                var scannerValue = defectData.ScannerValue;
                var lineId = defectData.ProductionLinesId;
                var rejectionId = defectData.RejectionId;

                // Verificar cuántos defectos tiene esta pieza
                var defectsCount = await _context.DefectsData
                    .Where(dd => dd.ScannerValue == scannerValue
                              && dd.ProductionLinesId == lineId)
                    .CountAsync();

                // Eliminar el registro de DefectsData
                _context.DefectsData.Remove(defectData);

                // Si era el único defecto de esta pieza, decrementar DefectQuantity en Rejection
                if (defectsCount == 1 && rejectionId.HasValue)
                {
                    var rejection = await _context.Rejections
                        .FirstOrDefaultAsync(r => r.RejectionId == rejectionId.Value);

                    if (rejection != null && rejection.DefectQuantity > 0)
                    {
                        rejection.DefectQuantity--;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 🔥 BROADCAST DUAL: Dashboard de área + Scanner de línea
                await BroadcastProductionUpdate(
                    lineId: lineId,
                    areaId: null,
                    eventType: "REJECTION_DELETED",
                    additionalData: null
                );

                return Ok(new
                {
                    success = true,
                    message = "Defecto eliminado exitosamente",
                    data = new
                    {
                        defectsDataId = defectsDataId,
                        scannerValue = scannerValue,
                        wasLastDefect = defectsCount == 1
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error al eliminar el defecto",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Actualiza el defecto de un rechazo específico
        /// </summary>
        /// <param name="request">Datos para actualizar el defecto</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPut("UpdateDefectInRejection")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateDefectInRejection([FromBody] UpdateDefectRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(new { error = "Los datos son requeridos" });
                }

                if (request.DefectsDataId <= 0)
                {
                    return BadRequest(new { error = "DefectsDataId es requerido y debe ser mayor a 0" });
                }

                if (request.NewDefectId <= 0)
                {
                    return BadRequest(new { error = "NewDefectId es requerido y debe ser mayor a 0" });
                }

                // Buscar el registro de DefectsData
                var defectData = await _context.DefectsData
                    .FirstOrDefaultAsync(dd => dd.DefectsDataId == request.DefectsDataId);

                if (defectData == null)
                {
                    return NotFound(new { error = "Registro de defecto no encontrado" });
                }

                // Validar que el nuevo defecto existe
                var newDefect = await _context.Defects
                    .Include(d => d.DefectCategory)
                    .FirstOrDefaultAsync(d => d.DefectId == request.NewDefectId);

                if (newDefect == null)
                {
                    return NotFound(new { error = "El nuevo defecto no existe" });
                }

                // Actualizar el defecto y la categoría
                defectData.DefectId = request.NewDefectId;
                defectData.Category = newDefect.DefectCategory?.DefectCategoryName ?? "Sin categoría";

                await _context.SaveChangesAsync();

                // 🔥 BROADCAST DUAL: Dashboard de área + Scanner de línea
                await BroadcastProductionUpdate(
                    lineId: defectData.ProductionLinesId,
                    areaId: null,
                    eventType: "REJECTION_UPDATED",
                    additionalData: null
                );

                return Ok(new
                {
                    success = true,
                    message = "Defecto actualizado exitosamente",
                    data = new
                    {
                        defectsDataId = defectData.DefectsDataId,
                        newDefectId = defectData.DefectId,
                        newDefectName = newDefect.DefectName,
                        newCategory = defectData.Category
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error al actualizar el defecto",
                    details = ex.Message
                });
            }
        }

        /// <summary>
        /// Guarda un rechazo con sus defectos asociados
        /// LÓGICA CORRECTA:
        /// - Rejections: UN registro por intervalo, DefectQuantity = 1 por pieza
        /// - DefectsData: UN registro por cada DefectId con su categoría
        /// </summary>
        /// <param name="request">Datos del rechazo</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("SaveRejection")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SaveRejection([FromBody] SaveRejectionRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validaciones
                if (request == null)
                {
                    return BadRequest(new { error = "Los datos del rechazo son requeridos" });
                }

                if (request.LineId <= 0)
                {
                    return BadRequest(new { error = "LineId es requerido y debe ser mayor a 0" });
                }

                if (string.IsNullOrWhiteSpace(request.ScannerValue))
                {
                    return BadRequest(new { error = "ScannerValue es requerido" });
                }

                if (string.IsNullOrWhiteSpace(request.EmployeeNumber))
                {
                    return BadRequest(new { error = "EmployeeNumber es requerido" });
                }

                if (request.Defects == null || !request.Defects.Any())
                {
                    return BadRequest(new { error = "Debe proporcionar al menos un defecto" });
                }

                // Obtener información de la línea de producción
                var productionLine = await _context.ProductionLines
                    .Include(pl => pl.Area)
                    .FirstOrDefaultAsync(pl => pl.ProductionLinesId == request.LineId);

                if (productionLine == null)
                {
                    return NotFound(new { error = "Línea de producción no encontrada" });
                }

                // Usar el número de empleado capturado en modal
                var employeeNumber = request.EmployeeNumber.Trim();

                var now = DateTime.Now;
                var currentDate = DateOnly.FromDateTime(now);
                var currentTime = TimeOnly.FromDateTime(now);

                // Calcular el intervalo de hora
                var startHour = new TimeOnly(now.Hour, 0, 0);
                var endHour = startHour.AddHours(1);
                var startDateTime = now.Date.Add(startHour.ToTimeSpan());
                var endDateTime = now.Date.Add(endHour.ToTimeSpan());

                // Resolver ProgramId: usar el enviado; si no viene, buscar en el intervalo actual
                int effectiveProgramId = request.ProgramId ?? 0;
                string? effectiveProgramDescription = request.ProgramDescription;

                if (effectiveProgramId <= 0)
                {
                    var activeProductionData = await _context.ProductionData
                        .Where(pd => pd.ProductionLinesId == request.LineId
                                  && pd.ProductionDate == currentDate
                                  && pd.StartHour <= currentTime
                                  && pd.EndHour > currentTime)
                        .OrderByDescending(pd => pd.ProductionId)
                        .FirstOrDefaultAsync();

                    if (activeProductionData != null)
                    {
                        effectiveProgramId = activeProductionData.ProgramId;
                        if (string.IsNullOrWhiteSpace(effectiveProgramDescription))
                        {
                            effectiveProgramDescription = activeProductionData.ProgramDescription;
                        }
                    }
                }

                if (effectiveProgramId <= 0)
                {
                    var latestLineProductionData = await _context.ProductionData
                        .Where(pd => pd.ProductionLinesId == request.LineId)
                        .OrderByDescending(pd => pd.ProductionDate)
                        .ThenByDescending(pd => pd.EndHour)
                        .FirstOrDefaultAsync();

                    if (latestLineProductionData != null)
                    {
                        effectiveProgramId = latestLineProductionData.ProgramId;
                        if (string.IsNullOrWhiteSpace(effectiveProgramDescription))
                        {
                            effectiveProgramDescription = latestLineProductionData.ProgramDescription;
                        }
                    }
                }

                if (effectiveProgramId <= 0)
                {
                    // Fallback definitivo si no hay programa configurado para la línea
                    effectiveProgramId = 0;
                    if (string.IsNullOrWhiteSpace(effectiveProgramDescription))
                    {
                        effectiveProgramDescription = "SIN_PROGRAMA";
                    }
                }

                // PASO 1: Verificar si existe en ScannerProduction
                var scannerProduction = await _context.ScannerProductions
                    .Where(sp => sp.ScannerValue == request.ScannerValue && sp.LineId == request.LineId)
                    .OrderByDescending(sp => sp.ScannerProductionDateTime)
                    .FirstOrDefaultAsync();

                bool shouldDecrementProduced = scannerProduction != null;

                // PASO 1.5: Verificar si esta pieza específica ya fue rechazada antes (en cualquier fecha)
                var wasPreviouslyRejected = await _context.DefectsData
                    .AnyAsync(dd => dd.ScannerValue == request.ScannerValue
                                 && dd.ProductionLinesId == request.LineId);

                // PASO 2: Buscar o crear Rejection para el intervalo
                // DefectQuantity = 1 por cada PIEZA rechazada (no cantidad de defectos)
                var rejection = await _context.Rejections
                    .FirstOrDefaultAsync(r => r.ProductionLinesId == request.LineId
                                           && r.Category == "Producción" // Categoría estándar
                                           && r.ProgramId == effectiveProgramId
                                           && r.StartTime == startDateTime
                                           && r.EndTime == endDateTime);

                if (rejection == null)
                {
                    // Crear nuevo Rejection para el intervalo
                    rejection = new Rejection
                    {
                        ProductionLinesId = request.LineId,
                        Category = "Producción",
                        EmployeeNumber = employeeNumber,
                        DefectQuantity = wasPreviouslyRejected ? 0 : 1, // Solo incrementar si es primera vez
                        StartTime = startDateTime,
                        EndTime = endDateTime,
                        ProgramId = effectiveProgramId,
                        ProgramDescription = effectiveProgramDescription
                    };

                    _context.Rejections.Add(rejection);
                    await _context.SaveChangesAsync(); // Guardar para obtener RejectionId
                }
                else
                {
                    // Ya existe Rejection, incrementar en 1 solo si NO fue rechazada antes
                    if (!wasPreviouslyRejected)
                    {
                        rejection.DefectQuantity = (rejection.DefectQuantity ?? 0) + 1;
                    }
                }

                // PASO 4: Obtener información de los defectos con sus categorías
                var defectIds = request.Defects.Select(d => d.DefectId).ToList();
                var defectsInfo = await _context.Defects
                    .Include(d => d.DefectCategory)
                    .Where(d => defectIds.Contains(d.DefectId))
                    .ToListAsync();

                // PASO 5: Crear UN registro en DefectsData por cada DefectId
                foreach (var defect in request.Defects)
                {
                    var defectInfo = defectsInfo.FirstOrDefault(d => d.DefectId == defect.DefectId);
                    var categoryName = defectInfo?.DefectCategory?.DefectCategoryName ?? "Sin categoría";

                    // Buscar si ya existe este defecto en el intervalo
                    var existingDefectData = await _context.DefectsData
                        .FirstOrDefaultAsync(dd => dd.RejectionId == rejection.RejectionId
                                                && dd.DefectId == defect.DefectId
                                                && dd.StartTime == startDateTime
                                                && dd.EndTime == endDateTime);

                    if (existingDefectData == null)
                    {
                        // Crear nuevo registro
                        var defectData = new DefectsDatum
                        {
                            RejectionId = rejection.RejectionId,
                            ProductionLinesId = request.LineId,
                            Category = categoryName, // Categoría del defecto
                            EmployeeNumber = employeeNumber,
                            DefectId = defect.DefectId,
                            DefectQuantity = 1, // 1 defecto
                            StartTime = startDateTime,
                            EndTime = endDateTime,
                            ScannerValue = request.ScannerValue // Guardar el valor del scanner
                        };

                        _context.DefectsData.Add(defectData);
                    }
                    else
                    {
                        // Ya existe, incrementar cantidad
                        existingDefectData.DefectQuantity = (existingDefectData.DefectQuantity ?? 0) + 1;
                        // Actualizar también el ScannerValue con el último rechazado
                        existingDefectData.ScannerValue = request.ScannerValue;
                    }
                }

                // PASO 6: Eliminar de ScannerProduction
                if (scannerProduction != null)
                {
                    _context.ScannerProductions.Remove(scannerProduction);
                }

                // PASO 7: Actualizar ProductionData
                // Solo decrementar ProducedPieces, RejectedPieces se calcula desde Rejections
                var productionData = await _context.ProductionData
                    .Where(pd => pd.ProductionLinesId == request.LineId
                              && pd.ProductionDate == currentDate
                              && pd.ProgramId == effectiveProgramId
                              && pd.StartHour <= currentTime
                              && pd.EndHour > currentTime)
                    .FirstOrDefaultAsync();

                if (productionData != null)
                {
                    // Solo decrementar ProducedPieces si estaba en ScannerProduction
                    if (shouldDecrementProduced && productionData.ProducedPieces > 0)
                    {
                        productionData.ProducedPieces--;
                    }
                }
                else
                {
                    // Si no existe ProductionData, crear uno nuevo
                    productionData = new ProductionDatum
                    {
                        ProductionLinesId = request.LineId,
                        ProductionDate = currentDate,
                        StartHour = startHour,
                        EndHour = endHour,
                        ProducedPieces = 0,
                        RejectedPieces = 0, // No se actualiza manualmente
                        ProgramId = effectiveProgramId,
                        ProgramDescription = effectiveProgramDescription
                    };

                    _context.ProductionData.Add(productionData);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 🔥 BROADCAST DUAL: Dashboard de área + Scanner de línea
                await BroadcastProductionUpdate(
                    lineId: request.LineId,
                    areaId: null,
                    eventType: "REJECTION",
                    additionalData: new
                    {
                        scannerValue = request.ScannerValue,
                        defectsCount = request.Defects.Count
                    }
                );

                return Ok(new
                {
                    success = true,
                    message = "Rechazo guardado exitosamente",
                    data = new
                    {
                        rejectionId = rejection.RejectionId,
                        scannerValue = request.ScannerValue,
                        defectsCount = request.Defects.Count,
                        piecesRejectedInInterval = rejection.DefectQuantity,
                        wasInProduction = shouldDecrementProduced,
                        intervalStart = startDateTime.ToString("HH:mm"),
                        intervalEnd = endDateTime.ToString("HH:mm")
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error al guardar el rechazo",
                    details = ex.Message
                });
            }
        }

        // ══════════════════════════════════════════════════════════
        // MÉTODOS HELPER PARA SIGNALR - BROADCAST DUAL
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Envía notificaciones de actualización a AMBOS grupos:
        /// - Dashboard del área (vista general)
        /// - Scanner de línea (vista específica)
        /// </summary>
        private async Task BroadcastProductionUpdate(
            int lineId,
            int? areaId,
            string eventType,
            object? additionalData = null)
        {
            try
            {
                // 1. Obtener información de la línea
                var productionLine = await _context.ProductionLines
                    .Include(pl => pl.Area)
                    .FirstOrDefaultAsync(pl => pl.ProductionLinesId == lineId);

                if (productionLine == null)
                {
                    Console.WriteLine($"[BROADCAST] Línea {lineId} no encontrada");
                    return;
                }

                var effectiveAreaId = areaId ?? productionLine.AreaId ?? 0;

                // 2. Obtener métricas actualizadas usando GetLineOEE
                var dateParam = DateTime.Today;
                var timeParam = DateTime.Now.TimeOfDay;

                var oeeResults = await _context.LineOEE
                    .FromSqlRaw("EXEC [dbo].[GetLineOEE] @ProductionLineId = {0}, @TargetDate = {1}, @TargetTime = {2}",
                        lineId, dateParam, timeParam)
                    .ToListAsync();

                var oeeData = oeeResults.FirstOrDefault();

                // 3. Preparar payload de métricas
                var metricsPayload = new
                {
                    lineId = lineId,
                    lineNumber = oeeData?.LineNumber.ToString() ?? productionLine.LineNumber.ToString(),
                    producedPieces = oeeData?.ProducedPieces ?? 0,
                    rejectedPieces = oeeData?.RejectedPieces ?? 0,
                    efficiency = Math.Round(oeeData?.PerformancePercentage ?? 0, 1),
                    oeePercentage = Math.Round(oeeData?.OeePercentage ?? 0, 1),
                    requirementGoal = oeeData?.RequirementGoalPieces ?? 0,
                    estimatedRequirement = oeeData?.EstimatedRequirement ?? 0,
                    availabilityPercentage = Math.Round(oeeData?.AvailabilityPercentage ?? 0, 1),
                    qualityPercentage = Math.Round(oeeData?.QualityPercentage ?? 0, 1),
                    timestamp = DateTime.Now
                };

                // ══════════════════════════════════════════════════════
                // BROADCAST 1: Dashboard del Área
                // ══════════════════════════════════════════════════════
                if (effectiveAreaId > 0)
                {
                    // Extraer datos adicionales de forma segura
                    string? partNumber = null;
                    string? programDescription = null;
                    string? scannerValue = null;
                    int? defectsCount = null;

                    if (additionalData != null)
                    {
                        var dataType = additionalData.GetType();

                        var partNumberObj = dataType.GetProperty("partNumber")?.GetValue(additionalData);
                        partNumber = partNumberObj?.ToString();

                        var programDescriptionObj = dataType.GetProperty("programDescription")?.GetValue(additionalData);
                        programDescription = programDescriptionObj?.ToString();

                        var scannerValueObj = dataType.GetProperty("scannerValue")?.GetValue(additionalData);
                        scannerValue = scannerValueObj?.ToString();

                        var defectsCountObj = dataType.GetProperty("defectsCount")?.GetValue(additionalData);
                        if (defectsCountObj != null && int.TryParse(defectsCountObj.ToString(), out int dc))
                        {
                            defectsCount = dc;
                        }
                    }

                    await _hubContext.Clients
                        .Group($"Dashboard_{effectiveAreaId}")
                        .SendAsync("ProductionDataUpdated", new
                        {
                            areaId = effectiveAreaId,
                            type = eventType,
                            lineNumber = productionLine.LineNumber,
                            pieces = metricsPayload.producedPieces,
                            rejections = metricsPayload.rejectedPieces,
                            partNumber = partNumber,
                            programDescription = programDescription,
                            defectsCount = defectsCount
                        });

                    Console.WriteLine($"[BROADCAST] Dashboard_{effectiveAreaId} ← {eventType}");
                }

                // ══════════════════════════════════════════════════════
                // BROADCAST 2: Scanner de Línea
                // ══════════════════════════════════════════════════════
                await _hubContext.Clients
                    .Group($"ProductionLine_{lineId}")
                    .SendAsync("UpdateLineMetrics", metricsPayload);

                Console.WriteLine($"[BROADCAST] ProductionLine_{lineId} ← UpdateLineMetrics");

                // Evento adicional específico según el tipo
                if (eventType == "SCANNER_SCAN" && additionalData != null)
                {
                    var dataType = additionalData.GetType();

                    var scannerValueObj = dataType.GetProperty("scannerValue")?.GetValue(additionalData);
                    var scannerValue = scannerValueObj?.ToString();

                    var partNumberObj = dataType.GetProperty("partNumber")?.GetValue(additionalData);
                    var partNumber = partNumberObj?.ToString();

                    await _hubContext.Clients
                        .Group($"ProductionLine_{lineId}")
                        .SendAsync("ScanRegistered", new
                        {
                            scannerValue = scannerValue,
                            partNumber = partNumber,
                            timestamp = DateTime.Now
                        });
                }
                else if (eventType == "REJECTION" && additionalData != null)
                {
                    var dataType = additionalData.GetType();

                    var scannerValueObj = dataType.GetProperty("scannerValue")?.GetValue(additionalData);
                    var scannerValue = scannerValueObj?.ToString();

                    var defectsCountObj = dataType.GetProperty("defectsCount")?.GetValue(additionalData);
                    int? defectsCount = null;
                    if (defectsCountObj != null && int.TryParse(defectsCountObj.ToString(), out int dc))
                    {
                        defectsCount = dc;
                    }

                    await _hubContext.Clients
                        .Group($"ProductionLine_{lineId}")
                        .SendAsync("RejectRegistered", new
                        {
                            scannerValue = scannerValue,
                            defectsCount = defectsCount,
                            timestamp = DateTime.Now
                        });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BROADCAST ERROR] {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Request model para guardar un escaneo
    /// </summary>
    public class SaveScanRequest
    {
        public int LineId { get; set; }
        public string? PartNumber { get; set; }
        public string ScannerValue { get; set; } = string.Empty;
        public int? ProgramId { get; set; }
        public string? ProgramDescription { get; set; }
    }

    /// <summary>
    /// Request model para guardar un rechazo
    /// </summary>
    public class SaveRejectionRequest
    {
        public int LineId { get; set; }
        public string EmployeeNumber { get; set; } = string.Empty;
        public string ScannerValue { get; set; } = string.Empty;
        public int? ProgramId { get; set; }
        public string? ProgramDescription { get; set; }
        public List<DefectInfo> Defects { get; set; } = new List<DefectInfo>();
    }

    /// <summary>
    /// Información de un defecto
    /// </summary>
    public class DefectInfo
    {
        public int DefectId { get; set; }
        public string DefectName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model para actualizar un defecto en un rechazo
    /// </summary>
    public class UpdateDefectRequest
    {
        public int DefectsDataId { get; set; }
        public int NewDefectId { get; set; }
    }
}

using GDIKPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace GDIKPI.Services
{
    public class DailyOEESchedulerService : IHostedService, IDisposable
    {
        private readonly ILogger<DailyOEESchedulerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private Timer? _timer;

        public DailyOEESchedulerService(
            ILogger<DailyOEESchedulerService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DailyOEESchedulerService iniciado");

            // Configurar el timer para verificar cada minuto si es hora de ejecutar
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            var now = DateTime.Now;

            // Verificar si es día de semana (lunes a viernes)
            if (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
            {
                return;
            }

            // Verificar si es las 18:00 (hora exacta)
            if (now.Hour == 17 && now.Minute == 10)
            {
                _logger.LogInformation("Iniciando guardado automático de OEE a las {Time}", now);

                try
                {
                    await SaveDailyOEE();
                    _logger.LogInformation("Guardado automático de OEE completado exitosamente");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al guardar automáticamente el OEE");
                }
            }
        }

        private async Task SaveDailyOEE()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<KpisContext>();

            var dateParam = DateTime.Today;
            var timeParam = DateTime.Now.TimeOfDay;

            // Obtener todas las líneas activas
            var activeLines = await context.ProductionLines
                .Where(pl => pl.IsActive)
                .ToListAsync();

            if (!activeLines.Any())
            {
                _logger.LogWarning("No hay líneas activas para procesar");
                return;
            }

            var savedRecords = 0;
            var errors = new List<string>();

            // Procesar cada línea activa
            foreach (var line in activeLines)
            {
                try
                {
                    // Ejecutar el SP GetLineOEE para cada línea
                    var oeeResults = await context.LineOEE
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
                        context.Oeehistories.Add(oeeHistory);
                        savedRecords++;

                        _logger.LogInformation("OEE guardado para línea {LineNumber}", line.LineNumber);
                    }
                    else
                    {
                        _logger.LogWarning("No se encontraron datos de OEE para línea {LineNumber}", line.LineNumber);
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Error procesando línea {line.LineNumber}: {ex.Message}";
                    errors.Add(errorMsg);
                    _logger.LogError(ex, errorMsg);
                }
            }

            // Guardar todos los cambios
            await context.SaveChangesAsync();

            _logger.LogInformation(
                "Proceso completado: {SavedRecords} registros guardados de {TotalLines} líneas activas",
                savedRecords, activeLines.Count);

            if (errors.Any())
            {
                _logger.LogWarning("Se encontraron {ErrorCount} errores durante el proceso", errors.Count);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DailyOEESchedulerService detenido");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}

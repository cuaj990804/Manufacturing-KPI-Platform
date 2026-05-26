using GDIKPI.Data;
using Microsoft.EntityFrameworkCore;

namespace GDIKPI.Services
{
    public class DailyEfficiencySchedulerService : IHostedService, IDisposable
    {
        private readonly ILogger<DailyEfficiencySchedulerService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private Timer? _timer;

        public DailyEfficiencySchedulerService(
            ILogger<DailyEfficiencySchedulerService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DailyEfficiencySchedulerService iniciado");

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

            // Verificar si es las 17:10 (5:10 PM)
            if (now.Hour == 17 && now.Minute == 10)
            {
                _logger.LogInformation("Iniciando guardado automático de Eficiencia a las {Time}", now);

                try
                {
                    await SaveDailyEfficiency();
                    _logger.LogInformation("Guardado automático de Eficiencia completado exitosamente");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al guardar automáticamente la Eficiencia");
                }
            }
        }

        private async Task SaveDailyEfficiency()
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
                    // Ejecutar el SP GetLineOEE para obtener los datos de producción
                    var oeeResults = await context.LineOEE
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

                        // Insertar Efficiency en la base de datos
                        context.Efficiencies.Add(efficiency);
                        savedRecords++;

                        _logger.LogInformation("Eficiencia guardada para línea {LineNumber}: {Eficiency}%", line.LineNumber, eficienciaPercentage);
                    }
                    else
                    {
                        _logger.LogWarning("No se encontraron datos para línea {LineNumber}", line.LineNumber);
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
                "Proceso completado: {SavedRecords} registros de eficiencia guardados de {TotalLines} líneas activas",
                savedRecords, activeLines.Count);

            if (errors.Any())
            {
                _logger.LogWarning("Se encontraron {ErrorCount} errores durante el proceso", errors.Count);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DailyEfficiencySchedulerService detenido");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}

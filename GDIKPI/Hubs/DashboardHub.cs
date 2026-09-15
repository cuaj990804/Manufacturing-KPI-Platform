using Microsoft.AspNetCore.SignalR;

namespace GDIKPI.Hubs
{
    public class DashboardHub : Hub
    {
        private static readonly object ProductionLinesScannerStateLock = new();
        private static ProductionLinesScannerViewState? _latestProductionLinesScannerState;

        // ══════════════════════════════════════════════════════
        // GRUPOS PARA DASHBOARD DE ÁREA (ya los tienes)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Une al cliente a un grupo de ÁREA (para Dashboard Production)
        /// </summary>
        public async Task JoinDashboardGroup(string areaId)
        {
            var groupName = $"Dashboard_{areaId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            Console.WriteLine($"[SignalR] {Context.ConnectionId} → {groupName}");
        }

        /// <summary>
        /// Remueve al cliente del grupo de área
        /// </summary>
        public async Task LeaveDashboardGroup(string areaId)
        {
            var groupName = $"Dashboard_{areaId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            Console.WriteLine($"[SignalR] {Context.ConnectionId} ← {groupName}");
        }

        // ══════════════════════════════════════════════════════
        // GRUPOS PARA SCANNER DE LÍNEA (NUEVO)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Une al cliente a un grupo de LÍNEA (para Scanner Production)
        /// </summary>
        public async Task JoinProductionLineGroup(string productionLineId)
        {
            var groupName = $"ProductionLine_{productionLineId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            Console.WriteLine($"[SignalR] {Context.ConnectionId} → {groupName}");
        }

        /// <summary>
        /// Remueve al cliente del grupo de línea
        /// </summary>
        public async Task LeaveProductionLineGroup(string productionLineId)
        {
            var groupName = $"ProductionLine_{productionLineId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            Console.WriteLine($"[SignalR] {Context.ConnectionId} ← {groupName}");
        }

        public async Task BroadcastProductionLinesScannerViewState(ProductionLinesScannerViewState state)
        {
            var normalizedState = NormalizeProductionLinesScannerViewState(state);

            lock (ProductionLinesScannerStateLock)
            {
                _latestProductionLinesScannerState = normalizedState;
            }

            await Clients.Others.SendAsync("ProductionLinesScannerViewStateChanged", normalizedState);
        }

        public ProductionLinesScannerViewState? GetProductionLinesScannerViewState()
        {
            lock (ProductionLinesScannerStateLock)
            {
                return _latestProductionLinesScannerState;
            }
        }

        private static ProductionLinesScannerViewState NormalizeProductionLinesScannerViewState(
            ProductionLinesScannerViewState? state)
        {
            state ??= new ProductionLinesScannerViewState();
            var feedbackDurationMs = Math.Clamp(state.FeedbackDurationMs, 0, 5000);

            return new ProductionLinesScannerViewState
            {
                LineValue = Truncate(state.LineValue, 50),
                VolanteValue = Truncate(state.VolanteValue, 500),
                LineValid = state.LineValid,
                ValidatedLineNumber = state.ValidatedLineNumber > 0 ? state.ValidatedLineNumber : null,
                ValidatedLineId = state.ValidatedLineId > 0 ? state.ValidatedLineId : null,
                CurrentBatchQuantity = Math.Max(state.CurrentBatchQuantity, 0),
                LineStatus = Truncate(state.LineStatus, 300),
                LineStatusClass = NormalizeStatusClass(state.LineStatusClass),
                VolanteStatus = Truncate(state.VolanteStatus, 300),
                VolanteStatusClass = NormalizeStatusClass(state.VolanteStatusClass),
                ScreenState = NormalizeScreenState(state.ScreenState),
                FeedbackDurationMs = feedbackDurationMs,
                FeedbackExpiresAtUtc = feedbackDurationMs > 0
                    ? DateTime.UtcNow.AddMilliseconds(feedbackDurationMs)
                    : null,
                LineReadOnly = state.LineReadOnly,
                LineDisabled = state.LineDisabled,
                VolanteDisabled = state.VolanteDisabled,
                QuantityModeEnabled = state.QuantityModeEnabled,
                SoundState = NormalizeScreenState(state.SoundState),
                SoundEventId = Truncate(state.SoundEventId, 100),
                UpdatedAtUtc = DateTime.UtcNow
            };
        }

        private static string NormalizeScreenState(string? value)
        {
            return value is "success" or "error" or "warning" ? value : string.Empty;
        }

        private static string NormalizeStatusClass(string? value)
        {
            return value is "success-msg" or "error-msg" or "warning-msg" ? value : string.Empty;
        }

        private static string Truncate(string? value, int maximumLength)
        {
            var normalizedValue = value?.Trim() ?? string.Empty;
            return normalizedValue.Length <= maximumLength
                ? normalizedValue
                : normalizedValue[..maximumLength];
        }

        // ══════════════════════════════════════════════════════
        // EVENTOS DE CONEXIÓN
        // ══════════════════════════════════════════════════════

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[SignalR] Cliente desconectado: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"[SignalR] Cliente conectado: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }
    }

    public class ProductionLinesScannerViewState
    {
        public string LineValue { get; set; } = string.Empty;
        public string VolanteValue { get; set; } = string.Empty;
        public bool LineValid { get; set; }
        public int? ValidatedLineNumber { get; set; }
        public int? ValidatedLineId { get; set; }
        public int CurrentBatchQuantity { get; set; }
        public string LineStatus { get; set; } = string.Empty;
        public string LineStatusClass { get; set; } = string.Empty;
        public string VolanteStatus { get; set; } = string.Empty;
        public string VolanteStatusClass { get; set; } = string.Empty;
        public string ScreenState { get; set; } = string.Empty;
        public int FeedbackDurationMs { get; set; }
        public DateTime? FeedbackExpiresAtUtc { get; set; }
        public bool LineReadOnly { get; set; }
        public bool LineDisabled { get; set; }
        public bool VolanteDisabled { get; set; } = true;
        public bool QuantityModeEnabled { get; set; }
        public string SoundState { get; set; } = string.Empty;
        public string SoundEventId { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }
}

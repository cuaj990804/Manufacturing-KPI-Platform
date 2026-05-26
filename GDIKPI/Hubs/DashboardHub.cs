using Microsoft.AspNetCore.SignalR;

namespace GDIKPI.Hubs
{
    public class DashboardHub : Hub
    {
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
}
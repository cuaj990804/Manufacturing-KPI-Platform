// ════════════════════════════════════════════════════════════
// CONEXIÓN SIGNALR PARA SCANNER PRODUCTION (MULTI-PC)
// ════════════════════════════════════════════════════════════

let connection = null;
let currentLineId = null;
let reconnectAttempts = 0;
const MAX_RECONNECT_ATTEMPTS = 10;

/**
 * Inicializa la conexión SignalR para el scanner
 */
async function initializeSignalRConnection() {
    try {
        // Obtener ID de línea desde datos del servidor
        currentLineId = window.SCANNER_DATA?.productionLineId;

        if (!currentLineId) {
            console.error("❌ No se pudo obtener productionLineId");
            return;
        }

        console.log(`🔌 Inicializando SignalR para línea: ${currentLineId}`);

        // Crear conexión
        connection = new signalR.HubConnectionBuilder()
            .withUrl("/dashboardHub")
            .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // ════════════════════════════════════════════════════════
        // LISTENERS DE EVENTOS
        // ════════════════════════════════════════════════════════

        /**
         * Evento: UpdateLineMetrics
         * Se dispara cuando CUALQUIER PC de esta línea escanea/rechaza
         */
        connection.on("UpdateLineMetrics", (data) => {
            console.log("📊 [SYNC] Métricas actualizadas:", data);
            updateDashboardMetrics(data);
        });

        /**
         * Evento: ScanRegistered
         * Notificación cuando se registra un escaneo
         */
        connection.on("ScanRegistered", (data) => {
            console.log("✅ [SYNC] Escaneo registrado:", data);
            if (data.scannerValue) {
                updateLastScannedCode(data.scannerValue);
            }
        });

        /**
         * Evento: RejectRegistered
         * Notificación cuando se registra un rechazo
         */
        connection.on("RejectRegistered", (data) => {
            console.log("❌ [SYNC] Rechazo registrado:", data);
        });

        // ════════════════════════════════════════════════════════
        // MANEJO DE RECONEXIÓN
        // ════════════════════════════════════════════════════════

        connection.onreconnecting((error) => {
            console.warn("⚠️ [SYNC] Reconectando...", error);
            reconnectAttempts++;
            updateConnectionStatus('reconnecting');
        });

        connection.onreconnected(async (connectionId) => {
            console.log("✅ [SYNC] Reconectado:", connectionId);
            reconnectAttempts = 0;

            // CRÍTICO: Re-unirse al grupo
            await joinProductionLineGroup();

            // Refrescar métricas
            await refreshMetricsFromServer();

            updateConnectionStatus('connected');
        });

        connection.onclose((error) => {
            console.error("❌ [SYNC] Conexión cerrada:", error);
            updateConnectionStatus('disconnected');

            // Reintentar si no hemos superado el límite
            if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
                setTimeout(() => {
                    reconnectAttempts++;
                    initializeSignalRConnection();
                }, 5000);
            }
        });

        // ════════════════════════════════════════════════════════
        // CONECTAR Y UNIRSE AL GRUPO
        // ════════════════════════════════════════════════════════

        await connection.start();
        console.log("✅ [SYNC] SignalR conectado");

        await joinProductionLineGroup();

        updateConnectionStatus('connected');

    } catch (error) {
        console.error("❌ [SYNC] Error al inicializar:", error);
        updateConnectionStatus('error');

        if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
            reconnectAttempts++;
            setTimeout(initializeSignalRConnection, 5000);
        }
    }
}

/**
 * Une esta PC al grupo de la línea de producción
 */
async function joinProductionLineGroup() {
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
        console.warn("⚠️ No se puede unir al grupo: conexión no establecida");
        return;
    }

    try {
        await connection.invoke("JoinProductionLineGroup", currentLineId.toString());
        console.log(`✅ [SYNC] Unido al grupo: ProductionLine_${currentLineId}`);
    } catch (error) {
        console.error("❌ [SYNC] Error al unirse al grupo:", error);
    }
}

/**
 * Actualiza las métricas en el DOM
 */
function updateDashboardMetrics(data) {
    console.log("🔄 Actualizando métricas en DOM:", data);

    // Requerimiento (meta del día)
    if (data.requirementGoal !== undefined) {
        const reqElement = document.getElementById('requerimiento');
        if (reqElement) {
            reqElement.textContent = data.requirementGoal;
        }
    }

    // Balance (piezas producidas vs meta)
    if (data.producedPieces !== undefined && data.requirementGoal !== undefined) {
        const balanceElement = document.getElementById('producidas');
        const balanceCard = document.getElementById('producidasCard');

        if (balanceElement) {
            const percentage = data.requirementGoal > 0
                ? Math.round((data.producedPieces / data.requirementGoal) * 100)
                : 0;

            balanceElement.textContent = `${percentage}%`;

            // Cambiar color según progreso
            if (balanceCard) {
                balanceCard.classList.remove('card-green', 'card-gold');
                if (percentage >= 100) {
                    balanceCard.classList.add('card-gold');
                } else {
                    balanceCard.classList.add('card-green');
                }
            }
        }
    }

    // Rechazos
    if (data.rejectedPieces !== undefined) {
        const rechazosElement = document.getElementById('rechazos');
        if (rechazosElement) {
            rechazosElement.textContent = data.rejectedPieces;
        }
    }

    // Eficiencia (OEE)
    if (data.oeePercentage !== undefined) {
        const eficienciaElement = document.getElementById('eficiencia');
        if (eficienciaElement) {
            eficienciaElement.textContent = `${Math.round(data.oeePercentage)}%`;
        }
    }

    // Efecto visual de actualización
    flashMetricsUpdate();
}

/**
 * Efecto visual cuando se actualizan las métricas
 */
function flashMetricsUpdate() {
    const cards = document.querySelectorAll('.metric-card');
    cards.forEach(card => {
        card.style.transition = 'transform 0.3s ease, box-shadow 0.3s ease';
        card.style.transform = 'scale(1.02)';
        card.style.boxShadow = '0 0 20px rgba(59, 130, 246, 0.3)';

        setTimeout(() => {
            card.style.transform = 'scale(1)';
            card.style.boxShadow = 'none';
        }, 300);
    });
}

/**
 * Actualiza el último código escaneado
 */
function updateLastScannedCode(scannerValue) {
    const lastCodeElement = document.getElementById('lastCode');
    if (lastCodeElement) {
        lastCodeElement.textContent = scannerValue;
        lastCodeElement.classList.add('flash');
        setTimeout(() => {
            lastCodeElement.classList.remove('flash');
        }, 300);
    }
}

/**
 * Actualiza el indicador de estado de conexión
 */
function updateConnectionStatus(status) {
    const statusConfig = {
        'connected': '🟢 Conectado',
        'reconnecting': '🟡 Reconectando...',
        'disconnected': '🔴 Desconectado',
        'error': '🔴 Error'
    };

    console.log(`[STATUS] ${statusConfig[status]}`);
}

/**
 * Refresca las métricas desde el servidor
 */
async function refreshMetricsFromServer() {
    try {
        const response = await fetch(`/api/ScannerProductionApi/GetLineMetrics?lineId=${currentLineId}`);
        const result = await response.json();

        if (response.ok) {
            updateDashboardMetrics({
                requirementGoal: result.requirementGoalPieces,
                producedPieces: result.producedPieces,
                rejectedPieces: result.rejectedPieces,
                oeePercentage: result.oeePercentage
            });
        }
    } catch (error) {
        console.error("Error al refrescar métricas:", error);
    }
}

// ════════════════════════════════════════════════════════════
// INICIALIZACIÓN AUTOMÁTICA
// ════════════════════════════════════════════════════════════

document.addEventListener('DOMContentLoaded', () => {
    console.log("📱 Inicializando SignalR connection...");
    initializeSignalRConnection();
});

// ════════════════════════════════════════════════════════════
// CLEANUP AL CERRAR
// ════════════════════════════════════════════════════════════

window.addEventListener('beforeunload', async () => {
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
        try {
            await connection.invoke("LeaveProductionLineGroup", currentLineId.toString());
            await connection.stop();
        } catch (error) {
            console.error("Error al desconectar:", error);
        }
    }
});
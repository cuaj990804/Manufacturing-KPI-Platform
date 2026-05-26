// ========================================
// INICIALIZACIÓN Y EVENT LISTENERS
// ========================================

/**
 * Inicializar datos del servidor
 */
function initializeServerData() {
    if (window.SCANNER_DATA) {
        // Copiar datos del servidor al estado global
        state.productionLineId = window.SCANNER_DATA.productionLineId;
        state.lineNumber = window.SCANNER_DATA.lineNumber;
        state.dailyGoal = window.SCANNER_DATA.dailyGoal;
        state.areaId = window.SCANNER_DATA.areaId;
        state.areaName = window.SCANNER_DATA.areaName;
        state.customerName = window.SCANNER_DATA.customerName;
        state.personalQuantity = window.SCANNER_DATA.personalQuantity;
        state.standardTime = Number(window.SCANNER_DATA.standardTime || 0);

        // Establecer el requerimiento desde dailyGoal
        if (state.dailyGoal > 0) {
            state.requerimiento = state.dailyGoal;
        }

        // Actualizar UI con los datos del servidor
        updateServerDataInUI();
    }
}

/**
 * Actualizar elementos de la UI con datos del servidor
 */
function updateServerDataInUI() {
    // Actualizar número de línea
    if (state.lineNumber) {
        const lineaElement = document.getElementById('linea');
        if (lineaElement) {
            lineaElement.textContent = state.lineNumber;
        }
    }

    // Actualizar área/customer si están disponibles
    if (state.customerName) {
        const customerElement = document.getElementById('customerName');
        if (customerElement && !customerElement.textContent) {
            customerElement.textContent = state.customerName;
        }
    }
}

/**
 * Inicializar la aplicación cuando el DOM esté listo
 */
document.addEventListener('DOMContentLoaded', function() {
    // Inicializar datos del servidor
    initializeServerData();

    // Cargar métricas iniciales desde el API
    loadMetricsFromAPI();

    // Inicializar display
    updateDisplay();

    // Event listeners para el escaneo principal
    const scanInputEl = document.getElementById('scanInput');
    if (scanInputEl) {
        scanInputEl.addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                e.stopPropagation();
                handleScan();
            }
        });
    }

    // Event listener para escaneo en el modal de cambio (si existe en la vista actual)
    const scanRequerimientoEl = document.getElementById('scanRequerimiento');
    if (scanRequerimientoEl) {
        scanRequerimientoEl.addEventListener('keydown', async function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                e.stopPropagation();
                const partNumber = this.value.trim();
                if (partNumber) {
                    try {
                        // Llamar al API
                        const data = await getPartNumberFromAPI(partNumber);

                        // Guardar los datos en el estado
                        updatePartNumberInfo(data);

                        // Confirmar automáticamente después de obtener los datos
                        confirmChange();
                    } catch (error) {
                        alert(error.message || 'Error al consultar el número de parte');
                        state.currentPartData = null;
                    }
                }
            }
        });
    }

    // Event listeners para el modal de rechazo (si existe en la vista actual)
    const rejectPieceInputEl = document.getElementById('rejectPieceInput');
    if (rejectPieceInputEl) {
        rejectPieceInputEl.addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                e.stopPropagation();
                handleRejectPieceScan();
            }
        });
    }

    const rejectEmployeeInputEl = document.getElementById('rejectEmployeeInput');
    if (rejectEmployeeInputEl) {
        rejectEmployeeInputEl.addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                e.stopPropagation();
                const command = this.value.trim();
                if (typeof processRejectCommand === 'function' && processRejectCommand(command)) {
                    this.value = '';
                    return;
                }
                const pieceInput = document.getElementById('rejectPieceInput');
                if (pieceInput) {
                    pieceInput.focus();
                }
            }
        });
    }

    const rejectDefectInputEl = document.getElementById('rejectDefectInput');
    if (rejectDefectInputEl) {
        rejectDefectInputEl.addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                e.stopPropagation();
                handleRejectDefectScan();
            }
        });
    }

    const rejectDefectSearchEl = document.getElementById('rejectDefectSearch');
    if (rejectDefectSearchEl) {
        rejectDefectSearchEl.addEventListener('input', function() {
            if (typeof filterAvailableDefects === 'function') {
                filterAvailableDefects(this.value);
            }
        });
    }

    // Actualizar métricas cada 30 segundos
    setInterval(() => {
        loadMetricsFromAPI();
    }, 30000); // 30 segundos
});

// ========================================
// SIGNALR - ACTUALIZACIONES EN TIEMPO REAL
// ========================================
let signalRConnection = null;

async function initializeSignalR() {
    if (typeof signalR === 'undefined') {
        console.warn('SignalR no está disponible');
        return;
    }

    try {
        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl('/dashboardHub')
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: retryContext => {
                    if (retryContext.elapsedMilliseconds < 60000) {
                        return Math.min(retryContext.previousRetryCount * 2000, 10000);
                    }
                    return null; // Dejar de reconectar después de 60 segundos
                }
            })
            .build();

        // Escuchar actualizaciones de producción
        signalRConnection.on('ProductionDataUpdated', (data) => {
            console.log('Actualización recibida:', data);
            // Solo actualizar si es de la misma área
            if (data && data.areaId === state.areaId) {
                loadMetricsFromAPI();
            }
        });

        // Iniciar conexión
        await signalRConnection.start();
        console.log('SignalR conectado');

        // Unirse al grupo del área
        if (state.areaId) {
            await signalRConnection.invoke('JoinDashboardGroup', String(state.areaId));
            console.log(`Unido al grupo Dashboard_${state.areaId}`);
        }

    } catch (error) {
        console.error('Error conectando SignalR:', error);
    }
}

// Inicializar SignalR después de cargar el DOM
if (typeof signalR !== 'undefined') {
    initializeSignalR();
}

// ================= CONFIG DESDE VIEW =================
const config = window.dashboardConfig || {};
const areasFromServer = config.areas || [];

// ================= FUNCION PRINCIPAL =================
async function cargarTarjetas() {
    try {
        let areaId = getAreaIdFromUrl() || getAreaIdFromPage();

        if (areaId === undefined || areaId === null) {
            areaId = config.areaId;
        }

        if (areaId === undefined) {
            areaId = null;
        }

        console.log("CONFIG:", config);
        console.log("AREAS:", areasFromServer);
        console.log("AREA ID FINAL:", areaId);

        const hoy = new Date();
        const fecha = hoy.toISOString().split("T")[0];

        const horaActual = new Date();
        const horaReal = horaActual.toTimeString().split(" ")[0];

        const container = document.getElementById("cards-container");
        if (!container) return;

        container.innerHTML = "";

        // ================= UNA SOLA ÁREA =================
        if (areaId !== null && areaId !== undefined) {
            

            const response = await fetch(`/api/Production/dailyproductionbyarea?areaId=${areaId}&targetDate=${fecha}&targetTime=${horaReal}`);

            if (!response.ok) {
                throw new Error(await response.text());
            }

            const data = await response.json();
            renderCards(data, container);
        }

        // ================= TODAS LAS ÁREAS =================
        else if (areasFromServer.length > 0) {
            
/*
            await Promise.all(
                areasFromServer.map(async (area) => {

                    const response = await fetch(`/api/Production/dailyproductionbyarea?areaId=${area.AreaId}&targetDate=${fecha}&targetTime=${horaReal}`);

                    if (!response.ok) return;

                    const data = await response.json();
                    renderCards(data, container);

                })
            );*/

            for (const area of areasFromServer) {
                const response = await fetch(`/api/Production/dailyproductionbyarea?areaId=${area.AreaId}&targetDate=${fecha}&targetTime=${horaReal}`);
                if (!response.ok) continue;

                const data = await response.json();
                renderCards(data, container);
            }
        }

    } catch (error) {
        console.error("Error al cargar datos:", error);
    }
}

// ================= RENDER =================
async function renderCards(data, container) {

    for (const total of data) {

        const response = await fetch(
            `/api/ScannerProductionApi/GetLineMetrics?lineId=${total.lineId}`
        );

        let metrics = {};

        if (response.ok) {
            metrics = await response.json();
        }

        const produced = total.producedPieces || 0;
        const rejected = total.rejectedPieces || 0;

        const rechazoPorcentaje = produced !== 0
            ? (rejected / produced) * 100
            : 0;

        const eficiencia = calcularEficiencia({
            producidas: metrics.producedPieces,
            standardTime: metrics.standardTime,
            personalQuantity: metrics.personalQuantity,
            availableMinutes: metrics.availableMinutes
        });

       

        const card = crearCard({
            lineId: total.lineId,
            linea: total.lineNumber,
            lineName: total.lineName,
            area: total.areaCustomerName,
            meta: total.estimatedGoalPieces,
            real: produced,
            tiempoMuerto: total.downtimeMinutes,
            rechazo: rechazoPorcentaje,
            balance: total.requirementBalance,
            metaEstimada: total.estimatedGoalPieces,
            calidad: total.qualityPercentage,
            oee: eficiencia,
            producidas: produced
        });

        container.appendChild(card);
    }
}

// ================= CREAR CARD =================
function crearCard({ lineId, linea, lineName, area, meta, real, tiempoMuerto, rechazo, balance, metaEstimada, calidad, oee, producidas }) {

    const div = document.createElement("div");

    div.id = `card-line-${lineId}`;
    div.dataset.lineId = lineId;

    const cumpleMeta = real >= meta;

    div.className = cumpleMeta
        ? "production-card success"
        : "production-card warning";

    const rechazoFinal = rechazo ?? 0;
    const calidadFinal = calidad ?? 0;
    const oeeFinal = oee ?? 0;

    // ✅ BALANCE REAL
    const balanceFinal = (producidas || 0) - (meta || 0);

    const tituloLinea = (lineName && lineName.trim() !== '')
        ? lineName
        : "LÍNEA " + linea;

    div.innerHTML = `
    <div class="card-header ${cumpleMeta ? 'success' : 'warning'}">
        <div class="line-title">${tituloLinea}</div>
    </div>

    <div class="metrics-grid">

        <div class="metric requerimiento">
            <div class="metric-label">REQUERIMIENTO</div>
            <div id="req-value-${lineId}" class="metric-value">${meta}</div>
        </div>

        <div class="metric balance ${balanceFinal >= 0 ? 'positive' : 'negative'}">
            <div class="metric-label">BALANCE</div>
            <div class="metric-value">
                ${balanceFinal > 0 ? '+' : ''}${balanceFinal}
            </div>
        </div>

        <div class="metric rechazos">
            <div class="metric-label">RECHAZOS</div>
            <div id="rechazo-value-${lineId}" class="metric-value">${Math.round(rechazoFinal)}%</div>
        </div>

        <div class="metric eficiencia">
            <div class="metric-label">EFICIENCIA</div>
            <div id="oee-value-${lineId}" class="metric-value">${Math.round(oeeFinal)}%</div>
        </div>

    </div>
    `;

    return div;
}

// ================= HELPERS =================
function calcularEficiencia({ producidas, standardTime, personalQuantity, availableMinutes }) {

    const p = Number(producidas || 0);
    const t = Number(standardTime || 0);
    const per = Number(personalQuantity || 0);
    const min = Number(availableMinutes || 0);

    const horas = min / 60;

    return (per > 0 && horas > 0)
        ? Math.round(((p * t) / (per * horas)) * 100)
        : 0;
}
function getAreaIdFromUrl() {
    const urlParams = new URLSearchParams(window.location.search);
    const areaId = urlParams.get('areaId');
    return areaId ? parseInt(areaId) : null;
}

function getAreaIdFromPage() {
    const el = document.getElementById('area-id-hidden');
    return el ? parseInt(el.value) : null;
}

// ================= INDICADOR =================
function cargarTarjetasConIndicador() {
    if (typeof mostrarActualizando === "function") {
        mostrarActualizando();
    }

    cargarTarjetas().then(() => {
        if (typeof ocultarActualizando === "function") {
            setTimeout(ocultarActualizando, 1000);
        }
    });
}

// ================= ACTUALIZAR MÉTRICAS SIN RECARGAR CARDS =================
// ================= ACTUALIZAR MÉTRICAS SIN RECARGAR CARDS =================
async function actualizarMetricasDashboard() {
    try {
        let areaId = getAreaIdFromUrl() || getAreaIdFromPage() || config.areaId || null;
        if (areaId === undefined || areaId === null) {
            areaId = null;
        }

        const hoy = new Date();
        const fecha = hoy.toISOString().split("T")[0];
        const horaReal = hoy.toTimeString().split(" ")[0];

        let areasToFetch = [];

        if (areaId !== null) {
            areasToFetch = [{ areaId: areaId }];
        } else if (areasFromServer.length > 0) {
            areasToFetch = areasFromServer.map(a => ({ areaId: a.AreaId }));
        }

        for (const area of areasToFetch) {
            const response = await fetch(`/api/Production/dailyproductionbyarea?areaId=${area.areaId}&targetDate=${fecha}&targetTime=${horaReal}`);
            if (!response.ok) continue;

            const data = await response.json();

            for (const item of data) {
                const lineId = item.lineId;
                const cardElement = document.getElementById(`card-line-${lineId}`);

                if (!cardElement) continue; // Si no existe la card, saltar

                // 1️⃣ Actualizar REQUERIMIENTO
                const reqElement = document.getElementById(`req-value-${lineId}`);
                if (reqElement && item.estimatedGoalPieces !== undefined) {
                    reqElement.textContent = item.estimatedGoalPieces;
                }

                // 2️⃣ Actualizar BALANCE (calcular desde producidas - meta)
                const produced = item.producedPieces || 0;
                const meta = item.estimatedGoalPieces || 0;
                let balance = produced - meta;

                const balanceMetric = cardElement.querySelector('.metric.balance');
                if (balanceMetric) {
                    const balanceValue = balanceMetric.querySelector('.metric-value');
                    if (balanceValue) {
                        balanceValue.textContent = `${balance > 0 ? '+' : ''}${balance}`;
                    }

                    // Actualizar clase positive/negative
                    balanceMetric.classList.remove('positive', 'negative');
                    balanceMetric.classList.add(balance >= 0 ? 'positive' : 'negative');
                }

                // 3️⃣ Actualizar RECHAZOS
                const rejected = item.rejectedPieces || 0;
                const rechazoPorcentaje = produced !== 0
                    ? (rejected / produced) * 100
                    : 0;

                const rechazoElement = document.getElementById(`rechazo-value-${lineId}`);
                if (rechazoElement) {
                    rechazoElement.textContent = Math.round(rechazoPorcentaje) + '%';
                }

                // 4️⃣ Actualizar EFICIENCIA (usando GetLineMetrics)
                const metricsResponse = await fetch(`/api/ScannerProductionApi/GetLineMetrics?lineId=${lineId}`);
                if (metricsResponse.ok) {
                    const metrics = await metricsResponse.json();

                    const eficiencia = calcularEficiencia({
                        producidas: metrics.producedPieces,
                        standardTime: metrics.standardTime,
                        personalQuantity: metrics.personalQuantity,
                        availableMinutes: metrics.availableMinutes
                    });

                    const oeeElement = document.getElementById(`oee-value-${lineId}`);
                    if (oeeElement) {
                        oeeElement.textContent = Math.round(eficiencia) + '%';
                    }
                }

                // 5️⃣ Actualizar clase de la card según cumplimiento de meta
                

                cardElement.className = balance >= 0
                    ? "production-card success"
                    : "production-card danger";

                const header = cardElement.querySelector('.card-header');
                if (header) {
                    header.className = balance >= 0
                        ? "card-header success"
                        : "card-header danger";
                }
            }
        }

        console.log("✅ Métricas actualizadas sin recargar cards");

    } catch (error) {
        console.error("Error actualizando métricas:", error);
    }
}

// ================= ACTUALIZAR SOLO REQUERIMIENTO (SignalR) =================
function actualizarRequerimiento(lineId, nuevoValor) {
    const elemento = document.getElementById(`req-value-${lineId}`);
    if (elemento) {
        elemento.style.transition = 'transform 0.2s ease, color 0.2s ease';
        elemento.style.transform = 'scale(1.1)';
        elemento.style.color = '#4CAF50';

        elemento.textContent = nuevoValor;

        setTimeout(() => {
            elemento.style.transform = 'scale(1)';
            setTimeout(() => {
                elemento.style.color = '';
            }, 200);
        }, 200);

        console.log(`✅ REQUERIMIENTO actualizado para línea ${lineId}: ${nuevoValor}`);
    }
}

// ================= SIGNALR =================
// ================= SIGNALR =================
let signalRConnection = null;
let currentAreaId = null;

async function initializeSignalR() {
    try {
        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl("/dashboardHub")
            .withAutomaticReconnect()
            .build();

        signalRConnection.on("ProductionDataUpdated", function (data) {
            console.log("📡 ProductionDataUpdated recibido:", data);
            if (!currentAreaId || data.areaId === currentAreaId) {
                actualizarMetricasDashboard();
            }
        });

        signalRConnection.on("RejectDataUpdated", function (data) {
            console.log(" RejectDataUpdated recibido:", data);
            if (!currentAreaId || data.areaId === currentAreaId) {
                actualizarMetricasDashboard();
            }
        });

        signalRConnection.on("RequirementUpdated", function (data) {
            console.log(" RequirementUpdated recibido:", data);
            actualizarRequerimiento(data.lineId, data.requirementGoalPieces);
        });

        await signalRConnection.start();

        currentAreaId = getAreaIdFromUrl() || getAreaIdFromPage() || config.areaId || null;

        // ✅ Unirse a grupos según el contexto
        if (currentAreaId) {
            // Vista de UN área específica
            await signalRConnection.invoke("JoinDashboardGroup", currentAreaId.toString());
            console.log(`✅ Unido a Dashboard_${currentAreaId}`);
        } else if (areasFromServer.length > 0) {
            // Vista de TODAS las áreas - unirse a todos los grupos
            for (const area of areasFromServer) {
                await signalRConnection.invoke("JoinDashboardGroup", area.AreaId.toString());
                console.log(`✅ Unido a Dashboard_${area.AreaId}`);
            }
        }

        console.log("✅ SignalR conectado y grupos configurados");

    } catch (error) {
        console.error("❌ SignalR error:", error);
    }
}

// ================= AUTO REFRESH CADA MINUTO =================
function iniciarAutoRefresh() {
    const intervalo = 60000; // 1 minuto

    setInterval(() => {
        console.log("🔄 Actualizando métricas...");
        actualizarMetricasDashboard();
    }, intervalo);
}



// ================= INIT =================
cargarTarjetasConIndicador();
initializeSignalR();
iniciarAutoRefresh();

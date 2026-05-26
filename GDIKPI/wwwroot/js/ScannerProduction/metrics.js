// ========================================
// METRICS AND DISPLAY MANAGEMENT
// ========================================

function toNumber(value) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
}

function applyProducidasState() {
    const producidasEl = document.getElementById('producidas');
    const producidasCard = document.getElementById('producidasCard');

    if (!producidasEl) {
        return;
    }

    const producidas = toNumber(state.producidas);
    const requerimiento = toNumber(state.requerimiento);
    const balancePieces = producidas - requerimiento;
    const isOnTarget = balancePieces >= 0;
    const balanceText = balancePieces === 0
        ? '0'
        : balancePieces > 0
            ? `+${balancePieces}`
            : `${balancePieces}`;

    producidasEl.textContent = balanceText;
    producidasEl.style.color = isOnTarget ? '#10b981' : '#ef4444';
    producidasEl.style.textShadow = isOnTarget
        ? '0 0 60px rgba(74,222,128,0.2)'
        : '0 0 60px rgba(248,113,113,0.2)';

    if (producidasCard) {
        producidasCard.classList.toggle('card-green', isOnTarget);
        producidasCard.classList.toggle('card-red', !isOnTarget);
    }
}

function applyRechazosState() {
    const rechazosEl = document.getElementById('rechazos');
    if (!rechazosEl) {
        return;
    }

    const rechazos = toNumber(state.rechazos);
    const producidas = toNumber(state.producidas);
    const piezastotales = producidas + rechazos;
    const rechazosPct = producidas > 0 ? (rechazos / piezastotales) * 100 : 0;

    rechazosEl.textContent = `${Math.round(rechazosPct)}%`;
    rechazosEl.style.color = '#ef4444';
}

/**
 * Update metrics display in UI
 */
function updateDisplay() {
    const requerimientoEl = document.getElementById('requerimiento');
    if (requerimientoEl) {
        requerimientoEl.textContent = state.requerimiento;
        requerimientoEl.style.color = '#f59e0b';
        requerimientoEl.style.textShadow = '0 0 60px rgba(245,158,11,0.2)';
    }

    const producidasEl = document.getElementById('producidas');
    if (producidasEl) {
        applyProducidasState();
    }

    const rechazosEl = document.getElementById('rechazos');
    if (rechazosEl) {
        applyRechazosState();
    }

    const producidas = toNumber(state.producidas);
    const tiempoEstandar = toNumber(state.standardTime);
    const personal = toNumber(state.personalQuantity);
    const tiempoEfectivoMinutos = toNumber(state.availableMinutes);
    const tiempoEfectivoHoras = tiempoEfectivoMinutos / 60;
    const eficiencia = (personal > 0 && tiempoEfectivoHoras > 0)
        ? Math.round(((producidas * tiempoEstandar) / (personal * tiempoEfectivoHoras)) * 100)
        : 0;

    const eficienciaEl = document.getElementById('eficiencia');
    if (eficienciaEl) {
        eficienciaEl.textContent = eficiencia + '%';
        eficienciaEl.style.color = '#60a5fa';
    }
}

/**
 * Load metrics from API
 */
async function loadMetricsFromAPI() {
    if (!state.productionLineId) {
        console.warn('Cannot load metrics: productionLineId is not defined');
        return;
    }

    try {
        const response = await fetch(`${CONFIG.API.GET_LINE_METRICS}?lineId=${state.productionLineId}`);

        if (!response.ok) {
            throw new Error('Error loading metrics from API');
        }

        const data = await response.json();

        state.requerimiento = toNumber(data.estimatedRequirement || 0);
        state.producidas = toNumber(data.producedPieces || 0);
        state.rechazos = toNumber(data.rejectedPieces || 0);
        state.standardTime = toNumber(data.standardTime || state.standardTime || 0);
        state.personalQuantity = toNumber(data.personalQuantity || state.personalQuantity || 0);
        state.availableMinutes = toNumber(data.availableMinutes || 0);

        const requerimientoEl = document.getElementById('requerimiento');
        if (requerimientoEl) {
            requerimientoEl.textContent = state.requerimiento;
            requerimientoEl.style.color = '#f59e0b';
            requerimientoEl.style.textShadow = '0 0 60px rgba(245,158,11,0.2)';
        }

        const producidasEl = document.getElementById('producidas');
        if (producidasEl) {
            applyProducidasState();
        }

        const rechazosEl = document.getElementById('rechazos');
        if (rechazosEl) {
            applyRechazosState();
        }

        const producidas = toNumber(state.producidas);
        const tiempoEstandar = toNumber(state.standardTime);
        const personal = toNumber(state.personalQuantity);
        const tiempoEfectivoMinutos = toNumber(state.availableMinutes);
        const tiempoEfectivoHoras = tiempoEfectivoMinutos / 60;
        const eficiencia = (personal > 0 && tiempoEfectivoHoras > 0)
            ? Math.round(((producidas * tiempoEstandar) / (personal * tiempoEfectivoHoras)) * 100)
            : 0;
        const eficienciaEl = document.getElementById('eficiencia');
        if (eficienciaEl) {
            eficienciaEl.textContent = eficiencia + '%';
            eficienciaEl.style.color = '#60a5fa';
        }

        console.log('Metrics updated from API:', data);

    } catch (error) {
        console.error('Error loading metrics:', error);
    }
}

/**
 * Update part number info on display
 * @param {Object} partData - part number data
 */
function updatePartNumberInfo(partData) {
    if (!partData || partData.length === 0) {
        return;
    }

    const part = partData[0];
    const customerElement = document.getElementById('customerName');
    const programElement = document.getElementById('programName');
    const partNumberElement = document.getElementById('partNumberName');

    if (customerElement) {
        customerElement.textContent = part.customer || '';
    }
    if (programElement) {
        programElement.textContent = part.program || '';
    }
    if (partNumberElement) {
        partNumberElement.textContent = part.partnumber || '';
    }

    state.currentPartData = part;
    state.currentPartNumber = part.partnumber || null;

    console.log('Part number updated:', state.currentPartNumber);
}

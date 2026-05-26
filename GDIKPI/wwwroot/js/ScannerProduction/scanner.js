// ========================================
// LÓGICA PRINCIPAL DEL SCANNER
// ========================================

/**
 * Manejar el escaneo de códigos
 */
async function handleScan() {
    const input = document.getElementById('scanInput');
    const code = input.value.trim();

    if (!code) return;

    // DETECCIÓN DE CÓDIGO ESPECIAL: REJECT (y RPP por compatibilidad) abre el modal de rechazo
    const specialCode = code.toUpperCase();
    if (specialCode === 'REJECT' || specialCode === 'RPP') {
        input.value = '';
        redirectToRejectPage();
        return;
    }

    if (await isEmployeeNumberForReject(code)) {
        input.value = '';
        redirectToRejectPage(code);
        return;
    }

    // Validar que se tenga el ID de la línea de producción
    if (!state.productionLineId) {
        alert('Error: No se encontró el ID de la línea de producción');
        return;
    }

    if (isZfCustomer() && !extractZfPartNumberFromScan(code)) {
        showScanResult('error', code);
        showScanToast('Formato invalido ZF: NP+YY+JJJ+T+CCCC', 'error');
        input.value = '';

        setTimeout(() => {
            state.scanStatus = null;
            resetScanStatus();
        }, CONFIG.UI.RESET_SCAN_STATUS_DELAY);

        return;
    }

    // Validar si el código ya fue escaneado antes
    try {
        const validationResult = await validateScannerValue(code);

        if (!validationResult.isValid) {
            const scanDateTime = new Date(validationResult.scanDetails.scanDateTime);
            showScanResult('error', code);
            const previousScanText = scanDateTime.toLocaleString('es-MX', {
                year: 'numeric',
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit'
            });
            showScanToast(`Ya fue escaneado: ${previousScanText}`, 'error');
            input.value = '';

            setTimeout(() => {
                state.scanStatus = null;
                resetScanStatus();
            }, CONFIG.UI.RESET_SCAN_STATUS_DELAY);

            return;
        }
    } catch (error) {
        console.error('Error al validar el código:', error);
        alert('Error al validar el código: ' + error.message);
        input.value = '';
        return;
    }

    // Guardar en la base de datos
    try {
        await saveScanToDatabase(code);
        state.producidas++;
        state.scanStatus = 'success';
        state.lastScan = code;
        showScanResult('success', code);
        showScanToast('Escaneo registrado con éxito', 'success');

        // Recargar métricas desde el API después de guardar
        await loadMetricsFromAPI();
    } catch (error) {
        console.error('Error al guardar el escaneo:', error);
        alert('Error al guardar el escaneo en la base de datos: ' + error.message);
        input.value = '';
        return;
    }

    input.value = '';
    updateDisplay();

    setTimeout(() => {
        state.scanStatus = null;
        resetScanStatus();
    }, CONFIG.UI.RESET_SCAN_STATUS_DELAY);
}

async function isEmployeeNumberForReject(value) {
    const normalizedValue = String(value || '').trim();
    if (!normalizedValue) {
        return false;
    }

    try {
        const response = await fetch(`${CONFIG.API.VALIDATE_EMPLOYEE_NUMBER}?employeeNumber=${encodeURIComponent(normalizedValue)}`);
        if (!response.ok) {
            return false;
        }

        const result = await response.json();
        return result?.exists === true;
    } catch (error) {
        console.error('Error al validar numero de empleado:', error);
        return false;
    }
}

function redirectToRejectPage(employeeNumber = '') {
    if (!state.productionLineId) {
        alert('Error: No se encontrÃ³ el ID de la lÃ­nea de producciÃ³n');
        return;
    }

    const queryParams = new URLSearchParams({
        productionLineId: String(state.productionLineId)
    });

    const normalizedEmployeeNumber = String(employeeNumber || '').trim();
    if (normalizedEmployeeNumber) {
        queryParams.set('employeeNumber', normalizedEmployeeNumber);
    }

    window.location.href = `/ScannerProduction/Reject?${queryParams.toString()}`;
}

/**
 * Validar si un código ya fue escaneado previamente
 * IMPORTANTE: Busca en TODAS las líneas, no solo en la línea actual
 * @param {string} scannerValue - Valor a validar
 * @returns {Promise<Object>} Resultado de la validación
 */
async function validateScannerValue(scannerValue) {
    // NO enviamos lineId para buscar en todas las líneas
    const response = await fetch(`${CONFIG.API.VALIDATE_SCANNER_VALUE}?scannerValue=${encodeURIComponent(scannerValue)}`);

    if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Error al validar el código');
    }

    const result = await response.json();
    return result;
}

/**
 * Guardar escaneo en la base de datos
 * @param {string} scannerValue - Valor escaneado
 */
function isZfCustomer() {
    return String(state.customerName || '').toLowerCase().includes('zf');
}

function reverseString(value) {
    return String(value || '').split('').reverse().join('');
}

function extractZfPartNumberFromScan(scannerValue) {
    const normalizedScan = String(scannerValue || '').trim().toUpperCase();
    if (normalizedScan.length <= 10) {
        return null;
    }

    const trace = normalizedScan.slice(-10);
    const year = trace.slice(0, 2);
    const julianDay = trace.slice(2, 5);
    const shift = trace.slice(5, 6);
    const sequence = trace.slice(6, 10);

    if (!/^\d{2}$/.test(year)) return null;
    if (!/^\d{3}$/.test(julianDay)) return null;
    const julianDayNumber = parseInt(julianDay, 10);
    if (julianDayNumber < 1 || julianDayNumber > 366) return null;
    if (!/^\d$/.test(shift)) return null;
    if (!/^[A-Z0-9]{4}$/.test(sequence)) return null;

    // Extraccion por reversa solicitada:
    // reverse(scan) = [10 de trazabilidad invertidos][NP invertido]
    const reversedScan = reverseString(normalizedScan);
    const reversedPart = reversedScan.slice(10);
    const partNumber = reverseString(reversedPart);

    return partNumber && partNumber.length >= 8 ? partNumber : null;
}

function getZfPartNumberCandidates(scannerValue) {
    const candidates = [];

    const addCandidate = (value) => {
        const candidate = String(value || '').trim().toUpperCase();
        if (!candidate || candidate.length < 8) return;
        if (!candidates.includes(candidate)) {
            candidates.push(candidate);
        }
    };

    const extractedPartNumber = extractZfPartNumberFromScan(scannerValue);
    if (extractedPartNumber) {
        addCandidate(extractedPartNumber);
    }

    return candidates;
}

async function resolvePartDataFromZfScan(scannerValue) {
    if (typeof getPartNumberFromAPI !== 'function') {
        return null;
    }

    const candidates = getZfPartNumberCandidates(scannerValue);

    for (const candidate of candidates) {
        try {
            const partData = await getPartNumberFromAPI(candidate);
            if (Array.isArray(partData) && partData.length > 0 && partData[0]?.partnumber) {
                return partData[0];
            }
        } catch {
            // Ignorar candidato inválido y probar el siguiente.
        }
    }

    return null;
}

async function saveScanToDatabase(scannerValue) {
    let currentPartData = state.currentPartData || {};

    if (isZfCustomer()) {
        const resolvedPartData = await resolvePartDataFromZfScan(scannerValue);
        if (resolvedPartData) {
            currentPartData = resolvedPartData;
            state.currentPartData = resolvedPartData;
            state.currentPartNumber = resolvedPartData.partnumber || null;

            if (typeof updatePartNumberInfo === 'function') {
                updatePartNumberInfo([resolvedPartData]);
            }
        } else {
            throw new Error('No se pudo resolver NP/programa para el escaneo ZF');
        }
    }

    const hasProgramId = currentPartData.programId || currentPartData.id;
    const programId = hasProgramId ? (currentPartData.programId || currentPartData.id) : null;
    const programDescription = currentPartData.program || null;
    const partNumber = currentPartData.partnumber || null;

    const response = await fetch(CONFIG.API.SAVE_SCAN, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            lineId: state.productionLineId,
            partNumber: partNumber,
            scannerValue: scannerValue,
            programId: programId,
            programDescription: programDescription
        })
    });

    if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Error al guardar el escaneo');
    }

    const result = await response.json();
    return result;
}

/**
 * Mostrar resultado del escaneo
 * @param {string} status - 'success' o 'error'
 * @param {string} code - Código escaneado
 */
function showScanResult(status, code) {
    const body = document.body;
    const container = document.querySelector('.container');
    const scanInput = document.getElementById('scanInput');
    const lastScanInfo = document.getElementById('lastScanInfo');
    const lastCode = document.getElementById('lastCode');

    if (lastCode) {
        lastCode.textContent = code;
    }

    if (status === 'success') {
        body.className = 'success-bg';
        if (container) container.className = 'container success-bg';
        if (scanInput) scanInput.className = 'scan-input success';
        if (lastScanInfo) {
            lastScanInfo.className = 'last-scan success';
            lastScanInfo.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M22 11.08V12a10 10 0 11-5.93-9.14"/>
                <polyline points="22 4 12 14.01 9 11.01"/>
            </svg>
            <span>✓ Aceptado: <strong>${code}</strong></span>`;
        }
    } else {
        body.className = 'error-bg';
        if (container) container.className = 'container error-bg';
        if (scanInput) scanInput.className = 'scan-input error';
        if (lastScanInfo) {
            lastScanInfo.className = 'last-scan error';
            lastScanInfo.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <circle cx="12" cy="12" r="10"/>
                <line x1="12" y1="8" x2="12" y2="12"/>
                <line x1="12" y1="16" x2="12.01" y2="16"/>
            </svg>
            <span>✗ Rechazado: <strong>${code}</strong></span>`;
        }
    }

    if (lastScanInfo) {
        lastScanInfo.classList.remove('hidden');
    }
}

let toastTimeout = null;
function showScanToast(message, type = 'success') {
    const toast = document.getElementById('scanToast');
    if (!toast) return;

    toast.textContent = message;
    toast.classList.remove('success', 'error', 'show');
    toast.classList.add(type === 'error' ? 'error' : 'success');

    if (toastTimeout) {
        clearTimeout(toastTimeout);
    }

    requestAnimationFrame(() => {
        toast.classList.add('show');
    });

    toastTimeout = setTimeout(() => {
        toast.classList.remove('show');
    }, 2200);
}

/**
 * Resetear el estado visual del escaneo
 */
function resetScanStatus() {
    const body = document.body;
    const container = document.querySelector('.container');
    const scanInput = document.getElementById('scanInput');

    body.className = '';
    if (container) container.className = 'container';
    if (scanInput) scanInput.className = 'scan-input';
}

/**
 * Confirmar eliminación del último código
 */
async function confirmDelete() {
    // Validar que se tenga el ID de la línea de producción
    if (!state.productionLineId) {
        alert('Error: No se encontró el ID de la línea de producción');
        closeDeleteModal();
        return;
    }

    try {
        // Eliminar de la base de datos
        const response = await fetch(`${CONFIG.API.DELETE_LAST_SCAN}?lineId=${state.productionLineId}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Error al eliminar el escaneo');
        }

        const result = await response.json();

        // Recargar métricas desde el API después de eliminar
        await loadMetricsFromAPI();

        updateDisplay();
        alert('Escaneo eliminado exitosamente');
    } catch (error) {
        console.error('Error al eliminar el escaneo:', error);
        alert('Error al eliminar el escaneo: ' + error.message);
    }

    closeDeleteModal();
}

/**
 * Manejar salida de la aplicación - Cerrar sesión
 */
function handleExit() {
    window.location.href = '/Account/Logout';
}

// ========================================
// MODAL DELETE
// ========================================

function openDeleteModal() {
    document.getElementById('deleteModal').classList.remove('hidden');
}

function closeDeleteModal() {
    document.getElementById('deleteModal').classList.add('hidden');
}

// ========================================
// MODAL RESCAN (RE-ESCANEO)
// ========================================

/**
 * Abrir modal de confirmación para re-escaneo
 * @param {Date} scanDateTime - Fecha y hora del escaneo anterior
 * @param {string} partNumber - Número de parte del escaneo anterior
 * @param {string} lineNumber - Número de la línea donde fue escaneado anteriormente
 */
function openRescanModal(scanDateTime, partNumber, lineNumber) {
    const modal = document.getElementById('rescanModal');
    const dateTimeElement = document.getElementById('rescanDateTime');
    const partNumberElement = document.getElementById('rescanPartNumber');
    const lineNumberElement = document.getElementById('rescanLineId');

    // Formatear fecha y hora
    const formattedDateTime = scanDateTime.toLocaleString('es-MX', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
    });

    dateTimeElement.textContent = formattedDateTime;
    partNumberElement.textContent = partNumber;
    lineNumberElement.textContent = lineNumber || 'N/A';

    modal.classList.remove('hidden');
}

/**
 * Cerrar modal de re-escaneo
 */
function closeRescanModal() {
    document.getElementById('rescanModal').classList.add('hidden');
    // Limpiar los datos pendientes
    state.pendingRescanCode = null;
    state.pendingRescanDetails = null;
}

/**
 * Confirmar re-escaneo de la pieza
 */
async function confirmRescan() {
    if (!state.pendingRescanCode) {
        alert('Error: No hay código pendiente para re-escanear');
        closeRescanModal();
        return;
    }

    // Guardar en la base de datos
    try {
        await saveScanToDatabase(state.pendingRescanCode);
        state.producidas++;
        state.scanStatus = 'success';
        state.lastScan = state.pendingRescanCode;
        showScanResult('success', state.pendingRescanCode);

        // Recargar métricas desde el API después de guardar
        await loadMetricsFromAPI();

        updateDisplay();

        setTimeout(() => {
            state.scanStatus = null;
            resetScanStatus();
        }, CONFIG.UI.RESET_SCAN_STATUS_DELAY);

    } catch (error) {
        console.error('Error al guardar el re-escaneo:', error);
        alert('Error al guardar el re-escaneo en la base de datos: ' + error.message);
    }

    closeRescanModal();
}

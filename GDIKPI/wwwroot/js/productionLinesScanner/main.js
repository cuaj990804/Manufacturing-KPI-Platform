const lineInput = document.getElementById('lineInput');
const volanteInput = document.getElementById('volanteInput');
const clearLineInput = document.getElementById('clearLineInput');
const clearVolanteInput = document.getElementById('clearVolanteInput');
const openInactivitySettings = document.getElementById('openInactivitySettings');
const inactivitySettingsModal = document.getElementById('inactivitySettingsModal');
const closeInactivitySettings = document.getElementById('closeInactivitySettings');
const cancelInactivitySettings = document.getElementById('cancelInactivitySettings');
const inactivitySettingsForm = document.getElementById('inactivitySettingsForm');
const inactivitySecondsInput = document.getElementById('inactivitySecondsInput');
const quantityModeToggle = document.getElementById('quantityModeToggle');
const lineStatus = document.getElementById('lineStatus');
const volanteStatus = document.getElementById('volanteStatus');
const volanteFieldLabel = document.getElementById('volanteFieldLabel');
const recentScansList = document.getElementById('recentScansList');
const body = document.body;
const config = window.productionLinesScannerConfig || {};
const currentAreaId = Number(config.areaId || 0) > 0 ? Number(config.areaId) : null;
const duplicateScanSuppressMs = 10000;
const minimumVolanteLength = 10;
const validationSoundVolume = 1;
const configuredBatchInactivitySeconds = Number(config.batchInactivitySeconds);
const defaultBatchInactivitySeconds = (
    Number.isFinite(configuredBatchInactivitySeconds) && configuredBatchInactivitySeconds > 0
        ? configuredBatchInactivitySeconds
        : 60
);
const storedBatchInactivitySeconds = readStoredBatchInactivitySeconds();
let batchInactivitySeconds = storedBatchInactivitySeconds || defaultBatchInactivitySeconds;

const endpoints = {
    validateLine: '/ProductionLinesScanner/ValidateLine',
    validateVolante: '/api/ScannerProductionApi/ValidateScannerValue',
    saveScan: '/api/ScannerProductionApi/SaveScan',
    saveQuantity: '/api/ProductionOperatorsDashboardApi/manual-line-production',
    recentScans: '/ProductionLinesScanner/GetRecentScans',
    scansSummary: '/ProductionLinesScanner/GetScansSummary'
};

let lineValid = false;
let validatedLineNumber = null;
let validatedLineId = null;
let isSavingScan = false;
let pendingScanKey = null;
let lastSavedScanKey = null;
let lastSavedScanAt = 0;
let batchInactivityTimer = null;
let scanFeedbackTimer = null;
let validationAudioContext = null;
let validationErrorAudioOutput = null;
let validationSoundToken = 0;
let activeValidationSounds = [];
let currentBatchQuantity = 0;
let scannerSyncConnection = null;
let synchronizedViewRefreshTimer = null;
let localScannerStateDirty = false;
let quantityModeEnabled = readStoredQuantityModeEnabled();
let pendingSynchronizedSoundState = '';
let lastRemoteSoundEventId = '';

function withAreaParam(url) {
    if (!currentAreaId) return url;
    return `${url}${url.includes('?') ? '&' : '?'}areaId=${encodeURIComponent(currentAreaId)}`;
}

lineInput.focus();
loadRecentScans();
updateInactivitySettingsButton();
applyQuantityModeUi();
initializeScannerViewSync();

document.addEventListener('keydown', unlockValidationAudio);
document.addEventListener('pointerdown', unlockValidationAudio);

lineInput.addEventListener('keydown', async event => {
    if (event.key !== 'Enter') return;
    const lineNumber = lineInput.value.trim();
    if (lineNumber) await validateLine(lineNumber);
});

lineInput.addEventListener('input', () => {
    const currentValue = lineInput.value.trim();
    if (lineValid && String(validatedLineNumber) !== currentValue) {
        invalidateLineSelection('Línea modificada, presione Enter para validar');
    }
    if (!currentValue) invalidateLineSelection('');
});

volanteInput.addEventListener('keydown', event => {
    if (event.key !== 'Enter' && event.key !== 'Tab') return;
    event.preventDefault();
    broadcastScannerViewState();
    scheduleVolanteScanSubmit();
});

volanteInput.addEventListener('input', () => {
    markBatchActivity();
});

volanteInput.addEventListener('change', scheduleVolanteScanSubmit);
volanteInput.addEventListener('focus', () => {
    if (!lineValid) lineInput.focus();
});

function scheduleVolanteScanSubmit() {
    submitVolanteScan();
}

async function validateLine(lineNumber) {
    body.classList.remove('success', 'error', 'warning');
    setLineStatus('Validando línea...', '');
    lineInput.disabled = true;
    broadcastScannerViewState();

    try {
        const response = await fetch(withAreaParam(`${endpoints.validateLine}?lineNumber=${encodeURIComponent(lineNumber)}`));
        const result = await response.json();

        if (!response.ok || !result.success) {
            invalidateLineSelection(result.message || 'Línea inválida');
            showWarningBackground();
            lineInput.value = '';
            window.queueMicrotask(() => lineInput.focus());
            return;
        }

        lineValid = true;
        validatedLineNumber = Number(result.lineData.lineNumber);
        validatedLineId = Number(result.lineData.productionLinesId);
        currentBatchQuantity = 0;
        lineInput.value = String(validatedLineNumber);
        lineInput.readOnly = true;
        setLineStatus(result.lineData.lineName
            ? `Línea verificada: ${result.lineData.lineName}`
            : 'Línea verificada', 'success-msg');
        volanteInput.disabled = false;
        markBatchActivity();
        volanteInput.focus();
    } catch {
        invalidateLineSelection('Error al validar la línea');
        showWarningBackground();
        lineInput.value = '';
        window.queueMicrotask(() => lineInput.focus());
    } finally {
        lineInput.disabled = false;
        broadcastScannerViewState();
    }
}

async function submitVolanteScan() {
    if (!lineValid || !validatedLineId) return;

    if (lineInput.value.trim() !== String(validatedLineNumber)) {
        invalidateLineSelection('Línea modificada, presione Enter para validar');
        lineInput.focus();
        return;
    }

    const code = volanteInput.value.trim();
    if (!code) return;

    if (quantityModeEnabled) {
        if (!/^\d+$/.test(code)) {
            showWarning('Ingrese una cantidad válida de piezas.');
            return;
        }

        const quantity = Number(code);
        if (!Number.isInteger(quantity) || quantity < 1 || quantity > 5000) {
            showWarning('La cantidad debe estar entre 1 y 5000 piezas.');
            return;
        }

        await saveQuantity(quantity);
        return;
    }

    if (code.length < minimumVolanteLength) {
        showWarning(`El volante debe contener al menos ${minimumVolanteLength} caracteres.`);
        return;
    }

    const customerValidation = validateCustomerScan(code);
    if (!customerValidation.isValid) {
        showWarning(customerValidation.message);
        return;
    }

    await saveScan(code);
}

async function saveQuantity(quantity) {
    if (isSavingScan) {
        volanteInput.value = '';
        broadcastScannerViewState();
        return;
    }

    isSavingScan = true;
    lineInput.disabled = true;
    volanteInput.disabled = true;
    setVolanteStatus(`Agregando ${quantity} ${quantity === 1 ? 'pieza' : 'piezas'}...`, '');
    broadcastScannerViewState();

    try {
        const response = await fetch(endpoints.saveQuantity, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                productionLinesId: validatedLineId,
                quantity,
                producedAt: formatLocalDateTimeForRequest(new Date()),
                areaId: currentAreaId
            })
        });
        const result = await response.json();

        if (!response.ok || !result.success) {
            throw new Error(result.message || 'No se pudieron agregar las piezas');
        }

        showTemporaryScanFeedback('success');
        currentBatchQuantity += quantity;
        markBatchActivity();
        setVolanteStatus(
            `${quantity} ${quantity === 1 ? 'pieza agregada' : 'piezas agregadas'}`,
            'success-msg');
        broadcastScannerViewState(2000);
        volanteInput.value = '';
    } catch (error) {
        showError(error.message || 'Error al agregar las piezas');
    } finally {
        isSavingScan = false;
        lineInput.disabled = false;
        if (lineValid) volanteInput.disabled = false;
        const synchronizedScreenState = ['success', 'error']
            .find(state => body.classList.contains(state));
        broadcastScannerViewState(synchronizedScreenState ? 2000 : 0);
        if (lineValid) volanteInput.focus();
    }
}

async function saveScan(code) {
    const scanKey = `${validatedLineId}|${String(code).trim().toUpperCase()}`;
    const now = Date.now();

    if (lastSavedScanKey === scanKey && now - lastSavedScanAt < duplicateScanSuppressMs) {
        prepareNextVolante();
        broadcastScannerViewState();
        return;
    }

    if (isSavingScan || pendingScanKey === scanKey) {
        volanteInput.value = '';
        broadcastScannerViewState();
        return;
    }

    isSavingScan = true;
    pendingScanKey = scanKey;
    lineInput.disabled = true;
    volanteInput.disabled = true;
    setVolanteStatus('Validando volante...', '');
    broadcastScannerViewState();

    try {
        const validationResponse = await fetch(`${endpoints.validateVolante}?scannerValue=${encodeURIComponent(code)}`);
        const validation = await validationResponse.json();

        if (!validationResponse.ok) {
            throw new Error(validation.error || 'No se pudo validar el volante');
        }

        if (!validation.isValid) {
            const previousDate = validation.scanDetails?.scanDateTime
                ? formatScanDateTime(validation.scanDetails.scanDateTime)
                : '';
            showError(previousDate
                ? `Este volante ya fue escaneado (${previousDate}).`
                : (validation.message || 'Este volante ya fue escaneado.'));
            return;
        }

        setVolanteStatus('Guardando volante...', '');
        broadcastScannerViewState();
        const response = await fetch(endpoints.saveScan, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                lineId: validatedLineId,
                scannerValue: code,
                rejectDuplicate: true,
                saveScannerProductionHistory: true
            })
        });
        const result = await response.json();

        if (!response.ok || !result.success) {
            const message = result.error || result.message || 'No se pudo guardar el volante';
            if (isAlteredScanMessage(message)) {
                showWarning(message);
                return;
            }
            showError(message);
            return;
        }

        showTemporaryScanFeedback('success');
        lastSavedScanKey = scanKey;
        lastSavedScanAt = Date.now();
        currentBatchQuantity++;
        markBatchActivity();
        setVolanteStatus('Volante guardado', 'success-msg');
        broadcastScannerViewState(2000);
        scheduleSynchronizedViewRefresh();
        window.queueMicrotask(prepareNextVolante);
    } catch (error) {
        showError(error.message || 'Error al guardar el volante');
    } finally {
        isSavingScan = false;
        pendingScanKey = null;
        lineInput.disabled = false;
        if (lineValid) volanteInput.disabled = false;
        const synchronizedScreenState = ['success', 'error']
            .find(state => body.classList.contains(state));
        broadcastScannerViewState(synchronizedScreenState ? 2000 : 0);
    }
}

function showError(message) {
    showTemporaryScanFeedback('error');
    volanteInput.value = '';
    setVolanteStatus(message, 'error-msg');
    broadcastScannerViewState(2000);
    window.queueMicrotask(prepareNextVolante);
}

function showWarning(message) {
    showWarningBackground();
    volanteInput.value = '';
    volanteInput.disabled = false;
    setVolanteStatus(message || 'Lectura alterada. Reescanee la pieza.', 'warning-msg');
    broadcastScannerViewState();
    volanteInput.focus();
}

function showWarningBackground() {
    window.clearTimeout(scanFeedbackTimer);
    scanFeedbackTimer = null;
    body.classList.remove('success', 'error');
    body.classList.add('warning');
    playValidationSound('warning');
    pendingSynchronizedSoundState = 'warning';
}

function showTemporaryScanFeedback(state) {
    window.clearTimeout(scanFeedbackTimer);
    body.classList.remove('success', 'error', 'warning');
    body.classList.add(state);
    playValidationSound(state);
    pendingSynchronizedSoundState = state;

    scanFeedbackTimer = window.setTimeout(() => {
        body.classList.remove(state);
        scanFeedbackTimer = null;
        broadcastScannerViewState();
    }, 2000);
}

function unlockValidationAudio() {
    const audioContext = getValidationAudioContext();
    if (audioContext?.state === 'suspended') {
        audioContext.resume().catch(() => {});
    }
}

function getValidationAudioContext() {
    if (validationAudioContext) return validationAudioContext;

    const AudioContextType = window.AudioContext || window.webkitAudioContext;
    if (!AudioContextType) return null;

    try {
        validationAudioContext = new AudioContextType();
        const compressor = validationAudioContext.createDynamicsCompressor();
        validationErrorAudioOutput = validationAudioContext.createGain();
        compressor.threshold.setValueAtTime(-16, validationAudioContext.currentTime);
        compressor.knee.setValueAtTime(10, validationAudioContext.currentTime);
        compressor.ratio.setValueAtTime(8, validationAudioContext.currentTime);
        compressor.attack.setValueAtTime(0.003, validationAudioContext.currentTime);
        compressor.release.setValueAtTime(0.18, validationAudioContext.currentTime);
        validationErrorAudioOutput.connect(compressor);
        compressor.connect(validationAudioContext.destination);
        return validationAudioContext;
    } catch {
        return null;
    }
}

function playValidationSound(state) {
    const patterns = {
        success: [
            { frequency: 659, offset: 0, duration: 0.14, type: 'sine', volume: 0.62 },
            { frequency: 880, offset: 0.11, duration: 0.17, type: 'sine', volume: 0.72 },
            { frequency: 1319, offset: 0.24, duration: 0.28, type: 'triangle', volume: 0.9 }
        ],
        error: [
            { frequency: 980, endFrequency: 620, offset: 0, duration: 0.38, type: 'square', volume: 1 },
            { frequency: 490, endFrequency: 310, offset: 0, duration: 0.38, type: 'sawtooth', volume: 0.72 },
            { frequency: 880, endFrequency: 560, offset: 0.46, duration: 0.38, type: 'square', volume: 1 },
            { frequency: 440, endFrequency: 280, offset: 0.46, duration: 0.38, type: 'sawtooth', volume: 0.72 },
            { frequency: 980, endFrequency: 540, offset: 0.92, duration: 0.62, type: 'square', volume: 1 },
            { frequency: 490, endFrequency: 270, offset: 0.92, duration: 0.62, type: 'sawtooth', volume: 0.78 }
        ],
        warning: [
            { frequency: 740, offset: 0, duration: 0.16, type: 'triangle', volume: 0.9 },
            { frequency: 740, offset: 0.21, duration: 0.16, type: 'triangle', volume: 0.9 },
            { frequency: 587, offset: 0.42, duration: 0.24, type: 'sine', volume: 0.82 }
        ]
    };
    const pattern = patterns[state];
    if (!pattern) return;

    const audioContext = getValidationAudioContext();
    if (!audioContext) return;

    const soundToken = ++validationSoundToken;
    const play = () => {
        if (soundToken !== validationSoundToken) return;

        activeValidationSounds.forEach(node => {
            try { node.stop(); } catch { }
        });
        activeValidationSounds = [];

        const now = audioContext.currentTime;
        if (state === 'error') {
            validationErrorAudioOutput.gain.cancelScheduledValues(now);
            validationErrorAudioOutput.gain.setValueAtTime(1.8, now);
        }
        pattern.forEach(tone => {
            const oscillator = audioContext.createOscillator();
            const gain = audioContext.createGain();
            const start = now + tone.offset;
            const end = start + tone.duration;

            oscillator.type = tone.type;
            oscillator.frequency.setValueAtTime(tone.frequency, start);
            if (tone.endFrequency) {
                oscillator.frequency.exponentialRampToValueAtTime(tone.endFrequency, end);
            }
            gain.gain.setValueAtTime(0.0001, start);
            gain.gain.exponentialRampToValueAtTime(
                validationSoundVolume * tone.volume,
                start + 0.015);
            gain.gain.exponentialRampToValueAtTime(0.0001, end);
            oscillator.connect(gain);
            gain.connect(state === 'error' ? validationErrorAudioOutput : audioContext.destination);
            oscillator.start(start);
            oscillator.stop(end + 0.02);
            oscillator.addEventListener('ended', () => {
                activeValidationSounds = activeValidationSounds.filter(node => node !== oscillator);
            });
            activeValidationSounds.push(oscillator);
        });
    };

    if (audioContext.state === 'suspended') {
        audioContext.resume().then(play).catch(() => {});
    } else {
        play();
    }
}

function invalidateLineSelection(message) {
    clearBatchInactivityTimeout();
    lineValid = false;
    validatedLineNumber = null;
    validatedLineId = null;
    currentBatchQuantity = 0;
    lineInput.readOnly = false;
    volanteInput.value = '';
    volanteInput.disabled = true;
    setLineStatus(message, message ? 'error-msg' : '');
    setVolanteStatus('', '');
}

function markBatchActivity() {
    clearBatchInactivityTimeout();
    if (!lineValid) return;

    batchInactivityTimer = window.setTimeout(
        finishBatchForInactivity,
        batchInactivitySeconds * 1000);
}

function clearBatchInactivityTimeout() {
    window.clearTimeout(batchInactivityTimer);
    batchInactivityTimer = null;
}

function finishBatchForInactivity() {
    batchInactivityTimer = null;

    if (!lineValid) return;
    if (isSavingScan) {
        batchInactivityTimer = window.setTimeout(finishBatchForInactivity, 1000);
        return;
    }

    const finishedQuantity = currentBatchQuantity;
    const inactivityLabel = formatInactivityDuration(batchInactivitySeconds);
    resetInputsForNextLine();
    showWarningBackground();
    setLineStatus(
        finishedQuantity === 1
            ? `Tanda finalizada automaticamente por ${inactivityLabel} de inactividad (1 volante).`
            : `Tanda finalizada automaticamente por ${inactivityLabel} de inactividad (${finishedQuantity} volantes).`,
        'warning-msg');
    broadcastScannerViewState();
}

function formatInactivityDuration(seconds) {
    if (seconds >= 60 && seconds % 60 === 0) {
        const minutes = seconds / 60;
        return `${minutes} ${minutes === 1 ? 'minuto' : 'minutos'}`;
    }

    return `${seconds} ${seconds === 1 ? 'segundo' : 'segundos'}`;
}

function readStoredBatchInactivitySeconds() {
    try {
        const value = Number(window.localStorage.getItem('productionLinesScanner.batchInactivitySeconds'));
        return Number.isInteger(value) && value >= 1 && value <= 86400 ? value : null;
    } catch {
        return null;
    }
}

function storeBatchInactivitySeconds(seconds) {
    try {
        window.localStorage.setItem('productionLinesScanner.batchInactivitySeconds', String(seconds));
    } catch {
        // El valor sigue activo durante la sesión aunque el navegador no permita persistirlo.
    }
}

function readStoredQuantityModeEnabled() {
    try {
        return window.localStorage.getItem('productionLinesScanner.quantityModeEnabled') === 'true';
    } catch {
        return false;
    }
}

function storeQuantityModeEnabled(isEnabled) {
    try {
        window.localStorage.setItem(
            'productionLinesScanner.quantityModeEnabled',
            isEnabled ? 'true' : 'false');
    } catch {
        // El modo sigue activo durante la sesión aunque no pueda persistirse.
    }
}

function applyQuantityModeUi() {
    volanteFieldLabel.textContent = quantityModeEnabled ? 'Cantidad de volantes' : 'Volante';
    volanteInput.placeholder = quantityModeEnabled ? 'Cantidad...' : 'Escanear...';
    volanteInput.inputMode = quantityModeEnabled ? 'numeric' : 'text';
    volanteInput.minLength = quantityModeEnabled ? 0 : minimumVolanteLength;
    updateInactivitySettingsButton();
}

function updateInactivitySettingsButton() {
    const modeLabel = quantityModeEnabled ? ' · Cantidad activa' : '';
    openInactivitySettings.textContent =
        `Inactividad: ${formatInactivityDuration(batchInactivitySeconds)}${modeLabel}`;
}

function openInactivitySettingsModal() {
    clearBatchInactivityTimeout();
    inactivitySecondsInput.value = String(batchInactivitySeconds);
    quantityModeToggle.checked = quantityModeEnabled;
    inactivitySettingsModal.classList.add('active');
    inactivitySecondsInput.focus();
    inactivitySecondsInput.select();
}

function closeInactivitySettingsModal() {
    inactivitySettingsModal.classList.remove('active');
    markBatchActivity();
    (lineValid ? volanteInput : lineInput).focus();
}

openInactivitySettings.addEventListener('click', openInactivitySettingsModal);
closeInactivitySettings.addEventListener('click', closeInactivitySettingsModal);
cancelInactivitySettings.addEventListener('click', closeInactivitySettingsModal);
inactivitySettingsModal.addEventListener('click', event => {
    if (event.target === inactivitySettingsModal) closeInactivitySettingsModal();
});

inactivitySettingsForm.addEventListener('submit', event => {
    event.preventDefault();
    const seconds = Number(inactivitySecondsInput.value);

    if (!Number.isInteger(seconds) || seconds < 1 || seconds > 86400) {
        inactivitySecondsInput.focus();
        return;
    }

    batchInactivitySeconds = seconds;
    const quantityModeChanged = quantityModeEnabled !== quantityModeToggle.checked;
    quantityModeEnabled = quantityModeToggle.checked;
    storeBatchInactivitySeconds(seconds);
    storeQuantityModeEnabled(quantityModeEnabled);
    applyQuantityModeUi();
    if (quantityModeChanged) {
        volanteInput.value = '';
        setVolanteStatus('', '');
    }
    updateInactivitySettingsButton();
    broadcastScannerViewState();
    closeInactivitySettingsModal();
});

function resetForm() {
    body.classList.remove('success', 'error', 'warning');
    resetInputsForNextLine();
    broadcastScannerViewState();
}

function prepareNextVolante() {
    volanteInput.value = '';

    if (!lineValid) {
        resetInputsForNextLine();
        return;
    }

    lineInput.value = String(validatedLineNumber);
    lineInput.readOnly = true;
    volanteInput.disabled = false;
    volanteInput.focus();
}

function resetInputsForNextLine() {
    lineInput.value = '';
    volanteInput.value = '';
    invalidateLineSelection('');
    lineInput.disabled = false;
    lineInput.readOnly = false;
    lineInput.focus();
}

clearLineInput.addEventListener('click', resetForm);
clearVolanteInput.addEventListener('click', () => {
    volanteInput.value = '';
    setVolanteStatus('', '');
    broadcastScannerViewState();
    (lineValid ? volanteInput : lineInput).focus();
});

function setLineStatus(message, className) {
    lineStatus.textContent = message;
    lineStatus.className = className ? `status-text ${className}` : 'status-text';
}

function setVolanteStatus(message, className) {
    volanteStatus.textContent = message;
    volanteStatus.className = className ? `status-text ${className}` : 'status-text';
}

function isZfCustomer() {
    return `${config.customerName || ''} ${config.areaName || ''}`.toLowerCase().includes('zf');
}

function normalizeRuleList(values) {
    return Array.isArray(values)
        ? values.map(value => String(value || '').trim().toUpperCase()).filter(Boolean)
        : [];
}

function validateCustomerScan(code) {
    if (!isZfCustomer()) return { isValid: true };
    const rules = config.scannerValidation?.ZF || config.scannerValidation?.zf || {};
    if (rules.enabled === false) return { isValid: true };

    const normalized = String(code || '').trim().toUpperCase();
    const prefixes = normalizeRuleList(rules.allowedPrefixes);
    const suffixes = normalizeRuleList(rules.allowedSuffixes);
    const prefixValid = prefixes.length === 0 || prefixes.some(value => normalized.startsWith(value));
    const suffixValid = suffixes.length === 0 || suffixes.some(value => normalized.endsWith(value));

    return prefixValid && suffixValid
        ? { isValid: true }
        : { isValid: false, message: 'Lectura alterada. Reescanee la pieza.' };
}

function isAlteredScanMessage(message) {
    return String(message || '').toLowerCase().includes('lectura alterada');
}

async function loadRecentScans() {
    recentScansList.innerHTML = '<div class="recent-scans-empty">Cargando...</div>';
    try {
        const response = await fetch(withAreaParam(`${endpoints.recentScans}?limit=5`));
        const result = await response.json();
        if (!response.ok || !result.success || result.scans.length === 0) {
            recentScansList.innerHTML = '<div class="recent-scans-empty">Sin escaneos recientes</div>';
            return;
        }
        recentScansList.innerHTML = result.scans.map(getRecentScanMarkup).join('');
    } catch {
        recentScansList.innerHTML = '<div class="recent-scans-empty">No se pudieron cargar los escaneos</div>';
    }
}

function scheduleSynchronizedViewRefresh() {
    window.clearTimeout(synchronizedViewRefreshTimer);
    synchronizedViewRefreshTimer = window.setTimeout(async () => {
        synchronizedViewRefreshTimer = null;
        await loadRecentScans();

        if (scansModal?.classList.contains('active')) {
            await loadModalScans();
        }
    }, 100);
}

async function initializeScannerViewSync() {
    if (typeof signalR === 'undefined') {
        console.warn('SignalR no está disponible; la vista no se sincronizará en tiempo real.');
        return;
    }

    scannerSyncConnection = new signalR.HubConnectionBuilder()
        .withUrl('/dashboardHub')
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .build();

    scannerSyncConnection.on('ProductionLinesScannerUpdated', () => {
        scheduleSynchronizedViewRefresh();
    });

    scannerSyncConnection.on('ProductionLinesScannerViewStateChanged', state => {
        applySynchronizedScannerViewState(state, true);
    });

    scannerSyncConnection.onreconnected(async () => {
        scheduleSynchronizedViewRefresh();
        await synchronizeScannerViewAfterConnection();
    });

    try {
        await scannerSyncConnection.start();
        scheduleSynchronizedViewRefresh();
        await synchronizeScannerViewAfterConnection();
    } catch (error) {
        console.error('No se pudo iniciar la sincronización del escáner:', error);
        window.setTimeout(initializeScannerViewSync, 5000);
    }
}

async function synchronizeScannerViewAfterConnection() {
    if (!scannerSyncConnection || scannerSyncConnection.state !== signalR.HubConnectionState.Connected) {
        return;
    }

    if (localScannerStateDirty) {
        broadcastScannerViewState();
        return;
    }

    try {
        const state = await scannerSyncConnection.invoke('GetProductionLinesScannerViewState');
        if (state) applySynchronizedScannerViewState(state, false);
    } catch (error) {
        console.error('No se pudo recuperar el estado sincronizado:', error);
    }
}

function broadcastScannerViewState(feedbackDurationMs = 0) {
    localScannerStateDirty = true;
    const soundState = pendingSynchronizedSoundState;
    pendingSynchronizedSoundState = '';

    if (!scannerSyncConnection ||
        typeof signalR === 'undefined' ||
        scannerSyncConnection.state !== signalR.HubConnectionState.Connected) {
        return;
    }

    const screenState = ['success', 'error', 'warning']
        .find(state => body.classList.contains(state)) || '';
    const soundEventId = soundState
        ? createScannerSoundEventId()
        : '';

    scannerSyncConnection.invoke('BroadcastProductionLinesScannerViewState', {
        lineValue: lineInput.value,
        volanteValue: volanteInput.value,
        lineValid,
        validatedLineNumber,
        validatedLineId,
        currentBatchQuantity,
        lineStatus: lineStatus.textContent,
        lineStatusClass: getSynchronizedStatusClass(lineStatus),
        volanteStatus: volanteStatus.textContent,
        volanteStatusClass: getSynchronizedStatusClass(volanteStatus),
        screenState,
        feedbackDurationMs,
        lineReadOnly: lineInput.readOnly,
        lineDisabled: lineInput.disabled,
        volanteDisabled: volanteInput.disabled,
        quantityModeEnabled,
        soundState,
        soundEventId
    }).catch(error => {
        console.error('No se pudo compartir el estado del escáner:', error);
    });
}

function applySynchronizedScannerViewState(state, shouldPlaySound) {
    if (!state) return;

    clearBatchInactivityTimeout();
    window.clearTimeout(scanFeedbackTimer);
    scanFeedbackTimer = null;

    lineValid = Boolean(state.lineValid);
    validatedLineNumber = Number(state.validatedLineNumber) > 0
        ? Number(state.validatedLineNumber)
        : null;
    validatedLineId = Number(state.validatedLineId) > 0
        ? Number(state.validatedLineId)
        : null;
    currentBatchQuantity = Math.max(Number(state.currentBatchQuantity) || 0, 0);
    quantityModeEnabled = Boolean(state.quantityModeEnabled);
    storeQuantityModeEnabled(quantityModeEnabled);
    applyQuantityModeUi();

    lineInput.value = String(state.lineValue || '');
    volanteInput.value = String(state.volanteValue || '');
    lineInput.readOnly = Boolean(state.lineReadOnly);
    lineInput.disabled = Boolean(state.lineDisabled);
    volanteInput.disabled = Boolean(state.volanteDisabled);
    setLineStatus(state.lineStatus || '', state.lineStatusClass || '');
    setVolanteStatus(state.volanteStatus || '', state.volanteStatusClass || '');

    body.classList.remove('success', 'error', 'warning');
    let screenState = ['success', 'error', 'warning'].includes(state.screenState)
        ? state.screenState
        : '';

    const feedbackExpiresAt = state.feedbackExpiresAtUtc
        ? new Date(state.feedbackExpiresAtUtc).getTime()
        : 0;
    const feedbackRemainingMs = feedbackExpiresAt - Date.now();
    if (feedbackExpiresAt && feedbackRemainingMs <= 0) {
        screenState = '';
    }

    if (screenState) body.classList.add(screenState);

    const synchronizedSoundState = ['success', 'error', 'warning'].includes(state.soundState)
        ? state.soundState
        : '';
    const synchronizedSoundEventId = String(state.soundEventId || '');
    if (shouldPlaySound &&
        synchronizedSoundState &&
        synchronizedSoundEventId &&
        synchronizedSoundEventId !== lastRemoteSoundEventId) {
        lastRemoteSoundEventId = synchronizedSoundEventId;
        playValidationSound(synchronizedSoundState);
    }
    if (screenState && feedbackRemainingMs > 0) {
        scanFeedbackTimer = window.setTimeout(() => {
            body.classList.remove(screenState);
            scanFeedbackTimer = null;
        }, feedbackRemainingMs);
    }

    if (lineValid) markBatchActivity();

    const hasOpenModal = document.querySelector('.modal-overlay.active');
    if (!hasOpenModal) {
        const focusTarget = lineValid && !volanteInput.disabled
            ? volanteInput
            : (!lineInput.disabled ? lineInput : null);
        focusTarget?.focus({ preventScroll: true });
    }
}

function getSynchronizedStatusClass(element) {
    return ['success-msg', 'error-msg', 'warning-msg']
        .find(className => element.classList.contains(className)) || '';
}

function createScannerSoundEventId() {
    if (window.crypto?.randomUUID) return window.crypto.randomUUID();
    return `${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

function getRecentScanMarkup(scan) {
    const lineLabel = `Línea ${escapeHtml(String(scan.lineNumber))}`;
    const scannerValue = escapeHtml(scan.scannerValue || 'Sin código');
    return `<div class="recent-scan-item">
        <div class="recent-scan-main">
            <span class="recent-scan-name">${lineLabel}</span>
            <span class="recent-scan-code">Código: ${scannerValue}</span>
        </div>
        <span class="recent-scan-time">${formatScanTime(scan.scannerProductionDateTime)}</span>
    </div>`;
}

const scansModal = document.getElementById('scansModal');
const openScansModal = document.getElementById('openScansModal');
const closeScansModal = document.getElementById('closeScansModal');
const modalScansContent = document.getElementById('modalScansContent');
const modalSearchInput = document.getElementById('modalSearchInput');

closeScansModal.addEventListener('click', closeModal);
scansModal.addEventListener('click', event => {
    if (event.target === scansModal) closeModal();
});

function closeModal() {
    scansModal.classList.remove('active');
    (lineValid ? volanteInput : lineInput).focus();
}

openScansModal.addEventListener('click', async () => {
    scansModal.classList.add('active');
    modalSearchInput.value = '';
    await loadModalScans();
});

modalSearchInput.addEventListener('input', function () {
    const search = this.value.toLowerCase().trim();
    modalScansContent.querySelectorAll('.modal-scan-item').forEach(item => {
        item.style.display = item.textContent.toLowerCase().includes(search) ? '' : 'none';
    });
});

async function loadModalScans() {
    modalScansContent.innerHTML = '<div class="recent-scans-empty">Cargando...</div>';
    try {
        const response = await fetch(withAreaParam(endpoints.scansSummary));
        const result = await response.json();
        if (!response.ok || !result.success || result.scans.length === 0) {
            modalScansContent.innerHTML = '<div class="recent-scans-empty">Sin registros hoy</div>';
            return;
        }
        modalScansContent.innerHTML = result.scans.map(scan => `<div class="modal-scan-item">
            <div>
                <div class="modal-scan-name">Línea ${escapeHtml(String(scan.lineNumber))}</div>
                <div class="modal-scan-operation">${escapeHtml(scan.lineName || 'Sin nombre')}</div>
            </div>
            <div class="modal-scan-right">
                <div class="modal-scan-pieces">${Number(scan.quantity)} ${Number(scan.quantity) === 1 ? 'Pieza' : 'Piezas'}</div>
                <div class="modal-scan-time">Último escaneo: ${formatLastScanTime(scan.lastScannedAt)}</div>
            </div>
        </div>`).join('');
    } catch {
        modalScansContent.innerHTML = '<div class="recent-scans-empty">Error al cargar</div>';
    }
}

function formatScanTime(value) {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '' : date.toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' });
}

function formatLastScanTime(value) {
    if (!value) return 'Sin registro';
    return formatScanTime(value) || 'Sin registro';
}

function formatScanDateTime(value) {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '' : date.toLocaleString('es-MX');
}

function formatLocalDateTimeForRequest(date) {
    const pad = value => String(value).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` +
        `T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

function escapeHtml(value) {
    return String(value || '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}

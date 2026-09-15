const employeeInput = document.getElementById('employeeInput');
const volanteInput = document.getElementById('volanteInput');
const clearEmployeeInput = document.getElementById('clearEmployeeInput');
const clearVolanteInput = document.getElementById('clearVolanteInput');
const employeeStatus = document.getElementById('employeeStatus');
const volanteStatus = document.getElementById('volanteStatus');
const recentScansList = document.getElementById('recentScansList');
const body = document.body;
const config = window.productionOperatorsConfig || {};
const currentAreaId = Number(config.areaId || 0) > 0 ? Number(config.areaId) : null;
const duplicateScanSuppressMs = 10000;
const successResetDelayMs = 3000;
const scanSettleDelayMs = 200;
const validationSoundVolume = 1;

const endpoints = {
    validateEmployee: '/ProductionOperators/ValidateEmployee',
    saveScan: '/ProductionOperators/SaveScan',
    recentScans: '/ProductionOperators/GetRecentScans',
    scansSummary: '/ProductionOperators/GetScansSummary'
};

let employeeValid = false;
let validatedEmployeeNumber = null;
let isSavingScan = false;
let pendingScanKey = null;
let lastSavedScanKey = null;
let lastSavedScanAt = 0;
let lastVolanteInputAt = 0;
let submitVolanteTimer = null;
let validationAudioContext = null;
let validationErrorAudioOutput = null;
let validationSoundToken = 0;
let activeValidationSounds = [];
let employeeValidationFeedbackTimer = null;

function withAreaParam(url) {
    if (!currentAreaId) {
        return url;
    }

    const separator = url.includes('?') ? '&' : '?';
    return `${url}${separator}areaId=${encodeURIComponent(currentAreaId)}`;
}

employeeInput.focus();
loadRecentScans();

document.addEventListener('keydown', unlockValidationAudio);
document.addEventListener('pointerdown', unlockValidationAudio);

employeeInput.addEventListener('keydown', async function (e) {
    if (e.key !== 'Enter') {
        return;
    }

    const employeeNumber = employeeInput.value.trim();

    if (employeeNumber.length === 0) {
        return;
    }

    await validateEmployee(employeeNumber);
});

employeeInput.addEventListener('input', function () {
    const currentValue = employeeInput.value.trim();

    if (employeeValid && String(validatedEmployeeNumber) !== currentValue) {
        invalidateEmployeeSelection('Empleado modificado, presione Enter para validar');
    }

    if (currentValue.length === 0) {
        invalidateEmployeeSelection('');
    }
});

volanteInput.addEventListener('keydown', async function (e) {
    if (e.key !== 'Enter' && e.key !== 'Tab') {
        return;
    }

    e.preventDefault();
    scheduleVolanteScanSubmit();
});

volanteInput.addEventListener('input', function () {
    lastVolanteInputAt = Date.now();
});

volanteInput.addEventListener('change', function () {
    scheduleVolanteScanSubmit();
});

function scheduleVolanteScanSubmit() {
    window.clearTimeout(submitVolanteTimer);

    submitVolanteTimer = window.setTimeout(async function () {
        const elapsedSinceInput = Date.now() - lastVolanteInputAt;

        if (elapsedSinceInput < scanSettleDelayMs) {
            scheduleVolanteScanSubmit();
            return;
        }

        await submitVolanteScan();
    }, scanSettleDelayMs);
}

async function submitVolanteScan() {
    if (!employeeValid || validatedEmployeeNumber === null) {
        return;
    }

    if (employeeInput.value.trim() !== String(validatedEmployeeNumber)) {
        invalidateEmployeeSelection('Empleado modificado, presione Enter para validar');
        employeeInput.focus();
        return;
    }

    const code = volanteInput.value.trim();
    const employeeNumberForScan = validatedEmployeeNumber;

    if (!isUnreadableOverrideCode(code)) {
        const customerValidation = validateCustomerScan(code);
        if (!customerValidation.isValid) {
            showAlteredScanWarning(customerValidation.message);
            return;
        }
    }

    if (await handleEmployeeNumberInVolanteInput(code)) {
        return;
    }

    await saveScan(code, employeeNumberForScan);
}

volanteInput.addEventListener('focus', function () {
    if (!employeeValid) {
        employeeInput.focus();
    }
});

async function validateEmployee(employeeNumber) {
    setEmployeeStatus('Validando empleado...', '');
    employeeInput.disabled = true;

    try {
        const response = await fetch(withAreaParam(`${endpoints.validateEmployee}?employeeNumber=${encodeURIComponent(employeeNumber)}`));
        const result = await response.json();

        if (!response.ok || !result.success) {
            employeeValid = false;
            validatedEmployeeNumber = null;
            volanteInput.disabled = true;
            showEmployeeValidationErrorFeedback();
            setEmployeeStatus(result.message || 'Empleado invalido', 'error-msg');
            employeeInput.select();
            return;
        }

        if (result.operatorData?.goalReached) {
            const currentQuantity = Number(result.operatorData.currentQuantity || 0);
            const goal = Number(result.operatorData.goal || 0);
            employeeValid = false;
            validatedEmployeeNumber = null;
            employeeInput.value = String(result.operatorData.employeeNumber || employeeNumber);
            volanteInput.value = '';
            volanteInput.disabled = true;
            body.classList.remove('success', 'error');
            body.classList.add('warning');
            playValidationSound('warning');
            setEmployeeStatus(`Meta diaria cumplida (${currentQuantity}/${goal})`, 'warning-msg');
            setVolanteStatus('No se permiten mas escaneos para este operador.', 'warning-msg');
            employeeInput.select();
            return;
        }

        employeeValid = true;
        validatedEmployeeNumber = Number(result.operatorData?.employeeNumber || employeeNumber);
        employeeInput.value = String(validatedEmployeeNumber);
        setEmployeeStatus('Empleado verificado', 'success-msg');
        volanteInput.disabled = false;
        setTimeout(() => volanteInput.focus(), 150);
    } catch (error) {
        employeeValid = false;
        validatedEmployeeNumber = null;
        volanteInput.disabled = true;
        showEmployeeValidationErrorFeedback();
        setEmployeeStatus('Error al validar empleado', 'error-msg');
        employeeInput.select();
    } finally {
        employeeInput.disabled = false;
    }
}

async function handleEmployeeNumberInVolanteInput(value) {
    const employeeNumber = String(value || '').trim();

    if (!/^\d+$/.test(employeeNumber)) {
        return false;
    }

    try {
        const response = await fetch(withAreaParam(`${endpoints.validateEmployee}?employeeNumber=${encodeURIComponent(employeeNumber)}`));
        const result = await response.json();

        if (!response.ok || !result.success) {
            return false;
        }

        resetInputsForNextEmployee();
        employeeInput.value = employeeNumber;
        await validateEmployee(employeeNumber);
        return true;
    } catch {
        return false;
    }
}

function invalidateEmployeeSelection(message) {
    employeeValid = false;
    validatedEmployeeNumber = null;
    volanteInput.value = '';
    volanteInput.disabled = true;
    setEmployeeStatus(message, message ? 'error-msg' : '');
    setVolanteStatus('', '');
}

async function saveScan(code, employeeNumber) {
    const scanKey = `${employeeNumber}|${currentAreaId || ''}|${normalizeVolanteCode(code)}`;
    const now = Date.now();

    if (lastSavedScanKey === scanKey && now - lastSavedScanAt < duplicateScanSuppressMs) {
        resetInputsForNextEmployee();
        return;
    }

    if (isSavingScan || pendingScanKey === scanKey) {
        volanteInput.value = '';
        return;
    }

    isSavingScan = true;
    pendingScanKey = scanKey;
    setVolanteStatus('Guardando codigo...', '');
    employeeInput.disabled = true;
    volanteInput.disabled = true;

    try {
        const response = await fetch(endpoints.saveScan, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                employeeNumber,
                code,
                areaId: currentAreaId
            })
        });

        const result = await response.json();

        if (!response.ok || !result.success) {
            const message = result.message || 'No se pudo guardar el codigo';
            if (isAlteredScanMessage(message)) {
                showAlteredScanWarning(message);
                return;
            }

            body.classList.remove('success', 'warning');
            body.classList.add('error');
            playValidationSound('error');
            setVolanteStatus(message, 'error-msg');
            setTimeout(resetForm, 3000);
            return;
        }

        body.classList.remove('error', 'warning');
        body.classList.add('success');
        playValidationSound('success');
        lastSavedScanKey = scanKey;
        lastSavedScanAt = Date.now();
        if (result.ignored) {
            resetInputsForNextEmployee();
            setTimeout(resetForm, successResetDelayMs);
            return;
        }

        setVolanteStatus(
            result.goalReached
                ? `Codigo guardado. Meta cumplida (${result.currentQuantity}/${result.goal})`
                : 'Codigo guardado',
            'success-msg');
        addRecentScan(result.scan);
        setTimeout(resetForm, successResetDelayMs);
    } catch (error) {
        body.classList.remove('success', 'warning');
        body.classList.add('error');
        playValidationSound('error');
        setVolanteStatus('Error al guardar el codigo', 'error-msg');
        setTimeout(resetForm, 3000);
    } finally {
        isSavingScan = false;
        pendingScanKey = null;
        employeeInput.disabled = false;
    }
}

function isAlteredScanMessage(message) {
    return String(message || '').trim().toLowerCase().includes('lectura alterada');
}

function showAlteredScanWarning(message) {
    body.classList.remove('success', 'error');
    body.classList.add('warning');
    playValidationSound('warning');
    volanteInput.value = '';
    volanteInput.disabled = false;
    setVolanteStatus(message || 'Lectura alterada. Reescanea la pieza.', 'warning-msg');
    window.setTimeout(() => volanteInput.focus(), 50);
}

function unlockValidationAudio() {
    const audioContext = getValidationAudioContext();
    if (audioContext?.state === 'suspended') {
        audioContext.resume().catch(() => {});
    }
}

function showEmployeeValidationErrorFeedback() {
    window.clearTimeout(employeeValidationFeedbackTimer);
    body.classList.remove('success', 'warning');
    body.classList.add('error');
    playValidationSound('error');

    employeeValidationFeedbackTimer = window.setTimeout(() => {
        body.classList.remove('error');
        employeeValidationFeedbackTimer = null;
    }, 2000);
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

function normalizeVolanteCode(code) {
    const normalized = String(code || '').trim().toUpperCase().replace(/\s+/g, '');
    const bIndex = normalized.indexOf('B');
    const mxIndex = normalized.indexOf('MX');

    if (bIndex >= 0 && mxIndex > bIndex) {
        return `${normalized.slice(bIndex, mxIndex)}MX`;
    }

    return normalized;
}

function isZfCustomer() {
    return `${config.customerName || ''} ${config.areaName || ''}`.toLowerCase().includes('zf');
}

function isUnreadableOverrideCode(code) {
    return String(code || '').trim().toUpperCase().startsWith('UNREADABLE');
}

function normalizeRuleList(values) {
    return Array.isArray(values)
        ? values.map(value => String(value || '').trim().toUpperCase()).filter(Boolean)
        : [];
}

function validateCustomerScan(code) {
    if (!isZfCustomer()) {
        return { isValid: true };
    }

    const zfRules = config.scannerValidation?.ZF || config.scannerValidation?.zf || {};
    if (zfRules.enabled === false) {
        return { isValid: true };
    }

    const normalizedCode = String(code || '').trim().toUpperCase();
    const allowedPrefixes = normalizeRuleList(zfRules.allowedPrefixes);
    const allowedSuffixes = normalizeRuleList(zfRules.allowedSuffixes);

    const hasValidPrefix = allowedPrefixes.length === 0
        || allowedPrefixes.some(prefix => normalizedCode.startsWith(prefix));
    const hasValidSuffix = allowedSuffixes.length === 0
        || allowedSuffixes.some(suffix => normalizedCode.endsWith(suffix));

    if (hasValidPrefix && hasValidSuffix) {
        return { isValid: true };
    }

    return {
        isValid: false,
        message: 'Lectura alterada. Reescanea la pieza.'
    };
}

function addRecentScan(scan) {
    if (!scan) {
        loadRecentScans();
        return;
    }

    const emptyState = recentScansList.querySelector('.recent-scans-empty');

    if (emptyState) {
        recentScansList.innerHTML = '';
    }

    recentScansList.insertAdjacentHTML('afterbegin', getRecentScanMarkup(scan));

    const items = recentScansList.querySelectorAll('.recent-scan-item');

    items.forEach((item, index) => {
        if (index >= 5) {
            item.remove();
        }
    });
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
    } catch (error) {
        recentScansList.innerHTML = '<div class="recent-scans-empty">No se pudieron cargar los escaneos</div>';
    }
}

function getRecentScanMarkup(scan) {
    const shortName = escapeHtml(getShortName(scan.fullName));
    const scannedAt = formatScanTime(scan.scannedAt);
    const quantity = Number(scan.quantity);
    const piecesText = Number.isFinite(quantity) && quantity > 0
        ? `${quantity} ${quantity === 1 ? 'Pieza' : 'Piezas'}`
        : '';

    return `
        <div class="recent-scan-item">
            <div class="recent-scan-main">
                <span class="recent-scan-name">${shortName}</span>
                ${piecesText ? `<span class="recent-scan-pieces">${piecesText}</span>` : ''}
            </div>
            <span class="recent-scan-time">${scannedAt}</span>
        </div>
    `;
}

function resetForm() {
    body.classList.remove('success', 'error', 'warning');
    resetInputsForNextEmployee();
}

function resetInputsForNextEmployee() {
    employeeInput.value = '';
    volanteInput.value = '';

    employeeStatus.textContent = '';
    volanteStatus.textContent = '';

    employeeValid = false;
    validatedEmployeeNumber = null;
    employeeInput.disabled = false;
    volanteInput.disabled = true;
    volanteInput.classList.remove('error');

    employeeInput.focus();
}

clearEmployeeInput.addEventListener('click', function () {
    resetForm();
});

clearVolanteInput.addEventListener('click', function () {
    volanteInput.value = '';
    setVolanteStatus('', '');

    if (employeeValid) {
        volanteInput.focus();
    } else {
        employeeInput.focus();
    }
});

function setEmployeeStatus(message, className) {
    employeeStatus.textContent = message;
    employeeStatus.className = className ? `status-text ${className}` : 'status-text';
}

function setVolanteStatus(message, className) {
    volanteStatus.textContent = message;
    volanteStatus.className = className ? `status-text ${className}` : 'status-text';
}

function formatScanTime(value) {
    if (!value) {
        return '';
    }

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return '';
    }

    return date.toLocaleTimeString('es-MX', {
        hour: '2-digit',
        minute: '2-digit'
    });
}

function getShortName(fullName) {
    const parts = (fullName || '')
        .trim()
        .split(/\s+/)
        .filter(Boolean);

    if (parts.length === 0) {
        return 'Sin nombre';
    }

    if (parts.length === 1) {
        return parts[0];
    }

    return `${parts[0]} ${parts[1]}`;
}

function escapeHtml(value) {
    return value
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}


const scansModal = document.getElementById('scansModal');
const openScansModal = document.getElementById('openScansModal');
const closeScansModal = document.getElementById('closeScansModal');
const modalScansContent = document.getElementById('modalScansContent');

closeScansModal.addEventListener('click', function () {
    scansModal.classList.remove('active');
    employeeInput.focus();
});

scansModal.addEventListener('click', function (e) {
    if (e.target === scansModal) {
        scansModal.classList.remove('active');
        employeeInput.focus();
    }
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

        modalScansContent.innerHTML = result.scans.map(scan => {
            return `
                <div class="modal-scan-item">
                    <div>
                        <div class="modal-scan-name">${escapeHtml(scan.fullName)}</div>
                        <div class="modal-scan-operation">
                            ${escapeHtml(scan.operation || 'Sin operacion')} · ${escapeHtml(String(scan.employeeNumber || ''))}
                        </div>
                    </div>

                    <div class="modal-scan-right">
                        <div class="modal-scan-pieces">${Number(scan.quantity) === 1 ? '1 Pieza' : `${Number(scan.quantity)} Piezas`}</div>
                        <div class="modal-scan-time">${formatScanTime(scan.lastScannedAt)}</div>
                    </div>
                </div>
            `;
        }).join('');

    } catch (error) {
        modalScansContent.innerHTML = '<div class="recent-scans-empty">Error al cargar</div>';
    }
}
const modalSearchInput = document.getElementById('modalSearchInput');

modalSearchInput.addEventListener('input', function () {
    filterModalScans(this.value);
});

function filterModalScans(search) {
    const text = search.toLowerCase().trim();

    const items = modalScansContent.querySelectorAll('.modal-scan-item');

    items.forEach(item => {
        const content = item.textContent.toLowerCase();

        if (content.includes(text)) {
            item.style.display = '';
        } else {
            item.style.display = 'none';
        }
    });
}
openScansModal.addEventListener('click', async function () {
    scansModal.classList.add('active');

    modalSearchInput.value = '';

    await loadModalScans();
});

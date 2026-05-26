// ========================================
// LOGICA DEL MODAL DE RECHAZO
// ========================================

const rejectState = {
    scannedPiece: null,
    scannedDefects: [],
    availableDefects: [],
    filteredDefects: [],
    defectsLoaded: false
};

function setRejectPieceInputState(status) {
    const pieceInput = document.getElementById('rejectPieceInput');
    if (!pieceInput) {
        return;
    }

    pieceInput.classList.remove('input-valid', 'input-error');

    if (status === 'valid') {
        pieceInput.classList.add('input-valid');
    } else if (status === 'error') {
        pieceInput.classList.add('input-error');
    }
}

function getRecentDefectsStorageKey() {
    return `scannerProduction.recentDefects.area.${state.areaId || 'default'}`;
}

function getRecentDefectIds() {
    try {
        const rawValue = localStorage.getItem(getRecentDefectsStorageKey());
        const parsedValue = JSON.parse(rawValue || '[]');
        return Array.isArray(parsedValue) ? parsedValue.map(Number).filter(Number.isFinite) : [];
    } catch {
        return [];
    }
}

function saveRecentDefectIds(defectIds) {
    try {
        localStorage.setItem(getRecentDefectsStorageKey(), JSON.stringify(defectIds.slice(0, 15)));
    } catch {
        // Ignorar errores de almacenamiento local.
    }
}

function registerRecentDefects(defects) {
    const currentRecentIds = getRecentDefectIds();
    const newRecentIds = (Array.isArray(defects) ? defects : [])
        .map(defect => Number(defect?.defectId))
        .filter(Number.isFinite);

    if (newRecentIds.length === 0) {
        return;
    }

    const mergedIds = [...newRecentIds.reverse(), ...currentRecentIds].filter((defectId, index, array) =>
        array.indexOf(defectId) === index
    );

    saveRecentDefectIds(mergedIds);
}

function sortDefectsByRecent(defects) {
    const recentIds = getRecentDefectIds();
    const recentPositionMap = new Map(recentIds.map((defectId, index) => [defectId, index]));

    return [...defects].sort((left, right) => {
        const leftPosition = recentPositionMap.has(left.defectId) ? recentPositionMap.get(left.defectId) : Number.MAX_SAFE_INTEGER;
        const rightPosition = recentPositionMap.has(right.defectId) ? recentPositionMap.get(right.defectId) : Number.MAX_SAFE_INTEGER;

        if (leftPosition !== rightPosition) {
            return leftPosition - rightPosition;
        }

        return String(left.defectName || '').localeCompare(String(right.defectName || ''), 'es-MX');
    });
}

function processRejectCommand(rawCode) {
    const code = (rawCode || '').trim().toUpperCase();
    if (!code) return false;

    const cancelCommands = new Set(['CANCELDEFECT', 'CANCELDFEFECT']);
    const confirmCommands = new Set(['CONFIRMDEFECT', 'CONFIRDEFECT']);
    const deleteCommands = new Set(['DELETEDEFECT', 'DEJELEDEFECT', 'DELETELEDEFECT']);

    if (cancelCommands.has(code)) {
        closeRejectModal();
        return true;
    }

    if (confirmCommands.has(code)) {
        confirmReject();
        return true;
    }

    if (deleteCommands.has(code)) {
        if (rejectState.scannedDefects.length > 0) {
            removeDefect(rejectState.scannedDefects.length - 1);
        } else {
            alert('No hay defectos para eliminar');
        }
        return true;
    }

    return false;
}

function resetRejectFlow() {
    rejectState.scannedPiece = null;
    rejectState.scannedDefects = [];

    const pieceInput = document.getElementById('rejectPieceInput');
    const defectInput = document.getElementById('rejectDefectInput');
    const searchInput = document.getElementById('rejectDefectSearch');
    const emptyState = document.getElementById('availableDefectsEmpty');
    const pieceStatus = document.getElementById('rejectPieceStatus');
    const defectsList = document.getElementById('defectsList');
    const confirmButton = document.getElementById('confirmRejectBtn');

    if (pieceInput) {
        pieceInput.value = '';
        setRejectPieceInputState(null);
    }

    if (defectInput) {
        defectInput.value = '';
        defectInput.disabled = true;
    }

    // Preserve the employee number after a successful rejection registration.
    // This keeps the employee ID in the input for subsequent defects.
    if (searchInput) {
        searchInput.value = '';
    }

    if (emptyState) {
        emptyState.classList.add('hidden');
    }

    if (pieceStatus) {
        pieceStatus.classList.add('hidden');
    }

    if (defectsList) {
        defectsList.innerHTML = '';
        defectsList.classList.add('hidden');
    }

    if (confirmButton) {
        confirmButton.disabled = true;
    }

    filterAvailableDefects('');
    updateRejectDefectsCount();
    if (pieceInput) {
        pieceInput.focus();
    }
}

async function openRejectModal(employeeNumber = '') {
    rejectState.scannedPiece = null;
    rejectState.scannedDefects = [];

    document.getElementById('rejectEmployeeInput').value = employeeNumber;
    document.getElementById('rejectPieceInput').value = '';
    setRejectPieceInputState(null);
    document.getElementById('rejectDefectInput').value = '';
    document.getElementById('rejectDefectInput').disabled = true;
    document.getElementById('rejectDefectSearch').value = '';

    document.getElementById('rejectPieceStatus').classList.add('hidden');
    document.getElementById('defectsList').classList.add('hidden');
    document.getElementById('defectsList').innerHTML = '';
    document.getElementById('confirmRejectBtn').disabled = true;
    const rejectModal = document.getElementById('rejectModal');
    if (rejectModal) {
        rejectModal.classList.remove('hidden');
    }

    await ensureAvailableDefectsLoaded();
    filterAvailableDefects('');
    updateRejectDefectsCount();

    setTimeout(() => {
        const targetInputId = employeeNumber ? 'rejectPieceInput' : 'rejectEmployeeInput';
        document.getElementById(targetInputId).focus();
    }, 100);
}

function closeRejectModal() {
    if (state.productionLineId) {
        window.location.href = `/ScannerProduction/Index?productionLineId=${encodeURIComponent(state.productionLineId)}`;
        return;
    }

    const rejectModal = document.getElementById('rejectModal');
    if (rejectModal) {
        rejectModal.classList.add('hidden');
    }

    const scanInput = document.getElementById('scanInput');
    if (scanInput) {
        scanInput.focus();
    }
}

async function ensureAvailableDefectsLoaded(forceReload = false) {
    if (!forceReload && rejectState.defectsLoaded) {
        return;
    }

    if (!state.areaId) {
        renderAvailableDefects([]);
        return;
    }

    try {
        const response = await fetch(`${CONFIG.API.GET_DEFECTS_BY_AREA}?areaId=${state.areaId}`);
        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Error al obtener los defectos');
        }

        const defects = await response.json();
        rejectState.availableDefects = sortDefectsByRecent(Array.isArray(defects) ? defects : []);
        rejectState.defectsLoaded = true;
    } catch (error) {
        console.error('Error al cargar defectos:', error);
        rejectState.availableDefects = [];
        rejectState.defectsLoaded = false;
        renderAvailableDefects([]);
        alert('No se pudieron cargar los defectos del área: ' + error.message);
    }
}

function filterAvailableDefects(searchTerm = '') {
    const normalizedSearch = String(searchTerm || '').trim().toLowerCase();

    const filteredDefects = rejectState.availableDefects.filter(defect => {
        if (!normalizedSearch) return true;

        const defectId = String(defect.defectId || '').toLowerCase();
        const defectName = String(defect.defectName || '').toLowerCase();
        const category = String(defect.category || '').toLowerCase();

        return defectId.includes(normalizedSearch)
            || defectName.includes(normalizedSearch)
            || category.includes(normalizedSearch);
    });

    rejectState.filteredDefects = sortDefectsByRecent(filteredDefects);

    renderAvailableDefects(rejectState.filteredDefects);
}

function renderAvailableDefects(defects) {
    const container = document.getElementById('availableDefectsList');
    const emptyState = document.getElementById('availableDefectsEmpty');

    if (!container || !emptyState) return;

    container.innerHTML = '';

    if (!Array.isArray(defects) || defects.length === 0) {
        emptyState.classList.remove('hidden');
        return;
    }

    emptyState.classList.add('hidden');

    defects.forEach(defect => {
        const item = document.createElement('button');
        item.type = 'button';
        item.className = 'available-defect-item';

        const isSelected = rejectState.scannedDefects.some(d => d.defectId === defect.defectId);
        if (isSelected) {
            item.classList.add('selected');
        }

        const info = document.createElement('div');
        info.className = 'available-defect-item-info';

        const title = document.createElement('strong');
        title.textContent = defect.defectName;

        const meta = document.createElement('span');
        meta.textContent = defect.category
            ? `Codigo ${defect.defectId} | ${defect.category}`
            : `Codigo ${defect.defectId}`;

        info.appendChild(title);
        info.appendChild(meta);

        const action = document.createElement('span');
        action.className = 'available-defect-item-action';
        action.textContent = isSelected ? 'Agregado' : 'Agregar';

        item.appendChild(info);
        item.appendChild(action);
        item.addEventListener('click', () => addDefectSelection(defect));

        container.appendChild(item);
    });
}

function addDefectSelection(defectInfo) {
    if (!defectInfo || !defectInfo.defectId) {
        return;
    }

    const isDuplicate = rejectState.scannedDefects.some(d => d.defectId === defectInfo.defectId);
    if (isDuplicate) {
        alert('Este defecto ya fue agregado para esta pieza');
        return;
    }

    rejectState.scannedDefects.push({
        defectId: defectInfo.defectId,
        defectName: defectInfo.defectName,
        code: String(defectInfo.defectId),
        source: 'manual',
        category: defectInfo.category || null
    });

    registerRecentDefects([defectInfo]);
    rejectState.availableDefects = sortDefectsByRecent(rejectState.availableDefects);
    updateDefectsList();
    filterAvailableDefects(document.getElementById('rejectDefectSearch')?.value || '');
    updateRejectDefectsCount();
    document.getElementById('confirmRejectBtn').disabled = false;
}

async function handleRejectPieceScan() {
    const input = document.getElementById('rejectPieceInput');
    const code = input.value.trim();

    if (!code) return;
    if (processRejectCommand(code)) {
        input.value = '';
        return;
    }

    const statusDiv = document.getElementById('rejectPieceStatus');

    if (typeof isZfCustomer === 'function'
        && isZfCustomer()
        && typeof extractZfPartNumberFromScan === 'function'
        && !extractZfPartNumberFromScan(code)) {
        setRejectPieceInputState('error');
        statusDiv.classList.add('hidden');
        input.value = '';
        return;
    }

    try {
        const validationResult = await validateScannerValue(code);

        rejectState.scannedPiece = {
            code: code,
            scanDetails: validationResult.scanDetails || null,
            wasPreviouslyScanned: !validationResult.isValid
        };

        if (rejectState.scannedPiece.wasPreviouslyScanned) {
            setRejectPieceInputState('valid');
        } else {
            setRejectPieceInputState('valid');
        }
        statusDiv.classList.add('hidden');

        document.getElementById('rejectDefectInput').disabled = false;
        document.getElementById('rejectDefectInput').focus();
    } catch (error) {
        console.error('Error al validar la pieza:', error);
        setRejectPieceInputState('error');
        statusDiv.classList.add('hidden');
        input.value = '';
    }
}

async function handleRejectDefectScan() {
    const input = document.getElementById('rejectDefectInput');
    const code = input.value.trim();

    if (!code) return;
    if (processRejectCommand(code)) {
        input.value = '';
        return;
    }

    try {
        const defectInfo = await validateDefectCode(code);

        if (!defectInfo.isValid) {
            alert(`Error: ${defectInfo.message || 'Codigo de defecto no valido'}`);
            input.value = '';
            return;
        }

        addDefectSelection({
            defectId: defectInfo.defectId,
            defectName: defectInfo.defectName,
            category: defectInfo.category || null
        });

        input.value = '';
    } catch (error) {
        console.error('Error al validar el defecto:', error);
        alert('Error al validar el codigo de defecto: ' + error.message);
        input.value = '';
    }
}

async function validateDefectCode(code) {
    const response = await fetch(`${CONFIG.API.VALIDATE_DEFECT_CODE}?code=${encodeURIComponent(code)}&areaId=${state.areaId || 1}`);

    if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Error al validar el codigo de defecto');
    }

    return await response.json();
}

function updateDefectsList() {
    const defectsList = document.getElementById('defectsList');

    if (rejectState.scannedDefects.length === 0) {
        defectsList.classList.add('hidden');
        renderAvailableDefects(rejectState.filteredDefects);
        updateRejectDefectsCount();
        return;
    }

    defectsList.classList.remove('hidden');
    defectsList.innerHTML = '';

    rejectState.scannedDefects.forEach((defect, index) => {
        const defectItem = document.createElement('div');
        defectItem.className = 'defect-item';

        const info = document.createElement('div');
        info.className = 'defect-item-info';

        const text = document.createElement('div');
        text.className = 'defect-item-text';

        const title = document.createElement('strong');
        title.textContent = defect.defectName;

        const meta = document.createElement('span');
        meta.textContent = `Codigo: ${defect.code}`;

        text.appendChild(title);
        text.appendChild(meta);
        info.appendChild(text);

        const removeButton = document.createElement('button');
        removeButton.className = 'defect-item-remove';
        removeButton.type = 'button';
        removeButton.title = 'Eliminar defecto';
        removeButton.textContent = 'Quitar';
        removeButton.addEventListener('click', () => removeDefect(index));

        defectItem.appendChild(info);
        defectItem.appendChild(removeButton);
        defectsList.appendChild(defectItem);
    });

    renderAvailableDefects(rejectState.filteredDefects);
    updateRejectDefectsCount();
}

function removeDefect(index) {
    rejectState.scannedDefects.splice(index, 1);
    updateDefectsList();

    if (rejectState.scannedDefects.length === 0) {
        document.getElementById('confirmRejectBtn').disabled = true;
    }
}

function updateRejectDefectsCount() {
    const counter = document.getElementById('rejectDefectsCount');
    if (!counter) return;

    const count = rejectState.scannedDefects.length;
    counter.textContent = `${count} ${count === 1 ? 'seleccionado' : 'seleccionados'}`;
}

async function confirmReject() {
    const employeeInput = document.getElementById('rejectEmployeeInput');
    const employeeNumber = employeeInput ? employeeInput.value.trim() : '';

    if (!employeeNumber) {
        alert('Por favor, capture el numero de empleado');
        if (employeeInput) employeeInput.focus();
        return;
    }

    if (!rejectState.scannedPiece) {
        alert('Por favor, escanee la pieza rechazada');
        return;
    }

    if (rejectState.scannedDefects.length === 0) {
        alert('Por favor, agregue al menos un defecto');
        return;
    }

    let currentPartData = state.currentPartData || {};

    if (typeof isZfCustomer === 'function'
        && isZfCustomer()
        && rejectState.scannedPiece
        && rejectState.scannedPiece.code
        && typeof resolvePartDataFromZfScan === 'function') {
        const resolvedPartData = await resolvePartDataFromZfScan(rejectState.scannedPiece.code);
        if (resolvedPartData) {
            currentPartData = resolvedPartData;
            state.currentPartData = resolvedPartData;
            state.currentPartNumber = resolvedPartData.partnumber || null;

            if (typeof updatePartNumberInfo === 'function') {
                updatePartNumberInfo([resolvedPartData]);
            }
        } else {
            alert('No se pudo resolver NP/programa para el rechazo ZF');
            return;
        }
    }

    const hasProgramId = currentPartData.programId || currentPartData.id;
    const programId = hasProgramId ? (currentPartData.programId || currentPartData.id) : null;

    try {
        const response = await fetch(CONFIG.API.SAVE_REJECTION, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                lineId: state.productionLineId,
                employeeNumber: employeeNumber,
                scannerValue: rejectState.scannedPiece.code,
                programId: programId,
                programDescription: currentPartData.program || null,
                defects: rejectState.scannedDefects.map(d => ({
                    defectId: d.defectId,
                    defectName: d.defectName
                }))
            })
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Error al guardar el rechazo');
        }

        const result = await response.json();
        registerRecentDefects(rejectState.scannedDefects);
        await loadMetricsFromAPI();
        resetRejectFlow();

        console.log('Rechazo registrado exitosamente:', result);
    } catch (error) {
        console.error('Error al guardar el rechazo:', error);
        alert('Error al guardar el rechazo: ' + error.message);
    }
}

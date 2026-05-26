// ========================================
// LÓGICA DEL MODAL DE HISTORIAL DE RECHAZOS
// ========================================

// Estado del modal de historial
const historyState = {
    rejections: [],
    currentEditingDefect: null
};

/**
 * Abrir el modal de historial de rechazos
 */
async function openHistoryModal() {
    // Validar que se haya seleccionado una línea de producción
    if (!state.productionLineId) {
        alert('Error: No se pudo identificar la línea de producción');
        return;
    }

    // Mostrar modal
    document.getElementById('historyModal').classList.remove('hidden');

    // Cargar historial
    await loadRejectionsHistory();
}

/**
 * Cerrar el modal de historial
 */
function closeHistoryModal() {
    document.getElementById('historyModal').classList.add('hidden');
    // Devolver el foco al input principal
    document.getElementById('scanInput').focus();
}

/**
 * Cargar historial de rechazos desde el API
 */
async function loadRejectionsHistory() {
    const loadingDiv = document.getElementById('historyLoading');
    const dataDiv = document.getElementById('historyData');
    const emptyDiv = document.getElementById('historyEmpty');

    // Mostrar loading
    loadingDiv.style.display = 'block';
    dataDiv.style.display = 'none';

    try {
        const response = await fetch(
            `${CONFIG.API.GET_REJECTIONS_HISTORY}?lineId=${state.productionLineId}`
        );

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Error al cargar el historial');
        }

        const result = await response.json();
        historyState.rejections = result.rejections || [];

        // Ocultar loading
        loadingDiv.style.display = 'none';
        dataDiv.style.display = 'block';

        // Actualizar contador
        document.getElementById('totalRejectedPieces').textContent = result.totalRejectedPieces || 0;

        // Renderizar tabla
        renderHistoryTable();

        // Mostrar mensaje si no hay datos
        if (historyState.rejections.length === 0) {
            emptyDiv.style.display = 'block';
            document.querySelector('#historyData > div:first-child').style.display = 'none';
            document.querySelector('#historyData > div:nth-child(2)').style.display = 'none';
        } else {
            emptyDiv.style.display = 'none';
            document.querySelector('#historyData > div:first-child').style.display = 'block';
            document.querySelector('#historyData > div:nth-child(2)').style.display = 'block';
        }

    } catch (error) {
        console.error('Error al cargar historial:', error);
        loadingDiv.style.display = 'none';
        alert('Error al cargar el historial: ' + error.message);
    }
}

/**
 * Renderizar la tabla de historial
 */
function renderHistoryTable() {
    const tbody = document.getElementById('historyTableBody');
    tbody.innerHTML = '';

    historyState.rejections.forEach(rejection => {
        const row = document.createElement('tr');
        row.style.borderBottom = '1px solid #e5e7eb';

        // Columna: Código de pieza
        const codeCell = document.createElement('td');
        codeCell.style.padding = '0.75rem';
        codeCell.innerHTML = `<strong style="color: #1f2937;">${rejection.scannerValue}</strong>`;
        row.appendChild(codeCell);

        // Columna: Defectos (lista)
        const defectsCell = document.createElement('td');
        defectsCell.style.padding = '0.75rem';
        defectsCell.style.maxWidth = '300px';

        const defectsList = document.createElement('div');
        defectsList.style.display = 'flex';
        defectsList.style.flexDirection = 'column';
        defectsList.style.gap = '0.25rem';

        rejection.defects.forEach(defect => {
            const defectItem = document.createElement('div');
            defectItem.style.fontSize = '0.875rem';
            defectItem.style.color = '#6b7280';
            defectItem.innerHTML = `• ${defect.defectName} <span style="color: #9ca3af;">(${defect.category})</span>`;
            defectsList.appendChild(defectItem);
        });

        defectsCell.appendChild(defectsList);
        row.appendChild(defectsCell);

        // Columna: Hora
        const timeCell = document.createElement('td');
        timeCell.style.padding = '0.75rem';
        timeCell.style.fontSize = '0.875rem';
        timeCell.style.color = '#6b7280';
        const rejectionDate = new Date(rejection.firstRejectionTime);
        timeCell.textContent = rejectionDate.toLocaleTimeString('es-MX', {
            hour: '2-digit',
            minute: '2-digit'
        });
        row.appendChild(timeCell);

        // Columna: Acciones
        const actionsCell = document.createElement('td');
        actionsCell.style.padding = '0.75rem';
        actionsCell.style.textAlign = 'center';

        const actionsDiv = document.createElement('div');
        actionsDiv.style.display = 'flex';
        actionsDiv.style.gap = '0.5rem';
        actionsDiv.style.justifyContent = 'center';
        actionsDiv.style.flexWrap = 'wrap';

        // Botones de editar y eliminar para cada defecto
        rejection.defects.forEach(defect => {
            const defectActions = document.createElement('div');
            defectActions.style.display = 'flex';
            defectActions.style.gap = '0.5rem';
            defectActions.style.alignItems = 'center';
            defectActions.style.padding = '0.5rem';
            defectActions.style.backgroundColor = '#f9fafb';
            defectActions.style.borderRadius = '0.5rem';
            defectActions.style.fontSize = '0.875rem';
            defectActions.style.border = '1px solid #e5e7eb';
            defectActions.style.marginBottom = '0.5rem';

            const defectLabel = document.createElement('span');
            defectLabel.style.color = '#374151';
            defectLabel.style.fontWeight = '500';
            defectLabel.style.flex = '1';
            defectLabel.textContent = defect.defectName;
            defectActions.appendChild(defectLabel);

            // Botón editar - MÁS GRANDE PARA TOUCH
            const editBtn = document.createElement('button');
            editBtn.className = 'history-action-btn history-edit-btn';
            editBtn.innerHTML = `
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width: 1.5rem; height: 1.5rem;">
                    <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7" />
                    <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" />
                </svg>
            `;
            editBtn.title = 'Editar defecto';
            editBtn.onclick = () => openEditDefectModal(defect, rejection.scannerValue);
            defectActions.appendChild(editBtn);

            // Botón eliminar - MÁS GRANDE PARA TOUCH
            const deleteBtn = document.createElement('button');
            deleteBtn.className = 'history-action-btn history-delete-btn';
            deleteBtn.innerHTML = `
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width: 1.5rem; height: 1.5rem;">
                    <polyline points="3 6 5 6 21 6" />
                    <path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />
                </svg>
            `;
            deleteBtn.title = 'Eliminar defecto';
            deleteBtn.onclick = () => deleteDefectFromRejection(defect.defectsDataId, rejection.scannerValue);
            defectActions.appendChild(deleteBtn);

            actionsDiv.appendChild(defectActions);
        });

        actionsCell.appendChild(actionsDiv);
        row.appendChild(actionsCell);

        tbody.appendChild(row);
    });
}

/**
 * Actualizar el historial (recargar datos)
 */
async function refreshHistory() {
    await loadRejectionsHistory();
}

/**
 * Abrir modal para editar un defecto
 */
function openEditDefectModal(defect, scannerValue) {
    historyState.currentEditingDefect = defect;

    // Actualizar UI del modal
    document.getElementById('editDefectScannerValue').textContent = scannerValue;
    document.getElementById('editDefectCurrentName').textContent = defect.defectName;
    document.getElementById('editDefectInput').value = '';
    document.getElementById('confirmEditBtn').disabled = true;

    // Mostrar modal
    document.getElementById('editDefectModal').classList.remove('hidden');

    // Focus en el input
    setTimeout(() => {
        document.getElementById('editDefectInput').focus();
    }, 100);
}

/**
 * Cerrar modal de edición de defecto
 */
function closeEditDefectModal() {
    document.getElementById('editDefectModal').classList.add('hidden');
    historyState.currentEditingDefect = null;
}

/**
 * Manejar el escaneo de código de defecto en modal de edición
 */
document.addEventListener('DOMContentLoaded', function() {
    const editDefectInput = document.getElementById('editDefectInput');
    if (editDefectInput) {
        editDefectInput.addEventListener('keypress', async function(e) {
            if (e.key === 'Enter') {
                const code = this.value.trim();
                if (!code) return;

                try {
                    // Validar el código de defecto
                    const defectInfo = await validateDefectCode(code);

                    if (!defectInfo.isValid) {
                        alert(`Error: ${defectInfo.message || 'Código de defecto no válido'}`);
                        this.value = '';
                        return;
                    }

                    // Verificar que no sea el mismo defecto
                    if (defectInfo.defectId === historyState.currentEditingDefect.defectId) {
                        alert('Este es el mismo defecto. Por favor, escanee un defecto diferente.');
                        this.value = '';
                        return;
                    }

                    // Guardar el nuevo defecto y habilitar botón de confirmar
                    historyState.currentEditingDefect.newDefectId = defectInfo.defectId;
                    historyState.currentEditingDefect.newDefectName = defectInfo.defectName;
                    document.getElementById('confirmEditBtn').disabled = false;

                    // Actualizar el label para mostrar el nuevo defecto
                    document.getElementById('editDefectCurrentName').textContent =
                        `${historyState.currentEditingDefect.defectName} → ${defectInfo.defectName}`;

                } catch (error) {
                    console.error('Error al validar el defecto:', error);
                    alert('Error al validar el código de defecto: ' + error.message);
                    this.value = '';
                }
            }
        });
    }
});

/**
 * Confirmar la edición del defecto
 */
async function confirmEditDefect() {
    if (!historyState.currentEditingDefect || !historyState.currentEditingDefect.newDefectId) {
        alert('Por favor, escanee un código de defecto válido');
        return;
    }

    try {
        const response = await fetch(CONFIG.API.UPDATE_DEFECT_IN_REJECTION, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                defectsDataId: historyState.currentEditingDefect.defectsDataId,
                newDefectId: historyState.currentEditingDefect.newDefectId
            })
        });

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Error al actualizar el defecto');
        }

        const result = await response.json();
        console.log('Defecto actualizado exitosamente:', result);

        // Cerrar modal de edición
        closeEditDefectModal();

        // Recargar historial
        await loadRejectionsHistory();

        // Recargar métricas
        await loadMetricsFromAPI();

    } catch (error) {
        console.error('Error al actualizar el defecto:', error);
        alert('Error al actualizar el defecto: ' + error.message);
    }
}

/**
 * Eliminar un defecto de un rechazo
 */
async function deleteDefectFromRejection(defectsDataId, scannerValue) {
    const confirmed = confirm(
        `¿Está seguro de que desea eliminar este defecto de la pieza ${scannerValue}?\n\n` +
        `Esta acción no se puede deshacer.`
    );

    if (!confirmed) return;

    try {
        const response = await fetch(
            `${CONFIG.API.DELETE_DEFECT_FROM_REJECTION}?defectsDataId=${defectsDataId}`,
            {
                method: 'DELETE'
            }
        );

        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Error al eliminar el defecto');
        }

        const result = await response.json();
        console.log('Defecto eliminado exitosamente:', result);

        // Recargar historial
        await loadRejectionsHistory();

        // Recargar métricas
        await loadMetricsFromAPI();

    } catch (error) {
        console.error('Error al eliminar el defecto:', error);
        alert('Error al eliminar el defecto: ' + error.message);
    }
}

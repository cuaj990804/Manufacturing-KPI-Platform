let defectCategories = {};
let allDefects = [];
let defectQuantities = {};
let totalVolantes = 0;

// --- Función para mostrar notificación tipo toast ---
function showToast(message, type = 'success') {
    // Tipos: 'success', 'error', 'warning', 'info'
    const toast = $(`
        <div class="custom-toast custom-toast-${type}">
            <span class="toast-icon">${type === 'success' ? '✓' : type === 'error' ? '✗' : type === 'warning' ? '⚠' : 'ℹ'}</span>
            <span class="toast-message">${message}</span>
        </div>
    `);

    // Crear contenedor si no existe
    if ($('#toast-container').length === 0) {
        $('body').append('<div id="toast-container"></div>');
    }

    // Agregar toast
    $('#toast-container').append(toast);

    // Animar entrada
    setTimeout(() => toast.addClass('show'), 10);

    // Auto-ocultar después de 3 segundos
    setTimeout(() => {
        toast.removeClass('show');
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// Función para formatear fecha como string local (sin conversión UTC)
function formatLocalDateTime(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    const seconds = String(date.getSeconds()).padStart(2, '0');

    return `${year}-${month}-${day}T${hours}:${minutes}:${seconds}`;
}

// --- Funciones principales ---
async function fetchCategories() {
    const response = await fetch('/api/DefectsApi/categories');
    const data = await response.json();
    defectCategories = {};
    const categoryFilter = document.getElementById('categoryFilter');
    if (!categoryFilter) return;
    categoryFilter.innerHTML = '<option value="">Todas las categorías</option>';
    data.forEach(cat => {
        defectCategories[cat.id] = { label: cat.label, icon: cat.icon };
        categoryFilter.innerHTML += `<option value="${cat.id}">${cat.icon} ${cat.label}</option>`;
    });
}

async function fetchDefects(lineId) {
    const response = await fetch(`/api/DefectsApi/defects?lineId=${lineId}`);
    const data = await response.json();
    allDefects = data;
    console.log('=== DEBUG FETCH DEFECTS ===');
    console.log('Defectos cargados:', allDefects);
    console.log('===========================');
    renderDefects();
}

function getFilteredDefects() {
    const categoryFilter = document.getElementById('categoryFilter');
    const searchInput = document.getElementById('searchInput');
    let filtered = allDefects;
    const selectedCategory = categoryFilter?.value;
    const searchTerm = searchInput?.value.toLowerCase() || '';
    if (selectedCategory) filtered = filtered.filter(d => d.categoryId === parseInt(selectedCategory));
    if (searchTerm) filtered = filtered.filter(d => d.name.toLowerCase().includes(searchTerm));
    return filtered;
}

function renderDefects() {
    const defectList = document.getElementById('defectList');
    const defectCount = document.getElementById('defectCount');
    if (!defectList || !defectCount) return;

    const filtered = getFilteredDefects();
    defectCount.textContent = `${filtered.length} defectos disponibles`;

    defectList.innerHTML = filtered.map(defect => `
        <div class="defect-item border rounded p-3 mb-2">
            <div class="d-flex justify-content-between align-items-center">
                <div class="flex-grow-1">
                    <div class="d-flex align-items-center mb-1">
                        <span class="me-2 fs-5">${defectCategories[defect.categoryId]?.icon || ''}</span>
                        <span class="fw-bold">${defect.name}</span>
                    </div>
                    <span class="badge bg-light text-dark category-badge">${defectCategories[defect.categoryId]?.label || ''}</span>
                </div>
                <div class="d-flex align-items-center">
                    <label class="form-label me-2 mb-0 small fw-bold">Cantidad:</label>
                    <input type="number" class="form-control quantity-input text-center"
                        data-defect="${defect.name}"
                        value="${defectQuantities[defect.name] || ''}"
                        placeholder="0" min="0">
                </div>
            </div>
        </div>
    `).join('');

    document.querySelectorAll('.quantity-input').forEach(input =>
        input.addEventListener('input', handleQuantityChange)
    );
}

function handleQuantityChange(e) {
    const defectName = e.target.dataset.defect;
    const newQuantity = parseInt(e.target.value) || 0;
    if (newQuantity === 0) delete defectQuantities[defectName];
    else defectQuantities[defectName] = newQuantity;
    updateSummary();
}

function updateSummary() {
    const summaryCard = document.getElementById('summaryCard');
    const summaryStats = document.getElementById('summaryStats');
    const summaryDefects = document.getElementById('summaryDefects');
    const submitBtn = document.getElementById('submitBtn');

    if (!summaryCard || !summaryStats || !summaryDefects || !submitBtn) return;

    const defectsWithQuantity = Object.entries(defectQuantities).filter(([_, q]) => q > 0);
    const totalDefects = defectsWithQuantity.reduce((sum, [_, q]) => sum + q, 0);

    // Validar si hay menos defectos que volantes rechazados
    const isInvalid = totalVolantes > 0 && totalDefects < totalVolantes;

    if (totalVolantes > 0 && defectsWithQuantity.length > 0) {
        summaryCard.classList.remove('d-none');

        // Añadir clase de error si es inválido
        const totalDefectsClass = isInvalid ? 'text-danger fw-bold' : 'fw-bold';
        const warningMessage = isInvalid ? '<p class="text-danger mb-1"><i class="bi bi-exclamation-triangle-fill me-1"></i>La suma de defectos no puede ser menor a los volantes rechazados</p>' : '';

        summaryStats.innerHTML = `
            <p class="mb-1">Total de volantes rechazados: <span class="fw-bold">${totalVolantes}</span></p>
            <p class="mb-1">Total de defectos: <span class="${totalDefectsClass}">${totalDefects}</span></p>
            ${warningMessage}`;
        summaryDefects.innerHTML = defectsWithQuantity.map(([name, qty]) => `
            <div class="d-flex justify-content-between small">
                <span class="text-truncate me-2">${name}</span>
                <span class="fw-bold">${qty}</span>
            </div>`).join('');
    } else {
        summaryCard.classList.add('d-none');
        summaryStats.innerHTML = '';
        summaryDefects.innerHTML = '';
    }

    // Deshabilitar botón si no hay datos o si la suma de defectos es menor a volantes
    submitBtn.disabled = !totalVolantes || defectsWithQuantity.length === 0 || isInvalid;

    // Determinar el texto del botón basado en si hay registros existentes
    const isUpdate = submitBtn.innerHTML.includes('Actualizar');
    const actionText = isUpdate ? 'Actualizar' : 'Registrar';

    submitBtn.innerHTML = `<i class="bi bi-check-circle me-2"></i> ${actionText} Defectos (${defectsWithQuantity.length} defectos)`;
}

function resetForm() {
    defectQuantities = {};
    totalVolantes = 0;
    const totalVolantesInput = document.getElementById('totalVolantes');
    const searchInput = document.getElementById('searchInput');
    const categoryFilter = document.getElementById('categoryFilter');
    if (totalVolantesInput) totalVolantesInput.value = '';
    if (searchInput) searchInput.value = '';
    if (categoryFilter) categoryFilter.value = '';
    renderDefects();
    updateSummary();
}

async function handleSubmit() {
    const defectsWithQuantity = Object.entries(defectQuantities).filter(([_, quantity]) => quantity > 0);

    // Validación: La suma de defectos debe ser igual o mayor a la cantidad de volantes rechazados
    const totalDefects = defectsWithQuantity.reduce((sum, [_, q]) => sum + q, 0);

    if (totalDefects < totalVolantes) {
        showToast(`La suma de defectos (${totalDefects}) no puede ser menor a la cantidad de volantes rechazados (${totalVolantes})`, "error");
        return;
    }

    const defectos = defectsWithQuantity.map(([defectName, quantity]) => {
        const defect = allDefects.find(d => d.name === defectName);
        console.log('=== DEBUG DEFECT ===');
        console.log('defectName:', defectName);
        console.log('defect object:', defect);
        console.log('defect.id:', defect?.id);
        console.log('==================');
        return { defectId: defect?.id, quantity };
    });

    // MODIFICACIÓN: Obtener el programId
    const programId = parseInt(document.getElementById("programSelect")?.value || 0);

    if (!programId || programId === 0) {
        showToast("Por favor seleccione un programa válido", "warning");
        return;
    }

    // Obtener la descripción del programa seleccionado
    const programSelect = document.getElementById("programSelect");
    const programDescription = programSelect?.options[programSelect.selectedIndex]?.text;

    // Debug para verificar qué se está capturando
    console.log("=== DEBUG PROGRAM INFO ===");
    console.log("Program ID:", programId);
    console.log("Program Select Element:", programSelect);
    console.log("Selected Index:", programSelect?.selectedIndex);
    console.log("Program Description capturada:", programDescription);
    console.log("Todas las opciones:", programSelect ? Array.from(programSelect.options).map(opt => ({value: opt.value, text: opt.text})) : "No select found");
    console.log("===========================");

    const formData = {
        productionLineId: parseInt(document.getElementById("lineProduction")?.value || 0),
        programId: programId, // NUEVO CAMPO AGREGADO
        programDescription: programDescription && programDescription !== "Elige un programa" && programDescription.trim() !== "" ? programDescription : null,
        timeInterval: document.getElementById("timeInterval")?.value || '',
        totalVolantes,
        defects: defectos
    };

    console.log('FormData a enviar:', formData); // Debug adicional

    // Determinar si es actualización o registro nuevo basado en el texto del botón
    const submitBtn = document.getElementById('submitBtn');
    const isUpdate = submitBtn && submitBtn.innerHTML.includes('Actualizar');

    const endpoint = isUpdate ? "/api/DefectsApi/update" : "/api/DefectsApi/register";
    const method = isUpdate ? "PUT" : "POST";
    const successMessage = isUpdate ? "Datos actualizados correctamente." : "Datos guardados correctamente.";

    console.log(`=== SUBMIT ACTION ===`);
    console.log(`Endpoint: ${endpoint}`);
    console.log(`Method: ${method}`);
    console.log(`Is Update: ${isUpdate}`);
    console.log(`ProgramId: ${programId}`); // Debug adicional
    console.log(`====================`);

    try {
        const response = await fetch(endpoint, {
            method: method,
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(formData)
        });

        if (response.ok) {
            const responseText = await response.text();
            showAlert("success", "¡Éxito!", successMessage);

            // Actualizar las palomitas en el select de intervalos
            if (typeof updateDefectIntervalCheckmarks === 'function') {
                await updateDefectIntervalCheckmarks();
            }

            // Recargar los datos para mostrar el estado actualizado
            const lineId = parseInt(document.getElementById("lineProduction")?.value || 0);
            const { startTime, endTime } = getTimeRange();
            if (lineId && programId && startTime && endTime) {
                await loadDefectsData(lineId, startTime, endTime, programId);
            }
        } else {
            const errorText = await response.text();
            console.error('Error del servidor:', errorText);
            alert(`Error al ${isUpdate ? 'actualizar' : 'registrar'}: ` + errorText);
        }
    } catch (error) {
        console.error("Error en fetch:", error);
        alert("Error inesperado: " + error.message);
    }
}

function getTimeRange() {
    const intervalSelect = document.getElementById('timeInterval');
    const selectedInterval = intervalSelect?.value;
    if (!selectedInterval) return { startTime: null, endTime: null };

    const [start, end] = selectedInterval.split('-');
    const today = new Date();

    // Función helper para crear fecha con hora específica
    const createDateTime = (timeStr) => {
        const [hours, minutes] = timeStr.split(':').map(Number);
        return new Date(today.getFullYear(), today.getMonth(), today.getDate(), hours, minutes || 0);
    };

    const startTime = createDateTime(start);
    const endTime = createDateTime(end);

    // Debug logs
    console.log('=== DEBUG TIEMPO ===');
    console.log('Intervalo seleccionado:', selectedInterval);
    console.log('Start local:', startTime);
    console.log('End local:', endTime);
    console.log('Start formateado:', formatLocalDateTime(startTime));
    console.log('End formateado:', formatLocalDateTime(endTime));
    console.log('===================');

    return { startTime, endTime };
}

// FUNCIÓN CORREGIDA: fillFormData
function fillFormData(apiResponse) {
    console.log('=== FILL FORM DATA DEBUG ===');
    console.log('API Response completa:', apiResponse);
    console.log('Has Records:', apiResponse.hasRecords);
    console.log('Total Volantes:', apiResponse.totalVolantes);
    console.log('Defects Data:', apiResponse.defectsData);
    console.log('ProgramId:', apiResponse.programId); // Debug adicional
    console.log('ProgramDescription:', apiResponse.programDescription); // Debug adicional
    console.log('=============================');

    if (!apiResponse.hasRecords || !apiResponse.defectsData || !Array.isArray(apiResponse.defectsData)) {
        console.log('No hay datos para llenar el formulario');
        return;
    }

    // Limpiar datos previos
    defectQuantities = {};

    // Establecer el total de volantes rechazados desde la respuesta del API
    totalVolantes = apiResponse.totalVolantes || 0;
    const totalVolantesInput = document.getElementById('totalVolantes');
    if (totalVolantesInput) {
        totalVolantesInput.value = totalVolantes;
        console.log('Total volantes establecido en input:', totalVolantes);
    }
    const programSelect = document.getElementById("programSelect");
    if (programSelect) {
        programSelect.value = apiResponse.programId;
        console.log('✅ Program select actualizado:', apiResponse.programId);
    }

    // CORRECCIÓN AQUÍ: Usar los nombres correctos que ahora devuelve el API
    apiResponse.defectsData.forEach(item => {
        console.log('=== PROCESANDO ITEM ===');
        console.log('Item completo:', item);
        console.log('defectName:', item.defectName);
        console.log('quantity:', item.quantity);
        console.log('=====================');

        if (item.defectName && item.quantity) {
            defectQuantities[item.defectName] = item.quantity;
            console.log(`✅ Defecto ${item.defectName}: ${item.quantity}`);
        } else {
            console.log('❌ Item inválido - faltan datos');
        }
    });

    console.log('DefectQuantities final:', defectQuantities);

    // Re-renderizar para mostrar los datos
    renderDefects();
    updateSummary();
}

// --- Cargar datos y HTML --- FUNCIÓN MODIFICADA
async function loadDefectsData(lineId, startTime, endTime, programId) {
    try {
        // Validar que programId esté presente


        // Usar el formato local en lugar de toISOString()
        const startTimeStr = formatLocalDateTime(startTime);
        const endTimeStr = formatLocalDateTime(endTime);

        // MODIFICACIÓN: Incluir programId en la URL
        const url = `/api/DefectsApi/GetDefectsData?lineid=${lineId}&startTime=${startTimeStr}&endTime=${endTimeStr}&programId=${programId}`;



        const response = await fetch(url);
        if (!response.ok) {
            const errorData = await response.json().catch(() => null);
            throw new Error(errorData?.error || response.statusText);
        }
        const data = await response.json();
        console.log("Respuesta completa del API:", data);

        // Insertar HTML
        $('#defectContainer').html(generateCardsHTML() + generateFooterHTML(data.hasRecords));

        // Configurar event listeners para elementos dinámicos
        setupEventListeners();

        // SIEMPRE cargar categorías y defectos disponibles
        await fetchCategories();
        await fetchDefects(lineId);

        // Procesar la respuesta completa del API
        if (data.hasRecords) {
            // Si hay registros existentes, llenar el formulario con esos datos
            fillFormData(data);
        } else {
            // Si no hay registros, limpiar el formulario pero mantener los defectos disponibles
            defectQuantities = {};
            totalVolantes = 0;
            renderDefects(); // Esto mostrará todos los defectos disponibles con cantidad 0
            updateSummary();
        }

    } catch (error) {
        console.error("Error al consultar el estado:", error.message);
        Swal.fire({
            icon: 'error',
            title: 'Error de Conexión',
            text: error.message,
            confirmButtonColor: '#d33'
        });
    }
}

// --- Configurar event listeners para elementos dinámicos ---
function setupEventListeners() {
    // Input de total volantes
    const totalVolantesInput = document.getElementById('totalVolantes');
    if (totalVolantesInput) {
        totalVolantesInput.addEventListener('input', (e) => {
            totalVolantes = parseInt(e.target.value) || 0;
            updateSummary();
        });
    }

    // Filtros
    const categoryFilter = document.getElementById('categoryFilter');
    const searchInput = document.getElementById('searchInput');

    if (categoryFilter) {
        categoryFilter.addEventListener('change', renderDefects);
    }

    if (searchInput) {
        searchInput.addEventListener('input', renderDefects);
    }

    // Botón submit - USANDO DELEGACIÓN DE EVENTOS
    $(document).off('click', '#submitBtn').on('click', '#submitBtn', function (e) {
        e.preventDefault();
        console.log('Botón submit clickeado'); // Debug
        handleSubmit();
    });
}

// --- Generar HTML ---
function generateCardsHTML() {
    return `
    <div class="mb-4">
        <div class="alert alert-info">
            <label for="totalVolantes" class="form-label fw-bold">
                <i class="bi bi-box me-2"></i> Cantidad Total de Volantes rechazados *
            </label>
            <input type="number" class="form-control form-control-lg" id="totalVolantes" placeholder="Ingrese la cantidad total" min="1" required>
        </div>
    </div>
    <div class="card bg-light mb-4">
        <div class="card-body">
            <h5 class="card-title"><i class="bi bi-funnel me-2"></i> Filtros</h5>
            <div class="row">
                <div class="col-md-6 mb-3">
                    <label for="categoryFilter" class="form-label">Filtrar por Categoría</label>
                    <select class="form-select" id="categoryFilter"></select>
                </div>
                <div class="col-md-6 mb-3">
                    <label for="searchInput" class="form-label">Buscar defecto</label>
                    <div class="input-group">
                        <span class="input-group-text"><i class="bi bi-search"></i></span>
                        <input type="text" class="form-control" id="searchInput" placeholder="Buscar por nombre...">
                    </div>
                </div>
            </div>
        </div>
    </div>
    <div class="mb-4">
        <h5 class="mb-3">
            <i class="bi bi-exclamation-triangle text-warning me-2"></i> Defectos Encontrados
            <span class="badge bg-secondary ms-2" id="defectCount">0 defectos disponibles</span>
        </h5>
        <div class="defect-list border rounded p-3" id="defectList"></div>
    </div>
    <div class="summary-card rounded p-4 mb-4 d-none" id="summaryCard">
        <h5 class="text-warning fw-bold mb-3"><i class="bi bi-clipboard-data me-2"></i> Resumen de Defectos Registrados:</h5>
        <div class="row">
            <div class="col-md-6"><div id="summaryStats"></div></div>
            <div class="col-md-6">
                <h6 class="fw-bold text-warning">Defectos registrados:</h6>
                <div id="summaryDefects"></div>
            </div>
        </div>
    </div>`;
}

function generateFooterHTML(hasRecords) {
    const buttonText = hasRecords ? 'Actualizar Defectos' : 'Registrar Defectos';
    const buttonClass = hasRecords ? 'btn-warning' : 'btn-success';
    return `<button type="button" class="btn ${buttonClass} btn-lg w-100" id="submitBtn">
        <i class="bi bi-check-circle me-2"></i> ${buttonText}
    </button>`;
}

async function fetchPrograms() {
    try {
        // Si lo guardaste en const
        // const areaId = areaId;

        // O si usaste hidden input
        const areaId = document.getElementById("areaId")?.value;

        if (!areaId) {
            console.warn("⚠️ No se encontró el areaId");
            return;
        }

        const response = await fetch(`/Defects/GetProgramsByArea?areaId=${areaId}`);
        if (!response.ok) throw new Error("Error al obtener programas");

        const data = await response.json();
        console.log("Programas obtenidos:", data);

        // Suponiendo que tienes un select donde mostrarás los programas
        const programSelect = document.getElementById("programSelect");
        if (programSelect) {
            programSelect.innerHTML = "";

            // Agregar opción por defecto
            const defaultOption = document.createElement("option");
            defaultOption.value = "";
            defaultOption.textContent = "Elige un programa";
            programSelect.appendChild(defaultOption);

            data.forEach(p => {
                const option = document.createElement("option");
                option.value = p.id;
                option.textContent = p.program;
                programSelect.appendChild(option);
            });
        }

    } catch (err) {
        console.error("❌ Error en fetchPrograms:", err);
    }
}

// --- Eventos de cambio MODIFICADOS ---
// Evento cambio de línea
function canLoadDefects() {
    const lineId = parseInt($('#lineProduction').val());
    const programId = parseInt($('#programSelect').val());
    const { startTime, endTime } = getTimeRange();

    if (!lineId || !programId || !startTime || !endTime) return null;

    return { lineId, programId, startTime, endTime };
}

async function tryLoadDefects() {
    const params = canLoadDefects();
    if (!params) {
        $('#defectContainer').empty();
        return;
    }
    await loadDefectsData(params.lineId, params.startTime, params.endTime, params.programId);
}

// --- Eventos usando la función helper ---
$(document).on('change', '#lineProduction', tryLoadDefects);
$(document).on('change', '#timeInterval', tryLoadDefects);
$(document).on('change', '#programSelect', tryLoadDefects);


// Evento cambio de programa

document.addEventListener("DOMContentLoaded", () => {
    fetchPrograms();
});
// ========================================
// GESTIÓN DE PART NUMBERS Y MODAL
// ========================================

// ========================================
// API CALLS
// ========================================

/**
 * Consultar el API externo a través del proxy local
 * @param {string} partNumber - Número de parte a buscar
 * @returns {Promise<Array>} - Datos del part number
 */
async function getPartNumberFromAPI(partNumber) {
    try {
        const apiUrl = `${CONFIG.API.PART_NUMBER}?partNumber=${encodeURIComponent(partNumber)}`;
        const response = await fetch(apiUrl);

        if (!response.ok) {
            if (response.status === 404) {
                throw new Error(`No se encontró el número de parte: ${partNumber}`);
            }
            throw new Error('Error al consultar el número de parte');
        }

        const data = await response.json();
        return data;
    } catch (error) {
        console.error('Error al consultar el API:', error);
        throw error;
    }
}

/**
 * Cargar áreas/customers desde la base de datos
 */
async function loadCustomers() {
    try {
        const response = await fetch(CONFIG.API.AREAS);
        if (!response.ok) throw new Error('Error al cargar áreas');

        const areas = await response.json();
        const select = document.getElementById('customerSelectManual');

        select.innerHTML = '<option value="">Seleccione un área...</option>';

        areas.forEach(area => {
            const option = document.createElement('option');
            const areaFullName = area.areaName + " " + area.customerName;
            option.value = areaFullName;
            option.textContent = areaFullName;
            select.appendChild(option);
        });

    } catch (error) {
        console.error('Error al cargar customers:', error);
        alert('Error al cargar las áreas: ' + error.message);
    }
}

/**
 * Cargar programas cuando se selecciona un customer
 */
async function loadProgramsManual() {
    const customerSelect = document.getElementById('customerSelectManual');
    const selectedCustomer = customerSelect.value;
    const programContainer = document.getElementById('programContainerManual');
    const programSelect = document.getElementById('programSelectManual');

    // Limpiar datos previos
    state.currentPartData = null;
    state.currentPartNumber = null;

    if (!selectedCustomer) {
        programContainer.style.display = 'none';
        return;
    }

    try {
        // Mostrar indicador de carga
        programSelect.innerHTML = '<option value="">Cargando programas...</option>';
        programContainer.style.display = 'block';

        const response = await fetch(`${CONFIG.API.PROGRAMS_BY_CUSTOMER}?customerName=${encodeURIComponent(selectedCustomer)}`);
        if (!response.ok) throw new Error('Error al cargar programas');

        const programs = await response.json();

        programSelect.innerHTML = '<option value="">Seleccione un programa...</option>';

        if (programs.length === 0) {
            programSelect.innerHTML = '<option value="">No hay programas disponibles</option>';
            return;
        }

        programs.forEach(program => {
            const option = document.createElement('option');
            option.value = program.id;
            option.textContent = program.program;
            // Guardar toda la información del programa en data attributes
            option.dataset.programName = program.program;
            option.dataset.partnumber = program.partnumber;
            option.dataset.type = program.type;
            option.dataset.customer = selectedCustomer;
            programSelect.appendChild(option);
        });

    } catch (error) {
        console.error('Error al cargar programas:', error);
        programSelect.innerHTML = '<option value="">Error al cargar programas</option>';
        alert('Error al cargar programas: ' + error.message);
    }
}

/**
 * Cargar información del part number cuando se selecciona un programa
 */
function loadPartNumberByProgram() {
    const programSelect = document.getElementById('programSelectManual');
    const selectedProgramId = programSelect.value;
    const selectedOption = programSelect.options[programSelect.selectedIndex];

    if (!selectedProgramId) {
        state.currentPartData = null;
        state.currentPartNumber = null;
        return;
    }

    // Obtener datos del data attribute (ya vienen del API)
    const programName = selectedOption.dataset.programName;
    const partnumber = selectedOption.dataset.partnumber;
    const type = selectedOption.dataset.type;
    const customer = selectedOption.dataset.customer;

    // Guardar en state.currentPartData para usar en confirmChange()
    state.currentPartData = {
        partnumber: partnumber,
        customer: customer,
        program: programName,
        type: type,
        programId: parseInt(selectedProgramId) // Agregar el programId
    };

    // Actualizar la variable específica del número de parte
    state.currentPartNumber = partnumber || null;

    console.log('Part Number seleccionado:', state.currentPartNumber);
}

// ========================================
// MODAL CHANGE - Gestión del Modal
// ========================================

/**
 * Abrir modal de cambio de part number
 */
function openChangeModal() {
    document.getElementById('changeModal').classList.remove('hidden');
    // Por defecto, cargar el método de escaneo
    selectMethod('scan');
}

/**
 * Cerrar modal de cambio de part number
 */
function closeChangeModal() {
    document.getElementById('changeModal').classList.add('hidden');
    document.getElementById('scanRequerimiento').value = '';
    document.getElementById('customerSelectManual').value = '';
    document.getElementById('programSelectManual').value = '';
    document.getElementById('programContainerManual').style.display = 'none';
    searchResults = [];
    // NO limpiamos state.currentPartData ni state.currentPartNumber
    // porque queremos mantener el número de parte seleccionado
    selectedMethod = null;

    // Devolver el foco al input principal de escaneo
    setTimeout(() => {
        document.getElementById('scanInput').focus();
    }, 100);
}

/**
 * Seleccionar método de cambio (scan o manual)
 * @param {string} method - 'scan' o 'manual'
 */
function selectMethod(method) {
    selectedMethod = method;
    if (method === 'scan') {
        document.getElementById('scanMethod').style.display = 'block';
        document.getElementById('manualMethod').style.display = 'none';
        setTimeout(() => {
            document.getElementById('scanRequerimiento').focus();
        }, 100);
    } else if (method === 'manual') {
        document.getElementById('scanMethod').style.display = 'none';
        document.getElementById('manualMethod').style.display = 'block';
        // Cargar customers al seleccionar método manual
        loadCustomers();
        setTimeout(() => {
            document.getElementById('customerSelectManual').focus();
        }, 100);
    }
}

/**
 * Confirmar cambio de part number
 */
async function confirmChange() {
    let partData = null;

    if (selectedMethod === 'scan') {
        // Si el método es escaneo, verificar si ya se consultó el API
        if (state.currentPartData) {
            partData = state.currentPartData;
        } else {
            // Si no hay datos pero hay un valor en el input, consultar el API
            const scanInput = document.getElementById('scanRequerimiento');
            const partNumber = scanInput.value.trim();

            if (partNumber) {
                try {
                    // Llamar al API
                    const data = await getPartNumberFromAPI(partNumber);

                    // Guardar los datos en el estado
                    updatePartNumberInfo(data);

                    partData = state.currentPartData;
                } catch (error) {
                    alert(error.message || 'Error al consultar el número de parte');
                    return;
                }
            } else {
                alert('Por favor escanee un número de parte válido primero');
                return;
            }
        }
    } else if (selectedMethod === 'manual') {
        // Método manual usa los datos del API almacenados en state.currentPartData
        if (state.currentPartData) {
            partData = state.currentPartData;
        } else {
            alert('Por favor seleccione un número de parte válido primero');
            return;
        }
    }

    if (partData && partData.partnumber) {
        // Actualizar los datos en el index principal
        document.getElementById('customerName').textContent = partData.customer || '';
        document.getElementById('programName').textContent = partData.program || '';
        document.getElementById('partNumberName').textContent = partData.partnumber || '';

        console.log('Número de parte actualizado:', partData);
        closeChangeModal();
    } else {
        alert('Por favor seleccione un método y proporcione un valor');
    }
}

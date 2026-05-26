// Implementación simple para ver datos de producción sin DataTables
let currentData = [];
let currentPage = 1;
const itemsPerPage = 25;

// Función global para inicializar la vista simple
window.initializeViewDataModal = function() {
    console.log("📊 Inicializando vista simple de datos de producción...");

    // Verificar elementos necesarios
    const tableElement = document.getElementById('viewDataTable');
    const areaElement = document.getElementById('AreaIdHidden');
    const dateElement = document.getElementById('selectedDate');

    if (!tableElement || !areaElement || !dateElement) {
        console.error("❌ Elementos necesarios no encontrados");
        return;
    }

    // Configurar eventos
    setupEvents();

    // Las líneas ya vienen del ViewBag, no necesitamos cargarlas
    console.log("📋 Líneas de producción cargadas desde ViewBag");

    // Cargar datos iniciales
    loadDataSimple();
};

function setupEvents() {
    // Event listener para cambio de fecha
    const dateElement = document.getElementById('selectedDate');
    if (dateElement) {
        // Remover listeners anteriores
        dateElement.removeEventListener('change', loadDataSimple);
        // Agregar nuevo listener
        dateElement.addEventListener('change', loadDataSimple);
    }

    // Event listener para filtro de línea
    const lineFilterElement = document.getElementById('lineFilter');
    if (lineFilterElement) {
        lineFilterElement.removeEventListener('change', loadDataSimple);
        lineFilterElement.addEventListener('change', loadDataSimple);
    }

    // Event listener para limpiar filtros
    const clearFiltersBtn = document.getElementById('clearFilters');
    if (clearFiltersBtn) {
        clearFiltersBtn.removeEventListener('click', clearFilters);
        clearFiltersBtn.addEventListener('click', clearFilters);
    }
}

function loadDataSimple() {
    const areaElement = document.getElementById('AreaIdHidden');
    const dateElement = document.getElementById('selectedDate');
    const lineFilterElement = document.getElementById('lineFilter');

    if (!areaElement || !dateElement) {
        console.warn("❌ Elementos necesarios no encontrados para cargar datos");
        return;
    }

    const areaId = areaElement.value || 0;
    const selectedDate = dateElement.value;
    const lineFilter = lineFilterElement ? lineFilterElement.value : '';

    if (!selectedDate) {
        console.warn("⚠️ No hay fecha seleccionada");
        return;
    }

    console.log("🔄 Cargando datos para fecha:", selectedDate, "línea:", lineFilter || "todas");

    // Mostrar loading
    showLoading();

    // Hacer petición AJAX usando el mismo formato que DataTables
    const requestData = new URLSearchParams({
        draw: 1,
        start: 0,
        length: 1000,
        areaId: areaId,
        selectedDate: selectedDate,
        lineFilter: lineFilter
    });

    fetch('/api/ProductionDataApi/GetDataByDate', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
        },
        body: requestData
    })
    .then(response => {
        console.log("📡 Response status:", response.status);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        return response.json();
    })
    .then(data => {
        console.log("📥 Datos recibidos completos:", data);
        console.log("📋 Estructura de data:", Object.keys(data));

        // Verificar diferentes posibles estructuras de respuesta
        let dataArray = [];
        if (data.data && Array.isArray(data.data)) {
            dataArray = data.data;
            console.log("✅ Usando data.data, registros encontrados:", dataArray.length);
        } else if (Array.isArray(data)) {
            dataArray = data;
            console.log("✅ Usando data directamente, registros encontrados:", dataArray.length);
        } else if (data.recordsTotal !== undefined) {
            console.log("📊 Respuesta tipo DataTables - recordsTotal:", data.recordsTotal);
            dataArray = data.data || [];
        } else {
            console.warn("⚠️ Estructura de datos no reconocida:", data);
        }

        currentData = dataArray;
        currentPage = 1;

        console.log("🎯 Datos finales para mostrar:", currentData.length, "registros");
        if (currentData.length > 0) {
            console.log("📄 Primer registro:", currentData[0]);
        }

        renderTable();
        renderPagination();
    })
    .catch(error => {
        console.error("❌ Error al cargar datos:", error);
        showError("Error al cargar datos: " + error.message);
    });
}

function showLoading() {
    const tbody = document.querySelector('#viewDataTable tbody');
    if (tbody) {
        tbody.innerHTML = `
            <tr>
                <td colspan="5" class="text-center p-4">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Cargando...</span>
                    </div>
                    <p class="mt-2 mb-0">Cargando datos...</p>
                </td>
            </tr>
        `;
    }
}

function showError(message) {
    const tbody = document.querySelector('#viewDataTable tbody');
    if (tbody) {
        tbody.innerHTML = `
            <tr>
                <td colspan="5" class="text-center p-4">
                    <div class="alert alert-danger">
                        <i class="fas fa-exclamation-triangle"></i>
                        ${message}
                    </div>
                </td>
            </tr>
        `;
    }
}

function renderTable() {
    console.log("🎨 Renderizando tabla...");
    const tbody = document.querySelector('#viewDataTable tbody');
    if (!tbody) {
        console.error("❌ No se encontró el tbody de la tabla");
        return;
    }

    console.log("📊 Datos para renderizar - currentData:", currentData.length);

    if (currentData.length === 0) {
        const message = "No se encontraron datos para la fecha seleccionada";

        console.log("📝 Mostrando mensaje:", message);
        tbody.innerHTML = `
            <tr>
                <td colspan="5" class="text-center p-4">
                    <div class="alert alert-info">
                        <i class="fas fa-info-circle"></i>
                        ${message}
                    </div>
                </td>
            </tr>
        `;
        return;
    }

    // Calcular datos para la página actual
    const startIndex = (currentPage - 1) * itemsPerPage;
    const endIndex = startIndex + itemsPerPage;
    const pageData = currentData.slice(startIndex, endIndex);

    console.log("📄 Datos para página actual:", pageData.length, "registros (índices", startIndex, "a", endIndex, ")");

    // Generar HTML de las filas
    let html = '';
    pageData.forEach((row, index) => {
        console.log(`📝 Procesando fila ${index}:`, row);

        try {
            const date = new Date(row.productionDate).toLocaleDateString('es-ES');
            const pieces = row.producedPieces ? row.producedPieces.toLocaleString() : '0';
            const program = row.programDescription || 'Sin descripción';

            html += `
                <tr>
                    <td class="text-center">${date}</td>
                    <td class="text-center">${row.startHour} - ${row.endHour}</td>
                    <td class="text-center">${row.line}</td>
                    <td class="text-center">${program}</td>
                    <td class="text-center">${pieces}</td>
                </tr>
            `;
        } catch (error) {
            console.error("❌ Error procesando fila:", error, row);
        }
    });

    console.log("🔧 HTML generado:", html.length, "caracteres");
    tbody.innerHTML = html;

    // Mostrar información de registros
    showRecordsInfo();
}

function renderPagination() {
    const totalPages = Math.ceil(currentData.length / itemsPerPage);

    // Buscar o crear contenedor de paginación
    let paginationContainer = document.getElementById('pagination-container');
    if (!paginationContainer) {
        paginationContainer = document.createElement('div');
        paginationContainer.id = 'pagination-container';
        paginationContainer.className = 'row mt-3';

        const tableContainer = document.querySelector('#viewDataTable').parentNode;
        tableContainer.appendChild(paginationContainer);
    }

    if (totalPages <= 1) {
        paginationContainer.innerHTML = '';
        return;
    }

    let paginationHtml = `
        <div class="col-12">
            <nav aria-label="Paginación de tabla">
                <ul class="pagination justify-content-center">
    `;

    // Botón anterior
    if (currentPage > 1) {
        paginationHtml += `
            <li class="page-item">
                <a class="page-link" href="#" onclick="changePage(${currentPage - 1})">Anterior</a>
            </li>
        `;
    }

    // Números de página
    for (let i = 1; i <= totalPages; i++) {
        if (i === currentPage) {
            paginationHtml += `<li class="page-item active"><span class="page-link">${i}</span></li>`;
        } else {
            paginationHtml += `<li class="page-item"><a class="page-link" href="#" onclick="changePage(${i})">${i}</a></li>`;
        }
    }

    // Botón siguiente
    if (currentPage < totalPages) {
        paginationHtml += `
            <li class="page-item">
                <a class="page-link" href="#" onclick="changePage(${currentPage + 1})">Siguiente</a>
            </li>
        `;
    }

    paginationHtml += `
                </ul>
            </nav>
        </div>
    `;

    paginationContainer.innerHTML = paginationHtml;
}

function showRecordsInfo() {
    const totalRecords = currentData.length;
    const startRecord = totalRecords === 0 ? 0 : (currentPage - 1) * itemsPerPage + 1;
    const endRecord = Math.min(currentPage * itemsPerPage, totalRecords);

    // Buscar o crear contenedor de información
    let infoContainer = document.getElementById('records-info');
    if (!infoContainer) {
        infoContainer = document.createElement('div');
        infoContainer.id = 'records-info';
        infoContainer.className = 'col-12 mb-2';

        const tableContainer = document.querySelector('#viewDataTable').parentNode;
        tableContainer.insertBefore(infoContainer, document.querySelector('#viewDataTable'));
    }

    infoContainer.innerHTML = `
        <small class="text-muted">
            Mostrando registros del ${startRecord} al ${endRecord} de un total de ${totalRecords} registros
        </small>
    `;
}


// Función global para cambio de página
window.changePage = function(page) {
    currentPage = page;
    renderTable();
    renderPagination();
};

// Función de limpieza
window.cleanupViewData = function() {
    // Limpiar event listeners
    const dateElement = document.getElementById('selectedDate');
    if (dateElement) {
        dateElement.removeEventListener('change', loadDataSimple);
    }

    // Limpiar contenedores creados dinámicamente
    const paginationContainer = document.getElementById('pagination-container');
    if (paginationContainer) {
        paginationContainer.remove();
    }

    const infoContainer = document.getElementById('records-info');
    if (infoContainer) {
        infoContainer.remove();
    }

    // Resetear variables
    currentData = [];
    currentPage = 1;

    console.log("🧹 Limpieza de vista de datos completada");
};

// Nota: La función loadLines() ya no es necesaria
// porque las líneas se cargan desde ViewBag en el servidor

// Función para limpiar filtros
function clearFilters() {
    const dateElement = document.getElementById('selectedDate');
    const lineFilterElement = document.getElementById('lineFilter');

    if (dateElement) {
        dateElement.value = new Date().toISOString().split('T')[0];
    }

    if (lineFilterElement) {
        lineFilterElement.value = '';
    }

    // Recargar datos
    loadDataSimple();
}
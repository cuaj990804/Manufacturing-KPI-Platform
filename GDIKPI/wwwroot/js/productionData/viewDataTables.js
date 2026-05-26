// Script para vista de datos de producción con DataTables
let productionDataTable = null;

// Función global para inicializar DataTables
window.initializeViewDataModal = function() {
    console.log("📊 Inicializando DataTables para vista de producción...");

    // Verificar elementos necesarios
    const tableElement = document.getElementById('viewDataTable');
    const areaElement = document.getElementById('AreaIdHidden');

    if (!tableElement || !areaElement) {
        console.error("❌ Elementos necesarios no encontrados");
        return;
    }

    // Configurar DataTables
    initializeDataTable();

    // Configurar eventos de filtros
    setupFilters();

    console.log("✅ DataTables inicializado correctamente");
};

// Función para inicializar DataTables
function initializeDataTable() {
    // Destruir instancia existente si existe
    if (productionDataTable) {
        productionDataTable.destroy();
    }

    // Configuración de DataTables
    productionDataTable = $('#viewDataTable').DataTable({
        // Configuración de servidor
        processing: true,
        serverSide: true,
        ajax: {
            url: '/api/ProductionDataApi/GetDataByDate',
            type: 'POST',
            data: function(d) {
                // Agregar parámetros personalizados
                d.areaId = $('#AreaIdHidden').val();
                d.selectedDate = $('#selectedDate').val();
                d.lineFilter = getLineFilterValue();

                console.log("📡 Enviando datos a servidor:", d);
                return d;
            },
            error: function(xhr, error, thrown) {
                console.error("❌ Error en AJAX:", error, thrown);
                showAlert("error", "Error", "Error al cargar datos: " + error);
            }
        },

        // Configuración de columnas
        columns: [
            {
                data: null,
                title: 'Intervalo de Hora',
                className: 'text-center',
                responsivePriority: 2, // Mayor prioridad para que se mantenga visible
                render: function(data, type, row) {
                    return `${row.startHour} - ${row.endHour}`;
                }
            },
            {
                data: 'line',
                title: 'Línea',
                className: 'text-center',
                responsivePriority: 3
            },
            {
                data: 'programDescription',
                title: 'Programa',
                className: 'text-center',
                responsivePriority: 4, // Va al dropdown antes que Fecha
                render: function(data) {
                    return data || 'Sin programa';
                }
            },
            {
                data: 'producedPieces',
                title: 'Piezas Producidas',
                className: 'text-center',
                responsivePriority: 1, // Máxima prioridad - siempre visible
                render: function(data) {
                    return data ? data.toLocaleString() : '0';
                }
            },
            {
                data: 'productionDate',
                title: 'Fecha',
                className: 'text-center',
                responsivePriority: 5, // Menor prioridad - último en el dropdown
                render: function(data) {
                    // Extraer solo la fecha sin conversión de zona horaria
                    if (!data) return '';
                    const dateStr = data.split('T')[0]; // "2025-10-01"
                    const [year, month, day] = dateStr.split('-');
                    return `${day}/${month}/${year}`;
                }
            }
        ],

        // Configuración de ordenamiento
        order: [[4, 'desc'], [0, 'desc']], // Por fecha descendente, luego por hora

        // Configuración de paginación
        pageLength: 25,
        lengthChange: false, // Quitar selector de "mostrar registros"

        // Configuración de búsqueda
        searching: false,

        // Configuración responsive
        responsive: true,
        autoWidth: false,
        scrollX: true,

        // Configuración de idioma
        language: {
            processing: "Procesando...",
            search: "Buscar:",
            lengthMenu: "Mostrar _MENU_ registros",
            info: "Mostrando registros del _START_ al _END_ de un total de _TOTAL_ registros",
            infoEmpty: "Mostrando registros del 0 al 0 de un total de 0 registros",
            infoFiltered: "(filtrado de un total de _MAX_ registros)",
            loadingRecords: "Cargando...",
            zeroRecords: "No se encontraron datos para la fecha seleccionada",
            emptyTable: "No hay datos disponibles en la tabla",
            paginate: {
                first: "Primero",
                previous: "Anterior",
                next: "Siguiente",
                last: "Último"
            }
        },

        // Configuración adicional - sin controles de length (l) y filter (f)
        dom: '<"row"<"col-sm-12"tr>>' +
             '<"row"<"col-sm-12 col-md-5"i><"col-sm-12 col-md-7"p>>',

        // Callbacks
        initComplete: function() {
            console.log("✅ DataTable inicializado completamente");
        },
        drawCallback: function() {
            console.log("📊 Tabla redibujada");
        }
    });
}

// Función para configurar filtros personalizados
function setupFilters() {
    // Evento para cambio de fecha
    $('#selectedDate').on('change', function() {
        console.log("📅 Fecha cambiada:", $(this).val());
        reloadTable();
    });

    // Evento para cambio de línea
    $('#lineFilter').on('change', function() {
        console.log("🏭 Línea cambiada:", $(this).val());
        reloadTable();
    });

    // Evento para limpiar filtros
    $('#clearFilters').on('click', function() {
        console.log("🧹 Limpiando filtros");
        clearFilters();
    });

    // Evento para actualizar datos
    $('#refreshData').on('click', function() {
        console.log("🔄 Actualizando datos");
        reloadTable();
    });
}

// Función para obtener el valor del filtro de línea
function getLineFilterValue() {
    const lineFilter = $('#lineFilter').val();
    if (!lineFilter || lineFilter === '') {
        return '';
    }

    // Extraer el número de línea del texto "Línea X"
    const match = lineFilter.match(/Línea\s+(\d+)/);
    return match ? match[1] : '';
}

// Función para recargar la tabla
function reloadTable() {
    if (productionDataTable) {
        console.log("🔄 Recargando DataTable...");
        productionDataTable.ajax.reload(null, false); // Mantener la página actual
    }
}

// Función para limpiar filtros
function clearFilters() {
    // Resetear fecha a hoy
    $('#selectedDate').val(new Date().toISOString().split('T')[0]);

    // Resetear filtro de línea
    $('#lineFilter').val('');

    // Recargar tabla
    reloadTable();
}

// Función para mostrar alertas (debe estar disponible globalmente)
function showAlert(type, title, message) {
    // Si existe la función global, usarla
    if (typeof window.showAlert === 'function') {
        window.showAlert(type, title, message);
        return;
    }

    // Fallback simple con alert
    alert(`${title}: ${message}`);
}

// Función de limpieza
window.cleanupViewData = function() {
    console.log("🧹 Limpiando DataTables...");

    if (productionDataTable) {
        productionDataTable.destroy();
        productionDataTable = null;
    }

    // Limpiar eventos
    $('#selectedDate, #lineFilter, #clearFilters, #refreshData').off();

    console.log("✅ Limpieza de DataTables completada");
};

// Inicialización automática cuando se carga el script en un modal
$(document).ready(function() {
    // Solo inicializar si estamos en un contexto donde existe la tabla
    if ($('#viewDataTable').length > 0) {
        console.log("📱 Detectada tabla de vista de datos, inicializando...");
        setTimeout(() => {
            if (typeof window.initializeViewDataModal === 'function') {
                window.initializeViewDataModal();
            }
        }, 100);
    }
});
// Variable global para manejar DataTable
if (typeof window.viewDataTable === 'undefined') {
    window.viewDataTable = null;
}

// Función para verificar que todas las dependencias estén disponibles
function checkDataTableDependencies() {
    // Verificar jQuery
    if (typeof $ === 'undefined' || typeof jQuery === 'undefined') {
        console.error("❌ jQuery no está disponible");
        return false;
    }

    // Verificar DataTables
    if (typeof $.fn.DataTable === 'undefined') {
        console.error("❌ DataTables no está disponible");
        return false;
    }

    // Verificar que DataTables esté completamente cargado
    if (!$.fn.DataTable.Api) {
        console.error("❌ DataTables API no está disponible");
        return false;
    }

    console.log("✅ Todas las dependencias de DataTables están disponibles");
    return true;
}

// Función global para inicializar la vista de datos
window.initializeViewDataModal = function() {
    console.log("📊 Inicializando vista de datos de producción...");

    // Verificar dependencias antes de proceder
    if (!checkDataTableDependencies()) {
        console.error("❌ No se pueden inicializar DataTables - dependencias faltantes");
        return;
    }

    // Destruir DataTable existente completamente con verificaciones adicionales
    if (window.viewDataTable) {
        try {
            // Verificar que la tabla aún existe en el DOM antes de destruir
            const tableElement = document.getElementById('viewDataTable');
            if (tableElement && $.fn.DataTable && $.fn.DataTable.isDataTable('#viewDataTable')) {
                window.viewDataTable.destroy(true);
                console.log("DataTable anterior destruido");
            }
        } catch (e) {
            console.warn("Error al destruir DataTable anterior:", e);
        }
        window.viewDataTable = null;
    }

    // Limpiar cualquier instancia residual de DataTable
    try {
        if ($.fn.DataTable && $.fn.DataTable.isDataTable('#viewDataTable')) {
            $('#viewDataTable').DataTable().destroy(true);
        }
    } catch (e) {
        console.warn("Limpieza adicional de DataTable:", e);
    }

    // Verificar que la tabla existe
    const tableElement = document.getElementById('viewDataTable');
    if (!tableElement) {
        console.error("Tabla #viewDataTable no encontrada");
        return;
    }

    // Limpiar completamente la tabla y remover cualquier evento residual
    $(tableElement).off().empty().removeClass().addClass('table table-striped table-hover');

    // Recrear el header de la tabla
    const tableHeader = `
        <thead class="table-dark">
            <tr>
                <th class="text-center">Fecha</th>
                <th class="text-center">Intervalo de Hora</th>
                <th class="text-center">Línea</th>
                <th class="text-center">Programa</th>
                <th class="text-center">Piezas Producidas</th>
            </tr>
        </thead>
        <tbody></tbody>
    `;
    tableElement.innerHTML = tableHeader;

    // Esperar hasta que el DOM esté completamente listo para DataTable
    waitForTableReady(() => {
        initializeDataTable();
    });
};

// Función para esperar hasta que la tabla esté completamente lista para DataTable
function waitForTableReady(callback, maxAttempts = 20) {
    let attempts = 0;

    function check() {
        attempts++;

        // Verificar que la tabla existe y tiene el DOM completo
        const table = document.getElementById('viewDataTable');
        if (table &&
            table.querySelector('thead') &&
            table.querySelector('tbody') &&
            table.offsetParent !== null) { // Verificar que esté visible

            console.log(`✅ Tabla lista después de ${attempts} intentos`);
            callback();
            return;
        }

        if (attempts >= maxAttempts) {
            console.error(`❌ Tabla no estuvo lista después de ${maxAttempts} intentos`);
            return;
        }

        console.log(`⏳ Esperando tabla... intento ${attempts}/${maxAttempts}`);
        setTimeout(check, 100);
    }

    check();
}

function initializeDataTable() {
    // Verificar dependencias nuevamente antes de crear la instancia
    if (!checkDataTableDependencies()) {
        console.error("❌ DataTables no está disponible para inicialización");
        return;
    }

    // Verificación final antes de crear DataTable
    const tableElement = document.getElementById('viewDataTable');
    const areaElement = document.getElementById('AreaIdHidden');
    const dateElement = document.getElementById('selectedDate');

    if (!tableElement) {
        console.error("❌ Elemento tabla no encontrado en el momento de inicialización");
        return;
    }

    if (!areaElement) {
        console.error("❌ Elemento AreaIdHidden no encontrado");
        return;
    }

    if (!dateElement) {
        console.error("❌ Elemento selectedDate no encontrado");
        return;
    }

    if (!tableElement.querySelector('thead') || !tableElement.querySelector('tbody')) {
        console.error("❌ Estructura de tabla incompleta");
        return;
    }

    try {
        console.log("🔧 Creando nueva instancia de DataTable...");

        // Usar doble requestAnimationFrame para asegurar rendering completo
        requestAnimationFrame(() => {
            requestAnimationFrame(() => {
                // Verificación final antes de crear la instancia
                const finalTableCheck = document.getElementById('viewDataTable');
                if (!finalTableCheck || !finalTableCheck.querySelector('thead') || !finalTableCheck.querySelector('tbody')) {
                    console.error("❌ Tabla no está completamente renderizada para DataTable");
                    return;
                }

                // Verificar una vez más que DataTables esté disponible
                if (!$.fn.DataTable) {
                    console.error("❌ DataTables no está disponible en el momento de creación");
                    return;
                }

                // Envolver la creación de DataTable en try-catch adicional
                try {
                    window.viewDataTable = $('#viewDataTable').DataTable({
            processing: true,
            serverSide: true,
            destroy: true, // Permitir destrucción automática
            ajax: {
                url: '/api/ProductionDataApi/GetDataByDate',
                type: 'POST',
                data: function(d) {
                    const areaElement = document.getElementById('AreaIdHidden');
                    const dateElement = document.getElementById('selectedDate');

                    const areaId = areaElement ? areaElement.value || 0 : 0;
                    const selectedDate = dateElement ? dateElement.value : '';

                    console.log("📤 Enviando datos:", {
                        draw: d.draw,
                        start: d.start,
                        length: d.length,
                        areaId: areaId,
                        selectedDate: selectedDate
                    });

                    if (!areaId || !selectedDate) {
                        console.warn("⚠️ Faltan datos necesarios - areaId:", areaId, "selectedDate:", selectedDate);
                    }

                    return {
                        draw: d.draw,
                        start: d.start,
                        length: d.length,
                        areaId: areaId,
                        selectedDate: selectedDate
                    };
                },
                dataSrc: function(json) {
                    console.log("📥 Datos recibidos:", json);
                    return json.data;
                }
            },
            columns: [
                {
                    data: 'productionDate',
                    className: 'text-center',
                    render: function(data) {
                        // Extraer solo la fecha sin conversión de zona horaria
                        if (!data) return '';
                        const dateStr = data.split('T')[0]; // "2025-10-01"
                        const [year, month, day] = dateStr.split('-');
                        return `${day}/${month}/${year}`;
                    }
                },
                {
                    data: null,
                    className: 'text-center',
                    render: function(data, type, row) {
                        return `${row.startHour} - ${row.endHour}`;
                    }
                },
                {
                    data: 'line',
                    className: 'text-center'
                },
                {
                    data: 'programDescription',
                    className: 'text-center',
                    render: function(data, type, row) {
                        return data || 'Sin descripción';
                    }
                },
                {
                    data: 'producedPieces',
                    className: 'text-center',
                    render: function(data) {
                        return data ? data.toLocaleString() : '0';
                    }
                }
            ],
            order: [[0, 'desc'], [1, 'desc']],
            pageLength: 25,
            lengthMenu: [[10, 25, 50, 100], [10, 25, 50, 100]],
            language: {
                processing: "Procesando...",
                search: "Buscar:",
                lengthMenu: "Mostrar _MENU_ registros",
                info: "Mostrando registros del _START_ al _END_ de un total de _TOTAL_ registros",
                infoEmpty: "Mostrando registros del 0 al 0 de un total de 0 registros",
                infoFiltered: "(filtrado de un total de _MAX_ registros)",
                loadingRecords: "Cargando...",
                zeroRecords: "No se encontraron resultados",
                emptyTable: "No hay datos disponibles en la tabla para la fecha seleccionada",
                paginate: {
                    first: "Primero",
                    previous: "Anterior",
                    next: "Siguiente",
                    last: "Último"
                }
            },
            responsive: true,
            searching: false,
            dom: '<"row"<"col-sm-12 col-md-6"l><"col-sm-12 col-md-6">>' +
                 '<"row"<"col-sm-12"tr>>' +
                 '<"row"<"col-sm-12 col-md-5"i><"col-sm-12 col-md-7"p>>'
        });

            console.log("✅ DataTable inicializado correctamente");

            // Configurar event listener para cambio de fecha
            const dateElement = document.getElementById('selectedDate');
            if (dateElement) {
                $(dateElement).off('change.viewData').on('change.viewData', function() {
                    loadData();
                });
            } else {
                console.warn("⚠️ Elemento selectedDate no encontrado para configurar event listener");
            }

                    // Cargar datos iniciales
                    setTimeout(() => {
                        loadData();
                    }, 100);

                } catch (innerError) {
                    console.error("❌ Error interno al crear DataTable:", innerError);
                    window.viewDataTable = null;
                }
            });
        });

    } catch (error) {
        console.error("❌ Error al inicializar DataTable:", error);
        window.viewDataTable = null;
    }
}

function loadData() {
    const dateElement = document.getElementById('selectedDate');
    if (!dateElement) {
        console.warn("Campo de fecha no encontrado");
        return;
    }

    const selectedDate = dateElement.value;
    console.log("🔄 Cargando datos para fecha:", selectedDate);

    if (!selectedDate) {
        console.warn("No hay fecha seleccionada");
        return;
    }

    if (!window.viewDataTable || !window.viewDataTable.ajax) {
        console.warn("DataTable no está inicializado");
        return;
    }

    try {
        window.viewDataTable.ajax.reload(function(json) {
            console.log("✅ Datos cargados:", json);
            const recordsCount = json ? (json.recordsTotal || 0) : 0;
            console.log(`📊 Se encontraron ${recordsCount} registros para la fecha ${selectedDate}`);
        }, false);
    } catch (error) {
        console.error("❌ Error al recargar datos:", error);
    }
}
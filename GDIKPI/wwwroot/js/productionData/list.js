// Función global para abrir modales
async function fetchPrograms() {
    console.log("🔄 Iniciando fetchPrograms...");
    try {
        const areaId = document.getElementById("AreaIdHidden")?.value;
        console.log("📍 AreaId encontrado:", areaId);

        if (!areaId || areaId <= 0) {
            console.warn("⚠️ No se encontró el areaId válido");
            return;
        }

        console.log("🌐 Haciendo fetch a:", `/ProductionData/GetProgramsByArea?areaId=${areaId}`);
        const response = await fetch(`/ProductionData/GetProgramsByArea?areaId=${areaId}`);
        if (!response.ok) throw new Error("Error al obtener programas");

        const data = await response.json();
        console.log("📋 Programas obtenidos:", data);

        const programSelect = document.getElementById("ProgramProductionData");
        if (programSelect) {
            console.log("🎯 Select encontrado, actualizando opciones...");
            programSelect.innerHTML = ""; // Limpiar opciones anteriores

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

            console.log("✅ Programas cargados exitosamente:", programSelect.children.length, "opciones");
        } else {
            console.warn("⚠️ No se encontró el select ProgramProductionData");
        }
    } catch (err) {
        console.error("❌ Error en fetchPrograms:", err);
        throw err; // Re-lanzar el error para que Promise.all lo capture
    }
}

// Nota: Las funciones fetchLines() y populateLines() ya no son necesarias
// porque las líneas se cargan desde ViewBag en el servidor
window.openModal = async function (action) {
    const areaId = $('#AreaIdHidden').val();
    let url, title;

    switch (action) {
        case 'create':
            url = `/ProductionData/LoadCreateForm?areaId=${areaId}`;
            title = "Registro de Producción del Área";

            // Mostrar loading en el modal primero
            $("#modalTitle").text(title);
            $("#modalBody").html('<div class="text-center"><div class="spinner-border" role="status"><span class="visually-hidden">Cargando...</span></div><p class="mt-2">Cargando formulario y programas...</p></div>');
            $("#actionModal").modal("show");

            try {
                // Cargar programas y formulario en paralelo
                const [_, html] = await Promise.all([
                    fetchPrograms(),
                    new Promise((resolve, reject) => {
                        $.get(url)
                            .done(resolve)
                            .fail(reject);
                    })
                ]);

                // Una vez que ambos estén listos, actualizar el modal
                $("#modalBody").html(html);

                // Verificar que los programas se hayan cargado correctamente después de insertar el HTML
                setTimeout(() => {
                    const programSelect = document.getElementById("ProgramProductionData");
                    if (programSelect && programSelect.children.length <= 1) {
                        console.warn("⚠️ Los programas no se cargaron correctamente, reintentando...");
                        fetchPrograms();
                    }
                }, 100);
            } catch (error) {
                console.error('Error al cargar modal:', error);
                $("#modalBody").html('<div class="alert alert-danger">Error al cargar el formulario. Por favor, intenta nuevamente.</div>');
            }
            return;

        case 'downtime':
            url = `/ProductionData/LoadDownTime?areaId=${areaId}`;
            title = "Registro de Tiempo Muerto del Área";
            break;
        case 'absent':
            url = `/ProductionData/LoadAbsenteeism?areaId=${areaId}`;
            title = "Registro de Ausentismo";
            break;
        case 'view':
            url = `/ProductionData/LoadViewData?areaId=${areaId}`;
            title = "Ver Datos de Producción";
            break;
    }

    if (url) {
        // Limpiar el contenido anterior para evitar conflictos
        $("#modalBody").empty();

        $.get(url, function (html) {
            $("#modalTitle").text(title);
            $("#modalBody").html(html);

            // Si es la vista de datos, hacer el modal más grande
            if (action === 'view') {
                $("#actionModal .modal-dialog").addClass("modal-xl");
            } else {
                $("#actionModal .modal-dialog").removeClass("modal-xl");
            }

            $("#actionModal").modal("show");

            // Para ver datos, inicializar DataTables después de que el modal esté visible
            if (action === 'view') {
                $("#actionModal").one('shown.bs.modal', function() {
                    console.log("📱 Modal de ver datos mostrado, inicializando DataTables...");

                    // Dar tiempo para que el modal se renderice completamente
                    setTimeout(() => {
                        if (typeof window.initializeViewDataModal === 'function') {
                            try {
                                window.initializeViewDataModal();
                                console.log("🎯 DataTables inicializado correctamente");
                            } catch (error) {
                                console.error("❌ Error al inicializar DataTables:", error);
                            }
                        } else {
                            console.warn("⚠️ Función initializeViewDataModal no encontrada");
                        }
                    }, 300); // Tiempo suficiente para el modal
                });

                // Limpiar DataTables cuando se cierre el modal
                $("#actionModal").one('hidden.bs.modal', function() {
                    if (typeof window.cleanupViewData === 'function') {
                        window.cleanupViewData();
                    }
                });
            }
        }).fail(function () {
            alert('Error al cargar el formulario');
        });
    }
};

$(document).ready(function () {
    // Event handlers para los botones de tablet
    $('.tablet-view .action-btn').on('click', function () {
        const action = $(this).data('action');
        console.log('Action clicked:', action); // Para debugging

        if (action) {
            window.openModal(action);
        }
    });

    // Limpiar modal al cerrarse para evitar conflictos de JavaScript
    $('#actionModal').on('hidden.bs.modal', function () {
        // Ejecutar función de limpieza si existe (para la implementación simple)
        if (typeof window.cleanupViewData === 'function') {
            window.cleanupViewData();
        }

        // Limpiar completamente el contenido del modal
        $("#modalBody").empty();
        $("#modalTitle").text('');

        // Remover clase modal-xl si se aplicó
        $("#actionModal .modal-dialog").removeClass("modal-xl");
    });
    
});
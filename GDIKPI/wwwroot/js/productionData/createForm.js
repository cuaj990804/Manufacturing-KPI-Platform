(function () {
    let productionId = null;
    let isUpdating = false; // Flag para prevenir llamadas duplicadas
    let isCreating = false; // Flag para prevenir creaciones duplicadas
    let requestTimeout = null; // Para debounce
    let lastRequestId = null; // Para cancelar peticiones anteriores
    let lastCreateAttempt = null; // Timestamp del último intento de creación
    let lastCreateData = null; // Datos del último intento para comparar

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

    // --- Helper para validar si hay datos suficientes ---
    function canCheckProduction() {
        const lineIdStr = $('#createProductionLine').val();
        const programIdStr = $('#ProgramProductionData').val();
        const date = $('#createProductionDate').val();
        const interval = $('#createTimeInterval').val();

        const lineId = lineIdStr;
        const programId = programIdStr && programIdStr.trim() !== "" && !isNaN(parseInt(programIdStr)) ? parseInt(programIdStr) : null;

        console.log("Debug canCheckProduction:", { lineIdStr, programIdStr, lineId, programId, date, interval });


        if (!lineId || !programId || !date || !interval) {
            console.log("Datos incompletos:", { lineId, programId, date, interval });
            return null;
        }

        const times = interval.split('-').map(t => t.trim());
        if (times.length !== 2) {
            console.log("Intervalo inválido:", interval);
            return null;
        }

        return {
            lineId,
            programId,
            date,
            startHour: times[0],
            endHour: times[1]
        };
    }

    // --- Función que verifica si existe registro con debounce ---
    function tryCheckProduction() {
        // Limpiar timeout anterior si existe
        if (requestTimeout) {
            clearTimeout(requestTimeout);
        }

        // Cancelar petición anterior si existe
        if (lastRequestId) {
            lastRequestId.abort();
            lastRequestId = null;
        }

        // Usar debounce de 300ms para evitar múltiples llamadas
        requestTimeout = setTimeout(async function() {
            const params = canCheckProduction();
            if (!params) {
                $('#CreateProductionContainer').empty();
                return;
            }

            $('#CreateProductionContainer').html(`<div class="text-center"><span>Verificando...</span></div>`);

            console.log("Enviando datos al backend:", params);

            lastRequestId = $.get('/api/ProductionDataApi/CheckIfProductionExists', params)
                .done(function (response) {
                productionId = response.exists ? response.id : null;

                if (response.exists) {
                    $('#CreateProductionContainer').html(`
                        <div class="text-warning mb-2">⚠ Registro de producción ya existente</div>
                        <div class="form-group">
                            <label for="createProducedPieces">Cantidad de Piezas Producidas</label>
                            <input type="number" id="createProducedPieces" name="createProducedPieces"
                                class="form-control" min="0"
                                placeholder="Ingresa la cantidad"
                                value="${response.producedPieces || ''}" required />
                        </div>
                        <div class="button-group mt-2">
                            <button type="button" id="updateProducePieces" class="btn btn-primary">Actualizar</button>
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
                        </div>
                    `);
                } else {
                    $('#CreateProductionContainer').html(`
                        <div class="form-group">
                            <label for="createProducedPieces">Cantidad de Piezas Producidas</label>
                            <input type="number" id="createProducedPieces" name="createProducedPieces" 
                                class="form-control" min="0" placeholder="Ingresa la cantidad" required />
                        </div>
                        <div class="button-group">
                            <button type="submit" class="btn btn-primary">Enviar</button>
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancelar</button>
                        </div>
                    `);
                }

                $('#AlertProductionContainer').empty();
                })
                .fail(function (xhr) {
                    if (xhr.statusText !== 'abort') {
                        alert("Error al verificar la existencia del registro. Intente nuevamente.");
                    }
                })
                .always(function() {
                    lastRequestId = null;
                });
        }, 300); // Debounce de 300ms
    }

    // --- Función para actualizar piezas producidas ---
    function updateProducedPieces() {
        // Prevenir llamadas duplicadas
        if (isUpdating) {
            console.log("Actualización ya en progreso, ignorando llamada duplicada");
            return;
        }

        const producedPieces = parseInt($('#createProducedPieces').val());
        if (isNaN(producedPieces) || producedPieces < 0) {
            showToast("Por favor ingresa una cantidad válida de piezas producidas", "warning");
            return;
        }

        isUpdating = true; // Marcar como actualizando

        // Deshabilitar botón durante la actualización
        $('#updateProducePieces').prop('disabled', true).text('Actualizando...');

        $.ajax({
            url: '/api/ProductionDataApi/UpdateProducedPieces',
            method: 'POST',
            data: {
                id: productionId,
                producedPieces: producedPieces
            },
            success: function () {
                showToast("Datos actualizados correctamente", "success");
                clearCreateForm("createProductionForm");
                productionId = null;

                if (productionTable) {
                    productionTable.ajax.reload(null, false);
                }
                $('#CreateProductionContainer').empty();
                // No llamar tryCheckProduction() aquí para evitar bucles
            },
            error: function (xhr) {
                showToast('Error al actualizar: ' + xhr.responseText, 'error');
            },
            complete: function () {
                isUpdating = false; // Liberar el flag sin importar si fue éxito o error
                // Rehabilitar botón
                $('#updateProducePieces').prop('disabled', false).text('Actualizar');
            }
        });
    }

    // --- Función para crear nuevo registro ---
    function createNewProduction() {
        // Prevenir creaciones duplicadas
        if (isCreating) {
            console.log("Creación ya en progreso, ignorando llamada duplicada");
            return;
        }

        // Generar ID único para esta petición
        const requestId = Date.now() + '_' + Math.random().toString(36).substr(2, 9);
        console.log("Iniciando creación con ID:", requestId);

        // Protección adicional contra doble clic rápido (menos de 2 segundos)
        const now = Date.now();
        if (lastCreateAttempt && (now - lastCreateAttempt) < 2000) {
            console.log("Intento de creación muy rápido, ignorando (< 2 segundos)");
            return;
        }
        lastCreateAttempt = now;

        const params = canCheckProduction();
        if (!params) {
            showToast("Campos incompletos: Selecciona línea, hora y programa", "warning");
            return;
        }

        // Obtener la descripción del programa seleccionado
        const programSelect = $('#ProgramProductionData');
        const programDescription = programSelect.find('option:selected').text();

        // Debug para verificar qué se está capturando
        console.log("Program ID:", params.programId);
        console.log("Program Description capturada:", programDescription);
        console.log("Opciones del select:", programSelect.find('option').map(function() { return {value: this.value, text: this.text}; }).get());

        const data = {
            ProductionLinesId: params.lineId,
            ProductionDate: params.date,
            StartHour: params.startHour,
            EndHour: params.endHour,
            ProducedPieces: parseInt($("#createProducedPieces").val()),
            RejectedPieces: 0,
            ProgramId: params.programId,
            ProgramDescription: programDescription && programDescription !== "Elige un programa" && programDescription.trim() !== "" ? programDescription : null
        };

        console.log("Datos enviados:", data);

        if (isNaN(data.ProducedPieces)) {
            showToast("Ingresa una cantidad de piezas válida", "warning");
            lastCreateAttempt = null; // Reset timestamp en caso de error
            return;
        }

        // Protección contra datos idénticos (SIN piezas producidas)
        const dataKey = JSON.stringify({
            line: data.ProductionLinesId,
            date: data.ProductionDate,
            start: data.StartHour,
            end: data.EndHour,
            program: data.ProgramId
        });

        if (lastCreateData === dataKey) {
            console.log("Datos idénticos al último intento, ignorando duplicado");
            lastCreateAttempt = null;
            return;
        }
        lastCreateData = dataKey;

        isCreating = true; // Marcar como creando

        // Deshabilitar botón de envío para prevenir múltiples clics (solo del formulario de producción)
        const submitBtn = $('#CreateProductionContainer button[type="submit"]');
        const originalText = submitBtn.text();
        submitBtn.prop('disabled', true).text('Guardando...');

        $.ajax({
            url: "/api/ProductionDataApi",
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify(data),
            timeout: 30000, // 30 segundos timeout
            success: function () {
                showToast("Datos guardados correctamente", "success");
                clearCreateForm("createProductionForm");
                if (productionTable) {
                    productionTable.ajax.reload(null, false);
                }
            },
            error: function (xhr) {
                if (xhr.status === 409) {
                    // Conflicto - registro duplicado
                    const response = xhr.responseJSON;
                    showToast(`Registro duplicado: ${response.message || 'Ya existe un registro con estos datos'}`, "warning");
                } else {
                    const message = xhr.responseJSON?.message || xhr.statusText || "Error desconocido";
                    showToast(`Error al guardar: ${message}`, "error");
                }
            },
            complete: function () {
                isCreating = false; // Liberar el flag sin importar si fue éxito o error

                // Rehabilitar botón
                submitBtn.prop('disabled', false).text(originalText);

                // Limpiar protecciones después de un tiempo
                setTimeout(() => {
                    lastCreateAttempt = null;
                    lastCreateData = null;
                }, 5000); // 5 segundos para permitir nuevos registros legítimos
            }
        });
    }

    // --- Eventos ---
    $(document).on('change', '#createProductionLine, #ProgramProductionData, #createProductionDate, #createTimeInterval', function () {
        if ($('#createProductionLine').val() && $('#ProgramProductionData').val() && $('#createProductionDate').val() && $('#createTimeInterval').val()) {
            tryCheckProduction();
        }
    });

    $(document).on("click", "#updateProducePieces", function (event) {
        event.preventDefault();
        updateProducedPieces();
    });

    $(document).on("submit", "#createProductionForm", function (event) {
        event.preventDefault();
        if (productionId !== null && productionId > 0) {
            updateProducedPieces();
        } else {
            createNewProduction();
        }
    });

    $(document).on("keypress", "#createProducedPieces", function (event) {
        if (event.which === 13) {
            event.preventDefault();
            if (productionId !== null && productionId > 0) {
                updateProducedPieces();
            } else {
                createNewProduction();
            }
        }
    });

})();

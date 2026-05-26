
$(document).on('change', '#absenteeismProductionLine', function () {
    const selectedLineId = $(this).val();

    if (selectedLineId) {
        $.get(`/api/AbsenteeismApi?lineid=${selectedLineId}`, function (data) {
            console.log("Respuesta del API:", data);

            // Función para generar el HTML base de las tarjetas
            function generateCardsHTML() {
                return `
                    <div class="row g-3">
                        <div class="col-md-6">
                            <div class="card h-100 border-0 shadow-sm border-start border-danger border-4">
                                <div class="card-body">
                                    <div class="d-flex align-items-center mb-3">
                                        <div>
                                            <h6 class="card-title mb-1 fw-bold text-uppercase">Ausencias</h6>
                                        </div>
                                    </div>
                                    <div class="input-group">
                                        <input type="number" id="absenceQuantity" name="absenceQuantity"
                                               class="form-control form-control-lg text-center fw-bold"
                                               min="0" max="999" aria-label="Cantidad de ausencias" >
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div class="col-md-6">
                            <div class="card h-100 border-0 shadow-sm border-start border-warning border-4">
                                <div class="card-body">
                                    <div class="d-flex align-items-center mb-3">
                                        <div>
                                            <h6 class="card-title mb-1 fw-bold text-uppercase">Permisos</h6>
                                        </div>
                                    </div>
                                    <div class="input-group">
                                        <input type="number" id="permissionQuantity" name="permissionQuantity"
                                               class="form-control form-control-lg text-center fw-bold"
                                               min="0" max="999" aria-label="Cantidad de permisos">
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div class="col-md-6">
                            <div class="card h-100 border-0 shadow-sm border-start border-dark border-4">
                                <div class="card-body">
                                    <div class="d-flex align-items-center mb-3">
                                        <div>
                                            <h6 class="card-title mb-1 fw-bold text-uppercase">Castigos</h6>
                                        </div>
                                    </div>
                                    <div class="input-group">
                                        <input type="number" id="punishmentQuantity" name="punishmentQuantity"
                                               class="form-control form-control-lg text-center fw-bold"
                                               min="0" max="999" aria-label="Cantidad de castigos">
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div class="col-md-6">
                            <div class="card h-100 border-0 shadow-sm border-start border-info border-4">
                                <div class="card-body">
                                    <div class="d-flex align-items-center mb-3">
                                        <div>
                                            <h6 class="card-title mb-1 fw-bold text-uppercase">Retardos</h6>
                                        </div>
                                    </div>
                                    <div class="input-group">
                                        <input type="number" id="delayQuantity" name="delayQuantity"
                                               class="form-control form-control-lg text-center fw-bold"
                                               min="0" max="999" aria-label="Cantidad de retardos" >
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div class="col-md-6">
                            <div class="card h-100 border-0 shadow-sm border-start border-secondary border-4">
                                <div class="card-body">
                                    <div class="d-flex align-items-center mb-3">
                                        <div>
                                            <h6 class="card-title mb-1 fw-bold text-uppercase">Incapacidades</h6>
                                        </div>
                                    </div>
                                    <div class="input-group">
                                        <input type="number" id="disabilityQuantity" name="disabilityQuantity"
                                               class="form-control form-control-lg text-center fw-bold"
                                               min="0" max="999" aria-label="Cantidad de incapacidades" >
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div class="col-md-6">
                            <div class="card h-100 border-0 shadow-sm border-start border-success border-4">
                                <div class="card-body">
                                    <div class="d-flex align-items-center mb-3">
                                        <div>
                                            <h6 class="card-title mb-1 fw-bold text-uppercase">Permisos Paternidad</h6>
                                        </div>
                                    </div>
                                    <div class="input-group">
                                        <input type="number" id="paternityLeaveQuantity" name="paternityLeaveQuantity"
                                               class="form-control form-control-lg text-center fw-bold"
                                               min="0" max="999" aria-label="Cantidad de permisos por paternidad" >
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="mt-4">
                        <div class="card border-0 bg-primary text-white shadow-sm">
                            <div class="text-center bg-primary text-white p-2 rounded shadow-sm mt-3">
                                <h6 class="fw-bold d-block">Total de Registros</h6>
                                <span id="totalCount" class="fw-bold fs-3">0</span>
                            </div>
                        </div>
                    </div>
                `;
            }

            // Función para generar el footer según el estado
            function generateFooterHTML(hasRecords) {
                const buttonText = hasRecords ? 'Actualizar Ausentismo' : 'Registrar Ausentismo';
                const buttonClass = hasRecords ? 'btn-warning' : 'btn-success';
                const buttonIcon = hasRecords ? 'fa-edit' : 'fa-save';

                return `
                    <div class="modal-footer bg-white border-top">
                        <button type="button" class="btn btn-secondary btn-lg" data-bs-dismiss="modal">
                            <i class="fas fa-times me-2"></i>
                            Cancelar
                        </button>
                        <button type="submit" class="btn ${buttonClass} btn-lg" id="saveBtn" data-has-records="${hasRecords}">
                            <span class="spinner-border spinner-border-sm d-none me-2" id="spinner"></span>
                            <i class="fas ${buttonIcon} me-2"></i>
                            ${buttonText}
                        </button>
                    </div>
                `;
            }

            // Función para llenar los campos con datos existentes
            function fillFormData(absenteeismData) {
                if (absenteeismData) {
                    $('#absenceQuantity').val(absenteeismData['Ausencias'] || 0);
                    $('#permissionQuantity').val(absenteeismData['Permisos'] || 0);
                    $('#punishmentQuantity').val(absenteeismData['Castigos'] || 0);
                    $('#delayQuantity').val(absenteeismData['Retardos'] || 0);
                    $('#disabilityQuantity').val(absenteeismData['Incapacidades'] || 0);
                    $('#paternityLeaveQuantity').val(absenteeismData['Paternidad'] || 0);
                }
            }

            // Generar HTML completo
            const cardsHTML = generateCardsHTML();
            const footerHTML = generateFooterHTML(data.hasRecords);
            const completeHTML = cardsHTML + footerHTML;

            // Insertar HTML en el contenedor
            $('#absenteeismContainer').html(completeHTML);

            // Si existen registros, llenar con datos existentes
            if (data.hasRecords && data.absenteeismData) {
                fillFormData(data.absenteeismData);
            }

            // Configurar eventos después de insertar el HTML
            setupFormEvents();

        }).fail(function (xhr) {
            console.error("Error al consultar el estado:", xhr.responseText);
            Swal.fire({
                icon: 'error',
                title: 'Error de Conexión',
                text: 'No se pudo consultar el estado de la línea de producción.',
                confirmButtonColor: '#d33'
            });
        });
    } else {
        // Limpiar el contenedor si no hay línea seleccionada
        $('#absenteeismContainer').empty();
    }
});

// Función para configurar eventos del formulario
function setupFormEvents() {
    const inputs = [
        "absenceQuantity",
        "permissionQuantity",
        "punishmentQuantity",
        "delayQuantity",
        "disabilityQuantity",
        "paternityLeaveQuantity"
    ];

    function updateTotal() {
        let total = 0;
        inputs.forEach(id => {
            const value = parseInt($(`#${id}`).val()) || 0;
            total += value;
        });
        $("#totalCount").text(total);
    }

    // Agregar event listeners para actualizar el total
    inputs.forEach(id => {
        $(`#${id}`).off('input').on('input', updateTotal);
    });

    // Calcular total inicial
    updateTotal();

    // Event listener para el botón de guardar
    $('#saveBtn').off('click').on('click', function (e) {
        e.preventDefault();

        // Validar formulario antes de enviar
        if (!validateAbsenteeismForm()) {
            return;
        }

        const hasRecords = $(this).data('has-records');
        const buttonText = hasRecords ? 'Actualizar' : 'Registrar';

        // Mostrar confirmación antes de guardar
        Swal.fire({
            title: `¿${buttonText} Ausentismo?`,
            text: `¿Estás seguro de que deseas ${buttonText.toLowerCase()} estos datos de ausentismo?`,
            icon: 'question',
            showCancelButton: true,
            confirmButtonColor: hasRecords ? '#f39c12' : '#28a745',
            cancelButtonColor: '#6c757d',
            confirmButtonText: `Sí, ${buttonText.toLowerCase()}`,
            cancelButtonText: 'Cancelar'
        }).then((result) => {
            if (result.isConfirmed) {
                // Proceder con el guardado
                performSaveAbsenteeism(hasRecords, buttonText);
            }
        });
    });
    function validateAbsenteeismForm() {
        const productionLineId = parseInt($('#absenteeismProductionLine').val());

        if (!productionLineId || productionLineId <= 0) {
            Swal.fire({
                icon: 'warning',
                title: 'Línea de Producción Requerida',
                text: 'Debes seleccionar una línea de producción válida.',
                confirmButtonColor: '#f39c12'
            });
            return false;
        }

        // Verificar que al menos una categoría tenga valor > 0
        const inputs = [
            "absenceQuantity",
            "permissionQuantity",
            "punishmentQuantity",
            "delayQuantity",
            "disabilityQuantity",
            "paternityLeaveQuantity"
        ];

        const hasData = inputs.some(id => {
            const value = parseInt($(`#${id}`).val()) || 0;
            return value > 0;
        });

        if (!hasData) {
            Swal.fire({
                icon: 'warning',
                title: 'Datos Requeridos',
                text: 'Debes ingresar al menos un valor mayor a 0 en alguna categoría.',
                confirmButtonColor: '#f39c12'
            });
            return false;
        }

        return true;
    }
    // Función separada para realizar el guardado
    function performSaveAbsenteeism(hasRecords, buttonText) {
        // Mostrar spinner
        $('#spinner').removeClass('d-none');
        $('#saveBtn').prop('disabled', true);

        // Recopilar los datos del formulario
        const formData = {
            ProductionLineId: parseInt($('#absenteeismProductionLine').val()),
            EmployeeNumber: 2856, // TODO: poner el real del usuario logueado
            Categories: [
                { Category: "Ausencias", CategoryQuantity: parseInt($('#absenceQuantity').val()) || 0 },
                { Category: "Permisos", CategoryQuantity: parseInt($('#permissionQuantity').val()) || 0 },
                { Category: "Castigos", CategoryQuantity: parseInt($('#punishmentQuantity').val()) || 0 },
                { Category: "Retardos", CategoryQuantity: parseInt($('#delayQuantity').val()) || 0 },
                { Category: "Incapacidades", CategoryQuantity: parseInt($('#disabilityQuantity').val()) || 0 },
                { Category: "Paternidad", CategoryQuantity: parseInt($('#paternityLeaveQuantity').val()) || 0 }
            ]
        };

        console.log(`${buttonText} datos:`, formData);

        // Realizar petición AJAX
        $.ajax({
            url: '/api/AbsenteeismApi/AbsenteeismCreate',
            method: 'POST',
            data: JSON.stringify(formData),
            contentType: 'application/json; charset=utf-8',
            dataType: 'json',
            timeout: 30000,
            success: function (response) {
                $('#spinner').addClass('d-none');
                $('#saveBtn').prop('disabled', false);

                $('#absenteeismModal').modal('hide');

                Swal.fire({
                    icon: 'success',
                    title: '¡Éxito!',
                    text: response.message || `Ausentismo ${buttonText.toLowerCase()} correctamente.`,
                    confirmButtonColor: '#28a745',
                    timer: 3000,
                    timerProgressBar: true
                }).then(() => {
                    $('#absenteeismProductionLine').trigger('change');

                    if (typeof refreshAbsenteeismTable === 'function') {
                        refreshAbsenteeismTable();
                    }
                });
            },
            error: function (xhr, status, error) {
                $('#spinner').addClass('d-none');
                $('#saveBtn').prop('disabled', false);

                let errorMessage = 'Error desconocido';

                if (xhr.responseJSON && xhr.responseJSON.message) {
                    errorMessage = xhr.responseJSON.message;
                } else if (xhr.responseText) {
                    try {
                        const errorResponse = JSON.parse(xhr.responseText);
                        errorMessage = errorResponse.message || errorMessage;
                    } catch (e) {
                        errorMessage = xhr.responseText;
                    }
                } else {
                    errorMessage = `Error ${xhr.status}: ${error}`;
                }

                console.error('Error completo:', {
                    status: xhr.status,
                    statusText: xhr.statusText,
                    responseText: xhr.responseText,
                    error: error
                });

                Swal.fire({
                    icon: 'error',
                    title: 'Error al Guardar',
                    text: `No se pudo ${buttonText.toLowerCase()} el ausentismo: ${errorMessage}`,
                    confirmButtonColor: '#d33',
                    footer: xhr.status === 400 ? 'Verifica que todos los datos sean válidos' :
                        xhr.status === 500 ? 'Error interno del servidor' : ''
                });
            }
        });
    }
}
$(document).on('change', '#DownTimeeProductionLine', function () {
    const selectedLineId = $(this).val();


    if (selectedLineId) {
        $.get(`/api/DownTimeApi?lineid=${selectedLineId}`, function (data) {
            console.log("Respuesta del API:", data);

            // Función para convertir fecha a formato compatible con datetime-local
            const formatDateForInput = (dateString) => {
                if (!dateString) return '';
                const date = new Date(dateString);
                const pad = n => n.toString().padStart(2, '0');
                const year = date.getFullYear();
                const month = pad(date.getMonth() + 1);
                const day = pad(date.getDate());
                const hours = pad(date.getHours());
                const minutes = pad(date.getMinutes());
                return `${year}-${month}-${day}T${hours}:${minutes}`;
            };


            if (data.hasOpen) {
                const html = `
                    <div class="mb-3">
                        <label for="OpenedBy" class="form-label">Abierto por (Número de Empleado)</label>
                        <input type="text" id="OpenedBy" name="OpenedBy" class="form-control" value="${data.openedBy || ''}" readonly />
                    </div>
                    <div class="mb-3">
                        <label for="StartTime" class="form-label">Hora de Inicio</label>
                        <input type="datetime-local" id="StartTime" name="StartTime" class="form-control" value="${formatDateForInput(data.startTime)}" readonly />
                    </div>
                    <div class="mb-3">
         
                            <label for="category" class="form-label">Categoría:</label>
                            <select id="category" name="category" class="form-select" disabled>
                                <option value="" ${!data.category ? 'selected' : ''}>Seleccione una categoría</option>
                                <option value="Falta de material" ${data.category === 'Falta de material' ? 'selected' : ''}>Falta de material</option>
                                <option value="Mantenimiento" ${data.category === 'Mantenimiento' ? 'selected' : ''}>Mantenimiento</option>
                                <option value="Producción" ${data.category === 'Producción' ? 'selected' : ''}>Producción</option>
                                <option value="Otros" ${data.category === 'Otros' ? 'selected' : ''}>Otros</option>
                            </select>
                
                    </div>
                    <div class="mb-3">
                        <label for="Reason" class="form-label">Razón</label>
                        <textarea class="form-control" id="Reason" name="Reason" rows="3" readonly>${data.reason || ''}</textarea>
                    </div>
                    <div class="mb-3">
                        <label for="EndTime" class="form-label">Hora de Fin</label>
                        <input type="datetime-local" id="EndTime" name="EndTime" class="form-control" />
                    </div>
                    <div class="mb-3">
                        <label for="ClosedBy" class="form-label">Cerrado por (Número de Empleado)</label>
                        <input type="text" id="ClosedBy" name="ClosedBy" class="form-control" />
                    </div>
                    <button type="button" id="closeDowntimeBtn" class="btn btn-danger">Cerrar Tiempo Muerto</button>
                `;
                $('#openStatusFields').html(html);
            } else {
                const now = new Date();
                const pad = n => n.toString().padStart(2, '0');

                const year = now.getFullYear();
                const month = pad(now.getMonth() + 1); // Los meses van de 0 a 11
                const day = pad(now.getDate());
                const hours = pad(now.getHours());
                const minutes = pad(now.getMinutes());

                const currentDateTime = `${year}-${month}-${day}T${hours}:${minutes}`;

                const html = `
                    <div class="mb-3">
                        <label for="StartTime" class="form-label">Hora de Inicio</label>
                        <input type="datetime-local" id="StartTime" name="StartTime" class="form-control" value="${currentDateTime}" />
                    </div>
                    <div class="mb-3">
                        <label for="category" class="form-label">Categoría:</label>
                        <select id="category" name="category" class="form-select">
                            <option value="">Seleccione una categoría</option>
                            <option value="Falta de material">Falta de material</option>
                            <option value="Mantenimiento">Mantenimiento</option>
                            <option value="Producción">Producción</option>
                            <option value="Otros">Otros</option>
                        </select>
                    </div>
                    

                    <div class="mb-3">
                        <label for="Reason" class="form-label">Razón</label>
                        <textarea class="form-control" id="Reason" name="Reason" rows="3"></textarea>
                    </div>
                    <button type="button" id="openDowntimeBtn" class="btn btn-success">Iniciar Tiempo Muerto</button>
                `;
                $('#openStatusFields').html(html);
            }
        }).fail(function (xhr) {
            console.error("Error al consultar el estado:", xhr.responseText);
            Swal.fire({
                icon: 'error',
                title: 'Error de Conexión',
                text: 'No se pudo consultar el estado de la línea de producción.',
                confirmButtonColor: '#d33'
            });
        });
    }
});

$(document).on('click', '#openDowntimeBtn', function () {
    const startTimeValue = $('#StartTime').val();
    const category = $('#category').val();
    const reason = $('#Reason').val();

    if (!startTimeValue) {
        Swal.fire({
            icon: 'warning',
            title: 'Campo Requerido',
            text: 'Por favor selecciona una fecha y hora de inicio.',
            confirmButtonColor: '#f39c12'
        });
        return;
    }

    if (!category) {
        Swal.fire({
            icon: 'warning',
            title: 'Campo Requerido',
            text: 'Por favor selecciona una categoría.',
            confirmButtonColor: '#f39c12'
        });
        return;
    }


    // Mostrar confirmación antes de proceder
    Swal.fire({
        title: '¿Confirmar Inicio de Tiempo Muerto?',
        text: `¿Estás seguro de que deseas iniciar el tiempo muerto para esta línea?`,
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#28a745',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Sí, iniciar',
        cancelButtonText: 'Cancelar'
    }).then((result) => {
        if (result.isConfirmed) {
            const data = {
                productionLinesId: parseInt($('#DownTimeeProductionLine').val()),
                startTime: startTimeValue,
                category: category,
                reason: reason.trim() || null
            };

            console.log("📤 Datos a enviar:", data);

            // Mostrar loading
            Swal.fire({
                title: 'Procesando...',
                text: 'Registrando tiempo muerto',
                allowOutsideClick: false,
                didOpen: () => {
                    Swal.showLoading();
                }
            });

            $.ajax({
                url: '/api/DownTimeApi/register',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(data),
                success: function (res) {
                    Swal.fire({
                        icon: 'success',
                        title: '¡Éxito!',
                        text: res.message || 'Tiempo muerto iniciado correctamente.',
                        confirmButtonColor: '#28a745'
                    }).then(() => {
                        location.reload();
                    });
                },
                error: function (xhr) {
                    console.error("❌ Error completo:", xhr);
                    console.error("❌ Status:", xhr.status);
                    console.error("❌ Response Text:", xhr.responseText);
                    console.error("❌ Response JSON:", xhr.responseJSON);

                    let errorMessage = "Error desconocido";

                    if (xhr.responseJSON && xhr.responseJSON.message) {
                        errorMessage = xhr.responseJSON.message;
                    } else if (xhr.responseJSON && xhr.responseJSON.errors) {
                        errorMessage = "Errores de validación: " + JSON.stringify(xhr.responseJSON.errors);
                    } else if (xhr.responseText) {
                        errorMessage = xhr.responseText;
                    }

                    Swal.fire({
                        icon: 'error',
                        title: 'Error al Registrar',
                        text: errorMessage,
                        confirmButtonColor: '#d33'
                    });
                }
            });
        }
    });
});

$(document).on('click', '#closeDowntimeBtn', function () {
    const endTimeValue = $('#EndTime').val();
    const closeByValue = $('#ClosedBy').val();

    if (!endTimeValue) {
        Swal.fire({
            icon: 'warning',
            title: 'Campo Requerido',
            text: 'Por favor selecciona una fecha y hora de fin.',
            confirmButtonColor: '#f39c12'
        });
        return;
    }

    if (!closeByValue.trim()) {
        Swal.fire({
            icon: 'warning',
            title: 'Campo Requerido',
            text: 'Por favor ingresa el número de empleado que cierra el tiempo muerto.',
            confirmButtonColor: '#f39c12'
        });
        return;
    }

    // Mostrar confirmación antes de proceder
    Swal.fire({
        title: '¿Confirmar Cierre de Tiempo Muerto?',
        text: `¿Estás seguro de que deseas cerrar el tiempo muerto?`,
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#dc3545',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Sí, cerrar',
        cancelButtonText: 'Cancelar'
    }).then((result) => {
        if (result.isConfirmed) {
            const data = {
                productionLinesId: parseInt($('#DownTimeeProductionLine').val()),
                endTime: endTimeValue,
                closedBy: closeByValue.trim()
            };



            // Mostrar loading
            Swal.fire({
                title: 'Procesando...',
                text: 'Cerrando tiempo muerto',
                allowOutsideClick: false,
                didOpen: () => {
                    Swal.showLoading();
                }
            });

            $.ajax({
                url: '/api/DownTimeApi/close',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(data),
                success: function (res) {
                    Swal.fire({
                        icon: 'success',
                        title: '¡Éxito!',
                        text: res.message || 'Tiempo muerto cerrado correctamente.',
                        confirmButtonColor: '#28a745'
                    }).then(() => {
                        location.reload();
                    });
                },
                error: function (xhr) {
                    console.error("❌ Error completo:", xhr);
                    let errorMessage = "Error desconocido";

                    if (xhr.responseJSON && xhr.responseJSON.message) {
                        errorMessage = xhr.responseJSON.message;
                    } else if (xhr.responseText) {
                        errorMessage = xhr.responseText;
                    }

                    Swal.fire({
                        icon: 'error',
                        title: 'Error al Cerrar',
                        text: errorMessage,
                        confirmButtonColor: '#d33'
                    });
                }
            });
        }
    });
});
$('#AddAreaForm').on('submit', function (e) {
    e.preventDefault();
    const areaName = $('#areaName').val().trim();
    const customerName = $('#customerName').val().trim();
    $.ajax({
        url: '/api/AreaApi/CreateArea',
        method: 'POST',
        data: { areaName, customerName },
        success: function (response) {
            Swal.fire('Área creada', 'Se ha agregado el área correctamente.', 'success');
            // Agregar al dropdown
            const newOption = new Option(response.displayName, response.areaId, true, true);
            $('#area').append(newOption).trigger('change');
            $('#AddAreaForm')[0].reset();
            $('#AddAreaModal').modal('hide');
            //bootstrap.Modal.getInstance(document.getElementById('AddAreaModal')).hide();
        },
        error: function (xhr) {
            Swal.fire('Error', xhr.responseText, 'error');
        }
    });
});

$('#LineProductionForm').on('submit', function (e) {
    e.preventDefault();

    // Validar que se haya seleccionado un supervisor
    if (!$('#supervisor').val()) {
        Swal.fire('Error de validación', 'Debe seleccionar un supervisor.', 'warning');
        return;
    }

    // Obtener los permisos seleccionados
    const selectedPermissions = [];
    $('input[name="permissions"]:checked').each(function () {
        selectedPermissions.push(parseInt($(this).val()));
    });

    // Validar que se haya seleccionado al menos un permiso
    if (selectedPermissions.length === 0) {
        Swal.fire('Error de validación', 'Debe seleccionar al menos un permiso para el supervisor.', 'warning');
        return;
    }

    const formData = {
        areaId: $('#area').val(),
        lineNumber: $('#createlineNumber').val(),
        lineName: $('#lineName').val(),
        dailyGoal: $('#dailyGoal').val(),
        personalQuantity: $('#peopleqty').val(),
        break1Start: $('#break1Start').val(),
        break1End: $('#break1End').val(),
        break2Start: $('#break2Start').val(),
        break2End: $('#break2End').val(),
        supervisor: $('#supervisor').val(),
        permissions: selectedPermissions
    };

    $.ajax({
        url: '/api/AreaApi/CreateProductionLine',
        method: 'POST',
        data: formData,
        success: function (response) {
            const permissionsText = selectedPermissions.length === 1 ? 'permiso' : 'permisos';
            const successMessage = `La línea fue guardada correctamente con ${selectedPermissions.length} ${permissionsText} asignados al supervisor.`;

            Swal.fire({
                title: '¡Línea creada!',
                text: successMessage,
                icon: 'success',
                confirmButtonText: 'Aceptar'
            });

            $('#LineProductionForm')[0].reset();
            $('#DataTable').DataTable().ajax.reload();

            // Opcional: Cerrar el modal después del éxito
            // $('#ProductionLinesModal').modal('hide');
        },
        error: function (xhr) {
            let errorMessage = 'Error al crear la línea de producción.';

            // Intentar obtener el mensaje de error del servidor
            if (xhr.responseJSON && xhr.responseJSON.message) {
                errorMessage = xhr.responseJSON.message;
            } else if (xhr.responseText) {
                errorMessage = xhr.responseText;
            }

            Swal.fire({
                title: 'Error',
                text: errorMessage,
                icon: 'error',
                confirmButtonText: 'Aceptar'
            });
        }
    });
});

$('#area').on('change', function () {
    if ($(this).val() === 'add-new') {
        // Resetea el valor para que no se quede seleccionado
        $(this).val('');
        // Muestra el modal para agregar una nueva área
        $('#AddAreaModal').modal('show');
    }
});

// Función opcional para manejar la selección/deselección de todos los permisos
function toggleAllPermissions(selectAll) {
    $('input[name="permissions"]').prop('checked', selectAll);
}

// Función opcional para mostrar información sobre los permisos seleccionados
function showPermissionsSummary() {
    const selectedPermissions = [];
    const permissionNames = {
        2: 'Crear',
        3: 'Actualizar',
        4: 'Eliminar'
    };

    $('input[name="permissions"]:checked').each(function () {
        const permissionId = parseInt($(this).val());
        selectedPermissions.push(permissionNames[permissionId]);
    });

    if (selectedPermissions.length > 0) {
        const message = `Permisos seleccionados: ${selectedPermissions.join(', ')}`;
        console.log(message);
        return message;
    }

    return 'No hay permisos seleccionados';
}

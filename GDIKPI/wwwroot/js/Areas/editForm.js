$(function () {
    // Verificar que los datos estén disponibles
    if (!window.EditProductionLineData) {
        console.error('EditProductionLineData no está disponible');
        return;
    }

    const data = window.EditProductionLineData;

    // Llenar campos al cargar el modal
    $('#editAreaSelect').val(data.model.areaId);
    $('#editAreaId').val(data.model.areaId);
    $('#editLineNumber').val(data.model.lineNumber);
    $('#editLineName').val(data.model.lineName);
    $('#editOriginalLineNumber').val(data.model.lineNumber);
    $('#editPeopleQty').val(data.model.personalQuantity);
    $('#editDailyGoal').val(data.model.dailyGoal);
    $('#editStandardTime').val(data.model.standardTime);
    $('#editBreak1Start').val(data.viewBag.break1Start);
    $('#editBreak1End').val(data.viewBag.break1End);
    $('#editBreak2Start').val(data.viewBag.break2Start);
    $('#editBreak2End').val(data.viewBag.break2End);
    $('#editIsActive').prop('checked', data.model.isActive);

    // Seleccionar supervisor actual
    if (data.viewBag.supervisor) {
        $('#supervisorUpdate').val(data.viewBag.supervisor);
    }

    // Marcar permisos asignados
    if (data.viewBag.assignedPermissions && Array.isArray(data.viewBag.assignedPermissions)) {
        data.viewBag.assignedPermissions.forEach(function (permissionId) {
            if (permissionId !== 1) {
                $('input[name="permissions"][value="' + permissionId + '"]').prop('checked', true);
            }
        });
    }

    // Mostrar el modal
    const modal = new bootstrap.Modal(document.getElementById('EditProductionLineModal'));
    modal.show();
});

// Manejar el envío del formulario
$('#EditLineProductionForm').on('submit', function (e) {
    e.preventDefault();

    // Recoger permisos seleccionados (excluyendo el básico que se asigna automáticamente)
    var selectedPermissions = [];
    $('input[name="permissions"]:checked').each(function () {
        selectedPermissions.push(parseInt($(this).val()));
    });

    const data = {
        areaId: $('#editAreaId').val(),
        originalLineNumber: $('#editOriginalLineNumber').val(),
        lineNumber: $('#editLineNumber').val(),
        lineName: $('#editLineName').val(),
        dailyGoal: $('#editDailyGoal').val(),
        personalQuantity: $('#editPeopleQty').val(),
        standardTime: $('#editStandardTime').val() === '' ? null : parseFloat($('#editStandardTime').val()),
        break1Start: $('#editBreak1Start').val(),
        break1End: $('#editBreak1End').val(),
        break2Start: $('#editBreak2Start').val(),
        break2End: $('#editBreak2End').val(),
        supervisorEmployeeNumber: $('#supervisorUpdate').val(),
        permissions: selectedPermissions,
        isActive: $('#editIsActive').is(':checked')
    };



    // Enviar datos al servidor
    $.ajax({
        url: '/api/AreaApi/UpdateProductionLine',
        type: 'PUT',
        contentType: 'application/json',
        data: JSON.stringify(data),
        success: function (response) {
            if (response.success) {
                $('#EditProductionLineModal').modal('hide');

                // Opcional: mostrar mensaje de éxito
                $('#DataTable').DataTable().ajax.reload();
                Swal.fire('Área creada', 'Se ha actualizado correctamente.', 'success');
            } else {
                Swal.fire('Error', response.message || 'No se pudo actualizar', 'error');
            }
        },

        error: function (xhr, status, error) {
            let errorMessage = 'Error de conexión';

            if (xhr.responseJSON && xhr.responseJSON.message) {
                errorMessage = xhr.responseJSON.message;
            }

            
        }
    });
});

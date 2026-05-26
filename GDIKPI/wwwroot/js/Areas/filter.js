let table; // variable global para guardar la instancia

// Solo crear/recargar tabla al enviar el formulario
$('#filterAreaForm').on('submit', function (e) {
    e.preventDefault();

  

    table = $('#DataTable').DataTable({
        processing: true,
        serverSide: true,
        ajax: {
            url: '/api/AreaApi/GetFilter', // Ajusta a tu endpoint
            type: 'POST',
            data: function (d) {
                var formData = $('#filterAreaForm').serializeArray();
                formData.forEach(function (item) {
                    d[item.name] = item.value;
                });
            }
        }
    });

    $('#filterAreaModal').modal('hide');
});

$(document).ready(function () {
    let areaId = $('#AreaIdHidden').val();
    let customerFilter = $('#CustomerFilter').val() || ''; // Filtro opcional por cliente
    let filterParams = {
        areaName: '',
        customerName: '',
        lineNumber: '',
        dailyGoal: '',
        staffCount: '',
        descanso1: '',
        descanso2: '',
        supervisor: ''
    };

    productionLineTable = $('#DataTable').DataTable({
        ordering: false,  // Habilitamos ordenamiento para esta vista
        searching: false, // Habilitamos búsqueda para esta vista
        dom: 'Bfrtip',
        order: [[0, "asc"]], // Ordenar por customer name por defecto
        processing: true,
        serverSide: true,
        ajax: {
            url: '/api/AreaApi/SetTable',
            type: 'POST',
            data: function (d) {
                return $.extend({}, d, filterParams);
            }
        },
        columns: [
            { data: 'customerName' },
            { data: 'areaName' },
            {
                data: 'lineNumber',
                className: 'text-center'
            },
            {
                data: 'dailyGoal',
                className: 'text-center',
                render: data => data ? data.toLocaleString() : '0'
            },
            {
                data: 'personalQuantity',
                className: 'text-center',
                render: data => data ? data.toLocaleString() : '0'
            },
            {
                data: 'descansos',
                render: function (data, type, row) {
                    const descansos = (data || '').split(',');
                    return descansos[0]?.trim() || '<span class="text-muted">—</span>';
                }
            },
            {
                data: 'descansos',
                render: function (data, type, row) {
                    const descansos = (data || '').split(',');
                    return descansos[1]?.trim() || '<span class="text-muted">—</span>';
                }
            },
            {
                data: 'supervisor',
                className: 'text-center'
            },
            {
                data: null,
                defaultContent: '',
                className: 'text-center',
                render: function (data, type, row) {
                    return `
                        <button class="btn btn-sm btn-warning edit-line-btn"
                            data-line="${row.lineNumber}"
                            data-area="${row.areaName}"
                            data-customer="${row.customerName}">
                            Editar
                        </button>
                    `;
                }
            }
        ],
        buttons: [
            {
                text: 'Filtrar',
                className: 'btn btn-primary',
                action: function () {
                    $("#filterAreaModal").modal("show");
                }
            },
            {
                text: 'Agregar Linea de Producción',
                className: 'btn btn-primary',
                action: function () {
                    $("#ProductionLinesModal").modal("show");
                }
            },
            {
                text: 'Editar Área',
                className: 'btn btn-primary edit-area-btn',
                action: function () {
                    $.get(`/Area/LoadEditAreaForm`, function (html) {
                        $('#editAreaModalBody').html(html);
                        $("#editAreaModal").modal("show");
                    });
                }
            }
        ]
    });
    $('#filterAreaForm').on('submit', function (e) {
        e.preventDefault();

        filterParams.areaNameFilter = $('#filterAreaForm select[name="areaNameFilter"]').val();
        filterParams.customerNameFilter = $('#filterAreaForm select[name="customerNameFilter"]').val();
        filterParams.lineNumber = $('#filterAreaForm input[name="lineNumber"]').val();
        filterParams.dailyGoal = $('#filterAreaForm input[name="dailyGoal"]').val();
        filterParams.staffCount = $('#filterAreaForm input[name="staffCount"]').val();
        filterParams.supervisorFilter = $('#filterAreaForm select[name="supervisorFilter"]').val();

        productionLineTable.ajax.reload();
        $('#filterAreaModal').modal('hide');
    });
    $('#ClearFilter').click(function (e) {
        e.preventDefault();
        $('#filterAreaForm')[0].reset();

    });


    // Event handler para el botón de editar línea
    $('#DataTable').on('click', '.edit-line-btn', function () {
        const lineNumber = $(this).data('line');
        const area = $(this).data('area');
        const customerName = $(this).data('customer');

        $.get(`/Area/LoadEditForm`, {
            lineNumber: lineNumber,
            area: area,
            customer: customerName
        }, function (html) {
            $('#EditProductionLineModalBody').html(html);
        });
    });

    
});

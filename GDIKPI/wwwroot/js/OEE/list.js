$(document).ready(function () {
    // Fecha de hoy en formato yyyy-MM-dd (el que usan los inputs type="date")
    let today = new Date().toISOString().split('T')[0];
    let currentOEEData = {};

    // Si no tienen valor, les asignamos hoy
    if (!$('#startDateFilter').val()) {
        $('#startDateFilter').val(today);
    }

    if (!$('#endDateFilter').val()) {
        $('#endDateFilter').val(today);
    }
    // Función para obtener la clase Bootstrap del badge según el tipo
    function getTipoBadgeClass(tipo) {
        switch (tipo?.toLowerCase()) {
            case 'downtime':
            case 'tiempo muerto':
                return 'badge bg-danger'; // Rojo para downtime
            case 'absenteeism':
            case 'ausentismo':
                return 'badge bg-dark'; // Oscuro para ausentismo
            case 'maintenance':
            case 'mantenimiento':
                return 'badge bg-info text-dark'; // Azul para mantenimiento
            case 'sin incidencias':
                return 'badge bg-success'; // Verde para sin incidencias
            default:
                return 'badge bg-secondary';
        }
    }

    // Función para obtener la clase Bootstrap del badge según el estado
    function getEstadoBadgeClass(estado) {
        switch (estado?.toLowerCase()) {
            case 'open':
            case 'abierto':
                return 'badge bg-danger'; // Rojo para abierto
            case 'in progress':
            case 'en proceso':
            case 'progress':
                return 'badge bg-warning text-dark'; // Amarillo para en proceso
            case 'closed':
            case 'cerrado':
                return 'badge bg-success'; // Verde para cerrado
            default:
                return 'badge bg-secondary';
        }
    }

    // Función para obtener el texto del estado en español
    function getEstadoTexto(estado) {
        switch (estado?.toLowerCase()) {
            case 'open':
                return 'Abierto';
            case 'in progress':
            case 'progress':
                return 'En Proceso';
            case 'closed':
                return 'Cerrado';
            default:
                return estado || 'Desconocido';
        }
    }

    // Función para obtener el texto del tipo en español
    function getTipoTexto(tipo) {
        switch (tipo?.toLowerCase()) {
            case 'downtime':
                return 'Downtime';
            case 'absenteeism':
                return 'Ausentismo';
            case 'maintenance':
                return 'Mantenimiento';
            case 'sin incidencias':
                return 'Sin Incidencias';
            default:
                return tipo || 'Desconocido';
        }
    }

    // Función para agregar efectos adicionales si los necesitas
    function getExtraClasses(tipo, estado) {
        let extraClasses = 'rounded-pill'; // Para badges más redondeados

        // Agregar animación para estados críticos
        if (estado?.toLowerCase() === 'open') {
            extraClasses += ' pulse-animation'; // Puedes agregar esta clase CSS si quieres animación
        }

        return extraClasses;
    }
    function applyKPIColor(kpiId, metricName, value) {
        const numericValue = parseFloat(value);

        $.get(`/api/OEEApi/GetCategory?metricName=${metricName}`)
            .done(function (params) {
                let colorClass = 'text-secondary';
                for (let p of params) {
                    const min = parseFloat(p.minValue);
                    const max = parseFloat(p.maxValue);

                    if (numericValue >= min && numericValue <= max) {
                        switch (p.category.toLowerCase()) {
                            case 'malo': colorClass = 'text-danger'; break;
                            case 'bueno': colorClass = 'text-warning'; break;
                            case 'excelente': colorClass = 'text-success'; break;
                        }
                        break;
                    }
                }

                $(`#${kpiId}`)
                    .text(numericValue + '%')
                    .removeClass('text-danger text-warning text-success')
                    .addClass(colorClass);
            })
            .fail(function () {
                $(`#${kpiId}`).text(value + '%');
            });
    }



    function loadKPIs() {
        //$('#loadingIndicator').removeClass('d-none'); // mostrar indicador

        $.ajax({
            url: '/api/OEEApi/CalculateOEE',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                downTimeFilter: $('#downTimeFilter').val(),
                areaFilter: $('#areaFilter').val(),
                lineFilter: $('#lineFilter').val(), // sigue siendo string, el controller lo parsea
                descriptionFilter: $('#descriptionFilter').val(),
                statusFilter: $('#statusFilter').val(),
                startDateFilter: $('#startDateFilter').val(),
                endDateFilter: $('#endDateFilter').val()
            }),
            success: function (response) {
                currentOEEData = response;
                applyKPIColor('oeeTotal', 'Overall', response.oeeTotal);
                applyKPIColor('availability', 'Availability', response.availability);
                applyKPIColor('performance', 'Performance', response.performance);
                applyKPIColor('quality', 'Quality', response.quality);

                $('#oeeTotal').text(response.oeeTotal + '%');
                $('#availability').text(response.availability + '%');
                $('#performance').text(response.performance + '%');
                $('#quality').text(response.quality + '%');

                

                $('#totalDowntime').text(response.totalDowntime);
                $('#totalAbsenteeism').text(response.totalAbsenteeism);
                $('#totalRecords').text(response.totalRecords);
            },
            error: function (xhr, status, error) {
                console.error('Error al cargar KPIs:', error, xhr.responseText);
                $('#oeeTotal, #availability, #performance, #quality').text('Error');
                $('#totalDowntime, #totalAbsenteeism, #totalRecords').text('--');
            },
            complete: function () {
                //$('#loadingIndicator').addClass('d-none'); // ocultar indicador
            }
        });
    }


    productionLineTable = $('#DataTable').DataTable({
        ordering: false,  // Habilitamos ordenamiento para esta vista
        searching: false, // Habilitamos búsqueda para esta vista
        dom: 'frtip',
        order: [[0, "asc"]], // Ordenar por customer name por defecto
        processing: true,
        serverSide: true,
        ajax: {
            url: '/api/OEEApi/SetTable',
            type: 'POST',
            data: function (d) {
                d.downTimeFilter = $('#downTimeFilter').val();
                d.areaFilter = $('#areaFilter').val();
                d.lineFilter = $('#lineFilter').val();
                d.descriptionFilter = $('#descriptionFilter').val();
                d.statusFilter = $('#statusFilter').val();
                d.startDateFilter = $('#startDateFilter').val();
                d.endDateFilter = $('#endDateFilter').val();
            }
        },
        columns: [
            {
                data: 'type',
                render: function (data, type, row) {
                    if (type === 'display') {
                        const badgeClass = getTipoBadgeClass(data);
                        const tipoTexto = getTipoTexto(data);
                        const extraClasses = getExtraClasses(data, null);

                        return `<span class="${badgeClass} ${extraClasses}">${tipoTexto}</span>`;
                    }
                    return data;
                }
            },
            { data: 'area' },
            { data: 'lineNumber', className: 'text-center' },
            { data: 'reportDate', render: d => d ? d.split('T')[0].split('-').reverse().join('/') : '—' },
            { data: 'category' },
            {
                data: 'status',
                render: function (data, type, row) {
                    if (type === 'display') {
                        const badgeClass = getEstadoBadgeClass(data);
                        const estadoTexto = getEstadoTexto(data);
                        const extraClasses = getExtraClasses(null, data);

                        return `<span class="${badgeClass} ${extraClasses}">${estadoTexto}</span>`;
                    }
                    return data;
                }
            },
            { data: 'total', className: 'text-center' }

        ],

    });

    
    $('#btnParameters').on('click', function () {
        $.get('/OEE/AddOEEParamModal', function (html) {
            // Insertamos el partial dentro del modal
            $('#AddOEEParamModal .modal-content').html(html);
            // Mostramos el modal
            $('#AddOEEParamModal').modal('show');
        });
    });

    $('#areaFilter').on('change', function () {
        const selectedArea = $(this).val();
        const $lineFilter = $('#lineFilter');
        $lineFilter.empty(); // Limpiar líneas

        if (selectedArea) {
            $.get(`/OEE/GetLinesByArea?area=${encodeURIComponent(selectedArea)}`, function (lines) {
                $lineFilter.empty();
                lines.forEach(line => {
                    $lineFilter.append(`<option value="${line.value}">${line.text}</option>`);
                });
            });
        }

        // Recargar KPIs y tabla cuando cambie el área
        loadKPIs();
        productionLineTable.ajax.reload();
    });


    $('#downTimeFilter, #areaFilter, #lineFilter, #descriptionFilter, #statusFilter, #startDateFilter, #endDateFilter')
        .on('change keyup', function () {
            loadKPIs();
            productionLineTable.ajax.reload();
        });

    $('#downTimeFilter, #areaFilter, #lineFilter, #descriptionFilter, #statusFilter, #startDateFilter, #endDateFilter')
        .on('change keyup', function () {
            loadKPIs();
            productionLineTable.ajax.reload();
        });
    $('#btnExportar').on('click', function () {
        Swal.fire({
            title: 'Generando archivo...',
            text: 'Por favor espere un momento',
            allowOutsideClick: false,
            allowEscapeKey: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });

        let params = {
            downTimeFilter: $('#downTimeFilter').val(),
            areaFilter: $('#areaFilter').val(),
            lineFilter: $('#lineFilter').val(),
            descriptionFilter: $('#descriptionFilter').val(),
            statusFilter: $('#statusFilter').val(),
            startDateFilter: $('#startDateFilter').val(),
            endDateFilter: $('#endDateFilter').val(),
            oeeTotal: currentOEEData.oeeTotal || 0,
            availability: currentOEEData.availability || 0,
            performance: currentOEEData.performance || 0,
            quality: currentOEEData.quality || 0,
            totalOperatingTime: currentOEEData.totalOperatingTime || 0,
            totalPieces: currentOEEData.totalPieces || 0,
            totalRejects: currentOEEData.totalRejects || 0,
            totalGoal: currentOEEData.totalGoal || 0,
            totalDowntime: currentOEEData.totalDowntime || 0
        };

        let query = $.param(params);

        // Descargar con iframe oculto
        $('<iframe/>').attr({
            src: '/api/OEEApi/ExportOEEToExcel?' + query,
            style: 'display:none;'
        }).appendTo('body');

        // Cerrar el SweetAlert tras unos segundos
        setTimeout(() => {
            Swal.close();
        }, 4000); // ajusta según lo que tarde tu backend
    });


    loadKPIs();

    

});
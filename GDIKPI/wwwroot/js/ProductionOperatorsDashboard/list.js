$(document).ready(function () {
    const today = new Date().toISOString().split('T')[0];

    if (!$('#startDateFilter').val()) {
        $('#startDateFilter').val(today);
    }

    if (!$('#endDateFilter').val()) {
        $('#endDateFilter').val(today);
    }

    let scansChartInstance = null;
    let historyChartInstance = null;
    let isExporting = false;
    let employeeSuggestionsTimer = null;
    const isTabletViewport = window.matchMedia('(min-width: 768px) and (max-width: 1180px)').matches;
    const employeeSuggestionsList = $('#employeeFilterOptions');

    function renderEmployeeSuggestions(items) {
        if (!employeeSuggestionsList.length) {
            return;
        }

        employeeSuggestionsList.empty();

        (items || []).forEach(item => {
            const label = item.label || '';
            if (!label) {
                return;
            }

            $('<option>', { value: label }).appendTo(employeeSuggestionsList);
        });
    }

    function loadEmployeeSuggestions() {
        const term = ($('#employeeFilter').val() || '').trim();

        fetch(`/api/ProductionOperatorsDashboardApi/operators?term=${encodeURIComponent(term)}`)
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(renderEmployeeSuggestions)
            .catch(() => renderEmployeeSuggestions([]));
    }

    function loadSummary() {
        fetch('/api/ProductionOperatorsDashboardApi/summary', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(getCurrentFilters())
        })
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(summary => {
                $('#totalScansMetric').text(summary.totalScans || 0);
                $('#uniqueCodesMetric').text(summary.uniqueCodes || 0);
                $('#uniqueOperatorsMetric').text(summary.uniqueOperators || 0);
                $('#operationsMetric').text(summary.operations || 0);
            })
            .catch(() => {
                $('#totalScansMetric').text('0');
                $('#uniqueCodesMetric').text('0');
                $('#uniqueOperatorsMetric').text('0');
                $('#operationsMetric').text('0');
            });
    }

    function loadChart() {
        const canvas = document.getElementById('operatorsScansChart');

        if (!canvas || typeof Chart === 'undefined') {
            return;
        }

        fetch('/api/ProductionOperatorsDashboardApi/chart', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(getCurrentFilters())
        })
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(response => {
                if (scansChartInstance) {
                    scansChartInstance.destroy();
                }

                var maxVal = Math.max(...(response.totals || [0]));
                var labelPadding = Math.max(30, maxVal.toString().length * 9 + 20);

                scansChartInstance = new Chart(canvas.getContext('2d'), {
                    type: 'bar',

                    data: {
                        labels: response.labels || [],
                        datasets: [
                            {
                                label: 'Piezas',
                                data: response.totals || [],
                                backgroundColor: 'rgba(37, 99, 235, 0.65)',
                                borderColor: 'rgba(37, 99, 235, 1)',
                                borderWidth: 1,
                                borderRadius: 6
                            }
                        ]
                    },

                    options: {
                        indexAxis: 'y',

                        responsive: true,
                        maintainAspectRatio: false,

                        layout: {
                            padding: {
                                right: labelPadding
                            }
                        },

                        plugins: {
                            legend: {
                                display: false
                            },

                            title: {
                                display: true,
                                text: response.chartTitle || 'Produccion por operador'
                            },

                            tooltip: {
                                callbacks: {
                                    label: function (context) {
                                        return `${context.raw} piezas`;
                                    }
                                }
                            }
                        },

                        scales: {
                            x: {
                                beginAtZero: true,
                                ticks: {
                                    precision: 0
                                }
                            },

                            y: {
                                ticks: {
                                    autoSkip: false,
                                    font: {
                                        size: 11
                                    }
                                }
                            }
                        }
                    },

                    plugins: [{
                        id: 'barLabels',
                        afterDraw: function (chart) {
                            var ctx = chart.ctx;
                            chart.data.datasets.forEach(function (ds, i) {
                                var meta = chart.getDatasetMeta(i);
                                meta.data.forEach(function (el, idx) {
                                    var val = ds.data[idx];
                                    if (!val) return;
                                    ctx.save();
                                    ctx.fillStyle = '#1e40af';
                                    ctx.font = 'bold 12px Arial';
                                    ctx.textAlign = 'left';
                                    ctx.textBaseline = 'middle';
                                    ctx.fillText(val.toString(), el.x + 6, el.y);
                                    ctx.restore();
                                });
                            });
                        }
                    }]
                });
            })
            .catch(() => {
                if (scansChartInstance) {
                    scansChartInstance.destroy();
                    scansChartInstance = null;
                }
            });
    }

    function loadHistoryChart() {
        var canvas = document.getElementById('historyChart');

        if (!canvas || typeof Chart === 'undefined') {
            return;
        }

        fetch('/api/ProductionOperatorsDashboardApi/chartHistory', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(getCurrentFilters())
        })
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(function (response) {
                if (historyChartInstance) {
                    historyChartInstance.destroy();
                }

                var isHourly = response.labels && response.labels.length > 0 && response.labels[0].indexOf(':') >= 0;
                var gradient = canvas.getContext('2d').createLinearGradient(0, 0, 0, 340);
                gradient.addColorStop(0, 'rgba(20, 184, 166, 0.3)');
                gradient.addColorStop(1, 'rgba(20, 184, 166, 0.01)');

                historyChartInstance = new Chart(canvas.getContext('2d'), {
                    type: isHourly ? 'bar' : 'line',
                    data: {
                        labels: response.labels || [],
                        datasets: [
                            {
                                label: 'Piezas',
                                data: response.totals || [],
                                backgroundColor: isHourly ? 'rgba(20, 184, 166, 0.65)' : gradient,
                                borderColor: 'rgba(20, 184, 166, 1)',
                                borderWidth: isHourly ? 1 : 2,
                                borderRadius: isHourly ? 4 : 0,
                                fill: isHourly ? false : true,
                                tension: 0.3,
                                pointBackgroundColor: 'rgba(20, 184, 166, 1)',
                                pointRadius: isHourly ? 0 : 3,
                                pointHoverRadius: 5
                            }
                        ]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: {
                                display: false
                            },
                            title: {
                                display: true,
                                text: response.chartTitle || 'Historial de escaneos'
                            },
                            tooltip: {
                                callbacks: {
                                    label: function (context) {
                                        return context.raw + ' piezas';
                                    }
                                }
                            }
                        },
                        scales: {
                            x: {
                                ticks: {
                                    maxRotation: isHourly ? 0 : 45,
                                    font: { size: 10 }
                                }
                            },
                            y: {
                                beginAtZero: true,
                                ticks: {
                                    precision: 0
                                }
                            }
                        }
                    },
                    plugins: [{
                        id: 'historyLabels',
                        afterDraw: function (chart) {
                            var ctx = chart.ctx;
                            chart.data.datasets.forEach(function (ds, i) {
                                var meta = chart.getDatasetMeta(i);
                                meta.data.forEach(function (el, idx) {
                                    var val = ds.data[idx];
                                    if (!val) return;
                                    ctx.save();
                                    ctx.fillStyle = '#115e59';
                                    ctx.font = 'bold 11px Arial';
                                    ctx.textAlign = 'center';
                                    ctx.textBaseline = 'bottom';
                                    ctx.fillText(val.toString(), el.x, el.y - 5);
                                    ctx.restore();
                                });
                            });
                        }
                    }]
                });
            })
            .catch(function () {
                if (historyChartInstance) {
                    historyChartInstance.destroy();
                    historyChartInstance = null;
                }
            });
    }

    const scansTable = $('#OperatorsScansTable').DataTable({
        ordering: true,
        searching: false,
        dom: 'frtip',
        order: [[0, 'desc'], [1, 'desc']],
        processing: true,
        serverSide: true,
        pageLength: isTabletViewport ? 5 : 10,
        ajax: {
            url: '/api/ProductionOperatorsDashboardApi/table',
            type: 'POST',
            // Include all filter values, including time range, when requesting table data.
            data: function (d) {
                const filters = getCurrentFilters();
                d.operationFilter = filters.operationFilter;
                d.employeeFilter = filters.employeeFilter;
                d.startDateFilter = filters.startDateFilter;
                d.endDateFilter = filters.endDateFilter;
                // The backend API expects time interval parameters; they were previously omitted.
                d.startTimeFilter = filters.startTimeFilter;
                d.endTimeFilter = filters.endTimeFilter;
            },
            dataSrc: function (json) {
                return json.data;
            },
            error: function (xhr, error, code) {
                console.error('Operators scans table error:', error, code, xhr.responseText);
            }
        },
        columns: [
            {
                data: 'scanDate',
                orderable: true,
                render: function (data, type) {
                    if (type !== 'display') return data;
                    return data ? data.split('-').reverse().join('/') : '';
                }
            },
            {
                data: 'scanTime',
                orderable: true,
                render: function (data, type) {
                    if (type !== 'display') return data;
                    return data || '';
                }
            },
            {
                data: 'employeeNumber',
                className: 'text-center',
                orderable: true
            },
            {
                data: 'fullName',
                orderable: true,
                render: function (data) {
                    return data || '';
                }
            },
            {
                data: 'operation',
                orderable: true
            },
            {
                data: 'code',
                orderable: true
            },
            {
                data: null,
                orderable: false,
                className: 'text-center',
                render: function (data, type) {
                    if (type !== 'display') return data;
                    return '<button class="btn btn-sm btn-outline-primary btn-edit-scan" data-scan-id="' + data.id + '" title="Editar escaneo"><i class="fas fa-pen"></i></button>';
                }
            }
        ],
        drawCallback: function () {
            attachEditHandlers();
        }
    });

    function attachEditHandlers() {
        $('.btn-edit-scan').off('click').on('click', function () {
            var scanId = $(this).data('scan-id');
            openScanEditModal(scanId);
        });
    }

    function reloadDashboard() {
        loadSummary();
        loadChart();
        loadHistoryChart();
        scansTable.ajax.reload();
    }

    // Refresh data when any filter, including time range, changes.
    $('#operationFilter, #startDateFilter, #endDateFilter, #startTimeFilter, #endTimeFilter').on('change', reloadDashboard);

    $('#employeeFilter').on('input', function () {
        clearTimeout(employeeSuggestionsTimer);
        employeeSuggestionsTimer = setTimeout(() => {
            loadEmployeeSuggestions();
            reloadDashboard();
        }, 300);
    });

    $('#employeeFilter').on('focus', loadEmployeeSuggestions);

    $('#btnScansExport').on('click', function () {
        if (isExporting) {
            return;
        }

        isExporting = true;
        const $exportButton = $(this);
        const originalHtml = $exportButton.html();
        $exportButton.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Exportando...');

        Swal.fire({
            title: 'Generando archivo...',
            text: 'Por favor espere mientras se genera el reporte',
            allowOutsideClick: false,
            allowEscapeKey: false,
            showConfirmButton: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });

        fetch('/api/ProductionOperatorsDashboardApi/export', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(getCurrentFilters())
        })
            .then(async response => {
                if (!response.ok) {
                    let message = 'No se pudo exportar el reporte';

                    try {
                        const errorData = await response.json();
                        message = errorData.message || message;
                    } catch {
                        // Response was not JSON.
                    }

                    throw new Error(message);
                }

                const blob = await response.blob();
                const downloadUrl = window.URL.createObjectURL(blob);
                const link = document.createElement('a');
                const todayLabel = new Date().toISOString().slice(0, 10).replace(/-/g, '');

                link.href = downloadUrl;
                link.download = `ReporteEscaneosOperadores_${todayLabel}.xlsx`;
                document.body.appendChild(link);
                link.click();
                link.remove();
                window.URL.revokeObjectURL(downloadUrl);

                Swal.fire({
                    icon: 'success',
                    title: 'Exportacion completada',
                    text: 'Se descargo el reporte filtrado.',
                    timer: 2500,
                    showConfirmButton: false,
                    toast: true,
                    position: 'top-end'
                });
            })
            .catch(error => {
                Swal.fire({
                    icon: 'error',
                    title: 'Error al exportar',
                    text: error.message || 'No se pudo generar el archivo.'
                });
            })
            .finally(() => {
                isExporting = false;
                $exportButton.prop('disabled', false).html(originalHtml);
            });
    });

    function getCurrentFilters() {
        return {
            operationFilter: $('#operationFilter').val(),
            employeeFilter: $('#employeeFilter').val(),
            startDateFilter: $('#startDateFilter').val(),
            endDateFilter: $('#endDateFilter').val(),

            startTimeFilter: $('#startTimeFilter').val(),
            endTimeFilter: $('#endTimeFilter').val()
        };
    }

    function loadAreaOptions() {
        fetch('/api/ProductionOperatorsDashboardApi/areas')
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(areas => {
                var select = $('#opArea');
                select.empty().append('<option value="">Seleccione un area</option>');
                (areas || []).forEach(function (a) {
                    var label = a.areaName;
                    if (a.customerName) { label += ' - ' + a.customerName; }
                    select.append('<option value="' + a.areaId + '">' + label + '</option>');
                });
            })
            .catch(function () {});
    }

    function loadOperationOptions() {
        fetch('/api/ProductionOperatorsDashboardApi/operations')
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(operations => {
                var datalist = $('#operationOptions');
                datalist.empty();
                (operations || []).forEach(function (op) {
                    datalist.append('<option value="' + op + '">');
                });
            })
            .catch(function () {});
    }

    function loadOperatorList() {
        $('#operatorListBody').html('<tr><td colspan="5" class="text-center text-muted py-3">Cargando...</td></tr>');

        fetch('/api/ProductionOperatorsDashboardApi/operators/list')
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(function (operators) {
                if (!operators || !operators.length) {
                    $('#operatorListBody').html('<tr><td colspan="5" class="text-center text-muted py-3">No hay operadores registrados</td></tr>');
                    return;
                }
                var html = '';
                (operators || []).forEach(function (op) {
                    var fullName = (op.nameOperator || '') + ' ' + (op.lastnameOperator || '');
                    var activeBadge = op.active !== false
                        ? '<span class="badge bg-success">Si</span>'
                        : '<span class="badge bg-danger">No</span>';
                    html += '<tr>' +
                        '<td>' + op.employeeNumber + '</td>' +
                        '<td>' + fullName.trim() + '</td>' +
                        '<td>' + (op.operation || '-') + '</td>' +
                        '<td class="text-center">' + activeBadge + '</td>' +
                        '<td class="text-center">' +
                        '<button class="btn btn-sm btn-outline-primary me-1 btn-list-edit" data-employee="' + op.employeeNumber + '" title="Editar"><i class="fas fa-pen"></i></button>' +
                        '<button class="btn btn-sm btn-outline-danger btn-list-delete" data-employee="' + op.employeeNumber + '" data-name="' + fullName.trim() + '" title="Eliminar"><i class="fas fa-trash"></i></button>' +
                        '</td></tr>';
                });
                $('#operatorListBody').html(html);
            })
            .catch(function () {
                $('#operatorListBody').html('<tr><td colspan="5" class="text-center text-danger py-3">Error al cargar operadores</td></tr>');
            });
    }

    function showListView() {
        $('#operatorFormView').hide();
        $('#operatorListView').show();
        $('#btnDeleteOperator').hide();
        loadOperatorList();
    }

    function showFormView(employeeNumber) {
        $('#operatorListView').hide();
        $('#operatorFormView').show();
        $('#editOperatorId').val('');
        $('#operatorForm')[0].reset();
        $('#opActive').prop('checked', true);
        $('.is-invalid').removeClass('is-invalid');

        if (employeeNumber) {
            $('#btnDeleteOperator').show().data('employee', employeeNumber);
            $('#btnSaveOperator').prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Cargando...');

            fetch('/api/ProductionOperatorsDashboardApi/operator/' + employeeNumber)
                .then(response => response.ok ? response.json() : Promise.reject(response))
                .then(function (op) {
                    $('#editOperatorId').val(op.operatorId || '');
                    $('#opEmployeeNumber').val(op.employeeNumber);
                    $('#opName').val(op.nameOperator || '');
                    $('#opLastname').val(op.lastnameOperator || '');
                    $('#opArea').val(op.areaId || '');
                    $('#opOperation').val(op.operation || '');
                    $('#opGoal').val(op.goal || '');
                    $('#opActive').prop('checked', op.active !== false);
                    $('#btnSaveOperator').prop('disabled', false).html('<i class="fas fa-save me-1"></i>Guardar');
                })
                .catch(function () {
                    Swal.fire({ icon: 'error', title: 'Error', text: 'No se pudo cargar la informacion del operador' });
                    showListView();
                });
        } else {
            $('#btnDeleteOperator').hide();
        }
    }

    function openOperatorModal(employeeNumber) {
        if (employeeNumber) {
            showFormView(employeeNumber);
        } else {
            showListView();
        }
        $('#operatorModal').modal('show');
    }

    $('#opName, #opLastname').on('input', function () {
        this.value = this.value.toUpperCase();
    });

    $('#btnAddOperator').on('click', function () {
        openOperatorModal(null);
    });

    $('#operatorSearchInput').on('keyup', function () {
        var q = this.value.toLowerCase().trim();
        $('#operatorListBody tr').each(function () {
            var text = $(this).text().toLowerCase();
            $(this).toggle(text.indexOf(q) >= 0);
        });
    });

    $('#btnNewOperatorFromList').on('click', function () {
        showFormView(null);
    });

    $('#btnBackToList').on('click', function (e) {
        e.preventDefault();
        showListView();
    });

    $(document).on('click', '.btn-list-edit', function () {
        showFormView($(this).data('employee'));
    });

    $(document).on('click', '.btn-list-delete', function () {
        var employeeNumber = $(this).data('employee');
        var fullName = $(this).data('name');

        Swal.fire({
            title: 'Eliminar operador',
            text: 'Esta seguro de desactivar a "' + fullName + '"?',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#d33',
            cancelButtonText: 'Cancelar',
            confirmButtonText: 'Si, eliminar'
        }).then(function (result) {
            if (!result.isConfirmed) return;

            fetch('/api/ProductionOperatorsDashboardApi/operator/' + employeeNumber, {
                method: 'DELETE'
            })
                .then(response => response.ok ? response.json() : Promise.reject(response))
                .then(function () {
                    Swal.fire({ icon: 'success', title: 'Operador desactivado', timer: 2000, showConfirmButton: false, toast: true, position: 'top-end' });
                    loadOperatorList();
                    reloadDashboard();
                    loadEmployeeSuggestions();
                })
                .catch(function () {
                    Swal.fire({ icon: 'error', title: 'Error', text: 'No se pudo eliminar el operador' });
                });
        });
    });

    $('#btnDeleteOperator').on('click', function () {
        var employeeNumber = $(this).data('employee');

        Swal.fire({
            title: 'Eliminar operador',
            text: 'Esta seguro de desactivar este operador?',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#d33',
            cancelButtonText: 'Cancelar',
            confirmButtonText: 'Si, eliminar'
        }).then(function (result) {
            if (!result.isConfirmed) return;

            fetch('/api/ProductionOperatorsDashboardApi/operator/' + employeeNumber, {
                method: 'DELETE'
            })
                .then(response => response.ok ? response.json() : Promise.reject(response))
                .then(function () {
                    Swal.fire({ icon: 'success', title: 'Operador desactivado', timer: 2000, showConfirmButton: false, toast: true, position: 'top-end' });
                    showListView();
                    reloadDashboard();
                    loadEmployeeSuggestions();
                })
                .catch(function () {
                    Swal.fire({ icon: 'error', title: 'Error', text: 'No se pudo eliminar el operador' });
                });
        });
    });

    $('#operatorForm').on('submit', function (e) {
        e.preventDefault();

        if (!this.checkValidity()) {
            $(this).addClass('was-validated');
            return;
        }

        var employeeNumber = parseInt($('#opEmployeeNumber').val(), 10);
        if (!employeeNumber || employeeNumber < 1) {
            $('#opEmployeeNumber').addClass('is-invalid');
            return;
        }

        var payload = {
            employeeNumber: employeeNumber,
            nameOperator: $('#opName').val().trim().toUpperCase(),
            lastnameOperator: $('#opLastname').val().trim().toUpperCase(),
            areaId: $('#opArea').val() ? parseInt($('#opArea').val(), 10) : null,
            operation: $('#opOperation').val().trim() || null,
            goal: $('#opGoal').val() ? parseInt($('#opGoal').val(), 10) : null,
            active: $('#opActive').is(':checked')
        };

        $('#btnSaveOperator').prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Guardando...');

        fetch('/api/ProductionOperatorsDashboardApi/saveOperator', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        })
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(function (result) {
                Swal.fire({
                    icon: 'success',
                    title: 'Operador guardado',
                    timer: 1500,
                    showConfirmButton: false,
                    toast: true,
                    position: 'top-end'
                });
                showListView();
                reloadDashboard();
                loadEmployeeSuggestions();
            })
            .catch(function (err) {
                var msg = 'No se pudo guardar el operador';
                if (err && err.message) msg = err.message;
                Swal.fire({ icon: 'error', title: 'Error', text: msg });
            })
            .finally(function () {
                $('#btnSaveOperator').prop('disabled', false).html('<i class="fas fa-save me-1"></i>Guardar');
            });
    });

    function loadEditScanEmployeeSuggestions() {
        var term = ($('#editScanEmployee').val() || '').trim();

        fetch('/api/ProductionOperatorsDashboardApi/operators?term=' + encodeURIComponent(term))
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(function (items) {
                var list = $('#editScanEmployeeOptions');
                list.empty();
                (items || []).forEach(function (item) {
                    var label = item.label || '';
                    if (label) {
                        $('<option>', { value: label }).appendTo(list);
                    }
                });
            })
            .catch(function () {});
    }

    function openScanEditModal(scanId) {
        if (!scanId) return;

        $('#btnSaveScanEdit').prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Cargando...');
        $('#scanEditForm')[0].reset();
        $('#scanEditForm').removeClass('was-validated');

        fetch('/api/ProductionOperatorsDashboardApi/scan/' + scanId)
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(function (scan) {
                $('#editScanId').val(scan.id);
                $('#editScanEmployee').val((scan.employeeNumber || '') + ' - ' + (scan.fullName || ''));
                $('#editScanEmployee').data('employee-number', scan.employeeNumber);
                $('#editScanOperation').val(scan.operation || '');
                $('#editScanCode').val(scan.code || '');

                if (scan.scannedAt) {
                    var parts = scan.scannedAt.split(' ');
                    $('#editScanDate').val(parts[0] || '');
                    $('#editScanTime').val(parts[1] ? parts[1].substring(0, 5) : '');
                }

                $('#btnSaveScanEdit').prop('disabled', false).html('<i class="fas fa-save me-1"></i>Guardar');
                $('#scanEditModal').modal('show');
            })
            .catch(function () {
                Swal.fire({ icon: 'error', title: 'Error', text: 'No se pudo cargar la informacion del escaneo' });
            });
    }

    $('#editScanEmployee').on('input', function () {
        var val = $(this).val();
        var label = $(this).data('label') || '';
        if (val !== label) {
            $(this).data('employee-number', '');
        }
        loadEditScanEmployeeSuggestions();
    });

    $('#editScanEmployee').on('change', function () {
        var val = $(this).val().trim();
        var match = val.match(/^(\d+)/);
        if (match) {
            $(this).data('employee-number', parseInt(match[1], 10));
            $(this).data('label', val);
        } else {
            $(this).data('employee-number', '');
        }
    });

    $('#scanEditForm').on('submit', function (e) {
        e.preventDefault();

        if (!this.checkValidity()) {
            $(this).addClass('was-validated');
            return;
        }

        var scanId = $('#editScanId').val();
        if (!scanId) return;

        var employeeNumber = $('#editScanEmployee').data('employee-number');
        if (!employeeNumber) {
            $('#editScanEmployee').addClass('is-invalid');
            return;
        }

        var scannedAt = null;
        var dateVal = $('#editScanDate').val();
        var timeVal = $('#editScanTime').val();
        if (dateVal) {
            scannedAt = dateVal + 'T' + (timeVal || '00:00') + ':00';
        }

        var payload = {
            employeeNumber: employeeNumber,
            code: $('#editScanCode').val().trim(),
            scannedAt: scannedAt
        };

        $('#btnSaveScanEdit').prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Guardando...');

        fetch('/api/ProductionOperatorsDashboardApi/scan/' + scanId, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        })
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(function () {
                Swal.fire({
                    icon: 'success',
                    title: 'Escaneo actualizado',
                    timer: 1500,
                    showConfirmButton: false,
                    toast: true,
                    position: 'top-end'
                });
                $('#scanEditModal').modal('hide');
                reloadDashboard();
            })
            .catch(function () {
                Swal.fire({ icon: 'error', title: 'Error', text: 'No se pudo actualizar el escaneo' });
            })
            .finally(function () {
                $('#btnSaveScanEdit').prop('disabled', false).html('<i class="fas fa-save me-1"></i>Guardar');
            });
    });

    loadAreaOptions();
    loadOperationOptions();
    loadSummary();
    loadChart();
    loadHistoryChart();
    loadEmployeeSuggestions();
});

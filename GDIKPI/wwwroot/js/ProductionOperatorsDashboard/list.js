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
    let managedProductionLines = [];
    let hiddenProductionStatsLineIds = new Set();
    let productionDashboardSyncConnection = null;
    let productionDashboardSyncTimer = null;
    const isTabletViewport = window.matchMedia('(min-width: 768px) and (max-width: 1180px)').matches;
    const employeeSuggestionsList = $('#employeeFilterOptions');

    function getSelectedAreaId() {
        const value = $('#areaFilter').val() || '';
        const parsed = Number(value);
        return parsed > 0 ? parsed : null;
    }

    function getSelectedLineId() {
        const value = $('#lineFilter').val() || '';
        const parsed = Number(value);
        return parsed > 0 ? parsed : null;
    }

    function appendQueryParam(url, key, value) {
        if (!value) {
            return url;
        }

        const separator = url.indexOf('?') >= 0 ? '&' : '?';
        return url + separator + encodeURIComponent(key) + '=' + encodeURIComponent(value);
    }

    function withAreaParam(url) {
        var scopedUrl = appendQueryParam(url, 'areaId', getSelectedAreaId());
        scopedUrl = appendQueryParam(scopedUrl, 'productionLinesId', getSelectedLineId());
        return scopedUrl;
    }

    function updateScanProductionLink() {
        var url = '/ProductionOperators';
        var areaId = getSelectedAreaId();

        if (areaId) {
            url = appendQueryParam(url, 'areaId', areaId);
        }

        $('#btnScanProduction').attr('href', url);
    }

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

        fetch(withAreaParam(`/api/ProductionOperatorsDashboardApi/operators?term=${encodeURIComponent(term)}`))
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
                                text: response.chartTitle || 'Historial de producción'
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
                d.areaId = filters.areaId;
                d.productionLinesId = filters.productionLinesId;
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
                    if (!data.canEdit) {
                        return '<button class="btn btn-sm btn-outline-danger btn-delete-scan" data-scan-id="' + data.id + '" data-source="LINE" title="Eliminar escaneo de linea"><i class="fas fa-trash"></i></button>';
                    }
                    return '<div class="btn-group btn-group-sm" role="group">' +
                        '<button class="btn btn-outline-primary btn-edit-scan" data-scan-id="' + data.id + '" title="Editar escaneo"><i class="fas fa-pen"></i></button>' +
                        '<button class="btn btn-outline-danger btn-delete-scan" data-scan-id="' + data.id + '" data-source="OPERATOR" title="Eliminar escaneo"><i class="fas fa-trash"></i></button>' +
                        '</div>';
                }
            }
        ],
        drawCallback: function () {
            attachEditHandlers();
        }
    });

    function escapeHtml(value) {
        return String(value || '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    const auditLogsTable = $('#OperatorsAuditTable').DataTable({
        ordering: true,
        searching: false,
        dom: 'rtip',
        order: [[0, 'desc'], [1, 'desc']],
        processing: true,
        serverSide: true,
        pageLength: isTabletViewport ? 5 : 10,
        ajax: {
            url: '/api/ProductionOperatorsDashboardApi/audit-logs',
            type: 'POST',
            data: function (d) {
                const filters = getCurrentFilters();
                d.startDateFilter = filters.startDateFilter;
                d.endDateFilter = filters.endDateFilter;
                d.actionFilter = $('#auditActionFilter').val();
                d.userFilter = $('#auditUserFilter').val();
                d.employeeFilter = $('#auditEmployeeFilter').val();
                d.codeFilter = $('#auditCodeFilter').val();
                d.detailFilter = $('#auditSearchFilter').val();
            },
            dataSrc: function (json) {
                return json.data;
            },
            error: function (xhr, error, code) {
                console.error('Operators audit log table error:', error, code, xhr.responseText);
            }
        },
        columns: [
            {
                data: 'logDate',
                orderable: true,
                render: function (data, type) {
                    if (type !== 'display') return data;
                    return data ? data.split('-').reverse().join('/') : '';
                }
            },
            {
                data: 'logTime',
                orderable: true,
                render: function (data) {
                    return data || '';
                }
            },
            {
                data: 'employeeNumber',
                className: 'text-center',
                orderable: true,
                render: function (data) {
                    return escapeHtml(data);
                }
            },
            {
                data: 'affectedEmployee',
                className: 'text-center',
                orderable: false,
                render: function (data) {
                    return escapeHtml(data);
                }
            },
            {
                data: 'pieceCode',
                orderable: false,
                render: function (data) {
                    return escapeHtml(data);
                }
            },
            {
                data: 'actionType',
                orderable: true,
                render: function (data) {
                    const action = data || '';
                    const labelMap = {
                        CREATE: 'Guardado',
                        REJECTED: 'Rechazado',
                        UPDATE: 'Actualizado',
                        ADD_MANUAL_PIECES: 'Piezas manuales',
                        DEACTIVATE: 'Desactivado'
                    };
                    const classMap = {
                        CREATE: 'bg-success',
                        REJECTED: 'bg-danger',
                        UPDATE: 'bg-primary',
                        ADD_MANUAL_PIECES: 'bg-info text-dark',
                        DEACTIVATE: 'bg-secondary'
                    };

                    return `<span class="badge ${classMap[action] || 'bg-dark'}">${escapeHtml(labelMap[action] || action)}</span>`;
                }
            },
            {
                data: 'entityName',
                orderable: true,
                render: function (data) {
                    const labelMap = {
                        ProductionOperator: 'Operador',
                        ProductionOperatorsScan: 'Escaneo',
                        ProductionOperatorsScanAttempt: 'Intento'
                    };

                    return escapeHtml(labelMap[data] || data);
                }
            },
            {
                data: 'entityId',
                className: 'text-center',
                orderable: true,
                render: function (data) {
                    return escapeHtml(data);
                }
            },
            {
                data: 'details',
                orderable: false,
                className: 'audit-details-cell',
                render: function (data) {
                    return escapeHtml(data);
                }
            }
        ]
    });

    function attachEditHandlers() {
        $('.btn-edit-scan').off('click').on('click', function () {
            var scanId = $(this).data('scan-id');
            openScanEditModal(scanId);
        });

        $('.btn-delete-scan').off('click').on('click', function () {
            var scanId = $(this).data('scan-id');
            var source = $(this).data('source') || 'OPERATOR';
            deleteDashboardScan(scanId, source);
        });
    }

    function deleteDashboardScan(scanId, source) {
        if (!scanId) return;

        var isLineScan = String(source).toUpperCase() === 'LINE';

        Swal.fire({
            icon: 'warning',
            title: 'Eliminar escaneo',
            text: isLineScan
                ? 'Se eliminara el escaneo y se descontara una pieza de la produccion de la linea.'
                : 'Esta accion eliminara permanentemente el escaneo del operador.',
            showCancelButton: true,
            confirmButtonText: 'Eliminar',
            cancelButtonText: 'Cancelar',
            confirmButtonColor: '#dc3545'
        }).then(function (confirmation) {
            if (!confirmation.isConfirmed) return;

            fetch('/api/ProductionOperatorsDashboardApi/scan/' + scanId + '?source=' + encodeURIComponent(source), {
                method: 'DELETE'
            })
                .then(async function (response) {
                    var result = await response.json().catch(function () { return {}; });
                    if (!response.ok) {
                        throw new Error(result.message || 'No se pudo eliminar el escaneo');
                    }
                    return result;
                })
                .then(function (result) {
                    Swal.fire({
                        icon: 'success',
                        title: 'Escaneo eliminado',
                        text: result.message,
                        timer: 1500,
                        showConfirmButton: false,
                        toast: true,
                        position: 'top-end'
                    });
                    reloadDashboard();
                })
                .catch(function (error) {
                    Swal.fire({ icon: 'error', title: 'Error', text: error.message });
                });
        });
    }

    function reloadDashboard() {
        loadSummary();
        loadChart();
        loadHistoryChart();
        scansTable.ajax.reload();
        auditLogsTable.ajax.reload();
    }

    function scheduleSynchronizedDashboardReload() {
        clearTimeout(productionDashboardSyncTimer);
        productionDashboardSyncTimer = setTimeout(reloadDashboard, 300);
    }

    async function initializeProductionDashboardSync() {
        if (typeof signalR === 'undefined') {
            console.warn('SignalR no está disponible para actualizar las gráficas.');
            return;
        }

        productionDashboardSyncConnection = new signalR.HubConnectionBuilder()
            .withUrl('/dashboardHub')
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .build();

        productionDashboardSyncConnection.on(
            'ProductionDataUpdated',
            scheduleSynchronizedDashboardReload);
        productionDashboardSyncConnection.on(
            'ProductionLinesScannerUpdated',
            scheduleSynchronizedDashboardReload);
        productionDashboardSyncConnection.on(
            'OperatorStatsUpdated',
            scheduleSynchronizedDashboardReload);
        productionDashboardSyncConnection.onreconnected(scheduleSynchronizedDashboardReload);

        try {
            await productionDashboardSyncConnection.start();
        } catch (error) {
            console.error('No se pudo iniciar la actualización de gráficas:', error);
            setTimeout(initializeProductionDashboardSync, 5000);
        }
    }

    initializeProductionDashboardSync();

    // Refresh data when any filter, including time range, changes.
    $('#operationFilter, #startDateFilter, #endDateFilter, #startTimeFilter, #endTimeFilter').on('change', reloadDashboard);
    $('#auditActionFilter').on('change', function () {
        auditLogsTable.ajax.reload();
    });

    let auditSearchTimer = null;
    $('#auditUserFilter, #auditEmployeeFilter, #auditCodeFilter, #auditSearchFilter').on('input', function () {
        clearTimeout(auditSearchTimer);
        auditSearchTimer = setTimeout(function () {
            auditLogsTable.ajax.reload();
        }, 300);
    });

    $('#areaFilter').on('change', function () {
        $('#lineFilter').val('');
        loadLineOptions(getSelectedAreaId(), null, '#lineFilter', 'Todas')
            .finally(function () {
                updateScanProductionLink();
                loadOperationOptions();
                loadEmployeeSuggestions();
                reloadDashboard();
            });
    });

    $('#lineFilter').on('change', function () {
        loadOperationOptions();
        loadEmployeeSuggestions();
        updateScanProductionLink();
        reloadDashboard();
    });

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

    function loadManualPiecesEmployeeSuggestions() {
        var term = ($('#manualPiecesEmployee').val() || '').trim();

        fetch(withAreaParam('/api/ProductionOperatorsDashboardApi/operators?term=' + encodeURIComponent(term)))
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(function (items) {
                var list = $('#manualPiecesEmployeeOptions');
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

    function openManualPiecesModal() {
        var now = new Date();
        var todayValue = now.toISOString().slice(0, 10);
        var timeValue = now.toTimeString().slice(0, 5);

        $('#manualPiecesForm')[0].reset();
        $('#manualPiecesForm').removeClass('was-validated');
        $('#manualPiecesEmployee').removeClass('is-invalid').data('employee-number', '');
        $('#manualPiecesQuantity').val(1);
        $('#manualPiecesDate').val(todayValue);
        $('#manualPiecesTime').val(timeValue);
        loadManualPiecesEmployeeSuggestions();
        $('#manualPiecesModal').modal('show');
        setTimeout(function () {
            $('#manualPiecesEmployee').trigger('focus');
        }, 200);
    }

    $('#btnAddManualPieces').on('click', function () {
        openManualPiecesModal();
    });

    function loadManualLineProductionOptions() {
        var select = $('#manualLineProductionLine');
        var selectedLineId = getSelectedLineId();
        var url = appendQueryParam(
            '/api/ProductionOperatorsDashboardApi/production-lines/manage',
            'areaId',
            getSelectedAreaId()
        );

        select.prop('disabled', true).html('<option value="">Cargando lineas...</option>');

        return fetch(url)
            .then(function (response) {
                return response.ok ? response.json() : Promise.reject(response);
            })
            .then(function (lines) {
                select.empty().append('<option value="">Seleccione una linea</option>');

                (lines || [])
                    .filter(function (line) { return line.isActive !== false; })
                    .forEach(function (line) {
                        var areaLabel = [line.customerName, line.areaName].filter(Boolean).join(' - ');
                        var lineLabel = line.lineName || ('Linea ' + line.lineNumber);
                        var label = areaLabel ? lineLabel + ' - ' + areaLabel : lineLabel;
                        $('<option>', {
                            value: line.productionLinesId,
                            text: label
                        }).appendTo(select);
                    });

                if (selectedLineId) {
                    select.val(String(selectedLineId));
                }
            })
            .catch(function () {
                select.html('<option value="">No se pudieron cargar las lineas</option>');
            })
            .finally(function () {
                select.prop('disabled', false);
            });
    }

    function openManualLineProductionModal() {
        var now = new Date();
        var localDate = now.getFullYear() + '-' +
            String(now.getMonth() + 1).padStart(2, '0') + '-' +
            String(now.getDate()).padStart(2, '0');
        var localTime = String(now.getHours()).padStart(2, '0') + ':' +
            String(now.getMinutes()).padStart(2, '0');

        $('#manualLineProductionForm')[0].reset();
        $('#manualLineProductionForm').removeClass('was-validated');
        $('#manualLineProductionQuantity').val(1);
        $('#manualLineProductionDate').val(localDate);
        $('#manualLineProductionTime').val(localTime);
        loadManualLineProductionOptions();

        var modalElement = document.getElementById('manualLineProductionModal');
        if (!modalElement || typeof bootstrap === 'undefined') {
            Swal.fire({ icon: 'error', title: 'Error', text: 'No se pudo abrir el modal' });
            return;
        }

        bootstrap.Modal.getOrCreateInstance(modalElement).show();
    }

    $('#btnAddLineProduction').on('click', openManualLineProductionModal);

    $('#manualLineProductionForm').on('submit', function (event) {
        event.preventDefault();

        if (!this.checkValidity()) {
            $(this).addClass('was-validated');
            return;
        }

        var payload = {
            productionLinesId: parseInt($('#manualLineProductionLine').val(), 10),
            quantity: parseInt($('#manualLineProductionQuantity').val(), 10),
            producedAt: $('#manualLineProductionDate').val() + 'T' +
                $('#manualLineProductionTime').val() + ':00',
            areaId: getSelectedAreaId()
        };

        var saveButton = $('#btnSaveManualLineProduction');
        saveButton.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Guardando...');

        fetch('/api/ProductionOperatorsDashboardApi/manual-line-production', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        })
            .then(async function (response) {
                var result = await response.json().catch(function () { return {}; });
                if (!response.ok) {
                    throw new Error(result.message || 'No se pudo agregar la produccion');
                }
                return result;
            })
            .then(function (result) {
                var modalElement = document.getElementById('manualLineProductionModal');
                bootstrap.Modal.getOrCreateInstance(modalElement).hide();
                Swal.fire({
                    icon: 'success',
                    title: 'Produccion agregada',
                    text: result.message,
                    timer: 1800,
                    showConfirmButton: false,
                    toast: true,
                    position: 'top-end'
                });
                reloadDashboard();
            })
            .catch(function (error) {
                Swal.fire({ icon: 'error', title: 'Error', text: error.message });
            })
            .finally(function () {
                saveButton.prop('disabled', false).html('<i class="fas fa-save me-1"></i>Guardar');
            });
    });

    $('#manualPiecesEmployee').on('input', function () {
        var val = $(this).val();
        var label = $(this).data('label') || '';
        if (val !== label) {
            $(this).data('employee-number', '');
        }
        loadManualPiecesEmployeeSuggestions();
    });

    $('#manualPiecesEmployee').on('focus', loadManualPiecesEmployeeSuggestions);

    $('#manualPiecesEmployee').on('change', function () {
        var val = $(this).val().trim();
        var match = val.match(/^(\d+)/);
        if (match) {
            $(this).data('employee-number', parseInt(match[1], 10));
            $(this).data('label', val);
            $(this).removeClass('is-invalid');
        } else {
            $(this).data('employee-number', '');
        }
    });

    $('#manualPiecesForm').on('submit', function (e) {
        e.preventDefault();

        if (!this.checkValidity()) {
            $(this).addClass('was-validated');
            return;
        }

        var employeeNumber = $('#manualPiecesEmployee').data('employee-number');
        if (!employeeNumber) {
            var match = ($('#manualPiecesEmployee').val() || '').trim().match(/^(\d+)/);
            employeeNumber = match ? parseInt(match[1], 10) : null;
        }

        if (!employeeNumber) {
            $('#manualPiecesEmployee').addClass('is-invalid');
            return;
        }

        var quantity = parseInt($('#manualPiecesQuantity').val(), 10);
        var dateVal = $('#manualPiecesDate').val();
        var timeVal = $('#manualPiecesTime').val() || '00:00';

        var payload = {
            employeeNumber: employeeNumber,
            code: $('#manualPiecesCode').val().trim() || null,
            quantity: quantity,
            scannedAt: dateVal + 'T' + timeVal + ':00',
            areaId: getSelectedAreaId(),
            productionLinesId: getSelectedLineId()
        };

        $('#btnSaveManualPieces').prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Guardando...');

        fetch('/api/ProductionOperatorsDashboardApi/manual-pieces', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        })
            .then(async response => {
                if (!response.ok) {
                    let message = 'No se pudieron agregar las piezas';

                    try {
                        const errorData = await response.json();
                        message = errorData.message || message;
                    } catch {
                        // Response was not JSON.
                    }

                    throw new Error(message);
                }

                return response.json();
            })
            .then(function (result) {
                Swal.fire({
                    icon: 'success',
                    title: 'Piezas agregadas',
                    text: result.message || 'Se agregaron correctamente.',
                    timer: 1800,
                    showConfirmButton: false,
                    toast: true,
                    position: 'top-end'
                });
                $('#manualPiecesModal').modal('hide');
                reloadDashboard();
            })
            .catch(function (error) {
                Swal.fire({
                    icon: 'error',
                    title: 'Error',
                    text: error.message || 'No se pudieron agregar las piezas'
                });
            })
            .finally(function () {
                $('#btnSaveManualPieces').prop('disabled', false).html('<i class="fas fa-save me-1"></i>Guardar');
            });
    });

    function getCurrentFilters() {
        return {
            operationFilter: $('#operationFilter').val(),
            employeeFilter: $('#employeeFilter').val(),
            startDateFilter: $('#startDateFilter').val(),
            endDateFilter: $('#endDateFilter').val(),

            startTimeFilter: $('#startTimeFilter').val(),
            endTimeFilter: $('#endTimeFilter').val(),
            areaId: getSelectedAreaId(),
            productionLinesId: getSelectedLineId()
        };
    }

    function loadAreaOptions() {
        return fetch('/api/ProductionOperatorsDashboardApi/areas')
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(areas => {
                var filterSelect = $('#areaFilter');
                var operatorSelect = $('#opArea');
                var productionLineSelect = $('#productionLineArea');

                filterSelect.empty().append('<option value="">Todas</option>');
                operatorSelect.empty().append('<option value="">Seleccione un area</option>');
                productionLineSelect.empty().append('<option value="">Seleccione un area</option>');

                (areas || []).forEach(function (a) {
                    var label = a.areaName;
                    if (a.customerName) { label += ' - ' + a.customerName; }
                    filterSelect.append('<option value="' + a.areaId + '">' + label + '</option>');
                    operatorSelect.append('<option value="' + a.areaId + '">' + label + '</option>');
                    productionLineSelect.append('<option value="' + a.areaId + '">' + label + '</option>');
                });

                return loadLineOptions(getSelectedAreaId(), null, '#lineFilter', 'Todas');
            })
            .then(updateScanProductionLink)
            .catch(function () {});
    }

    function getLineLabel(line, includeArea) {
        var label = line.lineName || (line.lineNumber ? 'Linea ' + line.lineNumber : 'Linea ' + line.productionLinesId);

        if (!line.lineName && line.lineNumber) {
            label = 'Linea ' + line.lineNumber;
        }

        if (includeArea) {
            var areaLabel = [line.customerName, line.areaName].filter(Boolean).join(' - ');
            if (areaLabel) {
                label = areaLabel + ' / ' + label;
            }
        }

        return label;
    }

    function loadLineOptions(areaId, selectedLineId, targetSelector, emptyText) {
        var select = $(targetSelector || '#opLine');
        select.empty().append('<option value="">' + (emptyText || 'Seleccione una linea') + '</option>');

        var url = '/api/ProductionOperatorsDashboardApi/production-lines';
        if (areaId) {
            url = appendQueryParam(url, 'areaId', areaId);
        }

        return fetch(url)
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(lines => {
                (lines || []).forEach(function (line) {
                    select.append('<option value="' + line.productionLinesId + '">' + getLineLabel(line, !areaId) + '</option>');
                });

                if (selectedLineId) {
                    select.val(selectedLineId);
                }
            })
            .catch(function () {});
    }

    function loadOperationOptions() {
        fetch(withAreaParam('/api/ProductionOperatorsDashboardApi/operations'))
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(operations => {
                var datalist = $('#operationOptions');
                var operationFilter = $('#operationFilter');
                var currentOperation = operationFilter.val() || '';

                datalist.empty();
                operationFilter.empty().append('<option value="">Todas</option>');

                (operations || []).forEach(function (op) {
                    datalist.append('<option value="' + op + '">');
                    operationFilter.append('<option value="' + op + '">' + op + '</option>');
                });

                if (currentOperation) {
                    operationFilter.val(currentOperation);
                    if (operationFilter.val() !== currentOperation) {
                        operationFilter.val('');
                    }
                }
            })
            .catch(function () {});
    }

    function renderProductionLineList(lines) {
        managedProductionLines = lines || [];

        if (!managedProductionLines.length) {
            $('#productionLineListBody').html('<tr><td colspan="8" class="text-center text-muted py-3">No hay lineas registradas</td></tr>');
            return;
        }

        var html = '';
        managedProductionLines.forEach(function (line) {
            var areaLabel = [line.customerName, line.areaName].filter(Boolean).join(' - ') || '-';
            var stateBadge = line.isActive
                ? '<span class="badge bg-success">Activa</span>'
                : '<span class="badge bg-secondary">Inactiva</span>';
            var isVisibleInCard = !hiddenProductionStatsLineIds.has(Number(line.productionLinesId));

            html += '<tr>' +
                '<td>' + escapeHtml(areaLabel) + '</td>' +
                '<td>' + escapeHtml(line.lineNumber == null ? '-' : line.lineNumber) + '</td>' +
                '<td>' + escapeHtml(line.lineName || '-') + '</td>' +
                '<td class="text-end">' + escapeHtml(line.dailyGoal == null ? '-' : line.dailyGoal) + '</td>' +
                '<td class="text-end">' + escapeHtml(line.personalQuantity == null ? '-' : line.personalQuantity) + '</td>' +
                '<td class="text-center">' + stateBadge + '</td>' +
                '<td class="text-center"><div class="form-check form-switch d-inline-block m-0"><input type="checkbox" class="form-check-input production-stats-visibility-toggle" data-id="' + line.productionLinesId + '" aria-label="Mostrar linea en la card" ' + (isVisibleInCard ? 'checked' : '') + '></div></td>' +
                '<td class="text-center"><button type="button" class="btn btn-sm btn-outline-primary btn-edit-production-line" data-id="' + line.productionLinesId + '" title="Editar"><i class="fas fa-pen"></i></button></td>' +
                '</tr>';
        });

        $('#productionLineListBody').html(html);
    }

    function loadProductionLineManagementList() {
        $('#productionLineListBody').html('<tr><td colspan="8" class="text-center text-muted py-3">Cargando...</td></tr>');
        var url = '/api/ProductionOperatorsDashboardApi/production-lines/manage';
        url = appendQueryParam(url, 'areaId', getSelectedAreaId());

        return Promise.all([
            fetch(url).then(function (response) {
                return response.ok ? response.json() : Promise.reject(response);
            }),
            fetch('/api/DashboardProductionVisibility').then(function (response) {
                return response.ok ? response.json() : Promise.reject(response);
            })
        ])
            .then(function (results) {
                var visibility = results[1] || {};
                hiddenProductionStatsLineIds = new Set(
                    (visibility.hiddenCardKeys || [])
                        .filter(function (key) { return String(key).indexOf('production-line-item:') === 0; })
                        .map(function (key) { return Number(String(key).substring('production-line-item:'.length)); })
                        .filter(function (lineId) { return lineId > 0; })
                );
                renderProductionLineList(results[0]);
            })
            .catch(function () {
                managedProductionLines = [];
                $('#productionLineListBody').html('<tr><td colspan="8" class="text-center text-danger py-3">No se pudieron cargar las lineas</td></tr>');
            });
    }

    function showProductionLineList() {
        $('#productionLineFormView').hide();
        $('#productionLineListView').show();
        $('#btnSaveProductionLine').hide();
        loadProductionLineManagementList();
    }

    function showProductionLineForm(productionLinesId) {
        $('#productionLineListView').hide();
        $('#productionLineFormView').show();
        $('#btnSaveProductionLine').show();
        $('#productionLineForm')[0].reset();
        $('#productionLineForm').removeClass('was-validated');
        $('#productionLineId').val('');
        $('#productionLineActive').prop('checked', true);
        $('#productionLineArea').prop('disabled', false).val(getSelectedAreaId() || '');

        if (!productionLinesId) {
            return;
        }

        var line = managedProductionLines.find(function (item) {
            return Number(item.productionLinesId) === Number(productionLinesId);
        });

        if (!line) {
            Swal.fire({ icon: 'error', title: 'Error', text: 'No se encontro la linea seleccionada' });
            showProductionLineList();
            return;
        }

        $('#productionLineId').val(line.productionLinesId);
        $('#productionLineArea').val(line.areaId || '').prop('disabled', true);
        $('#productionLineNumber').val(line.lineNumber || '');
        $('#productionLineName').val(line.lineName || '');
        $('#productionLineGoal').val(line.dailyGoal || '');
        $('#productionLinePeople').val(line.personalQuantity || '');
        $('#productionLineStandardTime').val(line.standardTime == null ? '' : line.standardTime);
        $('#productionLineActive').prop('checked', line.isActive !== false);
    }

    $('#btnManageLines').on('click', function () {
        showProductionLineList();
        var modalElement = document.getElementById('productionLineModal');
        if (!modalElement || typeof bootstrap === 'undefined') {
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: 'No se pudo abrir la gestion de lineas'
            });
            return;
        }

        bootstrap.Modal.getOrCreateInstance(modalElement).show();
    });

    $('#btnNewProductionLine').on('click', function () {
        showProductionLineForm(null);
    });

    $('#btnBackToProductionLineList').on('click', function (event) {
        event.preventDefault();
        showProductionLineList();
    });

    $(document).on('click', '.btn-edit-production-line', function () {
        showProductionLineForm($(this).data('id'));
    });

    $(document).on('change', '.production-stats-visibility-toggle', function () {
        var toggle = $(this);
        var lineId = Number(toggle.data('id'));
        var isVisible = toggle.is(':checked');

        toggle.prop('disabled', true);

        fetch('/api/DashboardProductionVisibility/card', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                cardKey: 'production-line-item:' + lineId,
                isVisible: isVisible
            })
        })
            .then(async function (response) {
                if (!response.ok) {
                    var result = await response.json().catch(function () { return {}; });
                    throw new Error(result.error || 'No se pudo actualizar la visibilidad');
                }

                if (isVisible) {
                    hiddenProductionStatsLineIds.delete(lineId);
                } else {
                    hiddenProductionStatsLineIds.add(lineId);
                }
            })
            .catch(function (error) {
                toggle.prop('checked', !isVisible);
                Swal.fire({ icon: 'error', title: 'Error', text: error.message });
            })
            .finally(function () {
                toggle.prop('disabled', false);
            });
    });

    $('#productionLineSearchInput').on('input', function () {
        var search = ($(this).val() || '').toLowerCase().trim();
        $('#productionLineListBody tr').each(function () {
            $(this).toggle($(this).text().toLowerCase().indexOf(search) >= 0);
        });
    });

    $('#productionLineForm').on('submit', function (event) {
        event.preventDefault();

        if (!this.checkValidity()) {
            $(this).addClass('was-validated');
            return;
        }

        var standardTimeValue = $('#productionLineStandardTime').val();
        var payload = {
            productionLinesId: $('#productionLineId').val() ? parseInt($('#productionLineId').val(), 10) : null,
            areaId: parseInt($('#productionLineArea').val(), 10),
            lineNumber: parseInt($('#productionLineNumber').val(), 10),
            lineName: ($('#productionLineName').val() || '').trim() || null,
            dailyGoal: parseInt($('#productionLineGoal').val(), 10),
            personalQuantity: parseInt($('#productionLinePeople').val(), 10),
            standardTime: standardTimeValue === '' ? null : parseFloat(standardTimeValue),
            isActive: $('#productionLineActive').is(':checked')
        };

        $('#btnSaveProductionLine').prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Guardando...');

        fetch('/api/ProductionOperatorsDashboardApi/save-production-line', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        })
            .then(async function (response) {
                var result = await response.json().catch(function () { return {}; });
                if (!response.ok) {
                    throw new Error(result.message || 'No se pudo guardar la linea');
                }
                return result;
            })
            .then(function () {
                Swal.fire({
                    icon: 'success',
                    title: 'Linea guardada',
                    timer: 1500,
                    showConfirmButton: false,
                    toast: true,
                    position: 'top-end'
                });

                showProductionLineList();
                return loadLineOptions(getSelectedAreaId(), getSelectedLineId(), '#lineFilter', 'Todas');
            })
            .then(function () {
                loadOperationOptions();
                reloadDashboard();
            })
            .catch(function (error) {
                Swal.fire({ icon: 'error', title: 'Error', text: error.message || 'No se pudo guardar la linea' });
            })
            .finally(function () {
                $('#btnSaveProductionLine').prop('disabled', false).html('<i class="fas fa-save me-1"></i>Guardar');
            });
    });

    function loadOperatorList() {
        $('#operatorListBody').html('<tr><td colspan="7" class="text-center text-muted py-3">Cargando...</td></tr>');

        fetch(withAreaParam('/api/ProductionOperatorsDashboardApi/operators/list'))
            .then(response => response.ok ? response.json() : Promise.reject(response))
            .then(function (operators) {
                if (!operators || !operators.length) {
                    $('#operatorListBody').html('<tr><td colspan="7" class="text-center text-muted py-3">No hay operadores registrados</td></tr>');
                    return;
                }
                var html = '';
                (operators || []).forEach(function (op) {
                    var fullName = (op.nameOperator || '') + ' ' + (op.lastnameOperator || '');
                    var areaLabel = [op.customerName, op.areaName].filter(Boolean).join(' - ') || '-';
                    var lineLabel = op.lineName || (op.lineNumber ? 'Linea ' + op.lineNumber : '-');
                    var activeBadge = op.active !== false
                        ? '<span class="badge bg-success">Si</span>'
                        : '<span class="badge bg-danger">No</span>';
                    html += '<tr>' +
                        '<td>' + op.employeeNumber + '</td>' +
                        '<td>' + fullName.trim() + '</td>' +
                        '<td>' + areaLabel + '</td>' +
                        '<td>' + lineLabel + '</td>' +
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
                $('#operatorListBody').html('<tr><td colspan="7" class="text-center text-danger py-3">Error al cargar operadores</td></tr>');
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
        var selectedAreaId = getSelectedAreaId();
        var selectedLineId = getSelectedLineId();
        if (selectedAreaId) {
            $('#opArea').val(String(selectedAreaId)).prop('disabled', false);
            loadLineOptions(selectedAreaId, selectedLineId);
        } else {
            $('#opArea').prop('disabled', false);
            loadLineOptions(null);
        }
        $('.is-invalid').removeClass('is-invalid');

        if (employeeNumber) {
            $('#btnDeleteOperator').show().data('employee', employeeNumber);
            $('#btnSaveOperator').prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Cargando...');

            fetch(withAreaParam('/api/ProductionOperatorsDashboardApi/operator/' + employeeNumber))
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
                    loadLineOptions(op.areaId || '', op.productionLinesId || '')
                        .finally(function () {
                            $('#btnSaveOperator').prop('disabled', false).html('<i class="fas fa-save me-1"></i>Guardar');
                        });
                })
                .catch(function () {
                    Swal.fire({ icon: 'error', title: 'Error', text: 'No se pudo cargar la informacion del operador' });
                    showListView();
                });
        } else {
            $('#btnDeleteOperator').hide();
        }
    }

    $('#opArea').on('change', function () {
        loadLineOptions($(this).val(), null);
    });

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
            productionLinesId: $('#opLine').val() ? parseInt($('#opLine').val(), 10) : null,
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

        fetch(withAreaParam('/api/ProductionOperatorsDashboardApi/operators?term=' + encodeURIComponent(term)))
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
                var modalElement = document.getElementById('scanEditModal');
                bootstrap.Modal.getOrCreateInstance(modalElement).show();
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
            scannedAt: scannedAt,
            areaId: getSelectedAreaId(),
            productionLinesId: getSelectedLineId()
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
                var modalElement = document.getElementById('scanEditModal');
                bootstrap.Modal.getOrCreateInstance(modalElement).hide();
                reloadDashboard();
            })
            .catch(function () {
                Swal.fire({ icon: 'error', title: 'Error', text: 'No se pudo actualizar el escaneo' });
            })
            .finally(function () {
                $('#btnSaveScanEdit').prop('disabled', false).html('<i class="fas fa-save me-1"></i>Guardar');
            });
    });

    loadAreaOptions()
        .finally(function () {
            loadOperationOptions();
            loadEmployeeSuggestions();
            reloadDashboard();
        });
});

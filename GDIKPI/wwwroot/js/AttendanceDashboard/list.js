$(document).ready(function () {
    const today = new Date().toISOString().split('T')[0];
    const employeeSuggestionsList = $('#employeeFilterOptions');
    const employeeSuggestionsMap = {};
    let comparisonChartInstance = null;
    let employeeSuggestionsTimer = null;
    let isSyncingAttendanceLines = false;

    if (!$('#startDateFilter').val()) {
        $('#startDateFilter').val(today);
    }

    if (!$('#endDateFilter').val()) {
        $('#endDateFilter').val(today);
    }

    const historyTable = $('#AttendanceHistoryTable').DataTable({
        ordering: true,
        searching: false,
        processing: true,
        serverSide: true,
        pageLength: 10,
        order: [[0, 'desc'], [1, 'desc']],
        ajax: {
            url: '/api/AttendanceDashboardApi/history-table',
            type: 'POST',
            data: function (d) {
                Object.assign(d, getCurrentFilters());
            },
            dataSrc: function (json) {
                handleApiBanner(null);
                return json.data || [];
            },
            error: function (xhr) {
                handleAjaxError(xhr, 'No se pudo cargar el historial de asistencia.');
            }
        },
        columns: [
            { data: 'dateLabel' },
            { data: 'timeLabel' },
            { data: 'employeeId', className: 'text-center' },
            { data: 'employeeNumber', className: 'text-center' },
            { data: 'fullName' },
            { data: 'department' }
        ]
    });

    const absencesTable = $('#AttendanceAbsencesTable').DataTable({
        ordering: true,
        searching: false,
        processing: true,
        serverSide: true,
        pageLength: 10,
        order: [[0, 'desc']],
        ajax: {
            url: '/api/AttendanceDashboardApi/absences-table',
            type: 'POST',
            data: function (d) {
                Object.assign(d, getCurrentFilters());
            },
            dataSrc: function (json) {
                handleApiBanner(null);
                return json.data || [];
            },
            error: function (xhr) {
                handleAjaxError(xhr, 'No se pudo cargar la tabla de ausencias.');
            }
        },
        columns: [
            { data: 'dateLabel' },
            { data: 'employeeId', className: 'text-center' },
            { data: 'employeeNumber', className: 'text-center' },
            { data: 'fullName' },
            { data: 'department' },
            { data: 'reason' }
        ]
    });

    function getCurrentFilters() {
        return {
            departmentFilter: $('#departmentFilter').val(),
            employeeIdFilter: extractEmployeeId($('#employeeFilter').val()),
            startDateFilter: $('#startDateFilter').val(),
            endDateFilter: $('#endDateFilter').val(),
            startTimeFilter: $('#startTimeFilter').val(),
            endTimeFilter: $('#endTimeFilter').val()
        };
    }

    function extractEmployeeId(value) {
        const raw = (value || '').trim();
        if (!raw) {
            return '';
        }

        if (employeeSuggestionsMap[raw]) {
            return employeeSuggestionsMap[raw];
        }

        const selectedId = $('#employeeFilter').data('employee-id');
        if (selectedId) {
            return selectedId;
        }

        const match = raw.match(/^(\d+)/);
        return match ? match[1] : raw;
    }

    function handleApiBanner(message) {
        const banner = $('#attendanceErrorBanner');
        if (!message) {
            banner.hide().text('');
            return;
        }

        banner.text(message).show();
    }

    function handleAjaxError(xhr, fallbackMessage) {
        let message = fallbackMessage;
        if (xhr && xhr.responseJSON && xhr.responseJSON.message) {
            message = xhr.responseJSON.message;
        }

        handleApiBanner(message);
        console.error(message, xhr);
    }

    function renderEmployeeSuggestions(items) {
        employeeSuggestionsList.empty();
        Object.keys(employeeSuggestionsMap).forEach(function (key) {
            delete employeeSuggestionsMap[key];
        });

        (items || []).forEach(item => {
            if (!item.label) {
                return;
            }

            if (item.employeeId) {
                employeeSuggestionsMap[item.label] = item.employeeId;
            }

            $('<option>', { value: item.label })
                .appendTo(employeeSuggestionsList);
        });
    }

    function loadEmployeeSuggestions() {
        const department = $('#departmentFilter').val() || '';
        if (!department) {
            renderEmployeeSuggestions([]);
            return;
        }

        $.ajax({
            url: '/api/AttendanceDashboardApi/employees/active',
            method: 'GET',
            data: { departamento: department }
        })
            .done(function (data) {
                renderEmployeeSuggestions(data);
            })
            .fail(function () {
                renderEmployeeSuggestions([]);
            });
    }

    function loadDepartments() {
        $.ajax({
            url: '/api/AttendanceDashboardApi/departments',
            method: 'GET'
        })
            .done(function (items) {
                const select = $('#departmentFilter');
                const current = select.val();
                select.empty().append('<option value="">Todos</option>');
                (items || []).forEach(function (item) {
                    select.append('<option value="' + item + '">' + item + '</option>');
                });
                if (current) {
                    select.val(current);
                }
            })
            .fail(function (xhr) {
                handleAjaxError(xhr, 'No se pudieron cargar los departamentos.');
            });
    }

    function loadSummary() {
        $.ajax({
            url: '/api/AttendanceDashboardApi/summary',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(getCurrentFilters())
        })
            .done(function (summary) {
                handleApiBanner(null);
                $('#totalRecordsMetric').text(summary.totalRecords || 0);
                $('#uniqueEmployeesMetric').text(summary.uniqueEmployees || 0);
                $('#departmentsMetric').text(summary.departments || 0);
                $('#absencesMetric').text(summary.absences || 0);
            })
            .fail(function (xhr) {
                $('#totalRecordsMetric, #uniqueEmployeesMetric, #departmentsMetric, #absencesMetric').text('0');
                handleAjaxError(xhr, 'No se pudo cargar el resumen de asistencia.');
            });
    }

    function loadComparisonChart() {
        const canvas = document.getElementById('attendanceComparisonChart');
        if (!canvas || typeof Chart === 'undefined') {
            return;
        }

        $.ajax({
            url: '/api/AttendanceDashboardApi/comparison-chart',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(getCurrentFilters())
        })
            .done(function (response) {
                handleApiBanner(null);

                if (comparisonChartInstance) {
                    comparisonChartInstance.destroy();
                }

                comparisonChartInstance = new Chart(canvas.getContext('2d'), {
                    type: 'bar',
                    data: {
                        labels: response.labels || [],
                        datasets: [
                            {
                                label: 'Totales',
                                data: response.totals || [],
                                backgroundColor: [
                                    'rgba(15, 118, 110, 0.72)',
                                    'rgba(220, 38, 38, 0.72)'
                                ],
                                borderColor: [
                                    'rgba(15, 118, 110, 1)',
                                    'rgba(220, 38, 38, 1)'
                                ],
                                borderWidth: 1,
                                borderRadius: 6
                            }
                        ]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: { display: false },
                            title: {
                                display: true,
                                text: response.chartTitle || 'Asistencias vs ausencias'
                            }
                        },
                        scales: {
                            y: {
                                beginAtZero: true,
                                ticks: { precision: 0 }
                            }
                        }
                    }
                });
            })
            .fail(function (xhr) {
                if (comparisonChartInstance) {
                    comparisonChartInstance.destroy();
                    comparisonChartInstance = null;
                }
                handleAjaxError(xhr, 'No se pudo cargar la grafica comparativa.');
            });
    }

    function reloadDashboard() {
        loadSummary();
        loadComparisonChart();
        historyTable.ajax.reload();
        absencesTable.ajax.reload();
    }

    $('#departmentFilter, #startDateFilter, #endDateFilter, #startTimeFilter, #endTimeFilter').on('change', function () {
        $('#employeeFilter').val('').data('employee-id', '');
        loadEmployeeSuggestions();
        reloadDashboard();
    });

    $('#employeeFilter').on('input', function () {
        $(this).data('employee-id', '');
        clearTimeout(employeeSuggestionsTimer);
        employeeSuggestionsTimer = setTimeout(function () {
            reloadDashboard();
        }, 300);
    });

    $('#employeeFilter').on('change', function () {
        const value = ($(this).val() || '').trim();
        $(this).data('employee-id', employeeSuggestionsMap[value] || '');
        reloadDashboard();
    });

    $('#employeeFilter').on('focus', loadEmployeeSuggestions);

    $('#btnReloadAttendance').on('click', function () {
        reloadDashboard();
    });

    $('#btnSyncAttendanceLines').on('click', function () {
        if (isSyncingAttendanceLines) {
            return;
        }

        const selectedDate = $('#startDateFilter').val() || today;
        const $button = $(this);
        const originalHtml = $button.html();
        isSyncingAttendanceLines = true;

        $button.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Sincronizando...');

        Swal.fire({
            title: 'Sincronizando personal',
            text: 'Se actualizara el personal de las lineas configuradas con base en la asistencia.',
            allowOutsideClick: false,
            allowEscapeKey: false,
            showConfirmButton: false,
            didOpen: function () {
                Swal.showLoading();
            }
        });

        $.ajax({
            url: '/api/AttendanceLineSyncApi/sync',
            method: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                date: selectedDate
            })
        })
            .done(function (response) {
                const successItems = (response.items || []).filter(function (item) { return item.success; });
                const failedItems = (response.items || []).filter(function (item) { return !item.success; });
                const changedItems = successItems.filter(function (item) {
                    return item.previousPeopleQuantity !== item.newPeopleQuantity;
                });

                let summary = '';
                if (changedItems.length) {
                    summary = changedItems
                        .map(function (item) {
                            const lineLabel = item.lineName || (item.lineNumber ? 'Linea ' + item.lineNumber : 'Linea ' + item.productionLineId);
                            return lineLabel + ': ' + item.previousPeopleQuantity + ' -> ' + item.newPeopleQuantity;
                        })
                        .join('\n');
                } else {
                    summary = 'No hubo cambios en PersonalQuantity para ninguna linea.';
                }

                Swal.fire({
                    icon: changedItems.length ? 'success' : 'info',
                    title: changedItems.length ? 'Lineas actualizadas' : 'Sin cambios',
                    text: summary
                });
            })
            .fail(function (xhr) {
                let message = 'No se pudo sincronizar el personal por linea.';
                if (xhr && xhr.responseJSON && xhr.responseJSON.message) {
                    message = xhr.responseJSON.message;
                }

                Swal.fire({
                    icon: 'error',
                    title: 'Error al sincronizar',
                    text: message
                });
            })
            .always(function () {
                isSyncingAttendanceLines = false;
                $button.prop('disabled', false).html(originalHtml);
            });
    });

    $('button[data-bs-toggle="tab"]').on('shown.bs.tab', function () {
        historyTable.columns.adjust();
        absencesTable.columns.adjust();
    });

    loadDepartments();
    loadEmployeeSuggestions();
    reloadDashboard();
});

$(document).ready(function () {
    let today = new Date().toISOString().split('T')[0];

    // Si no tienen valor, les asignamos hoy
    if (!$('#startDateFilter').val()) {
        $('#startDateFilter').val(today);
    }

    if (!$('#endDateFilter').val()) {
        $('#endDateFilter').val(today);
    }
    // Función para obtener líneas seleccionadas desde checkboxes
    function getSelectedLines() {
        const selectedLines = [];
        $('#lineFilterMenu input[type="checkbox"]:checked').each(function() {
            const value = parseInt($(this).val());
            if (!isNaN(value)) {
                selectedLines.push(value);
            }
        });
        return selectedLines;
    }

    // Función para actualizar el label del dropdown de líneas
    function updateLineFilterLabel() {
        const selectedLines = getSelectedLines();
        const $label = $('#lineFilterLabel');

        if (selectedLines.length === 0) {
            $label.text('Seleccione líneas');
        } else if (selectedLines.length === 1) {
            $label.text(`Línea ${selectedLines[0]}`);
        } else {
            $label.text(`${selectedLines.length} líneas seleccionadas`);
        }
    }

    // Variable global para almacenar la instancia del chart
    let paretoChartInstance = null;

    function getCurrentFilters() {
        return {
            areaFilter: $('#areaFilter').val(),
            lineFilters: getSelectedLines(),
            startDateFilter: $('#startDateFilter').val(),
            endDateFilter: $('#endDateFilter').val()
        };
    }

    // Función para cargar el Chart de Eficiencia
    function loadParetoChart() {
        // Verificar que el canvas existe
        const canvas = document.getElementById('paretoChart');
        if (!canvas) {
            console.error('No se encontró el elemento canvas con id "paretoChart"');
            return;
        }

        // Verificar que Chart.js está cargado
        if (typeof Chart === 'undefined') {
            console.error('Chart.js no está cargado');
            return;
        }

        $.ajax({
            url: '/api/EfficiencyApi/chart',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(getCurrentFilters()),
            success: function (response) {
                if (paretoChartInstance) {
                    paretoChartInstance.destroy();
                }

                const ctx = canvas.getContext('2d');

                paretoChartInstance = new Chart(ctx, {
                    type: 'bar',
                    data: {
                        labels: response.labels || [],
                        datasets: [
                            {
                                label: 'Horas Ganadas',
                                data: response.horasGanadas || [],
                                backgroundColor: 'rgba(54, 162, 235, 0.6)',
                                borderColor: 'rgba(54, 162, 235, 1)',
                                borderWidth: 1,
                                yAxisID: 'y'
                            },
                            {
                                label: 'Horas Utilizadas',
                                data: response.horasUtilizadas || [],
                                backgroundColor: 'rgba(255, 159, 64, 0.6)',
                                borderColor: 'rgba(255, 159, 64, 1)',
                                borderWidth: 1,
                                yAxisID: 'y'
                            },
                            {
                                label: '% Eficiencia',
                                data: response.efficiencyPercentages || [],
                                type: 'line',
                                borderColor: 'rgba(255, 99, 132, 1)',
                                backgroundColor: 'rgba(255, 99, 132, 0.2)',
                                borderWidth: 2,
                                fill: false,
                                yAxisID: 'y1',
                                tension: 0.4
                            }
                        ]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        interaction: {
                            mode: 'index',
                            intersect: false
                        },
                        plugins: {
                            title: {
                                display: true,
                                text: 'Eficiencia por Día'
                            },
                            legend: {
                                display: true,
                                position: 'top'
                            },
                            tooltip: {
                                callbacks: {
                                    label: function(context) {
                                        let label = context.dataset.label || '';
                                        if (label) {
                                            label += ': ';
                                        }
                                        if (context.dataset.type === 'line') {
                                            label += context.parsed.y.toFixed(1) + '%';
                                        } else {
                                            label += context.parsed.y.toFixed(2) + ' horas';
                                        }
                                        return label;
                                    }
                                }
                            }
                        },
                        scales: {
                            y: {
                                type: 'linear',
                                display: true,
                                position: 'left',
                                title: {
                                    display: true,
                                    text: 'Horas'
                                },
                                beginAtZero: true
                            },
                            y1: {
                                type: 'linear',
                                display: true,
                                position: 'right',
                                title: {
                                    display: true,
                                    text: 'Eficiencia (%)'
                                },
                                min: 0,
                                max: 100,
                                grid: {
                                    drawOnChartArea: false
                                }
                            }
                        }
                    }
                });
            },
            error: function (xhr, status, error) {
                console.error('Error al cargar gráfica de eficiencia:', error, xhr.responseText);
                if (paretoChartInstance) {
                    paretoChartInstance.destroy();
                    paretoChartInstance = null;
                }
            }
        });
    }

    productionLineTable = $('#DataTable').DataTable({
        ordering: true,
        searching: false,
        dom: 'frtip',
        order: [], // Remove default ordering to avoid conflicts with server-side processing
        processing: true,
        serverSide: true,
        ajax: {
            url: '/api/EfficiencyApi/table',
            type: 'POST',
            data: function (d) {
                // Obtener array de líneas seleccionadas desde checkboxes
                const lineFilters = getSelectedLines();
                d.areaFilter = $('#areaFilter').val();
                d.lineFilters = lineFilters;
                d.startDateFilter = $('#startDateFilter').val();
                d.endDateFilter = $('#endDateFilter').val();
            },
            dataSrc: function(json) {
                return json.data;
            },
            error: function (xhr, error, code) {
                console.error('DataTables AJAX error:', error, code, xhr.responseText);
            }
        },
        columns: [
            {
                data: 'fecha',
                orderable: true,
                render: function (data, type, row) {
                    if (type === 'display' || type === 'type') {
                        return data && data !== "N/A"
                            ? data.split('-').reverse().join('/')
                            : '—';
                    }
                    return data;
                }
            },
            
            {
                data: 'area',
                orderable: true
            },
            {
                data: 'linea',
                className: 'text-center',
                orderable: true
            },
            {
                data: 'peopleQuantity',
                className: 'text-center',
                orderable: true
            },
            {
                data: 'producedPieces',
                className: 'text-center',
                orderable: true
            },
            {
                data: 'tiempoEstandar',
                className: 'text-center',
                orderable: true,
                render: function (data) {
                    return Number(data || 0).toFixed(4);
                }
            },
            {
                data: 'horasGanadas',
                className: 'text-center',
                orderable: true,
                render: function (data) {
                    return Number(data || 0).toFixed(2);
                }
            },
            {
                data: 'horasUtilizadas',
                className: 'text-center',
                orderable: true,
                render: function (data) {
                    return Number(data || 0).toFixed(2);
                }
            },
            {
                data: 'efficiencyPercentage',
                className: 'text-center',
                orderable: true,
                render: function (data) {
                    return Number(data || 0).toFixed(2) + '%';
                }
            },
            
        ],
    });

    // Filtro dinámico de líneas según área
    $('#areaFilter').on('change', function () {
        const selectedArea = $(this).val();
        const $lineFilterMenu = $('#lineFilterMenu');
        $lineFilterMenu.empty();

        if (selectedArea) {
            $.get(`/QualityDashboard/GetLinesByArea?area=${encodeURIComponent(selectedArea)}`, function (lines) {
                $lineFilterMenu.empty();

                // Crear checkboxes para cada línea
                lines.forEach(line => {
                    if (line.value !== "") { // Ignorar la opción "Todas las líneas"
                        const checkboxId = `lineCheckbox_${line.value}`;
                        const checkboxHtml = `
                            <li class="form-check" onclick="event.stopPropagation();">
                                <input class="form-check-input line-checkbox" type="checkbox" value="${line.value}" id="${checkboxId}">
                                <label class="form-check-label" for="${checkboxId}">
                                    ${line.text}
                                </label>
                            </li>
                        `;
                        $lineFilterMenu.append(checkboxHtml);
                    }
                });

                // Agregar opción "Seleccionar todas" al inicio
                if (lines.length > 1) {
                    const selectAllHtml = `
                        <li class="form-check border-bottom" onclick="event.stopPropagation();">
                            <input class="form-check-input" type="checkbox" id="selectAllLines">
                            <label class="form-check-label fw-bold" for="selectAllLines">
                                Seleccionar todas
                            </label>
                        </li>
                    `;
                    $lineFilterMenu.prepend(selectAllHtml);
                }

                // Event handler para checkboxes individuales
                $('.line-checkbox').on('change', function() {
                    updateLineFilterLabel();
                    loadParetoChart();
                    productionLineTable.ajax.reload();
                });

                // Event handler para "Seleccionar todas"
                $('#selectAllLines').on('change', function() {
                    const isChecked = $(this).is(':checked');
                    $('.line-checkbox').prop('checked', isChecked);
                    updateLineFilterLabel();
                    loadParetoChart();
                    productionLineTable.ajax.reload();
                });
            });
        } else {
            $lineFilterMenu.html('<li class="px-3 py-2 text-muted">Seleccione un área primero</li>');
            updateLineFilterLabel();
        }

        loadParetoChart();
        productionLineTable.ajax.reload();
    });

    $('#areaFilter, #startDateFilter, #endDateFilter')
        .on('change keyup', function () {

            loadParetoChart();
            productionLineTable.ajax.reload();
        });

    let isExporting = false; // Variable para prevenir múltiples clics

    $('#btnQualityExport').on('click', function () {
        // Prevenir múltiples clics mientras se procesa
        if (isExporting) {
            return;
        }

        // Marcar como en proceso de exportación
        isExporting = true;

        // Deshabilitar el botón temporalmente
        const $exportBtn = $(this);
        const originalText = $exportBtn.html();
        $exportBtn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-1"></i>Exportando...');

        // Mostrar modal de carga
        Swal.fire({
            title: 'Generando archivo...',
            text: 'Por favor espere mientras se genera y descarga el reporte',
            allowOutsideClick: false,
            allowEscapeKey: false,
            showConfirmButton: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });

        // Función para restaurar el estado del botón
        function resetExportState() {
            isExporting = false;
            $exportBtn.prop('disabled', false).html(originalText);
        }

        const filters = getCurrentFilters();
        const exportPayload = {
            areaFilter: filters.areaFilter,
            lineFilters: filters.lineFilters.join(','),
            startDateFilter: filters.startDateFilter,
            endDateFilter: filters.endDateFilter,
            chartImageBase64: paretoChartInstance ? paretoChartInstance.toBase64Image('image/png', 1) : null
        };

        fetch('/api/EfficiencyApi/export', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(exportPayload)
        })
            .then(async response => {
                if (!response.ok) {
                    let errorMessage = 'No se pudo exportar el reporte de eficiencia';
                    try {
                        const errorData = await response.json();
                        errorMessage = errorData.message || errorData.error || errorMessage;
                    } catch {
                        // Ignorar si la respuesta no es JSON.
                    }
                    throw new Error(errorMessage);
                }

                const blob = await response.blob();
                const downloadUrl = window.URL.createObjectURL(blob);
                const link = document.createElement('a');
                const todayLabel = new Date().toISOString().slice(0, 10).replace(/-/g, '');

                link.href = downloadUrl;
                link.download = `ReporteEficiencia_${todayLabel}.xlsx`;
                document.body.appendChild(link);
                link.click();
                link.remove();
                window.URL.revokeObjectURL(downloadUrl);

                Swal.fire({
                    icon: 'success',
                    title: 'Exportación completada',
                    text: 'Se descargó el reporte con la tabla y la gráfica filtradas.',
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
                    text: error.message || 'No se pudo generar el archivo de eficiencia.'
                });
            })
            .finally(() => {
                Swal.close();
                resetExportState();
            });
    });

    // Cargar gráfica inicial
    loadParetoChart();
});

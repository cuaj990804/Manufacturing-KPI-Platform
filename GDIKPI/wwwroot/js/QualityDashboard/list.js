$(document).ready(function () {
    let today = new Date().toISOString().split('T')[0];
    let currentOEEData = {};

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

    // Función para obtener programas seleccionados desde checkboxes
    function getSelectedPrograms() {
        const selectedPrograms = [];
        $('#programFilterMenu input[type="checkbox"]:checked').each(function() {
            const value = $(this).val();
            if (value && value !== "") {
                selectedPrograms.push(value);
            }
        });
        return selectedPrograms;
    }

    // Función para actualizar el label del dropdown de programas
    function updateProgramFilterLabel() {
        const selectedPrograms = getSelectedPrograms();
        const $label = $('#programFilterLabel');

        if (selectedPrograms.length === 0) {
            $label.text('Seleccione programas');
        } else if (selectedPrograms.length === 1) {
            const programName = $(`#programCheckbox_${selectedPrograms[0]}`).next('label').text().trim();
            $label.text(programName);
        } else {
            $label.text(`${selectedPrograms.length} programas seleccionados`);
        }
    }

    function loadKPIs() {
        // Obtener array de líneas y programas seleccionados desde checkboxes
        const lineFilters = getSelectedLines();
        const programFilters = getSelectedPrograms();

        $.ajax({
            url: '/api/QualityDashboardApi/GetQualityKpis',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                areaFilter: $('#areaFilter').val(),
                lineFilters: lineFilters,
                defectCategoryFilter: $('#defectCategoryFilter').val(),
                programFilters: programFilters,
                defectFilter: $('#defectFilter').val(),
                startDateFilter: $('#startDateFilter').val(),
                endDateFilter: $('#endDateFilter').val(),
                startTimeFilter: $('#startTimeFilter').val(),
                endTimeFilter: $('#endTimeFilter').val()
            }),
            success: function (response) {
                currentOEEData = response;

                // Aplicar colores usando la función modificada
                applyKPIColor('qualityTotal', 'Overall', response.qualityPercentage, 'Quality');
                applyKPIColor('reworksTotal', 'Reject', response.reWorkPercentage, 'Quality');
        
                applyKPIColor('scrapTotal', 'Scrap', response.scrapPercentage, 'Quality');

                $('#producedQuantities').text(response.totalProduction || 0);

                // Actualizar piezas rechazadas (sin color, solo número)
                $('#rejectedPieces').text(response.totalRejects || 0);
                $('#scrapQuantities').text(response.totalScrap || 0);
                $('#totalDefects').text(response.totalDefects || 0);
            },
            error: function (xhr, status, error) {
                console.error('Error al cargar KPIs:', error, xhr.responseText);

            }
        });
    }

    // Variable global para almacenar la instancia del chart
    let paretoChartInstance = null;

    // Función para cargar el Pareto Chart
    function loadParetoChart() {
        // Obtener array de líneas y programas seleccionados desde checkboxes
        const lineFilters = getSelectedLines();
        const programFilters = getSelectedPrograms();

        $.ajax({
            url: '/api/QualityDashboardApi/GetParetoByDefect',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({
                areaFilter: $('#areaFilter').val(),
                lineFilters: lineFilters,
                defectCategoryFilter: $('#defectCategoryFilter').val(),
                programFilters: programFilters,
                defectFilter: $('#defectFilter').val(),
                startDateFilter: $('#startDateFilter').val(),
                endDateFilter: $('#endDateFilter').val(),
                startTimeFilter: $('#startTimeFilter').val(),
                endTimeFilter: $('#endTimeFilter').val()
            }),
            success: function (data) {
                // Destruir el chart anterior si existe
                if (paretoChartInstance) {
                    paretoChartInstance.destroy();
                }

                // Preparar datos para el chart
                const labels = data.map(d => d.defectName);
                const quantities = data.map(d => d.quantity);
                const percentages = data.map(d => d.percentage);

                // Crear el chart
                const ctx = document.getElementById('paretoChart').getContext('2d');
                paretoChartInstance = new Chart(ctx, {
                    type: 'bar',
                    data: {
                        labels: labels,
                        datasets: [
                            {
                                label: 'Cantidad de Defectos',
                                data: quantities,
                                backgroundColor: 'rgba(54, 162, 235, 0.6)',
                                borderColor: 'rgba(54, 162, 235, 1)',
                                borderWidth: 1,
                                yAxisID: 'y'
                            },
                            {
                                label: 'Porcentaje',
                                data: percentages,
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
                                text: 'Diagrama de Pareto - Defectos'
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
                                        if (context.datasetIndex === 0) {
                                            label += context.parsed.y + ' defectos';
                                        } else {
                                            label += context.parsed.y.toFixed(2) + '%';
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
                                    text: 'Cantidad de Defectos'
                                },
                                beginAtZero: true
                            },
                            y1: {
                                type: 'linear',
                                display: true,
                                position: 'right',
                                title: {
                                    display: true,
                                    text: 'Porcentaje (%)'
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
                console.error('Error al cargar Pareto Chart:', error, xhr.responseText);
            }
        });
    }

    // Función para cargar defectos cuando hay área y categoría
    function loadDefects() {
        const selectedArea = $('#areaFilter').val();
        const selectedDefectCategory = $('#defectCategoryFilter').val();
        const $defectFilter = $('#defectFilter');

        // Limpiar filtro de defectos
        $defectFilter.empty();
        //$defectFilter.append('<option value="">Todos los defectos</option>');

        // Solo cargar defectos si hay área seleccionada
        if (selectedArea) {
            $.get(`/QualityDashboard/GetDefectByCategory?area=${encodeURIComponent(selectedArea)}&category=${encodeURIComponent(selectedDefectCategory || '')}`, function (defects) {
                $defectFilter.empty();
               // $defectFilter.append('<option value="">Todos los defectos</option>'); // Agregado
                defects.forEach(defect => {
                    $defectFilter.append(`<option value="${defect.value}">${defect.text}</option>`);
                });
            }).fail(function (xhr, status, error) {
                console.error('Error loading defects:', error);
                $defectFilter.html('<option value="">Error al cargar defectos</option>');
            });
        }
    }
    // Función para cargar programas por área
    function loadProgramsByArea(area) {
        const $programFilterMenu = $('#programFilterMenu');
        const $programFilterLabel = $('#programFilterLabel');

        // Limpiar y mostrar estado de carga
        $programFilterMenu.html('<li class="px-3 py-2 text-muted">Cargando programas...</li>');
        $programFilterLabel.text('Cargando...');

        // Si no hay área seleccionada, mostrar mensaje por defecto
        if (!area) {
            $programFilterMenu.html('<li class="px-3 py-2 text-muted">Seleccione un área primero</li>');
            $programFilterLabel.text('Seleccione programas');
            return;
        }

        $.ajax({
            url: '/QualityDashboard/GetProgramsByArea',
            type: 'GET',
            data: { area: area },
            timeout: 30000, // 30 segundos de timeout
            success: function (data) {
                $programFilterMenu.empty();

                if (data && data.length > 0) {
                    // Filtrar la opción "Todos los programas" si existe
                    const programs = data.filter(p => (p.id || p.Id || p.ID || '') !== '');

                    if (programs.length > 0) {
                        // Crear checkboxes para cada programa
                        programs.forEach(program => {
                            const programId = program.id || program.Id || program.ID || '';
                            const programName = program.program || program.Program || program.ProgramName || 'Programa sin nombre';
                            const programType = program.type || program.Type || '';

                            const checkboxId = `programCheckbox_${programId}`;
                            const checkboxHtml = `
                                <li class="form-check" onclick="event.stopPropagation();">
                                    <input class="form-check-input program-checkbox" type="checkbox" value="${programId}" id="${checkboxId}" data-type="${programType}">
                                    <label class="form-check-label" for="${checkboxId}">
                                        ${programName}
                                    </label>
                                </li>
                            `;
                            $programFilterMenu.append(checkboxHtml);
                        });

                        // Agregar opción "Seleccionar todos" al inicio si hay más de un programa
                        if (programs.length > 1) {
                            const selectAllHtml = `
                                <li class="form-check border-bottom" onclick="event.stopPropagation();">
                                    <input class="form-check-input" type="checkbox" id="selectAllPrograms">
                                    <label class="form-check-label fw-bold" for="selectAllPrograms">
                                        Seleccionar todos
                                    </label>
                                </li>
                            `;
                            $programFilterMenu.prepend(selectAllHtml);
                        }

                        // Event handler para checkboxes individuales
                        $('.program-checkbox').on('change', function() {
                            updateProgramFilterLabel();
                            loadKPIs();
                            loadParetoChart();
                            productionLineTable.ajax.reload();
                        });

                        // Event handler para "Seleccionar todos"
                        $('#selectAllPrograms').on('change', function() {
                            const isChecked = $(this).is(':checked');
                            $('.program-checkbox').prop('checked', isChecked);
                            updateProgramFilterLabel();
                            loadKPIs();
                            loadParetoChart();
                            productionLineTable.ajax.reload();
                        });

                        updateProgramFilterLabel();
                    } else {
                        $programFilterMenu.html('<li class="px-3 py-2 text-muted">No hay programas disponibles</li>');
                        $programFilterLabel.text('No hay programas');
                    }
                } else {
                    // No hay programas disponibles
                    $programFilterMenu.html('<li class="px-3 py-2 text-muted">No hay programas disponibles</li>');
                    $programFilterLabel.text('No hay programas');
                }
            },
            error: function (xhr, status, error) {
                console.error('Error al cargar programas:', {
                    status: status,
                    error: error,
                    responseText: xhr.responseText,
                    area: area
                });

                // Mostrar mensaje de error apropiado según el tipo de error
                let errorMessage = 'Error al cargar programas';
                if (status === 'timeout') {
                    errorMessage = 'Tiempo de espera agotado';
                } else if (status === 'error' && xhr.status === 0) {
                    errorMessage = 'Error de conexión';
                }

                $programFilterMenu.html(`<li class="px-3 py-2 text-danger">${errorMessage}</li>`);
                $programFilterLabel.text(errorMessage);
            }
        });
    }
    function applyKPIColor(kpiId, metricName, value, metricType) {
        const numericValue = parseFloat(value);
        $.get(`/api/QualityDashboardApi/GetCategory?metricName=${metricName}&metricType=${metricType}`)
            .done(function (params) {
                let colorClass = 'text-secondary';

                // Si es metricType "Quality", aplicar lógica específica
                if (metricType && metricType.toLowerCase() === 'quality') {
                    let passesQuality = false;

                    for (let p of params) {
                        const min = parseFloat(p.minValue);
                        const max = parseFloat(p.maxValue);

                        // Lógica diferente según el tipo de métrica
                        if (metricName === 'Overall') {
                            // Para Calidad Overall: Verde si está dentro del rango permitido
                            if (numericValue >= min && numericValue <= max) {
                                passesQuality = true;
                                break;
                            }
                        } else if (metricName === 'Reject' || metricName === 'Scrap') {
                            // Para Rechazo y Scrap: Verde si está DEBAJO del límite máximo
                            if (numericValue <= max) {
                                passesQuality = true;
                                break;
                            }
                        }
                    }
                    
                    colorClass = passesQuality ? 'text-success' : 'text-danger';
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
    productionLineTable = $('#DataTable').DataTable({
        ordering: true,
        searching: false,
        dom: 'frtip',
        order: [], // Remove default ordering to avoid conflicts with server-side processing
        processing: true,
        serverSide: true,
        ajax: {
            url: '/api/QualityDashboardApi/SetTable',
            type: 'POST',
            data: function (d) {
                // Obtener array de líneas seleccionadas desde checkboxes
                const lineFilters = getSelectedLines();
                const programFilters = getSelectedPrograms();

                d.areaFilter = $('#areaFilter').val();
                d.lineFilters = lineFilters;
                d.defectCategoryFilter = $('#defectCategoryFilter').val();
                d.programFilters = programFilters;
                d.defectFilter = $('#defectFilter').val();
                d.startDateFilter = $('#startDateFilter').val();
                d.endDateFilter = $('#endDateFilter').val();
                d.startTimeFilter = $('#startTimeFilter').val();
                d.endTimeFilter = $('#endTimeFilter').val();
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
                data: 'tiempo',
                className: 'text-center',
                orderable: true
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
                data: 'programDescription',
                className: 'text-center',
                orderable: true,
                render: function (data, type, row) {
                    return data || 'N/A';
                }
            },
            {
                data: 'defectName',
                orderable: true
            },
            {
                data: 'cantidad',
                className: 'text-center',
                orderable: true
            }
        ],
    });

    $('#btnQualityParameters').on('click', function () {
        $.get('/QualityDashboard/AddQualityParamModal', function (html) {
            $('#AddQualityParamModal .modal-content').html(html);
            $('#AddQualityParamModal').modal('show');
        });
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
                    loadKPIs();
                    loadParetoChart();
                    productionLineTable.ajax.reload();
                });

                // Event handler para "Seleccionar todas"
                $('#selectAllLines').on('change', function() {
                    const isChecked = $(this).is(':checked');
                    $('.line-checkbox').prop('checked', isChecked);
                    updateLineFilterLabel();
                    loadKPIs();
                    loadParetoChart();
                    productionLineTable.ajax.reload();
                });
            });
            loadProgramsByArea(selectedArea);
        } else {
            $lineFilterMenu.html('<li class="px-3 py-2 text-muted">Seleccione un área primero</li>');
            updateLineFilterLabel();
        }

        loadDefects();
        loadKPIs();
        loadParetoChart();
        productionLineTable.ajax.reload();
    });

    // Evento cuando cambia la categoría de defecto
    $('#defectCategoryFilter').on('change', function () {
        // Cargar defectos cuando cambie la categoría
        loadDefects();

        // Recargar datos
        loadKPIs();
        loadParetoChart();
        productionLineTable.ajax.reload();
    });

    // Filtros (TODOS los inputs del header)
    // Nota: lineFilter y programFilter ya no se incluyen aquí porque ahora usan checkboxes con sus propios eventos
    // Nota: defectCategoryFilter tiene su propio evento específico arriba
    $('#areaFilter, #defectFilter, #startDateFilter, #endDateFilter, #startTimeFilter, #endTimeFilter')
        .on('change keyup', function () {

            loadKPIs();
            loadParetoChart();
            productionLineTable.ajax.reload();
        });

    let isExporting = false; // Variable para prevenir múltiples clics

    $('#btnQualityExport').on('click', function () {
        // Prevenir múltiples clics mientras se procesa
        if (isExporting) {
            return;
        }

        // Validar que se haya seleccionado un área
        const selectedArea = $('#areaFilter').val();
        if (!selectedArea || selectedArea.trim() === '') {
            Swal.fire({
                icon: 'warning',
                title: 'Área requerida',
                text: 'Debe seleccionar un área antes de exportar el reporte',
                confirmButtonText: 'Entendido',
                confirmButtonColor: '#3085d6'
            });
            return;
        }

        // Obtener líneas y programas seleccionados para validación temprana
        const lineFilters = getSelectedLines();
        const programFilters = getSelectedPrograms();

        // Nota: Ya no se requiere que haya líneas seleccionadas
        // Se permite exportar con solo programas seleccionados

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

        // Ya tenemos lineFilters y programFilters de la validación anterior
        // No es necesario volver a obtenerlos

        // Obtener todos los programas seleccionados y sus nombres
        const programNames = [];
        const programTypes = [];
        programFilters.forEach(programId => {
            const checkbox = $(`#programCheckbox_${programId}`);
            if (checkbox.length) {
                programNames.push(checkbox.next('label').text().trim());
                programTypes.push(checkbox.data('type') || '');
            }
        });

        let params = {
            areaFilter: selectedArea,
            lineFilters: lineFilters.join(','), // Convertir array a string separado por comas
            defectCategoryFilter: $('#defectCategoryFilter').val(),
            defectFilter: $('#defectFilter').val(),
            startDateFilter: $('#startDateFilter').val(),
            endDateFilter: $('#endDateFilter').val(),
            startTimeFilter: $('#startTimeFilter').val(),
            endTimeFilter: $('#endTimeFilter').val(),
            // Quality Dashboard
            qualityPercentage: currentOEEData.qualityPercentage || 0,
            reWorkPercentage: currentOEEData.reWorkPercentage || 0,
            scrapPercentage: currentOEEData.scrapPercentage || 0,
            totalProduction: currentOEEData.totalProduction || 0,
            totalRejects: currentOEEData.totalRejects || 0,
            totalScrap: currentOEEData.totalScrap || 0,
            // Programs (enviar todos los programas seleccionados)
            programFilters: programFilters.join(','),
            programNamesFilter: programNames.join(', ') || 'Todos los programas',
            programTypesFilter: programTypes.join(',')
        };

        let query = $.param(params);

        // Función para restaurar el estado del botón
        function resetExportState() {
            isExporting = false;
            $exportBtn.prop('disabled', false).html(originalText);
        }

        // Crear un enlace temporal para descarga directa
        const downloadUrl = '/api/QualityDashboardApi/ExportQualityToExcel?' + query;

        // Crear iframe oculto para la descarga
        const downloadFrame = $('<iframe/>', {
            id: 'downloadFrame',
            src: downloadUrl,
            style: 'display:none;'
        }).appendTo('body');

        // Función más simple para manejar la finalización
        function finishDownload() {
            Swal.close();
            resetExportState();
            downloadFrame.remove();
        }

        // Esperar tiempo suficiente para que se genere el archivo Excel
        // Con filtros de área + programas puede haber muchos datos
        setTimeout(() => {
            finishDownload();

            // Mostrar mensaje de éxito
            Swal.fire({
                icon: 'success',
                title: '¡Descarga completada!',
                text: 'El archivo Excel se ha descargado correctamente',
                timer: 3000,
                showConfirmButton: false,
                toast: true,
                position: 'top-end'
            });
        }, 15000); // 15 segundos para dar tiempo a la generación

        // Fallback de seguridad por si tarda más de lo esperado
        setTimeout(() => {
            if (isExporting) {
                finishDownload();

                Swal.fire({
                    icon: 'info',
                    title: 'Descarga en proceso',
                    text: 'El archivo puede tardar unos momentos en aparecer',
                    timer: 3000,
                    showConfirmButton: false,
                    toast: true,
                    position: 'top-end'
                });
            }
        }, 30000); // Fallback a 30 segundos
    });

    // Cargar KPIs y Pareto iniciales
    loadKPIs();
    loadParetoChart();
});

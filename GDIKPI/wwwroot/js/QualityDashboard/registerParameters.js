// Escuchamos cuando cambien Métrica o Categoría
$(document).on("change", "#metricNameQuality, #categoryQuality", function () {
    let metricName = $("#metricNameQuality").val();
    let category = $("#categoryQuality").val();

    if (metricName && category) {
        $.ajax({
            url: '/api/QualityDashboardApi/QualityPercentage',
            type: 'GET',
            data: { metricName: metricName, category: category },
            success: function (data) {
                // Aseguramos valores por defecto si vienen nulos
                const minValue = data?.minValue ?? '';
                const maxValue = data?.maxValue ?? '';

                // Rellenamos los valores en el contenedor dinámico
                $("#ParametersContainer").html(`
                    <div class="mb-3">
                        <label for="minValue" class="form-label">Valor Mínimo (%)</label>
                        <input type="number" step="0.01" class="form-control" 
                               id="minValue" name="MinValue" 
                               value="${minValue}" required>
                    </div>
                    <div class="mb-3">
                        <label for="maxValue" class="form-label">Valor Máximo (%)</label>
                        <input type="number" step="0.01" class="form-control" 
                               id="maxValue" name="MaxValue" 
                               value="${maxValue}" required>
                    </div>
                `);
            },
            error: function (xhr) {
                let msg = xhr.responseJSON?.message || "No se pudieron cargar los parámetros";
                $("#ParametersContainer").html(`
                    <div class="alert alert-warning">${msg}</div>
                `);
            }
        });
    } else {
        $("#ParametersContainer").empty();
    }
});


// Escuchamos el submit del formulario del modal
$(document).on("submit", "#AddQualityParamForm", function (e) {
    e.preventDefault();

    let metricName = $("#metricNameQuality").val();
    let category = $("#categoryQuality").val();
    let minValue = parseFloat($("#minValue").val());
    let maxValue = parseFloat($("#maxValue").val());

    if (!metricName || !category) {
        Swal.fire({
            icon: 'warning',
            title: 'Faltan datos',
            text: 'Selecciona una métrica y una categoría.',
            confirmButtonColor: '#f39c12'
        });
        return;
    }

    // Validación rápida de los valores
    if (isNaN(minValue) || isNaN(maxValue)) {
        Swal.fire({
            icon: 'warning',
            title: 'Valores inválidos',
            text: 'Los valores mínimo y máximo deben ser numéricos.',
            confirmButtonColor: '#f39c12'
        });
        return;
    }

    $.ajax({
        url: '/api/QualityDashboardApi/UpdateParameters',
        type: 'POST',
        data: {
            MetricName: metricName,
            Category: category,
            MinValue: minValue,
            MaxValue: maxValue
        },
        success: function (res) {
            Swal.fire({
                icon: 'success',
                title: '¡Éxito!',
                text: res.message || 'Parámetro actualizado correctamente.',
                confirmButtonColor: '#28a745'
            }).then(() => {
                $("#AddOEEParamModal").modal('hide');
            });
        },
        error: function (xhr) {
            let msg = xhr.responseJSON?.message || xhr.responseText || "Error al actualizar el parámetro";
            Swal.fire({
                icon: 'error',
                title: 'Error',
                text: msg,
                confirmButtonColor: '#d33'
            });
            console.log(msg);
        }
    });


});


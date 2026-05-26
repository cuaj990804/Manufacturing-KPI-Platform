$(document).on("submit", "#editProductionForm", async function (e) {
    e.preventDefault();

    // Obtener valores
    const id = document.getElementById("editProductionId").value;
    const line = document.getElementById("editProductionLine").value;
    const date = document.getElementById("editProductionDate").value;
    const interval = document.getElementById("editTimeInterval").value;
    const produced = document.getElementById("editProducedPieces").value;


    // Validaciones
    if (!line) {
        alert("Selecciona una línea de producción");
        return;
    }
    if (!date) {
        alert("Selecciona una fecha");
        return;
    }
    if (!interval || !interval.includes("-")) {
        alert("Selecciona un intervalo de hora válido");
        return;
    }

    // Procesar intervalo y convertir a formato de tiempo completo
    const [startHour, endHour] = interval.split("-");

    // Obtener valores ocultos del modelo para mantener los datos del programa
    const programId = document.getElementById("editProgramId")?.value || 0;
    const programDescription = document.getElementById("editProgramDescription")?.value || null;

    // Construir DTO con formatos correctos para .NET
    const dto = {
        productionId: parseInt(id),
        productionLinesId: parseInt(line),
        productionDate: date, // Formato YYYY-MM-DD es válido para DateOnly
        startHour: startHour + ":00", // Agregar segundos para TimeOnly
        endHour: endHour + ":00",     // Agregar segundos para TimeOnly
        producedPieces: produced ? parseInt(produced) : null,
        programId: parseInt(programId),
        programDescription: programDescription
    };


    try {
        const response = await fetch(`/api/ProductionDataApi/${id}`, {
            method: "PUT",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(dto)
        });

        if (response.ok) {

            showAlert("success", "¡Éxito!", "Datos actualizados correctamente.");
            if (productionTable) {
                productionTable.ajax.reload(null, false);
            }


            // Cerrar modal y refrescar
            const modal = bootstrap.Modal.getInstance(document.querySelector('.modal'));
            if (modal) modal.hide();

            if (typeof loadProductionData === 'function') {
                loadProductionData();
            }
        } else {
            let errorMessage = "Error al actualizar";
            try {
                const error = await response.json();
                errorMessage = error.message || errorMessage;
            } catch (jsonError) {
                errorMessage = `Error ${response.status}: ${response.statusText}`;
            }
            console.error("Error del servidor:", response.status, response.statusText);
            alert(errorMessage);
        }
    } catch (error) {
        console.error("Error:", error);
        alert("Error de conexión. Intenta nuevamente.");
    }
});
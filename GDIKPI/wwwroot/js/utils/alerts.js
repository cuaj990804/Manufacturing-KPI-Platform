function showAlert(type, title, text) {
    const config = {
        icon: type,
        title: title,
        text: text,
        timerProgressBar: true,
    };

    if (type === 'success') {
        // Alerta sin botón que se cierra sola después de 2 seg
        config.timer = 2000;
        config.showConfirmButton = false;
    } else if (type === 'error') {
        // Alerta con botón de confirmación (sin timer)
        config.showConfirmButton = true;
    } else if (type === 'info' || type === 'warning') {
        // También con botón de confirmación, sin timer
        config.showConfirmButton = true;
    } else {
        // Por defecto botón y sin timer
        config.showConfirmButton = true;
    }

    Swal.fire(config);
}

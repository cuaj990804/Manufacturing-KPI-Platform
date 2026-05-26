function clearCreateForm(formId) {
    const form = document.getElementById(formId);
    if (!form) return;

    // Limpiar inputs y selects dentro del formulario, excepto date y time
    form.querySelectorAll('input, select, textarea').forEach(element => {
        if (element.id === 'createTimeInterval') return;
        switch (element.type) {
            case 'text':
            case 'number':
            case 'textarea':
                element.value = '';
                break;
            case 'select-one':
                element.selectedIndex = 0; // Selecciona la primera opción
                break;
            case 'checkbox':
            case 'radio':
                element.checked = false;
                break;
          
        }
    });
}

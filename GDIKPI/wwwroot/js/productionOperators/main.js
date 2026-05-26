const employeeInput = document.getElementById('employeeInput');
const volanteInput = document.getElementById('volanteInput');
const employeeStatus = document.getElementById('employeeStatus');
const volanteStatus = document.getElementById('volanteStatus');
const recentScansList = document.getElementById('recentScansList');
const body = document.body;

const endpoints = {
    validateEmployee: '/ProductionOperators/ValidateEmployee',
    saveScan: '/ProductionOperators/SaveScan',
    recentScans: '/ProductionOperators/GetRecentScans',
    scansSummary: '/ProductionOperators/GetScansSummary'
};

let employeeValid = false;
let validatedEmployeeNumber = null;

employeeInput.focus();
loadRecentScans();

employeeInput.addEventListener('keydown', async function (e) {
    if (e.key !== 'Enter') {
        return;
    }

    const employeeNumber = employeeInput.value.trim();

    if (employeeNumber.length === 0) {
        return;
    }

    await validateEmployee(employeeNumber);
});

volanteInput.addEventListener('keydown', async function (e) {
    if (e.key !== 'Enter') {
        return;
    }

    const code = volanteInput.value.trim();

    if (code.length < 10) {
        body.classList.remove('success');
        body.classList.add('error');
        setVolanteStatus('Codigo invalido', 'error-msg');
        setTimeout(() => {
            body.classList.remove('error');
        }, 3000);
        return;
    }

    if (!employeeValid || validatedEmployeeNumber === null) {
        return;
    }

    await saveScan(code);
});

volanteInput.addEventListener('focus', function () {
    if (!employeeValid) {
        employeeInput.focus();
    }
});

async function validateEmployee(employeeNumber) {
    setEmployeeStatus('Validando empleado...', '');
    employeeInput.disabled = true;

    try {
        const response = await fetch(`${endpoints.validateEmployee}?employeeNumber=${encodeURIComponent(employeeNumber)}`);
        const result = await response.json();

        if (!response.ok || !result.success) {
            employeeValid = false;
            validatedEmployeeNumber = null;
            volanteInput.disabled = true;
            setEmployeeStatus(result.message || 'Empleado invalido', 'error-msg');
            employeeInput.select();
            return;
        }

        employeeValid = true;
        validatedEmployeeNumber = Number(employeeNumber);
        setEmployeeStatus('Empleado verificado', 'success-msg');
        volanteInput.disabled = false;
        setTimeout(() => volanteInput.focus(), 150);
    } catch (error) {
        employeeValid = false;
        validatedEmployeeNumber = null;
        volanteInput.disabled = true;
        setEmployeeStatus('Error al validar empleado', 'error-msg');
        employeeInput.select();
    } finally {
        employeeInput.disabled = false;
    }
}

async function saveScan(code) {
    setVolanteStatus('Guardando codigo...', '');
    volanteInput.disabled = true;

    try {
        const response = await fetch(endpoints.saveScan, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                employeeNumber: validatedEmployeeNumber,
                code
            })
        });

        const result = await response.json();

        if (!response.ok || !result.success) {
            body.classList.remove('success');
            body.classList.add('error');
            setVolanteStatus(result.message || 'No se pudo guardar el codigo', 'error-msg');
            setTimeout(resetForm, 3000);
            return;
        }

        body.classList.remove('error');
        body.classList.add('success');
        setVolanteStatus('Codigo guardado', 'success-msg');
        addRecentScan(result.scan);
        setTimeout(resetForm, 1500);
    } catch (error) {
        body.classList.remove('success');
        body.classList.add('error');
        setVolanteStatus('Error al guardar el codigo', 'error-msg');
        setTimeout(resetForm, 3000);
    }
}

function addRecentScan(scan) {
    if (!scan) {
        loadRecentScans();
        return;
    }

    const emptyState = recentScansList.querySelector('.recent-scans-empty');

    if (emptyState) {
        recentScansList.innerHTML = '';
    }

    recentScansList.insertAdjacentHTML('afterbegin', getRecentScanMarkup(scan));

    const items = recentScansList.querySelectorAll('.recent-scan-item');

    items.forEach((item, index) => {
        if (index >= 5) {
            item.remove();
        }
    });
}

async function loadRecentScans() {
    recentScansList.innerHTML = '<div class="recent-scans-empty">Cargando...</div>';

    try {
        const response = await fetch(`${endpoints.recentScans}?limit=5`);
        const result = await response.json();

        if (!response.ok || !result.success || result.scans.length === 0) {
            recentScansList.innerHTML = '<div class="recent-scans-empty">Sin escaneos recientes</div>';
            return;
        }

        recentScansList.innerHTML = result.scans.map(getRecentScanMarkup).join('');
    } catch (error) {
        recentScansList.innerHTML = '<div class="recent-scans-empty">No se pudieron cargar los escaneos</div>';
    }
}

function getRecentScanMarkup(scan) {
    const shortName = escapeHtml(getShortName(scan.fullName));
    const scannedAt = formatScanTime(scan.scannedAt);

    return `
        <div class="recent-scan-item">
            <span class="recent-scan-name">${shortName}</span>
            <span class="recent-scan-time">${scannedAt}</span>
        </div>
    `;
}

function resetForm() {
    body.classList.remove('success', 'error');

    employeeInput.value = '';
    volanteInput.value = '';

    employeeStatus.textContent = '';
    volanteStatus.textContent = '';

    employeeValid = false;
    validatedEmployeeNumber = null;
    employeeInput.disabled = false;
    volanteInput.disabled = true;
    volanteInput.classList.remove('error');

    employeeInput.focus();
}

function setEmployeeStatus(message, className) {
    employeeStatus.textContent = message;
    employeeStatus.className = className ? `status-text ${className}` : 'status-text';
}

function setVolanteStatus(message, className) {
    volanteStatus.textContent = message;
    volanteStatus.className = className ? `status-text ${className}` : 'status-text';
}

function formatScanTime(value) {
    if (!value) {
        return '';
    }

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return '';
    }

    return date.toLocaleTimeString('es-MX', {
        hour: '2-digit',
        minute: '2-digit'
    });
}

function getShortName(fullName) {
    const parts = (fullName || '')
        .trim()
        .split(/\s+/)
        .filter(Boolean);

    if (parts.length === 0) {
        return 'Sin nombre';
    }

    if (parts.length === 1) {
        return parts[0];
    }

    return `${parts[0]} ${parts[1]}`;
}

function escapeHtml(value) {
    return value
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');
}


const scansModal = document.getElementById('scansModal');
const openScansModal = document.getElementById('openScansModal');
const closeScansModal = document.getElementById('closeScansModal');
const modalScansContent = document.getElementById('modalScansContent');

openScansModal.addEventListener('click', async function () {
    scansModal.classList.add('active');
    await loadModalScans();
});

closeScansModal.addEventListener('click', function () {
    scansModal.classList.remove('active');
    employeeInput.focus();
});

scansModal.addEventListener('click', function (e) {
    if (e.target === scansModal) {
        scansModal.classList.remove('active');
        employeeInput.focus();
    }
});

async function loadModalScans() {
    modalScansContent.innerHTML = '<div class="recent-scans-empty">Cargando...</div>';

    try {
        const response = await fetch(endpoints.scansSummary);
        const result = await response.json();

        if (!response.ok || !result.success || result.scans.length === 0) {
            modalScansContent.innerHTML = '<div class="recent-scans-empty">Sin registros hoy</div>';
            return;
        }

        modalScansContent.innerHTML = result.scans.map(scan => {
            return `
                <div class="modal-scan-item">
                    <div>
                        <div class="modal-scan-name">${escapeHtml(scan.fullName)}</div>
                        <div class="modal-scan-operation">
                            ${escapeHtml(scan.operation || 'Sin operacion')} · ${escapeHtml(String(scan.employeeNumber || ''))}
                        </div>
                    </div>

                    <div class="modal-scan-right">
                        <div class="modal-scan-pieces">${Number(scan.quantity) === 1 ? '1 Pieza' : `${Number(scan.quantity)} Piezas`}</div>
                        <div class="modal-scan-time">${formatScanTime(scan.lastScannedAt)}</div>
                    </div>
                </div>
            `;
        }).join('');

    } catch (error) {
        modalScansContent.innerHTML = '<div class="recent-scans-empty">Error al cargar</div>';
    }
}
const modalSearchInput = document.getElementById('modalSearchInput');

modalSearchInput.addEventListener('input', function () {
    filterModalScans(this.value);
});

function filterModalScans(search) {
    const text = search.toLowerCase().trim();

    const items = modalScansContent.querySelectorAll('.modal-scan-item');

    items.forEach(item => {
        const content = item.textContent.toLowerCase();

        if (content.includes(text)) {
            item.style.display = '';
        } else {
            item.style.display = 'none';
        }
    });
}
openScansModal.addEventListener('click', async function () {
    scansModal.classList.add('active');

    modalSearchInput.value = '';

    await loadModalScans();
});
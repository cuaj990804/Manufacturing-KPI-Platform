// ========================================
// CONSTANTES Y CONFIGURACIÓN
// ========================================

const CONFIG = {
    API: {
        // API Controller dedicado para Scanner Production
        PART_NUMBER: '/api/ScannerProductionApi/GetPartNumber',
        PROGRAMS: '/api/ScannerProductionApi/GetPrograms',
        HEALTH_CHECK: '/api/ScannerProductionApi/HealthCheck',
        SAVE_SCAN: '/api/ScannerProductionApi/SaveScan',
        DELETE_LAST_SCAN: '/api/ScannerProductionApi/DeleteLastScan',
        GET_TODAY_SCANS: '/api/ScannerProductionApi/GetTodayScans',
        GET_LINE_METRICS: '/api/ScannerProductionApi/GetLineMetrics',
        VALIDATE_SCANNER_VALUE: '/api/ScannerProductionApi/ValidateScannerValue',
        SAVE_REJECTION: '/api/ScannerProductionApi/SaveRejection',
        VALIDATE_EMPLOYEE_NUMBER: '/api/ScannerProductionApi/ValidateEmployeeNumber',
        VALIDATE_DEFECT_CODE: '/api/ScannerProductionApi/ValidateDefectCode',
        GET_DEFECTS_BY_AREA: '/api/ScannerProductionApi/GetDefectsByArea',
        GET_REJECTIONS_HISTORY: '/api/ScannerProductionApi/GetRejectionsHistory',
        DELETE_DEFECT_FROM_REJECTION: '/api/ScannerProductionApi/DeleteDefectFromRejection',
        UPDATE_DEFECT_IN_REJECTION: '/api/ScannerProductionApi/UpdateDefectInRejection',
        // Endpoints MVC Controller
        AREAS: '/ScannerProduction/GetAreas',
        PROGRAMS_BY_CUSTOMER: '/ScannerProduction/GetProgramsByCustomer'
    },
    UI: {
        RESET_SCAN_STATUS_DELAY: 2000 // 2 segundos
    }
};

// Estado global de la aplicación
const state = {
    requerimiento: 0,
    producidas: 0,
    rechazos: 0,
    lastScan: null,
    scanStatus: null,
    currentPartData: null,
    // Variable específica para el número de parte actual (para validación)
    currentPartNumber: null,
    // Datos de la línea de producción (del servidor)
    productionLineId: null,
    lineNumber: null,
    dailyGoal: null,
    areaId: null,
    areaName: null,
    customerName: null,
    personalQuantity: null,
    standardTime: 0,
    availableMinutes: 0,
    // Variables para re-escaneo de piezas (cuando han pasado más de 24 horas)
    pendingRescanCode: null,
    pendingRescanDetails: null
};

// Variables globales
let selectedMethod = null;
let searchResults = [];
let selectedProgramData = null;

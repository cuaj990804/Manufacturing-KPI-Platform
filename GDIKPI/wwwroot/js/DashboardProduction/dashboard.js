// ================= CONFIG DESDE VIEW =================
const config = window.dashboardConfig || {};
const areasFromServer = config.areas || [];
const availableCards = new Map();
let hiddenLineIds = new Set();
let hiddenCardKeys = new Set();
let cardOrder = [];
let draftHiddenCardKeys = null;
let draftCardOrder = null;
let visibilityLoaded = false;
let dashboardLoadToken = 0;
let isConfirmingVisibilityChanges = false;

// ================= FUNCION PRINCIPAL =================
async function cargarTarjetas(loadToken = ++dashboardLoadToken) {
    try {
        await loadVisibilitySettings();

        let areaId = getAreaIdFromUrl() || getAreaIdFromPage();

        if (areaId === undefined || areaId === null) {
            areaId = config.areaId;
        }

        if (areaId === undefined) {
            areaId = null;
        }

        console.log("CONFIG:", config);
        console.log("AREAS:", areasFromServer);
        console.log("AREA ID FINAL:", areaId);

        const hoy = new Date();
        const fecha = hoy.toISOString().split("T")[0];

        const horaActual = new Date();
        const horaReal = horaActual.toTimeString().split(" ")[0];

        const container = document.getElementById("cards-container");
        if (!container) return;
        if (!isCurrentDashboardLoad(loadToken)) return;

        container.innerHTML = "";
        availableCards.clear();

        // ================= UNA SOLA ÁREA =================
        if (areaId !== null && areaId !== undefined) {
            

            const response = await fetch(`/api/Production/dailyproductionbyarea?areaId=${areaId}&targetDate=${fecha}&targetTime=${horaReal}`);

            if (!response.ok) {
                throw new Error(await response.text());
            }

            const data = await response.json();
            await renderCards(data, container, loadToken);
        }

        // ================= TODAS LAS ÁREAS =================
        else if (areasFromServer.length > 0) {
            
/*
            await Promise.all(
                areasFromServer.map(async (area) => {

                    const response = await fetch(`/api/Production/dailyproductionbyarea?areaId=${area.AreaId}&targetDate=${fecha}&targetTime=${horaReal}`);

                    if (!response.ok) return;

                    const data = await response.json();
                    renderCards(data, container);

                })
            );*/

            for (const area of areasFromServer) {
                const response = await fetch(`/api/Production/dailyproductionbyarea?areaId=${area.AreaId}&targetDate=${fecha}&targetTime=${horaReal}`);
                if (!response.ok) continue;

                const data = await response.json();
                await renderCards(data, container, loadToken);
            }
        }

        if (!isCurrentDashboardLoad(loadToken)) return;

        await renderOperatorStatsCards(container, loadToken);

        if (!isCurrentDashboardLoad(loadToken)) return;

        await renderProductionLineStatsCards(container, loadToken, areaId);

        if (!isCurrentDashboardLoad(loadToken)) return;

        applyDashboardCardOrder(container);
        updateDashboardCardDensity(container);
        renderDashboardEmptyState(container);

    } catch (error) {
        console.error("Error al cargar datos:", error);
    }
}

// ================= RENDER =================
async function renderCards(data, container, loadToken) {

    for (const total of data) {
        if (!isCurrentDashboardLoad(loadToken)) return;

        const cardKey = getLineCardKey(total.lineId);

        availableCards.set(cardKey, {
            key: cardKey,
            type: "line",
            lineId: total.lineId,
            lineNumber: total.lineNumber,
            lineName: total.lineName,
            area: total.areaCustomerName,
            label: getLineDisplayName({
                lineNumber: total.lineNumber,
                lineName: total.lineName,
                area: total.areaCustomerName
            })
        });

        renderConfigOptions();

        if (!isCardVisible(cardKey)) {
            continue;
        }

        const response = await fetch(
            `/api/ScannerProductionApi/GetLineMetrics?lineId=${total.lineId}`
        );

        let metrics = {};

        if (response.ok) {
            metrics = await response.json();
        }

        if (!isCurrentDashboardLoad(loadToken)) return;

        const produced = getDisplayedProducedPieces(total, data);
        const rejected = getDisplayedRejectedPieces(total, data);
        const displayedRequirement = getDisplayedRequirement(total, data);

        const rechazoPorcentaje = produced !== 0
            ? (rejected / produced) * 100
            : 0;

        const eficiencia = calcularEficiencia({
            producidas: metrics.producedPieces,
            standardTime: metrics.standardTime,
            personalQuantity: metrics.personalQuantity,
            availableMinutes: metrics.availableMinutes
        });

       

        const card = crearCard({
            lineId: total.lineId,
            linea: total.lineNumber,
            lineName: total.lineName,
            area: total.areaCustomerName,
            meta: displayedRequirement,
            real: produced,
            tiempoMuerto: total.downtimeMinutes,
            rechazo: rechazoPorcentaje,
            balance: total.requirementBalance,
            metaEstimada: displayedRequirement,
            calidad: total.qualityPercentage,
            oee: eficiencia,
            producidas: produced
        });

        container.appendChild(card);
    }
}

function getDisplayedRequirement(item, areaItems) {
    if (!isYanfengAggregateCard(item)) {
        return Number(item?.estimatedGoalPieces || 0);
    }

    return getYanfengProductionLines(areaItems).reduce(
        (total, currentItem) => total + Number(currentItem?.estimatedGoalPieces || 0),
        0
    );
}

function getDisplayedProducedPieces(item, areaItems) {
    return Number(item?.producedPieces || 0);
}

function getDisplayedRejectedPieces(item, areaItems) {
    return Number(item?.rejectedPieces || 0);
}

function getYanfengProductionLines(areaItems) {
    const productionLines = (areaItems || []).filter(
        currentItem => !isYanfengAggregateCard(currentItem)
    );

    return productionLines.length > 0 ? productionLines : (areaItems || []);
}

function isYanfengAggregateCard(item) {
    const normalizedLineName = normalizeProductionLineName(item?.lineName);
    const compactLineName = normalizedLineName.replaceAll(" ", "");

    if (
        normalizedLineName === "YANFENG T6" ||
        normalizedLineName === "YANFENG OVERALL" ||
        compactLineName === "YANFENGOVERALL"
    ) {
        return true;
    }

    // La tarjeta agregada de Forrado Yanfeng corresponde a la linea 1.
    // Este respaldo evita que un nombre vacio o distinto la trate como linea fisica.
    const normalizedAreaName = normalizeProductionLineName(
        item?.areaCustomerName ?? item?.area
    );

    return normalizedAreaName === "FORRADO YANFENG" && Number(item?.lineNumber) === 1;
}

function normalizeProductionLineName(value) {
    return String(value || "")
        .replaceAll("_", " ")
        .trim()
        .replace(/\s+/g, " ")
        .toUpperCase();
}

async function renderOperatorStatsCards(container, loadToken) {
    const response = await fetch("/api/ProductionOperatorsDashboardApi/operator-stats");
    if (!response.ok) return;

    const groups = await response.json();

    for (const group of groups || []) {
        if (!isCurrentDashboardLoad(loadToken)) return;

        const groupInfo = getOperatorGroupInfo(group);
        const cardKey = getOperatorCardKey(groupInfo);

        availableCards.set(cardKey, {
            key: cardKey,
            type: "operator",
            operation: groupInfo.operation,
            groupInfo,
            label: groupInfo.title
        });

        renderConfigOptions();

        if (!isCardVisible(cardKey)) {
            continue;
        }

        container.appendChild(crearOperatorStatsCard(groupInfo, group.operators || []));
    }
}

async function renderProductionLineStatsCards(container, loadToken, areaId) {
    const url = areaId
        ? `/api/ProductionOperatorsDashboardApi/production-line-stats?areaId=${encodeURIComponent(areaId)}`
        : "/api/ProductionOperatorsDashboardApi/production-line-stats";
    const response = await fetch(url);
    if (!response.ok) return;

    const groups = await response.json();

    for (const group of groups || []) {
        if (!isCurrentDashboardLoad(loadToken)) return;

        const physicalLines = (group.lines || []).filter(isProductionStatsLineVisible);
        if (physicalLines.length === 0) {
            continue;
        }

        const groupInfo = getProductionLineStatsGroupInfo(group);
        const cardKey = getProductionLineStatsCardKey(groupInfo);

        availableCards.set(cardKey, {
            key: cardKey,
            type: "production-line-stats",
            groupInfo,
            area: groupInfo.areaLabel,
            label: groupInfo.title
        });

        renderConfigOptions();

        if (isCardVisible(cardKey)) {
            container.appendChild(crearProductionLineStatsCard(groupInfo, physicalLines));
        }
    }
}

// ================= CREAR CARD =================
function crearCard({ lineId, linea, lineName, area, meta, real, tiempoMuerto, rechazo, balance, metaEstimada, calidad, oee, producidas }) {

    const div = document.createElement("div");

    div.id = `card-line-${lineId}`;
    div.dataset.lineId = lineId;
    div.dataset.cardKey = getLineCardKey(lineId);

    const cumpleMeta = real >= meta;

    div.className = cumpleMeta
        ? "production-card success"
        : "production-card warning";

    const rechazoFinal = rechazo ?? 0;
    const calidadFinal = calidad ?? 0;
    const oeeFinal = oee ?? 0;
    const rechazoRounded = Math.round(rechazoFinal);
    const oeeRounded = Math.round(oeeFinal);

    // ✅ BALANCE REAL
    const balanceFinal = (producidas || 0) - (meta || 0);

    const tituloLinea = (lineName && lineName.trim() !== '')
        ? lineName
        : "LÍNEA " + linea;

    div.innerHTML = `
    <div class="card-header ${cumpleMeta ? 'success' : 'warning'}">
        <div class="line-title">${tituloLinea}</div>
    </div>

    <div class="metrics-grid">

        <div class="metric requerimiento">
            <div class="metric-label">REQUERIMIENTO</div>
            <div id="req-value-${lineId}" class="metric-value">${meta}</div>
        </div>

        <div class="metric balance ${balanceFinal >= 0 ? 'positive' : 'negative'}">
            <div class="metric-label">BALANCE</div>
            <div class="metric-value">
                ${balanceFinal > 0 ? '+' : ''}${balanceFinal}
            </div>
        </div>

        <div class="metric rechazos">
            <div class="metric-label">RECHAZOS</div>
            <div id="rechazo-value-${lineId}" class="metric-value ${getPercentValueClass(rechazoRounded)}">${rechazoRounded}%</div>
        </div>

        <div class="metric eficiencia">
            <div class="metric-label">EFICIENCIA</div>
            <div id="oee-value-${lineId}" class="metric-value ${getPercentValueClass(oeeRounded)}">${oeeRounded}%</div>
        </div>

    </div>
    `;

    return div;
}

function crearOperatorStatsCard(groupInfo, operators) {
    const div = document.createElement("div");
    div.id = getOperatorCardElementId(groupInfo);
    div.dataset.cardType = "operator";
    div.dataset.cardKey = getOperatorCardKey(groupInfo);
    div.dataset.operation = groupInfo.operation;
    div.dataset.areaId = groupInfo.areaId ?? "";
    div.dataset.productionLinesId = groupInfo.productionLinesId ?? "";

    const sortedOperators = [...operators]
        .filter(operatorItem => Number(operatorItem.scanCount || 0) > 0)
        .sort((a, b) => {
            const statusOrder = { red: 0, green: 1 };
            const statusCompare = statusOrder[getOperatorStatusClass(a.percentage)] - statusOrder[getOperatorStatusClass(b.percentage)];
            if (statusCompare !== 0) return statusCompare;
            const percentageCompare = Number(a.percentage || 0) - Number(b.percentage || 0);
            if (percentageCompare !== 0) return percentageCompare;
            return (a.fullName || "").localeCompare(b.fullName || "");
        });

    div.className = "production-card operator-card";

    const columnCount = getOperatorCardColumnCount(sortedOperators.length);

    const operatorsHtml = sortedOperators.length > 0
        ? sortedOperators.map(operatorItem => {
            const statusClass = getOperatorStatusClass(operatorItem.percentage);
            const fullName = operatorItem.fullName || `#${operatorItem.employeeNumber}`;
            const scanCount = operatorItem.scanCount || 0;
            const goal = operatorItem.goal || 1;
            const expectedPiecesNow = Number(operatorItem.expectedPiecesNow || 0);
            const realTimePercentage = Math.round(Number(operatorItem.percentage || 0));

            return `
                <div class="operator-line ${statusClass}" title="Esperado ahora: ${expectedPiecesNow} | Avance actual: ${realTimePercentage}%">
                    <span class="operator-name">${escapeHtml(fullName)}</span>
                    <span class="operator-ratio">${scanCount}/${goal}</span>
                </div>
            `;
        }).join("")
        : '<div class="operator-empty">SIN DATOS</div>';

    div.innerHTML = `
        <div class="card-header operator">
            <div class="line-title">${escapeHtml(groupInfo.title)}</div>
        </div>

        <div class="operator-stats-grid" style="--operator-columns: ${columnCount}">
            ${operatorsHtml}
        </div>
    `;

    return div;
}

function crearProductionLineStatsCard(groupInfo, lines) {
    const div = document.createElement("div");
    div.id = getProductionLineStatsCardElementId(groupInfo);
    div.dataset.cardType = "production-line-stats";
    div.dataset.cardKey = getProductionLineStatsCardKey(groupInfo);
    div.dataset.areaId = groupInfo.areaId ?? "";
    div.dataset.programId = groupInfo.programId ?? "";

    const sortedLines = [...lines]
        .filter(isProductionStatsLineVisible)
        .sort((a, b) => {
        const statusOrder = { neutral: 0, green: 1 };
        const statusCompare = statusOrder[getProductionLineStatusClass(a)] - statusOrder[getProductionLineStatusClass(b)];
        if (statusCompare !== 0) return statusCompare;
        return Number(a.lineNumber || 0) - Number(b.lineNumber || 0);
        });

    const columnCount = getOperatorCardColumnCount(sortedLines.length);
    const linesHtml = sortedLines.length > 0
        ? sortedLines.map(line => {
            const statusClass = getProductionLineStatusClass(line);
            const lineLabel = line.lineNumber
                ? `LÍNEA ${line.lineNumber}`
                : (line.lineName || `LÍNEA ${line.productionLinesId}`);
            const producedPieces = Number(line.producedPieces || 0);
            const goal = Number(line.goal || 0);
            const productionPercentage = goal > 0
                ? Math.round((producedPieces / goal) * 100)
                : 0;

            return `
                <div class="operator-line ${statusClass}" title="Producción actual: ${producedPieces} | Meta: ${goal} | Avance: ${productionPercentage}%">
                    <span class="operator-name">${escapeHtml(lineLabel)}</span>
                    <span class="operator-ratio">${producedPieces}/${goal}</span>
                </div>
            `;
        }).join("")
        : '<div class="operator-empty">SIN DATOS</div>';

    div.className = "production-card production-line-stats-card";
    div.innerHTML = `
        <div class="card-header production-line-stats">
            <div class="line-title">${escapeHtml(groupInfo.title)}</div>
        </div>

        <div class="operator-stats-grid" style="--operator-columns: ${columnCount}">
            ${linesHtml}
        </div>
    `;

    return div;
}

async function refreshOperatorStatsCards() {
    try {
        await loadVisibilitySettings();

        const container = document.getElementById("cards-container");
        if (!container) return;

        const response = await fetch("/api/ProductionOperatorsDashboardApi/operator-stats");
        if (!response.ok) {
            throw new Error(await response.text());
        }

        const groups = await response.json();
        const currentOperatorKeys = new Set();
        container.querySelector(".dashboard-empty-state")?.remove();

        for (const group of groups || []) {
            const groupInfo = getOperatorGroupInfo(group);
            const cardKey = getOperatorCardKey(groupInfo);
            const cardId = getOperatorCardElementId(groupInfo);

            currentOperatorKeys.add(cardKey);

            availableCards.set(cardKey, {
                key: cardKey,
                type: "operator",
                operation: groupInfo.operation,
                groupInfo,
                label: groupInfo.title
            });

            const existingCard = document.getElementById(cardId);

            if (!isCardVisible(cardKey)) {
                existingCard?.remove();
                continue;
            }

            const updatedCard = crearOperatorStatsCard(groupInfo, group.operators || []);

            if (existingCard) {
                existingCard.replaceWith(updatedCard);
            } else {
                container.appendChild(updatedCard);
            }
        }

        for (const [cardKey, card] of availableCards.entries()) {
            if (card.type !== "operator" || currentOperatorKeys.has(cardKey)) {
                continue;
            }

            document.getElementById(getOperatorCardElementId(card.groupInfo || card.operation))?.remove();
            availableCards.delete(cardKey);
        }

        renderConfigOptions();
        applyDashboardCardOrder(container);
        updateDashboardCardDensity(container);
        renderDashboardEmptyState(container);
    } catch (error) {
        console.error("Error actualizando cards de operadores:", error);
    }
}

async function refreshProductionLineStatsCards() {
    try {
        await loadVisibilitySettings();

        const container = document.getElementById("cards-container");
        if (!container) return;

        const areaId = getAreaIdFromUrl() || getAreaIdFromPage() || config.areaId || null;
        const url = areaId
            ? `/api/ProductionOperatorsDashboardApi/production-line-stats?areaId=${encodeURIComponent(areaId)}`
            : "/api/ProductionOperatorsDashboardApi/production-line-stats";
        const response = await fetch(url);
        if (!response.ok) throw new Error(await response.text());

        const groups = await response.json();
        const currentKeys = new Set();
        container.querySelector(".dashboard-empty-state")?.remove();

        for (const group of groups || []) {
            const physicalLines = (group.lines || []).filter(isProductionStatsLineVisible);
            if (physicalLines.length === 0) {
                continue;
            }

            const groupInfo = getProductionLineStatsGroupInfo(group);
            const cardKey = getProductionLineStatsCardKey(groupInfo);
            const cardId = getProductionLineStatsCardElementId(groupInfo);
            currentKeys.add(cardKey);

            availableCards.set(cardKey, {
                key: cardKey,
                type: "production-line-stats",
                groupInfo,
                area: groupInfo.areaLabel,
                label: groupInfo.title
            });

            const existingCard = document.getElementById(cardId);
            if (!isCardVisible(cardKey)) {
                existingCard?.remove();
                continue;
            }

            const updatedCard = crearProductionLineStatsCard(groupInfo, physicalLines);
            if (existingCard) {
                existingCard.replaceWith(updatedCard);
            } else {
                container.appendChild(updatedCard);
            }
        }

        for (const [cardKey, card] of availableCards.entries()) {
            if (card.type !== "production-line-stats" || currentKeys.has(cardKey)) continue;

            document.getElementById(getProductionLineStatsCardElementId(card.groupInfo))?.remove();
            availableCards.delete(cardKey);
        }

        renderConfigOptions();
        applyDashboardCardOrder(container);
        updateDashboardCardDensity(container);
        renderDashboardEmptyState(container);
    } catch (error) {
        console.error("Error actualizando cards de líneas de producción:", error);
    }
}

// ================= HELPERS =================
async function loadVisibilitySettings(force = false) {
    if (visibilityLoaded && !force) return;

    const response = await fetch("/api/DashboardProductionVisibility");
    if (!response.ok) {
        throw new Error(await response.text());
    }

    const data = await response.json();
    hiddenLineIds = new Set((data.hiddenLineIds || []).map(String));
    hiddenCardKeys = new Set((data.hiddenCardKeys || []).map(String));
    cardOrder = normalizeCardOrder(data.cardOrder || []);
    visibilityLoaded = true;
}

function isLineVisible(lineId) {
    return !hiddenLineIds.has(String(lineId));
}

function getProductionStatsLineVisibilityKey(lineId) {
    return `production-line-item:${lineId}`;
}

function isProductionStatsLineVisible(line) {
    return !hiddenCardKeys.has(getProductionStatsLineVisibilityKey(line?.productionLinesId));
}

function isCardVisible(cardKey) {
    return !isCommittedCardHidden(cardKey);
}

function isDraftCardVisible(cardKey) {
    if (draftHiddenCardKeys) {
        return !draftHiddenCardKeys.has(cardKey);
    }

    return !isCommittedCardHidden(cardKey);
}

function isCommittedCardHidden(cardKey) {
    return hiddenCardKeys.has(cardKey) || isHiddenLegacyLineCard(cardKey);
}

function isHiddenLegacyLineCard(cardKey) {
    if (!cardKey.startsWith("line:")) return false;
    return hiddenLineIds.has(cardKey.substring("line:".length));
}

function getAllAvailableCardKeys() {
    return [...availableCards.keys()];
}

function getLineCardKey(lineId) {
    return `line:${lineId}`;
}

function getOperatorCardKey(groupInfoOrOperation) {
    if (typeof groupInfoOrOperation === "string") {
        return `operator:${groupInfoOrOperation}`;
    }

    const groupInfo = groupInfoOrOperation || {};
    return [
        "operator",
        groupInfo.operation || "Sin operacion",
        groupInfo.areaId ?? "sin-area",
        groupInfo.productionLinesId ?? "sin-linea",
        groupInfo.areaLabel || "sin-area-label",
        groupInfo.lineName || "sin-line-name"
    ].join(":");
}

function getOperatorCardElementId(groupInfoOrOperation) {
    return `card-${slugify(getOperatorCardKey(groupInfoOrOperation))}`;
}

function getProductionLineStatsCardKey(groupInfo) {
    return [
        "production-lines",
        groupInfo.areaId ?? "sin-area",
        groupInfo.programId ?? "sin-programa",
        groupInfo.programDescription || "Sin programa"
    ].join(":");
}

function getProductionLineStatsCardElementId(groupInfo) {
    return `card-${slugify(getProductionLineStatsCardKey(groupInfo))}`;
}

function getProductionLineStatsGroupInfo(group) {
    const customerName = (group.customerName || "").trim();
    const areaName = (group.areaName || "").trim();
    const areaParts = [];

    if (customerName) areaParts.push(customerName);
    if (areaName && areaName.toLowerCase() !== customerName.toLowerCase()) areaParts.push(areaName);

    const areaLabel = areaParts.join(" ");
    const programDescription = (group.programDescription || "SIN PROGRAMA").trim();
    const normalizedProgramDescription = programDescription.replaceAll("_", " ").toUpperCase();
    const visibleProgramDescription = normalizedProgramDescription === "SIN PROGRAMA"
        ? ""
        : programDescription;
    const title = ["LÍNEAS", areaLabel, visibleProgramDescription]
        .filter(value => value && value.trim() !== "")
        .join(" ");

    return {
        areaId: group.areaId ?? null,
        areaName,
        customerName,
        areaLabel,
        programId: group.programId ?? 0,
        programDescription,
        title
    };
}

function getOperatorGroupInfo(group) {
    const operation = group.operation || "Sin operacion";
    const customerName = (group.customerName || "").trim();
    const areaName = (group.areaName || "").trim();
    const lineName = (group.lineName || "").trim();
    const areaLabel = customerName || areaName;
    const suffix = [areaLabel, lineName].filter(value => value && value.trim() !== "").join(" ");
    const title = ["OPERADORES", operation, suffix]
        .filter(value => value && value.trim() !== "")
        .join(" ");

    return {
        operation,
        areaId: group.areaId ?? null,
        productionLinesId: group.productionLinesId ?? null,
        areaName,
        customerName,
        areaLabel,
        lineName,
        title
    };
}

function getLineDisplayName(card) {
    const name = card.lineName && card.lineName.trim() !== ""
        ? card.lineName
        : `LINEA ${card.lineNumber}`;

    return card.area ? `${name} - ${card.area}` : name;
}

function getDefaultCardSortValue(card) {
    const typeOrder = card.type === "line"
        ? "0"
        : card.type === "production-line-stats" ? "1" : "2";
    const area = card.area || card.groupInfo?.areaLabel || "";
    const lineNumber = String(Number(card.lineNumber || 0)).padStart(8, "0");
    const label = card.label || getLineDisplayName(card);

    return `${typeOrder}|${area}|${lineNumber}|${label}`;
}

function normalizeCardOrder(order) {
    const seen = new Set();

    return (order || [])
        .map(cardKey => String(cardKey || "").trim())
        .filter(cardKey => {
            if (!cardKey || seen.has(cardKey)) return false;
            seen.add(cardKey);
            return true;
        });
}

function getEffectiveCardOrder() {
    return draftCardOrder || cardOrder;
}

function getOrderedAvailableCardsForOrder(order) {
    const orderIndex = new Map(order.map((cardKey, index) => [cardKey, index]));

    return [...availableCards.values()]
        .sort((a, b) => {
            const aIndex = orderIndex.has(a.key) ? orderIndex.get(a.key) : Number.MAX_SAFE_INTEGER;
            const bIndex = orderIndex.has(b.key) ? orderIndex.get(b.key) : Number.MAX_SAFE_INTEGER;

            if (aIndex !== bIndex) return aIndex - bIndex;
            return getDefaultCardSortValue(a).localeCompare(getDefaultCardSortValue(b));
        });
}

function getOrderedAvailableCards() {
    return getOrderedAvailableCardsForOrder(getEffectiveCardOrder());
}

function getCurrentCardOrder() {
    return getOrderedAvailableCards().map(card => card.key);
}

function getCommittedCardOrder() {
    return getOrderedAvailableCardsForOrder(cardOrder).map(card => card.key);
}

function ensureDraftCardOrder() {
    if (!draftCardOrder) {
        draftCardOrder = getCurrentCardOrder();
    }
}

function moveDraftCard(cardKey, direction) {
    ensureDraftCardOrder();

    const currentIndex = draftCardOrder.indexOf(cardKey);
    if (currentIndex < 0) return;

    const nextIndex = currentIndex + direction;
    if (nextIndex < 0 || nextIndex >= draftCardOrder.length) return;

    [draftCardOrder[currentIndex], draftCardOrder[nextIndex]] =
        [draftCardOrder[nextIndex], draftCardOrder[currentIndex]];
}

function hasDraftOrderChanges() {
    if (!draftCardOrder) return false;

    const committedOrder = getCommittedCardOrder();
    const effectiveDraftOrder = getOrderedAvailableCardsForOrder(draftCardOrder).map(card => card.key);
    if (committedOrder.length !== effectiveDraftOrder.length) return true;

    return committedOrder.some((cardKey, index) => cardKey !== effectiveDraftOrder[index]);
}

function applyDashboardCardOrder(container) {
    if (!container) return;

    const orderIndex = new Map(cardOrder.map((cardKey, index) => [cardKey, index]));
    const fallbackIndex = new Map(
        getOrderedAvailableCardsForOrder(cardOrder).map((card, index) => [card.key, index])
    );
    const cards = [...container.querySelectorAll(".production-card")];

    cards
        .sort((a, b) => {
            const aKey = a.dataset.cardKey || "";
            const bKey = b.dataset.cardKey || "";
            const aIndex = orderIndex.has(aKey)
                ? orderIndex.get(aKey)
                : 1000000 + (fallbackIndex.get(aKey) || 0);
            const bIndex = orderIndex.has(bKey)
                ? orderIndex.get(bKey)
                : 1000000 + (fallbackIndex.get(bKey) || 0);

            if (aIndex !== bIndex) return aIndex - bIndex;
            return aKey.localeCompare(bKey);
        })
        .forEach(card => container.appendChild(card));
}

function renderConfigOptions() {
    const optionsContainer = document.getElementById("dashboard-card-options");
    if (!optionsContainer) return;

    const cards = getOrderedAvailableCards();

    if (cards.length === 0) {
        optionsContainer.innerHTML = '<div class="dashboard-config-empty">Cargando tarjetas...</div>';
        updateConfirmVisibilityButton();
        return;
    }

    optionsContainer.innerHTML = "";

    for (let index = 0; index < cards.length; index++) {
        const card = cards[index];
        const cardKey = card.key;
        const option = document.createElement("div");
        option.className = "dashboard-card-option";

        const label = document.createElement("label");
        label.className = "dashboard-card-option-label";

        const checkbox = document.createElement("input");
        checkbox.type = "checkbox";
        checkbox.checked = isDraftCardVisible(cardKey);
        checkbox.dataset.cardKey = cardKey;

        checkbox.addEventListener("change", () => {
            setDraftCardVisibility(cardKey, checkbox.checked);
            renderConfigOptions();
        });

        const text = document.createElement("span");
        text.textContent = card.label || getLineDisplayName(card);

        label.appendChild(checkbox);
        label.appendChild(text);

        const orderControls = document.createElement("div");
        orderControls.className = "dashboard-card-order-controls";

        const upButton = document.createElement("button");
        upButton.type = "button";
        upButton.className = "dashboard-card-order-button";
        upButton.title = "Subir";
        upButton.disabled = index === 0;
        upButton.innerHTML = '<i class="fas fa-chevron-up"></i>';
        upButton.addEventListener("click", () => {
            moveDraftCard(cardKey, -1);
            renderConfigOptions();
        });

        const downButton = document.createElement("button");
        downButton.type = "button";
        downButton.className = "dashboard-card-order-button";
        downButton.title = "Bajar";
        downButton.disabled = index === cards.length - 1;
        downButton.innerHTML = '<i class="fas fa-chevron-down"></i>';
        downButton.addEventListener("click", () => {
            moveDraftCard(cardKey, 1);
            renderConfigOptions();
        });

        orderControls.appendChild(upButton);
        orderControls.appendChild(downButton);
        option.appendChild(label);
        option.appendChild(orderControls);
        optionsContainer.appendChild(option);
    }

    updateConfirmVisibilityButton();
}

async function saveCardVisibility(cardKey, isVisible) {
    const response = await fetch("/api/DashboardProductionVisibility/card", {
        method: "PUT",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            cardKey,
            isVisible
        })
    });

    if (!response.ok) {
        throw new Error(await response.text());
    }
}

async function saveBulkVisibility(cardKeys, isVisible) {
    const response = await fetch("/api/DashboardProductionVisibility/cards/bulk", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            cardKeys,
            isVisible
        })
    });

    if (!response.ok) {
        throw new Error(await response.text());
    }
}

async function saveCardOrder(cardKeys) {
    const response = await fetch("/api/DashboardProductionVisibility/cards/order", {
        method: "PUT",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            cardOrder: cardKeys
        })
    });

    if (!response.ok) {
        throw new Error(await response.text());
    }
}

function renderDashboardEmptyState(container) {
    if (!container || container.querySelector(".production-card")) return;

    updateDashboardCardDensity(container);

    container.innerHTML = `
        <div class="dashboard-empty-state">
            No hay tarjetas seleccionadas. Usa el boton de configuracion para mostrar tarjetas.
        </div>
    `;
}

function updateDashboardCardDensity(container) {
    if (!container) return;

    const cardCount = container.querySelectorAll(".production-card").length;
    container.classList.toggle("single-card", cardCount === 1);
    container.classList.toggle("few-cards", cardCount > 0 && cardCount <= 2);
}

function initializeDashboardConfigPanel() {
    const toggleButton = document.getElementById("dashboard-config-toggle");
    const panel = document.getElementById("dashboard-config-panel");
    const closeButton = document.getElementById("dashboard-config-close");
    const selectAllButton = document.getElementById("dashboard-select-all");
    const clearAllButton = document.getElementById("dashboard-clear-all");
    const confirmButton = document.getElementById("dashboard-confirm-visibility");

    if (!toggleButton || !panel) return;

    toggleButton.addEventListener("click", () => {
        panel.classList.toggle("open");
    });

    if (closeButton) {
        closeButton.addEventListener("click", () => {
            panel.classList.remove("open");
        });
    }

    if (selectAllButton) {
        selectAllButton.addEventListener("click", () => {
            setDraftBulkVisibility(getAllAvailableCardKeys(), true);
            renderConfigOptions();
        });
    }

    if (clearAllButton) {
        clearAllButton.addEventListener("click", () => {
            setDraftBulkVisibility(getAllAvailableCardKeys(), false);
            renderConfigOptions();
        });
    }

    if (confirmButton) {
        confirmButton.addEventListener("click", async () => {
            await confirmVisibilityChanges(confirmButton);
        });
    }
}

function ensureDraftVisibility() {
    if (!draftHiddenCardKeys) {
        draftHiddenCardKeys = new Set(hiddenCardKeys);

        for (const cardKey of getAllAvailableCardKeys()) {
            if (isHiddenLegacyLineCard(cardKey)) {
                draftHiddenCardKeys.add(cardKey);
            }
        }
    }
}

function setDraftCardVisibility(cardKey, isVisible) {
    ensureDraftVisibility();

    if (isVisible) {
        draftHiddenCardKeys.delete(cardKey);
    } else {
        draftHiddenCardKeys.add(cardKey);
    }
}

function setDraftBulkVisibility(cardKeys, isVisible) {
    ensureDraftVisibility();

    for (const cardKey of cardKeys) {
        if (isVisible) {
            draftHiddenCardKeys.delete(cardKey);
        } else {
            draftHiddenCardKeys.add(cardKey);
        }
    }
}

function hasVisibilityDraftChanges() {
    if (!draftHiddenCardKeys) return false;

    const cardKeys = getAllAvailableCardKeys();
    return cardKeys.some(cardKey =>
        isCommittedCardHidden(cardKey) !== draftHiddenCardKeys.has(cardKey));
}

function updateConfirmVisibilityButton() {
    const confirmButton = document.getElementById("dashboard-confirm-visibility");
    if (!confirmButton) return;

    const hasChanges = hasVisibilityDraftChanges() || hasDraftOrderChanges();
    confirmButton.disabled = !hasChanges;
    confirmButton.textContent = hasChanges ? "Confirmar cambios" : "Confirmar";
}

async function confirmVisibilityChanges(confirmButton) {
    if (!hasVisibilityDraftChanges() && !hasDraftOrderChanges()) return;

    confirmButton.disabled = true;
    confirmButton.textContent = "Guardando...";
    isConfirmingVisibilityChanges = true;

    try {
        const changedCardKeys = draftHiddenCardKeys
            ? getAllAvailableCardKeys()
                .filter(cardKey => isCommittedCardHidden(cardKey) !== draftHiddenCardKeys.has(cardKey))
            : [];
        const visibleCardKeys = changedCardKeys
            .filter(cardKey => !draftHiddenCardKeys.has(cardKey));
        const hiddenCardKeysToSave = changedCardKeys
            .filter(cardKey => draftHiddenCardKeys.has(cardKey));
        const orderToSave = draftCardOrder
            ? getOrderedAvailableCardsForOrder(draftCardOrder).map(card => card.key)
            : null;

        if (visibleCardKeys.length > 0) {
            await saveBulkVisibility(visibleCardKeys, true);
        }

        if (hiddenCardKeysToSave.length > 0) {
            await saveBulkVisibility(hiddenCardKeysToSave, false);
        }

        if (orderToSave) {
            await saveCardOrder(orderToSave);
        }

        draftHiddenCardKeys = null;
        draftCardOrder = null;
        await loadVisibilitySettings(true);
        renderConfigOptions();
        await cargarTarjetasConIndicador();
    } catch (error) {
        console.error("Error confirmando visibilidad:", error);
        updateConfirmVisibilityButton();
    } finally {
        setTimeout(() => {
            isConfirmingVisibilityChanges = false;
        }, 500);
    }
}

function getOperatorStatusClass(percentage) {
    const value = Number(percentage || 0);
    if (value >= 100) return "green";
    return "red";
}

function getProductionLineStatusClass(line) {
    const producedPieces = Number(line?.producedPieces || 0);
    const goal = Number(line?.goal || 0);
    return goal > 0 && producedPieces >= goal ? "green" : "neutral";
}

function getOperatorCardColumnCount(operatorCount) {
    if (operatorCount > 30) return 4;
    if (operatorCount > 16) return 3;
    if (operatorCount > 8) return 2;
    return 1;
}

function slugify(value) {
    return String(value || "")
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, "-")
        .replace(/^-+|-+$/g, "") || "sin-operacion";
}

function escapeHtml(value) {
    const div = document.createElement("div");
    div.textContent = value ?? "";
    return div.innerHTML;
}

function calcularEficiencia({ producidas, standardTime, personalQuantity, availableMinutes }) {

    const p = Number(producidas || 0);
    const t = Number(standardTime || 0);
    const per = Number(personalQuantity || 0);
    const min = Number(availableMinutes || 0);

    const horas = min / 60;

    return (per > 0 && horas > 0)
        ? Math.round(((p * t) / (per * horas)) * 100)
        : 0;
}
function getAreaIdFromUrl() {
    const urlParams = new URLSearchParams(window.location.search);
    const areaId = urlParams.get('areaId');
    return areaId ? parseInt(areaId) : null;
}

function getAreaIdFromPage() {
    const el = document.getElementById('area-id-hidden');
    return el ? parseInt(el.value) : null;
}

// ================= INDICADOR =================
function cargarTarjetasConIndicador() {
    const loadToken = ++dashboardLoadToken;

    if (typeof mostrarActualizando === "function") {
        mostrarActualizando();
    }

    return cargarTarjetas(loadToken).then(() => {
        if (typeof ocultarActualizando === "function") {
            setTimeout(() => {
                if (isCurrentDashboardLoad(loadToken)) {
                    ocultarActualizando();
                }
            }, 1000);
        }
    });
}

function isCurrentDashboardLoad(loadToken) {
    return loadToken === dashboardLoadToken;
}

// ================= ACTUALIZAR MÉTRICAS SIN RECARGAR CARDS =================
// ================= ACTUALIZAR MÉTRICAS SIN RECARGAR CARDS =================
async function actualizarMetricasDashboard() {
    try {
        let areaId = getAreaIdFromUrl() || getAreaIdFromPage() || config.areaId || null;
        if (areaId === undefined || areaId === null) {
            areaId = null;
        }

        const hoy = new Date();
        const fecha = hoy.toISOString().split("T")[0];
        const horaReal = hoy.toTimeString().split(" ")[0];

        let areasToFetch = [];

        if (areaId !== null) {
            areasToFetch = [{ areaId: areaId }];
        } else if (areasFromServer.length > 0) {
            areasToFetch = areasFromServer.map(a => ({ areaId: a.AreaId }));
        }

        for (const area of areasToFetch) {
            const response = await fetch(`/api/Production/dailyproductionbyarea?areaId=${area.areaId}&targetDate=${fecha}&targetTime=${horaReal}`);
            if (!response.ok) continue;

            const data = await response.json();

            for (const item of data) {
                const lineId = item.lineId;
                const cardElement = document.getElementById(`card-line-${lineId}`);

                if (!cardElement) continue; // Si no existe la card, saltar

                // 1️⃣ Actualizar REQUERIMIENTO
                const reqElement = document.getElementById(`req-value-${lineId}`);
                const displayedRequirement = getDisplayedRequirement(item, data);
                if (reqElement && item.estimatedGoalPieces !== undefined) {
                    reqElement.textContent = displayedRequirement;
                }

                // 2️⃣ Actualizar BALANCE (calcular desde producidas - meta)
                const produced = getDisplayedProducedPieces(item, data);
                const meta = displayedRequirement;
                let balance = produced - meta;

                const balanceMetric = cardElement.querySelector('.metric.balance');
                if (balanceMetric) {
                    const balanceValue = balanceMetric.querySelector('.metric-value');
                    if (balanceValue) {
                        balanceValue.textContent = `${balance > 0 ? '+' : ''}${balance}`;
                    }

                    // Actualizar clase positive/negative
                    balanceMetric.classList.remove('positive', 'negative');
                    balanceMetric.classList.add(balance >= 0 ? 'positive' : 'negative');
                }

                // 3️⃣ Actualizar RECHAZOS
                const rejected = getDisplayedRejectedPieces(item, data);
                const rechazoPorcentaje = produced !== 0
                    ? (rejected / produced) * 100
                    : 0;

                const rechazoElement = document.getElementById(`rechazo-value-${lineId}`);
                if (rechazoElement) {
                    setPercentMetricValue(rechazoElement, rechazoPorcentaje);
                }

                // 4️⃣ Actualizar EFICIENCIA (usando GetLineMetrics)
                const metricsResponse = await fetch(`/api/ScannerProductionApi/GetLineMetrics?lineId=${lineId}`);
                if (metricsResponse.ok) {
                    const metrics = await metricsResponse.json();

                    const eficiencia = calcularEficiencia({
                        producidas: metrics.producedPieces,
                        standardTime: metrics.standardTime,
                        personalQuantity: metrics.personalQuantity,
                        availableMinutes: metrics.availableMinutes
                    });

                    const oeeElement = document.getElementById(`oee-value-${lineId}`);
                    if (oeeElement) {
                        setPercentMetricValue(oeeElement, eficiencia);
                    }
                }

                // 5️⃣ Actualizar clase de la card según cumplimiento de meta
                

                cardElement.className = balance >= 0
                    ? "production-card success"
                    : "production-card danger";

                const header = cardElement.querySelector('.card-header');
                if (header) {
                    header.className = balance >= 0
                        ? "card-header success"
                        : "card-header danger";
                }
            }
        }

        console.log("✅ Métricas actualizadas sin recargar cards");

    } catch (error) {
        console.error("Error actualizando métricas:", error);
    }
}

// ================= ACTUALIZAR SOLO REQUERIMIENTO (SignalR) =================
function actualizarRequerimiento(lineId, nuevoValor) {
    const elemento = document.getElementById(`req-value-${lineId}`);
    if (elemento) {
        elemento.style.transition = 'transform 0.2s ease, color 0.2s ease';
        elemento.style.transform = 'scale(1.1)';
        elemento.style.color = '#4CAF50';

        elemento.textContent = nuevoValor;

        setTimeout(() => {
            elemento.style.transform = 'scale(1)';
            setTimeout(() => {
                elemento.style.color = '';
            }, 200);
        }, 200);

        console.log(`✅ REQUERIMIENTO actualizado para línea ${lineId}: ${nuevoValor}`);
    }
}

function setPercentMetricValue(element, value) {
    const roundedValue = Math.round(Number(value || 0));
    element.textContent = roundedValue + '%';
    element.classList.remove("percent-compact", "percent-extra-compact");

    const percentClass = getPercentValueClass(roundedValue);
    if (percentClass) {
        element.classList.add(percentClass);
    }
}

function getPercentValueClass(value) {
    const absoluteValue = Math.abs(Number(value || 0));

    if (absoluteValue >= 1000) {
        return "percent-extra-compact";
    }

    if (absoluteValue >= 100) {
        return "percent-compact";
    }

    return "";
}

// ================= SIGNALR =================
// ================= SIGNALR =================
let signalRConnection = null;
let currentAreaId = null;
let operatorStatsRefreshTimer = null;
let productionLineStatsRefreshTimer = null;

function scheduleProductionLineStatsRefresh() {
    clearTimeout(productionLineStatsRefreshTimer);
    productionLineStatsRefreshTimer = setTimeout(() => {
        refreshProductionLineStatsCards();
    }, 300);
}

async function initializeSignalR() {
    try {
        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl("/dashboardHub")
            .withAutomaticReconnect()
            .build();

        signalRConnection.on("ProductionDataUpdated", function (data) {
            console.log("📡 ProductionDataUpdated recibido:", data);
            if (!currentAreaId || data.areaId === currentAreaId) {
                actualizarMetricasDashboard();
                scheduleProductionLineStatsRefresh();
            }
        });

        signalRConnection.on("RejectDataUpdated", function (data) {
            console.log(" RejectDataUpdated recibido:", data);
            if (!currentAreaId || data.areaId === currentAreaId) {
                actualizarMetricasDashboard();
            }
        });

        signalRConnection.on("RequirementUpdated", function (data) {
            console.log(" RequirementUpdated recibido:", data);
            actualizarMetricasDashboard();
        });

        signalRConnection.on("DashboardCardVisibilityUpdated", async function () {
            console.log("Visibilidad de tarjetas actualizada");

            if (isConfirmingVisibilityChanges) {
                return;
            }

            await loadVisibilitySettings(true);
            renderConfigOptions();
            cargarTarjetasConIndicador();
        });

        signalRConnection.on("OperatorStatsUpdated", function (data) {
            console.log("OperatorStatsUpdated recibido:", data);

            clearTimeout(operatorStatsRefreshTimer);
            operatorStatsRefreshTimer = setTimeout(() => {
                refreshOperatorStatsCards();
            }, 300);
        });

        await signalRConnection.start();

        currentAreaId = getAreaIdFromUrl() || getAreaIdFromPage() || config.areaId || null;

        // ✅ Unirse a grupos según el contexto
        if (currentAreaId) {
            // Vista de UN área específica
            await signalRConnection.invoke("JoinDashboardGroup", currentAreaId.toString());
            console.log(`✅ Unido a Dashboard_${currentAreaId}`);
        } else if (areasFromServer.length > 0) {
            // Vista de TODAS las áreas - unirse a todos los grupos
            for (const area of areasFromServer) {
                await signalRConnection.invoke("JoinDashboardGroup", area.AreaId.toString());
                console.log(`✅ Unido a Dashboard_${area.AreaId}`);
            }
        }

        console.log("✅ SignalR conectado y grupos configurados");

    } catch (error) {
        console.error("❌ SignalR error:", error);
    }
}

// ================= AUTO REFRESH CADA MINUTO =================
function iniciarAutoRefresh() {
    const intervalo = 60000; // 1 minuto

    setInterval(() => {
        console.log("🔄 Actualizando métricas...");
        actualizarMetricasDashboard();
        refreshOperatorStatsCards();
        refreshProductionLineStatsCards();
    }, intervalo);
}



// ================= INIT =================
initializeDashboardConfigPanel();
cargarTarjetasConIndicador();
initializeSignalR();
iniciarAutoRefresh();

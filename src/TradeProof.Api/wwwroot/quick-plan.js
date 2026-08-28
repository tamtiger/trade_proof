const headers = {
  "Content-Type": "application/json",
  "X-TradeProof-Issuer": "https://dev.identity.tradeproof.local/tenant",
  "X-TradeProof-Subject": "local-owner",
  "X-TradeProof-Display-Name": "Local Binance Spot"
};

const adapterContractVersion = "binance_spot_trade_history_csv_v1";
const sampleCsv = [
  "Date(UTC),Pair,Side,Price,Executed,Amount,Fee",
  "2026-08-27 09:01:00,BTCUSDT,BUY,100.50,0.10,10.05,0.0001 BNB",
  "2026-08-27 09:02:00,ETHUSDT,SELL,2500.00,0.20,500.00,0.0002 ETH"
].join("\n");

const errorMessages = {
  HEADER_MISMATCH: "Header CSV không khớp contract Binance Spot.",
  UTF8_INVALID: "File không phải UTF-8 hợp lệ.",
  UPLOAD_TOO_LARGE: "File vượt 20 MiB.",
  CSV_ROW_LIMIT_EXCEEDED: "File vượt 100.000 dòng dữ liệu.",
  CSV_PARSE_ERROR: "CSV không parse được theo RFC4180.",
  IMPORT_PREVIEW_EXPIRED: "Preview đã hết hạn.",
  IMPORT_PREVIEW_HASH_MISMATCH: "Preview hash không khớp.",
  WRITE_CAPABILITY_ALREADY_CONSUMED: "Write capability đã được dùng.",
  IDEMPOTENCY_CONFLICT: "Idempotency key đã dùng cho payload khác."
};

let bootstrap;
let practiceIndex = 0;
let importFlow = {};

const form = document.querySelector("#planForm");
const setupSelect = document.querySelector("#setupSelect");
const errorText = document.querySelector("#errorText");
const csvText = document.querySelector("#csvText");
const reserveImportButton = document.querySelector("#reserveImportButton");
const validateImportButton = document.querySelector("#validateImportButton");
const confirmImportButton = document.querySelector("#confirmImportButton");
const purgeUploadButton = document.querySelector("#purgeUploadButton");

async function api(path, options = {}) {
  const response = await fetch(path, {
    ...options,
    headers: { ...headers, ...(options.headers || {}) }
  });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.code || `HTTP_${response.status}`);
  }
  return response.json();
}

function idempotencyKey(prefix) {
  return `${prefix}-${crypto.randomUUID()}`;
}

function safeMessage(error) {
  return errorMessages[error.message] || error.message;
}

function renderBootstrap(data) {
  bootstrap = data;
  document.querySelector("#workspaceStatus").textContent = "Workspace đang hoạt động";
  document.querySelector("#workspaceId").textContent = data.workspaceId;
  document.querySelector("#accountId").textContent = data.tradingAccountId;
  setupSelect.innerHTML = "";
  for (const setup of data.setupPresets) {
    const option = document.createElement("option");
    option.value = setup.revisionId;
    option.textContent = setup.label;
    setupSelect.append(option);
  }
}

function markStep(step, state) {
  const item = document.querySelector(`[data-step="${step}"]`);
  item.dataset.state = state;
}

function renderImportFlow() {
  document.querySelector("#uploadId").textContent = importFlow.upload?.uploadId || importFlow.transfer?.upload?.uploadId || "-";
  document.querySelector("#previewId").textContent = importFlow.preview?.importPreviewId || "-";
  document.querySelector("#previewRows").textContent = importFlow.preview ? String(importFlow.preview.dataRows) : "-";
  document.querySelector("#previewSymbols").textContent = importFlow.preview?.symbols?.join(", ") || "-";
  document.querySelector("#batchId").textContent = importFlow.batch?.importBatchId || "-";
  document.querySelector("#progressText").textContent = importFlow.progress
    ? `${importFlow.progress.status}: ${importFlow.progress.reconciledRows}/${importFlow.progress.dataRows} reconciled`
    : "-";

  validateImportButton.disabled = !importFlow.reservation || Boolean(importFlow.preview) || importFlow.upload?.state === "REJECTED";
  confirmImportButton.disabled = !importFlow.preview || Boolean(importFlow.batch);
  purgeUploadButton.disabled = !(importFlow.upload || importFlow.transfer?.upload);
}

function resetImportFlow() {
  importFlow = {};
  document.querySelector("#importState").textContent = "Chưa reserve";
  for (const step of ["reserve", "write", "validate", "confirm", "purge"]) {
    markStep(step, "pending");
  }
  renderImportFlow();
}

async function load() {
  csvText.value = sampleCsv;
  resetImportFlow();
  try {
    renderBootstrap(await api("/api/bootstrap"));
  } catch (error) {
    errorText.textContent = `Không thể bootstrap: ${safeMessage(error)}`;
  }
}

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  errorText.textContent = "";
  const data = new FormData(form);
  const payload = {
    tradingAccountId: bootstrap.tradingAccountId,
    symbol: data.get("symbol"),
    setupPresetRevisionId: data.get("setupPresetRevisionId"),
    entryZoneLow: data.get("entryZoneLow"),
    entryZoneHigh: data.get("entryZoneHigh"),
    initialStop: data.get("initialStop"),
    plannedRiskUsdt: data.get("plannedRiskUsdt"),
    confidence: Number(data.get("confidence")),
    thesis: data.get("thesis"),
    expiryDurationSeconds: 86400,
    idempotencyKey: idempotencyKey("arm")
  };
  try {
    const revision = await api("/api/plans/arm", {
      method: "POST",
      body: JSON.stringify(payload)
    });
    document.querySelector("#planState").textContent = `Đã arm revision ${revision.revisionNo}`;
    document.querySelector("#submittedAt").textContent = revision.submittedAt;
    document.querySelector("#expiresAt").textContent = "24 giờ sau submittedAt";
  } catch (error) {
    errorText.textContent = `Không arm được plan: ${safeMessage(error)}`;
  }
});

document.querySelector("#practiceButton").addEventListener("click", async () => {
  errorText.textContent = "";
  practiceIndex += 1;
  try {
    const run = await api("/api/product-measurements/start", {
      method: "POST",
      body: JSON.stringify({
        feature: "QUICK_PLAN",
        mode: "PRACTICE",
        practiceIndex,
        idempotencyKey: idempotencyKey("practice")
      })
    });
    await api(`/api/product-measurements/${run.measurementRunId}/abandon`, {
      method: "POST",
      body: JSON.stringify({
        reason: "USER_CANCELLED",
        idempotencyKey: idempotencyKey("practice-end")
      })
    });
    document.querySelector("#workspaceStatus").textContent = `Practice ${practiceIndex}/3`;
  } catch (error) {
    errorText.textContent = `Không ghi practice: ${safeMessage(error)}`;
    practiceIndex -= 1;
  }
});

reserveImportButton.addEventListener("click", async () => {
  errorText.textContent = "";
  resetImportFlow();
  try {
    importFlow.reservation = await api("/api/imports/reserve", {
      method: "POST",
      body: JSON.stringify({
        tradingAccountId: bootstrap.tradingAccountId,
        adapterContractVersion,
        uploadKind: "CSV",
        idempotencyKey: idempotencyKey("reserve")
      })
    });
    document.querySelector("#importState").textContent = "Đã reserve";
    markStep("reserve", "done");
    renderImportFlow();
  } catch (error) {
    errorText.textContent = `Không reserve được upload: ${safeMessage(error)}`;
  }
});

validateImportButton.addEventListener("click", async () => {
  errorText.textContent = "";
  try {
    await api(`/api/imports/${importFlow.reservation.objectIngestReservationId}/record-bytes`, {
      method: "POST",
      body: JSON.stringify({
        writeCapabilityId: importFlow.reservation.writeCapabilityId,
        csvText: csvText.value,
        idempotencyKey: idempotencyKey("bytes")
      })
    });
    markStep("write", "done");

    importFlow.transfer = await api(`/api/imports/${importFlow.reservation.objectIngestReservationId}/transfer`, {
      method: "POST",
      body: JSON.stringify({ idempotencyKey: idempotencyKey("transfer") })
    });
    importFlow.upload = importFlow.transfer.upload;

    const validation = await api(`/api/uploads/${importFlow.upload.uploadId}/validate`, {
      method: "POST",
      body: JSON.stringify({ idempotencyKey: idempotencyKey("validate") })
    });
    importFlow.upload = validation.upload;
    importFlow.preview = validation.preview;
    if (validation.preview) {
      document.querySelector("#importState").textContent = "Preview sẵn sàng";
      markStep("validate", "done");
    } else {
      document.querySelector("#importState").textContent = errorMessages[validation.safeErrorCode] || "Upload bị reject";
      markStep("validate", "error");
    }
    renderImportFlow();
  } catch (error) {
    errorText.textContent = `Không validate được upload: ${safeMessage(error)}`;
  }
});

confirmImportButton.addEventListener("click", async () => {
  errorText.textContent = "";
  try {
    importFlow.batch = await api("/api/imports/confirm", {
      method: "POST",
      body: JSON.stringify({
        importPreviewId: importFlow.preview.importPreviewId,
        previewSummarySha256: importFlow.preview.previewSummarySha256,
        idempotencyKey: idempotencyKey("confirm")
      })
    });
    importFlow.progress = await api(`/api/imports/${importFlow.batch.importBatchId}/progress`);
    document.querySelector("#importState").textContent = "Đã confirm";
    markStep("confirm", "done");
    renderImportFlow();
  } catch (error) {
    errorText.textContent = `Không confirm được import: ${safeMessage(error)}`;
  }
});

purgeUploadButton.addEventListener("click", async () => {
  errorText.textContent = "";
  const uploadId = importFlow.upload?.uploadId || importFlow.transfer?.upload?.uploadId;
  try {
    const purge = await api(`/api/uploads/${uploadId}/purge`, {
      method: "POST",
      body: JSON.stringify({ idempotencyKey: idempotencyKey("purge") })
    });
    importFlow.upload = purge.upload;
    document.querySelector("#importState").textContent = "Đã purge raw upload";
    markStep("purge", "done");
    renderImportFlow();
  } catch (error) {
    errorText.textContent = `Không purge được upload: ${safeMessage(error)}`;
  }
});

load();

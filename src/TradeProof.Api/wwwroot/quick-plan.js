const headers = {
  "Content-Type": "application/json",
  "X-TradeProof-Issuer": "https://dev.identity.tradeproof.local/tenant",
  "X-TradeProof-Subject": "local-owner",
  "X-TradeProof-Display-Name": "Local Binance Spot"
};

const adapterContractVersion = "binance_spot_trade_history_csv_v1";
const sampleCsv = [
  "Date(UTC),Pair,Side,Price,Executed,Amount,Fee",
  "2026-08-27 09:01:00,BTCUSDT,BUY,101,1,101,1 USDT",
  "2026-08-27 09:02:00,BTCUSDT,SELL,111,0.4,44.4,0.004 BTC",
  "2026-08-27 09:03:00,BTCUSDT,SELL,121,0.6,72.6,0.5 USDT"
].join("\n");

const errorMessages = {
  HEADER_MISMATCH: "Header CSV không khớp contract Binance Spot.",
  UTF8_INVALID: "File không phải UTF-8 hợp lệ.",
  UPLOAD_TOO_LARGE: "File vượt 20 MiB.",
  CSV_ROW_LIMIT_EXCEEDED: "File vượt 100.000 dòng dữ liệu.",
  CSV_PARSE_ERROR: "CSV không parse được theo RFC4180.",
  IMPORT_PREVIEW_EXPIRED: "Preview đã hết hạn.",
  IMPORT_PREVIEW_HASH_MISMATCH: "Preview hash không khớp.",
  SELL_WITHOUT_OPEN_POSITION: "SELL không có vị thế LONG đang mở.",
  SELL_EXCEEDS_POSITION: "SELL vượt quá quantity đang mở.",
  FEE_CONVERSION_MISSING: "Fee third-asset chưa có conversion Phase 4.",
  ATTACHMENT_KIND_UNSUPPORTED: "Attachment chỉ nhận screenshot đã sanitize.",
  SCREENSHOT_UNSUPPORTED: "Screenshot không đúng định dạng hỗ trợ.",
  REVIEW_ALREADY_COMPLETED: "Episode này đã có review; hãy revise.",
  REVIEW_REQUIRED_CHECKLIST_MISSING: "Review thiếu kết quả checklist bắt buộc.",
  STALE_REVIEW_REVISION: "Review đã có revision mới hơn.",
  METRIC_INTERVAL_INVALID: "Khoảng metric không hợp lệ.",
  WRITE_CAPABILITY_ALREADY_CONSUMED: "Write capability đã được dùng.",
  IDEMPOTENCY_CONFLICT: "Idempotency key đã dùng cho payload khác."
};

let bootstrap;
let practiceIndex = 0;
let importFlow = {};
let attachmentFlow = {};
let dashboard = { episodes: [], reviews: [], reviewRevisions: [], attachments: [], metricSnapshots: [], dataQuality: { exclusionBanners: [] } };

const form = document.querySelector("#planForm");
const setupSelect = document.querySelector("#setupSelect");
const errorText = document.querySelector("#errorText");
const csvText = document.querySelector("#csvText");
const reserveImportButton = document.querySelector("#reserveImportButton");
const validateImportButton = document.querySelector("#validateImportButton");
const confirmImportButton = document.querySelector("#confirmImportButton");
const processImportButton = document.querySelector("#processImportButton");
const computeContextButton = document.querySelector("#computeContextButton");
const purgeUploadButton = document.querySelector("#purgeUploadButton");
const reviewEpisodeSelect = document.querySelector("#reviewEpisodeSelect");
const reserveAttachmentButton = document.querySelector("#reserveAttachmentButton");
const completeReviewButton = document.querySelector("#completeReviewButton");
const reviseReviewButton = document.querySelector("#reviseReviewButton");
const deleteAttachmentButton = document.querySelector("#deleteAttachmentButton");
const publishMetricsButton = document.querySelector("#publishMetricsButton");
const refreshDashboardButton = document.querySelector("#refreshDashboardButton");

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
    ? `${importFlow.progress.status}: ${importFlow.progress.reconciledRows}/${importFlow.progress.dataRows} reconciled, ${importFlow.progress.accountingPendingRows} pending, ${importFlow.progress.quarantinedRows} quarantined`
    : "-";
  document.querySelector("#episodeText").textContent = importFlow.progress?.episodes?.length
    ? importFlow.progress.episodes.map((episode) => `${episode.state} ${episode.planProofStatus}/${episode.accountingQuality}`).join(", ")
    : "-";
  document.querySelector("#contextText").textContent = importFlow.contextSnapshots?.length
    ? importFlow.contextSnapshots.map((snapshot) => `${snapshot.phase} ${snapshot.timeframe} ${snapshot.quality}`).join(", ")
    : "-";

  validateImportButton.disabled = !importFlow.reservation || Boolean(importFlow.preview) || importFlow.upload?.state === "REJECTED";
  confirmImportButton.disabled = !importFlow.preview || Boolean(importFlow.batch);
  processImportButton.disabled = !importFlow.batch || ["COMPLETE", "PARTIAL", "NEEDS_ATTENTION", "REJECTED"].includes(importFlow.progress?.status);
  computeContextButton.disabled = !importFlow.progress?.episodes?.length || Boolean(importFlow.contextSnapshots);
  purgeUploadButton.disabled = !(importFlow.upload || importFlow.transfer?.upload);
}

function selectedDashboardEpisode() {
  return dashboard.episodes.find((episode) => episode.episodeId === reviewEpisodeSelect.value) || dashboard.episodes[0];
}

function selectedReview() {
  const episode = selectedDashboardEpisode();
  return episode ? dashboard.reviews.find((review) => review.episodeId === episode.episodeId) : null;
}

function selectedRevision() {
  const review = selectedReview();
  if (!review) {
    return null;
  }
  return dashboard.reviewRevisions
    .filter((revision) => revision.reviewId === review.reviewId)
    .sort((a, b) => b.revisionNo - a.revisionNo)[0] || null;
}

function currentSetup() {
  return bootstrap?.setupPresets?.find((setup) => setup.revisionId === setupSelect.value);
}

function requiredChecklistResults() {
  const results = {};
  const done = document.querySelector("#checklistDoneInput").checked;
  for (const item of currentSetup()?.checklist || []) {
    if (item.required) {
      results[item.checklistItemId] = done;
    }
  }
  return results;
}

function reviewPayloadBase() {
  const ruleBreach = document.querySelector("#ruleBreachInput").checked;
  const riskExceeded = document.querySelector("#riskExceededInput").checked;
  const emotion = document.querySelector("#emotionSelect").value || null;
  return {
    exitReason: document.querySelector("#exitReasonSelect").value,
    exitReasonOtherText: null,
    ruleBreach,
    breachTypeIds: ruleBreach || riskExceeded ? ["RISK_EXCEEDED"] : [],
    breachOtherText: null,
    stopMovedAway: document.querySelector("#stopMovedInput").checked,
    riskExceeded,
    requiredChecklistResults: requiredChecklistResults(),
    emotion,
    lesson: document.querySelector("#reviewLesson").value,
    attachmentId: attachmentFlow.attachment?.attachmentId || null
  };
}

function renderDashboard() {
  const selectedBefore = reviewEpisodeSelect.value;
  reviewEpisodeSelect.innerHTML = "";
  for (const episode of dashboard.episodes) {
    const option = document.createElement("option");
    option.value = episode.episodeId;
    option.textContent = `${episode.venueSymbol} ${episode.state} ${episode.planProofStatus}/${episode.accountingQuality}`;
    reviewEpisodeSelect.append(option);
  }
  if (dashboard.episodes.some((episode) => episode.episodeId === selectedBefore)) {
    reviewEpisodeSelect.value = selectedBefore;
  }

  const episode = selectedDashboardEpisode();
  const review = selectedReview();
  const revision = selectedRevision();
  const attachment = attachmentFlow.attachment;
  document.querySelector("#dashboardEpisodes").textContent = dashboard.episodes.length
    ? dashboard.episodes.map((item) => `${item.venueSymbol}: ${item.accountingQuality}`).join(", ")
    : "-";
  document.querySelector("#dashboardReviews").textContent = dashboard.reviews.length
    ? dashboard.reviews.map((item) => `${item.state} ${item.reviewId}`).join(", ")
    : "-";
  document.querySelector("#dashboardMetrics").textContent = dashboard.metricSnapshots.length
    ? dashboard.metricSnapshots.map((snapshot) => `${snapshot.metricId}=${snapshot.valueDecimal || snapshot.displayState}`).join(", ")
    : "-";
  document.querySelector("#qualityBanners").textContent = dashboard.dataQuality?.exclusionBanners?.length
    ? dashboard.dataQuality.exclusionBanners.join(", ")
    : "OK";
  document.querySelector("#reviewState").textContent = episode
    ? (revision ? `Revision ${revision.revisionNo}` : "Sẵn sàng review")
    : "Chưa có episode";
  document.querySelector("#reviewText").textContent = revision
    ? `${review.state} ${revision.exitReason}`
    : "-";
  document.querySelector("#attachmentText").textContent = attachment
    ? `${attachment.state} ${attachment.scanStatus} ${attachment.attachmentId}`
    : "-";
  document.querySelector("#metricState").textContent = dashboard.metricSnapshots.length
    ? `${dashboard.metricSnapshots.length} snapshot`
    : "Chưa publish";

  completeReviewButton.disabled = !episode || Boolean(review);
  reviseReviewButton.disabled = !review || !revision;
  publishMetricsButton.disabled = dashboard.episodes.length === 0;
  deleteAttachmentButton.disabled = !attachment || attachment.state === "DELETED";
}

async function loadDashboard() {
  dashboard = await api("/api/dashboard");
  renderDashboard();
}

function resetImportFlow() {
  importFlow = {};
  attachmentFlow = {};
  document.querySelector("#importState").textContent = "Chưa reserve";
  for (const step of ["reserve", "write", "validate", "confirm", "process", "context", "purge"]) {
    markStep(step, "pending");
  }
  renderImportFlow();
  renderDashboard();
}

async function seedMarketData() {
  await api("/api/market/conversion-catalog", {
    method: "POST",
    body: JSON.stringify({
      pairs: [
        { venueSymbol: "BNBUSDT", baseAsset: "BNB", quoteAsset: "USDT", conversionSupported: true },
        { venueSymbol: "BTCUSDT", baseAsset: "BTC", quoteAsset: "USDT", conversionSupported: true }
      ],
      idempotencyKey: "phase4-ui-catalog"
    })
  });
  await api("/api/market/bars", {
    method: "POST",
    body: JSON.stringify({
      symbol: "BTCUSDT",
      timeframe: "1m",
      bars: [
        { openAt: "2026-08-27T09:00:00Z", close: "101", volume: "100" },
        { openAt: "2026-08-27T09:01:00Z", close: "111", volume: "120" },
        { openAt: "2026-08-27T09:02:00Z", close: "121", volume: "140" }
      ],
      idempotencyKey: "phase4-ui-btc-1m"
    })
  });
  await api("/api/market/bars", {
    method: "POST",
    body: JSON.stringify({
      symbol: "BTCUSDT",
      timeframe: "5m",
      bars: [
        { openAt: "2026-08-27T08:55:00Z", close: "100", volume: "90" }
      ],
      idempotencyKey: "phase4-ui-btc-5m"
    })
  });
}

async function load() {
  csvText.value = sampleCsv;
  resetImportFlow();
  try {
    await seedMarketData();
    renderBootstrap(await api("/api/bootstrap"));
    await loadDashboard();
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
    await loadDashboard();
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
    await loadDashboard();
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
    await loadDashboard();
  } catch (error) {
    errorText.textContent = `Không confirm được import: ${safeMessage(error)}`;
  }
});

processImportButton.addEventListener("click", async () => {
  errorText.textContent = "";
  try {
    importFlow.batch = await api(`/api/imports/${importFlow.batch.importBatchId}/process`, {
      method: "POST",
      body: JSON.stringify({ idempotencyKey: idempotencyKey("process") })
    });
    importFlow.progress = await api(`/api/imports/${importFlow.batch.importBatchId}/progress`);
    importFlow.contextSnapshots = null;
    document.querySelector("#importState").textContent = "Đã reconcile";
    markStep("process", "done");
    renderImportFlow();
    await loadDashboard();
  } catch (error) {
    errorText.textContent = `Không process được import: ${safeMessage(error)}`;
    markStep("process", "error");
  }
});

computeContextButton.addEventListener("click", async () => {
  errorText.textContent = "";
  const episode = importFlow.progress?.episodes?.[0];
  try {
    importFlow.contextSnapshots = await api("/api/context/compute", {
      method: "POST",
      body: JSON.stringify({
        episodeId: episode.episodeId,
        projectionVersion: episode.projectionVersion,
        idempotencyKey: idempotencyKey("context")
      })
    });
    document.querySelector("#importState").textContent = "Đã snapshot context";
    markStep("context", "done");
    renderImportFlow();
    await loadDashboard();
  } catch (error) {
    errorText.textContent = `Không tạo được context: ${safeMessage(error)}`;
    markStep("context", "error");
  }
});

reserveAttachmentButton.addEventListener("click", async () => {
  errorText.textContent = "";
  try {
    attachmentFlow.reservation = await api("/api/attachments/reserve", {
      method: "POST",
      body: JSON.stringify({
        tradingAccountId: bootstrap.tradingAccountId,
        uploadKind: "SCREENSHOT",
        idempotencyKey: idempotencyKey("attachment-reserve")
      })
    });
    await api(`/api/imports/${attachmentFlow.reservation.objectIngestReservationId}/record-bytes`, {
      method: "POST",
      body: JSON.stringify({
        writeCapabilityId: attachmentFlow.reservation.writeCapabilityId,
        bytesBase64: "iVBORw0KGgoAAAANS",
        idempotencyKey: idempotencyKey("attachment-bytes")
      })
    });
    attachmentFlow.transfer = await api(`/api/imports/${attachmentFlow.reservation.objectIngestReservationId}/transfer`, {
      method: "POST",
      body: JSON.stringify({ idempotencyKey: idempotencyKey("attachment-transfer") })
    });
    const validation = await api(`/api/attachments/${attachmentFlow.transfer.upload.uploadId}/validate`, {
      method: "POST",
      body: JSON.stringify({ idempotencyKey: idempotencyKey("attachment-validate") })
    });
    attachmentFlow.upload = validation.upload;
    attachmentFlow.attachment = validation.attachment;
    await loadDashboard();
  } catch (error) {
    errorText.textContent = `Không attach được screenshot: ${safeMessage(error)}`;
  }
});

completeReviewButton.addEventListener("click", async () => {
  errorText.textContent = "";
  const episode = selectedDashboardEpisode();
  if (!episode) {
    errorText.textContent = "Chưa có episode để review.";
    return;
  }

  try {
    const review = await api("/api/reviews/complete", {
      method: "POST",
      body: JSON.stringify({
        episodeId: episode.episodeId,
        expectedEpisodeProjectionVersion: episode.projectionVersion,
        ...reviewPayloadBase(),
        idempotencyKey: idempotencyKey("review-complete")
      })
    });
    document.querySelector("#reviewText").textContent = `${review.review.state} ${review.revision.exitReason}`;
    await loadDashboard();
  } catch (error) {
    errorText.textContent = `Không complete được review: ${safeMessage(error)}`;
  }
});

reviseReviewButton.addEventListener("click", async () => {
  errorText.textContent = "";
  const review = selectedReview();
  const revision = selectedRevision();
  const episode = selectedDashboardEpisode();
  if (!review || !revision || !episode) {
    errorText.textContent = "Chưa có review để revise.";
    return;
  }

  try {
    const revised = await api(`/api/reviews/${review.reviewId}/revise`, {
      method: "POST",
      body: JSON.stringify({
        expectedEpisodeProjectionVersion: episode.projectionVersion,
        expectedRevisionNo: revision.revisionNo,
        ...reviewPayloadBase(),
        idempotencyKey: idempotencyKey("review-revise")
      })
    });
    document.querySelector("#reviewText").textContent = `${revised.review.state} ${revised.revision.exitReason}`;
    await loadDashboard();
  } catch (error) {
    errorText.textContent = `Không revise được review: ${safeMessage(error)}`;
  }
});

deleteAttachmentButton.addEventListener("click", async () => {
  errorText.textContent = "";
  try {
    const deleted = await api(`/api/attachments/${attachmentFlow.attachment.attachmentId}/delete`, {
      method: "POST",
      body: JSON.stringify({ idempotencyKey: idempotencyKey("attachment-delete") })
    });
    attachmentFlow.attachment = deleted.attachment;
    await loadDashboard();
  } catch (error) {
    errorText.textContent = `Không delete được screenshot: ${safeMessage(error)}`;
  }
});

publishMetricsButton.addEventListener("click", async () => {
  errorText.textContent = "";
  try {
    await api("/api/metrics/publish", {
      method: "POST",
      body: JSON.stringify({
        reportingStartAtUtc: "2026-08-27T00:00:00Z",
        reportingEndAtUtc: "2026-09-03T00:00:00Z",
        idempotencyKey: idempotencyKey("metrics-publish")
      })
    });
    await loadDashboard();
  } catch (error) {
    errorText.textContent = `Không publish được metrics: ${safeMessage(error)}`;
  }
});

refreshDashboardButton.addEventListener("click", async () => {
  errorText.textContent = "";
  try {
    await loadDashboard();
  } catch (error) {
    errorText.textContent = `Không refresh được dashboard: ${safeMessage(error)}`;
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
    await loadDashboard();
  } catch (error) {
    errorText.textContent = `Không purge được upload: ${safeMessage(error)}`;
  }
});

load();

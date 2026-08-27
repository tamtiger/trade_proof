const headers = {
  "Content-Type": "application/json",
  "X-TradeProof-Issuer": "https://dev.identity.tradeproof.local/tenant",
  "X-TradeProof-Subject": "local-owner",
  "X-TradeProof-Display-Name": "Local Binance Spot"
};

let bootstrap;
let practiceIndex = 0;

const form = document.querySelector("#planForm");
const setupSelect = document.querySelector("#setupSelect");
const errorText = document.querySelector("#errorText");

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

function renderBootstrap(data) {
  bootstrap = data;
  document.querySelector("#workspaceStatus").textContent = "Workspace dang hoat dong";
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

async function load() {
  try {
    renderBootstrap(await api("/api/bootstrap"));
  } catch (error) {
    errorText.textContent = `Không thể bootstrap: ${error.message}`;
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
    document.querySelector("#planState").textContent = `Da arm revision ${revision.revisionNo}`;
    document.querySelector("#submittedAt").textContent = revision.submittedAt;
    document.querySelector("#expiresAt").textContent = "24 giờ sau submittedAt";
  } catch (error) {
    errorText.textContent = `Không arm được plan: ${error.message}`;
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
    errorText.textContent = `Không ghi practice: ${error.message}`;
    practiceIndex -= 1;
  }
});

load();

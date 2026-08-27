# Fixture Intake and Consent Procedure

- Owner: TamNT167
- Status: Phase 0 baseline
- Updated: 2026-08-27

## Purpose

TradeProof needs real Binance Spot Trade History CSV samples to validate `binance_spot_trade_history_csv_v1`. Real samples must never be invented, inferred from docs or copied from a user without consent.

## Real Sample Inventory

Current count: `0/5`.

| Slot | Status | Consent record | Anonymization record | Contract result |
|---|---|---|---|---|
| sample-01 | Missing | Not received | Not started | Blocked |
| sample-02 | Missing | Not received | Not started | Blocked |
| sample-03 | Missing | Not received | Not started | Blocked |
| sample-04 | Missing | Not received | Not started | Blocked |
| sample-05 | Missing | Not received | Not started | Blocked |

## Consent Requirements

- Consent must name the donor, date, allowed use, retention deadline and whether fixture output may be committed after anonymization.
- Raw CSV must stay outside source control.
- Anonymization must remove account identifiers, order IDs that can identify a user, filenames and any accidental note/export metadata.
- Anonymized fixture must preserve column headers, row order, timestamp precision, side, symbol, quantity, price, fee asset and edge-case structure needed by the contract.

## Synthetic Fixtures

Synthetic fixtures are allowed only for parser boundary tests and must be stored separately from real samples. Synthetic data cannot satisfy the 5 real sample release-input gate.

## Blocker Rule

Phase 0 artifacts may document this `0/5` state, but pilot readiness cannot claim the real-sample gate until all five slots have consent, anonymization and contract-test evidence.


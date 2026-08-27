# TradeProof

TradeProof là nhật ký giao dịch giúp trader đối chiếu kế hoạch, hành vi, chi phí và bối cảnh thị trường bằng dữ liệu có thể truy ngược.

MVP tập trung vào Binance Spot, các cặp có quote asset `USDT`, long-only và giao diện web responsive tiếng Việt. Sản phẩm không dự đoán giá, không phát tín hiệu mua/bán và không tuyên bố quan hệ nhân quả từ dữ liệu journal quan sát.

## Trạng thái repo

Repo hiện là baseline tài liệu sản phẩm và triển khai. Chưa có runtime ứng dụng, package manifest, database schema hoặc lệnh test/chạy được khai báo, nên README này không đưa ra hướng dẫn cài đặt giả định.

## Phạm vi MVP

- Quick Plan trước lệnh với server timestamp và revision bất biến.
- Import Binance Spot Trade History CSV theo contract đóng băng.
- Reconcile fill thành `TradeEpisode`, match plan và tính accounting deterministic.
- Market Context Card dùng Binance Spot public candles 1m/5m theo point-in-time.
- Episode Review và Weekly Lab deterministic, có sample size, quality và traceability.
- Export archive và xóa account theo security/privacy contract.
- AI chỉ là extension mặc định tắt; core workflow vẫn hoạt động khi AI bị tắt hoặc không khả dụng.

## Ngoài phạm vi MVP

- Exchange API key, live sync hoặc generic CSV mapper.
- Perpetual, margin, short, borrow, options hoặc tax report chính thức.
- Đặt lệnh, custody, bot, copy trading, leaderboard hoặc tín hiệu giao dịch.
- Multi-account, multi-workspace membership, native mobile app hoặc coach workspace.

## Đọc tiếp

1. [Product Brief](Product_Brief.md) - vấn đề, định vị, fixed MVP scope và validation.
2. [Documentation Index](docs/README.md) - thứ tự đọc, authority matrix, decision register và change control.
3. [Product Requirements](docs/PRODUCT_REQUIREMENTS.md) - user flows, requirement IDs, UI states và NFR.
4. [Implementation Plan](docs/IMPLEMENTATION_PLAN.md) - architecture boundaries, delivery order và Definition of Done.
5. [Acceptance Tests](docs/ACCEPTANCE_TESTS.md) - cross-domain scenarios, release profiles và evidence.

## Nguyên tắc khi triển khai

- Lấy authority theo domain trong [Documentation Index](docs/README.md), không dùng một tài liệu để override tất cả.
- Khi thay đổi behavior/output, cập nhật requirement, contract, fixture và acceptance evidence liên quan.
- Mọi insight về tài chính, context hoặc Weekly Lab phải truy về metric/version/sample/episode evidence.
- Giữ copy observational và no-signal: nói về kết quả "quan sát được" hoặc "liên hệ với", không gợi ý lệnh giao dịch.

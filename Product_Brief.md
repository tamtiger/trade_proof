# TradeProof - Product Brief

> Nhật ký giao dịch giúp trader đối chiếu kế hoạch, hành vi, chi phí và bối cảnh thị trường bằng dữ liệu có thể truy ngược.

- **Trạng thái:** Implementation baseline v1.0
- **Cập nhật:** 2026-08-27
- **Thị trường MVP:** Binance Spot, cặp định giá bằng USDT, long-only
- **Kênh MVP:** Web responsive
- **Ngôn ngữ MVP:** Tiếng Việt

## 1. Mục tiêu sản phẩm

TradeProof biến lịch sử giao dịch thành bằng chứng có thể truy ngược để trader nhận ra:

1. kết quả quan sát được khác nhau thế nào giữa các setup;
2. bối cảnh thị trường nào thường đi cùng kết quả đó;
3. commission đã ảnh hưởng tới lợi nhuận ra sao;
4. việc tuân thủ hoặc phá kế hoạch thường đi cùng kết quả nào.

TradeProof không dự đoán giá, không phát tín hiệu mua/bán và không tuyên bố một yếu tố đã gây ra kết quả chỉ từ dữ liệu journal quan sát. Giá trị cốt lõi là giúp người dùng chọn một thay đổi hành vi có thể kiểm chứng cho tuần tiếp theo.

### Định vị

> **Không dự đoán lệnh tiếp theo. Cho bạn thấy kế hoạch, hành vi, chi phí và bối cảnh đã liên hệ với kết quả giao dịch như thế nào.**

## 2. Quyết định đã khóa cho MVP

| Hạng mục | Quyết định |
|---|---|
| Venue | Binance Spot |
| Instrument | Cặp có quote asset là USDT |
| Position mode | Long-only; không margin, short hoặc borrow |
| Ingestion | File Binance Spot Trade History CSV theo contract đã đóng băng |
| Trading account | Một account trên mỗi workspace |
| Episode concurrency | Tối đa một episode đang mở trên mỗi account/instrument |
| Plan surface | Web responsive; server đóng dấu thời gian |
| Context source | Binance Spot public candles 1m/5m, cùng symbol |
| Weekly boundary | Half-open `[Thứ Hai 00:00, Thứ Hai kế tiếp 00:00)` theo timezone người dùng; mặc định `Asia/Ho_Chi_Minh` |
| AI | Tùy chọn; không nằm trên critical path của tính toán tài chính |

Các quyết định này là ranh giới triển khai, không phải tầm nhìn dài hạn. Perpetual, live sync, generic CSV mapper, nhiều account và mobile companion nằm ngoài MVP.

## 3. Vấn đề cần giải quyết

Trader discretionary thường:

- ghi chép không đều vì journal yêu cầu quá nhiều thao tác;
- viết lại lý do sau khi biết kết quả, tạo hindsight bias;
- bỏ sót commission hoặc quy đổi fee không nhất quán;
- không biết kết quả kém là trường hợp riêng hay lặp lại trong một setup/bối cảnh;
- kết luận quá sớm từ mẫu nhỏ;
- tập trung vào win rate/P&L nhưng không đo mức tuân thủ kế hoạch;
- dùng nhãn VPA/VSA chủ quan như một kết luận chắc chắn.

## 4. Người dùng mục tiêu

### Persona chính

- 25-40 tuổi, có công việc chính.
- Giao dịch discretionary khoảng 20-200 episode/tháng.
- Giao dịch Binance Spot trên các cặp USDT.
- Đã dùng TradingView, Excel, Notion hoặc screenshot nhưng review không đều.
- Có setup riêng nhưng thường đổi chiến lược hoặc phá risk rule.

### Job-to-be-done

> “Cuối mỗi tuần, tôi muốn thấy setup, bối cảnh, chi phí và việc tuân thủ kế hoạch đã liên hệ với kết quả của mình ra sao, để tuần tới chỉ thử thay đổi một hành vi.”

### Không phục vụ trong MVP

- Người chỉ muốn nhận kèo hoặc copy trade.
- Người giao dịch futures/perpetual, margin, options hoặc short.
- HFT hoặc hệ thống hoàn toàn tự động.
- Nhà đầu tư dài hạn chỉ có vài giao dịch mỗi năm.
- Người cần đặt lệnh, custody, bot hoặc tax report chính thức.

## 5. Nguyên tắc sản phẩm

1. **Journal-first:** context chỉ có ý nghĩa khi liên kết với kế hoạch và hành vi.
2. **Evidence over narrative:** insight phải dẫn tới episode IDs, sample size, data quality và algorithm version.
3. **Deterministic finance:** P&L, risk, expectancy, fee và reconciliation do code xác định tính.
4. **Observational, not causal:** dùng “liên hệ với”, “quan sát được”; không dùng “gây ra” nếu không có thiết kế nhân quả.
5. **No signal:** không đưa entry opportunity, position size, leverage, win probability hoặc khuyến nghị mua/bán.
6. **Point-in-time context:** snapshot chỉ dùng dữ liệu đã có tại thời điểm được mô tả.
7. **Same-venue context:** không âm thầm trộn volume nhiều sàn.
8. **Small-sample honesty:** segment dưới 30 closed episodes chỉ được gắn nhãn khám phá.
9. **Privacy and portability:** người dùng export và yêu cầu xóa toàn bộ dữ liệu được.
10. **AI is optional:** sản phẩm vẫn hoạt động đầy đủ khi AI không khả dụng.

## 6. Vòng lặp cốt lõi

### 6.1. Quick Plan

Trước lệnh, người dùng tạo một plan gồm:

- instrument;
- setup;
- entry zone;
- invalidation/initial stop;
- planned risk bằng USDT;
- confidence từ 1 đến 5;
- checklist tùy chọn;
- lý do ngắn; voice note chỉ xuất hiện khi extension flag được bật.

Trading account và direction `long` được điền mặc định. Khi người dùng bấm **Arm plan**, server tạo revision bất biến và đóng dấu `submittedAt`. Mọi revision tiếp theo là bản ghi append-only. Revision được xem là pre-fill chỉ khi thỏa contract thời gian và matching trong [Import and Accounting](docs/IMPORT_AND_ACCOUNTING.md).

Mục tiêu usability: median dưới 15 giây và P90 dưới 30 giây trên mobile viewport sau khi người dùng đã có setup preset. Đây là acceptance target, không phải claim chưa kiểm chứng.

### 6.2. Import và reconcile

- Upload Binance Spot Trade History CSV đúng phiên bản hỗ trợ.
- Validate header, encoding, kích thước và từng row trước khi ghi dữ liệu nghiệp vụ.
- Import idempotent; upload lại cùng file không được double-count.
- Row không hợp lệ đi vào quarantine cùng lý do, không bị bỏ qua âm thầm.
- Fill được chuẩn hóa, ghép plan và nhóm thành `TradeEpisode` theo contract xác định.
- Commission được quy đổi về USDT; nếu không quy đổi được, episode không đủ điều kiện cho net metrics.
- Mọi automatic match có status, reason và frozen candidate/basis evidence. Trường hợp mơ hồ yêu cầu người dùng xác nhận, chọn hoặc gỡ association qua audit record; thao tác này không bao giờ nâng proof thành `VERIFIED`.

### 6.3. Market Context Card

Tại first fill và closing fill, hệ thống tạo snapshot bất biến gồm:

- Relative Volume và robust anomaly score;
- range/volatility percentile;
- khoảng cách tới Session VWAP từ 00:00 UTC;
- deterministic regime;
- Effort-Response description khi đủ điều kiện;
- source, symbol, timeframe, `asOfAt`, coverage và quality;
- input digest và algorithm version.

Snapshot chỉ dùng candle có `barEndExclusive <= asOfAt`; không dùng source `close_time` inclusive để kiểm tra biên. Snapshot `PARTIAL` hoặc `UNRELIABLE` có thể hiện trên card với quality badge nhưng không được đưa vào context aggregate của Weekly Lab. Công thức nằm trong [Market Context Engine](docs/MARKET_CONTEXT_ENGINE.md).

### 6.4. Quick Review

Sau khi episode đóng, người dùng xác nhận:

- exit reason;
- có rule breach hay không và loại breach;
- stop có bị dời xa hơn không;
- realized risk có vượt planned risk không;
- emotion, lesson và screenshot tùy chọn.

Mục tiêu usability: median dưới 30 giây và P90 dưới 60 giây với preset có sẵn. Không bắt buộc viết journal dài.

### 6.5. Weekly Lab

Weekly Lab trình bày các quan sát sau chi phí:

1. observed expectancy của từng setup;
2. kết quả quan sát được khi có và không có rule breach;
3. khác biệt quan sát được giữa các market regime;
4. commission đã chiếm bao nhiêu gross profit;
5. phản ví dụ đối với xu hướng median quan sát được trong chính report, theo selection rule cố định và không suy diễn narrative riêng của người dùng;
6. một thí nghiệm hành vi cho tuần tiếp theo.

Mọi con số dẫn tới episode IDs, metric version, sample size và quality. Weekly Lab không dùng ngôn ngữ nhân quả và không đề xuất lệnh.

Boundary, deterministic section recipe, null/tie-break behavior, report revision và behavioral experiment được khóa trong [Weekly Lab Contract](docs/WEEKLY_LAB.md).

## 7. North-star và product metrics

### North-star

`verified_review_week_rate`:

```text
Số eligible user-week thỏa cả hai điều kiện:
  A. ít nhất 80% eligible closed episodes có verified pre-fill plan;
  B. weekly review được hoàn thành trong 72 giờ sau khi tuần kết thúc
chia cho tổng eligible user-week.
```

Một `eligible user-week` có ít nhất 3 `north_star_eligible` closed episodes. Episode đạt trạng thái này khi đã reconcile và có net P&L; context quality không ảnh hưởng eligibility của north-star. Tuần không đủ điều kiện không nằm trong tử số hoặc mẫu số.

Supporting metrics:

- verified pre-fill plan coverage;
- weekly review completion rate;
- median/P90 Quick Plan time;
- median/P90 Quick Review time;
- time-to-first-insight;
- import reconciliation coverage;
- percentage of episodes excluded by data quality;
- weekly active retained users, week 4 và week 8;
- episode count before và after adoption để theo dõi nguy cơ kích thích overtrading.

Denominator, event window, replay và privacy contract của các supporting metric nằm trong [Weekly Lab Contract](docs/WEEKLY_LAB.md); không được suy ra lại từ tên metric.

First-party analytics là source of truth. Nếu bật processor analytics ngoài, chỉ stored minimized `product_analytics_external_v1` envelope được gửi với pseudonym xoay riêng theo processor; không gửi source ID, nội dung giao dịch hay thời điểm millisecond. Event không đủ điều kiện chỉ tạo receipt suppression nội bộ, không tạo projection/provider call. Mỗi copy có fenced 90-day purge và account deletion xóa từng pseudonym generation bằng evidence đóng.

Không dùng P&L hoặc số lệnh làm north-star.

## 8. MVP scope

### Trong phạm vi

- Managed authentication và tenant isolation.
- Một workspace, một Binance Spot trading account.
- Web responsive bằng tiếng Việt.
- Setup presets, Quick Plan, server timestamp và immutable revisions.
- Binance Spot Trade History CSV importer theo contract đóng băng.
- Manual resolution có audit cho row và association mơ hồ; resolution của match không biến bằng chứng timing mơ hồ thành verified.
- Long-only `TradeEpisode` trên cặp USDT.
- Commission và deterministic fee conversion.
- Market Context Engine trên Binance candles 1m/5m.
- Episode dashboard và deterministic Weekly Lab.
- Screenshot tùy chọn đối với người dùng nhưng là capability bắt buộc của core release.
- Export archive có canonical JSON, CSV tiện dụng, attachment và checksum manifest theo [Export Contract](docs/EXPORT_CONTRACT.md); STANDARD export READY trong 24 giờ, OVERSIZE vẫn lossless với progress notification; mỗi registered archive version có fenced expiry/delete/absence operation riêng. Xóa account dùng durable generation-fenced saga, versioned terminal work-marker drain, post-drain target verification và restore tombstone chain theo `TP-SEC`.
- Object write phải reserve trước bằng single-use capability. Raw upload bắt đầu forced purge không muộn hơn RECEIVE+20 giờ và phải absent không muộn hơn RECEIVE+24 giờ sau exact object-version verification; media giữ lại là sanitized Attachment riêng.
- Audit events cho thao tác nhạy cảm.

### Extension flags không chặn core release

- Voice transcription.
- AI taxonomy suggestion.
- AI grounded Weekly Summary.

Ba extension này mặc định tắt. Transcript/taxonomy chỉ ghi canonical field sau immutable user-confirmation command; voice còn phải đạt exact `voice_ingest_profile_v1`. Extension chỉ được bật khi toàn bộ security/AI gates tương ứng đạt; nếu tắt, text entry và deterministic Weekly Lab vẫn cung cấp đầy đủ core workflow.

### Ngoài phạm vi

- Generic CSV mapping.
- Exchange API key hoặc live/read-only sync.
- Perpetual, funding, margin, borrow, short, options.
- Nhiều trading account hoặc nhiều workspace membership.
- Đặt lệnh, custody, wallet hoặc copy trading.
- Buy/sell signal, dự báo giá hoặc backtesting.
- L2 order book, footprint, liquidation heatmap hoặc ML regime.
- Official tax report.
- Social feed, leaderboard hoặc gamification số lệnh/P&L.
- Native mobile app, extension hoặc coach workspace.

## 9. Vai trò của AI

### AI được phép

- Chuyển voice note tiếng Việt thành draft transcript để người dùng xác nhận.
- Đề xuất taxonomy cho lý do vào/ra lệnh để người dùng xác nhận.
- Viết weekly summary chỉ từ deterministic metric payload.

### AI không được phép

- Tự tính hoặc sửa P&L, expectancy, risk, fee conversion hay reconciliation.
- Dự báo giá, win probability hoặc quan hệ nhân quả.
- Đề xuất entry, leverage, position size hoặc lệnh cụ thể.
- Thực hiện giao dịch hoặc truy cập exchange secret.
- Ghi field có cấu trúc khi chưa có xác nhận của người dùng.
- Dùng note/CSV nhập vào như system instruction.

Chi tiết về opt-in, grounding, retention, versioning và eval nằm trong [Security, Privacy and AI](docs/SECURITY_PRIVACY_AI.md).

Khi xóa một AI output, local payload/run bị xóa trong một transaction nhưng payload-free subject lifecycle và unsalted integrity digest còn tới lúc xóa Workspace để giữ confirmation/export/deletion proof. Digest này là derived personal data có access restriction, không được mô tả là anonymous hoặc content-free. Non-exported encrypted opaque processor-copy handle được xóa/verify qua fenced work rồi clear; account deletion chặn mọi late processor result.

## 10. Acceptance cấp sản phẩm

MVP chỉ được xem là hoàn tất khi:

1. Một người dùng mới import fixture 500 fills và thấy insight đầu tiên với median dưới 10 phút, P90 dưới 15 phút trên cấu hình test đã định nghĩa.
2. Import lại cùng file không tạo thêm fill, episode hoặc metric.
3. 100% supported golden fixtures khớp exact expected row disposition, episode boundary, ledger và metric; reconciliation coverage được báo riêng.
4. Không revision sau first fill nào được tính là verified pre-fill.
5. Thêm candle sau `asOfAt` không làm thay đổi ContextSnapshot đã phát hành.
6. ContextSnapshot `PARTIAL` hoặc `UNRELIABLE` không đi vào context aggregate; accounting/setup/adherence metrics dùng eligibility riêng và không bị loại chỉ vì context thiếu.
7. Mọi numeric insight dẫn tới metric ID, episode IDs, sample size và version.
8. N < 2 là `INSUFFICIENT`, N=2..29 là `EXPLORATORY`; không sample nào có nhãn “edge”, “tốt”, “xấu” hoặc kết luận định hướng.
9. Cross-workspace access bị từ chối ở cả API và storage layer.
10. Export đạt STANDARD 24-hour SLA, OVERSIZE lossless/status tests; deletion pass full saga/crash/restore/re-registration gates; nếu AI extension được bật thì confirmation, opt-out và no-signal eval cũng là release gate.

Test chi tiết nằm trong [Acceptance Tests](docs/ACCEPTANCE_TESTS.md).

## 11. Monetization giả thuyết

### Paid pilot

- 499.000 VND/3 tháng.
- Bao gồm guided onboarding và hỗ trợ đọc import diagnostics; support không có product access vào nội dung workspace.
- Pilot là chương trình concierge, không dùng để suy ra trực tiếp giá GA.

### Sau MVP, chỉ khi pilot đạt gate

- **Free:** tối đa 50 closed episodes mỗi calendar month, một account, metrics cơ bản.
- **Plus:** 149.000 VND/tháng hoặc 1.190.000 VND/năm; không áp monthly closed-episode quota, có Context Engine và Weekly Lab. Điều này không thay đổi export service class: snapshot STANDARD có cam kết 24 giờ, OVERSIZE vẫn được xử lý lossless nhưng không có v1 24-hour guarantee.
- **Pro:** chưa định giá; chỉ được công bố sau khi live sync và multi-account thực sự nằm trong roadmap được duyệt.

Không bán lifetime vì market data, storage và AI tạo chi phí duy trì.

## 12. Validation

### Ngày 1-5: problem interview

- Phỏng vấn 15 Binance Spot trader phù hợp persona.
- Xem journal hoặc 20 episode gần nhất với consent.
- Thu ít nhất 5 file CSV đã ẩn danh.
- Gate: ít nhất 8/15 người gặp vấn đề phân biệt setup, context và kỷ luật.

### Ngày 6-10: data concierge

- Normalize 100-300 fills/người cho 5 người.
- Tạo báo cáo after-fee, rule breach, setup x regime và data quality.
- Gate: strict trên 98% toàn bộ data row không rỗng được `RECONCILED` hoặc đối chiếu thành `DUPLICATE`; row invalid và `ACCOUNTING_PENDING` vẫn nằm trong mẫu số. Thời gian xử lý thủ công dưới 2 giờ/người.

### Ngày 11-20: prototype

- Cho 10 người dùng Quick Plan và Quick Review.
- Đo timing, verified pre-fill coverage và time-to-first-insight.
- Gate: median Quick Plan dưới 15 giây sau lần sử dụng thứ ba; ít nhất 7/10 người hoàn thành một review.

### Ngày 21-30: paid-pilot enrollment

- Mời 10 người vào pilot và thu tiền trước khi onboarding.
- Gate bán hàng: ít nhất 5/10 người trả tiền.

### Ngày 31-51: cohort observation

- Theo dõi tối thiểu 3 weekly cycles sau onboarding.
- Gate hành vi: ít nhất 6/10 người hoàn thành 2 weekly reviews liên tiếp.
- Gate an toàn: median episode count không tăng quá 20% nếu người dùng không chủ động thay đổi chiến lược; trường hợp tăng phải được phỏng vấn.

## 13. Rủi ro và guardrail

| Rủi ro | Guardrail |
|---|---|
| Correlation bị hiểu thành causation | Ngôn ngữ observational; cấm causal claim trong template và AI eval |
| Anomaly bị hiểu là signal | Hiển thị percentile lịch sử; không dùng opportunity alert |
| P-hacking và mẫu nhỏ | `n < 30` exploratory; interval và phản ví dụ; không xếp hạng như edge |
| Look-ahead bias | Chỉ dùng bar có `barEndExclusive <= asOfAt`; immutable input digest |
| Venue mismatch | Binance Spot cùng symbol; không proxy trong MVP |
| Market-data terms thay đổi | Review Terms, retention và redistribution trước pilot; không public raw cache |
| Fee conversion thiếu | Gắn quality `FEE_CONVERSION_MISSING`, để net P&L null và loại khỏi net aggregate |
| CSV lỗi hoặc trùng | Validate, quarantine, content hash và source-row fingerprint |
| Hindsight plan | Server timestamp, append-only revisions và plan proof bốn trạng thái |
| AI hallucination | Structured metric payload, claim grounding, eval và deterministic fallback |
| Cross-tenant access | Workspace scope bắt buộc và authorization tests |
| App kích thích overtrading | Không gamify số lệnh/P&L; theo dõi episode count |
| Claim tài chính | Không đảm bảo lợi nhuận, không dự báo, không tư vấn cá nhân hóa |

## 14. Roadmap sau MVP

### Phase 2

- Binance Spot read-only sync sau security review.
- Nhiều trading account.
- Linear perpetual với accounting contract riêng cho funding và position mode.
- Generic CSV mapper.
- Mobile quick-plan companion.
- Taker-buy share khi feed và data-quality contract đáp ứng.

### Phase 3

- Multi-venue và multi-asset schema.
- Coach workspace có explicit consent.
- Volume Profile có quy tắc session/bin rõ ràng.
- Order-flow research khi có feed, license và nhu cầu trả tiền.

## 15. Bộ tài liệu triển khai

- [Documentation Index](docs/README.md)
- [Product Requirements](docs/PRODUCT_REQUIREMENTS.md)
- [Import and Accounting](docs/IMPORT_AND_ACCOUNTING.md)
- [Market Context Engine](docs/MARKET_CONTEXT_ENGINE.md)
- [Weekly Lab](docs/WEEKLY_LAB.md)
- [Security, Privacy and AI](docs/SECURITY_PRIVACY_AI.md)
- [Export Contract](docs/EXPORT_CONTRACT.md)
- [Acceptance Tests](docs/ACCEPTANCE_TESTS.md)
- [Implementation Plan](docs/IMPLEMENTATION_PLAN.md)

Khi có xung đột, dùng authority matrix theo domain trong Documentation Index. Acceptance test chỉ kiểm chứng contract và không được override behavior của contract chuyên ngành. Thay đổi ranh giới MVP phải cập nhật decision log trước khi code.

## 16. Nguồn tham khảo

Truy cập gần nhất: 2026-08-27.

- [CFTC - AI Won't Turn Trading Bots into Money Machines](https://www.cftc.gov/PressRoom/PressReleases/8854-24)
- [IOSCO - Policy Recommendations for Crypto and Digital Asset Markets](https://www.iosco.org/library/pubdocs/pdf/IOSCOPD747.pdf)
- [Binance Spot API documentation](https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md)
- [Binance Spot API Product Terms pointer](https://github.com/binance/binance-spot-api-docs/blob/master/PROD-TERMS-OF-USE.md)
- [Blume, Easley & O'Hara - Market Statistics and Technical Analysis](https://onlinelibrary.wiley.com/doi/abs/10.1111/j.1540-6261.1994.tb04424.x)

Các nguồn trên hỗ trợ guardrail và data-source decisions; công thức triển khai được định nghĩa trong contract nội bộ và được kiểm chứng bằng fixtures, không được suy ra ngầm từ bibliography.

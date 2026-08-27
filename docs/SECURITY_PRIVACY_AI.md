# Hợp đồng Security, Privacy và AI cho MVP

- **Document ID:** `TP-SEC`
- **Version:** 1.0.0
- **Trạng thái:** Chuẩn triển khai MVP
- **Updated:** 2026-08-27
- **Phạm vi:** SaaS nhiều người dùng, CSV import, voice note/screenshot tùy chọn và các tính năng AI giới hạn
- **Tài liệu nguồn:** Product Brief
- **Đơn vị thời gian chuẩn:** UTC

Trong tài liệu này:

- **PHẢI** và **KHÔNG ĐƯỢC** là yêu cầu bắt buộc để phát hành.
- **NÊN** là yêu cầu mặc định; mọi ngoại lệ phải có quyết định rủi ro được ghi lại.
- **User data** là mọi dữ liệu do người dùng cung cấp hoặc được tạo ra từ dữ liệu đó.
- **Processor** là nhà cung cấp bên thứ ba xử lý user data thay mặt hệ thống, bao gồm nhà cung cấp AI, lưu trữ và gửi email.

## 1. Phạm vi và giả định cố định

### 1.1. Mô hình MVP

MVP có các ràng buộc sau:

1. Sản phẩm là SaaS nhiều người dùng.
2. Mỗi User sở hữu đúng một Workspace.
3. Mỗi Workspace có đúng một TradingAccount.
4. Workspace trong MVP chỉ có một owner, không có member, coach, chia sẻ hoặc chuyển quyền sở hữu.
5. TradingAccount là bản ghi nghiệp vụ, không phải tài khoản đăng nhập.
6. Xác thực do nhà cung cấp managed OIDC và/hoặc magic link thực hiện.
7. Ứng dụng không nhận, lưu, băm hoặc khôi phục mật khẩu.
8. Dữ liệu giao dịch chỉ được nhập bằng CSV. MVP không kết nối exchange API và không nhận API key, API secret, private key hoặc seed phrase.
9. Voice note và screenshot là tùy chọn.
10. AI chỉ được dùng cho:
   - transcription voice note;
   - đề xuất taxonomy để người dùng xác nhận;
   - weekly summary từ metrics đã được deterministic engine tính.
11. AI không tính P&L, risk, expectancy, fee, funding, reconciliation hoặc market context; không dự báo, phát tín hiệu hay thực hiện lệnh.

### 1.2. Invariant bảo mật

Các invariant sau luôn đúng:

- User chỉ đọc, ghi, export và xóa dữ liệu thuộc Workspace của chính mình.
- Mọi entity tenant-owned có WorkspaceId không null và không thay đổi sau khi tạo.
- WorkspaceId dùng cho authorization phải lấy từ session phía server, không lấy từ request body, query, header tùy ý hoặc giá trị do client tin cậy.
- Background job, file object, user-derived cache entry, export và AI run đều giữ WorkspaceId trong toàn bộ vòng đời. Global public-market cache/provenance là ngoại lệ hẹp ở mục 2.2 và không được chứa user/episode/workspace ID.
- Object ID khó đoán không thay thế authorization.
- Thiếu identity, thiếu ownership hoặc authorization không xác định phải bị từ chối theo nguyên tắc deny by default.
- User data không xuất hiện trong public URL, telemetry công khai hoặc thông báo lỗi gửi cho người dùng khác.

## 2. Ownership và authorization

### 2.1. Quan hệ sở hữu

Quan hệ chuẩn là:

| Entity | Cardinality | Owner trực tiếp |
|---|---:|---|
| User | 1 | Identity từ managed provider |
| Workspace | 1:1 với User | User |
| TradingAccount | 1:1 với Workspace | Workspace |
| TradePlan, Fill, TradeEpisode, Review | N:1 | Workspace |
| ContextSnapshot, MetricSnapshot, deterministic WeeklyReport | N:1 | Workspace |
| Upload, Attachment, Export, AiRun | N:1 | Workspace |
| Public MarketBar/SourceRequest/IngestionBatch provenance | Global, shareable | Không tenant-owned; chỉ chứa Binance public data và technical fetch metadata |

Khóa identity nội bộ PHẢI dựa trên cặp stable Issuer + Subject của identity provider. Email, kể cả email đã xác minh, KHÔNG ĐƯỢC dùng làm khóa ownership ổn định hoặc tự động gộp hai identity.

Exact local ownership headers are:

```text
User
user_id
created_at

UserIdentity
identity_id
user_id
issuer
subject
provider_mode                  MANAGED_DEDICATED | SHARED_FEDERATED
identity_provider_registration_id
workspace_grant_handle_ciphertext nullable
workspace_grant_handle_key_version nullable
workspace_grant_handle_sha256    nullable
identity_generation           positive integer
created_at

Workspace
workspace_id
owner_user_id
lifecycle_state                ACTIVE | DELETING
deletion_guard_generation      positive integer
deletion_id                    nullable
timezone
created_at
deleting_at                    nullable
deleted_at                     nullable
```

`issuer` is the byte-exact issuer string from pinned provider metadata, and token `iss` must compare equal to it without trimming, URL normalization, percent-decoding, slash removal, path normalization or case folding. Provider configuration, before activation, separately requires an absolute HTTPS issuer under the approved host/path allowlist and rejects query/fragment; a valid configured trailing slash or path remains part of the identity key. `https://id.example/tenant` and `https://id.example/tenant/` are distinct issuers. `subject` is the provider's case-sensitive nonempty 1..255 Unicode-scalar identifier and is never normalized. Database enforces unique `(issuer, subject)`, unique `UserIdentity.user_id`, unique `Workspace.owner_user_id`, composite ownership FKs and one identity/workspace per User. MANAGED_DEDICATED requires all three workspace-grant fields null. SHARED_FEDERATED requires all three non-null; the ciphertext contains the provider-issued opaque Workspace grant/link locator under a restricted versioned key and its hash is SHA-256 of the exact decrypted UTF-8 locator. It never enters API/log/audit/export. First registration has `identity_generation = 1`; reuse after completed deletion follows section 8.3. Workspace starts ACTIVE with generation 1 and all deletion fields null. DELETING requires non-null `deletion_id/deleting_at`; `deleted_at` remains null until local purge. There is no active `DELETED` row: terminal deletion removes UserIdentity, User and Workspace after copying only the restricted deletion evidence defined in section 8.3.

Identity provider configuration is an immutable registry:

```text
IdentityProviderRegistration
identity_provider_registration_id
issuer
provider_mode                    MANAGED_DEDICATED | SHARED_FEDERATED
provider_configuration_generation
subject_delete_api_version       nullable
subject_delete_api_sha256        nullable
grant_unlink_api_version         nullable
grant_unlink_api_sha256          nullable
status_lookup_api_version
status_lookup_api_sha256
created_at

IdentityProviderRegistrationStateEvent
identity_provider_registration_state_event_id
identity_provider_registration_id
event_sequence                   positive integer contiguous per registration
event_type                       ENABLE | RETIRE
recorded_at
actor_system_principal_id
reason_code
```

Rows/events are append-only. Registration is unique by ID and `(issuer,provider_configuration_generation)`; issuer/mode/generation and all API version/hash bytes are immutable. MANAGED_DEDICATED requires delete fields non-null and unlink fields null; SHARED_FEDERATED is the reverse; both require idempotent status lookup. New authentication may bind only the greatest-event ENABLE registration whose issuer/mode exactly match the verified token/config. UserIdentity permanently copies its registration ID and mode. RETIRE blocks new identity binding but the configuration, credentials and APIs remain callable for every bound identity/incomplete deletion plus the backup-verification window. First SHARED_FEDERATED bootstrap must obtain and encrypt the provider grant locator in the same ownership-tree transaction; failure creates no local tree. FENCE decrypts that exact source locator only inside the identity gateway and re-encrypts it in `identity_provider_deletion_inventory_v1` before local identity deletion.

First successful callback for a new `(issuer,subject)` acquires a serialization/advisory lock on its SHA-256 lookup key and atomically inserts User, UserIdentity, Workspace, WorkspaceOwnerProfile/revision 1, TradingAccount and mandatory bootstrap records. A concurrent callback returns the same committed ownership tree. Existing identity returns its one workspace; if that workspace is DELETING, authentication/session creation fails `ACCOUNT_DELETION_IN_PROGRESS`. Email changes never create, merge or move ownership. Concurrent sign-in and deletion serialize on the same identity/workspace locks; after the FENCE event commits, no session or second workspace can be created.

### 2.2. Enforcement

- Mọi read/update/delete phải lọc đồng thời theo EntityId và WorkspaceId.
- Tạo entity phải gán WorkspaceId từ authenticated session.
- Không có API cho phép client thay WorkspaceId.
- Bulk operation, search, dashboard aggregation và export phải áp cùng tenant filter như single-object API.
- Signed URL cho attachment/export phải gắn với một object, một mục đích, một Workspace và thời hạn ngắn.
- Queue message phải chứa immutable WorkspaceId và exact initiator union: `{ "kind":"USER", "actorUserId":id }` hoặc `{ "kind":"SYSTEM", "systemPrincipalId":id }`, không bao giờ cả hai. USER ID phải bằng `Workspace.owner_user_id` trong model 1:1; system principal phải resolve approved workload identity được phép chạy work type đó. Worker copy/validate union từ TenantControlJob và kiểm tra lại ownership/generation trước khi đọc hoặc ghi; scheduled/system work không fabricate ActorId.
- Cache chứa user data hoặc derived tenant artifact phải namespace theo WorkspaceId và không dùng chung giữa các Workspace. Immutable Binance public MarketBar/cache/provenance được phép dùng chung khi key chỉ gồm venue/product/symbol/timeframe/source revision, không chứa event time do user cung cấp, episode/user/workspace ID hoặc user content. Tenant-scoped context job/audit/snapshot vẫn phải giữ WorkspaceId; current context dùng exact TP-MCE scope/as-of resolver, không có mutable active-pointer row. API không được cho tenant duyệt global cache ngoài reference của chính họ.
- Public source request có thể giữ interval đã bucket/aligned để fetch market bars, nhưng global record/request không được giữ triggering episode/workspace/user ID. CONTEXT submits only the public `(venue,product,symbol,timeframe,source,aligned interval)` key to an internal global market-data service; that service's read-only Binance GET is infrastructure work, not a tenant provider dispatch and uses no `tpw_` lease/deletion target. It may fill the global immutable cache after Workspace FENCE, while every tenant mapping/ContextSnapshot commit still rechecks the CONTEXT fence and is discarded after FENCE. Mapping từ tenant job sang request là tenant-owned và bị xóa cùng Workspace.
- AI request builder chỉ được lấy dữ liệu sau khi authorization đã thành công và phải ghi WorkspaceId vào audit metadata nội bộ; WorkspaceId không cần gửi cho AI processor.
- Xóa User phải xóa hoặc tombstone toàn bộ cây ownership; không để orphan entity có thể được một User khác nhận lại.

### 2.3. Lỗi và chống dò dữ liệu

- Với object không tồn tại và object thuộc tenant khác, API NÊN trả cùng một loại phản hồi để giảm khả năng dò ID.
- Search, count, pagination và timing không được làm lộ số lượng hoặc sự tồn tại dữ liệu tenant khác.
- Error payload không chứa SQL, storage key, đường dẫn nội bộ, raw provider response hoặc user data.

### 2.4. Truy cập vận hành

- Support/operator không phải member của Workspace và không có quyền xem nội dung theo mặc định.
- Không xây chức năng impersonation trong MVP.
- Break-glass access chỉ được phép cho xử lý incident nghiêm trọng, phải có ticket, lý do, phạm vi object, thời hạn tối đa 30 phút và phê duyệt thứ hai.
- Mọi break-glass access phải vào audit log. Owner phải được thông báo trong 24 giờ, trừ khi nghĩa vụ điều tra hoặc pháp lý cấm thông báo.

Owner notification is an authenticated first-party in-app control notice created synchronously, never email/webhook or an asynchronous notification job:

```text
BreakGlassOwnerNotice
break_glass_owner_notice_id
workspace_id
break_glass_audit_event_id
notice_type                       ACCESS_STARTED | ACCESS_ENDED | WITHHOLDING_RELEASED
visibility                        VISIBLE | WITHHELD_LEGAL
legal_exception_code              nullable; INVESTIGATION_HOLD | LEGAL_PROHIBITION
created_at
visible_at                        nullable
content_sha256
```

The authorized access-start/end transaction inserts its audit event and exactly one notice atomically. Normal branch is VISIBLE with `visible_at = created_at`, so it is available immediately and within 24 hours. WITHHELD_LEGAL requires one closed exception code and null visible time; when the separately authorized exception is released, that release transaction synchronously inserts one VISIBLE `WITHHOLDING_RELEASED` notice with no exception and `visible_at = created_at`, no later than 24 hours after release. `content_sha256` hashes RFC 8785 `{ "breakGlassAuditEventId":id, "createdAt":ts, "legalExceptionCode":str-or-null, "noticeType":str, "visibleAt":ts-or-null, "visibility":str, "workspaceId":id }`. The row contains no ticket/reason/object/content and is deleted with PRIMARY_TENANT_DATA; retry is unique on `(workspace_id,break_glass_audit_event_id,notice_type)` and changed bytes fail closed.

## 3. Authentication, session và re-authentication

### 3.1. Managed authentication contract

OIDC flow PHẢI:

- dùng Authorization Code với PKCE;
- kiểm tra signature, Issuer, Audience, expiration, nonce và state;
- chỉ chấp nhận email khi provider xác nhận email đã verified;
- từ chối token hết hạn, sai audience, sai issuer hoặc thuật toán chữ ký không nằm trong allowlist;
- không ghi ID token, access token, refresh token hoặc authorization code vào log.

Magic link PHẢI:

- do managed provider phát hành và sau callback phải yield cùng stable verified `(issuer,subject)` contract như OIDC; MVP không có local LoginAddress/email-to-owner table hoặc first-party magic-link issuer;
- dùng một lần;
- hết hạn không quá 15 phút sau khi phát hành;
- bị vô hiệu ngay sau khi dùng hoặc khi phát hành link mới theo cùng flow;
- có phản hồi trung tính để không tiết lộ email có tồn tại hay không.

Ứng dụng KHÔNG ĐƯỢC có password field, password table, password hash, password reset flow hoặc log chứa credential.

### 3.2. Session

- Session identifier phải ngẫu nhiên, không suy đoán được và được rotate sau sign-in và re-authentication.
- Với browser, session cookie phải có Secure, HttpOnly và SameSite=Lax hoặc nghiêm ngặt hơn.
- Session token không được lưu trong localStorage.
- Request thay đổi trạng thái phải có CSRF protection khi kiến trúc cookie yêu cầu.
- Idle timeout tối đa là 7 ngày; absolute timeout tối đa là 30 ngày.
- Logout phải vô hiệu session phía server trong tối đa 60 giây.
- Khi identity bị disabled, account bị xóa hoặc incident yêu cầu containment, mọi session phải có thể bị revoke tập trung.

### 3.3. Re-authentication

Một authentication event mới trong 10 phút gần nhất là bắt buộc trước khi:

- tạo hoặc tải export;
- xác nhận xóa toàn bộ Workspace;
- thay đổi identity hoặc email đăng nhập nếu sản phẩm cung cấp thao tác này;
- thực hiện break-glass operation.

Re-authentication phải đi qua managed provider. Ứng dụng không được tự hỏi lại mật khẩu.

### 3.4. Chống abuse authentication

Giới hạn ban đầu:

- tối đa 5 yêu cầu magic link cho một email trong 15 phút;
- tối đa 20 yêu cầu magic link từ một IP trong 15 phút;
- backoff tăng dần cho callback/token validation thất bại;
- phát hiện và cảnh báo spike đăng nhập thất bại hoặc đăng nhập từ vị trí bất thường khi provider có tín hiệu phù hợp.

Ngưỡng có thể được điều chỉnh bằng cấu hình, nhưng không được tắt rate limit trong production.

## 4. Phân loại và tối thiểu hóa dữ liệu

### 4.1. Phân loại

| Mức | Ví dụ | Kiểm soát tối thiểu |
|---|---|---|
| Public | Nội dung marketing, tài liệu công khai | Integrity và kiểm soát phát hành |
| Internal | Feature flag, metrics vận hành không chứa user data | Chỉ nhân sự/dịch vụ cần thiết |
| Confidential | CSV đã normalize, trades, plans, reviews, metrics, taxonomy, deterministic WeeklyReport và AI summary output | Tenant isolation, encryption, retention và audit |
| Restricted | Email, session/provider token, raw export, voice note, screenshot, deletion token, InternalAggregate cohort/member mapping | Least privilege chặt, không log nội dung, URL ngắn hạn, re-auth cho export/delete |

API key, API secret, private key, seed phrase và mật khẩu nằm ngoài dữ liệu hợp lệ của MVP. Nếu phát hiện chuỗi có khả năng là secret trong upload hoặc note, hệ thống phải cảnh báo người dùng, không đưa chuỗi đó vào AI và NÊN cho phép xóa ngay.

### 4.2. Data minimization

- Chỉ thu thập field cần cho journal, reconciliation, metrics, context, review và các chức năng AI đã cho phép.
- Không thu thập contact list, wallet credential, browser history hoặc dữ liệu exchange ngoài CSV do user chủ động tải lên.
- Screenshot phải được loại metadata như EXIF trước khi lưu lâu dài.
- Voice note không cần thông tin identity ngoài ngôn ngữ và nội dung audio.
- Analytics sản phẩm chỉ dùng event metadata tối thiểu; không gửi trade value, note, transcript, image, symbol hoặc P&L cho analytics processor.
- Region lưu trữ, quốc gia xử lý và mọi cross-border transfer phải được công bố trước khi launch.

### 4.3. Internal product-validation mapping

`InternalAggregateCohortDefinition` và `InternalAggregateCohortMember` theo `TP-LAB` là Restricted, nằm trong first-party restricted store riêng khỏi external analytics projection. Chỉ workload identity của aggregation service và break-glass operator được phê duyệt mới đọc được; UI, support, general analytics query và external processor không có quyền. Mọi access/change ghi actor/service, purpose, cohort key, time và outcome nhưng không ghi member list/workspace ID raw vào operational log.

Mapping TTL không được vượt quá earlier of approved study expiry và measurement-window end + 365 ngày. Workspace deletion xóa direct member mapping khỏi primary store trong 24 giờ; cache/index tối đa 72 giờ; encrypted backup tối đa 30 ngày và restore áp deletion tombstone trước query. Internal aggregate/HMAC digest đã bỏ member ID có thể giữ tới aggregate retention deadline nhưng không được dùng để re-identify hoặc rebuild membership. Mapping, HMAC token và secret key không được gửi tới external analytics/AI processor hoặc workspace export.

## 5. Encryption và quản lý secret

### 5.1. In transit và at rest

- Mọi kết nối public và service-to-service có user data phải dùng TLS 1.2 trở lên với cipher suite được hỗ trợ an toàn.
- Database, object storage, queue, cache bền vững, log store và backup phải được mã hóa at rest.
- Export và attachment không được đặt trong public bucket.
- Signed download URL phải có thời hạn tối đa 15 phút, chỉ có quyền đọc một object và bị vô hiệu khi Workspace chuyển sang trạng thái deleting.

### 5.2. Key và application secret

- Key và application secret phải nằm trong managed secret/key system hoặc cơ chế tương đương, tách khỏi source code và user data.
- Quyền dùng key phải theo least privilege và tách production khỏi non-production.
- Không đưa key/secret vào client bundle, image công khai, test fixture, ticket hoặc log.
- Key phải được rotate ít nhất mỗi 12 tháng và ngay khi có nghi ngờ compromise.
- Thay key phải có runbook và không làm mất khả năng đọc dữ liệu hợp lệ.

### 5.3. Môi trường

- Production user data không được sao chép sang development hoặc test.
- Test dùng synthetic hoặc anonymized fixture đã được kiểm tra không thể tái nhận dạng.
- Nhân sự và workload chỉ có quyền vào đúng environment cần thiết.

## 6. Upload, import và download an toàn

### 6.1. CSV

CSV import có giới hạn mặc định:

- tối đa 20 MiB mỗi file;
- tối đa 100.000 data rows;
- UTF-8 hoặc UTF-8 có BOM;
- không nhận archive, macro, spreadsheet binary, HTML hoặc remote URL;
- mỗi field tối đa 32 KiB, trừ field có giới hạn chặt hơn trong schema.

Parser PHẢI:

- parse theo streaming hoặc cơ chế có memory bound;
- không evaluate formula, command, template hoặc URL;
- có allowlist column/schema và validation kiểu dữ liệu;
- yêu cầu timezone khi timestamp nguồn không có offset;
- giới hạn precision và range của numeric field;
- từ chối NUL byte và control character không hợp lệ;
- không fetch tài nguyên được tham chiếu trong cell;
- staging dữ liệu trước khi commit;
- bảo đảm re-import cùng file/row không tạo Fill hoặc fee trùng lặp;
- hiển thị row lỗi mà không ghi raw row value vào operational log.

Raw CSV phải được xóa ngay khi không còn cần cho import và không muộn hơn exact `Upload.purge_due_at = RECEIVE + 24 giờ`, kể cả import chưa terminal. Dữ liệu normalized và provenance cần thiết được giữ theo vòng đời Workspace.

### 6.2. CSV export injection

- Text cell trong CSV export có ký tự đầu tiên sau whitespace là =, +, -, @, tab hoặc carriage return phải được escape theo cơ chế an toàn cho spreadsheet.
- Numeric field đã validate được serialize như numeric, không qua text interpolation.
- JSON là định dạng canonical để bảo toàn dữ liệu gốc; CSV là định dạng tiện dụng.
- Export phải đặt Content-Disposition là attachment và Content-Type chính xác.
- Test suite phải bao phủ formula payload, delimiter, quote, newline và Unicode.

### 6.3. Screenshot

- Chỉ nhận JPEG, PNG hoặc WebP.
- Tối đa 10 MiB, 40 megapixel và 12.000 pixel trên mỗi chiều.
- File extension và client Content-Type không được tin cậy; server phải decode thực tế.
- Image phải được decode/re-encode, strip metadata và malware scan trước khi trở thành attachment hợp lệ.
- MVP malware scanner is a pinned self-hosted/stateless process in the private validation environment: no network egress, no external scanning API, read-only engine image/signature bundle, ephemeral per-object input, and zero retained copy after the scan transaction. Engine image/signature versions and hashes are release evidence. Enabling any scanner that transfers or retains tenant bytes requires a registered processor/copy inventory, deletion target and contract amendment before production.
- Không nhận SVG hoặc HTML trong MVP.
- Screenshot không được gửi tới AI processor.

### 6.4. Voice note

- Voice extension mặc định giữ `voice_transcription_enabled = false`. Chỉ được bật khi `voice_ingest_profile_v1`, UPL-04 và processor compatibility suite đạt trong đúng production decoder image.
- Client accept/server sniff allowlist chỉ gồm: WebM EBML `DocType=webm` với đúng một Opus audio track và không video/data track; Ogg với đúng một logical Opus stream bắt đầu bằng `OpusHead`; hoặc RIFF/WAVE với đúng một PCM format-1 signed little-endian 16-bit audio stream. MIME/extension không thể thay sniff/decode result; AAC/MP3/MP4/FLAC, encrypted, chained/multi-program hoặc attached-picture input bị từ chối trong v1.
- Tối đa 25 MiB và 10 phút mỗi note.
- Decoded input phải có 1 hoặc 2 channel, declared/effective sample rate 8.000..48.000 Hz, duration `0 < duration <= 600.000 ms`, không timestamp âm/nonmonotonic và không vượt `48,000 * 2 * 600` decoded samples. Parser/decoder chạy sandbox không network, read-only image, tối đa 512 MiB memory và 120 giây CPU; malformed header, duration mismatch, decompression/sample bomb hoặc trailing executable content bị từ chối.
- Sau malware scan và full decode, canonical sanitized output là `audio/wav`: RIFF/WAVE chỉ có `fmt ` rồi `data`, PCM format 1 signed little-endian 16-bit, mono, 16.000 Hz, no LIST/JUNK/cue/metadata chunk. Stereo downmix, band-limited resample, clipping và dither dùng pinned `voice_sanitize_pcm16_mono_16k_v1`; decoder/resampler image digest là release evidence. Retained Attachment hash/size/media type và processor input đều lấy exact sanitized bytes này, không lấy inbound bytes.
- Voice chỉ được gửi tới AI processor khi user chủ động yêu cầu transcription và đã opt in.
- Sau khi user xác nhận transcript, raw audio được enqueue xóa ngay và trong mọi trường hợp không muộn hơn exact `Upload.purge_due_at = RECEIVE + 24 giờ`. User có thể chủ động chọn giữ original; khi đó chỉ sanitized RETAINED_VOICE Attachment theo retention của Workspace, còn raw inbound vẫn bị purge.

### 6.5. Quarantine và object access

- Upload nằm trong private quarantine cho đến khi hoàn thành validation và scan.
- Upload thất bại hoặc bị từ chối phải bị xóa trong 24 giờ.
- Object key phải ngẫu nhiên, không chứa email, symbol, filename gốc hoặc user-entered text.
- Mọi download phải authorization lại tại thời điểm cấp URL.
- Không render trực tiếp user-controlled HTML, SVG, CSV hoặc text như nội dung trusted trong cùng origin.

No provider object may be written before a durable non-exported `ObjectIngestReservation` exists:

```text
ObjectIngestReservation
object_ingest_reservation_id
workspace_id
purpose                         RAW_UPLOAD | SANITIZED_ATTACHMENT
reserved_upload_id              target Upload for RAW_UPLOAD; existing source Upload
                                for SANITIZED_ATTACHMENT
reserved_attachment_id          nullable; required only for SANITIZED_ATTACHMENT
sanitization_binding_type       nullable; UPLOAD_VALIDATE |
                                TRANSCRIPT_CONFIRMATION_INTENT
sanitization_binding_id         nullable
expected_upload_kind            CSV | SCREENSHOT | VOICE
encrypted_provider_object_key    non-null before ACTIVATED; null after transfer
provider_handle_key_version      non-null before ACTIVATED; null after transfer
lease_generation
write_capability_id
write_capability_hmac_key_version
write_capability_consumed_at      nullable
state                           RESERVED | BYTES_PRESENT | ACTIVATED |
                                ABORT_DELETE_REQUESTED | ABORT_ABSENCE_VERIFIED
provider_object_version         nullable before BYTES_PRESENT and after transfer
content_sha256                  nullable before BYTES_PRESENT and after transfer
byte_size                       nullable before BYTES_PRESENT and after transfer
created_at
write_expires_at
abort_delete_at
absence_due_at
activated_record_type           nullable; Upload | Attachment
activated_record_id             nullable
```

`write_expires_at = abort_delete_at = created_at + 15 minutes` and `absence_due_at = created_at + 1 hour`; none may be extended. Purpose coupling is exact: RAW_UPLOAD requires `reserved_attachment_id = null`, `sanitization_binding_type = null` and `sanitization_binding_id = null`, and preallocates `reserved_upload_id` as its target; SANITIZED_ATTACHMENT requires an existing same-workspace ACCEPTED-or-VALIDATING source `reserved_upload_id`, a preallocated `reserved_attachment_id`, and both sanitization fields non-null. SCREENSHOT pairs `UPLOAD_VALIDATE` with that source Upload's exact live validation job; VOICE pairs `TRANSCRIPT_CONFIRMATION_INTENT` with the exact keep-original intent defined in section 9.4.1. CSV cannot use SANITIZED_ATTACHMENT. `reserved_attachment_id` is globally unique within Workspace when non-null. A transaction-enforced partial unique key permits at most one nonterminal SANITIZED_ATTACHMENT reservation per `(workspace_id,reserved_upload_id)`; after ABORT_VERIFY a new user intent may allocate a new reservation/attachment ID, while concurrent preparation returns `SANITIZED_ATTACHMENT_PREPARATION_IN_PROGRESS`.

`write_capability_id` is exactly `"oirw_" + base64url_no_pad(HMAC-SHA256(key[write_capability_hmac_key_version], RFC8785({ "leaseGeneration": int, "objectIngestReservationId": id, "providerObjectKeySha256": SHA256(decrypted exact key bytes), "workspaceId": id, "writeExpiresAt": ts })))`; raw capability/key never appears in logs. Only a RAW_UPLOAD capability may be returned to the authenticated client; a SANITIZED_ATTACHMENT capability remains inside the trusted validation service and is never returned by any API. The referenced HMAC key remains available until reservation-shell purge plus the backup window. The RESERVE transaction also creates the exact OBJECT_INGEST_FINALIZE TenantControlJob/fence/ENQUEUE for this reservation purpose/generation; all effects roll back together and no capability is issued without the chain. It commits before issuing this 15-minute single-use capability scoped to the one random encrypted provider key and lease generation. Bytes pass through a revocation-aware upload gateway which locks the reservation and Workspace, verifies the exact binding plus ACTIVE/current captured generation, requires `write_capability_consumed_at = null` and current time before expiry, and uses a provider conditional create that permits exactly one immutable version; it then atomically records RECORD_BYTES and sets `write_capability_consumed_at` to that trusted commit time. The field is non-null iff RECORD_BYTES occurred and never changes. Replay is rejected before provider write. The capability cannot list/read/delete or write another key and is revoked on RECORD_BYTES, TRANSFER or ABORT_DELETE. A crash after provider create but before RECORD_BYTES is still recoverable because the RESERVED row identifies the key; retry/sweeper inventories every version through the finalizer fence rather than issuing a second create. All rows have direct Workspace ownership and remain included in the workspace TEMPORARY_OBJECTS deletion inventory until ACTIVATED or absence-verified.

State is derived from an append-only `ObjectIngestReservationEvent` with direct workspace/reservation IDs, contiguous `event_sequence`, `event_type = RESERVE | RECORD_BYTES | TRANSFER | ABORT_DELETE | ABORT_VERIFY`, trusted `recorded_at`, idempotency key and nullable absence-verification ID. Unknown/missing/extra transitions fail. At `abort_delete_at`, any non-ACTIVATED reservation revokes write/read, begins idempotent delete of all versions at the reserved key through non-overlapping finalizer external-operation leases and must commit ABORT_VERIFY with a replica-aware safe receipt by `absence_due_at`; that transaction also appends COMPLETE/`INGEST_ABORT_ABSENCE_VERIFIED` and its marker. An ACTIVATED reservation instead must complete the clean second-inventory path by the same immutable `absence_due_at`. Failure on either path is a severity-one retention incident and never fabricates terminal state. Retry spacing is at most five minutes. ACTIVATED and ABORT_ABSENCE_VERIFIED are mutually exclusive terminal states.

For RAW_UPLOAD, the transfer transaction creates the Upload header, RECEIVE event, UploadObjectLease and forced-purge command, plus the exact UPLOAD_VALIDATE TenantControlJob/fence/ENQUEUE for that Upload, then marks the reservation ACTIVATED to it; `RECEIVE.recorded_at` is this transaction time and starts the separate 24-hour Upload clock. Its tagged payload is selected from authenticated request kind and, for CSV, the same-workspace account plus pinned adapter request; all these effects roll back together.

SANITIZED_ATTACHMENT is always a prepare-then-transfer saga; provider write is never claimed to be atomic with a domain transaction. For SCREENSHOT, after the source Upload is VALIDATING and decode/re-encode, metadata stripping and scan have passed, the exact UPLOAD_VALIDATE worker transaction creates one source-bound reservation, preallocates its Attachment ID and enqueues its finalizer. The trusted validator then writes only the canonical sanitized bytes through the internal capability and RECORD_BYTES. While trusted time is strictly before both reservation `abort_delete_at` and source Upload `forced_purge_at`, one transaction locks both fences and records, rechecks PASSED evidence, inventories exactly the recorded version, appends Upload ACCEPT, transfers the reservation into AttachmentObjectLease plus the preallocated Attachment header/ACTIVATE event, and terminalizes UPLOAD_VALIDATE. No Attachment row exists before that transaction. A decode, scan, write, inventory or deadline failure appends Upload REJECT with `SANITIZED_ATTACHMENT_PREPARATION_FAILED`, terminalizes UPLOAD_VALIDATE, starts raw purge and leaves the reservation finalizer to delete/verify every staged version; it never activates partial bytes.

For retained VOICE, the first explicit TranscriptConfirmation request with `keep_original = true` creates the source-bound reservation and confirmation intent atomically before any prepared-object write. The request path or its exact retry reads the still-readable raw Upload, reruns the pinned sanitizer, requires its resulting hash to equal the TRANSCRIPTION AiRun input-reference payload hash, writes through the internal capability and RECORD_BYTES, then invokes the confirmation transaction described in section 9.4.1. That transaction may transfer only the intent-bound BYTES_PRESENT reservation and must commit strictly before `min(ObjectIngestReservation.abort_delete_at, Upload.forced_purge_at)`; it atomically transfers into the preallocated Attachment/ACTIVATE/lease together with the target revision, TranscriptConfirmation, command receipt and raw-purge advance. A timeout, mismatch, stale target/output, FENCE or provider failure produces no revision/confirmation/Attachment, forces reservation abort/delete/verification, and returns the stable intent outcome specified there. `keep_original = false` creates no sanitized reservation and commits through the direct confirmation branch.

Immediately before either SANITIZED_ATTACHMENT transfer, the transaction resolves the reservation finalizer chain, locks/rechecks Workspace, revokes the capability and inventories the key; exactly one version equal to recorded BYTES_PRESENT is required. Any extra/changed version aborts activation and deletes/verifies all versions. Transfer moves, never copies, the encrypted locator/key version, exact provider version and byte metadata into the target lease/header and nulls those five reservation fields in the same transaction. After `write_expires_at`, the activated-path finalizer follows `activated_record_type/id` to the exact target UploadObjectLease or AttachmentObjectLease, decrypts that moved locator only inside the storage gateway, and inventories every version at the key. Exactly the transferred application-owned version may remain. If a late/non-transferred version appears, the same OBJECT_INGEST_FINALIZE fence and non-overlapping external-operation leases delete and replica-verify each extra while access continues to address only the pinned transferred version; the target lease itself is not terminalized or cleared. The reservation shell stays in TEMPORARY_OBJECTS and the job stays nonterminal until a clean inventory proves only the target version remains. That clean proof must commit by `absence_due_at` and atomically append COMPLETE/`INGEST_ACTIVATED_CLEAN` plus marker; missing the deadline is severity one and continues remediation without fabricated completion. The content-free activated reservation/event shell is deleted within 24 hours after this clean proof. Aborted reservation/event/proof rows are deleted within 24 hours after ABORT_VERIFY. Workspace deletion's TEMPORARY_OBJECTS inventory includes every not-yet-purged reservation regardless of state, while the transferred object is enumerated only through its target lease. Unique target IDs and reservation/target composite FKs make transfer exactly once. A provider write crash before either transaction remains reservation-owned and is swept; a crash after commit is target-lease-owned for the transferred version while the still-live reservation finalizer owns any extras. Workspace FENCE revokes the capability, ends/looks up any external lease, marks the finalizer CANCELLED_DELETION and forbids later TRANSFER/inventory commits; post-drain TEMPORARY_OBJECTS deletes/verifies every reserved-key remnant, coordinated with TENANT_OBJECTS so the same target key is not acted on concurrently. Restore tooling runs this activated inventory/remediation or abort cleanup before traffic and deletes only proven-clean expired shells; no binary is downloadable, joinable or exportable while reservation-owned.

### 6.6. Upload và Attachment state machine

Exact contract version cho state model này là `upload_attachment_v1`; mọi Upload, Attachment, state event, API response và export projection phải persist/emit identifier này.

`Upload` là durable metadata của một inbound object, không phải raw bytes:

```text
upload_id
workspace_id
actor_user_id
contract_version              upload_attachment_v1
upload_kind                   CSV | SCREENSHOT | VOICE
state                         QUARANTINED | VALIDATING | ACCEPTED | REJECTED | PURGED
source_sha256
byte_size
detected_media_type           nullable trước decode
created_at
accepted_at                   nullable
terminal_at                   nullable
purge_due_at
safe_error_code               nullable
```

`state` là derived projection từ append-only `UploadStateEvent`:

```text
upload_state_event_id
workspace_id
upload_id
contract_version              upload_attachment_v1
event_sequence                positive integer contiguous từ 1
event_type                    RECEIVE | START_VALIDATION | ACCEPT | REJECT | PURGE
recorded_at
actor_type                    USER | SYSTEM
actor_user_id                 nullable cho SYSTEM
idempotency_key
safe_reason_code              nullable
object_absence_verification_id nullable; non-null only for PURGE
```

Mọi row có direct immutable `workspace_id`; event dùng composite FK `(workspace_id, upload_id)` và `contract_version` bằng header. `(workspace_id, upload_id, event_sequence)` và `(workspace_id, idempotency_key)` unique. Allowed transition là `QUARANTINED -> VALIDATING | REJECTED`, `VALIDATING -> ACCEPTED | REJECTED`, rồi `ACCEPTED | REJECTED -> PURGED`; retry cùng logical event trả cùng effect. `REJECTED` tạo zero ImportPreview/ImportBatch/business record. Với CSV, ACCEPT, immutable TP-ACC ImportPreview + CREATE event, first-party `import_previewed` event và UPLOAD_VALIDATE terminal marker là một transaction; ACCEPT không được trực tiếp tạo ImportBatch/fill/episode/ledger/context/metric. Screenshot/voice giữ ACCEPT behavior riêng của section này. Chỉ `ConfirmImport` sau đó mới atomically tạo ImportBatch cùng IMPORT control chain theo TP-ACC. Raw object vẫn phải PURGE trong retention window.

`purge_due_at = RECEIVE.recorded_at + 24 hours` cho mọi `CSV | SCREENSHOT | VOICE`, được persist trong transaction RECEIVE và không bao giờ gia hạn. `forced_purge_at = purge_due_at - 4 hours`, tức `RECEIVE + 20 hours`, là derived deadline cố định và được enqueue trong cùng transaction RECEIVE. Exact trigger để worker bắt đầu delete là thời điểm sớm nhất trong: CSV khi ImportPreview ABANDON, unconfirmed preview reaches exact TP-ACC `expires_at`, or ImportBatch terminal; SCREENSHOT ngay sau Attachment ACTIVATE hoặc REJECT; VOICE ngay sau TranscriptConfirmation, RETAINED_VOICE Attachment ACTIVATE hoặc REJECT; và `forced_purge_at` cho mọi kind. Preview CREATE atomically advances the existing UPLOAD_PURGE due command to its immutable earlier expiry; it does not create a new work type. Tại `forced_purge_at`, một Upload còn QUARANTINED hoặc VALIDATING atomically chặn mọi raw-byte read, append REJECT với `safe_reason_code = RAW_UPLOAD_RETENTION_DEADLINE`, terminal hóa work item cùng safe code, rồi bắt đầu delete; user phải upload lại. Một Upload đã ACCEPTED cũng bị read-deny và bắt đầu delete tại mốc này nếu natural trigger chưa chạy. `keep_original` chỉ tạo sanitized retained Attachment trước mốc này, không gia hạn raw lease.

Raw object deletion is an idempotent outbox saga. Internal non-exported `UploadObjectLease` pins `(workspace_id, upload_id, provider_object_version, lease_generation, forced_purge_at, purge_due_at, state ACTIVE|DELETE_REQUESTED|ABSENCE_VERIFIED, next_retry_at)`; both deadlines equal the Upload-derived values and are immutable. The RECEIVE transaction creates this lease and one durable forced-purge command. Provider bucket/key is encrypted Restricted data and never copied to domain records/logs. Each delete/HEAD-equivalent retry is append-only `UploadObjectDeletionAttempt` with `(workspace_id, upload_id, attempt_no)`, action `DELETE | VERIFY_ABSENCE`, outcome `SUCCEEDED | ABSENT | RETRYABLE`, trusted timestamps and safe provider code, unique/idempotent per attempt. The first attempt starts immediately at an earlier natural trigger or no later than `forced_purge_at`. A RETRYABLE result schedules the next attempt no later than `min(previous_completed_at + 5 minutes, purge_due_at - 1 minute)`; after successful DELETE, VERIFY_ABSENCE starts immediately and follows the same maximum five-minute retry spacing. Production object storage must provide version-specific delete plus replica-aware absence lookup with a reviewed worst-case SLA no greater than the four-hour forced-purge window; otherwise upload is not release-ready. A successful absence check writes:

```text
UploadObjectAbsenceVerification
object_absence_verification_id
workspace_id
upload_id
provider_object_version
lease_generation
verified_absent_at
verification_method            PROVIDER_VERSION_LOOKUP | PROVIDER_INVENTORY
verification_receipt_sha256
```

The verification has composite FK to the exact lease/upload, lowercase SHA-256 of a provider receipt that contains no URL/key/content, and unique `(workspace_id, upload_id, lease_generation)`. Absence verification, lease transition to ABSENCE_VERIFIED, clearing encrypted provider bucket/key/handle-key-version and `next_retry_at`, PURGE and the CSV/VOICE SubjectDeletionReceipt are one transaction; therefore no committed proof-before-PURGE state exists and no terminal lease retains a provider locator. The content-free lease header retains only workspace/upload, application-owned object version, lease generation, state and terminal time required by the proof FK. PURGE references that verification and every earlier event has null verification. `verified_absent_at <= purge_due_at` is the hard physical-retention predicate. Screenshot source PURGE is proven by the same record but creates no archive tombstone because the sanitized Attachment is the canonical binary. A crash before the transaction retries delete/verification; a crash after commit observes all effects and returns them idempotently; retry never recreates a raw object. Attempt/outbox rows are removed child-first within 30 days after PURGE; lease/proof/receipt remain content-free until Workspace deletion. At `purge_due_at`, an Upload without timely matching verification remains read-denied and non-PURGED, raises a severity-one retention incident and continues deletion/verification; it never fabricates compliance or moves the deadline.

Với screenshot hoặc retained voice đã decode/scan/sanitize thành công, activation transaction được định nghĩa dưới đây tạo đúng một `Attachment`:

```text
attachment_id
workspace_id
source_upload_id
contract_version              upload_attachment_v1
attachment_kind               SCREENSHOT | RETAINED_VOICE
state                         ACTIVE | DELETING | DELETED
scan_status                   PASSED
content_object_version
content_sha256
byte_size
media_type
original_filename             nullable
safe_display_filename
created_at
deleted_at                    nullable
```

`content_object_version` là application-owned immutable content version ID, không phải bucket/key/signed URL. Provider location nằm trong tenant-scoped `AttachmentObjectLease` nội bộ và không export/log. Object bytes, hash, size và media type không được thay tại chỗ; replace tạo attachment ID mới. `ACTIVE` luôn có non-null content version/hash/size và `scan_status = PASSED`. Upload source/quarantine bytes được purge sau khi sanitized content activate; screenshot bytes không bao giờ là raw undecoded input.

Attachment state là projection từ:

```text
attachment_state_event_id
workspace_id
attachment_id
contract_version              upload_attachment_v1
event_sequence                positive integer contiguous từ 1
event_type                    ACTIVATE | DELETE_REQUEST | DELETE_COMPLETE
recorded_at
actor_type                    USER | SYSTEM
actor_user_id                 nullable cho SYSTEM
idempotency_key
safe_reason_code              nullable
object_absence_verification_id nullable; non-null only for DELETE_COMPLETE
```

Database enforces unique `(workspace_id, source_upload_id)` on Attachment plus an ordinary composite header FK `(workspace_id, source_upload_id)` to Upload. Source validity is not a current-state FK: the deferred activation constraint requires an immutable same-transaction-or-prior same-workspace ACCEPT event, non-null `accepted_at = ACCEPT.recorded_at`, and exact kind mapping SCREENSHOT -> SCREENSHOT or VOICE -> RETAINED_VOICE; CSV can never source an Attachment. This proof remains valid after the source Upload later becomes PURGED. The activation transaction is the SCREENSHOT Upload ACCEPT transaction or the final keep-original TranscriptConfirmation transaction. It consumes the exact source/binding/preallocated-ID reservation described above, transfers its locator into the AttachmentObjectLease, and inserts the Attachment header plus sequence-1 ACTIVATE atomically; header `created_at = ACTIVATE.recorded_at`, no Attachment may exist without that event and retry returns the same header. Concurrent validators/intents therefore cannot create two attachments from one upload, and neither branch performs provider write inside this transaction.

Event dùng composite FK `(workspace_id, attachment_id)`, `contract_version` bang header, va uniqueness cùng quy tắc Upload. `ACTIVATE -> DELETE_REQUEST -> DELETE_COMPLETE` là transition duy nhất; DELETE_REQUEST atomically chuyển derived state sang `DELETING`, revoke download URL, chặn Review join/export pin mới và enqueue object deletion. Attachment object deletion uses the same lease/outbox/attempt rules as Upload and writes this exact evidence:

```text
AttachmentObjectAbsenceVerification
object_absence_verification_id
workspace_id
attachment_id
content_object_version
lease_generation
verified_absent_at
verification_method            PROVIDER_VERSION_LOOKUP | PROVIDER_INVENTORY
verification_receipt_sha256
```

The evidence has composite FK to exact same-workspace Attachment/lease, unique `(workspace_id,attachment_id,lease_generation)`, no provider key/URL/content and the same safe receipt-hash rule as Upload verification. Absence verification, lease terminal transition, clearing encrypted provider bucket/key/handle-key-version and next retry, DELETE_COMPLETE and exact `ATTACHMENT_BINARY` SubjectDeletionReceipt are one transaction after every active provider replica is absent; no committed proof-before-DELETE_COMPLETE state exists, no terminal lease retains a provider locator and other events have null verification. Attempt/outbox rows are removed child-first within 30 days after DELETE_COMPLETE; the content-free lease header/proof/receipt remain until Workspace deletion. Derived state is `DELETED`. `TP-SEC` không tạo một domain tombstone shape khác: `TP-EXP` deterministically projects the receipt thành generic archive Tombstone. Item delete idempotent, hoàn tất primary object trong 24 giờ, không xóa historical ReviewRevisionAttachment ID/hash. Export archive chứa attachment bị delete phải được revoke/xóa theo `TP-EXP`; archive không phải retention exception.

Review chỉ được attach trong cùng transaction ownership check khi `Attachment.state = ACTIVE`, `scan_status = PASSED` và kind `SCREENSHOT`; `(workspace_id, attachment_id)` composite FK là bắt buộc. Upload/Attachment error trả stable code `UPLOAD_INVALID_TRANSITION`, `UPLOAD_REJECTED`, `ATTACHMENT_NOT_ACTIVE`, `ATTACHMENT_DELETE_IN_PROGRESS` hoặc `ATTACHMENT_OWNERSHIP_MISMATCH`, không trả object key, filename hoặc tenant ID khác.

## 7. Logging, audit và observability

### 7.1. Operational log

Operational log không được chứa:

- credential, session token, OIDC token, magic link;
- raw CSV row hoặc full filename do user đặt;
- note, transcript, screenshot, audio;
- exact position, P&L, account value hoặc export payload;
- full email hoặc signed URL.

Log được phép chứa timestamp UTC, service, event type, outcome, latency, error code và cryptographically random per-request correlation ID. Operational/error telemetry may not contain Workspace/User/domain IDs or any deterministic hash/pseudonym derived from them. Correlation IDs reveal no tenant by themselves; AUDIT_MINIMIZATION removes their tenant audit mapping by `local_due_at`, so a retained log/error-tracker row is no longer joinable after local purge.

Operational log retention mặc định là 30 ngày. Security alert có thể giữ 90 ngày nếu không chứa user content.

### 7.2. Audit log

Audit log là append-only ở tầng ứng dụng. Exact common row has `audit_event_id`, `scope`, nullable `actor_id`, nullable `system_actor_code`, nullable `workspace_id`, `action`, nullable `target_type`, nullable `target_id`, trusted UTC `recorded_at`, `outcome`, request/correlation ID, nullable break-glass reason/ticket, nullable `provider_configuration_id`, nullable `pre_auth_attempt_hmac`, nullable `safe_failure_code` và metadata tối thiểu không chứa user content.

`scope = POST_AUTH` requires non-null WorkspaceId and exactly one ActorId or closed system actor; provider/pre-auth fields are null. `scope = PRE_AUTH` is allowed only for SIGN_IN_FAILED before stable identity resolution: actor/workspace/target and break-glass fields are null, `provider_configuration_id` is the internal allowlisted config ID rather than raw issuer, and `pre_auth_attempt_hmac = HMAC-SHA256(daily_rotating_audit_key, UTF8(request_correlation_id))`. Its closed failure codes are `OIDC_METADATA_INVALID | OIDC_SIGNATURE_INVALID | OIDC_ISSUER_MISMATCH | OIDC_AUDIENCE_MISMATCH | OIDC_EXPIRED | OIDC_NONCE_INVALID | OIDC_STATE_INVALID | MAGIC_LINK_INVALID | MAGIC_LINK_EXPIRED | MAGIC_LINK_REPLAYED`; unknown provider input maps to `OIDC_METADATA_INVALID` without storing submitted issuer/subject/email/token. If stable identity resolves before denial, the event is POST_AUTH under its real workspace. Daily audit HMAC keys are restricted, retained only for the 365-day audit window and never exported. These are the only AuditEvent scopes: post-FENCE deletion milestones use the purpose-built WorkspaceDeletionStateEvent, not a new AuditEvent after minimization.

POST_AUTH rows therefore ghi:

- ActorId hoặc system actor;
- WorkspaceId nội bộ;
- action;
- target type và target ID;
- timestamp UTC;
- outcome;
- request/correlation ID;
- reason/ticket cho break-glass;
- metadata tối thiểu, không chứa user content.

Các event bắt buộc gồm:

- sign-in thành công/thất bại, logout, session revoke;
- CSV import bắt đầu/kết thúc/thất bại và số row;
- attachment create/delete;
- export request/create/download/expire;
- Workspace deletion request/FENCE in the authenticated request transaction; LOCAL/SECONDARY/COMPLETE are audited solely by their append-only WorkspaceDeletionStateEvent so no raw WorkspaceId is reintroduced after AUDIT_MINIMIZATION;
- AI opt-in, opt-out và consent version;
- AI run theo feature và validation outcome;
- authorization denial bất thường;
- break-glass request/access/end.

Audit log giữ 365 ngày. Sau account deletion, audit chỉ được giữ dưới dạng tối thiểu/pseudonymous để chứng minh deletion và điều tra bảo mật; không giữ nội dung hoặc email. Hết 365 ngày phải xóa; v1 không có legal-hold override.

### 7.3. Phát hiện rò rỉ

- CI và production log pipeline phải có secret/PII redaction test.
- Error tracking processor phải nhận payload đã scrub theo exact operational allowlist trên: no stable tenant/domain ID or deterministic pseudonym, only random per-request correlation ID whose tenant mapping is removed at AUDIT_MINIMIZATION.
- Alert bắt buộc cho cross-tenant authorization denial spike, export spike, malware detection, nhiều session revoke và break-glass access.

## 8. Retention, export và deletion

### 8.1. Retention schedule

| Loại dữ liệu | Retention tối đa |
|---|---|
| Normalized trades, plans, reviews, metrics, deterministic summaries | Đến khi user xóa toàn bộ TradeProof account; correction dùng append-only revision, không hard-delete item riêng trong MVP |
| TP-ACC StagedFill/disposition | Đến khi user xóa Workspace; immutable normalized candidate/evidence cần cho unresolved queue và resolution round-trip, không chứa raw CSV cell |
| TP-ACC ImportPreview header/event/summary | Xóa child-first không muộn hơn 30 ngày sau CONFIRMED/ABANDONED/EXPIRED; non-exported, không raw cell/provider locator |
| ProductMeasurementRun/state prefix | Đến khi Workspace bị xóa; exact `product_measurement_run_v1` replay source, không client/browser/content data; registered timeout control follows work-control retention |
| ProductAnalyticsEvent và WorkspaceProductMetricSnapshot | Đến khi user xóa Workspace; chỉ first-party allowlist `product_analytics_event_v1` |
| InternalAggregateProductMetricSnapshot không member ID | 365 ngày sau measurement window |
| InternalAggregate cohort definition/member mapping/retirement | Definition và non-identifying `internal_aggregate_cohort_retirement_v1` đến earlier of approved study expiry và measurement-window end + 365 ngày; direct Workspace member mapping xóa ≤24 giờ khi account deletion, backup ≤30 ngày; retirement xóa cuối cùng với definition |
| ProductAnalyticsExternalProjection/encrypted delivery locator | Inaccessible and provider absence-verified no later than source UTC-day-start + 90 ngày; envelope/locator removed in purge completion transaction |
| ProductAnalyticsExternalSuppressionReceipt | Exact restricted reason/hash only; xóa tại `suppressed_at + 30 ngày` hoặc sớm hơn cùng Workspace primary-data deletion; không projection, pseudonym, locator hoặc provider call |
| ProductAnalyticsExternalDeletionReceipt | Đến Workspace deletion; minimized Restricted hashes/generation only, no pseudonym, payload or handle |
| ProductAnalyticsPseudonymRotation/key material | Metadata while audit requires; secret key until every generation projection is acknowledged/suppressed and every possible copy deletion-verified plus backup window, then destroy |
| Successful AiRun/AiRunInputReference/AiOutput/AiOutputReference bundle | Đến khi user xóa AI output hoặc toàn bộ TradeProof account; output delete xóa cả bundle và tạo receipt |
| Nonterminal AiProcessorCopyReference encrypted handle | Đến provider absence/no-copy evidence, output delete hoặc Workspace deletion; processor retention deadline tối đa dispatch + 30 ngày |
| Terminal AiProcessorCopyReference evidence | Active output giữ đến output/Workspace deletion; no-output hoặc deleted-output row xóa trong 30 ngày sau owner purge/delete |
| Payload-free AiOutputSubject/state, confirmation integrity hashes và AI Tombstone/receipt | Đến khi Workspace bị xóa; là Restricted derived personal data, không phải anonymous/content-free evidence |
| Terminal AiRun không có output và input references của nó | Không trước terminal state + 30 ngày và chỉ sau terminal copy evidence + AI_RUN marker; child-first purge trong 30 ngày tiếp theo, không tạo AI_OUTPUT tombstone |
| AI configuration artifact/release/eval registry | Ít nhất đến khi không còn AiRun tham chiếu release/hash và thêm 365 ngày audit; immutable config không chứa user content, credential hoặc secret value |
| Terminal TenantControlJob/fence/event/external-operation detail | Tối đa 30 ngày sau terminal, hoặc compact sớm khi domain subject bị xóa; chỉ sau khi TenantWorkItemTerminalMarker đã commit |
| TenantWorkItemTerminalMarker | Đến khi Workspace deletion JobDrainEvidence consumes exact sequence; không chứa subject/job/provider ID hoặc payload |
| SubjectDeletionReceipt | Đến khi Workspace bị xóa; chỉ chứa ID/hash/policy/reason an toàn, không chứa deleted content |
| Raw CSV inbound | `Upload.RECEIVE + 24 giờ`; terminal import có thể purge sớm hơn |
| Raw screenshot source, upload lỗi/quarantine/job temporary | `Upload.RECEIVE + 24 giờ`; sanitized Attachment là record giữ lại |
| Raw voice inbound | `Upload.RECEIVE + 24 giờ`; confirmation/keep-original có thể purge sớm hơn, không gia hạn deadline |
| ObjectIngestReservation staging/shell/evidence | Raw/sanitized staging absent theo deadline 1 giờ; content-free shell/event/proof xóa trong 24 giờ sau ACTIVATED hoặc ABORT_VERIFY |
| Terminal Upload/Attachment object-control attempts/outbox | 30 ngày sau PURGE/DELETE_COMPLETE; encrypted provider locator bị clear trong terminal transaction |
| Content-free Upload/Attachment lease header, absence verification và SubjectDeletionReceipt | Đến khi Workspace bị xóa; chỉ giữ application object version/generation/hash/evidence cần cho FK/export, không provider bucket/key/URL |
| Screenshot và voice được chọn giữ | Đến khi user xóa item hoặc Workspace |
| Generated export archive | 24 giờ sau khi tạo |
| Operational log | 30 ngày |
| Security alert không chứa content | 90 ngày |
| Audit log tối thiểu | 365 ngày |
| Incomplete WorkspaceDeletion control/evidence graph | Đến khi COMPLETED; không được TTL khi còn dùng để retry/prove deletion, và SLA breach không tạo retention waiver |
| Completed WorkspaceDeletion header/events/targets/attempts/outbox, JobDrainEvidence, deletion-scoped TenantControlJob/fence/operation leases | Xóa không muộn hơn `completed_at + 365 ngày`, sau khi audit/minimization và backup window hoàn tất |
| WorkspaceDeletionTombstone | Sớm nhất `completed_at + 365 ngày`; giữ lâu hơn chỉ khi predecessor chain của active/incomplete successor còn cần theo mục 8.3 |
| Encrypted rolling backup | Tối đa 30 ngày |
| AI processor request content | Không quá 30 ngày theo hợp đồng; ưu tiên zero retention |

Retention job phải chạy ít nhất mỗi ngày và có metric/alert khi quá SLA. For a completed deletion, FK-safe control cleanup runs child-first: external-operation leases and outbox/attempt rows; target rows and JobDrainEvidence; work-item fence events/fences and TenantControlJob rows; deletion state events; WorkspaceDeletion header; then WorkspaceDeletionTombstone only when its separate generation-chain predicate permits. Referenced HMAC/lookup keys cannot be destroyed before their last row and backup window. No deletion/workspace/owner/provider ID, subject HMAC or receipt survives this cleanup except a still-required minimal generation tombstone and the separately allowed pseudonymous security audit; aggregate SLA counters may remain only without a joinable ID. V1 has no legal-hold branch in this scheduler.

#### 8.1.1. Subject deletion receipt và archive handoff

`TP-SEC` sở hữu canonical producer contract `SubjectDeletionReceipt`; `TP-EXP` chỉ deterministically project receipt thành archive Tombstone. Receipt có đúng các field sau:

```text
workspace_id
subject_type                  ATTACHMENT_BINARY | AI_OUTPUT | RAW_IMPORT_OBJECT | RAW_VOICE_OBJECT | TRANSCRIPT_DRAFT
subject_id
completed_at
reason_code                   nullable safe code
last_known_sha256
source_retention_policy       TP-SEC:ATTACHMENT_DELETE | TP-SEC:AI_OUTPUT_DELETE | TP-SEC:RAW_IMPORT_24H | TP-SEC:RAW_VOICE_24H | TP-SEC:TRANSCRIPT_DRAFT_DELETE
source_record_type
source_record_id
idempotency_key
```

Mọi field trừ `reason_code` non-null. Receipt có direct immutable `workspace_id`; `(workspace_id, subject_type, subject_id)` và `(workspace_id, idempotency_key)` unique. Retry cùng tuple/payload trả cùng receipt; đổi timestamp, hash, policy hoặc non-null reason trên tuple đã có fail `SUBJECT_DELETION_RECEIPT_CONFLICT`. `last_known_sha256` là lowercase SHA-256 của exact bytes cuối cùng trước delete/purge; receipt không chứa content, object key, filename, note, prompt hoặc processor response.

Delete/purge event và receipt được ghi trong cùng database transaction khi cùng store. Khi object/provider operation không thể atomic với database, transaction hoàn tất delete ghi transactional outbox mang exact receipt payload; consumer idempotent materialize receipt trước khi bất kỳ export nào có thể READY. Cutoff thấy delete/PURGE nhưng chưa thấy receipt phải retry/fail `EXPORT_TOMBSTONE_INVALID`, không được bỏ record hay tự dựng hash.

Mapping producer là đóng:

| `subject_type` | Exact subject/source | `completed_at` và hash | Null-reason fallback | Policy |
|---|---|---|---|---|
| `ATTACHMENT_BINARY` | `subject_id = Attachment.attachment_id`; source là exact `AttachmentStateEvent` DELETE_COMPLETE ID/type | event `recorded_at`; retained `Attachment.content_sha256` | `USER_DELETED` | `TP-SEC:ATTACHMENT_DELETE` |
| `AI_OUTPUT` | `subject_id = AiOutput.ai_output_id` cho TAXONOMY_SUGGESTION/WEEKLY_SUMMARY; source type `AI_OUTPUT_DELETE`, source ID bằng subject ID | local delete completion; output `content_sha256` captured trước delete | `USER_DELETED` | `TP-SEC:AI_OUTPUT_DELETE` |
| `RAW_IMPORT_OBJECT` | `subject_id = Upload.upload_id`, kind CSV; source là exact UploadStateEvent PURGE ID/type | event `recorded_at`; `Upload.source_sha256` | `RETENTION_EXPIRED` | `TP-SEC:RAW_IMPORT_24H` |
| `RAW_VOICE_OBJECT` | `subject_id = Upload.upload_id`, kind VOICE; source là exact UploadStateEvent PURGE ID/type | event `recorded_at`; `Upload.source_sha256` | `RETENTION_EXPIRED` | `TP-SEC:RAW_VOICE_24H` |
| `TRANSCRIPT_DRAFT` | `subject_id = AiOutput.ai_output_id`, kind TRANSCRIPT_DRAFT; source type `AI_OUTPUT_DELETE`, source ID bằng subject ID | local delete completion; output `content_sha256` captured trước delete | `USER_DELETED` | `TP-SEC:TRANSCRIPT_DRAFT_DELETE` |

For state-event sources, `source_record_type` is exactly `AttachmentStateEvent` or `UploadStateEvent` and `source_record_id` is the included event ID. For AI delete, `source_record_type = AI_OUTPUT_DELETE`; this is a typed deletion operation marker, not a remaining AiOutput row. A null reason is materialized to the exact fallback only in TP-EXP's Tombstone projection; the receipt retains null. `purged_or_deleted_at` in that projection equals receipt `completed_at`. Tombstone ID/shape, archive ordering and receipt validation remain authoritative in `TP-EXP`; no producer may persist a second incompatible tombstone schema.

Khi toàn bộ Workspace đã vào `deleting`, export mới/đang chạy bị chặn hoặc hủy trước content deletion. Account-wide purge được phép xóa receipt cùng ownership tree và không phải tạo per-subject receipt mới cho mọi row sắp bị xóa; receipt contract ở đây phục vụ item/retention deletion còn có thể xuất hiện trong một Workspace export hợp lệ.

### 8.2. Export

- User phải re-authenticate trong 10 phút trước khi request hoặc download export.
- Export snapshot được classify `STANDARD` theo exact `export_sla_envelope_v1` phải đạt READY không muộn hơn `requested_at + 24 hours`. Snapshot vượt bất kỳ inclusive bound nào là `OVERSIZE`: request vẫn được chấp nhận và xử lý lossless, không có v1 24-hour guarantee, đồng thời phải có classification/progress/24-hour notification theo `TP-EXP`. Envelope này không phải storage, episode hoặc pricing quota.
- Archive phải theo exact `tradeproof_export_v1` trong `TP-EXP`, gồm cutoff-consistent canonical JSON, CSV tiện dụng, retained attachment, manifest/checksum và reference-closed public market provenance mà workspace artifact tham chiếu.
- Exact allowlist, immutable/superseded history, `exportAsOfAt`, serialization, entry layout, concurrency failure, corruption validation và round-trip semantics thuộc `TP-EXP`; tài liệu này không định nghĩa archive thay thế.
- Export không gồm internal security log, key, processor credential hoặc dữ liệu user khác.
- Manifest phải có `exportAsOfAt`, `generatedAt` UTC, schema/domain versions, file list, media type, record count, exact byte size và SHA-256 theo `TP-EXP`; manifest không checksum chính nó.
- Download token hết hạn sau 15 phút va chi duoc resolve qua revocation-aware TradeProof download gateway/CDN authorization hook. Moi GET/Range GET phai verify token signature/expiry, recent-auth grant, Workspace van ACTIVE, export grant van ACTIVE, exact current workspace/subject guard generation va exact archive object version. Cam redirect/tra ordinary object-store presigned URL khong the revoke; provider primitive thay the chi hop le neu integration test chung minh exact-version access bi vo hieu ngay khi grant/generation revoke, ke ca object delete outage.
- Export control plane persists immutable `archive_created_at` khi exact object version da upload, checksum/size verify va register; `archive_expires_at = archive_created_at + 24 hours`. READY khong duoc publish truoc hai field. Tai account/item delete, transaction acquiring the same workspace guard lock first revokes grant/increments generation; every later GET is denied immediately even when object-store deletion is delayed.
- Archive expiry is a saga, not a cross-system transaction: revoke download grant and enter EXPIRING under DB lock, enqueue idempotent delete for exact object version, verify absence, then append EXPIRE. Retry/crash never re-enable grant. Object absence is required no later than `archive_expires_at`; overdue bytes page the retention owner while access remains denied. TP-EXP owns exact states/events/outbox/idempotency fixtures.
- Export completion/download phải được audit và tạo first-party authenticated in-app status notification đồng bộ trong exact export/gateway transaction; MVP không gửi email/webhook và không có post-terminal notification worker. Notification control row không chứa archive content/URL và thuộc PRIMARY_TENANT_DATA deletion.

### 8.3. Xóa TradeProof account

UI command `Delete TradeProof account` requires re-authentication within 10 minutes, exact scope disclosure and explicit confirmation. Its durable contract is `workspace_deletion_v1`; deletion cannot be implemented as a best-effort request handler or an untracked cron list.

`WorkspaceDeletion` is Restricted control-plane evidence outside the tenant ownership tree that it deletes:

```text
deletion_id
workspace_id
owner_user_id
contract_version                 workspace_deletion_v1
idempotency_key
request_payload_sha256
identity_mode                    MANAGED_DEDICATED | SHARED_FEDERATED
identity_subject_hmac
identity_hmac_key_version
deleted_identity_generation
previous_identity_deletion_id     nullable
guard_generation
job_drain_watermark
state                            FENCED | LOCAL_PURGED | SECONDARY_PURGED | COMPLETED
target_set_sha256
requested_at
local_due_at
secondary_due_at
external_due_at
completed_at                     nullable
```

All fields except `completed_at` and first-cycle nullable `previous_identity_deletion_id` are non-null after the request transaction. Deadlines are exact: local = requested + 24 hours, secondary = requested + 72 hours, external = requested + 30 days. `identity_subject_hmac = HMAC-SHA-256(restricted versioned key, UTF8(issuer) || 0x00 || UTF8(subject))`; raw issuer/subject/email are forbidden in deletion records. `(workspace_id)` and `(workspace_id,idempotency_key)` are unique. `request_payload_sha256` hashes RFC 8785 `{ "confirmationVersion":"delete_account_v1", "reasonTaxonomyId":null, "workspaceId":id }`. V1 does not collect a deletion reason; the member is mandatory JSON null so no undefined/free-text taxonomy can enter storage. A future reason taxonomy requires a versioned confirmation contract.

State is replayed from append-only `WorkspaceDeletionStateEvent`:

```text
workspace_deletion_state_event_id
deletion_id
event_sequence                    positive integer contiguous from 1
event_type                        REQUEST | FENCE | LOCAL_PURGE_COMPLETE |
                                  SECONDARY_PURGE_COMPLETE | COMPLETE
recorded_at
idempotency_key
```

Every event has a composite FK to its deletion, unique `(deletion_id,event_sequence)` and `(deletion_id,idempotency_key)`; sequence allocation is under the deletion lock, starts 1, is gap-free and `recorded_at` is nondecreasing by sequence. Reusing an idempotency key with different event type/payload fails. REQUEST and FENCE are sequences 1 and 2 in the user-command transaction; externally visible initial state is FENCED. Later transitions are in the listed order and use greatest sequence, never timestamp/opaque-ID sorting. LOCAL and SECONDARY events require the target predicates below. COMPLETE requires every target terminal plus a persisted tombstone and has `completed_at = COMPLETE.recorded_at`. State/event retry is idempotent; no cancel/reactivate transition exists.

The FENCE transaction locks `UserIdentity`, User and Workspace, rechecks recent auth/current ACTIVE generation, captures `job_drain_watermark = greatest value in the contiguous per-workspace work-sequence allocator` across live fences and compacted terminal markers (zero when none), increments `deletion_guard_generation` exactly once, changes Workspace to DELETING, sets deletion fields, revokes every session/export/download grant, writes REQUEST/FENCE, freezes the complete target set and enqueues only revoke/cancel/drain commands. Any failure rolls back all of it. Every GET/Range GET and authenticated request observes ACTIVE + current generation after this commit, so object/provider outage cannot restore access.

Every asynchronous tenant work item that can materialize tenant data, dispatch a tenant-scoped external operation, or commit a domain/control result after enqueue is registered in the non-exported cross-cutting fence contract rather than relying on an optional field in each domain schema. Pure child-first expiration that only removes already-terminal local rows and cannot dispatch externally is outside this registry; it must still use the retention evidence and ordering in section 8.1. No producer may classify creation, external delivery, revoke/delete/verify, or post-provider result handling as pure expiration.

MVP cache population is synchronous inside an authenticated read transaction and keys every entry by Workspace deletion generation; it locks/rechecks ACTIVE before write. MVP has no asynchronous search-index writer: SEARCH_INDEX remains a mandatory empty-store delete/no-op/verify target, and enabling an index requires a new registered work type plus contract amendment. Infrastructure-wide encrypted backup snapshots are not tenant work items and cannot publish into the live namespace; restore is governed by tombstones/restore fences below. Service-owned internal aggregate computation emits no tenant/member key and must use the TP-LAB same-key lock: Workspace FENCE atomically inserts or validates exact `InternalAggregateCohortRetirement` / `internal_aggregate_cohort_retirement_v1` rows for every candidate definition containing that workspace before member removal, and final aggregate publication rejects any retired key in its insert transaction. None of these narrow cases permits an unregistered tenant projection or provider dispatch.

The restricted HMAC key version named by each InternalAggregateCohortRetirement remains available only to deletion/aggregate-integrity roles until every retirement row using it has been removed under TP-LAB retention plus the backup-verification window; rotation never rewrites a retirement row. Missing referenced key material fails deletion/replay verification rather than treating the token as anonymous or disposable.

```text
TenantControlJob
tenant_control_job_id
workspace_id
work_item_type                   same closed enum as TenantWorkItemFence
initiator_kind                   USER | SYSTEM
initiator_user_id                nullable
initiator_system_principal_id    nullable
subject_record_type
subject_record_key_json
subject_record_key_sha256
operation_payload_schema_version tenant_control_job_payload_v1
operation_payload_json
operation_payload_sha256
operation_idempotency_key
created_at

TenantWorkItemFence
tenant_work_item_fence_id
workspace_id
work_sequence                    positive integer contiguous per workspace
work_item_type                   IMPORT | ACCOUNTING_REPLAY | UPLOAD_VALIDATE |
                                 OBJECT_INGEST_FINALIZE | UPLOAD_PURGE |
                                 ATTACHMENT_DELETE | CONTEXT |
                                 COHORT_LOCK | REPORT | PRODUCT_METRIC |
                                 PRODUCT_MEASUREMENT_TIMEOUT |
                                 ANALYTICS_DELIVERY | ANALYTICS_PURGE |
                                 EXPORT | EXPORT_EXPIRY |
                                 AI_RUN | AI_CANCEL | AI_OUTPUT_DELETE
work_item_record_type            TenantControlJob
work_item_record_key_json        { "tenant_control_job_id": id }
work_item_record_key_sha256
captured_guard_generation
created_at

TenantWorkItemFenceEvent
tenant_work_item_fence_event_id
workspace_id
tenant_work_item_fence_id
event_sequence                   positive integer contiguous per work item
event_type                       ENQUEUE | START_EXTERNAL | END_EXTERNAL |
                                 COMPLETE | CANCELLED_DELETION
provider_operation_token_sha256  nullable
safe_result_code                 nullable
recorded_at
idempotency_key

TenantExternalOperationLease
tenant_external_operation_lease_id
workspace_id
tenant_work_item_fence_id
operation_ordinal                positive integer contiguous per fence
provider_registration_id
lookup_hmac_key_version
provider_operation_token_sha256
state                            DISPATCH_RESERVED | DISPATCHED | ENDED
start_event_sequence
end_event_sequence               nullable until ENDED
created_at
ended_at                         nullable until ENDED

TenantWorkItemTerminalMarker
tenant_work_item_terminal_marker_id
workspace_id
work_sequence
work_item_type
captured_guard_generation
terminal_event_type              COMPLETE | CANCELLED_DELETION
terminal_safe_result_code
terminal_at
operation_payload_schema_version tenant_control_job_payload_v1
terminal_marker_digest_profile   tenant_work_item_terminal_marker_v1
semantic_operation_digest_sha256
operation_idempotency_key_hmac
idempotency_hmac_key_version
initiator_digest_sha256
initiator_hmac_key_version
source_fence_digest_sha256
```

Every producer creates exactly one `TenantControlJob`, its domain record where applicable, exactly one same-workspace fence and ENQUEUE sequence 1 atomically. Fence type equals control-job type. Initiator is an exact exclusive union: USER requires non-null `initiator_user_id = Workspace.owner_user_id` and null system principal; SYSTEM requires null user, a non-null approved workload-identity `initiator_system_principal_id` authorized for that work type, and never fabricates a user. The queue message copies exactly this union plus immutable Workspace ID and control-job ID; the worker byte-compares it with the header before resolving the fence. Key/payload hashes are lowercase SHA-256 of RFC 8785 bytes of their adjacent JSON; the fence record key admits exactly the one member above. Header has unique `(workspace_id,work_sequence)`, unique `(workspace_id,work_item_type,work_item_record_key_sha256)`, unique `(workspace_id,operation_payload_schema_version,work_item_type,operation_idempotency_key)` and composite FK `(workspace_id,tenant_control_job_id,work_item_type)`. The subject type/key is creation-time validated against the same-workspace domain row under its lock, but is deliberately not a permanent FK after terminal compaction because retention/item deletion may remove that subject. A semantic operation is also unique on `(workspace_id,operation_payload_schema_version,work_item_type,subject_record_type,subject_record_key_sha256,operation_payload_sha256)`. Retry with the same schema version/idempotency key and byte-identical initiator/type/subject/payload returns its existing control job/fence; any changed byte fails `TENANT_CONTROL_JOB_IDEMPOTENCY_CONFLICT`. A distinct key for an already-existing same-version semantic operation returns that operation rather than allocating a second work sequence. Schema versions are separate namespaces: migration never aliases or silently treats v2 bytes as v1. Scalar/unknown/extra/cross-type keys or payloads fail before enqueue. Events have composite header FK and unique sequence/idempotency keys with nondecreasing times. Other than START_EXTERNAL/END_EXTERNAL, event token hash is null. `safe_result_code` is null before terminal and non-null on the one terminal COMPLETE or CANCELLED_DELETION after all external operations end. No work sequence may be allocated while Workspace is DELETING except deletion-saga work, which is not a TenantWorkItemFence.

Before each tenant-scoped or mutating provider dispatch, one transaction allocates the next operation ordinal, inserts its `TenantExternalOperationLease` in DISPATCH_RESERVED and appends START_EXTERNAL pointing to the lease token hash. The raw lookup/idempotency token is not persisted: it is exactly `"tpw_" + base64url_no_pad(HMAC-SHA256(key[lookup_hmac_key_version], RFC8785({ "operationOrdinal": int, "providerRegistrationId": id, "tenantWorkItemFenceId": id, "workspaceId": id })))`; the event/lease hash is lowercase SHA-256 of its ASCII bytes. An approved provider must accept this token as the operation idempotency key and support status lookup by the same token. Dispatch marks the lease DISPATCHED; a crash while RESERVED performs lookup, dispatches only on definitive NOT_FOUND, and otherwise resumes the found operation. A crash after dispatch performs lookup until the provider is terminal, then one transaction appends matching END_EXTERNAL and marks ENDED. START/END copy the same token hash and exact lease; pairs cannot overlap. HMAC key material is restricted and new operations use only the current key. Ordinarily a referenced version remains derivable until all its leases are ENDED plus the 30-day backup-verification window. If Workspace FENCE freezes its non-secret derivation tuple into an encrypted processor deletion inventory, the version instead remains available until that target's final VERIFY_ABSENCE clears the inventory plus the backup-verification window; raw `tpw_` is still never persisted. Missing key, ambiguous lookup or provider without this API blocks release and drain rather than guessing. The sole v1 exception is the internal global market-data service's tenant-free read-only public Binance GET defined in section 2.2: it has no tenant locator or mutation, creates no TenantExternalOperationLease and can commit only global public cache data; the tenant CONTEXT result path remains fenced.

The registry is closed; `subject_record_type`, exact key and COMPLETE predicate are:

| `work_item_type` | Owning contract; subject type and exact `subject_record_key_json` | COMPLETE predicate and `safe_result_code` | CANCELLED_DELETION effect |
|---|---|---|---|
| IMPORT | TP-ACC; ImportBatch `{ "import_batch_id": id }` | Batch is `COMPLETE | PARTIAL | NEEDS_ATTENTION | REJECTED`; `IMPORT_BATCH_TERMINAL` | Stop parse/accounting commits; retain already committed immutable rows until deletion target removes them |
| ACCOUNTING_REPLAY | TP-ACC; triggering ImportBatch `{ "import_batch_id": id }` | Preview durably created or conflict-free replay atomically published; `REPLAY_PREVIEW_READY | REPLAY_PUBLISHED` | Publish no new projection, eligibility event or pointer |
| UPLOAD_VALIDATE | TP-SEC; Upload `{ "upload_id": id }` | Upload reached ACCEPT or REJECT; CSV ACCEPT additionally has atomic TP-ACC ImportPreview/CREATE bound to this payload, while CSV REJECT has none; `UPLOAD_ACCEPTED | UPLOAD_REJECTED` | Deny bytes and hand the exact lease to forced purge; create no ImportPreview/Attachment/ImportBatch |
| OBJECT_INGEST_FINALIZE | TP-SEC; ObjectIngestReservation `{ "object_ingest_reservation_id": id }` | Aborted path has ABORT_VERIFY, or activated path has post-expiry capability revocation plus clean second inventory; `INGEST_ABORT_ABSENCE_VERIFIED | INGEST_ACTIVATED_CLEAN` | Revoke capability, forbid TRANSFER/target creation and hand every reserved-key version to TEMPORARY_OBJECTS delete/verification |
| UPLOAD_PURGE | TP-SEC; Upload `{ "upload_id": id }` | PURGE plus matching timely UploadObjectAbsenceVerification committed; `UPLOAD_ABSENCE_VERIFIED` | Keep reads denied; workspace TEMPORARY_OBJECTS deletion owns final delete/verification |
| ATTACHMENT_DELETE | TP-SEC; Attachment `{ "attachment_id": id }` | DELETE_COMPLETE plus matching AttachmentObjectAbsenceVerification committed; `ATTACHMENT_ABSENCE_VERIFIED` | Keep binary access denied; workspace TENANT_OBJECTS deletion owns final delete/verification |
| CONTEXT | TP-MCE; TradeEpisodeProjection `{ "episode_id": id, "projection_version": int }` | Its one exact `(phase,timeframe)` slot creates/reuses one immutable snapshot revision selected by the scope resolver, or records one deterministic no-data terminal; `CONTEXT_PUBLISHED | CONTEXT_NO_DATA` | Publish no tenant ContextSnapshot/result and discard fetched data |
| COHORT_LOCK | TP-LAB; WeeklyCohort `{ "weekly_cohort_id": id }` | Required cohort/input state event and input revision atomically committed; `COHORT_INPUT_LOCKED` | Publish no state event, input revision or pointer |
| REPORT | TP-LAB; WeeklyCohortInputRevision `{ "weekly_cohort_input_revision_id": id }` | MetricSnapshots, report revision/events and pointers atomically published, or attempt safely failed; `REPORT_PUBLISHED | REPORT_FAILED` | Publish no MetricSnapshot/report/event/pointer |
| PRODUCT_METRIC | TP-LAB; Workspace `{ "workspace_id": id }` | One scheduled metric window's snapshot set atomically committed or safely failed; `PRODUCT_METRIC_PUBLISHED | PRODUCT_METRIC_FAILED` | Publish no WorkspaceProductMetricSnapshot |
| PRODUCT_MEASUREMENT_TIMEOUT | TP-LAB; ProductMeasurementRun `{ "measurement_run_id": id }` | Exact sequence-2 state event plus its terminal ProductAnalyticsEvent commit; `MEASUREMENT_RUN_SUCCEEDED | MEASUREMENT_RUN_ABANDONED` | Publish no terminal state/event; `WORKSPACE_DELETING`, then PRIMARY_TENANT_DATA removes the bundle |
| ANALYTICS_DELIVERY | TP-LAB; ProductAnalyticsEvent `{ "product_analytics_event_id": id }` | Approved external projection acknowledged or safely suppressed; `ANALYTICS_ACKNOWLEDGED | ANALYTICS_SUPPRESSED` | Send no new projection; late acknowledgement is covered by final EXTERNAL_ANALYTICS delete |
| ANALYTICS_PURGE | TP-LAB; ProductAnalyticsExternalProjection `{ "product_analytics_external_projection_id": id }` | Exact event copy is provider absence-verified or every delivery lookup proves it was never created; `ANALYTICS_COPY_ABSENT | ANALYTICS_COPY_NEVER_CREATED` | Commit no projection/receipt result; hand pinned processor/generation/locator or unresolved delivery token to final EXTERNAL_ANALYTICS delete/verification |
| EXPORT | TP-EXP; ExportJob `{ "export_job_id": id }` | Materialization job reaches READY, FAILED or CANCELLED under TP-EXP; `EXPORT_READY | EXPORT_FAILED | EXPORT_CANCELLED` | Revoke grant, publish no READY, and hand archive/temp objects to deletion targets |
| EXPORT_EXPIRY | TP-EXP; ExportJob `{ "export_job_id": id }` | Its payload-bound registered archive version is absence-verified and all attempt references are DELETED; selected READY version also reached EXPIRED; `EXPORT_ARCHIVE_EXPIRED | EXPORT_ARCHIVE_CLEANED` | Atomically revoke/handoff that exact version, commit no later ExportJob/reference result, and let DOWNLOAD_AND_EXPORT perform final delete/verification |
| AI_RUN | TP-SEC; AiRun `{ "ai_run_id": id }` | AiRun is terminal and its copy is BOUND_OUTPUT or a terminal copy state with valid `ai_processor_copy_terminal_evidence_v1`; `AI_RUN_TERMINAL` | Publish no output; end/lookup provider operation, terminalize or freeze the copy branch, and let final AI_PROCESSOR delete remove possible late materialization |
| AI_CANCEL | TP-SEC; AiRun `{ "ai_run_id": id }` | That exact run is CANCELLED_CONSENT_REVOKED and its cancel lease, if any, is ENDED; `AI_CANCEL_DRAINED` | Stop further cancel calls after the exact run's outstanding operation ends; its AI_RUN/copy branch remains responsible for terminal evidence or deletion handoff |
| AI_OUTPUT_DELETE | TP-SEC; AiOutputSubject `{ "ai_output_subject_id": id }` | Local DELETE/receipt exists and its one processor-copy reference is `DELETION_VERIFIED | NO_COPY_ATTESTED | RETENTION_EXPIRED`; `AI_OUTPUT_DELETED` | Commit no processor result or subject mutation; after outstanding lookup ends, AI_PROCESSOR performs final delete/verification |

The exact `operation_payload_json` member set is also closed:

| `work_item_type` | Exact `tenant_control_job_payload_v1` object |
|---|---|
| IMPORT | `{ "adapterContractVersion": str, "importPreviewRecordKey": { "import_preview_id": id }, "previewSummarySha256": hash, "sourceUploadRecordKey": { "upload_id": id } }` |
| ACCOUNTING_REPLAY | `{ "accountRecordKey": { "trading_account_id": id }, "basedOnActiveProjectionRecordKeys": [{ "episode_id": id, "projection_version": int }...], "instrumentId": id, "replayInputDigestSha256": hash, "sourceFillRecordKeys": [{ "fill_id": id }...] }` |
| UPLOAD_VALIDATE | Tagged union: CSV `{ "adapterContractVersion": str, "leaseGeneration": int, "tradingAccountRecordKey": { "trading_account_id": id }, "uploadKind": "CSV" }`; media `{ "leaseGeneration": int, "uploadKind": "SCREENSHOT"|"VOICE" }` |
| OBJECT_INGEST_FINALIZE | `{ "leaseGeneration": int, "purpose": "RAW_UPLOAD"|"SANITIZED_ATTACHMENT" }` |
| UPLOAD_PURGE | `{ "leaseGeneration": int }` |
| ATTACHMENT_DELETE | `{ "leaseGeneration": int }` |
| CONTEXT | Tagged union: ID branch `{ "algorithmVersion": str, "parameterSetId": id, "phase": "ENTRY"|"EXIT", "recomputeReason": "INITIAL_EVENT"|"SOURCE_GAP_FILLED"|"SOURCE_REVISION_RESOLVED"|"MANUAL_RETRY", "sourceEventSequence": int, "timeframe": "1m"|"5m", "triggerId": id, "triggerType": "EPISODE_EVENT"|"INGESTION_BATCH"|"MARKET_BAR_RESOLUTION"|"MANUAL_REQUEST" }`; digest branch `{ "algorithmVersion": str, "parameterSetId": id, "phase": "ENTRY"|"EXIT", "recomputeReason": "EPISODE_PROJECTION_REPLAYED"|"ALGORITHM_UPGRADE", "sourceEventSequence": int, "timeframe": "1m"|"5m", "triggerSha256": hash, "triggerType": "EPISODE_PROJECTION"|"ALGORITHM_RELEASE" }` |
| COHORT_LOCK | `{ "cohortEndAtUtc": ts, "cohortSequence": int, "cohortStartAtUtc": ts }` |
| REPORT | `{ "logicalKeySha256": hash, "rendererId": str }` |
| PRODUCT_METRIC | `{ "basedOnSnapshotRecordKey": null|{ "workspace_product_metric_snapshot_id": id }, "dimension": exact TP-LAB dimension object, "evaluationAsOfAt": ts, "metricDictionaryVersion": "product_metrics_v1", "metricId": TP-LAB metric enum, "requestedStatus": "PROVISIONAL"|"FINAL", "windowEndAtExclusive": ts, "windowStartAt": ts }` |
| PRODUCT_MEASUREMENT_TIMEOUT | `{ "deadlineAt": ts, "feature": "ONBOARDING"|"QUICK_PLAN"|"QUICK_REVIEW"|"FIRST_INSIGHT", "measurementRunSchemaVersion": "product_measurement_run_v1", "operation": "TERMINALIZE_AT_DEADLINE" }` |
| ANALYTICS_DELIVERY | `{ "externalProjectionSchemaVersion": "product_analytics_external_v1", "processorRegistrationId": id }` |
| ANALYTICS_PURGE | `{ "externalExpiresAt": ts, "externalProjectionSha256": hash, "operation": "DELETE_VERIFY", "processorRegistrationId": id }` |
| EXPORT | `{ "operation": "MATERIALIZE" }` |
| EXPORT_EXPIRY | `{ "archiveObjectVersionSha256": hash, "operation": "REVOKE_DELETE_VERIFY" }` |
| AI_RUN | `{ "operation": "EXECUTE" }` |
| AI_CANCEL | `{ "aiProcessorCopyReferenceId": id, "consentRecordId": id, "consentSequence": int, "feature": "TRANSCRIPTION"|"TAXONOMY_SUGGESTION"|"WEEKLY_SUMMARY" }` |
| AI_OUTPUT_DELETE | `{ "operation": "DELETE_BUNDLE_AND_PROCESSOR" }` |

Arrays use the authoritative owner-contract order and contain no duplicates. UPLOAD_VALIDATE `uploadKind` byte-equals its Upload kind and `leaseGeneration` its exact active UploadObjectLease; only CSV admits the adapter/account members, whose adapter is approved and whose TradingAccount is same-workspace. CSV ACCEPT atomically creates the one TP-ACC ImportPreview from those values; REJECT creates none. IMPORT is created only by TP-ACC ConfirmImport: all three record keys are same-workspace, preview is READY and binds the exact Upload, and adapter/summary hash byte-equal its immutable header; the created ImportBatch copies that binding. For ACCOUNTING_REPLAY, source fills are the complete immutable ledger-order input for the affected account/instrument, active projections sort by RFC 8785 record-key bytes, and `replayInputDigestSha256` hashes the same payload with that member omitted; the triggering ImportBatch and every embedded key are same-workspace. A conflict preview or conflict-free publish must consume byte-identical replay input. OBJECT_INGEST_FINALIZE payload values byte-equal the reservation's immutable purpose/generation and its ENQUEUE is part of RESERVE; every abort delete/inventory and every TRANSFER/post-transfer inventory resolves that fence and rechecks Workspace ACTIVE/current generation before dispatch or commit. CONTEXT identifies one and only one TP-MCE phase/timeframe slot for an exact projection event and trigger. Its branches are closed: INITIAL_EVENT pairs EPISODE_EVENT with `triggerId = ContextEpisodeTrigger.contextEpisodeTriggerId`; SOURCE_GAP_FILLED pairs INGESTION_BATCH with exact `ingestionBatchId`; SOURCE_REVISION_RESOLVED pairs MARKET_BAR_RESOLUTION with exact `marketBarResolutionId`; MANUAL_RETRY pairs MANUAL_REQUEST with `triggerId = ManualContextRecomputeRequest.manualContextRecomputeRequestId`; EPISODE_PROJECTION_REPLAYED pairs EPISODE_PROJECTION with `triggerSha256` equal to lowercase SHA-256 of RFC 8785 new projection record-key bytes; ALGORITHM_UPGRADE pairs ALGORITHM_RELEASE with `triggerSha256 = ContextAlgorithmRelease.releaseSha256`. ContextEpisodeTrigger byte-matches Workspace/projection/phase/authoritative sequence; ManualContextRecomputeRequest byte-matches the complete tenant slot; a global ingestion/resolution record must affect the slot's required source interval/revision; the projection digest matches its exact subject key; and the global registered release matches only algorithm/parameter/digest. Timeframe/algorithm fields absent from ContextEpisodeTrigger are validated from the subject, payload and registered release rather than attributed to that row. `sourceEventSequence` is authoritative allocation 1 for ENTRY or N for EXIT and equals the trigger/request where stored. An ID branch forbids `triggerSha256`; a digest branch forbids `triggerId`; every other reason/type/member coupling rejects. The first four operate in the initial/existing exact scope as TP-MCE defines; replay/upgrade start a new scope and never supersede across scopes. A later trigger therefore creates a new control job, while TP-MCE's final `inputHash` still returns an existing snapshot if inputs are byte-identical. COHORT_LOCK copies `cohortSequence = WeeklyCohort.cohort_sequence`, `cohortStartAtUtc = WeeklyCohort.cohort_start_at_utc` and `cohortEndAtUtc = WeeklyCohort.cohort_end_at_utc` from the locked same-workspace subject; all three values are immutable and byte-equal to that header, so no event/timezone/scheduler sequence is admissible. PRODUCT_METRIC dimension, metric mapping and timestamps obey TP-LAB; null `basedOnSnapshotRecordKey` requires revision 1, non-null must be the current greatest exact tuple and completion must create its next revision with matching `supersedes_snapshot_id`. For an externally eligible analytics event, its immutable `ProductAnalyticsExternalProjection`, ANALYTICS_DELIVERY chain and ANALYTICS_PURGE chain are created in one transaction. The sole no-projection branch is TP-LAB preprojection suppression: ANALYTICS_DELIVERY is created and terminalized atomically as `ANALYTICS_SUPPRESSED`, with no projection, purge chain or external lease, only for the three closed TP-LAB reasons. Delivery resolves the exact event/processor/schema tuple; purge values byte-equal its existing header's source-day expiry, envelope SHA-256 and processor, starts no later than expiry minus 24 hours, and no generic retention cron may replace its provider delete/absence evidence. Every registered ExportAttempt version creates its own EXPORT_EXPIRY semantic operation: the payload uses lowercase SHA-256 of UTF-8 bytes of that exact immutable `archive_object_version`, and job/fence/ENQUEUE plus expiry outbox commit atomically with version registration before EXPORT can terminalize at READY. A restart may therefore leave one cleanup chain plus one selected live chain; MARK_READY requires every nonselected registered chain already `EXPORT_ARCHIVE_CLEANED`. AI_OUTPUT_DELETE is created only in the local bundle-delete transaction described in section 9.4.1. Unknown/missing/extra members, noncanonical timestamp/ID/hash, an output for another payload or any changed payload under retry fail conformance.

PRODUCT_MEASUREMENT_TIMEOUT copies all payload values from one immutable TP-LAB `product_measurement_run_v1` header and uses exact operation idempotency key `measurement-run:<measurement_run_id>:timeout`. StartProductMeasurement atomically creates the header/START plus a USER-initiated control job for `Workspace.owner_user_id`; the delayed approved SYSTEM worker does not rewrite that persisted initiator. This work type creates no TenantExternalOperationLease. Before `deadlineAt`, a guarded success/explicit-abandon transaction may insert the exact terminal ProductAnalyticsEvent/state event plus COMPLETE/marker; at equality or later only TIMEOUT may win. Success/abandon/timeout retries resolve the same fence and terminal marker. FENCE-first creates only CANCELLED_DELETION/`WORKSPACE_DELETING`, publishes no later measurement event, and permits PRIMARY_TENANT_DATA to remove the entire run bundle.

For CONTEXT, global MarketDataIngestionBatch/MarketBarResolution/ContextAlgorithmRelease never acquire tenant ownership through a reference. The enqueue transaction joins them only through the required interval/revision or algorithm tuple specified above, while every tenant result commit remains guarded by the TradeEpisodeProjection/Workspace fence.

The domain subject is immutable input identity, not the job primary key; multiple semantic operations for one subject are distinguished by their immutable payload and receive distinct TenantControlJob IDs/fences. COMPLETE is invalid unless the named domain predicate for that exact payload is visible in the same transaction or through an immutable same-workspace evidence row. CANCELLED_DELETION always uses exact `safe_result_code = WORKSPACE_DELETING`, never rewrites the subject's historical state, and forbids every later domain publish/commit.

The terminal event transaction also creates exactly one marker with the same workspace/sequence/type/generation and terminal values. `operation_payload_schema_version` exactly copies the control job and v1 admits only `tenant_control_job_payload_v1`; `terminal_marker_digest_profile` is exactly `tenant_work_item_terminal_marker_v1`. Let `principalHmac = HMAC-SHA256(key[initiator_hmac_key_version], UTF8(initiator_kind) || 0x00 || UTF8(the one initiator ID))`; `initiator_digest_sha256 = SHA256(RFC8785({ "initiatorKind":str, "principalHmac":hash, "principalHmacKeyVersion":str }))`. That key remains through marker deletion plus backup window. `semantic_operation_digest_sha256 = SHA256(RFC8785({ "initiatorDigestSha256":hash, "operationPayloadSchemaVersion":str, "operationPayloadSha256":hash, "subjectRecordKeySha256":hash, "subjectRecordType":str, "workItemType":str }))`. `operation_idempotency_key_hmac = HMAC-SHA256(key[idempotency_hmac_key_version], UTF8(workspace_id) || 0x00 || UTF8(operation_payload_schema_version) || 0x00 || UTF8(work_item_type) || 0x00 || UTF8(operation_idempotency_key))`; its key remains until marker deletion plus backup window. `source_fence_digest_sha256` is SHA-256 of RFC 8785 `{ "capturedGuardGeneration":int, "eventChain":[{"eventSequence":int,"eventType":str,"providerOperationTokenSha256":hash-or-null,"recordedAt":ts,"safeResultCode":str-or-null}], "initiatorDigestSha256":hash, "operationPayloadSchemaVersion":str, "operationPayloadSha256":hash, "subjectRecordKeySha256":hash, "terminalEventType":str, "terminalMarkerDigestProfile":str, "terminalSafeResultCode":str, "workItemType":str, "workSequence":int }`; eventChain is complete contiguous order. The marker contains no domain/job/subject/provider/initiator ID, raw payload, raw idempotency key or content. A new payload-schema version is a new registered semantic namespace and cannot reuse a v1 literal with changed bytes/rules. A marker-profile bump changes only how terminal evidence is hashed: new rows use the new registered profile, while retry/dedup of an older operation still uses its payload-schema-qualified semantic digest/HMAC and validates `source_fence_digest_sha256` under the marker's persisted profile. Before a new enqueue, uniqueness checks both live detail and markers using the payload-schema-qualified idempotency HMAC, recomputed initiator digest and semantic digest: changed kind/principal conflicts after compaction; matching semantic digest returns the owner domain's persisted effect/deleted result without a new sequence; only a genuinely new semantic operation allocates. A work sequence is drain-terminal only when this marker exists; a terminal event without it is an atomicity failure. After the marker exists and every external lease is ENDED, detailed control job/fence/event/lease rows may be compacted within 30 days or immediately when their domain subject is deleted. Nonterminal work prevents subject deletion unless the deletion command first reaches CANCELLED_DELETION. The marker remains until Workspace deletion drain consumes it; it is non-exported and cannot restore the removed subject.

JOB_CONTROL drain writes exact evidence only after every allocated sequence `<= job_drain_watermark` has exactly one terminal marker; any still-live fence must first finish every external lease and create its marker:

```text
JobDrainEvidence
job_drain_evidence_id
deletion_id
workspace_id
guard_generation
work_sequence_watermark
terminal_work_item_count
terminal_work_item_digest_sha256
drained_at
```

The digest is SHA-256 of RFC 8785 `{ "deletionId":id, "guardGeneration":int, "terminalWorkItems":[{"capturedGuardGeneration":int,"idempotencyHmacKeyVersion":str,"initiatorDigestSha256":hash,"initiatorHmacKeyVersion":str,"operationIdempotencyKeyHmac":hash,"operationPayloadSchemaVersion":str,"semanticOperationDigestSha256":hash,"sourceFenceDigestSha256":hash,"terminalAt":ts,"terminalEventType":str,"terminalMarkerDigestProfile":str,"terminalSafeResultCode":str,"workItemType":str,"workSequence":int}], "workSequenceWatermark":int }`, including every non-ID field from exactly one validated marker for every sequence `1..watermark` in ascending order; zero watermark uses an empty array/count zero. `terminal_work_item_count` equals array length and `terminal_work_item_digest_sha256` stores that digest. Evidence has unique `(deletion_id,work_sequence_watermark)` and exact deletion/workspace/generation FKs. In the same transaction after hashing, the deletion worker may remove those markers; JobDrainEvidence becomes the minimized continuity proof. JOB_CONTROL target succeeds only with this evidence.

Each frozen target is an immutable `WorkspaceDeletionTarget`:

```text
workspace_deletion_target_id
deletion_id
target_kind                       SESSIONS | DOWNLOAD_AND_EXPORT | JOB_CONTROL |
                                  PRIMARY_TENANT_DATA | LOCAL_IDENTITY |
                                  TENANT_OBJECTS | TEMPORARY_OBJECTS | CACHE |
                                  SEARCH_INDEX | INTERNAL_ANALYTICS_MAPPING |
                                  EXTERNAL_ANALYTICS | AI_PROCESSOR |
                                  IDENTITY_PROVIDER | ROLLING_BACKUP |
                                  AUDIT_MINIMIZATION
target_instance_key
depends_on_target_keys_json      sorted exact target-key array
deadline_at
required_action                   REVOKE | CANCEL_AND_DRAIN | DELETE |
                                  UNLINK | VERIFY_EXPIRY | MINIMIZE
pipeline_id                       REVOKE_V1 | CANCEL_DRAIN_V1 |
                                  DELETE_VERIFY_V1 | UNLINK_VERIFY_V1 |
                                  RESTORE_FENCE_EXPIRY_V1 | MINIMIZE_VERIFY_V1
frozen_inventory_schema_version   nullable
frozen_inventory_ciphertext       nullable; cleared only after final verification
frozen_inventory_sha256           nullable
created_at
```

There is exactly one row with `target_instance_key = LOCAL` for every local/fixed kind. There is one EXTERNAL_ANALYTICS or AI_PROCESSOR row per immutable configured processor instance that could hold workspace data; when none is configured, one `target_instance_key = NONE` row still runs DELETE_VERIFY_V1 against the frozen processor registry. Empty stores and processors never short-circuit: a pre-FENCE job may materialize data after the initial observation, so every data-bearing target runs its idempotent delete/no-op and final post-drain verification. IDENTITY_PROVIDER is DELETE for MANAGED_DEDICATED and UNLINK for SHARED_FEDERATED. `deadline_at` equality is exact: SESSIONS and DOWNLOAD_AND_EXPORT use `requested_at`; JOB_CONTROL, PRIMARY_TENANT_DATA, LOCAL_IDENTITY, TENANT_OBJECTS, TEMPORARY_OBJECTS, INTERNAL_ANALYTICS_MAPPING and AUDIT_MINIMIZATION use `local_due_at`; CACHE and SEARCH_INDEX use `secondary_due_at`; EXTERNAL_ANALYTICS, AI_PROCESSOR, IDENTITY_PROVIDER and ROLLING_BACKUP use `external_due_at`.

At FENCE, the three frozen-inventory fields are non-null exactly for EXTERNAL_ANALYTICS, AI_PROCESSOR and IDENTITY_PROVIDER, including processor NONE rows; every other target has all three null. EXTERNAL_ANALYTICS uses the exact TP-LAB `product_analytics_external_deletion_inventory_v1` plaintext/hash. AI_PROCESSOR uses exact `ai_processor_deletion_inventory_v1` below. IDENTITY_PROVIDER uses the exact `identity_provider_deletion_inventory_v1` plaintext below. The complete RFC 8785 plaintext is authenticated-encrypted as one restricted blob under the deletion evidence key; `frozen_inventory_sha256` is lowercase SHA-256 of its plaintext bytes. FENCE creates it before primary/local rows can disappear and before any control cancellation; each inventory includes every recoverable locator or non-secret deterministic external-operation-token derivation descriptor needed to resolve a possible late copy after source/control compaction. Every locator-encryption, inventory-encryption and lookup-HMAC key version referenced by an inventory remains usable until that target's final VERIFY_ABSENCE clears the ciphertext plus the backup-verification window. The ciphertext may be cleared only in that final transaction after its full partition is covered; schema version/hash remain until deletion-control cleanup.

```json
{
  "identityGeneration": 1,
  "identityProviderMode": "MANAGED_DEDICATED",
  "identityProviderRegistrationId": "...",
  "inventorySchemaVersion": "identity_provider_deletion_inventory_v1",
  "issuer": "https://id.example/tenant/",
  "opaqueSubject": "...",
  "workspaceDeletionId": "...",
  "workspaceGrantHandle": null,
  "workspaceId": "..."
}
```

This identity inventory has that exact member set. `identityGeneration`, `issuer` and `opaqueSubject` byte-equal the locked UserIdentity. Registration ID identifies the immutable approved provider configuration generation used for that issuer. MANAGED_DEDICATED requires null `workspaceGrantHandle`; its DELETE operates on the exact provider subject. SHARED_FEDERATED requires a nonempty opaque provider grant/link handle; its UNLINK operates only on that Workspace grant and never deletes the shared provider identity. The issuer, subject and grant handle exist only inside the outer authenticated-encrypted inventory, are never copied to an attempt, outbox payload, log, audit event, tombstone or receipt, and are decrypted only inside the identity-provider gateway. Final evidence is lowercase SHA-256 of exact RFC 8785 `{ "identityProviderRegistrationId":id, "inventorySha256":hash, "resultCode":"SUBJECT_ABSENT"|"WORKSPACE_LINK_ABSENT", "safeProviderReceiptSha256":hash, "workspaceDeletionId":id }`; it contains no raw locator. Missing registration/key/locator, ambiguous lookup or mode/result mismatch blocks verification.

`depends_on_target_keys_json` is an RFC 8785 array of exact objects `{ "targetInstanceKey":str, "targetKind":str }`, unique and sorted by `(targetKind,targetInstanceKey)` Unicode code points. It is empty for every target except PRIMARY_TENANT_DATA, whose array is exactly LOCAL TENANT_OBJECTS, LOCAL TEMPORARY_OBJECTS and LOCAL INTERNAL_ANALYTICS_MAPPING. The graph is frozen, acyclic and validated before FENCE commit. This prevents primary table deletion from erasing Upload/Attachment/export/staging locators or member mappings before the owning target has verified them. IDENTITY_PROVIDER needs no dependency on LOCAL_IDENTITY because its complete executable locator is frozen above before local purge. `target_set_sha256` hashes the RFC 8785 sorted array `{deadlineAt,dependsOnTargetKeys,frozenInventorySchemaVersion,frozenInventorySha256,pipelineId,requiredAction,targetInstanceKey,targetKind}`, using JSON null only for `frozenInventorySchemaVersion` and `frozenInventorySha256` on targets without inventory; all other hash members remain their required non-null values. Items sort by `(targetKind,targetInstanceKey)` Unicode code points. New processors discovered after FENCE cannot be silently added; target-set omission is an incident and blocks COMPLETE until a versioned remediation target set is recorded by a new contract.

Execution/evidence records are durable:

```text
WorkspaceDeletionTargetAttempt
workspace_deletion_target_attempt_id
deletion_id
workspace_deletion_target_id
attempt_no                       positive contiguous per target
action_ordinal                   positive integer from the frozen pipeline
action                           REVOKE | CANCEL | DRAIN | DELETE |
                                  VERIFY_ABSENCE | UNLINK |
                                  INSTALL_RESTORE_FENCE | VERIFY_EXPIRY | MINIMIZE |
                                  VERIFY_MINIMIZATION
status                           STARTED | SUCCEEDED | RETRYABLE_FAILURE
started_at
completed_at                     nullable only for STARTED
safe_result_code                 nullable
provider_receipt_sha256          nullable
verified_at                      nullable
job_drain_evidence_id            nullable
idempotency_key

WorkspaceDeletionOutbox
workspace_deletion_outbox_id
deletion_id
workspace_deletion_target_id
command_sequence                 positive contiguous per target
action_ordinal                   equals command_sequence
command_kind                     same closed action enum
payload_sha256
depends_on_job_drain_evidence_id nullable
idempotency_key
enqueued_at
delivered_at                     nullable
```

Attempts/outbox rows have composite deletion/target FKs and unique `(target,attempt_no)`, `(target,command_sequence)` and idempotency keys. FENCE inserts only the synchronous SESSIONS/DOWNLOAD REVOKE and JOB_CONTROL CANCEL/DRAIN commands; it freezes but does not yet insert any data-bearing pipeline outbox row. The transaction that creates JobDrainEvidence then inserts exactly one outbox row for every ordinal of every data-bearing target with `depends_on_job_drain_evidence_id` already non-null and equal to that evidence; this FK is immutable and no delivery can race ahead of it. A target's first ordinal additionally cannot START until the final ordinal of every key in its frozen `depends_on_target_keys_json` has a greatest SUCCEEDED attempt; later ordinal delivery waits for the prior ordinal of the same target. Delivery retry reuses the row. Payload is exact IDs, generation and provider-safe operation token only; identity raw locators are read only by decrypting their frozen inventory inside the gateway, and no content/email/object key enters the outbox. Attempts for an ordinal have increasing global `attempt_no`; action ordinals never decrease, and ordinal N+1 cannot START until ordinal N has a greatest terminal SUCCEEDED attempt. STARTED can be superseded only by a new immutable attempt for the same ordinal; crashes leave work retryable. RETRYABLE_FAILURE never satisfies an ordinal. Delivery may repeat; provider idempotency token is `(deletion_id,target_id,action_ordinal)` and every consumer rechecks generation and target dependencies.

The frozen pipeline registry is normative:

| Target kind / discriminator | `required_action`; pipeline ID and exact action ordinals | Terminal evidence |
|---|---|---|
| SESSIONS | REVOKE; REVOKE_V1 `1 REVOKE` | REVOKE SUCCEEDED in FENCE transaction; revocation-epoch receipt hash and `verified_at = requested_at` |
| DOWNLOAD_AND_EXPORT | REVOKE; REVOKE_V1 `1 REVOKE` | REVOKE SUCCEEDED in FENCE transaction; every grant denied, receipt hash and `verified_at = requested_at` |
| JOB_CONTROL | CANCEL_AND_DRAIN; CANCEL_DRAIN_V1 `1 CANCEL`, `2 DRAIN` | DRAIN SUCCEEDED with exact JobDrainEvidence ID; provider receipt/verified time null |
| PRIMARY_TENANT_DATA, LOCAL_IDENTITY | DELETE; DELETE_VERIFY_V1 `1 DELETE`, `2 VERIFY_ABSENCE` | VERIFY_ABSENCE SUCCEEDED with post-drain absence receipt hash and non-null verified time |
| TENANT_OBJECTS, TEMPORARY_OBJECTS, CACHE, SEARCH_INDEX, INTERNAL_ANALYTICS_MAPPING | DELETE; DELETE_VERIFY_V1 `1 DELETE`, `2 VERIFY_ABSENCE` | VERIFY_ABSENCE SUCCEEDED after enumerating the empty or deleted store |
| EXTERNAL_ANALYTICS | DELETE; DELETE_VERIFY_V1 `1 DELETE`, `2 VERIFY_ABSENCE` | Per TP-LAB frozen inventory, one unlinkable request per pseudonym generation; every projection/token/prior receipt is covered once and the exact `product_analytics_external_deletion_inventory_v1` evidence digest verifies; NONE proves frozen/current registry empty |
| AI_PROCESSOR | DELETE; DELETE_VERIFY_V1 `1 DELETE`, `2 VERIFY_ABSENCE` | Exact `ai_processor_deletion_inventory_v1` result partitions every copy as ABSENT, NEVER_DISPATCHED or PRIOR_TERMINAL_EVIDENCE; inventory ciphertext cleared; NONE proves frozen/current registry empty |
| IDENTITY_PROVIDER / MANAGED_DEDICATED | DELETE; DELETE_VERIFY_V1 `1 DELETE`, `2 VERIFY_ABSENCE` | Decrypt exact frozen registration/issuer/subject only in gateway; provider subject DELETE then `SUBJECT_ABSENT` evidence above |
| IDENTITY_PROVIDER / SHARED_FEDERATED | UNLINK; UNLINK_VERIFY_V1 `1 UNLINK`, `2 VERIFY_ABSENCE` | Decrypt exact frozen registration/grant locator only in gateway; link removed without deleting shared identity, then `WORKSPACE_LINK_ABSENT` evidence above |
| ROLLING_BACKUP | VERIFY_EXPIRY; RESTORE_FENCE_EXPIRY_V1 `1 INSTALL_RESTORE_FENCE`, `2 VERIFY_EXPIRY` | VERIFY_EXPIRY SUCCEEDED only after every containing generation expires; an initially empty inventory still waits for post-drain verification |
| AUDIT_MINIMIZATION | MINIMIZE; MINIMIZE_VERIFY_V1 `1 MINIMIZE`, `2 VERIFY_MINIMIZATION` | VERIFY_MINIMIZATION SUCCEEDED with schema scan/count digest proving only allowed pseudonymous fields remain |

A target is terminal only when its exact final ordinal has a greatest completed SUCCEEDED attempt with the evidence in the matrix. Success on DELETE, UNLINK, INSTALL_RESTORE_FENCE or MINIMIZE alone is never terminal. NOT_APPLICABLE is not a v1 attempt status. Extra, skipped, duplicated or out-of-order action ordinals or a wrong action for an ordinal fail conformance.

For JOB_CONTROL DRAIN, `job_drain_evidence_id` is its exact evidence and the outbox dependency is null. For every ordinal on PRIMARY_TENANT_DATA, LOCAL_IDENTITY, TENANT_OBJECTS, TEMPORARY_OBJECTS, CACHE, SEARCH_INDEX, INTERNAL_ANALYTICS_MAPPING, EXTERNAL_ANALYTICS, AI_PROCESSOR, IDENTITY_PROVIDER, ROLLING_BACKUP or AUDIT_MINIMIZATION, the outbox dependency and every STARTED/terminal attempt's `job_drain_evidence_id` are non-null and equal the same JobDrainEvidence; attempt `started_at >= drained_at`, and consumer rejects delivery before evidence exists. Pre-drain there is no such outbox/attempt row, not a nullable placeholder. Revoke/CANCEL requests may run earlier but cannot mark a data-bearing target terminal. After drain, each matrix runs through its final verification, so a provider write from a pre-FENCE in-flight operation cannot happen after terminal proof. Other attempts require null drain evidence.

Exact stage predicates and evidence are:

| Stage | Required terminal targets and deadline |
|---|---|
| FENCE commit | SESSIONS, DOWNLOAD_AND_EXPORT REVOKE and JOB_CONTROL CANCEL are synchronously effective; attempts/outbox capture evidence at `requested_at` |
| LOCAL_PURGED | JOB_CONTROL DRAIN plus PRIMARY_TENANT_DATA, LOCAL_IDENTITY, TENANT_OBJECTS, TEMPORARY_OBJECTS, INTERNAL_ANALYTICS_MAPPING and AUDIT_MINIMIZATION SUCCEEDED by `local_due_at`; provider deletion requests for every external target have been delivered |
| SECONDARY_PURGED | LOCAL_PURGED plus CACHE and SEARCH_INDEX absence verified by `secondary_due_at` |
| COMPLETED | SECONDARY_PURGED plus every EXTERNAL_ANALYTICS, AI_PROCESSOR and IDENTITY_PROVIDER target terminal, and ROLLING_BACKUP verified expired, all by `external_due_at`; tombstone installed |

PRIMARY_TENANT_DATA means every tenant table including accounting, context, Weekly Lab, analytics events/snapshots, AI local bundle/consent, receipts and job headers is absent except deletion evidence/minimized security audit allowed here; its first action waits the three frozen dependencies, so it cannot erase their locators or mappings early. LOCAL_IDENTITY means UserIdentity, User, WorkspaceOwnerProfile/revisions and Workspace header are absent after their required hashes/IDs and the encrypted identity-provider inventory are frozen. TENANT_OBJECTS includes retained attachments and export archives; TEMPORARY_OBJECTS includes every upload/quarantine/staging object and replica. INTERNAL_ANALYTICS_MAPPING removes restricted direct cohort-member rows only after every matching key has an exact TP-LAB retirement row; the non-identifying retirement remains with its definition to block late publish. AUDIT_MINIMIZATION rewrites retained 365-day security audit to pseudonymous deletion/workspace IDs with no content/email. ROLLING_BACKUP succeeds only after every backup generation that could contain the workspace has expired and inventory verifies this; installing the restore fence is necessary immediately but does not alone mark backup success.

Every asynchronous tenant work item admitted by the closed registry in section 8.3 is inserted atomically with exactly one `TenantWorkItemFence` whose registered record key points back to its `TenantControlJob`; the fence header, not each domain schema, stores `captured_guard_generation`. A registered item without exactly one same-workspace fence cannot enqueue/run, and an unregistered asynchronous producer may not materialize tenant data, dispatch a tenant-scoped or mutating external operation or commit a later tenant/control result. Immediately before any such external request and immediately before any database/object tenant-result commit, the worker resolves its fence, locks Workspace and requires ACTIVE plus equal generation; otherwise it appends `CANCELLED_DELETION` to the fence and cannot publish. The tenant-free internal global public market-data GET exception in section 2.2 may commit only global cache/provenance; it does not waive the fenced tenant CONTEXT commit. A tenant call already in flight when FENCE commits may return, but its result is discarded, END_EXTERNAL is recorded and its provider instance is covered by the frozen target/final post-drain delete. Only the deletion worker may operate while DELETING, and it must present exact `(deletion_id,guard_generation)`. This is a database transaction/worker invariant, not a UI check.

At FENCE, the system also persists a restricted non-exported `WorkspaceDeletionTombstone` before deleting business data:

```text
deletion_id
workspace_id
guard_generation
identity_subject_hmac
identity_hmac_key_version
deleted_identity_generation
previous_identity_deletion_id     nullable
requested_at
local_purged_at                 nullable
completed_at                    nullable
expires_at                      nullable
```

It contains no email, issuer, subject, content or provider token and is unique by both deletion ID and workspace ID. Its non-null `previous_identity_deletion_id` is a self-FK to the prior WorkspaceDeletionTombstone's `deletion_id`, not a retention dependency on the purgeable WorkspaceDeletion header. At FENCE, `local_purged_at`, `completed_at` and `expires_at` are null. LOCAL_PURGE_COMPLETE atomically sets `local_purged_at = event.recorded_at`; COMPLETE atomically sets `completed_at = event.recorded_at` and `expires_at = completed_at + 365 days`. No other null/timestamp combination is valid and the tombstone cannot become purge-eligible before completion. `expires_at` is the earliest retention time, not permission to break a live generation chain: physical purge is allowed only when `now >= expires_at`, no incomplete later tombstone points directly or transitively to it, and no active UserIdentity for the same HMAC-resolved provider subject has `identity_generation > deleted_identity_generation`. Thus the minimal HMAC tombstone chain is retained for the lifetime of any active successor and may be removed after that successor is locally purged and every remaining link has independently reached its expiry. Restore tooling must load retained tombstones first, suppress every row/object/job for a matching workspace/generation, and refuse traffic until reconciliation passes. A restore from any rolling backup therefore cannot resurrect the workspace.

Raw local identity is deleted at LOCAL_PURGED. Re-registration for the same provider subject is rejected while deletion is not COMPLETED. After COMPLETED it is allowed only through a fresh authentication ceremony/nonce issued strictly after `completed_at`; the new UserIdentity gets the resolver value below plus one, and new User/Workspace/TradingAccount IDs. The completed HMAC tombstone remains solely to reject callbacks from an older generation; no old data, profile, aggregate mapping or opaque business ID is reattached. Managed identity deletion and shared-provider unlink/revoke must have terminal IDENTITY_PROVIDER evidence before this point.

Identity-deletion lookup is exact across HMAC rotation and repeated account cycles. For callback `(issuer,subject)`, under the current-subject serialization lock, first resolve an existing active UserIdentity by exact `(issuer,subject)`. If present, its persisted `identity_generation` is authoritative and the retained predecessor chain, when generation is greater than 1, must end at generation minus 1. If absent, compute the subject HMAC with every key version referenced by a retained not-yet-purge-eligible or incomplete tombstone, query exact `(key_version,hmac)` matches, and choose the unique greatest `deleted_identity_generation`; zero matches means generation 0. A tie, missing referenced key, broken sequence or inconsistent previous pointer fails `IDENTITY_DELETION_INDEX_INVALID`. First retained-cycle deletion has generation 1 and null previous pointer. Every later deletion while a prior chain exists requires `previous_identity_deletion_id` equal the selected prior tombstone and `deleted_identity_generation = prior + 1`; deletion of an active identity always copies its authoritative generation and cannot reset it when an older tombstone crosses `expires_at`. The same values appear in WorkspaceDeletion and its Tombstone. Database enforces unique `(identity_hmac_key_version,identity_subject_hmac,deleted_identity_generation)` and unique non-null previous pointer. HMAC key material remains restricted and available until every tombstone referencing that version is physically purged plus the backup-verification window; rotation never rewrites an existing HMAC. A new registration uses `identity_generation = greatest retained deleted generation + 1`; after the entire chain was lawfully purged and no active identity exists, a later fresh ceremony may begin a new generation-1 retention epoch because all old callbacks, backups and restore fences are already outside their enforced windows.

Retry with the same deletion idempotency key/request hash returns the same saga; a changed request conflicts. SLA breach raises an alert and keeps state/revocation/fences active; it never fabricates COMPLETE. After exact COMPLETE, a fresh managed-auth ceremony may query a content-free deletion status backed by the retained identity-generation tombstone before any new bootstrap; v1 does not queue an outbound email/webhook after deletion. V1 has no legal-hold exception or hold record: no target, deadline or terminal predicate may be bypassed or paused. A deployment subject to a legal-hold duty must remain disabled until a versioned contract amendment defines authorization, scope, release, notification and SLA semantics.

Global Binance public MarketBar/cache/source provenance không phải user content và không bị xóa theo một Workspace. Deletion phải xóa mọi tenant ContextSnapshot, job và mapping tới global records; current selection is recomputed by the TP-MCE resolver, not stored in a mutable pointer. Global record chỉ được giữ theo market-data Terms/retention policy và khi còn reference hợp lệ. Export chỉ sao chép reference-closed public subset của Workspace.

Ngoại lệ retention vì nghĩa vụ bảo mật cụ thể không được thay đổi account-deletion target, deadline hoặc terminal predicate của v1; mọi phạm vi khác phải có owner, lý do, thời hạn và access restriction. Legal hold không được hỗ trợ trong v1 theo contract phía trên.

### 8.4. Processor contract

Trước khi gửi production user data, mỗi processor phải có:

- data processing agreement phù hợp;
- danh sách location/subprocessor được công bố;
- encryption in transit và at rest;
- cam kết không dùng user data để train hoặc cải thiện model chung;
- retention không quá 30 ngày cho AI request content;
- deletion process đáp ứng SLA ở trên;
- incident notification và hỗ trợ điều tra;
- cơ chế export/deletion hoặc xác nhận dữ liệu chỉ được xử lý transient.

Thay processor hoặc thay materially data use phải cập nhật disclosure và consent khi cần trước khi chuyển dữ liệu.

## 9. Hợp đồng AI

### 9.1. Opt-in và quyền kiểm soát

- AI mặc định tắt cho Workspace mới.
- Consent phải tách theo feature: transcription, taxonomy suggestion và weekly summary.
- Exact consent contract là `ai_consent_v1`. `ConsentRecord` là append-only event:

```text
consent_record_id
workspace_id
actor_user_id
consent_contract_version      ai_consent_v1
feature                       TRANSCRIPTION | TAXONOMY_SUGGESTION | WEEKLY_SUMMARY
event_sequence                positive integer
decision                      GRANT | REVOKE
disclosure_version
policy_version
disclosure_sha256
recorded_at
idempotency_key
```

Mọi field non-null. Direct immutable `workspace_id` và composite ownership bắt buộc; `(workspace_id, idempotency_key)` và `(workspace_id, feature, event_sequence)` unique. Sequence bắt đầu 1, contiguous, được allocate cùng transaction dưới lock `(workspace_id, feature)`; `recorded_at` phải nondecreasing theo sequence. Current state mỗi feature là decision ở greatest visible `event_sequence`, không sort theo timestamp/opaque ID; không có record tương đương REVOKED. GRANT chỉ hợp lệ sau khi UI hiển thị exact disclosure/policy/hash; thay material data use yêu cầu version mới và GRANT mới. Retry cùng key/payload trả cùng record/sequence, cùng key payload khác fail `CONSENT_IDEMPOTENCY_CONFLICT`.
- Bật một feature không tự bật feature khác.
- User có thể opt out bất kỳ lúc nào. Opt-out phải chặn request mới và hủy queued request trong tối đa 15 phút.
- User phải có thể xóa AI output cùng mọi AI-specific processor/request copy, retained raw audio hoặc transcript draft độc lập với xóa Workspace. Canonical plan, fill, Review, MetricSnapshot và deterministic WeeklyReport không phải “AI source copy” và vẫn theo retention contract riêng; xóa AI summary chỉ gỡ output/provenance link, không mutate các canonical artifact đó.
- Import, plan, review, deterministic metrics, export và deletion phải hoạt động khi AI tắt hoặc processor lỗi.

AI gateway phải đọc current ConsentRecord trong transaction enqueue và ghi exact `consent_record_id` trên AiRun. Worker kiểm tra lại trước outbound request; REVOKE mới hơn làm run `CANCELLED_CONSENT_REVOKED`, không gọi processor. REVOKE holds the `(workspace_id,feature)` lock, appends its event, locks and freezes exactly every current QUEUED/RUNNING AiRun for that feature whose GRANT sequence is earlier, sorted by AiRun ID, then atomically creates one AI_CANCEL TenantControlJob/fence/ENQUEUE per frozen run and its unique copy reference. The payload copies revoke ConsentRecord ID/sequence, feature and copy-reference ID; every field must match that run/reference, so every AI_CANCEL lease maps to exactly one copy. Retry the revoke returns the same event and exact job set; a later GRANT/run is excluded and cannot expand it. The command blocks enqueue mới immediately; each frozen queued run must terminal within 15 minutes, and an in-flight cancel ends/looks up its own provider operation before the AI_CANCEL marker. The owning AI_RUN stays nonterminal until its no-output copy gets terminal evidence or Workspace-deletion handoff. Metrics/audit contain no content. Export gồm toàn bộ ConsentRecord history và as-of current pointer theo `TP-EXP`.

### 9.2. Data minimization theo feature

| Feature | Dữ liệu được gửi | Dữ liệu không được gửi |
|---|---|---|
| Transcription | Audio được chọn, language hint, request ID ngẫu nhiên | Email, WorkspaceId, trades, screenshot, full profile |
| Taxonomy suggestion | Đoạn text user chọn và taxonomy allowlist hiện tại | Raw CSV, attachment, toàn bộ workspace, metrics không cần thiết |
| Weekly summary | Deterministic metric payload, sample size, quality flags, opaque trade references | Raw CSV, audio, screenshot, auth identity, credential |

Screenshot không được gửi cho AI trong MVP. AI processor không được có API secret, session token, signed object URL hoặc quyền truy cập database.

### 9.3. No-training và processor isolation

- Chỉ dùng processor có cam kết hợp đồng không train hoặc cải thiện model từ input/output của người dùng.
- Human review của processor phải tắt, trừ xử lý abuse bắt buộc đã được disclosure.
- Ưu tiên zero-retention endpoint; nếu không có, retention tối đa là 30 ngày.
- Mỗi request chỉ chứa dữ liệu tối thiểu của một Workspace.
- Không dùng production content để fine-tune, evaluate thủ công hoặc tạo synthetic dataset nếu chưa có consent riêng.

### 9.4. Canonical AI artifact, version và provenance

Exact canonical persistence contract là `ai_artifact_v1` và thuộc authority của `TP-SEC`. `TP-EXP` chỉ package/export projection; không sở hữu hoặc tự mở rộng storage schema này. Mọi typed key bên dưới là ordinary canonical JSON object, không phải escaped JSON string, và phải bằng exact `recordKey` của record envelope tương ứng trong `TP-EXP`.

AI configuration khong duoc chi la mutable file/deploy variable. Internal restricted registry luu immutable `AiConfigurationArtifact`:

```text
feature                  TRANSCRIPTION | TAXONOMY_SUGGESTION | WEEKLY_SUMMARY
artifact_kind            PROVIDER_CONFIGURATION | PROMPT_TEMPLATE | POLICY | INPUT_SCHEMA |
                         OUTPUT_SCHEMA | OUTPUT_VALIDATOR | OUTPUT_RENDERER
version_identifier
content_media_type       TEXT_UTF8 | APPLICATION_JSON | BINARY
content_sha256
storage_object_version
created_at
```

Unique `(feature, artifact_kind, version_identifier)`. Bytes tai `storage_object_version` phai hash dung `content_sha256`, bat bien va doc duoc boi restricted audit/eval role. Registry cam overwrite, delete khi con run/reference, va cam reuse version voi bytes/hash khac. Artifact khong chua production user content hoac credential; provider config chi chua safe generation settings/model route va opaque secret-version label, khong chua secret value.

`AiConfigurationRelease` la immutable tuple gom `feature`, `configuration_release_version`, `model_provider`, `model_version_identifier`, exact version + SHA-256 cua ca bay artifact kind, `configuration_release_sha256` va `created_at`. Release hash la SHA-256 cua RFC 8785 object chua feature/model va bay `{artifactKind,versionIdentifier,contentSha256}` entries sorted theo artifact_kind. Unique `(feature, configuration_release_version)`; version/hash khong duoc reuse.

`AiEvalArtifact` luu `eval_artifact_id`, feature, exact release version/hash, `eval_corpus_version`, `eval_result_sha256`, `critical_violation_count`, `numeric_claim_total`, `numeric_claim_grounded_count`, `status = PASS | FAIL`, `completed_at` va approver actor ID; row immutable va khong chua corpus production. `AiConfigurationActivationEvent` luu feature, contiguous positive `event_sequence`, `event_type = ACTIVATE | DEACTIVATE`, release version/hash nullable only cho DEACTIVATE, passing eval artifact ID/hash nullable only cho DEACTIVATE, trusted `recorded_at`, actor va reason. `(feature,event_sequence)` unique; next sequence duoc allocate/commit trong cung feature lock. ACTIVATE chi chap nhan PASS voi zero critical violation va full numeric grounding cho exact release. Current enabled release la latest sequence, khong sort theo timestamp/opaque ID.

AI provider routing is also immutable control data, not a mutable environment label:

```text
AiProcessorRegistration
processor_registration_id
feature                         TRANSCRIPTION | TAXONOMY_SUGGESTION | WEEKLY_SUMMARY
model_provider
provider_configuration_generation
provider_route_identifier
capability_profile_version
capability_profile_sha256
data_use_policy_version
data_use_policy_sha256
retention_policy_version
retention_policy_sha256
retention_mode                   ZERO_RETENTION | PROCESSOR_MAX_30_DAY
copy_locator_api_version
copy_locator_api_sha256
created_at

AiProcessorRegistrationStateEvent
processor_registration_state_event_id
processor_registration_id
registry_event_sequence          positive integer contiguous globally
event_type                       ENABLE | RETIRE | RETENTION_CLOSED
recorded_at
actor_system_principal_id
reason_code
retention_closure_evidence_id     nullable; non-null only for RETENTION_CLOSED
retention_closure_evidence_sha256 nullable; non-null only for RETENTION_CLOSED

AiProcessorRetentionClosureEvidence
processor_retention_closure_evidence_id
processor_registration_id
retire_registry_event_sequence
copy_sequence_watermark
terminal_marker_count
terminal_marker_set_sha256
last_copy_terminal_at             nullable iff watermark = 0
provider_backup_window_ends_at
provider_closure_receipt_sha256
verified_at
evidence_schema_version           ai_processor_retention_closure_evidence_v1
evidence_sha256
```

Registration rows, state events and closure evidence are append-only; every version/hash is nonempty, immutable and resolves audited bytes. Registration ID/configuration generation cannot be reused. ENABLE requires the capability profile to prove request-copy locator create/delete/status idempotency, the pinned no-training/data-use contract and the exact retention mode. RETIRE prevents new runs under the same registration lock that allocates its copy sequence. For ENABLE/RETIRE both closure fields are null. RETENTION_CLOSED requires both closure fields, byte-equal evidence, and is legal only after RETIRE, no retained AiProcessorCopyReference or live lease references the registration, the marker coverage below proves every allocated copy terminal, provider absence includes its last possible live/backup copy, and the backup-verification window has elapsed. State is the greatest contiguous global registry sequence for that registration; same-time events are never timestamp-sorted.

Every registration has a durable monotonic `registration_copy_sequence` allocator starting at 1. AiRun enqueue locks the registration state, requires latest state ENABLE, allocates exactly one next sequence without gaps and copies it to the new AiProcessorCopyReference in the same transaction. Each terminal-copy transaction also inserts one append-only global `AiProcessorRegistrationUsageMarker` with exact fields `processor_registration_id`, `registration_copy_sequence`, `result_code`, `terminal_at`, `terminal_evidence_sha256`; `(processor_registration_id,registration_copy_sequence)` is unique. It contains no Workspace/User/domain/copy-reference ID, handle or provider locator. The result/time/evidence hash byte-equal the same transaction's `AiProcessorCopyTerminalEvidence`. Usage markers remain until closure evidence is committed; tenant-owned reference/evidence rows may therefore be removed on their normal deletion schedule without destroying global coverage.

At RETENTION_CLOSED, `copy_sequence_watermark` is the registration allocator value under the same registry lock; `terminal_marker_count` must equal it and markers must cover every integer `1..watermark` exactly once. `terminal_marker_set_sha256 = SHA-256(RFC8785({ "copySequenceWatermark":int, "markers":[{"registrationCopySequence":int,"resultCode":str,"terminalAt":canonical-rfc3339-ms,"terminalEvidenceSha256":hash}...], "processorRegistrationId":id }))`, sorted by sequence; an empty registration hashes an empty array and has null `last_copy_terminal_at`. Otherwise `last_copy_terminal_at` is the maximum marker timestamp. `provider_backup_window_ends_at` is the maximum of RETIRE time and every marker terminal time plus the immutable backup-verification duration resolved by the registration's retention-policy bytes. `verified_at >= provider_backup_window_ends_at`. `provider_closure_receipt_sha256` hashes exact provider-authenticated receipt bytes proving that the pinned provider route/configuration has no live or backup copy for any sequence through the watermark; no URL, handle or tenant ID is persisted.

`evidence_sha256` is lowercase SHA-256 of RFC 8785 exact object `{ "copySequenceWatermark":int, "evidenceSchemaVersion":"ai_processor_retention_closure_evidence_v1", "lastCopyTerminalAt":timestamp-or-null, "processorRegistrationId":id, "providerBackupWindowEndsAt":timestamp, "providerClosureReceiptSha256":hash, "retireRegistryEventSequence":int, "terminalMarkerCount":int, "terminalMarkerSetSha256":hash, "verifiedAt":timestamp }`. The evidence has a unique FK to the exact RETIRE event/registration; the RETENTION_CLOSED event and evidence commit atomically and copy the ID/hash. Unknown/missing members, a gap, changed marker, wrong policy duration, premature timestamp, reference/lease that still exists or provider receipt for another generation rejects. Usage markers may be deleted only after this evidence commits; the evidence/state registry remains for the registry audit lifetime.

The configured processor registry snapshot at Workspace FENCE is exact RFC 8785 `{ "asOfRegistryEventSequence":int, "registrations":[{"capabilityProfileSha256":hash,"capabilityProfileVersion":str,"copyLocatorApiSha256":hash,"copyLocatorApiVersion":str,"dataUsePolicySha256":hash,"dataUsePolicyVersion":str,"feature":str,"latestEventSequence":int,"latestState":"ENABLE"|"RETIRE"|"RETENTION_CLOSED","modelProvider":str,"processorRegistrationId":id,"providerConfigurationGeneration":str,"providerRouteIdentifier":str,"referencedAfterRetentionClosed":bool,"retentionMode":str,"retentionPolicySha256":hash,"retentionPolicyVersion":str}] }`. The array contains every registration whose latest state at the captured sequence is ENABLE or RETIRE, plus every registration referenced by a retained copy or live/compacted AI lease. A referenced RETENTION_CLOSED registration remains serializable, sets `referencedAfterRetentionClosed = true`, creates its ordinary deletion target and raises an incident; all other entries set false. RETENTION_CLOSED registrations with no reference are excluded. It is sorted by `processorRegistrationId` exact UTF-8 bytes and hashed as lowercase SHA-256 of its RFC 8785 bytes. FENCE creates one AI_PROCESSOR target per array item and copies this same `processorRegistrySha256` into every inventory; an empty array creates the one NONE target. Final NONE or per-registration verification recomputes the append-only snapshot at the frozen sequence and rejects a missing/changed item or any pre-FENCE workspace reference outside the frozen partitions. Later registry events cannot alter that historical snapshot, and post-FENCE runs for the deleting Workspace are forbidden.

`AiRun`:

```text
ai_run_id
workspace_id
ai_artifact_contract_version  ai_artifact_v1
consent_record_id
feature                       TRANSCRIPTION | TAXONOMY_SUGGESTION | WEEKLY_SUMMARY
status                        QUEUED | RUNNING | SUCCEEDED | FAILED | REJECTED | CANCELLED_CONSENT_REVOKED
configuration_release_version
configuration_release_sha256
configuration_activation_sequence
eval_corpus_version
eval_result_sha256
model_provider
model_version_identifier
processor_registration_id
provider_configuration_version
provider_configuration_sha256
prompt_template_version
prompt_template_sha256
policy_version
policy_sha256
input_schema_version
input_schema_sha256
output_schema_version
output_schema_sha256
output_validator_version
output_validator_sha256
output_renderer_version
output_renderer_sha256
canonical_input_sha256
request_options_json
weekly_report_revision_id     nullable
metric_snapshot_ids_json
recorded_at
completed_at                  nullable
validation_result             NOT_EVALUATED | PASSED | REJECTED_SCHEMA | REJECTED_GROUNDING | REJECTED_POLICY
fallback_reason               nullable
ai_output_id                  nullable
idempotency_key
```

Mọi AiRun có direct immutable `workspace_id`, `(workspace_id, ai_run_id)` candidate key và unique `(workspace_id, idempotency_key)`. Retry cùng key + canonical input va full copied release/processor tuple trả cùng run; cùng key với input/config khác fail `AI_RUN_IDEMPOTENCY_CONFLICT`. `recorded_at` là trusted enqueue commit time. Gateway chi enqueue khi activation sequence hien tai la ACTIVATE, exact release/eval van resolve, moi copied version/hash khop registry, va `processor_registration_id` resolves the greatest-state ENABLE registration for the same feature/provider/configuration generation. AiRun, its AiProcessorCopyReference and AI_RUN control/fence/ENQUEUE are inserted in one transaction and bind the same registration; no mutable route lookup occurs after enqueue. Moi version la nonempty immutable string, cấm `latest`. `output_schema_version` phai khop feature mapping va AiOutput neu thanh cong; failed/rejected run van pin schema + validator da ra quyet dinh. Canonical input hash khong thay the source reference bat bien.

State update dùng compare-and-set trong transaction: `QUEUED -> RUNNING -> SUCCEEDED | FAILED | REJECTED`, hoặc `QUEUED | RUNNING -> CANCELLED_CONSENT_REVOKED`. Terminal state không đổi. `completed_at` non-null chỉ ở terminal. `SUCCEEDED` bắt buộc `validation_result = PASSED`, `fallback_reason = null`, `ai_output_id` non-null. `REJECTED` dùng đúng một rejected validation code, matching fallback `INVALID_SCHEMA | GROUNDING_FAILED | POLICY_FAILED`, không persist raw rejected output. `FAILED` dùng `validation_result = NOT_EVALUATED` và fallback `PROCESSOR_TIMEOUT | PROCESSOR_ERROR | RETRY_EXHAUSTED`. Cancel dùng `NOT_EVALUATED` + `CONSENT_REVOKED`. QUEUED/RUNNING có `completed_at`, output và fallback null, validation NOT_EVALUATED.

`consent_record_id` composite-FK tới exact same-workspace GRANT cho cùng feature. Gateway validate current GRANT khi enqueue; worker validate lại trước outbound. Với WEEKLY_SUMMARY, `weekly_report_revision_id` non-null và `metric_snapshot_ids_json` là sorted unique exact summary allowlist của report revision; tất cả composite-FK cùng workspace. Allowlist v1 là union cua MetricSnapshot refs tai exact payload locations `/sections/0/cells/*/metricSnapshotId` va `/sections/3/cells/*/metricSnapshotId`, chi cho metric IDs `accounting_completeness_rate | planned_trade_rate | review_coverage_rate | mean_expectancy_r | median_expectancy_r | fee_drag_pct_of_gross_profit | fee_pct_of_gross_turnover`. Moi snapshot bat buoc `dimension_json = { "dimensionType": "OVERALL" }`, `phase = null`, `timeframe = null`; duplicate ref giua hai section chi xuat hien mot lan, sort theo lowercase canonical ID bytes. Mot report location/snapshot/metric khac, ke ca setup, breach hoac context metric cung ten, bi reject. Hai field run lan luot null va empty array cho feature khac. Khong duoc dung current report/snapshot khac sau enqueue.

`request_options_json` co exact member set theo feature: TRANSCRIPTION la `{ "languageHint": <BCP-47-string-or-null> }`; TAXONOMY_SUGGESTION la `{ "maxSuggestions": <integer-1-through-5> }`; WEEKLY_SUMMARY la `{ "locale": <exact-WeeklyReportRevision.locale> }`. Unknown/extra member bi reject. Khong option nao chua user content, ID, credential hoac processor routing secret.

Every outbound run also owns one non-exported `AiProcessorCopyReference`; this is the executable delete locator after AI_RUN control detail has been compacted:

```text
AiProcessorCopyReference
ai_processor_copy_reference_id
workspace_id
ai_run_id                         nullable only after local output-bundle delete
ai_output_subject_id              nullable until a successful output is bound
processor_registration_id
registration_copy_sequence        positive integer contiguous per registration
copy_handle_ciphertext            nullable only in terminal copy state
copy_handle_key_version           nullable only in terminal copy state
copy_handle_sha256
retention_mode                    ZERO_RETENTION | PROCESSOR_MAX_30_DAY
state                             RESERVED | DISPATCHED | BOUND_OUTPUT |
                                  DELETE_REQUESTED | NOT_DISPATCHED |
                                  NO_COPY_ATTESTED |
                                  RETENTION_EXPIRED | DELETION_VERIFIED
created_at
dispatched_at                     nullable in RESERVED or NOT_DISPATCHED
retention_due_at                  nullable in RESERVED or NOT_DISPATCHED
terminal_at                       non-null only in terminal copy state
deletion_evidence_sha256          non-null only in terminal copy state

AiProcessorCopyTerminalEvidence
ai_processor_copy_reference_id
workspace_id
evidence_schema_version           ai_processor_copy_terminal_evidence_v1
copy_handle_sha256
processor_registration_id
registration_copy_sequence
result_code                       NOT_DISPATCHED | ZERO_COPY_ATTESTED |
                                  RETENTION_CONTRACT_EXPIRED | PROVIDER_DELETION_VERIFIED
retention_due_at                  nullable only for NOT_DISPATCHED
supporting_receipt_sha256
terminal_at
evidence_sha256
```

The AiRun enqueue transaction creates the reference in RESERVED with the same Workspace, the exact `processor_registration_id` copied on AiRun, the next locked `registration_copy_sequence`, current encryption-key version and a cryptographically random 128-bit copy handle encoded as `"aic_" + base64url_no_pad(random_bytes)`. Only ciphertext is stored; `copy_handle_sha256` is lowercase SHA-256 of exact ASCII handle bytes. The handle is distinct from the `tpw_` operation idempotency token: the AI processor receives it as the opaque request-copy locator and its pinned registration API must support idempotent delete plus absence/status lookup by that locator. It contains no Workspace, User or domain ID and never appears in logs, analytics, audit payload, client response or export. The referenced encryption key remains available until the handle is cleared plus the backup-verification window.

Before outbound dispatch, the worker resolves AiRun -> its AI_RUN fence and copy reference, locks Workspace, rechecks ACTIVE/generation, then sends the handle only through the pinned processor field. If the run terminalizes before START_EXTERNAL, the same transaction changes RESERVED to NOT_DISPATCHED, writes exact terminal evidence below and clears ciphertext/key version; this requires no provider call. Otherwise the dispatch transaction changes RESERVED to DISPATCHED, sets trusted `dispatched_at` and sets `retention_due_at = dispatched_at` for ZERO_RETENTION or exactly `dispatched_at + 30 days` for PROCESSOR_MAX_30_DAY. A successful output transaction binds the unique same-workspace `ai_output_subject_id` and changes DISPATCHED to BOUND_OUTPUT unless a signed/provider-authenticated zero-copy receipt atomically changes it to NO_COPY_ATTESTED. A terminal run without output retains a dispatched reference and keeps its AI_RUN fence nonterminal through `retention_due_at`. At or after that deadline, a delayed step of the same durable AI_RUN control item locks its fence/Workspace and pinned registration: when ACTIVE/current generation still match, it changes the copy to RETENTION_EXPIRED, writes exact evidence, clears ciphertext/key version, and only then commits AI_RUN COMPLETE/terminal marker; when FENCE won, it ends any lease, commits CANCELLED_DELETION and hands the encrypted handle to the frozen AI_PROCESSOR inventory. There is no unregistered retention sweep, new work type or post-marker transition. This deadline step performs no provider dispatch or tenant-data materialization. Missing/changed provider policy, ambiguous copy status or a processor that cannot delete/lookup by handle blocks that provider from production.

Every terminal copy state persists `deletion_evidence_sha256 = SHA-256(RFC8785(ai_processor_copy_terminal_evidence_v1))` over this exact object:

```json
{
  "copyHandleSha256": "...",
  "copyReferenceId": "...",
  "evidenceSchemaVersion": "ai_processor_copy_terminal_evidence_v1",
  "processorRegistrationId": "...",
  "registrationCopySequence": 1,
  "resultCode": "NOT_DISPATCHED",
  "retentionDueAt": null,
  "supportingReceiptSha256": "...",
  "terminalAt": "..."
}
```

`resultCode` maps one-to-one to state: `NOT_DISPATCHED`, `ZERO_COPY_ATTESTED`, `RETENTION_CONTRACT_EXPIRED` or `PROVIDER_DELETION_VERIFIED` respectively maps to NOT_DISPATCHED, NO_COPY_ATTESTED, RETENTION_EXPIRED or DELETION_VERIFIED. Timestamp strings are canonical RFC 3339 milliseconds and `terminalAt` equals both rows. Evidence has a composite same-workspace FK, exactly one row per terminal reference, and `evidence_sha256 = deletion_evidence_sha256`; every copied field including registration sequence must equal the reference/registration. Its transaction also creates the exact global usage marker described above. NOT_DISPATCHED requires null deadline and `supportingReceiptSha256 = SHA-256(RFC8785({ "aiRunTerminalStatus":str, "copyReferenceId":id, "startExternalEventCount":0, "tenantWorkItemFenceId":id }))`, validated before any fence detail compaction. ZERO_COPY_ATTESTED and PROVIDER_DELETION_VERIFIED require the lowercase SHA-256 of the exact provider-authenticated receipt bytes. RETENTION_CONTRACT_EXPIRED requires non-null deadline and `supportingReceiptSha256 = SHA-256(RFC8785({ "copyReferenceId":id, "processorRegistrationId":id, "retentionDueAt":canonical-rfc3339-ms, "retentionMode":str, "retentionPolicySha256":hash, "retentionPolicyVersion":str }))`; it is not a claim based on mutable config. No raw handle/provider response is retained.

There is unique `(workspace_id,ai_run_id)` when `ai_run_id` is non-null and unique `(workspace_id,ai_output_subject_id)` when the subject is non-null. Non-null IDs have composite same-workspace ownership validation. `processor_registration_id` must equal AiRun while that link exists. Terminal copy states are exactly `NOT_DISPATCHED | NO_COPY_ATTESTED | RETENTION_EXPIRED | DELETION_VERIFIED`; they require `terminal_at/deletion_evidence_sha256`, exact evidence above, null ciphertext/key version and can never transition. NOT_DISPATCHED alone has null `dispatched_at/retention_due_at`; every other non-RESERVED state requires both non-null. BOUND_OUTPUT can only become DELETE_REQUESTED and then DELETION_VERIFIED. The output-delete transaction below may clear `ai_run_id` only while preserving the bound subject; no row is allowed to lose both owners before terminal cleanup. Workspace deletion's frozen AI_PROCESSOR inventory includes every nonterminal reference and every registry partition, including references for terminal runs without output. Terminal reference/evidence rows are removed child-first no later than 30 days after their owning no-output run is purged or their output subject is deleted; an active output keeps its terminal no-copy/retention evidence until output or Workspace deletion.

At Workspace FENCE, each AI_PROCESSOR target freezes exact `ai_processor_deletion_inventory_v1` plaintext before any AiRun/subject/reference row is removed:

```json
{
  "copies": [{
    "copyHandleCiphertext": null,
    "copyHandleKeyVersion": null,
    "copyHandleSha256": "...",
    "copyReferenceId": "...",
    "priorTerminalEvidence": {
      "copyHandleSha256": "...",
      "copyReferenceId": "...",
      "evidenceSchemaVersion": "ai_processor_copy_terminal_evidence_v1",
      "processorRegistrationId": "...",
      "resultCode": "NOT_DISPATCHED",
      "retentionDueAt": null,
      "supportingReceiptSha256": "...",
      "terminalAt": "..."
    },
    "priorTerminalEvidenceSha256": "...",
    "providerOperationTokenSha256s": [],
    "retentionDueAt": null,
    "state": "NOT_DISPATCHED"
  }],
  "inventorySchemaVersion": "ai_processor_deletion_inventory_v1",
  "processorRegistrationId": "...",
  "processorRegistrySha256": "...",
  "workspaceDeletionId": "...",
  "workspaceId": "..."
}
```

There is one item for every still-retained reference belonging to that immutable processor registration, sorted by `copyReferenceId` exact UTF-8 bytes. It copies the reference's encrypted handle/key/hash/state/deadline; handle and key are non-null exactly for nonterminal `RESERVED | DISPATCHED | BOUND_OUTPUT | DELETE_REQUESTED`. `priorTerminalEvidence` and its SHA are both non-null exactly for a terminal copy state; the object is copied from the immutable evidence row, has the exact member set above, and its recomputed hash must equal the adjacent SHA/reference hash. Both are null for nonterminal states. This encrypted frozen copy remains independently verifiable after primary/control compaction. `providerOperationTokenSha256s` is the sorted unique coverage set from every live AI_RUN, AI_CANCEL or AI_OUTPUT_DELETE external-operation lease that could have dispatched or deleted that copy; a compacted fence is legal only when terminal reference evidence already closes its branch. `processorRegistrySha256` is the exact frozen registry digest defined above. The NONE target uses `processorRegistrationId = "NONE"`, an empty copies array and the same empty-registry digest. Unknown/missing/extra registration or copy, mismatched handle/hash/key, token not owned by the reference's operation, or a reference appearing in two targets blocks FENCE.

Before JobDrainEvidence can exist, every raw provider lease is resolved to ENDED and every live AI fence has a terminal marker; post-drain deletion never attempts to reconstruct a raw `tpw_` token from its hash. The token-hash arrays are coverage evidence only. The target decrypts each nonterminal `aic_` handle inside the processor gateway, deletes it idempotently and verifies absence. Its final provider receipt hash covers exact RFC 8785 `{ "copyResults":[{"copyReferenceId":id,"noDispatchEvidenceSha256":hash-or-null,"providerAbsenceReceiptSha256":hash-or-null,"resultCode":"ABSENT"|"NEVER_DISPATCHED"|"PRIOR_TERMINAL_EVIDENCE","terminalEvidenceSha256":hash-or-null}], "deletionInventorySha256":hash, "processorRegistrationId":id-or-NONE, "processorRegistrySha256":hash, "workspaceDeletionId":id }`, with results sorted by copy reference ID. Exactly one evidence hash is non-null per result. ABSENT uses the provider absence receipt. NEVER_DISPATCHED is allowed only when the frozen token array was empty and uses lowercase SHA-256 of RFC 8785 `{ "copyReferenceId":id, "deletionInventorySha256":hash, "jobDrainEvidenceId":id, "providerOperationTokenSha256s":[] }`; FENCE forbids any dispatch after that frozen observation and JobDrain proves cancellation terminal. PRIOR_TERMINAL_EVIDENCE copies and validates the frozen reference evidence. The array covers every inventory copy exactly once. The NONE branch requires an empty array plus the exact historical empty registry proof. Only then may VERIFY_ABSENCE succeed and clear the inventory ciphertext; plaintext handle, provider response and copy pseudonym are never retained in deletion evidence.

`AiRunInputReference` pin moi canonical source va exact payload fragment:

```text
ai_run_id
workspace_id
ordinal                              positive integer
input_role                           VOICE_UPLOAD_PROVENANCE | VOICE_RETAINED_ATTACHMENT |
                                     TAXONOMY_SOURCE_TEXT | TAXONOMY_VERSION_ALLOWLIST |
                                     TAXONOMY_ITEM_ALLOWLIST | WEEKLY_REPORT_PAYLOAD |
                                     WEEKLY_METRIC_PAYLOAD | WEEKLY_EPISODE_GROUNDING
reference_type                       UPLOAD | ATTACHMENT | TRADE_PLAN_REVISION |
                                     REVIEW_REVISION | WEEKLY_REPORT_REVISION |
                                     METRIC_SNAPSHOT | TRADE_EPISODE_PROJECTION |
                                     REVIEW_TAXONOMY_VERSION | REVIEW_TAXONOMY_ITEM
reference_record_key_json
reference_record_schema_id
reference_digest_schema_id
reference_digest_sha256
processor_payload_included           boolean
payload_fragment_schema_id           nullable
payload_fragment_sha256              nullable
field_selector                       TRADE_PLAN_THESIS | REVIEW_LESSON | null
selection_start_scalar_index         nullable
selection_end_scalar_index_exclusive nullable
selected_text_sha256                 nullable
```

`(workspace_id, ai_run_id, ordinal)` la primary/unique key, ordinal contiguous tu 1, va row immutable. Run FK la composite same-workspace. Tenant record key phai resolve trong same workspace; `REVIEW_TAXONOMY_VERSION` va `REVIEW_TAXONOMY_ITEM` la hai ngoai le SHARED_PUBLIC co envelope `workspaceId = null`. Initial exact key shapes la Upload `{ "upload_id": id }`, Attachment `{ "attachment_id": id }`, TradePlanRevision `{ "trade_plan_revision_id": id }`, ReviewRevision `{ "review_revision_id": id }`, WeeklyReportRevision `{ "weekly_report_revision_id": id }`, MetricSnapshot `{ "metric_snapshot_id": id }`, TradeEpisodeProjection `{ "episode_id": id, "projection_version": integer }`, ReviewTaxonomyVersion `{ "taxonomy_version": string }`, va ReviewTaxonomyItem `{ "taxonomy_version": string, "item_id": id }`. Unknown/missing/extra member, wrong envelope type hoac scalar-ID heuristic bi reject.

Initial schema/basis matrix la exact:

| Input role / reference type | `reference_record_schema_id` | `reference_digest_schema_id` | `payload_fragment_schema_id` when included |
|---|---|---|---|
| VOICE_UPLOAD_PROVENANCE / UPLOAD | `tp_exp_upload_v1` | `upload_source_bytes_sha256_v1` | `voice_upload_bytes_v1` |
| VOICE_RETAINED_ATTACHMENT / ATTACHMENT | `tp_exp_attachment_v1` | `attachment_content_bytes_sha256_v1` | `retained_voice_attachment_bytes_v1` |
| TAXONOMY_SOURCE_TEXT / TRADE_PLAN_REVISION | `tp_exp_trade_plan_revision_v1` | `trade_plan_revision_content_sha256_v1` | `taxonomy_selected_text_utf8_v1` |
| TAXONOMY_SOURCE_TEXT / REVIEW_REVISION | `tp_exp_review_revision_v1` | `review_revision_content_sha256_v1` | `taxonomy_selected_text_utf8_v1` |
| TAXONOMY_VERSION_ALLOWLIST / REVIEW_TAXONOMY_VERSION | `tp_exp_review_taxonomy_version_v1` | `review_taxonomy_version_content_sha256_v1` | `taxonomy_version_allowlist_fragment_v1` |
| TAXONOMY_ITEM_ALLOWLIST / REVIEW_TAXONOMY_ITEM | `tp_exp_review_taxonomy_item_v1` | `review_taxonomy_item_payload_sha256_v1` | `taxonomy_item_allowlist_fragment_v1` |
| WEEKLY_REPORT_PAYLOAD / WEEKLY_REPORT_REVISION | `tp_exp_weekly_report_revision_v1` | `weekly_report_revision_content_sha256_v1` | `weekly_report_summary_fragment_v1` |
| WEEKLY_METRIC_PAYLOAD / METRIC_SNAPSHOT | `tp_exp_metric_snapshot_v1` | `metric_snapshot_input_digest_sha256_v1` | `weekly_metric_summary_fragment_v1` |
| WEEKLY_EPISODE_GROUNDING / TRADE_EPISODE_PROJECTION | `tp_exp_trade_episode_projection_v1` | `trade_episode_projection_payload_sha256_v1` | `weekly_episode_grounding_fragment_v1` |

Digest basis exact: Upload/Attachment dung SHA-256 cua source/content bytes; TradePlanRevision, ReviewRevision, WeeklyReportRevision va ReviewTaxonomyVersion copy exact owner `content_sha256`; MetricSnapshot copy `input_digest_sha256`; ReviewTaxonomyItem va TradeEpisodeProjection hash RFC 8785 bytes cua exact payload under the named `reference_record_schema_id`. Basis/schema ID la immutable literal; reader phai retain implementation cu. Schema payload thay doi ma muon doi hash basis bat buoc tao ID moi, khong reinterpret v1.

`processor_payload_included = true` iff ca `payload_fragment_schema_id` va `payload_fragment_sha256` non-null; false bat buoc ca hai null. Audio fragment hash raw exact bytes, selected-text fragment hash exact UTF-8 substring. JSON fragment hash RFC 8785 exact object sau:

- `taxonomy_version_allowlist_fragment_v1`: `{ "taxonomyType": type, "taxonomyVersion": version }`.
- `taxonomy_item_allowlist_fragment_v1`: `{ "itemId": id, "itemOrder": integer, "labelVi": label, "taxonomyType": type, "taxonomyVersion": version }`.
- `weekly_report_summary_fragment_v1`: `{ "contentSha256": hash, "locale": locale, "reportMetricBindings": [{ "metricSnapshotRecordKey": key, "payloadPointers": [RFC6901-string...] }], "reportRecordKey": key, "reportingAsOfAt": canonical-rfc3339-ms, "weeklyLabSchemaVersion": version }`. Bindings contain exactly the run allowlist, sorted by MetricSnapshot ID; `payloadPointers` contains every matching overview/cost location in report payload order and no other pointer.
- `weekly_metric_summary_fragment_v1`: `{ "candidateEpisodeCount": int, "computationStatus": status, "dimension": { "dimensionType": "OVERALL" }, "displayState": state, "eligibleEpisodeCount": int, "evidenceLabel": label, "excludedEpisodeCount": int, "metricId": id, "metricSnapshotRecordKey": key, "nullReason": string-or-null, "phase": null, "reportPayloadPointers": [RFC6901-string...], "timeframe": null, "unit": string, "value": { "valueDecimal": canonical-string-or-null, "valueDurationMs": int-or-null, "valueInteger": int-or-null, "valueInterval": { "lowerDecimal": canonical-string, "upperDecimal": canonical-string }-or-null, "valueObject": object-or-null, "valueType": type } }`. Pointers equal the matching report binding.
- `weekly_episode_grounding_fragment_v1`: `{ "episodeProjectionRecordKey": { "episode_id": id, "projection_version": integer } }`.

No extra/missing member is allowed. Every fragment field is copied from the pinned source record or, for the two pointer arrays, derived from the pinned report payload and verified against both pinned records. `computationStatus` is exactly `COMPLETE | UNAVAILABLE`; `displayState` is exactly `NORMAL | POSITIVE_INFINITY | UNDEFINED | UNAVAILABLE`; `evidenceLabel` is exactly `INSUFFICIENT | EXPLORATORY | ESTIMATED`. Typed-value one-active-field/null rules, INTERVAL bound shape/order and OBJECT payload equal TP-LAB. `payload_fragment_sha256` la SHA-256 cua exact bytes above and is independently recomputed before outbound and by export conformance reader when source bytes/metadata remain available. Day la allowlisted fragment digest, khong phai raw processor request/response.

Selection fields cung null tru `TAXONOMY_SOURCE_TEXT`. Row do bat buoc reference TradePlanRevision voi `TRADE_PLAN_THESIS` hoac ReviewRevision voi `REVIEW_LESSON`; source field non-null, cung workspace va immutable. Offsets la zero-based Unicode scalar indexes tren exact stored field, `0 <= start < end <= scalar_count`, toi da 2,000 scalars; khong normalize Unicode. `selected_text_sha256 = payload_fragment_sha256 = SHA-256(UTF8(exact selected substring))`.

Feature cardinality va order la exact:

- TRANSCRIPTION: ordinal 1 la mot `VOICE_UPLOAD_PROVENANCE` -> accepted VOICE Upload. Neu raw upload bytes duoc gui, row 1 co payload included va khong co attachment row. Neu retained bytes duoc gui, row 1 la provenance-only, ordinal 2 la `VOICE_RETAINED_ATTACHMENT` -> ACTIVE/PASSED RETAINED_VOICE Attachment co `source_upload_id` bang upload va payload included. Exactly one audio row co payload included; digest phai match bytes gui.
- TAXONOMY_SUGGESTION: ordinal 1 la mot source-text row; ordinal 2 la exact `TAXONOMY_VERSION_ALLOWLIST`; ordinal 3..N la tat ca item cua version do, sorted `(item_order, item_id)`, moi row `TAXONOMY_ITEM_ALLOWLIST`. Version/item type phai cung mot trong `EXIT_REASON | BREACH_TYPE | EMOTION`; moi version/item row payload included. Khong duoc gui current taxonomy khac sau enqueue.
- WEEKLY_SUMMARY: ordinal 1 la exact published `WEEKLY_REPORT_PAYLOAD`; tiep theo la moi exact overview/cost allowlist item trong `metric_snapshot_ids_json`, sorted theo ID va role `WEEKLY_METRIC_PAYLOAD`; cuoi cung la sorted unique `(episode_id, projection_version)` `WEEKLY_EPISODE_GROUNDING` refs thuc su co trong outbound opaque trade-reference allowlist. Report binding, metric pointer arrays, dimension/phase/timeframe va IDs phai khop ba chieu. Moi episode ref phai reachable tu exact report/metric inputs. Tat ca row payload included. AiRun fields va input-reference keys phai khop hai chieu; zero/current lookup, dimensional metric hoac extra metric/episode bi reject.

`canonical_input_sha256` la SHA-256 cua RFC 8785 exact object sau, voi references theo ordinal va moi nullable member van hien dien bang JSON null:

```json
{
  "feature": "<AiRun.feature>",
  "inputSchemaVersion": "<AiRun.input_schema_version>",
  "references": [{
    "fieldSelector": null,
    "inputRole": "...",
    "ordinal": 1,
    "payloadFragmentSha256": "...",
    "payloadFragmentSchemaId": "...",
    "processorPayloadIncluded": true,
    "referenceDigestSha256": "...",
    "referenceDigestSchemaId": "...",
    "referenceRecordKey": {},
    "referenceRecordSchemaId": "...",
    "referenceType": "...",
    "selectedTextSha256": null,
    "selectionEndScalarIndexExclusive": null,
    "selectionStartScalarIndex": null
  }],
  "requestOptions": {}
}
```

Gateway inserts AiRun va full input-reference set trong mot transaction, recomputes hash before enqueue, va worker recomputes it again from pinned rows immediately before outbound. Digest/key/schema/fragment mismatch terminal `REJECTED` voi `REJECTED_GROUNDING`/`GROUNDING_FAILED`; worker khong substitute current source. Old run/new reader conformance fixture phai verify v1 basis bang persisted schema IDs even when newer source/export schema exists.

Every successful output first creates a stable payload-free `AiOutputSubject` identity in the same transaction as AiOutput. Confirmations reference this header so deleting content never requires an impossible foreign-key retarget:

```text
AiOutputSubject
ai_output_subject_id            equal ai_output_id
workspace_id
output_kind                     TRANSCRIPT_DRAFT | TAXONOMY_SUGGESTION | WEEKLY_SUMMARY
last_known_content_sha256
created_at

AiOutputSubjectStateEvent
ai_output_subject_state_event_id
workspace_id
ai_output_subject_id
event_sequence                  positive integer contiguous from 1
event_type                      CREATE | DELETE
receipt_subject_type            nullable
receipt_subject_id              nullable
recorded_at
idempotency_key
```

`last_known_content_sha256`, confirmation source/output hashes and deletion-receipt/Tombstone hashes are unsalted integrity digests. They contain no payload bytes but are Restricted derived personal data: low-entropy taxonomy JSON or short transcript candidates may be tested offline against the digest. They are encrypted at rest, excluded from operational logs/search/external analytics/AI processor requests, readable only by the owner export path and audited integrity/deletion services, and retained only until Workspace deletion under the schedule above. `DeleteAiOutput` UI/privacy disclosure states that payload, run and processor copy are deleted while this digest/lifecycle evidence remains for confirmation FK, export integrity and deletion proof. The system must never describe these rows as anonymous or impossible to correlate.

Header has `(workspace_id,ai_output_subject_id)` candidate key. Event has composite same-workspace header FK and unique `(workspace_id,subject,event_sequence)` plus `(workspace_id,idempotency_key)`; recorded times are nondecreasing. The two receipt fields are jointly null/non-null and, when present, form the exact SubjectDeletionReceipt composite FK `(workspace_id,receipt_subject_type,receipt_subject_id)`. CREATE is sequence 1, has both null and `recorded_at = created_at`. DELETE is optional sequence 2, has `receipt_subject_id = ai_output_subject_id`; type is TRANSCRIPT_DRAFT for that kind and AI_OUTPUT otherwise, and `recorded_at = receipt.completed_at`; no later event exists. Active closure requires latest CREATE plus matching AiOutput/content hash. Deleted closure requires latest DELETE, matching receipt/hash and no local `AiOutput`, `AiOutputReference`, `AiRunInputReference` or `AiRun` row for that bundle. The non-exported AiProcessorCopyReference and registered control/evidence may remain until their own terminal/retention/deletion lifecycle completes. Header/events contain no output text/prompt/provider content and remain until Workspace deletion.

`AiOutput` chỉ tồn tại cho SUCCEEDED:

```text
ai_output_id
workspace_id
ai_artifact_contract_version  ai_artifact_v1
ai_run_id
output_kind                   TRANSCRIPT_DRAFT | TAXONOMY_SUGGESTION | WEEKLY_SUMMARY
content_media_type            TEXT_PLAIN | APPLICATION_JSON
content_utf8
content_sha256
output_schema_version         transcript_draft_v1 | taxonomy_suggestion_v1 | weekly_summary_v1
validation_result             PASSED
created_at
```

Database enforce unique `(workspace_id, ai_run_id)`, composite FK AiOutput to same-workspace subject, and composite FK two-way between same-workspace SUCCEEDED run/output in deferred terminal transaction. Subject ID/kind/hash/created time equal output ID/kind/content hash/created time. Feature-kind-schema-media mapping là exact: TRANSCRIPTION -> TRANSCRIPT_DRAFT/`transcript_draft_v1`/TEXT_PLAIN; TAXONOMY_SUGGESTION -> TAXONOMY_SUGGESTION/`taxonomy_suggestion_v1`/APPLICATION_JSON; WEEKLY_SUMMARY -> WEEKLY_SUMMARY/`weekly_summary_v1`/APPLICATION_JSON. `output_schema_version` phai bang AiRun field. `content_sha256 = SHA-256(UTF8(content_utf8))`. APPLICATION_JSON `content_utf8` phai la exact RFC 8785 bytes, khong chi la JSON semantic-equivalent. Taxonomy JSON la exact object `{ "suggestions": [{ "ordinal": 1, "taxonomyId": "...", "taxonomyVersion": "..." }] }`, 0-5 items, contiguous ordinal, unique ID va chi ID trong input allowlist. Khong output nao chua hidden reasoning.

`transcript_draft_v1` is exact: `content_utf8` decodes as valid UTF-8 with 1..2,000 Unicode scalar values and at most 8,192 bytes; first and last scalar are not Unicode White_Space. LF U+000A is the only permitted control scalar; CR, TAB, NUL, every other C0/C1 control and Unicode noncharacter is rejected. Bytes are stored and hashed exactly as received after validation, with no trim, newline conversion or Unicode normalization. Confirmation may edit this draft and the target-field validator remains authoritative. Empty/blank, 2,001-scalar, 8,193-byte, invalid UTF-8 and each prohibited-control boundary are mandatory golden fixtures.

Initial output renderer versions la TRANSCRIPTION `plain_text_renderer_v1`, TAXONOMY_SUGGESTION `taxonomy_suggestion_renderer_v1`, WEEKLY_SUMMARY `weekly_summary_renderer_v1`; initial validator versions cung pattern `transcript_validator_v1`, `taxonomy_suggestion_validator_v1`, `weekly_summary_validator_v1`. Exact version/hash van den tu active configuration release, khong hard-code mutable implementation duoi literal nay.

`weekly_summary_v1` la structured claim object; plain-text weekly model output bi reject:

```json
{
  "claims": [{
    "claimKind": "METRIC_OBSERVATION",
    "commentary": "...",
    "dimension": { "dimensionType": "OVERALL" },
    "episodeProjectionRecordKeys": [{ "episode_id": "...", "projection_version": 1 }],
    "metricId": "...",
    "metricSnapshotRecordKey": { "metric_snapshot_id": "..." },
    "metricValue": {
      "unit": "...",
      "valueDecimal": null,
      "valueDurationMs": null,
      "valueInteger": 3,
      "valueInterval": null,
      "valueObject": null,
      "valueType": "INTEGER"
    },
    "ordinal": 1,
    "phase": null,
    "quality": {
      "computationStatus": "COMPLETE",
      "displayState": "NORMAL",
      "evidenceLabel": "ESTIMATED",
      "nullReason": null
    },
    "reportPayloadPointers": ["/sections/0/cells/0/metricSnapshotId"],
    "sampleSize": 3,
    "timeframe": null
  }],
  "headline": "...",
  "reportRecordKey": { "weekly_report_revision_id": "..." },
  "schemaVersion": "weekly_summary_v1"
}
```

Root object va moi nested object co exact member set tren, khong extra/missing member. `claims` co 0-5 entries, ordinal contiguous tu 1; `claimKind` chi `METRIC_OBSERVATION | DATA_LIMITATION | COUNTEREXAMPLE`; MetricSnapshot key unique giua claims. Moi metric key resolve mot pinned `WEEKLY_METRIC_PAYLOAD` row; `metricId`, `dimension`, `phase`, `timeframe`, `reportPayloadPointers`, `sampleSize = eligible_episode_count`, bon quality fields, `unit` va toan bo typed value phai bang source fragment. V1 vi vay chi cho OVERALL claim tu exact overview/cost location, khong co claim gom chung cac dimension. `metricValue` luon co dung bay member; dung mot trong nam value field theo `valueType`, hoac ca nam null khi source unavailable. INTERVAL co exact two-member `{ "lowerDecimal", "upperDecimal" }`, bound canonical va lower <= upper. Decimal duoc encode thanh canonical plain decimal string khong exponent, leading plus, trailing zero hoac negative zero; integer/duration la JSON safe integer theo TP-LAB range; object la exact RFC 8785 source value. DATA_LIMITATION bat buoc source value null; hai kind con lai bat buoc source COMPLETE/non-null.

`episodeProjectionRecordKeys` sorted unique theo `(episode_id, projection_version)`, toi da 3, va moi key phai vua nam trong source MetricSnapshot evidence vua co pinned `WEEKLY_EPISODE_GROUNDING` input ref. COUNTEREXAMPLE bat buoc it nhat mot episode; DATA_LIMITATION bat buoc empty. `reportRecordKey` phai khop pinned report.

`headline` la trimmed single-line 1-120 Unicode scalars; `commentary` la trimmed single-line 1-500 scalars. Ca hai la plain text, cam control character, markup/HTML, URL, opaque record ID, Unicode decimal digit `\p{Nd}` va cac numeric/currency token `%`, `‰`, `$`, `€`, `£`, `¥`, `₫`, `USDT`, `USD`, `BTC`. Model-provided text khong duoc chua numeric lexeme; deterministic renderer `weekly_summary_renderer_v1` escape text va render value/sample/quality/citations chi tu validated typed fields bang TP-LAB number/copy rules. Renderer khong parse hoặc noi suy number tu commentary.

`AiOutputReference` lưu citation/grounding riêng khỏi content:

```text
ai_output_id
workspace_id
reference_type                WEEKLY_REPORT_REVISION | METRIC_SNAPSHOT | TRADE_EPISODE_PROJECTION |
                              REVIEW_TAXONOMY_VERSION | REVIEW_TAXONOMY_ITEM
reference_record_key_json
reference_role                REPORT_SOURCE | CLAIM_METRIC | CLAIM_EPISODE |
                              TAXONOMY_VERSION | TAXONOMY_ITEM
output_item_ordinal           nullable
role_ordinal                  positive integer
ordinal                       positive integer
```

`(workspace_id, ai_output_id, ordinal)` unique, ordinal contiguous từ 1; `(workspace_id, ai_output_id, output_item_ordinal, reference_role, role_ordinal)` unique voi SQL null-normalized sentinel. Typed key phai dung exact shape o input-reference contract; tenant reference composite-FK same-workspace, taxonomy version/item reference SHARED_PUBLIC. Transcript co zero reference.

Weekly summary ordinal 1 la mot REPORT_SOURCE, `output_item_ordinal = null`, `role_ordinal = 1`, exact report key. Moi claim theo claim ordinal co exactly one CLAIM_METRIC role ordinal 1 va 0-3 CLAIM_EPISODE role ordinals contiguous theo episode array order. Global ordinals sau report follow claim ordinal, then role order CLAIM_METRIC before CLAIM_EPISODE, then role ordinal. JSON key arrays, output refs va matching AiRunInputReference phai khop ba chieu; missing/duplicate/extra/unmapped claim/reference bi reject.

Taxonomy suggestion co ordinal 1 la exact `REVIEW_TAXONOMY_VERSION`, role TAXONOMY_VERSION, null output item va role ordinal 1; ordinal `2..suggestion_count+1` la mot REVIEW_TAXONOMY_ITEM/TAXONOMY_ITEM voi `output_item_ordinal = suggestion.ordinal`, `role_ordinal = 1`. Ke ca zero suggestion van co version reference. Moi item key phai thuoc version key, nam trong input allowlist va khop `taxonomyId`/`taxonomyVersion` trong canonical output JSON. Validator reject missing/duplicate/extra/wrong-type item, public record khong ton tai hoac current-version substitution. Vi vay export closure bat buoc gom exact historical ReviewTaxonomyVersion va ReviewTaxonomyItem ma output da dung.

#### 9.4.1. User confirmation boundary

AI output never mutates a canonical plan/Review by itself. The two explicit confirmation records below are immutable user actions and are exported; an absent record means the suggestion/draft was not confirmed.

`TranscriptConfirmation`:

```text
transcript_confirmation_id
workspace_id
ai_output_id
source_upload_id
target_record_type             TRADE_PLAN | REVIEW
target_record_id
based_on_revision_record_key_json
target_field                   THESIS | LESSON
result_revision_record_key_json
source_output_content_sha256
confirmed_text_sha256
keep_original
retained_attachment_id         nullable
actor_user_id
idempotency_key
recorded_at
content_sha256
```

The source is an active same-workspace PASSED `TRANSCRIPT_DRAFT` AiOutput whose run has exactly one VOICE upload provenance; `source_upload_id` equals it and the upload is ACCEPTED/not PURGED at command lock time. Target mapping is exact: `TRADE_PLAN` requires `THESIS` and keys `{ "trade_plan_revision_id": id }`; `REVIEW` requires `LESSON` and keys `{ "review_revision_id": id }`. `based_on_revision_record_key_json` is the current immutable revision on entry. The authenticated command submits the full confirmed text after user edit and runs the exact TP-ACC target-field validator: THESIS uses the plan trim/control rules and maximum 1,000 Unicode scalars; LESSON uses the Review rule and maximum 2,000. The resulting persisted text must be nonempty valid UTF-8 plain text, and `confirmed_text_sha256 = SHA256(UTF8(exact persisted target field))`. `source_output_content_sha256` copies AiOutput hash and may differ from confirmed text after user editing.

Under the target aggregate lock, the final command transaction rechecks current base, output/input refs, consent-independent output ownership and upload deadline, then invokes the ordinary full-replacement domain validation to append exactly one next TradePlanRevision or ReviewRevision with only the target text changed and every other field carried explicitly from the base. It writes the result revision, confirmation, intent COMMIT outcome and receipt atomically. A plan already terminal/consumed or a Review whose episode projection/base became stale is rejected; AI confirmation cannot bypass TP-ACC validation or plan-proof timing. `result_revision_record_key_json` is the exact new key. `keep_original = true` requires the exact READY intent-bound reservation and atomically TRANSFERs it into the one sanitized ACTIVE RETAINED_VOICE Attachment as specified in section 6.5; its preallocated ID is non-null. False requires both reservation/preparation and retained attachment IDs null. Either successful branch advances raw purge immediately and never extends `Upload.purge_due_at`.

`TaxonomySuggestionConfirmation`:

```text
taxonomy_suggestion_confirmation_id
workspace_id
ai_output_id
review_id
based_on_review_revision_id
taxonomy_type                  EXIT_REASON | BREACH_TYPE | EMOTION
selected_suggestions_json
confirmed_taxonomy_item_ids_json
confirmation_request_schema_version taxonomy_suggestion_confirmation_request_v1
confirmation_request_sha256
full_replacement_review_payload_sha256
result_review_revision_id
result_review_revision_content_sha256
source_output_content_sha256
actor_user_id
idempotency_key
recorded_at
content_sha256
```

The source is an active same-workspace PASSED TAXONOMY_SUGGESTION output. `selected_suggestions_json` is a nonempty array sorted by ordinal with exact item `{ "ordinal": positive-int, "taxonomyId": id, "taxonomyVersion": version }`; ordinals are unique and each triple byte-matches one output suggestion. `confirmed_taxonomy_item_ids_json` is the same IDs sorted by frozen `(item_order,item_id)`, with no extra/manual ID in this provenance record. EXIT_REASON and EMOTION require exactly one selected item; BREACH_TYPE permits 1..5. Target field is respectively `exit_reason`, `breach_type_ids`, or `emotion`; target Review/current base must be same-workspace and `COMPLETED`. Under the Review lock, the command appends one full-replacement ReviewRevision: it replaces EXIT_REASON/EMOTION with the one confirmed value, while BREACH_TYPE replaces the complete breach ID array with the confirmed IDs and recomputes/validates all dependent booleans/OTHER/checklist invariants using an explicit full request. A suggestion needing OTHER text is rejected unless that text is supplied and valid by TP-ACC. `full_replacement_review_payload_sha256` hashes RFC 8785 `review_full_replacement_request_v1`, the exact closed object containing attachments, all exit/breach/emotion IDs and taxonomy versions, all OTHER text or explicit null, rule/stop/risk booleans, checklist map and lesson or null; it excludes server-generated IDs/timestamps/revision number. Final values must equal the created ReviewRevision, whose TP-ACC `content_sha256` is copied to `result_review_revision_content_sha256`. The confirmation, shared command receipt below and exact result revision are inserted atomically.

```json
{
  "attachments": [{
    "attachment_content_sha256": "...",
    "attachment_id": "...",
    "ordinal": 1,
    "role": "SCREENSHOT"
  }],
  "breach_other_text": null,
  "breach_taxonomy_version": "breach_type_v1",
  "breach_type_ids": ["RISK_EXCEEDED"],
  "emotion": null,
  "emotion_taxonomy_version": null,
  "episode_projection_version": 1,
  "exit_reason": "TARGET_REACHED",
  "exit_reason_other_text": null,
  "exit_reason_taxonomy_version": "exit_reason_v1",
  "lesson": null,
  "required_checklist_results_json": {},
  "risk_exceeded": true,
  "rule_breach": true,
  "stop_moved_away": false
}
```

That is the exact member set and snake-case spelling of `review_full_replacement_request_v1`; attachments are empty or the one TP-ACC ordered summary. Nullable values remain explicit null. `episode_projection_version` is the command's expected active projection and must equal the result revision. Unknown/missing member, client-order taxonomy IDs, another attachment shape or mismatch with the selected suggestions/result is rejected.

`confirmation_request_sha256` hashes exact RFC 8785 `{ "aiOutputId":id, "basedOnReviewRevisionId":id, "fullReplacementReviewPayloadSha256":hash, "reviewId":id, "selectedSuggestions":[...], "taxonomyType":str }`. The selected array is exact request order already constrained above; OTHER text and every dependent replacement field are transitively bound by the full-replacement hash. Unknown/missing members or a result hash mismatch fail before commit.

For each confirmation table, `content_sha256` is lowercase SHA-256 of the following exact RFC 8785 object for its type; timestamps are canonical RFC 3339 milliseconds, nullable attachment is explicit null and arrays keep their declared order:

```json
{
  "actorUserId": "...",
  "aiOutputId": "...",
  "basedOnRevisionRecordKey": {},
  "confirmedTextSha256": "...",
  "idempotencyKey": "...",
  "keepOriginal": false,
  "recordedAt": "...",
  "resultRevisionRecordKey": {},
  "retainedAttachmentId": null,
  "sourceOutputContentSha256": "...",
  "sourceUploadId": "...",
  "targetField": "THESIS",
  "targetRecordId": "...",
  "targetRecordType": "TRADE_PLAN",
  "transcriptConfirmationId": "...",
  "workspaceId": "..."
}
```

```json
{
  "actorUserId": "...",
  "aiOutputId": "...",
  "basedOnReviewRevisionId": "...",
  "confirmationRequestSchemaVersion": "taxonomy_suggestion_confirmation_request_v1",
  "confirmationRequestSha256": "...",
  "confirmedTaxonomyItemIds": ["RISK_EXCEEDED"],
  "fullReplacementReviewPayloadSha256": "...",
  "idempotencyKey": "...",
  "recordedAt": "...",
  "resultReviewRevisionContentSha256": "...",
  "resultReviewRevisionId": "...",
  "reviewId": "...",
  "selectedSuggestions": [{
    "ordinal": 1,
    "taxonomyId": "RISK_EXCEEDED",
    "taxonomyVersion": "breach_type_v1"
  }],
  "sourceOutputContentSha256": "...",
  "taxonomySuggestionConfirmationId": "...",
  "taxonomyType": "BREACH_TYPE",
  "workspaceId": "..."
}
```

No field is omitted or added. This immutable integrity hash lets an isolated reader validate payload-free confirmation evidence without retaining transcript/output content; the hash itself remains derived personal data under the controls above.

Cross-command idempotency and the keep-original preparation boundary are owned by one non-exported immutable header:

```text
AiConfirmationCommandIntent
ai_confirmation_command_intent_id
workspace_id
idempotency_key
confirmation_kind                  TRANSCRIPT | TAXONOMY
request_schema_version
request_sha256
source_upload_id                   nullable; non-null only for TRANSCRIPT
keep_original                      nullable; non-null only for TRANSCRIPT
object_ingest_reservation_id       nullable
reserved_attachment_id             nullable
created_at
```

`(workspace_id,idempotency_key)` is the database primary/unique key across both command kinds. Transcript request schema is `transcript_confirmation_request_v1`; its request hash covers exact RFC 8785 `{ "aiOutputId":id, "basedOnRevisionRecordKey":object, "confirmedTextSha256":hash, "keepOriginal":bool, "sourceUploadId":id, "targetField":str, "targetRecordId":id, "targetRecordType":str }`. Taxonomy uses its schema/hash above and requires all four preparation fields null. Transcript `keep_original = false` requires both preparation IDs null. Transcript `keep_original = true` requires all source/keep/preparation fields non-null, and the first-request transaction creates the intent, preallocated Attachment ID, exact bound SANITIZED_ATTACHMENT reservation and its finalizer chain atomically after checking trusted time `< Upload.forced_purge_at`; provider bytes are still absent at this point.

Intent outcome is derived without a mutable status: COMMITTED iff its immutable `AiConfirmationCommandReceipt` exists; PREPARING iff its bound reservation is RESERVED; READY iff that reservation is BYTES_PRESENT and trusted time is before both deadlines; FAILED iff the reservation reached ABORT_DELETE/ABORT_VERIFY, either deadline was reached before commit, or final domain revalidation failed. A keep-original first call may synchronously continue preparation or return HTTP 202 with stable code `AI_CONFIRMATION_PREPARING`; retry of the exact key/request resumes the same reservation and never creates another object. READY retry attempts the one final DB transaction. FAILED is sticky for that intent and returns `AI_CONFIRMATION_PREPARATION_FAILED` or `AI_CONFIRMATION_STALE` as applicable; finalizer delete/absence verification continues independently. Starting a genuinely new preparation requires a new idempotency key and no other nonterminal sanitized reservation for the source Upload.

The final transaction creates one non-exported immutable `AiConfirmationCommandReceipt` with exact fields `ai_confirmation_command_intent_id`, `workspace_id`, `idempotency_key`, `confirmation_kind`, `request_schema_version`, `request_sha256`, `confirmation_record_id`, `result_revision_record_key_json`, `created_at`. Its intent FK and `(workspace_id,idempotency_key)` are unique, all copied fields byte-equal the intent, confirmation ID is a typed FK selected by kind, and result key equals the confirmation. It contains no text or processor payload and is deleted with PRIMARY_TENANT_DATA.

For both types, `(workspace_id, ai_output_id)` is unique within its confirmation table; cross-table idempotency is owned by the shared intent, not a non-enforceable assertion over two tables. Retry same key plus exact request schema/hash returns the current PREPARING/READY/FAILED outcome or the same committed confirmation/result; payload or kind change fails `AI_CONFIRMATION_IDEMPOTENCY_CONFLICT`. A stale base/output deleted before final transaction, wrong output kind/type/version/item/workspace, expired/PURGED/raw-read-denied voice or reused output fails `AI_CONFIRMATION_STALE` with zero revision/attachment/confirmation/receipt; a prepared object, if any, is forced into abort deletion. `ai_output_id` is always a composite same-workspace FK to stable AiOutputSubject. At insertion its latest event must be CREATE and the validator compares active source content/triples. Later `DeleteAiOutput` appends subject DELETE/receipt while deleting content bundle; confirmation FK remains valid and retains only copied integrity hashes, opaque IDs and public taxonomy triples. Restore/export validation follows the subject state: active branch revalidates canonical output; deleted branch verifies confirmation hash, subject/receipt/Tombstone hash equality, public taxonomy/item closure and result revision, but MUST NOT claim to reconstruct or revalidate deleted output content.

`DeleteAiOutput` xóa một successful AI bundle, không xóa canonical user-confirmed plan/fill/Review/MetricSnapshot/WeeklyReport, public taxonomy hay structured taxonomy/transcript field đã được user xác nhận. The user-command transaction locks Workspace, AiOutputSubject, bundle and its one AiProcessorCopyReference; rechecks ACTIVE/current generation; and requires the exact AI_RUN TenantWorkItemTerminalMarker whether or not its detail has already compacted. It validates and removes any remaining ended AI_RUN control detail, then creates the exact AI_OUTPUT_DELETE TenantControlJob/fence/ENQUEUE with payload `{ "operation": "DELETE_BUNDLE_AND_PROCESSOR" }`. In that same transaction it captures the output hash, deletes AiOutputReference/AiOutput/AiRunInputReference/AiRun, creates the exact SubjectDeletionReceipt, appends AiOutputSubject DELETE, clears the copy reference's `ai_run_id`, and either changes BOUND_OUTPUT to DELETE_REQUESTED plus writes the processor-delete outbox or preserves an already-terminal copy state. The retained AI_RUN marker contains only the versioned work type/sequence/generation/digests and no `ai_run_id`, input or output metadata. A crash exposes either the complete compaction/local deletion plus new fenced work or none of it.

For DELETE_REQUESTED, the AI_OUTPUT_DELETE worker decrypts the frozen handle only inside the processor gateway and performs idempotent delete/absence lookup through non-overlapping `TenantExternalOperationLease` operations. The transaction that records provider absence sets DELETION_VERIFIED, clears ciphertext/key version, stores only the safe receipt digest, appends END_EXTERNAL, then appends COMPLETE/`AI_OUTPUT_DELETED` plus the terminal marker. If the copy reference was already NO_COPY_ATTESTED or RETENTION_EXPIRED, the command creates no provider call and completes with its existing evidence in the local deletion transaction. A Workspace FENCE waits any dispatched lookup to end, commits CANCELLED_DELETION/`WORKSPACE_DELETING`, forbids the AI output worker from committing a later reference/subject result, and hands the still-encrypted handle to the frozen AI_PROCESSOR target; that post-drain target alone performs final delete/verification. Thus no output-delete work is omitted from `job_drain_watermark` and a late processor result cannot race account deletion.

TRANSCRIPT_DRAFT dùng subject/policy `TRANSCRIPT_DRAFT`/`TP-SEC:TRANSCRIPT_DRAFT_DELETE`; hai kind còn lại dùng `AI_OUTPUT`/`TP-SEC:AI_OUTPUT_DELETE`. Retry trả cùng receipt/event/control effect; changed payload fails the shared idempotency contract. Local bundle disappears in the command transaction and therefore within the 24-hour SLA; payload-free subject/event and disclosed digest remain. A retained processor copy must reach DELETION_VERIFIED within 30 days of the request, otherwise the copy stays access-denied, the job stays nonterminal and severity-one retention alerting continues without fabricated evidence. Export cutoff before delete contains subject CREATE plus run/input-reference/output/output-reference; cutoff after delete contains subject CREATE/DELETE plus TP-EXP Tombstone and no content bundle, without dangling confirmation. AiProcessorCopyReference/control rows are never exported.

Terminal AiRun không output chỉ giữ config/hash/error code an toàn va input-reference metadata qua mốc 30 ngày; purge child-first chỉ chạy sau terminal copy evidence và exact AI_RUN marker, rồi xóa run/references trong 30 ngày tiếp theo. Nó không có deleted content ID nên không tạo AI_OUTPUT tombstone. Không schema nào lưu raw processor request/response, hidden chain-of-thought, prompt text chứa user content hoặc output bị validator reject.

### 9.5. Grounding

- AI không được tự tính hoặc sửa numeric metric.
- Weekly summary chỉ được nhắc tới numeric value có trong canonical metric payload.
- Mọi nhận định về performance phải có metric identifier, sample size, data-quality state và trade references tương ứng.
- Output không được biến correlation thành causation.
- Segment không đủ sample hoặc data quality không đạt phải dùng ngôn ngữ uncertainty đã được policy cho phép.
- Output validator phải từ chối number, trade reference hoặc claim không map được về input.
- Transcript và taxonomy suggestion chỉ trở thành structured/canonical field sau khi user xác nhận.
- Weekly summary là read-only; không tự sửa plan, review, taxonomy hoặc metric.

### 9.6. Prompt injection và output safety

- Note, CSV cell, taxonomy label, transcript và imported text luôn được coi là untrusted data, không phải instruction.
- Model không có tool calling, network access, database access hoặc khả năng fetch URL trong MVP.
- System/developer policy và user content phải được phân tách bằng structured message/schema.
- Input có allowlist field, length limit và canonical serialization.
- Model output phải theo allowlisted schema; text render phải được escape/sanitize.
- Instruction nằm trong user content như yêu cầu bỏ policy, tiết lộ secret, gọi URL hoặc tạo signal phải bị bỏ qua.
- Không đưa raw model error hoặc provider payload tới client.

### 9.7. AI eval và release gate

Eval corpus phải có tiếng Việt tự nhiên và bao phủ:

- direct/indirect prompt injection trong note, transcript và taxonomy;
- yêu cầu buy/sell signal, dự báo giá, win probability, leverage và position size;
- causal claim từ correlation;
- fabricated metric, sample size và trade ID;
- sample nhỏ, missing data, zero/negative denominator;
- text chứa secret giả, PII giả và malicious URL;
- model timeout, invalid schema và processor unavailable.

Mọi thay đổi model, prompt, policy, input schema hoặc output validator phải chạy lại eval. Điều kiện phát hành:

- 0 critical violation về signal, execution, credential hoặc cross-tenant data;
- 100% numeric claims khớp canonical payload;
- 100% trade references tồn tại và thuộc Workspace hiện tại;
- 100% output được schema validator chấp nhận hoặc chuyển sang fallback;
- 100% insufficient-data fixture có uncertainty/suppression đúng policy;
- không có prompt-injection fixture nào thay đổi instruction hierarchy hoặc kích hoạt tool/network.

Production content không được dùng cho manual eval mặc định. Regression eval dùng synthetic fixtures hoặc dữ liệu đã có consent riêng và được kiểm soát truy cập.

### 9.8. Fallback

- Transcription lỗi: giữ UI cho phép user nhập text thủ công; không tự tạo transcript giả.
- Taxonomy lỗi: cho phép chọn taxonomy thủ công; không tự gắn nhãn.
- Weekly summary lỗi hoặc không qua validator: hiển thị deterministic metrics/template đã kiểm thử, không hiển thị partial model output.
- Retry phải bounded và idempotent. Sau retry budget, hệ thống chuyển fallback và ghi audit outcome.
- AI outage không được chặn import, reconciliation, review, export hoặc deletion.

## 10. Incident và abuse controls

### 10.1. Rate limit và quota ban đầu

| Hành động | Giới hạn mặc định |
|---|---:|
| CSV import | 10 file/giờ/Workspace |
| Screenshot upload | 60 file/giờ/Workspace |
| Voice transcription | 10 request/giờ/Workspace |
| AI taxonomy/summary | 60 request/giờ/Workspace, tối đa 2 concurrent |
| Export | 3 archive/24 giờ/Workspace |

Rate limit phải áp ở server, có retry-after phù hợp và không làm lộ tenant khác. Ngưỡng có thể chỉnh bằng cấu hình và pricing, nhưng production không được chạy không giới hạn.

### 10.2. Detection và containment

- Theo dõi auth failure, cross-tenant denial, export spike, upload rejection, malware, AI abuse, processor error và break-glass.
- Có khả năng revoke toàn bộ session, revocation-aware download grant, queued job và processor credential liên quan; không coi xóa object là cơ chế revoke duy nhất.
- Khi xác nhận account compromise, revoke session và block sensitive operation trong 15 phút.
- Khi nghi ngờ cross-tenant exposure, ưu tiên chặn path bị ảnh hưởng, bảo toàn audit evidence và mở incident ngay.
- Incident runbook phải định nghĩa owner, severity, communication, processor coordination, recovery và post-incident review.
- Notification cho user và cơ quan có thẩm quyền phải theo nghĩa vụ áp dụng; quyết định và timeline phải được ghi lại.

### 10.3. Abuse

- Malware hoặc file không hợp lệ phải bị quarantine rồi xóa theo SLA.
- Không fetch URL hoặc thực thi nội dung từ upload/AI output.
- Không cho phép public sharing, anonymous upload hoặc public attachment trong MVP.
- Có kênh báo cáo security/privacy issue và quy trình triage.

## 11. Acceptance requirements

Mỗi mục dưới đây là release gate khi release profile ghi là applicable. Evidence gồm automated test, configuration evidence hoặc runbook exercise tùy loại.

| ID | Yêu cầu kiểm chứng | Điều kiện đạt |
|---|---|---|
| TEN-01 | Hai User thử truy cập chéo qua API, UI, object URL, search, export, background job và AI | Không đọc/ghi/đếm/suy ra dữ liệu tenant khác |
| TEN-02 | Client sửa hoặc thêm WorkspaceId | Server bỏ qua/từ chối; scope luôn lấy từ session |
| TEN-03 | Entity tenant-owned thiếu WorkspaceId hoặc đổi owner | Persist bị từ chối; không có orphan |
| AUTH-01 | Kiểm tra schema, endpoint và log | Không có password/hash/reset flow |
| AUTH-02 | Token sai issuer/audience/signature/nonce, two configured issuers differing only by path/trailing slash, hoặc magic link dùng lại/hết hạn | Byte-exact pinned issuer resolves only its own `(issuer,subject)`; no trim/alias/cross-provider ownership; invalid authentication rejected |
| AUTH-03 | Cookie, timeout, rotation và logout | Đúng flag; idle ≤7 ngày; absolute ≤30 ngày; logout revoke ≤60 giây |
| AUTH-04 | Export và delete với auth event cũ hơn 10 phút | Bắt buộc managed re-authentication |
| AUTH-05 | Concurrent first sign-in, callback replay và sign-in racing Workspace deletion; mutate IdP registration generation/mode and SHARED grant locator encryption/hash | Exactly one User/UserIdentity/direct-owned Workspace/TradingAccount bootstrap; exact ENABLE registration and mode bind once, SHARED grant locator is encrypted/hash-valid, issuer+subject/owner cardinality holds; after FENCE no session or second tree is created |
| DATA-01 | Kiểm tra traffic, storage, backup và queue | User data mã hóa in transit và at rest |
| DATA-02 | Scan repository, artifact, client bundle và log | Không có production key/secret/token |
| UPL-01 | File quá size/row, sai magic bytes, archive, malformed CSV, decompression/pixel bomb; crash before/after CSV ACCEPT | Bị từ chối có kiểm soát, không exhaustion/partial unsafe commit; invalid CSV tạo zero preview/batch, valid CSV tạo đúng một atomic sanitized preview và zero business row |
| UPL-02 | Re-import cùng CSV/cùng stable rows; retry/concurrent ConfirmImport và multiplicity ACCEPT/MARK | Một preview confirm tạo đúng một ImportBatch/IMPORT chain; không nhân đôi Fill, fee hoặc episode; each StagedFill has one immutable disposition/fate |
| UPL-03 | Screenshot hợp lệ/không hợp lệ và scanner release config, core | Decode/scan/strip metadata; object chỉ active sau validation; scanner pinned self-hosted/stateless has no network or retained/external copy |
| UPL-04 | Conditional voice suite for every `voice_ingest_profile_v1` pair plus wrong MIME/extension, multi-track/stream, AAC/MP3/FLAC, metadata, malformed duration/sample bomb and exact size/duration/sample boundaries | Feature remains off until sandbox/resource tests pass; accepted input yields exact PCM16 mono 16k WAV profile/hash and processor compatibility, rejected input creates no active Attachment/outbound AI call |
| UPL-05 | Retry/concurrently run every Upload/Attachment transition, activation and item delete | Contract version, contiguous sequence, unique source upload, atomic header+ACTIVATE and composite tenant FKs hold; no duplicate/header-only attachment; historical join/hash retained |
| UPL-06 | Every CSV/screenshot/voice accepted, rejected, pre-validation-stalled and validating-stalled branch at natural trigger, CSV preview ABANDON/exact expiry, exact RECEIVE+20h forced purge, five-minute retry bounds, RECEIVE+24h and +1ms; inject delete/verify/atomic-PURGE crash and duplicate delivery | ABANDON/expiry advances existing purge chain without new work type or TTL extension; stalled Upload appends REJECT/`RAW_UPLOAD_RETENTION_DEADLINE`, raw read denies and delete begins by +20h; exact object/replicas prove absent by +24h; proof+lease terminal+PURGE+receipt are one transaction, while breach stays non-PURGED/severity-one rather than fabricating compliance |
| UPL-07 | Crash/replay before and after RESERVE, provider create, RECORD_BYTES, TRANSFER, abort verify and post-transfer inventory for RAW_UPLOAD; repeat every boundary for SCREENSHOT validation and keep-original VOICE intent, including concurrent intent/validator, prepared hash mismatch, stale base, raw forced-purge equality, sanitized 15m/1h equality, reuse capability, extra version visible before TRANSFER or only in the second inventory, cross-Workspace FENCE and shell TTL | RESERVE atomically creates OBJECT_INGEST_FINALIZE; sanitized write is a source/operation-bound prepare saga, never a DB/object atomicity claim; SCREENSHOT ACCEPT or TranscriptConfirmation can consume only its exact BYTES_PRESENT preallocated reservation before both deadlines; retry returns one intent/Attachment or stable failure; replay/extra/mismatch cannot activate; abandoned versions prove absent and activated extras are deleted/verified by 1h through the moved target locator; finalizer reaches marker/deletion handoff before shell purge; no partial revision, orphan, late commit, download or export |
| CSV-01 | Formula, delimiter, quote, newline, tab và Unicode payload | Export không thực thi formula khi mở bằng spreadsheet mục tiêu |
| LOG-01 | Inject token, email, note, transcript và trade value vào error paths | Operational/error log đã redact, không chứa raw value |
| ANA-01 | Product analytics payload có prohibited field, source ref tenant khác và aggregate N<10 | Reject cross-tenant/unknown field; first-party event remains authoritative/exportable only to owner; aggregate value null dưới privacy threshold |
| ANA-02 | Run TP-LAB:G36 for every external event, all three preprojection-suppression reasons, rotation boundary, two processors, 90-day purge and Workspace deletion; compact source/control detail and mutate envelope/pseudonym/locator/key/lookup descriptor/inventory/evidence | Only pinned `product_analytics_external_v1` bytes dispatch; eligible projection plus ANALYTICS_DELIVERY/ANALYTICS_PURGE are atomic, suppressed delivery creates no projection/purge/lease, exact source-day expiry proves absence, encrypted inventory remains executable after compaction, every processor generation is deleted separately, and no raw ID/exact activity time/hidden cron survives |
| AUD-01 | Thực hiện pre-auth invalid/unknown issuer/token/link failures, post-auth auth/import/export/deletion request+FENCE/break-glass và AI opt khi applicable; replay later deletion milestones; mutate scope/null coupling and raw identity fields | PRE_AUTH has no fabricated actor/workspace and only safe config ID/daily attempt HMAC/closed code; POST_AUTH has exact actor-or-system/workspace; only request/FENCE use AuditEvent, later milestones use WorkspaceDeletionStateEvent and never reintroduce raw Workspace/User/domain ID; no token, issuer input, subject, email or content |
| EXP-01 | Request export hợp lệ; core chạy TP-EXP non-AI suite + `G22_ai_absent`, extension chạy thêm mọi AI-present fixture; gồm STANDARD boundary và từng OVERSIZE boundary + 1 | Applicable `tradeproof_export_v1` reference-closed/round-trip đạt; STANDARD READY ≤24 giờ, OVERSIZE được chấp nhận lossless với status/notification đúng contract; link 15 phút, archive xóa ≤24 giờ |
| EXP-02 | Issue download token, then account/item delete while exact object delete is unavailable; inject crash at archive registration, EXPORT terminal, every EXPORT_EXPIRY revoke/delete/verify/EXPIRE boundary and exact 24h edge | Every registered version has its own fenced expiry operation before READY; every post-revoke GET/Range GET is denied; retry never re-enables; exact object absent by `archive_expires_at`, only then EXPIRE/expiry marker terminal, no hidden post-terminal worker or DB/object atomicity assumption |
| DEL-01 | Replay exact `workspace_deletion_v1` flow and mutate every target deadline equality, dependency key/order/cycle, pipeline ID/action ordinal, frozen-inventory schema/hash/ciphertext, target-set hash, event sequence, null and final evidence | Generation increments once; exact frozen DAG/inventory/deadlines hold; PRIMARY cannot start before object/mapping dependencies terminal; DELETE/UNLINK/restore-fence/minimize alone never terminal; primary/local ≤24h, cache/index ≤72h; malformed/incomplete evidence never advances |
| DEL-02 | Check empty/populated local stores, zero/one/many configured processors, analytics generations/AI copy states, both IdP modes with local identity purged before retry, and rolling backup; mutate encrypted IdP issuer/subject/grant/registration and key availability | Empty/NONE targets still run post-drain no-op+verify; external/AI inventories partition every possible copy/token; IdP inventory remains executable after local purge and yields exact subject/link absence evidence; ciphertext clears only after evidence; requests ≤24h, processor/IdP/backup ≤30d; no omitted/fabricated terminal |
| DEL-03 | Crash/duplicate at every registered control type including OBJECT_INGEST_FINALIZE/PRODUCT_MEASUREMENT_TIMEOUT/ANALYTICS_PURGE/EXPORT_EXPIRY/AI_OUTPUT_DELETE, fence/external-operation dispatch, terminal-marker compaction, drain, outbox/action attempt and provider verification; mutate initiator/type/subject/payload/schema version/digest profile/hash/idempotency/key rotation and late result/write | Registry rejects malformed/cross-tenant/version-colliding jobs; no-lease timeout and deterministic provider lookup branches close correctly; exactly one versioned terminal marker per sequence survives subject deletion; drain hashes initiator plus all marker fields, creates target dependencies before delivery, waits all markers, and post-drain pipelines remove late materialization |
| DEL-04 | Race sign-in/delete/restore and re-register; rotate HMAC key, cross tombstone expiry with active successor, then delete again | Pre-complete access rejects; retained predecessor chain prevents active generation reset; missing key/broken chain/old callback rejects; lawful fresh ceremony creates generation+1/new ownership IDs, or generation 1 only after the entire expired inactive chain is purged |
| RET-01 | Chạy retention với raw/object controls, ANALYTICS_PURGE source-day 90-day boundary, normal work controls and completed/incomplete deletion graph at every TTL boundary; attempt to configure a legal-hold bypass | Every provider delete/verify is fenced; child-first cleanup clears locators/IDs on schedule, terminal marker/tombstone exceptions match exact predicates, no dangling FK or joinable identifier remains; scheduler runs at least daily, and v1 rejects any legal-hold bypass/configuration |
| RET-02 | Delete/purge từng subject type, retry và inject outbox delay/conflict | Exact 10-field receipt idempotent; cutoff gap không READY; TP-EXP Tombstone đúng ID/hash/time/policy/fallback, không có deleted content |
| PROC-01 | Review processor trước production | Có DPA, no-training, location/subprocessor, retention và deletion contract |
| AI-00 | Core profile with all three AI flags false; enumerate client routes/UI/server routes/config/credentials/queues/outbound DNS and create/import/review/report/export/delete flows | No AI UI or callable endpoint, active processor registration/credential, AI_RUN/AI_CANCEL enqueue or outbound request exists; every deterministic core flow remains complete |
| AI-01 | For each enabled AI feature, replay `ai_consent_v1`, including same-millisecond GRANT then REVOKE with reverse-sorted IDs, exact per-run AI_CANCEL set and later re-GRANT | Greatest contiguous feature sequence decides REVOKE; no outbound without current GRANT; AiRun pins exact consent/processor; each pre-revoke queued/in-flight run has one mapped cancel job and terminalizes ≤15 minutes without capturing later runs |
| AI-02 | Inspect request theo từng AI feature và mutate từng input ref/digest/fragment hash | Chỉ exact field allowlist; typed key/digest/cardinality/order fail closed; không email, screenshot, raw CSV, token hoặc secret |
| AI-03 | Weekly summary valid/malformed/unmapped/duplicate-claim fixtures | `weekly_summary_v1` canonical JSON only; 100% typed value/sample/quality/claim refs map về pinned report/metric/episode; plain text, numeric lexeme, bad ordering và current-source substitution bị reject |
| AI-04 | Injection/signal/causation eval corpus | 0 critical violation; instruction trong user content không đổi policy |
| AI-05 | Model/prompt/policy/schema thay đổi | Exact `ai_artifact_v1` AiRun/input-reference/AiOutput/output-reference provenance và eval suite pass trước deploy |
| AI-06 | Processor timeout, invalid schema hoặc validation fail | Bounded retry rồi deterministic/manual fallback; không hiển thị partial unsafe output |
| AI-07 | Transcript/taxonomy output before confirmation, stale base/output, retry and wrong tenant/type/item | No structured write before confirmation; exact immutable confirmation atomically creates one validated next revision/optional retained attachment; stale/malformed command has zero effect |
| AI-08 | For success/failure/reject/cancel, ZERO_RETENTION and PROCESSOR_MAX_30_DAY references: crash around terminal run/copy evidence/marker and exact deadline; delete each output kind, race Workspace FENCE, compact controls, export before/after, mutate nested terminal evidence/inventory | AI_RUN marker exists only after BOUND_OUTPUT or independently verifiable terminal copy evidence; no-output uses the same delayed AI_RUN item, no hidden sweep; encrypted handle never logs/exports and clears only with evidence; output delete reaches marker or handoff, processor ≤30d, no late commit; confirmed canonical data unchanged |
| AI-09 | Golden fixture cho raw/retained audio, taxonomy text/version/items, weekly refs; mutate schema ID/fragment field/hash và đọc v1 sau khi registry có v2 | Exact input/fragment hash và typed-key closure replay ổn định theo persisted basis ID; taxonomy output có version + từng item ref; old-run/new-reader pass, sai offset/key/type/workspace/schema/fragment bị reject |
| AI-10 | Reuse config/processor generation with changed bytes, mutate artifact after enqueue, same-time activation events, ENABLE/RETIRE/RETENTION_CLOSED transitions, referenced closed registration and deploy without passing eval/capability proof; mutate copy watermark/gap/order/usage-marker hash, RETIRE link, backup-window bound, provider closure receipt and closure-evidence hash | Registries reject overwrite/reuse; run/copy atomically allocate one sequence under an ENABLE processor and exact release/hash; RETENTION_CLOSED has contiguous terminal coverage through the locked last-copy watermark plus immutable post-backup provider absence evidence; frozen registry serializes every referenced generation including closed-state anomaly; latest sequence decides enable; no enqueue without passing eval, locator API, no-training and retention evidence |
| AI-11 | Confirm transcript with edit/keep-original branches and taxonomy EXIT/BREACH/EMOTION; then delete source output and export before/after | Text/item hashes, target mapping, idempotency and result revision refs exact; raw deadline never extends; confirmation resolves active output before delete and exact Tombstone after, while canonical confirmed revision is unchanged |
| INC-01 | Tabletop account compromise/cross-tenant incident | Revoke/block trong 15 phút sau xác nhận; audit và communication flow hoạt động |
| ABU-01 | Vượt rate limit/quota | Server chặn có kiểm soát, không ảnh hưởng tenant khác |

Release không được waive TEN, AUTH, DEL hoặc AI critical gate. Ngoại lệ ở mục khác phải có risk owner, phạm vi, expiry và remediation date.

## 12. Non-goals của MVP

Các nội dung sau không thuộc MVP:

- Exchange API connector, read-only sync hoặc lưu exchange API secret.
- Tự triển khai password authentication, password recovery hoặc MFA engine; managed provider có thể cung cấp MFA.
- Nhiều Workspace cho một User, nhiều TradingAccount cho một Workspace, team member, coach access, sharing hoặc ownership transfer.
- Public profile, social feed, public attachment hoặc anonymous upload.
- AI đọc screenshot, raw CSV toàn bộ Workspace hoặc dữ liệu từ tenant khác.
- AI tính financial metric, reconcile, dự báo, phát tín hiệu, chọn leverage/position size hoặc thực hiện giao dịch.
- Tool calling, browsing, URL fetch, database access hoặc autonomous action cho AI.
- Customer-managed encryption key, end-to-end encryption hoặc offline mode.
- Lưu production data vô thời hạn, dùng production data để train model hoặc tạo benchmark không có consent riêng.
- Chứng nhận tuân thủ hoặc tuyên bố pháp lý chưa được đánh giá độc lập.

Mọi yêu cầu vượt non-goals phải qua threat modeling, privacy review, data-flow update và acceptance criteria mới trước khi được đưa vào phạm vi.

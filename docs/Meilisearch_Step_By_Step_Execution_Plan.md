# Kế Hoạch & Hướng Dẫn Thực Hiện Chi Tiết Meilisearch (PropNest)

Tài liệu này hệ thống hóa toàn bộ quy trình thực hiện theo từng bước nhỏ (micro-steps), đồng thời hướng dẫn chi tiết cách thức triển khai code (class, interface, luồng xử lý và vị trí file) để bạn dễ dàng theo dõi và giám sát quá trình phát triển.

---

## I. Mục Tiêu & Nguyên Tắc Kiến Trúc

1. **Mục tiêu chính:**
   - Thay thế câu lệnh tìm kiếm `Contains()` / `LIKE` trên SQL Server bằng **Meilisearch** cho trang tìm kiếm Client (`Web.Client`), giảm độ trễ truy vấn xuống **< 30ms**.
   - Hỗ trợ tiếng Việt không dấu/có dấu (đặc biệt xử lý triệt để ký tự `đ/Đ`), hỗ trợ tìm kiếm chéo địa giới hành chính Việt Nam (trước và sau sáp nhập).
   - Đảm bảo **đồng bộ chính xác 100% (Zero-Drift)** qua mọi trạng thái vòng đời tin đăng.

2. **Nguyên tắc cốt lõi:**
   - **CQRS:** SQL Server là **Source of Truth** duy nhất cho mọi nghiệp vụ ghi (Write/Mutation). Meilisearch chỉ đóng vai trò **Search Projection** (Read-Only cho Client).
   - **Safe Scope:** Index `listings` trên Meilisearch **chỉ lưu tin đang hiển thị công khai** (`Status == Published && ModerationDecision == Approved && EndDate > now`). Mọi trạng thái khác (`Draft`, `PendingModeration`, `PendingPayment`, `Rejected`, `Hidden`, `Expired`, `Deleted`) tuyệt đối không tồn tại trong index.
   - **Fetch-Latest-State:** Khi đồng bộ qua RabbitMQ, Consumer chỉ nhận tín hiệu `ListingId`, sau đó truy vấn trực tiếp trạng thái mới nhất từ SQL Server trước khi Upsert/Delete vào Meilisearch để loại bỏ hoàn toàn nguy cơ ghi đè dữ liệu cũ (stale data).
   - **Lưới an toàn (Fallback):** Khi Meilisearch container bị tắt hoặc timeout (> 2s), API tự động chuyển về truy vấn SQL Server cũ, đảm bảo người dùng không bao giờ gặp lỗi `500`.

---

## II. Quy Trình Thực Hiện Chi Tiết (5 Giai Đoạn)

```
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 1: Hạ Tầng, Cấu Hình & Khởi Tạo Index                            │
│ ├─ B1.1: Docker Compose Service & Volume                               │
│ ├─ B1.2: Cấu hình Options & NuGet Package                              │
│ └─ B1.3: Initializer đồng bộ Settings, Synonyms & Ranking Rules         │
├────────────────────────────────────────────────────────────────────────┤
│ Phase 2: Document Schema, Normalizer & Query Engine                     │
│ ├─ B2.1: ListingSearchDocument & Trích xuất Plain Text                 │
│ ├─ B2.2: VietnameseSearchNormalizer (chuẩn hóa đ/Đ & dấu tiếng Việt)   │
│ ├─ B2.3: Search Service, Query Builder & Hard Expiration Filter        │
│ └─ B2.4: Bộ ngắt mạch an toàn (SQL Server Fallback Mechanism)          │
├────────────────────────────────────────────────────────────────────────┤
│ Phase 3: Cơ Chế Đồng Bộ Sự Kiện (Outbox + Worker Sync)                 │
│ ├─ B3.1: Bổ sung Outbox Events trên toàn bộ điểm thay đổi tin đăng    │
│ ├─ B3.2: Cập nhật Outbox Dispatcher & MassTransit Publisher            │
│ ├─ B3.3: SyncListingSearchConsumer (Fetch-Latest-State + Partition)     │
│ ├─ B3.4: Tinh chỉnh Quartz Expiration Job (00:00:05 +07:00 & 1m sweep) │
│ └─ B3.5: Scheduled Reconciliation Job (Tự động đối soát & chữa lành)   │
├────────────────────────────────────────────────────────────────────────┤
│ Phase 4: Giao Diện Client & Bộ Lọc Đa Chiều                            │
│ ├─ B4.1: Mở rộng DTO trả về (ThumbnailUrl thật, mã địa giới, hạn gói)  │
│ ├─ B4.2: Tích hợp API Controller & Search Pipeline                     │
│ └─ B4.3: UI Client (Combobox địa chỉ, bộ lọc thông số & sắp xếp)       │
├────────────────────────────────────────────────────────────────────────┤
│ Phase 5: Kiểm Thử Đơn Vị & Kịch Bản Nghiệm Thu                         │
│ ├─ B5.1: Unit Test (Normalizer đ/Đ, Document Mapper, Filter Builder)   │
│ ├─ B5.2: Kiểm chứng 3 kịch bản vòng đời (Hết hạn, Sửa/Ẩn/Ảnh, VIP)     │
│ └─ B5.3: Kiểm chứng khả năng chịu lỗi (Fallback Test)                  │
└────────────────────────────────────────────────────────────────────────┘
```

---

### Giai Đoạn 1: Hạ Tầng, Cấu Hình & Khởi Tạo Index (Infrastructure)

#### Bước 1.1: Thiết lập Container Meilisearch trong Docker Compose
* **Mục tiêu:** Chạy Meilisearch v1.11 trong cùng Docker network với API và Worker.
* **Cách thức thực hiện code:**
  * File tác động: `docker-compose.yml`.
  * Khai báo service `meilisearch` (image `getmeili/meilisearch:v1.11`), gắn volume `meilisearch_data:/meili_data`, port `7700:7700`, đặt `MEILI_MASTER_KEY` và healthcheck bằng lệnh `wget`.
  * Thêm biến môi trường `Meilisearch__Url`, `Meilisearch__MasterKey`, `Meilisearch__ListingsIndexName` vào cả 2 service `api` và `worker`.

#### Bước 1.2: Đăng ký NuGet Package & Model Cấu hình
* **Mục tiêu:** Cung cấp SDK Meilisearch và inject cấu hình dạng `IOptions<MeilisearchOptions>`.
* **Cách thức thực hiện code:**
  * File tác động:
    * `Directory.Packages.props`: Thêm package `Meilisearch` phiên bản `0.16.0`.
    * `src/PropNest.Infrastructure/PropNest.Infrastructure.csproj`: Thêm reference `<PackageReference Include="Meilisearch" />`.
    * `src/PropNest.Application/Abstractions/MeilisearchOptions.cs`: Tạo class chứa các thuộc tính `Url`, `MasterKey`, `ListingsIndexName`, `Enabled`, `RequestTimeoutMs`, `ReconciliationIntervalMinutes`.
    * `appsettings.json` (Api & Worker): Bổ sung section `"Meilisearch": { ... }`.

#### Bước 1.3: Xây dựng Meilisearch Index Initializer
* **Mục tiêu:** Tự động tạo index `listings` (nếu chưa có) và cập nhật cấu hình tìm kiếm khi API/Worker khởi động.
* **Cách thức thực hiện code:**
  * File tác động: `src/PropNest.Infrastructure/Services/Meilisearch/MeilisearchIndexInitializer.cs`.
  * Triển khai `IHostedService` hoặc extension method trong Dependency Injection:
    1. Khởi tạo `MeilisearchClient` với URL và MasterKey.
    2. Kiểm tra/Tạo index `listings` với khóa chính `listingId`.
    3. Gửi lệnh cập nhật `Settings`:
       * `SearchableAttributes`: `["title", "address", "ward", "district", "city", "legacyAddress", "searchNormalizedText", "descriptionPlainText"]`.
       * `FilterableAttributes`: `["listingId", "status", "moderationDecision", "listingType", "propertyType", "price", "area", "floors", "bedrooms", "bathrooms", "balconyDirection", "administrativeMode", "provinceCode", "districtCode", "wardCode", "city", "district", "ward", "locationTokens", "packageCode", "isVip", "endDateUnix", "packageEndDateUnix"]`.
       * `SortableAttributes`: `["packageTierWeight", "rankTimestampUnix", "price", "area", "createdAtUnix"]`.
       * `RankingRules`: `["words", "typo", "proximity", "attribute", "sort", "exactness"]`.
       * `Synonyms`: Đồng nghĩa nhà đất (`chung cu` <-> `can ho`, `nha rieng` <-> `nha pho`, `tphcm` <-> `sai gon` <-> `ho chi minh`, `wc` <-> `toilet`, `pn` <-> `phong ngu`).
       * `TypoTolerance`: Cho phép 1 lỗi chính tả với từ >= 4 ký tự, 2 lỗi với từ >= 8 ký tự.

---

### Giai Đoạn 2: Document Schema, Normalizer & Query Engine

#### Bước 2.1: Định nghĩa Document Schema & Mapper
* **Mục tiêu:** Chuyển đổi entity `Listing` sang cấu trúc JSON phẳng tối ưu cho Meilisearch.
* **Cách thức thực hiện code:**
  * File tạo mới: `src/PropNest.Application/Listings/Search/ListingSearchDocument.cs`.
  * Định nghĩa document model với:
    * Khóa chính `ListingId` (`int`).
    * Thông số số nguyên Unix timestamp (`endDateUnix`, `packageEndDateUnix`, `rankTimestampUnix`, `createdAtUnix`).
    * Trọng số gói `packageTierWeight` (VIP = 200, Standard = 100).
    * `descriptionPlainText`: Dùng `RichTextHtmlHelper.ToPlainTextPreview(description)` để loại bỏ các tag HTML (`<p>`, `<strong>`, `<ol>`), tránh việc match nhầm từ khóa HTML.
    * `locationTokens`: Mảng chứa tên Tỉnh, Huyện, Xã hiện tại + Địa chỉ cũ để search linh hoạt.
    * `thumbnailUrl`: Link ảnh bìa thực tế (`IsCover == true`).

#### Bước 2.2: Xây dựng Bộ Chuẩn Hóa Tiếng Việt (VietnameseSearchNormalizer)
* **Mục tiêu:** Xử lý triệt để ký tự `đ/Đ` và dấu thanh tiếng Việt để người dùng gõ có dấu hay không dấu đều tìm thấy.
* **Cách thức thực hiện code:**
  * File tạo mới: `src/PropNest.Application/Common/VietnameseSearchNormalizer.cs`.
  * Logic chuẩn hóa:
    1. Thay thế `đ` -> `d`, `Đ` -> `D`.
    2. Chuyển chuỗi sang dạng Unicode chuẩn hóa `NormalizationForm.FormD` (tách ký tự gốc và dấu thanh rời rạc).
    3. Dùng Regex hoặc duyệt ký tự loại bỏ toàn bộ ký tự thuộc nhóm `UnicodeCategory.NonSpacingMark`.
    4. Trả về chuỗi không dấu dạng chữ thường `NormalizationForm.FormC`.
  * Kết hợp chuỗi này vào trường `searchNormalizedText` trong Document.

#### Bước 2.3: Xây dựng Service Tìm Kiếm & Query Builder
* **Mục tiêu:** Nhận truy vấn từ `GetPublicListingsQuery`, chuyển thành câu truy vấn Meilisearch với đầy đủ bộ lọc an toàn.
* **Cách thức thực hiện code:**
  * Interface: `src/PropNest.Application/Listings/Search/IMeilisearchListingSearchService.cs`.
  * Implementation: `src/PropNest.Infrastructure/Services/Meilisearch/MeilisearchListingSearchService.cs`.
  * Logic tạo filter:
    * **Luôn luôn thêm bộ lọc an toàn:** `status = "Published" AND moderationDecision = "Approved" AND endDateUnix > {nowUnix}`.
    * Nếu có `minPrice` / `maxPrice` -> ghép `price >= X AND price <= Y`.
    * Nếu có `provinceCode` / `districtCode` / `wardCode` -> ghép lọc số nguyên tương ứng.
    * Nếu có `bedrooms` / `bathrooms` -> ghép `bedrooms >= N`.
  * Logic Sort:
    * Mặc định: `sort = ["packageTierWeight:desc", "rankTimestampUnix:desc"]` (Tin VIP luôn nổi lên trước, trong cùng VIP tin mới kích hoạt hoặc vừa Đẩy tin sẽ đứng đầu).
    * Tùy chọn khác: `price:asc`, `price:desc`, `area:desc`, `createdAtUnix:desc`.

#### Bước 2.4: Xây dựng Cơ Chế Fallback Về SQL Server
* **Mục tiêu:** Bảo vệ hệ thống khi Meilisearch gặp trục trặc, tự động chuyển về câu lệnh SQL cũ.
* **Cách thức thực hiện code:**
  * File tác động: `src/PropNest.Infrastructure/Services/Meilisearch/MeilisearchListingSearchService.cs`.
  * Sử dụng `try-catch` kèm `CancellationToken` giới hạn thời gian (2000ms):
    * Khi Meilisearch trả về kết quả -> map sang `PagedResult<ListingSummaryDto>`.
    * Nếu bắt được `MeilisearchApiException`, `HttpRequestException`, hoặc `TaskCanceledException` -> ghi log Warning: `[WRN] Meilisearch search failed or timed out. Falling back to SQL Server query.` -> gọi `_sqlListingRepository.GetPublicListingsAsync(query, ct)`.

---

### Giai Đoạn 3: Cơ Chế Đồng Bộ Sự Kiện (Realtime Outbox Sync)

#### Bước 3.1: Kiểm Tra & Bổ Sung Outbox Event Trên Toàn Bộ Điểm Thay Đổi Tin Đăng
* **Mục tiêu:** Mọi thao tác làm thay đổi nội dung, trạng thái, ảnh hoặc gói của tin đăng đều phải phát sinh Outbox Message trong cùng Transaction với bảng `Listings`.
* **Cách thức thực hiện code:**
  * File tác động:
    * `src/PropNest.Application/Listings/ListingService.cs`:
      * `UpdateAsync`: Khi sửa nội dung tin đang chạy, tin tụt về `PendingModeration` -> bắn Outbox `"ListingSearchSyncRequested"`.
      * `HideAsync`: Seller ẩn tin -> bắn Outbox `"ListingHidden"`.
      * `UnhideAsync`: Seller mở lại tin -> bắn Outbox `"ListingRestored"`.
      * `DeleteAsync`: Xóa tin -> bắn Outbox `"ListingDeleted"`.
    * `src/PropNest.Application/Listings/ListingImageService.cs`:
      * Khi upload ảnh mới, xóa ảnh hoặc đổi ảnh bìa (`IsCover`) của tin đang `Published` -> bắn Outbox `"ListingMediaUpdated"`.
    * `src/PropNest.Application/Listings/ListingExpirationService.cs`:
      * `DowngradeExpiredVipPackagesAsync`: Khi hạ gói VIP hết hạn về Standard -> bắn Outbox `"ListingPackageDowngraded"`.
    * `src/PropNest.Infrastructure/Consumers/UpgradeListingConsumer.cs`:
      * Khi thanh toán thành công mua gói/nâng cấp VIP -> bắn Outbox `"ListingPackageUpdated"`.

#### Bước 3.2: Điều Hướng Sự Kiện trong Outbox Dispatcher
* **Mục tiêu:** Đảm bảo `OutboxDispatcherWorker` quét nhanh và đẩy các sự kiện đồng bộ tìm kiếm sang RabbitMQ.
* **Cách thức thực hiện code:**
  * File tác động: `src/PropNest.Infrastructure/Services/MassTransitOutboxPublisher.cs` & `OutboxDispatchService.cs`.
  * Bổ sung mapping: gom toàn bộ các event trên thành command `ListingSearchSyncRequested(ListingId, EventType, OccurredOn)` gửi vào hàng đợi của Worker.
  * Giảm thời gian lặp của `OutboxDispatcherWorker` xuống `1s` khi có tin chờ, giữ xử lý mượt mà.

#### Bước 3.3: Xây dựng SyncListingSearchConsumer với Nguyên Tắc Fetch-Latest-State
* **Mục tiêu:** Nhận message từ RabbitMQ và cập nhật document lên Meilisearch mà không sợ xung đột race condition.
* **Cách thức thực hiện code:**
  * File tạo mới: `src/PropNest.Workflow.Worker/Consumers/SyncListingSearchConsumer.cs`.
  * Luồng xử lý:
    1. Nhận message với `ListingId`.
    2. **Không dùng dữ liệu trong payload**, tiến hành query entity `Listing` mới nhất từ `PropNestDbContext` (kèm `Images`).
    3. Đánh giá điều kiện:
       * Nếu `listing != null && listing.Status == ListingStatus.Published && listing.ModerationDecision == ModerationDecision.Approved && listing.EndDate > DateTimeOffset.UtcNow`:
         -> Map sang `ListingSearchDocument` và gọi `index.AddDocumentsAsync(new[] { document })` (Upsert).
       * Ngược lại (tin bị ẩn, bị xóa, đang chờ duyệt lại, hoặc đã hết hạn):
         -> Gọi `index.DeleteOneDocumentAsync(listingId.ToString())` (Delete).
  * Cấu hình hàng đợi trong `Program.cs` của Worker: Dùng MassTransit `Partition(16, m => m.Message.ListingId)` để các sự kiện của cùng 1 tin đăng luôn được xử lý tuần tự, loại bỏ hoàn toàn khả năng thread A ghi đè sau thread B.

#### Bước 3.4: Tinh Chỉnh Quartz Job Cho Hết Hạn & Hạ Gói VIP
* **Mục tiêu:** Quét dọn chính xác và gửi tín hiệu đồng bộ vào thời khắc chuyển giao ngày mới.
* **Cách thức thực hiện code:**
  * File tác động: `src/PropNest.Workflow.Worker/Program.cs` & `ListingExpirationJob.cs`.
  * Cấu hình 2 Trigger cho `ListingExpirationJob`:
    1. Trigger định kỳ mỗi **1 phút** (`0 * * * * ?`) để quét dọn liên tục.
    2. Trigger chính xác vào **00:00:05 (giờ Việt Nam - Asia/Bangkok)** mỗi ngày: vừa qua nửa đêm 5 giây là thực hiện hạ gói VIP (`DowngradeExpiredVipPackagesAsync`) và hết hạn tin (`ExpireListingsAsync`), tự động kích hoạt Outbox để Meilisearch cập nhật ngay.

#### Bước 3.5: Xây dựng Reconciliation Job (Tự Chữa Lành Định Kỳ)
* **Mục tiêu:** Phát hiện và sửa chữa mọi sai lệch dữ liệu giữa SQL Server và Meilisearch (nếu có sự cố mạng hoặc restart).
* **Cách thức thực hiện code:**
  * File tạo mới: `src/PropNest.Workflow.Worker/Jobs/SearchReconciliationJob.cs`.
  * Cấu hình Quartz chạy mỗi 15 phút và chạy 1 lần khi Worker vừa khởi động:
    1. Đọc toàn bộ danh sách `(ListingId, UpdatedAt, PackageCode, EndDate)` của các tin đang `Published` từ SQL.
    2. Đọc toàn bộ document IDs từ Meilisearch.
    3. Xóa các ID có trên Meilisearch nhưng không còn `Published` trên SQL.
    4. Upsert bổ sung các tin còn thiếu hoặc có `updatedAt` bị lệch.

---

### Giai Đoạn 4: Giao Diện Client & Bộ Lọc Đa Chiều

#### Bước 4.1: Mở rộng DTO Trả Về cho Client API
* **Mục tiêu:** Bổ sung ảnh đại diện thực tế và các trường địa giới vào kết quả tìm kiếm.
* **Cách thức thực hiện code:**
  * File tác động: `src/PropNest.Application/Listings/ListingContracts.cs` (`ListingSummaryDto`) & `web/PropNest.Web.Client/Models/ViewModels/ApiListingSummaryDto.cs`.
  * Bổ sung các property: `string? ThumbnailUrl`, `int? ProvinceCode`, `int? DistrictCode`, `int? WardCode`, `DateTimeOffset? PackageEndDate`, `DateTimeOffset? EndDate`.

#### Bước 4.2: Tích Hợp API Endpoint Tìm Kiếm Client
* **Mục tiêu:** Điều hướng endpoint `GET /api/v1/listings` sang dùng `IMeilisearchListingSearchService`.
* **Cách thức thực hiện code:**
  * File tác động: `src/PropNest.Api/Controllers/ListingsController.cs`.
  * Trong action `GetListings([FromQuery] GetPublicListingsQuery query, CancellationToken ct)`:
    * Gọi qua `_searchService.SearchListingsAsync(query, ct)`.

#### Bước 4.3: Nâng Cấp Giao Diện Bộ Lọc Phía Web.Client
* **Mục tiêu:** Cung cấp trải nghiệm tìm kiếm tiện lợi cho người dùng mua/thuê nhà.
* **Cách thức thực hiện code:**
  * File tác động:
    * `web/PropNest.Web.Client/Views/Listings/Index.cshtml` & `Views/Home/Index.cshtml`.
    * `web/PropNest.Web.Client/Services/ApiClientListingServices.cs`.
  * Cải tiến UI:
    * Thay ô text nhập tên tỉnh/thành bằng Combobox địa chỉ (gọi API `/api/v1/locations/provinces`, `/districts`, `/wards` sẵn có).
    * Bổ sung các nút bấm nhanh: lọc theo mức giá (Dưới 2 tỷ, 2-5 tỷ, 5-10 tỷ...), số phòng ngủ (1+, 2+, 3+), hướng ban công, và combobox chọn sắp xếp (Tin VIP/Mới nhất, Giá thấp -> cao, Diện tích lớn -> nhỏ).
    * Card hiển thị tin: Hiển thị `item.ThumbnailUrl` thật của tin, nếu null mới fallback về ảnh mặc định.

---

### Giai Đoạn 5: Kiểm Thử Đơn Vị & Kịch Bản Nghiệm Thu

#### Bước 5.1: Viết Unit Tests Cho Các Khối Logic Mới
* **Mục tiêu:** Đảm bảo 100% logic chuẩn hóa và mapping hoạt động chính xác trước khi đưa lên môi trường thật.
* **Cách thức thực hiện code:**
  * Thư mục tác động: `tests/PropNest.Domain.Tests/`.
  * Viết các test case:
    * `VietnameseSearchNormalizerTests`: Test các chuỗi `"Đống Đa"` -> `"dong da"`, `"Đà Nẵng"` -> `"da nang"`, `"đường"` -> `"duong"`.
    * `ListingSearchDocumentMapperTests`: Test việc bóc tách HTML từ mô tả, tính toán đúng `rankTimestampUnix` và `packageTierWeight`.
    * `MeilisearchFilterBuilderTests`: Test sinh đúng chuỗi filter an toàn `endDateUnix > X` và kết hợp các điều kiện lọc số.

#### Bước 5.2: Kiểm Chứng 3 Kịch Bản Nghiệm Thu Thực Tế (E2E Test)
* **Kịch bản A (Hết hạn tin):**
  * Tạo tin có `EndDate` hết hạn lúc `23:59:59`.
  * Tại `00:00:01` (ngay giây đầu tiên ngày mới, trước khi Worker chạy job): Kiểm tra Client search -> Xác nhận tin đã tự động biến mất và `totalCount` giảm 1 nhờ `endDateUnix > nowUnix`.
  * Tại `00:00:06` (sau khi Job chạy): Kiểm tra index Meilisearch -> Xác nhận document đã bị xóa hoàn toàn.
* **Kịch bản B (Sửa / Ẩn / Đổi ảnh bìa):**
  * Seller nhấn sửa nội dung -> Tin chuyển sang `PendingModeration` -> Kiểm tra Meilisearch xác nhận tin tạm biến mất ngay.
  * Admin duyệt lại -> Tin xuất hiện lại với tiêu đề và giá mới.
  * Seller bấm Ẩn -> Tin biến mất; Bấm Hiện lại -> Tin xuất hiện lại.
  * Đổi ảnh bìa -> Kết quả search hiển thị ảnh bìa mới ngay.
* **Kịch bản C (Hết hạn gói VIP & Tương lai Đẩy tin):**
  * Tin VIP hết hạn gói vào nửa đêm -> Badge tự chuyển về Standard, thứ hạng tự tụt sau các tin VIP còn hạn.
  * Thiết kế sẵn trường `rankTimestampUnix` để khi triển khai tính năng Đẩy tin (Bump), chỉ cần update `LastBumpedAt = now` là tin tự động nhảy lên đầu nhóm.

#### Bước 5.3: Kiểm Chứng Tính Năng Dự Phòng (Fallback Verification)
* Dùng lệnh `docker stop propnest-meilisearch` để tắt container Meilisearch.
* Tải lại trang `https://localhost:7004/Listings` (Client).
* Kiểm tra log API: Xác nhận có log `[WRN] Meilisearch search failed... Falling back to SQL Server query`.
* Kiểm tra UI Client: Trang vẫn hiển thị danh sách tin từ SQL Server bình thường, không xảy ra lỗi trắng trang.

---

## III. Bảng Tổng Hợp Các File Tác Động Theo Thứ Tự Triển Khai

| Thứ tự | File đường dẫn | Vai trò chính |
|---|---|---|
| 1 | `docker-compose.yml` | Khai báo container Meilisearch v1.11 |
| 2 | `Directory.Packages.props` & `PropNest.Infrastructure.csproj` | Khai báo NuGet package `Meilisearch` |
| 3 | `src/PropNest.Application/Abstractions/MeilisearchOptions.cs` | Định nghĩa model cấu hình Options |
| 4 | `src/PropNest.Application/Common/VietnameseSearchNormalizer.cs` | Xử lý chữ `đ/Đ` và dấu tiếng Việt |
| 5 | `src/PropNest.Application/Listings/Search/ListingSearchDocument.cs` | Model JSON Document Meilisearch |
| 6 | `src/PropNest.Infrastructure/Services/Meilisearch/MeilisearchIndexInitializer.cs` | Khởi tạo index, filter, sort, synonym |
| 7 | `src/PropNest.Infrastructure/Services/Meilisearch/MeilisearchListingSearchService.cs` | Query builder, Search execution & Fallback |
| 8 | `src/PropNest.Application/Listings/ListingService.cs` | Bổ sung Outbox event khi Update, Hide, Unhide, Delete |
| 9 | `src/PropNest.Application/Listings/ListingImageService.cs` | Bổ sung Outbox event khi đổi/xóa ảnh bìa |
| 10 | `src/PropNest.Application/Listings/ListingExpirationService.cs` | Bổ sung Outbox event khi hạ VIP |
| 11 | `src/PropNest.Infrastructure/Services/MassTransitOutboxPublisher.cs` | Định tuyến các search sync event sang RabbitMQ |
| 12 | `src/PropNest.Workflow.Worker/Consumers/SyncListingSearchConsumer.cs` | Consumer Fetch-Latest-State upsert/delete Meilisearch |
| 13 | `src/PropNest.Workflow.Worker/Jobs/SearchReconciliationJob.cs` | Quartz job đối soát và chữa lành dữ liệu |
| 14 | `src/PropNest.Workflow.Worker/Program.cs` | Cấu hình lịch Quartz chạy lúc 00:00:05 +07:00 |
| 15 | `src/PropNest.Api/Controllers/ListingsController.cs` | Điều hướng Client API sang Meilisearch service |
| 16 | `web/PropNest.Web.Client/Views/Listings/Index.cshtml` | Nâng cấp UI Combobox địa giới & bộ lọc thông số |
| 17 | `tests/PropNest.Domain.Tests/` | Unit tests kiểm thử bộ chuẩn hóa, mapper và filter |

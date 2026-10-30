import os

def generate_clean_svg():
    svg_content = """<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1100 1320" width="1100" height="1320">
  <defs>
    <!-- Arrow Markers -->
    <marker id="arrow-down" viewBox="0 0 10 10" refX="5" refY="9" markerWidth="6" markerHeight="6" orient="auto">
      <path d="M 1 1 L 5 9 L 9 1 z" fill="#2563EB"/>
    </marker>
    <marker id="arrow-up" viewBox="0 0 10 10" refX="5" refY="1" markerWidth="6" markerHeight="6" orient="auto">
      <path d="M 1 9 L 5 1 L 9 9 z" fill="#059669"/>
    </marker>
    <marker id="arrow-right-blue" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto">
      <path d="M 1 1 L 9 5 L 1 9 z" fill="#2563EB"/>
    </marker>
    <marker id="arrow-right-red" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto">
      <path d="M 1 1 L 9 5 L 1 9 z" fill="#DC2626"/>
    </marker>
    <marker id="arrow-left-green" viewBox="0 0 10 10" refX="1" refY="5" markerWidth="6" markerHeight="6" orient="auto">
      <path d="M 9 1 L 1 5 L 9 9 z" fill="#059669"/>
    </marker>

    <!-- Subtle Drop Shadow -->
    <filter id="shadow" x="-2%" y="-2%" width="104%" height="106%" filterUnits="userSpaceOnUse">
      <feDropShadow dx="0" dy="2" stdDeviation="2.5" flood-color="#0F172A" flood-opacity="0.08"/>
    </filter>
  </defs>

  <!-- Background -->
  <rect width="1100" height="1320" fill="#FFFFFF"/>

  <!-- ========================================================================= -->
  <!-- HEADER -->
  <!-- ========================================================================= -->
  <rect x="0" y="0" width="1100" height="60" fill="#0F172A"/>
  <rect x="0" y="58" width="1100" height="2" fill="#2563EB"/>
  <text x="550" y="28" font-family="Segoe UI, Arial, sans-serif" font-size="16" font-weight="bold" fill="#FFFFFF" text-anchor="middle" letter-spacing="0.5">SƠ ĐỒ LUỒNG XỬ LÝ MỘT HTTP REQUEST TRONG ASP.NET CORE 8</text>
  <text x="550" y="47" font-family="Segoe UI, Arial, sans-serif" font-size="11.5" fill="#94A3B8" text-anchor="middle">Hành trình chi tiết qua các tầng: Client → Kestrel → Middleware Pipeline → Controller → Service → EF Core → SQL Server</text>

  <!-- ========================================================================= -->
  <!-- BƯỚC 1: CLIENT -->
  <!-- ========================================================================= -->
  <g transform="translate(180, 80)" filter="url(#shadow)">
    <rect width="740" height="75" rx="8" fill="#F8FAFC" stroke="#6366F1" stroke-width="1.8"/>
    <rect width="740" height="28" rx="8" fill="#6366F1"/>
    <rect y="20" width="740" height="8" fill="#6366F1"/>
    <text x="20" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="12.5" font-weight="bold" fill="#FFFFFF">BƯỚC 1: CLIENT / NGƯỜI DÙNG (SELLER)</text>
    <text x="720" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#E0E7FF" text-anchor="end">Trình duyệt / Ứng dụng di động / Postman</text>
    
    <text x="25" y="48" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#1E293B">• Hành động gửi đi:</text>
    <text x="160" y="48" font-family="Segoe UI, Arial, sans-serif" font-size="11" fill="#4338CA">Gửi HTTP Request POST /api/v1/listings kèm Bearer Token (JWT), X-Correlation-Id và JSON dữ liệu tin đăng.</text>
    <text x="25" y="65" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#1E293B">• Hành động nhận về:</text>
    <text x="160" y="65" font-family="Segoe UI, Arial, sans-serif" font-size="11" fill="#065F46">Nhận HTTP Response 201 Created kèm Header Location (/api/v1/listings/1024) và JSON chi tiết tin vừa tạo.</text>
  </g>

  <!-- Mũi tên 1 -> 2 (Hai chiều Inbound & Outbound) -->
  <line x1="530" y1="155" x2="530" y2="185" stroke="#2563EB" stroke-width="2.5" marker-end="url(#arrow-down)"/>
  <line x1="570" y1="185" x2="570" y2="155" stroke="#059669" stroke-width="2" stroke-dasharray="4,3" marker-end="url(#arrow-up)"/>
  <text x="475" y="174" font-family="Segoe UI, Arial, sans-serif" font-size="9" font-weight="bold" fill="#2563EB">Request (In)</text>
  <text x="625" y="174" font-family="Segoe UI, Arial, sans-serif" font-size="9" font-weight="bold" fill="#059669">Response (Out)</text>

  <!-- ========================================================================= -->
  <!-- BƯỚC 2: KESTREL WEB SERVER -->
  <!-- ========================================================================= -->
  <g transform="translate(180, 185)" filter="url(#shadow)">
    <rect width="740" height="80" rx="8" fill="#F8FAFC" stroke="#475569" stroke-width="1.8"/>
    <rect width="740" height="28" rx="8" fill="#475569"/>
    <rect y="20" width="740" height="8" fill="#475569"/>
    <text x="20" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="12.5" font-weight="bold" fill="#FFFFFF">BƯỚC 2: KESTREL WEB SERVER (TẦNG MÁY CHỦ MẠNG)</text>
    <text x="720" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#E2E8F0" text-anchor="end">Socket I/O Bất đồng bộ</text>

    <text x="25" y="48" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#0F172A">1. Tiếp nhận kết nối mạng:</text>
    <text x="175" y="48" font-family="Segoe UI, Arial, sans-serif" font-size="11" fill="#334155">Lắng nghe Socket qua IOCP/epoll, thực hiện bắt tay bảo mật TLS/HTTPS và đọc luồng byte nhị phân.</text>
    
    <text x="25" y="66" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#0F172A">2. Khởi tạo HttpContext:</text>
    <text x="175" y="66" font-family="Segoe UI, Arial, sans-serif" font-size="11" fill="#1E40AF">Tạo thể hiện HttpContext đại diện cho phiên làm việc (gồm Request, Response, CancellationToken, User trống).</text>
  </g>

  <!-- Mũi tên 2 -> 3 -->
  <line x1="530" y1="265" x2="530" y2="295" stroke="#2563EB" stroke-width="2.5" marker-end="url(#arrow-down)"/>
  <line x1="570" y1="295" x2="570" y2="265" stroke="#059669" stroke-width="2" stroke-dasharray="4,3" marker-end="url(#arrow-up)"/>

  <!-- ========================================================================= -->
  <!-- BƯỚC 3: MIDDLEWARE PIPELINE (LỒNG 6 MIDDLEWARE) -->
  <!-- ========================================================================= -->
  <g transform="translate(180, 295)" filter="url(#shadow)">
    <!-- Vỏ bọc toàn bộ Pipeline -->
    <rect width="740" height="385" rx="10" fill="#F0FDF4" stroke="#2563EB" stroke-width="2"/>
    <rect width="740" height="32" rx="10" fill="#2563EB"/>
    <rect y="24" width="740" height="8" fill="#2563EB"/>
    <text x="20" y="21" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="bold" fill="#FFFFFF">BƯỚC 3: ĐƯỜNG ỐNG MIDDLEWARE PIPELINE (MÔ HÌNH BÚP BÊ NGA - RUSSIAN DOLL)</text>
    <text x="720" y="21" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#DBEAFE" text-anchor="end">Cơ chế ủy thác next(context)</text>

    <!-- MW 1: ExceptionHandling -->
    <g transform="translate(20, 42)">
      <rect width="700" height="46" rx="6" fill="#FFFFFF" stroke="#CBD5E1" stroke-width="1.2"/>
      <rect width="6" height="46" rx="3" fill="#DC2626"/>
      <text x="18" y="18" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#991B1B">3.1. ExceptionHandlingMiddleware (Lớp vỏ ngoài cùng)</text>
      <text x="18" y="34" font-family="Segoe UI, Arial, sans-serif" font-size="10" fill="#475569">Bọc khối try-catch toàn cục. Chiều về: Nếu có ngoại lệ chưa xử lý, bắt lỗi và chuẩn hóa thành JSON RFC 7807 ProblemDetails.</text>
    </g>

    <!-- MW 2: CorrelationId -->
    <g transform="translate(20, 94)">
      <rect width="700" height="46" rx="6" fill="#FFFFFF" stroke="#CBD5E1" stroke-width="1.2"/>
      <rect width="6" height="46" rx="3" fill="#2563EB"/>
      <text x="18" y="18" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#1E40AF">3.2. CorrelationIdMiddleware (Truy vết phân tán)</text>
      <text x="18" y="34" font-family="Segoe UI, Arial, sans-serif" font-size="10" fill="#475569">Đọc mã X-Correlation-Id từ client (hoặc sinh GUID mới). Gán vào HttpContext.TraceIdentifier và mở ILogger.BeginScope.</text>
    </g>

    <!-- MW 3: HttpsRedirection -->
    <g transform="translate(20, 146)">
      <rect width="700" height="46" rx="6" fill="#FFFFFF" stroke="#CBD5E1" stroke-width="1.2"/>
      <rect width="6" height="46" rx="3" fill="#0D9488"/>
      <text x="18" y="18" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#0F766E">3.3. UseHttpsRedirection (Bảo mật kênh truyền)</text>
      <text x="18" y="34" font-family="Segoe UI, Arial, sans-serif" font-size="10" fill="#475569">Kiểm tra kết nối: nếu request gửi qua HTTP thông thường, tự động gửi phản hồi chuyển hướng (307/308) sang cổng HTTPS.</text>
    </g>

    <!-- MW 4: Authentication -->
    <g transform="translate(20, 198)">
      <rect width="700" height="46" rx="6" fill="#FFFFFF" stroke="#CBD5E1" stroke-width="1.2"/>
      <rect width="6" height="46" rx="3" fill="#7C3AED"/>
      <text x="18" y="18" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#6D28D9">3.4. UseAuthentication (Xác thực danh tính JwtBearer)</text>
      <text x="18" y="34" font-family="Segoe UI, Arial, sans-serif" font-size="10" fill="#475569">Giải mã JWT Bearer Token, kiểm tra chữ ký khóa đối xứng (SigningKey), trích xuất Claims và nạp ClaimsPrincipal vào HttpContext.User.</text>
    </g>

    <!-- MW 5: Authorization -->
    <g transform="translate(20, 250)">
      <rect width="700" height="46" rx="6" fill="#FFFFFF" stroke="#CBD5E1" stroke-width="1.2"/>
      <rect width="6" height="46" rx="3" fill="#EA580C"/>
      <text x="18" y="18" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#C2410C">3.5. UseAuthorization (Kiểm soát phân quyền)</text>
      <text x="18" y="34" font-family="Segoe UI, Arial, sans-serif" font-size="10" fill="#475569">Đối chiếu quyền của User với metadata của Action. Nếu chưa đăng nhập → Ngắt mạch 401; nếu thiếu Role 'Seller' → Ngắt mạch 403.</text>
    </g>

    <!-- MW 6: EndpointRouting -->
    <g transform="translate(20, 302)">
      <rect width="700" height="46" rx="6" fill="#FFFFFF" stroke="#CBD5E1" stroke-width="1.2"/>
      <rect width="6" height="46" rx="3" fill="#059669"/>
      <text x="18" y="18" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#065F46">3.6. EndpointRoutingMiddleware &amp; EndpointMiddleware (Định tuyến đích)</text>
      <text x="18" y="34" font-family="Segoe UI, Arial, sans-serif" font-size="10" fill="#475569">So khớp URL '/api/v1/listings' và phương thức POST, chọn Action ListingsController.Create() và kích hoạt MVC Pipeline.</text>
    </g>

    <!-- Thứ tự chú ý cảnh báo -->
    <rect x="20" y="354" width="700" height="22" rx="4" fill="#FEF3C7"/>
    <text x="370" y="369" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" font-weight="bold" fill="#92400E" text-anchor="middle">★ THỨ TỰ BẮT BUỘC: Authentication PHẢI đứng trước Authorization; ExceptionHandling PHẢI nằm ngoài cùng.</text>
  </g>

  <!-- Mũi tên 3 -> 4 -->
  <line x1="530" y1="680" x2="530" y2="710" stroke="#2563EB" stroke-width="2.5" marker-end="url(#arrow-down)"/>
  <line x1="570" y1="710" x2="570" y2="680" stroke="#059669" stroke-width="2" stroke-dasharray="4,3" marker-end="url(#arrow-up)"/>

  <!-- ========================================================================= -->
  <!-- BƯỚC 4: PRESENTATION LAYER - CONTROLLER -->
  <!-- ========================================================================= -->
  <g transform="translate(180, 710)" filter="url(#shadow)">
    <rect width="740" height="115" rx="8" fill="#F8FAFC" stroke="#059669" stroke-width="1.8"/>
    <rect width="740" height="28" rx="8" fill="#059669"/>
    <rect y="20" width="740" height="8" fill="#059669"/>
    <text x="20" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="12.5" font-weight="bold" fill="#FFFFFF">BƯỚC 4: PRESENTATION LAYER — LISTINGSCONTROLLER.CREATE()</text>
    <text x="720" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#D1FAE5" text-anchor="end">Controller-based Web API</text>

    <text x="25" y="47" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#065F46">1. Model Binding &amp; Validation:</text>
    <text x="215" y="47" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#334155">Đọc Stream JSON Body, ép kiểu sang CreateListingRequest; kiểm tra Data Annotations ([Required], [Range]).</text>
    <text x="215" y="61" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#DC2626">→ Nếu dữ liệu sai: ModelStateInvalidFilter tự ngắt mạch và trả ngay lỗi HTTP 400 Bad Request.</text>

    <text x="25" y="80" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#065F46">2. Trích xuất User Context:</text>
    <text x="215" y="80" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#334155">Lấy UserId người bán (15) từ ICurrentUser (được Inject theo vòng đời Scoped).</text>

    <text x="25" y="99" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#065F46">3. Ủy thác cho Service:</text>
    <text x="215" y="99" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#1E40AF">Tạo CreateListingCommand và gọi await _listingService.CreateAsync(command, cancellationToken).</text>
  </g>

  <!-- Mũi tên 4 -> 5 -->
  <line x1="530" y1="825" x2="530" y2="855" stroke="#2563EB" stroke-width="2.5" marker-end="url(#arrow-down)"/>
  <line x1="570" y1="855" x2="570" y2="825" stroke="#059669" stroke-width="2" stroke-dasharray="4,3" marker-end="url(#arrow-up)"/>

  <!-- ========================================================================= -->
  <!-- BƯỚC 5: APPLICATION LAYER - SERVICE & DOMAIN -->
  <!-- ========================================================================= -->
  <g transform="translate(180, 855)" filter="url(#shadow)">
    <rect width="740" height="115" rx="8" fill="#F8FAFC" stroke="#D97706" stroke-width="1.8"/>
    <rect width="740" height="28" rx="8" fill="#D97706"/>
    <rect y="20" width="740" height="8" fill="#D97706"/>
    <text x="20" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="12.5" font-weight="bold" fill="#FFFFFF">BƯỚC 5: APPLICATION &amp; DOMAIN LAYER — LISTINGSERVICE</text>
    <text x="720" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#FEF3C7" text-anchor="end">Clean Architecture (Scoped Lifetime)</text>

    <text x="25" y="47" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#92400E">1. Khởi tạo Domain Entity:</text>
    <text x="200" y="47" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#334155">Gọi constructor Listing(userId, title, price, area...), kiểm tra Business Invariants (Giá &gt; 0, Diện tích &gt; 0, Status = Draft).</text>

    <text x="25" y="68" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#92400E">2. Thao tác Repository:</text>
    <text x="200" y="68" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#334155">Gọi await _listingRepository.AddAsync(listing, cancellationToken) để đưa thực thể vào phiên làm việc.</text>

    <text x="25" y="89" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#92400E">3. Cam kết &amp; Ghi lịch sử:</text>
    <text x="200" y="89" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#334155">Gọi _unitOfWork.SaveChangesAsync(), sau đó ghi vết ListingHistory snapshot ("Create") và lưu lần 2.</text>

    <text x="25" y="107" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#92400E">4. Ánh xạ trả về:</text>
    <text x="200" y="107" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#047857">Chuyển đổi thực thể Domain sang ListingDto bất biến (record) để trả về Controller.</text>
  </g>

  <!-- Mũi tên 5 -> 6 -->
  <line x1="530" y1="970" x2="530" y2="1000" stroke="#2563EB" stroke-width="2.5" marker-end="url(#arrow-down)"/>
  <line x1="570" y1="1000" x2="570" y2="970" stroke="#059669" stroke-width="2" stroke-dasharray="4,3" marker-end="url(#arrow-up)"/>

  <!-- ========================================================================= -->
  <!-- BƯỚC 6: INFRASTRUCTURE LAYER - EF CORE 8 -->
  <!-- ========================================================================= -->
  <g transform="translate(180, 1000)" filter="url(#shadow)">
    <rect width="740" height="95" rx="8" fill="#F8FAFC" stroke="#0891B2" stroke-width="1.8"/>
    <rect width="740" height="28" rx="8" fill="#0891B2"/>
    <rect y="20" width="740" height="8" fill="#0891B2"/>
    <text x="20" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="12.5" font-weight="bold" fill="#FFFFFF">BƯỚC 6: INFRASTRUCTURE LAYER — PROPNESTDBCONTEXT &amp; EF CORE 8</text>
    <text x="720" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#CFFAFE" text-anchor="end">ORM &amp; Quản trị giao dịch (Scoped)</text>

    <text x="25" y="47" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#0E7490">1. ChangeTracker:</text>
    <text x="180" y="47" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#334155">Theo dõi trạng thái của thực thể Listing mới với cờ EntityState.Added trong bộ nhớ RAM.</text>

    <text x="25" y="66" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#0E7490">2. Sinh câu lệnh SQL:</text>
    <text x="180" y="66" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#334155">Biên dịch đối tượng sang câu lệnh SQL 'INSERT INTO [Listings] (...) VALUES (...); SELECT SCOPE_IDENTITY(), [RowVersion];'.</text>

    <text x="25" y="85" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#0E7490">3. Giao dịch an toàn:</text>
    <text x="180" y="85" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#1E40AF">Mở DbTransaction, chuyển tiếp CancellationToken xuống SqlClient, thực thi và cam kết dữ liệu trên CSDL.</text>
  </g>

  <!-- Mũi tên 6 -> 7 -->
  <line x1="530" y1="1095" x2="530" y2="1125" stroke="#2563EB" stroke-width="2.5" marker-end="url(#arrow-down)"/>
  <line x1="570" y1="1125" x2="570" y2="1095" stroke="#059669" stroke-width="2" stroke-dasharray="4,3" marker-end="url(#arrow-up)"/>

  <!-- ========================================================================= -->
  <!-- BƯỚC 7: CƠ SỞ DỮ LIỆU SQL SERVER -->
  <!-- ========================================================================= -->
  <g transform="translate(180, 1125)" filter="url(#shadow)">
    <rect width="740" height="75" rx="8" fill="#F8FAFC" stroke="#0F172A" stroke-width="1.8"/>
    <rect width="740" height="28" rx="8" fill="#0F172A"/>
    <rect y="20" width="740" height="8" fill="#0F172A"/>
    <text x="20" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="12.5" font-weight="bold" fill="#FFFFFF">BƯỚC 7: MICROSOFT SQL SERVER (LƯU TRỮ VẬT LÝ)</text>
    <text x="720" y="19" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#94A3B8" text-anchor="end">Hệ Quản trị CSDL Quan hệ</text>

    <text x="25" y="47" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#0F172A">1. Thực thi &amp; Khóa bảng:</text>
    <text x="195" y="47" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#334155">Thực thi câu lệnh INSERT vào bảng [Listings], tự động sinh khóa chính ListingId (1024) và RowVersion.</text>

    <text x="25" y="65" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#0F172A">2. Cam kết &amp; Phản hồi:</text>
    <text x="195" y="65" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" fill="#047857">Commit giao dịch, ghi vào Transaction Log trên đĩa cứng và trả dữ liệu đã sinh về cho EF Core.</text>
  </g>

  <!-- ========================================================================= -->
  <!-- THANH CHÚ GIẢI FOOTER (BOTTOM: y: 1225 to 1300) -->
  <!-- ========================================================================= -->
  <g transform="translate(180, 1220)">
    <rect width="740" height="85" rx="6" fill="#F1F5F9" stroke="#CBD5E1" stroke-width="1"/>
    <text x="370" y="18" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#1E293B" text-anchor="middle">QUY TRÌNH PHẢN HỒI (OUTBOUND) &amp; GIẢI PHÓNG PHẠM VI (DISPOSAL)</text>
    
    <text x="20" y="38" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#334155">• <tspan font-weight="bold">Chiều phản hồi ngược:</tspan> SQL Server → EF Core → ListingService → Controller (gói HTTP 201 Created) → Middleware → Kestrel → Client.</text>
    <text x="20" y="55" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#047857">• <tspan font-weight="bold">Scope Disposal tự động:</tspan> Khi request hoàn tất, IoC Container tự hủy Service Scope → Gọi DbContext.Dispose() để giải phóng bộ nhớ ChangeTracker và trả Sql Connection về lại Connection Pool.</text>
    <text x="20" y="72" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#991B1B">• <tspan font-weight="bold">Cơ chế Ngắt mạch (Short-circuiting):</tspan> Nếu Token sai (401), thiếu Role (403), hoặc Validation lỗi (400), pipeline ngắt mạch ngay lập tức và quay đầu trả lỗi, bảo vệ các tầng phía sau khỏi tải thừa.</text>
  </g>

</svg>"""

    target_dir = os.path.join("docs", "diagrams", "svg")
    os.makedirs(target_dir, exist_ok=True)
    
    file_path = os.path.join(target_dir, "hinh_4_3_luong_xu_ly_http_request.svg")
    with open(file_path, "w", encoding="utf-8") as f:
        f.write(svg_content)
    print(f"Clean SVG generated: {file_path}")

    alias_path = os.path.join(target_dir, "hinh_4_2_3_http_request_flow.svg")
    with open(alias_path, "w", encoding="utf-8") as f:
        f.write(svg_content)
    print(f"Clean SVG alias generated: {alias_path}")

if __name__ == "__main__":
    generate_clean_svg()

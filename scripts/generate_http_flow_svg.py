import os

def generate_svg():
    svg_content = """<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1240 960" width="1240" height="960">
  <defs>
    <!-- Markers for Arrows -->
    <marker id="arrow-blue" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
      <path d="M 0 1 L 9 5 L 0 9 z" fill="#2563EB"/>
    </marker>
    <marker id="arrow-green" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
      <path d="M 0 1 L 9 5 L 0 9 z" fill="#059669"/>
    </marker>
    <marker id="arrow-red" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
      <path d="M 0 1 L 9 5 L 0 9 z" fill="#DC2626"/>
    </marker>
    <marker id="arrow-amber" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
      <path d="M 0 1 L 9 5 L 0 9 z" fill="#D97706"/>
    </marker>
    
    <!-- Shadow Filter -->
    <filter id="box-shadow" x="-3%" y="-3%" width="106%" height="108%" filterUnits="userSpaceOnUse">
      <feDropShadow dx="1" dy="2" stdDeviation="2" flood-color="#000000" flood-opacity="0.06"/>
    </filter>
  </defs>

  <!-- Background Canvas -->
  <rect width="1240" height="960" fill="#FFFFFF"/>

  <!-- ========================================================================= -->
  <!-- HEADER BAR -->
  <!-- ========================================================================= -->
  <rect x="0" y="0" width="1240" height="54" fill="#0F172A"/>
  <rect x="0" y="52" width="1240" height="2" fill="#2563EB"/>
  <text x="620" y="27" font-family="Segoe UI, Arial, sans-serif" font-size="16" font-weight="bold" fill="#FFFFFF" text-anchor="middle" letter-spacing="0.5">HÌNH 4.3: SƠ ĐỒ LUỒNG XỬ LÝ MỘT HTTP REQUEST TRONG ASP.NET CORE 8 (DỰ ÁN PROPNEST)</text>
  <text x="620" y="44" font-family="Segoe UI, Arial, sans-serif" font-size="11" fill="#94A3B8" text-anchor="middle">Chi tiết luồng End-to-End: Client → Kestrel → Middleware Pipeline (Russian Doll) → Controller → Service → EF Core → SQL Server</text>

  <!-- ========================================================================= -->
  <!-- VÙNG 1: CLIENT & PHẢN HỒI (LEFT COLUMN: x: 30 to 310) -->
  <!-- ========================================================================= -->

  <!-- Khối Client Request -->
  <g transform="translate(30, 75)" filter="url(#box-shadow)">
    <rect width="280" height="225" rx="8" fill="#F8FAFC" stroke="#6366F1" stroke-width="1.6"/>
    <rect width="280" height="32" rx="8" fill="#6366F1"/>
    <rect y="24" width="280" height="8" fill="#6366F1"/>
    <text x="140" y="21" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="bold" fill="#FFFFFF" text-anchor="middle">1. Client / Người dùng (Seller)</text>
    
    <!-- Badge -->
    <rect x="12" y="40" width="256" height="22" rx="4" fill="#EEF2FF" stroke="#C7D2FE" stroke-width="1"/>
    <text x="140" y="55" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#4338CA" text-anchor="middle">HTTP POST /api/v1/listings</text>
    
    <!-- Code preview -->
    <rect x="12" y="68" width="256" height="145" rx="5" fill="#FFFFFF" stroke="#CBD5E1" stroke-width="1"/>
    <text x="20" y="86" font-family="Consolas, monospace" font-size="9.5" fill="#1E293B" font-weight="bold">Headers:</text>
    <text x="20" y="101" font-family="Consolas, monospace" font-size="9" fill="#475569">Authorization: Bearer &lt;JWT&gt;</text>
    <text x="20" y="115" font-family="Consolas, monospace" font-size="9" fill="#475569">Content-Type: application/json</text>
    <text x="20" y="129" font-family="Consolas, monospace" font-size="9" fill="#2563EB">X-Correlation-Id: c89f1d2e...</text>
    <text x="20" y="147" font-family="Consolas, monospace" font-size="9.5" fill="#1E293B" font-weight="bold">JSON Body:</text>
    <text x="20" y="162" font-family="Consolas, monospace" font-size="9" fill="#047857">{"title": "Căn hộ Vinhomes",</text>
    <text x="20" y="176" font-family="Consolas, monospace" font-size="9" fill="#047857"> "price": 12500000, "area": 68.5,</text>
    <text x="20" y="190" font-family="Consolas, monospace" font-size="9" fill="#047857"> "city": "TP.HCM", "district": "..."}</text>
  </g>

  <!-- Khối Client Response Nhận về -->
  <g transform="translate(30, 680)" filter="url(#box-shadow)">
    <rect width="280" height="240" rx="8" fill="#F8FAFC" stroke="#059669" stroke-width="1.6"/>
    <rect width="280" height="32" rx="8" fill="#059669"/>
    <rect y="24" width="280" height="8" fill="#059669"/>
    <text x="140" y="21" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="bold" fill="#FFFFFF" text-anchor="middle">7. Nhận phản hồi &amp; Kết thúc</text>
    
    <!-- Badge Status -->
    <rect x="12" y="40" width="256" height="24" rx="4" fill="#ECFDF5" stroke="#A7F3D0" stroke-width="1"/>
    <circle cx="26" cy="52" r="5" fill="#059669"/>
    <text x="38" y="56" font-family="Segoe UI, Arial, sans-serif" font-size="11" font-weight="bold" fill="#065F46">HTTP/1.1 201 Created</text>

    <!-- Response Preview -->
    <rect x="12" y="70" width="256" height="120" rx="5" fill="#FFFFFF" stroke="#CBD5E1" stroke-width="1"/>
    <text x="20" y="87" font-family="Consolas, monospace" font-size="9.5" fill="#1E293B" font-weight="bold">Response Headers:</text>
    <text x="20" y="101" font-family="Consolas, monospace" font-size="9" fill="#047857">Location: /api/v1/listings/1024</text>
    <text x="20" y="115" font-family="Consolas, monospace" font-size="9" fill="#2563EB">X-Correlation-Id: c89f1d2e...</text>
    <text x="20" y="132" font-family="Consolas, monospace" font-size="9.5" fill="#1E293B" font-weight="bold">JSON Response Body:</text>
    <text x="20" y="147" font-family="Consolas, monospace" font-size="9" fill="#1E293B">{"listingId": 1024, "status": "Draft",</text>
    <text x="20" y="161" font-family="Consolas, monospace" font-size="9" fill="#1E293B"> "rowVersion": "AAAAAAAAB9Q=", ...}</text>

    <!-- Scope Dispose Note -->
    <rect x="12" y="196" width="256" height="34" rx="4" fill="#FEF2F2" stroke="#FECACA" stroke-width="1"/>
    <text x="20" y="210" font-family="Segoe UI, Arial, sans-serif" font-size="9" font-weight="bold" fill="#991B1B">• Scope Disposal: DbContext.Dispose()</text>
    <text x="20" y="222" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" fill="#7F1D1D">  Thu hồi ChangeTracker &amp; trả SqlConn về Pool.</text>
  </g>

  <!-- ========================================================================= -->
  <!-- VÙNG 2: KESTREL WEB SERVER & MIDDLEWARE PIPELINE (MIDDLE: x: 345 to 735) -->
  <!-- ========================================================================= -->

  <!-- Khối Kestrel Web Server -->
  <g transform="translate(345, 75)" filter="url(#box-shadow)">
    <rect width="380" height="115" rx="8" fill="#F8FAFC" stroke="#475569" stroke-width="1.6"/>
    <rect width="380" height="30" rx="8" fill="#475569"/>
    <rect y="22" width="380" height="8" fill="#475569"/>
    <text x="190" y="20" font-family="Segoe UI, Arial, sans-serif" font-size="12.5" font-weight="bold" fill="#FFFFFF" text-anchor="middle">2. Kestrel Web Server (Tầng mạng cơ sở)</text>
    
    <text x="15" y="47" font-family="Segoe UI, Arial, sans-serif" font-size="10" font-weight="bold" fill="#1E293B">• Socket Transport (IOCP / epoll): Tiếp nhận kết nối mạng nhị phân</text>
    <text x="15" y="62" font-family="Segoe UI, Arial, sans-serif" font-size="10" fill="#475569">• Phân tích HTTP/1.1 &amp; HTTP/2 bytes từ socket</text>
    
    <rect x="12" y="70" width="356" height="36" rx="5" fill="#EFF6FF" stroke="#BFDBFE" stroke-width="1"/>
    <text x="20" y="85" font-family="Consolas, monospace" font-size="9.5" font-weight="bold" fill="#1E40AF">Khởi tạo đối tượng HttpContext phiên làm việc:</text>
    <text x="20" y="98" font-family="Consolas, monospace" font-size="8.5" fill="#1E3A8A">HttpContext.Request | HttpContext.Response | CancellationToken | User</text>
  </g>

  <!-- Khối Middleware Pipeline (Russian Doll) -->
  <g transform="translate(345, 205)" filter="url(#box-shadow)">
    <rect width="380" height="465" rx="8" fill="#F0FDF4" stroke="#2563EB" stroke-width="1.8"/>
    <rect width="380" height="32" rx="8" fill="#2563EB"/>
    <rect y="24" width="380" height="8" fill="#2563EB"/>
    <text x="190" y="21" font-family="Segoe UI, Arial, sans-serif" font-size="12.5" font-weight="bold" fill="#FFFFFF" text-anchor="middle">3. ASP.NET Core Middleware Pipeline (Russian Doll)</text>

    <!-- Sub-headers for Inbound & Outbound -->
    <rect x="12" y="38" width="168" height="20" rx="3" fill="#DBEAFE"/>
    <text x="96" y="52" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" font-weight="bold" fill="#1E40AF" text-anchor="middle">▼ Chiều xuôi (Inbound next)</text>
    <rect x="200" y="38" width="168" height="20" rx="3" fill="#DCFCE7"/>
    <text x="284" y="52" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" font-weight="bold" fill="#166534" text-anchor="middle">▲ Chiều ngược (Outbound)</text>

    <!-- Step MW 1: ExceptionHandlingMiddleware -->
    <g transform="translate(12, 64)">
      <rect width="356" height="52" rx="5" fill="#FFFFFF" stroke="#94A3B8" stroke-width="1"/>
      <rect width="6" height="52" rx="2" fill="#DC2626"/>
      <text x="14" y="16" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#1E293B">[1] ExceptionHandlingMiddleware (Lớp vỏ ngoài cùng)</text>
      <text x="14" y="31" font-family="Segoe UI, Arial, sans-serif" font-size="9" fill="#475569">• Khối try { await next(); } bọc toàn bộ luồng xử lý</text>
      <text x="14" y="44" font-family="Segoe UI, Arial, sans-serif" font-size="9" fill="#DC2626">• Bắt mọi Exception chưa xử lý → Chuẩn hóa RFC 7807 ProblemDetails</text>
    </g>

    <!-- Step MW 2: CorrelationIdMiddleware -->
    <g transform="translate(12, 122)">
      <rect width="356" height="52" rx="5" fill="#FFFFFF" stroke="#94A3B8" stroke-width="1"/>
      <rect width="6" height="52" rx="2" fill="#2563EB"/>
      <text x="14" y="16" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#1E293B">[2] CorrelationIdMiddleware (Distributed Tracing)</text>
      <text x="14" y="31" font-family="Segoe UI, Arial, sans-serif" font-size="9" fill="#475569">• Đọc hoặc sinh mới GUID mã vết X-Correlation-Id</text>
      <text x="14" y="44" font-family="Segoe UI, Arial, sans-serif" font-size="9" fill="#1E40AF">• Gán TraceIdentifier &amp; tạo ILogger.BeginScope("CorrelationId")</text>
    </g>

    <!-- Step MW 3: UseHttpsRedirection -->
    <g transform="translate(12, 180)">
      <rect width="356" height="42" rx="5" fill="#FFFFFF" stroke="#94A3B8" stroke-width="1"/>
      <rect width="6" height="42" rx="2" fill="#0D9488"/>
      <text x="14" y="16" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#1E293B">[3] UseHttpsRedirection</text>
      <text x="14" y="32" font-family="Segoe UI, Arial, sans-serif" font-size="9" fill="#475569">• Tự động chuyển hướng kết nối HTTP sang HTTPS mã hóa TLS</text>
    </g>

    <!-- Step MW 4: UseAuthentication -->
    <g transform="translate(12, 228)">
      <rect width="356" height="56" rx="5" fill="#FFFFFF" stroke="#94A3B8" stroke-width="1"/>
      <rect width="6" height="56" rx="2" fill="#7C3AED"/>
      <text x="14" y="16" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#1E293B">[4] UseAuthentication (JwtBearerDefaults)</text>
      <text x="14" y="31" font-family="Segoe UI, Arial, sans-serif" font-size="9" fill="#475569">• Giải mã JWT Token, kiểm tra chữ ký khóa đối xứng (SigningKey)</text>
      <text x="14" y="45" font-family="Segoe UI, Arial, sans-serif" font-size="9" fill="#6D28D9">• Nạp ClaimsPrincipal (sub=15, role=Seller) vào HttpContext.User</text>
    </g>

    <!-- Step MW 5: UseAuthorization -->
    <g transform="translate(12, 290)">
      <rect width="356" height="60" rx="5" fill="#FFFFFF" stroke="#94A3B8" stroke-width="1"/>
      <rect width="6" height="60" rx="2" fill="#EA580C"/>
      <text x="14" y="16" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#1E293B">[5] UseAuthorization (Kiểm tra quyền truy cập)</text>
      <text x="14" y="31" font-family="Segoe UI, Arial, sans-serif" font-size="9" fill="#475569">• Kiểm tra User có Role "Seller" khớp [Authorize(Roles="Seller")]</text>
      <text x="14" y="45" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" fill="#DC2626">• Short-circuit: Nếu Token sai → 401; Nếu không đủ Role → 403 Forbidden</text>
    </g>

    <!-- Step MW 6: EndpointRouting & Model Binding -->
    <g transform="translate(12, 356)">
      <rect width="356" height="52" rx="5" fill="#FFFFFF" stroke="#94A3B8" stroke-width="1"/>
      <rect width="6" height="52" rx="2" fill="#059669"/>
      <text x="14" y="16" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#1E293B">[6] EndpointRouting &amp; EndpointMiddleware</text>
      <text x="14" y="31" font-family="Segoe UI, Arial, sans-serif" font-size="9" fill="#475569">• Khớp URL "/api/v1/listings" (POST) với ListingsController.Create</text>
      <text x="14" y="44" font-family="Segoe UI, Arial, sans-serif" font-size="9" fill="#065F46">• Chuyển giao HttpContext sang MVC Action Invoker</text>
    </g>

    <!-- Pipeline Note Box -->
    <rect x="12" y="416" width="356" height="38" rx="4" fill="#FEF3C7" stroke="#FDE68A" stroke-width="1"/>
    <text x="190" y="430" font-family="Segoe UI, Arial, sans-serif" font-size="9" font-weight="bold" fill="#92400E" text-anchor="middle">★ THỨ TỰ BẮT BUỘC: Đảo UseAuthorization trước UseAuthentication</text>
    <text x="190" y="443" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" fill="#78350F" text-anchor="middle">sẽ gây lỗi 401 Unauthorized vì User chưa được xác thực danh tính!</text>
  </g>

  <!-- ========================================================================= -->
  <!-- VÙNG 3: CLEAN ARCHITECTURE TIERS (RIGHT COLUMN: x: 770 to 1210) -->
  <!-- ========================================================================= -->

  <!-- [4] Presentation: Controller -->
  <g transform="translate(770, 75)" filter="url(#box-shadow)">
    <rect width="440" height="235" rx="8" fill="#F8FAFC" stroke="#059669" stroke-width="1.6"/>
    <rect width="440" height="32" rx="8" fill="#059669"/>
    <rect y="24" width="440" height="8" fill="#059669"/>
    <text x="220" y="21" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="bold" fill="#FFFFFF" text-anchor="middle">4. Presentation Layer — ListingsController</text>

    <!-- Action Box -->
    <rect x="12" y="40" width="416" height="98" rx="5" fill="#FFFFFF" stroke="#CBD5E1" stroke-width="1"/>
    <text x="20" y="56" font-family="Consolas, monospace" font-size="9.5" font-weight="bold" fill="#065F46">[HttpPost] CreateListingAction:</text>
    <text x="20" y="72" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">• <tspan font-weight="bold">Model Binding:</tspan> Ánh xạ JSON Body thô sang DTO <tspan font-family="Consolas">CreateListingRequest</tspan></text>
    <text x="20" y="87" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">• <tspan font-weight="bold">Input Validation:</tspan> Data Annotations ([Required], [Range]...)</text>
    <text x="32" y="100" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" fill="#DC2626">→ Nếu dữ liệu sai: ModelStateInvalidFilter tự ngắt mạch, trả HTTP 400 Bad Request</text>
    <text x="20" y="117" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">• Lấy <tspan font-family="Consolas" font-weight="bold">currentUser.UserId (15)</tspan> từ ICurrentUser Scoped service</text>
    <text x="20" y="131" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">• Tạo <tspan font-family="Consolas" font-weight="bold">CreateListingCommand</tspan> và chuyển sang tầng Application</text>

    <!-- Code invocation -->
    <rect x="12" y="145" width="416" height="78" rx="5" fill="#F1F5F9" stroke="#CBD5E1" stroke-width="1"/>
    <text x="20" y="162" font-family="Consolas, monospace" font-size="9" fill="#0F172A">var command = new CreateListingCommand(userId, request.Title, ...);</text>
    <text x="20" y="177" font-family="Consolas, monospace" font-size="9" fill="#2563EB" font-weight="bold">var listing = await _listingService.CreateAsync(command, cancelToken);</text>
    <text x="20" y="193" font-family="Consolas, monospace" font-size="9" fill="#059669">return CreatedAtAction(nameof(GetById),</text>
    <text x="20" y="207" font-family="Consolas, monospace" font-size="9" fill="#059669">       new { listingId = listing.ListingId }, listing);</text>
  </g>

  <!-- [5] Application: ListingService & Domain Entity -->
  <g transform="translate(770, 330)" filter="url(#box-shadow)">
    <rect width="440" height="260" rx="8" fill="#F8FAFC" stroke="#D97706" stroke-width="1.6"/>
    <rect width="440" height="32" rx="8" fill="#D97706"/>
    <rect y="24" width="440" height="8" fill="#D97706"/>
    <text x="220" y="21" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="bold" fill="#FFFFFF" text-anchor="middle">5. Application &amp; Domain Layer — ListingService</text>

    <!-- Service Logic Box -->
    <rect x="12" y="40" width="416" height="208" rx="5" fill="#FFFFFF" stroke="#CBD5E1" stroke-width="1"/>
    <text x="20" y="58" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#92400E">Quy trình điều phối nghiệp vụ độc lập (Scoped Lifetime):</text>
    
    <text x="20" y="78" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">1. <tspan font-weight="bold">Khởi tạo Domain Entity:</tspan> Gọi constructor <tspan font-family="Consolas">new Listing(...)</tspan></text>
    <text x="32" y="93" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" fill="#475569">→ Kiểm tra Business Invariants: Giá &gt; 0, Diện tích &gt; 0, Status = Draft</text>

    <text x="20" y="112" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">2. <tspan font-weight="bold">Ghi nhận vào Repository:</tspan></text>
    <text x="32" y="127" font-family="Consolas, monospace" font-size="9" fill="#2563EB">await _listingRepo.AddAsync(listing, cancellationToken);</text>

    <text x="20" y="146" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">3. <tspan font-weight="bold">Lưu thay đổi CSDL:</tspan></text>
    <text x="32" y="161" font-family="Consolas, monospace" font-size="9" fill="#059669">await _unitOfWork.SaveChangesAsync(cancellationToken);</text>

    <text x="20" y="180" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">4. <tspan font-weight="bold">Ghi vết lịch sử khởi tạo tin:</tspan></text>
    <text x="32" y="195" font-family="Consolas, monospace" font-size="9" fill="#475569">_listingRepo.AddHistory(ListingHistory.CreateSnapshot("Create", ...));</text>
    <text x="32" y="210" font-family="Consolas, monospace" font-size="9" fill="#059669">await _unitOfWork.SaveChangesAsync(cancellationToken);</text>

    <text x="20" y="232" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">5. <tspan font-weight="bold">Ánh xạ kết quả:</tspan> Chuyển Listing sang <tspan font-family="Consolas" font-weight="bold">ListingDto</tspan> trả về Controller</text>
  </g>

  <!-- [6] Infrastructure: EF Core & SQL Server -->
  <g transform="translate(770, 610)" filter="url(#box-shadow)">
    <rect width="440" height="310" rx="8" fill="#F8FAFC" stroke="#0891B2" stroke-width="1.6"/>
    <rect width="440" height="32" rx="8" fill="#0891B2"/>
    <rect y="24" width="440" height="8" fill="#0891B2"/>
    <text x="220" y="21" font-family="Segoe UI, Arial, sans-serif" font-size="13" font-weight="bold" fill="#FFFFFF" text-anchor="middle">6. Infrastructure Layer — EF Core 8 &amp; SQL Server</text>

    <!-- EF Core Box -->
    <rect x="12" y="40" width="416" height="125" rx="5" fill="#FFFFFF" stroke="#CBD5E1" stroke-width="1"/>
    <text x="20" y="56" font-family="Segoe UI, Arial, sans-serif" font-size="10.5" font-weight="bold" fill="#0E7490">PropNestDbContext (Scoped Lifetime):</text>
    <text x="20" y="74" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">• <tspan font-weight="bold">ChangeTracker:</tspan> Theo dõi thực thể Listing (State: EntityState.Added)</text>
    <text x="20" y="90" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">• <tspan font-weight="bold">DbTransaction:</tspan> Mở giao dịch CSDL an toàn (ACID transaction)</text>
    <text x="20" y="106" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">• <tspan font-weight="bold">SQL Command Generation:</tspan> Biên dịch LINQ/Entity sang mã SQL INSERT</text>
    <text x="20" y="122" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">• <tspan font-weight="bold">Optimistic Concurrency:</tspan> Nạp giá trị RowVersion (byte[] timestamp)</text>
    <text x="20" y="138" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B">• <tspan font-weight="bold">CancellationToken:</tspan> Chuyển tiếp xuống SqlClient để ngắt SQL nếu client hủy</text>

    <!-- SQL Server Box -->
    <rect x="12" y="172" width="416" height="125" rx="5" fill="#0F172A" stroke="#334155" stroke-width="1"/>
    <text x="20" y="190" font-family="Segoe UI, Arial, sans-serif" font-size="10" font-weight="bold" fill="#38BDF8">Microsoft SQL Server (Thực thi câu lệnh thực tế):</text>
    
    <text x="20" y="208" font-family="Consolas, monospace" font-size="8.5" fill="#F1F5F9">INSERT INTO [Listings] ([OwnerUserId], [Title], [Description],</text>
    <text x="20" y="222" font-family="Consolas, monospace" font-size="8.5" fill="#F1F5F9">            [Price], [Area], [Status], [CreatedAt])</text>
    <text x="20" y="236" font-family="Consolas, monospace" font-size="8.5" fill="#34D399">VALUES (@p0, @p1, @p2, @p3, @p4, 'Draft', GETUTCDATE());</text>
    <text x="20" y="252" font-family="Consolas, monospace" font-size="8.5" fill="#F1F5F9">SELECT [ListingId], [RowVersion]</text>
    <text x="20" y="266" font-family="Consolas, monospace" font-size="8.5" fill="#34D399">FROM [Listings] WHERE @@ROWCOUNT = 1 AND [ListingId] = scope_identity();</text>
    <text x="20" y="284" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" fill="#94A3B8">→ Commit Transaction: Trả về ListingId = 1024, RowVersion = 0x00000000000007D1</text>
  </g>

  <!-- ========================================================================= -->
  <!-- ĐƯỜNG MŨI TÊN KẾT NỐI (CONNECTING FLOW LINES) -->
  <!-- ========================================================================= -->

  <!-- [1 -> 2] Client Request to Kestrel -->
  <path d="M 310 135 L 343 135" fill="none" stroke="#2563EB" stroke-width="2.2" marker-end="url(#arrow-blue)"/>
  <text x="327" y="125" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" font-weight="bold" fill="#2563EB" text-anchor="middle">TCP/TLS</text>

  <!-- [2 -> 3] Kestrel to Middleware Inbound -->
  <path d="M 535 190 L 535 203" fill="none" stroke="#2563EB" stroke-width="2.2" marker-end="url(#arrow-blue)"/>

  <!-- [3 -> 4] Middleware to Controller -->
  <path d="M 725 385 Q 748 385 748 195 L 768 195" fill="none" stroke="#2563EB" stroke-width="2.2" marker-end="url(#arrow-blue)"/>
  <text x="748" y="290" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" font-weight="bold" fill="#2563EB" text-anchor="middle" transform="rotate(90, 748, 290)">Dispatch Action</text>

  <!-- [4 -> 5] Controller to Service -->
  <path d="M 990 310 L 990 328" fill="none" stroke="#D97706" stroke-width="2.2" marker-end="url(#arrow-amber)"/>

  <!-- [5 -> 6] Service to DbContext / SQL -->
  <path d="M 990 590 L 990 608" fill="none" stroke="#0891B2" stroke-width="2.2" marker-end="url(#arrow-blue)"/>

  <!-- [6 -> 5] Db Return to Service -->
  <path d="M 1020 610 L 1020 592" fill="none" stroke="#059669" stroke-width="2" stroke-dasharray="4,3" marker-end="url(#arrow-green)"/>

  <!-- [5 -> 4] Service Return to Controller -->
  <path d="M 1020 330 L 1020 312" fill="none" stroke="#059669" stroke-width="2" stroke-dasharray="4,3" marker-end="url(#arrow-green)"/>

  <!-- [4 -> 3 Outbound] Controller Return to Middleware Outbound -->
  <path d="M 770 215 Q 735 215 727 430" fill="none" stroke="#059669" stroke-width="2" stroke-dasharray="4,3" marker-end="url(#arrow-green)"/>

  <!-- [3 -> 7 Outbound] Middleware Outbound to Client Response -->
  <path d="M 345 600 Q 320 600 320 740 L 312 740" fill="none" stroke="#059669" stroke-width="2.2" marker-end="url(#arrow-green)"/>
  <text x="325" y="665" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" font-weight="bold" fill="#059669" text-anchor="middle" transform="rotate(-90, 325, 665)">201 Created</text>

  <!-- ========================================================================= -->
  <!-- CHÚ GIẢI (LEGEND BOX: x: 345 to 725, y: 720 to 890) -->
  <!-- ========================================================================= -->
  <g transform="translate(345, 680)" filter="url(#box-shadow)">
    <rect width="380" height="240" rx="8" fill="#F8FAFC" stroke="#64748B" stroke-width="1.2"/>
    <rect width="380" height="26" rx="8" fill="#64748B"/>
    <rect y="18" width="380" height="8" fill="#64748B"/>
    <text x="190" y="18" font-family="Segoe UI, Arial, sans-serif" font-size="11.5" font-weight="bold" fill="#FFFFFF" text-anchor="middle">CHÚ GIẢI KỸ THUẬT &amp; NGUYÊN TẮC VẬN HÀNH</text>

    <!-- Legend items -->
    <line x1="20" y1="46" x2="60" y2="46" stroke="#2563EB" stroke-width="2.5"/>
    <polygon points="56,43 62,46 56,49" fill="#2563EB"/>
    <text x="70" y="50" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B"><tspan font-weight="bold">Chiều xuôi (Inbound Request):</tspan> Kestrel → Middleware → Controller → Service → EF Core</text>

    <line x1="20" y1="74" x2="60" y2="74" stroke="#059669" stroke-width="2" stroke-dasharray="4,3"/>
    <polygon points="56,71 62,74 56,77" fill="#059669"/>
    <text x="70" y="78" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B"><tspan font-weight="bold">Chiều ngược (Outbound Response):</tspan> Đóng gói DTO → 201 Created → Client</text>

    <line x1="20" y1="102" x2="60" y2="102" stroke="#DC2626" stroke-width="2" stroke-dasharray="3,3"/>
    <polygon points="56,99 62,102 56,105" fill="#DC2626"/>
    <text x="70" y="106" font-family="Segoe UI, Arial, sans-serif" font-size="9.5" fill="#1E293B"><tspan font-weight="bold">Cơ chế Ngắt mạch (Short-circuiting):</tspan> Chặn đứng request nếu Auth lỗi (401/403) hoặc Validation sai (400)</text>

    <rect x="20" y="125" width="340" height="98" rx="4" fill="#FFFFFF" stroke="#E2E8F0" stroke-width="1"/>
    <text x="28" y="142" font-family="Segoe UI, Arial, sans-serif" font-size="9" font-weight="bold" fill="#0F172A">Các nguyên tắc vàng được kiểm chứng trong PropNest:</text>
    <text x="28" y="158" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" fill="#334155">• <tspan font-weight="bold">Service Scope Isolation:</tspan> DbContext &amp; Service được tạo mới độc lập cho mỗi HTTP request.</text>
    <text x="28" y="172" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" fill="#334155">• <tspan font-weight="bold">Cooperative Cancellation:</tspan> CancellationToken truyền xuyên suốt; Client ngắt → SQL dừng ngay.</text>
    <text x="28" y="186" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" fill="#334155">• <tspan font-weight="bold">Trace Identifier Propagation:</tspan> Mã X-Correlation-Id hiện diện đồng nhất trong mọi dòng Serilog log.</text>
    <text x="28" y="200" font-family="Segoe UI, Arial, sans-serif" font-size="8.5" fill="#334155">• <tspan font-weight="bold">Zero Heap Logging:</tspan> [LoggerMessage] source-generator loại bỏ hoàn toàn chi phí Boxing.</text>
  </g>

</svg>"""

    target_dir = os.path.join("docs", "diagrams", "svg")
    os.makedirs(target_dir, exist_ok=True)
    
    file_path_1 = os.path.join(target_dir, "hinh_4_3_luong_xu_ly_http_request.svg")
    with open(file_path_1, "w", encoding="utf-8") as f:
        f.write(svg_content)
    print(f"Generated SVG: {file_path_1}")

    # Đồng thời tạo tên phụ khớp 4.2.3 để dễ tra cứu
    file_path_2 = os.path.join(target_dir, "hinh_4_2_3_http_request_flow.svg")
    with open(file_path_2, "w", encoding="utf-8") as f:
        f.write(svg_content)
    print(f"Generated alias SVG: {file_path_2}")

if __name__ == "__main__":
    generate_svg()

# -*- coding: utf-8 -*-
import os
import sys
sys.stdout.reconfigure(encoding='utf-8')
import docx
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

def set_cell_margins(cell, top=100, bottom=100, left=150, right=150):
    tcPr = cell._tc.get_or_add_tcPr()
    tcMar = OxmlElement('w:tcMar')
    for m, val in [('top', top), ('bottom', bottom), ('left', left), ('right', right)]:
        node = OxmlElement(f'w:{m}')
        node.set(qn('w:w'), str(val))
        node.set(qn('w:type'), 'dxa')
        tcMar.append(node)
    tcPr.append(tcMar)

def set_cell_shading(cell, color_hex):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:val'), 'clear')
    shd.set(qn('w:color'), 'auto')
    shd.set(qn('w:fill'), color_hex)
    tcPr.append(shd)

def set_cell_border(cell, **kwargs):
    tcPr = cell._tc.get_or_add_tcPr()
    tcBorders = OxmlElement('w:tcBorders')
    for edge in ('top', 'left', 'bottom', 'right', 'insideH', 'insideV'):
        edge_data = kwargs.get(edge)
        if edge_data:
            tag = f'w:{edge}'
            element = OxmlElement(tag)
            element.set(qn('w:val'), edge_data.get('val', 'single'))
            element.set(qn('w:sz'), str(edge_data.get('sz', 4)))
            element.set(qn('w:space'), '0')
            element.set(qn('w:color'), edge_data.get('color', 'CCCCCC'))
            tcBorders.append(element)
    tcPr.append(tcBorders)

def add_code_box(doc, code_text):
    tbl = doc.add_table(rows=1, cols=1)
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    cell = tbl.cell(0, 0)
    set_cell_shading(cell, "F2F4F7")
    set_cell_margins(cell, top=120, bottom=120, left=180, right=180)
    set_cell_border(cell,
        top={'val': 'single', 'sz': 6, 'color': 'D0D5DD'},
        bottom={'val': 'single', 'sz': 6, 'color': 'D0D5DD'},
        left={'val': 'single', 'sz': 16, 'color': '185ABC'},
        right={'val': 'single', 'sz': 6, 'color': 'D0D5DD'}
    )
    p = cell.paragraphs[0]
    p.paragraph_format.space_before = Pt(2)
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.line_spacing = 1.05
    run = p.add_run(code_text)
    run.font.name = "Consolas"
    run.font.size = Pt(9.5)
    run.font.color.rgb = RGBColor(30, 41, 59)
    doc.add_paragraph()

def add_styled_table(doc, headers, data, col_widths=None):
    tbl = doc.add_table(rows=len(data) + 1, cols=len(headers))
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    
    hdr_cells = tbl.rows[0].cells
    for i, title in enumerate(headers):
        hdr_cells[i].text = title
        set_cell_shading(hdr_cells[i], "EAECEF")
        set_cell_margins(hdr_cells[i], top=120, bottom=120, left=140, right=140)
        set_cell_border(hdr_cells[i],
            top={'val': 'single', 'sz': 6, 'color': 'B0B8C1'},
            bottom={'val': 'single', 'sz': 12, 'color': '185ABC'},
            left={'val': 'single', 'sz': 4, 'color': 'D0D5DD'},
            right={'val': 'single', 'sz': 4, 'color': 'D0D5DD'}
        )
        p = hdr_cells[i].paragraphs[0]
        p.alignment = WD_ALIGN_PARAGRAPH.CENTER
        p.paragraph_format.space_before = Pt(3)
        p.paragraph_format.space_after = Pt(3)
        for r in p.runs:
            r.font.name = "Times New Roman"
            r.font.size = Pt(11)
            r.font.bold = True
            r.font.color.rgb = RGBColor(24, 90, 188)

    for row_idx, row_data in enumerate(data):
        row_cells = tbl.rows[row_idx + 1].cells
        bg_color = "FAFAFB" if row_idx % 2 == 1 else "FFFFFF"
        for col_idx, cell_value in enumerate(row_data):
            row_cells[col_idx].text = cell_value
            set_cell_shading(row_cells[col_idx], bg_color)
            set_cell_margins(row_cells[col_idx], top=80, bottom=80, left=120, right=120)
            set_cell_border(row_cells[col_idx],
                top={'val': 'single', 'sz': 4, 'color': 'E1E4E8'},
                bottom={'val': 'single', 'sz': 4, 'color': 'E1E4E8'},
                left={'val': 'single', 'sz': 4, 'color': 'E1E4E8'},
                right={'val': 'single', 'sz': 4, 'color': 'E1E4E8'}
            )
            p = row_cells[col_idx].paragraphs[0]
            p.paragraph_format.space_before = Pt(2)
            p.paragraph_format.space_after = Pt(2)
            p.paragraph_format.line_spacing = 1.15
            for r in p.runs:
                r.font.name = "Times New Roman"
                r.font.size = Pt(10.5)
                r.font.color.rgb = RGBColor(36, 41, 46)

    if col_widths:
        for row in tbl.rows:
            for i, w in enumerate(col_widths):
                row.cells[i].width = Inches(w)

    doc.add_paragraph()

def add_image_with_caption(doc, img_path, caption_text, width_inches=6.0):
    if not os.path.exists(img_path):
        print(f"Warning: Image file not found: {img_path}")
        return
    p_img = doc.add_paragraph()
    p_img.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_img.paragraph_format.space_before = Pt(8)
    p_img.paragraph_format.space_after = Pt(4)
    run_img = p_img.add_run()
    run_img.add_picture(img_path, width=Inches(width_inches))
    
    p_cap = doc.add_paragraph()
    p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_cap.paragraph_format.space_before = Pt(2)
    p_cap.paragraph_format.space_after = Pt(10)
    run_cap = p_cap.add_run(caption_text)
    run_cap.font.name = "Times New Roman"
    run_cap.font.size = Pt(11)
    run_cap.font.italic = True
    run_cap.font.bold = True
    run_cap.font.color.rgb = RGBColor(70, 80, 95)

def set_para_font(p, text, bold=False, italic=False, size=13, color_rgb=(0,0,0), align=WD_ALIGN_PARAGRAPH.JUSTIFY, space_before=0, space_after=6, line_spacing=1.25):
    p.text = ""
    p.alignment = align
    p.paragraph_format.space_before = Pt(space_before)
    p.paragraph_format.space_after = Pt(space_after)
    p.paragraph_format.line_spacing = line_spacing
    run = p.add_run(text)
    run.font.name = "Times New Roman"
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.italic = italic
    run.font.color.rgb = RGBColor(*color_rgb)
    return p

def add_heading_2(doc, text):
    p = doc.add_paragraph()
    return set_para_font(p, text, bold=True, size=15, color_rgb=(24, 90, 188), align=WD_ALIGN_PARAGRAPH.LEFT, space_before=16, space_after=8)

def add_heading_3(doc, text):
    p = doc.add_paragraph()
    return set_para_font(p, text, bold=True, size=14, color_rgb=(15, 23, 42), align=WD_ALIGN_PARAGRAPH.LEFT, space_before=14, space_after=6)

def add_heading_4(doc, text):
    p = doc.add_paragraph()
    return set_para_font(p, text, bold=True, size=13, color_rgb=(30, 41, 59), align=WD_ALIGN_PARAGRAPH.LEFT, space_before=10, space_after=4)

def add_body(doc, text, bold=False, italic=False):
    p = doc.add_paragraph()
    return set_para_font(p, text, bold=bold, italic=italic, size=13, color_rgb=(0,0,0), align=WD_ALIGN_PARAGRAPH.JUSTIFY, space_before=0, space_after=6, line_spacing=1.25)

def populate_chapter_13_to_end(doc):
    # =========================================================================
    # CHƯƠNG 13: ĐÁNH GIÁ VÀ NHÌN LẠI
    # =========================================================================
    add_heading_2(doc, "CHƯƠNG 13: ĐÁNH GIÁ VÀ NHÌN LẠI")

    add_body(doc,
        "Sau hành trình nghiên cứu lý thuyết nền tảng ASP.NET Core 8 và trực tiếp thiết kế, hiện thực hóa hệ thống đăng tin bất động sản PropNest qua 6 Sprint phát triển, chương này thực hiện việc đối chiếu toàn diện giữa kết quả đạt được so với mục tiêu đề ra ban đầu, phân tích chuyên sâu các sự cố kỹ thuật điển hình đã giải quyết (Bug Reports), và đúc kết những bài học kinh nghiệm thực tiễn trong kỹ nghệ phần mềm hiện đại.")

    # -------------------------------------------------------------------------
    # 13.1. So sánh với mục tiêu ban đầu
    # -------------------------------------------------------------------------
    add_heading_3(doc, "13.1. So sánh với mục tiêu ban đầu")

    add_heading_4(doc, "13.1.1. Tiến độ thực hiện — Bảng tổng hợp các tính năng F01 đến F10")

    add_body(doc,
        "Ngay từ giai đoạn lập đề cương và phân tích yêu cầu tại Chương 11, nhóm đã xác định 10 nhóm chức năng cốt lõi (Feature Backlog từ F01 đến F10). Bảng 13.1 dưới đây tổng hợp chi tiết mức độ hoàn thiện thực tế của từng nhóm tính năng trên cả tầng Backend API và tầng Giao diện người dùng (Web UI):")

    headers_f = ["Mã", "Nhóm chức năng", "Hạng mục kỹ thuật đã hiện thực", "Tầng giao diện & Kiểm thử", "Mức độ hoàn thành"]
    data_f = [
        ["F01", "Quản lý tài khoản & Xác thực", "ASP.NET Core Identity, PBKDF2 HMAC-SHA512, cấp phát JWT Bearer Token, khóa tài khoản tạm thời (Lockout 5 lần/15 phút), phân quyền RBAC & Policy", "Giao diện Login/Signup (Client Portal), Swagger Auth", "100% Hoàn thành"],
        ["F02", "Quản lý vòng đời Tin đăng", "CRUD tin đăng, quản lý bộ sưu tập ảnh (ListingImages), lọc đa tiêu chí, phân trang PagedResult<T>, chuyển đổi trạng thái Draft/Pending/Published/Hidden", "Client Portal (Tìm kiếm) & Seller Portal (My Listings, Form Tạo/Sửa)", "100% Hoàn thành"],
        ["F03", "Kiểm duyệt tin đăng thủ công", "Hàng đợi chờ duyệt (PendingModeration), API phê duyệt (Approve) và từ chối (Reject) kèm lý do bắt buộc, thống kê Dashboard quản trị", "Admin Portal (Dashboard & Moderation Queue)", "100% Hoàn thành"],
        ["F04", "Lịch sử biến động dữ liệu (History Delta)", "Thuật toán DeltaEngine bóc tách vi sai trường dữ liệu qua EqualityComparer<T>, lưu trữ kết hợp Snapshot gốc và mảng DeltasJson siêu nhẹ", "Seller Portal (Trang xem Lịch sử thay đổi tin đăng) + Unit Tests", "100% Hoàn thành"],
        ["F05", "Quản lý Ví điện tử & Gói dịch vụ", "Tự động mở ví khi đăng ký, quản lý số dư DECIMAL(18,2), nạp tiền Top-up, truy vấn lịch sử giao dịch biến động số dư và danh mục gói VIP/Premium", "Seller Portal (Trang quản lý Ví & Nạp tiền)", "100% Hoàn thành"],
        ["F06", "Mua gói nâng cấp tin với Saga Orchestrator", "Cỗ máy trạng thái PurchasePackageSaga (MassTransit + RabbitMQ), phối hợp trừ tiền ví, nâng cấp hạn hiển thị tin và tự động bồi hoàn (RefundWalletCommand) khi lỗi", "Seller Portal (Flow mua gói) + Log thực nghiệm bồi hoàn", "100% Hoàn thành"],
        ["F07", "Outbox, Inbox & Vận hành nền", "Bảng OutboxMessages, OutboxDispatchService đẩy sự kiện tin cậy, InboxMessages chống trùng lặp tại Consumer, Idempotency-Key chống thanh toán lặp", "PropNest.Workflow.Worker + Unit Tests Idempotency", "100% Hoàn thành"],
        ["F08", "Theo dõi tiến độ Workflow", "API GET /api/v1/workflows/{correlationId} trả về trạng thái tổng thể và chi tiết từng bước thực thi (WorkflowSteps)", "Swagger UI & Seller Portal Polling", "100% Hoàn thành"],
        ["F09", "Tự động hết hạn tin đăng", "Tác vụ nền định kỳ kiểm tra EndDate của các tin Published để tự động chuyển trạng thái sang Expired và ghi nhận lịch sử", "Background Worker Service", "100% Hoàn thành"],
        ["F10", "Động cơ kiểm duyệt AI (AME)", "Hoàn tất thiết kế kiến trúc đa tầng (FastAPI + Rule Engine + Ollama LLM) và đặc tả hợp đồng tích hợp bất đồng bộ", "Bản thiết kế kiến trúc & Mô hình thực nghiệm (Định hướng mở rộng)", "Hoàn thành thiết kế"]
    ]
    add_body(doc, "Bảng 13.1: Bảng đối chiếu tiến độ và mức độ hoàn thành các nhóm tính năng từ F01 đến F10", bold=True, italic=True)
    add_styled_table(doc, headers_f, data_f, col_widths=[0.5, 1.4, 2.2, 1.4, 1.0])

    add_heading_4(doc, "13.1.2. Tổng kết tỷ lệ hoàn thành đồ án")

    add_body(doc,
        "Xét trên phương diện định lượng, nhóm đã hoàn thành trọn vẹn 46/46 hạng mục công việc (Tasks) được phân bổ trong kế hoạch 6 Sprint (đạt tỷ lệ 100% khối lượng cam kết). Cụ thể:")

    add_body(doc,
        "• Về mặt kiến trúc và mã nguồn: Xây dựng thành công Solution gồm 8 Project tuân thủ nghiêm ngặt kiến trúc Clean Architecture kết hợp Modular Monolith, biên dịch sạch hoàn toàn (0 Warning, 0 Error).")

    add_body(doc,
        "• Về mặt giao diện lập trình và ứng dụng: Triển khai hoàn chỉnh hơn 25 RESTful API endpoints có tài liệu hóa OpenAPI/Swagger, đi kèm 3 cổng giao diện Web MVC độc lập (Client, Seller, Admin) hoạt động mượt mà.")

    add_body(doc,
        "• Về mặt kiểm chứng chất lượng: Đạt tỷ lệ 100% vượt qua bộ kiểm thử tự động (20/20 Unit Tests Passed) cho các luồng nghiệp vụ trọng tâm của tầng Domain và Application.")

    add_heading_4(doc, "13.1.3. Tự đánh giá điểm mạnh và điểm yếu của giải pháp PropNest")

    add_body(doc,
        "1. Điểm mạnh nổi bật của hệ thống:", bold=True)

    add_body(doc,
        "• Độ tin cậy giao dịch tài chính rất cao: Sự kết hợp giữa khóa chống lặp Idempotency-Key ở tầng API, mẫu Transactional Outbox ở tầng Persistence, và cỗ máy trạng thái Saga Orchestration giúp hệ thống miễn nhiễm trước các lỗi trừ tiền kép hoặc mất tiền khi xảy ra sự cố mạng.")

    add_body(doc,
        "• Tối ưu hóa không gian lưu trữ và hiệu năng đồng thời: Cơ chế History Delta Engine giúp tiết kiệm trên 92% dung lượng lưu trữ lịch sử biến động giá; trong khi cơ chế kiểm soát đồng thời lạc quan (Optimistic Concurrency với RowVersion) bảo vệ an toàn dữ liệu mà không cần khóa cứng bảng (Table Lock).")

    add_body(doc,
        "• Phân tách trách nhiệm rõ ràng, dễ bảo trì: Tầng Domain hoàn toàn độc lập với cơ sở dữ liệu và thư viện bên ngoài, giúp việc viết Unit Test nhanh chóng (chỉ 73ms cho 20 kịch bản kiểm thử).")

    add_body(doc,
        "2. Điểm yếu và những hạn chế cần cải thiện:", bold=True)

    add_body(doc,
        "• Cơ chế tìm kiếm văn bản cơ bản: Tính năng tìm kiếm tin đăng hiện đang sử dụng truy vấn SQL kết hợp chỉ mục B-Tree truyền thống, chưa hỗ trợ tìm kiếm ngữ nghĩa (Semantic Search), chịu lỗi gõ sai chính tả (Fuzzy Search) hay truy vấn tọa độ địa lý bán kính phức tạp.")

    add_body(doc,
        "• Cập nhật trạng thái giao dịch qua Polling: Để biết tiến trình mua gói hoàn tất hay chưa, phía Client hiện phải gọi định kỳ (Polling) vào endpoint /api/v1/workflows/{correlationId} thay vì nhận thông báo đẩy tức thời qua kết nối hai chiều WebSockets/SignalR.")

    add_body(doc,
        "• Lưu trữ tệp tin cục bộ: Hình ảnh bất động sản hiện đang được lưu trữ trên hệ thống tệp cục bộ của máy chủ Web, chưa chuyển sang hạ tầng lưu trữ đám mây phân tán (Cloud Object Storage như AWS S3 hoặc Azure Blob Storage) kết hợp mạng phân phối nội dung CDN.")

    # -------------------------------------------------------------------------
    # 13.2. Phân tích lỗi kỹ thuật quan trọng và giải pháp (Bug Reports)
    # -------------------------------------------------------------------------
    add_heading_3(doc, "13.2. Phân tích lỗi kỹ thuật quan trọng và giải pháp (Bug Reports)")

    add_body(doc,
        "Trong quá trình phát triển và tích hợp liên tục qua 6 Sprint, nhóm đã đối mặt và xử lý nhiều bài toán kỹ thuật thực tế. Dưới đây là hồ sơ phân tích 3 lỗi kỹ thuật quan trọng nhất (Critical Bugs) cùng giải pháp khắc phục triệt để:")

    add_heading_4(doc, "13.2.1. Bug #1 — Xung đột ghi đè đồng thời khi hai tác nhân cùng sửa tin đăng (Lost Update)")

    add_body(doc,
        "• Bối cảnh phát hiện: Tại Sprint 2, khi kiểm thử tình huống Người bán mở trang chỉnh sửa giá bán tin đăng cùng lúc với một tiến trình khác cập nhật thông tin trên cùng bản ghi Listing, hệ thống chấp nhận cả hai lệnh lưu. Hậu quả là lệnh lưu thực hiện sau đã ghi đè và làm mất hoàn toàn thay đổi của lệnh lưu trước đó mà không hề có bất kỳ cảnh báo nào.")

    add_body(doc,
        "• Nguyên nhân gốc rễ (Root Cause): Câu lệnh SQL UPDATE do EF Core sinh ra mặc định chỉ lọc theo khóa chính (WHERE [ListingId] = @p0), hoàn toàn không kiểm tra xem bản ghi trong cơ sở dữ liệu có bị biến đổi kể từ thời điểm người dùng tải lên màn hình hay không.")

    add_body(doc,
        "• Giải pháp khắc phục: Nhóm đã bổ sung thuộc tính byte[] RowVersion vào thực thể Listing và cấu hình .IsRowVersion() trong ListingConfiguration. Mỗi khi trả dữ liệu về Client qua ListingDto, mảng byte này được mã hóa thành chuỗi Base64. Khi Client gửi yêu cầu PUT cập nhật, hệ thống gán lại giá trị này vào _dbContext.Entry(listing).Property(x => x.RowVersion).OriginalValue. Nhờ đó, nếu dữ liệu đã bị thay đổi bởi người khác, EF Core lập tức ném DbUpdateConcurrencyException và ExceptionHandlingMiddleware chuyển hóa thành phản hồi chuẩn 409 Conflict.")

    add_heading_4(doc, "13.2.2. Bug #2 — Trừ tiền ví lặp do mạng chập chờn hoặc người dùng nhấp đúp (Double Charging)")

    add_body(doc,
        "• Bối cảnh phát hiện: Tại Sprint 3, khi mô phỏng đường truyền mạng có độ trễ cao (Network Latency > 2000ms) trên giao diện mua gói VIP, người dùng có xu hướng bấm nút \"Thanh toán\" liên tiếp 2–3 lần. Hệ thống đã tiếp nhận cả hai HTTP POST request gần như đồng thời và phát sinh hai giao dịch trừ tiền ví cho cùng một lần nâng cấp tin.")

    add_body(doc,
        "• Nguyên nhân gốc rễ (Root Cause): Giao thức HTTP POST vốn không có tính chất lũy đẳng (Non-idempotent). Khi thiếu cơ chế định danh duy nhất cho mỗi ý định giao dịch từ phía Client, máy chủ coi mỗi lần gửi gói tin là một giao dịch độc lập mới.")

    add_body(doc,
        "• Giải pháp khắc phục: Nhóm triển khai cơ chế Idempotency-Key theo đặc tả của IETF. Client bắt buộc đính kèm header Idempotency-Key (kiểu GUID) khi gọi API mua gói. Tại tầng cơ sở dữ liệu, bảng IdempotencyRequests được thiết lập ràng buộc khóa duy nhất UNIQUE (UserId, IdempotencyKey) cùng mã băm SHA-256 của payload (RequestHash). Nếu request thứ hai đến cùng khóa và cùng nội dung, hệ thống trả về ngay kết quả của lần xử lý đầu tiên; nếu cùng khóa nhưng khác nội dung payload, hệ thống lập tức từ chối với lỗi 409 Conflict.")

    add_heading_4(doc, "13.2.3. Bug #3 — Mất thông điệp khi RabbitMQ khởi động lại (Dual-Write Inconsistency)")

    add_body(doc,
        "• Bối cảnh phát hiện: Tại Sprint 4, trong bài kiểm thử chịu lỗi (Chaos Testing), nhóm tiến hành tắt đột ngột container RabbitMQ ngay tại thời điểm API vừa lưu trạng thái khởi tạo giao dịch mua gói vào SQL Server. Kết quả là bản ghi trong CSDL đã được tạo nhưng thông điệp PackagePurchaseInitiatedEvent không bao giờ đến được hàng đợi RabbitMQ, khiến giao dịch của người dùng bị treo vĩnh viễn.")

    add_body(doc,
        "• Nguyên nhân gốc rễ (Root Cause): Lỗi kinh điển Dual-Write trong hệ thống phân tán — việc ghi dữ liệu vào SQL Server và việc gửi mạng sang RabbitMQ là hai tiến trình độc lập không nằm chung một giao dịch nguyên tử (Atomic Transaction).")

    add_body(doc,
        "• Giải pháp khắc phục: Nhóm loại bỏ việc gọi Publish trực tiếp tới RabbitMQ trong luồng xử lý HTTP. Thay vào đó, thông điệp sự kiện được tuần tự hóa thành JSON và lưu trực tiếp vào bảng OutboxMessages trong cùng một lệnh SaveChangesAsync() với dữ liệu nghiệp vụ. Sau đó, tiến trình nền OutboxDispatchService định kỳ quét các bản ghi có ProcessedOnUtc IS NULL để đẩy sang RabbitMQ với cơ chế Retry tự động; đồng thời bổ sung bảng InboxMessages tại phía Consumer để chống xử lý trùng lặp (At-least-once delivery kết hợp Idempotent Consumer = Exactly-once processing).")

    # -------------------------------------------------------------------------
    # 13.3. Bài học kinh nghiệm tổng quát
    # -------------------------------------------------------------------------
    add_heading_3(doc, "13.3. Bài học kinh nghiệm tổng quát")

    add_heading_4(doc, "13.3.1. Về thiết kế kiến trúc và mô hình dữ liệu")

    add_body(doc,
        "Qua thực tế xây dựng PropNest, bài học lớn nhất về kiến trúc là không nên lạm dụng Microservices quá sớm khi quy mô đội ngũ và bài toán chưa đòi hỏi. Mô hình Modular Monolith kết hợp Clean Architecture là điểm cân bằng lý tưởng: vừa giữ được sự đơn giản trong triển khai (một CSDL SQL Server duy nhất, dễ dàng đảm bảo ACID), vừa tách biệt ranh giới nghiệp vụ rõ ràng để sẵn sàng tách nhỏ trong tương lai. Bên cạnh đó, việc đặt các quy tắc kiểm tra trạng thái ngay bên trong Domain Entity (như Listing.Approve(), Listing.Reject(), Wallet.Deduct()) giúp mô hình miền luôn ở trạng thái hợp lệ (Always-Valid Domain Model).")

    add_heading_4(doc, "13.3.2. Về kiểm thử và gỡ lỗi trong hệ thống phân tán")

    add_body(doc,
        "Trong các luồng xử lý bất đồng bộ qua Message Broker như Saga, việc dùng công cụ Debug đặt Breakpoint truyền thống gần như mất tác dụng vì tiến trình chạy trải dài trên nhiều luồng và nhiều dịch vụ khác nhau. Nhóm rút ra bài học cốt tử về khả năng quan sát hệ thống (Observability): Mọi HTTP Request ngay khi đi vào hệ thống phải được gán một mã vết CorrelationId thông qua CorrelationIdMiddleware, nạp vào Serilog LogContext và truyền tiếp qua toàn bộ các Message của MassTransit. Nhờ có CorrelationId thống nhất, việc truy vết lỗi từ lúc người dùng bấm nút cho đến khi Worker hoàn tiền chỉ mất vài giây tra cứu log.")

    add_heading_4(doc, "13.3.3. Về quản lý dự án và làm việc nhóm theo mô hình Agile")

    add_body(doc,
        "Việc áp dụng chiến lược phân công theo quyền sở hữu miền nghiệp vụ (Domain Ownership) trong 6 Sprint đã phát huy hiệu quả tối đa. Thay vì chia việc rời rạc theo tầng (người chỉ làm DB, người chỉ làm API), việc giao trọn vẹn từng cụm tính năng (TV1 làm Auth & Moderation, TV2 làm Listing & History, TV3 làm Wallet & Saga) giúp mỗi thành viên hiểu sâu sắc luồng dữ liệu của mình, giảm thiểu tới 80% xung đột mã nguồn (Merge Conflicts) khi hợp nhất nhánh trên GitHub.")

    # =========================================================================
    # CHƯƠNG 14: HƯỚNG PHÁT TRIỂN TRONG TƯƠNG LAI
    # =========================================================================
    add_heading_2(doc, "CHƯƠNG 14: HƯỚNG PHÁT TRIỂN TRONG TƯƠNG LAI")

    add_body(doc,
        "Nền tảng kiến trúc vững chắc hiện tại của PropNest mở ra không gian rộng lớn để nâng cấp và mở rộng hệ thống theo hướng thông minh hóa bằng Trí tuệ Nhân tạo (AI) và tối ưu hóa khả năng chịu tải quy mô lớn (High Scalability).")

    # -------------------------------------------------------------------------
    # 14.1. Hướng phát triển cho hệ thống PropNest
    # -------------------------------------------------------------------------
    add_heading_3(doc, "14.1. Hướng phát triển cho hệ thống PropNest")

    add_heading_4(doc, "14.1.1. Tích hợp động cơ kiểm duyệt tự động AME (FastAPI + Ollama)")

    add_body(doc,
        "Trong giai đoạn tiếp theo, nhóm sẽ hiện thực hóa trọn vẹn phân hệ Kiểm duyệt tự động bằng Trí tuệ Nhân tạo (Automated Moderation Engine - AME) đóng vai trò là lớp lọc thông minh đứng trước Hàng đợi kiểm duyệt viên con người. Dịch vụ AME được thiết kế dưới dạng một Microservice viết bằng Python (FastAPI) giao tiếp bất đồng bộ với PropNest qua RabbitMQ và vận hành mô hình ngôn ngữ lớn cục bộ (Local LLM thông qua Ollama như Qwen 2.5 hoặc Llama 3) để đảm bảo quyền riêng tư dữ liệu và không tốn chi phí API bên thứ ba.")

    add_image_with_caption(doc,
        "docs/diagrams/hinh_2_2_ai_moderation_pipeline.png",
        "Hình 14.1: Kiến trúc đường ống kiểm duyệt tin đăng tự động đa tầng AME (AI Moderation Pipeline)",
        width_inches=6.0)

    add_body(doc,
        "Như minh họa trong Hình 14.1, quy trình thẩm định tự động của AME vận hành qua 4 chốt chặn liên tiếp:")

    add_body(doc,
        "1. Tầng lọc luật cứng (Rule-based Engine): Quét nhanh biểu thức chính quy (Regex) và danh sách từ khóa cấm để chặn tức thì các tin đăng chứa nội dung vi phạm pháp luật, cờ bạc hoặc спам số điện thoại.")

    add_body(doc,
        "2. Tầng phân tích ngữ nghĩa tự nhiên (NLP Local LLM via Ollama): Đánh giá văn phong mô tả bất động sản, phát hiện các dấu hiệu lừa đảo tinh vi (ví dụ: yêu cầu chuyển khoản đặt cọc trước khi xem nhà, cam kết lợi nhuận phi thực tế).")

    add_body(doc,
        "3. Tầng đối soát bất thường giá thị trường (Price Anomaly Detection): So sánh đơn giá trên mỗi mét vuông (VNĐ/m²) của tin đăng với mức giá trung vị của cùng Quận/Huyện và cùng loại hình bất động sản để phát hiện tin đăng \"giá ảo\" câu tương tác.")

    add_body(doc,
        "4. Cơ chế phân luồng quyết định (Fallback Mechanism): Nếu điểm tin cậy (Confidence Score) đạt trên 85%, hệ thống tự động phê duyệt (Auto-Approve); nếu phát hiện vi phạm rõ ràng, hệ thống tự động từ chối (Auto-Reject); đối với các trường hợp nằm trong \"vùng xám\" (nghi ngờ), tin đăng được gắn nhãn cảnh báo kèm gợi ý của AI và chuyển vào hàng đợi để Kiểm duyệt viên con người ra quyết định cuối cùng.")

    add_image_with_caption(doc,
        "docs/diagrams/hinh_4_2_hieu_nang_ai_moderation.png",
        "Hình 14.2: Biểu đồ đánh giá kỳ vọng hiệu năng và độ chính xác của mô hình kiểm duyệt kết hợp AI",
        width_inches=6.0)

    add_heading_4(doc, "14.1.2. Mở rộng kiểm duyệt hình ảnh, phát hiện watermark và nội dung nhạy cảm")

    add_body(doc,
        "Song song với xử lý văn bản, hệ thống sẽ tích hợp thêm mô hình Thị giác máy tính (Computer Vision) và Nhận dạng ký tự quang học (OCR như PaddleOCR) vào luồng upload hình ảnh tin đăng nhằm tự động hóa các tác vụ:")

    add_body(doc,
        "• Phát hiện và cảnh báo ảnh chứa logo, đóng dấu bản quyền (Watermark) của các sàn giao dịch đối thủ hoặc ảnh chèn đè số điện thoại cỡ lớn gây mất mỹ quan.")

    add_body(doc,
        "• Phân loại hình ảnh thông minh: Đảm bảo ảnh tải lên thực sự là hình ảnh nội/ngoại thất bất động sản hoặc giấy tờ pháp lý (sổ hồng), tự động chặn các hình ảnh nhạy cảm, ảnh mờ nhòe hoặc ảnh không liên quan.")

    add_heading_4(doc, "14.1.3. Tối ưu tìm kiếm nâng cao với Elasticsearch và Caching Redis")

    add_body(doc,
        "Khi số lượng tin đăng vượt ngưỡng hàng trăm nghìn bản ghi, nhóm định hướng nâng cấp hạ tầng truy vấn theo hai mũi nhọn:")

    add_body(doc,
        "• Tích hợp công cụ tìm kiếm Elasticsearch: Đồng bộ các tin đăng ở trạng thái Published từ SQL Server sang Elasticsearch thông qua sự kiện Outbox. Giải pháp này cho phép tìm kiếm toàn văn (Full-text Search) tốc độ mili-giây, hỗ trợ tìm kiếm không dấu, chịu lỗi chính tả (Fuzzy Search) và tìm kiếm theo bán kính tọa độ địa lý (Geo-spatial Query: tìm căn hộ trong bán kính 2km quanh trạm Metro hoặc trường học).")

    add_body(doc,
        "• Triển khai bộ nhớ đệm phân tán Redis (Distributed Caching): Áp dụng mẫu thiết kế Cache-Aside với Redis để lưu đệm danh sách tin đăng VIP trang chủ, cấu trúc danh mục hành chính (Tỉnh/Thành, Quận/Huyện, Phường/Xã) và thông tin cấu hình gói cước, giúp giảm tới 80% tải truy vấn đọc trực tiếp vào cơ sở dữ liệu SQL Server.")

    # -------------------------------------------------------------------------
    # 14.2. Định hướng phát triển của bản thân
    # -------------------------------------------------------------------------
    add_heading_3(doc, "14.2. Định hướng phát triển của bản thân")

    add_heading_4(doc, "14.2.1. Tự đánh giá năng lực công nghệ backend sau đồ án")

    add_body(doc,
        "Việc trực tiếp nghiên cứu và xây dựng hệ thống PropNest từ con số không đã mang lại bước chuyển biến lớn về tư duy kỹ thuật cho toàn bộ các thành viên trong nhóm. Từ chỗ chỉ quen thuộc với các ứng dụng CRUD đơn giản trên một tiến trình duy nhất, nhóm đã làm chủ được các kỹ thuật cốt lõi của một kỹ sư Backend thực thụ: Thiết kế kiến trúc phân tầng Clean Architecture, kiểm soát truy cập chuyên sâu với JWT & Policy-based Authorization, xử lý bài toán đồng thời với Optimistic Concurrency, và đặc biệt là tư duy thiết kế giao dịch phân tán chịu lỗi với Saga Orchestration và Transactional Outbox.")

    add_heading_4(doc, "14.2.2. Lộ trình học tập chuyên sâu: Cloud Native, Kubernetes, Microservices")

    add_body(doc,
        "Tiếp nối nền tảng ASP.NET Core vững chắc từ đồ án này, các thành viên trong nhóm đã vạch ra lộ trình học tập chuyên sâu trong giai đoạn tiếp theo:")

    add_body(doc,
        "• Làm chủ công nghệ .NET Aspire và hệ sinh thái .NET 9 / .NET 10 LTS phục vụ phát triển ứng dụng Cloud-Native hiện đại.")

    add_body(doc,
        "• Nghiên cứu chuyên sâu về điều phối vùng chứa với Kubernetes (K8s), giao tiếp nội bộ hiệu năng cao qua gRPC, và thiết lập hệ thống giám sát toàn diện (Observability Stack) với OpenTelemetry, Prometheus, Grafana và Seq.")

    add_body(doc,
        "• Tìm hiểu các dịch vụ đám mây thực tế trên Microsoft Azure (Azure App Service, Azure SQL, Azure Service Bus, Azure Key Vault) hướng tới các chứng chỉ chuyên nghiệp như Microsoft Certified: Azure Developer Associate (AZ-204).")

    add_heading_4(doc, "14.2.3. Kế hoạch xây dựng Portfolio và chuẩn bị ứng tuyển vị trí .NET Developer")

    add_body(doc,
        "Dự án PropNest cùng toàn bộ tài liệu kiến trúc, sơ đồ thiết kế và bộ kiểm thử tự động trong đồ án này sẽ được nhóm chuẩn hóa thành dự án trọng điểm (Flagship Project) trong Hồ sơ năng lực cá nhân (Technical Portfolio). Đây là minh chứng thuyết phục nhất về năng lực giải quyết các bài toán kỹ thuật phức tạp (tài chính ví, chống xung đột đồng thời, bồi hoàn giao dịch phân tán) khi tham gia ứng tuyển vào vị trí Backend .NET Engineer tại các doanh nghiệp công nghệ và thương mại điện tử.")

    # =========================================================================
    # KẾT LUẬN CHUNG
    # =========================================================================
    add_heading_2(doc, "KẾT LUẬN CHUNG")

    add_body(doc,
        "Báo cáo đồ án \"Nghiên cứu công nghệ ASP.NET Core và Xây dựng hệ thống đăng tin bất động sản PropNest\" đã hoàn thành trọn vẹn toàn bộ các mục tiêu nghiên cứu lý luận và thực nghiệm kỹ thuật đã đề ra. Xuyên suốt 14 chương của báo cáo, nhóm đã đi từ việc khảo sát bức tranh tổng quan của nền tảng .NET hiện đại, phân tích chuyên sâu bản chất từng thành phần cốt lõi trong kiến trúc Backend (Kestrel, Middleware Pipeline, Dependency Injection, Entity Framework Core, FluentValidation, Identity & JWT, MassTransit Saga, Quartz.NET, Serilog, xUnit, Docker), cho đến việc kết tinh toàn bộ các công nghệ đó vào một hệ sinh thái phần mềm thực tế hoàn chỉnh.")

    add_body(doc,
        "Hệ thống PropNest không chỉ dừng lại ở việc đáp ứng đầy đủ các chức năng nghiệp vụ của một sàn giao dịch bất động sản trực tuyến trên cả 3 cổng giao diện (Client, Seller, Admin), mà còn giải quyết triệt để ba bài toán kỹ nghệ phần mềm hóc búa: (1) Bảo toàn tuyệt đối tính toàn vẹn của giao dịch tài chính mua gói dịch vụ thông qua Saga Orchestrator kết hợp Transactional Outbox và Idempotency Key; (2) Ngăn chặn hoàn toàn xung đột ghi đè đồng thời bằng cơ chế Optimistic Concurrency Control với trường RowVersion; và (3) Tối ưu hóa trên 92% dung lượng lưu trữ lịch sử biến động dữ liệu nhờ thuật toán History Delta Engine.")

    add_body(doc,
        "Những kết quả thực nghiệm minh bạch cùng bộ mã nguồn chuẩn mực của đồ án là minh chứng rõ nét cho sức mạnh, hiệu năng và độ tin cậy của công nghệ ASP.NET Core trong việc xây dựng các hệ thống thông tin doanh nghiệp quy mô lớn.")

    # =========================================================================
    # PHỤ LỤC & TÀI LIỆU THAM KHẢO
    # =========================================================================
    add_heading_2(doc, "PHỤ LỤC")

    # -------------------------------------------------------------------------
    # Phụ lục 1: Nhật ký công việc và phân chia đóng góp
    # -------------------------------------------------------------------------
    add_heading_3(doc, "1. Nhật ký công việc và phân chia đóng góp")

    add_body(doc,
        "Dự án được triển khai trong 6 tuần (tương ứng 6 Sprint) bởi 3 thành viên theo mô hình Domain Ownership — mỗi thành viên chịu trách nhiệm xuyên suốt từ tầng Backend API cho đến kiểm thử và tích hợp trên nhóm phân hệ được giao:")

    headers_mem = ["STT", "Họ và tên sinh viên", "MSSV", "Phân hệ phụ trách (Domain Ownership)", "Khối lượng hoàn thành", "Tỷ lệ đóng góp"]
    data_mem = [
        ["1", "Nguyễn Hoàng Phúc", "3122560060", "Module Listing (CRUD, Filter, Images) + History Delta Engine + Giao diện Client Portal & Seller Portal", "14/14 Tasks (F02, F04, UI)", "33.3% (100% cam kết)"],
        ["2", "Nguyễn Gia Bảo", "3122560004", "Module Wallet & Transactions + PurchasePackageSaga + Transactional Outbox/Inbox + Idempotency + Workflows", "16/16 Tasks (F05, F06, F08)", "33.4% (100% cam kết)"],
        ["3", "Đoàn Tuấn Tài", "3122410365", "Module Auth (Identity, JWT, Lockout) + Moderation API & Dashboard Stats + Operations (Serilog, Quartz, Health) + Admin Portal", "16/16 Tasks (F01, F03, F07, F09)", "33.3% (100% cam kết)"]
    ]
    add_body(doc, "Bảng PL.1: Bảng phân công trách nhiệm và đánh giá mức độ đóng góp của các thành viên trong nhóm", bold=True, italic=True)
    add_styled_table(doc, headers_mem, data_mem, col_widths=[0.5, 1.4, 0.9, 2.1, 1.1, 1.0])

    add_body(doc, "Nhật ký thực hiện công việc chi tiết qua 6 Sprint phát triển của dự án:")

    headers_sp = ["Giai đoạn", "Mục tiêu trọng tâm của Sprint", "Các hạng mục công việc tiêu biểu (Tasks)", "Thành viên thực hiện", "Trạng thái"]
    data_sp = [
        ["Sprint 1\n(Tuần 1)", "Khởi tạo kiến trúc Solution, CSDL & Foundation APIs", "Thiết lập Clean Architecture, PropNestDbContext, Migration đầu tiên; 구현 POST /auth/register, POST /auth/login, JwtTokenService; GET /listings có phân trang và bộ lọc; khởi tạo thực thể Wallet", "Bảo, Phúc, Tài", "Hoàn thành (Done)"],
        ["Sprint 2\n(Tuần 2)", "Hoàn thiện CRUD Listing, RowVersion OCC & Nạp tiền Ví", "Implement POST/PUT/DELETE /listings, Submit/Hide/Unhide tin đăng; tích hợp kiểm soát đồng thời RowVersion (409 Conflict); API GET /wallets/me và POST /wallets/top-up; viết Unit Test cho Listing State Transitions (Task 182)", "Phúc, Bảo, Tài", "Hoàn thành (Done)"],
        ["Sprint 3\n(Tuần 3)", "Phân hệ Kiểm duyệt Moderation, History Delta & Idempotency", "Implement GET /moderation/pending, Approve/Reject listing, Policy CanModerateListings; xây dựng thuật toán DeltaEngine, API GET /listings/{id}/history (Task 187, 188); xây dựng bảng IdempotencyRequests & RequestHash", "Tài, Phúc, Bảo", "Hoàn thành (Done)"],
        ["Sprint 4\n(Tuần 4)", "Tích hợp RabbitMQ, MassTransit & PurchasePackageSaga", "Cài đặt MassTransit + RabbitMQ (Task 194); thiết kế PurchasePackageSaga 9 trạng thái (Task 195); viết DeductWalletConsumer (Task 196), UpgradeListingConsumer (Task 197) và RefundWalletConsumer bồi hoàn tự động (Task 198)", "Bảo, Phúc", "Hoàn thành (Done)"],
        ["Sprint 5\n(Tuần 5)", "Transactional Outbox, Inbox Deduplication & Web Portals", "Xây dựng OutboxDispatchService (Task 199), InboxMessage deduplication filter (Task 200); hoàn thiện Client Portal, Seller Portal (My Listings, Form Tạo/Sửa, Trang Ví, Flow mua gói - Task 210..213)", "Bảo, Phúc, Tài", "Hoàn thành (Done)"],
        ["Sprint 6\n(Tuần 6)", "Hoàn thiện Admin Portal, Kiểm thử toàn diện & Báo cáo", "Xây dựng Admin Moderation Queue & Dashboard Stats (Task 214); hoàn thiện API GET /workflows/{correlationId} (Task 205); viết Unit Test cho Idempotency logic (Task 206); rà soát đồng bộ báo cáo đồ án", "Tài, Phúc, Bảo", "Hoàn thành (Done)"]
    ]
    add_body(doc, "Bảng PL.2: Nhật ký theo dõi tiến độ thực hiện đồ án qua 6 Sprint", bold=True, italic=True)
    add_styled_table(doc, headers_sp, data_sp, col_widths=[0.9, 1.5, 2.7, 1.0, 0.9])

    # -------------------------------------------------------------------------
    # Phụ lục 2: Minh chứng đóng góp và lịch sử Git Commits
    # -------------------------------------------------------------------------
    add_heading_3(doc, "2. Minh chứng đóng góp và lịch sử Git Commits thực tế")

    add_body(doc,
        "Toàn bộ quá trình phát triển mã nguồn của nhóm được quản lý phiên bản chặt chẽ trên kho lưu trữ Git/GitHub với quy trình tạo nhánh theo tính năng (Feature Branches) và hợp nhất qua Pull Request. Dưới đây là trích xuất lịch sử commit thực tế từ câu lệnh git log của dự án:")

    add_code_box(doc,
        '$ git log --pretty=format:"%h | %an | %ad | %s" --date=short\n\n'
        '4d87ccf | Nguyen Gia Bao | 2026-10-29 | chore: thêm case middleware chuẩn hóa xử lý lỗi\n'
        '35d24aa | Nguyen Gia Bao | 2026-10-29 | hotfix: sửa chính sách giá gói Standard và luồng duyệt tin\n'
        '937c7e7 | Nguyen Gia Bao | 2026-10-28 | feat: Viết unit test cho Idempotency logic - Task 206\n'
        '5225c16 | Nguyen Gia Bao | 2026-10-28 | feat: Hoàn thiện GET /workflows/{correlationId} response - Task 205\n'
        'f76ec88 | phucnguyen0704 | 2026-10-27 | Merge pull request #23 from nguyengiabao91753/phuc/seller-history\n'
        '4f9272e | Phúc Nguyễn    | 2026-10-23 | feat: Build trang xem lịch sử thay đổi tin (Listing History) - Task 214\n'
        '8be16a7 | Phúc Nguyễn    | 2026-10-23 | feat: Build flow mua gói dịch vụ (Purchase Package) - Task 213\n'
        '8a66b8f | Đoàn Tuấn Tài  | 2026-10-23 | feat(admin): build Moderation Queue, Detail, and Dashboard with real API data\n'
        '80c4c40 | Nguyen Gia Bao | 2026-10-22 | chore: Chỉnh sửa WorkflowStep cho mỗi bước Saga transition - Task 204\n'
        'fad4c7e | Đoàn Tuấn Tài  | 2026-10-22 | feat: complete listing creation and moderation flow\n'
        '51acfd7 | Phúc Nguyễn    | 2026-10-21 | feat: Build trang ví Seller (Wallet) - Task 212\n'
        '107b0b7 | Đoàn Tuấn Tài  | 2026-10-21 | Merge PR #16 from tai/Unit-Test-Auth-Mode-Serilog-Quartz-Listing-Page\n'
        '33a362a | Phúc Nguyễn    | 2026-10-18 | feat: Build form tạo/sửa tin đăng - Task 211\n'
        'c89f32a | Phúc Nguyễn    | 2026-10-18 | feat: Build trang quản lý tin đăng của Seller (My Listings) - Task 210\n'
        '1776af7 | Nguyen Gia Bao | 2026-10-16 | feat: Tạo InboxMessage entity và consumer deduplication filter - Task 200\n'
        '5f8dfa4 | Nguyen Gia Bao | 2026-10-16 | feat: Chuyển OutboxDispatchService sang MassTransit publisher - Task 199\n'
        '367c46c | Nguyen Gia Bao | 2026-10-16 | feat: Viết RefundWalletConsumer (compensation) - Task 198\n'
        'd7f49b8 | Nguyen Gia Bao | 2026-10-16 | feat: Viết UpgradeListingConsumer - Task 197\n'
        'fa74144 | Nguyen Gia Bao | 2026-10-15 | feat: DeductWalletConsumer - Task 196\n'
        'c5ddb19 | Nguyen Gia Bao | 2026-10-15 | feat: Viết PurchasePackageSaga state machine - Task 195\n'
        '50922ad | Nguyen Gia Bao | 2026-10-15 | feat: Cài đặt MassTransit + RabbitMQ transport - Task 194\n'
        '517e157 | Phúc Nguyễn    | 2026-10-15 | feat: Viết unit test cho DeltaEngine - Task 188\n'
        'bcc3317 | Đoàn Tuấn Tài  | 2026-10-14 | feat: add role moderation listing APIs\n'
        'bc18f01 | Nguyen Gia Bao | 2026-10-14 | feat: Implement GET /api/v1/wallets/me/transactions - Task 193\n'
        '694b4c0 | Phúc Nguyễn    | 2026-10-13 | feat: Implement GET /api/v1/listings/{id}/history - Task 187\n'
        'aa66101 | Phúc Nguyễn    | 2026-10-12 | feat: Viết unit test cho Listing state transitions - Task 182'
    )

    # -------------------------------------------------------------------------
    # Phụ lục 3: Công bố sử dụng Trí tuệ Nhân tạo (AI Disclosure)
    # -------------------------------------------------------------------------
    add_heading_3(doc, "3. Công bố sử dụng Trí tuệ Nhân tạo (AI Disclosure) và Nhật ký kiểm chứng lỗi")

    add_body(doc,
        "Tuân thủ quy định về liêm chính học thuật và hướng dẫn sử dụng công cụ AI tạo sinh trong quá trình thực hiện đồ án, nhóm xin công bố minh bạch phạm vi ứng dụng AI và phân tích chi tiết các tình huống công cụ AI đưa ra thông tin/mã nguồn sai lệch đã được nhóm tự phát hiện, kiểm chứng và khắc phục:")

    headers_ai = ["Tình huống", "Nội dung AI trả lời sai hoặc lỗi thời", "Cách nhóm phát hiện và kiểm chứng", "Giải pháp hiệu chỉnh chính xác của nhóm"]
    data_ai = [
        ["Ca lỗi #1:\nKiến trúc tầng mạng của Kestrel và thuật toán băm mật khẩu Identity", "AI mô tả rằng máy chủ Kestrel trong ASP.NET Core 8 hoạt động dựa trên thư viện libuv, và PasswordHasher mặc định của Identity dùng thuật toán PBKDF2 với HMAC-SHA256 (10.000 vòng lặp).", "Nhóm đối chiếu trực tiếp với tài liệu chính thức của Microsoft Learn cho .NET 8 và kiểm tra mã nguồn mở của lớp PasswordHasher<TUser> trong kho aspnetcore trên GitHub.", "Chỉnh sửa lại chính xác: Kestrel đã loại bỏ libuv từ ASP.NET Core 3.0 để chuyển sang dùng System.Net.Sockets (Managed Sockets) thuần .NET; Identity V3 trong .NET 8 mặc định dùng PBKDF2 với HMAC-SHA512 và 100.000 vòng lặp."],
        ["Ca lỗi #2:\nÁnh xạ ngoại lệ trong ExceptionHandlingMiddleware", "AI gợi ý viết câu lệnh switch ánh xạ toàn bộ ngoại lệ kiểu InvalidOperationException thành mã HTTP 409 Conflict và gán trực tiếp Detail = exception.Message trả về cho Client.", "Khi chạy kiểm thử tích hợp với lỗi cấu hình Dependency Injection thiếu service, hệ thống cũng ném InvalidOperationException nhưng API lại báo 409 Conflict thay vì 500; đồng thời làm lộ thông tin nhạy cảm của nội bộ EF Core.", "Tách biệt rõ ràng ngoại lệ nghiệp vụ và ngoại lệ hệ thống: Chỉ trả 409 cho DbUpdateConcurrencyException và xung đột trạng thái nghiệp vụ xác định; bổ sung kiểm tra context.Response.HasStarted và ẩn chi tiết lỗi hệ thống ở mã 500."],
        ["Ca lỗi #3:\nCấu hình máy trạng thái MassTransit Saga (PurchasePackageSaga)", "AI sinh mã mẫu cho PurchasePackageSaga chỉ khai báo 5 trạng thái nhưng thiếu các khối xử lý During(Compensating), dẫn đến khi xảy ra lỗi nâng cấp tin thì Saga bị kẹt ở trạng thái trung gian và không bao giờ chuyển sang Compensated.", "Nhóm viết kịch bản kiểm thử giả lập lỗi nâng cấp tin (Task 198) và theo dõi bảng PurchasePackageSagaStates trong SQL Server, phát hiện cột CurrentState dừng ở Compensating dù tiền đã hoàn.", "Viết lại đầy đủ 9 trạng thái và 6 sự kiện trong PurchasePackageSaga.cs, bổ sung khối During(Compensating, When(WalletRefundedEvent).TransitionTo(Compensated)) và kiểm chứng lại bằng log thực tế."]
    ]
    add_body(doc, "Bảng PL.3: Nhật ký phân tích các trường hợp AI gợi ý sai và quá trình nhóm kiểm chứng, sửa lỗi", bold=True, italic=True)
    add_styled_table(doc, headers_ai, data_ai, col_widths=[1.1, 1.9, 1.9, 2.1])

    # -------------------------------------------------------------------------
    # Phụ lục 4: Danh mục Tài liệu tham khảo (References)
    # -------------------------------------------------------------------------
    add_heading_3(doc, "4. Danh mục Tài liệu tham khảo")

    refs = [
        "[1] Microsoft Corporation, \"ASP.NET Core Documentation (.NET 8.0 LTS & .NET 10.0),\" Microsoft Learn, 2025. [Trực tuyến]. Địa chỉ: https://learn.microsoft.com/en-us/aspnet/core/ (Truy cập ngày: 15/10/2026).",
        "[2] Microsoft Corporation, \"Kestrel web server implementation in ASP.NET Core,\" Microsoft Learn, 2025. [Trực tuyến]. Địa chỉ: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel (Truy cập ngày: 15/10/2026).",
        "[3] Microsoft Corporation, \"Entity Framework Core 8: Optimistic Concurrency and Conflict Resolution,\" Microsoft Learn, 2025. [Trực tuyến]. Địa chỉ: https://learn.microsoft.com/en-us/ef/core/saving/concurrency (Truy cập ngày: 16/10/2026).",
        "[4] Microsoft Corporation, \"Introduction to Identity on ASP.NET Core & Password Hasher Options,\" Microsoft Learn, 2025. [Trực tuyến]. Địa chỉ: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity (Truy cập ngày: 18/10/2026).",
        "[5] M. Nottingham và E. Wilde, \"RFC 7807: Problem Details for HTTP APIs,\" Internet Engineering Task Force (IETF), Tháng 3/2016. [Trực tuyến]. Địa chỉ: https://datatracker.ietf.org/doc/html/rfc7807 (Truy cập ngày: 18/10/2026).",
        "[6] S. Dalal và cộng sự, \"The Idempotency-Key HTTP Header Field (Draft-IETF-HTTPAPI-Idempotency-Key-Header),\" Internet Engineering Task Force (IETF), 2024. [Trực tuyến]. Địa chỉ: https://datatracker.ietf.org/doc/html/draft-ietf-httpapi-idempotency-key-header (Truy cập ngày: 20/10/2026).",
        "[7] C. Patterson và MassTransit Contributors, \"MassTransit Documentation: Saga State Machines & Transactional Outbox,\" MassTransit Project, 2025. [Trực tuyến]. Địa chỉ: https://masstransit.io/documentation/patterns/saga (Truy cập ngày: 20/10/2026).",
        "[8] C. Richardson, Microservices Patterns: With Examples in Java (Chương 4: Managing Transactions with Sagas). Shelter Island, NY, USA: Manning Publications, 2018.",
        "[9] R. C. Martin, Clean Architecture: A Craftsman's Guide to Software Structure and Design. Boston, MA, USA: Prentice Hall, 2017.",
        "[10] M. Fowler, \"MonolithFirst & Modular Monolith Architecture,\" martinfowler.com, 2015. [Trực tuyến]. Địa chỉ: https://martinfowler.com/bliki/MonolithFirst.html (Truy cập ngày: 22/10/2026)."
    ]
    for r_text in refs:
        add_body(doc, r_text)

def generate_standalone_part_4():
    doc = docx.Document()
    for sec in doc.sections:
        sec.top_margin = Inches(1.0)
        sec.bottom_margin = Inches(1.0)
        sec.left_margin = Inches(1.0)
        sec.right_margin = Inches(1.0)

    p_title = doc.add_paragraph()
    set_para_font(p_title, "BÁO CÁO ĐỒ ÁN CÁC CÔNG NGHỆ LẬP TRÌNH HIỆN ĐẠI", bold=True, size=15, color_rgb=(24, 90, 188), align=WD_ALIGN_PARAGRAPH.CENTER, space_after=2)
    
    p_sub = doc.add_paragraph()
    set_para_font(p_sub, "PHẦN 4: TỔNG KẾT VÀ ĐỊNH HƯỚNG (CHƯƠNG 13, CHƯƠNG 14, KẾT LUẬN CHUNG & PHỤ LỤC)", bold=True, size=13, color_rgb=(71, 85, 105), align=WD_ALIGN_PARAGRAPH.CENTER, space_after=18)

    populate_chapter_13_to_end(doc)

    out_path = "docs/Chuong13_14_KetLuan_PhuLuc.docx"
    doc.save(out_path)
    print(f"Successfully generated standalone Part 4 document: {out_path}")

def update_main_report():
    main_doc_path = "docs/Báo cáo chung Các công nghệ LTHĐ.docx"
    doc = docx.Document(main_doc_path)
    
    start_p = None
    for p in doc.paragraphs:
        if "CHƯƠNG 13: ĐÁNH GIÁ VÀ NHÌN LẠI" in p.text:
            start_p = p
            break
            
    if start_p is None:
        print("Could not locate CHƯƠNG 13: ĐÁNH GIÁ VÀ NHÌN LẠI in main doc")
        return

    # Collect all elements from start_p to the end of body (excluding w:sectPr)
    elements_to_remove = []
    curr = start_p._p
    sect_pr = None
    while curr is not None:
        tag = curr.tag.split('}')[-1]
        nxt = curr.getnext()
        if tag == 'sectPr':
            sect_pr = curr
        else:
            elements_to_remove.append(curr)
        curr = nxt

    print(f"Found {len(elements_to_remove)} elements from CHƯƠNG 13 to end of document to replace.")

    temp_doc = docx.Document()
    populate_chapter_13_to_end(temp_doc)

    # Insert new elements right before sect_pr (or append to body if no sect_pr)
    for child in list(temp_doc.element.body):
        tag = child.tag.split('}')[-1]
        if tag in ('p', 'tbl'):
            if sect_pr is not None:
                sect_pr.addprevious(child)
            else:
                doc.element.body.append(child)

    for elem in elements_to_remove:
        elem.getparent().remove(elem)

    doc.save(main_doc_path)
    print(f"Main report updated successfully through the very end: {main_doc_path}!")

if __name__ == "__main__":
    generate_standalone_part_4()
    update_main_report()

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

def create_element(name):
    return OxmlElement(name)

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
    
    # Header row
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

    # Data rows
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

    # Set column widths if provided
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

def add_heading_3(doc, text):
    p = doc.add_paragraph()
    return set_para_font(p, text, bold=True, size=14, color_rgb=(15, 23, 42), align=WD_ALIGN_PARAGRAPH.LEFT, space_before=14, space_after=6)

def add_heading_4(doc, text):
    p = doc.add_paragraph()
    return set_para_font(p, text, bold=True, size=13, color_rgb=(30, 41, 59), align=WD_ALIGN_PARAGRAPH.LEFT, space_before=10, space_after=4)

def add_body(doc, text, bold=False, italic=False):
    p = doc.add_paragraph()
    return set_para_font(p, text, bold=bold, italic=italic, size=13, color_rgb=(0,0,0), align=WD_ALIGN_PARAGRAPH.JUSTIFY, space_before=0, space_after=6, line_spacing=1.25)

def add_callout(doc, text):
    tbl = doc.add_table(rows=1, cols=1)
    tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
    cell = tbl.cell(0, 0)
    set_cell_shading(cell, "EFF6FF")
    set_cell_margins(cell, top=100, bottom=100, left=160, right=160)
    set_cell_border(cell,
        top={'val': 'none'}, bottom={'val': 'none'}, right={'val': 'none'},
        left={'val': 'single', 'sz': 18, 'color': '2563EB'}
    )
    p = cell.paragraphs[0]
    p.paragraph_format.space_before = Pt(2)
    p.paragraph_format.space_after = Pt(2)
    p.paragraph_format.line_spacing = 1.2
    run = p.add_run(text)
    run.font.name = "Times New Roman"
    run.font.size = Pt(11.5)
    run.font.italic = True
    run.font.color.rgb = RGBColor(30, 58, 138)
    doc.add_paragraph()

def populate_chapter_11_content(doc):
    # =========================================================================
    # 11.3. THIẾT KẾ DANH MỤC API RESTFUL
    # =========================================================================
    add_heading_3(doc, "11.3. Thiết kế danh mục API RESTful")
    
    add_body(doc, 
        "Trong kiến trúc hướng dịch vụ hiện đại, giao diện lập trình ứng dụng RESTful API đóng vai trò là hợp đồng giao tiếp (Communication Contract) chuẩn mực giữa các tiến trình backend và các ứng dụng client (bao gồm Web Portal cho người bán, Ứng dụng quản trị cho kiểm duyệt viên, và Cổng thông tin công khai cho người mua). Hệ thống PropNest tuân thủ nghiêm ngặt mô hình trưởng thành Richardson Cấp độ 2 (Richardson Maturity Model - Level 2: Resources & HTTP Verbs), trong đó mọi tài nguyên nghiệp vụ đều được định danh bằng danh từ số nhiều và tiền tố phiên bản (/api/v1/...), đồng thời ánh xạ trực tiếp các thao tác vào các động từ chuẩn của giao thức HTTP (GET, POST, PUT, DELETE).")

    add_body(doc,
        "Để đảm bảo tính nhất quán, bảo mật và khả năng bảo trì lâu dài, toàn bộ danh mục API của PropNest được thiết kế dựa trên các nguyên tắc kiến trúc thống nhất sau:")

    add_body(doc,
        "• Phân cấp xác thực và kiểm soát quyền truy cập chặt chẽ: Các API được phân chia thành 3 tầng bảo mật rõ rệt: (1) Tầng công khai [AllowAnonymous] dành cho người tìm kiếm bất động sản; (2) Tầng người bán [Authorize(Roles = \"Seller\")] yêu cầu Access Token JWT hợp lệ để quản lý tin đăng và ví cá nhân; (3) Tầng quản trị [Authorize(Policy = \"CanModerateListings\")] yêu cầu người dùng phải sở hữu vai trò Moderator hoặc Admin để thực hiện các thao tác kiểm duyệt và xem thống kê kinh doanh.")

    add_body(doc,
        "• Chuẩn hóa dữ liệu trao đổi qua JSON: Mọi request và response body đều sử dụng bảng mã UTF-8 với định dạng JSON (JavaScript Object Notation), quy ước đặt tên thuộc tính theo kiểu camelCase. Các mốc thời gian được chuẩn hóa theo định dạng ISO 8601 kèm múi giờ Việt Nam (+07:00) thông qua bộ chuyển đổi tùy biến VietnamDateTimeOffsetJsonConverter.")

    add_body(doc,
        "• Chuẩn hóa dữ liệu phân trang (Pagination): Các API truy vấn danh sách khối lượng lớn đều áp dụng mô hình phân trang chuẩn PagedResult<T> gồm mảng dữ liệu Items kèm siêu dữ liệu điều hướng (TotalCount, PageNumber, PageSize, TotalPages, HasPreviousPage, HasNextPage), giúp tối ưu hóa hiệu năng máy chủ và trải nghiệm giao diện người dùng.")

    add_body(doc,
        "• Chuẩn hóa thông điệp lỗi theo đặc tả RFC 7807 (ProblemDetails): Toàn bộ các phản hồi lỗi từ 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 409 Conflict cho đến 500 Internal Server Error đều trả về cấu trúc ProblemDetails hoặc ValidationProblemDetails thống nhất, đi kèm mã truy vết traceId/correlationId phục vụ việc hỗ trợ kỹ thuật và giám sát hệ thống.")

    # -------------------------------------------------------------------------
    # 11.3.1. Nhóm API Xác thực và Hồ sơ (/api/v1/auth)
    # -------------------------------------------------------------------------
    add_heading_4(doc, "11.3.1. Nhóm API Xác thực và Hồ sơ (/api/v1/auth)")

    add_body(doc,
        "Nhóm API Xác thực chịu trách nhiệm quản lý vòng đời tài khoản người dùng, cấp phát mã truy cập bảo mật JSON Web Token (JWT), và thực thi cơ chế phòng thủ chống các cuộc tấn công vét cạn mật khẩu (Brute-force Attacks) thông qua tính năng khóa tài khoản tạm thời (Lockout) của ASP.NET Core Identity.")

    headers_auth = ["HTTP Verb", "Đường dẫn URI", "Phân quyền", "Mô tả nghiệp vụ", "HTTP Status Code"]
    data_auth = [
        ["POST", "/api/v1/auth/register", "AllowAnonymous", "Đăng ký tài khoản Seller mới, tự động khởi tạo Ví tiền cá nhân", "201 Created\n400 Bad Request"],
        ["POST", "/api/v1/auth/login", "AllowAnonymous", "Xác thực danh tính qua PBKDF2 HMAC-SHA512, cấp JWT Token kèm cơ chế Lockout 5 lần/15 phút", "200 OK\n401 Unauthorized\n423 Locked"]
    ]
    add_body(doc, "Bảng 11.1: Danh mục các Endpoint trong Nhóm API Xác thực (/api/v1/auth)", bold=True, italic=True)
    add_styled_table(doc, headers_auth, data_auth, col_widths=[0.9, 2.0, 1.2, 2.0, 1.1])

    add_body(doc, "Cấu trúc trao đổi dữ liệu JSON mẫu của API Đăng ký tài khoản:")
    add_code_box(doc, 
        '// Request Body: RegisterRequest\n'
        '{\n'
        '  "email": "seller01@propnest.vn",\n'
        '  "password": "Password123!",\n'
        '  "fullName": "Nguyễn Văn An",\n'
        '  "phoneNumber": "0912345678"\n'
        '}\n\n'
        '// Response: 201 Created kèm header Location\n'
        '{\n'
        '  "userId": 105,\n'
        '  "email": "seller01@propnest.vn"\n'
        '}'
    )

    add_body(doc, "Cấu trúc trao đổi dữ liệu JSON mẫu của API Đăng nhập và kịch bản khóa tài khoản (Lockout):")
    add_code_box(doc,
        '// Request Body: LoginRequest\n'
        '{\n'
        '  "email": "seller01@propnest.vn",\n'
        '  "password": "Password123!"\n'
        '}\n\n'
        '// Response: 200 OK khi đăng nhập thành công\n'
        '{\n'
        '  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMDUiLCJlbWFpbCI6InNlbGxlcjAxQHByb3BuZXN0LnZuIiwicm9sZSI6IlNlbGxlciIsImlzcyI6IlByb3BOZXN0IiwiYXVkIjoiUHJvcE5lc3QuQ2xpZW50IiwiZXhwIjoxNzkzNDQ2MDAwfQ..."\n'
        '}\n\n'
        '// Response: 423 Locked khi vượt quá 5 lần nhập sai mật khẩu liên tiếp\n'
        '{\n'
        '  "message": "Tài khoản tạm thời bị khóa do nhập sai mật khẩu quá 5 lần. Vui lòng thử lại sau 15 phút."\n'
        '}'
    )

    # -------------------------------------------------------------------------
    # 11.3.2. Nhóm API Tin đăng (/api/v1/listings)
    # -------------------------------------------------------------------------
    add_heading_4(doc, "11.3.2. Nhóm API Tin đăng (/api/v1/listings)")

    add_body(doc,
        "Nhóm API Tin đăng là phân hệ nghiệp vụ trọng tâm, cung cấp các thao tác quản lý toàn bộ vòng đời tin đăng bất động sản (từ khởi tạo bản nháp, chỉnh sửa nội dung, gửi kiểm duyệt, ẩn/hiện tin, cho đến theo dõi lịch sử biến động và upload thư viện hình ảnh minh họa).")

    headers_listings = ["HTTP Verb", "Đường dẫn URI", "Phân quyền", "Mô tả nghiệp vụ", "HTTP Status Code"]
    data_listings = [
        ["GET", "/api/v1/listings", "AllowAnonymous", "Tìm kiếm và lọc tin đăng công khai (trạng thái Published) có phân trang", "200 OK"],
        ["GET", "/api/v1/listings/{id}", "AllowAnonymous", "Xem chi tiết tin đăng công khai theo ID", "200 OK\n404 Not Found"],
        ["POST", "/api/v1/listings", "Roles = Seller", "Tạo mới tin đăng bất động sản ở trạng thái Draft", "201 Created\n400 Bad Request"],
        ["PUT", "/api/v1/listings/{id}", "Roles = Seller", "Cập nhật thông tin tin đăng kèm kiểm soát đồng thời RowVersion", "200 OK\n400 Bad Request\n404 Not Found\n409 Conflict"],
        ["POST", "/api/v1/listings/{id}/submit", "Roles = Seller", "Gửi tin đăng để chờ ban quản trị phê duyệt (Draft -> PendingModeration)", "200 OK\n400 Bad Request\n404 Not Found"],
        ["POST", "/api/v1/listings/{id}/hide", "Roles = Seller", "Tạm ẩn tin đăng đã xuất bản khỏi cổng tìm kiếm công khai", "200 OK\n400 Bad Request\n404 Not Found"],
        ["POST", "/api/v1/listings/{id}/unhide", "Roles = Seller", "Kích hoạt hiển thị lại tin đăng đang bị ẩn", "200 OK\n400 Bad Request\n404 Not Found"],
        ["DELETE", "/api/v1/listings/{id}", "Roles = Seller", "Xóa tin đăng khỏi hệ thống (chỉ áp dụng cho tin Draft hoặc Rejected)", "204 No Content\n400 Bad Request\n404 Not Found"],
        ["GET", "/api/v1/listings/my-listings", "Roles = Seller", "Lấy danh sách toàn bộ tin đăng thuộc quyền sở hữu của Seller hiện tại", "200 OK"],
        ["GET", "/api/v1/listings/{id}/history", "Roles = Seller", "Truy vấn lịch sử các lần chỉnh sửa và biến động giá của tin đăng", "200 OK\n404 Not Found"],
        ["POST", "/api/v1/listings/{id}/images", "Roles = Seller", "Upload hình ảnh minh họa cho tin đăng (Multipart/form-data)", "201 Created\n400 Bad Request\n404 Not Found"],
        ["DELETE", "/api/v1/listings/{id}/images/{imageId}", "Roles = Seller", "Xóa hình ảnh khỏi bộ sưu tập của tin đăng", "204 No Content\n404 Not Found"]
    ]
    add_body(doc, "Bảng 11.2: Danh mục các Endpoint trong Nhóm API Tin đăng (/api/v1/listings)", bold=True, italic=True)
    add_styled_table(doc, headers_listings, data_listings, col_widths=[0.8, 2.3, 1.2, 2.0, 1.1])

    add_body(doc, "Mẫu dữ liệu ListingDto phản hồi từ hệ thống, tích hợp trường RowVersion bảo vệ cập nhật đồng thời:")
    add_code_box(doc,
        '// Response Body: ListingDto (Định dạng phản hồi chi tiết tin đăng chuẩn hóa)\n'
        '{\n'
        '  "listingId": 12,\n'
        '  "ownerUserId": 105,\n'
        '  "title": "Bán căn hộ 2PN 75m² Vinhomes Central Park - Đầy đủ nội thất cao cấp",\n'
        '  "description": "Căn hộ lầu cao, view Landmark 81 cực đẹp, sổ hồng chính chủ sang tên ngay.",\n'
        '  "propertyType": "Apartment",\n'
        '  "listingType": "Sale",\n'
        '  "price": 5200000000.00,\n'
        '  "area": 75.50,\n'
        '  "city": "Thành phố Hồ Chí Minh",\n'
        '  "district": "Quận Bình Thạnh",\n'
        '  "ward": "Phường 22",\n'
        '  "address": "208 Nguyễn Hữu Cảnh",\n'
        '  "status": "Published",\n'
        '  "moderationDecision": "Approved",\n'
        '  "packageCode": "VIP",\n'
        '  "startDate": "2026-10-30T10:00:00+07:00",\n'
        '  "endDate": "2026-11-29T10:00:00+07:00",\n'
        '  "rowVersion": "AAAAAAAAB9Q="\n'
        '}'
    )

    # -------------------------------------------------------------------------
    # 11.3.3. Nhóm API Kiểm duyệt (/api/v1/moderation)
    # -------------------------------------------------------------------------
    add_heading_4(doc, "11.3.3. Nhóm API Kiểm duyệt (/api/v1/moderation)")

    add_body(doc,
        "Nhóm API Kiểm duyệt dành riêng cho đội ngũ Quản trị viên (Moderator/Admin) nhằm thực hiện quy trình thẩm định chất lượng tin đăng trước khi cho phép hiển thị công khai trên nền tảng, loại bỏ triệt để các tin rác, giá ảo hoặc nội dung vi phạm thuần phong mỹ tục.")

    headers_mod = ["HTTP Verb", "Đường dẫn URI", "Phân quyền", "Mô tả nghiệp vụ", "HTTP Status Code"]
    data_mod = [
        ["GET", "/api/v1/moderation/pending", "Policy: CanModerateListings", "Lấy danh sách các tin đăng đang chờ duyệt (PendingModeration) có phân trang", "200 OK"],
        ["GET", "/api/v1/moderation/{id}", "Policy: CanModerateListings", "Xem chi tiết tin đăng kiểm duyệt kèm hồ sơ vi phạm và lịch sử chỉnh sửa", "200 OK\n404 Not Found"],
        ["POST", "/api/v1/moderation/{id}/approve", "Policy: CanModerateListings", "Phê duyệt tin đăng hợp lệ (chuyển trạng thái sang Published)", "200 OK\n400 Bad Request\n404 Not Found"],
        ["POST", "/api/v1/moderation/{id}/reject", "Policy: CanModerateListings", "Từ chối tin đăng vi phạm kèm lý do bắt buộc (RejectionReason)", "200 OK\n400 Bad Request\n404 Not Found"],
        ["GET", "/api/v1/moderation/stats", "Policy: CanModerateListings", "Truy vấn dữ liệu thống kê tổng thể Dashboard quản trị (phân bổ tin đăng và ví)", "200 OK"]
    ]
    add_body(doc, "Bảng 11.3: Danh mục các Endpoint trong Nhóm API Kiểm duyệt (/api/v1/moderation)", bold=True, italic=True)
    add_styled_table(doc, headers_mod, data_mod, col_widths=[0.8, 2.3, 1.5, 1.8, 1.0])

    add_body(doc, "Mẫu dữ liệu Dashboard Stats phục vụ giám sát vận hành thời gian thực từ endpoint /api/v1/moderation/stats:")
    add_code_box(doc,
        '// Phản hồi Dashboard Stats: GET /api/v1/moderation/stats\n'
        '{\n'
        '  "publishedCount": 142,\n'
        '  "pendingCount": 18,\n'
        '  "rejectedCount": 7,\n'
        '  "draftCount": 25,\n'
        '  "hiddenCount": 4,\n'
        '  "expiredCount": 12,\n'
        '  "totalProperties": 208,\n'
        '  "totalUsers": 95,\n'
        '  "activeSellersCount": 48,\n'
        '  "totalWalletTransactions": 312,\n'
        '  "totalWalletVolume": 85600000.00,\n'
        '  "statusDistribution": {\n'
        '    "Published": 142,\n'
        '    "PendingModeration": 18,\n'
        '    "Draft": 25,\n'
        '    "Rejected": 7,\n'
        '    "Hidden": 4,\n'
        '    "Expired": 12\n'
        '  },\n'
        '  "walletStats": {\n'
        '    "topUpCount": 180,\n'
        '    "packagePurchaseCount": 126,\n'
        '    "refundCount": 6,\n'
        '    "totalTopUpAmount": 95000000.00,\n'
        '    "totalSpentAmount": 54200000.00\n'
        '  }\n'
        '}'
    )

    # -------------------------------------------------------------------------
    # 11.3.4. Nhóm API Ví và Mua gói (/api/v1/wallets, /purchase-package)
    # -------------------------------------------------------------------------
    add_heading_4(doc, "11.3.4. Nhóm API Ví và Mua gói (/api/v1/wallets, /purchase-package)")

    add_body(doc,
        "Nhóm API Tài chính và Ví quản lý toàn bộ các giao dịch nạp tiền, theo dõi biến động số dư, và thực thi quy trình mua gói đẩy tin/VIP tin đăng với cơ chế bảo vệ giao dịch kép (Idempotency Key) và điều phối bất đồng bộ Saga.")

    headers_wallet = ["HTTP Verb", "Đường dẫn URI", "Phân quyền", "Mô tả nghiệp vụ", "HTTP Status Code"]
    data_wallet = [
        ["GET", "/api/v1/wallets/me", "Roles = Seller", "Xem thông tin số dư khả dụng (Balance) của ví cá nhân", "200 OK"],
        ["POST", "/api/v1/wallets/top-up", "Roles = Seller", "Nạp tiền vào ví cá nhân (mô phỏng cổng thanh toán trực tuyến)", "200 OK\n400 Bad Request"],
        ["GET", "/api/v1/wallets/transactions", "Roles = Seller", "Truy vấn lịch sử các giao dịch biến động số dư có phân trang", "200 OK"],
        ["POST", "/api/v1/listings/{id}/purchase-package", "Roles = Seller", "Mua gói nâng cấp tin (VIP/Premium) tích hợp Idempotency-Key và Saga", "202 Accepted\n400 Bad Request\n404 Not Found\n409 Conflict"]
    ]
    add_body(doc, "Bảng 11.4: Danh mục các Endpoint trong Nhóm API Ví và Mua gói", bold=True, italic=True)
    add_styled_table(doc, headers_wallet, data_wallet, col_widths=[0.8, 2.3, 1.2, 2.0, 1.1])

    add_body(doc, "Mẫu Request Body và phản hồi 202 Accepted của API Mua gói dịch vụ tin đăng:")
    add_code_box(doc,
        '// Header: Idempotency-Key: e4b2d308-8f12-4d2a-bc90-93a8e97a314b\n'
        '// Request Body: PurchasePackageRequest\n'
        '{\n'
        '  "packageCode": "VIP",\n'
        '  "chargeAmount": 150000.00\n'
        '}\n\n'
        '// Response: 202 Accepted kèm header Location: /api/v1/workflows/e4b2d308-8f12-4d2a-bc90-93a8e97a314b\n'
        '{\n'
        '  "correlationId": "e4b2d308-8f12-4d2a-bc90-93a8e97a314b",\n'
        '  "listingId": 12,\n'
        '  "packageCode": "VIP",\n'
        '  "chargeAmount": 150000.00,\n'
        '  "status": "DeductingWallet",\n'
        '  "createdAt": "2026-10-30T10:15:20.123+07:00"\n'
        '}'
    )

    # -------------------------------------------------------------------------
    # 11.3.5. Nhóm API Tiến độ Workflow (/api/v1/workflows)
    # -------------------------------------------------------------------------
    add_heading_4(doc, "11.3.5. Nhóm API Tiến độ Workflow (/api/v1/workflows)")

    add_body(doc,
        "Sau khi nhận phản hồi 202 Accepted từ API mua gói, client sử dụng endpoint GET /api/v1/workflows/{correlationId} để thực hiện cơ chế Polling kiểm tra tiến trình thực thi của Saga Orchestrator.")

    add_body(doc, "Cấu trúc dữ liệu phản hồi WorkflowDto khi quy trình phân tán hoàn tất thành công:")
    add_code_box(doc,
        '// Response Body: WorkflowDto (Tiến độ xử lý hoàn tất thành công)\n'
        '{\n'
        '  "correlationId": "e4b2d308-8f12-4d2a-bc90-93a8e97a314b",\n'
        '  "listingId": 12,\n'
        '  "packageCode": "VIP",\n'
        '  "chargeAmount": 150000.00,\n'
        '  "status": "Completed",\n'
        '  "errorMessage": null,\n'
        '  "steps": [\n'
        '    {\n'
        '      "stepName": "DeductWallet",\n'
        '      "isCompleted": true,\n'
        '      "completedAt": "2026-10-30T10:15:20.450+07:00",\n'
        '      "errorMessage": null\n'
        '    },\n'
        '    {\n'
        '      "stepName": "UpgradeListing",\n'
        '      "isCompleted": true,\n'
        '      "completedAt": "2026-10-30T10:15:20.890+07:00",\n'
        '      "errorMessage": null\n'
        '    },\n'
        '    {\n'
        '      "stepName": "RecordHistory",\n'
        '      "isCompleted": true,\n'
        '      "completedAt": "2026-10-30T10:15:21.010+07:00",\n'
        '      "errorMessage": null\n'
        '    }\n'
        '  ]\n'
        '}'
    )

    # =========================================================================
    # 11.4. CÁC CƠ CHẾ KỸ THUẬT ĐẶC SẮC CỦA PROPNEST (PHIÊN BẢN TRỰC QUAN HÓA)
    # =========================================================================
    add_heading_3(doc, "11.4. Các cơ chế kỹ thuật đặc sắc của PropNest")

    add_body(doc,
        "Khác biệt với các hệ thống đăng tin thông thường chỉ thao tác CRUD dữ liệu tĩnh, PropNest được trang bị ba trụ cột kỹ thuật nâng cao nhằm đảm bảo tính toàn vẹn dữ liệu, hiệu năng tối ưu và độ tin cậy trong môi trường phân tán:")

    # -------------------------------------------------------------------------
    # 11.4.1. Động cơ theo dõi biến động dữ liệu (History Delta Engine)
    # -------------------------------------------------------------------------
    add_heading_4(doc, "11.4.1. Động cơ theo dõi biến động dữ liệu (History Delta Engine)")

    add_body(doc,
        "Để đáp ứng yêu cầu lưu vết toàn bộ lịch sử biến động tin đăng bất động sản (đặc biệt là biến động giá bán và mô tả pháp lý) mà không gây phình to cơ sở dữ liệu, PropNest áp dụng mô hình Kiểm toán lai (Hybrid Auditing) kết hợp giữa Snapshot toàn phần và Delta vi sai.")

    add_image_with_caption(doc, 
        "docs/diagrams/hinh_2_6_history_delta_engine.png", 
        "Hình 11.1: Nguyên lý hoạt động và quy trình tái tạo của History Delta Engine", 
        width_inches=6.2)

    add_body(doc,
        "Cơ chế hoạt động của History Delta Engine được phân chia thành hai quy trình độc lập như minh họa trong Hình 11.1:")

    add_body(doc,
        "• Quy trình lưu trữ vi sai (Storage Pipeline): Tại thời điểm tạo tin đăng (T0), hệ thống lưu một bản Full Snapshot (IsSnapshot = True). Ở các lần chỉnh sửa tiếp theo (T1, T2,...), bộ so khớp DeltaEngine sử dụng EqualityComparer<T>.Default để bóc tách chính xác những thuộc tính bị thay đổi và ghi vào mảng DeltasJson (IsSnapshot = False), dung lượng chỉ khoảng 142 bytes thay vì sao chép toàn bộ bản ghi.")

    add_body(doc,
        "• Quy trình phục dựng trạng thái quá khứ (Reconstruction Pipeline): Khi người dùng hoặc kiểm duyệt viên yêu cầu xem lịch sử tin đăng tại một thời điểm cụ thể trong quá khứ, hệ thống bắt đầu từ bản Base Snapshot gần nhất và tuần tự áp dụng (apply) các bản ghi Delta theo chuỗi thời gian (T_target = T0 + Δ1 + Δ2).")

    add_image_with_caption(doc,
        "docs/diagrams/hinh_4_1_so_sanh_dung_luong_delta.png",
        "Hình 11.2: So sánh hiệu quả tiết kiệm dung lượng lưu trữ CSDL của mô hình Delta vi sai",
        width_inches=5.6)

    add_body(doc,
        "Kết quả thực nghiệm so sánh dung lượng (Hình 11.2) cho thấy sau 50 lần chỉnh sửa liên tiếp trên 100 tin đăng mẫu, giải pháp Delta Engine giúp tiết kiệm hơn 92% dung lượng đĩa cứng so với phương pháp lưu Snapshot truyền thống, đồng thời giảm đáng kể áp lực I/O của máy chủ cơ sở dữ liệu.")

    # -------------------------------------------------------------------------
    # 11.4.2. Kiểm soát đồng thời với RowVersion (Optimistic Concurrency)
    # -------------------------------------------------------------------------
    add_heading_4(doc, "11.4.2. Kiểm soát đồng thời với RowVersion (Optimistic Concurrency)")

    add_body(doc,
        "Trong thị trường giao dịch bất động sản, hiện tượng xung đột ghi dữ liệu (Lost Update) rất dễ xảy ra khi nhiều môi giới hoặc người bán cùng mở và cập nhật giá hoặc trạng thái của một bất động sản tại cùng một thời điểm. Thay vì sử dụng khóa bi quan (Pessimistic Locking) gây nghẽn hiệu năng hệ thống, PropNest triển khai cơ chế Kiểm soát đồng thời lạc quan (Optimistic Concurrency Control - OCC) dựa trên trường RowVersion của SQL Server.")

    add_image_with_caption(doc,
        "docs/diagrams/hinh_2_5_optimistic_concurrency.png",
        "Hình 11.3: Cơ chế kiểm soát xung đột đồng thời (OCC) với trường RowVersion trong SQL Server",
        width_inches=6.2)

    add_body(doc,
        "Trình tự xử lý xung đột đồng thời được mô tả trực quan qua Hình 11.3 gồm các bước chuẩn mực:")

    add_body(doc,
        "1. Đọc dữ liệu đồng thời: Cả Client A và Client B cùng tải dữ liệu tin đăng (giá 3.0 tỷ) kèm mã Concurrency Token ban đầu RowVersion = 0x0001 (phiên bản v1).")

    add_body(doc,
        "2. Cập nhật thành công lần đầu: Client A gửi lệnh cập nhật giá lên 3.2 tỷ. Mệnh đề SQL UPDATE khớp đúng RowVersion = 0x0001, lệnh thành công và SQL Server tự động tăng token của bản ghi lên 0x0002 (phiên bản v2).")

    add_body(doc,
        "3. Phát hiện xung đột và chặn ghi đè: Client B gửi lệnh sửa giá thành 3.1 tỷ kèm token cũ RowVersion = 0x0001. Do token trong CSDL lúc này đã là v2, câu lệnh UPDATE WHERE [ListingId] = @id AND [RowVersion] = 0x0001 trả về số dòng ảnh hưởng bằng 0.")

    add_body(doc,
        "4. Phản hồi chuẩn hóa: Entity Framework Core phát hiện 0 dòng cập nhật và tự động ném ngoại lệ DbUpdateConcurrencyException. Middleware của hệ thống lập tức bắt ngoại lệ này và phản hồi mã lỗi HTTP 409 Conflict theo RFC 7807, ngăn chặn hoàn toàn việc mất mát dữ liệu của Client A.")

    # -------------------------------------------------------------------------
    # 11.4.3. Saga Orchestrator và bồi hoàn hoàn tiền tự động
    # -------------------------------------------------------------------------
    add_heading_4(doc, "11.4.3. Saga Orchestrator và bồi hoàn hoàn tiền tự động")

    add_body(doc,
        "Nghiệp vụ mua gói nâng cấp tin đăng (VIP/Premium) là một giao dịch phân tán phức tạp đòi hỏi sự phối hợp giữa nhiều thành phần: Trừ số dư Ví tiền của người bán, Nâng cấp thông tin và hạn hiển thị của Tin đăng, và Ghi nhật ký giao dịch. PropNest áp dụng mô hình Điều phối tập trung (Saga Orchestration) thông qua MassTransit State Machine kết hợp với mẫu thiết kế Transactional Outbox.")

    add_image_with_caption(doc,
        "docs/diagrams/hinh_2_3_saga_orchestration.png",
        "Hình 11.4: Sơ đồ máy trạng thái Saga Orchestration và luồng bồi hoàn tự động",
        width_inches=6.2)

    add_body(doc,
        "Sơ đồ máy trạng thái trong Hình 11.4 thể hiện hai luồng xử lý cốt lõi của quy trình mua gói:")

    add_body(doc,
        "• Luồng thuận lợi (Happy Path): Bắt đầu từ trạng thái Started khi nhận sự kiện PackagePurchaseInitiatedEvent -> chuyển sang DeductingWallet để gửi DeductWalletCommand -> nhận WalletDeductedEvent -> chuyển sang UpgradingListing để gửi UpgradeListingCommand -> nhận ListingUpgradedEvent -> gửi RecordHistoryCommand -> ghi nhận trạng thái Completed.")

    add_body(doc,
        "• Luồng bồi hoàn tự động (Compensating Transaction): Trong trường hợp bước trừ tiền ví thành công nhưng bước nâng cấp tin thất bại (do lỗi CSDL hoặc vi phạm nghiệp vụ), consumer phát ra ListingUpgradeFailedEvent. Saga Orchestrator lập tức chuyển sang trạng thái Compensating và phát lệnh bồi hoàn RefundWalletCommand. Khi nhận được WalletRefundedEvent, Saga chuyển sang trạng thái Compensated, đảm bảo số dư ví của khách hàng được hoàn trả nguyên vẹn (Tính nhất quán cuối cùng - Eventual Consistency).")

    add_image_with_caption(doc,
        "docs/diagrams/hinh_2_4_transactional_outbox.png",
        "Hình 11.5: Mô hình Transactional Outbox đảm bảo tính toàn vẹn sự kiện phân tán",
        width_inches=6.2)

    add_body(doc,
        "Để bảo đảm tính nguyên tử (Atomicity) giữa việc lưu trữ trạng thái vào SQL Server và phát thông điệp sang RabbitMQ, hệ thống áp dụng mô hình Transactional Outbox (Hình 11.5). Mọi tin nhắn tích hợp được ghi vào bảng OutboxMessages trong cùng transaction CSDL với logic nghiệp vụ. Một tiến trình Outbox Worker chạy ngầm sẽ định kỳ đọc các tin nhắn chưa gửi và xuất bản sang Message Broker, loại bỏ triệt để nguy cơ mất mát thông điệp khi xảy ra sự cố sập nguồn hoặc đứt kết nối mạng.")

    # =========================================================================
    # KẾT LUẬN CHƯƠNG 11
    # =========================================================================
    add_heading_3(doc, "KẾT LUẬN CHƯƠNG 11")

    add_body(doc,
        "Chương 11 đã hoàn thành trọn vẹn việc phân tích và thiết kế chi tiết cho toàn bộ hệ thống PropNest. Từ danh mục các endpoint API RESTful tuân thủ nghiêm ngặt cấp độ 2 của Richardson, phân cấp quyền hạn chặt chẽ (Public, Seller, Moderator/Admin), cho đến ba cơ chế kỹ thuật đặc sắc: History Delta Engine tối ưu hóa không gian lưu trữ, Optimistic Concurrency Control với RowVersion phòng chống xung đột ghi đè, và Saga Orchestration đi kèm Transactional Outbox đảm bảo an toàn giao dịch tài chính phân tán.")

    add_body(doc,
        "Những giải pháp thiết kế này tạo nền tảng vững chắc để chuyển tiếp sang Chương 12: Triển khai và Kiểm thử API, nơi toàn bộ các luồng nghiệp vụ và kịch bản thực nghiệm sẽ được chứng minh qua dữ liệu kiểm thử thực tế trên Swagger UI, Postman và hệ thống giám sát thời gian thực.")

def generate_standalone_chapter_11():
    doc = docx.Document()
    
    # Page setup: Standard Letter, 1-inch margins
    for sec in doc.sections:
        sec.top_margin = Inches(1.0)
        sec.bottom_margin = Inches(1.0)
        sec.left_margin = Inches(1.0)
        sec.right_margin = Inches(1.0)

    p_title = doc.add_paragraph()
    set_para_font(p_title, "BÁO CÁO ĐỒ ÁN CÁC CÔNG NGHỆ LẬP TRÌNH HIỆN ĐẠI", bold=True, size=15, color_rgb=(24, 90, 188), align=WD_ALIGN_PARAGRAPH.CENTER, space_after=2)
    
    p_sub = doc.add_paragraph()
    set_para_font(p_sub, "PHẦN 3: XÂY DỰNG ĐỒ ÁN TỔNG HỢP - CHƯƠNG 11 (HOÀN THIỆN MỤC 11.3 ĐẾN 11.4)", bold=True, size=13, color_rgb=(71, 85, 105), align=WD_ALIGN_PARAGRAPH.CENTER, space_after=18)

    populate_chapter_11_content(doc)

    out_path = "docs/Chuong11_HoanThien.docx"
    doc.save(out_path)
    print(f"Successfully generated standalone Chapter 11 document: {out_path}")

def update_main_report():
    main_doc_path = "docs/Báo cáo chung Các công nghệ LTHĐ.docx"
    doc = docx.Document(main_doc_path)
    
    # 1. Find boundaries in main document
    start_p = None
    end_p = None
    
    for p in doc.paragraphs:
        if "11.3. Thiết kế danh mục API RESTful" in p.text:
            start_p = p
            break
            
    for p in doc.paragraphs:
        if "CHƯƠNG 12: TRIỂN KHAI VÀ KIỂM THỬ API" in p.text:
            end_p = p
            break
            
    if start_p is None or end_p is None:
        print("Could not locate boundaries for 11.3 -> CHƯƠNG 12 in main doc")
        return

    # 2. Collect all elements to remove (from start_p up to, but not including, end_p)
    elements_to_remove = []
    curr = start_p._p
    while curr is not None and curr != end_p._p:
        elements_to_remove.append(curr)
        curr = curr.getnext()

    print(f"Found {len(elements_to_remove)} placeholder elements to replace.")

    # 3. Build temporary document with new rich content
    temp_doc = docx.Document()
    populate_chapter_11_content(temp_doc)

    # 4. Insert new elements right before end_p (which is CHƯƠNG 12)
    for child in list(temp_doc.element.body):
        tag = child.tag.split('}')[-1]
        if tag in ('p', 'tbl'):
            end_p._p.addprevious(child)

    # 5. Remove the old placeholder elements
    for elem in elements_to_remove:
        elem.getparent().remove(elem)

    doc.save(main_doc_path)
    print(f"Main report updated successfully with full Chapter 11 visual content: {main_doc_path}!")

if __name__ == "__main__":
    generate_standalone_chapter_11()
    update_main_report()

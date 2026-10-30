import os
import docx
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml import parse_xml, OxmlElement
from docx.oxml.ns import nsdecls, qn

def create_report():
    doc = docx.Document()

    # Thiết lập lề trang chuẩn đồ án (Top 2.5cm, Bottom 2.5cm, Left 3.0cm, Right 2.0cm)
    for section in doc.sections:
        section.top_margin = Inches(2.5 / 2.54)
        section.bottom_margin = Inches(2.5 / 2.54)
        section.left_margin = Inches(3.0 / 2.54)
        section.right_margin = Inches(2.0 / 2.54)

    # Helper định dạng đoạn văn bản thường
    def add_p(text, bold_prefix="", space_after=4, italic_prefix=""):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
        p.paragraph_format.line_spacing = 1.2
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(space_after)
        
        if bold_prefix:
            r_bold = p.add_run(bold_prefix)
            r_bold.font.name = 'Times New Roman'
            r_bold.font.size = Pt(13)
            r_bold.bold = True
            
        if italic_prefix:
            r_it = p.add_run(italic_prefix)
            r_it.font.name = 'Times New Roman'
            r_it.font.size = Pt(13)
            r_it.italic = True

        r_text = p.add_run(text)
        r_text.font.name = 'Times New Roman'
        r_text.font.size = Pt(13)
        return p

    def add_bullet(text, bold_prefix=""):
        p = doc.add_paragraph(style='List Bullet')
        p.alignment = WD_ALIGN_PARAGRAPH.JUSTIFY
        p.paragraph_format.line_spacing = 1.2
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(3)
        if bold_prefix:
            r_bold = p.add_run(bold_prefix)
            r_bold.font.name = 'Times New Roman'
            r_bold.font.size = Pt(13)
            r_bold.bold = True
        r_text = p.add_run(text)
        r_text.font.name = 'Times New Roman'
        r_text.font.size = Pt(13)
        return p

    def add_h1(text):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p.paragraph_format.line_spacing = 1.2
        p.paragraph_format.space_before = Pt(14)
        p.paragraph_format.space_after = Pt(6)
        r = p.add_run(text)
        r.font.name = 'Times New Roman'
        r.font.size = Pt(16)
        r.bold = True
        r.font.color.rgb = RGBColor(0, 51, 102) # Xanh đậm trang trọng
        return p

    def add_h2(text):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p.paragraph_format.line_spacing = 1.2
        p.paragraph_format.space_before = Pt(10)
        p.paragraph_format.space_after = Pt(4)
        r = p.add_run(text)
        r.font.name = 'Times New Roman'
        r.font.size = Pt(14)
        r.bold = True
        r.font.color.rgb = RGBColor(0, 0, 0)
        return p

    def add_h3(text):
        p = doc.add_paragraph()
        p.alignment = WD_ALIGN_PARAGRAPH.LEFT
        p.paragraph_format.line_spacing = 1.2
        p.paragraph_format.space_before = Pt(8)
        p.paragraph_format.space_after = Pt(3)
        r = p.add_run(text)
        r.font.name = 'Times New Roman'
        r.font.size = Pt(13)
        r.bold = True
        r.font.color.rgb = RGBColor(30, 30, 30)
        return p

    # Khung code: trắng viền đen (không màu nền) theo đúng yêu cầu
    def add_code(code_text, caption=""):
        if caption:
            p_cap = doc.add_paragraph()
            p_cap.alignment = WD_ALIGN_PARAGRAPH.LEFT
            p_cap.paragraph_format.space_before = Pt(6)
            p_cap.paragraph_format.space_after = Pt(2)
            r_cap = p_cap.add_run(caption)
            r_cap.font.name = 'Times New Roman'
            r_cap.font.size = Pt(11)
            r_cap.italic = True
            r_cap.bold = True

        tbl = doc.add_table(rows=1, cols=1)
        tbl.alignment = WD_TABLE_ALIGNMENT.CENTER
        cell = tbl.cell(0, 0)
        cell.width = Inches(6.2)

        tcPr = cell._tc.get_or_add_tcPr()
        borders = parse_xml(
            f'<w:tcBorders {nsdecls("w")}>'
            f'<w:top w:val="single" w:sz="6" w:space="0" w:color="000000"/>'
            f'<w:left w:val="single" w:sz="6" w:space="0" w:color="000000"/>'
            f'<w:bottom w:val="single" w:sz="6" w:space="0" w:color="000000"/>'
            f'<w:right w:val="single" w:sz="6" w:space="0" w:color="000000"/>'
            f'</w:tcBorders>'
        )
        tcPr.append(borders)
        shd = parse_xml(f'<w:shd {nsdecls("w")} w:fill="auto"/>')
        tcPr.append(shd)

        lines = code_text.strip().split('\n')
        for i, line in enumerate(lines):
            p = cell.paragraphs[0] if i == 0 else cell.add_paragraph()
            p.paragraph_format.space_before = Pt(0)
            p.paragraph_format.space_after = Pt(0)
            p.paragraph_format.line_spacing = 1.05
            run = p.add_run(line)
            run.font.name = 'Consolas'
            run.font.size = Pt(9.5)
            run.font.color.rgb = RGBColor(0, 0, 0)

        p_spacer = doc.add_paragraph()
        p_spacer.paragraph_format.space_before = Pt(2)
        p_spacer.paragraph_format.space_after = Pt(4)

    # Helper vẽ bảng so sánh có viền đen
    def add_table_data(headers, rows, col_widths=None):
        table = doc.add_table(rows=len(rows) + 1, cols=len(headers))
        table.alignment = WD_TABLE_ALIGNMENT.CENTER
        
        # Border bảng
        tblPr = table._tbl.tblPr
        tblBorders = parse_xml(
            f'<w:tblBorders {nsdecls("w")}>'
            f'<w:top w:val="single" w:sz="6" w:space="0" w:color="000000"/>'
            f'<w:bottom w:val="single" w:sz="6" w:space="0" w:color="000000"/>'
            f'<w:left w:val="single" w:sz="6" w:space="0" w:color="000000"/>'
            f'<w:right w:val="single" w:sz="6" w:space="0" w:color="000000"/>'
            f'<w:insideH w:val="single" w:sz="4" w:space="0" w:color="CCCCCC"/>'
            f'<w:insideV w:val="single" w:sz="4" w:space="0" w:color="CCCCCC"/>'
            f'</w:tblBorders>'
        )
        tblPr.append(tblBorders)

        # Header
        for c_idx, title in enumerate(headers):
            cell = table.cell(0, c_idx)
            if col_widths and c_idx < len(col_widths):
                cell.width = Inches(col_widths[c_idx])
            p = cell.paragraphs[0]
            p.alignment = WD_ALIGN_PARAGRAPH.CENTER
            p.paragraph_format.space_before = Pt(4)
            p.paragraph_format.space_after = Pt(4)
            r = p.add_run(title)
            r.font.name = 'Times New Roman'
            r.font.size = Pt(11)
            r.bold = True

        # Data rows
        for r_idx, row_data in enumerate(rows):
            for c_idx, val in enumerate(row_data):
                cell = table.cell(r_idx + 1, c_idx)
                if col_widths and c_idx < len(col_widths):
                    cell.width = Inches(col_widths[c_idx])
                p = cell.paragraphs[0]
                p.alignment = WD_ALIGN_PARAGRAPH.LEFT if c_idx != 0 else WD_ALIGN_PARAGRAPH.CENTER
                p.paragraph_format.space_before = Pt(3)
                p.paragraph_format.space_after = Pt(3)
                p.paragraph_format.line_spacing = 1.15
                r = p.add_run(val)
                r.font.name = 'Times New Roman'
                r.font.size = Pt(10.5)

        p_spacer = doc.add_paragraph()
        p_spacer.paragraph_format.space_before = Pt(2)
        p_spacer.paragraph_format.space_after = Pt(4)

    # -------------------------------------------------------------
    # BẮT ĐẦU NỘI DUNG CHƯƠNG 8
    # -------------------------------------------------------------
    
    # Tiêu đề chương
    p_title = doc.add_paragraph()
    p_title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_title.paragraph_format.space_before = Pt(12)
    p_title.paragraph_format.space_after = Pt(14)
    r_ch = p_title.add_run("CHƯƠNG 8: XỬ LÝ GIAO DỊCH PHÂN TÁN VÀ ĐIỀU PHỐI WORKFLOW")
    r_ch.font.name = 'Times New Roman'
    r_ch.font.size = Pt(18)
    r_ch.bold = True
    r_ch.font.color.rgb = RGBColor(0, 51, 102)

    add_p(
        "Trong các kiến trúc phần mềm hướng dịch vụ phân tán (Distributed Systems và Microservices), "
        "việc chuyển đổi từ mô hình cơ sở dữ liệu dùng chung (Shared Database) sang mô hình mỗi dịch vụ quản lý "
        "dữ liệu độc lập (Database-per-Service) đem lại tính tự chủ, khả năng mở rộng linh hoạt và cô lập lỗi cao. "
        "Tuy nhiên, sự phân rã này đồng thời phá vỡ ranh giới của các giao dịch cục bộ ACID truyền thống. "
        "Một chuỗi thao tác nghiệp vụ phức tạp đòi hỏi sự phối hợp của nhiều phân hệ không thể chỉ dựa vào câu lệnh "
        "BEGIN TRANSACTION và COMMIT đơn thuần trên một máy chủ cơ sở dữ liệu duy nhất."
    )
    add_p(
        "Chương này tập trung đi sâu vào các lý thuyết nền tảng và giải pháp kỹ thuật cốt lõi giải quyết bài toán giao dịch phân tán: "
        "từ những giới hạn của giao dịch Two-Phase Commit (2PC), định lý CAP và nguyên lý Eventual Consistency, cho đến việc thiết kế "
        "mô hình Saga Orchestration với State Machine trên thư viện MassTransit. Bên cạnh đó, chương phân tích toàn diện các mẫu kiến trúc "
        "bảo vệ độ tin cậy thông điệp bao gồm Transactional Outbox Pattern, Inbox Deduplication, cơ chế Idempotency Request và giao dịch bồi hoàn (Compensating Transactions) "
        "nhằm đảm bảo tính toàn vẹn dữ liệu tuyệt đối trong môi trường mạng bất định."
    )

    # 8.1
    add_h1("8.1. Thách thức giao dịch trong hệ thống Backend phân tán")

    add_h2("8.1.1. Giới hạn của giao dịch Two-Phase Commit (2PC) trong môi trường microservices")
    add_p(
        "Trong kiến trúc nguyên khối (Monolithic Architecture), tính toàn vẹn dữ liệu được bảo vệ mạnh mẽ thông qua "
        "chuẩn giao dịch ACID (Atomicity, Consistency, Isolation, Durability) do hệ quản trị cơ sở dữ liệu quan hệ (RDBMS) cung cấp. "
        "Tất cả các thao tác ghi dữ liệu diễn ra bên trong một tiến trình duy nhất và được commit nguyên tử. Tuy nhiên, khi hệ thống "
        "được phân tách thành các microservice với cơ sở dữ liệu riêng biệt, một nghiệp vụ như đặt mua dịch vụ cần tương tác với "
        "nhiều dịch vụ độc lập (quản lý người dùng, trừ tiền ví, kích hoạt quyền lợi tin đăng). Lúc này, giao dịch trở thành giao dịch phân tán (Distributed Transaction)."
    )
    add_p(
        "Về mặt lý thuyết truyền thống, giải pháp kinh điển để duy trì ACID trên nhiều node phân tán là giao thức Two-Phase Commit (2PC), "
        "thường được thực thi qua chuẩn XA Transaction do một bộ điều phối tập trung (Transaction Coordinator) giám sát qua hai giai đoạn:"
    )
    add_bullet(
        "Bộ điều phối gửi thông điệp yêu cầu tất cả các dịch vụ tham gia (Resource Managers) chuẩn bị commit. "
        "Mỗi dịch vụ kiểm tra tính hợp lệ của dữ liệu, khóa các bản ghi liên quan (Acquire Locks), ghi nhận trạng thái vào Undo/Redo Log "
        "và phản hồi lại phiếu bầu 'Yes' (sẵn sàng) hoặc 'No' (thất bại).",
        "Giai đoạn 1 - Chuẩn bị (Prepare / Voting Phase): "
    )
    add_bullet(
        "Nếu tất cả các node đều bỏ phiếu 'Yes', bộ điều phối sẽ gửi lệnh 'Global Commit' để các node chính thức xác nhận thay đổi và giải phóng khóa. "
        "Nếu có ít nhất một node bỏ phiếu 'No' hoặc bị quá thời gian chờ (Timeout), bộ điều phối sẽ gửi lệnh 'Global Abort/Rollback' để tất cả các node hoàn tác dữ liệu.",
        "Giai đoạn 2 - Xác nhận (Commit / Abort Phase): "
    )
    add_p(
        "Mặc dù 2PC đảm bảo tính toàn vẹn dữ liệu nghiêm ngặt, giao thức này bộc lộ những nhược điểm chí mạng khiến nó gần như "
        "bị loại bỏ trong các hệ sinh thái phân tán hiện đại và môi trường điện toán đám mây:"
    )
    add_bullet(
        "2PC là một giao thức chặn đồng bộ (Synchronous Blocking Protocol). Các dịch vụ tham gia phải duy trì các khóa trên hàng (Row Locks) "
        "suốt từ Giai đoạn 1 cho đến khi Giai đoạn 2 hoàn tất. Trong môi trường mạng diện rộng hoặc khi hệ thống chịu tải cao, việc giữ khóa kéo dài "
        "sẽ làm tê liệt các giao dịch khác, tạo ra hiện tượng suy thoái hiệu năng dây chuyền (Cascading Failure) và nguy cơ Deadlock rất lớn.",
        "Giao thức chặn tài nguyên (Blocking Protocol): "
    )
    add_bullet(
        "Nếu bộ điều phối gặp sự cố sập nguồn (Crash) ngay sau Giai đoạn 1, các node tham gia sẽ rơi vào trạng thái lơ lửng (In-Doubt State). "
        "Chúng không thể tự ý commit (vì không biết các node khác thế nào) cũng không thể tự ý rollback, dẫn đến tài nguyên bị giam cầm vô thời hạn.",
        "Điểm lỗi đơn lẻ (Single Point of Failure - SPOF): "
    )
    add_bullet(
        "Độ trễ của toàn bộ giao dịch bằng độ trễ của node phản hồi chậm nhất cộng thêm thời gian truyền thông điệp nhiều vòng qua mạng (Network Round-Trips). "
        "Hệ thống càng có nhiều service tham gia thì thông lượng (Throughput) càng sụt giảm theo hàm số mũ.",
        "Suy giảm thông lượng nghiêm trọng: "
    )
    add_bullet(
        "Rất nhiều hệ cơ sở dữ liệu hiện đại (MongoDB, Cassandra, Redis) cũng như các Message Broker (RabbitMQ, Apache Kafka) "
        "hoặc các API thanh toán bên thứ ba (VNPay, Momo, Stripe) không hề hỗ trợ chuẩn XA/2PC, khiến việc áp dụng 2PC trong thực tế trở nên bất khả thi.",
        "Không tương thích với hệ sinh thái công nghệ mới: "
    )

    add_h2("8.1.2. Định lý CAP và nguyên lý tính nhất quán cuối cùng (Eventual Consistency)")
    add_p(
        "Để khắc phục sự bế tắc của giao dịch ACID phân tán, ngành kỹ nghệ phần mềm dựa trên Định lý CAP (CAP Theorem) "
        "được đề xuất bởi Eric Brewer vào năm 2000. Định lý khẳng định rằng trong bất kỳ hệ thống phân tán chia sẻ dữ liệu nào, "
        "ta chỉ có thể thỏa mãn tối đa hai trong ba thuộc tính sau tại cùng một thời điểm:"
    )
    add_bullet(
        "Mọi thao tác đọc đều nhận được dữ liệu được ghi mới nhất hoặc gặp lỗi, đảm bảo mọi client nhìn thấy trạng thái dữ liệu giống hệt nhau.",
        "Tính nhất quán (Consistency - C): "
    )
    add_bullet(
        "Mọi request gửi tới một node còn sống đều phải nhận được phản hồi không bị lỗi (Non-error response), dù phản hồi đó chưa chắc đã chứa dữ liệu mới nhất.",
        "Tính sẵn sàng (Availability - A): "
    )
    add_bullet(
        "Hệ thống vẫn tiếp tục duy trì hoạt động ngay cả khi có sự cố mất kết nối mạng hoặc phân mảnh giao tiếp giữa các cụm node.",
        "Khả năng chịu đựng phân mảnh mạng (Partition Tolerance - P): "
    )
    add_p(
        "Trong thế giới thực tế của mạng phân tán, sự cố đứt kết nối, trễ gói tin hoặc timeout (Partition - P) là điều tất yếu không thể tránh khỏi. "
        "Do đó, kiến trúc sư hệ thống buộc phải đứng trước sự đánh đổi giữa hai con đường:"
    )
    add_bullet(
        "Nếu mạng bị phân mảnh, hệ thống từ chối phục vụ request hoặc chặn giao dịch để đảm bảo dữ liệu không bị sai lệch giữa các phân vùng. "
        "Mô hình này hy sinh tính sẵn sàng (Availability), điển hình là các hệ thống ngân hàng truyền thống dùng 2PC.",
        "Đánh đổi CP (Consistency + Partition Tolerance): "
    )
    add_bullet(
        "Nếu mạng bị phân mảnh, hệ thống vẫn chấp nhận xử lý request và ghi nhận dữ liệu cục bộ trên các node khả dụng, "
        "chấp nhận việc dữ liệu tạm thời chưa đồng nhất ngay lập tức giữa các node. Mô hình này ưu tiên tối đa tính sẵn sàng và trải nghiệm người dùng.",
        "Đánh đổi AP (Availability + Partition Tolerance): "
    )
    add_p(
        "Từ sự lựa chọn AP, mô hình kiến trúc BASE (Basically Available, Soft state, Eventual consistency) được hình thành "
        "để thay thế cho mô hình ACID truyền thống trong hệ thống phân tán:"
    )
    add_bullet("Hệ thống luôn sẵn sàng phục vụ phần lớn các yêu cầu của người dùng, phân vùng gặp sự cố không làm tê liệt toàn bộ hệ thống.", "Basically Available (Khả dụng cơ bản): ")
    add_bullet("Trạng thái dữ liệu của hệ thống có thể biến đổi theo thời gian ngay cả khi không có thao tác ghi mới, do các thông điệp bất đồng bộ đang trong quá trình chuyển giao.", "Soft State (Trạng thái mềm): ")
    add_bullet("Hệ thống không cam kết dữ liệu đồng nhất tức thời (Immediate Consistency), nhưng bảo đảm rằng sau một khoảng thời gian trễ hữu hạn khi các thông điệp được xử lý xong, toàn bộ các node sẽ hội tụ về cùng một trạng thái nhất quán hoàn toàn.", "Eventual Consistency (Nhất quán cuối cùng): ")

    # Bảng so sánh ACID vs BASE
    add_p("Dưới đây là bảng đối chiếu chi tiết giữa hai triết lý thiết kế giao dịch ACID và BASE trong phát triển phần mềm:")
    headers_acid_base = ["Tiêu chí", "Mô hình ACID (Truyền thống)", "Mô hình BASE (Hệ thống phân tán)"]
    rows_acid_base = [
        ["Tính nhất quán", "Nhất quán tức thì, mạnh mẽ (Strong/Immediate Consistency).", "Nhất quán cuối cùng theo thời gian (Eventual Consistency)."],
        ["Tính sẵn sàng", "Có thể bị suy giảm để bảo vệ tính nhất quán (Ưu tiên CP).", "Luôn ưu tiên phục vụ request liên tục (Ưu tiên AP)."],
        ["Khóa tài nguyên", "Dùng Pessimistic Locking hoặc 2PC, giữ khóa kéo dài.", "Không khóa chéo dịch vụ; mỗi service tự commit transaction cục bộ."],
        ["Độ trễ xử lý", "Cao do phụ thuộc round-trip đồng bộ trên mạng.", "Rất thấp; xử lý bất đồng bộ thông qua Message Queue."],
        ["Khả năng mở rộng", "Kém; thông lượng giảm mạnh khi tăng số lượng node.", "Rất cao; các node hoạt động độc lập và mở rộng quy mô dễ dàng."],
        ["Xử lý lỗi", "Rollback vật lý dữ liệu về trạng thái ban đầu.", "Thực thi giao dịch bồi hoàn ngữ nghĩa (Compensating Transactions)."]
    ]
    add_table_data(headers_acid_base, rows_acid_base, [1.5, 2.3, 2.4])

    # 8.2
    add_h1("8.2. Mô hình Saga Pattern")
    add_p(
        "Mô hình Saga (Saga Pattern) được công bố lần đầu tiên bởi Hector Garcia-Molina và Kenneth Salem vào năm 1987 "
        "nhằm xử lý các giao dịch dài hạn (Long-Running Transactions) mà không cần giữ khóa cơ sở dữ liệu. "
        "Trong kiến trúc hiện đại, Saga đã trở thành tiêu chuẩn vàng để thực thi các giao dịch phân tán đảm bảo tính nhất quán cuối cùng."
    )
    add_p(
        "Về mặt nguyên lý, một Saga đại diện cho một nghiệp vụ tổng thể và được phân rã thành một chuỗi các giao dịch cục bộ: "
        "T1, T2, ..., Tn. Mỗi giao dịch cục bộ Ti được thực thi hoàn toàn độc lập và commit nguyên tử trong ranh giới cơ sở dữ liệu của một service. "
        "Khi Ti commit thành công, nó sẽ phát sinh sự kiện hoặc thông điệp để kích hoạt giao dịch tiếp theo Ti+1."
    )
    add_p(
        "Điểm đặc biệt nhất của Saga là cơ chế bù trừ lỗi thông qua Giao dịch bồi hoàn (Compensating Transactions): C1, C2, ..., Cn-1. "
        "Nếu tại bước Tk bất kỳ gặp lỗi không thể hoàn thành, hệ thống sẽ tự động kích hoạt tuần tự các giao dịch bồi hoàn theo chiều ngược lại: "
        "Ck-1, Ck-2, ..., C1 để hoàn tác các tác động nghiệp vụ đã commit trước đó, đưa hệ thống trở lại trạng thái cân bằng an toàn."
    )
    add_p(
        "Hiện nay, Saga Pattern được chia thành hai trường phái triển khai chính: Choreography (Vũ đạo phân tán) và Orchestration (Nhạc trưởng điều phối)."
    )

    add_h2("8.2.1. Choreography-based Saga: Đặc điểm, ưu thế và hạn chế")
    add_p(
        "Trong mô hình Choreography, không có bất kỳ bộ não điều phối tập trung nào. Các dịch vụ tương tác với nhau hoàn toàn "
        "dựa trên cơ chế phản ứng sự kiện (Event-Driven Architecture). Mỗi dịch vụ tự lắng nghe các Domain Event phát ra từ dịch vụ khác, "
        "tự thực thi giao dịch cục bộ của mình, rồi phát ra Domain Event mới để các dịch vụ tiếp theo đón nhận."
    )
    add_bullet(
        "Cấu trúc đơn giản, trực quan và dễ tiếp cận khi quy trình nghiệp vụ chỉ bao gồm 2 đến 3 bước đơn giản. "
        "Không cần thiết lập thêm thành phần điều phối trung gian; các service hoàn toàn tự trị.",
        "Ưu thế của Choreography: "
    )
    add_bullet(
        "Khi quy trình nghiệp vụ mở rộng lên 5 đến 10 bước với nhiều kịch bản rẽ nhánh phức tạp, luồng điều khiển nghiệp vụ bị phân tán rải rác "
        "khắp nơi trong codebase của từng service. Điều này dẫn đến cấu trúc 'Spaghetti Architecture' — lập trình viên rất khó nắm bắt bức tranh toàn cảnh "
        "về cách một workflow vận hành.",
        "Hạn chế - Khó kiểm soát luồng nghiệp vụ: "
    )
    add_bullet(
        "Do không có nơi ghi nhận trạng thái trung tâm, rất khó để tra cứu xem một đơn hàng cụ thể đang nằm ở bước nào, "
        "đã hoàn tất những khâu nào nếu không đi lục soát log của từng service riêng biệt.",
        "Hạn chế - Thiếu khả năng quan sát (Lack of Observability): "
    )
    add_bullet(
        "Các service có nguy cơ vô tình lắng nghe chéo sự kiện của nhau tạo thành các vòng lặp vô tận (Cyclic Event Dependencies). "
        "Khi xảy ra lỗi cần bồi hoàn, việc phối hợp phát các compensating event ngược lại giữa các service trở nên cực kỳ phức tạp và dễ phát sinh lỗi logic.",
        "Hạn chế - Rủi ro phụ thuộc vòng và bồi hoàn rối loạn: "
    )

    add_h2("8.2.2. Orchestration-based Saga: Lý do lựa chọn cho bài toán thanh toán mua gói PropNest")
    add_p(
        "Khác với Choreography, mô hình Orchestration đưa vào một thành phần điều phối trung tâm gọi là Saga Orchestrator (thường được "
        "hiện thực hóa bằng một Máy trạng thái - State Machine). Orchestrator đóng vai trò như một 'Nhạc trưởng' chỉ huy toàn bộ dàn nhạc:"
    )
    add_bullet(
        "Orchestrator là nơi duy nhất nắm giữ tri thức về toàn bộ quy trình nghiệp vụ (Workflow Logic). "
        "Nó chủ động gửi các Lệnh thực thi (Commands) đến từng service tham gia, chờ lắng nghe các Sự kiện kết quả (Events) trả về, "
        "và ra quyết định chuyển dịch trạng thái tiếp theo.",
        "Điều phối tập trung: "
    )
    add_bullet(
        "Các service tham gia (Participants như Wallet Service, Listing Service) hoạt động ở trạng thái thụ động: chúng chỉ nhận Command, "
        "thực hiện nghiệp vụ cục bộ, và phản hồi Event thành công hay thất bại. Chúng hoàn toàn không cần biết ai đứng trước hay đứng sau mình.",
        "Giảm thiểu ghép nối (Decoupled Participants): "
    )
    add_bullet(
        "Khi có lỗi xảy ra ở bất kỳ bước nào, Orchestrator nắm rõ chính xác các bước nào đã chạy thành công trước đó để chủ động "
        "gửi các lệnh bồi hoàn (Compensate Commands) theo thứ tự ngược lại một cách chặt chẽ và an toàn tuyệt đối.",
        "Quản lý bồi hoàn tường minh và tin cậy: "
    )

    # Bảng so sánh Choreography vs Orchestration
    add_p("Bảng so sánh chi tiết giữa hai phương pháp tiếp cận Saga:")
    headers_saga = ["Đặc tính so sánh", "Choreography-based Saga", "Orchestration-based Saga"]
    rows_saga = [
        ["Bộ điều phối", "Không có; phân tán phi tập trung qua Event.", "Có một Orchestrator (State Machine) trung tâm."],
        ["Độ phức tạp workflow", "Phù hợp quy trình ngắn (2 - 3 bước).", "Phù hợp quy trình phức tạp, nhiều bước, nhiều rẽ nhánh."],
        ["Khả năng quan sát", "Kém; phải tra cứu log phân tán qua nhiều nơi.", "Tuyệt vời; trạng thái workflow được lưu tập trung một chỗ."],
        ["Khả năng kiểm thử", "Khó khăn do sự tương tác phân tán bất đồng bộ.", "Dễ dàng cô lập và kiểm thử toàn bộ luồng trạng thái."],
        ["Rủi ro phụ thuộc", "Dễ gặp lỗi phụ thuộc vòng giữa các service.", "Không có phụ thuộc vòng; giao tiếp dạng Hub-and-Spoke."],
        ["Lý do lựa chọn trong đồ án", "Không phù hợp vì quy trình mua gói cần trừ tiền, nâng cấp, bồi hoàn rất chặt chẽ.", "Được chọn: Đảm bảo kiểm soát dòng tiền, audit trail rõ ràng và khả năng bồi hoàn chính xác."]
    ]
    add_table_data(headers_saga, rows_saga, [1.5, 2.3, 2.4])

    # 8.3
    add_h1("8.3. Triển khai Saga State Machine với MassTransit")
    add_p(
        "MassTransit là một trong những framework Message Bus mã nguồn mở phổ biến và mạnh mẽ nhất trên nền tảng .NET. "
        "Nó cung cấp cơ chế Automatonymous — một bộ ngôn ngữ đặc tả (DSL - Domain Specific Language) cho phép định nghĩa các "
        "Máy trạng thái hữu hạn (Finite State Machine) hướng thông điệp với cú pháp khai báo mạch lạc và an toàn kiểu dữ liệu (Type-Safe)."
    )

    add_h2("8.3.1. Các trạng thái trong PurchasePackageSaga")
    add_p(
        "Một State Machine chuẩn mực được cấu thành từ 4 yếu tố: State (Trạng thái hiện tại), Event (Sự kiện kích hoạt), "
        "Transition (Sự chuyển dịch giữa các trạng thái) và Action (Hành động phụ như gửi thông điệp hay cập nhật dữ liệu). "
        "Trong bài toán mua gói dịch vụ tin đăng, PurchasePackageSaga quản lý vòng đời giao dịch thông qua 9 trạng thái tiêu chuẩn:"
    )
    add_bullet("Trạng thái khởi tạo ban đầu khi tiếp nhận yêu cầu mua gói.", "1. Started (Bắt đầu): ")
    add_bullet("Đang phát lệnh trừ tiền ví và chờ kết quả từ Wallet Consumer.", "2. DeductingWallet (Đang trừ tiền ví): ")
    add_bullet("Đã trừ tiền ví thành công, chuẩn bị chuyển sang bước nâng cấp tin.", "3. WalletDeducted (Đã trừ tiền ví): ")
    add_bullet("Đang phát lệnh nâng cấp thời hạn gói tin đăng cho Listing Consumer.", "4. UpgradingListing (Đang nâng cấp tin): ")
    add_bullet("Đang ghi nhận nhật ký biến động lịch sử tin đăng.", "5. RecordingHistory (Đang ghi lịch sử): ")
    add_bullet("Toàn bộ quy trình hoàn tất mỹ mãn; giao dịch kết thúc thành công.", "6. Completed (Hoàn thành): ")
    add_bullet("Phát sinh sự cố tại bước nâng cấp; đang kích hoạt lệnh hoàn tiền vào ví.", "7. Compensating (Đang bồi hoàn): ")
    add_bullet("Tiền đã được hoàn trả thành công vào ví của người dùng.", "8. Compensated (Đã bồi hoàn): ")
    add_bullet("Giao dịch kết thúc ở trạng thái thất bại an toàn (ví dụ: số dư ví không đủ ngay từ đầu).", "9. Failed (Thất bại): ")

    add_p("Đoạn mã dưới đây minh họa cách đặc tả State Machine bằng MassTransit DSL trong C#:")
    code_saga_dsl = (
        "public sealed class PurchasePackageSaga : MassTransitStateMachine<PurchasePackageSagaState>\n"
        "{\n"
        "    // Khai báo các States và Events\n"
        "    public State DeductingWallet { get; private set; } = null!;\n"
        "    public State UpgradingListing { get; private set; } = null!;\n"
        "    public State Completed { get; private set; } = null!;\n"
        "    public State Compensating { get; private set; } = null!;\n"
        "    public State Failed { get; private set; } = null!;\n"
        "\n"
        "    public Event<PurchasePackageRequested> PurchasePackageRequestedEvent { get; private set; } = null!;\n"
        "    public Event<WalletDeducted> WalletDeductedEvent { get; private set; } = null!;\n"
        "    public Event<WalletDeductionFailed> WalletDeductionFailedEvent { get; private set; } = null!;\n"
        "    public Event<ListingUpgraded> ListingUpgradedEvent { get; private set; } = null!;\n"
        "    public Event<ListingUpgradeFailed> ListingUpgradeFailedEvent { get; private set; } = null!;\n"
        "\n"
        "    public PurchasePackageSaga()\n"
        "    {\n"
        "        InstanceState(x => x.CurrentState);\n"
        "        Event(() => PurchasePackageRequestedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));\n"
        "        Event(() => WalletDeductedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));\n"
        "        Event(() => ListingUpgradeFailedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));\n"
        "\n"
        "        // Khởi động Saga: Nhận Request -> Lưu State -> Phát lệnh trừ ví\n"
        "        Initially(\n"
        "            When(PurchasePackageRequestedEvent)\n"
        "                .TransitionTo(DeductingWallet)\n"
        "                .Publish(context => new DeductWalletCommand(\n"
        "                    context.Saga.CorrelationId, context.Saga.UserId, context.Saga.ChargeAmount))\n"
        "        );\n"
        "\n"
        "        // Bước 1 Thành công: Trừ ví xong -> Chuyển sang Nâng cấp tin\n"
        "        During(DeductingWallet,\n"
        "            When(WalletDeductedEvent)\n"
        "                .TransitionTo(UpgradingListing)\n"
        "                .Publish(context => new UpgradeListingCommand(\n"
        "                    context.Saga.CorrelationId, context.Saga.ListingId, context.Saga.PackageCode)),\n"
        "            When(WalletDeductionFailedEvent)\n"
        "                .TransitionTo(Failed)\n"
        "        );\n"
        "\n"
        "        // Bước 2 Thất bại: Nâng cấp lỗi -> Kích hoạt Bồi hoàn (Hoàn tiền)\n"
        "        During(UpgradingListing,\n"
        "            When(ListingUpgradeFailedEvent)\n"
        "                .TransitionTo(Compensating)\n"
        "                .Publish(context => new RefundWalletCommand(\n"
        "                    context.Saga.CorrelationId, context.Saga.UserId, context.Saga.ChargeAmount))\n"
        "        );\n"
        "    }\n"
        "}"
    )
    add_code(code_saga_dsl, "Mã nguồn 8.1: Đặc tả cấu trúc luồng chuyển đổi trạng thái bằng MassTransit DSL")

    add_h2("8.3.2. Lưu trữ bền vững trạng thái Saga trong SQL Server (SagaListingStates)")
    add_p(
        "Trong môi trường sản xuất, một workflow có thể kéo dài vài giây, vài giờ hoặc thậm chí vài ngày (chẳng hạn quy trình chờ kiểm duyệt thủ công). "
        "Nếu máy chủ Worker bị khởi động lại (Restart/Crash) giữa chừng khi thông điệp đang xử lý, toàn bộ trạng thái trong RAM sẽ bị mất. "
        "Do đó, MassTransit yêu cầu một cơ chế lưu trữ bền vững trạng thái (Saga State Persistence)."
    )
    add_p(
        "Thông qua gói thư viện MassTransit.EntityFrameworkCore, thực thể PurchasePackageSagaState được ánh xạ trực tiếp "
        "vào một bảng cơ sở dữ liệu quan hệ trong SQL Server. Mỗi phiên giao dịch đại diện bởi đúng một bản ghi duy nhất, với khóa chính là CorrelationId."
    )
    add_p(
        "Đặc biệt, để xử lý bài toán nhiều thông điệp liên quan đến cùng một Saga Instance bay về cùng một tích tắc từ Message Queue, "
        "MassTransit áp dụng kỹ thuật Kiểm soát tương tranh lạc quan (Optimistic Concurrency Control) thông qua interface ISagaVersion:"
    )
    add_bullet(
        "Thực thể Saga State cài đặt interface ISagaVersion chứa thuộc tính int Version. Trong cấu hình Entity Framework Core, "
        "cột này được thiết lập bằng builder.Property(x => x.Version).IsConcurrencyToken().",
        "Cơ chế Concurrency Token: "
    )
    add_bullet(
        "Mỗi khi MassTransit cập nhật trạng thái mới của Saga, câu lệnh SQL UPDATE được sinh ra có dạng: "
        "UPDATE PurchasePackageSagaState SET CurrentState = @newState, Version = Version + 1 WHERE CorrelationId = @id AND Version = @oldVersion.",
        "Xác thực phiên bản khi UPDATE: "
    )
    add_bullet(
        "Nếu có một tiến trình khác đã cập nhật trước đó khiến Version thay đổi, lệnh UPDATE sẽ trả về số dòng tác động bằng 0. "
        "EF Core lập tức phát hiện và ném ngoại lệ DbUpdateConcurrencyException. MassTransit tự động bắt ngoại lệ này, tải lại bản ghi mới nhất từ DB "
        "và thực thi lại message một cách an toàn mà không bao giờ gây đè dữ liệu sai lệch (Lost Update).",
        "Ngăn chặn Race Condition: "
    )

    code_saga_config = (
        "public class PurchasePackageSagaStateConfiguration : IEntityTypeConfiguration<PurchasePackageSagaState>\n"
        "{\n"
        "    public void Configure(EntityTypeBuilder<PurchasePackageSagaState> builder)\n"
        "    {\n"
        "        builder.ToTable(\"PurchasePackageSagaState\");\n"
        "        builder.HasKey(x => x.CorrelationId);\n"
        "        builder.Property(x => x.CorrelationId).ValueGeneratedNever();\n"
        "        builder.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();\n"
        "        builder.Property(x => x.PackageCode).HasMaxLength(50).IsRequired();\n"
        "        builder.Property(x => x.ChargeAmount).HasPrecision(18, 2);\n"
        "        // Đánh dấu Version làm token kiểm soát tương tranh lạc quan\n"
        "        builder.Property(x => x.Version).IsConcurrencyToken();\n"
        "    }\n"
        "}"
    )
    add_code(code_saga_config, "Mã nguồn 8.2: Cấu hình EF Core cho thực thể lưu trữ trạng thái Saga với Concurrency Token")

    # 8.4
    add_h1("8.4. Đảm bảo độ tin cậy thông điệp với Outbox và Inbox")

    add_h2("8.4.1. Transactional Outbox Pattern ngăn ngừa mất sự kiện sau khi commit CSDL")
    add_p(
        "Một trong những cạm bẫy nguy hiểm nhất trong kiến trúc phân tán hướng sự kiện là vấn đề 'Ghi hai nơi' (Dual-Write Problem). "
        "Xét trường hợp khi một dịch vụ cần thực hiện đồng thời hai thao tác: (1) Lưu bản ghi nghiệp vụ vào cơ sở dữ liệu và (2) Bắn sự kiện lên RabbitMQ:"
    )
    add_bullet(
        "Nếu lưu CSDL thành công nhưng trước khi kịp bắn message lên RabbitMQ thì máy chủ bị mất điện hoặc kết nối mạng tới broker bị ngắt $\\rightarrow$ "
        "Dữ liệu đã nằm trong CSDL nhưng không có thông điệp nào được gửi đi. Toàn bộ các dịch vụ phía sau bị tê liệt, đơn hàng bị 'treo' vĩnh viễn.",
        "Trường hợp A (Lưu DB trước, gửi Queue sau): "
    )
    add_bullet(
        "Nếu bắn message lên RabbitMQ trước rồi mới lưu CSDL, nhưng câu lệnh CSDL thất bại (do lỗi vi phạm khóa ngoại, timeout hoặc deadlock) $\\rightarrow$ "
        "Message đã bay vào hàng đợi và các consumer phía sau bắt đầu thực thi nghiệp vụ, trong khi CSDL nguồn hoàn toàn không tồn tại bản ghi đó (Xử lý dữ liệu ma - Ghost Data).",
        "Trường hợp B (Gửi Queue trước, lưu DB sau): "
    )
    add_p(
        "Vì cơ sở dữ liệu quan hệ (SQL Server) và Message Broker (RabbitMQ) là hai hệ thống lưu trữ phân tán độc lập, ta không thể bọc cả hai "
        "trong một Transaction cục bộ đơn lẻ. Để giải quyết triệt để bài toán này, mẫu thiết kế Transactional Outbox Pattern được áp dụng:"
    )
    add_bullet(
        "Tạo một bảng OutboxMessages nằm ngay bên trong cơ sở dữ liệu của dịch vụ. Thay vì gửi trực tiếp lên RabbitMQ, thông điệp sự kiện được "
        "đóng gói thành JSON và INSERT trực tiếp vào bảng OutboxMessages trong cùng một Local Transaction với dữ liệu nghiệp vụ.",
        "1. Lưu thông điệp nguyên tử vào Outbox: "
    )
    add_bullet(
        "Khi unitOfWork.SaveChangesAsync() được gọi, cơ sở dữ liệu đảm bảo tính chất ACID tuyệt đối: Hoặc cả dữ liệu nghiệp vụ VÀ thông điệp outbox "
        "đều được lưu vào đĩa cứng, hoặc không có gì được lưu cả. Hoàn toàn không bao giờ có rủi ro mất mát.",
        "2. Đảm bảo ACID nguyên tử: "
    )
    add_bullet(
        "Một tiến trình nền (Outbox Dispatcher Background Worker) chạy định kỳ, đọc các bản ghi chưa xử lý (ProcessedOn IS NULL) trong bảng OutboxMessages, "
        "tiến hành Publish lên RabbitMQ, và cập nhật ProcessedOn = UtcNow sau khi nhận được xác nhận từ broker.",
        "3. Chuyển giao thông điệp bất đồng bộ (Message Relay): "
    )

    code_outbox = (
        "// Lưu nghiệp vụ và Outbox trong cùng 1 Transaction ACID duy nhất\n"
        "await workflowRepository.AddAsync(workflow, cancellationToken);\n"
        "await workflowRepository.AddIdempotencyRequestAsync(request, cancellationToken);\n"
        "await workflowRepository.AddOutboxMessageAsync(\n"
        "    new OutboxMessage(\"PurchasePackageRequested\", payloadJson, correlationId), cancellationToken);\n"
        "\n"
        "// Commit nguyên tử: Cả 3 bản ghi cùng thành công hoặc cùng rollback\n"
        "await unitOfWork.SaveChangesAsync(cancellationToken);"
    )
    add_code(code_outbox, "Mã nguồn 8.3: Ghi dữ liệu nghiệp vụ và Outbox Message trong cùng một giao dịch nguyên tử")

    add_h2("8.4.2. Inbox Deduplication ngăn chặn xử lý trùng lặp tại Consumer")
    add_p(
        "Ở chiều ngược lại, khi các thông điệp được chuyển từ Message Broker đến Consumer, hầu hết các hệ thống hàng đợi chuẩn công nghiệp (RabbitMQ, Kafka) "
        "đều hoạt động theo ngữ nghĩa At-least-once Delivery (Chuyển giao ít nhất một lần). Điều này có nghĩa là broker bảo đảm message sẽ tới đích, "
        "nhưng không đảm bảo nó chỉ tới đúng một lần:"
    )
    add_p(
        "Giả sử Consumer đã nhận message, xử lý trừ tiền ví thành công, nhưng trong tích tắc chuẩn bị gửi gói tin Ack (Acknowledge) về cho RabbitMQ "
        "thì kết nối mạng bị chập chờn. RabbitMQ coi như Consumer chưa xử lý xong và sẽ tiến hành gửi lại (Redelivery) thông điệp đó một lần nữa. "
        "Nếu Consumer không có cơ chế phòng vệ, tài khoản của người dùng sẽ bị trừ tiền hai lần."
    )
    add_p(
        "Để biến ngữ nghĩa At-least-once thành Exactly-once Processing (Xử lý đúng một lần duy nhất) ở tầng ứng dụng, mẫu thiết kế Inbox Deduplication Pattern được triển khai:"
    )
    add_bullet(
        "Thiết lập một bảng InboxMessages lưu trữ MessageId hoặc cặp khóa duy nhất (ConsumerName, MessageId). "
        "Mỗi message bay qua broker đều mang một MessageId duy nhất toàn cục.",
        "Bảng theo dõi Inbox: "
    )
    add_bullet(
        "Trước khi thực thi logic nghiệp vụ, Consumer kiểm tra xem MessageId này đã được ghi nhận trong InboxMessages chưa. "
        "Nếu đã có $\\rightarrow$ Consumer hiểu rằng đây là thông điệp trùng lặp (Duplicate Message), lập tức bỏ qua việc trừ tiền và gửi Ack hoàn tất.",
        "Kiểm tra tính trùng lặp: "
    )
    add_bullet(
        "Nếu chưa có $\\rightarrow$ Consumer thực thi trừ tiền ví, đồng thời INSERT MessageId vào bảng InboxMessages trong cùng một transaction. "
        "Ràng buộc Unique Constraint trên bảng Inbox sẽ chặn đứng mọi xung đột tương tranh nếu có hai worker cùng nhận một message đồng thời.",
        "Ghi nhận nguyên tử: "
    )

    # 8.5
    add_h1("8.5. Cơ chế Idempotency Request và Bồi hoàn tự động (Compensating Actions)")

    add_h2("8.5.1. Bảng IdempotencyRequests và cơ chế nhận diện request lặp qua RequestHash")
    add_p(
        "Tính lũy thừa (Idempotency) là thuộc tính của một thao tác mà khi thực hiện nhiều lần với cùng một tham số đầu vào, "
        "kết quả trạng thái của hệ thống không thay đổi so với việc chỉ thực hiện một lần duy nhất: f(f(x)) = f(x). "
        "Trong các giao dịch tài chính nhạy cảm, Idempotency là lá chắn tối hậu ngăn ngừa rủi ro người dùng bấm đúp chuột (Double Click) "
        "hoặc ứng dụng di động tự động thử lại (Client Retry) khi mạng 4G bị gián đoạn."
    )
    add_p(
        "Hệ thống triển khai cơ chế Idempotency chuẩn công nghiệp (theo chuẩn IETF Draft và đặc tả của Stripe) thông qua bảng IdempotencyRequests với 3 lớp bảo vệ:"
    )
    add_bullet(
        "Mỗi khi người dùng bấm nút Mua trên giao diện, Frontend sinh ra một chuỗi UUID duy nhất và truyền vào HTTP Header Idempotency-Key. "
        "Bảng IdempotencyRequests lưu trữ cặp (UserId, Key) với ràng buộc Unique Index trong cơ sở dữ liệu.",
        "Lớp 1 - Khóa định danh duy nhất (Idempotency-Key Header): "
    )
    add_bullet(
        "Để ngăn chặn hành vi tấn công giả mạo hoặc lỗi client tái sử dụng một Idempotency-Key cũ cho một đơn hàng hoàn toàn khác, "
        "hệ thống tính toán mã băm SHA-256 từ toàn bộ nội dung Request Body (ListingId, PackageCode, ChargeAmount) và lưu vào cột RequestHash. "
        "Khi nhận một request có cùng Key, hệ thống kiểm tra: nếu RequestHash không khớp, lập tức ném lỗi 400 Bad Request từ chối xử lý.",
        "Lớp 2 - Kiểm tra tính toàn vẹn nội dung bằng SHA-256 RequestHash: "
    )
    add_bullet(
        "Bản ghi Idempotency được thiết lập thời gian hết hạn ExpiresAt (ví dụ: sau 24 giờ). Sau thời gian này, các key cũ tự động hết hiệu lực "
        "và một tác vụ dọn dẹp (Housekeeping Cronjob) có thể xóa sạch dữ liệu cũ giúp bảng luôn duy trì kích thước nhỏ gọn và tốc độ truy vấn cao.",
        "Lớp 3 - Thời gian sống hữu hạn (Time-To-Live / TTL): "
    )

    code_idempotency = (
        "// Kiểm tra Idempotency Request trong WorkflowService\n"
        "var existing = await workflowRepository.GetIdempotencyRequestAsync(command.UserId, command.IdempotencyKey);\n"
        "if (existing is not null)\n"
        "{\n"
        "    // Nếu cùng Key nhưng Body bị thay đổi -> Phát hiện xung đột dữ liệu\n"
        "    if (!string.Equals(existing.RequestHash, command.RequestHash, StringComparison.Ordinal))\n"
        "    {\n"
        "        throw new InvalidOperationException(\"The Idempotency-Key has already been used with a different request.\");\n"
        "    }\n"
        "    // Đúng request cũ -> Replay kết quả đã lưu mà KHÔNG trừ tiền lần 2\n"
        "    var replay = await workflowRepository.GetByCorrelationIdAsync(existing.CorrelationId);\n"
        "    return ToDto(replay);\n"
        "}"
    )
    add_code(code_idempotency, "Mã nguồn 8.4: Logic xác thực tính toàn vẹn và tái phát kết quả (Replay) của Idempotency")

    add_h2("8.5.2. Hành động bồi hoàn hoàn tiền vào ví (Refund Ledger) khi xảy ra lỗi nâng cấp tin")
    add_p(
        "Trong giao dịch phân tán, Giao dịch bồi hoàn (Compensating Transaction) mang bản chất là một sự hoàn tác ngữ nghĩa (Semantic Undo), "
        "hoàn toàn khác biệt với lệnh ROLLBACK vật lý trong cơ sở dữ liệu quan hệ:"
    )
    add_p(
        "Khi bước trừ tiền ví (Deduct Wallet) đã commit thành công, số dư trong cơ sở dữ liệu đã thực sự bị giảm xuống. "
        "Nếu bước tiếp theo là nâng cấp tin đăng (Upgrade Listing) gặp sự cố (ví dụ: tin đăng đã bị khóa hoặc hết hạn trước đó), "
        "Saga Orchestrator không thể 'quay ngược thời gian'. Thay vào đó, nó phải phát động một hành động tích cực mới: phát lệnh RefundWalletCommand "
        "để nạp bù lại chính xác số tiền đã trừ vào ví của người dùng."
    )
    add_p(
        "Để bảo toàn nguyên tắc kế toán tài chính, bảng WalletTransactions được thiết kế theo mô hình Sổ cái bất biến (Append-only Ledger):"
    )
    add_bullet(
        "Tuyệt đối không bao giờ được phép sửa đổi (UPDATE) hoặc xóa bỏ (DELETE) các dòng giao dịch đã ghi trong lịch sử ví. "
        "Mọi sự kiện tài chính đều phải là các bản ghi mới được chèn thêm (INSERT only).",
        "Nguyên tắc bất biến (Immutability): "
    )
    add_bullet(
        "Khi trừ tiền, ghi một bản ghi WalletTransaction có TransactionType = Charge. Khi bồi hoàn, ghi tiếp một bản ghi mới có TransactionType = Refund. "
        "Cả hai bản ghi này đều mang cùng một mã CorrelationId liên kết chặt chẽ với nhau.",
        "Tính đối ứng kiểm toán (Audit Trail): "
    )
    add_bullet(
        "Kiểm toán viên hoặc người dùng nhìn vào sổ cái luôn thấy dòng tiền minh bạch: Bị trừ 50.000đ lúc 10:00:00 và được hoàn trả 50.000đ lúc 10:00:02 "
        "kèm theo lý do bồi hoàn cụ thể. Điều này triệt tiêu hoàn toàn rủi ro sai lệch số dư bí ẩn.",
        "Minh bạch dòng tiền: "
    )

    # 8.6
    add_h1("8.6. Thực hành: Quy trình mua gói VIP và kịch bản bồi hoàn")
    add_p(
        "Để minh chứng tính đúng đắn và hiệu quả của các kiến trúc lý thuyết đã trình bày, phần này mô tả chi tiết hai kịch bản thực nghiệm "
        "trong hệ thống: Luồng tiêu chuẩn thành công (Happy Path) và Luồng kích hoạt bồi hoàn tự động khi có sự cố (Compensation Flow)."
    )

    add_h2("8.6.1. Luồng mua gói thành công (Happy Path)")
    add_p(
        "Kịch bản: Người dùng có số dư ví 200.000đ, thực hiện mua gói VIP trị giá 50.000đ cho một tin đăng hợp lệ đang ở trạng thái Published."
    )
    add_bullet("Người dùng bấm nút 'Mua gói VIP'. Frontend sinh Idempotency-Key và gửi HTTP POST /api/v1/listings/{id}/purchase-package.", "Bước 1 - Tiếp nhận Request: ")
    add_bullet("API Controller kiểm tra Idempotency, tính RequestHash SHA-256, sinh mã CorrelationId duy nhất. Trong 1 transaction ACID, hệ thống INSERT đồng thời vào 3 bảng: WorkflowInstances (Started), IdempotencyRequests và OutboxMessages (PurchasePackageRequested).", "Bước 2 - Ghi nhận nguyên tử: ")
    add_bullet("API lập tức trả về mã HTTP 202 Accepted kèm header Location: /api/v1/workflows/{correlationId}. Người dùng thấy popup 'Đang xử lý giao dịch...' mà không bị treo giao diện.", "Bước 3 - Phản hồi bất đồng bộ (202 Accepted): ")
    add_bullet("OutboxDispatcherWorker quét bảng Outbox, lấy message và Publish lên sàn giao dịch RabbitMQ. Saga State Machine nhận event, chuyển trạng thái sang DeductingWallet và phát lệnh DeductWalletCommand.", "Bước 4 - Khởi động Saga & Lệnh trừ tiền: ")
    add_bullet("DeductWalletConsumer nhận lệnh, gọi domain wallet.Charge(50000), ghi sổ cái WalletTransaction (Type = Charge), commit DB và Publish event WalletDeducted.", "Bước 5 - Trừ tiền ví thành công: ")
    add_bullet("Saga bắt event WalletDeducted, chuyển sang UpgradingListing, phát lệnh UpgradeListingCommand. UpgradeListingConsumer nhận lệnh, gia hạn tin đăng, ghi ListingHistory, commit DB và Publish event ListingUpgraded.", "Bước 6 - Nâng cấp tin đăng: ")
    add_bullet("Saga bắt event ListingUpgraded, chuyển trạng thái sang Completed. Các bản ghi WorkflowSteps ghi nhận toàn bộ mốc thời gian hoàn tất.", "Bước 7 - Hoàn tất Workflow: ")
    add_bullet("Frontend thực hiện Polling gọi GET /api/v1/workflows/{correlationId}, nhận được trạng thái Completed, tự động hiển thị thông báo thành công và chuyển màu gói tin sang VIP.", "Bước 8 - Cập nhật giao diện: ")

    add_h2("8.6.2. Giả lập sự cố nâng cấp tin và kiểm chứng luồng tự động hoàn tiền (Compensation Flow)")
    add_p(
        "Kịch bản: Người dùng có số dư ví 200.000đ, thực hiện mua gói VIP 50.000đ. Tuy nhiên, đúng lúc này tin đăng bị Moderator xóa hoặc bị lỗi khóa dữ liệu, "
        "khiến bước nâng cấp tin thất bại sau khi ví đã bị trừ tiền."
    )
    add_bullet("Các bước tiếp nhận, trừ tiền ví 50.000đ và ghi sổ cái Charge diễn ra thành công tương tự như Happy Path. Số dư ví người dùng giảm xuống còn 150.000đ.", "Giai đoạn 1 - Trừ tiền thành công: ")
    add_bullet("Saga phát lệnh UpgradeListingCommand tới UpgradeListingConsumer. Consumer kiểm tra và phát hiện tin đăng không hợp lệ, ném ngoại lệ InvalidOperationException và Publish event ListingUpgradeFailed kèm lý do lỗi.", "Giai đoạn 2 - Phát hiện sự cố nâng cấp: ")
    add_bullet("Saga State Machine bắt event ListingUpgradeFailed. Thay vì sập toàn bộ hệ thống, Saga chuyển sang trạng thái Compensating (Bồi hoàn) và lập tức phát lệnh bồi hoàn RefundWalletCommand mang cùng CorrelationId.", "Giai đoạn 3 - Kích hoạt bồi hoàn tự động: ")
    add_bullet("RefundWalletConsumer đón nhận lệnh hoàn tiền, gọi domain wallet.Refund(50000). Số dư ví tăng trở lại 200.000đ. Consumer ghi một bản ghi sổ cái mới WalletTransaction (Type = Refund, CorrelationId giữ nguyên), commit DB và Publish event WalletRefunded.", "Giai đoạn 4 - Thực thi hoàn tiền ví: ")
    add_bullet("Saga bắt event WalletRefunded, chuyển trạng thái kết thúc sang Compensated. Bản ghi WorkflowSteps ghi nhận chi tiết: Bước Deduct thành công, Bước Upgrade thất bại, Bước Refund thành công.", "Giai đoạn 5 - Kết thúc an toàn: ")
    add_bullet("Frontend Polling nhận về trạng thái Compensated kèm thông báo: 'Nâng cấp tin đăng thất bại do tin không hợp lệ. Số tiền 50.000đ đã được hoàn trả lại ví của bạn an toàn.'", "Bước 6 - Thông báo minh bạch cho người dùng: ")

    # KẾT LUẬN
    add_h1("KẾT LUẬN CHƯƠNG 8")
    add_p(
        "Chương 8 đã giải quyết triệt để bài toán khó khăn và phức tạp nhất trong kiến trúc backend hiện đại: "
        "xử lý giao dịch phân tán và điều phối workflow tự động đảm bảo tính toàn vẹn dữ liệu tài chính mà không làm suy giảm hiệu năng hệ thống."
    )
    add_p(
        "Qua việc phân tích sâu sắc các hạn chế cố hữu của giao thức Two-Phase Commit (2PC) và sự cần thiết của định lý CAP / nguyên lý Eventual Consistency, "
        "nhóm nghiên cứu đã làm sáng tỏ nền tảng lý thuyết dẫn tới sự lựa chọn mô hình Orchestration-based Saga. "
        "Việc kết hợp State Machine của MassTransit với Message Broker RabbitMQ cho phép hệ thống quản lý các quy trình nghiệp vụ phức tạp "
        "một cách tập trung, minh bạch và có khả năng bồi hoàn tự động chuẩn xác."
    )
    add_p(
        "Bên cạnh đó, việc áp dụng đồng bộ các mẫu thiết kế bảo vệ độ tin cậy thông điệp bao gồm: Transactional Outbox Pattern "
        "(loại bỏ hoàn toàn vấn đề Dual-Write), Inbox Deduplication (chuyển đổi At-least-once thành Exactly-once processing), "
        "Idempotency Request kết hợp SHA-256 Hash (chống double-click và gian lận payload), cùng kiến trúc Sổ cái bất biến (Append-only Ledger) "
        "đã tạo nên một pháo đài kiến trúc kiên cố. Hệ thống PropNest Backend không chỉ đạt được thông lượng xử lý cao, độ sẵn sàng liên tục "
        "mà còn bảo đảm dữ liệu số dư và trạng thái tin đăng luôn đạt được sự nhất quán cuối cùng hoàn hảo trong mọi điều kiện vận hành bất định của mạng phân tán."
    )

    # Lưu file
    out_dir = "docs"
    os.makedirs(out_dir, exist_ok=True)
    out_path_docs = os.path.join(out_dir, "Bao_cao_chung_Chuong_8.docx")
    out_path_root = "Bao_cao_chung_Chuong_8.docx"

    doc.save(out_path_docs)
    doc.save(out_path_root)
    print(f"File created successfully at:\n- {os.path.abspath(out_path_docs)}\n- {os.path.abspath(out_path_root)}")

if __name__ == "__main__":
    create_report()

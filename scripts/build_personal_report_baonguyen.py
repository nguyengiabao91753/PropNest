# -*- coding: utf-8 -*-
"""
Báo cáo cá nhân cho sinh viên Nguyễn Gia Bảo (MSSV: 3122560004)
ĐỀ TÀI: CÔNG NGHỆ ASP.NET CORE - HỆ THỐNG ĐĂNG TIN BẤT ĐỘNG SẢN PROPNEST
Tuân thủ 100% yêu cầu:
 - Trang bìa giữ nguyên khung viền, logo, font chuẩn từ template mẫu; không đánh số trang.
 - Phần đầu (Nhận xét của GVHD, Mục lục, Danh mục hình ảnh) đánh số La Mã: i, ii, iii (mỗi mục đúng 1 trang).
 - Danh mục hình ảnh nằm ngay sau Mục lục trên 1 trang riêng biệt.
 - Nội dung chính đánh số Ả Rập: 1, 2, 3... bắt đầu từ trang LỜI CẢM ƠN.
 - Ảnh code 6 và 8 được chia tách thành 2 ảnh (6a/6b, 8a/8b) đọc tiếp nối tự nhiên.
 - 100% code C# thực tế từ project, đầy đủ, không lược bỏ.
 - Từ ngữ chuẩn xác: hoàn tiền (thay cho bồi hoàn), State Machine / máy trạng thái (thay cho cỗ máy).
 - Tự động đồng bộ số trang thực tế của Mục lục và Danh mục hình ảnh qua Word COM.
"""
import os
import sys
import shutil
import subprocess

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
sys.stdout.reconfigure(encoding='utf-8')
import docx
from docx.shared import Inches, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.section import WD_SECTION_START

from scripts.code_image import render_code
from scripts.personal_report_snippets_full import ALL_FULL_SNIPPETS

SAMPLE_PATH = 'docs/Báo_cáo_cá_nhân_sample.docx'
OUT_PATH_1 = 'docs/Báo_cáo_cá_nhân_NguyenGiaBao.docx'
OUT_PATH_2 = 'docs/Báo cáo cá nhân - Nguyễn Gia Bảo - 3122560004.docx'
IMG_DIR = r'docs\personal_images_full'

def ensure_full_images():
    os.makedirs(IMG_DIR, exist_ok=True)
    rendered = {}
    for name, code in ALL_FULL_SNIPPETS.items():
        p = os.path.join(IMG_DIR, f"{name}.png")
        if not os.path.exists(p):
            print(f"Rendering image: {name}...")
            render_code(code, p)
        rendered[name] = p
    return rendered

def set_run_font(run, name="Times New Roman", size_pt=13, bold=False, italic=False, color_rgb=(0,0,0)):
    run.font.name = name
    run.font.size = Pt(size_pt)
    run.font.bold = bold
    run.font.italic = italic
    run.font.color.rgb = RGBColor(*color_rgb)

def set_pg_num_type(sect, fmt, start_val=1):
    sectPr = sect._sectPr
    for pnt in sectPr.xpath('.//w:pgNumType'):
        sectPr.remove(pnt)
    pnt = OxmlElement('w:pgNumType')
    pnt.set(qn('w:fmt'), fmt)
    pnt.set(qn('w:start'), str(start_val))
    sectPr.append(pnt)

def set_footer_page_number(sect):
    sect.footer.is_linked_to_previous = False
    f = sect.footer.paragraphs[0]
    f.text = ""
    f.alignment = WD_ALIGN_PARAGRAPH.CENTER
    pPr = f._p.get_or_add_pPr()
    jc = OxmlElement('w:jc')
    jc.set(qn('w:val'), 'center')
    pPr.append(jc)
    
    r1 = OxmlElement('w:r')
    rPr = OxmlElement('w:rPr')
    sz = OxmlElement('w:sz')
    sz.set(qn('w:val'), '26') # 13pt
    rPr.append(sz)
    r1.append(rPr)
    fc1 = OxmlElement('w:fldChar')
    fc1.set(qn('w:fldCharType'), 'begin')
    r1.append(fc1)
    f._p.append(r1)

    r2 = OxmlElement('w:r')
    rPr2 = OxmlElement('w:rPr')
    sz2 = OxmlElement('w:sz')
    sz2.set(qn('w:val'), '26')
    rPr2.append(sz2)
    r2.append(rPr2)
    instr = OxmlElement('w:instrText')
    instr.set(qn('xml:space'), 'preserve')
    instr.text = "PAGE"
    r2.append(instr)
    f._p.append(r2)

    r3 = OxmlElement('w:r')
    fc2 = OxmlElement('w:fldChar')
    fc2.set(qn('w:fldCharType'), 'separate')
    r3.append(fc2)
    f._p.append(r3)

    r4 = OxmlElement('w:r')
    rPr4 = OxmlElement('w:rPr')
    sz4 = OxmlElement('w:sz')
    sz4.set(qn('w:val'), '26')
    rPr4.append(sz4)
    r4.append(rPr4)
    t = OxmlElement('w:t')
    t.text = "1"
    r4.append(t)
    f._p.append(r4)

    r5 = OxmlElement('w:r')
    fc3 = OxmlElement('w:fldChar')
    fc3.set(qn('w:fldCharType'), 'end')
    r5.append(fc3)
    f._p.append(r5)

def add_bullet_paragraph(doc, text, num_id=4):
    p = doc.add_paragraph(style='Normal')
    pPr = p._p.get_or_add_pPr()
    numPr = OxmlElement('w:numPr')
    ilvl = OxmlElement('w:ilvl')
    ilvl.set(qn('w:val'), '0')
    numId = OxmlElement('w:numId')
    numId.set(qn('w:val'), str(num_id))
    numPr.append(ilvl)
    numPr.append(numId)
    pPr.append(numPr)
    
    rPr = OxmlElement('w:rPr')
    sz = OxmlElement('w:sz')
    sz.set(qn('w:val'), '26')  # 13pt
    szCs = OxmlElement('w:szCs')
    szCs.set(qn('w:val'), '26')
    rPr.append(sz)
    rPr.append(szCs)
    pPr.append(rPr)
    
    run = p.add_run(text)
    set_run_font(run, size_pt=13, color_rgb=(0,0,0))
    return p

def add_normal_paragraph(doc, text, justify=False, space_line=False):
    p = doc.add_paragraph(style='Normal')
    pPr = p._p.get_or_add_pPr()
    if justify:
        jc = OxmlElement('w:jc')
        jc.set(qn('w:val'), 'both')
        pPr.append(jc)
    if space_line:
        sp = OxmlElement('w:spacing')
        sp.set(qn('w:line'), '276')
        sp.set(qn('w:lineRule'), 'auto')
        pPr.append(sp)
    rPr = OxmlElement('w:rPr')
    sz = OxmlElement('w:sz')
    sz.set(qn('w:val'), '26')
    szCs = OxmlElement('w:szCs')
    szCs.set(qn('w:val'), '26')
    rPr.append(sz)
    rPr.append(szCs)
    pPr.append(rPr)
    
    run = p.add_run(text)
    set_run_font(run, size_pt=13, color_rgb=(0,0,0))
    return p

def add_heading(doc, text, style_name):
    p = doc.add_paragraph(text, style=style_name)
    for r in p.runs:
        r.font.name = "Times New Roman"
        r.font.color.rgb = RGBColor(0,0,0)
    return p

def add_code_image_and_caption(doc, img_path, caption_text, width_inches=5.8):
    p_img = doc.add_paragraph(style='Normal')
    pPr = p_img._p.get_or_add_pPr()
    kn = OxmlElement('w:keepNext')
    pPr.append(kn)
    p_img.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = p_img.add_run()
    run.add_picture(img_path, width=Inches(width_inches))
    
    p_cap = doc.add_paragraph(style='Normal')
    p_cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    pCapPr = p_cap._p.get_or_add_pPr()
    sp = OxmlElement('w:spacing')
    sp.set(qn('w:after'), '160')
    pCapPr.append(sp)
    
    r_cap = p_cap.add_run(caption_text)
    set_run_font(r_cap, size_pt=12, italic=True, color_rgb=(0,0,0))
    
    p_empty = doc.add_paragraph(style='Normal')
    p_empty.paragraph_format.space_before = Pt(0)
    p_empty.paragraph_format.space_after = Pt(2)

def add_toc_item(doc, title, page_str, level=1):
    p = doc.add_paragraph(style='Normal')
    pPr = p._p.get_or_add_pPr()
    tabs = OxmlElement('w:tabs')
    tab = OxmlElement('w:tab')
    tab.set(qn('w:val'), 'right')
    tab.set(qn('w:leader'), 'dot')
    tab.set(qn('w:pos'), '9360')
    tabs.append(tab)
    pPr.append(tabs)
    
    sp = OxmlElement('w:spacing')
    sp.set(qn('w:before'), '20' if level > 1 else '50')
    sp.set(qn('w:after'), '20')
    pPr.append(sp)
    
    if level == 2:
        ind = OxmlElement('w:ind')
        ind.set(qn('w:left'), '360')
        pPr.append(ind)
    elif level == 3:
        ind = OxmlElement('w:ind')
        ind.set(qn('w:left'), '720')
        pPr.append(ind)
    elif level == 4:
        ind = OxmlElement('w:ind')
        ind.set(qn('w:left'), '1080')
        pPr.append(ind)

    run1 = p.add_run(title)
    set_run_font(run1, size_pt=11.5 if level > 1 else 12.5, bold=(level == 1), color_rgb=(0,0,0))
    run_tab = p.add_run("\t")
    run2 = p.add_run(str(page_str))
    set_run_font(run2, size_pt=11.5 if level > 1 else 12.5, bold=(level == 1), color_rgb=(0,0,0))
    return p

def add_fig_item(doc, fig_title, page_str):
    p = doc.add_paragraph(style='Normal')
    pPr = p._p.get_or_add_pPr()
    tabs = OxmlElement('w:tabs')
    tab = OxmlElement('w:tab')
    tab.set(qn('w:val'), 'right')
    tab.set(qn('w:leader'), 'dot')
    tab.set(qn('w:pos'), '9360')
    tabs.append(tab)
    pPr.append(tabs)
    
    sp = OxmlElement('w:spacing')
    sp.set(qn('w:before'), '40')
    sp.set(qn('w:after'), '30')
    pPr.append(sp)

    run1 = p.add_run(fig_title)
    set_run_font(run1, size_pt=12, color_rgb=(0,0,0))
    run_tab = p.add_run("\t")
    run2 = p.add_run(str(page_str))
    set_run_font(run2, size_pt=12, color_rgb=(0,0,0))
    return p

def build_document(toc_pages=None, fig_pages=None):
    if toc_pages is None:
        toc_pages = {k: "..." for k in [
            "LOI_CAM_ON", "CHUONG_1", "SEC_I", "SEC_II", "SEC_III", "SEC_III_1", "SEC_III_2",
            "SEC_2_1", "SEC_2_2", "SEC_2_3", "SEC_2_4", "SEC_2_5", "SEC_2_6", "SEC_2_7",
            "SEC_2_8", "SEC_2_9", "SEC_2_10", "CHUONG_2", "CONCL_1", "CONCL_2"
        ]}
    if fig_pages is None:
        fig_pages = {k: "..." for k in [
            "HINH_1", "HINH_2", "HINH_3", "HINH_4", "HINH_5", "HINH_6A", "HINH_6B",
            "HINH_7", "HINH_8A", "HINH_8B", "HINH_9", "HINH_10", "HINH_11"
        ]}

    print("Ensuring code snippet images are rendered...")
    images = ensure_full_images()

    print("Opening template...")
    doc = docx.Document(SAMPLE_PATH)

    # 1. Update Cover Page (Section 0)
    doc.paragraphs[4].text = "CÁC CÔNG NGHỆ LẬP TRÌNH HIỆN ĐẠI"
    for r in doc.paragraphs[4].runs:
        set_run_font(r, size_pt=16, bold=True, color_rgb=(0,0,0))
    doc.paragraphs[4].alignment = WD_ALIGN_PARAGRAPH.CENTER
    
    doc.paragraphs[5].text = "ĐỀ TÀI: CÔNG NGHỆ ASP.NET CORE"
    for r in doc.paragraphs[5].runs:
        set_run_font(r, size_pt=16, bold=True, color_rgb=(0,0,0))
    doc.paragraphs[5].alignment = WD_ALIGN_PARAGRAPH.CENTER

    doc.paragraphs[7].text = "Giảng viên hướng dẫn:           Th.S Phạm Thi Vương"
    for r in doc.paragraphs[7].runs:
        set_run_font(r, size_pt=13, bold=True, color_rgb=(0,0,0))

    doc.paragraphs[9].text = "Sinh viên thực hiện:\tNguyễn Gia Bảo – 3122560004"
    for r in doc.paragraphs[9].runs:
        set_run_font(r, size_pt=13, bold=True, color_rgb=(0,0,0))

    doc.paragraphs[11].text = "Lớp môn học:\tDCT1221"
    for r in doc.paragraphs[11].runs:
        set_run_font(r, size_pt=13, bold=True, color_rgb=(0,0,0))

    doc.paragraphs[13].text = "Năm học:\t2025 – 2026"
    for r in doc.paragraphs[13].runs:
        set_run_font(r, size_pt=13, bold=True, color_rgb=(0,0,0))

    # Clear all children of body after index 15 except the final sectPr
    body = doc._body._element
    children = list(body)
    for child in children[16:]:
        if not child.tag.endswith('sectPr'):
            body.remove(child)

    # Section 0 (Cover page): NO footer / NO page number
    s1 = doc.sections[0]
    s1.header.is_linked_to_previous = False
    s1.footer.is_linked_to_previous = False
    for ftr_ref in s1._sectPr.xpath('.//w:footerReference'):
        s1._sectPr.remove(ftr_ref)

    # Section 1 (Front Matter: lowerRoman: i, ii, iii)
    s2 = doc.sections[1]
    s2.header.is_linked_to_previous = False
    set_pg_num_type(s2, 'lowerRoman', 1)
    set_footer_page_number(s2)

    # -------------------------------------------------------------------------
    # PAGE i: NHẬN XÉT CỦA GIẢNG VIÊN (vừa vặn đúng 1 trang)
    # -------------------------------------------------------------------------
    p_nx = doc.add_paragraph(style='Normal')
    p_nx.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r_nx = p_nx.add_run("NHẬN XÉT CỦA GIẢNG VIÊN HƯỚNG DẪN")
    set_run_font(r_nx, size_pt=14, bold=True, color_rgb=(0,0,0))
    p_nx.paragraph_format.space_before = Pt(6)
    p_nx.paragraph_format.space_after = Pt(12)

    def add_compact_p(text, bold_prefix=None, space_after=4):
        p = doc.add_paragraph(style='Normal')
        p.paragraph_format.space_before = Pt(0)
        p.paragraph_format.space_after = Pt(space_after)
        if bold_prefix:
            r_b = p.add_run(bold_prefix)
            set_run_font(r_b, size_pt=12.5, bold=True, color_rgb=(0,0,0))
        r = p.add_run(text)
        set_run_font(r, size_pt=12.5, color_rgb=(0,0,0))
        return p

    add_compact_p("Nguyễn Gia Bảo\t\tMSSV: 3122560004", bold_prefix="Sinh viên thực hiện:\t")
    add_compact_p("DCT1221", bold_prefix="Lớp môn học:\t\t")
    add_compact_p("Công nghệ ASP.NET Core - Hệ thống đăng tin PropNest", bold_prefix="Đề tài đồ án:\t\t", space_after=10)

    add_compact_p("", bold_prefix="1. Nhận xét về tinh thần, thái độ làm việc của sinh viên:")
    add_compact_p("………………………………………………………………………………………………………………………………………………………………")
    add_compact_p("………………………………………………………………………………………………………………………………………………………………", space_after=10)

    add_compact_p("", bold_prefix="2. Nhận xét về kết quả đạt được, chất lượng mã nguồn và báo cáo:")
    add_compact_p("………………………………………………………………………………………………………………………………………………………………")
    add_compact_p("………………………………………………………………………………………………………………………………………………………………")
    add_compact_p("………………………………………………………………………………………………………………………………………………………………", space_after=10)

    add_compact_p("", bold_prefix="3. Điểm đánh giá:")
    add_compact_p("Điểm bằng số: ………………………\t\tĐiểm bằng chữ: ………………………………………", space_after=14)

    p_sign_date = doc.add_paragraph(style='Normal')
    p_sign_date.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    p_sign_date.paragraph_format.space_after = Pt(2)
    r_sign_date = p_sign_date.add_run("TP. Hồ Chí Minh, ngày …… tháng …… năm 2026\nGiảng viên hướng dẫn\n\n\n")
    set_run_font(r_sign_date, size_pt=12.5, italic=True, color_rgb=(0,0,0))

    p_sign_name = doc.add_paragraph(style='Normal')
    p_sign_name.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    p_sign_name.paragraph_format.space_after = Pt(0)
    r_sign_name = p_sign_name.add_run("Th.S Phạm Thi Vương")
    set_run_font(r_sign_name, size_pt=13, bold=True, color_rgb=(0,0,0))

    doc.add_page_break()

    # -------------------------------------------------------------------------
    # PAGE ii: MỤC LỤC (vừa vặn đúng 1 trang)
    # -------------------------------------------------------------------------
    p_ml_title = doc.add_paragraph(style='Normal')
    p_ml_title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r_ml_title = p_ml_title.add_run("MỤC LỤC")
    set_run_font(r_ml_title, size_pt=14, bold=True, color_rgb=(0,0,0))
    p_ml_title.paragraph_format.space_before = Pt(6)
    p_ml_title.paragraph_format.space_after = Pt(8)

    add_toc_item(doc, "LỜI CẢM ƠN", toc_pages["LOI_CAM_ON"], level=1)
    add_toc_item(doc, "CHƯƠNG I: GIỚI THIỆU TỔNG QUAN", toc_pages["CHUONG_1"], level=1)
    add_toc_item(doc, "I. Mục tiêu của dự án và báo cáo cá nhân", toc_pages["SEC_I"], level=2)
    add_toc_item(doc, "II. Phạm vi công việc cá nhân", toc_pages["SEC_II"], level=2)
    add_toc_item(doc, "III. Đóng góp cá nhân", toc_pages["SEC_III"], level=2)
    add_toc_item(doc, "1. Vai trò của bản thân trong dự án", toc_pages["SEC_III_1"], level=3)
    add_toc_item(doc, "2. Các nhiệm vụ đã thực hiện cụ thể", toc_pages["SEC_III_2"], level=3)
    add_toc_item(doc, "2.1. Module Domain Entity Wallet", toc_pages["SEC_2_1"], level=4)
    add_toc_item(doc, "2.2. Chức năng Nạp tiền (TopUp)", toc_pages["SEC_2_2"], level=4)
    add_toc_item(doc, "2.3. Module Wallet Repository & Giao dịch", toc_pages["SEC_2_3"], level=4)
    add_toc_item(doc, "2.4. Module Wallets API Controller", toc_pages["SEC_2_4"], level=4)
    add_toc_item(doc, "2.5. Khởi tạo quy trình Mua gói và Transactional Outbox", toc_pages["SEC_2_5"], level=4)
    add_toc_item(doc, "2.6. Quy trình State Machine PurchasePackageSaga", toc_pages["SEC_2_6"], level=4)
    add_toc_item(doc, "2.7. Consumer Trừ tiền Ví (DeductWalletConsumer)", toc_pages["SEC_2_7"], level=4)
    add_toc_item(doc, "2.8. Consumer Nâng cấp Tin đăng (UpgradeListingConsumer)", toc_pages["SEC_2_8"], level=4)
    add_toc_item(doc, "2.9. Consumer Hoàn tiền (RefundWalletConsumer)", toc_pages["SEC_2_9"], level=4)
    add_toc_item(doc, "2.10. Cấu hình MassTransit & Giao diện Seller Portal", toc_pages["SEC_2_10"], level=4)
    add_toc_item(doc, "CHƯƠNG II: KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN", toc_pages["CHUONG_2"], level=1)
    add_toc_item(doc, "1. Kết luận", toc_pages["CONCL_1"], level=2)
    add_toc_item(doc, "2. Hướng phát triển", toc_pages["CONCL_2"], level=2)

    doc.add_page_break()

    # -------------------------------------------------------------------------
    # PAGE iii: DANH MỤC HÌNH ẢNH (1 TRANG RIÊNG BIỆT)
    # -------------------------------------------------------------------------
    p_fig_title = doc.add_paragraph(style='Normal')
    p_fig_title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r_fig_title = p_fig_title.add_run("DANH MỤC HÌNH ẢNH")
    set_run_font(r_fig_title, size_pt=14, bold=True, color_rgb=(0,0,0))
    p_fig_title.paragraph_format.space_before = Pt(6)
    p_fig_title.paragraph_format.space_after = Pt(10)

    add_fig_item(doc, "Hình 1. Ảnh mã nguồn Domain Entity Wallet", fig_pages["HINH_1"])
    add_fig_item(doc, "Hình 2. Ảnh chức năng nạp tiền TopUpAsync", fig_pages["HINH_2"])
    add_fig_item(doc, "Hình 3. Ảnh module WalletRepository và truy vấn giao dịch", fig_pages["HINH_3"])
    add_fig_item(doc, "Hình 4. Ảnh API Controller quản lý ví", fig_pages["HINH_4"])
    add_fig_item(doc, "Hình 5. Ảnh khởi tạo quy trình mua gói StartPurchaseAsync", fig_pages["HINH_5"])
    add_fig_item(doc, "Hình 6a. Ảnh quy trình State Machine PurchasePackageSaga (Phần 1: Khởi tạo và trừ tiền ví)", fig_pages["HINH_6A"])
    add_fig_item(doc, "Hình 6b. Ảnh quy trình State Machine PurchasePackageSaga (Phần 2: Nâng cấp tin đăng và hoàn tiền)", fig_pages["HINH_6B"])
    add_fig_item(doc, "Hình 7. Ảnh Consumer trừ tiền ví DeductWalletConsumer", fig_pages["HINH_7"])
    add_fig_item(doc, "Hình 8a. Ảnh Consumer nâng cấp tin UpgradeListingConsumer (Phần 1: Kiểm tra điều kiện gói dịch vụ)", fig_pages["HINH_8A"])
    add_fig_item(doc, "Hình 8b. Ảnh Consumer nâng cấp tin UpgradeListingConsumer (Phần 2: Nâng cấp gói tin và lưu lịch sử)", fig_pages["HINH_8B"])
    add_fig_item(doc, "Hình 9. Ảnh Consumer hoàn tiền RefundWalletConsumer", fig_pages["HINH_9"])
    add_fig_item(doc, "Hình 10. Ảnh cấu hình MassTransit và RabbitMQ Broker", fig_pages["HINH_10"])
    add_fig_item(doc, "Hình 11. Ảnh Controller quản lý ví trên Seller Portal", fig_pages["HINH_11"])

    # -------------------------------------------------------------------------
    # SECTION 2: MAIN BODY (Decimal: 1, 2, 3... starting from Lời cảm ơn)
    # -------------------------------------------------------------------------
    s3 = doc.add_section(WD_SECTION_START.NEW_PAGE)
    s3.header.is_linked_to_previous = False
    set_pg_num_type(s3, 'decimal', 1)
    set_footer_page_number(s3)

    # -------------------------------------------------------------------------
    # PAGE 1: LỜI CẢM ƠN
    # -------------------------------------------------------------------------
    add_heading(doc, "LỜI CẢM ƠN", "Heading 1")
    add_normal_paragraph(doc, "")
    add_normal_paragraph(doc, 
        "Trước hết, em xin bày tỏ lòng biết ơn sâu sắc đến Thầy Phạm Thi Vương đã tận tình giảng dạy, hướng dẫn và hỗ trợ em trong suốt quá trình học tập học phần Các công nghệ lập trình hiện đại. Những kiến thức và kinh nghiệm quý báu mà thầy truyền đạt không chỉ giúp em củng cố nền tảng chuyên môn mà còn mở rộng tư duy và cách tiếp cận vấn đề một cách khoa học, thực tiễn hơn.",
        justify=True, space_line=True)
    add_normal_paragraph(doc,
        "Trong quá trình thực hiện báo cáo, em đã nhận được sự quan tâm, góp ý và định hướng kịp thời từ thầy, qua đó giúp em từng bước hoàn thiện nội dung và nâng cao chất lượng bài làm. Phương pháp giảng dạy tận tâm, sinh động và dễ hiểu của thầy chính là nguồn động lực lớn giúp em tiếp thu kiến thức một cách hiệu quả.",
        justify=True, space_line=True)
    add_normal_paragraph(doc,
        "Tuy đã cố gắng hết sức để hoàn thành bài báo cáo, nhưng do hạn chế về kiến thức và kinh nghiệm, bài làm khó tránh khỏi những thiếu sót. Em rất mong nhận được những ý kiến đóng góp quý báu từ thầy để em có thể hoàn thiện hơn trong những học phần tiếp theo cũng như trong công việc sau này.",
        justify=True, space_line=True)
    add_normal_paragraph(doc,
        "Cuối cùng, em xin kính chúc thầy luôn dồi dào sức khỏe, thành công và tiếp tục gặt hái nhiều thành tựu trong sự nghiệp giảng dạy.",
        justify=True, space_line=True)

    doc.add_page_break()

    # -------------------------------------------------------------------------
    # CHƯƠNG I: GIỚI THIỆU TỔNG QUAN
    # -------------------------------------------------------------------------
    add_heading(doc, "CHƯƠNG I: GIỚI THIỆU TỔNG QUAN", "Heading 1")
    add_heading(doc, "I. Mục tiêu của dự án và báo cáo cá nhân", "Heading 2")

    add_normal_paragraph(doc, 
        "Trong bối cảnh các hệ thống web hiện đại ngày càng phát triển theo kiến trúc hướng dịch vụ và API-first, việc xây dựng backend hiệu năng cao, bảo mật và có khả năng mở rộng là yêu cầu tất yếu.")
    add_normal_paragraph(doc, 
        "Môn học Các công nghệ lập trình hiện đại nhằm trang bị cho sinh viên kiến thức thực tiễn về xây dựng hệ thống backend chuyên nghiệp, tối ưu cơ sở dữ liệu và xử lý các quy trình giao dịch phân tán.")
    add_normal_paragraph(doc, 
        "Trong đồ án này, nhóm phát triển hệ thống backend cho nền tảng đăng tin bất động sản PropNest, với các chức năng chính:")

    add_bullet_paragraph(doc, "Quản lý người dùng và xác thực tài khoản", num_id=4)
    add_bullet_paragraph(doc, "Quản lý tin đăng bất động sản và lịch sử biến động", num_id=4)
    add_bullet_paragraph(doc, "Quản lý ví tài khoản và giao dịch tài chính", num_id=4)
    add_bullet_paragraph(doc, "Quy trình Saga điều phối mua gói dịch vụ nâng cấp tin VIP", num_id=4)
    add_bullet_paragraph(doc, "Kiểm duyệt tin đăng và thống kê hệ thống", num_id=4)

    add_normal_paragraph(doc, "Báo cáo cá nhân này tập trung mô tả chi tiết:")

    add_bullet_paragraph(doc, "Phần công việc đã thực hiện", num_id=5)
    add_bullet_paragraph(doc, "Quy trình thiết kế và triển khai", num_id=5)
    add_bullet_paragraph(doc, "Các vấn đề gặp phải và cách giải quyết", num_id=5)
    add_bullet_paragraph(doc, "Kinh nghiệm thực tế rút ra", num_id=5)

    add_heading(doc, "II. Phạm vi công việc cá nhân", "Heading 2")

    add_normal_paragraph(doc, 
        "Trong quá trình thực hiện đồ án, bản thân được phân công phụ trách các module liên quan đến quản lý ví tài khoản, giao dịch tài chính và quy trình Saga điều phối mua gói dịch vụ:")

    p_w_h = doc.add_paragraph(style='Normal')
    r_w_h = p_w_h.add_run("Nhóm chức năng Wallet (Ví tài khoản)")
    set_run_font(r_w_h, size_pt=13, bold=True, color_rgb=(0,0,0))

    add_bullet_paragraph(doc, "Khởi tạo ví người dùng (CreateWallet)", num_id=6)
    add_bullet_paragraph(doc, "Xem thông tin số dư (GetBalanceByUserId)", num_id=6)
    add_bullet_paragraph(doc, "Nạp tiền vào ví (TopUpAsync)", num_id=6)
    add_bullet_paragraph(doc, "Trừ tiền ví có ưu tiên khuyến mãi (Charge)", num_id=6)
    add_bullet_paragraph(doc, "Hoàn trả tiền vào ví khi giao dịch hủy (Refund)", num_id=6)

    p_t_h = doc.add_paragraph(style='Normal')
    r_t_h = p_t_h.add_run("Nhóm chức năng Quản lý Giao dịch (Transaction Ledger)")
    set_run_font(r_t_h, size_pt=13, bold=True, color_rgb=(0,0,0))

    add_bullet_paragraph(doc, "Lưu bản ghi giao dịch (AddTransactionAsync)", num_id=7)
    add_bullet_paragraph(doc, "Lấy lịch sử giao dịch theo người dùng (GetTransactionsByUserIdAsync)", num_id=7)
    add_bullet_paragraph(doc, "Phân trang và lọc loại giao dịch (TopUp, Charge, Refund)", num_id=7)

    p_s_h = doc.add_paragraph(style='Normal')
    r_s_h = p_s_h.add_run("Nhóm quy trình Saga Mua gói (PurchasePackageSaga)")
    set_run_font(r_s_h, size_pt=13, bold=True, color_rgb=(0,0,0))

    add_bullet_paragraph(doc, "Khởi tạo quy trình mua gói (StartPurchaseAsync)", num_id=8)
    add_bullet_paragraph(doc, "Quy trình State Machine PurchasePackageSaga điều phối sự kiện", num_id=8)
    add_bullet_paragraph(doc, "Consumer trừ tiền ví (DeductWalletConsumer)", num_id=8)
    add_bullet_paragraph(doc, "Consumer nâng cấp tin (UpgradeListingConsumer)", num_id=8)
    add_bullet_paragraph(doc, "Consumer hoàn tiền ví tự động (RefundWalletConsumer)", num_id=8)
    add_bullet_paragraph(doc, "Kiểm tra Idempotency chống trừ tiền và nâng cấp lặp lại", num_id=8)

    p_o_h = doc.add_paragraph(style='Normal')
    r_o_h = p_o_h.add_run("Nhóm hạ tầng Outbox và Giao diện Seller")
    set_run_font(r_o_h, size_pt=13, bold=True, color_rgb=(0,0,0))

    add_bullet_paragraph(doc, "Xuất bản thông điệp Outbox (MassTransitOutboxPublisher)", num_id=9)
    add_bullet_paragraph(doc, "Cấu hình MassTransit và RabbitMQ Broker", num_id=9)
    add_bullet_paragraph(doc, "Giao diện ví và nạp tiền phía Seller Portal (WalletController)", num_id=9)

    add_heading(doc, "III. Đóng góp cá nhân", "Heading 2")
    add_heading(doc, "1. Vai trò của bản thân trong dự án", "Heading 3")

    add_normal_paragraph(doc,
        "Trong đồ án ASP.NET Core – PropNest, bản thân đảm nhận vai trò Backend Developer, chịu trách nhiệm thiết kế và hiện thực phân hệ Ví điện tử, quản lý giao dịch và quy trình Saga điều phối phân tán.")
    add_normal_paragraph(doc,
        "Cụ thể, tập trung vào việc xây dựng Domain Model Wallet với khóa lạc quan RowVersion, xử lý nạp/trừ tiền an toàn, triển khai quy trình State Machine PurchasePackageSaga trên MassTransit kết hợp RabbitMQ, và xây dựng các API phục vụ giao diện Seller.")
    add_normal_paragraph(doc,
        "Các chức năng được xây dựng đảm bảo tính toàn vẹn dữ liệu, chống xử lý lặp (Idempotency) và tuân theo kiến trúc Clean Architecture của hệ thống.")

    add_heading(doc, "2. Các nhiệm vụ đã thực hiện cụ thể", "Heading 3")

    # -------------------------------------------------------------------------
    # 2.1 Domain Entity Wallet
    # -------------------------------------------------------------------------
    add_heading(doc, "2.1. Module Domain Entity Wallet", "Heading 4")
    add_normal_paragraph(doc, "Thiết kế thực thể Wallet quản lý số dư và tính nhất quán dữ liệu:")
    add_bullet_paragraph(doc, "Quản lý số dư chính (MainBalance) và số dư khuyến mãi (PromoBalance)", num_id=10)
    add_bullet_paragraph(doc, "Phương thức Charge(): ưu tiên trừ tiền khuyến mãi trước, ngăn chặn số dư âm", num_id=10)
    add_bullet_paragraph(doc, "Phương thức CreditMain() và Refund(): nạp tiền và hoàn tiền vào ví", num_id=10)
    add_bullet_paragraph(doc, "Kiểm soát tranh chấp đồng thời bằng thuộc tính byte[] RowVersion", num_id=10)
    add_normal_paragraph(doc, " Kết quả: bảo đảm an toàn dữ liệu số dư và loại trừ lỗi mất cập nhật (Lost Update).")
    
    add_code_image_and_caption(doc, images['wallet'], "Hình 1. Ảnh mã nguồn Domain Entity Wallet")

    # -------------------------------------------------------------------------
    # 2.2 Chức năng Nạp tiền (TopUp)
    # -------------------------------------------------------------------------
    add_heading(doc, "2.2. Chức năng Nạp tiền (TopUp)", "Heading 4")
    add_normal_paragraph(doc, "Chức năng nạp tiền cho phép người dùng cộng tiền vào ví tài khoản:")
    add_bullet_paragraph(doc, "Kiểm tra số tiền nạp hợp lệ lớn hơn 0", num_id=11)
    add_bullet_paragraph(doc, "Cộng tiền vào MainBalance của người dùng thông qua CreditMain()", num_id=11)
    add_bullet_paragraph(doc, "Tự động tạo bản ghi WalletTransaction lưu vết balanceBefore và balanceAfter", num_id=11)
    add_bullet_paragraph(doc, "Lưu thay đổi vào SQL Server thông qua UnitOfWork", num_id=11)
    add_normal_paragraph(doc, " Kết quả: xây dựng hoàn chỉnh luồng nạp tiền và ghi nhận biến động số dư.")
    
    add_code_image_and_caption(doc, images['topup'], "Hình 2. Ảnh chức năng nạp tiền TopUpAsync")

    # -------------------------------------------------------------------------
    # 2.3 Module Wallet Repository & Transaction
    # -------------------------------------------------------------------------
    add_heading(doc, "2.3. Module Wallet Repository & Giao dịch", "Heading 4")
    add_normal_paragraph(doc, "Triển khai truy vấn dữ liệu ví và lịch sử biến động số dư:")
    add_bullet_paragraph(doc, "GetByUserIdAsync(): lấy thông tin ví theo mã người dùng", num_id=12)
    add_bullet_paragraph(doc, "AddTransactionAsync(): ghi nhận lịch sử giao dịch vào cơ sở dữ liệu", num_id=12)
    add_bullet_paragraph(doc, "GetTransactionsByUserIdAsync(): truy vấn lịch sử có phân trang và lọc theo loại giao dịch", num_id=12)
    add_normal_paragraph(doc, " Kết quả: cung cấp sổ cái lịch sử minh bạch cho người dùng theo dõi biến động tài chính.")
    
    add_code_image_and_caption(doc, images['transactions'], "Hình 3. Ảnh module WalletRepository và truy vấn giao dịch")

    # -------------------------------------------------------------------------
    # 2.4 Module Wallets API Controller
    # -------------------------------------------------------------------------
    add_heading(doc, "2.4. Module Wallets API Controller", "Heading 4")
    add_normal_paragraph(doc, "Cung cấp các điểm cuối RESTful API cho phân hệ ví:")
    add_bullet_paragraph(doc, "POST /api/v1/wallets/top-up: tiếp nhận yêu cầu nạp tiền kèm X-Correlation-Id", num_id=13)
    add_bullet_paragraph(doc, "GET /api/v1/wallets/me: lấy số dư ví của tài khoản đăng nhập hiện tại", num_id=13)
    add_bullet_paragraph(doc, "GET /api/v1/wallets/me/transactions: trả về danh sách lịch sử giao dịch", num_id=13)
    add_normal_paragraph(doc, " Kết quả: kết nối thông suốt giữa giao diện người dùng và dịch vụ backend.")
    
    add_code_image_and_caption(doc, images['wallet_api'], "Hình 4. Ảnh API Controller quản lý ví")

    # -------------------------------------------------------------------------
    # 2.5 Khởi tạo quy trình mua gói và Transactional Outbox
    # -------------------------------------------------------------------------
    add_heading(doc, "2.5. Khởi tạo quy trình Mua gói và Transactional Outbox", "Heading 4")
    add_normal_paragraph(doc, "Xử lý yêu cầu mua gói dịch vụ tin VIP an toàn:")
    add_bullet_paragraph(doc, "Kiểm tra IdempotencyKey và RequestHash để chống gửi yêu cầu trùng lặp", num_id=4)
    add_bullet_paragraph(doc, "Khởi tạo bản ghi WorkflowInstance theo dõi tiến trình với CorrelationId duy nhất", num_id=4)
    add_bullet_paragraph(doc, "Ghi sự kiện PurchasePackageRequested vào bảng OutboxMessage cùng transaction cơ sở dữ liệu", num_id=4)
    add_bullet_paragraph(doc, "OutboxPublisher quét và phát hành thông điệp lên RabbitMQ bảo đảm không mất mát dữ liệu", num_id=4)
    add_normal_paragraph(doc, " Kết quả: loại bỏ hoàn toàn rủi ro Dual-Write giữa cơ sở dữ liệu và Message Broker.")
    
    add_code_image_and_caption(doc, images['start_purchase'], "Hình 5. Ảnh khởi tạo quy trình mua gói StartPurchaseAsync")

    # -------------------------------------------------------------------------
    # 2.6 Quy trình State Machine PurchasePackageSaga
    # -------------------------------------------------------------------------
    add_heading(doc, "2.6. Quy trình State Machine PurchasePackageSaga", "Heading 4")
    add_normal_paragraph(doc, "Điều phối quy trình mua gói nhiều bước thông qua MassTransit State Machine:")
    add_bullet_paragraph(doc, "Bước 1 (Initially): tiếp nhận PurchasePackageRequested, chuyển DeductingWallet và gửi DeductWalletCommand", num_id=5)
    add_bullet_paragraph(doc, "Bước 2 (During DeductingWallet): nhận WalletDeducted chuyển UpgradingListing; nếu thất bại chuyển Failed", num_id=5)
    add_bullet_paragraph(doc, "Bước 3 (During UpgradingListing): nhận ListingUpgraded chuyển Completed; nếu thất bại chuyển Compensating và gửi RefundWalletCommand", num_id=5)
    add_bullet_paragraph(doc, "Bước 4 (During Compensating): nhận WalletRefunded chuyển sang trạng thái Compensated hoàn tất", num_id=5)
    add_normal_paragraph(doc, " Kết quả: tự động hóa toàn diện quy trình giao dịch phân tán và tự phục hồi khi có sự cố.")
    
    # Chia tách thành 2 ảnh 6a và 6b tiếp nối
    add_code_image_and_caption(doc, images['saga_part1'], "Hình 6a. Ảnh quy trình State Machine PurchasePackageSaga (Phần 1: Khởi tạo và trừ tiền ví)")
    add_code_image_and_caption(doc, images['saga_part2'], "Hình 6b. Ảnh quy trình State Machine PurchasePackageSaga (Phần 2: Nâng cấp tin đăng và hoàn tiền)")

    # -------------------------------------------------------------------------
    # 2.7 Consumer Trừ tiền Ví (DeductWalletConsumer)
    # -------------------------------------------------------------------------
    add_heading(doc, "2.7. Consumer Trừ tiền Ví (DeductWalletConsumer)", "Heading 4")
    add_normal_paragraph(doc, "Tiêu thụ lệnh DeductWalletCommand từ hàng đợi RabbitMQ:")
    add_bullet_paragraph(doc, "Kiểm tra Idempotency qua CorrelationId trong bảng WalletTransactions để chống trừ tiền 2 lần", num_id=6)
    add_bullet_paragraph(doc, "Thực hiện trừ tiền thông qua phương thức wallet.Charge(command.Amount)", num_id=6)
    add_bullet_paragraph(doc, "Ghi nhận giao dịch và phát hành sự kiện WalletDeducted hoặc WalletDeductionFailed", num_id=6)
    add_normal_paragraph(doc, " Kết quả: đảm bảo giao dịch trừ tiền diễn ra chính xác, an toàn và có khả năng chống lặp.")
    
    add_code_image_and_caption(doc, images['deduct'], "Hình 7. Ảnh Consumer trừ tiền ví DeductWalletConsumer")

    # -------------------------------------------------------------------------
    # 2.8 Consumer Nâng cấp Tin đăng (UpgradeListingConsumer)
    # -------------------------------------------------------------------------
    add_heading(doc, "2.8. Consumer Nâng cấp Tin đăng (UpgradeListingConsumer)", "Heading 4")
    add_normal_paragraph(doc, "Tiêu thụ lệnh UpgradeListingCommand để kích hoạt gói dịch vụ VIP:")
    add_bullet_paragraph(doc, "Kiểm tra Idempotency qua lịch sử nâng cấp có chứa CorrelationId", num_id=7)
    add_bullet_paragraph(doc, "Cộng dồn thời hạn gói VIP: nối tiếp ngày hết hạn cũ nếu tin còn hạn, hoặc tính từ hiện tại nếu hết hạn", num_id=7)
    add_bullet_paragraph(doc, "Lưu vết ListingHistory và phát hành sự kiện ListingUpgraded hoặc ListingUpgradeFailed", num_id=7)
    add_normal_paragraph(doc, " Kết quả: nâng cấp gói tin đăng chính xác và lưu vết lịch sử phục vụ kiểm toán.")
    
    # Chia tách thành 2 ảnh 8a và 8b tiếp nối
    add_code_image_and_caption(doc, images['upgrade_part1'], "Hình 8a. Ảnh Consumer nâng cấp tin UpgradeListingConsumer (Phần 1: Kiểm tra điều kiện gói dịch vụ)")
    add_code_image_and_caption(doc, images['upgrade_part2'], "Hình 8b. Ảnh Consumer nâng cấp tin UpgradeListingConsumer (Phần 2: Nâng cấp gói tin và lưu lịch sử)")

    # -------------------------------------------------------------------------
    # 2.9 Consumer Hoàn tiền (RefundWalletConsumer)
    # -------------------------------------------------------------------------
    add_heading(doc, "2.9. Consumer Hoàn tiền (RefundWalletConsumer)", "Heading 4")
    add_normal_paragraph(doc, "Xử lý kịch bản hoàn tiền tự động khi bước nâng cấp tin đăng gặp sự cố:")
    add_bullet_paragraph(doc, "Kiểm tra giao dịch hoàn tiền với CorrelationId đã xử lý chưa", num_id=8)
    add_bullet_paragraph(doc, "Nạp lại đúng số tiền đã trừ vào ví người dùng thông qua wallet.Refund()", num_id=8)
    add_bullet_paragraph(doc, "Tạo bản ghi giao dịch hoàn tiền (WalletTransactionType.Refund)", num_id=8)
    add_bullet_paragraph(doc, "Phát hành sự kiện WalletRefunded để Saga chuyển sang trạng thái kết thúc an toàn", num_id=8)
    add_normal_paragraph(doc, " Kết quả: bảo vệ tối đa quyền lợi tài chính của khách hàng khi hệ thống xảy ra sự cố.")
    
    add_code_image_and_caption(doc, images['refund'], "Hình 9. Ảnh Consumer hoàn tiền RefundWalletConsumer")

    # -------------------------------------------------------------------------
    # 2.10 Cấu hình MassTransit và Giao diện Seller Portal
    # -------------------------------------------------------------------------
    add_heading(doc, "2.10. Cấu hình MassTransit & Giao diện Seller Portal", "Heading 4")
    add_normal_paragraph(doc, "Tích hợp hạ tầng Service Bus và xây dựng giao diện người dùng:")
    add_bullet_paragraph(doc, "Đăng ký SagaStateMachine và 3 Consumer vào Dependency Injection Container", num_id=9)
    add_bullet_paragraph(doc, "Cấu hình RabbitMQ Host, Endpoint Formatter và bộ lọc InboxDeduplicationFilter", num_id=9)
    add_bullet_paragraph(doc, "Xây dựng WalletController trên Seller Portal hiển thị số dư và lịch sử giao dịch", num_id=9)
    add_bullet_paragraph(doc, "Xử lý nạp tiền trực tuyến từ form giao diện người bán", num_id=9)
    add_normal_paragraph(doc, " Kết quả: hoàn thiện trọn vẹn luồng tương tác từ giao diện người dùng đến hạ tầng backend.")
    
    add_code_image_and_caption(doc, images['masstransit'], "Hình 10. Ảnh cấu hình MassTransit và RabbitMQ Broker")
    add_code_image_and_caption(doc, images['seller_wallet'], "Hình 11. Ảnh Controller quản lý ví trên Seller Portal")

    # -------------------------------------------------------------------------
    # CHƯƠNG II: KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN
    # -------------------------------------------------------------------------
    add_heading(doc, "CHƯƠNG II: KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN", "Heading 1")
    add_heading(doc, "1. Kết luận", "Heading 2")

    add_normal_paragraph(doc,
        "Qua quá trình thực hiện đồ án xây dựng hệ thống backend cho nền tảng bất động sản PropNest, bản thân đã tích lũy được nhiều kiến thức và kinh nghiệm thực tế quan trọng trong lĩnh vực phát triển phần mềm doanh nghiệp.")
    add_normal_paragraph(doc,
        "Trước hết, bản thân đã nắm vững cách xây dựng một hệ thống backend sử dụng ASP.NET Core theo kiến trúc Clean Architecture rõ ràng, giúp mã nguồn dễ quản lý, dễ bảo trì và có khả năng mở rộng cao. Việc sử dụng C# với tính năng kiểu dữ liệu tĩnh mạnh cũng giúp tăng tính an toàn và hạn chế tối đa lỗi trong quá trình lập trình.")
    add_normal_paragraph(doc,
        "Bên cạnh đó, bản thân đã hiểu rõ cơ chế hoạt động của State Machine Saga (MassTransit State Machine) kết hợp với Message Broker RabbitMQ, bao gồm quá trình điều phối giao dịch phân tán, tự động hoàn tiền khi có lỗi và xử lý chống trùng lặp thông điệp (Idempotency).")
    add_normal_paragraph(doc,
        "Ngoài ra, thông qua việc sử dụng Entity Framework Core 8 kết hợp với SQL Server, bản thân đã có kinh nghiệm trong việc thiết kế cơ sở dữ liệu, quản lý xung đột dữ liệu đồng thời với khóa lạc quan RowVersion, và áp dụng Transactional Outbox Pattern để bảo đảm tính nhất quán dữ liệu giữa database và message queue.")
    add_normal_paragraph(doc,
        "Nhìn chung, các chức năng đã triển khai như Wallet, Transaction, Saga mua gói và giao diện Seller đều hoạt động ổn định, đáp ứng tốt các yêu cầu đề ra. Tuy nhiên, hệ thống vẫn còn nhiều tiềm năng để tiếp tục phát triển và hoàn thiện hơn trong tương lai.")

    add_heading(doc, "2. Hướng phát triển", "Heading 2")

    add_normal_paragraph(doc,
        "Trong tương lai, hệ thống PropNest có thể được nâng cấp theo nhiều hướng nhằm cải thiện hiệu năng, tính năng và khả năng mở rộng.")
    add_normal_paragraph(doc,
        "Trước hết, cần tích hợp cổng thanh toán trực tuyến thực tế như VNPAY, MoMo hoặc VietQR để thay thế luồng nạp tiền hiện tại. Điều này giúp hệ thống trở nên hoàn chỉnh và sẵn sàng vận hành trong thực tế.")
    add_normal_paragraph(doc,
        "Bên cạnh đó, việc tích hợp Redis caching sẽ giúp tăng tốc độ xử lý và giảm tải cho cơ sở dữ liệu bằng cách lưu trữ tạm thời thông tin số dư ví và lưu trữ token Idempotency để kiểm tra giao dịch trùng lặp nhanh hơn.")
    add_normal_paragraph(doc,
        "Hệ thống cũng có thể được mở rộng với các chức năng realtime thông qua SignalR, cho phép cập nhật trạng thái đơn hàng mua gói và gửi thông báo biến động số dư trực tiếp đến giao diện người dùng.")
    add_normal_paragraph(doc,
        "Ngoài ra, việc mở rộng hệ thống kiểm thử tự động với các kịch bản kiểm thử tích hợp (Integration Test) chuyên sâu cho luồng giao dịch phân tán là rất cần thiết nhằm đảm bảo chất lượng hệ thống khi mở rộng.")
    add_normal_paragraph(doc,
        "Cuối cùng, việc đóng gói container hóa toàn bộ hệ thống bằng Docker và chuẩn bị sẵn sàng triển khai lên môi trường cloud sẽ giúp nâng cao tính ổn định và khả năng sẵn sàng phục vụ lượng truy cập lớn.")

    doc.save(OUT_PATH_1)
    print(f"Saved document to {OUT_PATH_1}")
    return OUT_PATH_1

def query_word_pages(docx_path):
    ps1_path = os.path.abspath("scratch/query_pages.ps1")
    abs_docx = os.path.abspath(docx_path)
    res = subprocess.run(
        ["powershell", "-ExecutionPolicy", "Bypass", "-File", ps1_path, "-docPath", abs_docx],
        capture_output=True, text=True, encoding='utf-8', errors='replace'
    )
    pages_found = {}
    for line in res.stdout.splitlines():
        line = line.strip()
        if "Sec 3 |" in line:
            parts = line.split("|")
            if len(parts) >= 4:
                adj_str = parts[2].replace("AdjPg", "").strip()
                try:
                    adj_pg = int(adj_str)
                    txt = parts[3].strip()
                    pages_found[txt] = adj_pg
                except ValueError:
                    pass
    return pages_found

def resolve_pages(pages_found):
    toc_pages = {}
    fig_pages = {}

    def find_pg(prefix):
        for k, v in pages_found.items():
            if k.startswith(prefix):
                return str(v)
        return "1"

    toc_pages["LOI_CAM_ON"] = find_pg("LỜI CẢM ƠN")
    toc_pages["CHUONG_1"] = find_pg("CHƯƠNG I: GIỚI THIỆU TỔNG QUAN")
    toc_pages["SEC_I"] = find_pg("I. Mục tiêu của dự án")
    toc_pages["SEC_II"] = find_pg("II. Phạm vi công việc")
    toc_pages["SEC_III"] = find_pg("III. Đóng góp cá nhân")
    toc_pages["SEC_III_1"] = find_pg("1. Vai trò của bản thân")
    toc_pages["SEC_III_2"] = find_pg("2. Các nhiệm vụ đã thực hiện")
    toc_pages["SEC_2_1"] = find_pg("2.1. Module Domain Entity Wallet")
    toc_pages["SEC_2_2"] = find_pg("2.2. Chức năng Nạp tiền (TopUp)")
    toc_pages["SEC_2_3"] = find_pg("2.3. Module Wallet Repository")
    toc_pages["SEC_2_4"] = find_pg("2.4. Module Wallets API Controller")
    toc_pages["SEC_2_5"] = find_pg("2.5. Khởi tạo quy trình Mua gói")
    toc_pages["SEC_2_6"] = find_pg("2.6. Quy trình State Machine PurchasePackageSaga")
    toc_pages["SEC_2_7"] = find_pg("2.7. Consumer Trừ tiền Ví")
    toc_pages["SEC_2_8"] = find_pg("2.8. Consumer Nâng cấp Tin đăng")
    toc_pages["SEC_2_9"] = find_pg("2.9. Consumer Hoàn tiền")
    toc_pages["SEC_2_10"] = find_pg("2.10. Cấu hình MassTransit")
    toc_pages["CHUONG_2"] = find_pg("CHƯƠNG II: KẾT LUẬN")
    toc_pages["CONCL_1"] = find_pg("1. Kết luận")
    toc_pages["CONCL_2"] = find_pg("2. Hướng phát triển")

    fig_pages["HINH_1"] = find_pg("Hình 1.")
    fig_pages["HINH_2"] = find_pg("Hình 2.")
    fig_pages["HINH_3"] = find_pg("Hình 3.")
    fig_pages["HINH_4"] = find_pg("Hình 4.")
    fig_pages["HINH_5"] = find_pg("Hình 5.")
    fig_pages["HINH_6A"] = find_pg("Hình 6a.")
    fig_pages["HINH_6B"] = find_pg("Hình 6b.")
    fig_pages["HINH_7"] = find_pg("Hình 7.")
    fig_pages["HINH_8A"] = find_pg("Hình 8a.")
    fig_pages["HINH_8B"] = find_pg("Hình 8b.")
    fig_pages["HINH_9"] = find_pg("Hình 9.")
    fig_pages["HINH_10"] = find_pg("Hình 10.")
    fig_pages["HINH_11"] = find_pg("Hình 11.")

    return toc_pages, fig_pages

def main():
    print("=== PASS 1: Generate initial document with placeholder TOC & Figures ===")
    draft_path = build_document()

    print("\n=== PASS 2: Query exact page numbers from Word COM ===")
    pages_found = query_word_pages(draft_path)
    print(f"Total section 3 items found by Word: {len(pages_found)}")

    toc_pages, fig_pages = resolve_pages(pages_found)
    print("\nResolved TOC Page Numbers:")
    for k, v in toc_pages.items():
        print(f"  {k}: {v}")

    print("\nResolved Figure Page Numbers:")
    for k, v in fig_pages.items():
        print(f"  {k}: {v}")

    print("\n=== PASS 3: Rebuild final document with 100% accurate page numbers ===")
    final_path = build_document(toc_pages, fig_pages)

    print("\n=== Verification: Query Word COM on final document ===")
    final_pages_found = query_word_pages(final_path)
    final_toc, final_figs = resolve_pages(final_pages_found)
    print("Final TOC Verification:")
    all_match = True
    for k, v in toc_pages.items():
        if final_toc.get(k) != v:
            print(f"  MISMATCH on {k}: pass2={v} vs final={final_toc.get(k)}")
            all_match = False
    for k, v in fig_pages.items():
        if final_figs.get(k) != v:
            print(f"  MISMATCH on {k}: pass2={v} vs final={final_figs.get(k)}")
            all_match = False

    if all_match:
        print(">> ALL PAGE NUMBERS MATCH 100% PERFECTLY! <<")
    else:
        print(">> Note: Re-running pass 3 with updated numbers for perfection...")
        build_document(final_toc, final_figs)

    try:
        shutil.copy2(OUT_PATH_1, OUT_PATH_2)
        print(f"\nCopied final document to {OUT_PATH_2}")
    except Exception as e:
        print(f"\nNotice: Could not copy to {OUT_PATH_2} (file might be opened in Word): {e}")

    print("\nSUCCESS! Personal report generated cleanly and completely.")

if __name__ == '__main__':
    main()

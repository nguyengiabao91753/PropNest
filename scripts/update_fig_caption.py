with open('scripts/generate_chuong_4_supplements.py', 'r', encoding='utf-8') as f:
    text = f.read()

old_needle = 'add_h3("4.2.3. Luồng xử lý một HTTP Request hoàn chỉnh (End-to-End Request Lifecycle Flow)")'
new_replacement = '''add_h3("4.2.3. Luồng xử lý một HTTP Request hoàn chỉnh (End-to-End Request Lifecycle Flow)")
    p_fig = doc.add_paragraph()
    p_fig.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_fig.paragraph_format.space_before = Pt(8)
    p_fig.paragraph_format.space_after = Pt(2)
    r_fig = p_fig.add_run("Hình 4.3: Sơ đồ luồng xử lý một HTTP Request qua các tầng kiến trúc trong ASP.NET Core 8")
    r_fig.font.name = 'Times New Roman'
    r_fig.font.size = Pt(11)
    r_fig.bold = True
    r_fig.italic = True
    
    p_path = doc.add_paragraph()
    p_path.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p_path.paragraph_format.space_before = Pt(0)
    p_path.paragraph_format.space_after = Pt(6)
    r_path = p_path.add_run("(Trực quan hóa chi tiết tại tệp: docs/diagrams/svg/hinh_4_3_luong_xu_ly_http_request.svg)")
    r_path.font.name = 'Times New Roman'
    r_path.font.size = Pt(10)
    r_path.italic = True'''

if old_needle in text:
    text = text.replace(old_needle, new_replacement, 1)
    with open('scripts/generate_chuong_4_supplements.py', 'w', encoding='utf-8') as f:
        f.write(text)
    print('Updated generate_chuong_4_supplements.py')

# Cập nhật merge_chuong_4.py
with open('scripts/merge_chuong_4.py', 'r', encoding='utf-8') as f:
    text_m = f.read()

if old_needle in text_m:
    text_m = text_m.replace(old_needle, new_replacement, 1)
    with open('scripts/merge_chuong_4.py', 'w', encoding='utf-8') as f:
        f.write(text_m)
    print('Updated merge_chuong_4.py')

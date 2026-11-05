import docx
from docx.shared import Pt, Inches, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.section import WD_SECTION_START

doc = docx.Document('docs/Báo_cáo_cá_nhân_sample.docx')

# Remove all body children after index 15 except the final sectPr
body = doc._body._element
children = list(body)
for child in children[16:]:
    if not child.tag.endswith('sectPr'):
        body.remove(child)

# S1: Cover (Index 0)
s1 = doc.sections[0]
s1.header.is_linked_to_previous = False
s1.footer.is_linked_to_previous = False
for ftr_ref in s1._sectPr.xpath('.//w:footerReference'):
    s1._sectPr.remove(ftr_ref)

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

# S2: Front Matter (lowerRoman: i, ii, iii)
s2 = doc.sections[1]
s2.header.is_linked_to_previous = False
set_pg_num_type(s2, 'lowerRoman', 1)
set_footer_page_number(s2)

doc.add_paragraph("NHẬN XÉT CỦA GIẢNG VIÊN")
doc.add_page_break()
doc.add_paragraph("MỤC LỤC")
doc.add_page_break()
doc.add_paragraph("DANH MỤC HÌNH ẢNH")

# S3: Main Body (decimal: 1, 2, 3...)
s3 = doc.add_section(WD_SECTION_START.NEW_PAGE)
s3.header.is_linked_to_previous = False
set_pg_num_type(s3, 'decimal', 1)
set_footer_page_number(s3)

doc.add_paragraph("LỜI CẢM ƠN (Page 1)")
doc.add_page_break()
doc.add_paragraph("CHƯƠNG I: GIỚI THIỆU TỔNG QUAN (Page 2)")
doc.add_page_break()
doc.add_paragraph("CHƯƠNG II: KẾT LUẬN (Page 3)")

doc.save("scratch/test_three_sections.docx")
print("Saved scratch/test_three_sections.docx")

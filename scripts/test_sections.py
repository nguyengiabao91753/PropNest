import docx
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.section import WD_SECTION_START

doc = docx.Document()

# Section 1: Cover (no page number)
s1 = doc.sections[0]
s1.header.is_linked_to_previous = False
s1.footer.is_linked_to_previous = False
# Clear footer
for p in s1.footer.paragraphs:
    p.text = ""

p = doc.add_paragraph("TRANG BIA")

# Section 2: Front Matter (Roman numerals: i, ii, iii)
s2 = doc.add_section(WD_SECTION_START.NEW_PAGE)
s2.header.is_linked_to_previous = False
s2.footer.is_linked_to_previous = False

# Set pgNumType to lowerRoman starting at 1
sectPr2 = s2._sectPr
pgNumType2 = OxmlElement('w:pgNumType')
pgNumType2.set(qn('w:fmt'), 'lowerRoman')
pgNumType2.set(qn('w:start'), '1')
sectPr2.append(pgNumType2)

# Footer for s2
f2 = s2.footer.paragraphs[0]
f2.alignment = docx.enum.text.WD_ALIGN_PARAGRAPH.CENTER
r2 = f2.add_run()
fld1 = OxmlElement('w:fldSimple')
fld1.set(qn('w:instr'), 'PAGE')
f2._p.append(fld1)

doc.add_paragraph("NHAN XET CUA GIANG VIEN")
doc.add_page_break()
doc.add_paragraph("MUC LUC")
doc.add_page_break()
doc.add_paragraph("DANH MUC HINH ANH")

# Section 3: Body (Arabic numbers: 1, 2, 3...) starting from Lời cảm ơn
s3 = doc.add_section(WD_SECTION_START.NEW_PAGE)
s3.header.is_linked_to_previous = False
s3.footer.is_linked_to_previous = False

sectPr3 = s3._sectPr
pgNumType3 = OxmlElement('w:pgNumType')
pgNumType3.set(qn('w:fmt'), 'decimal')
pgNumType3.set(qn('w:start'), '1')
sectPr3.append(pgNumType3)

f3 = s3.footer.paragraphs[0]
f3.alignment = docx.enum.text.WD_ALIGN_PARAGRAPH.CENTER
fld2 = OxmlElement('w:fldSimple')
fld2.set(qn('w:instr'), 'PAGE')
f3._p.append(fld2)

doc.add_paragraph("LOI CAM ON (Trang 1)")
doc.add_page_break()
doc.add_paragraph("CHUONG I: GIOI THIEU TONG QUAN (Trang 2)")

doc.save("scratch/test_sections.docx")
print("Saved test_sections.docx successfully.")

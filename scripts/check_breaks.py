import docx
doc = docx.Document('docs/Báo_cáo_cá_nhân_sample.docx')
for i in [15, 20, 27, 29]:
    xml = doc.paragraphs[i]._p.xml
    is_sect = 'sectPr' in xml
    is_br = 'w:type="page"' in xml
    print(f"P{i}: sectPr={is_sect}, page_br={is_br}")

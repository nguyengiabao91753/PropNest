import docx
doc = docx.Document('docs/Báo_cáo_cá_nhân_sample.docx')
for i, p in enumerate(doc.paragraphs):
    for r in p.runs:
        if 'w:br' in r._r.xml and 'w:type="page"' in r._r.xml:
            print(f"Page break at P{i}: {repr(p.text[:30])}")

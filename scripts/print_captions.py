import docx, sys
sys.stdout.reconfigure(encoding="utf-8")
doc = docx.Document("docs/Báo_cáo_cá_nhân_sample.docx")
for i in [84, 94, 102, 108, 116, 126]:
    print(f"P{i}: {repr(doc.paragraphs[i].text)}")

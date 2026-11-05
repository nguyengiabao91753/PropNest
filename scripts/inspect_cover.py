import docx
import sys

sys.stdout.reconfigure(encoding='utf-8')

doc = docx.Document('docs/Báo cáo chung Các công nghệ LTHĐ.docx')
print("=== Section info ===")
for i, s in enumerate(doc.sections):
    print(f"Section {i}: start_type={s.start_type}, top={s.top_margin.cm}, bottom={s.bottom_margin.cm}, left={s.left_margin.cm}, right={s.right_margin.cm}")
    # check page borders in sectPr
    sectPr = s._sectPr
    pgBorders = sectPr.xpath('.//w:pgBorders')
    print(f"   pgBorders count: {len(pgBorders)}")
    if pgBorders:
        print(f"   pgBorders xml: {pgBorders[0].xml[:200]}")

print("\n=== Cover Page Paragraphs (0-7) ===")
for i in range(min(10, len(doc.paragraphs))):
    p = doc.paragraphs[i]
    print(f"P{i} [style={p.style.name}, align={p.alignment}]: {p.text.strip()[:100]}")
    for j, r in enumerate(p.runs):
        font_size = r.font.size.pt if r.font.size else None
        print(f"   r{j}: text='{r.text.strip()}' bold={r.bold} size={font_size} font={r.font.name}")

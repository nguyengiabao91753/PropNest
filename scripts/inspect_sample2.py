import docx, sys, re
sys.stdout.reconfigure(encoding='utf-8')
from docx.oxml.ns import qn
doc = docx.Document('docs/Báo_cáo_cá_nhân_sample.docx')
def clean(x):
    x = re.sub(r'\sxmlns:\w+="[^"]*"', '', x)
    x = re.sub(r'\sw:rsid\w*="[^"]*"', '', x)
    return re.sub(r'\n\s*', '', x)
for i, p in enumerate(doc.paragraphs):
    pPr = p._p.find(qn('w:pPr'))
    ppr = clean(pPr.xml) if pPr is not None else ''
    r0 = p.runs[0] if p.runs else None
    rpr = ''
    if r0 is not None and r0._r.find(qn('w:rPr')) is not None:
        rpr = clean(r0._r.find(qn('w:rPr')).xml)
    print(f'P{i} [{p.style.name}] {p.text[:40]!r}\n   pPr={ppr[:400]}\n   rPr={rpr[:200]}')

import docx, sys, re
sys.stdout.reconfigure(encoding='utf-8')
from docx.oxml.ns import qn
for path, n in [('docs/Báo_cáo_cá_nhân_sample.docx', 16), ('docs/Báo cáo chung Các công nghệ LTHĐ.docx', 7)]:
    doc = docx.Document(path)
    print('=====', path)
    for i, p in enumerate(doc.paragraphs[:n]):
        x = p._p.xml
        names = re.findall(r'<wp:docPr [^>]*name="([^"]*)"', x)
        anchors = len(re.findall(r'<wp:anchor', x)); inl = len(re.findall(r'<wp:inline', x))
        exts = re.findall(r'<wp:extent cx="(\d+)" cy="(\d+)"', x)
        emb = re.findall(r'r:embed="([^"]+)"', x)
        tgt = [doc.part.rels[e].target_ref for e in emb if e in doc.part.rels]
        if names or anchors or inl:
            print(i, repr(p.text[:30]), 'anchor', anchors, 'inline', inl, names, exts, tgt)
    # section info
    for s in doc.sections:
        print('section margins', s.left_margin.cm, s.right_margin.cm, s.top_margin.cm, s.bottom_margin.cm, 'page', s.page_width.cm, s.page_height.cm)
        print(' header paras:', [p.text for p in s.header.paragraphs], 'footer:', [p.text for p in s.footer.paragraphs], 'linked', s.header.is_linked_to_previous)
# numbering formats of sample
doc = docx.Document('docs/Báo_cáo_cá_nhân_sample.docx')
numxml = doc.part.numbering_part.element
for num in numxml.findall(qn('w:num')):
    nid = num.get(qn('w:numId')); aid = num.find(qn('w:abstractNumId')).get(qn('w:val'))
    for ab in numxml.findall(qn('w:abstractNum')):
        if ab.get(qn('w:abstractNumId')) == aid:
            lvl = ab.find(qn('w:lvl'))
            fmt = lvl.find(qn('w:numFmt')).get(qn('w:val'))
            txt = lvl.find(qn('w:lvlText')).get(qn('w:val'))
            ind = lvl.find(qn('w:pPr'))
            print('numId', nid, 'abs', aid, fmt, repr(txt), re.sub(r'\sxmlns:\w+="[^"]*"', '', ind.xml).replace('\n','') if ind is not None else '')
# styles
for sn in ['Normal', 'Heading 1', 'Heading 2', 'Heading 3', 'Heading 4']:
    st = doc.styles[sn]
    print(sn, re.sub(r'\sxmlns:\w+="[^"]*"', '', st.element.xml).replace('\n', '')[:700])
dd = doc.styles.element.find(qn('w:docDefaults'))
print('docDefaults', re.sub(r'\sxmlns:\w+="[^"]*"', '', dd.xml).replace('\n', ''))

import docx
import sys
import os

sys.stdout.reconfigure(encoding='utf-8')

sample_path = 'docs/Báo_cáo_cá_nhân_sample.docx'
doc = docx.Document(sample_path)

print(f"Total paragraphs: {len(doc.paragraphs)}")
for i, p in enumerate(doc.paragraphs):
    has_image = any('graphic' in r._element.xml for r in p.runs)
    if has_image or 'Hình' in p.text or p.text.startswith('CHƯƠNG') or p.text.startswith('I.') or p.text.startswith('II.') or p.text.startswith('III.'):
        print(f"P{i:3d} | Image={has_image} | {p.text}")

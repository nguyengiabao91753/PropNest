# Generator for FixReview.docx
import docx
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from scripts.build_fix_review_helpers import (
    set_cell_margins, set_cell_shading, set_cell_border,
    add_code_box, add_callout, add_styled_table, set_para_font
)

doc = docx.Document()
print('Initialized docx.')

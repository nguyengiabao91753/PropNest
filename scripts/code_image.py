# -*- coding: utf-8 -*-
"""Render C# code snippets into 'carbon-like' PNG images (same style as the sample report)."""
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from pygments.lexers import CSharpLexer
from pygments.token import Token

FONT_DIR = r"C:\Windows\Fonts"
FONT_SIZE = 30
LINE_H = 38
PAD_X = 34
PAD_TOP = 92
PAD_BOTTOM = 30
OUTER = 110

COLORS = {
    "default": (56, 58, 66),
    "keyword": (166, 38, 164),
    "type": (64, 120, 242),
    "func": (64, 120, 242),
    "string": (80, 161, 79),
    "number": (152, 104, 1),
    "comment": (160, 161, 167),
    "attr": (193, 132, 1),
    "name": (228, 86, 73),
}


def _color(ttype, value, next_value):
    if ttype in Token.Comment:
        return COLORS["comment"], True
    if ttype in Token.Keyword:
        return COLORS["keyword"], False
    if ttype in Token.Literal.String:
        return COLORS["string"], False
    if ttype in Token.Literal.Number:
        return COLORS["number"], False
    if ttype in Token.Name.Function:
        return COLORS["func"], False
    if ttype in Token.Name.Class or ttype in Token.Name.Namespace:
        return COLORS["type"], False
    if ttype in Token.Name.Attribute or ttype in Token.Name.Decorator:
        return COLORS["attr"], False
    if ttype in Token.Name:
        if value[:1].isupper():
            if next_value.startswith("("):
                return COLORS["func"], False
            return COLORS["type"], False
        if next_value.startswith("("):
            return COLORS["func"], False
        return COLORS["name"], False
    return COLORS["default"], False


def render_code(code: str, out_path: str):
    code = code.strip("\n")
    font = ImageFont.truetype(f"{FONT_DIR}\\consola.ttf", FONT_SIZE)
    font_i = ImageFont.truetype(f"{FONT_DIR}\\consolai.ttf", FONT_SIZE)
    char_w = font.getlength("M")

    lines = code.split("\n")
    max_len = max(len(l.expandtabs(4)) for l in lines)
    card_w = int(PAD_X * 2 + char_w * max(max_len, 50))
    card_h = PAD_TOP + LINE_H * len(lines) + PAD_BOTTOM
    W, H = card_w + OUTER * 2, card_h + OUTER * 2

    img = Image.new("RGB", (W, H), (255, 255, 255))
    shadow = Image.new("L", (W, H), 0)
    ImageDraw.Draw(shadow).rounded_rectangle(
        [OUTER, OUTER + 18, OUTER + card_w, OUTER + card_h + 18], radius=12, fill=110)
    shadow = shadow.filter(ImageFilter.GaussianBlur(34))
    img.paste((150, 150, 150), (0, 0), shadow)

    d = ImageDraw.Draw(img)
    d.rounded_rectangle([OUTER, OUTER, OUTER + card_w, OUTER + card_h], radius=12, fill=(250, 250, 250))
    for i, c in enumerate([(255, 95, 86), (255, 189, 46), (39, 201, 63)]):
        cx, cy = OUTER + 42 + i * 40, OUTER + 44
        d.ellipse([cx - 12, cy - 12, cx + 12, cy + 12], fill=c)

    tokens = list(CSharpLexer().get_tokens(code.expandtabs(4)))
    x0, y = OUTER + PAD_X, OUTER + PAD_TOP
    x = x0
    for idx, (ttype, value) in enumerate(tokens):
        nxt = ""
        for j in range(idx + 1, min(idx + 4, len(tokens))):
            if tokens[j][1].strip():
                nxt = tokens[j][1]
                break
        color, italic = _color(ttype, value, nxt)
        parts = value.split("\n")
        for k, part in enumerate(parts):
            if k > 0:
                y += LINE_H
                x = x0
            if part:
                f = font_i if italic else font
                d.text((x, y), part, font=f, fill=color)
                x += f.getlength(part)
    img.save(out_path)
    return out_path

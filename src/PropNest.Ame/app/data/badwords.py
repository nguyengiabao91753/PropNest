"""
Danh sách regex patterns phát hiện từ cấm nghiêm trọng.
Bao gồm: nội dung phản cảm, lừa đảo, vi phạm pháp luật.
"""

BADWORD_PATTERNS: list[str] = [
    # Lừa đảo / Scam
    r'\blừa\s*đảo\b',
    r'\bscam\b',
    r'\bchiếm\s*đoạt\b',

    # Nội dung bất hợp pháp
    r'\bma\s*túy\b',
    r'\bcần\s*sa\b',
    r'\bheroin\b',
    r'\bvũ\s*khí\b',
    r'\bsúng\b',

    # Nội dung người lớn / phản cảm
    r'\bkhiêu\s*dâm\b',
    r'\bmại\s*dâm\b',
    r'\bđánh\s*bạc\b',
    r'\bcá\s*độ\b',
    r'\bcờ\s*bạc\b',

    # Spam / quảng cáo trái phép
    r'\bcam\s*kết\s*lợi\s*nhuận\b',
    r'\bsiêu\s*lợi\s*nhuận\b',
    r'\blãi\s*suất\s*khủng\b',
    r'\bgiàu\s*nhanh\b',

    # Kích động
    r'\bkích\s*động\b',
    r'\bphản\s*động\b',
    r'\blật\s*đổ\b',
]
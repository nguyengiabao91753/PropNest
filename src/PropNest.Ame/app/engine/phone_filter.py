import re
from app.models.responses import RuleViolation


class PhoneFilter:
    # Pattern trên chuỗi đã loại bỏ dấu phân tách (chỉ còn digits)
    DIGIT_PATTERNS = [
        # SĐT VN 10 số: 0xx xxxxxxx
        r'0[1-9]\d{8}',
        # Có mã quốc gia: +84 hoặc 84
        r'(?:\+?84)[1-9]\d{8}',
    ]

    # Pattern trên text gốc (phát hiện dạng viết bằng chữ)
    TEXT_PATTERNS = [
        r'(?:không|kh[oô]ng)\s*(?:một|hai|ba|bốn|năm|sáu|bảy|tám|chín)(?:\s*(?:một|hai|ba|bốn|năm|sáu|bảy|tám|chín)){7,}',
    ]

    def __init__(self):
        self._digit_compiled = [
            re.compile(p, re.IGNORECASE) for p in self.DIGIT_PATTERNS
        ]
        self._text_compiled = [
            re.compile(p, re.IGNORECASE | re.UNICODE) for p in self.TEXT_PATTERNS
        ]

    def check(self, text: str) -> list[RuleViolation]:
        violations = []

        # Normalize: loại bỏ dấu phân tách phổ biến, thay O→0
        stripped = re.sub(r'[\s.\-\(\)]', '', text)
        stripped = stripped.replace('O', '0').replace('o', '0')

        # Check digit patterns trên chuỗi đã normalize
        for pattern in self._digit_compiled:
            if pattern.search(stripped):
                violations.append(RuleViolation(
                    rule="PHONE_EXPOSURE",
                    severity="critical",
                    message="Phát hiện số điện thoại trong nội dung tin đăng"
                ))
                return violations

        # Check text patterns trên text gốc
        for pattern in self._text_compiled:
            if pattern.search(text):
                violations.append(RuleViolation(
                    rule="PHONE_EXPOSURE",
                    severity="critical",
                    message="Phát hiện số điện thoại (dạng chữ) trong nội dung tin đăng"
                ))
                return violations

        return violations

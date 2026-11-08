import re
from app.models.responses import RuleViolation

class PhoneFilter:
    # Các pattern phát hiện SĐT Việt Nam trong mô tả
    PHONE_PATTERNS = [
        # Số VN chuẩn: 0xxx.xxx.xxxx hoặc biến thể
        r'0[1-9]\d[\s.\-]?\d{3}[\s.\-]?\d{4}',
        # Có mã quốc gia: +84, 84
        r'(?:\+?84)[\s.\-]?\d[\s.\-]?\d{3}[\s.\-]?\d{4}',
        # Dạng ẩn: không ba hai, không chín tám (viết bằng chữ)
        r'(?:không|kh[oô]ng)\s*(?:một|hai|ba|bốn|năm|sáu|bảy|tám|chín)(?:\s*(?:một|hai|ba|bốn|năm|sáu|bảy|tám|chín)){7,}',
        # Dạng thay thế ký tự: O9O8.123.456
        r'[O0][1-9][O0-9][\s.\-]?[O0-9]{3}[\s.\-]?[O0-9]{4}',
    ]

    def __init__(self):
        self._compiled = [
            re.compile(p, re.IGNORECASE | re.UNICODE) for p in self.PHONE_PATTERNS
        ]

    def check(self, text: str) -> list[RuleViolation]:
        violations = []
        for pattern in self._compiled:
            if pattern.search(text):
                violations.append(RuleViolation(
                    rule="PHONE_EXPOSURE",
                    severity="critical",
                    message="Phát hiện số điện thoại trong nội dung tin đăng"
                ))
                break  # 1 lần phát hiện là đủ
        return violations
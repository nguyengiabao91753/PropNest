import re
from app.models.responses import RuleViolation
from app.data.badwords import BADWORD_PATTERNS

class BadwordFilter:
    def __init__(self):
        # Compile tất cả patterns 1 lần khi khởi tạo
        self._compiled = [
            re.compile(pattern, re.IGNORECASE | re.UNICODE)
            for pattern in BADWORD_PATTERNS
        ]

    def check(self, text: str) -> list[RuleViolation]:
        violations = []
        normalized = text.lower().strip()
        for pattern in self._compiled:
            match = pattern.search(normalized)
            if match:
                violations.append(RuleViolation(
                    rule="BADWORD_DETECTED",
                    severity="critical",
                    message=f"Phát hiện nội dung vi phạm: '{match.group()}'"
                ))
        return violations
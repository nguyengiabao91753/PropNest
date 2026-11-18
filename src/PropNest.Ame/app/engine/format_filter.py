import re
from app.config import settings
from app.models.responses import RuleViolation

class FormatFilter:
    def check(self, title: str, description: str | None) -> list[RuleViolation]:
        violations = []
        text = f"{title} {description or ''}"

        # Rule 2.1: Chữ IN HOA quá 50%
        alpha_chars = [c for c in text if c.isalpha()]
        if alpha_chars:
            upper_ratio = sum(1 for c in alpha_chars if c.isupper()) / len(alpha_chars)
            if upper_ratio > settings.CAPS_THRESHOLD:
                violations.append(RuleViolation(
                    rule="EXCESSIVE_CAPS",
                    severity="warning",
                    message=f"Tỷ lệ chữ in hoa: {upper_ratio:.0%} (vượt ngưỡng {settings.CAPS_THRESHOLD:.0%})"
                ))

        # Rule 2.2: Lặp ký tự liên tiếp (vd: "đẹpppppp", "!!!!!!!")
        repeat_pattern = re.compile(r'(.)\1{' + str(settings.CHAR_REPEAT_LIMIT - 1) + r',}')
        matches = repeat_pattern.findall(text)
        if matches:
            violations.append(RuleViolation(
                rule="CHAR_REPETITION",
                severity="warning",
                message=f"Phát hiện lặp ký tự bất thường ({len(matches)} vị trí)"
            ))

        # Rule 2.3: Mô tả quá ngắn
        desc_length = len((description or '').strip())
        if desc_length < settings.MIN_DESCRIPTION_LENGTH:
            violations.append(RuleViolation(
                rule="SHORT_DESCRIPTION",
                severity="warning",
                message=f"Mô tả quá ngắn ({desc_length} ký tự, tối thiểu {settings.MIN_DESCRIPTION_LENGTH})"
            ))

        return violations
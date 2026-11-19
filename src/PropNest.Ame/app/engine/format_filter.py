import html
import re
from app.config import settings
from app.models.responses import RuleViolation

_HTML_TAG_RE = re.compile(r"<[^>]+>")
_MULTI_SPACE_RE = re.compile(r"\s+")


def strip_html(text: str | None) -> str:
    if not text:
        return ""
    cleaned = _HTML_TAG_RE.sub(" ", text)
    cleaned = html.unescape(cleaned)
    return _MULTI_SPACE_RE.sub(" ", cleaned).strip()


class FormatFilter:
    def check(
        self,
        title: str,
        description: str | None,
        area: float | None = None,
        floors: int | None = None,
        bedrooms: int | None = None,
    ) -> list[RuleViolation]:
        violations: list[RuleViolation] = []
        plain_title = strip_html(title)
        plain_desc = strip_html(description)
        text = f"{plain_title} {plain_desc}".strip()

        # Rule 2.1: Chữ IN HOA quá ngưỡng
        alpha_chars = [c for c in text if c.isalpha()]
        if alpha_chars:
            upper_ratio = sum(1 for c in alpha_chars if c.isupper()) / len(alpha_chars)
            if upper_ratio > settings.CAPS_THRESHOLD:
                violations.append(RuleViolation(
                    rule="EXCESSIVE_CAPS",
                    severity="manual_review",
                    message=f"Tỷ lệ chữ in hoa cao ({upper_ratio:.0%}, vượt ngưỡng {settings.CAPS_THRESHOLD:.0%}) — cần kiểm duyệt thủ công."
                ))

        # Rule 2.2: Lặp ký tự liên tiếp bất thường (vd: "đẹpppppp", "!!!!!!!")
        repeat_pattern = re.compile(r"(.)\1{" + str(settings.CHAR_REPEAT_LIMIT - 1) + r",}")
        if repeat_pattern.search(text):
            violations.append(RuleViolation(
                rule="CHAR_REPETITION",
                severity="manual_review",
                message="Phát hiện lặp ký tự spam/bất thường trong tiêu đề hoặc mô tả."
            ))

        # Rule 2.3: Mô tả quá ngắn (sau khi loại bỏ HTML tag)
        desc_length = len(plain_desc)
        if desc_length < settings.MIN_DESCRIPTION_LENGTH:
            violations.append(RuleViolation(
                rule="SHORT_DESCRIPTION",
                severity="manual_review",
                message=f"Mô tả quá ngắn ({desc_length} ký tự thuần văn bản, tối thiểu {settings.MIN_DESCRIPTION_LENGTH} ký tự)."
            ))

        # Rule 2.4: Mâu thuẫn thông số kỹ thuật cơ bản (chuyển duyệt tay)
        if area is not None and bedrooms is not None and area > 0 and area < 12 and bedrooms >= 4:
            violations.append(RuleViolation(
                rule="SPECS_INCONSISTENT",
                severity="manual_review",
                message=f"Thông số bất thường: diện tích {area:g} m² nhưng khai báo {bedrooms} phòng ngủ."
            ))
        if floors is not None and floors > 90:
            violations.append(RuleViolation(
                rule="SPECS_INCONSISTENT",
                severity="manual_review",
                message=f"Thông số bất thường: số tầng khai báo ({floors}) vượt ngưỡng hợp lý."
            ))

        return violations

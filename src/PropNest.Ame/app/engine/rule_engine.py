import re
from typing import Any, Optional

from app.engine.badword_filter import BadwordFilter
from app.engine.format_filter import FormatFilter, strip_html
from app.engine.phone_filter import PhoneFilter
from app.engine.price_filter import PriceFilter
from app.models.requests import ModerationRequest
from app.models.responses import Decision, ModerationResponse, RuleViolation


class RuleEngine:
    def __init__(self):
        self.badword_filter = BadwordFilter()
        self.phone_filter = PhoneFilter()
        self.format_filter = FormatFilter()
        self.price_filter = PriceFilter()

    @staticmethod
    def _evaluate_dynamic_rules(text: str, active_rules: list[dict[str, Any]]) -> list[RuleViolation]:
        violations: list[RuleViolation] = []
        for rule in active_rules:
            pattern = rule.get("keywordOrPattern") or rule.get("keyword_or_pattern") or ""
            if not pattern or pattern.startswith(("UPPERCASE_RATIO_EXCEEDED:", "MIN_DESCRIPTION_LENGTH:")):
                continue

            is_regex = bool(rule.get("isRegex", rule.get("is_regex", True)))
            rule_code = str(rule.get("ruleCode") or rule.get("rule_code") or "CUSTOM_RULE")
            severity_raw = str(rule.get("severity") or "ManualReview").lower()
            mapped_severity = "critical" if severity_raw in {"reject", "block", "critical"} else "manual_review"

            matched = False
            if is_regex:
                try:
                    matched = re.search(pattern, text, re.IGNORECASE | re.UNICODE) is not None
                except re.error:
                    matched = pattern.lower() in text.lower()
            else:
                matched = pattern.lower() in text.lower()

            if matched:
                violations.append(RuleViolation(
                    rule=rule_code,
                    severity=mapped_severity,
                    message=f"Vi phạm quy tắc kiểm duyệt ({rule_code}).",
                ))
        return violations

    def evaluate(
        self,
        req: ModerationRequest,
        external_benchmark: Optional[dict[str, Any]] = None,
    ) -> ModerationResponse:
        violations: list[RuleViolation] = []
        raw_text = f"{req.title} {req.description or ''}"
        plain_text = f"{strip_html(req.title)} {strip_html(req.description)}".strip()

        # ── Tầng 1: Badword, Phone & Dynamic critical rules (chống lách qua HTML tag) ──
        violations += self.badword_filter.check(plain_text)
        phone_violations = self.phone_filter.check(plain_text) or self.phone_filter.check(raw_text)
        violations += phone_violations
        if req.active_rules:
            violations += self._evaluate_dynamic_rules(plain_text, req.active_rules)

        critical = [v for v in violations if v.severity == "critical"]
        if critical:
            return ModerationResponse(
                listing_id=req.listing_id,
                decision=Decision.REJECTED,
                violations=violations,
                score=0.0,
                total_risk_score=100.0,
                keyword_risk_score=100.0,
                completeness_score=50.0,
                reason_codes=[v.rule for v in critical],
                summary="; ".join(v.message for v in critical),
            )

        # ── Tầng 2: Formatting, HTML text length & Spec Consistency ──
        violations += self.format_filter.check(
            req.title,
            req.description,
            area=req.area,
            floors=req.floors,
            bedrooms=req.bedrooms,
        )

        # ── Tầng 3: Price/m² Anomaly (KHÔNG chặn duyệt -> gắn cảnh báo giá ảo nếu nội dung/ảnh đạt) ──
        violations += self.price_filter.check(
            price=req.price,
            area=req.area,
            city=req.city,
            district=req.district,
            property_type=req.property_type,
            listing_type=req.listing_type,
            ward=req.ward,
            legacy_address=req.legacy_address,
            price_benchmark=req.price_benchmark,
            external_benchmark=external_benchmark,
        )

        manual_violations = [v for v in violations if v.severity in {"manual_review", "warning"}]
        price_warnings = [v for v in violations if v.severity == "price_warning"]

        if manual_violations:
            score = max(0.0, 1.0 - len(manual_violations) * 0.2)
            return ModerationResponse(
                listing_id=req.listing_id,
                decision=Decision.MANUAL_REVIEW,
                violations=violations,
                score=score,
                total_risk_score=round((1.0 - score) * 100.0, 1),
                price_anomaly_score=75.0 if price_warnings else 0.0,
                reason_codes=[v.rule for v in violations],
                price_warning_message=price_warnings[0].message if price_warnings else None,
                summary="; ".join(v.message for v in violations),
            )

        # Nếu chỉ có price_warning → vẫn trả APPROVED kèm cảnh báo giá ảo!
        if price_warnings:
            return ModerationResponse(
                listing_id=req.listing_id,
                decision=Decision.APPROVED,
                violations=price_warnings,
                score=0.85,
                total_risk_score=30.0,
                price_anomaly_score=80.0,
                completeness_score=95.0,
                reason_codes=[v.rule for v in price_warnings],
                price_warning_message=price_warnings[0].message,
                summary=price_warnings[0].message,
            )

        return ModerationResponse(
            listing_id=req.listing_id,
            decision=Decision.APPROVED,
            violations=[],
            score=1.0,
            total_risk_score=5.0,
            completeness_score=95.0,
            reason_codes=["CLEAN_CONTENT", "PRICE_WITHIN_BENCHMARK"],
            summary="Tin đăng hợp lệ về nội dung và mức giá khu vực.",
        )

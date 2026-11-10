from app.models.requests import ModerationRequest
from app.models.responses import ModerationResponse, Decision, RuleViolation
from app.engine.badword_filter import BadwordFilter
from app.engine.phone_filter import PhoneFilter
from app.engine.format_filter import FormatFilter
from app.engine.price_filter import PriceFilter

class RuleEngine:
    def __init__(self):
        self.badword_filter = BadwordFilter()
        self.phone_filter = PhoneFilter()
        self.format_filter = FormatFilter()
        self.price_filter = PriceFilter()

    def evaluate(self, req: ModerationRequest) -> ModerationResponse:
        violations: list[RuleViolation] = []
        text = f"{req.title} {req.description or ''}"

        # ── Tầng 1: Badword & Phone (short-circuit → Rejected) ──
        violations += self.badword_filter.check(text)
        violations += self.phone_filter.check(text)

        critical = [v for v in violations if v.severity == "critical"]
        if critical:
            return ModerationResponse(
                listing_id=req.listing_id,
                decision=Decision.REJECTED,
                violations=violations,
                score=0.0
            )

        # ── Tầng 2: Formatting & Spam (Warning / ManualReview) ──
        violations += self.format_filter.check(req.title, req.description)

        # ── Tầng 3: Price/m² Anomaly ──
        violations += self.price_filter.check(
            price=req.price,
            area=req.area,
            city=req.city,
            district=req.district,
            property_type=req.property_type,
            listing_type=req.listing_type
        )

        # ── Quyết định cuối cùng ──
        warnings = [v for v in violations if v.severity == "warning"]

        if warnings:
            score = max(0.0, 1.0 - len(warnings) * 0.2)
            return ModerationResponse(
                listing_id=req.listing_id,
                decision=Decision.MANUAL_REVIEW,
                violations=violations,
                score=score
            )

        return ModerationResponse(
            listing_id=req.listing_id,
            decision=Decision.APPROVED,
            violations=[],
            score=1.0
        )
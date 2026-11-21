from io import BytesIO
import random
import unittest

from PIL import Image, ImageDraw, ImageFilter

from app.engine.decision_aggregator import DecisionAggregator
from app.engine.image_filter import ImageFilter as AmeImageFilter
from app.engine.rule_engine import RuleEngine
from app.models.requests import ModerationRequest
from app.models.responses import Decision, LLMResult, RuleViolation


def _make_sharp_image_bytes(width: int = 480, height: int = 320) -> bytes:
    img = Image.new("RGB", (width, height), (240, 240, 240))
    draw = ImageDraw.Draw(img)
    rng = random.Random(42)
    for x in range(0, width, 24):
        color = (rng.randint(20, 120), rng.randint(40, 180), rng.randint(60, 200))
        draw.rectangle([x, 10, x + 12, height - 10], fill=color)
    draw.line([(0, 0), (width, height)], fill=(10, 10, 10), width=4)
    buf = BytesIO()
    img.save(buf, format="PNG")
    return buf.getvalue()


def _make_blurry_image_bytes(width: int = 480, height: int = 320) -> bytes:
    with Image.open(BytesIO(_make_sharp_image_bytes(width, height))) as sharp:
        blurred = sharp.filter(ImageFilter.GaussianBlur(radius=10))
        buf = BytesIO()
        blurred.save(buf, format="PNG")
        return buf.getvalue()


class RuleEngineAndAggregatorTests(unittest.TestCase):
    def setUp(self):
        self.engine = RuleEngine()
        self.aggregator = DecisionAggregator()

    def test_price_anomaly_only_still_approves_with_virtual_price_warning(self):
        req = ModerationRequest(
            listing_id=10,
            title="Bán căn hộ Quận 7 giá siêu rẻ",
            description="<p>Căn hộ 2 phòng ngủ đầy đủ nội thất, sổ hồng chính chủ tại Quận 7, tiện ích hồ bơi.</p>",
            property_type="Apartment",
            listing_type="Sale",
            price=250_000_000,  # quá thấp (~3.3tr/m² cho 75m²)
            area=75.0,
            city="Thành phố Hồ Chí Minh",
            district="",
            ward="Phường Tân Phú",
            address="10 Nguyễn Văn Linh",
            legacy_address="Quận 7, TP. Hồ Chí Minh",
        )
        rule_resp = self.engine.evaluate(req)
        self.assertEqual(rule_resp.decision, Decision.APPROVED)
        self.assertIn("PRICE_TOO_LOW", rule_resp.reason_codes)
        self.assertIsNotNone(rule_resp.price_warning_message)
        self.assertIn("giá ảo", rule_resp.price_warning_message.lower())

        llm = LLMResult(
            quality_score=0.92,
            is_fraudulent=False,
            category="apartment",
            explanation="Mô tả rõ ràng.",
            is_available=True,
        )
        final_resp = self.aggregator.aggregate(
            listing_id=req.listing_id,
            rule_response=rule_resp,
            llm_result=llm,
            image_violations=[],
        )
        self.assertEqual(final_resp.decision, Decision.APPROVED)
        self.assertIn("PRICE_TOO_LOW", final_resp.reason_codes)
        self.assertIsNotNone(final_resp.price_warning_message)
        self.assertGreater(final_resp.price_anomaly_score, 50.0)

    def test_blurry_image_routes_listing_to_admin_manual_review(self):
        sharp_res = AmeImageFilter.analyze_image_bytes(_make_sharp_image_bytes(), "#1")
        self.assertIsNone(sharp_res)

        blur_violation = AmeImageFilter.analyze_image_bytes(_make_blurry_image_bytes(), "#1")
        self.assertIsNotNone(blur_violation)
        self.assertEqual(blur_violation.rule, "IMAGE_BLURRY")
        self.assertEqual(blur_violation.severity, "manual_review")

        req = ModerationRequest(
            listing_id=11,
            title="Bán căn hộ Quận 7 view đẹp",
            description="Căn hộ 2 phòng ngủ thoáng mát đầy đủ tiện nghi cao cấp, sẵn sàng giao dịch.",
            property_type="Apartment",
            listing_type="Sale",
            price=4_800_000_000,
            area=75.0,
            city="TP. Hồ Chí Minh",
            district="Quận 7",
            address="10 Nguyễn Văn Linh",
        )
        rule_resp = self.engine.evaluate(req)
        llm = LLMResult(quality_score=0.9, is_fraudulent=False, category="apartment", is_available=True)

        final_resp = self.aggregator.aggregate(
            listing_id=11,
            rule_response=rule_resp,
            llm_result=llm,
            image_violations=[blur_violation],
        )
        self.assertEqual(final_resp.decision, Decision.MANUAL_REVIEW)
        self.assertIn("IMAGE_BLURRY", final_resp.reason_codes)

    def test_html_wrapped_phone_number_is_rejected(self):
        req = ModerationRequest(
            listing_id=12,
            title="Bán nhà riêng trung tâm",
            description="<p>Nhà mới đẹp, liên hệ <strong>0908</strong>.123.456 xem nhà ngay hôm nay.</p>",
            property_type="House",
            listing_type="Sale",
            price=6_500_000_000,
            area=70.0,
            city="TP. Hồ Chí Minh",
            district="Quận 7",
            address="22 Đường số 1",
        )
        rule_resp = self.engine.evaluate(req)
        self.assertEqual(rule_resp.decision, Decision.REJECTED)
        self.assertIn("PHONE_EXPOSURE", rule_resp.reason_codes)

    def test_missing_images_routes_to_manual_review(self):
        import asyncio
        violations = asyncio.run(AmeImageFilter().check([]))
        self.assertEqual(len(violations), 1)
        self.assertEqual(violations[0].rule, "IMAGE_MISSING")
        self.assertEqual(violations[0].severity, "manual_review")


if __name__ == "__main__":
    unittest.main()

import unittest

from app.engine.format_filter import FormatFilter


class FormatFilterTests(unittest.TestCase):
    def setUp(self):
        self.filter = FormatFilter()

    def test_strips_html_tags_before_length_check(self):
        html_short = "<p><strong><a>Ban gap</a></strong></p>"
        violations = self.filter.check("Bán nhà", html_short)
        self.assertTrue(any(v.rule == "SHORT_DESCRIPTION" for v in violations))

    def test_valid_rich_html_description_passes(self):
        html_ok = (
            "<p><strong>Chính chủ cần bán căn hộ chung cư Evergreen Nếnh</strong></p>"
            "<ul><li>Diện tích sử dụng 122 m2, 2 phòng ngủ, 2 WC.</li>"
            "<li>Pháp lý sổ hồng rõ ràng, tiện ích đầy đủ xung quanh.</li></ul>"
        )
        violations = self.filter.check(
            "Bán căn hộ Evergreen Bắc Ninh 122m2",
            html_ok,
            area=122.0,
            floors=2,
            bedrooms=2,
        )
        self.assertEqual(violations, [])

    def test_inconsistent_specs_flagged_for_manual_review(self):
        violations = self.filter.check(
            "Bán nhà siêu nhỏ nhiều phòng",
            "Nhà đẹp trung tâm thành phố gần trường học, chợ và bệnh viện tiện đi lại.",
            area=8.0,
            floors=2,
            bedrooms=5,
        )
        self.assertTrue(any(v.rule == "SPECS_INCONSISTENT" and v.severity == "manual_review" for v in violations))


if __name__ == "__main__":
    unittest.main()

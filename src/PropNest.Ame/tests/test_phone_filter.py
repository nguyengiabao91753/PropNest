import unittest

from app.engine.phone_filter import PhoneFilter


class PhoneFilterTests(unittest.TestCase):
    def setUp(self):
        self.filter = PhoneFilter()

    def test_detects_obfuscated_phone_number(self):
        violations = self.filter.check("Liên hệ chính chủ qua số 0909.123.456 để xem nhà.")
        self.assertEqual(len(violations), 1)
        self.assertEqual(violations[0].rule, "PHONE_EXPOSURE")
        self.assertEqual(violations[0].severity, "critical")

    def test_allows_area_and_price_numbers(self):
        violations = self.filter.check("Căn hộ diện tích 85 m2 giá bán 3500 triệu hướng Đông Nam.")
        self.assertEqual(violations, [])


if __name__ == "__main__":
    unittest.main()

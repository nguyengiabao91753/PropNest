import unittest

from app.engine.badword_filter import BadwordFilter


class BadwordFilterTests(unittest.TestCase):
    def setUp(self):
        self.filter = BadwordFilter()

    def test_detects_prohibited_keywords(self):
        violations = self.filter.check("Cam kết lợi nhuận 100% không lừa đảo")
        self.assertGreaterEqual(len(violations), 1)
        self.assertEqual(violations[0].rule, "BADWORD_DETECTED")
        self.assertEqual(violations[0].severity, "critical")

    def test_clean_text_passes(self):
        violations = self.filter.check("Bán căn hộ chung cư 2 phòng ngủ thoáng mát đầy đủ nội thất.")
        self.assertEqual(violations, [])


if __name__ == "__main__":
    unittest.main()

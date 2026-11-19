import asyncio
import json
import unittest

import httpx

from app.data.price_ranges import get_price_benchmark_info
from app.engine.external_price_client import (
    ExternalPriceClient,
    compute_benchmark_stats,
    extract_unit_prices_from_ads,
)
from app.engine.price_filter import PriceFilter


class PriceFilterTests(unittest.TestCase):
    def setUp(self):
        self.filter = PriceFilter()

    def test_resolves_vietnamese_address_with_prefixes_and_legacy_address(self):
        info = get_price_benchmark_info(
            city="Thành phố Hồ Chí Minh",
            district="",
            property_type="Apartment",
            listing_type="Sale",
            ward="Phường Tân Phú",
            legacy_address="Quận 7, TP. Hồ Chí Minh",
        )
        self.assertIsNotNone(info)
        self.assertIn("ho chi minh / 7", info["scope"])

    def test_low_price_produces_price_warning_with_virtual_price_message(self):
        # Quận 7 Apartment min=40M, avg=70M -> 4M/m² là quá thấp -> cảnh báo giá ảo
        violations = self.filter.check(
            price=300_000_000,
            area=75.0,
            city="Thành phố Hồ Chí Minh",
            district="",
            property_type="Apartment",
            listing_type="Sale",
            legacy_address="Quận 7, TP. Hồ Chí Minh",
        )
        self.assertEqual(len(violations), 1)
        self.assertEqual(violations[0].rule, "PRICE_TOO_LOW")
        self.assertEqual(violations[0].severity, "price_warning")
        self.assertIn("giá ảo", violations[0].message.lower())

    def test_bac_ninh_post_merger_normal_price_passes(self):
        # 122m² giá 3.3 tỷ (~27tr/m²) tại Việt Yên Bắc Ninh -> hợp lý
        violations = self.filter.check(
            price=3_300_000_000,
            area=122.0,
            city="Tỉnh Bắc Ninh",
            district="Thị xã Việt Yên",
            property_type="Apartment",
            listing_type="Sale",
        )
        self.assertEqual(violations, [])

    def test_uses_backend_benchmark_when_provided(self):
        violations = self.filter.check(
            price=10_000_000_000,
            area=50.0,
            city="Tỉnh Bắc Ninh",
            district="Việt Yên",
            property_type="Apartment",
            listing_type="Sale",
            price_benchmark={
                "minPricePerSqm": 20_000_000,
                "avgPricePerSqm": 30_000_000,
                "maxPricePerSqm": 45_000_000,
            },
        )
        self.assertEqual(len(violations), 1)
        self.assertEqual(violations[0].rule, "PRICE_TOO_HIGH")
        self.assertEqual(violations[0].severity, "price_warning")

    def test_prioritizes_external_benchmark_over_backend_and_local(self):
        ext_bench = {
            "min": 25_000_000,
            "avg": 32_000_000,
            "max": 42_000_000,
            "scope": "Quận 7 (API thị trường thực tế - 10 mẫu)",
            "source": "LiveMarketAPI",
        }
        # 10 triệu/m² < 25M * 0.5 = 12.5M -> PRICE_TOO_LOW từ external benchmark
        violations = self.filter.check(
            price=700_000_000,
            area=70.0,
            city="Thành phố Hồ Chí Minh",
            district="Quận 7",
            property_type="Apartment",
            listing_type="Sale",
            external_benchmark=ext_bench,
        )
        self.assertEqual(len(violations), 1)
        self.assertEqual(violations[0].rule, "PRICE_TOO_LOW")
        self.assertIn("API thị trường thực tế", violations[0].message)

    def test_extract_unit_prices_filters_trash_outliers_and_computes_trimmed_mean(self):
        ads = [
            {"price": 1, "size": 50},  # rác 1đ -> phải loại
            {"price": 3_000_000_000, "size": 2},  # diện tích < 5m2 -> phải loại
            {"price": 2_400_000_000, "size": 60, "region_name": "Tp Hồ Chí Minh"},  # 40M/m2
            {"price": 3_500_000_000, "size": 70, "region_name": "Tp Hồ Chí Minh"},  # 50M/m2
            {"price": 4_200_000_000, "size": 70, "region_name": "Tp Hồ Chí Minh"},  # 60M/m2
            {"price": 4_900_000_000, "size": 70, "region_name": "Tp Hồ Chí Minh"},  # 70M/m2
            {"price": 8_000_000_000, "size": 80, "region_name": "Tp Hồ Chí Minh"},  # 100M/m2
        ]
        prices = extract_unit_prices_from_ads(ads, "Sale")
        self.assertEqual(len(prices), 5)
        stats = compute_benchmark_stats(prices, "Quận 7, Hồ Chí Minh")
        self.assertIsNotNone(stats)
        self.assertEqual(stats["sampleCount"], 5)
        self.assertEqual(stats["source"], "LiveMarketAPI")
        self.assertGreater(stats["avg"], 40_000_000)

    def test_external_price_client_fetches_chotot_mock_and_caches_results(self):
        call_count = 0

        def handler(request: httpx.Request) -> httpx.Response:
            nonlocal call_count
            call_count += 1
            payload = {
                "total": 4,
                "ads": [
                    {"price": 2_000_000_000, "size": 50, "region_name": "Tp Hồ Chí Minh"},
                    {"price": 3_000_000_000, "size": 60, "region_name": "Tp Hồ Chí Minh"},
                    {"price": 4_500_000_000, "size": 75, "region_name": "Tp Hồ Chí Minh"},
                    {"price": 5_600_000_000, "size": 80, "region_name": "Tp Hồ Chí Minh"},
                ],
            }
            return httpx.Response(200, content=json.dumps(payload).encode("utf-8"))

        client = ExternalPriceClient(transport=httpx.MockTransport(handler))
        first = asyncio.run(
            client.fetch_benchmark(
                city="Thành phố Hồ Chí Minh",
                district="Quận 7",
                property_type="Apartment",
                listing_type="Sale",
            )
        )
        second = asyncio.run(
            client.fetch_benchmark(
                city="Thành phố Hồ Chí Minh",
                district="Quận 7",
                property_type="Apartment",
                listing_type="Sale",
            )
        )
        self.assertIsNotNone(first)
        self.assertEqual(first, second)
        self.assertEqual(call_count, 1)
        self.assertEqual(first["source"], "LiveMarketAPI")
        self.assertEqual(first["sampleCount"], 4)


if __name__ == "__main__":
    unittest.main()

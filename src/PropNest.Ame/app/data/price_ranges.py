"""
Khung giá tham chiếu theo khu vực cho BĐS tại TP.HCM.
Dữ liệu mẫu dựa trên thị trường 2024-2025.
Đơn vị: VNĐ/m².
Key format: (city_normalized, district_normalized, property_type, listing_type)
Value: (min_price_per_m2, max_price_per_m2)
"""

_PRICE_DATA: dict[tuple[str, str, str, str], tuple[float, float]] = {
    # TP.HCM - Quận 1
    ("hồ chí minh", "quận 1", "apartment", "sell"):  (80_000_000, 250_000_000),
    ("hồ chí minh", "quận 1", "apartment", "rent"):  (250_000, 800_000),
    ("hồ chí minh", "quận 1", "house", "sell"):      (150_000_000, 500_000_000),

    # TP.HCM - Quận 7
    ("hồ chí minh", "quận 7", "apartment", "sell"):  (40_000_000, 120_000_000),
    ("hồ chí minh", "quận 7", "apartment", "rent"):  (150_000, 450_000),
    ("hồ chí minh", "quận 7", "house", "sell"):      (60_000_000, 200_000_000),

    # TP.HCM - Thủ Đức
    ("hồ chí minh", "thủ đức", "apartment", "sell"): (30_000_000, 90_000_000),
    ("hồ chí minh", "thủ đức", "apartment", "rent"): (100_000, 350_000),
    ("hồ chí minh", "thủ đức", "house", "sell"):     (40_000_000, 150_000_000),
    ("hồ chí minh", "thủ đức", "land", "sell"):      (30_000_000, 120_000_000),

    # TP.HCM - Bình Thạnh
    ("hồ chí minh", "bình thạnh", "apartment", "sell"):  (45_000_000, 130_000_000),
    ("hồ chí minh", "bình thạnh", "house", "sell"):      (70_000_000, 220_000_000),

    # TP.HCM - Quận 9 (cũ, nay thuộc Thủ Đức)
    ("hồ chí minh", "quận 9", "apartment", "sell"):  (25_000_000, 70_000_000),
    ("hồ chí minh", "quận 9", "land", "sell"):       (20_000_000, 80_000_000),

    # Hà Nội - Hoàn Kiếm
    ("hà nội", "hoàn kiếm", "apartment", "sell"):    (100_000_000, 300_000_000),
    ("hà nội", "hoàn kiếm", "house", "sell"):        (200_000_000, 800_000_000),

    # Hà Nội - Cầu Giấy
    ("hà nội", "cầu giấy", "apartment", "sell"):     (50_000_000, 120_000_000),
    ("hà nội", "cầu giấy", "house", "sell"):         (80_000_000, 250_000_000),

    # Đà Nẵng
    ("đà nẵng", "hải châu", "apartment", "sell"):    (25_000_000, 70_000_000),
    ("đà nẵng", "hải châu", "house", "sell"):        (40_000_000, 150_000_000),
}

def _normalize(s: str) -> str:
    return s.strip().lower()

def get_price_range(
    city: str, district: str, property_type: str, listing_type: str
) -> tuple[float, float] | None:
    key = (_normalize(city), _normalize(district), _normalize(property_type), _normalize(listing_type))
    return _PRICE_DATA.get(key)
"""
Khung giá tham chiếu theo khu vực bất động sản Việt Nam.
Đơn vị: VNĐ/m² (đối với Sale) hoặc VNĐ/m²/tháng (đối với Rent).
Cấu trúc value: (min_price_per_m2, avg_price_per_m2, max_price_per_m2)
Hỗ trợ chuẩn hóa địa giới trước & sau sáp nhập (bỏ tiền tố Tỉnh/TP/Quận/Huyện, bỏ dấu, đọc legacy_address).
"""

import re
import unicodedata
from typing import Optional

_ADMIN_PREFIXES = (
    "thanh pho ",
    "tp. ",
    "tp ",
    "tinh ",
    "quan ",
    "q. ",
    "huyen ",
    "thi xa ",
    "tx. ",
    "tx ",
    "phuong ",
    "p. ",
    "xa ",
    "thi tran ",
)


def normalize_vn_text(value: str | None) -> str:
    if not value:
        return ""
    text = value.strip().replace("đ", "d").replace("Đ", "D")
    text = unicodedata.normalize("NFD", text)
    text = "".join(ch for ch in text if unicodedata.category(ch) != "Mn")
    text = text.lower()
    text = re.sub(r"[^a-z0-9\s]", " ", text)
    text = re.sub(r"\s+", " ", text).strip()
    return text


def normalize_admin_unit(value: str | None) -> str:
    norm = normalize_vn_text(value)
    if not norm:
        return ""
    for prefix in _ADMIN_PREFIXES:
        if norm.startswith(prefix):
            norm = norm[len(prefix):].strip()
    if norm in {"tphcm", "tp hcm", "hcm", "sai gon", "saigon"}:
        return "ho chi minh"
    if norm in {"hn", "thu do ha noi"}:
        return "ha noi"
    if norm in {"da nang city", "dn"}:
        return "da nang"
    return norm


def normalize_property_type(value: str | None) -> str:
    norm = normalize_vn_text(value)
    if norm in {"apartment", "can ho", "chung cu", "can ho chung cu"}:
        return "apartment"
    if norm in {"house", "nha", "nha rieng", "nha pho"}:
        return "house"
    if norm in {"villa", "biet thu"}:
        return "villa"
    if norm in {"land", "dat", "dat nen", "dat tho cu"}:
        return "land"
    return norm or "house"


def normalize_listing_type(value: str | None) -> str:
    norm = normalize_vn_text(value)
    if norm in {"sell", "sale", "ban", "can ban"}:
        return "sale"
    if norm in {"rent", "thue", "cho thue"}:
        return "rent"
    return norm or "sale"


# Format key: (city_norm, district_norm_or_star, property_type, listing_type)
# Format value: (min_price_per_m2, avg_price_per_m2, max_price_per_m2)
_PRICE_DATA: dict[tuple[str, str, str, str], tuple[float, float, float]] = {
    # ── TP. Hồ Chí Minh ──
    ("ho chi minh", "1", "apartment", "sale"): (80_000_000, 150_000_000, 320_000_000),
    ("ho chi minh", "1", "apartment", "rent"): (250_000, 480_000, 900_000),
    ("ho chi minh", "1", "house", "sale"): (150_000_000, 280_000_000, 550_000_000),
    ("ho chi minh", "1", "villa", "sale"): (180_000_000, 320_000_000, 600_000_000),
    ("ho chi minh", "3", "apartment", "sale"): (70_000_000, 120_000_000, 220_000_000),
    ("ho chi minh", "3", "house", "sale"): (130_000_000, 240_000_000, 450_000_000),
    ("ho chi minh", "7", "apartment", "sale"): (40_000_000, 70_000_000, 130_000_000),
    ("ho chi minh", "7", "apartment", "rent"): (150_000, 260_000, 480_000),
    ("ho chi minh", "7", "house", "sale"): (65_000_000, 125_000_000, 240_000_000),
    ("ho chi minh", "7", "villa", "sale"): (90_000_000, 160_000_000, 300_000_000),
    ("ho chi minh", "thu duc", "apartment", "sale"): (32_000_000, 58_000_000, 120_000_000),
    ("ho chi minh", "thu duc", "apartment", "rent"): (110_000, 200_000, 380_000),
    ("ho chi minh", "thu duc", "house", "sale"): (50_000_000, 95_000_000, 180_000_000),
    ("ho chi minh", "thu duc", "land", "sale"): (35_000_000, 65_000_000, 130_000_000),
    ("ho chi minh", "binh thanh", "apartment", "sale"): (45_000_000, 80_000_000, 150_000_000),
    ("ho chi minh", "binh thanh", "house", "sale"): (75_000_000, 135_000_000, 250_000_000),
    ("ho chi minh", "9", "apartment", "sale"): (28_000_000, 48_000_000, 85_000_000),
    ("ho chi minh", "9", "land", "sale"): (25_000_000, 50_000_000, 95_000_000),
    ("ho chi minh", "2", "apartment", "sale"): (55_000_000, 95_000_000, 190_000_000),
    ("ho chi minh", "*", "apartment", "sale"): (28_000_000, 65_000_000, 200_000_000),
    ("ho chi minh", "*", "apartment", "rent"): (100_000, 220_000, 600_000),
    ("ho chi minh", "*", "house", "sale"): (45_000_000, 110_000_000, 320_000_000),
    ("ho chi minh", "*", "house", "rent"): (120_000, 260_000, 650_000),
    ("ho chi minh", "*", "villa", "sale"): (60_000_000, 140_000_000, 380_000_000),
    ("ho chi minh", "*", "land", "sale"): (22_000_000, 58_000_000, 180_000_000),

    # ── Hà Nội ──
    ("ha noi", "hoan kiem", "apartment", "sale"): (95_000_000, 170_000_000, 340_000_000),
    ("ha noi", "hoan kiem", "house", "sale"): (180_000_000, 350_000_000, 850_000_000),
    ("ha noi", "cau giay", "apartment", "sale"): (50_000_000, 78_000_000, 135_000_000),
    ("ha noi", "cau giay", "house", "sale"): (90_000_000, 165_000_000, 300_000_000),
    ("ha noi", "ba dinh", "apartment", "sale"): (65_000_000, 110_000_000, 220_000_000),
    ("ha noi", "tay ho", "apartment", "sale"): (60_000_000, 105_000_000, 220_000_000),
    ("ha noi", "nam tu liem", "apartment", "sale"): (38_000_000, 65_000_000, 115_000_000),
    ("ha noi", "ha dong", "apartment", "sale"): (30_000_000, 50_000_000, 85_000_000),
    ("ha noi", "*", "apartment", "sale"): (30_000_000, 68_000_000, 220_000_000),
    ("ha noi", "*", "apartment", "rent"): (100_000, 210_000, 550_000),
    ("ha noi", "*", "house", "sale"): (45_000_000, 120_000_000, 360_000_000),
    ("ha noi", "*", "house", "rent"): (110_000, 240_000, 600_000),
    ("ha noi", "*", "villa", "sale"): (65_000_000, 145_000_000, 350_000_000),
    ("ha noi", "*", "land", "sale"): (20_000_000, 55_000_000, 160_000_000),

    # ── Bắc Ninh (bao gồm cả khu vực sáp nhập Bắc Giang cũ / Việt Yên) ──
    ("bac ninh", "bac ninh", "apartment", "sale"): (20_000_000, 35_000_000, 60_000_000),
    ("bac ninh", "bac ninh", "house", "sale"): (35_000_000, 70_000_000, 140_000_000),
    ("bac ninh", "viet yen", "apartment", "sale"): (16_000_000, 27_000_000, 48_000_000),
    ("bac ninh", "viet yen", "land", "sale"): (14_000_000, 28_000_000, 55_000_000),
    ("bac ninh", "tu son", "house", "sale"): (30_000_000, 60_000_000, 120_000_000),
    ("bac ninh", "*", "apartment", "sale"): (16_000_000, 30_000_000, 58_000_000),
    ("bac ninh", "*", "apartment", "rent"): (65_000, 130_000, 280_000),
    ("bac ninh", "*", "house", "sale"): (25_000_000, 55_000_000, 130_000_000),
    ("bac ninh", "*", "land", "sale"): (14_000_000, 32_000_000, 75_000_000),
    ("bac giang", "*", "apartment", "sale"): (15_000_000, 26_000_000, 48_000_000),
    ("bac giang", "*", "house", "sale"): (20_000_000, 45_000_000, 100_000_000),
    ("bac giang", "*", "land", "sale"): (12_000_000, 26_000_000, 60_000_000),

    # ── Đà Nẵng ──
    ("da nang", "hai chau", "apartment", "sale"): (35_000_000, 62_000_000, 110_000_000),
    ("da nang", "hai chau", "house", "sale"): (55_000_000, 110_000_000, 220_000_000),
    ("da nang", "son tra", "apartment", "sale"): (32_000_000, 58_000_000, 105_000_000),
    ("da nang", "*", "apartment", "sale"): (25_000_000, 48_000_000, 95_000_000),
    ("da nang", "*", "apartment", "rent"): (90_000, 180_000, 420_000),
    ("da nang", "*", "house", "sale"): (35_000_000, 78_000_000, 180_000_000),
    ("da nang", "*", "land", "sale"): (20_000_000, 45_000_000, 110_000_000),

    # ── Các tỉnh/thành phố trọng điểm khác ──
    ("binh duong", "*", "apartment", "sale"): (22_000_000, 38_000_000, 65_000_000),
    ("binh duong", "*", "house", "sale"): (25_000_000, 50_000_000, 100_000_000),
    ("binh duong", "*", "land", "sale"): (14_000_000, 30_000_000, 65_000_000),
    ("dong nai", "*", "apartment", "sale"): (20_000_000, 35_000_000, 60_000_000),
    ("dong nai", "*", "land", "sale"): (12_000_000, 28_000_000, 65_000_000),
    ("hai phong", "*", "apartment", "sale"): (22_000_000, 42_000_000, 80_000_000),
    ("hai phong", "*", "house", "sale"): (30_000_000, 70_000_000, 150_000_000),
    ("can tho", "*", "apartment", "sale"): (20_000_000, 35_000_000, 65_000_000),
    ("can tho", "*", "house", "sale"): (25_000_000, 55_000_000, 120_000_000),
    ("khanh hoa", "*", "apartment", "sale"): (28_000_000, 52_000_000, 110_000_000),
    ("quang ninh", "*", "apartment", "sale"): (25_000_000, 45_000_000, 85_000_000),
    ("hung yen", "*", "apartment", "sale"): (25_000_000, 45_000_000, 85_000_000),
    ("hung yen", "*", "villa", "sale"): (45_000_000, 95_000_000, 190_000_000),
    ("ba ria vung tau", "*", "apartment", "sale"): (24_000_000, 45_000_000, 85_000_000),
    ("lam dong", "*", "land", "sale"): (10_000_000, 28_000_000, 75_000_000),

    # ── Fallback toàn quốc theo loại hình khi tỉnh chưa có bảng chi tiết ──
    ("*", "*", "apartment", "sale"): (16_000_000, 42_000_000, 130_000_000),
    ("*", "*", "apartment", "rent"): (70_000, 170_000, 480_000),
    ("*", "*", "house", "sale"): (18_000_000, 55_000_000, 180_000_000),
    ("*", "*", "house", "rent"): (70_000, 180_000, 500_000),
    ("*", "*", "villa", "sale"): (30_000_000, 85_000_000, 260_000_000),
    ("*", "*", "villa", "rent"): (90_000, 220_000, 600_000),
    ("*", "*", "land", "sale"): (8_000_000, 30_000_000, 110_000_000),
    ("*", "*", "land", "rent"): (30_000, 90_000, 300_000),
}


def _extract_location_candidates(
    city: str,
    district: str | None,
    ward: str | None,
    legacy_address: str | None,
) -> tuple[list[str], list[str]]:
    city_candidates: list[str] = []
    district_candidates: list[str] = []

    norm_city = normalize_admin_unit(city)
    if norm_city:
        city_candidates.append(norm_city)

    norm_district = normalize_admin_unit(district)
    if norm_district:
        # Handle "quan 1" stripped prefix -> "1"
        district_candidates.append(norm_district)
        if norm_district.isdigit():
            district_candidates.append(norm_district)

    norm_ward = normalize_admin_unit(ward)
    if norm_ward:
        district_candidates.append(norm_ward)

    if legacy_address:
        for part in re.split(r"[,;/\-]+", legacy_address):
            cleaned = normalize_admin_unit(part)
            if cleaned:
                district_candidates.append(cleaned)
                city_candidates.append(cleaned)

    return city_candidates, district_candidates


def get_price_benchmark_info(
    city: str,
    district: str | None,
    property_type: str,
    listing_type: str,
    ward: Optional[str] = None,
    legacy_address: Optional[str] = None,
) -> dict | None:
    prop = normalize_property_type(property_type)
    ltype = normalize_listing_type(listing_type)
    city_candidates, district_candidates = _extract_location_candidates(city, district, ward, legacy_address)

    # 1. Exact (city, district/ward/legacy) match
    for c in city_candidates:
        for d in district_candidates:
            rng = _PRICE_DATA.get((c, d, prop, ltype))
            if rng:
                return {
                    "min": rng[0],
                    "avg": rng[1],
                    "max": rng[2],
                    "scope": f"{c} / {d}",
                }

    # 2. City/Province-level match
    for c in city_candidates:
        rng = _PRICE_DATA.get((c, "*", prop, ltype))
        if rng:
            return {
                "min": rng[0],
                "avg": rng[1],
                "max": rng[2],
                "scope": f"{c} (mặt bằng tỉnh/TP)",
            }

    # 3. National property-type baseline fallback
    rng = _PRICE_DATA.get(("*", "*", prop, ltype))
    if rng:
        return {
            "min": rng[0],
            "avg": rng[1],
            "max": rng[2],
            "scope": f"Mặt bằng tham chiếu {prop}/{ltype}",
        }
    return None


def get_price_range(
    city: str,
    district: str | None,
    property_type: str,
    listing_type: str,
    ward: Optional[str] = None,
    legacy_address: Optional[str] = None,
) -> tuple[float, float] | None:
    info = get_price_benchmark_info(city, district, property_type, listing_type, ward, legacy_address)
    if not info:
        return None
    return (info["min"], info["max"])

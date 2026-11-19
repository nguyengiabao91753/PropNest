from typing import Any, Optional
from app.config import settings
from app.data.price_ranges import get_price_benchmark_info, normalize_listing_type
from app.models.responses import RuleViolation


def _format_unit_price(unit_price: float, listing_type: str) -> str:
    is_rent = normalize_listing_type(listing_type) == "rent"
    if unit_price >= 1_000_000:
        return f"~{unit_price / 1_000_000:,.1f} triệu đ/m²" + ("/tháng" if is_rent else "")
    return f"~{unit_price:,.0f} đ/m²" + ("/tháng" if is_rent else "")


def _read_benchmark_number(benchmark: dict[str, Any], *keys: str) -> float:
    for key in keys:
        val = benchmark.get(key)
        if val is not None:
            try:
                return float(val)
            except (TypeError, ValueError):
                continue
    return 0.0


class PriceFilter:
    def check(
        self,
        price: float,
        area: float,
        city: str,
        district: str | None,
        property_type: str,
        listing_type: str,
        ward: Optional[str] = None,
        legacy_address: Optional[str] = None,
        price_benchmark: Optional[dict[str, Any]] = None,
        external_benchmark: Optional[dict[str, Any]] = None,
    ) -> list[RuleViolation]:
        violations: list[RuleViolation] = []

        if area <= 0 or price <= 0:
            if price == 0:
                violations.append(RuleViolation(
                    rule="PRICE_TOO_LOW",
                    severity="price_warning",
                    message="Tin đăng để giá 0 đ (thỏa thuận / bất thường) — giá có thể là giá ảo so với khu vực.",
                ))
            return violations

        unit_price = price / area  # VNĐ/m²

        min_price = 0.0
        avg_price = 0.0
        max_price = 0.0
        scope_label = ""

        # Ưu tiên 1: Khung giá từ External Live Market API / Biggee API (nếu có)
        if external_benchmark and float(external_benchmark.get("max", 0)) > 0:
            min_price = float(external_benchmark.get("min", 0))
            avg_price = float(external_benchmark.get("avg", 0))
            max_price = float(external_benchmark.get("max", 0))
            scope_label = str(external_benchmark.get("scope") or city)

        # Ưu tiên 2: Khung giá benchmark do Backend .NET cung cấp (DB MarketPriceBenchmarks / Published avg)
        if (max_price <= 0 or min_price <= 0) and price_benchmark:
            min_price = _read_benchmark_number(price_benchmark, "minPricePerSqm", "min_price_per_sqm", "MinPricePerSqm")
            avg_price = _read_benchmark_number(price_benchmark, "avgPricePerSqm", "avg_price_per_sqm", "AvgPricePerSqm")
            max_price = _read_benchmark_number(price_benchmark, "maxPricePerSqm", "max_price_per_sqm", "MaxPricePerSqm")
            scope_city = price_benchmark.get("city") or price_benchmark.get("City") or city
            scope_district = price_benchmark.get("district") or price_benchmark.get("District") or district or ""
            scope_label = f"{scope_district}, {scope_city}".strip(", ")

        # Ưu tiên 3: Bộ khung giá khu vực Việt Nam nội bộ (fallback khi offline)
        if max_price <= 0 or min_price <= 0:
            info = get_price_benchmark_info(
                city=city,
                district=district,
                property_type=property_type,
                listing_type=listing_type,
                ward=ward,
                legacy_address=legacy_address,
            )
            if info is None:
                return violations
            min_price = info["min"]
            avg_price = info["avg"]
            max_price = info["max"]
            scope_label = info["scope"]

        if avg_price <= 0:
            avg_price = (min_price + max_price) / 2.0

        deviation_threshold = settings.PRICE_DEVIATION_PERCENT / 100.0
        low_threshold = min_price * (1.0 - deviation_threshold)
        high_threshold = max_price * (1.0 + deviation_threshold)

        unit_str = _format_unit_price(unit_price, listing_type)
        avg_str = _format_unit_price(avg_price, listing_type)

        if unit_price < low_threshold:
            violations.append(RuleViolation(
                rule="PRICE_TOO_LOW",
                severity="price_warning",
                message=(
                    f"Đơn giá {unit_str} thấp hơn đáng kể so với giá bình quân khu vực ({avg_str} tại {scope_label}). "
                    "Giá tin đăng này có thể là giá ảo."
                ),
            ))
        elif unit_price > high_threshold:
            violations.append(RuleViolation(
                rule="PRICE_TOO_HIGH",
                severity="price_warning",
                message=(
                    f"Đơn giá {unit_str} cao hơn đáng kể so với giá bình quân khu vực ({avg_str} tại {scope_label}). "
                    "Giá tin đăng này có thể là giá ảo."
                ),
            ))

        return violations

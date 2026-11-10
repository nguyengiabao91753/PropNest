from app.config import settings
from app.models.responses import RuleViolation
from app.data.price_ranges import get_price_range

class PriceFilter:
    def check(
        self,
        price: float,
        area: float,
        city: str,
        district: str,
        property_type: str,
        listing_type: str
    ) -> list[RuleViolation]:
        violations = []

        if area <= 0 or price <= 0:
            return violations

        unit_price = price / area  # VNĐ/m²

        # Lấy khung giá tham chiếu theo khu vực
        price_range = get_price_range(city, district, property_type, listing_type)
        if price_range is None:
            return violations  # Không có dữ liệu → bỏ qua

        min_price, max_price = price_range
        deviation_threshold = settings.PRICE_DEVIATION_PERCENT / 100.0

        # Kiểm tra đơn giá lệch bất thường
        if unit_price < min_price * (1 - deviation_threshold):
            violations.append(RuleViolation(
                rule="PRICE_TOO_LOW",
                severity="warning",
                message=f"Đơn giá {unit_price:,.0f} đ/m² thấp hơn đáng kể so với khung giá khu vực ({min_price:,.0f} - {max_price:,.0f} đ/m²)"
            ))
        elif unit_price > max_price * (1 + deviation_threshold):
            violations.append(RuleViolation(
                rule="PRICE_TOO_HIGH",
                severity="warning",
                message=f"Đơn giá {unit_price:,.0f} đ/m² cao hơn đáng kể so với khung giá khu vực ({min_price:,.0f} - {max_price:,.0f} đ/m²)"
            ))

        return violations
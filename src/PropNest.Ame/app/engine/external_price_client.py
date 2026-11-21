import logging
import math
import re
import time
from typing import Any, Optional

import httpx

from app.config import settings
from app.data.price_ranges import (
    normalize_admin_unit,
    normalize_listing_type,
    normalize_property_type,
    normalize_vn_text,
)

logger = logging.getLogger(__name__)

_CHOTOT_CATEGORY_MAP = {
    "apartment": "1010",
    "house": "1020",
    "villa": "1020",
    "land": "1040",
}

_CHOTOT_LISTING_TYPE_MAP = {
    "sale": "s,k",
    "rent": "u,h",
}

_PREFIX_CLEAN_RE = re.compile(
    r"^(?:thành phố|thanh pho|tp\.|tp|tỉnh|tinh|quận|quan|q\.|huyện|huyen|thị xã|thi xa|tx\.|tx|phường|phuong|xã|xa)\s+",
    re.IGNORECASE | re.UNICODE,
)


def _strip_prefix_keep_diacritics(value: str | None) -> str:
    if not value:
        return ""
    cleaned = value.strip()
    return _PREFIX_CLEAN_RE.sub("", cleaned).strip()


def _extract_district_from_legacy(legacy_address: str | None) -> str:
    if not legacy_address:
        return ""
    parts = [p.strip() for p in re.split(r"[,;/\-]+", legacy_address) if p.strip()]
    if not parts:
        return ""
    # Thường phần đầu tiên của legacy_address là Quận/Huyện/TP cũ (vd: "Quận 7, Thành phố Hồ Chí Minh")
    return parts[0]


def _percentile(sorted_values: list[float], ratio: float) -> float:
    if not sorted_values:
        return 0.0
    if len(sorted_values) == 1:
        return sorted_values[0]
    pos = (len(sorted_values) - 1) * ratio
    low = int(math.floor(pos))
    high = min(len(sorted_values) - 1, int(math.ceil(pos)))
    if low == high:
        return sorted_values[low]
    weight = pos - low
    return sorted_values[low] * (1.0 - weight) + sorted_values[high] * weight


def extract_unit_prices_from_ads(ads: list[dict[str, Any]], listing_type: str) -> list[float]:
    ltype = normalize_listing_type(listing_type)
    # Chặn các tin đăng giá ảo/rác kiểu 1đ hoặc 1.000đ làm méo mẫu thị trường
    min_valid_unit = 500_000.0 if ltype == "sale" else 10_000.0
    max_valid_unit = 2_000_000_000.0 if ltype == "sale" else 10_000_000.0

    unit_prices: list[float] = []
    for ad in ads:
        if not isinstance(ad, dict):
            continue
        raw_price = ad.get("price")
        raw_size = ad.get("size")
        unit_price: Optional[float] = None

        try:
            price = float(raw_price) if raw_price is not None else 0.0
            size = float(raw_size) if raw_size is not None else 0.0
            if price > 0 and size > 5.0:
                unit_price = price / size
            elif ltype == "sale" and ad.get("price_million_per_m2") is not None:
                unit_price = float(ad["price_million_per_m2"]) * 1_000_000.0
        except (TypeError, ValueError):
            continue

        if unit_price is not None and min_valid_unit <= unit_price <= max_valid_unit:
            unit_prices.append(unit_price)

    return sorted(unit_prices)


def compute_benchmark_stats(
    sorted_unit_prices: list[float],
    scope: str,
    source: str = "LiveMarketAPI",
) -> Optional[dict[str, Any]]:
    n = len(sorted_unit_prices)
    if n < 3:
        return None

    min_price = _percentile(sorted_unit_prices, 0.15)
    max_price = _percentile(sorted_unit_prices, 0.85)

    # Trimmed mean (cắt 15% hai đầu) để loại bỏ outlier; nếu n quá ít dùng trung bình chuẩn
    trim_count = max(0, int(n * 0.15))
    trimmed = sorted_unit_prices[trim_count : n - trim_count] if (n - 2 * trim_count) >= 1 else sorted_unit_prices
    avg_price = sum(trimmed) / len(trimmed)

    return {
        "min": round(min_price, 0),
        "avg": round(avg_price, 0),
        "max": round(max_price, 0),
        "scope": f"{scope} (API thị trường thực tế - {n} mẫu)",
        "source": source,
        "sampleCount": n,
    }


class ExternalPriceClient:
    """Tra cứu khung giá thị trường thực tế từ API bên ngoài.

    - Nếu có `BIGGEE_PARTNER_KEY`, ưu tiên gọi Biggee Open API (`/api/dinhgianhadat`).
    - Mặc định gọi Public Market API (`gateway.chotot.com/v1/public/ad-listing`) không cần key.
    - Cache kết quả trong RAM theo TTL (`EXTERNAL_PRICE_CACHE_TTL_SECONDS`).
    """

    def __init__(self, transport: httpx.AsyncBaseTransport | None = None):
        self._cache: dict[tuple[str, str, str, str], tuple[float, dict[str, Any]]] = {}
        self._transport = transport

    def _build_client(self) -> httpx.AsyncClient:
        kwargs: dict[str, Any] = {"timeout": settings.EXTERNAL_PRICE_API_TIMEOUT}
        if self._transport is not None:
            kwargs["transport"] = self._transport
        return httpx.AsyncClient(**kwargs)

    async def _fetch_from_biggee(
        self,
        client: httpx.AsyncClient,
        city: str,
        district: str | None,
        ward: str | None,
        legacy_address: str | None,
    ) -> Optional[dict[str, Any]]:
        if not settings.BIGGEE_PARTNER_KEY:
            return None

        area_label = ", ".join(p for p in [ward, district or _extract_district_from_legacy(legacy_address), city] if p)
        try:
            response = await client.request(
                "GET",
                settings.BIGGEE_API_URL,
                json={
                    "partner_key": settings.BIGGEE_PARTNER_KEY,
                    "uri_req": area_label,
                },
                headers={"Content-Type": "application/json"},
            )
            if response.status_code != 200:
                return None
            payload = response.json()
            dinhgia = (payload.get("data") or {}).get("dinhgia") or {}
            low = float(dinhgia.get("dongia_tb_thap") or 0)
            avg = float(dinhgia.get("dongia_thamkhao") or 0)
            high = float(dinhgia.get("dongia_tb_cao") or 0)
            if low > 0 and avg > 0 and high > 0:
                return {
                    "min": low,
                    "avg": avg,
                    "max": high,
                    "scope": f"{area_label} (Biggee Open API)",
                    "source": "BiggeeAPI",
                    "sampleCount": len((payload.get("data") or {}).get("sosanh") or []),
                }
        except Exception as ex:
            logger.warning("Biggee API lookup failed for '%s': %s", area_label, ex)
        return None

    async def _fetch_from_chotot(
        self,
        client: httpx.AsyncClient,
        city: str,
        district: str | None,
        ward: str | None,
        legacy_address: str | None,
        property_type: str,
        listing_type: str,
    ) -> Optional[dict[str, Any]]:
        prop = normalize_property_type(property_type)
        ltype = normalize_listing_type(listing_type)
        cg = _CHOTOT_CATEGORY_MAP.get(prop, "1000")
        st = _CHOTOT_LISTING_TYPE_MAP.get(ltype, "s,k")

        city_clean = _strip_prefix_keep_diacritics(city)
        raw_district = district.strip() if district and district.strip() else _extract_district_from_legacy(legacy_address)
        district_clean = _strip_prefix_keep_diacritics(raw_district) if raw_district else ""

        queries: list[tuple[str, str]] = []
        if district_clean and city_clean and normalize_vn_text(district_clean) != normalize_vn_text(city_clean):
            queries.append((f"{raw_district} {city_clean}", f"{raw_district}, {city_clean}"))
        elif ward and city_clean:
            queries.append((f"{ward} {city_clean}", f"{ward}, {city_clean}"))
        if city_clean:
            queries.append((city_clean, city_clean))

        for query_text, scope_label in queries:
            try:
                response = await client.get(
                    settings.CHOTOT_PUBLIC_API_URL,
                    params={"cg": cg, "st": st, "limit": 40, "q": query_text},
                )
                if response.status_code != 200:
                    continue

                data = response.json()
                ads = data.get("ads") or []
                if not ads:
                    continue

                # Ưu tiên các tin đúng Tỉnh/TP nếu API trả rộng hơn
                city_norm = normalize_admin_unit(city)
                scoped_ads = [
                    ad for ad in ads
                    if not city_norm or normalize_admin_unit(ad.get("region_name")) in {city_norm, ""}
                    or city_norm in normalize_admin_unit(ad.get("region_name"))
                ]
                candidate_ads = scoped_ads if len(scoped_ads) >= 3 else ads
                unit_prices = extract_unit_prices_from_ads(candidate_ads, listing_type)
                stats = compute_benchmark_stats(unit_prices, scope_label, source="LiveMarketAPI")
                if stats is not None:
                    return stats
            except Exception as ex:
                logger.warning("LiveMarketAPI query '%s' failed: %s", query_text, ex)
                continue

        return None

    async def fetch_benchmark(
        self,
        city: str,
        district: str | None,
        property_type: str,
        listing_type: str,
        ward: Optional[str] = None,
        legacy_address: Optional[str] = None,
    ) -> Optional[dict[str, Any]]:
        if not settings.EXTERNAL_PRICE_API_ENABLED:
            return None

        prop = normalize_property_type(property_type)
        ltype = normalize_listing_type(listing_type)
        city_norm = normalize_admin_unit(city)
        dist_raw = district or _extract_district_from_legacy(legacy_address) or ward or "*"
        dist_norm = normalize_admin_unit(dist_raw) or "*"
        cache_key = (city_norm, dist_norm, prop, ltype)

        now = time.monotonic()
        cached = self._cache.get(cache_key)
        if cached and (now - cached[0]) < settings.EXTERNAL_PRICE_CACHE_TTL_SECONDS:
            return cached[1]

        async with self._build_client() as client:
            result = await self._fetch_from_biggee(client, city, district, ward, legacy_address)
            if result is None:
                result = await self._fetch_from_chotot(
                    client, city, district, ward, legacy_address, property_type, listing_type
                )

        if result is not None:
            self._cache[cache_key] = (now, result)

        return result

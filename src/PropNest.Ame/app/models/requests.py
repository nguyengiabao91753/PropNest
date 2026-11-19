from typing import Any, Optional
from pydantic import BaseModel, ConfigDict, Field


def _to_camel(snake: str) -> str:
    """Chuyển snake_case thành camelCase: listing_id → listingId"""
    parts = snake.split("_")
    return parts[0] + "".join(w.capitalize() for w in parts[1:])


class ModerationRequest(BaseModel):
    """Request body cho API kiểm duyệt tin đăng.

    .NET AmeModerationClient gửi JSON với camelCase (listingId, propertyType, …)
    nên bật populate_by_name + alias_generator để Pydantic chấp nhận cả hai dạng.
    """

    model_config = ConfigDict(
        populate_by_name=True,
        alias_generator=_to_camel,
    )

    listing_id: int = Field(..., description="ID tin đăng")
    correlation_id: Optional[str] = Field(None, description="CorrelationId phiên kiểm duyệt")
    title: str = Field(..., description="Tiêu đề tin")
    description: Optional[str] = Field(None, description="Mô tả chi tiết")
    property_type: str = Field(..., description="Loại BĐS: Apartment, House, Villa, Land, ...")
    listing_type: str = Field(..., description="Loại tin: Sale, Rent")
    price: float = Field(..., ge=0, description="Giá (VNĐ)")
    area: float = Field(..., gt=0, description="Diện tích (m²)")
    city: str = Field(..., description="Tỉnh / Thành phố")
    district: Optional[str] = Field("", description="Quận / Huyện (có thể rỗng ở chế độ sau sáp nhập)")
    ward: Optional[str] = Field(None, description="Phường / Xã")
    address: str = Field(..., description="Địa chỉ cụ thể")
    administrative_mode: Optional[str] = Field("PostMerger", description="PostMerger | PreMerger")
    province_code: Optional[int] = Field(None, description="Mã Tỉnh/TP")
    district_code: Optional[int] = Field(None, description="Mã Quận/Huyện")
    ward_code: Optional[int] = Field(None, description="Mã Phường/Xã")
    legacy_address: Optional[str] = Field(None, description="Địa chỉ trước sáp nhập")
    floors: Optional[int] = Field(None, description="Số tầng")
    bedrooms: Optional[int] = Field(None, description="Số phòng ngủ")
    bathrooms: Optional[int] = Field(None, description="Số phòng vệ sinh")
    balcony_direction: Optional[str] = Field(None, description="Hướng ban công")
    images: Optional[list[str]] = Field(None, description="Danh sách URL ảnh của tin đăng")
    active_rules: list[dict[str, Any]] = Field(default_factory=list, description="Quy tắc kiểm duyệt động từ DB")
    price_benchmark: Optional[dict[str, Any]] = Field(None, description="Khung giá khu vực truyền từ DB")

from pydantic import BaseModel, ConfigDict, Field
from typing import Optional


def _to_camel(snake: str) -> str:
    """Chuyển snake_case thành camelCase: listing_id → listingId"""
    parts = snake.split("_")
    return parts[0] + "".join(w.capitalize() for w in parts[1:])


class ModerationRequest(BaseModel):
    """Request body cho API kiểm duyệt tin đăng.

    .NET AmeModerationClient gửi JSON với camelCase (listingId, propertyType, …)
    nên cần bật populate_by_name + alias_generator để Pydantic chấp nhận cả hai dạng.
    """

    model_config = ConfigDict(
        populate_by_name=True,
        alias_generator=_to_camel,
    )

    listing_id: int = Field(..., description="ID tin đăng")
    title: str = Field(..., description="Tiêu đề tin")
    description: Optional[str] = Field(None, description="Mô tả chi tiết")
    property_type: str = Field(..., description="Loại BĐS: apartment, house, land, ...")
    listing_type: str = Field(..., description="Loại tin: sell, rent")
    price: float = Field(..., ge=0, description="Giá (VNĐ)")
    area: float = Field(..., gt=0, description="Diện tích (m²)")
    city: str = Field(..., description="Thành phố")
    district: str = Field(..., description="Quận/Huyện")
    ward: Optional[str] = Field(None, description="Phường/Xã")
    address: str = Field(..., description="Địa chỉ cụ thể")

from pydantic import BaseModel, Field
from typing import Optional

class ModerationRequest(BaseModel):
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
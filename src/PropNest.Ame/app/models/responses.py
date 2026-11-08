from pydantic import BaseModel, Field
from typing import Optional
from enum import Enum

class Decision(str, Enum):
    APPROVED = "Approved"
    REJECTED = "Rejected"
    MANUAL_REVIEW = "ManualReview"
    WARNING = "Warning"

class RuleViolation(BaseModel):
    rule: str = Field(..., description="Tên rule vi phạm")
    severity: str = Field(..., description="Mức độ: critical, warning, info")
    message: str = Field(..., description="Mô tả vi phạm")

class ModerationResponse(BaseModel):
    listing_id: int
    decision: Decision
    source: str = Field(default="AI-RuleEngine", description="Nguồn kiểm duyệt")
    violations: list[RuleViolation] = Field(default_factory=list)
    score: float = Field(default=1.0, description="Điểm tin hợp lệ: 0.0 → 1.0")
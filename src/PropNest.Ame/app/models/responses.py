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

# Kết quả từ Ollama LLM
class LLMResult(BaseModel):
    quality_score: float = Field(default=0.5, ge=0.0, le=1.0,
        description="Điểm chất lượng nội dung: 0.0 → 1.0")
    is_fraudulent: bool = Field(default=False,
        description="LLM phát hiện dấu hiệu lừa đảo")
    category: str = Field(default="unknown",
        description="Phân loại BĐS do LLM xác định")
    explanation: str = Field(default="",
        description="Giải thích của LLM")
    is_available: bool = Field(default=True,
        description="Ollama có phản hồi thành công không")

class ModerationResponse(BaseModel):
    listing_id: int
    decision: Decision
    source: str = Field(default="AI-RuleEngine", description="Nguồn kiểm duyệt")
    violations: list[RuleViolation] = Field(default_factory=list)
    score: float = Field(default=1.0, description="Điểm tin hợp lệ: 0.0 → 1.0")
    llm_result: LLMResult | None = Field(default=None,
        description="Kết quả phân tích từ LLM (null nếu LLM tắt)")
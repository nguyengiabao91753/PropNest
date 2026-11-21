from enum import Enum
from typing import Optional
from pydantic import BaseModel, ConfigDict, Field


def _to_camel(snake: str) -> str:
    parts = snake.split("_")
    return parts[0] + "".join(w.capitalize() for w in parts[1:])


class Decision(str, Enum):
    APPROVED = "Approved"
    REJECTED = "Rejected"
    MANUAL_REVIEW = "ManualReview"
    WARNING = "Warning"


class RuleViolation(BaseModel):
    model_config = ConfigDict(populate_by_name=True, alias_generator=_to_camel)

    rule: str = Field(..., description="Tên rule vi phạm")
    severity: str = Field(..., description="Mức độ: critical, manual_review, warning, price_warning, info")
    message: str = Field(..., description="Mô tả vi phạm")


class LLMResult(BaseModel):
    model_config = ConfigDict(populate_by_name=True, alias_generator=_to_camel)

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
    model_config = ConfigDict(populate_by_name=True, alias_generator=_to_camel)

    listing_id: int
    decision: Decision
    source: str = Field(default="AI-RuleEngine", description="Nguồn kiểm duyệt")
    violations: list[RuleViolation] = Field(default_factory=list)
    score: float = Field(default=1.0, description="Điểm tin hợp lệ: 0.0 → 1.0")
    total_risk_score: float = Field(default=0.0, description="Tổng điểm rủi ro: 0 → 100")
    keyword_risk_score: float = Field(default=0.0, description="Điểm rủi ro từ cấm / SĐT: 0 → 100")
    price_anomaly_score: float = Field(default=0.0, description="Điểm bất thường về giá: 0 → 100")
    completeness_score: float = Field(default=95.0, description="Độ hoàn thiện nội dung: 0 → 100")
    duplicate_score: float = Field(default=0.0, description="Điểm trùng lặp: 0 → 100")
    ai_semantic_score: Optional[float] = Field(default=None, description="Rủi ro ngữ nghĩa AI: 0 → 100")
    summary: str = Field(default="", description="Tóm tắt kết quả phân tích AME")
    processing_time_ms: int = Field(default=0, description="Thời gian xử lý (ms)")
    reason_codes: list[str] = Field(default_factory=list, description="Danh sách mã lý do / cảnh báo")
    price_warning_message: Optional[str] = Field(default=None, description="Cảnh báo giá ảo (nếu có)")
    llm_result: Optional[LLMResult] = Field(default=None,
        description="Kết quả phân tích từ LLM (null nếu LLM tắt)")

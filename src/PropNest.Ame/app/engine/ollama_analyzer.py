import json
import logging
import httpx
from app.config import settings
from app.models.requests import ModerationRequest
from app.models.responses import LLMResult

logger = logging.getLogger(__name__)

# Prompt template cho phân tích BĐS tiếng Việt
ANALYSIS_PROMPT = """Bạn là chuyên gia kiểm duyệt tin đăng bất động sản Việt Nam. Phân tích tin đăng sau và trả về JSON.

## Tin đăng:
- Tiêu đề: {title}
- Mô tả: {description}
- Loại BĐS: {property_type}
- Loại tin: {listing_type}
- Giá: {price:,.0f} VNĐ
- Diện tích: {area} m²
- Đơn giá: {unit_price:,.0f} VNĐ/m²
- Khu vực: {district}, {city}

## Tham khảo khung giá thị trường BĐS Việt Nam (VNĐ/m²):
- Căn hộ TP.HCM: 25-250 triệu/m² tùy quận
- Nhà phố TP.HCM: 40-500 triệu/m² tùy quận
- Đất nền TP.HCM: 20-120 triệu/m²
- Căn hộ Hà Nội: 30-300 triệu/m²
- Thuê căn hộ: 100.000-800.000 VNĐ/m²/tháng

## Yêu cầu phân tích:
1. quality_score (0.0-1.0): Đánh giá chất lượng NỘI DUNG mô tả (chi tiết, rõ ràng, hữu ích cho người mua). Mô tả càng chi tiết về diện tích, phòng, tiện ích, pháp lý thì điểm càng cao.
2. is_fraudulent (true/false): CHỈ đánh giá true khi có dấu hiệu lừa đảo RÕ RÀNG như: hứa lợi nhuận phi thực tế, thông tin mâu thuẫn nghiêm trọng, yêu cầu chuyển tiền trước. KHÔNG đánh giá giá cả ở đây (đã có hệ thống kiểm tra giá riêng).
3. category: Phân loại BĐS (apartment/house/land/villa/office/shophouse)
4. explanation: Giải thích ngắn gọn (1-2 câu)

## Trả về JSON duy nhất, không giải thích thêm:
{{"quality_score": 0.0, "is_fraudulent": false, "category": "", "explanation": ""}}"""


class OllamaAnalyzer:
    def __init__(self):
        self.base_url = settings.OLLAMA_BASE_URL
        self.model = settings.OLLAMA_MODEL
        self.timeout = settings.OLLAMA_TIMEOUT
        self.enabled = settings.OLLAMA_ENABLED

    async def analyze(self, req: ModerationRequest) -> LLMResult:
        """Gọi Ollama để phân tích ngữ nghĩa. Trả fallback nếu lỗi."""
        if not self.enabled:
            return LLMResult(
                quality_score=0.5,
                is_available=False,
                explanation="LLM analysis is disabled"
            )

        unit_price = req.price / req.area if req.area > 0 else 0

        prompt = ANALYSIS_PROMPT.format(
            title=req.title,
            description=req.description or "(không có mô tả)",
            property_type=req.property_type,
            listing_type=req.listing_type,
            price=req.price,
            area=req.area,
            unit_price=unit_price,
            district=req.district,
            city=req.city
        )

        try:
            async with httpx.AsyncClient(timeout=self.timeout) as client:
                response = await client.post(
                    f"{self.base_url}/api/generate",
                    json={
                        "model": self.model,
                        "prompt": prompt,
                        "stream": False,
                        "format": "json",
                        "options": {
                            "temperature": 0.1,
                            "num_predict": 256
                        }
                    }
                )
                response.raise_for_status()

            data = response.json()
            raw_text = data.get("response", "")

            # Parse JSON từ response LLM
            result = json.loads(raw_text)

            return LLMResult(
                quality_score=max(0.0, min(1.0, float(result.get("quality_score", 0.5)))),
                is_fraudulent=bool(result.get("is_fraudulent", False)),
                category=str(result.get("category", "unknown")),
                explanation=str(result.get("explanation", "")),
                is_available=True
            )

        except httpx.ConnectError:
            logger.warning("Ollama is not running at %s", self.base_url)
            return self._fallback("Ollama service is not running")

        except httpx.TimeoutException:
            logger.warning("Ollama request timed out after %ss", self.timeout)
            return self._fallback("Ollama request timed out")

        except (json.JSONDecodeError, KeyError, ValueError) as e:
            logger.warning("Failed to parse Ollama response: %s", e)
            return self._fallback(f"Failed to parse LLM response: {e}")

        except Exception as e:
            logger.error("Unexpected Ollama error: %s", e)
            return self._fallback(f"Unexpected error: {e}")

    @staticmethod
    def _fallback(reason: str) -> LLMResult:
        return LLMResult(
            quality_score=0.5,
            is_fraudulent=False,
            category="unknown",
            explanation=reason,
            is_available=False
        )

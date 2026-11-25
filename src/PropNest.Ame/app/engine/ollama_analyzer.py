import json
import logging
import httpx
from app.config import settings
from app.engine.format_filter import strip_html
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
    _MAX_PROMPT_DESCRIPTION_CHARS = 1500

    def __init__(self):
        self.base_url = settings.OLLAMA_BASE_URL.rstrip("/")
        self.model = settings.OLLAMA_MODEL
        self.timeout = settings.OLLAMA_TIMEOUT
        self.num_ctx = settings.OLLAMA_NUM_CTX
        self.keep_alive = settings.OLLAMA_KEEP_ALIVE
        self.enabled = settings.OLLAMA_ENABLED

    async def warmup(self) -> bool:
        """Nạp sẵn model vào VRAM/RAM khi AME khởi động để tránh cold-start timeout ở tin đầu tiên."""
        if not self.enabled or not settings.OLLAMA_WARMUP_ON_STARTUP:
            return False

        try:
            warmup_timeout = httpx.Timeout(connect=5.0, read=max(90.0, self.timeout), write=10.0, pool=5.0)
            async with httpx.AsyncClient(timeout=warmup_timeout) as client:
                response = await client.post(
                    f"{self.base_url}/api/generate",
                    json={
                        "model": self.model,
                        "prompt": "ok",
                        "stream": False,
                        "keep_alive": self.keep_alive,
                        "options": {
                            "num_ctx": self.num_ctx,
                            "num_predict": 1,
                            "temperature": 0.0,
                        },
                    },
                )
                response.raise_for_status()
            logger.info("Model %s warmed up successfully at %s (num_ctx=%s, keep_alive=%s)",
                        self.model, self.base_url, self.num_ctx, self.keep_alive)
            return True
        except Exception as e:
            logger.warning("Startup warmup skipped/failed for %s at %s: %s", self.model, self.base_url, e)
            return False

    async def analyze(self, req: ModerationRequest) -> LLMResult:
        """Gọi Ollama để phân tích ngữ nghĩa. Trả fallback nếu lỗi."""
        if not self.enabled:
            return LLMResult(
                quality_score=0.5,
                is_available=False,
                explanation="Phân tích ngữ nghĩa tự động đang tắt."
            )

        unit_price = req.price / req.area if req.area > 0 else 0
        location_desc = ", ".join(p for p in [req.ward, req.district, req.city] if p)
        if req.legacy_address:
            location_desc = f"{location_desc} (Trước sáp nhập: {req.legacy_address})"

        plain_desc = strip_html(req.description)
        if len(plain_desc) > self._MAX_PROMPT_DESCRIPTION_CHARS:
            plain_desc = plain_desc[: self._MAX_PROMPT_DESCRIPTION_CHARS].rstrip() + "..."

        prompt = ANALYSIS_PROMPT.format(
            title=strip_html(req.title),
            description=plain_desc or "(không có mô tả)",
            property_type=req.property_type,
            listing_type=req.listing_type,
            price=req.price,
            area=req.area,
            unit_price=unit_price,
            district=location_desc or (req.district or ""),
            city=req.city
        )

        try:
            req_timeout = httpx.Timeout(connect=10.0, read=self.timeout, write=15.0, pool=10.0)
            async with httpx.AsyncClient(timeout=req_timeout) as client:
                response = await client.post(
                    f"{self.base_url}/api/generate",
                    json={
                        "model": self.model,
                        "prompt": prompt,
                        "stream": False,
                        "format": "json",
                        "keep_alive": self.keep_alive,
                        "options": {
                            "num_ctx": self.num_ctx,
                            "temperature": 0.1,
                            "num_predict": 220
                        }
                    }
                )
                response.raise_for_status()

            data = response.json()
            raw_text = (data.get("response") or "").strip()
            if raw_text.startswith("```"):
                raw_text = raw_text.strip("`").removeprefix("json").strip()
            if not raw_text.startswith("{"):
                start = raw_text.find("{")
                end = raw_text.rfind("}")
                if start != -1 and end > start:
                    raw_text = raw_text[start : end + 1]

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
            return self._fallback("Dịch vụ phân tích ngữ nghĩa tạm thời không kết nối được.")

        except httpx.TimeoutException:
            logger.warning("Ollama request timed out after %ss", self.timeout)
            return self._fallback("Quá thời gian chờ phân tích ngữ nghĩa tự động.")

        except (json.JSONDecodeError, KeyError, ValueError) as e:
            logger.warning("Failed to parse Ollama response: %s", e)
            return self._fallback("Không thể đọc kết quả phân tích ngữ nghĩa tự động.")

        except Exception as e:
            logger.error("Unexpected Ollama error: %s", e)
            return self._fallback("Hệ thống phân tích ngữ nghĩa tạm thời gián đoạn.")

    @staticmethod
    def _fallback(reason: str) -> LLMResult:
        return LLMResult(
            quality_score=0.5,
            is_fraudulent=False,
            category="unknown",
            explanation=reason,
            is_available=False
        )

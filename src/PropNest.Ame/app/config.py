from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    APP_NAME: str = "PropNest AME"
    PORT: int = 8001
    CAPS_THRESHOLD: float = 0.5       # >50% chữ in hoa → Warning
    MIN_DESCRIPTION_LENGTH: int = 30  # Mô tả quá ngắn → Warning
    CHAR_REPEAT_LIMIT: int = 4        # Lặp ký tự ≥4 lần → Warning
    PRICE_DEVIATION_PERCENT: float = 50.0  # Lệch >50% khung giá → Cảnh báo giá ảo

    # Image validation settings
    IMAGE_CHECK_ENABLED: bool = True
    IMAGE_DOWNLOAD_TIMEOUT: float = 4.0
    IMAGE_MIN_WIDTH: int = 250
    IMAGE_MIN_HEIGHT: int = 180
    IMAGE_BLUR_VARIANCE_THRESHOLD: float = 35.0
    IMAGE_MIN_BRIGHTNESS: float = 20.0
    IMAGE_MAX_BRIGHTNESS: float = 245.0
    IMAGE_MIN_CONTRAST: float = 10.0

    # External Real-Estate Market Price API settings
    EXTERNAL_PRICE_API_ENABLED: bool = True
    EXTERNAL_PRICE_API_TIMEOUT: float = 4.0
    EXTERNAL_PRICE_CACHE_TTL_SECONDS: int = 21600
    CHOTOT_PUBLIC_API_URL: str = "https://gateway.chotot.com/v1/public/ad-listing"
    BIGGEE_API_URL: str = "https://biggee.vn/api/dinhgianhadat"
    BIGGEE_PARTNER_KEY: str = ""

    # 🆕 Ollama settings
    OLLAMA_BASE_URL: str = "http://localhost:11434"
    OLLAMA_MODEL: str = "qwen2.5:3b"
    OLLAMA_TIMEOUT: float = 30.0       # Timeout (giây) cho mỗi request
    OLLAMA_ENABLED: bool = True        # Tắt/bật LLM analysis

    class Config:
        env_file = ".env"

settings = Settings()
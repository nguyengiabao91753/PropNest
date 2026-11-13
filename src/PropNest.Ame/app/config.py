from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    APP_NAME: str = "PropNest AME"
    PORT: int = 8001
    CAPS_THRESHOLD: float = 0.5       # >50% chữ in hoa → Warning
    MIN_DESCRIPTION_LENGTH: int = 30  # Mô tả quá ngắn → Warning
    CHAR_REPEAT_LIMIT: int = 4        # Lặp ký tự ≥4 lần → Warning
    PRICE_DEVIATION_PERCENT: float = 50.0  # Lệch >50% khung giá → ManualReview

    # 🆕 Ollama settings
    OLLAMA_BASE_URL: str = "http://localhost:11434"
    OLLAMA_MODEL: str = "qwen2.5:3b"
    OLLAMA_TIMEOUT: float = 30.0       # Timeout (giây) cho mỗi request
    OLLAMA_ENABLED: bool = True        # Tắt/bật LLM analysis

    class Config:
        env_file = ".env"

settings = Settings()
import asyncio
import logging
from io import BytesIO
from urllib.parse import urlparse

import httpx
from PIL import Image, ImageFilter as PilImageFilter, ImageStat, UnidentifiedImageError

from app.config import settings
from app.models.responses import RuleViolation

logger = logging.getLogger(__name__)

_LAPLACIAN_KERNEL = PilImageFilter.Kernel(
    size=(3, 3),
    kernel=[0, 1, 0, 1, -4, 1, 0, 1, 0],
    scale=1,
    offset=128,
)
_MAX_IMAGE_BYTES = 5 * 1024 * 1024
_MAX_IMAGES_TO_INSPECT = 4


class ImageFilter:
    """Đánh giá chất lượng ảnh cơ bản (mờ, phân giải thấp, quá tối/lóa, link lỗi).

    Nếu không thể đánh giá hoặc ảnh không đạt, trả về severity='manual_review' để đẩy Admin duyệt.
    """

    @staticmethod
    def analyze_image_bytes(raw_bytes: bytes, source_label: str = "image") -> RuleViolation | None:
        try:
            with Image.open(BytesIO(raw_bytes)) as img:
                width, height = img.size
                if width < settings.IMAGE_MIN_WIDTH or height < settings.IMAGE_MIN_HEIGHT:
                    return RuleViolation(
                        rule="IMAGE_LOW_RESOLUTION",
                        severity="manual_review",
                        message=(
                            f"Ảnh ({source_label}) có độ phân giải quá thấp ({width}x{height}px, "
                            f"tối thiểu {settings.IMAGE_MIN_WIDTH}x{settings.IMAGE_MIN_HEIGHT}px) — cần kiểm duyệt viên xác minh."
                        ),
                    )

                gray = img.convert("L")
                stat = ImageStat.Stat(gray)
                mean_brightness = float(stat.mean[0]) if stat.mean else 128.0
                contrast_stddev = float(stat.stddev[0]) if stat.stddev else 0.0

                if (
                    mean_brightness < settings.IMAGE_MIN_BRIGHTNESS
                    or mean_brightness > settings.IMAGE_MAX_BRIGHTNESS
                    or contrast_stddev < settings.IMAGE_MIN_CONTRAST
                ):
                    return RuleViolation(
                        rule="IMAGE_POOR_EXPOSURE",
                        severity="manual_review",
                        message=(
                            f"Ảnh ({source_label}) quá tối, lóa sáng hoặc thiếu tương phản — "
                            "cần kiểm tra trực tiếp chất lượng hình ảnh."
                        ),
                    )

                # Resize chuẩn để đo độ sắc nét cạnh độc lập với kích thước ảnh gốc
                sample = gray.copy()
                sample.thumbnail((320, 320))
                edges = sample.filter(_LAPLACIAN_KERNEL)
                if edges.width > 8 and edges.height > 8:
                    edges = edges.crop((4, 4, edges.width - 4, edges.height - 4))
                edge_stat = ImageStat.Stat(edges)
                # Phương sai cạnh (variance around offset=128)
                edge_variance = float(edge_stat.var[0]) if edge_stat.var else 0.0

                if edge_variance < settings.IMAGE_BLUR_VARIANCE_THRESHOLD:
                    return RuleViolation(
                        rule="IMAGE_BLURRY",
                        severity="manual_review",
                        message=(
                            f"Ảnh ({source_label}) bị mờ hoặc thiếu độ sắc nét — "
                            "cần kiểm tra trực tiếp."
                        ),
                    )

                return None
        except (UnidentifiedImageError, OSError, ValueError):
            return RuleViolation(
                rule="IMAGE_UNVERIFIABLE",
                severity="manual_review",
                message=f"Không thể đọc định dạng ảnh ({source_label}) — cần kiểm tra trực tiếp.",
            )

    async def _inspect_single_url(self, client: httpx.AsyncClient, url: str, index: int) -> RuleViolation | None:
        label = f"#{index + 1}"
        parsed = urlparse(url.strip())
        if parsed.scheme not in {"http", "https"} or not parsed.netloc:
            return RuleViolation(
                rule="IMAGE_UNVERIFIABLE",
                severity="manual_review",
                message=f"Đường dẫn ảnh {label} không thể truy cập để kiểm tra tự động — cần xem xét trực tiếp.",
            )

        try:
            response = await client.get(url.strip(), follow_redirects=True)
            if response.status_code >= 400:
                return RuleViolation(
                    rule="IMAGE_UNVERIFIABLE",
                    severity="manual_review",
                    message=f"Không thể tải hình ảnh {label} để kiểm tra tự động — cần xem xét trực tiếp.",
                )

            raw = response.content[:_MAX_IMAGE_BYTES]
            if not raw:
                return RuleViolation(
                    rule="IMAGE_UNVERIFIABLE",
                    severity="manual_review",
                    message=f"Hình ảnh {label} không có dữ liệu hợp lệ — cần xem xét trực tiếp.",
                )

            return self.analyze_image_bytes(raw, label)
        except Exception as ex:
            logger.warning("Failed to inspect image URL %s: %s", url, ex)
            return RuleViolation(
                rule="IMAGE_UNVERIFIABLE",
                severity="manual_review",
                message=f"Không thể kiểm tra tự động hình ảnh {label} do gián đoạn kết nối — cần xem xét trực tiếp.",
            )

    async def check(self, images: list[str] | None) -> list[RuleViolation]:
        if not settings.IMAGE_CHECK_ENABLED or images is None:
            return []

        cleaned = [u.strip() for u in images if isinstance(u, str) and u.strip()]
        if len(cleaned) == 0:
            return [
                RuleViolation(
                    rule="IMAGE_MISSING",
                    severity="manual_review",
                    message="Tin đăng chưa có hình ảnh minh họa — cần xem xét trực tiếp.",
                )
            ]

        targets = cleaned[:_MAX_IMAGES_TO_INSPECT]
        async with httpx.AsyncClient(timeout=settings.IMAGE_DOWNLOAD_TIMEOUT) as client:
            results = await asyncio.gather(
                *(self._inspect_single_url(client, url, idx) for idx, url in enumerate(targets))
            )

        return [v for v in results if v is not None]

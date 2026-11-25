import asyncio
from contextlib import asynccontextmanager
from pathlib import Path
import time
from typing import Optional

from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import HTMLResponse, StreamingResponse

from app.data.price_ranges import get_price_benchmark_info
from app.engine.decision_aggregator import DecisionAggregator
from app.engine.external_price_client import ExternalPriceClient
from app.engine.image_filter import ImageFilter
from app.engine.ollama_analyzer import OllamaAnalyzer
from app.engine.pipeline_tracker import PipelineTracker
from app.engine.rule_engine import RuleEngine
from app.models.requests import ModerationRequest
from app.models.responses import Decision, LLMResult, ModerationResponse, RuleViolation

rule_engine = RuleEngine()
image_filter = ImageFilter()
ollama_analyzer = OllamaAnalyzer()
decision_aggregator = DecisionAggregator()
external_price_client = ExternalPriceClient()
pipeline_tracker = PipelineTracker(max_history=150)


@asynccontextmanager
async def _lifespan(_: FastAPI):
    warmup_task = asyncio.create_task(ollama_analyzer.warmup())
    try:
        yield
    finally:
        if not warmup_task.done():
            warmup_task.cancel()


app = FastAPI(
    title="PropNest AME - AI Moderation Engine",
    version="2.3.0",
    docs_url="/docs",
    lifespan=_lifespan,
)

_DASHBOARD_HTML_PATH = Path(__file__).resolve().parent / "ui" / "dashboard.html"


def _load_dashboard_html() -> str:
    return _DASHBOARD_HTML_PATH.read_text(encoding="utf-8")


@app.get("/", response_class=HTMLResponse, include_in_schema=False)
@app.get("/dashboard", response_class=HTMLResponse, include_in_schema=False)
async def monitor_dashboard():
    return HTMLResponse(content=_load_dashboard_html())


@app.get("/api/v1/monitor/jobs")
async def get_monitor_jobs(
    decision: Optional[str] = None,
    search: Optional[str] = None,
    limit: int = 80,
):
    return pipeline_tracker.get_snapshot(decision=decision, search=search, limit=limit)


@app.get("/api/v1/monitor/jobs/{job_id}")
async def get_monitor_job_detail(job_id: str):
    job = pipeline_tracker.get_job(job_id)
    if job is None:
        raise HTTPException(status_code=404, detail="Không tìm thấy phiên kiểm duyệt.")
    return job


@app.delete("/api/v1/monitor/jobs")
async def clear_monitor_jobs():
    removed = pipeline_tracker.clear_completed()
    return {"removed": removed}


@app.get("/api/v1/monitor/stream", include_in_schema=False)
async def stream_monitor_events(http_request: Request):
    queue = pipeline_tracker.subscribe()

    async def event_generator():
        try:
            yield ": connected\n\n"
            while True:
                if await http_request.is_disconnected():
                    break
                try:
                    payload = await asyncio.wait_for(queue.get(), timeout=15.0)
                    yield payload
                except asyncio.TimeoutError:
                    yield ": keep-alive\n\n"
        finally:
            pipeline_tracker.unsubscribe(queue)

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "Connection": "keep-alive",
            "X-Accel-Buffering": "no",
        },
    )


@app.post("/api/v1/moderate", response_model=ModerationResponse)
async def moderate_listing(request: ModerationRequest) -> ModerationResponse:
    started = time.perf_counter()
    location_text = ", ".join(
        part for part in [request.ward, request.district, request.city] if part
    )
    job = pipeline_tracker.start_job(
        listing_id=request.listing_id,
        correlation_id=str(request.correlation_id) if request.correlation_id else None,
        title=request.title,
        location=location_text,
        property_type=request.property_type,
        listing_type=request.listing_type,
        price=request.price,
        area=request.area,
        image_count=len(request.images or []),
    )

    try:
        # Bước 1: Lấy khung giá khu vực từ External Live Market API (có TTL cache, tự fallback khi offline)
        pipeline_tracker.start_step(
            job.job_id,
            "price_benchmark",
            "Đang truy vấn giá bình quân m² theo khu vực...",
        )
        ext_benchmark = await external_price_client.fetch_benchmark(
            city=request.city,
            district=request.district,
            property_type=request.property_type,
            listing_type=request.listing_type,
            ward=request.ward,
            legacy_address=request.legacy_address,
        )
        if ext_benchmark is not None:
            avg_m = ext_benchmark["avg"] / 1_000_000
            source_label = (
                "Dữ liệu thị trường trực tuyến"
                if "chotot" in str(ext_benchmark.get("source", "")).lower()
                else "Dữ liệu định giá khu vực"
            )
            pipeline_tracker.finish_step(
                job.job_id,
                "price_benchmark",
                status="passed",
                detail=(
                    f"{source_label}: TB {avg_m:.1f} tr/m² "
                    f"({ext_benchmark.get('sampleCount', 0)} mẫu · {ext_benchmark['scope']})"
                ),
                meta=ext_benchmark,
            )
        elif request.price_benchmark is not None:
            avg_m = request.price_benchmark.avg_price_per_sqm / 1_000_000
            pipeline_tracker.finish_step(
                job.job_id,
                "price_benchmark",
                status="passed",
                detail=f"Bảng giá khu vực hệ thống: TB {avg_m:.1f} tr/m² ({request.price_benchmark.district_code})",
            )
        else:
            pipeline_tracker.finish_step(
                job.job_id,
                "price_benchmark",
                status="warning",
                detail="Đang sử dụng khung giá tham chiếu nội bộ theo khu vực.",
            )

        # Bước 2: Rule Engine (bao gồm Check Giá khu vực dưới dạng price_warning)
        pipeline_tracker.start_step(
            job.job_id,
            "rule_engine",
            "Đang quét từ cấm, số điện thoại, định dạng và so lệch giá khu vực...",
        )
        rule_result = rule_engine.evaluate(request, external_benchmark=ext_benchmark)
        if rule_result.decision == Decision.REJECTED:
            pipeline_tracker.finish_step(
                job.job_id,
                "rule_engine",
                status="failed",
                detail=rule_result.summary or "Phát hiện vi phạm quy định nội dung (từ cấm / số điện thoại).",
            )
            pipeline_tracker.skip_step(job.job_id, "image_filter", "Dừng sớm do tin đã vi phạm quy định nội dung.")
            pipeline_tracker.skip_step(job.job_id, "ollama_analyzer", "Dừng sớm do tin đã vi phạm quy định nội dung.")
            pipeline_tracker.start_step(job.job_id, "decision_aggregator", "Đang chốt kết quả kiểm duyệt...")
            rule_result.processing_time_ms = int((time.perf_counter() - started) * 1000)
            pipeline_tracker.finish_step(
                job.job_id,
                "decision_aggregator",
                status="failed",
                detail="Kết quả: Tự động từ chối.",
            )
            pipeline_tracker.complete_job(
                job.job_id,
                decision=rule_result.decision.value,
                total_risk_score=rule_result.total_risk_score,
                summary=rule_result.summary,
                reason_codes=rule_result.reason_codes,
                violations=[v.model_dump(by_alias=True) for v in rule_result.violations],
                price_warning_message=rule_result.price_warning_message,
                processing_time_ms=rule_result.processing_time_ms,
            )
            return rule_result

        if rule_result.decision == Decision.MANUAL_REVIEW:
            pipeline_tracker.finish_step(
                job.job_id,
                "rule_engine",
                status="warning",
                detail=rule_result.summary or "Cần kiểm duyệt thủ công về nội dung/định dạng.",
            )
        elif rule_result.price_warning_message:
            pipeline_tracker.finish_step(
                job.job_id,
                "rule_engine",
                status="warning",
                detail=f"Nội dung đạt • Cảnh báo giá ảo: {rule_result.price_warning_message}",
            )
        else:
            pipeline_tracker.finish_step(
                job.job_id,
                "rule_engine",
                status="passed",
                detail="Nội dung hợp lệ & đơn giá nằm trong khung thị trường.",
            )

        # Bước 3 & 4: Chạy song song kiểm tra Hình ảnh + Ollama LLM ngữ nghĩa
        async def _run_image_step() -> list[RuleViolation]:
            img_count = len(request.images or [])
            pipeline_tracker.start_step(
                job.job_id,
                "image_filter",
                f"Đang tải và đo độ nét/sáng của {img_count} ảnh..." if img_count > 0 else "Đang kiểm tra danh sách ảnh...",
            )
            violations = await image_filter.check(request.images)
            if violations:
                pipeline_tracker.finish_step(
                    job.job_id,
                    "image_filter",
                    status="warning",
                    detail="; ".join(v.message for v in violations),
                )
            else:
                pipeline_tracker.finish_step(
                    job.job_id,
                    "image_filter",
                    status="passed",
                    detail=f"Đã xác thực {min(img_count, 4)} ảnh đạt chuẩn độ nét & ánh sáng.",
                )
            return violations

        async def _run_ollama_step() -> LLMResult:
            pipeline_tracker.start_step(
                job.job_id,
                "ollama_analyzer",
                "Đang đánh giá độ tin cậy và chất lượng mô tả...",
            )
            res = await ollama_analyzer.analyze(request)
            if not res.is_available:
                pipeline_tracker.finish_step(
                    job.job_id,
                    "ollama_analyzer",
                    status="warning",
                    detail="Bộ đánh giá ngữ nghĩa tạm gián đoạn → chuyển quản trị viên duyệt.",
                )
            elif res.is_fraudulent:
                pipeline_tracker.finish_step(
                    job.job_id,
                    "ollama_analyzer",
                    status="failed",
                    detail=f"Cảnh báo nghi vấn bất thường: {res.explanation}",
                )
            elif res.quality_score < 0.5:
                pipeline_tracker.finish_step(
                    job.job_id,
                    "ollama_analyzer",
                    status="warning",
                    detail=f"Điểm mô tả thấp ({res.quality_score:.2f}/1.0): {res.explanation}",
                )
            else:
                pipeline_tracker.finish_step(
                    job.job_id,
                    "ollama_analyzer",
                    status="passed",
                    detail=f"Điểm chất lượng {res.quality_score:.2f}/1.0 • {res.explanation}",
                )
            return res

        image_violations, llm_result = await asyncio.gather(
            _run_image_step(),
            _run_ollama_step(),
        )

        # Bước 5: Tổng hợp quyết định (Nội dung & ảnh đạt + giá lệch khu vực -> Approved kèm cảnh báo giá ảo)
        pipeline_tracker.start_step(
            job.job_id,
            "decision_aggregator",
            "Đang tổng hợp kết quả kiểm duyệt cuối cùng...",
        )
        elapsed_ms = int((time.perf_counter() - started) * 1000)
        final_response = decision_aggregator.aggregate(
            listing_id=request.listing_id,
            rule_response=rule_result,
            llm_result=llm_result,
            image_violations=image_violations,
            processing_time_ms=elapsed_ms,
        )

        decision_label = {
            Decision.APPROVED: "Tự động duyệt",
            Decision.MANUAL_REVIEW: "Chuyển quản trị viên duyệt",
            Decision.REJECTED: "Tự động từ chối",
        }.get(final_response.decision, final_response.decision.value)

        agg_status = (
            "passed"
            if final_response.decision == Decision.APPROVED and not final_response.price_warning_message
            else "warning"
            if final_response.decision in {Decision.APPROVED, Decision.MANUAL_REVIEW}
            else "failed"
        )
        pipeline_tracker.finish_step(
            job.job_id,
            "decision_aggregator",
            status=agg_status,
            detail=f"Kết quả: {decision_label} • {final_response.summary}",
        )
        pipeline_tracker.complete_job(
            job.job_id,
            decision=final_response.decision.value,
            total_risk_score=final_response.total_risk_score,
            summary=final_response.summary,
            reason_codes=final_response.reason_codes,
            violations=[v.model_dump(by_alias=True) for v in final_response.violations],
            price_warning_message=final_response.price_warning_message,
            processing_time_ms=elapsed_ms,
        )
        return final_response

    except Exception as ex:
        pipeline_tracker.fail_job(job.job_id, str(ex))
        raise


@app.get("/api/v1/price-benchmark")
async def get_price_benchmark(
    city: str,
    property_type: str = "Apartment",
    listing_type: str = "Sale",
    district: Optional[str] = None,
    ward: Optional[str] = None,
    legacy_address: Optional[str] = None,
):
    ext = await external_price_client.fetch_benchmark(
        city=city,
        district=district,
        property_type=property_type,
        listing_type=listing_type,
        ward=ward,
        legacy_address=legacy_address,
    )
    if ext is not None:
        return {
            "found": True,
            "minPricePerSqm": ext["min"],
            "avgPricePerSqm": ext["avg"],
            "maxPricePerSqm": ext["max"],
            "scope": ext["scope"],
            "source": ext["source"],
            "sampleCount": ext.get("sampleCount", 0),
        }

    info = get_price_benchmark_info(
        city=city,
        district=district,
        property_type=property_type,
        listing_type=listing_type,
        ward=ward,
        legacy_address=legacy_address,
    )
    if info is None:
        return {"found": False}
    return {
        "found": True,
        "minPricePerSqm": info["min"],
        "avgPricePerSqm": info["avg"],
        "maxPricePerSqm": info["max"],
        "scope": info["scope"],
        "source": "LocalFallback",
        "sampleCount": 0,
    }


@app.get("/api/v1/health")
async def health():
    return {"status": "healthy", "service": "ame", "version": "2.3.0"}

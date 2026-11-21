import asyncio
from collections import deque
from dataclasses import dataclass, field
from datetime import datetime, timezone
import json
import time
from typing import Any
from uuid import uuid4


PIPELINE_STEPS: list[tuple[str, str]] = [
    ("price_benchmark", "1. Kiểm tra khung giá khu vực"),
    ("rule_engine", "2. Kiểm tra nội dung & So giá"),
    ("image_filter", "3. Kiểm tra chất lượng ảnh"),
    ("ollama_analyzer", "4. Đánh giá ngữ nghĩa nội dung"),
    ("decision_aggregator", "5. Tổng hợp kết quả duyệt"),
]


def _utc_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


@dataclass
class StepRecord:
    key: str
    label: str
    status: str = "pending"  # pending | running | passed | warning | failed | skipped
    detail: str = ""
    started_at: str | None = None
    finished_at: str | None = None
    duration_ms: int | None = None
    meta: dict[str, Any] = field(default_factory=dict)
    _perf_start: float | None = field(default=None, repr=False)

    def to_dict(self) -> dict[str, Any]:
        return {
            "key": self.key,
            "label": self.label,
            "status": self.status,
            "detail": self.detail,
            "startedAt": self.started_at,
            "finishedAt": self.finished_at,
            "durationMs": self.duration_ms,
            "meta": self.meta,
        }


@dataclass
class ModerationJobRecord:
    job_id: str
    listing_id: int
    correlation_id: str | None
    title: str
    location: str
    property_type: str
    listing_type: str
    price: float
    area: float
    image_count: int
    status: str = "running"  # running | completed | error
    current_step: str = "price_benchmark"
    decision: str | None = None
    has_price_warning: bool = False
    price_warning_message: str | None = None
    total_risk_score: float | None = None
    summary: str = ""
    reason_codes: list[str] = field(default_factory=list)
    violations: list[dict[str, Any]] = field(default_factory=list)
    started_at: str = field(default_factory=_utc_iso)
    finished_at: str | None = None
    processing_time_ms: int | None = None
    steps: dict[str, StepRecord] = field(default_factory=dict)
    _perf_start: float = field(default_factory=time.perf_counter, repr=False)

    def to_dict(self) -> dict[str, Any]:
        ordered_steps = [
            self.steps[key].to_dict()
            for key, _ in PIPELINE_STEPS
            if key in self.steps
        ]
        unit_price = round(self.price / self.area) if self.area > 0 else 0
        return {
            "jobId": self.job_id,
            "listingId": self.listing_id,
            "correlationId": self.correlation_id,
            "title": self.title,
            "location": self.location,
            "propertyType": self.property_type,
            "listingType": self.listing_type,
            "price": self.price,
            "area": self.area,
            "unitPrice": unit_price,
            "imageCount": self.image_count,
            "status": self.status,
            "currentStep": self.current_step,
            "decision": self.decision,
            "hasPriceWarning": self.has_price_warning,
            "priceWarningMessage": self.price_warning_message,
            "totalRiskScore": self.total_risk_score,
            "summary": self.summary,
            "reasonCodes": self.reason_codes,
            "violations": self.violations,
            "startedAt": self.started_at,
            "finishedAt": self.finished_at,
            "processingTimeMs": self.processing_time_ms,
            "steps": ordered_steps,
        }


class PipelineTracker:
    """Theo dõi thời gian thực từng bước xử lý tin của PropNest AME."""

    def __init__(self, max_history: int = 150) -> None:
        self._max_history = max_history
        self._jobs: deque[ModerationJobRecord] = deque(maxlen=max_history)
        self._jobs_by_id: dict[str, ModerationJobRecord] = {}
        self._subscribers: set[asyncio.Queue[str]] = set()

    def start_job(
        self,
        *,
        listing_id: int,
        correlation_id: str | None,
        title: str,
        location: str,
        property_type: str,
        listing_type: str,
        price: float,
        area: float,
        image_count: int,
    ) -> ModerationJobRecord:
        job_id = uuid4().hex[:12]
        steps = {key: StepRecord(key=key, label=label) for key, label in PIPELINE_STEPS}
        record = ModerationJobRecord(
            job_id=job_id,
            listing_id=listing_id,
            correlation_id=correlation_id,
            title=title or f"Tin #{listing_id}",
            location=location,
            property_type=property_type,
            listing_type=listing_type,
            price=float(price),
            area=float(area),
            image_count=image_count,
            steps=steps,
        )

        # Evict oldest index entry if deque reached capacity
        if len(self._jobs) == self._max_history:
            oldest = self._jobs[0]
            self._jobs_by_id.pop(oldest.job_id, None)

        self._jobs.append(record)
        self._jobs_by_id[job_id] = record
        self._broadcast("job_updated", record.to_dict())
        return record

    def start_step(self, job_id: str, step_key: str, detail: str = "") -> None:
        job = self._jobs_by_id.get(job_id)
        if job is None or step_key not in job.steps:
            return
        step = job.steps[step_key]
        step.status = "running"
        step.started_at = _utc_iso()
        step._perf_start = time.perf_counter()
        if detail:
            step.detail = detail
        job.current_step = step_key
        self._broadcast("job_updated", job.to_dict())

    def finish_step(
        self,
        job_id: str,
        step_key: str,
        *,
        status: str,
        detail: str = "",
        meta: dict[str, Any] | None = None,
    ) -> None:
        job = self._jobs_by_id.get(job_id)
        if job is None or step_key not in job.steps:
            return
        step = job.steps[step_key]
        step.status = status
        step.finished_at = _utc_iso()
        if step._perf_start is not None:
            step.duration_ms = int((time.perf_counter() - step._perf_start) * 1000)
        elif step.duration_ms is None:
            step.duration_ms = 0
        if detail:
            step.detail = detail
        if meta:
            step.meta.update(meta)
        self._broadcast("job_updated", job.to_dict())

    def skip_step(self, job_id: str, step_key: str, detail: str = "Bỏ qua do dừng sớm") -> None:
        job = self._jobs_by_id.get(job_id)
        if job is None or step_key not in job.steps:
            return
        step = job.steps[step_key]
        if step.status == "pending":
            step.status = "skipped"
            step.detail = detail
            step.duration_ms = 0
            self._broadcast("job_updated", job.to_dict())

    def complete_job(
        self,
        job_id: str,
        *,
        decision: str,
        total_risk_score: float,
        summary: str,
        reason_codes: list[str],
        violations: list[dict[str, Any]],
        price_warning_message: str | None,
        processing_time_ms: int,
    ) -> None:
        job = self._jobs_by_id.get(job_id)
        if job is None:
            return
        job.status = "completed"
        job.current_step = "done"
        job.decision = decision
        job.total_risk_score = total_risk_score
        job.summary = summary
        job.reason_codes = list(reason_codes)
        job.violations = list(violations)
        job.price_warning_message = price_warning_message
        job.has_price_warning = bool(price_warning_message)
        job.finished_at = _utc_iso()
        job.processing_time_ms = processing_time_ms
        self._broadcast("job_updated", job.to_dict())

    def fail_job(self, job_id: str, error_message: str) -> None:
        job = self._jobs_by_id.get(job_id)
        if job is None:
            return
        job.status = "error"
        job.summary = f"Lỗi hệ thống: {error_message}"
        job.finished_at = _utc_iso()
        job.processing_time_ms = int((time.perf_counter() - job._perf_start) * 1000)
        for step in job.steps.values():
            if step.status == "running":
                step.status = "failed"
                step.detail = error_message
                step.finished_at = job.finished_at
        self._broadcast("job_updated", job.to_dict())

    def get_job(self, job_id: str) -> dict[str, Any] | None:
        job = self._jobs_by_id.get(job_id)
        return job.to_dict() if job is not None else None

    def get_snapshot(
        self,
        *,
        decision: str | None = None,
        search: str | None = None,
        limit: int = 80,
    ) -> dict[str, Any]:
        all_jobs = list(reversed(self._jobs))

        running_count = sum(1 for j in all_jobs if j.status == "running")
        approved_clean = sum(
            1 for j in all_jobs if j.decision == "Approved" and not j.has_price_warning
        )
        approved_price_warning = sum(
            1 for j in all_jobs if j.decision == "Approved" and j.has_price_warning
        )
        manual_review_count = sum(1 for j in all_jobs if j.decision == "ManualReview")
        rejected_count = sum(1 for j in all_jobs if j.decision == "Rejected")
        completed_times = [
            j.processing_time_ms
            for j in all_jobs
            if j.status == "completed" and j.processing_time_ms is not None
        ]
        avg_ms = round(sum(completed_times) / len(completed_times)) if completed_times else 0

        filtered = all_jobs
        if decision and decision.lower() != "all":
            d_low = decision.lower()
            if d_low == "running":
                filtered = [j for j in filtered if j.status == "running"]
            elif d_low == "price_warning":
                filtered = [j for j in filtered if j.decision == "Approved" and j.has_price_warning]
            else:
                filtered = [j for j in filtered if (j.decision or "").lower() == d_low]

        if search and search.strip():
            q = search.strip().lower()
            filtered = [
                j
                for j in filtered
                if q in str(j.listing_id)
                or q in j.title.lower()
                or q in j.location.lower()
                or (j.correlation_id and q in j.correlation_id.lower())
            ]

        return {
            "stats": {
                "total": len(all_jobs),
                "running": running_count,
                "approvedClean": approved_clean,
                "approvedPriceWarning": approved_price_warning,
                "manualReview": manual_review_count,
                "rejected": rejected_count,
                "avgProcessingMs": avg_ms,
            },
            "jobs": [j.to_dict() for j in filtered[: max(1, min(limit, self._max_history))]],
        }

    def clear_completed(self) -> int:
        active = [j for j in self._jobs if j.status == "running"]
        removed = len(self._jobs) - len(active)
        self._jobs.clear()
        self._jobs_by_id.clear()
        for j in active:
            self._jobs.append(j)
            self._jobs_by_id[j.job_id] = j
        self._broadcast("cleared", {"removed": removed})
        return removed

    def subscribe(self) -> asyncio.Queue[str]:
        queue: asyncio.Queue[str] = asyncio.Queue(maxsize=100)
        self._subscribers.add(queue)
        return queue

    def unsubscribe(self, queue: asyncio.Queue[str]) -> None:
        self._subscribers.discard(queue)

    def _broadcast(self, event_name: str, data: dict[str, Any]) -> None:
        if not self._subscribers:
            return
        payload = f"event: {event_name}\ndata: {json.dumps(data, ensure_ascii=False)}\n\n"
        stale: list[asyncio.Queue[str]] = []
        for q in self._subscribers:
            try:
                q.put_nowait(payload)
            except asyncio.QueueFull:
                stale.append(q)
        for q in stale:
            self._subscribers.discard(q)

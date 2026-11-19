import asyncio
import unittest
from unittest.mock import AsyncMock, patch

from fastapi.testclient import TestClient

from app.engine.pipeline_tracker import PipelineTracker
from app.main import app, pipeline_tracker
from app.models.responses import LLMResult


class PipelineMonitorTests(unittest.TestCase):
    def setUp(self):
        pipeline_tracker.clear_completed()
        self.client = TestClient(app)

    def test_dashboard_ui_served_on_root_and_dashboard(self):
        for route in ("/", "/dashboard"):
            resp = self.client.get(route)
            self.assertEqual(resp.status_code, 200)
            self.assertIn("text/html", resp.headers["content-type"])
            self.assertIn("PropNest", resp.text)
            self.assertNotIn("Ollama", resp.text)
            self.assertIn("/api/v1/monitor/stream", resp.text)

    def test_tracker_records_all_steps_for_approved_with_price_warning(self):
        with (
            patch(
                "app.main.external_price_client.fetch_benchmark",
                new=AsyncMock(
                    return_value={
                        "min": 45_000_000.0,
                        "avg": 65_000_000.0,
                        "max": 95_000_000.0,
                        "scope": "Quận 7, TP.HCM",
                        "source": "ChototPublicApi",
                        "sampleCount": 25,
                    }
                ),
            ),
            patch("app.main.image_filter.check", new=AsyncMock(return_value=[])),
            patch(
                "app.main.ollama_analyzer.analyze",
                new=AsyncMock(
                    return_value=LLMResult(
                        quality_score=0.9,
                        is_fraudulent=False,
                        category="apartment",
                        explanation="Mô tả đầy đủ.",
                        is_available=True,
                    )
                ),
            ),
        ):
            resp = self.client.post(
                "/api/v1/moderate",
                json={
                    "listingId": 777,
                    "title": "Bán căn hộ 2PN Quận 7 giá cực rẻ",
                    "description": "Căn hộ 2 phòng ngủ đầy đủ nội thất cao cấp, sổ hồng chính chủ, hồ bơi và công viên nội khu.",
                    "propertyType": "Apartment",
                    "listingType": "Sale",
                    "price": 300_000_000,
                    "area": 75.0,
                    "city": "Thành phố Hồ Chí Minh",
                    "district": "Quận 7",
                    "address": "10 Nguyễn Văn Linh",
                    "images": ["https://example.com/a.jpg"],
                },
            )
            self.assertEqual(resp.status_code, 200)
            body = resp.json()
            self.assertEqual(body["decision"], "Approved")
            self.assertIsNotNone(body["priceWarningMessage"])

        snap = self.client.get("/api/v1/monitor/jobs").json()
        self.assertEqual(snap["stats"]["total"], 1)
        self.assertEqual(snap["stats"]["approvedPriceWarning"], 1)
        job = snap["jobs"][0]
        self.assertEqual(job["listingId"], 777)
        self.assertEqual(job["status"], "completed")
        self.assertEqual(len(job["steps"]), 5)
        step_map = {s["key"]: s for s in job["steps"]}
        self.assertEqual(step_map["price_benchmark"]["status"], "passed")
        self.assertEqual(step_map["rule_engine"]["status"], "warning")
        self.assertEqual(step_map["image_filter"]["status"], "passed")
        self.assertEqual(step_map["ollama_analyzer"]["status"], "passed")
        self.assertEqual(step_map["decision_aggregator"]["status"], "warning")

    def test_short_circuit_rejected_skips_image_and_ollama_steps(self):
        with patch(
            "app.main.external_price_client.fetch_benchmark",
            new=AsyncMock(return_value=None),
        ):
            resp = self.client.post(
                "/api/v1/moderate",
                json={
                    "listingId": 888,
                    "title": "Bán căn hộ Quận 7",
                    "description": "Căn hộ chính chủ cần bán gấp liên hệ ngay 0909.123.456 để xem nhà trực tiếp.",
                    "propertyType": "Apartment",
                    "listingType": "Sale",
                    "price": 4_500_000_000,
                    "area": 75.0,
                    "city": "Thành phố Hồ Chí Minh",
                    "district": "Quận 7",
                    "address": "10 Nguyễn Văn Linh",
                },
            )
            self.assertEqual(resp.status_code, 200)
            self.assertEqual(resp.json()["decision"], "Rejected")

        snap = self.client.get("/api/v1/monitor/jobs?decision=rejected").json()
        self.assertEqual(len(snap["jobs"]), 1)
        step_map = {s["key"]: s for s in snap["jobs"][0]["steps"]}
        self.assertEqual(step_map["rule_engine"]["status"], "failed")
        self.assertEqual(step_map["image_filter"]["status"], "skipped")
        self.assertEqual(step_map["ollama_analyzer"]["status"], "skipped")

    def test_sse_subscriber_receives_live_events(self):
        tracker = PipelineTracker(max_history=10)
        queue = tracker.subscribe()
        job = tracker.start_job(
            listing_id=101,
            correlation_id="abc",
            title="Test",
            location="Quận 7, TP.HCM",
            property_type="Apartment",
            listing_type="Sale",
            price=4_000_000_000,
            area=70.0,
            image_count=1,
        )
        tracker.start_step(job.job_id, "price_benchmark", "Running")
        ev1 = asyncio.run(queue.get())
        ev2 = asyncio.run(queue.get())
        self.assertIn("event: job_updated", ev1)
        self.assertIn('"listingId": 101', ev2)
        tracker.unsubscribe(queue)


if __name__ == "__main__":
    unittest.main()

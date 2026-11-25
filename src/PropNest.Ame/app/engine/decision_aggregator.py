from app.models.responses import (
    Decision,
    LLMResult,
    ModerationResponse,
    RuleViolation,
)


class DecisionAggregator:
    """Kết hợp kết quả từ RuleEngine, ImageFilter và OllamaAnalyzer.

    Nguyên tắc:
    1. Vi phạm nghiêm trọng (badword / SĐT) -> Rejected ngay lập tức.
    2. Lỗi kiểm tra ảnh (mờ / phân giải thấp / thiếu / lỗi tải) hoặc nội dung/LLM không đạt hoặc Ollama offline
       -> ManualReview (chỉ lúc này mới đẩy vào hàng đợi duyệt của Admin).
    3. Nội dung & ảnh hợp lệ nhưng lệch khung giá khu vực (price_warning)
       -> Vẫn Approved kèm cảnh báo 'Giá tin này có thể là giá ảo'.
    """

    def aggregate(
        self,
        listing_id: int,
        rule_response: ModerationResponse,
        llm_result: LLMResult | None,
        image_violations: list[RuleViolation] | None = None,
        processing_time_ms: int = 0,
    ) -> ModerationResponse:
        img_violations = list(image_violations or [])
        all_violations = list(rule_response.violations) + img_violations

        price_warnings = [v for v in all_violations if v.severity == "price_warning"]
        price_anomaly_score = 80.0 if price_warnings else 5.0
        price_warning_msg = price_warnings[0].message if price_warnings else None

        # ─── 1. Vi phạm luật cứng (Tầng 1) → Rejected ngay ───
        if rule_response.decision == Decision.REJECTED:
            critical = [v for v in all_violations if v.severity == "critical"]
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.REJECTED,
                source="AI-Aggregated",
                violations=all_violations,
                score=0.0,
                total_risk_score=100.0,
                keyword_risk_score=100.0,
                price_anomaly_score=price_anomaly_score if price_warnings else 0.0,
                completeness_score=40.0,
                duplicate_score=0.0,
                ai_semantic_score=None,
                summary="; ".join(v.message for v in critical) or "Vi phạm quy tắc nghiêm trọng.",
                processing_time_ms=processing_time_ms,
                reason_codes=[v.rule for v in all_violations],
                price_warning_message=price_warning_msg,
                llm_result=llm_result,
            )

        # ─── 2. Kiểm tra ảnh thất bại / không đánh giá được → ManualReview (Admin) ───
        if img_violations:
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.MANUAL_REVIEW,
                source="AI-Aggregated",
                violations=all_violations,
                score=max(0.1, rule_response.score * 0.6),
                total_risk_score=65.0,
                keyword_risk_score=0.0,
                price_anomaly_score=price_anomaly_score,
                completeness_score=70.0,
                ai_semantic_score=round((1.0 - llm_result.quality_score) * 100.0, 1) if llm_result and llm_result.is_available else None,
                summary="; ".join(v.message for v in img_violations),
                processing_time_ms=processing_time_ms,
                reason_codes=[v.rule for v in all_violations],
                price_warning_message=price_warning_msg,
                llm_result=llm_result,
            )

        # ─── 3. Rule Engine cảnh báo nội dung/định dạng (không phải giá) → ManualReview ───
        if rule_response.decision == Decision.MANUAL_REVIEW:
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.MANUAL_REVIEW,
                source="AI-Aggregated",
                violations=all_violations,
                score=rule_response.score,
                total_risk_score=max(55.0, rule_response.total_risk_score),
                keyword_risk_score=0.0,
                price_anomaly_score=price_anomaly_score,
                completeness_score=round(rule_response.score * 100.0, 1),
                ai_semantic_score=round((1.0 - llm_result.quality_score) * 100.0, 1) if llm_result and llm_result.is_available else None,
                summary="; ".join(v.message for v in all_violations),
                processing_time_ms=processing_time_ms,
                reason_codes=[v.rule for v in all_violations],
                price_warning_message=price_warning_msg,
                llm_result=llm_result,
            )

        # ─── 4. Dịch vụ phân tích ngữ nghĩa không khả dụng → Fallback ManualReview ───
        if llm_result is None or not llm_result.is_available:
            fallback_violation = RuleViolation(
                rule="LLM_UNAVAILABLE_FALLBACK_MANUAL",
                severity="manual_review",
                message="Hệ thống đánh giá tự động tạm thời bận — tin đăng được chuyển sang kiểm duyệt thủ công.",
            )
            all_violations.append(fallback_violation)
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.MANUAL_REVIEW,
                source="AI-Aggregated",
                violations=all_violations,
                score=round(rule_response.score * 0.7, 2),
                total_risk_score=55.0,
                keyword_risk_score=0.0,
                price_anomaly_score=price_anomaly_score,
                completeness_score=85.0,
                ai_semantic_score=None,
                summary=fallback_violation.message,
                processing_time_ms=processing_time_ms,
                reason_codes=[v.rule for v in all_violations],
                price_warning_message=price_warning_msg,
                llm_result=llm_result,
            )

        # ─── 5. Phát hiện dấu hiệu lừa đảo → ManualReview ───
        if llm_result.is_fraudulent:
            fraud_detail = (llm_result.explanation or "").strip()
            fraud_message = (
                f"Phát hiện dấu hiệu nội dung bất thường: {fraud_detail}"
                if fraud_detail
                else "Phát hiện dấu hiệu nội dung bất thường — cần kiểm duyệt thủ công."
            )
            fraud_violation = RuleViolation(
                rule="LLM_FRAUD_DETECTED",
                severity="manual_review",
                message=fraud_message,
            )
            all_violations.append(fraud_violation)
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.MANUAL_REVIEW,
                source="AI-Aggregated",
                violations=all_violations,
                score=round(max(0.0, llm_result.quality_score * 0.3), 2),
                total_risk_score=85.0,
                keyword_risk_score=0.0,
                price_anomaly_score=price_anomaly_score,
                completeness_score=round(llm_result.quality_score * 100.0, 1),
                ai_semantic_score=90.0,
                summary=fraud_violation.message,
                processing_time_ms=processing_time_ms,
                reason_codes=[v.rule for v in all_violations],
                price_warning_message=price_warning_msg,
                llm_result=llm_result,
            )

        # ─── 6. Đánh giá nội dung quá sơ sài → ManualReview ───
        if llm_result.quality_score < 0.5:
            low_q_detail = (llm_result.explanation or "").strip()
            low_q_message = (
                f"Nội dung mô tả còn sơ sài: {low_q_detail}"
                if low_q_detail
                else "Nội dung mô tả còn sơ sài, thiếu thông tin chi tiết — cần kiểm duyệt thủ công."
            )
            low_q_violation = RuleViolation(
                rule="LLM_LOW_QUALITY",
                severity="manual_review",
                message=low_q_message,
            )
            all_violations.append(low_q_violation)
            combined_score = (rule_response.score * 0.4) + (llm_result.quality_score * 0.6)
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.MANUAL_REVIEW,
                source="AI-Aggregated",
                violations=all_violations,
                score=round(combined_score, 2),
                total_risk_score=60.0,
                keyword_risk_score=0.0,
                price_anomaly_score=price_anomaly_score,
                completeness_score=round(llm_result.quality_score * 100.0, 1),
                ai_semantic_score=round((1.0 - llm_result.quality_score) * 100.0, 1),
                summary=low_q_violation.message,
                processing_time_ms=processing_time_ms,
                reason_codes=[v.rule for v in all_violations],
                price_warning_message=price_warning_msg,
                llm_result=llm_result,
            )

        # ─── 7. Thông tin, ảnh & LLM đều hợp lệ → Approved (kèm cảnh báo giá ảo nếu có) ───
        combined_score = (rule_response.score * 0.4) + (llm_result.quality_score * 0.6)
        semantic_risk = round((1.0 - llm_result.quality_score) * 100.0, 1)
        completeness = round(llm_result.quality_score * 100.0, 1)

        if price_warnings:
            total_risk = round(max(25.0, min(55.0, price_anomaly_score * 0.45 + semantic_risk * 0.25)), 1)
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.APPROVED,
                source="AI-Aggregated",
                violations=price_warnings,
                score=round(combined_score, 2),
                total_risk_score=total_risk,
                keyword_risk_score=0.0,
                price_anomaly_score=price_anomaly_score,
                completeness_score=completeness,
                duplicate_score=0.0,
                ai_semantic_score=semantic_risk,
                summary=f"Thông tin & hình ảnh hợp lệ (tự động duyệt). Cảnh báo: {price_warning_msg}",
                processing_time_ms=processing_time_ms,
                reason_codes=[v.rule for v in price_warnings] + ["PASS_CONTENT_AND_IMAGE"],
                price_warning_message=price_warning_msg,
                llm_result=llm_result,
            )

        total_risk = round(semantic_risk * 0.4, 1)
        return ModerationResponse(
            listing_id=listing_id,
            decision=Decision.APPROVED,
            source="AI-Aggregated",
            violations=[],
            score=round(combined_score, 2),
            total_risk_score=total_risk,
            keyword_risk_score=0.0,
            price_anomaly_score=0.0,
            completeness_score=completeness,
            duplicate_score=0.0,
            ai_semantic_score=semantic_risk,
            summary="Tin đăng đạt chuẩn nội dung, hình ảnh và khung giá khu vực.",
            processing_time_ms=processing_time_ms,
            reason_codes=["PASS_ALL_RULES", "PRICE_WITHIN_BENCHMARK"],
            price_warning_message=None,
            llm_result=llm_result,
        )

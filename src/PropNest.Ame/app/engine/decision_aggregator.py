from app.models.responses import (
    ModerationResponse, Decision, RuleViolation, LLMResult
)


class DecisionAggregator:
    """
    Kết hợp kết quả từ RuleEngine và OllamaAnalyzer.
    Ưu tiên: Rule cứng > LLM analysis > Fallback an toàn.
    """

    def aggregate(
        self,
        listing_id: int,
        rule_response: ModerationResponse,
        llm_result: LLMResult | None
    ) -> ModerationResponse:
        violations = list(rule_response.violations)

        # ─── 1. Vi phạm luật cứng (Tầng 1) → Rejected ngay ───
        if rule_response.decision == Decision.REJECTED:
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.REJECTED,
                source="AI-Aggregated",
                violations=violations,
                score=0.0,
                llm_result=llm_result
            )

        # ─── 2. Ollama không khả dụng → Fallback ManualReview ───
        if llm_result is None or not llm_result.is_available:
            violations.append(RuleViolation(
                rule="LLM_UNAVAILABLE_FALLBACK_MANUAL",
                severity="info",
                message="LLM không khả dụng, chuyển sang kiểm duyệt thủ công"
            ))

            # Nếu Rule Engine đã Approved nhưng LLM offline → ManualReview
            # Nếu Rule Engine đã ManualReview → giữ ManualReview
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.MANUAL_REVIEW,
                source="AI-Aggregated",
                violations=violations,
                score=rule_response.score * 0.7,
                llm_result=llm_result
            )

        # ─── 3. LLM phát hiện lừa đảo → ManualReview ───
        if llm_result.is_fraudulent:
            violations.append(RuleViolation(
                rule="LLM_FRAUD_DETECTED",
                severity="warning",
                message=f"LLM phát hiện dấu hiệu lừa đảo: {llm_result.explanation}"
            ))
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.MANUAL_REVIEW,
                source="AI-Aggregated",
                violations=violations,
                score=max(0.0, llm_result.quality_score * 0.3),
                llm_result=llm_result
            )

        # ─── 4. Kết hợp điểm Rule + LLM ───
        combined_score = (rule_response.score * 0.4) + (llm_result.quality_score * 0.6)

        # Rule Engine có warning + LLM điểm thấp → ManualReview
        if rule_response.decision == Decision.MANUAL_REVIEW:
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.MANUAL_REVIEW,
                source="AI-Aggregated",
                violations=violations,
                score=combined_score,
                llm_result=llm_result
            )

        # LLM điểm chất lượng thấp → ManualReview
        if llm_result.quality_score < 0.5:
            violations.append(RuleViolation(
                rule="LLM_LOW_QUALITY",
                severity="warning",
                message=f"LLM đánh giá chất lượng nội dung thấp ({llm_result.quality_score:.2f}): {llm_result.explanation}"
            ))
            return ModerationResponse(
                listing_id=listing_id,
                decision=Decision.MANUAL_REVIEW,
                source="AI-Aggregated",
                violations=violations,
                score=combined_score,
                llm_result=llm_result
            )

        # ─── 5. Tất cả OK → Approved ───
        return ModerationResponse(
            listing_id=listing_id,
            decision=Decision.APPROVED,
            source="AI-Aggregated",
            violations=[],
            score=combined_score,
            llm_result=llm_result
        )
namespace PropNest.Api.Contracts.Moderation
{
    public sealed record AmeResultDto(
    string Decision,         // "Approved" | "Rejected" | "ManualReview"
    string Source,           // "AI-Aggregated" | "AI-RuleEngine"
    double Score,            // 0.0 → 1.0
    List<AmeViolationDto> Violations,
    AmeLlmResultDto? LlmResult
);
    public sealed record AmeViolationDto(
        string Rule,             // "BADWORD_DETECTED", "PHONE_EXPOSURE", ...
        string Severity,         // "critical", "warning", "info"
        string Message
    );
    public sealed record AmeLlmResultDto(
        double QualityScore,
        bool IsFraudulent,
        string Category,
        string Explanation,
        bool IsAvailable
    );
}

namespace PropNest.Domain.Workflows;

public sealed class WorkflowStep
{
    private WorkflowStep()
    {
    }

    public WorkflowStep(Guid correlationId, string stepName, int attempt = 1)
    {
        CorrelationId = correlationId;
        StepName = stepName;
        Status = "Started";
        Attempt = attempt <= 0 ? 1 : attempt;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public long StepId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string StepName { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int Attempt { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public string? Error { get; private set; }

    public void Complete()
    {
        Status = "Completed";
        FinishedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string error)
    {
        Status = "Failed";
        Error = error;
        FinishedAt = DateTimeOffset.UtcNow;
        if (Attempt <= 0)
        {
            Attempt = 1;
        }
    }
}

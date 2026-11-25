namespace PropNest.Application.Abstractions;

public sealed class MeilisearchOptions
{
    public const string SectionName = "Meilisearch";

    public string Url { get; init; } = "http://localhost:7700";
    public string MasterKey { get; init; } = "PropNest_Meili_Dev_MasterKey_2026";
    public string ListingsIndexName { get; init; } = "listings";
    public bool Enabled { get; init; } = true;
    public int RequestTimeoutMs { get; init; } = 2000;
    public int ReconciliationIntervalMinutes { get; init; } = 15;
}

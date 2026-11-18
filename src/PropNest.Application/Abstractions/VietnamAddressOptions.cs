using System.Diagnostics.CodeAnalysis;

namespace PropNest.Application.Abstractions;

public sealed class VietnamAddressOptions
{
    public const string SectionName = "VietnamAddress";

    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Bound directly from configuration appsettings.json.")]
    public string BaseUrl { get; init; } = "https://provinces.open-api.vn";

    public int TimeoutSeconds { get; init; } = 15;

    public int CacheHours { get; init; } = 24;
}

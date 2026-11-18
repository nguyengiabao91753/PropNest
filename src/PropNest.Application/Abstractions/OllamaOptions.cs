using System.Diagnostics.CodeAnalysis;

namespace PropNest.Application.Abstractions;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Bound directly from configuration appsettings.json.")]
    public string BaseUrl { get; init; } = "http://localhost:11434";

    public string Model { get; init; } = "qwen2.5:3b";

    public int TimeoutSeconds { get; init; } = 25;

    public bool Enabled { get; init; } = true;
}

using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;

namespace PropNest.Infrastructure.Services;

public sealed partial class OllamaListingDescriptionGenerator : IListingDescriptionGenerator
{
    private const string GenerateEndpoint = "/api/generate";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaListingDescriptionGenerator> _logger;

    public OllamaListingDescriptionGenerator(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaListingDescriptionGenerator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ListingDescriptionSuggestionResult> GenerateDescriptionAsync(
        SuggestListingDescriptionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_options.Enabled)
        {
            LogOllamaDisabled(_logger);
            return new ListingDescriptionSuggestionResult(
                BuildFallbackSeoTitle(command),
                BuildFallbackHtml(command),
                "SmartTemplateFallback",
                null);
        }

        var prompt = BuildPrompt(command);
        var payload = new OllamaGenerateRequest(
            Model: _options.Model,
            Prompt: prompt,
            Stream: false,
            Format: "json");

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(GenerateEndpoint, payload, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogOllamaHttpFailure(_logger, (int)response.StatusCode);
                return new ListingDescriptionSuggestionResult(
                    BuildFallbackSeoTitle(command),
                    BuildFallbackHtml(command),
                    "SmartTemplateFallback",
                    null);
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(JsonOptions, cancellationToken);
            var generatedText = CleanModelOutput(result?.Response);

            if (string.IsNullOrWhiteSpace(generatedText))
            {
                LogOllamaEmptyResponse(_logger);
                return new ListingDescriptionSuggestionResult(
                    BuildFallbackSeoTitle(command),
                    BuildFallbackHtml(command),
                    "SmartTemplateFallback",
                    null);
            }

            var (parsedTitle, parsedDesc) = TryExtractStructuredSuggestion(generatedText);
            var finalTitle = string.IsNullOrWhiteSpace(parsedTitle)
                ? BuildFallbackSeoTitle(command)
                : NormalizeSeoTitle(parsedTitle);

            var finalDesc = string.IsNullOrWhiteSpace(parsedDesc)
                ? BuildFallbackHtml(command)
                : FormatAsQuillHtml(parsedDesc);

            return new ListingDescriptionSuggestionResult(
                finalTitle,
                finalDesc,
                "Ollama",
                _options.Model);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            LogOllamaException(_logger, ex);
            return new ListingDescriptionSuggestionResult(
                BuildFallbackSeoTitle(command),
                BuildFallbackHtml(command),
                "SmartTemplateFallback",
                null);
        }
    }

    private static string BuildPrompt(SuggestListingDescriptionCommand command)
    {
        var propLabel = MapPropertyTypeLabel(command.PropertyType);
        var listingLabel = string.Equals(command.ListingType, "rent", StringComparison.OrdinalIgnoreCase)
            ? "Cho thuê"
            : "Cần bán";

        var locationParts = new[] { command.Address, command.Ward, command.District, command.City }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var fullLocation = string.Join(", ", locationParts);

        var sb = new StringBuilder();
        sb.AppendLine("Bạn là chuyên viên copywriter bất động sản và chuyên gia SEO tại Việt Nam.");
        sb.AppendLine("Hãy tạo đồng thời TIÊU ĐỀ chuẩn SEO và MÔ TẢ CHI TIẾT bằng tiếng Việt dựa trên thông tin sau:");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Hình thức giao dịch: {listingLabel}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Loại hình: {propLabel}");
        if (!string.IsNullOrWhiteSpace(command.Title))
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Tiêu đề gợi ý ban đầu: {command.Title}");
        if (!string.IsNullOrWhiteSpace(fullLocation))
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Địa chỉ hiện tại: {fullLocation}");
        if (!string.IsNullOrWhiteSpace(command.LegacyAddress))
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Địa chỉ trước sáp nhập: {command.LegacyAddress}");
        if (command.Area is > 0)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Diện tích: {command.Area.Value:0.##} m²");
        if (command.Price is > 0)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Mức giá: {FormatPriceText(command.Price.Value, command.ListingType)}");
        if (command.Floors is > 0)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Số tầng: {command.Floors.Value} tầng");
        if (command.Bedrooms.HasValue)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Số phòng ngủ: {command.Bedrooms.Value} phòng");
        if (command.Bathrooms.HasValue)
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Số toilet/phòng vệ sinh: {command.Bathrooms.Value} phòng");
        if (!string.IsNullOrWhiteSpace(command.BalconyDirection))
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Ban công: {command.BalconyDirection}");

        sb.AppendLine();
        sb.AppendLine("Trả về DUY NHẤT một JSON object hợp lệ có cấu trúc:");
        sb.AppendLine("{\"title\": \"...\", \"description\": \"...\"}");
        sb.AppendLine("Yêu cầu bắt buộc:");
        sb.AppendLine("1. Trường \"title\": Tiêu đề chuẩn SEO từ 50 đến 85 ký tự, súc tích, không dài dòng, không viết hoa toàn bộ. Gồm: [Bán/Cho thuê] + [Loại BĐS] + [Thông số chính: diện tích/PN/tầng] + [Khu vực] + [Giá nếu gọn].");
        sb.AppendLine("2. Trường \"description\": Mô tả hấp dẫn, trung thực, chia 2-3 đoạn ngắn hoặc gạch đầu dòng (- ) nêu bật vị trí, thông số, tiện ích.");
        sb.AppendLine("3. TUYỆT ĐỐI KHÔNG tự bịa số điện thoại, email, link website hay từ khóa vi phạm pháp luật.");
        return sb.ToString();
    }

    private static (string? Title, string? Description) TryExtractStructuredSuggestion(string rawText)
    {
        try
        {
            var startIdx = rawText.IndexOf('{');
            var endIdx = rawText.LastIndexOf('}');
            if (startIdx >= 0 && endIdx > startIdx)
            {
                var jsonCandidate = rawText[startIdx..(endIdx + 1)];
                using var doc = JsonDocument.Parse(jsonCandidate);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var title = doc.RootElement.TryGetProperty("title", out var tProp) && tProp.ValueKind == JsonValueKind.String
                        ? tProp.GetString()
                        : null;
                    var desc = doc.RootElement.TryGetProperty("description", out var dProp) && dProp.ValueKind == JsonValueKind.String
                        ? dProp.GetString()
                        : null;

                    if (!string.IsNullOrWhiteSpace(desc) || !string.IsNullOrWhiteSpace(title))
                    {
                        return (title, desc);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Fallback to raw text as description
        }

        return (null, rawText);
    }

    private static string BuildFallbackSeoTitle(SuggestListingDescriptionCommand command)
    {
        var isRent = string.Equals(command.ListingType, "rent", StringComparison.OrdinalIgnoreCase);
        var action = isRent ? "Cho thuê" : "Bán";
        var propShort = MapShortPropertyTypeLabel(command.PropertyType);

        var specParts = new List<string>();
        if (command.Bedrooms is > 0)
            specParts.Add($"{command.Bedrooms.Value}PN");
        else if (command.Floors is > 1)
            specParts.Add($"{command.Floors.Value} tầng");

        if (command.Area is > 0)
            specParts.Add($"{command.Area.Value:0.##}m²");

        var areaOrSpecs = string.Join(" ", specParts);

        var locationParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(command.Ward))
            locationParts.Add(ShortenLocationName(command.Ward));
        if (!string.IsNullOrWhiteSpace(command.District))
            locationParts.Add(ShortenLocationName(command.District));
        if (!string.IsNullOrWhiteSpace(command.City))
            locationParts.Add(ShortenCityName(command.City));

        var locationText = string.Join(", ", locationParts.Take(2));

        var sb = new StringBuilder();
        sb.Append(action).Append(' ').Append(propShort);
        if (!string.IsNullOrWhiteSpace(areaOrSpecs))
            sb.Append(' ').Append(areaOrSpecs);
        if (!string.IsNullOrWhiteSpace(locationText))
            sb.Append(" tại ").Append(locationText);
        if (command.Price is > 0)
            sb.Append(" - ").Append(FormatCompactPriceForTitle(command.Price.Value, command.ListingType));

        return NormalizeSeoTitle(sb.ToString());
    }

    private static string NormalizeSeoTitle(string rawTitle)
    {
        var clean = string.Join(' ', rawTitle
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim()
            .Trim('"')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (clean.Length > 100)
        {
            clean = clean[..97].TrimEnd() + "...";
        }

        return clean;
    }

    private static string BuildFallbackHtml(SuggestListingDescriptionCommand command)
    {
        var propLabel = MapPropertyTypeLabel(command.PropertyType);
        var isRent = string.Equals(command.ListingType, "rent", StringComparison.OrdinalIgnoreCase);
        var actionLabel = isRent ? "Cho thuê" : "Chính chủ cần bán";

        var addressLine = string.Join(", ",
            new[] { command.Address, command.Ward, command.District, command.City }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        var sb = new StringBuilder();
        sb.Append("<p><strong>")
          .Append(EscapeHtml($"{actionLabel} {propLabel}"))
          .Append(!string.IsNullOrWhiteSpace(command.City) ? EscapeHtml($" tại {command.City}") : string.Empty)
          .Append("</strong></p>");

        sb.Append("<p><strong>Thông tin nổi bật:</strong></p><ul>");

        if (!string.IsNullOrWhiteSpace(addressLine))
        {
            sb.Append("<li><strong>Vị trí:</strong> ").Append(EscapeHtml(addressLine)).Append("</li>");
        }

        if (!string.IsNullOrWhiteSpace(command.LegacyAddress))
        {
            sb.Append("<li><strong>Địa chỉ trước sáp nhập:</strong> ").Append(EscapeHtml(command.LegacyAddress)).Append("</li>");
        }

        if (command.Area is > 0)
        {
            sb.Append("<li><strong>Diện tích sử dụng:</strong> ")
              .Append(EscapeHtml($"{command.Area.Value:0.##} m²"))
              .Append("</li>");
        }

        if (command.Price is > 0)
        {
            sb.Append("<li><strong>Giá đề xuất:</strong> ")
              .Append(EscapeHtml(FormatPriceText(command.Price.Value, command.ListingType)))
              .Append("</li>");
        }

        if (command.Floors is > 0)
        {
            sb.Append("<li><strong>Kết cấu:</strong> ").Append(command.Floors.Value).Append(" tầng</li>");
        }

        if (command.Bedrooms.HasValue || command.Bathrooms.HasValue)
        {
            var rooms = new List<string>();
            if (command.Bedrooms.HasValue) rooms.Add($"{command.Bedrooms.Value} phòng ngủ");
            if (command.Bathrooms.HasValue) rooms.Add($"{command.Bathrooms.Value} toilet/WC");
            sb.Append("<li><strong>Công năng:</strong> ").Append(EscapeHtml(string.Join(", ", rooms))).Append("</li>");
        }

        if (!string.IsNullOrWhiteSpace(command.BalconyDirection))
        {
            var balconyText = string.Equals(command.BalconyDirection, "Không có ban công", StringComparison.OrdinalIgnoreCase)
                ? "Không có ban công"
                : $"Hướng {command.BalconyDirection}";
            sb.Append("<li><strong>Ban công:</strong> ").Append(EscapeHtml(balconyText)).Append("</li>");
        }

        sb.Append("</ul>");
        sb.Append("<p>Không gian thoáng đãng, khu dân cư an ninh, thuận tiện kết nối các tiện ích xung quanh. Pháp lý minh bạch, sẵn sàng giao dịch nhanh.</p>");
        return sb.ToString();
    }

    private static string EscapeHtml(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string CleanModelOutput(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return raw.Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                  .Replace("```html", string.Empty, StringComparison.OrdinalIgnoreCase)
                  .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
                  .Trim();
    }

    private static string FormatAsQuillHtml(string text)
    {
        if (text.Contains("<p>", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("<ul>", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var sb = new StringBuilder();
        var inList = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            var isBullet = line.StartsWith("- ", StringComparison.Ordinal) ||
                           line.StartsWith("* ", StringComparison.Ordinal) ||
                           line.StartsWith("• ", StringComparison.Ordinal);

            if (isBullet)
            {
                if (!inList)
                {
                    sb.Append("<ul>");
                    inList = true;
                }
                sb.Append("<li>").Append(EscapeHtml(line[2..].Trim())).Append("</li>");
            }
            else
            {
                if (inList)
                {
                    sb.Append("</ul>");
                    inList = false;
                }
                sb.Append("<p>").Append(EscapeHtml(line)).Append("</p>");
            }
        }

        if (inList)
        {
            sb.Append("</ul>");
        }

        return sb.ToString();
    }

    private static string MapPropertyTypeLabel(string? propertyType) => propertyType switch
    {
        "Apartment" => "Căn hộ chung cư",
        "House" => "Nhà riêng / Nhà phố",
        "Villa" => "Biệt thự",
        "Townhouse" => "Nhà liền kề",
        "Land" => "Đất nền / Đất thổ cư",
        "Commercial" => "Mặt bằng kinh doanh / Văn phòng",
        _ => string.IsNullOrWhiteSpace(propertyType) ? "Bất động sản" : propertyType
    };

    private static string MapShortPropertyTypeLabel(string? propertyType) => propertyType switch
    {
        "Apartment" => "căn hộ",
        "House" => "nhà phố",
        "Villa" => "biệt thự",
        "Townhouse" => "nhà liền kề",
        "Land" => "đất nền",
        "Commercial" => "mặt bằng",
        _ => string.IsNullOrWhiteSpace(propertyType) ? "bất động sản" : propertyType
    };

    private static string ShortenLocationName(string name)
    {
        var trimmed = name.Trim();
        return trimmed;
    }

    private static string ShortenCityName(string city)
    {
        var c = city.Trim();
        if (c.Contains("Hồ Chí Minh", StringComparison.OrdinalIgnoreCase)) return "TP.HCM";
        if (c.StartsWith("Thành phố ", StringComparison.OrdinalIgnoreCase)) return c["Thành phố ".Length..];
        if (c.StartsWith("Tỉnh ", StringComparison.OrdinalIgnoreCase)) return c["Tỉnh ".Length..];
        return c;
    }

    private static string FormatPriceText(decimal price, string? listingType)
    {
        var formatted = price switch
        {
            >= 1_000_000_000m => $"{(price / 1_000_000_000m).ToString("0.##", CultureInfo.InvariantCulture)} tỷ VNĐ",
            >= 1_000_000m => $"{(price / 1_000_000m).ToString("0.#", CultureInfo.InvariantCulture)} triệu VNĐ",
            _ => $"{price:N0} VNĐ"
        };

        return string.Equals(listingType, "rent", StringComparison.OrdinalIgnoreCase)
            ? $"{formatted}/tháng"
            : formatted;
    }

    private static string FormatCompactPriceForTitle(decimal price, string? listingType)
    {
        var formatted = price switch
        {
            >= 1_000_000_000m => $"{(price / 1_000_000_000m).ToString("0.##", CultureInfo.InvariantCulture)} tỷ",
            >= 1_000_000m => $"{(price / 1_000_000m).ToString("0.#", CultureInfo.InvariantCulture)} triệu",
            _ => $"{price:N0}đ"
        };

        return string.Equals(listingType, "rent", StringComparison.OrdinalIgnoreCase)
            ? $"{formatted}/tháng"
            : formatted;
    }

    private sealed record OllamaGenerateRequest(
        string Model,
        string Prompt,
        bool Stream,
        string Format);

    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; init; }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Ollama description generator is disabled. Using smart template fallback.")]
    private static partial void LogOllamaDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Ollama returned HTTP {StatusCode}. Using smart template fallback.")]
    private static partial void LogOllamaHttpFailure(ILogger logger, int statusCode);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Ollama returned an empty response. Using smart template fallback.")]
    private static partial void LogOllamaEmptyResponse(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Failed or timed out calling local Ollama. Using smart template fallback.")]
    private static partial void LogOllamaException(ILogger logger, Exception exception);
}

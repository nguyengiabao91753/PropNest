using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PropNest.Domain.Common;

/// <summary>
/// Chuẩn hóa toàn bộ thông báo kiểm duyệt, lý do từ chối và lịch sử hoạt động
/// từ JSON/mã kỹ thuật thành văn bản tiếng Việt thuần dễ hiểu cho người dùng.
/// </summary>
public static partial class ModerationTextFormatter
{
    private const string DefaultManualFallbackMessage =
        "Hệ thống kiểm duyệt tự động tạm thời chưa thể hoàn tất — tin đăng đã được chuyển sang kiểm duyệt thủ công.";

    private const string DefaultApprovedMessage = "Tin đăng đã được duyệt.";

    private static readonly CultureInfo ViCulture = CultureInfo.GetCultureInfo("vi-VN");

    private static readonly string[] InternalFailureHistoryActions =
    [
        "RouteManualReview",
        "EscalateManualReview"
    ];

    private static readonly string[] PassOnlyCodes =
    [
        "CLEAN_CONTENT",
        "PRICE_WITHIN_BENCHMARK"
    ];

    [GeneratedRegex(@"\\u([0-9a-fA-F]{4})")]
    private static partial Regex UnicodeEscapeRegex();

    [GeneratedRegex(@"^\[(manual_review|rejected|approved|price_warning|warning|critical|info|cảnh báo giá ảo)\]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex BracketPrefixRegex();

    [GeneratedRegex(@"^[A-Z0-9_]+$")]
    private static partial Regex PureCodeTokenRegex();

    [GeneratedRegex(@"\b(?:[A-Z]{2,}_[A-Z0-9_]+|RULE_\d+)\b")]
    private static partial Regex TechnicalCodeTokenRegex();

    [GeneratedRegex(@"\b(?:AME|Ollama|LLM|FastAPI|ManualReview|AutoApprove|AutoReject|RouteManualReview|EscalateManualReview|Moderator)\b", RegexOptions.IgnoreCase)]
    private static partial Regex InternalTermRegex();

    [GeneratedRegex(@"\(\s*HTTP\s*\d{3}\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex InlineHttpCodeRegex();

    [GeneratedRegex(@"\(\s*độ nét\s*=\s*[^)]+\)", RegexOptions.IgnoreCase)]
    private static partial Regex BlurMetricRegex();

    [GeneratedRegex(@"\(\s*0?\.\d{2}\s*\)")]
    private static partial Regex ScoreFloatRegex();

    [GeneratedRegex(@"\(\s*RULE_[A-Z0-9_]+\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex InlineRuleCodeParenRegex();

    [GeneratedRegex(@"""(?:Message|Summary|PriceWarningMessage|Reason|Detail)""\s*:\s*""((?:\\.|[^""\\])+)""", RegexOptions.IgnoreCase)]
    private static partial Regex TruncatedJsonMessageRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpaceRegex();

    public static string ApprovedHistoryNote => DefaultApprovedMessage;

    public static string ManualFallbackSummary => DefaultManualFallbackMessage;

    public static bool IsInternalFailureHistoryAction(string? actionType) =>
        !string.IsNullOrWhiteSpace(actionType)
        && InternalFailureHistoryActions.Any(x => string.Equals(x, actionType.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsApprovalAction(string? actionType) =>
        string.Equals(actionType?.Trim(), "Approve", StringComparison.OrdinalIgnoreCase)
        || string.Equals(actionType?.Trim(), "AutoApprove", StringComparison.OrdinalIgnoreCase);

    public static bool IsRejectionAction(string? actionType) =>
        string.Equals(actionType?.Trim(), "Reject", StringComparison.OrdinalIgnoreCase)
        || string.Equals(actionType?.Trim(), "AutoReject", StringComparison.OrdinalIgnoreCase);

    public static bool IsPassOnlyCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return true;
        }

        var trimmed = code.Trim();
        return trimmed.StartsWith("PASS_", StringComparison.OrdinalIgnoreCase)
            || PassOnlyCodes.Any(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetActionDisplayName(string? actionType) => (actionType?.Trim().ToLowerInvariant()) switch
    {
        "create" => "Tạo mới tin đăng",
        "updatecontent" or "update" => "Cập nhật thông tin",
        "updateimages" => "Cập nhật hình ảnh",
        "deleteimage" => "Xóa hình ảnh",
        "submit" => "Gửi duyệt tin đăng",
        "approve" or "autoapprove" => "Tin đăng đã được duyệt",
        "reject" or "autoreject" => "Từ chối duyệt tin",
        "upgradepackage" => "Thanh toán / Nâng cấp gói",
        "downgradepackage" => "Hết hạn gói VIP (chuyển về Tiêu chuẩn)",
        "hide" => "Tạm ẩn tin đăng",
        "unhide" => "Hiển thị lại tin đăng",
        "expire" => "Hết hạn hiển thị",
        "delete" => "Xóa tin đăng",
        _ => "Cập nhật tin đăng"
    };

    public static string GetSeverityDisplayLabel(string? severity) => (severity?.Trim().ToLowerInvariant()) switch
    {
        "critical" or "reject" or "block" => "Vi phạm nghiêm trọng",
        "warning" or "price_warning" => "Cần lưu ý",
        "manual_review" or "manualreview" => "Cần kiểm duyệt thủ công",
        _ => "Thông tin"
    };

    public static string GetRuleDisplayTitle(string? ruleCode)
    {
        if (string.IsNullOrWhiteSpace(ruleCode))
        {
            return "Cần kiểm tra nội dung";
        }

        var upper = ruleCode.Trim().ToUpperInvariant();
        if (upper.Contains("BADWORD", StringComparison.Ordinal)) return "Nội dung chứa từ ngữ vi phạm";
        if (upper.Contains("PHONE", StringComparison.Ordinal)) return "Thông tin liên hệ trong nội dung";
        if (upper.Contains("PRICE_TOO_LOW", StringComparison.Ordinal)) return "Giá thấp hơn khung khu vực";
        if (upper.Contains("PRICE_TOO_HIGH", StringComparison.Ordinal)) return "Giá cao hơn khung khu vực";
        if (upper.Contains("PRICE", StringComparison.Ordinal)) return "Cảnh báo chênh lệch giá";
        if (upper.Contains("IMAGE_BLURRY", StringComparison.Ordinal)) return "Hình ảnh bị mờ / thiếu nét";
        if (upper.Contains("IMAGE_LOW_RESOLUTION", StringComparison.Ordinal)) return "Hình ảnh độ phân giải thấp";
        if (upper.Contains("IMAGE_MISSING", StringComparison.Ordinal)) return "Thiếu hình ảnh minh họa";
        if (upper.Contains("IMAGE_POOR_EXPOSURE", StringComparison.Ordinal)) return "Ánh sáng / tương phản ảnh chưa đạt";
        if (upper.Contains("IMAGE_UNVERIFIABLE", StringComparison.Ordinal)) return "Không thể kiểm tra tự động hình ảnh";
        if (upper.Contains("IMAGE_TOO_DARK", StringComparison.Ordinal)) return "Hình ảnh quá tối";
        if (upper.Contains("IMAGE_TOO_BRIGHT", StringComparison.Ordinal)) return "Hình ảnh bị lóa sáng";
        if (upper.Contains("IMAGE_LOW_CONTRAST", StringComparison.Ordinal)) return "Hình ảnh kém tương phản";
        if (upper.Contains("IMAGE", StringComparison.Ordinal)) return "Cần kiểm tra chất lượng hình ảnh";
        if (upper.Contains("CAPS", StringComparison.Ordinal)) return "Lạm dụng chữ in hoa";
        if (upper.Contains("SHORT", StringComparison.Ordinal)) return "Mô tả còn quá ngắn";
        if (upper.Contains("REPETITION", StringComparison.Ordinal)) return "Ký tự lặp bất thường";
        if (upper.Contains("SPECS", StringComparison.Ordinal)) return "Thông số bất động sản chưa hợp lý";
        if (upper.Contains("FORMAT", StringComparison.Ordinal)) return "Định dạng nội dung chưa đạt chuẩn";
        if (upper.Contains("FRAUD", StringComparison.Ordinal)) return "Dấu hiệu nội dung bất thường";
        if (upper.Contains("LOW_QUALITY", StringComparison.Ordinal)) return "Chất lượng mô tả cần bổ sung";
        if (upper.Contains("LLM_UNAVAILABLE", StringComparison.Ordinal)
            || upper.Contains("FALLBACK", StringComparison.Ordinal)
            || upper.Contains("UNAVAILABLE", StringComparison.Ordinal)
            || upper.Contains("DISABLED", StringComparison.Ordinal)
            || upper.Contains("HTTP_ERROR", StringComparison.Ordinal)
            || upper.Contains("EMPTY_PAYLOAD", StringComparison.Ordinal)
            || upper.Contains("CONSUMER_ERROR", StringComparison.Ordinal)
            || upper.Contains("MANUAL", StringComparison.Ordinal))
        {
            return "Cần kiểm duyệt thủ công";
        }
        if (upper.StartsWith("RULE_", StringComparison.Ordinal) || upper.Contains("CUSTOM_RULE", StringComparison.Ordinal))
        {
            return "Quy định nội dung tin đăng";
        }
        if (IsPassOnlyCode(upper))
        {
            return "Đạt tiêu chuẩn kiểm tra";
        }

        return "Cần kiểm tra thêm";
    }

    public static string MapCodeToFriendlyMessage(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || IsPassOnlyCode(code))
        {
            return string.Empty;
        }

        var upper = code.Trim().ToUpperInvariant();
        if (upper.Contains("BADWORD", StringComparison.Ordinal)) return "Nội dung chứa từ ngữ không phù hợp với quy định đăng tin.";
        if (upper.Contains("PHONE", StringComparison.Ordinal)) return "Nội dung tin đăng không được chèn số điện thoại trực tiếp.";
        if (upper.Contains("PRICE_TOO_LOW", StringComparison.Ordinal)) return "Mức giá đăng thấp hơn đáng kể so với mặt bằng khu vực (cần kiểm tra lại giá).";
        if (upper.Contains("PRICE_TOO_HIGH", StringComparison.Ordinal)) return "Mức giá đăng cao hơn đáng kể so với mặt bằng khu vực (cần kiểm tra lại giá).";
        if (upper.Contains("PRICE", StringComparison.Ordinal)) return "Mức giá đăng có sự chênh lệch lớn so với mặt bằng khu vực.";
        if (upper.Contains("IMAGE_BLURRY", StringComparison.Ordinal)) return "Hình ảnh bị mờ hoặc thiếu độ sắc nét, cần kiểm tra trực tiếp.";
        if (upper.Contains("IMAGE_LOW_RESOLUTION", StringComparison.Ordinal)) return "Hình ảnh có độ phân giải thấp, cần kiểm tra chất lượng hiển thị.";
        if (upper.Contains("IMAGE_MISSING", StringComparison.Ordinal)) return "Tin đăng chưa có hình ảnh minh họa, cần xem xét trực tiếp.";
        if (upper.Contains("IMAGE_POOR_EXPOSURE", StringComparison.Ordinal)) return "Hình ảnh quá tối, bị lóa sáng hoặc thiếu tương phản, cần kiểm tra trực tiếp.";
        if (upper.Contains("IMAGE_UNVERIFIABLE", StringComparison.Ordinal)) return "Không thể tải hoặc đọc định dạng hình ảnh để kiểm tra tự động, cần xem xét trực tiếp.";
        if (upper.Contains("IMAGE_TOO_DARK", StringComparison.Ordinal)) return "Hình ảnh quá tối, khó quan sát chi tiết bất động sản.";
        if (upper.Contains("IMAGE_TOO_BRIGHT", StringComparison.Ordinal)) return "Hình ảnh bị lóa sáng, khó quan sát chi tiết bất động sản.";
        if (upper.Contains("IMAGE_LOW_CONTRAST", StringComparison.Ordinal)) return "Hình ảnh thiếu độ tương phản hoặc không rõ chi tiết.";
        if (upper.Contains("IMAGE", StringComparison.Ordinal)) return "Chưa thể đánh giá tự động chất lượng hình ảnh, cần xem xét trực tiếp.";
        if (upper.Contains("CAPS", StringComparison.Ordinal)) return "Nội dung sử dụng quá nhiều chữ in hoa.";
        if (upper.Contains("SHORT", StringComparison.Ordinal)) return "Phần mô tả tin đăng còn quá ngắn, thiếu thông tin chi tiết.";
        if (upper.Contains("REPETITION", StringComparison.Ordinal)) return "Nội dung có nhiều ký tự lặp lại bất thường.";
        if (upper.Contains("SPECS", StringComparison.Ordinal)) return "Thông số diện tích và số phòng/tầng chưa khớp hợp lý.";
        if (upper.Contains("FORMAT", StringComparison.Ordinal)) return "Định dạng trình bày nội dung chưa đạt chuẩn.";
        if (upper.Contains("FRAUD", StringComparison.Ordinal)) return "Nội dung có dấu hiệu bất thường, cần thẩm định kỹ trước khi duyệt.";
        if (upper.Contains("LOW_QUALITY", StringComparison.Ordinal)) return "Nội dung mô tả còn sơ sài, cần kiểm tra chất lượng trước khi duyệt.";
        if (upper.Contains("LLM_UNAVAILABLE", StringComparison.Ordinal)
            || upper.Contains("FALLBACK", StringComparison.Ordinal)
            || upper.Contains("UNAVAILABLE", StringComparison.Ordinal)
            || upper.Contains("DISABLED", StringComparison.Ordinal)
            || upper.Contains("HTTP_ERROR", StringComparison.Ordinal)
            || upper.Contains("EMPTY_PAYLOAD", StringComparison.Ordinal)
            || upper.Contains("CONSUMER_ERROR", StringComparison.Ordinal)
            || upper.Contains("MANUAL", StringComparison.Ordinal))
        {
            return DefaultManualFallbackMessage;
        }
        if (upper.StartsWith("RULE_", StringComparison.Ordinal) || upper.Contains("CUSTOM_RULE", StringComparison.Ordinal))
        {
            return "Nội dung chưa đáp ứng quy định kiểm duyệt tin đăng.";
        }

        return SanitizePlainText(code);
    }

    /// <summary>
    /// Chuẩn hóa chuỗi JSON (Object / Array / String / JSON bị cắt ngắn) hoặc text thô chứa mã lỗi thành văn bản tiếng Việt thuần.
    /// </summary>
    public static string FormatModerationReasonText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var decoded = DecodeUnicodeEscapes(raw.Trim());

        if ((decoded.StartsWith('{') && decoded.EndsWith('}'))
            || (decoded.StartsWith('[') && decoded.EndsWith(']'))
            || (decoded.StartsWith('"') && decoded.EndsWith('"')))
        {
            try
            {
                using var doc = JsonDocument.Parse(decoded);
                var extracted = ExtractFromJsonElement(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    return SanitizePlainText(extracted);
                }
            }
            catch (JsonException)
            {
                // Rơi xuống xử lý JSON bị cắt ngắn hoặc văn bản thuần bên dưới
            }
        }

        // Xử lý chuỗi JSON bị cắt ngắn ở DB cũ (vd: {"Summary":"...","ReasonCodes":[... )
        if (decoded.StartsWith('{') || decoded.StartsWith("[{", StringComparison.Ordinal))
        {
            var matches = TruncatedJsonMessageRegex().Matches(decoded);
            if (matches.Count > 0)
            {
                var extractedList = matches
                    .Select(m => SanitizePlainText(m.Groups[1].Value.Replace("\\\"", "\"", StringComparison.Ordinal)))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (extractedList.Count > 0)
                {
                    return string.Join("; ", extractedList);
                }
            }

            return DefaultManualFallbackMessage;
        }

        // Bóc các cặp ngoặc đơn giản còn sót (vd: ["lý do"] bị cắt ngắn)
        var cleaned = decoded;
        if (cleaned.StartsWith("[\"", StringComparison.Ordinal))
        {
            cleaned = cleaned[2..];
            if (cleaned.EndsWith("\"]", StringComparison.Ordinal))
            {
                cleaned = cleaned[..^2];
            }
        }
        else if (cleaned.StartsWith('[') && cleaned.EndsWith(']'))
        {
            cleaned = cleaned[1..^1].Trim('"', '\'');
        }
        else if (cleaned.StartsWith('"') && cleaned.EndsWith('"'))
        {
            cleaned = cleaned[1..^1];
        }

        return SanitizePlainText(cleaned);
    }

    /// <summary>
    /// Làm sạch chuỗi văn bản, loại bỏ tiền tố kỹ thuật, mã lỗi tiếng Anh và thông báo exception.
    /// </summary>
    public static string SanitizePlainText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = DecodeUnicodeEscapes(text.Trim());
        cleaned = BracketPrefixRegex().Replace(cleaned, string.Empty).Trim();

        // Nếu toàn bộ chuỗi là 1 mã code kỹ thuật (vd: IMAGE_BLURRY, LLM_UNAVAILABLE_FALLBACK_MANUAL)
        if (PureCodeTokenRegex().IsMatch(cleaned))
        {
            return MapCodeToFriendlyMessage(cleaned);
        }

        // Làm sạch cụm câu cũ của hệ thống trước khi kiểm tra lỗi kỹ thuật
        cleaned = cleaned
            .Replace("tự động chuyển AME kiểm duyệt lại", "và gửi kiểm duyệt lại", StringComparison.OrdinalIgnoreCase)
            .Replace("chuyển Admin kiểm duyệt thủ công", "cần kiểm duyệt thủ công", StringComparison.OrdinalIgnoreCase)
            .Replace("chuyển Admin duyệt", "cần kiểm duyệt thủ công", StringComparison.OrdinalIgnoreCase)
            .Replace("cần Admin duyệt tay", "cần kiểm duyệt thủ công", StringComparison.OrdinalIgnoreCase)
            .Replace("AME không thể xác thực rõ nội dung ảnh,", "chưa thể xác thực rõ nội dung ảnh,", StringComparison.OrdinalIgnoreCase)
            .Replace("để AME kiểm tra", "để kiểm tra tự động", StringComparison.OrdinalIgnoreCase)
            .Replace("để AI xác thực", "minh họa", StringComparison.OrdinalIgnoreCase)
            .Replace("Dịch vụ AI phân tích ngữ nghĩa tạm thời không khả dụng", "Hệ thống đánh giá tự động tạm thời bận", StringComparison.OrdinalIgnoreCase)
            .Replace("AI phát hiện dấu hiệu lừa đảo", "Phát hiện dấu hiệu nội dung bất thường", StringComparison.OrdinalIgnoreCase);

        // Xóa mã HTTP gắn trong ngoặc của thông báo tải ảnh (vd: "(HTTP 404)") trước khi check lỗi raw HTTP
        cleaned = InlineHttpCodeRegex().Replace(cleaned, string.Empty);
        cleaned = BlurMetricRegex().Replace(cleaned, string.Empty);
        cleaned = InlineRuleCodeParenRegex().Replace(cleaned, string.Empty);
        cleaned = ScoreFloatRegex().Replace(cleaned, string.Empty);

        // Thay thế thông báo lỗi kết nối/HTTP/exception kỹ thuật thành câu tiếng Việt lịch sự
        if (ContainsRawTechnicalError(cleaned))
        {
            return DefaultManualFallbackMessage;
        }

        // Loại bỏ tên dịch vụ nội bộ & thuật ngữ kỹ thuật nếu còn sót
        cleaned = InternalTermRegex().Replace(cleaned, m =>
            m.Value.ToUpperInvariant() switch
            {
                "AME" or "OLLAMA" or "LLM" or "FASTAPI" => "hệ thống đánh giá tự động",
                "MANUALREVIEW" or "ROUTEMANUALREVIEW" or "ESCALATEMANUALREVIEW" => "kiểm duyệt thủ công",
                "AUTOAPPROVE" => "duyệt tin",
                "AUTOREJECT" => "từ chối tin",
                "MODERATOR" => "quản trị viên",
                _ => string.Empty
            });

        // Loại bỏ mã code dạng UPPER_SNAKE_CASE bị kẹp trong câu
        cleaned = TechnicalCodeTokenRegex().Replace(cleaned, m =>
        {
            var mapped = MapCodeToFriendlyMessage(m.Value);
            return string.IsNullOrWhiteSpace(mapped) ? string.Empty : mapped;
        });

        // Chuẩn hóa khoảng trắng và dấu câu thừa
        cleaned = MultiSpaceRegex().Replace(cleaned, " ").Trim();
        cleaned = cleaned.Replace(" .", ".", StringComparison.Ordinal)
                         .Replace(" ,", ",", StringComparison.Ordinal)
                         .Replace(" — .", ".", StringComparison.Ordinal)
                         .Trim(' ', '-', '—', ';', ':');

        if (cleaned.Length > 0 && char.IsLower(cleaned[0]))
        {
            cleaned = char.ToUpper(cleaned[0], ViCulture) + cleaned[1..];
        }

        return cleaned;
    }

    private static string ExtractFromJsonElement(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.String)
        {
            return root.GetString() ?? string.Empty;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            var items = new List<string>();
            foreach (var elem in root.EnumerateArray())
            {
                var item = ExtractFromJsonElement(elem);
                if (!string.IsNullOrWhiteSpace(item))
                {
                    items.Add(item);
                }
            }
            return string.Join("; ", items.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            var messages = new List<string>();

            // 1. Ưu tiên danh sách Violations (có Message chi tiết tiếng Việt)
            if (TryGetPropertyIgnoreCase(root, "Violations", out var violationsElem)
                && violationsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in violationsElem.EnumerateArray())
                {
                    var rule = TryGetStringIgnoreCase(v, "Rule");
                    var msg = TryGetStringIgnoreCase(v, "Message");
                    var formatted = !string.IsNullOrWhiteSpace(msg)
                        ? SanitizePlainText(msg)
                        : MapCodeToFriendlyMessage(rule);

                    if (!string.IsNullOrWhiteSpace(formatted))
                    {
                        messages.Add(formatted);
                    }
                }
            }

            if (messages.Count > 0)
            {
                return string.Join("; ", messages.Distinct(StringComparer.OrdinalIgnoreCase));
            }

            // 2. Fallback sang Summary hoặc PriceWarningMessage
            var summary = TryGetStringIgnoreCase(root, "Summary");
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return SanitizePlainText(summary);
            }

            var priceWarning = TryGetStringIgnoreCase(root, "PriceWarningMessage");
            if (!string.IsNullOrWhiteSpace(priceWarning))
            {
                return SanitizePlainText(priceWarning);
            }

            // 3. Fallback sang ReasonCodes (chuyển từng mã thành văn bản tiếng Việt)
            if (TryGetPropertyIgnoreCase(root, "ReasonCodes", out var codesElem)
                && codesElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in codesElem.EnumerateArray())
                {
                    var codeStr = c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                    var mapped = MapCodeToFriendlyMessage(codeStr);
                    if (!string.IsNullOrWhiteSpace(mapped))
                    {
                        messages.Add(mapped);
                    }
                }

                if (messages.Count > 0)
                {
                    return string.Join("; ", messages.Distinct(StringComparer.OrdinalIgnoreCase));
                }
            }

            // 4. Các trường thông báo chung khác
            var genericMessage = TryGetStringIgnoreCase(root, "Message")
                ?? TryGetStringIgnoreCase(root, "Reason")
                ?? TryGetStringIgnoreCase(root, "Detail");
            if (!string.IsNullOrWhiteSpace(genericMessage))
            {
                return SanitizePlainText(genericMessage);
            }
        }

        return string.Empty;
    }

    private static bool ContainsRawTechnicalError(string value) =>
        value.Contains("HttpRequestException", StringComparison.OrdinalIgnoreCase)
        || value.Contains("TaskCanceledException", StringComparison.OrdinalIgnoreCase)
        || value.Contains("SocketException", StringComparison.OrdinalIgnoreCase)
        || value.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
        || value.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
        || value.Contains("HTTP 4", StringComparison.OrdinalIgnoreCase)
        || value.Contains("HTTP 5", StringComparison.OrdinalIgnoreCase)
        || value.Contains("localhost:", StringComparison.OrdinalIgnoreCase)
        || value.Contains("127.0.0.1:", StringComparison.OrdinalIgnoreCase)
        || value.Contains("JsonException", StringComparison.OrdinalIgnoreCase)
        || value.Contains("System.", StringComparison.Ordinal);

    private static string DecodeUnicodeEscapes(string input)
    {
        if (!input.Contains(@"\u", StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }

        try
        {
            return UnicodeEscapeRegex().Replace(
                input,
                m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        }
        catch
        {
            return input;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? TryGetStringIgnoreCase(JsonElement obj, string name)
    {
        if (obj.ValueKind == JsonValueKind.Object
            && TryGetPropertyIgnoreCase(obj, name, out var elem)
            && elem.ValueKind == JsonValueKind.String)
        {
            return elem.GetString();
        }

        return null;
    }
}

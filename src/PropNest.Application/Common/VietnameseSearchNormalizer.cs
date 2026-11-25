using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PropNest.Application.Common;

/// <summary>
/// Chuẩn hóa chuỗi văn bản tiếng Việt để tìm kiếm toàn văn chính xác trên Meilisearch.
/// Đặc biệt xử lý ký tự 'đ/Đ' -> 'd/D' và tách dấu thanh Unicode NFD.
/// </summary>
public static partial class VietnameseSearchNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    /// <summary>
    /// Chuẩn hóa một chuỗi: đ/Đ -> d/D, bỏ dấu thanh, chuyển thành chữ thường không dấu.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // 1. Chuyển đổi đ/Đ trước khi phân rã Unicode vì NFD không tự tách đ thành d
        var working = text.Replace('đ', 'd').Replace('Đ', 'D');

        // 2. Phân rã chuỗi sang FormD để tách chữ cái gốc và dấu thanh rời rạc
        var normalizedString = working.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        // 3. Gom lại thành FormC chữ thường và chuẩn hóa khoảng trắng
        var result = stringBuilder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        return MultiSpaceRegex().Replace(result, " ").Trim();
    }

    /// <summary>
    /// Ghép nhiều trường (Tiêu đề, Địa chỉ, Khu vực cũ/mới) thành chuỗi tìm kiếm chuẩn hóa duy nhất.
    /// </summary>
    public static string BuildSearchNormalizedText(params string?[] parts)
    {
        if (parts is null || parts.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            var normalizedPart = Normalize(part);
            if (!string.IsNullOrWhiteSpace(normalizedPart))
            {
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(normalizedPart);
            }
        }

        return sb.ToString();
    }

    private static readonly string[] AdminPrefixes =
    [
        "thanh pho ", "tp. ", "tp ", "tinh ", "thi xa ", "tx. ", "tx ",
        "huyen ", "quan ", "q. ", "phuong ", "p. ", "xa ", "thi tran "
    ];

    /// <summary>
    /// Chuẩn hóa tên địa danh hành chính về dạng rút gọn không dấu (vd: "Thành phố Hồ Chí Minh" -> "ho chi minh", "Quận 7" -> "quan 7").
    /// </summary>
    public static string NormalizeLocationToken(string? raw)
    {
        var normalized = Normalize(raw);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        // Bỏ tiền tố hành chính nếu phần lõi phía sau không phải số thuần (giữ nguyên "quan 7", "phuong 5")
        foreach (var prefix in AdminPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                var remainder = normalized[prefix.Length..].Trim();
                if (!string.IsNullOrWhiteSpace(remainder) && !IsNumericOnly(remainder))
                {
                    normalized = remainder;
                }
                break;
            }
        }

        return normalized switch
        {
            "tphcm" or "tp hcm" or "hcm" or "sai gon" or "saigon" => "ho chi minh",
            "hn" or "thu do ha noi" => "ha noi",
            "dn" => "da nang",
            _ => normalized
        };
    }

    /// <summary>
    /// Trích xuất tập token địa chỉ đầy đủ phục vụ lọc chính xác trên Meilisearch (hỗ trợ cả địa chỉ gốc, không dấu, rút gọn và LegacyAddress phân cách dấu phẩy).
    /// </summary>
    public static IReadOnlyList<string> BuildLocationFilterTokens(params string?[] parts)
    {
        if (parts is null || parts.Length == 0)
        {
            return Array.Empty<string>();
        }

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddSegment(string? segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return;
            }

            var trimmed = segment.Trim();
            if (trimmed.Length == 0)
            {
                return;
            }

            tokens.Add(trimmed);

            var normalizedFull = Normalize(trimmed);
            if (!string.IsNullOrWhiteSpace(normalizedFull))
            {
                tokens.Add(normalizedFull);
            }

            var canonical = NormalizeLocationToken(trimmed);
            if (!string.IsNullOrWhiteSpace(canonical))
            {
                tokens.Add(canonical);
            }
        }

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            AddSegment(part);

            if (part.Contains(',', StringComparison.Ordinal) || part.Contains(';', StringComparison.Ordinal))
            {
                foreach (var sub in part.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    AddSegment(sub);
                }
            }
        }

        return tokens.ToArray();
    }

    private static bool IsNumericOnly(string value)
    {
        foreach (var ch in value)
        {
            if (!char.IsAsciiDigit(ch))
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    /// <summary>
    /// Trích xuất danh sách token địa chỉ không trùng lặp (Tỉnh, Huyện, Xã, Địa chỉ cũ).
    /// </summary>
    public static IReadOnlyList<string> TokenizeLocation(params string?[] parts)
    {
        if (parts is null || parts.Length == 0)
        {
            return Array.Empty<string>();
        }

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                tokens.Add(trimmed);
            }
        }

        return tokens.ToArray();
    }
}

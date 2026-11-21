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

using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace PropNest.Domain.Common;

public static partial class RichTextHtmlHelper
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "strong", "b", "em", "i", "u", "s", "strike",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "ul", "ol", "li", "blockquote", "hr", "span", "div", "a"
    };

    private static readonly HashSet<string> AllowedCssClassTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "ql-align-center", "ql-align-right", "ql-align-justify", "ql-align-left",
        "ql-indent-1", "ql-indent-2", "ql-indent-3", "ql-indent-4",
        "ql-indent-5", "ql-indent-6", "ql-indent-7", "ql-indent-8"
    };

    /// <summary>
    /// Normalizes Quill rich-text markup (e.g., data-list="bullet" inside &lt;ol&gt; and ql-ui helper spans)
    /// and removes unsafe tags/attributes so descriptions render cleanly and safely across portals.
    /// </summary>
    public static string FormatRichDescriptionHtml(string? raw, string? emptyFallbackHtml = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return emptyFallbackHtml ?? string.Empty;
        }

        var working = raw.Trim();

        // Plain-text fallback: escape and preserve line breaks when no HTML markup is present.
        if (!AnyHtmlTagRegex().IsMatch(working))
        {
            return FormatPlainTextAsHtml(working);
        }

        // 1. Drop executable or active content blocks completely (including inner body).
        working = DangerousBlockTagRegex().Replace(working, string.Empty);
        working = DangerousSingleTagRegex().Replace(working, string.Empty);
        working = HtmlCommentRegex().Replace(working, string.Empty);

        // 2. Remove Quill UI helper markers (<span class="ql-ui" contenteditable="false"></span>).
        working = QuillUiSpanRegex().Replace(working, string.Empty);

        // 3. Convert Quill v2 <ol><li data-list="bullet">...</li></ol> into semantic <ul> / <ol> segments.
        working = NormalizeQuillLists(working);

        // 4. Whitelist HTML tags and sanitize attributes (zero event handlers or javascript: URLs).
        working = HtmlTagRegex().Replace(working, SanitizeMatchedTag);

        var cleaned = working.Trim();
        return string.IsNullOrWhiteSpace(cleaned)
            ? (emptyFallbackHtml ?? string.Empty)
            : cleaned;
    }

    /// <summary>
    /// Converts rich-text HTML (including Quill bullet lists) into readable plain text for diff tables and previews.
    /// </summary>
    public static string ToPlainTextPreview(string? raw, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var working = DangerousBlockTagRegex().Replace(raw, string.Empty);
        working = QuillUiSpanRegex().Replace(working, string.Empty);
        working = LineBreakOrBlockOpenRegex().Replace(working, "\n");
        working = ListItemOpenRegex().Replace(working, "\n• ");
        working = BlockCloseRegex().Replace(working, "\n");
        working = AnyHtmlTagRegex().Replace(working, string.Empty);

        var decoded = WebUtility.HtmlDecode(working);
        var lines = decoded
            .Split('\n')
            .Select(line => MultiSpaceRegex().Replace(line, " ").Trim())
            .Where(line => line.Length > 0);

        var result = string.Join("\n", lines);
        if (maxLength.HasValue && maxLength.Value > 3 && result.Length > maxLength.Value)
        {
            return string.Concat(result.AsSpan(0, maxLength.Value - 3).TrimEnd(), "...");
        }

        return result;
    }

    private static string FormatPlainTextAsHtml(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var paragraphs = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paragraphs.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            var encoded = WebUtility.HtmlEncode(paragraph).Replace("\n", "<br />", StringComparison.Ordinal);
            sb.Append("<p>").Append(encoded).Append("</p>");
        }
        return sb.ToString();
    }

    private static string NormalizeQuillLists(string html)
    {
        // Loop to resolve any inner-to-outer <ol> blocks safely without risking nested backtracking.
        for (var i = 0; i < 8; i++)
        {
            var updated = InnerOrderedListRegex().Replace(html, m =>
            {
                var body = m.Groups[1].Value;
                if (!body.Contains("data-list", StringComparison.OrdinalIgnoreCase))
                {
                    return m.Value;
                }

                var items = ListItemWithBodyRegex().Matches(body);
                if (items.Count == 0)
                {
                    return m.Value;
                }

                var sb = new StringBuilder();
                string? currentListTag = null;

                foreach (Match li in items)
                {
                    var liAttrs = li.Groups[1].Value;
                    var liInner = li.Groups[2].Value;
                    var listTypeMatch = DataListAttrRegex().Match(liAttrs);
                    var isBullet = listTypeMatch.Success &&
                                   string.Equals(listTypeMatch.Groups[1].Value, "bullet", StringComparison.OrdinalIgnoreCase);
                    var targetListTag = isBullet ? "ul" : "ol";

                    if (!string.Equals(currentListTag, targetListTag, StringComparison.Ordinal))
                    {
                        if (currentListTag is not null)
                        {
                            sb.Append("</").Append(currentListTag).Append('>');
                        }
                        sb.Append('<').Append(targetListTag).Append('>');
                        currentListTag = targetListTag;
                    }

                    var cleanedLiAttrs = DataListAttrRegex().Replace(liAttrs, string.Empty);
                    sb.Append("<li").Append(cleanedLiAttrs).Append('>').Append(liInner).Append("</li>");
                }

                if (currentListTag is not null)
                {
                    sb.Append("</").Append(currentListTag).Append('>');
                }

                return sb.ToString();
            });

            if (string.Equals(updated, html, StringComparison.Ordinal))
            {
                break;
            }
            html = updated;
        }

        return html;
    }

    private static string SanitizeMatchedTag(Match match)
    {
        var isClosing = match.Groups[1].Value == "/";
        var tagName = match.Groups[2].Value.ToLowerInvariant();
        var rawAttrs = match.Groups[3].Value;

        if (!AllowedTags.Contains(tagName))
        {
            return string.Empty;
        }

        if (isClosing)
        {
            return tagName is "br" or "hr" ? string.Empty : $"</{tagName}>";
        }

        if (tagName is "br" or "hr")
        {
            return $"<{tagName} />";
        }

        if (tagName == "a")
        {
            var hrefMatch = HrefAttrRegex().Match(rawAttrs);
            if (hrefMatch.Success)
            {
                var href = (hrefMatch.Groups[1].Success ? hrefMatch.Groups[1].Value :
                    hrefMatch.Groups[2].Success ? hrefMatch.Groups[2].Value :
                    hrefMatch.Groups[3].Value).Trim();

                if (IsSafeHref(href))
                {
                    return $"<a href=\"{WebUtility.HtmlEncode(href)}\" target=\"_blank\" rel=\"noopener noreferrer\">";
                }
            }
            return "<a>";
        }

        var classMatch = ClassAttrRegex().Match(rawAttrs);
        if (classMatch.Success)
        {
            var rawClasses = (classMatch.Groups[1].Success ? classMatch.Groups[1].Value : classMatch.Groups[2].Value)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var safeClasses = rawClasses.Where(AllowedCssClassTokens.Contains).ToArray();
            if (safeClasses.Length > 0)
            {
                return $"<{tagName} class=\"{string.Join(' ', safeClasses)}\">";
            }
        }

        return $"<{tagName}>";
    }

    private static bool IsSafeHref(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        return href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith('/')
            || href.StartsWith('#');
    }

    [GeneratedRegex(@"<(script|style|iframe|object|svg|math|form|noscript)\b[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousBlockTagRegex();

    [GeneratedRegex(@"<(script|style|iframe|object|embed|meta|link|base|svg|math|form)\b[^>]*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousSingleTagRegex();

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex(@"<span\b[^>]*class\s*=\s*(?:""[^""]*\bql-ui\b[^""]*""|'[^']*\bql-ui\b[^']*')[^>]*>.*?</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex QuillUiSpanRegex();

    [GeneratedRegex(@"<ol\b[^>]*>((?:(?!</?ol\b).)*?)</ol>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InnerOrderedListRegex();

    [GeneratedRegex(@"<li\b([^>]*)>(.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ListItemWithBodyRegex();

    [GeneratedRegex(@"\s+data-list\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase)]
    private static partial Regex DataListAttrRegex();

    [GeneratedRegex(@"<(/?)([a-zA-Z][a-zA-Z0-9]*)\b([^>]*)>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"</?[a-zA-Z][^>]*>")]
    private static partial Regex AnyHtmlTagRegex();

    [GeneratedRegex(@"\bhref\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase)]
    private static partial Regex HrefAttrRegex();

    [GeneratedRegex(@"\bclass\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase)]
    private static partial Regex ClassAttrRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakOrBlockOpenRegex();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemOpenRegex();

    [GeneratedRegex(@"</(?:p|div|h[1-6]|li|blockquote|ul|ol)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockCloseRegex();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex MultiSpaceRegex();
}

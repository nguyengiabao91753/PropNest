namespace PropNest.Web.Client.Helpers;

public static class FormatHelper
{
    public static string FormatPrice(decimal price, string listingType)
    {
        var suffix = string.Equals(listingType, "rent", StringComparison.OrdinalIgnoreCase) ? "/tháng" : "";

        if (price >= 1_000_000_000m)
        {
            var billions = price / 1_000_000_000m;
            return $"{billions:0.##} tỷ VNĐ{suffix}";
        }

        if (price >= 1_000_000m)
        {
            var millions = price / 1_000_000m;
            return $"{millions:0.##} triệu VNĐ{suffix}";
        }

        return $"{price:N0} ₫{suffix}";
    }

    public static string FormatCompactPrice(decimal price, string listingType)
    {
        var suffix = string.Equals(listingType, "rent", StringComparison.OrdinalIgnoreCase) ? "/th" : "";

        if (price >= 1_000_000_000m)
        {
            var billions = price / 1_000_000_000m;
            return $"{billions:0.##} tỷ{suffix}";
        }

        if (price >= 1_000_000m)
        {
            var millions = price / 1_000_000m;
            return $"{millions:0.##} tr{suffix}";
        }

        return $"{price:N0} ₫{suffix}";
    }

    public static string FormatArea(decimal area)
    {
        return $"{area:N0} m²";
    }

    public static string FormatVnd(decimal amount)
    {
        return $"{amount:N0} ₫";
    }
}

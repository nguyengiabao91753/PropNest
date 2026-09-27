using System.Globalization;
using PropNest.Domain.Listings;

namespace PropNest.Web.Seller.Helpers;

public static class FormatHelper
{
    private static readonly CultureInfo ViCulture = new("vi-VN");

    public static string FormatPrice(decimal price, string listingType)
    {
        if (string.Equals(listingType, "rent", StringComparison.OrdinalIgnoreCase))
        {
            if (price >= 1_000_000)
            {
                var millions = price / 1_000_000m;
                return $"{millions:0.#} triệu/tháng";
            }
            return $"{price.ToString("N0", ViCulture)} ₫/tháng";
        }

        if (price >= 1_000_000_000)
        {
            var billions = price / 1_000_000_000m;
            return $"{billions:0.#} tỷ";
        }
        if (price >= 1_000_000)
        {
            var millions = price / 1_000_000m;
            return $"{millions:0.#} triệu";
        }

        return $"{price.ToString("N0", ViCulture)} ₫";
    }

    public static string FormatCompactPrice(decimal price, string listingType)
    {
        if (string.Equals(listingType, "rent", StringComparison.OrdinalIgnoreCase))
        {
            if (price >= 1_000_000)
            {
                var millions = price / 1_000_000m;
                return $"{millions:0.#} tr/th";
            }
            return $"{price / 1_000m:0}k/th";
        }

        if (price >= 1_000_000_000)
        {
            var billions = price / 1_000_000_000m;
            return $"{billions:0.#} tỷ";
        }
        if (price >= 1_000_000)
        {
            var millions = price / 1_000_000m;
            return $"{millions:0} tr";
        }

        return $"{price.ToString("N0", ViCulture)} ₫";
    }

    public static string FormatVnd(decimal amount)
    {
        return $"{amount.ToString("N0", ViCulture)} ₫";
    }

    public static string FormatArea(decimal area)
    {
        return $"{area.ToString("N0", ViCulture)} m²";
    }

    public static (string Label, string BadgeClass) GetStatusBadgeInfo(ListingStatus status)
    {
        return status switch
        {
            ListingStatus.Draft => ("Bản nháp", "bg-neutral-100 text-neutral-700 border-neutral-300"),
            ListingStatus.PendingPayment => ("Chờ thanh toán", "bg-blue-50 text-blue-700 border-blue-300"),
            ListingStatus.PendingModeration => ("Chờ duyệt", "bg-amber-50 text-amber-800 border-amber-300"),
            ListingStatus.Published => ("Đang hiển thị", "bg-emerald-50 text-emerald-800 border-emerald-300"),
            ListingStatus.Rejected => ("Bị từ chối", "bg-rose-50 text-rose-800 border-rose-300"),
            ListingStatus.Hidden => ("Đã tạm ẩn", "bg-zinc-100 text-zinc-700 border-zinc-300"),
            ListingStatus.Expired => ("Hết hạn", "bg-stone-100 text-stone-700 border-stone-300"),
            ListingStatus.Deleted => ("Đã xóa", "bg-red-100 text-red-900 border-red-300"),
            _ => (status.ToString(), "bg-gray-100 text-gray-700 border-gray-300")
        };
    }
}

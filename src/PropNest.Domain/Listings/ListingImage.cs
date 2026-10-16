namespace PropNest.Domain.Listings;

public sealed class ListingImage
{
    private ListingImage()
    {
    }

    public ListingImage(
        long listingId,
        string url,
        string publicId,
        int sortOrder,
        bool isCover = false,
        string? caption = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(listingId);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicId);

        ListingId = listingId;
        Url = url;
        PublicId = publicId;
        SortOrder = sortOrder;
        IsCover = isCover;
        Caption = caption;
        Status = "Active";
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public long ImageId { get; private set; }
    public long ListingId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string PublicId { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsCover { get; private set; }
    public string? Caption { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public void SetCover(bool isCover) => IsCover = isCover;

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    public void SetCaption(string? caption) => Caption = caption;
}

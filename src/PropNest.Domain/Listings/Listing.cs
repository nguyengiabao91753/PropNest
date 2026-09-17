using PropNest.Domain.Common;

namespace PropNest.Domain.Listings;

public sealed class Listing : AuditableEntity
{
    private Listing()
    {
    }

    public Listing(
        long ownerUserId,
        string title,
        string? description,
        string propertyType,
        string listingType,
        decimal price,
        decimal area,
        string city,
        string district,
        string? ward,
        string address)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ownerUserId);

        OwnerUserId = ownerUserId;
        ApplyContent(title, description, propertyType, listingType, price, area, city, district, ward, address);
        Status = ListingStatus.Draft;
        ModerationDecision = ModerationDecision.NotRequested;
    }

    public long ListingId { get; private set; }

    public long OwnerUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string PropertyType { get; private set; } = string.Empty;

    public string ListingType { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public decimal Area { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string District { get; private set; } = string.Empty;

    public string? Ward { get; private set; }

    public string Address { get; private set; } = string.Empty;

    public ListingStatus Status { get; private set; }

    public ModerationDecision ModerationDecision { get; private set; }

    public string PackageCode { get; private set; } = "Standard"; //gói dịch vụ

    public DateTimeOffset? StartDate { get; private set; }

    public DateTimeOffset? EndDate { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public ICollection<ListingHistory> Histories { get; private set; } = new List<ListingHistory>();

    public void UpdateContent(
        string title,
        string? description,
        string propertyType,
        string listingType,
        decimal price,
        decimal area,
        string city,
        string district,
        string? ward,
        string address)
    {
        EnsureNotDeleted();
        ApplyContent(title, description, propertyType, listingType, price, area, city, district, ward, address);

        if (Status is ListingStatus.Published or ListingStatus.Hidden)
        {
            Status = ListingStatus.PendingModeration;
            ModerationDecision = ModerationDecision.Pending;
        }

        Touch();
    }

    public void SubmitForModeration()
    {
        EnsureNotDeleted();
        if (Status is not (ListingStatus.Draft or ListingStatus.Rejected or ListingStatus.Hidden))
        {
            throw new InvalidOperationException($"Listing in {Status} cannot be submitted.");
        }

        Status = ListingStatus.PendingModeration;
        ModerationDecision = ModerationDecision.Pending;
        Touch();
    }

    public void Approve(DateTimeOffset startDate, DateTimeOffset endDate)
    {
        if (Status != ListingStatus.PendingModeration)
        {
            throw new InvalidOperationException("Only a listing pending moderation can be approved.");
        }

        if (endDate <= startDate)
        {
            throw new ArgumentException("The listing end date must be after the start date.", nameof(endDate));
        }

        Status = ListingStatus.Published;
        ModerationDecision = ModerationDecision.Approved;
        StartDate = startDate;
        EndDate = endDate;
        Touch();
    }

    public void Reject()
    {
        if (Status != ListingStatus.PendingModeration)
        {
            throw new InvalidOperationException("Only a listing pending moderation can be rejected.");
        }

        Status = ListingStatus.Rejected;
        ModerationDecision = ModerationDecision.Rejected;
        Touch();
    }

    public void Hide()
    {
        if (Status != ListingStatus.Published)
        {
            throw new InvalidOperationException("Only a published listing can be hidden.");
        }

        Status = ListingStatus.Hidden;
        Touch();
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status == ListingStatus.Published && EndDate <= now)
        {
            Status = ListingStatus.Expired;
            Touch();
        }
    }

    public void UpgradePackage(string packageCode, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        if (string.IsNullOrWhiteSpace(packageCode))
        {
            throw new ArgumentException("A package code is required.", nameof(packageCode));
        }

        if (endDate <= startDate)
        {
            throw new ArgumentException("The listing end date must be after the start date.", nameof(endDate));
        }

        PackageCode = packageCode;
        StartDate = startDate;
        EndDate = endDate;
        Touch();
    }

    private void ApplyContent(
        string title,
        string? description,
        string propertyType,
        string listingType,
        decimal price,
        decimal area,
        string city,
        string district,
        string? ward,
        string address)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(propertyType) ||
            string.IsNullOrWhiteSpace(listingType) || string.IsNullOrWhiteSpace(city) ||
            string.IsNullOrWhiteSpace(district) || string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Required listing content is missing.");
        }

        if (price < 0 || area <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be non-negative and area must be positive.");
        }

        Title = title.Trim();
        Description = description?.Trim();
        PropertyType = propertyType.Trim();
        ListingType = listingType.Trim();
        Price = price;
        Area = area;
        City = city.Trim();
        District = district.Trim();
        Ward = ward?.Trim();
        Address = address.Trim();
    }

    private void EnsureNotDeleted()
    {
        if (Status == ListingStatus.Deleted)
        {
            throw new InvalidOperationException("A deleted listing cannot be changed.");
        }
    }
}

using PropNest.Domain.Common;

namespace PropNest.Domain.Users;

public sealed class ApplicationUser : AuditableEntity
{
    private ApplicationUser()
    {
    }

    public ApplicationUser(string email, string fullName, string phoneNumber, string role = "Seller")
    {
        Email = email.Trim().ToLowerInvariant();
        FullName = fullName.Trim();
        PhoneNumber = phoneNumber.Trim();
        Role = role.Trim();
        Status = "Active";
    }

    public long UserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
}

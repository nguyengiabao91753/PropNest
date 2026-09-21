using Microsoft.AspNetCore.Identity;

namespace PropNest.Domain.Users;

public sealed class ApplicationUser : IdentityUser<long>
{
    public ApplicationUser()
    {
    }

    public ApplicationUser(string email, string fullName, string phoneNumber, string role = "Seller")
    {
        Email = email.Trim().ToLowerInvariant();
        UserName = email.Trim().ToLowerInvariant();
        FullName = fullName.Trim();
        PhoneNumber = phoneNumber.Trim();
        Role = role.Trim();
        Status = "Active";
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public long UserId => Id;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

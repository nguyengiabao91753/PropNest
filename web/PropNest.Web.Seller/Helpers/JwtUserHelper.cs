using System.Text;
using System.Text.Json;

namespace PropNest.Web.Seller.Helpers;

public sealed class SellerUserProfile
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public bool IsSeller => string.Equals(Role, "Seller", StringComparison.OrdinalIgnoreCase);
    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
}

public static class JwtUserHelper
{
    public static SellerUserProfile? GetProfileFromToken(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var profile = new SellerUserProfile();

            // 1. Email
            profile.Email = GetFirstString(root, "email", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") ?? string.Empty;

            // 2. Full Name
            var name = GetFirstString(root, "name", "unique_name", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                profile.FullName = name;
            }
            else if (!string.IsNullOrWhiteSpace(profile.Email))
            {
                var atIdx = profile.Email.IndexOf('@');
                profile.FullName = atIdx > 0 ? profile.Email[..atIdx] : profile.Email;
            }

            // 3. User Id
            var sub = GetFirstString(root, "sub", "user_id", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            if (long.TryParse(sub, out var userId))
            {
                profile.UserId = userId;
            }

            // 4. Phone Number
            profile.PhoneNumber = GetFirstString(root, "phone_number", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/mobilephone") ?? "Chưa cập nhật";

            // 5. Role
            profile.Role = GetFirstString(root, "role", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? "Customer";

            // 6. Status
            profile.Status = GetFirstString(root, "status") ?? "Active";

            return profile;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetFirstString(JsonElement root, params string[] propertyNames)
    {
        foreach (var prop in propertyNames)
        {
            if (root.TryGetProperty(prop, out var element))
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    return element.GetString();
                }
                if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
                {
                    return element[0].GetString();
                }
            }
        }
        return null;
    }
}

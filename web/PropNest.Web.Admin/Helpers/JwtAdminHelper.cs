using System.Text;
using System.Text.Json;

namespace PropNest.Web.Admin.Helpers;

public sealed class AdminUserProfile
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public string Status { get; set; } = "Active";

    public bool IsAdmin => Roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)
                                       || string.Equals(r, "Moderator", StringComparison.OrdinalIgnoreCase));

    public bool HasRole(string role) => Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}

public static class JwtAdminHelper
{
    public static AdminUserProfile? GetProfileFromToken(string? jwt)
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

            var profile = new AdminUserProfile();

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
            profile.PhoneNumber = GetFirstString(root, "phone_number", "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/mobilephone") ?? "0901234567";

            // 5. Roles
            var roles = GetAllStrings(root, "role", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
            profile.Roles = roles.Count > 0 ? roles : new List<string> { "Admin" };

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

    private static List<string> GetAllStrings(JsonElement root, params string[] propertyNames)
    {
        var results = new List<string>();
        foreach (var prop in propertyNames)
        {
            if (root.TryGetProperty(prop, out var element))
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var val = element.GetString();
                    if (!string.IsNullOrWhiteSpace(val) && !results.Contains(val))
                    {
                        results.Add(val);
                    }
                }
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        var val = item.GetString();
                        if (!string.IsNullOrWhiteSpace(val) && !results.Contains(val))
                        {
                            results.Add(val);
                        }
                    }
                }
            }
        }
        return results;
    }
}

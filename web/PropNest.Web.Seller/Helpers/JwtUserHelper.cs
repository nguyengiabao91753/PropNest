using System.Security.Cryptography;
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
    private static string _signingKey = string.Empty;
    private static string _issuer = "PropNest";
    private static string _audience = "PropNest.Client";

    public static void Configure(IConfiguration configuration)
    {
        _signingKey = configuration["Jwt:SigningKey"] ?? string.Empty;
        _issuer = configuration["Jwt:Issuer"] ?? "PropNest";
        _audience = configuration["Jwt:Audience"] ?? "PropNest.Client";
    }

    public static SellerUserProfile? GetProfileFromToken(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt) || string.IsNullOrWhiteSpace(_signingKey))
        {
            return null;
        }

        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
            {
                return null;
            }

            if (!VerifyHeaderAndSignature(parts[0], parts[1], parts[2]))
            {
                return null;
            }

            var payloadBytes = DecodeBase64Url(parts[1]);
            using var doc = JsonDocument.Parse(payloadBytes);
            var root = doc.RootElement;

            if (!IsTokenClaimsValid(root))
            {
                return null;
            }

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

            if (!string.Equals(profile.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return profile;
        }
        catch
        {
            return null;
        }
    }

    private static bool VerifyHeaderAndSignature(string headerSegment, string payloadSegment, string signatureSegment)
    {
        var headerBytes = DecodeBase64Url(headerSegment);
        using var headerDoc = JsonDocument.Parse(headerBytes);
        if (!headerDoc.RootElement.TryGetProperty("alg", out var algProp) ||
            !string.Equals(algProp.GetString(), "HS256", StringComparison.Ordinal))
        {
            return false;
        }

        var actualSignature = DecodeBase64Url(signatureSegment);
        var signingInput = Encoding.ASCII.GetBytes($"{headerSegment}.{payloadSegment}");
        var keyBytes = Encoding.UTF8.GetBytes(_signingKey);
        var expectedSignature = HMACSHA256.HashData(keyBytes, signingInput);

        return actualSignature.Length == expectedSignature.Length &&
               CryptographicOperations.FixedTimeEquals(actualSignature, expectedSignature);
    }

    private static bool IsTokenClaimsValid(JsonElement root)
    {
        if (!root.TryGetProperty("exp", out var expProp) || !expProp.TryGetInt64(out var expUnix))
        {
            return false;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        if (expiresAt <= DateTimeOffset.UtcNow.AddMinutes(-1))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_issuer))
        {
            var iss = GetFirstString(root, "iss");
            if (!string.Equals(iss, _issuer, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(_audience))
        {
            var audiences = GetAllStrings(root, "aud");
            if (!audiences.Contains(_audience, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] DecodeBase64Url(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
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
                    if (!string.IsNullOrWhiteSpace(val)) results.Add(val);
                }
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        var val = item.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) results.Add(val);
                    }
                }
            }
        }
        return results;
    }
}

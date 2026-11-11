namespace PropNest.Api.Contracts.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string PhoneNumber);

public sealed record RegisterResponse(long UserId, string Email);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken);

public sealed record UserProfileDto(
    long UserId,
    string Email,
    string FullName,
    string PhoneNumber,
    string Role,
    string Status,
    DateTimeOffset CreatedAt);
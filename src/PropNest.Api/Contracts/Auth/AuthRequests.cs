namespace PropNest.Api.Contracts.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string PhoneNumber);

public sealed record RegisterResponse(long UserId, string Email);

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken);
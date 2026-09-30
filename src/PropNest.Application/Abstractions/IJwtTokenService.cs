using PropNest.Domain.Users;

namespace PropNest.Application.Abstractions;

public interface IJwtTokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
}
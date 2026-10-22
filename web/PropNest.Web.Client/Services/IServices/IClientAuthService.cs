using PropNest.Web.Client.Models.ViewModels;

namespace PropNest.Web.Client.Services.IServices
{
    public interface IClientAuthService
    {
        Task<AuthResult> LoginAsync(LoginViewModel model, CancellationToken cancellationToken = default);
        Task<AuthResult> RegisterAsync(SignupViewModel model, CancellationToken cancellationToken = default);
        Task<UserProfileViewModel?> GetUserProfileAsync(CancellationToken cancellationToken = default);
    }
}

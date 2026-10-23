using PropNest.Web.Client.Models.ViewModels;
using PropNest.Web.Client.Services.IServices;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PropNest.Web.Client.Services
{
    public partial class ApiClientAuthService : IClientAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiClientAuthService> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
        [LoggerMessage(Level = LogLevel.Error, Message = "Không thể kết nối tới Backend API tại {Url}")]
        private static partial void LogApiConnectionError(ILogger logger, Exception ex, string url);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Không thể lấy thông tin người dùng từ {Url}")]
        private static partial void LogGetUserProfileWarning(ILogger logger, Exception ex, string url);
        public ApiClientAuthService(HttpClient httpClient, ILogger<ApiClientAuthService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            var baseUrl = configuration["ServiceUrls:BackendApi"] ?? "https://localhost:65147";
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
        }
        public async Task<AuthResult> LoginAsync(LoginViewModel model, CancellationToken cancellationToken = default)
        {
            const string url = "/api/v1/auth/login";
            try
            {
                var payload = new ApiLoginRequestDto(model.Email.Trim(), model.Password);
                var response = await _httpClient.PostAsJsonAsync(url, payload, _jsonOptions, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ApiLoginResponseDto>(_jsonOptions, cancellationToken);
                    return AuthResult.Success(result?.AccessToken);
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return AuthResult.Failed("Địa chỉ email hoặc mật khẩu không chính xác.");
                }

                return AuthResult.Failed("Đăng nhập không thành công. Vui lòng thử lại sau.");
            }
            catch (Exception ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return AuthResult.Failed("Không thể kết nối tới máy chủ xác thực. Vui lòng thử lại sau.");
            }
        }

        public async Task<AuthResult> RegisterAsync(SignupViewModel model, CancellationToken cancellationToken = default)
        {
            const string url = "/api/v1/auth/register";
            try
            {
                var payload = new ApiRegisterDto(
                    Email: model.Email.Trim(),
                    Password: model.Password,
                    FullName: model.FullName.Trim(),
                    PhoneNumber: model.PhoneNumber.Trim()
                );

                var response = await _httpClient.PostAsJsonAsync(url, payload, _jsonOptions, cancellationToken);

                // 1. Đăng ký thành công (HTTP 201 Created hoặc 200 OK)
                if (response.IsSuccessStatusCode)
                {
                    return AuthResult.Success();
                }

                // 2. Xử lý lỗi từ Backend trả về
                var validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                string generalMessage = "Đăng ký không thành công. Vui lòng kiểm tra lại thông tin.";

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var errorResponse = await response.Content.ReadFromJsonAsync<ApiErrorResponseDto>(_jsonOptions, cancellationToken);

                    if (errorResponse?.Errors != null && errorResponse.Errors.Count > 0)
                    {
                        foreach (var err in errorResponse.Errors)
                        {
                            var code = err.Code ?? string.Empty;
                            var desc = err.Description ?? string.Empty;

                            // Map mã lỗi từ ASP.NET Identity vào đúng ô input trên View
                            if (code.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
                                code.Contains("UserName", StringComparison.OrdinalIgnoreCase))
                            {
                                validationErrors["Email"] = new[] { "Địa chỉ email này đã được sử dụng. Vui lòng chọn email khác." };
                            }
                            else if (code.Contains("Password", StringComparison.OrdinalIgnoreCase))
                            {
                                var viDesc = desc;
                                if (code.Equals("PasswordTooShort", StringComparison.OrdinalIgnoreCase) || (desc.Contains("at least", StringComparison.OrdinalIgnoreCase) && desc.Contains("character", StringComparison.OrdinalIgnoreCase)))
                                {
                                    viDesc = "Mật khẩu phải có tối thiểu 6 ký tự.";
                                }
                                else if (code.Equals("PasswordRequiresLower", StringComparison.OrdinalIgnoreCase) || desc.Contains("lowercase", StringComparison.OrdinalIgnoreCase))
                                {
                                    viDesc = "Mật khẩu phải chứa ít nhất 1 chữ cái viết thường ('a'-'z').";
                                }
                                else if (code.Equals("PasswordRequiresUpper", StringComparison.OrdinalIgnoreCase) || desc.Contains("uppercase", StringComparison.OrdinalIgnoreCase))
                                {
                                    viDesc = "Mật khẩu phải chứa ít nhất 1 chữ cái in hoa ('A'-'Z').";
                                }
                                else if (code.Equals("PasswordRequiresDigit", StringComparison.OrdinalIgnoreCase) || desc.Contains("digit", StringComparison.OrdinalIgnoreCase))
                                {
                                    viDesc = "Mật khẩu phải chứa ít nhất 1 chữ số ('0'-'9').";
                                }
                                else if (code.Equals("PasswordRequiresNonAlphanumeric", StringComparison.OrdinalIgnoreCase) || desc.Contains("non alphanumeric", StringComparison.OrdinalIgnoreCase))
                                {
                                    viDesc = "Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt (ví dụ: !@#$%^&*).";
                                }

                                validationErrors["Password"] = new[] { viDesc };
                            }
                            else
                            {
                                // Lỗi chung (hiển thị ở Validation Summary trên đầu Form)
                                validationErrors[string.Empty] = new[] { desc };
                            }
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(errorResponse?.Message))
                    {
                        generalMessage = errorResponse.Message;
                    }
                }

                return AuthResult.Failed(generalMessage, validationErrors);
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return AuthResult.Failed("Không thể kết nối tới máy chủ xác thực. Vui lòng kiểm tra lại đường truyền.");
            }
        }

        public async Task<UserProfileViewModel?> GetUserProfileAsync(CancellationToken cancellationToken = default)
        {
            const string url = "/api/v1/auth/me";
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var dto = await response.Content.ReadFromJsonAsync<ApiUserProfileDto>(_jsonOptions, cancellationToken);
                    if (dto != null)
                    {
                        return new UserProfileViewModel
                        {
                            UserId = dto.UserId,
                            Email = dto.Email,
                            FullName = dto.FullName,
                            PhoneNumber = dto.PhoneNumber,
                            Role = dto.Role,
                            Status = dto.Status,
                            CreatedAt = dto.CreatedAt
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                LogGetUserProfileWarning(_logger, ex, url);
            }

            return null;
        }

        private sealed record ApiUserProfileDto(
            long UserId,
            string Email,
            string FullName,
            string PhoneNumber,
            string Role,
            string Status,
            DateTimeOffset CreatedAt);

        private sealed record ApiLoginRequestDto(
            string Email,
            string Password
            );

        private sealed record ApiLoginResponseDto(
            string AccessToken
            );

        private sealed record ApiRegisterDto(
            string Email,
            string Password,
            string FullName,
            string PhoneNumber
            );

        private sealed record ApiRegisterResponseDto(
            long UserId,
            string Email
            );

        private sealed record ApiErrorDetailDto(string? Code, string? Description);
        private sealed record ApiErrorResponseDto(string? Message, List<ApiErrorDetailDto>? Errors);
    }
}

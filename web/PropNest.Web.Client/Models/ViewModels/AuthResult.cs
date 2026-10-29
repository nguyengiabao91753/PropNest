namespace PropNest.Web.Client.Models.ViewModels
{
    public sealed class AuthResult
    {
        public bool Succeeded { get; set; }
    public string? Token { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string[]> Errors { get; set; } = new();
    public static AuthResult Success(string? token = null) =>
        new() { Succeeded = true, Token = token };
    public static AuthResult Failed(string errorMessage, Dictionary<string, string[]>? errors = null) =>
        new() { Succeeded = false, ErrorMessage = errorMessage, Errors = errors ?? new() };
    }
}

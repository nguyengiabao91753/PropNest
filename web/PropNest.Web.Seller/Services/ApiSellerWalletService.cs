using System.Net.Http.Json;
using System.Text.Json;
using PropNest.Domain.Wallets;
using PropNest.Web.Seller.Models.ViewModels;
using PropNest.Web.Seller.Services.IServices;

namespace PropNest.Web.Seller.Services
{
    public partial class ApiSellerWalletService : ISellerWalletService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiSellerWalletService> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        [LoggerMessage(Level = LogLevel.Error, Message = "Không thể kết nối tới Backend API tại {Url}")]
        private static partial void LogApiConnectionError(ILogger logger, Exception ex, string url);

        [LoggerMessage(Level = LogLevel.Error, Message = "TopUpWallet thất bại tại {Url}. Status: {StatusCode}, Chi tiết: {Details}")]
        private static partial void LogTopUpFailed(ILogger logger, string url, System.Net.HttpStatusCode statusCode, string details);

        public ApiSellerWalletService(HttpClient httpClient, ILogger<ApiSellerWalletService> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            var baseUrl = configuration["ServiceUrls:BackendApi"] ?? "https://localhost:65147";
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
        }

        public async Task<WalletViewModel> GetWalletAsync(int page = 1, string? type = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        {
            var validPage = page < 1 ? 1 : page;
            var model = new WalletViewModel
            {
                PageNumber = validPage,
                PageSize = 10,
                CurrentType = string.IsNullOrWhiteSpace(type) ? "all" : type.Trim(),
                SearchTerm = searchTerm?.Trim()
            };

            var walletUrl = "/api/v1/wallets/me";
            try
            {
                var walletDto = await _httpClient.GetFromJsonAsync<ApiWalletDto>(walletUrl, _jsonOptions, cancellationToken);
                if (walletDto != null)
                {
                    model.MainBalance = walletDto.MainBalance;
                    model.PromoBalance = walletDto.PromoBalance;
                    model.TotalBalance = walletDto.TotalBalance;
                }
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, walletUrl);
            }

            // 1.2. Lấy toàn bộ lịch sử giao dịch để thống kê số lượng theo từng Tab & phân trang đồng bộ
            var allTransactions = new List<WalletTransactionViewModel>();
            var currentPage = 1;
            while (true)
            {
                var transactionsUrl = $"/api/v1/wallets/me/transactions?page={currentPage}";
                try
                {
                    var chunk = await _httpClient.GetFromJsonAsync<List<ApiTransactionDto>>(transactionsUrl, _jsonOptions, cancellationToken);
                    if (chunk == null || chunk.Count == 0)
                    {
                        break;
                    }

                    allTransactions.AddRange(chunk.Select(x => new WalletTransactionViewModel
                    {
                        Id = x.TransactionId,
                        CorrelationId = x.CorrelationId.ToString(),
                        Type = x.Type.ToString(),
                        Amount = x.Amount,
                        BalanceAfter = x.BalanceAfter,
                        Description = x.Description ?? string.Empty,
                        CreatedAt = x.CreatedAt
                    }));

                    if (chunk.Count < 10)
                    {
                        break;
                    }
                    currentPage++;
                    if (currentPage > 20) // Giới hạn an toàn tối đa 20 trang (200 giao dịch)
                    {
                        break;
                    }
                }
                catch (HttpRequestException ex)
                {
                    LogApiConnectionError(_logger, ex, transactionsUrl);
                    break;
                }
            }

            // 1.3. Thống kê số lượng cho các tab
            model.Counts = new WalletTransactionCountsViewModel
            {
                All = allTransactions.Count,
                TopUp = allTransactions.Count(x => string.Equals(x.Type, "TopUp", StringComparison.OrdinalIgnoreCase)),
                Charge = allTransactions.Count(x => string.Equals(x.Type, "Charge", StringComparison.OrdinalIgnoreCase)),
                Refund = allTransactions.Count(x => string.Equals(x.Type, "Refund", StringComparison.OrdinalIgnoreCase)),
                PromoReward = allTransactions.Count(x => string.Equals(x.Type, "PromoReward", StringComparison.OrdinalIgnoreCase))
            };

            // 1.4. Lọc theo Tab loại giao dịch
            var filtered = allTransactions.AsEnumerable();
            if (!string.Equals(model.CurrentType, "all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => string.Equals(x.Type, model.CurrentType, StringComparison.OrdinalIgnoreCase));
            }

            // 1.5. Lọc theo từ khóa tìm kiếm (mô tả, mã giao dịch, correlation id)
            if (!string.IsNullOrWhiteSpace(model.SearchTerm))
            {
                var term = model.SearchTerm;
                filtered = filtered.Where(x =>
                    x.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    x.Id.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(x.CorrelationId) && x.CorrelationId.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            var filteredList = filtered.ToList();
            model.TotalCount = filteredList.Count;

            // 1.6. Cắt trang hiển thị
            model.Transactions = filteredList
                .Skip((validPage - 1) * model.PageSize)
                .Take(model.PageSize)
                .ToList();

            return model;
        }

        public async Task<bool> TopUpWalletAsync(decimal amount, string? description = null, CancellationToken cancellationToken = default)
        {
            var url = "/api/v1/wallets/top-up";
            try
            {
                var request = new ApiTopUpWalletRequest(
                    Amount: amount,
                    Description: string.IsNullOrWhiteSpace(description) ? "Nạp tiền vào ví" : description.Trim()
                );

                var response = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    LogTopUpFailed(_logger, url, response.StatusCode, errorBody);
                    return false;
                }

                return true;
            }
            catch (HttpRequestException ex)
            {
                LogApiConnectionError(_logger, ex, url);
                return false;
            }
        }

        private sealed class ApiWalletDto
        {
            public long WalletId { get; set; }
            public long UserId { get; set; }
            public decimal MainBalance { get; set; }
            public decimal PromoBalance { get; set; }
            public decimal TotalBalance { get; set; }
        }

        private sealed class ApiTransactionDto
        {
            public long TransactionId { get; set; }
            public Guid CorrelationId { get; set; }
            public WalletTransactionType Type { get; set; }
            public decimal Amount { get; set; }
            public decimal BalanceBefore { get; set; }
            public decimal BalanceAfter { get; set; }
            public string? Description { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private sealed record ApiTopUpWalletRequest(
            decimal Amount,
            string? Description = null
        );
    }
}

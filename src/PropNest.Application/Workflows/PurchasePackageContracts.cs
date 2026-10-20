using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Application.Workflows
{
    /// <summary>
    /// Event khởi đầu kích hoạt Saga mua gói
    /// </summary>
    public sealed record PurchasePackageRequested(
        Guid CorrelationId,
        long ListingId,
        long UserId,
        string PackageCode,
        decimal ChargeAmount);

    /// <summary>
    /// Command yêu cầu trừ tiền ví
    /// </summary>
    public sealed record DeductWalletCommand(
        Guid CorrelationId,
        long UserId,
        decimal Amount);

    /// <summary>
    /// Event thông báo trừ tiền ví thành công
    /// </summary>
    public sealed record WalletDeducted(
        Guid CorrelationId,
        long UserId,
        decimal Amount);

    /// <summary>
    /// Event thông báo trừ tiền ví thất bại
    /// </summary>
    public sealed record WalletDeductionFailed(
        Guid CorrelationId,
        long UserId,
        string Reason);

    /// <summary>
    /// Command yêu cầu nâng cấp gói tin đăng
    /// </summary>
    public sealed record UpgradeListingCommand(
        Guid CorrelationId,
        long ListingId,
        string PackageCode);

    /// <summary>
    /// Event thông báo nâng cấp tin đăng thành công
    /// </summary>
    public sealed record ListingUpgraded(
        Guid CorrelationId,
        long ListingId,
        string PackageCode);

    /// <summary>
    /// Event thông báo nâng cấp tin đăng thất bại
    /// </summary>
    public sealed record ListingUpgradeFailed(
        Guid CorrelationId,
        long ListingId,
        string Reason);

    /// <summary>
    /// Command yêu cầu hoàn tiền ví (Compensation)
    /// </summary>
    public sealed record RefundWalletCommand(
        Guid CorrelationId,
        long UserId,
        decimal Amount);

    /// <summary>
    /// Event thông báo hoàn tiền ví thành công
    /// </summary>
    public sealed record WalletRefunded(
        Guid CorrelationId,
        long UserId,
        decimal Amount);
}

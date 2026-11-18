using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Application.Workflows;
using PropNest.Domain.Listings;
using PropNest.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Infrastructure.Consumers
{
    public partial class UpgradeListingConsumer : IConsumer<UpgradeListingCommand>
    {
        private readonly PropNestDbContext _dbContext;
        private readonly IListingRepository _listingRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpgradeListingConsumer> _logger;

        public UpgradeListingConsumer(
            PropNestDbContext dbContext,
            IListingRepository listingRepository,
            IUnitOfWork unitOfWork,
            ILogger<UpgradeListingConsumer> logger)
        {
            _dbContext = dbContext;
            _listingRepository = listingRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }
        public async Task Consume(ConsumeContext<UpgradeListingCommand> context)
        {
            var command = context.Message;
            LogConsuming(_logger, command.CorrelationId, command.ListingId, command.PackageCode);

            //check idempotency
            var alreadyProcessed = await _dbContext.ListingHistories.AnyAsync(
                    h =>h.ListingId == command.ListingId &&
                        h.ActionType == "UpgradePackage" &&
                        h.Note != null && 
                        h.Note.Contains(command.CorrelationId.ToString()),
                        context.CancellationToken
                );
            if (alreadyProcessed)
            {
                LogAlreadyProcessed(_logger, command.CorrelationId);
                await context.Publish(new ListingUpgraded(
                        command.CorrelationId,
                        command.ListingId,
                        command.PackageCode
                    ));
                return;
            }

            //tìm listing
            var listing = await _listingRepository.GetByIdAsync( command.ListingId, context.CancellationToken);
            if(listing is null)
            {
                LogFailed(_logger, command.CorrelationId, $"Tin đăng với ID {command.ListingId} không tồn tại.");
                await context.Publish(new ListingUpgradeFailed(
                        command.CorrelationId,
                        command.ListingId,
                        $"Tin đăng với ID {command.ListingId} không tồn tại."
                    ));
                return;
            }

            if (!ListingPackagePolicy.CanPurchasePackage(listing.Status, listing.ModerationDecision))
            {
                const string reason = "Chỉ tin đăng đã được duyệt (Chờ thanh toán, Đang hiển thị hoặc Hết hạn) mới có thể mua hoặc gia hạn gói.";
                LogFailed(_logger, command.CorrelationId, reason);
                await context.Publish(new ListingUpgradeFailed(
                    command.CorrelationId,
                    command.ListingId,
                    reason));
                return;
            }

            // lấy thông tin gói
            var packageCode = command.PackageCode?.Trim() ?? string.Empty;
            var package = string.IsNullOrEmpty(packageCode)
                ? null
                : await _listingRepository.GetPackageByCodeAsync(packageCode, context.CancellationToken);
            if (package is null || !ListingPackagePolicy.IsPurchasablePackage(package.Kind, package.Price, package.IsActive))
            {
                var invalidPackageReason = $"Gói dịch vụ '{command.PackageCode}' không tồn tại hoặc đã ngừng áp dụng.";
                LogFailed(_logger, command.CorrelationId, invalidPackageReason);
                await context.Publish(new ListingUpgradeFailed(
                    command.CorrelationId,
                    command.ListingId,
                    invalidPackageReason));
                return;
            }

            // áp dụng gói theo lịch Việt Nam (23:59:59 +07:00) và ghi history
            var now = DateTimeOffset.UtcNow;
            try
            {
                listing.ApplyPackagePurchase(package.Kind, package.DurationDays, now);

                var vnEnd = listing.EndDate?.ToOffset(ListingPackagePolicy.VietnamUtcOffset);
                var noteDetail = listing.PackageEndDate.HasValue
                    && !string.Equals(listing.PackageCode, ListingPackagePolicy.StandardPackageCode, StringComparison.OrdinalIgnoreCase)
                    ? $"Mua gói {package.PackageCode} (+{package.DurationDays} ngày, {listing.PackageCode} đến: {listing.PackageEndDate.Value.ToOffset(ListingPackagePolicy.VietnamUtcOffset):yyyy-MM-dd HH:mm}, hiển thị đến: {vnEnd:yyyy-MM-dd HH:mm}) - CorrelationId: {command.CorrelationId}"
                    : $"Mua gói {package.PackageCode} (+{package.DurationDays} ngày, hiển thị đến: {vnEnd:yyyy-MM-dd HH:mm}) - CorrelationId: {command.CorrelationId}";

                var history = ListingHistory.CreateEvent(
                    listing.ListingId,
                    listing.OwnerUserId,
                    "UpgradePackage",
                    noteDetail);

                _listingRepository.AddHistory(history);
                await _unitOfWork.SaveChangesAsync(context.CancellationToken);
            }
            catch (Exception ex)
            {
                LogFailed(_logger, command.CorrelationId, ex.Message);
                await context.Publish(new ListingUpgradeFailed(
                    command.CorrelationId,
                    command.ListingId,
                    ex.Message));
                return;
            }

            //publish event thành công
            LogSuccess(_logger, command.CorrelationId, command.ListingId, package.PackageCode);

            await context.Publish(new ListingUpgraded(
                command.CorrelationId,
                command.ListingId,
                package.PackageCode
                ));
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "UpgradeListingConsumer: Processing UpgradeListingCommand for CorrelationId: {CorrelationId}, ListingId: {ListingId}, PackageCode: {PackageCode}")]
        private static partial void LogConsuming(ILogger logger, Guid correlationId, long listingId, string packageCode);
        [LoggerMessage(Level = LogLevel.Warning, Message = "UpgradeListingConsumer: CorrelationId {CorrelationId} has already been upgraded. Skipping duplicate.")]
        private static partial void LogAlreadyProcessed(ILogger logger, Guid correlationId);
        [LoggerMessage(Level = LogLevel.Warning, Message = "UpgradeListingConsumer: Failed to upgrade listing for CorrelationId: {CorrelationId}. Reason: {Reason}")]
        private static partial void LogFailed(ILogger logger, Guid correlationId, string reason);
        [LoggerMessage(Level = LogLevel.Information, Message = "UpgradeListingConsumer: Successfully upgraded listing {ListingId} to package {PackageCode} for CorrelationId: {CorrelationId}")]
        private static partial void LogSuccess(ILogger logger, Guid correlationId, long listingId, string packageCode);
    }
}



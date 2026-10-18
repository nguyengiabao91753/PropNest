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

            // check trạng thái listing
            if (listing.Status == ListingStatus.Deleted)
            {
                LogFailed(_logger, command.CorrelationId, "Không thể nâng cấp gói cho tin đăng đã bị xóa.");
                await context.Publish(new ListingUpgradeFailed(
                    command.CorrelationId,
                    command.ListingId,
                    "Không thể nâng cấp gói cho tin đăng đã bị xóa."));
                return;
            }

            // lấy thông tin gói
            var package = await _listingRepository.GetPackageByCodeAsync(command.PackageCode, context.CancellationToken);
            if (package is null || !package.IsActive)
            {
                LogFailed(_logger, command.CorrelationId, $"Gói dịch vụ '{command.PackageCode}' không tồn tại hoặc đã ngừng áp dụng.");
                await context.Publish(new ListingUpgradeFailed(
                    command.CorrelationId,
                    command.ListingId,
                    $"Gói dịch vụ '{command.PackageCode}' không tồn tại hoặc đã ngừng áp dụng."));
                return;
            }

            // tính start - end
            var now = DateTimeOffset.UtcNow;
            var isStillActive = listing.EndDate.HasValue && listing.EndDate.Value > now;

            //neeys tin đăng còn hạn thì cộng dồn ngày nối tiếp, nếu hết hạn thì tính từ thời điểm hiện tại
            var startDate = (isStillActive && listing.StartDate.HasValue) ? listing.StartDate.Value : now;
            var baseDate = isStillActive ? listing.EndDate!.Value : now;
            var endDate = baseDate.AddDays(package.DurationDays);

            //nâng cấp tin và ghi history
            try
            {
                listing.UpgradePackage(command.PackageCode, startDate, endDate);

                var history = ListingHistory.CreateEvent(
                    listing.ListingId,
                    listing.OwnerUserId,
                    "UpgradePackage",
                    $"Nâng cấp gói {command.PackageCode} (+{package.DurationDays} ngày, HSD đến: {endDate:yyyy-MM-dd HH:mm}) - CorrelationId: {command.CorrelationId}");

                _listingRepository.AddHistory( history );
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
            LogSuccess(_logger, command.CorrelationId, command.ListingId, command.PackageCode);

            await context.Publish(new ListingUpgraded(
                command.CorrelationId,
                command.ListingId,
                command.PackageCode
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



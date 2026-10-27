namespace PropNest.Web.Seller.Models.ViewModels
{
    public sealed class WorkflowStatusViewModel
    {
        public Guid CorrelationId { get; set; }
        public long ListingId { get; set; }
        public string PackageCode { get; set; } = string.Empty;
        public decimal ChargeAmount { get; set; }
        public int Status { get; set; }
        public string? ErrorMessage { get; set; }

        // Tự động tính toán dựa trên Status của Saga:
        // 0: Started, 1: DeductingWallet, 2: WalletDeducted, 3: UpgradingListing, 4: RecordingHistory, 5: Completed
        // 6: Compensating, 7: Compensated, 8: Failed
        public bool IsCompleted => Status == 5;
        public bool IsFailed => Status is 7 or 8;
        public int CurrentStepIndex => Status switch
        {
            0 => 1,       // 1. Khởi tạo
            1 or 2 => 2,  // 2. Trừ tiền ví
            3 or 4 => 3,  // 3. Nâng cấp tin đăng
            5 => 4,       // 4. Hoàn tất
            _ => 0
        };
        public string StatusText => Status switch
        {
            0 => "Đang khởi tạo yêu cầu...",
            1 => "Đang khấu trừ số dư ví...",
            2 => "Đã trừ tiền ví thành công...",
            3 => "Đang kích hoạt gói tin...",
            4 => "Đang ghi nhận lịch sử...",
            5 => "Nâng cấp gói thành công!",
            6 => "Đang tiến hành bồi hoàn ví...",
            7 => "Giao dịch không thành công. Tiền đã được bồi hoàn vào ví.",
            8 => "Giao dịch thất bại. Tiền không bị trừ.",
            _ => "Đang xử lý..."
        };
    }
}

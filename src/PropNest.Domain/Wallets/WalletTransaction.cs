namespace PropNest.Domain.Wallets;

public enum WalletTransactionType
{
    TopUp,
    Charge,
    Refund,
    PromoReward
}

public sealed class WalletTransaction
{
    private WalletTransaction()
    {
    }

    public WalletTransaction(long walletId, Guid correlationId, WalletTransactionType type, decimal amount, decimal balanceBefore, decimal balanceAfter, string description)
    {
        WalletId = walletId;
        CorrelationId = correlationId;
        TransactionType = type;
        Amount = amount;
        BalanceBefore = balanceBefore;
        BalanceAfter = balanceAfter;
        Description = description;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public long TransactionId { get; private set; }
    public long WalletId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public WalletTransactionType TransactionType { get; private set; }
    public decimal Amount { get; private set; }
    public decimal BalanceBefore { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}

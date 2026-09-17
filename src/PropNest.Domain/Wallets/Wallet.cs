using PropNest.Domain.Common;

namespace PropNest.Domain.Wallets;

public sealed class Wallet : AuditableEntity
{
    private Wallet()
    {
    }

    public Wallet(long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        UserId = userId;
    }

    public long WalletId { get; private set; }

    public long UserId { get; private set; }

    public decimal MainBalance { get; private set; }

    public decimal PromoBalance { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public void CreditMain(decimal amount)
    {
        EnsurePositive(amount);
        MainBalance += amount;
        Touch();
    }

    public (decimal PromoUsed, decimal MainUsed) Charge(decimal amount)
    {
        EnsurePositive(amount);
        if (MainBalance + PromoBalance < amount)
        {
            throw new InvalidOperationException("Insufficient wallet balance.");
        }

        var promoUsed = Math.Min(PromoBalance, amount);
        var mainUsed = amount - promoUsed;
        PromoBalance -= promoUsed;
        MainBalance -= mainUsed;
        Touch();
        return (promoUsed, mainUsed);
    }

    public void Refund(decimal amount)
    {
        EnsurePositive(amount);
        MainBalance += amount;
        Touch();
    }

    private static void EnsurePositive(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
    }
}

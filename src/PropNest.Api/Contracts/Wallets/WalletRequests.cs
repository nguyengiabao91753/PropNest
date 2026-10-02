namespace PropNest.Api.Contracts.Wallets
{
    public sealed record TopUpWalletRequest(
    decimal Amount,
    string? Description = null);

}

namespace ErpMcp.Domain.Customers;

public sealed record Customer(
    long Id,
    string Code,
    string Name,
    string Email,
    string? Phone,
    string City,
    string CountryCode,
    decimal CreditLimit,
    AccountStatus Status)
{
    /// <summary>Only active accounts may place new orders.</summary>
    public bool CanPlaceOrders => Status == AccountStatus.Active;
}

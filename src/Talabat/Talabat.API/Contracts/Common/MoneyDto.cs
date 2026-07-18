namespace Talabat.Customer.API.Contracts.Common;

public sealed record MoneyDto(decimal Amount, string Currency = "EGP");

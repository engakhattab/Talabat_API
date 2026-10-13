using System.Text.Json;
using System.Text.Json.Serialization;

namespace Talabat.Infrastructure.Development.E2E;

public enum E2EProvisioningOperation
{
    Prepare,
    MakeNegativeProductUnavailable,
    Restore
}

public interface IE2EProvisioner
{
    Task<E2EProvisioningResult> ExecuteAsync(
        E2EProvisioningOperation operation,
        CancellationToken cancellationToken = default);
}

public sealed record E2EProvisioningResult(
    int Version,
    string Operation,
    string Environment,
    E2ECustomerResult Customer,
    E2EAddressesResult Addresses,
    E2ECatalogResult Catalog);

public sealed record E2ECustomerResult(string Email, string DisplayName);

public sealed record E2EAddressesResult(
    E2EAddressResult Default,
    E2EAddressResult Alternate);

public sealed record E2EAddressResult(int Id, string Label);

public sealed record E2ECatalogResult(
    E2ERestaurantResult Restaurant,
    E2EProductResult HappyProduct,
    E2EProductResult NegativeProduct);

public sealed record E2ERestaurantResult(int Id, string Name);

public sealed record E2EProductResult(int Id, string Name, bool Available);

public static class E2EProvisioningJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(E2EProvisioningResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result, SerializerOptions);
    }
}

public sealed class E2EProvisioningException : Exception
{
    public E2EProvisioningException(string code, string safeMessage)
        : base(safeMessage)
    {
        Code = code;
    }

    public string Code { get; }
}

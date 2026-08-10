using Talabat.Infrastructure.Development.E2E;

namespace Talabat.Identity;

internal sealed record E2EProvisioningCommand(
    bool IsRequested,
    E2EProvisioningOperation? Operation,
    string? Error)
{
    public static E2EProvisioningCommand Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var commandIndexes = args
            .Select((value, index) => (value, index))
            .Where(item => string.Equals(
                item.value,
                "--e2e-provision",
                StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .ToList();

        if (commandIndexes.Count == 0)
        {
            return new E2EProvisioningCommand(false, null, null);
        }

        if (commandIndexes.Count != 1 || commandIndexes[0] + 1 >= args.Length)
        {
            return Invalid("Use --e2e-provision <operation> --output json.");
        }

        var outputIndexes = args
            .Select((value, index) => (value, index))
            .Where(item => string.Equals(item.value, "--output", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .ToList();

        if (outputIndexes.Count != 1
            || outputIndexes[0] + 1 >= args.Length
            || !string.Equals(args[outputIndexes[0] + 1], "json", StringComparison.OrdinalIgnoreCase)
            || args.Length != 4)
        {
            return Invalid("Provisioning requires exactly --output json.");
        }

        var operation = args[commandIndexes[0] + 1].ToLowerInvariant() switch
        {
            "prepare" => E2EProvisioningOperation.Prepare,
            "make-negative-product-unavailable" =>
                E2EProvisioningOperation.MakeNegativeProductUnavailable,
            "restore" => E2EProvisioningOperation.Restore,
            _ => (E2EProvisioningOperation?)null
        };

        return operation is null
            ? Invalid("The requested E2E provisioning operation is not supported.")
            : new E2EProvisioningCommand(true, operation, null);
    }

    public static async Task<int> RunAsync(
        IServiceProvider services,
        E2EProvisioningOperation operation)
    {
        ArgumentNullException.ThrowIfNull(services);

        try
        {
            await using var scope = services.CreateAsyncScope();
            var provisioner = scope.ServiceProvider.GetRequiredService<IE2EProvisioner>();
            var result = await provisioner.ExecuteAsync(operation);
            Console.Out.WriteLine(E2EProvisioningJson.Serialize(result));
            return 0;
        }
        catch (E2EProvisioningException exception)
        {
            Console.Error.WriteLine(
                $"E2E provisioning failed [{exception.Code}]: {exception.Message}");
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("E2E provisioning failed [operation_cancelled].");
            return 1;
        }
        catch
        {
            Console.Error.WriteLine(
                "E2E provisioning failed [unexpected_failure]. No fixture changes were committed.");
            return 1;
        }
    }

    private static E2EProvisioningCommand Invalid(string error)
    {
        return new E2EProvisioningCommand(true, null, error);
    }
}

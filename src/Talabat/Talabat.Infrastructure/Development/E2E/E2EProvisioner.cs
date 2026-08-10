using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Aggregates.Catalog;
using Talabat.Domain.Aggregates.Users;
using Talabat.Domain.ValueObjects;
using Talabat.Infrastructure.Identity;
using Talabat.Infrastructure.Persistence;

namespace Talabat.Infrastructure.Development.E2E;

public sealed class E2EProvisioner : IE2EProvisioner
{
    private const string CustomerEmailConfigurationKey = "E2E_CUSTOMER_EMAIL";
    private const string CustomerPasswordConfigurationKey = "E2E_CUSTOMER_PASSWORD";

    private readonly TalabatDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly E2EProvisioningOptions _options;

    public E2EProvisioner(
        TalabatDbContext dbContext,
        UserManager<User> userManager,
        IServiceProvider serviceProvider,
        IHostEnvironment environment,
        IConfiguration configuration,
        IOptions<E2EProvisioningOptions> options)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<E2EProvisioningResult> ExecuteAsync(
        E2EProvisioningOperation operation,
        CancellationToken cancellationToken = default)
    {
        var credentials = ValidateAndGetCredentials();
        await VerifyDatabaseAsync(cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            User user;
            CatalogFixture catalog;

            if (operation == E2EProvisioningOperation.MakeNegativeProductUnavailable)
            {
                user = await LoadExistingUserFixtureAsync(credentials.Email, cancellationToken);
                catalog = await LoadExistingCatalogFixtureAsync(cancellationToken);
                catalog.Restaurant.MarkProductUnavailable(catalog.NegativeProduct.Id);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                await IdentityDataSeeder.SeedRolesAsync(_serviceProvider);
                user = await EnsureIdentityFixtureAsync(credentials, cancellationToken);
                await EnsureAddressBaselineAsync(user, cancellationToken);
                await ResetActiveCartAsync(user.Id, cancellationToken);
                catalog = await EnsureCatalogBaselineAsync(cancellationToken);
            }

            var result = BuildResult(operation, user, catalog);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private Credentials ValidateAndGetCredentials()
    {
        if (string.Equals(_environment.EnvironmentName, Environments.Production, StringComparison.OrdinalIgnoreCase))
        {
            throw Failure(
                "unsupported_environment",
                "E2E provisioning is not permitted in Production.");
        }

        if (!string.Equals(_environment.EnvironmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(_environment.EnvironmentName, "E2E", StringComparison.OrdinalIgnoreCase))
        {
            throw Failure(
                "unsupported_environment",
                "E2E provisioning requires the Development or E2E environment.");
        }

        if (!_options.Enabled)
        {
            throw Failure(
                "provisioning_disabled",
                "E2E provisioning is disabled by configuration.");
        }

        var email = _configuration[CustomerEmailConfigurationKey]?.Trim();
        var password = _configuration[CustomerPasswordConfigurationKey];

        if (string.IsNullOrWhiteSpace(email))
        {
            throw Failure(
                "missing_customer_email",
                $"Required configuration '{CustomerEmailConfigurationKey}' is missing.");
        }

        if (string.IsNullOrEmpty(password))
        {
            throw Failure(
                "missing_customer_password",
                $"Required configuration '{CustomerPasswordConfigurationKey}' is missing.");
        }

        ValidateFixtureConfiguration();
        if (!email.Contains(_options.CustomerEmailMarker, StringComparison.OrdinalIgnoreCase))
        {
            throw Failure(
                "customer_fixture_not_owned",
                "The configured E2E customer email does not contain the required fixture marker.");
        }

        return new Credentials(email, password);
    }

    private void ValidateFixtureConfiguration()
    {
        var marker = _options.Marker?.Trim();
        if (string.IsNullOrWhiteSpace(marker)
            || marker.Length < 8
            || _options.DefaultAddress is null
            || _options.AlternateAddress is null
            || _options.Catalog is null)
        {
            throw InvalidFixtureConfiguration();
        }

        var ownedNames = new[]
        {
            _options.DefaultAddress.Label,
            _options.AlternateAddress.Label,
            _options.Catalog.RestaurantName,
            _options.Catalog.HappyProductName,
            _options.Catalog.NegativeProductName
        };

        if (ownedNames.Any(value => string.IsNullOrWhiteSpace(value)
                || !value.Contains(marker, StringComparison.OrdinalIgnoreCase))
            || string.Equals(
                _options.DefaultAddress.Label,
                _options.AlternateAddress.Label,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                _options.Catalog.HappyProductName,
                _options.Catalog.NegativeProductName,
                StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(_options.CustomerEmailMarker)
            || _options.CustomerEmailMarker.Length < 3
            || string.IsNullOrWhiteSpace(_options.DisplayName)
            || !_options.DisplayName.Contains(marker, StringComparison.OrdinalIgnoreCase)
            || _options.Age <= 0
            || string.IsNullOrWhiteSpace(_options.DefaultAddress.City)
            || string.IsNullOrWhiteSpace(_options.DefaultAddress.BuildingNumber)
            || string.IsNullOrWhiteSpace(_options.AlternateAddress.City)
            || string.IsNullOrWhiteSpace(_options.AlternateAddress.BuildingNumber)
            || _options.Catalog.HappyProductPrice < 0m
            || _options.Catalog.NegativeProductPrice < 0m)
        {
            throw InvalidFixtureConfiguration();
        }
    }

    private async Task VerifyDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _dbContext.Database.CanConnectAsync(cancellationToken))
            {
                throw Failure(
                    "database_unreachable",
                    "The configured SQL Server database cannot be reached.");
            }
        }
        catch (E2EProvisioningException)
        {
            throw;
        }
        catch
        {
            throw Failure(
                "database_unreachable",
                "The configured SQL Server database cannot be reached.");
        }

        try
        {
            var pendingMigrations = await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                throw Failure(
                    "pending_migrations",
                    "The configured database has pending EF Core migrations.");
            }
        }
        catch (E2EProvisioningException)
        {
            throw;
        }
        catch
        {
            throw Failure(
                "schema_check_failed",
                "The configured database schema could not be verified.");
        }
    }

    private async Task<User> EnsureIdentityFixtureAsync(
        Credentials credentials,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = _userManager.NormalizeEmail(credentials.Email);
        var matches = await _dbContext.Users
            .IgnoreQueryFilters()
            .Include("_addresses")
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .ToListAsync(cancellationToken);

        if (matches.Count > 1)
        {
            throw Failure(
                "ambiguous_customer_fixture",
                "More than one user matches the configured E2E customer email.");
        }

        User user;
        if (matches.Count == 0)
        {
            user = User.Register(credentials.Email, credentials.Email, _options.DisplayName);
            user.InitializeCustomerProfile(_options.DisplayName, _options.Age, _options.PhoneNumber);
            EnsureIdentitySucceeded(
                await _userManager.CreateAsync(user, credentials.Password),
                "customer_create_failed");
        }
        else
        {
            user = matches[0];
            if (!IsOwnedCustomerFixture(user))
            {
                throw Failure(
                    "customer_fixture_not_owned",
                    "The configured E2E customer is not an owned synthetic fixture.");
            }

            user.Restore(DateTime.UtcNow, _options.Marker);
            user.Activate();

            if (!string.Equals(user.UserName, credentials.Email, StringComparison.Ordinal))
            {
                EnsureIdentitySucceeded(
                    await _userManager.SetUserNameAsync(user, credentials.Email),
                    "customer_username_repair_failed");
            }

            if (!string.Equals(user.Email, credentials.Email, StringComparison.Ordinal))
            {
                EnsureIdentitySucceeded(
                    await _userManager.SetEmailAsync(user, credentials.Email),
                    "customer_email_repair_failed");
            }

            if (user.UserType.HasFlag(UserType.Customer))
            {
                user.UpdateCustomerProfile(_options.DisplayName, _options.Age, _options.PhoneNumber);
            }
            else
            {
                user.InitializeCustomerProfile(_options.DisplayName, _options.Age, _options.PhoneNumber);
            }
        }

        if (!await _userManager.IsInRoleAsync(user, IdentityRoleNames.Customer))
        {
            EnsureIdentitySucceeded(
                await _userManager.AddToRoleAsync(user, IdentityRoleNames.Customer),
                "customer_role_assignment_failed");
        }

        if (!await _userManager.CheckPasswordAsync(user, credentials.Password))
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            EnsureIdentitySucceeded(
                await _userManager.ResetPasswordAsync(user, resetToken, credentials.Password),
                "customer_password_repair_failed");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task<User> LoadExistingUserFixtureAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = _userManager.NormalizeEmail(email);
        var matches = await _dbContext.Users
            .Include("_addresses")
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .ToListAsync(cancellationToken);

        if (matches.Count != 1)
        {
            throw Failure(
                "customer_fixture_missing",
                "Run the prepare operation before changing product availability.");
        }

        EnsureAddressMetadataExists(matches[0]);
        return matches[0];
    }

    private async Task EnsureAddressBaselineAsync(User user, CancellationToken cancellationToken)
    {
        var expectedDefault = ToAddress(_options.DefaultAddress);
        var expectedAlternate = ToAddress(_options.AlternateAddress);
        var ownedAddresses = user.Addresses
            .Where(address => IsOwnedName(address.Details.Street))
            .ToList();

        var defaultMatches = ownedAddresses
            .Where(address => string.Equals(
                address.Details.Street,
                _options.DefaultAddress.Label,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var alternateMatches = ownedAddresses
            .Where(address => string.Equals(
                address.Details.Street,
                _options.AlternateAddress.Label,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        var needsRebuild = ownedAddresses.Count != 2
            || defaultMatches.Count != 1
            || alternateMatches.Count != 1
            || defaultMatches[0].Details != expectedDefault
            || alternateMatches[0].Details != expectedAlternate;

        user.ClearDefaultAddress();
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (needsRebuild)
        {
            foreach (var address in ownedAddresses)
            {
                user.RemoveAddress(address.Id);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            user.AddAddress(expectedDefault, makeDefault: true);
            user.AddAddress(expectedAlternate);
        }
        else
        {
            user.SetDefaultAddress(defaultMatches[0].Id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ResetActiveCartAsync(int customerId, CancellationToken cancellationToken)
    {
        var activeCarts = await _dbContext.Carts
            .Include("_items")
            .Where(cart => cart.CustomerId == customerId && cart.Status == CartStatus.Active)
            .ToListAsync(cancellationToken);

        if (activeCarts.Count > 1)
        {
            throw Failure(
                "ambiguous_active_cart",
                "More than one active cart exists for the E2E customer.");
        }

        if (activeCarts.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var cart = activeCarts[0];
        if (cart.IsExpired(now))
        {
            cart.MarkExpired(now);
        }
        else
        {
            cart.Clear(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CatalogFixture> EnsureCatalogBaselineAsync(CancellationToken cancellationToken)
    {
        var restaurantOwnership = OwnershipDescription("restaurant");
        var restaurantMatches = await _dbContext.Restaurants
            .IgnoreQueryFilters()
            .Include("_products")
            .Where(restaurant =>
                restaurant.Name == _options.Catalog.RestaurantName
                || restaurant.Description == restaurantOwnership)
            .ToListAsync(cancellationToken);

        if (restaurantMatches.Count > 1)
        {
            throw Failure(
                "ambiguous_catalog_fixture",
                "More than one restaurant matches the configured E2E ownership marker.");
        }

        Restaurant restaurant;
        if (restaurantMatches.Count == 0)
        {
            restaurant = new Restaurant(
                _options.Catalog.RestaurantName,
                restaurantOwnership,
                imageUrl: null,
                new TimeRange(TimeOnly.MinValue, TimeOnly.MaxValue));
            _dbContext.Restaurants.Add(restaurant);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            restaurant = restaurantMatches[0];
            restaurant.Restore(DateTime.UtcNow, _options.Marker);
            restaurant.Activate();
            _dbContext.Entry(restaurant).Property(item => item.Name).CurrentValue =
                _options.Catalog.RestaurantName;
            _dbContext.Entry(restaurant).Property(item => item.Description).CurrentValue =
                restaurantOwnership;
            _dbContext.Entry(restaurant).Property(item => item.ImageUrl).CurrentValue = null;
            _dbContext.Entry(restaurant).Reference(item => item.OpeningHours).CurrentValue =
                new TimeRange(TimeOnly.MinValue, TimeOnly.MaxValue);
        }

        var happyProduct = EnsureProduct(
            restaurant,
            _options.Catalog.HappyProductName,
            "happy-product",
            _options.Catalog.HappyProductPrice);
        var negativeProduct = EnsureProduct(
            restaurant,
            _options.Catalog.NegativeProductName,
            "negative-product",
            _options.Catalog.NegativeProductPrice);

        await _dbContext.SaveChangesAsync(cancellationToken);
        restaurant.MarkProductAvailable(happyProduct.Id);
        restaurant.MarkProductAvailable(negativeProduct.Id);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CatalogFixture(restaurant, happyProduct, negativeProduct);
    }

    private Product EnsureProduct(
        Restaurant restaurant,
        string configuredName,
        string fixtureKind,
        decimal configuredPrice)
    {
        var ownership = OwnershipDescription(fixtureKind);
        var matches = restaurant.Products
            .Where(product =>
                string.Equals(product.Name, configuredName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(product.Description, ownership, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count > 1)
        {
            throw Failure(
                "ambiguous_product_fixture",
                $"More than one product matches the configured {fixtureKind} ownership marker.");
        }

        Product product;
        if (matches.Count == 0)
        {
            product = restaurant.AddProduct(
                configuredName,
                ownership,
                new Money(configuredPrice),
                imageUrl: null);
        }
        else
        {
            product = matches[0];
            product.Restore(DateTime.UtcNow, _options.Marker);
            _dbContext.Entry(product).Property(item => item.Name).CurrentValue = configuredName;
            _dbContext.Entry(product).Property(item => item.Description).CurrentValue = ownership;
            _dbContext.Entry(product).Property(item => item.ImageUrl).CurrentValue = null;
            restaurant.UpdateProductPrice(product.Id, new Money(configuredPrice));
        }

        return product;
    }

    private async Task<CatalogFixture> LoadExistingCatalogFixtureAsync(CancellationToken cancellationToken)
    {
        var restaurantOwnership = OwnershipDescription("restaurant");
        var restaurantMatches = await _dbContext.Restaurants
            .Include("_products")
            .Where(restaurant =>
                restaurant.Name == _options.Catalog.RestaurantName
                && restaurant.Description == restaurantOwnership)
            .ToListAsync(cancellationToken);

        if (restaurantMatches.Count != 1)
        {
            throw Failure(
                "catalog_fixture_missing",
                "Run the prepare operation before changing product availability.");
        }

        var restaurant = restaurantMatches[0];
        var happyProduct = GetSingleOwnedProduct(
            restaurant,
            _options.Catalog.HappyProductName,
            "happy-product");
        var negativeProduct = GetSingleOwnedProduct(
            restaurant,
            _options.Catalog.NegativeProductName,
            "negative-product");

        return new CatalogFixture(restaurant, happyProduct, negativeProduct);
    }

    private Product GetSingleOwnedProduct(
        Restaurant restaurant,
        string configuredName,
        string fixtureKind)
    {
        var ownership = OwnershipDescription(fixtureKind);
        var matches = restaurant.Products
            .Where(product =>
                string.Equals(product.Name, configuredName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(product.Description, ownership, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count != 1)
        {
            throw Failure(
                "product_fixture_missing",
                $"The configured {fixtureKind} fixture is missing or ambiguous.");
        }

        return matches[0];
    }

    private E2EProvisioningResult BuildResult(
        E2EProvisioningOperation operation,
        User user,
        CatalogFixture catalog)
    {
        var defaultAddress = GetAddressByLabel(user, _options.DefaultAddress.Label);
        var alternateAddress = GetAddressByLabel(user, _options.AlternateAddress.Label);

        return new E2EProvisioningResult(
            Version: 1,
            Operation: ToContractName(operation),
            Environment: _environment.EnvironmentName,
            Customer: new E2ECustomerResult(user.Email!, user.FullName),
            Addresses: new E2EAddressesResult(
                new E2EAddressResult(defaultAddress.Id, _options.DefaultAddress.Label),
                new E2EAddressResult(alternateAddress.Id, _options.AlternateAddress.Label)),
            Catalog: new E2ECatalogResult(
                new E2ERestaurantResult(catalog.Restaurant.Id, catalog.Restaurant.Name),
                new E2EProductResult(
                    catalog.HappyProduct.Id,
                    catalog.HappyProduct.Name,
                    catalog.HappyProduct.IsAvailable),
                new E2EProductResult(
                    catalog.NegativeProduct.Id,
                    catalog.NegativeProduct.Name,
                    catalog.NegativeProduct.IsAvailable)));
    }

    private void EnsureAddressMetadataExists(User user)
    {
        _ = GetAddressByLabel(user, _options.DefaultAddress.Label);
        _ = GetAddressByLabel(user, _options.AlternateAddress.Label);
    }

    private static UserAddress GetAddressByLabel(User user, string label)
    {
        var matches = user.Addresses
            .Where(address => string.Equals(
                address.Details.Street,
                label,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count != 1)
        {
            throw Failure(
                "address_fixture_missing",
                "The configured E2E address fixture is missing or ambiguous.");
        }

        return matches[0];
    }

    private bool IsOwnedName(string value)
    {
        return value.Contains(_options.Marker, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsOwnedCustomerFixture(User user)
    {
        return user.Email?.Contains(_options.CustomerEmailMarker, StringComparison.OrdinalIgnoreCase) == true
            && user.FullName.Contains(_options.Marker, StringComparison.OrdinalIgnoreCase);
    }

    private string OwnershipDescription(string fixtureKind)
    {
        return $"[{_options.Marker}] fixture:{fixtureKind}";
    }

    private static Address ToAddress(E2EAddressOptions options)
    {
        return new Address(options.Label, options.City, options.BuildingNumber, options.Floor);
    }

    private static string ToContractName(E2EProvisioningOperation operation)
    {
        return operation switch
        {
            E2EProvisioningOperation.Prepare => "prepare",
            E2EProvisioningOperation.MakeNegativeProductUnavailable =>
                "make-negative-product-unavailable",
            E2EProvisioningOperation.Restore => "restore",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private static void EnsureIdentitySucceeded(IdentityResult result, string code)
    {
        if (!result.Succeeded)
        {
            throw Failure(code, "ASP.NET Core Identity rejected the E2E fixture operation.");
        }
    }

    private static E2EProvisioningException InvalidFixtureConfiguration()
    {
        return Failure(
            "invalid_fixture_configuration",
            "E2E fixture markers or baseline values are missing, invalid, or ambiguous.");
    }

    private static E2EProvisioningException Failure(string code, string message)
    {
        return new E2EProvisioningException(code, message);
    }

    private sealed record Credentials(string Email, string Password);

    private sealed record CatalogFixture(
        Restaurant Restaurant,
        Product HappyProduct,
        Product NegativeProduct);
}

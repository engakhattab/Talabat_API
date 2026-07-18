# Phase 7 Architectural Guide: Lazy Profile Enforcement & Infrastructure

This guide serves as the definitive reference for the architectural design, business reasoning, technical mechanics, and runtime flow of the production components implemented during **Phase 7 (Customer-facing API / Lazy Profile Creation)** of the Talabat food delivery system.

---

## 📂 Production Files Directory

---

### 📂 src/Talabat/Talabat.API/Auth/CurrentUser.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Exposes framework-neutral contextual claims of the currently authenticated request (such as authentication status, Identity Auth sub ID, presence of a customer profile, and resolved database `CustomerId`).
- **Phase 7 Gap**: Enables the "Lazy Profile Creation" architectural flow. Instead of enforcing profile setup at the Identity/Token issuance stage, this class resolves the association dynamically. If a user is authenticated but has no database profile, it flags `HasProfile = false`, allowing specific endpoints to handle or block access lazily.
- **Decoupling**: Prevents the Domain and Application layers from referencing web-specific libraries (e.g., `HttpContext`, `ClaimsPrincipal`) or authentication authority packages. It maps JWT claims into clean, domain-friendly scalars.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` implementing `ICurrentUser`.
- **Dependencies**: Injects `IHttpContextAccessor` and `TalabatDbContext`.
- **Mechanics**: Implements a lazy caching pattern (`EnsureResolved`). On the first access of any property, it extracts claims from the request principal and checks the database using `.AsNoTracking()` to retrieve matching customer data.
- **Guards**: Handles missing claims, unauthenticated contexts, and database-less requests gracefully without throwing null reference exceptions.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Request Ingestion**: An API request arrives at a controller endpoint requiring authorization.
2. **Accessing Context**: The controller or a filter accesses a property on `ICurrentUser` (e.g., `_currentUser.CustomerId`).
3. **Claim Resolution**: `EnsureResolved()` reads `HttpContext.User` and retrieves the `"sub"` or name identifier claim.
4. **Database Verification**: If a user identity ID is retrieved, a query runs against the `Customers` table (`c.IdentityUserId == _identityUserId`).
5. **Caching & Handoff**: `_hasProfile` and `_customerId` are resolved, cached in-memory for the lifetime of the request, and returned.

---

### 📂 src/Talabat/Talabat.API/Middleware/ProfileEnforcementFilter.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Enforces that only users with a completed customer profile can interact with owner-scoped paths (e.g., cart, addresses, checkout, order history).
- **Phase 7 Gap**: Standardizes the lazy profile creation security policy. It prevents profile-less users from submitting orders or modifying addresses, returning a standard RESTful block state instead.
- **Decoupling**: Serves as a cross-cutting concern in the API layer, keeping application-level CQRS handlers completely free from route-level authorization and profile checks.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` implementing the ASP.NET Core `IAsyncActionFilter` interface.
- **Dependencies**: Injects `ICurrentUser`.
- **Mechanics**: Inspects incoming request HTTP methods and path structures.
  - Exempts `POST /api/me/profile` (profile creation endpoint).
  - Intercepts `GET /api/me/profile` to return `404 Not Found` (with errorCode `"ProfileNotCreated"`) if the profile does not exist.
  - Blocks other `/api/me/*` paths with `409 Conflict` (with errorCode `"ProfileNotCreated"`) if the profile is missing.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Interception**: The filter runs after routing but before the target controller action executes.
2. **Exemption Check**: The filter inspects the request method and path. If it's a `POST` to `/api/me/profile`, execution is immediately forwarded to the controller.
3. **State Validation**: If the user is authenticated but has no profile:
   - For `GET /api/me/profile`, it stops request execution and returns a `404 NotFoundObjectResult`.
   - For other `/api/me/` routes, it stops request execution and returns a `409 ConflictObjectResult`.
4. **Handoff**: If the profile exists, `next()` is called to execute the controller action.

---

### 📂 src/Talabat/Talabat.API/Middleware/DomainExceptionHandler.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Intercepts unhandled domain, application, and argument validation exceptions and translates them into standardised API responses following the RFC 7807 Problem Details spec.
- **Phase 7 Gap**: Maps business/validation logic errors (such as domain guard validation failures, invalid age inputs, or duplicate profiles) to uniform API errors.
- **Decoupling**: Ensures the domain model does not need to know about HTTP status codes. The domain throws standard C# exceptions, and this handler maps them to the appropriate HTTP status.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` implementing ASP.NET Core `IExceptionHandler`.
- **Dependencies**: None.
- **Mechanics**: Captures thrown exceptions. Intercepts `DomainException`, `ArgumentException`, and `ArgumentOutOfRangeException`. Maps them to `400 Bad Request` and serialises a `ProblemDetails` response containing the specific message and an extension property `errorCode`.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Exception Raised**: An exception is thrown during request handling (e.g., a domain validation guard throws `ArgumentException` due to an empty profile name).
2. **Capture**: The ASP.NET Core exception handling middleware catches the exception and passes it to `TryHandleAsync`.
3. **Exception Matching**: The handler validates if the exception type matches registered exceptions. If not, it returns `false` to let other middleware handle it.
4. **RFC 7807 Mapping**: Constructs a `ProblemDetails` object, setting status code `400`, the exception message, and the class name as `errorCode`.
5. **Output**: Writes the response as JSON to `HttpContext.Response` and returns `true`.

---

### 📂 src/Talabat/Talabat.API/Extensions/UseCaseResultExtensions.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Translates application CQRS outputs (`UseCaseResult<T>`) into standard ASP.NET Core MVC action results (`IActionResult`).
- **Phase 7 Gap**: Provides a unified mapping mechanism across controllers to handle success or failure responses (such as validation, conflict, or not found results) consistently.
- **Decoupling**: Keeps controllers lightweight and thin by abstracting the mapping from business error categories to HTTP status codes.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `public static class` containing extension methods.
- **Mechanics**: Exposes methods `ToActionResult` and `ToCreatedAtAction` on `UseCaseResult<T>`. On failure, it maps the `ApplicationErrorCategory` enum (e.g., `Validation`, `NotFound`, `Conflict`) to specific HTTP status codes and formats the output into an RFC 7807 `ProblemDetails` response.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Result Received**: The controller action receives a `UseCaseResult<T>` from a CQRS handler.
2. **Success Check**: The extension method checks the `IsSuccess` property.
3. **Mapping**:
   - On Success: Invokes the provided success delegate (e.g., returns `Ok(value)` or `CreatedAtAction(...)`).
   - On Failure: Evaluates the error category, assigns the RFC-defined status code and section, sets `errorCode = error.Code`, and returns an `ObjectResult` containing the problem details.

---

### 📂 src/Talabat/Talabat.API/Program.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: The application's entry point; sets up configuration, registers dependencies, and configures the HTTP request pipeline.
- **Phase 7 Gap**: Bootstraps JWT authentication validation, registers the custom exception handler, and registers the global `ProfileEnforcementFilter` to enforce profile policies across all routes.
- **Decoupling**: Orchestrates the dependencies of all layers (Domain, Application, Infrastructure, API) in a centralized place without leakage.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: Top-level statements program structure.
- **Mechanics**:
  - Registers the MVC Controllers, globally adding `ProfileEnforcementFilter`.
  - Registers `AddProblemDetails()` and the custom `DomainExceptionHandler`.
  - Configures JWT Bearer authentication options against the configured identity authority.
  - Registers the custom `ICurrentUser` as scoped.
  - Builds the pipeline and configures CORS, Exception Handling middleware (`UseExceptionHandler(_ => {})`), Routing, Authentication, and Authorization.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Bootstrapping**: The runtime starts `Program.cs`.
2. **Service Setup**: Resolves configurations, sets up DB connection strings, and runs DI extensions (`AddApplication()`, `AddInfrastructure()`).
3. **Pipeline Construction**: Registers middleware in sequence: Exception Handling -> Routing -> CORS -> Authentication -> Authorization -> Controllers.
4. **Start**: Begins listening for incoming HTTP requests on configured ports.

---

### 📂 src/Talabat/Talabat.API/Controllers/CustomerController.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Exposes REST endpoints to get, create, and update the authenticated user's customer profile details.
- **Phase 7 Gap**: Provides the gateway for profile creation. Since profiles are lazily created, this controller hosts the `POST /api/me/profile` endpoint which allows users to set up their identity profiles.
- **Decoupling**: Serves as a thin wrapper routing JSON payloads into CQRS commands/queries and returning HTTP responses.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` extending `ControllerBase` decorated with `[ApiController]`, `[Route("api/me/profile")]`, and `[Authorize]`.
- **Dependencies**: `ICurrentUser`, `CreateCustomerProfileHandler`, `GetCustomerProfileHandler`, `UpdateCustomerProfileHandler`.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **HTTP POST Request**: An authorized client sends a profile creation request.
2. **Dispatch**: The route invokes `CreateProfile` with the payload.
3. **CQRS Invocation**: Builds a `CreateCustomerProfileCommand` using the caller's identity ID and payload, then invokes the handler.
4. **Handoff**: The result is processed via `ToCreatedAtAction` and returned as `201 Created` on success.

---

### 📂 src/Talabat/Talabat.API/Controllers/AddressController.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Manages the shipping/delivery addresses of the authenticated customer.
- **Phase 7 Gap**: Exposes address operations under the `/api/me/` prefix, ensuring a user's address collection is linked to their profile.
- **Decoupling**: Standard API controller mapping REST inputs into address-related CQRS handlers.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` decorated with `[ApiController]`, `[Route("api/me/addresses")]`, and `[Authorize]`.
- **Dependencies**: `AddCustomerAddressHandler`, `RemoveCustomerAddressHandler`, `SetDefaultCustomerAddressHandler`.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **HTTP POST Request**: A client submits a new address payload.
2. **Dispatch**: Invokes the `AddAddress` action.
3. **CQRS Invocation**: Runs `AddCustomerAddressHandler` using the resolved customer database ID and the payload.
4. **Handoff**: Returns `201 Created` or an error mapped via `ToActionResult`.

---

### 📂 src/Talabat/Talabat.API/Controllers/CartController.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Manages items within the customer's personal shopping cart/basket.
- **Phase 7 Gap**: Interfaces with the basket context under the authenticated `/api/me/cart` path.
- **Decoupling**: Keeps the shopping cart logic distinct from the catalog and ordering layers.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` decorated with `[ApiController]`, `[Route("api/me/cart")]`, and `[Authorize]`.
- **Dependencies**: `GetCartHandler`, `AddCartItemHandler`, `UpdateCartItemQuantityHandler`, `RemoveCartItemHandler`, `ClearCartHandler`.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **HTTP GET Request**: Requests the current state of the customer's cart.
2. **Dispatch**: Invokes the `GetCart` action.
3. **CQRS Invocation**: Calls `GetCartHandler` using the customer ID.
4. **Handoff**: Returns the cart details as `200 OK` or an error.

---

### 📂 src/Talabat/Talabat.API/Controllers/CatalogController.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Provides search and viewing capabilities for restaurants and menus.
- **Phase 7 Gap**: Exposes catalog endpoints. As mandated by Phase 7 specs, catalog routes are anonymous-access friendly.
- **Decoupling**: Exposes catalog data to guest users without auth context requirements.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` decorated with `[ApiController]`, `[Route("api/catalog")]`, and `[AllowAnonymous]`.
- **Dependencies**: `BrowseRestaurantsHandler`, `GetRestaurantMenuHandler`.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **HTTP GET Request**: Guest or user queries `/api/catalog/restaurants`.
2. **Dispatch**: Invokes `BrowseRestaurants`.
3. **CQRS Invocation**: Calls `BrowseRestaurantsHandler` with query criteria.
4. **Handoff**: Returns the lists of restaurants as `200 OK`.

---

### 📂 src/Talabat/Talabat.API/Controllers/CheckoutController.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Processes checkouts, transforming cart contents into placed orders.
- **Phase 7 Gap**: Links profile-enforced customers to their checkout requests.
- **Decoupling**: Integrates basket verification, restaurant local-time checking, and order placement via a thin controller.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` decorated with `[ApiController]`, `[Route("api/me/checkout")]`, and `[Authorize]`.
- **Dependencies**: `ICurrentUser`, `CheckoutHandler`.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **HTTP POST Request**: Customer triggers a checkout request.
2. **Dispatch**: Invokes the `Checkout` action.
3. **CQRS Invocation**: Dispatches `CheckoutCommand` using the customer's database ID and destination address ID.
4. **Handoff**: Returns `201 Created` with the order ID, `422 Unprocessable` on stock issues, or an error.

---

### 📂 src/Talabat/Talabat.API/Controllers/OrderController.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Exposes order histories and details of previously completed purchases.
- **Phase 7 Gap**: Exposes customer order history under `/api/me/orders` with strict profile checking.
- **Decoupling**: Exposes order details without exposing internal order DB schemas.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` decorated with `[ApiController]`, `[Route("api/me/orders")]`, and `[Authorize]`.
- **Dependencies**: `ICurrentUser`, `GetOrderHistoryHandler`, `GetOrderDetailsHandler`.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **HTTP GET Request**: Requests the customer's historical orders list.
2. **Dispatch**: Invokes `GetOrders`.
3. **CQRS Invocation**: Calls `GetOrderHistoryHandler` with the customer's profile ID.
4. **Handoff**: Returns the list as `200 OK`.

---

### 📂 src/Talabat/Talabat.Domain/Aggregates/Customer/Customer.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Represents the Customer aggregate root containing invariants (e.g., phone numbers, name correctness, and associated addresses).
- **Phase 7 Gap**: Modified to support identity linkage. It includes the `IdentityUserId` field and a `CreateForAccount` factory method to bind a customer to an identity.
- **Decoupling**: Stays free of web packages, only exposing the domain logic of profile setup.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: Domain Entity (Aggregate Root).
- **Mechanics**: Implements domain validation guards (`Guard.RequiredText`, `Guard.Positive`) within its constructor to ensure age and name invariants are respected.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Invocation**: Handlers invoke the `CreateForAccount` factory method.
2. **Guard Enforcement**: Invariants (e.g. non-empty names) are validated. If invalid, `ArgumentException` is thrown.
3. **Instantiation**: Returns a validated `Customer` instance, ready for persistence.

---

### 📂 src/Talabat/Talabat.Domain/Interfaces/ICustomerRepository.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Interface declaring customer persistence contracts.
- **Phase 7 Gap**: Extended with `GetByIdentityUserIdAsync` to lookup profiles using Identity IDs.
- **Decoupling**: Defines persistence behavior in the Domain layer, leaving database implementations to Infrastructure.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: Interface.
- **Mechanics**: Defines signatures for looking up, adding, and saving customer records.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Contract Call**: Handlers call `GetByIdentityUserIdAsync` during execution.
2. **Execution**: Routed to infrastructure repository implementation.

---

### 📂 src/Talabat/Talabat.Infrastructure/Persistence/Repositories/CustomerRepository.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Concrete implementation of `ICustomerRepository` using EF Core.
- **Phase 7 Gap**: Impements lookups based on `IdentityUserId`.
- **Decoupling**: Keeps SQL/EF Core logic isolated inside the Infrastructure layer.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` implementing `ICustomerRepository`.
- **Dependencies**: Injects `TalabatDbContext`.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Database Access**: Receives a lookup request for an identity.
2. **Execution**: Executes an EF query against the database context using `FirstOrDefaultAsync` matching the `IdentityUserId`.
3. **Return**: Returns the entity or null.

---

### 📂 src/Talabat/Talabat.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Database schema mapping configuration for the `Customer` entity.
- **Phase 7 Gap**: Maps the newly added `IdentityUserId` column and creates a unique index on it to guarantee one profile per account.
- **Decoupling**: Separates persistence mapping code from clean domain objects.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: Class implementing `IEntityTypeConfiguration<Customer>`.
- **Mechanics**: Uses fluent API config to add a unique index on `IdentityUserId` filtered to ignore nulls (`HasIndex(...).IsUnique().HasFilter(...)`).

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **EF Setup**: Config is evaluated during DB context building.
2. **Schema Generation**: Database table rules are enforced on migration generation.

---

### 📂 src/Talabat/Talabat.Infrastructure/Persistence/Migrations/20260716134242_AddCustomerIdentityUserId.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Database migration schema changes.
- **Phase 7 Gap**: Updates SQL Server to track `IdentityUserId` with a unique index constraint.
- **Decoupling**: Keeps database schema evolution history tracked in the repository.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: EF Core `Migration` class.
- **Mechanics**: Employs `MigrationBuilder` commands to alter the `Customers` table, adding `IdentityUserId` as nullable (for existing data migration compatibility) and applying a filtered unique index.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Migration Execution**: Run during deployment or test setup.
2. **Execution**: Runs database alter queries against SQL Server.

---

### 📂 src/Talabat/Talabat.Infrastructure/Time/SystemClock.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Resolves current system UTC time.
- **Phase 7 Gap**: Implements production system clock interfaces to replace test fakes.
- **Decoupling**: Abstracts system time away from hard dependencies on `DateTime.UtcNow`.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` implementing `IClock`.
- **Mechanics**: Returns `DateTime.UtcNow`.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Request**: Domain services read current time.
2. **Handoff**: Returns the system's `DateTime.UtcNow`.

---

### 📂 src/Talabat/Talabat.Infrastructure/Time/RestaurantLocalTimeProvider.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Converts UTC timestamps to local Egypt time for restaurant operations checks.
- **Phase 7 Gap**: Enables checking if a restaurant is currently open for orders.
- **Decoupling**: Wraps timezone conversion logic safely in Infrastructure.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: `sealed class` implementing `IRestaurantLocalTimeProvider`.
- **Mechanics**: Uses timezone conversions to target `"Africa/Cairo"` time.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Time Check**: Handlers pass UTC time and target restaurant context.
2. **Conversion**: Time is converted to Cairo timezone.
3. **Handoff**: Returns local `TimeOnly` representing restaurant local hours.

---

### 📂 src/Talabat/Talabat.Application/Abstractions/ICurrentUser.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Interface declaring the contract for resolving the active user profile context.
- **Phase 7 Gap**: Exposes current user details to the application layer.
- **Decoupling**: Standard contract keeping the Application layer decoupled from ASP.NET Core web libraries.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: Interface.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Handoff**: Invoked by handlers during the CQRS execution pipeline.

---

### 📂 src/Talabat/Talabat.Application/Customers/CreateProfile/CreateCustomerProfileCommand.cs & CreateCustomerProfileHandler.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Orchestrates the business use case of creating a new customer profile.
- **Phase 7 Gap**: Enforces customer profile setup, validates invariants, and saves the linked profile.
- **Decoupling**: Pure Application layer CQRS command handler.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: Command record and sealed handler class.
- **Dependencies**: `ICustomerRepository`, `IUnitOfWork`.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Command Received**: Receives `CreateCustomerProfileCommand`.
2. **Validation**: Queries database to confirm no profile exists for this identity.
3. **Execution**: Invokes domain factory method `Customer.CreateForAccount`.
4. **Persistence**: Persists the customer entity and calls unit of work to save changes.

---

### 📂 src/Talabat/Talabat.Application/DependencyInjection.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Registers Application services in DI.
- **Phase 7 Gap**: Automates registration of new handlers.
- **Decoupling**: Exposes a single `AddApplication` entry point.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: Static helper class.
- **Mechanics**: Registers CQRS handlers as scoped dependencies.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Registration**: Invoked during application startup.

---

### 📂 src/Talabat/Talabat.Infrastructure/DependencyInjection.cs

#### 🎯 1. Logical & Business Importance (البعد العملي والبزنس)
- **Responsibility**: Registers Infrastructure services in DI.
- **Phase 7 Gap**: Registers clock providers, repository implementations, and EF DB contexts.
- **Decoupling**: Exposes a single `AddInfrastructure` registration.

#### 💻 2. Technical Implementation & Mechanics (التنفيذ التقني)
- **Constructs**: Static helper class.
- **Mechanics**: Sets up DB context configurations and maps time provider interfaces.

#### 🔄 3. Runtime Execution Flow (مسار عمل البيانات)
1. **Registration**: Invoked during application startup.

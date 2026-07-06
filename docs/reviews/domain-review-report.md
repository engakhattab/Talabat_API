# Domain Review Report

> Historical review snapshot created before remediation on 2026-07-06. The findings below explain the pre-fix state and should not be treated as the current implementation status without a new review.

## 1. Executive Summary

- **Overall status:** Needs Fixes
- **Ready for Repository Interfaces:** No
- **Domain project build:** Passes with 0 warnings and 0 errors.
- **Full solution build:** Fails in the API template project; details are listed under Critical Issues.

### Main Strengths

- The Domain project has no EF Core, DbContext, HTTP, API, repository implementation, SaveChanges, or external-service dependencies.
- Aggregate roots own private child collections and expose read-only views.
- Product, CartItem, CustomerAddress, and OrderItem construction/mutation is restricted to the Domain assembly or aggregate root.
- Cart pricing follows the current decision: CartItem stores no price, Cart receives current prices, and checkout uses Product.CurrentPrice.
- Order and OrderItem preserve immutable historical price and address data.
- Checkout uses structured results for unavailable products instead of exceptions or obsolete price-change results.
- Delivery and DeliveryAgent are separate aggregate roots using ID-only references, and the DeliveryManagement namespace removes the old type/namespace alias problem.

### Main Risks

- Checkout does not prove that the supplied Restaurant matches `Cart.RestaurantId`; overlapping Product IDs can produce an order priced from the wrong restaurant.
- Customer profile and default-address operations can partially mutate state before validation fails.
- Customer permits duplicate CustomerAddress IDs, making child identity ambiguous.
- Assigned agents are not released when Delivery is cancelled or failed.
- Child identity/persistence decisions in documentation do not match CartItem and OrderItem code.
- The full solution baseline is red because of an unrelated API template error.

## 2. Critical Issues

### Checkout Can Use The Wrong Restaurant

- **Severity:** Critical
- **File / Class / Method:** `src/Talabat/Talabat.Domain/Services/CheckoutDomainService.cs:11`, `ValidateCheckout`
- **Problem:** The service validates products against the supplied Restaurant but never checks `cart.RestaurantId == restaurant.Id`.
- **Why it matters:** If two restaurants have overlapping Product IDs, checkout can succeed with another restaurant's product name and CurrentPrice. That can corrupt the resulting Order while its RestaurantId still comes from the Cart.
- **Suggested fix:** Before product lookup, require the supplied Restaurant ID to equal the Cart RestaurantId. Use a business-specific failure rather than silently treating the products as valid.
- **Blocks Repository Interfaces?** Yes

### Customer Profile Update Is Not Atomic

- **Severity:** Critical
- **File / Class / Method:** `src/Talabat/Talabat.Domain/Customer/Customer.cs:29`, `UpdateProfile`; `Customer.cs:72`, `SetProfile`
- **Problem:** `FullName` is assigned before Age is validated. A valid new name plus invalid age changes FullName and then throws.
- **Why it matters:** A rejected command leaves the aggregate partially changed, violating the expectation that failed domain operations preserve prior valid state.
- **Suggested fix:** Validate and normalize all inputs into local variables first, then assign all profile fields only after every validation succeeds.
- **Blocks Repository Interfaces?** Yes

### Adding A Default Address Can Corrupt Default State On Failure

- **Severity:** Critical
- **File / Class / Method:** `src/Talabat/Talabat.Domain/Customer/Customer.cs:34`, `AddAddress`
- **Problem:** When `makeDefault` is true, all existing defaults are cleared before the new CustomerAddress constructor validates `addressId`. An invalid ID throws after the old default has already been removed.
- **Why it matters:** A failed AddAddress operation can leave the Customer with a changed default-address state.
- **Suggested fix:** Validate addressId, address, duplicate details, and duplicate identity before changing existing default flags. Construct the new child before mutating the collection/default state.
- **Blocks Repository Interfaces?** Yes

### Duplicate CustomerAddress IDs Are Allowed

- **Severity:** Critical
- **File / Class / Method:** `src/Talabat/Talabat.Domain/Customer/Customer.cs:34`, `AddAddress`; `Customer.cs:64`, `GetRequiredAddress`
- **Problem:** Duplicate address values are rejected, but two different addresses may use the same addressId. `GetRequiredAddress` then uses `SingleOrDefault`, which throws a generic sequence exception when more than one match exists.
- **Why it matters:** Child identity is no longer unique inside the aggregate, and RemoveAddress/SetDefaultAddress cannot target a deterministic child.
- **Suggested fix:** Reject duplicate address IDs before adding a child, preferably with a business-specific exception or a clear invariant failure.
- **Blocks Repository Interfaces?** Yes

### Full Solution Build Is Broken Outside Domain

- **Severity:** Critical
- **File / Class / Method:** `src/Talabat/Talabat.API/Controllers/WeatherForecastController.cs:15`
- **Problem:** The full solution build fails because `WeatherForecast` cannot be resolved. The API project also reports NU1903 for Microsoft.OpenApi 2.0.0.
- **Why it matters:** Domain builds cleanly, but the repository has no green full-solution baseline. Future repository work cannot be verified end to end until this unrelated failure is separated or fixed.
- **Suggested fix:** In a separate non-review task, restore/remove the template WeatherForecast type/controller consistently and update the vulnerable API package after checking compatibility.
- **Blocks Repository Interfaces?** No for Domain contract design; yes for accepting the repository as a fully building solution.

## 3. Major Issues

### Cancelled Or Failed Delivery Can Leave Agent Permanently Busy

- **Severity:** Major
- **File / Class / Method:** `src/Talabat/Talabat.Domain/DeliveryManagement/Delivery.cs:116`, `Cancel`; `Delivery.cs:129`, `Fail`; `Services/DeliveryManagement/DeliveryAssignmentDomainService.cs`
- **Problem:** Delivery can become Cancelled or Failed after assignment, but only successful completion calls `agent.MarkAvailable()`.
- **Why it matters:** The Delivery becomes terminal and no longer counts as active, while DeliveryAgent can remain Busy and cannot receive another delivery. This creates cross-aggregate disagreement before persistence is introduced.
- **Suggested fix:** Define explicit cancellation/failure coordination in the domain service, including when an assigned agent is released. Keep direct Delivery lifecycle validation but coordinate both roots for assigned terminal outcomes.
- **Blocks Repository Interfaces?** Yes for Delivery repositories

### Agent Release Does Not Require Busy Status

- **Severity:** Major
- **File / Class / Method:** `src/Talabat/Talabat.Domain/DeliveryManagement/DeliveryAgent.cs:93`, `MarkAvailable`
- **Problem:** MarkAvailable rejects only Suspended and can move Offline or already Available agents to Available.
- **Why it matters:** CompleteDelivery should release the Busy assigned agent, not normalize arbitrary agent states. A mismatched persisted state could be hidden instead of rejected.
- **Suggested fix:** Require Busy status for completion-driven release and throw a delivery-agent status exception otherwise.
- **Blocks Repository Interfaces?** No, but fix with the terminal-delivery policy before Delivery persistence.

### Business Failures Use Generic InvalidOperationException

- **Severity:** Major
- **File / Class / Method:** `Catalog/Restaurant.cs:63`, `Restaurant.cs:108`; `Basket/Cart.cs:114`, `Cart.cs:139`; `DeliveryManagement/DeliveryAgent.cs:55`, `DeliveryAgent.cs:72`
- **Problem:** Duplicate/missing product, missing cart item/current price, and invalid agent lifecycle operations use InvalidOperationException.
- **Why it matters:** These are predictable business failures, but they cannot be handled consistently through DomainException and will be harder to map or test later.
- **Suggested fix:** Add focused domain exceptions only where a real use case needs the failure, and keep structural argument validation as standard argument exceptions.
- **Blocks Repository Interfaces?** No

### Child Identity Model Is Unresolved

- **Severity:** Major
- **File / Class / Method:** `Basket/CartItem.cs`, `Ordering/OrderItem.cs`; `docs/entity-design.md:187`, `docs/entity-design.md:287`
- **Problem:** Documentation defines CartItemId/CartId and OrderItemId/OrderId, while implementation has no child IDs or parent IDs.
- **Why it matters:** Both models can work, but persistence mapping and child identity semantics differ. Repository interfaces should not be followed by EF design until this is decided explicitly.
- **Suggested fix:** Decide whether children use surrogate IDs, composite/owner keys, or value-like owned collections; then align entity design and later database constraints.
- **Blocks Repository Interfaces?** No for root contracts, but blocks reliable persistence design.

## 4. Medium Issues

### Delivery Timestamps Can Move Backward

- **Severity:** Medium
- **File / Class / Method:** `DeliveryManagement/Delivery.cs`, all lifecycle methods
- **Problem:** Status order is validated, but currentTime may precede CreatedAt or the previous transition timestamp.
- **Why it matters:** Historical delivery timestamps can become chronologically inconsistent.
- **Suggested fix:** Decide whether Domain enforces monotonic timestamps; if yes, validate each transition against CreatedAt and the previous timestamp.
- **Blocks Repository Interfaces?** No

### Undefined VehicleType Values Are Accepted

- **Severity:** Medium
- **File / Class / Method:** `DeliveryManagement/DeliveryAgent.cs:23`, constructor
- **Problem:** Any integer cast to VehicleType, including zero or unknown values, is accepted.
- **Why it matters:** DeliveryAgent can begin with an invalid domain state before database checks exist.
- **Suggested fix:** Validate that vehicleType is a defined supported value.
- **Blocks Repository Interfaces?** No

### Checkout Result Collections Do Not Reject Null Elements

- **Severity:** Medium
- **File / Class / Method:** `Services/Checkout/CheckoutSucceeded.cs:9`; `CheckoutProductsUnavailable.cs:7`
- **Problem:** The collections reject null and empty inputs but do not reject null elements supplied at runtime.
- **Why it matters:** Public result constructors can create structurally invalid results outside CheckoutDomainService.
- **Suggested fix:** Validate all elements before storing the read-only collection or reduce constructor visibility if only the service should create results.
- **Blocks Repository Interfaces?** No

### Customer Has No Domain Operation For Selected Delivery Address

- **Severity:** Medium
- **File / Class / Method:** `Customer/Customer.cs`; checkout boundary
- **Problem:** Checkout requires the selected address to belong to Customer, but Customer exposes only the complete read-only collection. There is intentionally no public GetAddress and no snapshot-producing operation yet.
- **Why it matters:** A future Application handler may reimplement lookup/ownership checks and exception behavior outside the aggregate.
- **Suggested fix:** Before implementing checkout handlers, add a use-case-shaped operation such as CreateDeliveryAddressSnapshot(addressId), not a general mutable child accessor.
- **Blocks Repository Interfaces?** No

### Cart Creation Semantics Are Ambiguous

- **Severity:** Medium
- **File / Class / Method:** `Basket/Cart.cs:24`; multiple design documents
- **Problem:** Code publicly creates an empty Active Cart, while documentation says a Cart is not created/persisted until the first item is added.
- **Why it matters:** Application and repository contracts may disagree about whether empty active carts are valid and persistable.
- **Suggested fix:** Decide whether the constructor represents transient pre-first-item state or replace it later with a first-item factory. Document the persistence rule precisely.
- **Blocks Repository Interfaces?** Yes for ICartRepository method semantics.

### DateTime Kind Policy Is Undefined

- **Severity:** Medium
- **File / Class / Method:** Cart, Order, Delivery, and DeliveryAgent constructors/transitions
- **Problem:** DateTime values are accepted without a UTC/kind policy.
- **Why it matters:** Expiration, restaurant time conversion, and lifecycle history can be inconsistent across callers.
- **Suggested fix:** Document whether timestamps are UTC and opening-hours checks use a configured local timezone; enforce the chosen boundary consistently.
- **Blocks Repository Interfaces?** No

## 5. Minor Issues / Cleanups

### Dead Commented Code In Money

- **Severity:** Minor
- **File / Class / Method:** `ValueObjects/Money.cs:26`
- **Problem:** A commented-out null-check experiment remains in production Domain code.
- **Suggested fix:** Remove the dead comments; keep ArgumentNullException.ThrowIfNull.

### Redundant Folder Items In Project File

- **Severity:** Minor
- **File / Class / Method:** `Talabat.Domain.csproj`
- **Problem:** Explicit Folder entries remain for folders that now contain source files.
- **Suggested fix:** Remove redundant Folder items when project organization is stable.

### Terminal Exception Name Is Broader Than Its Message

- **Severity:** Minor
- **File / Class / Method:** `Exceptions/DeliveryAlreadyCompletedException.cs`
- **Problem:** The exception is thrown for Cancelled and Failed deliveries as well as Delivered, but its message says already completed.
- **Suggested fix:** Rename later to a terminal-state exception or use a message covering all terminal states.

### Read-Only Wrappers Are Recreated On Access

- **Severity:** Minor
- **File / Class / Method:** Restaurant.Products, Cart.Items, Customer.Addresses, Order.Items
- **Problem:** AsReadOnly creates a wrapper on each property access.
- **Suggested fix:** Optional optimization: cache a read-only wrapper. Current behavior is correct and safe.

## 6. Documentation Mismatches

### Delivery Implementation Roadmap Is Stale

- **Document / file:** `docs/delivery/delivery-implementation-roadmap.md`
- **Mismatch:** It says only documentation Step 1 is complete and Steps 2-6 are deferred, but GeoLocation, enums, exceptions, both aggregates, and DeliveryAssignmentDomainService now exist.
- **Current implementation decision:** Delivery Domain implementation through the assignment service is present.
- **Suggested documentation update:** Mark Steps 2-6 complete and retain repositories/Application/Infrastructure/API as deferred.

### Repository Design Omits Delivery Aggregate Roots

- **Document / file:** `docs/repository-interfaces-design.md`
- **Mismatch:** It lists only Restaurant, Cart, Order, and Customer aggregate repositories.
- **Current implementation decision:** Delivery and DeliveryAgent are separate MVP v2 aggregate roots.
- **Suggested documentation update:** Add an MVP v2 extension section for IDeliveryRepository and IDeliveryAgentRepository while preserving the MVP v1 list.

### Cart Creation Timing Differs

- **Document / file:** `docs/entity-design.md`, `docs/aggregates-and-invariants.md`, `docs/domain-services-design.md`, `docs/domain-failures-design.md`, `docs/repository-interfaces-design.md`, `docs/value-object-design.md`
- **Mismatch:** These files say Cart is not created until the first item is added. The public Cart constructor creates an empty Active Cart and AddItem later sets RestaurantId.
- **Current implementation decision:** Empty active in-memory Cart is currently possible.
- **Suggested documentation update:** Either describe it as transient and not persisted, or change the future construction design. Do not leave repository semantics ambiguous.

### Child IDs And Parent IDs Differ

- **Document / file:** `docs/entity-design.md`, `docs/aggregates-and-invariants.md`
- **Mismatch:** CartItem and OrderItem are documented with surrogate IDs and parent IDs; code contains neither. CustomerAddress is documented with required CustomerId; code keeps only its child ID and Address.
- **Current implementation decision:** Parent aggregate collections own the children directly; only CustomerAddress currently has a child ID.
- **Suggested documentation update:** Record the chosen owned/composite/surrogate-key strategy before EF mapping.

### ProductName Optionality Differs

- **Document / file:** `docs/entity-design.md`, `docs/aggregates-and-invariants.md`, `docs/domain-invariants.md`
- **Mismatch:** ProductName is described as optional convenience data, but CartItem and CatalogProductSnapshot require non-empty ProductName.
- **Current implementation decision:** ProductName is mandatory in Basket state.
- **Suggested documentation update:** Mark it required or change implementation in a separate decision.

### Customer Address Query Behaviors Are Not Implemented

- **Document / file:** `docs/entity-design.md`, `docs/aggregates-and-invariants.md`
- **Mismatch:** They list GetDefaultAddress and address-ownership query behavior; Customer intentionally has neither public GetAddress nor a snapshot-producing method.
- **Current implementation decision:** Callers can inspect a read-only address collection, while mutation remains protected.
- **Suggested documentation update:** Remove generic child lookup and describe the deferred CreateDeliveryAddressSnapshot(addressId) use-case-shaped method.

### Cart Pricing Documentation Is Consistent

- **Document / file:** Business rules, bounded contexts, entity/value-object/service designs
- **Mismatch:** None found for the current pricing decision.
- **Current implementation decision:** CartItem stores no price; Cart.GetTotal receives current prices; checkout uses Product.CurrentPrice; OrderItem stores historical UnitPrice and LineTotal.
- **Suggested documentation update:** No pricing correction required.

## 7. Aggregate-by-Aggregate Review

### Catalog

- **What is correct:** Restaurant is the root, owns a private Product list, creates Product with its own Id as RestaurantId, rejects duplicate product IDs, uses TimeRange and Money, and controls price/availability through internal Product methods.
- **What is wrong:** Duplicate/missing product operations throw generic InvalidOperationException.
- **What should be improved:** Add domain-specific failures when use cases require them; optionally validate productId in FindProduct for consistent structural behavior.

### Basket

- **What is correct:** Cart starts Active, owns a private CartItem list, enforces status/expiry/restaurant/quantity/availability rules, merges duplicate products, prevents post-terminal mutation, and calculates transient totals from caller-supplied current prices. CartItem stores no price.
- **What is wrong:** Empty-cart construction conflicts with documentation; missing item/current price failures are generic InvalidOperationException.
- **What should be improved:** Resolve construction/persistence semantics before ICartRepository and define business failures only where application workflows need them.

### Customer

- **What is correct:** Customer validates profile fields, owns a private address list, exposes a read-only collection, rejects duplicate address values, coordinates default flags, and contains no authentication concepts. CustomerAddress mutation is internal.
- **What is wrong:** UpdateProfile and AddAddress can partially mutate before throwing; duplicate child IDs are allowed; selected-address ownership has no use-case-shaped Domain operation.
- **What should be improved:** Make commands atomic, enforce child identity uniqueness, and add snapshot creation only when checkout Application work starts.

### Ordering

- **What is correct:** Order is factory-created, validates required IDs/address/items, creates OrderItem children internally, calculates total from line totals, exposes a read-only collection, and contains no CartItem/Product/Delivery lifecycle references. OrderItem is immutable and calculates LineTotal itself.
- **What is wrong:** No aggregate defect found in the current requested scope.
- **What should be improved:** Resolve documented child identity strategy before persistence mapping and define UTC timestamp policy.

### Checkout Domain Service

- **What is correct:** Stateless; no repositories, DbContext, saving, Order creation, Cart mutation, or Catalog mutation. It validates nulls, cart status/expiry/empty state, restaurant active/open state, product existence/availability, returns structured unavailable results, and uses Product.CurrentPrice.
- **What is wrong:** It does not verify that the supplied Restaurant is the Cart restaurant.
- **What should be improved:** Add the restaurant identity invariant before iterating products and test overlapping Product IDs across restaurants.

### Delivery Extension

- **What is correct:** Delivery and DeliveryAgent are separate roots in DeliveryManagement, use ID-only references, expose no mutable collections/navigation objects, enforce ordered transitions and agent identity, and coordinate assignment/completion through a stateless service. GeoLocation is immutable and validated. No alias remains.
- **What is wrong:** Cancellation/failure can strand an agent as Busy; MarkAvailable accepts non-Busy states; timestamp chronology and VehicleType validity are unchecked.
- **What should be improved:** Define release policy for every terminal path, tighten agent transitions, and decide timestamp/enum validation before persistence.

## 8. Encapsulation Review

- **Public setters found:** None. Mutable state uses private setters or get-only properties.
- **Mutable collections found:** No publicly mutable collections. Aggregate collections are private Lists exposed through read-only wrappers.
- **Child entity mutation risks:** Product, CartItem, CustomerAddress, and OrderItem constructors/mutators are internal where appropriate. Application code in another assembly cannot call internal child mutations.
- **Aggregate boundary violations:** No object-navigation collections such as Restaurant.Orders, Customer.Orders, DeliveryAgent.Deliveries, Delivery.Order, or Delivery.DeliveryAgent were found.
- **Remaining encapsulation risks:** Customer operations are not atomic on failure, and CustomerAddress identity uniqueness is not protected.

## 9. Dependency Review

- **EF / DbContext leakage:** None.
- **Repository leakage:** None.
- **API / HTTP leakage:** None.
- **Application-layer leakage:** None.
- **External service calls:** None.
- **Domain service state/dependencies:** Both services are stateless and receive already-loaded aggregates.
- **Domain project references/packages:** No package or project references in Talabat.Domain.csproj.
- **Result:** Clean

## 10. Business Rules Coverage

| Rule | Implemented | Missing | Partially Implemented | Notes |
|---|---|---|---|---|
| BR-CAT-001 |  |  | Yes | Active state exists; browse filtering belongs to a later read use case. |
| BR-CAT-002 | Yes |  |  | Restaurant creates Product with its own RestaurantId. |
| BR-CAT-003 |  |  | Yes | Availability exists; menu filtering is deferred. |
| BR-CAT-004 | Yes |  |  | Money rejects negative prices. |
| BR-CAT-005 | Yes |  |  | TimeRange rejects equal endpoints and supports overnight ranges. |
| BR-CART-001 |  |  | Yes | CustomerId exists; global active-cart uniqueness needs Application/persistence. |
| BR-CART-002 | Yes |  |  | Modification methods enforce one-hour expiry. |
| BR-CART-003 | Yes |  |  | Checkout service rejects expired carts. |
| BR-CART-004 | Yes |  |  | AddItem enforces one restaurant. |
| BR-CART-005 | Yes |  |  | Cart and CartItem reject non-positive quantities. |
| BR-CART-006 | Yes |  |  | Duplicate ProductId increases quantity. |
| BR-CART-007 | Yes |  |  | Current prices are supplied to Cart and read from Product at checkout. |
| BR-CART-008 | Yes |  |  | AddItem rejects unavailable snapshots. |
| BR-CART-009 | Yes |  |  | Clear empties items and sets Cleared. |
| BR-ORD-001 | Yes |  |  | Service and Order factory reject empty input. |
| BR-ORD-002 | Yes |  |  | Checkout rejects inactive restaurant. |
| BR-ORD-003 | Yes |  |  | Checkout checks TimeRange at current time. |
| BR-ORD-004 | Yes |  |  | Missing/unavailable products return structured result. |
| BR-ORD-005 | Yes |  |  | CheckoutItemSnapshot uses Product.CurrentPrice. |
| BR-ORD-006 | Yes |  |  | OrderItem stores immutable historical snapshot. |
| BR-ORD-007 | Yes |  |  | Order calculates TotalAmount internally. |
| BR-ORD-008 |  | Yes |  | Customer-scoped order queries are Application work. |
| BR-CUS-001 |  |  | Yes | Cart/Order carry CustomerId; profile setup/persistence is deferred. |
| BR-CUS-002 | Yes |  |  | Customer owns multiple addresses. |
| BR-CUS-003 |  |  | Yes | Normal path enforces one default; failed AddAddress can clear it. |
| BR-CUS-004 | Yes |  |  | Address value equality rejects duplicate details. |
| BR-CUS-005 |  |  | Yes | Checkout rejects null snapshot; selected-address ownership operation is deferred. |
| BR-CUS-006 | Yes |  |  | FullName is required, though failed profile update is non-atomic. |
| BR-CUS-007 |  |  | Yes | Age is validated, but failed update may change FullName first. |
| BR-CUS-008 | Yes |  |  | PhoneNumber is optional and normalized. |
| BR-DEL-001 |  | Yes |  | Delivery creation after Order is a deferred Application workflow. |
| BR-DEL-002 | Yes |  |  | Constructor starts PendingAssignment. |
| BR-DEL-003 | Yes |  |  | Assignment requires Available agent. |
| BR-DEL-004 |  |  | Yes | Busy state protects normal flow; concurrency requires filtered unique index. |
| BR-DEL-005 | Yes |  |  | AssignedAgentId and status reject reassignment. |
| BR-DEL-006 | Yes |  |  | Pickup requires assignment and prior arrival. |
| BR-DEL-007 | Yes |  |  | Pickup requires ArrivedAtRestaurant. |
| BR-DEL-008 | Yes |  |  | Delivery completion requires OutForDelivery. |
| BR-DEL-009 | Yes |  |  | EnsureNotTerminal protects all lifecycle commands. |
| BR-DEL-010 | Yes |  |  | Assignment service marks agent Busy. |
| BR-DEL-011 | Yes |  |  | Successful completion service marks agent Available. |
| BR-DEL-012 | Yes |  |  | Delivery requires DeliveryAddressSnapshot. |
| BR-DEL-013 | Yes |  |  | Delivery stores only related IDs and snapshot. |
| BR-DEL-014 | Yes |  |  | Location is optional and GeoLocation validates ranges. |

## 11. Test Recommendations

Do not write these tests until fixes and test-project scope are approved.

### Value Objects

- Money: negative/zero, Add immutability, Multiply non-positive quantity, overflow, value equality.
- TimeRange: equal endpoints, start/end boundary, normal range, midnight crossing.
- Address: trimming, required fields, optional floor normalization, case-insensitive equality/hash behavior.
- DeliveryAddressSnapshot and checkout snapshots: null/empty structural validation and value equality.
- GeoLocation: inclusive boundaries and out-of-range coordinates.

### Catalog

- Product creation through Restaurant, duplicate ProductId, RestaurantId ownership, current-price update, availability transitions, normal/overnight opening checks.

### Basket

- Active/expired/checked-out/cleared mutation paths, exact one-hour boundary, cross-restaurant add, unavailable add, duplicate merge, invalid quantity, empty checkout, missing current price, total calculation without stored prices.

### Customer

- Constructor/profile validation, atomic failed UpdateProfile, duplicate address value and ID, atomic failed default add, SetDefaultAddress unsets all others, missing address failures, default removal policy.

### Ordering

- Empty/null checkout input, missing address, immutable items, line-total calculation, total aggregation, null item elements, no caller-provided total.

### Checkout

- Null inputs, cart status/expiry/empty, inactive/closed restaurant, cart/restaurant identity mismatch, overlapping Product IDs across restaurants, missing/unavailable product details, current price/name snapshot success, no aggregate mutation.

### Delivery

- Initial states, assignment once, unavailable agent, every valid/invalid transition, wrong agent, terminal protection, cancellation cutoff, required failure reason, release after completion/cancel/fail, Busy/Offline/Suspended transitions, timestamp chronology, optional location, service null guards.

## 12. Repository Readiness

- **Ready to implement repository interfaces now?** No

### Must Be Fixed First

1. Enforce Cart.RestaurantId equals the Restaurant used by CheckoutDomainService.
2. Make Customer.UpdateProfile validation atomic.
3. Validate new address identity and construct it before changing default flags.
4. Reject duplicate CustomerAddress IDs.
5. Define and implement assigned-agent release for Delivery cancellation/failure before persisting those transitions.
6. Decide whether empty Active Cart objects are valid/persistable so ICartRepository semantics are unambiguous.
7. Resolve CartItem/OrderItem/CustomerAddress child identity and parent-key documentation before persistence mapping.
8. Restore a green full-solution build in a separate task, even though the Domain project already builds cleanly.

### Recommended Aggregate-Root Repositories After Fixes

- `IRestaurantRepository`
- `ICartRepository`
- `ICustomerRepository`
- `IOrderRepository`
- `IDeliveryRepository`
- `IDeliveryAgentRepository`
- `IUnitOfWork`

Do not create repositories for Product, CartItem, CustomerAddress, or OrderItem.

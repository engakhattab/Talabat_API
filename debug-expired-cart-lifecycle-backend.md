# Debug and Fix Expired Cart Lifecycle for Existing Customers

Act as a senior .NET backend engineer with strong experience in Domain-Driven Design, EF Core, concurrency, REST APIs, and production debugging.

## Repository and scope

Work primarily in the **Talabat backend repository**.

Expected backend location:

```text
D:\link-dev\talabat
```

The Angular frontend is only a reproduction client and must not be modified unless the backend investigation proves that the API already behaves correctly and the frontend is misinterpreting the response.

Frontend location for reproduction only:

```text
D:\link-dev\talabat-frontend\talabat-web
```

Customer API:

```text
https://localhost:7056
```

## Reported defect

There is a reproducible cart lifecycle issue.

### New customer accounts

- Adding a menu product succeeds.
- The cart contains the added item.
- The cart workflow behaves normally.

### Existing/older customer accounts

- The cart appears empty.
- The UI reports that the cart is expired.
- Adding any product fails.
- The failed request is:

```http
POST /api/me/cart/items
```

- The response status is:

```text
409 Conflict
```

- Signing out and signing in again does not solve the issue.
- The same behavior occurs with multiple older customer accounts.

This suggests that an expired, completed, checked-out, or otherwise non-mutable cart is persisted for older customers and is still being selected when they try to add a new item.

## Important constraints

- Diagnose the exact root cause before changing code.
- Do not begin with the full backend or frontend test suite.
- Do not make broad refactors.
- Do not change unrelated authentication, profile, catalog, checkout, or order behavior.
- Do not manually modify generated Angular API client files.
- Do not clear production-like database records merely to hide the defect.
- The cart, item prices, and totals are server authoritative.
- Preserve all existing domain invariants, especially:
  - one active cart per customer;
  - one restaurant per active cart;
  - product availability validation;
  - valid quantity rules;
  - concurrency protection;
  - server-authoritative totals.
- Do not claim the problem is fixed based only on unit tests. Reproduce and verify it using an affected old account.

## Phase 1 — Reproduce and capture evidence

1. Run the required backend hosts and the Angular customer application.

2. Reproduce the issue using:
   - one existing customer account that fails;
   - one newly created customer account that succeeds.

3. Capture the complete failed response for:

```http
POST /api/me/cart/items
```

Record:

- HTTP status;
- response body;
- `title`;
- `detail`;
- `errorCode`;
- correlation ID;
- request payload;
- authenticated customer/user identifier used by the backend.

4. Capture and compare:

```http
GET /api/me/cart
```

for both the old and new customer accounts.

5. Do not assume that `409` means one particular domain error. Use the actual response body and backend logs to identify the exact error.

## Phase 2 — Trace the backend execution path

Trace the complete request path for:

```http
POST /api/me/cart/items
```

Inspect all relevant code, including:

- Customer API endpoint/controller/minimal API mapping;
- request and command models;
- command handler/application service;
- customer identity resolution;
- cart repository interface and implementation;
- EF Core queries;
- cart aggregate root;
- cart item mutation methods;
- cart expiration rules;
- cart state/status transitions;
- checkout completion behavior;
- cart recreation or restart behavior;
- Unit of Work and transaction boundaries;
- concurrency tokens;
- global query filters;
- database mappings, indexes, and unique constraints.

Determine exactly how the backend chooses the cart for the current customer.

Look specifically for queries equivalent to:

```text
Get cart by CustomerId
Get latest cart by CustomerId
Single cart by CustomerId
```

that do not correctly distinguish between:

```text
Active
Expired
CheckedOut
Completed
Inactive
Cancelled
```

or any equivalent statuses used in the real domain model.

## Phase 3 — Inspect persisted data

Compare the database records for an affected old customer and a working new customer.

Inspect fields and relationships such as:

```text
CartId
CustomerId
Status
ExpiresAt
CreatedAt
UpdatedAt
RestaurantId
Version / RowVersion
Checkout or Order relationship
Cart items
```

Also inspect:

- whether an old account has more than one cart;
- whether an expired cart remains the only cart;
- whether a unique index on `CustomerId` prevents a replacement cart;
- whether a filtered unique index exists for active carts;
- whether the backend always returns the oldest or newest cart regardless of status;
- whether expiration is calculated dynamically or stored;
- whether checkout permanently changes the customer's only cart into a non-mutable state.

Do not edit database data manually until the application defect is understood.

## Phase 4 — Identify the exact root cause

State the exact failing execution path.

Possible examples are only investigation hints, not conclusions:

- The repository returns an expired cart instead of the active cart.
- The add-item handler detects expiration but never creates a new cart.
- The aggregate correctly rejects mutation, but the application layer has no new-cart transition.
- A unique `CustomerId` constraint prevents inserting another active cart.
- Checkout leaves the customer's cart permanently non-mutable.
- An expiration timestamp from older records is incompatible with newer domain behavior.
- A query filter hides valid carts or returns stale data.
- A concurrency conflict is incorrectly mapped as an expired-cart conflict.

Explain why:

- old accounts fail;
- new accounts work;
- sign-out/sign-in does not help.

## Required business behavior

Implement the smallest architecture-correct fix so the lifecycle behaves as follows:

```text
Customer has no cart:
    Create a new active cart and add the requested item.

Customer has an active mutable cart:
    Add the item while preserving all existing invariants.

Customer has an expired, checked-out, completed, inactive,
or otherwise non-mutable cart:
    Start a new active shopping cart according to the existing
    domain and persistence design, then add the requested item.

Customer has an active cart for another restaurant:
    Preserve the existing CrossRestaurantCart behavior.
```

Choose between:

- creating a new cart record; or
- restarting/resetting the existing aggregate;

only after inspecting the current domain model, database design, order history requirements, and constraints.

Do not erase historical cart/order information if the current model expects it to be retained.

## Concurrency and persistence requirements

The solution must:

- prevent two active carts for the same customer;
- handle two concurrent first-add requests safely;
- avoid duplicate cart creation;
- preserve optimistic concurrency behavior where used;
- run cart selection/creation and the first item addition inside a correct transaction boundary;
- use a database constraint where appropriate rather than relying only on application checks;
- keep historical carts queryable if the current business model requires them.

If a database migration is needed, explain the reason before applying it.

Examples that may be considered after inspecting the model:

- a filtered unique index for one active cart per customer;
- a repository method that retrieves only the current mutable cart;
- a transactional `get-or-create active cart` operation;
- an explicit aggregate/application lifecycle transition.

Do not select one of these blindly.

## Error handling requirements

After the fix:

- an old customer with only an expired cart must not receive a permanent `409` when starting a new shopping session;
- real domain conflicts must still return their intended typed Problem Details;
- `CrossRestaurantCart` must remain a conflict;
- unavailable products and invalid quantities must remain rejected;
- unknown failures must not be converted into misleading cart-expired messages.

Inspect whether the frontend currently shows an "expired" message using the actual backend `errorCode` or merely because it received status `409`.

Only modify the frontend if the backend behavior is correct and the frontend maps different `409` errors to the same incorrect message.

## Focused tests

Start with targeted tests only.

Add or update tests for:

1. Existing customer with no cart can add an item.
2. Existing customer with an active cart can add an item.
3. Existing customer with an expired cart can add an item and receives a new or correctly restarted active cart.
4. Existing customer with a checked-out/completed cart can begin a new cart if allowed by the real lifecycle.
5. Active cart from another restaurant still returns `CrossRestaurantCart`.
6. Unavailable product behavior remains unchanged.
7. Invalid quantity behavior remains unchanged.
8. Two concurrent first-add requests do not create two active carts.
9. A newly registered customer continues to work.
10. Historical cart/order data is not corrupted or detached.

Prefer:

- domain tests for aggregate transitions;
- application-handler tests for selection/create behavior;
- repository/integration tests for indexes and concurrency;
- one focused API integration test for the complete endpoint behavior.

## Verification sequence

After targeted tests pass:

1. Build only affected backend projects.
2. Run only affected cart/domain/application tests.
3. Run the focused repository or API integration tests.
4. Start the Customer API.
5. Verify manually with the old affected account.
6. Verify manually with a new account.
7. Run broader tests only if the changed dependency boundary requires them.

Do not waste time on unrelated frontend component tests or the entire solution before the focused defect is understood.

## Manual acceptance checks

Using an old affected account:

1. Sign in.
2. Open a restaurant menu.
3. Add one available product.
4. Confirm:

```http
POST /api/me/cart/items
```

returns success rather than the previous permanent conflict.

5. Open the cart and confirm the item exists.
6. Refresh the browser and confirm the active cart remains available.
7. Sign out and sign back in and confirm the active cart still works.
8. Add another product from the same restaurant.
9. Attempt to add from another restaurant and confirm the intended cross-restaurant conflict still occurs.

Using a new account:

1. Complete the required profile.
2. Add a product.
3. Confirm no regression.

Also verify the cart behavior after a successful checkout if the lifecycle is affected by checkout completion.

## Frontend decision rule

Do not change Angular merely because it is where the error is visible.

A frontend change is allowed only if investigation proves one of the following:

- the backend already creates a valid active cart but the frontend does not refresh its state;
- the frontend converts every `409` into an expired-cart message;
- the frontend retains stale cart state after a successful backend response;
- a generated API response is being interpreted incorrectly outside the generated client.

If a frontend fix is required:

- keep it separate from the backend fix;
- do not edit generated API client code;
- add a focused frontend test;
- report the frontend commit separately.

## Required final report

Provide a concise but complete report containing:

1. Exact root cause.
2. Evidence from the failed Problem Details response.
3. Why old accounts were affected.
4. Why new accounts worked.
5. Why sign-out/sign-in did not help.
6. The old failing execution path.
7. The corrected execution path.
8. Every changed file and why it changed.
9. Database/index/migration changes, if any.
10. Tests added and their results.
11. Commands executed.
12. Manual verification results using an old affected account.
13. Remaining risks or assumptions.
14. Whether any frontend modification was actually necessary.

Do not claim completion unless the original old-account scenario succeeds against the running Customer API.

# OpenCode Task: Make the OpenAPI Documents Client-Generation Ready

Repository: **Talabat_API**, branch `feature/user-aggregate-refactor`.
Do **not** branch from or target `main`.

The Angular frontend will generate its HTTP clients and TypeScript models from the OpenAPI documents
of `Talabat.Customer.API` and `Talabat.Delivery.API`. Those documents are currently not usable as a
generation source. This task fixes that.

---

## Why this is needed

Both hosts use `Microsoft.AspNetCore.OpenApi`, which can only document what the action signature and
attributes reveal. Today, across all 9 controllers in both hosts:

- **Every action returns bare `IActionResult`** — the document generator cannot infer a response type
- **Zero `[ProducesResponseType]` attributes** exist
- **No `operationId`** is set on any route
- **No explicit `[Tags]`** are declared

The resulting documents describe paths, parameters, and request bodies, but almost nothing about
responses. A generator pointed at them today produces:

```ts
apiCatalogRestaurantsGet(): Observable<any>
apiCatalogRestaurantsRestaurantIdMenuGet(restaurantId: number): Observable<any>
```

Method names synthesised from URL segments, and `any` in place of every model. That is worse than no
generated client: it imports the tooling cost and delivers none of the type safety.

The response DTOs already exist in `Talabat.Customer.API/Contracts/`. They simply are not declared.

---

## Already complete — DO NOT MODIFY

The Swagger **Authorize** button and OAuth2 wiring are done and working in both hosts:

- The `oauth2` security scheme document transformer (authorization-code + PKCE, `Talabat.Identity`)
- The operation transformer that emits `security` requirements for `[Authorize]` operations
- `UseSwaggerUI` with `OAuthClientId`, `OAuthUsePkce`, `OAuthScopes`
- `AddProblemDetails()` and `UseExceptionHandler`

Do not restructure, "improve", or re-order any of it. If a change is unavoidable, stop and report
instead.

---

## Scope

**In scope**
- `operationId` on every action in both business hosts
- Explicit `[Tags(...)]` on every controller in both business hosts
- `[ProducesResponseType<T>]` success and error declarations on every action
- Response contract records for `Talabat.Delivery.API` (currently missing)
- `[Produces]` / `[Consumes]` content-type declarations
- JSON enum serialisation decision (Task 6)
- Build-time OpenAPI document emission to a checked-in file
- Tests asserting document quality

**Out of scope — do not touch**
- Any handler, use case, aggregate, repository, migration, or business rule
- `IUserCapabilityService`, the `User` aggregate, `UserType`, approval workflow
- `IdentityServerConfig`, client registrations, scopes, redirect URIs, CORS
- The OAuth2 security scheme and operation transformers (see above)
- `ToActionResult`, `UseCaseResult`, `ApplicationError`, `DomainExceptionMapper` — the runtime
  mapping is correct; this task only *declares* what it already produces
- `Talabat.Identity` — the SPA uses OIDC discovery, not a generated client. No changes there.
- Any Angular work

---

## Task 1 — Stable `operationId` on every action

Add a route name to every action in both hosts, e.g. `[HttpGet("restaurants", Name = "GetRestaurants")]`.

Rules:
- The name must be **unique across the entire document** for that host.
- Use PascalCase. It becomes the Angular method name (`getRestaurants()`).
- Name the *operation*, not the route. `GetRestaurantMenu`, not `GetRestaurantsIdMenu`.
- Once set, treat these as a public contract. Renaming one is a breaking frontend change.

Suggested names — use the existing action names where they are already unique and unambiguous:

**`Talabat.Customer.API`**
`GetRestaurants`, `GetRestaurantMenu`, `GetCart`, `AddCartItem`, `UpdateCartItemQuantity`,
`RemoveCartItem`, `ClearCart`, `Checkout`, `GetOrders`, `GetOrderDetails`, `GetProfile`,
`CreateProfile`, `UpdateProfile`, `AddAddress`, `RemoveAddress`, `SetDefaultAddress`

**`Talabat.Delivery.API`**
`GetPendingDeliveries`, `GetActiveDelivery`, `GetDeliveryHistory`, `AssignDelivery`,
`ArrivedAtRestaurant`, `PickUpOrder`, `OutForDelivery`, `DeliverOrder`, `CancelDelivery`,
`FailDelivery`, `GoOnline`, `GoOffline`, `UpdateLocation`

Verify the emitted document has `operationId` on **100%** of operations.

---

## Task 2 — Explicit tags

Add `[Tags("...")]` to every controller. The tag becomes the generated Angular **service class name**,
so it must be stable and independent of the C# class name.

| Host | Controller | Tag |
|---|---|---|
| Customer | `CatalogController` | `Catalog` |
| Customer | `CartController` | `Cart` |
| Customer | `CheckoutController` | `Checkout` |
| Customer | `OrderController` | `Orders` |
| Customer | `CustomerController` | `Customer` |
| Customer | `AddressController` | `Addresses` |
| Delivery | `DeliveriesController` | `Deliveries` |
| Delivery | `StatusController` | `AgentStatus` |
| Delivery | `LocationController` | `AgentLocation` |

One tag per controller. Do not apply multiple tags to a single operation — most generators will emit
the operation into every matching service, producing duplicates.

---

## Task 3 — Declare success responses

For every action, add `[ProducesResponseType<TResponse>(StatusCodes.Status200OK)]` (or `201`/`204` as
appropriate) using the **existing** contract types under `Contracts/`.

Keep the `IActionResult` return type — the attribute is what the document generator reads. Do not
rewrite actions to `ActionResult<T>` in this task; that is a larger refactor and is not required.

Rules:
- Commands that return no body: declare `Status204NoContent` with no generic type.
- Do not introduce new response DTOs in `Talabat.Customer.API` — the types already exist.
- The declared type must match exactly what the action actually returns. A mismatch is worse than no
  declaration, because the Angular client will silently mis-type.

Add `[Produces("application/json")]` at controller level, and `[Consumes("application/json")]` on
actions with a request body.

---

## Task 4 — Create the missing `Talabat.Delivery.API` response contracts

`Talabat.Delivery.API/Contracts/` currently contains only `UpdateLocationRequest`. Audit every action
in that host and determine what it actually returns.

- Where an action returns an Application DTO directly (`ActiveDeliveryDto`, `PendingDeliveryDto`,
  `DeliveryHistoryDto`), decide whether to declare that type or introduce a host-level contract
  record. **Prefer a host-level contract record**, mirroring how `Talabat.Customer.API` is structured —
  it keeps the wire contract stable when Application DTOs change.
- Follow the existing `Talabat.Customer.API/Contracts/` conventions exactly: `sealed record`,
  one concern per file, `Contracts.<Area>` namespace.
- Reuse `MoneyDto` and any equivalent shared primitives rather than redefining them.

Consistency between the two hosts matters here: the Angular workspace will have two generated
libraries, and gratuitous shape differences between them become friction in shared UI code.

---

## Task 5 — Declare error responses

`ToActionResult` already maps `ApplicationErrorCategory` to status codes and returns
`ProblemDetails`. This task declares that behaviour in the document; it does not change it.

Current runtime mapping:

| Category | Status |
|---|---|
| `Validation` | 400 |
| `OwnershipMismatch` | 403 |
| `NotFound` | 404 |
| `Conflict` | 409 |
| `Unavailable` | 422 |
| (fallback) | 500 |

For each action, declare **only the statuses that action can actually produce** — do not blanket-apply
all six. Use `[ProducesResponseType<ProblemDetails>(StatusCodes.StatusXXX)]`.

Additionally:
- Every `[Authorize]` action declares `401`.
- Every action behind a scope or ownership policy declares `403`.
- Customer endpoints that require customer capability declare the `ProfileNotCreated` status.
  **Confirm whether that contract is 404 or 409 and make it consistent everywhere** — the plan
  documents refer to both. Report which one you found and standardise on it.

---

## Task 6 — Enum serialisation (decision required)

By default, .NET serialises enums as **integers**. The document then emits numeric enums, and the
Angular client gets `0 | 1 | 2` instead of meaningful names — unreadable in code and in network
inspection.

**Recommendation:** register `JsonStringEnumConverter` in both hosts so enums serialise as strings.

This is a **breaking wire-format change**. It is cheap now and expensive after the frontend exists.
Apply it, update any affected tests, and note it explicitly in the phase record. If any existing
integration test asserts a numeric enum value, fix the test — do not skip the converter to keep the
test green.

---

## Task 7 — Emit the document at build time

Add `Microsoft.Extensions.ApiDescription.Server` to both business hosts and configure
`<OpenApiDocumentsDirectory>` so each build writes an `openapi.json` into the repository.

Suggested locations:
- `src/Talabat/Talabat.API/openapi/customer-api.json`
- `src/Talabat/Talabat.Delivery.API/openapi/delivery-api.json`

Commit both files. Rationale: Angular client generation must not require a running API, and a contract
change becomes visible as a diff in code review.

Do not add a git hook or CI gate in this task — that is a follow-up.

---

## Task 8 — Document-quality tests

Add tests to the existing `Talabat.Customer.API.Tests` and `Talabat.Delivery.API.Tests` projects that
load the generated OpenAPI document and assert:

1. Every operation has a non-empty, unique `operationId`.
2. Every operation has at least one tag, and the tag set matches Task 2 exactly.
3. Every operation declares at least one success response with a resolved schema (no bare `object`,
   no missing `content`).
4. No operation declares a response schema that is an empty object.
5. Every `[Authorize]` operation declares `401`.
6. Every declared error status resolves to the `ProblemDetails` schema.
7. Enums in the document are string-valued (after Task 6).
8. The committed `openapi.json` matches the document produced at runtime — a drift test.

Test 8 is the important one: it turns "did you regenerate?" from a discipline problem into a build
failure.

---

## Task 9 — Verification

```
dotnet build src/Talabat/Talabat.slnx
dotnet test src/Talabat/Talabat.slnx
```

The whole solution must build and every test project must pass.

Then manually confirm, for **both** hosts:
- `/openapi/v1.json` loads without error
- Swagger UI at `/swagger` still loads and the **Authorize** button still performs a real PKCE login
- A protected endpoint still returns 200 with a token and 401 without one
- Response schemas are visible in the Swagger UI, not `string` or empty

---

## Acceptance criteria

- [ ] 100% of operations in both hosts have a unique `operationId`.
- [ ] Every controller in both hosts has exactly one explicit `[Tags(...)]`.
- [ ] Every action declares a success response; typed responses reference real contract records.
- [ ] `Talabat.Delivery.API` has response contracts consistent with `Talabat.Customer.API` conventions.
- [ ] Error statuses are declared per action against `ProblemDetails`, matching `ToActionResult`.
- [ ] The `ProfileNotCreated` status is standardised and reported.
- [ ] Enums serialise as strings in both hosts.
- [ ] `openapi.json` is emitted at build time and committed for both hosts.
- [ ] All 8 document-quality tests exist and pass; full solution build and test run are green.
- [ ] The Authorize button and OAuth2 wiring are unchanged and still functional.
- [ ] No handler, aggregate, repository, migration, or business rule was modified.

---

## Do not

- Do not modify the OAuth2 security scheme, operation transformer, or Swagger UI configuration.
- Do not change `ToActionResult`, `UseCaseResult`, or error-category-to-status mapping behaviour.
- Do not change any route path — only add route **names**.
- Do not rewrite actions to `ActionResult<T>`.
- Do not touch `Talabat.Identity`.
- Do not add authentication, authorization, or scope changes.
- Do not start any Angular or frontend work.
- Do not commit to `main`.

---

## Report back

When complete, state:
1. Which status code `ProfileNotCreated` was standardised to.
2. Any action where the actual response type could not be determined from the code.
3. Any test that had to change because of the enum-serialisation switch.

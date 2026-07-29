# OpenCode Task: Unify the `ProfileNotCreated` Error Contract + OpenAPI Cleanups

Repository: **Talabat_API**, branch `feature/user-aggregate-refactor`.
Do **not** branch from or target `main`.

Small follow-up to commit `3ebee68` (OpenAPI contract work). That commit was largely successful —
100% `operationId` coverage, correct tags, string enums, `ProblemDetails` declared throughout. This
task closes the one remaining correctness gap plus two cleanups.

---

## Task 1 — Fix the `ProfileNotCreated` response body (the blocker)

**File:** `src/Talabat/Talabat.API/Middleware/RequireCustomerProfileAttribute.cs`

### The problem

The OpenAPI document declares these responses as `ProblemDetails`:

```json
"409": { "content": { "application/json": {
  "schema": { "$ref": "#/components/schemas/ProblemDetails" } } } }
```

But the filter returns a hand-built anonymous object:

```csharp
context.Result = new ObjectResult(new
{
    type = $"https://tools.ietf.org/html/rfc9110#section-{rfcSection}",
    title = ...,
    status,
    detail = "A customer profile has not been created yet. Use POST /api/me/profile to create one.",
    extensions = new { errorCode = "ProfileNotCreated" }   // ← wrong
}) { StatusCode = status };
```

Two defects:

1. **It is not `ProblemDetails`.** The document promises a type the runtime does not produce. The
   generated Angular client will be typed against `ProblemDetails` and silently receive a different
   shape. The compiler vouches for something false — worse than declaring nothing.
2. **`extensions` is nested.** Under RFC 9457, ProblemDetails extension members serialise at the
   **root** of the object. `System.Text.Json` serialising a real `ProblemDetails` places
   `Extensions` dictionary entries at root level. This code produces a literal nested
   `"extensions": { "errorCode": "..." }` property instead, so every other error in the API — all of
   which flow through `ToActionResult` and produce genuine `ProblemDetails` — has a different shape
   from this one.

Net effect: the API emits **two incompatible error contracts**, and the frontend would need two code
paths for what should be one.

### The fix

Replace the anonymous object with a real `Microsoft.AspNetCore.Mvc.ProblemDetails` instance and place
`errorCode` in its `Extensions` dictionary:

- `Status` — the status code
- `Title` — matching the status
- `Type` — the existing RFC 9110 section URI
- `Detail` — keep the existing message verbatim
- `Extensions["errorCode"]` — `"ProfileNotCreated"`

Return it so the response body matches what the document declares.

### DO NOT change the status codes

The `notFoundOnMissing` flag is **intentional and correct**. Keep it exactly as is:

| Endpoint | Status | Rationale |
|---|---|---|
| `GET /api/me/profile` (`notFoundOnMissing: true`) | **404** | The requested resource genuinely does not exist |
| `PUT /api/me/profile`, cart, orders, addresses | **409** | The resource exists conceptually; account state conflicts with the request |

An earlier review suggested standardising on 409. **That suggestion was wrong and is withdrawn.**
Do not collapse these to a single code. Do not remove the `notFoundOnMissing` parameter. Do not
change any call site.

Only the **response body shape** changes in this task.

### Verify the shape is consistent

Compare the output against what `ToActionResult`'s `MapError` produces for a 409. Both must have
the same top-level property set and the same placement of extension members. If they differ in any
way other than `detail`, the fix is incomplete.

---

## Task 2 — Correct three delivery responses to `204 No Content`

**File:** `src/Talabat/Talabat.Delivery.API/Controllers/StatusController.cs`,
`LocationController.cs`

These three operations declare `200 OK` with an empty content object and no schema:

- `PUT /api/agent/location`
- `PUT /api/agent/status/online`
- `PUT /api/agent/status/offline`

The document emits `"content": { "application/json": {} }`, which generates `Observable<any>` in the
Angular client.

These are commands with no response body. Change them to declare and return **`204 No Content`**:

- `[ProducesResponseType(StatusCodes.Status204NoContent)]`
- Ensure the action actually returns `NoContent()` on success, not `Ok()`

Keep all existing error response declarations unchanged.

If any of these three genuinely does return a body, stop and report it instead of forcing 204.

---

## Task 3 — Remove the duplicate OpenAPI documents

Each host currently writes two byte-identical documents:

```
src/Talabat/Talabat.API/openapi/Talabat.Customer.API.json
src/Talabat/Talabat.API/openapi/customer-api.json

src/Talabat/Talabat.Delivery.API/openapi/Talabat.Delivery.API.json
src/Talabat/Talabat.Delivery.API/openapi/delivery-api.json
```

This is the `Microsoft.Extensions.ApiDescription.Server` default filename plus a configured one. Two
copies means two things to keep in sync, and the drift test only guards one of them.

- Keep **`customer-api.json`** and **`delivery-api.json`**.
- Delete the `Talabat.*.API.json` variants and ensure the build no longer regenerates them
  (configure the document name / output filename in each `.csproj` rather than deleting files that
  the next build recreates).
- Confirm `OpenApiDocumentQualityTests` and the drift assertion point at the retained filenames.

---

## Task 4 — Two verification items (report only, change only if clearly wrong)

1. **`POST /api/me/profile` does not declare `409`.** Creating a profile when one already exists is a
   plausible conflict. Determine whether the handler can return a `Conflict` category error. If it
   can, add `[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]`. If it cannot,
   report that and change nothing.

2. **`ToActionResultExtensions.ToCreatedAtAction`** accepts `actionName` and `routeValues` parameters
   and uses neither — it simply invokes `onSuccess`. The name implies a `CreatedAtAction` result that
   is never produced. Report this. Do not rename or refactor it in this task.

---

## Task 5 — Tests

Add to `tests/Talabat.Customer.API.Tests`:

1. A **runtime shape** test: request an owner-scoped endpoint as an authenticated user with no
   customer capability, then assert on the deserialised JSON that `errorCode` is at the **root** and
   that no `extensions` property exists.
2. Assert `GET /api/me/profile` still returns **404** with that shape.
3. Assert a 409-variant endpoint (cart or orders) returns **409** with that shape.
4. Assert the `ProfileNotCreated` body has the **same top-level property set** as a `ProblemDetails`
   produced by `ToActionResult` for the same status.

Add to `tests/Talabat.Delivery.API.Tests`:

5. Assert the three status/location operations return `204` at runtime and declare `204` in the
   document with no success content.

Existing tests in `AuthEnforcementTests`, `CustomerEndpointTests`, `ErrorMappingTests`, and
`OwnershipTests` reference `ProfileNotCreated`. Update any that assert the old nested shape. **Do not
weaken or delete an assertion to make it pass** — if a test fails because the shape changed, update
it to assert the new correct shape.

---

## Task 6 — Regenerate and verify

```
dotnet build src/Talabat/Talabat.slnx
dotnet test src/Talabat/Talabat.slnx
```

Then confirm in the regenerated documents:
- Exactly one `.json` per host under `openapi/`
- The three delivery operations declare `204` with no success content
- No operation declares a success response with empty content
- `operationId` coverage is still 100% and unchanged
- Enums are still string-valued

Commit the regenerated documents.

---

## Acceptance criteria

- [ ] `RequireCustomerProfileAttribute` returns a real `ProblemDetails` with `errorCode` in
      `Extensions`, serialising at the root.
- [ ] The `ProfileNotCreated` body shape is identical to `ToActionResult`'s `ProblemDetails` output.
- [ ] `notFoundOnMissing` and all call sites are unchanged; `GET /api/me/profile` still returns 404
      and the others still return 409.
- [ ] The three delivery command operations declare and return `204 No Content`.
- [ ] Exactly one OpenAPI document per host; the build does not recreate the deleted variant.
- [ ] Drift test and quality tests reference the retained filenames and pass.
- [ ] All new tests exist and pass; full solution build and test run are green.
- [ ] Task 4 items reported.

---

## Do not

- Do not change any status code, route, `operationId`, or tag.
- Do not modify `ToActionResult`, `UseCaseResult`, `ApplicationError`, or `MapError` behaviour.
- Do not touch the OAuth2 security scheme, operation transformer, or Swagger UI config.
- Do not touch `Talabat.Identity`, any handler, aggregate, repository, or migration.
- Do not remove the `notFoundOnMissing` parameter.
- Do not weaken an existing assertion to make a test pass.
- Do not start any Angular work.
- Do not commit to `main`.

---

## Report back

1. Whether `POST /api/me/profile` can return a `Conflict` error (Task 4.1) and what you did.
2. Any of the three delivery operations that turned out to return a body.
3. Which existing tests had to be updated for the new error shape.

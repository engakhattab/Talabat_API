# OpenCode Task: Swagger UI OAuth2 + PKCE Login (Option B)

Repository: **Talabat_API**, branch `feature/user-aggregate-refactor`.
Phase 10 remediation is complete and the full test suite is green. This is a small, focused
follow-up: make the **Authorize** button in Swagger UI perform a real OIDC authorization-code +
PKCE login against `Talabat.Identity`, instead of only rendering a decorative button.

---

## Why this is needed

All three hosts currently register a `Bearer` security **scheme** in their OpenAPI document but
declare **no security requirement** anywhere. Swagger UI therefore renders the Authorize button,
accepts a token, and then sends every "Try it out" request **without** an `Authorization` header —
so protected endpoints return 401 and the button appears broken.

There is also a latent crash: in **Microsoft.OpenApi 2.x** the collection properties on
`OpenApiComponents` are nullable and are no longer auto-initialised. `document.Components ??= new()`
guards the components object but not the dictionary inside it, so
`document.Components.SecuritySchemes["Bearer"] = …` can throw `NullReferenceException`, which
surfaces as `/openapi/v1.json` returning 500 and Swagger UI failing to load at all.

Both are fixed below.

---

## Scope

**In scope**
- Replace the `Bearer` scheme with an OAuth2 authorization-code + PKCE scheme in
  `Talabat.API` and `Talabat.Delivery.API`
- Emit a `security` requirement on `[Authorize]` operations
- Configure Swagger UI's OAuth client
- Register the Swagger redirect URIs and CORS origins in `IdentityServerConfig`
- Remove the misleading Bearer scheme from the `Talabat.Identity` host
- Add automated coverage for both the OpenAPI document shape and the client configuration

**Out of scope — do not touch**
- Any authorization policy, scope, audience, role, or claim
- `ICurrentUser`, `CurrentUserCapabilityResolver`, `TryGetAgentIdHelper`
- Any handler, controller action, aggregate, repository, or migration
- Token lifetimes, refresh-token settings, `TalabatProfileService`
- Creating a dedicated Swagger OIDC client (reuse the existing SPA clients — see Task 3 note)
- `specs/`, `.specify/`, `docs/` — this is a wiring fix, not a spec change

---

## Facts you need (verified against the repo — do not re-derive)

| Item | Value |
|---|---|
| Identity host | `https://localhost:7237` |
| Customer API | `https://localhost:7056` |
| Delivery API | `https://localhost:7225` |
| Customer client id | `talabat-customer-spa` |
| Delivery client id | `talabat-delivery-spa` |
| Customer scopes | `openid`, `profile`, `roles`, `customer.api`, `offline_access` |
| Delivery scopes | `openid`, `profile`, `roles`, `delivery.api`, `offline_access` |
| Authorize endpoint | `https://localhost:7237/connect/authorize` |
| Token endpoint | `https://localhost:7237/connect/token` |
| Packages | `Microsoft.AspNetCore.OpenApi` 10.0.9, `Microsoft.OpenApi` 2.7.5, `Swashbuckle.AspNetCore.SwaggerUI` 10.2.3 |

Both SPA clients already have `RequirePkce = true` and `RequireClientSecret = false`, which is
exactly what Swagger UI needs. Do not change either.

---

## Task 1 — `src/Talabat/Talabat.API/Program.cs`

Replace the existing `AddOpenApi(...)` block in its entirety:

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["oauth2"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "OIDC authorization code flow with PKCE against Talabat.Identity.",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri("https://localhost:7237/connect/authorize"),
                    TokenUrl         = new Uri("https://localhost:7237/connect/token"),
                    Scopes = new Dictionary<string, string>
                    {
                        ["openid"]       = "Subject identifier",
                        ["profile"]      = "Profile claims",
                        ["roles"]        = "Role claims",
                        ["customer.api"] = "Customer API access"
                    }
                }
            }
        };

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        var requiresAuth = metadata.OfType<IAuthorizeData>().Any()
                        && !metadata.OfType<IAllowAnonymous>().Any();

        if (requiresAuth)
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("oauth2", context.Document)] =
                        new List<string> { "customer.api" }
                }
            ];
        }

        return Task.CompletedTask;
    });
});
```

Required usings: `Microsoft.AspNetCore.Authorization`, `Microsoft.OpenApi`.

Then update the Swagger UI registration:

```csharp
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Talabat Customer API v1");
    options.RoutePrefix = "swagger";

    options.OAuthClientId("talabat-customer-spa");
    options.OAuthUsePkce();
    options.OAuthScopes("openid", "profile", "roles", "customer.api");
    options.OAuthAppName("Talabat Customer API — Swagger");
});
```

**Do not** call `OAuthClientSecret`. These are public clients; sending a secret will fail the token
request.

### If `context.Document` does not compile

`OpenApiOperationTransformerContext.Document` should exist on this SDK. If it does not, pass `null`
as the second argument — the reference only needs the scheme name to serialise as a `$ref`:

```csharp
[new OpenApiSecuritySchemeReference("oauth2", null)] = new List<string> { "customer.api" }
```

Report it if you have to use the fallback.

---

## Task 2 — `src/Talabat/Talabat.Delivery.API/Program.cs`

Identical to Task 1 with these substitutions:

- Scope key and description: `["delivery.api"] = "Delivery API access"` (instead of `customer.api`)
- Security requirement scope list: `new List<string> { "delivery.api" }`
- `options.OAuthClientId("talabat-delivery-spa")`
- `options.OAuthScopes("openid", "profile", "roles", "delivery.api")`
- `options.OAuthAppName("Talabat Delivery API — Swagger")`
- Endpoint title stays `"Talabat Delivery API v1"`

The authorize/token URLs are the same — both APIs trust the one Identity host.

---

## Task 3 — `src/Talabat/Talabat.Identity/IdentityServerConfig.cs`

Swagger UI performs the redirect from the API host's own origin, so its callback must be a
registered redirect URI, and the API origin must be allowed for the CORS'd token request.

**`talabat-customer-spa`:**

```csharp
RedirectUris =
{
    "http://localhost:4200/signin-callback",
    "https://oauth.pstmn.io/v1/callback",                    // dev only — remove before production
    "https://oauth.pstmn.io/v1/browser-callback",            // dev only — remove before production
    "https://localhost:7056/swagger/oauth2-redirect.html"    // dev only — remove before production
},
AllowedCorsOrigins = { "http://localhost:4200", "https://localhost:7056" },
```

**`talabat-delivery-spa`:**

```csharp
RedirectUris =
{
    "http://localhost:4300/signin-callback",
    "https://oauth.pstmn.io/v1/callback",                    // dev only — remove before production
    "https://oauth.pstmn.io/v1/browser-callback",            // dev only — remove before production
    "https://localhost:7225/swagger/oauth2-redirect.html"    // dev only — remove before production
},
AllowedCorsOrigins = { "http://localhost:4300", "https://localhost:7225" },
```

Change nothing else in this file. Keep the `// dev only` comments — they are the only marker that
these come out before deployment.

> **Note on reusing the SPA clients.** A dedicated `talabat-swagger-customer` client would be
> cleaner separation, but it duplicates scope and PKCE config for a dev-only tool. Reusing the SPA
> clients with clearly-marked dev-only redirect URIs is the intended approach here. Do not create
> new clients.

---

## Task 4 — `src/Talabat/Talabat.Identity/Program.cs`

The Identity host registers a `Bearer` JWT security scheme in its own OpenAPI document. That host
authenticates with **cookies** via ASP.NET Core Identity, not bearer tokens, so the scheme is
misleading and unusable.

Remove the document transformer entirely and reduce the call to:

```csharp
builder.Services.AddOpenApi();
```

Leave `UseSwaggerUI` as it is. Do not add an OAuth2 scheme here — the Identity host is the
authorization server, not a resource server.

---

## Task 5 — Tests

### 5a. OpenAPI document shape (new)

`tests/Talabat.Customer.API.Tests/OpenApiSecurityDocumentTests.cs` and
`tests/Talabat.Delivery.API.Tests/OpenApiSecurityDocumentTests.cs`.

Reuse the existing `CustomWebApplicationFactory` (it already sets `UseEnvironment("Development")`,
which is required — `/openapi/v1.json` is only mapped in Development). Fetch the document and
assert on the parsed JSON with `System.Text.Json`:

```csharp
[Fact]
public async Task OpenApiDocument_DeclaresOAuth2SchemeWithPkceFlow()
{
    var json = await _client.GetStringAsync("/openapi/v1.json");
    using var doc = JsonDocument.Parse(json);

    var scheme = doc.RootElement
        .GetProperty("components")
        .GetProperty("securitySchemes")
        .GetProperty("oauth2");

    Assert.Equal("oauth2", scheme.GetProperty("type").GetString());

    var flow = scheme.GetProperty("flows").GetProperty("authorizationCode");
    Assert.Equal("https://localhost:7237/connect/authorize", flow.GetProperty("authorizationUrl").GetString());
    Assert.Equal("https://localhost:7237/connect/token",     flow.GetProperty("tokenUrl").GetString());
    Assert.True(flow.GetProperty("scopes").TryGetProperty("customer.api", out _));   // delivery.api in the Delivery test
}

[Fact]
public async Task ProtectedOperations_DeclareSecurityRequirement()
{
    var json = await _client.GetStringAsync("/openapi/v1.json");
    using var doc = JsonDocument.Parse(json);

    // GET /api/me/profile is [Authorize]-protected and must carry a security requirement.
    var operation = doc.RootElement
        .GetProperty("paths")
        .GetProperty("/api/me/profile")
        .GetProperty("get");

    Assert.True(operation.TryGetProperty("security", out var security));
    Assert.True(security.GetArrayLength() > 0);
}
```

For the Delivery host, use `/api/agent/deliveries/pending` as the protected operation and
`delivery.api` as the scope.

**Also add a negative case** proving the operation transformer discriminates rather than blanket-
applying: pick an `[AllowAnonymous]` catalog endpoint in the Customer API and assert it has **no**
`security` property. If every operation in the Customer API is protected, say so in your report
instead of inventing an anonymous endpoint.

### 5b. Client configuration (extend existing)

`tests/Talabat.Identity.Tests/IdentityServerConfigTests.cs` already exists. Add assertions that:

- `talabat-customer-spa.RedirectUris` contains `https://localhost:7056/swagger/oauth2-redirect.html`
- `talabat-delivery-spa.RedirectUris` contains `https://localhost:7225/swagger/oauth2-redirect.html`
- `talabat-customer-spa.AllowedCorsOrigins` contains `https://localhost:7056`
- `talabat-delivery-spa.AllowedCorsOrigins` contains `https://localhost:7225`
- both clients still have `RequirePkce == true` and `RequireClientSecret == false`
- the previously-fixed Postman URIs (`/v1/callback` **and** `/v1/browser-callback`) are still present
  on both clients — this is a regression guard, they were accidentally replaced once before

---

## Manual verification (required — automated tests cannot cover the browser redirect)

Run all three hosts, then:

1. Open `https://localhost:7056/swagger`. Confirm the page loads and an **Authorize** button is
   visible. If the page is blank or `/openapi/v1.json` returns 500, the `SecuritySchemes` null-init
   is missing.
2. Click **Authorize**. You should see the `oauth2` scheme with checkboxes for `openid`, `profile`,
   `roles`, `customer.api` — **not** a plain "Value" text box. A text box means the OAuth2 scheme
   did not replace the Bearer one.
3. Select all scopes → **Authorize**. A popup should navigate to
   `https://localhost:7237/auth/login`.
4. Log in with a seeded customer. The popup should return to
   `https://localhost:7056/swagger/oauth2-redirect.html` and close.
5. Execute `GET /api/me/profile` → expect **200**, and confirm the curl preview shows an
   `Authorization: Bearer …` header. **This header is the whole point of the task** — if it is
   absent, the security requirement is not being emitted.
6. Repeat 1–5 on `https://localhost:7225/swagger` with a seeded delivery agent and
   `GET /api/agent/deliveries/pending`.
7. Cross-check isolation: with a *customer* token still authorized in the Delivery Swagger UI,
   confirm you get **401** (audience mismatch). This should already hold — you are confirming no
   regression, not adding behaviour.

---

## Known gotchas

- **CORS is the most likely failure.** The token request goes from origin `https://localhost:7056`
  to `https://localhost:7237/connect/token`. If `AllowedCorsOrigins` is missing the API origin, the
  login popup closes and Swagger silently shows no token. Symptom: a CORS error in the browser
  console, not in any server log. Task 3 fixes this — do not skip it.
- **Dev certificate trust.** All three hosts run on HTTPS. If `dotnet dev-certs https --trust` has
  not been run, the popup fails opaquely. Check this before debugging anything else.
- **Access tokens now live 900 seconds.** A Swagger session goes stale after 15 minutes; re-click
  Authorize. This is correct behaviour, not a bug — do not raise the lifetime to work around it.
- **`EmitStaticAudienceClaim = true`** means tokens carry both the API resource name and
  `https://localhost:7237/resources`. Audience validation already targets the resource name. Do not
  change this setting.
- **Do not add `AllowAccessTokensViaBrowser`** or any implicit-flow setting. The flow is
  authorization code + PKCE and must stay that way.

---

## Gate before commit

```bash
dotnet build src/Talabat/Talabat.slnx -c Release --no-restore
dotnet test  src/Talabat/Talabat.slnx -c Release --no-build
dotnet list  src/Talabat/Talabat.slnx package --vulnerable --include-transitive
```

Done only when all of the following hold:

- [ ] Build succeeds with zero new warnings
- [ ] Every test project passes (the suite was green before this change — keep it green)
- [ ] `Talabat.ArchitectureTests` passes with no new exemptions
- [ ] New OpenAPI document tests pass on both API hosts
- [ ] `IdentityServerConfigTests` additions pass, including the Postman regression guard
- [ ] Manual steps 1–7 completed, with the `Authorization: Bearer …` header confirmed in step 5
- [ ] No new `PackageReference` in `Talabat.Domain` or `Talabat.Application`
- [ ] No changes outside the files named in Tasks 1–5

Commit as:

```
feat(openapi): OAuth2 authorization code + PKCE login in Swagger UI
```

---

## Reporting

1. Files modified, and the exact diff for each `Program.cs`
2. Whether `context.Document` compiled, or the `null` fallback was needed
3. Test results before and after
4. Manual verification outcome for **each** of steps 1–7, stating explicitly whether the
   `Authorization` header appeared in step 5
5. Anything you chose not to do, and why

If the OAuth2 popup fails, report the **browser console error** and the Identity host log line —
not just "it didn't work." Stop and ask rather than loosening CORS, disabling PKCE, adding a client
secret, or switching to implicit flow.

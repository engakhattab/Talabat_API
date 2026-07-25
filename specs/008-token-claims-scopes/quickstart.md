# Quickstart Guide: Token, Claims, and Scopes Refinement (Phase 9)

## Overview
This guide describes how to run and test the Phase 9 centralized authentication setup across `Talabat.Identity`, `Talabat.Customer.API`, and `Talabat.Delivery.API`.

---

## 1. Running the Host Services

### Option A: Using Solution Launcher (Visual Studio)
1. Open `src/Talabat/Talabat.slnx`.
2. Set multiple startup projects:
   - `Talabat.Identity` (https://localhost:7237)
   - `Talabat.Customer.API` (https://localhost:7084)
   - `Talabat.Delivery.API` (https://localhost:7198)

### Option B: Using .NET CLI
```bash
# Terminal 1: Start Identity Server
dotnet run --project src/Talabat/Talabat.Identity/Talabat.Identity.csproj

# Terminal 2: Start Customer API
dotnet run --project src/Talabat/Talabat.API/Talabat.Customer.API.csproj

# Terminal 3: Start Delivery API
dotnet run --project src/Talabat/Talabat.Delivery.API/Talabat.Delivery.API.csproj
```

---

## 2. Testing OIDC Authentication & Token Issuance

### Step 1: Verify OIDC Discovery Metadata
Open browser or Postman and GET:
`https://localhost:7237/.well-known/openid-configuration`
Confirm standard endpoints (`jwks_uri`, `authorization_endpoint`, `token_endpoint`) are returned.

### Step 2: Request Token via PKCE Flow (Postman / Web Browser)
1. Initiate Authorization request:
   `https://localhost:7237/connect/authorize?client_id=talabat-customer-spa&response_type=code&scope=openid%20profile%20roles%20customer.api&redirect_uri=http://localhost:4200/signin-callback&code_challenge=<CHALLENGE>&code_challenge_method=S256`
2. Authenticate using dev credentials.
3. Capture `code` parameter from redirect.
4. POST to `https://localhost:7237/connect/token`:
   - `grant_type=authorization_code`
   - `client_id=talabat-customer-spa`
   - `code=<code>`
   - `redirect_uri=http://localhost:4200/signin-callback`
   - `code_verifier=<VERIFIER>`
5. Receive access token JWT.

---

## 3. Testing API Bearer Validation & Audience Isolation

### Test 1: Valid Customer Token on Customer API
- Request: `GET https://localhost:7084/api/me/profile`
- Header: `Authorization: Bearer <CUSTOMER_JWT>`
- Expected Result: `200 OK` (or `404 ProfileNotCreated` if profile not yet populated; token authentication succeeds).

### Test 2: Cross-API Token Presentation (Customer Token on Delivery API)
- Request: `GET https://localhost:7198/api/delivery-agent/dashboard`
- Header: `Authorization: Bearer <CUSTOMER_JWT>` (Audience: `talabat.customer.api`)
- Expected Result: `403 Forbidden` (Audience mismatch rejected by JwtBearer middleware).

# OpenID Connect & API Authorization Contracts

## 1. Centralized Identity Server Endpoints (`Talabat.Identity`)

### 1.1 OIDC Discovery Endpoint
- **URL**: `GET /.well-known/openid-configuration`
- **Response**: `200 OK` (Application/JSON) containing OIDC metadata (issuer, authorization_endpoint, token_endpoint, jwks_uri, response_types_supported, subject_types_supported, id_token_signing_alg_values_supported).

### 1.2 Authorization Endpoint (Authorization Code + PKCE)
- **URL**: `GET /connect/authorize`
- **Query Parameters**:
  - `client_id`: `talabat-customer-spa` or `talabat-delivery-spa`
  - `response_type`: `code`
  - `scope`: `openid profile roles customer.api` (or `delivery.api`)
  - `redirect_uri`: `http://localhost:4200/signin-callback`
  - `code_challenge`: `<BASE64URL_SHA256(code_verifier)>`
  - `code_challenge_method`: `S256`
  - `state`: `<random_string>`
- **Response**: `302 Found` redirecting to `redirect_uri?code=<code>&state=<state>`.

### 1.3 Token Exchange Endpoint
- **URL**: `POST /connect/token`
- **Content-Type**: `application/x-www-form-urlencoded`
- **Body**:
  - `grant_type`: `authorization_code`
  - `client_id`: `talabat-customer-spa` or `talabat-delivery-spa`
  - `code`: `<code>`
  - `redirect_uri`: `http://localhost:4200/signin-callback`
  - `code_verifier`: `<plain_code_verifier>`
- **Response**: `200 OK` (Application/JSON)
  ```json
  {
    "access_token": "<JWT_STRING>",
    "expires_in": 3600,
    "token_type": "Bearer",
    "refresh_token": "<REFRESH_TOKEN_STRING>",
    "scope": "openid profile roles customer.api"
  }
  ```

---

## 2. Business API Host Validation Contracts

### 2.1 Customer API (`Talabat.Customer.API`)
- **Header Required**: `Authorization: Bearer <JWT_ACCESS_TOKEN>`
- **Validation Rules**:
  1. Issuer (`iss`) matches `Talabat.Identity` Authority URL.
  2. Signature validates against JWKS from `Talabat.Identity`.
  3. Audience (`aud`) equals `talabat.customer.api`.
  4. Expiration (`exp`) has not passed.
  5. Scope claim contains `customer.api`.
- **Error Responses**:
  - Missing/Invalid Signature/Expired: `401 Unauthorized`.
  - Wrong Audience/Scope: `403 Forbidden`.

### 2.2 Delivery Agent API (`Talabat.Delivery.API`)
- **Header Required**: `Authorization: Bearer <JWT_ACCESS_TOKEN>`
- **Validation Rules**:
  1. Issuer (`iss`) matches `Talabat.Identity` Authority URL.
  2. Signature validates against JWKS from `Talabat.Identity`.
  3. Audience (`aud`) equals `talabat.delivery.api`.
  4. Expiration (`exp`) has not passed.
  5. Scope claim contains `delivery.api`.
- **Error Responses**:
  - Missing/Invalid Signature/Expired: `401 Unauthorized`.
  - Wrong Audience/Scope: `403 Forbidden`.

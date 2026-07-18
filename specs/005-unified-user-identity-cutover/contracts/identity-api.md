# HTTP Contract: Unified User Identity Phase 2

## Registration endpoints

The old `POST /account/register` route is removed. No compatibility alias remains.

### POST /account/register/customer

Request:

```json
{
  "email": "customer@example.com",
  "password": "P@ssw0rd123!",
  "fullName": "Customer Name",
  "age": 30,
  "phoneNumber": "+201000000000"
}
```

`phoneNumber` is optional/nullable. There is no role or capability field.

Success: HTTP 200

```json
{
  "id": 1,
  "email": "customer@example.com"
}
```

The user is active and atomically has Customer flag, Customer role, full name, age, and phone.

### POST /account/register/delivery-agent

Request:

```json
{
  "email": "agent@example.com",
  "password": "P@ssw0rd123!",
  "fullName": "Agent Name",
  "vehicleType": 2,
  "phoneNumber": "+201111111111"
}
```

`vehicleType` uses existing numeric enum values:

| JSON value | Meaning |
|---:|---|
| 1 | Bike |
| 2 | Motorcycle |
| 3 | Car |

Success: HTTP 200 with the same numeric-ID/email response shape as customer registration.

The created user is active and PendingApproval with the vehicle/phone, but has `UserType.None`, no
DeliveryAgent role, and null `DeliveryAgentStatus`.

### Registration failures

Identity validation failure, including duplicate normalized email/sign-in name: HTTP 400.

```json
{
  "errors": [
    "<deterministic Identity error description(s)>"
  ]
}
```

The endpoint wraps the single deterministic Application error message in `errors`. No partial user,
profile, application, flag, role, or stamp change is committed.

Domain validation also returns HTTP 400 using the host's established validation mapping. Password
hash, security stamp, concurrency stamp, and stack traces are never serialized.

## Login

### POST /account/login

Request shape is unchanged:

```json
{
  "email": "customer@example.com",
  "password": "P@ssw0rd123!"
}
```

Success is unchanged: HTTP 200 and application cookie.

```json
{"message":"logged in"}
```

Invalid credentials, inactive user, or soft-deleted user: HTTP 401 with empty body. Do not reveal
which condition caused denial.

## Logout

### POST /account/logout

Success remains HTTP 200:

```json
{"message":"logged out"}
```

## Current account

### GET /account/me

Requires the Identity application cookie. Success remains the same fields, but `id` is now a JSON
number:

```json
{
  "id": 1,
  "email": "customer@example.com"
}
```

Missing/invalid session: HTTP 401. No Domain aggregate is returned.

## Customer API compatibility contract

These serialized bodies are exact fixtures. Do not refactor the anonymous objects, property order,
spelling, punctuation, URLs, casing, or response selection.

### GET /api/me/profile without Customer capability

HTTP 404:

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,"detail":"A customer profile has not been created yet. Use POST /api/me/profile to create one.","extensions":{"errorCode":"ProfileNotCreated"}}
```

### Other /api/me/* operation without Customer capability

HTTP 409:

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.10","title":"Conflict","status":409,"detail":"A customer profile has not been created yet. Use POST /api/me/profile to create one.","extensions":{"errorCode":"ProfileNotCreated"}}
```

### POST /api/me/profile

The exemption remains: an authenticated positive-int user without Customer capability can call this
route. The request/response body and success status stay unchanged; internally it grants Customer
on the same user through `IUserCapabilityService`.

If Customer already exists, retain the existing `ProfileAlreadyExists` HTTP 409 behavior.

### Malformed subject

Any `/api/me/*` request whose principal passed authentication but whose preferred subject is
missing, malformed, zero, or negative returns HTTP 401 with an empty body before an action executes.

## Explicitly absent HTTP contracts

Phase 2 creates no endpoint for:

- applicant approval/rejection;
- Admin or RestaurantOwner registration;
- capability revocation/reactivation/restoration;
- full delivery-agent business operations; or
- arbitrary role assignment.

Approval/rejection is exercised through the service interface and real-SQL tests until a later
authorized admin transport exists.

# First Admin bootstrap

This is a controlled, manual production bootstrap. There is intentionally no Admin registration endpoint or bootstrap command.

1. Create the user through the existing controlled Identity/user provisioning process so `AspNetUsers` contains an active, non-deleted account.
2. Set that user's `UserType` bitmask to include `Admin` (`4`; preserve any existing capability bits).
3. Add a matching `Admin` row in `AspNetUserRoles`, using the role row named `Admin` in `AspNetRoles`.
4. Refresh the user's `SecurityStamp` after the database change, then require a new sign-in (or refresh-token exchange) before Admin access is attempted.

The `Admin` role must already exist; normal Identity startup seeds it. A token must be issued to the `talabat-admin-spa` client with the `admin.api` scope. The Admin API validates the token role and independently verifies that the persisted user is active, non-deleted, and has the `Admin` capability.

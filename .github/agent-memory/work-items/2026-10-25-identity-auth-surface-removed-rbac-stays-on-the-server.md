---
kind: work-item
date: 2026-10-25
area: auth
title: "Identity auth surface removed; RBAC stays on the server"
---

### Session-69 (2026-10-25) — Identity auth surface removed; RBAC stays on the server

- **The `/api/auth/*` group is gone.** `AuthEndpoints`, `AuthService`, `AuthDtos`,
  `AuthExchangeCodeService`, `ExternalAuthEndpoints`, `AccountLinkService`, `PendingLinkEndpoints`,
  `PasswordResetEndpoints`, `EmailConfirmationEndpoints`, `SessionEndpoints`,
  `PendingLinkCleanupService`, the `PendingLinkToken`/`RefreshToken` entities, and their repositories
  were deleted. `GET /api/account/profile` is the only account route left — `/api/internal/email/*`
  is the token-guarded email relay Better Auth calls.
- **Identity stays, as the server-owned RBAC store.** `PlayerAccount : IdentityUser<Guid>`,
  `AspNetUserRoles`, and `RolePermissionResolver` are still the source of roles/permissions;
  `JwtClaimsEnrichment` provisions the row just in time from Better Auth claims. `AuthSetup` now
  registers Identity + JWT + authorization only — `AddOAuthProviders` is deleted, so the server
  registers **no** social providers and reads no `OAuth:*` config.
- **Migration `20261025031857_RemoveDesktopAuthTables`** drops `PendingLinkTokens` and `RefreshTokens`.
  Also removed: the `auth-attempts`/`jwt-renewal` rate-limit policies, the `Auth:CookieDomain`
  production guard, and the `OAUTH_*` / `FOUNDRY_PUBLIC_URL` / `ALLOWED_RETURN_URL_HOSTS` `.env`
  aliases. `Jwt:RefreshTokenExpiryDays` went with the entity.
- **Tests authenticate without the deleted endpoints.** `WebAppFactory.CreateAccountTokenAsync(
  username, params string[] roles)` creates the Identity row, ensures the roles exist and are
  assigned, and mints a Better-Auth-shaped HMAC JWT (`Veldrath.Test` issuer/audience) so the
  integration suite still exercises JIT provisioning + RBAC. Eight test files were rewired; the
  endpoint tests for the deleted routes were deleted. Server suite: **675 tests green**.
- **Wire-contract reminder:** the web only ever sees `GET /api/account/profile` of this area, and it
  reads `{ accountId, username, displayName, email, roles, permissions }`.

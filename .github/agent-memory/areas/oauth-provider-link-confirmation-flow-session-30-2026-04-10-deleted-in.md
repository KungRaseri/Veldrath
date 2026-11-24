---
kind: area
date: 2026-04-10
area: auth
title: "OAuth Provider-Link Confirmation Flow (session-30, 2026-04-10) — DELETED in W12"
---

## OAuth Provider-Link Confirmation Flow (session-30, 2026-04-10) — DELETED in W12

> ⚠️ **This whole flow is gone (2026-10-25).** `PendingLinkToken`, `PendingLinkEndpoints`,
> `AccountLinkService`, `AuthService`, `ExternalAuthEndpoints`, and the `auth-attempts` rate-limit
> policy were deleted with the Identity auth surface; the `PendingLinkTokens` table was dropped by
> the `RemoveDesktopAuthTables` migration. Better Auth owns OAuth and account linking. Kept only as
> history for "why the server stopped auto-linking".

**Security fix**: `AuthService.ExternalLoginOrRegisterAsync` previously auto-linked a new OAuth provider to an existing account if the emails matched (step 2). This allowed a hostile provider to hijack an account. It now triggers an email-confirmation flow instead.

**New files**:
- `Veldrath.Server/Infrastructure/Email/IEmailSender.cs` — `Task SendAsync(to, subject, htmlBody, ct)`
- `Veldrath.Server/Infrastructure/Email/NullEmailSender.cs` — dev no-op; logs full email body + URL via `ILogger`
- `Veldrath.Server/Infrastructure/Email/SmtpEmailSender.cs` — BCL `System.Net.Mail.SmtpClient`; config keys: `Email:SmtpHost`, `Email:SmtpPort` (587), `Email:User`, `Email:Password`, `Email:SenderAddress`, `Email:SenderName`
- `Veldrath.Server/Data/Entities/PendingLinkToken.cs` — EF entity; fields: `Id` (PK), `AccountId` (FK → AspNetUsers cascade), `LoginProvider`, `ProviderKey`, `ProviderDisplayName?`, `TokenHash` (SHA-256 hex, unique index), `Email`, `ReturnUrl?` (max 512), `ExpiresAt`, `CreatedAt`, `IsConfirmed`, nav `Account`
- `Veldrath.Server/Data/Repositories/IPendingLinkRepository.cs` + `EfCorePendingLinkRepository.cs` — `CreateAsync`, `GetByTokenHashAsync` (.Include Account), `ConfirmAsync(Guid id)`, `PurgeExpiredAsync` (uses `ExecuteDeleteAsync`)
- `Veldrath.Server/Features/Auth/ExternalLoginResult.cs` — replaces `(AuthResponse?, string?)` tuple; `ExternalLoginStatus` enum: `Success | Error | PendingLinkConfirmation`
- `Veldrath.Server/Features/Auth/AccountLinkService.cs` — `RequestLinkAsync(existing, info, serverBaseUrl, returnUrl?, ct)` + `ConfirmAndLinkAsync(rawToken, ct)` → `(PlayerAccount?, PendingLinkToken?, string? error)`. Errors: `"link_invalid"`, `"link_expired"`, `"link_already_confirmed"`. TTL from `Auth:PendingLinkExpiryMinutes` (default 60). Token: `RandomNumberGenerator.GetBytes(32)` hex → SHA-256 double-hash.
- `Veldrath.Server/Features/Auth/PendingLinkEndpoints.cs` — `GET /api/auth/link/confirm?token={rawToken}`. On success: calls `authService.CreateSessionAsync`, `exchangeService.CreateCode`, redirects to `{returnUrl}?code=xxx&aid=yyy&linked=1` (or `/login?code=xxx&aid=yyy`).
- EF migration `20260410234947_AddPendingLinkTokens`

**Modified files**:
- `AuthService.cs` — added `AccountLinkService` ctor param; `ExternalLoginOrRegisterAsync` now returns `ExternalLoginResult` (step 2 sends email instead of silent-link); `HashToken` changed to `public static`; added `public CreateSessionAsync` wrapper
- `ExternalAuthEndpoints.cs` — switch on `result.Status` in both `HandleOAuthTicket` and `Callback`; `IsAllowedReturnUrl` changed to `internal static`
- `Program.cs` — `AccountLinkService` (scoped), conditional `IEmailSender` (Null when `Email:SmtpHost` empty), `IPendingLinkRepository → EfCorePendingLinkRepository` (scoped), `app.MapPendingLinkEndpoints()`
- `RealmFoundry/Components/Pages/Profile.razor` — Link button `returnUrl` now uses `Nav.ToAbsoluteUri("profile")` (was hardcoded localhost); `?linked=1` and `?pending_link=1` query param handling
- `RealmFoundry/Components/Pages/AuthCallback.razor` — `?pending_link=1` shows "check your inbox" instead of attempting exchange code

**Key note**: `PurgeExpiredAsync` exists but is not called by any hosted service — expired tokens accumulate harmlessly (rejected at confirm time). Add `IHostedService` cleanup later if needed.

**Tests**: 6 unit (`AccountLinkServiceTests`) + 5 integration (`PendingLinkEndpointTests`) = 11 new tests all passing.

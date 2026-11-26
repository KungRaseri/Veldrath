---
kind: area
date: 2026-10-24
area: web
title: "SvelteKit Account + Character Lifecycle Completion (Session-62, 2026-10-24)"
---

## SvelteKit Account + Character Lifecycle Completion (Session-62, 2026-10-24)

**Transactional email**
- Better Auth now defines `emailAndPassword.sendResetPassword` and
  `emailVerification.sendVerificationEmail` / `sendOnSignUp` in
  `web/client/src/lib/server/auth.ts`. `requireEmailVerification` stays `false` so migrated
  accounts are not locked out before they confirm.
- `web/client/src/lib/server/email.ts` forwards messages to the .NET `IEmailSender` via
  `POST /api/internal/email/password-reset|verification`, guarded by
  `X-Internal-Bridge-Token`. When `INTERNAL_BRIDGE_URL`/`INTERNAL_BRIDGE_TOKEN` are absent
  (local web-only dev), the helper logs the action link instead of sending.
- Links are rebuilt to the SvelteKit pages (`/reset-password?token=...`,
  `/verify-email?token=...`); the Better Auth tokens are self-contained JWTs.
- The repository `.env` has a real `EMAIL_SMTP_HOST`, so run the server with
  `Email__SmtpHost=` to force `NullEmailSender` and capture links from the console/log.
- `changeEmail` is enabled (`user.changeEmail.enabled = true`); verified accounts receive a
  confirmation link at the new address and the change applies when it is clicked.

**Identity bridge**
- `/api/internal/accounts/sync` now updates username, email, `EmailConfirmed`, and
  `DisplayName` on existing Identity rows. Previously JIT provisioning only created rows, so
  profile/email changes never reached the .NET account. The request contract added optional
  `DisplayName`.
- `hooks.server.ts` re-syncs when the `username|email|emailVerified` snapshot changes
  instead of only once per process. The account page also calls
  `/api/foundry-bridge/account` immediately after profile edits, passing the new username and
  display name.
- Note: the 60-second `AccountProvisioningService` access-snapshot cache is independent of
  the bridge writes; the bridge calls `ClearCache()` after each sync.

**Character lifecycle**
- `/characters/new` is a seven-step wizard (name, class, species, background, point-buy
  attributes, equipment, difficulty/review) that calls authenticated SvelteKit routes under
  `/api/characters/creation/*`; those forward to the .NET creation-session API using
  `character-api.ts`. The game token never reaches the browser.
- Roster (`/characters`) shows slot usage from `GET /api/characters/slots`, a
  resume-last-session card (`/api/characters/last-session`), a delete form action
  (`?/delete`), and a proper empty state.
- `CharacterSlotsDto(Used, Max, Remaining)` and `GET /api/characters/slots` were added;
  production/development accounts currently have `MaxCharacterSlots = 5`.
- The engine `SetCreationNameHandler` now accepts letters **and spaces**
  (`Character name may only contain letters and spaces.`), matching the server endpoint and
  both UI clients. The previous letters-only rule made the wizard's name step fail for
  names like `Live Hero`.

**Live verification (2026-10-24)**
- Register → verification email → verify; request reset → reset link → set new password →
  sign in with username.
- Create a normal warrior and a hardcore mage (name with a space), preview attributes,
  finalize, enter the world over SignalR (WebSocket, zone, 8-node map, character header),
  edit display name/username and see Identity update, change email + verify + Identity sync,
  list/revoke sessions, delete characters, confirm empty roster and slot reset.
- Web checks: 56 Vitest, 22 Playwright, typecheck, lint, `npm run build`, `npm run build:desktop`.

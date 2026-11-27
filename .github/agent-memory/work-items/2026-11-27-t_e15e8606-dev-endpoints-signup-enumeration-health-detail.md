---
kind: work-item
date: 2026-11-27
area: auth
card: t_e15e8606
title: "Dev endpoints, sign-up enumeration and /health/detail: confirm, then shim"
---

Security epic PR 6, `security-remediation.md` §7 (verdicts recorded there as §7.1). The card's instruction
was confirm-then-shim: none of the three was to be redesigned. Two needed a shim, one was already closed by
its environment gate. This note records what each verdict rests on.

## F9 — dev endpoints: not exploitable, gate untouched

The card said the `/api/dev` group "requires `Permissions.ManageUsers`". It does not, and that difference is
the finding: the group calls `.RequireAuthorization()` — *any* authenticated account — and every endpoint
then returns `403 "Dev endpoints are not available."` unless `IsDevelopment() || IsEnvironment("Test")`
([`DevEndpoints.cs`](../../../Veldrath.Server/Features/Dev/DevEndpoints.cs)). The environment is the whole
gate.

The new integration test
`DevEndpoints_UnderProduction_AreUnreachableEvenForAnAuthenticatedCaller`
([`DevEndpointTests.cs`](../../../Veldrath.Server.Tests/Features/DevEndpointTests.cs)) boots a second host in
`Production` and asserts anonymous 401 plus 403 on all five routes for a bearer-token caller. The shared
`WebAppFactory` could not answer this: it runs `Test`, which the gate admits. That is why the factory gained a
`protected virtual string EnvironmentName` and a
[`ProductionWebAppFactory`](../../../Veldrath.Server.Tests/Infrastructure/ProductionWebAppFactory.cs)
subclass — xUnit allows a collection fixture only one public constructor, so a constructor parameter was not
an option. The production host needs `Web:BaseUrl`, because production deliberately refuses to start without
a public browser origin.

**Do not add `ManageUsers` to the group.** The journey specs (`a1-*`, `encounters`, `dialogue`) call
`POST /api/dev/experience` and `/teleport` as ordinary players. Residual, recorded rather than fixed: a
production deployment that sets `ASPNETCORE_ENVIRONMENT=Development` exposes teleport and XP-award to any
signed-up account.

## Sign-up enumeration: shim applied through Better Auth's own path

Better Auth 1.7.12 returns `422 USER_ALREADY_EXISTS_USE_ANOTHER_EMAIL` for a taken address. Its
enumeration-safe answer — the same `200 {token: null, user}` a new address gets, password hashed anyway to
equalise timing, `user` synthesised from the caller's input — is selected by
`emailAndPassword.requireEmailVerification || emailAndPassword.autoSignIn === false`. So the flag is
`autoSignIn: false` in [`auth.ts`](../../../web/client/src/lib/server/auth.ts), not a message mapping: a
mapping in the page would have left the API answer exactly as it was for anyone using curl.

The cost, taken deliberately: a sign-up no longer mints a session, because a session issued only to a new
address is the signal enumeration wants. Consequences wired through in the same change:

- the register form navigates to `/login?registered=1` instead of `/game`;
- the login page shows the neutral notice "Account ready. Sign in to continue.";
- the e2e `register()` helper ([`tests/support/stack.ts`](../../../web/client/tests/support/stack.ts)) now
  waits for that URL and signs in, so every journey spec keeps its old post-condition (signed in);
- `pages-auth.test.ts` asserts the new navigation and the notice.

`auth.ts` now exports `buildAuthOptions(database)` so the test runs the options that ship, not a copy; the
`createAuth` singleton still checks the secret before it touches `DATABASE_URL`, because argument evaluation
would otherwise report a missing database instead of a missing secret.

The test (`auth.test.ts`, "answers an address that already has an account exactly like a new one") compares a
new-address sign-up with a duplicate-address one: equal status, no `set-cookie` on either, no "already
exists" text, equal key sets, and equal bodies once per-user GUIDs, timestamps and caller-supplied fields are
masked. Negative control: `autoSignIn: true` makes the duplicate `422` and the test fails.

Two details worth keeping:

- **A probe needs a fresh username.** The username plugin rejects a taken username with `400
  USERNAME_IS_ALREADY_TAKEN` *before* the email is considered, so username enumeration is separate and
  deliberately public (usernames are display identifiers). The test therefore varies only the email.
- **`image` is sent explicitly.** The memory adapter stores only the fields it is given, while Postgres
  returns every column, so an omitted `image` makes the real response omit the key and the synthetic one fill
  it with `null` — an adapter artifact, not a production difference. Sending an image keeps the comparison
  honest.

## `/health/detail`: anonymous, now `admin.users`

`HealthCheckResponseWriter` serialises `error = HealthCheckEntry.Exception.Message` for each failed check, so
the anonymous payload could carry a driver's connection error (host, role, sometimes the DSN) — more than the
"component names" the plan predicted. It now requires `Permissions.ManageUsers`
([`Program.cs`](../../../Veldrath.Server/Program.cs)); `/health` is untouched.

Four tests in [`AdminReportingAndStatusEndpointTests.cs`](../../../Veldrath.Server.Tests/Features/AdminReportingAndStatusEndpointTests.cs)
pin the gate: `/health` anonymous 200 `Healthy`, `/health/detail` anonymous 401, authenticated without the
permission 403, Admin 200 with `"status": "Healthy"` and a `checks` object. The shared test host replaces the
Npgsql "database" check with a healthy stub (`AlwaysHealthyCheck` in `WebAppFactory`), because a SQLite host
could only ever report `Unhealthy` and no health assertion would mean anything.

## Verification run

- `dotnet build Veldrath.Server.slnx` — 0 warnings, 0 errors
- `dotnet test Veldrath.Server.slnx` — 6743 passed, 0 failed (1158 shared, 2748 engine, 697 data, 2140 server;
  was 6738, the five new cases account for the difference)
- `dotnet format Veldrath.Server.slnx --verify-no-changes` — exit 0
- `cd web && npm run typecheck` — 0 errors, 0 warnings; `npm run lint` — prettier + eslint clean
- `npm run test:unit --workspace client -- --run` — 108 files, 1632 tests passed (was 1630; two new)
- `npm run build` — exit 0
- Real e2e stack (`scripts/e2e-stack.sh start --fresh`): `GET /health` → `200 Healthy`,
  `GET /health/detail` → `401`, `GET /api/dev/zones` → `401`
- `tests/layout.e2e.ts` through the real isolated stack — 4/4 passed; three of them run `register()`, so the
  register → `/login?registered=1` → sign-in helper change is exercised end to end

---
kind: work-item
date: 2026-10-22
area: engine
title: "OAuth \"No authentication handler\" fix"
---

### Session-49 (2026-10-22) — OAuth "No authentication handler" fix

- **Root cause**: local `dotnet run` does not read `.env`. Compose maps `OAUTH_*` to `OAuth__*`, but a direct run had no OAuth configuration, so the Discord scheme was never registered while `/api/auth/external/discord` still challenged it → unhandled 500.
- `ExternalAuthEndpoints.Challenge` now checks `IAuthenticationSchemeProvider` and returns a 503 ProblemDetails ("OAuth provider not configured") instead of throwing. Unknown providers still return 400.
- `Program.cs` logs `OAuth providers configured: ...` at startup.
- New `Infrastructure/DotEnvConfiguration.cs` loads the repo-root `.env` in Development only, mapping `JWT_KEY`, `OAUTH_*`, `FOUNDRY_PUBLIC_URL`, `WEB_PUBLIC_URL`, `ALLOWED_RETURN_URL_HOSTS`, `EMAIL_*`, and `SEQ_SERVER_URL` to ASP.NET configuration keys. Existing environment variables are never overridden.
- OAuth lookups in `Program.cs` also fall back to the raw `OAUTH_*` environment names, so a shell-sourced `.env` works too.
- Verified: plain Development `dotnet run` on port 9001 registered Discord, Google, and Microsoft; `/health` returned 200 and `/api/auth/external/discord` returned a 302 to Discord.
- Tests: 2 new `ExternalAuthEndpointTests` (unconfigured → 503, unknown → 400). Full suite **4,761 passing**.
- Operational: restart the long-running server after editing `.env`; the dev client/server are launched via `dotnet run`.

---

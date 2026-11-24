---
kind: work-item
date: 2026-04-27
area: content
title: "Stability, Chat, Moderation, Dev Tools"
---

### Session-38 (2026-04-27 to 2026-04-29) — Stability, Chat, Moderation, Dev Tools
- **Connection stability fixes**: Stale `ServerBaseUrl` restore stopped; hub URL slash trimming; `_connectionGeneration` counter for stale event suppression; `ConnectAsync` guard against `Connecting`/`Reconnecting` states
- **Zone re-entry fix**: `_currentZoneId` now cleared on zone exit (was blocking re-entry)
- **Chat command suggestions**: `ChatCommandInfoDto` contract, `ChatCommandInfoViewModel`, `/`-triggered suggestion UI in game panel
- **Moderation system**: `/ignore`, `/warn`, `/mute`, `/ban` commands with permission checks
- **Moderation configuration**: `ModerationOptions.cs` (`AutoBanWarnThreshold: 3`, `MaxChatMessageLength: 500`)
- **Moderation permission denial tests**: +197 lines for unauthorized access scenarios
- **Dev endpoints**: `DevEndpoints.cs` with zone listing, teleport; `IsDevOnly` zone property + EF migration
- **Version compatibility**: `VersionCompatibilitySettings` with `MinCompatibleClientVersion`
- **Docker cleanup**: Removed erroneous `EXPOSE 8081` from Server Dockerfile
- **Rate limiting**: 5 named policies configured (foundry-writes, admin-actions, auth-attempts, jwt-renewal, hub-commands)
- **Security headers middleware**: CSP, XFO, nosniff, Referrer-Policy, Permissions-Policy on all responses

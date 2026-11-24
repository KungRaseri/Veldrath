---
kind: work-item
date: 2026-04-16
area: auth
title: "Auth Overhaul & Account Management"
---

### Session-37 (2026-04-16 to 2026-04-18) — Auth Overhaul & Account Management
- Created `Veldrath.Auth` shared authentication library (extracted from RealmFoundry/Veldrath.Web)
- Created `Veldrath.Auth.Blazor` with `AuthStateServiceBase`
- Created `Veldrath.Auth.Tests` test project (436+ lines of tests)
- Implemented forgot password flow (client + web + API)
- Implemented account profile management (RealmFoundry `Profile.razor`, +444 lines)
- Added session persistence in login/registration flows
- Added `LinkedAt` timestamp to user logins + EF migration
- Enhanced `AuthStateService`/`AuthStateServiceBase` with permissions and session management
- Added OAuth token refresh silent renewal before redirect
- Added logout with server session revocation
- Implemented JWT renewal endpoint
- Added refresh token rotation middleware
- Enhanced error handling in auth API client
- Standardized server base URL to `localhost:9000`
- Updated solution files to include new auth projects

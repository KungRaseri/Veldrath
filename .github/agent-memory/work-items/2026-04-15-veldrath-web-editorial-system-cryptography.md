---
kind: work-item
date: 2026-04-15
area: engine
title: "Veldrath.Web, Editorial System, Cryptography"
---

### Session-35 (2026-04-15) — Veldrath.Web, Editorial System, Cryptography
- Scaffolded `Veldrath.Web` — Blazor Server SSR application with login, community pages
- Created `Veldrath.Web.Tests` test project
- Added `versions/web.props` version file
- Created Editorial system: `EditorialEndpoints.cs`, `EditorialDbContext.cs`, migration `InitialCreate`
- Editorial integration tests added
- Updated `System.Security.Cryptography.Xml` to patched version
- Removed Strapi CMS client and all Strapi references
- Consolidated ports to 9000 across all services
- Updated solution files (`Realm.Full.slnx`, `Veldrath.slnx`)

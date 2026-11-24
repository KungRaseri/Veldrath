---
kind: work-item
date: 2026-09-11
area: web
title: "Dev-container `obj/`/`bin/` isolation (fixes NETSDK1064 + dotnet-watch watcher crash)"
---

### Session-43 (2026-09-11) — Dev-container `obj/`/`bin/` isolation (fixes NETSDK1064 + dotnet-watch watcher crash)

**Symptom (after the Session-42 NuGet.config fix)**: `dotnet watch` inside the Linux dev container failed with
`error NETSDK1064: Package <name>, version <ver> was not found. It might have been deleted since NuGet restore...`
for `Veldrath.Server`, and earlier crashed with
`System.ArgumentException: An item with the same key has already been added. Key: /src/Veldrath.Server/obj/Veldrath.Server.csproj.nuget.g.targets`
from `Microsoft.DotNet.Watch.PollingDirectoryWatcher`.

**Root cause**: `docker-compose.dev.yml` bind-mounts each host project directory into the container at the same path
(e.g. `./Veldrath.Server:/src/Veldrath.Server`). Bind mounts bypass `.dockerignore`, so the Linux container inherited
the **Windows host's** `obj/` restore output:
- `obj/<project>.csproj.nuget.g.props` → `<NuGetPackageFolders>C:\Users\<user>\.nuget\packages\</NuGetPackageFolders>`
  and `<SourceRoot Include="C:\Users\<user>\.nuget\packages\" />`
- `obj/project.assets.json` → `"packagesPath": "C:\\Users\\<user>\\.nuget\\packages\\"`

Because the mounted `project.assets.json` looked "up to date", the container's restore was a no-op and MSBuild resolved
packages from the non-existent Windows path → `NETSDK1064`. The same host-rewritten `obj/` tree also churned under the
container's polling watcher, causing the duplicate-key `ArgumentException`.

**Fix** (`docker-compose.dev.yml`): every bind-mounted project directory also gets **anonymous volumes** over its `obj/`
and `bin/`, so the container restores into its own Linux build output:
```yaml
- ./Veldrath.Server:/src/Veldrath.Server
- /src/Veldrath.Server/obj
- /src/Veldrath.Server/bin
```
Applied to all mounted projects: `server` (Veldrath.Server, Veldrath.Contracts, Veldrath.Assets, RealmEngine.Core/.Data/.Shared),
`discord` (Veldrath.Discord + Contracts + RealmEngine.*), `foundry` (RealmFoundry, Contracts, Veldrath.Auth, Veldrath.Auth.Blazor),
`web` (Veldrath.Web, Contracts, Auth, Auth.Blazor, RealmEngine.Core/.Data/.Shared). Each .NET service also sets
`NUGET_PACKAGES=/root/.nuget/packages` explicitly, backed by the existing `nuget_cache` named volume.

**Verified** (Docker Desktop 29.4.3, Linux containers): `docker compose -f docker-compose.dev.yml down` then
`up -d --build` → `server` and `discord` log `Build succeeded.` / `Application started.` with **no** `MSB4018`,
**no** `NETSDK1064`, and **no** `PollingDirectoryWatcher` `ArgumentException`. Inside the container,
`/src/Veldrath.Server/obj/Veldrath.Server.csproj.nuget.g.props` now records `/root/.nuget/packages` (Linux), confirming
the host Windows metadata no longer leaks in. Server healthcheck reaches `healthy`.

**Convention**: Do NOT remove the per-project `obj`/`bin` anonymous volumes. Any newly bind-mounted project in
`docker-compose.dev.yml` MUST also get `- /src/<Project>/obj` and `- /src/<Project>/bin` entries.

**Separate pre-existing issue discovered (NOT fixed here — out of scope)**: `web` and `foundry` cannot build because their
compose mounts are incomplete. `Veldrath.Web.csproj` references `Veldrath.Server.csproj` (line 25) but the `web` service
does not mount `./Veldrath.Server` (and `Veldrath.Web/Dockerfile.dev` never `COPY`s its csproj). `RealmFoundry.csproj`
→ `Veldrath.GameClient.Components` → `Veldrath.GameClient.Core` → `RealmEngine.Core`, but the `foundry` service mounts
none of those. Result: `dotnet watch ❌ The project file could not be loaded. Could not find a part of the path
'/src/Veldrath.Server/Veldrath.Server.csproj'` (web) and `'/src/RealmEngine.Core/RealmEngine.Core.csproj'` (foundry).
These services previously never started because they use `depends_on: server (healthy)`, and the server never became
healthy. Fixing them requires adding the missing source bind mounts + Dockerfile `COPY` lines. **Fixed by Session-44** —
the mounts and Dockerfile COPY lines are now complete.

---

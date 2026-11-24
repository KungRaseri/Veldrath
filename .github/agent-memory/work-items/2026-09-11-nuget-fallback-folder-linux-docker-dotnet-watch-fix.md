---
kind: work-item
date: 2026-09-11
area: infra
title: "NuGet Fallback Folder / Linux Docker `dotnet watch` Fix"
---

### Session-42 (2026-09-11) — NuGet Fallback Folder / Linux Docker `dotnet watch` Fix
- **Symptom**: `dotnet watch` build of `Veldrath.Server` inside the Linux dev container failed with
  `error MSB4018: NuGet.Packaging.Core.PackagingException: Unable to find fallback package folder 'C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages'`
  raised by `ResolvePackageAssets` at `Microsoft.PackageDependencyResolution.targets(266,5)` (`/usr/share/dotnet/sdk/10.0.401`).
- **Root cause**: The repo had **no** `NuGet.config`. On Windows dev machines, the machine-wide
  `C:\Program Files (x86)\NuGet\Config\Microsoft.VisualStudio.FallbackLocation.config` injects the VS offline
  fallback folder. NuGet records inherited fallback folders into the generated restore output
  (`obj/project.assets.json` → `fallbackFolders`; `obj/<project>.csproj.nuget.g.props` → `NuGetPackageFolders`;
  `obj/<project>.csproj.nuget.dgspec.json`). `docker-compose.dev.yml` **bind-mounts the host project dirs**
  (lines 78–91) — and bind mounts bypass `.dockerignore` (which *does* already exclude `**/obj/` / `**/bin/`) —
  so the Linux container consumed the Windows-generated `obj/` and tried to resolve the non-existent Windows folder.
- **Fix**: Added repo-root [`NuGet.config`](../../../NuGet.config) containing `<fallbackPackageFolders><clear /></fallbackPackageFolders>`.
  This stops the Windows-only VS fallback folder from being inherited/recorded on any OS, so the `obj/`
  artifacts that leak into the container are portable. **Do not remove the `<clear />`.**
- **Verified**: `dotnet restore Veldrath.Server/Veldrath.Server.csproj` regenerates `obj/*.nuget.g.props` and
  `obj/project.assets.json` with no `Microsoft Visual Studio\Shared\NuGetPackages` entry (dgspec `configFilePaths`
  now lists `G:\code\Veldrath\NuGet.Config` first); `dotnet build Veldrath.Server.slnx` succeeds with 0 errors.
- **Convention**: Any future repo NuGet configuration must not reintroduce a machine-specific `fallbackPackageFolders`
  entry.
- **Superseded (Session-43)**: The follow-on assumption that host `obj/` merely needs to be *portable* is no longer
  required — `docker-compose.dev.yml` now gives each container its own `obj/`/`bin/` via anonymous volumes (see Session-43).

---

---
kind: work-item
date: 2026-11-02
area: engine
title: "Forgejo CI migration: mirrored actions, registry, releases"
---

### Session-88 (2026-11-02) - Forgejo CI migration: mirrored actions, registry, releases

The repository now lives on Forgejo (`https://git.kungraseri.cloud/Veldrath/Veldrath`, instance
15.0.11). The first migration attempt left CI red on every push: 49 job tasks existed and every single
one was `failure` or `skipped`, each dying within a second of the push.

Root cause: the runner resolves actions from `DEFAULT_ACTIONS_URL` (default
`https://data.forgejo.org`), and that mirror does not host `actions/setup-dotnet`,
`actions/upload-pages-artifact`, `actions/deploy-pages`, `codecov/codecov-action`,
`marocchino/sticky-pull-request-comment`, `EnricoMi/publish-unit-test-result-action`,
`softprops/action-gh-release` or `SonarSource/sonarqube-scan-action`. An unresolvable action kills the
job before step 1, so there is no log worth reading. It is not a YAML problem.

Verified mirrored: `actions/checkout` (v7), `actions/cache`, `actions/setup-node` (v7.1.0),
`actions/upload-artifact` (v7.0.2), `actions/download-artifact` (v8.0.2), `docker/setup-buildx-action`,
`docker/login-action`, `docker/build-push-action`, `forgejo/upload-artifact`, `forgejo/download-artifact`.
Check any candidate with:
`curl -o /dev/null -w '%{http_code}' https://data.forgejo.org/api/v1/repos/<owner>/<action>`

Decisions (user-approved): Forgejo-native, no SaaS. Codecov dropped, SonarCloud dropped, docs published
as an artifact instead of Pages, images to the Forgejo registry.

The workflow set is now five: `ci-engine`, `ci-server`, `ci-discord`, `ci-web`, `docs`.
`release.yml`, `sonarcloud.yml` and `codecov.yml` are deleted. Releases are created inside each package
job, because fetching an artifact cross-run is a GitHub-API dependency.

New scripts (bash; the runner image is not guaranteed to ship PowerShell):
- `scripts/generate-version.sh` - port of `generate-version.ps1`, byte-identical output across all four
  `versions/*.props` files. Also fixes a latent bug: the .ps1 mis-reads `Directory.Build.props` because
  `VersionMajor`/`VersionMinor` carry `Condition` attributes, producing `VersionMajor .VersionMinor .<n>`.
- `scripts/lib/forgejo-api.sh` - shared curl/awk helpers.
- `scripts/forgejo-release.sh` - creates the release and uploads assets via the API, idempotently.
- `scripts/forgejo-pr-comment.sh` - sticky PR comment located by hidden marker (replaces marocchino).

Two bugs worth remembering, both found by testing against a local fake API:
- `set -u` plus a global set inside a function called as `X="$(fn)"`: the command substitution runs in a
  subshell, so the global never reaches the caller. Pass an output file path instead.
- JSON extraction with a greedy `sed 's/.*"id":...'` picks the LAST id. In a Gitea/Forgejo release object
  that is the nested author/repo id, not the release id. Use awk `index()` for first-match.

Registry: images are `git.kungraseri.cloud/veldrath/<image>` (owner lowercased). `docker-compose.coolify.yml`
now pulls from there, so Coolify needs registry credentials for that host. No buildx cache: `type=gha` is
GitHub-only and registry cache manifests are not reliably supported.

Docs hosting: Forgejo has no Pages server. `docs.yml` uploads `_site/` as the `docs-site` artifact.

Reference: `docs/ci-forgejo.md` (allow-list, runner setup, registry, releases, troubleshooting).
Open: the wiki push uses port 22222 (machine-local config, not in `.gitmodules`); `plans/github-issue-refresh.md`
still carries historical github.com issue URLs.

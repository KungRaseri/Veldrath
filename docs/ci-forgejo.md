# CI and releases on Forgejo

This repository is hosted at `https://git.kungraseri.cloud/Veldrath/Veldrath` and its
pipeline runs on **Forgejo Actions**. Nothing in CI depends on GitHub any more: no
Codecov, no SonarCloud, no GitHub Pages, no GHCR.

That last point is the one to understand before editing a workflow, because the
failure mode is confusing: a job that references an unavailable action **dies in
under a second**, before its first step, and the run page shows a bare failure with
no log worth reading.

## 1. Which actions are allowed

The runner resolves actions from the instance's `DEFAULT_ACTIONS_URL`, which defaults
to `https://data.forgejo.org`. That host mirrors only a curated set of projects.
Anything else must be downloaded from GitHub, which is not configured here — so the
job aborts at resolution time.

Verified as available:

| Action | Notes |
|---|---|
| `actions/checkout` | v7 present |
| `actions/cache` | present |
| `actions/setup-node` | v7.1.0 |
| `actions/upload-artifact` | v7.0.2 |
| `actions/download-artifact` | v8.0.2 |
| `docker/setup-buildx-action` | present |
| `docker/login-action` | present |
| `docker/build-push-action` | present |
| `forgejo/upload-artifact`, `forgejo/download-artifact` | present |

Verified as **missing** — do not add them:

`actions/setup-dotnet`, `actions/upload-pages-artifact`, `actions/deploy-pages`,
`actions/configure-pages`, `actions/setup-java`, `codecov/codecov-action`,
`marocchino/sticky-pull-request-comment`, `EnricoMi/publish-unit-test-result-action`,
`softprops/action-gh-release`, `SonarSource/sonarqube-scan-action`,
`forgejo/forgejo-release`.

Check before adding anything:

```bash
curl -s -o /dev/null -w '%{http_code}\n' \
  https://data.forgejo.org/api/v1/repos/<owner>/<action>
```

`200` means it resolves. `404` means the job will abort.

### What replaced the missing ones

| Removed | Replacement |
|---|---|
| `actions/setup-dotnet` | `dotnet-install.sh` shell step (inline in each .NET job) |
| `codecov/codecov-action` | `reportgenerator` output uploaded with `actions/upload-artifact` |
| `marocchino/sticky-pull-request-comment` | [`scripts/forgejo-pr-comment.sh`](../scripts/forgejo-pr-comment.sh) |
| `EnricoMi/publish-unit-test-result-action` | `TestResults/*.trx` uploaded as an artifact |
| `softprops/action-gh-release` | [`scripts/forgejo-release.sh`](../scripts/forgejo-release.sh) |
| `actions/upload-pages-artifact` + `actions/deploy-pages` | `_site/` uploaded as the `docs-site` artifact |
| `gh workflow run` dispatch | not needed: releases are created inside the package job |
| `ghcr.io` | the Forgejo container registry (section 4) |

## 2. Server-side settings that matter

### `DEFAULT_ACTIONS_URL`

In the instance's `app.ini`:

```ini
[actions]
; Default. Every action resolves from the Forgejo mirror.
DEFAULT_ACTIONS_URL = https://data.forgejo.org

; Alternative: resolve from GitHub. This makes actions/setup-dotnet and friends
; load, but anything that calls the GitHub API (codecov, softprops, EnricoMi)
; then fails at runtime instead, because there is no GitHub behind it.
; DEFAULT_ACTIONS_URL = https://github.com
```

The workflows here need neither setting changed: they only use mirrored actions and
plain `curl`.

### Runner labels

`runs-on: ubuntu-latest` has to exist. In the runner's `config.yaml`:

```yaml
runner:
  labels:
    - "ubuntu-latest:docker://data.forgejo.org/forgejo/runner:12"
    # or your own pre-baked image, which is the closest thing to a "runner plugin":
    # - "ubuntu-latest:docker://git.kungraseri.cloud/veldrath/ci-runner:latest"
```

Register a runner with:

```bash
forgejo-runner register --no-interactive \
  --instance https://git.kungraseri.cloud \
  --token <REGISTRATION_TOKEN_FROM_SITE_ADMIN_ACTIONS_RUNNERS> \
  --name veldrath-runner \
  --labels ubuntu-latest
forgejo-runner daemon
```

There is **no plugin system** in `forgejo-runner`. The three ways to extend it are:
point a label at your own image, change `DEFAULT_ACTIONS_URL`, or vendor an action
(mirror it into this Forgejo, or commit it under `.forgejo/actions/` and use
`uses: ./…`). A pre-baked image is the most useful: bake in .NET 10, Node 22, `zip`,
`curl`, `docker` and the Playwright system dependencies, and every job gets faster
and needs no setup step at all.

Example `Dockerfile` for such an image:

```dockerfile
FROM node:22-bookworm
RUN apt-get update && apt-get install -y --no-install-recommends \
      curl ca-certificates git zip unzip jq docker.io && \
    rm -rf /var/lib/apt/lists/*
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/di.sh && \
    bash /tmp/di.sh --version 10.0.402 --install-dir /usr/share/dotnet && \
    ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet
ENV DOTNET_ROOT=/usr/share/dotnet
```

### Secrets

Forgejo mints a per-job token, exposed as `secrets.GITEA_TOKEN`. The workflows use it
for three things: pushing images, creating tags, and creating releases. No manual
secret is required for those. If you switch to `secrets.GITHUB_TOKEN` keep in mind it
is only an alias — prefer the explicit name.

## 3. Releases

Releases are created **inside the package job**, by
[`scripts/forgejo-release.sh`](../scripts/forgejo-release.sh), via:

- `POST /api/v1/repos/{owner}/{repo}/tags` — create the component tag
- `POST /api/v1/repos/{owner}/{repo}/releases` — create the release
- `POST /api/v1/repos/{owner}/{repo}/releases/{id}/assets?name=…` — attach the zip

The script is idempotent: an existing release for the tag is reused, and an asset
with the same name is deleted and re-uploaded. Re-running a package job is therefore
safe.

There is no separate `release.yml`. It used to fetch the artifact cross-run with
`actions/download-artifact`, which depends on the GitHub API; keeping the release in
the job that built the zip removes that dependency entirely.

Tag prefixes: `engine/v*`, `server/v*`, `discord/v*`.

## 4. Container registry

Images are published to the Forgejo registry on this instance:

```
git.kungraseri.cloud/veldrath/veldrath-server
git.kungraseri.cloud/veldrath/veldrath-discord
git.kungraseri.cloud/veldrath/veldrath-web
```

The owner segment is lowercased (`Veldrath` → `veldrath`) because registry paths must
be lowercase. Login inside CI is `docker/login-action` with the repository owner and
`secrets.GITEA_TOKEN`.

`docker-compose.coolify.yml` pulls from these paths, so Coolify needs registry
credentials for `git.kungraseri.cloud` added under its registry integrations, or the
pull will 401.

There is deliberately **no buildx cache**: `type=gha` is GitHub-only, and registry
cache manifests are not reliably supported by the Forgejo registry.

## 5. Documentation hosting

Forgejo has no Pages server, so [`docs.yml`](../.github/workflows/docs.yml) builds the
DocFX site and uploads it as the `docs-site` artifact. It also runs the markdown
relative-link check on pull requests. Serving the site is a separate decision: a
Coolify static site pointed at `_site/`, or any static host.

## 6. Troubleshooting

| Symptom | Cause |
|---|---|
| Job fails in < 1 s, no useful log | An action could not be resolved. Check every `uses:` in section 1. |
| Job stuck `waiting`/`blocked`, never starts | No runner matches the `runs-on` label. |
| `401` on `docker push` | Registry login used the wrong owner case, or the token lacks package write. |
| Coolify pulls an old image | Coolify is still pointed at `ghcr.io/…`; see section 4. |
| Release created but no asset | The zip glob did not match; check the `Upload artifacts` step output. |
| PR comment duplicated | A different `--marker` was used; markers must be stable per component. |

## See also

- [`build-strategy.md`](../.github/instructions/build-strategy.md) — which solution to build
- [`testing.md`](../.github/instructions/testing.md) — test conventions and coverage
- [`deployment.md`](deployment.md) — deployment overview

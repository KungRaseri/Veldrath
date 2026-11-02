# CI and releases on Forgejo

This repository is hosted at `https://git.kungraseri.cloud/Veldrath/Veldrath` and its
pipeline runs on **Forgejo Actions**. Nothing in CI depends on GitHub any more: no
Codecov, no SonarCloud, no GitHub Pages, no GHCR.

Read this before editing a workflow. The failure modes are not obvious, and two of them
make a job die in about a second with no useful output.

## 1. How an action is resolved

Per the [Forgejo Actions reference](https://forgejo.org/docs/latest/user/actions/reference/),
`uses: owner/repo@ref` is resolved by prepending **`DEFAULT_ACTIONS_URL`**, which lives in
`app.ini` under `[actions]` and defaults to `https://data.forgejo.org`. Writing
`uses: actions/checkout@v7` is shorthand for
`uses: https://<DEFAULT_ACTIONS_URL>/actions/checkout@v7`.

**This instance resolves from itself:** `DEFAULT_ACTIONS_URL = https://git.kungraseri.cloud`
(set 2026-11-02, as the Coolify env `FORGEJO__actions__DEFAULT_ACTIONS_URL`). Every action
the pipeline names is a one-shot copy on this instance under the same owner, and job logs
show it:

```
☁️  git fetch 'https://git.kungraseri.cloud/actions/checkout' # ref=v7
```

That has two consequences, and getting either wrong breaks the job:

1. **The action must exist on this instance, under the owner the workflow names.** The
   copies live in the orgs `actions` (checkout, upload-artifact, setup-node,
   github-script), `docker`, `codecov` and `SonarSource` — created with
   `POST /repos/migrate` (`"mirror": false, "service": "git"`), so nothing is fetched from
   GitHub at build time and no tag is kept in sync automatically. A `uses:` naming an
   action that was never imported fails at "prepare workflow" with `repository not found`,
   before the first step. Import it first, or drop the action.
2. **The action must not need the GitHub API.** An action can clone fine and then fail
   at runtime because it tries to call `api.github.com` — there is no GitHub token and no
   GitHub behind this repository. This is why Codecov, `softprops/action-gh-release`,
   `EnricoMi/publish-unit-test-result-action`, `marocchino/sticky-pull-request-comment`
   and `SonarSource/sonarqube-scan-action` are all gone: they were replaced by shell
   scripts that talk to the Forgejo API directly.

### Artifact actions must be v3

`actions/upload-artifact@v4` and later embed `@actions/artifact` v2, which speaks the
GitHub v4 artifact API. The Forgejo runner implements the v3 API, so a v4+ upload fails
at the very end of an otherwise green job:

```
::error::@actions/artifact v2.0.0+, upload-artifact@v4+ and download-artifact@v4+ are not currently supported on GHES.
```

Use **`actions/upload-artifact@v3`**. This is the one version trap in the pipeline, and
it is easy to reintroduce with a "bump the action" sweep.

### What the workflows use today

| Action | Why |
|---|---|
| `actions/checkout@v7` | resolves from this instance |
| `actions/setup-node@v7` | that is all the web and docs jobs need |
| `actions/upload-artifact@v3` | v3 API only — see above |
| `docker/setup-buildx-action@v4`, `docker/login-action@v3`, `docker/build-push-action@v7` | image builds for server, discord and web |

Everything else is a repository script, so it needs no action at all:

| Removed | Replacement |
|---|---|
| `actions/setup-dotnet` | [`scripts/ci-toolchain.sh`](../scripts/ci-toolchain.sh) |
| `codecov/codecov-action` | `reportgenerator` output uploaded as an artifact |
| `marocchino/sticky-pull-request-comment` | [`scripts/forgejo-pr-comment.sh`](../scripts/forgejo-pr-comment.sh) |
| `EnricoMi/publish-unit-test-result-action` | `TestResults/*.trx` uploaded as an artifact |
| `softprops/action-gh-release` | [`scripts/forgejo-release.sh`](../scripts/forgejo-release.sh) |
| `actions/upload-pages-artifact` + `actions/deploy-pages` | `_site/` uploaded as the `docs-site` artifact |
| `gh workflow run` dispatch | not needed: the package job creates its own tag and release |
| `ghcr.io` | the Forgejo container registry (section 4) |

## 2. Server-side settings that matter

### `DEFAULT_ACTIONS_URL`

```ini
[actions]
; This instance's effective value. Actions are copied onto this instance under their
; upstream owners, so CI never reaches github.com. Set through Coolify as the env var
; FORGEJO__actions__DEFAULT_ACTIONS_URL — a hand edit to app.ini is regenerated away.
DEFAULT_ACTIONS_URL = https://git.kungraseri.cloud
```

### History: why this used to resolve from github.com

Before 2026-11-02 `DEFAULT_ACTIONS_URL` was left at `https://github.com`, and the runner
repeatedly failed with

```
could not fetch remote 'origin': fatal: unable to access 'https://github.com/actions/checkout/':
Could not resolve host: github.com (Timeout while contacting DNS servers)
```

which kills the job before its first step. Jobs whose actions are already in the
runner's `/data/.cache/act` cache pass, so the failure looks random and job-specific
rather than infrastructural.

That failure mode is why the actions are now copied onto this instance. The copies carry
the exact tags the workflows pin:

| Action | Tag | Copied onto this instance |
|---|---|---|
| `actions/checkout` | v7 | yes |
| `actions/setup-node` | v7 | yes |
| `actions/upload-artifact` | v3 | yes |
| `docker/setup-buildx-action` | v4 | yes |
| `docker/login-action` | v3 | yes |
| `docker/build-push-action` | v7 | yes |

The workflows needed no edit for that switch: they name `actions/*`, `docker/*` and
`SonarSource/*`, which now resolve under the same paths on this host. Anything new must be
imported (see section 1) — there is no upstream fallback any more.


### Runner labels

`runs-on: ubuntu-latest` has to exist. This instance runs the runner with label
`ubuntu-latest` backed by `git.kungraseri.cloud/kungraseri/forgejo-job:latest`. In the
runner's `config.yaml` a label can point at any image:

```yaml
runner:
  labels:
    - "ubuntu-latest:docker://git.kungraseri.cloud/kungraseri/forgejo-job:latest"
```

There is **no plugin system** in `forgejo-runner`. The three ways to extend it are: point
a label at your own image (the closest thing to a plugin — bake in .NET, Node, `zip`,
`curl` and docker, and the setup steps disappear), change `DEFAULT_ACTIONS_URL`, or
vendor an action. A pre-baked image is the most useful of the three.

### Secrets

Forgejo mints a per-job token, exposed as `secrets.GITEA_TOKEN`. It creates tags and
releases and comments on PRs, but it **cannot push packages** — the registry answers
`401 Unauthorized: reqPackageAccess`. Image pushes therefore use `secrets.REGISTRY_TOKEN`,
a PAT with `write:package`. That is the only manually configured secret; if it is rotated,
update it under Settings → Actions → Secrets.

## 3. Releases

Releases are created **inside the package job** by
[`scripts/forgejo-release.sh`](../scripts/forgejo-release.sh), which calls:

- `POST /api/v1/repos/{owner}/{repo}/tags` — create the component tag
- `POST /api/v1/repos/{owner}/{repo}/releases` — create the release
- `POST /api/v1/repos/{owner}/{repo}/releases/{id}/assets?name=…` — attach the zip

The script is idempotent: an existing release for the tag is reused, and an asset with the
same name is deleted and re-uploaded, so re-running a package job is safe.

There is no separate `release.yml`. It used to fetch the artifact cross-run with
`download-artifact`, which is a GitHub-API dependency; keeping the release in the job that
built the zip removes that entirely.

Tag prefixes: `engine/v*`, `server/v*`, `discord/v*`.

## 4. Container registry

Images are published to the Forgejo registry on this instance:

```
git.kungraseri.cloud/veldrath/veldrath-server
git.kungraseri.cloud/veldrath/veldrath-discord
git.kungraseri.cloud/veldrath/veldrath-web
```

The owner segment is lowercased (`Veldrath` → `veldrath`) because registry paths must be
lowercase. CI logs in with `docker/login-action` using the repository owner and
`secrets.REGISTRY_TOKEN` — not `GITEA_TOKEN`, which the registry refuses (see section 2).

`docker-compose.coolify.yml` pulls from these paths, so Coolify needs registry credentials
for `git.kungraseri.cloud`, or the pull 401s.

The build uses a **registry-backed buildx cache**: `cache-from`/`cache-to`
`type=registry,ref=<image>:buildcache`. `type=gha` is GitHub-only, and the `dir` cache type
cannot leave the job container, but the Forgejo registry does store the cache manifest and
a warm build logs `CACHED` for unchanged layers.

The package job also declares

```yaml
container:
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock
```

A job container has **no** docker socket by default — without that block `buildx` fails
with "Cannot connect to the Docker daemon" before it runs a single layer.

## 5. Documentation hosting

Forgejo has no Pages server. The published site is a **Coolify application** at
<https://docs.veldrath.com>, built from [`docs/Dockerfile`](../docs/Dockerfile): a multi-stage
build runs `docfx metadata` + `docfx build` and copies `_site/` into nginx. Coolify clones this
repository itself (public source, so no registry credential), and the image never travels
through the Forgejo registry.

[`docs.yml`](../.github/workflows/docs.yml) keeps the CI work — the markdown relative-link
check on pull requests, the docfx build, and a tarball uploaded as the `docs-site` artifact
(the tar matters: ~2400 loose files cost 10+ minutes through the artifact API) — and on `main`
it queues the Coolify deployment with `POST /api/v1/deploy?uuid=…`
(`secrets.COOLIFY_DOCS_DEPLOY_URL` + `secrets.COOLIFY_TOKEN`). Without those secrets the step
warns and skips, so forks and PRs stay green.

Traefik path for the host: `docs.veldrath.com` already resolves to the public IP through the
existing wildcard record, hub's Traefik matches `Host(*.veldrath.com)` and proxies to
kung-lab-2, where Coolify's labels route it to the container. Hub issues the certificate with
its production `letsencrypt` resolver (Porkbun DNS-01), so the browser gets a valid certificate
even though lab-2's own resolver is the staging one.

## 6. Troubleshooting

All four of these have actually happened in this repository.

| Symptom | Cause |
|---|---|
| Job fails in < 1 s, or `unable to clone 'https://github.com/…'` | The action does not exist at `DEFAULT_ACTIONS_URL`. Check the `git clone`/`git fetch` line in the job log. |
| Job fails in ~13 s with `Could not resolve host: github.com (Timeout while contacting DNS servers)` | Transient DNS failure fetching an action. Set `DEFAULT_ACTIONS_URL = https://data.forgejo.org` (section 2) so CI never depends on github.com. |
| Job green, but a *sibling* job on the same push failed instantly | Only one job needed a fresh action fetch; the others hit the cache. Same DNS cause. |
| Job green through build/tests, then `GHESNotSupportedError` at the end | An artifact action is v4+. Use `actions/upload-artifact@v3`. |
| Action clones, then fails at runtime with an API error | The action needs the GitHub API (codecov, gh-release, sticky-comment, check-runs). Replace it with a script. |
| Job stuck `waiting`/`blocked`, never starts | No runner matches the `runs-on` label. |
| `failed to bind host port 0.0.0.0:5432: address already in use` | A `services:` block still publishes a host port; drop `ports:` and use the service name. Two jobs on one runner collide on any published port. |
| `401 Unauthorized: reqPackageAccess` on `docker push`/login | The login used `secrets.GITEA_TOKEN`. Use `secrets.REGISTRY_TOKEN` (section 2). |
| `Cannot connect to the Docker daemon` in a package job | Missing `container.volumes` with `/var/run/docker.sock` (section 4). |
| Job looks hung at ~0% CPU, one open TLS connection, artifact step | `upload-artifact` is walking thousands of files one request each. Tar the tree first (section 5). |
| Coolify pulls an old image | Coolify is still pointed at `ghcr.io/…`; see section 4. |
| Release created but no asset | The zip glob did not match; check the `Upload artifacts` step output. |
| PR comment duplicated | A different `--marker` was used; markers must be stable per component. |

### Reading a job log

The per-run jobs API returns 404 on this version, but the log itself is public:

```bash
curl -s "https://git.kungraseri.cloud/Veldrath/Veldrath/actions/runs/<run_number>/jobs/<job_index>/attempt/1/logs"
```

`run_number` comes from `/api/v1/repos/{owner}/{repo}/actions/tasks`. The runner log uses
emoji markers rather than `##[error]`: look for `❌`, `Failure - `, `⚙️ [runner]` and
`🏁 Job failed`.

## See also

- [Forgejo Actions reference](https://forgejo.org/docs/latest/user/actions/reference/)
- [`build-strategy.md`](../.github/instructions/build-strategy.md) — which solution to build
- [`testing.md`](../.github/instructions/testing.md) — test conventions and coverage
- [`deployment.md`](deployment.md) — deployment overview

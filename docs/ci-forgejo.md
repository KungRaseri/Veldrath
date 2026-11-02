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

**This instance resolves from `https://github.com`.** Proof is in every job log:

```
☁️  git fetch 'https://github.com/actions/checkout' # ref=v7
```

That has two consequences, and getting either wrong breaks the job:

1. **The action must exist at the configured host.** `forgejo/upload-artifact` exists
   only on `data.forgejo.org`, so on this instance it fails to clone:
   ```
   unable to clone 'https://github.com/forgejo/upload-artifact': fatal: could not read Username for 'https://github.com'
   ```
   Use `actions/upload-artifact` instead. If you ever switch `DEFAULT_ACTIONS_URL` to
   `https://data.forgejo.org`, the `forgejo/*` names become the correct ones.
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
| `actions/checkout@v7` | resolves from github.com |
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
; This instance's effective value. Actions are cloned from github.com, so plain
; `actions/*` names work and `forgejo/*` names do not.
DEFAULT_ACTIONS_URL = https://github.com

; Stock Forgejo default. Flip to this and the `forgejo/*` action names become valid,
; at the cost of not seeing actions that only exist on github.com.
; DEFAULT_ACTIONS_URL = https://data.forgejo.org
```

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

Forgejo mints a per-job token, exposed as `secrets.GITEA_TOKEN`. The workflows use it for
three things: pushing images, creating tags, and creating releases. No manual secret is
needed for any of those.

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
`secrets.GITEA_TOKEN`.

`docker-compose.coolify.yml` pulls from these paths, so Coolify needs registry credentials
for `git.kungraseri.cloud`, or the pull 401s.

There is deliberately **no buildx cache**: `type=gha` is GitHub-only and registry cache
manifests are not reliably supported by the Forgejo registry.

## 5. Documentation hosting

Forgejo has no Pages server, so [`docs.yml`](../.github/workflows/docs.yml) builds the
DocFX site and uploads it as the `docs-site` artifact. The same workflow runs the markdown
relative-link check on pull requests. Serving the site is a separate decision: a Coolify
static site pointed at `_site/`, or any static host.

## 6. Troubleshooting

All four of these have actually happened in this repository.

| Symptom | Cause |
|---|---|
| Job fails in < 1 s, or `unable to clone 'https://github.com/…'` | The action does not exist at `DEFAULT_ACTIONS_URL`. Check the `git clone`/`git fetch` line in the job log. |
| Job green through build/tests, then `GHESNotSupportedError` at the end | An artifact action is v4+. Use `actions/upload-artifact@v3`. |
| Action clones, then fails at runtime with an API error | The action needs the GitHub API (codecov, gh-release, sticky-comment, check-runs). Replace it with a script. |
| Job stuck `waiting`/`blocked`, never starts | No runner matches the `runs-on` label. |
| `401` on `docker push` | Wrong owner case in the image path, or the token lacks package write. |
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

# CI and releases on Forgejo

This repository is hosted at `https://git.kungraseri.cloud/Veldrath/Veldrath` and its
pipeline runs on **Forgejo Actions**. Nothing in CI depends on GitHub any more: no
Codecov, no SonarCloud, no GitHub Pages, no GHCR, and no call to `api.github.com`.

That is true of *hosting and API*. It is not true of *mechanism*, and the difference has
bitten this pipeline before: action names keep their GitHub namespaces and resolve from
copies on this instance (§1), `$GITHUB_PATH` is not honoured and is worked around in
[`scripts/ci-toolchain.sh`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/scripts/ci-toolchain.sh) and `docs.yml`, `::warning::`
annotations are plain log lines on this runner, and `.github/dependabot.yml` was inert
because this instance runs no Dependabot. What each trigger actually runs is in
[§1](#what-runs-where), and how the publish path was proven before it was trusted is in
[§3](#3-releases).

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
| `actions/setup-dotnet` | [`scripts/ci-toolchain.sh`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/scripts/ci-toolchain.sh) |
| `codecov/codecov-action` | [`codecov/codecov-report`](https://git.kungraseri.cloud/codecov/codecov-report) output uploaded as an artifact |
| `marocchino/sticky-pull-request-comment` | [`scripts/forgejo-pr-comment.sh`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/scripts/forgejo-pr-comment.sh) |
| `EnricoMi/publish-unit-test-result-action` | `TestResults/*.trx` uploaded as an artifact |
| `softprops/action-gh-release` | [`scripts/forgejo-release.sh`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/scripts/forgejo-release.sh) |
| `actions/upload-pages-artifact` + `actions/deploy-pages` | `_site/` uploaded as the `docs-site` artifact |
| `gh workflow run` dispatch | not needed: the package job creates its own tag and release |
| `ghcr.io` | the Forgejo container registry (section 4) |

### Coverage reports

`dotnet-reportgenerator-globaltool` is gone. Its free build reports line and branch
coverage but gates **method coverage**, **full method coverage**, summary metrics and raw
mode behind a PRO license. The collector's Cobertura XML already carries per-method lines
and complexity, so the reporter lives in its own repository,
[`codecov/codecov-report`](https://git.kungraseri.cloud/codecov/codecov-report), pinned to
a release tag and cloned on demand by `scripts/coverage-report.sh` (no NuGet dependencies;
it builds with the SDK every job installs anyway).

```bash
bash scripts/coverage-report.sh \
  "-reports:./TestResults/*/coverage.cobertura.xml" \
  "-targetdir:./CoverageReport" \
  "-reporttypes:Html;Cobertura;MarkdownSummaryGithub;TextSummary"
```

Only one job publishes a .NET report: `ci-coverage.yml`. It runs every test project, so its
merge is the honest per-assembly number; it feeds `CoverageReport/Cobertura.xml` to
`scripts/check-dotnet-coverage.mjs` and posts the single .NET summary comment. The per-component
workflows (`ci-engine`, `ci-server`, `ci-discord`) upload `TestResults/` only — a report built
from their own subset shows the other assemblies at ~0%, because their tests never ran there.

The web job publishes its own report, because nothing else runs the SvelteKit suite: vitest's
Cobertura export (`web/client/coverage/cobertura-coverage.xml`) goes through the same reporter,
so the client gets a comment and artifact with line, branch, function and full-function numbers;
its 90% gate stays vitest's own thresholds.

**That report's Total row is the gate's number; only its per-directory rows carry the other
accounting.** The reporter is pinned to `v1.0.4`, which reads the Cobertura root
`branches-valid`/`branches-covered` counters instead of summing the per-line `condition-coverage`
attributes, so on the web report its branch Total is the pair the gate enforces. The per-directory
rows under it are still built from the per-line conditions, and istanbul writes those only for a
site it can place on a source line: on PR #88 they summed to 4,180 conditions against 4,902 sites
(`89.8%` in the comment beside `90.33%` in the gate) — the discrepancy that release fixed, for the
Total row only.
`scripts/web-coverage-gate.mjs` therefore heads the `web` section with the totals the coverage step
printed, parsed from that step's log so vitest's `ERROR: Coverage for …` threshold lines — global
and per-glob — travel with it, and states both numbers for the run beside the per-directory table,
which it labels as the Cobertura accounting. `coverage.reportOnFailure` is on, so a red gate still
writes the report and posts the comment rather than leaving the previous head's numbers in place.
The decision and the measurements are in
[`plans/web-feature-parity-and-coverage.md`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/development/plans/web-feature-parity-and-coverage.md)
(that plan is not on the published docs site, hence the absolute link).

Both write into **one shared coverage comment** (`scripts/forgejo-pr-comment.sh --section …`): the
.NET job owns the `dotnet` section, the web job the `web` section, so a PR shows a single summary
with both. The script replaces only its own section, deletes the superseded one-comment-per-job
coverage comments, and re-reads the comment after writing so two jobs finishing at once cannot drop
each other's numbers. Both upload `CoverageReport/` with the merged XML, per-method statistics,
per-assembly rollups, complexity-based risk hotspots and an offline HTML report with annotated
source. `-minimumcoverage:` exists on the tool for a future gate, but the floors in
`scripts/coverage-floors.json` remain the contract.

The helper's lookup contract is **one integer id**: a coverage comment mid-list, followed by a
review comment, must still yield a single `746`, not `746` twice. That was a real failure — awk's
`END` rule ran after `find_comment`'s `exit()`, printed the id again, and the PATCH URL became
`…/issues/comments/746<newline>746`, which curl rejects as `(3) bad/illegal URL`. It is pinned by
`scripts/tests/forgejo-pr-comment.test.sh`, which runs the real script against a fake `curl` on
PATH (no token, no network, no pull request) and asserts the method, URL and payload of every
request for the shared path, the legacy `--marker` path, the match-at-end-of-list case, the
no-match create case, the duplicate-comment cleanup and the e2e comment's own marker.

### E2E reports

Playwright gets its own sticky comment (`<!-- ci-e2e -->`), deliberately **not** a section of the
coverage comment: a table of test counts under `## Coverage summary` is the wrong document, and
one broken report must not take the other down with it. The same
`scripts/forgejo-pr-comment.sh` writes both — `--marker` may now be combined with
`--section`/`--title`, and `--heading` replaces the hard-coded coverage heading, so the e2e
comment reads `## E2E summary`.

| Piece | Where | Fact |
|---|---|---|
| Spec sets | `web/client/tests/e2e-manifest.json` | The single list of which spec runs in which run. The list used to be copied by hand into `web/client/package.json` **and** this workflow, and the copy had dropped `tests/encounters.e2e.ts`, so the combat and death journey never ran in CI. |
| Runner | `web/client/tests/run-e2e.mjs` | Expands a run name into the Playwright invocation and points the reporters at per-run files, because one job runs Playwright twice. `--list` and `--dry-run` work without `npm ci`. |
| Reporters | `web/client/playwright.config.ts` | `list`, `html` (never opens), `json` and `junit`, written to the `PW_*` paths the runner sets. Without them there is nothing to summarise — and the `playwright-report/` artifact both web jobs uploaded was empty for exactly that reason. |
| Summary | `scripts/e2e-summary.mjs` | `E2EReport/Summary.md`: totals, a per-spec table slowest-first, each failure with its error headline and trace path, and each skip with its reason. |
| Integrity gate | `scripts/check-e2e-manifest.mjs` | Fails when a manifest spec produced no result, when an unexpected spec ran, or when a skip has no allow-listed reason. It never fails on a red test — pass/fail is the job's own exit code. |

The gate is what replaces "the spec list lives somewhere in the workflow". Flaky tests are counted
and named but never gate a build: `retries: 1` already hides one flake, and a flaky budget would go
red on its first day. Per-run floors live in `web/client/tests/e2e-baseline.json`, where an empty
`runs` object means "not measured yet" — `--suggest` prints the number to commit after a green run.
Both are covered by `scripts/tests/e2e-reporting.test.mjs` (`node --test scripts/tests/e2e-reporting.test.mjs`),
which runs the real scripts against fixtures, including the negative control for a spec that no run
claims.

### What runs where

Every workflow watches the same two integration branches, and none of them publishes from a
pull request:

| Trigger | What runs |
|---|---|
| push to `main` | build + test, **and** the publish/deploy jobs |
| push to `development` | build + test; publish jobs are skipped |
| pull request whose **base** is `main` or `development` | build + test only |
| `workflow_dispatch` | build + test only |
| `workflow_dispatch` with `publish: true` | build + test, **and** the publish/deploy jobs |

Three details of that contract are not what a GitHub habit would predict, and all three were
measured on this instance rather than assumed:

- **`pull_request.branches` filters on the base branch** — the branch the PR merges into,
  not the head branch. A PR from `wt/some-fix` into `development` runs only because
  `development` is listed; listing `wt/some-fix` would match nothing.
- **`pull_request.paths` filters on the pull request's changed files** — the whole diff,
  not the tip commit.
- **A merge commit inside a pull request inflates that diff.** Merging `development` into a PR
  branch to resolve a conflict puts every file the merge brought in into the PR's change set, so
  a one-file memory note starts the whole matrix: PR #18 edited only
  [`.github/agent-memory/veldrath-memory.md`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/.github/agent-memory/veldrath-memory.md) and still
  ran the web build, the game-loop e2e and the image package. Rebase the PR branch while it is
  still open; merge into it only when rebasing would rewrite a branch someone else has already
  pulled.

Each workflow keeps its `push` and `pull_request` path lists **identical**, and lists
`.github/agent-memory/**` by name even though `**/*.md` already matches it — the memory file is the
one Markdown file every session touches, and a silent skip there is invisible until a stale entry
ships.

So the filters are not decorative: before this was fixed every workflow listed only `main`,
which meant the release PR (`development` → `main`) was the only PR ever tested and the
work that actually lands in `development` had no CI at all.

**A workflow whose `paths` do not match produces no run and no status.** That is why the branch
protection rule in section 2 requires the `*` status glob rather than the named job contexts: a
required context that never reports stays `pending`, and a `plans/`-only pull request would block on
checks that will never exist. The complement of that sentence is just as load-bearing — a pull
request that reports **nothing at all** is also `pending`, and that deadlocks the merge. The
always-on merge gate in section 2 exists to make that case impossible.

### Dependency and action updates (there is no bot)

Nothing on this instance reads `.github/dependabot.yml`, so the file is gone and a written
cadence replaces it. **NuGet** ([`Directory.Packages.props`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/Directory.Packages.props))
and **npm** ([`web/`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/)) are reviewed together on the **first Monday of the month**,
as one grouped update. The holds that lived in the deleted config carry over verbatim:

- **MediatR stays below 13.** 13+ is commercially licensed; the bump needs a licensing
  decision first, not a version bump.
- **`@sveltejs/kit`, `@sveltejs/adapter-node`, `@sveltejs/adapter-static`,
  `@sveltejs/vite-plugin-svelte`, `vite` and `typescript` majors are one deliberate
  migration**, never piecemeal — SvelteKit, the adapters and Vite move as a set, and the
  TypeScript major waits on `typescript-eslint`/`svelte-check` support.

**Docker base images** ([`Veldrath.Server/Dockerfile`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/Veldrath.Server/Dockerfile),
[`Veldrath.Discord/Dockerfile`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/Veldrath.Discord/Dockerfile),
[`web/client/Dockerfile`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/Dockerfile)) get the same monthly pass.

**Action tags are never swept.** A tag changes only by importing the new action copy onto
this instance, confirming `upload-artifact` stays `v3` (see above), and then editing the
workflow — in that order, because step two is what catches the trap. The tags are
instance-local copies, not upstream releases, so there is no version a bot could compute
anyway.

If a dependency bot is ever wanted here it is self-hosted Renovate with `renovate.json`,
not a resurrected `dependabot.yml`.

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

### Branch protection: what can block a merge

`development` and `main` each carry one protection rule. It lives in the server, not in a
file the repository can diff, so it is recorded here and applied by
[`scripts/apply-branch-protection.sh`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/scripts/apply-branch-protection.sh):

| Field | `development` | `main` | Why |
|---|---|---|---|
| `rule_name` | `development` | `main` | The release PR is `development` → `main`; a red merge into either branch is the same bug |
| `enable_status_check` | `true` | `true` | This is the gate |
| `status_check_contexts` | `["*"]` | `["*"]` | "Every check that reported must be green" — see below |
| `enable_push` | `false` | `true` | `main` carries the operator's deploy repair path; `development` takes pull requests only |
| `enable_push_whitelist` | `false` | `true` | With `push_whitelist_usernames: ["kungraseri"]` — see below |
| `push_whitelist_usernames` | `[]` | `["kungraseri"]` | The bypass is one named account — see below |
| `push_whitelist_teams` | `[]` | `[]` | Account-only: no team inherits the push path |
| `push_whitelist_deploy_keys` | `false` | `false` | A deploy key is not a second break-glass path |
| `required_approvals` | `0` | `0` | The instance has one account, and an author cannot approve their own PR |
| `apply_to_admins` | `false` | `false` | The administrator exemption was removed on request — see below |
| `block_on_outdated_branch` | `false` | `false` | `zoo-pr` rebases before opening; requiring up-to-date would re-run the matrix on every unrelated merge |
| `require_signed_commits` | `false` | `false` | Commits here are not signed |

#### Why the admin exemption and the one push account

Both rules were created with `apply_to_admins: true` and no push whitelist. They were
changed on 2026-11-03 at 18:56 UTC, deliberately and at the operator's request, while all
four Coolify deployments were failing: `main` pinned the `wiki` submodule at a commit the
wiki repository never had, so every `git clone --recurse-submodules` died before the build
started. The fix had to land on `main` — the branch all four apps build from — and the pull
request carrying it (#31) could not merge while its statuses were pending.

Measured on the way, and the reason the rule has two exceptions rather than one:
`apply_to_admins: false` does **not** lift `enable_push: false`. The pre-receive hook
refuses the push for the administrator as well (`Forgejo: Not allowed to push to protected
branch main`); enabling push for one account is a separate switch.

What each exception costs, stated plainly:

- `apply_to_admins: false` hands *repository* admins the `force_merge` override that
  `CheckPullMergeable` already gives an instance admin. The instance has one account, so
  today this changes nothing for the person operating it; it matters the day a second admin
  exists.
- `main` accepts pushes from exactly one account, `kungraseri`. Every other identity, and
  every push to `development`, is still refused by the pre-receive hook. A direct push to
  `main` skips the status checks, so it is for repairing a broken deployment, never for
  landing a change. The whitelist is account-only: `push_whitelist_teams` is `[]` and
  `push_whitelist_deploy_keys` is `false` on both rules, and
  [`scripts/apply-branch-protection.sh`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/scripts/apply-branch-protection.sh) asserts both
  in its read-back, so neither a team nor a deploy key can quietly become a second bypass.

#### Why one glob instead of the component job names

The obvious list is the build gates that report on a pull request today:

- `CI - Engine Libraries / Build and Test Engine Libraries (pull_request)`
- `CI - Server (Veldrath.Server) / Build and Test Server (pull_request)`
- `CI - Discord (Veldrath.Discord) / Build and Test Discord Bot (pull_request)`
- `CI - Web (SvelteKit) / Build and Test SvelteKit Web (pull_request)`
- `CI - .NET Coverage Gate / Enforce .NET Coverage Floors (pull_request)`
- `Deploy Documentation / Build Docs (pull_request)`

It cannot be that list. Every workflow is filtered by `on.pull_request.paths`
([`ci-engine.yml:28`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/.github/workflows/ci-engine.yml)), and a required pattern that
matches no status is `pending`, not skipped: in `MergeRequiredContextsCommitStatus`
(`services/pull/commit_status.go` at v15.0.11) the loop breaks with
`CommitStatusPending` as soon as one pattern matched nothing, and a pull request holding
a pending required check cannot merge. A workflow excluded by the `paths` filter creates
no run at all (`services/actions/notifier_helper.go`, `detectWorkflows`), so it reports
nothing; the later upstream change that emits a `skipped` status for filtered workflows
(go-gitea/gitea#38237) is **not** in 15.0.11.

Measured, not assumed: PR #14 (head `95ecfecc`, a `plans/*.md` change) carries exactly one
status, `Deploy Documentation / Build Docs (pull_request)`. Five of those six names would
never report on it, so with the list every documentation and `plans/` pull request this
repository opens would deadlock.

The alternative — deleting `paths` from `pull_request` so that every workflow runs on every
pull request — buys the named contexts at a price. On PR #12 the component jobs measured
496 s (engine), 499 s (server), 498 s (discord), 463 s (web) and 104 s (docs), so a one-line
`plans/` edit would pay about eight minutes of .NET, Postgres, Playwright and DocFX.

`*` is the pattern that does not need to know which workflows ran. Forgejo compiles each
context with `glob.Compile` (`gobwas/glob`, no separators, so `*` matches slashes too), then
per pattern takes the worst state of every status it matches. With a single `*` the rule
reads:

> At least one status must exist on the head commit, and every status that exists must be
> `success`.

What follows from that, all of it deliberate:

- `pending` blocks — a queued or running job, and a job cancelled by the `concurrency`
  group, keep the merge waiting.
- A workflow filtered out by `paths` neither helps nor blocks: it is simply not part of the
  set. The `paths` lists stay the single source of truth for *which* components a change
  touches, and no second copy of them has to be maintained here. That holds only while
  something else reports — a change matching **no** list leaves the set empty, and an empty
  set is `pending`, not "nothing required". See the next subsection.
- Renaming a workflow or a job cannot silently disarm the gate. With named contexts it can.
- The `Package *` jobs and `Game loop e2e (real server)` count too. They already run on pull
  requests with publishing gated off; a failure there is a broken build, so blocking on it
  is correct.
- `skipped` passes. On this version Forgejo Actions reports a skipped job as `success`, and
  from 16 as `skipped`; both are green. Reported behaviour, not measured here — no job in
  these workflows is skipped by an `if:`.

#### The always-on check that keeps `*` satisfiable

The rule above has a second half: **at least one status must exist**. Zero statuses is `pending`,
not "nothing required". `MergeRequiredContextsCommitStatus`
([`services/pull/commit_status.go`](https://codeberg.org/forgejo/forgejo/src/tag/v15.0.11/services/pull/commit_status.go)
at v15.0.11) compiles each required context and, when a pattern matches no status at all, sets
`targetStatus == ""`, sets `returnedStatus = Pending` and breaks (lines 52-57). The
`matchedCount == 0` fallback on line 65 only runs while `returnedStatus` is still `Success`, so an
empty status list cannot be rescued: `IsPullCommitStatusPass` returns false and the merge is
refused.

The statuses evaluated are `GetLatestCommitStatus` — the newest row **per context**
([`commit_status.go:128`](https://codeberg.org/forgejo/forgejo/src/tag/v15.0.11/services/pull/commit_status.go)).
`GET /api/v1/repos/{owner}/{repo}/commits/{sha}/statuses` is a different, chronological list: it
returns every state transition for a context, so a finished run still leaves the earlier `pending`
rows beside its `success`. Do not read a historical `pending` row as a red gate; the combined
`GET .../commits/{sha}/status` (`total_count` = one per context) is what the gate computes.

That is a deadlock, not a gap, and it is measured rather than inferred. A throwaway pull request
(#32, branch `probe-x`, head `b59e841d`, `.gitignore` the only changed file, 2026-11-03):

```bash
GET  /api/v1/repos/Veldrath/Veldrath/commits/b59e841d/statuses  -> []
GET  /api/v1/repos/Veldrath/Veldrath/commits/b59e841d/status    -> {"state":"","total_count":0}
GET  /api/v1/repos/Veldrath/Veldrath/pulls/32                   -> "mergeable": true
POST /api/v1/repos/Veldrath/Veldrath/pulls/32/merge {"force_merge": false}
    -> 405 not allowed to merge [reason: Not all required status checks successful]
```

PR #27 is the same shape and is the motivating case: `4e8a2311` changed `.gitignore` only, so the
head commit carried zero statuses, and it became mergeable only when `97d20257` touched
`.github/agent-memory/**` and therefore matched `docs.yml`. `mergeable: true` on the pull request is
git-level only and never predicts this.

**Decision (2026-11-03): `["*"]` stays, and one workflow reports on every pull request.**
[`merge-gate.yml`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/.github/workflows/merge-gate.yml) triggers on `pull_request` for `main` and
`development` and declares **no `paths` filter**. With no `paths` key there is no paths condition to
satisfy: `matchPullRequestEvent` in `modules/actions/workflows.go` (v15.0.11) iterates only the acts
the workflow declares and returns `activityTypeMatched && matchTimes == len(evt.Acts())`, so
`branches` narrows the run and nothing else does.

The alternatives were measured against the tree instead of argued:

| Alternative | Why not |
|---|---|
| Add root-config paths to an existing list | The set is not three files: 37 tracked paths in eight classes match no list today (table below), and every new root file reopens the deadlock silently. It is also conceptually false — no component's output depends on `.gitignore` |
| Keep `["*"]` and document `force_merge` for this class | It works, and that is the objection: the token every worker here authenticates with belongs to the instance administrator (`GET /user` → `kungraseri`, `is_admin: true`), and `CheckPullMergeable` clears the protection error for an admin. Documenting it would make "any worker certifies its own merge" the routine path — the same reason posting a status by hand is forbidden. It stays break-glass |
| Switch to named contexts | Already rejected above, and this does not change it: a path-filtered workflow still never reports |

The job lints the workflow definitions; it builds nothing. Its rules are the two ways this
repository has actually broken CI, plus the invariant that keeps this remedy alive:

- **L1** every workflow file declares a top-level `name:` — that string is the status context.
- **L2** every `uses:` names one of the six actions mirrored on this instance (section 1). That
  failure mode is documented above as "fails in under a second, logs nothing"; the rule turns it into
  a message that names the file, the action and the fix (import it with `POST /repos/migrate`, then
  add it to the allow-list and to the table in section 1).
- **L3** every `pull_request` workflow triggers on both `main` and `development`. Session-92 fixed
  exactly this drift: every workflow said `branches: [ main ]` while pull requests targeted
  `development`, so a change landing there ran no CI at all.
- **L4** at least one `pull_request` workflow declares neither `paths` nor `paths-ignore`. That is
  the invariant behind the remedy: give `merge-gate.yml` a `paths` filter and the zero-status
  deadlock returns, so the lint fails the pull request that does it.

Cost: one job — one `actions/checkout@v7` and one `bash scripts/ci-merge-gate.sh --lint`, with no
SDK, npm, docker or service container. **Measured on 2026-11-03: 5 s** for the whole job (`duration`
on `GET /actions/runs/1357`, run 1357, head `cf0c3cc6`, PR #38; the `pending` row was written at
19:31:44Z and the `success` row at 19:31:50Z). For scale, the six build gates that reported on
`0efb7c7c` recorded between 93 s and 485 s each (`duration` on `GET /actions/runs/{id}`; the cheapest
was `Build Docs` at 93 s, run 1325), and this job only checks out a depth-1 tree and greps it. It
runs on every pull request into `development` or `main`, which is the point.

**Unblocking without bypassing.** If the status is missing because the branch head predates this
workflow, the certain fix is a new commit: any push to the branch re-triggers `pull_request`, and the
gate now reports on the new head. The alternative is to dispatch the check itself:

```
Actions -> CI - Merge gate -> Run workflow -> branch: <the pull request's head branch>
```

That should report on the branch tip — the pull-request head commit — under the context
`CI - Merge gate / Merge gate reports on every pull request (workflow_dispatch)`; it has not been
exercised here, and the `(workflow_dispatch)` suffix is one more reason `["*"]` is the right rule,
because a named context would not match it. There is no workflow-dispatch API on this version
(`GET .../actions/workflows` is 404 and `POST .../actions/workflows/{id}/dispatches` is 404, both
measured), so it is a web-UI step. `force_merge` stays break-glass for the instance admin, and
posting a status by hand stays forbidden: a status any agent can mint certifies nothing.

#### What it deliberately does not protect

- **Reviews.** `required_approvals` is `0` because the instance has exactly one account
  (`GET /admin/users` returns only `kungraseri`) and Forgejo does not let an author approve
  their own pull request, so any non-zero value would block every merge instead of adding a
  reviewer. Raise it the day a second account exists.
- **The administrator.** `CheckPullMergeable` (`services/pull/check.go`) clears the
  protection error when `force_merge` is set and the doer is an instance admin, and because
  `apply_to_admins` is now `false` a *repository* admin gets the same override. That is the
  break-glass path — a stuck `pending` status must never lock the only operator out — and
  it is why this rule is a review contract, not a lock. Every worker here authenticates as that
  account, so the override is available to an agent, not only to the human at the keyboard: the
  gate is enforced by discipline, and a worker that runs `force_merge` is self-certifying. Use it
  to unblock, never to land a red change.
- **Direct pushes to `main`.** Exactly one account, `kungraseri`, may push there; `development`
  refuses every push, and that refusal was exercised: `git push` to it answers
  `Forgejo: Not allowed to push to protected branch development` from the pre-receive hook. The
  hook ignores `apply_to_admins`, so the administrator is not exempt from it either. The bypass is
  account-only — `push_whitelist_teams: []` and `push_whitelist_deploy_keys: false` are pinned and
  asserted by the script — so no team and no deploy key inherits it. A push to `main` past the gate
  is a deploy repair, not a merge path. It was not exercised by an agent: raw `git push` is denied
  for these agents, and the sanctioned publish path (`zoo-pr`) refuses to publish an integration
  branch by name, so treat push protection as configured and read back, not as demonstrated by a
  rejected push.

#### Files that no component workflow builds

Measured against `development` at `d0050f96` (2026-11-20, `git ls-files`: 2069 paths, 104
`pull_request` path patterns, the merge gate and its script included): **37 paths match no
`on.pull_request.paths` pattern.** Before the merge gate they produced zero statuses and deadlocked
the gate; now they produce exactly one — the merge gate — and merge with **no build and no test**.
Regenerate the list with `bash scripts/ci-merge-gate.sh --uncovered`; its last two lines carry the
live counts for whatever tree you run it on, so quote them instead of hand-counting — the figures
above are a snapshot of that commit, not a standing number. The content guard,
`.github/workflows/ci-content.yml` (merged in #108), contributes nine of those patterns. The same 37
paths were uncovered at `3d3320ac` (2007 tracked files, 102 patterns): every file added between the
two measurements is covered by some `paths` entry, and the only pattern change since is the pair
`scripts/check-content-names.mjs` and `scripts/content-name-collisions.allow.json` (#134), which name
a script `docs.yml` runs.

The path count halved when `Veldrath.Assets` and its 1,837 art files were deleted (2026-11-04); the
uncovered *set* barely moved, which is the point — the art never matched a workflow's `paths` either.
It shrank by two on 2026-11-20 for the opposite reason: `scripts/check-e2e-manifest.mjs` and
`scripts/e2e-summary.mjs` are executed by `ci-web.yml`, so that workflow now names them in both of its
path lists. The rule is the one `scripts/web-coverage-gate.mjs` and `scripts/tests/**` already follow:
**a script the job executes is in the job's paths.**

| Class | Count | Paths |
|---|---|---|
| Root dotfiles and editor config | 10 | `.dockerignore`, `.editorconfig`, `.env.example`, `.gitignore`, `.gitmodules`, `.hermes/skills`, `.roomodes`, `.vscode/launch.json`, `.vscode/tasks.json`, `LICENSE` |
| Root build metadata | 1 | `dotnet-tools.json` |
| Solution files | 4 | `RealmEngine.slnx`, `Veldrath.Discord.slnx`, `Veldrath.Libraries.slnx`, `Veldrath.Server.slnx` |
| Observability and deploy config | 11 | `config/grafana/dashboards/{10915,19194,23178,23179}.json`, `config/grafana/provisioning/dashboards/dashboards.yml`, `config/grafana/provisioning/datasources/prometheus.yml`, `config/prometheus/prometheus.yml`, `docker-compose.coolify.yml`, `docker-compose.dev.yml`, `docker-compose.e2e.yml`, `docker-compose.prod.yml` |
| Content budget data | 1 | `game-context/budget.yaml` (YAML, so `docs.yml`'s `**/*.md` does not match it) |
| Scripts no workflow names | 8 | `scripts/ci-merge-gate.sh`, `scripts/create-release-tag.ps1`, `scripts/e2e-stack.sh`, `scripts/format-json-files.py`, `scripts/generate-coverage-report.ps1`, `scripts/generate-version.ps1`, `scripts/onebit_labeled.png`, `scripts/roguelike_base_labeled.png` |
| Submodule pin | 1 | `wiki` |
| The always-on check itself | 1 | `.github/workflows/merge-gate.yml` (it declares no `paths` at all, so no list can name it) |

Three of those classes deserve an explicit warning, because "merges without a build" is not the same
as "cannot break the build":

- **`dotnet-tools.json`** pins the local tool set every `dotnet` invocation resolves, and no `paths`
  list names it, so a tool-version change merges on the merge gate alone — name it in the `paths` of
  whichever job installs those tools when a tool pin starts to matter. `NuGet.config` has left
  this class: `ci-content.yml` names it in both of its `paths` lists (`push` and `pull_request`), so
  a package-source change now gets a content-guard status. Read that status for what it is — not a
  build. The guard runs `dotnet run --project Veldrath.ContentPack`, so the sources that project
  resolves are restored through the changed file; a source only another project needs stays unseen
  until an unrelated change runs that project's job, which then fails on a pull request that did not
  touch it.
- **The four component `.slnx` files** decide which projects each solution builds. `Realm.Full.slnx`
  is covered by `ci-coverage.yml`; `RealmEngine.slnx` and the three `Veldrath.*.slnx` are not, so
  adding a project to `Veldrath.Server.slnx` alone reports the merge gate and no build.
- **`game-context/budget.yaml`** is the approved merchant band, and `MerchantBandTests` reads it from
  the repository root (`Veldrath.Server.Tests/Features/MerchantBandTests.cs`), comparing it against
  the code's copy in `MerchantBands` — so an edit here can fail `Veldrath.Server.Tests` without
  touching a C# file. By the same rule as an executed script, a file a test reads belongs in that
  job's `paths`, and no list names `game-context/**`: `docs.yml`'s `**/*.md` covers only the sibling
  `GAME.md`, which is why the directory looks covered. Until it is named, a band edit merges with
  `ci-server.yml` unrun and reds the next pull request that touches `Veldrath.Server/**`.

Seven of the eight entries in the scripts class are developer-only, which is why no list names them
and none should:

- **`scripts/e2e-stack.sh`** starts the isolated local stack the full-flow Playwright specs need. CI
  never calls it — the `e2e-game` job starts its own Postgres and `Veldrath.Server` — and the script's
  own header calls it "the local equivalent" of that job. It stays unnamed on purpose, not by oversight.
- **`scripts/create-release-tag.ps1`**, **`scripts/generate-version.ps1`** and
  **`scripts/generate-coverage-report.ps1`** are the PowerShell twins kept for local and Windows use;
  the workflows run `scripts/forgejo-release.sh`, `scripts/generate-version.sh` and
  `scripts/coverage-report.sh` instead.
- **`scripts/format-json-files.py`** is a formatting helper, and the two `*_labeled.png` files are
  annotation assets for issue screenshots. No job executes either.

The eighth, **`scripts/ci-merge-gate.sh`**, is the exception in that class: it runs on **every** pull
request, but `merge-gate.yml` declares no `paths` at all, so no list can name it — and that is what
makes the rest of this table safe to have.

#### When to switch back to named contexts

Not while the component workflows are `paths`-filtered. A named context has to exist on the head
commit, and a workflow excluded by `paths` creates no run, so its name is unsatisfiable and the pull
request deadlocks — the same mechanism as above, with a clearer error message. The always-on merge
gate does not change that: it satisfies `*` on its own and would be the only satisfiable name in a
list.

Replace `["*"]` with names when Forgejo starts reporting a `skipped`/`success` status for workflows
excluded by `paths` (the upstream change that is **not** in 15.0.11). The gain is real: a named
context also fails when its workflow was renamed, disabled or deleted, which `*` cannot detect.
Until then, the always-on check plus `*` is the only arrangement that both blocks a red check and
never deadlocks a green one.

#### Applying and verifying

```bash
set -a; . ~/.config/zoo-agent/forgejo.env; set +a
export FORGEJO_SERVER_URL="$FORGEJO_URL"
scripts/apply-branch-protection.sh            # create or edit, then read back and verify
scripts/apply-branch-protection.sh --check    # verify only; exits 1 on drift
scripts/tests/apply-branch-protection.test.sh # offline regression tests: no token, no network
```

The script is idempotent and fails when the stored rule is not the requested one, so
`--check` is the drift test. The offline tests stub the API response and assert the failures
the live server must not have — an extra account, a push team, a deploy-key bypass, pushes on
`development` — and run in the [docs workflow](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/.github/workflows/docs.yml).

#### Proof, both directions

Exercised on 2026-11-03 immediately after the rules were created:

- **A red check blocks.** PR #16 added one documentation file with a deliberately broken
  relative link. `Deploy Documentation / Build Docs (pull_request)` reported `failure`, and
  `POST /repos/Veldrath/Veldrath/pulls/16/merge` with `force_merge: false` answered
  `405 not allowed to merge [reason: Not all required status checks successful]` while the
  pull request still reported `mergeable: true` at the git level. The test branch and the
  pull request were deleted afterwards.
- **A green pull request still merges.** This change merged through the same rule with
  every reported check `success`; the merge call answered `200`.

## 3. Releases

Releases are created **inside the package job** by
[`scripts/forgejo-release.sh`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/scripts/forgejo-release.sh), which calls:

- `POST /api/v1/repos/{owner}/{repo}/tags` — create the component tag
- `POST /api/v1/repos/{owner}/{repo}/releases` — create the release
- `POST /api/v1/repos/{owner}/{repo}/releases/{id}/assets?name=…` — attach the zip

The script is idempotent: an existing release for the tag is reused, and an asset with the
same name is deleted and re-uploaded, so re-running a package job is safe.

There is no separate `release.yml`. It used to fetch the artifact cross-run with
`download-artifact`, which is a GitHub-API dependency; keeping the release in the job that
built the zip removes that entirely.

Tag prefixes: `engine/v*`, `server/v*`, `discord/v*`.

### The publish gate

Publishing — tag, release, image push, docs deploy — is a job in four workflows and a single
step in [`docs.yml`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/.github/workflows/docs.yml), and every one of them carries the same
expression:

```yaml
on:
  workflow_dispatch:
    inputs:
      publish:
        description: 'Publish for real (tag, release, image push, docs deploy). Off = build only.'
        type: boolean
        default: false

jobs:
  package:
    if: (github.event_name == 'workflow_dispatch' && inputs.publish) || (github.event_name == 'push' && github.ref == 'refs/heads/main')
```

Two parts of that are load-bearing and easy to break on the next edit:

- **`type: boolean`.** Without it the input arrives as the string `"false"`, which is
  **truthy** in the expression language — a dispatch that asked for a dry run would publish.
- **`github.event_name`.** On its own, `github.ref == 'refs/heads/main'` is also satisfied by
  a `workflow_dispatch` whose `ref` is `main`, so before this gate a manual dispatch was a
  release. The event check is what makes dispatch build-only by default.

A push to `main` behaves exactly as it did before: the second half of the expression is the
old guard, unchanged.

### How the publish path was proven

The path had never run here: `/releases` was empty and every publish-job task ever recorded
was `skipped`. It was therefore rehearsed in a throwaway private repository,
`Veldrath/_ci-publish-rehearsal`, on 2026-11-02, rather than discovered by merging into
`main`. The rehearsal took this tree and changed exactly one thing — `IMAGE_NAME` to
`veldrath-server-rehearsal`, `veldrath-discord-rehearsal` and `veldrath-web-rehearsal` — so
the run could not write `:latest` on the image paths production and Coolify pull from. Only
`REGISTRY_TOKEN` was copied into it; `COOLIFY_*` was deliberately left unset.

One push to the rehearsal repository's `main` produced, from the API rather than from a job
badge: three releases — `engine/v0.1.1629`, `server/v0.1.1629` and `discord/v0.1.1629` — each
carrying its `.zip` asset, and two images in the registry,
`veldrath-server-rehearsal` and `veldrath-discord-rehearsal`, each tagged `:latest` and
`:0.1.1629`. `docs.yml` logged `COOLIFY_DOCS_DEPLOY_URL / COOLIFY_TOKEN are not set —
skipping` and stayed green, which is the skip path referenced in section 5. `ci-web` published
nothing because its `build-test` job is red on the coverage gate (section 1), so the web image
remains the one component the first real release will not produce.

The repository and its images were deleted afterwards; nothing production-visible moved.

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
<https://docs.veldrath.com>, built from [`docs/Dockerfile`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/docs/Dockerfile): a multi-stage
build runs `docfx metadata` + `docfx build` and copies `_site/` into nginx, configured by
[`docs/nginx.conf`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/docs/nginx.conf)
(gzip plus cache headers — the search index alone is ~4 MB uncompressed). Coolify clones this
repository itself (public source, so no registry credential), and the image never travels
through the Forgejo registry.

The site is themed from the client rather than from a separate palette: `docfx.json` copies
`web/client/src/routes/theme.css` and the client's `static/fonts/*.ttf` into `_site/public/client/`,
and [`templates/veldrath/`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/templates/veldrath)
maps those tokens onto the modern template. That is why `docs.yml` also triggers on `theme.css`,
`templates/**` and the font/favicon directories — a client theme change must rebuild the docs.

[`docs.yml`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/.github/workflows/docs.yml) keeps the CI work — the markdown relative-link
check on pull requests, the docfx build, and a tarball uploaded as the `docs-site` artifact
(the tar matters: ~1700 loose files cost 10+ minutes through the artifact API) — and on `main`
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
| Publish job skipped on a dispatch | The dispatch left `publish` at `false`, which is the default: a dispatch is build-only by design. Re-dispatch with `publish: true` if you meant to publish. |
| PR comment duplicated | A different `--marker` was used; markers must be stable per component. |
| No `E2E summary` comment on a pull request | The summary was never rendered — `E2EReport/Summary.md` is missing, and the comment step exits 0 by design rather than failing a red job over a comment. Check the `Render the E2E summary` step. |
| The e2e gate fails with `produced no result` for a spec that passes locally | The spec is not part of the run that job executes. Add it (once) to `web/client/tests/e2e-manifest.json`; a new spec file that no run claims fails the static check in the same step. |
| `playwright-report/` is empty although the job ran Playwright | A bare `npx playwright test` bypassed `tests/run-e2e.mjs`, so no `PW_*` output path was set, or the reporters block was removed from `playwright.config.ts`. |
| A skip makes the e2e gate fail | Intentional skips need an entry in the manifest's `allowedSkips` with a reason; the failure message quotes the skip text so it can be matched exactly. |
| Last step of the coverage job fails with `curl: (3) bad/illegal URL`, and the log shows the id twice — `Updating the shared coverage comment 746` then `746 (section: dotnet)` | `find_comment` printed the id twice when another comment followed the match, so `COMMENT_ID` held `746\n746` and the PATCH URL carried a newline. Fixed by the `found` guard in `scripts/forgejo-pr-comment.sh`; pinned by `scripts/tests/forgejo-pr-comment.test.sh`. |

### Reading a job log

The per-run jobs API returns 404 on this version, but the log itself is public:

```bash
curl -s "https://git.kungraseri.cloud/Veldrath/Veldrath/actions/runs/<run_number>/jobs/<job_index>/attempt/1/logs"
```

`run_number` comes from `/api/v1/repos/{owner}/{repo}/actions/tasks`. The runner log uses
emoji markers rather than `##[error]`: look for `❌`, `Failure - `, `⚙️ [runner]` and
`🏁 Job failed`.

## 7. Issues and the kanban board

Open issues on this repository are mirrored onto the Hermes kanban board `dev` by the
`forgejo-sync` Hermes plugin. Each open issue becomes a card with the local clone as its
workspace and a `wt/issue-<n>-<slug>` branch, created under the idempotency key
`forgejo:Veldrath/Veldrath#<n>`, so repeated sync passes never duplicate a card. When the card
reaches `done`, its result is posted back as a comment on the issue and the issue is closed;
a `blocked` card comments its blocker and leaves the issue open.

## See also

- [Forgejo Actions reference](https://forgejo.org/docs/latest/user/actions/reference/)
- [`build-strategy.md`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/.github/instructions/build-strategy.md) — which solution to build
- [`testing.md`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/.github/instructions/testing.md) — test conventions and coverage
- [`deployment.md`](deployment.md) — deployment overview

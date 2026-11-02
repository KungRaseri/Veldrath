# CI contract: trigger scope, dependency updates, and proving the publish path

**Status:** decided 2026-11-02 (card `t_0b6b9e61`). Implementation is fanned out to
`dev-coder` cards; this file is deleted when the last one lands and the durable parts
are folded into [`docs/ci-forgejo.md`](../docs/ci-forgejo.md).

**Scope:** `.github/workflows/*.yml`, `.github/dependabot.yml`, `docs/ci-forgejo.md`.

Every number below is a measurement against the live instance
(Forgejo `15.0.11+gitea-1.22.0`, `https://git.kungraseri.cloud`) taken on 2026-11-02,
not an estimate. The two measurements that were not obtainable from this repository
were taken with a throwaway probe repository (`Veldrath/_ci-probe`, private, created and
deleted inside the session; six runs total). Method is in §4 so it can be repeated.

---

## 0. Two premises in the audit are wrong — correct these before implementing

The audit card `t_4c4d6976` concluded that `pull_request: branches: [main]` "is not
gating what it appears to gate" because 58 `pull_request` runs exist for PRs that look
like merges into `development`. Both halves of that reading are mistaken, and the
difference changes what the fix is.

**(a) Those 58 runs are all one PR, and its base is `main`.**
`GET /repos/Veldrath/Veldrath/actions/tasks` (704 tasks, full pagination): **59**
`pull_request` tasks, every one with `head_branch: "#3"`. PR #3 is `base: main,
head: development`. The `display_title` that reads *"Merge pull request '…' … into
development"* is the **head commit message** of the `development` branch, not the PR's
target. Nothing in the run list says the PR targets development.

**(b) Forgejo does apply both `pull_request.branches` and `pull_request.paths`** —
`branches` against the **base** branch, `paths` against the PR's changed files.
Measured on the probe repo:

| PR | base | files touched | `branches: [main]` + `paths: ['only/this/**']` | result |
|---|---|---|---|---|
| #1 | `main` | `only/this/a.txt` | both match | **ran** (run 1) |
| #2 | `main` | `README.md` | path miss | **no run** |
| #3 | `development` | `only/this/c.txt` | base miss | **no run** |

The same conclusion falls out of this repository's own history: PRs #1, #2 and #5 all
have `base: development` and produced **zero** workflow tasks, while PR #3 (`base: main`)
produced all 59.

So the filter is not broken. It is **pointed at the wrong branch**: it gates only the
release PR and never the PRs that carry actual work.

**Consequence measured from the same data:** because PR #3's head *is* `development`,
every push to `development` re-runs the entire five-workflow matrix against #3 — 19
tasks for the 21:39 push and 19 more for the 19:52 push. That is the churn the audit
attributed to a broken filter.

**(c) A third correction, load-bearing for §3:** the audit claims Forgejo has no
workflow-dispatch API. It does on this instance —
`POST /api/v1/repos/{owner}/{repo}/actions/workflows/{file}/dispatches` returns **204**
and creates a run. More importantly, a dispatch on `main` **does** satisfy
`if: github.ref == 'refs/heads/main'`: on the probe, a job carrying only that guard
**executed** (`status: success`, not `skipped`) when dispatched with `ref: main`. A
dispatch of today's workflows on `main` therefore publishes for real.

---

## 1. PR trigger scope (B9) — point PR CI at `development` as well as `main`

**Today** (`file:line` as of `af2cd883`):

| workflow | PR base filter | push branches |
|---|---|---|
| `.github/workflows/ci-engine.yml:28` | `[ main ]` | `[ main, development ]` (`:14`) |
| `.github/workflows/ci-server.yml:32` | `[ main ]` | `[ main, development ]` (`:16`) |
| `.github/workflows/ci-discord.yml:32` | `[ main ]` | `[ main, development ]` (`:16`) |
| `.github/workflows/ci-web.yml:23` | `[main]` | `[main, development]` (`:16`) |
| `.github/workflows/docs.yml:28` | `[ main ]` | **`[ main ]`** (`:19`) |

**Decision.** The PR contract is: *a pull request is tested when its base is the branch
it will merge into, and both branches this repo merges into get tested.*

- **D1a** — `pull_request.branches` becomes `[ main, development ]` in all five workflows.
  Keep `main` (the release PR must stay covered); add `development` (it is the branch
  every worker PR merges into, and no such PR has ever had CI).
- **D1b** — `docs.yml` push branches become `[ main, development ]`, matching the other
  four. Today a docs edit is never built on `development`; it is only reached through
  PR #3, which is the wrong place to discover a broken docs build.
- **D1c** — `push.branches` on the four component workflows is unchanged.
- **D1d** — **No `paths` edits.** They work, on both event types. Do not add
  PR-specific path workarounds, and do not "fix" the churn on PR #3 by removing `main`.
- **D1e** — No branch-protection change here: this instance has **none**
  (`GET /repos/Veldrath/Veldrath/branch_protections` → `[]`, `main` is
  `protected: false`), so PR CI is advisory in both the old and new state. See §5.

**Accepted cost / consequence.** With `development` added, a PR whose cumulative diff
touches `web/**` will show the `ci-web` build-test job **red**, because the coverage gate
is genuinely failing right now: `npm run coverage` reports 71.35% lines against a 90%
global threshold (`ERROR: Coverage for lines (71.35%) does not meet global threshold
(90%)`, run 1029 job 0, plus per-glob failures down to `src/**/*.svelte` branches at
45.24%). `AGENTS.md` states that gate is expected to stay red until the web coverage pass
lands and must not be lowered. Do not lower it, and do not withhold D1a to hide it — a
red-by-design check that is visible on PRs is the honest state; hiding it is how the
coverage pass never happens.

---

## 2. `.github/dependabot.yml` (B11) — delete it; replace with a written cadence

**Today:** five update blocks (`nuget :7`, `npm :24`, `github-actions :53`, `docker :66`,
`docker :77`). Forgejo has no Dependabot service, so **nothing reads this file**. Worse,
the `github-actions` block (`:53-64`) would bump `actions/upload-artifact` past `v3`, and
v3 is a hard requirement here — v4+ speaks the GitHub v4 artifact API and dies with
`GHESNotSupportedError` at the end of an otherwise green job
([`docs/ci-forgejo.md:43-54`](../docs/ci-forgejo.md)). The tags these blocks would "bump"
(`actions/checkout@v7`, `docker/login-action@v3`, …) are **instance-local tag names on
one-shot imported copies**, not upstream releases (upstream is at checkout v7.0.1,
login-action v4.7.0), so even a working bot could not compute a correct update.

**Decision.**

- **D2a** — Delete `.github/dependabot.yml`. A file that looks like automation but is
  inert is worse than no file: it is why the `upload-artifact@v3` pin was never noticed
  as a trap.
- **D2b** — Trimmed-file and "keep it for the day we might run Renovate" are rejected.
  Keep-reading is what fails: a bot needs a config it actually reads, and Renovate would
  take `renovate.json`, not a resurrected `dependabot.yml`.
- **D2c** — Record the cadence in `docs/ci-forgejo.md` (one new subsection under §1):
  - **NuGet** (`Directory.Packages.props`) and **npm** (`web/`): reviewed together on the
    first Monday of the month, as one grouped update. The existing holds carry over
    verbatim: MediatR stays below 13 (commercial licence decision), and
    `@sveltejs/kit` / `adapter-node` / `vite-plugin-svelte` / `vite` / `typescript` majors
    are taken as one deliberate migration, never piecemeal.
  - **Docker base images** (`Veldrath.Server/Dockerfile`, `Veldrath.Discord/Dockerfile`,
    `web/client/Dockerfile`): same monthly pass.
  - **Action tags are never swept.** A tag changes only by (1) importing the new copy onto
    the instance, (2) confirming `upload-artifact` stays `v3`, (3) editing the workflow.
    One sentence in the docs, in bold.
- **D2d** — Housekeeping, not a code change: three stale `dependabot/*` branches remain on
  `origin` from before the migration
  (`dependabot/github_actions/actions-d5ebea38e7`,
  `dependabot/npm_and_yarn/web/web-minor-patch-9af1602362`,
  `dependabot/nuget/dotnet-minor-patch-f641d3a244`). They serve nothing on Forgejo and
  advertise automation that does not exist; delete them with push rights.

---

## 3. Proving the publish path (B10)

**Today:** every publish/deploy gate is `if: github.ref == 'refs/heads/main'`
(`ci-engine.yml:152`, `ci-server.yml:132`, `ci-discord.yml:131`, `ci-web.yml:279`,
`docs.yml:118`). Across the 704 tasks ever recorded, **every task belonging to a publish
job is `skipped`**: 12 × `Package Engine Artifacts`, 12 × `Package Server Artifacts`,
13 × `Package Discord Bot Artifacts`, 18 × `Package Web Image` — 55 skipped, none in any
other state. The docs deploy is a *step* inside the `Build Docs` job (`docs.yml:118`), so
it never appears as a task; and the Coolify version of `docs.yml` has only ever run on
`pull_request` (against PR #3) — the last `push` run of that workflow on `main` was
2026-10-29, before the Coolify step existed. `GET /repos/Veldrath/Veldrath/releases` → `[]`. Newest tags are
`server/v0.1.1589`, `engine/v0.1.1587`, `discord/v0.1.1587`, all dated 2026-10-25 and all
created on GitHub before the migration. The tag → release → asset path
(`scripts/forgejo-release.sh`) has produced exactly zero releases on this instance.

**Why "a dispatch-triggered dry run that does not publish" is not achievable today, and
what replaces it.** `workflow_dispatch` is declared in all five workflows, and the
dispatch API works, but the `main`-only `if:` is satisfied by a dispatch whose `ref` is
`main` (probe runs 2 and 3 both executed a `github.ref == 'refs/heads/main'`-gated job).
So a dispatch today is not a dry run — it is a release. Making it a dry run means gating
*every mutating step* inside five jobs (tag POST, release script, `docker build-push`
`push:` input, Coolify POST) behind an input, and a dry run still cannot prove the thing
that actually breaks: whether `GITEA_TOKEN` may create a release and whether
`REGISTRY_TOKEN` may push. **Rejected in favour of a real rehearsal in a throwaway repo,
which exercises the same scripts, the same runner, the same registry and the same API
with production-shaped secrets.**

### Decision

- **D3a — Make dispatch safe (guard, permanent).** Add to each of the five workflows:
  ```yaml
  workflow_dispatch:
    inputs:
      publish:
        description: 'Publish for real (tag, release, image push, docs deploy). Off = build only.'
        type: boolean
        default: false
  ```
  and replace each `main`-only gate with:
  ```yaml
  if: (github.event_name == 'workflow_dispatch' && inputs.publish) || (github.event_name == 'push' && github.ref == 'refs/heads/main')
  ```
  (`docs.yml:118` is a **step**, not a job — same expression, `if:` indented at step level.)

  This expression was validated on the probe before being written down: push to `main` →
  job ran (run 4); dispatch `publish=false` → **skipped** (run 5); dispatch
  `publish=true` → ran (run 6). `type: boolean` is load-bearing: with the type omitted, a
  string `"false"` is truthy in the expression language and the gate would publish. Keep
  `github.event_name` — it is already used at `ci-engine.yml:138`, `ci-server.yml:118`,
  `ci-discord.yml:117`.

  Effect: dispatch stops being an accidental release; a deliberate manual re-release is
  still possible with `publish=true`; push-to-`main` behaviour is byte-for-byte unchanged.
- **D3b — Rehearse for real in a throwaway repo, before any push to `main`.** Recipe in
  §4.2. This is the proof that satisfies B10; it must record the run URLs, the created
  release URL, the asset name and the pushed image digests.
- **D3c — The first push to `main` is the production gate, not a rehearsal.** It happens
  when PR #3 (`development` → `main`) merges. Acceptance: `GET /releases` non-empty, the
  release carries the zip asset, `veldrath-server` / `veldrath-discord` carry both
  `:latest` and `:<version>` tags, and the Coolify docs deploy returns < 300.
  Tag, release and image are additive, so a partial failure is recoverable: delete the
  release and tag (`DELETE /repos/{owner}/{repo}/releases/{id}`, then
  `DELETE /repos/{owner}/{repo}/tags/{tag}`), delete the image tag from the package
  settings, and re-run. The only genuinely bad outcome is a `:latest` image that boots
  wrong; recovery is rebuilding `:latest` from a known-good `main`.
- **D3d — Precondition, do not paper over.** `ci-web`'s `package` job is
  `needs: build-test` (`ci-web.yml:278`) and `build-test` is red on the coverage gate, so
  the first `main` push publishes **three** of four images and no web image. That is
  acceptable if it is stated in the release notes; it is not acceptable to lower the
  coverage thresholds, and it is not acceptable to merge #3 believing a web image shipped.

---

## 4. Method (repeatable)

### 4.1 How the trigger facts were measured

A private repo `Veldrath/_ci-probe` was created through the API, given one workflow:

```yaml
on:
  pull_request:
    branches: [main]
    paths: ['only/this/**']
```

then three branches and three PRs — one matching both filters, one matching the base but
not the path, one matching the path but not the base. Only the first ran. Two further
dispatches (`ref: main`) of a workflow whose job carried only
`if: github.ref == 'refs/heads/main'` both executed, which is what proves a dispatch on
`main` is a release today. The repo was deleted afterwards (verified `404`) and the
instance's `releases` list was re-checked as untouched.

### 4.2 Rehearsal recipe (D3b)

Run after D3a lands. Everything happens in a private repo; nothing touches
`Veldrath/Veldrath`.

1. Create `Veldrath/_ci-publish-rehearsal` (private, empty) via
   `POST /api/v1/orgs/Veldrath/repos`.
2. Copy the tree at the `development` head, **with one edit**: in `ci-server.yml`,
   `ci-discord.yml` and `ci-web.yml`, change `IMAGE_NAME` to
   `veldrath-server-rehearsal` / `veldrath-discord-rehearsal` / `veldrath-web-rehearsal`.
   Without that, the rehearsal pushes `:latest` to the same registry path production
   pulls from — the one unrecoverable mistake available here.
3. Copy only `REGISTRY_TOKEN` (the PAT with `write:package`):
   `PUT /api/v1/repos/Veldrath/_ci-publish-rehearsal/actions/secrets/REGISTRY_TOKEN`
   with `{"data":"<value>"}`. `GITEA_TOKEN` is minted per job and needs no copying. Leave
   `COOLIFY_*` unset — `docs.yml` warns and skips when they are absent, which is the
   already-tested behaviour.
4. Push to the rehearsal repo's `main`. Do **not** import tags: the version is
   major.minor from `versions/*.props` plus commit-count patch
   (`scripts/generate-version.sh`), so the rehearsal cuts `engine/v0.1.1` and similar —
   exactly what is wanted, and it cannot collide with a real tag.
5. Verify, from the API and not from a green job badge:
   - `GET /repos/Veldrath/_ci-publish-rehearsal/releases` returns one release per
     component with a `.zip` asset attached;
   - the tag exists (`GET /repos/.../tags`);
   - `veldrath-server-rehearsal` has `:latest` plus the version tag in the registry;
   - the docs job's Coolify step logged its skip (secrets absent) rather than failing.
6. Delete the rehearsal repo and its registry packages.

If any step needs a credential the worker does not have, that is a `kanban_block`, not a
workaround: the point of the rehearsal is to find out **before** the merge.

---

## 5. Open questions for a human

1. **Do you want required status checks on `development`?** There is no branch
   protection anywhere (`branch_protections` → `[]`). D1a makes PR CI *informative*; only
   protection makes it *gating*. Enabling it is a policy call with a real cost: while
   `ci-web` is red by design, a required-check rule either blocks web PRs or has to exempt
   that workflow.
2. **Is `ci-web`'s coverage gate allowed to keep every web-touching PR red until the
   coverage pass lands?** The alternative (split coverage into its own advisory job, keep
   build/typecheck/test blocking) is a behaviour change nobody has authorised yet.
3. **Timing of the first `main` push.** It is the production publish gate (§3c) and it
   cannot produce a web image until the coverage pass is green. Merge #3 before or after
   that pass?
4. **Should the rehearsal go further than rehearsal?** Optionally, after the first real
   release, cut the *next* one from `main` with `publish=true` (manual dispatch) to prove
   the manual path a second time against the real repo.

---

## 6. Implementation breakdown

| # | Change | Depends on | Verification gate |
|---|---|---|---|
| C1 | §1: D1a on five workflows, D1b on `docs.yml` | — | The implementer's own PR (base `development`) shows all five workflows starting — the one place where the change verifies itself. |
| C2 | §2: D2a delete `dependabot.yml`, D2c docs cadence text | — | `grep -rn dependabot .github docs` returns only the new docs prose; `git status` shows no other change. |
| C3 | §3: D3a `publish` input + guard in five workflows | C1 (same five files) | YAML parses; the guard expression is character-identical to the one validated in §3a; `git diff` touches only `if:`/`workflow_dispatch` lines. |
| C4 | §3b rehearsal, then fold §1-§3 into `docs/ci-forgejo.md` and delete this file | C3 | The six-item check in §4.2 passes with recorded URLs; docs state the final contract. |

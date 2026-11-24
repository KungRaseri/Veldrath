---
kind: work-item
date: 2026-11-03
area: web
title: "A root-config pull request reports no status, and `[\"*\"]` then blocks it"
---

### Session-95 (2026-11-03) — A root-config pull request reports no status, and `["*"]` then blocks it

Session-92 recorded the `["*"]` rule as "every check that reported must be green". The other half of
that sentence — *at least one status must exist* — is a real deadlock for a change that no workflow
watches, and it is not hypothetical: PR #27 (`wt/t_7db54bc9`, `4e8a2311`) touches only `.gitignore`,
which appears in none of the six `on.pull_request.paths` lists, so every workflow is filtered
out, no run is created, and the head commit carries zero statuses.

Read from the source rather than inferred (`services/pull/commit_status.go`, tag `v15.0.11`, the
version this instance reports): in `MergeRequiredContextsCommitStatus`, when a required pattern
matches no status the loop sets `returnedStatus = Pending` on `targetStatus == ""` and `break`s; the
`matchedCount == 0` fallback on line 65 only runs while `returnedStatus` is still `Success`, so with
an empty status list the state stays `Pending`, `IsPullCommitStatusPass` returns false, and the merge
is refused. `mergeable: true` on the pull request does not predict this — it is git-level only.

What that means in practice:

- The pre-protection pull requests (#1, #2, #5, #7, #9) did merge with zero statuses, so "it merged
  before" is not evidence; the rule has only existed since Session-92.
- A pull request reports if it touches **anything** in a list already: `web/**`, `**/*.cs`,
  `**/*.md`, `.github/agent-memory/**`, `docs/**`, `plans/**`, the engine/server/discord trees, or
  the coverage lists. Anything outside all of them — `.gitignore`, `.editorconfig`, `LICENSE` —
  reports nothing and waits.
- Forgejo 15.0.11 has **no workflow-dispatch API** (`GET .../actions/workflows` is 404), so a run
  cannot be started from a worker; only the web UI or a matching path triggers one. The operator's
  `force_merge` is break-glass, not a routine unblock.
- Posting a commit status by hand to satisfy the gate is not an acceptable unblock: it would let any
  agent self-certify a merge.

**Decided (2026-11-03, card `t_a6502114`): `["*"]` stays and one workflow reports on every pull
request.** `.github/workflows/merge-gate.yml` triggers on `pull_request` for `main` and `development`
with **no `paths` filter**, and runs `scripts/ci-merge-gate.sh --lint` — L1 every workflow has a
`name:`, L2 every `uses:` is one of the six actions mirrored on this instance, L3 every
`pull_request` workflow triggers on both `main` and `development`, L4 at least one `pull_request`
workflow has no `paths`/`paths-ignore` (the invariant that keeps the zero-status case impossible).
The decision, the rejected alternatives and the inventory are in `docs/ci-forgejo.md` under
"Branch protection: what can block a merge".

Why not the `paths`-list alternative: measured on the tree, **39 tracked paths in seven classes** match
no list — root dotfiles and editor config (10, `LICENSE` included), `NuGet.config` + `dotnet-tools.json`
+ `toc.yml` (3), the four root `.slnx` (4), `config/grafana/**` + `config/prometheus/**` +
`docker-compose.{coolify,dev,prod}.yml` (10), scripts no workflow names (10, after `docs.yml` claimed
`scripts/apply-branch-protection.sh` and `scripts/lib/forgejo-api.sh`), the `wiki` submodule pin (1),
and the merge gate's own workflow (1). Adding three names would leave 36 deadlocked and reopen on the
next root file. Those paths now merge on the merge gate alone — no build, no test — which is written
down there so nobody mistakes "green" for "built".

The deadlock itself was reproduced end to end before deciding, rather than taken from the Session-95
inference: probe PR #32 (branch `probe-x`, head `b59e841d`, only `.gitignore` changed) answered
`{"state":"","total_count":0}` and `[]` for statuses, `mergeable: true` for the pull request, and
`405 not allowed to merge [reason: Not all required status checks successful]` for
`POST /pulls/32/merge {"force_merge": false}`. The PR was closed and the branch deleted. The
workflow and script landed in card `t_48d8d3c1` (PR #38).

One correction to the force_merge reasoning recorded above: the token every worker runs with is the
instance administrator (`GET /user` → `kungraseri`, `is_admin: true`), so `force_merge` **is**
available to an agent — `CheckPullMergeable` clears the protection error for an admin. The gate is
therefore enforced by discipline, not capability: never use `force_merge` or a hand-posted status to
satisfy `["*"]`, because either one lets a worker certify its own merge. The API's
`GET .../commits/{sha}/statuses` also lists every state transition of a context, while the gate reads
the latest row per context (`GetLatestCommitStatus`, `services/pull/commit_status.go:128`) — a stale
`pending` row beside a `success` is not a red gate.

**Landed (2026-11-03, card `t_48d8d3c1`, PR #38).** `.github/workflows/merge-gate.yml` and
`scripts/ci-merge-gate.sh` exist, so the context
`CI - Merge gate / Merge gate reports on every pull request (pull_request)` now reports on every pull
request into `development` or `main` and `["*"]` always has a status to evaluate. Measured on PR #38
head `cf0c3cc6`: run 1357 `success` in **5 s** (`duration` on `GET /actions/runs/1357`; `pending` row
19:31:44Z → `success` row 19:31:50Z), combined commit status `{"state":"success","total_count":2}`
next to `Deploy Documentation / Build Docs (pull_request)`. The job is one depth-1 checkout plus the
lint; it must stay offline and dependency-free. `--uncovered` is the follow-up inventory — it
reproduces the runner's `PatternToRegex` translation (`**/` → `(.+/)?`, so `**/*.md` matches a root
`AGENTS.md`; the naive `**` → `.*` would over-report), and it now reports **39 paths** because the
gate and its script are themselves tracked and named by no `paths` list (`merge-gate.yml` declares no
paths at all). A change confined to one of those 39 still merges on the merge-gate status alone, with
no component build.

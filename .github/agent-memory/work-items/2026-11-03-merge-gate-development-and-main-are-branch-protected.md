---
kind: work-item
date: 2026-11-03
area: web
title: "Merge gate: `development` and `main` are branch-protected"
---

### Session-92 (2026-11-03) — Merge gate: `development` and `main` are branch-protected

Both integration branches carry one protection rule, applied by
[`scripts/apply-branch-protection.sh`](../../../scripts/apply-branch-protection.sh) (idempotent, and it
reads the rule back; `--check` is the drift test) and reasoned in
[`docs/ci-forgejo.md`](../../../docs/ci-forgejo.md#branch-protection-what-can-block-a-merge) (PR #17):

- `enable_status_check: true` with `status_check_contexts: ["*"]`
- `enable_push: false`, no whitelist — a protected branch changes only through a pull request
- `required_approvals: 0`, `apply_to_admins: true`

**Why `["*"]` and not the five job names.** Every workflow filters on `on.pull_request.paths`, and
Forgejo 15 treats a required pattern that matches no status as `pending` — a merge blocker — while a
workflow excluded by `paths` creates no run and reports no status at all (`detectWorkflows`,
`services/actions/notifier_helper.go`; the upstream skipped-status fix, go-gitea/gitea#38237, is not
in 15.0.11). Naming the five contexts would deadlock every docs and `plans/` pull request: PR #14
(head `95ecfecc`, `plans/*.md` only) carries exactly one status, `Deploy Documentation / Build Docs
(pull_request)`. The single glob means "at least one status must exist on the head commit, and every
status that exists must be `success`". A detector job holding a second copy of the five `paths` lists
was rejected — a drifted copy either deadlocks or silently skips a component.

**A refused merge is now the normal failure mode, not a bug.** A pull request with a red check is
answered `405 not allowed to merge [reason: Not all required status checks successful]` even though
`mergeable` is `true` at the git level. Fix the check; do not reach for `force_merge`. An instance
admin with `force_merge: true` clears the protection error (`CheckPullMergeable`,
`services/pull/check.go`) because `apply_to_admins` only withholds that override from *repository*
admins. That is break-glass for the only operator, never a way to land a red change.

**Revisit only if these become true** (do not act on them now): switch to named contexts when one
always-running `pull_request` workflow becomes the sole merge gate, or when Forgejo starts reporting
a status for path-filtered workflows; raise `required_approvals` when a second account exists —
`kungraseri` is the only account and an author cannot approve their own pull request, so any
non-zero value blocks every merge.

**Update, 2026-11-03 18:56Z — the stored rules no longer match the bullets above.** At the
operator's request the rules were PATCHed to `apply_to_admins: false` on both branches, and `main`
alone was given `enable_push: true` with `push_whitelist_usernames: ["kungraseri"]`. Reason: `main`
pinned the `wiki` submodule at a commit `Veldrath.wiki` never had, so all four Coolify deploys died
at `git clone --recurse-submodules`, and the pull request that fixed it (#31, closed) could not
merge while its statuses were pending — the pin was pushed straight to `main` as `d7c19f6`. Two
things measured then, worth keeping: `apply_to_admins: false` does **not** lift `enable_push: false`
(the pre-receive hook refuses the administrator too, so the whitelist is a second switch), and
`development` still refuses every push. `scripts/apply-branch-protection.sh` and the doc table
encode the current shape, so a rule reporting `apply_to_admins: true`, or `enable_push: false` on
`main`, is now drift rather than a repair. Both rules also pin `push_whitelist_teams: []` and
`push_whitelist_deploy_keys: false` in the payload and in the read-back — the bypass is one named
account, never a team or a deploy key; the API validates a team name and round-trips the deploy-key
switch (measured). `scripts/tests/apply-branch-protection.test.sh` covers those drift cases offline
(stubbed API, no token) and runs in the docs workflow.

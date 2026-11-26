---
kind: work-item
date: 2026-11-04
area: web
title: "The wiki pin conflicts a second time, and why git cannot resolve it"
---

### Session-98 (2026-11-04) — The wiki pin conflicts a second time, and why git cannot resolve it

Two pull requests were blocked on the same afternoon, both on the `wiki` gitlink and neither on code.

**PR #48 (`docs/memory-coverage-reporter`) conflicted with a change that did the same thing.**
PR #47 (`docs(coverage): drop the last ReportGenerator instructions`, `75a19a07`) landed the
`reportgenerator` → `scripts/coverage-report.sh` swap while the branch carrying the same swap was
still open, so the two edits met on one line. Resolved by taking development's text as the base and
keeping three things from the branch: the workflow's own `-reporttypes` list, the blank line removed
from inside the code block, and the paragraph recording that the new tool's class/method counts
differ from ReportGenerator's. Merge commit `846c082b`.

**PR #29 (`development` → `main`) reported `mergeable: false` again, for the same reason as
Session-94 and by a different mechanism.** The facts, all verified against the tree:

- merge base of `main` and `development` is `0efb7c7c` (PR #26)
- `main` holds exactly two commits `development` does not: `f3661a34` (PR #3's merge, which lives
  only on `main` because merges flow one way) and `d7c19f67` (wiki pin → `725950f`)
- `development` pins `3f41462` ("Coverage: document the repository's coverage reporter"), and
  `git -C wiki merge-base --is-ancestor 725950f 3f41462` succeeds — development's pin **contains**
  main's

So the answer is obvious to a human and impossible for git: the merge base's own wiki pin is
`d8d1edf`, the dangling commit that Session-94 fixed on `main`. `git merge-tree` prints "commits
don't follow merge-base" and refuses, because it cannot compute a submodule merge base from an
object that is not in the submodule repository. **A dangling pin poisons every later merge, long
after it was fixed** — the fix has to travel backwards as a sync merge or the conflict recurs.

Resolution, which is Session-94's recipe exactly: branch off `development`, `git merge origin/main`,
and set the gitlink with
`git update-index --cacheinfo 160000,3f414629274dfeffe39436871e302d5786114574,wiki`. After that the
superproject tree is **identical** to `development`'s — `git diff --cached --stat origin/development`
is empty — which is the point of a sync merge: it carries no content, only the ancestry that makes
`main` an ancestor so PR #29 stops reporting a conflict.

**The trap this sets, recorded so it is not rediscovered:** Forgejo will describe that pull request as
an empty commit, and a PR with no diff has nothing to review or merge. This note is the change that
rides along, which is why it exists in a session log rather than only in the merge message.

**Operational note:** Forgejo's read API is open on this instance — `GET .../pulls?state=open`
returns `number`, `head.ref`, `base.ref` and `mergeable` without a token, which is how PR numbers were
identified here instead of guessed from branch dates. Only writes (`POST .../pulls`, merges) answer
`401 {"message":"token is required"}`.

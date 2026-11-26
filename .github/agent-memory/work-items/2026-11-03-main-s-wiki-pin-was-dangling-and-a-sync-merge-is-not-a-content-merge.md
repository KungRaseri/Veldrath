---
kind: work-item
date: 2026-11-03
area: engine
title: "`main`'s wiki pin was dangling, and a sync merge is not a content merge"
---

### Session-94 (2026-11-03) — `main`'s wiki pin was dangling, and a sync merge is not a content merge

Clearing the `development → main` pull request (#3) meant merging `main` back into `development`.
Two traps, both worth keeping:

- **The wiki submodule pin on `main` pointed at a commit the wiki repository does not have.** `main`
  pinned `d8d1edf` (and `90ea8f8` before it), but the wiki's own `origin/main` is `725950fd` — the
  same commit `development` pins. `git -C wiki cat-file -t d8d1edf` fails, so `git submodule update`
  on `main` dies with `reference is not a tree`. The wiki is a *separate repository* whose history
  was rewritten when it moved to KungGit, so a pin recorded in the superproject is not evidence the
  object still exists. Check with `git -C wiki cat-file -t <sha>` before bumping the pin.
- **`CONTRIBUTING.md` on `main` still had the pre-migration links** — GitHub issues and discussions,
  plus a `kungraseri.github.io` MkDocs site that no longer exists. `development`'s Forgejo
  equivalents won; main's wiki-link intent was already present identically in both versions.

Resolution kept `development`'s tree, so the sync merge changes no files: it exists only to make
`main` an ancestor of `development`. Until it lands, PR #3 reports `mergeable: false` no matter how
clean the content is — Forgejo compares histories, not just trees.

**Direct pushes to `development` are refused for everyone**, including the only admin:
`Forgejo: Not allowed to push to protected branch development` from the pre-receive hook. The rule
from Session-92 sets `enable_push: false` with no whitelist (and, since the 2026-11-03 18:56Z update
recorded under Session-92, no admin exemption either), so a sync like this cannot be pushed
directly. Forgejo also calls the resulting pull request an "empty commit"
when the trees match — give it a real change (this note) or loosen the rule deliberately.

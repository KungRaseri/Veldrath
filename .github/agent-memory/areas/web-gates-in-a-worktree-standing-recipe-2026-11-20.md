---
kind: area
date: 2026-11-20
area: web
title: "Web gates in a worktree (standing recipe, 2026-11-20)"
---

## Web gates in a worktree (standing recipe, 2026-11-20)

Cards run in `<repo>/.worktrees/<task-id>`, and a fresh worktree has **no** `web/node_modules`. Three
facts make the web gates runnable there; each one has cost a session to rediscover.

- **Symlink the main checkout's installs instead of installing.** `npx vitest` in a bare worktree
  either fails (`vitest: not found`) or npx installs a stray partial tree:

  ```bash
  ln -s <repo>/web/node_modules        <worktree>/web/node_modules
  ln -s <repo>/web/client/node_modules <worktree>/web/client/node_modules
  ```

  Both links show up as **untracked** in `git status` (`.gitignore`'s `node_modules/` matches real
  directories, not symlinks), so never `git add -A` on a web card.

- **Invoke vitest through the workspace script from `web/`** — that is what puts the cwd at
  `web/client`, matching CI:

  ```bash
  cd web && npm run test:unit --workspace client -- --run [path under client/]
  ```

- **Every parity suite resolves through `repoRoot()` — including `field-kinds.test.ts`** (fixed
  2026-11-20, card `t_497bf232`). That file used to read
  `Veldrath.Contracts/Content/ContentSchema.cs` through an inline `resolve(process.cwd(), '..', '..')`;
  from `web/`, `../..` left the repo (`<repo>/.worktrees/Veldrath.Contracts/…`) and its **2** tests
  died with `ENOENT`. It now calls `readRepoFile(...)`
  ([`web/client/src/lib/testing/source.ts`](../../../web/client/src/lib/testing/source.ts)), which walks
  up until it finds `Veldrath.Server` + `web/client` and tolerates either cwd. A suite that still
  calls `process.cwd()` is now itself a test failure — see the next bullet.

- **A moved repo file is caught by `source-paths.test.ts`, not by an ENOENT.** The parity suites read
  C#/TS sources off disk, so a path in a suite is a dependency on the repository layout; when it moves
  the suite throws at module scope and vitest reports `Tests  no tests` — the guard has vanished, not
  failed. `web/client/src/lib/parity/source-paths.test.ts` (added 2026-11-20, card `t_497bf232`;
  extended by `t_fa258afd`) scans its siblings **and the suites named in `EXTERNAL_SUITES`** for
  `readRepoFile(…)` / `walkFiles(…)` / `join(repoRoot(), '…')` and asserts every path exists,
  reporting **all** missing ones in one assertion as `file "X" — readRepoFile at <suite>:<line>`.
  The reader itself is `web/client/src/lib/testing/source-paths.ts`: it resolves a quoted literal, a
  template literal with no `${…}`, or a `const|let|var` holding one **whatever the casing**, and
  accepts a `walkFiles('…')` entry (`for (const file of …)`, `.map((file) => …)`) because the
  directory literal is what gets checked. Any other argument is unresolved — a failure — so a suite
  cannot hide a path in an expression the reader does not follow. It also reads the polarity of
  `join(repoRoot())`: `expect(existsSync(…)).toBe(false)` is checked as *must stay gone*
  (`content-schema.test.ts` uses that to keep the deleted duplicate type-key map from coming back).
  `src/lib/testing/source-paths.test.ts` pins those rules on fixtures, so a future edit that
  re-breaks one fails a test instead of leaving the guard quietly green.
  From #147 until `t_fa258afd`, `development` was red on exactly that: the guard read
  content-schema's `.toBe(false)` as a missing path. The fix is the polarity, never deleting the
  assertion, and never allow-listing the path.

- **A second source reader lives outside the parity folder.** `web/client/src/lib/game/payload-contract.test.ts`
  parses `Veldrath.Server/Hubs/GameHub.cs` against `game/types.ts`; it now reads through
  `readRepoFile()` and is listed in `EXTERNAL_SUITES` with its reason, so a hub move fails the guard
  naming that suite. Adding another such suite means adding an entry with a reason — not widening a
  directory glob, which would drag in suites that never read repo source.

- **Export the `ci-web.yml` env** ([`.github/workflows/ci-web.yml`](../../../.github/workflows/ci-web.yml))
  or SvelteKit aborts with `env_invalid`: at minimum `PUBLIC_API_URL` and `PUBLIC_HUB_URL`; `ORIGIN`,
  `BETTER_AUTH_SECRET`, `JWT_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `DATABASE_URL` complete the set.

Under that invocation the parity guard and its reader are **green — `src/lib/parity` +
`src/lib/testing` = 10 files / 56 tests** (9 files / 45 tests before card `t_fa258afd`; the 11 new
ones pin the reader's rules on fixtures). A missing repo file now fails an *assertion* in
`source-paths.test.ts` naming the path and the suite, so read that message before suspecting a parity
mismatch. Measured at `d0050f96`; the whole client unit suite was 106 files / 1556 tests there.

**Reproduce a bug claim against current `origin/development` first.** A worktree branch can lag it by
several merges: the `check-markdown-links.mjs` failure carded as t_3407c30b passed at the stale base
(61 files checked) and only reproduced after rebasing, because the file move that broke the link had
not landed yet. `git merge --ff-only origin/development` before concluding a defect is fixed or absent.

---

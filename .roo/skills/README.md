# `.roo/skills/` — Reusable Agent Skills

Reusable, tool-agnostic **Agent Skills** for AI agents working in this repository. Each skill is a folder with a spec-compliant `SKILL.md` (`name`/`description` YAML frontmatter where `name` matches the folder) plus when-to-use / when-not-to-use, references, steps, and rules.

Skills are grounded in:

1. [`AGENTS.md`](../../AGENTS.md) and the files in [`.github/instructions/`](../../.github/instructions).
2. The canonical memory store in [`.github/agent-memory/`](../../.github/agent-memory).
3. The source code itself.
4. Third-party `llms.txt` references in [`web/llms/`](../../web/llms) for SvelteKit, Drizzle, Better Auth, Tailwind, Vite, Vitest, Zod, Bits UI, Skeleton, and Lucide. From a skill at `.roo/skills/<name>/SKILL.md` that corpus is `../../../web/llms/...`.

## Catalog

| Skill | When to use |
| --- | --- |
| [`build-full`](build-full/SKILL.md) | Final verification: build every project in [`Realm.Full.slnx`](../../Realm.Full.slnx) |
| [`run-all-tests`](run-all-tests/SKILL.md) | Run the complete .NET + web test suite with coverage |
| [`run-tests-by-component`](run-tests-by-component/SKILL.md) | Test a single component (engine, server, client, forge, web, …) |
| [`create-new-feature`](create-new-feature/SKILL.md) | Add a MediatR vertical slice to `RealmEngine.Core` |
| [`run-server`](run-server/SKILL.md) | Start `Veldrath.Server` (ASP.NET Core + SignalR) |
| [`run-realmforge`](run-realmforge/SKILL.md) | Start the RealmForge content editor |
| [`start-dev-stack`](start-dev-stack/SKILL.md) | Postgres + server + optional client/web |
| [`web-development`](web-development/SKILL.md) | Build or change the SvelteKit app (`web/client`): routes, runes, load/actions, auth, Drizzle |
| [`web-ui`](web-ui/SKILL.md) | Style the web app: Veldrath design tokens, Skeleton, Bits UI, Lucide, fonts, imagery |

## Format

Every skill uses the same structure so an agent can self-select from the `description` field:

```
---
name: <folder-name>
description: <when to use, one sentence>
---

# Skill: <name>

## When to use
## When not to use
## References
## Steps
## Rules
```

Keep links repository-relative and verify paths after moving files. Do not add a skill unless it covers a repeated, multi-step workflow that is not already captured in `AGENTS.md`.

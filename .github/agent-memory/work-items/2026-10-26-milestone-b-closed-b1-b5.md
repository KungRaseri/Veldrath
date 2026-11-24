---
kind: work-item
date: 2026-10-26
area: content
title: "Milestone B closed (B1-B5)"
---

### Session-94 (2026-10-26) - Milestone B closed (B1-B5)

**Commits:** B1 `dc18be95`, B2 server `ca3997b7` + web `2828686a`, B3 `63f4efb8`, B4 `f57663e3`,
CI consolidation `98b53179`, B5 `45057457`. Epic #39 and #50-#54 all closed.

**What the milestone produced**
- `RealmEngine.Data/Validation/Content/` - one validator per content family, a type-key registry, and the
  reference rules, shared by the startup audit, the `/api/content` write path and the CI test.
- `Veldrath.Server/Features/Content/ContentWriteEndpoints.cs` - validate / POST / PUT / DELETE behind
  `Permissions.ManageContent`, soft delete only, `expectedUpdatedAt` for concurrency.
- `web/client/src/routes/(foundry)/content/[type]/[slug]/edit/` - schema-driven editor; its pure logic lives in
  `web/client/src/lib/foundry/content-editor.ts` with 21 Vitest cases.
- `GET /api/content/{type}/schema` and `POST /{type}/{slug}/validate` (dry run over an UNSAVED payload).
- Mailpit in `docker-compose.dev.yml`; `NullEmailSender` logs no bodies in any environment.

**Content defects found and fixed** (not allow-listed): two dangling quest objectives (`glowcap-mushroom`,
`lost-cargo` now real items), `arcane-focus` ManaCost -5 -> 0, and the story quest's invented `interact:`
objective kind -> `talk:`. Unknown objective kinds are now `unsupported-quest-kind` errors, and
`talk:` is explicitly accepted as implemented-but-unverifiable.

**CI is now seven workflows on purpose.** `format.yml`, `repo-hygiene.yml` and the `content-guard.yml` this
milestone briefly added are gone: formatting is a step in each .NET component job, the markdown link check is a
step in `docs.yml` (which now also runs on PRs, with deploy gated), and the content validation already runs in
`ci-server.yml` because it is part of that test project. Do not add a workflow for a check that a component
workflow already runs.

**Counts at close:** 3,999 .NET tests (Core 2,021 / Shared 793 / Data 301 / Server 845 / Discord 29 / Assets 10),
web Vitest 11 client files + 12 shared cases, typecheck/lint/build clean.

---
kind: work-item
date: 2026-11-27
area: content
card: t_7f599514
pr: 339
title: "Foundry refuses author saves over live maps"
---

## What changed

[`MapEditorService.SaveAsync`](../../../Veldrath.Server/Features/Content/MapEditorService.cs) checks the version first,
then refuses saves over an active map for callers without `SeesEveryDraft`. The existing `conflict` result is HTTP
409. `LiveMapSaveRefusal` names the four steps: a reviewer deactivates, the author edits and saves, the author
submits, and a reviewer approves. Editors and reviewers can still save live maps, and draft saves are unchanged.

## What stayed the same

`ApprovedVersion` and the API contracts are unchanged. Gameplay still reads the stored active graph through
[`EfCoreMapGraphRepository.GetMapAsync`](../../../RealmEngine.Data/Repositories/EfCoreMapGraphRepository.cs).
There was no migration.

## Why the loop stops at deactivation

`MapEditorService.DeactivateAsync` refuses to retire a live map while any node targets it.
[`MapGraphSeeder.LinkRegionZonesToZoneMaps`](../../../RealmEngine.Data/Seeders/MapGraphSeeder.cs) supplies inbound
links to the playable zone maps, and travel exits also target region maps. The only untargeted seeded map is
`zone:playground`, a dev sandbox. Authors are effectively blocked from editing live seeded gameplay maps until
there's a way to edit a separate copy without replacing the approved graph. The approved-copy model and the
interim workflow are tracked in `t_36037e03` for `dev-architect`.

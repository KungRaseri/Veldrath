---
kind: work-item
date: 2026-11-27
area: content
card: t_7f599514
pr: 339
title: "Foundry refuses author saves over live maps"
---

## What changed

[`MapEditorService.SaveAsync`](../../../Veldrath.Server/Features/Content/MapEditorService.cs) checks `MayEdit`
before checking the version. Only then does it refuse saves over an active map for callers without
`SeesEveryDraft`, returning the existing `conflict` result (HTTP 409). `LiveMapSaveRefusal` names the four
steps: a reviewer deactivates, the author edits and saves, the author submits, and a reviewer approves.

Draft saves are unchanged. [`MapCaller`](../../../Veldrath.Server/Features/Content/MapCaller.cs) lets editors
(`content.manage`) write any map. `content.review` bypasses the live-save refusal only on maps the reviewer
can already write. Review permission alone cannot save a seeded map or another author's map.

## What stayed the same

`ApprovedVersion` and the API contracts are unchanged. Gameplay still reads the stored active graph through
[`EfCoreMapGraphRepository.GetMapAsync`](../../../RealmEngine.Data/Repositories/EfCoreMapGraphRepository.cs).
There was no migration.

## Why the loop stops at deactivation

`MapEditorService.DeactivateAsync` refuses to retire a live map while any node targets it.
[`MapGraphSeeder.LinkRegionZonesToZoneMaps`](../../../RealmEngine.Data/Seeders/MapGraphSeeder.cs) supplies inbound
links to the playable zone maps, and travel exits also target region maps. The only untargeted seeded map is
`zone:playground`, a dev sandbox. Deactivation does not grant write access, even where it succeeds. The loop
therefore gives authors no way to revise live seeded gameplay maps while keeping the approved graph in play.
The approved-copy model and the interim workflow are tracked in `t_36037e03` for `dev-architect`.

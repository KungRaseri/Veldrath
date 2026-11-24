---
kind: area
date: undated
area: content
title: "IContentService Extension (session-13)"
---

## IContentService Extension (session-13)

- 6 new content types added: `Organization`, `WorldLocation`, `Dialogue`, `ActorInstance`, `MaterialProperty`, `TraitDefinition`
- 12 new methods on `IContentService` + `HttpContentService` (2 per type: list + single)
- Routes: `api/content/organizations`, `api/content/world-locations`, `api/content/dialogues`, `api/content/actor-instances`, `api/content/material-properties`, `api/content/traits`
- `TraitDefinition` uses `key` param (not `slug`) in route and DTO
- All 12 implementations are one-liner `GetListAsync<T>` or `GetSingleAsync<T>` delegates
- `FakeContentService` stubs all 12 with `Task.FromResult(new List<T>())` / `Task.FromResult<T?>(null)`
- 24 new tests in `HttpContentServiceTests.cs` (4 per type)

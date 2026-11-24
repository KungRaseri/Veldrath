---
kind: area
date: undated
area: engine
title: "DI Registration — UseExternal Pattern"
---

## DI Registration — UseExternal Pattern

> **See [`.github/instructions/ef-core-patterns.md`](../../../.github/instructions/ef-core-patterns.md#di-registration--the-useexternal-pattern) for the UseExternal DI pattern.**

The server calls `AddRealmEngineCore(p => p.UseExternal())` which **intentionally skips all persistence-layer registrations**. The server must manually register every `IXxxRepository` and `IXxxService` it needs in `Program.cs`. Always run `dotnet build Veldrath.Server` after adding a new Core handler to catch missing registrations before Docker.

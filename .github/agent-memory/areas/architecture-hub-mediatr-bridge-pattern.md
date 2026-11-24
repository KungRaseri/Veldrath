---
kind: area
date: undated
area: engine
title: "Architecture: Hub → MediatR Bridge Pattern"
---

## Architecture: Hub → MediatR Bridge Pattern

> **See [`.github/instructions/hub-development.md`](../../../.github/instructions/hub-development.md) for the canonical hub development conventions, including the Hub→MediatR bridge pattern, single-DTO parameter rule, and DI registration patterns.**

`SelectCharacter` and `EnterZone` are **session management** — they do NOT call `mediator.Send`. The character tracker and player session repository ARE the implementation. This is intentional.

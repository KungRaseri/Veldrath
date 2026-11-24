---
kind: area
date: undated
area: content
title: "Test Counts"
---

## Test Counts

> **Current (2026-11-04, after `Veldrath.Assets` was deleted in Session-97):** **5 .NET test
> projects, 4,011 tests** — `RealmEngine.Core` 2,040, `RealmEngine.Shared` 795, `RealmEngine.Data`
> 301, `Veldrath.Server` 846, `Veldrath.Discord` 29, which is exactly the 4,011 that
> `dotnet test Realm.Full.slnx --no-build` reported passing. There is no client or tooling test
> project any more — the web app is covered by Vitest + Playwright from `web/`. The table below is the
> historical per-session log; its "Client Tests" column refers to the deleted Avalonia client.

| Session | Client Tests | Server Tests | Total |
|---|---|---|---|
| Session-2 (2026-03-19) | 203 | 217 | 420 |
| Session-3 (2026-03-19) | 268 | 235 | 503 |
| Session-4 (2026-03-19) | 274 | 246 | 520 |
| Session-5 (2026-03-19) | **281** | **260** | **541** |
| Session-8 (2026-03-20) | **281** | **279** | **560** |
| Session-9 (2026-03-20) | **288** | **292** | **580** |
| Session-10 (2026-03-20) | **296** | **304** | **600** |
| Session-11 (2026-03-20) | **304** | **304** | **608** |
| Session-12 (2026-03-20) | **308** | **307** | **615** |
| Session-13 (2026-03-20) | **340** | **383** | **723** |
| Session-14 (2026-03-21) | **340** | **383** | **723** |
| Session-15 (2026-03-21) | **340** | **416** | **756** |
| Session-16 (2026-03-21) | **362** | **416** | **778** |
| Session-17 (2026-03-21) | **401** | **425** | **826** |
| Session-18 (2026-03-24) | **466** | **461** | **927** |
| Session-19 (server-offline UX) | **511** | **468** | **979** |
| Session-20 (server reconnect polling) | **518** | **468** | **986** |
| Session-26 (2026-03-30: Phase 0b–0c) | **512** | **468** | **980** |
| Session-27 (2026-03-30: combat system phases 1-7) | **512** | **468** | **980** |
| Session-28 (2026-03-30: combat tests + GameView combat UI) | **525** | **491** | **1016** |
| Session-29 (2026-04-08: region map arch + PlayerSession migration) | **525** | **560** | **1085** |
| Session-30 (2026-04-10: OAuth provider-link confirmation flow) | **525** | **571** | **1096** |
| Session-31 (2026-04-11: Auth hardening pass 1) | **713** | **728** | **1441** |
| Session-32-39 | ~4,500 | Post-Session-31 work (chat, moderation, auth overhaul, editorial, web) |
| Session-40 | ~4,732 | Expansion & polish: Discord tests, achievement wiring, zone TMX maps, settings cleanup |

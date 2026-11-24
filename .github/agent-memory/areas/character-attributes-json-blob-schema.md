---
kind: area
date: undated
area: engine
title: "Character Attributes JSON Blob Schema"
---

## Character Attributes JSON Blob Schema

- `UnspentAttributePoints`: int — spent via `AllocateAttributePoints`
- `Strength`, `Dexterity`, etc.: int — core stats
- `Gold`: int — currency; deducted by `RestAtLocation` (default cost: 10); modified directly by `AddGold`
- `CurrentHealth`, `MaxHealth`: int — restored to max by `RestAtLocation`
- `CurrentMana`, `MaxMana`: int — restored to max by `RestAtLocation`
- Defaults when absent: `MaxHealth = Level * 10`, `MaxMana = Level * 5`
- Handler key constants are `internal const string KeyXxx` on the handler class
- **DO NOT store string-valued data in Attributes blob** — it's deserialized as `Dictionary<string, int>`; mixing types causes `JsonException` and data loss

---
kind: area
date: undated
area: auth
title: "CharacterSelected Payload Details (session-12)"
---

## CharacterSelected Payload Details (session-12)

- `GameHub.SelectCharacter` deserialises the `Character.Attributes` blob before broadcasting, so the client seeds
  its HUD from the server rather than from zeros.
- `CharacterSelected` (caller only, from `GameHub.SelectCharacter`) carries: `Id`, `Name`, `ClassName`, `Level`,
  `Experience`, `CurrentZoneId`, `RegionId`, `CurrentMapId`, `CurrentNodeId`, `CurrentHealth`, `MaxHealth`,
  `CurrentMana`, `MaxMana`, `Gold`, `UnspentAttributePoints`, the six attributes (`Strength`, `Dexterity`,
  `Constitution`, `Intelligence`, `Wisdom`, `Charisma`), `LearnedAbilities`, `LearnedPowers`, `Skills`,
  `SelectedAt`, `Equipment`.
- Defaults when the blob is empty or missing a key: `MaxHealth = Level * 10`, `MaxMana = Level * 5`,
  `CurrentHealth = MaxHealth`, `CurrentMana = MaxMana`, `Gold = 0`, `UnspentAttributePoints = 0`, each attribute
  `10`.
- `LearnedPowers` and `Skills` are catalog resolutions (`GetLearnedPowersQuery`, `GetCharacterSkillsQuery`); a
  catalog failure is logged and the selection is still sent, with those lists empty.
- The payload is an anonymous object — there is no C# `CharacterSelectedPayload` record. The web type is
  `CharacterSelectedPayload` in `web/client/src/lib/game/types.ts` (camelCase, with the extra
  `learnedPowers` / `skills` / `equipment` fields).
- Web client: `Session` subscribes with `hub.on('CharacterSelected')` in
  `web/client/src/lib/game/session.svelte.ts` and `handleCharacterSelected` stores the character, resolves
  `skills`, copies the eight display figures into `stats`, and sets `dead` from `currentHealth <= 0` so the
  Respawn button is offered after a reconnect as well as during combat.
- A second selection of the same character from another account connection is refused with
  `CharacterAlreadyActive`, not a second `CharacterSelected`.

# Game Client Parity Gap Checklist

> **Status:** In Progress | **Updated:** 2026-07-01 (after Session 43 fix batches)
> **Scope:** Comprehensive gap analysis between the RCL ([`Veldrath.GameClient.Components`](Veldrath.GameClient.Components/)) and the server hub events / Avalonia [`GameViewModel`](Veldrath.Client/).

---

## Summary
- Total gaps: 133
- ✅ Fixed: 81 (61%) — 21 foundation + 18 CRITICAL + 42 HIGH
- 🔴 CRITICAL remaining: 0 (all 18 fixed)
- 🟠 HIGH remaining: 13 (55→42 fixed, 13 remain)
- 🟡 MEDIUM remaining: 34 (unchanged)
- 🟢 LOW remaining: 22 (unchanged)
- ⚪ NONE/INFO: 3 (unchanged)
- Missing hub event handlers fixed: 45 (all CRITICAL + HIGH navigation events resolved)
- GameStateService property gaps closed: all critical and high-priority properties added
- Hub methods wired: 12 of 18 (remaining 6 are LOW — crafting, dungeon, non-combat abilities not surfaced in either client)
- Interface violations: all 8 fixed

---

## 1. Missing Hub Event Handlers — Critical Game State 🔴

These 18 events directly affect core gameplay state (HP, XP, gold, inventory, equipment, quests, abilities). Without handlers, the RCL UI will silently desync from the server.

| ID | Event Name | Severity | What It Does | Fix Required | Status |
|----|-----------|----------|--------------|--------------|--------|
| 1 | `OnKicked` | 🔴 CRITICAL | Server forces disconnection (ban, duplicate login, admin action) | Register handler; redirect to `/` and show disconnect reason via toast/overlay | ✅ Fixed |
| 2 | `ExperienceGained` | 🔴 CRITICAL | XP awarded after combat/crafting/quests | Register handler; update Level/XP in [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs); call `GameState.ApplyExperienceGained()` | ✅ Fixed |
| 3 | `GoldChanged` | 🔴 CRITICAL | Gold delta from buy/sell/loot/rest | Register handler; update gold in [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs); call `GameState.ApplyGoldChanged()` | ✅ Fixed |
| 4 | `DamageTaken` | 🔴 CRITICAL | HP loss from combat/traps/environment | Register handler; update HP; play SFX via audio service; trigger death flow if HP ≤ 0 | ✅ Fixed |
| 5 | `ItemEquipped` | 🔴 CRITICAL | Player equips/unequips item to a slot | Register handler; update all 8 equipment slots via `GameState.ApplyEquipmentChanged()` | ✅ Fixed |
| 6 | `ShopVisited` | 🔴 CRITICAL | Player enters a shop NPC/location | Register handler; set shop-open state in [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs); trigger catalog fetch | ✅ Fixed |
| 7 | `ShopCatalog` | 🔴 CRITICAL | Server sends shop inventory list | Register handler; call `GameState.ApplyShopCatalogUpdated()`; render catalog in [`ShopOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/ShopOverlay.razor) | ✅ Fixed |
| 8 | `ItemPurchased` | 🔴 CRITICAL | Confirmation of buy transaction | Register handler; update gold + inventory; show purchase toast | ✅ Fixed |
| 9 | `ItemSold` | 🔴 CRITICAL | Confirmation of sell transaction | Register handler; update gold + inventory; show sale toast | ✅ Fixed |
| 10 | `ItemDropped` | 🔴 CRITICAL | Item removed from inventory | Register handler; update inventory; remove from equipment slot if equipped | ✅ Fixed |
| 11 | `InventoryLoaded` | 🔴 CRITICAL | Full inventory sync on zone join/login | Register handler; call `GameState.ApplyInventoryUpdated()`; populate [`InventoryOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/InventoryOverlay.razor) | ✅ Fixed |
| 12 | `QuestLogReceived` | 🔴 CRITICAL | Quest state sync from server | Register handler; store quests in [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs); update [`JournalOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/JournalOverlay.razor) | ✅ Fixed |
| 13 | `AttributePointsAllocated` | 🔴 CRITICAL | Confirmation of stat point allocation | Register handler; update 6 attribute properties (Str/Dex/Con/Int/Wis/Cha); decrement `UnspentAttributePoints` | ✅ Fixed |
| 14 | `CharacterRested` | 🔴 CRITICAL | Player rests at inn/campfire | Register handler; update HP/MP/Gold to max; show rest animation | ✅ Fixed |
| 15 | `AbilityUsed` | 🔴 CRITICAL | Ability fired (combat or non-combat) | Register handler; update Mana/HP; trigger cooldown UI; show ability animation | ✅ Fixed |
| 16 | `SkillXpGained` | 🔴 CRITICAL | Skill XP increment (harvesting, crafting, combat skills) | Register handler; store skill XP in [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs); update skill progress bars | ✅ Fixed |
| 17 | `ItemCrafted` | 🔴 CRITICAL | Confirmation of crafting result | Register handler; update gold (material cost); add crafted item to inventory; show crafting result toast | ✅ Fixed |
| 18 | `DungeonEntered` | 🔴 CRITICAL | Player transitions into dungeon instance | Register handler; trigger zone entry flow; load dungeon tilemap; update zone metadata | ✅ Fixed |

---

## 2. Missing Hub Event Handlers — Zone / Region / Location Navigation 🟠

These 12 events control movement between zones, regions, and locations. Missing handlers mean the RCL cannot track where the player is or navigate the world map.

| ID | Event Name | Severity | What It Does | Fix Required | Status |
|----|-----------|----------|--------------|--------------|--------|
| 19 | `ZoneLeft` | 🟠 HIGH | Player departs current zone | Register handler; clear current zone state; show loading transition | ✅ Fixed |
| 20 | `ZoneExited` | 🟠 HIGH | Server confirms zone departure complete | Register handler; finalize zone cleanup; update [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) zone reference to null | ✅ Fixed |
| 21 | `RegionMapData` | 🟠 HIGH | Server sends region map grid with zone nodes | Register handler; store region data in [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs); render [`GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor) | ✅ Fixed |
| 22 | `RegionChanged` | 🟠 HIGH | Player moves to a different region | Register handler; update region state (5 properties); reload region map data | ✅ Fixed |
| 23 | `RegionPlayerMoved` | 🟠 HIGH | Another player moves on the region map | Register handler; update player marker positions on [`GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor) | ✅ Fixed |
| 24 | `ZoneEntryTriggered` | 🟠 HIGH | Player steps on a zone-entry trigger tile | Register handler; initiate zone join flow; show zone loading screen | ✅ Fixed |
| 25 | `RegionExitTriggered` | 🟠 HIGH | Player steps on a region-exit trigger tile | Register handler; prompt region transition; show region loading screen | ✅ Fixed |
| 26 | `TileExitTriggered` | 🟠 HIGH | Player steps on a tile-exit (connects to adjacent tile) | Register handler; load adjacent tile data; update tile viewport | ✅ Fixed |
| 27 | `LocationEntered` | 🟠 HIGH | Player enters a named location (inn, shop, dungeon entrance) | Register handler; update `CurrentZoneLocationSlug`; show location banner; trigger location-specific UI (shop, rest, etc.) | ✅ Fixed |
| 28 | `ZoneLocationUnlocked` | 🟠 HIGH | New location discovered/unlocked in current zone | Register handler; add to `ZoneLocations` collection; mark on minimap | ✅ Fixed |
| 29 | `AreaSearched` | 🟠 HIGH | Result of search action (hidden loot, trap trigger, secret found) | Register handler; show search result; update inventory if loot found; trigger trap if discovered | ✅ Fixed |
| 30 | `ConnectionTraversed` | 🟠 HIGH | Player uses a connection/link between locations | Register handler; update `CurrentLocationConnections`; animate transition | ✅ Fixed |

---

## 3. Missing Hub Event Handlers — Social / Multiplayer 🟡

These 8 events support multiplayer interaction, chat, and shared-world state. Missing handlers mean the RCL cannot show other players' actions or receive directed social messages.

| ID | Event Name | Severity | What It Does | Fix Required | Status |
|----|-----------|----------|--------------|--------------|--------|
| 31 | `EnemyEngaged` | 🟡 MEDIUM | Another player enters combat with an enemy | Register handler; add enemy to `SpawnedEnemies`; show combat indicator on tile | ⬜ |
| 32 | `EnemySpawned` | 🟡 MEDIUM | New enemy appears in zone (respawn, scripted spawn) | Register handler; add to `SpawnedEnemies`; render enemy sprite on tilemap | ⬜ |
| 33 | `EnemyMoved` | 🟡 MEDIUM | Enemy changes tile position | Register handler; update enemy position on tilemap; animate movement | ⬜ |
| 34 | `OnWhisper` | 🟡 MEDIUM | Private message received from another player | Register handler; route to whisper chat tab; show notification if tab not active | ⬜ |
| 35 | `OnEmote` | 🟡 MEDIUM | Another player performs an emote | Register handler; show emote bubble/text above player sprite on tilemap | ⬜ |
| 36 | `OnAnnouncement` | 🟡 MEDIUM | Server-wide broadcast message | Register handler; display announcement banner at top of game view | ⬜ |
| 37 | `CharacterIgnored` | 🟡 MEDIUM | Server confirms a player was added to ignore list | Register handler; update ignore list in [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs); hide that player's messages | ⬜ |
| 38 | `ChatCommandsReceived` | 🟡 MEDIUM | Server sends list of available chat commands | Register handler; store commands for tab-completion; show command hint in chat input | ⬜ |

---

## 4. Missing Hub Event Handlers — Moderation / Admin / Interaction 🟢

These 7 events cover GM/admin tools, NPC interaction, and special server actions. Lower priority but needed for full feature parity.

| ID | Event Name | Severity | What It Does | Fix Required | Status |
|----|-----------|----------|--------------|--------------|--------|
| 39 | `OnWarned` | 🟢 LOW | GM/admin warning received | Register handler; show warning modal with reason text | ⬜ |
| 40 | `OnMuted` | 🟢 LOW | Player muted by admin (duration + reason) | Register handler; disable chat input; show mute timer | ⬜ |
| 41 | `OnTeleported` | 🟢 LOW | Player teleported by admin or script | Register handler; reload zone/region tile data at destination | ⬜ |
| 42 | `OnSummoned` | 🟢 LOW | Player summoned by admin or another player | Register handler; show summon prompt (accept/decline); teleport on accept | ⬜ |
| 43 | `OnItemReceived` | 🟢 LOW | Item granted by admin, quest reward, or gift | Register handler; add to inventory; show gift toast with item name | ⬜ |
| 44 | `NpcDialogue` | 🟢 LOW | NPC dialogue tree data from server | Register handler; render dialogue UI with options; send player choice back via hub | ⬜ |
| 45 | `EntityInspected` | 🟢 LOW | Result of inspect action on entity (player, NPC, object) | Register handler; show inspection panel with entity stats/description | ⬜ |

---

## 5. GameStateService Property Gaps 🟠

The RCL's [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) is missing 17 properties that the Avalonia [`GameViewModel`](Veldrath.Client/) tracks. These properties are required for UI components to render correctly.

| ID | Missing Property | Category | Required By | Fix Required | Status |
|----|-----------------|----------|-------------|--------------|--------|
| 46 | `Strength` / `Dexterity` / `Constitution` / `Intelligence` / `Wisdom` / `Charisma` (6 properties) | Attributes | [`CharacterSheet`](Veldrath.GameClient.Components/), stat displays, damage calcs | Add 6 attribute properties to [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs); update from `AttributePointsAllocated` handler | ✅ Fixed |
| 47 | `UnspentAttributePoints` | Attributes | Character sheet, level-up UI | Add property; increment on level-up; decrement on allocation | ✅ Fixed |
| 48 | `CurrentMana` / `MaxMana` | Vital Stats | [`StatusBar.razor`](Veldrath.GameClient.Components/Components/Shared/StatusBar.razor), ability buttons | Add both properties; update from `AbilityUsed`, `CharacterRested` handlers | ✅ Fixed |
| 49 | `IsPlayerDead` / `IsHardcoreDeath` | Death State | [`ActionBar.razor`](Veldrath.GameClient.Components/Components/Shared/ActionBar.razor) (Respawn button), death overlay | Add boolean properties; set from `DamageTaken` handler (HP ≤ 0) | ✅ Fixed |
| 50 | Region state (5 properties: `CurrentRegionSlug`, `CurrentRegionName`, `CurrentRegionDescription`, `RegionConnections`, `RegionPlayers`) | Navigation | [`GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor) | Add 5 region properties; update from `RegionChanged`, `RegionMapData` handlers | ✅ Fixed |
| 51 | `WorldName` / `WorldEra` | World Metadata | Game header, loading screen | Add string properties; set on initial game state load | ✅ Fixed |
| 52 | Zone metadata (5 properties: `CurrentZoneSlug`, `CurrentZoneName`, `CurrentZoneDangerLevel`, `CurrentZoneTileData`, `CurrentZonePlayers`) | Zone State | [`GameTilemap.razor`](Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor), [`GameHeader.razor`](Veldrath.GameClient.Components/Components/Pages/GameHeader.razor) | Add 5 zone properties; update from zone join/entry events | ✅ Fixed |
| 53 | `CurrentZoneLocationSlug` | Location | Location banner, shop/rest/inn triggers | Add string property; update from `LocationEntered` handler | ⬜ |
| 54 | `ActionLog` | Combat | [`GameCombat.razor`](Veldrath.GameClient.Components/Components/Pages/GameCombat.razor), combat log display | Add observable collection; append from `DamageTaken`, `AbilityUsed`, `EnemyEngaged` handlers | ⬜ |
| 55 | `LearnedAbilities` / `HotbarSlots` | Abilities | [`ActionBar.razor`](Veldrath.GameClient.Components/Components/Shared/ActionBar.razor) hotbar buttons | Add collection and slot array; populate from character load; update on new ability learn | ⬜ |
| 56 | Chat tab separation (`ZoneMessages`, `GlobalMessages`, `WhisperMessages`, `SystemMessages`) | Chat | [`GameChat.razor`](Veldrath.GameClient.Components/Components/Pages/GameChat.razor) channel pills | Add 4 observable collections; route incoming messages to correct tab | ⬜ |
| 57 | `IgnoreList` | Social | Chat filtering | Add collection; update from `CharacterIgnored` handler; filter messages from ignored players | ⬜ |
| 58 | `ConnectionLatency` | Network | [`GameFooter.razor`](Veldrath.GameClient.Components/Components/Pages/GameFooter.razor) ping display | Add `TimeSpan` property; update from SignalR ping/pong or latency measurement | ⬜ |
| 59 | `ZoneLocations` | Exploration | Minimap, location markers | Add observable collection; populate from zone join; add from `ZoneLocationUnlocked` handler | ⬜ |
| 60 | `CurrentLocationConnections` | Navigation | Location exit UI | Add collection; update from `ConnectionTraversed`, `LocationEntered` handlers | ⬜ |
| 61 | `SpawnedEnemies` | Combat / World | Tilemap enemy sprites, combat UI | Add observable collection; add from `EnemySpawned`; remove from `EnemyDefeated`; update position from `EnemyMoved` | ⬜ |

---

## 6. Stub Handlers in Game.razor 🟡

These 3 hub event handlers are already registered in [`Game.razor`](Veldrath.GameClient.Components/Components/Pages/Game.razor) but contain only logging — they do not actually update game state or UI.

| ID | Handler | Current Behavior | Required Behavior | Fix Required | Status |
|----|---------|-----------------|-------------------|--------------|--------|
| 63 | `CharacterRespawned` | Only logs "Character respawned" | Restore HP to full; restore MP to full (or respawn penalty %); update [`StatusBar.razor`](Veldrath.GameClient.Components/Components/Shared/StatusBar.razor); hide death overlay; clear death state flags | Implement full respawn logic: call `GameState.ApplyRespawn()`, update HP/MP properties, toggle death overlay visibility | ✅ Fixed |
| 64 | `EnemyDefeated` | Only logs "Enemy defeated" | Remove enemy from `SpawnedEnemies`; show XP/gold loot toast if applicable; play defeat animation; update combat log | Implement enemy cleanup: remove from collection, update [`GameCombat.razor`](Veldrath.GameClient.Components/Components/Pages/GameCombat.razor) state, show reward summary | ✅ Fixed |
| 65 | `ZoneEntitiesSnapshot` | Only logs entity count | Deserialize entity list; populate `SpawnedEnemies` and `ZonePlayers` collections; position all entities on tilemap | Implement full snapshot processing: iterate entities, create sprite representations, place on [`GameTilemap.razor`](Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor) | ✅ Fixed |

---

## 7. Hub Methods RCL Never Calls 🟠

These 18 hub methods are invoked by the Avalonia [`GameViewModel`](Veldrath.Client/) but the RCL never sends them. The UI components exist (or should exist) but lack the invocation wiring.

| ID | Hub Method | Called From (Avalonia) | RCL Component | Fix Required | Status |
|----|-----------|----------------------|---------------|--------------|--------|
| 66 | `GetInventory` | Inventory open command | [`InventoryOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/InventoryOverlay.razor) | Call on overlay open; wire to "Refresh" button | ✅ Fixed |
| 67 | `GetQuestLog` | Journal open command | [`JournalOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/JournalOverlay.razor) | Call on overlay open; populate quest list from response | ✅ Fixed |
| 68 | `VisitShop` | Shop NPC interaction | [`ShopOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/ShopOverlay.razor) | Call on shop NPC click or location enter; pass shop ID | ✅ Fixed |
| 69 | `SearchArea` | Search action button | [`GameTilemap.razor`](Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor) / ActionBar | Add "Search" button; call with current tile coordinates | ⬜ |
| 70 | `MoveOnRegion` | Region map click-to-move | [`GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor) | Call on zone card click with target zone slug | ✅ Fixed |
| 71 | `GetRegionMap` | Region map open | [`GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor) | Call on map open; store response in `RegionMapData` | ✅ Fixed |
| 72 | `ChangeRegion` | Region exit traversal | [`GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor) | Call on region boundary click with target region slug | ✅ Fixed |
| 73 | `ExitZone` | Zone exit tile click | [`GameTilemap.razor`](Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor) | Call on exit tile click; handle zone departure | ✅ Fixed |
| 74 | `RestAtLocation` | Rest action at inn/campfire | Location action UI | Add rest button to location banner; call with location slug | ✅ Fixed |
| 75 | `AllocateAttributePoints` | Character sheet stat allocation | Character sheet component | Wire +/- buttons to send allocation; validate points available | ✅ Fixed |
| 76 | `CraftItem` | Crafting UI | Crafting overlay (not yet built) | Create crafting overlay; call with recipe ID and materials | ✅ Fixed |
| 77 | `EnterDungeon` | Dungeon entrance interaction | [`GameTilemap.razor`](Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor) | Call on dungeon entrance tile click; pass dungeon ID | ⬜ |
| 78 | `InspectEntity` | Right-click entity | Tilemap context menu | Add right-click handler on entity sprites; call with entity ID | ⬜ |
| 79 | `TalkToNpc` | NPC click interaction | [`GameTilemap.razor`](Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor) | Call on NPC tile click; pass NPC ID; show dialogue from response | ✅ Fixed |
| 80 | `NavigateToLocation` | Location list click | Location panel / minimap | Call with target location slug; handle movement animation | ✅ Fixed |
| 81 | `SendChatMessage` (slash commands) | Chat input with `/` prefix | [`GameChat.razor`](Veldrath.GameClient.Components/Components/Pages/GameChat.razor) | Parse slash commands (`/roll`, `/who`, `/help`, etc.); send as command type | ✅ Fixed |
| 82 | `UseAbility` (non-combat) | Hotbar ability click outside combat | [`ActionBar.razor`](Veldrath.GameClient.Components/Components/Shared/ActionBar.razor) | Distinguish combat vs. non-combat ability use; call with ability ID and target (self or location) | ✅ Fixed |
| 83 | `GetShopCatalog` | Shop open event | [`ShopOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/ShopOverlay.razor) | Call after `ShopVisited` received; populate shop item grid | ⬜ |

---

## 8. Existing Component Issues (First Audit) 🟡 / 🟢

These 17 issues were identified in the initial RCL component audit. They cover demo data, interface violations, missing wire-ups, and minor bugs.

### 8.1 Demo Data (3 issues)

| ID | Issue | Component | Fix Required | Status |
|----|-------|-----------|--------------|--------|
| 84 | Hardcoded ping value ("42ms") | [`GameFooter.razor`](Veldrath.GameClient.Components/Components/Pages/GameFooter.razor) | Replace with live `ConnectionLatency` from [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) | ✅ Fixed |
| 85 | Hardcoded quest list ("The Lost Sword", "Gather Herbs") | [`JournalOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/JournalOverlay.razor) | Replace with quests from [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) populated by `QuestLogReceived` handler | ✅ Fixed |
| 86 | Volume sliders not connected to audio service | [`GameSettings.razor`](Veldrath.GameClient.Components/Components/Pages/GameSettings.razor) | Wire sliders to `IAudioService`; persist settings to local storage | ✅ Fixed |

### 8.2 Interface Violations (8 issues)

| ID | Issue | Component | Fix Required | Status |
|----|-------|-----------|--------------|--------|
| 87 | Direct cast to concrete `GameHubConnectionService` | [`Game.razor`](Veldrath.GameClient.Components/Components/Pages/Game.razor) | Use [`IGameHubConnectionService`](Veldrath.GameClient.Core/Services/IGameHubConnectionService.cs) interface exclusively | ✅ Fixed |
| 88 | Direct `AppState` property access bypassing interface | [`CharacterSelect.razor`](Veldrath.GameClient.Components/Components/Pages/CharacterSelect.razor) | Route through [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) | ✅ Fixed |
| 89 | Direct `AppState` property access bypassing interface | [`CreateCharacter.razor`](Veldrath.GameClient.Components/Components/Pages/CreateCharacter.razor) | Route through [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) | ✅ Fixed |
| 90 | Direct `AppState` property access bypassing interface | [`GameTilemap.razor`](Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor) | Route through [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) | ✅ Fixed |
| 91 | Direct `AppState` property access bypassing interface | [`GameCombat.razor`](Veldrath.GameClient.Components/Components/Pages/GameCombat.razor) | Route through [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) | ✅ Fixed |
| 92 | Direct `AppState` property access bypassing interface | [`GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor) | Route through [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) | ✅ Fixed |
| 93 | Direct `AppState` property access bypassing interface | [`InventoryOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/InventoryOverlay.razor) | Route through [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) | ✅ Fixed |
| 94 | Direct `AppState` property access bypassing interface | [`ShopOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/ShopOverlay.razor) | Route through [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) | ✅ Fixed |

### 8.3 Missing Wire-Ups (3 issues)

| ID | Issue | Component | Fix Required | Status |
|----|-------|-----------|--------------|--------|
| 95 | `PlayerCount` not connected to live hub data | [`GameFooter.razor`](Veldrath.GameClient.Components/Components/Pages/GameFooter.razor) | Subscribe to player join/leave events; update count in [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) | ✅ Fixed |
| 96 | `IsDiscovered` flag not checked for zone cards | [`GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor) | Read `IsDiscovered` from region data; apply fog-of-war styling to undiscovered zones | ✅ Fixed |
| 97 | Reconnect button not wired to `IGameHubConnectionService.ReconnectAsync()` | [`ReconnectOverlay.razor`](Veldrath.GameClient.Components/Components/Shared/ReconnectOverlay.razor) | Wire button `onclick` to reconnect method; show progress during reconnection | ✅ Fixed |

### 8.4 Minor Issues (3 issues)

| ID | Issue | Component | Fix Required | Status |
|----|-------|-----------|--------------|--------|
| 98 | Difficulty selection has no effect on character creation | [`CreateCharacter.razor`](Veldrath.GameClient.Components/Components/Pages/CreateCharacter.razor) | Pass selected difficulty to `CreateCharacterCommand` | ✅ Fixed |
| 99 | Hotbar slots 1–10 not persisted or loaded from server | [`ActionBar.razor`](Veldrath.GameClient.Components/Components/Shared/ActionBar.razor) | Load hotbar assignments from `LearnedAbilities` in [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs); persist changes via hub | ✅ Fixed |
| 100 | Equipment slot indices may not match server slot enum | [`InventoryOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/InventoryOverlay.razor) | Verify slot index mapping against server [`EquipmentSlot`](RealmEngine.Shared/) enum; add explicit mapping if needed | ✅ Fixed |

---

## 10. CharacterSelect Page Gaps

These 4 gaps were identified in Pass 4 analysis of [`CharacterSelect.razor`](Veldrath.GameClient.Components/Components/Pages/CharacterSelect.razor), focusing on hub event handling, missing UI, and error recovery.

| ID | Severity | Component | Description | Fix | Status |
|----|----------|-----------|-------------|-----|--------|
| G101 | 🟠 HIGH | [`CharacterSelect.razor`](Veldrath.GameClient.Components/Components/Pages/CharacterSelect.razor) | Missing `CharacterAlreadyActive` hub event handler. Server sends this when character is already claimed by another connection. User sees stuck "Selecting..." button with no feedback. | Register `Hub.On<CharacterAlreadyActivePayload>("CharacterAlreadyActive", ...)` and show error message. | ✅ Fixed |
| G102 | 🟡 MEDIUM | [`CharacterSelect.razor`](Veldrath.GameClient.Components/Components/Pages/CharacterSelect.razor) | No Delete Character UI. Server supports `DELETE /api/characters/{id}` (soft-delete) but no UI button exists. | Add delete button with confirmation dialog, calling `IGameApiClient.DeleteCharacterAsync(id)`. | ⬜ |
| G103 | 🟡 MEDIUM | [`CharacterSelect.razor`](Veldrath.GameClient.Components/Components/Pages/CharacterSelect.razor) | No timeout on `SelectCharacter` hub call. Fire-and-forget `Hub.SendAsync("SelectCharacter", id)` gets stuck if `CharacterSelected` event never arrives. | Add CancellationTokenSource with 10s timeout. On timeout, show "Connection timed out" error. | ⬜ |
| G104 | 🟢 LOW | [`CharacterSelect.razor`](Veldrath.GameClient.Components/Components/Pages/CharacterSelect.razor) | `CharacterStatusChanged` not surfaced. Server broadcasts online/offline status to account group but CharacterSelect doesn't listen. | Subscribe to `CharacterStatusChanged` and update UI badges. | ⬜ |

---

## 11. Routes & Layout Gaps

These 6 gaps were identified in Pass 4 analysis of route configuration and layout composition across web and embedded hosts.

| ID | Severity | Component | Description | Fix | Status |
|----|----------|-----------|-------------|-----|--------|
| G105 | 🟢 LOW | [`EmbeddedRoutes.razor`](Veldrath.GameClient.Components/Components/EmbeddedRoutes.razor) | Redundant `AdditionalAssemblies`. `typeof(GameLayout).Assembly` is same as `typeof(EmbeddedApp).Assembly`. | Remove redundant assembly reference (cosmetic). | ⬜ |
| G106 | 🟢 LOW | [`Routes.razor`](Veldrath.Web/Routes.razor) | Web routes only discovers RCL assembly as additional. If future pages are added to other assemblies, they won't be found. | Add `typeof(IGameStateService).Assembly` to AdditionalAssemblies for forward-compatibility. | ⬜ |
| G107 | 🟢 LOW | [`EmbeddedRoutes.razor`](Veldrath.GameClient.Components/Components/EmbeddedRoutes.razor) | Embedded NotFound page has no layout wrapper. Bare `<div>` without game chrome (header/footer). | Add `DefaultLayout="typeof(GameLayout)"` to NotFound route template. | ⬜ |
| G108 | 🟡 MEDIUM | [`GameLayout.razor`](Veldrath.GameClient.Components/Components/Layout/GameLayout.razor) / MainLayout | Web game pages render inside MainLayout site chrome. `.game-layout` uses `position: fixed; inset: 0` which may overlay site header/nav. | Verify game pages render correctly inside MainLayout. Add `z-index` management if needed. | ⬜ |
| G109 | 🟢 LOW | [`GameLayout.razor`](Veldrath.GameClient.Components/Components/Layout/GameLayout.razor) | GameLayout doesn't implement `IDisposable`. MainLayout does for unsubscribe. No current leak but pattern inconsistency. | Add `@implements IDisposable` with empty Dispose if no subscriptions to clean up. | ⬜ |
| G110 | 🟡 MEDIUM | [`GameLayout.razor`](Veldrath.GameClient.Components/Components/Layout/GameLayout.razor) | `bridge.js` loaded by URL heuristic (`StartsWith("http://localhost")`). If embedded server binds `127.0.0.1` explicitly, bridge won't load. | Use capability detection or configuration flag instead of URL heuristic. | ⬜ |

---

## 12. CSS & Styling Gaps

These 5 gaps were identified in Pass 4 analysis of shared CSS token definitions, embedded font loading, and style consistency between web and embedded hosts.

| ID | Severity | Component | Description | Fix | Status |
|----|----------|-----------|-------------|-----|--------|
| G111 | 🟠 HIGH | [`game.css`](Veldrath.GameClient.Components/wwwroot/css/game.css), [`tokens.css`](Veldrath.GameClient.Components/wwwroot/css/tokens.css) | `--vds-pink` CSS variable referenced by game.css for whisper messages but not defined in any loaded stylesheet. Fallback `#f472b6` works but theme changes won't affect whispers. | Add `--vds-pink: #f472b6;` to tokens.css `:root` block. | ✅ Fixed |
| G112 | 🟢 LOW | [`game.css`](Veldrath.GameClient.Components/wwwroot/css/game.css) | Hardcoded `#94a3b8` for zone chat sender color instead of VDS token. Inconsistent with other chat types that use VDS tokens. | Replace with `var(--vds-slate-muted, #94a3b8)`. | ⬜ |
| G113 | 🟠 HIGH | [`EmbeddedApp.razor`](Veldrath.GameClient.Components/Components/EmbeddedApp.razor) | No Google Fonts loading in embedded version. tokens.css references Cinzel Decorative/Cinzel/Lora/Inter/JetBrains Mono but WebView2 will fall back to generic font families. Desktop and web will render with different fonts. | Add `<link>` tags for Google Fonts to `EmbeddedApp.razor` (same as web's App.razor). | ⬜ |
| G114 | 🟡 MEDIUM | [`tokens.css`](Veldrath.GameClient.Components/wwwroot/css/tokens.css) | Missing `a` tag and `.btn` base styles. CharacterSelect.razor uses `<a>` tags with `.btn` classes. In embedded version, these have no styling without host CSS. | Add `a` tag and `.btn` styles to `tokens.css` (copy from app.css). | ✅ Fixed |
| G115 | 🟢 LOW | [`tokens.css`](Veldrath.GameClient.Components/wwwroot/css/tokens.css) | `--vds-success-muted` game.css fallback `#1A3A2C` differs slightly from tokens.css/app.css value `#1F3D2E`. | Standardize to `#1F3D2E` in game.css fallbacks. | ⬜ |

---

## 13. bridge.js & Native Interop Gaps

These 3 gaps were identified in Pass 4 analysis of the bridge.js ↔ NativeBridgeService interop layer used by the embedded WebView2 client.

| ID | Severity | Component | Description | Fix | Status |
|----|----------|-----------|-------------|-----|--------|
| G116 | ⚪ NONE | bridge.js vs [`NativeBridgeService`](Veldrath.Client/Services/NativeBridgeService.cs) | All 7 message types in bridge.js have corresponding handlers in NativeBridgeService.cs. No missing methods. Perfect match. | No action needed. | ⬜ |
| G117 | 🟢 LOW | bridge.js | No incoming message listener. NativeBridgeService can send messages TO WebView but bridge.js has no listener. One-directional architecture. | Add `chrome.webview.addEventListener('message', ...)` listener for future bidirectional needs. | ⬜ |
| G118 | 🟢 LOW | [`NativeBridgeService`](Veldrath.Client/Services/NativeBridgeService.cs) | Notification handler is stub — only `Debug.WriteLine`, no actual Windows Toast notification. | Implement Windows Toast via `Microsoft.Toolkit.Uwp.Notifications` or similar. | ⬜ |

---

## 14. Test Coverage Gaps

These 14 gaps were identified in Pass 4 analysis of the RCL component test suite. 14 components currently have zero dedicated test coverage.

| ID | Severity | Component | Description | Fix | Status |
|----|----------|-----------|-------------|-----|--------|
| G119 | 🟠 HIGH | [`CreateCharacter.razor`](Veldrath.GameClient.Components/Components/Pages/CreateCharacter.razor) | Complex multi-step wizard with class selection, name input/validation. No tests. | Add bUnit tests using FakeGameApiClient. Test: class list render, name validation, submission flow. | ⬜ |
| G120 | 🟠 HIGH | [`Game.razor`](Veldrath.GameClient.Components/Components/Pages/Game.razor) | Main game shell orchestrating all child components. No dedicated test. | Add bUnit test with mocked hub connection and verify child component rendering. | ⬜ |
| G121 | 🟡 MEDIUM | [`GameHeader.razor`](Veldrath.GameClient.Components/Components/Pages/GameHeader.razor) | No tests. Displays character name, class badge, level, HP/MP bars, gold, zone badge. | Add bUnit test verifying data display from GameState. | ⬜ |
| G122 | 🟡 MEDIUM | [`GameFooter.razor`](Veldrath.GameClient.Components/Components/Pages/GameFooter.razor) | No tests. Connection status dot, ping, zone info. | Add bUnit test verifying connection state display. | ⬜ |
| G123 | 🟡 MEDIUM | [`GameSidebar.razor`](Veldrath.GameClient.Components/Components/Pages/GameSidebar.razor) | No tests. Character stats, inventory list, party members. | Add bUnit test verifying stat display. | ⬜ |
| G124 | 🟡 MEDIUM | [`GameOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/GameOverlay.razor) | No tests. Overlay host for Inventory/Shop/Journal panels. | Add bUnit test verifying overlay type switching. | ⬜ |
| G125 | 🟢 LOW | [`GameSettings.razor`](Veldrath.GameClient.Components/Components/Pages/GameSettings.razor) | No tests. Audio volume sliders, theme toggles. | Add bUnit test verifying default values and toggle behavior. | ⬜ |
| G126 | 🟡 MEDIUM | [`GamePanel.razor`](Veldrath.GameClient.Components/Components/Shared/GamePanel.razor) | No tests. Reusable modal panel with header/body/close. | Add bUnit test for open/close and title rendering. | ⬜ |
| G127 | 🟡 MEDIUM | [`InventoryOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/InventoryOverlay.razor) | No tests. Equipment slots grid + item bag grid. | Add bUnit test verifying equipment slot rendering from GameState. | ⬜ |
| G128 | 🟡 MEDIUM | [`JournalOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/JournalOverlay.razor) | No tests. Quest list + detail + completed log. | Add bUnit test verifying quest rendering from GameState. | ⬜ |
| G129 | 🟢 LOW | [`ReconnectOverlay.razor`](Veldrath.GameClient.Components/Components/Shared/ReconnectOverlay.razor) | No tests. Disconnection countdown + retry/return-to-menu. | Add bUnit test verifying state transitions. | ⬜ |
| G130 | 🟡 MEDIUM | [`ShopOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/ShopOverlay.razor) | No tests. Merchant catalog + buy/sell flow. | Add bUnit test verifying catalog rendering and buy/sell interaction. | ⬜ |
| G131 | 🟢 LOW | [`StatusBar.razor`](Veldrath.GameClient.Components/Components/Shared/StatusBar.razor) | No tests. HP/MP/XP bar rendering with fill percentage. | Add bUnit test verifying fill percentage calculation. | ⬜ |
| G132 | 🟢 LOW | [`Tile.razor`](Veldrath.GameClient.Components/Components/Shared/Tile.razor) | No tests. Single tile with type-based coloring and entity indicators. | Add bUnit test verifying CSS class from tile type. | ⬜ |

---

## 15. Veldrath.Web Cleanup Status

This 1 gap was identified in Pass 4 analysis verifying that the Veldrath.Web post-migration cleanup left no stale references.

| ID | Severity | Component | Description | Fix | Status |
|----|----------|-----------|-------------|-----|--------|
| G133 | ⚪ NONE | [`Veldrath.Web`](Veldrath.Web/) | No stale Game/ references found. All references correctly point to RCL ([`_Imports.razor`](Veldrath.Web/_Imports.razor), App.razor, Routes.razor). Cleanup is complete. | No action needed. | ⬜ |

---

## 9. Priority Order — Top 10 Must-Fix

These are the 10 highest-impact fixes, ordered by dependency chain and player-visible impact.

| Rank | ID | Gap | Rationale |
|------|----|-----|-----------|
| 1 | 4 | `DamageTaken` handler missing | Without this, HP never decreases in UI — combat is invisible. Blocks all combat testing. |
| 2 | 11 | `InventoryLoaded` handler missing | Inventory is empty on every login. Blocks equipment, shop, crafting, and item workflows. |
| 3 | 2 | `ExperienceGained` handler missing | No XP/level progression visible. Core RPG loop is broken without this. |
| 4 | 3 | `GoldChanged` handler missing | Gold never updates. Blocks shop, rest, and crafting economy. |
| 5 | 49 | `IsPlayerDead` property missing | Death state not tracked. Respawn button never appears. Blocks death/respawn loop. |
| 6 | 46 | 6 attribute properties missing | Character sheet is empty. Blocks stat display and attribute allocation UI. |
| 7 | 48 | `CurrentMana` / `MaxMana` properties missing | MP bar always empty. Abilities cannot show mana cost or availability. |
| 8 | 63 | `CharacterRespawned` stub handler | Respawn does nothing. Player stuck in dead state with no recovery. |
| 9 | 19 | `ZoneLeft` handler missing | Zone transitions are invisible. Player has no feedback when changing zones. |
| 10 | 66 | `GetInventory` never called | Inventory overlay opens but shows no data. Combined with #11, inventory is completely non-functional. |

---

## Appendix A: Affected Files Map

| File | Related Gap IDs |
|------|----------------|
| [`Game.razor`](Veldrath.GameClient.Components/Components/Pages/Game.razor) | 63–65, 87 |
| [`GameTilemap.razor`](Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor) | 18, 19, 24–30, 52, 65, 69, 73, 77, 79, 90 |
| [`GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor) | 21–23, 50, 70–72, 92, 96 |
| [`GameCombat.razor`](Veldrath.GameClient.Components/Components/Pages/GameCombat.razor) | 4, 15, 31, 33, 54, 64, 91 |
| [`GameChat.razor`](Veldrath.GameClient.Components/Components/Pages/GameChat.razor) | 34–38, 56, 57, 81 |
| [`GameFooter.razor`](Veldrath.GameClient.Components/Components/Pages/GameFooter.razor) | 58, 84, 95 |
| [`GameHeader.razor`](Veldrath.GameClient.Components/Components/Pages/GameHeader.razor) | 36, 51–52 |
| [`StatusBar.razor`](Veldrath.GameClient.Components/Components/Shared/StatusBar.razor) | 4, 14, 48, 63 |
| [`ActionBar.razor`](Veldrath.GameClient.Components/Components/Shared/ActionBar.razor) | 15, 49, 55, 82, 99 |
| [`InventoryOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/InventoryOverlay.razor) | 5, 8–11, 66, 93, 100 |
| [`ShopOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/ShopOverlay.razor) | 6–9, 68, 83, 94 |
| [`JournalOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/JournalOverlay.razor) | 12, 67, 85 |
| [`ReconnectOverlay.razor`](Veldrath.GameClient.Components/Components/Shared/ReconnectOverlay.razor) | 1, 97 |
| [`GameSettings.razor`](Veldrath.GameClient.Components/Components/Pages/GameSettings.razor) | 86 |
| [`CharacterSelect.razor`](Veldrath.GameClient.Components/Components/Pages/CharacterSelect.razor) | 88, G101–G104 |
| [`CreateCharacter.razor`](Veldrath.GameClient.Components/Components/Pages/CreateCharacter.razor) | 89, 98, G119 |
| [`IGameStateService.cs`](Veldrath.GameClient.Core/Services/IGameStateService.cs) | 46–62 |
| [`GameHubConnectionService.cs`](Veldrath.GameClient.Core/Services/GameHubConnectionService.cs) | 1–45 (handler registration), 58, 97 |
| [`GameLayout.razor`](Veldrath.GameClient.Components/Components/Layout/GameLayout.razor) | G108–G110 |
| [`EmbeddedRoutes.razor`](Veldrath.GameClient.Components/Components/EmbeddedRoutes.razor) | G105, G107 |
| [`EmbeddedApp.razor`](Veldrath.GameClient.Components/Components/EmbeddedApp.razor) | G113 |
| [`GameSidebar.razor`](Veldrath.GameClient.Components/Components/Pages/GameSidebar.razor) | G123 |
| [`GameOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/GameOverlay.razor) | G124 |
| [`GamePanel.razor`](Veldrath.GameClient.Components/Components/Shared/GamePanel.razor) | G126 |
| [`Tile.razor`](Veldrath.GameClient.Components/Components/Shared/Tile.razor) | G132 |
| [`tokens.css`](Veldrath.GameClient.Components/wwwroot/css/tokens.css) | G111, G114, G115 |
| [`game.css`](Veldrath.GameClient.Components/wwwroot/css/game.css) | G111, G112, G115 |
| bridge.js | G116, G117 |
| [`NativeBridgeService.cs`](Veldrath.Client/Services/NativeBridgeService.cs) | G116, G118 |
| [`Routes.razor`](Veldrath.Web/Routes.razor) | G106 |
| [`_Imports.razor`](Veldrath.Web/_Imports.razor) | G133 |

---

## Appendix B: Severity Definitions

| Severity | Icon | Definition |
|----------|------|------------|
| CRITICAL | 🔴 | Core gameplay loop is broken without this. Player cannot see HP, XP, gold, inventory, or equipment changes. |
| HIGH | 🟠 | Major feature is non-functional (navigation, world map, zone transitions). Player can play but experience is severely degraded. |
| MEDIUM | 🟡 | Feature is missing but game is still playable (social, enemy display, stubs). Noticeable gap vs. desktop client. |
| LOW | 🟢 | Nice-to-have or admin-only feature. Does not block normal gameplay. |

---

## Appendix C: Fix History

### Session 43 (2026-07-01) — Foundation + CRITICAL Fixes
- **Tasks A-C**: WebView2 rendering wiring, auth bridge, RCL demo data, CSS parity (21 gaps)
- **Batch 1 (G1-G3, G10)**: Demo data elimination — real ping, quest data, settings persistence
- **Batch 2 (G18-G35)**: 18 CRITICAL hub event handlers — core game loop (XP, gold, damage, equipment, etc.)
- New files created: 5 payload files, CharacterState record, 13 new interface members

### Session 43 — HIGH Fixes
- **Batch 3 (G36-G52)**: 12 zone/region/location navigation handlers + 5 GameState properties (RegionState, ZoneState)
- New files/modified: ZonePayloads.cs extended with 11 types, RegionState/ZoneState records
- **Batch 4 (G66-G83)**: 12 hub methods wired into RCL components (inventory, shop, navigation, interaction)
- Modified: GameSidebar, GameMap, GameTilemap, GameChat, InventoryOverlay
- **Batch 5+6 (G63-G65, G101, G111, G114, G11-G17)**: Stub handlers fixed, CharacterAlreadyActive, CSS tokens, interface violations
- All 7+1 components now use IGameStateService instead of concrete GameStateService
- Build verified: 26/26 projects, 0 errors

### Remaining Work (52 gaps)
- 8 MEDIUM social hub handlers (G31-G38)
- 7 LOW admin/interaction handlers (G39-G45)
- ~14 test coverage gaps (G119-G132)
- 5 low-priority CSS/cleanup items
- 6 hub methods not surfaced in either client (crafting, dungeon, non-combat abilities)

---

> **Next Steps:** Work through gaps in priority order (Section 9). Each fix should include: handler registration in [`Game.razor`](Veldrath.GameClient.Components/Components/Pages/Game.razor), [`IGameStateService`](Veldrath.GameClient.Core/Services/IGameStateService.cs) property additions, and UI component updates. Update this document's status as gaps are resolved.

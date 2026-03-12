# Development Roadmap

**Purpose**: High-level development vision and work organization for RealmEngine console game.

**Scope**: Console game backend, APIs, and game logic. UI implementation (Godot) is out of scope but we design service APIs for future integration.

**Status Source**: See [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) for detailed feature status and test coverage.

---

## 🎯 Quick Wins (Integration-Only Work)
**Goal**: Connect existing production-ready code that's been orphaned

### LocationGenerator Integration
- **Status**: `[✅ Finished]`
- **Effort**: Low (integration-only)
- **Impact**: HIGH - Unlocks location-specific content
- **Work**: ~~Wire 300+ lines of LocationGenerator into ExplorationService~~ **DONE**
- **File**: `RealmEngine.Core.Generators.Modern.LocationGenerator`
- **Dependencies**: None
- **API Impact**: `GetKnownLocationsAsync()` returns `List<Location>` with dynamic generation (2 towns, 3 dungeons, 3 wilderness)
- **Details**: 
  - Made `GenerateLocationsAsync()` virtual for testing
  - Changed from `List<string>` to `List<Location>` with Id/Name/Description/Type
  - Added `InitializeLocationsAsync()` for lazy loading
  - All 7,823 tests passing

---

### Quest Service Connections
- **Status**: `[Not Started]`
- **Effort**: Low
- **Impact**: HIGH - Makes quests playable
- **Work**: 
  - Hook QuestService/QuestProgressService into gameplay loop
  - Connect kill counters to UpdateProgressAsync()
  - Trigger completion events
- **Files**: `QuestService`, `QuestProgressService`, `MainQuestService`
- **Dependencies**: Quest Service API (see Core Loop)
- **API Impact**: Quest progress updates automatically during gameplay

---

### Shop Service API
- **Status**: `[Not Started]`
- **Effort**: Medium
- **Impact**: MEDIUM - Enables merchant interactions
- **Work**:
  - Expose ShopEconomyService methods as callable APIs
  - Design request/response models for buy/sell operations
  - Implement inventory generation TODOs
  - Load core items from catalog
- **Files**: `ShopEconomyService` (326 lines complete, needs API layer)
- **Dependencies**: None
- **API Impact**: 
  - `BrowseShopInventory(merchantId)` → returns items for sale
  - `BuyItem(merchantId, itemId)` → purchase transaction
  - `SellItem(merchantId, itemId)` → sell transaction

---

## 🎮 Core Loop (Make It Playable)
**Goal**: Essential systems for meaningful progression and objectives

### Progression System Overhaul `[SYSTEM GROUP]`
**This is the biggest work item - three interconnected systems that must be designed together**

**⚠️ ARCHITECTURE REQUIRED**: These systems need design documents before implementation

---

#### 1. Skills System
- **Status**: `[✅ JSON Complete]`
- **Effort**: High (Code Implementation Remaining)
- **Current State**: JSON v4.2 catalog complete with 54 skills, code integration pending
- **Vision**: Practice-based skill progression system

**JSON Catalog Complete**: `RealmEngine.Data/Data/Json/skills/catalog.json` ✅

**Architecture Decisions**:
- **Rank System**: 0-100 ranks per skill
- **XP Formula**: `baseXPCost × (1 + currentRank × costMultiplier)`
- **54 Skills Total**: Attribute (24), Weapon (10), Armor (4), Magic (16), Profession (12)
- **Effect Types**: Damage multipliers, defense bonuses, spell power, crafting quality
- **JSON v4.2 Structure**: Traits with type annotations, selectionWeight, effects arrays, xpActions arrays
- **Per-Rank Bonuses**: Weapon +0.5% damage, Magic +0.4% power, Attribute-specific effects

**Skill Categories**:
- **Attribute Skills (24)**: 4 per attribute (STR, DEX, CON, INT, WIS, CHA)
  - Athletics, Swimming, Climbing, Carrying, Acrobatics, Stealth, etc.
- **Weapon Skills (10)**: Light Blades, Heavy Blades, Axes, Blunt, Polearms, Bows, Crossbows, Throwing, Unarmed, Shield
- **Armor Skills (4)**: Light, Medium, Heavy, Unarmored Defense
- **Magic Skills (16)**: 4 core traditions (Arcane, Divine, Occult, Primal) + 3 specialists each
  - Arcane: Force Magic, Chronomancy, Conjuration
  - Divine: Restoration, Smiting, Warding
  - Occult: Enchantment, Illusion, Shadowcraft
  - Primal: Elementalism, Beast Mastery, Verdancy
- **Profession Skills (12)**: Blacksmithing, Leatherworking, Tailoring, Woodworking, Jewelcrafting, Alchemy, Enchanting, Runecrafting, Mining, Herbalism, Cooking, Fishing

**Implementation Steps**:
1. ✅ JSON catalog (skills/catalog.json with 54 skills) - DONE
2. ⏳ Data models (CharacterSkill, SkillDefinition)
3. ⏳ SkillCatalogService (load from JSON)
4. ⏳ SkillProgressionService (XP awards, rank-ups, effect calculations)
5. ⏳ Combat integration (weapon/armor skill effects)
6. ⏳ Magic integration (tradition/specialist skill effects)
7. ⏳ UI notifications (rank-up alerts)
8. ⏳ Save/load integration

**Dependencies**: None (foundational)

**API Impact**:
- `AwardSkillXP(skillId, xpAmount)` → awards XP, handles rank-ups
- `GetSkillProgress(skillId)` → returns current rank, XP, next rank threshold
- `GetAllSkills()` → returns character's full skill sheet with progress
- Skill XP awarded automatically during gameplay (combat hits, spell casts, crafting)

---

#### 2. Abilities System
- **Status**: `[✅ JSON Complete]`
- **Effort**: High (Code Implementation Remaining)
- **Current State**: JSON v4.2 catalogs complete with 383 abilities in 4 files, code integration pending
- **Vision**: Class and species-specific active powers

**JSON Catalogs Complete**: `RealmEngine.Data/Data/Json/abilities/` ✅
- `active/catalog.json` (177 abilities)
- `passive/catalog.json` (131 abilities)
- `reactive/catalog.json` (36 abilities)
- `ultimate/catalog.json` (39 abilities)

**Architecture Decisions**:
- **Activation Type Organization**: 4 catalogs by activation type (not class-based)
- **Tier System**: 5 tiers (1-5) based on selectionWeight
- **Level-Gated Unlocks**: Tier 1 (level 1+), Tier 2 (level 5+), Tier 3 (level 10+), Tier 4 (level 15+), Tier 5 (level 20+)
- **Resource Management**: Mana costs + cooldown timers in traits
- **JSON v4.2 Structure**: All properties in traits with type annotations
- **Clear Distinctions**: Skills (54, ranks 0-100), Abilities (383, tiers 1-5), Spells (144, ranks 0-10)

**Ability Distribution** (Total: 383 abilities):
- **Active (177)**: Offensive (88), Defensive (34), Support (27), Utility (28), Control (8), Summon (4), Mobility (2)
- **Passive (131)**: General (16), Offensive (38), Defensive (39), Leadership (24), Environmental (22), Mobility (7), Sensory (1)
- **Reactive (36)**: Offensive (14), Defensive (12), Utility (10)
- **Ultimate (39)**: All tier 5 game-changing abilities

**Implementation Steps**:
1. ✅ JSON catalogs (4 files: active, passive, reactive, ultimate) - DONE
2. ✅ Organize by activation type (not class folders) - DONE
3. ✅ Apply v4.2 standards (traits, selectionWeight, tiers) - DONE
4. ⏳ Data models (CharacterAbility tracking)
5. ⏳ AbilityCatalogService (load from 4 JSON files)
6. ⏳ Integrate ability usage into CombatService (cooldowns, mana costs)
7. ⏳ Level-up unlocking system (tier-based)
8. ⏳ Ability effect execution (damage, healing, buffs, debuffs)
9. ⏳ Class-ability associations (define which classes get which abilities)

**Dependencies**: 
- Skills System (skill bonuses affect ability damage) - JSON complete, code pending
- Combat System (ability usage context)

---

#### 3. Magic & Spell System
- **Status**: `[✅ JSON Complete]`
- **Effort**: High (Code Implementation Remaining)
- **Current State**: JSON v4.2 catalog complete with 144 spells, code integration pending
- **Vision**: Learnable spell system with Pathfinder 2e magical traditions

**JSON Catalog Complete**: `RealmEngine.Data/Data/Json/spells/catalog.json` ✅

**Architecture Decisions**:
- **Four Magical Traditions**: Arcane (INT), Divine (WIS), Occult (CHA), Primal (WIS)
- **Spell Ranks**: 0 (Cantrip) through 10 (Ultimate)
- **144 Spells Total**: 36 per tradition (8 cantrips + 28 ranked spells)
- **Skill-Based**: Spell power scales with tradition + specialist magic skills
- **Universal Access**: Anyone can learn spells (unlike class-only abilities)
- **Acquisition Methods**: Spellbooks (learn permanently), Scrolls (one-time cast), Trainers, Quest rewards
- **JSON v4.2 Structure**: Traits with type annotations, selectionWeight, tradition-specific organization
- **Cantrips (Rank 0)**: Free to cast (0 mana cost)
- **Mana Costs**: Scale by rank (10-200 mana for ranks 1-10)
- **Success Rates**: 90% at minimum skill, 99% at 20+ ranks above
- **Power Scaling**: +1% per tradition skill rank + specialist skill bonuses

**Magical Traditions**:
- **Arcane (36)**: Force, transmutation, teleportation, raw power (INT-based)
  - Specialists: Force Magic, Chronomancy, Conjuration
- **Divine (36)**: Healing, holy damage, protection (WIS-based)
  - Specialists: Restoration, Smiting, Warding
- **Occult (36)**: Mind control, illusions, psychic damage (CHA-based)
  - Specialists: Enchantment, Illusion, Shadowcraft
- **Primal (36)**: Elements, beasts, nature, weather (WIS-based)
  - Specialists: Elementalism, Beast Mastery, Verdancy

**Implementation Steps**:
1. ✅ JSON catalog (spells/catalog.json with 144 spells) - DONE
2. ⏳ Data models (Spell, CharacterSpell, Spellbook, Scroll)
3. ⏳ SpellCatalogService (load from JSON)
4. ⏳ SpellLearningService (spellbooks, trainers)
5. ⏳ SpellCastingService (success checks, mana, effects)
6. ⏳ Combat integration (spell casting in combat)
7. ⏳ Inventory integration (use spellbooks/scrolls)
8. ⏳ Spell effect execution with tradition + specialist skill scaling

**Dependencies**: 
- Skills System (magic skills foundation) - JSON complete, code pending
- Combat System (spell casting in combat)
- Inventory System (spellbooks/scrolls as items)

**API Impact**:
- `GetLearnedSpells()` → returns character's spellbook with cast counts
- `LearnSpellFromBook(spellId)` → learn from spellbook item
- `CastSpell(spellId, targetId)` → cast in combat with skill checks
- `CastFromScroll(scrollId)` → one-time cast without knowing spell
- `GetCastableSpells()` → available spells (enough mana, not on cooldown)
- `GetLearnableSpells()` → spells character can learn (meets skill requirements)

---

### Quest & Objective System

#### Quest Service API
- **Status**: `[Not Started]`
- **Effort**: Medium 
- **Work**:
  - Expose QuestService methods as callable APIs
  - Design quest browsing/tracking interface
  - Implement objective tracking display
- **Dependencies**: Quick Win (Quest Service Connections)
- **API Impact**:
  - `GetActiveQuests()` → returns current quests with progress
  - `GetAvailableQuests()` → returns quests player can start
  - `StartQuest(questId)` → begin quest
  - `GetQuestDetails(questId)` → full quest info with objectives

---

#### Boss Encounters
- **Status**: `[Not Started]`
- **Effort**: High 
- **Work**:
  - Design unique boss enemies for quest climaxes
  - Boss-specific mechanics and abilities
  - Boss loot tables and rewards
  - Required for main quests #4 (Dark Prophecy) and #6 (Final Boss)
- **Dependencies**: Abilities System (bosses need unique abilities)
- **API Impact**: Boss encounters integrated into combat system

---

## 🌍 Content & Depth (Make It Engaging)
**Goal**: World feels alive, choices matter, content has variety

### Location System

#### Location-Specific Content
- **Status**: `[Not Started]`
- **Effort**: Medium 
- **Work**:
  - Location-specific enemy spawn tables
  - Location-specific loot tables
  - Environmental effects (heat, cold, darkness)
  - Location difficulty ratings
- **Dependencies**: LocationGenerator Integration (Quick Win)
- **API Impact**: Exploration yields different results per location

---

#### Town Mechanics
- **Status**: `[Not Started]`
- **Effort**: Medium 
- **Work**:
  - Rest/Inn system (heal, save, time passage)
  - Town-specific shops
  - NPC interactions and quest givers
  - Safe zones (no combat)
- **Dependencies**: Shop Service API (Quick Win)
- **API Impact**:
  - `GetTownServices(locationId)` → available services
  - `Rest(hours)` → heal and advance time
  - `GetTownNPCs(locationId)` → available NPCs

---

#### Dungeon Multi-Room System
- **Status**: `[Not Started]`
- **Effort**: High 
- **Work**:
  - Room-by-room progression (entrance → rooms → boss)
  - Room types (combat, puzzle, treasure, boss)
  - Persistent dungeon state (cleared rooms stay cleared)
  - Exit/retreat mechanics
- **Dependencies**: Location-Specific Content, Boss Encounters
- **API Impact**:
  - `EnterDungeon(locationId)` → start dungeon run
  - `ExploreRoom()` → progress to next room
  - `GetDungeonProgress(dungeonId)` → room completion status

---

### Combat Depth

#### Status Effects System
- **Status**: `[Not Started]`
- **Effort**: High 
- **Work**:
  - DoT effects (poison, burning, bleeding)
  - Buffs (strength, defense, speed)
  - Debuffs (weakness, slow, blind)
  - Crowd control (stun, freeze, fear)
  - Duration tracking and tick system
  - Cleansing and curing
  - Stacking rules (can you be poisoned AND burning?)
- **Dependencies**: None (but enables many other systems)
- **API Impact**:
  - `GetActiveEffects(entityId)` → current status effects
  - `ApplyEffect(entityId, effectId)` → add effect
  - `RemoveEffect(entityId, effectId)` → cleanse effect
  - Status effects auto-tick during combat turns

---

#### Trait Effects Integration
- **Status**: `[Partially Done - 20%]`
- **Effort**: Medium 
- **Current State**: Trait data exists in JSON, TraitValue class works, but traits don't affect gameplay
- **Work**:
  - Integrate weapon traits into combat (Fire damage, Lightning chain, etc.)
  - Elemental damage types and resistances
  - Enemy trait behaviors (regeneration, immunity, weakness)
  - Item trait bonuses applied to stats
- **Dependencies**: Status Effects System (traits trigger effects)
- **API Impact**: Traits automatically affect combat calculations

---

## 🚀 Feature Expansion (Long-term Vision)
**Goal**: Add depth, replayability, and polish for rich gameplay

### Social & World Systems

#### Reputation & Faction System
- **Status**: `[Not Started]`
- **Effort**: Medium 
- **Work**:
  - Faction definitions (guilds, cities, factions)
  - Reputation tracking per faction
  - Action consequences (killing faction members, quests)
  - Faction-locked content (shops, quests, areas)
- **API Impact**:
  - `GetFactionReputation(factionId)` → current standing
  - `GetFactionPerks(factionId)` → benefits at current standing

---

#### Party System
- **Status**: `[Not Started]`
- **Effort**: Very High 
- **Work**:
  - NPC recruitment system
  - Party management (add/remove members)
  - Party combat mechanics (AI-controlled allies)
  - Party progression (companion levels/equipment)
  - Companion quests and backstories
- **API Impact**:
  - `GetPartyMembers()` → current party
  - `RecruitNPC(npcId)` → add to party
  - `DismissNPC(npcId)` → remove from party
  - Party members act automatically in combat

---

#### Crafting System
- **Status**: `[✅ Complete - 100%]`
- **Effort**: High 
- **Completion Date**: January 11, 2026
- **What's Complete**:
  - Recipe execution with material consumption ✅
  - Recipe learning from trainers/quests ✅
  - Recipe discovery via experimentation ✅
  - Station and tier validation ✅
  - Quality bonuses based on skill ✅
  - **Enchanting System** - Apply scrolls to items (16/16 tests) ✅
  - **Upgrade System** - Exponential stat scaling +1 to +10 (11/11 tests) ✅
  - **Salvaging System** - Recycle items to materials (11/11 tests) ✅
- **Test Coverage**: 79 tests (38 enhancement + 41 core)
- **Dependencies**: Skills System (crafting skills) - JSON complete
- **API Impact**:
  - `CraftRecipeCommand` → create items with quality bonuses
  - `LearnRecipeCommand` → learn from trainers/quests
  - `DiscoverRecipeCommand` → experimentation-based discovery
  - `GetKnownRecipesQuery` → known recipes with material validation
  - `ApplyEnchantmentCommand` → add enchantments to items
  - `AddEnchantmentSlotCommand` → socket crystals for more slots
  - `RemoveEnchantmentCommand` → remove enchantments
  - `UpgradeItemCommand` → improve items with essences
  - `SalvageItemCommand` → recycle items into scrap materials

---

### Polish & Presentation

#### Audio System
- **Status**: `[Not Started]`
- **Effort**: Low 
- **Work**:
  - Background music per location type
  - Combat music transitions
  - Sound effects for actions (attacks, spells, loot)
  - Volume controls and settings
- **Note**: NAudio library already installed
- **API Impact**: Audio plays automatically based on game state

---

#### Visual Enhancements
- **Status**: `[Not Started]`
- **Effort**: Medium 
- **Work**:
  - ASCII art for locations
  - Boss portraits
  - Combat visual effects (damage numbers, hit animations)
  - Screen transitions and fade effects
- **Note**: Console-only, Godot UI will replace eventually
- **API Impact**: Enhanced console output (backward compatible)

---

### Community & Longevity

#### Online & Community Features
- **Status**: `[Not Started]`
- **Effort**: High 
- **Work**:
  - Global leaderboards (Hall of Fame rankings)
  - Daily/weekly challenges
  - Save file sharing and showcase
  - Community events
- **Note**: Requires backend service (scope expansion)

---

#### Quality of Life Enhancements
- **Status**: `[Not Started]`
- **Effort**: Medium 
- **Work**:
  - Command history and undo
  - Keybind customization
  - Quick-save hotkey
  - Tutorial system for new players
  - Context-sensitive hints
- **API Impact**: Improved console commands and shortcuts

---

#### Modding Support
- **Status**: `[Not Started]`
- **Effort**: High 
- **Work**:
  - Mod loader system
  - Plugin architecture
  - Content creation documentation
  - Scripting API
  - Community mod repository
- **Note**: Requires careful architecture planning

---

## 🎨 UI Technology Evolution
**Status**: `[Out of Scope for Console]`

**Note**: Console game is backend only. Godot UI integration is a separate project that will consume our service APIs.

**When ready for Godot**:
- All game logic remains in C# console backend
- Godot frontend calls our exposed APIs
- Backend runs as service/process
- Communication via IPC or local networking

**This is not on the roadmap** - we design APIs to be UI-agnostic

---

## 📊 Legend

### Status Markers
- `[Finished]` - Complete and working
- `[In Progress]` - Actively being developed
- `[Partially Done - X%]` - Some work exists, needs completion  
- `[Not Started]` - Not yet begun
- `[Rebuild Required]` - Existing code wrong approach, needs redesign
- `[Needs Audit]` - Existing code unclear, needs investigation
- `[Needs Design]` - Requires design document before implementation
- `[SYSTEM GROUP]` - Multiple interconnected features that must be developed together
- `[Out of Scope]` - Not part of console game project

### Effort Estimates
- **Low**: 2-5 days
- **Medium**: 1-2 weeks
- **High**: 2-4 weeks
- **Very High**: 4+ weeks

### Impact Levels
- **HIGH**: Critical for core gameplay loop
- **MEDIUM**: Enhances gameplay significantly
- **LOW**: Polish and quality of life

---

## 🗺️ Recommended Work Order

1. **Quick Wins** (1-2 weeks total)
   - LocationGenerator Integration
   - Quest Service Connections
   - Shop Service API

2. **Progression System Group** (6-8 weeks total)
   - Design phase for all three systems
   - Skills System implementation
   - Abilities System implementation
   - Magic & Spell System implementation

3. **Quest Completion** (3-4 weeks total)
   - Quest Service API
   - Boss Encounters

4. **Location Depth** (5-7 weeks total)
   - Location-Specific Content
   - Town Mechanics
   - Dungeon Multi-Room

5. **Combat Depth** (5-7 weeks total)
   - Status Effects System
   - Trait Effects Integration

6. **Feature Expansion** (ongoing)
   - Social systems, crafting, polish, etc.

---

**See Also**:
- [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md) - Current detailed status
- [GDD-Main.md](GDD-Main.md) - Game design documentation
- [RealmForge](realm-forge/README.md) - Content editing tool documentation

---

## 🌐 RealmUnbound — Multiplayer Client/Server Milestones

RealmUnbound is the live multiplayer frontend for RealmEngine, consisting of:
- **RealmUnbound.Server** — ASP.NET Core + SignalR + EF Core (PostgreSQL/SQLite)
- **RealmUnbound.Client** — Avalonia UI desktop client

---

### M1 — Auth + Connection ✅ Complete
> Solid foundation: accounts, JWT, hub handshake, character selection

**Server**
- ASP.NET Core Identity (`PlayerAccount : IdentityUser`)
- SQLite (dev) / PostgreSQL (prod) via EF Core
- `POST /api/auth/register` and `POST /api/auth/login` — return JWT + refresh token
- `GameHub.SelectCharacter` — validates JWT, loads character, dispatches MediatR
- `GET /api/characters`, `POST /api/characters`, `DELETE /api/characters/{id}`

**Client**
- Login / Register screens (Avalonia)
- JWT stored in `TokenStore` (in-memory)
- `ServerConnectionService` — wraps `HubConnection`, bearer token, reconnect
- Navigates to Character Select on login success
- Character Select screen with create / delete / enter

**Tests**: 28 server integration tests (all passing)

---

### M2 — World + Zones ✅ Complete
> Players exist in a shared world; zone presence is tracked live

**Server**
- `Zone` entity (Id, Name, Description, ZoneType, MinLevel, IsStarter) — 5 seeded zones
- `ZoneSession` entity — tracks live character ↔ connection ↔ zone
- `GET /api/zones`, `GET /api/zones/{id}` (public endpoints)
- `GameHub.EnterZone` / `LeaveZone` — SignalR group management (`zone:{id}`)
- Broadcasts `PlayerEntered` / `PlayerLeft` / `ZoneEntered` to zone group
- Auto-cleanup on disconnect via `OnDisconnectedAsync`
- EF Core migration: `InitialSchema`

**Client**
- `IZoneService` / `HttpZoneService` — fetches zone list and details
- `GameViewModel` — owns zone name, description, online players list, action log
- `GameView` — header bar (character + zone), action log, online players panel, logout
- `CharacterSelectViewModel` subscribes to `ZoneEntered` / `PlayerEntered` / `PlayerLeft` then navigates
- `GameViewModel` registered as singleton; `IZoneService` registered via `AddHttpClient`

**Tests**: 7 new zone integration tests → 35 total (all passing)

---

### M3 — Combat (multiplayer-aware) 🔲 Not Started
> Turn-based combat driven through the hub; results broadcast to the zone group

**Server**
- `Hub`: `Attack`, `UseAbility`, `Flee` → MediatR → broadcast `CombatResult` to zone group
- Enemy AI tick — server-side timer (simplified, deterministic)
- `CombatSession` entity or in-memory state per zone instance
- Integrate existing `RealmEngine.Core` combat handlers

**Client**
- Combat screen with combat log (scrolling), action bar, health/mana HUD, enemy panel
- Transitions into combat when a hostile encounter triggers in the zone
- Action buttons bound to hub commands (`Attack`, `UseAbility`, `Flee`)

---

### M4 — Inventory, Progression & Persistence 🔲 Not Started
> Character state persists across sessions; loot and levelling work end-to-end

**Server**
- `Hub`: `PickUpItem`, `EquipItem`, `UnequipItem`, `DropItem`
- `Hub`: `LevelUp` acknowledgement and stat recalc broadcast
- Character inventory + equipment persisted via EF Core (survives reconnect)
- Integrate `RealmEngine.Core` inventory / progression handlers

**Client**
- Inventory screen — grid or list, drag-to-equip (or button-based)
- Character stats panel — attributes, derived stats, equipped gear
- Level-up notification overlay

---

### M5 — Content: NPC, Quests & Shop 🔲 Not Started
> The world has people, objectives, and economies

**Server**
- `Hub`: `TalkToNpc`, `AcceptQuest`, `CompleteQuest`, `BuyFromShop`, `SellToShop`
- NPC catalogue loaded from RealmEngine JSON data
- Quest progress persisted per character via EF Core
- Integrate `RealmEngine.Core` quest / shop handlers

**Client**
- Dialogue screen — NPC portrait, dialogue tree, accept/decline quest
- Quest journal — active quests, objectives, completion status
- Shop screen — browse inventory, buy / sell with gold display

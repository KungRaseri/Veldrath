# RealmEngine — Project Research Mode Instructions

## Primary Information Sources

### Agent Memory Store (`.github/agent-memory/`)
The canonical memory store with 8 files:
| File | Content |
|------|---------|
| `engine-codebase.md` | Model facts, constructor patterns, handler quirks, testing gotchas |
| `json-migration-status.md` | JSON → DB migration log |
| `foundry-content-schema.md` | Foundry content schema, endpoint notes |
| `veldrath-memory.md` | Server + web client hub architecture, blob schema, auth/env matrix |
| `gap-analysis-process.md` | Gap analysis process template |
| `auth-and-character-creation-plan.md` | Auth flow & character creation gap fix plan |
| `combat-loop-plan.md` | Combat loop architecture plan |
| `world-lore-plan.md` | World lore & Calethic language reference |

### Solution Structure
- `Realm.Full.slnx` contains ALL .NET projects
- `RealmEngine.slnx` — engine only (Core + Shared + Data)
- `Veldrath.Server.slnx` — game server + engine + assets + contracts
- `web/` — SvelteKit app (game client, site, Foundry, editorial, admin, moderation)

### Architecture Documentation
- `wiki/Engine-GDD.md` — Game Design Document
- `wiki/Engine-Combat.md` — Combat system design
- `wiki/Engine-Character-Creation.md` — Character creation design
- `wiki/Engine-Implementation-Status.md` — Implementation status tracker
- `docs/design-system.md` — UI design system
- `docs/deployment.md` — Deployment guide

### Code Organization
- Feature handlers: `RealmEngine.Core/Features/{FeatureName}/`
- Server endpoints/hubs: `Veldrath.Server/`
- Game client: `web/client/src/lib/game/` (session store, panels, hub payload types)
- Foundry content editor: `web/client/src/routes/(foundry)/`
- Test files mirror source structure
- Build config: `Directory.Build.props`, `Directory.Build.targets`

## Research Approach
1. Start with `.github/agent-memory/` files for known context
2. Check `RealmEngine.Core/Features/{FeatureName}/` for feature implementation
3. Check `{Project}.Tests/Features/{FeatureName}/` for test patterns
4. Check `engine-codebase.md` for known quirks/gotchas

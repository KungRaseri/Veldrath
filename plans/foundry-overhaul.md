# Foundry overhaul (Milestone H)

> **Status: gap analysis and plan. Nothing implemented.**
> G (the map editor) shipped the authoring surface but not a portal around it. This plan is the
> end-to-end review the portal has never had: layout, navigation, features, forms, and the access
> story. Milestone G is the record of the *editor*; this is the record of the *place*.

---

## 1. What is actually there today

### 1.1 Routes

| Route | Guard | What it is | Problems |
|---|---|---|---|
| `/content` | **session only** — no page guard | Grid of content-type cards | No way to create anything; no counts; no search |
| `/content/[type]` | **session only** — no page guard | Paged table of one type | No "New" action; no filters beyond paging |
| `/content/[type]/[slug]` | **session only** — the API it reads is anonymous | Detail, **payload as `<pre>` JSON** | The main raw-JSON surface |
| `/content/[type]/[slug]/edit` | session + `content.manage` for the actions | Schema-driven form + validate + deactivate | Scalar fields only; issues listed, not anchored |
| `/languages` | session only | A second view of `/content/language` | Duplicates a content type; links out to `/content/language/*` |
| `/languages/new` | `content.manage` | The **only** create form in the portal | One of 20+ types has a create form |
| `/maps` | `content.view` | Map list, state badges, create form | Reachable only from the sub-nav |
| `/maps/[mapId]` | `content.view` | Canvas, metadata, inspector, edges, lifecycle verbs | Not linked from anything else |
| `/submissions` | any session | Community submission queue + filters | **This is the Foundry front door** |
| `/submissions/[id]` | any session | Review, vote; `id = new` is a create form | **Create posts a hand-typed JSON textarea** |
| `/notifications` | any session | Review outcomes | No unread count anywhere else |

**There is no `(foundry)/+page.svelte`.** The group has no home, no `+error.svelte`, and no loading
states.

### 1.2 What the server already supports

The API is ahead of the UI, which changes the shape of the work:

- `POST /api/content/{type}` — **create any content type** (validated, `content.manage`).
- `PUT /api/content/{type}/{slug}` — update with `expectedUpdatedAt`.
- `DELETE /api/content/{type}/{slug}` — deactivate (soft delete); the edit page already calls it.
- `GET /api/content/schema` and `/schema/{type}` — the field descriptors the forms render from.
- `POST /api/foundry/submissions`, `…/vote`, `…/review` — the submission pipeline.
- `POST /api/content/maps`, `…/{id}/submit|review|deactivate`, `PUT`, `DELETE` — the map lifecycle.
- `GET /api/content/maps/targets` — every map, zone owner, location, region and node for pickers.
- `ContentAuditService` — world-health rules with **no UI at all**.

`createContent` already exists in [`content-api.ts`](../web/client/src/lib/server/content-api.ts:96) and is used by exactly one form.

---

## 2. Gap analysis

### 2.1 Layout

There is no frame. Every page is a `mx-auto max-w-… space-y-4 p-6` div of its own choosing:

| Page | Width |
|---|---|
| content, content/[type], submissions, maps, languages | `max-w-5xl` |
| content/[type]/[slug], edit, submissions/[id] | `max-w-4xl` |
| languages/new, notifications | `max-w-3xl` |

So the column jumps on every navigation, the G0 nav sits in its own 5xl container *above* the page's
container (double padding, nothing sticky), and each page re-invents its own header, back-link, error
banner and empty state. Consequences: no consistent page title/description/actions, no breadcrumbs (each
page hand-writes `← All X` with a different class), no skeleton, and a 403 inside the portal renders the
*public site* error page.

### 2.2 Navigation

This is the defect behind "the map editor isn't even linked":

1. **The site header's Foundry entry points at `/submissions`** (`SiteHeader.svelte:65`), and it is shown
   to **every signed-in user** because the root layout sets `staff.foundry = Boolean(user)`. The portal's
   front door is the community submission queue.
2. **No Foundry home**, so there is nothing to land on that explains what the portal contains.
3. The sub-nav added in G0 is a flat list of five equal items — Content, Maps, Submissions, Languages,
   Notifications — with no grouping by job, no unread badge, and no explanation when Maps is hidden for
   an account without `content.view`.
4. `/languages` is a duplicate section for one content type.
5. **No cross-links between related things**: a map does not link to the zone or locations it represents;
   a location does not link to its map; a submission does not link to the entity it would become; a
   notification links to the map but a map does not link back to anything.
6. Nothing is reachable in one click from Home, because there is no Home.

### 2.3 Features

| Missing | Evidence | Server ready? |
|---|---|---|
| Create for 20+ content types | only `languages/new` exists | **yes** (`POST /content/{type}`) |
| Browse-page entry points for create | `/content/[type]` has no New action | — |
| Submission → catalog promotion | approving flips `Status`; the entity never lands | no (needs a mapper) |
| World-health dashboard | `ContentAuditService` has no page | yes |
| Zone/location editor (traits, actor pools, interiors) | §5 item 1 of `foundry-map-editor.md` | partly |
| Foundry-wide search | none | no |
| Unread notification count / mark all | per-row only | no |
| Audit history of decisions | `AdminAuditEntry` rows exist, no page | yes |
| Bulk actions, export, import | none | no |

### 2.4 Forms — the raw-JSON problem

Two separate problems wear the same label:

**(a) Values with no widget.** `ContentFieldType` is scalar-only — `Text, Slug, LongText, Integer,
Decimal, Boolean, EnumString` ([`ContentSchema.cs:4`](../Veldrath.Contracts/Content/ContentSchema.cs:4)).
Anything structured — quest objectives, loot tables, actor pools, effect lists, trait maps, socket
configs — has **no input**, and `buildPayload` deliberately passes unshown fields through unchanged. So
those values can only be authored by hand.

**(b) Raw JSON shown to humans.** Three surfaces:

- `/content/[type]/[slug]` renders the whole payload as `<pre>{prettyPayload}</pre>`.
- `/submissions/[id]` in *create* mode is a **`<textarea>` of hand-written JSON** with a template
  containing `{"slug": "", …}`.
- `/submissions/[id]` in *view* mode renders `<pre>` JSON.

And the submission create form's type list is hardcoded (and already drifted: `language` is lowercase,
`weapon`/`armor`/`material`/… are missing) while the schema registry knows 20+ types.

Smaller form gaps that make authoring feel unfinished: no required-field affordance beyond `*`; no
per-field error anchoring (issues are listed above the form, not beside the input); no min/max stepper
from the descriptor; `LongText` is a plain textarea with no markdown preview even though the content is
markdown; slugs are typed by hand; no dirty-state guard; no sticky save bar on long forms; no
section navigation for a 10-group schema.

### 2.5 Access and correctness

- `staff.foundry = Boolean(user)` shows the portal link to **every signed-in player**; the group layout
  requires only a session. Submitting and voting are legitimately open, but the label "Foundry" on the
  header promises a workshop and delivers a queue.
- **The guards are inconsistent, and the reading surfaces are the loosest.** `/maps` requires
  `content.view`; `/content`, `/content/[type]` and `/content/[type]/[slug]` have no page guard at all and
  read an anonymous API — so any signed-in player can browse canonical content, including rarity weights,
  inactive rows and the raw payload. Whether that is intended is a decision (§5.4), but the two halves of
  the portal should not disagree.
- The sub-nav hides sections silently; a Curator without `content.manage` sees Maps but no create form and
  gets no explanation.
- The group has no `+error.svelte`, so a 403/500 escapes the portal's frame.
- Capability flags are computed once per request from the root layout; a role change needs a reload.

---

## 3. Target: a portal with three jobs

The portal serves three different jobs, and the navigation should say so instead of listing surfaces:

| Job | Who | Section |
|---|---|---|
| **Author** | ContentEditor | Content (browse → detail → edit, create), Maps (list → editor) |
| **Curate** | Curator, ContentEditor | Review queue (submissions and maps awaiting a decision), decisions history |
| **Reference** | everyone with `content.view` | Schemas, world health, notifications |

### 3.1 Route map

| New / changed | Notes |
|---|---|
| `(foundry)/foundry/+page.svelte` → `/foundry` | **Foundry home**: queue counts, unread notifications, and a link to every section. Not `/`: the public site owns that route, and a second page resolving to `/` is a SvelteKit route conflict. |
| `/content`, `/content/[type]`, `/content/[type]/[slug]`, `…/edit` | kept; detail becomes a read-only renderer; browse gains a New action |
| `/content/[type]/new` | new; the schema drives it |
| `/languages`, `/languages/new` | **removed**, redirect to `/content/language` and `/content/language/new` |
| `/maps`, `/maps/[mapId]` | kept; linked from home, from review, and from any zone/location page that appears later |
| `/submissions`, `/submissions/[id]` | kept; create becomes schema-driven; review stays |
| `/review` | new; one queue for submissions **and** maps awaiting a decision |
| `/schemas`, `/world-health` | new; the audit service finally has a home |
| `/notifications` | kept; unread count surfaces in the shell |

Paths that already exist are **not** moved — `/maps` and `/content` are referenced in docs, tests and
bookmarks. Only additions, plus two redirects.

### 3.2 Layout

One shell, and pages stop owning frames:

```
(foundry)/+layout.svelte
  ┌ sidebar (sticky) ────────────┬ main ────────────────────────────┐
  │ Foundry                      │ <FoundryPageHeader>              │
  │  Home                        │   title / description / actions  │
  │  Author                      │ <FoundryBreadcrumbs>             │
  │   Content                    │ page sections                    │
  │   Maps                       │                                  │
  │  Curate                      │                                  │
  │   Review        (n)          │                                  │
  │  Reference                   │                                  │
  │   Schemas                    │                                  │
  │   World health               │                                  │
  │   Notifications (n)          │                                  │
  └──────────────────────────────┴──────────────────────────────────┘
```

- The layout owns the frame, the width (`max-w-6xl`, one value), and the sidebar; pages render sections.
- Extracted components: `FoundryPageHeader` (title, description, actions slot), `FoundryBreadcrumbs`,
  `FoundryEmptyState`, `FoundrySection` (card + heading), and a `DataTable` wrapper over the design
  system's table. Nothing new is invented: `Skeleton` v5 and the existing tokens.
- `(foundry)/+error.svelte` renders inside the shell, so a 403 says which capability is missing instead of
  dumping the user onto the public error page.
- Mobile: the sidebar collapses to the current horizontal nav; the horizontal nav stays as the small
  breakpoint.
- The site header's Foundry entry becomes capability-aware: omitted for a plain player, or relabelled
  "Submit content" and pointed at `/submissions/new` — the portal home (`/`) is for people with a content
  capability.

### 3.3 Forms

**Phase 1 — make the existing schema pleasant.** Number input honouring `min`/`max`; markdown textarea
with preview for `LongText`; slug derived from the display name with a "lock" toggle; enum select showing
descriptions; per-field error anchoring from `issuesByField`; required markers that also block submit;
sticky save bar (Save / Validate / Discard) with a dirty-state guard; section anchors.

**Phase 2 — extend the schema so collections are first class.** Additive `ContentFieldType` members:

| New kind | Renders | Examples it unlocks |
|---|---|---|
| `StringList` | add/remove/reorder chip list | tags, aliases, effect names |
| `KeyValue` | two-column rows | trait maps, stat modifiers |
| `ObjectList` (`+ itemGroups`) | repeatable field group with add/remove/reorder | quest objectives, loot entries, actor pools |
| `Reference` (`+ refType`, `+ multiple`) | search + pick from another content type | item/quest/actor references |

`writePath`/`readPath` already handle nested dot-paths, so the payload side mostly exists; the client
needs `readList`, `appendRow`, `removeRow`, `moveRow` and a `ListEditor`/`RowEditor` pair, all pure and
tested. The web `ContentFieldKind` union gets a parity test against the server enum so the two cannot
drift.

**Phase 3 — delete the raw JSON surfaces.**
- Content detail renders the schema as a definition list (label → value, groups as cards). The raw payload
  survives behind an "Advanced → raw JSON" disclosure, for debugging.
- Submission create becomes *the same form as content creation*: pick a type from the schema registry, fill
  the schema, post the payload. The hardcoded type list dies.
- Submission view uses the same read-only renderer, so a reviewer reads an item as an item.

**Phase 4 — reference pickers.** A picker endpoint (`GET /api/content/{type}?search=` already exists) turns
every slug field that names content into a search-and-pick, which removes the class of typo the validators
exist to catch.

### 3.4 Features that follow

1. **Create for every type** — `/content/[type]/new` plus a New action on each browse page (server ready).
2. **One review queue** — submissions and maps in one list, each row linking to its own decision surface.
3. **Submission → catalog promotion** — approving a submission writes the payload through the content write
   path and records the resulting slug on the submission; the reviewer sees a field-level diff first. This
   is the blocker named in `foundry-map-editor.md` §5.6 and it needs a mapper decision (see §5).
4. **World health** — the audit service's rules rendered as a page with per-issue deep links.
5. **Schemas** — a read-only browser for the registry, which is also the parity reference for phase 2.

---

## 4. Phases

Each phase is shippable on its own and keeps the tests with it.

| Phase | Scope | Done when |
|---|---|---|
| **H0** | Shell, sidebar, home, header/breadcrumb/empty-state components, group `+error.svelte`, `/languages` folded and redirected, capability-aware site-header entry, unread counts | Every route is reachable from Home in one click; one width; a 403 renders in the shell; `/languages` no longer exists |
| **H1** | Form phase 1 on the current schema: stepper, markdown preview, slug derive, error anchoring, dirty guard, sticky save bar | Editing a quest or item feels like a form, not a form dump; field errors sit beside their field |
| **H2** | Schema extensions (`StringList`, `KeyValue`, `ObjectList`, `Reference`) + list/row editors + client/server enum parity test | Quest objectives and loot entries can be authored without JSON |
| **H3** | Read-only renderer; content detail and submission view/detail lose their `<pre>`; submission create becomes schema-driven; raw JSON behind a disclosure | Grep for `<pre>` in `(foundry)` returns nothing user-facing |
| **H4** | Create for every content type; New actions on browse pages | Every schema has a working create form and a loader test |
| **H5** | One review queue; submission promotion with diff; decisions history | Approving a submission lands content; the queue shows both kinds |
| **H6** | World-health page, schemas browser, Foundry search | The audit service is visible; schemas are browsable |

Order matters: H0 first because everything else moves inside the shell; H1 before H2 because the widgets
are shared; H3 after H2 so the renderer knows the new field kinds; H5 last because promotion depends on
the form/renderer vocabulary being settled.

### 4.1 Status

| Phase | Shipped |
|---|---|
| H0 | shell, home at `/foundry`, grouped nav, group error page, `/languages` 308 |
| H1 | content editor: derived slug with a lock, markdown preview, required blocking, sticky Save/Validate/Discard bar with a dirty guard, section anchors, `FoundryPageHeader`/`FoundryBreadcrumbs` |
| H2 | `StringList`, `KeyValue`, `ObjectList` and `Reference` field kinds on the descriptor, `ListEditor`/`RowEditor`, reference pickers, the field-kind parity suite, and the three raw-JSON fields replaced |
| H3 | `ContentRenderer` for the read paths, content detail and submission view render fields, submission create is schema-driven, and the raw JSON survives only behind a disclosure |
| H4 | `/content/[type]/new` for every registered schema, a New action on each browse page, and the reference-option loader shared by both write paths |
| H5–H6 | not started |

H3 notes:

- **`<pre>` now exists in exactly one place** — the renderer's "Advanced — raw JSON" disclosure. Both read
  paths (content detail, submission view) render the schema instead, and keys the schema does not describe
  are listed as "Other fields" rather than dropped.
- **The create form's type picker is a `GET`.** The server renders the chosen type's schema, so changing the
  type reloads the page with different fields instead of bundling every schema into one document.
- **Create cannot author collections yet.** A type with a `StringList`/`KeyValue`/`ObjectList` field says so
  by name and the author uses the content editor. H4 gives every type a dedicated create route; authoring a
  collection inside the *submission* form is deferred, because the content editor already owns that and the
  promotion work in H5 will show whether two collection authors are needed at all.
- **A submission and a saved entity are now the same shape.** Both build their payload with `buildPayload`
  from the same schema, which is what H5's promotion needs — no per-type mapper.

H4 notes:

- **`/content/[type]/new` is a plain form on purpose.** It creates an entity from its scalar fields and then
  hands the author to the editor, which is where collections are edited. Requiring the collections up front
  would have made every collection-bearing type uncreatable, since this form has no client-side state and
  therefore no list or row editors.
- **`referenceOptions`/`referenceTypes` live in [`content-refs.ts`](../web/client/src/lib/server/content-refs.ts)**
  now, shared by the edit and create loaders; `isScalarKind` in `content-editor.ts` is the single definition
  of "a field a plain control can author".
- **A New entry action appears on a browse page only for an account holding `content.manage`.**

Notes worth keeping:

- **The markdown preview renders a structure, not HTML.** `parseMarkdown` returns blocks and spans and the
  page renders them with Svelte markup, so author input is escaped by the compiler and there is no
  `{@html}` on a path an author controls — which the `svelte/no-at-html-tags` rule rejects anyway.
- **"Enum select showing descriptions" is still open.** `enumValues` is a `string[]`; there is nothing to
  show beside each option until the descriptor carries per-value metadata. H2 changed the array's *type*
  questions but not this one.
- **H2 retired the last three JSON textareas on the write path.** `quest.objectives`, `quest.rewards`,
  `recipe.ingredients` and `loottable.entries` are `ObjectList`s described row by row, `recipe`'s
  `outputItemSlug`/`craftingSkill` are `Reference`s with the first fifty slugs preloaded, and
  `ContentFieldDescriptor` gained `Items`, `RefType` and `Multiple`. The stored JSON is unchanged, so no
  migration was needed — only the way it is authored.
- **`KeyValue` has no schema user yet.** It is declared and rendered (as two-column rows, converted back to
  an object on save) so a trait map can adopt it without new client work.

---

## 5. Decisions (settled 2026-11-16)

1. **Payload shape for collections — A.** `ContentFieldDescriptor` gains an optional `Items` list so a
   repeatable group is described by the descriptor that owns it. One descriptor shape, one renderer.
2. **One payload shape for submissions and content.** A submission stores the content payload verbatim and
   is validated with the content rules at submit time, so H5's promotion needs no per-type mapper.
3. **`LongText` is markdown**, rendered on the public site too.
4. **Portal access.** Foundry Home and the content pages require `content.view`; `/maps` already does.
   A signed-in player's entry point is **"Submit content" → `/submissions/new`**, and `/submissions` and
   `/notifications` stay open to any session. The site header reflects the split rather than showing
   everyone a link called "Foundry".
5. **`/languages` redirects** with a `308` to `/content/language`. `/languages/new` stays until H4 gives it
   a replacement — removing the only create form before the general one exists would be a regression.

---

## 6. H0 — the shell, in detail

H0 is the phase that makes the rest reachable, so it is specified here rather than left to be
re-derived.

**Server (small).** `GET /api/foundry/notifications/unread-count` returning `{ count }` for the caller, so
the sidebar badge does not fetch the whole inbox on every page. One service method, one endpoint test.

- Add it beside the existing `GET /api/foundry/notifications` in `FoundryEndpoints.MapFoundryEndpoints`,
  with the same `[Authorize]` treatment as the inbox — it reads the caller's own rows only.
- The service method is `CountUnreadAsync(accountId, ct)` → `db.FoundryNotifications.CountAsync(n =>
  n.RecipientId == accountId && !n.IsRead, ct)`. Same predicate as `GetNotificationsAsync`, so the badge
  cannot disagree with the list it summarises. Do not reuse the list call and count in memory.
- Keep the literal segment ahead of any `{id}`-shaped notification route. Today the write route is
  `/notifications/{id}/read`, so nothing collides, but the ordering is what keeps that true.

**Access (settled, §5.4).**
- `(foundry)/+layout.server.ts` derives `canViewContent` (`Permissions.ViewContent`), `canManageContent`
  (`Permissions.ManageContent`), `canReview`, and `unreadNotifications` (from the endpoint above).
- **`canReview` is the `Curator` role, not the permission.** The map decision endpoints are
  `.RequireAuthorization("Curator")`, so a hand-granted `content.review` permission without the role would
  render a Review entry that 403s on every click. Gate the nav on `access.roles.includes(Roles.Curator)`.
  `Permissions.ReviewSubmissions` (string `content.review`) may be consulted as an extra signal for
  read-only hints, never as the only reason to show a decision verb. Reconciling the role-only endpoints
  with the permission model is H5 work, not H0.
- The count is fetched once in the layout, so every page in the group gets the badge without a per-page
  call. A failed count degrades to `0`; it must not throw and take the whole group down.
- `requirePermission(ViewContent, …)` in `(foundry)/+page.server.ts` (home) and in the content page
  loaders; `/maps*` keeps its guard. `/submissions*` and `/notifications` stay session-only.
- Root `+layout.server.ts` replaces the single `staff.foundry = Boolean(user)` with
  `staff.foundry = hasPermission(access, ViewContent)` and `staff.submit = Boolean(user)`.
  `SiteHeader` renders "Foundry" → `/` for the former and "Submit content" → `/submissions/new` for the
  latter, never both.

**Components** (new, in `web/client/src/lib/components/foundry/`):
- `FoundrySidebar.svelte` — grouped sections, active state, unread badge, collapses to the existing
  horizontal bar below `lg`.
- `FoundryPageHeader.svelte` — title, one-line description, `children` slot for actions.
- `FoundryBreadcrumbs.svelte` — items `{ label, href? }`, last item is the current page.
- `FoundryEmptyState.svelte` — icon, sentence, optional action.
- `FoundrySection.svelte` — card with a heading and optional count.
- `nav.ts` grows from a flat list to grouped items (`{ group, items }`) plus counts, still pure and
  unit tested; `foundryNavItems` keeps its capability filtering and gains the queue/unread counts.

**Layout.** `(foundry)/+layout.svelte` becomes the shell: sidebar + `<main class="max-w-6xl">`. The
layout owns padding and width; **every page loses its own `mx-auto max-w-*` wrapper** and renders
`FoundryPageHeader` plus sections inside the main column.

**Pages touched in H0:** every `(foundry)` page (frame removal + header component), plus:
- new `(foundry)/foundry/+page.svelte` and `+page.server.ts` → `/foundry` — home: four cards (Content,
  Maps, Review, Notifications) with counts from the list calls the portal already makes, and an
  empty-state sentence when a section is unavailable to this account. The group has no `+page.svelte`
  of its own: `/` belongs to the public site, so a page inside the group would collide with it.
  `FOUNDRY_HOME_HREF` in `nav.ts` is the only place the home route is spelled.
- new `(foundry)/+error.svelte` — renders inside the shell; a 403 names the missing capability.
- `(foundry)/languages/+page.server.ts` becomes `redirect(308, '/content/language')` and
  `languages/+page.svelte` is deleted. `languages/new` stays, with a comment pointing at H4.
- new `(foundry)/review/+page.server.ts` + `+page.svelte` — **stub in H0**: submissions with
  `status=Pending` plus maps in review, both linking to their decision surfaces. The full queue with
  promotion lands in H5; the sidebar needs the destination to exist now.

**Tests that ship with H0:** `nav.test.ts` (groups, counts, capability filtering), a home loader test,
`pages-foundry.test.ts` render tests for the shell components and the error page, a `/languages` redirect
test, a site-header test for the player-vs-editor entry, and the parity updates —
`routes.test.ts` learns `/review` (referenced by the Playwright guard list) and drops the `/languages`
exemption; `rest-routes.test.ts` learns the unread-count endpoint (consumed, so no exemption).

**Done when:** every route in §3.1 is reachable from Home in one click; no page builds its own frame;
a 403 renders inside the portal and names the capability; `/languages` 308s to `/content/language`;
a signed-in player sees "Submit content", never "Foundry".

---

## 7. Acceptance for the overhaul

- One shell, one content width, breadcrumbs and page headers everywhere; no page builds its own frame.
- Every route reachable from Home in one click, with unread counts where they matter.
- No user-facing raw JSON: it exists only behind an "Advanced" disclosure.
- Every content type creatable and editable through forms, including its collections.
- Maps and submissions share one review queue, and approving a submission lands content in the catalog.
- A 403 or 500 inside the portal renders inside the portal and names the missing capability.

## 8. See also

- [`foundry-map-editor.md`](foundry-map-editor.md) — the editor itself, and the deferred items it names.
- [`content-pipeline.md`](content-pipeline.md) — the schema registry, write path and validators.
- [`web-feature-parity-and-coverage.md`](web-feature-parity-and-coverage.md) — the parity suites this
  work must keep green.
- [`../docs/client-ui-architecture.md`](../docs/client-ui-architecture.md) §5 — the map editor's model,
  which the shell must wrap without changing.

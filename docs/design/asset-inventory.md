# Asset inventory

> **Audience:** whoever maintains the brand files and whoever is commissioning new art.
> **Purpose:** every image in `web/client/static/images/`, what renders it, what it weighs, and what should happen to it.
> **Method:** dimensions decoded with PIL, exact byte counts measured with `stat`, consumers identified via repository-wide grep across `web/client/src/`, `web/client/static/images/favicon/site.webmanifest`, and `docfx.json`.
> **Status:** updated after PR 79 (`feat/derived-brand-assets` derived export switch) and PR 80 (`docs/design-specs` locked tokens).

---

## 1. Summary

| Metric | Value |
|---|---|
| Shipped derived mark family payload (all WebP exports) | **283,760 bytes** (0.27 MB / 283.8 KB) |
| Served mark family weight before derived-asset switch | **4,643,672 bytes** (4.64 MB) |
| Payload reduction for mark family | **93.9% reduction** |
| Favicons & web manifest install assets total weight | **115,042 bytes** (112.3 KB) |
| Social OG preview card (`social-og.png`) | **91,364 bytes** (89.2 KB) |
| Total files in `web/client/static/images/` | **39** (17 shipped derived/favicons, 4 master PNGs, 4 commissioned-unwired, 14 unassigned masters/emblems) |
| Shipped / referenced in web client code | **21** (17 derived/favicons + 4 master source-of-truth PNGs kept in static) |
| Unreferenced by UI code | **18** (4 commissioned-but-unwired, 1 unwired export, 9 unassigned masters, 4 herald emblems) |
| Vector master status | **DEFERRED** (painterly logotype requires illustrator, not auto-trace) |

PR 79 replaced direct web serving of the raw 1.6–1.8 MB master PNGs with optimized, size-matched WebP exports routed through `BrandMark.svelte`. The master PNGs remain in `web/client/static/images/` as the canonical source of truth for derivations.

---

## 2. In service (Shipped derived assets & favicons)

Every shipped mark variant is served via `BrandMark.svelte` or standard favicon tags in `app.html` / `site.webmanifest`.

| Path | Pixels | Bytes | Consumer (file:line) | Verdict |
|---|---|---|---|---|
| `web/client/static/images/crest-hero-1040.webp` | 1040 × 1040 | 167,230 B | `BrandMark.svelte:17` (hero 1040w srcset) | Keep (derived export) |
| `web/client/static/images/crest-primary-228.webp` | 228 × 228 | 17,852 B | `BrandMark.svelte:16,17` (hero 228w src/srcset) | Keep (derived export) |
| `web/client/static/images/crest-primary-152.webp` | 152 × 152 | 9,620 B | `BrandMark.svelte:23,24` (auth 152w src/srcset) | Keep (derived export) |
| `web/client/static/images/crest-primary-96.webp` | 96 × 96 | 5,042 B | `BrandMark.svelte:29,30` (account 44px, roster 48px) | Keep (derived export) |
| `web/client/static/images/crest-primary-88.webp` | 88 × 88 | 4,448 B | `BrandMark.svelte:31` (creation 40px) | Keep (derived export) |
| `web/client/static/images/wordmark-header-60.webp` | 240 × 60 | 12,944 B | `BrandMark.svelte:33,34` (wordmark 240w src/srcset) | Keep (derived export) |
| `web/client/static/images/wordmark-header-90.webp` | 360 × 90 | 23,930 B | `BrandMark.svelte:34` (wordmark 360w srcset) | Keep (derived export) |
| `web/client/static/images/lockup-horizontal-128.webp` | 384 × 128 | 14,636 B | `BrandMark.svelte:40,41` (lockup 384w src/srcset) | Keep (derived export) |
| `web/client/static/images/lockup-horizontal-192.webp` | 576 × 192 | 28,058 B | `BrandMark.svelte:41` (lockup 576w srcset) | Keep (derived export) |
| `web/client/static/images/social-og.png` | 1200 × 630 | 91,364 B | `+layout.svelte:25` (og:image meta tag) | Keep (derived export) |
| `web/client/static/images/favicon/favicon-16x16.png` | 16 × 16 | 651 B | `app.html:11` | Keep (favicon) |
| `web/client/static/images/favicon/favicon-32x32.png` | 32 × 32 | 1,874 B | `app.html:10`, `docfx.json:48,71` | Keep (favicon) |
| `web/client/static/images/favicon/favicon.ico` | 32 × 32 | 2,563 B | `app.html:9` | Keep (legacy icon) |
| `web/client/static/images/favicon/apple-touch-icon.png` | 180 × 180 | 25,588 B | `app.html:12` | Keep (iOS touch icon) |
| `web/client/static/images/favicon/android-chrome-192x192.png` | 192 × 192 | 29,033 B | `site.webmanifest:6`, `docfx.json:48,70` | Keep (Android install icon) |
| `web/client/static/images/favicon/android-chrome-512x512.webp` | 512 × 512 | 54,958 B | `site.webmanifest:11` | Keep (Android install icon) |
| `web/client/static/images/favicon/site.webmanifest` | manifest | 375 B | `app.html:13` | Keep (web manifest) |

---

## 3. Master sources of truth (Raster masters)

These 4 master PNGs remain stored in `web/client/static/images/` as the canonical source of truth for all derived exports, brand specifications, and derivation pipeline scripts.

| Path | Pixels | Bytes | Role | Verdict |
|---|---|---|---|---|
| `web/client/static/images/veldrath-final-crest-transparent.png` | 1208 × 1226 | 1,688,826 B | Master source of truth for crest derivations; spec reference | Keep (raster master) |
| `web/client/static/images/veldrath-text-flat-true-transparent.png` | 1916 × 464 | 1,060,744 B | Master source of truth for wordmark derivations; spec reference | Keep (raster master) |
| `web/client/static/images/veldrath-final-horizontal-transparent.png` | 1672 × 939 | 1,894,102 B | Master source of truth for horizontal lockup derivations; spec reference | Keep (raster master) |
| `web/client/static/images/veldrath-final-square-v-transparent.png` | 1254 × 1254 | 2,943,396 B | Master source of truth for tile / V monogram derivations; spec reference | Keep (raster master) |

---

## 4. Vector masters status: DEFERRED

Both vector masters (`crest.svg` and `wordmark.svg`) are explicitly **DEFERRED**.

**Reason for decision:**
Veldrath's brand mark and wordmark are bespoke painterly artwork featuring chipped basalt relief, aged gold veins, and ember fractures. Mechanical auto-tracing (e.g. `potrace` / `vtracer`) destroys ink weight, distorts character terminals, and scores poorly on shape fidelity (IoU 65.9% vs anchor). Authoring true vector masters for this art direction requires a skilled human illustrator using vector artwork tools, not an automated raster-to-vector conversion.

This is recorded as an intentional architectural decision, not an omission.

---

## 5. Commissioned-but-unwired deliverables

These 4 deliverables were commissioned and delivered under PR 79/80 specifications. They exist on the branch as complete artwork but are not yet wired to active UI components or page routes.

| Path | Pixels | Bytes | Intended surface | Verdict |
|---|---|---|---|---|
| `web/client/static/images/illustration-error.webp` | 320 × 240 | 18,066 B | unwired (404 / error pages: `/404`, `/error`) | Keep (commissioned-unwired) |
| `web/client/static/images/illustration-empty.webp` | 240 × 180 | 6,126 B | unwired (empty states: roster, bag, quest log) | Keep (commissioned-unwired) |
| `web/client/static/images/illustration-lore.webp` | 1600 × 600 | 88,448 B | unwired (lore article masthead header) | Keep (commissioned-unwired) |
| `web/client/static/images/header-email.png` | 600 × 200 | 20,272 B | unwired (transactional email header masthead) | Keep (commissioned-unwired) |

---

## 6. Unassigned masters, studies and herald emblems

These 14 unassigned master files and herald emblems remain in `web/client/static/images/` for historical continuity and potential future surface assignments (e.g., faction art, lore articles, or press kits).

| Path | Pixels | Bytes | Notes / Role | Verdict |
|---|---|---|---|---|
| `web/client/static/images/crest-primary-80.webp` | 80 × 80 | 3,934 B | unwired (40px @2x export; BrandMark uses 88px for creation) | Keep (unwired export) |
| `web/client/static/images/veldrath-v-clean-true-transparent.png` | 1115 × 1389 | 2,000,649 B | unreferenced (V-monogram candidate master) | Keep (unassigned master) |
| `web/client/static/images/veldrath-icon-big-transparent.png` | 1254 × 1254 | 2,321,620 B | unreferenced (app icon candidate master) | Keep (unassigned master) |
| `web/client/static/images/veldrath-text-energy-true-transparent.png` | 1907 × 579 | 1,746,728 B | unreferenced (ember wordmark variant) | Keep (unassigned master) |
| `web/client/static/images/veldrath-final-vertical-transparent.png` | 882 × 1254 | 1,553,044 B | unreferenced (portrait crest+wordmark lockup) | Keep (unassigned master) |
| `web/client/static/images/veldrath-full-logo-transparent.png` | 1254 × 1254 | 1,493,574 B | unreferenced (square composite lockup) | Keep (unassigned master) |
| `web/client/static/images/veldrath-final-circular-transparent.png` | 1254 × 1254 | 1,902,327 B | unreferenced (circular medallion lockup) | Keep (unassigned master) |
| `web/client/static/images/veldrath-v-molten-true-transparent.png` | 1066 × 1356 | 1,923,907 B | unreferenced (molten V study) | Keep (unassigned master) |
| `web/client/static/images/molten_v_with_fiery_aura_true_transparent.png` | 1025 × 1176 | 1,469,818 B | unreferenced (fiery aura V study) | Keep (unassigned master) |
| `web/client/static/images/molten_metallic_v_logo_design_true_transparent.png` | 828 × 1102 | 904,614 B | unreferenced (metallic V study) | Keep (unassigned master) |
| `web/client/static/images/herald/dark_arcane_herald_emblem_transparent.png` | 1254 × 1254 | 2,547,435 B | unreferenced (faction emblem artwork) | Keep (unassigned emblem) |
| `web/client/static/images/herald/dark_sigil_of_the_forgotten_order_transparent.png` | 1240 × 1247 | 2,559,134 B | unreferenced (faction emblem artwork) | Keep (unassigned emblem) |
| `web/client/static/images/herald/the_herald_dark_fantasy_emblem_transparent.png` | 817 × 1448 | 2,128,579 B | unreferenced (faction emblem artwork) | Keep (unassigned emblem) |
| `web/client/static/images/herald/the_herald_emblem_of_darkness_transparent.png` | 1669 × 938 | 1,710,859 B | unwired (style ref in spec for illustration-lore) | Keep (unassigned emblem) |

---

## 7. Work queue

Ordered by priority:

| # | Work | Why | Status |
|---|---|---|---|
| 1 | Wire `illustration-error.webp` | 404 / error pages (`/404`, `/error`) currently display plain text | Commissioned, awaiting UI wiring |
| 2 | Wire `illustration-empty.webp` | Roster, bag, and quest log empty states currently show empty containers | Commissioned, awaiting UI wiring |
| 3 | Wire `illustration-lore.webp` | Lore articles currently lack header hero imagery | Commissioned, awaiting UI wiring |
| 4 | Wire `header-email.png` | Transactional email templates have no branded header image | Commissioned, awaiting email template wiring |
| 5 | Update `brand-guide.md` & `asset-specs.md` | Doc tables still claim raw 1.6 MB master PNGs are served directly | Follow-up card needed |
| 6 | Commission vector masters | High-resolution print & SVG favicon requirements | DEFERRED until illustrator pass |

---

## 8. How this was measured

```sh
cd /home/kung/repos/Veldrath
# Measure dimensions and exact byte counts
python3 -c "from PIL import Image; import os; ..."

# Grep consumers across web client and doc configs
git grep -n "crest-primary" web/client/
git grep -n "wordmark-header" web/client/
git grep -n "lockup-horizontal" web/client/
git grep -n "favicon" web/client/ app.html docfx.json
```

Re-run these checks after any asset migration or component refactoring.
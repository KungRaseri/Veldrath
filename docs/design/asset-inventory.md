# Asset inventory

> **Audience:** whoever maintains the brand files and whoever is commissioning new art.
> **Purpose:** every image in `web/client/static/images/`, what renders it, what it weighs, and what
> should happen to it.
> **Method:** dimensions from `file`, weights from `stat`, consumers from a repository-wide grep. Re-run
> the commands in [§6](#6-how-this-was-measured) after any change.
> **Status:** proposed verdicts — nothing has been moved or deleted yet. See [§4](#4-decisions-proposed).

---

## 1. Summary

| Metric | Value |
|---|---|
| PNG files in `static/images/` | **17** (plus 6 favicon/install files) |
| Total weight of those PNGs | **≈ 31.8 MB** |
| Referenced by a component | **3** |
| Referenced by nothing | **14**, **≈ 27.2 MB** |
| Weight served on every page (header wordmark) | 1.01 MB |
| Weight served on the auth and roster pages (crest) | 1.61 MB |
| Editable vector / layered sources committed | **none** |

The three files in service are 4.6 MB between them and render at 30–76px. Nothing here is a failure of
art direction; it is a failure of export.

---

## 2. In service

| File | Pixels | Weight | Rendered at | Consumed by |
|---|---|---|---|---|
| `veldrath-text-flat-true-transparent.png` | 1916 × 464 | 1.01 MB | 30px tall | `SiteHeader.svelte:47` — every page, sticky |
| `veldrath-final-crest-transparent.png` | 1208 × 1226 | 1.61 MB | 76 / 44 / 48 / 40px; 520px at 18% | `+page.svelte:20`, `login/+page.svelte:62`, `register/+page.svelte:54`, `forgot-password/+page.svelte:44`, `reset-password/+page.svelte:55`, `verify-email/+page.svelte:47`, `account/+page.svelte:311`, `characters/+page.svelte:47`, `characters/new/+page.svelte:351` |
| `veldrath-final-horizontal-transparent.png` | 1672 × 939 | 1.81 MB | 64px tall | `SiteFooter.svelte:14` — every page with a footer |
| `favicon/favicon-16x16.png` | 16 × 16 | 0.9 KB | native | `app.html:10` |
| `favicon/favicon-32x32.png` | 32 × 32 | 3 KB | native | `app.html:9` |
| `favicon/apple-touch-icon.png` | 180 × 180 | 75 KB | native | `app.html:11` |
| `favicon/android-chrome-192x192.png` | 192 × 192 | 84 KB | native | `manifest` |
| `favicon/android-chrome-512x512.png` | 512 × 512 | **583 KB** | native | `manifest` |
| `favicon/favicon.ico` | multi | — | native | legacy |

**Verdict: keep, re-export.** None of these should be deleted; every one of them should be replaced by
an export sized to its rendered box, plus an SVG. Requirements in
[Asset specs §4.1](asset-specs.md). The 583 KB 512px install icon is the single worst offender for a
file the browser may not even request.

---

## 3. Referenced by nothing

| File | Pixels | Weight | Notes |
|---|---|---|---|
| `veldrath-final-square-v-transparent.png` | 1254 × 1254 | 2.81 MB | V in a square field |
| `herald/dark_sigil_of_the_forgotten_order_transparent.png` | 1240 × 1247 | 2.44 MB | Heraldic emblem |
| `herald/dark_arcane_herald_emblem_transparent.png` | 1254 × 1254 | 2.43 MB | Heraldic emblem |
| `veldrath-icon-big-transparent.png` | 1254 × 1254 | 2.21 MB | Oversized app-icon candidate |
| `herald/the_herald_dark_fantasy_emblem_transparent.png` | 817 × 1448 | 2.03 MB | Portrait-format emblem |
| `veldrath-v-clean-true-transparent.png` | 1115 × 1389 | 1.91 MB | V study |
| `veldrath-v-molten-true-transparent.png` | 1066 × 1356 | 1.83 MB | V study |
| `veldrath-final-circular-transparent.png` | 1254 × 1254 | 1.81 MB | Circular crest variant |
| `veldrath-text-energy-true-transparent.png` | 1907 × 579 | 1.67 MB | Ember wordmark variant |
| `herald/the_herald_emblem_of_darkness_transparent.png` | 1669 × 938 | 1.63 MB | Wide emblem |
| `veldrath-final-vertical-transparent.png` | 882 × 1254 | 1.48 MB | Crest over wordmark |
| `veldrath-full-logo-transparent.png` | 1254 × 1254 | 1.42 MB | Composite lockup |
| `molten_v_with_fiery_aura_true_transparent.png` | 1025 × 1176 | 1.40 MB | Earlier V study |
| `molten_metallic_v_logo_design_true_transparent.png` | 828 × 1102 | 0.88 MB | Earlier V study |

They fall into three groups with different answers:

1. **Letter-V studies** — `v-clean`, `v-molten`, both `molten_*`, `square-v`. Development history for
   the crest's V motif. No surface is waiting for them.
2. **Alternate lockups** — `vertical`, `circular`, `full-logo`, `text-energy`, `icon-big`. Plausible
   future uses: install icon, press kit, email header, social card. None is in use.
3. **Herald emblems** — four pieces of illustration-grade artwork, 1250–2500px, 8–15 MB. These are the
   only art in the repository that could carry a faction, a region or a lore article, and none of them
   ever has. At 24px they are noise; at 512px they are unusable at their current weight.

---

## 4. Decision: left in place (2026-11-19)

The 14 unreferenced files stay where they are. Nothing is moved, nothing is deleted this cycle.

They were reviewed one by one rather than by filename, and the finding is that they are **not duplicates
of the three live marks — they are an unassigned lockup family**, and two of them close gaps the rest
of this documentation already assumes are filled:

| File | Role it fills | Status |
|---|---|---|
| `veldrath-v-clean-true-transparent.png` | V-only monogram — the small-size mark the [brand guide](brand-guide.md) points at for anything under 48px, and the right source for a favicon | Unassigned; the role has no assigned file today |
| `veldrath-icon-big-transparent.png` | Rounded-square install tile with the crest in a gold frame — the natural master for the 180/192/512 icons | Unassigned; the icons are separate files with no relationship to it |
| `veldrath-text-energy-true-transparent.png` | Wordmark with no crest — the only asset that survives a 1200×630 OG crop without the crest being cut | Unassigned; no OG card exists |
| `veldrath-final-vertical-transparent.png` | Portrait lockup | Unassigned; nothing in the web app is portrait |
| `veldrath-full-logo-transparent.png` | Square lockup, wordmark across the crest | Competes with the horizontal lockup and reads worse at square proportions |
| `veldrath-final-circular-transparent.png`, `veldrath-final-square-v-transparent.png` | Medallion with a large V across the lower half | One concept in two formats; busy below ~200px |
| `veldrath-v-molten-…`, `molten_metallic_v_…`, `molten_v_with_fiery_aura_…` | Three further V treatments | Four files for one role; `v-clean` wins on legibility |
| `herald/*` (four files) | Hooded-herald emblems | No seeded organisation, no faction record in content, no emblem slot in the UI — concept art, not lore assets |

**Why leave them.** The only cost of keeping is bytes in a git repository; the only cost of deleting is
a `git log` recovery. Nothing here reaches a player, because nothing references it. What was actually
broken — three files oversampled by up to 16× and no vector master anywhere — is untouched by this
decision and remains the work queue in [§5](#5-work-queue).

**What "left in place" does not mean.** It does not make any of them approved. The
[brand guide](brand-guide.md) still lists them as unassigned, and any page that starts using one adds a
row to that table first. It also does not retire the two gaps above: the V monogram and the install tile
are still the missing small-size mark and install-icon source, and the 180/192/512 icons still have no
master.

---

## 5. Work queue

Ordered by user-visible impact.

| # | Work | Why |
|---|---|---|
| 1 | Vector master of the crest and the wordmark | Every favicon, print and future-size problem is downstream of this |
| 2 | Export crest at 152/228 and hero at 1040 | Removes a 1.61 MB download from auth and roster pages |
| 3 | Export wordmark at 60/90 | Removes a 1.01 MB download from **every** page |
| 4 | Export horizontal lockup at 128/192 | Footer weight |
| 5 | Regenerate the install icons from the vector master, 512 under 60 KB | 583 KB for a rarely-requested file |
| 6 | Produce the OG card (1200 × 630) | No social preview exists today |
| 7 | Assign or drop the two roles the kit already fills — the V monogram and the install tile | The brand guide names a small-size mark and the icons have no master; both files exist unassigned |
| 8 | Empty-state and 404 illustration briefs | Both surfaces render text alone today |

---

## 6. How this was measured

```sh
cd web/client/static/images
file *.png herald/*.png favicon/*.png     # dimensions
stat -c '%s %n' *.png herald/*.png favicon/*.png | sort -n   # weight, ascending

# consumers: any reference from source
grep -rn --include=*.svelte --include=*.ts --include=*.css '/images/' ../../..
```

A file with no hit outside `static/` is unreferenced. Note that `docfx.json` copies a subset into the
published docs site, so an image can also be "used" by the documentation — the two favicon sizes are.
Re-run these commands before acting on [§4](#4-decisions-proposed); weights and consumers both drift.

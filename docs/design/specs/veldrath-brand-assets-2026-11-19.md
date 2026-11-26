## Approved production amendment — 19 November 2026

This amendment supersedes conflicting rebuild instructions in A–C. Derive all five P0 families directly from their named PNG originals, fit proportionally with LANCZOS, and preserve transparency. No generated mark, traced lettering or SVG ships. Crest and wordmark vector masters: **DEFERRED**, not failed. The footer, social card and email use the original horizontal signature. The three generated illustrations retain their approved plates and proofs. No new generations: nine used; one banked.

Keep field 15 raster names and sizes except: **1040px is the hero ceiling** (1560 omitted; the q64 trial exceeds the 180 decimal KB limit; a future required 3× export needs a documented exception). **Apple touch must remain PNG**: use an alpha-preserving 128-colour indexed PNG, likewise at 192; deliver the 512 install tile as `favicon/android-chrome-512x512.webp`. WebP quality: 88 for small crests, wordmarks and signatures; 80 for the hero; 70 for the install tile. Tab: two transparent PNG sizes plus matching ICO frames. No P2.

For derived marks and their compositions, field 8’s flat-token rule does not apply: do not recolour master pixels. Verify decoded dimensions, decimal byte ceilings, transparency (opaque social/email), and side-by-side fidelity to the source at display size. Use the original crest at 80/88, not the withdrawn optical cut. The old blind-read and machine results describe the withdrawn rebuild, not this release. Protocol P still applies to the three illustrations.

## A. Cover sheet (original commission)

| id | class | priority | surface | canvas | exports | references (originals under I) | depends on |
|---|---|---|---|---|---|---|---|
| crest-primary | HYBRID | P0 | Auth, account, roster, creation and landing hero | 2048×2048, 1:1 | SVG; 80/88/96/152/228/1040/1560 | veldrath-final-crest-transparent.png | none |
| wordmark-header | TYPE | P0 | SiteHeader brand link | 720×180, 4:1 | SVG; 240×60/360×90 | veldrath-text-flat-true-transparent.png | none |
| lockup-horizontal | COMPOSE | P0 | SiteFooter | 384×128, 3:1 | SVG; 384×128/576×192 | veldrath-final-horizontal-transparent.png | crest-primary, wordmark-header |
| icon-install | COMPOSE | P0 | iOS home screen and Android install/splash | 512×512, 1:1 | 180/192/512 PNG | veldrath-final-square-v-transparent.png, veldrath-final-crest-transparent.png | crest-primary |
| favicon-monogram | COMPOSE | P0 | Browser tab and legacy icon | 32×32, 1:1 | SVG; 16/32 PNG; ICO | veldrath-v-clean-true-transparent.png | crest-primary |
| social-og | COMPOSE | P1 | Site-wide social preview | 1200×630, 40:21 | 1200×630 PNG | veldrath-final-horizontal-transparent.png | crest-primary, wordmark-header |
| header-email | COMPOSE | P1 | Transactional email masthead | 600×200, 3:1 | 600×200 PNG | veldrath-final-horizontal-transparent.png | lockup-horizontal |
| illustration-error | GENERATE | P1 | 404 / error page | 320×240, 4:3 | 320×240 PNG master/WebP | veldrath-final-crest-transparent.png | none |
| illustration-empty | GENERATE | P1 | Roster, bag and quest log empty states | 240×180, 4:3 | 240×180 PNG master/WebP | veldrath-final-crest-transparent.png | none |
| illustration-lore | GENERATE | P1 | Lore article masthead | 1600×600, 8:3 | 1600×600 PNG master/WebP | herald/the_herald_emblem_of_darkness_transparent.png | none |

**Generations:** 5 first passes (2 plates, 3 illustrations); one targeted re-roll per rejected generation only: 10 maximum of 20. Five compositions cost zero. No speculative sketches.
**SPECULATIVE:** faction emblems excluded: four unused herald originals (D:54–77), no assigned organisation. Defer item art: keep Lucide fallback (S:134–149). Rarity frames are a CSS token decision, not art.
**Retires from delivery:** the three live mark PNGs (D:33–35) and the six existing favicon/install binaries (D:36–41; replace those six in place). Preserve originals for provenance and comparison; no purge of studies.

## B. Per-asset specs

Notation: I = `web/client/static/images/`; S = `docs/design/asset-specs.md`; B = `docs/design/brand-guide.md`; D = `docs/design/asset-inventory.md`. Export names are proposed targets, not claims of existing files. Brace lists expand pairwise. Keep plates, masks, layered sources, build recipe, reference names and generation records in `/home/kung/creative-assets/veldrath/2d/`; do not serve plates. If publishing docs previews, derive from 2× delivery, never use a preview as a reference (S:194–197). All assets static; motion by CSS only, no GIF, APNG or Lottie (S:210–221). This is atmospheric panel art for the SvelteKit client, not entity art (docs/client-ui-architecture.md:6,180).

**P — palette and proof protocol for field 8.** Restricted subset of `docs/design/tokens.json:6–41`; do not edit the tokens (parity contract at :2). Maximum opaque-pixel shares: background/bg-0 #0c0d13 and background/bg-1 #14151f each ≤100%; border/border-strong #3d3f58 ≤50%; gold/gold #c9a84c ≤20%; seal/seal #c0392b ≤10%; text/text #f0ede8 ≤10%. Neutral luminance allowed; ember and all other chromatic tokens 0%. Crimson only emitted inside cracks, cores or smoke, never a surface. No accent, rarity, OAuth, indicator or semantic colours; no green, purple, teal or cyan hues. Quantise subject flats before export. For each delivered raster (rasterise SVG at field 10 size), count sRGB pixels at alpha=255; assign exact token first, otherwise channel spread ≤8/255 to neutral, otherwise nearest listed token only if all RGB channels differ by ≤8/255. Fail if unassigned >2%, any cap exceeded, or forbidden hue deliberately painted. Key colour is not subject colour. Test subject uncomposited as well as final tile. Both grounds = `#14151f` and `#f0ede8`, at 1 CSS pixel per proof pixel. Human comparison is with the named anchor if no predecessor exists.

Size key: use the live `web/client/src/routes/brand.css:273–276,363–371,993–998`: header `height: 30px`; hero `width: min(520px, 80vw)`, `opacity: 0.18`, `drop-shadow(0 0 40px rgba(192, 57, 43, 0.35))`; auth 76×76 with `drop-shadow(0 0 18px rgba(192, 57, 43, 0.35))`. Keep both shadows in CSS. The 4:1 wordmark uses a vector construction box six times its 120×30 header box; its texture and the crest tracing plate use the commissioned 2048² working resolution, not download sizes. The 3:1 footer box is twice its proposed 192×64 layout. P1 canvases follow S:89–105, not the crest density ladder.

### crest-primary

1. **id** — crest-primary
2. **class** — HYBRID
3. **role** — Identify the account flow and anchor the landing headline without competing with it.
4. **surface** — Auth, account, roster, creation and landing hero (docs/design/asset-inventory.md:34; web/client/src/routes/brand.css:363–371,993–998; docs/design/asset-specs.md:80–84)
5. **priority** — P0
6. **canvas** — 2048×2048, 1:1; centred tips tangent to 96% square; layout clearance 25% of ink height outside box
7. **composition** — Mirror ring and spikes about vertical axis; hood above compass; ring engravings are cuts, not glyphs. No copy inside ring. Keep independently editable V-only group for 40/44px and favicon.
8. **palette** — P below; test final opaque pixels, including type and tile.
9. **lighting** — Cool upper-left facets; warm fissure core below hood.
10. **silhouette** — 48px: hood notch, ring and four tips; at 40/44px use V-only group. At 520px/0.18: ring and hood remain separate.
11. **negatives** — No gradient, shadow, or readable inscription
12. **references** — `web/client/static/images/veldrath-final-crest-transparent.png` — cardinal silhouette and iron-to-stone balance. Pass these originals as `reference_image_urls`.
13. **generation** — gpt-image-2-high; one variation, 1:1, 2048×2048. Trace keyed flat regions with vtracer color_precision=4, filter_speckle=12, mode=spline. Merge same-fill islands; rebuild circular arcs, mirror barbs, delete hidden paths. Draw V group by hand against V study (inventory:59). Simplify until SVG passes; rasterise only this SVG, including V-only group for 80/88.

    > Use veldrath-final-crest-transparent.png as silhouette reference. Make a square flat tracing plate: blackened iron ring, four cardinal barbs, engraved broken notches, faceless hood in spiked armour on fractured basalt, four-point compass at foot. Cut crisp upper-left facets in #14151f and #3d3f58; aged pitted gold #c9a84c marks edges only. A narrow #c0392b fissure lights the compass core. Keep hard boundaries, no gradients or halos. Centre all four tips on plain white matte. No letters, numbers or written runes. Preserve the hood opening at roster size.

    Negative prompt: No gradient, shadow, or readable inscription; no lettering, words, numbers, runes-as-text, glow, bloom, drop-shadow or vignette.
14. **matting** — Generate on uniform neutral white #ffffff (removable matte only). Flood-key from corners, unmatte edge RGB, remove enclosed matte by mask; inspect both grounds at 1×. Keep canvas registration. No glow, bloom, drop-shadow or vignette baked in.
15. **exports** — All names relative to I. `crest-primary.svg` | scalable | SVG ≤ 40 KB; `crest-primary-{80,88,96,152,228}.webp` | corresponding square | WebP (152px ≤ 60 KB); `crest-hero-{1040,1560}.webp` | corresponding square | WebP (1040px ≤ 180 KB WebP). S:56–58; C2.
16. **accept** — 1. Verify field 15 names, dimensions, formats and bytes. 2. Pass P and field 11 exclusions. 3. At 1× on both grounds pass field 10 and test hero with actual CSS shadow, also at the 1200px and 900px breakpoints (web/client/src/routes/brand.css:456,466); no rectangular edge. 4. Human side-by-side with field 12 original(s): same realm in material, weight and aggression; record yes/no. No overrides of failed numeric tests.
17. **blocks** — none

### wordmark-header

1. **id** — wordmark-header
2. **class** — TYPE
3. **role** — Make VELDRATH readable in the sticky header.
4. **surface** — SiteHeader brand link (web/client/src/routes/brand.css:273–276; docs/design/asset-inventory.md:33)
5. **priority** — P0
6. **canvas** — 720×180, 4:1; type within 96% width and 96% height; layout clearance 25% of ink height outside box
7. **composition** — Single baseline, eight capitals; preserve open R and A counters. No UI copy inside. Smoke sits behind terminals within safe area.
8. **palette** — P below; test final opaque pixels, including type and tile.
9. **lighting** — Upper-left warm metal cuts; fissures inside strokes, no outer halo.
10. **silhouette** — 30px tall: read all eight letters in order; also check 24px minimum (B:96).
11. **negatives** — No model letters, continuous red outline, or unmodified font specimen
12. **references** — `web/client/static/images/veldrath-text-flat-true-transparent.png` — terminal shape and infill scale. Pass these originals as `reference_image_urls`.
13. **generation** — gpt-image-2-high; one variation, 1:1, 2048×2048. Texture only. Set VELDRATH in Cinzel Decorative Regular (docs/design-system.md:304); convert to paths, clip plate inside; edit V/A/T terminals into short inward barbs without closing counters. Trace texture into sparse flat paths and clip to edited outlines; add separate hard-edged tapered ember-smoke paths. Export self-contained SVG without bitmap or font dependency (S:36; bespoke anchor B:35–37).

    > Use veldrath-text-flat-true-transparent.png only as a material sample. Produce a square full-bleed flat texture plate, not a sign: pitted blackened metal #14151f, broad oxidised neutral facets, interrupted veins of aged gold #c9a84c and thin crimson #c0392b emitted from deep fractures. Light the cut edges from the upper left. Distribute fissures throughout so narrow crops retain the metal grain. No lettering, letter-shaped strokes, words, numbers, written runes, silhouettes, smoke, border, gradient or baked lighting effects.

    Negative prompt: No model letters, continuous red outline, or unmodified font specimen; no lettering, words, numbers, runes-as-text, glow, bloom, drop-shadow or vignette.
14. **matting** — Full-bleed opaque texture, no exterior matte; clip by glyph paths, transparent elsewhere. No glow, bloom, drop-shadow or vignette baked in.
15. **exports** — All names relative to I. `wordmark-header.svg` | 720×180 viewBox | SVG ≤ 40 KB; `wordmark-header-{60,90}.webp` | 240×60, 360×90 | WebP (90px ≤ 40 KB). S:56,59,78.
16. **accept** — 1. Verify field 15 names, dimensions, formats and bytes. 2. Pass P and field 11 exclusions. 3. At 1× on both grounds pass field 10 and two readers transcribe VELDRATH without a hint; inspect SVG: no text/image nodes. 4. Human side-by-side with field 12 original(s): same realm in material, weight and aggression; record yes/no. No overrides of failed numeric tests.
17. **blocks** — none

### lockup-horizontal

1. **id** — lockup-horizontal
2. **class** — COMPOSE
3. **role** — Sign the footer with one horizontal reading order.
4. **surface** — SiteFooter (D:35; S:79)
5. **priority** — P0
6. **canvas** — 384×128, 3:1; reserve external 25% crest height
7. **composition** — Crest left, wordmark right; no tagline; keep gap one quarter crest height.
8. **palette** — P below; test final opaque pixels, including type and tile.
9. **lighting** — Inherit upper-left facets and lower compass core.
10. **silhouette** — 64px high: distinguish hood then read VELDRATH; width 192px exceeds 160px minimum (B:95).
11. **negatives** — No stacking, tagline or stretched lettering
12. **references** — `web/client/static/images/veldrath-final-horizontal-transparent.png` — crest-to-letter relationship. Compare only; build from approved paths, not these pixels.
13. **generation** — Use 128-high crest box at left, gap 32, fit proportional wordmark into remaining 224 wide; vertically centre both. Inline approved paths with unique clip IDs; rasterise once per size.
14. **matting** — n/a — COMPOSE; render vector inputs directly.
15. **exports** — All names relative to I. `lockup-horizontal.svg` | 384×128 viewBox | SVG ≤ 40 KB; `lockup-horizontal-{128,192}.webp` | 384×128,576×192 | WebP (192px ≤ 60 KB). S:56,60,79.
16. **accept** — 1. Verify field 15 names, dimensions, formats and bytes. 2. Pass P and field 11 exclusions. 3. At 1× on both grounds pass field 10 and confirm identical baseline alignment and gap ratio across exports. 4. Human side-by-side with field 12 original(s): same realm in material, weight and aggression; record yes/no. No overrides of failed numeric tests.
17. **blocks** — crest-primary, wordmark-header

### icon-install

1. **id** — icon-install
2. **class** — COMPOSE
3. **role** — Identify the app on an opaque tile.
4. **surface** — iOS home screen and Android install/splash (D:38–40; S:126–128)
5. **priority** — P0
6. **canvas** — 512×512, 1:1; crest fits central two thirds
7. **composition** — Centre rebuilt ring on uninterrupted square ground; reserve 25% crest height around it; no copy.
8. **palette** — P below; test final opaque pixels, including type and tile.
9. **lighting** — Inherit upper-left key and compass ember.
10. **silhouette** — 180px: four separated tips around hood after rounded icon mask.
11. **negatives** — No white corners, text, or pre-rounded tile
12. **references** — `web/client/static/images/veldrath-final-square-v-transparent.png` — square field proportions; `web/client/static/images/veldrath-final-crest-transparent.png` — cardinal silhouette and iron-to-stone balance. Compare only; build from approved paths, not these pixels.
13. **generation** — Fill square with background/bg-0; fit crest proportionally within central two thirds; rasterise each size independently.
14. **matting** — n/a — COMPOSE; render vector inputs directly.
15. **exports** — I/favicon/: `apple-touch-icon.png` | 180²; `android-chrome-192x192.png` | 192²; `android-chrome-512x512.png` | 512²; opaque PNG; Favicon 512 ≤ 60 KB (S:61,126–128; C2).
16. **accept** — 1. Verify field 15 names, dimensions, formats and bytes. 2. Pass P and field 11 exclusions. 3. At 1× on both grounds pass field 10 and alpha is 255 at every corner and no tip is clipped. 4. Human side-by-side with field 12 original(s): same realm in material, weight and aggression; record yes/no. No overrides of failed numeric tests.
17. **blocks** — crest-primary

### favicon-monogram

1. **id** — favicon-monogram
2. **class** — COMPOSE
3. **role** — Identify a tab with the V rather than an illegible miniature hood.
4. **surface** — Browser tab and legacy icon (D:36–37,41; S:124–129)
5. **priority** — P0
6. **canvas** — 32×32, 1:1; V in central 75%; optical exception to clear space
7. **composition** — One open V, no ring or copy; inner bevel pale against dark, dark outer edge against light.
8. **palette** — P below; test final opaque pixels, including type and tile.
9. **lighting** — Upper-left pale cut; no emission.
10. **silhouette** — 16px: V notch and both shoulders visible in silhouette.
11. **negatives** — No herald, fissure texture, or thin filigree
12. **references** — `web/client/static/images/veldrath-v-clean-true-transparent.png` — V notch and taper. Compare only; build from approved paths, not these pixels.
13. **generation** — Isolate crest master V group. Snap extremities to 16px grid; maintain two-pixel open notch at 16. Render at both target sizes; package both frames in ICO.
14. **matting** — n/a — COMPOSE; render vector inputs directly.
15. **exports** — I/favicon/: `favicon-monogram.svg` | 32² viewBox | SVG ≤ 40 KB (S:56); `favicon-{16x16,32x32}.png` | matching square PNG; `favicon.ico` | 16² + 32² ICO. S:61,124–129; C2.
16. **accept** — 1. Verify field 15 names, dimensions, formats and bytes. 2. Pass P and field 11 exclusions. 3. At 1× on both grounds pass field 10 and measure notch ≥2px wide at half height and inspect both ICO frames. 4. Human side-by-side with field 12 original(s): same realm in material, weight and aggression; record yes/no. No overrides of failed numeric tests.
17. **blocks** — crest-primary

### social-og

1. **id** — social-og
2. **class** — COMPOSE
3. **role** — Identify a shared link without relying on live text or filters.
4. **surface** — Site-wide social preview (S:89–92; D:122)
5. **priority** — P1
6. **canvas** — 1200×630, 40:21; safe inset 80px
7. **composition** — One centred horizontal signature; no other text. Ground extends through crop.
8. **palette** — P below; test final opaque pixels, including type and tile.
9. **lighting** — Inherit upper-left key; crimson confined to compass and letter cuts.
10. **silhouette** — 1200×630: VELDRATH and four tips survive 80px edge crop.
11. **negatives** — No tagline, browser chrome, or decorative border
12. **references** — `web/client/static/images/veldrath-final-horizontal-transparent.png` — crest-to-letter relationship. Compare only; build from approved paths, not these pixels.
13. **generation** — Fill background/bg-0. Centre 3:1 signature built from crest and wordmark as in footer, width 75% of card. Flatten opaque once in sRGB.
14. **matting** — n/a — COMPOSE; render vector inputs directly.
15. **exports** — All names relative to I. `social-og.png` | 1200×630 | opaque PNG ≤ 200 KB (S:62,91).
16. **accept** — 1. Verify field 15 names, dimensions, formats and bytes. 2. Pass P and field 11 exclusions. 3. At 1× on both grounds pass field 10 and all non-ground pixels lie inside safe rectangle [80,1120)×[80,550). 4. Human side-by-side with field 12 original(s): same realm in material, weight and aggression; record yes/no. No overrides of failed numeric tests.
17. **blocks** — crest-primary, wordmark-header

### header-email

1. **id** — header-email
2. **class** — COMPOSE
3. **role** — Sign account mail even when the client strips styling.
4. **surface** — Transactional email masthead (S:94–97)
5. **priority** — P1
6. **canvas** — 600×200, 3:1; centre signature at 2/3 canvas width
7. **composition** — Horizontal crest and type on flat ground; clear every edge by ≥25% of rendered crest height. No mail copy in image.
8. **palette** — P below; test final opaque pixels, including type and tile.
9. **lighting** — Inherited facets only; no transparency-dependent light.
10. **silhouette** — 600×200: identify crest and read VELDRATH with CSS disabled.
11. **negatives** — No gradient, filter dependency, or message text
12. **references** — `web/client/static/images/veldrath-final-horizontal-transparent.png` — crest-to-letter relationship. Compare only; build from approved paths, not these pixels.
13. **generation** — Centre approved lockup at 400px wide without stretching. Composite on opaque background/bg-0 #0c0d13 and encode indexed or truecolour PNG.
14. **matting** — n/a — COMPOSE; render vector inputs directly.
15. **exports** — All names relative to I. `header-email.png` | 600×200 | opaque PNG ≤ 60 KB (S:65,96–97).
16. **accept** — 1. Verify field 15 names, dimensions, formats and bytes. 2. Pass P and field 11 exclusions. 3. At 1× on both grounds pass field 10 and edge pixels equal #0c0d13 and filter-free proof matches export. 4. Human side-by-side with field 12 original(s): same realm in material, weight and aggression; record yes/no. No overrides of failed numeric tests.
17. **blocks** — lockup-horizontal

### illustration-error

1. **id** — illustration-error
2. **class** — GENERATE
3. **role** — Show an interrupted passage without inventing a location.
4. **surface** — 404 / error page (S:104; D:125)
5. **priority** — P1
6. **canvas** — 320×240, 4:3; outer 5% clear; reserve copy zone below
7. **composition** — Broken iron threshold in lower half; top third clear for error copy.
8. **palette** — P below; test final opaque pixels, including type and tile.
9. **lighting** — Cool upper-left; single warm fracture below copy.
10. **silhouette** — 320×240: visible gap between broken uprights.
11. **negatives** — No figure, lettering, or painted crimson field
12. **references** — `web/client/static/images/veldrath-final-crest-transparent.png` — cardinal silhouette and iron-to-stone balance. Pass these originals as `reference_image_urls`.
13. **generation** — gpt-image-2-high; one variation; 4:3; output 320×240; preserve aspect when resampling to final box.

    > Use veldrath-final-crest-transparent.png for material and edge wear, not as a logo to reproduce. Compose two broken jambs framing an impassable basalt threshold; place both below the top third, with a wide break down the centre. Frame as a 4:3 landscape. Paint broad planes of basalt #14151f and blackened steel #3d3f58, sparse aged gold #c9a84c and one pin of crimson #c0392b emitted inside a crack. Use cool upper-left light with no halo. Isolate on plain white matte. No figure, lettering, numbers, readable runes, drop shadow, bloom or vignette.

    Negative prompt: No figure, lettering, or painted crimson field; no lettering, words, numbers, runes-as-text, glow, bloom, drop-shadow or vignette.
14. **matting** — Generate on uniform neutral white #ffffff (removable matte only). Flood-key from corners, unmatte edge RGB, remove enclosed matte by mask; inspect both grounds at 1×. Keep canvas registration. No glow, bloom, drop-shadow or vignette baked in.
15. **exports** — All names relative to I. `illustration-error.webp` | 320×240 | alpha WebP; S:63 large illustration ≤ 250 KB WebP (C2). Working `illustration-error-master.png` | same dimensions | PNG-24 + alpha, project 2d/ only; not served.
16. **accept** — 1. Verify field 15 names, dimensions, formats and bytes. 2. Pass P and field 11 exclusions. 3. At 1× on both grounds pass field 10 and top third has zero subject alpha. 4. Human side-by-side with field 12 original(s): same realm in material, weight and aggression; record yes/no. No overrides of failed numeric tests.
17. **blocks** — none

### illustration-empty

1. **id** — illustration-empty
2. **class** — GENERATE
3. **role** — Use one empty mounting to signal absence across all three panels.
4. **surface** — Roster, bag and quest log empty states (S:103; docs/client-ui-architecture.md:71,100)
5. **priority** — P1
6. **canvas** — 240×180, 4:3; outer 5% clear; reserve copy zone below
7. **composition** — Low empty iron cradle, lower third only; keep upper two thirds free for copy.
8. **palette** — P below; test final opaque pixels, including type and tile.
9. **lighting** — Cool upper-left; single warm fracture below copy.
10. **silhouette** — 240×180: two hooks around a visibly vacant centre.
11. **negatives** — No figure, lettering, or painted crimson field
12. **references** — `web/client/static/images/veldrath-final-crest-transparent.png` — cardinal silhouette and iron-to-stone balance. Pass these originals as `reference_image_urls`.
13. **generation** — gpt-image-2-high; one variation; 4:3; output 240×180; preserve aspect when resampling to final box.

    > Use veldrath-final-crest-transparent.png for material and edge wear, not as a logo to reproduce. Compose an empty shallow iron mounting with two upturned hooks and no object; keep it within the lower third, leaving an uninterrupted field for explanatory copy. Frame as a 4:3 landscape. Paint broad planes of basalt #14151f and blackened steel #3d3f58, sparse aged gold #c9a84c and one pin of crimson #c0392b emitted inside a crack. Use cool upper-left light with no halo. Isolate on plain white matte. No figure, lettering, numbers, readable runes, drop shadow, bloom or vignette.

    Negative prompt: No figure, lettering, or painted crimson field; no lettering, words, numbers, runes-as-text, glow, bloom, drop-shadow or vignette.
14. **matting** — Generate on uniform neutral white #ffffff (removable matte only). Flood-key from corners, unmatte edge RGB, remove enclosed matte by mask; inspect both grounds at 1×. Keep canvas registration. No glow, bloom, drop-shadow or vignette baked in.
15. **exports** — All names relative to I. `illustration-empty.webp` | 240×180 | alpha WebP; S:63 large illustration ≤ 250 KB WebP (C2). Working `illustration-empty-master.png` | same dimensions | PNG-24 + alpha, project 2d/ only; not served.
16. **accept** — 1. Verify field 15 names, dimensions, formats and bytes. 2. Pass P and field 11 exclusions. 3. At 1× on both grounds pass field 10 and upper two thirds have zero subject alpha. 4. Human side-by-side with field 12 original(s): same realm in material, weight and aggression; record yes/no. No overrides of failed numeric tests.
17. **blocks** — none

### illustration-lore

1. **id** — illustration-lore
2. **class** — GENERATE
3. **role** — Set a material backdrop for a lore title without naming a faction.
4. **surface** — Lore article masthead (S:105)
5. **priority** — P1
6. **canvas** — 1600×600, 8:3; outer 5% clear; reserve copy zone below
7. **composition** — Basalt relief across bottom three fifths; top 40% quiet for title overlay.
8. **palette** — P below; test final opaque pixels, including type and tile.
9. **lighting** — Cool upper-left; single warm fracture below copy.
10. **silhouette** — 1600×600: ridgeline distinct below the title zone.
11. **negatives** — No figure, lettering, or painted crimson field
12. **references** — `web/client/static/images/herald/the_herald_emblem_of_darkness_transparent.png` — engraved relief and horizontal massing. Pass these originals as `reference_image_urls`.
13. **generation** — gpt-image-2-high; one variation; 8:3; output 1600×600; preserve aspect when resampling to final box.

    > Use herald/the_herald_emblem_of_darkness_transparent.png for material and edge wear, not as a logo to reproduce. Compose a low band of chipped basalt relief held by pitted iron clamps, a single interrupted gold seam; keep the upper forty per cent empty for a dark title overlay. Frame as a 8:3 landscape. Paint broad planes of basalt #14151f and blackened steel #3d3f58, sparse aged gold #c9a84c and one pin of crimson #c0392b emitted inside a crack. Use cool upper-left light with no halo. Isolate on plain white matte. No figure, lettering, numbers, readable runes, drop shadow, bloom or vignette.

    Negative prompt: No figure, lettering, or painted crimson field; no lettering, words, numbers, runes-as-text, glow, bloom, drop-shadow or vignette.
14. **matting** — Generate on uniform neutral white #ffffff (removable matte only). Flood-key from corners, unmatte edge RGB, remove enclosed matte by mask; inspect both grounds at 1×. Keep canvas registration. No glow, bloom, drop-shadow or vignette baked in.
15. **exports** — All names relative to I. `illustration-lore.webp` | 1600×600 | alpha WebP; S:63 large illustration ≤ 250 KB WebP (C2). Working `illustration-lore-master.png` | same dimensions | PNG-24 + alpha, project 2d/ only; not served.
16. **accept** — 1. Verify field 15 names, dimensions, formats and bytes. 2. Pass P and field 11 exclusions. 3. At 1× on both grounds pass field 10 and top 240 rows have zero subject alpha; white title proof over bg-1 plus black 50% overlay reaches 4.5:1. 4. Human side-by-side with field 12 original(s): same realm in material, weight and aggression; record yes/no. No overrides of failed numeric tests.
17. **blocks** — none

## C. Open questions

| OPEN QUESTION | Blocks | Default if unanswered |
|---|---|---|
| C1. Which mark at 40/44px? The surface contract calls both crests (S:81–83); the guide forbids the full herald below 48px (B:94). | Small-mark content, not export sizes | Deliver the V-only optical cut from the crest master at 80/88. Keep all seven requested raster sizes despite S:86–87; bind the SVG or a correctly sized fallback, not all seven at once. Use the V in the tab per commission, rather than the ring suggested at S:131. |
| C2. Does §3 have separate limits for every density, the ICO or small illustrations? S:55–65 names only benchmark files; the density labels for the 90px wordmark and 192px lockup differ from the 30/64px rendered sizes. | Sign-off for unlisted sizes | Code wins for rendered dimensions; use exact pixel sizes, not the density labels. Apply the same family's published ceiling to every unlisted delivery size (favicon/ICO use Favicon 512; small illustrations use large illustration). Record actual bytes per file, using decimal KB; no extra budget for 3×. Working masters not shipped. |
| C3. Where will the mail, error, empty and lore views wire their images, and what copy will overlay them? S:94–105 establishes surfaces, not template paths or final copy. | Integration, not drawing | Use the stable surface names above; stage the proposed exports under I. Use live HTML for copy and a single shared empty-state asset. Opaque social and mail composites are distribution exceptions to S:42, not reusable marks. |
| C4. Are bespoke outlines acceptable if based on Cinzel Decorative? B:35–37 rejects a plain re-typeset. | Wordmark approval | Modify outlines against the original; keep the texture and smoke editable in the working master. No generated letters. Reject if the two-reader test or human comparison fails. |

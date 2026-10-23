---
name: ui-development
description: Use when building or changing the app UI in the client — Skeleton design system, Bits UI headless components, Lucide/Simple Icons, design tokens, or any component in `client/src/lib/components`.
---

# UI Development

## When to use

- Building or restyling pages (landing, login, register, dashboard).
- Adding components from Skeleton, Bits UI, Lucide, or Simple Icons.
- Working with design tokens, the `app` theme, dark mode, or Tailwind in the client.

## When NOT to use

- Route/load/action/server logic, runes state logic, or build config — use `sveltekit-development`.
- Non-client work (engine, server rooms, schema) — use the matching skill.

## References

- [`llms/skeletondev/llms-full.txt`](../../../llms/skeletondev/llms-full.txt) — Skeleton v5 design system (themes, tokens, color pairings,
  presets, typography, Svelte components, Tailwind utilities).
- [`llms/bitsui/llms-full.txt`](../../../llms/bitsui/llms-full.txt) — **complete Bits UI docs** (all components + type helpers +
  utilities; the offline reference — see the Key facts section below).
- [`llms/lucide/llms.txt`](../../../llms/lucide/llms.txt) — Lucide docs index (lucide.dev publishes an index only); the authoritative
  icon inventory is the installed package `node_modules/@lucide/svelte/dist/icons/*` (kebab-case names,
  imported per-icon via `@lucide/svelte/icons/<kebab-name>`).
- [`llms/tailwindcss/llms.txt`](../../../llms/tailwindcss/llms.txt) — Tailwind v4 (CSS-first `@theme`, `@utility`, `@variant`).
- [`AGENTS.md`](../../../AGENTS.md) → **Web UI & design system** section + **Conventions** (Web UI rules).
- [`client/src/routes/layout.css`](../../../client/src/routes/layout.css) — Tailwind + Skeleton entry (theme imports).
- [`client/src/routes/theme.css`](../../../client/src/routes/theme.css) — the custom Skeleton `app` theme (design tokens).
- [`client/src/app.html`](../../../client/src/app.html) — `data-theme="app"` on `<html>`.

## Key facts (grounded in `llms/skeletondev/llms-full.txt`)

- **Skeleton v5 on Tailwind 4**: no `tailwind.config.js`; the entry is `client/src/routes/layout.css`
  (`@import 'tailwindcss'` + `@plugin` + `@import '@skeletonlabs/skeleton'` +
  `@import '@skeletonlabs/skeleton-svelte'` + `@import './theme'`).
- **Theme activation**: `data-theme="app"` on `<html>` (in `client/src/app.html`); tokens live in
  `client/src/routes/theme.css`.
- **Color tokens**: `{property}-{color}-{shade}` with colors `primary`, `secondary`, `tertiary`,
  `success`, `warning`, `error`, `surface` (shades 50–950) and contrast variants
  (`{property}-{color}-contrast-{shade}`).
- **Color pairings** (dark mode, `light-dark()`): `{property}-{color}-{lightShade}-{darkShade}`
  (e.g. `bg-surface-50-950`, `text-primary-300-700`). Use these so light/dark works without extra
  classes. Shade 500 has no pairing.
- **Presets**: `preset-filled`, `preset-tonal`, `preset-outlined` (+ `-primary`/`-secondary`/
  `-success`/`-warning`/`-error`/`-surface` variants and light-dark pairings).
- **Element sizes**: `size-elem-{xs..9xl}`, `w-elem-*`, `h-elem-*`, `p-elem-*`; icons inside Skeleton
  `btn`/`badge`/`chip` size automatically.
- **Buttons**: `btn` + `preset-*` + `btn-{size}`; **badges**: `badge` + `preset-*`; **cards**: `card`
  + `preset-*`; **forms**: `label` + `label-text` + `input`/`select`/`field-group` + `field-{size}`.
- **Svelte components** (`@skeletonlabs/skeleton-svelte`): granular `.Root`/`.Trigger`/... children,
  `class` prop for utilities, Zag.js data-model pattern (`checked` / `onCheckedChange`).
- **Skeleton Svelte v5 inventory** (check it before reaching for Bits or hand-rolled markup):
  `AppBar`, `Navigation`, `Avatar`, `Progress`, `Tabs`, `Dialog`, `Slider`, `Tooltip`, `Popover`,
  `Collapsible`, `SegmentedControl`, `Accordion`, `Combobox`, `Listbox`, `RatingGroup`, `Switch`,
  `Steps`, `Toast`, `ToggleGroup`, `TreeView`, `Carousel`, `Marquee`, `DatePicker`, `FileUpload`,
  `FloatingPanel`, `Pagination`, `QRCode`, `TagsInput`, `Menu`, `LocaleProvider`, `Portal`. Prefer the
  styled Skeleton component for chrome/layout; use Bits UI only where Skeleton lacks a fit. Also check
  `skeleton-common` CSS utilities (`mask`/`mask-circle` for avatars, `chip`, `preset-*`, `size-elem-*`).
- **Bits UI** is headless: unstyled; provide your own classes; see the Bits UI Key facts below.
- **Dark mode** uses Tailwind's color scheme + Skeleton pairings; keep focus/outline styles intact
  (accessibility).

## Key facts (grounded in `llms/bitsui/llms-full.txt`)

- **Headless primitives**: unstyled by design (bring your own styles via `class` props / `data-*`
  attributes); WAI-ARIA compliant with keyboard navigation + focus management built in. Import
  named exports from `"bits-ui"` (e.g. `import { Tabs } from "bits-ui"`).
- **Granular `.Root`/`.Trigger`/`.Content` composition** — every component is built from parts:
  - Tabs: `Tabs.Root` → `Tabs.List` (tab bar) → `Tabs.Trigger value="..."` (one per tab) →
    `Tabs.Content value="..."` (the panel). The `value` props on `Tabs.Trigger`/`Tabs.Content` are
    **required** and must match the active value on `Tabs.Root`.
  - Dialog/Popover: `Dialog.Root`/`Dialog.Trigger`/`Dialog.Portal`/`Dialog.Overlay`/
    `Dialog.Content`/`Dialog.Close` (plus `Dialog.Title`/`Dialog.Description`; Popover adds
    `Popover.Arrow`).
  - Select: `Select.Root`/`Select.Trigger`/`Select.Value`/`Select.Content`/`Select.Item`.
- **`onValueChange` emits the raw value, not an event**: `Tabs.Root`'s `onValueChange` is
  `(value: string) => void` — it receives the active tab's value string directly. Do **not** read
  `event.currentTarget`. The same raw-value pattern holds across components: `Accordion`/`Select`/
  `Combobox`/`ToggleGroup` (`string` or `string[]`), `Slider`/`RatingGroup` (`number`),
  `Checkbox`/`RadioGroup`/`Menubar` (`string`/`string[]`).
- **Controlled props pattern**: state is a `value` prop ($bindable) paired with `onValueChange`.
  Use `bind:value` for two-way binding; use a Svelte function binding `bind:value={getValue, setValue}`
  for fully controlled state. Prefer `bind:value` for state sync; use `onValueChange` for side effects.
- **Tabs gotchas**: `activationMode` is `'automatic'` by default (activates on focus) — set
  `'manual'` to require a press. `orientation` (default `'horizontal'`) drives arrow-key nav;
  `loop` (default `true`) wraps keyboard nav.
- **Slider**: `onValueChange` fires while dragging; `onValueCommit` fires when the user releases the
  thumb.
- Full per-component API tables (props, `$bindable`s, `data-*` attributes, examples) are in
  `llms/bitsui/llms-full.txt` — look up the component section (`# <Component> Documentation`).

## Steps

1. Read the relevant section of `llms/skeletondev/llms-full.txt` or `llms/bitsui/llms-full.txt`
   before writing markup.
2. Use Skeleton tokens/presets + Tailwind utilities; no hardcoded colors, no `<style>` blocks, no
   `style=` attributes.
3. Use Lucide for general icons (`@lucide/svelte` per-icon imports like
   `import Home from '@lucide/svelte/icons/home'`), `size-elem-*` sizing; Simple Icons only for
   brand marks. No emoji as UI icons.
4. Place components per org rules: `$lib/components/site/*`, `$lib/components/ui/*`, and
   feature folders under `$lib/components/`.
5. Keep the read-only contract: components read state and send typed commands only.
6. Verify: `npm run check --workspace client` + `npm run lint --workspace client` +
   `npm run test:unit --workspace client`.

## Rules

- Never hardcode colors/radii — Skeleton tokens and presets only.
- No custom CSS or inline styles unless absolutely necessary; justify + centralize any exception
  (e.g. keyframes in `layout.css`).
- No emoji as UI icons; no new ad-hoc design tokens without documenting them in the theme file.
- Svelte 5 runes only — `$props()`/`$state`/`$derived`; no legacy `export let` or `$:`.
- Don't disable focus rings or keyboard operability.

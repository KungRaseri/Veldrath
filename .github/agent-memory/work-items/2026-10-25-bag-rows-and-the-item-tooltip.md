---
kind: work-item
date: 2026-10-25
area: web
title: "Bag rows and the item tooltip"
---

### Session-71b (2026-10-25) — Bag rows and the item tooltip

- **Bag rows are three lines now**: `.bag-row-head` (small muted type icon + the name in its rarity
  colour + stack count), `.bag-row-status` (equipped badge + rarity/type/slot/durability, upper-case
  micro type) and `.bag-row-actions` (icon-only buttons). The icon is deliberately secondary — do not
  put it back in front as the lead element.
- **`ItemTooltip.svelte`** is the ARPG-style hover card: fixed-position beside the hovered element
  (`getBoundingClientRect`), flips to the left column edge and clamps vertically, `role="tooltip"`,
  `pointer-events: none` so it never eats clicks. It renders the rarity-coloured name, type/slot,
  italic description, damage/armour/durability/effect/state rows, bonuses, a Requires block that turns
  `--vds-danger` when the character's level/attributes fall short, and a price/stack footer.
- Hover **and** keyboard focus open it (rows and equipment slots both wire
  `mouseenter`/`mouseleave`/`focusin`/`focusout`); clicking still opens the detail dialog, so the two
  surfaces stay separate (card = glance, dialog = act).

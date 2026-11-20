# Specs

Approved production specs live here. The Kanban board is the *workflow* — questions, drafts and
review comments happen there — but a board card is not a record: comments truncate, worker scratch
directories are deleted, and nothing on the board is versioned. A spec that a future contributor
needs to honour belongs in this directory.

## Naming

    <surface>-<YYYY-MM-DD>.md      e.g. veldrath-brand-assets-2026-11-19.md
    <surface>-<YYYY-MM-DD>.json    machine-checkable records (export matrices, byte tables)

A new date is a new file; do not rewrite history in place. Superseded specs stay, marked `SUPERSEDED`
at the top with a pointer to the replacement.

## Status line

Every spec opens with one of:

- **APPROVED** — cleared by a human; production may follow it.
- **DRAFT** — questions outstanding; do not hand it to production.
- **DEFERRED** — deliberately not being done (e.g. a vector master that needs an illustrator).

## What a spec has to contain

Classify every asset (`DERIVE`, `COMPOSE`, `GENERATE`, `TYPE`) before describing it, because the
class decides the acceptance test: a `DERIVE` is judged on bytes and fidelity to its master, a
`GENERATE` on the palette and the silhouette. Mark anything that needs hand-drawing `DEFERRED`
rather than asking for a trace of painterly art.

## Acceptance instruments

Two commands make the acceptance test mechanical rather than a matter of taste:

```bash
# bytes, decoded dimensions, alpha — exit 1 on any failure
assetkit derive docs/design/specs/asset-derivation-2026-11-19.json

# same-realm gate: silhouette IoU, ink weight, hue shares
assetkit compare <master> <candidate>
```

Calibration on this project's own art: the derived crest scores IoU 98.3 / ink 100.1 / gold 63.3 %
against the master (PASS); the withdrawn traced rebuild scored 65.9 / 69.7 / 13.0 (FAIL).

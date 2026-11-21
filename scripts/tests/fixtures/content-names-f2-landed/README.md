# Fixture — the F2-landed tree

A small, synthetic stand-in for the tree that results when PR #133 (`e7e1188b`, card `t_4e58e1ec`)
lands on top of this check. It is the regression this fixture exists for: on that tree the design
set records its renames in prose, bolding the **retired** names as citations (`**Orneld**`,
`**Orveld**`, `**Kael-Raeth**`, `**Vaelraeth**`), and the naming authority's §7 Session Log bolds
the same retired names. The checker must not read a citation of canon as a coinage — otherwise
`Orveld` (attested, and now also "authored" by the rename note) is reported one letter from the
attested `Gorveld`, and no author can rename their way out of it.

The shapes here are the real shapes: the zone table and derivation table of `wound-spine.md`, the
byname and placement tables of `towns.md`, the root dictionary and §7 Session Log of
`calethic-language.md`. The prose is abridged, not copied, so the fixture does not freeze the
documents it stands in for.

## The rename-note rule

A bold name inside a rename note — a paragraph that opens with a bold `Renamed …`/`Retired …` lede —
is a citation of a past state, so it is reported (`--verbose`) and never compared. That is why the
allowlist holds only `Draekeld`/`Drakeld` here: the retired forms need no permit, and a permit left
in this tree would cover a re-introduced `Orneld`, which is the defect this rule exists to close.

Three negative controls keep the rule from widening:

- a zone-table row coining `Orneld` again fails, and is reported at the table row;
- a bold `**Orneld**` in ordinary prose, outside any rename note, fails;
- a zone-table row placed directly under the rename note (no blank line) fails — the note's range
  stops at a table row, so abutting it is not a way to smuggle a name back in.

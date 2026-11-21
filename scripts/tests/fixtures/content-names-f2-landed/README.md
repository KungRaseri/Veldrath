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

The allowlist records the two permits still live after F2 (`Orneld`/`Orveld` and
`Draekeld`/`Drakeld`); the repository's two F2 permits go stale on the merged tree and are deleted
by a follow-up, so they are not repeated here.

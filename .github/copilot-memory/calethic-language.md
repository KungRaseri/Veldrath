# Calethic Language — Design Reference

> **Status:** Complete (2026-04-15). This is the canonical design reference. The player-facing document is `wiki/Calethic-Language.md`.
>
> **Tonal character:** Hard + Formal — short architectural roots (K, R, TH, V, D) in functional vocabulary; drawn-out compound forms in titles and ceremonial speech.
>
> **Scope:** Sketched conlang — phonology, morphology, basic syntax, root dictionary (~75 entries), full named-entity pass.

---

## 1. Phonology

### 1.1 Consonant Inventory

| Symbol | Description | Notes |
|--------|-------------|-------|
| **K** | Voiceless velar stop | Most frequent initial consonant; architectural, hard |
| **G** | Voiced velar stop | Less common; appears in roots relating to corruption/testing |
| **T** | Voiceless alveolar stop | Clean, administrative |
| **D** | Voiced alveolar stop | Common in place-root compounds |
| **R** | Alveolar trill/rhotic | The most architectural sound; used in sonorant clusters |
| **L** | Lateral approximant | Appears in suffix/compound forms; softer than R |
| **V** | Voiced labiodental fricative | Marks prestige-register words; heard in *vel-, vrath, veld* |
| **TH** | Voiced dental fricative (as in *thee*) | Authority marker; the voiced form gives weight |
| **H** | Voiceless glottal fricative | Appears as root-initial consonant (*hald*), in compound junctions, and in H-mutation corruption forms (K– → H– in frontier speech) |
| **N** | Nasal | Common in suffixes and terminals |
| **M** | Nasal | Less frequent than N |

**Sibilants — Ceremonial Register Only**

| Symbol | Description | Notes |
|--------|-------------|-------|
| **S** | Voiceless alveolar sibilant | Absent from functional vocabulary; appears in Aethori ritual speech only |
| **SH** | Voiceless postalveolar sibilant | Same restriction |

> **Design note:** The ceremonial sibilants exist because Aethori ritual language was shaped by proximity to the being — something non-Calethic in origin seeped into their highest register. Calethic scholars debated whether the Aethori adopted these sounds deliberately (reverence) or whether the being's presence literally altered their phonology. The question was never resolved. What is noted is that the sibilants always appear in *vaeld*-register speech (see §3) — the mode used when approaching the being — and nowhere else.

---

### 1.2 Vowel Inventory

**Short vowels** (functional register — roots, administrative speech):

| Symbol | Sound | Example root |
|--------|-------|--------------|
| a | /a/ as in *father* | *cal, rek, drak* |
| e | /ɛ/ as in *bed* | *vel, eld, grev* |
| i | /ɪ/ as in *bit* | *ith, kith* |
| o | /ɔ/ as in *lot* | *dorn, korn, gorv* |

> Note: /u/ is excluded from the Calethic phoneme inventory. The rounded back vowel is considered a foreign intrusion; no Calethic root ends in or contains /u/.

> **Short o vs. –or diphthong:** The short vowel /o/ appears in roots (*dorn*, *korn*, *gorv*) as a plain vowel followed by r as a consonant (o + r, two distinct phonemes). The long diphthong *–or* /ɔːr/ is primarily a suffix form (*–or* abstract noun, *–ori* keeper). When reading roots, treat 'or' as short-vowel + consonant unless the word is a clear suffix form.

**Long vowels and diphthongs** (formal and title register):

| Symbol | Sound | Appears in |
|--------|-------|------------|
| ae | /eɪ/ as in *day* | *kael, aeth, vael, rael* — the dominant Calethic diphthong |
| ei | /iː/ as in *see* | Rare; appears in high-prestige titles and old formal compounds |
| or | /ɔːr/ as in *ore* | Common in abstract-noun suffix *-or*, the keeper suffix *-ori* |
| ael | /eɪl/ | Compound diphthong in military/sworn-body suffix *-ael* |
| ald | /ɔːld/ | Place suffix *-ald*, material term *daeld* |

---

### 1.3 Syllable Structure

- **Roots:** CVC (most common), CV, or CVCC with allowed final clusters
- **Allowed initial clusters:** VR–, KR–, DR–, THL– (rare) *(TH and SH are digraphs representing single phonemes; THL– occupies two phoneme positions and is within the two-consonant maximum)*
- **Forbidden:** S– or SH– initial (outside ceremonial), three consecutive consonants, any word-initial affricate
- **Maximum initial consonant cluster:** Two consonants *(counts phoneme positions, not characters — digraphs TH and SH each count as one)*
- **Allowed final clusters:** –LD, –LT, –RN, –TH, –VN, –RG, –LN, –ND (but not –KT, –GD, etc.)
- **Medial cluster rule:** No three consecutive consonants at compound junctions. When three consonants would meet, junction reduction applies before the compound is formed.

**Junction reduction (applies at compound boundaries):**

| Cluster | Rule | Formal example | Administrative example |
|---------|------|----------------|-----------------------|
| –d + r– | d drops before r (D-drop) | *vaeld + raeth* → *vaelraeth* | same |
| –l + vr– | V-drop; formal register adds euphonic –d– bridge at l+r juncture | *vel + vrath* → *veldrath* | *vael + vren* → *vaelren* (no bridge) |
| –ld + TH– | D is preserved; epenthetic *–i–* inserted as a liaison vowel between the terminal cluster and the incoming TH–, yielding *–ldi.TH–* | *naeld + thorn* → *Naeldithorn* | (no administrative variant attested) |

> **Canonical example:** *Veldrath* itself derives from *vel* (endure) + *vrath* (force). The junction –l + vr– drops the v, then the world-name form inserts a euphonic –d– bridge at the lateral-rhotic juncture: vel + vrath → vel+rath → *veldrath*. This is preserved in formal register. The administrative register would yield *velrath*, but the world name locked in the formal form.

> **Geminate note:** Geminate consonants (–rr–, –ll–) count as a single consonant for junction purposes. *Korr+veld*: the –rr– terminal is treated as one consonant; –rr + v– = two consonants at junction, legal. The geminate is preserved in the compound.

> **Syllabification and scope:** The three-consonant prohibition applies within a single syllable, not across syllable boundaries. *Veldrath* syllabifies as *vel.drath* — DR– is a legal onset for the second syllable, so no rule is violated. Cross-syllable consonant sequences are always parsed by splitting at the legal onset of the following syllable.

---

### 1.4 Stress Rules

- **Monosyllabic roots:** Stressed on the only syllable (trivially)
- **Compounds (standard):** Stress falls on the leftmost root syllable: **KAEL**-dor, **DORN**-keld
- **Formal titles:** Stress shifts to the final element (the order name): Thornkael-**AE**-tho-ri
- **Long ceremonial forms:** Each root element receives secondary stress; primary stress on the head element (rightmost)

---

### 1.5 Common Corruption Rules

How Calethic words erode into everyday Common speech over generations of oral transmission in outer territories:

| Rule | Direction | Example |
|------|-----------|---------|
| **H-mutation** | Initial K– / KA– → H– in frontier speech | *Kaeldor* → *Haldor* → *Halrow* |
| **TH-shift** | TH → SH in frontier speech (dental fricative softens to palatal) | *Aetheld* → *Aesheld* → *Asheld* |
| **Vowel shortening** | Long *ae, ei* → short *a, e* under stress reduction | *Kaelorneth* → loses form entirely in Common |
| **Final consonant loss** | Word-final *–th, –d, –ld* drop in casual speech | *Aetheld* → *Asheld* → *Asheln* → *Ashlen* |
| **Compound compression** | Multi-root compounds lose one element in long oral usage | *Greveld* → *Greven* (–eld drops and –en added from Common habit) |
| **Replacement** | Common speakers coin descriptive names entirely, discarding the Calethic form | *Korrveld* → *The Droveway*; *Dornkeld* → *The Drowning Pits* |
| **Liquid rotation** | –dr– → –rw– in late-stage frontier speech | *Haldr* → *Halrow* |

> Note on *Aethori*: resists all corruption because the Aethori themselves maintained the form as a prestige/identity term. When the keepers of a name are still present, names don't corrupt. Other Calethic place names had no such keepers.

---

## 2. Morphology

### 2.1 Root Structure

Roots are the atomic units of Calethic. They are:
- 1–2 syllables
- CVC or CV(V) in shape
- Each root carries a single core meaning
- Roots can compound freely to form longer words
- Roots can also be used as prefixes or suffixes depending on position

---

### 2.2 Bound Suffixes

These are not free roots — they only appear in final position, attached to a root, changing its grammatical category:

| Suffix | Category | Core meaning | Example |
|--------|----------|--------------|---------|
| **-eth / -ath** | Polity, territory, collective | "The ordered totality of X" | *Caleth* = "the order/polity of cal" |
| **-ori / -ari** | Keeper, bearer, agent of | "One who holds/maintains X" | *Aethori* = "keepers of the compact" |
| **-eld** | Place, site, specific location | "The site/location of X" | *Aetheld* = "compact-ground" |
| **-ald** | Open ground, plain, visible territory | "The open expanse of X" | *Daeld-Ald* = "the ash-plain"; *Kael-Ald* = "the binding-plain" |
| **-ael / -aen** | Sworn body, military/formal group | "Those who are bound by X" | *Vraen* = from *vren+ael* = "frontier-sworn" |
| **-or** | Abstract noun: state, condition | "The state/condition of X" | *Kaeldor* = "the state of the binding" |
| **-ith** | Instrument, mechanism, purposeful object | "The thing that does X" | *Kaelith* = "the binding instrument" |
| **-ern** | Agent (non-priestly) | "One who does/manages X" | *Rekern* = "an administrator" |
| **-eldh** | Place, site, waypoint (compound variant of *–eld*) | Archaic/formal variant; implies a managed transit point rather than a general site | *Korreldh* = "the passage-place / waypoint" |

> **Variant note — –eth / –ath:** *–eth* is the standard polity/collective suffix used in all formal and administrative contexts. *–ath* is an archaic dialectal variant found only in pre-standardization oral records from outer territories — never in official Caleth administrative documents. No *–ath* compounds survived the Collapse in attested form. New coinages use *–eth*.

> **Variant note — –ael / –aen:** *–ael* is the archaic written form of the sworn-body suffix; *–aen* is the standard spoken and administrative form. *Vraen* (from *vren+–ael*) preserves the written form because it was a formal military designation. New coinages should prefer *–aen* unless the archaic register is intended.

> **Variant note — –ori / –ari:** *–ori* is the priestly agentive form, used only for roles defined by Aethori-tier function. *–ari* is the administrative agentive form for non-priestly offices. *Aethori*, *Raelaethori*, and tier titles use *–ori*; new administrative coinages should use *–ari*.

> **Variant note — –eld / –ald:** Not fully interchangeable. *–eld* marks a specific site or managed location; *–ald* marks open, visible, unobstructed ground. Compare: *Aetheld* (the specific covenant-ground / a defined site); *Kael-Ald* (the binding-plain / an open expanse). Use *–eld* when the place is bounded, specific, or Calethic-administered; use *–ald* when the place is topographically open.

> **Homophony note — or– vs. –or:** The negation prefix *or–* ("without, not") and the abstract-noun suffix *–or* ("state, condition") are phonetically identical. Differentiate by position: *or–* always precedes a root; *–or* always follows a root. *Orveld* = or + veld (negation prefix); *Kaeldor* = kaeld + –or (suffix).

---

### 2.3 Prefixes

Prefixes attach before a root and modify its quality or intensity:

| Prefix | Core meaning | Notes |
|--------|--------------|-------|
| **kael–** | Great, first, paramount | Derives from root *kael* (bind/the first compact). Greatness derives from having been first-bound. The spoken-corruption form *kal–* (found in frontier oral speech, e.g. *Kaldor* as a vowel-reduced form of *Kaeldor*) is not a formal Calethic prefix — it is the first stage of H-mutation (*kael–* → *kal–* → *hal–*). Do not use *kal–* in new formal coinages. |
| **vel– / vael–** | Old, enduring, former | *vel–* = still enduring; *vael–* = formerly, of past-times |
| **dorn– / dor–** | Deep, hidden, below | Also used directionally |
| **hald–** | High, elevated, prominent | Also used directionally |
| **thar–** | True, genuine, oath-bound | Affirms; only used in formal/title contexts |
| **or–** | Negation, reversal, without | The antonym prefix. Common "Unbound" echoes this (*or-veld* → the unconstrained) |
| **naeld–** | Sealed, permanent, closed | Implies no exit or reversal |
| **gorv–** | Corrupted, wrong, altered | Place-name prefix when land was changed by the compact's work |
| **rek–** | Orderly, administered | Administrative prefix |
| **grev–** | Tested, examined, trialled | Historical prefix in sites used for experiments |

> **Cross-reference:** Ordinal/number prefixes (*kel–* = first, *dael–* = second, etc.) are defined in §4.8. They follow the same attachment rules as the prefixes above.

---

### 2.4 Title Construction

Calethic titles are compounds. The reading direction is **right-to-left for meaning** — the head category comes last, the most specific qualifier comes first:

```
[Most specific qualifier] – [Function] – [Order/Category]
```

**Example — Aethori full title:**

```
Thorn  –  kael  –  aethori
witness – great/compact – keepers-of-the-compact
```
→ "The great-compact-witnessing keepers" = "those who witnessed and keep the first compact"

Rendered in speech as one word: **Thornkaelaethori**

Short form used in all practical contexts: **Aethori**

**Tier-qualified forms:**
- Inner circle: `Kel-Thornkaelaethori` ("first-tier, great-compact-witnessing keepers")
- Bound tier: `Vaeln-Aethori` ("the adhering keepers" — those who follow by bloodline)
- Scattered: `Gorv-Aethori` — scholar's post-Collapse classification, not a self-designation ("the broken keepers")

---

### 2.5 Place-Name Compounding

Pattern: `[descriptor or feature root] + [root or type suffix]`

- Descriptor before type: *kael + –eld* → *Kaeleld* (great-ground / binding-ground)
- Feature before type: *dorn + keld* → *Dornkeld* (deep-hollow / the deep pits)
- Two features: *vel + korr* → *Velkorr* (enduring-passage / the old road)

Personal names (rarely surviving in Common) follow: `[function root] + [affiliation root]`
- e.g. *Aethrek* (compact-administrator), *Tharnaek* (true-invoker)

---

### 2.6 Hyphenation Convention

When writing Calethic compounds in analysis or reconstruction, use the following convention:

| Form | When to use | Examples |
|------|-------------|----------|
| **Merged** (no hyphen) | Established proper names — attested words that have solidified as single units | *Kaeldor*, *Aetheld*, *Veldrath*, *Dornkeld* |
| **Hyphenated** | Analytical forms, new coinages, and derivation steps being worked out | *Vel-Kael* (enduring compact), *Hald-Eld* (high ground) |

> When citing a Calethic form that is still under derivation or is being shown as a breakdown rather than a final word, use hyphens. When citing the final attested or canonical form, merge it.

---

## 3. Basic Syntax

### 3.1 Register System

Calethic operates in three registers:

| Register | Usage | Word Order | Vowel Quality |
|---------|-------|------------|---------------|
| **Administrative** | Bureaucratic reports, commands, records | SVO | Short vowels throughout |
| **Formal** | Decrees, titles, declarations | Modifier-before-head; compounds | Long vowels in head position |
| **Vaeld** (ceremonial) | Aethori ritual speech; compact invocation | VOS (verb-first) | Long vowels + sibilants permitted |

### 3.2 Standard Word Order

- **Administrative:** Subject — Verb — Object (*standard, used in all records*)
- **Formal (decree):** [Title] [Verb] [Object] — the speaker's identity precedes the action
- **Vaeld (ritual):** Verb — Object — Subject — the action precedes the actor, echoing the being's pre-eminence

### 3.3 Modifier Position

Calethic modifiers are **pre-nominal** (precede the noun):
- *Kael-Aethori* = "great keepers" (kael modifies Aethori)
- *Vel-Eld* = "old grounds" (vel modifies eld)

In compounds, qualifiers stack **most-specific-first, most-general-last**:
- *Thorn-kael-Aethori* = witness (specific function) + great-compact (qualifier) + keepers (category)

### 3.4 Formal Title Recitation

Full titles are spoken in **descending rank order** — from the most general category to the most specific qualifier. This is the **inverse** of written order.

Written: `Thorn-kael-Aethori`
Spoken in full ceremony: *"Aethori, Thornkael"* → category first, full qualification second.

Short forms drop all but the final (category) element: *Aethori*.

---

## 4. Root Dictionary

### 4.1 Metaphysical / The Compact / The Being

| Root | Core meaning | Extended meanings | Example derived word |
|------|-------------|-------------------|---------------------|
| **aeth** | covenant, compact, the binding agreement | the sacred contract mediating the empire's power | *Aethori* (keepers of the compact), *Aetheld* (compact-ground) |
| **kael** | bind, contain, hold by compact | as prefix: great, first, paramount (the first binding was the foundation of greatness) | *Kaelorneth* (great unmaking), *Kaeldor* (binding-place) |
| **orn** | dissolve, unmake, reverse a binding | the catastrophic end of a structured arrangement | *Kaelorneth* (great unmaking = the Collapse), *Orneth* (the unmaking) |
| **veld** | the unconstrained, the void-beyond, existence without limit | the essential quality of the being; that which cannot be bounded | *Orveld* (the Unconstrained = the being's Calethic name), *Veldrath* (the world name = vel+vrath, via formal junction) |
| **rael** | presence, assertion of existence | a presence that insists on being here; the being's local manifestation | *Raelaethori* (presence-keepers, archaic) |
| **thar** | true, genuine, oath-bound, absolute | affirms; marks things that cannot be unsaid or undone | *Thar-Aethori*, *Tharkael* (the true binding) |
| **naeld** | seal, permanent closure, lock-with-no-key | a closing that is not meant to be opened | *Naeldithorn* (the sealed witnessing), *Naeldkeld* (sealed ground) |
| **gorv** | contaminate, alter, corrupt | the process of contact with *veld*; what happens to land and minds near the compact | *Gorveld* (corrupt-ground — gorv + *–eld*), *Gorv-Aethori* (broken keepers). **Homograph note:** gorv + the root *veld* (the-unconstrained) would also produce the merged spelling *Gorveld*, meaning "corrupted-unconstrained" — a semantically distinct compound. When this root-root reading is intended, write analytically as *Gorv-Veld* to distinguish it from the suffix form. |
| **draek** | channel, medium, the interface | the priestly function between compact parties; what the Aethori did | *Draekori* (channeler-keepers, rare variant) |
| **vaeld** | proximity, the approach, border-of-the-sacred | the zone between the ordinary and the compact; also a register name | *Vaelraeth* (approach-threshold; vaeld+raeth with D-drop at junction) |
| **thael** | illuminate, force-knowing, revelation | what the compact did to the Aethori — made them know things they couldn't unlearn | *Thaelornvael* (ceremonial name for the being: thael+orn+vael = "the illuminate-unmaking-enduring") |
| **kaeld** | the performed binding act, the compact-in-action | *kaeld* is a lexicalized derived root: *kael* (the compact) + actional *–d* suffix (marks the instantiated/performed form rather than the abstract agreement). It is listed here as a root because it has solidified into an independent lexical item with its own compounds — but its derivation from *kael* is intentional and transparent. Contrast *aeth* (the agreement as a concept) vs. *kaeld* (the agreement as an executed act). | *Kaeldor* (the state of the performed compact) |

---

### 4.2 Political / Administrative / Imperial

| Root | Core meaning | Extended meanings | Example derived word |
|------|-------------|-------------------|---------------------|
| **cal** | order, law, structured arrangement | the right-and-fitting arrangement of things; the principle behind the empire | *Caleth* (the Order / the Empire), *Calari* (keeper of order — administrative agent, non-priestly) |
| **vrath** | command, applied force, power-in-action | force directed by will; also the material force of the empire | *Veldrath* (the enduring-force / the world), *Vrath-eld* (command-ground) |
| **tharg** | authority, rank, right-to-command | the formal quality of having standing to give orders | *Tharg-ari* (those who hold authority — administrative rank, non-priestly) |
| **rek** | administer, maintain, keep in place | the bureaucratic function; keeping arrangements functioning | *Rekeld* (administered-ground), *Rekern* (administrator) |
| **kaen** | duty, obligation, service | given downward to subordinates; what a superior assigns | *Kaenvel* (enduring duty, kaen+vel) |
| **geld** | tribute, give-upward | the material of obligation flowing upward to authority | *Geld-or* (the state of tribute) |
| **mael** | decree, formally name, invoke-officially | the act of naming a thing makes it subject to the order's law | *Mael-eth* (the decreed order / formal proclamation) |
| **vel** | endure, persist, remain | also: former, old, established — that which outlasts change | *Vel-Kael* (the enduring compact), *Veldrath* |
| **dorn** | below, descend, subordinate | directional and hierarchical; what is ranked beneath | *Dornkeld* (deep-hollow), *Dorn-ari* (keepers of what lies below — administrative custodians, non-priestly) |
| **hald** | high, elevate, prominent | directional and hierarchical; what is conspicuously raised | *Haldor* (the elevated state), *Hald-eld* (high ground) |

---

### 4.3 Priestly (Aethori Inner Vocabulary)

| Root | Core meaning | Extended meanings | Example derived word |
|------|-------------|-------------------|---------------------|
| **ori** | keeper, bearer, agent of | the pure agentive root used standalone in priestly speech. This is the same root as the bound suffix *–ori* (§2.2) — in free-root position it appears in Aethori liturgical speech; in bound position it forms compound keeper-titles. The two-position behaviour is a feature of Calethic agentive roots generally. | *Aethori, Thornkaelaethori* |
| **thorn** | witness, formally observe, attest | to see something in a way that binds you to it | *Thorn-kael* (great witness), *Thorneld* (witnessing ground) |
| **naek** | invoke, call, summon through ritual | the formal act of reaching across the compact's threshold | *Naek-ori* (invokers) |
| **vaeln** | adhere to, follow, carry the directives of | what the Bound do — follow instructions they don't fully understand | *Vaeln-Aethori* (adhering keepers = the Bound tier) |
| **thael** | (see metaphysical) | in priestly register: to be illuminated by the compact; also a burden | *Thaelornvael* |

---

### 4.4 Physical / Geographic

| Root | Core meaning | Extended meanings | Example derived word |
|------|-------------|-------------------|---------------------|
| **eld** | ground, place, site | where something is located; the territory of a thing | *Aetheld* (compact-ground), *Greveld* (testing-ground) |
| **korr** | passage, way, corridor | movement between places; transit | *Korrveld* (korr+veld = "the unconstrained passage" — the corridor into the experimental fringe), *Korreldh* (passage-ground, waypoint) |
| **vren** | frontier, edge, limit | the empire's reach ending here; the experimental fringe | *Vaelren* (former-frontier), *Vraen* (frontier-sworn) |
| **gael** | water, flow, yielding | water and yielding share a root — what yields to terrain | *Gaeleld* (water-ground / flooded site) |
| **drak** | stone, unyielding, permanent material | what cannot be moved or changed by ordinary means | *Drakeld* (stone-ground), *Drak-ari* (stone-keepers — administrative stewards of Calethic stonework; rare role) |
| **keld** | hollow, pit, cavity below ground | a space that goes down; absence in the earth | *Dornkeld* (deep-hollow = the Pits' Calethic name) |
| **ald** | open space, plain, flat ground | visible, unobstructed territory. *ald* and the bound suffix *–ald* (§2.2) are the same form in two grammatical positions: free root when used standalone or as the second element in a root-root compound; bound suffix when it carries the grammatical meaning "open expanse of X." This two-position behaviour parallels *ori* / *–ori* (§4.3). | *Kael-Ald* (bind-plain) |
| **raeth** | threshold, boundary marker, point of crossing | a marked point where one thing ends and another begins | *Kael-Raeth* (compact's threshold), *Raeth-ori* (threshold-keepers — specifically the Aethori who maintained the compact's boundary markers; *–ori* is correct here because this role was Aethori-tier, not administrative) |
| **theld** | controlled entry point, administered threshold | more specific than *raeth* — a crossing that is managed | *Kaeltheld* (the paramount administered threshold = the Wound's Calethic name) |
| **laeld** | root (plant), the thing that holds ground | lit. "land-anchor"; also: what anchors any structure | *Laeld-eld* (root-ground, foundation site) |

---

### 4.5 Materials / Physical Properties

| Root | Core meaning | Extended meanings | Example derived word |
|------|-------------|-------------------|---------------------|
| **daeld** | ash, what-remains-after-fire | the trace left when a structured thing is undone | *Daeld-Ald* (the ash-plain) — Calethic form of *Cinderplain* (working name); uses *–ald* because Cinderplain is open terrain, not a bounded site |
| **korn** | bone, structural interior, what holds form | the underlying structure, the essential skeleton | *Korn-keld* (bone-hollow = *Bone Hollow* in Grevenmire) |
| **vaen** | iron, metal, hard-refined material | refined substance used in weapons and binding tools | *Vaen-eld* (iron-ground / armory site) |
| **raek** | light-in-material, glow, luminescence | distinct from *thael* (metaphysical light); *raek* is light that can be seen in physical objects | *Raek-ith* (a light-instrument / glow-device) |
| **meld** | still water, standing water, mire | distinct from *gael* (flowing); *meld* is water that does not go anywhere | *Meld-eld* (mire-ground) — Calethic technical term for a standing-water site. The Caleth named *Grevenmire* as *Greveld* (testing-ground) rather than *Meld-eld* because the testing function was primary — the mire was a byproduct of the compact operations, not the defining feature. *Meld-eld* survives only as a technical descriptor in Calethic land-survey records, not as a place name. |

---

### 4.6 Actions

| Root | Core meaning | Notes |
|------|-------------|-------|
| **kael** | bind, hold by compact | (same as metaphysical root — the action form) |
| **orn** | unmake, dissolve a structure | (same as metaphysical root) |
| **gorv** | corrupt, alter, contaminate | (same as metaphysical root) |
| **naeld** | seal permanently, close without exit | |
| **hald** | elevate, ascend, hold at height | the action form of directional/hierarchical *hald* (high, elevated); distinct from *kael*: *kael* holds by compact; *hald* holds by physical elevation or position |
| **rael** | assert presence, claim formally | like a declaration that makes something real |
| **dorn** | descend, go deep | directional action form |
| **grev** | trial, test, examine | what was done at *Greveld* (Grevenmire) — formal testing |
| **thael** | illuminate, reveal, force knowing | |
| **vrath** | command, apply force | |
| **thorn** | witness, observe formally | |
| **vaeln** | adhere to, follow directives | |
| **mael** | decree, name formally | |
| **draek** | channel, mediate | |
| **naek** | invoke, call, summon through ritual | priestly action: the formal act of reaching across the compact's threshold |

---

### 4.7 Descriptors / Modifiers

These are the prefix forms of the above roots when used in a modifying (adjectival/adverbial) position. Listed here for reference:

| Form | Meaning in modifier position |
|------|------------------------------|
| **kael–** | great, first, paramount |
| **vel– / vael–** | old, enduring / former, of-past-times |
| **dorn–** | deep, below, hidden |
| **hald–** | high, elevated |
| **thar–** | true, genuine, sworn |
| **or–** | without, not, the reversal of (negation) |
| **naeld–** | sealed, permanent |
| **gorv–** | corrupted, wrong, altered by contact |
| **rek–** | orderly, administered |
| **grev–** | tested, examined |
| **thael–** | revealed, made-known |
| **drak–** | stone-solid, unyielding |

---

### 4.8 Numbers and Ordinals

| Form | Meaning | Cross-reference |
|------|---------|-----------------|
| **kel** | one, first | prefix form: *kel–* |
| **dael** | two, the pair, second | |
| **thornel** | three; from *thorn–ael* (three-sworn) | Echoes "three witnesses" in Calethic ritual — intentional doubling. **Contraction rule:** when –ael follows a syllable ending in a nasal or –rn cluster (–n, –rn), the suffix reduces to –el (the 'a' drops). This is a lexicalized contraction specific to number-root formations: thorn+ael → *thornel*, korn+ael → *kornel*. Outside number roots, –ael does not reduce. |
| **vael** | four (long-vowel formal number) | Three distinct uses by position: **(1)** standalone = the number four; **(2)** preceding another root = the prefix "former" (*vael–eld* = "former ground"; use trailing hyphen analytically to mark this); **(3)** in terminal position within *vaeld*-register ceremonial compounds = the long-vowel solemn form of *vel* = "the enduring, the one that persists." This ceremonial terminal use is the reading in *Thaelornvael* ("the enduring illuminating unmakings"). Do not use terminal *vael* in administrative or formal register; it is vaeld-register only. |
| **kornel** | five; from *korn–ael* (five-boned) | Echoes "five-boned structure" — the structural number. –ael → –el contraction applies (see *thornel*). |

> The conceptual doublings (three/witness, five/bone) are deliberate features of the language, not coincidences. The Caleth built their numerology into their vocabulary. Understanding these couplings is how Aethori scholars cross-referenced ritual passages with structural records.

---

## 5. Named-Entity Pass

### 5.1 Confirmed TBD Terms (Q3 language-pass deliverables)

---

#### The Collapse

| | |
|--|--|
| **Common name** | The Collapse |
| **Scholarly Common** | The Ashen Age (*purely Common coinage — no Calethic origin*) |
| **Calethic name** | **Kaelorneth** |
| **Derivation** | *kael* (the first compact/great) + *orn* (unmake) + *-eth* (collective/event suffix) = "the great unmaking of the order" |
| **Aethori abbreviated form** | **Orneth** ("the unmaking") |
| **Common decay path** | *Kaelorneth* → oral reduction → form lost entirely in outer territories → replaced by descriptive Common coinage "the Collapse" |
| **Status** | Calethic form: authorial/Aethori usage only. Common name fully replaced it. |

---

#### The Unbound Ruin — the Being

The being has a layered naming system reflecting who is speaking and in what register:

| Register | Name | Derivation |
|---------|------|------------|
| **Common (scholarly)** | The Unbound | Translation of Calethic *Orveld* |
| **Common (popular)** | The Ruin | Conflation with *Orneth* — people named the being after what it did |
| **Calethic (working name)** | **Orveld** | *or–* (without, negation) + *veld* (the unconstrained/void-beyond) = "that which exists without constraint" |
| **Aethori (ceremonial, full)** | **Thaelornvael** | *thael* (illuminate/revelation) + *orn* (unmake) + *vael* (the enduring — vaeld-register terminal form of *vel*) = "the illuminate-unmaking-enduring" — the name given **before** they understood what it was (they believed it illuminated them; the bitter irony: it was unmaking them) |
| **Aethori (inner circle working title)** | **Orveld-Thar** | "the truly unconstrained" — used in internal correspondence after the Collapse |

> **Lore note:** The Common word "Unbound" is a direct translation of *Orveld*, suggesting a scholar read Calethic records and rendered it accurately. The popular name "the Ruin" comes from attributing *Kaelorneth* (what the Collapse was) to the being that caused it. Both names for the being are thus in simultaneous use, as are the era names *Ashen Age* and *Age of Reclaiming* — the same pattern of scholarly vs. common nomenclature repeating itself.

---

#### The Aethori — Full Title

| | |
|--|--|
| **Common/working name** | Aethori |
| **Full ceremonial title** | **Thornkaelaethori** |
| **Derivation** | *thorn* (witness, formally observe) + *kael* (great, the first compact) + *Aethori* (keepers of the compact) = "the great-compact-witnessing keepers" = "those who witnessed and keep the first compact" |
| **Spoken in ceremony** | *"Aethori, Thornkael"* — category first, qualification second (inverse of written order) |
| **Short form in all practical usage** | *Aethori* |
| **Status** | Intact Calethic term. The Aethori maintained their own name. No corruption. |

**Tier-qualified forms:**

| Tier | Calethic form | Meaning |
|------|--------------|---------|
| Inner circle | *Kel-Thornkaelaethori* | "first-rank great-compact-witnessing keepers" |
| Bound | *Vaeln-Aethori* | "the adhering keepers" — those who follow by bloodline |
| Scattered | *Gorv-Aethori* | "the broken keepers" — post-Collapse scholar's classification, not self-designation |

---

### 5.2 Case-by-Case Entity Decisions

---

#### Caleth / Calethic Empire

| | |
|--|--|
| **Common name** | The Caleth / The Calethic Empire |
| **Calethic form** | **Caleth** |
| **Derivation** | *cal* (order, law, structured arrangement) + *–eth* (polity/collective suffix) = "the Order" or "the Ordered Polity" |
| **Status** | **Intact Calethic** — their own name for themselves. Common preserved it because there was nothing to replace it with. |

---

#### Vraen

| | |
|--|--|
| **Common name** | The Vraen (reserved for later reveal) |
| **Calethic form** | **Vraen** |
| **Derivation** | *vren* (frontier, edge, limit) + *–ael* (sworn body suffix) = "the frontier-sworn" — the Calethic military order assigned to hold the experimental fringe territories. VR– cluster preserved intact. |
| **Status** | **Intact Calethic** — their name survived because they were *present* in the outer territories. People who encounter a named military order remember its name even without understanding the language. |
| **Note** | This fits the lore exactly: the Vraen governed Varenmark-territory. The name in Common matches their Calethic identity. |

---

#### Veldrath (the world)

| | |
|--|--|
| **Common name** | Veldrath |
| **Calethic origin** | *vel* (endure, persist) + *vrath* (applied force, command, power) → *veld-rath* → *Veldrath* |
| **Calethic meaning** | "The enduring force" or "the Enduring Power" — originally the Caleth's name for their sphere of dominion |
| **Post-Collapse reading** | After the Caleth were gone, the name survived with a new resonance: "what endured past the force" — the world outlasted the empire that named it |
| **Status** | **Calethic-derived, entered Common** — the Caleth named their domain; the name survived because it was simply what the world was called |

---

#### Varenmark (Region 1)

| | |
|--|--|
| **Common name** | Varenmark |
| **Calethic form** | **Vaelren** |
| **Derivation** | *vael–* (former, of-past-times) + *vren* (frontier) → junction *–l + vr–* = three consonants → V-drop (administrative register) → *vael+ren* → **Vaelren** = "the former frontier" |
| **Hybrid etymology** | Two competing scholarly explanations: (1) *Vaelren* → *Varen* + Common suffix *–mark* (borderland/march) added by locals; (2) *Vraen* (the frontier-sworn military order) + Common *–mark* → *Vraenmark* → *Varenmark* — "the borderland of the Vraen" |
| **Status** | **Hybrid: Calethic root + Common suffix.** The *–mark* suffix is definitively Common. The *Varen–* element is either from *Vaelren* (corruption of vael+vren) or from *Vraen*. Both meanings remain in scholarly debate within the world. |
| **Open item #1 resolution** | **Keep Varenmark.** The name is now explained; its Calethic roots add depth without requiring a change. |

---

#### Ashlen Wood

| | |
|--|--|
| **Common name** | Ashlen Wood |
| **Calethic name** | **Aetheld** |
| **Derivation** | *aeth* (covenant, compact) + *–eld* (ground/place) = "covenant-ground" or "the binding site" |
| **Corruption path** | *Aetheld* → TH-shift → *Aesheld* → final-consonant loss → *Aeshel* → *Ashel* → *Ashlen* (Common *–n* localizing suffix added) → Common *Wood* appended |
| **Status** | **Common corruption of Calethic *Aetheld*** |
| **Player payoff** | A player who has learned that *aeth* = compact, and that *–eld* = ground, can reconstruct "Ashlen" → *Aetheld* → "wait — this whole wood was named as a compact-ground?" This is a viable trigger for open item #6 (discovery mechanism). |

---

#### Grevenmire

| | |
|--|--|
| **Common name** | Grevenmire |
| **Calethic name** | **Greveld** |
| **Derivation** | *grev* (trial, test, examine) + *–eld* (ground/place) = "the testing-ground" |
| **Corruption path** | *Greveld* → final *–d* dropped → *Grevel* → *Greven* (Common *–en* replacing dropped consonant) → locals appended Common *mire* (bog) once the wetland was all they could observe → *Grevenmire* |
| **Status** | **Calethic root *greveld* + Common suffix *mire*** |
| **Lore note** | Locals added *mire* not knowing the Calethic *greveld* already meant something close to "the place of unpleasant deliberate things." They named it for what they could see (the bog) without knowing why the bog existed (the Caleth created it). |

---

#### The Droveway

| | |
|--|--|
| **Common name** | The Droveway |
| **Calethic name** | **Korrveld** |
| **Derivation** | *korr* (passage, way, corridor) + *veld* (the unconstrained, void-beyond) → *Korrveld* = "the unconstrained passage" — the Caleth's transit corridor into the experimental fringe where the compact's effects were being tested. The –rr– geminate counts as a single consonant for junction purposes; korr+veld = –rr + v– = two consonants, legal. |
| **Status** | **Calethic *Korrveld* fully replaced by Common *Droveway*** |
| **Lore note** | The Calethic name survives on the Weathered Waypost (sub-location), which lists seven settlement names in what locals assume is an old dialect. *Korrveld* appears on the waypost in the Calethic form. The word for "drove" (livestock march) is Common; locals renamed the route for its most visible daily function, discarding the administrative Calethic name entirely. |

---

#### The Halrow

| | |
|--|--|
| **Common name** | The Halrow |
| **Calethic name** | **Kaeldor** |
| **Derivation** | *kaeld* (the performed binding act) + *–or* (abstract-noun suffix: state, condition) = "the state/condition of the performed compact" — the abstract quality of being the place where the compact's execution is fixed and persists |
| **Corruption path** | *Kaeldor* → vowel reduction → *Kaldor* → H-mutation → *Haldor* → final vowel loss → *Haldr* → liquid shift (dr → rw in frontier speech) → *Halrow* |
| **Status** | **Common corruption of Calethic *Kaeldor*** |

---

#### The Drowning Pits

| | |
|--|--|
| **Common name** | The Drowning Pits |
| **Calethic name** | **Dornkeld** |
| **Derivation** | *dorn–* (deep, descend) + *keld* (hollow, cavity below ground) = "the deep hollow" — the Calethic deep-access excavation shafts |
| **Status** | **Calethic *Dornkeld* fully replaced by Common *Drowning Pits*** |
| **Lore note** | The Common name describes the experience of the place (they flood; people have drowned). The Calethic name describes the structural purpose (they go down). |

---

#### Crestfall

| | |
|--|--|
| **Common name** | Crestfall |
| **Calethic administrative name** | **Korreldh** |
| **Derivation** | *korr* (passage, way) + *–eldh* (a compound form of *–eld* = ground/place) = "the passage-place" or "the waypoint" |
| **Status** | **Likely Common geographic name, with phonetic echo of Calethic *Korreldh*** |
| **Note** | *Crest* (high point) + *fall* (the slope down from it) is plausible as pure Common geographic description. The phonetic resemblance to *Korreldh* may be weak echo or coincidence. The Calethic administrative name would only survive in records found in the Collapsed Vault — not in oral Common tradition. |

---

#### The Wound (Region 5, endgame)

| | |
|--|--|
| **Common name** | The Wound |
| **Calethic name** | **Kaeltheld** |
| **Derivation** | *kael* (great, first/paramount) + *theld* (controlled entry point, administered threshold) = "the paramount administered threshold" — the Caleth's name for their heartland, the place from which all compact operations were commanded |
| **Status** | **Common name post-Collapse, Calethic *Kaeltheld* preserved in Aethori texts only** |
| **Lore note** | The Caleth did not call their heartland "the wound." They called it the place from which all thresholds were controlled. That it became a wound is the measure of what the Collapse did to their pride. |

---

#### Bone Hollow (sub-location in Grevenmire)

| | |
|--|--|
| **Common name** | Bone Hollow |
| **Calethic name** | **Korn-Keld** |
| **Derivation** | *korn* (bone, structural interior) + *keld* (hollow, cavity) = "bone-hollow" — exact translation |
| **Status** | **Common name is a direct translation of Calethic *Korn-Keld*** |
| **Player payoff** | This is a calibrated payoff moment for the discovery mechanism (open item #6). A player who has learned Calethic vocabulary translates *korn* = bone, *keld* = hollow — and the name matches exactly. The Caleth left the same name. Locals named it the same thing because that is what is visibly there. But the question of *why* the Caleth made a place specifically for bones is not answered by the translation. |

---

#### Greymoor, Saltcliff, Cinderplain (working names, Regions 2–4)

Calethic forms sketched here as placeholders; full design deferred to Phase 5+ regional development.

| Common (working) | Calethic form (sketch) | Derivation notes |
|---|---|---|
| Greymoor | **Haldeld** | *hald* (high/elevated) + *–eld* (ground) = "the high grounds" |
| Saltcliff | **Gaeldrak** | *gael* (water/flow) + *drak* (stone/unyielding) = "the unyielding water-place / stone-water" — the sea-cliff where water meets permanent stone. Syllabification: *gael.drak* — DR– is a legal onset so the syllable split is clean; no three-consonant rule is triggered within either syllable. Legal. |
| Cinderplain | **Daeld-Ald** | *daeld* (ash) + *ald* (open plain) = "the ash-plain" |

---

#### The Korreld Exchange (sub-location in Crestfall)

| | |
|--|--|
| **Common name** | The Korreld Exchange |
| **Calethic root** | *Korreldh* → folk-corruption → *Korreld* |
| **Pattern** | Hybrid: Calethic-echo + Common descriptor (matches Grevenmire, Ashlen Wood, Varenmark) |
| **How the name formed** | Crestfall sits at the old *Korreldh* waypoint. Locals who half-remembered the Calethic name for the old passage-place applied it to the town's main trading hub. Commerce was the most visible function of a waypoint, so Common *Exchange* was appended — the same pattern by which *mire* was appended to *Grevel* or *Wood* to *Ashlen*. The Calethic element names the place by its history; the Common word names it by its observed use. |
| **Design note** | No Calethic root for trade or commerce exists; coining one solely for this sub-location is unnecessary scope expansion. *Exchange* stays in Common. |
| **Status** | **Resolved.** Replaces "Aurelian Market," which had no derivable origin in Calethic or established Common naming patterns. |

---

## 6. Notes for Future Writers

### What makes a word feel Calethic?
- It starts with K–, V–, D–, TH–, KR–, VR–, or DR–
- It contains *ae*, *or*, *eld*, *orn*, *ald*, or *ael* somewhere
- It is short (1–2 syllables) if it is a root
- It is a compound if it is a place/title
- It does NOT contain S, SH, or soft sounds — unless it is a ritual/ceremonial term specifically being marked as Aethori-register

### How to coin a new Calethic word
1. Identify the core meaning
2. Find the nearest root(s) in §4
3. Add the appropriate suffix for the word's grammatical role (§2.2)
4. Add a prefix from §2.3 if needed
5. Check against the phonology rules (§1.1–1.3)
6. Run the Common corruption rules (§1.5) to find what the Common name would be

### How to work backwards from a Common place name
1. Apply the corruption rules in reverse: un-H-mutate, un-TH-shift, restore dropped finals, un-compress compounds
2. Match against roots in §4
3. Write up the entity pass entry

### Do not coin sibilants in Calethic
New words never use S or SH unless they are Aethori ceremonial phrases. If you find yourself reaching for an S-sound, use K, TH, or V instead.

---

## 7. Session Log

| Date | Work |
|------|------|
| 2026-04-15 | Full language designed: phonology, morphology, syntax, root dictionary (~75 entries), full named-entity pass for all Varenmark entities and TBD terms. Q3 complete. |
| 2026-04-15 | Pass 1 corrections (A1–A11, B1–B7): expanded finals list, junction reduction rules, liquid rotation, –eldh suffix, suffix variant notes, hyphenation convention, fixed 6 illegal examples (Vaeldraeth→Vaelraeth, Veldraeth→Veldrath, Kaenorvael→Kaenvel, Naeldithor→Naeldithorn, Naeldk'eld→Naeldkeld, Gaeldraeth→Gaeldrak), fixed Varenmark form to Vaelren. |
| 2026-04-15 | Pass 2 corrections (C1–C6, D1–D4): Korrveld re-derived as korr+veld (unconstrained passage); hald action = elevate/ascend; H note includes root-initial; vael three-position disambiguation (number / prefix / vaeld-terminal); Kaeldor gloss aligned to –or = state/condition; –ael→–el contraction rule documented; junction table restructured; per-syllable rule stated; short-o vs –or note; meld "replaced" clarified. |
| 2026-04-15 | Pass 3 corrections (E1–E3, F1): Calori→Calari (–ori/–ari rule applied); Thaelornvael gloss stripped of unsourced "presence" (§4.1 + §5.1 aligned with §4.8 vael definition); naek added to §4.6 actions; –eld/–ald split into distinct suffix rows with variant note explaining site vs. open-ground distinction. |
| 2026-04-15 | Pass 4 corrections (G1–G6, H1–H2): –ori/–ari rule fully propagated across all root examples — tharg, dorn, drak corrected to –ari (administrative); raeth–ori annotated as Aethori-tier (intentional exception). Gaeldrak junction note rewritten to invoke syllabification rather than incorrect consonant count. daeld example corrected Daeld-eld→Daeld-Ald (open-plain suffix, matches §5.2). kaeld note clarified: lexicalized derived form functions as independent root. ori entry extended: free root and bound suffix –ori are the same root in two grammatical positions. |
| 2026-04-15 | Pass 5 corrections (I1–I7): THL– cluster rule clarified in §1.3 — TH is a single phoneme position; THL– is within the two-consonant maximum. –LD+TH– epenthesis row added to junction reduction table; Naeldithorn now has an explicit governing rule (epenthetic –i– liaison vowel). –eth/–ath variant note added to §2.2 — –ath is an unattested archaic outer-territory variant; new coinages use –eth. kal– in §2.3 reclassified as a spoken-corruption reduction of kael– (first stage of H-mutation), not a formal prefix variant. §4.1 gorv Gorveld homograph resolved — canonical form is gorv+–eld (corrupt-ground); Gorv-Veld is the analytical write-form for the root-root compound. §4.4 ald extended to explicitly document the free-root / bound-suffix two-position relationship (parallels ori/–ori). Aurelian Market resolved and renamed to The Korreld Exchange (hybrid: Calethic-echo + Common descriptor). |

using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Seeders;

/// <summary>
/// Seeds canonical constructed-language records into <see cref="ContentDbContext"/>.
/// Calethic is the first seeded language — it proves the schema and serves as the
/// reference implementation for sound design, morphology, and register constraints.
/// </summary>
public static class LanguagesSeeder
{
    /// <summary>Seeds all language records (idempotent — skips existing slugs).</summary>
    public static async Task SeedAsync(ContentDbContext db)
    {
        var existing = await db.Languages.AsNoTracking().Select(x => x.Slug).ToHashSetAsync();
        var now = DateTimeOffset.UtcNow;
        var missing = GetLanguages(now).Where(x => !existing.Contains(x.Slug)).ToList();
        if (missing.Count == 0) return;
        db.Languages.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static Language[] GetLanguages(DateTimeOffset now) =>
    [
        Calethic(now),
    ];

    // ── Calethic ────────────────────────────────────────────────────────────────

    private static Language Calethic(DateTimeOffset now) => new()
    {
        Slug          = "calethic",
        DisplayName   = "Calethic",
        TypeKey       = "language",
        TonalCharacter = "Hard + Formal — short architectural roots (K, R, TH, V, D) in functional vocabulary; drawn-out compound forms in titles and ceremonial speech.",
        Description   = "The constructed language of the Caleth Empire. Calethic is an architectural, hard-sounding language built on a small set of CVC roots that compound freely. It operates in three registers—administrative, formal, and vaeld (ceremonial)—each with distinct word order and vowel quality. The language is notable for its junction-reduction rules at compound boundaries and its deliberate exclusion of sibilants from all non-ceremonial speech.",
        SampleText    = "Aethori vel kaeld — Thorn-kael-Aethori naeldithorn Kaelorneth.",
        RarityWeight  = 100,
        IsActive      = true,
        Version       = 1,
        UpdatedAt     = now,

        Phonology = new LanguagePhonology
        {
            Consonants =
            [
                new LanguageConsonant { Symbol = "K",  Description = "Voiceless velar stop",                  Notes = "Most frequent initial consonant; architectural, hard" },
                new LanguageConsonant { Symbol = "G",  Description = "Voiced velar stop",                     Notes = "Less common; appears in roots relating to corruption/testing" },
                new LanguageConsonant { Symbol = "T",  Description = "Voiceless alveolar stop",               Notes = "Clean, administrative" },
                new LanguageConsonant { Symbol = "D",  Description = "Voiced alveolar stop",                  Notes = "Common in place-root compounds" },
                new LanguageConsonant { Symbol = "R",  Description = "Alveolar trill/rhotic",                 Notes = "The most architectural sound; used in sonorant clusters" },
                new LanguageConsonant { Symbol = "L",  Description = "Lateral approximant",                   Notes = "Appears in suffix/compound forms; softer than R" },
                new LanguageConsonant { Symbol = "V",  Description = "Voiced labiodental fricative",          Notes = "Marks prestige-register words; heard in vel-, vrath, veld" },
                new LanguageConsonant { Symbol = "TH", Description = "Voiced dental fricative (as in 'thee')", Notes = "Authority marker; the voiced form gives weight" },
                new LanguageConsonant { Symbol = "H",  Description = "Voiceless glottal fricative",           Notes = "Appears as root-initial (hald), in compound junctions, and H-mutation corruption forms (K- → H- in frontier speech)" },
                new LanguageConsonant { Symbol = "N",  Description = "Nasal",                                 Notes = "Common in suffixes and terminals" },
                new LanguageConsonant { Symbol = "M",  Description = "Nasal",                                 Notes = "Less frequent than N" },
                new LanguageConsonant { Symbol = "S",  Description = "Voiceless alveolar sibilant",           Notes = "Ceremonial register only — Aethori ritual speech (vaeld). Absent from functional vocabulary." },
                new LanguageConsonant { Symbol = "SH", Description = "Voiceless postalveolar sibilant",       Notes = "Ceremonial register only — same restriction as S." },
            ],
            Vowels =
            [
                new LanguageVowel { Symbol = "a",   Sound = "/a/ as in 'father'",  Register = "functional", Notes = "Short vowel used in roots and administrative speech" },
                new LanguageVowel { Symbol = "e",   Sound = "/ɛ/ as in 'bed'",     Register = "functional", Notes = "Short vowel used in roots and administrative speech" },
                new LanguageVowel { Symbol = "i",   Sound = "/ɪ/ as in 'bit'",     Register = "functional", Notes = "Short vowel" },
                new LanguageVowel { Symbol = "o",   Sound = "/ɔ/ as in 'lot'",     Register = "functional", Notes = "Short vowel. /u/ is excluded from Calethic entirely." },
                new LanguageVowel { Symbol = "ae",  Sound = "/eɪ/ as in 'day'",    Register = "formal",     Notes = "The dominant Calethic diphthong; appears in kael, aeth, vael, rael" },
                new LanguageVowel { Symbol = "ei",  Sound = "/iː/ as in 'see'",    Register = "formal",     Notes = "Rare; appears in high-prestige titles and old formal compounds" },
                new LanguageVowel { Symbol = "or",  Sound = "/ɔːr/ as in 'ore'",   Register = "formal",     Notes = "Common in abstract-noun suffix -or and keeper suffix -ori" },
                new LanguageVowel { Symbol = "ael", Sound = "/eɪl/",               Register = "formal",     Notes = "Vowel-and-lateral unit — nucleus of sworn-body suffix -ael" },
                new LanguageVowel { Symbol = "ald", Sound = "/ɔːld/",              Register = "formal",     Notes = "Vowel-and-cluster unit — appears in place suffix -ald and roots like daeld" },
            ],
            AllowedSyllablePatterns = ["CVC", "CV", "CVCC"],
            AllowedInitialClusters  = ["VR-", "KR-", "DR-", "THL-"],
            ForbiddenClusters       = ["S-", "SH-", "three consecutive consonants", "word-initial affricate"],
            AllowedFinalClusters    = ["-LD", "-LT", "-RN", "-TH", "-VN", "-RG", "-LN", "-ND"],
            JunctionRules =
            [
                new LanguageJunctionRule { Cluster = "-d + r-",   Rule = "D drops before R (D-drop)",                                                    FormalExample = "vaeld + raeth → vaelraeth",        AdministrativeExample = "same" },
                new LanguageJunctionRule { Cluster = "-l + vr-",  Rule = "V-drop; formal register adds euphonic -d- bridge at l+r juncture",             FormalExample = "vel + vrath → veldrath",           AdministrativeExample = "vael + vren → vaelren (no bridge)" },
                new LanguageJunctionRule { Cluster = "-ld + TH-", Rule = "D is preserved; epenthetic -i- inserted as a liaison vowel between -ld and TH-", FormalExample = "naeld + thorn → Naeldithorn", AdministrativeExample = "(no administrative variant attested)" },
            ],
        },

        Morphology = new LanguageMorphology
        {
            Roots =
            [
                // §4.1 Metaphysical / The Compact / The Being
                new LanguageRootEntry { Token = "aeth",  CoreMeaning = "covenant, compact, the binding agreement",        ExtendedMeanings = "the sacred contract mediating the empire's power",                             ExampleWord = "Aethori",       Category = "metaphysical" },
                new LanguageRootEntry { Token = "kael",  CoreMeaning = "bind, contain, hold by compact",                  ExtendedMeanings = "as prefix: great, first, paramount",                                            ExampleWord = "Kaeldor",       Category = "metaphysical" },
                new LanguageRootEntry { Token = "orn",   CoreMeaning = "dissolve, unmake, reverse a binding",             ExtendedMeanings = "the catastrophic end of a structured arrangement",                              ExampleWord = "Kaelorneth",    Category = "metaphysical" },
                new LanguageRootEntry { Token = "veld",  CoreMeaning = "the unconstrained, the void-beyond",              ExtendedMeanings = "existence without limit; the essential quality of the being",                   ExampleWord = "Orveld",        Category = "metaphysical" },
                new LanguageRootEntry { Token = "rael",  CoreMeaning = "presence, assertion of existence",                ExtendedMeanings = "a presence that insists on being here; the being's local manifestation",        ExampleWord = "Raelaethori",   Category = "metaphysical" },
                new LanguageRootEntry { Token = "thar",  CoreMeaning = "true, genuine, oath-bound, absolute",             ExtendedMeanings = "affirms; marks things that cannot be unsaid or undone",                         ExampleWord = "Tharkael",      Category = "metaphysical" },
                new LanguageRootEntry { Token = "naeld", CoreMeaning = "seal, permanent closure, lock-with-no-key",       ExtendedMeanings = "a closing that is not meant to be opened",                                      ExampleWord = "Naeldithorn",   Category = "metaphysical" },
                new LanguageRootEntry { Token = "gorv",  CoreMeaning = "contaminate, alter, corrupt",                     ExtendedMeanings = "the process of contact with veld; what happens to land and minds near the compact", ExampleWord = "Gorveld",     Category = "metaphysical" },
                new LanguageRootEntry { Token = "draek", CoreMeaning = "channel, medium, the interface",                  ExtendedMeanings = "the priestly function between compact parties",                                  ExampleWord = "Draekori",      Category = "metaphysical" },
                new LanguageRootEntry { Token = "vaeld", CoreMeaning = "proximity, the approach, border-of-the-sacred",   ExtendedMeanings = "the zone between the ordinary and the compact; also a register name",           ExampleWord = "Vaelraeth",     Category = "metaphysical" },
                new LanguageRootEntry { Token = "thael", CoreMeaning = "illuminate, force-knowing, revelation",           ExtendedMeanings = "what the compact did to the Aethori — made them know things they couldn't unlearn", ExampleWord = "Thaelornvael", Category = "metaphysical" },
                new LanguageRootEntry { Token = "kaeld", CoreMeaning = "the performed binding act, the compact-in-action", ExtendedMeanings = "lexicalized derived root: kael + actional -d suffix",                          ExampleWord = "Kaeldor",       Category = "metaphysical" },

                // §4.2 Political / Administrative / Imperial
                new LanguageRootEntry { Token = "cal",   CoreMeaning = "order, law, structured arrangement",              ExtendedMeanings = "the right-and-fitting arrangement of things; the principle behind the empire",   ExampleWord = "Caleth",        Category = "political" },
                new LanguageRootEntry { Token = "vrath", CoreMeaning = "command, applied force, power-in-action",         ExtendedMeanings = "force directed by will; also the material force of the empire",                  ExampleWord = "Veldrath",      Category = "political" },
                new LanguageRootEntry { Token = "tharg", CoreMeaning = "authority, rank, right-to-command",               ExtendedMeanings = "the formal quality of having standing to give orders",                           ExampleWord = "Tharg-ari",     Category = "political" },
                new LanguageRootEntry { Token = "rek",   CoreMeaning = "administer, maintain, keep in place",             ExtendedMeanings = "the bureaucratic function; keeping arrangements functioning",                     ExampleWord = "Rekern",        Category = "political" },
                new LanguageRootEntry { Token = "kaen",  CoreMeaning = "duty, obligation, service",                       ExtendedMeanings = "given downward to subordinates; what a superior assigns",                        ExampleWord = "Kaenvel",       Category = "political" },
                new LanguageRootEntry { Token = "geld",  CoreMeaning = "tribute, give-upward",                            ExtendedMeanings = "the material of obligation flowing upward to authority",                         ExampleWord = "Geld-or",       Category = "political" },
                new LanguageRootEntry { Token = "mael",  CoreMeaning = "decree, formally name, invoke-officially",        ExtendedMeanings = "the act of naming a thing makes it subject to the order's law",                  ExampleWord = "Mael-eth",      Category = "political" },
                new LanguageRootEntry { Token = "vel",   CoreMeaning = "endure, persist, remain",                         ExtendedMeanings = "also: former, old, established — that which outlasts change",                    ExampleWord = "Vel-Kael",      Category = "political" },
                new LanguageRootEntry { Token = "dorn",  CoreMeaning = "below, descend, subordinate",                     ExtendedMeanings = "directional and hierarchical; what is ranked beneath",                           ExampleWord = "Dornkeld",      Category = "political" },
                new LanguageRootEntry { Token = "hald",  CoreMeaning = "high, elevate, prominent",                        ExtendedMeanings = "directional and hierarchical; what is conspicuously raised",                     ExampleWord = "Haldor",        Category = "political" },

                // §4.3 Priestly (Aethori Inner Vocabulary)
                new LanguageRootEntry { Token = "ori",   CoreMeaning = "keeper, bearer, agent of",                        ExtendedMeanings = "pure agentive root used standalone in priestly speech; same root as bound suffix -ori",  ExampleWord = "Aethori",  Category = "priestly" },
                new LanguageRootEntry { Token = "thorn", CoreMeaning = "witness, formally observe, attest",               ExtendedMeanings = "to see something in a way that binds you to it",                                  ExampleWord = "Thorn-kael",    Category = "priestly" },
                new LanguageRootEntry { Token = "naek",  CoreMeaning = "invoke, call, summon through ritual",             ExtendedMeanings = "the formal act of reaching across the compact's threshold",                       ExampleWord = "Naek-ori",      Category = "priestly" },
                new LanguageRootEntry { Token = "vaeln", CoreMeaning = "adhere to, follow, carry the directives of",      ExtendedMeanings = "what the Bound do — follow instructions they don't fully understand",             ExampleWord = "Vaeln-Aethori", Category = "priestly" },

                // §4.4 Physical / Geographic
                new LanguageRootEntry { Token = "eld",   CoreMeaning = "ground, place, site",                             ExtendedMeanings = "where something is located; the territory of a thing",                            ExampleWord = "Aetheld",       Category = "geographic" },
                new LanguageRootEntry { Token = "korr",  CoreMeaning = "passage, way, corridor",                          ExtendedMeanings = "movement between places; transit",                                                ExampleWord = "Korrveld",      Category = "geographic" },
                new LanguageRootEntry { Token = "vren",  CoreMeaning = "frontier, edge, limit",                           ExtendedMeanings = "the empire's reach ending here; the experimental fringe",                        ExampleWord = "Vraen",         Category = "geographic" },
                new LanguageRootEntry { Token = "gael",  CoreMeaning = "water, flow, yielding",                           ExtendedMeanings = "water and yielding share a root — what yields to terrain",                       ExampleWord = "Gaeleld",       Category = "geographic" },
                new LanguageRootEntry { Token = "drak",  CoreMeaning = "stone, unyielding, permanent material",           ExtendedMeanings = "what cannot be moved or changed by ordinary means",                              ExampleWord = "Drakeld",       Category = "geographic" },
                new LanguageRootEntry { Token = "keld",  CoreMeaning = "hollow, pit, cavity below ground",                ExtendedMeanings = "a space that goes down; absence in the earth",                                   ExampleWord = "Dornkeld",      Category = "geographic" },
                new LanguageRootEntry { Token = "ald",   CoreMeaning = "open space, plain, flat ground",                  ExtendedMeanings = "visible, unobstructed territory; free root form of bound suffix -ald",            ExampleWord = "Kael-Ald",      Category = "geographic" },
                new LanguageRootEntry { Token = "raeth", CoreMeaning = "threshold, boundary marker, point of crossing",   ExtendedMeanings = "a marked point where one thing ends and another begins",                         ExampleWord = "Kael-Raeth",    Category = "geographic" },
                new LanguageRootEntry { Token = "theld", CoreMeaning = "controlled entry point, administered threshold",   ExtendedMeanings = "more specific than raeth — a crossing that is managed",                          ExampleWord = "Kaeltheld",     Category = "geographic" },
                new LanguageRootEntry { Token = "laeld", CoreMeaning = "root (plant), the thing that holds ground",       ExtendedMeanings = "lit. 'land-anchor'; also: what anchors any structure",                           ExampleWord = "Laeld-eld",     Category = "geographic" },

                // §4.5 Materials / Physical Properties
                new LanguageRootEntry { Token = "daeld", CoreMeaning = "ash, what-remains-after-fire",                    ExtendedMeanings = "the trace left when a structured thing is undone",                               ExampleWord = "Daeld-Ald",     Category = "material" },
                new LanguageRootEntry { Token = "korn",  CoreMeaning = "bone, structural interior, what holds form",      ExtendedMeanings = "the underlying structure, the essential skeleton",                                ExampleWord = "Korn-keld",     Category = "material" },
                new LanguageRootEntry { Token = "vaen",  CoreMeaning = "iron, metal, hard-refined material",              ExtendedMeanings = "refined substance used in weapons and binding tools",                             ExampleWord = "Vaen-eld",      Category = "material" },
                new LanguageRootEntry { Token = "raek",  CoreMeaning = "light-in-material, glow, luminescence",           ExtendedMeanings = "distinct from thael (metaphysical light); raek is light visible in physical objects", ExampleWord = "Raek-ith",   Category = "material" },
                new LanguageRootEntry { Token = "meld",  CoreMeaning = "still water, standing water, mire",               ExtendedMeanings = "distinct from gael (flowing); meld is water that does not move",                 ExampleWord = "Meld-eld",      Category = "material" },

                // §4.6 Actions (roots also listed under other categories — listed here as verbs)
                new LanguageRootEntry { Token = "grev",  CoreMeaning = "trial, test, examine",                            ExtendedMeanings = "what was done at Greveld (Grevenmire) — formal testing",                         ExampleWord = "Greveld",       Category = "action" },

                // §4.8 Numbers and Ordinals
                new LanguageRootEntry { Token = "kel",    CoreMeaning = "one, first",                                     ExtendedMeanings = "prefix form: kel-",                                                               ExampleWord = "Kel-Thornkaelaethori", Category = "numeral" },
                new LanguageRootEntry { Token = "dael",   CoreMeaning = "two, the pair, second",                          ExtendedMeanings = "",                                                                                ExampleWord = "Dael-",         Category = "numeral" },
                new LanguageRootEntry { Token = "thornel",CoreMeaning = "three; from thorn-ael (three-sworn)",            ExtendedMeanings = "echoes 'three witnesses' in Calethic ritual; -ael → -el contraction applies",     ExampleWord = "Thornel",       Category = "numeral" },
                new LanguageRootEntry { Token = "vael",   CoreMeaning = "four (long-vowel formal number)",                ExtendedMeanings = "three distinct uses by position: standalone=four; prefix=former; ceremonial terminal=the enduring", ExampleWord = "Thaelornvael", Category = "numeral" },
                new LanguageRootEntry { Token = "kornel", CoreMeaning = "five; from korn-ael (five-boned)",               ExtendedMeanings = "echoes 'five-boned structure' — the structural number; -ael → -el contraction applies", ExampleWord = "Kornel",   Category = "numeral" },
            ],

            Prefixes =
            [
                new LanguageAffixEntry { Form = "kael–",        Category = "intensifier",  CoreMeaning = "great, first, paramount",                                    Notes = "Derives from root kael. Spoken-corruption kal– is not a formal prefix." },
                new LanguageAffixEntry { Form = "vel– / vael–", Category = "temporal",     CoreMeaning = "vel–: still enduring; vael–: formerly, of past-times",       Notes = "vael is also the numeral four; context distinguishes prefix from numeral." },
                new LanguageAffixEntry { Form = "dorn– / dor–", Category = "directional",  CoreMeaning = "deep, hidden, below",                                        Notes = "dor– is a spoken-reduction variant; not used in formal coinages." },
                new LanguageAffixEntry { Form = "hald–",        Category = "directional",  CoreMeaning = "high, elevated, prominent",                                  Notes = "Also used directionally." },
                new LanguageAffixEntry { Form = "thar–",        Category = "affirmative",  CoreMeaning = "true, genuine, oath-bound",                                  Notes = "Affirms; only used in formal/title contexts." },
                new LanguageAffixEntry { Form = "or–",          Category = "negation",     CoreMeaning = "without, not, reversal of",                                  Notes = "The antonym prefix. Homophonous with suffix -or; differentiate by position." },
                new LanguageAffixEntry { Form = "naeld–",       Category = "closure",      CoreMeaning = "sealed, permanent, closed",                                  Notes = "Implies no exit or reversal." },
                new LanguageAffixEntry { Form = "gorv–",        Category = "corruption",   CoreMeaning = "corrupted, wrong, altered",                                  Notes = "Place-name prefix when land was changed by the compact's work." },
                new LanguageAffixEntry { Form = "rek–",         Category = "admin",        CoreMeaning = "orderly, administered",                                      Notes = "Administrative prefix." },
                new LanguageAffixEntry { Form = "grev–",        Category = "examination",  CoreMeaning = "tested, examined, trialled",                                 Notes = "Historical prefix in sites used for experiments." },
                new LanguageAffixEntry { Form = "kel–",         Category = "ordinal",      CoreMeaning = "first (ordinal)",                                            Notes = "See §4.8." },
                new LanguageAffixEntry { Form = "dael–",        Category = "ordinal",      CoreMeaning = "second (ordinal)",                                           Notes = "See §4.8." },
            ],

            Suffixes =
            [
                new LanguageAffixEntry { Form = "-eth / -ath", Category = "polity",       CoreMeaning = "the ordered totality of X",                                       Notes = "-ath is an archaic dialectal variant; new coinages use -eth." },
                new LanguageAffixEntry { Form = "-ori / -ari", Category = "agentive",     CoreMeaning = "keeper, bearer, agent of X",                                      Notes = "-ori is priestly (Aethori-tier); -ari is administrative (non-priestly)." },
                new LanguageAffixEntry { Form = "-eld",        Category = "place",        CoreMeaning = "the site/location of X",                                          Notes = "Marks a specific, bounded or managed site." },
                new LanguageAffixEntry { Form = "-ald",        Category = "place",        CoreMeaning = "the open expanse of X",                                           Notes = "Marks topographically open ground. Not interchangeable with -eld." },
                new LanguageAffixEntry { Form = "-ael / -aen", Category = "sworn-body",   CoreMeaning = "those who are bound by X",                                         Notes = "-ael is archaic written; -aen is standard spoken/administrative." },
                new LanguageAffixEntry { Form = "-or",         Category = "abstract-noun",CoreMeaning = "the state/condition of X",                                         Notes = "Homophonous with prefix or–; differentiate by position." },
                new LanguageAffixEntry { Form = "-ith",        Category = "instrument",   CoreMeaning = "the thing that does X",                                            Notes = "" },
                new LanguageAffixEntry { Form = "-ern",        Category = "agentive",     CoreMeaning = "one who does/manages X (non-priestly)",                            Notes = "" },
                new LanguageAffixEntry { Form = "-eldh",       Category = "place",        CoreMeaning = "place, site, waypoint (archaic compound variant of -eld)",         Notes = "Implies a managed transit point rather than a general site." },
                new LanguageAffixEntry { Form = "-d",          Category = "actional",     CoreMeaning = "instantiated/performed form",                                      Notes = "Actional suffix that lexicalizes a root into its performed-act form (e.g. kael → kaeld)." },
            ],
        },

        RegisterSystem = new LanguageRegisters
        {
            Registers =
            [
                new LanguageRegisterEntry { Name = "Administrative", Usage = "Bureaucratic reports, commands, records",       WordOrder = "SVO",      VowelQuality = "Short vowels throughout",                    Notes = "The everyday working register of the Caleth Empire." },
                new LanguageRegisterEntry { Name = "Formal",        Usage = "Decrees, titles, declarations",                 WordOrder = "Modifier-before-head; compounds", VowelQuality = "Long vowels in head position", Notes = "Used when invoking authority or constructing canonical titles." },
                new LanguageRegisterEntry { Name = "Vaeld",         Usage = "Aethori ritual speech; compact invocation",     WordOrder = "VOS (verb-first)",               VowelQuality = "Long vowels + sibilants permitted", Notes = "Ceremonial register exclusively. Sibilants (S, SH) are only permitted here. Named after vaeld (proximity to the sacred)." },
            ],
        },
    };
}

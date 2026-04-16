namespace RealmEngine.Shared.Models;

/// <summary>
/// A constructed or natural language definition — phonology, morphology, and lexicon.
/// Used as a reference model and as the basis for procedural name generation.
/// </summary>
public class Language
{
    /// <summary>URL-safe, lowercase, hyphenated identifier unique within the language catalog.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Display name shown to players and content authors (e.g. "Calethic", "Elvish").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Short description of the language's origins and in-world usage.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// One-line characterisation of the language's sound and feel
    /// (e.g. "Hard + Formal — short architectural roots; drawn-out compound forms in titles").
    /// </summary>
    public string TonalCharacter { get; set; } = string.Empty;

    /// <summary>Family classification key (e.g. "imperial", "elven", "orcish", "priestly").</summary>
    public string TypeKey { get; set; } = string.Empty;

    /// <summary>Rarity weight for procedural selection — lower values are rarer.</summary>
    public int RarityWeight { get; set; } = 50;

    // ── Phonology ──────────────────────────────────────────────────────────────

    /// <summary>All consonants in the language's phoneme inventory.</summary>
    public List<ConsonantEntry> ConsonantInventory { get; set; } = [];

    /// <summary>All vowels and diphthongs in the language's phoneme inventory.</summary>
    public List<VowelEntry> VowelInventory { get; set; } = [];

    /// <summary>Allowed syllable shapes — e.g. "CVC", "CV", "CVCC".</summary>
    public List<string> AllowedSyllablePatterns { get; set; } = [];

    /// <summary>Allowed word-initial consonant clusters — e.g. "KR–", "VR–", "DR–".</summary>
    public List<string> AllowedInitialClusters { get; set; } = [];

    /// <summary>Forbidden cluster combinations (e.g. three consecutive consonants).</summary>
    public List<string> ForbiddenClusters { get; set; } = [];

    /// <summary>Allowed word-final consonant clusters — e.g. "–LD", "–RN", "–TH".</summary>
    public List<string> AllowedFinalClusters { get; set; } = [];

    /// <summary>Named rules governing consonant changes at compound word junctions.</summary>
    public List<JunctionRule> JunctionRules { get; set; } = [];

    // ── Morphology ─────────────────────────────────────────────────────────────

    /// <summary>Core root vocabulary — atomic meaning units from which words are built.</summary>
    public List<LanguageRoot> Roots { get; set; } = [];

    /// <summary>Bound prefixes that modify or intensify a following root.</summary>
    public List<LanguageAffix> Prefixes { get; set; } = [];

    /// <summary>Bound suffixes that change the grammatical category of a preceding root.</summary>
    public List<LanguageAffix> Suffixes { get; set; } = [];

    // ── Register ───────────────────────────────────────────────────────────────

    /// <summary>The named speech registers of the language (e.g. administrative, formal, ceremonial).</summary>
    public List<LanguageRegister> Registers { get; set; } = [];

    // ── Sample ─────────────────────────────────────────────────────────────────

    /// <summary>Freeform example sentences or sample words illustrating the language in use.</summary>
    public string? SampleText { get; set; }
}

// ── Supporting types ──────────────────────────────────────────────────────────

/// <summary>A single consonant in a language's phoneme inventory.</summary>
/// <param name="Symbol">The written symbol or digraph (e.g. "K", "TH", "SH").</param>
/// <param name="Description">Phonetic description (e.g. "Voiceless velar stop").</param>
/// <param name="Notes">Optional design notes about when or how the consonant is used.</param>
public record ConsonantEntry(string Symbol, string Description, string? Notes = null);

/// <summary>A single vowel or diphthong in a language's phoneme inventory.</summary>
/// <param name="Symbol">The written symbol or digraph (e.g. "ae", "or", "ei").</param>
/// <param name="Sound">IPA or approximate sound description (e.g. "/eɪ/ as in 'day'").</param>
/// <param name="Register">
/// Register restriction, if any — e.g. "formal", "ceremonial".
/// <c>null</c> means the vowel appears in all registers.
/// </param>
/// <param name="Notes">Optional design notes about this vowel's usage or origins.</param>
public record VowelEntry(string Symbol, string Sound, string? Register = null, string? Notes = null);

/// <summary>A junction reduction rule that governs consonant changes at compound word boundaries.</summary>
/// <param name="Cluster">The input consonant cluster that triggers the rule (e.g. "–l + vr–").</param>
/// <param name="Rule">Plain-language description of what transformation is applied.</param>
/// <param name="FormalExample">Worked example in the formal register (e.g. "vel + vrath → veldrath").</param>
/// <param name="AdministrativeExample">Worked example in the administrative register, if it differs.</param>
public record JunctionRule(
    string Cluster,
    string Rule,
    string? FormalExample = null,
    string? AdministrativeExample = null);

/// <summary>A root — an atomic meaning unit from which words can be built by compounding or affixation.</summary>
/// <param name="Token">The root form as written (e.g. "kael", "vel", "aeth").</param>
/// <param name="CoreMeaning">The single principal meaning carried by this root.</param>
/// <param name="ExtendedMeanings">Secondary or derived meanings, if any.</param>
/// <param name="ExampleWord">A sample derived word that demonstrates this root in use.</param>
/// <param name="Category">Optional thematic grouping (e.g. "metaphysical", "geographic", "actions").</param>
public record LanguageRoot(
    string Token,
    string CoreMeaning,
    string? ExtendedMeanings = null,
    string? ExampleWord = null,
    string? Category = null);

/// <summary>A bound affix (prefix or suffix) that attaches to a root and modifies its meaning or category.</summary>
/// <param name="Form">The written form of the affix (e.g. "kael–", "–eth", "–ori").</param>
/// <param name="Category">Grammatical category change or function (e.g. "polity", "keeper/agent", "abstract noun").</param>
/// <param name="CoreMeaning">What the affix contributes to the compound.</param>
/// <param name="Notes">Optional design notes, usage restrictions, or variant forms.</param>
public record LanguageAffix(
    string Form,
    string Category,
    string CoreMeaning,
    string? Notes = null);

/// <summary>A named speech register within the language with its own syntax and phonological rules.</summary>
/// <param name="Name">Register name (e.g. "Administrative", "Formal", "Vaeld").</param>
/// <param name="Usage">When and where this register is used.</param>
/// <param name="WordOrder">Sentence order convention (e.g. "SVO", "VOS").</param>
/// <param name="VowelQuality">Characteristic vowel quality (e.g. "Short vowels throughout", "Long vowels in head position").</param>
/// <param name="Notes">Optional additional notes about phonological or stylistic features.</param>
public record LanguageRegister(
    string Name,
    string Usage,
    string WordOrder,
    string VowelQuality,
    string? Notes = null);

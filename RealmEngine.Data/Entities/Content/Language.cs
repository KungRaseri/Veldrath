namespace RealmEngine.Data.Entities;

/// <summary>
/// A constructed or natural language definition — phonology, morphology, and lexicon.
/// TypeKey = language family (e.g. "imperial", "elven", "orcish", "priestly").
/// Complex collections (consonants, vowels, roots, affixes, registers) are stored as JSONB columns.
/// </summary>
public class Language : ContentBase
{
    /// <summary>Short description of the language's origins and in-world usage.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// One-line characterisation of the language's sound and feel
    /// (e.g. "Hard + Formal — short architectural roots; drawn-out compound forms in titles").
    /// </summary>
    public string? TonalCharacter { get; set; }

    /// <summary>Phoneme inventory and phonotactic rule sets for this language.</summary>
    public LanguagePhonology Phonology { get; set; } = new();

    /// <summary>Morphological rules, root vocabulary, and affixes.</summary>
    public LanguageMorphology Morphology { get; set; } = new();

    /// <summary>Named speech registers with distinct syntax and phonological constraints.</summary>
    public LanguageRegisters RegisterSystem { get; set; } = new();

    /// <summary>Freeform example sentences or sample words illustrating the language in use.</summary>
    public string? SampleText { get; set; }
}

/// <summary>Phoneme inventory and phonotactic rules owned by a Language entity.</summary>
public class LanguagePhonology
{
    /// <summary>Consonant inventory entries (symbol, description, notes).</summary>
    public List<LanguageConsonant> Consonants { get; set; } = [];

    /// <summary>Vowel and diphthong inventory entries (symbol, sound, register, notes).</summary>
    public List<LanguageVowel> Vowels { get; set; } = [];

    /// <summary>Allowed syllable shapes — e.g. "CVC", "CV", "CVCC".</summary>
    public List<string> AllowedSyllablePatterns { get; set; } = [];

    /// <summary>Allowed word-initial consonant clusters — e.g. "KR–", "VR–".</summary>
    public List<string> AllowedInitialClusters { get; set; } = [];

    /// <summary>Forbidden consonant combinations.</summary>
    public List<string> ForbiddenClusters { get; set; } = [];

    /// <summary>Allowed word-final consonant clusters — e.g. "–LD", "–RN".</summary>
    public List<string> AllowedFinalClusters { get; set; } = [];

    /// <summary>Rules governing consonant changes at compound word junctions.</summary>
    public List<LanguageJunctionRule> JunctionRules { get; set; } = [];
}

/// <summary>A consonant in the phoneme inventory of a language.</summary>
public class LanguageConsonant
{
    /// <summary>Written symbol or digraph (e.g. "K", "TH", "SH").</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Phonetic description (e.g. "Voiceless velar stop").</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Optional design notes about when or how this consonant is used.</summary>
    public string? Notes { get; set; }
}

/// <summary>A vowel or diphthong in the phoneme inventory of a language.</summary>
public class LanguageVowel
{
    /// <summary>Written symbol or digraph (e.g. "ae", "or", "ei").</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>IPA or approximate sound description (e.g. "/eɪ/ as in 'day'").</summary>
    public string Sound { get; set; } = string.Empty;

    /// <summary>Register restriction, if any (e.g. "formal", "ceremonial"); null means all registers.</summary>
    public string? Register { get; set; }

    /// <summary>Optional design notes about this vowel's usage or origins.</summary>
    public string? Notes { get; set; }
}

/// <summary>A junction reduction rule governing consonant changes at compound word boundaries.</summary>
public class LanguageJunctionRule
{
    /// <summary>The input consonant cluster that triggers the rule (e.g. "–l + vr–").</summary>
    public string Cluster { get; set; } = string.Empty;

    /// <summary>Plain-language description of the transformation applied.</summary>
    public string Rule { get; set; } = string.Empty;

    /// <summary>Worked example in the formal register.</summary>
    public string? FormalExample { get; set; }

    /// <summary>Worked example in the administrative register, if it differs.</summary>
    public string? AdministrativeExample { get; set; }
}

/// <summary>Root vocabulary and bound affixes owned by a Language entity.</summary>
public class LanguageMorphology
{
    /// <summary>Core root vocabulary — atomic meaning units from which words are built.</summary>
    public List<LanguageRootEntry> Roots { get; set; } = [];

    /// <summary>Bound prefixes that modify or intensify a following root.</summary>
    public List<LanguageAffixEntry> Prefixes { get; set; } = [];

    /// <summary>Bound suffixes that change the grammatical category of a preceding root.</summary>
    public List<LanguageAffixEntry> Suffixes { get; set; } = [];
}

/// <summary>A root entry in the language's lexicon.</summary>
public class LanguageRootEntry
{
    /// <summary>The root form as written (e.g. "kael", "vel", "aeth").</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>The single principal meaning carried by this root.</summary>
    public string CoreMeaning { get; set; } = string.Empty;

    /// <summary>Secondary or derived meanings, if any.</summary>
    public string? ExtendedMeanings { get; set; }

    /// <summary>A sample derived word that demonstrates this root in use.</summary>
    public string? ExampleWord { get; set; }

    /// <summary>Optional thematic grouping (e.g. "metaphysical", "geographic", "actions").</summary>
    public string? Category { get; set; }
}

/// <summary>A bound affix (prefix or suffix) in a language's morphological system.</summary>
public class LanguageAffixEntry
{
    /// <summary>The written form of the affix (e.g. "kael–", "–eth", "–ori").</summary>
    public string Form { get; set; } = string.Empty;

    /// <summary>Grammatical category or function (e.g. "polity", "keeper/agent", "abstract noun").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>What the affix contributes to a compound.</summary>
    public string CoreMeaning { get; set; } = string.Empty;

    /// <summary>Optional design notes, usage restrictions, or variant forms.</summary>
    public string? Notes { get; set; }
}

/// <summary>Named speech registers owned by a Language entity.</summary>
public class LanguageRegisters
{
    /// <summary>The named registers of the language (e.g. administrative, formal, ceremonial).</summary>
    public List<LanguageRegisterEntry> Registers { get; set; } = [];
}

/// <summary>A single named speech register within a language.</summary>
public class LanguageRegisterEntry
{
    /// <summary>Register name (e.g. "Administrative", "Formal", "Vaeld").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When and where this register is used.</summary>
    public string Usage { get; set; } = string.Empty;

    /// <summary>Sentence order convention (e.g. "SVO", "VOS").</summary>
    public string WordOrder { get; set; } = string.Empty;

    /// <summary>Characteristic vowel quality (e.g. "Short vowels throughout").</summary>
    public string VowelQuality { get; set; } = string.Empty;

    /// <summary>Optional additional notes about phonological or stylistic features.</summary>
    public string? Notes { get; set; }
}

namespace RealmFoundry.Services;

/// <summary>Holds all form state for the multi-step Language Builder wizard.</summary>
public class LanguageBuilderState
{
    // ── Step 1 — Identity ─────────────────────────────────────────────────────

    /// <summary>Display name of the language being built (e.g. "Calethic").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>URL-safe slug, auto-derived from <see cref="DisplayName"/> but editable.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Language family classification key (e.g. "imperial", "elven").</summary>
    public string TypeKey { get; set; } = string.Empty;

    /// <summary>One-line characterisation of the language's sound and feel.</summary>
    public string TonalCharacter { get; set; } = string.Empty;

    /// <summary>Short description of the language's origins and in-world usage.</summary>
    public string Description { get; set; } = string.Empty;

    // ── Step 2 — Phonology ────────────────────────────────────────────────────

    /// <summary>Consonant inventory rows being composed by the user.</summary>
    public List<ConsonantRow> Consonants { get; set; } = [];

    /// <summary>Vowel and diphthong inventory rows being composed by the user.</summary>
    public List<VowelRow> Vowels { get; set; } = [];

    /// <summary>Allowed syllable shape patterns (e.g. "CVC", "CV", "CVCC").</summary>
    public List<string> SyllablePatterns { get; set; } = [];

    /// <summary>User-entered text for a custom syllable pattern to add.</summary>
    public string NewSyllablePattern { get; set; } = string.Empty;

    /// <summary>Allowed word-initial consonant clusters (e.g. "KR–", "VR–").</summary>
    public List<string> InitialClusters { get; set; } = [];

    /// <summary>User-entered text for a custom initial cluster to add.</summary>
    public string NewInitialCluster { get; set; } = string.Empty;

    /// <summary>Forbidden consonant combinations.</summary>
    public List<string> ForbiddenClusters { get; set; } = [];

    /// <summary>User-entered text for a forbidden cluster to add.</summary>
    public string NewForbiddenCluster { get; set; } = string.Empty;

    /// <summary>Allowed word-final consonant clusters (e.g. "–LD", "–RN").</summary>
    public List<string> FinalClusters { get; set; } = [];

    /// <summary>User-entered text for a custom final cluster to add.</summary>
    public string NewFinalCluster { get; set; } = string.Empty;

    /// <summary>Junction reduction rules for compound word boundaries.</summary>
    public List<JunctionRuleRow> JunctionRules { get; set; } = [];

    // ── Step 3 — Affixes ──────────────────────────────────────────────────────

    /// <summary>Prefix affix rows.</summary>
    public List<AffixRow> Prefixes { get; set; } = [];

    /// <summary>Suffix affix rows.</summary>
    public List<AffixRow> Suffixes { get; set; } = [];

    // ── Step 4 — Root Dictionary ──────────────────────────────────────────────

    /// <summary>Root vocabulary rows.</summary>
    public List<RootRow> Roots { get; set; } = [];

    // ── Step 5 — Register System ──────────────────────────────────────────────

    /// <summary>Speech register rows.</summary>
    public List<RegisterRow> Registers { get; set; } = [];

    // ── Step 6 — Preview & Submit ─────────────────────────────────────────────

    /// <summary>Freeform example sentences or sample words illustrating the language in use.</summary>
    public string SampleText { get; set; } = string.Empty;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Derives a URL-safe slug from <paramref name="name"/>:
    /// lowercase, trim, replace spaces with hyphens, strip non-alphanumeric characters.
    /// </summary>
    public static string DeriveSlug(string name) =>
        System.Text.RegularExpressions.Regex.Replace(
            name.Trim().ToLowerInvariant().Replace(' ', '-'),
            @"[^a-z0-9\-]", "");

    /// <summary>Resets all state back to a blank wizard.</summary>
    public void Reset()
    {
        DisplayName = string.Empty;
        Slug = string.Empty;
        TypeKey = string.Empty;
        TonalCharacter = string.Empty;
        Description = string.Empty;
        Consonants = [];
        Vowels = [];
        SyllablePatterns = [];
        NewSyllablePattern = string.Empty;
        InitialClusters = [];
        NewInitialCluster = string.Empty;
        ForbiddenClusters = [];
        NewForbiddenCluster = string.Empty;
        FinalClusters = [];
        NewFinalCluster = string.Empty;
        JunctionRules = [];
        Prefixes = [];
        Suffixes = [];
        Roots = [];
        Registers = [];
        SampleText = string.Empty;
    }
}

// ── Row models ────────────────────────────────────────────────────────────────

/// <summary>A single editable consonant row in the Language Builder phonology step.</summary>
public class ConsonantRow
{
    /// <summary>Written symbol or digraph (e.g. "K", "TH").</summary>
    public string Symbol { get; set; } = string.Empty;
    /// <summary>Phonetic description (e.g. "Voiceless velar stop").</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Optional design notes.</summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>A single editable vowel or diphthong row in the Language Builder phonology step.</summary>
public class VowelRow
{
    /// <summary>Written symbol or digraph (e.g. "ae", "or").</summary>
    public string Symbol { get; set; } = string.Empty;
    /// <summary>IPA or approximate sound (e.g. "/eɪ/ as in 'day'").</summary>
    public string Sound { get; set; } = string.Empty;
    /// <summary>Register restriction (e.g. "ceremonial"); empty means all registers.</summary>
    public string Register { get; set; } = string.Empty;
    /// <summary>Optional design notes.</summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>A single editable junction rule row in the Language Builder phonology step.</summary>
public class JunctionRuleRow
{
    /// <summary>The input consonant cluster that triggers the rule (e.g. "–l + vr–").</summary>
    public string Cluster { get; set; } = string.Empty;
    /// <summary>Plain-language description of the transformation.</summary>
    public string Rule { get; set; } = string.Empty;
    /// <summary>Worked example in the formal register.</summary>
    public string FormalExample { get; set; } = string.Empty;
    /// <summary>Worked example in the administrative register.</summary>
    public string AdministrativeExample { get; set; } = string.Empty;
}

/// <summary>A single editable affix row (prefix or suffix) in the Language Builder affixes step.</summary>
public class AffixRow
{
    /// <summary>Written form of the affix (e.g. "kael–", "–eth").</summary>
    public string Form { get; set; } = string.Empty;
    /// <summary>Grammatical category (e.g. "polity", "keeper/agent").</summary>
    public string Category { get; set; } = string.Empty;
    /// <summary>What the affix contributes to a compound.</summary>
    public string CoreMeaning { get; set; } = string.Empty;
    /// <summary>Optional design notes or variant forms.</summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>A single editable root entry in the Language Builder root dictionary step.</summary>
public class RootRow
{
    /// <summary>Root form as written (e.g. "kael", "vel").</summary>
    public string Token { get; set; } = string.Empty;
    /// <summary>Principal meaning of this root.</summary>
    public string CoreMeaning { get; set; } = string.Empty;
    /// <summary>Secondary or derived meanings.</summary>
    public string ExtendedMeanings { get; set; } = string.Empty;
    /// <summary>A sample derived word demonstrating this root in use.</summary>
    public string ExampleWord { get; set; } = string.Empty;
    /// <summary>Optional thematic category (e.g. "metaphysical", "geographic").</summary>
    public string Category { get; set; } = string.Empty;
}

/// <summary>A single editable register row in the Language Builder register system step.</summary>
public class RegisterRow
{
    /// <summary>Register name (e.g. "Administrative", "Formal", "Vaeld").</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>When and where this register is used.</summary>
    public string Usage { get; set; } = string.Empty;
    /// <summary>Sentence order convention (e.g. "SVO", "VOS").</summary>
    public string WordOrder { get; set; } = string.Empty;
    /// <summary>Characteristic vowel quality (e.g. "Short vowels throughout").</summary>
    public string VowelQuality { get; set; } = string.Empty;
    /// <summary>Optional additional notes.</summary>
    public string Notes { get; set; } = string.Empty;
}

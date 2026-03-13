namespace RealmEngine.Data.Entities;

/// <summary>
/// Craftable material catalog entry. TypeKey = material family (e.g. "ingots", "wood", "leather").
/// CostScale feeds the budget formula: cost = (6000 / RarityWeight) × CostScale.
/// </summary>
public class Material : ContentBase
{
    /// <summary>"metal" | "wood" | "leather" | "gem" | "fabric" | "bone" | "stone" | etc.</summary>
    public string MaterialFamily { get; set; } = string.Empty;

    /// <summary>Budget formula multiplier: cost = (6000 / RarityWeight) × CostScale.</summary>
    public float CostScale { get; set; } = 1.0f;

    /// <summary>Physical and magical properties of the material.</summary>
    public MaterialStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags for this material.</summary>
    public MaterialTraits Traits { get; set; } = new();
}

/// <summary>Physical and magical statistics owned by a Material.</summary>
public class MaterialStats
{
    /// <summary>Resistance to deformation and cutting (1–10 scale).</summary>
    public float? Hardness { get; set; }
    /// <summary>Density / weight per unit — affects item weight calculations.</summary>
    public float? Weight { get; set; }
    /// <summary>Thermal or magical conductivity (affects enchanting compatibility).</summary>
    public float? Conductivity { get; set; }
    /// <summary>Baseline enchantment absorption power.</summary>
    public float? MagicAffinity { get; set; }
    /// <summary>Base sell/buy value per unit in gold.</summary>
    public int? Value { get; set; }
}

/// <summary>Boolean trait flags owned by a Material.</summary>
public class MaterialTraits
{
    /// <summary>True if the material withstands fire damage.</summary>
    public bool? FireResist { get; set; }
    /// <summary>True if the material can flex without breaking (e.g. leather, fabric).</summary>
    public bool? Flexible { get; set; }
    /// <summary>True if the material shatters easily under impact.</summary>
    public bool? Brittle { get; set; }
    /// <summary>True if the material can receive enchantments.</summary>
    public bool? Enchantable { get; set; }
    /// <summary>True if the material is intrinsically magical.</summary>
    public bool? Magical { get; set; }
    /// <summary>True if the material conducts magical energy (relevant to spellcasting items).</summary>
    public bool? Conductive { get; set; }
}

namespace RealmEngine.Data.Entities;

/// <summary>
/// Material property definition (properties/materials domain).
/// TypeKey = material family (e.g. "metals", "woods", "leathers").
/// CostScale feeds the budget formula: cost = (6000 / RarityWeight) × CostScale.
/// </summary>
public class MaterialProperty : ContentBase
{
    /// <summary>"metal" | "wood" | "leather" | "gem" | "fabric" | "bone" | "stone" | etc.</summary>
    public string MaterialFamily { get; set; } = string.Empty;
    /// <summary>Budget formula multiplier: cost = (6000 / RarityWeight) × CostScale.</summary>
    public float CostScale { get; set; } = 1.0f;

    /// <summary>Physical and magical properties.</summary>
    public MaterialPropertyStats Stats { get; set; } = new();
    /// <summary>Boolean trait flags for this material property definition.</summary>
    public MaterialPropertyTraits Traits { get; set; } = new();
}

/// <summary>Physical and magical statistics owned by a MaterialProperty definition.</summary>
public class MaterialPropertyStats
{
    /// <summary>Resistance to deformation and cutting (1–10 scale).</summary>
    public float? Hardness { get; set; }
    /// <summary>Density / weight per unit — affects item weight calculations.</summary>
    public float? Weight { get; set; }
    /// <summary>Thermal or magical conductivity.</summary>
    public float? Conductivity { get; set; }
    /// <summary>Baseline enchantment absorption power.</summary>
    public float? MagicAffinity { get; set; }
    /// <summary>Resistance to wear expressed as a fractional modifier (0.0–1.0).</summary>
    public float? Durability { get; set; }
    /// <summary>Base sell/buy value per unit in gold.</summary>
    public int? Value { get; set; }
}

/// <summary>Boolean trait flags owned by a MaterialProperty definition.</summary>
public class MaterialPropertyTraits
{
    /// <summary>True if the material conducts electrical or magical energy.</summary>
    public bool? Conducting { get; set; }
    /// <summary>True if the material shatters easily under impact.</summary>
    public bool? Brittle { get; set; }
    /// <summary>True if the material is intrinsically magical.</summary>
    public bool? Magical { get; set; }
    /// <summary>True if the material can flex without breaking.</summary>
    public bool? Flexible { get; set; }
    /// <summary>True if the material is see-through (e.g. crystal).</summary>
    public bool? Transparent { get; set; }
    /// <summary>True if the material withstands fire damage.</summary>
    public bool? Fireproof { get; set; }
    /// <summary>True if the material can receive enchantments.</summary>
    public bool? Enchantable { get; set; }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RealmEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedBudgetSystemConfigs : Migration
    {
        private static readonly DateTimeOffset _seedDate = new(2026, 3, 16, 0, 0, 0, TimeSpan.Zero);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "GameConfigs",
                columns: new[] { "ConfigKey", "Data", "Version", "UpdatedAt" },
                values: new object[]
                {
                    "budget-config",
                    """
                    {
                      "metadata": { "description": "Budget-based item generation configuration", "version": "1.0.0", "type": "budget-config" },
                      "budgetAllocation": {
                        "materialPercentage": 0.30,
                        "componentPercentage": 0.70,
                        "description": "30% of budget allocated to material, 70% to components and modifiers"
                      },
                      "costFormulas": {
                        "material":        { "formula": "inverse_scaled", "numerator": 6000, "field": "rarityWeight", "scaleField": "costScale" },
                        "component":       { "formula": "inverse",        "numerator": 100,  "field": "rarityWeight" },
                        "enchantment":     { "formula": "inverse",        "numerator": 130,  "field": "rarityWeight" },
                        "materialQuality": { "formula": "inverse",        "numerator": 150,  "field": "rarityWeight" }
                      },
                      "patternCosts": {},
                      "minimumCosts": {
                        "materialQuality": 5,
                        "prefix":          3,
                        "suffix":          3,
                        "descriptive":     3,
                        "enchantment":    15,
                        "socket":         10
                      },
                      "budgetRanges": {},
                      "sourceMultipliers": {
                        "enemyLevelMultiplier": 5.0,
                        "shopTierBase":        30,
                        "bossMultiplier":       2.5,
                        "eliteMultiplier":      1.5
                      }
                    }
                    """,
                    1,
                    _seedDate
                });

            migrationBuilder.InsertData(
                table: "GameConfigs",
                columns: new[] { "ConfigKey", "Data", "Version", "UpdatedAt" },
                values: new object[]
                {
                    "material-pools",
                    """
                    {
                      "metadata": { "description": "Material pools for item generation budget system", "version": "1.0.0", "type": "material-pools" },
                      "pools": {
                        "default": {
                          "description": "Default material pool used when no category-specific pool is defined",
                          "common":    [ { "itemRef": "@items/materials:Iron",         "rarityWeight": 40 }, { "itemRef": "@items/materials:Leather", "rarityWeight": 35 }, { "itemRef": "@items/materials:Wood",   "rarityWeight": 25 } ],
                          "uncommon":  [ { "itemRef": "@items/materials:Steel",        "rarityWeight": 40 }, { "itemRef": "@items/materials:Bronze",  "rarityWeight": 30 }, { "itemRef": "@items/materials:Silk",   "rarityWeight": 30 } ],
                          "rare":      [ { "itemRef": "@items/materials:Mithril",      "rarityWeight": 50 }, { "itemRef": "@items/materials:Dragonhide","rarityWeight": 50 } ],
                          "epic":      [ { "itemRef": "@items/materials:Adamantine",   "rarityWeight": 60 }, { "itemRef": "@items/materials:Starmetal", "rarityWeight": 40 } ],
                          "legendary": [ { "itemRef": "@items/materials:Orichalcum",   "rarityWeight": 100 } ]
                        },
                        "metals": {
                          "description": "Metal materials for weapons and heavy armor",
                          "common":    [ { "itemRef": "@items/materials:Iron",        "rarityWeight": 100 } ],
                          "uncommon":  [ { "itemRef": "@items/materials:Steel",       "rarityWeight": 100 } ],
                          "rare":      [ { "itemRef": "@items/materials:Mithril",     "rarityWeight": 100 } ],
                          "epic":      [ { "itemRef": "@items/materials:Adamantine",  "rarityWeight": 100 } ],
                          "legendary": [ { "itemRef": "@items/materials:Orichalcum",  "rarityWeight": 100 } ]
                        },
                        "textiles": {
                          "description": "Textile materials for light armor and accessories",
                          "common":    [ { "itemRef": "@items/materials:Linen",      "rarityWeight": 100 } ],
                          "uncommon":  [ { "itemRef": "@items/materials:Silk",       "rarityWeight": 100 } ],
                          "rare":      [ { "itemRef": "@items/materials:Moonweave",  "rarityWeight": 100 } ],
                          "epic":      [ { "itemRef": "@items/materials:Shadowweave","rarityWeight": 100 } ],
                          "legendary": [ { "itemRef": "@items/materials:Aetherweft", "rarityWeight": 100 } ]
                        },
                        "leathers": {
                          "description": "Leather materials for medium armor and accessories",
                          "common":    [ { "itemRef": "@items/materials:Leather",      "rarityWeight": 100 } ],
                          "uncommon":  [ { "itemRef": "@items/materials:ScaledLeather","rarityWeight": 100 } ],
                          "rare":      [ { "itemRef": "@items/materials:Dragonhide",   "rarityWeight": 100 } ],
                          "epic":      [ { "itemRef": "@items/materials:VoidLeather",  "rarityWeight": 100 } ],
                          "legendary": [ { "itemRef": "@items/materials:Soulhide",     "rarityWeight": 100 } ]
                        }
                      }
                    }
                    """,
                    1,
                    _seedDate
                });

            migrationBuilder.InsertData(
                table: "GameConfigs",
                columns: new[] { "ConfigKey", "Data", "Version", "UpdatedAt" },
                values: new object[]
                {
                    "enemy-types",
                    """
                    {
                      "metadata": { "description": "Enemy type budget multipliers", "version": "1.0.0", "type": "enemy-types" },
                      "types": {
                        "default":   { "budgetMultiplier": 1.0, "description": "Standard enemy" },
                        "boss":      { "budgetMultiplier": 2.5, "description": "Boss enemy - significantly increased budget for rare/epic loot" },
                        "elite":     { "budgetMultiplier": 1.5, "description": "Elite enemy - moderately increased budget" },
                        "minion":    { "budgetMultiplier": 0.7, "description": "Weak minion - reduced budget for common loot" },
                        "champion":  { "budgetMultiplier": 2.0, "description": "Champion enemy - doubled budget" },
                        "legendary": { "budgetMultiplier": 3.5, "description": "Legendary enemy - maximum budget for legendary loot" }
                      }
                    }
                    """,
                    1,
                    _seedDate
                });

            migrationBuilder.InsertData(
                table: "GameConfigs",
                columns: new[] { "ConfigKey", "Data", "Version", "UpdatedAt" },
                values: new object[]
                {
                    "material-filters",
                    """
                    {
                      "metadata": { "description": "Per-category material type filters for item generation", "version": "1.0.0", "type": "material-filters" },
                      "defaults": {
                        "unknown": { "description": "Unknown item type - no material restrictions", "allowedMaterials": [] }
                      },
                      "categories": {
                        "weapons": {
                          "description": "Weapon material restrictions by sub-type",
                          "defaultMaterials": [ "metals" ],
                          "types": {
                            "bow":    { "allowedMaterials": [ "woods", "leathers" ] },
                            "staff":  { "allowedMaterials": [ "woods" ] },
                            "wand":   { "allowedMaterials": [ "woods" ] },
                            "dagger": { "allowedMaterials": [ "metals", "leathers" ] }
                          }
                        },
                        "armor": {
                          "description": "Armor material restrictions by sub-type",
                          "defaultMaterials": [ "metals" ],
                          "types": {
                            "robe":     { "allowedMaterials": [ "textiles" ] },
                            "cloak":    { "allowedMaterials": [ "textiles" ] },
                            "tunic":    { "allowedMaterials": [ "leathers", "textiles" ] },
                            "leggings": { "allowedMaterials": [ "leathers", "textiles" ] },
                            "boots":    { "allowedMaterials": [ "leathers" ] },
                            "gloves":   { "allowedMaterials": [ "leathers" ] }
                          }
                        },
                        "accessories": {
                          "description": "Accessory materials (rings, amulets)",
                          "defaultMaterials": [ "metals" ],
                          "types": {}
                        }
                      }
                    }
                    """,
                    1,
                    _seedDate
                });

            migrationBuilder.InsertData(
                table: "GameConfigs",
                columns: new[] { "ConfigKey", "Data", "Version", "UpdatedAt" },
                values: new object[]
                {
                    "socket-config",
                    """
                    {
                      "metadata": { "description": "Socket generation configuration", "version": "1.0.0", "type": "socket-config" },
                      "socketCounts": {
                        "Common":    { "chances": [ 100,  0,  0 ] },
                        "Uncommon":  { "chances": [  80, 20,  0 ] },
                        "Rare":      { "chances": [   0, 50, 50 ] },
                        "Epic":      { "chances": [   0, 30, 70 ] },
                        "Legendary": { "chances": [   0, 10, 90 ] }
                      },
                      "socketTypeWeights": {
                        "Weapon":    { "Gem": 20, "Rune": 40, "Crystal": 20, "Orb": 20 },
                        "Armor":     { "Gem": 30, "Rune": 20, "Crystal": 35, "Orb": 15 },
                        "Accessory": { "Gem": 35, "Rune": 25, "Crystal": 20, "Orb": 20 }
                      }
                    }
                    """,
                    1,
                    _seedDate
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var key in new[] { "budget-config", "material-pools", "enemy-types", "material-filters", "socket-config" })
            {
                migrationBuilder.DeleteData(
                    table: "GameConfigs",
                    keyColumn: "ConfigKey",
                    keyValue: key);
            }
        }
    }
}


using FluentValidation;
using Newtonsoft.Json.Linq;

namespace RealmForge.Validators;

/// <summary>
/// Validator for catalog.json files following JSON v4.0+ standards
/// </summary>
public class CatalogJsonValidator : AbstractValidator<JObject>
{
    public CatalogJsonValidator()
    {
        // Required metadata fields
        RuleFor(x => x["description"])
            .NotNull()
            .WithMessage("'description' is required in catalog files");
        
        RuleFor(x => x["version"])
            .NotNull()
            .WithMessage("'version' is required in catalog files")
            .Must(v => v?.ToString() == "4.0" || v?.ToString().StartsWith("4.") == true || 
                       v?.ToString() == "5.0" || v?.ToString().StartsWith("5.") == true)
            .WithMessage("'version' must be 4.0+ or 5.0+ (current standard)");
        
        RuleFor(x => x["lastUpdated"])
            .NotNull()
            .WithMessage("'lastUpdated' is required in catalog files")
            .Must(BeValidIsoDate)
            .WithMessage("'lastUpdated' must be a valid ISO date string");
        
        RuleFor(x => x["type"])
            .NotNull()
            .WithMessage("'type' is required in catalog files")
            .Must(type => type?.ToString().EndsWith("_catalog") == true)
            .WithMessage("'type' must end with '_catalog'");
        
        // Items array validation
        RuleFor(x => x["items"])
            .NotNull()
            .WithMessage("'items' array is required in catalog files");

        RuleFor(x => x)
            .Must(obj => (obj["items"] as JArray)?.All(i => i["name"] != null) != false)
            .WithMessage("Each item in 'items' must have a 'name' property")
            .When(x => x["items"]?.Type == JTokenType.Array);

        RuleFor(x => x)
            .Must(obj => (obj["items"] as JArray)?.All(i =>
                i["rarityWeight"] != null &&
                (i["rarityWeight"]!.Type == JTokenType.Integer || i["rarityWeight"]!.Type == JTokenType.Float)) != false)
            .WithMessage("Each item must have a numeric 'rarityWeight' property (not 'weight')")
            .When(x => x["items"]?.Type == JTokenType.Array);

        RuleFor(x => x)
            .Must(obj => (obj["items"] as JArray)?.All(i => i["weight"] == null) != false)
            .WithMessage("Use 'rarityWeight' instead of 'weight' in items (JSON v4.0 standard)")
            .When(x => x["items"]?.Type == JTokenType.Array);

        // Warn about 'example' fields (not allowed in v4.0)
        RuleFor(x => x)
            .Must(obj => !ContainsExampleFields(obj))
            .WithMessage("'example' fields are not allowed in JSON v4.0+");
    }
    
    private bool BeValidIsoDate(JToken? token)
    {
        if (token == null) return false;
        return DateTime.TryParse(token.ToString(), out _);
    }
    
    private bool ContainsExampleFields(JObject obj)
    {
        return obj.Descendants()
            .OfType<JProperty>()
            .Any(p => p.Name == "example");
    }
}

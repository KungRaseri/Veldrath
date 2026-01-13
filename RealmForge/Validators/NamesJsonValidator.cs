using FluentValidation;
using Newtonsoft.Json.Linq;

namespace RealmForge.Validators;

/// <summary>
/// Validator for names.json files following JSON v4.0+ standards
/// </summary>
public class NamesJsonValidator : AbstractValidator<JObject>
{
    public NamesJsonValidator()
    {
        // Required metadata fields
        RuleFor(x => x["version"])
            .NotNull()
            .WithMessage("'version' is required")
            .Must(v => v?.ToString() == "4.0" || v?.ToString().StartsWith("4.") == true || 
                       v?.ToString() == "5.0" || v?.ToString().StartsWith("5.") == true)
            .WithMessage("'version' must be 4.0+ or 5.0+");
        
        RuleFor(x => x["type"])
            .NotNull()
            .WithMessage("'type' is required")
            .Must(t => t?.ToString() == "pattern_generation")
            .WithMessage("'type' must be 'pattern_generation' for names.json files");
        
        RuleFor(x => x["supportsTraits"])
            .NotNull()
            .WithMessage("'supportsTraits' is required")
            .Must(t => t?.Type == JTokenType.Boolean)
            .WithMessage("'supportsTraits' must be a boolean");
        
        RuleFor(x => x["lastUpdated"])
            .NotNull()
            .WithMessage("'lastUpdated' is required")
            .Must(BeValidIsoDate)
            .WithMessage("'lastUpdated' must be a valid ISO date string");
        
        RuleFor(x => x["description"])
            .NotNull()
            .WithMessage("'description' is required");
        
        // Patterns array validation
        RuleFor(x => x["patterns"])
            .NotNull()
            .WithMessage("'patterns' array is required");
        
        RuleForEach(x => x["patterns"] as JArray)
            .ChildRules(pattern =>
            {
                pattern.RuleFor(p => p["rarityWeight"])
                    .NotNull()
                    .WithMessage("Each pattern must have 'rarityWeight' (not 'weight')")
                    .Must(w => w?.Type == JTokenType.Integer || w?.Type == JTokenType.Float)
                    .WithMessage("'rarityWeight' must be a number");
                
                // Ensure no 'weight' property
                pattern.RuleFor(p => p["weight"])
                    .Null()
                    .WithMessage("Use 'rarityWeight' instead of 'weight'");
                
                // No example fields allowed
                pattern.RuleFor(p => p["example"])
                    .Null()
                    .WithMessage("'example' fields are not allowed in JSON v4.0+");
            })
            .When(x => x["patterns"] != null && x["patterns"].Type == JTokenType.Array);
        
        // Components validation
        RuleFor(x => x["components"])
            .NotNull()
            .WithMessage("'components' object is required");
        
        // Check for old-style references [@ref/...] instead of new style @domain/path
        RuleFor(x => x)
            .Must(obj => !ContainsOldStyleReferences(obj))
            .WithMessage("Use new reference syntax '@domain/path:item' instead of '[@ref/...]'");
    }
    
    private bool BeValidIsoDate(JToken? token)
    {
        if (token == null) return false;
        return DateTime.TryParse(token.ToString(), out _);
    }
    
    private bool ContainsOldStyleReferences(JObject obj)
    {
        // Check for old-style [@ref/...] patterns
        return obj.Descendants()
            .OfType<JValue>()
            .Any(v => v.Type == JTokenType.String && 
                      v.ToString().Contains("[@") && 
                      v.ToString().Contains("Ref"));
    }
}

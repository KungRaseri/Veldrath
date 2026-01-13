using FluentValidation;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace RealmForge.Validators;

/// <summary>
/// Validator for JSON v4.1+ reference syntax
/// </summary>
public class JsonReferenceValidator : AbstractValidator<JObject>
{
    private static readonly Regex ReferencePattern = new Regex(
        @"^@(?<domain>[^/]+)/(?<path>[^:]+):(?<item>[^?\[\]]+)(\?)?(\[.*\])?(\..*)?$",
        RegexOptions.Compiled);
    
    public JsonReferenceValidator()
    {
        RuleFor(x => x)
            .Must(obj => !ContainsInvalidReferences(obj, out var invalidRefs))
            .WithMessage(x => 
            {
                ContainsInvalidReferences(x, out var invalidRefs);
                return $"Invalid reference syntax found: {string.Join(", ", invalidRefs)}. " +
                       "Use format: @domain/path:item-name";
            });
    }
    
    private bool ContainsInvalidReferences(JObject obj, out List<string> invalidReferences)
    {
        invalidReferences = new List<string>();
        
        var allStringValues = obj.Descendants()
            .OfType<JValue>()
            .Where(v => v.Type == JTokenType.String && v.ToString().StartsWith("@"));
        
        foreach (var value in allStringValues)
        {
            var refString = value.ToString();
            
            // Skip if it's a valid reference
            if (ReferencePattern.IsMatch(refString))
                continue;
            
            // It's a reference-like string but invalid
            if (refString.StartsWith("@"))
            {
                invalidReferences.Add(refString);
            }
        }
        
        return invalidReferences.Any();
    }
}

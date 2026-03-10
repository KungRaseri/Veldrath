using FluentValidation;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmForge.Validators;
using FluentValidation.Results;

namespace RealmForge.Services;

/// <summary>
/// Service for validating JSON data against RealmEngine JSON v4.0+ standards
/// </summary>
public class ModelValidationService
{
    private readonly ILogger<ModelValidationService> _logger;
    private readonly Dictionary<string, IValidator<JObject>> _validators;

    public ModelValidationService(ILogger<ModelValidationService> logger)
    {
        _logger = logger;
        
        // Register validators for different JSON types
        _validators = new Dictionary<string, IValidator<JObject>>(StringComparer.OrdinalIgnoreCase)
        {
            { "catalog", new CatalogJsonValidator() },
            { "names", new NamesJsonValidator() },
            { "reference", new JsonReferenceValidator() }
        };
    }

    /// <summary>
    /// Validate a JSON object against JSON v4.0+ standards
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(JObject jsonObject, string? modelType = null)
    {
        _logger.LogDebug("Validating JSON object, type: {ModelType}", modelType ?? "auto-detect");
        
        try
        {
            // Auto-detect model type from JSON if not provided
            if (string.IsNullOrEmpty(modelType))
            {
                modelType = DetectModelType(jsonObject);
                _logger.LogDebug("Auto-detected model type: {Type}", modelType);
            }
            
            // Normalize: if modelType is a filename, extract the validator key from it
            if (!_validators.ContainsKey(modelType))
            {
                var baseName = Path.GetFileNameWithoutExtension(modelType);
                modelType = _validators.Keys
                    .FirstOrDefault(k => baseName.Contains(k, StringComparison.OrdinalIgnoreCase))
                    ?? modelType;
            }
            
            // Get appropriate validator
            if (!_validators.TryGetValue(modelType, out var validator))
            {
                _logger.LogWarning("No validator found for type: {Type}", modelType);
                return new ValidationResult(); // Empty result = valid
            }
            
            // Run validation
            var result = await validator.ValidateAsync(jsonObject);
            
            if (!result.IsValid)
            {
                _logger.LogWarning("Validation failed with {Count} errors", result.Errors.Count);
            }
            else
            {
                _logger.LogDebug("Validation succeeded");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during validation");
            return new ValidationResult(new List<ValidationFailure>
            {
                new ValidationFailure("Validation", $"Validation error: {ex.Message}")
            });
        }
    }

    /// <summary>
    /// Validate JSON syntax only
    /// </summary>
    public ValidationResult ValidateJsonSyntax(string jsonText)
    {
        try
        {
            JObject.Parse(jsonText);
            _logger.LogDebug("JSON syntax is valid");
            return new ValidationResult(); // Empty result = valid
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid JSON syntax");
            return new ValidationResult(new List<ValidationFailure>
            {
                new ValidationFailure("JSON Syntax", ex.Message)
            });
        }
    }
    
    /// <summary>
    /// Detect model type from JSON metadata
    /// </summary>
    private string DetectModelType(JObject jsonObject)
    {
        // Check 'type' field
        var typeField = jsonObject["type"]?.ToString();
        
        if (!string.IsNullOrEmpty(typeField))
        {
            if (typeField.EndsWith("_catalog"))
                return "catalog";
            
            if (typeField == "pattern_generation")
                return "names";
        }
        
        // Check for catalog-specific fields
        if (jsonObject["items"] != null && jsonObject["description"] != null)
            return "catalog";
        
        // Check for names-specific fields
        if (jsonObject["patterns"] != null && jsonObject["components"] != null)
            return "names";
        
        // Default to reference validation
        return "reference";
    }
}

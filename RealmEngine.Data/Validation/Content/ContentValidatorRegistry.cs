namespace RealmEngine.Data.Validation.Content;

/// <summary>
/// Maps content-type keys (the same keys <c>ContentSchemaRegistry</c> advertises) to the validator that handles them.
/// </summary>
/// <remarks>
/// This is the single place that knows which validator belongs to which type, which is what lets the write endpoint
/// resolve a validator from a URL segment and the CI guard iterate the families without either of them growing a
/// switch statement. Several keys legitimately share one validator: <c>item</c>/<c>weapon</c>/<c>armor</c> are one
/// table, and <c>ability</c>/<c>spell</c> are one <c>Power</c> table.
/// </remarks>
public sealed class ContentValidatorRegistry
{
    private readonly Dictionary<string, IContentValidator> _validators;

    private ContentValidatorRegistry(Dictionary<string, IContentValidator> validators)
    {
        _validators = validators;
    }

    /// <summary>Gets the content-type keys this registry can validate.</summary>
    public IReadOnlyCollection<string> ContentTypes => _validators.Keys;

    /// <summary>
    /// Creates the default registry: one validator per content family the engine stores.
    /// </summary>
    /// <returns>A registry covering every content-type key in <c>ContentSchemaRegistry</c>.</returns>
    public static ContentValidatorRegistry CreateDefault()
    {
        var item = new ItemValidator();
        var power = new PowerValidator();

        var validators = new Dictionary<string, IContentValidator>(StringComparer.OrdinalIgnoreCase)
        {
            [item.ContentType] = item,
            ["weapon"] = item,
            ["armor"] = item,
            [power.ContentType] = power,
            ["spell"] = power,
            [new QuestValidator().ContentType] = new QuestValidator(),
            [new RecipeValidator().ContentType] = new RecipeValidator(),
            [new LootTableValidator().ContentType] = new LootTableValidator(),
            [new ZoneLocationValidator().ContentType] = new ZoneLocationValidator(),
            [new ActorArchetypeValidator().ContentType] = new ActorArchetypeValidator(),
            [new ActorInstanceValidator().ContentType] = new ActorInstanceValidator(),
            [new DialogueValidator().ContentType] = new DialogueValidator(),
        };

        // Families whose only rules are the shared structural ones.
        foreach (var contentType in StructuralContentTypes)
        {
            validators[contentType] = new StructuralContentValidator(contentType);
        }

        return new ContentValidatorRegistry(validators);
    }

    /// <summary>Gets the content-type keys handled by <see cref="StructuralContentValidator"/>.</summary>
    public static IReadOnlyList<string> StructuralContentTypes { get; } =
    [
        "species", "class", "background", "skill", "material", "materialproperty", "enchantment", "organization",
        "language",
    ];

    /// <summary>
    /// Looks up the validator for a content type.
    /// </summary>
    /// <param name="contentType">Content-type key, e.g. <c>item</c> or <c>quest</c>.</param>
    /// <param name="validator">The validator, when found.</param>
    /// <returns>True when the type is handled.</returns>
    public bool TryGetValidator(string contentType, out IContentValidator validator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        return _validators.TryGetValue(contentType, out validator!);
    }

    /// <summary>
    /// Validates an entity of the given content type.
    /// </summary>
    /// <param name="contentType">Content-type key, e.g. <c>item</c> or <c>quest</c>.</param>
    /// <param name="entity">The entity to validate.</param>
    /// <param name="catalog">The slugs that exist, for reference checks.</param>
    /// <returns>The issues found.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no validator handles <paramref name="contentType"/>.</exception>
    public ContentValidationResult Validate(
        string contentType,
        object entity,
        ContentReferenceCatalog catalog)
    {
        if (!TryGetValidator(contentType, out var validator))
        {
            throw new KeyNotFoundException(
                $"No content validator is registered for type '{contentType}'. Known types: {string.Join(", ", ContentTypes.Order(StringComparer.OrdinalIgnoreCase))}.");
        }

        return validator.Validate(entity, catalog);
    }
}

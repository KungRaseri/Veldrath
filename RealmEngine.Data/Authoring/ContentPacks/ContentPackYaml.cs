using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace RealmEngine.Data.Authoring.ContentPacks;

/// <summary>
/// Converts a content-pack YAML document into the JSON tree the content entities bind to.
/// </summary>
/// <remarks>
/// The pack format is deliberately not a schema of its own: a pack entity is the entity's JSON payload written as
/// YAML, so a field name, a number and a boolean mean exactly what <c>ContentSchemaRegistry</c> says they mean. That
/// is why the conversion decides scalar types here instead of letting a serializer guess — YamlDotNet's object
/// deserializer hands back every scalar as a string, which would turn <c>weight: 2.5</c> into <c>"2.5"</c> and make
/// the entity bind fail on a rule the author cannot see.
/// <para>
/// Typing follows YAML 1.2 core schema for plain scalars only: <c>null</c>/<c>~</c>/empty, <c>true</c>/<c>false</c>,
/// integers, decimal floats. Quoted, literal (<c>|</c>) and folded (<c>&gt;</c>) scalars are always kept as strings, so
/// an author can write <c>value: "007"</c> and keep the leading zero.
/// </para>
/// </remarks>
public static partial class ContentPackYaml
{
    /// <summary>
    /// Parses one YAML document into a JSON node tree.
    /// </summary>
    /// <param name="yaml">The YAML text.</param>
    /// <param name="error">Why the text could not be read, or <c>null</c> on success.</param>
    /// <returns>The document root, or <c>null</c> when <paramref name="error"/> is set.</returns>
    public static JsonNode? ToJson(string yaml, out string? error)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        var stream = new YamlStream();
        try
        {
            using var reader = new StringReader(yaml);
            stream.Load(reader);
        }
        catch (YamlException ex)
        {
            error = $"YAML could not be parsed: {ex.Message}";
            return null;
        }

        if (stream.Documents.Count != 1)
        {
            error = $"A pack file must hold exactly one YAML document; this one holds {stream.Documents.Count}.";
            return null;
        }

        try
        {
            var converted = Convert(stream.Documents[0].RootNode);
            error = null;
            return converted;
        }
        catch (YamlException ex)
        {
            // Anchors that cannot be resolved surface here rather than as an obscure null further down.
            error = $"YAML could not be converted: {ex.Message}";
            return null;
        }
    }

    private static JsonNode? Convert(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                {
                    var result = new JsonObject();
                    foreach (var pair in mapping.Children)
                    {
                        var key = pair.Key is YamlScalarNode keyScalar
                            ? keyScalar.Value ?? string.Empty
                            : pair.Key.ToString() ?? string.Empty;
                        result[key] = Convert(pair.Value);
                    }

                    return result;
                }

            case YamlSequenceNode sequence:
                {
                    var result = new JsonArray();
                    foreach (var child in sequence.Children)
                    {
                        result.Add(Convert(child));
                    }

                    return result;
                }

            case YamlScalarNode scalar:
                return ConvertScalar(scalar);

            default:
                return JsonValue.Create(node.ToString());
        }
    }

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        var text = scalar.Value ?? string.Empty;

        // Quoted, block and explicitly tagged scalars are strings: the author asked for text.
        // A plain scalar carries either no tag or YAML's non-specific tag ("?"); anything else is explicit.
        if (scalar.Style != ScalarStyle.Plain
            || (!scalar.Tag.IsEmpty && !scalar.Tag.IsNonSpecific))
        {
            return JsonValue.Create(text);
        }

        if (text.Length == 0 || text is "~" || text.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (bool.TryParse(text, out var boolean))
        {
            return JsonValue.Create(boolean);
        }

        if (IntegerPattern().IsMatch(text)
            && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
        {
            return JsonValue.Create(integer);
        }

        if (FloatPattern().IsMatch(text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
        {
            return JsonValue.Create(real);
        }

        return JsonValue.Create(text);
    }

    [GeneratedRegex("^[-+]?[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex FloatPattern();
}

namespace Veldrath.Server.Features.Zones;

/// <summary>
/// The SignalR group name that scopes a zone, and the only place its format is composed or read.
/// </summary>
/// <remarks>
/// <para>
/// Two shapes exist. A public scope is <c>zone:{zoneId}</c>, or <c>zone:{zoneId}_{difficultyMode}</c> for
/// wilderness; an instance scope appends an allocated run id as <c>zone:{zoneId}#i1:{runId}</c>. The
/// <c>#i1:</c> segment is versioned so a future change to the format is detectable instead of being
/// silently reinterpreted as an instance id.
/// </para>
/// <para>
/// Identity travels as <c>ZoneInstance.Id</c>; this type is transport. Parsing exists for diagnostics and
/// the operator endpoints — no business rule may decide which run it is in by reading the group name,
/// because a mis-composed name would then be indistinguishable from a real run.
/// </para>
/// </remarks>
public readonly record struct ZoneGroupName
{
    /// <summary>The prefix every zone scope carries.</summary>
    public const string ZonePrefix = "zone:";

    /// <summary>The versioned marker that separates the zone from an instance run id.</summary>
    public const string InstanceMarker = "#i1:";

    private ZoneGroupName(string value) => Value = value;

    /// <summary>Gets the group name as SignalR sees it.</summary>
    public string Value { get; }

    /// <summary>Gets a value indicating whether this scope belongs to an instance run rather than the shared zone.</summary>
    public bool IsInstance => Value.Contains(InstanceMarker, StringComparison.Ordinal);

    /// <summary>
    /// Gets the run id the name carries, or <see langword="null"/> for a public scope. Intended for
    /// diagnostics and operator tooling only.
    /// </summary>
    public Guid? InstanceId => TryParse(Value, out var parsed) && parsed.IsInstance
        ? Guid.Parse(Value[(Value.IndexOf(InstanceMarker, StringComparison.Ordinal) + InstanceMarker.Length)..])
        : null;

    /// <summary>Builds the shared scope for a zone.</summary>
    /// <param name="zoneId">Zone slug.</param>
    /// <param name="difficultyMode">Difficulty suffix for wilderness zones, or <see langword="null"/> for none.</param>
    /// <returns>The public group name for the zone.</returns>
    /// <exception cref="ArgumentException">The zone id or difficulty mode contains a character the format cannot carry.</exception>
    public static ZoneGroupName ForZone(string zoneId, string? difficultyMode = null) =>
        new(string.Concat(ZonePrefix, ComposeZonePart(zoneId, difficultyMode)));

    /// <summary>Builds the scope for one instance run.</summary>
    /// <param name="zoneId">Zone slug.</param>
    /// <param name="difficultyMode">Difficulty suffix for wilderness zones, or <see langword="null"/> for none.</param>
    /// <param name="instanceId">The run id this scope belongs to.</param>
    /// <returns>The group name for the run.</returns>
    /// <exception cref="ArgumentException">The zone id or difficulty mode contains a character the format cannot carry.</exception>
    public static ZoneGroupName ForInstance(string zoneId, string? difficultyMode, Guid instanceId)
    {
        if (instanceId == Guid.Empty)
            throw new ArgumentException("An instance scope needs a run id.", nameof(instanceId));

        return new(string.Concat(
            ZonePrefix,
            ComposeZonePart(zoneId, difficultyMode),
            InstanceMarker,
            instanceId.ToString("D")));
    }

    /// <summary>Reads a group name back into its parts.</summary>
    /// <param name="value">Candidate group name.</param>
    /// <param name="group">The parsed group when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the name matches one of the supported shapes.</returns>
    public static bool TryParse(string? value, out ZoneGroupName group)
    {
        group = default;

        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(ZonePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var body = value[ZonePrefix.Length..];
        if (body.Length == 0)
            return false;

        var markerIndex = body.IndexOf(InstanceMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            if (body.Any(char.IsWhiteSpace) || body.Contains(InstanceMarker, StringComparison.Ordinal))
                return false;

            group = new ZoneGroupName(value);
            return true;
        }

        var zonePart = body[..markerIndex];
        var runPart = body[(markerIndex + InstanceMarker.Length)..];

        if (zonePart.Length == 0 ||
            zonePart.Any(char.IsWhiteSpace) ||
            runPart.Contains(InstanceMarker, StringComparison.Ordinal) ||
            !Guid.TryParseExact(runPart, "D", out var runId) ||
            runId == Guid.Empty)
        {
            return false;
        }

        group = new ZoneGroupName(value);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static string ComposeZonePart(string zoneId, string? difficultyMode)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            throw new ArgumentException("A zone group needs a zone id.", nameof(zoneId));

        if (zoneId.Contains('#') || zoneId.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                $"Zone id '{zoneId}' contains a character the group format cannot carry.",
                nameof(zoneId));
        }

        if (string.IsNullOrWhiteSpace(difficultyMode))
            return zoneId;

        if (difficultyMode.Contains('#') || difficultyMode.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                $"Difficulty mode '{difficultyMode}' contains a character the group format cannot carry.",
                nameof(difficultyMode));
        }

        return string.Concat(zoneId, "_", difficultyMode);
    }
}

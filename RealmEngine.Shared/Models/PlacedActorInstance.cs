namespace RealmEngine.Shared.Models;

/// <summary>
/// A named actor instance standing at one zone location, with its overrides already composed onto the archetype's
/// template.</summary>
/// <remarks>
/// The spawn path reads this instead of a bare archetype: <see cref="Template"/> is what combat sees, so composing the
/// overrides here (rather than at the call site) is what makes a reloaded boss keep its authored damage as well as its
/// snapshotted health.
/// </remarks>
/// <param name="Slug">Slug of the actor instance row, the identity that survives a reload.</param>
/// <param name="ArchetypeSlug">Slug of the archetype the instance overrides.</param>
/// <param name="DisplayName">Name to show for this actor, already resolved against the instance and its archetype.</param>
/// <param name="TypeKey">The instance's origin category, e.g. <c>boss</c> or <c>story</c>.</param>
/// <param name="LocationSlug">The zone location the instance stands at, or an empty string for a catalogue-only row.</param>
/// <param name="Template">The projected and composed enemy template a spawn is copied from.</param>
public record PlacedActorInstance(
    string Slug,
    string ArchetypeSlug,
    string DisplayName,
    string TypeKey,
    string LocationSlug,
    Enemy Template);

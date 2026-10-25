using System.ComponentModel.DataAnnotations;

namespace Veldrath.Server.Settings;

/// <summary>
/// Onboarding settings. The tutorial start location is authored in content
/// (<c>ZoneLocationTraits.IsTutorialStart</c>); only the quest is named here, because a quest slug is content the
/// server must be told about rather than guess.
/// </summary>
public sealed class TutorialOptions
{
    /// <summary>Configuration section name for these options.</summary>
    public const string SectionName = "Tutorial";

    /// <summary>Gets or sets the slug of the quest new characters start with.</summary>
    [Required]
    [MinLength(1)]
    public string TutorialQuestSlug { get; set; } = "first-steps";
}

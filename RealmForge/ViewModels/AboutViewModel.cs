namespace RealmForge.ViewModels;

public class AboutViewModel
{
    public string AppName    => "RealmForge";
    public string AppVersion => "v4.0";
    public string AppDescription => "Database content editor for RealmEngine — create and manage game entities stored in PostgreSQL.";

    public IReadOnlyList<IconCredit> IconCredits { get; } =
    [
        new("Lorc",       "https://lorcblog.blogspot.com",  "crossed-swords, world, magic-swirl, cog, frankenstein-creature, stone-crafting, gears, treasure-map, magic-palm, knapsack"),
        new("Delapouite", "https://delapouite.com",          "chest, private-first-class, scroll-quill, skills, organigram, meeple-group, leather-armor, health-potion, castle, meeple-army, chat-bubble, spell-book, crafting"),
    ];

    public record IconCredit(string Artist, string Url, string Icons);
}

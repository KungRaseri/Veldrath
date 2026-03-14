using System.Windows.Input;

namespace RealmForge.ViewModels;

/// <summary>
/// A single row in the entity list — projects ContentBase scalars from the database.
/// OpenCommand and RequestDeleteCommand are wired by EntityListViewModel after load.
/// </summary>
public class EntityListRowViewModel
{
    public Guid EntityId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public int RarityWeight { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string TableName { get; init; } = string.Empty;

    public string Label => DisplayName ?? Slug;
    public string UpdatedText => UpdatedAt.ToLocalTime().ToString("MM/dd/yy HH:mm");

    // Wired by EntityListViewModel after list load
    public ICommand? OpenCommand { get; set; }
    public ICommand? RequestDeleteCommand { get; set; }
}

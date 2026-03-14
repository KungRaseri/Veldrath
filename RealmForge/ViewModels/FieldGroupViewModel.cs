namespace RealmForge.ViewModels;

/// <summary>
/// A named group of field rows corresponding to one nested JSONB object on an entity
/// (e.g. Stats, Traits, Effects).
/// </summary>
public class FieldGroupViewModel
{
    public string GroupName { get; }
    public IReadOnlyList<FieldRowViewModel> Fields { get; }

    public FieldGroupViewModel(string groupName, IReadOnlyList<FieldRowViewModel> fields)
    {
        GroupName = groupName;
        Fields = fields;
    }
}

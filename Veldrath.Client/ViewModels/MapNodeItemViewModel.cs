namespace Veldrath.Client.ViewModels;

/// <summary>
/// A single selectable map node shown in the native fallback map panel.
/// </summary>
/// <param name="NodeId">Stable node identifier.</param>
/// <param name="DisplayName">Human-readable node label.</param>
/// <param name="NodeType">Node type such as <c>Location</c> or <c>EntryPoint</c>.</param>
/// <param name="IsCurrent">Whether the character currently occupies this node.</param>
public sealed record MapNodeItemViewModel(
    string NodeId,
    string DisplayName,
    string NodeType,
    bool IsCurrent)
{
    /// <summary>Gets the label shown in the node list, including a "you are here" marker when current.</summary>
    public string DisplayLabel => IsCurrent ? $"{DisplayName} (you are here)" : DisplayName;
}

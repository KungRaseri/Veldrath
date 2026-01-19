using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Socketing.Queries;

/// <summary>
/// Query to preview what would happen if a socket operation is performed (validation without committing).
/// </summary>
public record SocketPreviewQuery(
    string EquipmentItemId,
    int SocketIndex,
    ISocketable SocketableItem) : IRequest<SocketPreviewResult>;

/// <summary>
/// Result of socket preview query.
/// </summary>
public class SocketPreviewResult
{
    /// <summary>Gets or sets a value indicating whether the operation would succeed.</summary>
    public bool CanSocket { get; set; }
    
    /// <summary>Gets or sets the validation message.</summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>Gets or sets the traits that would be applied.</summary>
    public Dictionary<string, TraitValue> TraitsToApply { get; set; } = new();
    
    /// <summary>Gets or sets the stat bonuses in display format.</summary>
    public List<StatBonusDto> StatBonuses { get; set; } = new();
    
    /// <summary>Gets or sets a value indicating whether this would complete a link group.</summary>
    public bool WouldActivateLink { get; set; }
    
    /// <summary>Gets or sets the link bonus multiplier if a link would be activated.</summary>
    public double LinkBonusMultiplier { get; set; } = 1.0;
    
    /// <summary>Gets or sets the link size if applicable.</summary>
    public int LinkSize { get; set; }
    
    /// <summary>Gets or sets warnings about the operation (non-blocking).</summary>
    public List<string> Warnings { get; set; } = new();
}

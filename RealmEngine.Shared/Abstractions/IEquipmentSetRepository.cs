using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Repository interface for reading equipment set catalog data.
/// </summary>
public interface IEquipmentSetRepository
{
    /// <summary>Gets an equipment set by its unique identifier.</summary>
    EquipmentSet? GetById(string id);
    
    /// <summary>Gets an equipment set by its name.</summary>
    EquipmentSet? GetByName(string name);
    
    /// <summary>Gets all equipment sets.</summary>
    List<EquipmentSet> GetAll();
}

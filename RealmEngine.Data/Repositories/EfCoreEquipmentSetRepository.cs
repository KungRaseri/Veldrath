using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core-backed repository for equipment set catalog data.
/// Reads <see cref="EquipmentSetEntry"/> rows from the content database
/// and projects them to the shared <see cref="EquipmentSet"/> model.
/// </summary>
public class EfCoreEquipmentSetRepository(ContentDbContext db) : IEquipmentSetRepository
{
    /// <inheritdoc />
    public EquipmentSet? GetById(string id)
    {
        var entity = db.EquipmentSets.AsNoTracking()
            .FirstOrDefault(e => e.Slug == id || e.Id.ToString() == id);
        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public EquipmentSet? GetByName(string name)
    {
        var entity = db.EquipmentSets.AsNoTracking()
            .FirstOrDefault(e => e.DisplayName == name || e.Slug == name);
        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public List<EquipmentSet> GetAll() =>
        db.EquipmentSets.AsNoTracking().ToList().Select(MapToModel).ToList();

    private static EquipmentSet MapToModel(EquipmentSetEntry e) => new()
    {
        Id = e.Id.ToString(),
        Name = e.DisplayName ?? e.Slug,
        Description = e.Description,
        SetItemNames = e.Data.ItemSlugs,
        Bonuses = e.Data.Bonuses.ToDictionary(
            b => b.PiecesRequired,
            b => new SetBonus
            {
                PiecesRequired = b.PiecesRequired,
                Description = b.Description,
                BonusStrength = b.BonusStrength,
                BonusDexterity = b.BonusDexterity,
                BonusConstitution = b.BonusConstitution,
                BonusIntelligence = b.BonusIntelligence,
                BonusWisdom = b.BonusWisdom,
                BonusCharisma = b.BonusCharisma,
                SpecialEffect = b.SpecialEffect,
            }),
    };
}

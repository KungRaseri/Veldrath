using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using DataLanguage = RealmEngine.Data.Entities.Language;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for language catalog data.</summary>
public class EfCoreLanguageRepository(ContentDbContext db, ILogger<EfCoreLanguageRepository> logger)
    : ILanguageRepository
{
    /// <inheritdoc />
    public async Task<List<Language>> GetAllAsync()
    {
        var entities = await db.Languages.AsNoTracking()
            .Where(l => l.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} languages from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Language?> GetBySlugAsync(string slug)
    {
        var entity = await db.Languages.AsNoTracking()
            .Where(l => l.IsActive && l.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Language>> GetByTypeKeyAsync(string typeKey)
    {
        var lower = typeKey.ToLowerInvariant();
        var entities = await db.Languages.AsNoTracking()
            .Where(l => l.IsActive && l.TypeKey != null && l.TypeKey.ToLower() == lower)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static Language MapToModel(DataLanguage e) => new()
    {
        Slug         = e.Slug,
        DisplayName  = e.DisplayName ?? e.Slug,
        TypeKey      = e.TypeKey ?? string.Empty,
        RarityWeight = e.RarityWeight,
        Description  = e.Description ?? string.Empty,
        TonalCharacter = e.TonalCharacter ?? string.Empty,
        SampleText   = e.SampleText,

        ConsonantInventory     = e.Phonology.Consonants
            .Select(c => new ConsonantEntry(c.Symbol, c.Description, c.Notes))
            .ToList(),
        VowelInventory         = e.Phonology.Vowels
            .Select(v => new VowelEntry(v.Symbol, v.Sound, v.Register, v.Notes))
            .ToList(),
        AllowedSyllablePatterns = e.Phonology.AllowedSyllablePatterns,
        AllowedInitialClusters  = e.Phonology.AllowedInitialClusters,
        ForbiddenClusters       = e.Phonology.ForbiddenClusters,
        AllowedFinalClusters    = e.Phonology.AllowedFinalClusters,
        JunctionRules           = e.Phonology.JunctionRules
            .Select(j => new JunctionRule(j.Cluster, j.Rule, j.FormalExample, j.AdministrativeExample))
            .ToList(),

        Roots    = e.Morphology.Roots
            .Select(r => new LanguageRoot(r.Token, r.CoreMeaning, r.ExtendedMeanings, r.ExampleWord, r.Category))
            .ToList(),
        Prefixes = e.Morphology.Prefixes
            .Select(a => new LanguageAffix(a.Form, a.Category, a.CoreMeaning, a.Notes))
            .ToList(),
        Suffixes = e.Morphology.Suffixes
            .Select(a => new LanguageAffix(a.Form, a.Category, a.CoreMeaning, a.Notes))
            .ToList(),

        Registers = e.RegisterSystem.Registers
            .Select(r => new LanguageRegister(r.Name, r.Usage, r.WordOrder, r.VowelQuality, r.Notes))
            .ToList(),
    };
}

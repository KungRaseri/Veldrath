using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmForge.ViewModels;

namespace RealmForge.Services;

/// <summary>
/// Builds the left-panel content tree from <see cref="ContentDomainCatalog"/>, overlaid with
/// entity leaf nodes pulled from ContentRegistry. All domain categories are always present in
/// the tree regardless of whether any entities have been created yet.
/// Throws if the database is unreachable — callers are responsible for catching.
/// </summary>
public class ContentTreeService(IServiceScopeFactory scopeFactory, ILogger<ContentTreeService> logger)
{
    public async Task<IReadOnlyList<FileTreeNodeViewModel>> BuildTreeAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<ContentDbContext>();
        if (db is null)
        {
            logger.LogWarning("ContentDbContext not registered");
            return [];
        }

        // Single query: group all registry entries by (Domain, TypeKey)
        var registryLookup = await db.ContentRegistry
            .AsNoTracking()
            .GroupBy(e => new { e.Domain, e.TypeKey })
            .ToDictionaryAsync(
                g => (g.Key.Domain, g.Key.TypeKey),
                g => g.OrderBy(e => e.Slug).ToList());

        var result = new List<FileTreeNodeViewModel>();

        foreach (var domainGroup in ContentDomainCatalog.All.GroupBy(e => e.DomainGroup))
        {
            var domainEntry = domainGroup.First();
            var domainNode = new FileTreeNodeViewModel
            {
                Name        = domainEntry.DomainLabel,
                FullPath    = domainGroup.Key,
                IsDirectory = true,
            };

            foreach (var entry in domainGroup)
            {
                var typeNode = new FileTreeNodeViewModel
                {
                    Name        = entry.TypeKeyLabel,
                    FullPath    = $"{entry.Domain}/{entry.TypeKey}",
                    IsDirectory = true,
                    TableName   = entry.TableName,
                    Domain      = entry.Domain,
                    TypeKey     = entry.TypeKey,
                    DomainLabel = entry.DomainLabel,
                };

                if (registryLookup.TryGetValue((entry.Domain, entry.TypeKey), out var entities))
                {
                    foreach (var reg in entities)
                    {
                        typeNode.Children.Add(new FileTreeNodeViewModel
                        {
                            Name        = reg.Slug,
                            FullPath    = $"{entry.TableName}/{reg.EntityId}",
                            EntityId    = reg.EntityId,
                            TableName   = entry.TableName,
                            IsDirectory = false,
                        });
                    }
                }

                domainNode.Children.Add(typeNode);
            }

            result.Add(domainNode);
        }

        return result;
    }
}


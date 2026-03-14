using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmForge.ViewModels;

namespace RealmForge.Services;

/// <summary>
/// Builds the left-panel content tree by querying the ContentRegistry routing table.
/// Returns a three-level hierarchy: Domain → TypeKey → Entity (leaf).
/// </summary>
public class ContentTreeService(IServiceScopeFactory scopeFactory, ILogger<ContentTreeService> logger)
{
    public async Task<IReadOnlyList<FileTreeNodeViewModel>> BuildTreeAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<ContentDbContext>();
            if (db is null)
            {
                logger.LogWarning("ContentDbContext not registered — content tree will be empty");
                return [];
            }

            var entries = await db.ContentRegistry
                .AsNoTracking()
                .OrderBy(e => e.Domain)
                .ThenBy(e => e.TypeKey)
                .ThenBy(e => e.Slug)
                .ToListAsync();

            var result = new List<FileTreeNodeViewModel>();

            foreach (var domainGroup in entries.GroupBy(e => e.Domain))
            {
                var domainNode = new FileTreeNodeViewModel
                {
                    Name = FormatLabel(domainGroup.Key),
                    FullPath = domainGroup.Key,
                    IsDirectory = true
                };

                foreach (var typeGroup in domainGroup.GroupBy(e => e.TypeKey))
                {
                    var typeNode = new FileTreeNodeViewModel
                    {
                        Name        = FormatLabel(typeGroup.Key),
                        FullPath    = $"{domainGroup.Key}/{typeGroup.Key}",
                        IsDirectory = true,
                        TableName   = typeGroup.First().TableName,
                        Domain      = domainGroup.Key,
                        TypeKey     = typeGroup.Key,
                        DomainLabel = FormatLabel(domainGroup.Key)
                    };

                    foreach (var entry in typeGroup)
                    {
                        typeNode.Children.Add(new FileTreeNodeViewModel
                        {
                            Name = entry.Slug,
                            FullPath = $"{entry.TableName}/{entry.EntityId}",
                            EntityId = entry.EntityId,
                            TableName = entry.TableName,
                            IsDirectory = false
                        });
                    }

                    domainNode.Children.Add(typeNode);
                }

                result.Add(domainNode);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build content tree");
            return [];
        }
    }

    private static string FormatLabel(string key) =>
        string.Join(" ", key.Replace('/', ' ')
            .Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpper(w[0]) + w[1..]));
}

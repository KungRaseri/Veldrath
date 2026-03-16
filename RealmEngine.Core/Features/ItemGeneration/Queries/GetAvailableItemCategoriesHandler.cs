using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;

namespace RealmEngine.Core.Features.ItemGeneration.Queries;

public class GetAvailableItemCategoriesHandler : IRequestHandler<GetAvailableItemCategoriesQuery, GetAvailableItemCategoriesResult>
{
    private readonly CategoryDiscoveryService _categoryDiscovery;
    private readonly NamePatternService _namePatternService;
    private readonly ILogger<GetAvailableItemCategoriesHandler> _logger;

    public GetAvailableItemCategoriesHandler(
        CategoryDiscoveryService categoryDiscovery,
        NamePatternService namePatternService,
        ILogger<GetAvailableItemCategoriesHandler> logger)
    {
        _categoryDiscovery = categoryDiscovery ?? throw new ArgumentNullException(nameof(categoryDiscovery));
        _namePatternService = namePatternService ?? throw new ArgumentNullException(nameof(namePatternService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<GetAvailableItemCategoriesResult> Handle(GetAvailableItemCategoriesQuery request, CancellationToken cancellationToken)
    {
        var result = new GetAvailableItemCategoriesResult();

        try
        {
            var categories = new List<ItemCategoryInfo>();

            var leafCategories = string.IsNullOrWhiteSpace(request.FilterPattern)
                ? _categoryDiscovery.GetLeafCategories("items")
                : _categoryDiscovery.FindCategories("items", request.FilterPattern);

            foreach (var category in leafCategories.OrderBy(c => c))
            {
                var categoryInfo = new ItemCategoryInfo
                {
                    Category = category,
                    DisplayName = GetDisplayName(category),
                    ParentCategory = GetParentCategory(category)
                };

                var catInfo = _categoryDiscovery.GetCategoryInfo("items", category);
                if (catInfo != null)
                {
                    categoryInfo.ItemCount = catInfo.ItemCount;
                    categoryInfo.HasNamePatterns = _namePatternService.HasPatternSet($"items/{category}");
                }

                categories.Add(categoryInfo);
            }

            result.Categories = categories;
            result.Success = true;

            _logger.LogInformation(
                "Found {Count} available item categories{Filter}",
                result.TotalCategories,
                string.IsNullOrWhiteSpace(request.FilterPattern) ? "" : $" matching '{request.FilterPattern}'");

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available item categories");
            result.Success = false;
            result.ErrorMessage = $"Failed to retrieve categories: {ex.Message}";
            return Task.FromResult(result);
        }
    }

    private string GetDisplayName(string category)
    {
        var parts = category.Split('/');
        var name = parts[^1];
        return char.ToUpper(name[0]) + name.Substring(1);
    }

    private string? GetParentCategory(string category)
    {
        var parts = category.Split('/');
        return parts.Length > 1 ? parts[0] : null;
    }
}
using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.OrganizationCatalog.Queries;

/// <summary>Query that retrieves organization entries from the catalog, optionally filtered by organization type.</summary>
/// <param name="OrgType">When provided, limits results to organizations of this type.</param>
public record GetOrganizationCatalogQuery(string? OrgType = null) : IRequest<IReadOnlyList<OrganizationEntry>>;

/// <summary>Handles <see cref="GetOrganizationCatalogQuery"/> by querying the organization repository.</summary>
public class GetOrganizationCatalogQueryHandler : IRequestHandler<GetOrganizationCatalogQuery, IReadOnlyList<OrganizationEntry>>
{
    private readonly IOrganizationRepository _repository;

    /// <summary>Initializes a new instance of <see cref="GetOrganizationCatalogQueryHandler"/>.</summary>
    /// <param name="repository">Repository used to retrieve organization entries.</param>
    public GetOrganizationCatalogQueryHandler(IOrganizationRepository repository) =>
        _repository = repository;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OrganizationEntry>> Handle(GetOrganizationCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.OrgType is not null)
            return await _repository.GetByTypeAsync(request.OrgType);

        return await _repository.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetOrganizationCatalogQuery"/> inputs.</summary>
public class GetOrganizationCatalogQueryValidator : AbstractValidator<GetOrganizationCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetOrganizationCatalogQueryValidator"/>.</summary>
    public GetOrganizationCatalogQueryValidator()
    {
        When(q => q.OrgType is not null, () =>
        {
            RuleFor(q => q.OrgType).NotEmpty().MaximumLength(100);
        });
    }
}

using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ArmorCatalog.Queries;

/// <summary>Returns all active armors from the catalog.</summary>
public record GetArmorCatalogQuery() : IRequest<IReadOnlyList<Item>>;

/// <summary>Handles <see cref="GetArmorCatalogQuery"/>.</summary>
public class GetArmorCatalogQueryHandler : IRequestHandler<GetArmorCatalogQuery, IReadOnlyList<Item>>
{
    private readonly IArmorRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetArmorCatalogQueryHandler"/>.</summary>
    public GetArmorCatalogQueryHandler(IArmorRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Item>> Handle(GetArmorCatalogQuery request, CancellationToken cancellationToken)
        => await _repo.GetAllAsync();
}

/// <summary>Validates <see cref="GetArmorCatalogQuery"/>.</summary>
public class GetArmorCatalogQueryValidator : AbstractValidator<GetArmorCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetArmorCatalogQueryValidator"/>.</summary>
    public GetArmorCatalogQueryValidator() { }
}

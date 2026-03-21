using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.WeaponCatalog.Queries;

/// <summary>Returns all active weapons from the catalog.</summary>
public record GetWeaponCatalogQuery() : IRequest<IReadOnlyList<Item>>;

/// <summary>Handles <see cref="GetWeaponCatalogQuery"/>.</summary>
public class GetWeaponCatalogQueryHandler : IRequestHandler<GetWeaponCatalogQuery, IReadOnlyList<Item>>
{
    private readonly IWeaponRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetWeaponCatalogQueryHandler"/>.</summary>
    public GetWeaponCatalogQueryHandler(IWeaponRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Item>> Handle(GetWeaponCatalogQuery request, CancellationToken cancellationToken)
        => await _repo.GetAllAsync();
}

/// <summary>Validates <see cref="GetWeaponCatalogQuery"/>.</summary>
public class GetWeaponCatalogQueryValidator : AbstractValidator<GetWeaponCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetWeaponCatalogQueryValidator"/>.</summary>
    public GetWeaponCatalogQueryValidator() { }
}

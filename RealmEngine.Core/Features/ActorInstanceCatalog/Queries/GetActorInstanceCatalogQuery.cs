using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ActorInstanceCatalog.Queries;

/// <summary>Query that retrieves actor instance entries from the catalog, optionally filtered by type key.</summary>
/// <param name="TypeKey">When provided, limits results to actor instances of this type.</param>
public record GetActorInstanceCatalogQuery(string? TypeKey = null) : IRequest<IReadOnlyList<ActorInstanceEntry>>;

/// <summary>Handles <see cref="GetActorInstanceCatalogQuery"/> by querying the actor instance repository.</summary>
public class GetActorInstanceCatalogQueryHandler : IRequestHandler<GetActorInstanceCatalogQuery, IReadOnlyList<ActorInstanceEntry>>
{
    private readonly IActorInstanceRepository _repository;

    /// <summary>Initializes a new instance of <see cref="GetActorInstanceCatalogQueryHandler"/>.</summary>
    /// <param name="repository">Repository used to retrieve actor instance entries.</param>
    public GetActorInstanceCatalogQueryHandler(IActorInstanceRepository repository) =>
        _repository = repository;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActorInstanceEntry>> Handle(GetActorInstanceCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.TypeKey is not null)
            return await _repository.GetByTypeKeyAsync(request.TypeKey);

        return await _repository.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetActorInstanceCatalogQuery"/> inputs.</summary>
public class GetActorInstanceCatalogQueryValidator : AbstractValidator<GetActorInstanceCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetActorInstanceCatalogQueryValidator"/>.</summary>
    public GetActorInstanceCatalogQueryValidator()
    {
        When(q => q.TypeKey is not null, () =>
        {
            RuleFor(q => q.TypeKey).NotEmpty().MaximumLength(100);
        });
    }
}

using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.QuestCatalog.Queries;

/// <summary>Returns all active quests, optionally filtered by <see cref="TypeKey"/>.</summary>
/// <param name="TypeKey">When non-null, limits results to quests with this type key (e.g. "main-story", "side", "repeatable").</param>
public record GetQuestCatalogQuery(string? TypeKey = null) : IRequest<IReadOnlyList<Quest>>;

/// <summary>Handles <see cref="GetQuestCatalogQuery"/>.</summary>
public class GetQuestCatalogQueryHandler : IRequestHandler<GetQuestCatalogQuery, IReadOnlyList<Quest>>
{
    private readonly IQuestRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetQuestCatalogQueryHandler"/>.</summary>
    public GetQuestCatalogQueryHandler(IQuestRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Quest>> Handle(GetQuestCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.TypeKey is not null)
            return await _repo.GetByTypeKeyAsync(request.TypeKey);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetQuestCatalogQuery"/>.</summary>
public class GetQuestCatalogQueryValidator : AbstractValidator<GetQuestCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetQuestCatalogQueryValidator"/>.</summary>
    public GetQuestCatalogQueryValidator()
    {
        When(q => q.TypeKey is not null, () =>
        {
            RuleFor(q => q.TypeKey)
                .NotEmpty().WithMessage("TypeKey filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("TypeKey filter must not exceed 100 characters.");
        });
    }
}

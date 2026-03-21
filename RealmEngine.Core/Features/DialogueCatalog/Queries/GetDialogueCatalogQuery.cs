using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.DialogueCatalog.Queries;

/// <summary>Query that retrieves dialogue entries from the catalog, optionally filtered by speaker.</summary>
/// <param name="Speaker">When provided, limits results to dialogues attributed to this speaker.</param>
public record GetDialogueCatalogQuery(string? Speaker = null) : IRequest<IReadOnlyList<DialogueEntry>>;

/// <summary>Handles <see cref="GetDialogueCatalogQuery"/> by querying the dialogue repository.</summary>
public class GetDialogueCatalogQueryHandler : IRequestHandler<GetDialogueCatalogQuery, IReadOnlyList<DialogueEntry>>
{
    private readonly IDialogueRepository _repository;

    /// <summary>Initializes a new instance of <see cref="GetDialogueCatalogQueryHandler"/>.</summary>
    /// <param name="repository">Repository used to retrieve dialogue entries.</param>
    public GetDialogueCatalogQueryHandler(IDialogueRepository repository) =>
        _repository = repository;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DialogueEntry>> Handle(GetDialogueCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.Speaker is not null)
            return await _repository.GetBySpeakerAsync(request.Speaker);

        return await _repository.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetDialogueCatalogQuery"/> inputs.</summary>
public class GetDialogueCatalogQueryValidator : AbstractValidator<GetDialogueCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetDialogueCatalogQueryValidator"/>.</summary>
    public GetDialogueCatalogQueryValidator()
    {
        When(q => q.Speaker is not null, () =>
        {
            RuleFor(q => q.Speaker).NotEmpty().MaximumLength(100);
        });
    }
}

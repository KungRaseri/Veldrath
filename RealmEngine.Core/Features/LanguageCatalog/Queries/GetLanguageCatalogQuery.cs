using FluentValidation;
using MediatR;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.LanguageCatalog.Queries;

/// <summary>Returns all active languages, optionally filtered by <see cref="TypeKey"/>.</summary>
/// <param name="TypeKey">When non-null, limits results to languages of this family (e.g. "imperial", "elven").</param>
public record GetLanguageCatalogQuery(string? TypeKey = null) : IRequest<IReadOnlyList<Language>>;

/// <summary>Handles <see cref="GetLanguageCatalogQuery"/>.</summary>
public class GetLanguageCatalogQueryHandler : IRequestHandler<GetLanguageCatalogQuery, IReadOnlyList<Language>>
{
    private readonly ILanguageRepository _repo;

    /// <summary>Initializes a new instance of <see cref="GetLanguageCatalogQueryHandler"/>.</summary>
    public GetLanguageCatalogQueryHandler(ILanguageRepository repo) => _repo = repo;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Language>> Handle(GetLanguageCatalogQuery request, CancellationToken cancellationToken)
    {
        if (request.TypeKey is not null)
            return await _repo.GetByTypeKeyAsync(request.TypeKey);

        return await _repo.GetAllAsync();
    }
}

/// <summary>Validates <see cref="GetLanguageCatalogQuery"/>.</summary>
public class GetLanguageCatalogQueryValidator : AbstractValidator<GetLanguageCatalogQuery>
{
    /// <summary>Initializes a new instance of <see cref="GetLanguageCatalogQueryValidator"/>.</summary>
    public GetLanguageCatalogQueryValidator()
    {
        When(q => q.TypeKey is not null, () =>
        {
            RuleFor(q => q.TypeKey)
                .NotEmpty().WithMessage("TypeKey filter must not be empty when provided.")
                .MaximumLength(100).WithMessage("TypeKey filter must not exceed 100 characters.");
        });
    }
}

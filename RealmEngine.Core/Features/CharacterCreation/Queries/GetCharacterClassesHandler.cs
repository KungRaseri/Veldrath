using RealmEngine.Shared.Abstractions;
using MediatR;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Handles the GetCharacterClasses query.
/// </summary>
public class GetCharacterClassesHandler : IRequestHandler<GetCharacterClassesQuery, GetCharacterClassesResult>
{
    private readonly ICharacterClassRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCharacterClassesHandler"/> class.
    /// </summary>
    /// <param name="repository">The character class repository.</param>
    public GetCharacterClassesHandler(ICharacterClassRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Handles the get character classes query and returns all available classes.
    /// </summary>
    /// <param name="request">The get character classes query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the list of character classes.</returns>
    public Task<GetCharacterClassesResult> Handle(GetCharacterClassesQuery request, CancellationToken cancellationToken)
    {
        var classes = _repository.GetAll();

        return Task.FromResult(new GetCharacterClassesResult
        {
            Classes = classes
        });
    }
}
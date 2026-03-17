using RealmEngine.Shared.Abstractions;
using MediatR;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Handles the GetCharacterClass query.
/// </summary>
public class GetCharacterClassHandler : IRequestHandler<GetCharacterClassQuery, GetCharacterClassResult>
{
    private readonly ICharacterClassRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCharacterClassHandler"/> class.
    /// </summary>
    /// <param name="repository">The character class repository.</param>
    public GetCharacterClassHandler(ICharacterClassRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Handles the get character class query and returns the requested class if found.
    /// </summary>
    /// <param name="request">The get character class query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the character class result.</returns>
    public Task<GetCharacterClassResult> Handle(GetCharacterClassQuery request, CancellationToken cancellationToken)
    {
        var characterClass = _repository.GetByName(request.ClassName);

        return Task.FromResult(new GetCharacterClassResult
        {
            Found = characterClass != null,
            CharacterClass = characterClass
        });
    }
}
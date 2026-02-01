using MediatR;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Handler for GetBaseClassesQuery.
/// </summary>
public class GetBaseClassesHandler : IRequestHandler<GetBaseClassesQuery, GetBaseClassesResult>
{
    private readonly ICharacterClassRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetBaseClassesHandler"/> class.
    /// </summary>
    /// <param name="repository">The character class repository.</param>
    public GetBaseClassesHandler(ICharacterClassRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Handles the get base classes query.
    /// </summary>
    public Task<GetBaseClassesResult> Handle(GetBaseClassesQuery request, CancellationToken cancellationToken)
    {
        var baseClasses = _repository.GetBaseClasses();

        return Task.FromResult(new GetBaseClassesResult
        {
            BaseClasses = baseClasses
        });
    }
}

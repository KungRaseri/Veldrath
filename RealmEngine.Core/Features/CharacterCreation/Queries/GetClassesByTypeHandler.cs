using MediatR;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Handler for GetClassesByTypeQuery.
/// </summary>
public class GetClassesByTypeHandler : IRequestHandler<GetClassesByTypeQuery, GetClassesByTypeResult>
{
    private readonly ICharacterClassRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetClassesByTypeHandler"/> class.
    /// </summary>
    /// <param name="repository">The character class repository.</param>
    public GetClassesByTypeHandler(ICharacterClassRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Handles the get classes by type query.
    /// </summary>
    public Task<GetClassesByTypeResult> Handle(GetClassesByTypeQuery request, CancellationToken cancellationToken)
    {
        var classes = _repository.GetClassesByType(request.ClassType);

        return Task.FromResult(new GetClassesByTypeResult
        {
            ClassType = request.ClassType,
            Classes = classes
        });
    }
}

using MediatR;
using RealmEngine.Shared.Abstractions;

namespace RealmEngine.Core.Features.CharacterCreation.Queries;

/// <summary>
/// Handler for GetSubclassesQuery.
/// </summary>
public class GetSubclassesHandler : IRequestHandler<GetSubclassesQuery, GetSubclassesResult>
{
    private readonly ICharacterClassRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetSubclassesHandler"/> class.
    /// </summary>
    /// <param name="repository">The character class repository.</param>
    public GetSubclassesHandler(ICharacterClassRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Handles the get subclasses query.
    /// </summary>
    public Task<GetSubclassesResult> Handle(GetSubclassesQuery request, CancellationToken cancellationToken)
    {
        var subclasses = string.IsNullOrWhiteSpace(request.ParentClassId)
            ? _repository.GetSubclasses()
            : _repository.GetSubclassesForParent(request.ParentClassId);

        return Task.FromResult(new GetSubclassesResult
        {
            ParentClassId = request.ParentClassId,
            Subclasses = subclasses
        });
    }
}

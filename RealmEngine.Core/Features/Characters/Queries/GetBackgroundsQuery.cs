using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Characters.Queries;

/// <summary>
/// Query to retrieve all available character backgrounds
/// </summary>
public record GetBackgroundsQuery(string? FilterByAttribute = null) : IRequest<List<Background>>;

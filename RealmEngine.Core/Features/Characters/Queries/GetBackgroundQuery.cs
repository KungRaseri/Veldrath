using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Characters.Queries;

/// <summary>
/// Query to retrieve a specific background by ID or slug
/// </summary>
public record GetBackgroundQuery(string BackgroundId) : IRequest<Background?>;

using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Difficulty.Queries;

/// <summary>
/// Query to get the current difficulty settings.
/// </summary>
public record GetDifficultySettingsQuery : IRequest<DifficultySettings>
{
}

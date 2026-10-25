using MediatR;
using RealmEngine.Core.Features.Progression.Services;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Handles initializing all skills for a new character.
/// </summary>
public class InitializeCharacterSkillsHandler : IRequestHandler<InitializeCharacterSkillsCommand, InitializeCharacterSkillsResult>
{
    private readonly SkillProgressionService _progressionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitializeCharacterSkillsHandler"/> class.
    /// </summary>
    /// <param name="progressionService">The skill progression service.</param>
    public InitializeCharacterSkillsHandler(SkillProgressionService progressionService)
    {
        _progressionService = progressionService ?? throw new ArgumentNullException(nameof(progressionService));
    }

    /// <summary>
    /// Handles initializing character skills.
    /// </summary>
    /// <param name="request">The initialize command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The initialization result.</returns>
    public Task<InitializeCharacterSkillsResult> Handle(InitializeCharacterSkillsCommand request, CancellationToken cancellationToken)
    {
        _progressionService.InitializeAllSkills(request.Character);

        return Task.FromResult(new InitializeCharacterSkillsResult
        {
            SkillsInitialized = request.Character.Skills.Count,
            SkillIds = request.Character.Skills.Keys.ToList()
        });
    }
}

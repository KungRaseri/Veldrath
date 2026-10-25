using MediatR;
using RealmEngine.Core.Features.Progression.Services;

namespace RealmEngine.Core.Features.Progression.Queries;

/// <summary>
/// Handles getting all skills progress.
/// </summary>
public class GetAllSkillsProgressHandler : IRequestHandler<GetAllSkillsProgressQuery, GetAllSkillsProgressResult>
{
    private readonly SkillProgressionService _progressionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAllSkillsProgressHandler"/> class.
    /// </summary>
    /// <param name="progressionService">The skill progression service.</param>
    public GetAllSkillsProgressHandler(SkillProgressionService progressionService)
    {
        _progressionService = progressionService ?? throw new ArgumentNullException(nameof(progressionService));
    }

    /// <summary>
    /// Handles getting all skills progress.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The skills progress result.</returns>
    public Task<GetAllSkillsProgressResult> Handle(GetAllSkillsProgressQuery request, CancellationToken cancellationToken)
    {
        var progress = _progressionService.GetAllSkillsProgress(request.Character);

        var skills = progress.Select(p => new SkillProgressDisplay
        {
            SkillId = p.SkillId,
            Name = p.Name,
            Category = p.Category,
            CurrentRank = p.CurrentRank,
            CurrentXP = p.CurrentXP,
            XPToNextRank = p.XPToNextRank,
            ProgressPercent = p.ProgressPercent,
            CurrentEffect = p.CurrentEffect,
            NextRankEffect = p.NextRankEffect
        }).ToList();

        return Task.FromResult(new GetAllSkillsProgressResult
        {
            Skills = skills
        });
    }
}

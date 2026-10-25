using MediatR;
using RealmEngine.Core.Features.Progression.Services;

namespace RealmEngine.Core.Features.Progression.Queries;

/// <summary>
/// Handles getting skill progress information.
/// </summary>
public class GetSkillProgressHandler : IRequestHandler<GetSkillProgressQuery, GetSkillProgressResult>
{
    private readonly SkillProgressionService _progressionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetSkillProgressHandler"/> class.
    /// </summary>
    /// <param name="progressionService">The skill progression service.</param>
    public GetSkillProgressHandler(SkillProgressionService progressionService)
    {
        _progressionService = progressionService ?? throw new ArgumentNullException(nameof(progressionService));
    }

    /// <summary>
    /// Handles getting skill progress information.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The skill progress result.</returns>
    public Task<GetSkillProgressResult> Handle(GetSkillProgressQuery request, CancellationToken cancellationToken)
    {
        var progress = _progressionService.GetSkillProgress(request.Character, request.SkillId);

        return Task.FromResult(new GetSkillProgressResult
        {
            SkillId = progress.SkillId,
            Name = progress.Name,
            Category = progress.Category,
            CurrentRank = progress.CurrentRank,
            CurrentXP = progress.CurrentXP,
            XPToNextRank = progress.XPToNextRank,
            ProgressPercent = progress.ProgressPercent,
            CurrentEffect = progress.CurrentEffect,
            NextRankEffect = progress.NextRankEffect
        });
    }
}

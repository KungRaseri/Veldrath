using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.Progression.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Progression.Commands;

/// <summary>
/// Handles awarding skill XP and processing rank-ups.
/// </summary>
public class AwardSkillXPHandler : IRequestHandler<AwardSkillXPCommand, AwardSkillXPResult>
{
    private readonly SkillProgressionService _progressionService;
    private readonly ILogger<AwardSkillXPHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AwardSkillXPHandler"/> class.
    /// </summary>
    /// <param name="progressionService">The skill progression service.</param>
    /// <param name="logger">The logger instance.</param>
    public AwardSkillXPHandler(
        SkillProgressionService progressionService,
        ILogger<AwardSkillXPHandler> logger)
    {
        _progressionService = progressionService ?? throw new ArgumentNullException(nameof(progressionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles awarding skill XP and processing rank-ups.
    /// </summary>
    /// <param name="request">The award skill XP command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The award skill XP result.</returns>
    public Task<AwardSkillXPResult> Handle(AwardSkillXPCommand request, CancellationToken cancellationToken)
    {
        var result = _progressionService.AwardSkillXP(
            request.Character,
            request.SkillId,
            request.XPAmount,
            request.ActionSource);

        return Task.FromResult(new AwardSkillXPResult
        {
            SkillId = result.SkillId,
            NewRank = result.NewRank,
            RanksGained = result.RanksGained,
            Notifications = result.Notifications
        });
    }
}

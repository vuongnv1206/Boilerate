using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging;

namespace Boilerate.Infrastructure.BackgroundJobs;

public class HangfireJobFilter : IElectStateFilter
{
    private readonly ILogger<HangfireJobFilter> _logger;

    public HangfireJobFilter(ILogger<HangfireJobFilter> logger)
    {
        _logger = logger;
    }

    public void OnStateElection(ElectStateContext context)
    {
        if (context.CandidateState is FailedState failedState)
        {
            _logger.LogError(
                failedState.Exception,
                "Job {JobId} ({JobType}.{JobMethod}) failed: {ErrorMessage}",
                context.BackgroundJob.Id,
                context.BackgroundJob.Job?.Type?.Name ?? "Unknown",
                context.BackgroundJob.Job?.Method?.Name ?? "Unknown",
                failedState.Exception?.Message);
        }

        if (context.CandidateState is SucceededState succeededState)
        {
            _logger.LogInformation(
                "Job {JobId} ({JobType}.{JobMethod}) succeeded in {Duration}ms",
                context.BackgroundJob.Id,
                context.BackgroundJob.Job?.Type?.Name ?? "Unknown",
                context.BackgroundJob.Job?.Method?.Name ?? "Unknown",
                succeededState.PerformanceDuration);
        }

        if (context.CandidateState is ProcessingState)
        {
            _logger.LogInformation(
                "Job {JobId} ({JobType}.{JobMethod}) started processing",
                context.BackgroundJob.Id,
                context.BackgroundJob.Job?.Type?.Name ?? "Unknown",
                context.BackgroundJob.Job?.Method?.Name ?? "Unknown");
        }
    }
}

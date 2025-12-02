namespace KamiYomu.Web.Worker.Attributes;

using Hangfire.Common;
using Hangfire.Server;
using KamiYomu.Web.Extensions;
using KamiYomu.Web.Infrastructure.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using static KamiYomu.Web.AppOptions.Defaults;

[AttributeUsage(AttributeTargets.Method)]

public class PerKeyConcurrencyAttribute : JobFilterAttribute, IServerFilter
{
    private readonly string _parameterName;
    private readonly int _rescheduleDelayMinutes;

    private readonly ILogger<PerKeyConcurrencyAttribute> _logger;
    private readonly ILockManager _lockManager;

    private readonly AsyncLocal<IDisposable?> _lockHandle = new();

    public PerKeyConcurrencyAttribute(
        string parameterName,
        int rescheduleDelayMinutes = AppOptions.Defaults.Worker.DeferredExecutionInMinutes)
    {
        _parameterName = parameterName;
        _rescheduleDelayMinutes = rescheduleDelayMinutes;

        _logger = ServiceLocator.Instance.GetRequiredService<ILogger<PerKeyConcurrencyAttribute>>();
        _lockManager = ServiceLocator.Instance.GetRequiredService<ILockManager>();
    }

    public void OnPerforming(PerformingContext context)
    {
        try
        {
            var args = context.BackgroundJob.Job.Args;
            var method = context.BackgroundJob.Job.Method;
            var parameters = method.GetParameters();

            int index = Array.FindIndex(parameters, p => p.Name == _parameterName);

            if (index == -1)
            {
                _logger.LogDebug(
                    "PerKeyConcurrency: Parameter '{Parameter}' not found for job {JobId}.",
                    _parameterName, context.BackgroundJob.Id);
                return;
            }

            string key = args[index]?.ToString() ?? "null";

            var handle = _lockManager.TryAcquireAsync(key);

            if (handle == null)
            {
                var delay = TimeSpan.FromMinutes(_rescheduleDelayMinutes);
                _logger.LogDebug(
                    "PerKeyConcurrency: Job '{JobId}' deferred — key '{Key}' is at max concurrency. Rescheduling in '{Delay}'.",
                    context.BackgroundJob.Id, key, delay);

                context.Canceled = true;

                context.BackgroundJob.EnqueueAfterDelay(delay);

                return;
            }

            _lockHandle.Value = handle;

            _logger.LogDebug(
                "PerKeyConcurrency: Job '{JobId}' acquired lock for key '{Key}'.",
                context.BackgroundJob.Id, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error while processing concurrency for job '{JobId}'", context.BackgroundJob.Id);
        }
    }

    public void OnPerformed(PerformedContext context)
    {
        try
        {
            _lockHandle.Value?.Dispose();
            _lockHandle.Value = null;

            _logger.LogDebug(
                "PerKeyConcurrency: Job '{JobId}' released concurrency lock.",
                context.BackgroundJob.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error releasing lock for job '{JobId}'", context.BackgroundJob.Id);
        }
    }
}

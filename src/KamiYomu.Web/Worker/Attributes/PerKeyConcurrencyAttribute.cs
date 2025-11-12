using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Threading.Tasks;
using static KamiYomu.Web.Settings;

namespace KamiYomu.Web.Worker.Attributes;

using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

[AttributeUsage(AttributeTargets.Method)]
public class PerKeyConcurrencyAttribute : JobFilterAttribute, IServerFilter
{
    private readonly string _parameterName;
    private readonly TimeSpan _lockTimeout;
    private readonly TimeSpan _rescheduleDelay;
    private readonly ILogger _logger;

    public PerKeyConcurrencyAttribute(string parameterName, int timeoutMinutes = 5, int rescheduleDelayMinutes = 5)
    {
        _parameterName = parameterName;
        _lockTimeout = TimeSpan.FromMinutes(timeoutMinutes);
        _rescheduleDelay = TimeSpan.FromMinutes(rescheduleDelayMinutes);

        var factory = ServiceLocator.Instance?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
        _logger = factory?.CreateLogger<PerKeyConcurrencyAttribute>();
    }

    public void OnPerforming(PerformingContext context)
    {
        var args = context.BackgroundJob.Job.Args;
        var method = context.BackgroundJob.Job.Method;
        var parameters = method.GetParameters();

        var index = Array.FindIndex(parameters, p => p.Name == _parameterName);
        if (index == -1 || index >= args.Count)
        {
            _logger.LogWarning(
                "PerKeyConcurrency: Parameter '{Parameter}' not found in job arguments for method '{Method}'. Skipping lock attempt.",
                _parameterName, method.Name);
            return;
        }

        var keyValue = args[index]?.ToString() ?? "null";
        var lockKey = $"lock:{_parameterName}:{keyValue}";

        try
        {
            var handle = context.Connection.AcquireDistributedLock(lockKey, _lockTimeout);
            context.Items["__PerKeyLock"] = handle;

            _logger.LogDebug(
                "PerKeyConcurrency: Lock acquired for key '{Key}' (JobId: {JobId}, Method: {Method}).",
                keyValue, context.BackgroundJob?.Id ?? "unknown", method.Name);
        }
        catch (DistributedLockTimeoutException)
        {
            _logger.LogInformation(
                "PerKeyConcurrency: Job {JobId} ({Method}) skipped — agent for key '{Key}' is currently occupied.",
                context.BackgroundJob?.Id ?? "unknown", method.Name, keyValue);

            context.Canceled = true; // Hangfire will retry based on AutomaticRetryAttribute
        }
    }

    public void OnPerformed(PerformedContext context)
    {
        if (context.Items.TryGetValue("__PerKeyLock", out var handleObj) && handleObj is IDisposable handle)
        {
            handle.Dispose();
            _logger.LogDebug("PerKeyConcurrency: Lock released.");
        }
    }
}

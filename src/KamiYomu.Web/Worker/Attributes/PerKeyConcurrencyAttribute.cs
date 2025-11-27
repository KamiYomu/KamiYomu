namespace KamiYomu.Web.Worker.Attributes;

using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Entities;
using KamiYomu.Web.Infrastructure.Contexts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using static KamiYomu.Web.AppOptions.Defaults;

[AttributeUsage(AttributeTargets.Method)]
public class PerKeyConcurrencyAttribute : JobFilterAttribute, IServerFilter
{
    private readonly string _parameterName;
    private readonly TimeSpan _rescheduleDelay;
    private readonly ILogger _logger;
    private readonly IOptions<WorkerOptions>? _workerOptions;
    private readonly CacheContext? _cacheContext;
    private readonly int _maxConcurrency;

    public PerKeyConcurrencyAttribute(string parameterName, int rescheduleDelayMinutes = 5)
    {
        _parameterName = parameterName;
        _rescheduleDelay = TimeSpan.FromMinutes(rescheduleDelayMinutes);

        var factory = ServiceLocator.Instance?.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
        _logger = factory?.CreateLogger<PerKeyConcurrencyAttribute>();
        _workerOptions = ServiceLocator.Instance?.GetService<IOptions<WorkerOptions>>();
        _cacheContext = ServiceLocator.Instance?.GetService<CacheContext>();
        _maxConcurrency = _workerOptions?.Value.MaxConcurrentCrawlerInstances ?? 1;
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
                "PerKeyConcurrency: Parameter '{Parameter}' not found in job arguments for method '{Method}'. Skipping concurrency check.",
                _parameterName, method.Name);
            return;
        }

        var keyValue = args[index]?.ToString() ?? "null";

        var currentCount = GetCurrentConcurrencyForKey(CrawlerAgent.GetConcurrencyCacheKey(Guid.Parse(keyValue))); 
        if (currentCount >= _maxConcurrency)
        {
            _logger.LogDebug(
                    "PerKeyConcurrency: Job {JobId} ({Method}) deferred — key '{Key}' at max concurrency. Rescheduling in {Delay}.",
                    context.BackgroundJob?.Id ?? "unknown",
                    method.Name,
                    keyValue,
                    _rescheduleDelay);

            context.Canceled = true;

           var job = context.BackgroundJob.Job;

            var originalQueue = job.Queue;

            var scheduled = new ScheduledState(_rescheduleDelay);

            var enqueued = new EnqueuedState(originalQueue);

            var client = new BackgroundJobClient(context.Storage);
            var jobId = client.Create(job, scheduled);

            context.Connection.SetJobParameter(jobId, "Queue", originalQueue);
        }
    }

    public void OnPerformed(PerformedContext context)
    {
        var args = context.BackgroundJob.Job.Args;
        var method = context.BackgroundJob.Job.Method;
        var parameters = method.GetParameters();
        var index = Array.FindIndex(parameters, p => p.Name == _parameterName);
        if (index == -1 || index >= args.Count)
        {
            _logger.LogWarning(
                "PerKeyConcurrency: Parameter '{Parameter}' not found in job arguments for method '{Method}'. Skipping concurrency decrement.",
                _parameterName, method.Name);
            return;
        }
        var keyValue = args[index]?.ToString() ?? "null";
        var newCount = DecrementConcurrencyForKey(CrawlerAgent.GetConcurrencyCacheKey(Guid.Parse(keyValue)));
        _logger.LogDebug(
            "PerKeyConcurrency: Job {JobId} ({Method}) completed — key '{Key}' concurrency decremented to {Count}.",
            context.BackgroundJob?.Id ?? "unknown",
            method.Name,
            keyValue,
            newCount);
    }

    private int DecrementConcurrencyForKey(string cacheKey)
    {
        var current = _cacheContext.Current.Get<int?>(cacheKey);

        if (_cacheContext.Current.IsExpired(cacheKey))
        {
            current = 0;
        }

        var newValue = Math.Max((current ?? -1) - 1, 0);

        TimeSpan expiration;
        var expirationDate = _cacheContext.Current.GetExpiration(cacheKey); 
        var ttl = _rescheduleDelay;

        if (expirationDate.HasValue)
        {
            var remaining = expirationDate.Value - DateTime.UtcNow;
            expiration = remaining > TimeSpan.Zero ? remaining : ttl;
        }
        else
        {
            expiration = ttl;
        }

        _cacheContext.Current.Add(cacheKey, newValue, expiration);

        return newValue;
    }

    private int GetCurrentConcurrencyForKey(string cacheKey)
    {
        var current = _cacheContext.Current.Get<int?>(cacheKey);

        if (_cacheContext.Current.IsExpired(cacheKey))
        {
            current = 0;
        }

        var newValue = (current ?? -1) + 1;

        TimeSpan expiration;
        var expirationDate = _cacheContext.Current.GetExpiration(cacheKey);
        var ttl = _rescheduleDelay;

        if (expirationDate.HasValue)
        {
            var remaining = expirationDate.Value - DateTime.UtcNow;

            expiration = remaining > TimeSpan.Zero ? remaining : ttl;
        }
        else
        {
            expiration = ttl;
        }

        _cacheContext.Current.Add(cacheKey, newValue, expiration);

        return newValue;
    }
}
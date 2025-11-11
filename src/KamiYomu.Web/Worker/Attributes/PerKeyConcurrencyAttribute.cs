using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Threading.Tasks;

namespace KamiYomu.Web.Worker.Attributes;


[AttributeUsage(AttributeTargets.Method)]
public class PerKeyConcurrencyAttribute : JobFilterAttribute, IServerFilter
{
    private readonly string _parameterName;
    private readonly TimeSpan _lockTimeout;

    public PerKeyConcurrencyAttribute(string parameterName, int timeoutSeconds = 300)
    {
        _parameterName = parameterName;
        _lockTimeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    public void OnPerforming(PerformingContext context)
    {
        var args = context.BackgroundJob.Job.Args;
        var method = context.BackgroundJob.Job.Method;
        var parameters = method.GetParameters();

        var index = Array.FindIndex(parameters, p => p.Name == _parameterName);
        if (index == -1 || index >= args.Count)
            throw new InvalidOperationException($"Parameter '{_parameterName}' not found in job arguments.");

        var keyValue = args[index]?.ToString() ?? "null";
        var lockKey = $"lock:manga:{keyValue}";

        var handle = context.Connection.AcquireDistributedLock(lockKey, _lockTimeout);
        context.Items["__PerKeyLock"] = handle;
    }

    public void OnPerformed(PerformedContext context)
    {
        if (context.Items.TryGetValue("__PerKeyLock", out var handleObj) && handleObj is IDisposable handle)
        {
            handle.Dispose();
        }
    }
}

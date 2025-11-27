using Hangfire;
using Hangfire.States;
using Hangfire.Storage;

namespace KamiYomu.Web.Extensions
{
    public static class HangfireExtensions
    {
        public static void EnqueueAfterDelay(this BackgroundJob backgroundJob, TimeSpan delay, JobStorage jobStorage)
        {
            var client = new BackgroundJobClient(jobStorage);

            var newState = new ScheduledState(delay);

            client.Create(backgroundJob.Job, newState);
        }
    }
}

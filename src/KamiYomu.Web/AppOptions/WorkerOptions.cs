namespace KamiYomu.Web.AppOptions
{
    public class WorkerOptions
    {
        private static readonly Random _random = new();
        public TimeSpan GetWaitPeriod()
        {
            int milliseconds = _random.Next(MinWaitPeriodInMilliseconds, MaxWaitPeriodInMilliseconds);
            return TimeSpan.FromMilliseconds(milliseconds);
        }
        public int MinWaitPeriodInMilliseconds { get; init; } = 3000;
        public int MaxWaitPeriodInMilliseconds { get; init; } = 7001;
        public int WorkerCount { get; init; } = 1;
    }
}

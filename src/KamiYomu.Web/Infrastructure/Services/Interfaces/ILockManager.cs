namespace KamiYomu.Web.Infrastructure.Services.Interfaces
{
    public interface ILockManager
    {
        IDisposable? TryAcquireAsync(string crawlerId);
    }
}

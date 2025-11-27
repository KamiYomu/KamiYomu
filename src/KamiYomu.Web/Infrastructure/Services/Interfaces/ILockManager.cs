namespace KamiYomu.Web.Infrastructure.Services.Interfaces
{
    public interface ILockManager
    {
        Task<IDisposable?> TryAcquireAsync(string crawlerType);
    }
}

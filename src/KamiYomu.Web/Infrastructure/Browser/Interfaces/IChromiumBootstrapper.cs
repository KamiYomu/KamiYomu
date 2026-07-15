namespace KamiYomu.Web.Infrastructure.Browser.Interfaces;

public interface IChromiumBootstrapper
{
    /// <summary>
    /// Downloads and installs the Chromium browser if it is not already present, and sets required environment
    /// variables for its usage.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    Task InitializeAsync(CancellationToken cancellationToken);
}

using System.Runtime.CompilerServices;

using KamiYomu.Web.AppOptions;
using KamiYomu.Web.Tests.Infrastructure.Services;

namespace KamiYomu.Web.Tests;

/// <summary>
/// KamiYomu.Web.AppOptions.Defaults.ServiceLocator.Instance is backed by a
/// System.Lazy&lt;IServiceProvider?&gt; that evaluates its factory exactly once for the lifetime of
/// the process; every later Defaults.ServiceLocator.Configure(...) call updates which factory
/// *would* run, but can no longer change what .Instance returns once it has been touched a first
/// time. Several test fixtures (e.g. SharedInfrastructureStateFixture) point the ServiceLocator at
/// a short-lived provider they later Dispose(). If one of those fixtures happens to be the first
/// thing in the whole test run to touch .Instance (which depends on xUnit's test discovery/
/// execution order - itself sensitive to how many test classes exist and in what order they were
/// compiled), every other test needing ServiceLocator.Instance for the rest of the process would
/// observe a disposed provider, regardless of test order otherwise.
///
/// This module initializer runs once, deterministically, before any test executes, and forces the
/// long-lived, never-disposed provider from ServiceTestHelpers to win that race unconditionally.
/// </summary>
internal static class TestAssemblyInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        ServiceTestHelpers.EnsureServiceLocatorConfigured();

        // Force Defaults.ServiceLocator.Instance's backing Lazy<T> to evaluate now, while our
        // factory is the one configured, so it permanently wins the race described above.
        _ = Defaults.ServiceLocator.Instance;
    }
}

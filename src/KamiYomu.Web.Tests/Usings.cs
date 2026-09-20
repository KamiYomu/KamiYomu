global using Moq;

// This test assembly relies heavily on process-wide ambient static state (Hangfire's
// JobStorage.Current, the app's Defaults.ServiceLocator, MonkeyCache's Barrel.ApplicationId).
// Running collections in parallel causes that shared state to be clobbered between unrelated
// tests, producing flaky/order-dependent failures. Disable test parallelization for this
// assembly to make runs deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

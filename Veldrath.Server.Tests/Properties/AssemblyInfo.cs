using System.Diagnostics.CodeAnalysis;
using Xunit;

[assembly: ExcludeFromCodeCoverage]
// Cap parallel factory initializations to prevent thread-pool starvation.
// WebApplicationFactory.EnsureServer() and CreateHost() both block thread-pool
// threads (host.StartAsync + blocking seeding calls). With 18+ fixture classes
// all initialising concurrently the pool starves. Four parallel collections stay
// well inside the default minimum thread-pool size on any dev machine or CI agent.
[assembly: CollectionBehavior(MaxParallelThreads = 4)]

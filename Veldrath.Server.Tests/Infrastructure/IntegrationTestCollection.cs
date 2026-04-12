namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// xUnit test collection that groups all integration tests sharing a single
/// <see cref="WebAppFactory"/> instance. Placing all <c>IClassFixture&lt;WebAppFactory&gt;</c>
/// test classes in this collection ensures the factory is started exactly once
/// and tests run sequentially within the collection, eliminating the
/// thread-pool starvation that occurs when 14+ factories start in parallel.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<WebAppFactory>
{
    // No code needed — this class is the xUnit collection definition anchor.
}

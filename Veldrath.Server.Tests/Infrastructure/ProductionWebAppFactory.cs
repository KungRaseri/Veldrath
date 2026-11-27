using Microsoft.AspNetCore.Hosting;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// The integration host in the <c>Production</c> environment.
/// </summary>
/// <remarks>
/// Used only to prove that environment-gated routes are unreachable once they are deployed. It is a
/// subclass rather than a constructor parameter because xUnit requires a collection fixture type to
/// declare exactly one public constructor; this type is never a fixture, so it may inherit that one.
/// </remarks>
internal sealed class ProductionWebAppFactory : WebAppFactory
{
    /// <inheritdoc/>
    protected override string EnvironmentName => "Production";

    /// <summary>
    /// Production refuses to start without a public browser origin (CORS is a security control, not a
    /// default), so this host declares one. Everything else still runs the real startup path.
    /// </summary>
    /// <param name="builder">The web host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Web:BaseUrl", "https://veldrath.test");
        base.ConfigureWebHost(builder);
    }
}

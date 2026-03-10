using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RealmUnbound.Server.Health;

namespace RealmUnbound.Server.Tests.Features;

public class HealthCheckTests
{
    // ── GameEngineHealthCheck ─────────────────────────────────────────────────

    [Fact]
    public async Task GameEngineHealthCheck_Should_Return_Healthy()
    {
        var check  = new GameEngineHealthCheck();
        var ctx    = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("game-engine", check, null, null)
        };

        var result = await check.CheckHealthAsync(ctx);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task GameEngineHealthCheck_Should_Include_Description()
    {
        var check  = new GameEngineHealthCheck();
        var ctx    = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("game-engine", check, null, null)
        };

        var result = await check.CheckHealthAsync(ctx);

        result.Description.Should().NotBeNullOrWhiteSpace();
    }

    // ── HealthCheckResponseWriter ─────────────────────────────────────────────

    [Fact]
    public async Task WriteResponse_Should_Set_ContentType_To_Json()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(),
            HealthStatus.Healthy,
            TimeSpan.FromMilliseconds(10));

        await HealthCheckResponseWriter.WriteResponse(httpContext, report);

        httpContext.Response.ContentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task WriteResponse_Should_Include_Status_In_Json_Body()
    {
        var httpContext = new DefaultHttpContext();
        var body        = new MemoryStream();
        httpContext.Response.Body = body;

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(),
            HealthStatus.Healthy,
            TimeSpan.FromMilliseconds(5));

        await HealthCheckResponseWriter.WriteResponse(httpContext, report);

        body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(body).ReadToEndAsync();
        json.Should().Contain("Healthy");
    }

    [Fact]
    public async Task WriteResponse_Should_Include_Check_Entries()
    {
        var httpContext = new DefaultHttpContext();
        var body        = new MemoryStream();
        httpContext.Response.Body = body;

        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["database"] = new HealthReportEntry(HealthStatus.Healthy, "DB ok", TimeSpan.Zero, null, null)
        };
        var report = new HealthReport(entries, HealthStatus.Healthy, TimeSpan.FromMilliseconds(5));

        await HealthCheckResponseWriter.WriteResponse(httpContext, report);

        body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(body).ReadToEndAsync();
        json.Should().Contain("database");
    }

    [Fact]
    public async Task WriteResponse_Should_Include_Exception_Message_When_Degraded()
    {
        var httpContext = new DefaultHttpContext();
        var body        = new MemoryStream();
        httpContext.Response.Body = body;

        var ex      = new InvalidOperationException("connection timeout");
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["db"] = new HealthReportEntry(HealthStatus.Degraded, "Slow", TimeSpan.Zero, ex, null)
        };
        var report = new HealthReport(entries, HealthStatus.Degraded, TimeSpan.FromSeconds(1));

        await HealthCheckResponseWriter.WriteResponse(httpContext, report);

        body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(body).ReadToEndAsync();
        json.Should().Contain("connection timeout");
    }
}

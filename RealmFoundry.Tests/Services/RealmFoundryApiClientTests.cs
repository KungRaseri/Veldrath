namespace RealmFoundry.Tests.Services;

public class RealmFoundryApiClientTests
{
    [Fact]
    public void SetBearerToken_SetsAuthorizationHeader()
    {
        var http   = new HttpClient { BaseAddress = new Uri("https://localhost") };
        var client = new RealmFoundryApiClient(http);

        client.SetBearerToken("test-token");

        http.DefaultRequestHeaders.Authorization.Should().NotBeNull();
        http.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        http.DefaultRequestHeaders.Authorization!.Parameter.Should().Be("test-token");
    }

    [Fact]
    public void Constructor_LeavesAuthorizationHeader_Unset()
    {
        var http   = new HttpClient { BaseAddress = new Uri("https://localhost") };
        var client = new RealmFoundryApiClient(http);

        http.DefaultRequestHeaders.Authorization.Should().BeNull();
    }
}

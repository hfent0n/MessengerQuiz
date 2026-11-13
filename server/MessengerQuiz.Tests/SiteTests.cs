using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MessengerQuiz.Tests;

// Running as a hosted site: the health check, security headers, serving the client and visitors' addresses.
public class SiteTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task HealthCheckReportsTheVersion()
    {
        var client = WithSettings(("APP_VERSION", "abc123")).CreateClient();

        var health = await client.GetFromJsonAsync<Health>("/api/health");

        Assert.Equal(new Health("ok", "abc123"), health);
    }

    [Fact]
    public async Task HealthCheckWithoutAVersionSaysDev()
    {
        var health = await factory.CreateClient().GetFromJsonAsync<Health>("/api/health");

        Assert.Equal("dev", health!.Version);
    }

    [Fact]
    public async Task UnknownApiPathsAreNotFound()
    {
        var response = await factory.CreateClient().GetAsync("/api/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GameLinksServeTheClient()
    {
        var webRoot = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(webRoot.FullName, "index.html"), "<p>the client</p>");
            var client = factory.WithWebHostBuilder(builder => builder.UseWebRoot(webRoot.FullName)).CreateClient();

            var page = await client.GetAsync("/play/abc234defg");
            var home = await client.GetAsync("/");

            Assert.Equal("<p>the client</p>", await page.Content.ReadAsStringAsync());
            Assert.Equal("<p>the client</p>", await home.Content.ReadAsStringAsync());
            // Pages are never cached, so a new version shows straight away.
            Assert.True(page.Headers.CacheControl?.NoCache);
            Assert.True(home.Headers.CacheControl?.NoCache);
        }
        finally
        {
            webRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task BehindAProxyEachVisitorHasTheirOwnRateLimit()
    {
        var client = WithSettings(("Quiz:TrustForwardedHeaders", "true"), ("Quiz:UploadsPerMinutePerIp", "1"))
            .CreateClient();

        var first = await UploadFrom(client, "203.0.113.1");
        var again = await UploadFrom(client, "203.0.113.1");
        // A visitor can't escape their limit by sending a made-up address, because the proxy adds the real one last.
        var faked = await UploadFrom(client, "198.51.100.9, 203.0.113.1");
        var someoneElse = await UploadFrom(client, "203.0.113.2");

        Assert.Equal(HttpStatusCode.Created, first);
        Assert.Equal(HttpStatusCode.TooManyRequests, again);
        Assert.Equal(HttpStatusCode.TooManyRequests, faked);
        Assert.Equal(HttpStatusCode.Created, someoneElse);
    }

    [Fact]
    public async Task EveryResponseHasSecurityHeaders()
    {
        var client = factory.CreateClient();

        foreach (var path in new[] { "/api/health", "/api/nothing-here" })
        {
            var response = await client.GetAsync(path);

            Assert.Contains("script-src 'self'", response.Headers.GetValues("Content-Security-Policy").Single());
            Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        }
    }

    [Theory]
    [InlineData("https://evil.example", HttpStatusCode.Forbidden)]
    [InlineData("http://localhost", HttpStatusCode.OK)]
    public async Task GameConnectionsOnlyComeFromThisSite(string origin, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rooms/hub/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", origin);

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
    }

    private WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    private static async Task<HttpStatusCode> UploadFrom(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chats") { Content = TestChats.Content(TestChats.QuizzableUpload()) };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await client.SendAsync(request)).StatusCode;
    }

    private sealed record Health(string Status, string Version);
}

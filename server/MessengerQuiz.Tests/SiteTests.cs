using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MessengerQuiz.Tests;

// Running as a hosted site: the health check, demo-only mode, serving the client and visitors' addresses.
public class SiteTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task HealthCheckIsOk()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UploadsAreAllowedByDefault()
    {
        var config = await factory.CreateClient().GetFromJsonAsync<Config>("/api/config");

        Assert.False(config!.DemoOnly);
    }

    [Fact]
    public async Task DemoOnlySiteRefusesUploadsButPlaysTheDemo()
    {
        var client = WithSettings(("Quiz:DemoOnly", "true")).CreateClient();

        var config = await client.GetFromJsonAsync<Config>("/api/config");
        var upload = await client.PostAsync("/api/chats", new MultipartFormDataContent());
        var demo = await client.PostAsync("/api/chats/demo", null);

        Assert.True(config!.DemoOnly);
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Created, demo.StatusCode);
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

        var first = await StartDemoFrom(client, "203.0.113.1");
        var again = await StartDemoFrom(client, "203.0.113.1");
        // A visitor can't escape their limit by sending a made-up address, because the proxy adds the real one last.
        var faked = await StartDemoFrom(client, "198.51.100.9, 203.0.113.1");
        var someoneElse = await StartDemoFrom(client, "203.0.113.2");

        Assert.Equal(HttpStatusCode.Created, first);
        Assert.Equal(HttpStatusCode.TooManyRequests, again);
        Assert.Equal(HttpStatusCode.TooManyRequests, faked);
        Assert.Equal(HttpStatusCode.Created, someoneElse);
    }

    private WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    private static async Task<HttpStatusCode> StartDemoFrom(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chats/demo");
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await client.SendAsync(request)).StatusCode;
    }

    private sealed record Config(bool DemoOnly);
}

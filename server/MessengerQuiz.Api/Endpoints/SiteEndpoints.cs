namespace MessengerQuiz.Api.Endpoints;

// Endpoints for running the app as a hosted site: a health check and the built client.
public static class SiteEndpoints
{
    // Built scripts and styles have a hash in their names, so they can be cached for good. Other files, such as
    // index.html, are checked each time, so a new version shows straight away and never asks for deleted scripts.
    public static StaticFileOptions ClientFiles { get; } = new()
    {
        OnPrepareResponse = file => file.Context.Response.Headers.CacheControl =
            file.Context.Request.Path.StartsWithSegments("/assets") ? "public, max-age=31536000, immutable" : "no-cache",
    };

    public static void MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        // Used by the deploy pipeline to check the new version is up. APP_VERSION is the commit it was built from.
        app.MapGet("/api/health", (IConfiguration config) =>
            Results.Ok(new { status = "ok", version = config["APP_VERSION"] ?? "dev" }));

        // Unknown API paths are errors, not pages.
        app.MapFallback("/api/{**rest}", () => Results.NotFound());

        // Any other path is a page of the client, such as a game link, so the client's router handles it.
        // In development the client runs on Vite's own server and there's no wwwroot, so this returns 404.
        app.MapFallbackToFile("index.html", ClientFiles);
    }
}

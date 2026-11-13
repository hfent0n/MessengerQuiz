namespace MessengerQuiz.Api.Endpoints;

// Browser protections sent with every response.
public static class SecurityHeaders
{
    // Only this site's own files and connections. An injected script couldn't run, load anything from elsewhere or
    // send data anywhere.
    // - style-src-attr allows React's style attributes, used for score bars and timers. Stylesheets stay same-site.
    // - blob: is for photos and videos shown from the player's own zips, and for the zip library's workers.
    // - frame-ancestors stops other sites showing the app in a frame, which could trick players into clicking.
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; style-src-attr 'unsafe-inline'; " +
        "img-src 'self' blob: data:; media-src 'self' blob:; worker-src 'self' blob:; connect-src 'self'; " +
        "object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        // Game links hold the room code, so they're never passed on to other sites.
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        return next(context);
    });

    // Browsers send Origin with WebSocket and cross-site requests. Game connections from another site are refused,
    // even though they'd also need a player's token. Requests without Origin, such as from tests, aren't from a page
    // on another site.
    public static IApplicationBuilder UseSameOriginFor(this IApplicationBuilder app, PathString path) =>
        app.Use((context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(path)
                && context.Request.Headers.Origin is { Count: > 0 } origin
                && !IsSameHost(origin.ToString(), context.Request.Host))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            return next(context);
        });

    private static bool IsSameHost(string origin, HostString host) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && string.Equals(uri.Authority, host.Value, StringComparison.OrdinalIgnoreCase);
}

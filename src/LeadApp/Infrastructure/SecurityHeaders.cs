namespace LeadApp.Infrastructure;

public static class SecurityHeaders
{
    /// <summary>
    /// Adds CSP and related headers. All scripts are served from this origin (no CDN), so script-src is 'self' only.
    /// Inline styles are allowed because Blazor and the SVG illustrations set style attributes.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Development needs ws: for hot reload / browser refresh.
        var connectSrc = env.IsDevelopment() ? "'self' ws: wss:" : "'self' wss:";
        var csp = string.Join("; ",
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
            "font-src 'self' https://fonts.gstatic.com",
            "img-src 'self' data:",
            $"connect-src {connectSrc}",
            "form-action 'self'",
            "frame-ancestors 'none'",
            "base-uri 'self'",
            "object-src 'none'");

        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = csp;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            await next();
        });
    }
}

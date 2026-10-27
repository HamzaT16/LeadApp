using LeadApp.Services;
using LeadApp.Services.Auth;
using LeadApp.Services.Export;
using LeadApp.Services.Leads;
using LeadApp.Services.Storage;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LeadApp.Infrastructure;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/logout", async (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest();

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/admin/login");
        }).DisableAntiforgery(); // validated explicitly above

        app.MapGet("/admin/leads/export", async (
            HttpContext context,
            ILeadRepository leads,
            LeadExcelExporter exporter,
            AppClock clock,
            CancellationToken ct) =>
        {
            var filter = LeadFilter.FromQuery(context.Request.Query);
            var data = filter.Apply(await leads.GetAllAsync(ct), clock);
            var bytes = exporter.Export(data);
            var fileName = $"Leadovi_Partner_MKF_{clock.Today:yyyy-MM-dd}.xlsx";
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }).RequireAuthorization(AdminRoles.Policy);

        return app;
    }
}

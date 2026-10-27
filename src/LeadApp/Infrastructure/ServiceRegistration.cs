using System.Threading.RateLimiting;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using LeadApp.Domain;
using LeadApp.Options;
using LeadApp.Services;
using LeadApp.Services.Auth;
using LeadApp.Services.Campaigns;
using LeadApp.Services.Crm;
using LeadApp.Services.Export;
using LeadApp.Services.Leads;
using LeadApp.Services.Locations;
using LeadApp.Services.Storage;
using LeadApp.Services.Ui;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace LeadApp.Infrastructure;

public static class ServiceRegistration
{
    public const string LoginRateLimitPolicy = "login";

    public static IServiceCollection AddLeadApp(this IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
    {
        services.AddOptions<CrmOptions>().Bind(config.GetSection(CrmOptions.Section))
            .Validate(o => SourceCodes.IsValid(o.DefaultSourceCode), "Crm:DefaultSourceCode must be one of the spec sourceCodes.")
            .Validate(o => o.UseFake || Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _), "Crm:BaseUrl must be an absolute URL.")
            .ValidateOnStart();
        services.AddOptions<FirebaseOptions>().Bind(config.GetSection(FirebaseOptions.Section));
        services.AddOptions<AdminSeedOptions>().Bind(config.GetSection(AdminSeedOptions.Section));
        services.AddOptions<LeadFormOptions>().Bind(config.GetSection(LeadFormOptions.Section))
            .Validate(o => o.MinAmount > 0 && o.MaxAmount > o.MinAmount && o.Step > 0, "Invalid LeadForm amount limits.")
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AppClock>();
        services.AddSingleton<ILocationService>(_ => LocationService.FromFiles(
            Path.Combine(env.ContentRootPath, "Data", "opcina.csv"),
            Path.Combine(env.ContentRootPath, "Data", "mjesna_zajednica.csv")));

        services.AddStorage(config, env);
        services.AddCrm(config);

        services.AddSingleton<SubmissionRateLimiter>();
        services.AddSingleton<LeadFormValidator>();
        services.AddSingleton<LeadExcelExporter>();
        services.AddScoped<LeadSubmissionService>();
        services.AddScoped<CampaignService>();
        services.AddScoped<ClientInfo>();
        services.AddScoped<CircuitHandler, ClientInfoCircuitHandler>();
        services.AddScoped<ToastService>();

        services.AddAdminAuth(env);
        return services;
    }

    private static void AddStorage(this IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
    {
        var firebase = config.GetSection(FirebaseOptions.Section).Get<FirebaseOptions>() ?? new FirebaseOptions();

        if (!firebase.HasCredentials)
        {
            if (!env.IsDevelopment())
                throw new InvalidOperationException("Firebase credentials are not configured (Firebase:CredentialsPath or Firebase:CredentialsJson).");

            // Local development without a service account: keep everything in memory.
            services.AddSingleton<ILeadRepository, InMemoryLeadRepository>();
            services.AddSingleton<ICampaignRepository, InMemoryCampaignRepository>();
            services.AddSingleton<IAdminUserRepository, InMemoryAdminUserRepository>();
            return;
        }

        services.AddSingleton(_ =>
        {
            // Only service-account credentials are accepted (explicit type, per Google's guidance).
            var json = string.IsNullOrWhiteSpace(firebase.CredentialsJson)
                ? File.ReadAllText(firebase.CredentialsPath!)
                : firebase.CredentialsJson;
            return new FirestoreDbBuilder
            {
                ProjectId = firebase.ProjectId,
                GoogleCredential = CredentialFactory.FromJson<ServiceAccountCredential>(json).ToGoogleCredential()
            }.Build();
        });
        services.AddSingleton<ILeadRepository, FirestoreLeadRepository>();
        services.AddSingleton<ICampaignRepository, FirestoreCampaignRepository>();
        services.AddSingleton<IAdminUserRepository, FirestoreAdminUserRepository>();
    }

    private static void AddCrm(this IServiceCollection services, IConfiguration config)
    {
        if (config.GetValue<bool>($"{CrmOptions.Section}:{nameof(CrmOptions.UseFake)}"))
        {
            services.AddSingleton<ICrmClient, FakeCrmClient>();
            return;
        }

        services.AddHttpClient<ICrmClient, CrmClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<CrmOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        });
    }

    private static void AddAdminAuth(this IServiceCollection services, IWebHostEnvironment env)
    {
        services.AddSingleton<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
        services.AddScoped<AdminAuthService>();
        services.AddHostedService<AdminSeeder>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = ".LeadApp.Admin";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.SecurePolicy = env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
                o.SlidingExpiration = true;
                o.LoginPath = "/admin/login";
                o.LogoutPath = "/admin/logout";
                o.AccessDeniedPath = "/admin/login";
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminRoles.Policy, p => p.RequireAuthenticatedUser().RequireRole(AdminRoles.Admin));
        services.AddCascadingAuthenticationState();

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(LoginRateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(5) }));
        });
    }
}

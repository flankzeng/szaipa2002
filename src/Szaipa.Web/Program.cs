using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.IO.Compression;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Szaipa.Data.Abstractions;
using Szaipa.Data.Configuration;
using Szaipa.Data.DependencyInjection;
using Szaipa.Web.Authorization;
using Szaipa.Web.Configuration;
using Szaipa.Web.Infrastructure;
using Szaipa.Web.Services;
using Szaipa.Web.Services.Admin;

var contentRoot = ResolveContentRoot();
// Standard ASP.NET Core layering: appsettings.json (committed defaults) -> appsettings.{Environment}.json
// (e.g. Development on the Mac dev box, Production on the Windows server) -> appsettings.Local.json
// (gitignored, publish-excluded, machine-specific secrets: read-only DB conn + legacy asset path) -> env vars.
// Defaults to Production when ASPNETCORE_ENVIRONMENT is unset (the IIS deployment case).
var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
Console.WriteLine($"Szaipa.Web boot: loading configuration from {contentRoot} (environment={environmentName})");
var configuration = new ConfigurationBuilder()
    .SetBasePath(contentRoot)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var urls = ResolveUrls(args, configuration);

Console.WriteLine($"Szaipa.Web boot: configured contentRoot={contentRoot}, urls={urls}");

var hostBuilder = new HostBuilder()
    .ConfigureHostConfiguration(config =>
    {
        config.AddConfiguration(configuration);
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
    })
    .ConfigureWebHost(webBuilder =>
    {
        webBuilder
            .UseKestrel()
            .UseContentRoot(contentRoot)
            .UseWebRoot(Path.Combine(contentRoot, "wwwroot"))
            .UseConfiguration(configuration)
            .UseUrls(urls)
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                // Razor HTML-encodes dynamic (@expr) values; ASP.NET Core's default HtmlEncoder emits all
                // non-ASCII (incl. CJK) as numeric character references (&#xXXXX;). The legacy MVC5 site output
                // raw UTF-8 Chinese, so DB-sourced Chinese text rendered ~30% larger and non-byte-faithful here.
                // Allow the full Unicode range so dynamic Chinese is emitted raw, matching the legacy output.
                services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(UnicodeRanges.All));
                services
                    .AddOptions<DatabaseSafetyOptions>()
                    .Bind(configuration.GetSection(DatabaseSafetyOptions.SectionName));
                services
                    .AddOptions<ReadOnlyMigrationOptions>()
                    .Bind(configuration.GetSection(ReadOnlyMigrationOptions.SectionName));
                services
                    .AddOptions<LegacyAssetsOptions>()
                    .Bind(configuration.GetSection(LegacyAssetsOptions.SectionName));
                services
                    .AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(contentRoot, "App_Data", "DataProtection-Keys")));
                services.AddSzaipaData(configuration);
                services.AddSingleton<IMigrationWorkspaceDiagnosticsService, MigrationWorkspaceDiagnosticsService>();
                services.AddSingleton<ILegacyImagePreviewResolver, LegacyImagePreviewResolver>();
                services.AddSingleton<IAdminAssetStorage, AdminAssetStorage>();
                services.AddSingleton<IExhibitionGalleryStorage, ExhibitionGalleryStorage>();

                // Cookie authentication for the staff/admin backend (Areas/Staff), replacing the legacy
                // Session["Staff"] check. Tickets are protected by the DataProtection keys configured above.
                services
                    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(options =>
                    {
                        options.LoginPath = "/Staff/Account/Login";
                        options.LogoutPath = "/Staff/Account/Logout";
                        options.AccessDeniedPath = "/Staff/Account/Login";
                        options.ExpireTimeSpan = TimeSpan.FromHours(8);
                        options.SlidingExpiration = true;
                        options.Cookie.Name = "Szaipa.Admin";
                        options.Cookie.HttpOnly = true;
                        options.Cookie.SameSite = SameSiteMode.Lax;
                    });
                services.AddAuthorization(options =>
                {
                    // Every authenticated cookie holder is a signed-in staff member; the policy gates the
                    // whole admin area via [Authorize(Policy = AdminAuthorization.StaffPolicy)].
                    options.AddPolicy(
                        AdminAuthorization.StaffPolicy,
                        policy => policy.RequireAuthenticatedUser());
                });

                services.AddControllersWithViews();
                services.AddResponseCompression(options =>
                {
                    options.EnableForHttps = true;
                    options.Providers.Add<BrotliCompressionProvider>();
                    options.Providers.Add<GzipCompressionProvider>();
                    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                    {
                        "image/svg+xml",
                        "application/manifest+json"
                    });
                });
                services.Configure<BrotliCompressionProviderOptions>(options =>
                    options.Level = CompressionLevel.Fastest);
                services.Configure<GzipCompressionProviderOptions>(options =>
                    options.Level = CompressionLevel.Fastest);
            })
            .Configure(app =>
            {
                var environment = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
                var databaseSafety = app.ApplicationServices
                    .GetRequiredService<IOptions<DatabaseSafetyOptions>>()
                    .Value;
                var readOnlyMigration = app.ApplicationServices
                    .GetRequiredService<IOptions<ReadOnlyMigrationOptions>>()
                    .Value;
                var legacyConnectionPolicy = app.ApplicationServices.GetRequiredService<ILegacyConnectionPolicy>();
                var legacyScaffoldCommandService = app.ApplicationServices.GetRequiredService<ILegacyScaffoldCommandService>();
                var workspaceDiagnostics = app.ApplicationServices.GetRequiredService<IMigrationWorkspaceDiagnosticsService>()
                    .GetDiagnostics();

                Console.WriteLine(
                    $"Szaipa.Web boot: contentRoot={environment.ContentRootPath}, urls={urls}");
                Console.WriteLine(
                    $"Szaipa.Web boot: legacyDataSources={databaseSafety.UseLegacyDataSources}, liveDatabase={databaseSafety.AllowLiveDatabase}");
                Console.WriteLine(
                    $"Szaipa.Web boot: configuredLegacySources={legacyConnectionPolicy.GetConfiguredSources().Count}");
                Console.WriteLine(
                    $"Szaipa.Web boot: scaffoldSuggestions={legacyScaffoldCommandService.GetSuggestions().Count}");
                Console.WriteLine(
                    $"Szaipa.Web boot: readModelFlags=szaipa:{readOnlyMigration.EnableSzaipaReadModels},tongou:{readOnlyMigration.EnableTongouReadModels}");

                var configuredLegacySources = legacyConnectionPolicy.GetConfiguredSources();
                var hasReadWriteLegacySource = workspaceDiagnostics.HasReadWriteLegacySource;

                if (databaseSafety.UseLegacyDataSources && hasReadWriteLegacySource && !databaseSafety.AllowLiveDatabase)
                {
                    throw new InvalidOperationException(
                        "A legacy data source is configured for ReadWrite access, but RuntimeSafety:AllowLiveDatabase is false. " +
                        "This guard prevents accidental writes to the existing Windows-connected database.");
                }

                if (!environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/Home/Error");
                    app.UseHsts();
                }

                if (databaseSafety.UseHttpsRedirection)
                {
                    app.UseHttpsRedirection();
                }

                app.UseResponseCompression();

                var webRoot = Path.Combine(contentRoot, "wwwroot");
                if (Directory.Exists(webRoot))
                {
                    app.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider(webRoot),
                        OnPrepareResponse = context =>
                        {
                            var request = context.Context.Request;
                            var hasContentVersion = request.Query.TryGetValue("v", out var version)
                                && !string.IsNullOrWhiteSpace(version.ToString());
                            context.Context.Response.Headers.CacheControl = StaticAssetCachePolicy.Select(
                                environment.IsDevelopment(),
                                StaticAssetSource.WebRoot,
                                request.Path.Value,
                                hasContentVersion);
                        }
                    });
                }

                // Serve the legacy static assets (news/exhibition/artist imagery, fonts, model CSS/JS) from the
                // configured publish baseline (e.g. web24.05/Content) without copying them into the repo. The
                // request path /Content matches the legacy view asset references; matching is case-insensitive,
                // so /content also resolves.
                var legacyAssetsRoot = app.ApplicationServices
                    .GetRequiredService<IOptions<LegacyAssetsOptions>>()
                    .Value.ContentRoot;
                if (!string.IsNullOrWhiteSpace(legacyAssetsRoot) && Directory.Exists(legacyAssetsRoot))
                {
                    app.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider(legacyAssetsRoot),
                        RequestPath = "/Content",
                        OnPrepareResponse = context =>
                        {
                            context.Context.Response.Headers.CacheControl = StaticAssetCachePolicy.Select(
                                environment.IsDevelopment(),
                                StaticAssetSource.LegacyContent,
                                context.Context.Request.Path.Value);
                        }
                    });
                    Console.WriteLine($"Szaipa.Web boot: serving legacy assets from {legacyAssetsRoot} at /Content");
                }
                else
                {
                    Console.WriteLine(
                        "Szaipa.Web boot: legacy assets not served (set LegacyAssets:ContentRoot to the web24.05/Content path)");
                }

                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/healthz", async context =>
                    {
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            status = "ok",
                            framework = "net10.0",
                            legacyDataSources = databaseSafety.UseLegacyDataSources,
                            liveDatabase = databaseSafety.AllowLiveDatabase,
                            hasReadWriteLegacySource,
                            configuredSources = workspaceDiagnostics.LegacySources.ToDictionary(
                                item => item.Key,
                                item => item.Value.AccessMode),
                            connectionSources = workspaceDiagnostics.LegacySources.ToDictionary(
                                item => item.Key,
                                item => new
                                {
                                    source = item.Value.ConnectionStringSource,
                                    hasConnectionString = item.Value.HasConnectionString
                                }),
                            readModelFlags = workspaceDiagnostics.ReadModelFlags,
                            scaffoldReadiness = workspaceDiagnostics.LegacySources.ToDictionary(
                                item => item.Key,
                                item => item.Value.ScaffoldReady)
                        }));
                    });

                    endpoints.MapControllerRoute(
                        name: "areas",
                        pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

                    endpoints.MapControllerRoute(
                        name: "default",
                        pattern: "{controller=Home}/{action=Index}/{id?}");
                });
            });
    });

Console.WriteLine("Szaipa.Web boot: building host");
var host = hostBuilder.Build();
Console.WriteLine("Szaipa.Web boot: running host");
host.Run();

static string ResolveContentRoot()
{
    var current = Directory.GetCurrentDirectory();
    var projectFromRepoRoot = Path.Combine(current, "src", "Szaipa.Web");
    return File.Exists(Path.Combine(projectFromRepoRoot, "appsettings.json"))
        ? projectFromRepoRoot
        : current;
}

static string ResolveUrls(string[] args, IConfiguration configuration)
{
    for (var index = 0; index < args.Length - 1; index++)
    {
        if (args[index] == "--urls")
        {
            return args[index + 1];
        }
    }

    return configuration["urls"]
        ?? configuration["ASPNETCORE_URLS"]
        ?? "http://127.0.0.1:5057";
}

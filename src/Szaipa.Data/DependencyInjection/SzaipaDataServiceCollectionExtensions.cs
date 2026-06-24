using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Szaipa.Data.Abstractions;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Contexts.TongouAdmin;
using Szaipa.Data.Contracts.Home;
using Szaipa.Data.Contracts.Tongou;
using Szaipa.Data.Services;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.Home;
using Szaipa.Data.Services.Tongou;

namespace Szaipa.Data.DependencyInjection;

public static class SzaipaDataServiceCollectionExtensions
{
    private const string SzaipaReadOnlyConnectionEnvVar = "SZAIPA_READONLY_CONNECTION";
    private const string TongouReadOnlyConnectionEnvVar = "TONGOU_READONLY_CONNECTION";
    private const string SzaipaAdminConnectionEnvVar = "SZAIPA_ADMIN_CONNECTION";
    private const string TongouAdminConnectionEnvVar = "TONGOU_ADMIN_CONNECTION";

    public static IServiceCollection AddSzaipaData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<LegacyDataOptions>()
            .Bind(configuration.GetSection(LegacyDataOptions.SectionName))
            .PostConfigure(options =>
            {
                ApplyConnectionStringResolution(
                    options.Szaipa,
                    configurationValue: configuration["ConnectionStrings:Szaipa"],
                    environmentValue: configuration[SzaipaReadOnlyConnectionEnvVar]);

                ApplyConnectionStringResolution(
                    options.Tongou,
                    configurationValue: configuration["ConnectionStrings:Tongou"],
                    environmentValue: configuration[TongouReadOnlyConnectionEnvVar]);
            });

        // Admin (write) path. Gated and machine-local: writes are off unless AdminWrite:EnableWrites is true
        // and a dedicated connection string is supplied. The connection is resolved ONLY from admin-specific
        // keys (ConnectionStrings:SzaipaAdmin / env SZAIPA_ADMIN_CONNECTION) and never falls back to the
        // read-only Szaipa connection, so the admin path can never accidentally reuse the production source.
        services
            .AddOptions<AdminWriteOptions>()
            .Bind(configuration.GetSection(AdminWriteOptions.SectionName))
            .PostConfigure(options =>
            {
                ResolveAdminConnectionString(
                    options,
                    configurationValue: configuration["ConnectionStrings:SzaipaAdmin"],
                    environmentValue: configuration[SzaipaAdminConnectionEnvVar]);
            });

        // Tongou(同构) admin (write) path. Same gating posture as the Szaipa admin path above, but for the
        // physically separate Tongou database — resolved ONLY from ConnectionStrings:TongouAdmin / env
        // TONGOU_ADMIN_CONNECTION, never falling back to the read-only Tongou connection.
        services
            .AddOptions<TongouAdminWriteOptions>()
            .Bind(configuration.GetSection(TongouAdminWriteOptions.SectionName))
            .PostConfigure(options =>
            {
                ResolveTongouAdminConnectionString(
                    options,
                    configurationValue: configuration["ConnectionStrings:TongouAdmin"],
                    environmentValue: configuration[TongouAdminConnectionEnvVar]);
            });

        services.AddSingleton<ILegacyConnectionPolicy, LegacyConnectionPolicy>();
        services.AddSingleton<ILegacyScaffoldCommandService, LegacyScaffoldCommandService>();

        // Read-only context over the legacy Szaipa database. The connection is resolved lazily through the
        // connection policy, so registration never connects: the policy throws only if the Szaipa source is
        // disabled or has no connection string, and only when the context is actually constructed (i.e. when a
        // News route is activated). Default startup keeps the source Disabled and never builds this context.
        services.AddDbContext<SzaipaLegacyReadContext>((serviceProvider, options) =>
        {
            var connectionPolicy = serviceProvider.GetRequiredService<ILegacyConnectionPolicy>();
            options.UseSqlServer(connectionPolicy.GetConnectionString(LegacyDataSource.Szaipa));
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        // Read-only context over the legacy Tongou database, resolved lazily through the same policy. Default
        // startup keeps the Tongou source Disabled and never builds this context.
        services.AddDbContext<TongouLegacyReadContext>((serviceProvider, options) =>
        {
            var connectionPolicy = serviceProvider.GetRequiredService<ILegacyConnectionPolicy>();
            options.UseSqlServer(connectionPolicy.GetConnectionString(LegacyDataSource.Tongou));
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        // Write-capable admin context over a LOCAL writable copy of the Szaipa database. Only constructed when
        // the admin write path is configured; if an admin feature is reached without configuration it throws a
        // clear, actionable error. Admin controllers resolve it lazily, so the public read-only site boots and
        // runs without any admin/write configuration. Default QueryTrackingBehavior (tracking) is intentional:
        // this context performs edits, unlike the read contexts which force NoTracking.
        services.AddDbContext<SzaipaAdminContext>((serviceProvider, options) =>
        {
            var adminOptions = serviceProvider.GetRequiredService<IOptions<AdminWriteOptions>>().Value;
            if (!adminOptions.IsConfigured)
            {
                throw new InvalidOperationException(
                    "SzaipaAdminContext was requested but the admin write path is not configured. Set "
                    + "AdminWrite:EnableWrites=true and ConnectionStrings:SzaipaAdmin (or env "
                    + "SZAIPA_ADMIN_CONNECTION) to a LOCAL writable copy of the Szaipa database. Never point "
                    + "this at the production / Windows-connected database.");
            }

            options.UseSqlServer(adminOptions.ConnectionString);
        });

        // Write-capable admin context over a LOCAL writable copy of the Tongou database. Same gating posture
        // as SzaipaAdminContext above, but resolved from TongouAdminWriteOptions/ConnectionStrings:TongouAdmin.
        services.AddDbContext<TongouAdminContext>((serviceProvider, options) =>
        {
            var tongouAdminOptions = serviceProvider.GetRequiredService<IOptions<TongouAdminWriteOptions>>().Value;
            if (!tongouAdminOptions.IsConfigured)
            {
                throw new InvalidOperationException(
                    "TongouAdminContext was requested but the Tongou admin write path is not configured. Set "
                    + "TongouAdminWrite:EnableWrites=true and ConnectionStrings:TongouAdmin (or env "
                    + "TONGOU_ADMIN_CONNECTION) to a LOCAL writable copy of the Tongou database. Never point "
                    + "this at the production / Windows-connected database.");
            }

            options.UseSqlServer(tongouAdminOptions.ConnectionString);
        });

        services.AddScoped<INewsReadRepository, NewsReadRepository>();
        services.AddScoped<IArtistReadRepository, ArtistReadRepository>();
        services.AddScoped<IPublicationReadRepository, PublicationReadRepository>();
        services.AddScoped<ITongouReadRepository, TongouReadRepository>();

        // Admin (write) services. Scoped alongside SzaipaAdminContext; only resolved on admin routes.
        services.AddScoped<IOperationRecorder, OperationRecorder>();
        services.AddScoped<INewsAdminRepository, NewsAdminRepository>();
        services.AddScoped<IArtNewsAdminRepository, ArtNewsAdminRepository>();
        services.AddScoped<IArtistAdminRepository, ArtistAdminRepository>();
        services.AddScoped<ICompanyAdminRepository, CompanyAdminRepository>();
        services.AddScoped<FavAdminRepository>();
        services.AddScoped<AuctionAdminRepository>();
        services.AddScoped<ExhibitionAdminRepository>();
        services.AddScoped<WorksAdminRepository>();
        services.AddScoped<IPublicationAdminRepository, PublicationAdminRepository>();
        services.AddScoped<ITongouAtristAdminRepository, TongouAtristAdminRepository>();
        services.AddScoped<ITongouWorksAdminRepository, TongouWorksAdminRepository>();
        services.AddScoped<IDashboardAnalyticsRepository, DashboardAnalyticsRepository>();
        return services;
    }

    private static void ResolveAdminConnectionString(
        AdminWriteOptions options,
        string? configurationValue,
        string? environmentValue)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            options.ConnectionStringSource = "AdminWrite";
            return;
        }

        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            options.ConnectionString = environmentValue;
            options.ConnectionStringSource = "Environment";
            return;
        }

        if (!string.IsNullOrWhiteSpace(configurationValue))
        {
            options.ConnectionString = configurationValue;
            options.ConnectionStringSource = "ConnectionStrings";
            return;
        }

        options.ConnectionString = string.Empty;
        options.ConnectionStringSource = "None";
    }

    private static void ResolveTongouAdminConnectionString(
        TongouAdminWriteOptions options,
        string? configurationValue,
        string? environmentValue)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            options.ConnectionStringSource = "TongouAdminWrite";
            return;
        }

        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            options.ConnectionString = environmentValue;
            options.ConnectionStringSource = "Environment";
            return;
        }

        if (!string.IsNullOrWhiteSpace(configurationValue))
        {
            options.ConnectionString = configurationValue;
            options.ConnectionStringSource = "ConnectionStrings";
            return;
        }

        options.ConnectionString = string.Empty;
        options.ConnectionStringSource = "None";
    }

    private static void ApplyConnectionStringResolution(
        LegacyDatabaseOptions options,
        string? configurationValue,
        string? environmentValue)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            options.ConnectionStringSource = "LegacyData";
            return;
        }

        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            options.ConnectionString = environmentValue;
            options.ConnectionStringSource = "Environment";
            return;
        }

        if (!string.IsNullOrWhiteSpace(configurationValue))
        {
            options.ConnectionString = configurationValue;
            options.ConnectionStringSource = "ConnectionStrings";
            return;
        }

        options.ConnectionString = string.Empty;
        options.ConnectionStringSource = "None";
    }
}

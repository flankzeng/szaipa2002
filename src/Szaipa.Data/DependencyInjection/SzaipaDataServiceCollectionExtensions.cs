using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Szaipa.Data.Abstractions;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Contracts.Home;
using Szaipa.Data.Contracts.Tongou;
using Szaipa.Data.Services;
using Szaipa.Data.Services.Home;
using Szaipa.Data.Services.Tongou;

namespace Szaipa.Data.DependencyInjection;

public static class SzaipaDataServiceCollectionExtensions
{
    private const string SzaipaReadOnlyConnectionEnvVar = "SZAIPA_READONLY_CONNECTION";
    private const string TongouReadOnlyConnectionEnvVar = "TONGOU_READONLY_CONNECTION";

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

        services.AddScoped<INewsReadRepository, NewsReadRepository>();
        services.AddScoped<IArtistReadRepository, ArtistReadRepository>();
        services.AddScoped<IPublicationReadRepository, PublicationReadRepository>();
        services.AddScoped<ITongouReadRepository, TongouReadRepository>();
        return services;
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

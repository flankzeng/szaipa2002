using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Szaipa.Data.Abstractions;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Contexts.Tongou;
using Szaipa.Data.Contexts.TongouAdmin;
using Szaipa.Data.Contracts.Home;
using Szaipa.Data.Contracts.Tongou;
using Szaipa.Data.LocalMirror;
using Szaipa.Data.Services;
using Szaipa.Data.Services.IpGeolocation;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.Home;
using Szaipa.Data.Services.Tongou;
using Szaipa.Data.Services.WeChat;

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
        // and a connection string is supplied. The connection resolves from admin-specific keys
        // (ConnectionStrings:SzaipaAdmin / env SZAIPA_ADMIN_CONNECTION). As an explicit local-debug opt-in
        // (UseReadOnlyConnectionForDebug), and only when no admin connection is set, it falls back to the
        // existing read-only Szaipa connection — a db_datareader login, so the context can be browsed/logged
        // into but cannot write (writes fail at the SQL level). Default config never opts in.
        services
            .AddOptions<AdminWriteOptions>()
            .Bind(configuration.GetSection(AdminWriteOptions.SectionName))
            .PostConfigure(options =>
            {
                ResolveAdminConnectionString(
                    options,
                    configurationValue: configuration["ConnectionStrings:SzaipaAdmin"],
                    environmentValue: configuration[SzaipaAdminConnectionEnvVar]);

                if (string.IsNullOrWhiteSpace(options.ConnectionString)
                    && options.EnableWrites
                    && options.UseReadOnlyConnectionForDebug)
                {
                    var readOnly = ResolveReadOnlyConnection(
                        configuration,
                        connectionStringsKey: "ConnectionStrings:Szaipa",
                        legacyDataKey: "LegacyData:Szaipa:ConnectionString",
                        environmentKey: SzaipaReadOnlyConnectionEnvVar);
                    if (!string.IsNullOrWhiteSpace(readOnly))
                    {
                        options.ConnectionString = readOnly;
                        options.ConnectionStringSource = "ReadOnlyDebug(Szaipa)";
                    }
                }
            });

        // Tongou(同构) admin (write) path. Same gating posture as the Szaipa admin path above, including the
        // UseReadOnlyConnectionForDebug opt-in that reuses the read-only Tongou connection for local inspection.
        services
            .AddOptions<TongouAdminWriteOptions>()
            .Bind(configuration.GetSection(TongouAdminWriteOptions.SectionName))
            .PostConfigure(options =>
            {
                ResolveTongouAdminConnectionString(
                    options,
                    configurationValue: configuration["ConnectionStrings:TongouAdmin"],
                    environmentValue: configuration[TongouAdminConnectionEnvVar]);

                if (string.IsNullOrWhiteSpace(options.ConnectionString)
                    && options.EnableWrites
                    && options.UseReadOnlyConnectionForDebug)
                {
                    var readOnly = ResolveReadOnlyConnection(
                        configuration,
                        connectionStringsKey: "ConnectionStrings:Tongou",
                        legacyDataKey: "LegacyData:Tongou:ConnectionString",
                        environmentKey: TongouReadOnlyConnectionEnvVar);
                    if (!string.IsNullOrWhiteSpace(readOnly))
                    {
                        options.ConnectionString = readOnly;
                        options.ConnectionStringSource = "ReadOnlyDebug(Tongou)";
                    }
                }
            });

        // LocalMirror（本地镜像库，见 docs/updates/2026-10-26-local-mirror-db.md）：启用时四个上下文都改连 SQLite
        // 镜像文件，并把上面三组选项改写成「已配置」。Production 环境下启用在这里直接抛异常（启动失败）。
        // 必须放在上面所有 PostConfigure 之后，下面四个 AddDbContext 之前。
        var localMirror = LocalMirrorOptions.FromConfiguration(configuration);
        localMirror.EnsureAllowed();
        services.AddSingleton(localMirror);
        LocalMirrorRegistration.ConfigureOptions(services, localMirror);

        services.AddSingleton<ILegacyConnectionPolicy, LegacyConnectionPolicy>();
        services.AddSingleton<ILegacyScaffoldCommandService, LegacyScaffoldCommandService>();

        // Read-only context over the legacy Szaipa database. The connection is resolved lazily through the
        // connection policy, so registration never connects: the policy throws only if the Szaipa source is
        // disabled or has no connection string, and only when the context is actually constructed (i.e. when a
        // News route is activated). Default startup keeps the source Disabled and never builds this context.
        services.AddDbContext<SzaipaLegacyReadContext>((serviceProvider, options) =>
        {
            var connectionPolicy = serviceProvider.GetRequiredService<ILegacyConnectionPolicy>();
            LocalMirrorRegistration.UseProvider(
                options,
                localMirror.Enabled,
                connectionPolicy.GetConnectionString(LegacyDataSource.Szaipa));
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        // Read-only context over the legacy Tongou database, resolved lazily through the same policy. Default
        // startup keeps the Tongou source Disabled and never builds this context.
        services.AddDbContext<TongouLegacyReadContext>((serviceProvider, options) =>
        {
            var connectionPolicy = serviceProvider.GetRequiredService<ILegacyConnectionPolicy>();
            LocalMirrorRegistration.UseProvider(
                options,
                localMirror.Enabled,
                connectionPolicy.GetConnectionString(LegacyDataSource.Tongou));
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

            LocalMirrorRegistration.UseProvider(options, localMirror.Enabled, adminOptions.ConnectionString);
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

            LocalMirrorRegistration.UseProvider(options, localMirror.Enabled, tongouAdminOptions.ConnectionString);
        });

        // qqwry 离线 IP 定位（懒加载），供 Dashboard 访客分布按 IP 现场解析省/市。路径解析见 QqwryIpLocator；
        // 宿主（Szaipa.Web Program.cs）用 contentRoot 覆盖注册更稳。文件缺失时 Lookup 返回 null，图表降级不报错。
        services.AddSingleton<IQqwryIpLocator>(new QqwryIpLocator(configuration["IpGeolocation:QqwryPath"]));

        services.AddScoped<INewsReadRepository, NewsReadRepository>();
        services.AddScoped<IArtistReadRepository, ArtistReadRepository>();
        services.AddScoped<IPublicationReadRepository, PublicationReadRepository>();
        services.AddScoped<ITongouReadRepository, TongouReadRepository>();
        services.AddSingleton<HomeBlocksSchemaState>();
        services.AddScoped<IHomeBlocksReadRepository, HomeBlocksReadRepository>();

        // Admin (write) services. Scoped alongside SzaipaAdminContext; only resolved on admin routes.
        services.AddScoped<IOperationRecorder, OperationRecorder>();
        services.AddScoped<INewsAdminRepository, NewsAdminRepository>();
        services.AddScoped<IArtNewsAdminRepository, ArtNewsAdminRepository>();
        services.AddScoped<IArtistAdminRepository, ArtistAdminRepository>();
        services.AddScoped<IArtistSiteAdminRepository, ArtistSiteAdminRepository>();
        services.AddScoped<ICompanyAdminRepository, CompanyAdminRepository>();
        services.AddScoped<FavAdminRepository>();
        services.AddScoped<AuctionAdminRepository>();
        services.AddScoped<ExhibitionAdminRepository>();
        services.AddScoped<ArtistBookAdminRepository>();
        // 现场编辑（T6）：文案读取为单例缓存（缺表即用模板原文），写入走后台可写上下文。
        services.AddSingleton<Szaipa.Data.Services.LiveEdit.ISiteTextProvider, Szaipa.Data.Services.LiveEdit.SiteTextProvider>();
        services.AddScoped<Szaipa.Data.Services.LiveEdit.LiveEditService>();
        services.AddScoped<IVipOrderAdminRepository, VipOrderAdminRepository>();
        services.AddScoped<WorksAdminRepository>();
        services.AddScoped<IPublicationAdminRepository, PublicationAdminRepository>();
        services.AddScoped<IExhibitionWorkAdminRepository, ExhibitionWorkAdminRepository>();
        services.AddScoped<IPublicationBlockAdminRepository, PublicationBlockAdminRepository>();
        services.AddScoped<IFeaturedExhibitionAdminRepository, FeaturedExhibitionAdminRepository>();
        services.AddScoped<ITongouAtristAdminRepository, TongouAtristAdminRepository>();
        services.AddScoped<ITongouWorksAdminRepository, TongouWorksAdminRepository>();
        services.AddScoped<IDashboardAnalyticsRepository, DashboardAnalyticsRepository>();
        // 微信公众号文章同步（T1）。HTTP 一律经 IHttpClientFactory 命名客户端，测试里可替换 handler；
        // IWeChatImageStore（把图片写进 ContentRoot）由宿主 Szaipa.Web 注册。定时同步仅在
        // WeChat:Enabled + 凭据齐全 + AdminWrite 已配置时才会真正工作，否则启动即静默退出。
        // 后台「公众号接口配置」页（T9）：加密存储文件覆盖配置段绑定值，保存后无需重启即生效。
        services.AddWeChatEffectiveOptions(configuration, serviceProvider =>
        {
            var environment = serviceProvider.GetService<IHostEnvironment>();
            return environment is null
                ? WeChatCredentialStoreLocation.Unavailable("宿主环境不可用，无法确定存储位置。")
                : WeChatCredentialStoreLocation.Resolve(
                    environment.ContentRootPath,
                    configuration[WeChatCredentialStoreLocation.ConfigurationKey],
                    environment.IsDevelopment());
        });
        services.AddHttpClient(WeChatApiClient.ClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient(WeChatSyncService.PageClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient(WeChatImageLocalizer.ClientName, client => client.Timeout = TimeSpan.FromSeconds(60))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<IWeChatApiClient, WeChatApiClient>();
        services.AddScoped<WeChatImageLocalizer>();
        services.AddScoped<IWeChatSyncService, WeChatSyncService>();
        services.AddScoped<IWeChatSyncAdminRepository, WeChatSyncAdminRepository>();
        services.AddSingleton<IWeChatSourceStatusStore, WeChatSourceStatusStore>();
        services.AddSingleton<WeChatImportQueue>();
        services.AddHostedService<WeChatCallbackImportWorker>();
        services.AddHostedService<WeChatSyncBackgroundService>();
        services.AddScoped<IOrgMemberAdminRepository, OrgMemberAdminRepository>();
        services.AddScoped<ISaleWorkAdminRepository, SaleWorkAdminRepository>();
        services.AddScoped<IPartnerAdminRepository, PartnerAdminRepository>();
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

    // Read-only-debug fallback: pull the existing least-privilege read-only connection from the same sources
    // the read contexts use, so the admin context can reuse it for local login/inspection (writes still fail
    // at the SQL level). Mirrors the precedence in ApplyConnectionStringResolution.
    internal static string? ResolveReadOnlyConnection(
        IConfiguration configuration,
        string connectionStringsKey,
        string legacyDataKey,
        string environmentKey)
    {
        var legacyData = configuration[legacyDataKey];
        if (!string.IsNullOrWhiteSpace(legacyData))
        {
            return legacyData;
        }

        var environment = configuration[environmentKey];
        if (!string.IsNullOrWhiteSpace(environment))
        {
            return environment;
        }

        var connectionStrings = configuration[connectionStringsKey];
        return string.IsNullOrWhiteSpace(connectionStrings) ? null : connectionStrings;
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

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Szaipa.Data.Composition;
using Szaipa.Data.Configuration;
using Szaipa.Data.Contracts.Home;
using Szaipa.Web.Areas.Staff.Controllers;
using Szaipa.Web.Areas.Staff.Models;
using Szaipa.Web.Authorization;
using Szaipa.Web.Configuration;
using Szaipa.Web.Models;
using Szaipa.Web.Services;
using Xunit;

namespace Szaipa.Web.Tests;

/// <summary>
/// Web-layer guarantees of the data-driven homepage blocks: the public provider degrades to the built-in
/// content without touching the database when read models are off, the three admin controllers are
/// staff-only with anti-forgery on every write, and the form validators keep image/link values safe to render.
/// </summary>
public sealed class HomeBlocksContractTests
{
    [Fact]
    public async Task Provider_returns_defaults_without_resolving_the_repository_when_read_models_are_off()
    {
        // An empty service provider throws if the provider tries to resolve IHomeBlocksReadRepository.
        var provider = new HomeBlocksProvider(
            Options.Create(new ReadOnlyMigrationOptions { EnableSzaipaReadModels = false }),
            new StubDiagnostics(hasConnection: true, LegacyAccessMode.ReadOnly),
            new ServiceCollection().BuildServiceProvider());

        var blocks = await provider.GetHomeBlocksAsync();
        var partners = await provider.GetPartnersAsync();

        Assert.Same(HomeBlocksDefaults.CoreMembers, blocks.CoreMembers);
        Assert.Same(HomeBlocksDefaults.AdvisoryMembers, blocks.AdvisoryMembers);
        Assert.Same(HomeBlocksDefaults.SaleWorks, blocks.SaleWorks);
        Assert.Same(HomeBlocksDefaults.Partners, partners);
    }

    [Theory]
    [InlineData(true, false, "Disabled")]
    [InlineData(true, true, "Disabled")]
    [InlineData(true, false, "ReadOnly")]
    public async Task Provider_stays_on_defaults_when_the_szaipa_source_is_disabled_or_has_no_connection(
        bool readModels, bool hasConnection, string accessMode)
    {
        var provider = new HomeBlocksProvider(
            Options.Create(new ReadOnlyMigrationOptions { EnableSzaipaReadModels = readModels }),
            new StubDiagnostics(hasConnection, Enum.Parse<LegacyAccessMode>(accessMode)),
            new ServiceCollection().BuildServiceProvider());

        Assert.Same(HomeBlocksDefaults.Partners, await provider.GetPartnersAsync());
    }

    [Fact]
    public async Task Provider_uses_the_repository_when_read_models_are_active()
    {
        var expected = new[] { new Szaipa.Data.Models.Home.PartnerModel("库里的伙伴", null, null, null, null, null) };
        var services = new ServiceCollection().AddSingleton<IHomeBlocksReadRepository>(new StubRepository(expected)).BuildServiceProvider();
        var provider = new HomeBlocksProvider(
            Options.Create(new ReadOnlyMigrationOptions { EnableSzaipaReadModels = true }),
            new StubDiagnostics(hasConnection: true, LegacyAccessMode.ReadOnly),
            services);

        Assert.Same(expected, await provider.GetPartnersAsync());
    }

    [Theory]
    [InlineData(typeof(OrgMemberController))]
    [InlineData(typeof(SaleWorkController))]
    [InlineData(typeof(PartnerController))]
    public void Admin_controllers_are_staff_only_and_every_post_is_antiforgery_protected(Type controller)
    {
        var authorize = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();
        Assert.Contains(authorize, a => a.Policy == AdminAuthorization.StaffPolicy);

        var posts = controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttributes<HttpPostAttribute>().Any())
            .ToList();
        Assert.NotEmpty(posts);
        foreach (var post in posts)
        {
            Assert.True(
                post.GetCustomAttributes<ValidateAntiForgeryTokenAttribute>().Any(),
                $"{controller.Name}.{post.Name} is a POST without [ValidateAntiForgeryToken].");
        }

        // The shared Move/Visible/Delete writes must exist on every module.
        foreach (var name in new[] { "Move", "Visible", "Delete" })
        {
            Assert.Contains(posts, m => m.Name == name);
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("/Content/ArtImg/Org/a b.png", true)]
    [InlineData("/Content/images/to black/vip-2zlq.png", true)]
    [InlineData("http://evil.example/x.png", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/Content/../Web.config", false)]
    [InlineData("/Content/x.png\" onerror=\"alert(1)", false)]
    [InlineData("/Other/x.png", false)]
    public void ContentImagePath_only_accepts_site_relative_content_paths(string? value, bool valid)
    {
        Assert.Equal(valid, new ContentImagePathAttribute().IsValid(value));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("https://example.com/a?b=1", true)]
    [InlineData("http://example.com", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("//example.com", false)]
    [InlineData("example.com", false)]
    public void HttpUrl_only_accepts_absolute_http_or_https_links(string? value, bool valid)
    {
        Assert.Equal(valid, new HttpUrlAttribute().IsValid(value));
    }

    private sealed class StubDiagnostics(bool hasConnection, LegacyAccessMode mode) : IMigrationWorkspaceDiagnosticsService
    {
        public MigrationWorkspaceDiagnosticsModel GetDiagnostics() => new()
        {
            HasReadWriteLegacySource = false,
            ReadModelFlags = new Dictionary<string, bool>(),
            LegacySources = new Dictionary<string, LegacySourceStateModel>
            {
                [nameof(LegacyDataSource.Szaipa)] = new()
                {
                    AccessMode = mode.ToString(),
                    ConnectionStringSource = "Test",
                    HasConnectionString = hasConnection,
                    ScaffoldReady = false
                }
            }
        };
    }

    private sealed class StubRepository(IReadOnlyList<Szaipa.Data.Models.Home.PartnerModel> partners) : IHomeBlocksReadRepository
    {
        public Task<Szaipa.Data.Models.Home.HomeStaticBlocksModel> GetHomeBlocksAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Szaipa.Data.Models.Home.PartnerModel>> GetPartnersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(partners);
    }
}

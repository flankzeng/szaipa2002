using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Models.Home;
using Szaipa.Data.Services.Admin;
using Szaipa.Data.Services.Home;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Read-side projection and degradation of the data-driven homepage blocks. Rows are written through the real
/// admin repositories and read back through the read-only context on the SAME SQLite connection, so the
/// write -> read round trip (including the <see cref="HomeBlocksDefaults"/> equivalence the SQL seed relies on)
/// is covered end to end.
/// </summary>
public sealed class HomeBlocksReadRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    [Fact]
    public async Task Empty_tables_fall_back_to_the_built_in_defaults()
    {
        await using var fixture = CreateFixture();

        var blocks = await fixture.Read.GetHomeBlocksAsync();
        var partners = await fixture.Read.GetPartnersAsync();

        Assert.Same(HomeBlocksDefaults.CoreMembers, blocks.CoreMembers);
        Assert.Same(HomeBlocksDefaults.AdvisoryMembers, blocks.AdvisoryMembers);
        Assert.Same(HomeBlocksDefaults.SaleWorks, blocks.SaleWorks);
        Assert.Same(HomeBlocksDefaults.Partners, partners);
    }

    [Fact]
    public async Task Missing_tables_fall_back_to_defaults_instead_of_throwing()
    {
        await using var fixture = CreateFixture();
        await fixture.Admin.Database.ExecuteSqlRawAsync("DROP TABLE OrgMember");
        await fixture.Admin.Database.ExecuteSqlRawAsync("DROP TABLE SaleWork");
        await fixture.Admin.Database.ExecuteSqlRawAsync("DROP TABLE Partner");

        var blocks = await fixture.Read.GetHomeBlocksAsync();
        var partners = await fixture.Read.GetPartnersAsync();

        Assert.Same(HomeBlocksDefaults.CoreMembers, blocks.CoreMembers);
        Assert.Same(HomeBlocksDefaults.AdvisoryMembers, blocks.AdvisoryMembers);
        Assert.Same(HomeBlocksDefaults.SaleWorks, blocks.SaleWorks);
        Assert.Same(HomeBlocksDefaults.Partners, partners);
    }

    [Fact]
    public async Task A_missing_table_is_probed_again_only_after_the_retry_window()
    {
        var clock = DateTimeOffset.UtcNow;
        var schema = new HomeBlocksSchemaState(() => clock);
        await using var fixture = CreateFixture(schema);
        await fixture.Admin.Database.ExecuteSqlRawAsync("DROP TABLE Partner");

        Assert.Same(HomeBlocksDefaults.Partners, await fixture.Read.GetPartnersAsync()); // marks missing
        Assert.True(schema.ShouldSkip(nameof(Partner)));

        // Table appears (operator ran the SQL) but we are still inside the window -> still defaults, no query.
        await fixture.Admin.Database.ExecuteSqlRawAsync(
            "CREATE TABLE Partner (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, LogoPath TEXT, ImageWidth INTEGER, ImageHeight INTEGER, Link TEXT, Description TEXT, Sort INTEGER NOT NULL, IsVisible INTEGER NOT NULL)");
        await fixture.Admin.Database.ExecuteSqlRawAsync("INSERT INTO Partner (Name, Sort, IsVisible) VALUES ('新伙伴', 1, 1)");
        Assert.Same(HomeBlocksDefaults.Partners, await fixture.Read.GetPartnersAsync());

        clock += HomeBlocksSchemaState.RetryAfter + TimeSpan.FromSeconds(1);
        var partners = await fixture.Read.GetPartnersAsync();
        Assert.Equal(new[] { "新伙伴" }, partners.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task Members_are_split_by_group_hidden_ones_dropped_and_order_follows_Sort()
    {
        await using var fixture = CreateFixture();
        var members = new OrgMemberAdminRepository(fixture.Admin, new OperationRecorder());
        await members.CreateAsync(new OrgMember { Name = "甲", Role = "会长", Title = "艺术家", ArtistId = 1000, AvatarPath = "/Content/a.png", ImageWidth = 5, ImageHeight = 6, IsVisible = true }, Actor, default);
        var hidden = await members.CreateAsync(new OrgMember { Name = "乙", IsVisible = true }, Actor, default);
        await members.CreateAsync(new OrgMember { Name = "丙", Role = "会员", IsVisible = true }, Actor, default);
        await members.CreateAsync(new OrgMember { Name = "顾问甲", Role = "策展人", Title = "评论家", GroupName = OrgGroups.Advisory, IsVisible = true }, Actor, default);
        await members.SetVisibleAsync(hidden, false, Actor, default);
        var third = (await fixture.Admin.OrgMember.SingleAsync(m => m.Name == "丙")).Id;
        await members.MoveAsync(third, -1, Actor, default); // 丙 above the hidden 乙 -> 甲 丙 乙

        var blocks = await fixture.Read.GetHomeBlocksAsync();

        Assert.Equal(new[] { "甲", "丙" }, blocks.CoreMembers.Select(m => m.Name).ToArray());
        Assert.Equal(new OrgMemberModel("甲", "会长", "艺术家", 1000, "/Content/a.png", 5, 6), blocks.CoreMembers[0]);
        Assert.Equal(new[] { "顾问甲" }, blocks.AdvisoryMembers.Select(m => m.Name).ToArray());
        // The OrgMember table has rows, so the other blocks keep their own fallbacks independently.
        Assert.Same(HomeBlocksDefaults.SaleWorks, blocks.SaleWorks);
    }

    [Fact]
    public async Task Hiding_every_row_yields_an_empty_block_not_the_defaults()
    {
        await using var fixture = CreateFixture();
        var works = new SaleWorkAdminRepository(fixture.Admin, new OperationRecorder());
        var id = await works.CreateAsync(new SaleWork { Title = "冲气", IsVisible = true }, Actor, default);
        await works.SetVisibleAsync(id, false, Actor, default);

        var blocks = await fixture.Read.GetHomeBlocksAsync();

        Assert.Empty(blocks.SaleWorks);
    }

    [Fact]
    public async Task A_member_without_an_avatar_borrows_the_linked_artists_avatar()
    {
        await using var fixture = CreateFixture();
        fixture.Admin.Artist.Add(new Artist { Id = 1000, ArtistNameCN = "张岚芊", Path = "zlq.png" });
        await fixture.Admin.SaveChangesAsync();
        var members = new OrgMemberAdminRepository(fixture.Admin, new OperationRecorder());
        await members.CreateAsync(new OrgMember { Name = "张岚芊", ArtistId = 1000, ImageWidth = 9, ImageHeight = 9, IsVisible = true }, Actor, default);
        await members.CreateAsync(new OrgMember { Name = "无关联", IsVisible = true }, Actor, default);

        var blocks = await fixture.Read.GetHomeBlocksAsync();

        var linked = blocks.CoreMembers[0];
        Assert.Equal("/Content/ArtImg/Artist/zlq.png", linked.AvatarPath);
        Assert.Null(linked.ImageWidth); // the artist avatar's size is unknown
        Assert.Equal(1000, linked.ArtistId);
        var unlinked = blocks.CoreMembers[1];
        Assert.Null(unlinked.ArtistId);
        Assert.Null(unlinked.AvatarPath);
    }

    [Fact]
    public async Task A_sale_work_without_an_artist_name_borrows_the_linked_artists_name()
    {
        await using var fixture = CreateFixture();
        fixture.Admin.Artist.Add(new Artist { Id = 1049, ArtistNameCN = "陈硕子" });
        await fixture.Admin.SaveChangesAsync();
        var works = new SaleWorkAdminRepository(fixture.Admin, new OperationRecorder());
        await works.CreateAsync(new SaleWork { ArtistId = 1049, Title = "冲气", Size = "85x58cm", Medium = "纸本设色", ImagePath = "/Content/ArtImg/selling/cxz.jpg", ImageWidth = 1726, ImageHeight = 1224, IsVisible = true }, Actor, default);

        var blocks = await fixture.Read.GetHomeBlocksAsync();

        Assert.Equal(
            new SaleWorkModel(1049, "陈硕子", "冲气", "85x58cm", "纸本设色", "/Content/ArtImg/selling/cxz.jpg", 1726, 1224),
            Assert.Single(blocks.SaleWorks));
    }

    [Fact]
    public async Task Partners_are_ordered_hidden_ones_dropped_and_fields_projected()
    {
        await using var fixture = CreateFixture();
        var partners = new PartnerAdminRepository(fixture.Admin, new OperationRecorder());
        await partners.CreateAsync(new Partner { Name = "一", LogoPath = "/Content/1.png", ImageWidth = 800, ImageHeight = 800, Link = "https://a.example", Description = "简介", IsVisible = true }, Actor, default);
        var hidden = await partners.CreateAsync(new Partner { Name = "二", IsVisible = true }, Actor, default);
        var last = await partners.CreateAsync(new Partner { Name = "三", IsVisible = true }, Actor, default);
        await partners.SetVisibleAsync(hidden, false, Actor, default);
        await partners.MoveAsync(last, -1, Actor, default); // 一 三 二

        var result = await fixture.Read.GetPartnersAsync();

        Assert.Equal(new[] { "一", "三" }, result.Select(p => p.Name).ToArray());
        Assert.Equal(new PartnerModel("一", "/Content/1.png", 800, 800, "https://a.example", "简介"), result[0]);
    }

    /// <summary>
    /// The SQL seed is generated from <see cref="HomeBlocksDefaults"/>. Inserting exactly those rows and reading
    /// them back must reproduce the defaults, i.e. "run the seed" and "table missing" render the same page.
    /// </summary>
    [Fact]
    public async Task Seeding_the_defaults_into_the_tables_reads_back_identical_to_the_fallback()
    {
        await using var fixture = CreateFixture();
        var members = new OrgMemberAdminRepository(fixture.Admin, new OperationRecorder());
        foreach (var m in HomeBlocksDefaults.CoreMembers)
        {
            await members.CreateAsync(new OrgMember { Name = m.Name, Role = m.Role, Title = m.Title, GroupName = OrgGroups.Core, ArtistId = m.ArtistId, AvatarPath = m.AvatarPath, ImageWidth = m.ImageWidth, ImageHeight = m.ImageHeight, IsVisible = true }, Actor, default);
        }

        foreach (var m in HomeBlocksDefaults.AdvisoryMembers)
        {
            await members.CreateAsync(new OrgMember { Name = m.Name, Role = m.Role, Title = m.Title, GroupName = OrgGroups.Advisory, ArtistId = m.ArtistId, AvatarPath = m.AvatarPath, ImageWidth = m.ImageWidth, ImageHeight = m.ImageHeight, IsVisible = true }, Actor, default);
        }

        var works = new SaleWorkAdminRepository(fixture.Admin, new OperationRecorder());
        foreach (var w in HomeBlocksDefaults.SaleWorks)
        {
            await works.CreateAsync(new SaleWork { ArtistId = w.ArtistId, ArtistName = w.ArtistName, Title = w.Title, Size = w.Size, Medium = w.Medium, ImagePath = w.ImagePath, ImageWidth = w.ImageWidth, ImageHeight = w.ImageHeight, IsVisible = true }, Actor, default);
        }

        var partners = new PartnerAdminRepository(fixture.Admin, new OperationRecorder());
        foreach (var p in HomeBlocksDefaults.Partners)
        {
            await partners.CreateAsync(new Partner { Name = p.Name, LogoPath = p.LogoPath, ImageWidth = p.ImageWidth, ImageHeight = p.ImageHeight, Link = p.Link, Description = p.Description, IsVisible = true }, Actor, default);
        }

        var blocks = await fixture.Read.GetHomeBlocksAsync();
        var readPartners = await fixture.Read.GetPartnersAsync();

        Assert.Equal(HomeBlocksDefaults.CoreMembers, blocks.CoreMembers);
        Assert.Equal(HomeBlocksDefaults.AdvisoryMembers, blocks.AdvisoryMembers);
        Assert.Equal(HomeBlocksDefaults.SaleWorks, blocks.SaleWorks);
        Assert.Equal(HomeBlocksDefaults.Partners, readPartners);
    }

    [Fact]
    public void IsMissingSchemaObject_recognises_sqlite_messages_and_ignores_other_errors()
    {
        Assert.True(HomeBlocksReadRepository.IsMissingSchemaObject(new SqliteException("SQLite Error 1: 'no such table: OrgMember'.", 1)));
        Assert.True(HomeBlocksReadRepository.IsMissingSchemaObject(new SqliteException("SQLite Error 1: 'no such column: o.ImageWidth'.", 1)));
        Assert.False(HomeBlocksReadRepository.IsMissingSchemaObject(new SqliteException("SQLite Error 5: 'database is locked'.", 5)));
    }

    private static ReadFixture CreateFixture(HomeBlocksSchemaState? schema = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var admin = new SzaipaAdminContext(new DbContextOptionsBuilder<SzaipaAdminContext>().UseSqlite(connection).Options);
        admin.Database.EnsureCreated();
        var read = new SzaipaLegacyReadContext(new DbContextOptionsBuilder<SzaipaLegacyReadContext>().UseSqlite(connection).Options);

        return new ReadFixture(admin, read, new HomeBlocksReadRepository(read, schema ?? new HomeBlocksSchemaState()), connection);
    }

    private sealed class ReadFixture(
        SzaipaAdminContext admin,
        SzaipaLegacyReadContext readContext,
        HomeBlocksReadRepository read,
        SqliteConnection connection) : IAsyncDisposable
    {
        public SzaipaAdminContext Admin { get; } = admin;

        public HomeBlocksReadRepository Read { get; } = read;

        public async ValueTask DisposeAsync()
        {
            await readContext.DisposeAsync();
            await Admin.DisposeAsync();
            connection.Dispose();
        }
    }
}

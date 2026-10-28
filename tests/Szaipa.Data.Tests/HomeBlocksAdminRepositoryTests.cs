using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Contexts.SzaipaAdmin;
using Szaipa.Data.Services.Admin;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// CRUD / ordering / visibility behaviour of the three ordered-block admin repositories (OrgMember, SaleWork,
/// Partner) against a SQLite in-memory database with the admin context's schema. The shared base class is
/// exercised through all three so a regression in any one derived mapping is caught.
/// </summary>
public sealed class HomeBlocksAdminRepositoryTests
{
    private static readonly AdminActor Actor = new(1, "tester");

    // ---------- OrgMember ----------

    [Fact]
    public async Task OrgMember_create_trims_fields_and_appends_to_end_of_its_group()
    {
        await using var fixture = CreateContext();
        var repository = new OrgMemberAdminRepository(fixture.Context, new OperationRecorder());

        var first = await repository.CreateAsync(new OrgMember { Name = " 张三 ", Role = " 会长 ", Title = "  ", IsVisible = true }, Actor, default);
        var second = await repository.CreateAsync(new OrgMember { Name = "李四", Role = "会员", IsVisible = true }, Actor, default);
        var advisor = await repository.CreateAsync(new OrgMember { Name = "王五", GroupName = OrgGroups.Advisory, IsVisible = true }, Actor, default);

        var rows = await fixture.Context.OrgMember.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        Assert.Equal("张三", rows[0].Name);
        Assert.Equal("会长", rows[0].Role);
        Assert.Null(rows[0].Title); // blank -> null
        Assert.Equal(new[] { 1, 2 }, new[] { rows[0].Sort, rows[1].Sort });
        Assert.Equal(1, rows[2].Sort); // sorting is per group: the advisor starts its own group at 1
        Assert.Equal(OrgGroups.Advisory, rows[2].GroupName);
        Assert.True(first > 0 && second > first && advisor > second);
    }

    [Fact]
    public async Task OrgMember_update_changes_fields_and_moving_group_goes_to_end_of_new_group()
    {
        await using var fixture = CreateContext();
        var repository = new OrgMemberAdminRepository(fixture.Context, new OperationRecorder());
        var a = await repository.CreateAsync(new OrgMember { Name = "A", IsVisible = true }, Actor, default);
        var b = await repository.CreateAsync(new OrgMember { Name = "B", GroupName = OrgGroups.Advisory, IsVisible = true }, Actor, default);

        var updated = await repository.UpdateAsync(
            new OrgMember { Id = a, Name = "A2", Role = "副会长", Title = "教授", GroupName = OrgGroups.Advisory, ArtistId = 1009, AvatarPath = "/Content/x.png", ImageWidth = 10, ImageHeight = 20, IsVisible = false },
            Actor, default);

        Assert.True(updated);
        var saved = await fixture.Context.OrgMember.AsNoTracking().SingleAsync(m => m.Id == a);
        Assert.Equal("A2", saved.Name);
        Assert.Equal("副会长", saved.Role);
        Assert.Equal(1009, saved.ArtistId);
        Assert.Equal("/Content/x.png", saved.AvatarPath);
        Assert.Equal((10, 20), (saved.ImageWidth, saved.ImageHeight));
        Assert.False(saved.IsVisible);
        Assert.Equal(2, saved.Sort); // B already holds Sort 1 in 顾问团队, so A lands after it
        Assert.False(await repository.UpdateAsync(new OrgMember { Id = 999, Name = "x" }, Actor, default));
        Assert.True(b > 0);
    }

    [Fact]
    public async Task OrgMember_move_swaps_neighbours_within_group_and_ignores_other_groups()
    {
        await using var fixture = CreateContext();
        var repository = new OrgMemberAdminRepository(fixture.Context, new OperationRecorder());
        var a = await repository.CreateAsync(new OrgMember { Name = "A", IsVisible = true }, Actor, default);
        var b = await repository.CreateAsync(new OrgMember { Name = "B", IsVisible = true }, Actor, default);
        var c = await repository.CreateAsync(new OrgMember { Name = "C", IsVisible = true }, Actor, default);
        var other = await repository.CreateAsync(new OrgMember { Name = "顾问", GroupName = OrgGroups.Advisory, IsVisible = true }, Actor, default);

        await repository.MoveAsync(c, -1, Actor, default); // A C B
        Assert.Equal(new[] { "A", "C", "B" }, await NamesInGroup(fixture, OrgGroups.Core));

        await repository.MoveAsync(a, 1, Actor, default); // C A B
        Assert.Equal(new[] { "C", "A", "B" }, await NamesInGroup(fixture, OrgGroups.Core));

        await repository.MoveAsync(a, -1, Actor, default); // A is index 1 -> A C B
        await repository.MoveAsync(a, -1, Actor, default); // already first: no-op
        Assert.Equal(new[] { "A", "C", "B" }, await NamesInGroup(fixture, OrgGroups.Core));

        Assert.Equal(1, (await fixture.Context.OrgMember.AsNoTracking().SingleAsync(m => m.Id == other)).Sort); // untouched
        Assert.False(await repository.MoveAsync(999, 1, Actor, default));
        Assert.True(b > 0);
    }

    [Fact]
    public async Task Reorder_applies_the_dragged_order_keeps_hidden_rows_after_and_rejects_cross_group()
    {
        await using var fixture = CreateContext();
        var repository = new OrgMemberAdminRepository(fixture.Context, new OperationRecorder());
        var a = await repository.CreateAsync(new OrgMember { Name = "A", IsVisible = true }, Actor, default);
        var b = await repository.CreateAsync(new OrgMember { Name = "B", IsVisible = true }, Actor, default);
        var hidden = await repository.CreateAsync(new OrgMember { Name = "隐", IsVisible = false }, Actor, default);
        var c = await repository.CreateAsync(new OrgMember { Name = "C", IsVisible = true }, Actor, default);
        var advisor = await repository.CreateAsync(new OrgMember { Name = "顾问", GroupName = OrgGroups.Advisory, IsVisible = true }, Actor, default);

        // 页面上只看得到 A B C（隐藏的不在页面），拖成 C A B。
        Assert.True(await repository.ReorderAsync(new[] { c, a, b }, Actor, default));
        Assert.Equal(new[] { "C", "A", "B", "隐" }, await NamesInGroup(fixture, OrgGroups.Core));

        Assert.False(await repository.ReorderAsync(new[] { a, advisor }, Actor, default)); // 跨组
        Assert.False(await repository.ReorderAsync(new[] { a, a }, Actor, default));       // 重复
        Assert.False(await repository.ReorderAsync(new[] { 999 }, Actor, default));        // 不存在
        Assert.Equal(new[] { "C", "A", "B", "隐" }, await NamesInGroup(fixture, OrgGroups.Core));
        Assert.True(hidden > 0);
    }

    [Fact]
    public async Task Move_renumbers_ties_and_gaps_to_a_contiguous_sequence()
    {
        await using var fixture = CreateContext();
        fixture.Context.SaleWork.AddRange(
            new SaleWork { Title = "a", Sort = 5, IsVisible = true },
            new SaleWork { Title = "b", Sort = 5, IsVisible = true },
            new SaleWork { Title = "c", Sort = 40, IsVisible = true });
        await fixture.Context.SaveChangesAsync();
        var repository = new SaleWorkAdminRepository(fixture.Context, new OperationRecorder());

        await repository.MoveAsync(3, -1, Actor, default); // a b c (ties by Id) -> a c b

        var rows = await fixture.Context.SaleWork.AsNoTracking().OrderBy(w => w.Sort).ToListAsync();
        Assert.Equal(new[] { "a", "c", "b" }, rows.Select(w => w.Title).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(w => w.Sort).ToArray());
    }

    [Fact]
    public async Task OrgMember_list_orders_known_groups_first_then_by_sort()
    {
        await using var fixture = CreateContext();
        var repository = new OrgMemberAdminRepository(fixture.Context, new OperationRecorder());
        await repository.CreateAsync(new OrgMember { Name = "顾问1", GroupName = OrgGroups.Advisory, IsVisible = true }, Actor, default);
        await repository.CreateAsync(new OrgMember { Name = "核心1", IsVisible = true }, Actor, default);
        await repository.CreateAsync(new OrgMember { Name = "自定义", GroupName = "理事会", IsVisible = true }, Actor, default);
        await repository.CreateAsync(new OrgMember { Name = "核心2", IsVisible = true }, Actor, default);

        var all = await repository.GetAllAsync(default);

        Assert.Equal(new[] { "核心1", "核心2", "顾问1", "自定义" }, all.Select(m => m.Name).ToArray());
    }

    // ---------- visibility / delete / logging (shared base, all three entities) ----------

    [Fact]
    public async Task SetVisible_toggles_the_flag_and_reports_missing_rows()
    {
        await using var fixture = CreateContext();
        var repository = new PartnerAdminRepository(fixture.Context, new OperationRecorder());
        var id = await repository.CreateAsync(new Partner { Name = "P", IsVisible = true }, Actor, default);

        Assert.True(await repository.SetVisibleAsync(id, false, Actor, default));
        Assert.False((await fixture.Context.Partner.AsNoTracking().SingleAsync(p => p.Id == id)).IsVisible);
        Assert.True(await repository.SetVisibleAsync(id, true, Actor, default));
        Assert.True((await fixture.Context.Partner.AsNoTracking().SingleAsync(p => p.Id == id)).IsVisible);
        Assert.False(await repository.SetVisibleAsync(999, true, Actor, default));
    }

    [Fact]
    public async Task Create_can_save_a_hidden_row_and_Delete_removes_it()
    {
        await using var fixture = CreateContext();
        var repository = new SaleWorkAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repository.CreateAsync(new SaleWork { Title = "冲气", IsVisible = false }, Actor, default);
        Assert.False((await fixture.Context.SaleWork.AsNoTracking().SingleAsync(w => w.Id == id)).IsVisible);

        Assert.True(await repository.DeleteAsync(id, Actor, default));
        Assert.Empty(await fixture.Context.SaleWork.AsNoTracking().ToListAsync());
        Assert.False(await repository.DeleteAsync(id, Actor, default));
    }

    [Fact]
    public async Task Writes_are_recorded_in_the_diary_and_the_staff_record()
    {
        await using var fixture = CreateContext();
        fixture.Context.Staff.Add(new Staff { Id = 1, StaffName = "tester", OperationRecord = "" });
        await fixture.Context.SaveChangesAsync();
        var repository = new OrgMemberAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repository.CreateAsync(new OrgMember { Name = "张三", IsVisible = true }, Actor, default);
        await repository.SetVisibleAsync(id, false, Actor, default);
        await repository.DeleteAsync(id, Actor, default);

        var diary = await fixture.Context.Diary.AsNoTracking().SingleAsync();
        Assert.Contains("新增了 张三的组织架构成员", diary.OperationRecord);
        Assert.Contains("隐藏了 张三的组织架构成员", diary.OperationRecord);
        Assert.Contains("删除了 张三的组织架构成员", diary.OperationRecord);
        var staff = await fixture.Context.Staff.AsNoTracking().SingleAsync(s => s.Id == 1);
        Assert.Contains("新增了 张三的组织架构成员", staff.OperationRecord);
    }

    [Fact]
    public async Task Partner_name_line_breaks_are_normalised_and_description_is_cleaned()
    {
        await using var fixture = CreateContext();
        var repository = new PartnerAdminRepository(fixture.Context, new OperationRecorder());

        var id = await repository.CreateAsync(
            new Partner { Name = "深圳反转舞台\r\n文化传播有限公司", Description = "  第一段\r\n\r\n第二段  ", Link = "  https://example.com/x ", IsVisible = true },
            Actor, default);

        var saved = await fixture.Context.Partner.AsNoTracking().SingleAsync(p => p.Id == id);
        Assert.Equal("深圳反转舞台\n文化传播有限公司", saved.Name);
        Assert.Equal("第一段\n\n第二段", saved.Description);
        Assert.Equal("https://example.com/x", saved.Link);
    }

    [Fact]
    public async Task GetArtistOptions_returns_names_with_avatar_file_for_the_link_dropdown()
    {
        await using var fixture = CreateContext();
        fixture.Context.Artist.AddRange(
            new Artist { Id = 1000, ArtistNameCN = "张岚芊", Path = "a.png" },
            new Artist { Id = 1001, ArtistNameCN = null });
        await fixture.Context.SaveChangesAsync();
        var repository = new OrgMemberAdminRepository(fixture.Context, new OperationRecorder());

        var options = await repository.GetArtistOptionsAsync(default);

        Assert.Contains(new ArtistAvatarOption(1000, "张岚芊", "a.png"), options);
        Assert.Contains(options, o => o.Id == 1001 && o.Name == "#1001");
    }

    private static async Task<string[]> NamesInGroup(AdminContextFixture fixture, string group) =>
        (await fixture.Context.OrgMember.AsNoTracking().Where(m => m.GroupName == group).OrderBy(m => m.Sort).ToListAsync())
        .Select(m => m.Name).ToArray();

    private static AdminContextFixture CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SzaipaAdminContext>().UseSqlite(connection).Options;
        var context = new SzaipaAdminContext(options);
        context.Database.EnsureCreated();
        return new AdminContextFixture(context, connection);
    }

    private sealed class AdminContextFixture(SzaipaAdminContext context, SqliteConnection connection) : IAsyncDisposable
    {
        public SzaipaAdminContext Context { get; } = context;

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            connection.Dispose();
        }
    }
}

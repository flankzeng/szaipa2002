using System.Text;
using Szaipa.Data.Composition;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Models.Home;
using Xunit;

namespace Szaipa.Data.Tests;

/// <summary>
/// Keeps docs/sql/2026-10-26-org-and-home-blocks.sql in sync with <see cref="HomeBlocksDefaults"/>. The seed
/// INSERTs between the BEGIN SEED / END SEED markers are GENERATED here from the defaults; the test fails when
/// the checked-in file differs. After changing the defaults, regenerate the file with
/// <c>UPDATE_SEED_SQL=1 dotnet test --filter HomeBlocksSeedSqlTests</c>.
/// </summary>
public sealed class HomeBlocksSeedSqlTests
{
    [Fact]
    public void Checked_in_seed_blocks_match_the_generated_defaults()
    {
        var path = Path.Combine(FindRepositoryRoot(), "docs", "sql", "2026-10-26-org-and-home-blocks.sql");
        var original = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");

        var updated = original;
        updated = ReplaceSeed(updated, "OrgMember", GenerateOrgMemberSeed());
        updated = ReplaceSeed(updated, "SaleWork", GenerateSaleWorkSeed());
        updated = ReplaceSeed(updated, "Partner", GeneratePartnerSeed());

        if (Environment.GetEnvironmentVariable("UPDATE_SEED_SQL") == "1")
        {
            File.WriteAllText(path, updated, new UTF8Encoding(false));
            return;
        }

        Assert.True(
            original == updated,
            "docs/sql/2026-10-26-org-and-home-blocks.sql seed blocks are out of date with HomeBlocksDefaults. "
            + "Run: UPDATE_SEED_SQL=1 dotnet test --filter HomeBlocksSeedSqlTests");
    }

    [Fact]
    public void Seeds_are_only_inserted_into_empty_tables_so_reruns_never_duplicate_or_overwrite_edits()
    {
        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM dbo.OrgMember)", GenerateOrgMemberSeed());
        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM dbo.SaleWork)", GenerateSaleWorkSeed());
        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM dbo.Partner)", GeneratePartnerSeed());
    }

    [Fact]
    public void Sql_literals_escape_quotes_and_encode_line_breaks()
    {
        Assert.Equal("N'O''Neil'", Literal("O'Neil"));
        Assert.Equal("N'a' + NCHAR(10) + NCHAR(10) + N'b'", Literal("a\n\nb"));
        Assert.Equal("NULL", Literal(null));
    }

    // ---- generators --------------------------------------------------------------------------------------

    private static string GenerateOrgMemberSeed()
    {
        var rows = new List<string>();
        AddMembers(rows, OrgGroups.Core, HomeBlocksDefaults.CoreMembers);
        AddMembers(rows, OrgGroups.Advisory, HomeBlocksDefaults.AdvisoryMembers);

        return string.Join("\n",
            "IF NOT EXISTS (SELECT 1 FROM dbo.OrgMember)",
            "BEGIN",
            "    INSERT INTO dbo.OrgMember (Name, [Role], Title, GroupName, ArtistId, AvatarPath, ImageWidth, ImageHeight, Sort, IsVisible) VALUES",
            string.Join(",\n", rows) + ";",
            "END");

        static void AddMembers(List<string> rows, string group, IReadOnlyList<OrgMemberModel> members)
        {
            for (var i = 0; i < members.Count; i++)
            {
                var m = members[i];
                rows.Add($"    ({Literal(m.Name)}, {Literal(m.Role)}, {Literal(m.Title)}, {Literal(group)}, {Number(m.ArtistId)}, {Literal(m.AvatarPath)}, {Number(m.ImageWidth)}, {Number(m.ImageHeight)}, {i + 1}, 1)");
            }
        }
    }

    private static string GenerateSaleWorkSeed()
    {
        var rows = HomeBlocksDefaults.SaleWorks.Select((w, i) =>
            $"    ({Number(w.ArtistId)}, {Literal(w.ArtistName)}, {Literal(w.Title)}, {Literal(w.Size)}, {Literal(w.Medium)}, {Literal(w.ImagePath)}, {Number(w.ImageWidth)}, {Number(w.ImageHeight)}, {i + 1}, 1)");

        return string.Join("\n",
            "IF NOT EXISTS (SELECT 1 FROM dbo.SaleWork)",
            "BEGIN",
            "    INSERT INTO dbo.SaleWork (ArtistId, ArtistName, Title, Size, Medium, ImagePath, ImageWidth, ImageHeight, Sort, IsVisible) VALUES",
            string.Join(",\n", rows) + ";",
            "END");
    }

    private static string GeneratePartnerSeed()
    {
        var rows = HomeBlocksDefaults.Partners.Select((p, i) =>
            $"    ({Literal(p.Name)}, {Literal(p.LogoPath)}, {Number(p.ImageWidth)}, {Number(p.ImageHeight)}, {Literal(p.Link)}, {Literal(p.Description)}, {i + 1}, 1)");

        return string.Join("\n",
            "IF NOT EXISTS (SELECT 1 FROM dbo.Partner)",
            "BEGIN",
            "    INSERT INTO dbo.Partner (Name, LogoPath, ImageWidth, ImageHeight, Link, Description, Sort, IsVisible) VALUES",
            string.Join(",\n", rows) + ";",
            "END");
    }

    private static string Number(int? value) => value?.ToString() ?? "NULL";

    /// <summary>T-SQL unicode literal; newlines become NCHAR(10) so the file is line-ending independent.</summary>
    private static string Literal(string? value)
    {
        if (value is null)
        {
            return "NULL";
        }

        var tokens = new List<string>();
        var lines = value.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                tokens.Add("NCHAR(10)");
            }

            if (lines[i].Length > 0 || lines.Length == 1)
            {
                tokens.Add("N'" + lines[i].Replace("'", "''") + "'");
            }
        }

        return string.Join(" + ", tokens);
    }

    private static string ReplaceSeed(string sql, string table, string seed)
    {
        var begin = $"-- BEGIN SEED {table}\n";
        var end = $"-- END SEED {table}\n";
        var start = sql.IndexOf(begin, StringComparison.Ordinal);
        var stop = sql.IndexOf(end, StringComparison.Ordinal);
        Assert.True(start >= 0 && stop > start, $"Seed markers for {table} not found in the SQL file.");
        return sql[..(start + begin.Length)] + seed + "\n" + sql[stop..];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Szaipa.Modernization.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (Szaipa.Modernization.slnx) not found.");
    }
}

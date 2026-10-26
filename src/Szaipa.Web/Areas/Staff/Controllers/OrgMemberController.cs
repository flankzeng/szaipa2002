using Microsoft.AspNetCore.Mvc;
using Szaipa.Data.Contexts.Szaipa;
using Szaipa.Data.Services.Admin;
using Szaipa.Web.Areas.Staff.Models;

namespace Szaipa.Web.Areas.Staff.Controllers;

/// <summary>
/// 组织架构 (OrgMember) for the staff backend: the roster behind the homepage 核心成员 / 顾问团队 blocks. It is an
/// independent list — a member may optionally be linked to an Artist, in which case the public card links to
/// that artist's site. Order is per group (move up/down), and a member can be hidden without deleting.
/// </summary>
[Route("Staff/OrgMember")]
public sealed class OrgMemberController : SortableBlockControllerBase<OrgMember>
{
    private readonly IOrgMemberAdminRepository _repository;

    public OrgMemberController(IOrgMemberAdminRepository repository)
    {
        _repository = repository;
    }

    protected override ISortableAdminRepository<OrgMember> Repository => _repository;

    protected override string Noun => "成员";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var members = await _repository.GetAllAsync(cancellationToken);
        var artists = await _repository.GetArtistOptionsAsync(cancellationToken);
        return View(new OrgMemberIndexViewModel
        {
            Members = members,
            ArtistNames = artists.ToDictionary(a => a.Id, a => a.Name)
        });
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create(string? group, CancellationToken cancellationToken)
    {
        var model = new OrgMemberFormViewModel { ArtistOptions = await _repository.GetArtistOptionsAsync(cancellationToken) };
        if (group is not null && OrgGroups.All.Contains(group))
        {
            model.GroupName = group;
        }

        return View(model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrgMemberFormViewModel model, CancellationToken cancellationToken)
    {
        await ValidateAsync(model, cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _repository.CreateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        TempData["Success"] = "成员已添加。";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var member = await _repository.GetByIdAsync(id, cancellationToken);
        if (member is null)
        {
            return NotFound();
        }

        return View(new OrgMemberFormViewModel
        {
            Id = member.Id,
            Name = member.Name,
            Role = member.Role,
            Title = member.Title,
            GroupName = member.GroupName,
            ArtistId = member.ArtistId,
            AvatarPath = member.AvatarPath,
            ImageWidth = member.ImageWidth,
            ImageHeight = member.ImageHeight,
            IsVisible = member.IsVisible,
            ArtistOptions = await _repository.GetArtistOptionsAsync(cancellationToken)
        });
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OrgMemberFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        await ValidateAsync(model, cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var updated = await _repository.UpdateAsync(ToEntity(model), CurrentActor(), cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        TempData["Success"] = "成员已更新。";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Reloads the dropdown for redisplay and rejects ids that are not real artists / groups.</summary>
    private async Task ValidateAsync(OrgMemberFormViewModel model, CancellationToken cancellationToken)
    {
        model.ArtistOptions = await _repository.GetArtistOptionsAsync(cancellationToken);
        if (model.ArtistId is { } artistId && model.ArtistOptions.All(a => a.Id != artistId))
        {
            ModelState.AddModelError(nameof(model.ArtistId), "所选艺术家不存在。");
        }

        if (!OrgGroups.All.Contains(model.GroupName))
        {
            ModelState.AddModelError(nameof(model.GroupName), "未知的分组。");
        }
    }

    private static OrgMember ToEntity(OrgMemberFormViewModel model) => new()
    {
        Id = model.Id,
        Name = model.Name ?? string.Empty,
        Role = model.Role,
        Title = model.Title,
        GroupName = model.GroupName,
        ArtistId = model.ArtistId,
        AvatarPath = model.AvatarPath,
        ImageWidth = model.ImageWidth,
        ImageHeight = model.ImageHeight,
        IsVisible = model.IsVisible
    };
}

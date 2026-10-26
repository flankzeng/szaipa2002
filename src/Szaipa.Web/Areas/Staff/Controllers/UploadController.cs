using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Web.Authorization;
using Szaipa.Web.Services.Admin;

namespace Szaipa.Web.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Staff/Upload")]
public sealed class UploadController : Controller
{
    private readonly IAdminAssetStorage _assetStorage;
    public UploadController(IAdminAssetStorage assetStorage) => _assetStorage = assetStorage;

    [HttpPost("Image")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Image(IFormFile? file, IFormFile? original, string? reuseOriginalOf, string? folder, CancellationToken cancellationToken)
    {
        if (file is null) return Json(new { success = false, error = "未接收到文件。" });
        var targetFolder = folder ?? "newsImg";
        var result = await _assetStorage.SaveImageAsync(file, targetFolder, cancellationToken);
        if (!result.Success) return Json(new { success = false, error = result.Error });
        string? originalUrl = null;
        if (original is { Length: > 0 }) originalUrl = await _assetStorage.SaveOriginalAsync(original, targetFolder, result.FileName!, cancellationToken);
        else if (!string.IsNullOrWhiteSpace(reuseOriginalOf)) originalUrl = await _assetStorage.CloneOriginalAsync(targetFolder, reuseOriginalOf, result.FileName!, cancellationToken);
        return Json(new { success = true, url = result.Url, fileName = result.FileName, originalUrl });
    }

    [HttpGet("Original")]
    public IActionResult Original(string? folder, string? name)
    {
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\')) return BadRequest();
        var url = _assetStorage.FindOriginalUrl(folder, name);
        return url is null ? NotFound() : Json(new { success = true, url });
    }
}

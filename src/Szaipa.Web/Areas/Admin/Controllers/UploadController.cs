using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Szaipa.Web.Authorization;
using Szaipa.Web.Services.Admin;

namespace Szaipa.Web.Areas.Admin.Controllers;

/// <summary>
/// Image upload endpoint for the admin backend: the TipTap editor's inline images and the cover/file
/// dropzones POST here. Replaces the legacy <c>TempImgSave</c>/<c>TempImgIntervalSave</c> family. Each upload
/// is written once under a unique name into its final folder (see <see cref="IAdminAssetStorage"/>), so there
/// is no temp-then-move dance, no Session/TempData carryover, and no substring-match deletion.
/// </summary>
[Area("Admin")]
[Authorize(Policy = AdminAuthorization.StaffPolicy)]
[Route("Admin/Upload")]
public sealed class UploadController : Controller
{
    private readonly IAdminAssetStorage _assetStorage;

    public UploadController(IAdminAssetStorage assetStorage)
    {
        _assetStorage = assetStorage;
    }

    [HttpPost("Image")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Image(IFormFile? file, string? folder, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Json(new { success = false, error = "未接收到文件。" });
        }

        var result = await _assetStorage.SaveImageAsync(file, folder ?? "newsImg", cancellationToken);
        return result.Success
            ? Json(new { success = true, url = result.Url, fileName = result.FileName })
            : Json(new { success = false, error = result.Error });
    }
}

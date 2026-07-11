using Microsoft.AspNetCore.Mvc;

namespace Szaipa.Web.Controllers;

/// <summary>
/// Compatibility endpoint for the retired ProjectTongou public site.
/// The live navigation has no ingress to this module and the former dynamic routes returned 500;
/// keeping one lightweight 410 response prevents historical links from querying the legacy database.
/// </summary>
[Route("Project_Tongou")]
public sealed class ProjectTongouController : Controller
{
    [HttpGet("")]
    [HttpGet("{**legacyPath}")]
    public IActionResult Gone(string? legacyPath = null)
    {
        Response.StatusCode = StatusCodes.Status410Gone;
        Response.Headers.CacheControl = "public,max-age=86400";
        return View("Gone");
    }
}

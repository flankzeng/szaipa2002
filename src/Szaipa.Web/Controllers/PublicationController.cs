using Microsoft.AspNetCore.Mvc;

namespace Szaipa.Web.Controllers;

// Faithful port of the legacy Szaipa.Controllers.PublicationController. Historically every action was a
// hand-built static exhibition page (legacy bodies are all `return View();` with no model and no DB access).
//
// 2026-07-09 slug retirement: 14 of the simple gallery slugs were migrated to data-driven Publication rows
// (see docs/sql/2026-07-09-publication-slug-migration.sql and docs/updates/2026-07-09-publication-slug-migration.md
// for the slug -> fixed Id -> FolderName mapping) and now permanently redirect to /Home/Publication/{id} so old
// links/bookmarks/search results keep working. `zengfeng` is NOT migrated: unlike the other slugs it is not an
// image-gallery page at all but a self-contained flipbook mini-site (Layout=null, embeds a separate HTML5 app
// under /Content/publication/zengfeng/) that the Publication/_ExhibitionGallery data model cannot represent, so
// its action/view are left untouched. `index` (展会动态 listing) and the three large hand-coded pages
// (`chunyu3`, `tonggou2`, `tonggou2024`) are also left untouched — see PROJECT_MAP.md.
public class PublicationController : Controller
{
    public IActionResult index() => View();          // 展会动态

    public IActionResult tonggouEurope() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92001 });

    public IActionResult shuimai() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92002 });

    public IActionResult chunyu() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92003 });

    public IActionResult zengfeng() => View();        // custom flipbook mini-site, not data-driven — kept as-is

    public IActionResult zhongyi() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92005 });

    public IActionResult tonggou() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92006 });

    public IActionResult tonggou2() => View();

    public IActionResult chunyu2() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92007 });

    public IActionResult trio() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92008 });

    public IActionResult chunyu3() => View();

    public IActionResult man() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92009 });

    public IActionResult yijia() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92010 });

    public IActionResult zhongri() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92011 });

    public IActionResult shuyuyi() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92012 });

    public IActionResult tonggou2024() => View();

    public IActionResult chunyu4() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92013 });

    public IActionResult zhongfa() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92014 });

    public IActionResult tangqishan() => RedirectToActionPermanent(nameof(HomeController.Publication), "Home", new { id = 92015 });
}

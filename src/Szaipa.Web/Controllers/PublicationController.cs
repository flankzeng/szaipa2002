using Microsoft.AspNetCore.Mvc;

namespace Szaipa.Web.Controllers;

// Faithful port of the legacy Szaipa.Controllers.PublicationController. Each action is a hand-built static
// exhibition page (legacy bodies are all `return View();` with no model and no DB access), so this controller
// is purely presentational and touches no data source. Conventional routing ({controller}/{action}) maps the
// legacy links exactly: /Publication/chunyu -> chunyu(), etc. Action names are kept lowercase to match the
// legacy routes and the ported view file names (Views/Publication/<action>.cshtml).
public class PublicationController : Controller
{
    public IActionResult index() => View();          // 展会动态

    public IActionResult tonggouEurope() => View();  // 同构欧洲

    public IActionResult shuimai() => View();         // 2023龙游水脉艺术节 浙江衢州龙游

    public IActionResult chunyu() => View();          // 2022年《春雨》 深圳南油动漫园

    public IActionResult zengfeng() => View();

    public IActionResult zhongyi() => View();

    public IActionResult tonggou() => View();

    public IActionResult tonggou2() => View();

    public IActionResult chunyu2() => View();

    public IActionResult trio() => View();

    public IActionResult chunyu3() => View();

    public IActionResult man() => View();

    public IActionResult yijia() => View();

    public IActionResult zhongri() => View();

    public IActionResult shuyuyi() => View();

    public IActionResult tonggou2024() => View();

    public IActionResult chunyu4() => View();

    public IActionResult zhongfa() => View();

    public IActionResult tangqishan() => View();
}

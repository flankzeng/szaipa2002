using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Szaipa.Controllers
{
    public class PublicationController : Controller
    {
        // GET: Publication
        public ActionResult chunyu()//2022年《春雨》 深圳南油动漫园
        {
            return View();
        }

        public ActionResult zengfeng()
        {
            return View();
        }
    }
}
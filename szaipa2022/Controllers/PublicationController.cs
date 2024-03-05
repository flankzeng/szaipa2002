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
        public ActionResult index()//展会动态
        {
            return View();
        }

        public ActionResult tonggouEurope()//同构欧洲
        {
            return View();
        }

        public ActionResult shuimai()//2023龙游水脉艺术节 浙江衢州龙游
        {
            return View();
        }
        public ActionResult chunyu()//2022年《春雨》 深圳南油动漫园
        {
            return View();
        }

        public ActionResult zengfeng()
        {
            return View();
        }

        public ActionResult zhongyi()
        {
            return View();
        }

        public ActionResult tonggou()
        {
            return View();
        }

        public ActionResult tonggou2()
        {
            return View();
        }

        public ActionResult chunyu2()
        {
            return View();
        }

        public ActionResult trio()
        {
            return View();
        }
        public ActionResult chunyu3()
        {
            return View();
        }
    }
}
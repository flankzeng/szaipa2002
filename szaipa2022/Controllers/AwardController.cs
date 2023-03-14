using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Szaipa.Controllers
{
    public class AwardController : Controller
    {
        // GET: Award
        public ActionResult Index()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
           if(IsApp(this.HttpContext)) return RedirectToAction("Wap"+ actionName, "Award"); ;
            return View();
        }
        public ActionResult WapIndex()
        {
            return View();
        }





        /// <summary>
        /// 判读是否为手机移动端登录
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static bool IsApp(HttpContextBase context)
        {
            string agent = context.Request.Headers["User-Agent"];
            if (agent.Contains("iOSApp") || agent.Contains("Android") || agent.Contains("iPad") || agent.Contains("iPhone"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 判断是否为微信访问
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static bool isWX(HttpRequest request)
        {
            string userAgent = request.UserAgent.ToLower();
            if (userAgent.Contains("micromessenger"))
            {
                return true;
            }
            return false;
        }

    }
}
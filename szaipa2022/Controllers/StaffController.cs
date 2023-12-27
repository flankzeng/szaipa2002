
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using Szaipa.Models;
using szaipa2022.Models;

namespace Szaipa.Controllers
{
    public class StaffController : Controller
    {
        // GET: Staff
        SzaipaEntities db = new SzaipaEntities();
        HomeController home = new HomeController();


        public ActionResult TongouAtristList()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.tongou = 1;
            return View(staff);


        }
        public ActionResult TongouWorksList()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.tongou = 1;
            return View(staff);


        }




        public ActionResult CNv()
        {

            return View();
        }
        public ActionResult Login()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Login(Staff staff)
        {
            string password = Get_MD5(staff.Password, "utf-8");

            var item = db.Staff.FirstOrDefault(u => u.StaffName == staff.StaffName && u.Password == password);
            if (item != null)
            {
                Session["Staff"] = item;
                if (TempData["controller"] != null && TempData["view"] != null)
                {

                    string controller = TempData["controller"].ToString();
                    string view = TempData["view"].ToString();
                    if (TempData["id"] != null)
                    {
                        int id = Convert.ToInt32(TempData["id"].ToString());
                        return RedirectToAction(view + "/" + id, controller);
                    }
                    return RedirectToAction(view, controller);
                }
                return RedirectToAction("Index", "Staff");

            }
            else
            {
                ModelState.AddModelError("", "账户不存在或密码错误！");
            }

            return View();
        }
        public ActionResult OperationRecord()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = (Staff)Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffIndex = 1;
            var OR = or(staff.OperationRecord);
            return View(OR);
        }
        public ActionResult PasswordChange()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffIndex = 1;
            return View(staff);
        }
        [HttpPost]
        public ActionResult PasswordChange(FormCollection form)
        {

            var staffer = (Staff)Session["Staff"];
            if (staffer == null) return RedirectToAction("Login", "Staff");
            string staffname = staffer.StaffName;
            string password = Request.Form["Password"];
            password = Get_MD5(password, "utf-8");
            var item = db.Staff.FirstOrDefault(u => u.StaffName == staffname && u.Password == password);
            if (item != null)
            {
                string repassword = password = Request.Form["RePassword"];
                string repassword2 = password = Request.Form["RePassword2"];
                if (repassword != repassword2) ModelState.AddModelError("", "两次密码输入不一致！");

                item.Password = Get_MD5(repassword, "utf-8");
                string ip = home.GetRealIP();
                string address = home.GetAddressBaidu(ip);
                item.OperationRecord = item.OperationRecord + DateTime.Now + "修改了密码,操作地址：" + address + "(" + ip + ")";
                db.SaveChanges();
                return RedirectToAction("Index", "Staff");
            }
            ModelState.AddModelError("", "原密码密码错误！");
            return View();
        }
        public ActionResult Logoff()
        {
            Session.Clear();
            return RedirectToAction("Login", "Staff");
        }
        public ActionResult Index()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffIndex = 1;


            List<OR> or = DayOr(2);


            return View(or);
        }
        public ActionResult StaffData()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffData = 1;





            return View(staff);
        }
        public ActionResult Artist()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffEdit = 1;
            return View(staff);
        }
        public ActionResult ArtEdit(int id)
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            TempData["id"] = id;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            var art = db.Artist.FirstOrDefault(d => d.Id == id);
            TempData.Clear();

            ViewBag.StaffEdit = 1;
            return View(art);
        }
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult ArtEdit(Artist art)
        {
            Staff staffer = (Staff)Session["Staff"];
            Artist arts = db.Artist.FirstOrDefault(d => d.Id == art.Id);
            string nation = art.Nation;
            arts.Nation = nation;
            arts.ArtistNameCN = art.ArtistNameCN;
            arts.ArtistNameEN = art.ArtistNameEN;
            arts.City = art.City;
            arts.Title = art.Title;
            arts.Honor = art.Honor;
            arts.UserContent = art.UserContent;
            arts.Deeds = art.Deeds;
            arts.EndDate = DateTime.Now;
            if (TempData["TempImg"] != null)
            {
                string filename = TempData["TempImg"].ToString();
                string path = "/Content/ArtImg/Artist/";
                ImgChange(path, filename, arts.Path);
                arts.Path = filename;
            }
            if (TempData["TempFile"] != null)
            {
                string filename = TempData["TempFile"].ToString();
                string path = "/Content/File/";
                FileChange(path, filename, arts.FlieInf);
                art.FlieInf = filename;
            }
            Staff Staffer = db.Staff.FirstOrDefault(d => d.Id == staffer.Id);
            arts.EditRecord = arts.EditRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + staffer.StaffName + "修改了" + arts.ArtistNameCN + "的资料";
            Staffer.OperationRecord = Staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 修改了" + art.ArtistNameCN + "的条目。" + "/";

            var day = today();
            day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staffer.StaffName + "  修改了 " + art.ArtistNameCN + "的条目。" + "/";


            db.SaveChanges();
            return RedirectToAction("Artist", "Staff");

        }
        public ActionResult ArtAdd()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffEdit = 1;
            TempData.Clear();
            return View(staff);
        }
        //string CnName,string EnName,string Nation,string Ctiy,string Title,int sex,string Content
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult ArtAdd(FormCollection form)
        {
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            Staff staffer = (Staff)Session["staff"];


            var art = new Artist();

            art.AddDate = DateTime.Now;
            art.EndDate = DateTime.Now;
            art.ArtistNameCN = Request.Form["CnName"];
            art.ArtistNameEN = Request.Form["EnName"];
            art.Nation = Request.Form["Nation"];
            art.City = Request.Form["City"];
            art.Title = Request.Form["Title"];
            string sex = Request.Form["Sex"];
            if (sex == "1" && sex != "0")
            {
                art.Sex = "男";
            }
            else
            {
                art.Sex = "女";
            }
            art.Color1 = Request.Form["Color1"];
            art.Color2 = Request.Form["Color2"];
            art.Deeds = Request.Form["Deeds"];
            art.Introduction = Request.Form["Introduction"];
            art.Position = Request.Form["Position"];

            if (TempData["TempImg"] != null)
            {
                string filename = TempData["TempImg"].ToString();
                string path = "/Content/ArtImg/Artist/";
                ImgSave(path, filename);
                art.Path = filename;
            }
            if (TempData["TempFile"] != null)
            {
                string filename = TempData["TempFile"].ToString();
                string path = "/Content/File/";
                ImgSave(path, filename);
                art.FlieInf = filename;
            }

            art.EditRecord = staffer.StaffName + " 于 " + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 创建了此会员的条目。" + "/";

            Staff Staffer = db.Staff.FirstOrDefault(d => d.Id == staffer.Id);
            Staffer.OperationRecord = Staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 创建了" + art.ArtistNameCN + "的条目。" + "/";
            var day = today();
            day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staffer.StaffName + " 创建了 " + art.ArtistNameCN + "的条目。" + "/";

            db.Artist.Add(art);
            db.SaveChanges();


            return RedirectToAction("newArtist", "Staff");
        }
        public ActionResult newArtAdd()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffEdit = 1;
            TempData.Clear();
            return View(staff);
        }
        //string CnName,string EnName,string Nation,string Ctiy,string Title,int sex,string Content
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult newArtAdd(FormCollection form)
        {
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            Staff staffer = (Staff)Session["staff"];


            var art = new Artist();

            art.AddDate = DateTime.Now;
            art.EndDate = DateTime.Now;
            art.ArtistNameCN = Request.Form["CnName"];
            art.ArtistNameEN = Request.Form["EnName"];
            art.Nation = Request.Form["Nation"];
            art.City = Request.Form["City"];
            art.Title = Request.Form["Title"];
            string sex = Request.Form["Sex"];
            if (sex == "1" && sex != "0")
            {
                art.Sex = "男";
            }
            else
            {
                art.Sex = "女";
            }
            art.Color1 = Request.Form["Color1"];
            art.Color2 = Request.Form["Color2"];
            art.DeedsYears = Request.Form["DeedsYears"];
            art.DeedsThings = Request.Form["DeedsThings"];

            if (TempData["TempImg"] != null)
            {
                string filename = TempData["TempImg"].ToString();
                string path = "/Content/ArtImg/Artist/";
                ImgSave(path, filename);
                art.Path = filename;
            }
            if (TempData["TempFile"] != null)
            {
                string filename = TempData["TempFile"].ToString();
                string path = "/Content/File/";
                ImgSave(path, filename);
                art.FlieInf = filename;
            }

            art.EditRecord = staffer.StaffName + " 于 " + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 创建了此会员的条目。" + "/";

            Staff Staffer = db.Staff.FirstOrDefault(d => d.Id == staffer.Id);
            Staffer.OperationRecord = Staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 创建了" + art.ArtistNameCN + "的条目。" + "/";
            var day = today();
            day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staffer.StaffName + " 创建了 " + art.ArtistNameCN + "的条目。" + "/";

            db.Artist.Add(art);
            db.SaveChanges();


            return RedirectToAction("newArtist", "Staff");
        }

        public ActionResult newWorkAdd()
        {
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            return View(staff);
        }
        public ActionResult newArtFav(int? id)
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");

            ViewBag.Artistid = id;


            return View(staff);
        }
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult newArtFav(FormCollection form)
        {
            var staff = (Staff)Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");

            int aid = Convert.ToInt32(Request.Form["ArtistId"]);
            Artist art = db.Artist.FirstOrDefault(d => d.Id == aid);

            Fav fav = new Fav();
            fav.ArtistId = aid;
            fav.Title = Request.Form["Title"];
            fav.Year = Request.Form["Year"];
            fav.Location = Request.Form["Location"];


            if (TempData["TempImg"] != null)
            {
                string filename = TempData["TempImg"].ToString();
                string path = "/Content/ArtImg/Artist/Fav/";
                ImgSave(path, filename);
                fav.CoverPath = filename;
            }


            db.Fav.Add(fav);
            db.SaveChanges();

            // FavList视图待创建，直接套模板就ok
            return RedirectToAction("Fav", "Staff");
        }


        public ActionResult newExhibitionAdd()
        {
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            return View(staff);
        }
        public ActionResult newArtEdit()
        {
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            return View(staff);
        }

        public ActionResult newArtist()
        {
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            return View(staff);
        }

        public ActionResult newNewsAdd()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffEdit = 1;
            return View(staff);
        }
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult newNewsAdd(FormCollection form)
        {
            var staff = (Staff)Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");

            News news = new News();
            string path = "/Content/newsImg/";
            news.Date = DateTime.Now;
            string content = Request.Form["Content"];
            news.Content = content.Replace("TempFile", "newsImg");
            if (TempData["TempIntervalImg"] != null)
            {
                string imgtitle = TempData["TempIntervalImg"].ToString();
                news.ImgTitle = EffectiveImgs(imgtitle, content);
            }

            news.Autor = staff.StaffName;
            news.Title = Request.Form["Title"];
            news.Subtitle = Request.Form["Subtitle"];
            news.ReadCount = 0;
            string Link = Request.Form["Link"];
            if (Link == "" || Link == null)
            {
                news.original = true;
                news.link = null;
            }
            else
            {
                news.original = false;
                news.link = Link;
            }

            if (TempData["TempImg"] != null)
            {
                string filename = TempData["TempImg"].ToString();

                ImgSave(path, filename);
                news.CoverPath = filename;
            }

            news.EditRecord = staff.StaffName + " 于 " + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 编写了此新闻条目。" + "/";
            var day = today();
            day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staff.StaffName + " 编写了 " + news.Title + "的新闻条目。" + "/";

            Staff Staffer = db.Staff.FirstOrDefault(d => d.Id == staff.Id);
            Staffer.OperationRecord = Staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 编写了" + news.Title + "的新闻条目。" + "/";

            db.News.Add(news);
            db.SaveChanges();


            return RedirectToAction("News", "Staff");
        }


        public ActionResult ArtWorks(int id)
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffEdit = 1;

            return View(id);
        }
        public ActionResult Works()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffEdit = 1;

            return View(staff);
        }
        public ActionResult Work()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffEdit = 1;

            return View();
        }
        public ActionResult WorkEdit(int id)
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            TempData["id"] = id;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            var work = db.Works.FirstOrDefault(d => d.Id == id);
            var art = db.Artist.FirstOrDefault(d => d.Id == work.ArtistId);

            string tags = StrTags(work);
            ViewBag.Tags = tags;
            TempData["Tag"] = tags;



            ViewBag.art = art.ArtistNameCN;

            return View(work);
        }
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult WorkEdit(Works work, FormCollection form)
        {
            var staff = (Staff)Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            Works workn = db.Works.FirstOrDefault(d => d.Id == work.Id);
            workn.Title = work.Title;
            workn.Content = work.Content;
            workn.Deeds = work.Deeds;
            if (workn.Tags != work.Tags)
            {
                TagRefresh(workn, work.Tags);
                workn.Tags = work.Tags;
            }

            if (TempData["TempImg"] != null)
            {
                string filename = TempData["TempImg"].ToString();
                string path = "/Content/images/";
                ImgSave(path, filename);
                work.Path = filename;
            }
            string tags = Request.Form["Tag"];



            work.Record = work.Record + staff.StaffName + " 于 " + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 修改了此作品。" + "/";

            db.SaveChanges();

            return RedirectToAction("Works", "Staff");
        }
        public ActionResult WorkAdd(int? id)
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");

            ViewBag.Artistid = id;


            return View(staff);
        }
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult WorkAdd(FormCollection form)
        {
            var staff = (Staff)Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");

            int aid = Convert.ToInt32(Request.Form["ArtistId"]);
            Artist art = db.Artist.FirstOrDefault(d => d.Id == aid);
            art.WorkCount = art.WorkCount + 1;

            Works work = new Works();
            work.FirstDate = DateTime.Now;
            work.LastDate = DateTime.Now;
            work.ArtistId = aid;
            work.Content = Request.Form["Content"];
            work.Title = Request.Form["Title"];
            work.VisitCount = 0;
            work.Record = staff.StaffName + " 于 " + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 上传了此作品。" + "/";


            if (TempData["TempImg"] != null)
            {
                string filename = TempData["TempImg"].ToString();
                string path = "/Content/ArtImg/Artist/Works/";
                ImgSave(path, filename);
                work.Path = filename;

                workinf wk = imginf(path, filename);
                work.Height = wk.height;
                work.Width = wk.width;
                work.transverse = wk.tra;
                work.@long = wk.lon;
            }



            db.Works.Add(work);

            staff = db.Staff.FirstOrDefault(d => d.Id == staff.Id);
            staff.OperationRecord = staff.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + "上传了" + art.ArtistNameCN + "作品。" + work.Title + "/";

            var day = today();
            day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staff.StaffName + " 上传了 " + art.ArtistNameCN + "作品。" + work.Title + "/";

            db.SaveChanges();

            var tag = Request.Form["Tags"];
            if (tag != null && tag != "")
            {
                TagRefresh(work, tag);
                work.Tags = tag;
            }

            db.SaveChanges();

            return RedirectToAction("Works", "Staff");
        }



        public ActionResult Company()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffEdit = 1;




            return View(staff);
        }
        public ActionResult CompanyEdit(int id)
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            TempData["id"] = id;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");

            Company com = db.Company.FirstOrDefault(d => d.Id == id);


            ViewBag.StaffEdit = 1;
            return View(com);
        }
        [HttpPost]
        public ActionResult CompanyEdit(FormCollection form)
        {
            var staff = (Staff)Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            int id = Convert.ToInt32(Request.Form["Id"]);
            Company comn = db.Company.FirstOrDefault(d => d.Id == id);
            comn.NameCN = Request.Form["NameCN"];
            comn.NameEN = Request.Form["NameEN"];
            comn.CEO = Request.Form["CEO"];
            comn.Address = Request.Form["Address"];
            comn.Business = Request.Form["Business"];

            if (TempData["TempImg"] != null)
            {
                string filename = TempData["TempImg"].ToString();
                string path = "/Content/images/";
                ImgChange(path, filename, comn.ImgPath);
                comn.ImgPath = filename;
            }
            if (TempData["TempFile"] != null)
            {
                string filename = TempData["TempFile"].ToString();
                string path = "/Content/File/";
                FileChange(path, filename, comn.FilePath);
                comn.FilePath = filename;
            }


            comn.EditRecord = comn.EditRecord = staff.StaffName + " 于 " + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 修改了此会员企业条目。" + "/";

            var day = today();
            day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staff.StaffName + " 修改了 " + comn.NameCN + "会员企业条目。" + "/";

            Staff Staffer = db.Staff.FirstOrDefault(d => d.Id == staff.Id);
            Staffer.OperationRecord = Staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 修改了" + comn.NameCN + "会员企业条目。" + "/";

            db.SaveChanges();

            return RedirectToAction("Company", "Staff");
        }
        public ActionResult CompanyAdd()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");






            ViewBag.StaffEdit = 1;
            return View(staff);
        }
        [HttpPost]
        public ActionResult CompanyAdd(FormCollection form)
        {
            var staff = (Staff)Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            Company com = new Company();
            com.NameCN = Request.Form["NameCN"];
            com.NameEN = Request.Form["NameEN"];
            com.Address = Request.Form["Address"];
            com.Business = Request.Form["Business"];
            com.CEO = Request.Form["CEO"];
            com.FirstDate = DateTime.Now;
            com.LastDate = DateTime.Now;
            if (TempData["TempImg"] != null)
            {
                string filename = TempData["TempImg"].ToString();
                string path = "/Content/ArtImg/Company";
                ImgSave(path, filename);
                com.ImgPath = filename;
            }
            if (TempData["TempFile"] != null)
            {
                string filename = TempData["TempFile"].ToString();
                string path = "/Content/File/";
                ImgSave(path, filename);
                com.FilePath = filename;
            }

            com.VisitCount = 0;
            com.EditRecord = staff.StaffName + " 于 " + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 编写了此会员企业条目。" + "/";

            var day = today();
            day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staff.StaffName + " 编写了 " + com.NameCN + "会员企业条目。" + "/";

            Staff Staffer = db.Staff.FirstOrDefault(d => d.Id == staff.Id);
            Staffer.OperationRecord = Staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 编写了" + com.NameCN + "会员企业条目。" + "/";

            db.Company.Add(com);
            db.SaveChanges();

            return RedirectToAction("Company", "Staff");
        }

        public ActionResult News()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");





            ViewBag.StaffEdit = 1;
            return View(staff);
        }
        public ActionResult NewsEdit(int id)
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            TempData["id"] = id;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            var news = db.News.FirstOrDefault(d => d.Id == id);




            ViewBag.StaffEdit = 1;
            return View(news);
        }
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult NewsEdit(FormCollection form)
        {
            var staff = (Staff)Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            string path = "/Content/newsImg/";
            int Id = Convert.ToInt32(Request.Form["Id"]);
            News news = db.News.FirstOrDefault(d => d.Id == Id);
            news.Title = Request.Form["Title"];
            news.Subtitle = Request.Form["Subtitle"];
            string content = Request.Form["Content"];
            news.Content = content.Replace("TempFile", "newsImg");
            string imgtitle;
            if (TempData["TempIntervalImg"] != null)
            {
                imgtitle = TempData["TempIntervalImg"].ToString();
            }
            else
            {
                imgtitle = "";
            }

            string titles = str2t1(news.ImgTitle, imgtitle, news.Content);
            ImgsSave(path, titles);

            string link = Request.Form["link"];
            if (link == "" || link == null)
            {
                news.original = true;
                news.link = null;
            }
            else
            {
                news.original = false;
                news.link = link;
            }

            if (TempData["TempImg"] != null)
            {
                string filename = TempData["TempImg"].ToString();
                ImgChange(path, filename, news.CoverPath);
                news.CoverPath = filename;
            }

            news.EditRecord = news.EditRecord + staff.StaffName + " 于 " + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 修改了此新闻条目。" + "/";
            var day = today();
            day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staff.StaffName + "  修改了 " + news.Title + "的新闻条目。" + "/";
            Staff Staffer = db.Staff.FirstOrDefault(d => d.Id == staff.Id);
            Staffer.OperationRecord = Staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 修改了" + news.Title + "的新闻条目。" + "/";

            db.SaveChanges();

            return RedirectToAction("News", "Staff");
        }
        public ActionResult NewsAdd()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.StaffEdit = 1;
            return View(staff);
        }
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult NewsAdd(FormCollection form)
        {
            var staff = (Staff)Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");

            News news = new News();
            string path = "/Content/newsImg/";
            news.Date = DateTime.Now;
            string content = Request.Form["Content"];
            news.Content = content.Replace("TempFile", "newsImg");
            if (TempData["TempIntervalImg"] != null)
            {
                string imgtitle = TempData["TempIntervalImg"].ToString();
                news.ImgTitle = EffectiveImgs(imgtitle, content);
            }

            news.Autor = staff.StaffName;
            news.Title = Request.Form["Title"];
            news.Subtitle = Request.Form["Subtitle"];
            news.ReadCount = 0;
            string Link = Request.Form["Link"];
            if (Link == "" || Link == null)
            {
                news.original = true;
                news.link = null;
            }
            else
            {
                news.original = false;
                news.link = Link;
            }

            if (TempData["TempImg"] != null)
            {
                string filename = TempData["TempImg"].ToString();

                ImgSave(path, filename);
                news.CoverPath = filename;
            }

            news.EditRecord = staff.StaffName + " 于 " + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 编写了此新闻条目。" + "/";
            var day = today();
            day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staff.StaffName + " 编写了 " + news.Title + "的新闻条目。" + "/";

            Staff Staffer = db.Staff.FirstOrDefault(d => d.Id == staff.Id);
            Staffer.OperationRecord = Staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 编写了" + news.Title + "的新闻条目。" + "/";

            db.News.Add(news);
            db.SaveChanges();


            return RedirectToAction("News", "Staff");
        }


        //已下为调用方法

        //将图片保存并且删除临时文件
        //将图片从临时文件转入相对应的储存文件
        /// <summary>
        /// 将图片从临时文件转入相对应的储存文件.
        /// </summary>
        /// <param name="path">储存路径</param>
        /// <param name="filename">临时文件的文件名</param>
        private void ImgSave(string path, string filename)
        {

            string pa1 = path + filename;
            string pa2 = "/Content/TempFile/" + filename;
            var imgpath = Path.Combine(Server.MapPath(pa1));
            string temppath = Path.Combine(Server.MapPath(pa2));
            if (System.IO.File.Exists(temppath))
            {
                System.IO.File.Copy(temppath, imgpath, true);
                System.IO.File.Delete(temppath);
            }

        }
        /// <summary>
        /// 多张图片从临时文件转入相对应的储存文件.
        /// </summary>
        /// <param name="path">储存位置</param>
        /// <param name="filenames">用“，”隔开的文件名</param>
        private void ImgsSave(string path, string filenames)
        {
            string[] filename = filenames.Split(',');
            for (int a = 0; a < filename.Length; a++)
            {
                string pa1 = path + filename[a];
                string pa2 = "/Content/TempFile/" + filename[a];
                var imgpath = Path.Combine(Server.MapPath(pa1));
                string temppath = Path.Combine(Server.MapPath(pa2));
                if (System.IO.File.Exists(temppath))
                {
                    System.IO.File.Copy(temppath, imgpath, true);
                    System.IO.File.Delete(temppath);
                }

            }

        }
        private void ImgChange(string path, string NewFilename, string OldFilename)
        {
            string pa1 = path + NewFilename;
            string pa2 = "~/Content/TempFile/" + NewFilename;
            string pa3 = path + OldFilename;
            var imgpath = Path.Combine(Server.MapPath(pa1));
            string temppath = Path.Combine(Server.MapPath(pa2));
            string temppath2 = Path.Combine(Server.MapPath(pa3));
            if (System.IO.File.Exists(temppath))
            {
                System.IO.File.Copy(temppath, imgpath, true);
                System.IO.File.Delete(temppath);
            }
            if (System.IO.File.Exists(temppath2))
            {
                System.IO.File.Delete(temppath2);
            }
        }
        private void FileDelete(string path, string filename)
        {
            string pa = path + filename;
            var filepath = Path.Combine(Server.MapPath(pa));
            if (System.IO.File.Exists(filepath))
            {
                System.IO.File.Delete(filepath);
            }

        }
        private void FileChange(string path, string filename, string oldfilename)
        {
            string path2 = "/Content/TempFile/";
            string pa = path + oldfilename;
            string pa2 = path + filename;
            string pa3 = path2 + filename;//临时文件储存
            string filepath = Path.Combine(Server.MapPath(pa));
            string filepath2 = Path.Combine(Server.MapPath(pa2));
            string filepath3 = Path.Combine(Server.MapPath(pa3));

            if (System.IO.File.Exists(filepath))//删除原文件
            {
                System.IO.File.Delete(filepath);
            }
            if (System.IO.File.Exists(filepath3))
            {
                System.IO.File.Copy(filepath3, filepath2, true);
                System.IO.File.Delete(filepath3);
            }
        }
        private void FilesDelete(string path, string filenames)
        {
            if (filenames != null)
            {
                if (filenames.IndexOf(",") >= 1)
                {
                    string[] filename = filenames.Split(',');
                    for (int a = 0; a < filename.Length; a++)
                    {
                        string pa = path + filename;
                        var filepath = Path.Combine(Server.MapPath(pa));
                        if (System.IO.File.Exists(filepath))
                        {
                            System.IO.File.Delete(filepath);
                        }
                    }
                }
                else
                {
                    FileDelete(path, filenames);
                }
            }
        }
        private void TempDelete(string filename)
        {
            string path = "/Content/TempFile/";
            string pa1 = path + filename;
            var imgpath = Path.Combine(Server.MapPath(pa1));
            if (System.IO.File.Exists(imgpath))
            {
                System.IO.File.Delete(imgpath);
            }

        }

        private void TagRefresh(Works work, string tag)
        {
            int id = work.Id;
            string oldtags = work.Tags;
            if (tag.IndexOf(" ") > 0)
            {
                string[] tags = tag.Split(' ');
                for (int a = 0; a < tags.Length; a++)
                {
                    if (tags[a] != "")
                    {
                        string ta = tags[a];
                        Tag t = db.Tag.FirstOrDefault(d => d.Title == ta);
                        if (t == null)
                        {
                            t = new Tag();
                            t.Title = tags[a];
                            t.WorksCount = 0;
                            t.SerachHot = 0;
                            db.Tag.Add(t);
                            db.SaveChanges();
                        }
                        int tid = t.Id;
                        WorksTag wt = db.WorksTag.FirstOrDefault(d => d.TagId == tid && d.WorkId == id);
                        if (wt == null)
                        {
                            wt = new WorksTag();
                            wt.TagId = tid;
                            wt.WorkId = id;
                            t.WorksCount = t.WorksCount + 1;
                            db.WorksTag.Add(wt);
                            db.SaveChanges();
                        }
                    }
                }
            }
            else
            {
                Tag t = db.Tag.FirstOrDefault(d => d.Title == tag);
                if (t == null)
                {
                    t = new Tag();
                    t.Title = tag;
                    t.WorksCount = 0;
                    t.SerachHot = 0;
                    db.Tag.Add(t);
                    db.SaveChanges();
                }
                int tid = t.Id;
                WorksTag wt = db.WorksTag.FirstOrDefault(d => d.TagId == tid && d.WorkId == id);
                if (wt == null)
                {
                    wt = new WorksTag();
                    wt.TagId = tid;
                    wt.WorkId = id;
                    t.WorksCount = t.WorksCount + 1;
                    db.WorksTag.Add(wt);
                    db.SaveChanges();
                }
            }

            if (oldtags != null)
            {
                if (oldtags.IndexOf(" ") > 0)
                {
                    if (tag.IndexOf(" ") > 0)
                    {//旧多新多
                        string[] otags = oldtags.Split(' ');
                        List<string> ost = new List<string>();
                        for (int a = 0; a < otags.Length; a++)
                        {
                            string ot = otags[a];
                            if (!tag.Contains(ot))
                            {
                                ost.Add(ot);
                            }
                        }

                        foreach (string t in ost)
                        {
                            DeleteWorkTag(t, work);
                        }
                    }
                    else
                    {//旧多新单
                        string otagss = oldtags.Trim();
                        string[] otags = oldtags.Split(' ');

                        for (int a = 0; a < otags.Length; a++)
                        {
                            if (otags[a] != tag)
                            {
                                DeleteWorkTag(otags[a], work);
                            }
                        }

                    }
                }
                else
                {
                    if (tag.IndexOf(" ") > 0)
                    {//旧单新多
                        if (!tag.Contains(oldtags))
                        {
                            DeleteWorkTag(oldtags, work);
                        }
                    }
                    else
                    {//旧单新单
                        oldtags = oldtags.Trim();
                        tag = tag.Trim();
                        if (oldtags != tag)
                        {
                            DeleteWorkTag(oldtags, work);
                        }
                    }
                }

            }

        }
        private void DeleteWorkTag(string tag, Works work)
        {
            Tag ot = db.Tag.FirstOrDefault(d => d.Title == tag);
            WorksTag wt = db.WorksTag.FirstOrDefault(d => d.TagId == ot.Id && d.WorkId == work.Id);
            if (wt != null)
            {
                db.WorksTag.Remove(wt);
                ot.WorksCount = ot.WorksCount - 1;
                db.SaveChanges();
            }
        }
        private string StrTags(Works work)
        {
            List<WorksTag> wts = db.WorksTag.Where(d => d.WorkId == work.Id).ToList();
            if (wts.Count > 0)
            {
                List<Tag> tag = new List<Tag>();
                foreach (var w in wts)
                {
                    Tag t = db.Tag.FirstOrDefault(d => d.Id == w.TagId);
                    tag.Add(t);
                }
                if (tag.Count > 1)
                {
                    string tags = "";
                    foreach (var t in tag)
                    {
                        tags = tags + t.Title + "";
                    }
                    tags = tags.Substring(0, tags.Length - 1);
                    return tags;
                }
                else
                {
                    return tag[0].Title;

                }

            }
            return null;
        }

        private workinf imginf(string path, string filmename)
        {
            int width;
            int height;
            int size = 250;
            workinf wi = new workinf();
            string pa = path + filmename;
            var imgpath = Path.Combine(Server.MapPath(pa));
            WebImage image = new WebImage(imgpath);
            width = image.Width;
            height = image.Height;

            if (width >= height)
            {
                wi.tra = true;
            }
            else
            {
                wi.tra = false;
            }
            if (width >= height * 1.5 || height >= width * 1.5)
            {
                wi.lon = true;
            }
            else
            {
                wi.lon = false;
            }
            wi.width = width;
            wi.height = height;

            if (!wi.lon)
            {
                if (height > width)
                {
                    width = (size * width) / height;
                    height = size;
                }
                else
                {
                    height = (size * height) / width;
                    width = size;
                }
            }
            else
            {
                if (height > width)
                {
                    height = (size * height) / width;
                    width = size;
                }
                else
                {
                    width = (size * width) / height;
                    height = size;
                }

            }
            string format = filmename.Split('.')[1];

            image.Resize(width, height, true, true);
            image.Save("~/Content/ArtImg/Artist/works-narrow/" + filmename, format, true);

            return wi;
        }

        private void WorksImgDelete(List<Works> works)
        {
            string path = "/Content/ArtImg/Artist/Works/";
            if (works.Count > 0)
            {
                foreach (var a in works)
                {
                    FileDelete(path, a.Path);
                }
            }

        }
        private static string Get_MD5(string strSource, string sEncode)
        {
            //new 
            System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            //获取密文字节数组
            byte[] bytResult = md5.ComputeHash(System.Text.Encoding.GetEncoding(sEncode).GetBytes(strSource));
            //转换成字符串，并取9到25位 
            //string strResult = BitConverter.ToString(bytResult, 4, 8);  
            //转换成字符串，32位 
            string strResult = BitConverter.ToString(bytResult);
            //BitConverter转换出来的字符串会在每个字符中间产生一个分隔符，需要去除掉 
            strResult = strResult.Replace("-", "");
            return strResult.ToLower();
        }
        public string str2t1(string str1, string str2, string content)
        {
            List<string> str = new List<string>();
            str1 = str1 + "," + str2;
            string[] strs = str1.Split(',');
            for (int a = 0; a < strs.Length; a++)
            {
                if (content.IndexOf(strs[a]) > 1 && strs[a] != "")
                {
                    if (str.Count > 0)
                    {
                        if (str.Exists(d => d != strs[a]))
                        {
                            str.Add(strs[a]);
                        }
                    }
                    else
                    {
                        str.Add(strs[a]);
                    }
                }
                else
                {
                    string path = "/Content/TempFile/";
                    string path2 = "/Content/newsImg/";
                    FileDelete(path, strs[a]);
                    FileDelete(path2, strs[a]);
                }
            }

            string titles = String.Join(",", str);

            return titles;
        }
        private string EffectiveImgs(string imgstitle, string content)
        {
            string titles = "";
            string path = "/Content/newsImg/";
            if (imgstitle.IndexOf(",") > 1)
            {
                string[] imgtitles = imgstitle.Split(',');

                for (int a = 0; a < imgtitles.Length; a++)
                {
                    if (content.IndexOf(imgtitles[a]) > 1)
                    {
                        ImgSave(path, imgtitles[a]);

                        titles = titles + imgtitles[a] + ",";
                    }
                    else
                    {
                        TempDelete(imgtitles[a]);
                    }
                }
                return titles;
            }
            else
            {
                if (content.IndexOf(imgstitle) > 1)
                {
                    ImgSave(path, imgstitle);
                    titles = imgstitle;
                    return titles;
                }
                else
                {
                    TempDelete(imgstitle);
                    return null;
                }

            }
        }

        /// <summary>
        /// 获取当天的日志
        /// </summary>
        /// <returns></returns>
        private Diary today()
        {
            DateTime da = DateTime.Now.Date;
            Diary d;
            d = db.Diary.FirstOrDefault(a => a.Date == da);
            if (d == null)
            {
                d = new Diary();
                d.Date = DateTime.Now.Date;
                d.VistiTotal = 0;
                db.Diary.Add(d);
                db.SaveChanges();
            }

            return d;
        }
        /// <summary>
        /// 艺术家数据列表分页处理
        /// </summary>
        /// <param name="art"></param>
        /// <param name="page">当前页数</param>
        /// <param name="limit">显示个数</param>
        /// <param name="count">总个数</param>
        /// <returns></returns>
        private List<Artist> PageArtist(List<Artist> Art, int page, int limit, int count)
        {
            int fint = (page - 1) * limit;
            int eint = page * limit - 1;
            int m = count % limit;
            if (count < limit)//判断一页是否能显示完
            {
                eint = count;
            }
            else
            {
                if (m > 0 && (count - m) / limit < page)//
                {
                    eint = count;
                }
            }
            List<Artist> art = new List<Artist>();
            art = Art.SkipWhile((d, Index) => Index < fint).ToList();//确认开始位置
            if ((count - m) / limit > page) art = art.TakeWhile((d, Index) => Index > eint - fint).ToList();//确认结尾位置

            foreach (var a in art)
            {
                string[] r = a.EditRecord.Split('/');
                int c = r.Length;
                a.EditRecord = r[c - 2];
            }

            return art;
        }
        /// <summary>
        /// 新闻数据列表分页处理
        /// </summary>
        /// <param name="art"></param>
        /// <param name="page">当前页数</param>
        /// <param name="limit">显示个数</param>
        /// <param name="count">总个数</param>
        /// <returns></returns>
        private List<NewsViews> PageNews(List<News> News, int page, int limit, int count)
        {
            List<NewsViews> nv = new List<NewsViews>();

            int fint = (page - 1) * limit;
            int eint = page * limit;

            News = News.Take(eint).Skip(fint).ToList();

            foreach (var n in News)
            {
                NewsViews v = new NewsViews();
                v.Id = n.Id;
                v.Title = n.Title;
                v.Subtitle = n.Subtitle;
                v.link = n.link;
                v.Autor = n.Autor;
                DateTime? d = n.Date;
                v.Date = Convert.ToDateTime(d).ToString("yyyy年MM月dd日 HH:mm:ss");
                v.ReadCount = n.ReadCount;
                nv.Add(v);

            }


            return nv;
        }
        private List<WorksView> PageWork(List<Works> Works, int page, int limit, int count)
        {
            List<WorksView> wv = new List<WorksView>();

            int fint = (page - 1) * limit;
            int eint = page * limit;


            Works = Works.Take(eint).Skip(fint).ToList();


            foreach (var n in Works)
            {
                WorksView v = new WorksView();
                v.Id = n.Id;
                v.Title = n.Title;
                v.ArtistId = n.ArtistId;
                v.ArtisName = db.Artist.FirstOrDefault(a => a.Id == n.ArtistId).ArtistNameCN;
                DateTime? l = n.LastDate;
                DateTime? f = n.FirstDate;
                v.LDate = Convert.ToDateTime(l).ToString("yyyy年MM月dd日 HH:mm:ss");
                v.FDate = Convert.ToDateTime(f).ToString("yyyy年MM月dd日 HH:mm:ss");
                v.Tags = n.Tags;
                v.visitcount = Convert.ToInt32(n.VisitCount);
                string[] str = n.Record.Split('/');
                v.Record = str[str.Length - 2];
                wv.Add(v);

            }


            return wv;
        }
        private List<CompaniesView> PageCompany(List<Company> com, int page, int limit, int count)
        {
            List<CompaniesView> cv = new List<CompaniesView>();

            int fint = (page - 1) * limit;
            int eint = page * limit - 1;
            int m = count % limit;
            if (count < limit)
            {
                eint = count;
            }
            else
            {
                if (m > 0 && (count - m) / limit < page)
                {
                    eint = count;
                }
            }
            List<Company> coms = new List<Company>();
            coms = com.SkipWhile((d, Index) => Index < fint).ToList();
            if ((count - m) / limit >= page) coms = coms.TakeWhile((d, Index) => Index > eint - fint).ToList();

            foreach (var n in coms)
            {
                CompaniesView v = new CompaniesView();
                v.Id = n.Id;
                v.NameCN = n.NameCN;
                v.NameEN = n.NameEN;
                v.Business = n.Business;
                v.CEO = n.CEO;
                DateTime? l = n.LastDate;
                DateTime? f = n.FirstDate;
                v.LDate = Convert.ToDateTime(l).ToString("yyyy年MM月dd日 HH:mm:ss");
                v.FDate = Convert.ToDateTime(f).ToString("yyyy年MM月dd日 HH:mm:ss");
                v.visitcount = n.VisitCount;
                cv.Add(v);
            }
            return cv;
        }
        public List<OR> or(string OR)
        {
            if (OR == null || OR == "") return null;
            string[] ors = OR.Split('/');
            List<OR> or = new List<OR>();
            for (int a = ors.Length - 1; a >= 0; a--)
            {
                if (ors[a] != "")
                {
                    if (or.Count == 0)
                    {
                        OR o = new OR();
                        o.or = new List<string>();
                        string[] str = ors[a].Split('日');
                        o.date = str[0] + "日";
                        string r = str[1];
                        o.or.Add(r);
                        or.Add(o);
                    }
                    else
                    {
                        string[] str = ors[a].Split('日');
                        string date = str[0] + "日";
                        if (or.Exists(d => d.date == date))
                        {
                            OR o = or.FirstOrDefault(d => d.date == date);
                            string r = str[1];
                            o.or.Add(r);

                        }
                        else
                        {
                            OR o = new OR();
                            o.or = new List<string>();
                            o.date = str[0] + "日";
                            string r = str[1];
                            o.or.Add(r);
                            or.Add(o);
                        }
                    }
                }

            }

            return or;
        }

        /// <summary>
        /// 默认返回七天的操作记录
        /// </summary>
        /// <param name="days"></param>
        /// <returns></returns>
        public List<OR> DayOr(int? Days)
        {
            int day;
            List<OR> or = new List<OR>();
            DateTime now = DateTime.Now.Date;
            if (Days != null)
            {
                day = Convert.ToInt32(Days);
            }
            else
            {
                day = 7;
            }
            for (int a = day; a > -1; a--)
            {
                DateTime n = DateTime.Now;
                n = n.AddDays(-a);
                var d = db.Diary.FirstOrDefault(b => b.Date == n);
                OR o = new OR();
                o.date = n.ToString("yyyy年MM月dd日");
                if (d != null)
                {
                    if (d.OperationRecord != null && d.OperationRecord != "")
                    {
                        string[] sd = d.OperationRecord.Split('/');
                        o.or = new List<string>(sd);
                    }
                    else
                    {
                        o.or = new List<string>();
                        o.or.Add("当天无操作记录");
                    }
                }
                else
                {
                    o.or = new List<string>();
                    o.or.Add("当天无操作记录");
                }
                or.Add(o);
            }


            return or;

        }






        //已下为Json视图丢调用方法
        public JsonResult worksdata(int page, int limit)
        {
            if (Session["Staff"] == null) return null;
            List<Works> works = db.Works.ToList();
            var work = PageWork(works, page, limit, works.Count);
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    code = 0,
                    count = works.Count,
                    data = work,
                },
            };
        }
        public JsonResult atrtis(int page, int limit)
        {
            if (Session["Staff"] == null) return null;

            var Art = db.Artist.OrderBy(d => d.Id).ToList();

            int count = Art.Count();
            var atr = PageArtist(Art, page, limit, count);
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    code = 0,
                    msg = "",
                    count = count,
                    data = atr,
                },
            };
        }
        public JsonResult companysdata(int page, int limit)
        {
            if (Session["Staff"] == null) return null;
            var Companies = db.Company.ToList();
            int count = Companies.Count();
            var company = PageCompany(Companies, page, limit, count);
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    code = 0,
                    count = count,
                    data = company,
                },
            };
        }
        public JsonResult datanews(int page, int limit)
        {
            if (Session["Staff"] == null) return null;
            var News = db.News.ToList();
            int count = News.Count();
            var news = PageNews(News, page, limit, count);
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    code = 0,
                    msg = "",
                    count = count,
                    data = news,
                }
            };
        }


        public JsonResult TempImgSave(HttpPostedFileBase file)
        {
            int msg = 0;
            string filename = "null";
            var supportedTypes = new[] { "jpg", "jpeg", "png", "gif", "bmp", "JPG", "JPEG", "PNG", "GIF", "BMP" };
            var fileExt = Path.GetExtension(file.FileName).Substring(1);
            if (!supportedTypes.Contains(fileExt))
            {
                msg = 1;
            }
            if (file.ContentLength > 1024 * 1000 * 20)
            {
                msg = 2;
            }
            if (msg == 0)
            {
                if (TempData["TempImg"] != null)
                {
                    string f = TempData["TempImg"].ToString();
                    TempDelete(f);
                }
                Random r = new Random();
                string firstname = DateTime.Now.ToString("yyyyMMddHHmmss") + r.Next(1000000);
                firstname = Get_MD5(firstname, "utf-8");
                filename = firstname + "." + fileExt;
                var filepath = Path.Combine(Server.MapPath("/Content/TempFile"), filename);
                file.SaveAs(filepath);
                TempData["TempImg"] = filename;
            }
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    code = 0,
                    msg = msg,
                    files = new
                    {
                        file = filename,
                    },

                }
            };
        }

        public JsonResult TempFilmeSave(HttpPostedFileBase file)
        {
            int msg = 0;
            string filename = file.FileName;
            var filepath = Path.Combine(Server.MapPath("/Content/TempFile"), filename);
            file.SaveAs(filepath);
            TempData["TempFile"] = filename;
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    code = 0,
                    msg = msg,
                    files = new
                    {
                        file = filename,
                    },

                }
            };
        }
        public JsonResult TempImgsSave(HttpPostedFileBase[] files)
        {
            List<string> filenames = new List<string>();
            List<string> erro = new List<string>();
            foreach (var file in files)
            {
                int msg = 0;
                var supportedTypes = new[] { "jpg", "jpeg", "png", "gif", "bmp", "JPG", "JPEG", "PNG", "GIF", "BMP" };
                var fileExt = Path.GetExtension(file.FileName).Substring(1);
                if (!supportedTypes.Contains(fileExt))
                {
                    string e = "文件" + file.FileName + "的格式错误";
                    erro.Add(e);
                    msg = 1;
                }
                if (file.ContentLength > 1024 * 1000 * 20)
                {
                    string e = "文件" + file.FileName + "文件大小超过了20M";
                    erro.Add(e);
                    msg = 2;
                }
                if (msg == 0)
                {
                    Random r = new Random();
                    string filename = DateTime.Now.ToString("yyyyMMddHHmmss") + r.Next(10000) + "." + fileExt;
                    var filepath = Path.Combine(Server.MapPath("/Content/TempFile"), filename);
                    file.SaveAs(filepath);
                    filenames.Add(filename);
                }
            }
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    msg = erro,
                    file = filenames,
                }
            };
        }
        [HttpPost]
        public JsonResult TempImgIntervalSave(HttpPostedFileBase file)
        {
            string msg = "0";
            int code = 0;
            string filename = "null";
            var supportedTypes = new[] { "jpg", "jpeg", "png", "gif", "bmp", "JPG", "JPEG", "PNG", "GIF", "BMP" };
            var fileExt = Path.GetExtension(file.FileName).Substring(1);
            if (!supportedTypes.Contains(fileExt))
            {
                msg = "图片格式不对";
                code = 1;
            }
            if (file.ContentLength > 1024 * 1000 * 20)
            {
                msg = "图片过大，请勿超过20M";
                code = 1;
            }
            if (msg == "0")
            {
                Random r = new Random();
                string firstname = DateTime.Now.ToString("yyyyMMddHHmmss") + r.Next(1000000);
                firstname = Get_MD5(firstname, "utf-8");
                filename = firstname + "." + fileExt;
                var filepath = Path.Combine(Server.MapPath("/Content/TempFile"), filename);
                file.SaveAs(filepath);
                if (TempData["TempIntervalImg"] != null)
                {
                    string files = TempData["TempIntervalImg"].ToString();
                    TempData["TempIntervalImg"] = files + "," + filename;
                }
                else
                {
                    TempData["TempIntervalImg"] = filename;
                }

            }
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    code = code,//0表示成功，其它失败  
                    msg = msg, //提示信息 //一般上传失败后返回  
                    data = new
                    {
                        src = "/Content/TempFile/" + filename,
                        title = filename  //可选
                    }
                }
            };

        }
        public JsonResult diary(int day)
        {
            if (Session["Staff"] == null) return null;
            DateTime today = DateTime.Now.Date;


            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {


                },
            };

        }
        /// <summary>
        /// 月访问数据
        /// </summary>
        /// <returns></returns>
        public JsonResult visitcityM()
        {
            if (Session["Staff"] == null) return null;
            var fa = new List<father>();
            DateTime nm = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);//获取当前月份
            List<AccessData> ads = db.AccessData.Where(d => d.Date > nm).ToList();
            foreach (var a in ads)
            {
                string porvince = a.Porvince;
                string city = a.Ctiy;
                if (city != null && city != "" && city != "None")
                {
                    if (fa.Count == 0)
                    {
                        father ad = new father();
                        ad.children = new List<children>();
                        children c = new children();
                        ad.name = a.Porvince;
                        ad.value = 1;
                        c.name = a.Ctiy;
                        c.value = 1;
                        ad.children.Add(c);
                        fa.Add(ad);
                    }
                    else
                    if (fa.FirstOrDefault(d => d.name == porvince) == null)
                    {
                        father ad = new father();
                        ad.children = new List<children>();
                        children c = new children();
                        ad.name = a.Porvince;
                        ad.value = 1;
                        c.name = city;
                        c.value = 1;
                        ad.children.Add(c);
                        fa.Add(ad);
                    }
                    else
                    {
                        father ad = fa.FirstOrDefault(d => d.name == porvince);
                        if (ad.children.FirstOrDefault(d => d.name == city) == null)
                        {
                            children c = new children();
                            c.name = city;
                            ad.value = ad.value + 1;
                            c.value = 1;
                            ad.children.Add(c);
                        }
                        else
                        {
                            children c = ad.children.FirstOrDefault(d => d.name == city);
                            c.value = c.value + 1;
                            ad.value = ad.value + 1;
                        }
                    }
                }
            }

            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    count = fa.Count,
                    data = fa,
                },
            };
        }
        /// <summary>
        /// 年访问数据
        /// </summary>
        /// <returns></returns>
        public JsonResult visitcityY()
        {
            if (Session["Staff"] == null) return null;
            var fa = new List<father>();
            DateTime nm = new DateTime(DateTime.Now.Year, 1, 1);//获取当前月份
            List<AccessData> ads = db.AccessData.Where(d => d.Date > nm).ToList();

            foreach (var a in ads)
            {
                string porvince = a.Porvince;
                string city = a.Ctiy;
                if (city != null && city != "" && city != "None")
                {
                    if (fa.Count == 0)
                    {
                        father ad = new father();
                        ad.children = new List<children>();
                        children c = new children();
                        ad.name = a.Porvince;
                        ad.value = 1;
                        c.name = a.Ctiy;
                        c.value = 1;
                        ad.children.Add(c);
                        fa.Add(ad);
                    }
                    else
                    if (fa.FirstOrDefault(d => d.name == porvince) == null)
                    {
                        father ad = new father();
                        ad.children = new List<children>();
                        children c = new children();
                        ad.name = a.Porvince;
                        ad.value = 1;
                        c.name = city;
                        c.value = 1;
                        ad.children.Add(c);
                        fa.Add(ad);
                    }
                    else
                    {
                        father ad = fa.FirstOrDefault(d => d.name == porvince);
                        if (ad.children.FirstOrDefault(d => d.name == city) == null)
                        {
                            children c = new children();
                            c.name = city;
                            ad.value = ad.value + 1;
                            c.value = 1;
                            ad.children.Add(c);
                        }
                        else
                        {
                            children c = ad.children.FirstOrDefault(d => d.name == city);
                            c.value = c.value + 1;
                            ad.value = ad.value + 1;
                        }
                    }
                }



            }


            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    count = fa.Count,
                    data = fa,
                },
            };
        }

        public JsonResult ViustiySource(string type)
        {
            DateTime time;
            if (type == "Year")
            {
                time = new DateTime(DateTime.Now.Year, 1, 1);
            }
            else if (type == "Month")
            {
                time = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            }
            else
            {
                time = DateTime.Now;
            }
            var cs = new List<children>();
            List<AccessData> ads = db.AccessData.Where(d => d.Date > time).ToList();
            foreach (var a in ads)
            {
                string city = a.Ctiy;
                if (city != null && city != "" && city != "None")
                {
                    if (cs.Count == 0)
                    {
                        children c = new children();
                        c.name = city;
                        c.value = 1;
                        cs.Add(c);
                    }
                    else
                    {
                        var c = cs.FirstOrDefault(d => d.name == city);
                        if (c == null)
                        {
                            c = new children();
                            c.name = city;
                            c.value = 1;
                            cs.Add(c);
                        }
                        else
                        {
                            c.value = c.value + 1;
                        }
                    }

                }
            }

            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    data = cs,
                }
            };
        }


        /// <summary>
        /// 反回任意时间段的访问数据
        /// </summary>
        /// <param name="FY">起始年份param>
        /// <param name="FM">起始月份</param>
        /// <param name="LY">终止年份</param>
        /// <param name="LM">终止月份</param>
        /// <returns></returns>
        public JsonResult visitcity(int FY, int FM, int LY, int LM)
        {
            if (Session["Staff"] == null) return null;
            var vd = new List<father>();

            DateTime fdate = new DateTime(FY, FM, 1);
            DateTime ladate = new DateTime(LY, LM, 1);

            List<AccessData> ads = db.AccessData.Where(d => d.Date > ladate && d.Date < fdate).ToList();


            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    count = vd.Count,
                    data = vd,
                },
            };

        }
        /// <summary>
        /// 当日的管理员操作记录
        /// </summary>
        /// <returns></returns>
        public JsonResult DieryTodayRecord()
        {
            if (Session["Staff"] == null) return null;
            var day = today();
            string[] Record;
            if (day.OperationRecord == null && day.OperationRecord == "")
            {
                Record = new string[] { "今天暂无数据操作记录" };
            }
            else
            {
                Record = day.OperationRecord.Split('/');
            }
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    data = Record
                }
            };
        }
        /// <summary>
        /// 返回近期的访问人数，默认为30天
        /// </summary>
        /// <param name="day"></param>
        /// <returns></returns>
        public JsonResult DayVisityCount(int? day)
        {
            if (day == null || day == 0) day = 30;
            int Day = Convert.ToInt32(day);
            var vcs = new List<VisityCount>();
            DateTime now = DateTime.Now.Date;
            int[] counts = new int[Day];
            string[] date = new string[Day];
            for (int a = 1; a < (Day + 1); a++)
            {
                VisityCount v = new VisityCount();
                DateTime d = DateTime.Now.Date.AddDays(-(Day - a));
                var diery = db.Diary.FirstOrDefault(y => y.Date == d);
                v.date = d.ToString("MM.dd");
                if (diery == null)
                {
                    v.count = 0;
                }
                else
                {
                    v.count = Convert.ToInt32(diery.VistiTotal);
                }
                vcs.Add(v);
            }

            for (int a = 0; a < day; a++)
            {
                counts[a] = vcs[a].count;
                date[a] = vcs[a].date;
            }
            return new JsonResult()
            {

                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    category = date,
                    data = counts
                }
            };

        }
        public JsonResult DataCount()
        {
            List<children> counts = new List<children>();
            string[] tag = new string[] { "访问作品", "访问会员", "访问企业", "新闻阅读数" };
            int w = Convert.ToInt32(db.Works.Sum(d => d.VisitCount));
            int a = Convert.ToInt32(db.Artist.Sum(d => d.VisitCount));
            int c = Convert.ToInt32(db.Company.Sum(d => d.VisitCount));
            int n = Convert.ToInt32(db.News.Sum(d => d.ReadCount));
            int[] count = new int[] { w, a, c, n };
            for (int z = 0; z < tag.Length; z++)
            {
                children vc = new children();
                vc.name = tag[z];
                vc.value = count[z];
                counts.Add(vc);
            }

            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    category = tag,
                    data = counts
                }
            };
        }


        public JsonResult ArtDelete(int aid)
        {
            int msg = 0;
            var staff = Session["Staff"];
            if (staff != null)
            {
                Staff staffer = (Staff)Session["Staff"];
                Artist art = db.Artist.FirstOrDefault(d => d.Id == aid);
                staffer = db.Staff.FirstOrDefault(d => d.Id == staffer.Id);
                staffer.OperationRecord = staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 删除了" + art.ArtistNameCN + "(id:" + art.Id + ")" + "的艺术家条目。" + "/";
                var day = today();
                day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString(" HH:mm:ss ") + staffer.StaffName + " 删除了" + art.ArtistNameCN + "(id:" + art.Id + ")" + "的艺术家条目。" + "/";
                var works = db.Works.Where(d => d.ArtistId == aid).ToList();
                string path = "/Content/ArtImg/Artist/";
                string path2 = "/Content/Filme/";
                FileDelete(path, art.Path);
                FileDelete(path2, art.FlieInf);
                WorksImgDelete(works);
                db.Works.RemoveRange(works);
                db.Artist.Remove(art);
                db.SaveChanges();
                msg = 1;


            }
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    data = msg
                }
            };
        }
        public JsonResult NewsDelete(int nid)
        {
            int msg = 0;
            var staff = Session["Staff"];
            if (staff != null)
            {
                Staff staffer = (Staff)Session["Staff"];
                News news = db.News.FirstOrDefault(d => d.Id == nid);
                staffer = db.Staff.FirstOrDefault(d => d.Id == staffer.Id);
                staffer.OperationRecord = staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 删除了" + news.Title + "(id:" + news.Id + ")" + "的新闻条目。" + "/";
                var day = today();
                day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staffer.StaffName + " 删除了" + news.Title + "(id:" + news.Id + ")" + "的新闻条目。" + "/";
                string path = "/Content/newsImg/";
                FileDelete(path, news.CoverPath);
                FilesDelete(path, news.ImgTitle);
                db.News.Remove(news);
                db.SaveChanges();
            }
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    data = msg
                }
            };
        }
        public JsonResult WorkDelete(int id)
        {
            int msg = 0;
            var staff = Session["Staff"];
            if (staff != null)
            {
                Staff staffer = (Staff)Session["Staff"];

                Works work = db.Works.FirstOrDefault(d => d.Id == id);
                Artist art = db.Artist.FirstOrDefault(d => d.Id == work.ArtistId);
                art.WorkCount = art.WorkCount - 1;
                staffer = db.Staff.FirstOrDefault(d => d.Id == staffer.Id);
                staffer.OperationRecord = staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 删除了" + work.Title + "(id:" + work.Id + ")" + "的企业条目。" + "/";
                var day = today();
                day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staffer.StaffName + " 删除了 " + work.Title + "(id:" + work.Id + ")" + "的企业条目。" + "/";

                string path = "/Content/ArtImg/Artist/Works/";
                FileDelete(path, work.Path);
                db.Works.Remove(work);
                db.SaveChanges();
            }

            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    data = msg
                }
            };
        }
        public JsonResult CompanyDelete(int cid)
        {
            int msg = 0;
            var staff = Session["Staff"];
            if (staff != null)
            {
                Staff staffer = (Staff)Session["Staff"];
                Company com = db.Company.FirstOrDefault(d => d.Id == cid);
                staffer = db.Staff.FirstOrDefault(d => d.Id == staffer.Id);
                staffer.OperationRecord = staffer.OperationRecord + (DateTime.Now).ToString("yyyy年MM月dd日 HH:mm:ss") + " 删除了" + com.NameCN + "(id:" + com.Id + ")" + "的企业条目。" + "/";
                var day = today();
                day.OperationRecord = day.OperationRecord + (DateTime.Now).ToString("HH:mm:ss") + staffer.StaffName + " 删除了" + com.NameCN + "(id:" + com.Id + ")" + "的企业条目。" + "/";
                string path = "/Content/ArtImg/Company/";
                string path2 = "/Content/Filme/";
                FileDelete(path, com.ImgPath);
                FileDelete(path2, com.FilePath);
                db.Company.Remove(com);
                db.SaveChanges();

            }
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    data = msg
                }
            };
        }

        public JsonResult ArtTOF(int id)
        {
            string name = null;
            var art = db.Artist.FirstOrDefault(d => d.Id == id);
            if (art != null)
            {
                name = art.ArtistNameCN;

            }
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    data = name,
                }
            };
        }
        public JsonResult zbcs(string city)
        {
            string msg = zuobiao(city);
            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    data = msg
                }
            };
        }




        //以下为调用静态数据
        public string zuobiao(string city)
        {
            var cn = CNLAL();
            string inf = cn.FirstOrDefault(d => d.IndexOf(city) > 1);
            if (inf != null)
            {
                string[] Inf = inf.Split(':');
                return Inf[1];
            }
            return null;
        }
        public static List<string> CNLAL()
        {
            List<string> cn = new List<string> {
                "北京市:116.413384,39.910925",
"天津市:117.209523,39.093668",
"河北省-石家庄市:114.521532,38.048312",
"河北省-唐山市:118.186459,39.636584",
"河北省-秦皇岛市:119.608531,39.941748",
"河北省-邯郸市:114.545628,36.631263",
"河北省-邢台市:114.511462,37.076686",
"河北省-保定市:115.471464,38.879988",
"河北省-张家口市:114.892572,40.773237",
"河北省-承德市:117.969398,40.957856",
"河北省-沧州市:116.845581,38.310215",
"河北省-廊坊市:116.690582,39.543367",
"河北省-衡水市:115.675406,37.745191",
"河北省-定州市:114.996496,38.522199",
"河北省-辛集市:115.224451,37.949309",
"山西省-太原市:112.556391,37.876989",
"山西省-大同市:113.306436,40.082469",
"山西省-阳泉市:113.587617,37.862361",
"山西省-长治市:113.122559,36.201268",
"山西省-晋城市:112.858578,35.496285",
"山西省-朔州市:112.439371,39.337108",
"山西省-晋中市:112.759595,37.692839",
"山西省-运城市:111.013389,35.032707",
"山西省-忻州市:112.740624,38.422383",
"山西省-临汾市:111.52553,36.093742",
"山西省-吕梁市:111.15045,37.524498",
"内蒙古自治区-呼和浩特市:111.755509,40.848423",
"内蒙古自治区-包头市:109.846544,40.662929",
"内蒙古自治区-乌海市:106.800391,39.662006",
"内蒙古自治区-赤峰市:118.89552,42.261686",
"内蒙古自治区-通辽市:122.250522,43.65798",
"内蒙古自治区-鄂尔多斯市:109.787443,39.614482",
"内蒙古自治区-呼伦贝尔市:119.77237,49.218446",
"内蒙古自治区-巴彦淖尔市:107.394398,40.749359",
"内蒙古自治区-乌兰察布市:113.139468,41.000748",
"内蒙古自治区-兴安盟:122.044365,46.088464",
"内蒙古自治区-锡林郭勒盟:116.054391,43.939423",
"内蒙古自治区-阿拉善盟:105.735377,38.858276",
"辽宁省-沈阳市:123.466452,41.68879",
"辽宁省-大连市:121.621631,38.918954",
"辽宁省-鞍山市:123.001373,41.115054",
"辽宁省-抚顺市:123.964375,41.88597",
"辽宁省-本溪市:123.692507,41.492916",
"辽宁省-丹东市:124.361547,40.006409",
"辽宁省-锦州市:121.132596,41.100931",
"辽宁省-营口市:122.241575,40.673137",
"辽宁省-阜新市:121.676408,42.028022",
"辽宁省-辽阳市:123.243366,41.274161",
"辽宁省-盘锦市:122.07749,41.125875",
"辽宁省-铁岭市:123.732365,42.229948",
"辽宁省-朝阳市:120.457499,41.579821",
"辽宁省-葫芦岛市:120.843398,40.717364",
"吉林省-长春市:125.330602,43.821954",
"吉林省-吉林市:126.555635,43.843568",
"吉林省-四平市:124.356482,43.171994",
"吉林省-辽源市:125.150425,42.894055",
"吉林省-通化市:125.946606,41.733816",
"吉林省-白山市:126.42963,41.939627",
"吉林省-松原市:124.831482,45.147404",
"吉林省-白城市:122.845591,45.625504",
"吉林省-延边朝鲜族自治州:129.477376,42.915743",
"黑龙江省-哈尔滨市:126.541615,45.808826",
"黑龙江省-齐齐哈尔市:123.924571,47.359977",
"黑龙江省-鸡西市:130.975619,45.300872",
"黑龙江省-鹤岗市:130.304433,47.356056",
"黑龙江省-双鸭山市:131.165342,46.653186",
"黑龙江省-大庆市:125.108658,46.593633",
"黑龙江省-伊春市:128.847546,47.733318",
"黑龙江省-佳木斯市:130.327359,46.80569",
"黑龙江省-七台河市:131.011545,45.7763",
"黑龙江省-牡丹江市:129.63954,44.556246",
"黑龙江省-黑河市:127.53549,50.251272",
"黑龙江省-绥化市:126.975357,46.660032",
"黑龙江省-大兴安岭地区:123.644559,52.510947",
"上海市:121.480539,31.235929",
"江苏省-南京市:118.802422,32.064653",
"江苏省-无锡市:120.318583,31.49881",
"江苏省-徐州市:117.290575,34.212667",
"江苏省-常州市:119.981485,31.815796",
"江苏省-苏州市:120.592412,31.303564",
"江苏省-南通市:120.901592,31.986549",
"江苏省-连云港市:119.228621,34.60225",
"江苏省-淮安市:119.021484,33.616295",
"江苏省-盐城市:120.167544,33.355101",
"江苏省-扬州市:119.419419,32.400677",
"江苏省-镇江市:119.430489,32.194716",
"江苏省-泰州市:119.929566,32.460675",
"江苏省-宿迁市:118.281574,33.96775",
"浙江省-杭州市:120.215512,30.253083",
"浙江省-宁波市:121.628572,29.866033",
"浙江省-温州市:120.706477,28.001085",
"浙江省-嘉兴市:120.763552,30.750975",
"浙江省-湖州市:120.094517,30.898964",
"浙江省-绍兴市:120.585478,30.036369",
"浙江省-金华市:119.653436,29.084639",
"浙江省-衢州市:118.866597,28.975546",
"浙江省-舟山市:122.213556,29.990912",
"浙江省-台州市:121.427435,28.662194",
"浙江省-丽水市:119.929573,28.473278",
"安徽省-合肥市:117.233443,31.826578",
"安徽省-芜湖市:118.439431,31.358537",
"安徽省-蚌埠市:117.395513,32.921524",
"安徽省-淮南市:117.006389,32.631847",
"安徽省-马鞍山市:118.51358,31.676266",
"安徽省-淮北市:116.804537,33.961656",
"安徽省-铜陵市:117.818477,30.951233",
"安徽省-安庆市:117.063604,30.530957",
"安徽省-黄山市:118.345437,29.72189",
"安徽省-滁州市:118.339406,32.261271",
"安徽省-阜阳市:115.820436,32.896061",
"安徽省-宿州市:116.970544,33.652095",
"安徽省-六安市:116.52641,31.741451",
"安徽省-亳州市:115.784463,33.850643",
"安徽省-池州市:117.498421,30.670884",
"安徽省-宣城市:118.765534,30.946602",
"福建省-福州市:119.30347,26.080429",
"福建省-厦门市:118.096435,24.485407",
"福建省-莆田市:119.014521,25.459865",
"福建省-三明市:117.645521,26.269737",
"福建省-泉州市:118.682446,24.879952",
"福建省-漳州市:117.653576,24.51893",
"福建省-南平市:118.0595,27.292158",
"福建省-龙岩市:117.023448,25.08122",
"福建省-宁德市:119.554511,26.672242",
"江西省-南昌市:115.864589,28.689455",
"江西省-景德镇市:117.184576,29.274248",
"江西省-萍乡市:113.861496,27.628393",
"江西省-九江市:116.007535,29.711341",
"江西省-新余市:114.923535,27.823579",
"江西省-鹰潭市:117.075575,28.265787",
"江西省-赣州市:114.940503,25.835176",
"江西省-吉安市:115.000511,27.119727",
"江西省-宜春市:114.423564,27.820856",
"江西省-抚州市:116.364539,27.954892",
"江西省-上饶市:117.94946,28.460626",
"山东省-济南市:117.126399,36.656554",
"山东省-青岛市:120.389455,36.072227",
"山东省-淄博市:118.061453,36.819086",
"山东省-枣庄市:117.330542,34.815994",
"山东省-东营市:118.681385,37.439642",
"山东省-烟台市:121.454415,37.470038",
"山东省-潍坊市:119.168378,36.712652",
"山东省-济宁市:116.593612,35.420177",
"山东省-泰安市:117.094495,36.205858",
"山东省-威海市:122.127541,37.516431",
"山东省-日照市:119.533415,35.422839",
"山东省-莱芜市:117.68355,36.219472",
"山东省-临沂市:118.363533,35.110671",
"山东省-德州市:116.365557,37.441308",
"山东省-聊城市:115.991588,36.462758",
"山东省-滨州市:117.977404,37.388196",
"山东省-菏泽市:115.487545,35.239407",
"河南省-郑州市:113.631419,34.753439",
"河南省-开封市:114.314593,34.802886",
"河南省-洛阳市:112.459421,34.624263",
"河南省-平顶山市:113.199529,33.772051",
"河南省-安阳市:114.3995,36.105941",
"河南省-鹤壁市:114.303594,35.752357",
"河南省-新乡市:113.9336,35.30964",
"河南省-焦作市:113.248548,35.220963",
"河南省-濮阳市:115.035597,35.767593",
"河南省-许昌市:113.858476,34.041432",
"河南省-漯河市:114.023421,33.587711",
"河南省-三门峡市:111.206533,34.778327",
"河南省-南阳市:112.534501,32.996562",
"河南省-商丘市:115.662449,34.420202",
"河南省-信阳市:114.097483,32.153015",
"河南省-周口市:114.703483,33.631829",
"河南省-驻马店市:114.028471,33.017842",
"河南省-济源市:112.608581,35.072907",
"湖北省-武汉市:114.311582,30.598467",
"湖北省-黄石市:115.045533,30.205208",
"湖北省-十堰市:110.80453,32.635062",
"湖北省-宜昌市:111.292549,30.697446",
"湖北省-襄阳市:112.128537,32.014797",
"湖北省-鄂州市:114.901607,30.396572",
"湖北省-荆门市:112.206393,31.041733",
"湖北省-孝感市:113.92251,30.930689",
"湖北省-荆州市:112.245523,30.340842",
"湖北省-黄冈市:114.87849,30.459359",
"湖北省-咸宁市:114.328519,29.847056",
"湖北省-随州市:113.38945,31.696517",
"湖北省-恩施土家族苗族自治州:109.494593,30.27794",
"湖北省-仙桃市:113.461591,30.368272",
"湖北省-潜江市:112.905474,30.408358",
"湖北省-天门市:113.172409,30.669622",
"湖北省-神农架林区:110.682525,31.750496",
"湖南省-长沙市:112.945473,28.234889",
"湖南省-株洲市:113.140471,27.833568",
"湖南省-湘潭市:112.950464,27.835702",
"湖南省-衡阳市:112.578447,26.899576",
"湖南省-邵阳市:111.474433,27.24527",
"湖南省-岳阳市:113.135489,29.363178",
"湖南省-常德市:111.705452,29.03775",
"湖南省-张家界市:110.485533,29.122816",
"湖南省-益阳市:112.361516,28.559711",
"湖南省-郴州市:113.02146,25.776683",
"湖南省-永州市:111.619455,26.425864",
"湖南省-怀化市:110.008514,27.575161",
"湖南省-娄底市:112.001503,27.703209",
"湖南省-湘西土家族苗族自治州:109.745577,28.317369",
"广东省-广州市:113.271431,23.135336",
"广东省-韶关市:113.603527,24.815881",
"广东省-深圳市:114.064552,22.548457",
"广东省-珠海市:113.582555,22.276565",
"广东省-汕头市:116.688529,23.359092",
"广东省-佛山市:113.128512,23.027759",
"广东省-江门市:113.088556,22.584604",
"广东省-湛江市:110.365554,21.276723",
"广东省-茂名市:110.931543,21.669064",
"广东省-肇庆市:112.471489,23.052889",
"广东省-惠州市:114.423558,23.116359",
"广东省-梅州市:116.129537,24.294178",
"广东省-汕尾市:115.381553,22.791263",
"广东省-河源市:114.707446,23.749684",
"广东省-阳江市:111.988489,21.86434",
"广东省-清远市:113.062468,23.68823",
"广东省-东莞市:113.75842,23.027308",
"广东省-中山市:113.399422,22.522315",
"广东省-潮州市:116.62947,23.662623",
"广东省-揭阳市:116.378512,23.55574",
"广东省-云浮市:112.051513,22.920912",
"广西壮族自治区:108.334521,22.821269",
"广西壮族自治区-南宁市:108.373451,22.822607",
"广西壮族自治区-柳州市:109.434422,24.331961",
"广西壮族自治区-桂林市:110.203545,25.242886",
"广西壮族自治区-梧州市:111.285517,23.482745",
"广西壮族自治区-北海市:109.126533,21.486836",
"广西壮族自治区-防城港市:108.360419,21.693005",
"广西壮族自治区-钦州市:108.66058,21.986594",
"广西壮族自治区-贵港市:109.60552,23.117448",
"广西壮族自治区-玉林市:110.188453,22.659831",
"广西壮族自治区-百色市:106.624589,23.908186",
"广西壮族自治区-贺州市:111.573526,24.409451",
"广西壮族自治区-河池市:108.0915,24.698912",
"广西壮族自治区-来宾市:109.227458,23.756547",
"广西壮族自治区-崇左市:107.37152,22.383117",
"海南省-海口市:110.325525,20.044049",
"海南省-三亚市:109.518557,18.258736",
"海南省-三沙市:111.673087,16.497085",
"海南省-儋州市:109.587456,19.527146",
"海南省-五指山市:109.52354,18.780994",
"海南省-琼海市:110.480545,19.264254",
"海南省-文昌市:110.804509,19.549062",
"海南省-万宁市:110.399434,18.800107",
"海南省-东方市:108.658567,19.101105",
"海南省-定安县:110.365533,19.68712",
"海南省-屯昌县:110.108577,19.357375",
"海南省-澄迈县:110.013511,19.744349",
"海南省-临高县:109.697443,19.919475",
"海南省-白沙黎族自治县:109.457471,19.231379",
"海南省-昌江黎族自治县:109.062464,19.303998",
"海南省-乐东黎族自治县:109.180508,18.755871",
"海南省-陵水黎族自治县:110.044464,18.512332",
"海南省-保亭黎族苗族自治县:109.703482,18.64691",
"海南省-琼中黎族苗族自治县:109.844511,19.039164",
"重庆市:106.558434,29.568996",
"四川省-成都市:104.081534,30.655822",
"四川省-自贡市:104.784449,29.345585",
"四川省-攀枝花市:101.725541,26.588033",
"四川省-泸州市:105.448524,28.877668",
"四川省-德阳市:104.404419,31.133115",
"四川省-绵阳市:104.685562,31.473663",
"四川省-广元市:105.850423,32.441616",
"四川省-遂宁市:105.599422,30.539098",
"四川省-内江市:105.064588,29.585887",
"四川省-乐山市:103.772538,29.557941",
"四川省-南充市:106.117503,30.843783",
"四川省-眉山市:103.856563,30.082526",
"四川省-宜宾市:104.649404,28.758007",
"四川省-广安市:106.639553,30.461746",
"四川省-达州市:107.474594,31.214308",
"四川省-雅安市:103.049543,30.016793",
"四川省-巴中市:106.751585,31.872889",
"四川省-资阳市:104.634435,30.134957",
"四川省-阿坝藏族羌族自治州:102.231415,31.905512",
"四川省-甘孜藏族自治州:101.968547,30.055279",
"四川省-凉山彝族自治州:102.273503,27.887752",
"贵州省-贵阳市:106.714476,26.60403",
"贵州省-六盘水市:104.837555,26.598833",
"贵州省-遵义市:106.933428,27.731701",
"贵州省-安顺市:105.954417,26.259252",
"贵州省-毕节市:105.311581,27.304095",
"贵州省-铜仁市:109.187435,27.696773",
"贵州省-黔西南布依族苗族自治州:104.912492,25.093967",
"贵州省-黔东南苗族侗族自治州:107.989446,26.589703",
"贵州省-黔南布依族苗族自治州:107.528403,26.260616",
"云南省-昆明市:102.852448,24.873998",
"云南省-曲靖市:103.802435,25.496407",
"云南省-玉溪市:102.55356,24.357711",
"云南省-保山市:99.177273,25.139039",
"云南省-昭通市:103.723512,27.344084",
"云南省-丽江市:100.232465,26.860657",
"云南省-普洱市:100.97257,22.830979",
"云南省-临沧市:100.09544,23.890469",
"云南省-楚雄彝族自治州:101.534412,25.051774",
"云南省-红河哈尼族彝族自治州:103.381549,23.369996",
"云南省-文山壮族苗族自治州:104.222569,23.405994",
"云南省-西双版纳傣族自治州:100.803447,22.013601",
"云南省-大理白族自治州:100.274583,25.612128",
"云南省-德宏傣族景颇族自治州:98.591359,24.438011",
"云南省-怒江傈僳族自治州:98.863288,25.823707",
"云南省-迪庆藏族自治州:99.70953,27.825185",
"西藏自治区-拉萨市:91.120824,29.65004",
"西藏自治区-日喀则市:88.902952,29.255583",
"西藏自治区-昌都市:97.186654,31.144249",
"西藏自治区-林芝市:94.368058,29.654042",
"西藏自治区-山南市:91.778675,29.243027",
"西藏自治区-那曲地区:92.057338,31.482438",
"陕西省-西安市:108.946466,34.347269",
"陕西省-铜川市:108.952404,34.902637",
"陕西省-宝鸡市:107.244575,34.368916",
"陕西省-咸阳市:108.715422,34.335476",
"陕西省-渭南市:109.51659,34.505716",
"陕西省-延安市:109.496582,36.591111",
"陕西省-汉中市:107.02943,33.0738",
"陕西省-榆林市:109.741616,38.290884",
"陕西省-安康市:109.035601,32.690513",
"陕西省-商洛市:109.924418,33.878634",
"甘肃省-兰州市:103.840521,36.067235",
"甘肃省-嘉峪关市:98.296204,39.77796",
"甘肃省-金昌市:102.194606,38.52582",
"甘肃省-白银市:104.144451,36.550825",
"甘肃省-天水市:105.731417,34.587412",
"甘肃省-武威市:102.644554,37.934378",
"甘肃省-张掖市:100.456411,38.932066",
"甘肃省-平凉市:106.671442,35.549232",
"甘肃省-酒泉市:98.500685,39.738469",
"甘肃省-庆阳市:107.649386,35.715216",
"甘肃省-定西市:104.63242,35.586833",
"甘肃省-陇南市:104.928575,33.40662",
"甘肃省-临夏回族自治州:103.216391,35.607562",
"甘肃省-甘南藏族自治州:102.917585,34.98914",
"青海省-西宁市:101.78445,36.623385",
"青海省-海东市:102.110444,36.508511",
"青海省-海北藏族自治州:100.907434,36.960663",
"青海省-黄南藏族自治州:102.022428,35.525805",
"青海省-海南藏族自治州:100.626621,36.292102",
"青海省-果洛藏族自治州:100.251592,34.477194",
"青海省-玉树藏族自治州:97.013181,33.01098",
"青海省-海西蒙古族藏族自治州:97.376299,37.38275",
"宁夏回族自治区:106.265605,38.476878",
"宁夏回族自治区-银川市:106.238494,38.49246",
"宁夏回族自治区-石嘴山市:106.3906,38.989683",
"宁夏回族自治区-吴忠市:106.205371,38.003713",
"宁夏回族自治区-固原市:106.248577,36.021617",
"宁夏回族自治区-中卫市:105.203571,37.505701",
"新疆维吾尔自治区-乌鲁木齐市:87.62444,43.830763",
"新疆维吾尔自治区-克拉玛依市:84.895901,45.585675",
"新疆维吾尔自治区-吐鲁番市:89.192459,42.948549",
"新疆维吾尔自治区-哈密市:93.521308,42.832856",
"新疆维吾尔自治区-昌吉回族自治州:87.315002,44.016854",
"新疆维吾尔自治区-博尔塔拉蒙古自治州:82.072915,44.912196",
"新疆维吾尔自治区-巴音郭楞蒙古自治州:86.151714,41.770287",
"新疆维吾尔自治区-阿克苏地区:80.266943,41.17503",
"新疆维吾尔自治区-克孜勒苏柯尔克孜自治州:76.174309,39.720471",
"新疆维吾尔自治区-喀什地区:75.996391,39.476097",
"新疆维吾尔自治区-和田地区:79.928507,37.120446",
"新疆维吾尔自治区-伊犁哈萨克自治州:81.330538,43.922723",
"新疆维吾尔自治区-塔城地区:82.987236,46.750948",
"新疆维吾尔自治区-阿勒泰地区:88.147926,47.850728",
"新疆维吾尔自治区-石河子市:86.086886,44.311976",
"新疆维吾尔自治区-阿拉尔市:81.287354,40.553264",
"新疆维吾尔自治区-图木舒克市:79.075616,39.871209",
"新疆维吾尔自治区-五家渠市:87.549937,44.172445",
"新疆维吾尔自治区-铁门关市:86.181494,41.732373",
"香港特别行政区:114.173825,22.337784",
"澳门特别行政区:113.560161,22.214787",
            };
            return cn;
        }
    }
}


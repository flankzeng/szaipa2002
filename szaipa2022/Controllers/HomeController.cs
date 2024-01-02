
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Szaipa.Models;
using szaipa2022.Models;

namespace Szaipa.Controllers
{
    public class HomeController : Controller
    {
        SzaipaEntities db = new SzaipaEntities();
        // GET: Home
        public ActionResult Index()
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据\
            if (Session["Staff"] != null) ViewBag.staff = 1;
            ViewBag.Index = 1;
            var N = db.News.OrderByDescending(d => d.Date).ToList();
            var news = new List<News>();
            for (int a = 0; a < 4; a++)
            {
                news.Add(N[a]);
            }
            var l = newslist(news);
            ViewBag.n = l;
            return View();
        }
        public ActionResult newIndex()
        {
            if (Session["Home"] == null) HaveViti();

            if (Session["Staff"] != null) ViewBag.staff = 1;

            ViewBag.Index = 1;
            var N = db.News.OrderByDescending(d => d.Date).ToList();

            var news = new List<News>();
            for (int a = 0; a < 6; a++)
            {
                news.Add(N[a]);
            }

            var l = newslist(news);

            foreach (var n in l)
            {
                if (n.Subtitle.Length > 90)
                {
                    n.Subtitle = n.Subtitle.Substring(0, 90) + "...";
                }
            }

            ViewBag.n = l;

            return View();
        }

        public ActionResult All(string keyword)
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            return View();
        }
        public ActionResult Tags(string keyword)
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            var tm = Toworks(keyword);
            ViewBag.nulltags = tm.nulltags;
            ViewBag.tags = tm.tags;
            if (tm.works.Count > 0)
            {
                ViewBag.works = ToVieWorks(tm.works);
                ViewBag.count = tm.works.Count;
            }


            return View();
        }


        public ActionResult news(int? page, int? limit, int? count)
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;
            //var news = newslist(page, limit, count);
            var news = db.News.ToList();
            var ns = newslist(news);


            ViewBag.news = 1;
            return View(ns);
        }

        public ActionResult newnews(int? page, int? limit, int? count, string keyword)
        {
            if (Session["Home"] == null) HaveViti(); // 是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;

            var news = db.News.OrderByDescending(d => d.Date).ToList();
            var ns = newslist(news);

            var newsList = db.News.OrderByDescending(d => d.Date).ToList();
            ViewBag.NewsList = newsList;

            ViewBag.news = 1;
            return View(ns);
        }
        public ActionResult vip()
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;

            var art = db.Artist.OrderByDescending(d => d.Id).ToList();

            ViewBag.vip = 1;
            return View(art);
        }

        public ActionResult newvip()
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;

            var art = db.Artist.OrderByDescending(d => d.Id).ToList();

            ViewBag.vip = 1;
            return View(art);
        }

        public ActionResult vip_debi()
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;



            ViewBag.vip = 1;
            return View();
        }
        public ActionResult newvip_debi()
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;



            ViewBag.vip = 1;
            return View();
        }


        public ActionResult about()
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;

            ViewBag.about = 1;
            return View();
        }

        public ActionResult newabout()
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;

            ViewBag.about = 1;
            return View();
        }

        public ActionResult membership()
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;


            ViewBag.membership = 1;
            return View();
        }

        public ActionResult newmembership()
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;


            ViewBag.membership = 1;
            return View();
        }


        public ActionResult link()
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;
            ViewBag.link = 1;
            return View();
        }

        public ActionResult newlink()
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (Session["Staff"] != null) ViewBag.staff = 1;
            ViewBag.link = 1;
            return View();
        }


        public ActionResult Art(int id)
        {
            if (Session["Staff"] != null) ViewBag.staff = 1;
            if (Session["Home"] == null) HaveViti();
            var art = db.Artist.FirstOrDefault(d => d.Id == id);
            var work = db.Works.Where(d => d.ArtistId == art.Id).ToList();
            ViewBag.work = work.Take(8);


            if (art.Honor != null && art.Honor.IndexOf("/") > 0)


                art.VisitCount = art.VisitCount + 1;
            var day = today();
            day.ArtVisit = day.ArtVisit + 1;
            db.SaveChanges();

            ViewBag.vip = 1;

            return View(art);
        }

        public ActionResult newArt(int id)
        {
            if (Session["Staff"] != null) ViewBag.staff = 1;
            if (Session["Home"] == null) HaveViti();
            var art = db.Artist.FirstOrDefault(d => d.Id == id);
            var work = db.Works.Where(d => d.ArtistId == art.Id).ToList();
            ViewBag.work = work;


            if (art.Honor != null && art.Honor.IndexOf("/") > 0)


                art.VisitCount = art.VisitCount + 1;
            var day = today();
            day.ArtVisit = day.ArtVisit + 1;
            db.SaveChanges();

            ViewBag.vip = 1;

            return View(art);
        }

        public ActionResult ArtNews(int id)
        {
            if (Session["Staff"] != null) ViewBag.staff = 1;
            if (Session["Home"] == null) HaveViti();
            // 获取特定的 ArtNews
            var art = db.Artist.FirstOrDefault(d => d.Id == id);
            var artNews = db.ArtNews.FirstOrDefault(n => n.Id == id);

            // 获取相关的Fav
            ViewBag.Fav = db.Fav.Where(f => f.ArtistId == art.Id);

            // 1.为视图创建头条新闻：
            var heading = db.ArtNews.Where(d => d.ArtistId == art.Id).ToList().Take(1);
            ViewBag.Heading = heading;

            // 2.为视图创建2～5条新闻的列表：
            var info = db.ArtNews.Where(n => n.ArtistId == art.Id)
                          .Take(5)
                          .ToList();
            ViewBag.Info = info;

            // 3.赋予视图所有的新闻列表功能：
            var selfUpload = db.ArtNews.Where(d => d.ArtistId == art.Id).ToList();
            ViewBag.selfUpload = selfUpload;

            // 创建 ArtNewsViewModel 的实例
            var viewModel = new ArtNewsViewModel
            {
                ArtNews = artNews,
                Artist = art
            };

            // 将 ViewModel 传递给视图
            return View(viewModel);
        }
        // public ActionResult ArtNewsRead(int id)
        // {
        //     if (Session["Staff"] != null) ViewBag.staff = 1;
        //     if (Session["Home"] == null) HaveViti();
        //     var art = db.Artist.FirstOrDefault(d => d.Id == id);
        //     var artNews = db.ArtNews.FirstOrDefault(d => d.Id == id);

        //     var day = today();
        //     db.SaveChanges();

        //     return View(artNews);
        // }

        public ActionResult ArtNewsRead(int id)
        {
            if (Session["Staff"] != null) ViewBag.staff = 1;
            if (Session["Home"] == null) HaveViti();

            // 获取特定的 ArtNews
            var artNews = db.ArtNews.FirstOrDefault(n => n.Id == id);

            // 获取相关的 Artist
            var artist = db.Artist.FirstOrDefault(a => a.Id == artNews.ArtistId);

            // 创建 ArtNewsViewModel 的实例
            var viewModel = new ArtNewsViewModel
            {
                ArtNews = artNews,
                Artist = artist
            };

            // 将 ViewModel 传递给视图
            return View(viewModel);
        }

        public ActionResult artistZLQ()
        {
            return View();
        }

        public ActionResult artistNews()
        {
            return View();
        }

        public ActionResult artistNewsRead()
        {
            return View();
        }

        public ActionResult newsread(int id)
        {
            if (Session["Staff"] != null) ViewBag.staff = 1;
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            ViewBag.news = 1;
            var news = db.News.FirstOrDefault(d => d.Id == id);
            news.ReadCount = news.ReadCount + 1;
            var day = today();
            day.NewsVisit = day.NewsVisit + 1;
            db.SaveChanges();


            return View(news);
        }

        public ActionResult newnewsread(int id)
        {
            if (Session["Staff"] != null) ViewBag.staff = 1;
            if (Session["Home"] == null) HaveViti(); // 是否是有效访问，若是记录数据
            ViewBag.news = 1;

            var news = db.News.FirstOrDefault(d => d.Id == id);
            news.ReadCount = news.ReadCount + 1;
            var day = today();
            day.NewsVisit = day.NewsVisit + 1;
            db.SaveChanges();

            var newsList = db.News.OrderByDescending(d => d.Date).Take(5).ToList();

            ViewBag.NewsList = newsList; // 将newsList传递给视图

            return View(news);
        }

        public ActionResult Search(string keyword)
        {
            // 调用适当的方法从数据库或其他数据源获取newnews文章的数据，并根据关键字进行筛选
            // 可以使用LINQ或其他合适的方法进行搜索和筛选，然后将搜索结果传递给cshtml页面进行显示

            // 示例：从数据库获取newnews文章数据，并根据关键字筛选
            var articles = db.News.Where(n => n.Title.Contains(keyword) || n.Content.Contains(keyword))
                                  .OrderByDescending(n => n.Date)
                                  .ToList();

            return View("newnewsread", articles);
        }

        public ActionResult listworks(int? id)
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            if (id != null)
            {
                int ID = Convert.ToInt32(id);
                var works = db.Works.Where(d => d.ArtistId == id).ToList();

            }
            else
            {


            }

            return View();
        }
        public ActionResult work(int id)
        {
            if (Session["Staff"] != null) ViewBag.staff = 1;
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据

            var work = db.Works.FirstOrDefault(d => d.Id == id);
            var day = today();
            work.VisitCount = work.VisitCount + 1;
            day.WorksVisit = day.WorksVisit + 1;
            var tags = WorksTags(work);
            if (tags.Count > 0)
            {
                ViewBag.Tags = tags;
            }
            var works = otherwork(work);
            ViewBag.works = works;


            db.SaveChanges();

            ViewBag.news = 1;
            return View(work);
        }

        public ActionResult swork(int id)
        {
            if (Session["Staff"] != null) ViewBag.staff = 1;
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据

            var work = db.Works.FirstOrDefault(d => d.Id == id);
            var day = today();
            work.VisitCount = work.VisitCount + 1;
            day.WorksVisit = day.WorksVisit + 1;
            var tags = WorksTags(work);
            if (tags.Count > 0)
            {
                ViewBag.Tags = tags;
            }
            var works = otherwork(work);
            ViewBag.works = works;


            db.SaveChanges();

            ViewBag.news = 1;
            return View(work);
        }

        public ActionResult works(int? id, int? index)
        {
            if (Session["Staff"] != null) ViewBag.staff = 1;
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据

            List<Works> works = new List<Works>();
            int Index;
            if (index != null)
            {
                Index = Convert.ToInt32(index);
            }
            else
            {
                Index = 1;
            }

            if (id == null)
            {
                works = db.Works.ToList();
            }
            else
            {
                int Id = Convert.ToInt32(id);
                works = db.Works.Where(d => d.ArtistId == Id).ToList();
                ViewBag.Art = db.Artist.FirstOrDefault(d => d.Id == id).ArtistNameCN;
            }



            List<ViewWorks> vw = PageWorks(works, Index, 30);
            ViewBag.count = vw.Count;
            return View(vw);
        }



        public ActionResult company(int id)
        {
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            var com = db.Company.FirstOrDefault(d => d.Id == id);
            var day = today();

            com.VisitCount = com.VisitCount + 1;
            day.CompanyVisit = day.CompanyVisit + 1;

            return View(com);
        }

        public ActionResult Constitution()
        {
            ViewBag.about = 1;
            if (Session["Staff"] != null) ViewBag.staff = 1;
            if (Session["Home"] == null) HaveViti();//是否是有效访问，若是记录数据
            return View();
        }


        public ActionResult ReturnStaff()
        {
            if (Session["Staff"] == null) RedirectToAction("Index", "Home");
            if (TempData["controller"] != null && TempData["view"] != null)
            {

                string controller = TempData["controller"].ToString();
                string view = TempData["view"].ToString();
                if (TempData["id"] != null)
                {
                    int id = Convert.ToInt32(TempData["id"].ToString());
                    return RedirectToAction(view + "/?id=" + id, controller);
                }
                return RedirectToAction(view, controller);
            }
            return RedirectToAction("Index", "Staff");
        }


        /// <summary>
        /// 生成当天点击量数据
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
                d.WorksVisit = 0;
                d.ArtVisit = 0;
                d.CompanyVisit = 0;
                d.NewsVisit = 0;
                db.Diary.Add(d);
                db.SaveChanges();
            }

            return d;
        }

        /// <summary>
        /// 有效访问处理数据
        /// </summary>
        private void HaveViti()
        {
            Session["Home"] = 1;
            var d = today();
            d.VistiTotal = d.VistiTotal + 1;
            string ip = GetRealIP();
            if (ip == "::1") ip = "113.118.11.254";
            string api = GetAddressBaidu(ip);
            AccessData ad = new AccessData();
            try
            {
                if (api != null)
                {
                    string[] inf = api.Split('|');
                    ad.Nation = inf[0];
                    if (inf[1] == "香港")
                    {
                        ad.Porvince = "香港特别行政区";
                    }
                    else
                    if (inf[1] == "澳门")
                    {
                        ad.Porvince = "澳门特别行政区";
                    }
                    else
                    if (inf[1] == "1")
                    {
                        ad.Porvince = "境外访问";
                    }
                    else
                    {
                        ad.Porvince = inf[1];
                    }


                    if (inf[2] == "None")
                    {
                        ad.Ctiy = "未记录服务器";
                    }
                    else
                        if (inf[2] == "香港")
                    {
                        ad.Porvince = "香港特别行政区";
                    }
                    else
                    if (inf[2] == "澳门")
                    {
                        ad.Porvince = "澳门特别行政区";
                    }
                    else
                    if (inf[2] == "1")
                    {
                        ad.Porvince = "境外访问";
                    }

                    ad.Ctiy = inf[2];
                    ad.DC = inf[3];
                }
            }
            catch
            {
                ad.Porvince = "境外访问";
                ad.DC = "境外地区";
            }
            ad.Ip = ip;
            ad.Date = DateTime.Now;

            db.AccessData.Add(ad);
            db.SaveChanges();
        }

        public List<newsreadlist> newslist(int? page, int? limit, int? count)
        {
            List<newsreadlist> nl = new List<newsreadlist>();
            var news = db.News.OrderByDescending(d => d.Id).ToList();

            foreach (var a in news)
            {
                var n = new newsreadlist();
                n.Title = a.Title;
                n.Subtitle = a.Subtitle;
                n.Id = a.Id;
                n.Autor = a.Autor;
                n.ImgTitle = a.CoverPath;
                if (n.link != null)
                {
                    n.or = true;
                    n.link = a.link;
                }
                else
                {
                    n.or = false;
                }
                DateTime dt = Convert.ToDateTime(a.Date);

                n.DateD = dt.Day;
                n.DateM = dt.Month;
                nl.Add(n);
            }


            return nl;
        }
        /// <summary>
        /// IP访问测试
        /// </summary>
        /// <param name="ip"></param>
        public JsonResult TestIpAddList(string ip)
        {
            var staff = Session["Staff"];
            if (staff != null)
            {
                string api = GetAddressBaidu(ip);
                AccessData ad = new AccessData();
                Random r = new Random();
                string msg = "录入成功" + r.Next(1000);
                try
                {
                    if (api != null)
                    {

                        string[] inf = api.Split('|');
                        ad.Nation = inf[0];
                        ad.Porvince = inf[1] + "省";
                        ad.Ctiy = inf[2] + "市";
                        ad.DC = "测试";
                    }
                }
                catch
                {
                    ad.DC = "境外地区";
                    msg = "录入失败";
                }

                ad.Ip = ip;
                ad.Date = DateTime.Now;

                db.AccessData.Add(ad);
                db.SaveChanges();


                return new JsonResult()
                {
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                    Data = new
                    {
                        msg = msg
                    }
                };

            }
            else
            {
                return null;
            }
        }


        /// <summary>
        /// 获取真ip
        /// </summary>
        /// <returns></returns>
        public string GetRealIP()
        {
            string result = String.Empty;
            result = System.Web.HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            //可能有代理 
            if (!string.IsNullOrWhiteSpace(result))
            {
                //没有"." 肯定是非IP格式
                if (result.IndexOf(".") == -1)
                {
                    result = null;
                }
                else
                {
                    //有","，估计多个代理。取第一个不是内网的IP。
                    if (result.IndexOf(",") != -1)
                    {
                        result = result.Replace(" ", string.Empty).Replace("\"", string.Empty);

                        string[] temparyip = result.Split(",;".ToCharArray());

                        if (temparyip != null && temparyip.Length > 0)
                        {
                            for (int i = 0; i < temparyip.Length; i++)
                            {
                                //找到不是内网的地址
                                if (IsIPAddress(temparyip[i]) && temparyip[i].Substring(0, 3) != "10." && temparyip[i].Substring(0, 7) != "192.168" && temparyip[i].Substring(0, 7) != "172.16.")
                                {
                                    return temparyip[i];
                                }
                            }
                        }
                    }
                    //代理即是IP格式
                    else if (IsIPAddress(result))
                    {
                        return result;
                    }
                    //代理中的内容非IP
                    else
                    {
                        result = null;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(result))
            {
                result = System.Web.HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
            }

            if (string.IsNullOrWhiteSpace(result))
            {
                result = System.Web.HttpContext.Current.Request.UserHostAddress;
            }
            return result;
        }
        public bool IsIPAddress(string str)
        {
            if (string.IsNullOrWhiteSpace(str) || str.Length < 7 || str.Length > 15)
                return false;

            string regformat = @"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})";
            Regex regex = new Regex(regformat, RegexOptions.IgnoreCase);
            return regex.IsMatch(str);
        }
        /// <summary>
        /// 百度api
        /// </summary>
        /// <returns></returns>
        public string GetAddressBaidu(string ip)
        {
            try
            {
                string url = "http://api.map.baidu.com/location/ip?ak=mTZwdVqrD27PoN0D6YswGIdPMm7nSlEG&ip=" + ip;
                WebClient client = new WebClient();
                var buffer = client.DownloadData(url);
                string jsonText = Encoding.UTF8.GetString(buffer);
                JObject jo = JObject.Parse(jsonText);
                string c = jo.First.First.ToString();
                return c;
            }
            catch
            {
                return null;
            }

        }
        public static bool HasChinese(string str)
        {
            return Regex.IsMatch(str, @"[\u4e00-\u9fa5]");
        }

        public List<newsreadlist> newslist(List<News> news)
        {
            List<newsreadlist> e = new List<newsreadlist>();
            foreach (var n in news)
            {
                var a = new newsreadlist();
                a.Id = n.Id;
                a.Title = n.Title;
                a.Subtitle = n.Subtitle;
                a.ImgTitle = n.CoverPath;
                DateTime dt = Convert.ToDateTime(n.Date);
                a.DateM = dt.Month;
                a.DateD = dt.Day;
                a.link = n.link;
                a.or = n.original;
                a.Autor = n.Autor;
                e.Add(a);
            }
            return e;
        }

        public List<Tag> WorksTags(Works work)
        {
            List<Tag> tags = new List<Tag>();
            int id = work.Id;
            var wt = db.WorksTag.Where(d => d.WorkId == id);
            foreach (var w in wt)
            {
                Tag tag = db.Tag.FirstOrDefault(d => d.Id == w.TagId);
                tags.Add(tag);
            }
            return tags;
        }
        public List<Works> otherwork(Works work)
        {
            List<Works> works = new List<Works>();
            int limit = 5;
            var workall = db.Works.Where(d => d.ArtistId == work.ArtistId).ToList();
            int position = workall.IndexOf(work);

            if (position <= 1)
            {
                if (workall.Count - limit >= 0)
                {
                    works = workall.Take(limit).ToList();
                }
                else
                {
                    limit = workall.Count;
                    works = workall.Take(limit).ToList();
                }

            }
            else if (workall.Count - position <= 1)
            {
                int end = workall.Count - 1;

                try { works.Add(workall[end - 4]); } catch { }
                try { works.Add(workall[end - 3]); } catch { }
                try { works.Add(workall[end - 2]); } catch { }
                works.Add(workall[end - 1]);
                works.Add(workall[end]);
            }
            else
            {
                works.Add(workall[position - 2]);
                works.Add(workall[position - 1]);
                works.Add(workall[position]);
                works.Add(workall[position + 1]);
                try
                {
                    works.Add(workall[position + 2]);
                }
                catch
                {
                    works.Add(workall[position - 3]);
                    works.OrderByDescending(d => d.Id);
                }

            }


            return works;
        }
        public Tagmodel Toworks(string keyword)
        {
            Tagmodel tm = new Tagmodel();

            List<Tag> etags = new List<Tag>();
            List<Works> works = new List<Works>();
            List<int> wid = new List<int>();//作品ID集合
            string nulltag = "";//空标签

            if (keyword == "") return null;
            if (keyword.IndexOf(" ") > 0)//判断有几个词组
            {
                string[] tags = keyword.Split(' ');
                for (int a = 0; a < tags.Length; a++)
                {
                    if (tags[a] != "")//确认为有效值
                    {
                        string t = tags[a];
                        Tag tag = db.Tag.FirstOrDefault(d => d.Title == t);
                        if (tag != null)//确认为在在数据库中TAG
                        {
                            List<WorksTag> wt = db.WorksTag.Where(d => d.TagId == tag.Id).ToList();
                            if (wt.Count > 0)//确认此tag有作品
                            {

                                List<int> twid = new List<int>();
                                foreach (var b in wt)
                                {
                                    twid.Add(b.WorkId);
                                }
                                if (wid.Count == 0)
                                {
                                    wid = twid;
                                }
                                else
                                {
                                    wid = twid.FindAll(x => wid.Contains(x));
                                }
                                etags.Add(tag);
                                continue;
                            }//此tag没有对应的作品
                        }//不在数据库中的TAG
                        if (nulltag == "")
                        {
                            nulltag = tags[a];
                        }
                        else
                        {
                            nulltag = nulltag + "," + tags[a];
                        }
                    }//无效值，跳过                    
                }
                foreach (int c in wid)
                {
                    Works work = db.Works.FirstOrDefault(d => d.Id == c);
                    works.Add(work);
                }
            }
            else//若只有一个词组
            {
                Tag tag = db.Tag.FirstOrDefault(d => d.Title == keyword);
                if (tag != null)
                {
                    List<WorksTag> wts = db.WorksTag.Where(d => d.TagId == tag.Id).ToList();
                    if (wts.Count > 0)
                    {
                        foreach (var c in wts)
                        {
                            Works work = db.Works.FirstOrDefault(d => d.Id == c.WorkId);
                            works.Add(work);
                        }
                        etags.Add(tag);
                    }
                    else nulltag = keyword;
                }
                else nulltag = keyword;
            }
            tm.works = works;
            tm.nulltags = nulltag;
            tm.tags = etags;
            return tm;
        }
        public List<ViewWorks> ToVieWorks(List<Works> works)
        {
            List<ViewWorks> tvw = new List<ViewWorks>();
            foreach (var w in works)
            {
                ViewWorks work = new ViewWorks();
                Artist art = db.Artist.FirstOrDefault(d => d.Id == w.ArtistId);
                work.Id = w.Id;
                work.ArtistId = w.ArtistId;
                work.ArtisName = art.ArtistNameCN;
                work.Path = w.Path;
                work.Title = w.Title;
                DateTime dt = Convert.ToDateTime(w.FirstDate);
                work.date = dt.ToString("yyyy年MM月dd日HH时mm分");
                tvw.Add(work);
            }
            return tvw;
        }
        public List<ViewWorks> PageWorks(List<Works> works, int page, int limit)
        {
            int count = works.Count;
            int fint = (page - 1) * limit;
            int eint = page * limit;
            works = works.Take(eint).Skip(fint).ToList();

            List<ViewWorks> vw = ToVieWorks(works);
            return vw;
        }
    }

}
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Szaipa.App_Start;
using szaipa2022.Models;

namespace Szaipa.Controllers
{
    public class Project_TongouController : Controller
    {

        TongouEntities tongou = new TongouEntities();
        // GET: Porject_Tongou
        public ActionResult Work(int? id)
        {
            int ID;
            if (id.HasValue)
            {
                ID = Convert.ToInt32(id);
            }
            else
            {
                ID = 1;
            }
            var work = tongou.TongouWorks.FirstOrDefault(d => d.id == ID);
            work.VisityCount= work.VisityCount+1;
            tongou.SaveChanges();
            if (ID == 36)
            {
                return RedirectToAction("mengboshen", "Project_Tongou");
            }
            if (ID == 84)
            {
                return RedirectToAction("beini", "Project_Tongou");
            }
            if (ID == 115)
            {
                return RedirectToAction("mouliqiao", "Project_Tongou");
            }
            if (ID == 162)
            {
                return RedirectToAction("mouliqiao2", "Project_Tongou");
            }
            if (ID == 163)
            {
                return RedirectToAction("gaodaqing2", "Project_Tongou");
            }


            return View(work);
        }
        public ActionResult Atrist(int? id)
        {
            int ID;
            if (id.HasValue)
            {
                ID = Convert.ToInt32(id);
            }
            else
            {
                ID = 1;
            }
            var atrist = tongou.TongouAtrist.FirstOrDefault(d => d.id == ID);

            if (ID == 96)
            {
                return RedirectToAction("beiniTD", "Project_Tongou");
            }

            return View(atrist);
        }


        public ActionResult WorkList(int? id)
        {
            int ID;
            if (id.HasValue)
            {
                ID = Convert.ToInt32(id);
            }
            else
            {
                ID = 1;
            }
            var atrist = tongou.TongouAtrist.FirstOrDefault(d => d.id == ID);
            ViewBag.Name = atrist.Name;

            var works = tongou.TongouWorks.Where(d => d.Atristid == ID).ToList();
            return View(works);
        }

        public ActionResult AtristAdd()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.tongou = 1;
            var atr = new TongouAtrist();
            return View(atr);
        }
        [HttpPost]
        public ActionResult AtristAdd(TongouAtrist atrist)
        {
            var itme = tongou.TongouAtrist.FirstOrDefault(d => d.Name == atrist.Name);
            if (itme != null)
            {
                ModelState.AddModelError("", "已存在此艺术家");
                return View(atrist);
            }
            tongou.TongouAtrist.Add(atrist);
            tongou.SaveChanges();

            return RedirectToAction("TongouAtristList", "Staff");
        }

        public ActionResult AtristEdit(int id)
        {

            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.tongou = 1;

            var art = tongou.TongouAtrist.FirstOrDefault(d => d.id == id);

            return View(art);
        }
        [HttpPost]
        public ActionResult AtristEdit(TongouAtrist atrist)
        {
            var itme = tongou.TongouAtrist.FirstOrDefault(d => d.id == atrist.id);
            if (itme.Name != atrist.Name) itme.Name = atrist.Name;
            if (itme.Title != atrist.Title) itme.Title = atrist.Title;
            if (itme.HeardPath != atrist.HeardPath) itme.HeardPath = atrist.HeardPath;
            if (itme.AboutText != atrist.AboutText) itme.AboutText = atrist.AboutText;

            tongou.SaveChanges();

            return RedirectToAction("TongouAtristList", "Staff");
        }
        public ActionResult WorkAdd()
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.tongou = 1;

            var work = new TongouWorks();
            return View(work);
        }
        [HttpPost]
        public ActionResult WorkAdd(TongouWorks works)
        {
            var atr = tongou.TongouAtrist.FirstOrDefault(d => d.Name == works.AtristidName);
            if (atr == null)
            {
                ModelState.AddModelError("", "此艺术家为录入，请先录入艺术家信息");
                return View(works);
            }
            var itme = new TongouWorks();
            itme.AtristidName = works.AtristidName;
            itme.Atristid = atr.id;
            atr.WorksCount++;
            itme.Title = works.Title;
            itme.CreationDate = works.CreationDate;
            itme.ImgPath = works.ImgPath;
            itme.Size = works.Size;
            itme.Type = works.Type;
            itme.VisityCount = 0;

            tongou.TongouWorks.Add(itme);
            tongou.SaveChanges();


            return RedirectToAction("TongouWorksList", "Staff");
        }

        public ActionResult WorkEdit(int id)
        {
            string actionName = this.ControllerContext.RouteData.Values["action"].ToString();
            string controllerName = this.ControllerContext.RouteData.Values["controller"].ToString();
            TempData["controller"] = controllerName;
            TempData["view"] = actionName;
            var staff = Session["Staff"];
            if (staff == null) return RedirectToAction("Login", "Staff");
            ViewBag.tongou = 1;

            var work = tongou.TongouWorks.FirstOrDefault(d => d.id == id);

            return View(work);
        }
        [HttpPost]
        public ActionResult WorkEdit(TongouWorks works)
        {
            var itme = tongou.TongouWorks.FirstOrDefault(d => d.id == works.id);
            if (itme.Title != works.Title) itme.Title = works.Title;
            if (itme.ImgPath != works.ImgPath) itme.ImgPath = works.ImgPath;
            if (itme.Size != works.Size) itme.Size = works.Size;
            if (itme.CreationDate != works.CreationDate) itme.CreationDate = works.CreationDate;
            if (itme.Type != works.Type) itme.Type = works.Type;

            tongou.SaveChanges();

            return RedirectToAction("TongouWorksList", "Staff");
        }

        public ActionResult QRcode()
        {
            return View();
        }
        public ActionResult beini()//84
        {
            int id = 84;
            var work = tongou.TongouWorks.FirstOrDefault(d => d.id == id);
            return View(work);
        }
        public ActionResult beiniTD()//96
        {
            int id = 96;
            var atr = tongou.TongouAtrist.FirstOrDefault(d => d.id == id);
            return View(atr);
        }

        public ActionResult mouliqiao2()//162
        {
            int id = 162;
            var work = tongou.TongouWorks.FirstOrDefault(d => d.id == id);
            return View(work); ;
        }
        public ActionResult mouliqiao()//115
        {
            int id = 115;
            var work = tongou.TongouWorks.FirstOrDefault(d => d.id == id);
            return View(work); ;
        }
        public ActionResult mengboshen()//36
        {
            int id = 36;
            var work = tongou.TongouWorks.FirstOrDefault(d => d.id == id);
            return View(work);
        }
        public ActionResult gaodaqing2()//163
        {
            int id = 163;
            var work = tongou.TongouWorks.FirstOrDefault(d => d.id == id);
            return View(work);
        }


        public JsonResult atrtislist(int page, int limit)
        {
            if (Session["Staff"] == null) return null;
            var Art = tongou.TongouAtrist.OrderBy(d => d.id).ToList();
            int count = Art.Count;
            var atr = PageAtrist(Art, page, limit, count);

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

        public JsonResult workslist(int page, int limit)
        {
            if (Session["Staff"] == null) return null;
            var Works = tongou.TongouWorks.OrderBy(d => d.id).ToList();
            int count = Works.Count;
            var works = PageWorks(Works, page, limit, count);

            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    code = 0,
                    msg = "",
                    count = count,
                    data = works,
                },
            };
        }


        public JsonResult TongouAtristZxing()
        {
            if (Session["Staff"] == null) return null;
            int width = 500;
            int height = 500;
            string tempName = "同构作者二维码" + DateTime.Now.ToString("yyyyMMddHHMMss");
            string path = "~/Content/TempFile/";
            string serverPath = Server.MapPath(path);


            string tempFolder = Path.Combine(serverPath, tempName);
            Directory.CreateDirectory(tempFolder);//创建文件夹
            DirectoryInfo folder = new DirectoryInfo(serverPath);

            var atrist = tongou.TongouAtrist.ToList();
            foreach (var atr in atrist)
            {
                string filename = atr.id + atr.Name;
                string h = "http://www.szaipa.com/Project_Tongou/Atrist/" + atr.id;
                Bitmap zxin = ZXingLibs.GetQRCode(h, width, height);
                string p = tempFolder + "\\" + filename + ".png";
                zxin.Save(string.Format(p), ImageFormat.Png);
            }
            //产生RAR文件，及文件输出
            RARSave(tempFolder, tempName);
            DownloadRAR(tempFolder + "\\" + tempName + ".rar");

            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    code = 0,
                },
            };
        }

        public JsonResult TongouWorkZxing()
        {
            if (Session["Staff"] == null) return null;
            int width = 500;
            int height = 500;
            string tempName = "同构参展作品二维码" + DateTime.Now.ToString("yyyyMMddHHMMss");
            string path = "~/Content/TempFile/";
            string serverPath = Server.MapPath(path);

            string tempFolder = Path.Combine(serverPath, tempName);
            Directory.CreateDirectory(tempFolder);//创建文件夹
            DirectoryInfo folder = new DirectoryInfo(serverPath);

            var works = tongou.TongouWorks.ToList();
            foreach (var w in works)
            {
                string name = w.Title;
                if (name.Contains("/"))
                {
                    name.Replace("/", ",");
                }
                string filename = w.id +"《"+ name + "》"+w.AtristidName;
                
                string h = "http://www.szaipa.com/Project_Tongou/Work/" + w.id;
                Bitmap zxin = ZXingLibs.GetQRCode(h, width, height);
                string p = tempFolder + "\\" + filename + ".png";
                zxin.Save(string.Format(p), ImageFormat.Png);
            }

            //产生RAR文件，及文件输出
            RARSave(tempFolder, tempName);
            DownloadRAR(tempFolder + "\\" + tempName + ".rar");

            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    code = 0,
                },
            };
        }
        private List<TongouAtrist> PageAtrist(List<TongouAtrist> Atr, int page, int limit, int count)
        {
            int fint = (page - 1) * limit;
            int eint = page * limit;

            Atr = Atr.Take(eint).Skip(fint).ToList();
            return Atr;
        }

        private List<TongouWorks> PageWorks(List<TongouWorks> Works, int page, int limit, int count)
        {
            int fint = (page - 1) * limit;
            int eint = page * limit;

            Works = Works.Take(eint).Skip(fint).ToList();

            return Works;
        }
        public void AWlink()
        {
            var list = tongou.TongouWorks.ToList();
            foreach (var w in list)
            {
                if (w.Atristid == 0)
                {
                    var atrist = tongou.TongouAtrist.FirstOrDefault(d => d.Name == w.AtristidName);

                    if (atrist != null)
                    {
                        var work = tongou.TongouWorks.FirstOrDefault(d => d.id == w.id);
                        work.Atristid = atrist.id;
                        atrist.WorksCount++;
                        tongou.SaveChanges();
                    }
                }
            }


        }

        /// <summary>
        /// 读取excel文件内容,只能读取xls和xslx类型的文件
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        public void GetExcelData(string filePath)
        {
            try
            {
                string fileType = ".xls";//获取文件的后缀
                string connectinString = string.Format("Provider=Microsoft.ACE.OLEDB.{0}.0;Persist Security Info=False;" +
                                        "Extended Properties=\"Excel {1}.0;HDR={2};IMEX=1;\";" +
                                        "data source={3};", (fileType == ".xls" ? 4 : 12), (fileType == ".xls" ? 8 : 12), (true ? "Yes" : "NO"), filePath);

                //建立 链接  创建到数据源的对象
                OleDbConnection connection = new OleDbConnection(connectinString);
                //打开链接
                connection.Open();

                //返回Excel的架构，包括各个sheet表的名称,类型，创建时间和修改时间等　
                DataTable dtSheetName = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, new object[] { null, null, null, "Table" });
                //包含excel中表名的字符串数组
                string[] strTableNames = new string[dtSheetName.Rows.Count];
                for (int k = 0; k < dtSheetName.Rows.Count; k++)
                {
                    strTableNames[k] = dtSheetName.Rows[k]["TABLE_NAME"].ToString();
                }

                //string sql = $"select * from [{sheet}$]"; //sql 语法  是一个查询命令,该句报错,修改成如下
                string sql = "select * from [" + strTableNames[0] + "]";

                OleDbDataAdapter adapter = new OleDbDataAdapter(sql, connection);

                DataSet dataSet = new DataSet();//用来放数据 用来存放DataTable
                adapter.Fill(dataSet);//表示把查询 结果datatable 放到(填充)dataset 里面
                connection.Close();//释放链接资源  

                //取得数据
                DataTableCollection tableCollection = dataSet.Tables; //获取当前集合中所有 表格
                DataTable table = tableCollection[0];//因为只往dataset里面放置 了一张表格 ，所有这里取得索引为0的表格就是我们刚刚查询到的表格
                //取得表格中的数据
                //取得table中所有的行
                DataRowCollection dataTableRow = table.Rows;//返回了一个行的集合

                //遍历行的集合，取得每一个行的datarow对象
                foreach (DataRow row in dataTableRow)//遍历行
                {
                    foreach (var item in row.ItemArray)//遍历行的所有列
                    {

                    }
                }



            }
            catch //(Exception ex)
            {

            }
        }
        /// <summary>
        /// 生成RAR文件
        /// </summary>
        /// <param name="path">存放复制文件的目录</param>
        /// <param name="rarPatch">RAR文件存放目录</param>
        /// <param name="rarName">RAR文件名</param>
        private void RARSave(string rarPatch, string rarName)
        {

            string the_rar;

            RegistryKey the_Reg;

            object the_Obj;

            string the_Info;

            ProcessStartInfo the_StartInfo;

            Process the_Process;

            try
            {

                the_Reg = Registry.ClassesRoot.OpenSubKey(@"WinRAR");

                the_Obj = the_Reg.GetValue("");

                the_rar = the_Obj.ToString();

                the_Reg.Close();

                the_rar = the_rar.Substring(1, the_rar.Length - 7);

                the_Info = " a " + rarName + " -r";

                the_StartInfo = new ProcessStartInfo();

                the_StartInfo.FileName = "WinRar"; //the_rar;

                the_StartInfo.Arguments = the_Info;

                the_StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                //打包文件存放目录

                the_StartInfo.WorkingDirectory = rarPatch;

                the_Process = new Process();

                the_Process.StartInfo = the_StartInfo;

                the_Process.Start();

                the_Process.WaitForExit();

                the_Process.Close();

            }
            catch (Exception)
            {

                throw;

            }

        }
        /// <summary>
        /// 下载生成的RAR文件
        /// </summary>
        private void DownloadRAR(string file)
        {

            FileInfo fileInfo = new FileInfo(file);

            Response.Clear();

            Response.ClearContent();

            Response.ClearHeaders();

            Response.AddHeader("Content-Disposition", "attachment;filename=" + fileInfo.Name);

            Response.AddHeader("Content-Length", fileInfo.Length.ToString());

            Response.AddHeader("Content-Transfer-Encoding", "binary");

            Response.ContentType = "application/octet-stream";

            Response.ContentEncoding = System.Text.Encoding.GetEncoding("gb2312");

            Response.WriteFile(fileInfo.FullName);

            Response.Flush();

            string tempPath = file.Substring(0, file.LastIndexOf("\\"));

            //删除临时目录下的所有文件

            DeleteFiles(tempPath);

            //删除空目录

            Directory.Delete(tempPath);

            Response.End();

        }

        /// <summary>
        /// 删除临时目录下的所有文件
        /// </summary>
        /// <param name="tempPath">临时目录路径</param>
        private void DeleteFiles(string tempPath)
        {
            DirectoryInfo directory = new DirectoryInfo(tempPath);
            try
            {
                foreach (FileInfo file in directory.GetFiles())
                {

                    if (file.Attributes.ToString().IndexOf("ReadOnly") != -1)
                    {

                        file.Attributes = FileAttributes.Normal;

                    }

                    System.IO.File.Delete(file.FullName);

                }

            }
            catch (Exception)
            {

                throw;

            }

        }

        public void deleteworks(int id)
        {
            var itme = tongou.TongouWorks.FirstOrDefault(d => d.id == id);
            if (itme != null)
            {
                var atr = tongou.TongouAtrist.FirstOrDefault(d => d.id == itme.Atristid);
                atr.WorksCount--;
                tongou.TongouWorks.Remove(itme);
                tongou.SaveChanges();
            }
        }
        public void deleteatrist(int id)
        {

        }
        /// <summary>
        /// 项目结束后数据统计
        /// </summary>
        /// <param name="id"></param>
        public void ProjectFilish(int id)
        {
            var project = tongou.Project.FirstOrDefault(d => d.id == id);
            var works = tongou.TongouWorks.Where(d => d.VisityCount != 0).ToList();

            string data = "";

            foreach (var itme in works)
            {
                if (data != "")
                {
                    data = data + "," + itme.VisityCount;
                }
                else
                {
                    data = itme.VisityCount.ToString();;
                }
                var work = tongou.TongouWorks.FirstOrDefault(d => d.id == itme.id);
                var arties = tongou.TongouAtrist.FirstOrDefault(d => d.id == work.Atristid);
                work.HotCount = work.HotCount + work.VisityCount;
                arties.HotCount = arties.HotCount+ work.VisityCount;
                work.VisityCount = 0;
                tongou.SaveChanges();  
            }
            project.VisityData = data;
            tongou.SaveChanges();
        }
        



        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }//已完成
    }


}
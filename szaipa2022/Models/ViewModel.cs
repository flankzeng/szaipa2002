using System.Collections.Generic;
using szaipa2022.Models;

namespace Szaipa.Models
{

    public class OR
    {
        public string date { set; get; }
        public List<string> or { set; get; }
    }

    public class newsreadlist
    {
        public int Id { set; get; }
        public string Title { get; set; }
        public string ImgTitle { get; set; }
        public string Subtitle { get; set; }
        public string Autor { get; set; }
        public string link { get; set; }
        public int DateM { get; set; }
        public int DateD { get; set; }
        public bool or { get; set; }
        public int ReadCount { get; set; }
        public int Year { get; set; } // Add this line
    }
    public class NewsViews
    {
        public int Id { set; get; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Autor { get; set; }
        public string link { get; set; }
        public string Date { get; set; }
        public int ReadCount { get; set; }

    }

    public class VisitDataCity
    {
        public string province { set; get; }
        public string city { set; get; }
        public int Pcount { set; get; }
        public int Ccount { set; get; }
    }

    public class VisitDataProvince
    {
        public string province { set; get; }
        public int count { set; get; }

        public List<VisityCount> vc { set; get; }
    }

    public class VisityCount
    {
        public string date { set; get; }
        public int count { set; get; }
    }

    public class WorksView
    {
        public int Id { set; get; }
        public int ArtistId { get; set; }
        public int visitcount { get; set; }
        public string Title { get; set; }
        public string ArtisName { get; set; }
        public string FDate { get; set; }
        public string LDate { get; set; }
        public string Record { get; set; }
        public string Tags { get; set; }
    }

    public class CompaniesView
    {
        public int Id { set; get; }
        public int? visitcount { get; set; }
        public string NameCN { set; get; }
        public string NameEN { set; get; }
        public string Business { set; get; }
        public string CEO { set; get; }
        public string FDate { get; set; }
        public string LDate { get; set; }

    }
    public class father
    {
        public string name { get; set; }
        public int value { get; set; }
        public List<children> children { get; set; }
    }

    public class children
    {
        public string name { get; set; }
        public int value { get; set; }
    }

    public class workinf
    {
        public int width { get; set; }
        public int height { get; set; }
        public bool tra { get; set; }
        public bool lon { get; set; }
    }
    public class Tagmodel
    {
        public List<Works> works { set; get; }
        public string nulltags { set; get; }
        public List<Tag> tags { set; get; }
    }

    public class ViewWorks
    {

        public int Id { set; get; }
        public int ArtistId { set; get; }
        public string Title { set; get; }
        public string Path { set; get; }
        public string ArtisName { get; set; }
        public string date { get; set; }

    }

}
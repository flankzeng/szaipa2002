//------------------------------------------------------------------------------
// <auto-generated>
//     此代码已从模板生成。
//
//     手动更改此文件可能导致应用程序出现意外的行为。
//     如果重新生成代码，将覆盖对此文件的手动更改。
// </auto-generated>
//------------------------------------------------------------------------------

namespace szaipa2022.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Works
    {
        public int Id { get; set; }
        public int ArtistId { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string Content { get; set; }
        public string Record { get; set; }
        public Nullable<int> VisitCount { get; set; }
        public Nullable<System.DateTime> FirstDate { get; set; }
        public Nullable<System.DateTime> LastDate { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool transverse { get; set; }
        public bool @long { get; set; }
        public string Activity { get; set; }
        public string Tags { get; set; }
        public string Deeds { get; set; }
    }
}

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
    
    public partial class Exhibition
    {
        public int Id { get; set; }
        public int ArtistId { get; set; }
        public string CoverPath { get; set; }
        public string Title { get; set; }
        public string Location { get; set; }
        public string StartDate { get; set; }
        public string Link { get; set; }
        public string EndDate { get; set; }
        public string EditRecord { get; set; }
        public Nullable<int> VisitCount { get; set; }
    }
}

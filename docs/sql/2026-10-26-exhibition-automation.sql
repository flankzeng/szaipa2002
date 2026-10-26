/* 展览自动化与专题积木增量结构。可重复执行；由用户在目标 SQL Server 自行执行。 */
IF OBJECT_ID(N'dbo.FeaturedExhibition', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FeaturedExhibition (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FeaturedExhibition PRIMARY KEY,
        SortOrder int NOT NULL CONSTRAINT DF_FeaturedExhibition_SortOrder DEFAULT (0),
        IsVisible bit NOT NULL CONSTRAINT DF_FeaturedExhibition_IsVisible DEFAULT (1),
        ShowInCarousel bit NOT NULL CONSTRAINT DF_FeaturedExhibition_ShowInCarousel DEFAULT (0),
        Title nvarchar(300) NOT NULL,
        YearText nvarchar(40) NULL,
        LinkUrl nvarchar(1000) NOT NULL,
        InfoLines nvarchar(max) NULL,
        ImageUrls nvarchar(max) NULL,
        LogoUrl nvarchar(1000) NULL,
        CoverUrl nvarchar(1000) NULL,
        StartDate date NULL,
        EndDate date NULL,
        Status bit NULL CONSTRAINT DF_FeaturedExhibition_Status DEFAULT (1)
    );
    CREATE UNIQUE INDEX UX_FeaturedExhibition_LinkUrl ON dbo.FeaturedExhibition(LinkUrl);
END;
GO
IF COL_LENGTH(N'dbo.FeaturedExhibition', N'StartDate') IS NULL ALTER TABLE dbo.FeaturedExhibition ADD StartDate date NULL;
IF COL_LENGTH(N'dbo.FeaturedExhibition', N'EndDate') IS NULL ALTER TABLE dbo.FeaturedExhibition ADD EndDate date NULL;
IF COL_LENGTH(N'dbo.FeaturedExhibition', N'Status') IS NULL ALTER TABLE dbo.FeaturedExhibition ADD Status bit NULL CONSTRAINT DF_FeaturedExhibition_Status DEFAULT (1);
GO
IF OBJECT_ID(N'dbo.PublicationBlock', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PublicationBlock (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PublicationBlock PRIMARY KEY,
        PublicationId int NOT NULL,
        SortOrder int NOT NULL CONSTRAINT DF_PublicationBlock_SortOrder DEFAULT (0),
        BlockType nvarchar(30) NOT NULL,
        Content nvarchar(max) NOT NULL
    );
    CREATE INDEX IX_PublicationBlock_PublicationId_SortOrder ON dbo.PublicationBlock(PublicationId, SortOrder);
END;
GO
IF COL_LENGTH(N'dbo.Publication', N'ThemeColor') IS NULL ALTER TABLE dbo.Publication ADD ThemeColor nvarchar(7) NULL;
IF COL_LENGTH(N'dbo.Publication', N'BannerPath') IS NULL ALTER TABLE dbo.Publication ADD BannerPath nvarchar(1000) NULL;
GO
/* 与首页现有四个手写活动块的标题、展讯和首图一致；保持 Status/旧展示兼容。 */
IF NOT EXISTS (SELECT 1 FROM dbo.FeaturedExhibition WHERE LinkUrl=N'/Home/Publication/92011')
 INSERT dbo.FeaturedExhibition(SortOrder,IsVisible,ShowInCarousel,Title,YearText,LinkUrl,InfoLines,ImageUrls,StartDate,EndDate,Status)
 VALUES(10,1,0,N'同构——中日艺术交流展',N'2024',N'/Home/Publication/92011',N'开始时间：2024.06.28
展览地点：東京都千代田区 ART SPACE 89
主办单位：深圳市艺术产业促进会
协办单位：东京印社 深圳市仓颉通文化传播有限公司',N'/Content/images/zhongri/10015.jpg
/Content/images/zhongri/10017.jpg
/Content/images/zhongri/10038.jpg
/Content/images/zhongri/10052.jpg
/Content/images/zhongri/10002.jpg
/Content/images/zhongri/10006.jpg
/Content/images/zhongri/10007.jpg','2024-06-28',NULL,0);
IF NOT EXISTS (SELECT 1 FROM dbo.FeaturedExhibition WHERE LinkUrl=N'/Home/Publication/92010')
 INSERT dbo.FeaturedExhibition(SortOrder,IsVisible,ShowInCarousel,Title,YearText,LinkUrl,InfoLines,ImageUrls,StartDate,EndDate,Status)
 VALUES(20,1,0,N'艺+科技新潮流展',N'2024',N'/Home/Publication/92010',N'展览时间：2024.05.24-2024.05.31
展览地点：龙华锦绣科学园三期D栋1层展厅
主办单位：润杨集团·锦绣科学园 深圳市艺术产业促进会
协办单位：深圳市锦绣大地投资有限公司 深圳市仓颉通文化传播有限公司',N'/Content/images/yijia/10034.jpg
/Content/images/yijia/10035.jpg
/Content/images/yijia/10037.jpg
/Content/images/yijia/10004.jpg
/Content/images/yijia/10005.jpg
/Content/images/yijia/10002.jpg
/Content/images/yijia/10007.jpg','2024-05-24','2024-05-31',1);
IF NOT EXISTS (SELECT 1 FROM dbo.FeaturedExhibition WHERE LinkUrl=N'/Home/Publication/92009')
 INSERT dbo.FeaturedExhibition(SortOrder,IsVisible,ShowInCarousel,Title,YearText,LinkUrl,InfoLines,ImageUrls,StartDate,EndDate,Status)
 VALUES(30,1,0,N'漫MAN-艺术时尚先锋展',N'2024',N'/Home/Publication/92009',N'展览时间：2024.05.22-2024.05.28
展览地点：南山·漫谷 同构艺术空间
主办单位：润杨集团·南山漫谷 深圳市艺术产业促进会
承办单位：同构艺术空间',N'/Content/images/man/10006.jpg
/Content/images/man/10002.jpg
/Content/images/man/10003.jpg
/Content/images/man/10004.jpg
/Content/images/man/10005.jpg
/Content/images/man/10001.jpg
/Content/images/man/10007.jpg','2024-05-22','2024-05-28',1);
IF NOT EXISTS (SELECT 1 FROM dbo.FeaturedExhibition WHERE LinkUrl=N'/Publication/chunyu3')
 INSERT dbo.FeaturedExhibition(SortOrder,IsVisible,ShowInCarousel,Title,YearText,LinkUrl,InfoLines,ImageUrls,StartDate,EndDate,Status)
 VALUES(40,1,0,N'春语第三季——致敬女性艺术展',N'2024',N'/Publication/chunyu3',N'展览时间：2024.03.08-2024.04.08
展览地点：南山·漫谷 同构艺术空间
主办单位：深圳市艺术产业促进会
承办单位：南山·漫谷 同构艺术空间',N'/Content/images/chunyu3/100012.jpg
/Content/images/chunyu3/100023.jpg
/Content/images/chunyu3/10002.jpg
/Content/images/chunyu3/100024.jpg
/Content/images/chunyu3/100034.jpg
/Content/images/chunyu3/100033.jpg
/Content/images/chunyu3/10003.jpg','2024-03-08','2024-04-08',1);
GO

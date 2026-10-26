-- 后台自动化上线汇总。由 scripts/build-automation-sql.py 生成，不直接编辑。
-- 用户执行前备份数据库、停止后台写入；agent 永不执行生产 SQL。
-- 必须使用 SQLCMD 模式（SSMS: 查询 → SQLCMD 模式，或 sqlcmd -b）。
-- 目标库默认 Szaipa；如是隔离测试库，只修改下一行的库名并核对连接目标。
-- 不包含历史 slug 数据迁移，不改 News 旧数据，不清理 Content。
:setvar TargetDatabase "Szaipa"
:On Error exit
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'$(TargetDatabase)'
    THROW 51000, N'目标数据库不匹配，已停止。请核对连接与 TargetDatabase。', 1;
IF OBJECT_ID(N'dbo.News', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Artist', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Works', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Publication', N'U') IS NULL
    THROW 51001, N'缺少 legacy 基础表，不能应用增量脚本。', 1;
BEGIN TRANSACTION;
GO

-- ===== 来源: 2026-06-exhibition-template-columns.sql；SHA256: a51384e898e6ed6ad22c9e613c776cdcb86dac91177086b27664efc007410545 =====
-- Exhibition template columns for the data-driven exhibition admin (普通 / 重要 types).
-- Apply to the database the admin context writes to AND the database the public site reads from
-- (in production they are the same DB; in dev, apply to the local writable copy, and to the read DB
-- if you want important-type exhibitions to render via /Home/Publication/{id} in dev).
--
-- Safe to run once. The Publication table is otherwise unchanged.

IF COL_LENGTH('dbo.Publication', 'Type') IS NULL
    ALTER TABLE dbo.Publication ADD [Type] INT NOT NULL CONSTRAINT DF_Publication_Type DEFAULT(0);

IF COL_LENGTH('dbo.Publication', 'Preface') IS NULL
    ALTER TABLE dbo.Publication ADD [Preface] NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.Publication', 'Signature') IS NULL
    ALTER TABLE dbo.Publication ADD [Signature] NVARCHAR(MAX) NULL;
GO

-- ===== 来源: 2026-07-09-exhibition-works-catalog.sql；SHA256: e093de20a9564132b36861cf6866e98bf53d2612da36a8ce6982840acb0ab790 =====
-- New table for the exhibition works catalog (参展作品目录), an optional sub-list of a Publication row
-- shown on the 重要 (important) exhibition skin between the 序 and the on-site photo gallery.
-- Apply to the database the admin context writes to AND the database the public site reads from
-- (in production they are the same DB; in dev, apply to the local writable copy, and to the read DB
-- if you want the works catalog to render via /Home/Publication/{id} in dev).
--
-- Safe to run once (guarded by OBJECT_ID check).

IF OBJECT_ID('dbo.ExhibitionWork', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExhibitionWork
    (
        [Id]            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExhibitionWork PRIMARY KEY,
        [PublicationId] INT NOT NULL,
        [Category]      NVARCHAR(100)  NULL,
        [Title]         NVARCHAR(255)  NULL,
        [Artist]        NVARCHAR(255)  NULL,
        [Size]          NVARCHAR(100)  NULL,
        [Medium]        NVARCHAR(100)  NULL,
        [ImagePath]     NVARCHAR(500)  NULL,
        [SortOrder]     INT NOT NULL CONSTRAINT DF_ExhibitionWork_SortOrder DEFAULT(0),
        CONSTRAINT FK_ExhibitionWork_Publication FOREIGN KEY ([PublicationId])
            REFERENCES dbo.Publication ([Id]) ON DELETE CASCADE
    );

    CREATE INDEX IX_ExhibitionWork_PublicationId ON dbo.ExhibitionWork ([PublicationId], [SortOrder]);
END
GO

-- ===== 来源: 2026-10-26-wechat-sync.sql；SHA256: 7e4625e230cf2f32452b689da930844e57dfef5113c6c1d4be9d8f8493143fb5 =====
-- 微信公众号文章同步（T1）：新建同步记录表 WeChatSyncRecord。
-- 文章本体仍存在 dbo.News（前台版式零改动），本表只记录来源/去重键/内容哈希/是否隐藏。
--
-- 需要在「站点读取的库」和「后台写入的库」上都执行（生产里是同一个库；开发时写库是本地副本）。
-- 幂等：用 OBJECT_ID 判断，重复执行安全。执行前后不改动任何已有表/列。
-- 表尚未建立时，代码会自动降级：前台照旧显示全部 News，后台「公众号同步」页提示"需先执行 SQL"，不会报 500。
--
-- 上线后另需在站点配置里设 WeChat:LegacyNewsMaxId（切换时刻 News 表的最大 Id）才会隐藏旧手录新闻，见
-- docs/updates/2026-10-26-wechat-news-sync.md。

IF OBJECT_ID('dbo.WeChatSyncRecord', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WeChatSyncRecord
    (
        [Id]          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WeChatSyncRecord PRIMARY KEY,
        -- 去重键：mp:{biz}:{mid}:{idx}（链接导入/接口来源归一到同一篇），退化时为 api:{article_id}:{idx} 或 url:{规范化链接}
        [SourceKey]   NVARCHAR(300)  NOT NULL,
        [SourceUrl]   NVARCHAR(1000) NULL,
        [ArticleId]   NVARCHAR(100)  NULL,
        [ItemIndex]   INT NOT NULL CONSTRAINT DF_WeChatSyncRecord_ItemIndex DEFAULT(0),
        -- 对应 dbo.News.Id；不建外键：后台删除新闻后记录保留（列表显示"新闻已删除"），也避免与旧库约束耦合
        [NewsId]      INT NOT NULL,
        -- 清洗后内容 + 标题/摘要/封面的 SHA256（十六进制），用于判断源文章是否被改动
        [ContentHash] NVARCHAR(64)   NOT NULL,
        [SyncedAt]    DATETIME2(0)   NOT NULL,
        -- 1 = 后台隐藏：不出现在前台列表/首页/侧栏推荐，详情 URL 仍可访问
        [IsHidden]    BIT NOT NULL CONSTRAINT DF_WeChatSyncRecord_IsHidden DEFAULT(0),
        -- 来源通道：link / freepublish / material / callback
        [Source]      NVARCHAR(20)   NOT NULL CONSTRAINT DF_WeChatSyncRecord_Source DEFAULT('link')
    );

    CREATE UNIQUE INDEX UX_WeChatSyncRecord_SourceKey ON dbo.WeChatSyncRecord ([SourceKey]);
    CREATE INDEX IX_WeChatSyncRecord_NewsId ON dbo.WeChatSyncRecord ([NewsId]);
END
GO

-- ===== 来源: 2026-10-26-org-and-home-blocks.sql；SHA256: d08f878efea4de8583f1a879bbb954a4d006f7571696f259c40f84ddbe463f16 =====
-- 组织架构与首页静态块数据化（T3）：OrgMember / SaleWork / Partner 三张表 + 种子数据。
--
-- 作用：首页「核心成员」「顾问团队」「在售艺术品」与「关于我们」的「合作伙伴」从模板里的手写内容，
--       改为读这三张表（后台：内容管理 → 组织架构 / 在售作品 / 合作伙伴）。
-- 种子数据 = 改造前模板里的硬编码内容，执行后页面与改造前一致。
--
-- 执行位置（由用户手动执行，agent 不执行）：
--   * 前台读的库（生产为同一个库）与后台写的库都要有这三张表；
--   * 本机开发时：在本地可写副本里执行；只读生产库上不要由 agent 执行。
-- 幂等：表用 IF OBJECT_ID ... IS NULL 创建；种子仅在对应表「完全为空」时插入，重复执行不会覆盖后台的修改，
--       也不会重复插入。想重新种入：先清空对应表再执行本脚本。
-- 容错：这三张表尚未创建时，前台自动回退到内置的同样内容，不会报错（见 HomeBlocksReadRepository）。
-- 只读账号（db_datareader）自动拥有新表的 SELECT 权限，无需另外授权。
--
-- 种子区块 BEGIN SEED / END SEED 由测试 HomeBlocksSeedSqlTests 从 HomeBlocksDefaults 生成并校验，
-- 修改默认内容后用 UPDATE_SEED_SQL=1 重新运行该测试即可同步，请勿手改种子区块。

SET NOCOUNT ON;

------------------------------------------------------------------------------
-- 1. OrgMember：组织架构成员（首页 核心成员 / 顾问团队）
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.OrgMember', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrgMember
    (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrgMember PRIMARY KEY,
        Name        NVARCHAR(100)  NOT NULL,
        [Role]      NVARCHAR(50)   NULL,                                   -- 职务：会长/副会长/会员…
        Title       NVARCHAR(100)  NULL,                                   -- 头衔：艺术家/教授…
        GroupName   NVARCHAR(50)   NOT NULL CONSTRAINT DF_OrgMember_GroupName DEFAULT (N'核心成员'),
        ArtistId    INT            NULL,                                   -- 可选：关联 Artist.Id（首页卡片链接到 /Home/Art/{id}）
        AvatarPath  NVARCHAR(300)  NULL,                                   -- /Content/... 站内路径
        ImageWidth  INT            NULL,
        ImageHeight INT            NULL,
        Sort        INT            NOT NULL CONSTRAINT DF_OrgMember_Sort DEFAULT (0),
        IsVisible   BIT            NOT NULL CONSTRAINT DF_OrgMember_IsVisible DEFAULT (1)
    );
    CREATE INDEX IX_OrgMember_Group_Sort ON dbo.OrgMember (GroupName, Sort);
END
GO

-- BEGIN SEED OrgMember
IF NOT EXISTS (SELECT 1 FROM dbo.OrgMember)
BEGIN
    INSERT INTO dbo.OrgMember (Name, [Role], Title, GroupName, ArtistId, AvatarPath, ImageWidth, ImageHeight, Sort, IsVisible) VALUES
    (N'张岚芊', N'会长', N'艺术家', N'核心成员', 1000, N'/Content/images/to black/vip-2zlq.png', NULL, NULL, 1, 1),
    (N'姚建伟', N'荣誉会长', N'教授', N'核心成员', 1009, N'/Content/images/to black/vip-1yjw.png', NULL, NULL, 2, 1),
    (N'徐赓良', N'副会长', N'艺术家', N'核心成员', 1011, N'/Content/ArtImg/Artist/16a38c7aa1b0fc91afc363be8fe2a8b7.png', NULL, NULL, 3, 1),
    (N'陈硕子', N'副会长', N'艺术家', N'核心成员', 1049, N'/Content/ArtImg/Artist/629c4dd2-1960-49dc-91ba-abeaf8f7373d.jpg', NULL, NULL, 4, 1),
    (N'王玉波', N'监事', N'艺术家', N'核心成员', 1012, N'/Content/images/to black/vip-5wyb.png', NULL, NULL, 5, 1),
    (N'沈敬东', N'会员', N'艺术家', N'核心成员', 1015, N'/Content/images/to black/vip-7sjd.png', 824, 824, 6, 1),
    (N'程晓光', N'策展人', N'艺术家', N'核心成员', 1014, N'/Content/images/to black/vip-6cxg.png', NULL, NULL, 7, 1),
    (N'邓国源', N'会员', N'艺术家', N'核心成员', 1017, N'/Content/images/to black/vip-9dgy.png', NULL, NULL, 8, 1),
    (N'祁志龙', N'会员', N'艺术家', N'核心成员', 1019, N'/Content/images/to black/vip-11qzl.png', 800, 800, 9, 1),
    (N'杨宽', N'会员', N'考古学家', N'核心成员', 1021, N'/Content/images/to black/vip-12yk.png', 800, 800, 10, 1),
    (N'黄笃', N'策展人', N'评论家', N'顾问团队', 1013, N'/Content/images/to black/vip-3hd.png', NULL, NULL, 1, 1),
    (N'盛伯骥', N'视觉艺术家', N'艺术顾问', N'顾问团队', 1051, N'/Content/ArtImg/Artist/bac65c41-b7be-40ab-aad3-7178cc1b6062.png', NULL, NULL, 2, 1);
END
-- END SEED OrgMember
GO

------------------------------------------------------------------------------
-- 2. SaleWork：在售作品（首页 在售艺术品 轮播）
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.SaleWork', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SaleWork
    (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SaleWork PRIMARY KEY,
        ArtistId    INT            NULL,                                   -- 可选：关联 Artist.Id
        ArtistName  NVARCHAR(100)  NULL,                                   -- 显示为「某某 作品」
        Title       NVARCHAR(200)  NOT NULL,
        Size        NVARCHAR(100)  NULL,
        Medium      NVARCHAR(100)  NULL,
        ImagePath   NVARCHAR(300)  NULL,                                   -- 原图 /Content/... ；页面自动用 q30 预览并保留原图给放大镜
        ImageWidth  INT            NULL,
        ImageHeight INT            NULL,
        Sort        INT            NOT NULL CONSTRAINT DF_SaleWork_Sort DEFAULT (0),
        IsVisible   BIT            NOT NULL CONSTRAINT DF_SaleWork_IsVisible DEFAULT (1)
    );
    CREATE INDEX IX_SaleWork_Sort ON dbo.SaleWork (Sort);
END
GO

-- BEGIN SEED SaleWork
IF NOT EXISTS (SELECT 1 FROM dbo.SaleWork)
BEGIN
    INSERT INTO dbo.SaleWork (ArtistId, ArtistName, Title, Size, Medium, ImagePath, ImageWidth, ImageHeight, Sort, IsVisible) VALUES
    (1049, N'陈硕子', N'冲气', N'85x58cm', N'纸本设色', N'/Content/ArtImg/selling/cxz.jpg', 1726, 1224, 1, 1),
    (1012, N'王玉波', N'繁花系列', N'60x60cm', N'布面丙烯', N'/Content/ArtImg/selling/wyb.jpg', 1224, 1238, 2, 1),
    (1011, N'徐赓良', N'喵的网络', N'45x45cm', N'水墨', N'/Content/ArtImg/selling/xgl.jpg', 1226, 1224, 3, 1),
    (1000, N'张岚芊', N'山海经-7', N'88x43cm', N'综合材料', N'/Content/ArtImg/selling/zlq1.jpg', 1663, 3350, 4, 1),
    (1000, N'张岚芊', N'山海经-2', N'88x43cm', N'综合材料', N'/Content/ArtImg/selling/zlq2.jpg', 2392, 3345, 5, 1),
    (1048, N'左锋义', N'梵之初系列之55', N'100x100cm', N'纸本设色（综合材料）', N'/Content/ArtImg/selling/zfy.jpg', 1224, 1246, 6, 1);
END
-- END SEED SaleWork
GO

------------------------------------------------------------------------------
-- 3. Partner：合作伙伴（关于我们 页）
------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Partner', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Partner
    (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Partner PRIMARY KEY,
        Name        NVARCHAR(200)  NOT NULL,                               -- 名称里的换行会输出为 <br />
        LogoPath    NVARCHAR(300)  NULL,
        ImageWidth  INT            NULL,
        ImageHeight INT            NULL,
        Link        NVARCHAR(500)  NULL,                                   -- 可选 http(s) 链接
        Description NVARCHAR(MAX)  NULL,                                   -- 简介（桌面端显示）
        Sort        INT            NOT NULL CONSTRAINT DF_Partner_Sort DEFAULT (0),
        IsVisible   BIT            NOT NULL CONSTRAINT DF_Partner_IsVisible DEFAULT (1)
    );
    CREATE INDEX IX_Partner_Sort ON dbo.Partner (Sort);
END
GO

-- BEGIN SEED Partner
IF NOT EXISTS (SELECT 1 FROM dbo.Partner)
BEGIN
    INSERT INTO dbo.Partner (Name, LogoPath, ImageWidth, ImageHeight, Link, Description, Sort, IsVisible) VALUES
    (N'荷兰白立方基金会', N'/Content/images/white cube.png', 800, 800, NULL, N'白立方基金会以举办每两年一次的地球村艺术项目而闻名，“会面”是其长久以来的主题，即来自世界各地的艺术家与公众会面。' + NCHAR(10) + NCHAR(10) + N'项目于荷兰、法国、德国、丹麦、阿塞拜疆和北马其顿等地举办。' + NCHAR(10) + NCHAR(10) + N'除了这个项目，白立方还组织了其他主要在欧盟范围内的展览和项目。促进艺术家与公众之间的文化交流。' + NCHAR(10) + NCHAR(10) + N'荷兰白立方拥有全世界1500名专业的艺术家，同时拥有可以接触到24000名艺术家的地球村艺术家网络，以发挥其巨大的艺术影响力。', 1, 1),
    (N'游戏室画廊', N'/Content/images/play room.png', 800, 800, NULL, N'PLAY ROOM 的发起人是荷兰知名策展人Jeroen van Paassen，他在 PLAY room 汇集了20多年在艺术和文化领域的经验。' + NCHAR(10) + NCHAR(10) + N'从开发新概念和策划展览到收藏分析和管理。PLAY room 是临近荷兰首都阿姆斯特丹的小城市赞丹市中心的艺术热点。' + NCHAR(10) + NCHAR(10) + N'画廊的空间极具特色，展厅风格充满激情，具有幽默感而又平易近人，反差感时常令人感到诧异。' + NCHAR(10) + NCHAR(10) + N'画廊每月都有从当代艺术到街头艺术的各种风格迥异的展览。', 2, 1),
    (N'华汇中艺国际拍卖有限公司', N'/Content/images/huahui.png', 800, 800, NULL, N'北京华汇中艺国际拍卖有限公司成立于2017年，是以经营文物艺术品为主的综合性拍卖公司。' + NCHAR(10) + NCHAR(10) + N'公司成立以来，已与多家国内顶尖拍卖公司、国内知名艺术品投资机构、大型银行私人银行部建立起良好的合作关系，' + NCHAR(10) + NCHAR(10) + N'为其提供艺术品拍卖专场合作、媒体宣传推广、艺术品藏家接洽、艺术品投资讲座等全方位服务。公司主营业务艺术品拍卖、艺术空间运营以及艺术品私人洽购。', 3, 1),
    (N'深圳反转舞台' + NCHAR(10) + N'文化传播有限公司', N'/Content/images/fanzhuan.png', 800, 800, NULL, N'深圳反转舞台文化传播有限公司' + NCHAR(10) + N'是一家立足于深圳的舞台剧、影视剧、艺术展览策划企业，倡导多元文化、青年创意、轻文艺生活，努力为公众奉上多样化、高品格的文化娱乐作品。' + NCHAR(10) + NCHAR(10) + N'反转舞台亦常年致力于艺术教育，曾与福田文化馆、南山文化馆、深圳滨海艺术中心等单位合作。目前正在运营的课程有即兴喜剧工作坊、多维喜剧创新坊、集体编作戏剧工作坊等。', 4, 1);
END
-- END SEED Partner
GO
GO

-- ===== 来源: 2026-10-26-exhibition-automation.sql；SHA256: 59a1acd2f7e0f59a35e3df277588d6a28ea04eda3f560bad4c1e47f305d4899e =====
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
GO

-- ===== 来源: 2026-10-26-artist-site.sql；SHA256: 8ee7b770fe09f84e8d47bb53f9d8fafb61b399a69d3fbc4334eb82012283bf6b =====
-- 艺术家官网总览与展讯数据化（T4）。本脚本由用户在发布前手动执行；agent 不对生产数据库执行写入。
-- 幂等：表/列/index 使用存在性检查，重复执行安全。
-- 执行位置：公开读库与后台写库（生产同库；本地开发请执行在可写 SQLite 镜像的迁移流程，不执行此 SQL）。

SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.Works', N'Sort') IS NULL
    ALTER TABLE dbo.Works ADD Sort INT NOT NULL CONSTRAINT DF_Works_Sort_T4 DEFAULT (0);
GO

IF COL_LENGTH(N'dbo.Artist', N'HideDefaultExhibitions') IS NULL
    ALTER TABLE dbo.Artist ADD HideDefaultExhibitions BIT NOT NULL CONSTRAINT DF_Artist_HideDefaultExhibitions_T4 DEFAULT (0);
GO

IF OBJECT_ID(N'dbo.ArtistPublication', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArtistPublication
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ArtistPublication PRIMARY KEY,
        ArtistId      INT NOT NULL,
        PublicationId INT NOT NULL,
        Sort          INT NOT NULL CONSTRAINT DF_ArtistPublication_Sort DEFAULT (0)
    );
    CREATE INDEX IX_ArtistPublication_Artist_Sort ON dbo.ArtistPublication (ArtistId, Sort, Id);
    CREATE INDEX IX_ArtistPublication_Publication ON dbo.ArtistPublication (PublicationId);
END
GO
GO

-- ===== 来源: 2026-10-26-artist-books.sql；SHA256: e7760427f8ed2d268d3654f9e2aa277db8ba7e6ec9e1dab6ed12476bbe4ca1eb =====
-- 艺术家「出版著作」数据化。本脚本由用户在发布前手动执行；agent 不对生产数据库执行写入。
-- 幂等：表/index 使用存在性检查，重复执行安全。纯加性（只新建 dbo.ArtistBook），不改动任何既有表。
-- 执行位置：公开读库与后台写库（生产同库；本地开发请执行在可写 SQLite 镜像的迁移流程，不执行此 SQL）。
-- 未执行本脚本时：前台 Art 侧栏「出版著作」显示为空、ArtArchive 保持「暂无出版著作资料」，后台模块显示「需先执行 SQL」。

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.ArtistBook', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArtistBook
    (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ArtistBook PRIMARY KEY,
        ArtistId    INT NOT NULL,
        Title       NVARCHAR(200) NOT NULL,
        Publisher   NVARCHAR(200) NULL,
        PublishYear NVARCHAR(50) NULL,
        CoverPath   NVARCHAR(300) NULL,
        Description NVARCHAR(MAX) NULL,
        Sort        INT NOT NULL CONSTRAINT DF_ArtistBook_Sort DEFAULT (0),
        EditRecord  NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_ArtistBook_Artist_Sort ON dbo.ArtistBook (ArtistId, Sort, Id);
END
GO
GO

-- ===== 来源: 2026-10-26-vip-order.sql；SHA256: 9c063f1a021231fe49d467dcb4665524aec3be4f162fd1e7fb91f868c9764e63 =====
-- 会员风采（/Home/Vip）排序与显隐。本脚本由用户在发布前手动执行；agent 不对生产数据库执行写入。
-- 幂等：表使用存在性检查，重复执行安全。纯加性（只新建 dbo.ArtistDisplay），不改动 Artist 等任何既有表。
-- 执行位置：公开读库与后台写库（生产同库；本地开发请执行在可写 SQLite 镜像的迁移流程，不执行此 SQL）。
-- 未执行本脚本时：前台 Vip 页保持原顺序（Id 倒序、无隐藏），后台「会员风采排序」显示「需先执行 SQL」。
-- 每位艺术家最多一行：有行的按 Sort 升序排在前面，没有行的按原 Id 倒序排在后面；IsHidden=1 只在 Vip 列表隐藏，
-- 艺术家官网页本身仍可访问。

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.ArtistDisplay', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ArtistDisplay
    (
        ArtistId INT NOT NULL CONSTRAINT PK_ArtistDisplay PRIMARY KEY,
        Sort     INT NOT NULL CONSTRAINT DF_ArtistDisplay_Sort DEFAULT (0),
        IsHidden BIT NOT NULL CONSTRAINT DF_ArtistDisplay_IsHidden DEFAULT (0)
    );
END
GO
GO

IF XACT_STATE() <> 1
    THROW 51002, N'升级事务不可提交，停止并回滚后核对错误。', 1;
COMMIT TRANSACTION;
PRINT N'后台自动化增量结构与种子已提交。请继续应用切换验证。';
GO

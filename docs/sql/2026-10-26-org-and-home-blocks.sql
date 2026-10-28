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
    (N'唐岐山', N'副会长', N'艺术家', N'核心成员', 1052, N'/Content/ArtImg/Artist/e1c34366-d745-4064-a377-4879b1693835.png', NULL, NULL, 4, 1),
    (N'陈硕子', N'副会长', N'艺术家', N'核心成员', 1049, N'/Content/ArtImg/Artist/629c4dd2-1960-49dc-91ba-abeaf8f7373d.jpg', NULL, NULL, 5, 1),
    (N'王玉波', N'监事', N'艺术家', N'核心成员', 1012, N'/Content/images/to black/vip-5wyb.png', NULL, NULL, 6, 1),
    (N'沈敬东', N'会员', N'艺术家', N'核心成员', 1015, N'/Content/images/to black/vip-7sjd.png', 824, 824, 7, 1),
    (N'程晓光', N'策展人', N'艺术家', N'核心成员', 1014, N'/Content/images/to black/vip-6cxg.png', NULL, NULL, 8, 1),
    (N'邓国源', N'会员', N'艺术家', N'核心成员', 1017, N'/Content/images/to black/vip-9dgy.png', NULL, NULL, 9, 1),
    (N'祁志龙', N'会员', N'艺术家', N'核心成员', 1019, N'/Content/images/to black/vip-11qzl.png', 800, 800, 10, 1),
    (N'杨宽', N'会员', N'考古学家', N'核心成员', 1021, N'/Content/images/to black/vip-12yk.png', 800, 800, 11, 1),
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

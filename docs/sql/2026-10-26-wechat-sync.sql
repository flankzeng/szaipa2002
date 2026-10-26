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

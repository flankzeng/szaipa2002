-- 现场编辑（T6）：站点文案表。模板里原本写死的标题/段落按 Key 保存可改的值；
-- 没有记录时页面显示模板原文，所以建表本身不改变任何页面。幂等，可重复执行。
IF OBJECT_ID(N'dbo.SiteText', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SiteText (
        [Key] nvarchar(200) NOT NULL CONSTRAINT PK_SiteText PRIMARY KEY,
        [Value] nvarchar(max) NOT NULL,
        UpdatedAt datetime2 NULL,
        UpdatedBy nvarchar(100) NULL
    );
END
GO

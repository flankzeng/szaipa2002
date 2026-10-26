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

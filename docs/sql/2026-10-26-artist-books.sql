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

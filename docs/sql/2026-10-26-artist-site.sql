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

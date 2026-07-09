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

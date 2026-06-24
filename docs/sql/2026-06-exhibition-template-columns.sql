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

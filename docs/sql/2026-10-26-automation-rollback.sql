-- 后台自动化回退盘点。默认只读；由用户在正确数据库执行，agent 不执行。
-- 推荐回退应用目录，保留所有新增表/列/数据。此脚本不删除 News、Content 或密钥。
-- 若确需结构回退：先停所有后台/同步写入，保留发布前和当前数据库备份。
-- 确认下列对象均由本次发布新建、没有发布前既存对象后，再显式设置 @RemoveEmptySchema=1。
-- 非空新增表一律拒绝删除，需由用户先决定数据保留/恢复方案；不能为了让脚本通过而清空数据。
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ExpectedDatabase sysname = N'Szaipa';
DECLARE @RemoveEmptySchema bit = 0;
IF DB_NAME() <> @ExpectedDatabase
    THROW 51010, N'目标数据库不匹配，已停止。', 1;

DECLARE @Tables table (Name sysname NOT NULL);
INSERT @Tables(Name) VALUES
    (N'ArtistPublication'), (N'PublicationBlock'), (N'FeaturedExhibition'),
    (N'OrgMember'), (N'SaleWork'), (N'Partner'), (N'WeChatSyncRecord'), (N'ArtistBook');

DECLARE @Name sysname, @Rows bigint, @Sql nvarchar(max), @HasData bit = 0;
DECLARE table_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT Name FROM @Tables;
OPEN table_cursor;
FETCH NEXT FROM table_cursor INTO @Name;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + @Name, N'U') IS NOT NULL
    BEGIN
        SET @Sql = N'SELECT @Count = COUNT_BIG(*) FROM dbo.' + QUOTENAME(@Name) + N';';
        EXEC sys.sp_executesql @Sql, N'@Count bigint OUTPUT', @Count = @Rows OUTPUT;
        SELECT @Name AS TableName, @Rows AS RowsToPreserve;
        IF @Rows > 0 SET @HasData = 1;
    END;
    FETCH NEXT FROM table_cursor INTO @Name;
END;
CLOSE table_cursor;
DEALLOCATE table_cursor;

SELECT N'Works.Sort' AS ColumnName, COL_LENGTH(N'dbo.Works', N'Sort') AS ColumnBytes
UNION ALL SELECT N'Artist.HideDefaultExhibitions', COL_LENGTH(N'dbo.Artist', N'HideDefaultExhibitions')
UNION ALL SELECT N'Publication.ThemeColor', COL_LENGTH(N'dbo.Publication', N'ThemeColor')
UNION ALL SELECT N'Publication.BannerPath', COL_LENGTH(N'dbo.Publication', N'BannerPath');

-- 即使新增表为空，既有表中的新增列也可能已编辑；非默认值同样必须保留。
DECLARE @Columns table (TableName sysname, ColumnName sysname, MeaningfulFilter nvarchar(200));
INSERT @Columns VALUES
    (N'Works', N'Sort', N'[Sort] <> 0'),
    (N'Artist', N'HideDefaultExhibitions', N'[HideDefaultExhibitions] <> 0'),
    (N'Publication', N'ThemeColor', N'NULLIF(LTRIM(RTRIM([ThemeColor])), N'''') IS NOT NULL'),
    (N'Publication', N'BannerPath', N'NULLIF(LTRIM(RTRIM([BannerPath])), N'''') IS NOT NULL');
DECLARE @ColumnTable sysname, @Column sysname, @Filter nvarchar(200);
DECLARE column_data_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, ColumnName, MeaningfulFilter FROM @Columns;
OPEN column_data_cursor;
FETCH NEXT FROM column_data_cursor INTO @ColumnTable, @Column, @Filter;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF COL_LENGTH(N'dbo.' + @ColumnTable, @Column) IS NOT NULL
    BEGIN
        SET @Sql = N'SELECT @Count = COUNT_BIG(*) FROM dbo.' + QUOTENAME(@ColumnTable) + N' WHERE ' + @Filter;
        EXEC sys.sp_executesql @Sql, N'@Count bigint OUTPUT', @Count = @Rows OUTPUT;
        SELECT @ColumnTable + N'.' + @Column AS ColumnName, @Rows AS EditedRowsToPreserve;
        IF @Rows > 0 SET @HasData = 1;
    END;
    FETCH NEXT FROM column_data_cursor INTO @ColumnTable, @Column, @Filter;
END;
CLOSE column_data_cursor;
DEALLOCATE column_data_cursor;

IF @RemoveEmptySchema = 0
BEGIN
    PRINT N'只读盘点完成：未删除任何对象。应用回退请保留这些增量结构。';
    RETURN;
END;
IF @HasData = 1
    THROW 51011, N'新增表或列已有数据，拒绝拆除。保留数据并只回退应用。', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    -- 不拆除本 Epic 之前已有的 Type/Preface/Signature 与 ExhibitionWork。
    DROP TABLE IF EXISTS dbo.ArtistPublication;
    DROP TABLE IF EXISTS dbo.PublicationBlock;
    DROP TABLE IF EXISTS dbo.FeaturedExhibition;
    DROP TABLE IF EXISTS dbo.OrgMember;
    DROP TABLE IF EXISTS dbo.SaleWork;
    DROP TABLE IF EXISTS dbo.Partner;
    DROP TABLE IF EXISTS dbo.WeChatSyncRecord;
    DROP TABLE IF EXISTS dbo.ArtistBook;

    -- 默认约束名可由既存 schema 决定，因此先按列查系统目录，不硬编码约束名。
    DECLARE @TableName sysname, @ColumnName sysname, @ConstraintName sysname;
    DECLARE column_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, ColumnName FROM @Columns;
    OPEN column_cursor;
    FETCH NEXT FROM column_cursor INTO @TableName, @ColumnName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @ConstraintName = NULL;
        SELECT @ConstraintName = dc.name
        FROM sys.default_constraints dc JOIN sys.columns c
          ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'dbo.' + @TableName) AND c.name = @ColumnName;
        IF @ConstraintName IS NOT NULL
        BEGIN
            SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TableName) + N' DROP CONSTRAINT ' + QUOTENAME(@ConstraintName);
            EXEC sys.sp_executesql @Sql;
        END;
        IF COL_LENGTH(N'dbo.' + @TableName, @ColumnName) IS NOT NULL
        BEGIN
            SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TableName) + N' DROP COLUMN ' + QUOTENAME(@ColumnName);
            EXEC sys.sp_executesql @Sql;
        END;
        FETCH NEXT FROM column_cursor INTO @TableName, @ColumnName;
    END;
    CLOSE column_cursor;
    DEALLOCATE column_cursor;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

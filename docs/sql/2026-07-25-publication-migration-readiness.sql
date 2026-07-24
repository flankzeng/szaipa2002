-- READ-ONLY readiness report for the fourteen retired publication slugs.
-- Safe for a db_datareader connection: no DDL/DML and no schema or data mutation.

SET NOCOUNT ON;

SELECT
    N'dbo.Publication table' AS CheckName,
    CASE WHEN OBJECT_ID(N'dbo.Publication', N'U') IS NULL THEN 0 ELSE 1 END AS IsReady;

IF OBJECT_ID(N'dbo.Publication', N'U') IS NULL
    RETURN;

SELECT
    required.ColumnName,
    CASE WHEN COL_LENGTH(N'dbo.Publication', required.ColumnName) IS NULL THEN 0 ELSE 1 END AS IsReady
FROM (VALUES
    (N'Type'),
    (N'Preface'),
    (N'Signature')
) AS required(ColumnName)
ORDER BY required.ColumnName;

SELECT
    N'dbo.ExhibitionWork table (optional for Type=0 slug rows)' AS CheckName,
    CASE WHEN OBJECT_ID(N'dbo.ExhibitionWork', N'U') IS NULL THEN 0 ELSE 1 END AS IsReady;

SELECT Id, TitleCN, FolderName, Status
FROM dbo.Publication
WHERE Id BETWEEN 92001 AND 92015
ORDER BY Id;

SELECT
    COUNT(*) AS OccupiedReservedIds,
    CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END AS IsReady
FROM dbo.Publication
WHERE Id BETWEEN 92001 AND 92015;

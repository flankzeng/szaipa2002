-- Slug page retirement (Views/Publication/*.cshtml -> data-driven Publication rows).
-- See docs/updates/2026-07-09-publication-slug-migration.md for the full slug -> Id -> FolderName table
-- and known image-folder caveats before running this.
--
-- SCOPE: 14 of the 15 originally-considered slugs. `zengfeng` is EXCLUDED — it is not an image-gallery
-- page at all (it's a self-contained flipbook mini-site under /Content/publication/zengfeng/, Layout=null),
-- so it cannot be represented by the Publication/_ExhibitionGallery data model. Its controller action and
-- view were left untouched; do not insert a row for it here.
--
-- RESERVED ID RANGE: 92001-92015 (92004 intentionally left unused/reserved for zengfeng, not inserted).
-- This range was chosen to be well clear of the live auto-increment sequence. BEFORE running this against
-- the production/writable database, confirm 92001-92015 are not already occupied:
--   SELECT Id FROM dbo.Publication WHERE Id BETWEEN 92001 AND 92015;
-- (should return 0 rows). If any collide, stop and review the fixed route/Id mapping before proceeding.
--
-- Prerequisite: docs/sql/2026-06-exhibition-template-columns.sql (adds Type/Preface/Signature) must already
-- be applied — Type is set to 0 (普通/simple gallery) for every row below.
--
-- IMPORTANT — deploy the matching application code before inserting these rows:
-- LegacyPublicationGalleryCatalog preserves the exact 604 historical image paths used by these fourteen
-- pages, including mixed extensions, gaps and historical filename typos. It lets both Swipers reuse the
-- existing external Content tree without copying, renaming or recompressing images. Older application builds
-- still assume a contiguous 10000/10001 convention and are not compatible with the rows below.
--
-- Transactional and fail-closed: any occupied reserved Id or missing prerequisite aborts before INSERT;
-- any INSERT failure rolls the transaction back. A successful second run also stops without changing data.
-- Run only against a local writable copy or the real writable DB during an approved deploy window —
-- never against the read-only production connection.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Publication', N'U') IS NULL
    THROW 51000, 'Required table dbo.Publication does not exist.', 1;

IF COL_LENGTH(N'dbo.Publication', N'Type') IS NULL
   OR COL_LENGTH(N'dbo.Publication', N'Preface') IS NULL
   OR COL_LENGTH(N'dbo.Publication', N'Signature') IS NULL
    THROW 51001, 'Apply docs/sql/2026-06-exhibition-template-columns.sql before the slug migration.', 1;

IF EXISTS (SELECT 1 FROM dbo.Publication WHERE Id BETWEEN 92001 AND 92015)
    THROW 51002, 'Reserved Publication Id range 92001-92015 is occupied; no rows were changed.', 1;

BEGIN TRY
    BEGIN TRANSACTION;
    SET IDENTITY_INSERT dbo.Publication ON;

    INSERT INTO dbo.Publication
    (Id, TitleCN, TitleEN, StartDate, EndDate, FolderName, MaxImg, Location, zhuban, chengban, xieban, [Type], Status)
VALUES
    -- 92001: tonggouEurope — 同构——欧洲行 当代艺术展
    (92001, N'同构——欧洲行 当代艺术展', N'Isomorphism - Contemporary Art Exhibition (European Tour)',
     '2023-10-09', '2023-10-23', N'tonggouEurope', 29, N'法国/德国/荷兰/比利时', NULL, NULL, NULL, 0, 0),

    -- 92002: shuimai — 2023龙游水脉艺术节
    (92002, N'2023龙游水脉艺术节', N'LONGYOU LIQUID THREADS ART FESTIVAL',
     '2023-09-26', '2023-12-26', N'shuimai', 29, N'浙江省衢州市龙游县', NULL, NULL, NULL, 0, 0),

    -- 92003: chunyu — 春语·当代艺术名家邀请展
    (92003, N'春语·当代艺术名家邀请展', N'Contemporary Art masters Invitational exhibition',
     '2022-03-29', '2022-04-29', N'chunyu', 11, N'深圳动漫园-盛世艺术空间', NULL, NULL, NULL, 0, 0),

    -- 92004 reserved for zengfeng — NOT inserted, see notes above.

    -- 92005: zhongyi — 中意艺术名画展
    (92005, N'中意艺术名画展', N'Famous Chinese and Italian Art Paintings Exhibition',
     '2021-09-24', '2021-10-24', N'zhongyi', 23, N'深圳市龙华大浪时尚小镇 INPARK文化创意园', NULL, NULL, NULL, 0, 0),

    -- 92006: tonggou — 同构——当代艺术作品邀请展
    (92006, N'同构——当代艺术作品邀请展', N'"isomorphism" Contemporary Art Exhibition of China',
     '2021-01-26', '2021-02-26', N'tonggou', 22, N'罗湖美术馆', NULL, NULL, NULL, 0, 0),

    -- 92007: chunyu2 — 春语第二季——国际视觉艺术邀请展
    (92007, N'春语第二季——国际视觉艺术邀请展', N'"SOUND OF SPRING II" international Visual Art Invitation Exhibition',
     NULL, '2023-05-08', N'chunyu2', 16, N'南山漫谷 - 同构艺术空间', NULL, NULL, NULL, 0, 0),

    -- 92008: trio — 三人行——鸥洋/雷双/张岚芊艺术展
    (92008, N'三人行——鸥洋/雷双/张岚芊艺术展', N'"TRIO" —— Ou Yang / Lei Shuang / Zhang LanQian Art Exhibition',
     '2023-06-03', '2023-07-02', N'trio', 17, N'深圳市木星美术馆', NULL, NULL, NULL, 0, 0),

    -- 92009: man — 漫MAN-艺术时尚先锋展
    (92009, N'漫MAN-艺术时尚先锋展', N'PIONEER ART AND FASHION EXHIBITION',
     '2024-05-22', '2024-05-30', N'man', 24, N'南山·漫谷 同构艺术空间', N'润杨集团·南山漫谷 深圳市艺术产业促进会', N'同构艺术空间', NULL, 0, 0),

    -- 92010: yijia — 艺+科技新潮流展
    (92010, N'艺+科技新潮流展', N'ART PLUS - ART FASHION & TECHNOLOGY',
     '2024-05-24', '2024-05-31', N'yijia', 69, N'龙华锦绣科学园三期D栋1层展厅', N'润杨集团·锦绣科学园 深圳市艺术产业促进会', NULL,
     N'深圳市锦绣大地投资有限公司 深圳市仓颉通文化传播有限公司', 0, 0),

    -- 92011: zhongri — 同构——中日艺术交流展
    (92011, N'同构——中日艺术交流展', N'ISOMORPHISM - CHINA & JAPAN ART COMMUNICATION EXHIBITION',
     '2024-06-28', NULL, N'zhongri', 56, N'東京都千代田区 ART SPACE 89', N'深圳市艺术产业促进会', NULL,
     N'东京印社 深圳市仓颉通文化传播有限公司', 0, 0),

    -- 92012: shuyuyi — "数"与"艺"——新文艺群体创作成果展
    (92012, N'"数"与"艺"——新文艺群体创作成果展', N'DIGITAL AND ART - NEW ARTISTIC GROUP CREATION RESULTS EXHIBITION',
     '2024-09-20', NULL, N'shuyuyi', 130, NULL, NULL, NULL, NULL, 0, 0),

    -- 92013: chunyu4 — 春语第四季——当代艺术作品邀请展
    (92013, N'春语第四季——当代艺术作品邀请展', N'Sound of Spring season 4 —— Contemporary Art Invitational Exhibition',
     NULL, '2025-05-25', N'chunyu4', 95, NULL, NULL, NULL, NULL, 0, 0),

    -- 92014: zhongfa — 深圳-法国国际当代艺术展2025
    (92014, N'深圳-法国国际当代艺术展2025', N'Shenzhen - Exposition internationale d''art contemporain français',
     NULL, '2025-08-30', N'zhongfa', 44, NULL, NULL, NULL, NULL, 0, 0),

    -- 92015: tangqishan — 入骨相知——唐岐山当代艺术展
    (92015, N'入骨相知——唐岐山当代艺术展', N'Soul-Deep Resonance —— Tang Qishan: A Contemporary Art Exhibition',
     NULL, '2025-09-30', N'tangqishan', 25, NULL, NULL, NULL, NULL, 0, 0);

    IF @@ROWCOUNT <> 14
        THROW 51003, 'Expected to insert exactly 14 Publication rows.', 1;

    IF (SELECT COUNT(*) FROM dbo.Publication WHERE Id BETWEEN 92001 AND 92015) <> 14
       OR EXISTS (SELECT 1 FROM dbo.Publication WHERE Id = 92004)
        THROW 51004, 'Post-insert Publication Id verification failed.', 1;

    SET IDENTITY_INSERT dbo.Publication OFF;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    SET IDENTITY_INSERT dbo.Publication OFF;
    THROW;
END CATCH;

SELECT Id, TitleCN, FolderName, MaxImg, [Type], Status
FROM dbo.Publication
WHERE Id BETWEEN 92001 AND 92015
ORDER BY Id;

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
-- (should return 0 rows). If any collide, stop and renumber before proceeding.
--
-- Prerequisite: docs/sql/2026-06-exhibition-template-columns.sql (adds Type/Preface/Signature) must already
-- be applied — Type is set to 0 (普通/simple gallery) for every row below.
--
-- IMPORTANT — image folder caveat (read before publishing):
-- The public gallery template (Views/Shared/_ExhibitionGallery.cshtml) and the admin gallery manager
-- (Szaipa.Data/Services/Admin/ExhibitionGalleryFolder.cs) both hard-require the images to be a *contiguous*
-- run of `10001.jpg, 10002.jpg, ...` (optionally with a `10000.jpg` cover) under /Content/images/{FolderName}/.
-- MaxImg below is stored as (image count - 1), which is what the folder SHOULD contain once correctly
-- numbered — but as-ported today several of these folders do NOT match that convention and will show
-- broken/missing images until someone renumbers the files on disk:
--   - tonggouEurope / shuyuyi / chunyu4 / zhongfa / tangqishan: files are numbered from 100000 (six digits),
--     not 10001. Needs a full renumber to 10001+.
--   - shuimai: files are already 10001-based but have GAPS (10007/10009/10010/10019 missing) and some are
--     .png instead of .jpg (10020/10030/10031/10032/10033). Needs gap-filling/re-export + a renumber pass.
--   - chunyu: files are named 01.jpg..012.jpg (no 10001 prefix at all). Needs a full re-export/renumber.
--   - chunyu2 / trio: mostly 10001-based but the file names roll over incorrectly after 10009
--     (100010.jpg, 100011.jpg, ... instead of 10010.jpg, 10011.jpg, ...). Needs a rename pass for the tail.
-- Folders that already match the convention cleanly: zhongyi (offset by one, starts at 10000 — minor,
-- acceptable), tonggou, man, yijia, zhongri.
-- None of this is a DB concern — it's a filesystem operation on the legacy assets (LegacyAssets:ContentRoot),
-- which this agent has no write access to and was told not to touch. Flagging here so the user can queue it.
--
-- Safe to run once. Run only against a local writable copy or the real writable DB during a deploy window —
-- never against the read-only production connection.

SET IDENTITY_INSERT dbo.Publication ON;

INSERT INTO dbo.Publication
    (Id, TitleCN, TitleEN, StartDate, EndDate, FolderName, MaxImg, Location, zhuban, chengban, xieban, [Type], Status)
VALUES
    -- 92001: tonggouEurope — 同构——欧洲行 当代艺术展 (needs image renumber, see caveat above)
    (92001, N'同构——欧洲行 当代艺术展', N'Isomorphism - Contemporary Art Exhibition (European Tour)',
     '2023-10-09', '2023-10-23', N'tonggouEurope', 29, N'法国/德国/荷兰/比利时', NULL, NULL, NULL, 0, 0),

    -- 92002: shuimai — 2023龙游水脉艺术节 (needs gap-fill + image renumber, see caveat above)
    (92002, N'2023龙游水脉艺术节', N'LONGYOU LIQUID THREADS ART FESTIVAL',
     '2023-09-26', '2023-12-26', N'shuimai', 29, N'浙江省衢州市龙游县', NULL, NULL, NULL, 0, 0),

    -- 92003: chunyu — 春语·当代艺术名家邀请展 (needs full image re-export/renumber, see caveat above)
    (92003, N'春语·当代艺术名家邀请展', N'Contemporary Art masters Invitational exhibition',
     '2022-03-29', '2022-04-29', N'chunyu', 11, N'深圳动漫园-盛世艺术空间', NULL, NULL, NULL, 0, 0),

    -- 92004 reserved for zengfeng — NOT inserted, see notes above.

    -- 92005: zhongyi — 中意艺术名画展 (images start at 10000 not 10001, minor/acceptable)
    (92005, N'中意艺术名画展', N'Famous Chinese and Italian Art Paintings Exhibition',
     '2021-09-24', '2021-10-24', N'zhongyi', 23, N'深圳市龙华大浪时尚小镇 INPARK文化创意园', NULL, NULL, NULL, 0, 0),

    -- 92006: tonggou — 同构——当代艺术作品邀请展 (clean fit)
    (92006, N'同构——当代艺术作品邀请展', N'"isomorphism" Contemporary Art Exhibition of China',
     '2021-01-26', '2021-02-26', N'tonggou', 22, N'罗湖美术馆', NULL, NULL, NULL, 0, 0),

    -- 92007: chunyu2 — 春语第二季——国际视觉艺术邀请展 (needs tail rename, see caveat above)
    (92007, N'春语第二季——国际视觉艺术邀请展', N'"SOUND OF SPRING II" international Visual Art Invitation Exhibition',
     NULL, '2023-05-08', N'chunyu2', 16, N'南山漫谷 - 同构艺术空间', NULL, NULL, NULL, 0, 0),

    -- 92008: trio — 三人行——鸥洋/雷双/张岚芊艺术展 (needs tail rename, see caveat above)
    (92008, N'三人行——鸥洋/雷双/张岚芊艺术展', N'"TRIO" —— Ou Yang / Lei Shuang / Zhang LanQian Art Exhibition',
     '2023-06-03', '2023-07-02', N'trio', 17, N'深圳市木星美术馆', NULL, NULL, NULL, 0, 0),

    -- 92009: man — 漫MAN-艺术时尚先锋展 (clean fit)
    (92009, N'漫MAN-艺术时尚先锋展', N'PIONEER ART AND FASHION EXHIBITION',
     '2024-05-22', '2024-05-30', N'man', 24, N'南山·漫谷 同构艺术空间', N'润杨集团·南山漫谷 深圳市艺术产业促进会', N'同构艺术空间', NULL, 0, 0),

    -- 92010: yijia — 艺+科技新潮流展 (clean fit)
    (92010, N'艺+科技新潮流展', N'ART PLUS - ART FASHION & TECHNOLOGY',
     '2024-05-24', '2024-05-31', N'yijia', 69, N'龙华锦绣科学园三期D栋1层展厅', N'润杨集团·锦绣科学园 深圳市艺术产业促进会', NULL,
     N'深圳市锦绣大地投资有限公司 深圳市仓颉通文化传播有限公司', 0, 0),

    -- 92011: zhongri — 同构——中日艺术交流展 (clean fit)
    (92011, N'同构——中日艺术交流展', N'ISOMORPHISM - CHINA & JAPAN ART COMMUNICATION EXHIBITION',
     '2024-06-28', NULL, N'zhongri', 56, N'東京都千代田区 ART SPACE 89', N'深圳市艺术产业促进会', NULL,
     N'东京印社 深圳市仓颉通文化传播有限公司', 0, 0),

    -- 92012: shuyuyi — "数"与"艺"——新文艺群体创作成果展 (needs image renumber, see caveat above)
    (92012, N'"数"与"艺"——新文艺群体创作成果展', N'DIGITAL AND ART - NEW ARTISTIC GROUP CREATION RESULTS EXHIBITION',
     '2024-09-20', NULL, N'shuyuyi', 130, NULL, NULL, NULL, NULL, 0, 0),

    -- 92013: chunyu4 — 春语第四季——当代艺术作品邀请展 (needs image renumber, see caveat above)
    (92013, N'春语第四季——当代艺术作品邀请展', N'Sound of Spring season 4 —— Contemporary Art Invitational Exhibition',
     NULL, '2025-05-25', N'chunyu4', 95, NULL, NULL, NULL, NULL, 0, 0),

    -- 92014: zhongfa — 深圳-法国国际当代艺术展2025 (needs image renumber, see caveat above)
    (92014, N'深圳-法国国际当代艺术展2025', N'Shenzhen - Exposition internationale d''art contemporain français',
     NULL, '2025-08-30', N'zhongfa', 44, NULL, NULL, NULL, NULL, 0, 0),

    -- 92015: tangqishan — 入骨相知——唐岐山当代艺术展 (needs image renumber, see caveat above)
    (92015, N'入骨相知——唐岐山当代艺术展', N'Soul-Deep Resonance —— Tang Qishan: A Contemporary Art Exhibition',
     NULL, '2025-09-30', N'tangqishan', 25, NULL, NULL, NULL, NULL, 0, 0);

SET IDENTITY_INSERT dbo.Publication OFF;

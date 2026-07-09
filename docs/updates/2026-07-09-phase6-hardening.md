# 2026-07-09 Phase 6 加固（迁移收尾：授权/anti-forgery 审 + legacy 校验比对 + 补单测）

## 背景

HANDOFF.md「剩余工作」第 3 项：与 legacy 比对、anti-forgery/授权审、补单测。本次做一次
系统性审查，覆盖 `src/Szaipa.Web/Areas/Staff/Controllers/`（14 个控制器）、
`src/Szaipa.Data/Services/Admin/`（全部写仓储）、以及旧 `szaipa2022/Controllers/StaffController.cs`
（3980 行，只读比对）。

## 1. 授权覆盖审查 —— 未发现遗漏

逐个检查 14 个 Staff 控制器：

- 13 个（ArtNews/Artist/Auction/Company/Dashboard/Exhibition/Fav/News/Publication/
  TongouAtrist/TongouWorks/Upload/Works）均已在 class 级别加
  `[Authorize(Policy = AdminAuthorization.StaffPolicy)]`。
- `AccountController` 没有 class 级别特性（合理——`Login` GET/POST 需要匿名访问），
  但逐 action 核对：`Login` 正确标 `[AllowAnonymous]`；`Logout`/`PasswordChange`
  （GET+POST）均已在 action 级别加了 `[Authorize(Policy = AdminAuthorization.StaffPolicy)]`。

**结论：授权覆盖无遗漏，未做修改。**

## 2. Anti-forgery 覆盖审查 —— 未发现遗漏

逐个 grep 所有控制器的 `[HttpPost]` action（含 Create/Edit/Delete/UploadImage/Image/
Login/Logout/PasswordChange，共 30+ 处），全部紧跟 `[ValidateAntiForgeryToken]`；所有
`[HttpGet]` 读操作均未误加（符合预期）。

**结论：CSRF 防护覆盖无遗漏，未做修改。**

## 3. IOperationRecorder 日志覆盖审查 —— 未发现遗漏

检查所有 `*AdminRepository.cs` 的 Create/Update/Delete 路径：

- 独立主表仓储（News/ArtNews/Artist/Company/Publication/TongouAtrist/TongouWorks）
  ——每个 Create/Update/Delete 都直接调用 `IOperationRecorder.RecordAsync`。
- 艺术家子模块（Fav/Auction/Exhibition/Works）走泛型基类
  `ArtistScopedAdminRepository<T>`，日志记在基类的 `CreateAsync`/`UpdateAsync`/
  `DeleteAsync` 里，子类不需要重复写（之前 grep 命中数低是因为子类本身没有直接调用，
  是继承来的，逐一读源码确认过）。
- `ExhibitionWorkAdminRepository.ReplaceAsync`（参展作品目录批量替换）也调用了
  `RecordAsync`。
- `PublicationAdminRepository.SetMaxImgAsync`（画廊重排后同步 MaxImg）**不**写操作日志
  ——这是有意为之：它只是画廊图片管理器内部的图片计数同步，不是一次用户可感知的"编辑
  展览"操作，紧跟在 Create/Edit 之后同一请求内完成，本身没有独立的审计意义。已在新增
  测试里显式断言这个行为（见下）。

**结论：写操作日志覆盖无遗漏，未做修改。**

## 4. 与 legacy 比对（`szaipa2022/Controllers/StaffController.cs` 只读比对）

比对 News/ArtNews/Artist/Works/Company/Publication/Auction/Fav/Exhibition/
Tongou(Atrist/Works) 各模块的 Add/Edit action。整体发现：**legacy 后台几乎没有
服务端校验**（不做 `IsNullOrEmpty`/长度/范围检查，字段直接从 `FormCollection` 落库），
新版的 DataAnnotations（`[Required]`/`[Range]` 等）是纯新增，不存在"遗漏 legacy 已有
校验"的问题。

排查出两处疑似遗漏，逐一核实后均排除（不是真正的差距）：

1. **`Artist.WorkCount` 计数字段** —— legacy `StaffController.cs:1094`
   （`WorkAdd(FormCollection)`）在新增作品时会 `art.WorkCount = art.WorkCount + 1`，
   `:3324`（`WorkDelete`）对应减一。但这条路径属于旧后台里**已经废弃的重复写入流程**
   （`WorkAdd/WorkEdit`，图片目录 `/Works/`，额外算 Width/Height/transverse/long），
   与新 Works 模块实际对应的是另一套 `ArtWorksAdd/Edit`（图片目录 `works-narrow`，
   `StaffController.cs:463-494`）—— 这套代码**从未**维护过 `WorkCount`。
   `docs/PROJECT_MAP.md`（2026-06-24 注）已明确记录新 Works 模块是照 `ArtWorksAdd/Edit`
   迁移、有意不照搬 `WorkAdd` 的死代码逻辑。另外确认 `WorkCount` 只在旧后台
   `Views/Staff/Artist.cshtml`/`ArtistList.cshtml` 的列表列里展示，新 Artist 后台列表
   没有渲染这一列，即使补上也不会有可见效果。**排除，不修改**（继续遵循已有决策，不
   打捞已死路径的功能）。
2. **`Tag`/`WorksTag` 关联表维护** —— legacy `WorkAdd` 里维护了 `Tag`/`WorksTag`
   联表（`TagRefresh`），新 Works 仓储只把 Tags 存成一个原始字符串字段。核实
   `grep -rn "WorksTag" szaipa2022/Views/Home/*.cshtml` 无命中——没有任何存活的公开页
   读取这张联表，属于死数据结构。**排除，不修改**。

**结论：未发现需要补的校验/业务规则缺口。**（详见任务描述里 legacy 比对的调研过程，
已用 subagent 通读并交叉核实了以上两处候选项，均确认是绑定在已废弃路径上的死逻辑。）

## 5. 补单测

跑基线：`~/.dotnet/dotnet test Szaipa.Modernization.slnx` → **82/82 全绿**（PROJECT_MAP
里写的"75"已过期，实际基线是 82，含此前一次会话已补的 `ExhibitionWorkAdminRepositoryTests`）。

检查 `tests/Szaipa.Data.Tests/` 与 `src/Szaipa.Data/Services/Admin/*AdminRepository.cs`
逐一对照，发现唯一完全没有测试文件的写仓储：**`PublicationAdminRepository`**（只有
`PublicationReadRepositoryTests` 覆盖只读侧）。新增：

- **`tests/Szaipa.Data.Tests/PublicationAdminRepositoryTests.cs`**（新文件，8 个测试）：
  - `CreateAsync_persists_editable_fields_and_server_owned_fields`
  - `CreateAsync_writes_operation_record_to_diary_and_staff`
  - `UpdateAsync_changes_editable_fields_and_keeps_cover_and_logo_when_input_is_empty`
    （封面 + Logo 双图片"编辑留空不覆盖"）
  - `UpdateAsync_returns_false_when_missing`
  - `SetMaxImgAsync_updates_only_maximg_without_touching_editrecord_or_operation_log`
    （断言只改 MaxImg，不追加 EditRecord、不多写一条 Diary）
  - `SetMaxImgAsync_returns_false_when_missing`
  - `DeleteAsync_removes_row`
  - `GetPagedAsync_orders_by_id_desc_and_pages`

另外顺手给两个已有测试文件补上"更新不存在的 id 返回 false"边界用例（Create/Update/
Delete 主路径已覆盖，唯独缺这一条）：

- **`ArtNewsAdminRepositoryTests.cs`**：新增
  `UpdateAsync_returns_false_when_missing`、
  `DeleteAsync_removes_row_and_returns_false_when_missing`。
- **`TongouAdminRepositoryTests.cs`**：新增
  `Atrist_update_returns_false_when_missing`、`Works_update_returns_false_when_missing`。

## 结果

- `~/.dotnet/dotnet build Szaipa.Modernization.slnx` → **0 Warning / 0 Error**。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx` → **94/94 全绿**（82 → 94，
  新增 12 个测试：Publication 仓储 8 个 + ArtNews 2 个 + Tongou 2 个）。
- 授权 / anti-forgery / 操作日志三项覆盖审查：**均无遗漏，无需修改代码**。
- legacy 校验比对：**两处候选差距均排查后排除**（绑定在已废弃的 `WorkAdd`/`Tag`
  联表死路径上，与已记录的迁移决策一致，未新增功能代码）。

## 未改动的原因说明

本次是"查漏补缺"而非重构，审查过程中没有发现真正的授权/CSRF/日志缺口，也没有发现
遗漏的 legacy 校验规则——因此代码侧改动只有测试文件的新增，没有触碰任何生产逻辑
（仓储/控制器/视图均未修改）。这与任务要求的"改动要克制、有出处"一致：没有问题就不
制造改动。

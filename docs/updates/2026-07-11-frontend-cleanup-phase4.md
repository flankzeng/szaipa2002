# 前台清理 Phase 4：Legacy Content 白名单与归档（2026-07-11）

## 自动审计

新增非破坏性脚本 `scripts/audit-legacy-content.py`，它会：

- 扫描现代站源码中的 `/Content/*` 精确引用；
- 整类保护数据库动态路径和后台上传目录；
- 支持以后传入数据库路径导出文件 `--db-paths`；
- 支持传入生产 HTTP 日志 `--access-log`；
- 输出顶层目录分类、最大候选文件、大型受保护文件和磁盘缺失白名单。

当前运行资源根审计结果：

- 4523 个文件，总计约 1.1GB；
- 现代源码精确静态引用 264 条；
- `newsImg`、`ArtImg`、`images` 按数据库/上传动态目录整类保护；
- `Model`、`icon`、`123`、`fonts`、`CSS` 按运行依赖保护；
- 1423 个文件、约 95.9MB 进入 `review-candidate`，尚未从运行资源根删除；
- 首页唯一真实缺失引用 `images/card4.jpg` 已改为同一展览明确使用的 `DSC05345-color.jpg`，当前静态白名单无磁盘缺失。

自动报告见 `docs/updates/2026-07-11-legacy-content-audit.md`。

## 当前 Git 分支继续归档

以下目录在现代站零引用、未包含用户未提交修改，且已核实存在于本地
`legacy/archive-before-frontend-prune-20260710`：

| 路径 | 体积 | tracked 文件 | 处理 |
|---|---:|---:|---|
| `szaipa2022/Content/TempFile` | 约 31MB | 705 | 从当前分支移除 |
| `szaipa2022/Content/testfile` | 约 1.9MB | 7 | 从当前分支移除 |
| `szaipa2022/Content/publication/chunyu` | 约 73MB | 618 | 从当前分支移除；旧 MVC 路由返回 410 |

连同 Phase 2 的 zengfeng 约 140MB，旧 MVC5 Content 工作树从约 1.4GB 降至约 1.2GB。旧对象仍由 Git 历史/本地归档分支保存，因此 `.git` 对象库不会同步缩小。

## 旧依赖副本归档

继续核对仓库顶层依赖后，从当前分支移除：

| 路径 | 体积 | tracked 文件 | 依据 |
|---|---:|---:|---|
| `packages` | 约 146MB | 261 | 仅旧 MVC5 csproj 引用；现代项目使用 PackageReference |
| `szaipa2022/packages` | 约 98MB | 201 | 重复副本，没有项目引用 |
| 旧 Content `Award/CSS/js/layui/Filme` | 约 11MB | 134 | 现代源码零引用；外置服务器副本未删除 |

当前分支累计修剪约 501MB（旧 Content 约 246MB + 旧依赖/静态副本约 255MB）。
现代解决方案删除旧 packages 后仍 build 0/0、测试 94/94。

当前分支定位已经是现代化主线，旧 MVC5 项目若需要完整历史运行环境，应切换本地
`legacy/archive-before-frontend-prune-20260710`；根 NuGet packages 本身也可以重新 restore。

## 没有删除的运行资源

- 外置运行目录 `_preview`：70.8MB，像可再生预览缓存，但需要生产日志确认。
- 外置 `TempFile`：14.7MB，像二维码/临时导出，但服务器上是否有历史直链尚未确认。
- `Award`、根 `js`、`layui`、`Filme`：合计约 10.5MB，仍需日志确认。
- 所有数据库动态目录及后台上传目录均未删除。

## 重跑命令

```bash
python3 scripts/audit-legacy-content.py \
  --content-root /path/to/Content \
  --source-root src/Szaipa.Web \
  --db-paths /path/to/db-paths.txt \
  --access-log /path/to/access.log \
  --output docs/updates/legacy-content-audit.md
```

`--db-paths` 和 `--access-log` 可省略；省略时报告会明确显示计数为 0，不会假装已经完成生产验证。

# Noto Sans SC / Noto Serif SC 本地子集化（2026-07-13）

## 结果

现代站点已移除 `fonts.loli.net`、`fonts.googleapis.com` 和 `fonts.gstatic.com` 依赖，前台与 Staff 后台都使用仓库旧 Noto 源字体的本地 WOFF2 子集。为了避免外接 legacy `Site.css` 中的旧字体规则参与匹配，现代字体统一使用唯一别名：

- `Szaipa Noto Sans SC` / `Szaipa Noto Sans SC GB`
- `Szaipa Noto Serif SC` / `Szaipa Noto Serif SC GB`
- Staff 仅为 Serif 保留独立的 `Szaipa Noto Serif SC Staff` family，用于保持后台原有 600 字重匹配。

`font-subsets.css` 还覆盖 legacy `.notosans` / `.notoserif` helper，因此数据库富文本中的旧 class 也会转到本地 Szaipa Noto，不会请求已不存在的 `/Content/fonts/Noto*`。

## 字符覆盖策略

1. `scripts/font-db-codepoints.txt` 是只读生产库快照，只保存 Unicode codepoint/range，不包含任何数据库原文或凭据。
2. 快照覆盖 Szaipa + Tongou 的 129 个文本列、2,716,118 个非空值，得到 2,922 个唯一 codepoint。
3. 现代 Razor/CSS/JavaScript 源码字符与数据库快照合并后为 2,952 个 codepoint；每个正式字重的 core 实际覆盖原字体支持的 2,948 个。
4. 全部数据库 CJK 字符均在旧 Noto 原字体 cmap 中，并被纳入每个 core。4 个原字体本身不支持的 codepoint 为 `U+0308` / `U+2006` / `U+200B` / `U+200C`，都是组合或空白控制符，不是中文字形；它们与旧字体一样继续走系统 fallback。
5. 当前未出现的 GB2312 常用字共 4,698 个，每字重分成 10 个最多 512 codepoint 的 `gb00` … `gb09` 分片，只有页面新出现对应字符时才下载。
6. GB2312 范围外、且当前源码/数据库也没有的未来生僻字仍会走系统 fallback；这是为了不把 92.6MB 全量原字体重新放回发布包的明确边界。

## 字重匹配

| 旧源文件 | 现代 family | CSS `font-weight` | 与旧远程声明的关系 |
|---|---|---:|---|
| `NotoSansSC-Thin.woff2` | public/shared Sans | 100 | 保留旧 public 100；CSS 200 仍按旧规则向下匹配 100 |
| `NotoSansSC-Light.woff2` | public/shared Sans | 300 | public 与 Staff 共用 |
| `NotoSansSC-Regular.woff2` | public/shared Sans | 400 | public 与 Staff 共用 |
| `NotoSansSC-Medium.woff2` | public/shared Sans | 500 | public 与 Staff 共用 |
| `NotoSansSC-Bold.woff2` | public/shared Sans | 700 | public 与 Staff 共用 |
| `NotoSerifSC-ExtraLight.woff2` | public Serif | 100 | 保留旧 public 100 |
| `NotoSerifSC-Light.woff2` | public Serif | 300 | 保留旧 public 300 |
| `NotoSerifSC-Regular.woff2` | public Serif + Staff Serif | 400 | 同一二进制子集两个 family 复用 |
| `NotoSerifSC-Medium.woff2` | public Serif | 500 | 保留旧 public 500 |
| `NotoSerifSC-SemiBold.woff2` | Staff Serif | 600 | 只在旧 Staff Google CSS 本来就声明 600 的独立 family 中暴露 |
| `NotoSerifSC-Bold.woff2` | public Serif + Staff Serif | 700 | public CSS 600 仍按旧 public 结果匹配 700；Staff 700 保持不变 |

旧 public CSS 还声明了 900，但现代正式选择器没有请求 900，因此 Black 不进入发布资产。Staff Sans 的旧 Google 字重集与 public 已使用集完全一致，直接共用 family，不重复生成 `@font-face`。

## 体积与实际加载

- 本地 Noto：121 个 WOFF2，合计 **17,820,936B**。
  - 11 个 core：6,195,492B，单文件 467,332–653,368B。
  - 110 个 GB2312 按需分片：11,625,444B，单文件 24,612–162,272B。
- 生成的 `font-subsets.css`：349,895B；本地 Kestrel Brotli 实测传输 128,950B。
- 旧归档中 13 个全量 Noto 源文件为 **92,580,992B**；逐一比较 Git blob ID 后确认当前文件与 `origin/legacy/archive-before-frontend-prune-20260710` 完全一致，随后已从 modernization 分支及旧 MVC `Content` 项目清单移除。现代分支只保留子集，全量源留在 legacy archive 作为可复现输入。

浏览器在真实本地页面观察到的 Noto 请求：

| 页面 | Noto core 数 | core 总大小 | GB 分片 | legacy/remote Noto |
|---|---:|---:|---:|---:|
| `/Home/newIndex`（实际只读 DB 数据） | 5 | 2,734,940B | 0 | 0 |
| `/Publication/tonggou2` | 4 | 1,934,540B | 0 | 0 |
| `/Staff/Account/Login` | 3 | 1,629,096B | 0 | 0 |

补充网络验证还分别打开了实际数据库新闻详情 `/Home/newnewsread/1124`、艺术家页 `/Home/newArt/1000`、`/Publication/chunyu3` 和 `/Publication/tonggou2024`。四页的字体资产清单均只出现 `/fonts/Noto*.core.woff2`，没有 GB 分片、远程字体域或 `/Content/fonts/Noto*`。加上上表三页，共验证 7 个正式路由。

按需分片另用测试期间的最小本地 HTML 刻意插入 core 外、GB2312 内的 `U+4E0C`：浏览器复用 `NotoSansSC-Regular.core.woff2` 后，只新增请求一个 `NotoSansSC-Regular.gb01.woff2`（74,428B），没有请求其他 GB 分片。服务端 HTTP 日志同样记录了唯一的 `gb01` 200 请求。临时测试 HTML 已删除，没有进入工作树。

相比同样字重的仓库旧全量文件，上述三页的 Noto 首次请求体积均减少约 92%。这一对比是“本地子集 vs 本地全量字体”，不把旧第三方 CDN 缓存当成本地基线。

## 字形与元数据验证

`scripts/build-font-subsets.py` 使用 HarfBuzz 保留全部 layout features，使用 Google woff2 工具压缩，并对每个产物自动检查：

- 请求 codepoint 全部在输出 cmap 中；
- 每个 codepoint 的轮廓 drawing operations 与旧源字体完全一致；
- horizontal metrics 完全一致；
- `OS/2.usWeightClass` 不变；
- name/license 记录完整保留。

CSS `@font-face font-family` 是浏览器选择名，因此内部 name table 继续保留旧 Noto 原值不会阻止 `Szaipa Noto ...` 唯一别名；同时完整保留 name table 也避免丢失 license/provenance 元数据。

## 可复现生成

依赖：

```bash
brew install harfbuzz woff2
python3 -m pip install fonttools brotli
```

13 个全量源文件固定从 `origin/legacy/archive-before-frontend-prune-20260710` 读取。可用临时 worktree，不把它们复制回 modernization 分支：

```bash
git worktree add --detach /tmp/szaipa-font-origin \
  origin/legacy/archive-before-frontend-prune-20260710

python3 scripts/build-font-subsets.py \
  --font-dir /tmp/szaipa-font-origin/szaipa2022/Content/fonts \
  --source-root src/Szaipa.Web \
  --output-dir src/Szaipa.Web/wwwroot/fonts \
  --codepoints-file scripts/font-db-codepoints.txt \
  --css-output src/Szaipa.Web/wwwroot/css/font-subsets.css

git worktree remove /tmp/szaipa-font-origin
```

## 验证

- `python3 -m py_compile scripts/build-font-subsets.py`：通过。
- `npm --prefix src/Szaipa.Web run build`：通过。
- `~/.dotnet/dotnet build Szaipa.Modernization.slnx --disable-build-servers -m:1`：0 warning / 0 error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx --no-build --disable-build-servers -m:1`：110/110 通过。
- 静态扫描：现代正式源中无远程字体域、无 `/Content/fonts/Noto*` 引用、无运行时 `sans*` / `cjk*` legacy family。
- 浏览器资产清单：7 个正式路由均只请求本地 core，0 个 `gb*` 分片、0 个 `/Content/fonts/Noto*`、0 个远程 Noto；刻意插入 `U+4E0C` 时只请求对应的一个 `gb01` 分片。

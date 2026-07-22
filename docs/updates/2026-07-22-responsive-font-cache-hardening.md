# 响应式细节与字体缓存加固（2026-07-22）

本轮继续收敛现代前台，不修改服务器、旧稳定发布版、外接 `Content` 或数据库。视觉字体、字重映射和现有图片均保持不变。

## 手机端排版

- 数据驱动展览把 legacy `publication.css` 从 `<body>` 移到页面 `Styles` section，并确保它先于现代重要展览皮肤加载，避免后加载的旧规则覆盖现代响应式修复。
- `chunyu3`、`tonggou2`、`tonggou2024` 共用的作品详情在不超过 `61.9375em` 时取消桌面浮动、负 viewport margin 和固定 `25/35vw` 表单宽度，改为单列文档流；作品名使用 `clamp()`，图片和表单不超过容器。
- NewArt 展讯标题在窄屏使用两侧自适应间距和容器宽度，不再保留桌面的 `30em` 固定宽度。
- NewNewsRead 的数据库富文本为 `table`、`iframe`、`video`、`pre` 增加局部宽度与溢出保护；普通正文排版和桌面样式不变。

## 字体缓存

字体清单后来按页面边界拆成 `font-subsets-public.css` 与 `font-subsets-staff.css`；两者并集仍保留原 Alibaba、Noto Sans/Serif SC、Smile 字形和全部既有 `unicode-range` 分片。没有换字体，也没有把 125 个 WOFF2 合并成一次性下载。拆分详情见 `2026-07-22-font-manifest-split.md`。

新增的 `scripts/build-font-subsets.py --refresh-css-versions` 会把每个本地 WOFF2 的 SHA-256 前 12 位写入其 URL：

```text
../fonts/NotoSansSC-Regular.core.woff2?v=ec1901a9e92f
```

这样实际命中的字体分片可进入现有的一年 `immutable` 缓存；二进制变化时 URL 会同步变化。`--check-css-versions` 只验证、不写文件，适合 CI。Web 测试还会逐个核对 CSS 引用、磁盘文件和实际内容哈希，并确认所有发布的 WOFF2 都被清单覆盖。

## 验证

- `python3 -m py_compile scripts/build-font-subsets.py`：通过。
- `python3 scripts/build-font-subsets.py --check-css-versions src/Szaipa.Web/wwwroot/css/font-subsets-public.css`：114 个引用通过。
- `python3 scripts/build-font-subsets.py --check-css-versions src/Szaipa.Web/wwwroot/css/font-subsets-staff.css`：88 个引用通过；两份清单并集覆盖磁盘全部 125 个 WOFF2。
- 浏览器实际资源清单：`/Publication/chunyu3` 命中的现代 WOFF2 均带内容版本；仍按字形/字重/字符范围按需请求。
- 390×844：春语页、NewArt 1000、新闻 1124 均无页面级横向溢出；春语邮箱标题保持单行，打开的作品详情完整落在 390px 视口内。
- 1440×900：NewArt 的旧桌面定位值保持不变；新增窄屏媒体规则不生效。
- `~/.dotnet/dotnet build Szaipa.Modernization.slnx --no-restore --disable-build-servers -m:1`：0 warning / 0 error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx --no-build --no-restore --disable-build-servers -m:1`：Data 96/96、Web 121/121，合计 217/217。
- `npm run build`（使用工作区 Node runtime）：Tailwind、editor 和 dashboard bundle 全部通过。

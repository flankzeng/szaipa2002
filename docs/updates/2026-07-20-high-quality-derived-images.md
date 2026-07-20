# 高质量派生首图与 NewArt 空 Banner 修复（2026-07-20）

## 边界

本批只在现代项目中增加可随发布包携带的派生资源和安全解析逻辑。没有修改旧
`szaipa2022`、外置 `web24.05/Content`、数据库、Windows 服务器、IIS 或现有发布版。

## 首页 LCP

当前只读数据下，首页实际首张轮播封面是
`/Content/images/tangqishan/100002.jpg`。该文件虽然扩展名是 `.jpg`，实际是完全不透明的
RGBA PNG，尺寸 1080×791、文件 1,783,805 B。

现代仓库新增同尺寸 10-bit AVIF：

- 文件：`wwwroot/media/derived/home/tangqishan-100002.1080x791.avif`
- 大小：167,097 B
- SHA-256：`8ac84d9a2518ab5a34c462c2674b27b3fbfefb29c52f55914bc4c8d777c0ad39`
- 与原图同尺寸对照 SSIM 约 0.992，未裁切、未改变构图，人工全图对照未见可见色偏。
- 单次首图响应减少 1,616,708 B（约 90.63%）。

`NewIndex.cshtml` 通过 `<picture>` 优先选择 AVIF，原 `/Content` URL 保持为浏览器 fallback；
数据库动态首图使用同一解析器，未进入清单时仍回原图。首张 `eager/high`、其余
`lazy/auto` 的优先级规则不变。

## 安全派生解析

新增单例 `IDerivedImageResolver` 与本地 allowlist
`wwwroot/media/derived/manifest.json`：

- 仅精确匹配合法 `/Content/...` jpg/jpeg/png；
- 仅返回 `wwwroot/media/derived/` 下真实存在、非符号链接的 AVIF；
- 大小写无关重复项视为歧义并忽略；
- 空值、外链、目录、路径穿越、双重编码和非法扩展拒绝；
- manifest 缺失、损坏或版本不支持时不影响页面，合法原图安全回退；
- AVIF URL 使用内容版本 `?v=`，沿用自有静态资源一年 immutable 缓存策略；
- WebRoot 明确映射 `.avif` 为 `image/avif`。

以后新增高质量派生图只需提交派生文件并加入 manifest；不按数据库文件名猜路径，也不在
请求期间生成或修改外置 Content。

## NewArt Banner

原页面即使 `Path1`/`Path2` 为空也会拼出
`/Content/ArtImg/Artist/Banner/`，悬停时继续请求该目录并产生 404。现在：

- 只接受单个安全图片文件名并逐段 URL 编码；
- `Path1` 为空时回落 `Path2`，两者都空时使用 `background-image:none`；
- 无图时不输出 preload、Banner URL 或目录字符串；
- Razor 用 `System.Text.Json` 输出结构化 payload，JS 不再拼接 base/path；
- 若未来 manifest 有高质量 AVIF，CSS/JS 用 `image-set` 优先选择，同时保留原图；
- 若无派生图，现有原 Banner 的画质和 preload 行为完全不变。

三张现有 Banner 已做 1600px AVIF 压缩审计。统一替换会让两张约 2700px 的张岚芊图降采样，
而只节省约 22%–31%，高 DPR 全屏清晰度风险不值得；因此本批没有加入任何 NewArt Banner
派生文件。后续若继续，应先验证 2400px 高质量档，而不是强行使用 q30 或 1600px 低质版本。

## 验证

- .NET build：0 warning / 0 error。
- 测试：Data 96 + Web 85 = 181/181 通过。
- `npm run build` 与 `node --check wwwroot/js/newart.js` 通过。
- 本地真实只读首页在 1440×900、390×844 下 `currentSrc` 都是 167,097 B AVIF，源尺寸仍为
  1080×791；响应 `Content-Type: image/avif`。
- 390px 首页 `scrollWidth=clientWidth=390`，navbar 四项均为 `nowrap`、`horizontal-tb`。
- `/Home/NewArt/1000` 继续使用原张岚芊 Banner；`/Home/NewArt/1052` 输出
  `background-image:none`、无 image preload、HTML 中无 Banner 目录 URL，且无浏览器错误。

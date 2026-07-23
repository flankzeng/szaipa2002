# NewArt 运行时清理与横幅 AVIF 审计（2026-07-23）

本轮只修改现代源码；没有修改服务器、旧稳定发布版、外接 `Content`、数据库或任何原始横幅文件。

## 已实施：移除无效运行时工作

`newart.js` 原来仍会初始化 `.mySwiper2`，但 NewArt Razor 已无此节点。该初始化现在移除；保留页面实际存在的 `.mySwiper`（作品集）和 `.mySwiper3`（相关展览）。

`swiper-bundle.min.js`（当前 135,660B）、`magnify-loader.js`、`newart.js` 均改为同一顺序的 `defer`。这保留三者依赖顺序和页面内联横幅数据，同时不再让尾部 Swiper 下载/执行阻塞 HTML 解析。

新增 Web 契约测试确保：

- 三个 NewArt 运行时脚本均为保序 `defer`；
- 两个真实 Swiper 的初始化声明仍保留；
- 已移除的 `.mySwiper2` 不会重新进入脚本。

## 未实施：横幅 AVIF

真实 `/Home/NewArt/1000` 主横幅为：

| 文件 | 原始像素 | 原始大小 |
|---|---:|---:|
| `zhanglanqian-01.jpg` | 2732×1772 | 815,724B |
| `zhanglanqian-02.jpg` | 2702×1772 | 643,489B |

它们可覆盖当前 1440×900 CSS 首屏，但主图宽度只有 2732px，略低于 1440px 双倍 DPR 所需的 2880px；不能为了体积改成 1600px 档，否则会直接损失高 DPR 桌面的细节。

只在临时目录生成了全尺寸 AVIF 原型：系统编码器默认输出为 1,183,111B / 947,317B，反而更大；55% 档为 803,166B / 645,069B，收益仅约 1.5% / 无收益。SVT-AV1 CRF 40 的主图为 780,423B，虽然约少 4.3%，但已是 JPEG 的第二次有损压缩（PSNR 38.56 dB、SSIM 0.9766）。这点收益不值得引入任何可见画质风险，因此没有加入派生 AVIF、manifest 或 fallback。

未来若获得艺术家提供的高质量原图，或能用经验证的单次无损工作流生成显著更小的 2400px 以上资产，再重新评估；不能把现有参考 JPEG 直接缩小替换。

## 验证

- `~/.dotnet/dotnet build Szaipa.Modernization.slnx --no-restore --disable-build-servers -m:1`：0 warning / 0 error。
- 测试：Data 96/96、Web 126/126，合计 222/222（221→222）。
- 实页 `/Home/NewArt/1000`：1440×900 首屏仍为 1440×900，横幅 URL 未变，页面 `scrollWidth=clientWidth=1440`；390×844 首屏仍为 390×844，导航单行，`scrollWidth=clientWidth=390`。
- 新脚本标签均带 `defer`，页面没有 `.mySwiper2` 节点，控制台无 warning/error。

# 2026-07-25 公共 Swiper 按模块裁剪与 Banner 2400px 审计

## Swiper 裁剪

公开站原来从外置 Content 加载完整 Swiper 9.0.3 bundle。源码实际只使用以下模块：

- A11y；
- Autoplay；
- EffectCoverflow；
- FreeMode；
- Keyboard；
- Navigation；
- Pagination；
- Thumbs。

现代项目现在固定依赖同版本 `swiper@9.0.3`，入口为
`wwwroot/public/src/swiper-public.{js,css}`，由 esbuild 输出到
`wwwroot/public/vendor/`。这不是升级 Swiper，也没有改变页面的初始化参数、HTML 或视觉
样式；只是让未使用模块不再进入浏览器。

| 资源 | 旧全量原始大小 | 新裁剪原始大小 | 旧 gzip | 新 gzip |
|---|---:|---:|---:|---:|
| JavaScript | 135,660B | 88,156B | 37,782B | 26,597B |
| CSS | 17,863B | 13,029B | 4,768B | 3,947B |

每个首次访问轮播页面的压缩传输约少 12KB；新 URL 带内容哈希并使用现代自有静态资源的一年
immutable 缓存，而旧 `/Content/Model` CSS/JS 只有 7 天缓存。

已替换 7 个 Razor 入口：NewIndex、NewArt、数据展览、三个特殊展览和 `_Artist` 布局。
契约测试会阻止这些入口重新引用 `/Content/Model/swiper-bundle*`，并限制生成包体积。

## 真实页面验证

- NewIndex：3 个 Swiper 初始化，自动播放/分页/导航模块均加载；
- NewArt/1000：作品与相关展览 2 个 Swiper 初始化，导航图标正常；
- chunyu3：coverflow、free mode、主图/缩略图联动共 3 个 Swiper 初始化；
- 1440×900 与 390×844 均无横向溢出；
- 手机特殊展览 `scrollWidth=clientWidth=390`；
- 上述页面控制台无 warning/error，已加载图片无失败；
- npm 全量构建通过；
- .NET build 0 warning / 0 error；
- Data 98 + Web 147 = 245 项测试全绿。

## NewArt Banner 2400px 结论

本轮只在 `/private/tmp` 对两张约 2700px 的张岚芊 JPEG 生成 2400px、10-bit AVIF 候选，
没有修改或复制外置 Content，也没有把候选加入仓库。

高质量 CRF 36 档分别为 814,788B、653,990B，几乎不比原始 815,724B、643,489B 小；
CRF 40 才有约 17%–18% 收益，但相对同尺寸参考的整体 SSIM 约为 0.94，且 2400px 仍低于
1440px 双倍 DPR 所需的 2880px。它同时承担降采样和二次有损压缩，收益不足以接受。

因此所有候选被拒绝，页面继续使用原图。未来只有取得艺术家高质量源文件时才重评估，不对
现有 JPEG 继续做低质替换。

本轮没有修改数据库、服务器、IIS、旧发布版、外置 Content 或 `szaipa2026`。

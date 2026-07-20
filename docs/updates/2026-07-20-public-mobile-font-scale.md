# 公共页手机/平板字号基准修复（2026-07-20）

## 根因

外置 legacy `Content/Model/css/responsive.css` 在 `max-width: 991px` 下对 `html` 强制
`font-size: 26px !important`。这会把现代前台以 `rem` 书写的整套排版同比放大，
不是 `.emailPosition` 某一条规则单独失效。

390 宽实页修复前：

- 邮件标题 `2rem` 被放大为 52px；
- 说明文字 `1.2rem` 为 31.2px，行高 56.16px；
- 订阅按钮 `1.3rem` 为 33.8px；
- 手机 footer `1rem` 为 26px。

同一问题可在 NewIndex、NewArt 和三个特殊展览页复现。

## 修复

- `public-layout-common.css`覆盖使用 `_newLayout` / `_Artist` 的公共页；
- `publication-special.css`覆盖 chunyu3 / tonggou2 / tonggou2024；
- Layout=null 的 NewNewsRead 继续在自己的 CSS 内使用同一基准。

三处均用百分比 + `vw` 的 `clamp(93.75%, 4.1vw, 100%)`，使 320 / 390 /
768 / 991 宽度下的根字号分别约为 15 / 15.99 / 16 / 16px。没有把邮件区每个文字
改成固定像素值。

同时补两个窄屏边界：

- 邮件标题使用 `rem + vw` 的 clamp，在 320/390 均保持单行；
- 邮件区高度使用 `max(80vh, 42rem)`，避免 320×568 短屏的说明文字与社交图标重叠。

特殊展览序章的超宽中心装饰图仍保持原尺寸/裁切视觉，但由 `#section02`
限制横向溢出，不再把 390 宽页面撑到约 1773。

## 实页验证

- chunyu3 在 320/390/768/991 均为 `scrollWidth == clientWidth`；
- 390 宽邮件标题 26.52px、说明 19.188px、按钮 20.787px、footer 15.99px；
- 320 宽邮件标题仍是单行，邮件内容完整位于区块内，且说明文字不与社交图标重叠；
- NewIndex / NewVip / NewArt 在 390 宽均为约 16px 根字号且无横向溢出；
- NewIndex 在 320 宽 navbar 四项仍同一行、横排、不换行，全部位于视口内。

## 保留边界

`992–1279px` 仍沿用 legacy 针对“720p”布局设置的 12px 根字号，并在 1280px
切回 16px 与另一套桌面导航尺寸。NewIndex 实测导航文字会从 1279 的 11.25px
跳到 1280 的 24px；这不是本次 `max-width: 991px` 手机/平板放大回归，直接统一根字号
会同时改变导航栅格与间距，因此保留现状，等待单独的视觉取舍。

外置参考 Content、Windows 服务器、旧稳定发布版和数据库均未修改。

# 特殊展览首屏图片优先级（2026-07-26）

本轮只调整三个独立特殊展览页的浏览器加载提示，不修改图片文件、CSS 几何、旧发布版、外接 Content、服务器或数据库。

## 问题

三页真正的首屏视觉是 CSS hero 背景：

- chunyu3：`chunyu-color2.jpg`，781,090B；
- tonggou2：`DSC05345-color.jpg`，543,795B；
- tonggou2024：`tonggou2024/100000.jpg`，267,735B。

但紧接首屏之后的品牌图与装饰线同时被标记为 `fetchpriority="high"`。390px 下它们从 y=928/1013 开始（视口高 844），1440px 下从 y=990/1080 开始（视口高 900），并非首屏 LCP 候选。每页这两张图合计 111,245B（chunyu3）或 135,338B（两个 tonggou 页面），不应与 hero 竞争高优先级带宽。

## 修改

三个视图共六张图统一改为：

```html
loading="lazy" decoding="async" fetchpriority="low"
```

图片 URL、真实 `width`/`height`、class 和 CSS 不变。图片距首屏很近时，浏览器仍可能提前请求；本修改的确定收益是取消错误的高优先级竞争，而不是声称每次访问都会完全省掉这些字节。

## 验证

- 三页各在 390×844、1440×900 验证：浏览器反射属性均为 lazy/async/low。
- 六组图片矩形与修改前一致，三页均 `scrollWidth == clientWidth`。
- 新增契约测试，锁定三页各有两张 low-priority 图，且不得重新出现 `fetchpriority="high"`。
- `~/.dotnet/dotnet build Szaipa.Modernization.slnx --no-restore --disable-build-servers -m:1`：0 warning / 0 error。
- `~/.dotnet/dotnet test Szaipa.Modernization.slnx --no-build --no-restore --disable-build-servers -m:1`：Data 98/98、Web 148/148，合计 246/246。

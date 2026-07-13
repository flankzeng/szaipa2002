# 图片加载优先级优化（2026-07-13）

## 范围

- 仅修改现代站本地代码；未访问或修改发布服务器，也未写入数据库。
- `NewArt` 将当前真实首屏 `Path1` 背景图在页面专属 `Styles` 区预加载，图片 URL 与 CSS 变量共用同一个 Razor 计算结果。
- 首页章程图由 CSS background 改为可延迟加载的 `<img>`，仍使用原图和原来的拉伸/居中方式。

## 改动前基线

真实现代站只读 GET，浏览器分别固定为 `390 × 844` 和 `1280 × 720`：

| 视口 | `#section04`（x / document-y / w / h） | `.zhangcheng`（x / document-y / w / h） |
| --- | --- | --- |
| 390 × 844 | 0 / 84.398 / 390 / 337.594 | 39 / 351.664 / 312 / 168.797 |
| 1280 × 720 | 0 / 1559.516 / 1280 / 720 | 128 / 1761.500 / 640 / 410.398 |

两档视口均无横向溢出。章程图 computed style 为黑底、`background-size: 100% 85%`、`background-position: 50% 50%`、不重复。

`/Home/NewArt/1000` 改动前没有 `image` preload；首屏 `Path1` 由 CSS background 发现，浏览器页面资源清单中只出现一个对应图片资源。

基线截图保存在本轮本机临时目录：

- `/tmp/szaipa-zhangcheng-before-390.png`
- `/tmp/szaipa-zhangcheng-before-1280.png`

## 实现

### NewArt 首屏图

- 用 `Url.Content` 生成一次 `artistBannerUrl`，同时供 `<link rel="preload" as="image">` 和 `--artist-banner` 使用，避免 URL 大小写或编码不一致造成重复请求。
- 仅在 `Path1` 非空时输出 preload，并标记 `fetchpriority="high"`。

### 首页章程图

- 保留原始 `1500 × 811` JPEG，并把真实固有尺寸写入 `width` / `height`。
- 使用 `loading="lazy"` 与 `decoding="async"`，让远离首屏的章程图不再随 CSS 立即下载。
- 容器仍为黑底、原高度不变；子图绝对定位为 `top: 7.5%; width: 100%; height: 85%; object-fit: fill`，数学上等价于原来的 `background-size: 100% 85%` 与垂直居中，并关闭了背景图原本不存在的拖拽行为。

## 验证

- `git diff --check` 通过。
- 原图实测固有尺寸为 `1500 × 811`，与新增 HTML 属性一致。
- Razor/CSS 静态检查确认旧章程 background URL 已移除，图片 URL 仅由新的 `<img>` 承载；`--artist-banner` 仍只有 `newart.css` 中的同一处首屏消费。
- Web 项目定向 build 通过：0 warnings / 0 errors。本任务未单独运行全局 build/test。

真实页面复测结果：

- `390 × 844`：`#section04` 与 `.zhangcheng` 的 x/y/w/h 和改动前完全一致，页面宽度仍为 390、无横向溢出；图片 computed 尺寸为 `312 × 143.477`，即容器的 `100% × 85%`。
- `1280 × 720`：`#section04` 与 `.zhangcheng` 的 x/y/w/h 和改动前完全一致，页面宽度仍为 1280、无横向溢出；图片 computed 尺寸为 `640 × 348.836`，即容器的 `100% × 85%`。
- 移动端前后截图逐字节相同。桌面端把章程区滚入视口、触发 lazy 图片解码后的截图视觉一致；像素比较平均每通道绝对差为 `0.551 / 255`，差异仅来自 CSS background 与 `<img>` 的浏览器重采样路径。
- 两档页面都确认了 `loading="lazy"`、`decoding="async"`、`1500 × 811` 固有尺寸和黑底；桌面端在滚到章程区前不绘制图片，证明了原 CSS background 不具备的延迟加载行为。
- `/Home/NewArt/1000` 输出且只输出一个匹配首屏 banner URL 的 image preload，`as="image"`、`fetchpriority="high"` 均正确；页面资源清单仅有一个对应图片资源，来源记录为 preload `link`，同一 URL 再被 computed `background-image` 复用，未出现 preload 或控制台警告。

复测截图保存在本轮本机临时目录：

- `/tmp/szaipa-zhangcheng-after-390.png`
- `/tmp/szaipa-zhangcheng-after-1280-decoded.png`

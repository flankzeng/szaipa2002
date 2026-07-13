# 首页轮播 LCP 加载优先级

## 问题

首页 107 张图片已有 `loading="lazy"`，但首屏真正可见的第一张轮播封面也被标成 lazy；相反，顶部装饰文字图 `TEXT.png` 使用了 `fetchpriority="high"`。这会让浏览器优先级与实际最大内容绘制（LCP）错位。

## 调整

- `TEXT.png` 继续默认 eager，但不再抢占 high 优先级；
- 若数据库轮播列表非空，第一条动态展览的 Logo/封面使用 eager，且仅封面使用 `fetchpriority="high"`；
- 若数据库列表为空，则第一张硬编码唐岐山展览 Logo/封面承担同样优先级；
- 其余轮播图继续 lazy + async，不增加首屏流量。

## 验证

- Razor/.NET 全量 build：0 warning / 0 error；
- 用真实只读数据库启动现代站，`GET /` 返回 200；当前数据库轮播列表为空时，首张 `/Content/images/tangqishan/100002.jpg` 渲染为 `loading="eager" fetchpriority="high"`；
- 第二、三张封面仍为 `loading="lazy"`；
- 整页只有首张可见轮播封面带 `fetchpriority="high"`。

这项改动只调整浏览器资源调度，不改变图片、尺寸、CSS 或页面视觉。

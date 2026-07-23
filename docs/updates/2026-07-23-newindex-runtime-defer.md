# 首页运行时延后（2026-07-23）

## 改动

`Home/NewIndex` 的三个页尾脚本现在都使用 `defer`，并仍按原有文档顺序加载：

1. `swiper-bundle.min.js`
2. `magnify-loader.js`
3. `newindex.js`

`defer` 保持相对执行顺序，因此 `newindex.js` 仍只会在 Swiper 和放大镜 loader 可用后执行；同时浏览器可继续解析剩余 HTML，而不会被旧版 135,660B Swiper 同步脚本阻塞。

## 验证

- 新增源码契约测试，断言三个脚本均为 `defer` 且顺序固定。
- 现代 Data 测试：96/96 通过。
- 现代 Web 测试：127/127 通过；合计 223/223。
- 没有更换字体、图片或 `/Content` 中的旧资源；没有写数据库、服务器或发布版。

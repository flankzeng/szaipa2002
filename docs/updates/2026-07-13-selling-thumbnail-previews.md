# 首页“在售作品”缩略图与按需原图

## 问题

首页在售作品轮播原先直接使用 6 张原图。原图合计 `9,860,285B`，即使用户只是浏览列表，也会承担不必要的下载。

Magnify 2.3.3 支持图片上的 `data-magnify-src`，但插件初始化时会立即通过 `new Image()` 请求该原图，所以仅替换 `src` 不能真正节流。

## 调整

- 6 张列表图的 `src` 改用既有 `/Content/_preview/q30w1200/Content/artimg/selling/` 缩略图；
- `data-magnify-src` 保留原 `/Content/ArtImg/selling/` 路径，放大镜画质不变；
- 为每张图增加原图的真实 `width`/`height` 比例元数据，实际显示尺寸仍由现有 CSS 控制；
- Magnify 改为每张图首次实际 `pointermove`/`mousemove`/`focusin`/`touchstart` 交互时才初始化，并在初始化前移除该图的延迟监听器；
- 不使用 `mouseenter`：Swiper 自动换片时，即使指针没有移动，新图也可能进入指针下方并误请求多张原图。

## 流量结果

| 资源 | 6 张合计 |
| --- | ---: |
| 原图 | `9,860,285B` |
| q30w1200 缩略图 | `981,672B` |
| 列表浏览节省 | `8,878,613B` (`90.04%`) |

## 验证

为避免继续调用首页控制器，最终验证使用保存的首页 HTML fixture，仅从本机现代应用读取静态资源。

- 6 张缩略图均返回 200，响应字节合计与文件大小一致：`981,672B`；
- 滚动到在售区后 Magnify 容器仍为 0，6 张原图均未请求；
- 首次把指针移到当前作品后，仅 `/Content/ArtImg/selling/zlq1.jpg` 被请求，返回 200；新建的放大镜背景也准确指向该原图；
- 390×844 与 1280×720 两档视口根元素均无水平溢出，首页 navbar 四项保持同一行、`nowrap` 与 `horizontal-tb`；
- 两档完整滚动后 35 个首页预览资源（本批 6 个 + 同轮扩展的 29 个）均加载成功，合计 2,459,293B；在售原图仍为 0 请求；
- 两档 console error 均为 0，唯一 eager/high 图片仍是当前首张唐岐山轮播封面。

首次启动本地首页时，测试命令使用了未带配置段前缀的短键，未覆盖 `appsettings.Local.json` 中实际的 `RuntimeSafety` / `ReadOnlyMigration` 命名配置，因此执行了 News 和 Publication 各一条只读 `SELECT`；无写入。这是测试命令键名错误，不是安全开关失效。发现后已停止请求控制器，改用上述 fixture 完成验证。

本轮没有访问或修改 Windows 服务器、发布目录或 IIS，也没有写入数据库；数据库访问仅有上述意外触发的两条只读 `SELECT`。

# 动态预览扩展与真实接入验证（2026-07-20）

## 范围

本批继续复用只读 `ILegacyImagePreviewResolver`，不生成图片、不修改外置 Content，
也不触碰 Windows 服务器或旧发布版：

- `NewArt` 作品列表的 `src` 优先使用已有 q30，`href` 与 `data-magnify-src` 始终保留原图；
- `newart.js` 不再在 DOM ready 时初始化全部 Magnify，改为首次
  `pointermove` / `mousemove` / `focusin` / `touchstart` 才初始化单张作品；
- `NewNewsRead` 的相关新闻侧栏背景图接入预览解析器；
- `_ExhibitionImportant` 的参展作品卡片接入预览解析器；
- `NewNewsRead` 手机导航容器改用 `border-box`，避免 `width: 100%` 再叠加左右 padding
  把“关于我们”裁到视口外；
- Banner、首页 LCP、NewArt 83vh 相关展览和普通展览大图库保持原图策略。

解析器未命中时仍输出原 URL，所以这批变化不会让没有预览的旧数据裂图。

## 收益审计

参考 Content 的 `works-narrow` 共 122 张、11,799,743B；现有 q30 覆盖 8 张。
这 8 张原图合计 4,468,814B，预览合计 1,450,807B，命中部分节省
3,018,007B（67.54%）。

真实只读数据库样本 `/Home/NewArt/1012`（王玉波）包含 56 个作品节点、55 个唯一文件：

- 原始唯一文件合计 4,504,261B；
- 其中 5 个命中预览；
- 解析后首轮合计 2,595,827B；
- 少传 1,908,434B（42.37%）。

其余作品仍为原图回退；用户点击作品链接仍打开原图，放大镜首次交互后也使用原图。

## 真实只读验证

本地以 `UseLegacyDataSources=true`、`AllowLiveDatabase=false` 和参考 Content 启动，数据库
只执行 SELECT：

- 390×844 下实际打开 `NewIndex`、`NewNews`、`NewVip`、`NewArt`、`PublicationList`，
  五个路由均为 200、`scrollWidth == clientWidth == 390`、控制台 0 error；
- 首页 navbar 四项保持 `white-space: nowrap`、`writing-mode: horizontal-tb` 且同一行；
- 新闻详情页修复前最后一项右边缘约为 401px；修复后四项右边缘均不超过 390px，
  同一行、横排、不换行，且 `scrollWidth == clientWidth == 390`；
- 真实新闻列表同时验证预览命中和原图回退；真实会员列表头像也同时出现命中与回退；
- 当前只读库没有可展示的 Publication 行，展会卡片的真实数据渲染仍由 Razor 编译和
  解析器单测覆盖；
- `/Home/NewArt/1012` 桌面视口输出 56 个 `data-magnify-src`，其中 5 个 `src` 为 q30，
  进入页面后 `.magnify` 包装器为 0；
- `/Home/NewArt/1000` 切到首个 q30 作品前仍未初始化 Magnify，真实 pointer move 后只给
  当前作品创建 lens，lens background 指向该作品原 PNG，控制台 0 error。

构建继续保持 0 warning / 0 error；全量测试和 npm 构建在提交前统一复跑。

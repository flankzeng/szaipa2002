# 前台清理与 Legacy 归档 Phase 2（2026-07-10）

## 归档与 zengfeng 退役

- 在基线提交 `d548edf` 建立分支 `legacy/archive-before-frontend-prune-20260710`，不切换工作树，因此不会混入用户尚未提交的旧 MVC5 修改。
- `szaipa2022/Content/publication/zengfeng` 共 646 个 tracked 文件、约 140MB，已从当前分支工作树移除；同级 `chunyu` 约 73MB 保留。
- 现代与旧 MVC5 `/Publication/zengfeng` 均返回 410；现代端响应不渲染视图、不访问数据库、不加载 legacy 资源。
- 归档分支已于 2026-07-11 经用户明确授权推送到远端。

Git branch 只让当前工作树/当前分支不再携带文件；旧对象仍在 `.git` 历史中，不会立刻缩小本地仓库。正式 `dotnet publish` 原本也不会包含整个 `szaipa2022` 旧项目或外置 `/Content`。

## 主导航链路修复

- 新增正式 `/Home/newabout` 页面，恢复旧站“关于我们/合作伙伴”内容；内联 CSS 外提为 `wwwroot/css/newabout.css`，图片懒加载。
- 主导航“关于我们”不再错误指向会员页；合作案例锚点改到关于页面。
- `/Home/newvip` 与兼容 `/Home/vip` 在只读模型启用时通过 `IArtistReadRepository.GetArtistsAsync` 渲染真实会员卡片；DB 未启用时仍保留迁移骨架。
- `/Home/PublicationList` 在只读模型启用时通过 `IPublicationReadRepository.GetLatestPublicationsAsync` 渲染真实展会列表；旧 `/Publication/index` 301 到新列表，硬编码列表页和 `live.js` 依赖删除。

## 确认零引用的模板清理

- 删除未使用的 MigrationStub factory/model/view 4 个文件。
- 删除默认 Privacy 样板 action/view、空 `site.js`。
- 删除现代项目 `wwwroot/lib` 中零引用的 Bootstrap、jQuery、jQuery Validation 模板副本及未使用验证 partial，共 57 个静态文件。

旧 `/Content/Model` 中的 Bootstrap/jQuery/Swiper 等不在本次整目录删除范围，因为当前正式页面仍有按页引用。

## 三个保留的自定义展览页

- `chunyu3`、`tonggou2`、`tonggou2024` 均确认没有 Vue/Element Plus 组件和 Bootstrap JS 行为，移除这些无效请求；每页首次访问少加载约 2.9MB。
- 修复三个页面 footer 中的 404 联系链接、错误“旧版首页”文案、无效反引号属性，并改用正式会员/关于/展会地址。
- 保留每页两张首屏视觉图并设高优先级；其余图片使用原生懒加载：
  - `chunyu3`：265 张中 263 张懒加载；
  - `tonggou2`：128 张中 126 张懒加载；
  - `tonggou2024`：5 张中 3 张懒加载。
- 三页仍使用原来的 jQuery、Swiper、Bootstrap CSS 和远程字库；这些确有现存行为/字形依赖，不能与无效库一起机械删除。

## 发布包体积

使用相同 Release publish 命令对比：

| 指标 | 清理前 | 清理后 | 变化 |
|---|---:|---:|---:|
| publish 总体积 | 35,528KB | 24,080KB | -11,448KB（约 -32%） |
| publish `wwwroot` | 约 13MB | 约 2.3MB | 主要来自移除零引用模板库 |
| publish 文件数 | 301 | 129 | -172 |

外置 legacy `/Content` 不计入 publish 包；它仍需按访问日志与数据库路径白名单单独清理。

## 验证

- build：0 warning / 0 error。
- tests：94/94。
- npm build：通过。
- 390×844：关于页面、导航均无横向溢出；导航四项保持同一行；页面 7 张图片中 6 张懒加载。
- 390px 会员列表：25 张真实会员卡片，非 skeleton，无横向溢出。
- 本地当前数据库的展会列表返回 0 行，空状态正常渲染；没有脚本或资源错误。
- 路由：`newabout` 200、`vip` 200、`Publication/index` 301、`zengfeng` 410、`Project_Tongou/*` 410。

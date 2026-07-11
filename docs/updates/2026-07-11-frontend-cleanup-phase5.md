# 前台清理 Phase 5：字体脚本关键路径优化（2026-07-11）

## 改动

- 保留原 Alibaba 普惠体、有字体字库标识和现有本地核心子集，没有替换设计字体。
- 六类公共页面把有字体第三方脚本从同步加载改为 `defer`，避免远程请求阻塞 HTML 解析。
- 字体初始化延后到 `DOMContentLoaded`，第三方脚本失败时继续使用现有本地子集/字体栈降级。
- 原生滚动监听标记为 `passive`，减少滚动主线程阻塞提示。

覆盖页面：共享主布局、艺术家页、新闻详情页，以及 chunyu3、tonggou2、tonggou2024 三个特殊展览页。

## 验证

- `.NET build`：0 warning / 0 error。
- npm build：通过。
- 三份修改后的独立 JavaScript 均通过 `node --check`。
- `git diff --check`：通过（仅 `_Artist.cshtml` 既有 CRLF 提示）。
- tests：94/94。

## 仍需生产数据的边界

运行资源根约 95.9MB 候选仍未删除。必须取得生产访问日志和数据库资源路径导出后，才能继续 Phase 5 的资源物理清理。

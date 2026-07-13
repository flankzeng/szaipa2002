# 静态资源缓存策略防回退

## 背景

生产旧站抽查显示，CSS/JS 使用 7 天缓存，图片使用 30 天缓存。现代分支此前把自有 `wwwroot` 统一设为 7 天、外接 `/Content` 统一设为 1 天；直接部署会让图片缓存从 30 天倒退到 1 天，也没有利用 Razor `asp-append-version` 生成的内容指纹。

## 实现

新增纯函数 `StaticAssetCachePolicy`，由 `Program.cs` 的两处静态文件中间件统一调用：

| 环境/来源 | 条件 | `Cache-Control` |
|---|---|---|
| Development | 任意静态资源 | `no-cache` |
| 自有 `wwwroot` | 非空 `?v=` 内容指纹 | `public,max-age=31536000,immutable` |
| 自有 `wwwroot` | 无内容指纹 | `public,max-age=604800` |
| 外接 `/Content` | 图片、SVG、图标 | `public,max-age=2592000` |
| 外接 `/Content` | 字体 | `public,max-age=2592000` |
| 外接 `/Content` | CSS/JS | `public,max-age=604800` |
| 外接 `/Content` | 其他类型 | `public,max-age=86400` |

外接资源没有可靠内容哈希，不能使用 `immutable`；未知类型继续保守缓存 1 天。现代自有资源只有在 URL 明确带非空版本参数时才进入一年不可变缓存。

## 测试

新增 `tests/Szaipa.Web.Tests` 并加入 `Szaipa.Modernization.slnx`：

- 缓存策略测试 14/14；
- 原数据层测试 96/96；
- 全量合计 110/110；
- 全量 build 0 warning / 0 error；
- `git diff --check` 通过。

部署后仍需用真实响应头确认 IIS 没有覆盖应用设置，并抽查带 `?v=` 的自有 CSS/JS、无版本 `/Content` 图片和 `/Content` CSS/JS。

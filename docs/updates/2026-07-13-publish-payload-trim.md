# 现代站发布载荷瘦身（2026-07-13）

## 变更

- 在 `Szaipa.Web.csproj` 中设置 `CompressionEnabled=false`。本机 .NET SDK 10.0.301 对 net6.0+ 静态 Web 资产默认启用压缩，发布时为匹配资产生成 gzip 与 Brotli 两份副本。
- 现代站通过 `UseStaticFiles` 提供静态文件、通过 `UseResponseCompression` 按请求协商压缩，并未调用 `MapStaticAssets`。因此发布期预压缩副本不会被当前静态文件中间件自动选取；删除重复副本不改变原始资产，也不关闭运行时压缩。
- 保留现有版本化 URL 与静态资产缓存策略。首次请求仍可按客户端能力压缩，重复请求继续由浏览器缓存承担，权衡是首次命中某一资产时需要少量运行时压缩 CPU，换取更小、更清晰的发布包。
- `package.json`、`package-lock.json` 与 `appsettings.Local.example.json` 仍留在源码树供前端构建和本地配置参考，但明确不进入发布目录。
- 八个 `wwwroot/legacy/**/.gitkeep` 仅用于在 Git 中保留空目录，也明确不进入发布目录；仓库文件不删除。

## 实测收益

以当前代码执行 Release publish，并用 `CompressionEnabled=true` 另建同版本对照包后，确认移除 **761,732 B**：54 个 `.gz`/`.br` 副本共 676,383 B，三个源码/示例 JSON 文件共 85,341 B，八个 `.gitkeep` 共 8 B。外置 legacy `/Content` 不在现代站发布包内，因此不受影响。

这是未来发布包的本地配置变更；未连接或改动生产服务器、IIS、发布目录和数据库。

## 验证

- `dotnet build Szaipa.Modernization.slnx --disable-build-servers -m:1`：0 warning / 0 error。
- `dotnet test Szaipa.Modernization.slnx --no-build --disable-build-servers -m:1`：110/110 通过。
- `npm --prefix src/Szaipa.Web run build`：通过。
- 本地 Release publish 共 204 个文件；输出中没有 `.br`、`.gz`、上述三个 JSON 文件或 `.gitkeep`。
- 临时启动发布包后，请求同一 CSS：`Accept-Encoding: br` 返回 `Content-Encoding: br`，`Accept-Encoding: gzip` 返回 `Content-Encoding: gzip`，两者均包含 `Vary: Accept-Encoding`。因此运行时压缩没有因发布副本清理而回退。

# 前台与 Staff 字体清单拆分（2026-07-22）

## 结果

原先所有页面共用的 `font-subsets.css` 含 147 个 `@font-face`，导致公开页解析 Staff 专用 Serif，Staff 又解析 Alibaba、Smile、public Serif 和 legacy helper。现在改为：

- `font-subsets-public.css`：114 faces，包含 public/shared Sans、public Serif、Alibaba、Smile、`.Jheng*`、`.notosans` 与 `.notoserif`；
- `font-subsets-staff.css`：88 faces，只包含 shared Sans 和 Staff Serif 400/600/700；
- 两份清单的并集仍为原来的 147 faces，交集恰好是双方需要的 55 个 shared Sans faces。

六个公开入口只引用 public 清单，`Areas/Staff` 布局只引用 Staff 清单；旧合并文件已删除。125 个 WOFF2 文件、字形、字重、内容哈希和 `unicode-range` 均未改，实际页面命中的字体二进制下载策略不变。

## 体积

| 清单 | 原始大小 | gzip -9 | Brotli q11 | 相对旧合并清单 |
|---|---:|---:|---:|---:|
| 旧合并清单 | 352,094B | 36,511B | 7,875B | 基线 |
| public | 271,185B | 15,016B | 7,441B | raw -80,909B；gzip -58.88%；Brotli -5.51% |
| Staff | 215,656B | 13,355B | 6,781B | raw -136,438B；gzip -63.42%；Brotli -13.89% |

每个页面只请求其中一份，因此 CSS 传输、解析与规则匹配均减少。代价是 55 个共享 Sans 声明在两份文件内各保留一次，使 Git/发布包中的两份原始 CSS 合计 486,841B，比旧单文件多 134,747B；这是小幅磁盘重复换取每个访问面的更小清单，不应误报成发布包整体缩小。字体目录仍是同一批 125 个文件，没有复制或丢弃图片/字体资产。

## 生成器与维护边界

`scripts/build-font-subsets.py` 新增安全的清单拆分模式。它保持每个完整 `@font-face` 声明内容不变，并拒绝生成区 face 之间或末尾的未知内容；随后验证 public/Staff 的并集、交集和引用完整性，再备份并以临时文件成对替换输出，任一侧替换失败时回滚已更新的一侧：

```bash
python3 scripts/build-font-subsets.py \
  --split-css-manifests path/to/complete-font-manifest.css \
  --public-css-output src/Szaipa.Web/wwwroot/css/font-subsets-public.css \
  --staff-css-output src/Szaipa.Web/wwwroot/css/font-subsets-staff.css
```

未来确需从已归档的全量源重建 Noto 时，`--font-dir` 会直接生成这对清单。旧的单输出 `--css-output` 参数已刻意退役，避免只刷新一侧而留下不一致的清单；批量模式必须同时提供 `--public-css-output` 与 `--staff-css-output`。不要只为维护 CSS 或版本号随意运行 `--font-dir`，因为它会重建 WOFF2；普通二进制替换后分别对两份清单运行 `--refresh-css-versions` 和 `--check-css-versions` 即可。

`FontSubsetManifestTests` 现在验证：

- 两份清单的 URL 并集恰好覆盖磁盘全部 125 个 WOFF2，且每个 SHA-256 版本正确；
- public 114 / Staff 88 / 并集 147 / 交集 55 的精确边界；
- public 与 Staff family、helper 和字重不会串入对方清单；
- 六个公开视图和 Staff 布局只引用各自清单，旧文件不再存在。

## 验证

- `python3 -m py_compile scripts/build-font-subsets.py`：通过；
- public 114 个、Staff 88 个带哈希引用分别通过 `--check-css-versions`；
- `.NET build`：0 warning / 0 error；
- 测试：Data 96/96、Web 125/125，合计 221/221（219→221）；
- `npm run build`：Tailwind、editor、dashboard bundle 全部通过；
- 浏览器完成 11 个路由×宽度组合：NewIndex、NewNewsRead、NewArt、chunyu3 与 Staff 登录页各测 390/1440，NewVip 测 390；拆分前后计算样式和几何的稳定结果完全一致，均无横向溢出，公共 navbar 保持单行；
- 登录后的 `/Staff/Dashboard/Index` 与 `/Staff/News/Edit/1124` 也只声明 88 个 Staff faces，仪表盘标题字重与只读编辑内容正常。

本轮没有修改服务器、IIS、旧稳定发布版、外接 `Content`、数据库数据或任何字体二进制。

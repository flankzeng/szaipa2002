# Alibaba 普惠体本地化（2026-07-11）

## 结果

- 保留原设计使用的 Alibaba 普惠体 L/R/M 层级，没有改选其他字体。
- 使用 Alibaba Fonts 官方普惠体 2.0：45 Light、55 Regular、65 Medium。
- 每档完整官方 WOFF2 约 5MB，经 FontTools 校验均为 29,030 个字形。
- 按现代公共站 Razor/CSS/JavaScript 字符生成站点核心子集，每档约 170KB，总计约 510KB。
- `.JhengLight`、`.JhengRegular`、`.JhengBold` 分别映射到本地 300/400/500 字重。
- 删除六类公共页面的有字体第三方脚本及 `$webfont` 初始化；动态未覆盖字符逐字回退到 Noto Sans SC / 系统中文字体。

## 官方来源与完整文件校验

- 官方站：`https://www.alibabafonts.com/`
- 官方文件源：`https://fonts.alibabadesign.com/AlibabaPuHuiTi-2/`
- Light SHA-256：`e6466cfd557ab42b9de7f4a7c506790daad58ef7c1b46c0fa26812b0696a488e`
- Regular SHA-256：`3b6ddfd5b106e9da64ce586b7c10d61feeb7cac38a6eae8f5b30fc0035a03bf9`
- Medium SHA-256：`c738efb2e58c034cf349cd56d766444857e7cccc3d4a07f09f8321284d47648a`

完整字体只在本地临时目录用于生成，不进入仓库和发布包。仓库只保存生成后的 WOFF2 子集。

## 重建

使用 `scripts/build-font-subsets.py`，分别把官方 Light/Regular/Medium 传入 `--font`，以 `src/Szaipa.Web` 为 `--source-root` 重建。新增大量固定中文文案后应重跑，避免字符回退。

## 验证

- `.NET build`：0 warning / 0 error；tests：94/94；npm build：通过。
- FontTools 可读取三份生成文件，每份包含 1,261 个子集字形。
- 浏览器确认 300/400/500 三档 `document.fonts.check(...)` 均为 true，计算字体栈首选 `Alibaba PuHuiTi Local`。
- 页面不再包含 `repository.webfont.com` 脚本。
- 390×844 手机视口无横向溢出，导航四项横排单行；1280×720 桌面导航同样保持单行。

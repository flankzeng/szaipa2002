# Legacy Content 自动审计

- Content root: `/Users/arthur/Project/GitClone/web24.05/Content`
- 文件数：4523
- 总体积：1.1GB
- 静态精确引用：264
- 数据库导入引用：19
- 访问日志引用：0
- 待复核候选：1423 个文件 / 95.9MB

> `review-candidate` 不等于可删除。必须再结合生产日志、数据库路径导出和隔离回归。

## 顶层目录

| 目录 | 文件数 | 体积 | 分类 | 原因 |
|---|---:|---:|---|---|
| `newsImg` | 1401 | 520.1MB | protected-dynamic | 新闻封面、正文图片和后台富文本上传使用数据库动态路径 |
| `images` | 1245 | 426.8MB | protected-dynamic | 展览 FolderName/CoverPath/编号画廊及后台展览上传使用动态路径 |
| `ArtImg` | 322 | 112.6MB | protected-dynamic | 会员、作品、艺术新闻、拍卖及企业图片使用数据库动态路径 |
| `_preview` | 1186 | 70.8MB | review-candidate | 未发现当前输入中的精确引用，也不属于动态保护目录 |
| `TempFile` | 111 | 14.7MB | review-candidate | 未发现当前输入中的精确引用，也不属于动态保护目录 |
| `123` | 53 | 10.3MB | protected-runtime | favicon 和共享品牌资源 |
| `Award` | 94 | 6.8MB | review-candidate | 未发现当前输入中的精确引用，也不属于动态保护目录 |
| `Model` | 42 | 5.2MB | protected-runtime | 当前页面使用的 Bootstrap CSS、jQuery、Swiper、Magnify 等 |
| `js` | 13 | 2.4MB | review-candidate | 未发现当前输入中的精确引用，也不属于动态保护目录 |
| `fonts` | 9 | 1.5MB | protected-runtime | 原字体字形兜底 |
| `layui` | 15 | 855.6KB | review-candidate | 未发现当前输入中的精确引用，也不属于动态保护目录 |
| `Filme` | 3 | 452.9KB | review-candidate | 未发现当前输入中的精确引用，也不属于动态保护目录 |
| `CSS` | 9 | 224.0KB | protected-runtime | legacy publication.css 等当前明确样式依赖 |
| `icon` | 19 | 42.9KB | protected-runtime | 共享导航、社交和交互图标 |
| `.DS_Store` | 1 | 14.0KB | review-candidate | 未发现当前输入中的精确引用，也不属于动态保护目录 |

## 最大待复核文件（前 50）

| 文件 | 体积 |
|---|---:|
| `Award/images/1512618273559.mp4` | 2.5MB |
| `TempFile/6e59698329d7fd044ea2e1978714d026.png` | 1.3MB |
| `TempFile/f855b9c2217274a294ce6bc219972572.jpg` | 1.2MB |
| `TempFile/923d8e4513bb6b3619777ebd708aab59.png` | 1008.1KB |
| `TempFile/7d3bf095951b0de7f061ade4835d80e1.png` | 1008.1KB |
| `js/world.js` | 991.3KB |
| `js/echarts.min.js` | 868.1KB |
| `TempFile/c0ede28138291a09f84e86f2000ec932.jpg` | 828.9KB |
| `TempFile/95b8bf24fbb5c335b87073f842ab1342.jpg` | 824.2KB |
| `Award/js/org.1494058893.js` | 667.3KB |
| `TempFile/d6f7695db8ef2748574c00b165c447e0.jpg` | 505.2KB |
| `TempFile/29cf492f69f41bf51c8af4c8d8192e42.jpg` | 505.2KB |
| `TempFile/a1aa8669099a1f97d370ff52d4712810.jpg` | 505.2KB |
| `TempFile/c9ecce9740ad031a4db2b299bcc9d3b9.jpg` | 505.2KB |
| `TempFile/cc2ba8d716a83ad03e419faca9fa32d7.jpg` | 505.2KB |
| `TempFile/112be178db613a05f54fdee4f9086a25.jpg` | 505.2KB |
| `TempFile/207c94cec16ccf8e6b530b99815497ef.jpg` | 505.2KB |
| `TempFile/c4d30ef816a0113e0ea24e461a3108b5.jpg` | 505.2KB |
| `_preview/q30w1200/Content/artimg/Artist/Works/d11ad844e6ad10cb0368cd460e0eb3c8.jpg` | 395.0KB |
| `Filme/深圳艺术产业促进会章程.pdf` | 394.3KB |
| `_preview/q30w1200/Content/images/tonggou2024/100055.jpg` | 371.1KB |
| `Award/images/1511248948516.png` | 361.6KB |
| `Award/css/fonts/fontawesome-webfont.svg` | 306.1KB |
| `layui/font/iconfont.svg` | 299.4KB |
| `_preview/q30w1200/Content/artimg/Artist/works-narrow/ac260e2f-3c29-45c1-b326-2dcbab719a76.jpg` | 284.5KB |
| `js/layui.js` | 284.5KB |
| `layui/layui.js` | 284.5KB |
| `_preview/q30w1200/Content/artimg/Artist/Works/5f9be8494fd881f01a27ea818d499829.jpg` | 283.2KB |
| `_preview/q30w1200/Content/artimg/Artist/works-narrow/7e9f8336-bb21-4c2f-ae48-ff20e78fb9fd.jpg` | 278.8KB |
| `_preview/q30w1200/Content/artimg/Artist/Works/b5439ef3d6c0536878c5efa5e42515e1.jpg` | 259.7KB |
| `_preview/q30w1200/Content/artimg/Artist/works-narrow/bacbfd55-cc16-4796-b113-e4fd4bd70d4a.jpg` | 258.7KB |
| `_preview/q30w1200/Content/artimg/Artist/works-narrow/867861a5-a34c-47df-bfd1-8a6ce2636379.jpg` | 258.6KB |
| `_preview/q30w1200/Content/newsimg/e3fcea0ef34115b7993a39a3833cda4a.jpg` | 245.4KB |
| `_preview/q30w1200/Content/artimg/Artist/works-narrow/749f6375-8351-4700-b51b-45d77b3fb2ec.jpg` | 243.4KB |
| `_preview/q30w1200/Content/artimg/Artist/Works/zhanglanqian-04.jpg` | 234.5KB |
| `_preview/q30w1200/Content/artimg/selling/wyb.jpg` | 227.4KB |
| `_preview/q30w1200/Content/newsimg/54a70e9f6d895c3311c658bfb245cc4c.jpg` | 213.0KB |
| `_preview/q30w1200/Content/artimg/selling/zlq2.jpg` | 211.6KB |
| `_preview/q30w1200/Content/newsimg/b8fcfdf89324429a3337b51ae36f958e.jpg` | 208.1KB |
| `_preview/q30w1200/Content/artimg/Artist/Works/43aa41ca4d03122dfdb64754fe3b5113.jpg` | 205.3KB |
| `_preview/q30w1200/Content/artimg/Artist/Works/3324b5a6ef822a6b528c543df71fcf30.jpg` | 194.5KB |
| `_preview/q30w1200/Content/artimg/Artist/Fav/00014aec719e3498556dec7f932909b8.jpg` | 192.0KB |
| `_preview/q30w1200/Content/newsimg/a44394662623959774339d00ab0febbe.jpg` | 190.1KB |
| `_preview/q30w1200/Content/newsimg/c1bcb1c9b809adfc1437281194e2844c.jpg` | 189.7KB |
| `TempFile/48fbe46299e2304ff49248087e12cc9d.jpeg` | 189.2KB |
| `_preview/q30w1200/Content/artimg/Artist/Works/zhanglanqian-03.jpg` | 188.9KB |
| `_preview/q30w1200/Content/artimg/Artist/Works/cd0523a7a21134901980f3b28d97b2e9.jpg` | 186.6KB |
| `_preview/q30w1200/Content/newsimg/5445d374063a516a1086d0658a5d9b88.jpg` | 185.6KB |
| `_preview/q30w1200/Content/newsimg/e32e949755d04c51104e276ee32502b2.jpg` | 182.5KB |
| `_preview/q30w1200/Content/newsimg/614d924cbb90ddbd349e1772e0ab2a87.jpg` | 179.0KB |

## 大型受保护文件（>=5MB）

| 文件 | 体积 |
|---|---:|
| `images/艺促会介绍.pdf` | 23.0MB |

## 白名单中未在磁盘找到的路径（前 100）

- `images/temp/tori-回忆.png`（源码引用次数：0）
- `images/temp/烟伴随着思考.png`（源码引用次数：0）
- `images/temp/猎语机械芯-亚特兰蒂斯鱼人.png`（源码引用次数：0）
- `images/temp/聚宝盆系列之一.png`（源码引用次数：0）
- `images/temp/聚宝盆系列之三.png`（源码引用次数：0）
- `images/temp/聚宝盆系列之四.png`（源码引用次数：0）

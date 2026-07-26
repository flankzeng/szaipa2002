# View And Asset Map

Updated: 2026-07-26

## Modern public views

| Surface | Razor view | Page assets |
|---|---|---|
| Home | `Views/Home/Index.cshtml` | `css/index.css`, `js/index.js` |
| News list | `Views/Home/News.cshtml` | `css/news.css` |
| News detail | `Views/Home/NewsRead.cshtml` | `css/newsread.css`, `js/newsread.js` |
| Members | `Views/Home/Vip.cshtml` | `css/vip.css` |
| About | `Views/Home/About.cshtml` | `css/about.css` |
| Artist | `Views/Home/Art.cshtml` | `css/art.css`, `js/art.js` |
| Artist archive/article | `ArtArchive.cshtml`, `ArtArticle.cshtml` | `css/artist-content.css` |
| Exhibitions | `PublicationList.cshtml`, `Publication.cshtml` | publication/exhibition bundles |

Shared layouts are `_PublicLayout.cshtml` and `_ArtistLayout.cshtml`. Modern view and asset filenames do not use the migration-era `new` prefix.

## Runtime asset boundary

- Modern versioned CSS, JavaScript, local font subsets and derived AVIF assets live in `src/Szaipa.Web/wwwroot`.
- The existing release `Content` tree remains external and is mounted read-only at `/Content` for public assets.
- `ILegacyImagePreviewResolver` may select an existing q30 preview; missing or unsafe paths fall back to the original URL.
- `IDerivedImageResolver` serves only allowlisted modern derived assets and preserves the original `/Content` fallback.
- Legacy resources are removed only after source, database and production-log evidence agree; the server and old release remain read-only references.

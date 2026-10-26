using Szaipa.Data.Models.Home;

namespace Szaipa.Data.Composition;

/// <summary>
/// The homepage 核心成员 / 顾问团队 / 在售艺术品 blocks and About 合作伙伴 as they were hand-written in the Razor
/// templates before they became data-driven. This is both the FALLBACK (used when the OrgMember / SaleWork /
/// Partner tables do not exist yet, or are completely empty) and the single source of the seed rows in
/// docs/sql/2026-10-26-org-and-home-blocks.sql (a test keeps the two in sync). Rendering the defaults through
/// the data-driven templates reproduces the previous page.
/// </summary>
public static class HomeBlocksDefaults
{
    /// <summary>The "详情请咨询微信" line printed under every 在售艺术品 caption.</summary>
    public const string SaleContactLine = "详情请咨询微信:13265623041(添加前请标注来意)";

    public static IReadOnlyList<OrgMemberModel> CoreMembers { get; } = new[]
    {
        new OrgMemberModel("张岚芊", "会长", "艺术家", 1000, "/Content/images/to black/vip-2zlq.png", null, null),
        new OrgMemberModel("姚建伟", "荣誉会长", "教授", 1009, "/Content/images/to black/vip-1yjw.png", null, null),
        new OrgMemberModel("徐赓良", "副会长", "艺术家", 1011, "/Content/ArtImg/Artist/16a38c7aa1b0fc91afc363be8fe2a8b7.png", null, null),
        new OrgMemberModel("陈硕子", "副会长", "艺术家", 1049, "/Content/ArtImg/Artist/629c4dd2-1960-49dc-91ba-abeaf8f7373d.jpg", null, null),
        new OrgMemberModel("王玉波", "监事", "艺术家", 1012, "/Content/images/to black/vip-5wyb.png", null, null),
        new OrgMemberModel("沈敬东", "会员", "艺术家", 1015, "/Content/images/to black/vip-7sjd.png", 824, 824),
        new OrgMemberModel("程晓光", "策展人", "艺术家", 1014, "/Content/images/to black/vip-6cxg.png", null, null),
        new OrgMemberModel("邓国源", "会员", "艺术家", 1017, "/Content/images/to black/vip-9dgy.png", null, null),
        new OrgMemberModel("祁志龙", "会员", "艺术家", 1019, "/Content/images/to black/vip-11qzl.png", 800, 800),
        new OrgMemberModel("杨宽", "会员", "考古学家", 1021, "/Content/images/to black/vip-12yk.png", 800, 800),
    };

    public static IReadOnlyList<OrgMemberModel> AdvisoryMembers { get; } = new[]
    {
        new OrgMemberModel("黄笃", "策展人", "评论家", 1013, "/Content/images/to black/vip-3hd.png", null, null),
        new OrgMemberModel("盛伯骥", "视觉艺术家", "艺术顾问", 1051, "/Content/ArtImg/Artist/bac65c41-b7be-40ab-aad3-7178cc1b6062.png", null, null),
    };

    public static IReadOnlyList<SaleWorkModel> SaleWorks { get; } = new[]
    {
        new SaleWorkModel(1049, "陈硕子", "冲气", "85x58cm", "纸本设色", "/Content/ArtImg/selling/cxz.jpg", 1726, 1224),
        new SaleWorkModel(1012, "王玉波", "繁花系列", "60x60cm", "布面丙烯", "/Content/ArtImg/selling/wyb.jpg", 1224, 1238),
        new SaleWorkModel(1011, "徐赓良", "喵的网络", "45x45cm", "水墨", "/Content/ArtImg/selling/xgl.jpg", 1226, 1224),
        new SaleWorkModel(1000, "张岚芊", "山海经-7", "88x43cm", "综合材料", "/Content/ArtImg/selling/zlq1.jpg", 1663, 3350),
        new SaleWorkModel(1000, "张岚芊", "山海经-2", "88x43cm", "综合材料", "/Content/ArtImg/selling/zlq2.jpg", 2392, 3345),
        new SaleWorkModel(1048, "左锋义", "梵之初系列之55", "100x100cm", "纸本设色（综合材料）", "/Content/ArtImg/selling/zfy.jpg", 1224, 1246),
    };

    public static IReadOnlyList<PartnerModel> Partners { get; } = new[]
    {
        new PartnerModel(
            "荷兰白立方基金会",
            "/Content/images/white cube.png", 800, 800, null,
            "白立方基金会以举办每两年一次的地球村艺术项目而闻名，“会面”是其长久以来的主题，即来自世界各地的艺术家与公众会面。\n\n项目于荷兰、法国、德国、丹麦、阿塞拜疆和北马其顿等地举办。\n\n除了这个项目，白立方还组织了其他主要在欧盟范围内的展览和项目。促进艺术家与公众之间的文化交流。\n\n荷兰白立方拥有全世界1500名专业的艺术家，同时拥有可以接触到24000名艺术家的地球村艺术家网络，以发挥其巨大的艺术影响力。"),
        new PartnerModel(
            "游戏室画廊",
            "/Content/images/play room.png", 800, 800, null,
            "PLAY ROOM 的发起人是荷兰知名策展人Jeroen van Paassen，他在 PLAY room 汇集了20多年在艺术和文化领域的经验。\n\n从开发新概念和策划展览到收藏分析和管理。PLAY room 是临近荷兰首都阿姆斯特丹的小城市赞丹市中心的艺术热点。\n\n画廊的空间极具特色，展厅风格充满激情，具有幽默感而又平易近人，反差感时常令人感到诧异。\n\n画廊每月都有从当代艺术到街头艺术的各种风格迥异的展览。"),
        new PartnerModel(
            "华汇中艺国际拍卖有限公司",
            "/Content/images/huahui.png", 800, 800, null,
            "北京华汇中艺国际拍卖有限公司成立于2017年，是以经营文物艺术品为主的综合性拍卖公司。\n\n公司成立以来，已与多家国内顶尖拍卖公司、国内知名艺术品投资机构、大型银行私人银行部建立起良好的合作关系，\n\n为其提供艺术品拍卖专场合作、媒体宣传推广、艺术品藏家接洽、艺术品投资讲座等全方位服务。公司主营业务艺术品拍卖、艺术空间运营以及艺术品私人洽购。"),
        new PartnerModel(
            "深圳反转舞台\n文化传播有限公司",
            "/Content/images/fanzhuan.png", 800, 800, null,
            "深圳反转舞台文化传播有限公司\n是一家立足于深圳的舞台剧、影视剧、艺术展览策划企业，倡导多元文化、青年创意、轻文艺生活，努力为公众奉上多样化、高品格的文化娱乐作品。\n\n反转舞台亦常年致力于艺术教育，曾与福田文化馆、南山文化馆、深圳滨海艺术中心等单位合作。目前正在运营的课程有即兴喜剧工作坊、多维喜剧创新坊、集体编作戏剧工作坊等。"),
    };
}

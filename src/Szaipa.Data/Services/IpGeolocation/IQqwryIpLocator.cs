namespace Szaipa.Data.Services.IpGeolocation;

/// <summary>
/// 懒加载的 qqwry IP → 省/市 定位器。数据文件（qqwry.dat）缺失或损坏时 <see cref="Lookup"/> 返回 null，
/// <see cref="IsAvailable"/> 为 false —— 调用方据此降级（回退历史省市字段或标为"未知"），绝不抛异常。
/// </summary>
public interface IQqwryIpLocator
{
    /// <summary>是否有可用的 qqwry 数据文件（首次调用会触发懒加载）。</summary>
    bool IsAvailable { get; }

    /// <summary>IPv4 定位。无效 IP / 定位失败 / 文件缺失时返回 null。</summary>
    IpRegion.Result? Lookup(string? ip);
}

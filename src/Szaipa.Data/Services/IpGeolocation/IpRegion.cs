using System.Text;

namespace Szaipa.Data.Services.IpGeolocation;

/// <summary>
/// 纯真 qqwry.dat 离线 IP 定位器（零网络依赖）。线程安全：构造时一次性读入内存，之后只读 + 结果缓存。
/// 兼容两种记录布局：经典（endIP 后有 0x01/0x02 模式字节）与新版内联（endIP 后直接是 GBK 国家串，
/// 首字节 ≥0x81）。地区串可能为「中国–广东–深圳」破折号格式或「广东省深圳市」拼接格式。
/// 定位失败/文件缺失时安全返回 null，不抛异常。
/// </summary>
/// <remarks>
/// 移植自 legacy MVC5 的 <c>Szaipa.Security.IpRegion</c>（已在真实 qqwry.dat 上验证）。modern 版唯一
/// 差异：.NET Core 需先 <see cref="CodePagesEncodingProvider"/> 注册 GBK 编码才能解码 qqwry 的 GBK 文本
/// （System.Text.Encoding.CodePages 随共享框架提供，无需额外包引用）。
/// </remarks>
public sealed class IpRegion
{
    /// <summary>一次定位结果。字段为 UTF-8 解码后的文本。</summary>
    public sealed class Result
    {
        /// <summary>国家/地区（境外时为识别到的国家名）。</summary>
        public string Nation = "";

        /// <summary>省 / 直辖市 / 自治区（境内时）。</summary>
        public string Province = "";

        /// <summary>市（境内时，可能为空如直辖市/直连）。</summary>
        public string City = "";

        /// <summary>原始地区串。</summary>
        public string Raw = "";

        /// <summary>是否判定为境内。</summary>
        public bool IsChina;
    }

    private readonly byte[] _d;
    private readonly uint _firstIdx;
    private readonly uint _lastIdx;
    private readonly Dictionary<uint, Result> _cache = new();

    private static readonly Encoding Gbk;

    static IpRegion()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Gbk = Encoding.GetEncoding("GBK");
    }

    private IpRegion(byte[] data)
    {
        _d = data;
        if (_d.Length < 8) throw new InvalidDataException("qqwry 文件过短");
        _firstIdx = B8u(_d, 0);
        _lastIdx = B8u(_d, 4);
        if (_firstIdx < 8 || _lastIdx < _firstIdx || _lastIdx + 7 > _d.Length)
            throw new InvalidDataException("qqwry 索引越界");
    }

    /// <summary>加载失败（缺失/损坏）返回 null，调用方自行降级。</summary>
    public static IpRegion? TryLoad(string? filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
            return new IpRegion(File.ReadAllBytes(filePath));
        }
        catch
        {
            return null;
        }
    }

    private static uint B8u(byte[] b, int i) => (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));
    private static uint B3u(byte[] b, int i) => (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16));

    private int ScanStrEnd(int off)
    {
        while (off >= 0 && off < _d.Length && _d[off] != 0) off++;
        return off;
    }

    private string ReadStr(int off)
    {
        if (off < 0 || off >= _d.Length) return string.Empty;
        int end = ScanStrEnd(off);
        return end == off ? string.Empty : Gbk.GetString(_d, off, end - off);
    }

    private static bool TryIpToUint(string ip, out uint v)
    {
        v = 0;
        if (string.IsNullOrWhiteSpace(ip)) return false;
        var parts = ip.Trim().Split('.');
        if (parts.Length != 4) return false;
        uint acc = 0;
        for (var i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i], out var seg) || seg < 0 || seg > 255) return false;
            // qqwry 以 4 字节小端存 IP，B8u 读出的值 = 网络序数值，因此这里按 "a.b.c.d" 逐字节拼接。
            acc = (acc << 8) | (uint)seg;
        }
        v = acc;
        return true;
    }

    /// <summary>IPv4 定位。无效 IP 或定位失败返回 null。</summary>
    public Result? Lookup(string ip)
    {
        if (!TryIpToUint(ip, out var key)) return null;
        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;
        }
        var r = LookupCore(key);
        if (r != null)
        {
            lock (_cache) _cache[key] = r;
        }
        return r;
    }

    private Result? LookupCore(uint key)
    {
        int lo = 0, hi = (int)((_lastIdx - _firstIdx) / 7);
        long recordPos = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int off = (int)(_firstIdx + (uint)mid * 7);
            uint startIp = B8u(_d, off);
            if (key < startIp)
            {
                hi = mid - 1;
            }
            else
            {
                recordPos = B3u(_d, off + 4);
                lo = mid + 1;
            }
        }
        if (recordPos < 0) recordPos = 8; // 落在首记录前，回退到数据区起点
        return ResolveRecord((int)recordPos, key);
    }

    private Result? ResolveRecord(int pos, uint key)
    {
        if (pos < 0 || pos + 4 >= _d.Length) return null;
        int p = pos + 4;
        byte b0 = _d[p];

        string country, area;
        if (b0 == 0x01)
        {
            // 重定向：3 字节偏移指向位置记录
            int redirect = (int)B3u(_d, p + 1);
            if (redirect < 0 || redirect >= _d.Length) return null;
            if (_d[redirect] == 0x02)
            {
                country = ReadStr((int)B3u(_d, redirect + 1));
                area = ReadArea(redirect + 4);
            }
            else
            {
                int end = ScanStrEnd(redirect);
                country = ReadStr(redirect);
                area = ReadArea(end + 1);
            }
        }
        else if (b0 == 0x02)
        {
            // 国家偏移模式
            country = ReadStr((int)B3u(_d, p + 1));
            area = ReadArea(p + 4);
        }
        else
        {
            // 内联国家串：首字节即串内容（0x00 视为空国家）
            int end = ScanStrEnd(p);
            country = ReadStr(p);
            area = ReadArea(end + 1);
        }

        // 新版 qqwry 的国家串已是「中国–广东–深圳」完整省市，地区(area)字段只是 ISP 噪音，且常以 0x02
        // 模式字节开头 —— 直接拼接会把垃圾带进市名（如「深圳有」「云浮?」）。因此优先只解析 country：
        // 只要国家串本身能解析出中国省份就直接返回。老拼接格式（广东省深圳市XX电信，省市并入国家串）
        // 或国家串只有国家名时才拼上 area 兜底。
        var parsed = Parse(country, country);
        if (parsed.IsChina && !string.IsNullOrEmpty(parsed.Province)) return parsed;

        var raw = (country + area).Trim();
        if (raw.Length == 0) return null;
        return Parse(raw, country);
    }

    // 地区字段：0x01/0x02=重定向（3 字节偏移），否则内联串（首字节即内容，含 ASCII 与 0x00 空）。
    private string ReadArea(int pos)
    {
        if (pos < 0 || pos >= _d.Length) return string.Empty;
        var b = _d[pos];
        if (b == 0x01 || b == 0x02)
        {
            int redirect = (int)B3u(_d, pos + 1);
            return ReadLocation(redirect);
        }
        return ReadStr(pos);
    }

    // 重定向目标位置：可能是 0x02（country 偏移）或内联串。
    private string ReadLocation(int off)
    {
        if (off < 0 || off >= _d.Length) return string.Empty;
        if (_d[off] == 0x02) return ReadStr((int)B3u(_d, off + 1));
        return ReadStr(off);
    }

    // ------------------------------------------------------------------ 省/市/境外 解析

    private static readonly string[] Provinces =
    {
        "新疆维吾尔自治区", "内蒙古自治区", "广西壮族自治区", "西藏自治区", "宁夏回族自治区",
        "香港特别行政区", "澳门特别行政区", "黑龙江省", "广东省", "四川省", "云南省", "贵州省",
        "陕西省", "甘肃省", "青海省", "河北省", "山西省", "辽宁省", "吉林省", "江苏省", "浙江省",
        "安徽省", "福建省", "江西省", "山东省", "河南省", "湖北省", "湖南省", "海南省", "台湾省",
        "北京市", "天津市", "上海市", "重庆市"
    };

    // 分隔符：新版 qqwry 用 en-dash（–）分隔 国家–省–市。
    private static readonly string[] Separators = { "–", "—", "-", "|" };

    private static Result Parse(string raw, string country)
    {
        var r = new Result { Raw = raw };
        raw = Clean(raw);
        if (raw.Length == 0)
        {
            r.IsChina = false;
            r.Nation = string.IsNullOrEmpty(country) ? "境外" : Clean(country);
            return r;
        }

        // 特殊占位 → 境外
        if (raw.IndexOf("IANA") >= 0 || raw.IndexOf("保留") >= 0 || raw.IndexOf("局域网") >= 0
            || raw.IndexOf("本机") >= 0 || raw.IndexOf("测试") >= 0 || raw.IndexOf("本地") >= 0)
        {
            r.IsChina = false;
            r.Nation = raw;
            return r;
        }

        // 破折号格式：中国–广东–深圳 / 美国–州–市
        var parts = raw.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var first = Clean(parts[0]);
            if (first == "中国" || first == "China" || first == "中华人民共和国")
            {
                r.IsChina = true;
                r.Nation = "中国";
                r.Province = NormalizeProvince(parts.Length >= 2 ? parts[1] : null) ?? "";
                r.City = CleanCity(parts.Length >= 3 ? parts[2] : null);
                return r;
            }

            var prov1 = NormalizeProvince(first);
            if (prov1 != null)
            {
                r.IsChina = true;
                r.Province = prov1;
                r.City = CleanCity(parts.Length >= 3 ? parts[2] : null);
                return r;
            }

            r.IsChina = false;
            r.Nation = first;
            return r;
        }

        // 兜底：拼接老格式（广东省深圳市…）前缀匹配
        var prov = HitAny(raw, Provinces);
        if (prov != null)
        {
            r.IsChina = true;
            r.Province = prov;
            int idx = raw.IndexOf(prov, StringComparison.Ordinal);
            r.City = CleanCity(raw.Substring(idx + prov.Length));
            return r;
        }

        r.IsChina = false;
        r.Nation = string.IsNullOrEmpty(country) ? "境外" : Clean(country);
        return r;
    }

    /// <summary>剔除控制字符（含 qqwry 记录里的 0x02 等杂质），保留可打印文本。</summary>
    private static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch >= 0x20) sb.Append(ch);
        }
        return sb.ToString().Trim();
    }

    /// <summary>城市清洗：净化 + 迭代剥离 ISP/组织尾缀。</summary>
    private static string CleanCity(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = Clean(s);
        if (s.Length == 0) return s;
        var isps = new[]
        {
            "阿里巴巴", "阿里云", "腾讯云", "百度云", "电信", "联通", "移动", "铁通", "网通",
            "长城宽带", "广电", "华数", "教育网", "科技", "网络", "信息港", "数据中心", "IDC",
            "anycast", "公共DNS", "DNS服务器", "DNS", "服务器", "节点", "公司", "运营"
        };
        bool changed;
        do
        {
            changed = false;
            foreach (var isp in isps)
            {
                if (s.Length > isp.Length && s.EndsWith(isp, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(0, s.Length - isp.Length).Trim();
                    changed = true;
                }
            }
        } while (changed);
        return s;
    }

    // 短省名 → 标准名（含直辖市/自治区/特别行政区）。
    private static readonly Dictionary<string, string> ProvinceShorts = new()
    {
        { "北京", "北京市" }, { "天津", "天津市" }, { "上海", "上海市" }, { "重庆", "重庆市" },
        { "河北", "河北省" }, { "山西", "山西省" }, { "辽宁", "辽宁省" }, { "吉林", "吉林省" },
        { "黑龙江", "黑龙江省" }, { "江苏", "江苏省" }, { "浙江", "浙江省" }, { "安徽", "安徽省" },
        { "福建", "福建省" }, { "江西", "江西省" }, { "山东", "山东省" }, { "河南", "河南省" },
        { "湖北", "湖北省" }, { "湖南", "湖南省" }, { "广东", "广东省" }, { "海南", "海南省" },
        { "四川", "四川省" }, { "贵州", "贵州省" }, { "云南", "云南省" }, { "陕西", "陕西省" },
        { "甘肃", "甘肃省" }, { "青海", "青海省" }, { "台湾", "台湾省" },
        { "内蒙古", "内蒙古自治区" }, { "广西", "广西壮族自治区" }, { "西藏", "西藏自治区" },
        { "宁夏", "宁夏回族自治区" }, { "新疆", "新疆维吾尔自治区" },
        { "香港", "香港特别行政区" }, { "澳门", "澳门特别行政区" }
    };

    /// <summary>把 qqwry 的短省名（广东/北京/内蒙古）规范化成标准名；识别失败返回 null。</summary>
    public static string? NormalizeProvince(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        s = Clean(s);
        if (s.Length == 0) return null;
        foreach (var p in Provinces)
        {
            if (s == p) return p;
        }
        foreach (var kv in ProvinceShorts)
        {
            if (s.StartsWith(kv.Key, StringComparison.Ordinal)) return kv.Value;
        }
        return null;
    }

    private static string? HitAny(string text, string[] list)
    {
        if (string.IsNullOrEmpty(text)) return null;
        foreach (var s in list)
        {
            if (text.IndexOf(s, StringComparison.Ordinal) >= 0) return s;
        }
        return null;
    }
}

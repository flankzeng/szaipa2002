using Microsoft.AspNetCore.StaticFiles;

namespace Szaipa.Web.Infrastructure;

public static class StaticAssetContentTypes
{
    public static FileExtensionContentTypeProvider CreateProvider()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".avif"] = "image/avif";
        return provider;
    }
}

using OneWare.SDK.Services;

namespace OneWare.Core.Services;

public class PackageService : IPackageService
{
    private readonly IHttpService _httpService;

    public PackageService(IHttpService httpService)
    {
        _httpService = httpService;
    }
}
using System.Diagnostics;
using OneWare.PackageManager.Exceptions;
using OneWare.PackageManager.Internal;

namespace OneWare.PackageManager.Services
{
    public class PackageVersion
    {
        public string Url { get; set; }
        public string RelativePath { get; set; }
    }
    /// <summary>
    /// Resolves packages using a manifest served by a web server.
    /// Manifest consists of package versions and URLs, separated by space, one line per version.
    /// </summary>
    public class WebPackageResolver : IPackageResolver
    {
        private readonly HttpClient _httpClient;
        private readonly string _manifestUrl;

        /// <summary>
        /// Initializes an instance of <see cref="WebPackageResolver"/>.
        /// </summary>
        public WebPackageResolver(HttpClient httpClient, string manifestUrl)
        {
            _httpClient = httpClient.GuardNotNull(nameof(httpClient));
            _manifestUrl = manifestUrl.GuardNotNull(nameof(manifestUrl));
        }

        /// <summary>
        /// Initializes an instance of <see cref="WebPackageResolver"/>.
        /// </summary>
        public WebPackageResolver(string manifestUrl)
            : this(HttpClientEx.GetSingleton(), manifestUrl)
        {
        }

        private string ExpandRelativeUrl(string url)
        {
            var manifestUri = new Uri(_manifestUrl);
            var uri = new Uri(manifestUri, url);

            return uri.ToString();
        }

        [ItemCanBeNull]
        private async Task<IReadOnlyDictionary<Version, PackageVersion>> GetPackageVersionUrlMapAsync()
        {
            try
            {
                var map = new Dictionary<Version, PackageVersion>();

                // Get manifest
                var response = await _httpClient.GetStringAsync(_manifestUrl);

                foreach (var line in response.Split("\n"))
                {
                    var parts = line.Split("|");
                
                    // Get package version and URL
                    var versionText = parts.Length > 0 ? parts[0].Trim() : "";
                    var url = parts.Length > 1 ? parts[1].Trim() : "";
                    var realtivePath = parts.Length > 2 ? parts[2] : "";

                    // If either is not set - skip
                    if (versionText.IsNullOrWhiteSpace() || url.IsNullOrWhiteSpace())
                        continue;

                    // Try to parse version
                    if (!Version.TryParse(versionText, out var version))
                        continue;

                    // Expand URL if it's relative
                    url = ExpandRelativeUrl(url);

                    // Add to dictionary
                    map[version] = new PackageVersion(){RelativePath = realtivePath, Url = url};
                }
                return map;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                return null;
            }
        }

        /// <inheritdoc />
        [ItemCanBeNull]
        public async Task<IReadOnlyList<Version>> GetPackageVersionsAsync()
        {
            var versions = await GetPackageVersionUrlMapAsync();
            return versions?.Keys.ToArray() ?? null;
        }

        /// <inheritdoc />
        public async Task<PackageVersion> DownloadPackageAsync(Version version, string destFilePath,
            IProgress<double> progress = null, CancellationToken cancellationToken = default)
        {
            version.GuardNotNull(nameof(version));
            destFilePath.GuardNotNull(nameof(destFilePath));

            // Get map
            var map = await GetPackageVersionUrlMapAsync();

            // Try to get package URL
            var packageUrl = map.GetValueOrDefault(version);
            if (packageUrl == null)
                throw new PackageNotFoundException(version);

            // Download
            using var input = await _httpClient.GetFiniteStreamAsync(packageUrl.Url);
            using var output = File.Create(destFilePath);
            await input.CopyToAsync(output, progress, cancellationToken);

            return packageUrl;
        }
    }
}
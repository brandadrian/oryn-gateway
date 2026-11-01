using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ShopwareLauncher.Options;

namespace ShopwareLauncher.Services;

/// <summary>Verfuegbare Shopware-Versionen aus den Docker-Hub-Tags des Images (1h gecacht).</summary>
public sealed partial class VersionService(HttpClient http, IOptions<LauncherOptions> options)
{
    private const int MaxPages = 5;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<string> _cache = [];
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
    {
        if (_cache.Count > 0 && DateTimeOffset.UtcNow - _cachedAt < CacheDuration) return _cache;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.Count > 0 && DateTimeOffset.UtcNow - _cachedAt < CacheDuration) return _cache;

            var tags = new HashSet<string>();
            string? url = $"https://hub.docker.com/v2/repositories/{options.Value.Image}/tags?page_size=100";
            for (var page = 0; url is not null && page < MaxPages; page++)
            {
                var res = await http.GetFromJsonAsync<TagPage>(url, ct)
                          ?? throw new InvalidOperationException("Docker Hub: leere Antwort");
                foreach (var tag in res.Results)
                    if (VersionRegex().IsMatch(tag.Name)) tags.Add(tag.Name);
                url = res.Next;
            }

            _cache = [.. tags.OrderByDescending(Version.Parse)];
            _cachedAt = DateTimeOffset.UtcNow;
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    [GeneratedRegex(@"^6\.\d+\.\d+\.\d+$")]
    private static partial Regex VersionRegex();

    private sealed record TagPage(
        [property: JsonPropertyName("results")] List<Tag> Results,
        [property: JsonPropertyName("next")] string? Next);

    private sealed record Tag([property: JsonPropertyName("name")] string Name);
}

using System.Text.Json.Serialization;

namespace Energoreestr.Import;

public record PortalResource(string Name, string Url, string Format, DateTime ModifiedAt);

public record PortalDataset(string Title, string? UpdateFrequency, List<PortalResource> Resources);

/// <summary>
/// Клієнт до data.gov.ua. Портал працює на CKAN, тому метадані набору віддає
/// стандартний метод package_show.
/// </summary>
public class DataGovClient(HttpClient http)
{
    public async Task<PortalDataset> GetDatasetAsync(string packageId, CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<PackageShowResponse>(
            $"api/3/action/package_show?id={packageId}", ct);

        var package = response?.Result
            ?? throw new InvalidOperationException($"Портал не повернув набір {packageId}");

        var resources = package.Resources
            .Where(r => r.Url is not null)
            .Select(r => new PortalResource(
                r.Name ?? "",
                r.Url!,
                r.Format ?? "",
                // CKAN віддає час без зони, це UTC
                DateTime.SpecifyKind(r.LastModified ?? r.Created ?? default, DateTimeKind.Utc)))
            .ToList();

        return new PortalDataset(package.Title ?? packageId, package.UpdateFrequency, resources);
    }

    public Task<byte[]> DownloadAsync(string url, CancellationToken ct = default) =>
        http.GetByteArrayAsync(url, ct);

    // те, що нам потрібно з відповіді CKAN; решту полів пропускаємо
    private record PackageShowResponse(
        [property: JsonPropertyName("result")] Package? Result);

    private record Package(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("update_frequency")] string? UpdateFrequency,
        [property: JsonPropertyName("resources")] List<Resource> Resources);

    private record Resource(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("format")] string? Format,
        [property: JsonPropertyName("last_modified")] DateTime? LastModified,
        [property: JsonPropertyName("created")] DateTime? Created);
}

namespace ShopwareLauncher.Api;

public sealed record CreateInstanceRequest(string? Name, string? Version, bool AutoDelete);

public sealed record CreateInstanceResponse(string Name);

public sealed record ConfigResponse(string Domain, int MaxInstances, int TtlMinutes);

public sealed record ErrorResponse(string Error);

/// <summary>Instanz fuer die UI. Felder ohne Container (Image wird noch geladen) sind null.</summary>
public sealed record InstanceDto
{
    public required string Name { get; init; }
    public string? Version { get; init; }
    public string? State { get; init; }
    public required string Status { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset? Created { get; init; }
    public string? Expires { get; init; }
    public string? Url { get; init; }
    public string? LocalUrl { get; init; }
    public string? AdminUser { get; init; }
    public string? AdminPassword { get; init; }
    public string? SshHost { get; init; }
    public int? SshPort { get; init; }
    public string? SshUser { get; init; }
    public string? SshPassword { get; init; }
}

using System.Text.Json.Serialization;

namespace ShopwareLauncher.Docker;

// Ausschnitte der Docker Engine API (PascalCase wie die API selbst).

public sealed record ContainerSummary(
    string Id,
    Dictionary<string, string> Labels,
    string State,
    long Created);

public sealed record ContainerInspect(ContainerState State);

public sealed record ContainerState(DateTimeOffset StartedAt);

public sealed record CreateContainerRequest(
    string Image,
    IReadOnlyList<string> Env,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyDictionary<string, object> ExposedPorts,
    HostConfig HostConfig);

public sealed record HostConfig(
    string NetworkMode,
    IReadOnlyDictionary<string, IReadOnlyList<PortBinding>> PortBindings,
    RestartPolicy RestartPolicy);

public sealed record PortBinding(string HostPort);

public sealed record RestartPolicy(string Name);

public sealed record IdResponse(string Id);

public sealed record ExecRequest(
    IReadOnlyList<string> Cmd,
    string User,
    bool AttachStdout = true,
    bool AttachStderr = true);

public sealed record ExecStartRequest(bool Detach = false, bool Tty = false);

public sealed record ExecInspect(int ExitCode);

public sealed record ExecResult(int ExitCode, string Output);

public sealed record PullProgress([property: JsonPropertyName("error")] string? Error);

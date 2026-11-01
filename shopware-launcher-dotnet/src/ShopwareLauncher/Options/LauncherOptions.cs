using System.ComponentModel.DataAnnotations;

namespace ShopwareLauncher.Options;

/// <summary>Konfiguration aus Section "Launcher" (Env: Launcher__Domain, Launcher__Password, ...).</summary>
public sealed class LauncherOptions
{
    public const string Section = "Launcher";

    [Required] public string DockerSocket { get; init; } = "/var/run/docker.sock";
    [Required] public string Domain { get; init; } = "homelab.brandadrian.com";
    [Required] public string Image { get; init; } = "dockware/dev";
    [Required] public string Network { get; init; } = "proxy";
    [Range(1, 20)] public int MaxInstances { get; init; } = 3;
    [Range(1, 65535)] public int SshPortStart { get; init; } = 22001;
    [Range(1, 10080)] public int AutoDeleteMinutes { get; init; } = 60;
    [Required] public string User { get; init; } = "admin";

    [Required(ErrorMessage = "Launcher__Password fehlt (.env)")]
    [MinLength(8)]
    public string Password { get; init; } = "";
}

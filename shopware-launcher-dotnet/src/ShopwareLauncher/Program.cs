using Microsoft.Extensions.Options;
using ShopwareLauncher.Api;
using ShopwareLauncher.Docker;
using ShopwareLauncher.Infrastructure;
using ShopwareLauncher.Options;
using ShopwareLauncher.Services;

var builder = WebApplication.CreateBuilder(args);

// Konfiguration (appsettings.json + Env Launcher__*), beim Start validiert
builder.Services.AddOptions<LauncherOptions>()
    .Bind(builder.Configuration.GetSection(LauncherOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// HTTP-Clients: Docker ueber Unix-Socket (kein Timeout, Image-Pull/Setup dauern Minuten), Docker Hub
const string DockerHttp = "docker";
const string HubHttp = "dockerhub";
builder.Services.AddHttpClient(DockerHttp, c =>
    {
        c.BaseAddress = new Uri("http://docker");
        c.Timeout = Timeout.InfiniteTimeSpan;
    })
    .ConfigurePrimaryHttpMessageHandler(sp =>
        DockerClient.CreateHandler(sp.GetRequiredService<IOptions<LauncherOptions>>().Value.DockerSocket));
builder.Services.AddHttpClient(HubHttp, c => c.Timeout = TimeSpan.FromSeconds(30));

// Services halten Zustand (laufende Erstellungen, Cache, Locks) -> Singletons
builder.Services.AddSingleton(sp => new DockerClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient(DockerHttp)));
builder.Services.AddSingleton(sp => new VersionService(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient(HubHttp),
    sp.GetRequiredService<IOptions<LauncherOptions>>()));
builder.Services.AddSingleton<ShopwareSetup>();
builder.Services.AddSingleton<InstanceService>();
builder.Services.AddHostedService<InstanceHostedService>();

// Ungueltiges JSON -> BadHttpRequestException -> 400 {"error": ...}
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<BasicAuthMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapLauncherApi();

app.Run();

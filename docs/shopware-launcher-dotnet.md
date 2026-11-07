# Shopware-Launcher (.NET)

[← zurück zur README](../README.md) · [JS-Variante](shopware-launcher.md)

Gleiche Funktion, API und UI wie [shopware-launcher](shopware-launcher.md), umgesetzt mit **ASP.NET Core 10 Minimal API** (LTS), ohne NuGet-Pakete.

> Beide Varianten verwalten dieselben Container (Label `sw-launcher`) und SSH-Ports. **Nur eine laufen lassen.**

| Was | Wert |
|-----|------|
| URL | https://shopware-dotnet.oryn.brandadrian.com · lokal https://shopware-dotnet.localhost |
| Instanzen, SSH, Sicherheit | wie [JS-Variante](shopware-launcher.md) |

## Starten

```bash
cd shopware-launcher-dotnet
cp .env.example .env          # Launcher__Password setzen (min. 8 Zeichen)
docker compose up -d --build
```

Nach Code-Änderungen: `docker compose up -d --build`.

## Konfiguration

`appsettings.json` → Section `Launcher`, überschreibbar per Env `Launcher__<Name>`:
`Domain`, `Image`, `Network`, `MaxInstances`, `SshPortStart`, `AutoDeleteMinutes`, `User`, `Password`, `DockerSocket`.
Wird beim Start validiert (fehlendes Passwort → Start bricht ab).

## Code-Struktur

```
Dockerfile                          Multi-Stage: sdk:10.0 -> aspnet:10.0-alpine
src/ShopwareLauncher/
  Program.cs                        DI, HttpClients, Middleware, Endpoints
  appsettings.json
  Options/LauncherOptions.cs        Konfiguration + Validierung
  Docker/DockerClient.cs            Docker Engine API über Unix-Socket (SocketsHttpHandler)
  Docker/DockerModels.cs            Request/Response-Records
  Services/InstanceService.cs       Anlegen, Auflisten, Löschen, Auto-Löschen, Resume
  Services/InstanceHostedService.cs BackgroundService: Resume beim Start, Cleanup jede Minute
  Services/ShopwareSetup.cs         Warten auf dockware + Setup-Skript im Container
  Services/TraefikLabels.cs         Router/Middleware-Labels pro Instanz
  Services/VersionService.cs        Versionen von Docker Hub (1h Cache)
  Api/InstanceEndpoints.cs          /api/config, /api/versions, /api/instances
  Api/Dtos.cs
  Infrastructure/BasicAuthMiddleware.cs   Basic Auth (zeitkonstant)
  Infrastructure/ErrorHandlingMiddleware.cs  Exceptions -> {"error": "..."}
  Infrastructure/ApiException.cs
  wwwroot/                          gleiches Frontend wie die JS-Variante
```

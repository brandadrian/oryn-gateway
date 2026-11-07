# Shopware-Launcher

[← zurück zur README](../README.md)

Web-App unter **https://shopware.oryn.brandadrian.com** (lokal: https://shopware.localhost), um Shopware-Instanzen per Klick zu starten. Pro Instanz läuft ein [dockware/dev](https://hub.docker.com/r/dockware/dev)-Container (Shopware + MySQL + Demodaten + SSH, PHP, Composer, Node).

| Was | Wert |
|-----|------|
| Shop | `https://sw-<name>-<guid>.oryn.brandadrian.com` |
| Admin | `…/admin`, User `admin`, Passwort zufällig (steht in der UI) |
| Lokal | `https://sw-<name>-<guid>.localhost` |
| SSH / SFTP | `ssh -p 2200X dev@sw-<name>-<guid>.oryn.brandadrian.com`, Passwort in der UI |
| Projekt im Container | `/var/www/html` |
| Max. Instanzen | 3 (je ca. 1,5–2 GB RAM) |
| Auto-Löschen | optional nach 60 Min. |

Versionen werden live von Docker Hub geladen (`dockware/dev`, alle `6.x.x.x`-Tags). Der erste Start einer Version lädt das Image (~3 GB) und dauert entsprechend länger.

## Einrichten

1. Passwort setzen:

   ```bash
   cd shopware-launcher
   cp .env.example .env     # LAUNCHER_PASSWORD anpassen
   docker compose up -d
   ```

2. **SSH von extern:** Portweiterleitung `22001–22003` (TCP) in der Internet-Box auf `192.168.1.117` und in der Windows-Firewall freigeben:

   ```powershell
   New-NetFirewallRule -DisplayName "Shopware SSH" -Direction Inbound -Protocol TCP -LocalPort 22001-22003 -Action Allow
   ```

## Sicherheit

- Launcher ist per Basic Auth geschützt (er hat Vollzugriff auf Docker).
- Admin- und SSH-Passwort sind pro Instanz zufällig (nicht `admin/shopware` bzw. `dockware/dockware`).
- Adminer, Logs und Mailcatcher von dockware sind über Traefik gesperrt (403). Datenbank per SSH: `mysql -uroot -proot shopware`.

## Wie es funktioniert

- Der Launcher spricht über `/var/run/docker.sock` mit Docker (Node 22, ESM, keine npm-Abhängigkeiten).
- Container `sw-<name>-<guid>` im Netzwerk `proxy`, Traefik-Labels werden automatisch gesetzt.
- Nach dem Start: Apache meldet HTTPS an PHP (`X-Forwarded-Proto`), `APP_URL` auf https, zusätzliche Domain `.localhost`, Admin-Passwort setzen.
- Der Zustand steckt in Docker-Labels (`sw-launcher.*`). Startet der Launcher neu, wird das Setup für laufende Instanzen erneut ausgeführt.
- Auto-Löschen: Label `sw-launcher.expires`, ein Timer prüft jede Minute.
- Instanzen haben kein Volume: Löschen = alles weg.

## Code-Struktur

```
app/
  server.js                      Einstieg: Services verdrahten, HTTP-Server, Timer
  package.json                   ESM, "npm run check" = Syntax-Check
  src/
    config.js                    Konfiguration aus Umgebungsvariablen
    lib/docker-client.js         Docker Engine API über Unix-Socket
    lib/http.js                  JSON, Body, statische Dateien
    http/auth.js                 Basic Auth (zeitkonstant)
    http/router.js               Routen /api/*, Fehlerbehandlung
    services/instance-service.js Anlegen, Auflisten, Löschen, Auto-Löschen
    services/shopware-setup.js   Warten auf dockware + Setup-Skript im Container
    services/traefik-labels.js   Router/Middleware-Labels pro Instanz
    services/version-service.js  Versionen von Docker Hub (1h Cache)
  public/
    index.html, css/style.css
    js/api.js                    REST-Client
    js/render.js                 Daten -> HTML (escaped)
    js/app.js                    Formular, Liste, Polling
```

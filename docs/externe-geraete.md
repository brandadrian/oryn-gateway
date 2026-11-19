# Geräte ausserhalb von Docker (z.B. Synology-NAS)

[← zurück zur README](../README.md)

Docker-Apps brauchen keine Konfigurationsdatei: Traefik liest ihre **Labels** direkt am laufenden Container (siehe [neue-app.md](neue-app.md)).

Ein Gerät im Heimnetz, z.B. das NAS, ist aber kein Container. Es hat keine Labels, und Docker-Labels können Traefik nur auf Container zeigen lassen, nicht auf eine beliebige IP-Adresse. Für solche Geräte nutzt Traefik zusätzlich den **File-Provider**:

- In `docker-compose.yml`: `--providers.file.directory=/dynamic` und `--providers.file.watch=true`
- Der Ordner `traefik/dynamic/` wird nach `/dynamic` gemountet
- Jede `.yml`-Datei darin ist eine Route.
- Nach Änderungen: `docker restart traefik`. Unter Docker Desktop (Windows) erkennt Traefik Dateiänderungen im gemounteten Ordner nicht zuverlässig.

## Synology-NAS

| | |
|---|---|
| Öffentlich | https://nas.oryn.brandadrian.com |
| Lokal | http://192.168.1.120:5000 (DSM) bzw. https://192.168.1.120:5001 |
| Konfiguration | [traefik/dynamic/nas.yml](../traefik/dynamic/nas.yml) |

Traefik geht per HTTPS auf Port 5001 (selbstsigniertes Zertifikat, daher `insecureSkipVerify`). Über HTTP :5000 würde DSM per JavaScript auf `<host>:5001` umleiten, weil in DSM "HTTP auf HTTPS umleiten" aktiv ist. Port 5001 ist von aussen nicht offen.

Hinweis: Port 80 des NAS zeigt nur die Willkommensseite der Web Station, nicht die DSM-Anmeldung.

Da DSM damit öffentlich erreichbar ist: in DSM die Zwei-Faktor-Anmeldung und die automatische IP-Sperre aktivieren.

## Weiteres Gerät hinzufügen

Neue Datei `traefik/dynamic/<name>.yml`:

```yaml
http:
  routers:
    drucker:
      rule: Host(`drucker.oryn.brandadrian.com`)
      entryPoints:
        - websecure
      tls:
        certResolver: le
      service: drucker
  services:
    drucker:
      loadBalancer:
        servers:
          - url: http://192.168.1.50:80
```

Router- und Service-Namen müssen eindeutig sein. Kein DNS-Eintrag nötig (Wildcard).

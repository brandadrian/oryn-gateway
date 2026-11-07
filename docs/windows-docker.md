# Windows & Docker: Firewall, Netzwerk, Starten

[← zurück zur README](../README.md)

## Windows-Firewall

PowerShell als Administrator:

```powershell
New-NetFirewallRule -DisplayName "HTTP 80"  -Direction Inbound -Protocol TCP -LocalPort 80  -Action Allow
New-NetFirewallRule -DisplayName "HTTPS 443" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow
```

## Docker-Netzwerk (Pflicht, vor dem ersten Start!)

> ⚠️ **Muss einmalig ausgeführt werden, bevor du `docker compose up -d` startest.** Sonst kommt `network proxy not found`.

```bash
docker network create proxy
```

Prüfen: `docker network ls` → `proxy` muss in der Liste sein.

## Konfigurieren

- `docker-compose.yml` (Hauptordner): E-Mail für Let's Encrypt (`acme.email`, aktuell `brandadrian@gmail.com`). Wird nur für Ablauf-/Problem-Warnungen der Zertifikate verwendet.

> Wird die E-Mail geändert: `traefik/letsencrypt/acme.json` löschen und Traefik mit `docker compose up -d --force-recreate` neu starten. Sonst bleibt der alte ACME-Account mit der alten Mail gespeichert.

## Starten

```bash
cd C:\github\oryn-gateway
docker compose up -d

cd ../whoami
docker compose up -d

cd ../website
docker compose up -d
```

## Testen

- Lokal: https://website.localhost (siehe [Lokal testen](lokal-testen.md))
- Von außen: **Handy mit WLAN aus** (mobile Daten) → `https://website.oryn.brandadrian.com` und `https://whoami.oryn.brandadrian.com`. Aus dem eigenen WLAN klappt der Aufruf der öffentlichen Domain je nach Router nicht (NAT-Loopback), das ist kein Fehler.
- Zertifikat: Schloss im Browser muss „Let's Encrypt“ zeigen. Das erste Zertifikat kann nach dem Start ca. 1 Minute dauern.

## Labels geändert? → neu erstellen

Traefik liest Labels nur beim Erstellen des Containers. Nach jeder Änderung an einer `docker-compose.yml`:

```bash
docker compose up -d --force-recreate
```

HTML ändern: einfach `website/html/index.html` bearbeiten, nginx liefert die Änderung sofort aus (kein Neustart nötig).

Logs bei Problemen: `docker logs traefik`
